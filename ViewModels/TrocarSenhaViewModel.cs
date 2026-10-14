using System.ComponentModel.DataAnnotations;

namespace PortalSanMarco.ViewModels;

public class TrocarSenhaViewModel
{
    [Required(ErrorMessage = "Informe a senha atual.")]
    [DataType(DataType.Password)]
    [Display(Name = "Senha atual")]
    public string SenhaAtual { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe a nova senha.")]
    [DataType(DataType.Password)]
    [MaxLength(128)]
    [Display(Name = "Nova senha")]
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
    [Display(Name = "Confirmar nova senha")]
    [MaxLength(128)]
    [Compare(
        nameof(NovaSenha),
        ErrorMessage = "As senhas não coincidem.")]
    public string ConfirmacaoSenha { get; set; } = string.Empty;
}
