using System.ComponentModel.DataAnnotations;

namespace PortalSanMarco.Models;

public class CategoriaChamado
{
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Nome { get; set; } = string.Empty;

    public bool Ativa { get; set; } = true;

    public ICollection<Chamado> Chamados { get; set; }
        = new List<Chamado>();
}