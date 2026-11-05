# Builds the package to share: Package\output\PaoPaoLocalCoop_v<version>.zip
# Prerequisites: the launcher is published (XInputRelay\publish), the mod is built (CoopLocal\bin\Release),
# and bepinex_base contains BepInEx 6 IL2CPP (winhttp.dll, doorstop_config.ini, .doorstop_version, dotnet, BepInEx\core, BepInEx\unity-libs).
param([string]$Base = "$PSScriptRoot\bepinex_base")

$ErrorActionPreference = 'Stop'
$ici = $PSScriptRoot
$racineMod = Split-Path $ici
$version = ([regex]::Match((Get-Content "$racineMod\CoopLocal\Plugin.cs" -Raw), 'BepInPlugin\("[^"]+", "[^"]+", "([^"]+)"')).Groups[1].Value
$nom = "PaoPaoLocalCoop_v$version"
$dest = "$ici\output\$nom"
$contenu = "$dest\mod_files"

if (Test-Path $dest) { Remove-Item $dest -Recurse -Force }
New-Item -ItemType Directory -Force "$contenu\BepInEx\plugins" | Out-Null

Copy-Item "$racineMod\XInputRelay\publish\PaoPaoLocalCoop.exe" $dest
Copy-Item "$ici\docs\*" $dest
foreach ($f in 'winhttp.dll', 'doorstop_config.ini', '.doorstop_version') { Copy-Item "$Base\$f" $contenu }
Copy-Item "$Base\dotnet" "$contenu\dotnet" -Recurse
Copy-Item "$Base\BepInEx\core" "$contenu\BepInEx\core" -Recurse
Copy-Item "$Base\BepInEx\unity-libs" "$contenu\BepInEx\unity-libs" -Recurse
New-Item -ItemType Directory -Force "$contenu\BepInEx\config" | Out-Null
Copy-Item "$ici\config\BepInEx.cfg" "$contenu\BepInEx\config"  # BepInEx console disabled (otherwise a black window hides the game)
Copy-Item "$racineMod\CoopLocal\bin\Release\net6.0\CoopLocal.dll" "$contenu\BepInEx\plugins"
Set-Content "$contenu\version.txt" $version -Encoding ascii

$zip = "$ici\output\$nom.zip"
if (Test-Path $zip) { Remove-Item $zip }
Compress-Archive -Path $dest -DestinationPath $zip
"{0} : {1:N1} Mo ({2} fichiers)" -f $zip, ((Get-Item $zip).Length / 1MB), (Get-ChildItem $dest -Recurse -File).Count
