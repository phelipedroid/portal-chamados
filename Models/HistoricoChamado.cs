using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace PortalSanMarco.Models;

public class HistoricoChamado
{
    public int Id { get; set; }

    public int ChamadoId { get; set; }

    public Chamado Chamado { get; set; } = null!;

    [Required]
    public string UsuarioId { get; set; } = string.Empty;

    public IdentityUser Usuario { get; set; } = null!;

    [Required]
    [MaxLength(100)]
    public string Acao { get; set; } = string.Empty;

    [Required]
    [MaxLength(1000)]
    public string Descricao { get; set; } = string.Empty;

    public DateTime CriadoEm { get; set; } = DateTime.Now;
}