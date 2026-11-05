"""Generate Chrome Web Store and GitHub promotional artwork for RapidFetch.

Outputs (docs/store-assets):
  01-desktop.png             1280x800
  02-browser-capture.png     1280x800
  03-pause-resume.png        1280x800
  04-queue-scheduler.png     1280x800
  05-free-for-life.png       1280x800
  promo-440x280.png          440x280
  marquee-1400x560.png       1400x560
  social-preview-1280x640.png 1280x640

Run: python tools/make_store_assets.py
"""
from __future__ import annotations

import os
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = Path(__file__).resolve().parent.parent
SHOTS = ROOT / "docs" / "screenshots"
OUT = ROOT / "docs" / "store-assets"
ICON = ROOT / "extension" / "icons" / "icon128.png"

TEXT = (232, 238, 247)
MUTED = (154, 168, 188)
BLUE = (96, 165, 250)
CYAN = (34, 211, 238)
GREEN = (52, 211, 153)
LINE = (42, 58, 84)
FONT_DIR = Path("C:/Windows/Fonts")

FONT_REG = str(FONT_DIR / "segoeui.ttf")
FONT_SEMI = str(FONT_DIR / "seguisb.ttf")
FONT_BOLD = str(FONT_DIR / "segoeuib.ttf")

_font_cache: dict[tuple[str, int], ImageFont.FreeTypeFont] = {}


def font(path: str, size: int) -> ImageFont.FreeTypeFont:
    key = (path, size)
    if key not in _font_cache:
        _font_cache[key] = ImageFont.truetype(path, size)
    return _font_cache[key]


def background(width: int, height: int) -> Image.Image:
    yy, xx = np.mgrid[0:height, 0:width].astype(np.float32)
    nx, ny = xx / width, yy / height
    base = np.zeros((height, width, 3), dtype=np.float32)
    base[:] = (7, 11, 18)

    glows = [
        (-0.06, -0.12, 0.78, (59, 130, 246), 0.42),
        (1.04, 0.02, 0.62, (34, 211, 238), 0.16),
        (0.52, 1.18, 0.72, (52, 211, 153), 0.10),
    ]
    for cx, cy, radius, color, strength in glows:
        dist = np.sqrt((nx - cx) ** 2 + (ny - cy) ** 2)
        falloff = np.clip(1.0 - dist / radius, 0.0, 1.0) ** 2 * strength
        for channel in range(3):
            base[..., channel] += color[channel] * falloff

    # Fine engineering grid.
    grid = np.zeros((height, width), dtype=np.float32)
    grid[:, ::64] = 4.0
    grid[::64, :] = 4.0
    base += grid[..., None]

    # Vignette.
    edge = np.clip(np.maximum(np.abs(nx - 0.5), np.abs(ny - 0.5)) - 0.18, 0.0, 0.6)
    base *= (1.0 - edge[..., None] * 0.9)

    rng = np.random.default_rng(7)
    base += rng.normal(0.0, 1.1, base.shape)

    return Image.fromarray(np.clip(base, 0, 255).astype(np.uint8), "RGB").convert("RGBA")


def wrap(draw: ImageDraw.ImageDraw, text: str, fnt: ImageFont.FreeTypeFont, max_width: int) -> list[str]:
    words = text.split()
    lines: list[str] = []
    current = ""
    for word in words:
        candidate = f"{current} {word}".strip()
        if draw.textlength(candidate, font=fnt) <= max_width:
            current = candidate
        else:
            if current:
                lines.append(current)
            current = word
    if current:
        lines.append(current)
    return lines


def draw_text_block(
    draw: ImageDraw.ImageDraw,
    lines: list[str],
    x: int,
    y: int,
    fnt: ImageFont.FreeTypeFont,
    line_height: int,
    color=TEXT,
    highlight_last: bool = False,
) -> int:
    for index, line in enumerate(lines):
        fill = color
        if highlight_last and index == len(lines) - 1:
            fill = BLUE
        draw.text((x, y), line, font=fnt, fill=fill)
        y += line_height
    return y


