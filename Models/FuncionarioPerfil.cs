using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace PortalSanMarco.Models;

public class FuncionarioPerfil
{
    public int Id { get; set; }

    [Required]
    public string UsuarioId { get; set; } = string.Empty;

    public IdentityUser Usuario { get; set; } = null!;

    [Required]
    [MaxLength(150)]
    public string NomeCompleto { get; set; } = string.Empty;

    public int UnidadeId { get; set; }

    public Unidade Unidade { get; set; } = null!;

    public int SetorId { get; set; }

    public Setor Setor { get; set; } = null!;

    [MaxLength(100)]
    public string? Cargo { get; set; }

    public bool Ativo { get; set; } = true;

    public bool CadastroPendente { get; set; }

    public bool DeveTrocarSenha { get; set; } = true;

    public DateTime CriadoEm { get; set; } = DateTime.Now;
}
