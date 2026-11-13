param()
$ErrorActionPreference='Stop'
$certificateRoot=Join-Path $PSScriptRoot '.synthetic-tls'
if (-not (Test-Path -LiteralPath (Join-Path $certificateRoot 'server.key'))) { throw 'Generate synthetic certificates first.' }
$startupScript=Join-Path $PSScriptRoot 'tls-start.sh'
# Exclusive names: Docker refuses reuse; no existing resource is removed.
docker run --detach --rm --name seenit-b03b21-tls --label seenit.audit=b03b21 --publish 127.0.0.1:15434:5432 --mount "type=bind,source=$certificateRoot,target=/tls,readonly" --mount "type=bind,source=$startupScript,target=/audit-start.sh,readonly" --env POSTGRES_PASSWORD=b03b21_synthetic_admin --env POSTGRES_DB=seenit_b03b21 --entrypoint /bin/sh postgres:17-alpine /audit-start.sh server
if ($LASTEXITCODE -ne 0) { throw 'TLS container creation failed.' }
docker run --detach --rm --name seenit-b03b21-expired --label seenit.audit=b03b21 --publish 127.0.0.1:15435:5432 --mount "type=bind,source=$certificateRoot,target=/tls,readonly" --mount "type=bind,source=$startupScript,target=/audit-start.sh,readonly" --env POSTGRES_PASSWORD=b03b21_synthetic_admin --env POSTGRES_DB=seenit_b03b21 --entrypoint /bin/sh postgres:17-alpine /audit-start.sh expired
if ($LASTEXITCODE -ne 0) { throw 'Expired-certificate test container creation failed; inspect dedicated resources.' }
