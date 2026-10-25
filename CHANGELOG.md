# Histórico

## 1.0.2

- Excluir um agendamento também exclui todos os seus lançamentos financeiros vinculados, com confirmação explícita e atualização dos resultados.
- Formulário financeiro permite selecionar um agendamento vinculado ou manter o lançamento avulso.
- Lançamentos avulsos e de outros eventos são preservados. Conflitos e falhas parciais interrompem a operação, sem repetir escritas automaticamente.
- Não requer alterações na estrutura do banco existente.

## 1.0.1 — correção Android

- Corrigida a falha de inicialização `MauiApplication.n_onCreate` do APK Release: desativada a geração de marshal methods JNI no Android.
- Publicação Android limpa os intermediários antes de compilar e permite incrementar o código da versão, preservando a assinatura.
- APK assinado validado no emulador Android 16: atualização de 1.0.0, duas aberturas, navegação ao Financeiro e retorno do segundo plano. Teste físico permanece pendente.
- Versão anterior preservada no histórico. O instalador Windows continua sendo o mesmo da versão 1.0.0.

## 1.0.0

Primeira versão publicada do Orbe.

- Agenda para atendimentos, compromissos, folgas e outros eventos.
- Calendário mensal, visão diária, filtros, edição e exclusão.
- Receitas e despesas, resultado diário/mensal/anual e gráficos animados.
- Interface escura adaptada a Windows e Android, marca e ícone próprios.
- API local Nexus, seleção de banco e armazenamento seguro da chave.
- Instalador único Windows autocontido e APK Android assinado para distribuição direta.
- Testes de núcleo/HTTP, integração Nexus em dados temporários e instalação/reinstalação/desinstalação isoladas.

As prévias 0.1 e 0.2 foram utilizadas apenas no desenvolvimento local. A versão Android estável usa chave própria, diferente da assinatura de desenvolvimento.
