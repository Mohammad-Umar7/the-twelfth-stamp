@echo off
rem Creates "The Twelfth Stamp - Quest 3" and "The Twelfth Stamp - Desktop" shortcuts on your Desktop,
rem pointing at TheTwelfthStamp.exe in this folder. Bypass applies to this one PowerShell process only.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Install-Shortcuts.ps1" %*
echo.
pause
