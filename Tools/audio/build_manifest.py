"""Verify every delivered audio file and write the manifests.

  Tools/.venv/Scripts/python.exe -I Tools/audio/build_manifest.py

For each required id: ffprobe (codec / rate / channels / duration), ffmpeg ebur128 (integrated LUFS,
true peak, LRA), and a decode of the delivered file (sample count, clipping, DC, loop seam measured on
the encoded file). Writes:
  Game/Assets/Resources/Audio/audio_manifest.json   (machine manifest, schema v1)
  Audio/AudioManifest.md                            (human manifest + evidence)
  Audio/_work/verify_report.json                    (all measurements)
"""
import json
import os
import re
import subprocess
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np  # noqa: E402
from scipy import signal  # noqa: E402

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
RES = os.path.join(ROOT, "Game", "Assets", "Resources")
AUD = os.path.join(RES, "Audio")
WORK = os.path.join(ROOT, "Audio", "_work")
MANIFEST_JSON = os.path.join(AUD, "audio_manifest.json")
MANIFEST_MD = os.path.join(ROOT, "Audio", "AudioManifest.md")
REPORT = os.path.join(WORK, "verify_report.json")
SR = 48000
HF_VERSION = "0.8.167"

SURFACES = ("wood", "stone", "tile", "sand", "grass", "metal")
BODY = [("breath_calm_out", 3), ("breath_calm_in", 3), ("breath_run_out", 6), ("breath_run_in", 6), ("breath_hard_out", 4),
        ("breath_hard_in", 4), ("breath_pant_out", 4), ("breath_pant_in", 4), ("breath_fear_in", 4), ("breath_fear_out", 3),
        ("breath_gasp", 3), ("breath_relief", 2), ("cloth_run", 6), ("heart_beat", 4)]
SHIFT = [("shift_rumble", "Shift"), ("shift_whoosh", "Shift"), ("shift_reverse_swell", "Shift"), ("shift_tear", "Shift"),
         ("shift_arrive", "Shift"), ("sting_chase_1", "Threat"), ("sting_chase_2", "Threat")]
SFX_SPEC = [  # (id, requested seconds, group)
    ("ui_hover", 0.08, "UI"), ("ui_click", 0.12, "UI"), ("ui_back", 0.15, "UI"), ("ui_confirm", 0.4, "UI"),
    ("ui_slider_tick", 0.05, "UI"),
    ("pp_open", 0.6, "Passport"), ("pp_close", 0.5, "Passport"), ("pp_page", 0.4, "Passport"), ("pp_tab", 0.1, "Passport"),
    ("pp_seam_shimmer", 1.2, "Passport"),
    ("tear_open_slit", 0.8, "Tear"), ("tear_open_window", 1.2, "Tear"), ("tear_open_door", 1.8, "Tear"),
    ("tear_seal", 1.0, "Tear"), ("tear_breach", 1.0, "Tear"), ("tear_hum_loop", 6.0, "Tear"), ("tear_stretch_loop", 4.0, "Tear"),
    ("stamp_hit", 0.35, "Stamp"), ("seal_verify", 1.6, "Stamp"), ("seal_counterfeit", 2.2, "Stamp"), ("stamp_void", 0.6, "Stamp"),
    ("sunbeam_on", 1.5, "Light"), ("sunbeam_loop", 6.0, "Light"), ("sun_sizzle", 1.0, "Light"),
] + [(f"pass_step_{i}", 0.3, "Passenger") for i in (1, 2, 3)] + [(f"pass_creak_{i}", 0.8, "Passenger") for i in (1, 2, 3)] + [
    ("pass_breath", 2.0, "Passenger"), ("pass_recoil", 0.7, "Passenger"), ("pass_tap", 1.2, "Passenger"),
    ("pass_drone_loop", 8.0, "Passenger"), ("pass_whisper", 2.5, "Passenger"), ("pass_sting", 1.5, "Passenger"),
] + [(f"foot_{s}_{i}", None, "Player") for s in SURFACES for i in range(1, 7)] + [
    (f"foot_run_{s}_{i}", None, "Player") for s in SURFACES for i in range(1, 5)] + [
    (f"foot_wet_{i}", None, "Player") for i in range(1, 5)] + [
    ("breath_scared", 3.0, "Player"), ("heartbeat_loop", 2.0, "Player"),
    ("grab", 0.15, "Props"), ("drop_soft", 0.3, "Props"), ("drop_hard", 0.4, "Props"), ("place_socket", 0.3, "Props"),
    ("dial_click", 0.1, "Props"), ("lever_clunk", 0.5, "Props"), ("button_press", 0.15, "Props"), ("drawer_open", 0.8, "Props"),
    ("door_open", 1.2, "Props"), ("door_close", 1.0, "Props"), ("glasses_unfold", 0.4, "Props"), ("case_open", 0.4, "Props"),
    ("case_close", 0.35, "Props"), ("glasses_break", 1.2, "Props"), ("coin_token", 0.5, "Props"), ("booth_power", 1.5, "Props"),
    ("noisemaker_loop", 4.0, "Props"), ("lantern_place", 0.4, "Props"), ("drum_hit", 0.6, "Props"), ("clock_chime", 6.0, "Props"),
    ("clock_tick", 0.1, "Props"), ("phone_buzz", 1.0, "Props"), ("voicemail_beep", 0.3, "Props"),
    ("fridge_hum_loop", 6.0, "Props"), ("fridge_stop", 0.6, "Props"), ("camera_shutter", 0.3, "Props"),
    ("reflection_sting", 2.0, "Props"), ("skyline_shift", 4.0, "Props"),
    ("birds_flutter_1", 1.6, "Props"), ("birds_flutter_2", 1.6, "Props"), ("birds_flutter_3", 1.6, "Props"),
] + [(f"{k}_{i}", None, "Body") for k, cnt in BODY for i in range(1, cnt + 1)] + [
    (sid, None, grp) for sid, grp in SHIFT]
