try
{
    if (args.Contains("--postgres-only")) await PostgresIntegrityChecks.RunAsync();
    else
    {
        await CatalogResilienceChecks.RunAsync();
        await DataIntegrityChecks.RunAsync();
        await RegistrationIntegrationChecks.RunAsync();
        await RateLimitingChecks.RunAsync();
        await RateLimitingRegressionChecks.RunAsync();
    }
}
catch (Exception error)
{
    Console.Error.WriteLine(error);
    Environment.ExitCode = 1;
}
