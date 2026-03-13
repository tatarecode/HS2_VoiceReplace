# HS2VoiceReplace GUI

This directory contains the main WinForms application for HS2VoiceReplace.

The GUI coordinates the full voice-replacement workflow:

1. Extract source voice bundles from a selected Honey Select 2 installation
2. Decode `.fsb` audio into `.wav`
3. Prepare style samples
4. Run Seed-VC conversion
5. Rebuild `unity3d` voice bundles
6. Generate split zipmods
7. Optionally deploy the generated files to a selected HS2 environment

## Bundle Number Handling

- Base-game personalities can use `30.unity3d`
- DX-added personalities can use `50.unity3d`
- The tool does not assume that `50.unity3d` always exists
- Bundle selection prefers the personality-appropriate file name and falls back to the highest available numeric bundle in the target folder

## Dependency Resolution Priority

1. External tools root selected in the GUI
2. Bundled runtime assets as fallback

## Initial Setup

1. Verify the external tools root in the GUI
2. If the repository does not already contain a prebuilt `UabAudioClipPatcher.exe`, provide `AssetsTools.NET.dll` first
   - default source-build path: `.\_tools\uabea\v8\AssetsTools.NET.dll`
   - optional helper script: `powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\setup_assetstools.ps1`
   - or set `HS2VR_ASSETSTOOLS_NET_PATH=C:\path\to\AssetsTools.NET.dll` before launching the GUI
3. Run `Setup Dependencies`
4. After setup finishes, continue with extraction, preview, conversion, and deployment as needed

## Dependencies Downloaded by Setup

- Python embeddable package
- `get-pip.py` / pip
- Seed-VC repository archive
- UABEA release archive for `classdata.tpk`
- vgmstream release archive for `vgmstream-cli.exe`
- Required Seed-VC Python packages
- `noisereduce`

`Setup Dependencies` does not download or vendor `AssetsTools.NET.dll`. Source builds that need it must obtain it separately.

## Project-Supplied Auxiliary Files

The repository provides the following source-side assets:

- `tools/seed_vc_v1_inprocess_batch.py`
- `tools/seed_vc_v2_inprocess_batch.py`
- `tools/select_voice_style_segment.py`
- `tools/UabAudioClipPatcher/*`
- `mods_src/HS2VoiceReplaceRuntime/*`
- `runtime/HS2VoiceReplace.Runtime/*`

During dependency setup or local development, the application can copy or build the required files into the selected external tools root.

If a required file is missing from the selected external tools root, the application resolves it in this order:

1. local source files in the repository
2. bundled runtime assets
3. `dotnet build` for `UabAudioClipPatcher` or the runtime plugin, when applicable

If setup falls back to building `UabAudioClipPatcher` from source, the build uses `AssetsTools.NET.dll` from the default path above or from `HS2VR_ASSETSTOOLS_NET_PATH`.

## Skipping Completed Steps

When `Skip completed steps` is enabled:

- dependency setup markers are stored under `external_tools\\_state\\*.done`
- workflow step markers are stored under `...gui_runs\\resume_cXX\\_done\\*.done`

## URL Overrides

- `HS2VR_UABEA_ZIP_URL`
- `HS2VR_VGMSTREAM_ZIP_URL`

## Build

```powershell
dotnet build .\tools\HS2VoiceReplace\HS2VoiceReplace.csproj -c Release
dotnet publish .\tools\HS2VoiceReplace\HS2VoiceReplace.csproj -c Release -r win-x64 --self-contained false -o .\tools\HS2VoiceReplace\publish\win-x64
```

If `AssetsTools.NET.dll` is not present at the default path, build with:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\setup_assetstools.ps1
dotnet build .\tools\HS2VoiceReplace\HS2VoiceReplace.csproj -c Release -p:AssetsToolsNetPath=C:\path\to\AssetsTools.NET.dll
```

## Notes

- Deployment targets only the HS2 folder explicitly selected by the user
- Review third-party download sources and license terms separately
