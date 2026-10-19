"""Builds every branding derivative from the approved key art.

Usage (from the repo root):  python -I Tools/branding/make_branding.py

Sources (both supplied and approved by Awaiz Ahmed; the untouched originals stay in the repo root, byte-identical
hash-checked copies live in Art/Branding/source/, and nothing at runtime depends on an external absolute path):
  "The Twelfth Stamp_ Portals of Shadow.png"   the key art (cover)
  "The Twelfth Stamp_ Bloodied Metallic Logo.png"   the title lettering on transparency (welcome and intro screens)

The PNG has no alpha channel. Its picture is a rounded square on black, with different corner radii at the top and the
bottom. The radii are fitted from the image (circle fit along each corner's edge band), and only pixels OUTSIDE the
fitted corner arcs become transparent. Dark pixels inside the picture (the Passenger, shadows) are never keyed out.

Outputs
  Art/Branding/source/KeyArt_PortalsOfShadow.png        byte-identical copy (SHA-256 checked)
  Game/Assets/Resources/Art/Branding/keyart_cover.png   full-resolution cover, corners transparent
  Game/Assets/Resources/Art/Branding/keyart_logo.png    the supplied logo (alpha kept as supplied), max 2048 px wide
  Game/Assets/Resources/Art/Branding/keyart_menu.png    the cover's picture area above its title (pairs with the logo)
  Game/Assets/Resources/Art/Branding/keyart_thumb.png   thumbnail derivative (Passenger, glasses, passport; no title)
  Game/Assets/Branding/Icons/icon_<size>.png            Windows application icon sizes for Unity Player Settings
  Tools/install/TheTwelfthStamp.ico                     multi-resolution .ico for the shortcuts (16 to 256 px)
  Art/Branding/previews/*.png                           contact sheets at 1x and magnified, light and dark grounds
  Art/Branding/branding_manifest.json                   hashes, fitted radii, crop boxes, size rules
"""
import hashlib
import io
import json
import shutil
import struct
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

ROOT = Path(__file__).resolve().parents[2]
SRC_NAME = "The Twelfth Stamp_ Portals of Shadow.png"
SRC_ROOT = ROOT / SRC_NAME
SRC_COPY = ROOT / "Art" / "Branding" / "source" / "KeyArt_PortalsOfShadow.png"
LOGO_NAME = "The Twelfth Stamp_ Bloodied Metallic Logo.png"
LOGO_ROOT = ROOT / LOGO_NAME
LOGO_COPY = ROOT / "Art" / "Branding" / "source" / "Logo_BloodiedMetallic.png"
RES = ROOT / "Game" / "Assets" / "Resources" / "Art" / "Branding"
ICONS = ROOT / "Game" / "Assets" / "Branding" / "Icons"
ICO = ROOT / "Tools" / "install" / "TheTwelfthStamp.ico"
PREV = ROOT / "Art" / "Branding" / "previews"
MANIFEST = ROOT / "Art" / "Branding" / "branding_manifest.json"

# Crop boxes in source pixels (left, top, right, bottom).
THUMB_BOX = (160, 55, 1050, 945)      # Passenger face, both lenses (Egypt / Paris), passport: reads down to 16 px
MENU_BOX = (0, 0, 1254, 915)          # the cover above its title lettering ("THE" starts at y ~945)
THUMB_MAX = 96                        # icon sizes up to this use the thumbnail; larger sizes use the full cover
UNITY_ICON_SIZES = [1024, 512, 256, 128, 64, 48, 32, 16]
ICO_SIZES = [16, 20, 24, 32, 40, 48, 64, 96, 128, 256]


