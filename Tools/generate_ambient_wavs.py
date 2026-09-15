#!/usr/bin/env python3
"""Generate non-tonal ambient / SFX WAVs for Build-A-Tower (stdlib only).

Replaces pitched sine placeholders with filtered noise beds + sparse mechanical
accents so the mix reads as room tone, not an out-of-tune keyboard.
"""

from __future__ import annotations

import math
import os
import random
import struct
import wave
from pathlib import Path

SR = 44100
AMP = 0.18  # keep beds quiet; mixer still scales


def clamp(x: float, lo: float = -1.0, hi: float = 1.0) -> float:
    return lo if x < lo else hi if x > hi else x


def write_wav(path: Path, samples: list[float], sr: int = SR) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with wave.open(str(path), "w") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(sr)
        frames = bytearray()
        for s in samples:
            v = int(clamp(s) * 32767.0)
            frames += struct.pack("<h", v)
        w.writeframes(frames)


def brown_noise(n: int, rng: random.Random, leak: float = 0.997) -> list[float]:
    out = [0.0] * n
    x = 0.0
    for i in range(n):
        x = leak * x + rng.uniform(-1.0, 1.0)
        out[i] = x
    # normalize
    peak = max(abs(v) for v in out) or 1.0
    return [v / peak for v in out]


def pinkish(n: int, rng: random.Random) -> list[float]:
    # Voss-McCartney-ish cheap pink
    b = [0.0] * 7
    out = [0.0] * n
    for i in range(n):
        white = rng.uniform(-1.0, 1.0)
        b[0] = 0.99886 * b[0] + white * 0.0555179
        b[1] = 0.99332 * b[1] + white * 0.0750759
        b[2] = 0.96900 * b[2] + white * 0.1538520
        b[3] = 0.86650 * b[3] + white * 0.3104856
        b[4] = 0.55000 * b[4] + white * 0.5329522
        b[5] = -0.7616 * b[5] - white * 0.0168980
        pink = b[0] + b[1] + b[2] + b[3] + b[4] + b[5] + b[6] + white * 0.5362
        b[6] = white * 0.115926
        out[i] = pink
    peak = max(abs(v) for v in out) or 1.0
    return [v / peak for v in out]


def one_pole_lp(src: list[float], cutoff_hz: float, sr: int = SR) -> list[float]:
    # y += a * (x - y)
    a = 1.0 - math.exp(-2.0 * math.pi * cutoff_hz / sr)
    y = 0.0
    out = [0.0] * len(src)
    for i, x in enumerate(src):
        y += a * (x - y)
        out[i] = y
    return out


def one_pole_hp(src: list[float], cutoff_hz: float, sr: int = SR) -> list[float]:
    lp = one_pole_lp(src, cutoff_hz, sr)
    return [src[i] - lp[i] for i in range(len(src))]


def soft_impulse(n: int, pos: int, width: int, amp: float, rng: random.Random) -> None:
    """Add a short noise burst envelope into buffer n (mutates list of floats)."""
    for i in range(max(0, pos - width), min(len(n), pos + width)):
        t = (i - pos) / max(1, width)
        env = math.exp(-3.5 * abs(t))
        n[i] += amp * env * rng.uniform(-1.0, 1.0)


def rumble_whoosh(buf: list[float], pos: int, length: int, amp: float, rng: random.Random) -> None:
    for i in range(pos, min(len(buf), pos + length)):
        t = (i - pos) / max(1, length)
        env = math.sin(math.pi * t) ** 1.5
        # slow modulated noise
        buf[i] += amp * env * (0.6 * rng.uniform(-1.0, 1.0) + 0.4 * math.sin(i * 0.01))


def crossfade_loop(buf: list[float], fade: int) -> list[float]:
    if fade <= 0 or fade * 2 >= len(buf):
        return buf
    out = buf[:]
    for i in range(fade):
        t = i / fade
        a = out[i]
        b = out[len(out) - fade + i]
        out[i] = a * t + b * (1.0 - t)
        out[len(out) - fade + i] = b * t + a * (1.0 - t)
    return out


def normalize(buf: list[float], peak: float = AMP) -> list[float]:
    m = max(abs(v) for v in buf) or 1.0
    scale = peak / m
    return [v * scale for v in buf]


