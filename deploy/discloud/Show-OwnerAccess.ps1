# Run locally when you want to see your first access. Nothing is printed to the console.
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Windows.Forms
$file=Join-Path $env:LOCALAPPDATA 'OrbeDev/cloud-test.dpapi'
try{
 $secure=ConvertTo-SecureString ([IO.File]::ReadAllText($file).Trim())
 $vault=[Net.NetworkCredential]::new('',$secure).Password|ConvertFrom-Json
 if(!$vault.OwnerEmail){throw 'Conta administrativa ainda não configurada.'}
 $form=[Windows.Forms.Form]::new();$form.Text='Orbe — primeiro acesso';$form.ClientSize=[Drawing.Size]::new(540,245);$form.StartPosition='CenterScreen';$form.FormBorderStyle='FixedDialog';$form.MaximizeBox=$false
 $label=[Windows.Forms.Label]::new();$label.Text="Usuário: WillianBN`r`nSenha inicial protegida neste computador. Troque-a em Minha conta após entrar.";$label.SetBounds(20,18,500,48)
 $password=[Windows.Forms.TextBox]::new();$password.Text=$vault.OwnerPassword;$password.UseSystemPasswordChar=$true;$password.ReadOnly=$true;$password.SetBounds(20,83,500,28)
 $show=[Windows.Forms.CheckBox]::new();$show.Text='Mostrar senha';$show.SetBounds(20,120,160,26);$show.add_CheckedChanged({$password.UseSystemPasswordChar=!$show.Checked})
 $copy=[Windows.Forms.Button]::new();$copy.Text='Copiar senha';$copy.SetBounds(350,119,170,30);$copy.add_Click({[Windows.Forms.Clipboard]::SetText($password.Text);$copy.Text='Copiada'})
 $note=[Windows.Forms.Label]::new();$note.Text='A senha inicial deixa de funcionar após você trocá-la. Este assistente não redefine sua senha.';$note.SetBounds(20,167,500,44)
 $form.Controls.AddRange(@($label,$password,$show,$copy,$note));$null=$form.ShowDialog();$password.Clear();$form.Dispose()
}finally{Remove-Variable vault,secure -ErrorAction SilentlyContinue}
