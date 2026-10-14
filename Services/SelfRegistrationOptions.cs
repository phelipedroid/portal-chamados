using System.ComponentModel.DataAnnotations;

namespace PortalSanMarco.Services;

public sealed class SelfRegistrationOptions
{
    [Required]
    [MinLength(1)]
    public string[] AllowedDomains { get; set; } = [];

    public bool DominioPermitido(string email)
    {
        var separador = email.LastIndexOf('@');

        if (separador <= 0 || separador == email.Length - 1)
        {
            return false;
        }

        var dominio = email[(separador + 1)..].Trim();

        return AllowedDomains.Any(item =>
            string.Equals(
                item.Trim().TrimStart('@'),
                dominio,
                StringComparison.OrdinalIgnoreCase));
    }
}
