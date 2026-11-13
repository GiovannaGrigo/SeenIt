using Npgsql;
using System.Text.Json;
using System.Text.Json.Nodes;
using SeenIt.HistoricalAudit;
using SeenIt.HistoricalAudit.Remote;

internal static partial class RemoteChecks
{
    private static async Task VerifyPublicTemporaryAsync()
    {
        await using var admin=await AdminAsync();
        // This action is solely for the dedicated loopback synthetic fixture.
        Assert(admin.PostgreSqlVersion.Major==17 && Equals(await AuditRunner.ScalarAsync(admin,
            "SELECT current_database()='seenit_b03b21'",Ct),true),"PUBLIC fixture requires isolated PostgreSQL 17 database");
        var before=await AuditRunner.ScalarAsync(admin,
            "SELECT datacl::text FROM pg_catalog.pg_database WHERE datname=current_database()",Ct);
        var schemaBefore=await AuditRunner.ScalarAsync(admin,
            "SELECT nspacl::text FROM pg_catalog.pg_namespace WHERE nspname='public'",Ct);
        Assert(Equals(await AuditRunner.ScalarAsync(admin,"""
            SELECT NOT EXISTS (
             SELECT 1 FROM pg_catalog.pg_database d,
              LATERAL pg_catalog.aclexplode(coalesce(d.datacl,pg_catalog.acldefault('d',d.datdba))) a
             WHERE d.datname=current_database() AND a.grantee=0)
             AND NOT EXISTS (
             SELECT 1 FROM pg_catalog.pg_namespace n,
              LATERAL pg_catalog.aclexplode(coalesce(n.nspacl,pg_catalog.acldefault('n',n.nspowner))) a
             WHERE n.nspname='public' AND a.grantee=0)
            """,Ct),true),"PUBLIC fixture starts with known empty PUBLIC database and schema grants");
        await ExecuteAsync(admin,"GRANT CONNECT,TEMP ON DATABASE seenit_b03b21 TO PUBLIC; GRANT USAGE ON SCHEMA public TO PUBLIC");
        try { await VerifyAsync(); }
        finally
        {
            await ExecuteAsync(admin,"REVOKE CONNECT,TEMP ON DATABASE seenit_b03b21 FROM PUBLIC; REVOKE USAGE ON SCHEMA public FROM PUBLIC");
            Assert(Equals(before,await AuditRunner.ScalarAsync(admin,
                "SELECT datacl::text FROM pg_catalog.pg_database WHERE datname=current_database()",Ct))
                && Equals(schemaBefore,await AuditRunner.ScalarAsync(admin,
                "SELECT nspacl::text FROM pg_catalog.pg_namespace WHERE nspname='public'",Ct)),
                "synthetic PUBLIC database and schema ACLs restored exactly");
        }
        var reportPath=Path.Combine(Repository,"docs/B03-B23-TLS-AUDIT-REPORT.json");
        var result=JsonNode.Parse(await File.ReadAllTextAsync(reportPath,Ct))!;
        result["checks"]=JsonSerializer.SerializeToNode(Passed);
        result["publicFixture"]=JsonSerializer.SerializeToNode(new {
            databasePrivileges=new[]{"CONNECT","TEMPORARY"},schemaPrivileges=new[]{"USAGE"},
            aclRestored=true,scope="isolated loopback PostgreSQL only",securityProfile="B03-B2.3/PUBLIC-TEMP/1"});
        await File.WriteAllTextAsync(reportPath,result.ToJsonString(AuditReport.JsonOptions),Ct);
        Console.WriteLine($"PASS: {Passed.Count} checks including PUBLIC ACL restoration; B03-B2.3 evidence finalized.");
    }

