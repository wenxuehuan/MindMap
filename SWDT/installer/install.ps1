$ErrorActionPreference = "Stop"

$appName = "SWDT"
$installRoot = Join-Path $env:LOCALAPPDATA "Programs\SWDT"
$sourceExe = Join-Path $PSScriptRoot "SWDT.exe"
$targetExe = Join-Path $installRoot "SWDT.exe"

if (-not (Test-Path -LiteralPath $sourceExe)) {
    throw "Missing payload: $sourceExe"
}

New-Item -ItemType Directory -Force -Path $installRoot | Out-Null
Copy-Item -LiteralPath $sourceExe -Destination $targetExe -Force

$uninstallScript = Join-Path $installRoot "uninstall.ps1"
$uninstallContent = @'
$ErrorActionPreference = "SilentlyContinue"

$appName = "SWDT"
$installRoot = Join-Path $env:LOCALAPPDATA "Programs\SWDT"
$programsFolder = [Environment]::GetFolderPath("Programs")
$desktopFolder = [Environment]::GetFolderPath("DesktopDirectory")
$startMenuFolder = Join-Path $programsFolder $appName

Remove-Item -LiteralPath (Join-Path $desktopFolder "$appName.lnk") -Force
Remove-Item -LiteralPath $startMenuFolder -Recurse -Force
Remove-Item -LiteralPath $installRoot -Recurse -Force
'@
Set-Content -LiteralPath $uninstallScript -Value $uninstallContent -Encoding UTF8

$shell = New-Object -ComObject WScript.Shell

function New-Shortcut {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$TargetPath,
        [string]$Arguments = "",
        [string]$WorkingDirectory = ""
    )

    $shortcut = $shell.CreateShortcut($Path)
    $shortcut.TargetPath = $TargetPath
    $shortcut.Arguments = $Arguments
    $shortcut.WorkingDirectory = $WorkingDirectory
    $shortcut.IconLocation = "$targetExe,0"
    $shortcut.Save()
}

$programsFolder = [Environment]::GetFolderPath("Programs")
$desktopFolder = [Environment]::GetFolderPath("DesktopDirectory")
$startMenuFolder = Join-Path $programsFolder $appName
New-Item -ItemType Directory -Force -Path $startMenuFolder | Out-Null

New-Shortcut `
    -Path (Join-Path $startMenuFolder "$appName.lnk") `
    -TargetPath $targetExe `
    -WorkingDirectory $installRoot

New-Shortcut `
    -Path (Join-Path $desktopFolder "$appName.lnk") `
    -TargetPath $targetExe `
    -WorkingDirectory $installRoot

New-Shortcut `
    -Path (Join-Path $startMenuFolder "Uninstall $appName.lnk") `
    -TargetPath "powershell.exe" `
    -Arguments "-NoProfile -ExecutionPolicy Bypass -File `"$uninstallScript`"" `
    -WorkingDirectory $installRoot

Write-Host "$appName installed to $installRoot"
