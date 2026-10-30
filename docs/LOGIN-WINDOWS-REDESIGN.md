# Login Windows — nova composição

Revisão de 30/10/2026, após rejeição visual da compilação 20005.

## Direção visual

Um painel único centralizado, com largura máxima de 1360 pixels, aproxima a marca do formulário. Lado esquerdo em violeta com ilustração orbital vetorial nativa, marca e descrição curta; lado direito com hierarquia de acesso, campos em moldura única, botão de mostrar/ocultar senha e ação de reserva para clientes. Fundo em gradiente escuro. Abaixo de 1200 pixels a ilustração é ocultada e a marca acompanha o formulário. Janelas baixas reduzem os espaçamentos; rolagem mantém os controles acessíveis.

Implementação Windows separada em LoginPage.Desktop.cs; Android preserva o layout existente. Login, restauração de sessão, descoberta de conexão e links do portal utilizam os mesmos fluxos anteriores. Nenhum servidor, cadastro ou dado foi alterado.

## Verificação

Debug Windows compilado sem erros ou avisos. Capturas finais em 1024×768, 1440×960 e 3440×1400, com viewport real conferido, inspecionadas. Fixture Debug exercita validação vazia sem chamar API e os eventos reais de mostrar/ocultar senha; passou nos três tamanhos. Fixture não integra Release. Arte desenhada no próprio aplicativo, sem imagem remota ou dados de demonstração.

Produto permanece 2.0.0; compilação Windows 20006. Android continua 20004. Entrega pelo instalador local; nenhuma release publicada automaticamente.

Instalador autocontido 2.0.0.20006 instalado, aberto/renderizado e desinstalado em pasta isolada; aprovado. Captura da Release conferida. Setup local dist/Orbe-Setup.exe e aliases substituídos. SHA-256: 5b6fd51df639198ea2de910bcd889cd9f32eaf27b4b2e260179652922a42bf81.
