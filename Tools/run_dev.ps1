# Starts co-op in menu mode with the in-development mod (without going through the package).
$g = 'C:\Program Files (x86)\Steam\steamapps\common\PaoPao'
$logs = "$env:LOCALAPPDATA\PaoPaoLocalCoop\logs"; New-Item -ItemType Directory -Force $logs | Out-Null
Start-Process "$PSScriptRoot\..\XInputRelay\publish\PaoPaoLocalCoop.exe" -ArgumentList '--relay','--game','PaoPao'
Start-Process "$g\PaoPao.exe" -WorkingDirectory $g -ArgumentList '--coop-local','--coop-window','1','-popupwindow','--coop-workarea','-logFile',"$logs\coop_player1.log"
Start-Sleep 5
Start-Process "$g\PaoPao.exe" -WorkingDirectory $g -ArgumentList '--coop-local','--coop-window','2','-popupwindow','--coop-workarea','-logFile',"$logs\coop_player2.log"
