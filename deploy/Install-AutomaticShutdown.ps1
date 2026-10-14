[CmdletBinding()]
param(
    [ValidateRange(0, 23)]
    [int]$ShutdownHour = 19,

    [ValidateRange(0, 59)]
    [int]$ShutdownMinute = 0,

    [ValidateRange(1, 60)]
    [int]$WarningMinutes = 5
)

$ErrorActionPreference = "Stop"

$identity =
    [Security.Principal.WindowsIdentity]::GetCurrent()
$principal =
    [Security.Principal.WindowsPrincipal]::new($identity)

if (-not $principal.IsInRole(
        [Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw "Execute este instalador como administrador."
}

$taskName = "Portal San Marco - Desligamento Diario"
$shutdownTime =
    [DateTime]::Today.AddHours($ShutdownHour).AddMinutes($ShutdownMinute)
$triggerTime = $shutdownTime.AddMinutes(-$WarningMinutes)
$warningSeconds = $WarningMinutes * 60
$shutdownExecutable =
    Join-Path $env:SystemRoot "System32\shutdown.exe"
$message =
    "Este computador sera desligado automaticamente as " +
    "$($shutdownTime.ToString('HH:mm')). Salve seu trabalho. " +
    "Para cancelar, execute: shutdown /a"

$action = New-ScheduledTaskAction `
    -Execute $shutdownExecutable `
    -Argument (
        "/s /t $warningSeconds /d p:0:0 /c `"$message`"")
$trigger = New-ScheduledTaskTrigger `
    -Daily `
    -At $triggerTime
$settings = New-ScheduledTaskSettingsSet `
    -WakeToRun `
    -ExecutionTimeLimit (New-TimeSpan -Minutes 10) `
    -MultipleInstances IgnoreNew
$taskPrincipal = New-ScheduledTaskPrincipal `
    -UserId "SYSTEM" `
    -LogonType ServiceAccount `
    -RunLevel Highest

Register-ScheduledTask `
    -TaskName $taskName `
    -Action $action `
    -Trigger $trigger `
    -Settings $settings `
    -Principal $taskPrincipal `
    -Description (
        "Avisa com $WarningMinutes minutos de antecedência e desliga " +
        "o computador diariamente às $($shutdownTime.ToString('HH:mm')).") `
    -Force | Out-Null

Write-Output (
    "Desligamento diário configurado para " +
    "$($shutdownTime.ToString('HH:mm')).")
Write-Output (
    "O aviso começa às $($triggerTime.ToString('HH:mm')).")
Write-Output "Para cancelar um desligamento pendente: shutdown /a"
