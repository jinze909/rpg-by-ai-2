#!/usr/bin/env python3
"""The Briar Beast: hand-authored pixel geometry, rasterized with Pillow.
Three readable Boss phases x 6 actions x 4 individually rendered keyframes.
No sprite grafts, body warps, source character recolor, or external AI model.
320px cells painted on a consistent 80px source grid, nearest-neighbor x4.
"""
from pathlib import Path
from PIL import Image, ImageDraw
import argparse

ROOT=Path(__file__).resolve().parents[1]
STAGED=ROOT/".starfall-checks/phase2-briar/BriarBeast.png"
INSTALLED=ROOT/"Assets/Resources/Frontier/Art/BriarBeast.png"
ACTIONS=("Idle","Walk","Windup","Release","Hit","Death")
CELL=320
PAL=(
 ("#17283b","#34483b","#526c53","#6e8461","#9aaf78","#bb9c70","#78b6a6"),
 ("#17283b","#473e42","#77604c","#967251","#e4a968","#d0c093","#e69a65"),
 ("#17283b","#30364e","#495a70","#657e85","#9aafaf","#d7ceab","#9ecddd"),
)
# Shift, shoulder, antler rise, two grounded strides, claw reach,
# crouch, and root-spike height -- each keyframe draws a WHOLE actor.
KEY={
 "Idle":[(0,0,0,-1,1,0,0,0),(1,-1,1,0,1,0,0,1),
         (0,-1,0,1,0,-1,0,0),(-1,0,1,0,-1,0,0,-1)],
 "Walk":[(-2,-1,1,-5,3,-2,0,0),(0,-2,0,-2,5,2,-1,1),
         (2,-1,2,4,-4,2,0,0),(0,0,0,5,-2,-3,-1,1)],
 "Windup":[(-2,1,0,-2,2,-4,1,0),(-4,2,3,-4,3,-6,2,2),
           (-5,1,5,-5,4,-8,3,3),(-6,0,6,-6,5,-10,3,4)],
 "Release":[(-5,-1,6,-5,4,-10,2,4),(1,-3,4,-4,5,9,1,3),
            (4,-2,1,-5,5,12,0,1),(1,0,0,-3,3,5,1,0)],
 "Hit":[(-4,1,-1,-4,3,-5,3,0),(-7,3,-4,-4,2,-6,5,-1),
        (-4,2,-2,-3,2,-3,3,0),(-1,1,0,-2,2,-1,1,0)],
 "Death":[(-5,4,-3,-4,4,-6,5,-1),(-8,10,-7,-3,5,-7,9,-3),
          (-10,16,-10,-2,4,-8,13,-5),(-10,18,-12,-2,3,-8,14,-7)]
}
def polygon(d,points,fill):
    d.polygon(points,fill=fill)

