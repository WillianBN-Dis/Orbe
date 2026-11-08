# Atualizações Android fora da Play Store

O botão **Atualizar aplicativo** fica no login e no menu Conta. Não exige sessão: pode corrigir problemas de acesso ao próprio app. A versão 2.0.3 consulta automaticamente o GitHub público oficial alguns segundos após abrir, uma vez por execução. Havendo compilação mais recente, exibe um aviso com Ver atualização e Agora não. O botão permite consultar manualmente. A consulta não usa tokens de sessão do Orbe.

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

Ajuste 4 (20004): seleção do calendário e navegação com menos reconstruções, sem fade bloqueando cliques; login do PC com painel direito amplo e página de login web responsiva em /entrar. Versão de produto2.0.0 mantida.

A partir do ajuste4, publicar cada ajuste em release própria (ex.: v2.0.0-ajuste.4) com somente Orbe-Setup.exe e Orbe-2.0.0.apk. O feed aponta esse APK em endereço permanente, sem precisar duplicá-lo com outro nome. Guias e hashes permanecem na documentação e nas notas da release.
## Numeração das releases

Para ajustes pequenos e melhorias, avance o terceiro número da versão em uma unidade. Exemplo `v2.0.2` → `v2.0.3`.

Para uma release dedicada apenas a corrigir bugs, mantenha a versão base e acrescente um identificador numérico de quatro dígitos à tag e ao título. Exemplo `v2.0.3.8249`. A versão base continua no campo `version` do feed e nos parâmetros de empacotamento. Incremente `versionCode` no APK e `build` no feed a cada publicação para permitir a atualização.

Release v2.0.2.8249, código Android 20013. APK assinado com a chave anterior e atualização testada em Android 16. Windows autocontido validado em instalação isolada. Correções de respostas públicas e cabeçalhos da API verificadas por 73 cenários HTTP/PostgreSQL. Implantação na Discloud pendente; atualizar os aplicativos não implanta a API. ZAP autenticado continua pendente devido a bloqueio da revisão automática. Links e hashes nas notas da release.

Correção v2.0.2.8250, Android 20014. Links do login e da conta abrem https://agendaorbe.com.br diretamente, sem aguardar descoberta para abrir o navegador. Instaladores Windows/Android aprovados; Android atualiza a versão anterior com a mesma assinatura. Botão de cliente testado no emulador e intent HTTPS conferido.

Correção v2.0.2.8251, Android 20015. Descoberta inicia em https://orbe.discloud.dev para remover dependência do bootstrap antigo indisponível. Mensagens específicas para falha de rede e timeout. Windows autocontido e Android 16 aprovados em QA. Descoberta real .NET e chamada de login pelo APK com identidade sintética inexistente validadas. Assinatura anterior preservada; hashes nas notas da release.

## Versão 2.0.3 — Windows e Android

Build 20016. A consulta automática tem limite de 12 segundos e falhas de rede não interrompem o acesso. A instalação é iniciada pelo usuário na página de atualização. Windows também oferece Atualizar aplicativo no login e em Conta; baixa o Setup com SHA-256 e abre o instalador. O rodapé desktop usa a versão instalada.

Cada publicação atualiza updates/android.json e updates/windows.json somente depois de disponibilizar os dois assets da release. Ambos seguem os campos version, build, url, sha256 e notes; o manifesto Windows aponta ao arquivo .exe e o Android ao .apk. A versão e a compilação devem acompanhar o projeto. O aplicativo rejeita o pacote da outra plataforma e downloads que falhem na integridade. Anexar somente Setup Windows e APK Android à release, com hashes e notas no corpo.

Validados 15 cenários sintéticos do atualizador, 25 do núcleo e smoke de Windows autocontido e Android 16. Assinatura Android mantida, instalação sobre versão anterior aprovada e layout desktop do login conferido.
