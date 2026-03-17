# Maintainer Notes

This note is for routine repository maintenance after the initial public release.

## Release Flow

1. Commit and push the intended `main` state
2. Build the release bundle locally
3. Upload the release bundle to GitHub Releases

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\package_release.ps1 -GameRoot=C:\path\to\HoneySelect2 -Force
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\publish_github_release.ps1 -GameRoot=C:\path\to\HoneySelect2 -Tag=v1.0.0 -Force
```

`publish_github_release.ps1` now force-aligns the release tag to the current `HEAD` before uploading the asset.

## Current Release Policy

- GitHub Releases are for end-user downloads
- The packaged zip should include:
  - `HS2VoiceReplaceGui.exe/.dll`
  - `UabAudioClipPatcher.exe/.dll`
  - locally built `HS2_VoiceReplace.dll`
  - `mods_template/HS2VoiceReplaceRuntime`
- Dependency-install outputs such as `external_tools` are not vendored in the release zip
- zipmods are created by the user in their own environment

## When To Rebuild Locally

Local packaging is required whenever the release should include a new `HS2_VoiceReplace.dll`, because that build depends on a valid HS2 `GameRoot`.

## Repo Settings To Keep Enabled

- GitHub Actions CI
- Dependabot alerts
- Dependabot security updates
- Private vulnerability reporting

## Public Repo Housekeeping

- Keep `README.md`, `README_JA.md`, `LICENSE`, and `SECURITY.md` in sync with the actual workflow
- Prefer small, test-backed commits for GUI workflow changes
- Do not commit generated working data, dependency-install outputs, or machine-local paths

## UX Intent

- Keep the main workflow easy to follow:
  - dependency setup
  - extraction
  - sample audio selection
  - preview
  - full conversion
  - deploy
- Prefer plain task-oriented labels over developer-oriented terminology
- Keep destructive or irreversible actions visually separated from routine actions
- Prefer table views for inspection, and keep action buttons outside the table when possible
- Let enabled and disabled states explain what the user can do at the current moment
- Preserve in-progress context across restarts where practical:
  - selected run root
  - voice lines
  - signatures
  - column widths
- Favor layouts where important buttons are visible without guesswork; use internal scrolling when a dialog cannot reliably fit on screen
- Keep README text, UI wording, and actual behavior aligned whenever the workflow changes
