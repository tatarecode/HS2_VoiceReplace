# UabAudioClipPatcher

`UabAudioClipPatcher` is a small command-line tool used by HS2VoiceReplace during bundle rebuild.

It opens a Unity asset bundle, locates `AudioClip` payload fields, and replaces those payloads with converted audio files prepared earlier in the pipeline.

## Purpose

- Patch rebuilt voice bundles without relying on a full editor workflow
- Replace only `AudioClip` payload bytes
- Keep the original bundle structure and object layout intact as much as possible

## Build Prerequisites

You need all of the following:

- .NET 8 SDK
- `AssetsTools.NET.dll`
- A writable output directory for `dotnet build`

This repository does not vendor `AssetsTools.NET.dll`; obtain it separately from the upstream project or your local toolchain.
An optional helper script is available at `.\tools\setup_assetstools.ps1`.

By default, the project expects `AssetsTools.NET.dll` at:

- `..\..\_tools\uabea\v8\AssetsTools.NET.dll`

If that file does not exist, you must provide an explicit path:

```powershell
dotnet build .\tools\UabAudioClipPatcher\UabAudioClipPatcher.csproj -c Release -p:AssetsToolsNetPath=C:\path\to\AssetsTools.NET.dll
```

A successful build requires that the file exists and is compatible with the `AssetsTools.NET` namespaces used in `Program.cs`.

## Build

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\setup_assetstools.ps1
dotnet build .\tools\UabAudioClipPatcher\UabAudioClipPatcher.csproj -c Release
```

## Usage

```text
UabAudioClipPatcher <bundlePath> <classdata.tpk> <payloadDir> <outputBundlePath> <payloadExt=.wav>
```

## Arguments

- `bundlePath`
  - Source Unity asset bundle to patch
- `classdata.tpk`
  - UABEA class database used by AssetsTools.NET
- `payloadDir`
  - Directory that contains replacement audio payload files
- `outputBundlePath`
  - Patched bundle output path
- `payloadExt`
  - Extension of replacement payloads, default `.wav`

## Notes

- This tool is intended to be called by the GUI workflow, but it can also be used manually for debugging.
- The patcher only works when payload file names match the clip names expected by the bundle.
- `classdata.tpk` is not vendored here; obtain it separately through the dependency setup flow or from the upstream UABEA release.
