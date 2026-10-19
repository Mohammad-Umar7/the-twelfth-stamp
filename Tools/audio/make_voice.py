"""Narration pipeline for The Twelfth Stamp.

Kokoro TTS through the Hyperframes CLI (local, free), then ffmpeg post-processing.

  Tools/.venv/Scripts/python.exe -I Tools/audio/make_voice.py            # all lines
  Tools/.venv/Scripts/python.exe -I Tools/audio/make_voice.py --only mom_01,end_02
  Tools/.venv/Scripts/python.exe -I Tools/audio/make_voice.py --force-tts  # regenerate raw TTS

Steps per line (id from Audio/Script/narration.json):
  1. `npx --no-install hyperframes tts <txt> -v <voice> -s 1.0 -o Audio/_work/voice_raw/<id>.wav --json`
     (text is passed through a .txt file to avoid shell quoting of ? ! ' characters)
  2. ffmpeg: trim silence (silenceremove, ~60 ms head / ~150 ms tail), speaker colour
     (mother = phone band 300-3400 Hz + light saturation; optician = light aecho room),
     two-pass loudnorm to -18 LUFS / -2 dBTP (linear), 10 ms fades, 48 kHz mono s16 (TPDF dither).
Output: Game/Assets/Resources/Audio/Voice/<id>.wav ; log: Audio/_work/voice_log.json
"""
import argparse
import json
import os
import re
import shutil
import subprocess
import sys
import time

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
SCRIPT = os.path.join(ROOT, "Audio", "Script", "narration.json")
WORK = os.path.join(ROOT, "Audio", "_work")
RAW = os.path.join(WORK, "voice_raw")
TXT = os.path.join(WORK, "voice_txt")
OUT = os.path.join(ROOT, "Game", "Assets", "Resources", "Audio", "Voice")
LOG = os.path.join(WORK, "voice_log.json")
VENV_PY = os.path.join(ROOT, "Tools", ".venv", "Scripts", "python.exe")

TARGET_I = -18.0
TARGET_TP = -2.0
TARGET_LRA = 15.0

# Closest-voice fallbacks, used only if `tts --list` does not report the scripted voice.
FALLBACK = {
    "am_michael": ["am_adam", "bm_george"],
    "am_adam": ["am_michael", "bm_george"],
    "bm_george": ["am_adam", "am_michael"],
    "bf_emma": ["bf_isabella", "af_sky"],
    "bf_isabella": ["bf_emma", "af_sky"],
    "af_heart": ["af_nova", "af_sky"],
}

# Pronunciation fixes. Kokoro's espeak front-end picked the wrong reading of these heteronyms/names
# (checked with kokoro_onnx.tokenizer.Tokenizer().phonemize and by ASR). Only the TTS input is
# respelled; subtitles/transcripts keep the script text.
#   tut_seal: "Close" -> /klˈəʊs/ (adjective) and "tear" -> /tˈiə/ (crying); want /kləʊz/, /teə/
#   opt_02:   "lives" -> /lˈaɪvz/ (noun);  want the verb /lɪvz/. With "livs", George's "leave" was
#             heard as "lave" by Whisper large-v3 in every comma variant; ending the sentence at
#             "leave." gives a clear /liːv/ (both ASR models: "leave", p=1.00).
#   end_02:   "Saeed" -> /sˈiːd/ ("seed"); want /sɑːˈiːd/
#   shop_02:  "Ah, those." is phonemised correctly (/ˈɑː/) but ran into "those" and both ASR models heard
#             "Are those"; a hesitation ("Ah... those.") makes the interjection read (large-v3: "Ah, those").
RESPELL = {
    "shop_02": [("Ah, those", "Ah... those")],
    "tut_seal": [("Close", "Cloze"), ("tear", "tare")],
    "opt_02": [("lives", "livs"), ("leave, unless", "leave. Unless")],
    "end_02": [("Saeed", "Sa-eed")],
}


def tts_text(lid, text):
    for a, b in RESPELL.get(lid, []):
        text = re.sub(r"\b%s\b" % re.escape(a), b, text)
    return text


