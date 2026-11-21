# Validação Orbe 2.0.11

Duração dos serviços em horas e minutos em todo o app e site. Seletores separados em criação/edição, mantendo os valores exatos em minutos no contrato existente da API e sem migração de banco.

- Windows Debug: edição real com catálogo sintético em 1440/430 px; preservação de 30min, conversão 1h30→90min, 2h15→135min, limites 1min/24h e rejeição de zero/24h1min. Capturas conferidas.
- Web390/768/1440: fluxo de reserva, categorias, retorno, conflitos e idempotência; edição preserva135min, PUT envia90min, zero/24h1min não enviam requisição e criação24h envia1440min. Layout sem overflow.
- Listas, cartões e resumos usam o mesmo formatador em cada plataforma.

WindowsSetup instalou/abriu2.0.11.20027/desinstalou isoladamente. Android formulário service-edit passou, APK atualizou a versão anterior, abriu duas vezes e retornou do segundo plano sem crash. Assinatura original e build20027 conferidos. Site implantado após backup na aplicação existente, healthb5004b6 pronto, e-mail ativo e catálogos preservados. Release v2.0.11 publicada somente com Setup e APK. Downloads públicos baixados e hashes conferidos. Cliente AppUpdates real confirmou os dois feeds públicos2.0.11/build20027 com URLs e hashes corretos.

SHA-256 Windows:36dbd577915ede2f7f80d77828b88482aa40fd9ca578ee27e60b9720ccbf9da3

SHA-256 Android:7dcfc3b19401e7879c303a49f75d5a8d0b9a8d683dd707a7f01c530ed1e2ad82
