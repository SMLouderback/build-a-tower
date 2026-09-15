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


def bandpass_noise(n: int, rng: random.Random, low_hz: float, high_hz: float) -> list[float]:
    src = [rng.uniform(-1.0, 1.0) for _ in range(n)]
    src = one_pole_hp(src, low_hz)
    src = one_pole_lp(src, high_hz)
    peak = max(abs(v) for v in src) or 1.0
    return [v / peak for v in src]


def add_chatter_syllable(buf: list[float], start: int, rng: random.Random, amp: float) -> None:
    """Short speech-ish noise burst (formant-ish band), not a musical tone."""
    n = len(buf)
    dur = rng.uniform(0.05, 0.16)
    length = int(dur * SR)
    f_lo = rng.uniform(220, 420)
    f_hi = rng.uniform(1400, 2800)
    for i in range(length):
        idx = start + i
        if idx >= n:
            break
        u = i / max(1, length - 1)
        env = math.sin(math.pi * u) ** 1.35
        # cheap moving band: mix two filtered noise samples
        noise = rng.uniform(-1.0, 1.0)
        # soft AM so it doesn't read as static hiss
        am = 0.65 + 0.35 * math.sin(2 * math.pi * (f_lo * 0.02) * (i / SR))
        buf[idx] += amp * env * am * noise
    # local blur toward speech band
    end = min(n, start + length)
    if end - start > 8:
        segment = buf[start:end]
        segment = one_pole_hp(segment, f_lo)
        segment = one_pole_lp(segment, f_hi)
        for i, v in enumerate(segment):
            # blend filtered back
            buf[start + i] = 0.35 * buf[start + i] + 0.65 * v


