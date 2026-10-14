using System.ComponentModel.DataAnnotations;
using PortalSanMarco.Models;

namespace PortalSanMarco.ViewModels;

public class NovoChamadoViewModel
{
    [Required(ErrorMessage = "Informe o título.")]
    [MaxLength(150)]
    [Display(Name = "Título")]
    public string Titulo { get; set; } = string.Empty;

    [Required(ErrorMessage = "Descreva o problema.")]
    [MaxLength(4000)]
    [Display(Name = "Descrição do problema")]
    public string Descricao { get; set; } = string.Empty;

    [Range(1, int.MaxValue, ErrorMessage = "Selecione a categoria.")]
    [Display(Name = "Categoria")]
    public int CategoriaId { get; set; }

    [EnumDataType(
        typeof(PrioridadeChamado),
        ErrorMessage = "Selecione uma prioridade válida.")]
    public PrioridadeChamado Prioridade { get; set; }
        = PrioridadeChamado.Leve;

    [Range(1, int.MaxValue, ErrorMessage = "Selecione a unidade.")]
    [Display(Name = "Unidade")]
    public int UnidadeId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Selecione o setor.")]
    [Display(Name = "Setor")]
    public int SetorId { get; set; }

    [MaxLength(100)]
    [Display(Name = "Computador ou patrimônio")]
    public string? ComputadorPatrimonio { get; set; }
}
