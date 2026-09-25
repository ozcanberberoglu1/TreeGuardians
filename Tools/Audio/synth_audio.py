#!/usr/bin/env python3
"""Tree Guardians procedural audio (stdlib only).

Renders every SFX, ambience loop and music loop used by the game into
Assets/TreeGuardians/Audio/{SFX,Ambience,Music}. Deterministic (fixed seeds),
mono 16-bit 44.1 kHz, 20 Hz DC high-pass and TPDF dither on write.
Loops are seamless: every event is mixed with wrap-around, and the reverb and
DC filters are pre-rolled with the loop's own tail.

usage: python3 Tools/Audio/synth_audio.py [--only name1,name2] [--list] [--sfx] [--loops]
"""
import math, os, random, sys, wave, zlib
from array import array

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "Assets", "TreeGuardians", "Audio")
TAU = 2.0 * math.pi

# ----------------------------------------------------------------------------- basics
def zeros(n): return [0.0] * n

def mix_into(dst, src, start=0, gain=1.0):
    """Adds src into dst at 'start' (samples); anything outside dst is dropped."""
    n = len(dst); L = len(src)
    a = max(0, start); b = min(n, start + L)
    if b <= a: return
    o = a - start
    if gain == 1.0: dst[a:b] = [p + q for p, q in zip(dst[a:b], src[o:o + (b - a)])]
    else: dst[a:b] = [p + q * gain for p, q in zip(dst[a:b], src[o:o + (b - a)])]

def mix_wrap(dst, src, start=0, gain=1.0):
    """Adds src into dst, wrapping past the end back to the start (seamless loops)."""
    n = len(dst); L = len(src); i = 0
    j = start % n
    while i < L:
        m = min(L - i, n - j)
        dst[j:j + m] = [p + q * gain for p, q in zip(dst[j:j + m], src[i:i + m])]
        i += m; j = 0

def peak(x): return max((abs(v) for v in x), default=0.0)

def normalize(x, peak_db=-1.0):
    m = peak(x)
    if m <= 1e-9: return x
    g = (10 ** (peak_db / 20.0)) / m
    return [v * g for v in x]

def norm_to(x, level):
    """Scales a layer so its peak is 'level' (linear). Keeps recipes independent of primitive gains."""
    m = peak(x)
    if m <= 1e-9: return x
    g = level / m
    return [v * g for v in x]

def fade(x, sr, fin=0.004, fout=0.012):
    n = len(x); a = max(1, int(fin * sr)); b = max(1, int(fout * sr))
    for i in range(min(a, n)): x[i] *= i / a
    for i in range(min(b, n)): x[n - 1 - i] *= i / b
    return x

def tail_fade(x, sr, sec=0.012):
    """Fades only the last 'sec' so a truncated decaying note ends without a click."""
    n = len(x); b = min(n, max(1, int(sec * sr)))
    for i in range(b): x[n - 1 - i] *= i / b
    return x

def write_wav(path, x, sr, seed=0):
    """16-bit PCM with TPDF dither (+-1 LSB triangular) and rounding."""
    os.makedirs(os.path.dirname(path), exist_ok=True)
    rng = random.Random(seed)
    r = rng.random
    a = array("h", bytes(2 * len(x)))
    for i, v in enumerate(x):
        s = int(round(v * 32767.0 + (r() - r())))
        a[i] = 32767 if s > 32767 else (-32768 if s < -32768 else s)
    if sys.byteorder == "big": a.byteswap()
    with wave.open(path, "wb") as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(sr)
        w.writeframes(a.tobytes())

# ----------------------------------------------------------------------------- envelopes
def env_exp(n, sr, decay, attack=0.002):
    a = max(1, int(attack * sr)); k = math.exp(-1.0 / max(1e-5, decay * sr))
    out = zeros(n); v = 1.0
    for i in range(n):
        out[i] = (i / a if i < a else 1.0) * v
        if i >= a: v *= k
    return out

def env_adsr(n, sr, a, d, s, r):
    A = int(a * sr); D = int(d * sr); R = int(r * sr); S = max(0, n - A - D - R)
    out = []
    for i in range(A): out.append(i / max(1, A))
    for i in range(D): out.append(1.0 - (1.0 - s) * i / max(1, D))
    out += [s] * S
    for i in range(R): out.append(s * (1.0 - i / max(1, R)))
    return (out + [0.0] * n)[:n]

def decay_burst(x, sr, tau):
    """Multiplies x by exp(-t/tau) (used for clicks and cracks)."""
    return [v * math.exp(-i / (tau * sr)) for i, v in enumerate(x)]

# ----------------------------------------------------------------------------- sources
def sine_sweep(n, sr, f0, f1, curve=1.0, phase=0.0):
    out = zeros(n); ph = phase
    for i in range(n):
        t = (i / max(1, n - 1)) ** curve
        f = f0 + (f1 - f0) * t
        ph += TAU * f / sr
        out[i] = math.sin(ph)
    return out

def tone(n, sr, f, partials=((1, 1.0),), detune=0.0):
    out = zeros(n)
    for mult, amp in partials:
        w = TAU * f * mult * (1.0 + detune) / sr
        for i in range(n): out[i] += amp * math.sin(w * i)
    return out

def noise(n, rng): return [rng.uniform(-1.0, 1.0) for _ in range(n)]

def pink(n, rng):
    b0 = b1 = b2 = 0.0; out = zeros(n)
    for i in range(n):
        w = rng.uniform(-1, 1)
        b0 = 0.99765 * b0 + w * 0.0990460
        b1 = 0.96300 * b1 + w * 0.2965164
        b2 = 0.57000 * b2 + w * 1.0526913
        out[i] = (b0 + b1 + b2 + w * 0.1848) * 0.18
    return out

def brown(n, rng):
    out = zeros(n); v = 0.0
    for i in range(n):
        v = (v + rng.uniform(-1, 1) * 0.02) * 0.998
        out[i] = v * 3.0
    return out

def resample(x, ratio):
    """Linear-interpolation resample: ratio > 1 plays higher and shorter."""
    L = len(x); n = max(1, int((L - 1) / ratio)); out = zeros(n)
    for i in range(n):
        p = i * ratio; k = int(p); f = p - k
        a = x[k]; b = x[k + 1] if k + 1 < L else 0.0
        out[i] = a + (b - a) * f
    return out

def blips(n, sr, rng, count, fmin, fmax, tau, t0, t1, amp=1.0):
    """Short decaying sine sparkles scattered between t0 and t1."""
    out = zeros(n)
    for _ in range(count):
        at = int(rng.uniform(t0, t1) * sr); f = rng.uniform(fmin, fmax)
        w = TAU * f / sr; k = math.exp(-1.0 / (tau * sr)); v = amp * rng.uniform(0.5, 1.0)
        for j in range(int(tau * 6 * sr)):
            if at + j >= n: break
            out[at + j] += math.sin(w * j) * v * min(1.0, j / 20.0)
            v *= k
    return out

# ----------------------------------------------------------------------------- filters
def lowpass1(x, sr, fc):
    a = 1.0 - math.exp(-TAU * fc / sr); y = 0.0; out = zeros(len(x))
    for i, v in enumerate(x):
        y += a * (v - y); out[i] = y
    return out

def highpass1(x, sr, fc):
    lp = lowpass1(x, sr, fc)
    return [a - b for a, b in zip(x, lp)]

def biquad(x, sr, kind, f, q, f_end=None, curve=1.0, sweep_len=None, path=None):
    """RBJ biquad. f_end sweeps the cutoff f -> f_end over 'sweep_len' samples (default: the whole buffer);
    'path' (u in 0..1 -> Hz) overrides both for arbitrary trajectories."""
    n = len(x); out = zeros(n); x1 = x2 = y1 = y2 = 0.0
    L = sweep_len if sweep_len else n
    step = 32; b0 = b1 = b2 = a1 = a2 = 0.0
    for i in range(n):
        if i % step == 0:
            if path is not None: fc = path(i / max(1, n - 1))
            elif f_end is None: fc = f
            else: fc = f + (f_end - f) * (min(1.0, i / max(1, L - 1)) ** curve)
            fc = min(max(fc, 20.0), sr * 0.45)
            w0 = TAU * fc / sr; al = math.sin(w0) / (2 * q); c = math.cos(w0)
            if kind == "lp":
                b0 = (1 - c) / 2; b1 = 1 - c; b2 = (1 - c) / 2
            elif kind == "hp":
                b0 = (1 + c) / 2; b1 = -(1 + c); b2 = (1 + c) / 2
            else:  # band-pass, constant peak gain
                b0 = al; b1 = 0.0; b2 = -al
            a0 = 1 + al; a1 = -2 * c; a2 = 1 - al
            b0 /= a0; b1 /= a0; b2 /= a0; a1 /= a0; a2 /= a0
        v = x[i]
        y = b0 * v + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2
        x2, x1, y2, y1 = x1, v, y1, y
        out[i] = y
    return out

def resonator(n, sr, f, decay, amp=1.0, excite=None):
    """Damped mode (wood/metal partial)."""
    r = math.exp(-1.0 / max(1e-4, decay * sr)); w = TAU * f / sr
    c1 = 2 * r * math.cos(w); c2 = -r * r
    out = zeros(n); y1 = y2 = 0.0
    for i in range(n):
        e = (excite[i] if excite is not None and i < len(excite) else (1.0 if i == 0 else 0.0))
        y = e + c1 * y1 + c2 * y2
        y2, y1 = y1, y
        out[i] = y * amp * (1 - r)
    return out

def softclip(x, drive=1.5):
    k = math.tanh(drive)
    return [math.tanh(v * drive) / k for v in x]

def mul(a, b): return [p * q for p, q in zip(a, b)]

def reverb(x, sr, room=0.78, damp=0.35, wet=0.22, loop=False):
    """Small Schroeder reverb. loop=True pre-rolls the loop's last 4 s so the tail wraps seamlessly."""
    n = len(x)
    if loop:
        pre = min(n, int(sr * 4.0))
        src = x[n - pre:] + x
    else:
        pre = 0
        src = x + zeros(int(sr * 1.2))
    L = len(src)
    s = sr / 44100.0
    combs = [int(d * s) for d in (1557, 1617, 1491, 1422)]
    aps = [int(d * s) for d in (556, 225)]
    wetbuf = zeros(L)
    d1 = 1.0 - damp
    for d in combs:
        buf = zeros(d); idx = 0; filt = 0.0
        for i in range(L):
            y = buf[idx]
            filt = y * d1 + filt * damp
            buf[idx] = src[i] + filt * room
            wetbuf[i] += y
            idx += 1
            if idx == d: idx = 0
    for d in aps:
        buf = zeros(d); idx = 0; out = zeros(L)
        for i in range(L):
            v = wetbuf[i]
            b = buf[idx]
            out[i] = -v + b
            buf[idx] = v + b * 0.5
            idx += 1
            if idx == d: idx = 0
        wetbuf = out
    g = 0.25 * wet; dry = 1.0 - wet
    if loop:
        return [x[i] * dry + wetbuf[pre + i] * g for i in range(n)]
    return [(src[i] if i < n else 0.0) * dry + wetbuf[i] * g for i in range(L)]

def dc_block(x, sr, loop=False):
    """20 Hz one-pole high-pass. For loops it is pre-rolled with the loop tail so the seam stays continuous."""
    if not loop: return highpass1(x, sr, 20.0)
    pre = min(len(x), int(sr * 1.0))
    y = highpass1(x[len(x) - pre:] + x, sr, 20.0)
    return y[pre:]

def trim_tail(x, sr, thresh_db=-60.0):
    t = 10 ** (thresh_db / 20.0) * max((abs(v) for v in x), default=1.0)
    end = len(x)
    while end > 1 and abs(x[end - 1]) < t: end -= 1
    return x[:min(len(x), end + int(0.01 * sr))]

def secs(sr, s): return max(1, int(sr * s))

# ----------------------------------------------------------------------------- instruments
_CACHE = {}