def draw_gradient_text(
    base: Image.Image,
    text: str,
    xy: tuple[int, int],
    fnt: ImageFont.FreeTypeFont,
    colors=((96, 165, 250), (34, 211, 238), (52, 211, 153)),
) -> None:
    bbox = fnt.getbbox(text)
    width, height = bbox[2] - bbox[0], bbox[3] - bbox[1]
    if width <= 0 or height <= 0:
        return
    mask = Image.new("L", (width + 4, height + 4), 0)
    ImageDraw.Draw(mask).text((2 - bbox[0], 2 - bbox[1]), text, font=fnt, fill=255)

    grad = Image.new("RGB", (width + 4, height + 4), colors[0])
    gd = ImageDraw.Draw(grad)
    for gx in range(width + 4):
        t = gx / max(1, width + 3)
        if t < 0.5:
            local = t / 0.5
            a, b = colors[0], colors[1]
        else:
            local = (t - 0.5) / 0.5
            a, b = colors[1], colors[2]
        gd.line(
            [(gx, 0), (gx, height + 4)],
            fill=tuple(int(a[i] + (b[i] - a[i]) * local) for i in range(3)),
        )
    base.paste(grad, (xy[0] - 2, xy[1] - 2), mask)


def alpha_round_rect(base: Image.Image, box, radius: int, fill, outline=None, width: int = 1) -> None:
    """Draw a translucent rounded rectangle correctly on an RGBA canvas."""
    layer = Image.new("RGBA", base.size, (0, 0, 0, 0))
    ImageDraw.Draw(layer).rounded_rectangle(box, radius=radius, fill=fill, outline=outline, width=width)
    base.alpha_composite(layer)


