using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Npgsql;
using SeenIt.HistoricalAudit;
using SeenIt.HistoricalAudit.Remote;

return await RemoteChecks.MainAsync(args);

internal static partial class RemoteChecks
{
    private static readonly List<string> Passed=[];
    private static readonly CancellationTokenSource Deadline=new(TimeSpan.FromMinutes(5));
    private static CancellationToken Ct=>Deadline.Token;
    private static readonly string Repository=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../.."));
    private static string TlsDirectory=>Path.Combine(Repository,"tools/historical-audit/.synthetic-tls");
    private static string Secret=>File.ReadAllText(Path.Combine(TlsDirectory,"reader.secret"));
    private static RemoteConfiguration Configuration=>new() { Host="localhost",Port=15434,Database="seenit_b03b21",
        ConnectionUser="seenit_audit_synthetic",ExpectedSessionUser="seenit_audit_synthetic",ExpectedCurrentUser="seenit_audit_synthetic",
        RootCertificate=Path.Combine(TlsDirectory,"root.crt"),ExclusiveAuditIdentity=true };
    private static string Admin=>new NpgsqlConnectionStringBuilder {Host="127.0.0.1",Port=15434,Database="seenit_b03b21",
        Username="postgres",Password="b03b21_synthetic_admin",Timeout=3,CommandTimeout=30,Pooling=false,
        SslMode=SslMode.Disable,IncludeErrorDetail=false,Options="-c search_path=pg_catalog"}.ConnectionString;

