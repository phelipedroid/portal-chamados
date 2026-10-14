[CmdletBinding()]
param(
    [string]$BackupDirectory =
        "D:\Backups\PortalSanMarco",

    [ValidateRange(0, 23)]
    [int]$Hour = 2,

    [ValidateRange(0, 59)]
    [int]$Minute = 0,

    [ValidateRange(1, 3650)]
    [int]$RetentionDays = 30
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

$taskName = "Portal San Marco - Backup Diario"
$programDirectory =
    "C:\ProgramData\PortalSanMarco"
$installedScript =
    Join-Path $programDirectory "Backup-PortalDatabase.ps1"
$sourceScript =
    Join-Path $PSScriptRoot "Backup-PortalDatabase.ps1"
$backupDirectoryFull =
    [System.IO.Path]::GetFullPath($BackupDirectory)
$sqlServiceAccount =
    'NT SERVICE\MSSQL$SQLEXPRESS'
$systemAccount =
    [Security.Principal.SecurityIdentifier]::new(
        "S-1-5-18").Translate(
            [Security.Principal.NTAccount]).Value
$systemAccountSqlLiteral =
    $systemAccount.Replace("'", "''")
$systemAccountSqlIdentifier =
    $systemAccount.Replace("]", "]]" )

if (-not (Test-Path -LiteralPath $sourceScript)) {
    throw "Script de backup não encontrado: $sourceScript"
}

if (-not $backupDirectoryFull.StartsWith(
        "D:\Backups\PortalSanMarco",
        [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "O diretório de backup deve permanecer em D:\Backups\PortalSanMarco."
}

New-Item `
    -ItemType Directory `
    -Path $programDirectory `
    -Force | Out-Null

Copy-Item `
    -LiteralPath $sourceScript `
    -Destination $installedScript `
    -Force

New-Item `
    -ItemType Directory `
    -Path $backupDirectoryFull `
    -Force | Out-Null

& icacls.exe $backupDirectoryFull `
    /inheritance:r | Out-Null

if ($LASTEXITCODE -ne 0) {
    throw "Não foi possível restringir o diretório de backup."
}

& icacls.exe $backupDirectoryFull `
    /grant:r "*S-1-5-18:(OI)(CI)F" `
    "*S-1-5-32-544:(OI)(CI)F" `
    "${sqlServiceAccount}:(OI)(CI)M" | Out-Null

if ($LASTEXITCODE -ne 0) {
    throw "Não foi possível configurar o acesso do SQL ao backup."
}

$connection =
    [System.Data.SqlClient.SqlConnection]::new(
        "Server=.\SQLEXPRESS;Database=master;" +
        "Integrated Security=True;Encrypt=False;" +
        "TrustServerCertificate=True")

try {
    $connection.Open()
    $command = $connection.CreateCommand()
    $command.CommandText = @"
IF NOT EXISTS (
    SELECT 1
    FROM sys.server_principals
    WHERE name = N'$systemAccountSqlLiteral'
)
BEGIN
    CREATE LOGIN [$systemAccountSqlIdentifier] FROM WINDOWS;
END;

USE [PortalSanMarco];

IF NOT EXISTS (
    SELECT 1
    FROM sys.database_principals
    WHERE name = N'$systemAccountSqlLiteral'
)
BEGIN
    CREATE USER [$systemAccountSqlIdentifier]
        FOR LOGIN [$systemAccountSqlIdentifier];
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.database_role_members memberships
    INNER JOIN sys.database_principals roles
        ON roles.principal_id = memberships.role_principal_id
    INNER JOIN sys.database_principals members
        ON members.principal_id = memberships.member_principal_id
    WHERE roles.name = N'db_backupoperator'
      AND members.name = N'$systemAccountSqlLiteral'
)
BEGIN
    ALTER ROLE [db_backupoperator]
        ADD MEMBER [$systemAccountSqlIdentifier];
END;
"@
    $command.ExecuteNonQuery() | Out-Null
}
finally {
    $connection.Dispose()
}

$powershellArguments =
    '-NoProfile -NonInteractive -ExecutionPolicy Bypass ' +
    "-File `"$installedScript`" " +
    "-BackupDirectory `"$backupDirectoryFull`" " +
    "-RetentionDays $RetentionDays"
$windowsPowerShell =
    "$env:SystemRoot\System32\WindowsPowerShell\v1.0\powershell.exe"

$action = New-ScheduledTaskAction `
    -Execute $windowsPowerShell `
    -Argument $powershellArguments `
    -WorkingDirectory $programDirectory

$scheduleTime =
    [DateTime]::Today.AddHours($Hour).AddMinutes($Minute)
$trigger = New-ScheduledTaskTrigger `
    -Daily `
    -At $scheduleTime
$settings = New-ScheduledTaskSettingsSet `
    -StartWhenAvailable `
    -WakeToRun `
    -ExecutionTimeLimit (New-TimeSpan -Hours 1) `
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
        "Backup completo e verificado do banco PortalSanMarco. " +
        "Retenção: $RetentionDays dias.") `
    -Force | Out-Null

$existingBackups =
    @(Get-ChildItem `
        -LiteralPath $backupDirectoryFull `
        -Filter "PortalSanMarco_full_*.bak" `
        -File).Count

Start-ScheduledTask -TaskName $taskName

$deadline = (Get-Date).AddSeconds(60)

do {
    Start-Sleep -Seconds 2
    $task = Get-ScheduledTask -TaskName $taskName
}
while ($task.State -eq "Running" -and
       (Get-Date) -lt $deadline)

$taskInfo = Get-ScheduledTaskInfo -TaskName $taskName
$currentBackups =
    @(Get-ChildItem `
        -LiteralPath $backupDirectoryFull `
        -Filter "PortalSanMarco_full_*.bak" `
        -File).Count

if ($task.State -eq "Running" -or
    $taskInfo.LastTaskResult -ne 0 -or
    $currentBackups -le $existingBackups) {
    throw (
        "A tarefa foi criada, mas o primeiro backup falhou. " +
        "Resultado: $($taskInfo.LastTaskResult).")
}

$latestBackup =
    Get-ChildItem `
        -LiteralPath $backupDirectoryFull `
        -Filter "PortalSanMarco_full_*.bak" `
        -File |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1
$latestBackupSql =
    $latestBackup.FullName.Replace("'", "''")
$verificationConnection =
    [System.Data.SqlClient.SqlConnection]::new(
        "Server=.\SQLEXPRESS;Database=master;" +
        "Integrated Security=True;Encrypt=False;" +
        "TrustServerCertificate=True")

try {
    $verificationConnection.Open()
    $verificationCommand =
        $verificationConnection.CreateCommand()
    $verificationCommand.CommandTimeout = 600
    $verificationCommand.CommandText = @"
RESTORE VERIFYONLY
FROM DISK = N'$latestBackupSql'
WITH CHECKSUM;
"@
    $verificationCommand.ExecuteNonQuery() | Out-Null
}
finally {
    $verificationConnection.Dispose()
}

Write-Output "Backup diário configurado para $($scheduleTime.ToString('HH:mm'))."
Write-Output "Destino: $backupDirectoryFull"
Write-Output "Retenção: $RetentionDays dias"
