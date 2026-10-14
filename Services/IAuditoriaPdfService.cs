using PortalSanMarco.Models;

namespace PortalSanMarco.Services;

public interface IAuditoriaPdfService
{
    byte[] Gerar(
        IReadOnlyCollection<AuditoriaAdministrativa> registros,
        DateTime dataInicial,
        DateTime dataFinal,
        string? busca,
        string? entidade,
        string geradoPor);
}
