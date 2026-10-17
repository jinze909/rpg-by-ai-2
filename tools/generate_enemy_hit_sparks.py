#!/usr/bin/env python3
"""Source-painted 3 material groups x 4 hit-reaction frames, 24px lossless cells."""
from pathlib import Path
from PIL import Image, ImageDraw
import sys

COLORS=[
    [(64,98,73,134),(113,159,102,194),(205,218,141,225)],
    [(114,60,44,126),(222,113,55,192),(255,213,117,223)],
    [(45,91,126,129),(97,176,210,199),(182,240,244,230)]
]

def cell(material,frame):
    out=Image.new('RGBA',(24,24))
    p=out.load()
    d=ImageDraw.Draw(out)
    shades=COLORS[material]
    # Carefully placed whole fragments with a different phase arc.
    spokes=[(-7,-1),(-6,-6),(-2,-8),(5,-7),
            (8,-2),(6,5),(0,8),(-7,6)]
    drift=[(0,0),(1,-1),(2,-2),(3,-1)][frame]
    for k,(dx,dy) in enumerate(spokes):
        if (frame+k)%5==0:
            continue
        ex=12+dx+(-1 if k%2 else 1)*drift[0]
        ey=12+dy-(1 if k%2 else -1)*drift[1]
        if not(2<=ex<22 and 2<=ey<22):
            continue
        rgb=shades[(k+frame)%3]
        if material==0:
            # Leaf shards are open three-pixel V silhouettes.
            d.line([(ex-1,ey+1),(ex,ey),(ex+1,ey-1)],
                   fill=rgb,width=1)
            if k%3==0:
                d.point((ex+1,ey+1),fill=shades[0])
        elif material==1:
            # Sharp vertical ember streaks recede into ash.
            d.line((ex,ey+1,ex+(k%2),ey-2),fill=rgb,width=1)
            if k%2==1:
                d.point((ex-1,ey),fill=shades[0])
        else:
            # Orthogonal blue/cyan crystal glints, not sparks of fire.
            d.line((ex-1,ey,ex+1,ey),fill=rgb,width=1)
            d.line((ex,ey-1,ex,ey+1),fill=shades[2],width=1)
    for j,(x,y) in enumerate([(11,11),(13,12),(12,14),(10,13)]):
        if (j+frame)%3!=0:
            p[x,y]=shades[(j+material)%3]
    return out

def build():
    sheet=Image.new('RGBA',(96,72))
    for material in range(3):
        for frame in range(4):
            sheet.paste(cell(material,frame),
                        (24*frame,24*material))
    return sheet

if __name__=='__main__':
    target=Path(sys.argv[1]) if len(sys.argv)>1 else (
        Path(__file__).resolve().parents[1] /
        'Assets/Resources/Frontier/Art/EnemyHitSparks.png')
    target.parent.mkdir(parents=True,exist_ok=True)
    build().save(target,optimize=True)
    print(target)
