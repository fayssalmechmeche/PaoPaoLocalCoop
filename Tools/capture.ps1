# Captures the screen with the game windows in the foreground. Usage: capture.ps1 <file.png> [width]
param([string]$Out, [int]$Width = 1600)
Add-Type -AssemblyName System.Drawing
Add-Type -Name D -Namespace U -MemberDefinition '[DllImport("user32.dll")] public static extern bool SetProcessDPIAware(); [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr a, int x, int y, int w, int cy, uint f);'
[U.D]::SetProcessDPIAware() | Out-Null
$ws = Get-Process PaoPao -ErrorAction SilentlyContinue | % { $_.MainWindowHandle }
foreach ($h in $ws) { [U.D]::SetWindowPos($h, [IntPtr](-1), 0, 0, 0, 0, 0x13) | Out-Null }
Start-Sleep -Milliseconds 400
$b = New-Object Drawing.Bitmap 1920, 1080
[Drawing.Graphics]::FromImage($b).CopyFromScreen(0, 0, 0, 0, $b.Size)
foreach ($h in $ws) { [U.D]::SetWindowPos($h, [IntPtr](-2), 0, 0, 0, 0, 0x13) | Out-Null }
$s = New-Object Drawing.Bitmap $Width, ([int]($Width * 1080 / 1920))
[Drawing.Graphics]::FromImage($s).DrawImage($b, 0, 0, $s.Width, $s.Height)
$s.Save($Out)
