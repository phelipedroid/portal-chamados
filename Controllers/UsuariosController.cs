using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PortalSanMarco.Data;
using PortalSanMarco.Models;
using PortalSanMarco.Services;
using PortalSanMarco.ViewModels;

namespace PortalSanMarco.Controllers;

[Authorize(Roles = "Administrador")]
public class UsuariosController : Controller
{
    private readonly UserManager<IdentityUser> _userManager;
    private readonly ApplicationDbContext _context;
    private readonly IAuditoriaService _auditoria;
    private readonly IUsuarioEmailNotificationService _usuarioEmail;
    private readonly SelfRegistrationOptions _selfRegistrationOptions;

    public UsuariosController(
        UserManager<IdentityUser> userManager,
        ApplicationDbContext context,
        IAuditoriaService auditoria,
        IUsuarioEmailNotificationService usuarioEmail,
        IOptions<SelfRegistrationOptions> selfRegistrationOptions)
    {
        _userManager = userManager;
        _context = context;
        _auditoria = auditoria;
        _usuarioEmail = usuarioEmail;
        _selfRegistrationOptions = selfRegistrationOptions.Value;
    }

    public async Task<IActionResult> Index()
    {
        var usuarios = await _userManager.Users
            .AsNoTracking()
            .Where(usuario => !_context.FuncionariosPerfis.Any(perfil =>
                perfil.UsuarioId == usuario.Id &&
                perfil.CadastroPendente))
            .OrderBy(usuario => usuario.Email)
            .ToListAsync();

        var perfis = await _context.FuncionariosPerfis
            .AsNoTracking()
            .Include(perfil => perfil.Unidade)
            .Include(perfil => perfil.Setor)
            .ToDictionaryAsync(
                perfil => perfil.UsuarioId);

        var vinculosDeGerentes =
            await _context.GerentesUnidades
                .AsNoTracking()
                .Select(item => new
                {
                    item.UsuarioId,
                    Unidade = item.Unidade.Nome
                })
                .ToListAsync();

        var unidadesPorGerente =
            vinculosDeGerentes
                .GroupBy(item => item.UsuarioId)
                .ToDictionary(
                    grupo => grupo.Key,
                    grupo => string.Join(
                        ", ",
                        grupo
                            .OrderBy(item => item.Unidade)
                            .Select(item => item.Unidade)));

        var resultado =
            new List<UsuarioListaViewModel>();

        foreach (var usuario in usuarios)
        {
            perfis.TryGetValue(
                usuario.Id,
                out var perfil);

            var funcoes =
                await _userManager.GetRolesAsync(usuario);

            unidadesPorGerente.TryGetValue(
                usuario.Id,
                out var unidadesGerenciadas);

            resultado.Add(
                new UsuarioListaViewModel
                {
                    UsuarioId = usuario.Id,

                    Email =
                        usuario.Email ??
                        string.Empty,

                    NomeCompleto =
                        perfil?.NomeCompleto ??
                        "Cadastro incompleto",

                    Unidade =
                        perfil?.Unidade?.Nome ??
                        "Não vinculada",

                    UnidadesGerenciadas =
                        funcoes.Contains("Gerente")
                            ? unidadesGerenciadas ??
                              perfil?.Unidade?.Nome
                            : null,

                    Setor =
                        perfil?.Setor?.Nome ??
                        "Não vinculado",

                    Perfis =
                        funcoes.Count > 0
                            ? string.Join(", ", funcoes)
                            : "Sem perfil",

                    Ativo =
                        perfil?.Ativo ??
                        !usuario.LockoutEnd.HasValue
                });
        }

        return View(resultado);
    }

    [HttpGet]
    public async Task<IActionResult> Pendentes()
    {
        var cadastros = await (
            from perfil in _context.FuncionariosPerfis.AsNoTracking()
            join usuario in _context.Users.AsNoTracking()
                on perfil.UsuarioId equals usuario.Id
            where perfil.CadastroPendente
            orderby perfil.CriadoEm
            select new CadastroPendenteListaViewModel
            {
                UsuarioId = usuario.Id,
                NomeCompleto = perfil.NomeCompleto,
                Email = usuario.Email ?? string.Empty,
                Unidade = perfil.Unidade.Nome,
                Setor = perfil.Setor.Nome,
                Cargo = perfil.Cargo,
                SolicitadoEm = perfil.CriadoEm
            }).ToListAsync();

        return View(cadastros);
    }

