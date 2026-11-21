# Correção 2.0.10.8249 — rolagem da agenda diária

Versão base 2.0.10, build interno 20026.

## Comportamento

A grade de horários mantém o gesto de rolagem, inclusive no início/fim do dia. A página rola quando o gesto começa fora da grade. Os botões e eventos continuam recebendo toques. A alteração é restrita ao ScrollView da grade Dia.

No Android, a grade desativa nested scrolling e mantém a interceptação dos ancestrais bloqueada até finalizar/cancelar o gesto. No Windows, desativa o encadeamento de rolagem no controle nativo.

## Verificação

- Windows Debug: rolagem independente de filho/página e configuração nativa; calendários e agendamento rápido em 1440/430 pixels passaram.
- DebugScrollFixture contém uma página de dados sintéticos com grade rolável dentro de página rolável. Android exercita gestos dentro/fora, extremidades, cancelamento e toque; Windows verifica a independência das posições.
- Relatórios e pacotes em artifacts/qa/2.0.10.8249. Android passou os gestos dentro/fora, extremidades, cancelamento e toque (1 clique recebido). Windows Setup instalou/abriu 2.0.10.20026/desinstalou em pasta isolada.

Sem alterações no site/API/banco; não requer implantação do servidor.

## Pacotes

- Windows SHA-256: fe72688a7f8dfc890bbaee1d8fe1e4b014d96bfbb9914045d823c7c480afecfd
- Android SHA-256: bb610cc98d6b50c9baa7712ecf6e99e88c1123d8c464c51e512eadb5d002306c
- APK versionName2.0.10/versionCode20026 e assinatura original conferidos. Atualização sobre o build20025, duas aberturas, retorno do segundo plano e ausência de fixtures Debug no APK distribuído passaram. Release [v2.0.10.8249](https://github.com/WillianBN-Dis/Orbe/releases/tag/v2.0.10.8249) publicada com somente Setup e APK, preservadas as anteriores. Ambos os downloads públicos conferidos por SHA-256. O cliente real AppUpdates confirmou os dois feeds em versão base2.0.10/build20026, URLs da correção e hashes corretos.
