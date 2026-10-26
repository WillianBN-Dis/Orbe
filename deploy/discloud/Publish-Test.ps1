param([string]$SiteName='orbe-willianbn',[string]$OwnerEmail='', [switch]$SkipBuild,[string[]]$TrustedProxyAddresses=@(),[switch]$ProbeProxy)
$ErrorActionPreference='Stop'
$workspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$output=Join-Path $workspace 'artifacts/discloud-test'
$vaultFolder=Join-Path $env:LOCALAPPDATA 'OrbeDev'
$vaultFile=Join-Path $vaultFolder 'cloud-test.dpapi'
$stateFile=Join-Path $output 'state.json'
New-Item -ItemType Directory -Force $output,$vaultFolder | Out-Null
function RandomSecret { [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32)) }
function SaveVault { $vault|ConvertTo-Json -Depth 12 -Compress|ConvertTo-SecureString -AsPlainText -Force|ConvertFrom-SecureString|Set-Content -LiteralPath $vaultFile -Encoding ascii }
function SaveState { $state|ConvertTo-Json -Depth 6|Set-Content -LiteralPath $stateFile -Encoding utf8 }
if(Test-Path -LiteralPath $vaultFile){$secure=ConvertTo-SecureString ([IO.File]::ReadAllText($vaultFile).Trim());$vault=[Net.NetworkCredential]::new('',$secure).Password|ConvertFrom-Json -AsHashtable}
else{
 $rsa=[Security.Cryptography.RSA]::Create(2048)
 $request=[Security.Cryptography.X509Certificates.CertificateRequest]::new('CN=Orbe Test Data Protection',$rsa,[Security.Cryptography.HashAlgorithmName]::SHA256,[Security.Cryptography.RSASignaturePadding]::Pkcs1)
 $cert=$request.CreateSelfSigned([DateTimeOffset]::UtcNow.AddMinutes(-5),[DateTimeOffset]::UtcNow.AddYears(5));$certPassword=RandomSecret
 $vault=@{OwnerPassword=('Aa1!'+(RandomSecret));DbPassword=(RandomSecret);CertificatePassword=$certPassword;Certificate=[Convert]::ToBase64String($cert.Export([Security.Cryptography.X509Certificates.X509ContentType]::Pfx,$certPassword));OwnerEmail=$OwnerEmail};$cert.Dispose();$rsa.Dispose();SaveVault
}
if($OwnerEmail){$vault.OwnerEmail=$OwnerEmail;SaveVault}
$state=if(Test-Path -LiteralPath $stateFile){Get-Content -LiteralPath $stateFile -Raw|ConvertFrom-Json -AsHashtable}else{@{SiteName=$SiteName;SiteId='';DatabaseId='';DatabaseHost='';DatabaseReady=$false;PublicUrl=''}}
if($state.SiteName -ne $SiteName){throw 'O estado existente usa outro nome de site. Não será sobrescrito.'}
if($TrustedProxyAddresses.Count -gt 0){foreach($address in $TrustedProxyAddresses){$parsed=$null;if(![Net.IPAddress]::TryParse($address,[ref]$parsed) -or $address -in '0.0.0.0','::'){throw 'Informe somente IPs explícitos de proxies verificados.'}};$state.TrustedProxyAddresses=$TrustedProxyAddresses;SaveState}
$authFile=Join-Path $env:USERPROFILE '.discloud/.cli'
try{$encoded=[IO.File]::ReadAllText($authFile).Trim();$auth=[Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($encoded))|ConvertFrom-Json;$apiToken=$auth.token;if(!$apiToken){throw 'missing'}}catch{throw 'Autentique a CLI Discloud local antes de publicar. Não envie tokens pelo chat.'}
$headers=@{'api-token'=$apiToken}
function Call([string]$Method,[string]$Route,$Body=$null,$File=$null){
 try{
  if($File){$r=Invoke-RestMethod -Method $Method -Uri ('https://api.discloud.app/v2'+$Route) -Headers $headers -Form @{file=Get-Item -LiteralPath $File} -TimeoutSec 180}
  elseif($null -ne $Body){$r=Invoke-RestMethod -Method $Method -Uri ('https://api.discloud.app/v2'+$Route) -Headers $headers -ContentType 'application/json' -Body ($Body|ConvertTo-Json -Depth 12 -Compress) -TimeoutSec 180}
  else{$r=Invoke-RestMethod -Method $Method -Uri ('https://api.discloud.app/v2'+$Route) -Headers $headers -TimeoutSec 45}
 }catch{
  $reason='';try{$detail=$_.ErrorDetails.Message|ConvertFrom-Json;$reason=[string]$detail.message}catch{}
  foreach($value in @($apiToken,$encoded)+@($vault.Values)){if($value -is [string] -and $value.Length -gt 0){$reason=$reason.Replace($value,'[redacted]')}}
  if($reason.Length -gt 500){$reason=$reason.Substring(0,500)}
  throw (('Discloud '+$Method+' '+$Route+': HTTP ')+[int]$_.Exception.Response.StatusCode+'. '+$reason+' Confira o estado antes de repetir operações de criação.')
 }
 if($r -is [string] -and $r.TrimStart().StartsWith('{')){$r=$r|ConvertFrom-Json}
 if($r -is [string] -and $r.TrimStart().StartsWith('[')){$r=$r|ConvertFrom-Json}
 if($r.status -eq 'error'){throw ('Discloud rejeitou a operação (código '+$r.statusCode+'). Nenhum segredo será exibido no log.')};return $r
}
$account=Call GET '/user';$free=$account.user.totalRamMb-$account.user.ramUsedMb
$needed=0;if(!$state.SiteId){$needed+=512};if(!$state.DatabaseId){$needed+=512};if($free -lt $needed){throw "RAM insuficiente: necessário $needed MB livres para os serviços ainda não criados. O plano não será alterado."}
$subdomains=Call GET '/subdomain/all'
if(!($subdomains.subdomains|Where-Object {$_.id -eq $SiteName})){$null=Call POST "/subdomain/$SiteName" @{}}
$subdomain=Call GET "/subdomain/$SiteName"
if(!$subdomain.subdomain.baseDomain){throw 'A Discloud ainda não informou o domínio público. Aguarde a configuração antes de publicar.'}
$state.PublicUrl="https://$SiteName.$($subdomain.subdomain.baseDomain)";SaveState
$dotnet=Join-Path $env:LOCALAPPDATA 'NexusDev/dotnet/dotnet.exe'
$server=Join-Path $output 'package/server'
if(!$SkipBuild){& $dotnet publish (Join-Path $workspace 'src/Orbe.Server') -c Release -r linux-x64 --self-contained true -p:RuntimeFrameworkVersion=10.0.14 -p:InvariantGlobalization=true -p:DebugType=None -p:DebugSymbols=false -o $server;if($LASTEXITCODE -ne 0){throw 'Publicação Linux falhou'}}
if(!(Test-Path -LiteralPath (Join-Path $server 'Orbe.Server'))){throw 'Pacote Linux ausente.'}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'index.js') -Destination (Join-Path $output 'package/index.js')
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'package.json') -Destination (Join-Path $output 'package/package.json')
@("NAME=Orbe Testes","TYPE=site","ID=$SiteName","MAIN=index.js","RAM=512","VERSION=latest","AUTORESTART=true","VLAN=true")|Set-Content (Join-Path $output 'package/discloud.config') -Encoding ascii
$revision=(& git -C $workspace rev-parse --short HEAD).Trim()
function Package([bool]$DatabaseReady){
 $secret=@{PublicUrl=$state.PublicUrl;Deployment=@{Revision=$revision};DataProtection=@{Certificate=$vault.Certificate;Password=$vault.CertificatePassword};Database=@{SkipInitialize=(!$DatabaseReady)};Bootstrap=@{AllowUnconfiguredOwner=([string]::IsNullOrWhiteSpace($vault.OwnerEmail))};Owner=@{Email=$vault.OwnerEmail;Password=$vault.OwnerPassword}}
 if($state.TrustedProxyAddresses){$secret.Proxy=@{Addresses=@($state.TrustedProxyAddresses)}}
 if($ProbeProxy){$secret.Logging=@{LogLevel=@{'Microsoft.AspNetCore.HttpOverrides.ForwardedHeadersMiddleware'='Debug'}}}
 if($DatabaseReady){$secret.ConnectionStrings=@{Orbe="Host=$($state.DatabaseHost);Port=5432;Database=orbe_test;Username=orbe;Password=$($vault.DbPassword);Maximum Pool Size=10;Timeout=15;Command Timeout=15"}}
 $secret|ConvertTo-Json -Depth 12|Set-Content -LiteralPath (Join-Path $server 'orbe.secrets.json') -Encoding utf8
 $zip=Join-Path $output 'orbe-test-private.zip'
 if(Test-Path -LiteralPath $zip){Remove-Item -LiteralPath $zip}
 [IO.Compression.ZipFile]::CreateFromDirectory((Join-Path $output 'package'),$zip,[IO.Compression.CompressionLevel]::Optimal,$false)
 return $zip
}
if(!$state.SiteId){
 $zip=Package $false;$created=Call POST '/upload' -File $zip
 if(!$created.app.id){throw 'Upload sem ID confirmado. Consulte suas aplicações antes de repetir.'}
 $state.SiteId=$created.app.id;SaveState;Write-Output "Site de teste criado: $($state.PublicUrl) (512 MB). Reservas fechadas."
}
if(!$state.DatabaseId){
 $existing=Call GET '/database';$items=if($existing -is [array]){$existing}else{$existing.data};$matches=@($items|Where-Object {$_.displayName -eq 'Orbe PostgreSQL Testes'})
 if($matches.Count -gt 1){throw 'Há mais de um banco Orbe de testes; selecione manualmente antes de continuar.'}
 if($matches.Count -eq 1){$state.DatabaseId=$matches[0]._id;SaveState}
 else{
  $presets=Call GET '/database/presets';$preset=@($presets.data.databases|Where-Object {$_.type -eq 'postgres'})[0]
  if(!$preset){throw 'Preset PostgreSQL não encontrado na conta.'}
  $created=Call POST '/database' @{displayName='Orbe PostgreSQL Testes';db=@{type='postgres';version='17';port=5432};vars=@{POSTGRES_DB='orbe_test';POSTGRES_USER='orbe';POSTGRES_PASSWORD=$vault.DbPassword};totalRam=512;autoRestart=$true}
  if(!$created.id){throw 'Criação sem ID de banco confirmado. Consulte o dashboard antes de repetir.'}
  $state.DatabaseId=$created.id;SaveState;Write-Output 'PostgreSQL de testes solicitado pelo preset da Discloud (512 MB).'
 }
}
$ready=$false
for($attempt=0;$attempt -lt 24;$attempt++){
 try{$database=Call GET "/database/$($state.DatabaseId)"}catch{if($_.Exception.Message -match 'HTTP 404'){Start-Sleep -Seconds 5;continue};throw}
 if($database.database.online -and $database.database.exists){$ready=$true;$state.DatabaseHost=$database.database.hostname;if($database.database.vars.POSTGRES_PASSWORD){$vault.DbPassword=$database.database.vars.POSTGRES_PASSWORD;SaveVault};break}
 Start-Sleep -Seconds 5
}
if(!$ready -or !$state.DatabaseHost){throw 'Banco ainda em preparação. O ID foi salvo; retome sem criar outro banco.'}
$state.DatabaseReady=$true;SaveState
$zip=Package $true;$null=Call PUT "/app/$($state.SiteId)/commit" -File $zip
Write-Output "Código e conexão enviados para $($state.PublicUrl). Confira health/status antes de considerar a implantação concluída."
Remove-Variable apiToken,headers,auth,encoded,secret,vault -ErrorAction SilentlyContinue
