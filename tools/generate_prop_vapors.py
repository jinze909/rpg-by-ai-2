#!/usr/bin/env python3
"""Original 4x2 32x48 hand-directed vapor and ash-fleck Sprite paintings."""
from pathlib import Path
from PIL import Image,ImageDraw
import math,sys
W,H=32,48
DARK=[(57,53,53,34),(76,74,75,45),(100,97,93,47)]
LIGHT=[(122,116,108,45),(150,139,121,47),(196,159,100,69)]
COOL=[(27,91,107,42),(51,130,153,55),(87,184,197,66)]
COOL_HI=[(116,214,217,86),(158,234,231,87),(221,248,239,104)]

def paint(row,phase):
    art=Image.new('RGBA',(W,H),(0,0,0,0))
    d=ImageDraw.Draw(art)
    # Connected wisps are not a rigid procedural coil; both families have
    # hand-adjusted silhouettes as their thermal/arcane currents curl upward.
    pulse=math.sin((phase/4)*math.tau)
    offset=[0,1,0,-1][phase]
    if row==0:
        # Warm smoke fades to colder charcoal at its highest crest.
        strokes=[[(15,42),(12+offset,36),(17+offset,31),(12+offset,26),(11+offset,20)],
                 [(17,41),(19-offset,35),(16-offset,29),(21-offset,23),(21-offset,17)],
                 [(14,32),(10+offset,26),(13+offset,19),(9+offset,14)]]
        for index,points in enumerate(strokes):
            d.line(points,fill=DARK[(index+phase)%3],width=5-index)
            d.line([(x-1,y) for x,y in points],fill=LIGHT[(index+phase)%3],width=1)
        for x,y,r in [(11+offset,20,3),(21-offset,17,4),(9+offset,14,3)]:
            d.arc((x-r,y-r,x+r,y+r),175+phase*18,340+phase*12,
                  fill=LIGHT[(x+phase)%3],width=1)
        # Each real glowing ash fleck belongs to the warm fuel source.
        for n,(x,y) in enumerate([(12,38),(22,31),(9,24),(24,19),(17,12)]):
            dx=[-1,0,1,0][(phase+n)%4];dy=(phase+n)%3
            c=[(235,120,55,183),(255,174,71,202),(255,222,141,222)][n%3]
            d.line([(x+dx,y-dy),(x+dx+(n%2),y-dy-1)],fill=c,width=1)
    else:
        # Cold spectral altar emits thinner looping rune fog, a separate
        # illustration family rather than recoloring the campfire plume.
        curves=[[(14,43),(11+offset,37),(12+offset,30),(19+offset,24),(17+offset,17),(13+offset,12)],
                [(18,41),(22-offset,34),(20-offset,28),(14-offset,22),(16-offset,17)],
                [(16,30),(22+offset,25),(24+offset,19),(21+offset,13)]]
        for i,path in enumerate(curves):
            d.line(path,fill=COOL[(i+phase)%3],width=3 if i<2 else 2)
            d.line(path[1:-1],fill=COOL_HI[(i+phase)%3],width=1)
        for j,(x,y) in enumerate([(11,36),(22,30),(19,18),(13,11),(25,23)]):
            if (j+phase)%3 != 0:
                dx=[0,1,0,-1][(phase+j)%4]
                d.point((x+dx,y-(phase+j)%2),fill=COOL_HI[j%3])
                if j==2: d.line([(x+dx,y),(x+dx,y+2)],fill=COOL[2],width=1)
        d.arc((11+offset,8,21+offset,19),230+phase*13,330+phase*11,
              fill=COOL_HI[(phase+1)%3],width=1)
    # Keep every visible pixel inside 1px padding for source-family QA.
    return art

def build():
    sheet=Image.new('RGBA',(4*W,2*H),(0,0,0,0))
    for row in range(2):
        for phase in range(4):sheet.paste(paint(row,phase),(phase*W,row*H))
    return sheet

if __name__=='__main__':
    path=Path(sys.argv[1]) if len(sys.argv)>1 else Path(__file__).resolve().parents[1]/'Assets/Resources/Frontier/Art/PropVapors.png'
    path.parent.mkdir(parents=True,exist_ok=True)
    build().save(path,optimize=True)
    print(path)
