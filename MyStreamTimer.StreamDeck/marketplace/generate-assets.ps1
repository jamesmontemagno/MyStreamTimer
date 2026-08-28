param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.Drawing

$marketplaceDir = $PSScriptRoot
$streamDeckDir = Split-Path $marketplaceDir -Parent
$repoRoot = Split-Path $streamDeckDir -Parent
$pluginDir = Join-Path $streamDeckDir 'com.refractored.mystreamtimer.sdPlugin'

function New-RoundedPath {
    param(
        [System.Drawing.RectangleF]$Bounds,
        [float]$Radius
    )

    $diameter = $Radius * 2
    $path = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $path.AddArc($Bounds.X, $Bounds.Y, $diameter, $diameter, 180, 90)
    $path.AddArc($Bounds.Right - $diameter, $Bounds.Y, $diameter, $diameter, 270, 90)
    $path.AddArc($Bounds.Right - $diameter, $Bounds.Bottom - $diameter, $diameter, $diameter, 0, 90)
    $path.AddArc($Bounds.X, $Bounds.Bottom - $diameter, $diameter, $diameter, 90, 90)
    $path.CloseFigure()
    return $path
}

function Fill-RoundedRectangle {
    param(
        [System.Drawing.Graphics]$Graphics,
        [System.Drawing.Brush]$Brush,
        [System.Drawing.RectangleF]$Bounds,
        [float]$Radius
    )

    $path = New-RoundedPath -Bounds $Bounds -Radius $Radius
    try {
        $Graphics.FillPath($Brush, $path)
    }
    finally {
        $path.Dispose()
    }
}

function Draw-RoundedRectangle {
    param(
        [System.Drawing.Graphics]$Graphics,
        [System.Drawing.Pen]$Pen,
        [System.Drawing.RectangleF]$Bounds,
        [float]$Radius
    )

    $path = New-RoundedPath -Bounds $Bounds -Radius $Radius
    try {
        $Graphics.DrawPath($Pen, $path)
    }
    finally {
        $path.Dispose()
    }
}

function Draw-FitImage {
    param(
        [System.Drawing.Graphics]$Graphics,
        [System.Drawing.Image]$Image,
        [System.Drawing.RectangleF]$Bounds
    )

    $scale = [Math]::Min($Bounds.Width / $Image.Width, $Bounds.Height / $Image.Height)
    $width = [float]($Image.Width * $scale)
    $height = [float]($Image.Height * $scale)
    $destination = [System.Drawing.RectangleF]::new(
        $Bounds.X + (($Bounds.Width - $width) / 2),
        $Bounds.Y + (($Bounds.Height - $height) / 2),
        $width,
        $height
    )
    $Graphics.DrawImage($Image, $destination)
}

function Draw-ClockMark {
    param(
        [System.Drawing.Graphics]$Graphics,
        [float]$CenterX,
        [float]$CenterY,
        [float]$Radius,
        [System.Drawing.Color]$Color,
        [bool]$ShowPlay = $false,
        [bool]$ShowPause = $false
    )

    $pen = [System.Drawing.Pen]::new($Color, [Math]::Max(4, $Radius * 0.13))
    $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    try {
        $Graphics.DrawEllipse($pen, $CenterX - $Radius, $CenterY - $Radius, $Radius * 2, $Radius * 2)
        $Graphics.DrawLine($pen, $CenterX, $CenterY, $CenterX, $CenterY - ($Radius * 0.55))
        $Graphics.DrawLine($pen, $CenterX, $CenterY, $CenterX + ($Radius * 0.45), $CenterY + ($Radius * 0.30))
    }
    finally {
        $pen.Dispose()
    }

    if ($ShowPlay) {
        $brush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 110, 234, 174))
        $points = @(
            [System.Drawing.PointF]::new($CenterX + ($Radius * 0.72), $CenterY + ($Radius * 0.10)),
            [System.Drawing.PointF]::new($CenterX + ($Radius * 1.48), $CenterY + ($Radius * 0.55)),
            [System.Drawing.PointF]::new($CenterX + ($Radius * 0.72), $CenterY + ($Radius * 1.00))
        )
        $Graphics.FillPolygon($brush, $points)
        $brush.Dispose()
    }

    if ($ShowPause) {
        $brush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 255, 190, 75))
        $barWidth = $Radius * 0.20
        $barHeight = $Radius * 0.80
        $Graphics.FillRectangle($brush, $CenterX + ($Radius * 0.80), $CenterY + ($Radius * 0.20), $barWidth, $barHeight)
        $Graphics.FillRectangle($brush, $CenterX + ($Radius * 1.15), $CenterY + ($Radius * 0.20), $barWidth, $barHeight)
        $brush.Dispose()
    }
}

