[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$OutputDirectory,
    [ValidateNotNullOrEmpty()][ValidateSet('All','Npm','NuGet','Mcr')][string[]]$Scope = @('All'),
    [switch]$IncludeScout,
    [ValidatePattern('^sha256:[a-f0-9]{64}$')][string]$ImageId,
    [switch]$Offline,
    [ValidateRange(1,900)][int]$TimeoutSeconds = 120
)

$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSVersion -lt [version]'5.1' -or $env:OS -ne 'Windows_NT') {
    Write-Error 'Use Windows PowerShell 5.1 or PowerShell 7 on Windows.' -ErrorAction Continue
    exit 3
}

function Invoke-AuditCommand($Name, [string[]]$Arguments, $Directory, [hashtable]$Environment = @{}) {
    $result = @{ code = $null; output = ''; error = $null; warning = $false; feedError = $false; auditWarnings = @() }
    if ($Name -eq 'git') { $Arguments = @('-c',"safe.directory=$repo",'-c','core.fsmonitor=false') + $Arguments }
    $command = Get-Command $Name -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
    if (!$command) { $result.error = "Command unavailable: $Name"; return $result }
    $process = New-Object Diagnostics.Process
    try {
        $info = $process.StartInfo
        $info.FileName = $command.Source
        $info.WorkingDirectory = $Directory
        $info.UseShellExecute = $false
        $info.CreateNoWindow = $true
        $info.RedirectStandardOutput = $true
        $info.RedirectStandardError = $true
        $info.StandardOutputEncoding = [Text.Encoding]::UTF8
        $info.StandardErrorEncoding = [Text.Encoding]::UTF8
        $info.Arguments = ($Arguments | ForEach-Object {
            '"' + ($_ -replace '(\\*)"', '$1$1\"' -replace '(\\+)$', '$1$1') + '"'
        }) -join ' '
        $variables = $info.EnvironmentVariables
        foreach ($key in @($variables.Keys)) {
            if ($key -match '^(NPM_|NUGET_|GIT_|MSBUILD|DOTNET_)' -or $key -in @('NODE_OPTIONS','NODE_PATH')) {
                $variables.Remove($key)
            }
        }
        $variables['TEMP'] = $scratch
        $variables['TMP'] = $scratch
        foreach ($key in $Environment.Keys) { $variables[$key] = $Environment[$key] }
        [void]$process.Start()
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        $timer = [Diagnostics.Stopwatch]::StartNew()
        $exited = $process.WaitForExit($TimeoutSeconds * 1000)
        $remaining = [Math]::Max(0, $TimeoutSeconds * 1000 - [int]$timer.ElapsedMilliseconds)
        if (!$exited -or ![Threading.Tasks.Task]::WaitAll([Threading.Tasks.Task[]]@($stdout,$stderr), $remaining)) {
            $result.error = 'External command timed out.'
            if (!$process.HasExited) {
                try {
                    if ($PSVersionTable.PSEdition -eq 'Core') { $process.Kill($true) }
                    else {
                        try {
                            $killInfo = New-Object Diagnostics.ProcessStartInfo
                            $killInfo.FileName = Join-Path $env:SystemRoot 'System32\taskkill.exe'
                            $killInfo.Arguments = "/PID $($process.Id) /T /F"
                            $killInfo.UseShellExecute = $false
                            $killInfo.CreateNoWindow = $true
                            $killInfo.RedirectStandardOutput = $true
                            $killInfo.RedirectStandardError = $true
                            $killer = [Diagnostics.Process]::Start($killInfo)
                            try {
                                if (!$killer.WaitForExit(2000)) { $killer.Kill() }
                            } finally { $killer.Dispose() }
                        } catch { $result.warning = $true }
                        if (!$process.HasExited) { $process.Kill() }
                    }
                    if (!$process.WaitForExit(3000)) { $result.warning = $true }
                } catch {
                    $result.warning = $true
                }
            }
            return $result
        }
        $result.code = $process.ExitCode
        $result.output = $stdout.Result
        $result.warning = ![string]::IsNullOrWhiteSpace($stderr.Result)
        $diagnostics = $stdout.Result + [Environment]::NewLine + $stderr.Result
        $result.feedError = $diagnostics -match '\bNU(?:1900|1905|130[01])\b'
        if ($Name -eq 'dotnet') {
            $result.auditWarnings = @($diagnostics -split '\r?\n' | Where-Object { $_ -match '\bNU190[1-4]\b' })
        }
    } catch { $result.error = "Unable to execute or terminate $Name." }
    finally { $process.Dispose() }
    return $result
}

