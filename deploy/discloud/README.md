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

## Backups diários

O launcher executa `pg_dump` todos os dias a partir de **06:00 UTC (03:00 em São Paulo)**, mesmo com o computador desligado. Após reinício, recupera o backup do dia se necessário; uma falha tenta novamente em uma hora. Mantém sete dias na pasta privada `.orbe-backups`, fora de `wwwroot`. A persistência dessa pasta entre publicações foi verificada na Discloud. Não há token da API Discloud hospedado no app.

O cliente PostgreSQL para Debian 12 vem do [repositório oficial PGDG](https://www.postgresql.org/download/linux/debian/), com versões e SHA-256 fixados em `pg-tools.lock.json`; não instala pacotes de sistema. O dump em formato customizado é cifrado enquanto é produzido, sem gravar uma cópia em claro no servidor. Formato `ORBEBAK1`: chave AES-256 aleatória envolvida com RSA-OAEP/SHA-256, nonce de 12 bytes, conteúdo AES-256-GCM e tag de 16 bytes; cabeçalho autenticado como AAD. Arquivos só ficam disponíveis após o processo e a cifragem terminarem. Limites: cinco minutos por tentativa e 1 GB de dump.

Uma cópia real foi autenticada, decifrada e restaurada em PostgreSQL isolado; os testes também recusam adulterações/truncamento e verificam a retenção. Isso protege contra perda do banco separado, mas não substitui cópias privadas fora da conta Discloud. Já existe uma cópia cifrada no PC. Guarde também o vault DPAPI/certificado de forma privada e recuperável: perder a chave impede decifrar os backups. O vault atual só pode ser aberto pelo mesmo usuário Windows neste computador.

Para decifrar uma cópia baixada neste PC:

```powershell
./deploy/discloud/Decrypt-Backup.ps1 -EncryptedFile 'caminho/arquivo.orbebak' -Destination 'caminho/novo.dump'
```

Depois use `pg_restore --no-owner --no-acl --exit-on-error` **somente em banco novo e isolado**. O script nunca restaura automaticamente sobre banco em uso. `Backup-Test.ps1` é um segundo mecanismo manual do provedor: seu gzip contém dump de cluster/roles e exige uma restauração diferente, em cluster novo; não confundir os dois formatos.

## Primeiro acesso

O usuário administrativo é `WillianBN`. Após configurar o e-mail real, execute localmente `./deploy/discloud/Show-OwnerAccess.ps1`: a senha inicial é exibida em janela mascarada e pode ser copiada por ação explícita. Não é impressa no terminal, enviada ao GitHub nem adicionada às notas. Troque a senha em Minha conta após entrar. O assistente não redefine uma senha já trocada.

**Pendências:** SMTP (adiado pelo usuário; cadastro e recuperação de clientes fechados), QA final dos aplicativos e release. A versão estável 1.0.2 e as anteriores permanecem intactas no GitHub.

## Prévia sem SMTP, autorizada para testes

O parâmetro explícito `-EnableUnverifiedClientTests` do Publish-Test.ps1 define `Preview:AllowUnverifiedClients=true` no arquivo privado do servidor. O padrão permanece false. Nesse modo, o site exibe aviso, permite cadastro/login de Client sem envio de e-mail e mantém EmailConfirmed=false. Equipe e Owner continuam sujeitos à regra normal de confirmação. Desativar a configuração invalida também o acesso das sessões não verificadas.

Recuperação de senha por e-mail continua indisponível. Antes do uso definitivo, feche as reservas, desative a prévia, configure/teste SMTP e verifique as contas. A abertura de reservas continua exigindo serviço, profissional ativo, jornada e ausência de histórico sem atribuição.

## Endereço curto — 29/10/2026
Servidor principal: https://orbe.discloud.dev (512 MB), mesmo PostgreSQL de 512 MB. O app orbe-willianbn agora executa somente deploy/discloud/legacy/index.js (512 MB) para compatibilidade: descoberta e API são encaminhadas ao destino HTTPS fixo; navegação web redireciona. Não possui worker de banco ou backup. Não remova esse acesso enquanto existirem aplicativos instalados com bootstrap antigo. A RAM total do plano não foi alterada; compatibilidade usa 512 MB adicionais do saldo existente.
O bootstrap estável do código nativo continua no endereço antigo deliberadamente, mas retorna apiUrl novo; aplicativos ajuste3 descobrem a mudança e solicitam novo login. Não precisam reinstalar. Publicações normais usam SiteName=orbe; o estado privado registra LegacySiteId separadamente. Backup diário permanece somente no servidor principal. Uma migração futura de bootstrap exige novo APK/código interno, preservando a origem antiga durante transição.

## Recuperação de acesso — 29/10/2026

A promoção do endereço curto foi suspensa: o DNS do provedor usado pelo Wi-Fi do proprietário(138.117.233.254) retorna NXDOMAIN para A de orbe.discloud.dev, apesar de responder AAAA e de1.1.1.1/8.8.8.8 já responderem A válido. Servidor novo e HTTPS funcionam com resolução explícita. Não tratar como troca concluída para esse usuário.

O gateway foi corrigido para servir site/API pelo endereço antigo sem redirecionamento e anunciar o endereço antigo na descoberta enquanto o DNS novo estiver pendente. A transformação da descoberta pedeAccept-Encodingidentity ao upstream para evitar tentar interpretar JSON comprimido. O backend e banco continuam os mesmos, backup diário somente no principalorbe. HTTP200 da raiz/descoberta e navegador móvel real carregando reserva sem redirecionar aprovados. Dados/contas preservados. Links principais voltaram ao endereço estável.

Antes de promover novamente: resolver A usando DNS padrão da rede do usuário e testar navegador/app normalmente, sem override de resolução. Não trocar DNS do PC/roteador, hosts ou Firewall. Se persistir no provedor, manter acesso antigo e explicar o bloqueio; não forçar promoção. Evidências privadas emartifacts/domain-move, recovered-browser.cjs/recovered-mobile.png.

## Conclusão após propagação — 29/10/2026, depois de 16h34

O DNS padrão deste PC e o DNS do provedor(138.117.233.254) passaram a responder os registros A de orbe.discloud.dev. Acesso HTTPS/health validado sem override de resolução. Navegador Edge headless carregou normalmente a reserva em390px e1440px pelo endereço novo; após a promoção, também carregou pelo endereço antigo com redirecionamento até o endereço curto. Descoberta antiga retorna o endereço novo com a mesma InstanceId. Login/token/me de cliente sintético e encerramento de sessão aprovados nos dois acessos, preservando a mesma conta e banco.

O gateway voltou a encaminhar a descoberta sem reescrita e redirecionar navegação web307 para o endereço novo. Ele continua servindo API aos clientes antigos, sem acesso direto ao banco/backups. Recursos, plano, dados e contas inalterados nesta conclusão. Nenhum DNS/hosts/Firewall do PC/roteador foi alterado. Aplicativos ajuste3 recebem a nova origem pela descoberta; podem solicitar login novamente, sem necessidade de reinstalar. As seções de recuperação anteriores registram o histórico; o endereço recomendado agora é https://orbe.discloud.dev.

Evidências privadas: artifacts/domain-move/normal-dns-check.cjs, final-redirect-check.cjs e capturas normal-dns-390/1440.png. Teste legadoNode passou. Automação de continuação encerrada após concluir a validação e documentação.