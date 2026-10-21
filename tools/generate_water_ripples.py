#!/usr/bin/env python3
"""Eight reproducible, hand-directed 24x16 temperate/cold-water ripple poses."""
from PIL import Image, ImageDraw
from pathlib import Path
import sys

W,H=24,16
PALETTES=[
    ((38,132,164,122),(102,207,208,172),(195,244,231,215),(233,253,239,235)),
    ((73,113,161,130),(142,191,222,177),(201,231,248,218),(241,251,255,237)),
]

def build():
    sheet=Image.new("RGBA",(W*4,H*2),(0,0,0,0))
    for biome,colors in enumerate(PALETTES):
        for frame in range(4):
            im=Image.new("RGBA",(W,H),(0,0,0,0))
            pen=ImageDraw.Draw(im)
            radius=[4,6,8,9][frame]
            ry=[2,3,4,5][frame]
            cx,cy=12,9
            rect=(cx-radius,cy-ry,cx+radius,cy+ry)
            pen.arc(rect,195+frame*4,329-frame*4,fill=colors[2],width=1)
            pen.arc(rect,16+frame*3,149-frame*3,fill=colors[1],width=1)
            if frame>0:
                inner=radius-3
                h2=max(1,ry-2)
                pen.arc((cx-inner,cy-h2,cx+inner,cy+h2),
                    206,307,fill=colors[0],width=1)
                pen.arc((cx-inner,cy-h2,cx+inner,cy+h2),
                    29,128,fill=colors[1],width=1)
            points=[
                [(9,7,3),(14,8,2),(17,10,1)],
                [(8,7,3),(16,7,2),(19,10,1),(5,10,0)],
                [(7,6,3),(17,7,2),(20,11,1),(4,9,0)],
                [(6,7,2),(16,5,3),(21,9,1),(3,11,0)],
            ][frame]
            for x,y,ink in points:
                pen.point((x,y),fill=colors[ink])
            if frame==0: pen.line([(10,10),(13,10)],fill=colors[0])
            if frame==1: pen.point((12,4),fill=colors[3])
            if frame==2: pen.line([(10,3),(12,3)],fill=colors[0])
            if frame==3: pen.line([(18,12),(20,12)],fill=colors[0])
            sheet.paste(im,(frame*W,biome*H))
    return sheet

if __name__=="__main__":
    output=Path(sys.argv[1]) if len(sys.argv)>1 else Path(__file__).resolve().parents[1]/"Assets/Resources/Frontier/Art/WaterRipples.png"
    output.parent.mkdir(parents=True,exist_ok=True)
    build().save(output,optimize=True)
    print("PASS eight original 24x16 water ripple sprites:",output)
