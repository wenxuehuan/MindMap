$ErrorActionPreference = "Stop"

$appName = "SWDT"
$installRoot = Join-Path $env:LOCALAPPDATA "Programs\SWDT"
$sourceExe = Join-Path $PSScriptRoot "SWDT.exe"
$targetExe = Join-Path $installRoot "SWDT.exe"
$webViewBootstrapper = Join-Path $PSScriptRoot "MicrosoftEdgeWebView2Setup.exe"

if (-not (Test-Path -LiteralPath $sourceExe)) {
    throw "Missing payload: $sourceExe"
}

function Test-WebView2Runtime {
    $clientId = "{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}"
    $paths = @(
        "HKLM:\SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\$clientId",
        "HKLM:\SOFTWARE\Microsoft\EdgeUpdate\Clients\$clientId",
        "HKCU:\Software\Microsoft\EdgeUpdate\Clients\$clientId"
    )

    foreach ($path in $paths) {
        $version = (Get-ItemProperty -LiteralPath $path -Name "pv" -ErrorAction SilentlyContinue).pv
        if ($version -and $version -ne "0.0.0.0") {
            return $true
        }
    }

    return $false
}

if (-not (Test-WebView2Runtime)) {
    if (-not (Test-Path -LiteralPath $webViewBootstrapper)) {
        throw "Microsoft Edge WebView2 Runtime is required, but its installer is missing."
    }

    $runtimeInstall = Start-Process `
        -FilePath $webViewBootstrapper `
        -ArgumentList @("/silent", "/install") `
        -WindowStyle Hidden `
        -PassThru `
        -Wait
    if ($runtimeInstall.ExitCode -ne 0 -and -not (Test-WebView2Runtime)) {
        throw "Microsoft Edge WebView2 Runtime installation failed with exit code $($runtimeInstall.ExitCode)."
    }
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
