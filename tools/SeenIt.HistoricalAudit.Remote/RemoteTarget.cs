using System.Reflection;
using Npgsql;
using SeenIt.HistoricalAudit;

namespace SeenIt.HistoricalAudit.Remote;

internal sealed class RemoteTarget : IAuditTarget
{
    private readonly RemoteConfiguration configuration;
    private readonly string connectionString;
    public string Mode=>"remote-explicit";
    internal RemoteTarget(RemoteConfiguration configuration,string password)
    {
        this.configuration=configuration;
        connectionString=configuration.Connection(password).ConnectionString;
    }
    public NpgsqlConnection CreateConnection()=>new(connectionString);

    public async Task VerifyAsync(NpgsqlConnection connection,string schema,CancellationToken ct)
    {
        var settings=new NpgsqlConnectionStringBuilder(connection.ConnectionString);
        if(schema!="public" || settings.SslMode!=SslMode.VerifyFull || settings.GssEncryptionMode!=GssEncryptionMode.Disable
            || settings.RootCertificate!=configuration.RootCertificate || settings.Host!=configuration.Host
            || settings.Port!=configuration.Port || settings.Database!=configuration.Database
            || settings.Username!=configuration.ConnectionUser)
            throw new AuditRejectedException("verified_tls_required");
        // Identity is the effective server identity, not the pooler's wire username.
        await using(var identity=new NpgsqlCommand("""
            SELECT current_database()=@database AND session_user=@session AND current_user=@current
             AND current_setting('transaction_read_only')='on'
             AND current_setting('transaction_isolation')='repeatable read'
             AND current_setting('search_path')='pg_catalog'
             AND current_setting('statement_timeout')='5s'
             AND current_setting('lock_timeout')='1s'
             AND current_setting('idle_in_transaction_session_timeout')='10s'
             AND current_setting('session_replication_role')='origin'
            """,connection))
        {
            identity.Parameters.AddWithValue("database",configuration.Database);
            identity.Parameters.AddWithValue("session",configuration.ExpectedSessionUser);
            identity.Parameters.AddWithValue("current",configuration.ExpectedCurrentUser);
            if(!Equals(await identity.ExecuteScalarAsync(ct),true))
                throw new AuditRejectedException("identity_or_session_settings_unconfirmed");
        }
        using var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("SeenIt.HistoricalAudit.Remote.Privileges.sql")!;
        using var text=new StreamReader(stream);
        try
        {
            await using var privileges=new NpgsqlCommand(await text.ReadToEndAsync(ct),connection);
            await using var reader=await privileges.ExecuteReaderAsync(ct);
            var count=0;
            var excess=false;
            while(await reader.ReadAsync(ct)) { count++;excess|=reader.GetInt64(1)!=0; }
            if(count!=12) throw new AuditRejectedException("privilege_coverage_inconclusive");
            if(excess) throw new AuditRejectedException("privilege_allowlist_violation");
        }
        catch(PostgresException e) when(e.SqlState is not ("57014" or "55P03"))
        { throw new AuditRejectedException("privilege_coverage_inconclusive"); }
    }
}
