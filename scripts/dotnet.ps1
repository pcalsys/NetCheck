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

    $sdkArchiveUri = "https://aka.ms/dotnet/$Channel/dotnet-sdk-win-x64.zip"
    $downloadDirectory = Join-Path ([IO.Path]::GetTempPath()) 'NetCheck-build'
    $archiveName = "dotnet-sdk-$Channel-win-x64-$([guid]::NewGuid().ToString('N')).zip"
    $archivePath = Join-Path $downloadDirectory $archiveName
    [IO.Directory]::CreateDirectory($downloadDirectory) | Out-Null
    [IO.Directory]::CreateDirectory($InstallDirectory) | Out-Null

    try {
        Invoke-NetCheckDownloadWithProgress `
            -Uri $sdkArchiveUri `
            -DestinationPath $archivePath `
            -DisplayName ".NET SDK $Channel (x64)"

        Write-Host 'Extracting the .NET SDK. This can take a moment...' -ForegroundColor Cyan
        Expand-Archive -LiteralPath $archivePath -DestinationPath $InstallDirectory -Force
    } catch {
        throw "Could not install the official .NET SDK from $sdkArchiveUri. Check the internet connection, proxy settings, and available disk space. $($_.Exception.Message)"
    } finally {
        if (Test-Path -LiteralPath $archivePath -PathType Leaf) {
            Remove-Item -LiteralPath $archivePath -Force
        }
    }
}

function Invoke-NetCheckDownloadWithProgress {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [uri]$Uri,

        [Parameter(Mandatory)]
        [string]$DestinationPath,

        [Parameter(Mandatory)]
        [string]$DisplayName,

        [ValidateRange(1, 10)]
        [int]$MaximumAttempts = 3
    )

    if ([Net.ServicePointManager]::SecurityProtocol -band [Net.SecurityProtocolType]::Tls12) {
        # TLS 1.2 is already enabled.
    } else {
        [Net.ServicePointManager]::SecurityProtocol =
            [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
    }

    $partialPath = "$DestinationPath.partial"
    for ($attempt = 1; $attempt -le $MaximumAttempts; $attempt++) {
        try {
            Invoke-NetCheckDownloadAttempt `
                -Uri $Uri `
                -DestinationPath $DestinationPath `
                -PartialPath $partialPath `
                -DisplayName $DisplayName
            return
        } catch {
            if (Test-Path -LiteralPath $partialPath -PathType Leaf) {
                Remove-Item -LiteralPath $partialPath -Force
            }

            if ($attempt -eq $MaximumAttempts) {
                throw
            }

            Write-Warning "Download attempt $attempt of $MaximumAttempts failed: $($_.Exception.Message)"
            Write-Host "Retrying the download..." -ForegroundColor Yellow
            Start-Sleep -Seconds 2
        }
    }
}

function Invoke-NetCheckDownloadAttempt {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [uri]$Uri,

        [Parameter(Mandatory)]
        [string]$DestinationPath,

        [Parameter(Mandatory)]
        [string]$PartialPath,

        [Parameter(Mandatory)]
        [string]$DisplayName
    )

    Add-Type -AssemblyName System.Net.Http
    $handler = [Net.Http.HttpClientHandler]::new()
    $handler.UseProxy = $true
    $handler.DefaultProxyCredentials = [Net.CredentialCache]::DefaultCredentials
    $client = [Net.Http.HttpClient]::new($handler)
    $client.Timeout = [TimeSpan]::FromMinutes(20)
    $headRequest = $null
    $headResponse = $null
    $response = $null
    $sourceStream = $null
    $destinationStream = $null
    $progressStarted = $false

    try {
        $expectedLength = $null
        try {
            $headRequest = [Net.Http.HttpRequestMessage]::new([Net.Http.HttpMethod]::Head, $Uri)
            $headResponse = $client.SendAsync(
                $headRequest,
                [Net.Http.HttpCompletionOption]::ResponseHeadersRead
            ).GetAwaiter().GetResult()
            if ($headResponse.IsSuccessStatusCode) {
                $expectedLength = $headResponse.Content.Headers.ContentLength
            }
        } catch {
            # Some proxies and download servers reject HEAD requests. The GET response is the fallback.
        } finally {
            if ($null -ne $headResponse) {
                $headResponse.Dispose()
                $headResponse = $null
            }
            if ($null -ne $headRequest) {
                $headRequest.Dispose()
                $headRequest = $null
            }
        }

        $response = $client.GetAsync(
            $Uri,
            [Net.Http.HttpCompletionOption]::ResponseHeadersRead
        ).GetAwaiter().GetResult()
        $response.EnsureSuccessStatusCode() | Out-Null

        $totalBytes = $response.Content.Headers.ContentLength
        if ($null -eq $totalBytes -or $totalBytes -le 0) {
            $totalBytes = $expectedLength
        }
        if ($null -eq $totalBytes -or $totalBytes -le 0) {
            throw 'The download server did not provide the SDK archive size.'
        }

        $sourceStream = $response.Content.ReadAsStreamAsync().GetAwaiter().GetResult()
        $destinationStream = [IO.File]::Open(
            $PartialPath,
            [IO.FileMode]::Create,
            [IO.FileAccess]::Write,
            [IO.FileShare]::None
        )

        $buffer = [byte[]]::new(1MB)
        $downloadedBytes = [long]0
        $lastPercentage = 0
        $totalMegabytes = $totalBytes / 1MB
        $progressStarted = $true
        Write-Host -NoNewline (
            "`rDownloading {0}:   0% (0.0 of {1:N1} MB)" -f
            $DisplayName,
            $totalMegabytes
        )

        while (($bytesRead = $sourceStream.Read($buffer, 0, $buffer.Length)) -gt 0) {
            $destinationStream.Write($buffer, 0, $bytesRead)
            $downloadedBytes += $bytesRead
            $percentage = [Math]::Min(
                100,
                [int][Math]::Floor(($downloadedBytes * 100.0) / $totalBytes)
            )

            if ($percentage -ne $lastPercentage) {
                $downloadedMegabytes = $downloadedBytes / 1MB
                Write-Host -NoNewline (
                    "`rDownloading {0}: {1,3}% ({2:N1} of {3:N1} MB)" -f
                    $DisplayName,
                    $percentage,
                    $downloadedMegabytes,
                    $totalMegabytes
                )
                $lastPercentage = $percentage
            }
        }

        $destinationStream.Flush()
        $destinationStream.Dispose()
        $destinationStream = $null

        if ($downloadedBytes -ne $totalBytes) {
            throw "The SDK download is incomplete. Expected $totalBytes bytes but received $downloadedBytes bytes."
        }

        if ($lastPercentage -lt 100) {
            Write-Host -NoNewline (
                "`rDownloading {0}: 100% ({1:N1} of {1:N1} MB)" -f
                $DisplayName,
                $totalMegabytes
            )
        }
        Write-Host

        Move-Item -LiteralPath $PartialPath -Destination $DestinationPath -Force
    } catch {
        if ($progressStarted) {
            Write-Host
        }
        throw
    } finally {
        if ($null -ne $destinationStream) {
            $destinationStream.Dispose()
        }
        if ($null -ne $sourceStream) {
            $sourceStream.Dispose()
        }
        if ($null -ne $response) {
            $response.Dispose()
        }
        if ($null -ne $headResponse) {
            $headResponse.Dispose()
        }
        if ($null -ne $headRequest) {
            $headRequest.Dispose()
        }
        $client.Dispose()
        $handler.Dispose()
    }
}
