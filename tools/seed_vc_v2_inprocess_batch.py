#!/usr/bin/env python3
import argparse
import os
import sys
import time
import warnings
import io
import contextlib
import tempfile
import shutil
from types import SimpleNamespace

import librosa
import numpy as np
import soundfile as sf
import torch
from scipy.signal import butter, sosfiltfilt

# Embedded Python with python._pth may not include the script directory on sys.path.
SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
if SCRIPT_DIR not in sys.path:
    sys.path.insert(0, SCRIPT_DIR)

from python_cli_common import read_manifest_rows
from seed_vc_batch_common import (
    build_failure_note,
    copy_source_fallback,
    match_output_loudness_to_source,
    write_seed_vc_report,
    write_silence_fallback,
)

# Force UTF-8 output on Windows (avoids mojibake when GUI captures stdout/stderr).
os.environ.setdefault("PYTHONUTF8", "1")
os.environ.setdefault("PYTHONIOENCODING", "utf-8")

warnings.filterwarnings("ignore", category=UserWarning)
warnings.filterwarnings("ignore", category=FutureWarning)
warnings.filterwarnings(
    "ignore",
    category=RuntimeWarning,
    message=r".*Couldn't find ffmpeg or avconv.*",
)
os.environ["PYTHONWARNINGS"] = "ignore::UserWarning,ignore::FutureWarning"
os.environ["TQDM_DISABLE"] = "1"

try:
    import noisereduce as nr
except Exception:  # noqa: BLE001
    nr = None


def parse_args() -> argparse.Namespace:
    p = argparse.ArgumentParser(description="Seed-VC v2 in-process batch converter (single model load).")
    p.add_argument("--seed-root", required=True, help="Path to _tools/seed_vc_v2")
    p.add_argument("--manifest", required=True, help="routing_manifest.csv")
    p.add_argument("--style-normal", required=True, help="normal style wav")
    p.add_argument("--style-ero", required=True, help="ero style wav")
    p.add_argument("--out-root", required=True, help="output root")
    p.add_argument("--report", required=True, help="report csv path")
    p.add_argument("--diffusion-steps", type=int, default=16)
    p.add_argument("--length-adjust", type=float, default=1.0)
    p.add_argument("--intelligibility-cfg-rate", type=float, default=0.7)
    p.add_argument("--similarity-cfg-rate", type=float, default=0.7)
    p.add_argument("--top-p", type=float, default=0.9)
    p.add_argument("--temperature", type=float, default=1.0)
    p.add_argument("--repetition-penalty", type=float, default=1.0)
    p.add_argument("--convert-style", action="store_true")
    p.add_argument("--anonymization-only", action="store_true")
    p.add_argument("--compile", action="store_true")
    p.add_argument("--ar-checkpoint-path", default=None)
    p.add_argument("--cfm-checkpoint-path", default=None)
    p.add_argument("--max-files", type=int, default=0, help="for smoke test/debug")
    p.add_argument("--harsh-fix", action="store_true", help="Apply per-file harsh HF fix using source wav.")
    p.add_argument("--harsh-hf-cutoff", type=float, default=4000.0)
    p.add_argument("--harsh-src-hf-mix", type=float, default=0.65)
    p.add_argument("--harsh-over-factor", type=float, default=1.55)
    p.add_argument("--harsh-flatness-th", type=float, default=0.34)
    p.add_argument("--harsh-min-segment-ms", type=float, default=18.0)
    p.add_argument("--breath-pass-through", action="store_true", help="Use source audio for breath-like harsh segments.")
    p.add_argument("--breath-flatness-th", type=float, default=0.42)
    p.add_argument("--breath-rms-max", type=float, default=0.22)
    p.add_argument("--breath-mix", type=float, default=1.0, help="0..1 source mix for breath-like segments")
    p.add_argument("--nr-style-pre", action="store_true", help="Apply light spectral gating to style refs before VC.")
    p.add_argument("--nr-out-post", action="store_true", help="Apply light spectral gating to converted output.")
    p.add_argument("--nr-style-prop-decrease", type=float, default=0.6)
    p.add_argument("--nr-out-prop-decrease", type=float, default=0.5)
    p.add_argument("--nr-time-mask-smooth-ms", type=float, default=40.0)
    p.add_argument("--nr-freq-mask-smooth-hz", type=float, default=200.0)
    p.add_argument("--global-hf-blend", action="store_true", help="Apply uniform high-frequency source blend on all files.")
    p.add_argument("--global-hf-cutoff", type=float, default=3500.0)
    p.add_argument("--global-hf-src-mix", type=float, default=0.25, help="0..1 source HF mix ratio")
    p.add_argument("--global-deesser", action="store_true", help="Apply uniform light de-esser on all files.")
    p.add_argument("--deesser-low-hz", type=float, default=6000.0)
    p.add_argument("--deesser-high-hz", type=float, default=10000.0)
    p.add_argument("--deesser-strength", type=float, default=0.25, help="0..1 band reduction strength")
    p.add_argument("--prefer-fp32", action="store_true", help="Force float32 inference (stability over speed).")
    p.add_argument(
        "--on-error",
        choices=("fail", "copy-source", "silence"),
        default="copy-source",
        help="Per-file failure handling. copy-source keeps pipeline progressing.",
    )
    p.add_argument("--report-every", type=int, default=1, help="Flush report every N files.")
    p.add_argument("--progress-offset", type=int, default=0, help="Display progress index offset for chunked execution.")
    p.add_argument("--progress-total", type=int, default=0, help="Display progress total for chunked execution.")
    p.add_argument("--disable-style-cache", action="store_true", help="Disable style feature cache.")
    return p.parse_args()


