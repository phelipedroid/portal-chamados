using System.ComponentModel.DataAnnotations;

namespace PortalSanMarco.Models;

public class Setor
{
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Nome { get; set; } = string.Empty;

    public bool Ativo { get; set; } = true;

    public ICollection<FuncionarioPerfil> Funcionarios { get; set; }
        = new List<FuncionarioPerfil>();
}