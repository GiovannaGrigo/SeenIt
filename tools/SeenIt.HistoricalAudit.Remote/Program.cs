using System.Text.Json;
using SeenIt.HistoricalAudit;
using SeenIt.HistoricalAudit.Remote;

try
{
    var enabled=false;
    var execute=false;
    string? configurationPath=null;
    foreach(var arg in args)
    {
        if(arg=="--enable-remote") enabled=true;
        else if(arg=="--execute") execute=true;
        else if(arg.StartsWith("--configuration=")) configurationPath=arg[16..];
        else throw new AuditRejectedException("unsupported_argument");
    }
    if(!enabled || configurationPath is null) throw new AuditRejectedException("explicit_remote_configuration_required");
    var configuration=RemoteConfiguration.Load(configurationPath);
    configuration.Validate();
    if(!execute)
    {
        Console.WriteLine("{\"configurationValid\":true,\"connectionAttempted\":false,\"executionRequiresExplicitFlag\":true}");
        return 0;
    }
    // Only the dedicated variable is read. No API connection settings or secret in CLI/configuration.
    var password=Environment.GetEnvironmentVariable("SEENIT_AUDIT_REMOTE_PASSWORD")
        ?? throw new AuditRejectedException("exclusive_secret_required");
    // Process-local cleanup prevents implicit Npgsql defaults from importing application credentials/certificates.
    foreach(var name in new[]{"PGUSER","PGPASSWORD","PGPASSFILE","PGSSLCERT","PGSSLKEY","PGSSLROOTCERT","PGOPTIONS","PGAPPNAME","PGSSLNEGOTIATION","PGSERVICE","PGSERVICEFILE"})
        Environment.SetEnvironmentVariable(name,null);
    Environment.SetEnvironmentVariable("SEENIT_AUDIT_REMOTE_PASSWORD",null);
    var target=new RemoteTarget(configuration,password);
    using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(120));
    Console.CancelKeyPress+=(_,e)=>{e.Cancel=true;deadline.Cancel();};
    var report=await new AuditRunner(target).RunAsync(false,deadline.Token);
    Console.WriteLine(JsonSerializer.Serialize(report,AuditReport.JsonOptions));
    return report.AuditComplete ? 0 : 2;
}
catch(Exception error)
{
    Console.WriteLine(JsonSerializer.Serialize(new { version=AuditReport.Version,auditComplete=false,
        failure=SafeFailure.Code(error),counts=(object?)null }));
    return 2;
}
