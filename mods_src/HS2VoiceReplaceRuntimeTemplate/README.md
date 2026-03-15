# HS2VoiceReplace Runtime Template

This directory contains the template files used when the tool generates runtime zipmods.

## Purpose

- Replace voice bundles for an existing personality ID
- Keep the original personality text and gameplay behavior unchanged
- Avoid direct edits to a live game installation

## Expected Payload Structure

Generated zipmods can contain bundle replacements such as:

- `abdata/sound/data/pcm/cXX/adv/30.unity3d`
- `abdata/sound/data/pcm/cXX/adv/50.unity3d`
- `abdata/sound/data/pcm/cXX/etc/30.unity3d`
- `abdata/sound/data/pcm/cXX/etc/50.unity3d`
- `abdata/sound/data/pcm/cXX/h/bre/30.unity3d`
- `abdata/sound/data/pcm/cXX/h/bre/50.unity3d`

The exact bundle number depends on the target personality and the source game data that is available. The tool prefers the personality-appropriate bundle file and falls back to the available source bundle when needed.

## Notes

- `cXX` represents the target personality directory
- The runtime plugin DLL itself is built from `runtime/HS2VoiceReplace.Runtime`
- This folder contains template input for packaging, not a finished mod release
