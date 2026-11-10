try { await RegistrationIntegrationChecks.RunAsync(); }
catch (Exception error)
{
    Console.Error.WriteLine(error);
    Environment.ExitCode = 1;
}
