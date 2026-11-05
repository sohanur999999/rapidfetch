"""Generate RapidFetch app icon + logo (no external assets required)."""
from PIL import Image, ImageDraw
import math
import os

ACCENT = (59, 130, 246, 255)
ACCENT_DARK = (29, 78, 216, 255)
BG = (15, 17, 21, 255)
CYAN = (34, 211, 238, 255)
GREEN = (52, 211, 153, 255)


def rounded(size, radius):
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.rounded_rectangle((0, 0, size - 1, size - 1), radius=radius, fill=BG)
    # subtle diagonal sheen
    for i in range(size):
        alpha = int(18 * (1 - i / size))
        d.line([(0, i), (size, i)], fill=(255, 255, 255, alpha))
    d.rounded_rectangle((0, 0, size - 1, size - 1), radius=radius, outline=(60, 68, 84, 255), width=max(1, size // 64))
    return img


def draw_glyph(size, with_word=False):
    img = rounded(size, int(size * 0.22))
    d = ImageDraw.Draw(img)
    cx = size / 2
    # arrow shaft
    shaft_w = int(size * 0.11)
    top = int(size * 0.20)
    bottom = int(size * 0.56)
    d.rounded_rectangle((cx - shaft_w / 2, top, cx + shaft_w / 2, bottom), radius=shaft_w / 2, fill=ACCENT)
    # arrow head
    head = int(size * 0.20)
    d.polygon([(cx - head, bottom - size * 0.03), (cx + head, bottom - size * 0.03), (cx, bottom + size * 0.15)], fill=CYAN)
    # tray
    tray_y = int(size * 0.72)
    tray_h = max(2, int(size * 0.05))
    d.rounded_rectangle((size * 0.22, tray_y, size * 0.78, tray_y + tray_h), radius=tray_h / 2, fill=GREEN)
    return img


def make_ico(path, sizes=(16, 24, 32, 48, 64, 128, 256)):
    imgs = [draw_glyph(s) for s in sizes]
    os.makedirs(os.path.dirname(path), exist_ok=True)
    imgs[-1].save(path, format="ICO", sizes=[(s, s) for s in sizes], append_images=imgs[:-1])


def make_logo(path, size=512):
    img = draw_glyph(size)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    img.save(path, format="PNG")


if __name__ == "__main__":
    root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    assets = os.path.join(root, "src", "RapidFetch.App", "Assets")
    make_ico(os.path.join(assets, "app.ico"))
    make_logo(os.path.join(assets, "logo.png"))
    for s in (16, 32, 48, 128):
        os.makedirs(os.path.join(root, "extension", "icons"), exist_ok=True)
        draw_glyph(s).save(os.path.join(root, "extension", "icons", f"icon{s}.png"))
    print("assets written to", assets)
