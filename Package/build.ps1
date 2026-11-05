# Builds the package to share: Package\output\PaoPaoLocalCoop_v<version>.zip
# Prerequisites: the launcher is published (XInputRelay\publish), the mod is built (CoopLocal\bin\Release),
# and bepinex_base contains BepInEx 6 IL2CPP (winhttp.dll, doorstop_config.ini, .doorstop_version, dotnet, BepInEx\core, BepInEx\unity-libs).
param([string]$Base = "$PSScriptRoot\bepinex_base")

$ErrorActionPreference = 'Stop'
$here = $PSScriptRoot
$modRoot = Split-Path $here
$version = ([regex]::Match((Get-Content "$modRoot\CoopLocal\Plugin.cs" -Raw), 'BepInPlugin\("[^"]+", "[^"]+", "([^"]+)"')).Groups[1].Value
$name = "PaoPaoLocalCoop_v$version"
$dest = "$here\output\$name"
$content = "$dest\mod_files"

if (Test-Path $dest) { Remove-Item $dest -Recurse -Force }
New-Item -ItemType Directory -Force "$content\BepInEx\plugins" | Out-Null

Copy-Item "$modRoot\XInputRelay\publish\PaoPaoLocalCoop.exe" $dest
Copy-Item "$here\docs\*" $dest
foreach ($f in 'winhttp.dll', 'doorstop_config.ini', '.doorstop_version') { Copy-Item "$Base\$f" $content }
Copy-Item "$Base\dotnet" "$content\dotnet" -Recurse
Copy-Item "$Base\BepInEx\core" "$content\BepInEx\core" -Recurse
Copy-Item "$Base\BepInEx\unity-libs" "$content\BepInEx\unity-libs" -Recurse
New-Item -ItemType Directory -Force "$content\BepInEx\config" | Out-Null
Copy-Item "$here\config\BepInEx.cfg" "$content\BepInEx\config"  # BepInEx console disabled (otherwise a black window hides the game)
Copy-Item "$modRoot\CoopLocal\bin\Release\net6.0\CoopLocal.dll" "$content\BepInEx\plugins"
Set-Content "$content\version.txt" $version -Encoding ascii

$zip = "$here\output\$name.zip"
if (Test-Path $zip) { Remove-Item $zip }
Compress-Archive -Path $dest -DestinationPath $zip
"{0}: {1:N1} MB ({2} files)" -f $zip, ((Get-Item $zip).Length / 1MB), (Get-ChildItem $dest -Recurse -File).Count
