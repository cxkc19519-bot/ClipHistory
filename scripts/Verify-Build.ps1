[CmdletBinding()]
param(
    [switch]$NoRestore
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$solutionPath = Join-Path $projectRoot 'ClipHistory.sln'
$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue

if ($null -eq $dotnet) {
    $userDotnet = Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet\dotnet.exe'
    if (-not (Test-Path -LiteralPath $userDotnet)) {
        throw '.NET SDK was not found. Install the SDK version specified by global.json.'
    }

    $dotnetPath = $userDotnet
}
else {
    $dotnetPath = $dotnet.Source
}

$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'

if (-not $NoRestore) {
    & $dotnetPath restore $solutionPath --locked-mode
    if ($LASTEXITCODE -ne 0) {
        throw "Restore failed with exit code $LASTEXITCODE."
    }
}

& $dotnetPath build $solutionPath --configuration Release --no-restore
if ($LASTEXITCODE -ne 0) {
    throw "Build failed with exit code $LASTEXITCODE."
}

& $dotnetPath test $solutionPath --configuration Release --no-build --no-restore
if ($LASTEXITCODE -ne 0) {
    throw "Tests failed with exit code $LASTEXITCODE."
}

Write-Output 'Build verification passed.'

