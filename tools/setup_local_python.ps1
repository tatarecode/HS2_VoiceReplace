param(
    [string]$DestinationDir = "",
    [string]$PythonEmbedUrl = "",
    [string]$GetPipUrl = "",
    [string[]]$Packages = @(),
    [switch]$SkipPackages,
    [switch]$Force
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

function Get-RuntimeManifest {
    $defaults = [ordered]@{
        embed_version = "3.10.11"
        embed_zip_url = "https://www.python.org/ftp/python/3.10.11/python-3.10.11-embed-amd64.zip"
        get_pip_url = "https://bootstrap.pypa.io/get-pip.py"
        repo_local_python_relative_path = "_tools/python310/python.exe"
        repo_local_packages = @("numpy", "soundfile", "librosa")
    }

    $manifestPath = Join-Path $repoRoot "tools\python_runtime_manifest.json"
    if (Test-Path $manifestPath) {
        $loaded = Get-Content $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
        foreach ($name in $defaults.Keys) {
            if ($null -ne $loaded.$name -and @($loaded.$name).Count -gt 0) {
                $defaults[$name] = $loaded.$name
            }
        }
    }

    return [pscustomobject]$defaults
}

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

function Invoke-CheckedCommand {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FilePath,
        [string[]]$ArgumentList = @()
    )

    & $FilePath @ArgumentList
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed (exit=$LASTEXITCODE): $FilePath $($ArgumentList -join ' ')"
    }
}

function Patch-EmbeddedPythonPth {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PythonDir
    )

    $pthPath = Get-ChildItem -Path $PythonDir -Filter "python*._pth" -File | Select-Object -First 1 -ExpandProperty FullName
    if (-not $pthPath) {
        throw "Embedded Python path file was not found under: $PythonDir"
    }

    $zipName = Get-ChildItem -Path $PythonDir -Filter "python*.zip" -File | Select-Object -First 1 -ExpandProperty Name
    if (-not $zipName) {
        $zipName = "python310.zip"
    }

    $lines = @(
        $zipName,
        ".",
        "Lib",
        "Lib\site-packages",
        "import site"
    )

    Set-Content -Path $pthPath -Value $lines -Encoding Ascii
}

function Install-EmbeddedPython {
    param(
        [Parameter(Mandatory = $true)]
        [string]$DestinationFullPath,
        [Parameter(Mandatory = $true)]
        [string]$ZipDownloadUrl
    )

    $tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("hs2vr_python_" + [System.Guid]::NewGuid().ToString("N"))
    $zipPath = Join-Path $tempRoot "python-embed.zip"
    $extractRoot = Join-Path $tempRoot "extract"

    try {
        New-Item -ItemType Directory -Force -Path $tempRoot | Out-Null
        New-Item -ItemType Directory -Force -Path $extractRoot | Out-Null

        Write-Host "[download] $ZipDownloadUrl"
        Invoke-WebRequest -Uri $ZipDownloadUrl -OutFile $zipPath -Headers @{
            "User-Agent" = "HS2VoiceReplace setup_local_python.ps1"
            "Accept" = "application/octet-stream"
        }

        if (Test-Path $DestinationFullPath) {
            Remove-Item -Recurse -Force $DestinationFullPath
        }

        New-Item -ItemType Directory -Force -Path $DestinationFullPath | Out-Null

        Write-Host "[extract] $zipPath"
        Expand-Archive -Path $zipPath -DestinationPath $extractRoot -Force
        Copy-Item -Path (Join-Path $extractRoot "*") -Destination $DestinationFullPath -Recurse -Force

        New-Item -ItemType Directory -Force -Path (Join-Path $DestinationFullPath "Lib") | Out-Null
        New-Item -ItemType Directory -Force -Path (Join-Path $DestinationFullPath "Lib\site-packages") | Out-Null
        Patch-EmbeddedPythonPth -PythonDir $DestinationFullPath
    }
    finally {
        if (Test-Path $tempRoot) {
            Remove-Item -Recurse -Force $tempRoot
        }
    }
}

$runtimeManifest = Get-RuntimeManifest
if (-not $PSBoundParameters.ContainsKey("DestinationDir")) {
    $relativePythonPath = ($runtimeManifest.repo_local_python_relative_path -replace "/", "\")
    $DestinationDir = Split-Path -Parent $relativePythonPath
}
if (-not $PSBoundParameters.ContainsKey("PythonEmbedUrl")) {
    $PythonEmbedUrl = $runtimeManifest.embed_zip_url
}
if (-not $PSBoundParameters.ContainsKey("GetPipUrl")) {
    $GetPipUrl = $runtimeManifest.get_pip_url
}
if (-not $PSBoundParameters.ContainsKey("Packages")) {
    $Packages = @($runtimeManifest.repo_local_packages)
}

$destinationFullPath = Get-AbsolutePath $DestinationDir
$pythonExe = Join-Path $destinationFullPath "python.exe"

if ($Force -or -not (Test-Path $pythonExe)) {
    Install-EmbeddedPython -DestinationFullPath $destinationFullPath -ZipDownloadUrl $PythonEmbedUrl
}
else {
    Write-Host "[skip] Embedded Python already exists: $pythonExe"
    Patch-EmbeddedPythonPth -PythonDir $destinationFullPath
    New-Item -ItemType Directory -Force -Path (Join-Path $destinationFullPath "Lib\site-packages") | Out-Null
}

$pythonExe = Join-Path $destinationFullPath "python.exe"
if (-not (Test-Path $pythonExe)) {
    throw "python.exe was not found after setup: $pythonExe"
}

$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("hs2vr_python_bootstrap_" + [System.Guid]::NewGuid().ToString("N"))
$getPipPath = Join-Path $tempRoot "get-pip.py"

try {
    New-Item -ItemType Directory -Force -Path $tempRoot | Out-Null

    Write-Host "[download] $GetPipUrl"
    Invoke-WebRequest -Uri $GetPipUrl -OutFile $getPipPath -Headers @{
        "User-Agent" = "HS2VoiceReplace setup_local_python.ps1"
        "Accept" = "text/x-python"
    }

    Write-Host "[bootstrap] pip"
    Invoke-CheckedCommand -FilePath $pythonExe -ArgumentList @($getPipPath, "--disable-pip-version-check")

    Write-Host "[install] pip setuptools wheel"
    Invoke-CheckedCommand -FilePath $pythonExe -ArgumentList @("-m", "pip", "install", "--upgrade", "pip", "setuptools", "wheel")

    if (-not $SkipPackages -and $Packages.Count -gt 0) {
        Write-Host "[install] $($Packages -join ', ')"
        $packageArgs = @("-m", "pip", "install") + $Packages
        Invoke-CheckedCommand -FilePath $pythonExe -ArgumentList $packageArgs
    }
}
finally {
    if (Test-Path $tempRoot) {
        Remove-Item -Recurse -Force $tempRoot
    }
}

Write-Host "[done] Repo-local Python is ready: $pythonExe"
