param([ValidatePattern('^\d+\.\d+\.\d+$')][string]$Version='2.0.5',[switch]$SkipTests)
$ErrorActionPreference='Stop'
$dotnet=Join-Path $env:LOCALAPPDATA 'NexusDev\dotnet\dotnet.exe'
if(!(Test-Path -LiteralPath $dotnet)){$dotnet=(Get-Command dotnet -ErrorAction Stop).Source}
$iscc=Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'
if(!(Test-Path -LiteralPath $iscc)){$iscc=(Get-Command ISCC.exe -ErrorAction Stop).Source}
Push-Location $PSScriptRoot
try {
 if(!$SkipTests){& $dotnet run --project tests/Agenda.Checks;if($LASTEXITCODE -ne 0){throw 'Testes falharam.'}}
 & $dotnet restore src/Agenda.App -p:AgendaTargetFramework=net10.0-windows10.0.19041.0 -p:RuntimeIdentifier=win-x64
 if($LASTEXITCODE -ne 0){throw 'Restauração Windows falhou.'}
 & $dotnet clean src/Agenda.App -c Release -f net10.0-windows10.0.19041.0 -p:AgendaTargetFramework=net10.0-windows10.0.19041.0 -v:q
 if($LASTEXITCODE -ne 0){throw 'Limpeza de build falhou.'}
 $payload=Join-Path $PSScriptRoot ("artifacts/release-windows/"+$Version+"-"+[Guid]::NewGuid().ToString('N'))
 & $dotnet publish src/Agenda.App -f net10.0-windows10.0.19041.0 -p:AgendaTargetFramework=net10.0-windows10.0.19041.0 -c Release -p:WindowsAppSDKSelfContained=true -p:SelfContained=true -p:RuntimeIdentifier=win-x64 -p:DebugType=None -p:DebugSymbols=false "-p:ApplicationDisplayVersion=$Version" -o $payload
 if($LASTEXITCODE -ne 0){throw 'Publicação Windows falhou.'}
 Copy-Item -LiteralPath licenses -Destination $payload -Recurse -Force
 Copy-Item docs/PRIMEIROS-PASSOS.md $payload/PRIMEIROS-PASSOS.md
 # Preserve the checked-in multi-resolution ICO, including rounded transparency.
 & $iscc /Qp "/DOrbeVersion=$Version" "/DOrbePayload=$payload" installer/orbe.iss
 if($LASTEXITCODE -ne 0){throw 'Instalador falhou.'}
 Get-FileHash dist/Orbe-Setup.exe -Algorithm SHA256 | ForEach-Object{$_.Hash.ToLowerInvariant()+'  Orbe-Setup.exe'} | Set-Content dist/SHA256SUMS.txt -Encoding ascii
}finally{Pop-Location}
