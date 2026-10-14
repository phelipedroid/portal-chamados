using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PortalSanMarco.Data;
using PortalSanMarco.Models;
using PortalSanMarco.Services;
using PortalSanMarco.ViewModels;

namespace PortalSanMarco.Controllers;

[Authorize]
public class ChamadosController : Controller
{
    private readonly ApplicationDbContext _context;

    private readonly UserManager<IdentityUser> _userManager;

    private readonly IAuthorizationService _authorizationService;

    private readonly IChamadoAccessService _chamadoAccessService;

    private readonly ISlaService _slaService;

    private readonly IChamadoEmailNotificationService
        _emailNotifications;

    public ChamadosController(
        ApplicationDbContext context,
        UserManager<IdentityUser> userManager,
        IAuthorizationService authorizationService,
        IChamadoAccessService chamadoAccessService,
        ISlaService slaService,
        IChamadoEmailNotificationService emailNotifications)
    {
        _context = context;
        _userManager = userManager;
        _authorizationService = authorizationService;
        _chamadoAccessService = chamadoAccessService;
        _slaService = slaService;
        _emailNotifications = emailNotifications;
    }

    [HttpGet]
public async Task<IActionResult> Index(
    string? busca,
    StatusChamado? status,
    PrioridadeChamado? prioridade,
    string? responsavel,
    string ordem = "recentes",
    int pagina = 1)
{
    var usuario =
        await _userManager.GetUserAsync(User);

    if (usuario is null)
    {
        return Challenge();
    }

    var consulta =
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
                chamado.TecnicoResponsavel)
            .AsQueryable();

    var podeVisualizarTodos =
        await _authorizationService.AuthorizeAsync(
            User,
            "PodeVisualizarTodosChamados");

    consulta = await _chamadoAccessService
        .AplicarFiltroAsync(consulta, User);

    ViewBag.TotalAbertos =
        await consulta.CountAsync(
            chamado =>
                chamado.Status ==
                StatusChamado.Aberto);

    ViewBag.TotalEmAndamento =
        await consulta.CountAsync(
            chamado =>
                chamado.Status ==
                StatusChamado.EmAndamento);

    ViewBag.TotalAguardando =
        await consulta.CountAsync(
            chamado =>
                chamado.Status ==
                    StatusChamado.AguardandoFuncionario ||

                chamado.Status ==
                    StatusChamado.AguardandoFornecedor);

    ViewBag.TotalFinalizados =
        await consulta.CountAsync(
            chamado =>
                chamado.Status ==
                    StatusChamado.Resolvido ||

                chamado.Status ==
                    StatusChamado.Fechado);

    if (!string.IsNullOrWhiteSpace(busca))
    {
        busca =
            busca.Trim();

        if (int.TryParse(
                busca.TrimStart('#'),
                out var numeroChamado))
        {
            consulta =
                consulta.Where(
                    chamado =>
                        chamado.Id == numeroChamado ||
                        chamado.Titulo.Contains(busca));
        }
        else
        {
            consulta =
                consulta.Where(
                    chamado =>
                        chamado.Titulo.Contains(busca) ||
                        chamado.Descricao.Contains(busca));
        }
    }

    if (status.HasValue)
    {
        consulta =
            consulta.Where(
                chamado =>
                    chamado.Status ==
                    status.Value);
    }

    if (prioridade.HasValue)
    {
        consulta =
            consulta.Where(
                chamado =>
                    chamado.Prioridade ==
                    prioridade.Value);
    }

    if (podeVisualizarTodos.Succeeded &&
        !string.IsNullOrWhiteSpace(responsavel))
    {
        responsavel =
            responsavel.Trim();

        if (responsavel == "sem-responsavel")
        {
            consulta =
                consulta.Where(
                    chamado =>
                        chamado.TecnicoResponsavelId ==
                        null);
        }
        else
        {
            consulta =
                consulta.Where(
                    chamado =>
                        chamado.TecnicoResponsavelId ==
                        responsavel);
        }
    }

    consulta =
        ordem switch
        {
            "antigos" =>
                consulta.OrderBy(
                    chamado =>
                        chamado.CriadoEm),

            "prioridade" =>
                consulta
                    .OrderByDescending(
                        chamado =>
                            chamado.Prioridade)
                    .ThenByDescending(
                        chamado =>
                            chamado.CriadoEm),

            "numero" =>
                consulta.OrderByDescending(
                    chamado =>
                        chamado.Id),

            _ =>
                consulta.OrderByDescending(
                    chamado =>
                        chamado.CriadoEm)
        };

