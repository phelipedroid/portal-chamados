using PortalSanMarco.Models;

namespace PortalSanMarco.Services;

public interface ISlaService
{
    DateTime CalcularPrazo(
        DateTime criadoEm,
        PrioridadeChamado prioridade);

    void AplicarMudancaStatus(
        Chamado chamado,
        StatusChamado statusAnterior,
        StatusChamado novoStatus,
        DateTime agora);

    void RecalcularPorPrioridade(
        Chamado chamado,
        PrioridadeChamado prioridadeAnterior,
        DateTime agora);

    int ContarMinutosUteis(DateTime inicio, DateTime fim);
}