function Draw-FileMark {
    param(
        [System.Drawing.Graphics]$Graphics,
        [float]$X,
        [float]$Y,
        [float]$Size,
        [bool]$ShowPlay = $false,
        [bool]$ShowPause = $false
    )

    $paper = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 231, 240, 250))
    $ink = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(255, 26, 43, 72), [Math]::Max(3, $Size * 0.04))
    $accent = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 86, 224, 210))
    try {
        $points = @(
            [System.Drawing.PointF]::new($X, $Y),
            [System.Drawing.PointF]::new($X + ($Size * 0.68), $Y),
            [System.Drawing.PointF]::new($X + $Size, $Y + ($Size * 0.30)),
            [System.Drawing.PointF]::new($X + $Size, $Y + $Size),
            [System.Drawing.PointF]::new($X, $Y + $Size)
        )
        $Graphics.FillPolygon($paper, $points)
        $fold = @(
            [System.Drawing.PointF]::new($X + ($Size * 0.68), $Y),
            [System.Drawing.PointF]::new($X + ($Size * 0.68), $Y + ($Size * 0.30)),
            [System.Drawing.PointF]::new($X + $Size, $Y + ($Size * 0.30))
        )
        $Graphics.FillPolygon($accent, $fold)
        $Graphics.DrawLine($ink, $X + ($Size * 0.20), $Y + ($Size * 0.52), $X + ($Size * 0.76), $Y + ($Size * 0.52))
        $Graphics.DrawLine($ink, $X + ($Size * 0.20), $Y + ($Size * 0.72), $X + ($Size * 0.62), $Y + ($Size * 0.72))
    }
    finally {
        $paper.Dispose()
        $ink.Dispose()
        $accent.Dispose()
    }

    if ($ShowPlay) {
        $brush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 110, 234, 174))
        $points = @(
            [System.Drawing.PointF]::new($X + ($Size * 0.86), $Y + ($Size * 0.58)),
            [System.Drawing.PointF]::new($X + ($Size * 1.32), $Y + ($Size * 0.82)),
            [System.Drawing.PointF]::new($X + ($Size * 0.86), $Y + ($Size * 1.06))
        )
        $Graphics.FillPolygon($brush, $points)
        $brush.Dispose()
    }

    if ($ShowPause) {
        $brush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 255, 190, 75))
        $Graphics.FillRectangle($brush, $X + ($Size * 0.94), $Y + ($Size * 0.62), $Size * 0.10, $Size * 0.42)
        $Graphics.FillRectangle($brush, $X + ($Size * 1.12), $Y + ($Size * 0.62), $Size * 0.10, $Size * 0.42)
        $brush.Dispose()
    }
}

function Draw-Key {
    param(
        [System.Drawing.Graphics]$Graphics,
        [float]$X,
        [float]$Y,
        [string]$Label,
        [ValidateSet('clock-play', 'clock-pause', 'file-play', 'file-pause')]
        [string]$Kind
    )

    $bounds = [System.Drawing.RectangleF]::new($X, $Y, 210, 210)
    $shadowBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(70, 0, 0, 0))
    $keyBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 12, 23, 43))
    $borderPen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(255, 91, 112, 145), 2)
    try {
        Fill-RoundedRectangle -Graphics $Graphics -Brush $shadowBrush -Bounds ([System.Drawing.RectangleF]::new($X + 8, $Y + 12, 210, 210)) -Radius 28
        Fill-RoundedRectangle -Graphics $Graphics -Brush $keyBrush -Bounds $bounds -Radius 28
        Draw-RoundedRectangle -Graphics $Graphics -Pen $borderPen -Bounds $bounds -Radius 28
    }
    finally {
        $shadowBrush.Dispose()
        $keyBrush.Dispose()
        $borderPen.Dispose()
    }

    switch ($Kind) {
        'clock-play' { Draw-ClockMark -Graphics $Graphics -CenterX ($X + 92) -CenterY ($Y + 82) -Radius 38 -Color ([System.Drawing.Color]::FromArgb(255, 86, 224, 210)) -ShowPlay $true }
        'clock-pause' { Draw-ClockMark -Graphics $Graphics -CenterX ($X + 92) -CenterY ($Y + 82) -Radius 38 -Color ([System.Drawing.Color]::FromArgb(255, 86, 224, 210)) -ShowPause $true }
        'file-play' { Draw-FileMark -Graphics $Graphics -X ($X + 64) -Y ($Y + 39) -Size 78 -ShowPlay $true }
        'file-pause' { Draw-FileMark -Graphics $Graphics -X ($X + 64) -Y ($Y + 39) -Size 78 -ShowPause $true }
    }

    $font = [System.Drawing.Font]::new('Segoe UI Semibold', 22, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
    $brush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::White)
    $format = [System.Drawing.StringFormat]::new()
    $format.Alignment = [System.Drawing.StringAlignment]::Center
    $format.LineAlignment = [System.Drawing.StringAlignment]::Center
    try {
        $Graphics.DrawString($Label, $font, $brush, [System.Drawing.RectangleF]::new($X + 12, $Y + 142, 186, 54), $format)
    }
    finally {
        $font.Dispose()
        $brush.Dispose()
        $format.Dispose()
    }
}

