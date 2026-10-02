<#
.SYNOPSIS
    Regenerates the Palette ribbon icons in Palette/Assets (a search bar with a magnifier).

.DESCRIPTION
    Revit shows "Dark - Palette*.png" (light glyph) on its dark theme and "Light - Palette*.png" (dark glyph) on the
    light theme. 32 px is the large button image, 16 px the small one. Drawn at 8x and scaled down for clean edges.
#>
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

$out = Join-Path (Split-Path -Parent $PSScriptRoot) "Palette\Assets"
New-Item -ItemType Directory -Force -Path $out | Out-Null

function New-Icon([int] $size, [System.Drawing.Color] $color, [string] $path) {
    $scale = 8
    $big = $size * $scale
    $bmp = New-Object System.Drawing.Bitmap $big, $big
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = "AntiAlias"
    $g.Clear([System.Drawing.Color]::Transparent)
    $u = $big / 32.0                      # design on a 32 unit grid
    $pen = New-Object System.Drawing.Pen $color, (2.2 * $u)
    $pen.StartCap = "Round"; $pen.EndCap = "Round"; $pen.LineJoin = "Round"

    # search bar (rounded rectangle)
    $r = 6 * $u
    $x = 2 * $u; $y = 7 * $u; $w = 28 * $u; $h = 18 * $u
    $gp = New-Object System.Drawing.Drawing2D.GraphicsPath
    $gp.AddArc($x, $y, $r, $r, 180, 90)
    $gp.AddArc($x + $w - $r, $y, $r, $r, 270, 90)
    $gp.AddArc($x + $w - $r, $y + $h - $r, $r, $r, 0, 90)
    $gp.AddArc($x, $y + $h - $r, $r, $r, 90, 90)
    $gp.CloseFigure()
    $g.DrawPath($pen, $gp)

    # magnifier
    $g.DrawEllipse($pen, (7.2 * $u), (12.2 * $u), (6.4 * $u), (6.4 * $u))
    $g.DrawLine($pen, (12.9 * $u), (18.1 * $u), (15.2 * $u), (20.4 * $u))

    # text line
    $g.DrawLine($pen, (19 * $u), (16 * $u), (26 * $u), (16 * $u))

    $final = New-Object System.Drawing.Bitmap $size, $size
    $fg = [System.Drawing.Graphics]::FromImage($final)
    $fg.InterpolationMode = "HighQualityBicubic"
    $fg.PixelOffsetMode = "HighQuality"
    $fg.DrawImage($bmp, 0, 0, $size, $size)
    $final.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $fg.Dispose(); $final.Dispose(); $g.Dispose(); $bmp.Dispose(); $pen.Dispose()
}

$light = [System.Drawing.Color]::FromArgb(255, 236, 236, 240)   # glyph for Revit's dark theme
$dark = [System.Drawing.Color]::FromArgb(255, 52, 52, 58)       # glyph for Revit's light theme
New-Icon 32 $light (Join-Path $out "Dark - Palette.png")
New-Icon 16 $light (Join-Path $out "Dark - Palette16.png")
New-Icon 32 $dark (Join-Path $out "Light - Palette.png")
New-Icon 16 $dark (Join-Path $out "Light - Palette16.png")
Write-Host "Icons written to $out"
