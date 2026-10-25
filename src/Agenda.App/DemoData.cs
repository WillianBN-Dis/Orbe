using Agenda.Core;

namespace Agenda.App;

internal static class DemoData
{
    // Somente memória: não consulta, cria ou altera bancos e credenciais.
    public static (IReadOnlyList<Stored<Appointment>>,IReadOnlyList<Stored<MoneyEntry>>) Create(DateOnly selected)
    {
        var appointments=new List<Stored<Appointment>>();var entries=new List<Stored<MoneyEntry>>();
        var empty=new RowSnapshot([],[]);var month=new DateOnly(selected.Year,selected.Month,1);
        for(int d=1;d<=DateTime.DaysInMonth(selected.Year,selected.Month);d++)
        {
            var date=month.AddDays(d-1);
            if(date.DayOfWeek==DayOfWeek.Sunday)continue;
            entries.Add(new(new("demo-receita-"+d,"Atendimentos do dia",EntryKind.Receita,date,18000+(d*791)%35000,null),empty));
            if(d%3==0)entries.Add(new(new("demo-despesa-"+d,"Materiais e despesas",EntryKind.Despesa,date,d==12?45000:5000+(d*193)%9000,null),empty));
            if(d!=selected.Day&&d%3==0)appointments.Add(new(new("demo-evento-"+d,"Atendimento reservado",EventKind.Atendimento,date.ToDateTime(new(10,0)),date.ToDateTime(new(11,0)),"Cliente de exemplo",null,18000),empty));
        }
        var start=selected.ToDateTime(new(9,0));
        appointments.Add(new(new("demo-1","Atendimento individual",EventKind.Atendimento,start,start.AddHours(1),"Marina · exemplo","Primeiro atendimento do dia",18000),empty));
        appointments.Add(new(new("demo-2","Planejamento da semana",EventKind.Compromisso,start.AddHours(2),start.AddHours(3),null,null,0),empty));
        appointments.Add(new(new("demo-3","Pausa para recarregar",EventKind.Folga,start.AddHours(4),start.AddHours(5),null,null,0),empty));
        appointments.Add(new(new("demo-4","Consultoria de acompanhamento",EventKind.Atendimento,start.AddHours(6),start.AddHours(7),"Rafael · exemplo",null,24000),empty));
        return(appointments,entries);
    }
}

