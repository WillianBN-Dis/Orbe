# Layout Windows — revisão de 30/10/2026

O login usa uma composição contínua em duas áreas a partir de 1000 pixels: apresentação à esquerda e painel de acesso à direita, com proporção 56/44. O formulário tem largura máxima de 520 pixels; a introdução, 640. Em janela estreita o formulário continua em um cartão com rolagem.

Agenda, financeiro e conta compartilham uma área útil máxima de 1920 pixels. As margens são recalculadas durante o redimensionamento, sem reconstruir os controles de navegação a cada pixel. A área útil se adapta às janelas menores; botões e células mantêm suas dimensões próprias.

Validação: Debug Windows compilado sem erros nem avisos; capturas do login e agenda em 1440×960 e 3440×1400 conferidas. Fixture com dados sintéticos passou seleção de dias, mudança de mês, Hoje e alternância Agenda/Conta/Financeiro, preservando 42 controles do calendário e 3 de navegação. Capturas privadas em artifacts/qa/desktop-30-10. Nenhuma conta ou dado real foi usado. O primeiro build encontrou um arquivo temporário bloqueado; repetição após encerramento dos processos de compilação passou.

Distribuição: produto 2.0.0, compilação Windows 20005; Android permanece 20004. Esta entrega prepara somente o instalador Windows local. Sem nova implantação de site, alteração de servidor ou publicação de release.

Instalador local final gerado e validado: instalação isolada, abertura/renderização 2.0.0.20005 e desinstalação aprovadas. SHA-256: a759ef214df98ef1154e75c438ce6928b00f7016e3b2b0ec93beafd7f29c7b50. Arquivo: dist/Orbe-Setup.exe; aliases locais substituídos para evitar instalação do setup antigo.

A composição de login 20005 foi rejeitada pelo proprietário e substituída pela revisão 20006. Consulte LOGIN-WINDOWS-REDESIGN.md; as mudanças da área de trabalho permanecem.
