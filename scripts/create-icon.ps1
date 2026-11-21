[CmdletBinding()]
param(
    # Cartella di destinazione alternativa (utile per anteprime); predefinita: Assets dell'applicazione.
    [string]$OutputDirectory
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

$root = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$assets = if ($OutputDirectory) { [System.IO.Path]::GetFullPath($OutputDirectory) } else { Join-Path $root "src\PizziniMaker.App\Assets" }
New-Item -ItemType Directory -Path $assets -Force | Out-Null

function New-Color([int]$r, [int]$g, [int]$b, [int]$a = 255) {
    [System.Drawing.Color]::FromArgb($a, $r, $g, $b)
}

function New-Brush([System.Drawing.Color]$color) {
    New-Object System.Drawing.SolidBrush($color)
}

# Punto sul cerchio: angolo in gradi, 0 = destra, senso orario (come GDI+).
function Get-Point([float]$cx, [float]$cy, [float]$radius, [float]$degrees) {
    $radians = $degrees * [Math]::PI / 180
    New-Object System.Drawing.PointF(($cx + $radius * [Math]::Cos($radians)), ($cy + $radius * [Math]::Sin($radians)))
}

function New-WobblyPath([float]$cx, [float]$cy, [float]$radius, [float]$wobble, [int]$seed) {
    $points = New-Object 'System.Collections.Generic.List[System.Drawing.PointF]'
    for ($angle = 0; $angle -lt 360; $angle += 4) {
        $radians = $angle * [Math]::PI / 180
        $factor = 1 + $wobble * [Math]::Sin(9 * $radians + $seed) + ($wobble * 0.5) * [Math]::Sin(17 * $radians + 2 * $seed)
        $points.Add((Get-Point $cx $cy ($radius * $factor) $angle))
    }
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddClosedCurve($points.ToArray(), 0.5)
    return $path
}

function Fill-Circle($graphics, $brush, [float]$cx, [float]$cy, [float]$radius) {
    $graphics.FillEllipse($brush, ($cx - $radius), ($cy - $radius), (2 * $radius), (2 * $radius))
}

function Draw-Leaf($graphics, [float]$x, [float]$y, [float]$length, [float]$angle) {
    $state = $graphics.Save()
    $graphics.TranslateTransform($x, $y)
    $graphics.RotateTransform($angle)
    $leaf = New-Brush (New-Color 58 138 62)
    $light = New-Brush (New-Color 96 178 88)
    $vein = New-Object System.Drawing.Pen((New-Color 34 92 40), [Math]::Max(1, $length / 14))
    $graphics.FillEllipse($leaf, (-$length / 2), (-$length / 4.4), $length, ($length / 2.2))
    $graphics.FillEllipse($light, (-$length / 2.6), (-$length / 6), ($length / 1.6), ($length / 4.5))
    $graphics.DrawLine($vein, (-$length / 2.2), 0, ($length / 2.4), 0)
    $graphics.Restore($state)
    $leaf.Dispose(); $light.Dispose(); $vein.Dispose()
}

function Draw-Salami($graphics, [float]$cx, [float]$cy, [float]$radius, [bool]$detailed) {
    $edge = New-Brush (New-Color 128 32 30)
    $meat = New-Brush (New-Color 200 52 44)
    $fat = New-Brush (New-Color 244 178 160)
    $shine = New-Brush (New-Color 236 110 90)
    Fill-Circle $graphics $edge $cx $cy $radius
    Fill-Circle $graphics $meat $cx $cy ($radius * 0.84)
    if ($detailed) {
        Fill-Circle $graphics $shine ($cx - $radius * 0.3) ($cy - $radius * 0.32) ($radius * 0.2)
        foreach ($offset in @(@(0.35, 0.1), @(-0.1, 0.42), @(0.05, -0.45), @(-0.42, 0.05), @(0.3, 0.45))) {
            Fill-Circle $graphics $fat ($cx + $radius * $offset[0]) ($cy + $radius * $offset[1]) ($radius * 0.08)
        }
    }
    $edge.Dispose(); $meat.Dispose(); $fat.Dispose(); $shine.Dispose()
}

# Disegna una pizza; se $missingStart è >= 0 manca una fetta (da $missingStart per $missingSweep gradi).
function Draw-Pizza($graphics, [float]$cx, [float]$cy, [float]$radius, [int]$seed, [bool]$detailed,
                    [float]$missingStart = -1, [float]$missingSweep = 60, [object[]]$toppings) {
    $state = $graphics.Save()
    if ($missingStart -ge 0) {
        $cut = New-Object System.Drawing.Drawing2D.GraphicsPath
        $big = $radius * 1.4
        $cut.AddPie(($cx - $big), ($cy - $big), (2 * $big), (2 * $big), $missingStart, $missingSweep)
        $everything = New-Object System.Drawing.RectangleF(0, 0, 4096, 4096)
        $region = New-Object System.Drawing.Region($everything)
        $region.Exclude($cut)
        $graphics.Clip = $region
    }

    # Ombra
    $shadow = New-Brush (New-Color 60 35 10 70)
    $shadowPath = New-WobblyPath ($cx + $radius * 0.05) ($cy + $radius * 0.08) $radius 0.02 $seed
    $graphics.FillPath($shadow, $shadowPath)

    # Cornicione: bordo irregolare dorato, più scuro verso l'esterno.
    $crustPath = New-WobblyPath $cx $cy $radius 0.012 $seed
    $crust = New-Object System.Drawing.Drawing2D.PathGradientBrush($crustPath)
    $crust.CenterPoint = New-Object System.Drawing.PointF($cx, $cy)
    $crust.CenterColor = New-Color 246 196 104
    $crust.SurroundColors = [System.Drawing.Color[]]@(New-Color 172 96 30)
    $crust.FocusScales = New-Object System.Drawing.PointF(0.8, 0.8)
    $graphics.FillPath($crust, $crustPath)
    $outline = New-Object System.Drawing.Pen((New-Color 112 58 18), [Math]::Max(1.2, $radius * 0.03))
    $graphics.DrawPath($outline, $crustPath)

    if ($detailed) {
        $toast = New-Brush (New-Color 150 78 22 120)
        $random = New-Object System.Random($seed)
        for ($i = 0; $i -lt 16; $i++) {
            $p = Get-Point $cx $cy ($radius * (0.88 + 0.06 * $random.NextDouble())) ($random.Next(0, 360))
            Fill-Circle $graphics $toast $p.X $p.Y ($radius * (0.02 + 0.025 * $random.NextDouble()))
        }
        $toast.Dispose()
    }

    # Salsa e formaggio
    $sauce = New-Brush (New-Color 206 74 38)
    $sauceRadius = $radius * 0.80
    $saucePath = New-WobblyPath $cx $cy $sauceRadius 0.03 ($seed + 3)
    $graphics.FillPath($sauce, $saucePath)
    $cheese = New-Brush (New-Color 255 214 92)
    $cheesePath = New-WobblyPath $cx $cy ($radius * 0.74) 0.045 ($seed + 7)
    $graphics.FillPath($cheese, $cheesePath)

    if ($detailed) {
        $baked = New-Brush (New-Color 232 160 56 150)
        $mozzarella = New-Brush (New-Color 255 251 236)
        $random = New-Object System.Random($seed + 11)
        for ($i = 0; $i -lt 10; $i++) {
            $p = Get-Point $cx $cy ($radius * 0.6 * $random.NextDouble()) ($random.Next(0, 360))
            Fill-Circle $graphics $baked $p.X $p.Y ($radius * (0.03 + 0.04 * $random.NextDouble()))
        }
        foreach ($spot in @(@(200, 0.42, 0.13), @(320, 0.5, 0.11), @(80, 0.3, 0.12), @(140, 0.58, 0.09))) {
            $p = Get-Point $cx $cy ($radius * $spot[1]) $spot[0]
            $graphics.FillEllipse($mozzarella, ($p.X - $radius * $spot[2]), ($p.Y - $radius * $spot[2] * 0.75),
                ($radius * $spot[2] * 2), ($radius * $spot[2] * 1.5))
        }
        $baked.Dispose(); $mozzarella.Dispose()
    }

    # Tagli delle fette
    $cutPen = New-Object System.Drawing.Pen((New-Color 196 132 48 170), [Math]::Max(1, $radius * 0.018))
    foreach ($angle in @(15, 75, 135, 195, 255, 315)) {
        $graphics.DrawLine($cutPen, $cx, $cy, (Get-Point $cx $cy ($radius * 0.78) $angle).X, (Get-Point $cx $cy ($radius * 0.78) $angle).Y)
    }

    foreach ($topping in $toppings) {
        $p = Get-Point $cx $cy ($radius * $topping[1]) $topping[0]
        if ($topping[2] -eq "leaf") {
            Draw-Leaf $graphics $p.X $p.Y ($radius * $topping[3]) $topping[4]
        } else {
            Draw-Salami $graphics $p.X $p.Y ($radius * $topping[3]) $detailed
        }
    }

    $graphics.Restore($state)

    # Bordi del taglio della fetta mancante: separano nettamente la pizza dallo sfondo.
    if ($missingStart -ge 0) {
        $edgePen = New-Object System.Drawing.Pen((New-Color 112 58 18), [Math]::Max(1.2, $radius * 0.03))
        $edgePen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
        $edgePen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
        foreach ($angle in @($missingStart, ($missingStart + $missingSweep))) {
            $p = Get-Point $cx $cy ($radius * 0.99) $angle
            $graphics.DrawLine($edgePen, $cx, $cy, $p.X, $p.Y)
        }
        $edgePen.Dispose()
    }

    $shadow.Dispose(); $crust.Dispose(); $outline.Dispose(); $sauce.Dispose(); $cheese.Dispose(); $cutPen.Dispose()
}

function New-PizzaBitmap([int]$size) {
    $canvas = 512
    $bitmap = New-Object System.Drawing.Bitmap($canvas, $canvas, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.Clear([System.Drawing.Color]::Transparent)

    if ($size -le 32) {
        # Versione semplificata per tray e icone piccole: una sola pizza, fetta mancante e pochi salami grandi.
        $toppings = @(
            @(200, 0.42, "salami", 0.2),
            @(110, 0.45, "salami", 0.2),
            @(290, 0.42, "salami", 0.2),
            @(160, 0.05, "salami", 0.17)
        )
        Draw-Pizza $graphics 248 262 236 3 $false 300 75 $toppings
        if ($size -eq 32) {
            Draw-Leaf $graphics 205 345 70 -30
        }
    } else {
        $backToppings = @(
            @(200, 0.5, "salami", 0.15),
            @(250, 0.45, "salami", 0.15),
            @(150, 0.32, "salami", 0.14),
            @(300, 0.25, "leaf", 0.26, 20),
            @(110, 0.55, "leaf", 0.22, -40)
        )
        Draw-Pizza $graphics 188 186 168 5 $true -1 60 $backToppings

        $frontToppings = @(
            @(180, 0.45, "salami", 0.16),
            @(240, 0.5, "salami", 0.16),
            @(120, 0.5, "salami", 0.16),
            @(200, 0.05, "salami", 0.15),
            @(285, 0.55, "salami", 0.14),
            @(150, 0.25, "leaf", 0.25, 35),
            @(255, 0.25, "leaf", 0.22, -20),
            @(95, 0.25, "leaf", 0.2, 70)
        )
        Draw-Pizza $graphics 320 322 182 9 $true 310 70 $frontToppings
    }

    $graphics.Dispose()
    if ($size -eq $canvas) {
        return $bitmap
    }

    $scaled = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $scaledGraphics = [System.Drawing.Graphics]::FromImage($scaled)
    $scaledGraphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $scaledGraphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $scaledGraphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
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
    if ($OutputDirectory) {
        $bitmap.Save((Join-Path $assets "PizziniMaker-$size.png"), [System.Drawing.Imaging.ImageFormat]::Png)
    }
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
