using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PortalSanMarco.Data;
using PortalSanMarco.Models;
using PortalSanMarco.Services;
using PortalSanMarco.ViewModels;

namespace PortalSanMarco.Controllers;

[Authorize(Roles = "Administrador")]
public class SetoresController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IAuditoriaService _auditoria;

    public SetoresController(
        ApplicationDbContext context,
        IAuditoriaService auditoria)
    {
        _context = context;
        _auditoria = auditoria;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var setores =
            await _context.Setores
                .AsNoTracking()
                .OrderByDescending(setor => setor.Ativo)
                .ThenBy(setor => setor.Nome)
                .Select(
                    setor =>
                        new SetorListaViewModel
                        {
                            Id = setor.Id,
                            Nome = setor.Nome,
                            Ativo = setor.Ativo,
                            TotalFuncionarios =
                                setor.Funcionarios.Count,
                            TotalChamados =
                                _context.Chamados.Count(
                                    chamado =>
                                        chamado.SetorId == setor.Id)
                        })
                .ToListAsync();

        return View(setores);
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View(
            new SetorFormViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        SetorFormViewModel model)
    {
        model.Nome =
            (model.Nome ?? string.Empty).Trim();

        if (await NomeEmUso(model.Nome))
        {
            ModelState.AddModelError(
                nameof(model.Nome),
                "Já existe um setor com esse nome.");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var setor = new Setor
        {
            Nome = model.Nome,
            Ativo = true
        };

        _context.Setores.Add(setor);

        await _context.SaveChangesAsync();

        await _auditoria.RegistrarAsync(
            "Cadastro",
            "Setores",
            setor.Id.ToString(),
            $"Setor {setor.Nome} cadastrado e ativado.");

        TempData["Sucesso"] =
            $"Setor {model.Nome} cadastrado com sucesso.";

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var setor =
            await _context.Setores
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    item => item.Id == id);

        if (setor is null)
        {
            return NotFound();
        }

        return View(
            new SetorFormViewModel
            {
                Id = setor.Id,
                Nome = setor.Nome,
                Ativo = setor.Ativo
            });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        SetorFormViewModel model)
    {
        var setor =
            await _context.Setores
                .SingleOrDefaultAsync(
                    item => item.Id == model.Id);

        if (setor is null)
        {
            return NotFound();
        }

        model.Nome =
            (model.Nome ?? string.Empty).Trim();

        if (await NomeEmUso(
                model.Nome,
                setor.Id))
        {
            ModelState.AddModelError(
                nameof(model.Nome),
                "Já existe outro setor com esse nome.");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var nomeAnterior = setor.Nome;
        var ativoAnterior = setor.Ativo;

        setor.Nome = model.Nome;
        setor.Ativo = model.Ativo;

        await _context.SaveChangesAsync();

        await _auditoria.RegistrarAsync(
            "Alteração",
            "Setores",
            setor.Id.ToString(),
            $"Setor alterado de {nomeAnterior} " +
            $"({(ativoAnterior ? "ativo" : "inativo")}) para " +
            $"{setor.Nome} ({(setor.Ativo ? "ativo" : "inativo")}).");

        TempData["Sucesso"] =
            $"Setor {setor.Nome} atualizado com sucesso.";

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AlternarStatus(
        int id)
    {
        var setor =
            await _context.Setores
                .SingleOrDefaultAsync(
                    item => item.Id == id);

        if (setor is null)
        {
            return NotFound();
        }

        setor.Ativo = !setor.Ativo;

        await _context.SaveChangesAsync();

        await _auditoria.RegistrarAsync(
            setor.Ativo ? "Ativação" : "Desativação",
            "Setores",
            setor.Id.ToString(),
            $"Setor {setor.Nome} foi " +
            $"{(setor.Ativo ? "ativado" : "desativado")}.");

        TempData["Sucesso"] =
            setor.Ativo
                ? $"Setor {setor.Nome} ativado."
                : $"Setor {setor.Nome} desativado.";

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var setor =
            await _context.Setores
                .SingleOrDefaultAsync(
                    item => item.Id == id);

        if (setor is null)
        {
            return NotFound();
        }

        var possuiFuncionarios =
            await _context.FuncionariosPerfis
                .AnyAsync(
                    perfil => perfil.SetorId == id);

        var possuiChamados =
            await _context.Chamados
                .AnyAsync(
                    chamado => chamado.SetorId == id);

        if (possuiFuncionarios || possuiChamados)
        {
            TempData["Erro"] =
                "O setor não pode ser excluído porque possui " +
                "funcionários ou chamados vinculados. " +
                "Desative-o para impedir novos usos.";

            return RedirectToAction(nameof(Index));
        }

        _context.Setores.Remove(setor);
        await _context.SaveChangesAsync();

        await _auditoria.RegistrarAsync(
            "Exclusão",
            "Setores",
            id.ToString(),
            $"Setor {setor.Nome} excluído sem vínculos existentes.");

        TempData["Sucesso"] =
            $"Setor {setor.Nome} excluído com sucesso.";

        return RedirectToAction(nameof(Index));
    }

    private Task<bool> NomeEmUso(
        string nome,
        int? ignorarId = null)
    {
        return _context.Setores.AnyAsync(
            setor =>
                setor.Nome == nome &&
                (!ignorarId.HasValue ||
                 setor.Id != ignorarId.Value));
    }
}
