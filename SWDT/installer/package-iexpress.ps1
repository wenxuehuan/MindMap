$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$publishDir = Join-Path $repoRoot "artifacts\publish\SWDT-win-x64"
$packageDir = Join-Path $repoRoot "artifacts\installer\package"
$outputDir = Join-Path $repoRoot "artifacts\installer"
$setupExe = Join-Path $outputDir "SWDT-Setup-win-x64.exe"
$sedPath = Join-Path $outputDir "SWDT-Setup.sed"

$publishedExe = Join-Path $publishDir "SWDT.exe"
$webViewBootstrapper = Join-Path $outputDir "MicrosoftEdgeWebView2Setup.exe"
if (-not (Test-Path -LiteralPath $publishedExe)) {
    throw "Published executable not found. Run dotnet publish first: $publishedExe"
}

New-Item -ItemType Directory -Force -Path $packageDir | Out-Null
if (-not (Test-Path -LiteralPath $webViewBootstrapper)) {
    Invoke-WebRequest `
        -Uri "https://go.microsoft.com/fwlink/p/?LinkId=2124703" `
        -OutFile $webViewBootstrapper `
        -UseBasicParsing
}

Copy-Item -LiteralPath $publishedExe -Destination (Join-Path $packageDir "SWDT.exe") -Force
Copy-Item -LiteralPath $webViewBootstrapper -Destination (Join-Path $packageDir "MicrosoftEdgeWebView2Setup.exe") -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot "install.cmd") -Destination (Join-Path $packageDir "install.cmd") -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot "install.ps1") -Destination (Join-Path $packageDir "install.ps1") -Force

$packageDirWithSlash = $packageDir.TrimEnd("\") + "\"
$sed = @"
[Version]
Class=IEXPRESS
SEDVersion=3

[Options]
PackagePurpose=InstallApp
ShowInstallProgramWindow=0
HideExtractAnimation=1
UseLongFileName=1
InsideCompressed=0
CAB_FixedSize=0
CAB_ResvCodeSigning=0
RebootMode=N
InstallPrompt=
DisplayLicense=
FinishMessage=SWDT has been installed.
TargetName=$setupExe
FriendlyName=SWDT Setup
AppLaunched=install.cmd
PostInstallCmd=<None>
AdminQuietInstCmd=
UserQuietInstCmd=
SourceFiles=SourceFiles

[Strings]

[SourceFiles]
SourceFiles0=$packageDirWithSlash

[SourceFiles0]
SWDT.exe=
MicrosoftEdgeWebView2Setup.exe=
install.cmd=
install.ps1=
"@

Set-Content -LiteralPath $sedPath -Value $sed -Encoding ASCII

$iexpress = Join-Path $env:SystemRoot "System32\iexpress.exe"
if (-not (Test-Path -LiteralPath $iexpress)) {
    throw "IExpress was not found: $iexpress"
}

$process = Start-Process -FilePath $iexpress -ArgumentList @("/N", "/Q", $sedPath) -PassThru -Wait
if ($process.ExitCode -ne 0 -and -not (Test-Path -LiteralPath $setupExe)) {
    throw "IExpress failed with exit code $($process.ExitCode)"
}

$deadline = (Get-Date).AddMinutes(5)
while (-not (Test-Path -LiteralPath $setupExe)) {
    if ((Get-Date) -gt $deadline) {
        throw "Timed out waiting for installer: $setupExe"
    }

    Start-Sleep -Seconds 1
}

if (-not (Test-Path -LiteralPath $setupExe)) {
    throw "Installer was not created: $setupExe"
}

Get-Item -LiteralPath $setupExe