    [HttpGet]
    public async Task<IActionResult> RevisarCadastro(string id)
    {
        var usuario = await _userManager.FindByIdAsync(id);
        var perfil = usuario is null
            ? null
            : await _context.FuncionariosPerfis
                .AsNoTracking()
                .SingleOrDefaultAsync(item =>
                    item.UsuarioId == usuario.Id &&
                    item.CadastroPendente);

        if (usuario is null || perfil is null)
        {
            return NotFound();
        }

        await CarregarListas(incluirAdministrador: false);

        return View(new RevisarCadastroPendenteViewModel
        {
            UsuarioId = usuario.Id,
            NomeCompleto = perfil.NomeCompleto,
            Email = usuario.Email ?? string.Empty,
            UnidadeId = perfil.UnidadeId,
            SetorId = perfil.SetorId,
            Cargo = perfil.Cargo
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RevisarCadastro(
        RevisarCadastroPendenteViewModel model)
    {
        var usuario = await _userManager.FindByIdAsync(model.UsuarioId);
        var perfil = usuario is null
            ? null
            : await _context.FuncionariosPerfis.SingleOrDefaultAsync(item =>
                item.UsuarioId == usuario.Id &&
                item.CadastroPendente);

        if (usuario is null || perfil is null)
        {
            return NotFound();
        }

        model.Email = (model.Email ?? string.Empty).Trim().ToLowerInvariant();
        await ValidarVinculosOrganizacionais(model.UnidadeId, model.SetorId);

        if (!_selfRegistrationOptions.DominioPermitido(model.Email))
        {
            ModelState.AddModelError(
                nameof(model.Email),
                "Use um e-mail corporativo autorizado do Grupo San Marco.");
        }

        var duplicado = await _userManager.FindByEmailAsync(model.Email);

        if (duplicado is not null && duplicado.Id != usuario.Id)
        {
            ModelState.AddModelError(
                nameof(model.Email),
                "Já existe outro usuário com esse e-mail.");
        }

        if (!ModelState.IsValid)
        {
            await CarregarListas(incluirAdministrador: false);
            return View(model);
        }

        await using var transacao = await _context.Database.BeginTransactionAsync();

        if (!string.Equals(usuario.Email, model.Email, StringComparison.OrdinalIgnoreCase))
        {
            var emailResultado = await _userManager.SetEmailAsync(usuario, model.Email);
            var loginResultado = emailResultado.Succeeded
                ? await _userManager.SetUserNameAsync(usuario, model.Email)
                : emailResultado;

            if (!emailResultado.Succeeded || !loginResultado.Succeeded)
            {
                AdicionarErros(
                    !emailResultado.Succeeded ? emailResultado : loginResultado,
                    nameof(model.Email));
                await transacao.RollbackAsync();
                await CarregarListas(incluirAdministrador: false);
                return View(model);
            }
        }

        perfil.NomeCompleto = model.NomeCompleto.Trim();
        perfil.UnidadeId = model.UnidadeId;
        perfil.SetorId = model.SetorId;
        perfil.Cargo = model.Cargo?.Trim();

        await _context.SaveChangesAsync();
        await _auditoria.RegistrarAsync(
            "Revisão",
            "Cadastros",
            usuario.Id,
            $"Solicitação de {perfil.NomeCompleto} ({model.Email}) revisada; permanece pendente.");
        await transacao.CommitAsync();

        TempData["Sucesso"] = "Cadastro pendente atualizado com sucesso.";
        return RedirectToAction(nameof(Pendentes));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AprovarCadastro(string id)
    {
        var usuario = await _userManager.FindByIdAsync(id);
        var perfil = usuario is null
            ? null
            : await _context.FuncionariosPerfis.SingleOrDefaultAsync(item =>
                item.UsuarioId == usuario.Id &&
                item.CadastroPendente);

        if (usuario is null || perfil is null || string.IsNullOrWhiteSpace(usuario.Email))
        {
            return NotFound();
        }

        await using var transacao = await _context.Database.BeginTransactionAsync();

        usuario.EmailConfirmed = true;
        var atualizarUsuario = await _userManager.UpdateAsync(usuario);
        var liberarUsuario = atualizarUsuario.Succeeded
            ? await _userManager.SetLockoutEndDateAsync(usuario, null)
            : atualizarUsuario;

        if (!atualizarUsuario.Succeeded || !liberarUsuario.Succeeded)
        {
            await transacao.RollbackAsync();
            TempData["Erro"] = "Não foi possível aprovar o cadastro. Tente novamente.";
            return RedirectToAction(nameof(Pendentes));
        }

        perfil.Ativo = true;
        perfil.CadastroPendente = false;
        perfil.DeveTrocarSenha = false;

        _usuarioEmail.EnfileirarCadastroAprovado(
            usuario.Email,
            perfil.NomeCompleto);

        await _context.SaveChangesAsync();
        await _auditoria.RegistrarAsync(
            "Aprovação",
            "Cadastros",
            usuario.Id,
            $"Cadastro de {perfil.NomeCompleto} ({usuario.Email}) aprovado como Funcionário; uma notificação foi enfileirada para o próprio usuário.");
        await transacao.CommitAsync();

        TempData["Sucesso"] = $"Cadastro de {perfil.NomeCompleto} aprovado.";
        return RedirectToAction(nameof(Pendentes));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejeitarCadastro(string id)
    {
        var usuario = await _userManager.FindByIdAsync(id);
        var perfil = usuario is null
            ? null
            : await _context.FuncionariosPerfis.SingleOrDefaultAsync(item =>
                item.UsuarioId == usuario.Id &&
                item.CadastroPendente);

        if (usuario is null || perfil is null)
        {
            return NotFound();
        }

        var descricao = $"Solicitação de {perfil.NomeCompleto} ({usuario.Email}) rejeitada e removida sem envio de e-mail.";

        await using var transacao = await _context.Database.BeginTransactionAsync();
        await _auditoria.RegistrarAsync(
            "Rejeição",
            "Cadastros",
            usuario.Id,
            descricao);

        var resultado = await _userManager.DeleteAsync(usuario);

        if (!resultado.Succeeded)
        {
            await transacao.RollbackAsync();
            TempData["Erro"] = "Não foi possível rejeitar o cadastro. Tente novamente.";
            return RedirectToAction(nameof(Pendentes));
        }

        await transacao.CommitAsync();
        TempData["Sucesso"] = "Solicitação rejeitada e removida. Nenhum e-mail foi enviado.";
        return RedirectToAction(nameof(Pendentes));
    }

    [HttpGet]
    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> Create()
    {
        await CarregarListas(
            incluirAdministrador: true);

        return View(
            new CadastrarFuncionarioViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> Create(
        CadastrarFuncionarioViewModel model)
    {
        var perfisPermitidos = new[]
        {
            "Funcionario",
            "Tecnico",
            "Gerente",
            "Administrador"
        };

        if (!perfisPermitidos.Contains(model.Perfil))
        {
            ModelState.AddModelError(
                nameof(model.Perfil),
                "Perfil de acesso inválido.");
        }

        await ValidarVinculosOrganizacionais(
            model.UnidadeId,
            model.SetorId);

        await ValidarUnidadesGerenciadas(model);

        if (!string.IsNullOrWhiteSpace(model.Email))
        {
            model.Email = model.Email.Trim();

            var usuarioExistente =
                await _userManager.FindByEmailAsync(
                    model.Email);

            if (usuarioExistente is not null)
            {
                ModelState.AddModelError(
                    nameof(model.Email),
                    "Já existe um usuário com esse e-mail.");
            }
        }

        if (!ModelState.IsValid)
        {
            await CarregarListas(
                incluirAdministrador: true);

            return View(model);
        }

        await using var transacao =
            await _context.Database
                .BeginTransactionAsync();

        var usuario = new IdentityUser
        {
            UserName = model.Email,
            Email = model.Email,
            EmailConfirmed = true,
            LockoutEnabled = true
        };

        var resultadoUsuario =
            await _userManager.CreateAsync(
                usuario,
                model.SenhaProvisoria);

        if (!resultadoUsuario.Succeeded)
        {
            AdicionarErros(
                resultadoUsuario,
                nameof(model.SenhaProvisoria));

            await transacao.RollbackAsync();

            await CarregarListas(
                incluirAdministrador: true);

            return View(model);
        }

        var resultadoPerfil =
            await _userManager.AddToRoleAsync(
                usuario,
                model.Perfil);

        if (!resultadoPerfil.Succeeded)
        {
            AdicionarErros(
                resultadoPerfil);

            await transacao.RollbackAsync();

            await CarregarListas(
                incluirAdministrador: true);

            return View(model);
        }

        var funcionario = new FuncionarioPerfil
        {
            UsuarioId = usuario.Id,
            NomeCompleto = model.NomeCompleto.Trim(),
            UnidadeId = model.UnidadeId,
            SetorId = model.SetorId,
            Cargo = model.Cargo?.Trim(),
            Ativo = true,
            DeveTrocarSenha = true,
            CriadoEm = DateTime.Now
        };

        _context.FuncionariosPerfis.Add(
            funcionario);

        if (model.Perfil == "Gerente")
        {
            _context.GerentesUnidades.AddRange(
                model.UnidadesGerenciadasIds
                    .Distinct()
                    .Select(unidadeId =>
                        new GerenteUnidade
                        {
                            UsuarioId = usuario.Id,
                            UnidadeId = unidadeId
                        }));
        }

        await _context.SaveChangesAsync();

        await _auditoria.RegistrarAsync(
            "Cadastro",
            "Usuários",
            usuario.Id,
            $"Usuário {funcionario.NomeCompleto} ({usuario.Email}) " +
            $"cadastrado com perfil {model.Perfil}, unidade " +
            $"{model.UnidadeId} e setor {model.SetorId}.");

        await transacao.CommitAsync();

        TempData["Sucesso"] =
            $"Funcionário {funcionario.NomeCompleto} " +
            "cadastrado com sucesso.";

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> Edit(
        string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return NotFound();
        }

        var usuario =
            await _userManager.FindByIdAsync(id);

        if (usuario is null)
        {
            return NotFound();
        }

        var perfil =
            await _context.FuncionariosPerfis
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    item =>
                        item.UsuarioId == usuario.Id);

        if (perfil?.CadastroPendente == true)
        {
            return RedirectToAction(
                nameof(RevisarCadastro),
                new { id = usuario.Id });
        }

        var funcoes =
            await _userManager.GetRolesAsync(usuario);

        var perfilAtual =
            ObterPerfilPrincipal(funcoes);

        var model = new EditarUsuarioViewModel
        {
            UsuarioId = usuario.Id,
            Email = usuario.Email ?? string.Empty,
            NomeCompleto =
                perfil?.NomeCompleto ??
                string.Empty,
            UnidadeId =
                perfil?.UnidadeId ?? 0,
            SetorId =
                perfil?.SetorId ?? 0,
            Cargo = perfil?.Cargo,
            Perfil = perfilAtual,
            Ativo =
                perfil?.Ativo ??
                !EstaBloqueado(usuario),

            UnidadesGerenciadasIds =
                await _context.GerentesUnidades
                    .AsNoTracking()
                    .Where(item =>
                        item.UsuarioId == usuario.Id)
                    .Select(item => item.UnidadeId)
                    .ToListAsync()
        };

        if (model.Perfil == "Gerente" &&
            model.UnidadesGerenciadasIds.Count == 0 &&
            model.UnidadeId > 0)
        {
            model.UnidadesGerenciadasIds.Add(
                model.UnidadeId);
        }

        await CarregarListas(
            incluirAdministrador: true);

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> Edit(
        EditarUsuarioViewModel model)
    {
        var usuario =
            await _userManager.FindByIdAsync(
                model.UsuarioId);

        if (usuario is null)
        {
            return NotFound();
        }

        var cadastroPendente = await _context.FuncionariosPerfis
            .AsNoTracking()
            .AnyAsync(item =>
                item.UsuarioId == usuario.Id &&
                item.CadastroPendente);

        if (cadastroPendente)
        {
            return RedirectToAction(
                nameof(RevisarCadastro),
                new { id = usuario.Id });
        }

        var perfisPermitidos = new[]
        {
            "Funcionario",
            "Tecnico",
            "Gerente",
            "Administrador"
        };

        if (!perfisPermitidos.Contains(model.Perfil))
        {
            ModelState.AddModelError(
                nameof(model.Perfil),
                "Perfil de acesso inválido.");
        }

        await ValidarVinculosOrganizacionais(
            model.UnidadeId,
            model.SetorId);

        await ValidarUnidadesGerenciadas(model);

        if (!string.IsNullOrWhiteSpace(model.Email))
        {
            model.Email = model.Email.Trim();

            var usuarioComMesmoEmail =
                await _userManager.FindByEmailAsync(
                    model.Email);

            if (usuarioComMesmoEmail is not null &&
                usuarioComMesmoEmail.Id != usuario.Id)
            {
                ModelState.AddModelError(
                    nameof(model.Email),
                    "Já existe outro usuário com esse e-mail.");
            }
        }

        var usuarioLogado =
            await _userManager.GetUserAsync(User);

        var funcoesAtuais =
            await _userManager.GetRolesAsync(usuario);

        var eraAdministrador =
            funcoesAtuais.Contains("Administrador");

        var alterandoPropriaConta =
            usuarioLogado?.Id == usuario.Id;

        if (alterandoPropriaConta &&
            !model.Ativo)
        {
            ModelState.AddModelError(
                nameof(model.Ativo),
                "Você não pode desativar sua própria conta.");
        }

        if (alterandoPropriaConta &&
            eraAdministrador &&
            model.Perfil != "Administrador")
        {
            ModelState.AddModelError(
                nameof(model.Perfil),
                "Você não pode remover seu próprio " +
                "perfil de Administrador.");
        }

        var removendoAdministrador =
            eraAdministrador &&
            (model.Perfil != "Administrador" ||
             !model.Ativo);

        if (removendoAdministrador)
        {
            var administradores =
                await _userManager.GetUsersInRoleAsync(
                    "Administrador");

            var administradoresAtivos = 0;

            foreach (var administrador
                     in administradores)
            {
                var perfilAdministrador =
                    await _context.FuncionariosPerfis
                        .AsNoTracking()
                        .SingleOrDefaultAsync(
                            item =>
                                item.UsuarioId ==
                                administrador.Id);

                var estaAtivo =
                    perfilAdministrador?.Ativo ??
                    !EstaBloqueado(administrador);

                if (estaAtivo)
                {
                    administradoresAtivos++;
                }
            }

            if (administradoresAtivos <= 1)
            {
                ModelState.AddModelError(
                    nameof(model.Perfil),
                    "Não é possível remover ou desativar " +
                    "o último Administrador ativo.");
            }
        }

        if (!ModelState.IsValid)
        {
            await CarregarListas(
                incluirAdministrador: true);

            return View(model);
        }

        var emailFoiAlterado =
            usuario.Email != model.Email;

        await using var transacao =
            await _context.Database
                .BeginTransactionAsync();

        if (emailFoiAlterado)
        {
            var resultadoEmail =
                await _userManager.SetEmailAsync(
                    usuario,
                    model.Email);

            if (!resultadoEmail.Succeeded)
            {
                AdicionarErros(
                    resultadoEmail,
                    nameof(model.Email));

                await transacao.RollbackAsync();

                await CarregarListas(
                    incluirAdministrador: true);

                return View(model);
            }

            var resultadoNomeUsuario =
                await _userManager.SetUserNameAsync(
                    usuario,
                    model.Email);

            if (!resultadoNomeUsuario.Succeeded)
            {
                AdicionarErros(
                    resultadoNomeUsuario,
                    nameof(model.Email));

                await transacao.RollbackAsync();

                await CarregarListas(
                    incluirAdministrador: true);

                return View(model);
            }

            usuario.EmailConfirmed = true;

            var atualizarEmail =
                await _userManager.UpdateAsync(usuario);

            if (!atualizarEmail.Succeeded)
            {
                AdicionarErros(
                    atualizarEmail,
                    nameof(model.Email));

                await transacao.RollbackAsync();

                await CarregarListas(
                    incluirAdministrador: true);

                return View(model);
            }
        }

        var perfil =
            await _context.FuncionariosPerfis
                .SingleOrDefaultAsync(
                    item =>
                        item.UsuarioId == usuario.Id);

        if (perfil is null)
        {
            perfil = new FuncionarioPerfil
            {
                UsuarioId = usuario.Id,
                CriadoEm = DateTime.Now,
                DeveTrocarSenha = false
            };

            _context.FuncionariosPerfis.Add(
                perfil);
        }

        perfil.NomeCompleto =
            model.NomeCompleto.Trim();

        perfil.UnidadeId =
            model.UnidadeId;

        perfil.SetorId =
            model.SetorId;

        perfil.Cargo =
            model.Cargo?.Trim();

        perfil.Ativo =
            model.Ativo;

        var unidadesGerenciadasAtuais =
            await _context.GerentesUnidades
                .Where(item =>
                    item.UsuarioId == usuario.Id)
                .ToListAsync();

        _context.GerentesUnidades.RemoveRange(
            unidadesGerenciadasAtuais);

        if (model.Perfil == "Gerente")
        {
            _context.GerentesUnidades.AddRange(
                model.UnidadesGerenciadasIds
                    .Distinct()
                    .Select(unidadeId =>
                        new GerenteUnidade
                        {
                            UsuarioId = usuario.Id,
                            UnidadeId = unidadeId
                        }));
        }

        var funcoesParaRemover =
            funcoesAtuais
                .Where(funcao =>
                    funcao != model.Perfil)
                .ToList();

        if (funcoesParaRemover.Count > 0)
        {
            var resultadoRemocao =
                await _userManager.RemoveFromRolesAsync(
                    usuario,
                    funcoesParaRemover);

            if (!resultadoRemocao.Succeeded)
            {
                AdicionarErros(resultadoRemocao);

                await transacao.RollbackAsync();

                await CarregarListas(
                    incluirAdministrador: true);

                return View(model);
            }
        }

        if (!await _userManager.IsInRoleAsync(
                usuario,
                model.Perfil))
        {
            var resultadoNovaFuncao =
                await _userManager.AddToRoleAsync(
                    usuario,
                    model.Perfil);

            if (!resultadoNovaFuncao.Succeeded)
            {
                AdicionarErros(resultadoNovaFuncao);

                await transacao.RollbackAsync();

                await CarregarListas(
                    incluirAdministrador: true);

                return View(model);
            }
        }

        var resultadoLockout =
            await _userManager.SetLockoutEnabledAsync(
                usuario,
                true);

        if (!resultadoLockout.Succeeded)
        {
            AdicionarErros(resultadoLockout);

            await transacao.RollbackAsync();

            await CarregarListas(
                incluirAdministrador: true);

            return View(model);
        }

        var resultadoBloqueio =
            await _userManager.SetLockoutEndDateAsync(
                usuario,
                model.Ativo
                    ? null
                    : DateTimeOffset.MaxValue);

        if (!resultadoBloqueio.Succeeded)
        {
            AdicionarErros(resultadoBloqueio);

            await transacao.RollbackAsync();

            await CarregarListas(
                incluirAdministrador: true);

            return View(model);
        }

        var deveInvalidarSessoes =
            !model.Ativo ||
            emailFoiAlterado ||
            funcoesAtuais.Any(funcao =>
                funcao != model.Perfil) ||
            !funcoesAtuais.Contains(model.Perfil);

        if (deveInvalidarSessoes)
        {
            var resultadoSeguranca =
                await _userManager
                    .UpdateSecurityStampAsync(usuario);

            if (!resultadoSeguranca.Succeeded)
            {
                AdicionarErros(resultadoSeguranca);

                await transacao.RollbackAsync();

                await CarregarListas(
                    incluirAdministrador: true);

                return View(model);
            }
        }

        await _context.SaveChangesAsync();

        await _auditoria.RegistrarAsync(
            "Alteração",
            "Usuários",
            usuario.Id,
            $"Cadastro de {perfil.NomeCompleto} ({usuario.Email}) " +
            $"atualizado para perfil {model.Perfil}, conta " +
            $"{(model.Ativo ? "ativa" : "bloqueada")}, unidade " +
            $"{model.UnidadeId} e setor {model.SetorId}.");

        await transacao.CommitAsync();

        TempData["Sucesso"] =
            $"Usuário {perfil.NomeCompleto} " +
            "atualizado com sucesso.";

        return RedirectToAction(nameof(Index));
    }

[HttpGet]
[Authorize(Roles = "Administrador")]
public async Task<IActionResult> ResetarSenha(
    string id)
{
    if (string.IsNullOrWhiteSpace(id))
    {
        return NotFound();
    }

    var usuario =
        await _userManager.FindByIdAsync(id);

    if (usuario is null)
    {
        return NotFound();
    }

    var perfil =
        await _context.FuncionariosPerfis
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item =>
                    item.UsuarioId == usuario.Id);

    var model =
        new ResetarSenhaUsuarioViewModel
        {
            UsuarioId = usuario.Id,

            NomeCompleto =
                perfil?.NomeCompleto ??
                "Cadastro incompleto",

            Email =
                usuario.Email ??
                string.Empty
        };

    return View(model);
}

[HttpPost]
[ValidateAntiForgeryToken]
[Authorize(Roles = "Administrador")]
public async Task<IActionResult> ResetarSenha(
    ResetarSenhaUsuarioViewModel model)
{
    var usuario =
        await _userManager.FindByIdAsync(
            model.UsuarioId);

    if (usuario is null)
    {
        return NotFound();
    }

    var perfil =
        await _context.FuncionariosPerfis
            .SingleOrDefaultAsync(
                item =>
                    item.UsuarioId == usuario.Id);

    model.NomeCompleto =
        perfil?.NomeCompleto ??
        "Cadastro incompleto";

    model.Email =
        usuario.Email ??
        string.Empty;

    if (!ModelState.IsValid)
    {
        return View(model);
    }

    var token =
        await _userManager
            .GeneratePasswordResetTokenAsync(usuario);

    var resultado =
        await _userManager.ResetPasswordAsync(
            usuario,
            token,
            model.NovaSenha);

    if (!resultado.Succeeded)
    {
        AdicionarErros(
            resultado,
            nameof(model.NovaSenha));

        return View(model);
    }

    if (perfil is not null)
    {
        perfil.DeveTrocarSenha = true;

        await _context.SaveChangesAsync();
    }

    await _userManager
        .UpdateSecurityStampAsync(usuario);

    await _auditoria.RegistrarAsync(
        "Redefinição de senha",
        "Usuários",
        usuario.Id,
        $"Senha provisória de {model.NomeCompleto} ({model.Email}) " +
        "redefinida; troca obrigatória ativada.");

    TempData["Sucesso"] =
        $"Senha de {model.NomeCompleto} " +
        "redefinida com sucesso.";

    return RedirectToAction(nameof(Index));
}
    private async Task CarregarListas(
        bool incluirAdministrador)
    {
        var unidades =
            await _context.Unidades
                .AsNoTracking()
                .Where(unidade => unidade.Ativa)
                .OrderBy(unidade => unidade.Nome)
                .ToListAsync();

        var setores =
            await _context.Setores
                .AsNoTracking()
                .Where(setor => setor.Ativo)
                .OrderBy(setor => setor.Nome)
                .ToListAsync();

        ViewBag.Unidades = new SelectList(
            unidades,
            "Id",
            "Nome");

        ViewBag.Setores = new SelectList(
            setores,
            "Id",
            "Nome");

        var perfis = new List<object>
        {
            new
            {
                Valor = "Funcionario",
                Texto = "Funcionário"
            },
            new
            {
                Valor = "Tecnico",
                Texto = "Técnico"
            },
            new
            {
                Valor = "Gerente",
                Texto = "Gerente"
            }
        };

        if (incluirAdministrador)
        {
            perfis.Add(
                new
                {
                    Valor = "Administrador",
                    Texto = "Administrador"
                });
        }

        ViewBag.Perfis = new SelectList(
            perfis,
            "Valor",
            "Texto");
    }

    private void AdicionarErros(
        IdentityResult resultado,
        string propriedade = "")
    {
        foreach (var erro in resultado.Errors)
        {
            ModelState.AddModelError(
                propriedade,
                erro.Description);
        }
    }

    private async Task ValidarUnidadesGerenciadas(
        CadastrarFuncionarioViewModel model)
    {
        model.UnidadesGerenciadasIds =
            model.UnidadesGerenciadasIds
                .Where(id => id > 0)
                .Distinct()
                .ToList();

        if (model.Perfil != "Gerente")
        {
            model.UnidadesGerenciadasIds.Clear();
            return;
        }

        if (model.UnidadesGerenciadasIds.Count == 0)
        {
            ModelState.AddModelError(
                nameof(model.UnidadesGerenciadasIds),
                "Selecione ao menos uma unidade para o gerente.");
            return;
        }

        var unidadesValidas =
            await _context.Unidades.CountAsync(unidade =>
                unidade.Ativa &&
                model.UnidadesGerenciadasIds.Contains(unidade.Id));

        if (unidadesValidas !=
            model.UnidadesGerenciadasIds.Count)
        {
            ModelState.AddModelError(
                nameof(model.UnidadesGerenciadasIds),
                "Uma das unidades selecionadas é inválida.");
        }
    }

    private async Task ValidarVinculosOrganizacionais(
        int unidadeId,
        int setorId)
    {
        var unidadeValida =
            await _context.Unidades
                .AsNoTracking()
                .AnyAsync(unidade =>
                    unidade.Id == unidadeId &&
                    unidade.Ativa);

        if (!unidadeValida)
        {
            ModelState.AddModelError(
                "UnidadeId",
                "Selecione uma unidade ativa.");
        }

        var setorValido =
            await _context.Setores
                .AsNoTracking()
                .AnyAsync(setor =>
                    setor.Id == setorId &&
                    setor.Ativo);

        if (!setorValido)
        {
            ModelState.AddModelError(
                "SetorId",
                "Selecione um setor ativo.");
        }
    }

    private async Task ValidarUnidadesGerenciadas(
        EditarUsuarioViewModel model)
    {
        model.UnidadesGerenciadasIds =
            model.UnidadesGerenciadasIds
                .Where(id => id > 0)
                .Distinct()
                .ToList();

        if (model.Perfil != "Gerente")
        {
            model.UnidadesGerenciadasIds.Clear();
            return;
        }

        if (model.UnidadesGerenciadasIds.Count == 0)
        {
            ModelState.AddModelError(
                nameof(model.UnidadesGerenciadasIds),
                "Selecione ao menos uma unidade para o gerente.");
            return;
        }

        var unidadesValidas =
            await _context.Unidades.CountAsync(unidade =>
                unidade.Ativa &&
                model.UnidadesGerenciadasIds.Contains(unidade.Id));

        if (unidadesValidas !=
            model.UnidadesGerenciadasIds.Count)
        {
            ModelState.AddModelError(
                nameof(model.UnidadesGerenciadasIds),
                "Uma das unidades selecionadas é inválida.");
        }
    }

    private static string ObterPerfilPrincipal(
        IEnumerable<string> funcoes)
    {
        if (funcoes.Contains("Administrador"))
        {
            return "Administrador";
        }

        if (funcoes.Contains("Gerente"))
        {
            return "Gerente";
        }

        if (funcoes.Contains("Tecnico"))
        {
            return "Tecnico";
        }

        return "Funcionario";
    }

    private static bool EstaBloqueado(
        IdentityUser usuario)
    {
        return usuario.LockoutEnd.HasValue &&
               usuario.LockoutEnd.Value >
               DateTimeOffset.UtcNow;
    }
}
