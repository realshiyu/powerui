param(
    [string]$OutputDirectory = (Join-Path $PSScriptRoot 'artifacts\portable'),
    [switch]$IncludeProbe
)
$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) {
    throw 'The Windows .NET Framework C# compiler is unavailable.'
}
$output = $OutputDirectory
New-Item -ItemType Directory -Path $output -Force | Out-Null
if ($IncludeProbe) {
    & $compiler /nologo /codepage:65001 /optimize+ /platform:x64 /target:exe "/out:$output\PowerUI.Probe.exe" `
        (Join-Path $PSScriptRoot 'src\HidDevice.cs') (Join-Path $PSScriptRoot 'src\RapooBattery.cs') (Join-Path $PSScriptRoot 'src\LogitechBattery.cs') (Join-Path $PSScriptRoot 'src\VgnBattery.cs') (Join-Path $PSScriptRoot 'src\Probe.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Probe build failed.' }
    Write-Output "Built $output\PowerUI.Probe.exe"
}
$framework = Split-Path -Parent $compiler
$references = @('PresentationFramework', 'PresentationCore', 'WindowsBase') | ForEach-Object {
    '/reference:' + (Join-Path $framework "WPF\$_.dll")
}
& $compiler /nologo /codepage:65001 /optimize+ /platform:x64 /target:winexe "/out:$output\PowerUI.exe" `
    "/win32icon:$PSScriptRoot\src\PowerUI.ico" `
    "/win32manifest:$PSScriptRoot\src\app.manifest" "/resource:$PSScriptRoot\src\Shell.xaml,Shell.xaml" `
    "/resource:$PSScriptRoot\src\README-portable.txt,README.txt" `
    "/resource:$PSScriptRoot\LICENSE,LICENSE" "/resource:$PSScriptRoot\THIRD_PARTY_NOTICES.md,THIRD_PARTY_NOTICES.md" `
    /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Xaml.dll $references `
    (Join-Path $PSScriptRoot 'src\HidDevice.cs') (Join-Path $PSScriptRoot 'src\RapooBattery.cs') (Join-Path $PSScriptRoot 'src\LogitechBattery.cs') (Join-Path $PSScriptRoot 'src\VgnBattery.cs') `
    (Join-Path $PSScriptRoot 'src\AppState.cs') (Join-Path $PSScriptRoot 'src\DesktopNative.cs') (Join-Path $PSScriptRoot 'src\TrayIcon.cs') (Join-Path $PSScriptRoot 'src\App.cs')
if ($LASTEXITCODE -ne 0) { throw 'Application build failed.' }
Write-Output "Built $output\PowerUI.exe"
