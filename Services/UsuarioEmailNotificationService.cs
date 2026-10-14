using System.Net;
using PortalSanMarco.Data;
using PortalSanMarco.Models;

namespace PortalSanMarco.Services;

public sealed class UsuarioEmailNotificationService
    : IUsuarioEmailNotificationService
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;

    public UsuarioEmailNotificationService(
        ApplicationDbContext context,
        IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    public void EnfileirarCadastroAprovado(
        string destinatario,
        string nomeCompleto)
    {
        var baseUrl = _configuration["Portal:BaseUrl"]?.TrimEnd('/');

        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new InvalidOperationException(
                "Endereço público do portal não configurado.");
        }

        var nomeSeguro = WebUtility.HtmlEncode(nomeCompleto);
        var linkSeguro = WebUtility.HtmlEncode(baseUrl + "/Conta/Entrar");

        _context.EmailsNotificacoes.Add(
            new EmailNotificacao
            {
                Destinatario = destinatario,
                Assunto = "Cadastro aprovado - Portal San Marco",
                ConteudoHtml = $$"""
                    <!doctype html>
                    <html lang="pt-BR">
                    <body style="margin:0;background:#f4f7f5;font-family:Arial,sans-serif;color:#24352d">
                      <div style="max-width:600px;margin:32px auto;background:#fff;border-radius:14px;overflow:hidden;border:1px solid #dfe9e3">
                        <div style="padding:24px 30px;background:#087443;color:#fff">
                          <div style="font-size:13px;text-transform:uppercase;letter-spacing:.08em;opacity:.8">Grupo San Marco</div>
                          <h1 style="margin:8px 0 0;font-size:24px">Cadastro aprovado</h1>
                        </div>
                        <div style="padding:30px">
                          <p>Olá, {{nomeSeguro}}.</p>
                          <p>Seu cadastro no Portal de Chamados San Marco foi aprovado. Você já pode entrar usando o e-mail e a senha criados na solicitação.</p>
                          <p style="margin:28px 0">
                            <a href="{{linkSeguro}}" style="display:inline-block;padding:13px 22px;background:#198754;color:#fff;text-decoration:none;border-radius:8px;font-weight:bold">Acessar o portal</a>
                          </p>
                          <p style="font-size:14px;color:#65756d">Esta é a única mensagem enviada pelo processo de cadastro.</p>
                        </div>
                      </div>
                    </body>
                    </html>
                    """,
                CriadoEm = DateTime.Now,
                ProximaTentativaEm = DateTime.Now
            });
    }
}
