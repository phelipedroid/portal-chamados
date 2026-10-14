using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using PortalSanMarco.Data;
using PortalSanMarco.Models;

namespace PortalSanMarco.Services;

public class AuditoriaService : IAuditoriaService
{
    private static readonly Regex UnidadeRegex = new(
        @"\bunidade\s+(?<id>\d+)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex SetorRegex = new(
        @"\bsetor\s+(?<id>\d+)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private readonly ApplicationDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuditoriaService(
        ApplicationDbContext context,
        IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task RegistrarAsync(
        string acao,
        string entidade,
        string? entidadeId,
        string descricao,
        CancellationToken cancellationToken = default)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        var usuario = httpContext?.User;
        descricao = await TraduzirDescricaoAsync(
            descricao,
            cancellationToken);

        _context.AuditoriasAdministrativas.Add(
            new AuditoriaAdministrativa
            {
                UsuarioId = usuario?.FindFirstValue(
                    ClaimTypes.NameIdentifier) ?? "sistema",
                UsuarioEmail = usuario?.Identity?.Name ?? "sistema",
                Acao = acao,
                Entidade = entidade,
                EntidadeId = entidadeId,
                Descricao = descricao,
                EnderecoIp = httpContext?.Connection.RemoteIpAddress?.ToString(),
                CriadoEm = DateTime.Now
            });

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task TraduzirReferenciasOrganizacionaisAsync(
        IEnumerable<AuditoriaAdministrativa> registros,
        CancellationToken cancellationToken = default)
    {
        var lista = registros.ToList();

        if (lista.Count == 0)
        {
            return;
        }

        var descricoes = lista.Select(item => item.Descricao).ToList();
        var unidadeIds = ObterIds(descricoes, UnidadeRegex);
        var setorIds = ObterIds(descricoes, SetorRegex);

        var unidades = unidadeIds.Count == 0
            ? new Dictionary<int, string>()
            : await _context.Unidades
                .AsNoTracking()
                .Where(item => unidadeIds.Contains(item.Id))
                .ToDictionaryAsync(
                    item => item.Id,
                    item => item.Nome,
                    cancellationToken);

        var setores = setorIds.Count == 0
            ? new Dictionary<int, string>()
            : await _context.Setores
                .AsNoTracking()
                .Where(item => setorIds.Contains(item.Id))
                .ToDictionaryAsync(
                    item => item.Id,
                    item => item.Nome,
                    cancellationToken);

        foreach (var registro in lista)
        {
            registro.Descricao = TraduzirDescricao(
                registro.Descricao,
                unidades,
                setores);
        }
    }

    private async Task<string> TraduzirDescricaoAsync(
        string descricao,
        CancellationToken cancellationToken)
    {
        var unidadeIds = ObterIds([descricao], UnidadeRegex);
        var setorIds = ObterIds([descricao], SetorRegex);

        var unidades = unidadeIds.Count == 0
            ? new Dictionary<int, string>()
            : await _context.Unidades
                .AsNoTracking()
                .Where(item => unidadeIds.Contains(item.Id))
                .ToDictionaryAsync(
                    item => item.Id,
                    item => item.Nome,
                    cancellationToken);

        var setores = setorIds.Count == 0
            ? new Dictionary<int, string>()
            : await _context.Setores
                .AsNoTracking()
                .Where(item => setorIds.Contains(item.Id))
                .ToDictionaryAsync(
                    item => item.Id,
                    item => item.Nome,
                    cancellationToken);

        return TraduzirDescricao(descricao, unidades, setores);
    }

    private static HashSet<int> ObterIds(
        IEnumerable<string> descricoes,
        Regex regex) =>
        descricoes
            .SelectMany(descricao => regex.Matches(descricao))
            .Select(match => match.Groups["id"].Value)
            .Select(valor => int.TryParse(valor, out var id) ? id : 0)
            .Where(id => id > 0)
            .ToHashSet();

    private static string TraduzirDescricao(
        string descricao,
        IReadOnlyDictionary<int, string> unidades,
        IReadOnlyDictionary<int, string> setores)
    {
        descricao = UnidadeRegex.Replace(
            descricao,
            match => SubstituirReferencia(match, "unidade", unidades));

        return SetorRegex.Replace(
            descricao,
            match => SubstituirReferencia(match, "setor", setores));
    }

    private static string SubstituirReferencia(
        Match match,
        string tipo,
        IReadOnlyDictionary<int, string> nomes)
    {
        if (!int.TryParse(match.Groups["id"].Value, out var id) ||
            !nomes.TryGetValue(id, out var nome) ||
            string.IsNullOrWhiteSpace(nome))
        {
            return match.Value;
        }

        return $"{tipo} {nome.Trim()}";
    }
}
