$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $PSScriptRoot
$logDirectory = Join-Path $projectRoot "artifacts"
$logFile = Join-Path $logDirectory "production-upgrade.log"
$serviceInstaller = Join-Path $PSScriptRoot "Install-PortalService.ps1"
$monitorInstaller = Join-Path $PSScriptRoot "Install-PortalMonitoring.ps1"

New-Item -ItemType Directory `
    -Path $logDirectory `
    -Force | Out-Null

try {
    "[$(Get-Date -Format O)] Iniciando atualização de produção." |
        Out-File -LiteralPath $logFile -Encoding utf8

    & $serviceInstaller *>&1 |
        Out-File -LiteralPath $logFile -Encoding utf8 -Append

    & $monitorInstaller *>&1 |
        Out-File -LiteralPath $logFile -Encoding utf8 -Append

    "[$(Get-Date -Format O)] Atualização concluída." |
        Out-File -LiteralPath $logFile -Encoding utf8 -Append
}
catch {
    $_ | Out-String |
        Out-File -LiteralPath $logFile -Encoding utf8 -Append

    exit 1
}

exit 0
