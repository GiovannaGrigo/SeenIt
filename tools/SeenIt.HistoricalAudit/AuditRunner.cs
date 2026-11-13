using System.Data;
using System.Diagnostics;
using System.Reflection;
using Npgsql;

namespace SeenIt.HistoricalAudit;

internal sealed class AuditRunner
{
    private readonly string host;
    private readonly int port;
    private readonly IAuditTarget? target;
    internal AuditRunner(string host="127.0.0.1",int port=15433) { this.host=host;this.port=port; }
    internal AuditRunner(IAuditTarget target) { this.target=target;host="127.0.0.1";port=15433; }
    internal async Task<AuditReport> RunAsync(bool evidence, CancellationToken ct,
        string schema = "public", bool diagnoseDriftForChecks = false)
    {
        if (schema is not ("public" or "b03b2_drift" or "b03b2_valid"))
            throw new AuditRejectedException("unsupported_schema");
        if(target is not null && (schema!="public" || diagnoseDriftForChecks || evidence))
            throw new AuditRejectedException("remote_scope_restricted");
        var report = new AuditReport { Mode=target?.Mode ?? "local-synthetic" };
        var queries = evidence ? new[] { "Q0", "Q1", "Q2", "Q3", "Q4", "Q5", "QFinal" } : ["Q0", "Q1", "Q2", "Q3", "Q4", "QFinal"];
        if (evidence) report.EvidenceReasonCode = "investigate-invalid-feelings";
        else report.Skipped.Add("Q5: disabled by default");
        report.Skipped.Add("Q6/Q7: synthetic checks only; no external catalog execution path");
        foreach (var query in queries)
        {
            var batch = new BatchReport(query);
            report.Batches.Add(batch);
            var watch = Stopwatch.StartNew();
            try
            {
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
                deadline.CancelAfter(TimeSpan.FromSeconds(12));
                await using var connection = target?.CreateConnection() ?? new NpgsqlConnection(LocalTarget.Connection(host, port));
                await connection.OpenAsync(deadline.Token);
                await using var begin = new NpgsqlCommand("BEGIN TRANSACTION ISOLATION LEVEL REPEATABLE READ READ ONLY", connection);
                await begin.ExecuteNonQueryAsync(deadline.Token);
                // Dispose/close also rolls back on failure; no COMMIT anywhere in the executor.
                try
                {
                    var ro = await ScalarAsync(connection, "SHOW transaction_read_only", deadline.Token);
                    if (!Equals(ro, "on")) throw new AuditRejectedException("read_only_required");
                    batch.ReadOnlyConfirmed = true;
                    if(target is null) await VerifyRoleAsync(connection, schema, deadline.Token);
                    else await target.VerifyAsync(connection,schema,deadline.Token);
                    batch.IdentityVerified=true;
                    batch.PrivilegesVerified=true;
                    batch.PrivilegeCoverage=target is null ? "Local B03-B2 role checks; not the remote allowlist."
                        : "B03-B2.3/PUBLIC-TEMP/1: connected database catalog allowlist; only nondelegable PUBLIC TEMP on identity-verified database exempt; direct/member TEMP denied; other connectable database CONNECT/TEMP denied. Built-in routine sandbox not claimed.";
                    // Remote policy constructs VerifyFull-only connections and verifies its configuration.
                    batch.TlsVerified=target is not null;
                    report.PostgreSqlVersion = connection.PostgreSqlVersion.ToString();
                    if (connection.PostgreSqlVersion.Major != 17) throw new AuditRejectedException("postgresql_17_required");
                    var sqlWatch=Stopwatch.StartNew();
                    var actual = await MetadataAsync(connection, schema, deadline.Token);
                    batch.MetadataDurationMs=sqlWatch.ElapsedMilliseconds;
                    if(query is "Q0" or "QFinal") batch.QueryDurationMs=batch.MetadataDurationMs;
                    batch.SchemaObjectsChecked = actual.GroupBy(x => x.Kind).ToDictionary(g => g.Key, g => g.Count());
                    var differences = SchemaContract.Differences(actual, schema);
                    batch.SchemaDifferences = differences.Count;
                    batch.SchemaDifferenceKinds = differences.Distinct().ToList();
                    batch.SchemaConformant = differences.Count == 0;
                    if (query == "Q0") report.SchemaValidatedInitially = batch.SchemaConformant;
                    if(query=="QFinal" || !batch.SchemaConformant) report.SchemaConformantAtEnd=batch.SchemaConformant;
                    if (!batch.SchemaConformant && !diagnoseDriftForChecks)
                        throw new AuditRejectedException("schema_drift_stop");
                    if (query is not ("Q0" or "QFinal"))
                    {
                        sqlWatch.Restart();
                        batch.EvaluatedRows = await CountsAsync(connection, schema, deadline.Token);
                        batch.DenominatorDurationMs=sqlWatch.ElapsedMilliseconds;
                        sqlWatch.Restart();
                        if (query == "Q5")
                        {
                            if (report.Batches.SelectMany(b => b.Findings ?? []).Any(f => f.Category == "H04_sentimento_enum" && f.Count > 0))
                            {
                                var rows = await ReadAsync(connection, Sql(query, schema), deadline.Token);
                                batch.OpaqueEvidence = rows.Select(r => ((Guid)r.Values.Single()!).ToString()).ToList();
                            }
                            else
                            {
                                batch.OpaqueEvidence = [];
                                report.Skipped.Add("Q5 evidence SELECT: no nonzero invalid-feelings aggregate");
                            }
                        }
                        else
                        {
                            var sets = await ReadSetsAsync(connection, Sql(query, schema), deadline.Token);
                            batch.Findings = sets[0].Select(r => Classify((string)r.Values.ElementAt(0)!, Convert.ToInt64(r.Values.ElementAt(1))))
                                .OrderBy(f=>f.Category,StringComparer.Ordinal).ToList();
                            if (query == "Q3") batch.Distribution = sets[1];
                        }
                        batch.QueryDurationMs=sqlWatch.ElapsedMilliseconds;
                    }
                    batch.Status = "completed";
                }
                catch(Exception error)
                {
                    batch.Failure=SafeFailure.Code(error);
                    throw;
                }
                finally
                {
                    if (connection.State == ConnectionState.Open)
                    {
                        try
                        {
                            using var cleanup=new CancellationTokenSource(TimeSpan.FromSeconds(5));
                            await using var rollback = new NpgsqlCommand("ROLLBACK", connection);
                            await rollback.ExecuteNonQueryAsync(cleanup.Token);
                            batch.RollbackConfirmed=true;
                        }
                        catch(Exception cleanupError)
                        {
                            batch.CleanupFailure=SafeFailure.Code(cleanupError);
                            if(batch.Failure is null) throw new AuditRejectedException("rollback_unconfirmed");
                        }
                    }
                    else if(batch.Failure is null) throw new AuditRejectedException("rollback_unconfirmed");
                }
            }
            catch (Exception error)
            {
                batch.Status = "incomplete";
                batch.Failure ??= SafeFailure.Code(error);
                batch.Findings = null;
                batch.EvaluatedRows = null;
                batch.Distribution = null;
                batch.OpaqueEvidence = null;
                report.Skipped.AddRange(queries.SkipWhile(q => q != query).Skip(1).Select(q => $"{q}: previous batch incomplete"));
                report.InterruptedQueries.Add(query);
                report.OmittedBatches.AddRange(queries.SkipWhile(q=>q!=query).Skip(1));
                if(batch.SchemaConformant || batch.SchemaDifferenceKinds is null) report.SchemaConformantAtEnd=null;
                break;
            }
            finally { batch.DurationMs = watch.ElapsedMilliseconds; }
        }
        report.AllBatchesReadOnly = report.Batches.Count>0 && report.Batches.All(b => b.ReadOnlyConfirmed);
        report.Complete = report.Batches.Count == queries.Length && report.Batches.All(b => b.Status == "completed");
        report.FinishedUtc = DateTimeOffset.UtcNow;
        return report;
    }

