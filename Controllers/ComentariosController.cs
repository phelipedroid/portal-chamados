using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PortalSanMarco.Data;
using PortalSanMarco.Models;
using PortalSanMarco.ViewModels;

namespace PortalSanMarco.Controllers;

[Authorize]
public class ComentariosController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly IAuthorizationService
        _authorizationService;

    public ComentariosController(
        ApplicationDbContext context,
        UserManager<IdentityUser> userManager,
        IAuthorizationService authorizationService)
    {
        _context = context;
        _userManager = userManager;
        _authorizationService = authorizationService;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        AdicionarComentarioViewModel model)
    {
        var usuario = await _userManager.GetUserAsync(User);

        if (usuario is null)
        {
            return Challenge();
        }

        var chamado = await _context.Chamados
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.Id == model.ChamadoId);

        if (chamado is null)
        {
            return NotFound();
        }

        var podeAtender =
            await _authorizationService.AuthorizeAsync(
                User,
                "PodeAtenderChamados");

        if (!podeAtender.Succeeded &&
            chamado.SolicitanteId != usuario.Id)
        {
            return Forbid();
        }

        if (chamado.Status == StatusChamado.Fechado)
        {
            TempData["Erro"] =
                "Não é possível comentar em chamado fechado.";

            return RedirectToAction(
                "Details",
                "Chamados",
                new { id = chamado.Id });
        }

        if (!ModelState.IsValid ||
            string.IsNullOrWhiteSpace(model.Mensagem))
        {
            TempData["Erro"] =
                "Escreva uma mensagem antes de enviar.";

            return RedirectToAction(
                "Details",
                "Chamados",
                new { id = chamado.Id });
        }

        _context.ComentariosChamados.Add(
            new ComentarioChamado
            {
                ChamadoId = chamado.Id,
                AutorId = usuario.Id,
                Mensagem = model.Mensagem.Trim(),
                CriadoEm = DateTime.Now
            });

        _context.HistoricosChamados.Add(
            new HistoricoChamado
            {
                ChamadoId = chamado.Id,
                UsuarioId = usuario.Id,
                Acao = "Comentário adicionado",
                Descricao =
                    "Uma nova mensagem foi adicionada.",
                CriadoEm = DateTime.Now
            });

        await _context.SaveChangesAsync();

        TempData["Sucesso"] =
            "Comentário enviado com sucesso.";

        return RedirectToAction(
            "Details",
            "Chamados",
            new { id = chamado.Id });
    }
}
