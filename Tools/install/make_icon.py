"""Builds the multi-size Windows .ico used by the desktop shortcuts (the game .exe keeps its own icon,
Game/Assets/Resources/Art/icon_app.png).

Usage: python -I make_icon.py <source.png> <out.ico> [--round]

The shortcut icon is made from the key art "The Twelfth Stamp_ Portals of Shadow.png". With --round, black corners
around a rounded-square picture become transparent (the corner radius is measured from the image's top row).
"""
import sys
from PIL import Image, ImageDraw

src_path, ico_path = sys.argv[1], sys.argv[2]
round_corners = "--round" in sys.argv[3:]

img = Image.open(src_path).convert("RGBA")
w, h = img.size
if round_corners:
    # Radius = where the picture starts along the top edge (first clearly non-black pixel).
    radius = 0
    for x in range(w // 2):
        r, g, b, _ = img.getpixel((x, 0))
        if r + g + b > 36:
            radius = x
            break
    scale = 4  # supersampled mask for a smooth edge
    mask = Image.new("L", (w * scale, h * scale), 0)
    ImageDraw.Draw(mask).rounded_rectangle((0, 0, w * scale - 1, h * scale - 1), radius=radius * scale, fill=255)
    img.putalpha(mask.resize((w, h), Image.LANCZOS))
    print("corner radius", radius, "px")

sizes = [(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)]
img.resize((256, 256), Image.LANCZOS).save(ico_path, format="ICO", sizes=sizes)
print("wrote", ico_path)
