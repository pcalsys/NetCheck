[CmdletBinding()]
param(
    [string]$Version = '1.2.0'
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

$artifactsDirectory = [IO.Path]::GetFullPath((Join-Path $repositoryRoot 'artifacts'))
$publishDirectory = [IO.Path]::GetFullPath((Join-Path $artifactsDirectory 'publish\win-x64'))
$expectedPrefix = $repositoryRoot.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
if (-not $publishDirectory.StartsWith($expectedPrefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'The resolved publish directory is outside the repository.'
}

if (Test-Path -LiteralPath $publishDirectory) {
    Remove-Item -LiteralPath $publishDirectory -Recurse -Force
}
New-Item -ItemType Directory -Path $publishDirectory -Force | Out-Null

$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$project = Join-Path $repositoryRoot 'src\NetCheck.App\NetCheck.App.csproj'
& $dotnet publish $project `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    --output $publishDirectory `
    -p:Version=$Version `
    --nologo
if ($LASTEXITCODE -ne 0) { throw "Publish failed with exit code $LASTEXITCODE." }

$archive = Join-Path $artifactsDirectory "NetCheck-$Version-win-x64.zip"
if (Test-Path -LiteralPath $archive) {
    Remove-Item -LiteralPath $archive -Force
}
Compress-Archive -Path (Join-Path $publishDirectory '*') -DestinationPath $archive -CompressionLevel Optimal

Write-Host "Published NetCheck to $publishDirectory" -ForegroundColor Green
Write-Host "Created $archive" -ForegroundColor Green