    internal static string Sql(string query, string schema)
    {
        if (schema is not ("public" or "b03b2_drift" or "b03b2_valid") || query is not ("Q0" or "Q1" or "Q2" or "Q3" or "Q4" or "Q5"))
            throw new AuditRejectedException("query_not_allowlisted");
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream($"SeenIt.HistoricalAudit.Sql.{query}.sql")!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().Replace("{{schema}}", schema, StringComparison.Ordinal);
    }

    internal static async Task<object?> ScalarAsync(NpgsqlConnection c, string sql, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand(sql, c);
        return await command.ExecuteScalarAsync(ct);
    }

    internal static async Task VerifyRoleAsync(NpgsqlConnection c, string schema, CancellationToken ct)
    {
        const string sql = """
            SELECT current_database()='seenit_b03b2' AND current_user='b03b2_reader'
              AND NOT r.rolsuper AND NOT r.rolcreatedb AND NOT r.rolcreaterole
              AND NOT r.rolreplication AND NOT r.rolbypassrls AND NOT r.rolinherit
              AND NOT EXISTS(SELECT 1 FROM pg_catalog.pg_auth_members m WHERE m.member=r.oid)
              AND NOT pg_catalog.has_database_privilege(current_database(),'CREATE')
              AND NOT pg_catalog.has_database_privilege(current_database(),'TEMP')
              AND NOT pg_catalog.has_schema_privilege(@schema,'CREATE')
              AND NOT EXISTS (
                SELECT 1 FROM pg_catalog.pg_class t JOIN pg_catalog.pg_namespace n ON n.oid=t.relnamespace
                WHERE n.nspname NOT IN ('pg_catalog','information_schema') AND n.nspname !~ '^pg_toast'
                  AND t.relkind IN ('r','p','v','m','f')
                  AND (t.relowner=r.oid OR pg_catalog.has_table_privilege(t.oid,'INSERT,UPDATE,DELETE,TRUNCATE,REFERENCES,TRIGGER')
                    OR pg_catalog.has_any_column_privilege(t.oid,'INSERT,UPDATE,REFERENCES')))
            FROM pg_catalog.pg_roles r WHERE r.rolname=current_user
            """;
        await using var command = new NpgsqlCommand(sql, c);
        command.Parameters.AddWithValue("schema", schema);
        if (!Equals(await command.ExecuteScalarAsync(ct), true))
            throw new AuditRejectedException("restricted_select_role_required");
    }

