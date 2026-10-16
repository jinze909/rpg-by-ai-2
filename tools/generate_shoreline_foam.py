#!/usr/bin/env python3
"""Reproduce four unique 32x12 pixel shoreline crests; never touch ground art."""
from PIL import Image, ImageDraw
from pathlib import Path
WIDTH,HEIGHT=32,12
DEEP=(38,119,147,104)
TEAL=(88,193,208,151)
FROTH=(168,230,226,213)
GLINT=(238,249,231,250)
TAIL=(69,155,177,110)
CRESTS=[
 [(2,7),(3,6),(4,6),(6,5),(7,5),(10,6),(12,6),(14,5),(15,5),(17,6),(19,6),(21,5),(24,5),(25,6),(29,7)],
 [(2,7),(4,7),(5,6),(7,5),(8,5),(10,5),(12,6),(14,6),(16,5),(18,5),(20,6),(23,5),(25,6),(27,6),(29,7)],
 [(2,7),(4,6),(6,6),(8,6),(10,5),(11,5),(13,6),(15,5),(16,4),(18,5),(20,6),(22,6),(24,5),(26,6),(29,7)],
 [(2,7),(5,7),(7,6),(9,5),(11,6),(13,6),(15,5),(17,5),(18,6),(20,6),(22,5),(24,5),(26,6),(28,7),(29,7)],
]
SPARKS=[((6,3),(21,4)),((9,3),(24,4)),((16,2),(26,4)),((10,3),(23,3))]
def build():
    image=Image.new("RGBA",(WIDTH*4,HEIGHT),(0,0,0,0))
    for frame,contour in enumerate(CRESTS):
        cell=Image.new("RGBA",(WIDTH,HEIGHT),(0,0,0,0))
        pen=ImageDraw.Draw(cell)
        for x in range(2,30):
            ys=[y for xx,y in contour if xx==x]
            if not ys:continue
            y=ys[0]
            pen.point((x,min(10,y+3)),fill=TAIL)
            pen.point((x,min(10,y+2)),fill=DEEP)
        for k,(x,y) in enumerate(contour):
            pen.point((x,y),fill=GLINT if (k+frame)%5==0 else FROTH)
            if (k+frame)%4!=3 and x+1<30:
                pen.point((x+1,y),fill=TEAL)
            if k%3==0:pen.point((x,min(y+1,10)),fill=FROTH)
        for i,(x,y) in enumerate(SPARKS[frame]):
            pen.point((x,y),fill=GLINT if i==0 else TEAL)
        if frame%2==0:pen.point((27,9),fill=DEEP)
        else:pen.point((3,9),fill=TEAL)
        image.paste(cell,(frame*WIDTH,0))
    return image
if __name__=="__main__":
    import sys
    out=Path(sys.argv[1]) if len(sys.argv)>1 else Path(__file__).resolve().parents[1]/"Assets/Resources/Frontier/Art/ShorelineFoam.png"
    out.parent.mkdir(parents=True,exist_ok=True)
    build().save(out,optimize=True)
    print("PASS four painted shoreline poses",out)
