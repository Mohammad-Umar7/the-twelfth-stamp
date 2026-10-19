"""Transcribe the final narration clips and diff them word-by-word against the script.

`npx --no-install hyperframes transcribe` is installed but has no ASR runtime on this machine
(whisper.cpp needs cmake to build; Parakeet is not installed), so this uses the faster-whisper
package that is already installed in the system Python together with Whisper models that are
already in the local Hugging Face cache (offline, nothing is downloaded).

  python -I Tools/audio/verify_voice.py                    # small.en, all clips
  python -I Tools/audio/verify_voice.py --model large-v3 --only fin_01,sau_01

Writes Audio/_work/voice_transcripts_<model>.json (text, word timestamps, mismatches).
"""
import argparse
import difflib
import glob
import json
import os
import re
import sys

os.environ["HF_HUB_OFFLINE"] = "1"
HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
VOICE = os.path.join(ROOT, "Game", "Assets", "Resources", "Audio", "Voice")
SCRIPT = os.path.join(ROOT, "Audio", "Script", "narration.json")
WORK = os.path.join(ROOT, "Audio", "_work")

NUM = {"1": "one", "2": "two", "3": "three", "4": "four", "5": "five", "6": "six", "7": "seven", "8": "eight",
       "9": "nine", "10": "ten", "11": "eleven", "12": "twelve"}


def tokens(s):
    s = s.lower().replace("’", "'")
    s = re.sub(r"[^a-z0-9' ]+", " ", s)
    out = []
    for w in s.split():
        w = w.strip("'")
        if not w:
            continue
        out.append(NUM.get(w, w))
    return out


def model_path(name):
    pat = os.path.join(os.path.expanduser("~"), ".cache", "huggingface", "hub",
                       f"models--Systran--faster-whisper-{name}", "snapshots", "*", "model.bin")
    hits = glob.glob(pat)
    if not hits:
        sys.exit(f"no cached faster-whisper model '{name}' (looked for {pat})")
    return os.path.dirname(hits[0])


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--model", default="small.en")
    ap.add_argument("--only", default="")
    args = ap.parse_args()
    from faster_whisper import WhisperModel
    import faster_whisper

    mp = model_path(args.model)
    model = WhisperModel(mp, device="cpu", compute_type="int8")
    with open(SCRIPT, encoding="utf-8") as fh:
        lines = {l["id"]: l for l in json.load(fh)["lines"]}
    only = set(filter(None, args.only.split(",")))
    res = {"engine": f"faster-whisper {faster_whisper.__version__}", "model": args.model, "model_path": mp, "clips": {}}
    for lid, line in lines.items():
        if only and lid not in only:
            continue
        wav = os.path.join(VOICE, lid + ".wav")
        segs, info = model.transcribe(wav, language="en", beam_size=5, word_timestamps=True,
                                      condition_on_previous_text=False, vad_filter=False, temperature=0.0)
        words, text = [], []
        for s in segs:
            text.append(s.text.strip())
            for w in (s.words or []):
                words.append({"w": w.word.strip(), "start": round(w.start, 3), "end": round(w.end, 3),
                              "p": round(w.probability, 3)})
        hyp = " ".join(text)
        a, b = tokens(line["text"]), tokens(hyp)
        mism = []
        for op, i1, i2, j1, j2 in difflib.SequenceMatcher(a=a, b=b, autojunk=False).get_opcodes():
            if op != "equal":
                mism.append({"op": op, "script": " ".join(a[i1:i2]), "heard": " ".join(b[j1:j2])})
        res["clips"][lid] = {"script": line["text"], "heard": hyp, "match": not mism, "mismatches": mism,
                             "words": words}
        flag = "OK " if not mism else "MISMATCH"
        print(f"{flag} {lid:12s} | {hyp}" + ("" if not mism else f"   <- {mism}"), flush=True)
    glitch = os.path.join(VOICE, "mom_01_glitch.wav")
    if os.path.exists(glitch) and (not only or "mom_01_glitch" in only):
        segs, _ = model.transcribe(glitch, language="en", beam_size=5, word_timestamps=True,
                                   condition_on_previous_text=False, vad_filter=False, temperature=0.0)
        segs = list(segs)
        words = [{"w": w.word.strip(), "start": round(w.start, 3), "end": round(w.end, 3), "p": round(w.probability, 3)}
                 for s in segs for w in (s.words or [])]
        hyp = " ".join(s.text.strip() for s in segs)
        res["clips"]["mom_01_glitch"] = {"script": None, "heard": hyp, "match": None, "mismatches": [], "words": words,
                                         "note": "derived clip (expected: 'unpacked' x3 + a cut-off 'unpa-')"}
        print(f"--  mom_01_glitch | {hyp}", flush=True)
    out = os.path.join(WORK, f"voice_transcripts_{args.model}.json")
    prev = {}
    if only and os.path.exists(out):
        with open(out, encoding="utf-8") as fh:
            prev = json.load(fh).get("clips", {})
    prev.update(res["clips"])
    res["clips"] = prev
    with open(out, "w", encoding="utf-8") as fh:
        json.dump(res, fh, indent=1)
    n_bad = sum(1 for c in res["clips"].values() if c["match"] is False)
    print(f"{len(res['clips'])} clips, {n_bad} with word mismatches -> {out}")


if __name__ == "__main__":
    main()
