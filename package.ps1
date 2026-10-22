param([string]$InputDirectory = (Join-Path $PSScriptRoot 'artifacts\portable'))
$ErrorActionPreference = 'Stop'
$output = Join-Path $PSScriptRoot 'artifacts'
$file = Join-Path $InputDirectory 'PowerUI.exe'
if (-not (Test-Path -LiteralPath $file -PathType Leaf)) { throw 'Missing PowerUI.exe. Run build.ps1 first.' }
New-Item -ItemType Directory -Path $output -Force | Out-Null
$archive = Join-Path $output 'PowerUI-0.1-win11-x64.zip'
Compress-Archive -LiteralPath $file -DestinationPath $archive -Force
Write-Output "Packaged $archive"
