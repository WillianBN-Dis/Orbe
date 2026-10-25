param([string]$Version='1.0.0')
$ErrorActionPreference='Stop'
$dotnet=Join-Path $env:LOCALAPPDATA 'NexusDev\dotnet\dotnet.exe'
if(!(Test-Path -LiteralPath $dotnet)){$dotnet=(Get-Command dotnet -ErrorAction Stop).Source}
$java=Join-Path $env:LOCALAPPDATA 'AgendaDev\jdk'
$sdk=Join-Path $env:LOCALAPPDATA 'AgendaDev\android-sdk'
$signing=Join-Path $env:LOCALAPPDATA 'OrbeDev\signing'
New-Item -ItemType Directory -Force $signing | Out-Null
$key=Join-Path $signing 'orbe-release.keystore';$protected=Join-Path $signing 'password.dpapi'
try {
 if(!(Test-Path -LiteralPath $key)){
  if(Test-Path -LiteralPath $protected){throw 'Senha existente sem keystore. Recupere o backup antes de continuar.'}
  $bytes=[Security.Cryptography.RandomNumberGenerator]::GetBytes(32)
  $env:ORBE_ANDROID_SIGNING_PASSWORD=[Convert]::ToBase64String($bytes)
  ConvertTo-SecureString $env:ORBE_ANDROID_SIGNING_PASSWORD -AsPlainText -Force | ConvertFrom-SecureString | Set-Content -LiteralPath $protected -Encoding ascii
  & "$java\bin\keytool.exe" -genkeypair -keystore $key -storepass:env ORBE_ANDROID_SIGNING_PASSWORD -keypass:env ORBE_ANDROID_SIGNING_PASSWORD -alias orbe -keyalg RSA -keysize 3072 -validity 10000 -dname 'CN=Orbe, O=Orbe' -noprompt
  if($LASTEXITCODE -ne 0){throw 'Não foi possível criar a chave Android.'}
 }else{
  $secret=ConvertTo-SecureString (Get-Content -LiteralPath $protected -Raw).Trim()
  $env:ORBE_ANDROID_SIGNING_PASSWORD=[Net.NetworkCredential]::new('', $secret).Password
 }
 Push-Location $PSScriptRoot
 try {
  & $dotnet publish src/Agenda.App -f net10.0-android -p:AgendaTargetFramework=net10.0-android -c Release -p:AndroidPackageFormats=apk -p:EmbedAssembliesIntoApk=true -p:PublishTrimmed=false -p:RunAOTCompilation=false "-p:AndroidSdkDirectory=$sdk" "-p:JavaSdkDirectory=$java" -p:AndroidKeyStore=true "-p:AndroidSigningKeyStore=$key" -p:AndroidSigningKeyAlias=orbe -p:AndroidSigningStorePass=env:ORBE_ANDROID_SIGNING_PASSWORD -p:AndroidSigningKeyPass=env:ORBE_ANDROID_SIGNING_PASSWORD "-p:ApplicationDisplayVersion=$Version"
  if($LASTEXITCODE -ne 0){throw 'Publicação Android falhou.'}
  New-Item -ItemType Directory -Force dist | Out-Null
  $apk=Get-ChildItem src/Agenda.App/bin/Release/net10.0-android/publish -Filter '*-Signed.apk' | Select-Object -First 1
  if(!$apk){throw 'APK assinado não encontrado.'}
  Copy-Item -LiteralPath $apk.FullName -Destination "dist/Orbe-$Version.apk"
 }finally{Pop-Location}
}finally{Remove-Item Env:ORBE_ANDROID_SIGNING_PASSWORD -ErrorAction SilentlyContinue}
