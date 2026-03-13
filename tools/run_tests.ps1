param(
    [switch]$SkipDotNet,
    [switch]$SkipPython
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

function Get-RuntimeManifest {
    $defaults = [ordered]@{
        repo_local_python_relative_path = "_tools/python310/python.exe"
    }

    $manifestPath = Join-Path $repoRoot "tools\python_runtime_manifest.json"
    if (Test-Path $manifestPath) {
        $loaded = Get-Content $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
        if (-not [string]::IsNullOrWhiteSpace($loaded.repo_local_python_relative_path)) {
            $defaults.repo_local_python_relative_path = $loaded.repo_local_python_relative_path
        }
    }

    return [pscustomobject]$defaults
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

function Invoke-CheckedCommandWithRetry {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FilePath,
        [string[]]$ArgumentList = @(),
        [int]$MaxAttempts = 3,
        [int]$DelaySeconds = 2
    )

    for ($attempt = 1; $attempt -le $MaxAttempts; $attempt++) {
        try {
            Invoke-CheckedCommand $FilePath $ArgumentList
            return
        }
        catch {
            if ($attempt -ge $MaxAttempts) {
                throw
            }
            Write-Host "[retry] Attempt $attempt failed. Retrying in $DelaySeconds second(s)..."
            Start-Sleep -Seconds $DelaySeconds
        }
    }
}

function Resolve-PythonExe {
    $runtimeManifest = Get-RuntimeManifest
    $repoLocalPython = Join-Path $repoRoot (($runtimeManifest.repo_local_python_relative_path -replace "/", "\"))
    $candidates = @(
        (Join-Path $repoRoot "_tools\rvc_webui\.venv\Scripts\python.exe"),
        $repoLocalPython,
        "python"
    )
    foreach ($candidate in $candidates) {
        if ($candidate -eq "python") {
            $cmd = Get-Command python -ErrorAction SilentlyContinue
            if ($cmd) { return $cmd.Source }
        }
        elseif (Test-Path $candidate) {
            return $candidate
        }
    }
    throw "Python executable was not found."
}

if (-not $SkipDotNet) {
    Write-Host "[dotnet] Running C# tests..."
    Invoke-CheckedCommandWithRetry "dotnet" @(
        "test",
        ".\\tests\\HS2VoiceReplace.Tests\\HS2VoiceReplace.Tests.csproj",
        "-c", "Release",
        "--nologo"
    )
}

if (-not $SkipPython) {
    $pythonExe = Resolve-PythonExe
    Write-Host "[python] Running Python tests with $pythonExe ..."
    $env:PYTHONWARNINGS = "ignore::UserWarning"
    Invoke-CheckedCommand $pythonExe @("-m", "unittest", "discover", "-s", ".\\tests\\python", "-p", "test_*.py", "-v")
}

Write-Host "All requested tests completed."

