using System.ComponentModel.DataAnnotations;

namespace PortalSanMarco.ViewModels;

public class CadastrarFuncionarioViewModel
{
    [Required(ErrorMessage = "Informe o nome completo.")]
    [MaxLength(150)]
    [Display(Name = "Nome completo")]
    public string NomeCompleto { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe o e-mail.")]
    [EmailAddress(ErrorMessage = "Informe um e-mail válido.")]
    [MaxLength(256)]
    [Display(Name = "E-mail")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe uma senha provisória.")]
    [DataType(DataType.Password)]
    [Display(Name = "Senha provisória")]
    [StringLength(
        128,
        MinimumLength = 8,
        ErrorMessage = "A senha deve ter entre 8 e 128 caracteres.")]
    [RegularExpression(
        @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^a-zA-Z\d]).+$",
        ErrorMessage = "Use letra maiúscula, minúscula, número e caractere especial.")]
    public string SenhaProvisoria { get; set; } = string.Empty;

    [Range(1, int.MaxValue, ErrorMessage = "Selecione a unidade.")]
    [Display(Name = "Unidade")]
    public int UnidadeId { get; set; }

    [Display(Name = "Unidades visualizadas pelo gerente")]
    public List<int> UnidadesGerenciadasIds { get; set; } = [];

    [Range(1, int.MaxValue, ErrorMessage = "Selecione o setor.")]
    [Display(Name = "Setor")]
    public int SetorId { get; set; }

    [MaxLength(100)]
    public string? Cargo { get; set; }

    [Required(ErrorMessage = "Selecione o perfil.")]
    [Display(Name = "Perfil de acesso")]
    public string Perfil { get; set; } = "Funcionario";
}
