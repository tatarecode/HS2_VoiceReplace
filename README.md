# HS2VoiceReplace

HS2VoiceReplace is a C# GUI tool for replacing Honey Select 2 voice assets without directly editing a live game installation.

This export contains the minimum source set required to continue development in a separate repository.

Japanese documentation is available in `README_JA.md`.

## Repository Layout

- `tools/HS2VoiceReplaceGui`
  - Main WinForms GUI application (`HS2VoiceReplaceGui.exe`)
- `runtime/HS2VoiceReplace.Runtime`
  - Runtime plugin project used by deployed voice-replacement packages
- `tools/UabAudioClipPatcher`
  - AudioClip bundle patcher used during bundle rebuild
- `tools/*.py`
  - Python helper scripts used for style selection and Seed-VC batch conversion
- `mods_src/HS2VoiceReplaceRuntime`
  - Template files used for generated zipmods
- `tests/HS2VoiceReplace.Tests`
  - C# automated tests
- `tests/python`
  - Python automated tests

## What This Export Includes

- Application source code
- Runtime plugin source code
- Required helper scripts
- Test code
- Minimal packaging template

## What This Export Excludes

- Local build outputs
- Local publish outputs
- Machine-specific virtual environments
- Unrelated historical tooling from the original workspace

## Build Prerequisites

### GUI application

- .NET 8 SDK
- A separately supplied `AssetsTools.NET.dll`
  - This repository does not vendor `AssetsTools.NET.dll`
  - Default source-build path:
    - `.\_tools\uabea\v8\AssetsTools.NET.dll`
  - Optional helper script:
    - `powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\setup_assetstools.ps1`
  - Command-line builds can also use:
    - `-p:AssetsToolsNetPath=C:\path\to\AssetsTools.NET.dll`
- Optional repo-local Python for maintained helper scripts and Python tests
  - Default path:
    - `.\_tools\python310\python.exe`
  - Optional helper script:
    - `powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\setup_local_python.ps1`
- GUI-generated working data defaults to a repository-local folder when possible
  - Default path in a repository checkout:
    - `.\.hs2voicereplace\`
  - The output root can be changed from the GUI basic settings dialog

### Runtime plugin

- .NET Framework 4.7.2 targeting pack
- Game-side references for:
  - `BepInEx.dll`
  - `0Harmony.dll`
  - `UnityEngine.dll`
  - `UnityEngine.CoreModule.dll`
  - `UnityEngine.UI.dll`

The runtime plugin project intentionally does not vendor those game-side assemblies.

## Build

```powershell
dotnet build .\tools\HS2VoiceReplaceGui\HS2VoiceReplaceGui.csproj -c Release
dotnet build .\tools\UabAudioClipPatcher\UabAudioClipPatcher.csproj -c Release
```

If `AssetsTools.NET.dll` is not present at the default path, provide it explicitly:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\setup_assetstools.ps1
dotnet build .\tools\HS2VoiceReplaceGui\HS2VoiceReplaceGui.csproj -c Release -p:AssetsToolsNetPath=C:\path\to\AssetsTools.NET.dll
dotnet build .\tools\UabAudioClipPatcher\UabAudioClipPatcher.csproj -c Release -p:AssetsToolsNetPath=C:\path\to\AssetsTools.NET.dll
```

## Test

Run all C# and Python tests:

```powershell
.\tools\run_tests.cmd
```

Or:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\run_tests.ps1
```

If you want a repository-local Python instead of relying on a machine-wide install:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\setup_local_python.ps1
```

## Main Development Entry Points

- GUI application:
  - `tools/HS2VoiceReplaceGui/MainForm.cs`
- Pipeline orchestration:
  - `tools/HS2VoiceReplaceGui/VoiceReplacePipeline.cs`
- Application service layer:
  - `tools/HS2VoiceReplaceGui/ApplicationServices.cs`
- Localization catalog:
  - `tools/HS2VoiceReplaceGui/UiTextCatalog.cs`

## Additional Notes

- `DEVELOPMENT.md` and `DEVELOPMENT_JA.md` describe the code layout and maintenance entry points.
- `TESTING.md` and `TESTING_JA.md` describe how to run automated tests.
- `tools/HS2VoiceReplaceGui/README.md` and `tools/HS2VoiceReplaceGui/README_JA.md` document tool-specific behavior and dependency setup.
- `tools/UabAudioClipPatcher/README.md` and `tools/UabAudioClipPatcher/README_JA.md` document the bundle patcher and its build prerequisites.
- `runtime/HS2VoiceReplace.Runtime/README.md` and `runtime/HS2VoiceReplace.Runtime/README_JA.md` document the runtime plugin project and its game-side dependencies.