function New-GalleryCanvas {
    $bitmap = [System.Drawing.Bitmap]::new(1920, 960, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::ClearTypeGridFit

    $background = [System.Drawing.Drawing2D.LinearGradientBrush]::new(
        [System.Drawing.Rectangle]::new(0, 0, 1920, 960),
        [System.Drawing.Color]::FromArgb(255, 13, 28, 52),
        [System.Drawing.Color]::FromArgb(255, 49, 29, 88),
        12
    )
    $graphics.FillRectangle($background, 0, 0, 1920, 960)
    $background.Dispose()

    return @{
        Bitmap = $bitmap
        Graphics = $graphics
    }
}

function Draw-Heading {
    param(
        [System.Drawing.Graphics]$Graphics,
        [string]$Eyebrow,
        [string]$Title,
        [string]$Subtitle,
        [System.Drawing.RectangleF]$Bounds
    )

    $eyebrowFont = [System.Drawing.Font]::new('Segoe UI Semibold', 24, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
    $titleFont = [System.Drawing.Font]::new('Segoe UI Semibold', 62, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
    $subtitleFont = [System.Drawing.Font]::new('Segoe UI', 28, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
    $accentBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 86, 224, 210))
    $whiteBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::White)
    $mutedBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 193, 202, 222))
    try {
        $Graphics.DrawString($Eyebrow.ToUpperInvariant(), $eyebrowFont, $accentBrush, $Bounds.X, $Bounds.Y)
        $Graphics.DrawString($Title, $titleFont, $whiteBrush, [System.Drawing.RectangleF]::new($Bounds.X, $Bounds.Y + 42, $Bounds.Width, 170))
        $Graphics.DrawString($Subtitle, $subtitleFont, $mutedBrush, [System.Drawing.RectangleF]::new($Bounds.X, $Bounds.Y + 205, $Bounds.Width, 110))
    }
    finally {
        $eyebrowFont.Dispose()
        $titleFont.Dispose()
        $subtitleFont.Dispose()
        $accentBrush.Dispose()
        $whiteBrush.Dispose()
        $mutedBrush.Dispose()
    }
}

function Draw-ScreenshotCard {
    param(
        [System.Drawing.Graphics]$Graphics,
        [string]$ImagePath,
        [System.Drawing.RectangleF]$Bounds,
        [System.Drawing.Rectangle]$SourceBounds = [System.Drawing.Rectangle]::Empty
    )

    $shadowBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(95, 0, 0, 0))
    $cardBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 238, 242, 248))
    $borderPen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(255, 112, 130, 160), 2)
    try {
        Fill-RoundedRectangle -Graphics $Graphics -Brush $shadowBrush -Bounds ([System.Drawing.RectangleF]::new($Bounds.X + 14, $Bounds.Y + 18, $Bounds.Width, $Bounds.Height)) -Radius 26
        Fill-RoundedRectangle -Graphics $Graphics -Brush $cardBrush -Bounds $Bounds -Radius 26
        Draw-RoundedRectangle -Graphics $Graphics -Pen $borderPen -Bounds $Bounds -Radius 26

        $clipPath = New-RoundedPath -Bounds ([System.Drawing.RectangleF]::new($Bounds.X + 12, $Bounds.Y + 12, $Bounds.Width - 24, $Bounds.Height - 24)) -Radius 18
        $previousClip = $Graphics.Clip
        $Graphics.SetClip($clipPath)
        $image = [System.Drawing.Image]::FromFile($ImagePath)
        try {
            if ($SourceBounds.IsEmpty) {
                $SourceBounds = [System.Drawing.Rectangle]::new(0, 0, $image.Width, $image.Height)
            }

            $imageBounds = [System.Drawing.RectangleF]::new($Bounds.X + 12, $Bounds.Y + 12, $Bounds.Width - 24, $Bounds.Height - 24)
            $scale = [Math]::Min($imageBounds.Width / $SourceBounds.Width, $imageBounds.Height / $SourceBounds.Height)
            $width = [float]($SourceBounds.Width * $scale)
            $height = [float]($SourceBounds.Height * $scale)
            $destination = [System.Drawing.RectangleF]::new(
                $imageBounds.X + (($imageBounds.Width - $width) / 2),
                $imageBounds.Y + (($imageBounds.Height - $height) / 2),
                $width,
                $height
            )
            $Graphics.DrawImage($image, $destination, $SourceBounds, [System.Drawing.GraphicsUnit]::Pixel)
        }
        finally {
            $image.Dispose()
            $Graphics.Clip = $previousClip
            $previousClip.Dispose()
            $clipPath.Dispose()
        }
    }
    finally {
        $shadowBrush.Dispose()
        $cardBrush.Dispose()
        $borderPen.Dispose()
    }
}

