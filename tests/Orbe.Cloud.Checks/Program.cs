using Orbe.Cloud.Core;

var checks = 0;
void Check(bool condition, string name) { if (!condition) throw new Exception(name); checks++; Console.WriteLine("PASS " + name); }
void Reject(Action action, string name) { try { action(); } catch (ArgumentException) { Check(true, name); return; } throw new Exception(name); }
var date = new DateOnly(2026, 10, 26);
DateTimeOffset At(int hour, int minute = 0) => new(2026, 10, 26, hour, minute, 0, TimeSpan.FromHours(-3));
var now = At(6);
var policy = new BookingPolicy();
WeeklyWindow[] hours = [new(DayOfWeek.Monday, new(540, 720)), new(DayOfWeek.Monday, new(780, 1080))];
IReadOnlyList<AvailableSlot> Slots(int duration = 60, OccupiedInterval[]? busy = null, DateSchedule[]? changes = null)
    => Availability.Calculate(date, duration, policy, hours, changes ?? [], busy ?? [], now);
Check(Slots().Count == 26, "Jornada dividida em manhã e tarde");
Check(Slots().All(x => !(x.Start < At(13) && x.End > At(12))), "Serviço inteiro não atravessa almoço");
Check(Slots().Any(x => x.Start == At(11) && x.End == At(12)), "Pode terminar exatamente no fim da jornada");
Check(!Slots().Any(x => x.Start == At(11, 15)), "Não oferece serviço sem tempo suficiente");
var blocked = Slots(busy: [new(At(10), At(11))]);
Check(blocked.All(x => !(x.Start < At(11) && x.End > At(10))), "Compromisso parcial remove todas as sobreposições");
Check(blocked.Any(x => x.Start == At(9) && x.End == At(10)) && blocked.Any(x => x.Start == At(11)), "Limites consecutivos não se sobrepõem");
Check(Slots(busy: [new(At(0), At(0).AddDays(1))]).Count == 0, "Folga integral indisponibiliza o dia");
Check(Slots(changes: [new(date, [])]).Count == 0, "Exceção de jornada vazia fecha o dia");
Check(Slots(changes: [new(date, [new(840, 960)])]).Count == 5, "Jornada específica substitui a semanal");
Check(Slots(240).Count == 5 && Slots(240).All(x => x.Start >= At(13)), "Duração longa só cabe na tarde");
Check(Availability.Calculate(date, 60, policy, hours, [], [], At(8)).Any(x => x.Start == At(10)), "Antecedência mínima aceita limite exato");
Check(Availability.Calculate(date, 60, policy, hours, [], [], At(8)).All(x => x.Start >= At(10)), "Antecedência usa relógio explícito do servidor");
Check(Availability.Calculate(date.AddDays(-1), 60, policy, hours, [], [], now).Count == 0, "Não oferece dia passado");
Check(Availability.Calculate(date.AddDays(60), 60, policy, hours, [], [], now).Count == 0, "Respeita horizonte de 60 dias");
Check(Availability.Calculate(date.AddDays(1), 60, policy, hours, [], [], now).Count == 0, "Funcionário sem jornada no dia não tem vagas");
Check(Availability.CanClientChange(At(10).AddDays(1), At(10), policy), "Cancelamento aceita 24 horas exatas");
Check(!Availability.CanClientChange(At(10).AddDays(1), At(10).AddSeconds(1), policy), "Cancelamento recusa prazo vencido");
Check(!Availability.CanClientChange(At(10), At(10), policy with { CancellationHours = 0 }), "Não altera atendimento já iniciado");
Reject(() => Slots(0), "Rejeita duração inválida");
Reject(() => Availability.Calculate(date, 60, policy, [new(DayOfWeek.Monday, new(540, 660)), new(DayOfWeek.Monday, new(600, 720))], [], [], now), "Rejeita jornadas sobrepostas");
Reject(() => Slots(changes: [new(date, []), new(date, [])]), "Rejeita exceções duplicadas");
Check(Slots().All(x => x.Start.Offset == TimeSpan.Zero && x.Start.Hour >= 12), "Retorna instantes UTC com conversão do fuso do negócio");
Console.WriteLine($"{checks} verificações de disponibilidade passaram; sem banco ou dados pessoais.");
