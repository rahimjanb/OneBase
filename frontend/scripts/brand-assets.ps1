# Графика бренда OneBase из логотипа «1Base»: прозрачный wordmark для шапок, иконки приложения и favicon.
# Запуск: powershell -File frontend/scripts/brand-assets.ps1 — исходник docs/brand/1base-logo.png (1254×1254, без прозрачности).
# Файл сохранён в UTF-8 с BOM: Windows PowerShell 5.1 иначе читает кириллицу в комментариях как мусор.
Add-Type -AssemblyName System.Drawing
$ErrorActionPreference = 'Stop'

$root = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$src = Join-Path $root 'docs\brand\1base-logo.png'
$pub = Join-Path $root 'frontend\public'
New-Item -ItemType Directory -Force (Join-Path $pub 'brand') | Out-Null
New-Item -ItemType Directory -Force (Join-Path $pub 'icons') | Out-Null

$navy = [System.Drawing.Color]::FromArgb(255, 0, 16, 66)
$blueB = 248; $navyB = 66   # канал B: тёмно-синий фон → синяя единица
$navyR = 0                  # канал R: тёмно-синий фон → белые буквы

$bmp = New-Object System.Drawing.Bitmap $src
# Границы надписи (по анализу исходника): единица x 129..261, «Base» x 295..1135, y 467..761. Запас — 6 px.
$x0 = 123; $x1 = 1141; $y0 = 461; $y1 = 767; $split = 278
$w = $x1 - $x0; $h = $y1 - $y0

