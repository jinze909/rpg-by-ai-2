#!/usr/bin/env python3
"""Reproduce the committed eight original 24px projectile motion glyphs."""
from PIL import Image, ImageDraw
from pathlib import Path
import math
W=H=24

def paint():
    out=Image.new("RGBA",(96,48))
    for row in range(2):
        for f in range(4):
            im=Image.new("RGBA",(W,H));d=ImageDraw.Draw(im)
            if row==0:
                warm=[(250,232,173,255),(241,177,97,245),
                      (224,115,75,205),(187,80,62,148)]
                for i in range(4):
                    x=5+i*4
                    y=11+int(1.1*math.sin(i*.85+f*.67))
                    d.line((x,y,x+2+(i%2),y),fill=warm[i],width=1)
                    if i<3:
                        d.point((x+1,y-1),fill=(255,234,179,90+35*i))
                for j in range(f+1):
                    x=7+3*j;y=8 if j%2==0 else 15
                    d.point((x,y),fill=(255,206,124,160+j*21))
                    if j%2==1:
                        d.point((x+1,y+1),fill=(255,242,200,125))
                d.line((6,12,17,12),fill=(246,201,146,93))
            else:
                for j in range(3):
                    y=7+j*5
                    coords=[(5,y+f%2),(8,y-1),(11,y+1),
                            (14,y-2),(17,y)]
                    pal=[(78,208,246,95),(121,222,255,170),
                         (180,179,254,225),(226,142,251,235),
                         (247,191,255,165)]
                    for k,(x,yy) in enumerate(coords):
                        d.point((x,yy),fill=pal[k])
                    for k in range(1,len(coords)):
                        (ax,ay),(bx,by)=coords[k-1:k+1]
                        d.line((ax,ay,bx,by),fill=pal[k],width=1)
                for j in range(f+1):
                    d.point((7+j*3,3+(j*5+f*2)%17),
                        fill=(241,222,255,205))
                    d.point((19-j*2,6+(j*3+f)%13),
                        fill=(128,234,255,128))
            out.paste(im,(24*f,24*row))
    return out

if __name__=="__main__":
    destination=Path(__file__).resolve().parents[1]/(
        "Assets/Resources/Frontier/Art/ProjectileTrails.png")
    destination.parent.mkdir(parents=True,exist_ok=True)
    paint().save(destination,optimize=True)
    print("Eight authored projectile trail poses:",destination)
