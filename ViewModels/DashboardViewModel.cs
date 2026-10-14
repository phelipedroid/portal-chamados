namespace PortalSanMarco.ViewModels;

public class DashboardViewModel
{
    public DateTime? DataInicial { get; set; }

    public DateTime? DataFinal { get; set; }

    public int TotalChamados { get; set; }

    public int TotalAbertos { get; set; }

    public int TotalEmAndamento { get; set; }

    public int TotalAguardando { get; set; }

    public int TotalFechados { get; set; }

    public int TotalUrgentes { get; set; }

    public int TotalSemResponsavel { get; set; }

    public double? TempoMedioAtendimentoMinutos { get; set; }

    public double? TempoMedioTrabalhadoMinutos { get; set; }

    public List<IndicadorDashboardViewModel>
        ChamadosPorCategoria { get; set; } = [];

    public List<IndicadorDashboardViewModel>
        ChamadosPorSetor { get; set; } = [];

    public List<IndicadorDashboardViewModel>
        ChamadosPorTecnico { get; set; } = [];
}

public class IndicadorDashboardViewModel
{
    public string Nome { get; set; } = string.Empty;

    public int Quantidade { get; set; }
}
