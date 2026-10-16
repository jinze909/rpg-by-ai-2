#!/usr/bin/env python3
"""Four connected full-body flame keyposes for the existing detailed campfire."""
from PIL import Image, ImageDraw
from pathlib import Path
import sys

W,H=24,32
OUTER=(171,46,32,210)
EMBER=(231,90,35,229)
GOLD=(253,163,54,245)
BRIGHT=(255,217,103,255)
CORE=(255,246,199,255)
POSES=[
 {"outline":[(4,28),(5,22),(7,20),(7,14),(10,17),(11,7),(13,9),(13,16),(16,13),(17,19),(19,22),(18,28)],"hot":[(8,26),(9,20),(11,19),(11,12),(13,18),(16,19),(16,26)]},
 {"outline":[(4,28),(5,23),(6,20),(8,19),(8,11),(11,14),(12,8),(14,11),(15,16),(18,13),(17,22),(19,28)],"hot":[(8,26),(9,21),(10,17),(12,13),(13,18),(15,20),(16,26)]},
 {"outline":[(4,28),(4,22),(7,19),(8,13),(10,15),(12,6),(13,12),(15,15),(16,12),(18,19),(19,28)],"hot":[(8,26),(9,21),(11,16),(12,11),(14,18),(15,20),(16,26)]},
 {"outline":[(4,28),(5,21),(7,20),(8,15),(10,17),(11,10),(12,14),(14,8),(15,13),(16,19),(19,24),(18,28)],"hot":[(8,26),(8,21),(11,19),(12,15),(14,18),(15,21),(16,26)]},
]

def build():
    sheet=Image.new("RGBA",(W*4,H),(0,0,0,0))
    for k,pose in enumerate(POSES):
        im=Image.new("RGBA",(W,H),(0,0,0,0))
        pen=ImageDraw.Draw(im)
        pen.polygon(pose["outline"],fill=OUTER)
        pen.polygon([(6,27),(7,22),(9,19),(10,14 if k==2 else 16),
                     (12,18),(14,14),(17,22),(17,27)],fill=EMBER)
        pen.polygon(pose["hot"],fill=GOLD)
        pen.polygon([(10,26),(10,21),(12,18),(13,21),(14,26)],fill=BRIGHT)
        pen.polygon([(11,26),(12,22),(13,26)],fill=CORE)
        for x,y in (((9,8),(18,14)),((6,13),(16,7)),
                    ((9,5),(20,14)),((7,10),(17,5)))[k]:
            pen.point((x,y),fill=GOLD)
        if k%2==0:pen.point((20,21),fill=EMBER)
        else:pen.point((3,23),fill=OUTER)
        sheet.paste(im,(k*W,0))
    return sheet

if __name__=="__main__":
    output=Path(sys.argv[1]) if len(sys.argv)>1 else Path(__file__).resolve().parents[1]/"Assets/Resources/Frontier/Art/CampfireFlames.png"
    output.parent.mkdir(parents=True,exist_ok=True)
    build().save(output,optimize=True)
    print("PASS four connected hand-painted flame keyposes:",output)
