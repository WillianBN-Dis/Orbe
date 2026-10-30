# Validação — Orbe 2.0.0 ajuste 4

Entrega29/10/2026. Fonte implementação f784686, normalização0926beb; produto2.0.0, Windows2.0.0.20004 e Android20004. Site em https://orbe.discloud.dev/entrar .

Calendário: seleção reaproveita42botões e atualiza cartões/timeline; mudança de mês reconstrói somente calendário. Abas preservam botões/glyphs de navegação. Fade180ms removido dessas ações. FixtureDebug verificou seleção, Hoje, meses eConta/Financeiro/Agenda sem reconstruir a navegação. Trabalho síncrono medido emQA30ms na seleção, sem afirmar latência pontaaponta ou desempenho em todos os aparelhos.

LoginWindows: painel direito ocupa44%da largura e altura útil, formulário máximo720px; apresentação à esquerda centralizada dentro da própria coluna. Loginweb: composição desktop em duas colunas, formulário compacto no celular, mostrar/ocultar senha e acessibilidade; autenticação/CSRF mantidos.

Testes: buildDebug sem erros/avisos, capturas nativas3440/1440/430; PlaywrightcomAPIsintética360/390/768/1440/3440 sem overflow, senha visível/erro e envioCSRF. Site público390/1440 comDNSnormal; loginreal apenasClienteTeste e logout204. Nenhum cadastro/agendamento real alterado.

Windowsautocontido: instalador montado com guia atualizado; instalação, abertura/renderização2.0.0.20004 e desinstalação isoladas aprovadas. APKmesmaassinatura(sha256certbbb5effe20b368e404843727b278546002b7f0a182b5a007996e47092669b365),arm64-v8a/x86_64,Android16 noemulador: atualização por cima, duas aberturas e retorno sem crash. Atualizador consultou feed e confirmou20004. EmuladorQAencerrado. Sem validação no Android físico do usuário neste turno.

Publicação separada v2.0.0-ajuste.4 contém SOMENTE Orbe-Setup.exe e Orbe-2.0.0.apk, conforme pedido humano. Documentos/hashes ficam na fonte e notas, sem anexos adicionais. Assets temporários duplicados do ajuste4 removidos de v2.0.0 e seus nomes de ajuste3 restaurados; versões anteriores preservadas. Feed aponta a URL permanente do APK na releaseprópria. Downloads públicos finais baixados sem autenticação e SHA256comparados.

| Arquivo | SHA-256 |
|---|---|
| Orbe-Setup.exe | 02af71e504ebe7662822dd8b52b0e44ea1491a431629eb6d14dd853c4e883ab6 |
| Orbe-2.0.0.apk | 3daf82b4892622698ae96a8f5c4426351d63cf34ae778c4a9b08df0eea9a4880 |

Instalador Windows sem Authenticode. Preview semSMTP segue autorizado. Site/DB/compatibilidade mantêm os mesmos recursosDiscloud; nenhuma alteração emNexus/Firewall, contas ou dados pessoais. Evidênciasprivadas emartifacts/qa/ajuste4.

Compatibilidade do atualizador: o primeiro endereço publicado do APK20004 em v2.0.0/Orbe-2.0.0-ajuste.4.apk foi mantido no histórico, pois o feed público pode permanecer em cache por300s e clientes podem ter consultado essaURL. A release nova tem exatamente dois arquivos; publicações futuras usam somente esses dois instaladores em tag própria. A fonte do feed foi validada pela API GitHub comURL nova; durante o cache, ambos os links20004 permanecem válidos com o mesmoSHA256. Não remover URLs já divulgadas pelo atualizador.
