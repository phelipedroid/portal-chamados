namespace PortalSanMarco.Services;

public interface IChamadoEmailNotificationService
{
    Task EnfileirarAsync(
        int chamadoId,
        string autorUsuarioId,
        string evento,
        string descricao,
        bool incluirAutor = false,
        CancellationToken cancellationToken = default);
}
