# Atualizações Android fora da Play Store

O botão **Atualizar aplicativo** fica no login e no menu Conta. Não exige sessão: pode corrigir problemas de acesso ao próprio app. A verificação é manual, pelo GitHub público oficial; não usa tokens, credenciais do usuário ou servidor Discloud.

O ajuste 1 mantém `versionName=2.0.0` e incrementa `versionCode` para `20001`. Quem possui o APK anterior precisa instalar este primeiro ajuste manualmente uma vez. O instalador do Android exige confirmação e, desde Android 8, autorização por fonte. O app orienta a habilitar somente o Orbe e retomar a instalação.

O **ajuste 2**, código **20002**, inclui os ajustes responsivos e a retirada de Configurar servidor do login. O acesso à conexão fica restrito ao administrador após autenticar. A versão exibida permanece 2.0.0. Os downloads principais da release 2.0.0 acompanham o ajuste recomendado; pacotes anteriores ficam identificados como histórico.

## Publicar o próximo ajuste

1. Mantenha a chave de assinatura privada original. Incremente o código interno Android; nunca reutilize códigos de APKs entregues. A versão exibida pode permanecer igual para ajustes da mesma versão.
2. Atualize o código padrão em `Agenda.App.csproj` e `release-android.ps1`. Compile com `./release-android.ps1 -Version 2.0.0 -VersionCode 20003 -OutputName Orbe-2.0.0-ajuste.3.apk`, usando números e nomes do novo ajuste.
3. Teste instalação sobre o APK anterior e o fluxo do botão. Gere SHA-256 do APK final assinado.
4. Publique o APK com um nome próprio do ajuste no repositório `WillianBN-Dis/Orbe`. Se atualizar os downloads principais da release 2.0.0, preserve o arquivo anterior com nome de histórico antes de substituí-lo. Releases anteriores devem permanecer disponíveis.
5. Atualize `updates/android.json` na branch `codex/orbe-cloud`: `version`, `build`, URL pública do asset, `sha256` e notas curtas em português. Publique o feed depois que o asset estiver disponível. Ele indica o ajuste Android recomendado, incluindo a linha de testes 2.0.
6. Verifique o feed público e reteste o download, integridade e instalação. Registre resultado, hash e links no Obsidian.

O download exige HTTPS, origem oficial e redirecionamentos somente dentro do GitHub. O manifesto tem limite de 16 KiB e o APK de 150 MiB. Downloads incompletos são removidos; o pacote só fica disponível ao instalador depois da conferência SHA-256. O app também confere o identificador do pacote e o código interno anunciado. O próprio Android verifica a assinatura para aceitar a atualização e preservar dados.

O cache é privado. O FileProvider compartilha temporariamente apenas arquivos da subpasta `updates/`; não expõe bancos, tokens ou outros arquivos. O cancelamento encerra a transferência quando a página é fechada. Não há instalação silenciosa ou alteração das configurações de segurança sem ação do usuário.

Ajuste 3 (20003): conexão central exclusiva do proprietário, atualização automática da agenda, menu móvel organizado, logo menor e ícone com fundo navy. Continua versionName 2.0.0 e preserva a assinatura anterior.
