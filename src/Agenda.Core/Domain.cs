using System.Globalization;

namespace Agenda.Core;

public enum EventKind { Atendimento, Compromisso, Folga, Outro }
public enum EntryKind { Receita, Despesa }
public enum Period { Dia, Mes, Ano }

public sealed record Appointment(string Id, string Title, EventKind Kind, DateTime Start, DateTime End,
    string? Client, string? Notes, long ExpectedCents)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Title)) throw new ArgumentException("Informe o título do agendamento.");
        if (End <= Start) throw new ArgumentException("O término deve ser depois do início.");
        if (ExpectedCents < 0) throw new ArgumentException("O valor previsto não pode ser negativo.");
        if (!Enum.IsDefined(Kind)) throw new ArgumentException("Tipo de agendamento inválido.");
    }
    public bool Intersects(DateOnly date) => Start < date.AddDays(1).ToDateTime(TimeOnly.MinValue)
        && End > date.ToDateTime(TimeOnly.MinValue);
}

public sealed record MoneyEntry(string Id, string Description, EntryKind Kind, DateOnly Date, long Cents, string? AppointmentId)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Description)) throw new ArgumentException("Informe a descrição do lançamento.");
        if (Cents <= 0) throw new ArgumentException("O valor deve ser maior que zero.");
        if (!Enum.IsDefined(Kind)) throw new ArgumentException("Tipo de lançamento inválido.");
    }
}

public sealed record FinancialBucket(string Label, long Income, long Expense)
{
    public long Profit => Income - Expense;
}
public sealed record FinancialSummary(long Income, long Expense, IReadOnlyList<FinancialBucket> Buckets)
{
    public long Profit => Income - Expense;
}

public static class Finance
{
    public static readonly CultureInfo Brazilian = CultureInfo.GetCultureInfo("pt-BR");
    public static string Currency(long cents) => (cents / 100m).ToString("C", Brazilian);
    public static long ParseCents(string text)
    {
        if (!decimal.TryParse(text, NumberStyles.Number, Brazilian, out var value) || value < 0 || decimal.Round(value, 2) != value)
            throw new ArgumentException("Use um valor em reais com até duas casas decimais, por exemplo 150,00.");
        return checked((long)(value * 100));
    }
    public static FinancialSummary Summarize(IEnumerable<MoneyEntry> entries, DateOnly selected, Period period)
    {
        var start = period switch { Period.Dia => selected, Period.Mes => new(selected.Year, selected.Month, 1), _ => new DateOnly(selected.Year, 1, 1) };
        var end = period switch { Period.Dia => start.AddDays(1), Period.Mes => start.AddMonths(1), _ => start.AddYears(1) };
        var rows = entries.Where(e => e.Date >= start && e.Date < end).ToArray();
        var buckets = new List<FinancialBucket>();
        var count = period switch { Period.Dia => 1, Period.Mes => DateTime.DaysInMonth(start.Year, start.Month), _ => 12 };
        for (int i = 0; i < count; i++)
        {
            var day = period == Period.Ano ? start.AddMonths(i) : start.AddDays(i);
            var group = rows.Where(e => period == Period.Ano ? e.Date.Month == day.Month : e.Date == day);
            buckets.Add(new(period == Period.Ano ? day.ToString("MMM", Brazilian) : day.Day.ToString(),
                group.Where(e => e.Kind == EntryKind.Receita).Sum(e => e.Cents), group.Where(e => e.Kind == EntryKind.Despesa).Sum(e => e.Cents)));
        }
        return new(rows.Where(e => e.Kind == EntryKind.Receita).Sum(e => e.Cents), rows.Where(e => e.Kind == EntryKind.Despesa).Sum(e => e.Cents), buckets);
    }
}
