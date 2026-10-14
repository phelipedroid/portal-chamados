using System.ComponentModel.DataAnnotations;

namespace PortalSanMarco.Services;

public class SlaOptions
{
    [Range(0, 23)]
    public int InicioHora { get; set; } = 8;

    [Range(0, 59)]
    public int InicioMinuto { get; set; }

    [Range(0, 23)]
    public int FimHora { get; set; } = 18;

    [Range(0, 59)]
    public int FimMinuto { get; set; }

    public List<DateOnly> Feriados { get; set; } = [];
}
