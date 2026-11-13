using System.Data;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text.Json;
using Npgsql;
using SeenIt.HistoricalAudit;

return await Checks.MainAsync(args);

internal static class Checks
{
    private static readonly List<string> Passed = [];
    private static readonly CancellationTokenSource Deadline = new(TimeSpan.FromMinutes(3));
    private static CancellationToken Ct => Deadline.Token;
    private static string Admin => new NpgsqlConnectionStringBuilder
    {
        Host = "127.0.0.1", Port = 15433, Database = "seenit_b03b2", Username = "postgres",
        Password = "b03b2_synthetic_admin", Timeout = 3, CommandTimeout = 10, Pooling = false,
        IncludeErrorDetail = false, Options = "-c search_path=pg_catalog"
    }.ConnectionString;

    internal static async Task<int> MainAsync(string[] args)
    {
        try
        {
            if (args.Length != 1 || args[0] is not ("--prepare-only" or "--verify-only"))
                throw new Exception("Use --prepare-only or --verify-only");
            if (args[0] == "--prepare-only")
            {
                await PrepareAsync();
                Console.WriteLine("PASS: dedicated synthetic database provisioned; no migrations.");
                return 0;
            }
            await VerifyAsync();
            return 0;
        }
        catch (Exception e)
        {
            // Assertion names are static; database exceptions/messages are never printed.
            Console.WriteLine($"FAIL: {(e is CheckFailure ? e.Message : SafeFailure.Code(e))}");
            return 1;
        }
    }

    private static void Assert(bool condition, string name)
    {
        if (!condition) throw new CheckFailure(name);
        Passed.Add(name);
        Console.WriteLine($"PASS: {name}");
    }

    private static string Resource(string file)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream($"SeenIt.HistoricalAudit.Checks.{file}.sql")!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static async Task<NpgsqlConnection> OpenAsync(bool admin = false)
    {
        var connection = new NpgsqlConnection(admin ? Admin : LocalTarget.Connection());
        await connection.OpenAsync(Ct);
        return connection;
    }

    private static async Task ExecuteAsync(NpgsqlConnection c, string sql)
    {
        await using var command = new NpgsqlCommand(sql, c);
        await command.ExecuteNonQueryAsync(Ct);
    }

    private static async Task PrepareAsync()
    {
        await using var c = await OpenAsync(true);
        if (c.PostgreSqlVersion.Major != 17 ||
            !Equals(await AuditRunner.ScalarAsync(c, "SELECT current_database()='seenit_b03b2'", Ct), true))
            throw new CheckFailure("dedicated_database_required");
        // Preparation refuses to overwrite initialized or unrelated resources, even on loopback.
        var occupied = await AuditRunner.ScalarAsync(c, """
            SELECT EXISTS(SELECT 1 FROM pg_catalog.pg_class c JOIN pg_catalog.pg_namespace n ON n.oid=c.relnamespace
              WHERE n.nspname NOT IN ('pg_catalog','information_schema') AND n.nspname !~ '^pg_toast')
              OR EXISTS(SELECT 1 FROM pg_catalog.pg_roles WHERE rolname='b03b2_reader')
            """, Ct);
        if (!Equals(occupied, false)) throw new CheckFailure("preparation_requires_empty_dedicated_database");
        await using var transaction = await c.BeginTransactionAsync(Ct);
        await ExecuteAsync(c, """
            CREATE ROLE b03b2_reader LOGIN NOINHERIT NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS
              PASSWORD 'b03b2_synthetic_reader';
            REVOKE ALL ON DATABASE seenit_b03b2 FROM PUBLIC;
            GRANT CONNECT ON DATABASE seenit_b03b2 TO b03b2_reader;
            REVOKE ALL ON SCHEMA public FROM PUBLIC;
            ALTER ROLE b03b2_reader SET default_transaction_read_only=on;
            ALTER ROLE b03b2_reader SET statement_timeout='5s';
            ALTER ROLE b03b2_reader SET lock_timeout='1s';
            """);
        foreach (var schema in new[] { "public", "b03b2_valid" })
        {
            await ExecuteAsync(c, Resource("Schema").Replace("{{schema}}", schema));
            await ExecuteAsync(c, Resource("Valid").Replace("{{schema}}", schema));
        }
        await ExecuteAsync(c, Resource("Invalid"));
        await ExecuteAsync(c, "CREATE SCHEMA b03b2_drift");
        foreach (var table in SchemaContract.Tables)
        {
            await ExecuteAsync(c, $"CREATE TABLE b03b2_drift.\"{table}\" (LIKE b03b2_valid.\"{table}\" INCLUDING DEFAULTS)");
            await ExecuteAsync(c, $"INSERT INTO b03b2_drift.\"{table}\" SELECT * FROM b03b2_valid.\"{table}\"");
        }
        await ExecuteAsync(c, """
            INSERT INTO b03b2_drift."EpisodiosAssistidos" SELECT * FROM b03b2_drift."EpisodiosAssistidos" WHERE "ExternalEpisodeId"=1001;
            INSERT INTO b03b2_drift."UserSeries" SELECT * FROM b03b2_drift."UserSeries" WHERE "ExternalSeriesId"=101;
            INSERT INTO b03b2_drift."EpisodiosAssistidosSentimentos" VALUES ('20000000-0000-0000-0000-000000000001',1);
            UPDATE b03b2_drift."EpisodiosAssistidos" SET "UserId"='00000000-0000-0000-0000-000000000099' WHERE "ExternalEpisodeId"=1051;
            UPDATE b03b2_drift."UserSeries" SET "UserId"='00000000-0000-0000-0000-000000000099' WHERE "ExternalSeriesId"=102;
            INSERT INTO b03b2_drift."EpisodiosAssistidosSentimentos" VALUES ('20000000-0000-0000-0000-000000000099',1);
            """);
        foreach (var schema in new[] { "public", "b03b2_valid", "b03b2_drift" })
        {
            await ExecuteAsync(c, $"GRANT USAGE ON SCHEMA {schema} TO b03b2_reader");
            foreach (var table in SchemaContract.Tables)
                await ExecuteAsync(c, table == "Users"
                    ? $"GRANT SELECT (\"Id\") ON {schema}.\"Users\" TO b03b2_reader"
                    : $"GRANT SELECT ON {schema}.\"{table}\" TO b03b2_reader");
        }
        await transaction.CommitAsync(Ct);
    }

