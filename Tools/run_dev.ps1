# Starts co-op in menu mode with the in-development mod (without going through the package).
$g = 'C:\Program Files (x86)\Steam\steamapps\common\PaoPao'
$logs = "$env:LOCALAPPDATA\PaoPaoLocalCoop\logs"; New-Item -ItemType Directory -Force $logs | Out-Null
Start-Process "$PSScriptRoot\..\XInputRelay\publish\PaoPaoLocalCoop.exe" -ArgumentList '--relay','--game','PaoPao'
Start-Process "$g\PaoPao.exe" -WorkingDirectory $g -ArgumentList '--coop-local','--coop-fenetre','1','-popupwindow','--coop-workarea','-logFile',"$logs\coop_joueur1.log"
Start-Sleep 5
Start-Process "$g\PaoPao.exe" -WorkingDirectory $g -ArgumentList '--coop-local','--coop-fenetre','2','-popupwindow','--coop-workarea','-logFile',"$logs\coop_joueur2.log"
