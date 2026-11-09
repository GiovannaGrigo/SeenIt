[CmdletBinding()]
param(
    [string]$Image = 'seenit-api:render-check',
    [ValidateRange(1024, 65535)]
    [int]$Port = 18081
)

$ErrorActionPreference = 'Stop'
$containerName = 'seenit-smoke-' + [Guid]::NewGuid().ToString('N')
$savedEnvironment = @{}
$containerStarted = $false

function Invoke-Docker {
    param([string[]]$DockerArguments)
    $result = & docker @DockerArguments 2>&1
    if ($LASTEXITCODE -ne 0) {
        # Do not print container configuration, environment or arbitrary raw errors.
        throw "Docker operation '$($DockerArguments[0])' failed (exit $LASTEXITCODE)."
    }
    return ($result -join "`n")
}

try {
    # Only synthetic runtime configuration. Nothing is read from User Secrets.
    # Network isolation prevents all contact with Supabase and other services.
    $random = New-Object byte[] 32
    $generator = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try { $generator.GetBytes($random) } finally { $generator.Dispose() }
    $testEnvironment = @{
        ASPNETCORE_ENVIRONMENT = 'Production'
        DOTNET_ENVIRONMENT = 'Production'
        PORT = [string]$Port
        ConnectionStrings__DefaultConnection = 'Host=127.0.0.1;Port=1;Database=container_smoke;Username=container_smoke;Timeout=1'
        Jwt__Key = [Convert]::ToBase64String($random)
        Jwt__Issuer = 'SeenIt.ContainerSmoke'
        Jwt__Audience = 'SeenIt.ContainerSmoke'
        Jwt__ExpirationMinutes = '5'
        SupabaseStorage__Url = 'https://abcdefghijklmnopqrst.supabase.co'
        SupabaseStorage__SecretKey = 'sb_secret_LOCAL_CONTAINER_SMOKE_ONLY'
        SupabaseStorage__Bucket = 'avatars'
        AllowedHosts = 'localhost;127.0.0.1'
    }
    $runArguments = @('run', '--detach', '--network', 'none', '--name', $containerName)
    foreach ($entry in $testEnvironment.GetEnumerator()) {
        $savedEnvironment[$entry.Key] = [Environment]::GetEnvironmentVariable($entry.Key, 'Process')
        [Environment]::SetEnvironmentVariable($entry.Key, $entry.Value, 'Process')
        # Values stay out of the command line; Docker reads only these named variables.
        $runArguments += @('--env', $entry.Key)
    }
    $runArguments += $Image
    $null = Invoke-Docker -DockerArguments $runArguments
    $containerStarted = $true

    $uid = (Invoke-Docker -DockerArguments @('exec', $containerName, 'id', '-u')).Trim()
    if ($uid -eq '0') { throw 'Container must run as non-root.' }
    $sdks = Invoke-Docker -DockerArguments @('exec', $containerName, 'dotnet', '--list-sdks')
    if (-not [string]::IsNullOrWhiteSpace($sdks)) { throw 'SDK unexpectedly present in runtime image.' }
    $null = Invoke-Docker -DockerArguments @('exec', $containerName, 'test', '-f', '/app/.container-checks-passed')
    $null = Invoke-Docker -DockerArguments @('exec', $containerName, 'test', '-f', '/app/libSkiaSharp.so')
    $settingsFiles = (Invoke-Docker -DockerArguments @('exec', $containerName, 'find', '/app', '-type', 'f', '-name', 'appsettings*.json')).Trim()
    if ($settingsFiles -ne '/app/appsettings.json') { throw 'Unexpected local configuration in runtime image.' }
    $webFiles = Invoke-Docker -DockerArguments @('exec', $containerName, 'find', '/app/wwwroot', '-type', 'f')
    if (-not [string]::IsNullOrWhiteSpace($webFiles)) { throw 'Unexpected local files in wwwroot.' }
    $settingsCheck = '! grep -Eq ''ConnectionStrings|SecretKey|sb_secret_|Jwt|Password'' /app/appsettings.json'
    $null = Invoke-Docker -DockerArguments @('exec', $containerName, '/bin/bash', '-c', $settingsCheck)

    # No curl/SDK/test binary needs to be installed in the production image.
    $probe = 'exec 3<>/dev/tcp/127.0.0.1/__PORT__; printf ''GET /health HTTP/1.1\r\nHost: localhost\r\nConnection: close\r\n\r\n'' >&3; cat <&3'
    $probe = $probe.Replace('__PORT__', [string]$Port)
    $healthy = $false
    for ($attempt = 0; $attempt -lt 40; $attempt++) {
        try {
            $response = Invoke-Docker -DockerArguments @('exec', $containerName, '/bin/bash', '-c', $probe)
            if ($response -match 'HTTP/1\.[01] 200' -and $response -match '\{"status":"ok"\}') {
                $healthy = $true
                break
            }
        }
        catch { }
        Start-Sleep -Milliseconds 300
    }
    if (-not $healthy) { throw 'GET /health failed inside the isolated Linux container.' }
    $startupLogs = Invoke-Docker -DockerArguments @('logs', $containerName)
    if ($startupLogs -notmatch "http://0\.0\.0\.0:$Port") {
        throw 'Expected PORT binding on 0.0.0.0 was not observed.'
    }
    [pscustomobject]@{
        Image = $Image
        Runtime = 'Linux / ASP.NET Core 10 / Production'
        UserId = $uid
        NonRoot = $true
        SdkAbsent = $true
        PortOverride = $Port
        Binding = "0.0.0.0:$Port"
        HealthStatus = 200
        HealthBody = '{"status":"ok"}'
        BuildNativeChecksMarker = $true
        SanitizedConfigurationOnly = $true
        LocalPhotosAbsent = $true
        Network = 'none'
        RealCredentialsUsed = $false
    }
}
finally {
    if ($containerStarted) {
        $null = & docker rm --force $containerName 2>&1
        if ($LASTEXITCODE -ne 0) { Write-Warning "Could not remove this test container: $containerName" }
    }
    foreach ($entry in $savedEnvironment.GetEnumerator()) {
        [Environment]::SetEnvironmentVariable($entry.Key, $entry.Value, 'Process')
    }
    $testEnvironment = $null
    $random = $null
}
