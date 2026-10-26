#!/usr/bin/env python3
"""Inspect and edit sound effects with ffmpeg, so that a model that cannot hear can still check them.

Subcommands:
  inspect FILE... [--png DIR]   length, peak, RMS, loudness, leading/trailing silence; optional waveform+spectrogram PNG
  fit IN OUT.wav                trim silence, fade, peak-normalize, write 16-bit WAV
  extend IN OUT.wav --to SEC    lengthen by --method slow (pitch down), tempo (same pitch) or echo (add a tail)
  layer OUT.wav A[@ms[:dB]] B[@ms[:dB]]...   mix several sounds at offsets
  mux VIDEO OUT.mp4 A[@ms[:dB]]...           put sounds on a silent recording at the given times
  frames VIDEO OUT.png --end SEC             tile frames to find the moment of an action

Run with `python3 -I`. Needs ffmpeg and ffprobe on PATH.
"""

from __future__ import annotations

import argparse
import array
import json
import math
import re
import subprocess
import sys
from pathlib import Path

SILENCE_DB = -50.0
PRE_ROLL_MS = 5.0
CLICK_FADE_MS = 3.0


def run(args: list[str]) -> subprocess.CompletedProcess:
    return subprocess.run(args, capture_output=True, check=True)


def probe(path: Path) -> tuple[int, int, float]:
    out = run(
        [
            "ffprobe", "-v", "error", "-select_streams", "a:0",
            "-show_entries", "stream=sample_rate,channels:format=duration",
            "-of", "json", str(path),
        ]
    ).stdout
    info = json.loads(out)
    stream = info["streams"][0]
    return int(stream["sample_rate"]), int(stream["channels"]), float(info["format"]["duration"])


def decode(path: Path, rate: int, channels: int) -> array.array:
    raw = run(
        ["ffmpeg", "-v", "error", "-i", str(path), "-f", "f32le", "-acodec", "pcm_f32le",
         "-ar", str(rate), "-ac", str(channels), "-"]
    ).stdout
    samples = array.array("f")
    samples.frombytes(raw)
    if sys.byteorder != "little":
        samples.byteswap()
    return samples


def db(value: float) -> float:
    return 20 * math.log10(value) if value > 0 else -math.inf


def frame_peaks(samples: array.array, channels: int) -> list[float]:
    return [
        max(abs(samples[i + c]) for c in range(channels))
        for i in range(0, len(samples) - channels + 1, channels)
    ]


def silence_bounds(peaks: list[float], threshold_db: float) -> tuple[int, int]:
    """Index of the first and one past the last frame louder than the threshold."""
    limit = 10 ** (threshold_db / 20)
    loud = [i for i, p in enumerate(peaks) if p > limit]
    if not loud:
        return 0, 0
    return loud[0], loud[-1] + 1


def loudness_lufs(path: Path) -> float | None:
    err = subprocess.run(
        ["ffmpeg", "-hide_banner", "-nostats", "-i", str(path), "-af", "ebur128", "-f", "null", "-"],
        # Tags in some files (Shift_JIS titles in 魔王魂's WAVs) are not UTF-8
        capture_output=True, text=True, errors="replace",
    ).stderr
    found = re.findall(r"I:\s+(-?[\d.]+|-inf) LUFS", err)
    # ebur128 gates out quiet blocks and reports -70 when nothing is left (clips under 0.4s)
    if not found or found[-1] == "-inf" or float(found[-1]) <= -69.9:
        return None
    return float(found[-1])


def inspect(paths: list[Path], png_dir: Path | None) -> None:
    for path in paths:
        rate, channels, duration = probe(path)
        samples = decode(path, rate, channels)
        peaks = frame_peaks(samples, channels)
        peak = max(peaks, default=0.0)
        rms = math.sqrt(sum(s * s for s in samples) / len(samples)) if samples else 0.0
        start, end = silence_bounds(peaks, SILENCE_DB)
        lufs = loudness_lufs(path)
        print(f"{path.name}")
        print(f"  長さ {duration:.3f}秒  {rate}Hz  {channels}ch")
        print(f"  ピーク {db(peak):.1f}dBFS  RMS {db(rms):.1f}dBFS  ラウドネス "
              + (f"{lufs:.1f}LUFS" if lufs is not None else "測れない（0.4秒未満か無音）"))
        print(f"  頭の無音 {start / rate * 1000:.0f}ms  終わりの無音 {(len(peaks) - end) / rate * 1000:.0f}ms"
              f"（{SILENCE_DB:.0f}dB以下）")
        if png_dir is not None:
            png_dir.mkdir(parents=True, exist_ok=True)
            out = png_dir / f"{path.stem}.png"
            run(
                ["ffmpeg", "-v", "error", "-y", "-i", str(path), "-filter_complex",
                 "[0:a]aformat=channel_layouts=mono,asplit[a][b];"
                 "[a]showwavespic=s=800x200:colors=0x40c0ff:scale=sqrt:draw=full[w];"
                 "[b]showspectrumpic=s=800x240:legend=0:color=intensity:scale=log[s];"
                 "[w][s]vstack", "-frames:v", "1", str(out)]
            )
            print(f"  画像 {out}")


