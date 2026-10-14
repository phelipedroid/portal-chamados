[CmdletBinding()]
param(
    [string]$InstallDirectory =
        "C:\Program Files\PortalSanMarco",

    [string]$TaskName =
        "Portal San Marco - Monitoramento",

    [ValidateRange(1, 60)]
    [int]$IntervalMinutes = 5
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

$executable = Join-Path $InstallDirectory "PortalSanMarco.exe"
$stateDirectory = "C:\ProgramData\PortalSanMarco"

if (-not (Test-Path -LiteralPath $executable)) {
    throw "Portal instalado não encontrado em $executable."
}

New-Item -ItemType Directory `
    -Path $stateDirectory `
    -Force | Out-Null

$action = New-ScheduledTaskAction `
    -Execute $executable `
    -Argument "--monitor-once" `
    -WorkingDirectory $InstallDirectory

$trigger = New-ScheduledTaskTrigger `
    -Once `
    -At (Get-Date).AddMinutes(1) `
    -RepetitionInterval (
        New-TimeSpan -Minutes $IntervalMinutes)

$settings = New-ScheduledTaskSettingsSet `
    -ExecutionTimeLimit (New-TimeSpan -Minutes 2) `
    -StartWhenAvailable `
    -MultipleInstances IgnoreNew

$taskPrincipal = New-ScheduledTaskPrincipal `
    -UserId "SYSTEM" `
    -LogonType ServiceAccount `
    -RunLevel Highest

Register-ScheduledTask `
    -TaskName $TaskName `
    -Action $action `
    -Trigger $trigger `
    -Settings $settings `
    -Principal $taskPrincipal `
    -Description (
        "Verifica portal, SQL Server, backup e espaço em disco; " +
        "envia alertas pelo Microsoft 365.") `
    -Force | Out-Null

Start-ScheduledTask -TaskName $TaskName

Write-Host "Monitoramento instalado e executado."
Write-Host "Tarefa: $TaskName"
Write-Host "Intervalo: $IntervalMinutes minuto(s)"
Write-Host "Estado: $stateDirectory\monitor-state.json"