    const int itensPorPagina = 10;

    var totalItens =
        await consulta.CountAsync();

    var totalPaginas =
        Math.Max(
            1,
            (int)Math.Ceiling(
                totalItens /
                (double)itensPorPagina));

    pagina =
        Math.Clamp(
            pagina,
            1,
            totalPaginas);

    var chamados =
        await consulta
            .Skip(
                (pagina - 1) *
                itensPorPagina)
            .Take(itensPorPagina)
            .ToListAsync();

    ViewBag.Busca =
        busca;

    ViewBag.StatusSelecionado =
        status;

    ViewBag.PrioridadeSelecionada =
        prioridade;

    ViewBag.ResponsavelSelecionado =
        responsavel;

    ViewBag.OrdemSelecionada =
        ordem;

    ViewBag.PodeFiltrarResponsavel =
        podeVisualizarTodos.Succeeded;

    ViewBag.PaginaAtual =
        pagina;

    ViewBag.TotalPaginas =
        totalPaginas;

    ViewBag.TotalItens =
        totalItens;

    ViewBag.ItensPorPagina =
        itensPorPagina;

    if (podeVisualizarTodos.Succeeded)
    {
        var responsavelSelecionado =
            responsavel == "sem-responsavel"
                ? null
                : responsavel;

        await CarregarTecnicosParaTransferencia(
            responsavelSelecionado);

        ViewBag.ResponsaveisFiltro =
            ViewBag.TecnicosTransferencia;
    }

