using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography.X509Certificates;
using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Options;

namespace PortalSanMarco.Services;

public class GraphEmailSender : IPortalEmailSender
{
    private static readonly string[] Escopos =
    {
        "https://graph.microsoft.com/.default"
    };

    private readonly HttpClient _httpClient;
    private readonly GraphMailOptions _options;

    public GraphEmailSender(
        HttpClient httpClient,
        IOptions<GraphMailOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task EnviarAsync(
        string destinatario,
        string assunto,
        string conteudoHtml,
        CancellationToken cancellationToken = default)
    {
        using var certificado = CarregarCertificado();

        var credencial =
            new ClientCertificateCredential(
                _options.TenantId,
                _options.ClientId,
                certificado);

        var token =
            await credencial.GetTokenAsync(
                new TokenRequestContext(Escopos),
                cancellationToken);

        var remetente =
            Uri.EscapeDataString(
                _options.SenderAddress);

        using var requisicao =
            new HttpRequestMessage(
                HttpMethod.Post,
                "https://graph.microsoft.com/v1.0/users/" +
                $"{remetente}/sendMail");

        requisicao.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                token.Token);

        requisicao.Content = JsonContent.Create(
            new
            {
                message = new
                {
                    subject = assunto,
                    body = new
                    {
                        contentType = "HTML",
                        content = conteudoHtml
                    },
                    from = new
                    {
                        emailAddress = new
                        {
                            name = _options.SenderName,
                            address = _options.SenderAddress
                        }
                    },
                    toRecipients = new[]
                    {
                        new
                        {
                            emailAddress = new
                            {
                                address = destinatario
                            }
                        }
                    }
                },
                saveToSentItems = true
            });

        using var resposta =
            await _httpClient.SendAsync(
                requisicao,
                cancellationToken);

        if (!resposta.IsSuccessStatusCode)
        {
            var detalhe =
                await resposta.Content.ReadAsStringAsync(
                    cancellationToken);

            throw new InvalidOperationException(
                "O Microsoft Graph recusou o envio " +
                $"({(int)resposta.StatusCode}): {detalhe}");
        }
    }

    private X509Certificate2 CarregarCertificado()
    {
        using var loja =
            new X509Store(
                StoreName.My,
                StoreLocation.LocalMachine);

        loja.Open(OpenFlags.ReadOnly);

        var encontrados =
            loja.Certificates.Find(
                X509FindType.FindByThumbprint,
                _options.CertificateThumbprint,
                validOnly: false);

        var agora = DateTime.Now;

        var certificado =
            encontrados
                .OfType<X509Certificate2>()
                .FirstOrDefault(item =>
                    item.HasPrivateKey &&
                    item.NotBefore <= agora &&
                    item.NotAfter > agora);

        if (certificado is null)
        {
            throw new InvalidOperationException(
                "Certificado do Microsoft Graph não encontrado " +
                "ou sem acesso à chave privada.");
        }

        return new X509Certificate2(certificado);
    }
}
