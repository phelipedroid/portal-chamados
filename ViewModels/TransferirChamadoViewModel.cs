using System.ComponentModel.DataAnnotations;

namespace PortalSanMarco.ViewModels;

public class TransferirChamadoViewModel
{
    [Range(1, int.MaxValue)]
    public int ChamadoId { get; set; }

    [Required(
        ErrorMessage =
            "Selecione o novo responsável.")]
    [Display(
        Name =
            "Novo responsável")]
    public string TecnicoId { get; set; } =
        string.Empty;
}
