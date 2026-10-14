namespace PortalSanMarco.Services;

public interface IPortalEmailSender
{
    Task EnviarAsync(
        string destinatario,
        string assunto,
        string conteudoHtml,
        CancellationToken cancellationToken = default);
}
