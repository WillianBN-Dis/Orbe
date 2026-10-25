# Validação Orbe 1.0.2

- 25 verificações de núcleo/HTTP aprovadas.
- 12 verificações contra API Nexus real e banco temporário aprovadas: CRUD, conflito, exclusão de múltiplos recebimentos e despesa vinculados, preservação de avulsos e outro evento, evento sem lançamentos, snapshot desatualizado, falha parcial e retomada sem repetir escritas automaticamente.
- Windows autocontido e APK Release compilados. Instalador: instalação, abertura real da janela na versão correta, reinstalação, desinstalação e preservação de arquivo sintético aprovadas em pasta/AppId de QA.
- APK atualizado de 1.0.1 para 1.0.2 no emulador Android 16 x86_64: duas aberturas, Financeiro e retorno do segundo plano sem crash. Assinatura de distribuição preservada. Teste físico ainda pendente.

## Comportamento

Excluir agendamento consulta os lançamentos atuais, apaga somente os que têm agendamento_id igual ao ID do evento e depois apaga o evento, usando snapshots otimistas. A confirmação informa esse efeito. O financeiro é recarregado ao concluir. O formulário permite vincular novos lançamentos; recebimento iniciado na agenda vem com vínculo pré-selecionado. Lançamentos antigos sem vínculo não são associados por nome, data ou valor.

A API Nexus não oferece transação entre requisições. Uma falha no meio pode deixar exclusão parcial: o app informa isso e pede atualização para conferir antes de tentar novamente, mantendo o evento até a etapa final quando possível. A verificação inicial de snapshot não bloqueia edições ou novos lançamentos simultâneos durante a operação; não é uma transação atômica. Nenhuma escrita é repetida automaticamente.

Não exige migração de tabelas/colunas. Nenhum banco ou credencial pessoal foi alterado nos testes.

O seletor de agendamento vinculado foi aberto no emulador com dados sintéticos em memória; seleção de evento confirmada no formulário e screenshot inspecionado, sem salvamento.
