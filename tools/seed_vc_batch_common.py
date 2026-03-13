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


def match_output_loudness_to_source(
    y_out: np.ndarray,
    y_src: np.ndarray,
    *,
    activity_ratio: float = 0.05,
    silence_floor: float = 1.0e-4,
    min_gain: float = 0.25,
    max_gain: float = 4.0,
) -> np.ndarray:
    """Scale converted audio so its active-region RMS follows the source clip."""
    y_out_arr = np.asarray(y_out, dtype=np.float32)
    y_src_arr = np.asarray(y_src, dtype=np.float32)
    n = min(len(y_out_arr), len(y_src_arr))
    if n <= 0:
        return y_out_arr

    src_aligned = y_src_arr[:n]
    out_aligned = y_out_arr[:n]
    src_abs = np.abs(src_aligned)

    threshold = max(float(silence_floor), float(np.max(src_abs)) * float(activity_ratio))
    active_mask = src_abs >= threshold
    if not np.any(active_mask):
        active_mask = src_abs > float(silence_floor)
    if not np.any(active_mask):
        active_mask = np.ones(n, dtype=bool)

    src_rms = float(np.sqrt(np.mean(np.square(src_aligned[active_mask]), dtype=np.float64)))
    out_rms = float(np.sqrt(np.mean(np.square(out_aligned[active_mask]), dtype=np.float64)))
    if src_rms <= 1.0e-8 or out_rms <= 1.0e-8:
        return y_out_arr

    gain = float(np.clip(src_rms / out_rms, min_gain, max_gain))
    return (y_out_arr * gain).astype(np.float32)
