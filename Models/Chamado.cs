using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace PortalSanMarco.Models;

public class Chamado
{
    public int Id { get; set; }

    [Required]
    [MaxLength(150)]
    public string Titulo { get; set; } = string.Empty;

    [Required]
    [MaxLength(4000)]
    public string Descricao { get; set; } = string.Empty;

    public int CategoriaId { get; set; }

    public CategoriaChamado Categoria { get; set; } = null!;

    public PrioridadeChamado Prioridade { get; set; }

    public int UnidadeId { get; set; }

    public Unidade Unidade { get; set; } = null!;

    public int SetorId { get; set; }

    public Setor Setor { get; set; } = null!;

    [MaxLength(100)]
    public string? ComputadorPatrimonio { get; set; }

    [Required]
    public string SolicitanteId { get; set; } = string.Empty;

    public IdentityUser Solicitante { get; set; } = null!;

    public string? TecnicoResponsavelId { get; set; }

    public IdentityUser? TecnicoResponsavel { get; set; }

    public StatusChamado Status { get; set; }
        = StatusChamado.Aberto;

    public DateTime CriadoEm { get; set; }
        = DateTime.Now;

    public DateTime? PrazoEm { get; set; }

    public DateTime? SlaPausadoEm { get; set; }

    public int SlaMinutosPausados { get; set; }

    public DateTime? AssumidoEm { get; set; }

    public DateTime? ResolvidoEm { get; set; }

    public int? TempoGastoMinutos { get; set; }

    [MaxLength(4000)]
    public string? SolucaoAplicada { get; set; }
}
