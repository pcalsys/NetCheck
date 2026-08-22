[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$SkipTests,
    [switch]$SkipPublish,
    [switch]$SkipDownloadsCopy,
    [switch]$CollectCoverage
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
. (Join-Path $PSScriptRoot 'dotnet.ps1')
$dotnet = Get-NetCheckDotNet -RepositoryRoot $repositoryRoot

$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_NOLOGO = '1'
$env:NUGET_XMLDOC_MODE = 'skip'

Write-Host "Using $(& $dotnet --version) from $dotnet" -ForegroundColor DarkGray
& $dotnet restore (Join-Path $repositoryRoot 'NetCheck.sln') -p:Configuration=$Configuration
if ($LASTEXITCODE -ne 0) { throw "Restore failed with exit code $LASTEXITCODE." }

& $dotnet build (Join-Path $repositoryRoot 'NetCheck.sln') --configuration $Configuration --no-restore --nologo
if ($LASTEXITCODE -ne 0) { throw "Build failed with exit code $LASTEXITCODE." }

if (-not $SkipTests) {
    $testArguments = @(
        'test',
        (Join-Path $repositoryRoot 'NetCheck.sln'),
        '--configuration', $Configuration,
        '--no-build',
        '--nologo'
    )
    if ($CollectCoverage) {
        $testArguments += '--collect:XPlat Code Coverage'
    }

    & $dotnet @testArguments
    if ($LASTEXITCODE -ne 0) { throw "Tests failed with exit code $LASTEXITCODE." }
}

if ($Configuration -eq 'Release' -and -not $SkipPublish) {
    $isContinuousIntegration =
        [string]::Equals($env:CI, 'true', [StringComparison]::OrdinalIgnoreCase)
    $publishArguments = @{
        DotNetPath = $dotnet
    }
    if (-not $SkipDownloadsCopy -and -not $isContinuousIntegration) {
        $publishArguments.CopyToDownloads = $true
    }

    & (Join-Path $PSScriptRoot 'publish.ps1') @publishArguments
}

Write-Host "NetCheck $Configuration build completed successfully." -ForegroundColor Green
