namespace Orbe.Cloud.Core;

public sealed record BookingPolicy(int SlotMinutes = 15, int LeadMinutes = 120,
    int HorizonDays = 60, int CancellationHours = 24, string TimeZoneId = "America/Sao_Paulo")
{
    public void Validate()
    {
        if (SlotMinutes is < 1 or > 120 || LeadMinutes < 0 || HorizonDays is < 1 or > 365 || CancellationHours < 0)
            throw new ArgumentException("Regras de agendamento inválidas.");
        _ = TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId);
    }
}

// Minutes from midnight; 1440 is allowed only as the end of a working window.
public sealed record WorkingWindow(int StartMinute, int EndMinute)
{
    public void Validate()
    {
        if (StartMinute < 0 || EndMinute > 1440 || StartMinute >= EndMinute)
            throw new ArgumentException("Informe um período de trabalho válido dentro do dia.");
    }
}
public sealed record WeeklyWindow(DayOfWeek Day, WorkingWindow Window);
public sealed record DateSchedule(DateOnly Date, IReadOnlyList<WorkingWindow> Windows);
public sealed record OccupiedInterval(DateTimeOffset Start, DateTimeOffset End)
{
    public bool Overlaps(DateTimeOffset start, DateTimeOffset end) => Start < end && End > start;
}
public sealed record AvailableSlot(DateTimeOffset Start, DateTimeOffset End);

public static class Availability
{
    // Input must already be scoped to one employee by the application service.
    // A returned slot is a proposal, never a reservation: revalidate in the database transaction.
    public static IReadOnlyList<AvailableSlot> Calculate(DateOnly date, int durationMinutes,
        BookingPolicy policy, IEnumerable<WeeklyWindow> weekly, IEnumerable<DateSchedule> exceptions,
        IEnumerable<OccupiedInterval> occupied, DateTimeOffset now)
    {
        policy.Validate();
        if (durationMinutes is < 1 or > 1440) throw new ArgumentException("Duração do serviço inválida.");
        var zone = TimeZoneInfo.FindSystemTimeZoneById(policy.TimeZoneId);
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);
        if (date < today || date >= today.AddDays(policy.HorizonDays)) return [];
        var overrides = exceptions.Where(x => x.Date == date).ToArray();
        if (overrides.Length > 1) throw new ArgumentException("Existe mais de uma jornada especial para a data.");
        var windows = (overrides.Length == 1 ? overrides[0].Windows : weekly.Where(x => x.Day == date.DayOfWeek).Select(x => x.Window))
            .OrderBy(x => x.StartMinute).ToArray();
        foreach (var window in windows) window.Validate();
        for (int i = 1; i < windows.Length; i++)
            if (windows[i].StartMinute < windows[i - 1].EndMinute)
                throw new ArgumentException("Períodos da jornada não podem se sobrepor.");
        var busy = occupied.ToArray();
        if (busy.Any(x => x.End <= x.Start)) throw new ArgumentException("Bloqueio de horário inválido.");
        var slots = new List<AvailableSlot>();
        foreach (var window in windows)
            for (int minute = window.StartMinute; minute + durationMinutes <= window.EndMinute; minute += policy.SlotMinutes)
            {
                var startLocal = date.ToDateTime(TimeOnly.MinValue).AddMinutes(minute);
                var endLocal = startLocal.AddMinutes(durationMinutes);
                // Avoid offering a nonexistent or ambiguous wall-clock time during DST changes.
                if (zone.IsInvalidTime(startLocal) || zone.IsAmbiguousTime(startLocal) ||
                    zone.IsInvalidTime(endLocal) || zone.IsAmbiguousTime(endLocal)) continue;
                var start = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(startLocal, zone));
                var end = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(endLocal, zone));
                if (start < now.AddMinutes(policy.LeadMinutes) || busy.Any(x => x.Overlaps(start, end))) continue;
                slots.Add(new(start, end));
            }
        return slots;
    }

    public static bool CanClientChange(DateTimeOffset start, DateTimeOffset now, BookingPolicy policy)
    {
        policy.Validate();
        return start > now && now <= start.AddHours(-policy.CancellationHours);
    }
}
