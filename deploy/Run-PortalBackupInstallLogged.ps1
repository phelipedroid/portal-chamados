$ErrorActionPreference = "Stop"

$installer =
    Join-Path $PSScriptRoot "Install-PortalBackup.ps1"
$logDirectory =
    Join-Path (Split-Path -Parent $PSScriptRoot) "artifacts"
$logFile =
    Join-Path $logDirectory "backup-install.log"

New-Item `
    -ItemType Directory `
    -Path $logDirectory `
    -Force | Out-Null

try {
    & $installer `
        -BackupDirectory "D:\Backups\PortalSanMarco" `
        -Hour 17 `
        -Minute 0 `
        -RetentionDays 30 *>&1 |
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
