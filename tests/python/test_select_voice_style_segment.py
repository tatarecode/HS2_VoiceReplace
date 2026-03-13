from __future__ import annotations

import sys
import unittest
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]
TOOLS_DIR = REPO_ROOT / "tools"
if str(TOOLS_DIR) not in sys.path:
    sys.path.insert(0, str(TOOLS_DIR))


class SelectVoiceStyleSegmentTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        try:
            global np, svs
            import numpy as np  # type: ignore
            import select_voice_style_segment as svs  # type: ignore
        except ModuleNotFoundError as ex:
            raise unittest.SkipTest(f"Optional style-selection test dependencies are missing: {ex}") from ex

    def test_norm01_returns_zero_array_for_flat_input(self) -> None:
        arr = np.array([5.0, 5.0, 5.0], dtype=np.float32)
        out = svs._norm01(arr)
        self.assertTrue(np.array_equal(out, np.zeros_like(arr)))

    def test_overlap_ratio_handles_partial_overlap(self) -> None:
        ratio = svs._overlap_ratio((0, 10), (5, 15))
        self.assertAlmostEqual(0.5, ratio)

    def test_pick_normal_ero_pair_prefers_non_overlapping_extremes(self) -> None:
        segs = [
            svs.SegmentResult(0.0, 2.0, 2.0, 0.90, 1.0, clarity_score=0.95, erotic_score=0.05, rank=1),
            svs.SegmentResult(2.5, 4.5, 2.0, 0.85, 0.9, clarity_score=0.80, erotic_score=0.30, rank=2),
            svs.SegmentResult(5.0, 7.0, 2.0, 0.75, 0.8, clarity_score=0.55, erotic_score=0.95, rank=3),
        ]

        normal, ero, ranked, order = svs.pick_normal_ero_pair(segs, erotic_order="high-is-erotic")

        self.assertEqual("high-is-erotic", order)
        self.assertIs(normal, segs[0])
        self.assertIs(ero, segs[2])
        self.assertEqual(segs[2], ranked[0])


if __name__ == "__main__":
    unittest.main()
