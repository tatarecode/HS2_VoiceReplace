# Development Notes

This document is intended for a new developer starting from this export only.

## Recommended First Steps

1. Read `README.md`
2. Read `tools/HS2VoiceReplace/README.md`
3. Run the automated tests
4. Verify local build prerequisites for the GUI and runtime plugin

## Project Boundaries

- The GUI project is the main product.
- The runtime plugin and AudioClip patcher are supporting components.
- Python scripts are operational dependencies of the GUI workflow and should be treated as part of the maintained codebase.

## Architectural Orientation

- UI code lives under `tools/HS2VoiceReplace`
- The codebase has already been split into:
  - UI partials
  - application services
  - pipeline helpers
  - pure utility classes

## Testing Strategy

- Prefer adding tests for pure helper logic before changing workflow orchestration.
- C# tests cover:
  - localization
  - signatures
  - freshness checks
  - report parsing
  - target resolution
  - grid data helpers
- Python tests cover:
  - shared CLI helpers
  - Seed-VC batch helpers
  - style-segment selection helpers

## Expected External Dependencies

- GUI and `UabAudioClipPatcher` source builds depend on a separately supplied `AssetsTools.NET.dll`
- This repository does not vendor `AssetsTools.NET.dll`
- Runtime plugin build depends on game-side Unity and BepInEx assemblies
- Runtime execution may download external tools during dependency setup

## Maintenance Guidance

- Keep user-facing text in `UiTextCatalog.cs`
- Keep new logic testable by extracting pure helpers where possible
- Avoid introducing machine-local paths or personal identifiers into source, templates, or generated defaults