function Save-Canvas {
    param(
        [hashtable]$Canvas,
        [string]$Path
    )

    try {
        $Canvas.Bitmap.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $Canvas.Graphics.Dispose()
        $Canvas.Bitmap.Dispose()
    }
}

function Convert-ToWhiteIcon {
    param([string]$Path)

    $source = [System.Drawing.Bitmap]::new($Path)
    try {
        $output = [System.Drawing.Bitmap]::new($source.Width, $source.Height, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        try {
            for ($y = 0; $y -lt $source.Height; $y++) {
                for ($x = 0; $x -lt $source.Width; $x++) {
                    $pixel = $source.GetPixel($x, $y)
                    $output.SetPixel($x, $y, [System.Drawing.Color]::FromArgb($pixel.A, 255, 255, 255))
                }
            }
            $temporaryPath = "$Path.tmp.png"
            $output.Save($temporaryPath, [System.Drawing.Imaging.ImageFormat]::Png)
        }
        finally {
            $output.Dispose()
        }
    }
    finally {
        $source.Dispose()
    }

    Move-Item -Path $temporaryPath -Destination $Path -Force
}

$actionListIcons = @(
    'imgs/plugin/category-icon.png',
    'imgs/plugin/category-icon@2x.png',
    'imgs/actions/stream-start/icon.png',
    'imgs/actions/stream-start/icon@2x.png',
    'imgs/actions/stream-control/icon.png',
    'imgs/actions/stream-control/icon@2x.png',
    'imgs/actions/file-start/icon.png',
    'imgs/actions/file-start/icon@2x.png',
    'imgs/actions/file-control/icon.png',
    'imgs/actions/file-control/icon@2x.png'
)

foreach ($relativePath in $actionListIcons) {
    Convert-ToWhiteIcon -Path (Join-Path $pluginDir $relativePath)
}

$thumbnail = New-GalleryCanvas
Draw-Heading -Graphics $thumbnail.Graphics -Eyebrow 'My Stream Timer for Stream Deck' -Title "Your timers.`nOne keypress away." -Subtitle 'Start, pause, adjust, reset, and stop timers without leaving your stream.' -Bounds ([System.Drawing.RectangleF]::new(120, 118, 870, 330))
Draw-Key -Graphics $thumbnail.Graphics -X 1050 -Y 170 -Label 'Start' -Kind 'clock-play'
Draw-Key -Graphics $thumbnail.Graphics -X 1300 -Y 170 -Label 'Pause' -Kind 'clock-pause'
Draw-Key -Graphics $thumbnail.Graphics -X 1050 -Y 430 -Label 'File timer' -Kind 'file-play'
Draw-Key -Graphics $thumbnail.Graphics -X 1300 -Y 430 -Label 'Reset' -Kind 'file-pause'
Save-Canvas -Canvas $thumbnail -Path (Join-Path $marketplaceDir 'thumbnail-1920x960.png')

$gallery1 = New-GalleryCanvas
Draw-Heading -Graphics $gallery1.Graphics -Eyebrow 'App timer actions' -Title "Control every timer`nfrom Stream Deck." -Subtitle 'Launch countdowns, count-ups, and clocks. Pause, resume, adjust, reset, or stop in an instant.' -Bounds ([System.Drawing.RectangleF]::new(105, 135, 690, 350))
Draw-Key -Graphics $gallery1.Graphics -X 125 -Y 575 -Label 'Start 5 min' -Kind 'clock-play'
Draw-Key -Graphics $gallery1.Graphics -X 375 -Y 575 -Label 'Pause' -Kind 'clock-pause'
Draw-ScreenshotCard -Graphics $gallery1.Graphics -ImagePath (Join-Path $repoRoot 'winui-migration/screenshots/dashboard-final.png') -Bounds ([System.Drawing.RectangleF]::new(825, 105, 980, 750)) -SourceBounds ([System.Drawing.Rectangle]::new(0, 29, 2042, 1250))
Save-Canvas -Canvas $gallery1 -Path (Join-Path $marketplaceDir 'gallery-01-app-control.png')

$gallery2 = New-GalleryCanvas
Draw-ScreenshotCard -Graphics $gallery2.Graphics -ImagePath (Join-Path $repoRoot 'winui-migration/screenshots/gate-timer-dark-running.png') -Bounds ([System.Drawing.RectangleF]::new(105, 105, 1040, 750)) -SourceBounds ([System.Drawing.Rectangle]::new(0, 29, 1754, 951))
Draw-Heading -Graphics $gallery2.Graphics -Eyebrow 'Live control' -Title "Stay focused`non your stream." -Subtitle 'Run the commands you use most without switching windows or breaking your flow.' -Bounds ([System.Drawing.RectangleF]::new(1215, 175, 610, 340))
Draw-Key -Graphics $gallery2.Graphics -X 1260 -Y 590 -Label '+1 minute' -Kind 'clock-play'
Draw-Key -Graphics $gallery2.Graphics -X 1510 -Y 590 -Label 'Reset' -Kind 'clock-pause'
Save-Canvas -Canvas $gallery2 -Path (Join-Path $marketplaceDir 'gallery-02-live-control.png')

$gallery3 = New-GalleryCanvas
Draw-Heading -Graphics $gallery3.Graphics -Eyebrow 'Independent file timers' -Title "Timer text ready`nfor your overlay." -Subtitle 'Write countdown, count-up, or clock output directly to a text file for streaming software.' -Bounds ([System.Drawing.RectangleF]::new(105, 145, 750, 340))
Draw-Key -Graphics $gallery3.Graphics -X 140 -Y 575 -Label 'Start file' -Kind 'file-play'
Draw-Key -Graphics $gallery3.Graphics -X 390 -Y 575 -Label 'Pause file' -Kind 'file-pause'

$flowBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 12, 23, 43))
$flowBorder = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(255, 91, 112, 145), 3)
$flowAccent = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(255, 86, 224, 210), 8)
$flowTitleFont = [System.Drawing.Font]::new('Segoe UI Semibold', 34, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
$flowValueFont = [System.Drawing.Font]::new('Cascadia Mono', 66, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
$flowTextBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::White)
$flowMutedBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 193, 202, 222))
try {
    $flowBounds = [System.Drawing.RectangleF]::new(930, 165, 790, 600)
    Fill-RoundedRectangle -Graphics $gallery3.Graphics -Brush $flowBrush -Bounds $flowBounds -Radius 32
    Draw-RoundedRectangle -Graphics $gallery3.Graphics -Pen $flowBorder -Bounds $flowBounds -Radius 32
    $gallery3.Graphics.DrawString('countdown.txt', $flowTitleFont, $flowTextBrush, 1010, 245)
    $gallery3.Graphics.DrawString('Starting in', $flowTitleFont, $flowMutedBrush, 1010, 365)
    $gallery3.Graphics.DrawString('00:04:58', $flowValueFont, $flowTextBrush, 1010, 430)
    $gallery3.Graphics.DrawLine($flowAccent, 1010, 570, 1600, 570)
    $gallery3.Graphics.DrawString('Updates automatically while your timer runs', $flowTitleFont, $flowMutedBrush, 1010, 620)
}
finally {
    $flowBrush.Dispose()
    $flowBorder.Dispose()
    $flowAccent.Dispose()
    $flowTitleFont.Dispose()
    $flowValueFont.Dispose()
    $flowTextBrush.Dispose()
    $flowMutedBrush.Dispose()
}
Save-Canvas -Canvas $gallery3 -Path (Join-Path $marketplaceDir 'gallery-03-file-output.png')

$marketplaceIconSource = [System.Drawing.Image]::FromFile((Join-Path $repoRoot 'Art/NewArt1024.png'))
try {
    $marketplaceIcon = [System.Drawing.Bitmap]::new(288, 288, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($marketplaceIcon)
    try {
        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $graphics.DrawImage($marketplaceIconSource, 0, 0, 288, 288)
        $marketplaceIcon.Save((Join-Path $marketplaceDir 'app-icon-288.png'), [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $graphics.Dispose()
        $marketplaceIcon.Dispose()
    }
}
finally {
    $marketplaceIconSource.Dispose()
}

Write-Host 'Updated Stream Deck action-list icons and Marketplace media.'
