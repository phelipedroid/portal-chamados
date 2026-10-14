[CmdletBinding()]
param(
    [string]$InstallDirectory =
        "C:\Program Files\PortalSanMarco",

    [int]$Port = 80
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

$serviceName = "PortalSanMarco"
$serviceDisplayName = "Portal San Marco"
$sqlServiceName = 'MSSQL$SQLEXPRESS'
$serviceSid = "S-1-5-20"
$serviceAccount =
    [Security.Principal.SecurityIdentifier]::new(
        $serviceSid).Translate(
            [Security.Principal.NTAccount]).Value
$serviceStartAccount = "NT AUTHORITY\NetworkService"
$firewallRuleName = "Portal San Marco - Rede interna"
$projectRoot = Split-Path -Parent $PSScriptRoot
$sourceDirectory = Join-Path $projectRoot "artifacts\publish"
$executable = Join-Path $InstallDirectory "PortalSanMarco.exe"
$keysDirectory = Join-Path $InstallDirectory "DataProtectionKeys"
$legacyDatabase = Join-Path $InstallDirectory "app.db"

if (-not (Test-Path -LiteralPath (
        Join-Path $sourceDirectory "PortalSanMarco.exe"))) {
    throw "Publicação não encontrada. Execute primeiro Publish-Portal.ps1."
}

$existingService =
    Get-Service -Name $serviceName -ErrorAction SilentlyContinue

if ($existingService -and
    $existingService.Status -ne "Stopped") {
    Write-Host "Parando a versão atual do serviço..."
    Stop-Service -Name $serviceName -Force
    (Get-Service -Name $serviceName).WaitForStatus(
        "Stopped",
        [TimeSpan]::FromSeconds(30))
}

Write-Host "Copiando a publicação..."
New-Item -ItemType Directory `
    -Path $InstallDirectory `
    -Force | Out-Null

Copy-Item `
    -Path (Join-Path $sourceDirectory "*") `
    -Destination $InstallDirectory `
    -Recurse `
    -Force

if (Test-Path -LiteralPath $legacyDatabase) {
    Remove-Item -LiteralPath $legacyDatabase -Force
}

New-Item -ItemType Directory `
    -Path $keysDirectory `
    -Force | Out-Null

& icacls.exe $InstallDirectory `
    /grant "*${serviceSid}:(OI)(CI)RX" `
    /C | Out-Null

if ($LASTEXITCODE -ne 0) {
    throw "Não foi possível conceder leitura da aplicação ao serviço."
}

& icacls.exe $keysDirectory `
    /inheritance:r `
    /C | Out-Null

if ($LASTEXITCODE -ne 0) {
    throw "Não foi possível restringir a pasta de chaves do portal."
}

& icacls.exe $keysDirectory `
    /grant:r "*S-1-5-18:(OI)(CI)F" `
    "*S-1-5-32-544:(OI)(CI)F" `
    "*${serviceSid}:(OI)(CI)M" `
    /C | Out-Null

if ($LASTEXITCODE -ne 0) {
    throw "Não foi possível liberar a pasta de chaves do portal."
}

Write-Host "Concedendo acesso do serviço ao banco PortalSanMarco..."
$connection =
    [System.Data.SqlClient.SqlConnection]::new(
        "Server=.\SQLEXPRESS;Database=master;" +
        "Integrated Security=True;Encrypt=False;" +
        "TrustServerCertificate=True")

try {
    $connection.Open()
    $command = $connection.CreateCommand()
    $serviceAccountSqlLiteral =
        $serviceAccount.Replace("'", "''")
    $serviceAccountSqlIdentifier =
        $serviceAccount.Replace("]", "]]" )
    $command.CommandText = @"
IF NOT EXISTS (
    SELECT 1
    FROM sys.server_principals
    WHERE name = N'$serviceAccountSqlLiteral'
)
BEGIN
    CREATE LOGIN [$serviceAccountSqlIdentifier] FROM WINDOWS;
END;

USE [PortalSanMarco];

IF NOT EXISTS (
    SELECT 1
    FROM sys.database_principals
    WHERE name = N'$serviceAccountSqlLiteral'
)
BEGIN
    CREATE USER [$serviceAccountSqlIdentifier]
        FOR LOGIN [$serviceAccountSqlIdentifier];
END;

IF IS_ROLEMEMBER(N'db_owner', N'$serviceAccountSqlLiteral') = 1
BEGIN
    ALTER ROLE [db_owner]
        DROP MEMBER [$serviceAccountSqlIdentifier];
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.database_role_members AS memberships
    INNER JOIN sys.database_principals AS roles
        ON roles.principal_id = memberships.role_principal_id
    INNER JOIN sys.database_principals AS members
        ON members.principal_id = memberships.member_principal_id
    WHERE roles.name = N'db_datareader'
      AND members.name = N'$serviceAccountSqlLiteral'
)
BEGIN
    ALTER ROLE [db_datareader]
        ADD MEMBER [$serviceAccountSqlIdentifier];
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.database_role_members AS memberships
    INNER JOIN sys.database_principals AS roles
        ON roles.principal_id = memberships.role_principal_id
    INNER JOIN sys.database_principals AS members
        ON members.principal_id = memberships.member_principal_id
    WHERE roles.name = N'db_datawriter'
      AND members.name = N'$serviceAccountSqlLiteral'
)
BEGIN
    ALTER ROLE [db_datawriter]
        ADD MEMBER [$serviceAccountSqlIdentifier];
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.database_role_members AS memberships
    INNER JOIN sys.database_principals AS roles
        ON roles.principal_id = memberships.role_principal_id
    INNER JOIN sys.database_principals AS members
        ON members.principal_id = memberships.member_principal_id
    WHERE roles.name = N'db_ddladmin'
      AND members.name = N'$serviceAccountSqlLiteral'
)
BEGIN
    ALTER ROLE [db_ddladmin]
        ADD MEMBER [$serviceAccountSqlIdentifier];
