using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PortalSanMarco.Data;
using PortalSanMarco.Models;
using PortalSanMarco.Services;
using PortalSanMarco.ViewModels;

namespace PortalSanMarco.Controllers;

[Authorize(Roles = "Administrador")]
public class AuditoriaController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IAuditoriaPdfService _pdfService;
    private readonly IAuditoriaService _auditoria;

    public AuditoriaController(
        ApplicationDbContext context,
        IAuditoriaPdfService pdfService,
        IAuditoriaService auditoria)
    {
        _context = context;
        _pdfService = pdfService;
        _auditoria = auditoria;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        string? busca,
        string? entidade,
        DateTime? dataInicial,
        DateTime? dataFinal,
        int pagina = 1)
    {
        DefinirPeriodoPadrao(ref dataInicial, ref dataFinal);

        if (!PeriodoValido(dataInicial, dataFinal))
        {
            ViewBag.Erro =
                "A data inicial não pode ser maior que a data final.";
            dataInicial = new DateTime(
                DateTime.Today.Year,
                DateTime.Today.Month,
                1);
            dataFinal = DateTime.Today;
        }

        var consulta = AplicarFiltros(
            _context.AuditoriasAdministrativas.AsNoTracking(),
            busca,
            entidade,
            dataInicial!.Value,
            dataFinal!.Value);

        if (!string.IsNullOrWhiteSpace(busca))
        {
            busca = busca.Trim();
        }

        const int itensPorPagina = 25;
        var totalItens = await consulta.CountAsync();
        var totalPaginas = Math.Max(
            1,
            (int)Math.Ceiling(totalItens / (double)itensPorPagina));
        pagina = Math.Clamp(pagina, 1, totalPaginas);

        var registros = await consulta
            .OrderByDescending(item => item.CriadoEm)
            .Skip((pagina - 1) * itensPorPagina)
            .Take(itensPorPagina)
            .ToListAsync();

        await _auditoria
            .TraduzirReferenciasOrganizacionaisAsync(registros);

        ViewBag.Entidades = await _context.AuditoriasAdministrativas
            .AsNoTracking()
            .Select(item => item.Entidade)
            .Distinct()
            .OrderBy(item => item)
            .ToListAsync();

        return View(new AuditoriaListaViewModel
        {
            Registros = registros,
            Busca = busca,
            Entidade = entidade,
            DataInicial = dataInicial,
            DataFinal = dataFinal,
            PaginaAtual = pagina,
            TotalPaginas = totalPaginas,
            TotalItens = totalItens
        });
    }

    [HttpGet]
    public async Task<IActionResult> ExportarPdf(
        string? busca,
        string? entidade,
        DateTime? dataInicial,
        DateTime? dataFinal,
        CancellationToken cancellationToken)
    {
        DefinirPeriodoPadrao(ref dataInicial, ref dataFinal);

        if (!PeriodoValido(dataInicial, dataFinal))
        {
            TempData["Erro"] =
                "A data inicial não pode ser maior que a data final.";
            return RedirectToAction(nameof(Index));
        }

        busca = string.IsNullOrWhiteSpace(busca)
            ? null
            : busca.Trim();
        entidade = string.IsNullOrWhiteSpace(entidade)
            ? null
            : entidade.Trim();

        var consulta = AplicarFiltros(
            _context.AuditoriasAdministrativas.AsNoTracking(),
            busca,
            entidade,
            dataInicial!.Value,
            dataFinal!.Value);

        const int limiteRegistros = 10000;
        var total = await consulta.CountAsync(cancellationToken);

        if (total > limiteRegistros)
        {
            TempData["Erro"] =
                $"O período possui {total} registros. Reduza o período ou os filtros para exportar no máximo {limiteRegistros}.";
            return RedirectToAction(
                nameof(Index),
                new
                {
                    busca,
                    entidade,
                    dataInicial = dataInicial.Value.ToString("yyyy-MM-dd"),
                    dataFinal = dataFinal.Value.ToString("yyyy-MM-dd")
                });
        }

        var registros = await consulta
            .OrderBy(item => item.CriadoEm)
            .ThenBy(item => item.Id)
            .ToListAsync(cancellationToken);

        await _auditoria.TraduzirReferenciasOrganizacionaisAsync(
            registros,
            cancellationToken);

        var geradoPor = User.Identity?.Name ?? "Administrador";
        var pdf = _pdfService.Gerar(
            registros,
            dataInicial.Value,
            dataFinal.Value,
            busca,
            entidade,
            geradoPor);

        await _auditoria.RegistrarAsync(
            "Exportação PDF",
            "Auditoria",
            null,
            $"Relatório de auditoria exportado para o período de {dataInicial:dd/MM/yyyy} a {dataFinal:dd/MM/yyyy}, com {registros.Count} registro(s).",
            cancellationToken);

        var nomeArquivo =
            $"auditoria_{dataInicial:yyyyMMdd}_{dataFinal:yyyyMMdd}.pdf";

        return File(pdf, "application/pdf", nomeArquivo);
    }

    private static IQueryable<AuditoriaAdministrativa> AplicarFiltros(
        IQueryable<AuditoriaAdministrativa> consulta,
        string? busca,
        string? entidade,
        DateTime dataInicial,
        DateTime dataFinal)
    {
        var inicio = dataInicial.Date;
        var fimExclusivo = dataFinal.Date.AddDays(1);

        consulta = consulta.Where(item =>
            item.CriadoEm >= inicio &&
            item.CriadoEm < fimExclusivo);

        if (!string.IsNullOrWhiteSpace(busca))
        {
            var termo = busca.Trim();
            consulta = consulta.Where(item =>
                item.UsuarioEmail.Contains(termo) ||
                item.Acao.Contains(termo) ||
                item.Descricao.Contains(termo));
        }

        if (!string.IsNullOrWhiteSpace(entidade))
        {
            var area = entidade.Trim();
            consulta = consulta.Where(item => item.Entidade == area);
        }

        return consulta;
    }

    private static void DefinirPeriodoPadrao(
        ref DateTime? dataInicial,
        ref DateTime? dataFinal)
    {
        var hoje = DateTime.Today;
        dataInicial ??= new DateTime(hoje.Year, hoje.Month, 1);
        dataFinal ??= hoje;
    }

    private static bool PeriodoValido(
        DateTime? dataInicial,
        DateTime? dataFinal) =>
        dataInicial.HasValue &&
        dataFinal.HasValue &&
        dataInicial.Value.Date <= dataFinal.Value.Date &&
        dataFinal.Value.Date < DateTime.MaxValue.Date;
}
