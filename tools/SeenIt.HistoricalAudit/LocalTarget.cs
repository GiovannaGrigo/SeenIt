using Npgsql;

namespace SeenIt.HistoricalAudit;

// No application configuration, environment connection strings, URLs or DNS resolution.
internal static class LocalTarget
{
    internal const string Database = "seenit_b03b2";
    internal const string Reader = "b03b2_reader";
    internal const string ReaderPassword = "b03b2_synthetic_reader";

    internal static string Connection(string host = "127.0.0.1", int port = 15433)
    {
        if (host is not ("127.0.0.1" or "::1") || port is < 1024 or > 65535)
            throw new AuditRejectedException("local_target_required");
        return new NpgsqlConnectionStringBuilder
        {
            Host = host, Port = port, Database = Database,
            Username = Reader,
            Password = ReaderPassword,
            Timeout = 3, CommandTimeout = 5, CancellationTimeout = 1000,
            Pooling = false, IncludeErrorDetail = false,
            ApplicationName = "seenit-b03b2-readonly",
            Options = "-c search_path=pg_catalog -c statement_timeout=5000 -c lock_timeout=1000 -c idle_in_transaction_session_timeout=10000",
            SslMode = SslMode.Disable
        }.ConnectionString;
    }
}

internal sealed class AuditRejectedException(string code) : Exception(code);