def bed(
    seconds: float,
    seed: int,
    *,
    brown_mix: float,
    pink_mix: float,
    lp_hz: float,
    hp_hz: float,
    click_rate: float,
    click_amp: float,
    whoosh_rate: float,
    whoosh_amp: float,
    whoosh_len: float,
) -> list[float]:
    rng = random.Random(seed)
    n = int(seconds * SR)
    b = brown_noise(n, rng)
    p = pinkish(n, rng)
    mix = [brown_mix * b[i] + pink_mix * p[i] for i in range(n)]
    if hp_hz > 0:
        mix = one_pole_hp(mix, hp_hz)
    mix = one_pole_lp(mix, lp_hz)

    # sparse accents
    if click_rate > 0:
        interval = max(1, int(SR / click_rate))
        t = rng.randint(interval // 3, interval)
        while t < n - 200:
            soft_impulse(mix, t, width=rng.randint(40, 120), amp=click_amp, rng=rng)
            t += rng.randint(interval // 2, interval * 2)

    if whoosh_rate > 0:
        interval = max(1, int(SR / whoosh_rate))
        t = rng.randint(interval // 2, interval)
        wlen = int(whoosh_len * SR)
        while t < n - wlen - 10:
            rumble_whoosh(mix, t, wlen, whoosh_amp, rng)
            t += rng.randint(interval, int(interval * 2.5))

    mix = crossfade_loop(mix, fade=int(0.12 * SR))
    return normalize(mix, AMP)


def sfx_build_place() -> list[float]:
    rng = random.Random(101)
    n = int(0.22 * SR)
    thud = [rng.uniform(-1, 1) * math.exp(-22.0 * (i / SR)) for i in range(n)]
    thud = one_pole_lp(thud, 220)
    click = [0.0] * n
    soft_impulse(click, int(0.015 * SR), 35, 0.9, rng)
    click = one_pole_hp(one_pole_lp(click, 3500), 800)
    mix = [0.7 * thud[i] + 0.35 * click[i] for i in range(n)]
    return normalize(mix, 0.55)


def sfx_elevator_ping() -> list[float]:
    """Soft mechanical ding: very short decaying partials buried in noise (not sustained tone)."""
    rng = random.Random(202)
    n = int(0.28 * SR)
    buf = [0.0] * n
    for i in range(n):
        t = i / SR
        env = math.exp(-14.0 * t)
        # brief inharmonic partials that die fast — ding, not held note
        ding = (
            0.45 * math.sin(2 * math.pi * 880 * t)
            + 0.22 * math.sin(2 * math.pi * 1320 * t)
            + 0.12 * math.sin(2 * math.pi * 1760 * t)
        )
        body = 0.25 * rng.uniform(-1, 1)
        buf[i] = env * (ding + body)
    buf = one_pole_lp(buf, 4200)
    return normalize(buf, 0.35)


def sfx_elevator_door() -> list[float]:
    rng = random.Random(303)
    n = int(0.55 * SR)
    buf = [0.0] * n
    rumble_whoosh(buf, 0, n, 0.55, rng)
    soft_impulse(buf, int(0.08 * SR), 90, 0.35, rng)
    soft_impulse(buf, int(0.42 * SR), 110, 0.4, rng)
    buf = one_pole_lp(buf, 900)
    buf = one_pole_hp(buf, 80)
    return normalize(buf, 0.4)


def outdoor_bed(seconds: float = 10.0, seed: int = 7) -> list[float]:
    """Light breeze + sparse bird chirps (short sweeps, not sustained tones)."""
    rng = random.Random(seed)
    n = int(seconds * SR)
    # Very soft breeze — low and quiet so it does not read as highway/static.
    breeze = brown_noise(n, rng, leak=0.9985)
    breeze = one_pole_lp(breeze, 280)
    breeze = [0.045 * v for v in breeze]

    buf = breeze[:]
    # Bird chirps: short rising/falling sine sweeps with noise grain.
    t = rng.uniform(0.4, 1.2)
    while t < seconds - 0.35:
        chirp_len = rng.uniform(0.08, 0.22)
        f0 = rng.uniform(1800, 3200)
        f1 = f0 + rng.uniform(400, 1400) * rng.choice([-1, 1])
        amp = rng.uniform(0.045, 0.09)
        start = int(t * SR)
        length = int(chirp_len * SR)
        for i in range(length):
            if start + i >= n:
                break
            u = i / max(1, length - 1)
            env = math.sin(math.pi * u) ** 1.2
            freq = f0 + (f1 - f0) * u
            tone = math.sin(2 * math.pi * freq * (i / SR))
            grain = 0.15 * rng.uniform(-1, 1)
            buf[start + i] += amp * env * (0.85 * tone + grain)
        t += rng.uniform(0.55, 1.8)

    buf = crossfade_loop(buf, fade=int(0.2 * SR))
    return normalize(buf, 0.16)


PROFILES = {
    # quiet office murmur + sparse key clicks
    "office": dict(seconds=6.0, seed=11, brown_mix=0.55, pink_mix=0.45, lp_hz=1400, hp_hz=80,
                   click_rate=4.5, click_amp=0.12, whoosh_rate=0.15, whoosh_amp=0.04, whoosh_len=0.4),
    # busier midrange + more activity
    "restaurant": dict(seconds=6.0, seed=22, brown_mix=0.35, pink_mix=0.65, lp_hz=2200, hp_hz=100,
                       click_rate=6.0, click_amp=0.10, whoosh_rate=0.8, whoosh_amp=0.08, whoosh_len=0.25),
    "retail": dict(seconds=6.0, seed=33, brown_mix=0.45, pink_mix=0.55, lp_hz=1800, hp_hz=90,
                   click_rate=2.0, click_amp=0.07, whoosh_rate=0.35, whoosh_amp=0.05, whoosh_len=0.35),
    "condo": dict(seconds=7.0, seed=44, brown_mix=0.65, pink_mix=0.35, lp_hz=1100, hp_hz=60,
                  click_rate=0.8, click_amp=0.05, whoosh_rate=0.2, whoosh_amp=0.06, whoosh_len=0.6),
    "hotel": dict(seconds=7.0, seed=55, brown_mix=0.7, pink_mix=0.3, lp_hz=900, hp_hz=50,
                  click_rate=0.35, click_amp=0.04, whoosh_rate=0.12, whoosh_amp=0.05, whoosh_len=0.8),
    "conference": dict(seconds=6.0, seed=66, brown_mix=0.5, pink_mix=0.5, lp_hz=1600, hp_hz=90,
                       click_rate=1.2, click_amp=0.06, whoosh_rate=0.25, whoosh_amp=0.05, whoosh_len=0.5),
    "event": dict(seconds=6.0, seed=77, brown_mix=0.3, pink_mix=0.7, lp_hz=2400, hp_hz=80,
                  click_rate=1.5, click_amp=0.05, whoosh_rate=1.0, whoosh_amp=0.1, whoosh_len=0.35),
    "elevator": dict(seconds=5.0, seed=88, brown_mix=0.8, pink_mix=0.2, lp_hz=700, hp_hz=40,
                     click_rate=0.2, click_amp=0.03, whoosh_rate=0.45, whoosh_amp=0.12, whoosh_len=1.2),
    "parking": dict(seconds=7.0, seed=99, brown_mix=0.75, pink_mix=0.25, lp_hz=800, hp_hz=35,
                    click_rate=0.15, click_amp=0.08, whoosh_rate=0.55, whoosh_amp=0.14, whoosh_len=1.4),
    "utility": dict(seconds=5.0, seed=111, brown_mix=0.85, pink_mix=0.15, lp_hz=600, hp_hz=45,
                    click_rate=0.4, click_amp=0.05, whoosh_rate=0.3, whoosh_amp=0.07, whoosh_len=0.9),
    "lobby": dict(seconds=6.0, seed=122, brown_mix=0.55, pink_mix=0.45, lp_hz=1500, hp_hz=70,
                  click_rate=1.0, click_amp=0.05, whoosh_rate=0.4, whoosh_amp=0.06, whoosh_len=0.45),
    "stairs": dict(seconds=5.0, seed=133, brown_mix=0.6, pink_mix=0.4, lp_hz=1200, hp_hz=100,
                   click_rate=2.2, click_amp=0.14, whoosh_rate=0.05, whoosh_amp=0.03, whoosh_len=0.3),
}


def main() -> None:
    root = Path(__file__).resolve().parents[1]
    amb = root / "Assets" / "Resources" / "Audio" / "Ambience"
    sfx = root / "Assets" / "Resources" / "Audio" / "Sfx"

    outdoor = outdoor_bed()
    write_wav(amb / "outdoor.wav", outdoor)
    print(f"wrote outdoor.wav ({len(outdoor)/SR:.1f}s)")

    for name, kwargs in PROFILES.items():
        samples = bed(**kwargs)
        out = amb / f"{name}.wav"
        write_wav(out, samples)
        print(f"wrote {out} ({len(samples)/SR:.1f}s)")

    write_wav(sfx / "build_place.wav", sfx_build_place())
    write_wav(sfx / "elevator_ping.wav", sfx_elevator_ping())
    write_wav(sfx / "elevator_door.wav", sfx_elevator_door())
    print("wrote SFX")


if __name__ == "__main__":
    main()