def pill(
    base: Image.Image,
    xy: tuple[int, int],
    label: str,
    size: int = 13,
    fg=(167, 243, 208),
    border=(52, 211, 153, 90),
    fill=(52, 211, 153, 26),
    dot: bool = True,
) -> tuple[int, int]:
    draw = ImageDraw.Draw(base, "RGBA")
    fnt = font(FONT_BOLD, size)
    text_w = int(draw.textlength(label, font=fnt))
    pad_x, height = 14, size + 20
    width = text_w + pad_x * 2 + (14 if dot else 0)
    x, y = xy
    alpha_round_rect(base, (x, y, x + width, y + height), height // 2, fill, border, 1)
    if dot:
        cy = y + height / 2
        draw.ellipse((x + pad_x - 2, cy - 3, x + pad_x + 4, cy + 3), fill=GREEN)
    draw.text((x + pad_x + (12 if dot else 0), y + (height - size) / 2 - 1), label, font=fnt, fill=fg)
    return width, height


def paste_icon(base: Image.Image, xy: tuple[int, int], size: int, opacity: int = 255) -> None:
    icon = Image.open(ICON).convert("RGBA").resize((size, size), Image.LANCZOS)
    if opacity < 255:
        alpha = icon.getchannel("A").point(lambda v: int(v * opacity / 255))
        icon.putalpha(alpha)
    base.alpha_composite(icon, xy)


def brand_lockup(base: Image.Image, x: int, y: int, icon_size: int = 38, text_size: int = 20) -> None:
    paste_icon(base, (x, y), icon_size)
    draw = ImageDraw.Draw(base, "RGBA")
    draw.text((x + icon_size + 11, y + (icon_size - text_size) / 2 - 2), "RapidFetch", font=font(FONT_BOLD, text_size), fill=TEXT)


def shadow_glow(base: Image.Image, box: tuple[int, int, int, int], color=(59, 130, 246, 90), blur: int = 70) -> None:
    layer = Image.new("RGBA", base.size, (0, 0, 0, 0))
    draw = ImageDraw.Draw(layer, "RGBA")
    draw.ellipse(box, fill=color)
    base.alpha_composite(layer.filter(ImageFilter.GaussianBlur(blur)))


def screenshot_card(
    base: Image.Image,
    image_name: str,
    box: tuple[int, int, int, int],
    title: str,
    max_scale: float = 1.6,
    chrome_bar: bool = True,
) -> tuple[int, int, int, int]:
    image = Image.open(SHOTS / image_name).convert("RGB")
    left, top, right, bottom = box
    max_w, max_h = right - left, bottom - top
    pad = 7
    bar = 36 if chrome_bar else 0
    scale = min((max_w - pad * 2) / image.width, (max_h - bar - pad * 2) / image.height, max_scale)
    image = image.resize((max(1, int(image.width * scale)), max(1, int(image.height * scale))), Image.LANCZOS)

    card_w = image.width + pad * 2
    card_h = image.height + bar + pad * 2
    cx = (left + right) // 2
    cy = (top + bottom) // 2
    x = cx - card_w // 2
    y = cy - card_h // 2

    # Ambient glow and soft shadow.
    shadow_glow(base, (x - 40, y + 30, x + card_w + 40, y + card_h + 50), (59, 130, 246, 60), 70)
    shadow = Image.new("RGBA", base.size, (0, 0, 0, 0))
    sd = ImageDraw.Draw(shadow, "RGBA")
    sd.rounded_rectangle((x + 4, y + 16, x + card_w + 4, y + card_h + 16), radius=14, fill=(0, 0, 0, 150))
    base.alpha_composite(shadow.filter(ImageFilter.GaussianBlur(26)))

    draw = ImageDraw.Draw(base, "RGBA")
    draw.rounded_rectangle((x, y, x + card_w, y + card_h), radius=13, fill=(11, 18, 32, 255), outline=LINE, width=1)

    if chrome_bar:
        draw.rounded_rectangle((x, y, x + card_w, y + bar), radius=13, fill=(13, 21, 36, 255))
        draw.rectangle((x, y + bar - 13, x + card_w, y + bar), fill=(13, 21, 36, 255))
        draw.line((x, y + bar, x + card_w, y + bar), fill=LINE, width=1)
        for index, color in enumerate(((248, 113, 113), (251, 191, 36), (52, 211, 153))):
            cxx = x + 16 + index * 14
            draw.ellipse((cxx, y + 14, cxx + 8, y + 22), fill=color)
        title_font = font(FONT_REG, 12)
        tw = draw.textlength(title, font=title_font)
        draw.text((x + (card_w - tw) / 2, y + 12), title, font=title_font, fill=(120, 137, 160))

    # Image with rounded corners clipped under the title bar.
    mask = Image.new("L", image.size, 0)
    md = ImageDraw.Draw(mask)
    md.rounded_rectangle((0, 0, image.width - 1, image.height - 1), radius=9, fill=255)
    image_rgba = image.convert("RGBA")
    image_rgba.putalpha(mask)
    base.alpha_composite(image_rgba, (x + pad, y + bar + pad))
    draw.rounded_rectangle((x, y, x + card_w, y + card_h), radius=13, outline=LINE, width=1)
    return x, y, x + card_w, y + card_h


def store_slide(
    filename: str,
    image_name: str,
    eyebrow: str,
    headline: str,
    sub: str,
    card_title: str,
    slide_number: int,
    extra_note: str = "",
    head_size: int = 48,
) -> None:
    width, height = 1280, 800
    base = background(width, height)
    brand_lockup(base, 72, 42, 38, 20)

    draw = ImageDraw.Draw(base, "RGBA")
    url_font = font(FONT_REG, 13)
    draw.text((1208 - draw.textlength("rapidfetch.github.io", font=url_font), 54), "rapidfetch.github.io", font=url_font, fill=(110, 124, 146))

    pill(base, (72, 144), eyebrow, 12)

    head_font = font(FONT_BOLD, head_size)
    lines = wrap(draw, headline, head_font, 412)
    y = 190
    line_height = int(head_size * 1.15)
    for index, line in enumerate(lines):
        if index == len(lines) - 1:
            draw_gradient_text(base, line, (72, y), head_font)
        else:
            draw.text((72, y), line, font=head_font, fill=TEXT)
        y += line_height

    # Accent rule.
    rule = Image.new("RGB", (86, 4), BLUE)
    rd = ImageDraw.Draw(rule)
    for gx in range(86):
        t = gx / 85
        rd.line([(gx, 0), (gx, 4)], fill=(int(96 + (34 - 96) * t), int(165 + (211 - 165) * t), int(250 + (238 - 250) * t)))
    base.paste(rule, (72, y + 12))

    sub_font = font(FONT_REG, 20)
    sy = y + 38
    for line in wrap(draw, sub, sub_font, 402):
        draw.text((72, sy), line, font=sub_font, fill=MUTED)
        sy += 29

    note_font = font(FONT_REG, 13)
    if extra_note:
        draw.text((72, 744), extra_note, font=note_font, fill=(105, 120, 142))

    # Bottom benefits.
    w, h = pill(base, (72, 688), "FREE FOR LIFE", 12)
    draw.text((72 + w + 14, 688 + 7), "No trial · No subscription", font=font(FONT_REG, 13), fill=MUTED)

    screenshot_card(base, image_name, (492, 116, 1224, 690), card_title)

    # Slide counter, subtle.
    counter_font = font(FONT_SEMI, 13)
    label = f"{slide_number:02d} / 05"
    draw.text((1208 - draw.textlength(label, font=counter_font), 744), label, font=counter_font, fill=(105, 120, 142))

    base.convert("RGB").save(OUT / filename, "PNG", optimize=True)


def promo_tile() -> None:
    width, height = 440, 280
    base = background(width, height)
    paste_icon(base, (24, 22), 34)
    draw = ImageDraw.Draw(base, "RGBA")
    draw.text((68, 28), "RapidFetch", font=font(FONT_BOLD, 18), fill=TEXT)

    # Faint icon watermark replaced by a clear price mark.
    draw_gradient_text(base, "$0", (334, 148), font(FONT_BOLD, 70))
    draw.text((344, 230), "forever", font=font(FONT_REG, 14), fill=MUTED)

    head_font = font(FONT_BOLD, 25)
    y = 82
    for line in wrap(draw, "Free download manager for Windows & Chrome", head_font, 300):
        draw.text((24, y), line, font=head_font, fill=TEXT)
        y += 30

    draw.text((24, y + 8), "Segmented, resumable transfers.", font=font(FONT_REG, 14), fill=MUTED)
    pill(base, (24, 214), "FREE FOR LIFE", 11)
    draw.text((24, 246), "Windows 10/11 · Chrome extension", font=font(FONT_REG, 11.5), fill=(110, 124, 146))

    base.convert("RGB").save(OUT / "promo-440x280.png", "PNG", optimize=True)


def marquee() -> None:
    width, height = 1400, 560
    base = background(width, height)
    brand_lockup(base, 70, 40, 36, 19)

    draw = ImageDraw.Draw(base, "RGBA")
    pill(base, (70, 126), "FREE DOWNLOAD MANAGER", 12)

    head_font = font(FONT_BOLD, 51)
    y = 174
    for line in ("Downloads at full speed.", "Free for life."):
        if line.startswith("Free"):
            draw_gradient_text(base, line, (70, y), head_font)
        else:
            draw.text((70, y), line, font=head_font, fill=TEXT)
        y += 58

    sub_font = font(FONT_REG, 18)
    y += 8
    for line in wrap(draw, "Segmented, resumable transfers with native Chrome capture — no trial, subscription or account.", sub_font, 540):
        draw.text((70, y), line, font=sub_font, fill=MUTED)
        y += 27

    # CTA chips.
    draw.rounded_rectangle((70, 455, 240, 501), radius=9, fill=(59, 130, 246, 255))
    cta_font = font(FONT_SEMI, 14)
    draw.text((92, 468), "Download for Windows", font=cta_font, fill=(255, 255, 255))
    alpha_round_rect(base, (252, 455, 382, 501), 9, (255, 255, 255, 12), LINE, 1)
    draw.text((272, 468), "Add to Chrome", font=cta_font, fill=TEXT)

    screenshot_card(base, "main.png", (690, 78, 1336, 500), "RapidFetch — Downloads", chrome_bar=True)

    draw.text((70, 526), "Windows 10/11 · Chrome extension · v1.0.0", font=font(FONT_REG, 12), fill=(105, 120, 142))
    base.convert("RGB").save(OUT / "marquee-1400x560.png", "PNG", optimize=True)


def social_preview() -> None:
    width, height = 1280, 640
    base = background(width, height)
    brand_lockup(base, 76, 80, 76, 34)

    draw = ImageDraw.Draw(base, "RGBA")
    draw.text((76, 178), "Free download manager", font=font(FONT_BOLD, 46), fill=TEXT)
    draw_gradient_text(base, "for Windows & Chrome.", (76, 234), font(FONT_BOLD, 46))

    draw.text((76, 326), "Segmented, resumable transfers with native Chrome capture.", font=font(FONT_REG, 20), fill=MUTED)
    draw.text((76, 356), "Free for life — no trial, subscription or account.", font=font(FONT_REG, 20), fill=(167, 243, 208))

    pill(base, (76, 414), "FREE FOR LIFE", 12)
    draw.text((76, 500), "sohanur999999.github.io/rapidfetch", font=font(FONT_REG, 14), fill=(120, 137, 160))

    screenshot_card(base, "main.png", (690, 118, 1222, 522), "RapidFetch — Downloads")
    base.convert("RGB").save(OUT / "social-preview-1280x640.png", "PNG", optimize=True)


def main() -> None:
    OUT.mkdir(parents=True, exist_ok=True)

    # Clean source captures: drop the harness heading and the grey window matte.
    with Image.open(SHOTS / "media-bar.png") as image:
        image.convert("RGB").crop((0, 160, 1280, 900)).save(SHOTS / "media-bar-demo.png")
    with Image.open(SHOTS / "transfer.png") as image:
        image.convert("RGB").crop((22, 22, 516, 588)).save(SHOTS / "transfer-clean.png")

    store_slide(
        "01-desktop.png", "main.png", "FREE DOWNLOAD MANAGER",
        "Downloads that use your whole connection.",
        "Up to 32 segmented connections per file, with live speed, ETA and automatic retry.",
        "RapidFetch · Downloads", 1,
    )
    store_slide(
        "02-browser-capture.png", "media-bar-demo.png", "CHROME EXTENSION",
        "Every video on the page, in real formats.",
        "Detect video, audio and HLS streams in Chrome, then hand them to the desktop app.",
        "Chrome · RapidFetch panel", 2, head_size=42,
    )
    store_slide(
        "03-pause-resume.png", "transfer-clean.png", "RESUMABLE DOWNLOADS",
        "Pause. Close. Resume. Nothing restarts.",
        "Segment maps and .part files survive restarts, dropped Wi-Fi and sleep.",
        "Live transfer", 3,
    )
    store_slide(
        "04-queue-scheduler.png", "scheduler.png", "QUEUE & SCHEDULER",
        "Queue it now. Run it at 2 AM.",
        "Download windows, daily transfer caps and completion actions are built in.",
        "Scheduler", 4,
    )
    store_slide(
        "05-free-for-life.png", "add-download.png", "FREE FOR LIFE",
        "No trial. No key. No subscription.",
        "Every core feature ships unlocked — with no account and no activation server.",
        "Add download", 5,
    )

    promo_tile()
    marquee()
    social_preview()

    expected = {
        "01-desktop.png": (1280, 800),
        "02-browser-capture.png": (1280, 800),
        "03-pause-resume.png": (1280, 800),
        "04-queue-scheduler.png": (1280, 800),
        "05-free-for-life.png": (1280, 800),
        "promo-440x280.png": (440, 280),
        "marquee-1400x560.png": (1400, 560),
        "social-preview-1280x640.png": (1280, 640),
    }
    for name, size in expected.items():
        with Image.open(OUT / name) as image:
            assert image.size == size, f"{name}: {image.size} != {size}"
        print(f"{name:34s} {size[0]}x{size[1]}  {(OUT / name).stat().st_size / 1024:.1f} KB")


if __name__ == "__main__":
    main()
