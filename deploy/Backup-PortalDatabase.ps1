[CmdletBinding()]
param(
    [string]$BackupDirectory =
        "D:\Backups\PortalSanMarco",

    [ValidateRange(1, 3650)]
    [int]$RetentionDays = 30
)

$ErrorActionPreference = "Stop"

$diagnosticDirectory =
    "C:\ProgramData\PortalSanMarco"
$diagnosticLog =
    Join-Path $diagnosticDirectory "backup.log"

trap {
    New-Item `
        -ItemType Directory `
        -Path $diagnosticDirectory `
        -Force | Out-Null

    $details =
        "[$(Get-Date -Format O)] FALHA`r`n" +
        ($_ | Out-String) +
        $_.ScriptStackTrace +
        "`r`n"

    Add-Content `
        -LiteralPath $diagnosticLog `
        -Value $details `
        -Encoding utf8

    exit 1
}

$databaseName = "PortalSanMarco"
$serverName = ".\SQLEXPRESS"
$backupDirectoryFull =
    [System.IO.Path]::GetFullPath($BackupDirectory)

if (-not $backupDirectoryFull.StartsWith(
        "D:\Backups\PortalSanMarco",
        [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "O diretório de backup deve permanecer em D:\Backups\PortalSanMarco."
}

New-Item `
    -ItemType Directory `
    -Path $backupDirectoryFull `
    -Force | Out-Null

$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$backupFile = Join-Path `
    $backupDirectoryFull `
    "${databaseName}_full_${timestamp}.bak"
$backupFileSql = $backupFile.Replace("'", "''")

$connection =
    [System.Data.SqlClient.SqlConnection]::new(
        "Server=$serverName;Database=master;" +
        "Integrated Security=True;Encrypt=False;" +
        "TrustServerCertificate=True")

try {
    $connection.Open()

    $backupCommand = $connection.CreateCommand()
    $backupCommand.CommandTimeout = 600
    $backupCommand.CommandText = @"
BACKUP DATABASE [$databaseName]
TO DISK = N'$backupFileSql'
WITH COPY_ONLY, INIT, CHECKSUM;
"@
    $backupCommand.ExecuteNonQuery() | Out-Null

}
catch {
    if (Test-Path -LiteralPath $backupFile) {
        Remove-Item -LiteralPath $backupFile -Force
    }

    throw
}
finally {
    $connection.Dispose()
}

$backupCreated = Get-Item -LiteralPath $backupFile

if ($backupCreated.Length -le 0) {
    Remove-Item -LiteralPath $backupFile -Force
    throw "O arquivo de backup foi criado sem conteúdo."
}

$limit = (Get-Date).AddDays(-$RetentionDays)

Get-ChildItem `
    -LiteralPath $backupDirectoryFull `
    -Filter "${databaseName}_full_*.bak" `
    -File |
    Where-Object LastWriteTime -LT $limit |
    Remove-Item -Force

Write-Output "Backup criado com checksum: $backupFile"

Add-Content `
    -LiteralPath $diagnosticLog `
    -Value (
        "[$(Get-Date -Format O)] SUCESSO: " +
        "$backupFile") `
    -Encoding utf8
