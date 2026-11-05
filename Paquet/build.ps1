# Fabrique le paquet a partager : Paquet\sortie\PaoPaoLocalCoop_v<version>.zip
# Prerequis : le lanceur est publie (XInputRelay\publish), le mod est compile (CoopLocal\bin\Release),
# et bepinex_base contient BepInEx 6 IL2CPP (winhttp.dll, doorstop_config.ini, .doorstop_version, dotnet, BepInEx\core, BepInEx\unity-libs).
param([string]$Base = "$PSScriptRoot\bepinex_base")

$ErrorActionPreference = 'Stop'
$ici = $PSScriptRoot
$racineMod = Split-Path $ici
$version = ([regex]::Match((Get-Content "$racineMod\CoopLocal\Plugin.cs" -Raw), 'BepInPlugin\("[^"]+", "[^"]+", "([^"]+)"')).Groups[1].Value
$nom = "PaoPaoLocalCoop_v$version"
$dest = "$ici\sortie\$nom"
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
Copy-Item "$ici\config\BepInEx.cfg" "$contenu\BepInEx\config"  # console BepInEx desactivee (sinon une fenetre noire cache le jeu)
Copy-Item "$racineMod\CoopLocal\bin\Release\net6.0\CoopLocal.dll" "$contenu\BepInEx\plugins"
Set-Content "$contenu\version.txt" $version -Encoding ascii

$zip = "$ici\sortie\$nom.zip"
if (Test-Path $zip) { Remove-Item $zip }
Compress-Archive -Path $dest -DestinationPath $zip
"{0} : {1:N1} Mo ({2} fichiers)" -f $zip, ((Get-Item $zip).Length / 1MB), (Get-ChildItem $dest -Recurse -File).Count
