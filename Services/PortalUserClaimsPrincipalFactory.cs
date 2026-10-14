using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PortalSanMarco.Data;

namespace PortalSanMarco.Services;

public sealed class PortalUserClaimsPrincipalFactory
    : UserClaimsPrincipalFactory<IdentityUser, IdentityRole>
{
    public const string PrimeiroNomeClaim = "portal:primeiro_nome";

    private readonly ApplicationDbContext _context;
    private readonly ILogger<PortalUserClaimsPrincipalFactory> _logger;

    public PortalUserClaimsPrincipalFactory(
        UserManager<IdentityUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IOptions<IdentityOptions> options,
        ApplicationDbContext context,
        ILogger<PortalUserClaimsPrincipalFactory> logger)
        : base(userManager, roleManager, options)
    {
        _context = context;
        _logger = logger;
    }

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(
        IdentityUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);

        try
        {
            var nomeCompleto = await _context.FuncionariosPerfis
                .AsNoTracking()
                .Where(perfil => perfil.UsuarioId == user.Id)
                .Select(perfil => perfil.NomeCompleto)
                .SingleOrDefaultAsync();

            var primeiroNome = ObterPrimeiroNome(nomeCompleto);

            if (!string.IsNullOrWhiteSpace(primeiroNome))
            {
                identity.AddClaim(
                    new Claim(PrimeiroNomeClaim, primeiroNome));
            }
        }
        catch (Exception exception)
        {
            // A saudação é apenas visual e nunca deve impedir o login.
            _logger.LogWarning(
                exception,
                "Não foi possível carregar o primeiro nome do usuário {UserId}.",
                user.Id);
        }

        return identity;
    }

    private static string? ObterPrimeiroNome(string? nomeCompleto)
    {
        if (string.IsNullOrWhiteSpace(nomeCompleto))
        {
            return null;
        }

        return nomeCompleto
            .Trim()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault();
    }
}
