using System.ComponentModel.DataAnnotations;

namespace PortalSanMarco.ViewModels;

public class EditarUsuarioViewModel
{
    [Required]
    public string UsuarioId { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe o nome completo.")]
    [MaxLength(150)]
    [Display(Name = "Nome completo")]
    public string NomeCompleto { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [MaxLength(256)]
    [Display(Name = "E-mail")]
    public string Email { get; set; } = string.Empty;

    [Range(1, int.MaxValue, ErrorMessage = "Selecione a unidade.")]
    [Display(Name = "Unidade")]
    public int UnidadeId { get; set; }

    [Display(Name = "Unidades visualizadas pelo gerente")]
    public List<int> UnidadesGerenciadasIds { get; set; } = [];

    [Range(1, int.MaxValue, ErrorMessage = "Selecione o setor.")]
    [Display(Name = "Setor")]
    public int SetorId { get; set; }

    [MaxLength(100)]
    [Display(Name = "Cargo")]
    public string? Cargo { get; set; }

    [Required(ErrorMessage = "Selecione o perfil.")]
    [Display(Name = "Perfil de acesso")]
    public string Perfil { get; set; } = "Funcionario";

    [Display(Name = "Usuário ativo")]
    public bool Ativo { get; set; } = true;
}
