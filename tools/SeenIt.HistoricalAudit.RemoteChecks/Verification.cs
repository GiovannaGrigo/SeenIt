using System.Diagnostics;
using System.Text.Json;
using Npgsql;
using SeenIt.HistoricalAudit;
using SeenIt.HistoricalAudit.Remote;

internal static partial class RemoteChecks
{
    private static async Task TlsAndIdentityAsync()
    {
        foreach(var (configuration,password,name) in new[]{
            (Configuration with{Host="127.0.0.1"},Secret,"wrong hostname rejected without data SELECT"),
            (Configuration with{RootCertificate=Path.Combine(TlsDirectory,"untrusted.crt")},Secret,"untrusted CA rejected without fallback"),
            (Configuration with{RootCertificate=Path.Combine(TlsDirectory,"malformed.crt")},Secret,"invalid CA certificate rejected"),
            (Configuration with{Port=15435},Secret,"expired server certificate rejected"),
            (Configuration,new string('x',32),"incorrect credentials rejected without data SELECT")})
        {
            var r=await RunAsync(configuration,password);
            Assert(!r.AuditComplete && r.Batches.All(b=>b.EvaluatedRows is null) && !r.ReadOnlyVerified,name);
        }
        foreach(var invalid in new[]{Configuration with{ExpectedSessionUser="postgres",ExpectedCurrentUser="postgres",ConnectionUser="postgres"},
            Configuration with{ExpectedCurrentUser="seenit_audit_other"},Configuration with{Host="https://example.invalid"},
            Configuration with{Host="localhost,example.invalid"},Configuration with{ExclusiveAuditIdentity=false}})
        {
            try {invalid.Validate();throw new CheckFailure("invalid_contract_accepted");}
            catch(AuditRejectedException){}
        }
        Assert(true,"owner API-style identities mismatched identity and unsafe endpoints rejected before connection");
        var builder=Configuration.Connection(Secret);
        Assert(builder.SslMode==SslMode.VerifyFull && builder.GssEncryptionMode==GssEncryptionMode.Disable && !builder.Pooling,
            "remote builder cannot downgrade TLS or enable parallel connection pool");
        var path=Path.Combine(TlsDirectory,"unknown-property.json");
        await File.WriteAllTextAsync(path,JsonSerializer.Serialize(Configuration).TrimEnd('}')+",\"TrustServerCertificate\":true}",Ct);
        try {RemoteConfiguration.Load(path);throw new CheckFailure("trust_override_accepted");}
        catch(JsonException){Assert(true,"unknown TLS and trust override properties rejected");}
        await CliAsync();
        await using var admin=await AdminAsync();
        await ExecuteAsync(admin,"GRANT seenit_audit_safe_group TO seenit_audit_synthetic");
        try
        {
            await using var c=new RemoteTarget(Configuration,Secret).CreateConnection();await c.OpenAsync(Ct);
            await ExecuteAsync(c,"BEGIN ISOLATION LEVEL REPEATABLE READ READ ONLY; SET ROLE seenit_audit_safe_group");
            try {await new RemoteTarget(Configuration,Secret).VerifyAsync(c,"public",Ct);throw new CheckFailure("effective_identity_mismatch_accepted");}
            catch(AuditRejectedException e){Assert(e.Message=="identity_or_session_settings_unconfirmed","server session_user and current_user mismatch blocks audit");}
            await ExecuteAsync(c,"ROLLBACK");
        }
        finally {await ExecuteAsync(admin,"REVOKE seenit_audit_safe_group FROM seenit_audit_synthetic");}
    }

