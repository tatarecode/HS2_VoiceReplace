from __future__ import annotations

import csv
import os
import shutil
import sys
import tempfile
import unittest
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[2]
TOOLS_DIR = REPO_ROOT / "tools"
if str(TOOLS_DIR) not in sys.path:
    sys.path.insert(0, str(TOOLS_DIR))

import python_cli_common as common


class PythonCliCommonTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temp_dir = Path(tempfile.mkdtemp(prefix="hs2vr_pycommon_"))

    def tearDown(self) -> None:
        shutil.rmtree(self.temp_dir, ignore_errors=True)

    def test_write_csv_report_and_read_manifest_rows_round_trip(self) -> None:
        report_path = self.temp_dir / "reports" / "sample.csv"
        rows = [{"name": "alpha", "value": 1}, {"name": "beta", "value": 2}]

        common.write_csv_report(str(report_path), ["name", "value"], rows)
        loaded = common.read_manifest_rows(str(report_path))

        self.assertEqual(
            [{"name": "alpha", "value": "1"}, {"name": "beta", "value": "2"}],
            loaded,
        )

    def test_normalize_file_not_found_note_uses_missing_filename(self) -> None:
        missing_path = self.temp_dir / "missing.bin"
        try:
            open(missing_path, "rb").close()
        except FileNotFoundError as ex:
            note = common.normalize_file_not_found_note(ex)
        else:
            self.fail("Expected FileNotFoundError")

        self.assertIn("FileNotFoundError", note)
        self.assertIn(str(missing_path), note)

    def test_copy_binary_file_copies_exact_bytes(self) -> None:
        src = self.temp_dir / "src.bin"
        dst = self.temp_dir / "nested" / "dst.bin"
        payload = b"\x00\x01\x02test"
        src.write_bytes(payload)

        common.copy_binary_file(str(src), str(dst))

        self.assertEqual(payload, dst.read_bytes())

    def test_list_wavs_returns_sorted_relative_unix_paths(self) -> None:
        wav_a = self.temp_dir / "b" / "clip2.wav"
        wav_b = self.temp_dir / "a" / "clip1.wav"
        wav_a.parent.mkdir(parents=True, exist_ok=True)
        wav_b.parent.mkdir(parents=True, exist_ok=True)
        wav_a.write_bytes(b"RIFF")
        wav_b.write_bytes(b"RIFF")

        listed = common.list_wavs(str(self.temp_dir))

        self.assertEqual(["a/clip1.wav", "b/clip2.wav"], [rel for rel, _ in listed])
        self.assertTrue(all(os.path.isabs(path) for _, path in listed))


if __name__ == "__main__":
    unittest.main()
