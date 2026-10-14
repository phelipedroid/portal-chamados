using System.ComponentModel.DataAnnotations;

namespace PortalSanMarco.ViewModels;

public sealed class CadastroPendenteListaViewModel
{
    public string UsuarioId { get; set; } = string.Empty;
    public string NomeCompleto { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Unidade { get; set; } = string.Empty;
    public string Setor { get; set; } = string.Empty;
    public string? Cargo { get; set; }
    public DateTime SolicitadoEm { get; set; }
}

public sealed class RevisarCadastroPendenteViewModel
{
    [Required]
    public string UsuarioId { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe o nome completo.")]
    [MaxLength(150)]
    [Display(Name = "Nome completo")]
    public string NomeCompleto { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe o e-mail.")]
    [EmailAddress(ErrorMessage = "Informe um e-mail válido.")]
    [MaxLength(256)]
    [Display(Name = "E-mail corporativo")]
    public string Email { get; set; } = string.Empty;

    [Range(1, int.MaxValue, ErrorMessage = "Selecione a unidade.")]
    [Display(Name = "Unidade")]
    public int UnidadeId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Selecione o setor.")]
    [Display(Name = "Setor")]
    public int SetorId { get; set; }

    [MaxLength(100)]
    public string? Cargo { get; set; }
}