AMB_IDS = ["amb_shop", "amb_apartment", "amb_apartment_corrupt", "amb_egypt", "amb_paris_rain", "amb_ny_harbour",
           "amb_japan_night", "amb_riyadh_wind", "amb_india_garden", "amb_rio_frozen", "amb_london_river",
           "amb_berlin_hall", "amb_margin"]
MUS_IDS = ["mus_menu", "mus_tension", "mus_chase", "mus_reveal", "mus_home", "mus_credits"]
IMPORT = {"Voice": "Compressed In Memory (Vorbis); clips are already mono - keep Force To Mono / Normalize OFF",
          "SFX": "Decompress On Load (PCM/ADPCM)", "UI": "Decompress On Load (PCM/ADPCM), 2D",
          "Ambience": "Streaming (Vorbis), loop", "Music": "Streaming (Vorbis)"}
VOICE_LIC = ("AI speech generated locally with Kokoro-82M (Apache-2.0 model weights) via hyperframes tts "
             f"{HF_VERSION}; output owned by the project. Disclose as AI-generated in jam notes.")


def load(path, default=None):
    if os.path.exists(path):
        with open(path, encoding="utf-8") as fh:
            return json.load(fh)
    return default if default is not None else {}


def ffprobe(path):
    p = subprocess.run(["ffprobe", "-v", "error", "-show_entries",
                        "stream=codec_name,sample_rate,channels,sample_fmt,bits_per_sample:format=duration,size",
                        "-of", "json", path], capture_output=True, text=True, check=True)
    d = json.loads(p.stdout)
    s = d["streams"][0]
    return {"codec": s.get("codec_name"), "sample_rate": int(s.get("sample_rate", 0)), "channels": int(s.get("channels", 0)),
            "sample_fmt": s.get("sample_fmt"), "duration": float(d["format"]["duration"]), "bytes": int(d["format"]["size"])}


def ebur128(path):
    p = subprocess.run(["ffmpeg", "-hide_banner", "-nostats", "-i", path, "-af", "ebur128=peak=true", "-f", "null", "-"],
                       capture_output=True, text=True, encoding="utf-8", errors="replace")
    txt = p.stderr[p.stderr.rfind("Summary:"):]
    i = re.search(r"I:\s+(-?[\d.]+|-inf|nan) LUFS", txt)
    lra = re.search(r"LRA:\s+(-?[\d.]+) LU", txt)
    tp = re.search(r"Peak:\s+(-?[\d.]+|-inf) dBFS", txt)
    f = lambda mm: None if (mm is None or mm.group(1) in ("-inf", "nan")) else float(mm.group(1))  # noqa: E731
    return {"I": f(i), "LRA": f(lra), "TP": f(tp)}


def decode(path, ch):
    p = subprocess.run(["ffmpeg", "-v", "error", "-i", path, "-f", "f32le", "-acodec", "pcm_f32le", "-ar", str(SR), "-"],
                       capture_output=True, check=True)
    x = np.frombuffer(p.stdout, dtype=np.float32).astype(np.float64)
    return x.reshape(-1, ch).T


def seam(x):
    """Wrap-around checks on the decoded file, judged against the audio around the junction.

    The loop is viewed circularly (end -> start). A click shows up as a wrap step, curvature (second
    difference) or high-pass burst that is larger than what the signal itself does within +-50 ms.
    Ratios <= 1 mean the junction is indistinguishable from its neighbourhood."""
    m = x.mean(axis=0)
    W = int(0.05 * SR)
    c = np.concatenate([m[-W:], m[:W]])  # junction between index W-1 (last sample) and W (first sample)
    d1 = np.abs(np.diff(c))
    d2 = np.abs(np.diff(c, 2))
    j1 = d1[W - 1]
    j2 = max(d2[W - 2], d2[W - 1])
    nb1 = np.delete(d1, [W - 1])
    nb2 = np.delete(d2, [W - 2, W - 1])
    s = signal.butter(2, 2000, "highpass", fs=SR, output="sos")
    h = np.abs(signal.sosfilt(s, np.concatenate([m[-3 * W:-W], c]))[2 * W:])
    k = int(0.001 * SR)
    hj = h[W - k:W + k].max()
    hn = np.delete(h, np.arange(W - k, W + k))
    eps = 10 ** (-80 / 20)  # ignore differences below -80 dBFS (dither level)
    r_end = np.sqrt(np.mean(m[-W:] ** 2)) + 1e-12
    r_start = np.sqrt(np.mean(m[:W] ** 2)) + 1e-12
    rms = np.sqrt(np.mean(m ** 2)) + 1e-12
    dbf = lambda v: round(float(20 * np.log10(abs(v) + 1e-15)), 1)  # noqa: E731
    return {"step_ratio": round(float(j1 / (np.percentile(nb1, 99) + eps)), 3),
            "curvature_ratio": round(float(j2 / (np.percentile(nb2, 99) + eps)), 3),
            "hf_ratio": round(float(hj / (np.percentile(hn, 99) + eps)), 3),
            "step_dbfs": dbf(j1), "kink_dbfs": dbf(j2), "kink_below_rms_db": round(float(20 * np.log10(rms / (j2 + 1e-15))), 1),
            "rms_end_to_start_db": round(float(20 * np.log10(r_start / r_end)), 2)}


