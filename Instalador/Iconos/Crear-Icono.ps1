# Dibuja el icono (bola de bingo con una letra) de Gregal y de
# BingoHistoricoEditor. No hace falta para compilar: los .ico ya están en cada
# repo. Para rehacerlos (PowerShell 5.1, usa System.Drawing):
#   .\Crear-Icono.ps1 -Salida ..\..\Gregal\Gregal.ico -Letra G -ColorFondo '#0F2A4A' -ColorBola '#1E6FD9'
#   .\Crear-Icono.ps1 -Salida C:\CSHARP\BingoHistoricoEditor\BingoHistoricoEditor.ico -Letra H -ColorFondo '#4A2A0F' -ColorBola '#E8771E'
param(
    [Parameter(Mandatory)][string]$Salida,
    [Parameter(Mandatory)][string]$Letra,
    [Parameter(Mandatory)][string]$ColorFondo,   # cuadrado
    [Parameter(Mandatory)][string]$ColorBola,
    [string]$Png                                 # vista previa opcional (256)
)
Add-Type -AssemblyName System.Drawing
$ErrorActionPreference = 'Stop'

function Dibujar([int]$n) {
    $bmp = New-Object System.Drawing.Bitmap $n, $n, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'; $g.TextRenderingHint = 'AntiAliasGridFit'; $g.PixelOffsetMode = 'HighQuality'
    $g.Clear([System.Drawing.Color]::Transparent)
    $fondo = [System.Drawing.ColorTranslator]::FromHtml($ColorFondo)
    $bola = [System.Drawing.ColorTranslator]::FromHtml($ColorBola)

    # Cuadrado redondeado
    $r = [single]($n * 0.22); $w = [single]($n - 1)
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $p.AddArc(0, 0, $r, $r, 180, 90); $p.AddArc($w - $r, 0, $r, $r, 270, 90)
    $p.AddArc($w - $r, $w - $r, $r, $r, 0, 90); $p.AddArc(0, $w - $r, $r, $r, 90, 90); $p.CloseFigure()
    $g.FillPath((New-Object System.Drawing.SolidBrush $fondo), $p)

    # Bola de color con brillo
    $pequeno = $n -le 24   # en 16-24 px prima que se lea la letra
    $m = [single]($n * $(if ($pequeno) { 0.0 } else { 0.08 })); $d = [single]($n - 2 * $m)
    $rect = New-Object System.Drawing.RectangleF $m, $m, $d, $d
    $bp = New-Object System.Drawing.Drawing2D.GraphicsPath; $bp.AddEllipse($rect)
    $grad = New-Object System.Drawing.Drawing2D.PathGradientBrush $bp
    $grad.CenterColor = [System.Drawing.Color]::FromArgb(255, [Math]::Min(255, $bola.R + 70), [Math]::Min(255, $bola.G + 70), [Math]::Min(255, $bola.B + 70))
    $grad.SurroundColors = @($bola)
    $grad.CenterPoint = New-Object System.Drawing.PointF ([single]($n * 0.38)), ([single]($n * 0.36))
    $g.FillEllipse($grad, $rect)

    # Disco blanco con la letra
    $dd = [single]($d * $(if ($pequeno) { 0.80 } else { 0.62 })); $o = [single](($n - $dd) / 2)
    $g.FillEllipse([System.Drawing.Brushes]::White, $o, $o, $dd, $dd)
    $font = New-Object System.Drawing.Font 'Arial', ([single]($dd * $(if ($pequeno) { 0.82 } else { 0.72 }))), ([System.Drawing.FontStyle]::Bold), ([System.Drawing.GraphicsUnit]::Pixel)
    $sf = New-Object System.Drawing.StringFormat; $sf.Alignment = 'Center'; $sf.LineAlignment = 'Center'
    $txt = New-Object System.Drawing.RectangleF $o, ([single]($o + $dd * 0.04)), $dd, $dd
    $g.DrawString($Letra, $font, (New-Object System.Drawing.SolidBrush $bola), $txt, $sf)
    $g.Dispose()
    return $bmp
}

# Entrada DIB de 32 bits (lo que entienden todos los lectores de .ico)
function Dib($bmp) {
    $n = $bmp.Width; $ms = New-Object System.IO.MemoryStream; $bw = New-Object System.IO.BinaryWriter $ms
    $bw.Write([int]40); $bw.Write([int]$n); $bw.Write([int]($n * 2)); $bw.Write([int16]1); $bw.Write([int16]32)
    $bw.Write([int]0); $bw.Write([int]0); $bw.Write([int]0); $bw.Write([int]0); $bw.Write([int]0); $bw.Write([int]0)
    for ($y = $n - 1; $y -ge 0; $y--) { for ($x = 0; $x -lt $n; $x++) {
        $c = $bmp.GetPixel($x, $y); $bw.Write([byte]$c.B); $bw.Write([byte]$c.G); $bw.Write([byte]$c.R); $bw.Write([byte]$c.A) } }
    $fila = [int]([Math]::Ceiling($n / 32.0) * 4)
    $bw.Write((New-Object byte[] ($fila * $n)))   # máscara AND vacía: manda el alfa
    $bw.Flush(); return , $ms.ToArray()
}

$tamanos = 16, 20, 24, 32, 40, 48, 64, 256
$datos = foreach ($t in $tamanos) {
    $b = Dibujar $t
    if ($t -eq 256) {
        $ms = New-Object System.IO.MemoryStream; $b.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
        if ($Png) { $b.Save($Png, [System.Drawing.Imaging.ImageFormat]::Png) }
        , $ms.ToArray()
    } else { , (Dib $b) }
    $b.Dispose()
}

$fs = [System.IO.File]::Create($Salida); $bw = New-Object System.IO.BinaryWriter $fs
$bw.Write([int16]0); $bw.Write([int16]1); $bw.Write([int16]$tamanos.Count)
$offset = 6 + 16 * $tamanos.Count
for ($i = 0; $i -lt $tamanos.Count; $i++) {
    $t = $tamanos[$i]; $lado = if ($t -ge 256) { 0 } else { $t }
    $bw.Write([byte]$lado); $bw.Write([byte]$lado); $bw.Write([byte]0); $bw.Write([byte]0)
    if ($datos[$i] -isnot [byte[]]) { throw "Entrada $t no es byte[]: $($datos[$i].GetType())" }
    $bw.Write([int16]1); $bw.Write([int16]32); $bw.Write([int]$datos[$i].Length); $bw.Write([int]$offset)
    $offset += $datos[$i].Length
}
foreach ($d in $datos) { $bw.Write([byte[]]$d) }
$bw.Close()
"Creado $Salida ($((Get-Item $Salida).Length) bytes)"
