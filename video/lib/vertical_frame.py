#!/usr/bin/env python3
"""Draws the 1080x1920 frame of a vertical clip (STIL.md "Vertikalni klip"): logo, title, lines, message.

    vertical_frame.py out.png logo.png "Naslov 1" "Naslov 2" "Poruka 1" "Poruka 2" [phone]

With "phone" it draws the frame for the app's mobile layout (STIL.md "Klip sa telefona") and, next to
out.png, out-rub.png: the screen's rounded corners and edge, laid over the video.
"""
import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

FONT_FILES = Path(__file__).resolve().parents[2] / "src/web/node_modules/@fontsource/ibm-plex-sans/files"
BG, LINE, TEXT, MUTED, ACCENT = "#11130E", "#2C3125", "#E4E7DC", "#9BA18F", "#B4C17E"
# The phone's screen in the frame; its size is record.js's PHONE window at 2x.
SCREEN = (150, 400, 930, 1560)
SCREEN_RADIUS = 40
SMALL_PRINT = "Besplatno dok traje beta · bez kartice"


class Face:
    """The font comes split in two subsets, and Serbian letters (č, ć, š, ž, đ) are in the second one."""

    def __init__(self, weight, size):
        self.latin = ImageFont.truetype(FONT_FILES / f"ibm-plex-sans-latin-{weight}-normal.woff", size)
        self.ext = ImageFont.truetype(FONT_FILES / f"ibm-plex-sans-latin-ext-{weight}-normal.woff", size)
        self.ext_chars = set("čćšžđČĆŠŽĐ")

    def runs(self, text):
        run, font = "", None
        for char in text:
            wanted = self.ext if char in self.ext_chars else self.latin
            if font is not None and wanted is not font:
                yield run, font
                run = ""
            run, font = run + char, wanted
        if run:
            yield run, font

    def draw(self, canvas, x, y, text, fill):
        for run, font in self.runs(text):
            canvas.text((x, y), run, font=font, fill=fill)
            x += font.getlength(run)


def paste_logo(image, logo, x, y, width):
    mark = Image.open(logo).convert("RGBA")
    mark = mark.resize((width, round(mark.height * width / mark.width)), Image.LANCZOS)
    image.paste(mark, (x, y), mark)


def phone(out, logo, title1, title2, message1, message2):
    """Smaller title and message, so the phone's screen (780x1160) gets most of the height."""
    image = Image.new("RGB", (1080, 1920), BG)
    canvas = ImageDraw.Draw(image)
    paste_logo(image, logo, 80, 110, 220)

    title = Face(600, 64)
    title.draw(canvas, 80, 190, title1, TEXT)
    title.draw(canvas, 80, 268, title2, ACCENT)

    message = Face(600, 48)
    message.draw(canvas, 80, 1610, message1, TEXT)
    message.draw(canvas, 80, 1672, message2, TEXT)
    Face(500, 32).draw(canvas, 80, 1770, SMALL_PRINT, MUTED)
    image.save(out)

    # Drawn at 4x and scaled down, so the rounded corners have smooth edges.
    scale = 4
    edge = Image.new("RGBA", (1080 * scale, 1920 * scale), (0, 0, 0, 0))
    # Background only in the screen's corners: its rectangle minus its rounded shape.
    corners = Image.new("L", edge.size, 0)
    box = tuple(v * scale for v in SCREEN)
    ImageDraw.Draw(corners).rectangle(box, fill=255)
    ImageDraw.Draw(corners).rounded_rectangle(box, SCREEN_RADIUS * scale, fill=0)
    edge.paste(Image.new("RGBA", edge.size, BG), (0, 0), corners)
    ring = tuple(v * scale for v in (SCREEN[0] - 2, SCREEN[1] - 2, SCREEN[2] + 2, SCREEN[3] + 2))
    ImageDraw.Draw(edge).rounded_rectangle(ring, (SCREEN_RADIUS + 2) * scale, outline=LINE, width=3 * scale)
    edge.resize((1080, 1920), Image.LANCZOS).save(out.replace(".png", "-rub.png"))


def main(out, logo, title1, title2, message1, message2):
    image = Image.new("RGB", (1080, 1920), BG)
    canvas = ImageDraw.Draw(image)
    paste_logo(image, logo, 80, 160, 280)

    title = Face(600, 76)
    title.draw(canvas, 80, 290, title1, TEXT)
    title.draw(canvas, 80, 380, title2, ACCENT)

    # The video itself goes between these two lines.
    canvas.rectangle((0, 554, 1079, 554), fill=LINE)
    canvas.rectangle((0, 1365, 1079, 1365), fill=LINE)

    message = Face(600, 56)
    message.draw(canvas, 80, 1450, message1, TEXT)
    message.draw(canvas, 80, 1525, message2, TEXT)
    Face(500, 36).draw(canvas, 80, 1650, SMALL_PRINT, MUTED)

    image.save(out)


if __name__ == "__main__":
    (phone if sys.argv[7:8] == ["phone"] else main)(*sys.argv[1:7])