    return View(chamados);
}
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var usuario =
            await _userManager.GetUserAsync(User);

        if (usuario is null)
        {
            return Challenge();
        }

        var model =
            new NovoChamadoViewModel();

        var perfil =
            await _context.FuncionariosPerfis
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    item =>
                        item.UsuarioId == usuario.Id);

        if (perfil is not null)
        {
            model.UnidadeId = perfil.UnidadeId;

            model.SetorId = perfil.SetorId;
        }

        await CarregarListas();

        return View(model);
    }

   [HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> Create(
    NovoChamadoViewModel model)
{
    var usuario = await _userManager.GetUserAsync(User);

    if (usuario is null)
    {
        return Challenge();
    }

    await ValidarNovoChamadoAsync(model);

    if (!ModelState.IsValid)
    {
        await CarregarListas();

        return View(model);
    }

    var criadoEm = DateTime.Now;

    var chamado = new Chamado
    {
        Titulo = model.Titulo.Trim(),
        Descricao = model.Descricao.Trim(),
        CategoriaId = model.CategoriaId,
        Prioridade = model.Prioridade,
        UnidadeId = model.UnidadeId,
        SetorId = model.SetorId,
        ComputadorPatrimonio =
            model.ComputadorPatrimonio?.Trim(),
        SolicitanteId = usuario.Id,
        Status = StatusChamado.Aberto,
        CriadoEm = criadoEm,
        PrazoEm = _slaService.CalcularPrazo(
            criadoEm,
            model.Prioridade)
    };

    _context.Chamados.Add(chamado);

    await _context.SaveChangesAsync();

    RegistrarHistorico(
        chamado.Id,
        usuario.Id,
        "Chamado aberto",
        $"Chamado aberto com prioridade " +
        $"{ObterNomePrioridade(chamado.Prioridade)}. " +
        $"Prazo previsto: " +
        $"{chamado.PrazoEm:dd/MM/yyyy HH:mm}.");

    await _emailNotifications.EnfileirarAsync(
        chamado.Id,
        usuario.Id,
        "Chamado aberto",
        "Recebemos sua solicitação. Você pode acompanhar " +
        "todas as atualizações pelo botão abaixo.",
        incluirAutor: true);

    await _context.SaveChangesAsync();

    TempData["Sucesso"] =
        $"Chamado #{chamado.Id:D6} aberto com sucesso.";

    return RedirectToAction(nameof(Index));
}

    [HttpGet]
    public async Task<IActionResult> Details(
        int id)
    {
        var usuario =
            await _userManager.GetUserAsync(User);

        if (usuario is null)
        {
            return Challenge();
        }

        var chamado =
            await _context.Chamados
                .AsNoTracking()
                .Include(item => item.Categoria)
                .Include(item => item.Unidade)
                .Include(item => item.Setor)
                .Include(item => item.Solicitante)
                .Include(item => item.TecnicoResponsavel)
                .SingleOrDefaultAsync(
                    item => item.Id == id);

        if (chamado is null)
        {
            return NotFound();
        }

        if (!await _chamadoAccessService
                .PodeVisualizarAsync(chamado.Id, User))
        {
            return Forbid();
        }

        var visualizacao =
            await _context.ChamadosVisualizacoes
                .SingleOrDefaultAsync(item =>
                    item.UsuarioId == usuario.Id &&
                    item.ChamadoId == chamado.Id);

        if (visualizacao is null)
        {
            _context.ChamadosVisualizacoes.Add(
                new ChamadoVisualizacao
                {
                    UsuarioId = usuario.Id,
                    ChamadoId = chamado.Id,
                    VisualizadoEm = DateTime.Now
                });
        }
        else
        {
            visualizacao.VisualizadoEm = DateTime.Now;
        }

        await _context.SaveChangesAsync();

        var podeAtender =
            await _authorizationService.AuthorizeAsync(
                User,
                "PodeAtenderChamados");

        var historicos =
            await _context.HistoricosChamados
                .AsNoTracking()
                .Include(historico => historico.Usuario)
                .Where(historico =>
                    historico.ChamadoId == chamado.Id)
                .OrderByDescending(historico =>
                    historico.CriadoEm)
                .ToListAsync();

        var comentarios =
            await _context.ComentariosChamados
                .AsNoTracking()
                .Include(comentario => comentario.Autor)
                .Where(comentario =>
                    comentario.ChamadoId == chamado.Id)
                .OrderBy(comentario =>
                    comentario.CriadoEm)
                .ToListAsync();

        var anexos =
            await _context.AnexosChamados
                .AsNoTracking()
                .Where(anexo =>
                    anexo.ChamadoId == chamado.Id)
                .OrderByDescending(anexo =>
                    anexo.CriadoEm)
                .Select(anexo =>
                    new AnexoListaViewModel
                    {
                        Id =
                            anexo.Id,

                        NomeOriginal =
                            anexo.NomeOriginal,

                        TipoConteudo =
                            anexo.TipoConteudo,

                        TamanhoBytes =
                            anexo.TamanhoBytes,

                        AutorEmail =
                            anexo.Autor.Email ??
                            string.Empty,

                        CriadoEm =
                            anexo.CriadoEm
                    })
                .ToListAsync();

        ViewBag.PodeAtender =
            podeAtender.Succeeded;

        ViewBag.PodeInteragir =
            podeAtender.Succeeded ||
            chamado.SolicitanteId == usuario.Id;

        ViewBag.Historicos =
            historicos;

        ViewBag.Comentarios =
            comentarios;

        ViewBag.Anexos =
            anexos;

        var podeTransferir =
            User.IsInRole("Administrador");

        ViewBag.PodeTransferir =
            podeTransferir;

        if (podeTransferir &&
            chamado.Status != StatusChamado.Fechado)
        {
            await CarregarTecnicosParaTransferencia(
                chamado.TecnicoResponsavelId);
        }

        return View(chamado);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(
        Policy =
            "PodeAtenderChamados")]
    public async Task<IActionResult> Assumir(
        int id)
    {
        var usuario =
            await _userManager.GetUserAsync(User);

        if (usuario is null)
        {
            return Challenge();
        }

        var chamado =
            await _context.Chamados
                .SingleOrDefaultAsync(
                    item => item.Id == id);

        if (chamado is null)
        {
            return NotFound();
        }

        if (chamado.Status ==
            StatusChamado.Fechado)
        {
            TempData["Erro"] =
                "Este chamado já foi finalizado " +
                "e não pode ser assumido novamente.";

            return RedirectToAction(
                nameof(Details),
                new
                {
                    id
                });
        }

        if (chamado.TecnicoResponsavelId is not null &&
            chamado.TecnicoResponsavelId != usuario.Id)
        {
            TempData["Erro"] =
                "Este chamado já foi assumido " +
                "por outro técnico.";

            return RedirectToAction(
                nameof(Details),
                new
                {
                    id
                });
        }

        var statusAnterior = chamado.Status;
        var agora = DateTime.Now;

        chamado.TecnicoResponsavelId =
            usuario.Id;

        chamado.AssumidoEm ??=
            agora;

        chamado.Status =
            StatusChamado.EmAndamento;

        _slaService.AplicarMudancaStatus(
            chamado,
            statusAnterior,
            chamado.Status,
            agora);

        RegistrarHistorico(
            chamado.Id,
            usuario.Id,
            "Chamado assumido",
            "O chamado foi assumido e colocado " +
            "em andamento.");

        await _context.SaveChangesAsync();

        TempData["Sucesso"] =
            "Chamado assumido com sucesso.";

        return RedirectToAction(
            nameof(Details),
            new
            {
                id
            });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(
        Policy =
            "PodeAtenderChamados")]
    public async Task<IActionResult> AtualizarAtendimento(
        AtualizarChamadoViewModel model)
    {
        var usuario =
            await _userManager.GetUserAsync(User);

        if (usuario is null)
        {
            return Challenge();
        }

        var chamado =
            await _context.Chamados
                .SingleOrDefaultAsync(
                    item =>
                        item.Id == model.ChamadoId);

        if (chamado is null)
        {
            return NotFound();
        }

        if (chamado.Status ==
            StatusChamado.Fechado)
        {
            TempData["Erro"] =
                "Este chamado já foi finalizado " +
                "e não pode ser alterado.";

            return RedirectToAction(
                nameof(Details),
                new
                {
                    id =
                        model.ChamadoId
                });
        }

        var podeSupervisionar =
            User.IsInRole("Administrador");

        var pertenceAOutroTecnico =
            chamado.TecnicoResponsavelId is not null &&
            chamado.TecnicoResponsavelId != usuario.Id;

        if (pertenceAOutroTecnico &&
            !podeSupervisionar)
        {
            TempData["Erro"] =
                "Este chamado está atribuído " +
                "a outro técnico.";

            return RedirectToAction(
                nameof(Details),
                new
                {
                    id =
                        model.ChamadoId
                });
        }

        if (!ModelState.IsValid)
        {
            TempData["Erro"] =
                "Confira os dados do atendimento " +
                "antes de salvar.";

            return RedirectToAction(
                nameof(Details),
                new
                {
                    id =
                        model.ChamadoId
                });
        }

        if (!Enum.IsDefined(model.Status) ||
            !Enum.IsDefined(model.Prioridade))
        {
            TempData["Erro"] =
                "O status ou a prioridade informados são inválidos.";

            return RedirectToAction(
                nameof(Details),
                new
                {
                    id = model.ChamadoId
                });
        }

        if (model.Status ==
                StatusChamado.Resolvido &&

            chamado.Status !=
                StatusChamado.Resolvido)
        {
            TempData["Erro"] =
                "Para finalizar o atendimento, " +
                "selecione o status Fechado.";

            return RedirectToAction(
                nameof(Details),
                new
                {
                    id =
                        model.ChamadoId
                });
        }

        var estaFinalizando =
            model.Status ==
                StatusChamado.Resolvido ||

            model.Status ==
                StatusChamado.Fechado;

        if (estaFinalizando &&
            string.IsNullOrWhiteSpace(
                model.SolucaoAplicada))
        {
            TempData["Erro"] =
                "Informe a solução aplicada " +
                "antes de finalizar o chamado.";

            return RedirectToAction(
                nameof(Details),
                new
                {
                    id =
                        model.ChamadoId
                });
        }

        if (estaFinalizando &&
            (!model.TempoGastoMinutos.HasValue ||
             model.TempoGastoMinutos.Value <= 0))
        {
            TempData["Erro"] =
                "Informe o tempo gasto " +
                "antes de finalizar o chamado.";

            return RedirectToAction(
                nameof(Details),
                new
                {
                    id =
                        model.ChamadoId
                });
        }

        var statusAnterior =
            chamado.Status;

        var prioridadeAnterior =
            chamado.Prioridade;

        var tempoAnterior =
            chamado.TempoGastoMinutos;

        var solucaoAnterior =
            chamado.SolucaoAplicada;

        var foiAtribuidoAutomaticamente =
            chamado.TecnicoResponsavelId is null;

        chamado.TecnicoResponsavelId ??=
            usuario.Id;

        chamado.AssumidoEm ??=
            DateTime.Now;

        chamado.Status =
            model.Status;

        chamado.Prioridade =
            model.Prioridade;

        chamado.TempoGastoMinutos =
            model.TempoGastoMinutos;

        chamado.SolucaoAplicada =
            model.SolucaoAplicada?.Trim();

        var agora = DateTime.Now;

        _slaService.AplicarMudancaStatus(
            chamado,
            statusAnterior,
            chamado.Status,
            agora);

        _slaService.RecalcularPorPrioridade(
            chamado,
            prioridadeAnterior,
            agora);

        if (estaFinalizando)
        {
            chamado.ResolvidoEm ??=
                agora;
        }
        else
        {
            chamado.ResolvidoEm =
                null;
        }

        var alteracoes =
            new List<string>();

        if (foiAtribuidoAutomaticamente)
        {
            alteracoes.Add(
                "Responsável atribuído automaticamente.");
        }

        if (statusAnterior != chamado.Status)
        {
            alteracoes.Add(
                "Status alterado de " +
                $"{ObterNomeStatus(statusAnterior)} " +
                "para " +
                $"{ObterNomeStatus(chamado.Status)}.");

            if (SlaService.EhStatusPausado(chamado.Status) &&
                !SlaService.EhStatusPausado(statusAnterior))
            {
                alteracoes.Add(
                    "Contagem do SLA pausada enquanto o chamado aguarda retorno.");
            }
            else if (!SlaService.EhStatusPausado(chamado.Status) &&
                     SlaService.EhStatusPausado(statusAnterior))
            {
                alteracoes.Add(
                    "Contagem do SLA retomada e prazo ajustado em minutos úteis.");
            }
        }

        if (prioridadeAnterior != chamado.Prioridade)
        {
            alteracoes.Add(
                "Prioridade alterada de " +
                $"{ObterNomePrioridade(prioridadeAnterior)} " +
                "para " +
                $"{ObterNomePrioridade(chamado.Prioridade)}.");
        }

        if (tempoAnterior !=
            chamado.TempoGastoMinutos)
        {
            alteracoes.Add(
                "Tempo registrado: " +
                $"{chamado.TempoGastoMinutos ?? 0} " +
                "minuto(s).");
        }

        if (solucaoAnterior !=
                chamado.SolucaoAplicada &&

            !string.IsNullOrWhiteSpace(
                chamado.SolucaoAplicada))
        {
            alteracoes.Add(
                "Solução atualizada.");
        }

        if (alteracoes.Count == 0)
        {
            alteracoes.Add(
                "Os dados do atendimento foram salvos.");
        }

        var acaoHistorico =
            chamado.Status switch
            {
                StatusChamado.Fechado =>
                    "Chamado finalizado",

                StatusChamado.Resolvido =>
                    "Chamado resolvido",

                _ =>
                    "Atendimento atualizado"
            };

        RegistrarHistorico(
            chamado.Id,
            usuario.Id,
            acaoHistorico,
            string.Join(
                " ",
                alteracoes));

        if (statusAnterior != chamado.Status &&
            chamado.Status == StatusChamado.AguardandoFuncionario)
        {
            await _emailNotifications.EnfileirarAsync(
                chamado.Id,
                usuario.Id,
                "Aguardando seu retorno",
                "A equipe de TI precisa de uma informação ou ação " +
                "sua para continuar o atendimento. Acesse o chamado " +
                "para conferir os detalhes.");
        }
        else if (statusAnterior != chamado.Status &&
                 chamado.Status == StatusChamado.Fechado)
        {
            await _emailNotifications.EnfileirarAsync(
                chamado.Id,
                usuario.Id,
                "Chamado finalizado",
                "Seu chamado foi finalizado pela equipe de TI. " +
                "Acesse o portal para consultar a solução aplicada.");
        }

        await _context.SaveChangesAsync();

        TempData["Sucesso"] =
            chamado.Status ==
                StatusChamado.Fechado

                ? "Chamado finalizado com sucesso."

                : "Atendimento atualizado com sucesso.";

        return RedirectToAction(
            nameof(Details),
            new
            {
                id =
                    model.ChamadoId
            });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(
        Roles =
            "Administrador")]
    public async Task<IActionResult> Transferir(
        TransferirChamadoViewModel model)
    {
        var usuario =
            await _userManager.GetUserAsync(User);

        if (usuario is null)
        {
            return Challenge();
        }

        var chamado =
            await _context.Chamados
                .SingleOrDefaultAsync(
                    item =>
                        item.Id == model.ChamadoId);

        if (chamado is null)
        {
            return NotFound();
        }

        if (chamado.Status ==
            StatusChamado.Fechado)
        {
            TempData["Erro"] =
                "Chamados finalizados não podem " +
                "ser transferidos.";

            return RedirectToAction(
                nameof(Details),
                new
                {
                    id =
                        chamado.Id
                });
        }

        if (!ModelState.IsValid ||
            string.IsNullOrWhiteSpace(
                model.TecnicoId))
        {
            TempData["Erro"] =
                "Selecione o novo responsável.";

            return RedirectToAction(
                nameof(Details),
                new
                {
                    id =
                        chamado.Id
                });
        }

        if (chamado.TecnicoResponsavelId ==
            model.TecnicoId)
        {
            TempData["Erro"] =
                "Este usuário já é o responsável " +
                "pelo chamado.";

            return RedirectToAction(
                nameof(Details),
                new
                {
                    id =
                        chamado.Id
                });
        }

        var novoResponsavel =
            await _userManager.FindByIdAsync(
                model.TecnicoId);

        if (novoResponsavel is null)
        {
            TempData["Erro"] =
                "O responsável selecionado " +
                "não foi encontrado.";

            return RedirectToAction(
                nameof(Details),
                new
                {
                    id =
                        chamado.Id
                });
        }

        var perfilNovoResponsavel =
            await _context.FuncionariosPerfis
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    perfil =>
                        perfil.UsuarioId ==
                        novoResponsavel.Id);

        if (perfilNovoResponsavel is null ||
            !perfilNovoResponsavel.Ativo)
        {
            TempData["Erro"] =
                "O responsável selecionado " +
                "não possui uma conta ativa.";

            return RedirectToAction(
                nameof(Details),
                new
                {
                    id =
                        chamado.Id
                });
        }

        var possuiPermissaoDeAtendimento =
            await _userManager.IsInRoleAsync(
                novoResponsavel,
                "Tecnico") ||

            await _userManager.IsInRoleAsync(
                novoResponsavel,
                "Administrador");

        if (!possuiPermissaoDeAtendimento)
        {
            TempData["Erro"] =
                "O usuário selecionado não possui " +
                "permissão para atender chamados.";

            return RedirectToAction(
                nameof(Details),
                new
                {
                    id =
                        chamado.Id
                });
        }

        var responsavelAnterior =
            "Não atribuído";

        if (chamado.TecnicoResponsavelId is not null)
        {
            var perfilAnterior =
                await _context.FuncionariosPerfis
                    .AsNoTracking()
                    .SingleOrDefaultAsync(
                        perfil =>
                            perfil.UsuarioId ==
                            chamado.TecnicoResponsavelId);

            responsavelAnterior =
                perfilAnterior?.NomeCompleto ??
                "Responsável anterior";
        }

        chamado.TecnicoResponsavelId =
            novoResponsavel.Id;

        chamado.AssumidoEm ??=
            DateTime.Now;

        if (chamado.Status ==
            StatusChamado.Aberto)
        {
            chamado.Status =
                StatusChamado.EmAndamento;
        }

        RegistrarHistorico(
            chamado.Id,
            usuario.Id,
            "Responsável alterado",
            "Responsável alterado de " +
            $"\"{responsavelAnterior}\" para " +
            $"\"{perfilNovoResponsavel.NomeCompleto}\".");

        await _context.SaveChangesAsync();

        TempData["Sucesso"] =
            "Chamado transferido para " +
            $"{perfilNovoResponsavel.NomeCompleto}.";

        return RedirectToAction(
            nameof(Details),
            new
            {
                id =
                    chamado.Id
            });
    }

    private async Task CarregarTecnicosParaTransferencia(
        string? tecnicoSelecionado)
    {
        var tecnicos =
            await _userManager.GetUsersInRoleAsync(
                "Tecnico");

        var administradores =
            await _userManager.GetUsersInRoleAsync(
                "Administrador");

        var idsPermitidos =
            tecnicos
                .Concat(administradores)
                .Select(
                    usuario =>
                        usuario.Id)
                .Distinct()
                .ToList();

        var responsaveis =
            await _context.FuncionariosPerfis
                .AsNoTracking()
                .Where(
                    perfil =>
                        perfil.Ativo &&
                        idsPermitidos.Contains(
                            perfil.UsuarioId))
                .OrderBy(
                    perfil =>
                        perfil.NomeCompleto)
                .Select(
                    perfil => new
                    {
                        Id =
                            perfil.UsuarioId,

                        Nome =
                            perfil.NomeCompleto
                    })
                .ToListAsync();

        ViewBag.TecnicosTransferencia =
            new SelectList(
                responsaveis,
                "Id",
                "Nome",
                tecnicoSelecionado);
    }

    private void RegistrarHistorico(
        int chamadoId,
        string usuarioId,
        string acao,
        string descricao)
    {
        _context.HistoricosChamados.Add(
            new HistoricoChamado
            {
                ChamadoId =
                    chamadoId,

                UsuarioId =
                    usuarioId,

                Acao =
                    acao,

                Descricao =
                    descricao,

                CriadoEm =
                    DateTime.Now
            });
    }

    private async Task ValidarNovoChamadoAsync(
        NovoChamadoViewModel model)
    {
        if (!Enum.IsDefined(model.Prioridade))
        {
            ModelState.AddModelError(
                nameof(model.Prioridade),
                "Selecione uma prioridade válida.");
        }

        var categoriaValida =
            await _context.CategoriasChamados
                .AsNoTracking()
                .AnyAsync(categoria =>
                    categoria.Id == model.CategoriaId &&
                    categoria.Ativa);

        if (!categoriaValida)
        {
            ModelState.AddModelError(
                nameof(model.CategoriaId),
                "Selecione uma categoria ativa.");
        }

        var unidadeValida =
            await _context.Unidades
                .AsNoTracking()
                .AnyAsync(unidade =>
                    unidade.Id == model.UnidadeId &&
                    unidade.Ativa);

        if (!unidadeValida)
        {
            ModelState.AddModelError(
                nameof(model.UnidadeId),
                "Selecione uma unidade ativa.");
        }

        var setorValido =
            await _context.Setores
                .AsNoTracking()
                .AnyAsync(setor =>
                    setor.Id == model.SetorId &&
                    setor.Ativo);

        if (!setorValido)
        {
            ModelState.AddModelError(
                nameof(model.SetorId),
                "Selecione um setor ativo.");
        }
    }

    private static string ObterNomeStatus(
        StatusChamado status)
    {
        return status switch
        {
            StatusChamado.EmAndamento =>
                "Em andamento",

            StatusChamado.AguardandoFuncionario =>
                "Aguardando funcionário",

            StatusChamado.AguardandoFornecedor =>
                "Aguardando fornecedor",

            _ =>
                status.ToString()
        };
    }

    private static string ObterNomePrioridade(
        PrioridadeChamado prioridade)
    {
        return prioridade switch
        {
            PrioridadeChamado.Media =>
                "Média",

            PrioridadeChamado.Urgente =>
                "Urgente",

            _ =>
                "Leve"
        };
    }

    private async Task CarregarListas()
    {
        ViewBag.Categorias =
            new SelectList(
                await _context.CategoriasChamados
                    .AsNoTracking()
                    .Where(
                        categoria =>
                            categoria.Ativa)
                    .OrderBy(
                        categoria =>
                            categoria.Nome)
                    .ToListAsync(),
                "Id",
                "Nome");

        ViewBag.Unidades =
            new SelectList(
                await _context.Unidades
                    .AsNoTracking()
                    .Where(
                        unidade =>
                            unidade.Ativa)
                    .OrderBy(
                        unidade =>
                            unidade.Nome)
                    .ToListAsync(),
                "Id",
                "Nome");

        ViewBag.Setores =
            new SelectList(
                await _context.Setores
                    .AsNoTracking()
                    .Where(
                        setor =>
                            setor.Ativo)
                    .OrderBy(
                        setor =>
                            setor.Nome)
                    .ToListAsync(),
                "Id",
                "Nome");
    }
}
