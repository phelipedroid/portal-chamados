using Microsoft.Extensions.Options;
using PortalSanMarco.Models;

namespace PortalSanMarco.Services;

public class SlaService : ISlaService
{
    private readonly SlaOptions _options;
    private readonly HashSet<DateOnly> _feriados;

    public SlaService(IOptions<SlaOptions> options)
    {
        _options = options.Value;
        _feriados = _options.Feriados.ToHashSet();

        if (FimDoExpediente <= InicioDoExpediente)
        {
            throw new InvalidOperationException(
                "O fim do expediente do SLA deve ser posterior ao início.");
        }
    }

    public DateTime CalcularPrazo(
        DateTime criadoEm,
        PrioridadeChamado prioridade)
    {
        return AdicionarMinutosUteis(
            criadoEm,
            ObterMinutosDaPrioridade(prioridade));
    }

    public void AplicarMudancaStatus(
        Chamado chamado,
        StatusChamado statusAnterior,
        StatusChamado novoStatus,
        DateTime agora)
    {
        var estavaPausado = EhStatusPausado(statusAnterior);
        var ficaraPausado = EhStatusPausado(novoStatus);

        if (!estavaPausado && ficaraPausado)
        {
            chamado.SlaPausadoEm = agora;
            return;
        }

        if (estavaPausado && !ficaraPausado &&
            chamado.SlaPausadoEm.HasValue)
        {
            var minutosPausados = ContarMinutosUteis(
                chamado.SlaPausadoEm.Value,
                agora);

            chamado.SlaMinutosPausados += minutosPausados;

            if (chamado.PrazoEm.HasValue && minutosPausados > 0)
            {
                chamado.PrazoEm = AdicionarMinutosUteis(
                    chamado.PrazoEm.Value,
                    minutosPausados);
            }

            chamado.SlaPausadoEm = null;
        }
    }

    public void RecalcularPorPrioridade(
        Chamado chamado,
        PrioridadeChamado prioridadeAnterior,
        DateTime agora)
    {
        if (prioridadeAnterior == chamado.Prioridade)
        {
            return;
        }

        var baseCalculo = chamado.CriadoEm;
        var novoPrazo = CalcularPrazo(baseCalculo, chamado.Prioridade);

        if (chamado.SlaMinutosPausados > 0)
        {
            novoPrazo = AdicionarMinutosUteis(
                novoPrazo,
                chamado.SlaMinutosPausados);
        }

        if (chamado.SlaPausadoEm.HasValue)
        {
            novoPrazo = AdicionarMinutosUteis(
                novoPrazo,
                ContarMinutosUteis(chamado.SlaPausadoEm.Value, agora));
        }

        chamado.PrazoEm = novoPrazo;
    }

    private DateTime AdicionarMinutosUteis(DateTime inicio, int minutos)
    {
        var atual = NormalizarParaHorarioUtil(inicio);
        var restantes = minutos;

        while (restantes > 0)
        {
            var fimDia = atual.Date + FimDoExpediente;
            var disponiveis = Math.Max(
                0,
                (int)Math.Floor((fimDia - atual).TotalMinutes));

            if (restantes <= disponiveis)
            {
                return atual.AddMinutes(restantes);
            }

            restantes -= disponiveis;
            atual = ProximoDiaUtil(atual.Date.AddDays(1));
        }

        return atual;
    }

    public int ContarMinutosUteis(DateTime inicio, DateTime fim)
    {
        if (fim <= inicio)
        {
            return 0;
        }

        var total = 0;
        var dia = inicio.Date;

        while (dia <= fim.Date)
        {
            if (EhDiaUtil(dia))
            {
                var comeco = dia + InicioDoExpediente;
                var termino = dia + FimDoExpediente;
                var intervaloInicio = inicio > comeco ? inicio : comeco;
                var intervaloFim = fim < termino ? fim : termino;

                if (intervaloFim > intervaloInicio)
                {
                    total += (int)Math.Floor(
                        (intervaloFim - intervaloInicio).TotalMinutes);
                }
            }

            dia = dia.AddDays(1);
        }

        return total;
    }

    private DateTime NormalizarParaHorarioUtil(DateTime data)
    {
        if (!EhDiaUtil(data.Date))
        {
            return ProximoDiaUtil(data.Date.AddDays(1));
        }

        var inicio = data.Date + InicioDoExpediente;
        var fim = data.Date + FimDoExpediente;

        if (data < inicio)
        {
            return inicio;
        }

        return data >= fim
            ? ProximoDiaUtil(data.Date.AddDays(1))
            : data;
    }

    private DateTime ProximoDiaUtil(DateTime data)
    {
        var dia = data.Date;

        while (!EhDiaUtil(dia))
        {
            dia = dia.AddDays(1);
        }

        return dia + InicioDoExpediente;
    }

    private bool EhDiaUtil(DateTime data)
    {
        return data.DayOfWeek is not DayOfWeek.Saturday and
            not DayOfWeek.Sunday &&
            !_feriados.Contains(DateOnly.FromDateTime(data));
    }

    private static int ObterMinutosDaPrioridade(
        PrioridadeChamado prioridade)
    {
        return prioridade switch
        {
            PrioridadeChamado.Urgente => 4 * 60,
            PrioridadeChamado.Media => 24 * 60,
            _ => 72 * 60
        };
    }

    public static bool EhStatusPausado(StatusChamado status)
    {
        return status is StatusChamado.AguardandoFuncionario or
            StatusChamado.AguardandoFornecedor;
    }

    private TimeSpan InicioDoExpediente =>
        new(_options.InicioHora, _options.InicioMinuto, 0);

    private TimeSpan FimDoExpediente =>
        new(_options.FimHora, _options.FimMinuto, 0);
}