def _apply_noisereduce_light(
    y: np.ndarray,
    sr: int,
    prop_decrease: float,
    time_mask_smooth_ms: float,
    freq_mask_smooth_hz: float,
) -> np.ndarray:
    if nr is None:
        return y
    y = np.asarray(y, dtype=np.float32)
    if y.size == 0:
        return y
    try:
        out = nr.reduce_noise(
            y=y,
            sr=sr,
            stationary=False,
            prop_decrease=float(np.clip(prop_decrease, 0.0, 1.0)),
            time_mask_smooth_ms=float(max(0.0, time_mask_smooth_ms)),
            freq_mask_smooth_hz=float(max(0.0, freq_mask_smooth_hz)),
            n_fft=1024,
            win_length=1024,
            hop_length=256,
            n_std_thresh_stationary=1.2,
        )
        return np.asarray(out, dtype=np.float32)
    except Exception:  # noqa: BLE001
        return y


def _sample_mask_from_frame_mask(frame_mask: np.ndarray, n_samples: int, hop: int) -> np.ndarray:
    s_mask = np.zeros(n_samples, dtype=np.float32)
    idx = np.where(frame_mask > 0)[0]
    for i in idx:
        s = i * hop
        e = min(n_samples, s + hop)
        if s < e:
            s_mask[s:e] = 1.0
    return s_mask


def _morph_min_len(mask: np.ndarray, min_len: int) -> np.ndarray:
    if min_len <= 1:
        return mask
    out = mask.copy()
    n = len(mask)
    i = 0
    while i < n:
        if out[i] == 0:
            i += 1
            continue
        j = i + 1
        while j < n and out[j] == 1:
            j += 1
        if (j - i) < min_len:
            out[i:j] = 0
        i = j
    return out


