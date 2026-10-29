# Primeiros passos — Orbe 2.0

1. No Windows, execute **Orbe-Setup.exe**. O instalador inclui .NET e Windows App SDK; o atalho na área de trabalho é opcional. No Android, instale **Orbe-2.0.0.apk**, assinado com a mesma chave da versão 1.0.2.
2. Abra o Orbe e entre com usuário e senha. Servidor padrão: https://orbe-willianbn.discloud.dev. O computador principal não precisa permanecer ligado; uma conexão com a internet é necessária.
3. O proprietário entra como **WillianBN**. A senha inicial foi gerada e protegida no PC; consulte o assistente local deploy/discloud/Show-OwnerAccess.ps1 no projeto. Depois de entrar, use **Conta → Editar meu cadastro → Trocar senha**. Não compartilhe a senha administrativa.
4. Em **Conta → Equipe e acessos**, crie os funcionários. Se o proprietário também atende, use **Eu também atendo**. Somente WillianBN cria/desativa acessos e define os serviços de cada profissional.
5. Cadastre os serviços e atribua-os aos profissionais. Cada funcionário configura a própria jornada, folgas e compromissos, sem aprovação. Você também pode ajustar essas configurações.

## Jornada e agenda

Em **Conta → Jornadas, folgas e compromissos**, adicione períodos por dia da semana: por exemplo, 09:00–12:00 e 13:00–18:00. Uma data especial substitui a jornada semanal; sem períodos, fecha o dia inteiro. Folgas e compromissos também podem bloquear algumas horas. Alterações que conflitam com atendimentos confirmados são recusadas.

Para criar atendimentos manualmente como proprietário, escolha antes o profissional em **Conta → Agenda em foco**. A visão geral reúne todos os registros; funcionários veem somente os próprios atendimentos e financeiro.

## Clientes e reservas online

**Ambiente atual de testes: cadastro de clientes e reservas liberados sem confirmação de e-mail, por autorização do proprietário. A recuperação por e-mail continua indisponível.** O botão **Entrar como cliente** abre o site, sem acesso à agenda administrativa. Clientes usam **e-mail e senha** após criar a própria conta; o usuário WillianBN é exclusivo da administração. A configuração de servidor foi retirada do login e fica em **Conta → Configurar conexão**, somente para WillianBN.

Quando e-mail e configuração inicial estiverem prontos, o cliente poderá criar conta, confirmar e-mail e escolher serviço → profissional → dia → horário. A confirmação é automática, sem sobreposição. O cliente vê somente o próprio cadastro e agendamentos. Cancelamento/remarcação permite até 24 horas antes, prazo ajustável pelo proprietário.

## Importar dados antigos

O Orbe 2 não se conecta diretamente ao Nexus. O Nexus continua independente, com seus bancos originais. **Nenhum histórico é importado automaticamente.**

No Windows, use **Conta → Importar e atribuir histórico**, selecione somente o SQLite dedicado ao Orbe e confira a prévia. O app gera uma cópia consistente de segurança sem alterar o original. Preserva IDs, datas, centavos e vínculos; repetir o mesmo arquivo não duplica os dados. Depois, atribua os registros aos profissionais. Registros sem atribuição ficam visíveis somente para WillianBN. Bancos de credenciais/catálogo Nexus são recusados. O Android aceita o JSON exportado pelo Orbe no Windows.

Antes do uso definitivo, confira importação/atribuição, equipe, serviços, jornadas e **Regras e reservas online**. Configure e teste a confirmação de e-mail antes de encerrar o modo de testes.

O modo atual permite experimentar o fluxo sem envio de e-mails. Para encerrar os testes, feche as reservas e desative `Preview:AllowUnverifiedClients`; configure SMTP e valide as contas antes do uso definitivo. O modo de testes não marca e-mails como confirmados.

## Financeiro e backups

Agendar não gera receita: registre recebimentos e despesas efetivos. Lucro é receita menos despesa. Cancelar preserva histórico e financeiro; **excluir** um agendamento também exclui seus lançamentos vinculados em uma transação. Lançamentos avulsos são preservados.

O servidor gera backups diários cifrados a partir de 03:00 em São Paulo e mantém sete dias. Restauração validada em banco isolado. Backups ficam fora da área pública do site; mantenha cópias privadas fora da hospedagem e preserve o certificado de recuperação. Operação documentada em deploy/discloud/README.md.

## Atualizações

Versões anteriores permanecem no GitHub. Atualize executando o novo instalador ou instalando o APK assinado por cima da versão 1.0.2. O instalador não altera bancos, credenciais ou Firewall do Nexus. O instalador Windows não possui assinatura Authenticode; o APK é distribuído diretamente, sem Play Store.

No Android, o **ajuste 1 da versão 2.0.0** inclui **Atualizar aplicativo**, disponível na tela de entrada e em **Conta**. Instale esse primeiro ajuste manualmente pelo GitHub, por cima do app existente. A partir dele, toque em **Verificar atualizações → Baixar e instalar**. Se solicitado, permita instalações desta fonte para o Orbe, volte ao aplicativo e toque em **Continuar instalação**. Confirme a atualização no instalador do Android; não é necessário desinstalar o app.

A versão exibida continua **2.0.0**; o código interno atual é **20003 (ajuste 3)**. O aplicativo busca os ajustes no repositório oficial, verifica SHA-256 e abre o instalador com acesso temporário somente ao APK. Sua conta e os dados são preservados pela atualização com a mesma assinatura. Para futuras publicações, consulte [Atualizações Android](ATUALIZACOES-ANDROID.md).

No ajuste 3, a agenda atualiza automaticamente a cada 15 segundos com a tela ativa. Somente WillianBN altera a conexão em Conta, com efeito global. O endereço inicial de descoberta precisa permanecer disponível e o novo destino deve usar o mesmo banco; mudar a URL não transfere dados.