def write_wav(source: Path, out: Path, filters: list[str], rate: int, channels: int) -> None:
    run(["ffmpeg", "-v", "error", "-y", "-i", str(source), "-af", ",".join(filters),
         "-ar", str(rate), "-ac", str(channels), "-c:a", "pcm_s16le", str(out)])


def normalize_peak(path: Path, target_db: float) -> None:
    rate, channels, _ = probe(path)
    peak = max(frame_peaks(decode(path, rate, channels), channels), default=0.0)
    if peak <= 0:
        return
    gain = target_db - db(peak)
    tmp = path.with_suffix(".tmp.wav")
    run(["ffmpeg", "-v", "error", "-y", "-i", str(path), "-af", f"volume={gain:.3f}dB",
         "-c:a", "pcm_s16le", str(tmp)])
    tmp.replace(path)


def fit(src: Path, out: Path, trim_start: bool, trim_end: bool, fade_out_ms: float,
        peak_db: float, mono: bool, rate: int | None) -> None:
    src_rate, channels, duration = probe(src)
    rate = rate or src_rate
    peaks = frame_peaks(decode(src, src_rate, channels), channels)
    start, end = silence_bounds(peaks, SILENCE_DB)
    begin = max(0.0, start / src_rate - PRE_ROLL_MS / 1000) if trim_start else 0.0
    finish = min(duration, end / src_rate + 0.02) if trim_end and end else duration
    length = finish - begin
    filters = [f"atrim=start={begin:.4f}:end={finish:.4f}", "asetpts=PTS-STARTPTS"]
    if trim_start:
        filters.append(f"afade=t=in:d={CLICK_FADE_MS / 1000:.4f}")
    if fade_out_ms > 0:
        fade = min(fade_out_ms / 1000, length)
        filters.append(f"afade=t=out:st={length - fade:.4f}:d={fade:.4f}")
    out.parent.mkdir(parents=True, exist_ok=True)
    write_wav(src, out, filters, rate, 1 if mono else channels)
    normalize_peak(out, peak_db)


def tempo_chain(factor: float) -> list[str]:
    """atempo per stage is limited to 0.5..100, so chain stages for larger slow-downs."""
    speed = 1 / factor
    stages = []
    while speed < 0.5:
        stages.append("atempo=0.5")
        speed /= 0.5
    stages.append(f"atempo={speed:.5f}")
    return stages


def extend(src: Path, out: Path, method: str, target: float, peak_db: float) -> None:
    rate, channels, duration = probe(src)
    factor = target / duration
    if method == "slow":
        filters = [f"asetrate={rate / factor:.2f}", f"aresample={rate}"]
    elif method == "tempo":
        filters = tempo_chain(factor)
    elif method == "echo":
        tail = max(0.0, target - duration)
        delays = [int(tail * 1000 * f) for f in (0.18, 0.38, 0.6)] if tail > 0 else [60, 120, 200]
        decays = ["0.45", "0.28", "0.15"]
        filters = [f"apad=pad_dur={tail:.3f}",
                   f"aecho=0.85:0.8:{'|'.join(str(max(d, 20)) for d in delays)}:{'|'.join(decays)}"]
    else:
        raise ValueError(method)
    filters += [f"apad=whole_dur={target:.4f}", f"atrim=end={target:.4f}", f"afade=t=out:st={target * 0.7:.4f}:d={target * 0.3:.4f}"]
    out.parent.mkdir(parents=True, exist_ok=True)
    write_wav(src, out, filters, rate, channels)
    normalize_peak(out, peak_db)


def parse_specs(specs: list[str]) -> list[tuple[Path, float, float]]:
    """FILE[@delay_ms[:gain_dB]] -> (path, delay_ms, gain_db)."""
    parts = []
    for spec in specs:
        match = re.fullmatch(r"(.+?)(?:@(\d+(?:\.\d+)?))?(?::(-?\d+(?:\.\d+)?))?", spec)
        parts.append((Path(match.group(1)), float(match.group(2) or 0), float(match.group(3) or 0)))
    return parts


def mix_graph(parts: list[tuple[Path, float, float]], rate: int, first_input: int) -> str:
    chains = [
        f"[{first_input + i}:a]aresample={rate},aformat=channel_layouts=stereo,"
        f"adelay={ms:.0f}:all=1,volume={gain}dB[l{i}]"
        for i, (_, ms, gain) in enumerate(parts)
    ]
    mix = "".join(f"[l{i}]" for i in range(len(parts))) + f"amix=inputs={len(parts)}:normalize=0:duration=longest"
    return ";".join(chains + [mix])