def marimba(sr, f, dur=0.9, vel=1.0, rng=None):
    n = secs(sr, dur)
    body = tone(n, sr, f, ((1, 1.0),))
    e1 = env_exp(n, sr, 0.28 * (440.0 / max(110.0, f)) ** 0.3, 0.001)
    p4 = tone(n, sr, f, ((3.93, 0.35),)); e4 = env_exp(n, sr, 0.05, 0.001)
    p10 = tone(n, sr, f, ((9.2, 0.12),)); e10 = env_exp(n, sr, 0.015, 0.0005)
    out = [(body[i] * e1[i] + p4[i] * e4[i] + p10[i] * e10[i]) * vel for i in range(n)]
    if rng is not None:
        click = lowpass1([v * math.exp(-i / (0.002 * sr)) for i, v in enumerate(noise(secs(sr, 0.01), rng))], sr, 3500)
        mix_into(out, click, 0, 0.25 * vel)
    return tail_fade(out, sr, 0.015)

def marimba_note(sr, f, dur):
    """Cached unit-velocity marimba (music): scale with the mix gain."""
    key = ("mar", sr, round(f, 3), round(dur, 3))
    if key not in _CACHE: _CACHE[key] = marimba(sr, f, dur, 1.0, random.Random(int(f * 10)))
    return _CACHE[key]

def pluck(sr, f, dur=0.6, bright=0.5, vel=1.0, seed=0):
    """Karplus-Strong string."""
    rng = random.Random(seed)
    n = secs(sr, dur); period = max(2, int(sr / f))
    buf = [rng.uniform(-1, 1) for _ in range(period)]
    buf = lowpass1(buf, sr, 1500 + 6000 * bright)
    out = zeros(n); idx = 0; prev = 0.0; decay = 0.996
    for i in range(n):
        v = buf[idx]
        nv = decay * 0.5 * (v + prev)
        prev = v; buf[idx] = nv; idx = (idx + 1) % period
        out[i] = v * vel
    return fade(out, sr, 0.001, 0.05)

def pluck_note(sr, f, dur, bright=0.6):
    key = ("plk", sr, round(f, 3), round(dur, 3), bright)
    if key not in _CACHE: _CACHE[key] = pluck(sr, f, dur, bright, 1.0, int(f * 7) % 9973)
    return _CACHE[key]

_PAD_TABLE = None

def _pad_table():
    global _PAD_TABLE
    if _PAD_TABLE is None:
        N = 4096
        _PAD_TABLE = [math.sin(TAU * i / N) + 0.28 * math.sin(2 * TAU * i / N) + 0.09 * math.sin(3 * TAU * i / N) for i in range(N + 1)]
    return _PAD_TABLE

def pad(sr, freqs, dur, vel=0.5, attack=0.8, release=1.0):
    """Warm detuned pad (3 voices per note) from an interpolated wavetable; cached per chord."""
    key = ("pad", sr, tuple(round(f, 3) for f in freqs), round(dur, 4), vel, attack, release)
    if key in _CACHE: return _CACHE[key]
    n = secs(sr, dur); out = zeros(n)
    T = _pad_table(); N = len(T) - 1
    for k, f in enumerate(freqs):
        for det in (-0.004, 0.0, 0.0045):
            inc = f * (1 + det) / sr * N
            ph = (((k * 1.7 + det * 400) / TAU) % 1.0) * N
            for i in range(n):
                j = int(ph); fr = ph - j; a = T[j]
                out[i] += a + (T[j + 1] - a) * fr
                ph += inc
                if ph >= N: ph -= N
    env = env_adsr(n, sr, attack, 0.3, 0.85, release)
    g = vel / (len(freqs) * 3)
    w = TAU * 0.25 / sr
    res = [out[i] * env[i] * (1.0 + 0.06 * math.sin(w * i)) * g for i in range(n)]
    _CACHE[key] = res
    return res

def kick_click(sr, rng):
    L = secs(sr, 0.03)
    c = biquad(noise(L, rng), sr, "bp", 2500, 1.5)
    return decay_burst(c, sr, 0.004)

def kick(sr, vel=1.0, rng=None):
    """Phone-speaker friendly kick: 160->55 Hz body plus a 2.5 kHz beater click."""
    key = ("kick", sr)
    if key not in _CACHE:
        r = random.Random(5)
        n = secs(sr, 0.35)
        body = sine_sweep(n, sr, 160, 55, 0.35); e = env_exp(n, sr, 0.12, 0.001)
        ov = sine_sweep(n, sr, 320, 110, 0.35); eo = env_exp(n, sr, 0.03, 0.001)
        out = [body[i] * e[i] + ov[i] * eo[i] * 0.25 for i in range(n)]
        mix_into(out, norm_to(kick_click(sr, r), 0.35))
        _CACHE[key] = tail_fade(out, sr, 0.01)
    return [v * vel for v in _CACHE[key]]

def snare(sr, rng, vel=1.0):
    n = secs(sr, 0.22)
    nz = highpass1(noise(n, rng), sr, 1800); e = env_exp(n, sr, 0.06, 0.001)
    t = tone(n, sr, 190, ((1, 1.0), (1.6, 0.4))); et = env_exp(n, sr, 0.04, 0.001)
    return tail_fade([(nz[i] * e[i] * 0.7 + t[i] * et[i] * 0.5) * vel for i in range(n)], sr, 0.01)

def hat(sr, rng, vel=1.0, open_=False):
    n = secs(sr, 0.18 if open_ else 0.05)
    nz = highpass1(highpass1(noise(n, rng), sr, 6000), sr, 6000)
    e = env_exp(n, sr, 0.06 if open_ else 0.012, 0.0005)
    return [nz[i] * e[i] * vel for i in range(n)]

def shaker(sr, rng, vel=1.0):
    n = secs(sr, 0.09)
    nz = biquad(noise(n, rng), sr, "bp", 7000, 1.2)
    e = env_adsr(n, sr, 0.02, 0.02, 0.4, 0.05)
    return [nz[i] * e[i] * vel * 2.0 for i in range(n)]

def variants(key, count, maker):
    """A few pre-rendered noise-drum variants; picked at random while sequencing."""
    if key not in _CACHE: _CACHE[key] = [maker(random.Random(zlib.crc32(key.encode()) % 1000 + k)) for k in range(count)]
    return _CACHE[key]

NOTE_INDEX = {"C": 0, "C#": 1, "Db": 1, "D": 2, "D#": 3, "Eb": 3, "E": 4, "F": 5, "F#": 6, "Gb": 6, "G": 7, "G#": 8, "Ab": 8,
              "A": 9, "A#": 10, "Bb": 10, "B": 11}

def note_hz(name):
    pitch, octave = name[:-1], int(name[-1])
    midi = 12 * (octave + 1) + NOTE_INDEX[pitch]
    return 440.0 * 2 ** ((midi - 69) / 12.0)

# ----------------------------------------------------------------------------- SFX recipes (44.1 kHz)
SR = 44100

def sfx_ui_click():
    n = secs(SR, 0.07)
    t = tone(n, SR, 1250, ((1, 1.0), (2.01, 0.35))); e = env_exp(n, SR, 0.018, 0.0008)
    return [t[i] * e[i] for i in range(n)]

def sfx_card_select():
    rng = random.Random(3)
    a = marimba(SR, note_hz("E5"), 0.35, 0.9, rng); b = marimba(SR, note_hz("B5"), 0.3, 0.6, rng)
    out = zeros(secs(SR, 0.42)); mix_into(out, a); mix_into(out, b, secs(SR, 0.045)); return out

def sfx_panel_open():
    rng = random.Random(5); n = secs(SR, 0.26)
    sw = biquad(noise(n, rng), SR, "bp", 600, 2.5, 2600, 0.8); e = env_adsr(n, SR, 0.05, 0.08, 0.5, 0.12)
    tn = sine_sweep(n, SR, 420, 880, 0.7); et = env_exp(n, SR, 0.09, 0.01)
    return [sw[i] * e[i] * 0.9 + tn[i] * et[i] * 0.35 for i in range(n)]

def sfx_panel_close():
    rng = random.Random(6); n = secs(SR, 0.22)
    sw = biquad(noise(n, rng), SR, "bp", 2200, 2.5, 500, 0.8); e = env_adsr(n, SR, 0.02, 0.06, 0.5, 0.12)
    tn = sine_sweep(n, SR, 760, 380, 0.7); et = env_exp(n, SR, 0.07, 0.005)
    return [sw[i] * e[i] * 0.8 + tn[i] * et[i] * 0.3 for i in range(n)]

def sfx_ui_error():
    """Soft descending double wood 'bonk' (233 -> 185 Hz), low-passed; replaces the old square-ish buzzer."""
    rng = random.Random(7)
    out = zeros(secs(SR, 0.36))
    a = marimba(SR, 233.0, 0.16, 0.9, rng); a = [v * e for v, e in zip(a, env_exp(len(a), SR, 0.07, 0.001))]
    b = marimba(SR, 185.0, 0.22, 0.9, rng); b = [v * e for v, e in zip(b, env_exp(len(b), SR, 0.09, 0.001))]
    mix_into(out, tail_fade(a, SR, 0.03)); mix_into(out, tail_fade(b, SR, 0.05), secs(SR, 0.10))
    out = lowpass1(out, SR, 1600)
    ck = decay_burst(noise(secs(SR, 0.004), rng), SR, 0.0006)
    mix_into(out, norm_to(ck, 0.2 * peak(out)))
    return out

def sfx_aim_start():
    rng = random.Random(8); n = secs(SR, 0.16)
    tick = tone(n, SR, 2100, ((1, 1.0),)); et = env_exp(n, SR, 0.01, 0.0005)
    sw = biquad(noise(n, rng), SR, "bp", 1200, 3.0, 2500); e = env_adsr(n, SR, 0.03, 0.03, 0.3, 0.08)
    return [tick[i] * et[i] * 0.5 + sw[i] * e[i] * 0.5 for i in range(n)]

def sfx_launch(seed=11, lo=520, hi=1700, dur=0.34):
    rng = random.Random(seed); n = secs(SR, dur)
    sw = biquad(noise(n, rng), SR, "bp", hi, 1.8, lo, 0.6); e = env_adsr(n, SR, 0.01, 0.06, 0.45, dur * 0.6)
    th = sine_sweep(n, SR, 150, 60, 0.5); et = env_exp(n, SR, 0.05, 0.001)
    tw = biquad(noise(n, rng), SR, "bp", 3200, 6.0); etw = env_exp(n, SR, 0.02, 0.001)
    return [sw[i] * e[i] * 1.1 + th[i] * et[i] * 0.6 + tw[i] * etw[i] * 0.4 for i in range(n)]

def wood_knock(rng, n, base, bright=1.0):
    exc = [v * math.exp(-i / (0.0015 * SR)) for i, v in enumerate(noise(secs(SR, 0.01), rng))]
    out = zeros(n)
    for mult, dec, amp in ((1.0, 0.09, 1.0), (2.31, 0.05, 0.7), (3.87, 0.03, 0.45), (5.9, 0.018, 0.3 * bright)):
        f = base * mult * rng.uniform(0.97, 1.03)
        mix_into(out, resonator(n, SR, f, dec, 60.0 * amp, exc))
    return tail_fade(out, SR, 0.006)

def crackle(rng, n, density, dur_ms=3.0, gain=0.6):
    out = zeros(n)
    for i in range(n):
        if rng.random() < density * (1.0 - i / n) ** 1.5:
            L = secs(SR, dur_ms / 1000.0 * rng.uniform(0.5, 1.6))
            amp = rng.uniform(0.3, 1.0) * gain
            for k in range(L):
                if i + k < n: out[i + k] += rng.uniform(-1, 1) * amp * math.exp(-k / (L * 0.3))
    return highpass1(out, SR, 900)

def crack_burst(rng, ms, hp, tau_ms):
    """A few ms of high-passed noise with a fast exponential decay (wood/whip crack transient)."""
    return decay_burst(highpass1(noise(secs(SR, ms / 1000.0), rng), SR, hp), SR, tau_ms / 1000.0)