function Get-SafeAuditPath($Path, $Worktrees) {
    $full = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Path)
    if ($full -notmatch '^[A-Za-z]:\\' -or $full.Substring(2).Contains(':')) {
        throw 'A local filesystem directory is required.'
    }
    if ($full -match '[. ](?:\\|$)') { throw 'Directory names ending in dots or spaces are not supported.' }
    foreach ($tree in $Worktrees) {
        $prefix = [IO.Path]::GetFullPath($tree).TrimEnd('\') + '\'
        if (($full.TrimEnd('\') + '\').StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
            throw 'Output and scratch must be outside every repository worktree.'
        }
    }
    for ($parent = $full; $parent; $parent = Split-Path -Parent $parent) {
        if (Test-Path -LiteralPath $parent) {
            $item = Get-Item -Force -LiteralPath $parent
            if (!$item.PSIsContainer -or ($item.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
                throw 'Use ordinary directories without junctions or symlinks.'
            }
            if ($full -match '~\d+(?:[.\\]|$)' -and (Test-Path -LiteralPath (Join-Path $parent '.git'))) {
                throw 'A short Windows path cannot be used inside a Git checkout.'
            }
        }
    }
    return $full
}

function Get-AdvisoryIds([string]$Text) {
    [regex]::Matches($Text, '(?i)\b(?:CVE-\d{4}-\d{4,}|GHSA-[a-z0-9]{4}-[a-z0-9]{4}-[a-z0-9]{4})\b') |
        ForEach-Object { $_.Value.ToUpperInvariant() } | Sort-Object -Unique
}

function Record-AuditCommand($Source, $Result) {
    $Source.exitCode = $Result.code
    if ($Result.warning) { $Source.warnings += 'Tool emitted diagnostics on stderr. Raw text omitted.' }
    if ($Result.error) { $Source.errors += $Result.error; return $false }
    return $true
}

function Invoke-NpmAudit($Source, $Production) {
    $npm = Get-Command npm.cmd -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
    if (!$npm) { $Source.errors += 'Command unavailable: npm.cmd'; return }
    $cli = Join-Path (Split-Path -Parent $npm.Source) 'node_modules/npm/bin/npm-cli.js'
    if (!(Test-Path -LiteralPath $cli -PathType Leaf)) { $Source.errors += 'npm CLI missing beside npm.cmd.'; return }
    $folder = Join-Path $scratch $Source.name
    [void][IO.Directory]::CreateDirectory($folder)
    foreach ($file in @('package.json','package-lock.json')) {
        Copy-Item -LiteralPath (Join-Path $repo "frontend/$file") -Destination $folder
    }
    $lockText = Get-Content -Raw -LiteralPath (Join-Path $folder 'package-lock.json')
    if ($PSVersionTable.PSEdition -eq 'Core') {
        $lock = ConvertFrom-Json -InputObject $lockText -AsHashtable
    } else {
        Add-Type -AssemblyName System.Web.Extensions
        $parser = New-Object Web.Script.Serialization.JavaScriptSerializer
        $parser.MaxJsonLength = [int]::MaxValue
        $lock = $parser.DeserializeObject($lockText)
    }
    $settings = @{
        NPM_CONFIG_CACHE = (Join-Path $scratch 'npm-cache')
        NPM_CONFIG_USERCONFIG = (Join-Path $scratch 'empty-user.npmrc')
        NPM_CONFIG_GLOBALCONFIG = (Join-Path $scratch 'empty-global.npmrc')
        NPM_CONFIG_REGISTRY = $Source.feed
        NPM_CONFIG_FETCH_RETRIES = '0'
        NPM_CONFIG_UPDATE_NOTIFIER = 'false'
    }
    $node = Invoke-AuditCommand node @('--version') $folder
    if (!(Record-AuditCommand $Source $node)) { return }
    $version = Invoke-AuditCommand node @($cli,'--version') $folder $settings
    if (!(Record-AuditCommand $Source $version)) { return }
    if ($node.code -ne 0 -or $node.output.Trim() -notmatch '^v\d+\.\d+\.\d+$' -or
        $version.code -ne 0 -or $version.output.Trim() -notmatch '^(?:[7-9]|[1-9]\d+)\.\d+\.\d+$') {
        $Source.errors += 'Node and npm 7+ must report valid versions.'
        return
    }
    $Source.toolVersion = $version.output.Trim()
    $Source.details = @{ nodeVersion = $node.output.Trim(); severities = $null }
    $arguments = @($cli,'audit','--json','--package-lock-only','--ignore-scripts','--audit-level=low')
    if ($Production) { $arguments += '--omit=dev' }
    $Source.consulted = $true
    $run = Invoke-AuditCommand node $arguments $folder $settings
    if (!(Record-AuditCommand $Source $run)) { return }
    try { $audit = ConvertFrom-Json -InputObject $run.output }
    catch { $Source.errors += 'Invalid npm audit JSON.'; return }
    if ($audit.error) {
        $feedCode = [string]$audit.error.code
        if ($feedCode -notmatch '^[A-Z0-9_]{1,32}$') { $feedCode = 'unknown' }
        $Source.errors += "npm feed or configuration error ($feedCode)."
        return
    }
    $counts = [ordered]@{}
    foreach ($severity in @('info','low','moderate','high','critical','total')) {
        $value = $audit.metadata.vulnerabilities.$severity
        if ($null -eq $value -or "$value" -notmatch '^\d+$') {
            $Source.errors += 'Invalid npm severity counts.'; return
        }
        $counts[$severity] = [int]$value
    }
    if ($audit.auditReportVersion -ne 2 -or $audit.vulnerabilities -isnot [pscustomobject] -or
        $run.code -notin @(0,1) -or ($run.code -eq 1 -and !$counts.total)) {
        $Source.errors += 'Unexpected npm audit schema or exit code.'
        return
    }
    $Source.findings = @(foreach ($property in $audit.vulnerabilities.PSObject.Properties) {
        $item = $property.Value
        $advisories = @(foreach ($via in $item.via) {
            if ($via -isnot [string]) {
                @{ ids = @(Get-AdvisoryIds $via.url); npmId = $via.source; affectedRange = $via.range; severity = $via.severity }
            }
        })
        $versions = @(foreach ($path in $item.nodes) {
            $entry = $lock.packages[[string]$path]
            if ($entry -and $entry.version) { $entry.version }
        })
        $fix = if ($item.fixAvailable -and $item.fixAvailable -isnot [bool]) {
            @{ package = $item.fixAvailable.name; version = $item.fixAvailable.version; semverMajor = $item.fixAvailable.isSemVerMajor }
        } elseif ($item.fixAvailable) { @{ suggested = $true } }
        @{
            package = $property.Name; severity = $item.severity; affectedRange = $item.range
            versions = @($versions | Sort-Object -Unique); direct = $item.isDirect; advisories = $advisories
            viaPackages = @($item.via | Where-Object { $_ -is [string] }); fix = $fix; applicability = 'unverified'
        }
    })
    $Source.details.severities = $counts
    $Source.findingCount = $Source.findings.Count
    if ($counts.total -ne $Source.findingCount -or
        $counts.total -ne ($counts.info + $counts.low + $counts.moderate + $counts.high + $counts.critical)) {
        $Source.state = 'partial'
        $Source.errors += 'npm findings and severity totals disagree.'
    } elseif ($Source.findingCount) { $Source.state = 'findings' }
    else { $Source.state = 'success' }
}

function Invoke-NuGetAudit($Source) {
    $folder = Join-Path $scratch 'nuget'
    [void][IO.Directory]::CreateDirectory($folder)
    $settings = @{
        DOTNET_CLI_HOME = (Join-Path $scratch 'dotnet-home')
        DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
        DOTNET_ADD_GLOBAL_TOOLS_TO_PATH = 'false'
        DOTNET_NOLOGO = '1'; DOTNET_CLI_TELEMETRY_OPTOUT = '1'
        DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE = 'true'
        DOTNET_CLI_USE_MSBUILD_SERVER = '0'; MSBUILDDISABLENODEREUSE = '1'
        NUGET_PACKAGES = (Join-Path $scratch 'nuget-packages')
        NUGET_HTTP_CACHE_PATH = (Join-Path $scratch 'nuget-http')
        NUGET_PLUGINS_CACHE_PATH = (Join-Path $scratch 'nuget-plugins')
    }
    $sdk = Invoke-AuditCommand dotnet @('--version') $folder $settings
    if (!(Record-AuditCommand $Source $sdk)) { return }
    if ($sdk.code -ne 0 -or $sdk.output.Trim() -notmatch '^10\.\d+\.\d+$') {
        $Source.errors += 'A stable .NET SDK 10 is required.'; return
    }
    $Source.toolVersion = $sdk.output.Trim()
    $feed = $Source.feed
    $config = Join-Path $folder 'NuGet.Config'
    [IO.File]::WriteAllText($config, "<configuration><packageSources><clear/><add key='nuget.org' value='$feed'/></packageSources><auditSources><clear/><add key='nuget.org' value='$feed'/></auditSources></configuration>")
    $projects = @('SeenIt.Api','SeenIt.AvatarChecks','SeenIt.Checks','SeenIt.RegisterIntegrationChecks')
    foreach ($name in $projects) {
        $relative = "backend/$name/$name.csproj"
        [void][IO.Directory]::CreateDirectory((Join-Path $folder "backend/$name"))
        Copy-Item -LiteralPath (Join-Path $repo $relative) -Destination (Join-Path $folder $relative)
    }
    $Source.details = @{ projects = @(); covered = 0 }
    foreach ($name in $projects) {
        $relative = "backend/$name/$name.csproj"
        $item = [ordered]@{ project = $relative; tfm = 'net10.0'; rid = 'linux-x64'; state = 'failed'
            restoreExitCode = $null; auditExitCode = $null; resolved = @(); errors = @(); warnings = @() }
        $Source.details.projects += $item
        $Source.consulted = $true
        $restore = Invoke-AuditCommand dotnet @('restore',$relative,'--runtime','linux-x64','--configfile',$config,
            '--disable-build-servers','-p:NuGetAudit=true','-p:NuGetAuditMode=all') $folder $settings
        $item.restoreExitCode = $restore.code
        if ($restore.error -or $restore.code -ne 0 -or $restore.feedError) {
            $item.errors += $(if ($restore.error) { $restore.error } else { 'Restore failed or audit feed unavailable.' })
            continue
        }
        try {
            $assets = Get-Content -Raw -LiteralPath (Join-Path $folder "backend/$name/obj/project.assets.json") | ConvertFrom-Json
            $target = $assets.targets.'net10.0/linux-x64'
            if (!$target) { throw 'missing target' }
            $item.resolved = @(foreach ($property in $target.PSObject.Properties) {
                if ($property.Value.type -eq 'package') {
                    $pair = $property.Name -split '/',2
                    @{ package = $pair[0]; version = $pair[1] }
                }
            })
            if (!$item.resolved.Count) { throw 'empty graph' }
        } catch { $item.errors += 'No verifiable Linux-x64 package graph.'; continue }
        $list = Invoke-AuditCommand dotnet @('package','list','--project',$relative,'--no-restore',
            '--vulnerable','--include-transitive','--source',$feed,'--format','json') $folder $settings
        $item.auditExitCode = $list.code
        $Source.exitCode = $list.code
        if ($list.code -ne 0 -or $list.error -or $list.feedError) {
            $item.errors += $(if ($list.error) { $list.error } else { 'NuGet audit failed or audit feed unavailable.' })
            continue
        }
        if ($restore.warning -or $list.warning) { $item.warnings += 'Tool emitted diagnostics on stderr. Raw text omitted.' }
        try {
            $audit = ConvertFrom-Json -InputObject $list.output
            if ($audit.version -ne 1 -or $feed -notin $audit.sources -or @($audit.projects).Count -ne 1 -or
                [IO.Path]::GetFileName($audit.projects[0].path) -ne "$name.csproj") { throw 'invalid schema' }
            $project = $audit.projects[0]
            if ('frameworks' -in $project.PSObject.Properties.Name -and $project.frameworks -isnot [array]) {
                throw 'invalid frameworks'
            }
            $findings = @(foreach ($framework in $project.frameworks) {
                if (!$framework.framework -or !(@('topLevelPackages','transitivePackages') |
                    Where-Object { $_ -in $framework.PSObject.Properties.Name })) { throw 'invalid framework' }
                foreach ($group in @('topLevelPackages','transitivePackages')) {
                    if ($group -notin $framework.PSObject.Properties.Name) { continue }
                    if ($framework.$group -isnot [array]) { throw 'invalid package group' }
                    foreach ($package in $framework.$group) {
                        if (!$package.id -or !$package.resolvedVersion -or $package.vulnerabilities -isnot [array] -or
                            !$package.vulnerabilities.Count) { throw 'invalid vulnerable package' }
                        foreach ($vulnerability in $package.vulnerabilities) {
                            if (!$vulnerability.severity -or !$vulnerability.advisoryurl) { throw 'invalid vulnerability' }
                            @{ project = $relative; package = $package.id; version = $package.resolvedVersion
                               severity = $vulnerability.severity; ids = @(Get-AdvisoryIds $vulnerability.advisoryurl)
                               direct = $group -eq 'topLevelPackages'; fix = $null; applicability = 'unverified' }
                        }
                    }
                }
            })
            $Source.findings += $findings
            $restoreWarnings = @($restore.auditWarnings | Where-Object { $_ })
            $restoreIds = @(Get-AdvisoryIds ($restoreWarnings -join ' '))
            $listIds = @($findings | ForEach-Object { $_.ids })
            if ($restoreWarnings.Count -and (!$findings.Count -or @($restoreIds | Where-Object { $_ -notin $listIds }).Count)) {
                $item.state = 'partial'
                $item.errors += 'Restore reported vulnerability warnings not corroborated by the package list.'
                $item.restoreAdvisoryIds = $restoreIds
                continue
            }
            $item.state = if ($findings.Count) { 'findings' } else { 'success' }
            $Source.details.covered++
        } catch { $item.errors += 'Invalid NuGet audit JSON.' }
    }
    $Source.errors = @($Source.details.projects | Where-Object { $_.errors.Count } | ForEach-Object {
        '{0}: {1}' -f $_.project, ($_.errors -join ' ')
    })
    $valid = $Source.details.covered
    $hasPartialProject = 'partial' -in $Source.details.projects.state
    $Source.findingCount = if ($valid -or $hasPartialProject) { $Source.findings.Count }
    if ($valid -eq $projects.Count) { $Source.state = if ($Source.findingCount) { 'findings' } else { 'success' } }
    elseif ($valid -or $hasPartialProject) { $Source.state = 'partial' }
    if ($valid -ne $projects.Count) { $Source.exitCode = $null }
}

function Get-McrDocument($Url, $MediaType) {
    $run = Invoke-AuditCommand curl.exe @('--disable','--silent','--show-error','--fail','--location',
        '--max-redirs','3','--proto','=https','--proto-redir','=https','--header',"Accept: $MediaType",
        '--write-out','\n%header{docker-content-digest}',$Url) $scratch
    $result = @{ code = $run.code; error = $run.error; data = $null; digest = $null }
    if ($run.error) { return $result }
    if ($run.code -ne 0) { $result.error = 'MCR request failed.'; return $result }
    try {
        $separator = $run.output.LastIndexOf([char]10)
        if ($separator -lt 0) { throw 'missing digest line' }
        $result.data = ConvertFrom-Json -InputObject ($run.output.Substring(0,$separator))
        $result.digest = $run.output.Substring($separator + 1).Trim()
    } catch { $result.error = 'Invalid MCR JSON or response format.' }
    return $result
}

function Invoke-McrAudit($Source, $Name) {
    $version = Invoke-AuditCommand curl.exe @('--disable','--version') $scratch
    if (!(Record-AuditCommand $Source $version)) { return }
    if ($version.code -ne 0 -or $version.output -notmatch '^curl ([0-9.]+)' -or [version]$Matches[1] -lt [version]'7.84') {
        $Source.errors += 'curl 7.84+ is required for registry response headers.'; return
    }
    $Source.toolVersion = $Matches[1]
    $tag = "mcr.microsoft.com/dotnet/${Name}:10.0-noble"
    $content = Get-Content -Raw -LiteralPath (Join-Path $repo 'backend/SeenIt.Api/Dockerfile')
    $pins = [regex]::Matches($content, '(?m)^FROM ' + [regex]::Escape($tag) + '@(sha256:[a-f0-9]{64})(?:\s|$)')
    if ($pins.Count -ne 1) { $Source.errors += 'Expected a unique fixed Dockerfile digest.'; return }
    $pin = $pins[0].Groups[1].Value
    $Source.details = [ordered]@{ tag = $tag; pinnedDigest = $pin; indexDigest = $null; amd64Digest = $null
        pinMatchesIndex = $null; pinMatchesAmd64 = $null; declaredVersions = $null }
    $base = "https://mcr.microsoft.com/v2/dotnet/$Name"
    $Source.consulted = $true
    $index = Get-McrDocument "$base/manifests/10.0-noble" 'application/vnd.docker.distribution.manifest.list.v2+json, application/vnd.oci.image.index.v1+json'
    $Source.exitCode = $index.code
    if ($index.error -or $index.digest -notmatch '^sha256:[a-f0-9]{64}$') {
        $Source.errors += $(if ($index.error) { $index.error } else { 'Invalid or missing MCR index digest.' }); return
    }
    $Source.state = 'partial'
    $Source.details.indexDigest = $index.digest
    $Source.details.pinMatchesIndex = $pin -eq $index.digest
    $amd64 = @($index.data.manifests | Where-Object { $_.platform.os -eq 'linux' -and $_.platform.architecture -eq 'amd64' })
    if ($amd64.Count -ne 1 -or $amd64[0].digest -notmatch '^sha256:[a-f0-9]{64}$') {
        $Source.errors += 'Index lacks a unique Linux/amd64 manifest.'; return
    }
    $Source.details.amd64Digest = $amd64[0].digest
    $Source.details.pinMatchesAmd64 = $pin -eq $amd64[0].digest
    $manifest = Get-McrDocument "$base/manifests/$($amd64[0].digest)" 'application/vnd.docker.distribution.manifest.v2+json, application/vnd.oci.image.manifest.v1+json'
    $Source.exitCode = $manifest.code
    if ($manifest.error -or $manifest.digest -ne $amd64[0].digest -or $manifest.data.config.digest -notmatch '^sha256:[a-f0-9]{64}$') {
        $Source.errors += $(if ($manifest.error) { $manifest.error } else { 'Invalid AMD64 manifest or config digest.' }); return
    }
    $config = Get-McrDocument "$base/blobs/$($manifest.data.config.digest)" 'application/octet-stream'
    $Source.exitCode = $config.code
    if ($config.error -or $config.data.os -ne 'linux' -or $config.data.architecture -ne 'amd64') {
        $Source.errors += $(if ($config.error) { $config.error } else { 'Linux/amd64 config was not verified.' }); return
    }
    $versions = [ordered]@{}
    foreach ($entry in $config.data.config.Env) {
        if ($entry -match '^(DOTNET_VERSION|ASPNET_VERSION|DOTNET_SDK_VERSION)=([0-9.]+)$') {
            $versions[$Matches[1]] = $Matches[2]
        }
    }
    $Source.details.declaredVersions = $versions
    if (!$versions.DOTNET_VERSION -or !$versions.ASPNET_VERSION -or ($Name -eq 'sdk' -and !$versions.DOTNET_SDK_VERSION)) {
        $Source.errors += 'Expected SDK or runtime version metadata is missing.'; return
    }
    $Source.state = 'success'
    $Source.findingCount = 0
}

function Invoke-ScoutAudit($Source) {
    $Source.state = 'partial'
    $Source.details = @{ imageId = $ImageId; architecture = $null; entitlement = 'unverified'; severities = $null }
    if (!$ImageId) { $Source.errors += 'Specify the full local sha256 image ID.'; return }
    $settings = @{ DOCKER_SCOUT_CACHE_DIR = (Join-Path $scratch 'scout-cache') }
    $version = Invoke-AuditCommand docker @('scout','version') $scratch $settings
    if (!(Record-AuditCommand $Source $version)) { return }
    if ($version.code -ne 0 -or $version.output -notmatch '(?m)\b(v?\d+\.\d+\.\d+)\b') {
        $Source.errors += 'Scout CLI version was not verified.'; return
    }
    $Source.toolVersion = $Matches[1]
    $info = Invoke-AuditCommand docker @('info','--format','{{.ServerVersion}}') $scratch
    if (!(Record-AuditCommand $Source $info)) { return }
    if ($info.code -ne 0) { $Source.errors += 'Docker Engine unavailable or read permission denied.'; return }
    $image = Invoke-AuditCommand docker @('image','inspect','--format','{{.Id}} {{.Os}}/{{.Architecture}}',$ImageId) $scratch
    if (!(Record-AuditCommand $Source $image)) { return }
    $parts = $image.output.Trim() -split ' '
    if ($image.code -ne 0 -or $parts.Count -ne 2 -or $parts[0] -ne $ImageId -or $parts[1] -notmatch '^[a-z0-9]+/[a-z0-9]+$') {
        $Source.errors += 'The specified local image is absent or inaccessible.'; return
    }
    $Source.details.architecture = $parts[1]
    $sarif = Join-Path $scratch 'scout.sarif'
    $Source.consulted = $true
    $scan = Invoke-AuditCommand docker @('scout','cves','--format','sarif','--output',$sarif,"local://$ImageId") $scratch $settings
    if (!(Record-AuditCommand $Source $scan)) { return }
    if ($scan.code -ne 0) {
        $Source.errors += 'Scout scan incomplete. Check authentication, entitlement and feed.'; return
    }
    try {
        $data = Get-Content -Raw -LiteralPath $sarif | ConvertFrom-Json
        if ($data.version -ne '2.1.0' -or !$data.runs) { throw 'invalid SARIF' }
        $Source.findings = @(foreach ($entry in $data.runs) {
            if (!$entry.tool.driver.name -or $entry.results -isnot [array]) { throw 'invalid SARIF run' }
            foreach ($result in $entry.results) {
                $ids = @(Get-AdvisoryIds ($result.ruleId + ' ' + $result.message.text))
                $rules = @($entry.tool.driver.rules)
                $rule = if ($null -ne $result.ruleIndex) { $rules[$result.ruleIndex] }
                if ($rule.id -ne $result.ruleId) {
                    $rule = $rules | Where-Object { $_.id -eq $result.ruleId } | Select-Object -First 1
                }
                $severity = $rule.properties.cvssV3_severity
                if ($severity -notin @('critical','high','medium','low','unspecified')) {
                    if ($result.message.text -match '(?im)^Severity\s*:\s*(critical|high|medium|low|unspecified)\b') { $severity = $Matches[1] }
                    else { $severity = 'unknown' }
                }
                $package = $null
                $packageVersion = $null
                if (@($rule.properties.purls)[0] -match '^pkg:([^?@]+)@([^?]+)') {
                    $package = [Uri]::UnescapeDataString($Matches[1])
                    $packageVersion = [Uri]::UnescapeDataString($Matches[2])
                }
                $fix = if ($rule.properties.fixed_version -match '^[0-9][0-9A-Za-z.:+~-]*$') {
                    @{ version = $rule.properties.fixed_version }
                }
                @{ ids = $ids; package = $package; version = $packageVersion; severity = $severity.ToLowerInvariant()
                   applicability = 'unverified'; fix = $fix }
            }
        })
        $Source.findingCount = $Source.findings.Count
        $counts = [ordered]@{ critical = 0; high = 0; medium = 0; low = 0; unspecified = 0; unknown = 0 }
        foreach ($finding in $Source.findings) { $counts[$finding.severity]++ }
        $Source.details.severities = $counts
        $Source.details.entitlement = 'Scan accepted by Scout for this operation.'
        $Source.state = if ($Source.findingCount) { 'findings' } else { 'success' }
    } catch { $Source.errors += 'Scout SARIF missing or invalid.' }
}

function Write-AuditReports($Report, $Destination) {
    $encoding = New-Object Text.UTF8Encoding($false)
    [IO.File]::WriteAllText((Join-Path $Destination 'audit.json'), ($Report | ConvertTo-Json -Depth 24), $encoding)
    $lines = @('# SeenIt Security Audit','',
        "- Date: $($Report.startedAt)", "- HEAD: $($Report.git.head) ($($Report.git.branch), $($Report.git.state))",
        "- Scope: $($Report.scope -join ', ')", "- Result: **$($Report.state)** (exit $($Report.exitCode))",
        "- Temporary directory: $($Report.scratchCleanup)", '',
        '| Source | State | Tool/version | Feed attempted | Exit | Findings |','|---|---|---|---|---|---|')
    foreach ($source in $Report.sources) {
        $lines += "| $($source.name) | $($source.state) | $($source.tool) $($source.toolVersion) | $($source.consulted) | $($source.exitCode) | $($source.findingCount) |"
    }
    $lines += '', '## Findings and coverage'
    foreach ($source in $Report.sources) {
        $lines += '', "### $($source.name)"
        if ($source.details.reason) { $lines += "- $($source.details.reason)" }
        if ($source.details.imageId) {
            $lines += "- Local image: $($source.details.imageId) ($($source.details.architecture))"
        }
        if ($source.details.pinnedDigest) {
            $lines += "- Pinned: $($source.details.pinnedDigest)", "- Current index: $($source.details.indexDigest)",
                "- Current Linux/amd64: $($source.details.amd64Digest)"
        }
        foreach ($message in @($source.errors) + @($source.warnings) + @($source.limitations)) { $lines += "- $message" }
        foreach ($finding in $source.findings) {
            $ids = (@($finding.ids) + @($finding.advisories | ForEach-Object { $_.ids })) -join ', '
            $versions = (@($finding.version) + @($finding.versions) | Where-Object { $_ }) -join ', '
            $fix = if ($finding.fix.version) { $finding.fix.version } elseif ($finding.fix.suggested) { 'suggested by npm' } else { 'unverified' }
            $lines += "- Feed finding: $($finding.package) $versions [$($finding.severity)] $ids. Fix: $fix. Applicability: unverified."
        }
    }
    $lines += '', '## Manual follow-up','',
        '- Reconcile feed advisories with shipped versions, reachability and vendor fixes.',
        '- Review the 17 historical Scout records and libpng CVE-2026-46675 separately.',
        '- Compare image digests and OS packages before attributing CVE fixes.',
        '- Review Data Protection, GitHub settings, the Render artifact and Vercel Node version.',
        '- See docs/SECURITY-MONITORING-RUNBOOK.md for triage and authorized validation.'
    [IO.File]::WriteAllLines((Join-Path $Destination 'audit.md'), [string[]]$lines, $encoding)
}

$exitCode = 3
try {
    if ($ImageId -and !$IncludeScout) { throw 'ImageId requires IncludeScout.' }
    $started = [DateTimeOffset]::Now
    $id = $started.UtcDateTime.ToString('yyyyMMddTHHmmssfffZ') + '-' + [guid]::NewGuid().ToString('N')
    $repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
    $scratch = [IO.Path]::GetTempPath()
    $worktrees = Invoke-AuditCommand git @('--no-optional-locks','worktree','list','--porcelain','-z') $repo
    if ($worktrees.error -or $worktrees.code -ne 0) { throw 'Cannot identify repository worktrees.' }
    $trees = @($repo) + @($worktrees.output -split '\x00' | Where-Object { $_ -like 'worktree *' } | ForEach-Object { $_.Substring(9) })
    $outputRoot = Get-SafeAuditPath $OutputDirectory $trees
    $tempRoot = Get-SafeAuditPath $scratch $trees
    $scratchName = 'seenit-audit-scratch-' + [guid]::NewGuid().ToString('N')
    $scratch = Join-Path $tempRoot $scratchName
    $destination = Join-Path $outputRoot "seenit-audit-$id"
    if ((Test-Path -LiteralPath $scratch) -or (Test-Path -LiteralPath $destination)) { throw 'Execution directory already exists.' }
    [void][IO.Directory]::CreateDirectory($scratch)
    $scratchCreated = $true
    [void][IO.Directory]::CreateDirectory($destination)

    $head = Invoke-AuditCommand git @('--no-optional-locks','rev-parse','HEAD') $repo
    $branch = Invoke-AuditCommand git @('--no-optional-locks','rev-parse','--abbrev-ref','HEAD') $repo
    $status = Invoke-AuditCommand git @('--no-optional-locks','status','--porcelain=v1','--untracked-files=normal') $repo
    $gitVersion = Invoke-AuditCommand git @('--version') $repo
    if (@(@($head,$branch,$status,$gitVersion) | Where-Object { $_.error -or $_.code -ne 0 }).Count -or
        $head.output.Trim() -notmatch '^[0-9a-f]{40}$') { throw 'Cannot verify Git metadata.' }

    $selected = @(if ('All' -in $Scope) { 'Npm','NuGet','Mcr' } else { $Scope | Sort-Object -Unique })
    $definitions = @(
        @{ name = 'npm-all'; tool = 'npm'; scope = 'Full frontend lockfile'; feed = 'https://registry.npmjs.org/'; selected = 'Npm' -in $selected; limitation = 'Lockfile coverage differs from shipped browser chunks.' },
        @{ name = 'npm-production'; tool = 'npm'; scope = 'Frontend lockfile excluding dev'; feed = 'https://registry.npmjs.org/'; selected = 'Npm' -in $selected; limitation = 'Lockfile coverage differs from shipped browser chunks.' },
        @{ name = 'nuget'; tool = 'dotnet'; scope = 'Four backend projects, net10.0/linux-x64, direct and transitive'; feed = 'https://api.nuget.org/v3/index.json'; selected = 'NuGet' -in $selected; limitation = 'Shared frameworks, static codecs and published artifacts require separate review.' },
        @{ name = 'mcr-sdk'; tool = 'curl'; scope = 'SDK tag index, Linux/amd64 and config'; feed = 'https://mcr.microsoft.com/'; selected = 'Mcr' -in $selected; limitation = 'A digest change does not establish a CVE fix.' },
        @{ name = 'mcr-aspnet'; tool = 'curl'; scope = 'ASP.NET tag index, Linux/amd64 and config'; feed = 'https://mcr.microsoft.com/'; selected = 'Mcr' -in $selected; limitation = 'A digest change does not establish a CVE fix.' },
        @{ name = 'scout'; tool = 'docker scout'; scope = 'Explicit local image'; feed = 'Docker Scout advisory service'; selected = [bool]$IncludeScout; limitation = 'Static codecs can escape coverage. A local image does not attest the Render deploy.' }
    )
    $sources = @(foreach ($definition in $definitions) {
        $source = [ordered]@{
            name = $definition.name; state = 'skipped'; time = [DateTimeOffset]::Now.ToString('o')
            exitCode = $null; tool = $definition.tool; toolVersion = $null; scope = $definition.scope
            feed = $definition.feed; consulted = $false; selected = $definition.selected
            findingCount = $null; findings = @(); errors = @(); warnings = @()
            limitations = @($definition.limitation); details = $null
        }
        if (!$definition.selected) { $source.details = @{ reason = 'Not selected.' } }
        elseif ($Offline) {
            $source.details = @{ reason = 'Offline: requires a remote feed.' }
            $source.warnings += 'Selected coverage was not executed.'
        } else {
            $source.state = 'failed'
            try {
                switch ($definition.name) {
                    'npm-all'        { Invoke-NpmAudit $source $false }
                    'npm-production' { Invoke-NpmAudit $source $true }
                    'nuget'          { Invoke-NuGetAudit $source }
                    'mcr-sdk'        { Invoke-McrAudit $source 'sdk' }
                    'mcr-aspnet'     { Invoke-McrAudit $source 'aspnet' }
                    'scout'          { Invoke-ScoutAudit $source }
                }
            } catch { $source.errors += 'Unable to prepare or interpret this source. Raw diagnostics omitted.' }
        }
        $source
    })

    $requested = @($sources | Where-Object selected)
    $completed = @($requested | Where-Object { $_.state -in @('success','findings') })
    $incomplete = @($requested | Where-Object { $_.state -in @('failed','partial','skipped') })
    $state = 'success'
    $exitCode = 0
    if ($incomplete.Count) {
        if ($completed.Count -or @($incomplete | Where-Object { $_.state -in @('partial','skipped') }).Count) {
            $state = 'partial'; $exitCode = 2
        } else { $state = 'failed'; $exitCode = 3 }
    } elseif ('findings' -in $requested.state) { $state = 'findings'; $exitCode = 1 }
    $gitState = if ($status.output.Trim()) { 'modified' } else { 'clean' }
    $report = [ordered]@{
        schemaVersion = 1; runId = $id; startedAt = $started.ToString('o')
        finishedAt = [DateTimeOffset]::Now.ToString('o'); repository = 'GiovannaGrigo/SeenIt'
        git = @{ branch = $branch.output.Trim(); head = $head.output.Trim(); state = $gitState
            representsOnlyCommit = $gitState -eq 'clean'; version = $gitVersion.output.Trim() }
        host = @{ os = [Environment]::OSVersion.VersionString; powershell = $PSVersionTable.PSVersion.ToString() }
        scope = $selected; offline = [bool]$Offline; timeoutSeconds = $TimeoutSeconds
        scoutRequested = [bool]$IncludeScout; imageId = $ImageId; state = $state; exitCode = $exitCode
        hasFindings = @($sources | Where-Object { $_.findingCount -gt 0 }).Count -gt 0; sources = $sources
    }
} catch {
    Write-Error ('Audit could not generate reports ({0}, line {1}). Check Git metadata, arguments and directory access.' -f $_.Exception.GetType().Name, $_.InvocationInfo.ScriptLineNumber) -ErrorAction Continue
    $exitCode = 3
} finally {
    $cleanup = 'not-created'
    if ($scratchCreated) {
        $cleanup = 'retained'
        try {
            $verified = Get-SafeAuditPath $scratch $trees
            if ($verified -eq (Join-Path $tempRoot $scratchName)) {
                [IO.Directory]::Delete(('\\?\' + $verified), $true)
                $cleanup = 'removed'
            }
        } catch { Write-Warning 'Could not clean this run''s temporary audit directory.' }
    }
}
if ($report) {
    $report.scratchCleanup = $cleanup
    $report.finishedAt = [DateTimeOffset]::Now.ToString('o')
    if ($cleanup -eq 'retained' -and $exitCode -lt 2) {
        $report.state = 'partial'
        $report.exitCode = $exitCode = 2
    }
    try {
        Write-AuditReports $report $destination
        Write-Output $destination
    } catch {
        Write-Error 'Could not write both audit reports.' -ErrorAction Continue
        $exitCode = 3
    }
}
exit $exitCode
