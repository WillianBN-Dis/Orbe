# Orbe 2.0.1 — candidata em validação

A versão está em preparação, ainda sem release publicada.

- Cada profissional gerencia o próprio catálogo; WillianBN gerencia qualquer profissional. O servidor valida acesso, concorrência e vínculo.
- As ofertas antigas herdam o catálogo comum; alterações por profissional usam overrides e preservam IDs e histórico. Excluir retira a oferta do profissional, preservando agendamentos e valores congelados.
- O portal começa por profissional, depois serviço, dia e horário.
- Jornada semanal e datas especiais com seletores de dia/hora, manhã e tarde, desativação por período, períodos extras e suporte à meia-noite. Bloqueios com atalhos dia inteiro/manhã/tarde/personalizado.
- Notificações no aplicativo e no site: apenas layout de preparação, sem entrega de avisos.

Validação realizada: 70 verificações HTTP/PostgreSQL existentes mais regressões de catálogo/permissões/preço/histórico; seis cenários web com API simulada em390/1440; fixtures Debug nativas em1440/430. A correção posterior dos seletores empilha Início/Fim abaixo de560px; recompilação Debug e novas capturas passaram sem cortes de minutos.

Distribuição final depende ainda dos pacotes autocontidos Windows/Android, instalação/abertura, backup e implantação da migração, verificação HTTPS e publicação com hashes. Não distribuir a candidata intermediária. Produto2.0.1/build20010; somente instalador Windows e APK como anexos da futura release.
