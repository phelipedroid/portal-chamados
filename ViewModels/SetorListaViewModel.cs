namespace PortalSanMarco.ViewModels;

public class SetorListaViewModel
{
    public int Id { get; set; }

    public string Nome { get; set; } = string.Empty;

    public bool Ativo { get; set; }

    public int TotalFuncionarios { get; set; }

    public int TotalChamados { get; set; }

    public bool PodeExcluir =>
        TotalFuncionarios == 0 &&
        TotalChamados == 0;
}