    private static async Task CliAsync()
    {
        var dll=Path.Combine(Repository,"tools/SeenIt.HistoricalAudit.Remote/bin/Debug/net10.0/SeenIt.HistoricalAudit.Remote.dll");
        async Task<(int Code,string Output)> Invoke(params string[] arguments)
        {
            var start=new ProcessStartInfo("dotnet") {RedirectStandardOutput=true,RedirectStandardError=true,UseShellExecute=false,CreateNoWindow=true};
            start.ArgumentList.Add(dll);foreach(var a in arguments)start.ArgumentList.Add(a);
            start.Environment.Remove("SEENIT_AUDIT_REMOTE_PASSWORD");
            using var p=Process.Start(start)!;
            var output=p.StandardOutput.ReadToEndAsync(Ct);var error=p.StandardError.ReadToEndAsync(Ct);
            await p.WaitForExitAsync(Ct);await error;return(p.ExitCode,await output);
        }
        var config=Path.Combine(TlsDirectory,"remote.local.json");
        var disabled=await Invoke("--configuration="+config);
        Assert(disabled.Code==2 && !disabled.Output.Contains(Secret),"CLI refuses remote mode without explicit enable flag");
        var validated=await Invoke("--enable-remote","--configuration="+config);
        Assert(validated.Code==0 && validated.Output.Contains("\"connectionAttempted\":false"),"configuration validation does not connect or need secret");
        var missing=await Invoke("--enable-remote","--execute","--configuration="+config);
        Assert(missing.Code==2 && missing.Output.Contains("exclusive_secret_required"),"CLI execution requires separately provided secret");
    }

    private static async Task InterventionAsync(string setup,string cleanup,string name,bool inconclusive=false)
    {
        await using var admin=await AdminAsync();
        await ExecuteAsync(admin,setup);
        try
        {
            var r=await RunAsync();
            Assert(!r.AuditComplete && r.Batches[0].Failure==(inconclusive ? "privilege_coverage_inconclusive" : "privilege_allowlist_violation")
                && r.Batches.All(b=>b.EvaluatedRows is null),name);
        }
        finally {await ExecuteAsync(admin,cleanup);}
    }

