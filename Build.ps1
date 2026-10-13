param(
    [string]$GameDirectory = 'C:\Program Files (x86)\Steam\steamapps\common\Terraria',
    [string]$BuildDirectory = (Join-Path $PSScriptRoot 'work\overlay-build'),
    [switch]$FlightLab,
    [switch]$FullbrightLab,
    [string]$OutputDirectory = (Join-Path $PSScriptRoot 'bin')
)

$ErrorActionPreference = 'Stop'
$game = Join-Path $GameDirectory 'Terraria.exe'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'

if (-not (Test-Path -LiteralPath $game)) { throw "Terraria client not found: $game" }
if (-not (Test-Path -LiteralPath $compiler)) { throw '.NET Framework C# compiler not found.' }
$before = (Get-FileHash -LiteralPath $game -Algorithm SHA256).Hash

$bin = [IO.Path]::GetFullPath($OutputDirectory)
[void][IO.Directory]::CreateDirectory($BuildDirectory)
[void][IO.Directory]::CreateDirectory($bin)

# Extract a reference copy without changing the game executable.
Add-Type -Path (Join-Path $PSScriptRoot 'lib\Mono.Cecil.dll')
$assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($game)
try {
    $resource = $assembly.MainModule.Resources | Where-Object Name -eq 'Terraria.Libraries.ReLogic.ReLogic.dll'
    if (-not $resource) { throw 'This build has no supported embedded ReLogic dependency.' }

    $reference = Join-Path $BuildDirectory 'ReLogic.original.dll'
    [IO.File]::WriteAllBytes($reference, $resource.GetResourceData())
    $version = $assembly.Name.Version.ToString()
} finally {
    $assembly.Dispose()
}

$references = @()
foreach ($name in @('Microsoft.Xna.Framework', 'Microsoft.Xna.Framework.Graphics', 'Microsoft.Xna.Framework.Game')) {
    $root = Join-Path $env:WINDIR ('Microsoft.NET\assembly\GAC_32\' + $name)
    $dll = Get-ChildItem -LiteralPath $root -Recurse -Filter "$name.dll" | Select-Object -First 1
    if (-not $dll) { throw "XNA 4 dependency not installed: $name" }
    $references += '/r:' + $dll.FullName
}

$options = @(
    '/nologo', '/target:library', '/platform:x86',
    ('/out:' + (Join-Path $bin 'ReLogicHijack.dll')),
    ('/r:' + $game),
    ('/r:' + $reference)
) + $references + @(
    (Join-Path $PSScriptRoot 'src\Bootstrap.cs'),
    (Join-Path $PSScriptRoot 'src\Overlay.cs'),
    (Join-Path $PSScriptRoot 'src\FlightController.cs'),
    (Join-Path $PSScriptRoot 'src\FullbrightController.cs')
)

if ($FlightLab) {
    $options += '/define:FLIGHT_LAB'
    $options += (Join-Path $PSScriptRoot 'tests\FlightLab.cs')
}
if ($FullbrightLab) {
    $options += '/define:FULLBRIGHT_LAB'
    $options += (Join-Path $PSScriptRoot 'tests\FullbrightLab.cs')
}

& $compiler @options
if ($LASTEXITCODE -ne 0) { throw 'Overlay compilation failed.' }

# Build the generator, then insert the bootstrap call into ReLogic.
& $compiler /nologo ('/out:' + (Join-Path $PSScriptRoot 'lib\GenerateProxy.exe')) (
    '/r:' + (Join-Path $PSScriptRoot 'lib\Mono.Cecil.dll')) (Join-Path $PSScriptRoot 'src\GenerateProxy.cs')
if ($LASTEXITCODE -ne 0) { throw 'Proxy generator compilation failed.' }

& (Join-Path $PSScriptRoot 'lib\GenerateProxy.exe') $game (Join-Path $bin 'ReLogicHijack.dll') $bin
if ($LASTEXITCODE -ne 0) { throw 'Proxy generation failed.' }

$files = @{}
foreach ($name in @('ReLogic.dll', 'ReLogicHijack.dll')) {
    $files[$name] = (Get-FileHash -LiteralPath (Join-Path $bin $name) -Algorithm SHA256).Hash
}
if ((Get-FileHash -LiteralPath $game -Algorithm SHA256).Hash -ne $before) { throw 'Game executable changed during build.' }
@{
    GameVersion = $version
    GameExeSHA256 = $before
    Files = $files
    FlightLab = [bool]$FlightLab
    FullbrightLab = [bool]$FullbrightLab
    BuiltUtc = [DateTime]::UtcNow.ToString('o')
} |
    ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $bin '..\build-manifest.json') -Encoding UTF8

Write-Output "Build complete for Terraria $version. Original EXE SHA256: $before"
