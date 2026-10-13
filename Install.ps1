param([string]$GameDirectory = 'C:\Program Files (x86)\Steam\steamapps\common\Terraria')

$ErrorActionPreference = 'Stop'
$GameDirectory = (Resolve-Path -LiteralPath $GameDirectory).Path
$game = Join-Path $GameDirectory 'Terraria.exe'
$manifestPath = Join-Path $PSScriptRoot 'build-manifest.json'
$receiptPath = Join-Path $GameDirectory 'ReLogicHijack.install.json'

if ((Test-Path -LiteralPath (Join-Path $GameDirectory 'TerrariaOverlayPoC.install.json')) -or
    (Test-Path -LiteralPath (Join-Path $GameDirectory 'TerrariaOverlayPoC.dll'))) {
    throw 'A release from before the ReLogicHijack rename exists. Run Uninstall.ps1 first.'
}

$names = @('ReLogic.dll', 'ReLogicHijack.dll')
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
if ($manifest.FlightLab -or $manifest.FullbrightLab -or $manifest.UtilitiesLab) {
    throw 'Refusing to install a scripted lab build. Run Build.ps1 without lab switches first.'
}

$before = (Get-FileHash -LiteralPath $game -Algorithm SHA256).Hash
if ($before -ne $manifest.GameExeSHA256) {
    throw 'Terraria.exe differs from the build manifest. Rebuild against this version first.'
}

foreach ($p in @(Get-Process -Name Terraria -ErrorAction SilentlyContinue)) {
    if (-not $p.Path -or [string]::Equals($p.Path, $game, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Close Terraria before installing.'
    }
}

# Validate every destination before copying any DLLs.
foreach ($name in $names) {
    $source = Join-Path $PSScriptRoot ('bin\' + $name)
    $expected = $manifest.Files.PSObject.Properties[$name].Value
    if ((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash -ne $expected) {
        throw "Package hash mismatch: $name"
    }

    if (Test-Path -LiteralPath (Join-Path $GameDirectory $name)) {
        throw "$name already exists. Uninstall the previous ReLogicHijack installation first; existing files will not be overwritten."
    }
}
if (Test-Path -LiteralPath $receiptPath) { throw 'An installation receipt already exists. Run Uninstall.ps1 first.' }

$created = @()
try {
    # Install the overlay before the proxy which makes it loadable.
    foreach ($name in @('ReLogicHijack.dll', 'ReLogic.dll')) {
        $destination = Join-Path $GameDirectory $name
        [IO.File]::Copy((Join-Path $PSScriptRoot ('bin\' + $name)), $destination, $false)
        $created += $destination

        if ((Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash -ne $manifest.Files.PSObject.Properties[$name].Value) {
            throw "Installed hash mismatch: $name"
        }
    }
    if ((Get-FileHash -LiteralPath $game -Algorithm SHA256).Hash -ne $before) { throw 'Game executable changed during installation.' }

    $receipt = @{
        GameVersion = $manifest.GameVersion
        GameExeSHA256 = $before
        Files = $manifest.Files
        InstalledUtc = [DateTime]::UtcNow.ToString('o')
    }

    $json = $receipt | ConvertTo-Json -Depth 4
    $stream = [IO.File]::Open($receiptPath, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write)
    $created += $receiptPath
    try {
        $writer = New-Object IO.StreamWriter($stream)
        $writer.Write($json)
        $writer.Flush()
    } finally {
        $stream.Dispose()
    }

    Write-Output "Installed ReLogicHijack for Terraria $($manifest.GameVersion). Launch Terraria normally through Steam."
    Write-Output 'Original game executable verified unchanged.'
} catch {
    # Roll back only files created by this attempt.
    foreach ($path in $created) { Remove-Item -LiteralPath $path -Force }
    throw
}