    private static async Task PrivilegesAsync()
    {
        await InterventionAsync("GRANT SELECT(\"Email\") ON public.\"Users\" TO seenit_audit_synthetic",
            "REVOKE SELECT(\"Email\") ON public.\"Users\" FROM seenit_audit_synthetic","excess personal column privilege blocks before data");
        await InterventionAsync("GRANT SELECT ON public.\"Users\" TO seenit_audit_synthetic",
            "REVOKE SELECT ON public.\"Users\" FROM seenit_audit_synthetic; GRANT SELECT(\"Id\") ON public.\"Users\" TO seenit_audit_synthetic",
            "excess table-level SELECT blocks despite column allowlist");
        await InterventionAsync("GRANT SELECT(\"Email\") ON public.\"Users\" TO PUBLIC",
            "REVOKE SELECT(\"Email\") ON public.\"Users\" FROM PUBLIC","PUBLIC personal column grant detected effectively");
        foreach(var privilege in new[]{"UPDATE","MAINTAIN"})
            await InterventionAsync($"GRANT {privilege} ON public.\"UserSeries\" TO seenit_audit_synthetic",
                $"REVOKE {privilege} ON public.\"UserSeries\" FROM seenit_audit_synthetic",$"{privilege} effective privilege blocks remote audit");
        await PublicTemporaryAsync();
        await InterventionAsync("GRANT CONNECT ON DATABASE postgres TO seenit_audit_synthetic",
            "REVOKE CONNECT ON DATABASE postgres FROM seenit_audit_synthetic","access to other database blocks privilege proof");
        await InterventionAsync("REVOKE SELECT(\"Id\") ON public.\"Users\" FROM seenit_audit_synthetic",
            "GRANT SELECT(\"Id\") ON public.\"Users\" TO seenit_audit_synthetic","missing required SELECT yields incomplete not zero");
        await InterventionAsync("CREATE TABLE public.extra_personal(id integer); GRANT SELECT ON public.extra_personal TO seenit_audit_synthetic",
            "DROP TABLE public.extra_personal","excess access to other table blocks");
        await InterventionAsync("CREATE SEQUENCE public.extra_sequence; GRANT USAGE ON SEQUENCE public.extra_sequence TO seenit_audit_synthetic",
            "DROP SEQUENCE public.extra_sequence","sequence access blocks");
        await InterventionAsync("CREATE SCHEMA extra_schema; GRANT CREATE ON SCHEMA extra_schema TO seenit_audit_synthetic",
            "DROP SCHEMA extra_schema","CREATE on unrelated schema blocks");
        await InterventionAsync("CREATE FUNCTION public.elevated_test() RETURNS integer LANGUAGE sql SECURITY DEFINER AS 'SELECT 1'",
            "DROP FUNCTION public.elevated_test()","PUBLIC EXECUTE on custom SECURITY DEFINER function blocks without calling it");
        await InterventionAsync("CREATE FUNCTION pg_catalog.synthetic_elevated_test() RETURNS integer LANGUAGE sql SECURITY DEFINER AS 'SELECT 1'",
            "DROP FUNCTION pg_catalog.synthetic_elevated_test()","custom elevated function inside pg_catalog is not trusted as built-in");
        await InterventionAsync("ALTER ROLE seenit_audit_synthetic BYPASSRLS",
            "ALTER ROLE seenit_audit_synthetic NOBYPASSRLS","administrative attribute BYPASSRLS blocks");
        await InterventionAsync("GRANT pg_monitor TO seenit_audit_synthetic",
            "REVOKE pg_monitor FROM seenit_audit_synthetic","administrative role membership blocks even with NOINHERIT");
        await InterventionAsync("GRANT seenit_audit_safe_group TO seenit_audit_synthetic WITH ADMIN OPTION",
            "REVOKE seenit_audit_safe_group FROM seenit_audit_synthetic","role membership ADMIN OPTION blocks administrative delegation");
        await InterventionAsync("REVOKE SELECT ON pg_catalog.pg_parameter_acl FROM PUBLIC",
            "GRANT SELECT ON pg_catalog.pg_parameter_acl TO PUBLIC","unavailable privilege catalog is inconclusive and blocks",true);
        await using var admin=await AdminAsync();
        await ExecuteAsync(admin,"ALTER ROLE seenit_audit_synthetic INHERIT; GRANT seenit_audit_safe_group TO seenit_audit_synthetic; GRANT SELECT(\"Id\") ON public.\"Users\" TO seenit_audit_safe_group");
        try
        {
            Assert((await RunAsync()).AuditComplete,"safe inherited privileges accepted only when within effective allowlist");
            await ExecuteAsync(admin,"GRANT SELECT(\"Email\") ON public.\"Users\" TO seenit_audit_safe_group");
            Assert(!(await RunAsync()).AuditComplete,"excess inherited column privilege detected");
        }
        finally {await ExecuteAsync(admin,"REVOKE ALL(\"Id\",\"Email\") ON public.\"Users\" FROM seenit_audit_safe_group; REVOKE seenit_audit_safe_group FROM seenit_audit_synthetic; ALTER ROLE seenit_audit_synthetic NOINHERIT");}
    }

