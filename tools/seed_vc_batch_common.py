#!/usr/bin/env python3
"""Shared helpers for repository-maintained Seed-VC batch wrappers."""

from __future__ import annotations

import os

import librosa
import numpy as np
import soundfile as sf

from python_cli_common import copy_binary_file, normalize_file_not_found_note, write_csv_report


SEED_VC_REPORT_FIELDS = [
    "relative_path",
    "bucket",
    "source_file",
    "style_file",
    "output_file",
    "status",
    "exit_code",
    "note",
]


def write_seed_vc_report(report_path: str, rows: list[dict[str, object]]) -> None:
    """Persist the standard Seed-VC per-file report."""
    write_csv_report(report_path, SEED_VC_REPORT_FIELDS, rows, encoding="utf-8-sig")


def build_failure_note(ex: Exception) -> str:
    """Format failures consistently across the v1/v2 wrappers."""
    if isinstance(ex, FileNotFoundError):
        return normalize_file_not_found_note(ex)
    return f"{type(ex).__name__}: {ex}"


def copy_source_fallback(src: str, out_file: str) -> None:
    """Fallback path that preserves the original source WAV for one item."""
    copy_binary_file(src, out_file)


def write_silence_fallback(src: str, out_file: str) -> None:
    """Fallback path that preserves timing while muting the failed item."""
    y_src, sr_src = librosa.load(src, sr=None, mono=True)
    y_zero = np.zeros_like(y_src, dtype=np.float32)
    os.makedirs(os.path.dirname(out_file), exist_ok=True)
    sf.write(out_file, y_zero, sr_src)
