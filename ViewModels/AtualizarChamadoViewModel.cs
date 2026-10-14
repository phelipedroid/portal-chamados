using System.ComponentModel.DataAnnotations;
using PortalSanMarco.Models;

namespace PortalSanMarco.ViewModels;

public class AtualizarChamadoViewModel
{
    [Range(1, int.MaxValue)]
    public int ChamadoId { get; set; }

    [EnumDataType(
        typeof(StatusChamado),
        ErrorMessage = "Selecione um status válido.")]
    public StatusChamado Status { get; set; }

    [EnumDataType(
        typeof(PrioridadeChamado),
        ErrorMessage = "Selecione uma prioridade válida.")]
    public PrioridadeChamado Prioridade { get; set; }

    [Range(
        1,
        100000,
        ErrorMessage = "Informe um tempo válido.")]
    [Display(Name = "Tempo gasto em minutos")]
    public int? TempoGastoMinutos { get; set; }

    [MaxLength(4000)]
    [Display(Name = "Solução aplicada")]
    public string? SolucaoAplicada { get; set; }
}
