# Orbe 2.0.1

Implementação concluída: serviços por profissional com autorização no servidor, reserva profissional → serviço → horário, jornadas manhã/tarde com seletores e períodos desativáveis, atalhos de bloqueio e aba Notificações somente visual.

Migração ProfessionalServiceOptions preserva IDs, reservas e valores históricos. Excluir serviço retira a oferta do profissional sem apagar histórico. Owner acessa todos; funcionário somente o próprio catálogo.

Validado em Windows, Android e site. Consulte VALIDACAO-2.0.1.md para verificações, hashes e limitações. Servidor implantado em 31/10/2026, revisão fb903d9, health ready e modo de testes sem SMTP preservado. Snapshot privado obtido antes da migração. Site real conferido em390/1440, sem criar reservas ou alterar dados pessoais.
