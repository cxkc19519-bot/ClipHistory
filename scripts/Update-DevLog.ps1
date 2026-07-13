[CmdletBinding()]
param(
    [switch]$Start,
    [string]$Plan,
    [string]$Completed,
    [string]$Verification,
    [string]$Decision,
    [string]$Todo
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$logDirectory = Join-Path $projectRoot 'dev-logs'
$date = Get-Date -Format 'yyyy-MM-dd'
$logPath = Join-Path $logDirectory "$date.md"

if (-not (Test-Path -LiteralPath $logDirectory)) {
    New-Item -ItemType Directory -Path $logDirectory | Out-Null
}

if (-not (Test-Path -LiteralPath $logPath)) {
    $template = @"
# Development Log $date

## Plan

## Completed

## Verification and Tests

## Issues and Decisions

## Todo

"@
    Set-Content -LiteralPath $logPath -Value $template -Encoding utf8
}

function Add-LogEntry {
    param(
        [Parameter(Mandatory)] [string]$Heading,
        [Parameter(Mandatory)] [AllowEmptyString()] [string]$Value
    )

    if ([string]::IsNullOrWhiteSpace($Value)) {
        return
    }

    $timestamp = Get-Date -Format 'HH:mm'
    Add-Content -LiteralPath $logPath -Value "`n- [$timestamp] ${Heading}: $Value" -Encoding utf8
}

if ($Start -and [string]::IsNullOrWhiteSpace($Plan)) {
    Add-LogEntry -Heading 'Session' -Value 'Development session started'
}

Add-LogEntry -Heading 'Plan' -Value $Plan
Add-LogEntry -Heading 'Completed' -Value $Completed
Add-LogEntry -Heading 'Verification' -Value $Verification
Add-LogEntry -Heading 'Decision' -Value $Decision
Add-LogEntry -Heading 'Todo' -Value $Todo

Write-Output $logPath
