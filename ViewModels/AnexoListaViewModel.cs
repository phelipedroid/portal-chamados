namespace PortalSanMarco.ViewModels;

public class AnexoListaViewModel
{
    public int Id { get; set; }

    public string NomeOriginal { get; set; } = string.Empty;

    public string TipoConteudo { get; set; } = string.Empty;

    public long TamanhoBytes { get; set; }

    public string AutorEmail { get; set; } = string.Empty;

    public DateTime CriadoEm { get; set; }
}