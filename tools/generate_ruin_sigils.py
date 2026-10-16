#!/usr/bin/env python3
"""Original 4-pose enchanted stone-rune glyphs in cold and warm ruin palettes.

Four contiguous 32px authored cells per biome, fully transparent. This is
environment illumination layered over existing accepted masonry pixels, never
a replacement for the ruin structure or for any actor/action animation.
"""
from pathlib import Path
from PIL import Image, ImageDraw
import math
import sys

def build():
    sheet = Image.new("RGBA", (128, 64), (0,0,0,0))
    palettes = [
        [(27,74,91,148),(44,143,169,176),(112,218,216,205),(206,247,222,229)],
        [(79,53,44,140),(173,91,59,182),(232,166,87,209),(251,228,157,231)],
    ]
    for row, palette in enumerate(palettes):
        for frame in range(4):
            pose=Image.new("RGBA",(32,32),(0,0,0,0))
            d=ImageDraw.Draw(pose)
            wobble=math.sin(math.tau*frame/4)
            # Interrupted carved ring; exposed chipped stone/metal fragments.
            for start,finish,ink in [(186,242,1),(260,322,2),(12,82,3),(105,152,1)]:
                d.arc((6,6,25,25),start+frame*3,finish+frame*3,fill=palette[ink])
            # Angled intact glyph, not a circle/blur pretending to be architecture.
            d.line([(11,19),(16,10),(20,17),(16,24)],fill=palette[1],width=2)
            d.line([(12,19),(20,17)],fill=palette[2],width=1)
            d.line([(16,10),(16,21)],fill=palette[3],width=1)
            d.point((14,15),fill=palette[0])
            sparks=[(4,13),(27,12),(9,28),(24,27)]
            for index,(x,y) in enumerate(sparks):
                dx=int(round(math.sin(math.tau*frame/4+index*1.57)))
                dy=int(round(math.cos(math.tau*frame/4+index*.88)))
                ink=palette[2+(index%2)]
                d.point((x+dx,y+dy),fill=ink)
                if (frame+index)%3==0:
                    d.line((x+dx-1,y+dy,x+dx+1,y+dy),fill=palette[1])
            d.polygon([(15,14),(17,15),(16,18),(14,17)],fill=palette[(frame%2)+1])
            d.point((15,15),fill=palette[3])
            sheet.paste(pose,(frame*32,row*32))
    return sheet

if __name__=="__main__":
    output=(Path(sys.argv[1]) if len(sys.argv)>1 else
        Path(__file__).resolve().parents[1]/"Assets/Resources/Frontier/Art/RuinSigils.png")
    output.parent.mkdir(parents=True,exist_ok=True)
    build().save(output,optimize=True)
    print("PASS original 8 painted ruin-sigil cells:",output)
