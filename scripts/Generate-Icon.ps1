[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$root = Split-Path -Parent $PSScriptRoot
$output = Join-Path $root 'installer/assets'
New-Item -ItemType Directory -Path $output -Force | Out-Null
$bitmap = [Drawing.Bitmap]::new(256,256)
$graphics = [Drawing.Graphics]::FromImage($bitmap)
$graphics.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
$graphics.Clear([Drawing.Color]::Transparent)
$shield = [Drawing.Drawing2D.GraphicsPath]::new()
$shield.AddLines([Drawing.PointF[]]@([Drawing.PointF]::new(30,22),[Drawing.PointF]::new(226,22),[Drawing.PointF]::new(226,132)))
$shield.AddBezier(226,132,226,189,171,227,128,244)
$shield.AddBezier(128,244,85,227,30,189,30,132)
$shield.CloseFigure()
$blue = [Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#326FCE'))
$white = [Drawing.SolidBrush]::new([Drawing.Color]::White)
$graphics.FillPath($blue,$shield)
$pen = [Drawing.Pen]::new([Drawing.Color]::White,12)
$pen.StartCap = [Drawing.Drawing2D.LineCap]::Round
$pen.EndCap = [Drawing.Drawing2D.LineCap]::Round
$graphics.DrawLine($pen,128,192,128,78)
$graphics.DrawLine($pen,128,140,88,116)
$graphics.DrawLine($pen,88,116,88,93)
$graphics.DrawLine($pen,128,158,171,133)
$graphics.DrawLine($pen,171,133,171,104)
$graphics.FillEllipse($white,116,180,24,24)
$graphics.FillEllipse($white,78,78,20,20)
$graphics.FillRectangle($white,161,83,20,20)
$graphics.FillPolygon($white,[Drawing.PointF[]]@([Drawing.PointF]::new(128,50),[Drawing.PointF]::new(110,82),[Drawing.PointF]::new(146,82)))
$png = [IO.MemoryStream]::new()
$bitmap.Save($png,[Drawing.Imaging.ImageFormat]::Png)
$bytes = $png.ToArray()
$icon = [IO.File]::Create((Join-Path $output 'PortSentinel.ico'))
$writer = [IO.BinaryWriter]::new($icon)
$writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]1)
$writer.Write([byte]0); $writer.Write([byte]0); $writer.Write([byte]0); $writer.Write([byte]0)
$writer.Write([uint16]1); $writer.Write([uint16]32); $writer.Write([uint32]$bytes.Length); $writer.Write([uint32]22); $writer.Write($bytes)
$writer.Dispose(); $png.Dispose(); $pen.Dispose(); $blue.Dispose(); $white.Dispose(); $shield.Dispose(); $graphics.Dispose(); $bitmap.Dispose()
Write-Host 'PortSentinel.ico oluşturuldu.'
