using System.ComponentModel.DataAnnotations;

namespace PortalSanMarco.ViewModels;

public class AdicionarComentarioViewModel
{
    [Range(1, int.MaxValue)]
    public int ChamadoId { get; set; }

    [Required(ErrorMessage = "Escreva uma mensagem.")]
    [MaxLength(2000)]
    public string Mensagem { get; set; } = string.Empty;
}
