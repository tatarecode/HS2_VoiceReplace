from __future__ import annotations

import shutil
import sys
import tempfile
import unittest
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]
TOOLS_DIR = REPO_ROOT / "tools"
if str(TOOLS_DIR) not in sys.path:
    sys.path.insert(0, str(TOOLS_DIR))


class SeedVcBatchCommonTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        try:
            global np, sf, common
            import numpy as np  # type: ignore
            import soundfile as sf  # type: ignore
            import seed_vc_batch_common as common  # type: ignore
        except ModuleNotFoundError as ex:
            raise unittest.SkipTest(f"Optional audio test dependencies are missing: {ex}") from ex

    def setUp(self) -> None:
        self.temp_dir = Path(tempfile.mkdtemp(prefix="hs2vr_seedvc_common_"))

    def tearDown(self) -> None:
        shutil.rmtree(self.temp_dir, ignore_errors=True)

    def test_write_seed_vc_report_writes_expected_columns(self) -> None:
        report = self.temp_dir / "report.csv"
        common.write_seed_vc_report(
            str(report),
            [
                {
                    "relative_path": "adv/test.wav",
                    "bucket": "normal",
                    "source_file": "src.wav",
                    "style_file": "style.wav",
                    "output_file": "out.wav",
                    "status": "ok",
                    "exit_code": 0,
                    "note": "",
                }
            ],
        )

        text = report.read_text(encoding="utf-8-sig")
        self.assertIn("relative_path,bucket,source_file,style_file,output_file,status,exit_code,note", text)
        self.assertIn("adv/test.wav", text)

    def test_build_failure_note_formats_file_not_found_cleanly(self) -> None:
        ex = FileNotFoundError(2, "missing", "foo.wav")
        note = common.build_failure_note(ex)
        self.assertIn("FileNotFoundError", note)
        self.assertIn("foo.wav", note)

    def test_copy_source_fallback_preserves_source_bytes(self) -> None:
        src = self.temp_dir / "src.wav"
        dst = self.temp_dir / "out" / "dst.wav"
        payload = b"RIFFtestdata"
        src.write_bytes(payload)

        common.copy_source_fallback(str(src), str(dst))

        self.assertEqual(payload, dst.read_bytes())

    def test_write_silence_fallback_keeps_length_and_zeros_signal(self) -> None:
        src = self.temp_dir / "src.wav"
        dst = self.temp_dir / "out" / "dst.wav"
        sr = 22050
        t = np.linspace(0, 0.1, int(sr * 0.1), endpoint=False)
        y = 0.25 * np.sin(2 * np.pi * 440 * t)
        sf.write(src, y.astype(np.float32), sr)

        common.write_silence_fallback(str(src), str(dst))

        out, out_sr = sf.read(dst)
        self.assertEqual(sr, out_sr)
        self.assertEqual(len(y), len(out))
        self.assertTrue(np.allclose(out, 0.0, atol=1e-6))


if __name__ == "__main__":
    unittest.main()
