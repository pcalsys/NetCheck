[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',
    [switch]$SkipTests
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$localDotnet10 = Join-Path $repositoryRoot '.dotnet10\dotnet.exe'
$localDotnet = Join-Path $repositoryRoot '.dotnet\dotnet.exe'
$dotnet = if (Test-Path -LiteralPath $localDotnet10) {
    $localDotnet10
} elseif (Test-Path -LiteralPath $localDotnet) {
    $localDotnet
} else {
    $command = Get-Command dotnet -ErrorAction Stop
    $command.Source
}

$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
& $dotnet restore (Join-Path $repositoryRoot 'NetCheck.sln') -p:Configuration=$Configuration
if ($LASTEXITCODE -ne 0) { throw "Restore failed with exit code $LASTEXITCODE." }

& $dotnet build (Join-Path $repositoryRoot 'NetCheck.sln') --configuration $Configuration --no-restore --nologo
if ($LASTEXITCODE -ne 0) { throw "Build failed with exit code $LASTEXITCODE." }

if (-not $SkipTests) {
    & $dotnet test (Join-Path $repositoryRoot 'NetCheck.sln') --configuration $Configuration --no-build --nologo
    if ($LASTEXITCODE -ne 0) { throw "Tests failed with exit code $LASTEXITCODE." }
}

Write-Host "NetCheck $Configuration build completed successfully." -ForegroundColor Green
