"""Small DSP toolkit for The Twelfth Stamp's synthesized audio (numpy + scipy only).

Everything runs at 48 kHz. Mono signals are 1-D arrays, stereo signals are (2, n) arrays.
Physically-inspired building blocks: shaped noise, impulse/crackle trains, stick-slip friction,
modal resonators (glass, metal, wood, bells), membrane modes, additive tones, synthetic
convolution reverb, circular (seamless) loop tools, BS.1770 loudness + true peak, WAV/OGG output.
"""
import os
import subprocess

import numpy as np
from scipy import signal
from scipy.ndimage import maximum_filter1d

SR = 48000
TAU = 2 * np.pi


# ----------------------------------------------------------------------------- basics
def N(sec):
    return int(round(sec * SR))


def t_axis(n):
    return np.arange(n) / SR


def rng_for(name, salt=0):
    """Deterministic RNG per sound id so re-runs reproduce identical files."""
    h = 0
    for ch in f"{name}:{salt}":
        h = (h * 131 + ord(ch)) % (2 ** 32)
    return np.random.default_rng(h)


def db(x):
    return 20 * np.log10(np.maximum(np.abs(x), 1e-12))


def undb(d):
    return 10 ** (d / 20)


def place(dst, src, t0, gain=1.0):
    """Add src into dst starting at time t0 (seconds); clips at the end. Works for mono/stereo."""
    i = N(t0)
    if i >= dst.shape[-1]:
        return dst
    m = min(src.shape[-1], dst.shape[-1] - i)
    if dst.ndim == 1 and src.ndim == 1:
        dst[i:i + m] += gain * src[:m]
    elif dst.ndim == 2 and src.ndim == 1:
        dst[:, i:i + m] += gain * src[None, :m]
    else:
        dst[..., i:i + m] += gain * src[..., :m]
    return dst


def fade(x, fin=0.002, fout=0.005):
    x = x.copy()
    n = x.shape[-1]
    a, b = min(N(fin), n), min(N(fout), n)
    if a > 0:
        x[..., :a] *= np.sin(np.linspace(0, np.pi / 2, a)) ** 2
    if b > 0:
        x[..., n - b:] *= np.cos(np.linspace(0, np.pi / 2, b)) ** 2
    return x


def env_points(points, n, curve="lin"):
    """Piecewise envelope from [(t, v), ...]; curve 'lin' or 'exp' (exp interpolates in dB)."""
    t = t_axis(n)
    ts = np.array([p[0] for p in points], float)
    vs = np.array([p[1] for p in points], float)
    if curve == "exp":
        return undb(np.interp(t, ts, db(np.maximum(vs, 1e-6))))
    return np.interp(t, ts, vs)


def exp_env(n, t60, delay=0.0):
    t = t_axis(n) - delay
    e = np.exp(-6.9078 * np.maximum(t, 0) / t60)
    e[t < 0] = 0
    return e


def ar_env(n, attack, t60, delay=0.0):
    """Raised-cosine attack then exponential decay."""
    t = t_axis(n) - delay
    e = np.exp(-6.9078 * np.maximum(t - attack, 0) / t60)
    a = np.clip(t / max(attack, 1e-5), 0, 1)
    e *= np.sin(a * np.pi / 2) ** 2
    e[t < 0] = 0
    return e


# ----------------------------------------------------------------------------- noise
def white(n, rng):
    return rng.standard_normal(n)


