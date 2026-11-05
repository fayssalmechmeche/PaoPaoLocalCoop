# Lance le coop en mode menu avec le mod en cours de developpement (sans passer par le paquet).
$g = 'C:\Program Files (x86)\Steam\steamapps\common\PaoPao'
$logs = "$env:LOCALAPPDATA\PaoPaoLocalCoop\logs"; New-Item -ItemType Directory -Force $logs | Out-Null
Start-Process "$env:USERPROFILE\Documents\PaoPao_Mod\XInputRelay\publish\PaoPaoLocalCoop.exe" -ArgumentList '--relay','--game','PaoPao'
Start-Process "$g\PaoPao.exe" -WorkingDirectory $g -ArgumentList '--coop-local','--coop-fenetre','1','-popupwindow','--coop-workarea','-logFile',"$logs\coop_joueur1.log"
Start-Sleep 5
Start-Process "$g\PaoPao.exe" -WorkingDirectory $g -ArgumentList '--coop-local','--coop-fenetre','2','-popupwindow','--coop-workarea','-logFile',"$logs\coop_joueur2.log"
