$ErrorActionPreference = "Continue"

$portalServiceName = "PortalSanMarco"
$sqlServiceName = 'MSSQL$SQLEXPRESS'
$portalHost = "portalsanmarco.local"
$healthUrl = "http://localhost/health"

function Write-Check {
    param(
        [string]$Name,
        [bool]$Success,
        [string]$Details
    )

    $status = if ($Success) { "OK" } else { "FALHA" }
    $color = if ($Success) { "Green" } else { "Red" }

    Write-Host "[$status] $Name - $Details" -ForegroundColor $color
}

Write-Host "Diagnostico do Portal San Marco" -ForegroundColor Cyan
Write-Host "Executado em: $(Get-Date -Format 'dd/MM/yyyy HH:mm:ss')"
Write-Host ""

$sqlService = Get-Service -Name $sqlServiceName -ErrorAction SilentlyContinue
$sqlDetails = if ($sqlService) {
    $sqlService.Status.ToString()
}
else {
    "servico nao encontrado"
}
Write-Check `
    -Name "SQL Server" `
    -Success ($sqlService.Status -eq "Running") `
    -Details $sqlDetails

$portalService =
    Get-Service -Name $portalServiceName -ErrorAction SilentlyContinue
$portalDetails = if ($portalService) {
    $portalService.Status.ToString()
}
else {
    "servico nao encontrado"
}
Write-Check `
    -Name "Servico do portal" `
    -Success ($portalService.Status -eq "Running") `
    -Details $portalDetails

$listener = Get-NetTCPConnection `
    -LocalPort 80 `
    -State Listen `
    -ErrorAction SilentlyContinue |
    Select-Object -First 1

Write-Check `
    -Name "Porta 80" `
    -Success ($null -ne $listener) `
    -Details $(
        if ($listener) {
            "escutando pelo processo $($listener.OwningProcess)"
        }
        else {
            "nenhum processo esta escutando"
        })

$healthOk = $false

try {
    $health = Invoke-RestMethod -Uri $healthUrl -TimeoutSec 10
    $healthOk = $health.status -eq "ok"
    Write-Check `
        -Name "Saude local" `
        -Success $healthOk `
        -Details "resposta: $($health.status)"
}
catch {
    Write-Check `
        -Name "Saude local" `
        -Success $false `
        -Details $_.Exception.Message
}

$dns = Resolve-DnsName $portalHost -Type A -ErrorAction SilentlyContinue |
    Select-Object -First 1

Write-Check `
    -Name "DNS interno" `
    -Success ($null -ne $dns.IPAddress) `
    -Details $(
        if ($dns.IPAddress) {
            "$portalHost -> $($dns.IPAddress)"
        }
        else {
            "$portalHost nao foi resolvido"
        })

Write-Host ""

if ($sqlService.Status -ne "Running") {
    Write-Host "Proximo passo: iniciar primeiro o SQL Server." -ForegroundColor Yellow
}
elseif ($portalService.Status -ne "Running") {
    Write-Host "Proximo passo: iniciar ou reparar o servico PortalSanMarco." -ForegroundColor Yellow
}
elseif (-not $listener) {
    Write-Host "Proximo passo: verificar se o servico encerrou e consultar os eventos do Windows." -ForegroundColor Yellow
}
elseif (-not $healthOk) {
    Write-Host "Proximo passo: consultar os eventos da aplicacao e a conexao com o banco." -ForegroundColor Yellow
}
elseif (-not $dns.IPAddress) {
    Write-Host "Proximo passo: conferir o registro DNS portalsanmarco.local." -ForegroundColor Yellow
}
else {
    Write-Host "O portal esta funcionando localmente e o DNS esta correto." -ForegroundColor Green
}
