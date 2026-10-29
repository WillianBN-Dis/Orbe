# Validação — Orbe 2.0.0 ajuste 3

Data: 29/10/2026. Implementação d60e0cd, feed 11cc5f2. Versão exibida 2.0.0; Windows 2.0.0.20003 e Android versionCode 20003. Ambiente de testes sem SMTP, conforme autorização do proprietário.

- 66 verificações HTTP/PostgreSQL e 15 de conexão central aprovadas em banco exclusivo sintético. Autorizações Owner/Staff/Client, concorrência e identidade do banco verificadas.
- Cancelamento externo com app nativo e site abertos refletiu automaticamente, sem reiniciar/recarregar. Consulta a cada 15 segundos enquanto a tela está ativa; formulários não são interrompidos.
- Capturas desktop 3440/1280 e celular 430/390; menu móvel Owner/Client, Escape/foco e ausência de sobreposição/overflow. Login Windows final em duas colunas, marca visível. Ícone navy e logo móvel menor.
- Windows autocontido: instalação isolada, abertura/renderização e desinstalação aprovadas. A instalação ultrapassou o limite inicial de 60 segundos; log confirmou sucesso e a verificação posterior passou. Limites da ferramenta de QA ampliados. Nenhuma instalação pessoal alterada.
- APK assinado com a mesma chave, arm64-v8a e x86_64. Android 16: atualização sobre instalação anterior, duas aberturas e retorno do segundo plano, sem crash. Login sem Configurar servidor. Atualizador consultou feed e informou instalado/atualizado20003.
- Backup privado antes da implantação, migração de configuração aplicada nos recursos Discloud existentes, health ready e origem/identidade públicas válidas. PUT de conexão anônimo recusado401. Contas e dados existentes não foram apagados/recriados. A configuração global só aceita destino validado no mesmo banco; não transfere dados e exige manter origem inicial disponível.
- Downloads canônicos públicos Windows/Android baixados sem autenticação e conferidos por SHA-256. Release v2.0.0 continua latest; tag original e arquivos anteriores preservados como histórico. Feed aponta APK imutável ajuste3.

| Arquivo | SHA-256 |
|---|---|
| Orbe-Setup.exe | aa6096373d10ef905cc26b5d48f5df8e57c37c7ac7699aa224635071c0a20254 |
| Orbe-2.0.0.apk / Orbe-2.0.0-ajuste.3.apk | 460e823062fd76bc1809d28535095e7dd1268c6c4732ceb54730c391fdb94c9b |

Instalador Windows sem Authenticode. Android físico do usuário não foi operado; validação nativa realizada no emulador Android16. Evidências privadas em artifacts/qa/ajuste3, sem credenciais no repositório.
