using Microsoft.AspNetCore.Identity;

namespace PortalSanMarco.Models;

public class GerenteUnidade
{
    public string UsuarioId { get; set; } = string.Empty;

    public IdentityUser Usuario { get; set; } = null!;

    public int UnidadeId { get; set; }

    public Unidade Unidade { get; set; } = null!;
}
