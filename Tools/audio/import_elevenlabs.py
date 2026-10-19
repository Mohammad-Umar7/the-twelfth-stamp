"""Master the ElevenLabs horror set into the game.

Reads Tools/audio/elevenlabs_horror.json (prompt, settings and loudness target of every id) and one raw MP3 per id
as the ElevenLabs connector delivered it (<id>.mp3 in a download folder), and masters them like the synthesized set:

  ambience  48 kHz stereo, 20 Hz high-pass, correlation-aware 1 s crossfade into a seamless loop that starts on a
            zero crossing, integrated loudness to target_lufs with true peak <= -3 dBTP (limited circularly),
            OGG Vorbis q5 -> Game/Assets/Resources/Audio/Ambience/<id>.ogg (replaces the synthesized bed)
  scare     48 kHz mono, 30 Hz high-pass, leading/trailing silence trimmed, 3 ms / 60 ms fades, peak short-window
            loudness (dsp.loud_max) to lmax with true peak <= -1.5 dBTP (a lookahead limiter may shave up to 6 dB
            off transients so hits reach their loudness), PCM16 WAV
            -> Game/Assets/Resources/Audio/SFX/<id>.wav

Then it writes their entries into Game/Assets/Resources/Audio/audio_manifest.json and the measured table into
Audio/ElevenLabsHorror.md.

  python Tools/audio/import_elevenlabs.py <download folder>
"""
import json
import os
import subprocess
import sys

import numpy as np

import dsp

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
RES_AUDIO = os.path.join(ROOT, "Game", "Assets", "Resources", "Audio")
MANIFEST = os.path.join(RES_AUDIO, "audio_manifest.json")
SPEC = os.path.join(HERE, "elevenlabs_horror.json")
REPORT = os.path.join(ROOT, "Audio", "ElevenLabsHorror.md")
WORK = os.path.join(ROOT, "Audio", "_work", "elevenlabs", "master")

SOURCE = "elevenlabs:eleven_text_to_sound_v2"
LICENSE = ("Generated with ElevenLabs Sound Effects (eleven_text_to_sound_v2) on 2026-10-18 for The Twelfth Stamp; "
           "use is governed by the ElevenLabs terms of the generating account's plan. Disclose as AI-generated.")


def decode(path, ch):
    cmd = ["ffmpeg", "-hide_banner", "-loglevel", "error", "-i", path, "-f", "f32le", "-acodec", "pcm_f32le",
           "-ac", str(ch), "-ar", str(dsp.SR), "-"]
    x = np.frombuffer(subprocess.run(cmd, check=True, capture_output=True).stdout, dtype=np.float32).astype(np.float64)
    return x.reshape(-1, ch).T.copy() if ch > 1 else x


def master_ambience(cid, spec, src):
    x = dsp.hp(decode(src, 2), 20.0)
    raw_seam = dsp.seam_report(x)
    F = dsp.N(1.0)
    y = dsp.make_loop(x, x.shape[-1] - F, F)
    y = dsp.norm_lufs(y, spec["target_lufs"], tp_max=-3.0, circular=True)
    wav = os.path.join(WORK, cid + ".wav")
    dsp.write_wav(wav, y)
    dsp.encode_ogg(wav, os.path.join(RES_AUDIO, "Ambience", cid + ".ogg"), 5)
    return {"y": y, "lufs": dsp.lufs(y), "tp": dsp.true_peak_db(y), "seam": dsp.seam_report(y), "raw_seam": raw_seam}


def trim(x, start_db=-50.0, end_db=-60.0):
    a = np.abs(x)
    pk = a.max() + 1e-12
    on = np.where(a > pk * dsp.undb(start_db))[0]
    off = np.where(a > pk * dsp.undb(end_db))[0]
    s = max(0, int(on[0]) - dsp.N(0.01)) if len(on) else 0
    e = min(len(x), int(off[-1]) + dsp.N(0.05)) if len(off) else len(x)
    return x[s:e]


def master_scare(cid, spec, src):
    x = trim(dsp.hp(decode(src, 1), 30.0))
    x = dsp.fade(x, fin=0.003, fout=0.06)
    y, gain = dsp.norm_loud(x, spec["lmax"], tp_max=-1.5, max_gr=6.0)
    dsp.write_wav(os.path.join(RES_AUDIO, "SFX", cid + ".wav"), y)
    return {"y": y, "lufs": dsp.lufs(y), "lmax": dsp.loud_max(y), "tp": dsp.true_peak_db(y), "gain": gain}


