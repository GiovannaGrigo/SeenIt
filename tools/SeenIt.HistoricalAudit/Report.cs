using System.Text.Json;
using Npgsql;
using System.Text.Json.Serialization;

namespace SeenIt.HistoricalAudit;

internal sealed record AuditReport
{
    public const string Version = "B03-B2.1/1.0";
    public static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    public string AuditVersion { get; init; } = Version;
    public DateTimeOffset StartedUtc { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset FinishedUtc { get; set; }
    public string? PostgreSqlVersion { get; set; }
    public string Mode { get; set; } = "local-synthetic";
    public bool AuditComplete { get; set; }
    public bool? SchemaValidatedInitially { get; set; }
    public bool? SchemaConformantAtEnd { get; set; }
    public bool ReadOnlyVerified { get; set; }
    public int CompletedBatches => Batches.Count(b=>b.Status=="completed");
    public List<string> OmittedBatches { get; } = [];
    public List<string> InterruptedQueries { get; } = [];
    [JsonIgnore] public bool Complete { get=>AuditComplete; set=>AuditComplete=value; }
    [JsonIgnore] public bool SchemaValidated { get=>SchemaValidatedInitially==true; set=>SchemaValidatedInitially=value; }
    [JsonIgnore] public bool AllBatchesReadOnly { get=>ReadOnlyVerified; set=>ReadOnlyVerified=value; }
    public string? EvidenceReasonCode { get; set; }
    public string ExecutorDeclaration { get; init; } = "Fixed SELECTs and transaction control only; this statement is not independent evidence of unchanged data.";
    public string DatabaseGuarantees => "See per-batch READ ONLY, identity, privilege and rollback confirmations.";
    public string? IndependentTestEvidence { get; init; } = null;
    public List<BatchReport> Batches { get; } = [];
    public List<string> Skipped { get; } = [];
    public List<string> Limitations { get; } = [
        "No TVmaze access. Test evidence and production authorization are separate.",
        "Each batch has its own repeatable-read snapshot; batches are not one global snapshot.",
        "Categories overlap; counts of groups, rows and valid controls must not be summed.",
        "Manual status choices and history without a library entry are valid.",
        "Text/URL SQL checks are screening only, not exact .NET Unicode/URI validation.",
        "No proof of historical intent, ownership mistakes between valid users or past atomicity."];
}

internal sealed record BatchReport(string Query)
{
    public bool ReadOnlyConfirmed { get; set; }
    public bool IdentityVerified { get; set; }
    public bool PrivilegesVerified { get; set; }
    public string PrivilegeCoverage { get; set; } = "not_verified";
    public bool TlsVerified { get; set; }
    public bool RollbackConfirmed { get; set; }
    public string? CleanupFailure { get; set; }
    public bool SchemaConformant { get; set; }
    public string Status { get; set; } = "not_executed";
    public string? Failure { get; set; }
    public Dictionary<string, long>? EvaluatedRows { get; set; }
    public List<Finding>? Findings { get; set; }
    public List<Dictionary<string, object?>>? Distribution { get; set; }
    public List<string>? OpaqueEvidence { get; set; }
    public int SchemaDifferences { get; set; }
    public Dictionary<string, int>? SchemaObjectsChecked { get; set; }
    public List<string>? SchemaDifferenceKinds { get; set; }
    public long DurationMs { get; set; }
    public long? QueryDurationMs { get; set; }
    public long? MetadataDurationMs { get; set; }
    public long? DenominatorDurationMs { get; set; }
}

internal sealed record Finding(string Category, long Count, string Unit, string Classification, string Severity);

internal static class SafeFailure
{
    internal static string Code(Exception e) => e switch
    {
        AuditRejectedException a => a.Message,
        OperationCanceledException => "cancelled_or_deadline",
        PostgresException p when p.SqlState == "57014" => "timeout_or_cancelled",
        PostgresException p when p.SqlState == "55P03" => "lock_timeout",
        PostgresException => "database_failure",
        NpgsqlException => "connection_or_command_failure",
        _ => "audit_failure"
    };
}
