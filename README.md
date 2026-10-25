# Orbe

**Tempo e finanças em sintonia.** Agenda e calendário financeiro em C#/.NET MAUI para Windows e Android.

<p align="center"><img src="assets/orbe-icon.png" width="128" alt="Ícone Orbe" /></p>

## Instalar

**[Baixar instalador Windows](https://github.com/WillianBN-Dis/Orbe/releases/latest/download/Orbe-Setup.exe)** · [Todas as versões](https://github.com/WillianBN-Dis/Orbe/releases)

Windows x64, versão 1809 ou posterior. Um único `Orbe-Setup.exe` inclui .NET e Windows App SDK; não é necessário instalar SDK, Python ou runtime separado. Assistente em português, instalação por usuário e atalho opcional na área de trabalho. Atualize executando o novo instalador.

Android: [baixe o APK 1.0.2](https://github.com/WillianBN-Dis/Orbe/releases/download/v1.0.2/Orbe-1.0.2.apk). A versão 1.0.0 permanece no histórico, mas seu APK apresenta falha na abertura. A instalação é direta, sem Play Store. A versão estável usa assinatura própria; a prévia local de desenvolvimento pode precisar ser removida antes da primeira instalação estável.

## Recursos
- Calendário mensal com marcadores, agenda do dia, filtros e navegação por data.
- Criar, editar e excluir atendimentos, compromissos, folgas e outros eventos.
- Horário de início/término, pessoa opcional, observações, valor previsto e aviso de sobreposição.
- Receitas e despesas efetivas, lucro por dia/mês/ano e gráficos animados.
- Tema escuro, navegação lateral no computador e inferior em telas estreitas.
- Demonstração opcional, claramente identificada, com dados fictícios em memória e salvamento desativado.

**Lucro = recebimentos menos despesas.** Agendar não gera receita. Excluir um evento também exclui seus lançamentos financeiros vinculados; lançamentos avulsos e de outros eventos são mantidos. Valores em centavos inteiros; horários locais do negócio.

## Dados e Nexus

Para usar seus dados, o Orbe depende do **[Nexus](https://github.com/WillianBN-Dis/Nexus)** instalado e em execução no computador. O Nexus é configurado separadamente; não é instalado pelo Orbe.

1. No Nexus, crie um banco dedicado.
2. Execute cada `CREATE TABLE` de [database/001-inicial.sql](database/001-inicial.sql) separadamente no editor SQL.
3. Ative a API e gere uma chave com leitura/escrita somente para esse banco.
4. Em **Conexão** no Orbe, informe endereço e chave, consulte os bancos e selecione o preparado.

No mesmo computador: `http://127.0.0.1:5080`. No Android: IPv4 privado do computador Nexus, na mesma rede local. O token fica no armazenamento seguro do dispositivo; o app não abre o catálogo pessoal diretamente, não configura Firewall e não envia dados a uma nuvem própria.

A desinstalação remove o programa, preservando bancos Nexus e preferências/credenciais fora da pasta instalada. Veja [Primeiros passos](docs/PRIMEIROS-PASSOS.md).

## Compilar e testar

Requer .NET SDK 10 e workloads `maui-windows`/`maui-android`. Para gerar o instalador, Inno Setup 6 também é necessário.

```powershell
./build.ps1 -Target Windows
./build.ps1 -Target Android
./release.ps1 -Version 1.0.2
./installer/test-installer.ps1 -Version 1.0.2
./release-android.ps1 -Version 1.0.2 -VersionCode 10002
```

O teste padrão possui 25 verificações de regras/HTTP. O teste opcional `tests/Agenda.NexusChecks` possui 12 verificações contra o servidor Nexus real com conta, chave e banco exclusivamente temporários; requer o repositório Nexus em uma pasta irmã, ou `-p:NexusRoot=CAMINHO`.

O instalador é testado com identificação e pasta temporárias, abertura real do aplicativo, reinstalação e desinstalação. O comando `--startup-check` usa dados sintéticos, verifica a janela e encerra sem ler credenciais pessoais.

[Guia de publicação](docs/PUBLICACAO.md) · [Histórico](CHANGELOG.md) · [Identidade visual](assets/IDENTIDADE.md)

## Limites da versão 1.0

Sem sincronização offline, acesso pela internet, contas próprias, recorrência, lembretes ou recebimento automático. Correção de lançamento por exclusão e recadastro. Escritas não são repetidas automaticamente; em conflito, atualizar antes de refazer a alteração. O Nexus não oferece transações entre requisições nem snapshot global de leitura.

O instalador Windows não possui assinatura Authenticode. O APK é assinado com chave de distribuição própria, com execução da correção 1.0.1 validada no emulador Android 16 (instalação limpa e atualização); teste em aparelho físico permanece pendente. Não há atualizador automático nesta versão.
