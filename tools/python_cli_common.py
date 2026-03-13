#!/usr/bin/env python3
"""Common helpers for small repository-maintained Python CLI utilities."""

from __future__ import annotations

import csv
import os
from pathlib import Path
from typing import Iterable


def ensure_parent_dir(path: str) -> None:
    """Create the parent directory for a target file path when it exists."""
    parent = os.path.dirname(path)
    if parent:
        os.makedirs(parent, exist_ok=True)


def read_manifest_rows(path: str) -> list[dict[str, str]]:
    """Load a UTF-8 CSV manifest into a list of dictionaries."""
    with open(path, "r", encoding="utf-8-sig", newline="") as handle:
        return list(csv.DictReader(handle))


def write_csv_report(
    path: str,
    fieldnames: list[str],
    rows: Iterable[dict[str, object]],
    *,
    encoding: str = "utf-8-sig",
) -> None:
    """Write a CSV report using an Excel-friendly encoding by default."""
    ensure_parent_dir(path)
    with open(path, "w", encoding=encoding, newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=fieldnames)
        writer.writeheader()
        writer.writerows(rows)


def normalize_file_not_found_note(ex: FileNotFoundError) -> str:
    """Create a stable, easy-to-read message for missing-file failures."""
    missing = getattr(ex, "filename", None)
    winerr = getattr(ex, "winerror", None)
    if missing and winerr:
        return f"FileNotFoundError: [WinError {winerr}] missing={missing}"
    if missing:
        return f"FileNotFoundError: missing={missing}"
    if winerr:
        return f"FileNotFoundError: [WinError {winerr}]"
    return "FileNotFoundError"


def copy_binary_file(src: str, dst: str) -> None:
    """Copy a binary file while ensuring the destination directory exists."""
    ensure_parent_dir(dst)
    with open(src, "rb") as fsrc, open(dst, "wb") as fdst:
        fdst.write(fsrc.read())


def list_wavs(root: str) -> list[tuple[str, str]]:
    """Return WAV files under *root* as (relative_unix_path, absolute_path)."""
    out: list[tuple[str, str]] = []
    for path in sorted(Path(root).rglob("*.wav")):
        rel = path.relative_to(root).as_posix()
        out.append((rel, str(path.resolve())))
    return out
