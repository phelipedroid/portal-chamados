using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PortalSanMarco.Data;
using PortalSanMarco.Services;

namespace PortalSanMarco.Controllers;

[Authorize]
[ApiController]
[Route("Notificacoes")]
public class NotificacoesController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly IChamadoAccessService _chamadoAccessService;

    public NotificacoesController(
        ApplicationDbContext context,
        UserManager<IdentityUser> userManager,
        IChamadoAccessService chamadoAccessService)
    {
        _context = context;
        _userManager = userManager;
        _chamadoAccessService = chamadoAccessService;
    }

    [HttpGet("Pendentes")]
    public async Task<IActionResult> Pendentes()
    {
        var usuario = await _userManager.GetUserAsync(User);

        if (usuario is null)
        {
            return Unauthorized();
        }

        var chamadosAcessiveis =
            await _chamadoAccessService.AplicarFiltroAsync(
                _context.Chamados.AsNoTracking(),
                User);

        var interacoesNaoLidas =
            _context.HistoricosChamados
                .AsNoTracking()
                .Where(historico =>
                    historico.UsuarioId != usuario.Id &&
                    chamadosAcessiveis.Any(chamado =>
                        chamado.Id == historico.ChamadoId) &&
                    !_context.ChamadosVisualizacoes.Any(
                        visualizacao =>
                            visualizacao.UsuarioId == usuario.Id &&
                            visualizacao.ChamadoId == historico.ChamadoId &&
                            visualizacao.VisualizadoEm >= historico.CriadoEm));

        var total =
            await interacoesNaoLidas
                .Select(historico => historico.ChamadoId)
                .Distinct()
                .CountAsync();

        var historicosRecentes =
            await interacoesNaoLidas
                .OrderByDescending(historico => historico.CriadoEm)
                .Select(historico => new
                {
                    historico.ChamadoId,
                    historico.Chamado.Titulo,
                    historico.Acao,
                    historico.CriadoEm
                })
                .Take(100)
                .ToListAsync();

        var itens = historicosRecentes
            .GroupBy(item => item.ChamadoId)
            .Select(grupo => grupo.First())
            .Take(8)
            .Select(item => new
            {
                chamadoId = item.ChamadoId,
                numero = item.ChamadoId.ToString("D6"),
                titulo = item.Titulo,
                acao = item.Acao,
                criadaEm = item.CriadoEm,
                url = Url.Action(
                    "Details",
                    "Chamados",
                    new { id = item.ChamadoId })
            });

        return Ok(new
        {
            total,
            itens
        });
    }
}
