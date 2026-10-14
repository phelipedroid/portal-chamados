using System.ComponentModel.DataAnnotations;

namespace PortalSanMarco.Services;

public class GraphMailOptions
{
    [Required]
    public string TenantId { get; set; } = string.Empty;

    [Required]
    public string ClientId { get; set; } = string.Empty;

    [Required]
    public string CertificateThumbprint { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string SenderAddress { get; set; } = string.Empty;

    [Required]
    public string SenderName { get; set; } = string.Empty;
}
