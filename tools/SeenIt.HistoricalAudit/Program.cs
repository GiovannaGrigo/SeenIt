using System.Text.Json;
using SeenIt.HistoricalAudit;

// Deliberately narrow CLI. No --connection, --sql, config, API, external catalog or schema option.
try
{
    var host = "127.0.0.1";
    var port = 15433;
    var evidence = false;
    var justification = false;
    foreach (var arg in args)
    {
        if (arg.StartsWith("--host=")) host = arg[7..];
        else if (arg.StartsWith("--port=") && int.TryParse(arg[7..], out var value)) port = value;
        else if (arg == "--evidence=invalid-feelings") evidence = true;
        // Reason codes only: free text could contain personal information.
        else if (arg == "--justification=investigate-invalid-feelings") justification = true;
        else throw new AuditRejectedException("unsupported_argument");
    }
    if (evidence != justification) throw new AuditRejectedException("evidence_requires_reason_code");
    using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(120));
    Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancellation.Cancel(); };
    var report = await new AuditRunner(host, port).RunAsync(evidence, cancellation.Token);
    Console.WriteLine(JsonSerializer.Serialize(report, AuditReport.JsonOptions));
    return report.Complete ? 0 : 2;
}
catch (Exception error)
{
    // Never print exception messages, server detail, arguments or connection strings.
    Console.WriteLine(JsonSerializer.Serialize(new { version = AuditReport.Version, complete = false,
        failure = SafeFailure.Code(error), counts = (object?)null }));
    return 2;
}
