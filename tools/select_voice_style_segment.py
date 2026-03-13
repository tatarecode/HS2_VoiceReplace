#!/usr/bin/env python3
import argparse
import csv
import os
from dataclasses import dataclass
from typing import List, Tuple

import librosa
import numpy as np
import soundfile as sf


@dataclass
class SegmentResult:
    start_sec: float
    end_sec: float
    duration_sec: float
    voiced_ratio: float
    score: float
    clarity_score: float = 0.0
    erotic_score: float = 0.0
    rank: int = 1


def _norm01(x: np.ndarray) -> np.ndarray:
    x = np.asarray(x, dtype=np.float32)
    lo = float(np.nanmin(x))
    hi = float(np.nanmax(x))
    if not np.isfinite(lo) or not np.isfinite(hi) or hi <= lo:
        return np.zeros_like(x, dtype=np.float32)
    return (x - lo) / (hi - lo)


def _overlap_ratio(a: Tuple[int, int], b: Tuple[int, int]) -> float:
    s1, e1 = a
    s2, e2 = b
    inter = max(0, min(e1, e2) - max(s1, s2))
    if inter <= 0:
        return 0.0
    den = float(min(e1 - s1, e2 - s2))
    return inter / den if den > 0 else 0.0


def _overlap_ratio_sec(a: SegmentResult, b: SegmentResult) -> float:
    inter = max(0.0, min(a.end_sec, b.end_sec) - max(a.start_sec, b.start_sec))
    if inter <= 0.0:
        return 0.0
    den = min(max(1e-6, a.duration_sec), max(1e-6, b.duration_sec))
    return float(inter / den)


def _ensure_parent(path: str) -> None:
    parent = os.path.dirname(path)
    if parent:
        os.makedirs(parent, exist_ok=True)


def _compute_features(y: np.ndarray, sr: int):
    frame_length = 2048
    hop_length = 256

    rms = librosa.feature.rms(y=y, frame_length=frame_length, hop_length=hop_length)[0]
    zcr = librosa.feature.zero_crossing_rate(y, frame_length=frame_length, hop_length=hop_length)[0]
    flatness = librosa.feature.spectral_flatness(y=y, n_fft=frame_length, hop_length=hop_length)[0]
    centroid = librosa.feature.spectral_centroid(y=y, sr=sr, n_fft=frame_length, hop_length=hop_length)[0]
    rolloff = librosa.feature.spectral_rolloff(y=y, sr=sr, n_fft=frame_length, hop_length=hop_length)[0]
    contrast = librosa.feature.spectral_contrast(y=y, sr=sr, n_fft=frame_length, hop_length=hop_length)
    onset = librosa.onset.onset_strength(y=y, sr=sr, hop_length=hop_length)

    n = min(
        len(rms),
        len(zcr),
        len(flatness),
        len(centroid),
        len(rolloff),
        contrast.shape[1],
        len(onset),
    )
    rms = rms[:n]
    zcr = zcr[:n]
    flatness = flatness[:n]
    centroid = centroid[:n]
    rolloff = rolloff[:n]
    contrast_mean = np.mean(contrast[:, :n], axis=0)
    onset = onset[:n]

    rms_thr = float(np.percentile(rms, 35))
    voiced = (rms > rms_thr) & (zcr > 0.01) & (zcr < 0.24) & (flatness < 0.50)

    rms_n = _norm01(rms)
    zcr_n = _norm01(zcr)
    flat_n = _norm01(flatness)
    cent_n = _norm01(centroid)
    roll_n = _norm01(rolloff)
    contrast_n = _norm01(contrast_mean)
    onset_n = _norm01(onset)
    cent_local = np.abs(cent_n - np.convolve(cent_n, np.ones(17) / 17.0, mode="same"))

    # Higher means clearer articulation.
    clarity = (
        0.45 * onset_n
        + 0.35 * zcr_n
        + 0.35 * contrast_n
        + 0.25 * cent_local
        + 0.20 * roll_n
    )
    # Higher means more breathy/sensual tendency.
    eroticness = (
        0.40 * flat_n
        + 0.25 * (1.0 - zcr_n)
        + 0.20 * (1.0 - onset_n)
        + 0.10 * rms_n
        + 0.05 * (1.0 - roll_n)
    )
    erotic_penalty = (0.55 * flat_n) + (0.15 * (1.0 - zcr_n))

    return {
        "hop_length": hop_length,
        "n": n,
        "voiced": voiced,
        "rms_n": rms_n,
        "flat_n": flat_n,
        "cent_local": cent_local,
        "clarity": clarity,
        "eroticness": eroticness,
        "erotic_penalty": erotic_penalty,
    }


