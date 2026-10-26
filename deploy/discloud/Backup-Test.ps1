# Manual provider snapshot for the single Orbe test database. No broad API token is uploaded.
$ErrorActionPreference='Stop'
$workspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$stateFile=Join-Path $workspace 'artifacts/discloud-test/state.json'
if(!(Test-Path -LiteralPath $stateFile)){throw 'Publique o ambiente de testes antes de criar o backup.'}
$state=Get-Content -LiteralPath $stateFile -Raw|ConvertFrom-Json
if($state.SiteName -ne 'orbe-willianbn' -or $state.DatabaseId -notmatch '^\d+db$'){throw 'Estado não corresponde ao ambiente de testes Orbe esperado.'}
$directory=Join-Path $workspace 'artifacts/discloud-test/backups'
New-Item -ItemType Directory -Force $directory|Out-Null
try{
 $authFile=Join-Path $env:USERPROFILE '.discloud/.cli'
 $auth=[Text.Encoding]::UTF8.GetString([Convert]::FromBase64String([IO.File]::ReadAllText($authFile).Trim()))|ConvertFrom-Json
 if(!$auth.token){throw 'missing'}
 $headers=@{'api-token'=$auth.token}
}catch{throw 'Autentique a CLI Discloud local. Não envie tokens pelo chat.'}
try{
 try{$response=Invoke-RestMethod -Method Post -Uri "https://api.discloud.app/v2/database/$($state.DatabaseId)/backup" -Headers $headers -ContentType 'application/json' -Body '{"gzip":true}' -TimeoutSec 180}
 catch{throw 'O provedor não confirmou o backup. Consulte somente o banco Orbe no dashboard antes de repetir.'}
 $metadata=$response.data
 if(!$metadata.backupId -or !$metadata.downloadUrl){throw 'Resposta de backup sem metadados esperados; consulte o dashboard antes de repetir.'}
 $url=[Uri]$metadata.downloadUrl
 if($url.Scheme -ne 'https' -or $url.UserInfo){throw 'Endereço de download inválido.'}
 $name='orbe-test-'+[DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss')
 $file=Join-Path $directory ($name+'.backup')
 try{Invoke-WebRequest -Uri $url -OutFile $file -TimeoutSec 180|Out-Null}
 catch{throw 'Snapshot criado no provedor; download privado não concluído. Não compartilhe o endereço temporário.'}
 if((Get-Item -LiteralPath $file).Length -eq 0){throw 'Arquivo de backup vazio.'}
 [ordered]@{DatabaseId=$state.DatabaseId;BackupId=[string]$metadata.backupId;CreatedUtc=[DateTime]::UtcNow.ToString('O');File=[IO.Path]::GetFileName($file);Bytes=(Get-Item -LiteralPath $file).Length;Sha256=(Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash;RestoreVerified=$false}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $directory ($name+'.json')) -Encoding utf8
 Write-Output 'Snapshot do banco Orbe criado e cópia privada salva em artifacts/discloud-test/backups. Restauração ainda precisa ser testada; isto não configura backup diário.'
}finally{Remove-Variable auth,headers,response,metadata,url -ErrorAction SilentlyContinue}
