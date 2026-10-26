# Discloud — ambiente de testes do Orbe

Site ASP.NET Core self-contained Linux x64, iniciado pelo launcher Node sem dependências. Limite do site: **512 MB**; heap .NET limitado a 256 MB. PostgreSQL 17 provisionado separadamente pelo modelo gerenciado da Discloud, com **512 MB**.

`Publish-Test.ps1` usa a autenticação já existente da CLI Discloud no computador e grava o estado dos recursos em `artifacts/discloud-test/state.json`. Reexecuções reutilizam esses recursos. Não modifica as outras aplicações da conta nem contrata plano.

```powershell
./deploy/discloud/Publish-Test.ps1
# Apenas após definir o e-mail real do proprietário:
./deploy/discloud/Publish-Test.ps1 -OwnerEmail 'seu-email@exemplo.com'
```

Os segredos gerados ficam protegidos por DPAPI em `%LOCALAPPDATA%/OrbeDev/cloud-test.dpapi`. O pacote privado de upload inclui configuração de execução fora de `wwwroot`: nunca compartilhar esse ZIP, nem adicioná-lo ao Git ou a uma release. Faça backup privado do certificado de proteção das chaves e das credenciais antes de substituir o computador. Não imprima o arquivo de autenticação da CLI: seu conteúdo codificado não é anonimizado.

Sem e-mail administrativo, o preview inicia fechado, sem conta administrativa e sem endpoint público para criar proprietário. SMTP real continua necessário para confirmação de cadastro e recuperação de senha. Não habilitar reservas até concluir importação e atribuição dos dados antigos, catálogo, equipe, jornadas e validação operacional.

**Pendências:** automação de backup diário com restauração testada, configuração de e-mail, QA final dos aplicativos e release. Este ambiente não é a entrega final. A versão estável 1.0.2 e as anteriores permanecem intactas no GitHub.
