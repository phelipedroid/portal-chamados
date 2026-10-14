[CmdletBinding()]
param(
    [string]$BackupDirectory =
        "D:\Backups\PortalSanMarco"
)

$ErrorActionPreference = "Stop"

$identity =
    [Security.Principal.WindowsIdentity]::GetCurrent()
$principal =
    [Security.Principal.WindowsPrincipal]::new($identity)

if (-not $principal.IsInRole(
        [Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw "Execute esta validação como administrador."
}

$taskName = "Portal San Marco - Backup Diario"
$task = Get-ScheduledTask -TaskName $taskName
$taskInfo = Get-ScheduledTaskInfo -TaskName $taskName
$latestBackup =
    Get-ChildItem `
        -LiteralPath $BackupDirectory `
        -Filter "PortalSanMarco_full_*.bak" `
        -File |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1

if ($null -eq $latestBackup) {
    throw "Nenhum backup foi encontrado em $BackupDirectory."
}

$backupSql = $latestBackup.FullName.Replace("'", "''")
$connection =
    [System.Data.SqlClient.SqlConnection]::new(
        "Server=.\SQLEXPRESS;Database=master;" +
        "Integrated Security=True;Encrypt=False;" +
        "TrustServerCertificate=True")

try {
    $connection.Open()
    $command = $connection.CreateCommand()
    $command.CommandTimeout = 600
    $command.CommandText = @"
RESTORE VERIFYONLY
FROM DISK = N'$backupSql'
WITH CHECKSUM;
"@
    $command.ExecuteNonQuery() | Out-Null

    $command.CommandText = @"
SELECT roles.name
FROM [PortalSanMarco].sys.database_role_members memberships
INNER JOIN [PortalSanMarco].sys.database_principals roles
    ON roles.principal_id = memberships.role_principal_id
INNER JOIN [PortalSanMarco].sys.database_principals members
    ON members.principal_id = memberships.member_principal_id
WHERE members.sid = 0x010100000000000512000000;
"@
    $roles = @()
    $reader = $command.ExecuteReader()

    while ($reader.Read()) {
        $roles += $reader.GetString(0)
    }

    $reader.Close()
}
finally {
    $connection.Dispose()
}

$action = $task.Actions | Select-Object -First 1
$trigger = $task.Triggers | Select-Object -First 1

[pscustomobject]@{
    TaskName = $taskName
    TaskState = $task.State
    LastTaskResult = $taskInfo.LastTaskResult
    LastRunTime = $taskInfo.LastRunTime
    NextRunTime = $taskInfo.NextRunTime
    Executable = $action.Execute
    Arguments = $action.Arguments
    ScheduleStart = $trigger.StartBoundary
    LatestBackup = $latestBackup.FullName
    BackupSizeBytes = $latestBackup.Length
    BackupCreated = $latestBackup.LastWriteTime
    RestoreVerifyOnly = "OK"
    SystemDatabaseRoles = ($roles -join ", ")
} | Format-List

Write-Output "`r`nPermissões do diretório:"
& icacls.exe $BackupDirectory