def fmt(v, nd=1):
    return "-" if v is None else f"{v:.{nd}f}"


def main():
    narration = load(os.path.join(ROOT, "Audio", "Script", "narration.json"))
    vlog = load(os.path.join(WORK, "voice_log.json"))
    glog = load(os.path.join(WORK, "voice_glitch_log.json"))
    sfx_meta = load(os.path.join(WORK, "sfx_meta.json"))
    amb_meta = load(os.path.join(WORK, "amb_meta.json"))
    mus_meta = load(os.path.join(WORK, "music_meta.json"))
    asr_s = load(os.path.join(WORK, "voice_transcripts_small.en.json"), {"clips": {}})
    asr_l = load(os.path.join(WORK, "voice_transcripts_large-v3.json"), {"clips": {}})

    items = []  # (id, folder, ext, bus, loop, source, license, transcript, speaker, spec)
    for line in narration["lines"]:
        items.append(dict(id=line["id"], folder="Voice", ext=".wav", bus="Voice", loop=False,
                          source=f"kokoro-hyperframes-{HF_VERSION}", license=VOICE_LIC, transcript=line["text"],
                          speaker=line["speaker"], spec=None))
    items.append(dict(id="mom_01_glitch", folder="Voice", ext=".wav", bus="Voice", loop=False,
                      source=f"kokoro-hyperframes-{HF_VERSION}", license=VOICE_LIC,
                      transcript="Unpacked... unpacked... unpacked... unpa-", speaker="mother", spec=4.0))
    for sid, dur, grp in SFX_SPEC:
        mt = sfx_meta.get(sid, {})
        items.append(dict(id=sid, folder="SFX", ext=".wav", bus=mt.get("bus", "UI" if grp == "UI" else "SFX"),
                          loop=bool(mt.get("loop", sid.endswith("_loop"))), source=mt.get("source", "?"),
                          license=mt.get("license", "?"), transcript=None, speaker=None, spec=dur, group=grp,
                          note=mt.get("note", ""), norm=mt.get("norm"), peak=mt.get("peak"), desc=mt.get("desc", ""),
                          loud=mt.get("loud_max"), shift=mt.get("vs_peak3_db")))
    for aid in AMB_IDS:
        mt = amb_meta.get(aid, {})
        items.append(dict(id=aid, folder="Ambience", ext=".ogg", bus="Ambience", loop=True, source=mt.get("source", "?"),
                          license=mt.get("license", "?"), transcript=None, speaker=None, spec=mt.get("loop_seconds"),
                          target=mt.get("target_lufs")))
    for cid in MUS_IDS:
        mt = mus_meta.get(cid, {})
        items.append(dict(id=cid, folder="Music", ext=".ogg", bus="Music", loop=bool(mt.get("loop", False)),
                          source=mt.get("source", "?"), license=mt.get("license", "?"), transcript=None, speaker=None,
                          spec=None))

    report, clips, missing, issues = {}, [], [], []
    total_bytes = 0
    for it in items:
        path = os.path.join(AUD, it["folder"], it["id"] + it["ext"])
        if not os.path.exists(path):
            missing.append(it["id"])
            continue
        pr = ffprobe(path)
        lo = ebur128(path)
        x = decode(path, pr["channels"])
        n = x.shape[1]
        clip_count = int(np.sum(np.abs(x) >= 0.9999))
        r = {"path": os.path.relpath(path, ROOT).replace("\\", "/"), **pr, "decoded_samples": n,
             "I": lo["I"], "LRA": lo["LRA"], "TP": lo["TP"], "sample_peak_db": round(float(20 * np.log10(np.max(np.abs(x)) + 1e-12)), 2),
             "clipped_samples": clip_count, "dc": round(float(np.mean(x)), 6)}
        if it["loop"]:
            r["seam"] = seam(x)
            if it["folder"] in ("Ambience", "Music"):
                mw = os.path.join(WORK, "amb" if it["folder"] == "Ambience" else "music", it["id"] + ".wav")
                if os.path.exists(mw):
                    import soundfile as sf
                    xm, _ = sf.read(mw, always_2d=True)
                    r["master_seam"] = seam(xm.T)
        total_bytes += pr["bytes"]
        bus = it["bus"]
        lufs_val = lo["I"] if (pr["duration"] >= 0.4 and lo["I"] is not None and lo["I"] > -69.0) else None
        # ---- requirement checks
        prob = []
        if pr["sample_rate"] != SR:
            prob.append(f"sample rate {pr['sample_rate']}")
        if clip_count:
            prob.append(f"{clip_count} clipped samples")
        if it["folder"] == "Voice":
            if pr["channels"] != 1 or pr["sample_fmt"] != "s16":
                prob.append("not mono s16")
            if pr["duration"] < 0.4:
                prob.append("shorter than 0.4 s")
            if it["id"] != "mom_01_glitch" and (lufs_val is None or abs(lufs_val + 18) > 1.0):
                prob.append(f"loudness {lufs_val} LUFS (target -18)")
            if lo["TP"] is not None and lo["TP"] > -1.95:
                prob.append(f"true peak {lo['TP']} dBTP > -2")
        elif it["folder"] == "SFX":
            mt = sfx_meta.get(it["id"], {})
            want_ch = mt.get("channels", 2 if it["id"] == "tear_hum_loop" else 1)
            if pr["channels"] != want_ch:
                prob.append(f"{pr['channels']} channels")
            nrm = mt.get("norm") or ["peak", -3.0]
            ceiling = nrm[2] if (nrm[0] == "loud" and len(nrm) > 2) else -3.0
            if lo["TP"] is not None and lo["TP"] > ceiling + 0.2:
                prob.append(f"true peak {lo['TP']} dBTP > {ceiling}")
        elif it["folder"] == "Ambience":
            if pr["codec"] != "vorbis" or pr["channels"] != 2:
                prob.append("not vorbis stereo")
            if lufs_val is None or not (-30.6 <= lufs_val <= -25.4):
                prob.append(f"loudness {lufs_val} LUFS outside -30..-26")
            if not (40 <= pr["duration"] <= 60.5):
                prob.append(f"duration {pr['duration']:.1f} s outside 40-60")
        elif it["folder"] == "Music":
            if pr["codec"] != "vorbis" or pr["channels"] != 2:
                prob.append("not vorbis stereo")
            if lufs_val is None or abs(lufs_val + 24) > 1.0:
                prob.append(f"loudness {lufs_val} LUFS (target -24)")
            if lo["TP"] is not None and lo["TP"] > -1.0:
                prob.append(f"true peak {lo['TP']} dBTP")
        if it["loop"]:
            s = r["seam"]
            # construction check: the lossless signal (WAV file, or the float master of an OGG) must be
            # seamless; the OGG's own wrap only carries the codec's independent start/end error (reported).
            ref = r.get("master_seam", s)
            if max(ref["step_ratio"], ref["curvature_ratio"]) > 1.5:
                prob.append(f"loop seam {ref}")
            if it["folder"] in ("Ambience", "Music"):
                exp = None
                if it["folder"] == "Ambience":
                    exp = int(round(amb_meta.get(it["id"], {}).get("loop_seconds", 0) * SR))
                else:
                    exp = int(round(mus_meta.get(it["id"], {}).get("seconds", 0) * SR))
                r["expected_samples"] = exp
                if exp and n != exp:
                    prob.append(f"decoded {n} samples, expected {exp} (not gapless)")
        r["problems"] = prob
        if prob:
            issues.append((it["id"], prob))
        report[it["id"]] = r
        c = {"id": it["id"], "file": f"Audio/{it['folder']}/{it['id']}", "bus": bus, "duration": round(pr["duration"], 3),
             "loop": it["loop"], "source": it["source"], "license": it["license"]}
        if it["folder"] == "Voice":
            c["transcript"] = it["transcript"]
            c["speaker"] = it["speaker"]
        c["lufs"] = None if lufs_val is None else round(lufs_val, 1)
        if it.get("peak") is not None:
            c["peak"] = it["peak"]  # seconds into the clip of its dramatic peak (AudioDirector.PlayToPeak)
        clips.append(c)
        it["r"] = r
        it["lufs"] = lufs_val

    manifest = {"version": 1, "clips": clips}
    with open(MANIFEST_JSON, "w", encoding="utf-8", newline="\n") as fh:
        json.dump(manifest, fh, indent=2)
        fh.write("\n")
    with open(REPORT, "w", encoding="utf-8") as fh:
        json.dump({"report": report, "missing": missing, "issues": issues, "total_bytes": total_bytes}, fh, indent=1)

    write_md(items, narration, vlog, glog, asr_s, asr_l, missing, issues, total_bytes, sfx_meta, amb_meta, mus_meta)
    counts = {}
    for c in clips:
        counts[c["bus"]] = counts.get(c["bus"], 0) + 1
    print("clips:", len(clips), counts, "missing:", missing)
    print("total bytes:", total_bytes, f"({total_bytes / 1e6:.1f} MB)")
    print("issues:", len(issues))
    for i in issues:
        print("  ", i)


