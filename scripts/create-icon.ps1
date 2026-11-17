[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

$root = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$assets = Join-Path $root "src\PizziniMaker.App\Assets"
New-Item -ItemType Directory -Path $assets -Force | Out-Null

function New-PizzaBitmap([int]$size) {
    $bitmap = New-Object System.Drawing.Bitmap(512, 512, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias

    $shadow = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(45, 70, 45, 25))
    $crustEdge = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(173, 103, 37))
    $crust = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(240, 177, 70))
    $cheese = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 220, 92))
    $pepperoniEdge = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(166, 54, 42))
    $pepperoni = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(213, 72, 55))
    $highlight = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(245, 120, 84))

    $graphics.FillEllipse($shadow, 62, 70, 290, 290)
    $graphics.FillEllipse($shadow, 194, 200, 292, 292)
    $graphics.FillEllipse($crustEdge, 52, 42, 282, 282)
    $graphics.FillEllipse($crust, 60, 50, 266, 266)
    $graphics.FillEllipse($cheese, 78, 68, 230, 230)
    $graphics.FillEllipse($crustEdge, 182, 172, 300, 300)
    $graphics.FillEllipse($crust, 191, 181, 282, 282)
    $graphics.FillEllipse($cheese, 211, 201, 242, 242)

    $backPepperoni = @(@(112,105), @(211,105), @(104,205), @(207,210), @(157,157))
    foreach ($point in $backPepperoni) {
        $graphics.FillEllipse($pepperoniEdge, $point[0], $point[1], 48, 48)
        $graphics.FillEllipse($pepperoni, $point[0]+4, $point[1]+4, 40, 40)
        $graphics.FillEllipse($highlight, $point[0]+12, $point[1]+10, 8, 8)
    }
    $frontPepperoni = @(@(250,233), @(361,230), @(246,338), @(363,341), @(307,291), @(309,385))
    foreach ($point in $frontPepperoni) {
        $graphics.FillEllipse($pepperoniEdge, $point[0], $point[1], 51, 51)
        $graphics.FillEllipse($pepperoni, $point[0]+4, $point[1]+4, 43, 43)
        $graphics.FillEllipse($highlight, $point[0]+13, $point[1]+11, 8, 8)
    }

    $graphics.Dispose()
    foreach ($brush in @($shadow, $crustEdge, $crust, $cheese, $pepperoniEdge, $pepperoni, $highlight)) {
        $brush.Dispose()
    }

    if ($size -eq 512) {
        return $bitmap
    }

    $scaled = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $scaledGraphics = [System.Drawing.Graphics]::FromImage($scaled)
    $scaledGraphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $scaledGraphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $scaledGraphics.DrawImage($bitmap, 0, 0, $size, $size)
    $scaledGraphics.Dispose()
    $bitmap.Dispose()
    return $scaled
}

$source = New-PizzaBitmap 512
$pngPath = Join-Path $assets "PizziniMaker.png"
$source.Save($pngPath, [System.Drawing.Imaging.ImageFormat]::Png)
$source.Dispose()

$images = @()
foreach ($size in @(16, 32, 48, 256)) {
    $bitmap = New-PizzaBitmap $size
    $stream = New-Object System.IO.MemoryStream
    $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
    $images += ,@{ Size = $size; Bytes = $stream.ToArray() }
    $stream.Dispose()
    $bitmap.Dispose()
}

$icoPath = Join-Path $assets "PizziniMaker.ico"
$file = [System.IO.File]::Create($icoPath)
$writer = New-Object System.IO.BinaryWriter($file)
$writer.Write([uint16]0)
$writer.Write([uint16]1)
$writer.Write([uint16]$images.Count)
$offset = 6 + 16 * $images.Count
foreach ($image in $images) {
    $writer.Write([byte]$(if ($image.Size -eq 256) { 0 } else { $image.Size }))
    $writer.Write([byte]$(if ($image.Size -eq 256) { 0 } else { $image.Size }))
    $writer.Write([byte]0)
    $writer.Write([byte]0)
    $writer.Write([uint16]1)
    $writer.Write([uint16]32)
    $writer.Write([uint32]$image.Bytes.Length)
    $writer.Write([uint32]$offset)
    $offset += $image.Bytes.Length
}
foreach ($image in $images) {
    $writer.Write($image.Bytes)
}
$writer.Flush()
$writer.Dispose()
$file.Dispose()

Write-Output "Creati $pngPath e $icoPath"