    private static async Task PublicTemporaryAsync()
    {
        await using var admin=await AdminAsync();
        var hadPublicTemp=Equals(await AuditRunner.ScalarAsync(admin,"""
            SELECT EXISTS (SELECT 1 FROM pg_catalog.pg_database d,
             LATERAL pg_catalog.aclexplode(coalesce(d.datacl,pg_catalog.acldefault('d',d.datdba))) a
             WHERE d.datname=current_database() AND a.grantee=0 AND a.privilege_type='TEMPORARY')
            """,Ct),true);
        await ExecuteAsync(admin,"GRANT TEMP ON DATABASE seenit_b03b21 TO PUBLIC");
        try
        {
            var report=await RunAsync();
            Assert(report.AuditComplete && report.Batches.All(b=>b.PrivilegesVerified && b.ReadOnlyConfirmed && b.RollbackConfirmed),
                "PUBLIC-only TEMP accepted with all six READ ONLY batches and rollback");
            await InterventionAsync("GRANT TEMP ON DATABASE seenit_b03b21 TO seenit_audit_synthetic",
                "REVOKE TEMP ON DATABASE seenit_b03b21 FROM seenit_audit_synthetic",
                "redundant direct TEMP rejected despite PUBLIC TEMP");
            await InterventionAsync("GRANT TEMP ON DATABASE seenit_b03b21 TO seenit_audit_synthetic WITH GRANT OPTION",
                "REVOKE TEMP ON DATABASE seenit_b03b21 FROM seenit_audit_synthetic",
                "direct TEMP grant option rejected despite PUBLIC TEMP");
            foreach(var inherit in new[]{false,true})
                await InterventionAsync($"ALTER ROLE seenit_audit_synthetic {(inherit ? "INHERIT" : "NOINHERIT")}; GRANT TEMP ON DATABASE seenit_b03b21 TO seenit_audit_safe_group; GRANT seenit_audit_safe_group TO seenit_audit_synthetic",
                    "REVOKE seenit_audit_safe_group FROM seenit_audit_synthetic; REVOKE TEMP ON DATABASE seenit_b03b21 FROM seenit_audit_safe_group; ALTER ROLE seenit_audit_synthetic NOINHERIT",
                    $"membership TEMP rejected with inherit={inherit} despite PUBLIC TEMP");
            await InterventionAsync("GRANT TEMP ON DATABASE seenit_b03b21 TO seenit_audit_safe_group; CREATE ROLE seenit_audit_intermediate NOLOGIN; GRANT seenit_audit_safe_group TO seenit_audit_intermediate; GRANT seenit_audit_intermediate TO seenit_audit_synthetic",
                "REVOKE seenit_audit_intermediate FROM seenit_audit_synthetic; DROP ROLE seenit_audit_intermediate; REVOKE TEMP ON DATABASE seenit_b03b21 FROM seenit_audit_safe_group",
                "transitive NOINHERIT membership TEMP rejected");
            await InterventionAsync("GRANT CREATE ON DATABASE seenit_b03b21 TO PUBLIC",
                "REVOKE CREATE ON DATABASE seenit_b03b21 FROM PUBLIC","PUBLIC CREATE still rejected with PUBLIC TEMP");
            await InterventionAsync("GRANT CREATE ON DATABASE seenit_b03b21 TO seenit_audit_synthetic",
                "REVOKE CREATE ON DATABASE seenit_b03b21 FROM seenit_audit_synthetic","direct CREATE still rejected with PUBLIC TEMP");
            await InterventionAsync("GRANT TEMP ON DATABASE postgres TO PUBLIC",
                "REVOKE TEMP ON DATABASE postgres FROM PUBLIC","PUBLIC TEMP on other connectable database rejected even without CONNECT");
            await InterventionAsync("CREATE FUNCTION public.invoker_test() RETURNS integer LANGUAGE sql AS 'SELECT 1'",
                "DROP FUNCTION public.invoker_test()","PUBLIC EXECUTE on custom invoker function still rejected with PUBLIC TEMP");
            await InterventionAsync("ALTER DATABASE seenit_b03b21 OWNER TO seenit_audit_synthetic",
                "ALTER DATABASE seenit_b03b21 OWNER TO postgres; GRANT CONNECT ON DATABASE seenit_b03b21 TO seenit_audit_synthetic","database ownership still rejected with PUBLIC TEMP");
            await InterventionAsync("ALTER TABLE public.\"UserSeries\" OWNER TO seenit_audit_synthetic",
                "ALTER TABLE public.\"UserSeries\" OWNER TO postgres; REVOKE ALL ON public.\"UserSeries\" FROM seenit_audit_synthetic; GRANT SELECT(\"Id\",\"UserId\",\"ExternalSeriesId\",\"Name\",\"PosterUrl\",\"Status\",\"IsAnime\",\"AddedAtUtc\",\"UpdatedAtUtc\") ON public.\"UserSeries\" TO seenit_audit_synthetic",
                "table ownership still rejected with PUBLIC TEMP");
            await ExecuteAsync(admin,"ALTER TABLE public.\"UserSeries\" ENABLE ROW LEVEL SECURITY");
            try
            {
                var drift=await RunAsync();
                Assert(!drift.AuditComplete && drift.SchemaValidatedInitially==false
                    && drift.Batches.All(b=>b.EvaluatedRows is null),"PUBLIC TEMP does not bypass Q0 RLS schema drift");
            }
            finally {await ExecuteAsync(admin,"ALTER TABLE public.\"UserSeries\" DISABLE ROW LEVEL SECURITY");}
            await TemporaryThreatAsync();
        }
        finally { if(!hadPublicTemp) await ExecuteAsync(admin,"REVOKE TEMP ON DATABASE seenit_b03b21 FROM PUBLIC"); }
        // No TEMP at all remains valid. TEMP from a role alone does not qualify.
        await ExecuteAsync(admin,"REVOKE TEMP ON DATABASE seenit_b03b21 FROM PUBLIC");
        try
        {
            Assert((await RunAsync()).AuditComplete,"absence of TEMP remains accepted");
            await InterventionAsync("GRANT TEMP ON DATABASE seenit_b03b21 TO seenit_audit_synthetic",
                "REVOKE TEMP ON DATABASE seenit_b03b21 FROM seenit_audit_synthetic","direct-only TEMP rejected without PUBLIC");
        }
        finally {if(hadPublicTemp) await ExecuteAsync(admin,"GRANT TEMP ON DATABASE seenit_b03b21 TO PUBLIC");}
    }