def entry(cid, folder, bus, y, loop, lufs_val):
    return {"id": cid, "file": f"Audio/{folder}/{cid}", "bus": bus, "duration": round(y.shape[-1] / dsp.SR, 3),
            "loop": loop, "source": SOURCE, "license": LICENSE,
            "lufs": None if lufs_val is None or not np.isfinite(lufs_val) else round(lufs_val, 1)}


def fmt(v, nd=1):
    return "-" if v is None or not np.isfinite(v) else f"{v:.{nd}f}"


def main():
    raw_dir = sys.argv[1]
    spec = json.load(open(SPEC, encoding="utf-8"))
    os.makedirs(WORK, exist_ok=True)
    manifest = json.load(open(MANIFEST, encoding="utf-8"))
    index = {c["id"]: i for i, c in enumerate(manifest["clips"])}
    rows_amb, rows_sc, missing = [], [], []

    def put(e):
        if e["id"] in index:
            manifest["clips"][index[e["id"]]] = e
        else:
            index[e["id"]] = len(manifest["clips"])
            manifest["clips"].append(e)

    for cid, s in spec["ambience"].items():
        src = os.path.join(raw_dir, cid + ".mp3")
        if not os.path.exists(src):
            missing.append(cid)
            continue
        r = master_ambience(cid, s, src)
        put(entry(cid, "Ambience", "Ambience", r["y"], True, r["lufs"]))
        rows_amb.append((cid, s, r))
        print(f"{cid}: {r['y'].shape[-1] / dsp.SR:.2f} s loop, {fmt(r['lufs'])} LUFS, TP {fmt(r['tp'])} dBTP, "
              f"seam {r['seam']['jump_over_p999']} (raw {r['raw_seam']['jump_over_p999']})")

    for cid, s in spec["scares"].items():
        src = os.path.join(raw_dir, cid + ".mp3")
        if not os.path.exists(src):
            missing.append(cid)
            continue
        r = master_scare(cid, s, src)
        put(entry(cid, "SFX", "SFX", r["y"], False, r["lufs"]))
        rows_sc.append((cid, s, r))
        print(f"{cid}: {r['y'].shape[-1] / dsp.SR:.2f} s, Lmax {fmt(r['lmax'])}, TP {fmt(r['tp'])} dBTP, "
              f"gain {r['gain']:+.1f} dB")

    with open(MANIFEST, "w", encoding="utf-8", newline="\n") as fh:
        json.dump(manifest, fh, indent=2)
        fh.write("\n")

    w = []
    w.append("# The Twelfth Stamp - ElevenLabs horror set\n")
    w.append(f"Generated {spec['generated']} with ElevenLabs Sound Effects (`{spec['model']}`) in {spec['flow']}, one take "
             "per prompt, then mastered by `Tools/audio/import_elevenlabs.py` (spec: `Tools/audio/elevenlabs_horror.json`). "
             "The ambience beds replace the synthesized beds of the same id; the scares are new one-shots that "
             "`Scripts/Audio/AmbientScares.cs` drops around the player.\n")
    w.append("> The numbers below were measured from the mastered files. The set was listened to and approved on "
             "18 Oct 2026 (soundboard preview).\n")
    w.append("## Ambience (OGG Vorbis q5, 48 kHz stereo, seamless loop, true peak <= -3 dBTP)\n")
    w.append("| id | loop (s) | LUFS | TP (dBTP) | seam step / p99.9 | prompt |")
    w.append("|---|---|---|---|---|---|")
    for cid, s, r in rows_amb:
        w.append(f"| {cid} | {r['y'].shape[-1] / dsp.SR:.2f} | {fmt(r['lufs'])} | {fmt(r['tp'])} | "
                 f"{r['seam']['jump_over_p999']} | {s['prompt']} |")
    w.append("\n## Scares (WAV 48 kHz mono 16-bit, peak short-window loudness, true peak <= -1.5 dBTP)\n")
    w.append("| id | dur (s) | Lmax target | Lmax | TP (dBTP) | prompt |")
    w.append("|---|---|---|---|---|---|")
    for cid, s, r in rows_sc:
        w.append(f"| {cid} | {r['y'].shape[-1] / dsp.SR:.2f} | {s['lmax']:.1f} | {fmt(r['lmax'])} | {fmt(r['tp'])} | "
                 f"{s['prompt']} |")
    if missing:
        w.append("\nMissing raw files: " + ", ".join(missing))
    with open(REPORT, "w", encoding="utf-8", newline="\n") as fh:
        fh.write("\n".join(w) + "\n")

    print(f"{len(rows_amb)} ambience, {len(rows_sc)} scares; missing: {', '.join(missing) or 'none'}")
    sys.exit(1 if missing else 0)


if __name__ == "__main__":
    main()
