$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) {
    throw 'The Windows .NET Framework C# compiler is unavailable.'
}
$output = Join-Path $PSScriptRoot 'artifacts'
New-Item -ItemType Directory -Path $output -Force | Out-Null
& $compiler /nologo /codepage:65001 /optimize+ /platform:x64 /target:exe "/out:$output\PowerUI.Probe.exe" `
    (Join-Path $PSScriptRoot 'src\HidDevice.cs') (Join-Path $PSScriptRoot 'src\RapooBattery.cs') (Join-Path $PSScriptRoot 'src\Probe.cs')
if ($LASTEXITCODE -ne 0) { throw 'Probe build failed.' }
Write-Output "Built $output\PowerUI.Probe.exe"
$framework = Split-Path -Parent $compiler
$references = @('PresentationFramework', 'PresentationCore', 'WindowsBase') | ForEach-Object {
    '/reference:' + (Join-Path $framework "WPF\$_.dll")
}
& $compiler /nologo /codepage:65001 /optimize+ /platform:x64 /target:winexe "/out:$output\PowerUI.exe" `
    "/win32manifest:$PSScriptRoot\src\app.manifest" "/resource:$PSScriptRoot\src\Shell.xaml,Shell.xaml" `
    /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Xaml.dll $references `
    (Join-Path $PSScriptRoot 'src\HidDevice.cs') (Join-Path $PSScriptRoot 'src\RapooBattery.cs') `
    (Join-Path $PSScriptRoot 'src\AppState.cs') (Join-Path $PSScriptRoot 'src\DesktopNative.cs') (Join-Path $PSScriptRoot 'src\App.cs')
if ($LASTEXITCODE -ne 0) { throw 'Application build failed.' }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'src\App.config') -Destination (Join-Path $output 'PowerUI.exe.config')
Write-Output "Built $output\PowerUI.exe"