    private static async Task TemporaryThreatAsync()
    {
        await using var c=new RemoteTarget(Configuration,Secret).CreateConnection();
        await c.OpenAsync(Ct);
        await ExecuteAsync(c,"BEGIN ISOLATION LEVEL REPEATABLE READ READ ONLY");
        try
        {
            await ExecuteAsync(c,"CREATE TEMP TABLE b23_temporary_probe(id integer)");
            throw new CheckFailure("read_only_temp_creation_accepted");
        }
        catch(PostgresException e){Assert(e.SqlState=="25006","READ ONLY rejects CREATE TEMP despite PUBLIC TEMP");}
        finally {await ExecuteAsync(c,"ROLLBACK");}
        // Threat demonstration only in synthetic tests: SQL identity is not a sandbox.
        await ExecuteAsync(c,"BEGIN READ WRITE; CREATE TEMP TABLE b23_temporary_probe(id integer); INSERT INTO b23_temporary_probe VALUES (1); COMMIT");
        await ExecuteAsync(c,"BEGIN READ ONLY; INSERT INTO b23_temporary_probe VALUES (2)");
        Assert(Equals(await AuditRunner.ScalarAsync(c,"SELECT count(*) FROM pg_temp.b23_temporary_probe",Ct),2L),
            "identity can switch READ WRITE and READ ONLY permits writes to preexisting temp table");
        await ExecuteAsync(c,"ROLLBACK");
        await ExecuteAsync(c,"BEGIN READ WRITE");
        try
        {
            await ExecuteAsync(c,"UPDATE public.\"UserSeries\" SET \"Status\"=\"Status\"");
            throw new CheckFailure("persistent_write_privilege_present");
        }
        catch(PostgresException e){Assert(e.SqlState=="42501","persistent table UPDATE denied even outside READ ONLY");}
        finally {await ExecuteAsync(c,"ROLLBACK");}
        // Closing the nonpooled synthetic connection removes its temporary objects.
    }
}
