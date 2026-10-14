$ErrorActionPreference = "Stop"

$validator =
    Join-Path $PSScriptRoot "Test-PortalBackup.ps1"
$logDirectory =
    Join-Path (Split-Path -Parent $PSScriptRoot) "artifacts"
$logFile =
    Join-Path $logDirectory "backup-validation.log"

New-Item `
    -ItemType Directory `
    -Path $logDirectory `
    -Force | Out-Null

try {
    $validationOutput = @(& $validator *>&1)
    $shutdownTask =
        Get-ScheduledTask `
            -TaskName "Portal San Marco - Desligamento Diario"
    $shutdownInfo =
        Get-ScheduledTaskInfo `
            -TaskName "Portal San Marco - Desligamento Diario"
    $shutdownAction =
        $shutdownTask.Actions | Select-Object -First 1
    $shutdownTrigger =
        $shutdownTask.Triggers | Select-Object -First 1

    $validationOutput += "`r`nDesligamento automático:"
    $validationOutput += [pscustomobject]@{
        TaskName = $shutdownTask.TaskName
        TaskState = $shutdownTask.State
        LastTaskResult = $shutdownInfo.LastTaskResult
        NextRunTime = $shutdownInfo.NextRunTime
        Executable = $shutdownAction.Execute
        Arguments = $shutdownAction.Arguments
        ScheduleStart = $shutdownTrigger.StartBoundary
        StartWhenAvailable =
            $shutdownTask.Settings.StartWhenAvailable
        WakeToRun = $shutdownTask.Settings.WakeToRun
    } | Format-List | Out-String

    $validationOutput |
        Out-File `
            -LiteralPath $logFile `
            -Encoding utf8
}
catch {
    $_ | Out-String |
        Out-File `
            -LiteralPath $logFile `
            -Encoding utf8 `
            -Append

    exit 1
}

exit 0
