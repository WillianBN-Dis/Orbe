# Endereço curto do Orbe — 29/10/2026

Endereço principal: https://orbe.discloud.dev . O nome foi reservado na mesma conta e a Discloud atribuiu o domínio discloud.dev. Não foi comprado domínio ou alterado o plano.

O serviço principal orbe usa512MB e o PostgreSQL original permanece o mesmo, com512MB. O serviço anterior orbe-willianbn agora executa apenas o gateway Node de compatibilidade,512MB: navegação web recebe307 para o endereço novo; API e descoberta são encaminhadas ao destino HTTPS fixo. Não executa servidor .NET, conexão de banco ou worker de backups. Essa compatibilidade usa512MB adicionais do saldo do plano: uso total da conta3072/4096MB após a transição. Outros aplicativos não foram alterados.

O bootstrap dos APKs/Windows já instalados continua acessível e retorna o endereço novo. Ajuste3 descobre a mudança e pode solicitar novo login; não requer reinstalação. Versões antigas continuam acessando a API através do gateway. Não remover o gateway enquanto houver clientes usando o bootstrap antigo.

Backup privado do banco criado antes da transição. HTTPS e health ready conferidos no endereço novo, mesma InstanceId do banco original e descoberta antiga retornandoapiUrlnovo. Login/token/me/logout de cliente sintético pelo gateway aprovados. Teste Node confirma redirects e encaminhamento, sem credenciais reais. Backups diários habilitados somente no serviço principal; certificado de proteção e banco preservados.

DNS público validado nos resolvedores1.1.1.1,8.8.8.8 eGoogleDoH. O resolvedor padrão deste PC ainda retornava nome desconhecido durante a publicação, após uma consulta inicial feita antes da criação. Conferências HTTPS usaram resolução explícita dos IPs públicos, mantendo validação normal do certificado. Propagação/cache local pode atrasar o acesso; nenhum servidorDNS, arquivohosts ou Firewall foi alterado. Somente cacheDNS local foi limpo.

Publicação futura: Publish-Test.ps1 usaSiteNameorbe; estado privado apontaSiteIdorbe eLegacySiteIdorbe-willianbn. Backup-Test.ps1 aceita estado de ambas as etapas. Dados e contas não foram importados, apagados ou recriados nesta operação. Chaves e pacotes privados ficam ignorados emartifacts/domain-move.
