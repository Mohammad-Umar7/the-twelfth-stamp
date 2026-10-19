"""The Twelfth Stamp - procedural texture and 2D graphics generator.

Regenerates every texture deterministically (fixed seeds derived from asset names):

    Tools/.venv/Scripts/python.exe Tools/textures/gen_textures.py            # everything
    Tools/.venv/Scripts/python.exe Tools/textures/gen_textures.py --only paper_ivory stamp_uae
    Tools/.venv/Scripts/python.exe Tools/textures/gen_textures.py --preview <dir>   # also write QA sheets

Outputs
    Game/Assets/Resources/Textures/<name>_a.png / <name>_n.png   tileable materials
    Game/Assets/Resources/Art/*.png, *.json                       2D game graphics + layouts
    Art/TextureManifest.md                                        manifest + tiling-check table
    Art/texture_contact_sheet.png                                 contact sheet (tiled 2x2 + graphics)

Only numpy, scipy and Pillow are used; nothing is downloaded.
"""
from __future__ import annotations

import argparse
import json
import sys
import time
from pathlib import Path

import numpy as np
from PIL import Image

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))

import tx_core as T  # noqa: E402
from tx_materials import MATERIALS  # noqa: E402

ROOT = HERE.parents[1]
TEX_DIR = ROOT / "Game" / "Assets" / "Resources" / "Textures"
ART_DIR = ROOT / "Game" / "Assets" / "Resources" / "Art"
DOC_DIR = ROOT / "Art"
STATE_FILE = HERE / "last_run.json"   # cached stats so partial (--only) runs keep a full manifest


# --------------------------------------------------------------------------- tiling check
def seam_check(a: np.ndarray) -> dict:
    """Compare the wrap-around neighbours (last column vs first column, last row vs first row) with
    ordinary neighbouring pixel pairs. A seamless tile has seam differences of the same size as
    interior neighbour differences (ratio ~1). Values are mean absolute differences in 8-bit levels."""
    a = a.astype(np.float32)
    if a.ndim == 2:
        a = a[..., None]
    col_d = np.abs(np.diff(a, axis=1)).mean(axis=(0, 2))      # per adjacent column pair
    row_d = np.abs(np.diff(a, axis=0)).mean(axis=(1, 2))      # per adjacent row pair
    seam_lr = float(np.abs(a[:, 0] - a[:, -1]).mean())
    seam_tb = float(np.abs(a[0] - a[-1]).mean())
    med_c, med_r = float(np.median(col_d)), float(np.median(row_d))
    p99_c, p99_r = float(np.percentile(col_d, 99)), float(np.percentile(row_d, 99))
    r_lr = seam_lr / max(med_c, 0.5)
    r_tb = seam_tb / max(med_r, 0.5)
    ok = (seam_lr <= max(1.5 * med_c, p99_c) + 0.5) and (seam_tb <= max(1.5 * med_r, p99_r) + 0.5)
    return dict(seam_lr=round(seam_lr, 2), inner_lr=round(med_c, 2), ratio_lr=round(r_lr, 2),
                seam_tb=round(seam_tb, 2), inner_tb=round(med_r, 2), ratio_tb=round(r_tb, 2), ok=bool(ok))


def albedo_stats(rgb_u8: np.ndarray) -> dict:
    y = T.lum(rgb_u8.astype(np.float32) / 255.0)
    return dict(lum_mean=round(float(y.mean()), 3), lum_p1=round(float(np.percentile(y, 1)), 3),
                lum_p99=round(float(np.percentile(y, 99)), 3))


# --------------------------------------------------------------------------- materials
def build_material(name: str, spec: dict, log: list) -> dict:
    t0 = time.time()
    n = spec["size"]
    rng = T.rng_for(name)
    out = spec["fn"](n, rng)
    rec = dict(name=name, size=n, kind=spec["kind"], purpose=spec["purpose"], method=spec["method"], files=[])
    if "albedo" in out:
        alb = T.to_u8(T.albedo_guard(out["albedo"]))
        p = TEX_DIR / f"{name}_a.png"
        T.save_png(p, alb)
        rec["files"].append(dict(file=p.name, w=n, h=n, fmt="RGB sRGB albedo", seam=seam_check(alb),
                                 **albedo_stats(alb)))
    if "mask" in out:
        m = T.to_u8(out["mask"])
        p = TEX_DIR / f"{name}_a.png"
        T.save_png(p, m)
        rec["files"].append(dict(file=p.name, w=n, h=n, fmt="L 8-bit grayscale mask", seam=seam_check(m),
                                 mask_mean=round(float(m.mean() / 255), 3)))
    if "normal" in out or "height" in out:
        nr = out.get("normal")
        if nr is None:
            nr = T.height_to_normal(out["height"], out.get("nstr", 4.0))
        nu8 = T.normal_rgb(nr)
        p = TEX_DIR / f"{name}_n.png"
        T.save_png(p, nu8)
        rec["files"].append(dict(file=p.name, w=n, h=n, fmt="RGB tangent normal (+Y up)", seam=seam_check(nu8),
                                 tilt_deg=round(T.normal_tilt_deg(nr), 2)))
    secs = time.time() - t0
    msg = f"[mat] {name:20s} {secs:5.1f}s " + "  ".join(
        f"{f['file']}: seam lr {f['seam']['ratio_lr']} tb {f['seam']['ratio_tb']} {'OK' if f['seam']['ok'] else 'FAIL'}"
        + (f" lum {f['lum_mean']} [{f['lum_p1']},{f['lum_p99']}]" if 'lum_mean' in f else "")
        + (f" tilt {f['tilt_deg']}" if 'tilt_deg' in f else "")
        for f in rec["files"])
    print(msg, flush=True)
    log.append(rec)
    return rec


