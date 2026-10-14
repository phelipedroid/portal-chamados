namespace PortalSanMarco.Services;

public interface IUsuarioEmailNotificationService
{
    void EnfileirarCadastroAprovado(
        string destinatario,
        string nomeCompleto);
}
