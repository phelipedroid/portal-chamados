using System.ComponentModel.DataAnnotations;

namespace PortalSanMarco.ViewModels;

public class EsqueciSenhaViewModel
{
    [Required(ErrorMessage = "Informe o e-mail.")]
    [EmailAddress(ErrorMessage = "Informe um e-mail válido.")]
    [MaxLength(256)]
    [Display(Name = "E-mail corporativo")]
    public string Email { get; set; } = string.Empty;
}
