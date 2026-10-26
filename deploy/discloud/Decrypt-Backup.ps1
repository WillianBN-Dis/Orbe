param([Parameter(Mandatory)][string]$EncryptedFile,[Parameter(Mandatory)][string]$Destination)
$ErrorActionPreference='Stop'
$source=(Resolve-Path -LiteralPath $EncryptedFile).Path
$target=[IO.Path]::GetFullPath($Destination)
if([IO.Path]::GetExtension($source) -ne '.orbebak' -or [IO.Path]::GetExtension($target) -ne '.dump'){throw 'Selecione um .orbebak e um destino novo .dump.'}
if(Test-Path -LiteralPath $target){throw 'O destino já existe; nenhum arquivo será sobrescrito.'}
$vaultFile=Join-Path $env:LOCALAPPDATA 'OrbeDev/cloud-test.dpapi'
$node=Join-Path $env:USERPROFILE '.cache/codex-runtimes/codex-primary-runtime/dependencies/node/bin/node.exe'
try{
 $secure=ConvertTo-SecureString ([IO.File]::ReadAllText($vaultFile).Trim())
 $vault=[Net.NetworkCredential]::new('',$secure).Password|ConvertFrom-Json
 $flags=[Security.Cryptography.X509Certificates.X509KeyStorageFlags]::EphemeralKeySet -bor [Security.Cryptography.X509Certificates.X509KeyStorageFlags]::Exportable
 $cert=[Security.Cryptography.X509Certificates.X509CertificateLoader]::LoadPkcs12([Convert]::FromBase64String($vault.Certificate),$vault.CertificatePassword,$flags)
 $rsa=[Security.Cryptography.X509Certificates.RSACertificateExtensions]::GetRSAPrivateKey($cert)
 $payload=@{input=$source;output=$target;privateKey=$rsa.ExportPkcs8PrivateKeyPem()}|ConvertTo-Json -Compress
 $payload|& $node (Join-Path $PSScriptRoot 'backup.js')
 if($LASTEXITCODE -ne 0){throw 'Arquivo inválido, alterado ou chave incompatível. Nenhuma saída restaurada foi mantida.'}
 Write-Output 'Dump autenticado recuperado. Restaure apenas em um banco novo e isolado, nunca sobre o banco em uso.'
}finally{
 if($rsa){$rsa.Dispose()};if($cert){$cert.Dispose()}
 Remove-Variable payload,vault,secure -ErrorAction SilentlyContinue
}