def sfx_wall_hit(seed):
    rng = random.Random(seed); n = secs(SR, 0.42)
    knock = wood_knock(rng, n, rng.uniform(150, 190))
    crk = crackle(rng, n, 0.004, 3.0, 0.5)
    thud = sine_sweep(n, SR, 110, 55, 0.4); et = env_exp(n, SR, 0.06, 0.001)
    dust = lowpass1(noise(n, rng), SR, 900); ed = env_adsr(n, SR, 0.005, 0.05, 0.25, 0.25)
    return [knock[i] * 0.9 + crk[i] + thud[i] * et[i] * 0.7 + dust[i] * ed[i] * 0.35 for i in range(n)]

def sfx_wall_break(seed=21):
    rng = random.Random(seed); n = secs(SR, 1.1)
    out = zeros(n)
    for k in range(3):
        mix_into(out, wood_knock(rng, secs(SR, 0.5), rng.uniform(95, 140), 1.2), secs(SR, 0.05 * k + rng.uniform(0, 0.03)), 1.0 - 0.2 * k)
    crk = crackle(rng, n, 0.012, 4.0, 0.7)
    boom = sine_sweep(n, SR, 90, 38, 0.35); eb = env_exp(n, SR, 0.18, 0.002)
    rumble = lowpass1(noise(n, rng), SR, 400); er = env_adsr(n, SR, 0.01, 0.15, 0.35, 0.7)
    debris = zeros(n)
    for _ in range(18):
        at = secs(SR, rng.uniform(0.15, 0.95)); L = secs(SR, 0.06)
        mix_into(debris, wood_knock(rng, L, rng.uniform(600, 1400), 1.5), at, rng.uniform(0.15, 0.4))
    return softclip([out[i] + crk[i] + boom[i] * eb[i] * 0.9 + rumble[i] * er[i] * 0.6 + debris[i] for i in range(n)], 1.3)

def sfx_explosion():
    rng = random.Random(31); n = secs(SR, 1.2)
    nz = biquad(noise(n, rng), SR, "lp", 3000, 0.8, 180, 0.5); e = env_adsr(n, SR, 0.003, 0.12, 0.35, 0.9)
    boom = sine_sweep(n, SR, 70, 32, 0.3); eb = env_exp(n, SR, 0.25, 0.002)
    crk = crackle(rng, n, 0.006, 3.0, 0.35)
    return softclip([nz[i] * e[i] * 1.2 + boom[i] * eb[i] * 1.0 + crk[i] for i in range(n)], 1.8)

def sfx_debris():
    rng = random.Random(41); n = secs(SR, 0.6); out = zeros(n)
    for _ in range(10):
        mix_into(out, wood_knock(rng, secs(SR, 0.07), rng.uniform(700, 1600), 1.4), secs(SR, rng.uniform(0.0, 0.5)), rng.uniform(0.2, 0.6))
    return out

def formant_voice(n, f0, f1, formants, seed=0, vib_hz=0.0, vib_depth=0.0):
    rng = random.Random(seed)
    saw = zeros(n); ph = 0.0
    for i in range(n):
        f = f0 + (f1 - f0) * (i / max(1, n - 1))
        if vib_depth > 0.0: f *= 1.0 + vib_depth * math.sin(TAU * vib_hz * i / SR)
        f += rng.uniform(-3, 3)
        ph = (ph + f / SR) % 1.0
        saw[i] = 2 * ph - 1
    out = zeros(n)
    for fc, q, g in formants: mix_into(out, biquad(saw, SR, "bp", fc, q), 0, g)
    return out

def sfx_guardian_hurt():
    n = secs(SR, 0.26)
    v = formant_voice(n, 300, 190, ((650, 6, 1.0), (1100, 7, 0.6), (2500, 9, 0.2)), 5)
    e = env_adsr(n, SR, 0.01, 0.05, 0.6, 0.12)
    return [v[i] * e[i] for i in range(n)]

def sfx_guardian_death():
    rng = random.Random(51); n = secs(SR, 0.7)
    v = formant_voice(n, 280, 110, ((600, 5, 1.0), (1000, 6, 0.5)), 9); ev = env_adsr(n, SR, 0.01, 0.1, 0.5, 0.4)
    puff = biquad(noise(n, rng), SR, "bp", 1400, 1.2, 300, 0.7); ep = env_adsr(n, SR, 0.02, 0.1, 0.3, 0.4)
    return [v[i] * ev[i] * 0.8 + puff[i] * ep[i] * 0.9 for i in range(n)]

def bell(n, f, decay=0.6, amp=1.0):
    out = zeros(n)
    for mult, d, a in ((1.0, 1.0, 1.0), (2.76, 0.55, 0.5), (5.4, 0.3, 0.25), (8.93, 0.18, 0.12)):
        if f * mult >= SR * 0.45: continue
        t = tone(n, SR, f * mult, ((1, 1.0),)); e = env_exp(n, SR, decay * d, 0.001)
        for i in range(n): out[i] += t[i] * e[i] * a * amp
    return tail_fade(out, SR, 0.01)

def sfx_crit():
    rng = random.Random(61); n = secs(SR, 0.7)
    b = bell(n, 1660, 0.35); k = wood_knock(rng, n, 210)
    sw = sine_sweep(n, SR, 900, 2400, 0.4); es = env_exp(n, SR, 0.06, 0.001)
    return [b[i] * 0.55 + k[i] * 0.7 + sw[i] * es[i] * 0.25 for i in range(n)]

def arp(notes, step, dur, vel=0.9, inst="marimba", seed=0, tail=0.8):
    rng = random.Random(seed)
    out = zeros(secs(SR, step * (len(notes) - 1) + max(dur, step) + tail))
    for k, name in enumerate(notes):
        if inst == "bell": s = bell(secs(SR, dur), note_hz(name), dur * 0.6, vel)
        elif inst == "pluck": s = pluck(SR, note_hz(name), dur, 0.7, vel, seed + k)
        else: s = marimba(SR, note_hz(name), dur, vel, rng)
        mix_into(out, s, secs(SR, step * k))
    return out

def sfx_victory():
    lead = arp(["C5", "E5", "G5", "C6"], 0.11, 0.8, 0.9, "marimba", 1, 1.2)
    chord = pad(SR, [note_hz(n) for n in ("C4", "E4", "G4", "C5")], 1.4, 1.2, 0.05, 0.9)
    bells = arp(["G5", "C6", "E6"], 0.09, 1.0, 0.35, "bell", 2, 1.0)
    out = zeros(max(len(lead), len(chord) + secs(SR, 0.44), len(bells) + secs(SR, 0.5)))
    mix_into(out, lead); mix_into(out, chord, secs(SR, 0.44), 0.8); mix_into(out, bells, secs(SR, 0.5))
    return reverb(out, SR, 0.8, 0.3, 0.25)

def sfx_defeat():
    lead = arp(["G4", "D#4", "C4"], 0.26, 0.9, 0.8, "marimba", 3, 1.2)
    chord = pad(SR, [note_hz(n) for n in ("C3", "D#3", "G3")], 1.6, 1.0, 0.1, 1.0)
    out = zeros(max(len(lead), len(chord) + secs(SR, 0.6))); mix_into(out, lead); mix_into(out, chord, secs(SR, 0.6), 0.8)
    return reverb(lowpass1(out, SR, 2600), SR, 0.82, 0.4, 0.3)

def sfx_chest_open():
    rng = random.Random(71); n = secs(SR, 0.45)
    creak = formant_voice(n, 55, 85, ((380, 4, 1.0), (900, 5, 0.4)), 13); ec = env_adsr(n, SR, 0.05, 0.1, 0.6, 0.15)
    creak = [creak[i] * ec[i] * 0.6 for i in range(n)]
    thud = wood_knock(rng, secs(SR, 0.3), 120)
    sparkle = arp(["E6", "G6", "B6", "E7", "G6", "B6"], 0.05, 0.5, 0.35, "bell", 5, 0.8)
    out = zeros(len(creak) + len(sparkle)); mix_into(out, creak); mix_into(out, thud, secs(SR, 0.38), 0.8); mix_into(out, sparkle, secs(SR, 0.42))
    return reverb(out, SR, 0.75, 0.3, 0.2)

def sfx_upgrade():
    rise = arp(["C5", "E5", "G5", "B5", "D6"], 0.06, 0.7, 0.7, "marimba", 6, 0.9)
    sh = arp(["G6", "B6", "D7"], 0.05, 0.9, 0.25, "bell", 7, 0.9)
    out = zeros(len(rise) + secs(SR, 0.3)); mix_into(out, rise); mix_into(out, sh, secs(SR, 0.3))
    return reverb(out, SR, 0.78, 0.3, 0.22)

def sfx_reward_pop():
    """Upward 'bloop' plus a G6 bell: reads as gain, not deflate."""
    n = secs(SR, 0.25)
    s = sine_sweep(n, SR, 380, 1150, 0.4); e = env_exp(n, SR, 0.05, 0.001)
    out = [s[i] * e[i] for i in range(n)]
    mix_into(out, bell(secs(SR, 0.22), 1568.0, 0.18, 0.4), secs(SR, 0.025))
    return out

def sfx_star_pop():
    """Results star: soft pop + C6 bell with a G6 shimmer and sparkles. The game ladders its pitch (1, 1.26, 1.5)."""
    rng = random.Random(171); n = secs(SR, 0.75)
    out = zeros(n)
    L = secs(SR, 0.07)
    pop = sine_sweep(L, SR, 480, 1350, 0.5); ep = env_exp(L, SR, 0.022, 0.0008)
    mix_into(out, [pop[i] * ep[i] * 0.5 for i in range(L)])
    th = sine_sweep(L, SR, 330, 190, 0.5); et = env_exp(L, SR, 0.025, 0.0008)
    mix_into(out, [th[i] * et[i] * 0.35 for i in range(L)])
    mix_into(out, bell(secs(SR, 0.65), note_hz("C6"), 0.35, 0.8), secs(SR, 0.012))
    mix_into(out, bell(secs(SR, 0.5), note_hz("G6"), 0.25, 0.3), secs(SR, 0.04))
    mix_into(out, blips(n, SR, rng, 7, 4200, 7500, 0.018, 0.05, 0.32, 0.14))
    return reverb(out, SR, 0.72, 0.3, 0.18)

def sfx_coin():
    n = secs(SR, 0.22); out = zeros(n)
    for k, f in enumerate((1976.0, 2637.0)):
        b = bell(secs(SR, 0.2), f, 0.12, 0.8); mix_into(out, b, secs(SR, 0.035 * k))
    return out

def sfx_coin_shower():
    """CoinCount: six coin tings, accelerating, each at a slightly different pitch."""
    rng = random.Random(231); base = sfx_coin(); out = zeros(secs(SR, 0.85)); t = 0.0
    for k, gap in enumerate((0.11, 0.095, 0.08, 0.07, 0.06, 0.05)):
        c = resample(base, rng.choice((1.0, 1.06, 1.12, 1.19)))
        mix_into(out, c, secs(SR, t), 1.0 - 0.07 * k)
        t += gap
    return out

def sfx_tool_use():
    """Root catapult release: arm slams the stop bar, rope twang, frame rattle (the boulder's whoosh is LaunchHeavy)."""
    rng = random.Random(81); n = secs(SR, 0.55); out = zeros(n)
    Lc = secs(SR, 0.12)
    creak = formant_voice(Lc, 70, 95, ((500, 4, 1.0), (1100, 6, 0.4)), 84); ec = env_adsr(Lc, SR, 0.01, 0.03, 0.6, 0.05)
    mix_into(out, norm_to([creak[i] * ec[i] for i in range(Lc)], 0.3))
    tk = secs(SR, 0.05)
    mix_into(out, norm_to(wood_knock(rng, secs(SR, 0.4), 95, 1.0), 1.0), tk)
    mix_into(out, norm_to(wood_knock(rng, secs(SR, 0.2), 240, 1.3), 0.45), tk + secs(SR, 0.004))
    mix_into(out, norm_to(pluck(SR, 147.0, 0.45, 0.35, 1.0, 82), 0.35), tk + secs(SR, 0.006))
    mix_into(out, norm_to(crackle(rng, secs(SR, 0.3), 0.012, 2.0, 0.5), 0.3), tk + secs(SR, 0.03))
    return out

