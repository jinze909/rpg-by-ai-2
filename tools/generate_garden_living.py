#!/usr/bin/env python3
"""Hand-authored 8 painted 32x32 Moonherb garden growth poses; Unity source."""
from pathlib import Path
from PIL import Image, ImageDraw
import sys
W=32; H=32

def paint(stage,phase):
    im=Image.new("RGBA",(W,H))
    d=ImageDraw.Draw(im)
    sway=[0,1,0,-1][phase]
    foliage=[(39,80,65,122),(58,117,76,164),
             (98,160,87,183),(160,203,122,199)]
    bloom=[(241,196,108,191),(232,147,142,177),
           (185,159,215,191),(248,225,144,216)]
    for k,(rootx,rooty) in enumerate([(9,28),(16,29),(24,28)]):
        lean=((k%2)*2-1)*sway
        height=(10+(k%2)*3) if stage==0 else (16+(k%2)*4)
        tx=rootx+lean+(-1 if k==0 else 1 if k==2 else 0)
        ty=rooty-height
        d.line([(rootx,rooty),(rootx,rooty-7),(tx,ty)],
               fill=foliage[1+k%3],width=2)
        py=rooty-9-(k%2)*2
        for side in (-1,1):
            leafx=rootx+side*(4+(phase+k)%2)
            d.polygon([(rootx,py+2),(leafx,py-3),
                       (leafx+side,py-5),(leafx-side,py-2),
                       (rootx,py+3)],fill=foliage[2+(k%2)])
            d.line((rootx,py+1,leafx,py-3),
                   fill=foliage[0],width=1)
        if stage==1:
            cx,cy=tx,ty
            for ox,oy in [(0,-3),(-3,0),(3,0),(0,3)]:
                d.point((cx+ox,cy+oy),fill=bloom[(phase+k)%4])
                d.point((cx+ox,cy+oy+1),
                        fill=bloom[(phase+k+1)%4])
            d.rectangle((cx-1,cy-1,cx+1,cy+1),
                        fill=(250,227,128,215))
    if stage==0 and phase==2:
        # Hand-painted leaf recovery glint distinct from initial neutral pose.
        d.line((19,19,21,18),fill=foliage[0],width=1)
    if stage==1:
        for j,(bx,by) in enumerate([(5,8),(27,10),(12,5),(21,7)]):
            x=bx+sway*(1 if j%2 else -1)
            y=by+[-1,0,1,0][(phase+j)%4]
            d.point((x,y),fill=bloom[j%4])
    return im

def build():
    sheet=Image.new("RGBA",(128,64))
    for row in range(2):
        for phase in range(4):
            sheet.paste(paint(row,phase),(phase*W,row*H))
    return sheet

if __name__=="__main__":
    target=Path(sys.argv[1]) if len(sys.argv)>1 else (
        Path(__file__).resolve().parents[1] /
        "Assets/Resources/Frontier/Art/GardenLiving.png")
    target.parent.mkdir(parents=True,exist_ok=True)
    build().save(target,optimize=True)
    print(target)
