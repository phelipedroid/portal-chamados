using System.ComponentModel.DataAnnotations;

namespace PortalSanMarco.ViewModels;

public class SetorFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Informe o nome do setor.")]
    [MaxLength(
        100,
        ErrorMessage = "O nome deve possuir no máximo 100 caracteres.")]
    [Display(Name = "Nome do setor")]
    public string Nome { get; set; } = string.Empty;

    [Display(Name = "Setor ativo")]
    public bool Ativo { get; set; } = true;
}