def sfx_heal():
    notes = arp(["C6", "G5", "E6", "C7"], 0.07, 0.8, 0.4, "bell", 9, 0.8)
    rng = random.Random(91); n = len(notes)
    air = biquad(noise(n, rng), SR, "bp", 5000, 2.0); e = env_adsr(n, SR, 0.2, 0.2, 0.4, 0.4)
    return reverb([notes[i] + air[i] * e[i] * 0.15 for i in range(n)], SR, 0.8, 0.25, 0.3)

def sfx_countdown():
    rng = random.Random(95); return wood_knock(rng, secs(SR, 0.14), 820, 0.6)

def sfx_timer_tick():
    n = secs(SR, 0.07); t = tone(n, SR, 3100, ((1, 1.0), (1.5, 0.3))); e = env_exp(n, SR, 0.008, 0.0003)
    return [t[i] * e[i] for i in range(n)]

def sfx_turn_start():
    out = arp(["G4", "C5"], 0.12, 0.6, 0.9, "marimba", 11, 0.6)
    hi = arp(["G5", "C6"], 0.12, 0.7, 0.3, "bell", 12, 0.6)
    mix_into(out, hi); return reverb(out, SR, 0.7, 0.3, 0.18)

def sfx_enemy_turn():
    out = arp(["C4", "G3"], 0.14, 0.7, 0.9, "marimba", 13, 0.6)
    return reverb(lowpass1(out, SR, 2200), SR, 0.72, 0.4, 0.2)

def sfx_turn_timeout():
    """Time's up: descending marimba G4 -> D4 with a soft low 'womp' (phone-audible 220 -> 120 Hz)."""
    out = arp(["G4", "D4"], 0.16, 0.5, 0.8, "marimba", 31, 0.25)
    out = lowpass1(out, SR, 1800)
    L = secs(SR, 0.32)
    sw = sine_sweep(L, SR, 220, 120, 0.6); sw2 = sine_sweep(L, SR, 440, 240, 0.6); e = env_exp(L, SR, 0.08, 0.004)
    mix_into(out, norm_to([(sw[i] + 0.3 * sw2[i]) * e[i] for i in range(L)], 0.5 * peak(out)), secs(SR, 0.16))
    return out

def sfx_wind_gust():
    rng = random.Random(101); n = secs(SR, 2.6)
    nz = pink(n, rng)
    out = zeros(n)
    bp = biquad(nz, SR, "bp", 500, 0.9, 1400, 1.0)
    e = env_adsr(n, SR, 0.9, 0.4, 0.6, 1.2)
    for i in range(n): out[i] = bp[i] * e[i] * (0.85 + 0.15 * math.sin(TAU * 3.1 * i / SR))
    return out

def sfx_thunder():
    rng = random.Random(111); n = secs(SR, 4.5)
    crack = biquad(noise(secs(SR, 0.4), rng), SR, "lp", 6000, 0.7, 800, 0.5)
    ec = env_exp(len(crack), SR, 0.08, 0.002)
    rum = biquad(brown(n, rng), SR, "lp", 220, 0.7)
    env = zeros(n); t = 0
    while t < n:
        L = secs(SR, rng.uniform(0.25, 0.8)); a = rng.uniform(0.4, 1.0) * (1 - t / n) ** 1.2
        for k in range(L):
            if t + k < n: env[t + k] = max(env[t + k], a * math.sin(math.pi * k / L))
        t += secs(SR, rng.uniform(0.12, 0.5))
    out = [rum[i] * env[i] * 2.2 for i in range(n)]
    mix_into(out, [crack[i] * ec[i] for i in range(len(crack))], 0, 0.8)
    return softclip(out, 1.4)

# ---- battle flow
def horn(f, dur, harmonics=6, tilt=1.0, scoop_cents=-40.0, scoop_t=0.06, lp0=500.0, lp1=2600.0, lp_t=0.15,
         adsr=(0.04, 0.15, 0.75, 0.45), vib=0.0, vib_hz=5.5):
    """Brassy additive tone: pitch scoop into the note, low-pass 'opening' sweep, optional delayed vibrato."""
    n = secs(SR, dur); out = zeros(n); ph = 0.0
    parts = [(k, 1.0 / (k ** tilt)) for k in range(1, harmonics + 1) if f * k < SR * 0.45]
    for i in range(n):
        t = i / SR
        cents = scoop_cents * max(0.0, 1.0 - t / scoop_t) if scoop_t > 0 else 0.0
        fi = f * 2 ** (cents / 1200.0)
        if vib > 0.0: fi *= 1.0 + vib * math.sin(TAU * vib_hz * t) * min(1.0, t / 0.25)
        ph += TAU * fi / SR
        v = 0.0
        for k, a in parts: v += a * math.sin(k * ph)
        out[i] = v
    out = biquad(out, SR, "lp", lp0, 0.9, lp1, 1.0, sweep_len=secs(SR, lp_t))
    env = env_adsr(n, SR, *adsr)
    return [out[i] * env[i] for i in range(n)]

def taiko(rng, dur=0.5, f0=85.0, f1=48.0, vel=1.0):
    """Big drum: sub body + phone-audible overtone + skin slap + beater click."""
    n = secs(SR, dur)
    body = sine_sweep(n, SR, f0, f1, 0.4); e = env_exp(n, SR, 0.14, 0.001)
    ov = sine_sweep(n, SR, f0 * 2.3, f1 * 2.3, 0.4); eo = env_exp(n, SR, 0.06, 0.001)
    out = [body[i] * e[i] + ov[i] * eo[i] * 0.4 for i in range(n)]
    Ls = secs(SR, 0.04)
    skin = lowpass1(noise(Ls, rng), SR, 600)
    mix_into(out, norm_to([v * (1 - i / Ls) for i, v in enumerate(skin)], 0.5))
    mix_into(out, norm_to(kick_click(SR, rng), 0.35))
    return [v * vel for v in tail_fade(out, SR, 0.02)]

def sfx_battle_start():
    """Battle horn: D4 power fifth with a taiko flam underneath."""
    rng = random.Random(131); n = secs(SR, 1.55); out = zeros(n)
    mix_into(out, norm_to(horn(note_hz("D4"), 1.3), 0.7), secs(SR, 0.02))
    mix_into(out, norm_to(horn(note_hz("A4"), 1.3, scoop_cents=-30.0), 0.35), secs(SR, 0.02))
    mix_into(out, norm_to(taiko(rng, 0.6), 0.55), secs(SR, 0.0))
    mix_into(out, norm_to(taiko(rng, 0.6, 95.0, 52.0), 0.4), secs(SR, 0.16))
    return reverb(out, SR, 0.8, 0.35, 0.25)

# ---- destruction
def sfx_wall_crack(seed):
    """A wall part cracks to the next damage stage: strained creak (stick-slip), sharp crack, splinters, knock."""
    rng = random.Random(seed); n = secs(SR, 0.62); out = zeros(n)
    Lc = secs(SR, 0.3)
    f0, f1 = (60.0, 85.0) if seed % 2 else (66.0, 94.0)
    cr = formant_voice(Lc, f0, f1, ((700, 4, 1.0), (1500, 6, 0.4)), seed)
    ec = env_adsr(Lc, SR, 0.03, 0.05, 0.6, 0.15)
    w = TAU * (32.0 if seed % 2 else 27.0) / SR
    creak = [cr[i] * ec[i] * (0.6 + 0.4 * (0.5 + 0.5 * math.tanh(4.0 * math.sin(w * i)))) for i in range(Lc)]
    mix_into(out, norm_to(creak, 0.5))
    tc = secs(SR, 0.26 + rng.uniform(-0.02, 0.02))
    mix_into(out, norm_to(crack_burst(rng, 6, 1500, 1.2), 1.0), tc)
    mix_into(out, norm_to(crackle(rng, secs(SR, 0.25), 0.02, 2.0, 0.6), 0.6), tc + secs(SR, 0.003))
    mix_into(out, norm_to(wood_knock(rng, secs(SR, 0.2), 260 * rng.uniform(0.92, 1.08), 1.3), 0.6), tc)
    return softclip(out, 1.4)

def sfx_castle_collapse():
    """Whole castle falls: layered wall breaks (later ones pitched down), rolling rumble, settle thud, falling debris."""
    rng = random.Random(141); n = secs(SR, 3.4)
    br = lowpass1(brown(n, rng), SR, 160)
    mid = biquad(noise(n, rng), SR, "bp", 600, 0.7)
    env = zeros(n)
    for i in range(n):
        t = i / SR
        sw = 0.5 + 0.45 * math.exp(-((t - 0.3) / 0.18) ** 2) + 0.4 * math.exp(-((t - 0.9) / 0.25) ** 2) + 0.35 * math.exp(-((t - 1.6) / 0.3) ** 2)
        env[i] = min(1.0, t / 0.08) * sw * math.exp(-max(0.0, t - 1.8) / 1.2)
    rumble = norm_to([br[i] * env[i] for i in range(n)], 1.0)
    crumble = norm_to([mid[i] * env[i] for i in range(n)], 0.3)
    out = [rumble[i] * 0.9 + crumble[i] for i in range(n)]
    for k, (t0, g) in enumerate(((0.0, 1.0), (0.22, 0.8), (0.55, 0.7), (0.95, 0.55), (1.5, 0.4))):
        wb = sfx_wall_break(21 + k)
        if k >= 2: wb = resample(wb, 0.85)
        mix_into(out, norm_to(wb, g), secs(SR, t0))
    L = secs(SR, 1.2)
    st = sine_sweep(L, SR, 70, 35, 0.5); es = env_exp(L, SR, 0.25, 0.002)
    mix_into(out, [st[i] * es[i] * 0.9 for i in range(L)], secs(SR, 1.9))
    mix_into(out, norm_to(wood_knock(rng, secs(SR, 0.45), 105, 0.8), 0.5), secs(SR, 1.9))
    for _ in range(25):
        t = rng.uniform(1.0, 2.8)
        mix_into(out, norm_to(wood_knock(rng, secs(SR, 0.08), rng.uniform(500, 1400), 1.4), rng.uniform(0.12, 0.35) * (1.0 - 0.5 * (t - 1.0) / 1.8)), secs(SR, t))
    out = normalize(reverb(out, SR, 0.84, 0.4, 0.28), 0.0)
    return softclip(out, 1.8)

# ---- impacts
def sfx_ground_thud(seed):
    """Missed shot hits the dirt: low thump (with a phone-audible upper thump), dirt burst, gravel."""
    rng = random.Random(seed); n = secs(SR, 0.42)
    sw = sine_sweep(n, SR, 95, 50, 0.5); e = env_exp(n, SR, 0.07, 0.001)
    up = sine_sweep(n, SR, 200 * rng.uniform(0.9, 1.1), 115, 0.5); eu = env_exp(n, SR, 0.035, 0.001)
    dirt = lowpass1(brown(n, rng), SR, 500); ed = env_adsr(n, SR, 0.003, 0.04, 0.3, 0.2)
    burst = biquad(noise(n, rng), SR, "bp", 420, 1.0, 180, 0.6); eb = env_exp(n, SR, 0.045, 0.001)
    out = [sw[i] * e[i] * 0.9 + up[i] * eu[i] * 0.4 for i in range(n)]
    mix_into(out, norm_to([dirt[i] * ed[i] for i in range(n)], 0.45))
    mix_into(out, norm_to([burst[i] * eb[i] for i in range(n)], 0.45))
    mix_into(out, norm_to(crackle(rng, n, 0.006, 2.0, 0.25), 0.22))
    return lowpass1(out, SR, 3000)

