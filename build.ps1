param([ValidateSet('Windows','Android')][string]$Target = 'Windows')
$ErrorActionPreference = 'Stop'
$dotnet = Join-Path $env:LOCALAPPDATA 'NexusDev\dotnet\dotnet.exe'
if (!(Test-Path -LiteralPath $dotnet)) { $dotnet = (Get-Command dotnet -ErrorAction Stop).Source }
Push-Location $PSScriptRoot
try {
    & $dotnet run --project tests/Agenda.Checks
    if ($LASTEXITCODE -ne 0) { throw 'Testes falharam.' }
    if ($Target -eq 'Windows') {
        & $dotnet publish src/Agenda.App -f net10.0-windows10.0.19041.0 -p:AgendaTargetFramework=net10.0-windows10.0.19041.0 -c Release -p:WindowsAppSDKSelfContained=true -p:SelfContained=true -p:RuntimeIdentifier=win-x64 -o artifacts/orbe-windows
    } else {
        & $dotnet build src/Agenda.App -f net10.0-android -p:AgendaTargetFramework=net10.0-android -p:AndroidPackageFormats=apk -p:EmbedAssembliesIntoApk=true "-p:AndroidSdkDirectory=$env:LOCALAPPDATA\AgendaDev\android-sdk" "-p:JavaSdkDirectory=$env:LOCALAPPDATA\AgendaDev\jdk"
    }
    if ($LASTEXITCODE -ne 0) { throw 'Compilação falhou.' }
    if ($Target -eq 'Android') {
        New-Item -ItemType Directory -Force artifacts/android | Out-Null
        Copy-Item -LiteralPath 'src/Agenda.App/bin/Debug/net10.0-android/br.com.agendafinanceira.app-Signed.apk' -Destination 'artifacts/android/Orbe-1.0.0-debug.apk'
    }
} finally { Pop-Location }



