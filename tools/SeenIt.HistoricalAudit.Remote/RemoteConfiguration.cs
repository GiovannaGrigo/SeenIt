using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Npgsql;
using SeenIt.HistoricalAudit;

namespace SeenIt.HistoricalAudit.Remote;

// No connection string, TLS override, custom SQL, schema override or secret in this contract.
internal sealed record RemoteConfiguration
{
    public required string Host { get; init; }
    public required int Port { get; init; }
    public required string Database { get; init; }
    public required string ConnectionUser { get; init; }
    public required string ExpectedSessionUser { get; init; }
    public required string ExpectedCurrentUser { get; init; }
    public required string RootCertificate { get; init; }
    public required bool ExclusiveAuditIdentity { get; init; }

    internal static RemoteConfiguration Load(string path)
    {
        var file=new FileInfo(path);
        if(!file.Exists || file.Length>8192) throw new AuditRejectedException("remote_configuration_invalid");
        return JsonSerializer.Deserialize<RemoteConfiguration>(File.ReadAllText(path),new JsonSerializerOptions {
            UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow })
            ?? throw new AuditRejectedException("remote_configuration_invalid");
    }

    internal void Validate()
    {
        if(!ExclusiveAuditIdentity || Port is <1024 or >65535 || string.IsNullOrWhiteSpace(Host)
            || Host.Length>253 || Host.Contains(',') || Host.Contains('/') || Host.Contains(':')
            || Host!=Host.Trim() || Uri.CheckHostName(Host)==UriHostNameType.Unknown)
            throw new AuditRejectedException("remote_endpoint_invalid");
        if(Database is null || !Regex.IsMatch(Database,"^[a-zA-Z_][a-zA-Z0-9_]{0,62}$"))
            throw new AuditRejectedException("remote_database_invalid");
        if(ExpectedSessionUser is null || !Regex.IsMatch(ExpectedSessionUser,"^seenit_audit_[a-z0-9_]{1,40}$")
            || ExpectedCurrentUser!=ExpectedSessionUser || ConnectionUser is null
            || !(ConnectionUser==ExpectedSessionUser || Regex.IsMatch(ConnectionUser,
                "^"+Regex.Escape(ExpectedSessionUser)+"[.][a-z0-9]{8,40}$")))
            throw new AuditRejectedException("exclusive_audit_identity_required");
        if(string.IsNullOrWhiteSpace(RootCertificate) || !Path.IsPathFullyQualified(RootCertificate)
            || !File.Exists(RootCertificate)) throw new AuditRejectedException("trusted_ca_file_required");
    }

    internal NpgsqlConnectionStringBuilder Connection(string password)
    {
        Validate();
        if(string.IsNullOrWhiteSpace(password) || password.Length<24)
            throw new AuditRejectedException("exclusive_secret_required");
        return new NpgsqlConnectionStringBuilder {
            Host=Host,Port=Port,Database=Database,Username=ConnectionUser,Password=password,
            SslMode=SslMode.VerifyFull,RootCertificate=RootCertificate,GssEncryptionMode=GssEncryptionMode.Disable,
            Pooling=false,Timeout=3,CommandTimeout=5,CancellationTimeout=1000,IncludeErrorDetail=false,
            PersistSecurityInfo=false,Enlist=false,ApplicationName="seenit-b03b21-remote-audit",
            Options="-c search_path=pg_catalog -c statement_timeout=5000 -c lock_timeout=1000 -c idle_in_transaction_session_timeout=10000"
        };
    }
}
