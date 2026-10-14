using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace PortalSanMarco.Models;

public class AnexoChamado
{
    public int Id { get; set; }

    public int ChamadoId { get; set; }

    public Chamado Chamado { get; set; } = null!;

    [Required]
    [MaxLength(255)]
    public string NomeOriginal { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string TipoConteudo { get; set; } = string.Empty;

    public long TamanhoBytes { get; set; }

    [Required]
    public byte[] Conteudo { get; set; } = [];

    [Required]
    public string AutorId { get; set; } = string.Empty;

    public IdentityUser Autor { get; set; } = null!;

    public DateTime CriadoEm { get; set; } = DateTime.Now;
}