def _apply_harsh_fix(
    y_conv: np.ndarray,
    y_src: np.ndarray,
    sr: int,
    hf_cutoff: float,
    src_hf_mix: float,
    over_factor: float,
    flatness_th: float,
    min_segment_ms: float,
    breath_pass_through: bool,
    breath_flatness_th: float,
    breath_rms_max: float,
    breath_mix: float,
):
    n = min(len(y_conv), len(y_src))
    if n <= 0:
        return y_conv
    y_conv = y_conv[:n].astype(np.float32)
    y_src = y_src[:n].astype(np.float32)

    n_fft = 1024
    hop = 256
    S_conv = np.abs(librosa.stft(y_conv, n_fft=n_fft, hop_length=hop))
    S_src = np.abs(librosa.stft(y_src, n_fft=n_fft, hop_length=hop))
    freqs = librosa.fft_frequencies(sr=sr, n_fft=n_fft)
    hf_idx = freqs >= hf_cutoff
    hf_conv = np.mean(S_conv[hf_idx, :], axis=0) + 1e-8
    hf_src = np.mean(S_src[hf_idx, :], axis=0) + 1e-8
    flatness = librosa.feature.spectral_flatness(S=S_conv + 1e-8)[0]
    rms = librosa.feature.rms(y=y_conv, frame_length=n_fft, hop_length=hop)[0]
    rms_n = (rms - np.min(rms)) / (np.max(rms) - np.min(rms) + 1e-8)

    harsh = (hf_conv > (hf_src * over_factor)) & (flatness > flatness_th)
    min_seg_frames = max(1, int(round((min_segment_ms / 1000.0) * sr / hop)))
    harsh = _morph_min_len(harsh.astype(np.uint8), min_seg_frames).astype(np.float32)

    sos_hp = butter(4, hf_cutoff / (sr / 2.0), btype="highpass", output="sos")
    hp_conv = sosfiltfilt(sos_hp, y_conv).astype(np.float32)
    hp_src = sosfiltfilt(sos_hp, y_src).astype(np.float32)
    low = (y_conv - hp_conv).astype(np.float32)

    s_mask = _sample_mask_from_frame_mask(harsh, n, hop)
    hp_fix = hp_conv * (1.0 - s_mask) + (hp_conv * (1.0 - src_hf_mix) + hp_src * src_hf_mix) * s_mask
    y_out = low + hp_fix

    if breath_pass_through:
        breath = harsh.astype(bool) & (flatness > breath_flatness_th) & (rms_n < breath_rms_max)
        breath = _morph_min_len(breath.astype(np.uint8), max(1, min_seg_frames // 2)).astype(np.float32)
        b_mask = _sample_mask_from_frame_mask(breath, n, hop)
        bm = float(np.clip(breath_mix, 0.0, 1.0))
        y_out = y_out * (1.0 - b_mask) + (y_out * (1.0 - bm) + y_src * bm) * b_mask

    peak = float(np.max(np.abs(y_out)) + 1e-8)
    if peak > 0.995:
        y_out = y_out * (0.995 / peak)
    return y_out.astype(np.float32)


def _apply_global_hf_blend(
    y_conv: np.ndarray,
    y_src: np.ndarray,
    sr: int,
    hf_cutoff: float,
    src_mix: float,
) -> np.ndarray:
    n = min(len(y_conv), len(y_src))
    if n <= 0:
        return y_conv
    y_conv = y_conv[:n].astype(np.float32)
    y_src = y_src[:n].astype(np.float32)
    mix = float(np.clip(src_mix, 0.0, 1.0))
    if mix <= 0.0:
        return y_conv

    sos_hp = butter(4, hf_cutoff / (sr / 2.0), btype="highpass", output="sos")
    hp_conv = sosfiltfilt(sos_hp, y_conv).astype(np.float32)
    hp_src = sosfiltfilt(sos_hp, y_src).astype(np.float32)
    low = (y_conv - hp_conv).astype(np.float32)
    y_out = low + (hp_conv * (1.0 - mix) + hp_src * mix)
    return y_out.astype(np.float32)


def _apply_deesser(
    y: np.ndarray,
    sr: int,
    low_hz: float,
    high_hz: float,
    strength: float,
) -> np.ndarray:
    y = np.asarray(y, dtype=np.float32)
    if y.size == 0:
        return y
    st = float(np.clip(strength, 0.0, 1.0))
    if st <= 0.0:
        return y

    nyq = sr / 2.0
    lo = max(100.0, min(low_hz, nyq * 0.95))
    hi = max(lo + 200.0, min(high_hz, nyq * 0.99))
    sos_bp = butter(2, [lo / nyq, hi / nyq], btype="bandpass", output="sos")
    sib = sosfiltfilt(sos_bp, y).astype(np.float32)

    env = np.abs(sib)
    win = max(1, int(round(0.006 * sr)))  # ~6ms smoothing
    ker = np.ones(win, dtype=np.float32) / float(win)
    env_sm = np.convolve(env, ker, mode="same")
    thr = float(np.percentile(env_sm, 75))
    over = np.clip((env_sm - thr) / (thr + 1e-8), 0.0, 1.0)
    gain = 1.0 - (st * over)
    sib_out = sib * gain
    y_out = y - sib + sib_out
    return y_out.astype(np.float32)


def main() -> int:
    args = parse_args()

    seed_root = os.path.abspath(args.seed_root)
    manifest = os.path.abspath(args.manifest)
    style_normal = os.path.abspath(args.style_normal)
    style_ero = os.path.abspath(args.style_ero)
    out_root = os.path.abspath(args.out_root)
    report_path = os.path.abspath(args.report)
    os.makedirs(out_root, exist_ok=True)
    os.makedirs(os.path.dirname(report_path), exist_ok=True)

    rows = read_manifest_rows(manifest)
    if args.max_files and args.max_files > 0:
        rows = rows[: args.max_files]
    if not rows:
        print("manifest has no rows", file=sys.stderr)
        return 2

    os.chdir(seed_root)
    if seed_root not in sys.path:
        sys.path.insert(0, seed_root)

    import inference_v2 as iv2  # noqa: WPS433

    if args.prefer_fp32:
        iv2.dtype = torch.float32
        iv2.vc_wrapper_v2 = None

    if (args.nr_style_pre or args.nr_out_post) and nr is None:
        print("[warn] noisereduce is not installed. nr-style-pre / nr-out-post will be skipped.", flush=True)

    inf_args = SimpleNamespace(
        diffusion_steps=args.diffusion_steps,
        length_adjust=args.length_adjust,
        intelligibility_cfg_rate=args.intelligibility_cfg_rate,
        similarity_cfg_rate=args.similarity_cfg_rate,
        top_p=args.top_p,
        temperature=args.temperature,
        repetition_penalty=args.repetition_penalty,
        convert_style=args.convert_style,
        anonymization_only=args.anonymization_only,
        compile=args.compile,
        ar_checkpoint_path=args.ar_checkpoint_path,
        cfm_checkpoint_path=args.cfm_checkpoint_path,
    )

    style_cache = {}
    style_cache_wrapper_id = None

    def _ensure_wrapper_loaded():
        nonlocal style_cache_wrapper_id
        if iv2.vc_wrapper_v2 is None:
            iv2.vc_wrapper_v2 = iv2.load_v2_models(inf_args)
        wid = id(iv2.vc_wrapper_v2)
        if style_cache_wrapper_id != wid:
            style_cache.clear()
            style_cache_wrapper_id = wid

    def _build_style_ctx(target_audio_path: str):
        vc = iv2.vc_wrapper_v2
        if vc is None:
            raise RuntimeError("vc_wrapper_v2 is not initialized")

        source_key = os.path.abspath(target_audio_path)
        if source_key in style_cache:
            return style_cache[source_key]

        target_wave = librosa.load(target_audio_path, sr=vc.sr)[0]
        target_wave = target_wave[: vc.sr * (vc.dit_max_context_len - 5)]
        target_wave_tensor = torch.tensor(target_wave).unsqueeze(0).float().to(iv2.device)

        target_wave_16k = librosa.resample(target_wave, orig_sr=vc.sr, target_sr=16000)
        target_wave_16k_tensor = torch.tensor(target_wave_16k).unsqueeze(0).to(iv2.device)

        target_mel = vc.mel_fn(target_wave_tensor)
        target_mel_len = target_mel.size(2)
        with torch.autocast(device_type=iv2.device.type, dtype=iv2.dtype):
            target_content_indices = vc._process_content_features(target_wave_16k_tensor, is_narrow=False)
            target_style = vc.compute_style(target_wave_16k_tensor)
            prompt_condition, _ = vc.cfm_length_regulator(
                target_content_indices,
                ylens=torch.LongTensor([target_mel_len]).to(iv2.device),
            )

        ctx = {
            "target_mel": target_mel,
            "target_style": target_style,
            "target_mel_len": target_mel_len,
            "prompt_condition": prompt_condition,
        }
        style_cache[source_key] = ctx
        return ctx

    def _convert_voice_v2_cached(source_audio_path: str, target_audio_path: str):
        vc = iv2.vc_wrapper_v2
        if vc is None:
            raise RuntimeError("vc_wrapper_v2 is not initialized")

        if args.convert_style or args.anonymization_only or args.disable_style_cache:
            # Keep original path for modes this optimization doesn't target.
            return iv2.convert_voice_v2(source_audio_path, target_audio_path, inf_args)

        ctx = _build_style_ctx(target_audio_path)
        target_mel = ctx["target_mel"]
        target_style = ctx["target_style"]
        target_mel_len = ctx["target_mel_len"]
        prompt_condition = ctx["prompt_condition"]

        source_wave = librosa.load(source_audio_path, sr=vc.sr)[0]
        source_wave_tensor = torch.tensor(source_wave).unsqueeze(0).float().to(iv2.device)
        source_wave_16k = librosa.resample(source_wave, orig_sr=vc.sr, target_sr=16000)
        source_wave_16k_tensor = torch.tensor(source_wave_16k).unsqueeze(0).to(iv2.device)

        source_mel = vc.mel_fn(source_wave_tensor)
        source_mel_len = source_mel.size(2)
        with torch.autocast(device_type=iv2.device.type, dtype=iv2.dtype):
            source_content_indices = vc._process_content_features(source_wave_16k_tensor, is_narrow=False)
            cond, _ = vc.cfm_length_regulator(
                source_content_indices,
                ylens=torch.LongTensor([source_mel_len]).to(iv2.device),
            )

        max_context_window = vc.sr // vc.hop_size * vc.dit_max_context_len
        max_source_window = max_context_window - target_mel.size(2)
        overlap_wave_len = vc.overlap_frame_len * vc.hop_size

        generated_wave_chunks = []
        processed_frames = 0
        previous_chunk = None
        full_audio = None

        while processed_frames < cond.size(1):
            chunk_cond = cond[:, processed_frames:processed_frames + max_source_window]
            is_last_chunk = processed_frames + max_source_window >= cond.size(1)
            cat_condition = torch.cat([prompt_condition, chunk_cond], dim=1)
            original_len = cat_condition.size(1)
            if vc.dit_compiled:
                cat_condition = torch.nn.functional.pad(
                    cat_condition,
                    (0, 0, 0, vc.compile_len - cat_condition.size(1)),
                    value=0,
                )

            with torch.autocast(device_type=iv2.device.type, dtype=torch.float32):
                vc_mel = vc.cfm.inference(
                    cat_condition,
                    torch.LongTensor([original_len]).to(iv2.device),
                    target_mel,
                    target_style,
                    args.diffusion_steps,
                    inference_cfg_rate=[args.intelligibility_cfg_rate, args.similarity_cfg_rate],
                    random_voice=args.anonymization_only,
                )
            vc_mel = vc_mel[:, :, target_mel_len:original_len]
            vc_wave = vc.vocoder(vc_mel).squeeze()[None]
            processed_frames, previous_chunk, should_break, _, full_audio = vc._stream_wave_chunks(
                vc_wave,
                processed_frames,
                vc_mel,
                overlap_wave_len,
                generated_wave_chunks,
                previous_chunk,
                is_last_chunk,
                False,  # Quality is preserved; only MP3 streaming path is skipped.
            )
            if should_break:
                break

        if full_audio is None:
            full_audio = np.concatenate(generated_wave_chunks) if generated_wave_chunks else np.zeros(0, dtype=np.float32)
        return (vc.sr, full_audio)

    style_normal_use = style_normal
    style_ero_use = style_ero
    tmp_dir = None
    if args.nr_style_pre and nr is not None:
        tmp_dir = tempfile.mkdtemp(prefix="seedvc_style_nr_")
        sn_y, sn_sr = librosa.load(style_normal, sr=22050, mono=True)
        sn_y = _apply_noisereduce_light(
            sn_y, sn_sr, args.nr_style_prop_decrease, args.nr_time_mask_smooth_ms, args.nr_freq_mask_smooth_hz
        )
        style_normal_use = os.path.join(tmp_dir, "style_normal_nr.wav")
        sf.write(style_normal_use, sn_y, sn_sr)

        if os.path.abspath(style_ero) == os.path.abspath(style_normal):
            style_ero_use = style_normal_use
        else:
            se_y, se_sr = librosa.load(style_ero, sr=22050, mono=True)
            se_y = _apply_noisereduce_light(
                se_y, se_sr, args.nr_style_prop_decrease, args.nr_time_mask_smooth_ms, args.nr_freq_mask_smooth_hz
            )
            style_ero_use = os.path.join(tmp_dir, "style_ero_nr.wav")
            sf.write(style_ero_use, se_y, se_sr)
        print(f"[info] nr-style-pre applied: {style_normal_use}", flush=True)

    try:
        t0 = time.time()
        failed = 0
        report_rows = []
        total = len(rows)
        progress_offset = max(0, int(args.progress_offset or 0))
        progress_total = int(args.progress_total or 0)
        display_total = progress_total if progress_total > 0 else total

        def _write_report(rows_out):
            write_seed_vc_report(report_path, rows_out)

        # Create report file early so GUI can always point to a valid path even if interrupted.
        _write_report([])

        def _run_convert(src_path: str, style_path: str):
            _ensure_wrapper_loaded()
            # Suppress noisy upstream stdout (e.g. min/max tensor prints) while preserving our own progress logs.
            _buf = io.StringIO()
            with contextlib.redirect_stdout(_buf), contextlib.redirect_stderr(_buf):
                with torch.no_grad(), torch.inference_mode():
                    result = _convert_voice_v2_cached(src_path, style_path)
            if result is None:
                raise RuntimeError("convert_voice_v2 returned None")
            return result

        for i, r in enumerate(rows, start=1):
            rel = (r.get("relative_path") or "").replace("\\", "/")
            bucket = (r.get("model_bucket") or "").strip().lower()
            src = os.path.abspath(r.get("source_file") or "")
            style = style_ero_use if bucket == "ero" else style_normal_use
            out_file = os.path.abspath(os.path.join(out_root, rel))
            os.makedirs(os.path.dirname(out_file), exist_ok=True)

            display_i = progress_offset + i
            print(f"[{display_i}/{display_total}] start bucket={bucket or 'normal'} rel={rel}", flush=True)
            started = time.time()
            status = "ok"
            code = 0
            note = ""
            try:
                retried_fp32 = False
                try:
                    sr, wav = _run_convert(src, style)
                except Exception as ex_first:  # noqa: BLE001
                    msg = f"{type(ex_first).__name__}: {ex_first}"
                    lower = msg.lower()
                    dtype_mismatch = ("same dtype" in lower) and ("half" in lower) and ("float" in lower)
                    if dtype_mismatch and getattr(iv2, "dtype", None) == torch.float16:
                        print("[warn] dtype mismatch detected, retrying with float32", flush=True)
                        iv2.vc_wrapper_v2 = None
                        iv2.dtype = torch.float32
                        style_cache.clear()
                        style_cache_wrapper_id = None
                        sr, wav = _run_convert(src, style)
                        retried_fp32 = True
                    else:
                        raise

                y_src, _ = librosa.load(src, sr=sr, mono=True)
                if args.harsh_fix:
                    wav = _apply_harsh_fix(
                        y_conv=np.asarray(wav, dtype=np.float32),
                        y_src=np.asarray(y_src, dtype=np.float32),
                        sr=sr,
                        hf_cutoff=args.harsh_hf_cutoff,
                        src_hf_mix=args.harsh_src_hf_mix,
                        over_factor=args.harsh_over_factor,
                        flatness_th=args.harsh_flatness_th,
                        min_segment_ms=args.harsh_min_segment_ms,
                        breath_pass_through=args.breath_pass_through,
                        breath_flatness_th=args.breath_flatness_th,
                        breath_rms_max=args.breath_rms_max,
                        breath_mix=args.breath_mix,
                    )
                if args.nr_out_post and nr is not None:
                    wav = _apply_noisereduce_light(
                        wav,
                        sr,
                        args.nr_out_prop_decrease,
                        args.nr_time_mask_smooth_ms,
                        args.nr_freq_mask_smooth_hz,
                    )
                if args.global_hf_blend:
                    wav = _apply_global_hf_blend(
                        y_conv=np.asarray(wav, dtype=np.float32),
                        y_src=np.asarray(y_src, dtype=np.float32),
                        sr=sr,
                        hf_cutoff=args.global_hf_cutoff,
                        src_mix=args.global_hf_src_mix,
                    )
                if args.global_deesser:
                    wav = _apply_deesser(
                        y=np.asarray(wav, dtype=np.float32),
                        sr=sr,
                        low_hz=args.deesser_low_hz,
                        high_hz=args.deesser_high_hz,
                        strength=args.deesser_strength,
                    )
                wav = match_output_loudness_to_source(wav, y_src)
                peak = float(np.max(np.abs(wav)) + 1e-8)
                if peak > 0.995:
                    wav = (wav * (0.995 / peak)).astype(np.float32)
                sf.write(out_file, wav, sr)
                if retried_fp32:
                    note = "retried_with_fp32"
            except Exception as ex:  # noqa: BLE001
                note = build_failure_note(ex)
                if args.on_error == "copy-source":
                    try:
                        copy_source_fallback(src, out_file)
                        status = "fallback_src"
                        code = 0
                        print(f"[warn] fallback(copy-source): rel={rel} note={note}", flush=True)
                    except Exception as ex_fb:  # noqa: BLE001
                        status = "failed"
                        code = 1
                        failed += 1
                        note = f"{note} | fallback_error={type(ex_fb).__name__}: {ex_fb}"
                elif args.on_error == "silence":
                    try:
                        write_silence_fallback(src, out_file)
                        status = "fallback_silence"
                        code = 0
                        print(f"[warn] fallback(silence): rel={rel} note={note}", flush=True)
                    except Exception as ex_fb:  # noqa: BLE001
                        status = "failed"
                        code = 1
                        failed += 1
                        note = f"{note} | fallback_error={type(ex_fb).__name__}: {ex_fb}"
                else:
                    status = "failed"
                    code = 1
                    failed += 1
                    print(f"[warn] failed: rel={rel} note={note}", flush=True)

            elapsed = time.time() - t0
            avg = elapsed / i
            eta = (total - i) * avg
            one = time.time() - started
            print(
                f"[{display_i}/{display_total}] {status} exit={code} one={one:.1f}s failed={failed} "
                f"elapsed={elapsed:.1f}s eta={eta:.1f}s",
                flush=True,
            )

            report_rows.append(
                {
                    "relative_path": rel,
                    "bucket": bucket,
                    "source_file": src,
                    "style_file": style,
                    "output_file": out_file,
                    "status": status,
                    "exit_code": code,
                    "note": note,
                }
            )
            if args.report_every > 0 and (i % args.report_every == 0 or i == total):
                _write_report(report_rows)

        _write_report(report_rows)
        print(f"done. total={total} failed={failed}")
        print(f"report={report_path}")
        return 1 if failed else 0
    finally:
        if tmp_dir:
            shutil.rmtree(tmp_dir, ignore_errors=True)


if __name__ == "__main__":
    raise SystemExit(main())