def colored(n, rng, slope_db_oct=-3.0, lo=10.0, hi=None):
    """Noise with a spectral slope (dB/octave): 0 white, -3 pink, -6 brown-ish."""
    nfft = 1 << int(np.ceil(np.log2(n + 1)))
    spec = rng.standard_normal(nfft // 2 + 1) + 1j * rng.standard_normal(nfft // 2 + 1)
    f = np.fft.rfftfreq(nfft, 1 / SR)
    g = (np.maximum(f, lo) / 1000.0) ** (slope_db_oct / 6.0206)
    g[f < lo * 0.5] = 0
    if hi:
        g[f > hi] *= np.exp(-((f[f > hi] - hi) / (0.15 * hi)) ** 2)
    x = np.fft.irfft(spec * g, nfft)[:n]
    return x / (np.std(x) + 1e-12)


def shaped_noise(n, mag_fn, rng, nfft=2048, hop=None):
    """Noise whose time-varying magnitude spectrum is mag_fn(t[frames], f[bins]) -> (bins, frames).

    Random-phase overlap-add synthesis (Hann window, 75 % overlap)."""
    hop = hop or nfft // 4
    nf = n // hop + 4
    tf = (np.arange(nf) * hop) / SR  # frame k is centred at output sample k*hop
    f = np.fft.rfftfreq(nfft, 1 / SR)
    M = mag_fn(np.maximum(tf, 0), f)
    Z = (rng.standard_normal(M.shape) + 1j * rng.standard_normal(M.shape)) * M
    fr = np.fft.irfft(Z, n=nfft, axis=0) * np.hanning(nfft)[:, None]
    out = np.zeros(nf * hop + nfft)
    for k in range(nf):
        out[k * hop:k * hop + nfft] += fr[:, k]
    out = out[nfft // 2: nfft // 2 + n]
    return out


def smooth_random(n, rate_hz, rng, lo=0.0, hi=1.0, order=3):
    """Slowly varying random control signal in [lo, hi] with ~rate_hz bandwidth."""
    from scipy.ndimage import uniform_filter1d
    k = max(int(n * rate_hz / SR) + 4, 4)
    pts = rng.uniform(0, 1, k)
    x = np.interp(np.linspace(0, k - 1, n), np.arange(k), pts)
    # smooth with a few passes of a moving average for curvature (O(n) running mean)
    w = max(int(SR / max(rate_hz, 1e-3) / 2), 1)
    for _ in range(order - 1):
        if 1 < w < n:
            x = uniform_filter1d(x, size=w, mode="nearest")
    x = (x - x.min()) / (x.max() - x.min() + 1e-12)
    return lo + (hi - lo) * x


# ----------------------------------------------------------------------------- filters
def sos(kind, f, order=2):
    if kind in ("bandpass", "bandstop"):
        f = [max(f[0], 5.0), min(f[1], SR / 2 * 0.98)]
    else:
        f = min(max(f, 5.0), SR / 2 * 0.98)
    return signal.butter(order, f, btype=kind, fs=SR, output="sos")


def filt(x, kind, f, order=2):
    return signal.sosfilt(sos(kind, f, order), x, axis=-1)


def lp(x, f, order=2):
    return filt(x, "lowpass", f, order)


def hp(x, f, order=2):
    return filt(x, "highpass", f, order)


def bp(x, lo, hi, order=2):
    return filt(x, "bandpass", (lo, hi), order)


def peak_eq(x, f, gain_db, q=1.0):
    A = 10 ** (gain_db / 40)
    w = TAU * f / SR
    al = np.sin(w) / (2 * q)
    b = [1 + al * A, -2 * np.cos(w), 1 - al * A]
    a = [1 + al / A, -2 * np.cos(w), 1 - al / A]
    return signal.lfilter(np.array(b) / a[0], np.array(a) / a[0], x, axis=-1)


def shelf(x, f, gain_db, high=True, s=0.8):
    A = 10 ** (gain_db / 40)
    w = TAU * f / SR
    al = np.sin(w) / 2 * np.sqrt((A + 1 / A) * (1 / s - 1) + 2)
    c = np.cos(w)
    if high:
        b = [A * ((A + 1) + (A - 1) * c + 2 * np.sqrt(A) * al), -2 * A * ((A - 1) + (A + 1) * c),
             A * ((A + 1) + (A - 1) * c - 2 * np.sqrt(A) * al)]
        a = [(A + 1) - (A - 1) * c + 2 * np.sqrt(A) * al, 2 * ((A - 1) - (A + 1) * c),
             (A + 1) - (A - 1) * c - 2 * np.sqrt(A) * al]
    else:
        b = [A * ((A + 1) - (A - 1) * c + 2 * np.sqrt(A) * al), 2 * A * ((A - 1) - (A + 1) * c),
             A * ((A + 1) - (A - 1) * c - 2 * np.sqrt(A) * al)]
        a = [(A + 1) + (A - 1) * c + 2 * np.sqrt(A) * al, -2 * ((A - 1) + (A + 1) * c),
             (A + 1) + (A - 1) * c - 2 * np.sqrt(A) * al]
    return signal.lfilter(np.array(b) / a[0], np.array(a) / a[0], x, axis=-1)


def resonator(x, f, t60, gain=1.0):
    """Two-pole resonator: a decaying mode at f Hz with T60 decay, unity-ish peak gain."""
    r = np.exp(-6.9078 / (t60 * SR))
    w = TAU * f / SR
    a = [1.0, -2 * r * np.cos(w), r * r]
    b = [(1 - r * r) * np.sin(w) * gain]
    return signal.lfilter(b, a, x, axis=-1)


def resonator_bank(x, freqs, t60s, gains):
    y = np.zeros_like(x, dtype=float)
    for f, t, g in zip(freqs, t60s, gains):
        if f < SR * 0.45:
            y = y + resonator(x, f, t, g)
    return y


def tv_bandpass(x, fc, q, block=256):
    """Time-varying 2-pole band-pass (centre fc[n] array) using block-wise coefficients + carried state."""
    y = np.zeros_like(x)
    zi = np.zeros(2)
    for s in range(0, len(x), block):
        e = min(s + block, len(x))
        f = float(np.clip(fc[(s + e) // 2], 20, SR * 0.45))
        b, a = signal.iirpeak(f, q, fs=SR)
        y[s:e], zi = signal.lfilter(b, a, x[s:e], zi=zi)
    return y


def tv_lowpass(x, fc, block=256):
    """Time-varying one-pole low-pass (fc[n] array)."""
    y = np.zeros_like(x)
    zi = np.zeros(1)
    for s in range(0, len(x), block):
        e = min(s + block, len(x))
        f = float(np.clip(fc[(s + e) // 2], 10, SR * 0.45))
        k = np.exp(-TAU * f / SR)
        y[s:e], zi = signal.lfilter([1 - k], [1, -k], x[s:e], zi=zi)
    return y


# ----------------------------------------------------------------------------- excitations
def impulse_train(n, rate, rng, jitter=0.3, amp_sigma=0.5, t0=0.0):
    """Quasi-periodic impulse train; rate can be scalar or per-sample array (Hz)."""
    rate = np.broadcast_to(np.asarray(rate, float), (n,))
    ph = np.cumsum(rate / SR)
    out = np.zeros(n)
    k = 1
    i0 = N(t0)
    pos = np.searchsorted(ph, np.arange(1, int(ph[-1]) + 2))
    for p in pos:
        if p >= n:
            break
        j = int(p + rng.normal(0, jitter) * SR / max(rate[min(p, n - 1)], 1.0) * 0.5)
        if i0 <= j < n:
            out[j] += np.exp(rng.normal(0, amp_sigma))
        k += 1
    return out


def poisson_clicks(n, rate, rng, amp_sigma=0.8):
    """Poisson impulses with time-varying rate (Hz) and log-normal amplitudes."""
    rate = np.broadcast_to(np.asarray(rate, float), (n,))
    hits = rng.random(n) < rate / SR
    out = np.zeros(n)
    k = int(hits.sum())
    out[hits] = np.exp(rng.normal(0, amp_sigma, k)) * rng.choice([-1, 1], k)
    return out


def grains(clicks, rng, length=0.0008, color="white"):
    """Turn impulses into short noise grains (crackle)."""
    g = rng.standard_normal(max(N(length), 4)) * np.exp(-np.linspace(0, 5, max(N(length), 4)))
    return signal.fftconvolve(clicks, g)[:len(clicks)]


def crackle(n, rate, rng, lo=1500, hi=9000, grain=0.0006, amp_sigma=0.9):
    c = grains(poisson_clicks(n, rate, rng, amp_sigma), rng, grain)
    return bp(c, lo, hi, 2)


def stick_slip(n, rate, rng, modes, jitter=0.25, amp_sigma=0.35, noise=0.15):
    """Friction creak: quasi-periodic slip impulses exciting a resonator bank.

    modes: list of (freq, t60, gain)."""
    exc = impulse_train(n, rate, rng, jitter=jitter, amp_sigma=amp_sigma)
    # each slip also releases a little broadband rubbing noise proportional to its strength
    local = signal.fftconvolve(np.abs(exc), np.hanning(48), mode="same")
    exc = exc + noise * rng.standard_normal(n) * local
    f, t, g = zip(*modes)
    return resonator_bank(exc, f, t, g)


def click(rng, dur=0.004, lo=1000, hi=12000):
    """Very short broadband click grain (zero-padded so the band-pass can ring out)."""
    n = max(N(dur), 8)
    x = rng.standard_normal(n) * np.exp(-np.linspace(0, 7, n))
    return bp(np.concatenate([x, np.zeros(n)]), lo, hi, 2)


# ----------------------------------------------------------------------------- modal / tonal
def modal(n, freqs, amps, t60s, rng=None, phase="sin", attack=0.0005, detune=None):
    """Sum of exponentially decaying partials (struck object)."""
    t = t_axis(n)
    y = np.zeros(n)
    for i, (f, a, T) in enumerate(zip(freqs, amps, t60s)):
        if f <= 0 or f >= SR * 0.47 or a == 0:
            continue
        ph = rng.uniform(0, TAU) if (rng is not None and phase == "rand") else 0.0
        y += a * np.exp(-6.9078 * t / T) * np.sin(TAU * f * t + ph)
    if attack > 0:
        k = min(N(attack), n)
        y[:k] *= np.sin(np.linspace(0, np.pi / 2, k)) ** 2
    return y


def glide_tone(n, f_start, f_end, curve=1.0, harmonics=((1, 1.0),)):
    """Tone whose frequency glides from f_start to f_end (power curve)."""
    u = np.linspace(0, 1, n) ** curve
    f = f_start + (f_end - f_start) * u
    ph = TAU * np.cumsum(f) / SR
    y = np.zeros(n)
    for h, a in harmonics:
        y += a * np.sin(h * ph)
    return y


def osc(freq, n, phase=0.0):
    """Sine oscillator; freq scalar or per-sample array."""
    if np.isscalar(freq):
        return np.sin(TAU * freq * t_axis(n) + phase)
    return np.sin(TAU * np.cumsum(freq) / SR + phase)


def additive(freq, n, partials, phase=0.0):
    """partials: list of (ratio, amp); freq scalar or array; skips partials above 20 kHz."""
    if np.isscalar(freq):
        base = TAU * freq * t_axis(n)
        fmax = freq
    else:
        base = TAU * np.cumsum(freq) / SR
        fmax = float(np.max(freq))
    y = np.zeros(n)
    for r, a in partials:
        if r * fmax < 20000:
            y += a * np.sin(r * base + phase * r)
    return y


def pluck(f0, dur, rng, bright=0.5, decay=2.5, pos=0.21, inharm=0.0004, body=True):
    """Additive plucked string (oud/guitar-ish): harmonics with frequency-dependent decay."""
    n = N(dur)
    t = t_axis(n)
    y = np.zeros(n)
    kmax = int(min(16000 / f0, 60))
    for k in range(1, kmax + 1):
        fk = k * f0 * np.sqrt(1 + inharm * k * k)
        if fk > 18000:
            break
        a = abs(np.sin(np.pi * k * pos)) / k ** (1.6 - bright)
        T = decay / (1 + 0.012 * (k ** 1.6) * (1 + 2 * (1 - bright)))
        y += a * np.exp(-6.9078 * t / T) * np.sin(TAU * fk * t + rng.uniform(0, 0.3))
    att = rng.standard_normal(N(0.006)) * np.exp(-np.linspace(0, 6, N(0.006)))
    y[:len(att)] += 0.08 * lp(att, 3000 + 4000 * bright)
    y[:N(0.002)] *= np.linspace(0, 1, N(0.002))
    if body:
        y = y + 0.25 * resonator(y, 110, 0.08) + 0.15 * resonator(y, 230, 0.06)
    return y


def bell_partials(f_nom, kind="church"):
    """Partial ratios/amps/T60 for bells, relative to the nominal (strike) frequency."""
    if kind == "church":
        # hum, prime, tierce, quint, nominal, superquint, octave-nominal ...
        r = [0.5, 1.0, 1.2, 1.5, 2.0, 2.5, 2.67, 3.0, 4.0, 5.33, 6.0]
        a = [0.55, 0.65, 0.5, 0.18, 0.9, 0.35, 0.3, 0.25, 0.15, 0.08, 0.05]
        T = [11.0, 7.5, 6.0, 3.5, 4.5, 2.6, 2.2, 1.8, 1.2, 0.8, 0.6]
        return [x * f_nom / 2 for x in r], a, T
    if kind == "glass":
        r = [1.0, 2.32, 4.25, 6.63, 9.38]
        a = [1.0, 0.45, 0.22, 0.1, 0.05]
        T = [3.0, 1.6, 0.9, 0.5, 0.3]
        return [x * f_nom for x in r], a, T
    if kind == "small":  # small brass bell / wind chime
        r = [1.0, 2.76, 5.40, 8.93]
        a = [1.0, 0.5, 0.25, 0.12]
        T = [2.2, 1.0, 0.5, 0.25]
        return [x * f_nom for x in r], a, T
    raise ValueError(kind)


def bell(f_nom, dur, rng, kind="church", doublet=0.6, bright=1.0, t60_scale=1.0):
    """Struck bell with split (beating) partials and a clapper transient."""
    fr, am, T = bell_partials(f_nom, kind)
    n = N(dur)
    y = np.zeros(n)
    t = t_axis(n)
    for f, a, tt in zip(fr, am, T):
        if f >= 18000:
            continue
        a = a * (bright if f > 2 * f_nom else 1.0)
        d = rng.uniform(0.2, 1.0) * doublet
        for s in (-1, 1):
            y += 0.5 * a * np.exp(-6.9078 * t / (tt * t60_scale)) * np.sin(TAU * (f + s * d / 2) * t + rng.uniform(0, TAU))
    strike = rng.standard_normal(N(0.004)) * np.exp(-np.linspace(0, 6, N(0.004)))
    y[:len(strike)] += 0.15 * bp(strike, 800, 7000)
    y[:N(0.0015)] *= np.linspace(0, 1, N(0.0015))
    return y


def membrane(n, f0, rng, decay=0.45, drop=1.18, drop_time=0.05):
    """Circular membrane (drum) modes with an initial pitch drop (tension modulation)."""
    ratios = [1.0, 1.59, 2.14, 2.30, 2.65, 2.92, 3.16, 3.50]
    amps = [1.0, 0.45, 0.32, 0.22, 0.18, 0.12, 0.08, 0.06]
    t = t_axis(n)
    pitch = 1 + (drop - 1) * np.exp(-t / drop_time)
    y = np.zeros(n)
    for i, (r, a) in enumerate(zip(ratios, amps)):
        T = decay / (1 + 0.9 * i)
        ph = TAU * np.cumsum(f0 * r * pitch) / SR
        y += a * np.exp(-6.9078 * t / T) * np.sin(ph + rng.uniform(0, 0.4))
    return y


# ----------------------------------------------------------------------------- space
def make_ir(rt60=1.0, rng=None, predelay=0.008, hf=0.45, lf=1.1, stereo=True, er=6, dur=None, width=1.0):
    """Synthetic reverb impulse response: band-wise exponentially decaying noise + early reflections.

    hf: ratio of RT60 at 8 kHz to mid RT60 (air/absorption damping); lf: ratio at 125 Hz."""
    rng = rng or np.random.default_rng(1)
    dur = dur or min(rt60 * 1.3 + predelay, 9.0)
    n = N(dur)
    t = t_axis(n)
    bands = [(20, 180, lf), (180, 700, (lf + 1) / 2), (700, 2000, 1.0), (2000, 5000, (1 + hf) / 2),
             (5000, 11000, hf), (11000, 23500, hf * 0.7)]
    chans = []
    for c in range(2 if stereo else 1):
        x = np.zeros(n)
        nz = rng.standard_normal(n)
        for lo, hi, k in bands:
            b = bp(nz, lo, hi, 3)
            x += b * np.exp(-6.9078 * t / (rt60 * k))
        # soften the onset of the diffuse tail
        x *= np.clip((t - predelay) / 0.012, 0, 1)
        for i in range(er):
            d = predelay * 0.3 + rng.uniform(0.002, 0.035 + 0.02 * rt60)
            j = N(d)
            if j < n:
                x[j] += rng.choice([-1, 1]) * rng.uniform(0.3, 0.8) * np.exp(-d * 6.9 / rt60) * 2.0
        x = x / np.sqrt(np.sum(x ** 2) + 1e-12)
        chans.append(x)
    ir = np.stack(chans) if stereo else chans[0]
    if stereo and width < 1.0:
        m = ir.mean(axis=0)
        ir = width * ir + (1 - width) * m[None, :]
    return ir


def convolve(x, ir):
    """Convolve mono/stereo x with mono/stereo IR; returns stereo if either is stereo."""
    if x.ndim == 1 and ir.ndim == 1:
        return signal.fftconvolve(x, ir)
    xs = np.atleast_2d(x)
    irs = np.atleast_2d(ir)
    if xs.shape[0] == 1:
        xs = np.repeat(xs, irs.shape[0], axis=0)
    if irs.shape[0] == 1:
        irs = np.repeat(irs, xs.shape[0], axis=0)
    return np.stack([signal.fftconvolve(a, b) for a, b in zip(xs, irs)])


def reverb(x, ir, wet=0.3, dry=1.0, keep_len=True):
    w = convolve(x, ir)
    d = x if (x.ndim == w.ndim) else np.stack([x, x])
    out = np.zeros_like(w)
    out[..., :d.shape[-1]] += dry * d
    out += wet * w
    if keep_len:
        out = out[..., :d.shape[-1]]
    return out


def pan(x, p):
    """Equal-power pan of mono x; p in [-1, 1] (scalar or array)."""
    th = (np.asarray(p) + 1) * np.pi / 4
    return np.stack([x * np.cos(th), x * np.sin(th)])


def to_stereo(x):
    return x if x.ndim == 2 else np.stack([x, x])


def mono(x):
    return x if x.ndim == 1 else x.mean(axis=0)


# ----------------------------------------------------------------------------- loops
def fold(x, L):
    """Circularly fold a signal longer than L into L samples (event tails wrap to the start)."""
    out = np.zeros(x.shape[:-1] + (L,))
    for k in range(0, x.shape[-1], L):
        seg = x[..., k:k + L]
        out[..., :seg.shape[-1]] += seg
    return out


def circ_sosfilt(s, x, warm):
    xx = np.concatenate([x[..., -warm:], x], axis=-1)
    return signal.sosfilt(s, xx, axis=-1)[..., warm:]


def circ_convolve(x, ir):
    """Circular convolution: reverb tail of the loop end wraps into its start."""
    L = x.shape[-1]
    return fold(convolve(x, ir), L)


def loop_crossfade(x, L, F):
    """Equal-power crossfade of the tail x[L:L+F] into the head x[:F] -> seamless loop of L samples.

    The law is corrected for the measured head/tail correlation rho so power stays constant
    (rho = 0 -> classic sin/cos equal-power; rho -> 1 -> equal-gain)."""
    assert x.shape[-1] >= L + F
    head = x[..., :F]
    tail = x[..., L:L + F]
    hm, tm = mono(head), mono(tail)
    rho = float(np.dot(hm, tm) / (np.sqrt(np.dot(hm, hm) * np.dot(tm, tm)) + 1e-12))
    rho = float(np.clip(rho, 0.0, 1.0))
    u = np.linspace(0, 1, F)
    a0, b0 = np.sin(u * np.pi / 2), np.cos(u * np.pi / 2)
    nrm = np.sqrt(a0 ** 2 + b0 ** 2 + 2 * rho * a0 * b0)
    y = x[..., :L].copy()
    y[..., :F] = head * (a0 / nrm) + tail * (b0 / nrm)
    return y


def rotate_to_zero_crossing(x, search=0.01):
    """Roll a seamless (circular) loop so the file starts on an upward zero crossing of the mid signal."""
    m = mono(x)
    k = N(search)
    idx = np.where((m[:-1] < 0) & (m[1:] >= 0))[0] + 1
    if len(idx) == 0:
        return x, 0
    # prefer crossings close to the start (either side, circularly)
    L = len(m)
    cand = idx[(idx < k) | (idx > L - k)]
    if len(cand) == 0:
        cand = idx
    dist = np.minimum(cand, L - cand)
    r = int(cand[np.argmin(dist)])
    return np.roll(x, -r, axis=-1), r


def seam_report(x):
    """Compare the wrap-around step (last -> first sample) with ordinary sample-to-sample steps,
    and the RMS just before/after the seam."""
    m = mono(x)
    d = np.abs(np.diff(m))
    jump = abs(m[0] - m[-1])
    p999 = float(np.percentile(d, 99.9)) + 1e-12
    w = N(0.05)
    r_end = np.sqrt(np.mean(m[-w:] ** 2)) + 1e-12
    r_start = np.sqrt(np.mean(m[:w] ** 2)) + 1e-12
    return {"jump_over_p999": round(float(jump / p999), 3), "rms_step_db": round(float(db(r_start / r_end)), 2),
            "first": float(m[0]), "last": float(m[-1])}


def make_loop(x, L, F):
    """Full loop recipe: correlation-aware equal-power crossfade, then start on a zero crossing."""
    y = loop_crossfade(x, L, F)
    y, _ = rotate_to_zero_crossing(y)
    return y


# ----------------------------------------------------------------------------- loudness
_KW = np.array([
    [1.53512485958697, -2.69169618940638, 1.19839281085285, 1.0, -1.69065929318241, 0.73248077421585],
    [1.0, -2.0, 1.0, 1.0, -1.99004745483398, 0.99007225036621],
])


def lufs(x):
    """ITU-R BS.1770-4 integrated loudness (48 kHz). Returns None for clips < 0.4 s."""
    xs = np.atleast_2d(x)
    y = signal.sosfilt(_KW, xs, axis=-1)
    blk, hop = N(0.4), N(0.1)
    if y.shape[1] < blk:
        return None
    nb = 1 + (y.shape[1] - blk) // hop
    cs = np.concatenate([np.zeros((y.shape[0], 1)), np.cumsum(y ** 2, axis=1)], axis=1)
    idx = np.arange(nb) * hop
    ms = ((cs[:, idx + blk] - cs[:, idx]) / blk).sum(axis=0)
    lk = -0.691 + 10 * np.log10(ms + 1e-20)
    g = ms[lk > -70]
    if len(g) == 0:
        return -np.inf
    rel = -0.691 + 10 * np.log10(np.mean(g)) - 10
    g2 = ms[(lk > -70) & (lk > rel)]
    return float(-0.691 + 10 * np.log10(np.mean(g2)))


def true_peak_db(x):
    xs = np.atleast_2d(x)
    up = signal.resample_poly(xs, 4, 1, axis=-1)
    return float(db(np.max(np.abs(up))))


def loud_max(x, win=0.1, hop=0.01):
    """Peak short-window loudness of a one-shot (K-weighted like BS.1770, LUFS-scaled): the loudest `win` seconds.

    Integrated LUFS is meaningless for transients (ebur128 gates them out below 0.4 s), and peak normalisation
    makes a click and a sustained tone 'equal'. Energy over ~100 ms follows the ear's temporal integration, so a
    5 ms click and a 1 s tone are judged on the same scale. Clips shorter than the window are zero-padded."""
    y = signal.sosfilt(_KW, np.atleast_2d(x), axis=-1)
    w = max(N(win), 1)
    if y.shape[1] < w:
        y = np.concatenate([y, np.zeros((y.shape[0], w - y.shape[1]))], axis=1)
    cs = np.concatenate([np.zeros((y.shape[0], 1)), np.cumsum(y ** 2, axis=1)], axis=1)
    idx = np.arange(0, y.shape[1] - w + 1, max(N(hop), 1))
    ms = ((cs[:, idx + w] - cs[:, idx]) / w).sum(axis=0)
    return float(-0.691 + 10 * np.log10(ms.max() + 1e-20))


def norm_loud(x, target, tp_max=-2.0, clamp=None, max_gr=0.0):
    """Gain a one-shot so loud_max() hits target, never above tp_max dBTP.

    clamp=(lo, hi) limits the change (dB) relative to plain -3 dBTP peak normalisation, so re-levelling an existing
    clip cannot move it further than that from what the game was tuned against. max_gr > 0 lets a fast lookahead
    limiter shave up to that many dB off the first milliseconds of a transient (a heel snap is a 1-3 ms spike) so
    hard-surface footsteps can reach the same loudness as soft ones; 0 = no limiting. Returns (y, gain_db)."""
    tp0 = true_peak_db(x)
    g = target - loud_max(x)
    if clamp is not None:
        ref = -3.0 - tp0
        g = float(np.clip(g, ref + clamp[0], ref + clamp[1]))
    if tp0 + g <= tp_max:
        return x * undb(g), g
    if max_gr <= 0:
        g = tp_max - tp0
        return x * undb(g), g
    g = min(g, tp_max + max_gr - tp0)
    ceiling = tp_max - 0.4
    z = x * undb(g)
    for _ in range(6):
        z = limiter(x * undb(g), ceiling, lookahead=0.0015, release=0.035)
        miss = target - loud_max(z)
        if abs(miss) < 0.1 or g >= tp_max + max_gr - tp0 - 1e-6:
            break
        g = min(g + miss, tp_max + max_gr - tp0)
    tp = true_peak_db(z)
    if tp > tp_max:
        z = z * undb(tp_max - tp)
    return z, g


def sample_peak_db(x):
    return float(db(np.max(np.abs(x))))


def limiter(x, ceiling_db=-3.0, lookahead=0.002, release=0.06):
    """Linked-channel lookahead peak limiter (block-smoothed gain, instant attack via max filter)."""
    xs = np.atleast_2d(x)
    c = undb(ceiling_db)
    a = np.max(np.abs(xs), axis=0)
    la = max(N(lookahead), 1)
    pk = maximum_filter1d(a, size=2 * la + 1)
    g = np.minimum(1.0, c / np.maximum(pk, 1e-12))
    # release smoothing in blocks (gain may only rise slowly)
    blk = 32
    nb = int(np.ceil(len(g) / blk))
    gb = np.array([g[i * blk:(i + 1) * blk].min() for i in range(nb)])
    coef = np.exp(-blk / (release * SR))
    out = np.empty(nb)
    cur = 1.0
    for i in range(nb):
        cur = gb[i] if gb[i] < cur else gb[i] + (cur - gb[i]) * coef
        out[i] = cur
    from scipy.ndimage import uniform_filter1d
    gs = np.repeat(out, blk)[:len(g)]
    # short smoothing of the gain curve (never above the instantaneous requirement)
    sm = uniform_filter1d(gs, size=max(la, 2), mode="nearest")
    gs = np.minimum(np.minimum(gs, sm), g)
    y = xs * gs[None, :]
    return y if x.ndim == 2 else y[0]


def norm_peak(x, target_db=-3.0):
    """Normalise so the TRUE peak sits at target_db."""
    return x * undb(target_db - true_peak_db(x))


def norm_lufs(x, target, tp_max=-3.0, circular=False):
    """Normalise integrated loudness to target with the TRUE peak held <= tp_max.

    If plain gain would break the ceiling, a lookahead limiter is applied and its (sample-peak)
    ceiling is lowered until the 4x-oversampled true peak complies (HF-rich material such as
    crackle has inter-sample overs). circular=True limits a loop with wrap-around padding so the
    gain curve is continuous across the loop point."""
    y = x * undb(target - lufs(x))
    tp = true_peak_db(y)
    if tp <= tp_max:
        return y
    pad = N(0.3)

    def lim(sig, c):
        if not circular:
            return limiter(sig, c)
        s2 = np.atleast_2d(sig)
        ext = np.concatenate([s2[:, -pad:], s2, s2[:, :pad]], axis=1)
        out = limiter(ext, c)[:, pad:-pad]
        return out if sig.ndim == 2 else out[0]
    ceiling = tp_max - 0.3
    z = y
    for _ in range(10):
        z = lim(y, ceiling)
        z = z * undb(target - lufs(z))
        tp = true_peak_db(z)
        if tp <= tp_max + 0.02:
            return z
        ceiling -= (tp - tp_max) + 0.1
    return z * undb(min(0.0, tp_max - tp))


# ----------------------------------------------------------------------------- output
def write_wav(path, x, bits=16, rng=None):
    """Write 48 kHz PCM16 WAV with TPDF dither. x: mono (n,) or stereo (2, n), float in [-1, 1]."""
    import soundfile as sf
    rng = rng or np.random.default_rng(12345)
    xs = np.atleast_2d(x).T.copy()
    q = 32767.0
    d = (rng.random(xs.shape) - rng.random(xs.shape))
    xi = np.clip(np.round(xs * q + d), -32768, 32767).astype(np.int16)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    sf.write(path, xi if xi.shape[1] > 1 else xi[:, 0], SR, subtype="PCM_16")
    return xi


def encode_ogg(wav_path, ogg_path, quality):
    os.makedirs(os.path.dirname(ogg_path), exist_ok=True)
    cmd = ["ffmpeg", "-hide_banner", "-loglevel", "error", "-y", "-i", wav_path, "-c:a", "libvorbis",
           "-q:a", str(quality), "-ar", str(SR), "-map_metadata", "-1", ogg_path]
    subprocess.run(cmd, check=True)
    return cmd
