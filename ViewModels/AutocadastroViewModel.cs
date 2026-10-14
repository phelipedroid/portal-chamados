using System.ComponentModel.DataAnnotations;

namespace PortalSanMarco.ViewModels;

public sealed class AutocadastroViewModel
{
    [Required(ErrorMessage = "Informe seu nome completo.")]
    [MaxLength(150)]
    [Display(Name = "Nome completo")]
    public string NomeCompleto { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe seu e-mail corporativo.")]
    [EmailAddress(ErrorMessage = "Informe um e-mail válido.")]
    [MaxLength(256)]
    [Display(Name = "E-mail corporativo")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Crie uma senha.")]
    [DataType(DataType.Password)]
    [Display(Name = "Senha")]
    [StringLength(128, MinimumLength = 8,
        ErrorMessage = "A senha deve ter entre 8 e 128 caracteres.")]
    [RegularExpression(
        @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^a-zA-Z\d]).+$",
        ErrorMessage = "Use letra maiúscula, minúscula, número e caractere especial.")]
    public string Senha { get; set; } = string.Empty;

    [Required(ErrorMessage = "Confirme a senha.")]
    [DataType(DataType.Password)]
    [Display(Name = "Confirmar senha")]
    [Compare(nameof(Senha), ErrorMessage = "As senhas não coincidem.")]
    public string ConfirmarSenha { get; set; } = string.Empty;

    [Range(1, int.MaxValue, ErrorMessage = "Selecione sua unidade.")]
    [Display(Name = "Unidade")]
    public int UnidadeId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Selecione seu setor.")]
    [Display(Name = "Setor")]
    public int SetorId { get; set; }

    [MaxLength(100)]
    public string? Cargo { get; set; }
}
