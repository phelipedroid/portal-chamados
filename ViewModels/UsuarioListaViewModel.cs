namespace PortalSanMarco.ViewModels;

public class UsuarioListaViewModel
{
    public string UsuarioId { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string NomeCompleto { get; set; } = string.Empty;

    public string Unidade { get; set; } = "Não vinculada";

    public string? UnidadesGerenciadas { get; set; }

    public string Setor { get; set; } = "Não vinculado";

    public string Perfis { get; set; } = string.Empty;

    public bool Ativo { get; set; }
}
