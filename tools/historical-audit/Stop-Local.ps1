param()
$ErrorActionPreference = 'Stop'
$containerText = docker inspect seenit-b03b2-local
if ($LASTEXITCODE -ne 0) { throw 'Dedicated container not found; nothing was stopped.' }
$containerInfo = ($containerText | ConvertFrom-Json)[0]
if ($containerInfo.Name -ne '/seenit-b03b2-local' -or $containerInfo.Config.Labels.'seenit.audit' -ne 'b03b2') {
    throw 'Container ownership label mismatch; nothing was stopped.'
}
# Exact inspected ID; never enumerate or remove containers belonging to other tasks.
docker stop $containerInfo.Id
if ($LASTEXITCODE -ne 0) { throw 'Dedicated container could not be stopped.' }
