# HS2VoiceReplace

HS2VoiceReplace is a Windows GUI tool for creating and deploying Honey Select 2 voice-replacement packages.

It is built around a practical workflow: extract original in-game voice files, convert them with Seed-VC toward a supplied reference voice, rebuild game-ready replacements, and deploy them as zipmods plus a small runtime DLL.

The target is an existing personality such as `Composed` or `Obsessed`.
The tool does not add a new personality slot. It prepares replacement assets for an existing one.

The generated packages are meant to avoid directly editing base game files.
Deployment is done through `mods` and `BepInEx\plugins`, and the GUI supports per-personality deploy and undeploy.

Japanese documentation is available in `README_JA.md`.

## Prerequisites

- Windows environment that can run `HS2VoiceReplaceGui.exe`
- `.NET 8 Desktop Runtime`
- A Honey Select 2 installation that uses `mods` and `BepInEx\plugins`
- Reference voice clips for the voice you want to imitate

The GUI can set up most conversion-side dependencies as part of its workflow.

## Quick Start

Download the packaged zip from GitHub Releases and start from `HS2VoiceReplaceGui.exe`.

- `HS2VoiceReplaceGui.exe`
  - Main GUI application that coordinates the workflow
- generated `HS2_VoiceReplace.dll`
  - Runtime support DLL used by deployed voice-replacement packages
- generated `HS2VoiceReplace_cXX_*.zipmod`
  - Per-personality zipmods created by the GUI

1. Launch `HS2VoiceReplaceGui.exe`
2. Select the HS2 folder and the target personality
3. Provide reference voice clips and run dependency setup if needed
4. Run extraction, preview, and full conversion
5. Deploy the generated files from the GUI, or place them manually:
   - `HS2_VoiceReplace.dll` under `BepInEx\plugins`
   - `HS2VoiceReplace_cXX_*.zipmod` under `mods`

Example screen after extraction and conversion:

![HS2VoiceReplaceGui conversion grid](docs/images/gui-conversion-grid-en.png)

## Seed-VC Settings

- `v1`
  - Closer to the original line feel
  - Good default when replacing existing HS2 voices
- `v2`
  - More expressive
  - Better when you want a stronger shift toward the reference voice

Common settings:

- `DiffusionSteps`
  - Higher = slower but often cleaner
- `LengthAdjust`
  - Adjusts line length
- `IntelligibilityCfgRate`
  - Makes pronunciation clearer
- `SimilarityCfgRate`
  - Pulls harder toward the target voice
- `Temperature` / `TopP`
  - Controls how stable or varied the result feels

## Notes

- Generated working data defaults to a repository-local `.hs2voicereplace` folder when running from this repository
- The working-data root can be changed from the GUI basic settings dialog
- The runtime side is intentionally small, and the main orchestration stays in the GUI
- GitHub Actions artifacts provide `HS2VoiceReplaceGui.exe/.dll` and `UabAudioClipPatcher.exe/.dll`
- `HS2_VoiceReplace.dll` is built locally because it depends on a valid HS2 `GameRoot`

## For Developers

Development-only information is kept out of this README.

- source layout and build prerequisites:
  - `docs/DEVELOPMENT.md`
  - `docs/DEVELOPMENT_JA.md`
- automated tests:
  - `docs/TESTING.md`
  - `docs/TESTING_JA.md`
- tool-specific development notes:
  - `tools/HS2VoiceReplaceGui/README.md`
  - `tools/HS2VoiceReplaceGui/README_JA.md`
  - `tools/UabAudioClipPatcher/README.md`
  - `tools/UabAudioClipPatcher/README_JA.md`
  - `runtime/HS2VoiceReplace.Runtime/README.md`
  - `runtime/HS2VoiceReplace.Runtime/README_JA.md`
