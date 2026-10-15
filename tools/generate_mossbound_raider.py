#!/usr/bin/env python3
"""24 independently posed, originally pixel-painted Mossbound raider frames.
Pillow drawing, NOT an image-generation API, skeleton deformation, or source-art recolor.
The 128px canvas shares the existing monster foot at (64,120).
"""
from pathlib import Path
from PIL import Image, ImageDraw
import argparse

ROOT=Path(__file__).resolve().parents[1]
STAGED=ROOT/".starfall-checks/phase2-mossbound"
INSTALLED=ROOT/"Assets/Resources/Frontier/Art/MossboundRaider.png"
CELL=128
PHASES=("Idle","Walk","Windup","Attack","Hit","Death")
INK="#17283b"
STONE="#52655a"
MOSS="#607b53"
LIT="#88a56e"
BARK="#644939"
WOOD="#85614d"
TEAL="#78b6a6"
GOLD="#d8b875"
# Hand-authored keypose topology (lean, rise, left foot, right foot, arm,
# weapon x/y, eyelid). Distinct action-specific silhouettes in every slot.
KEYS={
 "Idle": [(0,0,-3,3,0,0,0,0),(1,-1,-3,3,1,1,0,0),
          (0,0,-3,3,-1,0,0,1),(-1,1,-3,3,-2,-1,1,0)],
 "Walk": [(-2,0,-7,1,3,0,0,0),(0,-2,-4,5,1,1,-1,0),
          (2,0,-1,7,-2,2,0,0),(0,-2,-5,4,-1,0,-1,0)],
 "Windup": [(-2,0,-5,5,-2,-3,-2,0),(-3,0,-6,5,-4,-5,-7,0),
             (-4,-1,-6,6,-6,-7,-10,0),(-5,-1,-7,6,-7,-8,-12,0)],
 "Attack": [(0,-2,-5,6,-6,-5,-10,0),(4,-2,-7,6,-7,3,-8,0),
             (6,0,-8,7,-5,7,0,0),(3,0,-4,6,-4,3,3,0)],
 "Hit": [(-4,1,-6,3,-8,-2,2,1),(-7,2,-7,4,-9,-4,5,1),
          (-5,1,-6,4,-5,-2,3,0),(-2,0,-4,3,-3,0,2,0)],
 "Death": [(-8,3,-8,7,-9,-2,4,1),(-10,9,-9,8,-10,-2,9,1),
            (-12,16,-9,9,-9,-3,14,1),(-10,20,-9,10,-9,-2,16,1)],
}
def pose(action, column):
    lean,rise,lfoot,rfoot,arm,axe_x,axe_y,blink=KEYS[action][column]
    im=Image.new("RGBA",(64,64))
    d=ImageDraw.Draw(im)
    cx=32+lean
    fall=action=="Death" and column>=2
    top=39+(column-2)*3 if fall else 24+rise+(4 if action=="Death" else 0)
    middle=49+(column-2)*2 if fall else 44+rise
    base=54+(column-2) if fall else 50+rise
    d.ellipse((19,56,46,61),fill=(18,29,39,65))
    # No boot overlays or head grafts: draw one fully connected keypose.
    feet=(18,47) if fall else (25+lfoot//2,41+rfoot//2)
    for foot,shift in ((feet[0],0),(feet[1],1)):
        d.polygon(((foot-5,base-3),(foot+4,base-3),
            (foot+5,58-shift),(foot-6,59-shift)),fill=INK)
        d.polygon(((foot-5,55-shift),(foot+3,55-shift),
            (foot+7,59-shift),(foot-7,59-shift)),fill=WOOD)
        d.line((foot-6,58-shift,foot+5,58-shift),fill=LIT)
    ax,ay=(15,49) if fall else (19+lean//2+arm,37+rise+arm//2)
    d.polygon(((cx-8,top+9),(cx-15,top+11),
        (ax-5,ay),(ax,ay+6),(cx-6,middle)),fill=INK)
    d.polygon(((cx-11,top+10),(cx-13,top+13),
        (ax-2,ay+2),(ax+1,ay+4),(cx-6,middle-3)),fill=MOSS)
    d.polygon(((cx-9,top+4),(cx+8,top+3),(cx+12,top+12),
        (cx+11,middle),(cx+3,base),(cx-9,base-1),
        (cx-13,middle-6)),fill=INK)
    d.polygon(((cx-8,top+7),(cx+6,top+5),(cx+9,top+15),
        (cx+8,middle-1),(cx+1,base-3),
        (cx-7,base-3),(cx-10,middle-7)),fill=STONE)
    d.polygon(((cx-8,top+11),(cx-2,top+7),(cx+4,top+16),
        (cx,middle-5),(cx-7,middle-7)),fill=MOSS)
    d.polygon(((cx+4,top+7),(cx+8,top+13),
        (cx+7,middle-7),(cx+2,middle-3),(cx-1,middle-10)),
        fill="#35454b")
    for dx,dy in ((-7,18),(1,21),(6,29)):
        if top+dy<56:
            d.polygon(((cx+dx-3,top+dy),(cx+dx+2,top+dy-3),
                (cx+dx+5,top+dy),(cx+dx+1,top+dy+5)),fill=LIT)
    d.line((cx-2,top+14,cx+4,top+22),fill=INK,width=2)
    hx,hy=(46,52) if fall else (min(49,48+axe_x),38+rise+axe_y)
    d.polygon(((cx+7,top+9),(cx+12,top+9),
        (hx+2,hy),(hx-3,hy+3),(cx+8,middle-5)),fill=INK)
    d.polygon(((cx+9,top+12),(cx+12,top+12),
        (hx,hy+1),(hx-1,hy+3),(cx+8,middle-7)),fill=WOOD)
    d.line((hx-1,hy+3,hx+3,hy-12),fill=BARK,width=4)
    d.line((hx,hy+2,hx+3,hy-12),fill="#a98461",width=2)
    d.polygon(((hx+3,hy-13),(hx+10,hy-14),(hx+12,hy-9),
        (hx+6,hy-6),(hx+1,hy-8)),fill=INK)
    d.polygon(((hx+4,hy-12),(hx+9,hy-12),(hx+10,hy-10),
        (hx+6,hy-7),(hx+3,hy-9)),fill=TEAL)
    head=38+(column-2)*3 if fall else 17+rise+(2 if action=="Death" else 0)
    d.rectangle((cx-5,head+8,cx+6,top+10),fill=INK)
    d.polygon(((cx-9,head+2),(cx-4,head-4),
        (cx+6,head-4),(cx+11,head+2),
        (cx+9,head+12),(cx-7,head+12)),fill=INK)
    d.polygon(((cx-8,head+1),(cx-4,head-3),
        (cx+5,head-2),(cx+9,head+2),(cx+7,head+11),
        (cx-6,head+9)),fill=MOSS)
    d.polygon(((cx-5,head-2),(cx,head-6),(cx+5,head-4),
        (cx+8,head+1),(cx+2,head)),fill=LIT)
    d.polygon(((cx-8,head+1),(cx-11,head-8),(cx-4,head-3)),
        fill="#293b43")
    d.polygon(((cx+1,head-4),(cx+4,head-12),
        (cx+7,head-3)),fill=STONE)
    d.polygon(((cx+7,head+2),(cx+12,head-8),
        (cx+10,head+6)),fill=LIT)
    if blink:
        d.line((cx-5,head+5,cx-2,head+6),fill=INK)
        d.line((cx+3,head+6,cx+6,head+5),fill=INK)
    else:
        d.rectangle((cx-5,head+4,cx-2,head+5),fill=GOLD)
        d.rectangle((cx+3,head+4,cx+6,head+5),fill=GOLD)
        d.point((cx-3,head+4),fill="#f2ead7")
        d.point((cx+5,head+4),fill="#f2ead7")
    d.line((cx-1,head+9,cx+3,head+9),fill=BARK)
    if action=="Hit" and column<=1:
        d.polygon(((cx-13,top+9),(cx-18,top+6),
            (cx-16,top+13)),fill="#bd715f")
    return im.resize((CELL,CELL),Image.Resampling.NEAREST)

def render(dest):
    atlas=Image.new("RGBA",(CELL*4,CELL*6))
    for row,action in enumerate(PHASES):
        for col in range(4):
            atlas.paste(pose(action,col),(col*CELL,row*CELL))
    dest.parent.mkdir(parents=True,exist_ok=True)
    atlas.save(dest,optimize=True)

if __name__=="__main__":
    parser=argparse.ArgumentParser()
    parser.add_argument("--install",action="store_true")
    args=parser.parse_args()
    render(INSTALLED if args.install else STAGED/"MossboundRaider.png")
    print("Generated 24 original whole-body Mossbound raider frames")
