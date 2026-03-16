param(
    [Parameter(Mandatory = $true)]
    [string]$GameRoot,
    [string]$OutputRoot = ".\release",
    [string]$PackageLabel = "",
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

function Copy-DirectoryContents {
    param(
        [Parameter(Mandatory = $true)]
        [string]$SourceDir,
        [Parameter(Mandatory = $true)]
        [string]$DestinationDir
    )

    if (-not (Test-Path $SourceDir)) {
        throw "Directory was not found: $SourceDir"
    }

    New-Item -ItemType Directory -Force -Path $DestinationDir | Out-Null
    Copy-Item -Path (Join-Path $SourceDir "*") -Destination $DestinationDir -Recurse -Force
}

function Get-DefaultPackageLabel {
    $manifestPath = Join-Path $repoRoot "mods_src\HS2VoiceReplaceRuntimeTemplate\manifest.xml"
    if (Test-Path $manifestPath) {
        try {
            [xml]$manifest = Get-Content $manifestPath -Raw -Encoding UTF8
            $version = $manifest.manifest.version
            if (-not [string]::IsNullOrWhiteSpace($version)) {
                return "HS2VoiceReplace-$($version.Trim())-win-x64"
            }
        }
        catch {
        }
    }

    return "HS2VoiceReplace-win-x64"
}

$gameRootFullPath = Get-AbsolutePath $GameRoot
if (-not (Test-Path $gameRootFullPath)) {
    throw "GameRoot was not found: $gameRootFullPath"
}

$packageLabelValue = if ([string]::IsNullOrWhiteSpace($PackageLabel)) {
    Get-DefaultPackageLabel
}
else {
    $PackageLabel.Trim()
}

$outputRootFullPath = Get-AbsolutePath $OutputRoot
$packageRoot = Join-Path $outputRootFullPath $packageLabelValue
$zipPath = Join-Path $outputRootFullPath ($packageLabelValue + ".zip")
$stageRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("hs2vr_release_" + [System.Guid]::NewGuid().ToString("N"))
$guiPublishDir = Join-Path $stageRoot "gui"
$patcherPublishDir = Join-Path $stageRoot "patcher"

if ((Test-Path $packageRoot) -or (Test-Path $zipPath)) {
    if (-not $Force) {
        throw "Release output already exists. Use -Force to overwrite: $packageRoot"
    }
}

try {
    New-Item -ItemType Directory -Force -Path $stageRoot | Out-Null
    New-Item -ItemType Directory -Force -Path $outputRootFullPath | Out-Null

    if (Test-Path $packageRoot) {
        Remove-Item -Recurse -Force $packageRoot
    }
    if (Test-Path $zipPath) {
        Remove-Item -Force $zipPath
    }

    Invoke-CheckedCommand -FilePath "powershell" -ArgumentList @(
        "-NoProfile",
        "-ExecutionPolicy", "Bypass",
        "-File", ".\tools\setup_assetstools.ps1"
    )

    Invoke-CheckedCommand -FilePath "dotnet" -ArgumentList @(
        "publish",
        ".\tools\HS2VoiceReplaceGui\HS2VoiceReplaceGui.csproj",
        "-c", "Release",
        "-r", "win-x64",
        "--self-contained", "false",
        "-o", $guiPublishDir,
        "--nologo"
    )

    Invoke-CheckedCommand -FilePath "dotnet" -ArgumentList @(
        "publish",
        ".\tools\UabAudioClipPatcher\UabAudioClipPatcher.csproj",
        "-c", "Release",
        "-r", "win-x64",
        "--self-contained", "false",
        "-o", $patcherPublishDir,
        "--nologo"
    )

    Invoke-CheckedCommand -FilePath "dotnet" -ArgumentList @(
        "build",
        ".\runtime\HS2VoiceReplace.Runtime\HS2VoiceReplace.Runtime.csproj",
        "-c", "Release",
        "-p:GameRoot=$gameRootFullPath",
        "--nologo"
    )

    $runtimeDllPath = Join-Path $repoRoot "runtime\HS2VoiceReplace.Runtime\bin\Release\net472\HS2_VoiceReplace.dll"
    if (-not (Test-Path $runtimeDllPath)) {
        throw "Runtime DLL was not found after build: $runtimeDllPath"
    }

    Copy-DirectoryContents -SourceDir $guiPublishDir -DestinationDir $packageRoot
    Copy-DirectoryContents -SourceDir $patcherPublishDir -DestinationDir (Join-Path $packageRoot "tools\UabAudioClipPatcher")

    New-Item -ItemType Directory -Force -Path (Join-Path $packageRoot "plugins") | Out-Null
    Copy-Item -Path $runtimeDllPath -Destination (Join-Path $packageRoot "plugins\HS2_VoiceReplace.dll") -Force

    Copy-DirectoryContents `
        -SourceDir (Join-Path $repoRoot "mods_src\HS2VoiceReplaceRuntimeTemplate") `
        -DestinationDir (Join-Path $packageRoot "mods_template\HS2VoiceReplaceRuntime")

    New-Item -ItemType Directory -Force -Path (Join-Path $packageRoot "tools") | Out-Null
    foreach ($relativePath in @(
        "tools\python_cli_common.py",
        "tools\seed_vc_batch_common.py",
        "tools\seed_vc_v1_inprocess_batch.py",
        "tools\seed_vc_v2_inprocess_batch.py",
        "tools\select_voice_style_segment.py",
        "tools\python_runtime_manifest.json"
    )) {
        $sourcePath = Join-Path $repoRoot $relativePath
        if (-not (Test-Path $sourcePath)) {
            throw "Bundled source file was not found: $sourcePath"
        }

        $destinationPath = Join-Path $packageRoot $relativePath
        New-Item -ItemType Directory -Force -Path (Split-Path -Parent $destinationPath) | Out-Null
        Copy-Item -Path $sourcePath -Destination $destinationPath -Force
    }

    foreach ($relativePath in @("README.md", "README_JA.md", "LICENSE")) {
        $sourcePath = Join-Path $repoRoot $relativePath
        if (Test-Path $sourcePath) {
            Copy-Item -Path $sourcePath -Destination (Join-Path $packageRoot $relativePath) -Force
        }
    }

    Compress-Archive -Path $packageRoot -DestinationPath $zipPath -Force

    Write-Host "[done] Release folder: $packageRoot"
    Write-Host "[done] Release zip: $zipPath"
}
finally {
    if (Test-Path $stageRoot) {
        Remove-Item -Recurse -Force $stageRoot
    }
}