    private static async Task<List<(string Kind, string Signature)>> MetadataAsync(NpgsqlConnection c, string schema, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand(Sql("Q0", schema), c);
        command.Parameters.AddWithValue("schema", schema);
        await using var reader = await command.ExecuteReaderAsync(ct);
        List<(string, string)> result = [];
        while (await reader.ReadAsync(ct))
        {
            if (result.Count >= 512) throw new AuditRejectedException("metadata_limit");
            result.Add((reader.GetString(0), reader.GetString(1)));
        }
        return result;
    }

    private static async Task<Dictionary<string, long>> CountsAsync(NpgsqlConnection c, string schema, CancellationToken ct)
    {
        // Identifiers are fixed; schema has already passed exact allowlist validation.
        var sql = string.Join(" UNION ALL ", SchemaContract.Tables.Select(t => $"SELECT '{t}' AS table_name, count(*) AS rows FROM \"{schema}\".\"{t}\""));
        var rows = await ReadAsync(c, sql, ct);
        return rows.OrderBy(r=>Array.IndexOf(SchemaContract.Tables,(string)r["table_name"]!))
            .ToDictionary(r => (string)r["table_name"]!, r => (long)r["rows"]!);
    }

    internal static async Task<List<Dictionary<string, object?>>> ReadAsync(NpgsqlConnection c, string sql, CancellationToken ct)
        => (await ReadSetsAsync(c, sql, ct))[0];

    internal static async Task<List<List<Dictionary<string, object?>>>> ReadSetsAsync(NpgsqlConnection c, string sql, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand(sql, c);
        await using var reader = await command.ExecuteReaderAsync(ct);
        List<List<Dictionary<string, object?>>> sets = [];
        do
        {
            List<Dictionary<string, object?>> rows = [];
            while (await reader.ReadAsync(ct))
            {
                if (rows.Count >= 256) throw new AuditRejectedException("result_limit");
                Dictionary<string, object?> row = [];
                for (var i = 0; i < reader.FieldCount; i++)
                    row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                rows.Add(row);
            }
            sets.Add(rows);
        } while (await reader.NextResultAsync(ct));
        return sets;
    }

    private static Finding Classify(string category, long count)
    {
        var classification = category.StartsWith("H01") || category.StartsWith("H02") ? "C"
            : category.StartsWith("H10") || category.StartsWith("H14") ? "L"
            : category.StartsWith("H05") || category.StartsWith("H07") || category.StartsWith("H12") ? "M"
            : "D";
        var unit = category.Contains("grupos") || category.StartsWith("H05") ? "groups"
            : category == "H04_episodios_mais_cinco" ? "episodes" : "rows";
        var severity = classification == "L" ? "informative" : classification == "C" || category.StartsWith("H03") ? "high" : "review";
        return new Finding(category, count, unit, classification, severity);
    }
}
