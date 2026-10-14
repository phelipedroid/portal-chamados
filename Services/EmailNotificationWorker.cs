using Microsoft.EntityFrameworkCore;
using PortalSanMarco.Data;
using PortalSanMarco.Models;

namespace PortalSanMarco.Services;

public sealed class EmailNotificationWorker : BackgroundService
{
    private const int MaximoTentativas = 8;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EmailNotificationWorker> _logger;

    public EmailNotificationWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<EmailNotificationWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        await Task.Delay(
            TimeSpan.FromSeconds(5),
            stoppingToken);

        using var timer = new PeriodicTimer(
            TimeSpan.FromSeconds(20));

        do
        {
            await ProcessarPendentesAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task ProcessarPendentesAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider
                .GetRequiredService<ApplicationDbContext>();
            var emailSender = scope.ServiceProvider
                .GetRequiredService<IPortalEmailSender>();
            var agora = DateTime.Now;

            var pendentes = await context
                .Set<EmailNotificacao>()
                .Where(item =>
                    item.EnviadoEm == null &&
                    item.Tentativas < MaximoTentativas &&
                    item.ProximaTentativaEm <= agora)
                .OrderBy(item => item.CriadoEm)
                .Take(10)
                .ToListAsync(cancellationToken);

            foreach (var notificacao in pendentes)
            {
                try
                {
                    await emailSender.EnviarAsync(
                        notificacao.Destinatario,
                        notificacao.Assunto,
                        notificacao.ConteudoHtml,
                        cancellationToken);

                    notificacao.EnviadoEm = DateTime.Now;
                    notificacao.UltimoErro = null;
                }
                catch (Exception excecao)
                    when (!cancellationToken.IsCancellationRequested)
                {
                    notificacao.Tentativas++;
                    notificacao.UltimoErro = LimitarErro(
                        excecao.Message);
                    notificacao.ProximaTentativaEm =
                        DateTime.Now.Add(
                            ObterIntervalo(notificacao.Tentativas));

                    _logger.LogWarning(
                        excecao,
                        "Falha ao enviar notificação {NotificacaoId} do chamado {ChamadoId}. Tentativa {Tentativa} de {MaximoTentativas}.",
                        notificacao.Id,
                        notificacao.ChamadoId,
                        notificacao.Tentativas,
                        MaximoTentativas);
                }

                await context.SaveChangesAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            // Encerramento normal do serviço.
        }
        catch (Exception excecao)
        {
            _logger.LogError(
                excecao,
                "Falha ao processar a fila de notificações por e-mail.");
        }
    }

    private static TimeSpan ObterIntervalo(int tentativa)
    {
        var minutos = tentativa switch
        {
            1 => 1,
            2 => 5,
            3 => 15,
            4 => 60,
            5 => 180,
            _ => 360
        };

        return TimeSpan.FromMinutes(minutos);
    }

    private static string LimitarErro(string mensagem)
    {
        const int tamanhoMaximo = 2000;

        return mensagem.Length <= tamanhoMaximo
            ? mensagem
            : mensagem[..tamanhoMaximo];
    }
}
