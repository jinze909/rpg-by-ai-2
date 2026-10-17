#!/usr/bin/env python3
"""Source-authored original ground-contact wisps for real enemy attack release.

3 distinct regional/material styles x 4 complete 48x24 transparent pixel cells;
this paints *under* whole-body monsters, never replaces any actor animation.
"""
from pathlib import Path
from PIL import Image, ImageDraw
import math, sys
CELL_W,CELL_H=48,24
PALETTES = [
    ((74,89,69),(134,145,96),(201,182,116)),  # mossbound soil
    ((98,61,55),(186,94,53),(244,170,77)),   # cinder grit
    ((42,83,109),(72,155,188),(146,220,226)) # arcane cold ash
]

def color(rgb,opacity): return tuple(rgb)+(opacity,)
def draw_cell(row,phase):
    canvas=Image.new('RGBA',(CELL_W,CELL_H),(0,0,0,0))
    d=ImageDraw.Draw(canvas)
    dark,mid,glow=PALETTES[row]
    half=[7,12,17,20][phase]
    top=[13,12,11,10][phase]
    # Two interrupted radial ground-rim strokes: leave room for the actors'
    # visible boots and never turn the hit into a fabricated AoE hitbox.
    for side in (-1,1):
        left=24+side*half
        curve=[(24+side*2,20),(24+side*(half-3),19-phase//2),
               (left,top+phase//2)]
        d.line(curve,fill=color(dark,137-phase*17),width=2)
        d.line(curve[1:],fill=color(mid,172-phase*22),width=1)
        d.point((left,top+phase//2),fill=color(glow,198-phase*23))
    # Grounded ribbons are hand adjusted for each row and time slice.
    inner=[(-8,19),(-4,18),(0,19),(4,18),(8,19)]
    for k,(x,y) in enumerate(inner):
        if (k+phase)%3==0:continue
        px=24+x+(phase-1)*(1 if x>0 else -1)
        d.line((px,y,px+((-1)**k),y-1),fill=color(mid,100-phase*9))
    # Material-specific chips: moss leaves, blazing ember slivers, or tiny
    # crystalline shards. Independent authored signatures in all 12 cells.
    for j in range(5):
        x=24+[-16,-9,0,10,17][j]+[0,1,-1,2][phase]*(-1 if j%2 else 1)
        y=[10,7,12,8,13][j]-[0,1,2,1][phase]
        if not (2<=x<=45 and 2<=y<=20):continue
        if row==0:
            d.line(((x,y+2),(x-1,y),(x+1,y-1)),fill=color(glow,145-phase*15),width=1)
            if j%2:d.point((x+2,y+1),fill=color(mid,118-phase*12))
        elif row==1:
            d.line((x,y+2,x+1,y-1),fill=color(glow,209-phase*27),width=1)
            if j!=2:d.point((x-1,y-1),fill=color(mid,146-phase*17))
        else:
            d.line(((x,y+2),(x+1,y),(x,y-2)),fill=color(glow,172-phase*17),width=1)
            if j%2==0:d.point((x-2,y),fill=color(mid,142-phase*12))
    # Sparse shadow motes fade over the same four GameCore release snapshots.
    for j in range(6):
        x=24+[-18,-12,-6,7,13,18][j]+phase*(1 if j%2 else -1)
        y=[17,14,9,9,15,18][j]-(phase//2)
        if 2<x<46 and 3<y<22:
            d.point((x,y),fill=color(dark if j%2 else mid,72-phase*8))
    return canvas

def build():
    sheet=Image.new('RGBA',(CELL_W*4,CELL_H*3),(0,0,0,0))
    for row in range(3):
        for phase in range(4):
            sheet.paste(draw_cell(row,phase),(phase*CELL_W,row*CELL_H))
    return sheet

if __name__=='__main__':
    target=Path(sys.argv[1]) if len(sys.argv)>1 else Path(__file__).resolve().parents[1]/'Assets/Resources/Frontier/Art/EnemyReleaseDust.png'
    target.parent.mkdir(parents=True,exist_ok=True)
    build().save(target,optimize=True)
    print(target)