    private static long Count(AuditReport r, string category) => r.Batches.SelectMany(b => b.Findings ?? [])
        .Single(f => f.Category == category).Count;

    private static async Task VerifyAsync()
    {
        var before = await FingerprintAsync();
        var runner = new AuditRunner();
        var valid = await runner.RunAsync(false, Ct, "b03b2_valid");
        Assert(valid.Complete && valid.SchemaValidated, "valid schema matches all frozen signatures");
        Assert(valid.AllBatchesReadOnly, "every batch confirms READ ONLY before metadata and data SELECTs");
        Assert(valid.Batches.SelectMany(b => b.Findings ?? []).Where(f => f.Classification != "L").All(f => f.Count == 0),
            "valid fixtures produce zero structural domain and identity findings");
        Assert(Count(valid, "H10_historico_sem_biblioteca_valido") == 1 && Count(valid, "H14_anime_desconhecido_valido") == 1,
            "history without library and unknown anime are valid controls");
        var distribution = valid.Batches.Single(b => b.Query == "Q3").Distribution!;
        Assert(distribution.Count == 4 && distribution.Sum(x => (long)x["vinculos"]!) == 5,
            "manual statuses preserved in distribution without corruption labels");
        var report = await runner.RunAsync(false, Ct);
        Assert(report.Complete && report.SchemaValidated, "Q0-Q4 execute on PostgreSQL 17 principal schema");
        var expected = new Dictionary<string, long>
        {
            ["H01_episodio_grupos"]=0, ["H01_biblioteca_grupos"]=0, ["H01_sentimento_grupos"]=0,
            ["H02_episodio_sem_usuario"]=0,["H02_biblioteca_sem_usuario"]=0,["H02_sentimento_sem_episodio"]=0,
            ["H03_biblioteca_id"]=1,["H03_status"]=1,["H03_episodio_ids_posicao"]=1,["H03_personagem_negativo"]=1,
            ["H04_sentimento_enum"]=1,["H04_episodios_mais_cinco"]=1,["H07_zero_com_nome"]=1,["H07_positivo_nome_branco"]=1,
            ["H12_serie_nome_branco"]=1,["H12_episodio_nome_branco"]=1,["H12_poster_triagem"]=1,
            ["H12_data_assistido"]=2,["H12_datas_biblioteca"]=1,
            ["H10_historico_sem_biblioteca_valido"]=2,["H14_anime_desconhecido_valido"]=1,
            ["H05_episodios_com_varias_series"]=1,["H05_episodios_com_varias_posicoes"]=2,["H05_mesma_posicao_varios_ids"]=1
        };
        Assert(expected.All(k => Count(report, k.Key) == k.Value), "exact expected counts for every Q1-Q4 category");
        Assert(report.Batches.Single(b => b.Query == "Q1").EvaluatedRows!.Values.SequenceEqual(new long[] {2,6,8,6}),
            "evaluated row denominators are 2 users 6 library 8 watched 6 feelings");
        var again = await runner.RunAsync(false, Ct);
        Assert(Signature(report) == Signature(again), "repeated execution returns identical schema counts and distributions");
        Assert(report.Batches.All(b => b.OpaqueEvidence is null), "Q5 disabled by default");
        var evidence = await runner.RunAsync(true, Ct);
        Assert(evidence.Batches.Single(b => b.Query == "Q5").OpaqueEvidence?.Count == 1,
            "optional Q5 returns opaque identifiers only");
        // Large synthetic fixture proves the hard-coded LIMIT rather than a happy-path small result.
        await EvidenceLimitAsync();
        var stopped = await runner.RunAsync(false, Ct, "b03b2_drift");
        Assert(!stopped.Complete && !stopped.SchemaValidated && stopped.Batches.Count == 1 &&
            stopped.Batches[0].Failure == "schema_drift_stop", "Q0 detects missing PK FK and unique indexes and stops normal executor");
        var diagnostic = await runner.RunAsync(false, Ct, "b03b2_drift", diagnoseDriftForChecks:true);
        Assert(diagnostic.Complete && !diagnostic.SchemaValidated &&
            Count(diagnostic,"H01_episodio_grupos")==2 && Count(diagnostic,"H01_biblioteca_grupos")==2 &&
            Count(diagnostic,"H01_sentimento_grupos")==1 && Count(diagnostic,"H02_episodio_sem_usuario")==1 &&
            Count(diagnostic,"H02_biblioteca_sem_usuario")==1 && Count(diagnostic,"H02_sentimento_sem_episodio")==1,
            "isolated drift fixture detects exact duplicate groups and orphan rows");
        await SchemaDriftVariantsAsync();
        await BatchFailureAsync();
        await SyntheticCatalogAsync();
        Assert(before == await FingerprintAsync(), "full row and schema fingerprints unchanged by all audit runs");
        await SecurityAsync();
        await SnapshotAsync();
        Assert(before == await FingerprintAsync(), "test-owned mutations restored and principal schema never weakened");
        var result = new { audit = report, validControls = valid, driftStop = stopped,
            checks = Passed, externalNetworkCalls = 0, executorDataChanges = 0,
            decision = "APROVADO PARA REVISÃO" };
        var json = JsonSerializer.Serialize(result, AuditReport.JsonOptions);
        Assert(new[] { "CANARY", "example.invalid", "b03b2_synthetic", "Password", "ConnectionString" }
            .All(s => !json.Contains(s, StringComparison.OrdinalIgnoreCase)), "report contains no synthetic PII or secret canaries");
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../docs/B03-B21-LOCAL-REGRESSION-REPORT.json"));
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(result, AuditReport.JsonOptions), Ct);
        Console.WriteLine($"PASS: {Passed.Count} assertions; local aggregate report written.");
    }

    private static string Signature(AuditReport r) => JsonSerializer.Serialize(r.Batches.Select(b => new {
        b.Query,b.Status,b.ReadOnlyConfirmed,b.SchemaConformant,b.SchemaDifferences,b.EvaluatedRows,b.Findings,b.Distribution }));

    private static async Task<string> FingerprintAsync()
    {
        await using var c = await OpenAsync(true);
        List<string> hashes = [];
        foreach (var schema in new[] { "public", "b03b2_valid", "b03b2_drift" })
            foreach (var table in SchemaContract.Tables)
                hashes.Add((string)(await AuditRunner.ScalarAsync(c,
                    $"SELECT coalesce(md5(string_agg(row_hash,'' ORDER BY row_hash)),'empty') FROM (SELECT md5(to_jsonb(t)::text) AS row_hash FROM {schema}.\"{table}\" t) h", Ct))!);
        hashes.Add((string)(await AuditRunner.ScalarAsync(c, """
            SELECT md5(string_agg(signature,'' ORDER BY signature)) FROM (
              SELECT c.oid::text || c.relname::text || c.relkind::text || coalesce(pg_catalog.pg_get_indexdef(c.oid),'') AS signature
              FROM pg_catalog.pg_class c JOIN pg_catalog.pg_namespace n ON n.oid=c.relnamespace
              WHERE n.nspname IN ('public','b03b2_valid','b03b2_drift')
              UNION ALL SELECT k.oid::text || pg_catalog.pg_get_constraintdef(k.oid)
              FROM pg_catalog.pg_constraint k JOIN pg_catalog.pg_namespace n ON n.oid=k.connamespace
              WHERE n.nspname IN ('public','b03b2_valid','b03b2_drift')) s
            """, Ct))!);
        return string.Join(':', hashes);
    }

    private static async Task EvidenceLimitAsync()
    {
        await using var admin = await OpenAsync(true);
        await using var tx = await admin.BeginTransactionAsync(Ct);
        await ExecuteAsync(admin, """
            INSERT INTO b03b2_drift."EpisodiosAssistidos" SELECT
              ('30000000-0000-0000-0000-' || lpad(n::text,12,'0'))::uuid,
              '00000000-0000-0000-0000-000000000001'::uuid,101,2000+n,1,n,'synthetic',0,'','2020-01-01'::timestamptz
            FROM generate_series(1,25) n;
            INSERT INTO b03b2_drift."EpisodiosAssistidosSentimentos"
            SELECT ('30000000-0000-0000-0000-' || lpad(n::text,12,'0'))::uuid,99 FROM generate_series(1,25) n;
            """);
        // Same test-owned transaction sees uncommitted fixture; audit SQL itself remains fixed SELECT.
        var rows = await AuditRunner.ReadAsync(admin, AuditRunner.Sql("Q5","b03b2_drift"), Ct);
        Assert(rows.Count==20 && rows.All(r=>r.Count==1 && r.Values.Single() is Guid), "Q5 SQL caps evidence at twenty opaque UUIDs");
        await tx.RollbackAsync(Ct);
    }

    private static async Task SchemaDriftVariantsAsync()
    {
        // Each variant is transactional in the alternative schema; principal remains untouched.
        await using var admin = await OpenAsync(true);
        foreach (var (ddl, name) in new[] {
            ("ALTER TABLE b03b2_valid.\"UserSeries\" ALTER COLUMN \"Name\" TYPE varchar(241)","column type or length drift"),
            ("ALTER TABLE b03b2_valid.\"UserSeries\" ALTER COLUMN \"Name\" DROP NOT NULL","column nullability drift"),
            ("CREATE UNIQUE INDEX extra_partial ON b03b2_valid.\"UserSeries\" (\"Name\") WHERE false","unexpected partial index"),
            ("ALTER TABLE b03b2_valid.\"Users\" RENAME TO \"UsersMissing\"","missing table") })
        {
            await using var tx = await admin.BeginTransactionAsync(Ct);
            await ExecuteAsync(admin, ddl);
            // Metadata uses schema signatures in this transaction; expose only difference kinds/counts.
            await using var cmd = new NpgsqlCommand(AuditRunner.Sql("Q0","b03b2_valid"),admin);
            cmd.Parameters.AddWithValue("schema","b03b2_valid");
            List<(string,string)> rows=[];
            await using (var reader=await cmd.ExecuteReaderAsync(Ct))
                while(await reader.ReadAsync(Ct)) rows.Add((reader.GetString(0),reader.GetString(1)));
            Assert(SchemaContract.Differences(rows,"b03b2_valid").Count>0,$"Q0 detects {name}");
            await tx.RollbackAsync(Ct);
        }
    }

    private static async Task BatchFailureAsync()
    {
        await using var admin=await OpenAsync(true);
        await using var transaction=await admin.BeginTransactionAsync(Ct);
        // Test fixture only: hold a conflicting lock in the alternative schema.
        // The audit executor never takes an administrative lock.
        await ExecuteAsync(admin,"LOCK TABLE b03b2_valid.\"EpisodiosAssistidos\" IN ACCESS EXCLUSIVE MODE");
        var result=await new AuditRunner().RunAsync(false,Ct,"b03b2_valid");
        Assert(!result.Complete && result.Batches.Last().Failure=="lock_timeout" &&
            result.Batches.Last().Findings is null && result.Batches.Last().EvaluatedRows is null,
            "actual executor lock timeout returns incomplete null counts and skipped later batches");
        await transaction.RollbackAsync(Ct);
    }

    private static async Task SyntheticCatalogAsync()
    {
        await using var c = await OpenAsync();
        await ExecuteAsync(c,"BEGIN ISOLATION LEVEL REPEATABLE READ READ ONLY");
        Assert(Equals(await AuditRunner.ScalarAsync(c,"SHOW transaction_read_only",Ct),"on"),"Q6/Q7 synthetic SQL uses READ ONLY");
        var q6=await AuditRunner.ReadAsync(c,Resource("Q6").Replace("{{schema}}","public"),Ct);
        Assert((long)q6[0]["registros_comparados"]! == 5 && (long)q6[0]["ausentes_no_snapshot"]! == 1 &&
            (long)q6[0]["posicoes_divergentes"]! == 1,"Q6 synthetic removed episode and renumbered position counts");
        var q7=await AuditRunner.ReadSetsAsync(c,Resource("Q7").Replace("{{schema}}","public"),Ct);
        Assert((long)q7[1][0]["escolhas_positivas_comparadas"]! == 4 && (long)q7[1][0]["votos_elegiveis"]! == 3 &&
            (long)q7[1][0]["fora_do_elenco_atual"]! == 1,"Q7 principal cast guest cast old name and removed character counts");
        var validQ7=await AuditRunner.ReadSetsAsync(c,Resource("Q7").Replace("{{schema}}","b03b2_valid"),Ct);
        Assert(validQ7[0].Any(r=>(int)r["Status"]! == 1 && (int)r["derivado"]! == 2) &&
            validQ7[0].Any(r=>(int)r["Status"]! == 2 && (int)r["derivado"]! == 3),
            "Q7 manual InList and Watching remain distributions not corruption");
        var incomplete=Resource("Q6").Replace("{{schema}}","public").Replace("VALUES (101, true)","VALUES (101, false)");
        Assert((long)(await AuditRunner.ReadAsync(c,incomplete,Ct))[0]["registros_comparados"]! == 0,
            "incomplete synthetic episode response excluded from comparisons");
        var missingCast=Resource("Q7").Replace("{{schema}}","public").Replace("1001,true","1001,false").Replace("1002,true","1002,false");
        Assert((long)(await AuditRunner.ReadSetsAsync(c,missingCast,Ct))[1][0]["escolhas_positivas_comparadas"]! == 0,
            "incomplete synthetic cast never means invalid character");
        // Empty catalog and expanded series Finished are contextual, never corruption assertions.
        var expanded=Resource("Q7").Replace("{{schema}}","b03b2_valid")
            .Replace("(101,1001), (101,1002)","(104,1041), (104,1042)")
            .Replace("(101,true)","(103,true), (104,true)");
        var rows=(await AuditRunner.ReadSetsAsync(c,expanded,Ct))[0];
        Assert(rows.Any(r=>(int)r["Status"]! == 3 && r["derivado"] is null) &&
            rows.Any(r=>(int)r["Status"]! == 3 && r["derivado"] is 1),
            "Finished with empty or expanded catalog is review context only");
        await ExecuteAsync(c,"ROLLBACK");
    }

    private static async Task SecurityAsync()
    {
        foreach (var host in new[] {"db.supabase.co","https://production.invalid","localhost","127.0.0.1,remote","/tmp","127.0.0.2"})
        {
            try { LocalTarget.Connection(host); throw new CheckFailure("unsafe host accepted"); }
            catch(AuditRejectedException) { }
        }
        Assert(true,"external hosts URLs DNS aliases multi-host strings and sockets rejected before connection");
        foreach(var query in new[]{"DELETE","SELECT pg_sleep(1)","Q1; DROP TABLE","Q6"})
        {
            try { AuditRunner.Sql(query,"public"); throw new CheckFailure("unsafe SQL accepted"); }
            catch(AuditRejectedException) { }
        }
        Assert(true,"no free SQL path: DDL DML side-effect SELECTs and Q6 rejected by query allowlist");
        await using var c=await OpenAsync();
        await AuditRunner.VerifyRoleAsync(c,"public",Ct);
        Assert(true,"reader has no write privileges ownership role memberships CREATE or TEMP");
        await ExecuteAsync(c,"BEGIN READ ONLY");
        try { await ExecuteAsync(c,"UPDATE public.\"UserSeries\" SET \"Status\"=\"Status\""); throw new CheckFailure("read-only write allowed"); }
        catch(PostgresException e) { Assert(e.SqlState=="25006","server rejects DML in READ ONLY with SQLSTATE 25006"); }
        await ExecuteAsync(c,"ROLLBACK");
        await ExecuteAsync(c,"BEGIN READ WRITE");
        try { await ExecuteAsync(c,"UPDATE public.\"UserSeries\" SET \"Status\"=\"Status\""); throw new CheckFailure("SELECT role wrote data"); }
        catch(PostgresException e) { Assert(e.SqlState=="42501","SELECT-only role rejects DML even in READ WRITE with SQLSTATE 42501"); }
        await ExecuteAsync(c,"ROLLBACK");
        try { await ExecuteAsync(c,"CREATE TABLE public.forbidden_test(id integer)"); throw new CheckFailure("DDL allowed"); }
        catch(PostgresException e) { Assert(e.SqlState is "25006" or "42501","server rejects DDL"); }
        try { await AuditRunner.ScalarAsync(c,"SELECT \"Email\" FROM public.\"Users\"",Ct); throw new CheckFailure("PII privilege allowed"); }
        catch(PostgresException e) { Assert(e.SqlState=="42501","reader cannot SELECT user emails"); }
        try { await AuditRunner.ScalarAsync(c,"SELECT pg_catalog.pg_sleep(6)",Ct); throw new CheckFailure("timeout did not fire"); }
        catch(PostgresException e) { Assert(e.SqlState=="57014" && SafeFailure.Code(e)=="timeout_or_cancelled","statement timeout is incomplete failure never zero"); }
        using(var cancel=new CancellationTokenSource(100))
        {
            try { await AuditRunner.ScalarAsync(c,"SELECT pg_catalog.pg_sleep(6)",cancel.Token); throw new CheckFailure("cancellation did not fire"); }
            catch(Exception e) when(e is OperationCanceledException or NpgsqlException) { Assert(true,"command cancellation interrupts active SELECT"); }
        }
        using(var cancelled=new CancellationTokenSource())
        {
            cancelled.Cancel();
            var r=await new AuditRunner().RunAsync(false,cancelled.Token);
            Assert(!r.Complete && r.Batches[0].Findings is null && r.Batches[0].EvaluatedRows is null,
                "executor cancellation yields incomplete report with null counts and skipped queries");
        }
        // Reserve loopback TCP port without accepting PostgreSQL: deterministic startup timeout, no other service contacted.
        var listener=new TcpListener(IPAddress.Loopback,0);
        listener.Start();
        try
        {
            var r=await new AuditRunner(port:((IPEndPoint)listener.LocalEndpoint).Port).RunAsync(false,Ct);
            Assert(!r.Complete && r.Batches[0].Failure=="connection_or_command_failure" && r.Batches[0].Findings is null,
                "connection failure sanitized and incomplete rather than zero");
        }
        finally { listener.Stop(); }
        // A privileged or accidentally granted role must be rejected by executor preflight.
        await using var admin=await OpenAsync(true);
        await ExecuteAsync(admin,"GRANT UPDATE ON public.\"UserSeries\" TO b03b2_reader");
        try
        {
            var r=await new AuditRunner().RunAsync(false,Ct);
            Assert(!r.Complete && r.Batches[0].Failure=="restricted_select_role_required","executor refuses accidentally elevated reader");
        }
        finally { await ExecuteAsync(admin,"REVOKE UPDATE ON public.\"UserSeries\" FROM b03b2_reader"); }
    }

    private static async Task SnapshotAsync()
    {
        await using var reader=await OpenAsync();
        await using var admin=await OpenAsync(true);
        await ExecuteAsync(reader,"BEGIN ISOLATION LEVEL REPEATABLE READ READ ONLY");
        const string select="SELECT count(*) FROM b03b2_valid.\"UserSeries\" WHERE \"Status\"=99";
        var initial=await AuditRunner.ScalarAsync(reader,select,Ct);
        await ExecuteAsync(admin,"UPDATE b03b2_valid.\"UserSeries\" SET \"Status\"=99 WHERE \"ExternalSeriesId\"=102");
        try
        {
            Assert(Equals(initial,await AuditRunner.ScalarAsync(reader,select,Ct)),"repeatable-read snapshot unchanged after concurrent committed write");
            await ExecuteAsync(reader,"ROLLBACK");
            Assert(Equals(await AuditRunner.ScalarAsync(reader,select,Ct),1L),"new transaction sees concurrent commit");
        }
        finally { await ExecuteAsync(admin,"UPDATE b03b2_valid.\"UserSeries\" SET \"Status\"=2 WHERE \"ExternalSeriesId\"=102"); }
    }

    private sealed class CheckFailure(string name):Exception(name);
}
