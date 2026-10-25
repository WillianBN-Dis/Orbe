-- Executar cada CREATE separadamente no editor SQL do Nexus, em banco exclusivo de testes.
CREATE TABLE agendamentos (
 id TEXT PRIMARY KEY NOT NULL,
 titulo TEXT NOT NULL CHECK(length(trim(titulo)) > 0),
 tipo TEXT NOT NULL CHECK(tipo IN ('Atendimento','Compromisso','Folga','Outro')),
 inicio TEXT NOT NULL,
 fim TEXT NOT NULL CHECK(fim > inicio),
 cliente TEXT,
 observacoes TEXT,
 valor_previsto_centavos INTEGER NOT NULL DEFAULT 0 CHECK(valor_previsto_centavos >= 0)
);

-- Vínculo informativo: excluir um evento preserva seu histórico financeiro.
CREATE TABLE lancamentos (
 id TEXT PRIMARY KEY NOT NULL,
 descricao TEXT NOT NULL CHECK(length(trim(descricao)) > 0),
 tipo TEXT NOT NULL CHECK(tipo IN ('Receita','Despesa')),
 data TEXT NOT NULL,
 valor_centavos INTEGER NOT NULL CHECK(valor_centavos > 0),
 agendamento_id TEXT
);
