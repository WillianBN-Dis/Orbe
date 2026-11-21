# Validação Orbe 2.0.12

Aviso e contato para a cliente na confirmação de serviços A partir de. Texto autorizado: O profissional [nome] entrará em contato com você para informar o valor final.

API enriquece apenas a confirmação da própria cliente com mínimo congelado, profissional, telefone e fuso da empresa. Campos opcionais preservam compatibilidade com versões anteriores. Sem nova migração. O aviso Payment do profissional permanece.

- Suíte HTTP/PostgreSQL passou: contato/fuso corretos, ausência de telefone, aviso pessoal, preço fixo sem conselho/contato; regressões de ACL, leitura/exclusão, cancelamento e financeiro, incluindo Payment, passaram.
- Web390/768/1440 passou: texto atualizado, telefone inválido/ausente sem botão, WhatsApp do responsável com serviço/data/hora no fuso correto, preço fixo sem aviso e ação Payment mantida. Regressões de reserva e duração de serviços passaram.
- WindowsDebug1440/430 passou: três confirmações sintéticas, dois avisos de preço e somente um botão válido; captura430 conferida.

Pacotes finais/Android/deploy/publicação em andamento.
