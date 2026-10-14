using System.ComponentModel.DataAnnotations;

namespace PortalSanMarco.ViewModels;

public class ResetarSenhaUsuarioViewModel
{
    [Required]
    public string UsuarioId { get; set; } = string.Empty;

    public string NomeCompleto { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe a nova senha.")]
    [DataType(DataType.Password)]
    [Display(Name = "Nova senha provisória")]
    [StringLength(
        128,
        MinimumLength = 8,
        ErrorMessage = "A senha deve ter entre 8 e 128 caracteres.")]
    [RegularExpression(
        @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^a-zA-Z\d]).+$",
        ErrorMessage = "Use letra maiúscula, minúscula, número e caractere especial.")]
    public string NovaSenha { get; set; } = string.Empty;

    [Required(ErrorMessage = "Confirme a nova senha.")]
    [DataType(DataType.Password)]
    [Compare(
        nameof(NovaSenha),
        ErrorMessage = "As senhas não coincidem.")]
    [Display(Name = "Confirmar nova senha")]
    [MaxLength(128)]
    public string ConfirmacaoSenha { get; set; } = string.Empty;
}