def sfx_shield_block():
    """A hit fully absorbed by a shield: glassy double bell ping, high glint, click, small body knock."""
    rng = random.Random(151); n = secs(SR, 0.5); out = zeros(n)
    mix_into(out, bell(n, 1760.0, 0.25), 0, 0.7)
    mix_into(out, bell(n, 2637.0, 0.18), 0, 0.35)
    t = tone(n, SR, 5274.0); e = env_exp(n, SR, 0.03, 0.0005)
    mix_into(out, [t[i] * e[i] * 0.2 for i in range(n)])
    mix_into(out, norm_to(crack_burst(rng, 2, 3000, 0.5), 0.5))
    mix_into(out, norm_to(wood_knock(rng, secs(SR, 0.12), 420, 0.8), 0.3))
    return reverb(out, SR, 0.75, 0.3, 0.18)

def sfx_magic_impact():
    rng = random.Random(161); n = secs(SR, 0.42); out = zeros(n)
    s = sine_sweep(n, SR, 1400, 500, 0.4); e = env_exp(n, SR, 0.05, 0.001)
    mix_into(out, [s[i] * e[i] * 0.6 for i in range(n)])
    mix_into(out, blips(n, SR, rng, 6, 2500, 5000, 0.02, 0.0, 0.12, 0.2))
    nz = biquad(noise(n, rng), SR, "bp", 2500, 1.5); en = env_exp(n, SR, 0.04, 0.001)
    mix_into(out, norm_to([nz[i] * en[i] for i in range(n)], 0.35))
    return reverb(out, SR, 0.74, 0.3, 0.15)

def sfx_poison_hiss():
    """Spore splat, gas hiss and popping bubbles."""
    rng = random.Random(171); n = secs(SR, 0.95); out = zeros(n)
    Ls = secs(SR, 0.05)
    splat = lowpass1(decay_burst(noise(Ls, rng), SR, 0.012), SR, 1200)
    mix_into(out, norm_to(splat, 0.8))
    Lw = secs(SR, 0.3)
    sw = sine_sweep(Lw, SR, 300, 110, 0.6); ew = env_exp(Lw, SR, 0.05, 0.001)
    mix_into(out, [sw[i] * ew[i] * 0.6 for i in range(Lw)])
    hs = biquad(noise(n, rng), SR, "bp", 4500, 0.8); eh = env_adsr(n, SR, 0.05, 0.1, 0.5, 0.6)
    mix_into(out, norm_to([hs[i] * eh[i] for i in range(n)], 0.45))
    for _ in range(12):
        L = secs(SR, rng.uniform(0.02, 0.035))
        b = sine_sweep(L, SR, 250, 900, 0.8); eb = env_exp(L, SR, 0.008, 0.0005)
        mix_into(out, [b[i] * eb[i] for i in range(L)], secs(SR, rng.uniform(0.05, 0.8)), rng.uniform(0.15, 0.35))
    return out

# ---- launches
def sfx_launch_light(seed, f0, f1, twang):
    """Light shot (thorn, feather, quill, chip): rising air whoosh and a short bow twang, no thud."""
    rng = random.Random(seed); n = secs(SR, 0.22)
    sw = biquad(noise(n, rng), SR, "bp", f0, 2.2, f1, 0.7); e = env_adsr(n, SR, 0.012, 0.03, 0.4, 0.12)
    out = norm_to([sw[i] * e[i] for i in range(n)], 1.0)
    mix_into(out, norm_to(pluck(SR, twang, 0.12, 0.8, 1.0, seed), 0.4))
    return out

def sfx_launch_heavy(seed, f0, f1, knock_f):
    """Heavy lob (acorn, cones, boulder, spore bombs): throwing arm creak + wooden knock + slow air push."""
    rng = random.Random(seed); n = secs(SR, 0.42)
    sw = biquad(pink(n, rng), SR, "bp", f0, 1.2, f1, 0.8); e = env_adsr(n, SR, 0.03, 0.08, 0.5, 0.25)
    out = norm_to([sw[i] * e[i] for i in range(n)], 1.0)
    mix_into(out, norm_to(wood_knock(rng, secs(SR, 0.25), knock_f, 1.0), 0.7))
    Lc = secs(SR, 0.12)
    cr = formant_voice(Lc, 70, 95, ((500, 4, 1.0), (1100, 6, 0.4)), seed); ec = env_adsr(Lc, SR, 0.01, 0.02, 0.7, 0.04)
    mix_into(out, norm_to([cr[i] * ec[i] for i in range(Lc)], 0.25))
    return out

def sfx_launch_magic(seed, notes):
    """Homing orb (dew drop, light seed, sprout orb): airy shimmer whoosh, bell sparkle run, rising tone."""
    rng = random.Random(seed); n = secs(SR, 0.5); out = zeros(n)
    sw = biquad(noise(n, rng), SR, "bp", 2000, 3.0, 5500, 0.8); e = env_adsr(n, SR, 0.02, 0.08, 0.4, 0.25)
    mix_into(out, norm_to([sw[i] * e[i] for i in range(n)], 0.6))
    mix_into(out, norm_to(arp(notes, 0.04, 0.3, 0.25, "bell", seed, 0.1), 0.5))
    r = sine_sweep(n, SR, 700, 1400, 0.6); er = env_exp(n, SR, 0.1, 0.005)
    mix_into(out, norm_to([r[i] * er[i] for i in range(n)], 0.25))
    return reverb(out, SR, 0.7, 0.3, 0.12)

def sfx_bee_buzz():
    """Angry bee buzz: formant-filtered saw with 28 Hz wing vibrato and 45 Hz amplitude flutter."""
    rng = random.Random(181); n = secs(SR, 0.7)
    saw = zeros(n); ph = 0.0
    for i in range(n):
        t = i / SR
        f = (190.0 + 20.0 * i / n) * (1.0 + 0.08 * math.sin(TAU * 28.0 * t)) + rng.uniform(-3, 3)
        ph = (ph + f / SR) % 1.0
        saw[i] = 2 * ph - 1
    out = zeros(n)
    for fc, q, g in ((600, 4, 1.0), (1400, 6, 0.5), (2800, 8, 0.2)): mix_into(out, biquad(saw, SR, "bp", fc, q), 0, g)
    env = env_adsr(n, SR, 0.04, 0.05, 0.8, 0.2)
    return [out[i] * (0.75 + 0.25 * math.sin(TAU * 45.0 * i / SR)) * env[i] for i in range(n)]

def sfx_vine_whip():
    """Vine spear / vine net: whip swish rising into a sharp crack, leafy crackle tail."""
    rng = random.Random(191); n = secs(SR, 0.34); out = zeros(n)
    L = secs(SR, 0.12)
    sw = biquad(noise(L, rng), SR, "bp", 800, 2.0, 5000, 1.5); e = env_adsr(L, SR, 0.08, 0.02, 0.6, 0.02)
    mix_into(out, norm_to([sw[i] * e[i] for i in range(L)], 0.8))
    mix_into(out, norm_to(crack_burst(rng, 4, 2000, 1.1), 1.0), L)
    mix_into(out, norm_to(crackle(rng, secs(SR, 0.18), 0.03, 2.0, 0.35), 0.45), L + secs(SR, 0.004))
    return softclip(normalize(out, 0.0), 2.0)

# ---- specials and tools
def sfx_special_ready():
    rng = random.Random(201)
    out = arp(["E6", "G6", "C7"], 0.06, 0.6, 0.4, "bell", 19, 0.4)
    n = len(out)
    mix_into(out, norm_to(blips(n, SR, rng, 10, 3000, 6000, 0.025, 0.0, 0.4), 0.12 * peak(out) / 0.4))
    L = secs(SR, 0.5)
    s = sine_sweep(L, SR, 1200, 2400, 0.6); e = env_adsr(L, SR, 0.02, 0.1, 0.3, 0.3)
    mix_into(out, norm_to([s[i] * e[i] for i in range(L)], 0.1 * peak(out) / 0.4))
    return reverb(out, SR, 0.78, 0.3, 0.25)

def sfx_special_buff():
    n = secs(SR, 0.9); rise = zeros(n)
    for k, a in ((1, 1.0), (2, 0.5), (3, 0.33), (4, 0.25)):
        mix_into(rise, sine_sweep(n, SR, 300.0 * k, 900.0 * k, 0.6), 0, a)
    env = env_adsr(n, SR, 0.05, 0.2, 0.6, 0.4)
    rise = norm_to(lowpass1([rise[i] * env[i] for i in range(n)], SR, 2500), 0.4)
    bells = norm_to(arp(["G5", "D6", "G6"], 0.08, 0.7, 0.35, "bell", 43, 0.4), 0.35)
    out = zeros(max(n, secs(SR, 0.35) + len(bells)))
    mix_into(out, rise); mix_into(out, bells, secs(SR, 0.35))
    return reverb(out, SR, 0.78, 0.3, 0.22)

def sfx_shield_up():
    n = secs(SR, 0.85); out = zeros(n)
    for det in (-0.006, 0.0, 0.006):
        mix_into(out, sine_sweep(n, SR, 380.0 * (1 + det), 760.0 * (1 + det), 0.7))
        mix_into(out, sine_sweep(n, SR, 760.0 * (1 + det), 1520.0 * (1 + det), 0.7), 0, 0.25)
    env = env_adsr(n, SR, 0.05, 0.1, 0.6, 0.4)
    w = TAU * 9.0 / SR
    out = norm_to([out[i] * env[i] * (1.0 - 0.25 * (0.5 + 0.5 * math.sin(w * i))) for i in range(n)], 0.4)
    block = norm_to(sfx_shield_block(), 0.6)
    res = zeros(max(n, secs(SR, 0.35) + len(block)))
    mix_into(res, out); mix_into(res, block, secs(SR, 0.35))
    return res

def sfx_wind_chime():
    rng = random.Random(211); n = secs(SR, 1.8); out = zeros(n)
    names = ["D6", "E6", "G6", "A6", "B6", "D7"]; rng.shuffle(names)
    times = sorted(rng.uniform(0.0, 0.7) for _ in names)
    for name, t in zip(names, times):
        mix_into(out, bell(secs(SR, 1.1), note_hz(name), 0.8, 0.4 * rng.uniform(0.7, 1.0)), secs(SR, t))
    air = biquad(pink(n, rng), SR, "bp", 800, 0.7); e = env_adsr(n, SR, 0.3, 0.2, 0.5, 0.8)
    mix_into(out, norm_to([air[i] * e[i] for i in range(n)], 0.15 * peak(out)))
    return reverb(out, SR, 0.82, 0.3, 0.3)

# ---- menu flow
def sfx_equip():
    rng = random.Random(221); out = zeros(secs(SR, 0.2))
    mix_into(out, wood_knock(rng, secs(SR, 0.12), 520, 1.2))
    mix_into(out, wood_knock(rng, secs(SR, 0.12), 660, 1.2), secs(SR, 0.045), 0.7)
    L = secs(SR, 0.03); t = tone(L, SR, 3300); e = env_exp(L, SR, 0.005, 0.0003)
    mix_into(out, norm_to([t[i] * e[i] for i in range(L)], 0.12 * peak(out)), secs(SR, 0.045))
    return out

def sfx_chest_shake():
    rng = random.Random(231); out = zeros(secs(SR, 0.5))
    for k, t in enumerate((0.0, 0.07, 0.15, 0.22, 0.30)):
        mix_into(out, norm_to(wood_knock(rng, secs(SR, 0.12), rng.uniform(240, 320), 1.1), 0.8 - 0.075 * k), secs(SR, t))
    mix_into(out, norm_to(crackle(rng, secs(SR, 0.35), 0.01, 2.0, 0.5), 0.3))
    for t in (0.1, 0.24):
        L = secs(SR, 0.2); c = tone(L, SR, 2637.0); e = env_exp(L, SR, 0.04, 0.0005)
        mix_into(out, [c[i] * e[i] * 0.15 for i in range(L)], secs(SR, t))
    return out

