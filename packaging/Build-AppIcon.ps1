# Render the app's simple SVG primitives with GDI+, then package PNG frames as ICO.
# No external dependencies. Run on Windows after editing Assets/app-icon.svg.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$assetDirectory = Join-Path $PSScriptRoot '..\src\Wisegar.DTXInspector.App\Assets'
[xml]$svg = [IO.File]::ReadAllText((Join-Path $assetDirectory 'app-icon.svg'))
$frames = @()
$sizes = @(16, 20, 24, 32, 48, 64, 128, 256)
foreach ($size in $sizes) {
    $bitmap = New-Object Drawing.Bitmap ($size * 4), ($size * 4)
    $graphics = [Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.ScaleTransform(($size * 4 / 64), ($size * 4 / 64))
    foreach ($element in $svg.DocumentElement.ChildNodes) {
        if ($element.LocalName -eq 'title') { continue }
        $path = New-Object Drawing.Drawing2D.GraphicsPath
        switch ($element.LocalName) {
            'rect' {
                $x = [float]$element.x; $y = [float]$element.y
                $w = [float]$element.width; $h = [float]$element.height; $d = 2 * [float]$element.rx
                $path.AddArc($x, $y, $d, $d, 180, 90)
                $path.AddArc(($x + $w - $d), $y, $d, $d, 270, 90)
                $path.AddArc(($x + $w - $d), ($y + $h - $d), $d, $d, 0, 90)
                $path.AddArc($x, ($y + $h - $d), $d, $d, 90, 90)
                $path.CloseFigure()
            }
            'circle' {
                $r = [float]$element.r
                $path.AddEllipse(([float]$element.cx - $r), ([float]$element.cy - $r), (2 * $r), (2 * $r))
            }
            'line' { $path.AddLine([float]$element.x1, [float]$element.y1, [float]$element.x2, [float]$element.y2) }
            'polyline' {
                $points = foreach ($pair in ($element.points -split '\s+')) {
                    $coordinates = $pair -split ','
                    New-Object Drawing.PointF ([float]::Parse($coordinates[0], [Globalization.CultureInfo]::InvariantCulture)), ([float]::Parse($coordinates[1], [Globalization.CultureInfo]::InvariantCulture))
                }
                $path.AddLines([Drawing.PointF[]]$points)
            }
            default { throw "Unsupported SVG element: $($element.LocalName)" }
        }
        if ($element.fill -and $element.fill -ne 'none') {
            $brush = New-Object Drawing.SolidBrush ([Drawing.ColorTranslator]::FromHtml($element.fill))
            $graphics.FillPath($brush, $path); $brush.Dispose()
        }
        if ($element.stroke) {
            $pen = New-Object Drawing.Pen ([Drawing.ColorTranslator]::FromHtml($element.stroke)), ([float]$element.'stroke-width')
            $pen.StartCap = $pen.EndCap = [Drawing.Drawing2D.LineCap]::Round
            $pen.LineJoin = [Drawing.Drawing2D.LineJoin]::Round
            $graphics.DrawPath($pen, $path); $pen.Dispose()
        }
        $path.Dispose()
    }
    $graphics.Dispose()
    $resized = New-Object Drawing.Bitmap $size, $size
    $scaled = [Drawing.Graphics]::FromImage($resized)
    $scaled.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $scaled.DrawImage($bitmap, 0, 0, $size, $size)
    $scaled.Dispose(); $bitmap.Dispose()
    $stream = New-Object IO.MemoryStream
    $resized.Save($stream, [Drawing.Imaging.ImageFormat]::Png)
    $frames += ,$stream.ToArray()
    if ($size -eq 256) { $resized.Save((Join-Path $assetDirectory 'app-icon.png'), [Drawing.Imaging.ImageFormat]::Png) }
    $stream.Dispose(); $resized.Dispose()
}
$file = [IO.File]::Create((Join-Path $assetDirectory 'app-icon.ico'))
$writer = New-Object IO.BinaryWriter $file
try {
    $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$sizes.Count)
    $offset = 6 + 16 * $sizes.Count
    for ($i = 0; $i -lt $sizes.Count; $i++) {
        $dimension = if ($sizes[$i] -eq 256) { 0 } else { $sizes[$i] }
        $writer.Write([byte]$dimension); $writer.Write([byte]$dimension)
        $writer.Write([byte]0); $writer.Write([byte]0)
        $writer.Write([uint16]1); $writer.Write([uint16]32)
        $writer.Write([uint32]$frames[$i].Length); $writer.Write([uint32]$offset)
        $offset += $frames[$i].Length
    }
    foreach ($frame in $frames) { $writer.Write([byte[]]$frame) }
} finally { $writer.Dispose(); $file.Dispose() }
