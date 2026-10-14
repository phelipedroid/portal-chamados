$ErrorActionPreference = "Stop"

$appcmd =
    Join-Path $env:windir "System32\inetsrv\appcmd.exe"
$projectRoot = Split-Path -Parent $PSScriptRoot
$outputPath =
    Join-Path $projectRoot "artifacts\iis-sites.txt"

& $appcmd list site "/text:*" |
    Set-Content -LiteralPath $outputPath
