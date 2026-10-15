#!/usr/bin/env python3
"""Two unique original 128px full-body Emberfall residents, idle/talk x four.

Hand-authored per-frame Pillow pixel geometry on 64px grids (2x nearest).
Uses ART-BIBLE foot at (64,120); no image-gen claims or skeletal warping.
Guild steward Lyra and blacksmith Noll are separate characters.
"""
from pathlib import Path
import argparse
from PIL import Image, ImageDraw

ROOT=Path(__file__).resolve().parents[1]
INSTALLED=ROOT/"Assets/Resources/Frontier/Art/EmberfallResidents.png"
STAGED=ROOT/".starfall-checks/phase2-residents/EmberfallResidents.png"
ACTIONS=("guild_idle","guild_talk","forge_idle","forge_talk")
SIZE=128
INK="#17283b"
WHITE="#f2ead7"
SHADOW="#304052"
TEAL="#78b6a6"
GOLD="#d8b875"

# Per-keyframe independently drawn arm and body postures. Values are not
# offsets applied to a preexisting full-body sprite.
POSES={
 "guild_idle":[(0,0,0,0,0),(0,-1,1,0,0),(0,0,-1,1,1),(-1,0,0,-1,0)],
 "guild_talk":[(0,0,-4,-3,0),(1,-1,-6,-4,1),(0,0,-2,-1,0),(-1,0,-5,-5,1)],
 "forge_idle":[(0,0,0,0,0),(0,-1,1,0,1),(1,0,0,-1,0),(0,0,-1,1,0)],
 "forge_talk":[(0,0,2,-4,0),(0,-1,4,-7,1),(1,-1,1,-3,0),(0,0,3,-6,1)],
}

