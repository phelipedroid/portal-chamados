using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PortalSanMarco.Data;
using PortalSanMarco.Models;
using PortalSanMarco.Services;
using PortalSanMarco.ViewModels;

namespace PortalSanMarco.Controllers;

[Authorize(Policy = "PodeVisualizarTodosChamados")]
public class DashboardController : Controller
{
    private readonly ApplicationDbContext _context;

    private readonly IChamadoAccessService _chamadoAccessService;

    public DashboardController(
        ApplicationDbContext context,
        IChamadoAccessService chamadoAccessService)
    {
        _context = context;
        _chamadoAccessService = chamadoAccessService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        DateTime? dataInicial,
        DateTime? dataFinal)
    {
        var hoje = DateTime.Today;

        dataInicial ??=
            new DateTime(
                hoje.Year,
                hoje.Month,
                1);

        dataFinal ??= hoje;

        if (dataInicial.Value.Date >
            dataFinal.Value.Date)
        {
            TempData["Erro"] =
                "A data inicial não pode ser maior " +
                "que a data final.";

            dataInicial =
                new DateTime(
                    hoje.Year,
                    hoje.Month,
                    1);

            dataFinal = hoje;
        }

        var inicio =
            dataInicial.Value.Date;

        var fimExclusivo =
            ObterFimExclusivo(
                dataFinal.Value.Date);

        var consultaBase =
            _context.Chamados
                .AsNoTracking();

        consultaBase = await _chamadoAccessService
            .AplicarFiltroAsync(consultaBase, User);

        var consulta =
            consultaBase
                .Where(chamado =>
                    chamado.CriadoEm >= inicio &&
                    chamado.CriadoEm < fimExclusivo);

        var encerrados =
            consulta.Where(chamado =>
                chamado.Status ==
                    StatusChamado.Resolvido ||

                chamado.Status ==
                    StatusChamado.Fechado);

        var atendimentosFinalizados =
            await encerrados
                .Where(chamado =>
                    chamado.ResolvidoEm.HasValue)
                .Select(chamado => new
                {
                    chamado.CriadoEm,
                    chamado.ResolvidoEm
                })
                .ToListAsync();

        var temposValidos =
            atendimentosFinalizados
                .Select(item =>
                    (item.ResolvidoEm!.Value -
                     item.CriadoEm).TotalMinutes)
                .Where(minutos => minutos >= 0)
                .ToList();

        var tempoMedioTrabalhado =
            await encerrados
                .Where(chamado =>
                    chamado.TempoGastoMinutos.HasValue &&
                    chamado.TempoGastoMinutos.Value > 0)
                .Select(chamado =>
                    (double?)chamado.TempoGastoMinutos)
                .AverageAsync();

        var model = new DashboardViewModel
        {
            DataInicial = inicio,

            DataFinal =
                dataFinal.Value.Date,

            TotalChamados =
                await consulta.CountAsync(),

            TotalAbertos =
                await consulta.CountAsync(chamado =>
                    chamado.Status ==
                        StatusChamado.Aberto),

            TotalEmAndamento =
                await consulta.CountAsync(chamado =>
                    chamado.Status ==
                        StatusChamado.EmAndamento),

            TotalAguardando =
                await consulta.CountAsync(chamado =>
                    chamado.Status ==
                        StatusChamado
                            .AguardandoFuncionario ||

                    chamado.Status ==
                        StatusChamado
                            .AguardandoFornecedor),

            TotalFechados =
                await encerrados.CountAsync(),

            TotalUrgentes =
                await consulta.CountAsync(chamado =>
                    chamado.Prioridade ==
                        PrioridadeChamado.Urgente &&

                    chamado.Status !=
                        StatusChamado.Resolvido &&

                    chamado.Status !=
                        StatusChamado.Fechado),

            TotalSemResponsavel =
                await consulta.CountAsync(chamado =>
                    chamado.TecnicoResponsavelId ==
                        null &&

                    chamado.Status !=
                        StatusChamado.Resolvido &&

                    chamado.Status !=
                        StatusChamado.Fechado),

            TempoMedioAtendimentoMinutos =
                temposValidos.Count > 0
                    ? Math.Round(
                        temposValidos.Average(),
                        2)
                    : null,

            TempoMedioTrabalhadoMinutos =
                tempoMedioTrabalhado.HasValue
                    ? Math.Round(
                        tempoMedioTrabalhado.Value,
                        2)
                    : null,

            ChamadosPorCategoria =
                await consulta
                    .GroupBy(chamado =>
                        chamado.Categoria.Nome)
                    .Select(grupo =>
                        new IndicadorDashboardViewModel
                        {
                            Nome = grupo.Key,

                            Quantidade =
                                grupo.Count()
                        })
                    .OrderByDescending(item =>
                        item.Quantidade)
                    .Take(5)
                    .ToListAsync(),

            ChamadosPorSetor =
                await consulta
                    .GroupBy(chamado =>
                        chamado.Setor.Nome)
                    .Select(grupo =>
                        new IndicadorDashboardViewModel
                        {
                            Nome = grupo.Key,

                            Quantidade =
                                grupo.Count()
                        })
                    .OrderByDescending(item =>
                        item.Quantidade)
                    .Take(5)
                    .ToListAsync(),

            ChamadosPorTecnico =
                await encerrados
                    .Where(chamado =>
                        chamado.TecnicoResponsavelId !=
                            null)
                    .GroupBy(chamado =>
                        chamado
                            .TecnicoResponsavel!
                            .Email)
                    .Select(grupo =>
                        new IndicadorDashboardViewModel
                        {
                            Nome =
                                grupo.Key ??
                                "Usuário sem e-mail",

                            Quantidade =
                                grupo.Count()
                        })
                    .OrderByDescending(item =>
                        item.Quantidade)
                    .Take(5)
                    .ToListAsync()
        };

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> ExportarCsv(
        DateTime? dataInicial,
        DateTime? dataFinal)
    {
        var hoje = DateTime.Today;

        dataInicial ??=
            new DateTime(
                hoje.Year,
                hoje.Month,
                1);

        dataFinal ??= hoje;

        if (dataInicial.Value.Date >
            dataFinal.Value.Date)
        {
            TempData["Erro"] =
                "A data inicial não pode ser maior " +
                "que a data final.";

            return RedirectToAction(
                nameof(Index));
        }

        var inicio =
            dataInicial.Value.Date;

        var fimExclusivo =
            ObterFimExclusivo(
                dataFinal.Value.Date);

        IQueryable<Chamado> consulta =
            _context.Chamados
                .AsNoTracking()
                .Include(chamado =>
                    chamado.Categoria)
                .Include(chamado =>
                    chamado.Unidade)
                .Include(chamado =>
                    chamado.Setor)
                .Include(chamado =>
                    chamado.Solicitante)
                .Include(chamado =>
                    chamado.TecnicoResponsavel);

        consulta = await _chamadoAccessService
            .AplicarFiltroAsync(consulta, User);

        var chamados =
            await consulta
                .Where(chamado =>
                    chamado.CriadoEm >= inicio &&
                    chamado.CriadoEm < fimExclusivo)
                .OrderBy(chamado =>
                    chamado.CriadoEm)
                .ToListAsync();

        var csv =
            new StringBuilder();

        csv.AppendLine(
            "Número;" +
            "Título;" +
            "Descrição;" +
            "Categoria;" +
            "Prioridade;" +
            "Status;" +
            "Unidade;" +
            "Setor;" +
            "Solicitante;" +
            "Responsável;" +
            "Computador ou patrimônio;" +
            "Aberto em;" +
            "Finalizado em;" +
            "Tempo gasto em minutos;" +
            "Solução aplicada");

        foreach (var chamado in chamados)
        {
            var prioridade =
                chamado.Prioridade switch
                {
                    PrioridadeChamado.Media =>
                        "Média",

                    PrioridadeChamado.Urgente =>
                        "Urgente",

                    _ =>
                        "Leve"
                };

            var status =
                chamado.Status switch
                {
                    StatusChamado.EmAndamento =>
                        "Em andamento",

                    StatusChamado
                        .AguardandoFuncionario =>
                        "Aguardando funcionário",

                    StatusChamado
                        .AguardandoFornecedor =>
                        "Aguardando fornecedor",

                    StatusChamado.Resolvido =>
                        "Resolvido",

                    StatusChamado.Fechado =>
                        "Fechado",

                    _ =>
                        "Aberto"
                };

            var valores = new[]
            {
                chamado.Id.ToString("D6"),

                chamado.Titulo,

                chamado.Descricao,

                chamado.Categoria.Nome,

                prioridade,

                status,

                chamado.Unidade.Nome,

                chamado.Setor.Nome,

                chamado.Solicitante.Email,

                chamado.TecnicoResponsavel?.Email,

                chamado.ComputadorPatrimonio,

                chamado.CriadoEm.ToString(
                    "dd/MM/yyyy HH:mm"),

                chamado.ResolvidoEm?.ToString(
                    "dd/MM/yyyy HH:mm"),

                chamado.TempoGastoMinutos?.ToString(),

                chamado.SolucaoAplicada
            };

            var linha =
                string.Join(
                    ";",
                    valores.Select(
                        PrepararCsv));

            csv.AppendLine(linha);
        }

        var conteudo =
            Encoding.UTF8.GetBytes(
                csv.ToString());

        var preambulo =
            Encoding.UTF8.GetPreamble();

        var arquivo =
            new byte[
                preambulo.Length +
                conteudo.Length];

        Buffer.BlockCopy(
            preambulo,
            0,
            arquivo,
            0,
            preambulo.Length);

        Buffer.BlockCopy(
            conteudo,
            0,
            arquivo,
            preambulo.Length,
            conteudo.Length);

        var nomeArquivo =
            $"chamados_" +
            $"{inicio:yyyyMMdd}_" +
            $"{dataFinal.Value:yyyyMMdd}.csv";

        return File(
            arquivo,
            "text/csv; charset=utf-8",
            nomeArquivo);
    }

    private static string PrepararCsv(
        string? valor)
    {
        var texto =
            valor ?? string.Empty;

        var textoSemEspacos =
            texto.TrimStart();

        if (textoSemEspacos.StartsWith("=") ||
            textoSemEspacos.StartsWith("+") ||
            textoSemEspacos.StartsWith("-") ||
            textoSemEspacos.StartsWith("@"))
        {
            texto = "'" + texto;
        }

        texto = texto.Replace(
            "\"",
            "\"\"");

        return $"\"{texto}\"";
    }

    private static DateTime ObterFimExclusivo(
        DateTime dataFinal)
    {
        return dataFinal >= DateTime.MaxValue.Date
            ? DateTime.MaxValue
            : dataFinal.AddDays(1);
    }
}