TRIM = (
    "adelay=delays=100:all=1,apad=pad_dur=0.3,"
    "silenceremove=start_periods=1:start_threshold=-55dB:start_silence=0.06:detection=rms:window=0.01,"
    "areverse,"
    "silenceremove=start_periods=1:start_threshold=-55dB:start_silence=0.15:detection=rms:window=0.01,"
    "areverse"
)
# Mother: voicemail played from a phone. Band 300-3400 Hz (two 2-pole stages each side),
# a small presence lift and gentle tanh saturation driven by +6 dB then trimmed back.
PHONE = (
    "highpass=f=300:poles=2,highpass=f=300:poles=2,"
    "lowpass=f=3400:poles=2,lowpass=f=3400:poles=2,"
    "equalizer=f=1700:t=q:w=1.1:g=3,"
    "volume=6dB,asoftclip=type=tanh:oversample=4,volume=-6dB"
)
# Optician: short, light early-reflection room (keeps intelligibility).
ROOM = "aecho=in_gain=0.9:out_gain=0.88:delays=17|31|47|71:decays=0.20|0.14|0.10|0.06"


def npx():
    exe = shutil.which("npx") or shutil.which("npx.cmd")
    if not exe:
        sys.exit("npx not found on PATH")
    return exe


def run(cmd, env=None, check=True):
    p = subprocess.run(cmd, capture_output=True, text=True, env=env, encoding="utf-8", errors="replace")
    if check and p.returncode != 0:
        raise RuntimeError(f"command failed ({p.returncode}): {' '.join(cmd)}\n{p.stdout[-2000:]}\n{p.stderr[-2000:]}")
    return p


def list_voices(env):
    p = run([npx(), "--no-install", "hyperframes", "tts", "--list"], env=env)
    ids = re.findall(r"^\s+([a-z]{2}_[a-z]+)\s", p.stdout, flags=re.M)
    return ids, p.stdout


def last_json(text):
    """Return the last {...} JSON object printed in text."""
    end = text.rfind("}")
    depth = 0
    for i in range(end, -1, -1):
        if text[i] == "}":
            depth += 1
        elif text[i] == "{":
            depth -= 1
            if depth == 0:
                return json.loads(text[i:end + 1])
    raise ValueError("no JSON object found")


def colour_for(speaker):
    if speaker == "mother":
        return PHONE
    if speaker == "optician":
        return ROOM
    return None


def measure(raw, chain):
    f = f"{chain},loudnorm=I={TARGET_I}:TP={TARGET_TP}:LRA={TARGET_LRA}:print_format=json"
    p = run(["ffmpeg", "-hide_banner", "-nostats", "-i", raw, "-af", f, "-f", "null", "-"])
    return f, last_json(p.stderr)


