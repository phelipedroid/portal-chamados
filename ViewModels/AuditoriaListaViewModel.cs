using PortalSanMarco.Models;

namespace PortalSanMarco.ViewModels;

public class AuditoriaListaViewModel
{
    public List<AuditoriaAdministrativa> Registros { get; set; } = [];
    public string? Busca { get; set; }
    public string? Entidade { get; set; }
    public DateTime? DataInicial { get; set; }
    public DateTime? DataFinal { get; set; }
    public int PaginaAtual { get; set; }
    public int TotalPaginas { get; set; }
    public int TotalItens { get; set; }
}