    private static async Task StructuralAsync()
    {
        await using var admin=await AdminAsync();
        foreach(var (ddl,name) in new[]{
            ("ALTER TABLE b03b2_valid.\"EpisodiosAssistidos\" DISABLE TRIGGER ALL","disabled internal FK triggers detected in isolated schema"),
            ("DROP INDEX b03b2_valid.\"IX_Users_Email\"; CREATE UNIQUE INDEX \"IX_Users_Email\" ON b03b2_valid.\"Users\" (\"Email\" COLLATE \"C\")","index collation drift detected separately from column collation"),
            ("DROP INDEX b03b2_valid.\"IX_Users_Email\"; CREATE UNIQUE INDEX \"IX_Users_Email\" ON b03b2_valid.\"Users\" (\"Email\" DESC)","index per-key options drift detected")})
        {
            await using var tx=await admin.BeginTransactionAsync(Ct);
            await ExecuteAsync(admin,ddl);
            var rows=await MetadataAsync(admin,"b03b2_valid");
            Assert(SchemaContract.Differences(rows,"b03b2_valid").Count>0,name);
            await tx.RollbackAsync(Ct);
        }
        var baseRows=await MetadataAsync(admin,"public");
        Assert(SchemaContract.Differences(baseRows,"public").Count==0 && baseRows.Count(x=>x.Kind=="fk_trigger")==12,
            "principal schema validates twelve internal FK triggers and unchanged index contract");
    }
    private static async Task<List<(string Kind,string Signature)>> MetadataAsync(NpgsqlConnection c,string schema)
    {
        await using var command=new NpgsqlCommand(AuditRunner.Sql("Q0",schema),c);command.Parameters.AddWithValue("schema",schema);
        await using var r=await command.ExecuteReaderAsync(Ct);List<(string,string)> rows=[];
        while(await r.ReadAsync(Ct)) rows.Add((r.GetString(0),r.GetString(1)));return rows;
    }
    private static async Task FailureAsync()
    {
        await using(var c=new RemoteTarget(Configuration,Secret).CreateConnection())
        {
            await c.OpenAsync(Ct);await ExecuteAsync(c,"BEGIN ISOLATION LEVEL REPEATABLE READ READ ONLY");
            try {await ExecuteAsync(c,"UPDATE public.\"UserSeries\" SET \"Status\"=\"Status\"");throw new CheckFailure("read_only_write_accepted");}
            catch(PostgresException e){Assert(e.SqlState=="25006","TLS server rejects writes with SQLSTATE 25006");}
            await ExecuteAsync(c,"ROLLBACK");
            // Avoid a race between server statement_timeout and the equally long
            // Npgsql command timeout. Shorten only this synthetic session's server
            // deadline; production builder and audit limits remain unchanged.
            await ExecuteAsync(c,"SET statement_timeout='100ms'");
            try{await AuditRunner.ScalarAsync(c,"SELECT pg_catalog.pg_sleep(1)",Ct);throw new CheckFailure("timeout_missing");}
            catch(PostgresException e){Assert(e.SqlState=="57014","TLS statement timeout enforced without extending limits");}
            finally {await ExecuteAsync(c,"SET statement_timeout='5s'");}
            using var activeCancellation=new CancellationTokenSource(150);
            try {await AuditRunner.ScalarAsync(c,"SELECT pg_catalog.pg_sleep(6)",activeCancellation.Token);throw new CheckFailure("active_TLS_cancellation_missing");}
            catch(Exception e) when(e is OperationCanceledException or NpgsqlException)
            {Assert(true,"active TLS SELECT cancellation enforced");}
        }
        using(var cancel=new CancellationTokenSource())
        {
            cancel.Cancel();var r=await RunAsync(ct:cancel.Token);
            Assert(!r.AuditComplete && r.InterruptedQueries.Count==1 && r.OmittedBatches.Count==5 && r.Batches[0].Findings is null,
                "cancelled TLS audit records interrupted and omitted batches with null counts");
        }
        await using var admin=await AdminAsync();
        await using(var tx=await admin.BeginTransactionAsync(Ct))
        {
            await ExecuteAsync(admin,"LOCK TABLE public.\"EpisodiosAssistidos\" IN ACCESS EXCLUSIVE MODE");
            var r=await RunAsync();
            Assert(!r.AuditComplete && r.Batches.Last().Failure=="lock_timeout" && r.Batches.Last().Findings is null
                && r.SchemaValidatedInitially==true && r.SchemaConformantAtEnd is null && r.Batches.Last().RollbackConfirmed,
                "interrupted batch has confirmed rollback and unknown final schema not stale success");
            await tx.RollbackAsync(Ct);
        }
    }

    private static async Task<string> FingerprintAsync()
    {
        await using var admin=await AdminAsync();List<string> hashes=[];
        foreach(var schema in new[]{"public","b03b2_valid"})
        {
            foreach(var table in SchemaContract.Tables)
                hashes.Add((string)(await AuditRunner.ScalarAsync(admin,$"SELECT coalesce(md5(string_agg(h,'' ORDER BY h)),'empty') FROM (SELECT md5(to_jsonb(t)::text) h FROM {schema}.\"{table}\" t) v",Ct))!);
            var metadata=await MetadataAsync(admin,schema);
            hashes.Add(JsonSerializer.Serialize(metadata.OrderBy(x=>x.Kind).ThenBy(x=>x.Signature).Select(x=>new{x.Kind,x.Signature})));
        }
        return string.Join(':',hashes);
    }

