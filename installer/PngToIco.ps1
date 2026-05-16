# ICO multi-tailles (PNG par entree) — compatible raccourcis Windows
param([string]$PngPath, [string]$IcoPath)
$PngPath = (Resolve-Path $PngPath).Path
Add-Type -AssemblyName System.Drawing
$src = [System.Drawing.Image]::FromFile($PngPath)
$sizes = @(16, 32, 48, 256)
$pngStreams = New-Object System.Collections.Generic.List[byte[]]
foreach ($sz in $sizes) {
    $bmp = New-Object System.Drawing.Bitmap $sz, $sz
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $g.DrawImage($src, 0, 0, $sz, $sz)
    $g.Dispose()
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngStreams.Add($ms.ToArray())
    $bmp.Dispose()
    $ms.Dispose()
}
$src.Dispose()

$out = New-Object System.IO.MemoryStream
$bw = New-Object System.IO.BinaryWriter $out
$bw.Write([uint16]0)
$bw.Write([uint16]1)
$bw.Write([uint16]$sizes.Count)
$offset = 6 + (16 * $sizes.Count)
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $sz = $sizes[$i]
    $png = $pngStreams[$i]
    if ($sz -ge 256) { $w = [byte]0; $h = [byte]0 } else { $w = [byte]$sz; $h = [byte]$sz }
    $bw.Write($w)
    $bw.Write($h)
    $bw.Write([byte]0)
    $bw.Write([byte]0)
    $bw.Write([uint16]1)
    $bw.Write([uint16]32)
    $bw.Write([uint32]$png.Length)
    $bw.Write([uint32]$offset)
    $offset += $png.Length
}
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $bw.Write($pngStreams[$i])
}
$dir = Split-Path $IcoPath -Parent
if (-not [string]::IsNullOrEmpty($dir) -and -not (Test-Path $dir)) {
    New-Item -ItemType Directory -Path $dir -Force | Out-Null
}
[System.IO.File]::WriteAllBytes($IcoPath, $out.ToArray())
$bw.Dispose()
$out.Dispose()
Write-Host "ICO cree : $IcoPath"
