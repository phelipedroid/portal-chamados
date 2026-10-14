namespace PortalSanMarco.Models;

public enum PrioridadeChamado
{
    Leve = 1,

    [System.ComponentModel.DataAnnotations.Display(Name = "Média")]
    Media = 2,

    Urgente = 3
}

public enum StatusChamado
{
    Aberto = 1,
    EmAndamento = 2,
    AguardandoFuncionario = 3,
    AguardandoFornecedor = 4,
    Resolvido = 5,
    Fechado = 6
}
