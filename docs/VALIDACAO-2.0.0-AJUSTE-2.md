# Orbe 2.0.0 — ajuste 2, 27/10/2026

Os ajustes de interface que estavam somente no setup Windows local agora também estão no APK Android e nos downloads principais do GitHub. A versão exibida permanece **2.0.0**; Android usa código interno **20002**, necessário para atualizar os builds 20000/20001 sem desinstalar.

## Mudanças

- Configurar servidor retirado do login nas duas plataformas. Configurar conexão fica em Conta, somente para o administrador autenticado, com nova conferência do papel Owner antes de salvar a preferência do dispositivo.
- Área principal limitada a 1440 px e ações com largura natural limitada a 320 px. Navegação, calendário e formulário de entrada mantêm preenchimento apropriado.
- Botão Atualizar aplicativo preservado no Android; feed passa a indicar o ajuste 2.
- Fonte inclui o modo de testes sem confirmação de e-mail já autorizado e implantado anteriormente. E-mails permanecem não verificados; apenas Client pode usar essa exceção. Desativar a configuração bloqueia também suas sessões. Recuperação por e-mail continua indisponível.
- Downloads principais identificados na release 2.0.0; arquivos antigos mantidos com identificação de histórico. A tag original e as releases anteriores são preservadas.

## Pacotes

| Arquivo | Bytes | SHA-256 |
| --- | ---: | --- |
| Orbe-Setup.exe | 76655004 | 51fc7876d772636a900d8e798c62341f6d3110c647af1cccc120ad4e1f95e168 |
| Orbe-2.0.0.apk / Orbe-2.0.0-ajuste.2.apk | 64913941 | 850c9c82770ec9188f0166903e9ac8b7dace8c3ecbfc3942b40371fe6e9d43c5 |

Android ARM64/x86_64 assinado com o mesmo certificado: SHA-256 `bbb5effe20b368e404843727b278546002b7f0a182b5a007996e47092669b365`. APK com assemblies embutidos; Windows autocontido, sem assinatura Authenticode.

## Evidências

- APK final atualizado sobre build anterior no emulador Orbe_QA_API36/Android 16 x86_64: duas aberturas, login obrigatório e retorno do segundo plano sem crash.
- Manifesto confirma package `br.com.agendafinanceira.app`, versionName 2.0.0, versionCode 20002 e ABIs ARM64/x86_64. Assinatura verificada por apksigner.
- Árvore de acessibilidade e screenshot do APK final confirmam ausência de Configurar servidor/Servidor Orbe no login; Atualizar aplicativo continua disponível.
- Payload Windows usado no instalador final abriu/renderizou o login 2.0.0 com screenshot própria e sem erro; Configurar servidor ausente. O mesmo payload passou instalação/desinstalação isoladas e QA responsiva em 26/10; o empacotamento atual atualiza o guia sem alterar seus binários.
- Regressões de servidor já concluídas sobre a mesma implementação: 60 verificações HTTP/PostgreSQL; UI em 3440×1400, 1280×850 e 430×900; reserva e cancelamento no site desktop/celular. Não foram repetidas sem mudança de implementação.
- Artefatos privados de QA em artifacts/cloud-dev e artifacts/qa. Nenhuma credencial ou dado pessoal acompanha a publicação.

Downloads: [Windows](https://github.com/WillianBN-Dis/Orbe/releases/download/v2.0.0/Orbe-Setup.exe) · [Android](https://github.com/WillianBN-Dis/Orbe/releases/download/v2.0.0/Orbe-2.0.0.apk). Instale por cima do app existente. O site continua explicitamente em modo de testes.
