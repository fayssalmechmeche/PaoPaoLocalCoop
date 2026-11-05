# Simule des appuis de manette (le relais doit etre arrete). Etapes : masque:lx:ly:rx:ry:ms[:emplacement]
# Boutons : 1 haut, 2 bas, 4 gauche, 8 droite, 1000 A, 2000 B, 4000 X, 8000 Y, 100 LB, 200 RB, 10 Start
Get-Process PaoPaoLocalCoop -ErrorAction SilentlyContinue | ? { $_.Id -ne $PID } | Stop-Process -Force
Start-Process "$env:USERPROFILE\Documents\PaoPao_Mod\XInputRelay\publish\PaoPaoLocalCoop.exe" -ArgumentList (@('--hold') + $args) -Wait
