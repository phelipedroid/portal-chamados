using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PortalSanMarco.Data;
using PortalSanMarco.Models;

namespace PortalSanMarco.Services;

public interface IChamadoAccessService
{
    Task<IQueryable<Chamado>> AplicarFiltroAsync(
        IQueryable<Chamado> consulta,
        ClaimsPrincipal principal);

    Task<bool> PodeVisualizarAsync(
        int chamadoId,
        ClaimsPrincipal principal);
}

public class ChamadoAccessService : IChamadoAccessService
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<IdentityUser> _userManager;

    public ChamadoAccessService(
        ApplicationDbContext context,
        UserManager<IdentityUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public async Task<IQueryable<Chamado>> AplicarFiltroAsync(
        IQueryable<Chamado> consulta,
        ClaimsPrincipal principal)
    {
        var usuario =
            await _userManager.GetUserAsync(principal);

        if (usuario is null)
        {
            return consulta.Where(_ => false);
        }

        if (principal.IsInRole("Administrador") ||
            principal.IsInRole("Tecnico"))
        {
            return consulta;
        }

        if (principal.IsInRole("Gerente"))
        {
            var unidades =
                await _context.GerentesUnidades
                    .AsNoTracking()
                    .Where(item =>
                        item.UsuarioId == usuario.Id)
                    .Select(item => item.UnidadeId)
                    .ToListAsync();

            if (unidades.Count == 0)
            {
                var unidadePrincipal =
                    await _context.FuncionariosPerfis
                        .AsNoTracking()
                        .Where(item =>
                            item.UsuarioId == usuario.Id)
                        .Select(item => (int?)item.UnidadeId)
                        .SingleOrDefaultAsync();

                if (unidadePrincipal.HasValue)
                {
                    unidades.Add(unidadePrincipal.Value);
                }
            }

            return consulta.Where(chamado =>
                unidades.Contains(chamado.UnidadeId));
        }

        return consulta.Where(chamado =>
            chamado.SolicitanteId == usuario.Id);
    }

    public async Task<bool> PodeVisualizarAsync(
        int chamadoId,
        ClaimsPrincipal principal)
    {
        var consulta = await AplicarFiltroAsync(
            _context.Chamados.AsNoTracking(),
            principal);

        return await consulta.AnyAsync(chamado =>
            chamado.Id == chamadoId);
    }
}
