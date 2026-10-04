# Sales Base app icons: the 1Base wordmark (public/brand/logo-light.png) on a deep green square with the word SALES.
# Run: powershell -File frontend/scripts/field-assets.ps1  (after brand-assets.ps1). ASCII only on purpose (PowerShell 5.1 encoding).
Add-Type -AssemblyName System.Drawing
$ErrorActionPreference = 'Stop'

$root = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$pub = Join-Path $root 'frontend\public'
$mark = [System.Drawing.Image]::FromFile((Join-Path $pub 'brand\logo-light.png'))
$aspect = $mark.Width / $mark.Height

$bg = [System.Drawing.Color]::FromArgb(255, 13, 59, 51)       # #0d3b33
$label = [System.Drawing.Color]::FromArgb(255, 94, 234, 212)  # #5eead4

function RoundedPath([int]$size, [double]$radius) {
  $p = New-Object System.Drawing.Drawing2D.GraphicsPath
  if ($radius -le 0) { $p.AddRectangle((New-Object System.Drawing.Rectangle 0, 0, $size, $size)); return $p }
  $d = [float](2 * $radius); $s = [float]$size
  $p.AddArc(0, 0, $d, $d, 180, 90)
  $p.AddArc($s - $d, 0, $d, $d, 270, 90)
  $p.AddArc($s - $d, $s - $d, $d, $d, 0, 90)
  $p.AddArc(0, $s - $d, $d, $d, 90, 90)
  $p.CloseFigure()
  return $p
}

function Icon([int]$size, [double]$radiusShare, [double]$scale) {
  $img = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $g = [System.Drawing.Graphics]::FromImage($img)
  $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
  $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
  $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
  $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
  $g.Clear([System.Drawing.Color]::Transparent)
  $brush = New-Object System.Drawing.SolidBrush $bg
  $path = RoundedPath $size ($size * $radiusShare)
  $g.FillPath($brush, $path)

  # Wordmark: width = scale of the icon, a bit above the center.
  $aw = [int][Math]::Round($size * $scale); $ah = [int][Math]::Round($aw / $aspect)
  $top = [int][Math]::Round($size * 0.5 - $ah * 0.95)
  $g.DrawImage($mark, [int](($size - $aw) / 2), $top, $aw, $ah)

  # SALES under the wordmark, letter-spaced.
  $fontSize = [float]($size * $scale * 0.2)
  $font = New-Object System.Drawing.Font 'Segoe UI', $fontSize, ([System.Drawing.FontStyle]::Bold), ([System.Drawing.GraphicsUnit]::Pixel)
  $text = 'S A L E S'
  $fmt = New-Object System.Drawing.StringFormat
  $fmt.Alignment = [System.Drawing.StringAlignment]::Center
  $labelBrush = New-Object System.Drawing.SolidBrush $label
  $rect = New-Object System.Drawing.RectangleF 0, ([float]($top + $ah + $size * 0.06)), ([float]$size), ([float]($fontSize * 1.6))
  $g.DrawString($text, $font, $labelBrush, $rect, $fmt)

  $g.Dispose(); $brush.Dispose(); $path.Dispose(); $font.Dispose(); $labelBrush.Dispose(); $fmt.Dispose()
  return $img
}

$icons = @(
  @{ name = 'icons\field-192.png'; size = 192; radius = 0.22; scale = 0.74 },
  @{ name = 'icons\field-512.png'; size = 512; radius = 0.22; scale = 0.74 },
  @{ name = 'icons\field-maskable-512.png'; size = 512; radius = 0; scale = 0.58 },
  @{ name = 'icons\field-apple-touch-icon.png'; size = 180; radius = 0; scale = 0.70 }
)
foreach ($i in $icons) {
  $img = Icon $i.size $i.radius $i.scale
  $img.Save((Join-Path $pub $i.name), [System.Drawing.Imaging.ImageFormat]::Png)
  $img.Dispose()
  "$($i.name) $($i.size)px"
}
$mark.Dispose()
