# HS2VoiceReplace Runtime

This directory contains the runtime plugin project that is deployed as `HS2_VoiceReplace.dll`.

The plugin is intentionally small. It provides runtime-side configuration and integration points used by generated voice-replacement packages.

## Purpose

- Ship the runtime DLL used by deployed voice-replacement packages
- Keep runtime behavior separate from the GUI and build pipeline
- Avoid embedding GUI-only logic into the game-side plugin

## Build Prerequisites

You need all of the following:

- .NET Framework 4.7.2 targeting pack
- A valid `GameRoot` that points to a Honey Select 2 installation or equivalent reference layout
- The following assemblies available under `$(GameRoot)`:
  - `BepInEx\core\BepInEx.dll`
  - `BepInEx\core\0Harmony.dll`
  - `HoneySelect2_Data\Managed\UnityEngine.dll`
  - `HoneySelect2_Data\Managed\UnityEngine.CoreModule.dll`
  - `HoneySelect2_Data\Managed\UnityEngine.UI.dll`

## Build

```powershell
dotnet build .\runtime\HS2VoiceReplace.Runtime\HS2VoiceReplace.Runtime.csproj -c Release -p:GameRoot=C:\path\to\HoneySelect2
```

## Output

- Assembly name: `HS2_VoiceReplace.dll`
- Target framework: `net472`

## Notes

- This project does not vendor game-side assemblies.
- The plugin is meant to stay generic and work with existing personality IDs rather than injecting new IDs.
