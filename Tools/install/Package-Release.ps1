<#
.SYNOPSIS
    Packages the Windows build into a ZIP for the jam page (nothing is uploaded).

.DESCRIPTION
    Copies the build (minus Unity's "BackUpThisFolder_ButDontShipItWithYourGame" and Burst debug folders) into
    a TheTwelfthStamp_v<version>_Windows folder, zips it, removes the temporary copy and prints the ZIP's size,
    entry count and SHA-256. The ZIP contains the game, README.txt and the shortcut installer.
#>
param(
    [string]$BuildDir = "",
    [string]$OutDir = "",
    [string]$Version = "0.9.0"
)

$ErrorActionPreference = 'Stop'
# Builds and packages live next to the repo (C:\Dev\12th-Stamp -> C:\Dev\12th-Stamp-builds), not under AppData: a
# packaged app such as the Claude desktop app redirects AppData writes into its own private storage, out of Explorer's sight.
$repo = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..')).Path
$buildsRoot = Join-Path (Split-Path -Parent $repo) ((Split-Path -Leaf $repo) + '-builds')
if (-not $BuildDir) { $BuildDir = Join-Path $buildsRoot 'Builds\Windows' }
if (-not $OutDir) { $OutDir = Join-Path $buildsRoot 'Release' }
if (-not (Test-Path -LiteralPath (Join-Path $BuildDir 'TheTwelfthStamp.exe'))) { throw "No build at '$BuildDir'." }

$name = "TheTwelfthStamp_v${Version}_Windows"
New-Item -ItemType Directory -Force $OutDir | Out-Null
$stage = Join-Path $OutDir $name
if (Test-Path -LiteralPath $stage) { Remove-Item -LiteralPath $stage -Recurse -Force }
New-Item -ItemType Directory -Force $stage | Out-Null

Get-ChildItem -LiteralPath $BuildDir |
    Where-Object { $_.Name -notlike '*_ButDontShipItWithYourGame' -and $_.Name -notlike '*_DoNotShip' } |
    ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination $stage -Recurse }

$zip = Join-Path $OutDir "$name.zip"
if (Test-Path -LiteralPath $zip) { Remove-Item -LiteralPath $zip -Force }
Compress-Archive -Path $stage -DestinationPath $zip -CompressionLevel Optimal
Remove-Item -LiteralPath $stage -Recurse -Force

Add-Type -AssemblyName System.IO.Compression.FileSystem
$z = [System.IO.Compression.ZipFile]::OpenRead($zip)
try {
    $entries = $z.Entries.Count
    $exe = @($z.Entries | Where-Object { $_.FullName -like '*TheTwelfthStamp.exe' }).Count -gt 0
    $readme = @($z.Entries | Where-Object { $_.FullName -like '*README.txt' }).Count -gt 0
    $installer = @($z.Entries | Where-Object { $_.FullName -like '*Install Shortcuts.cmd' }).Count -gt 0
    $leak = @($z.Entries | Where-Object { $_.FullName -like '*DontShip*' -or $_.FullName -like '*DoNotShip*' }).Count
} finally { $z.Dispose() }

$hash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash
$mb = [math]::Round((Get-Item -LiteralPath $zip).Length / 1MB, 1)
Write-Output "ZIP: $zip"
Write-Output "Size: $mb MB | entries: $entries | exe: $exe | README: $readme | installer: $installer | do-not-ship entries: $leak"
Write-Output "SHA-256: $hash"
