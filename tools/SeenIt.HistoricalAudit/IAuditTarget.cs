using Npgsql;

namespace SeenIt.HistoricalAudit;

internal interface IAuditTarget
{
    string Mode { get; }
    NpgsqlConnection CreateConnection();
    Task VerifyAsync(NpgsqlConnection connection, string schema, CancellationToken cancellationToken);
}
