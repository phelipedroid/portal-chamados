using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PortalSanMarco.Data;
using PortalSanMarco.Models;
using PortalSanMarco.Services;

namespace PortalSanMarco.Controllers;

[Authorize]
public class AnexosController : Controller
{
    private const long TamanhoMaximo =
        5 * 1024 * 1024;

    private readonly ApplicationDbContext _context;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly IAuthorizationService
        _authorizationService;

    private readonly IChamadoAccessService
        _chamadoAccessService;

    public AnexosController(
        ApplicationDbContext context,
        UserManager<IdentityUser> userManager,
        IAuthorizationService authorizationService,
        IChamadoAccessService chamadoAccessService)
    {
        _context = context;
        _userManager = userManager;
        _authorizationService = authorizationService;
        _chamadoAccessService = chamadoAccessService;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Enviar(
        int chamadoId,
        IFormFile arquivo)
    {
        var usuario = await _userManager.GetUserAsync(User);

        if (usuario is null)
        {
            return Challenge();
        }

        var chamado = await _context.Chamados
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.Id == chamadoId);

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
                "Não é possível anexar arquivo " +
                "em chamado fechado.";

            return Voltar(chamadoId);
        }

        if (arquivo is null || arquivo.Length == 0)
        {
            TempData["Erro"] =
                "Selecione um arquivo.";

            return Voltar(chamadoId);
        }

        if (arquivo.Length > TamanhoMaximo)
        {
            TempData["Erro"] =
                "O arquivo deve possuir no máximo 5 MB.";

            return Voltar(chamadoId);
        }

        var extensao = Path
            .GetExtension(arquivo.FileName)
            .ToLowerInvariant();

        var tiposPermitidos =
            new Dictionary<string, string>
            {
                [".jpg"] = "image/jpeg",
                [".jpeg"] = "image/jpeg",
                [".png"] = "image/png",
                [".webp"] = "image/webp",
                [".pdf"] = "application/pdf"
            };

        if (!tiposPermitidos.TryGetValue(
                extensao,
                out var tipoEsperado) ||
            arquivo.ContentType != tipoEsperado)
        {
            TempData["Erro"] =
                "Formato não permitido. Use JPG, PNG, " +
                "WebP ou PDF.";

            return Voltar(chamadoId);
        }

        await using var memoria = new MemoryStream();
        await arquivo.CopyToAsync(memoria);

        var conteudo = memoria.ToArray();

        if (!AssinaturaValida(extensao, conteudo))
        {
            TempData["Erro"] =
                "O conteúdo do arquivo não corresponde " +
                "ao formato informado.";

            return Voltar(chamadoId);
        }

        var nomeSeguro = Path.GetFileName(
            arquivo.FileName);

        if (string.IsNullOrWhiteSpace(nomeSeguro) ||
            nomeSeguro.Length > 255)
        {
            TempData["Erro"] =
                "O nome do arquivo deve possuir no máximo 255 caracteres.";

            return Voltar(chamadoId);
        }

        _context.AnexosChamados.Add(
            new AnexoChamado
            {
                ChamadoId = chamado.Id,
                NomeOriginal = nomeSeguro,
                TipoConteudo = tipoEsperado,
                TamanhoBytes = arquivo.Length,
                Conteudo = conteudo,
                AutorId = usuario.Id,
                CriadoEm = DateTime.Now
            });

        _context.HistoricosChamados.Add(
            new HistoricoChamado
            {
                ChamadoId = chamado.Id,
                UsuarioId = usuario.Id,
                Acao = "Arquivo anexado",
                Descricao =
                    $"Arquivo \"{nomeSeguro}\" anexado.",
                CriadoEm = DateTime.Now
            });

        await _context.SaveChangesAsync();

        TempData["Sucesso"] =
            "Arquivo anexado com sucesso.";

        return Voltar(chamadoId);
    }

    [HttpGet]
    public async Task<IActionResult> Download(int id)
    {
        var usuario = await _userManager.GetUserAsync(User);

        if (usuario is null)
        {
            return Challenge();
        }

        var anexo = await _context.AnexosChamados
            .AsNoTracking()
            .Include(item => item.Chamado)
            .SingleOrDefaultAsync(item => item.Id == id);

        if (anexo is null)
        {
            return NotFound();
        }

        if (!await _chamadoAccessService
                .PodeVisualizarAsync(anexo.ChamadoId, User))
        {
            return Forbid();
        }

        return File(
            anexo.Conteudo,
            anexo.TipoConteudo,
            anexo.NomeOriginal);
    }

    [HttpGet]
    public async Task<IActionResult> Visualizar(int id)
    {
        var usuario = await _userManager.GetUserAsync(User);

        if (usuario is null)
        {
            return Challenge();
        }

        var anexo = await _context.AnexosChamados
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == id);

        if (anexo is null)
        {
            return NotFound();
        }

        if (!anexo.TipoConteudo.StartsWith(
                "image/",
                StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(
                "A pré-visualização está disponível somente para imagens.");
        }

        if (!await _chamadoAccessService
                .PodeVisualizarAsync(anexo.ChamadoId, User))
        {
            return Forbid();
        }

        Response.Headers.CacheControl =
            "private, max-age=300";

        Response.Headers.XContentTypeOptions =
            "nosniff";

        return File(
            anexo.Conteudo,
            anexo.TipoConteudo);
    }

    private RedirectToActionResult Voltar(
        int chamadoId)
    {
        return RedirectToAction(
            "Details",
            "Chamados",
            new { id = chamadoId });
    }

    private static bool AssinaturaValida(
        string extensao,
        byte[] conteudo)
    {
        if (conteudo.Length < 12)
        {
            return false;
        }

        return extensao switch
        {
            ".jpg" or ".jpeg" =>
                conteudo[0] == 0xFF &&
                conteudo[1] == 0xD8 &&
                conteudo[2] == 0xFF,

            ".png" =>
                conteudo[0] == 0x89 &&
                conteudo[1] == 0x50 &&
                conteudo[2] == 0x4E &&
                conteudo[3] == 0x47,

            ".webp" =>
                conteudo[0] == 0x52 &&
                conteudo[1] == 0x49 &&
                conteudo[2] == 0x46 &&
                conteudo[3] == 0x46 &&
                conteudo[8] == 0x57 &&
                conteudo[9] == 0x45 &&
                conteudo[10] == 0x42 &&
                conteudo[11] == 0x50,

            ".pdf" =>
                conteudo[0] == 0x25 &&
                conteudo[1] == 0x50 &&
                conteudo[2] == 0x44 &&
                conteudo[3] == 0x46,

            _ => false
        };
    }
}
