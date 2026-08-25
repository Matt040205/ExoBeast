[CmdletBinding()]
param(
    [string]$OutputDirectory = (Join-Path $PSScriptRoot 'dist')
)

$ErrorActionPreference = 'Stop'
$version = '1.0.0'
$staging = Join-Path ([System.IO.Path]::GetTempPath()) ("exo-bridge-" + [guid]::NewGuid().ToString('N'))
$packageRoot = Join-Path $staging 'ExoBridge'

try {
    New-Item -ItemType Directory -Path $packageRoot -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot '__init__.py') -Destination (Join-Path $packageRoot '__init__.py')
    New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
    $zipPath = Join-Path $OutputDirectory "ExoBridge-$version.zip"
    if (Test-Path -LiteralPath $zipPath) { Remove-Item -LiteralPath $zipPath -Force }
    Compress-Archive -LiteralPath $packageRoot -DestinationPath $zipPath -CompressionLevel Optimal
    Write-Output $zipPath
}
finally {
    if (Test-Path -LiteralPath $staging) { Remove-Item -LiteralPath $staging -Recurse -Force }
}
