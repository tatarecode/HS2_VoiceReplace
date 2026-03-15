# ADV Voice-Line Extraction Handoff

This document describes a practical way to extract `hsa_*` voice lines and their paired dialogue text from `abdata/adv/scenario` bundles.

## Goal

The goal is not to fully interpret ADV branching logic.
The goal is to build a useful CSV mapping:

- `clip`
- `text`
- optional provenance fields such as bundle name and scenario block name

This is intended for UI display, lookup, and voice-line filling.

## Why this works

Based on the current investigation, many ADV scenario blocks store voice playback and text display in a nearby command sequence.

A common pattern is:

- `17` = play voice clip
- `16` = show dialogue text

In many cases, the relevant text is either:
- immediately after the `17` command, or
- after a few lightweight presentation commands

This means that a high-accuracy extractor can be built without fully simulating the ADV VM.

## Recommended extraction strategy

### Step 1: Load every scenario bundle for one personality

Target directory pattern:

```text
abdata/adv/scenario/cXX/<bundle-set>/
```

Examples:
- `abdata/adv/scenario/c02/30`
- `abdata/adv/scenario/c13/50`

### Step 2: Iterate all `ADV.ScenarioData` MonoBehaviours

For each `ScenarioData` block:
- read `m_Name`
- enumerate `list.Array`
- inspect each item in order

### Step 3: Detect voice commands

When `_command == 17`:
- read `_args`
- the last string argument is usually the clip name (`hsa_*`)
- only continue if the clip name starts with the target personality prefix such as `hsa_02_` or `hsa_13_`

### Step 4: Find the paired dialogue text

Look ahead a short distance, for example up to 6 to 8 items.

If `_command == 16` appears before another strong boundary, use that text.

A practical stopping rule is:
- stop if another `17` is reached
- stop if a new label block begins (`12`)
- stop if a choice block begins (`24`)

A practical tolerance rule is:
- allow small presentation/control commands between `17` and `16`

This already works well for many `022` and `024` families.

## Commands that can usually be ignored during pairing

These commands often appear between a voice command and the actual text, or as nearby presentation logic:

- `18`
- `19`
- `98`
- `104`
- `120`

You do not need to interpret them in the first implementation.
They can simply be skipped while searching for the next `16` text command.

## Suggested CSV schema

Recommended columns:

- `clip`
- `text`
- `bundle`
- `scenario_name`
- `item_index`
- `command_window`
- `source_bundle_path`

This keeps the extracted result debuggable.

## Suggested deduplicated CSV schema

For a second-stage CSV intended for application use:

- `clip`
- `text`
- `candidate_rows`
- `unique_texts`
- `sample_bundle`
- `sample_scenario`

This allows one row per clip while preserving ambiguity metadata.

## Current prototype result

A private-side prototype using this method on `c02` produced:

- `1208` raw rows
- `861` unique clips

The result quality was high enough to be useful for line filling.

## Known limitations

This method is intentionally heuristic.
It does not fully interpret ADV control flow.

Known limitations:
- some clips may appear multiple times
- some clips may have multiple candidate texts
- some branches may require deeper label resolution
- some lines may exist only in `list/h/sound/voice`
- a few matches may fail if the text is not near the voice command

## Practical recommendation

Use a two-layer fill strategy:

1. Load existing line mappings from `list/h/sound/voice`
2. Supplement missing ADV lines from `adv/scenario` using the `17 -> nearby 16` rule

This gives a strong balance between implementation cost and coverage.

## Future improvements

If better coverage is needed later, improve the extractor in this order:

1. allow more skip commands between `17` and `16`
2. keep multiple candidate texts per clip
3. resolve nearby labels and simple branch commands
4. only if necessary, interpret the ADV command VM more fully
