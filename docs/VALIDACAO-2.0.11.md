# Validação Orbe 2.0.11

Duração dos serviços em horas e minutos em todo o app e site. Seletores separados em criação/edição, mantendo os valores exatos em minutos no contrato existente da API e sem migração de banco.

- Windows Debug: edição real com catálogo sintético em 1440/430 px; preservação de 30min, conversão 1h30→90min, 2h15→135min, limites 1min/24h e rejeição de zero/24h1min. Capturas conferidas.
- Web390/768/1440: fluxo de reserva, categorias, retorno, conflitos e idempotência; edição preserva135min, PUT envia90min, zero/24h1min não enviam requisição e criação24h envia1440min. Layout sem overflow.
- Listas, cartões e resumos usam o mesmo formatador em cada plataforma.

Pacotes finais, Android, implantação e publicação em verificação.
