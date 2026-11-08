# 构建番茄时钟（免安装单文件 exe）
# 依赖：Windows 自带的 .NET Framework 4.8 编译器（csc.exe）
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$src = Join-Path $root 'src'
$build = Join-Path $root 'build'
$dist = Join-Path $root 'dist'
$icon = Join-Path $build 'app.ico'

New-Item -ItemType Directory -Force -Path $build, $dist | Out-Null
Add-Type -AssemblyName System.Drawing

function New-TomatoPng([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)
    $s = [double]$size / 256.0

    # 果实
    $rect = New-Object System.Drawing.RectangleF([float](26 * $s), [float](58 * $s), [float](204 * $s), [float](176 * $s))
    $body = New-Object System.Drawing.Drawing2D.GraphicsPath
    $body.AddEllipse($rect)
    $bodyBrush = New-Object System.Drawing.Drawing2D.PathGradientBrush($body)
    $bodyBrush.CenterPoint = New-Object System.Drawing.PointF([float](96 * $s), [float](110 * $s))
    $bodyBrush.CenterColor = [System.Drawing.Color]::FromArgb(255, 255, 118, 100)
    $bodyBrush.SurroundColors = [System.Drawing.Color[]]@([System.Drawing.Color]::FromArgb(255, 209, 38, 30))
    $g.FillPath($bodyBrush, $body)

    # 高光
    $hl = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(70, 255, 255, 255))
    $g.FillEllipse($hl, [float](62 * $s), [float](92 * $s), [float](58 * $s), [float](40 * $s))

    # 叶片
    $leafBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        (New-Object System.Drawing.RectangleF(0, 0, [float]$size, [float]$size)),
        [System.Drawing.Color]::FromArgb(255, 70, 226, 160),
        [System.Drawing.Color]::FromArgb(255, 36, 158, 104), 90)
    for ($i = 0; $i -lt 5; $i++) {
        $angle = -90 + ($i - 2) * 36
        $state = $g.Save()
        $g.TranslateTransform([float](128 * $s), [float](62 * $s))
        $g.RotateTransform([float]$angle)
        $g.FillEllipse($leafBrush, [float](-10 * $s), [float](-34 * $s), [float](20 * $s), [float](36 * $s))
        $g.Restore($state)
    }

    # 梗
    $stem = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 46, 176, 118))
    $g.FillEllipse($stem, [float](120 * $s), [float](24 * $s), [float](16 * $s), [float](26 * $s))

    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bytes = $ms.ToArray()
    $ms.Dispose(); $g.Dispose(); $bmp.Dispose()
    return , $bytes
}

function New-IcoFile([string]$path) {
    $sizes = @(16, 24, 32, 48, 64, 128, 256)
    $images = @()
    foreach ($sz in $sizes) { $images += , (New-TomatoPng -size $sz) }

    $fs = [System.IO.File]::Create($path)
    $bw = New-Object System.IO.BinaryWriter($fs)
    $bw.Write([UInt16]0)
    $bw.Write([UInt16]1)
    $bw.Write([UInt16]$sizes.Count)
    $offset = 6 + 16 * $sizes.Count
    for ($i = 0; $i -lt $sizes.Count; $i++) {
        $sz = $sizes[$i]
        $data = $images[$i]
        $dim = if ($sz -ge 256) { 0 } else { $sz }
        $bw.Write([Byte]$dim)
        $bw.Write([Byte]$dim)
        $bw.Write([Byte]0)
        $bw.Write([Byte]0)
        $bw.Write([UInt16]1)
        $bw.Write([UInt16]32)
        $bw.Write([UInt32]$data.Length)
        $bw.Write([UInt32]$offset)
        $offset += $data.Length
    }
    foreach ($d in $images) { $bw.Write([byte[]]$d) }
    $bw.Flush(); $bw.Close(); $fs.Close()
}

Write-Host '生成图标...' -ForegroundColor DarkGray
New-IcoFile -path $icon

$fw = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319'
$csc = Join-Path $fw 'csc.exe'
$out = Join-Path $dist '番茄时钟.exe'

$refs = @(
    (Join-Path $fw 'System.dll'),
    (Join-Path $fw 'System.Core.dll'),
    (Join-Path $fw 'System.Drawing.dll'),
    (Join-Path $fw 'System.Windows.Forms.dll'),
    (Join-Path $fw 'System.Xaml.dll'),
    (Join-Path $fw 'WPF\PresentationFramework.dll'),
    (Join-Path $fw 'WPF\PresentationCore.dll'),
    (Join-Path $fw 'WPF\WindowsBase.dll')
)

$sources = Get-ChildItem (Join-Path $src '*.cs') | ForEach-Object { $_.FullName }

$args = @(
    '/nologo',
    '/target:winexe',
    '/platform:anycpu',
    '/optimize+',
    '/debug-',
    '/langversion:5',
    '/codepage:65001',
    "/out:$out",
    "/win32icon:$icon",
    "/win32manifest:$(Join-Path $src 'app.manifest')",
    "/resource:$(Join-Path $src 'MainWindow.xaml'),MainWindow.xaml",
    "/resource:$(Join-Path $src 'ClockWindow.xaml'),ClockWindow.xaml",
    "/resource:$icon,app.ico"
)
foreach ($r in $refs) { $args += "/r:$r" }
$args += $sources

Write-Host '编译...' -ForegroundColor DarkGray
& $csc $args
if ($LASTEXITCODE -ne 0) {
    Write-Host '编译失败' -ForegroundColor Red
    exit 1
}

$info = Get-Item $out
Write-Host ("完成: {0}  ({1:N0} 字节)" -f $info.FullName, $info.Length) -ForegroundColor Green
