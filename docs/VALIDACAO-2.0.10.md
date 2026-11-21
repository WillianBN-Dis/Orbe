# Validação Orbe 2.0.10

Versão 2.0.10, Android 20025. Revisão de interface aprovada no chat.

## Escopo

- Agendamento rápido no bloco Dia e em horários livres, com data/hora/profissional preenchidos.
- Formulários de disponibilidade e bloqueios com ações no rodapé, dias recolhíveis e confirmação de mudanças.
- Navegação sem animações e resposta imediata ao toque no celular.
- Reserva web em etapas, resumo editável e distribuição responsiva de calendário e horários.

## Verificações

- Windows Debug: calendário e agendamento rápido em 1440/430 px; prefill 14:30–15:30; formulários de jornadas existentes/novas, datas, bloqueios, perfil, categorias, serviços, convites e plataforma.
- Site com dados sintéticos em 390/768/1440 px: categorias opcionais, voltar preservando escolhas, horários agrupados, ausência de disponibilidade, conflito 409 e confirmação idempotente; sem overflow e sem animações móveis.
- Testes de domínio executados pelo script de release: 25 verificações passaram.

- Windows: instalador autocontido instalado, login renderizado em 2.0.10.20025 e desinstalação isolada concluída.
- Android Debug sintético: formulários e seletores conferidos; teclado aberto mantém Cancelar/Salvar visíveis acima do teclado após configurar Resize explicitamente no MAUI.
- Backup privado antes de implantar o site. HTTPS/health prontos, Studio com reservas abertas, catálogos preservados, e-mail ativo. Login público conferido em navegador 390/1440 px.

- Android final: atualização 2.0.9/20024 → 2.0.10/20025 no emulador Android16; duas aberturas, retorno do segundo plano e ausência de fixture Debug/crash. Uma tentativa precisou ser repetida por falha transitória do logcat do emulador.
- Assinatura Android original preservada; pacote br.com.agendafinanceira.app, versionCode20025 e versionName2.0.10 conferidos.

Não há nova migração de banco nesta versão.

## SHA-256

- Windows: 98f2ac648db62e7199e191d305d2a6098937b5ab26e118ae5e1a2eb391af37d1
- Android: 2121966b889812ab01a80ef6017fd79bf3c998c01d4a28d971921efb9d103e50

Publicação: release v2.0.10 com somente Setup e APK; feeds serão conferidos após a publicação.