def layer(out: Path, specs: list[str], peak_db: float) -> None:
    parts = parse_specs(specs)
    rate = max(probe(p)[0] for p, _, _ in parts)
    args = ["ffmpeg", "-v", "error", "-y"]
    for path, _, _ in parts:
        args += ["-i", str(path)]
    out.parent.mkdir(parents=True, exist_ok=True)
    run(args + ["-filter_complex", mix_graph(parts, rate, 0), "-c:a", "pcm_s16le", str(out)])
    normalize_peak(out, peak_db)


def mux(video: Path, out: Path, specs: list[str]) -> None:
    """Put sounds on a silent recording at the given times, keeping the video length."""
    parts = parse_specs(specs)
    args = ["ffmpeg", "-v", "error", "-y", "-i", str(video)]
    for path, _, _ in parts:
        args += ["-i", str(path)]
    graph = mix_graph(parts, 48000, 1) + ",apad[a]"
    out.parent.mkdir(parents=True, exist_ok=True)
    run(args + ["-filter_complex", graph, "-map", "0:v", "-map", "[a]", "-c:v", "copy",
                "-c:a", "aac", "-b:a", "160k", "-shortest", "-movflags", "+faststart", str(out)])


def frames(video: Path, out: Path, start: float, end: float, step: float, columns: int) -> None:
    """Tile frames from start to end so the moment of an action can be found; prints each tile's time."""
    count = max(1, int(round((end - start) / step)) + 1)
    rows = math.ceil(count / columns)
    run(["ffmpeg", "-v", "error", "-y", "-ss", f"{start:.3f}", "-i", str(video), "-vf",
         f"fps=1/{step},scale=240:-1,tile={columns}x{rows}", "-frames:v", "1", str(out)])
    for i in range(count):
        print(f"  {i + 1}枚目（{i // columns + 1}行{i % columns + 1}列） {start + i * step:.2f}秒")


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = parser.add_subparsers(dest="command", required=True)

    p = sub.add_parser("inspect")
    p.add_argument("files", nargs="+", type=Path)
    p.add_argument("--png", type=Path, help="waveform and spectrogram images go here")

    p = sub.add_parser("fit")
    p.add_argument("src", type=Path)
    p.add_argument("out", type=Path)
    p.add_argument("--keep-start", action="store_true", help="do not trim leading silence")
    p.add_argument("--keep-end", action="store_true", help="do not trim trailing silence")
    p.add_argument("--fade-out", type=float, default=0.0, help="ms")
    p.add_argument("--peak", type=float, default=-1.0, help="dBFS")
    p.add_argument("--mono", action="store_true")
    p.add_argument("--rate", type=int)

    p = sub.add_parser("extend")
    p.add_argument("src", type=Path)
    p.add_argument("out", type=Path)
    p.add_argument("--method", choices=["slow", "tempo", "echo"], required=True)
    p.add_argument("--to", type=float, required=True, help="target length in seconds")
    p.add_argument("--peak", type=float, default=-1.0, help="dBFS")

    p = sub.add_parser("layer")
    p.add_argument("out", type=Path)
    p.add_argument("specs", nargs="+", help="FILE[@delay_ms[:gain_dB]]")
    p.add_argument("--peak", type=float, default=-1.0, help="dBFS")

    p = sub.add_parser("mux")
    p.add_argument("video", type=Path)
    p.add_argument("out", type=Path)
    p.add_argument("specs", nargs="+", help="FILE[@time_ms[:gain_dB]]")

    p = sub.add_parser("frames")
    p.add_argument("video", type=Path)
    p.add_argument("out", type=Path)
    p.add_argument("--start", type=float, default=0.0, help="seconds")
    p.add_argument("--end", type=float, required=True, help="seconds")
    p.add_argument("--step", type=float, default=0.1, help="seconds between tiles")
    p.add_argument("--columns", type=int, default=5)

    a = parser.parse_args()
    if a.command == "inspect":
        inspect(a.files, a.png)
    elif a.command == "fit":
        fit(a.src, a.out, not a.keep_start, not a.keep_end, a.fade_out, a.peak, a.mono, a.rate)
    elif a.command == "extend":
        extend(a.src, a.out, a.method, a.to, a.peak)
    elif a.command == "layer":
        layer(a.out, a.specs, a.peak)
    elif a.command == "mux":
        mux(a.video, a.out, a.specs)
    else:
        frames(a.video, a.out, a.start, a.end, a.step, a.columns)


if __name__ == "__main__":
    main()
