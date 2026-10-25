param([ValidatePattern('^\d+\.\d+\.\d+$')][string]$Version='1.0.0',[switch]$SkipTests)
$ErrorActionPreference='Stop'
$dotnet=Join-Path $env:LOCALAPPDATA 'NexusDev\dotnet\dotnet.exe'
if(!(Test-Path -LiteralPath $dotnet)){$dotnet=(Get-Command dotnet -ErrorAction Stop).Source}
$iscc=Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'
if(!(Test-Path -LiteralPath $iscc)){$iscc=(Get-Command ISCC.exe -ErrorAction Stop).Source}
Push-Location $PSScriptRoot
try {
 if(!$SkipTests){& $dotnet run --project tests/Agenda.Checks;if($LASTEXITCODE -ne 0){throw 'Testes falharam.'}}
 & $dotnet clean src/Agenda.App -c Release -f net10.0-windows10.0.19041.0 -p:AgendaTargetFramework=net10.0-windows10.0.19041.0 -v:q
 if($LASTEXITCODE -ne 0){throw 'Limpeza de build falhou.'}
 $payload=Join-Path $PSScriptRoot 'artifacts\release-windows'
 & $dotnet publish src/Agenda.App -f net10.0-windows10.0.19041.0 -p:AgendaTargetFramework=net10.0-windows10.0.19041.0 -c Release -p:WindowsAppSDKSelfContained=true -p:SelfContained=true -p:RuntimeIdentifier=win-x64 -p:DebugType=None -p:DebugSymbols=false "-p:ApplicationDisplayVersion=$Version" -o $payload
 if($LASTEXITCODE -ne 0){throw 'Publicação Windows falhou.'}
 Copy-Item -LiteralPath licenses -Destination $payload -Recurse -Force
 Copy-Item database/001-inicial.sql $payload/001-inicial.sql
 Copy-Item docs/PRIMEIROS-PASSOS.md $payload/PRIMEIROS-PASSOS.md
 $png=Join-Path $PSScriptRoot 'assets\orbe-icon.png';$icon=Join-Path $PSScriptRoot 'assets\orbe.ico'
 # ICO com PNG 256px, preservando a transparência do asset original.
 $bytes=[IO.File]::ReadAllBytes($png);$stream=[IO.File]::Create($icon)
 try{$writer=[IO.BinaryWriter]::new($stream);$writer.Write([uint16]0);$writer.Write([uint16]1);$writer.Write([uint16]1);$writer.Write([byte]0);$writer.Write([byte]0);$writer.Write([byte]0);$writer.Write([byte]0);$writer.Write([uint16]1);$writer.Write([uint16]32);$writer.Write([uint32]$bytes.Length);$writer.Write([uint32]22);$writer.Write($bytes);$writer.Flush()}finally{$stream.Dispose()}
 & $iscc /Qp "/DOrbeVersion=$Version" "/DOrbePayload=$payload" installer/orbe.iss
 if($LASTEXITCODE -ne 0){throw 'Instalador falhou.'}
 Get-FileHash dist/Orbe-Setup.exe -Algorithm SHA256 | ForEach-Object{$_.Hash.ToLowerInvariant()+'  Orbe-Setup.exe'} | Set-Content dist/SHA256SUMS.txt -Encoding ascii
}finally{Pop-Location}
