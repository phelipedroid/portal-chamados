using Microsoft.AspNetCore.Identity;

namespace PortalSanMarco.Models;

public class ChamadoVisualizacao
{
    public string UsuarioId { get; set; } = string.Empty;

    public IdentityUser Usuario { get; set; } = null!;

    public int ChamadoId { get; set; }

    public Chamado Chamado { get; set; } = null!;

    public DateTime VisualizadoEm { get; set; } = DateTime.Now;
}
