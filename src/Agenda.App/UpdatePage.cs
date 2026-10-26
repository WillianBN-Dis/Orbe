using Agenda.Core;
using static Agenda.App.OrbeUi;

namespace Agenda.App;

internal sealed class UpdatePage:ContentPage
{
 private readonly Label status=Text("Confira se há um novo ajuste para o seu Orbe.",14,Muted),details=Text("",13,Muted);
 private readonly ProgressBar progress=new(){ProgressColor=Mint,IsVisible=false};
 private readonly ActivityIndicator spinner=new(){Color=Violet,IsVisible=false};
 private readonly Button check,install;
 private AndroidUpdate? available;
 private CancellationTokenSource? cancellation;
 private bool busy;
 public UpdatePage()
 {
  Title="Atualizar aplicativo";BackgroundColor=CanvasColor;SafeAreaEdges=SafeAreaEdges.All;
  check=Button("Verificar atualizações",Check,primary:true);install=Button("Baixar e instalar",Install,primary:true);install.IsVisible=false;
  var panel=Stack(18,Card(Stack(18,Pill("SEMPRE EM DIA",Mint),Text("Um Orbe cada vez melhor.",27,Ink,true),Text("Versão instalada: "+AppInfo.Current.VersionString,14,Ink),status,spinner,details,progress,check,install,Text("A atualização vem do GitHub oficial. O Android pedirá sua confirmação para instalar; sua conta e seus dados são preservados.",12,Muted),Text("Compilação "+AppInfo.Current.BuildString,10,Dim)),22));
  panel.Padding=22;panel.MaximumWidthRequest=780;panel.HorizontalOptions=LayoutOptions.Fill;Content=new ScrollView{Content=panel};
 }
 protected override void OnDisappearing(){cancellation?.Cancel();base.OnDisappearing();}
 private async Task Run(Func<AppUpdates,CancellationToken,Task> action)
 {
  if(busy)return;busy=true;check.IsEnabled=install.IsEnabled=false;spinner.IsVisible=spinner.IsRunning=true;cancellation=new();
  cancellation.CancelAfter(TimeSpan.FromMinutes(15));
  try{using var service=new AppUpdates();await action(service,cancellation.Token);}
  catch(OperationCanceledException){status.Text="A verificação ou o download foi cancelado. Você pode tentar novamente.";}
  catch(Exception e){status.Text=e is InvalidDataException or HttpRequestException?e.Message:"Não foi possível atualizar agora. Confira sua internet e tente novamente.";}
  finally{cancellation.Dispose();cancellation=null;busy=false;check.IsEnabled=install.IsEnabled=true;spinner.IsVisible=spinner.IsRunning=false;}
 }
 private Task Check()=>Run(async(service,token)=>
 {
  status.Text="Consultando atualizações…";progress.IsVisible=false;install.IsVisible=false;details.Text="";available=null;var update=await service.Check(token);
  if(!int.TryParse(AppInfo.Current.BuildString,out var current))throw new InvalidDataException("Não foi possível identificar a versão instalada.");
  if(update.Build<=current){status.Text="Seu Orbe já está atualizado.";return;}
  available=update;status.Text="Novo ajuste disponível · Orbe "+update.Version;details.Text=update.Notes;install.Text="Baixar e instalar";install.IsVisible=true;
 });
 private Task Install()=>Run(async(service,token)=>
 {
  if(available is null)return;progress.IsVisible=true;status.Text="Baixando atualização…";
  var downloading=true;
  string file;
  try{file=await service.Download(available,Path.Combine(FileSystem.CacheDirectory,"updates"),new Progress<double>(p=>{if(!downloading)return;progress.Progress=p;status.Text=p>0?$"Baixando atualização… {p:P0}":"Baixando atualização…";}),token);}
  finally{downloading=false;}
  progress.Progress=1;
#if ANDROID
  AndroidAppInstaller.Validate(file,available.Build);
  if(!AndroidAppInstaller.Allowed())
  {
   install.Text="Continuar instalação";status.Text="Permita que o Orbe instale suas atualizações. Depois volte aqui e toque em Continuar instalação.";
   if(await DisplayAlertAsync("Permitir atualizações do Orbe","Na próxima tela, ative Permitir desta fonte para o Orbe. Depois volte ao aplicativo para continuar.","Abrir configurações","Agora não"))AndroidAppInstaller.OpenPermission();
   return;
  }
  status.Text="Confirme a atualização no instalador do Android.";
  if(!await Launcher.Default.OpenAsync(new OpenFileRequest("Atualizar Orbe",new ReadOnlyFile(file,"application/vnd.android.package-archive"))))throw new InvalidDataException("O Android não disponibilizou um instalador para este pacote.");
#else
  status.Text="Este ajuste está disponível para o aplicativo Android.";
#endif
 });
}