def render_death(phase,step):
    """Four separately composed grounded collapse drawings, not an upright warp."""
    dark,shade,bark,leaf,glow,bone,eye=PAL[phase]
    im=Image.new("RGBA",(80,80))
    d=ImageDraw.Draw(im)
    d.ellipse((14,72,65,76),fill=(16,28,40,65))
    roll=step*3
    polygon(d,[(22,53+roll),(31,47+roll),(55,49+roll),
        (67,60+roll//3),(64,69),(25,70)],dark)
    polygon(d,[(25,56+roll),(35,51+roll),(55,52+roll),
        (62,61),(54,66),(27,67)],shade)
    for i in range(4+phase):
        x=33+i*6
        tip=47+roll-i%2*5-phase
        polygon(d,[(x-3,56+roll),(x+2,tip),(x+5,59+roll)],dark)
        polygon(d,[(x-1,56+roll),(x+2,tip+3),(x+3,57+roll)],leaf)
    for j in range(3):
        d.line((36+j*4,57+roll+j,52+j*3,61+roll+j),fill=bark,width=2)
    if phase:
        d.line((42,56+roll,53,64+roll//2),fill=glow,width=2)
    if phase==2:
        d.ellipse((47,57+roll//2,55,65+roll//2),fill=dark)
        d.ellipse((50,60+roll//2,53,63+roll//2),fill=eye)
    for x,extend in ((36,-8),(56,8)):
        polygon(d,[(x-5,63),(x+1,62),(x+extend+3,68),
                   (x+extend+1,72),(x+extend-6,71)],dark)
        polygon(d,[(x-3,64),(x,64),(x+extend,69),(x+extend-2,70)],bark)
        for t in (-3,1,5):
            polygon(d,[(x+extend+t-3,68),(x+extend+t,68),
                        (x+extend+t+1,73),(x+extend+t-1,72)],bone)
    hx=24-step*2
    hy=58+step*2
    polygon(d,[(hx-13,hy-5),(hx-8,hy-11),(hx+5,hy-10),
               (hx+12,hy-3),(hx+10,hy+9),(hx-6,hy+9),(hx-14,hy+3)],dark)
    polygon(d,[(hx-11,hy-3),(hx-6,hy-9),(hx+4,hy-8),
               (hx+9,hy),(hx+3,hy+5),(hx-10,hy+4)],bark)
    for side in (-1,1):
        x=hx+side*7
        polygon(d,[(x-3,hy-5),(x+side*3,hy-15+step),
                   (x+side*6,hy-3)],dark)
        d.line((x,hy-8,x+side*4,hy-12+step),fill=leaf,width=2)
    d.line((hx-5,hy+1,hx+6,hy+1),fill=eye if step<3 else dark,width=2)
    d.line((hx-4,hy+6,hx+5,hy+6),fill=dark,width=2)
    return im.resize((CELL,CELL),Image.Resampling.NEAREST)

def render_frame(phase,action,step):
    if action=="Death":return render_death(phase,step)
    dark,shade,bark,leaf,glow,bone,eye=PAL[phase]
    shift,drop,horn,lf,rf,reach,crouch,spike=KEY[action][step]
    im=Image.new("RGBA",(80,80))
    d=ImageDraw.Draw(im)
    cx=40+shift
    d.ellipse((19,72,61,76),fill=(16,28,40,65))
    knee=60+max(0,crouch)//2
    for side,dx in ((-1,lf),(1,rf)):
        hip=cx+side*12
        toe=hip+dx+side*4
        polygon(d,[(hip-6,knee-5),(hip+6,knee-5),(toe+7,69),
                   (toe+6,73),(toe-7,73),(toe-9,67)],dark)
        polygon(d,[(hip-4,knee-3),(hip+4,knee-3),(toe+4,68),(toe-4,69)],bark)
        for claw in (-4,0,4):
            polygon(d,[(toe+claw,70),(toe+claw+3,70),
                       (toe+claw+4,74),(toe+claw+1,73)],bone)
    top=29+drop+max(0,crouch)
    back=51+drop+max(0,crouch)//2
    polygon(d,[(cx-17,top+4),(cx-23,top+13),(cx-20,back+9),
               (cx-12,64),(cx+14,64),(cx+23,back+5),
               (cx+19,top+7),(cx+7,top-3),(cx-9,top-4)],dark)
    polygon(d,[(cx-16,top+5),(cx-18,top+16),(cx-14,back+7),
               (cx-8,61),(cx+13,61),(cx+19,back+3),
               (cx+16,top+9),(cx+5,top),(cx-7,top-1)],shade)
    polygon(d,[(cx-14,top+6),(cx-10,top+1),(cx-3,top+8),
               (cx-4,back+4),(cx-13,back+1)],bark)
    polygon(d,[(cx+2,top+4),(cx+10,top+2),(cx+17,top+11),
               (cx+13,back+2),(cx+3,back-2)],bark)
    for j in range(4+phase):
        x=cx-16+j*8
        tip=top-5-horn//2-spike-(j%2)*5-phase*2
        polygon(d,[(x-5,top+6),(x+1,tip),(x+4,top+7)],dark)
        polygon(d,[(x-2,top+5),(x+1,tip+4),(x+2,top+5)],leaf)
    for q in range(4):
        y=top+13+q*5
        d.line((cx-10+q,y,cx+5+q*2,y-3),fill=leaf if q%2 else bark,width=2)
    if phase>=1:
        d.line((cx-6,top+13,cx+6,back-1),fill=glow,width=3)
        d.line((cx+6,top+12,cx-6,back-3),fill=dark,width=2)
    if phase==2:
        d.ellipse((cx-5,top+15,cx+6,top+25),fill=dark)
        d.ellipse((cx-3,top+17,cx+3,top+23),fill=eye)
    ay=top+12
    lx=max(13,cx-23-(2 if action=="Windup" else 0))
    rx=min(68,cx+23+reach)
    ry=ay+(8 if action in ("Idle","Walk") else -9 if action=="Windup" else 0)
    polygon(d,[(cx-15,ay-3),(cx-22,ay),(lx-6,ry-1),
               (lx-4,ry+9),(lx+2,ry+10),(cx-10,ay+11)],dark)
    polygon(d,[(cx-16,ay),(lx-2,ry+2),(lx,ry+7),(cx-9,ay+10)],leaf)
    polygon(d,[(cx+12,ay-4),(cx+23,ay),(rx+6,ry-1),
               (rx+6,ry+8),(rx-2,ry+11),(cx+10,ay+9)],dark)
    polygon(d,[(cx+14,ay-2),(cx+20,ay+2),(rx+3,ry+3),
               (rx+1,ry+7),(cx+12,ay+5)],bark)
    for anchor,fy in ((lx-2,ry+7),(rx+2,ry+8)):
        for ox in (-4,0,4):
            polygon(d,[(anchor+ox-2,fy-2),(anchor+ox+1,fy),
                       (anchor+ox+2,fy+6),(anchor+ox-1,fy+5)],bone)
    faceY=max(26,19+drop+max(0,crouch))
    polygon(d,[(cx-12,faceY),(cx-9,faceY-6),(cx-5,faceY-7),
               (cx,faceY-3),(cx+7,faceY-8),(cx+12,faceY),
               (cx+12,faceY+12),(cx+5,faceY+16),
               (cx-9,faceY+14),(cx-13,faceY+8)],dark)
    polygon(d,[(cx-10,faceY+1),(cx-7,faceY-4),(cx+8,faceY-2),
               (cx+9,faceY+8),(cx+2,faceY+12),
               (cx-8,faceY+9)],bark)
    for side in (-1,1):
        hx=cx+side*7
        hy=faceY-5-horn//2
        polygon(d,[(hx-3,faceY+1),(hx+side*6,hy-9-phase*2),
                   (hx+side*5,hy+5)],dark)
        d.line((hx+side*2,hy-4,hx+side*9,hy-8),fill=leaf,width=2)
    for eyex in (cx-6,cx+5):
        d.rectangle((eyex-2,faceY+5,eyex+1,faceY+7),fill=eye)
        d.point((eyex-1,faceY+5),fill=bone)
    d.line((cx-4,faceY+12,cx+5,faceY+12),fill=dark,width=2)
    d.point((cx-4,faceY+13),fill=bone)
    d.point((cx+4,faceY+13),fill=bone)
    for i in range(phase*2):
        y=38+i*5
        x=cx+(-16 if i%2==0 else 16)
        polygon(d,[(x-3,y+4),(x+(5 if i%2 else -5),y-5),
                   (x+4,y+5)],dark)
        d.line((x,y+1,x+(3 if i%2 else -3),y-1),fill=glow,width=2)
    return im.resize((CELL,CELL),Image.Resampling.NEAREST)

def render(dest):
    atlas=Image.new("RGBA",(CELL*4,CELL*18))
    for phase in range(3):
        for action_index,action in enumerate(ACTIONS):
            for step in range(4):
                frame=render_frame(phase,action,step)
                atlas.paste(frame,(step*CELL,(phase*6+action_index)*CELL))
    dest.parent.mkdir(parents=True,exist_ok=True)
    atlas.save(dest,optimize=True)

if __name__=="__main__":
    parser=argparse.ArgumentParser()
    parser.add_argument("--install",action="store_true")
    args=parser.parse_args()
    render(INSTALLED if args.install else STAGED)
    print("Generated The Briar Beast: 72 separate 320px poses across three phases")
