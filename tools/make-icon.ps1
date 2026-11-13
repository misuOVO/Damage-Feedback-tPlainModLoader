Add-Type -AssemblyName System.Drawing

$size = 80
$bmp = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.Clear([System.Drawing.Color]::Transparent)

function New-RoundPath([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $r * 2
    $p.AddArc($x, $y, $d, $d, 180, 90)
    $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $p.CloseFigure()
    return $p
}

# ---- panel ----
$panel = New-RoundPath 2 2 76 76 14
$panelBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
    (New-Object System.Drawing.Point(0, 0)),
    (New-Object System.Drawing.Point(0, 80)),
    [System.Drawing.Color]::FromArgb(255, 52, 54, 78),
    [System.Drawing.Color]::FromArgb(255, 16, 16, 26))
$g.FillPath($panelBrush, $panel)

$panelPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 196, 42, 42), 4)
$g.DrawPath($panelPen, $panel)

# inner glow line
$inner = New-RoundPath 8 8 64 64 9
$innerPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(70, 255, 120, 120), 1.5)
$g.DrawPath($innerPen, $inner)

# ---- heart ----
$heart = New-Object System.Drawing.Drawing2D.GraphicsPath
$heart.StartFigure()
$heart.AddBezier(40, 54, 26, 42, 18, 33, 23, 25)
$heart.AddBezier(23, 25, 27, 17, 37, 19, 40, 28)
$heart.AddBezier(40, 28, 43, 19, 53, 17, 57, 25)
$heart.AddBezier(57, 25, 62, 33, 54, 42, 40, 54)
$heart.CloseFigure()

$heartBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
    (New-Object System.Drawing.Point(0, 18)),
    (New-Object System.Drawing.Point(0, 54)),
    [System.Drawing.Color]::FromArgb(255, 255, 96, 96),
    [System.Drawing.Color]::FromArgb(255, 176, 16, 32))
$g.FillPath($heartBrush, $heart)

$heartPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(200, 90, 0, 10), 1.2)
$g.DrawPath($heartPen, $heart)

# heart highlight
$hl = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(150, 255, 220, 220))
$g.FillEllipse($hl, 27, 24, 7, 5)

# ---- hp bar ----
$barBack = New-RoundPath 13 58 54 12 5
$barBackBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 10, 10, 16))
$g.FillPath($barBackBrush, $barBack)
$barBackPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 90, 90, 110), 1)
$g.DrawPath($barBackPen, $barBack)

$barFill = New-RoundPath 15 60 36 8 4
$barFillBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
    (New-Object System.Drawing.Point(15, 0)),
    (New-Object System.Drawing.Point(51, 0)),
    [System.Drawing.Color]::FromArgb(255, 126, 217, 87),
    [System.Drawing.Color]::FromArgb(255, 58, 160, 52))
$g.FillPath($barFillBrush, $barFill)

# sparkle (flicker hint)
$spark = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(220, 255, 255, 255))
$g.FillEllipse($spark, 60, 18, 5, 5)
$g.FillEllipse($spark, 66, 24, 3, 3)

$out = Join-Path (Split-Path -Parent $MyInvocation.MyCommand.Path) '..\assets\ico.png'
$bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose(); $bmp.Dispose()

"icon written: $out"
