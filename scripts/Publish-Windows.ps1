[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $projectRoot 'src\ClipHistory.App\ClipHistory.App.csproj'
$outputPath = Join-Path $projectRoot 'artifacts\win-x64'
$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue

if ($null -eq $dotnet) {
    $dotnetPath = Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet\dotnet.exe'
}
else {
    $dotnetPath = $dotnet.Source
}

if (-not (Test-Path -LiteralPath $dotnetPath)) {
    throw 'The .NET SDK specified by global.json was not found.'
}

$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'

& $dotnetPath publish $projectPath `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    --output $outputPath `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:DebugType=None `
    -p:DebugSymbols=false

if ($LASTEXITCODE -ne 0) {
    throw "Publish failed with exit code $LASTEXITCODE."
}

$executable = Join-Path $outputPath 'ClipHistory.App.exe'
if (-not (Test-Path -LiteralPath $executable)) {
    throw "Published executable was not found: $executable"
}

Write-Output $executable

