$ErrorActionPreference = "Stop"

$installer =
    Join-Path $PSScriptRoot "Install-AutomaticShutdown.ps1"
$logDirectory =
    Join-Path (Split-Path -Parent $PSScriptRoot) "artifacts"
$logFile =
    Join-Path $logDirectory "automatic-shutdown-install.log"

New-Item `
    -ItemType Directory `
    -Path $logDirectory `
    -Force | Out-Null

try {
    & $installer `
        -ShutdownHour 19 `
        -ShutdownMinute 0 `
        -WarningMinutes 5 *>&1 |
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
