"""Render the original 128x128 NuGet package icon (build/icon.png).

The motif is abstract: a rounded workspace containing a tool pane, a tabbed
document pane and a bottom pane, with a small docking-target cross. Shapes are
drawn at 4x and downsampled so edges stay smooth. Requires Pillow.

    python3 tools/generate-package-icon.py [output]
"""
import pathlib
import sys

from PIL import Image, ImageDraw

SIZE = 128
SCALE = 4
BACKGROUND = (38, 50, 92, 255)
FRAME = (58, 74, 128, 255)
TOOL = (104, 117, 217, 255)
DOCUMENT = (236, 240, 250, 255)
TAB_ACTIVE = (236, 240, 250, 255)
TAB_INACTIVE = (150, 162, 214, 255)
BOTTOM = (82, 196, 190, 255)
GUIDE = (255, 184, 76, 255)


def box(x0, y0, x1, y1):
    return [x0 * SCALE, y0 * SCALE, x1 * SCALE - 1, y1 * SCALE - 1]


def render():
    canvas = Image.new('RGBA', (SIZE * SCALE, SIZE * SCALE), (0, 0, 0, 0))
    draw = ImageDraw.Draw(canvas)
    draw.rounded_rectangle(box(4, 4, 124, 124), radius=22 * SCALE, fill=BACKGROUND)
    draw.rounded_rectangle(box(14, 14, 114, 114), radius=8 * SCALE, fill=FRAME)

    # Tool pane on the left.
    draw.rounded_rectangle(box(20, 20, 46, 108), radius=4 * SCALE, fill=TOOL)
    for top in (30, 40, 50):
        draw.rounded_rectangle(box(25, top, 41, top + 4), radius=2 * SCALE, fill=(170, 180, 240, 255))

    # Document tabs and body.
    draw.rounded_rectangle(box(52, 20, 76, 32), radius=3 * SCALE, fill=TAB_ACTIVE)
    draw.rounded_rectangle(box(79, 22, 101, 32), radius=3 * SCALE, fill=TAB_INACTIVE)
    draw.rectangle(box(52, 28, 108, 32), fill=TAB_ACTIVE)
    draw.rounded_rectangle(box(52, 28, 108, 76), radius=4 * SCALE, fill=DOCUMENT)

    # Bottom pane.
    draw.rounded_rectangle(box(52, 82, 108, 108), radius=4 * SCALE, fill=BOTTOM)

    # Docking-target cross centered in the document pane.
    cx, cy, arm, width = 80, 52, 13, 7
    for x0, y0, x1, y1 in ((cx - width / 2, cy - arm, cx + width / 2, cy + arm),
                           (cx - arm, cy - width / 2, cx + arm, cy + width / 2)):
        draw.rounded_rectangle(box(x0, y0, x1, y1), radius=2 * SCALE, fill=GUIDE)
    draw.rounded_rectangle(box(cx - 5, cy - 5, cx + 5, cy + 5), radius=2 * SCALE, fill=BACKGROUND)
    draw.rounded_rectangle(box(cx - 3, cy - 3, cx + 3, cy + 3), radius=1 * SCALE, fill=GUIDE)
    return canvas.resize((SIZE, SIZE), Image.Resampling.LANCZOS)


def main():
    root = pathlib.Path(__file__).resolve().parents[1]
    output = pathlib.Path(sys.argv[1]) if len(sys.argv) > 1 else root / 'build/icon.png'
    output.parent.mkdir(parents=True, exist_ok=True)
    render().save(output, format='PNG', optimize=True)
    print(f'Wrote {output}')


if __name__ == '__main__':
    main()
