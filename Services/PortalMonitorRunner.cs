using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace PortalSanMarco.Services;

public class PortalMonitorRunner
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IPortalEmailSender _emailSender;
    private readonly PortalMonitorOptions _options;
    private readonly ILogger<PortalMonitorRunner> _logger;

    public PortalMonitorRunner(
        IHttpClientFactory httpClientFactory,
        IPortalEmailSender emailSender,
        IOptions<PortalMonitorOptions> options,
        ILogger<PortalMonitorRunner> logger)
    {
        _httpClientFactory = httpClientFactory;
        _emailSender = emailSender;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<int> ExecutarAsync(
        CancellationToken cancellationToken = default)
    {
        var agora = DateTime.Now;
        var falhas = new List<string>();

        await VerificarPortalAsync(falhas, cancellationToken);
        VerificarBackup(falhas, agora);
        VerificarDiscos(falhas);

        var estadoAnterior = await CarregarEstadoAsync(cancellationToken);
        var estaSaudavel = falhas.Count == 0;
        var deveNotificarFalha = !estaSaudavel &&
            (estadoAnterior.Saudavel ||
             !estadoAnterior.UltimoAlertaEm.HasValue ||
             agora - estadoAnterior.UltimoAlertaEm.Value >=
                TimeSpan.FromHours(_options.RepeatAlertHours));
        var deveNotificarRecuperacao = estaSaudavel &&
            !estadoAnterior.Saudavel;

        var alertaFalhaEnviado = false;

        if (deveNotificarFalha)
        {
            alertaFalhaEnviado = await EnviarAsync(
                "ALERTA: Portal San Marco requer atenção",
                MontarHtmlFalha(falhas, agora),
                cancellationToken);
        }
        else if (deveNotificarRecuperacao)
        {
            await EnviarAsync(
                "RECUPERADO: Portal San Marco normalizado",
                MontarHtmlRecuperacao(agora),
                cancellationToken);
        }

        await SalvarEstadoAsync(
            new MonitorState
            {
                Saudavel = estaSaudavel,
                UltimaVerificacaoEm = agora,
                UltimoAlertaEm = alertaFalhaEnviado
                    ? agora
                    : estadoAnterior.UltimoAlertaEm,
                Falhas = falhas
            },
            cancellationToken);

        if (!estaSaudavel)
        {
            _logger.LogWarning(
                "Monitoramento encontrou falhas: {Falhas}",
                string.Join(" | ", falhas));
        }

        return estaSaudavel ? 0 : 2;
    }

    private async Task VerificarPortalAsync(
        List<string> falhas,
        CancellationToken cancellationToken)
    {
        Exception? ultimaFalha = null;

        for (var tentativa = 1; tentativa <= 3; tentativa++)
        {
            try
            {
                var cliente = _httpClientFactory.CreateClient();
                cliente.Timeout = TimeSpan.FromSeconds(8);

                using var resposta = await cliente.GetAsync(
                    _options.HealthUrl,
                    cancellationToken);

                if (resposta.StatusCode == HttpStatusCode.OK)
                {
                    return;
                }

                ultimaFalha = new InvalidOperationException(
                    $"resposta HTTP {(int)resposta.StatusCode}");
            }
            catch (Exception ex) when (
                ex is not OperationCanceledException ||
                !cancellationToken.IsCancellationRequested)
            {
                ultimaFalha = ex;
            }

            if (tentativa < 3)
            {
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            }
        }

        falhas.Add(
            "Portal ou banco de dados indisponível após 3 tentativas: " +
            ultimaFalha?.GetBaseException().Message);
    }

    private void VerificarBackup(List<string> falhas, DateTime agora)
    {
        try
        {
            var diretorio = new DirectoryInfo(_options.BackupDirectory);
            var ultimo = diretorio.Exists
                ? diretorio.GetFiles("PortalSanMarco_full_*.bak")
                    .OrderByDescending(item => item.LastWriteTime)
                    .FirstOrDefault()
                : null;

            if (ultimo is null)
            {
                falhas.Add("Nenhum backup válido foi encontrado.");
                return;
            }

            var idade = agora - ultimo.LastWriteTime;

            if (idade > TimeSpan.FromHours(_options.MaxBackupAgeHours))
            {
                falhas.Add(
                    $"Último backup tem {idade.TotalHours:N1} horas " +
                    $"({ultimo.LastWriteTime:dd/MM/yyyy HH:mm}).");
            }
        }
        catch (Exception ex)
        {
            falhas.Add(
                "Não foi possível verificar o backup: " +
                ex.GetBaseException().Message);
        }
    }

    private void VerificarDiscos(List<string> falhas)
    {
        foreach (var nome in _options.Drives.Distinct(
                     StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var unidade = new DriveInfo(nome.TrimEnd(':') + ":\\");

                if (!unidade.IsReady || unidade.TotalSize <= 0)
                {
                    falhas.Add($"Unidade {nome}: não está disponível.");
                    continue;
                }

                var livre = unidade.AvailableFreeSpace * 100d /
                    unidade.TotalSize;

                if (livre < _options.MinimumFreeDiskPercent)
                {
                    falhas.Add(
                        $"Unidade {nome}: com apenas {livre:N1}% livre.");
                }
            }
            catch (Exception ex)
            {
                falhas.Add(
                    $"Não foi possível verificar a unidade {nome}: " +
                    ex.GetBaseException().Message);
            }
        }
    }

    private async Task<bool> EnviarAsync(
        string assunto,
        string html,
        CancellationToken cancellationToken)
    {
        var enviou = false;

        foreach (var destinatario in _options.Recipients
                     .Where(item => !string.IsNullOrWhiteSpace(item))
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                await _emailSender.EnviarAsync(
                    destinatario,
                    assunto,
                    html,
                    cancellationToken);

                enviou = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Falha ao enviar alerta do portal para {Destinatario}.",
                    destinatario);
            }
        }

        return enviou;
    }

    private async Task<MonitorState> CarregarEstadoAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            if (!File.Exists(_options.StateFile))
            {
                return new MonitorState { Saudavel = true };
            }

            await using var arquivo = File.OpenRead(_options.StateFile);
            return await JsonSerializer.DeserializeAsync<MonitorState>(
                       arquivo,
                       cancellationToken: cancellationToken) ??
                new MonitorState { Saudavel = true };
        }
        catch
        {
            return new MonitorState { Saudavel = true };
        }
    }

    private async Task SalvarEstadoAsync(
        MonitorState estado,
        CancellationToken cancellationToken)
    {
        var diretorio = Path.GetDirectoryName(_options.StateFile);

        if (!string.IsNullOrWhiteSpace(diretorio))
        {
            Directory.CreateDirectory(diretorio);
        }

        await using var arquivo = File.Create(_options.StateFile);
        await JsonSerializer.SerializeAsync(
            arquivo,
            estado,
            new JsonSerializerOptions { WriteIndented = true },
            cancellationToken);
    }

    private static string MontarHtmlFalha(
        IEnumerable<string> falhas,
        DateTime agora)
    {
        var itens = string.Join(
            string.Empty,
            falhas.Select(item =>
                $"<li>{WebUtility.HtmlEncode(item)}</li>"));

        return "<h2>Monitoramento do Portal San Marco</h2>" +
            "<p>Foram encontrados os seguintes problemas:</p>" +
            $"<ul>{itens}</ul>" +
            $"<p>Verificação: {agora:dd/MM/yyyy HH:mm:ss}</p>";
    }

    private static string MontarHtmlRecuperacao(DateTime agora)
    {
        return "<h2>Portal San Marco normalizado</h2>" +
            "<p>Portal, banco, backup e espaço em disco passaram " +
            "novamente nas verificações automáticas.</p>" +
            $"<p>Verificação: {agora:dd/MM/yyyy HH:mm:ss}</p>";
    }

    private sealed class MonitorState
    {
        public bool Saudavel { get; set; } = true;
        public DateTime UltimaVerificacaoEm { get; set; }
        public DateTime? UltimoAlertaEm { get; set; }
        public List<string> Falhas { get; set; } = [];
    }
}
