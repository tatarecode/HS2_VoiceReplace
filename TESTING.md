# Testing

This repository includes automated tests for both the C# application code and the maintained Python helper scripts.

## What is covered

- `tests/HS2VoiceReplace.Tests`
  - localization catalog lookups
  - localized attribute behavior
  - `SeedVcUiSettings` clone and summary behavior
  - signatures, freshness checks, report parsing, target resolution, and grid helpers
- `tests/python`
  - `python_cli_common.py`
  - `seed_vc_batch_common.py`
  - pure helper logic in `select_voice_style_segment.py`

The current suite intentionally focuses on stable, dependency-light logic first. GUI rendering, long-running external processes, and full HS2 asset pipelines are not part of this first test layer.

## Run everything

```powershell
.\tools\run_tests.ps1
.\tools\run_tests.cmd
```

## Run only one side

```powershell
.\tools\run_tests.ps1 -SkipPython
.\tools\run_tests.ps1 -SkipDotNet
```

## Minimal Build Verification

### HS2VoiceReplace

Verify that the GUI project can resolve `AssetsTools.NET.dll` and produce a build:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\setup_assetstools.ps1
dotnet build .\tools\HS2VoiceReplace\HS2VoiceReplace.csproj -c Release
```

If `AssetsTools.NET.dll` is not available at the default path, specify it explicitly:

```powershell
dotnet build .\tools\HS2VoiceReplace\HS2VoiceReplace.csproj -c Release -p:AssetsToolsNetPath=C:\path\to\AssetsTools.NET.dll
```

### UabAudioClipPatcher

Verify that the patcher project can resolve `AssetsTools.NET.dll` and produce a build:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\setup_assetstools.ps1
dotnet build .\tools\UabAudioClipPatcher\UabAudioClipPatcher.csproj -c Release
```

If `AssetsTools.NET.dll` is not available at the default path, specify it explicitly:

```powershell
dotnet build .\tools\UabAudioClipPatcher\UabAudioClipPatcher.csproj -c Release -p:AssetsToolsNetPath=C:\path\to\AssetsTools.NET.dll
```

### HS2VoiceReplace.Runtime

Verify that the runtime plugin project can resolve the game-side assemblies and produce `HS2_VoiceReplace.dll`:

```powershell
dotnet build .\runtime\HS2VoiceReplace.Runtime\HS2VoiceReplace.Runtime.csproj -c Release -p:GameRoot=C:\path\to\HoneySelect2
```

This check requires a valid `GameRoot` with the expected BepInEx and Unity assemblies.

## Notes

- Python tests prefer `._tools\python310\python.exe` when it exists.
- If PowerShell script execution is restricted, use `.\tools\run_tests.cmd`.
- C# tests use `xUnit` and `dotnet test`.
- If you add user-facing strings or localization metadata, update the related C# tests as well.
