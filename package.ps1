$ErrorActionPreference = 'Stop'
$output = Join-Path $PSScriptRoot 'artifacts'
$readme = Join-Path $output 'README.txt'
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'src\README-portable.txt') -Destination $readme
foreach ($name in @('LICENSE', 'THIRD_PARTY_NOTICES.md')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination (Join-Path $output $name)
}
$files = @('PowerUI.exe', 'PowerUI.exe.config', 'PowerUI.Probe.exe', 'README.txt', 'LICENSE', 'THIRD_PARTY_NOTICES.md') | ForEach-Object {
    $file = Join-Path $output $_
    if (-not (Test-Path -LiteralPath $file)) { throw "Missing artifact: $_. Run build.ps1 first." }
    $file
}
$archive = Join-Path $output 'PowerUI-0.1-win11-x64.zip'
Compress-Archive -LiteralPath $files -DestinationPath $archive -Force
Write-Output "Packaged $archive"