def sfx_transition():
    """Scene/panel transition: filtered air swell up then down."""
    rng = random.Random(241); n = secs(SR, 0.45)
    def path(u): return 300.0 * 6.0 ** (u / 0.5) if u < 0.5 else 1800.0 * (500.0 / 1800.0) ** ((u - 0.5) / 0.5)
    sw = biquad(pink(n, rng), SR, "bp", 300, 1.1, path=path)
    return [sw[i] * math.sin(math.pi * i / n) ** 2 * 0.9 for i in range(n)]

def sfx_unlock():
    """Unlock fanfare: brass G4-C5-E5-G5 (last held with vibrato), timpani roll, bell sparkle."""
    rng = random.Random(251); n = secs(SR, 2.3); out = zeros(n)
    for name, t, d in (("G4", 0.0, 0.2), ("C5", 0.14, 0.2), ("E5", 0.28, 0.2), ("G5", 0.42, 1.2)):
        h = horn(note_hz(name), d, harmonics=8, tilt=1.2, scoop_cents=-25.0, scoop_t=0.04, lp0=800.0, lp1=3200.0, lp_t=0.06,
                 adsr=(0.015, 0.06, 0.8, min(0.3, d * 0.45)), vib=0.004 if d > 1.0 else 0.0)
        mix_into(out, norm_to(h, 0.6), secs(SR, t))
    for k in range(10):
        vel = 0.2 + 0.4 * k / 9.0
        L = secs(SR, 0.5)
        b = sine_sweep(L, SR, 110, 90, 0.5); o = sine_sweep(L, SR, 165, 135, 0.5); o2 = sine_sweep(L, SR, 218, 178, 0.5)
        e = env_exp(L, SR, 0.12, 0.001); e2 = env_exp(L, SR, 0.06, 0.001)
        hit = [(b[i] * e[i] + (0.5 * o[i] + 0.35 * o2[i]) * e2[i]) for i in range(L)]
        mix_into(out, norm_to(hit, 0.45 * vel), secs(SR, 0.3 + 0.04 * k))
    mix_into(out, norm_to(arp(["C6", "E6", "G6", "C7"], 0.06, 0.8, 0.3, "bell", 252, 0.3), 0.3), secs(SR, 0.45))
    return reverb(out, SR, 0.8, 0.3, 0.3)

# ----------------------------------------------------------------------------- loops (44.1 kHz)
LSR = 44100

def seamless(x, sr, xf=1.0):
    F = secs(sr, xf); n = len(x) - F
    out = x[:n]
    for i in range(F):
        t = i / F
        out[i] = x[n + i] * math.cos(t * math.pi / 2) + out[i] * math.sin(t * math.pi / 2)
    return out

def amb_rain():
    """32 s rain loop: filtered bed, 6 kHz sizzle, low body, 60 drops/s, leaf taps, integer-cycle swell."""
    rng = random.Random(201); T = 32.0; xf = 2.0; n = secs(LSR, T + xf)
    bed = biquad(pink(n, rng), LSR, "bp", 2400, 0.55)
    siz = highpass1(highpass1(noise(n, rng), LSR, 6000), LSR, 6000)
    body = lowpass1(pink(n, rng), LSR, 450)
    out = [bed[i] * 1.0 + siz[i] * 0.12 + body[i] * 0.5 for i in range(n)]
    for _ in range(int(60 * (T + xf))):
        at = rng.randrange(n); L = secs(LSR, rng.uniform(0.003, 0.010)); f = rng.uniform(2000, 6000); amp = rng.uniform(0.02, 0.12)
        w = TAU * f / LSR; tau = L * 0.35
        for k in range(L):
            if at + k >= n: break
            out[at + k] += math.sin(w * k) * amp * math.exp(-k / tau)
    for _ in range(int(4 * (T + xf))):
        at = rng.randrange(n); L = secs(LSR, 0.09)
        exc = decay_burst(noise(secs(LSR, 0.002), rng), LSR, 0.0005)
        mix_into(out, norm_to(resonator(L, LSR, rng.uniform(700, 1200), 0.015, 1.0, exc), 0.05 * rng.uniform(0.5, 1.0)), at)
    swell = [0.85 + 0.1 * math.sin(TAU * 1.0 * (i / LSR) / T) + 0.05 * math.sin(TAU * 3.0 * (i / LSR) / T) for i in range(n)]
    return seamless([out[i] * swell[i] for i in range(n)], LSR, xf)

def rustle(rng, dur, sr=LSR):
    """Leaf rustle: band-passed noise through random smooth grains under a hann envelope."""
    L = secs(sr, dur)
    nz = biquad(noise(L, rng), sr, "bp", 3500, 0.9)
    gate = zeros(L); i = 0
    while i < L:
        g = secs(sr, rng.uniform(0.004, 0.018)); a = rng.uniform(0.2, 1.0) if rng.random() < 0.7 else 0.05
        for k in range(g):
            if i + k < L: gate[i + k] = a * math.sin(math.pi * k / g)
        i += g
    return [nz[i] * gate[i] * math.sin(math.pi * i / L) ** 2 for i in range(L)]

def bird_call(rng, falling=False, sr=LSR):
    syll = rng.randint(2, 5); f0 = rng.uniform(2600, 3800); out = zeros(secs(sr, 1.2)); pos = 0
    for _ in range(syll):
        L = secs(sr, rng.uniform(0.045, 0.1)); r = 0.75 if falling else rng.uniform(1.15, 1.45)
        s = sine_sweep(L, sr, f0, f0 * r, 0.6); h = sine_sweep(L, sr, 2 * f0, 2 * f0 * r, 0.6)
        e = env_adsr(L, sr, 0.008, 0.015, 0.6, 0.03)
        mix_into(out, [(s[i] + 0.15 * h[i]) * e[i] for i in range(L)], pos)
        pos += L + secs(sr, rng.uniform(0.05, 0.11))
        f0 *= rng.uniform(0.94, 1.06)
    return lowpass1(out[:pos + secs(sr, 0.05)], sr, 7000)

def amb_forest():
    """48 s forest loop: wind bed with integer-cycle swells, leaf rustles, a few distant varied birds, far creaks."""
    rng = random.Random(211); T = 48.0; xf = 2.0; n = secs(LSR, T + xf)
    wind = biquad(pink(n, rng), LSR, "bp", 700, 0.6)
    air = biquad(pink(n, rng), LSR, "bp", 2200, 0.8)
    low = lowpass1(pink(n, rng), LSR, 250)
    out = zeros(n)
    for i in range(n):
        t = i / LSR
        sw = 0.55 + 0.25 * math.sin(TAU * 2 * t / T + 1.3) + 0.12 * math.sin(TAU * 3 * t / T + 0.4) + 0.08 * math.sin(TAU * 5 * t / T + 2.1)
        sa = 0.3 + 0.2 * math.sin(TAU * 4 * t / T + 0.7)
        out[i] = wind[i] * sw * 0.6 + air[i] * sa * 0.25 + low[i] * 0.3
    bed = math.sqrt(sum(v * v for v in out) / n)  # bed RMS: one-shot layers are set relative to it
    t = rng.uniform(1.0, 4.0)
    while t < T + xf - 1.0:
        mix_into(out, norm_to(rustle(rng, rng.uniform(0.3, 0.6)), 1.6 * bed * rng.uniform(0.7, 1.0)), secs(LSR, t))
        t += rng.uniform(4.5, 7.5)
    for k in range(6):
        at = secs(LSR, 3.0 + k * 7.6 + rng.uniform(-1.5, 1.5))
        mix_into(out, norm_to(bird_call(rng, falling=(k == 3)), 1.1 * bed * rng.uniform(0.6, 1.0)), at)
    for k in range(2):
        Lc = secs(LSR, 0.9)
        cr = formant_voice(Lc, 48, 62, ((420, 4, 1.0), (900, 5, 0.3)), 900 + k)
        ec = env_adsr(Lc, LSR, 0.25, 0.1, 0.6, 0.45)
        mix_into(out, norm_to(lowpass1([cr[i] * ec[i] for i in range(Lc)], LSR, 1200), 0.7 * bed), secs(LSR, 12.0 + 24.0 * k))
    return seamless(out, LSR, xf)

# ---- music
class Song:
    """Beat-addressed loop buffer. Every event wraps around the loop end, so tails continue at the start."""
    def __init__(self, bpm, bars, seed=0, sr=LSR):
        self.sr = sr; self.beat = 60.0 / bpm; self.bars = bars
        self.n = int(round(sr * self.beat * 4 * bars))
        self.out = zeros(self.n); self.rng = random.Random(seed)

    def at(self, beats): return int(round(beats * self.beat * self.sr))

    def add(self, buf, beats, gain=1.0): mix_wrap(self.out, buf, self.at(beats), gain)

    def drums(self, bar_from, bar_to, pattern, maker, vel, swing=0.0, skip=None):
        """16-step pattern per bar on bars [bar_from, bar_to); maker(rng) returns a unit-velocity hit."""
        for b in range(bar_from, bar_to):
            for s16 in range(16):
                if pattern[s16 % len(pattern)] != "x": continue
                if skip and (b, s16) in skip: continue
                t = b * 4 + s16 / 4.0 + (swing / 4.0 if s16 % 2 == 1 else 0.0)
                self.add(maker(self.rng), t, vel * self.rng.uniform(0.85, 1.0))

    def finish(self, wet=0.22):
        return reverb(self.out, self.sr, 0.8, 0.35, wet, loop=True)

def bass_note(sr, name, beats_len, beat):
    """Bass one octave up (130-220 Hz) with strong harmonics and gentle saturation: survives phone speakers."""
    key = ("bass", sr, name, round(beats_len * beat, 4))
    if key not in _CACHE:
        L = secs(sr, beats_len * beat + 0.12)
        t = tone(L, sr, note_hz(name), ((1, 1.0), (2, 0.6), (3, 0.4), (4, 0.25), (5, 0.12)))
        e = env_adsr(L, sr, 0.008, 0.08, 0.7, 0.1)
        _CACHE[key] = softclip([t[i] * e[i] / 2.37 for i in range(L)], 1.3)
    return _CACHE[key]

def hit_kick(r): return kick(LSR)
def hit_snare(r): return r.choice(variants("snare", 4, lambda q: snare(LSR, q)))
def hit_hat(r): return r.choice(variants("hat", 4, lambda q: hat(LSR, q)))
def hit_open_hat(r): return r.choice(variants("ohat", 2, lambda q: hat(LSR, q, 1.0, True)))
def hit_shaker(r): return r.choice(variants("shaker", 4, lambda q: shaker(LSR, q)))