def pick_segment(
    y: np.ndarray,
    sr: int,
    target_sec: float = 18.0,
    min_sec: float = 8.0,
    pad_sec: float = 0.15,
    speech_clarity_bias: float = 0.0,
) -> SegmentResult:
    feat = _compute_features(y, sr)
    hop_length = feat["hop_length"]
    n = feat["n"]
    voiced = feat["voiced"]
    rms_n = feat["rms_n"]
    flat_n = feat["flat_n"]
    cent_local = feat["cent_local"]
    clarity = feat["clarity"]
    eroticness = feat["eroticness"]
    erotic_penalty = feat["erotic_penalty"]

    frame_score = (1.6 * rms_n) + (0.9 * (1.0 - flat_n)) + (0.5 * cent_local) + (speech_clarity_bias * clarity)
    frame_score = frame_score * voiced.astype(np.float32)

    win = max(1, int(round(target_sec * sr / hop_length)))
    min_win = max(1, int(round(min_sec * sr / hop_length)))

    if n <= win:
        voiced_ratio = float(np.mean(voiced)) if n > 0 else 0.0
        return SegmentResult(
            start_sec=0.0,
            end_sec=float(len(y) / sr),
            duration_sec=float(len(y) / sr),
            voiced_ratio=voiced_ratio,
            score=float(np.sum(frame_score)),
            clarity_score=float(np.mean(clarity)) if n > 0 else 0.0,
            erotic_score=float(np.mean(eroticness)) if n > 0 else 0.0,
        )

    csum = np.concatenate([[0.0], np.cumsum(frame_score)])
    vsum = np.concatenate([[0], np.cumsum(voiced.astype(np.int32))])

    best = None
    for s in range(0, n - win + 1):
        e = s + win
        score = float(csum[e] - csum[s])
        vratio = float(vsum[e] - vsum[s]) / float(win)
        if vratio < 0.60:
            continue
        if best is None or score > best[0]:
            best = (score, s, e, vratio)

    if best is None:
        for cur_win in range(win, min_win - 1, -max(1, win // 8)):
            for s in range(0, n - cur_win + 1):
                e = s + cur_win
                score = float(csum[e] - csum[s])
                vratio = float(vsum[e] - vsum[s]) / float(cur_win)
                if best is None or (vratio >= 0.45 and score > best[0]):
                    best = (score, s, e, vratio)
            if best is not None:
                break

    if best is None:
        return SegmentResult(
            start_sec=0.0,
            end_sec=float(len(y) / sr),
            duration_sec=float(len(y) / sr),
            voiced_ratio=float(np.mean(voiced)) if n > 0 else 0.0,
            score=float(np.sum(frame_score)),
            clarity_score=float(np.mean(clarity)) if n > 0 else 0.0,
            erotic_score=float(np.mean(eroticness)) if n > 0 else 0.0,
        )

    _, s, e, vratio = best
    start = max(0, int(s * hop_length - (pad_sec * sr)))
    end = min(len(y), int(e * hop_length + (pad_sec * sr)))
    return SegmentResult(
        start_sec=float(start / sr),
        end_sec=float(end / sr),
        duration_sec=float((end - start) / sr),
        voiced_ratio=float(vratio),
        score=float(best[0]),
        clarity_score=float(np.mean(clarity[s:e])) if e > s else 0.0,
        erotic_score=float(np.mean(eroticness[s:e])) if e > s else 0.0,
    )


def pick_top_segments(
    y: np.ndarray,
    sr: int,
    target_sec: float = 10.0,
    min_sec: float = 8.0,
    pad_sec: float = 0.15,
    speech_clarity_bias: float = 1.0,
    top_k: int = 5,
    max_overlap_ratio: float = 0.35,
) -> List[SegmentResult]:
    feat = _compute_features(y, sr)
    hop_length = feat["hop_length"]
    n = feat["n"]
    voiced = feat["voiced"]
    rms_n = feat["rms_n"]
    flat_n = feat["flat_n"]
    cent_local = feat["cent_local"]
    clarity = feat["clarity"]
    eroticness = feat["eroticness"]
    erotic_penalty = feat["erotic_penalty"]

    frame_score = (
        (1.2 * rms_n)
        + (1.0 * (1.0 - flat_n))
        + (speech_clarity_bias * clarity)
        - (0.60 * erotic_penalty)
    )
    frame_score = frame_score * voiced.astype(np.float32)

    win = max(1, int(round(target_sec * sr / hop_length)))
    min_win = max(1, int(round(min_sec * sr / hop_length)))
    if n <= min_win:
        seg = pick_segment(
            y=y,
            sr=sr,
            target_sec=target_sec,
            min_sec=min_sec,
            pad_sec=pad_sec,
            speech_clarity_bias=speech_clarity_bias,
        )
        return [seg]

    csum = np.concatenate([[0.0], np.cumsum(frame_score)])
    vsum = np.concatenate([[0], np.cumsum(voiced.astype(np.int32))])

    candidates = []
    for cur_win in (win, max(min_win, int(round(win * 0.9))), max(min_win, int(round(win * 0.8)))):
        for s in range(0, n - cur_win + 1):
            e = s + cur_win
            score = float(csum[e] - csum[s])
            vratio = float(vsum[e] - vsum[s]) / float(cur_win)
            if vratio < 0.62:
                continue
            clarity_avg = float(np.mean(clarity[s:e])) if e > s else 0.0
            erotic_avg = float(np.mean(eroticness[s:e])) if e > s else 0.0
            candidates.append((score, s, e, vratio, clarity_avg, erotic_avg))

    if not candidates:
        seg = pick_segment(
            y=y,
            sr=sr,
            target_sec=target_sec,
            min_sec=min_sec,
            pad_sec=pad_sec,
            speech_clarity_bias=speech_clarity_bias,
        )
        return [seg]

    candidates.sort(key=lambda x: x[0], reverse=True)
    picked = []
    for c in candidates:
        _, s, e, _, _, _ = c
        rng = (s, e)
        if any(_overlap_ratio(rng, (ps, pe)) > max_overlap_ratio for _, ps, pe, _, _, _ in picked):
            continue
        picked.append(c)
        if len(picked) >= top_k:
            break

    results: List[SegmentResult] = []
    for rank, (score, s, e, vratio, clarity_avg, erotic_avg) in enumerate(picked, start=1):
        start = max(0, int(s * hop_length - (pad_sec * sr)))
        end = min(len(y), int(e * hop_length + (pad_sec * sr)))
        results.append(
            SegmentResult(
                start_sec=float(start / sr),
                end_sec=float(end / sr),
                duration_sec=float((end - start) / sr),
                voiced_ratio=float(vratio),
                score=float(score),
                clarity_score=float(clarity_avg),
                erotic_score=float(erotic_avg),
                rank=rank,
            )
        )
    return results


def build_pair_candidate_segments(
    y: np.ndarray,
    sr: int,
    target_sec: float,
    min_sec: float,
    pad_sec: float = 0.15,
    max_candidates: int = 120,
) -> List[SegmentResult]:
    feat = _compute_features(y, sr)
    hop_length = feat["hop_length"]
    n = feat["n"]
    voiced = feat["voiced"]
    clarity = feat["clarity"]
    eroticness = feat["eroticness"]
    rms_n = feat["rms_n"]

    if n <= 0:
        return []

    base_win = max(1, int(round(target_sec * sr / hop_length)))
    min_win = max(1, int(round(min_sec * sr / hop_length)))
    win_list = sorted(
        {
            base_win,
            max(min_win, int(round(base_win * 0.8))),
            max(min_win, int(round(base_win * 1.2))),
        }
    )

    vsum = np.concatenate([[0], np.cumsum(voiced.astype(np.int32))])
    csum = np.concatenate([[0.0], np.cumsum(clarity)])
    esum = np.concatenate([[0.0], np.cumsum(eroticness)])
    rsum = np.concatenate([[0.0], np.cumsum(rms_n)])

    raw: List[SegmentResult] = []
    for cur_win in win_list:
        if cur_win >= n:
            continue
        step = max(1, cur_win // 4)
        for s in range(0, n - cur_win + 1, step):
            e = s + cur_win
            vratio = float(vsum[e] - vsum[s]) / float(cur_win)
            if vratio < 0.45:
                continue
            cavg = float(csum[e] - csum[s]) / float(cur_win)
            eavg = float(esum[e] - esum[s]) / float(cur_win)
            ravg = float(rsum[e] - rsum[s]) / float(cur_win)

            # Candidate quality for pairing pool: keep audible/articulated, but do not bias toward erotic.
            quality = (1.00 * cavg) + (0.70 * vratio) + (0.25 * ravg)
            start = max(0, int(s * hop_length - (pad_sec * sr)))
            end = min(len(y), int(e * hop_length + (pad_sec * sr)))
            raw.append(
                SegmentResult(
                    start_sec=float(start / sr),
                    end_sec=float(end / sr),
                    duration_sec=float((end - start) / sr),
                    voiced_ratio=vratio,
                    score=quality,
                    clarity_score=cavg,
                    erotic_score=eavg,
                    rank=0,
                )
            )

    if not raw:
        return []

    # Remove near-duplicate windows; keep higher-quality representative.
    raw.sort(key=lambda x: x.score, reverse=True)
    picked: List[SegmentResult] = []
    for c in raw:
        if any(_overlap_ratio_sec(c, p) > 0.85 for p in picked):
            continue
        picked.append(c)
        if len(picked) >= max_candidates:
            break

    # Deterministic ranking index by quality.
    for i, c in enumerate(picked, start=1):
        c.rank = i
    return picked


def pick_normal_ero_pair(
    segs: List[SegmentResult],
    max_overlap_ratio: float = 0.25,
    erotic_order: str = "auto",
) -> Tuple[SegmentResult, SegmentResult, List[SegmentResult], str]:
    if not segs:
        raise RuntimeError("no segments to rank")
    asc = sorted(segs, key=lambda s: s.erotic_score)
    if len(asc) == 1:
        return asc[0], asc[0], asc, "high-is-erotic"

    def infer_low_is_erotic() -> bool:
        # If low-score side is less articulate than high-score side, low is likely erotic.
        k = max(2, len(asc) // 5)
        low = asc[:k]
        high = asc[-k:]
        low_cl = float(np.mean([s.clarity_score for s in low]))
        high_cl = float(np.mean([s.clarity_score for s in high]))
        low_vo = float(np.mean([s.voiced_ratio for s in low]))
        high_vo = float(np.mean([s.voiced_ratio for s in high]))
        votes = 0
        if low_cl + 0.008 < high_cl:
            votes += 1
        if low_vo + 0.008 < high_vo:
            votes += 1
        return votes >= 1

    if erotic_order == "low-is-erotic":
        low_is_erotic = True
    elif erotic_order == "high-is-erotic":
        low_is_erotic = False
    else:
        low_is_erotic = infer_low_is_erotic()

    # erotic_ranked: rank1 = most erotic
    erotic_ranked = asc if low_is_erotic else list(reversed(asc))

    def effective_erotic(s: SegmentResult) -> float:
        return (-s.erotic_score) if low_is_erotic else s.erotic_score

    def effective_plain(s: SegmentResult) -> float:
        return -effective_erotic(s)

    # pick ero from most-erotic side, avoid exact overlap with normal later
    ero = erotic_ranked[0]

    # normal candidates from least-erotic side
    tail_n = max(3, len(erotic_ranked) // 4)
    plain_side = erotic_ranked[-tail_n:]
    plain_side = [s for s in plain_side if s.voiced_ratio >= 0.55] or plain_side

    def normal_obj(s: SegmentResult) -> float:
        return (2.20 * effective_plain(s)) + (1.20 * s.clarity_score) + (0.70 * s.voiced_ratio)

    normal = max(plain_side, key=normal_obj)

    # if overlap too high, choose next erotic candidate with minimal overlap
    if _overlap_ratio_sec(normal, ero) > max_overlap_ratio:
        for cand in erotic_ranked[1:]:
            if _overlap_ratio_sec(normal, cand) <= max_overlap_ratio:
                ero = cand
                break

    order_label = "low-is-erotic" if low_is_erotic else "high-is-erotic"
    return normal, ero, erotic_ranked, order_label


def run(
    input_path: str,
    output_path: str,
    report_path: str,
    target_sec: float,
    min_sec: float,
    speech_clarity_bias: float,
    top_k: int,
    export_candidates_dir: str,
) -> None:
    y, sr = librosa.load(input_path, sr=22050, mono=True)
    y, _ = librosa.effects.trim(y, top_db=35)
    if len(y) == 0:
        raise RuntimeError(f"audio appears silent: {input_path}")

    segs = pick_top_segments(
        y=y,
        sr=sr,
        target_sec=target_sec,
        min_sec=min_sec,
        speech_clarity_bias=speech_clarity_bias,
        top_k=max(1, top_k),
    )
    seg = segs[0]
    s = int(round(seg.start_sec * sr))
    e = int(round(seg.end_sec * sr))
    y_out = y[s:e]
    _ensure_parent(output_path)
    sf.write(output_path, y_out, sr)

    if export_candidates_dir:
        os.makedirs(export_candidates_dir, exist_ok=True)
        stem, _ = os.path.splitext(os.path.basename(output_path))
        for cand in segs:
            cs = int(round(cand.start_sec * sr))
            ce = int(round(cand.end_sec * sr))
            cw = y[cs:ce]
            out_cand = os.path.join(export_candidates_dir, f"{stem}_rank{cand.rank:02d}.wav")
            sf.write(out_cand, cw, sr)

    if report_path:
        _ensure_parent(report_path)
        with open(report_path, "w", encoding="utf-8", newline="") as f:
            w = csv.DictWriter(
                f,
                fieldnames=[
                    "rank",
                    "input",
                    "output",
                    "sr",
                    "start_sec",
                    "end_sec",
                    "duration_sec",
                    "voiced_ratio",
                    "score",
                    "clarity_score",
                    "erotic_score",
                ],
            )
            w.writeheader()
            for cand in segs:
                out_path = os.path.abspath(output_path)
                if export_candidates_dir:
                    stem, _ = os.path.splitext(os.path.basename(output_path))
                    out_path = os.path.abspath(
                        os.path.join(export_candidates_dir, f"{stem}_rank{cand.rank:02d}.wav")
                    )
                w.writerow(
                    {
                        "rank": cand.rank,
                        "input": os.path.abspath(input_path),
                        "output": out_path,
                        "sr": sr,
                        "start_sec": f"{cand.start_sec:.3f}",
                        "end_sec": f"{cand.end_sec:.3f}",
                        "duration_sec": f"{cand.duration_sec:.3f}",
                        "voiced_ratio": f"{cand.voiced_ratio:.4f}",
                        "score": f"{cand.score:.4f}",
                        "clarity_score": f"{cand.clarity_score:.4f}",
                        "erotic_score": f"{cand.erotic_score:.4f}",
                    }
                )

    print(f"selected: {output_path}")
    print(
        f"segment: {seg.start_sec:.2f}s - {seg.end_sec:.2f}s "
        f"(dur={seg.duration_sec:.2f}s voiced={seg.voiced_ratio:.3f} "
        f"clarity={seg.clarity_score:.3f} erotic={seg.erotic_score:.3f})"
    )


def run_pair(
    input_path: str,
    normal_output_path: str,
    ero_output_path: str,
    report_path: str,
    target_sec: float,
    min_sec: float,
    speech_clarity_bias: float,
    top_k: int,
    export_candidates_dir: str,
    pair_max_overlap_ratio: float,
    erotic_order: str,
) -> None:
    y, sr = librosa.load(input_path, sr=22050, mono=True)
    y, _ = librosa.effects.trim(y, top_db=35)
    if len(y) == 0:
        raise RuntimeError(f"audio appears silent: {input_path}")

    segs = build_pair_candidate_segments(
        y=y,
        sr=sr,
        target_sec=target_sec,
        min_sec=min_sec,
        max_candidates=max(120, top_k * 5),
    )
    if not segs:
        segs = pick_top_segments(
            y=y,
            sr=sr,
            target_sec=target_sec,
            min_sec=min_sec,
            speech_clarity_bias=speech_clarity_bias,
            top_k=max(2, top_k),
        )
    else:
        # Keep broad candidate pool for reliable low/hi erotic split.
        segs = sorted(segs, key=lambda s: s.score, reverse=True)

    normal_seg, ero_seg, erotic_ranked, order_label = pick_normal_ero_pair(
        segs,
        max_overlap_ratio=pair_max_overlap_ratio,
        erotic_order=erotic_order,
    )

    def _write(seg: SegmentResult, out_path: str):
        s = int(round(seg.start_sec * sr))
        e = int(round(seg.end_sec * sr))
        y_out = y[s:e]
        _ensure_parent(out_path)
        sf.write(out_path, y_out, sr)

    _write(normal_seg, normal_output_path)
    _write(ero_seg, ero_output_path)

    if export_candidates_dir:
        os.makedirs(export_candidates_dir, exist_ok=True)
        for idx, cand in enumerate(erotic_ranked, start=1):
            cs = int(round(cand.start_sec * sr))
            ce = int(round(cand.end_sec * sr))
            cw = y[cs:ce]
            out_cand = os.path.join(export_candidates_dir, f"candidate_erotic_rank{idx:02d}.wav")
            sf.write(out_cand, cw, sr)

    if report_path:
        _ensure_parent(report_path)
        with open(report_path, "w", encoding="utf-8", newline="") as f:
            w = csv.DictWriter(
                f,
                fieldnames=[
                    "erotic_rank",
                    "erotic_order",
                    "role",
                    "input",
                    "output",
                    "sr",
                    "start_sec",
                    "end_sec",
                    "duration_sec",
                    "voiced_ratio",
                    "score",
                    "clarity_score",
                    "erotic_score",
                ],
            )
            w.writeheader()
            for idx, cand in enumerate(erotic_ranked, start=1):
                role = ""
                out_path = ""
                if cand is normal_seg:
                    role = "normal"
                    out_path = os.path.abspath(normal_output_path)
                elif cand is ero_seg:
                    role = "ero"
                    out_path = os.path.abspath(ero_output_path)
                elif export_candidates_dir:
                    out_path = os.path.abspath(
                        os.path.join(export_candidates_dir, f"candidate_erotic_rank{idx:02d}.wav")
                    )
                w.writerow(
                    {
                        "erotic_rank": idx,
                        "erotic_order": order_label,
                        "role": role,
                        "input": os.path.abspath(input_path),
                        "output": out_path,
                        "sr": sr,
                        "start_sec": f"{cand.start_sec:.3f}",
                        "end_sec": f"{cand.end_sec:.3f}",
                        "duration_sec": f"{cand.duration_sec:.3f}",
                        "voiced_ratio": f"{cand.voiced_ratio:.4f}",
                        "score": f"{cand.score:.4f}",
                        "clarity_score": f"{cand.clarity_score:.4f}",
                        "erotic_score": f"{cand.erotic_score:.4f}",
                    }
                )

    print(f"normal selected: {normal_output_path}")
    print(f"ero selected: {ero_output_path}")
    print(
        f"erotic_order={order_label} "
        f"normal erotic={normal_seg.erotic_score:.4f}, "
        f"ero erotic={ero_seg.erotic_score:.4f}, "
        f"overlap={_overlap_ratio_sec(normal_seg, ero_seg):.3f}"
    )


def parse_args() -> argparse.Namespace:
    p = argparse.ArgumentParser(
        description="Pick style segments from a long sample and optionally auto-pair normal/ero."
    )
    p.add_argument("--input", required=True, help="Input audio path (wav/mp3/etc)")
    p.add_argument("--output", default="", help="Output wav path (single mode)")
    p.add_argument("--normal-output", default="", help="Normal output wav path (pair mode)")
    p.add_argument("--ero-output", default="", help="Ero output wav path (pair mode)")
    p.add_argument("--report", default="", help="Optional CSV report path")
    p.add_argument("--target-sec", type=float, default=18.0, help="Target segment duration in seconds")
    p.add_argument("--min-sec", type=float, default=8.0, help="Minimum duration fallback in seconds")
    p.add_argument(
        "--speech-clarity-bias",
        type=float,
        default=0.0,
        help="0=neutral, 0.5~1.5=prefer clear articulation speech",
    )
    p.add_argument("--top-k", type=int, default=1, help="Number of non-overlapping candidates to select")
    p.add_argument(
        "--export-candidates-dir",
        default="",
        help="If set, export top-k candidate wav files into this directory",
    )
    p.add_argument(
        "--pair-max-overlap-ratio",
        type=float,
        default=0.25,
        help="Max overlap ratio allowed between selected normal/ero in pair mode",
    )
    p.add_argument(
        "--erotic-order",
        choices=("auto", "low-is-erotic", "high-is-erotic"),
        default="auto",
        help="Interpretation of erotic_score direction in pair mode.",
    )
    return p.parse_args()


if __name__ == "__main__":
    a = parse_args()
    pair_mode = bool(a.normal_output or a.ero_output)
    if pair_mode:
        if not a.normal_output or not a.ero_output:
            raise SystemExit("--normal-output and --ero-output must be set together")
        run_pair(
            a.input,
            a.normal_output,
            a.ero_output,
            a.report,
            a.target_sec,
            a.min_sec,
            a.speech_clarity_bias,
            a.top_k,
            a.export_candidates_dir,
            a.pair_max_overlap_ratio,
            a.erotic_order,
        )
    else:
        if not a.output:
            raise SystemExit("--output is required in single mode")
        run(
            a.input,
            a.output,
            a.report,
            a.target_sec,
            a.min_sec,
            a.speech_clarity_bias,
            a.top_k,
            a.export_candidates_dir,
        )
