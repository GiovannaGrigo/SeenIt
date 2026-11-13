param()
$ErrorActionPreference = 'Stop'
# Never reuse or remove an existing container. Docker itself refuses an occupied name.
docker run --detach --rm --name seenit-b03b2-local --label seenit.audit=b03b2 --publish 127.0.0.1:15433:5432 --env POSTGRES_PASSWORD=b03b2_synthetic_admin --env POSTGRES_DB=seenit_b03b2 postgres:17-alpine
if ($LASTEXITCODE -ne 0) { throw 'Container creation failed; no existing resource was modified.' }
