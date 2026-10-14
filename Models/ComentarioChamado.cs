using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace PortalSanMarco.Models;

public class ComentarioChamado
{
    public int Id { get; set; }

    public int ChamadoId { get; set; }

    public Chamado Chamado { get; set; } = null!;

    [Required]
    public string AutorId { get; set; } = string.Empty;

    public IdentityUser Autor { get; set; } = null!;

    [Required]
    [MaxLength(2000)]
    public string Mensagem { get; set; } = string.Empty;

    public DateTime CriadoEm { get; set; } = DateTime.Now;
}