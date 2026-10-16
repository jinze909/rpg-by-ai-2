#!/usr/bin/env python3
"""Repaint the exact eight hand-authored transparent NPC world-state icons.
Four subtle speech poses and four urgent escort warning poses. This ONLY
generates visual overlays; original detailed Npcs.png is immutable.
"""
from pathlib import Path
from PIL import Image, ImageDraw

CELL_W,CELL_H=24,20
GLOW=(28,39,65,160)
OUT=(49,60,82,230)
GOLD=(243,210,141,240)
CREAM=(254,248,213,255)
CYAN=(154,238,223,235)
RED=(222,99,104,240)
RIM=(255,185,140,255)

def build():
    art=Image.new('RGBA',(CELL_W*4,CELL_H*2))
    for frame in range(4):
        tile=Image.new('RGBA',(CELL_W,CELL_H),(0,0,0,0))
        d=ImageDraw.Draw(tile)
        if frame % 2 == 0:d.point((4,2),fill=(255,235,183,145))
        if frame%3==1:d.line((20,15,21,15),fill=CYAN)
        d.polygon([(5,5),(7,3),(18,3),(20,5),(20,12),(17,15),
                   (12,15),(9,18),(9,15),(6,14),(5,12)],fill=OUT)
        d.rectangle((7,5,18,12),fill=(104,153,164,225))
        d.line((8,5,17,5),fill=CYAN)
        dots=[(9,9),(12,9),(15,9)]
        statuses=([1,0,0],[1,1,0],[1,1,1],[0,1,1])[frame]
        for (x,y),on in zip(dots,statuses):
            d.rectangle((x,y,x+1,y+1),fill=CREAM if on else GLOW)
        d.point((17,7),fill=CREAM)
        art.paste(tile,(frame*CELL_W,0))
    for frame in range(4):
        tile=Image.new('RGBA',(CELL_W,CELL_H),(0,0,0,0))
        d=ImageDraw.Draw(tile)
        d.polygon([(12,2),(18,5),(22,15),(18,17),(6,17),
                   (2,15),(6,5)],fill=OUT)
        d.polygon([(12,4),(17,6),(20,14),(17,15),(7,15),
                   (4,14),(7,6)],fill=RED)
        d.line((12,7,12,11),fill=CREAM,width=2)
        d.rectangle((11,13,13,14),fill=RIM)
        accents=[[(3,4),(20,4)],[(2,10),(21,10)],
                 [(4,17),(19,17)],[(7,2),(17,2)]]
        for x,y in accents[frame]:d.point((x,y),fill=RIM)
        if frame in (1,2):d.point((12,0),fill=GOLD)
        art.paste(tile,(frame*CELL_W,CELL_H))
    return art

if __name__=='__main__':
    output=Path(__file__).resolve().parents[1]/(
        "Assets/Resources/Frontier/Art/NpcActionSignals.png")
    output.parent.mkdir(parents=True,exist_ok=True)
    build().save(output,optimize=True)
    print("Rendered eight distinct pixel NPC and escort cues:",output)
