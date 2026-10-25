param([string]$Version='1.0.0')
$ErrorActionPreference='Stop'
$iscc=Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'
$root=Join-Path ([IO.Path]::GetTempPath()) ('orbe-installer-qa-'+[guid]::NewGuid().ToString('N'))
$target=Join-Path $root 'programa';$testId='Orbe.QA.'+[guid]::NewGuid().ToString('N')
function RunInstaller([string]$Exe,[string[]]$Arguments){$p=Start-Process -FilePath $Exe -ArgumentList $Arguments -WindowStyle Hidden -Wait -PassThru;if($p.ExitCode -ne 0){throw "Instalador retornou $($p.ExitCode)"}}
function CheckApp {
 $p=Start-Process -FilePath (Join-Path $target 'Agenda.App.exe') -ArgumentList '--startup-check' -WindowStyle Hidden -PassThru
 if(!$p.WaitForExit(30000)){Stop-Process -Id $p.Id;throw 'Aplicativo não concluiu startup-check.'}
 if($p.ExitCode -ne 0){throw 'Aplicativo falhou na instalação de teste.'}
 $report=Join-Path ([IO.Path]::GetTempPath()) "orbe-startup-check-$($p.Id).json"
 $r=Get-Content -LiteralPath $report -Raw|ConvertFrom-Json
 if(!$r.visible -or ($r.version -ne $Version -and !$r.version.StartsWith($Version+'.'))){throw 'Janela invisível ou versão incorreta.'}
 Remove-Item -LiteralPath $report
}
Push-Location $PSScriptRoot\..
try {
 New-Item -ItemType Directory -Force $root|Out-Null
 & $iscc "/DOrbeVersion=$Version" "/DOrbePayload=$PSScriptRoot\..\artifacts\release-windows" "/DOrbeAppId=$testId" /DOrbeOutput=Orbe-Setup-QA installer/orbe.iss | Out-Null
 if($LASTEXITCODE -ne 0){throw 'Compilação QA falhou.'}
 $args=@('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/NOICONS',('/DIR="'+$target+'"'),('/LOG="'+(Join-Path $root 'install.log')+'"'))
 RunInstaller (Join-Path (Get-Location) 'dist\Orbe-Setup-QA.exe') $args
 CheckApp
 $marker=Join-Path $target 'preservar.sqlite';Set-Content -LiteralPath $marker -Value 'fixture-sintetica' -Encoding ascii
 RunInstaller (Join-Path (Get-Location) 'dist\Orbe-Setup-QA.exe') $args
 if((Get-Content -LiteralPath $marker -Raw).Trim() -ne 'fixture-sintetica'){throw 'Reinstalação alterou dados.'}
 CheckApp
 RunInstaller (Join-Path $target 'unins000.exe') @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART')
 if(!(Test-Path -LiteralPath $marker)){throw 'Desinstalação apagou dados não gerenciados.'}
 if(Test-Path -LiteralPath (Join-Path $target 'Agenda.App.exe')){throw 'Desinstalação deixou o programa.'}
 if(Test-Path -LiteralPath "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\${testId}_is1"){throw 'Registro de desinstalação permaneceu.'}
 New-Item -ItemType Directory -Force artifacts/qa|Out-Null
 'Instalação, abertura real WinUI via MAUI, reinstalação, preservação de arquivo não gerenciado e desinstalação: passaram. Conta/registro/pasta exclusivos de QA.'|Set-Content artifacts/qa/installer-1.0.0.txt -Encoding utf8
 Write-Output 'PASS instalação, abertura WinUI real, reinstalação e desinstalação com preservação de dados sintéticos.'
}finally{
 if(Test-Path -LiteralPath (Join-Path $target 'unins000.exe')){RunInstaller (Join-Path $target 'unins000.exe') @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART')}
 $resolved=[IO.Path]::GetFullPath($root);$prefix=[IO.Path]::GetFullPath([IO.Path]::GetTempPath())
 if(!$resolved.StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase)-or !(Split-Path $resolved -Leaf).StartsWith('orbe-installer-qa-')){throw 'Diretório QA inválido para limpeza.'}
 if(Test-Path -LiteralPath $resolved){Remove-Item -LiteralPath $resolved -Recurse -Force}
 Pop-Location
}
