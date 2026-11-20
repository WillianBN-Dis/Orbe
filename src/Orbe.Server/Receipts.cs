using Microsoft.EntityFrameworkCore;

namespace Orbe.Server;

public sealed record ReceiptChange(Guid Version,string Description,DateOnly Date,long Cents);

public sealed class Receipts(OrbeDb db,Actor actor,Scheduling scheduling)
{
 public async Task<FinancialEntry> Create(MoneyInput input)
 {
  if(string.IsNullOrWhiteSpace(input.Description)||input.Description.Length>200||input.Kind is not ("Receita" or "Despesa")||input.Cents<=0)
   throw new ApiError(400,"Confira a descrição e informe um valor positivo.");
  if(input.Id is not null&&!Guid.TryParse(input.Id,out _))throw new ApiError(400,"Identificador inválido.");
  var employee=actor.Owner&&input.EmployeeId is null?null:await actor.Employee(input.EmployeeId,true);
  await using var tx=await db.Database.BeginTransactionAsync();
  if(input.Id is not null)await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({'M'+input.Id},0))");
  ScheduleItem? appointment=null;
  if(input.AppointmentId is not null){
   await Lock(input.AppointmentId);appointment=await scheduling.Accessible(input.AppointmentId,true);
   if(employee is not null&&employee!=appointment.EmployeeId)throw new ApiError(400,"O vínculo pertence a outro profissional.");
   employee=appointment.EmployeeId;
  }
  if(input.Id is not null&&await db.Entries.SingleOrDefaultAsync(x=>x.Id==input.Id) is {} prior){
   if(prior.EmployeeId!=employee||prior.AppointmentId!=input.AppointmentId||prior.Description!=input.Description.Trim()||prior.Kind!=input.Kind||prior.Date!=input.Date||prior.Cents!=input.Cents)
    throw new ApiError(409,"Esta confirmação já foi usada para outro lançamento.");
   return prior;
  }
  if(appointment is not null&&input.Kind=="Receita"){
   if(appointment.Kind!="Atendimento"||appointment.Status!="Confirmed")throw new ApiError(409,"Só atendimentos confirmados podem receber um recebimento.");
   if(await db.Entries.AnyAsync(e=>e.AppointmentId==appointment.Id&&e.Kind=="Receita"))
    throw new ApiError(409,"Recebimento já registrado. Consulte ou edite o lançamento existente.");
  }
  var row=new FinancialEntry{EmployeeId=employee,AppointmentId=input.AppointmentId,Description=input.Description.Trim(),Kind=input.Kind,Date=input.Date,Cents=input.Cents};
  if(input.Id is not null)row.Id=input.Id;db.Entries.Add(row);await db.SaveChangesAsync();await tx.CommitAsync();return row;
 }
 private Task Lock(string id)=>db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"Appointments\" WHERE \"Id\"={id} FOR UPDATE");
 public async Task<FinancialEntry> Edit(string id,ReceiptChange input)
 {
  if(string.IsNullOrWhiteSpace(input.Description)||input.Description.Length>200||input.Cents<=0)throw new ApiError(400,"Confira descrição e valor.");
  var row=await db.Entries.SingleOrDefaultAsync(e=>e.Id==id)??throw new ApiError(404,"Lançamento não encontrado.");
  if(!actor.Owner)await actor.Employee(row.EmployeeId??"unassigned",true);
  await using var tx=await db.Database.BeginTransactionAsync();
  if(row.AppointmentId is {} appointment)await Lock(appointment);
  await db.Entry(row).ReloadAsync();if(db.Entry(row).State==EntityState.Detached)throw new ApiError(409,"Lançamento excluído por outra sessão. Atualize a lista.");
  if(row.Version!=input.Version)throw new ApiError(409,"Lançamento alterado. Atualize antes de editar.");
  row.Description=input.Description.Trim();row.Cents=input.Cents;row.Date=input.Date;row.Version=Guid.NewGuid();
  await db.SaveChangesAsync();await tx.CommitAsync();return row;
 }
 public async Task Delete(string id,VersionInput input)
 {
  var row=await db.Entries.SingleOrDefaultAsync(e=>e.Id==id)??throw new ApiError(404,"Lançamento não encontrado.");
  if(!actor.Owner)await actor.Employee(row.EmployeeId??"unassigned",true);
  await using var tx=await db.Database.BeginTransactionAsync();
  if(row.AppointmentId is {} appointment){
   await Lock(appointment);
   if(row.Kind=="Receita"&&await db.Appointments.SingleOrDefaultAsync(a=>a.Id==appointment) is {} item)item.AutoReceiptSuppressed=true;
  }
  await db.Entry(row).ReloadAsync();if(row.Version!=input.Version)throw new ApiError(409,"Lançamento alterado. Atualize antes de excluir.");
  db.Entries.Remove(row);await db.SaveChangesAsync();await tx.CommitAsync();
 }
}

public sealed class AutomaticReceipts(OrbeDb db,TimeProvider clock)
{
 public async Task Run(CancellationToken token=default)
 {
  var now=clock.GetUtcNow();
  var pending=await (from a in db.Appointments.IgnoreQueryFilters() join b in db.Settings.IgnoreQueryFilters() on a.BusinessId equals b.Id
   where a.Kind=="Atendimento"&&a.Status=="Confirmed"&&!a.PriceFrom&&!a.AutoReceiptSuppressed&&a.ExpectedCents>0&&a.End<=now&&a.End>=b.AutoFinanceStart&&
    !db.Entries.IgnoreQueryFilters().Any(e=>e.AppointmentId==a.Id&&e.Kind=="Receita")
   orderby a.End,a.Id select new{a.Id,a.BusinessId,b.TimeZoneId}).Take(100).ToArrayAsync(token);
  foreach(var candidate in pending){
   db.BusinessId=candidate.BusinessId;
   await using var tx=await db.Database.BeginTransactionAsync(token);
   await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"Appointments\" WHERE \"Id\"={candidate.Id} FOR UPDATE",token);
   var item=await db.Appointments.SingleOrDefaultAsync(a=>a.Id==candidate.Id,token);
   if(item is not null&&item.Status=="Confirmed"&&!item.PriceFrom&&!item.AutoReceiptSuppressed&&item.ExpectedCents>0&&item.End<=now&&
    !await db.Entries.AnyAsync(e=>e.AppointmentId==item.Id&&e.Kind=="Receita",token)){
    var zone=TimeZoneInfo.FindSystemTimeZoneById(candidate.TimeZoneId);
    db.Entries.Add(new(){EmployeeId=item.EmployeeId,AppointmentId=item.Id,Description=item.Title,Kind="Receita",Cents=item.ExpectedCents,
     Date=DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(item.End,zone).DateTime),Automatic=true});
    await db.SaveChangesAsync(token);
   }
   await tx.CommitAsync(token);db.ChangeTracker.Clear();
  }
 }
}

public sealed class ReceiptWorker(IServiceScopeFactory scopes,ILogger<ReceiptWorker> log):BackgroundService
{
 protected override async Task ExecuteAsync(CancellationToken token)
 {
  while(!token.IsCancellationRequested){
   try{using var scope=scopes.CreateScope();await scope.ServiceProvider.GetRequiredService<AutomaticReceipts>().Run(token);}
   catch(OperationCanceledException)when(token.IsCancellationRequested){break;}
   catch(Exception){log.LogWarning("Não foi possível conferir recebimentos automáticos; a rotina tentará novamente.");}
   try{await Task.Delay(TimeSpan.FromSeconds(30),token);}catch(OperationCanceledException){break;}
  }
 }
}
