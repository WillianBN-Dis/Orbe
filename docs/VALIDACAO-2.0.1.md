# Orbe 2.0.1 — validação

Produto 2.0.1, compilação 20010. Pacotes finais verificados em 31/10/2026.

- API/PostgreSQL: 70 verificações existentes e 11 regressões de serviços por profissional, incluindo isolamento, concorrência, catálogo legado, histórico preservado e administração de profissional desativado.
- Site: seis cenários Client/Staff/Owner em 390 e 1440 px, API simulada, profissional antes do serviço, jornadas e notificações.
- Windows: formulários sintéticos em 430/1440 px, minutos legíveis após empilhamento dos campos; pacote autocontido instalado, aberto e desinstalado em diretório de QA. Tempo de espera ampliado para 180 s no host de build.
- Android 16 x86_64: APK instalado como atualização, duas aberturas, retorno do segundo plano, login obrigatório, ausência do atalho de fixture Debug no Release e nenhum crash do Orbe. Formulários sintéticos de jornada, bloqueio e serviços também passaram em pacote Debug separado, removido ao final. Capturas inspecionadas.
- O emulador inicialmente apresentou ANRs do Pixel Launcher/System UI; após reinicialização e encerramento dos diálogos do sistema, a execução final passou. Isso não substitui teste em aparelhos físicos.
- APK inclui arm64-v8a e x86_64 e mantém a chave original. Windows sem assinatura Authenticode.

## SHA-256 dos instaladores

Windows `Orbe-Setup.exe`: `511f6e402317f058404a1d47012663df7a72e22088bdf986cef80ea3f60043d5`.

Android `Orbe-2.0.1.apk`: `e63b106dcf1b9cdcc6ad64c06ccd4d52b428cd2ec3728904fe88dec8c5979db5`.

Notificações são somente layout. Confirmação e recuperação por e-mail continuam desativadas no ambiente de testes por decisão do proprietário. Nenhum banco Nexus pessoal foi utilizado nos testes.
