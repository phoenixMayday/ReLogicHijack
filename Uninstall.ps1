param([string]$GameDirectory = 'C:\Program Files (x86)\Steam\steamapps\common\Terraria')

$ErrorActionPreference = 'Stop'
$GameDirectory = (Resolve-Path -LiteralPath $GameDirectory).Path
$game = Join-Path $GameDirectory 'Terraria.exe'
$receiptPath = Join-Path $GameDirectory 'ReLogicHijack.install.json'

# Accept receipts from releases before the project was named ReLogicHijack.
if (-not (Test-Path -LiteralPath $receiptPath)) {
    $receiptPath = Join-Path $GameDirectory 'TerrariaOverlayPoC.install.json'
}
if (-not (Test-Path -LiteralPath $receiptPath)) {
    throw 'No ReLogicHijack installation receipt found; nothing will be removed.'
}

foreach ($p in @(Get-Process -Name Terraria -ErrorAction SilentlyContinue)) {
    if (-not $p.Path -or [string]::Equals($p.Path, $game, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Close Terraria before uninstalling.'
    }
}
$receipt = Get-Content -LiteralPath $receiptPath -Raw | ConvertFrom-Json

# Legacy filenames only support removing files owned by earlier receipts.
$names = @('ReLogic.dll', 'ReLogicHijack.dll', 'TerrariaOverlayPoC.dll', '0Harmony.dll') |
    Where-Object { $receipt.Files.PSObject.Properties[$_] }
$before = (Get-FileHash -LiteralPath $game -Algorithm SHA256).Hash

# Fixed file names only; never take a removal path from the receipt.
foreach ($name in $names) {
    $path = Join-Path $GameDirectory $name
    if ((Test-Path -LiteralPath $path) -and (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $receipt.Files.PSObject.Properties[$name].Value) {
        throw "$name changed since installation. Inspect it before removing it."
    }
}
foreach ($name in $names) {
    $path = Join-Path $GameDirectory $name
    if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path -Force }
}
Remove-Item -LiteralPath $receiptPath -Force

if ((Get-FileHash -LiteralPath $game -Algorithm SHA256).Hash -ne $before) { throw 'Game executable changed during removal.' }

Write-Output 'ReLogicHijack files removed. Original executable verified unchanged.'
