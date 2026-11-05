# PaoPaoLocalCoop

Unofficial mod for **PAO PAO**: local split-screen co-op on one PC with two controllers (two game windows side by side).

| Folder | Role |
|---|---|
| `CoopLocal/` | BepInEx 6 (IL2CPP) plugin loaded in the game: local host/join menu, input assignment, window layout |
| `XInputRelay/` | Launcher `PaoPaoLocalCoop.exe`: installs the mod in the game folder, starts the two game windows, relays XInput controllers |
| `SigTool/` | Dev helper that lists type and method signatures from game assemblies (not shipped) |
| `Package/` | Packaging script (`build.ps1`), BepInEx config and end-user docs |
| `Tools/` | Dev scripts (not shipped) |

`CoopLocal/_legacy/` holds old exploration code. It is not compiled.

Player documentation: [`Package/docs/README.txt`](Package/docs/README.txt).

The in-game menu and the launcher dialogs are shown in French or English, depending on the game and Windows language.

## Build

Requirements: Windows 10/11 x64, .NET 8 SDK (plus the .NET 6 targeting pack), PAO PAO from Steam with BepInEx 6 IL2CPP installed once so that `BepInEx/interop/*.dll` exists.

1. Set `GamePath` in `CoopLocal/CoopLocal.csproj` to your PAO PAO folder.
2. `dotnet build CoopLocal -c Release` -> `CoopLocal/bin/Release/net6.0/CoopLocal.dll`
3. `dotnet publish XInputRelay -c Release -o XInputRelay/publish`
4. Put an unmodified BepInEx 6 IL2CPP release in `Package/bepinex_base/` (see `build.ps1` header), then run `Package/build.ps1` to produce the zip in `Package/output/`.

## What the launcher and the mod change

- Game folder: adds BepInEx, the plugin DLL, `steam_appid.txt` (needed to start the game outside Steam) and an install state file. No game file is modified. `PaoPaoLocalCoop.exe --uninstall` removes them.
- Windows: reads the Steam install path from the registry and hides the taskbar while playing (it is restored when the game closes). The window that is not in the foreground gets a higher process priority.
- Network: the mod contains no network code of its own. The two windows connect to each other through the game's own multiplayer stack on the same PC.
- BepInEx downloads: `UnityBaseLibrariesSource` is left empty in `Package/config/BepInEx.cfg` and the Unity base libraries are shipped extracted, so BepInEx downloads nothing and the package contains no nested archive.

## Third-party

BepInEx (LGPL-2.1) and others: see [`Package/docs/LICENSES.txt`](Package/docs/LICENSES.txt).
