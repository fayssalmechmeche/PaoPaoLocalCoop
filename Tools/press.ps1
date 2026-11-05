# Simulates controller presses (the relay must be stopped). Steps: mask:lx:ly:rx:ry:ms[:slot]
# Buttons: 1 up, 2 down, 4 left, 8 right, 1000 A, 2000 B, 4000 X, 8000 Y, 100 LB, 200 RB, 10 Start
Get-Process PaoPaoLocalCoop -ErrorAction SilentlyContinue | ? { $_.Id -ne $PID } | Stop-Process -Force
Start-Process "$PSScriptRoot\..\XInputRelay\publish\PaoPaoLocalCoop.exe" -ArgumentList (@('--hold') + $args) -Wait
