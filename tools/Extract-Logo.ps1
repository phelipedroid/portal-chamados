param(
    [Parameter(Mandatory = $true)]
    [string]$SourcePath,

    [Parameter(Mandatory = $true)]
    [string]$OutputPath
)

Add-Type -AssemblyName System.Drawing

$outputDirectory = Split-Path -Parent $OutputPath

if (-not (Test-Path -LiteralPath $outputDirectory))
{
    New-Item `
        -ItemType Directory `
        -Path $outputDirectory `
        -Force | Out-Null
}

$source = [System.Drawing.Bitmap]::FromFile($SourcePath)

try
{
    # A arte original ocupa a faixa central do arquivo. Este recorte
    # preserva toda a marca, inclusive o "mob", e remove o espaço vazio.
    $sourceRectangle = [System.Drawing.Rectangle]::new(
        500,
        940,
        2450,
        430)

    $output = [System.Drawing.Bitmap]::new(
        $sourceRectangle.Width,
        $sourceRectangle.Height,
        [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)

    try
    {
        $graphics = [System.Drawing.Graphics]::FromImage($output)

        try
        {
            $graphics.Clear(
                [System.Drawing.Color]::Transparent)

            $attributes =
                [System.Drawing.Imaging.ImageAttributes]::new()

            try
            {
                # Remove os tons escuros do fundo sem redesenhar a marca.
                $attributes.SetColorKey(
                    [System.Drawing.Color]::FromArgb(0, 0, 0),
                    [System.Drawing.Color]::FromArgb(32, 32, 32))

                $destinationRectangle =
                    [System.Drawing.Rectangle]::new(
                        0,
                        0,
                        $output.Width,
                        $output.Height)

                $graphics.DrawImage(
                    $source,
                    $destinationRectangle,
                    $sourceRectangle.X,
                    $sourceRectangle.Y,
                    $sourceRectangle.Width,
                    $sourceRectangle.Height,
                    [System.Drawing.GraphicsUnit]::Pixel,
                    $attributes)
            }
            finally
            {
                $attributes.Dispose()
            }

        }
        finally
        {
            $graphics.Dispose()
        }

        $output.Save(
            $OutputPath,
            [System.Drawing.Imaging.ImageFormat]::Png)

        [pscustomobject]@{
            Path = $OutputPath
            Width = $output.Width
            Height = $output.Height
            SizeKB = [math]::Round(
                (Get-Item -LiteralPath $OutputPath).Length / 1KB,
                1)
        }
    }
    finally
    {
        $output.Dispose()
    }
}
finally
{
    $source.Dispose()
}
