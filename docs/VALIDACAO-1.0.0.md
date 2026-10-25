# Validação — Orbe 1.0.0

Realizada em 24/10/2026, Windows x64.

- 25 verificações de domínio/cliente HTTP: moeda em centavos, datas, lucro por período, paginação de 201 registros, snapshots, conflitos e falhas HTTP sem repetição automática de escrita.
- 8 verificações contra servidor Nexus real: conta, chave e banco exclusivos em diretório temporário; CRUD, conflito otimista, preservação de financeiro e chave somente leitura.
- Instalador compilado com o payload Windows autocontido. Uma compilação de QA altera apenas AppId/nome de saída para isolar o teste da instalação pessoal.
- Instalação silenciosa sem atalhos em diretório temporário; abertura de janela WinUI real verificada, metadados 1.0.0 e encerramento automático com dados sintéticos.
- Reinstalação preservando arquivo não gerenciado; nova abertura verificada.
- Desinstalação removendo o programa e seu registro de QA, preservando arquivo não gerenciado. Diretório temporário limpo.
- APK em Release com versão 1.0.0/build 10000, nome Orbe, assemblies e avisos de licença embutidos; assinatura RSA própria verificada com apksigner (v1, v2 e v3).
- Não foram alterados dados, chaves, catálogo, Firewall ou instalação pessoal Nexus.

Execução Android em aparelho/emulador ainda não validada. Não há assinatura Authenticode no instalador Windows.
