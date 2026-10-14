using System.Diagnostics;
using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using PortalSanMarco.Data;
using PortalSanMarco.Models;
using PortalSanMarco.Services;
using PortalSanMarco.ViewModels;

namespace PortalSanMarco.Controllers;

[Authorize]
public class ContaController : Controller
{
    private readonly UserManager<IdentityUser>
        _userManager;

    private readonly SignInManager<IdentityUser>
        _signInManager;

    private readonly ApplicationDbContext
        _context;

    private readonly IPortalEmailSender
        _emailSender;

    private readonly IMemoryCache
        _cache;

    private readonly IConfiguration
        _configuration;

    private readonly ILogger<ContaController>
        _logger;

    private readonly IWebHostEnvironment
        _environment;

    private readonly SelfRegistrationOptions
        _selfRegistrationOptions;

    private readonly IAuditoriaService
        _auditoria;

    public ContaController(
        UserManager<IdentityUser> userManager,
        SignInManager<IdentityUser> signInManager,
        ApplicationDbContext context,
        IPortalEmailSender emailSender,
        IMemoryCache cache,
        IConfiguration configuration,
        ILogger<ContaController> logger,
        IWebHostEnvironment environment,
        IOptions<SelfRegistrationOptions> selfRegistrationOptions,
        IAuditoriaService auditoria)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _context = context;
        _emailSender = emailSender;
        _cache = cache;
        _configuration = configuration;
        _logger = logger;
        _environment = environment;
        _selfRegistrationOptions = selfRegistrationOptions.Value;
        _auditoria = auditoria;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Entrar(
        string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            var usuario =
                await _userManager.GetUserAsync(User);

            if (usuario is not null)
            {
                return await RedirecionarPorPerfil(
                    usuario);
            }
        }

        ViewBag.ReturnUrl = returnUrl;

