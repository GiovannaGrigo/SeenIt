try { await RegistrationIntegrationChecks.RunAsync(); await RateLimitingChecks.RunAsync(); await RateLimitingRegressionChecks.RunAsync(); }
catch (Exception error)
{
    Console.Error.WriteLine(error);
    Environment.ExitCode = 1;
}