def chatter_bed(
    seconds: float,
    seed: int,
    *,
    density: float,
    amp: float,
    room_tone: float,
    accent_rate: float,
    accent_amp: float,
) -> list[float]:
    """Quiet room tone + overlapping random chatter syllables."""
    rng = random.Random(seed)
    n = int(seconds * SR)
    tone = brown_noise(n, rng, leak=0.998)
    tone = one_pole_lp(tone, 500)
    buf = [room_tone * v for v in tone]

    # Several overlapping talkers
    talkers = max(2, int(3 * density))
    for t_i in range(talkers):
        tr = random.Random(seed * 31 + t_i * 97)
        t = tr.uniform(0.2, 1.0)
        while t < seconds - 0.2:
            # burst of 2–6 syllables
            count = tr.randint(2, 6)
            pos = int(t * SR)
            for _ in range(count):
                add_chatter_syllable(buf, pos, tr, amp * tr.uniform(0.7, 1.15))
                pos += int(tr.uniform(0.07, 0.18) * SR)
            t += tr.uniform(0.55 / density, 1.8 / density)

    if accent_rate > 0:
        interval = max(1, int(SR / accent_rate))
        t = rng.randint(interval // 3, interval)
        while t < n - 200:
            soft_impulse(buf, t, width=rng.randint(30, 90), amp=accent_amp, rng=rng)
            t += rng.randint(interval, interval * 3)

    buf = one_pole_lp(buf, 3400)
    buf = crossfade_loop(buf, fade=int(0.15 * SR))
    return soft_peak_limit(normalize(buf, 0.14), 0.14)


def motor_whir_bed(seconds: float = 6.0, seed: int = 88) -> list[float]:
    """Quiet electric motor: soft tonal whir + tiny hiss, easy to ignore."""
    rng = random.Random(seed)
    n = int(seconds * SR)
    buf = [0.0] * n
    f0 = 118.0
    for i in range(n):
        t = i / SR
        # slight RPM flutter
        flutter = 1.0 + 0.012 * math.sin(2 * math.pi * 0.35 * t)
        whir = (
            0.55 * math.sin(2 * math.pi * f0 * flutter * t)
            + 0.22 * math.sin(2 * math.pi * (2.0 * f0) * flutter * t)
            + 0.08 * math.sin(2 * math.pi * (3.0 * f0) * flutter * t)
        )
        hiss = 0.04 * rng.uniform(-1.0, 1.0)
        buf[i] = 0.035 * whir + hiss
    buf = one_pole_lp(buf, 900)
    buf = one_pole_hp(buf, 60)
    buf = crossfade_loop(buf, fade=int(0.2 * SR))
    return soft_peak_limit(buf, 0.05)


def parking_bed(seconds: float = 7.0, seed: int = 99) -> list[float]:
    """Distant garage — quieter rumble, sparse pass-bys (not brown wall)."""
    rng = random.Random(seed)
    n = int(seconds * SR)
    base = brown_noise(n, rng, leak=0.9988)
    base = one_pole_lp(base, 350)
    buf = [0.025 * v for v in base]
    t = rng.uniform(0.8, 2.0)
    while t < seconds - 1.2:
        rumble_whoosh(buf, int(t * SR), int(rng.uniform(0.8, 1.5) * SR), 0.05, rng)
        t += rng.uniform(2.2, 4.0)
    buf = crossfade_loop(buf, fade=int(0.2 * SR))
    return soft_peak_limit(buf, 0.08)


def utility_bed(seconds: float = 5.0, seed: int = 111) -> list[float]:
    rng = random.Random(seed)
    n = int(seconds * SR)
    base = brown_noise(n, rng, leak=0.999)
    base = one_pole_lp(base, 280)
    buf = [0.02 * v for v in base]
    t = rng.uniform(0.5, 1.5)
    while t < seconds - 0.3:
        soft_impulse(buf, int(t * SR), width=rng.randint(40, 100), amp=0.03, rng=rng)
        t += rng.uniform(1.2, 2.8)
    buf = crossfade_loop(buf, fade=int(0.15 * SR))
    return soft_peak_limit(buf, 0.06)


def stairs_bed(seconds: float = 5.0, seed: int = 133) -> list[float]:
    rng = random.Random(seed)
    n = int(seconds * SR)
    buf = [0.0] * n
    t = rng.uniform(0.4, 1.0)
    while t < seconds - 0.2:
        soft_impulse(buf, int(t * SR), width=rng.randint(50, 110), amp=0.06, rng=rng)
        t += rng.uniform(0.35, 0.9)
    buf = one_pole_lp(buf, 1200)
    buf = crossfade_loop(buf, fade=int(0.12 * SR))
    return soft_peak_limit(normalize(buf, 0.08), 0.08)


def soft_peak_limit(buf: list[float], peak: float) -> list[float]:
    m = max(abs(v) for v in buf) or 1.0
    if m <= peak:
        return buf
    scale = peak / m
    return [v * scale for v in buf]


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
    rng = random.Random(202)
    n = int(0.28 * SR)
    buf = [0.0] * n
    for i in range(n):
        t = i / SR
        env = math.exp(-14.0 * t)
        ding = (
            0.45 * math.sin(2 * math.pi * 880 * t)
            + 0.22 * math.sin(2 * math.pi * 1320 * t)
            + 0.12 * math.sin(2 * math.pi * 1760 * t)
        )
        body = 0.25 * rng.uniform(-1, 1)
        buf[i] = env * (ding + body)
    buf = one_pole_lp(buf, 4200)
    return normalize(buf, 0.28)


def sfx_elevator_door() -> list[float]:
    rng = random.Random(303)
    n = int(0.55 * SR)
    buf = [0.0] * n
    rumble_whoosh(buf, 0, n, 0.55, rng)
    soft_impulse(buf, int(0.08 * SR), 90, 0.35, rng)
    soft_impulse(buf, int(0.42 * SR), 110, 0.4, rng)
    buf = one_pole_lp(buf, 900)
    buf = one_pole_hp(buf, 80)
    return normalize(buf, 0.32)


def add_distant_chirp(buf: list[float], start: int, rng: random.Random) -> None:
    n = len(buf)
    notes = rng.randint(1, 3)
    pos = start
    base_f = rng.uniform(2200, 4800)
    for note in range(notes):
        chirp_len = rng.uniform(0.035, 0.09)
        f0 = base_f * rng.uniform(0.92, 1.08)
        f1 = f0 * rng.uniform(0.88, 1.18)
        amp = rng.uniform(0.012, 0.028)
        length = int(chirp_len * SR)
        for i in range(length):
            idx = pos + i
            if idx >= n:
                break
            u = i / max(1, length - 1)
            env = (math.sin(math.pi * u) ** 1.6) * (0.7 + 0.3 * (1.0 - note * 0.15))
            freq = f0 + (f1 - f0) * u
            tone = math.sin(2 * math.pi * freq * (i / SR))
            buf[idx] += amp * env * tone
        pos += length + int(rng.uniform(0.02, 0.07) * SR)


def outdoor_bed(seconds: float = 12.0, seed: int = 19) -> list[float]:
    rng = random.Random(seed)
    n = int(seconds * SR)
    breeze = brown_noise(n, rng, leak=0.999)
    breeze = one_pole_lp(breeze, 160)
    breeze = [0.008 * v for v in breeze]
    buf = breeze[:]
    for bird in range(4):
        bird_rng = random.Random(seed * 17 + bird * 91)
        t = bird_rng.uniform(0.8, 2.5 + bird * 0.4)
        while t < seconds - 0.5:
            add_distant_chirp(buf, int(t * SR), bird_rng)
            if bird_rng.random() < 0.22:
                add_distant_chirp(buf, int((t + bird_rng.uniform(0.15, 0.35)) * SR), bird_rng)
            t += bird_rng.uniform(2.6, 5.5)
    buf = one_pole_lp(buf, 3200)
    buf = crossfade_loop(buf, fade=int(0.25 * SR))
    return soft_peak_limit(buf, 0.09)


CHATTER_PROFILES = {
    "office": dict(seconds=7.0, seed=11, density=1.0, amp=0.045, room_tone=0.012, accent_rate=2.5, accent_amp=0.035),
    "restaurant": dict(seconds=7.0, seed=22, density=1.6, amp=0.055, room_tone=0.014, accent_rate=1.2, accent_amp=0.03),
    "retail": dict(seconds=7.0, seed=33, density=0.9, amp=0.04, room_tone=0.012, accent_rate=1.0, accent_amp=0.025),
    "condo": dict(seconds=8.0, seed=44, density=0.7, amp=0.035, room_tone=0.01, accent_rate=0.4, accent_amp=0.02),
    "hotel": dict(seconds=8.0, seed=55, density=0.35, amp=0.025, room_tone=0.01, accent_rate=0.25, accent_amp=0.015),
    "conference": dict(seconds=7.0, seed=66, density=0.85, amp=0.04, room_tone=0.012, accent_rate=0.5, accent_amp=0.02),
    "event": dict(seconds=7.0, seed=77, density=1.8, amp=0.05, room_tone=0.015, accent_rate=0.6, accent_amp=0.025),
    "lobby": dict(seconds=7.0, seed=122, density=1.1, amp=0.04, room_tone=0.012, accent_rate=0.8, accent_amp=0.02),
}


def main() -> None:
    root = Path(__file__).resolve().parents[1]
    amb = root / "Assets" / "Resources" / "Audio" / "Ambience"
    sfx = root / "Assets" / "Resources" / "Audio" / "Sfx"

    outdoor = outdoor_bed()
    write_wav(amb / "outdoor.wav", outdoor)
    print(f"wrote outdoor.wav ({len(outdoor)/SR:.1f}s)")

    for name, kwargs in CHATTER_PROFILES.items():
        samples = chatter_bed(**kwargs)
        write_wav(amb / f"{name}.wav", samples)
        print(f"wrote {name}.wav chatter ({len(samples)/SR:.1f}s)")

    write_wav(amb / "elevator.wav", motor_whir_bed())
    print("wrote elevator.wav motor whir")
    write_wav(amb / "parking.wav", parking_bed())
    print("wrote parking.wav")
    write_wav(amb / "utility.wav", utility_bed())
    print("wrote utility.wav")
    write_wav(amb / "stairs.wav", stairs_bed())
    print("wrote stairs.wav")

    write_wav(sfx / "build_place.wav", sfx_build_place())
    write_wav(sfx / "elevator_ping.wav", sfx_elevator_ping())
    write_wav(sfx / "elevator_door.wav", sfx_elevator_door())
    print("wrote SFX")


if __name__ == "__main__":
    main()