# --- Wordmark с прозрачностью: альфа по удалению цвета от фона, цвет буквы — чистый (синий или белый). ---
$rect = New-Object System.Drawing.Rectangle $x0, $y0, $w, $h
$data = $bmp.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadOnly, [System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
$stride = $data.Stride
$bytes = New-Object byte[] ($stride * $h)
[System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $bytes, 0, $bytes.Length)
$bmp.UnlockBits($data)

$mark = New-Object System.Drawing.Bitmap $w, $h, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$mrect = New-Object System.Drawing.Rectangle 0, 0, $w, $h
$mdata = $mark.LockBits($mrect, [System.Drawing.Imaging.ImageLockMode]::WriteOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$mstride = $mdata.Stride
$out = New-Object byte[] ($mstride * $h)
for ($y = 0; $y -lt $h; $y++) {
  for ($x = 0; $x -lt $w; $x++) {
    $i = $y * $stride + $x * 3
    $b = $bytes[$i]; $g = $bytes[$i + 1]; $r = $bytes[$i + 2]
    $o = $y * $mstride + $x * 4
    if (($x + $x0) -lt $split) {
      # синяя единица: прозрачность по синему каналу
      $a = [Math]::Round(255.0 * ($b - $navyB) / ($blueB - $navyB))
      $out[$o] = 248; $out[$o + 1] = 129; $out[$o + 2] = 5
    } else {
      # белые буквы: прозрачность по красному каналу
      $a = [Math]::Round(255.0 * ($r - $navyR) / (255 - $navyR))
      $out[$o] = 255; $out[$o + 1] = 255; $out[$o + 2] = 255
    }
    # Порог: шум фона исходника (альфа до 5%) — прозрачно, почти буква (от 95%) — непрозрачно; иначе за надписью виден прямоугольник.
    if ($a -lt 14) { $a = 0 } elseif ($a -gt 241) { $a = 255 }
    $out[$o + 3] = [byte]$a
  }
}
[System.Runtime.InteropServices.Marshal]::Copy($out, 0, $mdata.Scan0, $out.Length)
$mark.UnlockBits($mdata)

function Resize([System.Drawing.Image]$img, [int]$width, [int]$height) {
  $r = New-Object System.Drawing.Bitmap $width, $height, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $g = [System.Drawing.Graphics]::FromImage($r)
  $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
  $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
  $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
  $g.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
  $g.DrawImage($img, 0, 0, $width, $height)
  $g.Dispose()
  return $r
}

# Wordmark для шапок: высота 120 px (4× от 30 px в интерфейсе).
$mh = 120; $mw = [int][Math]::Round($w * $mh / $h)
$wordmark = Resize $mark $mw $mh
$wordmark.Save((Join-Path $pub 'brand\logo-light.png'), [System.Drawing.Imaging.ImageFormat]::Png)
"brand/logo-light.png ${mw}x${mh}"

# Знак «1» для favicon: вырезаем единицу из wordmark.
$one = $mark.Clone((New-Object System.Drawing.Rectangle 0, 0, ($split - $x0), $h), [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)

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

# Иконка: тёмно-синий квадрат (со скруглением или без), надпись шириной scale от размера, по центру.
function Icon([int]$size, [double]$radiusShare, [double]$scale, [System.Drawing.Image]$art, [double]$artAspect) {
  $bmpOut = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $g = [System.Drawing.Graphics]::FromImage($bmpOut)
  $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
  $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
  $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
  $g.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
  $g.Clear([System.Drawing.Color]::Transparent)
  $brush = New-Object System.Drawing.SolidBrush $navy
  $path = RoundedPath $size ($size * $radiusShare)
  $g.FillPath($brush, $path)
  $aw = [int][Math]::Round($size * $scale); $ah = [int][Math]::Round($aw / $artAspect)
  $g.DrawImage($art, [int](($size - $aw) / 2), [int](($size - $ah) / 2), $aw, $ah)
  $g.Dispose(); $brush.Dispose(); $path.Dispose()
  return $bmpOut
}

$aspect = $w / $h
$oneAspect = ($split - $x0) / $h
$icons = @(
  @{ name = 'icons\icon-192.png'; size = 192; radius = 0.22; scale = 0.80 },
  @{ name = 'icons\icon-512.png'; size = 512; radius = 0.22; scale = 0.80 },
  @{ name = 'icons\icon-maskable-512.png'; size = 512; radius = 0; scale = 0.64 }, # без скругления: маску накладывает система
  @{ name = 'icons\apple-touch-icon.png'; size = 180; radius = 0; scale = 0.76 },  # iOS скругляет сам и не любит прозрачность
  @{ name = 'brand\1base-icon-1024.png'; size = 1024; radius = 0.22; scale = 0.80 }
)
foreach ($i in $icons) {
  $img = Icon $i.size $i.radius $i.scale $mark $aspect
  $img.Save((Join-Path $pub $i.name), [System.Drawing.Imaging.ImageFormat]::Png)
  $img.Dispose()
  "$($i.name) $($i.size)px"
}

# Favicon: единица на тёмно-синем скруглённом квадрате, 16/32/48 — PNG внутри ICO.
function PngBytes([System.Drawing.Image]$img) {
  $ms = New-Object System.IO.MemoryStream
  $img.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
  return ,$ms.ToArray()   # запятая: иначе PowerShell развернёт массив байтов по одному
}
$sizes = @(16, 32, 48)
$entries = @()
foreach ($s in $sizes) {
  $img = Icon $s 0.22 0.0 $one $oneAspect
  # единица высотой 62% иконки
  $g = [System.Drawing.Graphics]::FromImage($img)
  $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
  $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
  $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
  $ah = [int][Math]::Round($s * 0.62); $aw = [int][Math]::Round($ah * $oneAspect)
  $g.DrawImage($one, [int](($s - $aw) / 2), [int](($s - $ah) / 2), $aw, $ah)
  $g.Dispose()
  $entries += ,@{ size = $s; png = (PngBytes $img) }
  if ($s -eq 32) { $img.Save((Join-Path $pub 'brand\mark-32.png'), [System.Drawing.Imaging.ImageFormat]::Png) }
  $img.Dispose()
}
$ico = New-Object System.IO.MemoryStream
$bw = New-Object System.IO.BinaryWriter $ico
$bw.Write([uint16]0); $bw.Write([uint16]1); $bw.Write([uint16]$entries.Count)
$offset = 6 + 16 * $entries.Count
foreach ($e in $entries) {
  $bw.Write([byte]$(if ($e.size -ge 256) { 0 } else { $e.size })); $bw.Write([byte]$(if ($e.size -ge 256) { 0 } else { $e.size }))
  $bw.Write([byte]0); $bw.Write([byte]0); $bw.Write([uint16]1); $bw.Write([uint16]32)
  $bw.Write([uint32]([byte[]]$e.png).Length); $bw.Write([uint32]$offset)
  $offset += ([byte[]]$e.png).Length
}
foreach ($e in $entries) { $bw.Write([byte[]]$e.png) }
$bw.Flush()
[System.IO.File]::WriteAllBytes((Join-Path $pub 'favicon.ico'), $ico.ToArray())
"favicon.ico: $($ico.Length) bytes ($($sizes -join ','))"

$one.Dispose(); $wordmark.Dispose(); $mark.Dispose(); $bmp.Dispose()