def music_menu():
    """'Grove' - 92 BPM, 32 bars (~83.5 s), C major pentatonic. Form A A' B A''."""
    s = Song(92, 32, 301)
    A = ["C", "Am", "F", "G"] * 2; B = ["Am", "Em", "F", "G"] * 2
    prog = A + A + B + A
    PAD = {"C": ("C4", "E4", "G4"), "Am": ("A3", "C4", "E4"), "F": ("F3", "A3", "C4"), "G": ("G3", "B3", "D4"), "Em": ("G3", "B3", "E4")}
    ROOT = {"C": "C3", "Am": "A3", "F": "F3", "G": "G3", "Em": "E3"}
    COUNTER = {"C": ("C4", "G4", "C5", "G4"), "Am": ("A3", "E4", "A4", "E4"), "F": ("F3", "C4", "F4", "C4"), "G": ("G3", "D4", "G4", "D4")}
    bar_s = s.beat * 4
    for b, ch in enumerate(prog):
        in_b = 16 <= b < 24
        s.add(pad(LSR, [note_hz(x) for x in PAD[ch]], bar_s + 0.6, 0.45, 0.8 if in_b else 0.35, 0.7), b * 4)
        if in_b: s.add(bass_note(LSR, ROOT[ch], 3.75, s.beat), b * 4, 0.3)
        else:
            s.add(bass_note(LSR, ROOT[ch], 2.5, s.beat), b * 4, 0.36)
            s.add(bass_note(LSR, ROOT[ch], 0.75, s.beat), b * 4 + 3, 0.3)
        if 8 <= b < 16 or b >= 24:
            for k in range(8):
                s.add(pluck_note(LSR, note_hz(COUNTER[ch][k % 4]), 0.5, 0.5), b * 4 + k * 0.5, 0.22 if b < 16 else 0.18)
    phrase_a = [
        (0, "E5", 1.5), (1.5, "D5", 0.5), (2, "C5", 1), (3, "D5", 0.5), (3.5, "E5", 0.5),
        (4, "E5", 1), (5, "G5", 0.5), (5.5, "A5", 0.5), (6, "A5", 2),
        (8, "A5", 1), (9, "G5", 0.5), (9.5, "E5", 0.5), (10, "C5", 1.5), (11.5, "D5", 0.5),
        (12, "D5", 1), (13, "E5", 0.5), (13.5, "D5", 0.5), (14, "G5", 2),
        (16, "E5", 1), (17, "G5", 0.5), (17.5, "A5", 0.5), (18, "G5", 1), (19, "E5", 1),
        (20, "A5", 1.5), (21.5, "G5", 0.5), (22, "E5", 1), (23, "D5", 1),
        (24, "C5", 1), (25, "D5", 0.5), (25.5, "E5", 0.5), (26, "A5", 1), (27, "G5", 1),
    ]
    end_a = [(28, "D5", 1.5), (29.5, "E5", 0.5), (30, "G5", 2)]
    end_a2 = [(28, "D5", 1), (29, "E5", 1), (30, "G5", 1), (31, "A5", 1)]
    phrase_b = [
        (0, "E6", 1.5), (1.5, "D6", 0.5), (2, "C6", 1), (3, "A5", 1),
        (4, "G5", 3), (7, "A5", 1),
        (8, "C6", 1.5), (9.5, "D6", 0.5), (10, "A5", 1), (11, "C6", 1),
        (12, "D6", 3), (15, "E6", 1),
        (16, "E6", 1), (17, "D6", 0.5), (17.5, "C6", 0.5), (18, "A5", 2),
        (20, "G5", 1), (21, "A5", 1), (22, "G5", 1.5), (23.5, "A5", 0.5),
        (24, "A5", 1), (25, "C6", 1), (26, "A5", 1), (27, "G5", 1),
        (28, "G5", 2), (30, "D6", 2),
    ]
    def lead(events, offset, vel):
        for st, name, ln in events:
            s.add(marimba_note(LSR, note_hz(name), max(0.35, ln * s.beat + 0.4)), offset + st, vel)
    lead(phrase_a + end_a, 0, 0.55)
    lead(phrase_a + end_a2, 32, 0.55)
    lead(phrase_b, 64, 0.45)
    lead(phrase_a + end_a, 96, 0.55)
    s.drums(0, 16, "x.......x.......", hit_kick, 0.35)
    s.drums(24, 32, "x.......x.......", hit_kick, 0.35)
    s.drums(0, 16, "..x...x...x...x.", hit_shaker, 0.18, 0.12)
    s.drums(16, 24, "..x...x...x...x.", hit_shaker, 0.12, 0.12)
    s.drums(24, 32, "..x...x...x...x.", hit_shaker, 0.18, 0.12)
    s.drums(24, 32, "xxxxxxxxxxxxxxxx", hit_shaker, 0.1, 0.12)
    return s.finish(0.22)

def bell_note(sr, f, dur):
    key = ("bell", sr, round(f, 3), round(dur, 3))
    if key not in _CACHE: _CACHE[key] = bell(secs(sr, dur), f, dur * 0.6, 1.0)
    return _CACHE[key]

BATTLE_DRUMS = {
    # 16-step bar patterns for the A/B/D sections; section C (bars 17-24) is always half-time.
    "drive":   {"kick": "x.....x...x.....", "snare": "....x.......x...", "hat": "x.x.x.x.x.x.x.x.", "extra": None},
    "march":   {"kick": "x...x...x...x...", "snare": "....x.......x...", "hat": "x.x.x.x.x.x.x.x.", "extra": ("..............x.", hit_snare, 0.12)},
    "shuffle": {"kick": "x..x......x.....", "snare": "....x.......x...", "hat": "x.x.x.x.x.x.x.x.", "extra": ("..x...x...x...x.", hit_shaker, 0.12)},
    "sparse":  {"kick": "x......x..x.....", "snare": "....x.......x...", "hat": "..x...x...x...x.", "extra": ("xxxxxxxxxxxxxxxx", hit_shaker, 0.05)},
}

def battle_song(bpm, seed, prog, pads, roots, arps, motif, motif2, drums="drive", lead="marimba", lead_vel=0.5,
                arp_bright=0.6, arp_gain=1.0, pad_vel=0.42, kick_vel=0.55, snare_vel=0.32, hat_vel=0.08, swing=0.0, wet=0.2):
    """32-bar battle loop. A (1-8): arps + drums. B (9-16): lead motif. C (17-24): half-time, softer arps, swelling pads.
    D (25-32): everything, with a 4-hit snare fill in bar 32 that wraps into bar 1. Arps are spelled per chord (no clashes)."""
    s = Song(bpm, 32, seed)
    bar_s = s.beat * 4
    for b in range(32):
        ch = prog[b % len(prog)]
        sec = b // 8  # 0=A 1=B 2=C 3=D
        s.add(pad(LSR, [note_hz(x) for x in pads[ch]], bar_s + 0.5, pad_vel * (1.2 if sec == 2 else 1.0), 0.6 if sec == 2 else 0.3, 0.6), b * 4)
        av = {0: 0.32, 1: 0.27, 2: 0.2, 3: 0.3}[sec] * arp_gain
        for k, name in enumerate(arps[ch]):
            s.add(pluck_note(LSR, note_hz(name), 0.5, arp_bright), b * 4 + k * 0.5, av)
        r = roots[ch]
        if sec == 2:
            s.add(bass_note(LSR, r, 1.5, s.beat), b * 4, 0.4); s.add(bass_note(LSR, r, 1.5, s.beat), b * 4 + 2.5, 0.34)
        else:
            for st, ln, g in ((0, 0.75, 0.42), (1.5, 0.5, 0.34), (2, 0.75, 0.4), (3.5, 0.5, 0.34)):
                s.add(bass_note(LSR, r, ln, s.beat), b * 4 + st, g)
    def play(events, offset, vel):
        for st, name, ln in events:
            dur = max(0.3, ln * s.beat + 0.35)
            buf = bell_note(LSR, note_hz(name), dur + 0.4) if lead == "bell" else marimba_note(LSR, note_hz(name), dur)
            s.add(buf, offset + st, vel)
    play(motif, 32, lead_vel); play(motif2, 48, lead_vel)
    play(motif, 96, lead_vel * 0.92); play(motif2, 112, lead_vel * 0.92)
    d = BATTLE_DRUMS[drums]
    for a, b in ((0, 16), (24, 32)):
        s.drums(a, b, d["kick"], hit_kick, kick_vel, swing)
        s.drums(a, b, d["snare"], hit_snare, snare_vel, swing, skip={(31, 12)} if a == 24 else None)
        s.drums(a, b, d["hat"], hit_hat, hat_vel, swing)
        if d["extra"]: s.drums(a, b, d["extra"][0], d["extra"][1], d["extra"][2], swing)
    s.drums(16, 24, "x.........x.....", hit_kick, kick_vel * 0.9, swing)
    s.drums(16, 24, "........x.......", hit_snare, snare_vel * 0.95, swing)
    s.drums(16, 24, "x...x...x...x...", hit_hat, hat_vel, swing)
    for k, v in enumerate((0.15, 0.22, 0.28, 0.35)):
        s.add(hit_snare(s.rng), 31 * 4 + 3 + k * 0.25, v * snare_vel / 0.32)
    for b in (8, 16, 24):
        s.add(hit_open_hat(s.rng), b * 4, 0.2)
    return s.finish(wet)

def music_battle():
    """Sunny Grove - 'Castle Clash': 112 BPM, ~68.6 s, A minor (Am F C G)."""
    return battle_song(
        112, 311, ["Am", "F", "C", "G"],
        {"Am": ("A3", "C4", "E4"), "F": ("F3", "A3", "C4"), "C": ("C4", "E4", "G4"), "G": ("G3", "B3", "D4")},
        {"Am": "A3", "F": "F3", "C": "C3", "G": "G3"},
        {"Am": ["A4", "C5", "E5", "A5", "E5", "C5", "D5", "E5"],
         "F": ["F4", "A4", "C5", "F5", "C5", "A4", "G4", "A4"],
         "C": ["E4", "G4", "C5", "E5", "C5", "G4", "A4", "G4"],
         "G": ["D4", "G4", "B4", "D5", "B4", "G4", "A4", "B4"]},
        [(0, "E5", 0.5), (0.5, "G5", 0.5), (1, "A5", 1), (2, "G5", 1),
         (4, "E5", 0.5), (4.5, "D5", 0.5), (5, "C5", 1), (6, "D5", 1),
         (8, "E5", 4),
         (12, "D5", 1), (13, "E5", 1), (14, "G5", 2)],
        [(0, "E5", 0.5), (0.5, "G5", 0.5), (1, "A5", 1), (2, "G5", 1),
         (4, "E5", 0.5), (4.5, "D5", 0.5), (5, "C5", 1), (6, "D5", 1),
         (8, "C5", 1), (9, "D5", 1), (10, "E5", 1), (11, "G5", 1),
         (12, "G5", 1.5), (13.5, "A5", 0.5), (14, "G5", 1), (15, "E5", 1)],
        drums="drive")

def music_battle_swamp():
    """Misty Swamp - 'Bog Hollow': 100 BPM, ~76.8 s, D minor (Dm Bb F C), darker plucks, lazy shuffle, wetter room."""
    return battle_song(
        100, 331, ["Dm", "Bb", "F", "C"],
        {"Dm": ("A3", "D4", "F4"), "Bb": ("Bb3", "D4", "F4"), "F": ("A3", "C4", "F4"), "C": ("G3", "C4", "E4")},
        {"Dm": "D3", "Bb": "Bb2", "F": "F3", "C": "C3"},
        {"Dm": ["D4", "F4", "A4", "D5", "A4", "F4", "E4", "F4"],
         "Bb": ["Bb3", "D4", "F4", "Bb4", "F4", "D4", "C4", "D4"],
         "F": ["C4", "F4", "A4", "C5", "A4", "F4", "G4", "F4"],
         "C": ["C4", "E4", "G4", "C5", "G4", "E4", "D4", "E4"]},
        [(0, "D5", 1), (1, "F5", 0.5), (1.5, "E5", 0.5), (2, "D5", 1.5), (3.5, "C5", 0.5),
         (4, "D5", 1), (5, "C5", 0.5), (5.5, "D5", 0.5), (6, "F5", 2),
         (8, "A4", 1), (9, "C5", 1), (10, "F5", 1.5), (11.5, "E5", 0.5),
         (12, "E5", 1), (13, "D5", 0.5), (13.5, "C5", 0.5), (14, "C5", 2)],
        [(0, "A5", 1), (1, "G5", 0.5), (1.5, "F5", 0.5), (2, "F5", 1), (3, "E5", 1),
         (4, "D5", 1.5), (5.5, "F5", 0.5), (6, "D5", 2),
         (8, "C5", 1), (9, "A4", 1), (10, "C5", 1), (11, "D5", 1),
         (12, "E5", 2), (14, "G5", 1), (15, "A5", 1)],
        drums="shuffle", lead_vel=0.5, arp_bright=0.35, pad_vel=0.45, kick_vel=0.5, snare_vel=0.28, swing=0.2, wet=0.26)

