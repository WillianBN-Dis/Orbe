$ErrorActionPreference='Stop'
$workspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$cluster=[IO.Path]::GetFullPath((Join-Path $workspace 'artifacts/cloud-dev/pgdata'))
if(!$cluster.StartsWith((Join-Path $workspace 'artifacts')+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'QA cluster outside artifacts'}
$pg=Join-Path $workspace 'artifacts/cloud-dev/pg/pgsql/bin/pg_ctl.exe'
& $pg status -D $cluster *> $null
if($LASTEXITCODE -eq 0){throw 'QA cluster already running'}
$output=Join-Path $workspace 'artifacts/qa/2.0.1';New-Item -ItemType Directory -Force $output|Out-Null
& $pg start -D $cluster -l (Join-Path $output 'postgres.log') -o '-h 127.0.0.1 -p 55439' -w
if($LASTEXITCODE -ne 0){throw 'QA cluster failed to start'}
try{
 $password=[IO.File]::ReadAllText((Join-Path $workspace 'artifacts/cloud-dev/test-password.txt')).Trim()
 $env:ORBE_TEST_DATABASE='Host=127.0.0.1;Port=55439;Database=orbe_cloud_test;Username=orbe_test;Password='+$password
 Push-Location $workspace
 try{& (Join-Path $env:LOCALAPPDATA 'NexusDev/dotnet/dotnet.exe') run --project tests/Orbe.Server.Checks -p:UseSharedCompilation=false *> (Join-Path $output 'http-tests.log');if($LASTEXITCODE -ne 0){throw 'HTTP suite failed; see private QA log'}}finally{Pop-Location}
 Get-Content (Join-Path $output 'http-tests.log') -Tail 12
}finally{
 & $pg stop -D $cluster -m fast -w *> $null
 Remove-Item Env:ORBE_TEST_DATABASE -ErrorAction SilentlyContinue
}
