param(
    [string]$DestinationPath = ".\_tools\uabea\v8\AssetsTools.NET.dll",
    [string]$ReleaseApiUrl = "https://api.github.com/repos/nesrak1/UABEA/releases/latest",
    [string]$ZipUrl,
    [switch]$Force
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

function Get-AbsolutePath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PathValue
    )

    if ([System.IO.Path]::IsPathRooted($PathValue)) {
        return [System.IO.Path]::GetFullPath($PathValue)
    }

    return [System.IO.Path]::GetFullPath((Join-Path $repoRoot $PathValue))
}

function Resolve-UabeaZipUrl {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ApiUrl
    )

    Write-Host "[info] Querying latest UABEA release metadata..."
    $release = Invoke-RestMethod -Uri $ApiUrl -Headers @{
        "User-Agent" = "HS2VoiceReplace setup_assetstools.ps1"
        "Accept" = "application/vnd.github+json"
    }

    foreach ($asset in $release.assets) {
        if (-not $asset.browser_download_url) { continue }
        if (-not $asset.name.EndsWith(".zip", [System.StringComparison]::OrdinalIgnoreCase)) { continue }
        if ($asset.name -match "win") {
            return $asset.browser_download_url
        }
    }

    foreach ($asset in $release.assets) {
        if (-not $asset.browser_download_url) { continue }
        if ($asset.name.EndsWith(".zip", [System.StringComparison]::OrdinalIgnoreCase)) {
            return $asset.browser_download_url
        }
    }

    throw "Could not find a downloadable UABEA zip asset in the latest release."
}

function Find-AssetsToolsDll {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Root
    )

    $matches = Get-ChildItem -Path $Root -Recurse -File -Filter "AssetsTools.NET.dll"
    if (-not $matches) {
        return $null
    }

    return $matches |
        Sort-Object FullName |
        Select-Object -First 1 -ExpandProperty FullName
}

$destinationFullPath = Get-AbsolutePath $DestinationPath
if ((Test-Path $destinationFullPath) -and (-not $Force)) {
    Write-Host "[skip] AssetsTools.NET.dll already exists: $destinationFullPath"
    Write-Host "[hint] Use -Force to overwrite it."
    exit 0
}

$zipDownloadUrl = if ([string]::IsNullOrWhiteSpace($ZipUrl)) {
    Resolve-UabeaZipUrl -ApiUrl $ReleaseApiUrl
}
else {
    $ZipUrl.Trim()
}

$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("hs2vr_assetstools_" + [System.Guid]::NewGuid().ToString("N"))
$zipPath = Join-Path $tempRoot "uabea.zip"
$extractRoot = Join-Path $tempRoot "extract"

try {
    New-Item -ItemType Directory -Force -Path $tempRoot | Out-Null
    New-Item -ItemType Directory -Force -Path $extractRoot | Out-Null

    Write-Host "[download] $zipDownloadUrl"
    Invoke-WebRequest -Uri $zipDownloadUrl -OutFile $zipPath -Headers @{
        "User-Agent" = "HS2VoiceReplace setup_assetstools.ps1"
        "Accept" = "application/octet-stream"
    }

    Write-Host "[extract] $zipPath"
    Expand-Archive -Path $zipPath -DestinationPath $extractRoot -Force

    $dllSourcePath = Find-AssetsToolsDll -Root $extractRoot
    if (-not $dllSourcePath) {
        throw "AssetsTools.NET.dll was not found in the downloaded archive."
    }

    $destinationDir = Split-Path -Parent $destinationFullPath
    New-Item -ItemType Directory -Force -Path $destinationDir | Out-Null

    Copy-Item -Force $dllSourcePath $destinationFullPath
    Write-Host "[done] Copied AssetsTools.NET.dll to $destinationFullPath"
}
finally {
    if (Test-Path $tempRoot) {
        Remove-Item -Recurse -Force $tempRoot
    }
}