        return View(new LoginViewModel());
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("Autenticacao")]
    public async Task<IActionResult> Entrar(
        LoginViewModel model,
        string? returnUrl = null)
    {
        ViewBag.ReturnUrl = returnUrl;

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var email = model.Email.Trim();

        var usuario =
            await _userManager.FindByEmailAsync(
                email);

        if (usuario is null)
        {
            ModelState.AddModelError(
                string.Empty,
                "E-mail ou senha inválidos.");

            return View(model);
        }

        var perfilAcesso = await _context.FuncionariosPerfis
            .AsNoTracking()
            .SingleOrDefaultAsync(perfil =>
                perfil.UsuarioId == usuario.Id);

        if (perfilAcesso is null || !perfilAcesso.Ativo)
        {
            ModelState.AddModelError(
                string.Empty,
                perfilAcesso?.CadastroPendente == true
                    ? "Seu cadastro está aguardando aprovação da equipe de TI."
                    : "Esta conta está desativada. Procure a equipe de TI.");

            return View(model);
        }

        var resultado =
            await _signInManager.PasswordSignInAsync(
                usuario,
                model.Senha,
                model.ManterConectado,
                lockoutOnFailure: true);

        if (resultado.Succeeded)
        {
            if (!string.IsNullOrWhiteSpace(returnUrl) &&
                Url.IsLocalUrl(returnUrl))
            {
                return LocalRedirect(returnUrl);
            }

            return await RedirecionarPorPerfil(
                usuario);
        }

        if (resultado.IsLockedOut)
        {
            ModelState.AddModelError(
                string.Empty,
                "Esta conta está desativada ou " +
                "temporariamente bloqueada. " +
                "Procure a equipe de TI.");

            return View(model);
        }

        if (resultado.RequiresTwoFactor)
        {
            return RedirectToPage(
                "/Account/LoginWith2fa",
                new
                {
                    area = "Identity",
                    ReturnUrl = returnUrl,
                    RememberMe =
                        model.ManterConectado
                });
        }

        ModelState.AddModelError(
            string.Empty,
            "E-mail ou senha inválidos.");

        return View(model);
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult EsqueciSenha()
    {
        return View(new EsqueciSenhaViewModel());
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("RecuperacaoSenha")]
    public async Task<IActionResult> EsqueciSenha(
        EsqueciSenhaViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var inicio = Stopwatch.GetTimestamp();
        var email = model.Email.Trim();
        var emailNormalizado =
            _userManager.NormalizeEmail(email);
        var chaveLimite =
            $"recuperacao-senha:{emailNormalizado}";

        try
        {
            var usuario =
                await _userManager.FindByEmailAsync(
                    email);

            var perfilAtivo =
                usuario is not null &&
                await _context.FuncionariosPerfis
                    .AsNoTracking()
                    .AnyAsync(
                        perfil =>
                            perfil.UsuarioId == usuario.Id &&
                            perfil.Ativo,
                        cancellationToken);

            if (usuario is not null &&
                perfilAtivo &&
                !_cache.TryGetValue(chaveLimite, out _))
            {
                var token =
                    await _userManager
                        .GeneratePasswordResetTokenAsync(
                            usuario);

                var caminho =
                    Url.Action(
                        nameof(RedefinirSenha),
                        "Conta",
                        new
                        {
                            usuarioId = usuario.Id,
                            token
                        });

                var baseUrl =
                    _configuration["Portal:BaseUrl"]
                        ?.TrimEnd('/');

                if (string.IsNullOrWhiteSpace(baseUrl) ||
                    string.IsNullOrWhiteSpace(caminho))
                {
                    throw new InvalidOperationException(
                        "Endereço público do portal não configurado.");
                }

                var link = baseUrl + caminho;
                var nome =
                    await _context.FuncionariosPerfis
                        .AsNoTracking()
                        .Where(perfil =>
                            perfil.UsuarioId == usuario.Id)
                        .Select(perfil =>
                            perfil.NomeCompleto)
                        .SingleAsync(cancellationToken);

                await _emailSender.EnviarAsync(
                    usuario.Email!,
                    "Redefinição de senha - Portal San Marco",
                    MontarEmailRecuperacao(nome, link),
                    cancellationToken);

                _cache.Set(
                    chaveLimite,
                    true,
                    TimeSpan.FromMinutes(5));
            }
        }
        catch (Exception excecao)
            when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(
                excecao,
                "Falha ao processar recuperação de senha.");

            await RegistrarFalhaRecuperacaoAsync(
                excecao,
                cancellationToken);
        }

        var decorrido =
            Stopwatch.GetElapsedTime(inicio);
        var tempoMinimo =
            TimeSpan.FromMilliseconds(800);

        if (decorrido < tempoMinimo)
        {
            await Task.Delay(
                tempoMinimo - decorrido,
                cancellationToken);
        }

        return RedirectToAction(
            nameof(SolicitacaoSenhaRecebida));
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult SolicitacaoSenhaRecebida()
    {
        return View();
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> SolicitarAcesso()
    {
        await CarregarListasAutocadastroAsync();
        return View(new AutocadastroViewModel());
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("Autocadastro")]
    public async Task<IActionResult> SolicitarAcesso(
        AutocadastroViewModel model)
    {
        model.Email = (model.Email ?? string.Empty)
            .Trim()
            .ToLowerInvariant();

        if (!_selfRegistrationOptions.DominioPermitido(model.Email))
        {
            ModelState.AddModelError(
                nameof(model.Email),
                "Use um e-mail corporativo autorizado do Grupo San Marco.");
        }

        var unidadeValida = await _context.Unidades
            .AsNoTracking()
            .AnyAsync(item => item.Id == model.UnidadeId && item.Ativa);

        var setorValido = await _context.Setores
            .AsNoTracking()
            .AnyAsync(item => item.Id == model.SetorId && item.Ativo);

        if (!unidadeValida)
        {
            ModelState.AddModelError(
                nameof(model.UnidadeId),
                "Selecione uma unidade ativa.");
        }

        if (!setorValido)
        {
            ModelState.AddModelError(
                nameof(model.SetorId),
                "Selecione um setor ativo.");
        }

        if (await _userManager.FindByEmailAsync(model.Email) is not null)
        {
            ModelState.AddModelError(
                nameof(model.Email),
                "Já existe um cadastro com esse e-mail. Use a recuperação de senha ou procure a TI.");
        }

        if (!ModelState.IsValid)
        {
            await CarregarListasAutocadastroAsync();
            return View(model);
        }

        await using var transacao =
            await _context.Database.BeginTransactionAsync();

        var usuario = new IdentityUser
        {
            UserName = model.Email,
            Email = model.Email,
            EmailConfirmed = false,
            LockoutEnabled = true,
            LockoutEnd = DateTimeOffset.MaxValue
        };

        var resultadoUsuario = await _userManager.CreateAsync(
            usuario,
            model.Senha);

        if (!resultadoUsuario.Succeeded)
        {
            foreach (var erro in resultadoUsuario.Errors)
            {
                ModelState.AddModelError(nameof(model.Senha), erro.Description);
            }

            await transacao.RollbackAsync();
            await CarregarListasAutocadastroAsync();
            return View(model);
        }

        var resultadoPerfil = await _userManager.AddToRoleAsync(
            usuario,
            "Funcionario");

        if (!resultadoPerfil.Succeeded)
        {
            foreach (var erro in resultadoPerfil.Errors)
            {
                ModelState.AddModelError(string.Empty, erro.Description);
            }

            await transacao.RollbackAsync();
            await CarregarListasAutocadastroAsync();
            return View(model);
        }

        var perfil = new FuncionarioPerfil
        {
            UsuarioId = usuario.Id,
            NomeCompleto = model.NomeCompleto.Trim(),
            UnidadeId = model.UnidadeId,
            SetorId = model.SetorId,
            Cargo = model.Cargo?.Trim(),
            Ativo = false,
            CadastroPendente = true,
            DeveTrocarSenha = false,
            CriadoEm = DateTime.Now
        };

        _context.FuncionariosPerfis.Add(perfil);
        await _context.SaveChangesAsync();

        await _auditoria.RegistrarAsync(
            "Solicitação",
            "Cadastros",
            usuario.Id,
            $"Solicitação de acesso recebida para {perfil.NomeCompleto} ({usuario.Email}), unidade {perfil.UnidadeId} e setor {perfil.SetorId}.");

        await transacao.CommitAsync();

        return RedirectToAction(nameof(CadastroSolicitado));
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult CadastroSolicitado()
    {
        return View();
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult RedefinirSenha(
        string? usuarioId,
        string? token)
    {
        if (string.IsNullOrWhiteSpace(usuarioId) ||
            string.IsNullOrWhiteSpace(token))
        {
            return RedirectToAction(
                nameof(EsqueciSenha));
        }

        return View(
            new RedefinirSenhaViewModel
            {
                UsuarioId = usuarioId,
                Token = token
            });
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("RecuperacaoSenha")]
    public async Task<IActionResult> RedefinirSenha(
        RedefinirSenhaViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var usuario =
            await _userManager.FindByIdAsync(
                model.UsuarioId);

        if (usuario is null)
        {
            ModelState.AddModelError(
                string.Empty,
                "Este link é inválido ou expirou. " +
                "Solicite uma nova recuperação de senha.");

            return View(model);
        }

        var perfil =
            await _context.FuncionariosPerfis
                .SingleOrDefaultAsync(
                    item =>
                        item.UsuarioId == usuario.Id);

        if (perfil is null || !perfil.Ativo)
        {
            ModelState.AddModelError(
                string.Empty,
                "Não foi possível redefinir a senha. " +
                "Procure a equipe de TI.");

            return View(model);
        }

        var resultado =
            await _userManager.ResetPasswordAsync(
                usuario,
                model.Token,
                model.NovaSenha);

        if (!resultado.Succeeded)
        {
            ModelState.AddModelError(
                string.Empty,
                "Este link é inválido ou expirou. " +
                "Solicite uma nova recuperação de senha.");

            return View(model);
        }

        perfil.DeveTrocarSenha = false;
        await _context.SaveChangesAsync();

        TempData["Sucesso"] =
            "Senha redefinida com sucesso. " +
            "Você já pode entrar no portal.";

        return RedirectToAction(nameof(Entrar));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Sair()
    {
        await _signInManager.SignOutAsync();

        return RedirectToAction(
            nameof(Entrar));
    }

[HttpGet]
[AllowAnonymous]
public IActionResult AcessoNegado()
{
    Response.StatusCode = 403;

    return View();
}

    [HttpGet]
    public IActionResult TrocarSenha()
    {
        return View(
            new TrocarSenhaViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TrocarSenha(
        TrocarSenhaViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var usuario =
            await _userManager.GetUserAsync(User);

        if (usuario is null)
        {
            return Challenge();
        }

        var resultado =
            await _userManager.ChangePasswordAsync(
                usuario,
                model.SenhaAtual,
                model.NovaSenha);

        if (!resultado.Succeeded)
        {
            foreach (var erro in resultado.Errors)
            {
                ModelState.AddModelError(
                    string.Empty,
                    erro.Description);
            }

            return View(model);
        }

        var perfil =
            await _context.FuncionariosPerfis
                .SingleOrDefaultAsync(
                    item =>
                        item.UsuarioId == usuario.Id);

        if (perfil is not null)
        {
            perfil.DeveTrocarSenha = false;

            await _context.SaveChangesAsync();
        }

        await _signInManager
            .RefreshSignInAsync(usuario);

        TempData["Sucesso"] =
            "Senha alterada com sucesso.";

        return await RedirecionarPorPerfil(
            usuario);
    }

    private async Task<IActionResult>
        RedirecionarPorPerfil(
            IdentityUser usuario)
    {
        var podeAcessarDashboard =
            await _userManager.IsInRoleAsync(
                usuario,
                "Tecnico") ||

            await _userManager.IsInRoleAsync(
                usuario,
                "Gerente") ||

            await _userManager.IsInRoleAsync(
                usuario,
                "Administrador");

        if (podeAcessarDashboard)
        {
            return RedirectToAction(
                "Index",
                "Dashboard");
        }

        return RedirectToAction(
            "Index",
            "Chamados");
    }

    private static string MontarEmailRecuperacao(
        string nome,
        string link)
    {
        var nomeSeguro = WebUtility.HtmlEncode(nome);
        var linkSeguro = WebUtility.HtmlEncode(link);

        return $$"""
            <!doctype html>
            <html lang="pt-BR">
            <body style="margin:0;background:#f4f7f5;font-family:Arial,sans-serif;color:#24352d">
              <div style="max-width:600px;margin:32px auto;background:#ffffff;border-radius:14px;overflow:hidden;border:1px solid #dfe9e3">
                <div style="padding:24px 30px;background:#087443;color:#ffffff">
                  <div style="font-size:13px;text-transform:uppercase;letter-spacing:.08em;opacity:.8">Grupo San Marco</div>
                  <h1 style="margin:8px 0 0;font-size:24px">Redefinição de senha</h1>
                </div>
                <div style="padding:30px">
                  <p>Olá, {{nomeSeguro}}.</p>
                  <p>Recebemos uma solicitação para redefinir sua senha do Portal San Marco.</p>
                  <p style="margin:28px 0">
                    <a href="{{linkSeguro}}" style="display:inline-block;padding:13px 22px;background:#198754;color:#ffffff;text-decoration:none;border-radius:8px;font-weight:bold">Criar nova senha</a>
                  </p>
                  <p style="font-size:14px;color:#65756d">Este link expira em 30 minutos e só pode ser utilizado uma vez.</p>
                  <p style="font-size:14px;color:#65756d">Se você não fez esta solicitação, ignore esta mensagem. Sua senha atual continuará funcionando.</p>
                </div>
              </div>
            </body>
            </html>
            """;
    }

    private async Task RegistrarFalhaRecuperacaoAsync(
        Exception excecao,
        CancellationToken cancellationToken)
    {
        try
        {
            var diretorio =
                Path.Combine(
                    _environment.ContentRootPath,
                    "DataProtectionKeys");

            Directory.CreateDirectory(diretorio);

            var caminho =
                Path.Combine(
                    diretorio,
                    "password-recovery-errors.log");

            var registro =
                $"[{DateTime.Now:O}] {excecao}\n\n";

            await System.IO.File.AppendAllTextAsync(
                caminho,
                registro,
                cancellationToken);
        }
        catch
        {
            // O diagnóstico nunca deve alterar a resposta genérica
            // do fluxo de recuperação de senha.
        }
    }

    private async Task CarregarListasAutocadastroAsync()
    {
        ViewBag.Unidades = new SelectList(
            await _context.Unidades
                .AsNoTracking()
                .Where(item => item.Ativa)
                .OrderBy(item => item.Nome)
                .ToListAsync(),
            "Id",
            "Nome");

        ViewBag.Setores = new SelectList(
            await _context.Setores
                .AsNoTracking()
                .Where(item => item.Ativo)
                .OrderBy(item => item.Nome)
                .ToListAsync(),
            "Id",
            "Nome");
    }
}
