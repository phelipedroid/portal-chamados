using PortalSanMarco.Models;

namespace PortalSanMarco.Services;

public interface IAuditoriaService
{
    Task RegistrarAsync(
        string acao,
        string entidade,
        string? entidadeId,
        string descricao,
        CancellationToken cancellationToken = default);

    Task TraduzirReferenciasOrganizacionaisAsync(
        IEnumerable<AuditoriaAdministrativa> registros,
        CancellationToken cancellationToken = default);
}
