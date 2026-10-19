<#
.SYNOPSIS
    Creates (or removes) the two desktop shortcuts for The Twelfth Stamp.

.DESCRIPTION
    "The Twelfth Stamp - Quest 3.lnk"  ->  TheTwelfthStamp.exe --mode=vr       (Quest 3 as PC VR via Meta Horizon Link / OpenXR)
    "The Twelfth Stamp - Desktop.lnk"  ->  TheTwelfthStamp.exe --mode=desktop  (keyboard and mouse)

    Portable: by default it uses the TheTwelfthStamp.exe that sits next to this script, so it works wherever the
    game folder was unzipped. The Desktop folder is resolved through the Windows shell, so a OneDrive-redirected
    Desktop works. It writes two .lnk files and nothing else: no registry keys, no admin rights, and it never
    touches the active OpenXR runtime.

.PARAMETER GameDir
    Folder that contains TheTwelfthStamp.exe. Defaults to this script's folder.

.PARAMETER Remove
    Delete the two shortcuts instead of creating them.

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File .\Install-Shortcuts.ps1
#>
param(
    [string]$GameDir = $PSScriptRoot,
    [switch]$Remove
)

$ErrorActionPreference = 'Stop'

$shell = New-Object -ComObject WScript.Shell
$desktop = $shell.SpecialFolders.Item('Desktop')
if ([string]::IsNullOrWhiteSpace($desktop) -or -not (Test-Path -LiteralPath $desktop)) {
    $desktop = [Environment]::GetFolderPath('Desktop')
}
if ([string]::IsNullOrWhiteSpace($desktop) -or -not (Test-Path -LiteralPath $desktop)) {
    throw "Could not find your Desktop folder."
}

$shortcuts = @(
    @{ Name = 'The Twelfth Stamp - Quest 3.lnk'; Args = '--mode=vr';      Desc = 'The Twelfth Stamp: Quest 3 as PC VR (Meta Horizon Link / OpenXR)' },
    @{ Name = 'The Twelfth Stamp - Desktop.lnk'; Args = '--mode=desktop'; Desc = 'The Twelfth Stamp: keyboard and mouse' }
)

if ($Remove) {
    foreach ($s in $shortcuts) {
        $path = Join-Path $desktop $s.Name
        if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path; Write-Output "Removed: $path" }
        else { Write-Output "Not present: $path" }
    }
    return
}

$exe = Join-Path $GameDir 'TheTwelfthStamp.exe'
if (-not (Test-Path -LiteralPath $exe)) {
    throw "TheTwelfthStamp.exe was not found in '$GameDir'. Run this script from the game folder, or pass -GameDir <folder>."
}
$exe = (Resolve-Path -LiteralPath $exe).Path
$workDir = Split-Path -Parent $exe
$ico = Join-Path $workDir 'TheTwelfthStamp.ico'
$iconLocation = if (Test-Path -LiteralPath $ico) { "$ico,0" } else { "$exe,0" }

foreach ($s in $shortcuts) {
    $path = Join-Path $desktop $s.Name
    $lnk = $shell.CreateShortcut($path)
    $lnk.TargetPath = $exe
    $lnk.Arguments = $s.Args
    $lnk.WorkingDirectory = $workDir
    $lnk.IconLocation = $iconLocation
    $lnk.Description = $s.Desc
    $lnk.WindowStyle = 1
    $lnk.Save()

    # Read it back: a shortcut only counts if it really points at the game with the right mode.
    $check = $shell.CreateShortcut($path)
    if ($check.TargetPath -ne $exe -or $check.Arguments -ne $s.Args -or $check.WorkingDirectory -ne $workDir) {
        throw "Shortcut check failed for '$path'."
    }
    Write-Output ("Created: {0}`n   -> `"{1}`" {2}`n   start in: {3}`n   icon: {4}" -f $path, $exe, $s.Args, $workDir, $iconLocation)
}
