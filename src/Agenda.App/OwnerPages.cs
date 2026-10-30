using System.Text.Json;
using Agenda.Core;
using static Agenda.App.OrbeUi;

namespace Agenda.App;
internal static partial class AccountPages
{
 internal static Page Employees(CloudApi api)
 {
  var page=new CloudFormPage(api,"Equipe e acessos"){ReloadOnReturn=true};
  page.Load=async()=>
  {
   var rows=await api.Get<JsonElement>("owner/employees");var me=await api.Me();page.Fields.Clear();page.Fields.Add(Text("Sua equipe, seus acessos",24,Ink,true));page.Fields.Add(Text("Somente WillianBN cria acessos e atribui serviços. Cada profissional configura a própria disponibilidade.",13,Muted));page.Fields.Add(Button("+ Funcionário",async()=>await page.Navigation.PushAsync(NewEmployee(api)),primary:true));
   if(!rows.EnumerateArray().Any(x=>S(x,"userId")==me.Id))page.Fields.Add(Button("Eu também atendo",()=>page.Run(async()=>{await api.Post<JsonElement>("owner/employees",new{userName="WillianBN",email=me.Email,fullName=me.FullName,password=""});await page.Load!();})));
   foreach(var row in rows.EnumerateArray()){var person=row.Clone();page.Fields.Add(Card(Stack(12,Between(Text(S(person,"displayName"),19,Ink,true),Pill(B(person,"enabled")?"ATIVO":"INATIVO",B(person,"enabled")?Mint:Coral)),Text(S(person,"userName")+" · "+S(person,"email"),12,Muted),Button("Configurar profissional",async()=>await page.Navigation.PushAsync(EditEmployee(api,person))))));}
  };return page;
 }
 private static Page NewEmployee(CloudApi api)
 {
  var page=new CloudFormPage(api,"Novo funcionário");Entry name=Input("Nome completo"),user=Input("Ex.: maria.silva"),email=Input("E-mail do profissional"),password=Input("Senha inicial");email.Keyboard=Keyboard.Email;password.IsPassword=true;password.MaxLength=256;
  page.Fields.Add(Stack(18,Text("Crie um acesso pessoal",24,Ink,true),Field("Nome",name),Field("Usuário",user),Field("E-mail",email),Field("Senha inicial",password,"Pelo menos 10 caracteres, com letras maiúsculas, minúsculas e números. Forneça a senha diretamente ao profissional."),Button("Criar funcionário",()=>page.Run(async()=>{await api.Post<JsonElement>("owner/employees",new{fullName=Required(name,"o nome"),userName=Required(user,"o usuário"),email=Required(email,"o e-mail"),password=Required(password,"a senha inicial")});password.Text="";await Back(page);}),primary:true)));return page;
 }
 private static Page EditEmployee(CloudApi api,JsonElement person)
 {
  var page=new CloudFormPage(api,"Configurar profissional");page.Load=async()=>
  {
   var catalog=await api.Get<JsonElement>("owner/services");var assigned=person.GetProperty("services").EnumerateArray().Select(x=>x.GetString()).ToHashSet();Entry name=Input("Nome público",S(person,"displayName"));var enabled=new Switch{IsToggled=B(person,"enabled")};var checks=new List<(string Id,CheckBox Check)>();
   page.Fields.Add(Stack(14,Text(S(person,"userName"),23,Ink,true),Field("Nome exibido às clientes",name),Between(Text("Profissional ativo",13,Muted),enabled),Text("Serviços que realiza",18,Ink,true)));
   foreach(var service in catalog.EnumerateArray()){var check=new CheckBox{IsChecked=assigned.Contains(S(service,"id")),Color=Violet};checks.Add((S(service,"id"),check));page.Fields.Add(Row(10,check,Text(S(service,"name"),13,Ink)));}
   page.Fields.Add(Text("Desativar o acesso não cancela reservas existentes. Confira a agenda antes de reorganizar os atendimentos.",12,Muted));page.Fields.Add(Button("Salvar profissional",()=>page.Run(async()=>{await api.Put<JsonElement>("owner/employees/"+S(person,"id"),new{version=S(person,"version"),displayName=Required(name,"o nome"),enabled=enabled.IsToggled,services=checks.Where(x=>x.Check.IsChecked).Select(x=>x.Id).ToArray()});await Back(page);}),primary:true));
   if(S(person,"userName")!="WillianBN")page.Fields.Add(Button("Redefinir senha",async()=>await page.Navigation.PushAsync(ResetEmployee(api,S(person,"id")))));
  };return page;
 }
 private static Page ResetEmployee(CloudApi api,string id)
 {
  var page=new CloudFormPage(api,"Redefinir acesso");var password=Input("Nova senha");password.IsPassword=true;password.MaxLength=256;page.Fields.Add(Stack(18,Text("Defina uma nova senha",23,Ink,true),Field("Senha",password,"As sessões anteriores serão encerradas."),Button("Redefinir senha",()=>page.Run(async()=>{await api.Post<JsonElement>("owner/employees/"+id+"/password",new{password=Required(password,"a nova senha")});password.Text="";await Back(page);}),primary:true)));return page;
 }
 internal static Page Services(CloudApi api)
 {
  string? selectedEmployee=null;var page=new CloudFormPage(api,"Serviços"){ReloadOnReturn=true};page.Load=async()=>
  {
   var profile=await api.Me();var people=await api.Get<CloudEmployee[]>("team/employees");page.Fields.Clear();page.Fields.Add(Text(profile.Owner?"Serviços dos profissionais":"Meus serviços",24,Ink,true));
   if(people.Length==0){page.Fields.Add(Text("Cadastre um profissional na seção Equipe para começar.",14,Muted));return;}
   var picker=People(people,selectedEmployee??profile.EmployeeId??people[0].Id);if(profile.Owner)page.Fields.Add(Field("Profissional",picker));var host=Stack(16);page.Fields.Add(host);
   async Task Render(){var employee=Picked(picker);selectedEmployee=employee;var catalog=await api.Get<JsonElement>("team/services?employeeId="+Uri.EscapeDataString(employee));host.Children.Clear();host.Add(Button("+ Serviço",async()=>await page.Navigation.PushAsync(EditService(api,employee)),primary:true));
    foreach(var record in catalog.EnumerateArray()){var item=record.Clone();host.Add(Card(Stack(12,Between(Text(S(item,"name"),19,Ink,true),Pill(B(item,"enabled")?"ATIVO":"INDISPONÍVEL",B(item,"enabled")?Mint:Muted)),Text($"{I(item,"durationMinutes")} min · {Finance.Currency(item.GetProperty("priceCents").GetInt64())}",13,Muted),Button("Editar serviço",async()=>await page.Navigation.PushAsync(EditService(api,employee,item))),Button("Excluir serviço",()=>page.Run(async()=>{if(!await page.DisplayAlertAsync("Excluir serviço?","Ele sairá do catálogo deste profissional. Agendamentos e valores anteriores serão preservados.","Excluir","Manter"))return;await api.Delete("team/services/"+S(item,"id")+"?employeeId="+Uri.EscapeDataString(employee),new{version=S(item,"version")});await Render();}),danger:true))));}
    if(!catalog.EnumerateArray().Any())host.Add(Text("Crie seu primeiro serviço para disponibilizá-lo às clientes.",14,Muted));
   }
   picker.SelectedIndexChanged+=async(_,_)=>await page.Run(Render);await Render();
  };return page;
 }
 private static Page EditService(CloudApi api,string employee,JsonElement? item=null)
 {
  var page=new CloudFormPage(api,item is null?"Novo serviço":"Editar serviço");Entry name=Input("Nome do serviço",item is null?"":S(item.Value,"name")),duration=Input("60",item is null?"60":I(item.Value,"durationMinutes").ToString()),price=Input("0,00",item is null?"0,00":(item.Value.GetProperty("priceCents").GetInt64()/100m).ToString("F2",Finance.Brazilian));duration.Keyboard=price.Keyboard=Keyboard.Numeric;var enabled=new Switch{IsToggled=item is null||B(item.Value,"enabled")};
  page.Fields.Add(Stack(18,Field("Serviço",name),Field("Duração · minutos",duration),Field("Preço · R$",price),Between(Text("Disponível para reservar",13,Muted),enabled),Text("O serviço pertence ao profissional selecionado. Alterar o catálogo não modifica reservas existentes.",12,Muted),Button("Salvar serviço",()=>page.Run(async()=>{var input=new{name=Required(name,"o nome"),durationMinutes=Number(duration,"a duração"),priceCents=Finance.ParseCents(price.Text??""),enabled=enabled.IsToggled,version=item is null?null:S(item.Value,"version")};var path="team/services"+(item is null?"":"/"+S(item.Value,"id"))+"?employeeId="+Uri.EscapeDataString(employee);if(item is null)await api.Post<JsonElement>(path,input);else await api.Put<JsonElement>(path,input);await Back(page);}),primary:true)));return page;
 }
 internal static Page Settings(CloudApi api)
 {
  var page=new CloudFormPage(api,"Regras e reservas online");page.Load=async()=>
  {
   var data=await api.Get<JsonElement>("owner/settings");Entry name=Input("Seu estabelecimento",S(data,"name"));Entry step=Input("15",I(data,"slotMinutes").ToString()),lead=Input("120",I(data,"leadMinutes").ToString()),horizon=Input("60",I(data,"horizonDays").ToString()),cancel=Input("24",I(data,"cancellationHours").ToString());foreach(var e in new[]{step,lead,horizon,cancel})e.Keyboard=Keyboard.Numeric;
   var imported=new CheckBox{IsChecked=B(data,"importCompleted"),Color=Violet};var open=new Switch{IsToggled=B(data,"publicBookingOpen")};var zones=new[]{"America/Sao_Paulo","America/Manaus","America/Rio_Branco","America/Noronha"}.Append(S(data,"timeZoneId")).Distinct().ToArray();var zone=new Picker{ItemsSource=zones,SelectedIndex=Array.IndexOf(zones,S(data,"timeZoneId"))};
   page.Fields.Add(Stack(18,Field("Nome do estabelecimento",name),Field("Intervalo entre horários · minutos",step),Field("Antecedência mínima · minutos",lead),Field("Agenda disponível · dias",horizon),Field("Prazo para cancelar ou remarcar · horas",cancel),Field("Fuso do estabelecimento",zone),Row(8,imported,Text("Conferi a importação dos dados antigos, ou não há dados para importar.",12,Muted)),Between(Text("Abrir reservas públicas",14,Ink,true),open),Text("Configure profissionais, serviços e jornadas. Atribua os agendamentos importados antes de abrir o site para reservas.",12,Muted),Button("Salvar configurações",()=>page.Run(async()=>{await api.Put<JsonElement>("owner/settings",new{version=S(data,"version"),name=Required(name,"o nome"),slotMinutes=Number(step,"o intervalo"),leadMinutes=Number(lead,"a antecedência"),horizonDays=Number(horizon,"o horizonte"),cancellationHours=Number(cancel,"o prazo"),timeZoneId=(string)zone.SelectedItem,importCompleted=imported.IsChecked,publicBookingOpen=open.IsToggled});await api.ConfigureZone();await Back(page);}),primary:true)));};return page;
 }
 internal static Page Import(CloudApi api)
 {
  var page=new CloudFormPage(api,"Importar e atribuir histórico");page.Fields.Add(Stack(16,Text("Seu histórico, preservado",24,Ink,true),Text("A importação mantém IDs, valores e vínculos. Os registros entram sem profissional para sua conferência; nomes antigos não criam contas de cliente.",13,Muted)));
#if WINDOWS
  page.Fields.Add(Button("Selecionar banco Orbe no computador",()=>page.Run(async()=>
  {
   var picked=await FilePicker.Default.PickAsync(new PickOptions{PickerTitle="Selecione apenas o banco SQLite dedicado do Orbe"});if(picked is null)return;
   var result=await Task.Run(()=>global::Orbe.Import.LegacyExport.Export(picked.FullPath,Path.Combine(FileSystem.AppDataDirectory,"migrations")));
   await page.DisplayAlertAsync("Backup criado","A cópia de segurança e o arquivo de importação foram salvos em:\n"+Path.GetDirectoryName(result.BackupPath),"Continuar");
   await using var stream=File.OpenRead(result.JsonPath);await Preview(stream);
  }),primary:true));
#else
  page.Fields.Add(Text("Use o Orbe no Windows para gerar a cópia do banco Nexus. Aqui você pode importar um JSON já exportado.",12,Muted));
#endif
  page.Fields.Add(Button("Selecionar arquivo de importação JSON",()=>page.Run(async()=>{var file=await FilePicker.Default.PickAsync(new PickOptions{PickerTitle="Selecione o JSON exportado pelo Orbe"});if(file is null)return;await using var stream=await file.OpenReadAsync();await Preview(stream);})));page.Fields.Add(Button("Atribuir registros aos profissionais",async()=>await page.Navigation.PushAsync(Assignments(api))));
  async Task Preview(Stream stream)
  {
   using var document=await JsonDocument.ParseAsync(stream);var data=document.RootElement.Clone();if(data.GetRawText().Length>2*1024*1024)throw new ArgumentException("O arquivo é extenso demais para a importação direta. Entre em contato para migrar um histórico maior.");var preview=await api.Post<JsonElement>("owner/import/preview",data);
   var summary=$"{I(preview,"appointments")} agendamentos e {I(preview,"entries")} lançamentos.\nReceitas: {Finance.Currency(preview.GetProperty("income").GetInt64())}\nDespesas: {Finance.Currency(preview.GetProperty("expense").GetInt64())}";
   if(B(preview,"alreadyImported")){await page.DisplayAlertAsync("Arquivo já importado",summary+"\nNão serão criadas duplicatas.","Entendi");return;}
   if(await page.DisplayAlertAsync("Importar os dados conferidos?",summary+"\nNenhum dado do banco original será alterado.","Importar","Cancelar")){await api.Post<JsonElement>("owner/import/commit",data);await page.DisplayAlertAsync("Histórico importado","Atribua os agendamentos aos profissionais antes de abrir as reservas públicas.","Continuar");await page.Navigation.PushAsync(Assignments(api));}
  }
  return page;
 }
 private static Page Assignments(CloudApi api)
 {
  var page=new CloudFormPage(api,"Conferir atribuições"){ReloadOnReturn=true};page.Load=async()=>
  {
   var data=await api.Get<JsonElement>("owner/import/unassigned");var people=await Active(api);page.Fields.Clear();page.Fields.Add(Text("Cada registro no lugar certo",23,Ink,true));var count=0;
   foreach(var record in data.GetProperty("appointments").EnumerateArray()){var item=record.Clone();var picker=People(people);page.Fields.Add(Card(Stack(14,Text(S(item,"title"),18,Ink,true),Text(clientDate(item),12,Muted),Field("Profissional",picker),Button("Atribuir agendamento",()=>page.Run(async()=>{await api.Post<JsonElement>("owner/import/appointments/"+Uri.EscapeDataString(S(item,"id"))+"/assign",new{employeeId=Picked(picker),version=S(item,"version")});await page.Load!();}),primary:true))));count++;}
   foreach(var record in data.GetProperty("entries").EnumerateArray()){var item=record.Clone();var picker=People(people);page.Fields.Add(Card(Stack(14,Text(S(item,"description"),18,Ink,true),Text(Finance.Currency(item.GetProperty("cents").GetInt64())+" · avulso sem atribuição",12,Muted),Field("Profissional",picker),Button("Atribuir lançamento",()=>page.Run(async()=>{await api.Post<JsonElement>("owner/import/entries/"+Uri.EscapeDataString(S(item,"id"))+"/assign",new{employeeId=Picked(picker),version=S(item,"version")});await page.Load!();})))));count++;}
   if(count==0)page.Fields.Add(Text("Nenhum registro aguardando atribuição.",14,Mint));
   string clientDate(JsonElement item)=>api.Local(item.GetProperty("start").GetDateTimeOffset()).ToString("dd/MM/yyyy HH:mm");
  };return page;
 }
}
