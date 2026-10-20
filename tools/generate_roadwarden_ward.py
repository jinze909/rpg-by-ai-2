#!/usr/bin/env python3
"""Paint eight original, transparent, world-rooted Roadwarden beacon ward poses.

One four-cell row is a healthy aurora ward, the other a critical ember ward.
Each frame is a distinct *full* ring/rune pose, not a recolored actor body.
"""
from pathlib import Path
from PIL import Image, ImageDraw
import math
import sys

CELL=64

def ring_points(radius, start, end, tilt=1):
    return [(round(31.5+math.cos(a*math.pi/180)*radius),
             round(31.5+math.sin(a*math.pi/180)*radius*tilt))
            for a in range(start, end+1, 5)]

def paint(row,phase):
    im=Image.new('RGBA',(CELL,CELL),(0,0,0,0))
    d=ImageDraw.Draw(im)
    if row==0:
        outer=(66,190,187,170); inner=(134,250,222,207)
        shard=(250,216,128,230); core=(218,252,240,238)
    else:
        outer=(205,70,102,191); inner=(255,148,102,226)
        shard=(255,219,143,243); core=(255,198,196,238)
    # Stationary core silhouette and planted side anchors keep the
    # shield centered on the existing stone column, not a flying orb.
    for offset in (-1,1):
        x=32+offset*22
        d.polygon([(x,27),(x+offset*3,32),(x,37),(x-offset*2,32)],fill=outer)
        d.point((x,32),fill=core)
    d.line([(32,8),(34,12),(32,15),(30,12),(32,8)],fill=inner,width=2)
    d.line([(32,49),(34,52),(32,56),(30,52),(32,49)],fill=inner,width=2)
    # Split segmented 3/4 circumference is an authored protective ward,
    # not an ordinary flat GUI ring. Phases slide only segment edges.
    shift=[0,5,11,17][phase]
    for start,end in [(12+shift,112+shift),(132+shift,232+shift),(252+shift,335+shift)]:
        points=ring_points(25,start,end,0.79)
        if len(points)>1:d.line(points,fill=outer,width=2)
    # Four asymmetric rune nodes orbit along the ring; each of the eight
    # source cells has a different literal pigment footprint.
    for j in range(4):
        a=math.radians(25+j*91+phase*13+row*8)
        x=round(31.5+math.cos(a)*24)
        y=round(31.5+math.sin(a)*19)
        d.polygon([(x,y-3),(x+3,y),(x,y+3),(x-3,y)],fill=shard)
        d.point((x,y),fill=core)
    # Concentric fractured center ticks express a fragile defense
    # without ever painting across the NPC's existing full-body sprite.
    d.line(ring_points(18,30+phase*7,151+phase*7,0.8),fill=inner,width=1)
    d.line(ring_points(18,205+phase*6,325+phase*6,0.8),fill=inner,width=1)
    if row==1:
        # Broken, heated arcs convey critical integrity nonverbally.
        d.line([(14+phase,21),(18+phase,17),(21+phase,20)],fill=shard,width=2)
        d.line([(42-phase,43),(46-phase,47),(50-phase,45)],fill=shard,width=2)
    else:
        d.line([(26,14+phase%2),(32,17+phase%2),(38,14+phase%2)],fill=core,width=1)
    return im

def build():
    sheet=Image.new('RGBA',(CELL*4,CELL*2),(0,0,0,0))
    for row in range(2):
        for phase in range(4):
            sheet.paste(paint(row,phase),(phase*CELL,row*CELL))
    return sheet

if __name__=='__main__':
    target=Path(sys.argv[1]) if len(sys.argv)>1 else Path(__file__).resolve().parents[1]/'Assets/Resources/Frontier/Art/RoadwardenWard.png'
    target.parent.mkdir(parents=True,exist_ok=True)
    build().save(target,optimize=True)
    print('PASS eight original transparent ward keyposes',target)
