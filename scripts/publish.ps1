[CmdletBinding()]
param(
    [string]$Version,
    [string]$DotNetPath,
    [string]$SigningCertificatePath,
    [string]$SigningCertificatePassword,
    [switch]$CopyToDownloads
)

$ErrorActionPreference = 'Stop'

function Get-NetCheckDownloadsDirectory {
    [CmdletBinding()]
    param()

    $downloadsKnownFolderId = '{374DE290-123F-4565-9164-39C4925E467B}'
    $userShellFoldersPath =
        'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\User Shell Folders'
    $downloadsPath = $null

    try {
        $userShellFolders = Get-Item -LiteralPath $userShellFoldersPath -ErrorAction Stop
        $downloadsPath = [string]$userShellFolders.GetValue(
            $downloadsKnownFolderId,
            $null,
            [Microsoft.Win32.RegistryValueOptions]::DoNotExpandEnvironmentNames)
    } catch {
        # Fall back to the standard profile location when the known-folder entry is unavailable.
    }

    if ([string]::IsNullOrWhiteSpace($downloadsPath)) {
        $userProfile = [Environment]::GetFolderPath(
            [Environment+SpecialFolder]::UserProfile)
        if ([string]::IsNullOrWhiteSpace($userProfile)) {
            throw 'The Windows user profile directory could not be resolved.'
        }
        $downloadsPath = Join-Path $userProfile 'Downloads'
    } else {
        $downloadsPath = [Environment]::ExpandEnvironmentVariables($downloadsPath)
    }

    if (-not [IO.Path]::IsPathRooted($downloadsPath)) {
        throw "The Windows Downloads directory is not an absolute path: $downloadsPath"
    }

    return [IO.Path]::GetFullPath($downloadsPath)
}

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

$hasSigningCertificate = -not [string]::IsNullOrWhiteSpace($SigningCertificatePath)
$hasSigningPassword = -not [string]::IsNullOrWhiteSpace($SigningCertificatePassword)
if ($hasSigningCertificate -xor $hasSigningPassword) {
    throw 'Authenticode signing requires both SigningCertificatePath and SigningCertificatePassword.'
}
if ($hasSigningCertificate) {
    & (Join-Path $PSScriptRoot 'sign.ps1') `
        -FilePath $executable `
        -CertificatePath $SigningCertificatePath `
        -CertificatePassword $SigningCertificatePassword
} else {
    Write-Warning 'No Authenticode certificate was provided. The executable is intentionally unsigned.'
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

$downloadsExecutable = $null
if ($CopyToDownloads) {
    $downloadsDirectory = Get-NetCheckDownloadsDirectory
    [IO.Directory]::CreateDirectory($downloadsDirectory) | Out-Null

    $downloadsPackageDirectory = Join-Path $downloadsDirectory "NetCheck-$Version"
    try {
        [IO.Directory]::CreateDirectory($downloadsPackageDirectory) | Out-Null
        Copy-Item -LiteralPath $executable `
            -Destination (Join-Path $downloadsPackageDirectory 'NetCheck.exe') `
            -Force
        Copy-Item -LiteralPath (Join-Path $publishDirectory 'LICENSE.txt') `
            -Destination (Join-Path $downloadsPackageDirectory 'LICENSE.txt') `
            -Force
    } catch {
        $fallbackName = "NetCheck-$Version-$([DateTime]::Now.ToString('yyyyMMdd-HHmmss'))"
        $downloadsPackageDirectory = Join-Path $downloadsDirectory $fallbackName
        Write-Warning "The existing Downloads copy could not be updated. Using $downloadsPackageDirectory instead."
        [IO.Directory]::CreateDirectory($downloadsPackageDirectory) | Out-Null
        Copy-Item -LiteralPath $executable `
            -Destination (Join-Path $downloadsPackageDirectory 'NetCheck.exe') `
            -Force
        Copy-Item -LiteralPath (Join-Path $publishDirectory 'LICENSE.txt') `
            -Destination (Join-Path $downloadsPackageDirectory 'LICENSE.txt') `
            -Force
    }

    $downloadsExecutable = Join-Path $downloadsPackageDirectory 'NetCheck.exe'
    $publishedExecutableHash = (Get-FileHash -LiteralPath $executable -Algorithm SHA256).Hash
    $downloadsExecutableHash =
        (Get-FileHash -LiteralPath $downloadsExecutable -Algorithm SHA256).Hash
    if ($publishedExecutableHash -ne $downloadsExecutableHash) {
        throw "The Downloads copy does not match the published executable: $downloadsExecutable"
    }
}

Write-Host "Published NetCheck to $publishDirectory" -ForegroundColor Green
Write-Host "Created $archive" -ForegroundColor Green
Write-Host "SHA-256 $hash" -ForegroundColor Green
Write-Host
Write-Host '============================================================' -ForegroundColor Green
Write-Host ' NETCHECK IS READY / NETCHECK IST FERTIG' -ForegroundColor Green
Write-Host '============================================================' -ForegroundColor Green
if ($null -ne $downloadsExecutable) {
    Write-Host 'The start-ready application is in your Downloads folder:'
    Write-Host "  $downloadsExecutable" -ForegroundColor Cyan
    Write-Host 'Open that folder and double-click NetCheck.exe.'
} else {
    Write-Host 'The start-ready application is here:'
    Write-Host "  $executable" -ForegroundColor Cyan
}
Write-Host '============================================================' -ForegroundColor Green

if ($null -ne $downloadsExecutable) {
    try {
        $explorerArguments = '"{0}"' -f $downloadsPackageDirectory
        Start-Process -FilePath 'explorer.exe' -ArgumentList $explorerArguments
    } catch {
        Write-Warning "Downloads could not be opened automatically. Open this file manually: $downloadsExecutable"
    }
}
