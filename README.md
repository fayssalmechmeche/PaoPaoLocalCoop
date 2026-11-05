# PaoPaoLocalCoop

Unofficial mod for **PAO PAO**: local split-screen co-op on one PC with two controllers (two game windows side by side).

| Folder | Role |
|---|---|
| `CoopLocal/` | BepInEx 6 (IL2CPP) plugin loaded in the game: local host/join menu, input assignment, window layout |
| `XInputRelay/` | Launcher `PaoPaoLocalCoop.exe`: installs the mod in the game folder, starts the two game windows, relays XInput controllers |
| `SigTool/` | Dev helper used to locate game methods (not shipped) |
| `Paquet/` | Packaging script (`build.ps1`) and end-user docs |

Player documentation: [`Paquet/docs/README.txt`](Paquet/docs/README.txt) (FR/EN).

## Build

Requirements: Windows 10/11 x64, .NET SDK 6+, PAO PAO from Steam with BepInEx 6 IL2CPP installed once so that `BepInEx/interop/*.dll` exists.

1. Set `GamePath` in `CoopLocal/CoopLocal.csproj` to your PAO PAO folder.
2. `dotnet build CoopLocal -c Release` -> `CoopLocal/bin/Release/net6.0/CoopLocal.dll`
3. `dotnet publish XInputRelay -c Release -o XInputRelay/publish`
4. Put an unmodified BepInEx 6 IL2CPP release in `Paquet/bepinex_base/` (see `build.ps1` header), then run `Paquet/build.ps1` to produce the zip.

## What the mod does not do

No data collection and no network code of its own. It only adds BepInEx and the plugin DLL to the game folder. Everything is in this repository.

## Third-party

BepInEx (LGPL-2.1) and others: see [`Paquet/docs/LICENSES.txt`](Paquet/docs/LICENSES.txt).
