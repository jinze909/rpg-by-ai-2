#!/usr/bin/env python3
"""Original hand-directed 4x2 translucent 32px cast glyphs.
Fire/ember Boss windup in row 0, frost-violet elite area target in row 1.
Every frame retains its center; only the perimeter glyphs advance.
"""
from pathlib import Path
from PIL import Image, ImageDraw
from math import radians,sin,cos
SIDE=32
COLORS=((229,105,69,240),(253,189,111,255),(255,235,175,255))
COOL=((105,129,216,230),(173,151,242,255),(235,217,255,255))
def build():
    sheet=Image.new("RGBA",(SIDE*4,SIDE*2))
    for kind,palette in enumerate((COLORS,COOL)):
        base,light,core=palette
        for frame in range(4):
            tile=Image.new("RGBA",(SIDE,SIDE))
            d=ImageDraw.Draw(tile)
            for j in range(8):
                phase=(j+frame)%8
                if phase in (2,7):continue
                start=radians(j*45+7+frame*5)
                end=start+radians(19 if kind==0 else 23)
                points=[]
                for k in range(5):
                    a=start+(end-start)*k/4
                    radius=12 if kind==0 else 11
                    points.append((int(round(15.5+cos(a)*radius)),
                                   int(round(15.5+sin(a)*radius))))
                d.line(points,fill=base,width=1)
                if (j+frame)%3==0:
                    a=start+.1
                    for rr in (13,14):
                        d.point((int(round(15.5+cos(a)*rr)),
                                 int(round(15.5+sin(a)*rr))),fill=light)
            for j in range(4):
                if (j+frame)%4==3:continue
                angle=radians(j*90+frame*9)
                d.point((int(round(15.5+cos(angle)*8)),
                         int(round(15.5+sin(angle)*8))),fill=light)
            d.line([(15,10),(20,15),(15,20),(10,15),(15,10)],
                   fill=light,width=1)
            d.line([(15,12),(18,15),(15,18),(12,15),(15,12)],
                   fill=core,width=1)
            d.point((15,15),fill=core)
            if kind==0:
                d.point((15,8-frame%2),fill=core)
                d.point((15,22+frame%2),fill=base)
                d.point((7,15),fill=base)
                d.point((23,15),fill=base)
            else:
                d.point((8-frame%2,8),fill=light)
                d.point((22+frame%2,22),fill=light)
                d.point((8,22),fill=base)
                d.point((22,8),fill=base)
            sheet.paste(tile,(frame*SIDE,kind*SIDE))
    return sheet
if __name__=="__main__":
    out=Path(__file__).resolve().parents[1]/"Assets/Resources/Frontier/Art/CastSigils.png"
    out.parent.mkdir(parents=True,exist_ok=True)
    build().save(out,optimize=True)
    print("Rendered 8 original Boss and elite telegraph frames:",out)
