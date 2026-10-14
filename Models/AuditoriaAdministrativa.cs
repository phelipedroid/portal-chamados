using System.ComponentModel.DataAnnotations;

namespace PortalSanMarco.Models;

public class AuditoriaAdministrativa
{
    public long Id { get; set; }

    [Required]
    [MaxLength(450)]
    public string UsuarioId { get; set; } = string.Empty;

    [Required]
    [MaxLength(256)]
    public string UsuarioEmail { get; set; } = string.Empty;

    [Required]
    [MaxLength(80)]
    public string Acao { get; set; } = string.Empty;

    [Required]
    [MaxLength(80)]
    public string Entidade { get; set; } = string.Empty;

    [MaxLength(450)]
    public string? EntidadeId { get; set; }

    [Required]
    [MaxLength(2000)]
    public string Descricao { get; set; } = string.Empty;

    [MaxLength(64)]
    public string? EnderecoIp { get; set; }

    public DateTime CriadoEm { get; set; } = DateTime.Now;
}
