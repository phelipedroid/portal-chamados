using System.ComponentModel.DataAnnotations;

namespace PortalSanMarco.Models;

public class EmailNotificacao
{
    public long Id { get; set; }

    public int? ChamadoId { get; set; }

    [Required]
    [MaxLength(256)]
    public string Destinatario { get; set; } = string.Empty;

    [Required]
    [MaxLength(250)]
    public string Assunto { get; set; } = string.Empty;

    [Required]
    public string ConteudoHtml { get; set; } = string.Empty;

    public DateTime CriadoEm { get; set; } = DateTime.Now;

    public DateTime ProximaTentativaEm { get; set; } = DateTime.Now;

    public int Tentativas { get; set; }

    public DateTime? EnviadoEm { get; set; }

    [MaxLength(2000)]
    public string? UltimoErro { get; set; }
}
