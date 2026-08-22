[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$FilePath,

    [Parameter(Mandatory)]
    [string]$CertificatePath,

    [Parameter(Mandatory)]
    [string]$CertificatePassword
)

$ErrorActionPreference = 'Stop'
$target = [IO.Path]::GetFullPath($FilePath)
$certificateFile = [IO.Path]::GetFullPath($CertificatePath)
if (-not (Test-Path -LiteralPath $target -PathType Leaf)) {
    throw "The Authenticode target does not exist: $target"
}
if (-not (Test-Path -LiteralPath $certificateFile -PathType Leaf)) {
    throw "The Authenticode certificate does not exist: $certificateFile"
}

$certificate = [Security.Cryptography.X509Certificates.X509Certificate2]::new(
    $certificateFile,
    $CertificatePassword,
    [Security.Cryptography.X509Certificates.X509KeyStorageFlags]::EphemeralKeySet)
try {
    if (-not $certificate.HasPrivateKey) {
        throw 'The Authenticode certificate does not contain a private key.'
    }
    if ([string]::Equals(
            $certificate.Subject,
            $certificate.Issuer,
            [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Self-signed certificates are not accepted for NetCheck release signing.'
    }
} finally {
    $certificate.Dispose()
}

$windowsKitsRoot = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\bin'
$signTool = Get-ChildItem -LiteralPath $windowsKitsRoot `
    -Filter 'signtool.exe' `
    -File `
    -Recurse `
    -ErrorAction SilentlyContinue |
    Where-Object FullName -Match '\\x64\\signtool\.exe$' |
    Sort-Object FullName -Descending |
    Select-Object -First 1 -ExpandProperty FullName
if ([string]::IsNullOrWhiteSpace($signTool)) {
    throw 'The Windows SDK signtool.exe could not be located.'
}

& $signTool sign `
    /fd SHA256 `
    /td SHA256 `
    /tr 'https://timestamp.digicert.com' `
    /f $certificateFile `
    /p $CertificatePassword `
    $target
if ($LASTEXITCODE -ne 0) {
    throw "signtool failed with exit code $LASTEXITCODE."
}

$signature = Get-AuthenticodeSignature -LiteralPath $target
if ($signature.Status.ToString() -ne 'Valid') {
    throw "Authenticode verification failed with status $($signature.Status): $($signature.StatusMessage)"
}
if ($null -eq $signature.SignerCertificate -or [string]::Equals(
        $signature.SignerCertificate.Subject,
        $signature.SignerCertificate.Issuer,
        [StringComparison]::OrdinalIgnoreCase)) {
    throw 'The resulting Authenticode signature is missing or self-signed.'
}

Write-Host "Authenticode signature verified for $target" -ForegroundColor Green
