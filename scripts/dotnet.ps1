Set-StrictMode -Version Latest

function Get-NetCheckDotNet {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$RepositoryRoot
    )

    $resolvedRoot = [IO.Path]::GetFullPath($RepositoryRoot)
    $globalJsonPath = Join-Path $resolvedRoot 'global.json'
    if (-not (Test-Path -LiteralPath $globalJsonPath -PathType Leaf)) {
        throw "The required SDK configuration is missing: $globalJsonPath"
    }

    try {
        $sdkConfiguration = Get-Content -LiteralPath $globalJsonPath -Raw -Encoding UTF8 | ConvertFrom-Json
        $requiredVersion = [string]$sdkConfiguration.sdk.version
    } catch {
        throw "The SDK configuration is invalid: $($_.Exception.Message)"
    }

    $versionMatch = [regex]::Match($requiredVersion, '^(?<major>\d+)\.(?<minor>\d+)\.\d+$')
    if (-not $versionMatch.Success) {
        throw "The SDK version in global.json is invalid: $requiredVersion"
    }

    $channel = "$($versionMatch.Groups['major'].Value).$($versionMatch.Groups['minor'].Value)"
    $localInstallDirectory = Join-Path $resolvedRoot '.dotnet'
    $localDotnet = Join-Path $localInstallDirectory 'dotnet.exe'
    $repairInstallDirectory = Join-Path $localInstallDirectory 'bootstrap'
    $repairDotnet = Join-Path $repairInstallDirectory 'dotnet.exe'
    $candidates = [Collections.Generic.List[string]]::new()
    if (Test-Path -LiteralPath $localDotnet -PathType Leaf) {
        $candidates.Add($localDotnet)
    }
    if (Test-Path -LiteralPath $repairDotnet -PathType Leaf) {
        $candidates.Add($repairDotnet)
    }

    $globalDotnet = Get-Command dotnet -CommandType Application -ErrorAction SilentlyContinue |
        Select-Object -First 1
    if ($null -ne $globalDotnet -and -not $candidates.Contains($globalDotnet.Source)) {
        $candidates.Add($globalDotnet.Source)
    }

    foreach ($candidate in $candidates) {
        if (Test-NetCheckDotNetSdk -DotNetPath $candidate -RepositoryRoot $resolvedRoot -Channel $channel) {
            return $candidate
        }
    }

    $installDirectory = $localInstallDirectory
    $installedDotnet = $localDotnet
    if (Test-Path -LiteralPath $localInstallDirectory -PathType Container) {
        Write-Warning 'The repository-local .NET SDK is incomplete or incompatible. Installing an isolated repair copy.'
        if (Test-Path -LiteralPath $repairInstallDirectory) {
            $repairPrefix = $localInstallDirectory.TrimEnd([IO.Path]::DirectorySeparatorChar) +
                [IO.Path]::DirectorySeparatorChar
            if (-not $repairInstallDirectory.StartsWith($repairPrefix, [StringComparison]::OrdinalIgnoreCase)) {
                throw 'The resolved SDK repair directory is outside the repository-local SDK directory.'
            }

            Remove-Item -LiteralPath $repairInstallDirectory -Recurse -Force
        }
        $installDirectory = $repairInstallDirectory
        $installedDotnet = $repairDotnet
    } else {
        Write-Host ".NET SDK $channel was not found. Installing it locally without administrator rights..." -ForegroundColor Cyan
    }

    Install-NetCheckDotNetSdk -InstallDirectory $installDirectory -Channel $channel

    if (-not (Test-NetCheckDotNetSdk -DotNetPath $installedDotnet -RepositoryRoot $resolvedRoot -Channel $channel)) {
        throw "The local .NET SDK installation completed, but $installedDotnet is incomplete or cannot satisfy global.json."
    }

    return $installedDotnet
}

function Test-NetCheckDotNetSdk {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$DotNetPath,

        [Parameter(Mandatory)]
        [string]$RepositoryRoot,

        [Parameter(Mandatory)]
        [string]$Channel
    )

    if (-not (Test-Path -LiteralPath $DotNetPath -PathType Leaf)) {
        return $false
    }

    Push-Location -LiteralPath $RepositoryRoot
    try {
        $versionOutput = & $DotNetPath --version 2>$null
        if ($LASTEXITCODE -ne 0 -or $null -eq $versionOutput) {
            return $false
        }

        $resolvedVersion = ([string](@($versionOutput)[-1])).Trim()
        if (-not $resolvedVersion.StartsWith("$Channel.", [StringComparison]::Ordinal)) {
            return $false
        }

        $dotnetRoot = Split-Path -Parent ([IO.Path]::GetFullPath($DotNetPath))
        $sdkDirectory = Join-Path $dotnetRoot "sdk\$resolvedVersion"
        $requiredFiles = @(
            (Join-Path $sdkDirectory 'NuGet.targets'),
            (Join-Path $sdkDirectory 'Sdks\Microsoft.NET.Sdk\Sdk\Sdk.props'),
            (Join-Path $sdkDirectory 'Sdks\Microsoft.NET.Sdk\Sdk\Sdk.targets')
        )
        foreach ($requiredFile in $requiredFiles) {
            if (-not (Test-Path -LiteralPath $requiredFile -PathType Leaf)) {
                return $false
            }
        }

        $windowsDesktopReferenceRoot = Join-Path $dotnetRoot 'packs\Microsoft.WindowsDesktop.App.Ref'
        $windowsDesktopReferencePack = Get-ChildItem -LiteralPath $windowsDesktopReferenceRoot `
            -Directory `
            -ErrorAction SilentlyContinue |
            Where-Object Name -Like "$Channel.*" |
            Select-Object -First 1
        return $null -ne $windowsDesktopReferencePack
    } catch {
        return $false
    } finally {
        Pop-Location
    }
}

function Install-NetCheckDotNetSdk {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$InstallDirectory,

        [Parameter(Mandatory)]
        [string]$Channel
    )

    $installerUri = 'https://dot.net/v1/dotnet-install.ps1'
    $downloadDirectory = Join-Path ([IO.Path]::GetTempPath()) 'NetCheck-build'
    $installerPath = Join-Path $downloadDirectory 'dotnet-install.ps1'
    [IO.Directory]::CreateDirectory($downloadDirectory) | Out-Null
    [IO.Directory]::CreateDirectory($InstallDirectory) | Out-Null

    $previousProgressPreference = $ProgressPreference
    try {
        $ProgressPreference = 'SilentlyContinue'
        if ([Net.ServicePointManager]::SecurityProtocol -band [Net.SecurityProtocolType]::Tls12) {
            # TLS 1.2 is already enabled.
        } else {
            [Net.ServicePointManager]::SecurityProtocol =
                [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
        }

        Invoke-WebRequest -Uri $installerUri -OutFile $installerPath -UseBasicParsing
    } catch {
        throw "Could not download the official .NET installer from $installerUri. Check the internet connection and proxy settings. $($_.Exception.Message)"
    } finally {
        $ProgressPreference = $previousProgressPreference
    }

    if (-not (Test-Path -LiteralPath $installerPath -PathType Leaf) -or
        (Get-Item -LiteralPath $installerPath).Length -lt 10KB) {
        throw 'The downloaded .NET installer is missing or incomplete.'
    }

    & $installerPath `
        -Channel $Channel `
        -Quality GA `
        -Architecture x64 `
        -InstallDir $InstallDirectory `
        -NoPath
    $installerSucceeded = $?
    if (-not $installerSucceeded) {
        throw 'The .NET SDK installer did not complete successfully.'
    }
}