# --------------------------------------------------------------------------- previews
def _thumb(arr, size):
    im = Image.fromarray(arr) if not isinstance(arr, Image.Image) else arr
    return im.resize((size, size), Image.LANCZOS)


def tiled2(a):
    return np.concatenate([np.concatenate([a, a], 1)] * 2, 0)


def preview_materials(names, out_dir: Path, per_sheet=4, panel=360):
    """QA sheet per material: [2x2 tiled overview] [1:1 albedo crop centred on the tile corner where all
    four seams meet] [1:1 normal crop there] [1:1 lit crop there]."""
    out_dir.mkdir(parents=True, exist_ok=True)
    sheets = []
    for s in range(0, len(names), per_sheet):
        chunk = names[s:s + per_sheet]
        sheet = Image.new("RGB", (panel * 4, panel * len(chunk)), (40, 40, 40))
        for r, nm in enumerate(chunk):
            a_p, n_p = TEX_DIR / f"{nm}_a.png", TEX_DIR / f"{nm}_n.png"
            alb = np.asarray(Image.open(a_p).convert("RGB")) if a_p.exists() else None
            nrm = np.asarray(Image.open(n_p).convert("RGB")) if n_p.exists() else None
            ref = alb if alb is not None else nrm
            n = ref.shape[0]
            h2 = panel // 2

            def corner(img):
                t = tiled2(img)
                return t[n - h2:n + h2, n - h2:n + h2]

            panels = [tiled2(ref)]
            if alb is not None:
                panels.append(corner(alb))
            if nrm is not None:
                panels.append(corner(nrm))
                nf = nrm.astype(np.float32) / 127.5 - 1
                base = alb.astype(np.float32) / 255 if alb is not None and alb.shape == nrm.shape else np.full(nrm.shape, 0.6, np.float32)
                panels.append(corner(T.to_u8(T.shade_lit(base, nf))))
            for c, p in enumerate(panels):
                sheet.paste(_thumb(p, panel), (c * panel, r * panel))
        fp = out_dir / f"mat_sheet_{s // per_sheet:02d}.png"
        sheet.save(fp)
        sheets.append(fp)
    return sheets


# --------------------------------------------------------------------------- main
def main(argv=None):
    ap = argparse.ArgumentParser()
    ap.add_argument("--only", nargs="*", default=None, help="asset names to (re)build")
    ap.add_argument("--preview", default=None, help="directory for QA preview sheets")
    ap.add_argument("--no-report", action="store_true", help="skip manifest + contact sheet")
    ap.add_argument("--skip-mat", action="store_true", help="do not rebuild materials")
    ap.add_argument("--skip-gfx", action="store_true", help="do not rebuild graphics")
    args = ap.parse_args(argv)
    for d in (TEX_DIR, ART_DIR, DOC_DIR):
        d.mkdir(parents=True, exist_ok=True)

    try:
        import tx_graphics
        GRAPHICS = tx_graphics.GRAPHICS
    except ImportError as e:  # graphics module optional while developing
        print("graphics module not available:", e)
        GRAPHICS = {}

    state = json.loads(STATE_FILE.read_text()) if STATE_FILE.exists() else {"materials": {}, "graphics": {}}
    only = set(args.only) if args.only else None
    mat_log, gfx_log = [], []
    for name, spec in MATERIALS.items():
        if (only and name not in only) or args.skip_mat:
            continue
        rec = build_material(name, spec, mat_log)
        state["materials"][name] = rec
    for name, spec in GRAPHICS.items():
        if (only and name not in only) or args.skip_gfx:
            continue
        t0 = time.time()
        recs = tx_graphics.build(name, spec, ART_DIR)
        for r in recs:
            state["graphics"][r["file"]] = r
        print(f"[gfx] {name:24s} {time.time() - t0:5.1f}s -> " + ", ".join(r["file"] for r in recs), flush=True)
        gfx_log.extend(recs)

    # drop stale entries for assets that no longer exist in the registries
    state["materials"] = {k: v for k, v in state["materials"].items() if k in MATERIALS}
    state["graphics"] = {k: v for k, v in state["graphics"].items() if v.get("group") in GRAPHICS}
    STATE_FILE.write_text(json.dumps(state, indent=1))

    if args.preview:
        pdir = Path(args.preview)
        names = [m for m in MATERIALS if (not only or m in only)]
        for fp in preview_materials(names, pdir):
            print("preview:", fp)
        if GRAPHICS:
            for fp in tx_graphics.preview(state, ART_DIR, pdir, only):
                print("preview:", fp)

    if not args.no_report and GRAPHICS:
        import tx_report
        tx_report.write_contact_sheet(state, TEX_DIR, ART_DIR, DOC_DIR / "texture_contact_sheet.png")
        tx_report.write_manifest(state, DOC_DIR / "TextureManifest.md", TEX_DIR, ART_DIR)
        print("report written")

    fails = [f["file"] for r in state["materials"].values() for f in r["files"] if not f["seam"]["ok"]]
    print("tiling check:", "ALL OK" if not fails else f"FAIL {fails}")
    return 0 if not fails else 1


if __name__ == "__main__":
    sys.exit(main())
