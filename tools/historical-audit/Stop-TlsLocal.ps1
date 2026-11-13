param()
$ErrorActionPreference='Stop'
foreach($containerName in @('seenit-b03b21-tls','seenit-b03b21-expired')) {
    $containerText=docker inspect $containerName
    if($LASTEXITCODE -ne 0) { throw 'Dedicated TLS container not found; no substitute selected.' }
    $containerInfo=($containerText|ConvertFrom-Json)[0]
    if($containerInfo.Name -ne "/$containerName" -or $containerInfo.Config.Labels.'seenit.audit' -ne 'b03b21') {
        throw 'Ownership mismatch; container preserved.'
    }
    docker stop $containerInfo.Id
    if($LASTEXITCODE -ne 0) { throw 'Dedicated container could not be stopped.' }
}
