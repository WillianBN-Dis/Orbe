# Orbe 2.0.0 — ajuste 1 Android

Validado em 26/10/2026. A versão exibida permanece **2.0.0**; Android `versionCode=20001`. A versão original 20000 e as releases anteriores permanecem preservadas. Código do APK: `e55e9be`; feed publicado em `f228fb8`.

- APK assinado ARM64/x86_64: `Orbe-2.0.0-ajuste.1.apk`, 64.909.845 bytes.
- SHA-256: `cf417d62c92b71900fc9ffa667c0f3c7dd0fe8a45e77edeb897ce07a74aa6540`.
- Certificado SHA-256: `bbb5effe20b368e404843727b278546002b7f0a182b5a007996e47092669b365`, preservado da linha anterior.
- Release: https://github.com/WillianBN-Dis/Orbe/releases/tag/v2.0.0-ajuste.1.

## Verificações concluídas

- 11 verificações automatizadas do atualizador com HTTP e APK sintéticos: metadados inválidos, origem/HTTPS, hash/build, download verificado, cache, corrupção, redirecionamentos e cancelamento. `dotnet run --project tests/Orbe.Update.Checks`.
- Build Windows Release sem erros/avisos; nenhuma republicação do instalador Windows necessária para este ajuste Android.
- APK instalado por cima da versão anterior no emulador próprio Orbe_QA_API36, Android 16 x86_64: duas aberturas com login, retorno de segundo plano sem crash. Manifesto e binários ARM64/x86_64 conferidos; sem teste em celular físico nesta entrega.
- Tela de atualização inspecionada visualmente. Entrada disponível sem login e no menu Conta da equipe. APK final consultou o feed real e informou que já estava atualizado.
- Fluxo completo validado com bootstrap exclusivo de QA `versionCode=20000`, contendo o atualizador: consulta ao feed público, novo ajuste com a mesma versão exibida, download real do asset GitHub com progresso, SHA-256, validação do pacote/build, diálogo de autorização, configuração nativa de instalação pelo Orbe, retorno e **Continuar instalação**.
- Instalador Android abriu pelo FileProvider e exibiu **Do you want to update this app?**. Após confirmar, mostrou **App installed**. `dumpsys package` passou de 20000 para 20001, manteve `versionName=2.0.0` e o mesmo `firstInstallTime`, comprovando atualização sobre a instalação existente.
- APK final reabriu, reconheceu que já estava atualizado e voltou ao login pelo botão Voltar. Log de crash vazio.
- Hash do asset publicado conferido com o APK local e o manifesto de atualização.

Artefatos e screenshots privados de QA em `artifacts/qa/android-updater`; builds em `artifacts/cloud-dev`. Apenas emulador próprio e dados sintéticos. Não houve acesso a dados pessoais, alterações no Nexus ou nova implantação Discloud. Cadastro público/SMTP/reservas continuam fechados conforme configuração anterior.

## Uso

Instale este primeiro ajuste manualmente pelo GitHub. Depois use **Atualizar aplicativo** no login ou em **Conta**, verifique e instale quando houver ajuste novo. Autorize a fonte Orbe quando solicitado e confirme no Android. Não desinstale o app para atualizar.
