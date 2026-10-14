using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PortalSanMarco.Data;

namespace PortalSanMarco.ViewComponents;

public sealed class CadastrosPendentesViewComponent : ViewComponent
{
    private readonly ApplicationDbContext _context;

    public CadastrosPendentesViewComponent(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IViewComponentResult> InvokeAsync(bool ativo)
    {
        ViewBag.Ativo = ativo;

        var quantidade = await _context.FuncionariosPerfis
            .AsNoTracking()
            .CountAsync(perfil => perfil.CadastroPendente);

        return View(quantidade);
    }
}
