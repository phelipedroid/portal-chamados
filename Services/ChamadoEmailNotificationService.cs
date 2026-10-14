using System.Net;
using Microsoft.EntityFrameworkCore;
using PortalSanMarco.Data;
using PortalSanMarco.Models;

namespace PortalSanMarco.Services;

public class ChamadoEmailNotificationService
    : IChamadoEmailNotificationService
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;

    public ChamadoEmailNotificationService(
        ApplicationDbContext context,
        IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    public async Task EnfileirarAsync(
        int chamadoId,
        string autorUsuarioId,
        string evento,
        string descricao,
        bool incluirAutor = false,
        CancellationToken cancellationToken = default)
    {
        var chamado = await _context.Chamados
            .Include(item => item.Solicitante)
            .Include(item => item.TecnicoResponsavel)
            .SingleOrDefaultAsync(
                item => item.Id == chamadoId,
                cancellationToken);

        if (chamado is null)
        {
            return;
        }

        var destinatarios = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase);

        AdicionarDestinatario(
            destinatarios,
            chamado.SolicitanteId,
            chamado.Solicitante.Email,
            autorUsuarioId,
            incluirAutor);

        if (chamado.TecnicoResponsavel is not null &&
            chamado.TecnicoResponsavelId is not null)
        {
            AdicionarDestinatario(
                destinatarios,
                chamado.TecnicoResponsavelId,
                chamado.TecnicoResponsavel.Email,
                autorUsuarioId,
                incluirAutor);
        }

        if (destinatarios.Count == 0)
        {
            return;
        }

        var assunto =
            $"[Chamado #{chamado.Id:D6}] {evento}";
        var conteudoHtml = MontarConteudo(
            chamado,
            evento,
            descricao);
        var agora = DateTime.Now;

        foreach (var destinatario in destinatarios.Keys)
        {
            _context.Set<EmailNotificacao>().Add(
                new EmailNotificacao
                {
                    ChamadoId = chamado.Id,
                    Destinatario = destinatario,
                    Assunto = assunto,
                    ConteudoHtml = conteudoHtml,
                    CriadoEm = agora,
                    ProximaTentativaEm = agora
                });
        }
    }

    private static void AdicionarDestinatario(
        IDictionary<string, string> destinatarios,
        string usuarioId,
        string? email,
        string autorUsuarioId,
        bool incluirAutor)
    {
        if (string.IsNullOrWhiteSpace(email) ||
            (!incluirAutor && usuarioId == autorUsuarioId))
        {
            return;
        }

        destinatarios[email] = usuarioId;
    }

    private string MontarConteudo(
        Chamado chamado,
        string evento,
        string descricao)
    {
        var baseUrl = _configuration["Portal:BaseUrl"]
            ?.TrimEnd('/');

        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new InvalidOperationException(
                "Endereço do portal não configurado.");
        }

        var link =
            $"{baseUrl}/Chamados/Details/{chamado.Id}";

        return $$"""
            <!doctype html>
            <html lang="pt-BR">
            <body style="margin:0;background:#f4f7f5;font-family:Arial,sans-serif;color:#1f2937">
              <div style="max-width:640px;margin:0 auto;padding:28px 16px">
                <div style="background:#0f7a4b;color:#fff;padding:22px 26px;border-radius:14px 14px 0 0">
                  <div style="font-size:14px;opacity:.85">Portal San Marco</div>
                  <div style="font-size:24px;font-weight:700;margin-top:4px">Chamado #{{chamado.Id:D6}}</div>
                </div>
                <div style="background:#fff;padding:26px;border:1px solid #e5e7eb;border-top:0;border-radius:0 0 14px 14px">
                  <h2 style="margin:0 0 14px;font-size:21px">{{WebUtility.HtmlEncode(evento)}}</h2>
                  <p style="margin:0 0 18px;line-height:1.55">{{WebUtility.HtmlEncode(descricao)}}</p>
                  <div style="background:#f7faf8;border-radius:10px;padding:16px;margin-bottom:22px">
                    <strong>{{WebUtility.HtmlEncode(chamado.Titulo)}}</strong><br>
                    <span style="color:#4b5563">Status: {{WebUtility.HtmlEncode(ObterNomeStatus(chamado.Status))}}</span>
                  </div>
                  <a href="{{WebUtility.HtmlEncode(link)}}" style="display:inline-block;background:#168b57;color:#fff;text-decoration:none;font-weight:700;padding:13px 20px;border-radius:8px">Acompanhar chamado</a>
                  <p style="font-size:12px;color:#6b7280;margin:22px 0 0">O link funciona quando você está conectado à rede interna da empresa.</p>
                </div>
              </div>
            </body>
            </html>
            """;
    }

    private static string ObterNomeStatus(
        StatusChamado status)
    {
        return status switch
        {
            StatusChamado.Aberto => "Aberto",
            StatusChamado.EmAndamento => "Em andamento",
            StatusChamado.AguardandoFuncionario =>
                "Aguardando funcionário",
            StatusChamado.AguardandoFornecedor =>
                "Aguardando fornecedor",
            StatusChamado.Resolvido => "Resolvido",
            StatusChamado.Fechado => "Fechado",
            _ => status.ToString()
        };
    }
}
