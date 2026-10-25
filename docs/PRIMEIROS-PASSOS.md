# Primeiros passos — Orbe 1.0

1. Execute Orbe-Setup.exe. O instalador contém todas as dependências para Windows x64. O atalho na área de trabalho é opcional.
2. Abra Orbe. Use Explorar demonstração para conhecer a interface com dados fictícios.
3. Para dados reais, instale/abra o Nexus: https://github.com/WillianBN-Dis/Nexus/releases/latest
4. Crie no Nexus um banco dedicado e execute separadamente cada CREATE TABLE do arquivo 001-inicial.sql que acompanha o Orbe.
5. Ative a API Nexus e gere uma chave de leitura/escrita somente para esse banco.
6. Em Conexão no Orbe, informe http://127.0.0.1:5080 no mesmo computador, consulte bancos e selecione o banco preparado.

Android: use o IPv4 privado do computador Nexus e mantenha os dispositivos na mesma rede. O Nexus deve estar em execução. Não há sincronização offline ou acesso pela internet.

Agendar não gera recebimento. O lucro considera somente receitas menos despesas registradas. Excluir um evento também exclui os lançamentos vinculados a ele. Para vincular, use Lançar recebimento na agenda ou selecione Agendamento vinculado no formulário financeiro. Lançamentos sem vínculo são mantidos. Não compartilhe sua chave.

O instalador/desinstalador gerencia somente os arquivos do programa. Não apaga bancos Nexus, catálogo, credenciais ou preferências do aplicativo. A atualização é manual, executando o instalador mais recente.

O instalador Windows não possui assinatura Authenticode. O APK Android é assinado para distribuição direta, sem publicação na Play Store. Versões locais anteriores de Android usavam assinatura de desenvolvimento: pode ser necessário remover a prévia antes de instalar esta primeira versão estável.