    internal static async Task<int> MainAsync(string[] args)
    {
        try
        {
            if(args.Length!=1) throw new CheckFailure("one_explicit_action_required");
            switch(args[0])
            {
                case "--certificates": Certificates.Generate(TlsDirectory);Console.WriteLine("PASS: synthetic certificates generated; no connection attempted.");break;
                case "--prepare-only":await PrepareAsync();Console.WriteLine("PASS: TLS synthetic fixtures prepared separately.");break;
                case "--verify-only":await VerifyAsync();break;
                case "--verify-public-temp":await VerifyPublicTemporaryAsync();break;
                case "--volume":await VolumeAsync();break;
                default:throw new CheckFailure("unsupported_action");
            }
            return 0;
        }
        catch(Exception e)
        {
            Console.WriteLine("FAIL: "+(e is CheckFailure ? e.Message : SafeFailure.Code(e)));
            return 1;
        }
    }
    private static void Assert(bool value,string name)
    {
        if(!value) throw new CheckFailure(name);
        Passed.Add(name);Console.WriteLine("PASS: "+name);
    }
    private static async Task<NpgsqlConnection> AdminAsync()
    {
        var c=new NpgsqlConnection(Admin);await c.OpenAsync(Ct);return c;
    }
    private static async Task ExecuteAsync(NpgsqlConnection c,string sql)
    {
        await using var command=new NpgsqlCommand(sql,c);await command.ExecuteNonQueryAsync(Ct);
    }
    private static string Resource(string file)
    {
        using var s=Assembly.GetExecutingAssembly().GetManifestResourceStream($"SeenIt.HistoricalAudit.RemoteChecks.{file}.sql")!;
        using var r=new StreamReader(s);return r.ReadToEnd();
    }
    private static async Task PrepareAsync()
    {
        await using var c=await AdminAsync();
        if(c.PostgreSqlVersion.Major!=17 || !Equals(await AuditRunner.ScalarAsync(c,"SELECT current_database()='seenit_b03b21'",Ct),true))
            throw new CheckFailure("dedicated_synthetic_database_required");
        if(!Equals(await AuditRunner.ScalarAsync(c,"""
            SELECT EXISTS(SELECT 1 FROM pg_catalog.pg_class c JOIN pg_catalog.pg_namespace n ON n.oid=c.relnamespace
             WHERE n.nspname='public') OR EXISTS(SELECT 1 FROM pg_catalog.pg_roles WHERE rolname='seenit_audit_synthetic')
            """,Ct),false)) throw new CheckFailure("empty_synthetic_database_required");
        await using var transaction=await c.BeginTransactionAsync(Ct);
        await ExecuteAsync(c,"""
            CREATE ROLE seenit_audit_synthetic LOGIN NOINHERIT NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS;
            CREATE ROLE seenit_audit_safe_group NOLOGIN NOINHERIT;
            REVOKE ALL ON DATABASE seenit_b03b21 FROM PUBLIC;
            REVOKE CONNECT,TEMP ON DATABASE postgres,template1 FROM PUBLIC;
            REVOKE ALL ON SCHEMA public FROM PUBLIC;
            GRANT CONNECT ON DATABASE seenit_b03b21 TO seenit_audit_synthetic;
            GRANT USAGE ON SCHEMA public TO seenit_audit_synthetic;
            ALTER ROLE seenit_audit_synthetic SET default_transaction_read_only=on;
            """);
        // Password value is synthetic and parameterized with server-side format, never logged.
        await using(var passwordCommand=new NpgsqlCommand("SELECT format('ALTER ROLE seenit_audit_synthetic PASSWORD %L',@secret)",c))
        {
            passwordCommand.Parameters.AddWithValue("secret",Secret);
            var passwordDdl=(string)(await passwordCommand.ExecuteScalarAsync(Ct))!;
            await ExecuteAsync(c,passwordDdl);
        }
        await ExecuteAsync(c,Resource("Schema").Replace("{{schema}}","public"));
        await ExecuteAsync(c,Resource("Valid").Replace("{{schema}}","public"));
        await ExecuteAsync(c,Resource("Invalid"));
        await ExecuteAsync(c,Resource("Schema").Replace("{{schema}}","b03b2_valid"));
        await ExecuteAsync(c,Resource("Valid").Replace("{{schema}}","b03b2_valid"));
        await ExecuteAsync(c,"""
            GRANT SELECT("Id") ON public."Users" TO seenit_audit_synthetic;
            GRANT SELECT("Id","UserId","ExternalSeriesId","Name","PosterUrl","Status","IsAnime","AddedAtUtc","UpdatedAtUtc")
              ON public."UserSeries" TO seenit_audit_synthetic;
            GRANT SELECT ON public."EpisodiosAssistidos",public."EpisodiosAssistidosSentimentos" TO seenit_audit_synthetic;
            """);
        await transaction.CommitAsync(Ct);
        await File.WriteAllTextAsync(Path.Combine(TlsDirectory,"remote.local.json"),JsonSerializer.Serialize(Configuration),Ct);
    }
    private static Task<AuditReport> RunAsync(RemoteConfiguration? configuration=null,string? password=null,CancellationToken? ct=null)
        =>new AuditRunner(new RemoteTarget(configuration??Configuration,password??Secret)).RunAsync(false,ct??Ct);
    private static async Task VerifyAsync()
    {
        var fingerprint=await FingerprintAsync();
        var report=await RunAsync();
        Assert(report.AuditComplete && report.SchemaValidatedInitially==true && report.SchemaConformantAtEnd==true,"TLS VerifyFull minimal privileges complete audit and final schema check");
        Assert(report.Batches.All(b=>b.ReadOnlyConfirmed && b.TlsVerified && b.IdentityVerified && b.PrivilegesVerified && b.RollbackConfirmed),"each batch confirms identity TLS READ ONLY privileges and rollback");
        Assert(report.CompletedBatches==6 && report.OmittedBatches.Count==0 && report.InterruptedQueries.Count==0,"report explicitly distinguishes completed omitted and interrupted batches");
        var counts=report.Batches.SelectMany(b=>b.Findings??[]).ToDictionary(f=>f.Category,f=>f.Count);
        Assert(counts["H03_status"]==1 && counts["H12_data_assistido"]==2 && counts["H05_episodios_com_varias_posicoes"]==2
            && counts["H10_historico_sem_biblioteca_valido"]==2,"expected synthetic counts including valid controls");
        var repeat=await RunAsync();
        Assert(Signature(report)==Signature(repeat),"TLS repeated execution same counts and distributions");
        await TlsAndIdentityAsync();
        await PrivilegesAsync();
        await StructuralAsync();
        await FailureAsync();
        Assert(fingerprint==await FingerprintAsync(),"full fixture and structure fingerprints unchanged after executor runs and restored test interventions");
        var result=new {audit=report,checks=Passed,independentEvidence=new {fullFixtureFingerprintsEqual=true,
            scope="local synthetic TLS database"},sessionPoolerValidated=false,
            limitations=new[]{"Supabase not contacted","Session pooler not simulated","Privilege proof scoped to connected PostgreSQL 17 catalog; metadata failure blocks execution"}};
        var json=JsonSerializer.Serialize(result,AuditReport.JsonOptions);
        Assert(!json.Contains(Secret,StringComparison.Ordinal) && !json.Contains("CANARY",StringComparison.OrdinalIgnoreCase)
            && !json.Contains("example.invalid",StringComparison.OrdinalIgnoreCase),"remote report excludes secrets and synthetic personal canaries");
        await File.WriteAllTextAsync(Path.Combine(Repository,"docs/B03-B23-TLS-AUDIT-REPORT.json"),JsonSerializer.Serialize(result,AuditReport.JsonOptions),Ct);
        Console.WriteLine($"PASS: {Passed.Count} TLS checks; aggregate report written.");
    }
    private static string Signature(AuditReport r)=>JsonSerializer.Serialize(r.Batches.Select(b=>new {b.Query,b.Status,b.Findings,b.Distribution,b.SchemaConformant,b.RollbackConfirmed}));
    private sealed class CheckFailure(string code):Exception(code);
}
