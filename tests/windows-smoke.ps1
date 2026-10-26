param([Parameter(Mandatory)][string]$Payload,[string]$Version='2.0.0')
$ErrorActionPreference='Stop'
$workspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$payloadPath=(Resolve-Path -LiteralPath $Payload).Path
if(!$payloadPath.StartsWith((Join-Path $workspace 'artifacts')+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Use somente payload de QA dentro de artifacts.'}
$qaRoot=Join-Path $workspace ('artifacts/qa/windows/'+$Version+'-'+[Guid]::NewGuid().ToString('N'))
$install=Join-Path $qaRoot 'installed';New-Item -ItemType Directory -Force $qaRoot|Out-Null
$iscc=Join-Path $env:LOCALAPPDATA 'Programs/Inno Setup 6/ISCC.exe'
$qaOutput='Orbe-QA-Setup-'+$Version
& $iscc /Q "/DOrbeVersion=$Version" "/DOrbePayload=$payloadPath" /DOrbeQa=1 "/DOrbeAppId=Orbe.QA.$Version" "/DOrbeOutput=$qaOutput" (Join-Path $workspace 'installer/orbe.iss') *> (Join-Path $qaRoot 'compile.txt')
if($LASTEXITCODE -ne 0){throw 'Falha ao compilar instalador isolado de QA.'}
$installer=Join-Path $workspace ('dist/'+$qaOutput+'.exe')
$setup=Start-Process -FilePath $installer -ArgumentList '/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/NOICONS',('/DIR="'+$install+'"'),('/LOG="'+(Join-Path $qaRoot 'install.log')+'"') -WindowStyle Hidden -PassThru
if(!$setup.WaitForExit(60000) -or $setup.ExitCode -ne 0){throw 'Instalação isolada falhou.'}
$savedDotnetRoot=$env:DOTNET_ROOT
try{
 Remove-Item Env:DOTNET_ROOT -ErrorAction SilentlyContinue
 foreach($name in 'Agenda.App.exe','coreclr.dll','hostfxr.dll','Microsoft.UI.Xaml.dll','e_sqlite3.dll'){if(!(Test-Path -LiteralPath (Join-Path $install $name))){throw "Dependência autocontida ausente: $name"}}
 $app=Start-Process -FilePath (Join-Path $install 'Agenda.App.exe') -ArgumentList '--startup-check' -WindowStyle Hidden -PassThru
 if(!$app.WaitForExit(20000)){Stop-Process -Id $app.Id;throw 'Abertura do app instalado excedeu prazo.'}
 $report=Join-Path ([IO.Path]::GetTempPath()) "orbe-startup-check-$($app.Id).json"
 if(!(Test-Path -LiteralPath $report)){throw 'Relatório de abertura do app instalado ausente.'}
 $result=Get-Content -LiteralPath $report -Raw|ConvertFrom-Json
 if($app.ExitCode -ne 0 -or !$result.visible -or $result.captureError -or !$result.version.StartsWith($Version+'.')){throw 'App instalado não passou no teste de versão/janela/renderização.'}
 Copy-Item -LiteralPath $report -Destination (Join-Path $qaRoot 'startup.json')
 Copy-Item -LiteralPath $result.screenshot -Destination (Join-Path $qaRoot 'login.png')
 'PASS: instalação isolada sem registro/atalhos pessoais; app autocontido abriu login, versão '+$result.version+'.'|Tee-Object -FilePath (Join-Path $qaRoot 'result.txt')
}finally{
 if($null -eq $savedDotnetRoot){Remove-Item Env:DOTNET_ROOT -ErrorAction SilentlyContinue}else{$env:DOTNET_ROOT=$savedDotnetRoot}
 $uninstaller=Join-Path $install 'unins000.exe'
 if(Test-Path -LiteralPath $uninstaller){$remove=Start-Process -FilePath $uninstaller -ArgumentList '/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART' -WindowStyle Hidden -PassThru;if(!$remove.WaitForExit(30000) -or $remove.ExitCode -ne 0){throw 'Desinstalação isolada precisa ser verificada.'}}
}
if(Test-Path -LiteralPath (Join-Path $install 'Agenda.App.exe')){throw 'Executável de QA permaneceu após desinstalação.'}
'PASS: desinstalação isolada concluída sem alterar instalação pessoal.'|Tee-Object -FilePath (Join-Path $qaRoot 'result.txt') -Append
