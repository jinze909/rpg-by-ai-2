#!/usr/bin/env python3
"""Build four hand-directed transparent 24px relic light poses.
Only a supplemental visual accent: never replaces accepted FieldProps art.
"""
from pathlib import Path
from PIL import Image, ImageDraw

CELL = 24
output = Image.new("RGBA", (CELL * 4, CELL), (0, 0, 0, 0))
outline = (21, 43, 78, 146)
blue = (48, 135, 169, 160)
cyan = (111, 229, 242, 226)
ice = (201, 255, 244, 250)
white = (252, 255, 241, 255)
profiles = [
    ((12, 12), (1, 2, 3, 2), (-2, 1, 0)),
    ((12, 11), (2, 3, 2, 3), (1, -2, 0)),
    ((12, 12), (3, 2, 4, 2), (-1, 2, 1)),
    ((12, 13), (2, 4, 2, 1), (2, -1, 0)),
]
for f, (center, lengths, drift) in enumerate(profiles):
    tile = Image.new("RGBA", (CELL, CELL), (0, 0, 0, 0))
    d = ImageDraw.Draw(tile)
    cx, cy = center
    d.polygon([(cx, cy-5), (cx+3,cy-1), (cx+2,cy+3),
               (cx,cy+5),(cx-3,cy+1),(cx-2,cy-3)], fill=outline)
    d.polygon([(cx,cy-4),(cx+2,cy-1),(cx+1,cy+3),
               (cx,cy+4),(cx-2,cy+1),(cx-1,cy-3)], fill=blue)
    d.polygon([(cx,cy-3),(cx+1,cy),(cx,cy+3),(cx-1,cy)], fill=cyan)
    d.point((cx,cy-1), fill=white)
    north, east, south, west = lengths
    d.line([(cx,cy-5),(cx,cy-5-north)],fill=cyan)
    d.point((cx,cy-6-north),fill=ice)
    d.line([(cx+4,cy),(cx+4+east,cy)],fill=cyan)
    d.point((cx+5+east,cy),fill=ice)
    d.line([(cx,cy+5),(cx,cy+5+south)],fill=blue)
    d.point((cx,cy+6+south),fill=cyan)
    d.line([(cx-4,cy),(cx-4-west,cy)],fill=cyan)
    d.point((cx-5-west,cy),fill=ice)
    d.line([(cx+drift[0]+3,cy-4+drift[1]),
            (cx+drift[0]+4,cy-5+drift[1])],fill=ice)
    d.point((cx-7+drift[0]//2, cy+5+drift[2]), fill=blue)
    output.paste(tile,(f*CELL,0))
out=Path(__file__).resolve().parents[1]/"Assets/Resources/Frontier/Art/RelicShimmer.png"
out.parent.mkdir(parents=True,exist_ok=True)
output.save(out,optimize=True)
print("Painted 4 unique translucent relic shimmer poses:",out)
