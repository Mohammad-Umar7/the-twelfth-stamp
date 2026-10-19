"""Derived clip mom_01_glitch.wav: the mother's last word "unpacked" repeated eerily (~4 s).

  Tools/.venv/Scripts/python.exe -I Tools/audio/make_voice_glitch.py

Word boundary: Whisper word timestamps (Audio/_work/voice_transcripts_*.json, from verify_voice.py)
give a rough window; the exact cut is then snapped to the waveform energy (10 ms RMS):
  start = first 5 ms frame after the deepest energy dip inside the ASR window that is both >= 15 dB
          above the dip and voiced (zero-crossing rate < 2500/s), i.e. the vowel of "un-" without the
          /v/ frication of "you've" (an earlier cut at 1.745 s was heard as "fun packed")
  end   = last frame above -50 dBFS (the /t/ release of "-cked"), plus 20 ms
Build (ffmpeg atrim/asplit/asetrate/concat): 4 repeats, gaps 0.30 / 0.42 / 0.36 s, tape-style pitch
drop 1.00 / 0.97 / 0.94 / 0.90, 30 ms quarter-sine onsets, the last repeat cut off mid-word (inside the "pa"
vowel), padded to 4.0 s.
"""
import argparse
import glob
import json
import os
import subprocess

import numpy as np
import soundfile as sf

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
VOICE = os.path.join(ROOT, "Game", "Assets", "Resources", "Audio", "Voice")
WORK = os.path.join(ROOT, "Audio", "_work")
SRC = os.path.join(VOICE, "mom_01.wav")
OUT = os.path.join(VOICE, "mom_01_glitch.wav")
SR = 48000
FADE_IN = 0.03  # natural vowel onset; 5 ms read as a plosive ("fun packed") to ASR


def frames_db(x, hop):
    n = len(x) // hop
    return 20 * np.log10(np.sqrt(np.mean(x[:n * hop].reshape(n, hop) ** 2, axis=1)) + 1e-9)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--fade-in", type=float, default=FADE_IN, help="onset fade of every repeat (s)")
    ap.add_argument("--out", default=OUT)
    args = ap.parse_args()
    x, sr = sf.read(SRC)
    assert sr == SR
    hop = SR // 100  # 10 ms
    e = frames_db(x, hop)

    # ASR window (union over available models)
    starts, ends, used = [], [], []
    for p in sorted(glob.glob(os.path.join(WORK, "voice_transcripts_*.json"))):
        with open(p, encoding="utf-8") as fh:
            d = json.load(fh)
        words = d["clips"].get("mom_01", {}).get("words", [])
        for w in words:
            if w["w"].lower().strip(".,!?") == "unpacked":
                starts.append(w["start"])
                ends.append(w["end"])
                used.append(f"{d['model']}: {w['start']:.2f}-{w['end']:.2f}")
    if starts:
        lo, hi = min(starts) - 0.10, max(starts) + 0.05
    else:  # energy only: search the last second of speech
        lo, hi = len(x) / SR - 1.2, len(x) / SR - 0.4
    i0, i1 = int(lo * 100), int(hi * 100)
    dip = i0 + int(np.argmin(e[i0:i1]))
    h5 = SR // 200  # 5 ms frames for the onset
    e5 = frames_db(x, h5)
    n5 = len(x) // h5
    zcr = np.array([np.mean(np.abs(np.diff(np.sign(x[j * h5:(j + 1) * h5])))) / 2 * SR for j in range(n5)])
    k = dip * 2
    while k < n5 - 1 and not (e5[k] >= e[dip] + 15 and zcr[k] < 2500):
        k += 1
    start = k * 0.005
    last = int(np.where(e > -50)[0][-1])
    end = (last + 1) * 0.01 + 0.02
    word = end - start
    print(f"ASR windows: {used}")
    print(f"energy dip at {dip * 0.01:.2f}s ({e[dip]:.1f} dB) -> word start {start:.3f}s; last >-50dB frame "
          f"{last * 0.01:.2f}s -> word end {end:.3f}s; word length {word:.3f}s")

    rates = [1.00, 0.97, 0.94, 0.90]
    gaps = [0.30, 0.42, 0.36]
    gains = [0.0, -1.0, -2.0, -0.5]
    cut_frac = 0.55  # last repeat stops 55 % into the (slowed) word, inside the "pa" vowel
    parts = [f"[0:a]atrim=start={start:.4f}:end={end:.4f},asetpts=PTS-STARTPTS,asplit=4[w0][w1][w2][w3]"]
    for i, r in enumerate(rates):
        chain = f"[w{i}]"
        if r != 1.0:
            chain += f"asetrate={int(round(SR * r))},aresample={SR}:resampler=soxr,"
        if i == 3:
            chain += f"atrim=end={word / r * cut_frac:.4f},asetpts=PTS-STARTPTS,"
            fo = 0.003
        else:
            fo = 0.010
        chain += (f"volume={gains[i]}dB,afade=t=in:curve=qsin:d={args.fade_in},areverse,afade=t=in:d={fo},areverse,"
                  f"aformat=sample_fmts=flt:sample_rates={SR}:channel_layouts=mono[r{i}]")
        parts.append(chain)
    for j, g in enumerate(gaps):
        parts.append(f"aevalsrc=0:c=mono:s={SR}:d={g},aformat=sample_fmts=flt:sample_rates={SR}:channel_layouts=mono[g{j}]")
    parts.append("[r0][g0][r1][g1][r2][g2][r3]concat=n=7:v=0:a=1,apad=whole_dur=4.0,"
                 f"aresample={SR}:dither_method=triangular,aformat=sample_fmts=s16:sample_rates={SR}:channel_layouts=mono[out]")
    fc = ";".join(parts)
    cmd = ["ffmpeg", "-hide_banner", "-loglevel", "error", "-y", "-i", SRC, "-filter_complex", fc,
           "-map", "[out]", "-c:a", "pcm_s16le", "-map_metadata", "-1", args.out]
    subprocess.run(cmd, check=True)
    info = {"word_start": round(start, 3), "word_end": round(end, 3), "asr": used, "energy_dip_s": round(dip * 0.01, 2),
            "rates": rates, "gaps": gaps, "cut_fraction": cut_frac, "fade_in": args.fade_in, "ffmpeg_filter_complex": fc}
    if os.path.abspath(args.out) == os.path.abspath(OUT):
        with open(os.path.join(WORK, "voice_glitch_log.json"), "w", encoding="utf-8") as fh:
            json.dump(info, fh, indent=1)
    print("wrote", args.out)


if __name__ == "__main__":
    main()
