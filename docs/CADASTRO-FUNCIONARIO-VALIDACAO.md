# Cadastro de funcionário — mensagens de validação

Corrigido em 30/10/2026: falhas do Identity eram tratadas genericamente como erro de senha. A API agora diferencia usuário inválido, duplicidades, e-mail e requisitos de senha. Política preservada; nenhum cadastro real foi criado ou alterado.

Validação em memória usa UserManager, validadores e hashers reais do Identity: criação e autenticação com usuário teste.funcionario / nome Nome Teste e senha de teste fornecida em variável de ambiente; rejeições por espaço/acento no usuário, senha curta, e-mail e usuário repetidos. Conteúdo da senha não armazenado no repositório. Comando: dotnet run --project tests/Orbe.Server.Checks -- --identity-only.

A suíte HTTP completa não foi executada nesta revisão: autenticação do cluster antigo de QA falhou; cluster parado. Novos casos HTTP compilados, mas não declarados aprovados. Validação direcionada em memória passou. Produção não foi usada para testes de criação de contas.

Implantação ddd9237 no recurso Discloud orbe existente; HTTPS /health confirmou ready e revisão correspondente. Modo de testes e reservas permaneceram abertos. Ajuste no servidor, sem atualizar instaladores.
