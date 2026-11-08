# Collect credentials locally. Never pass the app password as a command argument.
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Windows.Forms
$vaultFolder=Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'OrbeDev'
New-Item -ItemType Directory -Path $vaultFolder -Force | Out-Null
$vaultFile=Join-Path $vaultFolder 'smtp.dpapi'
$original=if(Test-Path -LiteralPath $vaultFile){[IO.File]::ReadAllText($vaultFile)}else{$null}
$form=New-Object Windows.Forms.Form
$form.Text='Orbe — configurar envio pelo Gmail'
$form.ClientSize=New-Object Drawing.Size(510,270)
$form.StartPosition='CenterScreen'
$form.FormBorderStyle='FixedDialog'
$form.MaximizeBox=$false
$emailLabel=New-Object Windows.Forms.Label
$emailLabel.Text='E-mail Gmail usado como remetente'
$emailLabel.SetBounds(20,20,460,22)
$emailBox=New-Object Windows.Forms.TextBox
$emailBox.SetBounds(20,47,465,27)
$passwordLabel=New-Object Windows.Forms.Label
$passwordLabel.Text='Senha de app gerada pelo Google'
$passwordLabel.SetBounds(20,87,460,22)
$passwordBox=New-Object Windows.Forms.TextBox
$passwordBox.UseSystemPasswordChar=$true
$passwordBox.SetBounds(20,114,465,27)
$hint=New-Object Windows.Forms.Label
$hint.Text='A configuração será cifrada no cofre local do Orbe. Depois será necessário testar o envio e atualizar o servidor.'
$hint.SetBounds(20,158,465,45)
$save=New-Object Windows.Forms.Button
$save.Text='Salvar no cofre local'
$save.SetBounds(275,215,210,34)
$save.Add_Click({
 if($emailBox.Text.Trim() -notmatch '^[^\s@]+@gmail\.com$' -or ($passwordBox.Text -replace '\s','') -notmatch '^[a-zA-Z0-9]{16}$'){
  [Windows.Forms.MessageBox]::Show('Informe o Gmail e a senha de app de 16 caracteres.','Confira os campos') | Out-Null
  return
 }
 $form.DialogResult=[Windows.Forms.DialogResult]::OK
})
$form.Controls.AddRange(@($emailLabel,$emailBox,$passwordLabel,$passwordBox,$hint,$save))
$temporary=$null
try{
 if($form.ShowDialog() -ne [Windows.Forms.DialogResult]::OK){return}
 $current=if(Test-Path -LiteralPath $vaultFile){[IO.File]::ReadAllText($vaultFile)}else{$null}
 if($current -ne $original){throw 'O cofre foi atualizado por outra operação. Abra novamente este assistente.'}
 $address=$emailBox.Text.Trim()
 $settings=@{Host='smtp.gmail.com';Port=587;From=$address;User=$address;Password=($passwordBox.Text -replace '\s','')}
 $encrypted=$settings | ConvertTo-Json -Depth 12 -Compress | ConvertTo-SecureString -AsPlainText -Force | ConvertFrom-SecureString
 $temporary=Join-Path (Split-Path $vaultFile) ([guid]::NewGuid().ToString('N')+'.tmp')
 [IO.File]::WriteAllText($temporary,$encrypted,[Text.Encoding]::ASCII)
 if($null -eq $original){[IO.File]::Move($temporary,$vaultFile)}else{[IO.File]::Replace($temporary,$vaultFile,[NullString]::Value)}
 [Windows.Forms.MessageBox]::Show('Configuração salva no cofre local. Avise para continuar o teste de envio e a configuração do servidor.','Orbe') | Out-Null
}finally{
 $passwordBox.Clear()
 $form.Dispose()
 if($temporary -and (Test-Path -LiteralPath $temporary)){Remove-Item -LiteralPath $temporary}
 Remove-Variable vault,secure,settings,encrypted,original -ErrorAction SilentlyContinue
}
