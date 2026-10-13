# ReLogicHijack

A Terraria dependency-hijacking proof of concept, with an overlay, noclip flight, and fullbright.

Built and tested against **Windows Steam Terraria 1.4.5.8**, running on **.NET Framework 4 / XNA 4, x86**.

ReLogicHijack adds DLLs beside the game executable. It leaves the original `Terraria.exe` unchanged on disk and modifies the running client's behaviour. It uses the embedded-dependency technique described in [Chapter 7 of Bepis's article](https://bepis.io/blog/binding-redirects/#chapter-7-optimizations-and-caveats), without a binding redirect or separate launcher.

See [Technical details](docs/TECHNICAL.md) for the loading sequence, runtime modifications, network behaviour, and validation limits.

## Features and controls

Both modes start **off** when the game launches and when entering a world. The overlay displays the game version, connection state, FPS, and mode status.

| Key | Action |
| --- | --- |
| **F4** | Toggle fullbright |
| **F6** | Toggle noclip flight |
| **WASD / arrow keys** | Move while flying |
| **Shift** | Increase flight speed from 15 to 45 tiles per second |

- **Flight:** hover or move through terrain. Move into open space before disabling it; disabling inside solid terrain is refused. Movement is limited by world boundaries and loaded world sections. Flight alone does not grant damage immunity.
- **Fullbright:** illuminate the view in Color, White, Retro, and Trippy lighting modes. Disabling it restores the original lighting engine.

Mode keys are ignored while unfocused, typing, or using the game's input-blocking interfaces. Flight movement also pauses while the inventory is open. Existing game bindings still receive these keys.

## Build and install

You need a local Terraria installation, Windows' .NET Framework C# compiler, and the XNA 4 assemblies installed with the game. Mono.Cecil is included in `lib/` as a **build-time tool** for generating the hijacked dependency; building requires no package download. The demo adds no third-party runtime library and uses no method-detour framework.

From the repository directory in PowerShell, with Terraria closed:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Build.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Install.ps1
```

Launch Terraria normally through Steam.

The default game directory is:

```text
C:\Program Files (x86)\Steam\steamapps\common\Terraria
```

For another installation, pass the same directory to both scripts:

```powershell
.\Build.ps1 -GameDirectory 'D:\SteamLibrary\steamapps\common\Terraria'
.\Install.ps1 -GameDirectory 'D:\SteamLibrary\steamapps\common\Terraria'
```

The installer adds `ReLogic.dll`, `ReLogicHijack.dll`, and `ReLogicHijack.install.json`. It checks the executable against the build manifest and refuses to overwrite existing DLLs. Write access to the game directory is required.

## Remove or upgrade

Close Terraria, then run:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Uninstall.ps1
```

For a custom installation, add `-GameDirectory` as above. The uninstaller verifies the added DLLs against their receipt before removing them. It also recognizes the earlier `TerrariaOverlayPoC` DLL and receipt names and removes obsolete runtime dependencies owned by those receipts.

To upgrade, uninstall the previous build, build the new version, and install it.

**Before a Terraria update, uninstall ReLogicHijack.** An outdated external `ReLogic.dll` can shadow a newer embedded dependency and prevent startup. Rebuild and retest against the updated game before reinstalling. Steam verification may retain these extra DLLs.

## Compatibility and diagnostics

The loader preserves Terraria's version, handshake, content IDs, and packet formats. Noclip flight was tested against a local vanilla server. Fullbright was tested in singleplayer; its multiplayer behaviour and Steam friend-lobby transport remain unverified. Server software and rules determine which client-reported actions are accepted.

Diagnostics are written to:

```text
%TEMP%\ReLogicHijack-<Terraria process ID>.log
```

The log records startup, mode toggles, flight coordinates, and game-state transitions. Drawing errors disable the overlay for the session and are logged. See [validation.txt](validation.txt) and the [technical validation notes](docs/TECHNICAL.md#validation) for the test scope.

## Repository layout

| Path | Contents |
| --- | --- |
| [`src/`](src/) | Bootstrap, dependency generator, overlay, and mode implementations |
| [`tests/`](tests/) | Opt-in integration probes and server observer source |
| [`docs/TECHNICAL.md`](docs/TECHNICAL.md) | Architecture and implementation details |
| [`lib/`](lib/) | Mono.Cecil 0.11.6 and its MIT license; build-time use only |
| `bin/` | Generated local release DLLs; ignored by Git |
| `work/` | Build intermediates and local test files; ignored by Git |
| `outputs/` | Local validation evidence; ignored by Git |

Builds also generate a local `build-manifest.json`, which is ignored by Git.

## Source and licenses

The generated `ReLogic.dll` contains code extracted from your game installation. Keep generated game dependencies and game files out of a public source repository; regenerate them locally.

Mono.Cecil retains its [MIT license](lib/Mono.Cecil.LICENSE.txt). A license for ReLogicHijack's own source has not yet been selected.
