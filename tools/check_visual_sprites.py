#!/usr/bin/env python3
"""Audit the shipped Sprite PNGs using the repo's real sprite-animation-qa Skill.

Never claim native Unity/GPU validation; use this to preserve the accepted
blonde protagonist and check real asset bytes, frame distinctness, pixel
density, alpha, head-root sheets and authored 4-phase animation sequences.
"""
import hashlib
import json
import subprocess
import sys
from pathlib import Path
from PIL import Image

ROOT=Path(__file__).resolve().parents[1]
ART=ROOT/"Assets/Resources/Frontier/Art"
REPORT=ROOT/".starfall-checks/sprite-qa"
INSPECT=ROOT/".agents/skills/sprite-animation-qa/scripts/inspect_animation.py"

def check(predicate,why):
    if not predicate:raise AssertionError(why)

def cell_frames(name,cols,rows):
    path=ART/(name+".png")
    check(path.exists(),f"Missing shipped art {path}")
    sheet=Image.open(path).convert("RGBA")
    check(sheet.width%cols==0 and sheet.height%rows==0,
          f"Source atlas grid damaged: {name}")
    w,h=sheet.width//cols,sheet.height//rows
    return path,[sheet.crop(((k%cols)*w,(k//cols)*h,
          ((k%cols)+1)*w,((k//cols)+1)*h)) for k in range(cols*rows)]

def qa(name,frames,indices,anchor,fps):
    dest=REPORT/name
    cmd=[sys.executable,str(INSPECT),"--sheet",str(ART/(name+".png")),
         "--cols",str(frames[0]),"--rows",str(frames[1]),
         "--indices",",".join(map(str,indices)),
         "--fps",str(fps),"--scale","3",
         "--anchor",str(anchor[0])+","+str(anchor[1]),
         "--out",str(dest)]
    subprocess.run(cmd,check=True,cwd=ROOT)
    data=json.loads((dest/"metrics.json").read_text())
    check(data["frame_count"]==len(indices),
          f"Animation QA skipped frames {name}")
    return len(data["warnings"])

def main():
    REPORT.mkdir(parents=True,exist_ok=True)
    results={}
    # The game's accepted canonical blond body/weapon/gait are immutable here.
    # All three professions reuse WalkVanguard for their exact canonical head.
    for name in ("WalkVanguard","WalkRanger","WalkArcanist"):
        path,frames=cell_frames(name,4,3)
        check(frames[0].size==(256,256),"Source hero foot-pivot cell changed")
        check(all(f.getchannel("A").getbbox() for f in frames),
              f"Blank authored walk pose in {name}")
        key=[hashlib.sha256(f.tobytes()).hexdigest() for f in frames[:4]]
        check(len(set(key))>=3,
              f"At least three real unique gait sprites required: {name}")
        results[name]={"unique_first_cycle":len(set(key)),
                       "warnings":qa(name,(4,3),list(range(4)),(128,240),8)}
    for name in ("PlayerVanguard","PlayerRanger","PlayerArcanist"):
        path,frames=cell_frames(name,4,3)
        check(frames[0].size==(256,256),"Action canvas/pivot changed")
        check(all(frames[k].getchannel("A").getbbox() for k in range(12)),
              f"Blank hero combat frame in {name}")
        results[name]={"warnings":qa(name,(4,3),[4,5,6,7],(128,240),8)}
    # These nine experimental replacements were rolled BACK FROM RUNTIME,
    # not deleted or declared permanently forbidden. Their source PNGs,
    # .meta GUIDs and prototype generators remain for later REPAINTS.
    # Do not hard-gate future artwork to 32px or byte-equal old pixels.
    optional=("MossboundRaider","CinderstoneGuard","BriarBeast",
              "EmberfallResidents","TerrainAccentRemaster",
              "CoastlineRemaster","CombatRhythmRemaster",
              "HeroMotionAccents","ActorContactShadows")
    for name in optional:
        path=ART/(name+".png")
        if not path.exists():
            results[name]={"candidate":"not currently installed"}
            continue
        with Image.open(path) as proposed:
            check(proposed.format=="PNG",f"Candidate should be valid PNG: {name}")
            check(proposed.width>0 and proposed.height>0,
                  f"Candidate PNG has no usable pixels: {name}")
            results[name]={"candidate":"editable; not visually approved",
                           "dimensions":list(proposed.size),
                           "format":proposed.format}
    # Production art must remain resolvable as a fallback, but no static
    # negative lookup or fixed resource hash prohibits an approved remake.
    for required in ("Terrain","TerrainVariants","WorldProps",
                     "FieldProps","Monsters","Bosses","Npcs",
                     "Effects","Icons"):
        path=ART/(required+".png")
        check(path.is_file(), "MISSING_PRODUCTION_FALLBACK_ART:"+required)
        # Bosses and WorldProps may use LFS pointers in the GitHub API,
        # but Actions checks out their real art via Git LFS.
        with Image.open(path) as actual:
            check(actual.width>0 and actual.height>0,
                  "INVALID_PRODUCTION_FALLBACK_ART:"+required)
    actor=(ROOT/"Assets/Scripts/Frontier/Runtime/FrontierActors.cs").read_text()
    for stable in ('root + "Npcs"','root + "Monsters"',
                   'root + "Bosses"','root + "Effects"',
                   'root + "Icons"'):
        check(stable in actor,"ORIGINAL_ART_FALLBACK_NO_LONGER_CONNECTED:"+stable)
    (REPORT/"summary.json").write_text(json.dumps(results,indent=2)+"\n")
    print("PASS canonical hero art and original detailed production sources; 9 retained candidates remain editable and opt-in")
    print("Sprite QA warnings are review candidates, NOT Unity native approval.")
if __name__=="__main__":main()
