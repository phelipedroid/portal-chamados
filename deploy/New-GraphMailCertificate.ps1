[CmdletBinding()]
param(
    [string]$Subject =
        "CN=PortalSanMarco-GraphMail",

    [string]$OutputPath =
        (Join-Path `
            (Split-Path -Parent $PSScriptRoot) `
            "artifacts\PortalSanMarco-GraphMail.cer")
)

$ErrorActionPreference = "Stop"

$identity =
    [Security.Principal.WindowsIdentity]::GetCurrent()
$principal =
    [Security.Principal.WindowsPrincipal]::new($identity)

if (-not $principal.IsInRole(
        [Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw "Abra o PowerShell como administrador e execute novamente."
}

$agora = Get-Date
$certificado =
    Get-ChildItem Cert:\LocalMachine\My |
        Where-Object {
            $_.Subject -eq $Subject -and
            $_.HasPrivateKey -and
            $_.NotAfter -gt $agora.AddMonths(3)
        } |
        Sort-Object NotAfter -Descending |
        Select-Object -First 1

if (-not $certificado) {
    Write-Host "Criando certificado do Microsoft Graph..."

    $certificado =
        New-SelfSignedCertificate `
            -Subject $Subject `
            -CertStoreLocation "Cert:\LocalMachine\My" `
            -KeyAlgorithm RSA `
            -KeyLength 2048 `
            -HashAlgorithm SHA256 `
            -KeyExportPolicy NonExportable `
            -KeyUsage DigitalSignature `
            -NotAfter $agora.AddYears(2)
}
else {
    Write-Host "Reutilizando certificado válido já instalado."
}

$rsa =
    [Security.Cryptography.X509Certificates.RSACertificateExtensions]::GetRSAPrivateKey(
        $certificado)

try {
    $nomeChave = $rsa.Key.UniqueName
}
finally {
    $rsa.Dispose()
}

if ([string]::IsNullOrWhiteSpace($nomeChave)) {
    throw "Não foi possível localizar a chave privada do certificado."
}

$caminhoChave =
    Join-Path `
        $env:ProgramData `
        "Microsoft\Crypto\Keys\$nomeChave"

if (-not (Test-Path -LiteralPath $caminhoChave)) {
    throw "Arquivo da chave privada não encontrado."
}

Write-Host "Liberando leitura da chave somente para o serviço..."
& icacls.exe $caminhoChave `
    /grant "*S-1-5-20:R" | Out-Null

if ($LASTEXITCODE -ne 0) {
    throw "Não foi possível liberar a chave para o NetworkService."
}

$diretorioSaida =
    Split-Path -Parent $OutputPath

New-Item `
    -ItemType Directory `
    -Path $diretorioSaida `
    -Force | Out-Null

Export-Certificate `
    -Cert $certificado `
    -FilePath $OutputPath `
    -Force | Out-Null

$caminhoMetadados =
    Join-Path `
        $diretorioSaida `
        "GraphMailCertificate.metadata.json"

[pscustomobject]@{
    Subject = $certificado.Subject
    Thumbprint = $certificado.Thumbprint
    NotAfter = $certificado.NotAfter.ToString("O")
    KeyPath = $caminhoChave
    KeyAclSddl = (Get-Acl -LiteralPath $caminhoChave).Sddl
    PublicCertificatePath = $OutputPath
} |
    ConvertTo-Json |
    Set-Content `
        -LiteralPath $caminhoMetadados `
        -Encoding UTF8

Write-Host "Certificado instalado e arquivo público exportado."
Write-Host "Thumbprint: $($certificado.Thumbprint)"
Write-Host "Validade: $($certificado.NotAfter.ToString('dd/MM/yyyy HH:mm'))"
Write-Host "Arquivo público: $OutputPath"