def sha256(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def fit_corner_radius(content, corner, lo=200, hi=460):
    """Circle fit for one rounded corner: minimise content cut away + black left inside, in a band around the arc."""
    if corner == "TL":
        sub = content
    elif corner == "TR":
        sub = content[:, ::-1]
    elif corner == "BL":
        sub = content[::-1, :]
    else:
        sub = content[::-1, ::-1]
    S = hi + 20
    sub = sub[:S, :S]
    yy, xx = np.mgrid[0:S, 0:S] + 0.5
    best = None
    for R in range(lo, hi, 1):
        inside_box = (xx < R) & (yy < R)
        dist = np.sqrt((xx - R) ** 2 + (yy - R) ** 2) - R
        band = inside_box & (np.abs(dist) < 25)
        outside = inside_box & (dist > 0)
        lost = (sub & outside & band).sum()
        resid = (~sub & ~outside & band & (dist > -6)).sum()
        score = lost + resid
        if best is None or score < best[0]:
            best = (score, R, int(lost), int(resid))
    return {"radius": best[1], "content_cut": best[2], "dark_left_inside": best[3]}


def rounded_mask(w, h, radii, ss=4):
    """Anti-aliased rounded-rectangle mask with per-corner radii (TL, TR, BR, BL), supersampled."""
    W, H = w * ss, h * ss
    m = Image.new("L", (W, H), 255)
    d = ImageDraw.Draw(m)
    tl, tr, br, bl = [r * ss for r in radii]
    for (cx, cy, r, qx, qy) in [(tl, tl, tl, 0, 0), (W - tr, tr, tr, 1, 0), (W - br, H - br, br, 1, 1), (bl, H - bl, bl, 0, 1)]:
        if r <= 0:
            continue
        # Clear the corner square, then draw the quarter disc back in.
        x0 = 0 if qx == 0 else W - r
        y0 = 0 if qy == 0 else H - r
        d.rectangle((x0, y0, x0 + r - 1, y0 + r - 1), fill=0)
        d.ellipse((cx - r, cy - r, cx + r - 1, cy + r - 1), fill=255)
    # The ellipses also painted inside the rectangle body, which was already 255: harmless.
    return m.resize((w, h), Image.LANCZOS)


def feather_mask(w, h, edge):
    """Soft rectangular fade to transparent over `edge` pixels on every side (smoothstep)."""
    x = np.minimum(np.arange(w), np.arange(w)[::-1]).astype(np.float32) / edge
    y = np.minimum(np.arange(h), np.arange(h)[::-1]).astype(np.float32) / edge
    fx = np.clip(x, 0, 1)
    fy = np.clip(y, 0, 1)
    fx = fx * fx * (3 - 2 * fx)
    fy = fy * fy * (3 - 2 * fy)
    return (np.outer(fy, fx) * 255).astype(np.uint8)


def resize(img, size, sharpen=False):
    out = img.resize((size, size), Image.LANCZOS)
    if sharpen:
        # Very light unsharp mask on colour only, so tiny sizes keep the frame and lens edges.
        rgb = out.convert("RGB").filter(ImageFilter.UnsharpMask(radius=0.6, percent=60, threshold=2))
        rgb.putalpha(out.getchannel("A"))
        out = rgb
    return out


def bmp_icon_entry(img):
    """32-bit BGRA DIB with an AND mask, the classic ICO payload (best compatibility below 256 px)."""
    w, h = img.size
    a = np.asarray(img.convert("RGBA"))
    bgra = a[::-1, :, [2, 1, 0, 3]].tobytes()   # bottom-up rows
    row_bytes = ((w + 31) // 32) * 4
    and_mask = bytearray(row_bytes * h)
    alpha = a[::-1, :, 3]
    for y in range(h):
        for x in range(w):
            if alpha[y, x] == 0:
                and_mask[y * row_bytes + x // 8] |= 0x80 >> (x % 8)
    header = struct.pack("<IiiHHIIiiII", 40, w, h * 2, 1, 32, 0, len(bgra) + len(and_mask), 0, 0, 0, 0)
    return header + bgra + bytes(and_mask)


def write_ico(path, images):
    """images: {size: RGBA Image}. 256 px is stored as PNG, smaller sizes as DIB."""
    sizes = sorted(images)
    payloads = []
    for s in sizes:
        if s >= 256:
            buf = io.BytesIO()
            images[s].save(buf, format="PNG", optimize=True)
            payloads.append(buf.getvalue())
        else:
            payloads.append(bmp_icon_entry(images[s]))
    out = bytearray(struct.pack("<HHH", 0, 1, len(sizes)))
    offset = 6 + 16 * len(sizes)
    for s, data in zip(sizes, payloads):
        out += struct.pack("<BBBBHHII", s % 256, s % 256, 0, 0, 1, 32, len(data), offset)
        offset += len(data)
    for data in payloads:
        out += data
    Path(path).write_bytes(bytes(out))


def contact_sheet(images, bg, scale=1):
    sizes = sorted(images)
    pad = 12
    W = sum(s * scale for s in sizes) + pad * (len(sizes) + 1)
    H = max(sizes) * scale + 2 * pad
    sheet = Image.new("RGBA", (W, H), bg)
    x = pad
    for s in sizes:
        im = images[s]
        if scale != 1:
            im = im.resize((s * scale, s * scale), Image.NEAREST)
        sheet.alpha_composite(im, (x, H - pad - s * scale))
        x += s * scale + pad
    return sheet.convert("RGB")


def main():
    if not SRC_ROOT.exists() and not SRC_COPY.exists():
        sys.exit("Approved key art not found: " + str(SRC_ROOT))
    SRC_COPY.parent.mkdir(parents=True, exist_ok=True)
    if SRC_ROOT.exists():
        if not SRC_COPY.exists() or sha256(SRC_COPY) != sha256(SRC_ROOT):
            shutil.copyfile(SRC_ROOT, SRC_COPY)
        assert sha256(SRC_COPY) == sha256(SRC_ROOT), "copy is not byte-identical"
    src_hash = sha256(SRC_COPY)

    src = Image.open(SRC_COPY)
    mode, size = src.mode, src.size
    rgb = src.convert("RGB")
    arr = np.asarray(rgb).astype(int)
    content = arr.sum(axis=2) > 40
    fits = {c: fit_corner_radius(content, c) for c in ("TL", "TR", "BL", "BR")}
    radii = (fits["TL"]["radius"], fits["TR"]["radius"], fits["BR"]["radius"], fits["BL"]["radius"])
    print("source", size, mode, src_hash)
    print("fitted corner radii (TL, TR, BR, BL):", radii, fits)

    w, h = size
    mask = rounded_mask(w, h, radii)
    cover = rgb.copy()
    cover.putalpha(mask)

    # Check: how much clearly visible picture the mask removes (should be only the anti-aliased rim of the arcs).
    m = np.asarray(mask).astype(np.float32) / 255.0
    lum = arr.sum(axis=2) / 3.0
    removed_bright = int(((m < 0.5) & (lum > 60)).sum())
    print("mask check: bright pixels (luma > 60) made transparent =", removed_bright)

    RES.mkdir(parents=True, exist_ok=True)
    ICONS.mkdir(parents=True, exist_ok=True)
    PREV.mkdir(parents=True, exist_ok=True)

    cover.save(RES / "keyart_cover.png", optimize=True)

    # Logo: the supplied lettering, alpha untouched (its soft shadow is part of it); only resized to fit 2048 px.
    if not LOGO_ROOT.exists() and not LOGO_COPY.exists():
        sys.exit("Supplied logo not found: " + str(LOGO_ROOT))
    if LOGO_ROOT.exists() and (not LOGO_COPY.exists() or sha256(LOGO_COPY) != sha256(LOGO_ROOT)):
        shutil.copyfile(LOGO_ROOT, LOGO_COPY)
    logo_hash = sha256(LOGO_COPY)
    logo_src = Image.open(LOGO_COPY)
    logo_mode, logo_size = logo_src.mode, logo_src.size
    logo = logo_src.convert("RGBA")
    if logo.width > 2048:
        logo = logo.resize((2048, round(logo.height * 2048 / logo.width)), Image.LANCZOS)
    logo.save(RES / "keyart_logo.png", optimize=True)
    print("logo", logo_size, logo_mode, logo_hash, "->", logo.size)

    # Menu art: the cover above its title, so the welcome screen can pair it with the logo without showing the title
    # twice. Same picture, same top corners; the bottom corners get the cover's top radius scaled to the crop.
    menu = cover.crop(MENU_BOX)
    mw, mh = menu.size
    ma = np.asarray(menu.getchannel("A")).astype(np.float32) / 255.0
    br = radii[0]
    rb = np.asarray(rounded_mask(mw, mh, (0, 0, br, br))).astype(np.float32) / 255.0
    menu.putalpha(Image.fromarray((ma * rb * 255).astype(np.uint8)))
    menu.save(RES / "keyart_menu.png", optimize=True)
    old = RES / "keyart_title.png"
    if old.exists():
        old.unlink()  # superseded by the supplied logo

    # Thumbnail derivative: same glasses/passport composition, rounded like the cover (radius scaled to the crop).
    thumb_src = rgb.crop(THUMB_BOX)
    tw = thumb_src.width
    tr = int(round(tw * 0.16))
    thumb = thumb_src.copy()
    thumb.putalpha(rounded_mask(tw, tw, (tr, tr, tr, tr)))
    thumb256 = resize(thumb, 256)
    thumb256.save(RES / "keyart_thumb.png", optimize=True)

    def icon_for(s):
        return resize(thumb, s, sharpen=s <= 48) if s <= THUMB_MAX else resize(cover, s)

    unity_icons = {}
    for s in UNITY_ICON_SIZES:
        im = icon_for(s)
        p = ICONS / f"icon_{s}.png"
        im.save(p, optimize=True)
        unity_icons[s] = p
    ico_images = {s: icon_for(s) for s in ICO_SIZES}
    write_ico(ICO, ico_images)
    with Image.open(ICO) as ico_check:
        ico_sizes = sorted(ico_check.info.get("sizes", []))
    print("ico sizes:", ico_sizes)

    contact_sheet(ico_images, (236, 236, 238, 255)).save(PREV / "icon_sizes_light.png")
    contact_sheet(ico_images, (24, 24, 28, 255)).save(PREV / "icon_sizes_dark.png")
    contact_sheet({s: ico_images[s] for s in (16, 20, 24, 32, 48)}, (236, 236, 238, 255), scale=4).save(PREV / "icon_small_sizes_4x.png")
    bg = Image.new("RGBA", cover.size, (236, 236, 238, 255))
    bg.alpha_composite(cover)
    bg.convert("RGB").resize((512, 512), Image.LANCZOS).save(PREV / "cover_on_light.png")
    for name, bgc in (("logo_on_dark.png", (14, 12, 14, 255)), ("logo_on_paper.png", (232, 226, 210, 255))):
        lbg = Image.new("RGBA", logo.size, bgc)
        lbg.alpha_composite(logo)
        lbg.convert("RGB").resize((1024, round(logo.height * 1024 / logo.width)), Image.LANCZOS).save(PREV / name)
    mbg = Image.new("RGBA", menu.size, (14, 12, 14, 255))
    mbg.alpha_composite(menu)
    mbg.convert("RGB").resize((627, round(mh * 627 / mw)), Image.LANCZOS).save(PREV / "menu_art_on_dark.png")
    stale = PREV / "title_plate_on_dark.png"
    if stale.exists():
        stale.unlink()

    manifest = {
        "source": {
            "approved_by": "Awaiz Ahmed",
            "original_path": SRC_NAME + " (repo root, left untouched)",
            "build_copy": str(SRC_COPY.relative_to(ROOT)).replace("\\", "/"),
            "sha256": src_hash,
            "size": list(size),
            "mode": mode,
            "alpha_in_source": "none (RGB); corners are black pixels",
            "made_with": "ChatGPT image generation (per Awaiz Ahmed, 17 Oct 2026)",
        },
        "corner_mask": {
            "method": "circle fit per corner on the edge band; only pixels outside the arcs become transparent",
            "radii_px": {"TL": radii[0], "TR": radii[1], "BR": radii[2], "BL": radii[3]},
            "fit": fits,
            "bright_pixels_made_transparent": removed_bright,
        },
        "logo": {
            "approved_by": "Awaiz Ahmed",
            "original_path": LOGO_NAME + " (repo root, left untouched)",
            "build_copy": str(LOGO_COPY.relative_to(ROOT)).replace("\\", "/"),
            "sha256": logo_hash,
            "size": list(logo_size),
            "mode": logo_mode,
            "alpha_in_source": "yes; kept exactly as supplied",
            "used_for": "title on the intro and welcome screens and on end cards",
        },
        "crops": {"thumbnail": THUMB_BOX, "menu_art": MENU_BOX},
        "icon_rule": f"sizes <= {THUMB_MAX} px use the thumbnail crop (title unreadable); larger sizes use the full cover",
        "outputs": {},
    }
    outs = [RES / "keyart_cover.png", RES / "keyart_logo.png", RES / "keyart_menu.png", RES / "keyart_thumb.png", ICO] + [unity_icons[s] for s in UNITY_ICON_SIZES]
    for p in outs:
        with Image.open(p) as im:
            dims = list(im.size)
        manifest["outputs"][str(Path(p).relative_to(ROOT)).replace("\\", "/")] = {"sha256": sha256(p), "size": dims, "bytes": Path(p).stat().st_size}
    manifest["outputs"][str(ICO.relative_to(ROOT)).replace("\\", "/")]["ico_sizes"] = ico_sizes
    MANIFEST.write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    print("wrote", MANIFEST.relative_to(ROOT))


if __name__ == "__main__":
    main()