def process(raw, out, speaker):
    pre = TRIM
    col = colour_for(speaker)
    if col:
        pre = pre + "," + col
    # pass 0: measure the trimmed/coloured speech
    f0, m0 = measure(raw, pre)
    gain = TARGET_I - float(m0["input_i"])
    pred_tp = float(m0["input_tp"]) + gain
    limited = pred_tp > TARGET_TP - 0.5
    iterations = 0
    if limited:
        # Kokoro speech often has a peak-to-loudness ratio > 16 dB, so -18 LUFS would put peaks
        # above -2 dBTP. Apply the gain, then a 4x-oversampled lookahead limiter at -2.6 dBFS
        # (sample peaks at 192 kHz ~= true peaks) so the final loudnorm pass can stay linear.
        # Limiting lowers loudness a little, so the pre-gain is re-measured and nudged until the
        # remaining loudnorm gain fits under the true-peak ceiling.
        ceiling = 10 ** ((TARGET_TP - 0.6) / 20)
        base = pre
        g = gain
        for iterations in range(1, 6):
            pre = (f"{base},volume={g:.2f}dB,aresample=192000:resampler=soxr,"
                   f"alimiter=limit={ceiling:.4f}:attack=1:release=60:level=disabled:latency=1")
            f1, m = measure(raw, pre)
            short = TARGET_I - float(m["input_i"])
            if float(m["input_tp"]) + short <= TARGET_TP - 0.1 and abs(short) < 0.4:
                break
            g += short + 0.05
    else:
        # pass 1: measure
        f1, m = measure(raw, pre)
    # pass 2: apply (linear), fades, resample to 48k s16 with TPDF dither
    f2 = (
        f"{pre},loudnorm=I={TARGET_I}:TP={TARGET_TP}:LRA={TARGET_LRA}"
        f":measured_I={m['input_i']}:measured_TP={m['input_tp']}:measured_LRA={m['input_lra']}"
        f":measured_thresh={m['input_thresh']}:offset={m['target_offset']}:linear=true:print_format=json,"
        "afade=t=in:d=0.01,areverse,afade=t=in:d=0.01,areverse,"
        "aresample=48000:resampler=soxr:precision=28:dither_method=triangular,"
        "aformat=sample_fmts=s16:sample_rates=48000:channel_layouts=mono"
    )
    p2 = run(["ffmpeg", "-hide_banner", "-nostats", "-y", "-i", raw, "-af", f2,
              "-c:a", "pcm_s16le", "-ar", "48000", "-ac", "1", "-map_metadata", "-1", out])
    m2 = last_json(p2.stderr)
    return {"filter_pass0": f0, "pass0": m0, "limiter_applied": limited, "limiter_iterations": iterations,
            "predicted_tp_without_limiter": round(pred_tp, 2),
            "filter_pass1": f1, "filter_pass2": f2, "pass1": m, "pass2": m2}


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--only", default="")
    ap.add_argument("--force-tts", action="store_true")
    args = ap.parse_args()

    for d in (RAW, TXT, OUT):
        os.makedirs(d, exist_ok=True)
    env = dict(os.environ)
    env["HYPERFRAMES_PYTHON"] = VENV_PY  # venv with kokoro-onnx + soundfile

    with open(SCRIPT, encoding="utf-8") as fh:
        script = json.load(fh)
    voices_available, list_text = list_voices(env)
    only = set(filter(None, args.only.split(",")))

    log = {}
    if os.path.exists(LOG):
        with open(LOG, encoding="utf-8") as fh:
            log = json.load(fh)
    log.setdefault("lines", {})
    log["tts_list_voices"] = voices_available
    subs = {}
    for spk, v in script["voices"].items():
        if v in voices_available:
            subs[spk] = {"requested": v, "used": v, "substituted": False}
        else:
            alt = next((a for a in FALLBACK.get(v, []) if a in voices_available), None)
            if not alt:
                sys.exit(f"no available voice for {spk} ({v})")
            subs[spk] = {"requested": v, "used": alt, "substituted": True}
    log["voices"] = subs

    for line in script["lines"]:
        lid = line["id"]
        if only and lid not in only:
            continue
        spk = line["speaker"]
        voice = subs[spk]["used"]
        txt = os.path.join(TXT, lid + ".txt")
        say = tts_text(lid, line["text"])
        with open(txt, "w", encoding="utf-8", newline="\n") as fh:
            fh.write(say)
        raw = os.path.join(RAW, lid + ".wav")
        rec = log["lines"].get(lid, {})
        if (args.force_tts or not os.path.exists(raw) or rec.get("tts_text", line["text"]) != say
                or rec.get("voice") != voice):
            cmd = [npx(), "--no-install", "hyperframes", "tts", txt, "-v", voice, "-s", "1.0", "-o", raw, "--json"]
            t0 = time.time()
            p = run(cmd, env=env)
            res = last_json(p.stdout)
            if not res.get("ok"):
                raise RuntimeError(f"tts failed for {lid}: {res}")
            rec = {"tts_cmd": "npx --no-install hyperframes tts " + os.path.relpath(txt, ROOT).replace("\\", "/")
                   + f" -v {voice} -s 1.0 -o " + os.path.relpath(raw, ROOT).replace("\\", "/") + " --json",
                   "tts_result": res, "tts_seconds": round(time.time() - t0, 2)}
        rec.update({"speaker": spk, "voice": voice, "text": line["text"], "tts_text": say,
                    "respelled": say != line["text"]})
        out = os.path.join(OUT, lid + ".wav")
        rec["post"] = process(raw, out, spk)
        log["lines"][lid] = rec
        n = rec["post"]["pass2"]
        print(f"{lid:12s} {voice:10s} raw_I={rec['post']['pass0']['input_i']:>7s} lim={str(rec['post']['limiter_applied']):5s} "
              f"pred_TP={rec['post']['predicted_tp_without_limiter']:>6.2f} -> out_I={n['output_i']:>7s} "
              f"out_TP={n['output_tp']:>6s} type={n['normalization_type']}", flush=True)

    with open(LOG, "w", encoding="utf-8") as fh:
        json.dump(log, fh, indent=1)


if __name__ == "__main__":
    main()
