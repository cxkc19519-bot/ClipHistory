[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$iconScript = Join-Path $PSScriptRoot 'Create-WindowsIcon.ps1'
$publishScript = Join-Path $PSScriptRoot 'Publish-Windows.ps1'
$installerScript = Join-Path $projectRoot 'installer\ClipHistory.iss'
$compilerCandidates = @(
    'C:\Program Files (x86)\Inno Setup 6\ISCC.exe',
    'C:\Program Files\Inno Setup 6\ISCC.exe',
    (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe')
)

& $iconScript

& $publishScript

$compiler = $compilerCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if ($null -eq $compiler) {
    throw 'Inno Setup 6 compiler was not found.'
}

& $compiler $installerScript
if ($LASTEXITCODE -ne 0) { throw "Installer compilation failed with exit code $LASTEXITCODE." }

$installer = Join-Path $projectRoot 'artifacts\installer\ClipHistory-Setup-win-x64.exe'
if (-not (Test-Path -LiteralPath $installer)) {
    throw "Installer was not found: $installer"
}

Write-Output $installer