def music_battle_autumn():
    """Crimson Autumn - 'Ember March': 120 BPM, ~64 s, E minor (Em C G D), four-on-the-floor march."""
    return battle_song(
        120, 341, ["Em", "C", "G", "D"],
        {"Em": ("G3", "B3", "E4"), "C": ("G3", "C4", "E4"), "G": ("G3", "B3", "D4"), "D": ("F#3", "A3", "D4")},
        {"Em": "E3", "C": "C3", "G": "G3", "D": "D3"},
        {"Em": ["E4", "G4", "B4", "E5", "B4", "G4", "A4", "B4"],
         "C": ["E4", "G4", "C5", "E5", "C5", "G4", "A4", "G4"],
         "G": ["D4", "G4", "B4", "D5", "B4", "G4", "A4", "B4"],
         "D": ["D4", "F#4", "A4", "D5", "A4", "F#4", "E4", "F#4"]},
        [(0, "B4", 0.5), (0.5, "E5", 0.5), (1, "G5", 1), (2, "E5", 1), (3, "D5", 1),
         (4, "E5", 1), (5, "G5", 1), (6, "G5", 1.5), (7.5, "A5", 0.5),
         (8, "B5", 1), (9, "A5", 0.5), (9.5, "G5", 0.5), (10, "D5", 2),
         (12, "A4", 1), (13, "D5", 1), (14, "F#5", 2)],
        [(0, "E5", 0.5), (0.5, "G5", 0.5), (1, "B5", 1), (2, "G5", 1), (3, "F#5", 1),
         (4, "E5", 1.5), (5.5, "D5", 0.5), (6, "C5", 2),
         (8, "D5", 1), (9, "E5", 1), (10, "G5", 1), (11, "A5", 1),
         (12, "F#5", 1.5), (13.5, "E5", 0.5), (14, "D5", 2)],
        drums="march", lead_vel=0.5, arp_bright=0.7, pad_vel=0.4, kick_vel=0.5, snare_vel=0.32, wet=0.18)

def music_battle_moon():
    """Moonlit Forest - 'Moonlit Watch': 92 BPM, ~83.5 s, B minor (Bm F#m G Em), bell lead, sparse drums, big room."""
    return battle_song(
        92, 351, ["Bm", "F#m", "G", "Em"],
        {"Bm": ("F#3", "B3", "D4"), "F#m": ("F#3", "A3", "C#4"), "G": ("G3", "B3", "D4"), "Em": ("G3", "B3", "E4")},
        {"Bm": "B2", "F#m": "F#3", "G": "G3", "Em": "E3"},
        {"Bm": ["B3", "D4", "F#4", "B4", "F#4", "D4", "E4", "F#4"],
         "F#m": ["F#4", "A4", "C#5", "F#5", "C#5", "A4", "B4", "A4"],
         "G": ["G4", "B4", "D5", "G5", "D5", "B4", "A4", "B4"],
         "Em": ["E4", "G4", "B4", "E5", "B4", "G4", "F#4", "G4"]},
        [(0, "F#5", 2), (2, "D5", 2),
         (4, "C#5", 2), (6, "A4", 1), (7, "B4", 1),
         (8, "B4", 1), (9, "D5", 1), (10, "G5", 2),
         (12, "G5", 2), (14, "E5", 1), (15, "F#5", 1)],
        [(0, "B5", 1.5), (1.5, "A5", 0.5), (2, "F#5", 2),
         (4, "A5", 1), (5, "F#5", 1), (6, "C#5", 2),
         (8, "D5", 1), (9, "E5", 1), (10, "B4", 1), (11, "D5", 1),
         (12, "E5", 3), (15, "F#5", 1)],
        drums="sparse", lead="bell", lead_vel=0.32, arp_bright=0.4, arp_gain=0.9, pad_vel=0.46, kick_vel=0.45, snare_vel=0.2, hat_vel=0.07, wet=0.3)

def music_results():
    """Results - 100 BPM, 8 bars (~19.2 s), bright C major."""
    s = Song(100, 8, 321)
    chords = ["C", "F", "G", "C"] * 2
    PAD = {"C": ("C4", "E4", "G4"), "F": ("F3", "A3", "C4"), "G": ("G3", "B3", "D4")}
    ROOT = {"C": "C3", "F": "F3", "G": "G3"}
    bar_s = s.beat * 4
    for b, ch in enumerate(chords):
        s.add(pad(LSR, [note_hz(x) for x in PAD[ch]], bar_s + 0.6, 0.5, 0.35, 0.7), b * 4)
        s.add(bass_note(LSR, ROOT[ch], 2.5, s.beat), b * 4, 0.36)
        s.add(bass_note(LSR, ROOT[ch], 1.0, s.beat), b * 4 + 3, 0.3)
    melody = [(0, "G5", 1), (1, "E5", 1), (2, "C6", 2), (4, "A5", 1), (5, "F5", 1), (6, "A5", 2), (8, "B5", 1), (9, "G5", 1), (10, "D6", 2), (12, "C6", 4),
              (16, "E5", 1), (17, "G5", 1), (18, "C6", 2), (20, "A5", 1), (21, "C6", 1), (22, "F5", 2), (24, "G5", 2), (26, "B5", 2), (28, "C6", 4)]
    for st, name, ln in melody:
        s.add(marimba_note(LSR, note_hz(name), max(0.35, ln * s.beat + 0.4)), st, 0.55)
    s.drums(0, 8, "x.......x.......", hit_kick, 0.3)
    s.drums(0, 8, "....x.......x...", hit_snare, 0.18)
    s.drums(0, 8, "..x...x...x...x.", hit_shaker, 0.16, 0.1)
    return s.finish(0.22)

# ----------------------------------------------------------------------------- catalogue
SFX = {
    # UI
    "ui_click": sfx_ui_click, "ui_card_select": sfx_card_select, "ui_panel_open": sfx_panel_open, "ui_panel_close": sfx_panel_close,
    "ui_error": sfx_ui_error, "reward_pop": sfx_reward_pop, "star_pop": sfx_star_pop, "coin": sfx_coin, "coin_shower": sfx_coin_shower,
    "equip": sfx_equip, "chest_shake": sfx_chest_shake, "transition": sfx_transition, "unlock": sfx_unlock,
    "chest_open": sfx_chest_open, "upgrade": sfx_upgrade, "victory": sfx_victory, "defeat": sfx_defeat,
    # battle flow
    "countdown": sfx_countdown, "timer_tick": sfx_timer_tick, "turn_start": sfx_turn_start, "enemy_turn": sfx_enemy_turn,
    "turn_timeout": sfx_turn_timeout, "battle_start": sfx_battle_start,
    # launches
    "aim_start": sfx_aim_start,
    "launch_1": lambda: sfx_launch(11, 520, 1700, 0.34), "launch_2": lambda: sfx_launch(12, 450, 2000, 0.3), "launch_3": lambda: sfx_launch(13, 600, 1500, 0.38),
    "launch_light_1": lambda: sfx_launch_light(401, 1500, 4800, 330.0),
    "launch_light_2": lambda: sfx_launch_light(402, 1400, 4400, 300.0),
    "launch_light_3": lambda: sfx_launch_light(403, 1650, 5200, 370.0),
    "launch_heavy_1": lambda: sfx_launch_heavy(411, 300, 1000, 130.0),
    "launch_heavy_2": lambda: sfx_launch_heavy(412, 260, 900, 118.0),
    "launch_magic_1": lambda: sfx_launch_magic(421, ["C6", "D6", "E6", "G6", "A6"]),
    "launch_magic_2": lambda: sfx_launch_magic(422, ["D6", "E6", "G6", "A6", "C7"]),
    "bee_buzz": sfx_bee_buzz, "vine_whip": sfx_vine_whip,
    # impacts and destruction
    "wall_hit_1": lambda: sfx_wall_hit(1), "wall_hit_2": lambda: sfx_wall_hit(2), "wall_hit_3": lambda: sfx_wall_hit(3),
    "wall_break": sfx_wall_break, "explosion": sfx_explosion, "debris": sfx_debris,
    "wall_crack_1": lambda: sfx_wall_crack(431), "wall_crack_2": lambda: sfx_wall_crack(432),
    "castle_collapse": sfx_castle_collapse,
    "ground_thud_1": lambda: sfx_ground_thud(441), "ground_thud_2": lambda: sfx_ground_thud(442),
    "shield_block": sfx_shield_block, "magic_impact": sfx_magic_impact, "poison_hiss": sfx_poison_hiss,
    "guardian_hurt": sfx_guardian_hurt, "guardian_death": sfx_guardian_death, "crit": sfx_crit,
    # specials and tools
    "special_ready": sfx_special_ready, "special_buff": sfx_special_buff, "shield_up": sfx_shield_up, "wind_chime": sfx_wind_chime,
    "tool_use": sfx_tool_use, "heal": sfx_heal,
    # weather
    "wind_gust": sfx_wind_gust, "thunder": sfx_thunder,
}
LOOPS = {"amb_rain": ("Ambience", amb_rain), "amb_forest": ("Ambience", amb_forest),
         "music_menu": ("Music", music_menu), "music_battle": ("Music", music_battle), "music_battle_swamp": ("Music", music_battle_swamp),
         "music_battle_autumn": ("Music", music_battle_autumn), "music_battle_moon": ("Music", music_battle_moon),
         "music_results": ("Music", music_results)}

# Variants of one event are matched in short-term loudness (to the quietest), so random picks do not jump in level.
FAMILIES = [("launch_1", "launch_2", "launch_3"), ("launch_light_1", "launch_light_2", "launch_light_3"),
            ("launch_heavy_1", "launch_heavy_2"), ("launch_magic_1", "launch_magic_2"), ("wall_hit_1", "wall_hit_2", "wall_hit_3"),
            ("wall_crack_1", "wall_crack_2"), ("ground_thud_1", "ground_thud_2")]

def st_loudness(x, sr=SR, win=0.1):
    """Max 100 ms RMS after a 150 Hz high-pass (rough phone-speaker weighting)."""
    y = highpass1(x, sr, 150.0); W = max(1, int(win * sr)); best = 0.0; s2 = 0.0
    for i, v in enumerate(y):
        s2 += v * v
        if i >= W: s2 -= y[i - W] * y[i - W]
        if i >= W - 1: best = max(best, s2 / W)
    if len(y) < W: best = s2 / max(1, len(y))
    return math.sqrt(max(best, 1e-12))

def render_sfx(name):
    x = SFX[name](); x = trim_tail(x, SR); x = dc_block(x, SR)
    return fade(normalize(x, -1.0), SR, 0.0015, 0.012)

def main():
    args = sys.argv[1:]
    if "--list" in args:
        print("\n".join(list(SFX) + list(LOOPS))); return
    only = None
    if "--only" in args: only = set(args[args.index("--only") + 1].split(","))
    do_sfx = "--loops" not in args
    do_loops = "--sfx" not in args
    if do_sfx:
        family_of = {m: fam for fam in FAMILIES for m in fam}
        done = set()
        for name in SFX:
            if (only and name not in only) or name in done: continue
            group = family_of.get(name, (name,))
            rendered = {m: render_sfx(m) for m in group}
            if len(group) > 1:
                loud = {m: st_loudness(x) for m, x in rendered.items()}
                ref = min(loud.values())
                rendered = {m: [v * (ref / loud[m]) for v in x] for m, x in rendered.items()}
            for m, x in rendered.items():
                write_wav(os.path.join(OUT, "SFX", m + ".wav"), x, SR, zlib.crc32(m.encode()))
                print(f"sfx   {m:16s} {len(x) / SR:5.2f}s", flush=True)
                done.add(m)
    if do_loops:
        for name, (folder, fn) in LOOPS.items():
            if only and name not in only: continue
            x = dc_block(fn(), LSR, loop=True)
            x = normalize(x, -1.5 if folder == "Music" else -3.0)
            write_wav(os.path.join(OUT, folder, name + ".wav"), x, LSR, zlib.crc32(name.encode()))
            print(f"loop  {name:16s} {len(x) / LSR:5.2f}s", flush=True)

if __name__ == "__main__":
    main()
