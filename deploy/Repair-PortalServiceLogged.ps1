$ErrorActionPreference = "Stop"

$logDirectory = "C:\ProgramData\PortalSanMarco"
$logPath = Join-Path $logDirectory "service-repair.log"
$installer = Join-Path $PSScriptRoot "Install-PortalService.ps1"

New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null

try {
    "$(Get-Date -Format o) - Iniciando reparo." |
        Set-Content -LiteralPath $logPath -Encoding utf8

    & $installer *>&1 |
        Tee-Object -FilePath $logPath -Append

    "$(Get-Date -Format o) - Reparo concluído." |
        Add-Content -LiteralPath $logPath -Encoding utf8
}
catch {
    "$(Get-Date -Format o) - ERRO: $($_.Exception.Message)" |
        Add-Content -LiteralPath $logPath -Encoding utf8

    $_ | Out-String |
        Add-Content -LiteralPath $logPath -Encoding utf8

    exit 1
}
