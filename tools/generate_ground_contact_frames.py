#!/usr/bin/env python3
"""Reproducible source paintings: five native-pixel ground materials x four steps."""
from pathlib import Path
from PIL import Image, ImageDraw
import sys

CELL_W, CELL_H = 32, 20
MATERIALS = ("grass", "stone", "sand", "snow", "wood")
PALETTES = (
    ((41, 85, 53, 120), (94, 147, 79, 191), (174, 194, 108, 224)),
    ((60, 72, 84, 112), (137, 146, 154, 179), (205, 203, 176, 210)),
    ((114, 83, 48, 100), (208, 160, 92, 178), (246, 211, 138, 210)),
    ((53, 109, 143, 103), (137, 199, 218, 188), (225, 244, 247, 220)),
    ((99, 62, 46, 103), (172, 113, 70, 186), (233, 173, 109, 219)),
)

def cell(material: int, frame: int) -> Image.Image:
    image = Image.new("RGBA", (CELL_W, CELL_H))
    draw = ImageDraw.Draw(image)
    base, mid, bright = PALETTES[material]
    reach = (6, 9, 11, 13)[frame]
    ground_y = 14
    for side in (-1, 1):
        inner = (2, 4, 6, 9)[frame]
        x0 = 16 + side * inner
        x1 = 16 + side * reach
        draw.line((x0, ground_y, x1, ground_y - (frame % 2)), fill=mid)
        draw.point((x1, ground_y - 1 - (frame % 2)), fill=bright)
        if frame <= 2:
            draw.line((x0, ground_y + 1, x1 - side, ground_y + 1), fill=base)
    scatter = (
        ((-5, -1), (5, -2), (-8, -3), (7, -1)),
        ((-7, -2), (8, -4), (-10, -4), (11, -2)),
        ((-10, -4), (10, -6), (-12, -7), (13, -4)),
        ((-12, -6), (13, -8), (-13, -9), (12, -5)),
    )[frame]
    for k, (dx, dy) in enumerate(scatter):
        x, y = 16 + dx, ground_y + dy
        if material == 0:
            draw.line((x - 1, y + 1, x, y - 1), fill=mid)
            draw.line((x, y - 1, x + (1 if k % 2 else -1), y - 2), fill=bright)
            draw.point((x + 1, y + 1), fill=base)
        elif material == 1:
            draw.rectangle((x - 1, y, x, y + 1), fill=mid)
            draw.point((x + 1, y), fill=bright)
            draw.point((x - 1, y - 1), fill=base)
        elif material == 2:
            draw.point((x - 1, y), fill=mid)
            draw.point((x + 1, y - 1), fill=bright)
            draw.point((x, y + 1), fill=mid)
            draw.point((x - 2, y + 1), fill=base)
        elif material == 3:
            draw.line((x - 1, y, x + 1, y), fill=mid)
            draw.line((x, y - 1, x, y + 1), fill=bright)
            draw.point((x - 1, y + 1), fill=base)
        else:
            draw.line((x - 2, y, x + 1, y), fill=mid)
            draw.point((x, y - 1), fill=bright)
            draw.point((x + 2, y + 1), fill=base)
    if frame == 0:
        draw.line((13, 15, 19, 15), fill=base)
        draw.point((16, 13), fill=bright)
    elif frame == 1:
        draw.point((14, 13), fill=bright)
        draw.point((18, 13), fill=bright)
    elif frame == 2:
        draw.point((12, 15), fill=base)
        draw.point((20, 15), fill=base)
    else:
        draw.point((10, 14), fill=base)
        draw.point((22, 14), fill=base)
    return image

def build() -> Image.Image:
    atlas = Image.new("RGBA", (CELL_W * 4, CELL_H * len(MATERIALS)))
    for material in range(len(MATERIALS)):
        for frame in range(4):
            atlas.paste(cell(material, frame),
                        (frame * CELL_W, material * CELL_H))
    return atlas

if __name__ == "__main__":
    target = Path(sys.argv[1]) if len(sys.argv) > 1 else (
        Path(__file__).resolve().parents[1] /
        "Assets/Resources/Frontier/Art/GroundContactFrames.png")
    target.parent.mkdir(parents=True, exist_ok=True)
    build().save(target, optimize=True)
    print(target)
