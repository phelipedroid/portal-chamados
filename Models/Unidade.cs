using System.ComponentModel.DataAnnotations;

namespace PortalSanMarco.Models;

public class Unidade
{
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Nome { get; set; } = string.Empty;

    public bool Ativa { get; set; } = true;

    public ICollection<FuncionarioPerfil> Funcionarios { get; set; }
        = new List<FuncionarioPerfil>();

    public ICollection<GerenteUnidade> Gerentes { get; set; }
        = new List<GerenteUnidade>();
}
