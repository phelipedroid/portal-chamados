[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"

$identity =
    [Security.Principal.WindowsIdentity]::GetCurrent()
$principal =
    [Security.Principal.WindowsPrincipal]::new($identity)

if (-not $principal.IsInRole(
        [Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw "Abra o PowerShell como administrador e execute novamente."
}

if ([string]::IsNullOrWhiteSpace(
        $env:PORTAL_TEMP_PASSWORD)) {
    throw "A senha temporária não foi fornecida."
}

$projectRoot = Split-Path -Parent $PSScriptRoot
$maintenanceExecutable =
    Join-Path $projectRoot `
        "artifacts\maintenance\PortalMaintenance.exe"
$serviceName = "PortalSanMarco"

if (-not (Test-Path -LiteralPath $maintenanceExecutable)) {
    throw "Utilitário de manutenção não encontrado."
}

Write-Host "Parando temporariamente o portal..."
Stop-Service -Name $serviceName -Force
(Get-Service -Name $serviceName).WaitForStatus(
    "Stopped",
    [TimeSpan]::FromSeconds(30))

try {
    & $maintenanceExecutable

    if ($LASTEXITCODE -ne 0) {
        throw "A rotina de limpeza falhou."
    }
}
finally {
    Remove-Item Env:\PORTAL_TEMP_PASSWORD `
        -ErrorAction SilentlyContinue

    Write-Host "Iniciando o portal..."
    Start-Service -Name $serviceName
    (Get-Service -Name $serviceName).WaitForStatus(
        "Running",
        [TimeSpan]::FromSeconds(30))
}

$health = Invoke-RestMethod `
    -Uri "http://localhost/health" `
    -TimeoutSec 10

if ($health.status -ne "ok") {
    throw "O portal reiniciou, mas a verificação de saúde falhou."
}

Write-Host "Limpeza concluída e portal saudável."