def draw_frame(action,step):
    guild=action.startswith("guild")
    speaking=action.endswith("talk")
    sx,rise,left,right,mouth=POSES[action][step]
    im=Image.new("RGBA",(64,64))
    d=ImageDraw.Draw(im)
    cx=32+sx
    # Stable ground shadow and both boots anchored at the canonical foot.
    d.ellipse((20,57,44,61),fill=(23,40,59,70))
    for x in (cx-7,cx+6):
        d.polygon([(x-4,49),(x+4,49),(x+4,57),(x-4,58)],fill=INK)
        d.polygon([(x-3,50),(x+2,50),(x+2,56),(x-3,56)],
                  fill="#85614d" if guild else "#66544b")
        d.polygon([(x-5,56),(x+5,56),(x+7,60),(x-6,60)],fill=SHADOW)
        d.line((x-5,59,x+6,59),fill="#bca184")
    # Lower coat / leather work trousers (single-body silhouette).
    d.polygon([(cx-11,37+rise),(cx+11,37+rise),(cx+12,52),
               (cx+4,55),(cx,48),(cx-4,55),(cx-12,52)],fill=INK)
    d.polygon([(cx-9,39+rise),(cx+8,39+rise),(cx+9,52),
               (cx+3,51),(cx,47),(cx-4,52),(cx-10,52)],
               fill="#355e73" if guild else "#644939")
    for x in (cx-7,cx+6):
        d.line((x,45,x,52),fill="#667c83" if guild else "#ae8464",width=2)
    # Coherent tunic + apron, with explicit shoulder-to-hand sleeves.
    d.polygon([(cx-9,27+rise),(cx+8,27+rise),(cx+12,38),
               (cx+10,47),(cx-11,47),(cx-13,37)],fill=INK)
    d.polygon([(cx-7,29+rise),(cx+6,29+rise),(cx+10,40),
               (cx+7,45),(cx-8,45),(cx-10,39)],
               fill="#355e73" if guild else "#bd715f")
    d.polygon([(cx-5,33),(cx+5,33),(cx+8,45),(cx-7,45)],
              fill="#526c53" if guild else "#85614d")
    if guild:
        d.line((cx,32,cx,46),fill=GOLD,width=2)
        d.ellipse((cx-2,32,cx+2,36),fill=WHITE)
        d.point((cx,34),fill=TEAL)
    else:
        d.rectangle((cx-6,37,cx+6,41),fill="#4d342a")
        d.rectangle((cx-1,38,cx+2,41),fill=GOLD)
        for y in (43,46):
            d.line((cx-5,y,cx+3,y),fill="#b38e6c",width=1)
    # Arms are fully redrawn for every posture, no floating overlay.
    for side,shift in ((-1,left),(1,right)):
        shoulder=cx+side*9
        elbow=cx+side*12+shift//2
        handx=cx+side*13+shift
        handy=(39+rise+shift//2) if speaking else 43+rise
        d.polygon([(shoulder-3,30+rise),(shoulder+3,30+rise),
                   (elbow+3,39+rise),(handx+3,handy),
                   (handx-3,handy+3),(elbow-4,38+rise)],fill=INK)
        d.line((shoulder,32+rise,elbow,39+rise),
               fill="#526c53" if guild else "#85614d",width=4)
        d.line((elbow,39+rise,handx,handy),fill=SHADOW,width=4)
        d.ellipse((handx-3,handy-2,handx+2,handy+4),fill="#d2ae8d")
    # Permanent class-specific props; talking arms change hand and prop pose.
    if guild:
        # Messenger satchel with a folded guild charter.
        d.line((cx-9,33,cx+8,45),fill=INK,width=3)
        d.rectangle((cx+5,43,cx+13,52),fill=INK)
        d.rectangle((cx+7,44,cx+11,50),fill="#85614d")
        d.line((cx+8,47,cx+11,47),fill=GOLD)
        if speaking:
            d.polygon([(cx-22,30+step%2),(cx-13,29+step%2),
                       (cx-11,36+step%2),(cx-20,38+step%2)],fill=INK)
            d.polygon([(cx-21,31+step%2),(cx-14,30+step%2),
                       (cx-12,35+step%2),(cx-19,36+step%2)],fill=WHITE)
            d.line((cx-19,33,cx-15,33),fill="#85614d",width=1)
    else:
        # No duplicated weapon: hammer always belongs to the right hand.
        handleX=cx+13+right
        tip=24+rise+(0 if speaking else 7)
        d.line((handleX,43+rise,handleX+2,tip),fill=INK,width=4)
        d.line((handleX+1,42+rise,handleX+2,tip+2),fill=GOLD,width=2)
        d.rectangle((handleX-5,tip-4,handleX+7,tip),fill=INK)
        d.rectangle((handleX-3,tip-3,handleX+5,tip-1),fill="#a5b5b9")
        d.line((handleX-1,tip-3,handleX+4,tip-3),fill=WHITE)
    # Head and recognizable hair are re-authored as complete pixel regions.
    head=19+rise
    d.rectangle((cx-4,head+8,cx+5,head+14),fill="#d2ae8d")
    d.polygon([(cx-9,head-1),(cx+8,head-1),(cx+10,head+11),
               (cx+6,head+14),(cx-7,head+13),(cx-11,head+8)],
              fill=INK)
    d.polygon([(cx-7,head+1),(cx+6,head+1),(cx+8,head+9),
               (cx+4,head+12),(cx-6,head+11),(cx-8,head+7)],
              fill="#e6c6a3" if guild else "#c99b7d")
    if guild:
        # Lyra: chestnut side braid and gold ribbon.
        d.polygon([(cx-11,head+5),(cx-12,head-7),
                   (cx-4,head-11),(cx+6,head-10),
                   (cx+11,head-4),(cx+10,head+3),(cx+5,head),
                   (cx-1,head-2),(cx-9,head+3)],fill="#644939")
        d.polygon([(cx-9,head-5),(cx-4,head-10),
                   (cx+2,head-9),(cx-5,head-2)],fill="#a47a59")
        for y in (head+7,head+11,head+15):
            d.ellipse((cx+7,y,cx+11,y+4),fill="#85614d")
        d.rectangle((cx+8,head+18,cx+10,head+19),fill=GOLD)
    else:
        # Noll: auburn short hair + leather forge headband and heavy beard.
        d.polygon([(cx-10,head+1),(cx-8,head-9),
                   (cx+5,head-11),(cx+11,head-5),(cx+9,head+3)],
                  fill="#824e38")
        d.line((cx-9,head-3,cx+8,head-5),fill="#d49d63",width=3)
        d.polygon([(cx-8,head+7),(cx-4,head+11),(cx+4,head+12),
                   (cx+9,head+6),(cx+6,head+16),(cx-3,head+17)],
                  fill="#644939")
        d.polygon([(cx-3,head+13),(cx+3,head+12),
                   (cx+1,head+17)],fill="#85614d")
    for eyeX in (cx-5,cx+4):
        d.line((eyeX-1,head+5,eyeX+1,head+5),fill=INK,width=2)
    if mouth:
        d.line((cx-2,head+10,cx+2,head+10),fill="#9a6654",width=2)
        d.point((cx,head+11),fill=INK)
    else:
        d.line((cx-2,head+10,cx+2,head+10),fill=INK)
    return im.resize((SIZE,SIZE),Image.Resampling.NEAREST)

def render(destination):
    atlas=Image.new("RGBA",(SIZE*4,SIZE*4))
    for row,action in enumerate(ACTIONS):
        for col in range(4):
            frame=draw_frame(action,col)
            atlas.paste(frame,(col*SIZE,row*SIZE))
    destination.parent.mkdir(parents=True,exist_ok=True)
    atlas.save(destination,optimize=True)

if __name__=="__main__":
    p=argparse.ArgumentParser()
    p.add_argument("--install",action="store_true")
    args=p.parse_args()
    render(INSTALLED if args.install else STAGED)
    print("Generated 16 hand-authored pixel poses (guild/forge idle and talk)")