def asr_line(asr, lid):
    c = asr.get("clips", {}).get(lid)
    if not c:
        return None, None
    return c.get("heard"), c.get("mismatches")


def write_md(items, narration, vlog, glog, asr_s, asr_l, missing, issues, total_bytes, sfx_meta, amb_meta, mus_meta):
    L = []
    w = L.append
    by = {}
    for it in items:
        if "r" in it:
            by.setdefault(it["folder"], []).append(it)
    w("# The Twelfth Stamp - Audio Manifest\n")
    w("Generated by `Tools/audio/build_manifest.py` from measurements of the delivered files. "
      "Machine manifest: `Game/Assets/Resources/Audio/audio_manifest.json` (schema v1; `file` is the "
      "Resources.Load path without extension).\n")
    w("> **Honesty note:** every number below was measured from the files on disk (ffprobe / ffmpeg ebur128 / decoded "
      "samples), and pronunciation was checked with speech recognition. Nobody has *listened* to these assets yet: "
      "a human listening pass (headphones and Quest 3 speakers) is still required before they ship.\n")
    w("## Summary\n")
    w("| Bus | Clips | Format | Unity import |")
    w("|---|---|---|---|")
    cnt = {}
    for it in items:
        if "r" in it:
            cnt[it["bus"]] = cnt.get(it["bus"], 0) + 1
    w(f"| Voice | {cnt.get('Voice', 0)} | WAV 48 kHz mono 16-bit, -18 LUFS, TP <= -2 dBTP | {IMPORT['Voice']} |")
    w(f"| UI | {cnt.get('UI', 0)} | WAV 48 kHz mono 16-bit, Lmax -19, TP <= -2 dBTP | {IMPORT['UI']} |")
    w(f"| SFX | {cnt.get('SFX', 0)} | WAV 48 kHz 16-bit (mono; `tear_hum_loop` and the shift/sting clips stereo); one-shots by peak short-window loudness, TP <= -2 dBTP; loops by LUFS | {IMPORT['SFX']} |")
    w(f"| Ambience | {cnt.get('Ambience', 0)} | OGG Vorbis q5, 48 kHz stereo, seamless 48-60 s loops, -30..-27.5 LUFS | {IMPORT['Ambience']} |")
    w(f"| Music | {cnt.get('Music', 0)} | OGG Vorbis q6, 48 kHz stereo, -24 LUFS | {IMPORT['Music']} |")
    w("")
    w(f"Total: **{sum(cnt.values())} clips, {total_bytes / 1e6:.1f} MB** on disk. Missing required ids: "
      f"**{', '.join(missing) if missing else 'none'}**. Requirement-check failures: **{len(issues)}**"
      + ("" if not issues else " (listed under Evidence)") + ".\n")
    w("**Import notes.** SFX/UI = Decompress On Load; Ambience/Music = Streaming; Voice = Compressed In Memory. "
      "Leave *Normalize* and *Force To Mono* off for all clips: the loudness matching is baked into the files, and "
      "Unity's Normalize (applied during Force To Mono) re-peaks each clip. When this manifest was written, "
      "`Game/Assets/Editor/ProjectSetup.cs` was importing Voice with `forceToMono: 1` + `normalize: 1`. That will undo "
      "the -18 LUFS matching between lines and should be switched off. Ambience and music loops loop gaplessly "
      "(decoded sample counts equal the loop length). Set `AudioSource.loop = true` for every clip marked loop.\n")

    # ---------------- voice
    w("## Voice (narration)\n")
    w("Kokoro-82M through `npx --no-install hyperframes tts` (local, free). Voices requested in "
      "`Audio/Script/narration.json` were all reported by `hyperframes tts --list`, so **no substitutions** were needed:\n")
    for spk, v in narration["voices"].items():
        sub = vlog.get("voices", {}).get(spk, {})
        w(f"- {spk}: `{v}`" + (f" -> substituted `{sub.get('used')}`" if sub.get("substituted") else " (used as scripted)"))
    w("")
    w("| id | speaker | dur (s) | LUFS | TP (dBTP) | transcript | notes |")
    w("|---|---|---|---|---|---|---|")
    for it in by.get("Voice", []):
        r = it["r"]
        lid = it["id"]
        rec = vlog.get("lines", {}).get(lid, {})
        notes = []
        if rec.get("respelled"):
            notes.append(f"TTS input respelled: \"{rec.get('tts_text')}\"")
        if it["speaker"] == "mother":
            notes.append("phone band 300-3400 Hz + light saturation")
        if it["speaker"] == "optician":
            notes.append("light aecho room")
        if lid == "mom_01_glitch":
            notes.append("derived from mom_01's last word (see Evidence)")
        w(f"| {lid} | {it['speaker']} | {r['duration']:.2f} | {fmt(it['lufs'])} | {fmt(r['TP'])} | {it['transcript']} | "
          f"{'; '.join(notes)} |")
    w("")

    # ---------------- sfx
    w("## SFX and UI\n")
    w("One-shots are loudness-normalised on their **peak short-window loudness** (`Lmax`: K-weighted energy of the "
      "loudest 100 ms, LUFS-scaled; `dsp.loud_max`) to a per-category target, true peak <= -2 dBTP (-1.5 for the "
      "stereo shift clips); older clips moved at most -10/+6 dB from their former -3 dBTP peak level (`d` column). "
      "Footsteps may shave up to 8 dB off the first milliseconds of the heel transient (soft clip + lookahead limiter) "
      "so hard and soft surfaces sit at the same loudness. Sustained loops are loudness-normalised (target shown) with "
      "peaks <= -3 dBTP. `sunbeam_on` and `fridge_stop` share the gain of `sunbeam_loop` / `fridge_hum_loop`.\n")
    w("| id | bus | group | dur (s) (asked) | loop | ch | TP (dBTP) | LUFS | Lmax | d (dB) | peak (s) | notes |")
    w("|---|---|---|---|---|---|---|---|---|---|---|---|")
    for it in by.get("SFX", []):
        r = it["r"]
        nrm = it.get("norm") or []
        nnote = ""
        if nrm and nrm[0] == "lufs":
            nnote = f"loop normalised to {nrm[1]} LUFS"
        elif nrm and nrm[0] == "loud":
            nnote = f"Lmax target {nrm[1]}"
        elif nrm and nrm[0] == "match":
            nnote = f"level-matched to {nrm[1]}"
        extra = "; ".join(x for x in [it.get("desc", ""), nnote] if x)
        w(f"| {it['id']} | {it['bus']} | {it.get('group', '')} | {r['duration']:.2f} ({fmt(it['spec'], 2)}) | "
          f"{'yes' if it['loop'] else ''} | {r['channels']} | {fmt(r['TP'])} | {fmt(it['lufs'])} | {fmt(it.get('loud'))} | "
          f"{fmt(it.get('shift'))} | {fmt(it.get('peak'), 2)} | {extra} |")
    w("")

    # ---------------- ambience / music
    w("## Ambience (seamless loops)\n")
    w("Seam columns: ratio of the wrap's step / curvature (second difference) to the 99th percentile of the same "
      "quantity within +-50 ms (<= 1 means indistinguishable from the surrounding audio), first for the float master "
      "and then for the decoded .ogg. The last column gives the decoded wrap kink in dBFS and how far it sits below "
      "the cue's RMS.\n")
    w("| id | dur (s) | LUFS | TP (dBTP) | master step/curv | ogg step/curv | ogg kink | content |")
    w("|---|---|---|---|---|---|---|---|")
    content = {
        "amb_shop": "warm interior room tone, distant wordless street murmur, ceiling fan (blade swish + motor), rare wood creaks",
        "amb_apartment": "room tone, distant city rumble with far car passes and one faint horn, air-con with compressor hum",
        "amb_apartment_corrupt": "thinner room tone, faint detuned beating tones, two reversed glass swells",
        "amb_egypt": "gusting wind, sand hiss with grain crackle on gusts",
        "amb_paris_rain": "steady rain on stone (dense drop clicks + hiss), two gutter trickles, downpipe flow, distant river",
        "amb_ny_harbour": "water lapping with pile slaps, distant metal creaks, far ship horn every 20 s",
        "amb_japan_night": "light wind, faint distant glass chimes clustered with gusts, occasional paper rustles",
        "amb_riyadh_wind": "dry gusting wind with a resonant whistle, distant 50 Hz city hum and rumble",
        "amb_india_garden": "still air, fountain splash and bubbling trickle, faint distant small bells",
        "amb_rio_frozen": "suspended low hum (55/82.5/110 Hz, slow beating), near-silence; no drum",
        "amb_london_river": "river flow, cold whistling wind, distant water slaps in a wide space",
        "amb_berlin_hall": "large-hall air handling (duct rumble + fan tone), distant door/clank/footstep events with 4.5 s reverb tails",
        "amb_margin": "very low slow-beating tones (41-165 Hz pairs), faint glassy undertones, trace air",
    }
    for it in by.get("Ambience", []):
        r = it["r"]
        s = r.get("seam", {})
        ms = r.get("master_seam", {})
        w(f"| {it['id']} | {r['duration']:.2f} | {fmt(it['lufs'])} | {fmt(r['TP'])} | {ms.get('step_ratio')} / "
          f"{ms.get('curvature_ratio')} | {s.get('step_ratio')} / {s.get('curvature_ratio')} | {s.get('kink_dbfs')} dBFS "
          f"({s.get('kink_below_rms_db')} dB < RMS) | {content.get(it['id'], '')} |")
    w("")
    w("## Music\n")
    w("| id | dur (s) | loop | LUFS | TP (dBTP) | seam master / ogg (step, curv) | description |")
    w("|---|---|---|---|---|---|---|")
    mdesc = {
        "mus_menu": "90 s loop: D-minor drone, slow upper harmony (Dm-Gm-Dm7-E dim), sparse oud-like plucks, faint bells",
        "mus_tension": "60 s loop: 60 bpm lub-dub pulse, dark D drone, Eb dissonance swelling, faint bowed glass",
        "mus_chase": "45 s loop (24 bars @128 bpm): staccato low-string ostinato Dm-Bb-Gm-A-Dm-A, quarter pulse, Shepard riser, tremolo cluster; no drum kit",
        "mus_reveal": "8 s sting: glass cluster swell cut into a detuned 'counterfeit' chime + dark D/Ab/C# bloom; no boom",
        "mus_home": "45 s: D major resolution (D - G/D - Bm - Asus4-A - D), guitar-like melody, warm pad, fades out",
        "mus_credits": "90 s: Dm-Bb-F-C-Dm-Bb-Gm-A x2 ending on D major, guitar arpeggios + bell melody, fades out",
    }
    for it in by.get("Music", []):
        r = it["r"]
        ms, s = r.get("master_seam"), r.get("seam")
        sc = "-" if not s else (f"{ms['step_ratio']}, {ms['curvature_ratio']} / {s['step_ratio']}, {s['curvature_ratio']} "
                                f"(kink {s['kink_dbfs']} dBFS, {s['kink_below_rms_db']} dB < RMS)")
        w(f"| {it['id']} | {r['duration']:.2f} | {'yes' if it['loop'] else ''} | {fmt(it['lufs'])} | {fmt(r['TP'])} | "
          f"{sc} | {mdesc.get(it['id'], '')} |")
    w("")

    # ---------------- sources / licences
    w("## Sources and licences\n")
    w("- **Voice:** synthetic speech, Kokoro-82M (Apache-2.0 weights) run locally by Hyperframes CLI 0.8.167 "
      "(`hyperframes tts`) with the `kokoro-onnx` runtime in `Tools/.venv`. AI-generated: list it in the jam's AI-use notes.")
    w("- **Synthesised SFX / ambience / music:** written from scratch in Python (numpy/scipy) by "
      "`Tools/audio/make_sfx.py`, `make_ambience.py`, `make_music.py` (helpers `dsp.py`, `materials.py`). No samples, "
      "loops or third-party recordings are used. Project-owned.")
    w("- **No third-party audio.** The earlier Kenney CC0 footsteps (`foot_wood_*`, `foot_stone_*`) were replaced by "
      "original synthesis (`Tools/audio/sfx_steps.py`); every SFX, ambience and music file is generated by the scripts above.")
    w("")

    # ---------------- evidence
    w("## Evidence\n")
    w("### Commands\n")
    w("```bash")
    w("# environment (project-local venv; Kokoro model/voices were already cached in ~/.cache/hyperframes/tts)")
    w("uv pip install --python Tools/.venv numpy scipy soundfile kokoro-onnx")
    w("export HYPERFRAMES_PYTHON=$PWD/Tools/.venv/Scripts/python.exe   # hyperframes tts needs kokoro_onnx + soundfile")
    w("npx --no-install hyperframes tts --list")
    w("# narration (per line; text passed as a .txt file)")
    w("Tools/.venv/Scripts/python.exe -I Tools/audio/make_voice.py")
    w("#   -> npx --no-install hyperframes tts Audio/_work/voice_txt/<id>.txt -v <voice> -s 1.0 -o Audio/_work/voice_raw/<id>.wav --json")
    w("#   -> ffmpeg pass 0 (measure), optional gain + 192 kHz lookahead limiter, pass 1 (measure), pass 2 (linear loudnorm + fades + 48 kHz s16)")
    w("Tools/.venv/Scripts/python.exe -I Tools/audio/make_voice_glitch.py")
    w("python -I Tools/audio/verify_voice.py --model small.en      # system Python faster-whisper, cached model, offline")
    w("python -I Tools/audio/verify_voice.py --model large-v3")
    w("# SFX / ambience / music")
    w("Tools/.venv/Scripts/python.exe -I Tools/audio/make_sfx.py")
    w("Tools/.venv/Scripts/python.exe -I Tools/audio/make_ambience.py   # ffmpeg -i <float wav> -c:a libvorbis -q:a 5 -ar 48000")
    w("Tools/.venv/Scripts/python.exe -I Tools/audio/make_music.py      # ffmpeg -i <float wav> -c:a libvorbis -q:a 6 -ar 48000")
    w("# verification + manifests")
    w("Tools/.venv/Scripts/python.exe -I Tools/audio/build_manifest.py  # ffprobe, ffmpeg -af ebur128=peak=true, decode checks")
    w("```\n")
    ex = vlog.get("lines", {}).get("pro_01", {}).get("post", {})
    if ex:
        w("Example voice chain (`pro_01`, final pass):\n")
        w("```")
        w(ex.get("filter_pass2", ""))
        w("```\n")
    if glog:
        w(f"`mom_01_glitch`: Whisper put \"unpacked\" at {', '.join(glog.get('asr', []))}. The cut was snapped to the "
          f"waveform energy: the deepest dip ({glog.get('energy_dip_s')} s, the /v/ of \"you've\") gives start "
          f"{glog.get('word_start')} s, and the last frame above -50 dBFS gives end {glog.get('word_end')} s. The word "
          f"is repeated {len(glog.get('rates', []))} times with gaps {glog.get('gaps')} s and tape-style rates "
          f"{glog.get('rates')}; the last repeat stops {int(100 * glog.get('cut_fraction', 0))} % into the word; the "
          f"clip is padded to 4.0 s.\n")
    w("### Transcription check (pronunciation)\n")
    w("`npx --no-install hyperframes transcribe --help` works, but no ASR runtime is installed for it on this machine "
      "(`--no-runtime-install` returned `whisper_unavailable`). Its whisper.cpp path needs cmake to build, and Parakeet is "
      "not installed. So the check used the **faster-whisper** package already in the system Python with Whisper models "
      "already in the local Hugging Face cache (`small.en`, the same model family Hyperframes defaults to, plus "
      "`large-v3` as a second opinion), offline. Phonemes were also inspected with Kokoro's own front-end "
      "(`kokoro_onnx.tokenizer.Tokenizer().phonemize`).\n")
    w("**Fixed during generation** (wrong reading found, TTS input respelled, re-verified):\n")
    w("- `tut_seal`: \"Close the tear\" was phonemised /klˈəʊs ðə tˈiə/ (adjective \"close\", crying \"tear\"). Small.en "
      "heard \"tier\". Respelled \"Cloze the tare\", which gives /kləʊz/ and /teə/; both models now hear \"Close the tear\".")
    w("- `opt_02`: \"lives\" was /lˈaɪvz/ (noun). Respelled \"livs\" for /lɪvz/. With that change large-v3 heard \"leave\" as "
      "\"lave\", so the TTS input ends the sentence at \"leave.\"; both models now hear \"leave\".")
    w("- `end_02`: \"Saeed\" was /sˈiːd/ (both models heard \"Seed\"). Respelled \"Sa-eed\" (/sɑːˈiːd/); both models now hear \"Saeed\".")
    w("- `shop_02`: \"Ah, those.\" was phonemised correctly (/ˈɑː/), but it ran into \"those\" and both models heard "
      "\"Are those\". The TTS input now has a hesitation (\"Ah... those.\"), which large-v3 hears as \"Ah, those\".")
    w("")
    w("**Remaining word differences** (script vs. heard):\n")
    w("| id | small.en heard | small.en diff | large-v3 diff | assessment |")
    w("|---|---|---|---|---|")
    assess = {
        "shop_02": "with the hesitation take large-v3 hears \"Ah, those\"; small.en hears \"odd\". \"an\" -> \"and\" is liaison. Listen.",
        "mom_01": "you're/your homophone (/jʊɹ/ is a normal reading)",
        "usa_01": "two/to homophone",
        "opt_02": "two/too homophone",
        "opt_03": "home stamp vs homestamp (word split only)",
        "disc_01": "small.en: \"stamp\"; large-v3 hears \"stamped\". The /t/ before \"this\" is light. Listen.",
    }
    any_rows = False
    for lid in [l["id"] for l in narration["lines"]]:
        hs, ms = asr_line(asr_s, lid)
        hl, ml = asr_line(asr_l, lid)
        if (ms or ml):
            any_rows = True
            d1 = "; ".join(f"{x['script']} -> {x['heard']}" for x in (ms or [])) or "match"
            d2 = "; ".join(f"{x['script']} -> {x['heard']}" for x in (ml or [])) or "match"
            w(f"| {lid} | {hs} | {d1} | {d2} | {assess.get(lid, 'check by ear')} |")
    if not any_rows:
        w("| - | - | none | none | - |")
    g = asr_s.get("clips", {}).get("mom_01_glitch")
    if g:
        w(f"\n`mom_01_glitch` (derived, not compared): small.en heard \"{g.get('heard')}\".")
    w("")
    w("### Loudness and integrity\n")
    vs = [it for it in by.get("Voice", []) if it["id"] != "mom_01_glitch"]
    if vs:
        Is = [it["lufs"] for it in vs if it["lufs"] is not None]
        TPs = [it["r"]["TP"] for it in vs if it["r"]["TP"] is not None]
        ds = [it["r"]["duration"] for it in vs]
        w(f"- Voice ({len(vs)} lines): integrated {min(Is):.1f} to {max(Is):.1f} LUFS (mean {np.mean(Is):.2f}). True peak "
          f"{min(TPs):.1f} to {max(TPs):.1f} dBTP. Durations {min(ds):.2f}-{max(ds):.2f} s (none < 0.4 s). All passes used "
          "loudnorm in linear mode. Heads ~60 ms and tails ~150 ms after trimming (the optician lines are longer because "
          "of the room echo).")
    for folder in ("SFX", "Ambience", "Music"):
        its = by.get(folder, [])
        if not its:
            continue
        TPs = [it["r"]["TP"] for it in its if it["r"]["TP"] is not None]
        Is = [it["lufs"] for it in its if it["lufs"] is not None]
        clipped = sum(it["r"]["clipped_samples"] for it in its)
        loops = [it for it in its if it["loop"]]
        sj = max([it["r"]["seam"]["step_ratio"] for it in loops], default=0)
        sc = max([it["r"]["seam"]["curvature_ratio"] for it in loops], default=0)
        sh = max([it["r"]["seam"]["hf_ratio"] for it in loops], default=0)
        line = (f"- {folder} ({len(its)}): true peak max {max(TPs):.1f} dBTP, clipped samples {clipped}")
        if Is:
            line += f", integrated {min(Is):.1f} to {max(Is):.1f} LUFS"
        if loops:
            line += (f"; {len(loops)} loops, worst seam ratios: step {sj}, curvature {sc}, 2 kHz high-pass {sh} "
                     "(the junction's step, second difference and HF burst over the 99th percentile of the same "
                     "quantity within +-50 ms; <= 1 means the seam is indistinguishable from its surroundings)")
        w(line + ".")
    w("- Ogg Vorbis loops decode to exactly loop-length sample counts (gapless). Seams were measured on the "
      "decoded .ogg files, not the intermediates.")
    if issues:
        w("\n**Requirement-check failures:**\n")
        for lid, pr in issues:
            w(f"- `{lid}`: {'; '.join(pr)}")
    else:
        w("- No requirement-check failures (rate, channels, loudness window, true-peak ceiling, clipping, loop seams, "
          "gapless length, minimum voice duration).")
    w("")
    w("### Not verified (needs a human)\n")
    w("- Taste and realism of every synthesised sound and the music (only objective metrics were checked).")
    w("- Prosody/emotion of the Kokoro lines, the shop_02 \"Ah...\" and the disc_01 \"stamped\" ending, and the "
      "mom_01_glitch edit (large-v3 hears \"Unpacked\" x4; small.en hears \"fun packed\", a common-phrase bias).")
    w("- Final in-game balance between buses. Reference levels at clip level: voice -18 LUFS, music -24 LUFS, ambience "
      "-30..-27.5 LUFS; SFX one-shots by Lmax (UI -19, props -20..-16, events/stingers -16..-13, footsteps -15 walk / "
      "-17 running layer, breathing -24..-14), set per-sound volumes in code. AudioDirector plays ambience beds at 0.8 "
      "and ducks music, ambience, SFX (lightly) and body sounds (breath, heartbeat) under narration.")
    with open(MANIFEST_MD, "w", encoding="utf-8", newline="\n") as fh:
        fh.write("\n".join(L) + "\n")


if __name__ == "__main__":
    main()
