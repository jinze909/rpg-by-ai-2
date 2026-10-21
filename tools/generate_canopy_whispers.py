#!/usr/bin/env python3
"""Original loopable eight-pose dapple/leaf pixel overlays for mature tree canopies.

Every frame has the same fixed transparent 32px footprint. The foliage shifts
in small periodic arcs, never slices or resizes the original Unity WorldProps.
Pillow is the pixel painter; this does not invoke an AI image API.
"""
import math
from pathlib import Path
from PIL import Image, ImageDraw

CELL = 32
FRAMES = 8
# Deciduous green, coastal palms, snowpine: distinct authored pigments.
PALETTES = [
    ((37,75,45,166),(59,110,56,202),(103,159,77,222),(178,205,117,213)),
    ((22,85,75,165),(37,139,106,203),(101,185,132,223),(207,203,114,211)),
    ((55,91,110,157),(102,143,162,198),(179,215,223,216),(232,245,237,236)),
]
# Per-leaf centers: offset phases for a seamless 8-pose flutter loop.
LEAVES = [
    (6,9,0,.9),(13,5,2,1.6),(23,9,4,1.2),
    (9,19,3,.8),(20,20,1,1.4),(25,15,5,.7),
]

def paint_leaf(draw, x, y, phase, palette, index, snow=False):
    light = palette[2+(index%2)]
    base = palette[1+(index%2)]
    dark = palette[0]
    if snow:
        # Broad, separate cold needles/crystals, not tinted summer leaves.
        draw.line((x-2,y,x+2,y),fill=base,width=1)
        draw.line((x,y-2,x,y+2),fill=light,width=1)
        draw.point((x,y),fill=palette[3])
        if index%2==0:
            draw.point((x-1,y-1),fill=dark)
            draw.point((x+1,y+1),fill=base)
        return
    mirrored=(index+phase//2)%2
    if mirrored:
        # Hand-shaped swept leaves: 1px veins follow the blade direction.
        draw.polygon([(x-3,y),(x-1,y-2),(x+2,y-2),(x+3,y),
                      (x+1,y+2),(x-2,y+1)],fill=dark)
        draw.polygon([(x-2,y),(x,y-2),(x+2,y-1),(x+1,y+1)],fill=base)
        draw.line((x-2,y,x+1,y),fill=light)
    else:
        draw.polygon([(x,y-3),(x+2,y-2),(x+2,y+1),(x,y+3),
                      (x-2,y+1),(x-2,y-2)],fill=dark)
        draw.polygon([(x,y-2),(x+1,y-1),(x+1,y+1),(x,y+2),
                      (x-1,y)],fill=base)
        draw.line((x,y-2,x,y+1),fill=light)

def build():
    sheet=Image.new('RGBA',(CELL*FRAMES,CELL*len(PALETTES)),(0,0,0,0))
    for biome,palette in enumerate(PALETTES):
        for frame in range(FRAMES):
            canvas=Image.new('RGBA',(CELL,CELL),(0,0,0,0))
            pen=ImageDraw.Draw(canvas)
            for idx,(cx,cy,phase,amplitude) in enumerate(LEAVES):
                theta=math.tau*(frame/FRAMES)+(phase*math.pi/3)
                dx=round(math.sin(theta)*amplitude)
                dy=round(math.cos(theta)*.9)
                paint_leaf(pen,cx+dx,cy+dy,frame,palette,idx,biome==2)
            # A softly painted visual center provides living canopy glints.
            # Three scattered one-pixel lights, all still anchored in the 32px cell.
            for n,(px,py) in enumerate(((15,13),(5,25),(24,26))):
                theta=math.tau*(frame/FRAMES+n/3)
                shift=round(math.sin(theta)*2)
                pen.point((px+shift,py),fill=palette[2 if n!=1 else 1])
            sheet.paste(canvas,(frame*CELL,biome*CELL))
    return sheet

if __name__=='__main__':
    import sys
    output=Path(sys.argv[1]) if len(sys.argv)>1 else Path(__file__).resolve().parents[1]/'Assets/Resources/Frontier/Art/CanopyWhispers.png'
    output.parent.mkdir(parents=True,exist_ok=True)
    build().save(output,optimize=True)
    print('PASS 24 original 32px rooted canopy overlay cells:',output)
