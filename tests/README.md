# Verificações da versão 2

- `dotnet run --project tests/Orbe.Cloud.Checks`: política de disponibilidade, janelas divididas, bloqueios e limites.
- `dotnet run --project tests/Orbe.Update.Checks`: atualização com a mesma versão exibida, origem/HTTPS/build/hash inválidos, download íntegro, cache verificado, descarte de pacote corrompido, redirecionamentos e cancelamento; somente dados sintéticos e diretório temporário.
- `dotnet run --project tests/Orbe.Import.Checks`: SQLite temporário, snapshot/WAL, IDs, centavos, vínculos e proteção de catálogos.
- `dotnet run --project tests/Orbe.Server.Checks`: autenticação, isolamento, concorrência e transações com PostgreSQL real. Exige `ORBE_TEST_DATABASE`, host127.0.0.1/porta55439/databaseorbe_cloud_test. **Recria o schema desse banco exclusivo de testes.** Não aceita outros endereços/nomes.
- `node tests/backup-checks.cjs`: cifragem streaming, autenticação, adulterações, truncamento, tamanho e retenção, só arquivos temporários sintéticos.
- `node tests/web-finance-check.cjs`: Chromium/MS Edge headless e Playwright; servidor de testes exclusivo em127.0.0.1:5188 com perfilowner@example.test e senha sintética gerada pelos testesHTTP. Valida soma após200movimentos, repetição financeira após falha de rede e remoção de campos de senha ao fechar o diálogo. `ORBE_PLAYWRIGHT_MODULE` pode indicar o caminho local do módulo Playwright. Não opera no site real.
- `tests/windows-smoke.ps1 -Payload <pasta-em-artifacts>`: compila instalador próprio deQA, sem registro/atalhos e sem fechar apps pessoais; instala em pasta exclusiva, verifica runtime autocontido, versão, janela e renderização do login, depois desinstala só essa cópia.
- `tests/android-smoke.ps1 -Apk <apk> -Version 2.0.0`: somente emuladorOrbe_QA_*, duas aberturas do login e retorno segundo plano, sem crash. Use emulador limpo/sem sessão autenticada.

A validação de conta/jornada/equipe/importação Android usa fixtures do banco de testes e servidorloopback via `adb reverse tcp:5188 tcp:5188`. Não usa contas reais ou dadosNexus pessoais. Artefatos deQA ficam ignorados em `artifacts/cloud-dev` e `artifacts/qa`.

A candidata anterior revelou que o botão Voltar doAndroid16 saía do app quando um formulário estava aberto. O projeto agora fixaMAUI10.0.110; o reteste confirmou o retorno correto. Referência do problema na base anterior: https://github.com/dotnet/maui/issues/33480. O teste deve incluir fechar primeiro qualquer seletor nativo antes de enviar Voltar para a página, para não confundir fechamento do seletor com navegação.

- `dotnet run --project tests/Orbe.Connection.Checks`: 15 verificações de descoberta central, HTTPS/origem, tamanho, identidade, cancelamento e ausência de token na descoberta. HTTP/PostgreSQL também verifica autorização Owner, concorrência e rejeição de outro banco na troca global.
- Ajuste 3: QA Debug nativo e Playwright com API/PostgreSQL locais confirmaram cancelamento externo refletido automaticamente (15s), sem reiniciar/recarregar. Capturas desktop 3440/1280 e mobile 430/390 ficam nos artifacts privados. Fixtures Debug não entram no Release.