    private static async Task VolumeAsync()
    {
        await using var admin=await AdminAsync();
        var baseDataset=Equals(await AuditRunner.ScalarAsync(admin,"SELECT count(*)=2 FROM public.\"Users\"",Ct),true);
        if(!baseDataset && !Equals(await AuditRunner.ScalarAsync(admin,"""
            SELECT (SELECT count(*) FROM public."Users")=1002
              AND (SELECT count(*) FROM public."UserSeries")=10006
              AND (SELECT count(*) FROM public."EpisodiosAssistidos")=200008
              AND (SELECT count(*) FROM public."EpisodiosAssistidosSentimentos")=400006
            """,Ct),true)) throw new CheckFailure("volume_fixture_requires_known_synthetic_dataset");
        if(baseDataset)
        {
            await using var tx=await admin.BeginTransactionAsync(Ct);
            await ExecuteAsync(admin,"""
                INSERT INTO public."Users" SELECT md5('volume-user-'||n)::uuid,'synthetic','volume-'||n||'@example.invalid','unused',NULL,'2020-01-01' FROM generate_series(1,1000)n;
                INSERT INTO public."UserSeries"("Id","UserId","ExternalSeriesId","Name","Status","AddedAtUtc","UpdatedAtUtc","IsAnime")
                SELECT md5('volume-series-'||u||'-'||s)::uuid,md5('volume-user-'||u)::uuid,10000+s,'synthetic',1+(s%3),'2020-01-01','2020-01-01',false
                FROM generate_series(1,1000)u CROSS JOIN generate_series(1,10)s;
                INSERT INTO public."EpisodiosAssistidos" SELECT md5('volume-episode-'||u||'-'||s||'-'||e)::uuid,
                md5('volume-user-'||u)::uuid,10000+s,(10000+s)*1000+e,1,e,'synthetic',0,'','2020-01-01'
                FROM generate_series(1,1000)u CROSS JOIN generate_series(1,10)s CROSS JOIN generate_series(1,20)e;
                INSERT INTO public."EpisodiosAssistidosSentimentos" SELECT md5('volume-episode-'||u||'-'||s||'-'||e)::uuid,f
                FROM generate_series(1,1000)u CROSS JOIN generate_series(1,10)s CROSS JOIN generate_series(1,20)e CROSS JOIN generate_series(1,2)f;
                """);
            await tx.CommitAsync(Ct);
        }
        var before=await FingerprintAsync();
        var report=await RunAsync();
        Assert(report.AuditComplete,"volume synthetic audit complete within original operational limits");
        var repeated=await RunAsync();
        Assert(Signature(report)==Signature(repeated),"volume repeated counts and distributions identical");
        Assert(before==await FingerprintAsync(),"volume full fixture fingerprint unchanged by both audits");
        var result=new{audit=report,syntheticRows=new{users=1002,library=10006,watched=200008,feelings=400006},
            measuredBatches=report.Batches.Select(b=>new{b.Query,b.DurationMs,b.QueryDurationMs,b.MetadataDurationMs,b.DenominatorDurationMs,b.Status,b.Failure}).OrderByDescending(b=>b.QueryDurationMs),
            timeouts=report.Batches.Count(b=>b.Failure is "timeout_or_cancelled" or "lock_timeout"),
            independentEvidence=new{fullFixtureFingerprintsEqual=true,repeatedResultsEqual=true},
            limitsAutomaticallyIncreased=false,limitations="Local Docker measurements do not predict Supabase shared database cost"};
        await File.WriteAllTextAsync(Path.Combine(Repository,"docs/B03-B21-VOLUME-AUDIT-REPORT.json"),JsonSerializer.Serialize(result,AuditReport.JsonOptions),Ct);
        Console.WriteLine("PASS: volume aggregate timing report written.");
    }
}
