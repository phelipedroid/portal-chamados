[CmdletBinding()]
param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $PSScriptRoot
$projectFile = Join-Path $projectRoot "PortalSanMarco.csproj"
$packagesDirectory = Join-Path $projectRoot ".packages"
$publishDirectory = Join-Path $projectRoot "artifacts\publish"
$dotnetCliDirectory = Join-Path $projectRoot ".dotnet_cli"

$env:DOTNET_CLI_HOME = $dotnetCliDirectory
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"

$projectRootFull = [IO.Path]::GetFullPath($projectRoot)
$publishDirectoryFull = [IO.Path]::GetFullPath($publishDirectory)

if (-not $publishDirectoryFull.StartsWith(
        $projectRootFull + [IO.Path]::DirectorySeparatorChar,
        [StringComparison]::OrdinalIgnoreCase) -or
    (Split-Path -Leaf $publishDirectoryFull) -ne "publish") {
    throw "Diretório de publicação inválido: $publishDirectoryFull"
}

if (Test-Path -LiteralPath $publishDirectoryFull) {
    Write-Host "Limpando a publicação anterior..."
    Remove-Item `
        -LiteralPath $publishDirectoryFull `
        -Recurse `
        -Force
}

Write-Host "Restaurando dependências..."
dotnet restore $projectFile `
    --packages $packagesDirectory `
    --runtime win-x64

if ($LASTEXITCODE -ne 0) {
    throw "A restauração das dependências falhou."
}

Write-Host "Publicando o Portal San Marco..."
dotnet publish $projectFile `
    --configuration $Configuration `
    --runtime win-x64 `
    --self-contained false `
    --no-restore `
    --output $publishDirectory

if ($LASTEXITCODE -ne 0) {
    throw "A publicação falhou."
}

# Remove um banco SQLite legado que não pertence à instalação atual em SQL
# Server e pode permanecer no diretório quando uma publicação é sobrescrita.
$legacyDatabase = Join-Path $publishDirectory "app.db"

if (Test-Path -LiteralPath $legacyDatabase) {
    Remove-Item -LiteralPath $legacyDatabase -Force
}

Write-Host "Publicação concluída em: $publishDirectory"
