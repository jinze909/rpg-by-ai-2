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
    # Hero motion accents are distinct 4-frame, 32px authored source poses:
    # sword follow-through, bow release, arcanist glyph and grounded footfalls.
    # Preserve accepted body atlases and verify production resource metadata.
    name="HeroMotionAccents"
    path,frames=cell_frames(name,4,4)
    check(frames[0].size==(32,32),"Hero motion accent canvas changed")
    hashes=[hashlib.sha256(frame.tobytes()).hexdigest() for frame in frames]
    check(len(set(hashes))==16,"Hero accent animation has duplicate cells")
    visible=[sum(1 for a in frame.getchannel("A").getdata() if a>0) for frame in frames]
    check(all(12<=n<=220 for n in visible),"Hero effects blank or too dense")
    meta=path.with_suffix(".png.meta").read_text()
    check("enableMipMap: 0" in meta and "filterMode: 0" in meta,
          "Hero motion sprite must use nearest neighbor / no mipmaps")
    results[name]={"unique":16,"min_opaque":min(visible),
                   "max_opaque":max(visible)}
    for row in range(4):
        results[name][f"row{row}_warnings"]=qa(name,(4,4),
            list(range(row*4,row*4+4)),(16,29),8)
    # Four explicit coastline orientations x four irregular authored edge
    # variants, placed ONLY on real water-to-land transitions by the renderer.
    coast="CoastlineRemaster"
    path,frames=cell_frames(coast,4,4)
    check(frames[0].size==(32,32),"Coastline edge canvas changed")
    hashes=[hashlib.sha256(frame.tobytes()).hexdigest() for frame in frames]
    check(len(set(hashes))==16,"Coastline directional cells repeat")
    visible=[sum(1 for a in frame.getchannel("A").getdata() if a>0)
             for frame in frames]
    check(all(20<=n<=220 for n in visible),
          "Coastline foam must be visible but not cover original mature water")
    meta=path.with_suffix(".png.meta").read_text()
    check("enableMipMap: 0" in meta and "filterMode: 0" in meta,
          "Coastline pixels require nearest neighbor/no mipmap")
    results[coast]={"unique":16,"min_opaque":min(visible),
                    "max_opaque":max(visible)}
    # Contact shadows have 4 four-frame/variant rows, and each is real
    # art with one tile root and no imported palette loss or blurred mipmaps.
    shade="ActorContactShadows"
    path,frames=cell_frames(shade,4,4)
    check(frames[0].size==(32,32),"Ground contact atlas canvas changed")
    signatures=[hashlib.sha256(f.tobytes()).hexdigest() for f in frames]
    check(len(set(signatures))==16,"Every grounded material needs unique pixels")
    coverage=[sum(1 for p in f.getchannel("A").getdata() if p>0)
              for f in frames]
    check(all(60<=n<=320 for n in coverage),
          "Shadow cells should stay legible but never occlude a whole terrain tile")
    meta=path.with_suffix(".png.meta").read_text()
    check("filterMode: 0" in meta and "enableMipMap: 0" in meta,
          "Pixel contact material must not be bilinear or mipmapped")
    results[shade]={"unique":16,"min_coverage":min(coverage),
                    "max_coverage":max(coverage)}
    for name in ("TerrainAccentRemaster","CombatRhythmRemaster"):
        path,frames=cell_frames(name,4,4)
        check(frames[0].size==(32,32),f"Remaster cells must remain 32px {name}")
        sig=[hashlib.sha256(im.tobytes()).hexdigest() for im in frames]
        check(len(set(sig))==16,f"Remaster has repeated or fake frames: {name}")
        alpha=[im.getchannel("A") for im in frames]
        check(all(5<a.getbbox()[2]*a.getbbox()[3] for a in alpha),
              f"An accent frame is blank: {name}")
        visible=[sum(1 for p in a.getdata() if p>0) for a in alpha]
        check(all(12<=n<=32*32*.35 for n in visible),
              f"Over-dense/invisible overlay would damage original terrain: {name}")
        meta=path.with_suffix(".png.meta").read_text()
        check("enableMipMap: 0" in meta and "filterMode: 0" in meta,
              f"Remaster art must remain nearest-neighbor/no-mip in Unity {name}")
        results[name]={"unique":len(set(sig)),
                       "min_opaque":min(visible),"max_opaque":max(visible)}
        if name=="CombatRhythmRemaster":
            for row in range(4):
                results[name][f"row{row}_warnings"]=qa(name,(4,4),
                    list(range(row*4,row*4+4)),(16,29),8)
    # Production-visual rollback gate: all three rich world source atlases
    # remain live; tiny unapproved decorative layers may be audited as
    # candidates but cannot obscure high-detail ground/river/props again.
    env=(ROOT/"Assets/Scripts/Frontier/Runtime/FrontierEnvironment.cs").read_text()
    runtime=(ROOT/"Assets/Scripts/Frontier/Runtime/FrontierRuntime.cs").read_text()
    order=(ROOT/"Assets/Scripts/Frontier/Runtime/FrontierWorldOrder.cs").read_text()
    surface=env.split("private void DrawSurface(",1)[1].split(
        "private void DrawRemasteredMaterial(",1)[0]
    world_draw=runtime.split("private void DrawWorld()",1)[1].split(
        "private void DrawHero()",1)[0]
    opacity=order.split("private float FadeWorldProp(",1)[1].split(
        "private void DrawOrderedWorld(",1)[0]
    check("Atlas(terrainSheet, 8, 2, id," in surface,
          "Accepted original Terrain art missing from live renderer")
    check("terrainVariants != null" in surface,
          "Accepted original terrain variants absent from live renderer")
    check("DrawRemasteredMaterial(x,y,tile,p,seed);" not in surface and
          "DrawRemasteredCoastline(x,y,tile,p,seed);" not in surface,
          "UNAPPROVED_LOW_DETAIL_TERRAIN_OR_WATER_OVERPAINT")
    check("DrawAnnexBiomeLayers(x, y, tile, p);" not in world_draw,
          "UNAPPROVED_BLOCKY_BIOME_OVERPAINT")
    check("return 1f;" in opacity and "EdgeOpacity(" not in opacity and
          "TerrainTileOpacity(" not in opacity,
          "VISUAL_REGRESSION: lamps, signs or foreground props faded away")
    for accepted in ("Terrain","TerrainVariants","WorldProps",
                     "FieldProps","Npcs","Effects","Icons"):
        check((ART/(accepted+".png")).is_file(),
              "MISSING_ACCEPTED_HIGH_DETAIL_ART:"+accepted)
    (REPORT/"summary.json").write_text(json.dumps(results,indent=2)+"\n")
    print("PASS shipped 3-class canonical blonde action/walk sheets, real 16+16 unique material/motion art cells and 4 staged attack animations")
    print("Sprite QA warnings are review candidates, NOT Unity native approval.")
if __name__=="__main__":main()
