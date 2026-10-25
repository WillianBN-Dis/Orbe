# Publicação do Orbe

## Windows

`./release.ps1 -Version 1.0.0` executa os testes, publica Windows x64 com runtime e Windows App SDK, gera o ICO, compila `dist/Orbe-Setup.exe` e calcula seu SHA-256. O script busca o SDK no PATH ou na instalação privada NexusDev; Inno Setup no PATH ou na instalação local padrão. `-SkipTests` somente para reempacotar código já validado.

Antes de publicar: `./installer/test-installer.ps1 -Version 1.0.0`. O teste usa AppId próprio, pasta temporária, sem atalhos, abertura real em modo sintético e validação de reinstalação/desinstalação preservando arquivo não gerenciado.

## Android

`./release-android.ps1 -Version 1.0.0` cria/reutiliza uma chave RSA de distribuição em `%LOCALAPPDATA%/OrbeDev/signing`. A senha persistida é protegida por DPAPI do usuário do Windows; não fica no código, no APK, no Git ou em logs. As senhas são passadas por variável de ambiente, conforme a [documentação Microsoft](https://learn.microsoft.com/dotnet/maui/android/deployment/publish-cli). Faça backup seguro do keystore e da senha: atualizações devem usar a mesma chave. DPAPI não é uma cópia portável da senha entre máquinas.

O script usa SDK/JDK em `%LOCALAPPDATA%/AgendaDev`, Release, assemblies embutidos e assinatura própria. Saída: `dist/Orbe-1.0.0.apk`. Não usa a chave debug. O identificador de pacote foi preservado, mas a troca de assinatura requer remover a prévia Android local antes da instalação estável.

## Release

Atualizar versão no projeto MAUI, rótulo da interface, changelog e documentação. Usar uma versão nova, nunca sobrescrever uma versão publicada. Compilar e testar, verificar APK com apksigner, criar commit/tag e publicar no repositório `WillianBN-Dis/Orbe`.

Arquivos públicos: `Orbe-Setup.exe`, `Orbe-1.0.0.apk`, `001-inicial.sql`, `PRIMEIROS-PASSOS.md` e `SHA256SUMS.txt`. Publicar primeiro como rascunho; confirmar os arquivos e depois marcar a release como publicada/latest. Verificar o link público fixo do instalador e seu hash após a publicação.

Não publicar chaves, senhas, dados pessoais, catálogos ou bancos SQLite. `dist`, `artifacts`, `bin` e `obj` estão ignorados. O instalador não embute ou configura o Nexus e não modifica os bancos dele. A atualização do Orbe é manual; enviar código não instala novas versões automaticamente.
