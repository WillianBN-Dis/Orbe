# Orbe 2.0.4

## Suporte e acesso

Em **Suporte**, WillianBN pode cadastrar um WhatsApp internacional e um e-mail. O site e o aplicativo consultam os mesmos contatos no servidor. Funcionários e clientes podem usar os canais cadastrados.

O reenvio da confirmação fica em **Minha conta** no site. Uma cliente com e-mail pendente pode autenticar com a senha para solicitar outro link. A conta permanece com acesso restrito até a confirmação.

## Agendamentos fixos

A cliente escolhe um usuário único em **Minha conta → Usuário para agendamentos fixos** e o informa à profissional. O acesso continua com o e-mail. A profissional abre **Agenda → Agendamento fixo**, informa o usuário exato e confere o nome retornado. A consulta exige acesso à agenda do profissional e retorna somente o nome.

Uma série cria de 2 a 52 ocorrências dentro dos próximos 12 meses, no mesmo dia da semana e horário local, com frequência em semanas. Uma sobreposição com a agenda da profissional ou da cliente recusa a série inteira. As ocorrências podem ser editadas ou canceladas individualmente. O valor informado é previsto e não gera lançamento financeiro. Cada ocorrência de atendimento vinculada aparece na agenda e nas notificações da cliente.

## Android

Para ativar o Firebase Cloud Messaging, registre o pacote `br.com.agendafinanceira.app` em um projeto Firebase. Guarde `google-services.json` fora do repositório e gere o APK passando `-FirebaseConfig` com o caminho do arquivo ao script de release. Essa variante requer Android 6 ou posterior. Configure `Push__ServiceAccountJson` no ambiente privado do servidor com a conta de serviço autorizada a enviar FCM. O arquivo do cliente e a conta de serviço precisam pertencer ao mesmo projeto.

O servidor associa o aparelho à sessão nativa vigente. O aviso na tela bloqueada é genérico. Ao tocá-lo, o aplicativo abre a aba Notificações após autenticar. As mensagens chegam aos aparelhos registrados, com permissão do Android, para avisos novos e não lidos. Os detalhes e o WhatsApp ficam na caixa autenticada.

- [Configurar o aplicativo Android no Firebase](https://firebase.google.com/docs/android/setup)
- [Configurar o envio HTTP v1](https://firebase.google.com/docs/cloud-messaging/send/v1-api)