END;
"@
    $command.ExecuteNonQuery() | Out-Null
}
finally {
    $connection.Dispose()
}

if (-not $existingService) {
    Write-Host "Registrando o serviço do Windows..."
    $quotedExecutable = '"' + $executable + '"'

    & sc.exe create $serviceName `
        binPath= $quotedExecutable `
        start= auto `
        obj= $serviceStartAccount `
        DisplayName= $serviceDisplayName | Out-Null

    if ($LASTEXITCODE -ne 0) {
        throw "Não foi possível registrar o serviço do Windows."
    }
}

Write-Host "Configurando início após o SQL Server..."
& sc.exe config $serviceName `
    start= delayed-auto `
    depend= $sqlServiceName | Out-Null

if ($LASTEXITCODE -ne 0) {
    throw "Não foi possível configurar a dependência do SQL Server."
}

& sc.exe failureflag $serviceName 1 | Out-Null

& sc.exe description $serviceName `
    "Portal interno de chamados da San Marco." | Out-Null

& sc.exe failure $serviceName `
    reset= 86400 `
    actions= "restart/5000/restart/15000/restart/60000" | Out-Null

$serviceRegistryPath =
    "HKLM:\SYSTEM\CurrentControlSet\Services\$serviceName"

New-ItemProperty `
    -Path $serviceRegistryPath `
    -Name "Environment" `
    -PropertyType MultiString `
    -Value @(
        "ASPNETCORE_ENVIRONMENT=Production",
        "ASPNETCORE_URLS=http://0.0.0.0:$Port"
    ) `
    -Force | Out-Null

$existingFirewallRule =
    Get-NetFirewallRule `
        -DisplayName $firewallRuleName `
        -ErrorAction SilentlyContinue

if ($existingFirewallRule) {
    $existingFirewallRule | Remove-NetFirewallRule
}

Write-Host "Liberando a porta $Port somente na rede de domínio..."
New-NetFirewallRule `
    -DisplayName $firewallRuleName `
    -Direction Inbound `
    -Action Allow `
    -Protocol TCP `
    -LocalPort $Port `
    -Profile Domain `
    -RemoteAddress LocalSubnet | Out-Null

Write-Host "Iniciando o portal..."
Start-Service -Name $serviceName
(Get-Service -Name $serviceName).WaitForStatus(
    "Running",
    [TimeSpan]::FromSeconds(30))

$healthUrl = "http://localhost:$Port/health"
$deadline = (Get-Date).AddSeconds(30)
$health = $null

while ((Get-Date) -lt $deadline -and -not $health) {
    try {
        $health = Invoke-RestMethod `
            -Uri $healthUrl `
            -TimeoutSec 3
    }
    catch {
        Start-Sleep -Seconds 2
    }
}

if ($health.status -ne "ok") {
    throw "O serviço iniciou, mas a verificação de saúde falhou."
}

Write-Host ""
Write-Host "Portal instalado e funcionando."
Write-Host "Teste local: http://localhost:$Port"
Write-Host "Teste na rede: http://<IP-da-maquina>:$Port"
