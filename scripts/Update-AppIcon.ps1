$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

function New-RoundedRectangle([single]$X, [single]$Y, [single]$Width, [single]$Height, [single]$Radius) {
    $path = [Drawing.Drawing2D.GraphicsPath]::new()
    $diameter = 2 * $Radius
    $path.AddArc($X, $Y, $diameter, $diameter, 180, 90)
    $path.AddArc($X + $Width - $diameter, $Y, $diameter, $diameter, 270, 90)
    $path.AddArc($X + $Width - $diameter, $Y + $Height - $diameter, $diameter, $diameter, 0, 90)
    $path.AddArc($X, $Y + $Height - $diameter, $diameter, $diameter, 90, 90)
    $path.CloseFigure()
    return $path
}

$root = Split-Path -Parent $PSScriptRoot
$sizes = @(16, 20, 24, 32, 40, 48, 64, 128, 256)
$images = [Collections.Generic.List[byte[]]]::new()
foreach ($size in $sizes) {
    $canvas = [Drawing.Bitmap]::new($size * 4, $size * 4)
    $graphics = [Drawing.Graphics]::FromImage($canvas)
    $white = [Drawing.SolidBrush]::new([Drawing.Color]::White)
    $black = [Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#101412'))
    $green = [Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#32E875'))
    $body = New-RoundedRectangle 44 16 168 224 80
    $fill = New-RoundedRectangle 56 28 144 200 68
    $wheel = New-RoundedRectangle 116 48 24 42 12
    $battery = New-RoundedRectangle 76 140 96 48 8
    $batteryFill = New-RoundedRectangle 82 146 84 36 4
    $terminal = New-RoundedRectangle 168 154 12 20 4
    $bitmap = [Drawing.Bitmap]::new($size, $size)
    $outputGraphics = [Drawing.Graphics]::FromImage($bitmap)
    $stream = [IO.MemoryStream]::new()
    try {
        $graphics.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $scale = $size * 4 / 256.0
        $graphics.ScaleTransform($scale, $scale)
        $graphics.FillPath($white, $body)
        $graphics.FillPath($black, $fill)
        $graphics.FillRectangle($white, 123, 28, 10, 22)
        $graphics.FillPath($white, $wheel)
        $graphics.FillRectangle($white, 123, 88, 10, 24)
        $graphics.FillPath($white, $terminal)
        $graphics.FillPath($white, $battery)
        $graphics.FillPath($green, $batteryFill)
        $graphics.FillRectangle($green, 164, 159, 10, 10)
        $graphics.FillRectangle($black, 104, 152, 8, 24)
        $graphics.FillRectangle($black, 134, 152, 8, 24)
        $outputGraphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $outputGraphics.PixelOffsetMode = [Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $outputGraphics.DrawImage($canvas, [Drawing.Rectangle]::new(0, 0, $size, $size))
        $bitmap.Save($stream, [Drawing.Imaging.ImageFormat]::Png)
        $images.Add($stream.ToArray())
        if ($size -eq 256) {
            $bitmap.Save((Join-Path $root 'docs\images\app-icon.png'), [Drawing.Imaging.ImageFormat]::Png)
        }
    } finally {
        $stream.Dispose()
        $outputGraphics.Dispose()
        $bitmap.Dispose()
        $fill.Dispose()
        $body.Dispose()
        $wheel.Dispose()
        $battery.Dispose()
        $batteryFill.Dispose()
        $terminal.Dispose()
        $green.Dispose()
        $black.Dispose()
        $white.Dispose()
        $graphics.Dispose()
        $canvas.Dispose()
    }
}

$iconPath = Join-Path $root 'src\PowerUI.ico'
$writer = [IO.BinaryWriter]::new([IO.File]::Create($iconPath))
try {
    $writer.Write([uint16]0)
    $writer.Write([uint16]1)
    $writer.Write([uint16]$sizes.Count)
    $offset = 6 + 16 * $sizes.Count
    for ($index = 0; $index -lt $sizes.Count; $index++) {
        $dimension = if ($sizes[$index] -eq 256) { 0 } else { $sizes[$index] }
        $writer.Write([byte]$dimension)
        $writer.Write([byte]$dimension)
        $writer.Write([byte]0)
        $writer.Write([byte]0)
        $writer.Write([uint16]1)
        $writer.Write([uint16]32)
        $writer.Write([uint32]$images[$index].Length)
        $writer.Write([uint32]$offset)
        $offset += $images[$index].Length
    }
    foreach ($image in $images) { $writer.Write([byte[]]$image) }
} finally { $writer.Dispose() }
Write-Output "Updated $iconPath"
