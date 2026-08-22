[CmdletBinding()]
param(
    [string]$Version,
    [string]$DotNetPath
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$project = Join-Path $repositoryRoot 'src\NetCheck.App\NetCheck.App.csproj'
. (Join-Path $PSScriptRoot 'dotnet.ps1')

$dotnet = if ([string]::IsNullOrWhiteSpace($DotNetPath)) {
    Get-NetCheckDotNet -RepositoryRoot $repositoryRoot
} else {
    [IO.Path]::GetFullPath($DotNetPath)
}
if (-not (Test-Path -LiteralPath $dotnet -PathType Leaf)) {
    throw "The specified dotnet executable does not exist: $dotnet"
}

if ([string]::IsNullOrWhiteSpace($Version)) {
    [xml]$projectFile = Get-Content -LiteralPath $project -Raw -Encoding UTF8
    $Version = [string]($projectFile.Project.PropertyGroup.Version | Select-Object -First 1)
}
if ($Version -notmatch '^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$') {
    throw "The release version is invalid: $Version"
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
New-Item -ItemType Directory -Path $artifactsDirectory -Force | Out-Null
New-Item -ItemType Directory -Path $publishDirectory -Force | Out-Null

$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_NOLOGO = '1'
$env:NUGET_XMLDOC_MODE = 'skip'

& $dotnet restore $project `
    --runtime win-x64 `
    -p:Configuration=Release `
    --nologo
if ($LASTEXITCODE -ne 0) { throw "Publish restore failed with exit code $LASTEXITCODE." }

& $dotnet publish $project `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    --no-restore `
    --output $publishDirectory `
    -p:Version=$Version `
    --nologo
if ($LASTEXITCODE -ne 0) { throw "Publish failed with exit code $LASTEXITCODE." }

$executable = Join-Path $publishDirectory 'NetCheck.exe'
if (-not (Test-Path -LiteralPath $executable -PathType Leaf)) {
    throw "Publish completed without the expected executable: $executable"
}

Copy-Item -LiteralPath (Join-Path $repositoryRoot 'LICENSE') `
    -Destination (Join-Path $publishDirectory 'LICENSE.txt') `
    -Force

$archive = Join-Path $artifactsDirectory "NetCheck-$Version-win-x64.zip"
if (Test-Path -LiteralPath $archive) {
    Remove-Item -LiteralPath $archive -Force
}
Compress-Archive -Path (Join-Path $publishDirectory '*') -DestinationPath $archive -CompressionLevel Optimal

$hash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash
$checksumFile = "$archive.sha256"
[IO.File]::WriteAllText(
    $checksumFile,
    "$hash  $([IO.Path]::GetFileName($archive))",
    [Text.Encoding]::ASCII)

Write-Host "Published NetCheck to $publishDirectory" -ForegroundColor Green
Write-Host "Created $archive" -ForegroundColor Green
Write-Host "SHA-256 $hash" -ForegroundColor Green
