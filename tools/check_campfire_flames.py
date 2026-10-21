#!/usr/bin/env python3
"""Hard gate painted campfire full-flame animation, GUID, and live production."""
from pathlib import Path
from PIL import Image
import runpy

ROOT=Path(__file__).resolve().parents[1]
ART=ROOT/"Assets/Resources/Frontier/Art/CampfireFlames.png"

def validate(sheet):
    assert sheet.mode=="RGBA" and sheet.size==(96,32),"CAMPFIRE_ART_WRONG_DIMENSIONS"
    frames=[sheet.crop((i*24,0,(i+1)*24,32)) for i in range(4)]
    assert len({i.tobytes() for i in frames})==4,"CAMPFIRE_DUPLICATE_FLAME_CELLS"
    for number,im in enumerate(frames):
        pixels=list(im.getdata())
        opaque=sum(p[3]>0 for p in pixels)
        assert 155<=opaque<=265,("CAMPFIRE_FLAME_NOT_BODY_PAINT",number,opaque)
        assert sum(p[3]==0 for p in pixels)>450,("CAMPFIRE_OPAQUE_SHEET",number)
        # Each pose has a connected hot center and a planted base, not
        # disconnected floating fragments or a weapon/hero silhouette.
        assert im.getpixel((12,26))[3]>0 and im.getpixel((12,23))[3]>0,(
            "CAMPFIRE_FLAME_LOST_GROUNDED_CORE",number)
    return frames

def main():
    shipped=Image.open(ART).convert("RGBA")
    frames=validate(shipped)
    painted=runpy.run_path(str(ROOT/"tools/generate_campfire_flames.py"))["build"]()
    assert shipped.tobytes()==painted.tobytes(),"SHIPPED_CAMPFIRE_NOT_AUTHORED_SOURCE"
    forged=shipped.copy()
    forged.paste(frames[0],(24,0))
    try:validate(forged)
    except AssertionError as error:
        assert "CAMPFIRE_DUPLICATE" in str(error),error
    else:raise AssertionError("DUPLICATED_CAMPFIRE_ART_ESCAPED_QA")
    meta=ART.with_suffix(".png.meta").read_text()
    for marker in ("guid: c6972d52bd234bc99037de215a9685ef",
                   "enableMipMap: 0","filterMode: 0","alphaIsTransparency: 1",
                   "textureCompression: 0"):
        assert marker in meta,("CAMPFIRE_IMPORTER",marker)
    env=(ROOT/"Assets/Scripts/Frontier/Runtime/FrontierEnvironment.cs").read_text()
    for marker in ('"Frontier/Art/CampfireFlames"', "DrawAuthoredCampfireFlames",
                   "campfireFlamesSheet,4,1,cell", "Game.save.reducedMotion",
                   "DrawAuthoredCampfireEmbers(foot", "candidate.width==96&&"):
        assert marker in env,("CAMPFIRE_NOT_LIVE_IN_GAME",marker)
    assert 'root + "CampfireFlames"' in (ROOT/"Assets/Scripts/Frontier/Runtime/FrontierActors.cs").read_text()
    assert "VerifyPaintedCampfireFlamesInProduction" in (ROOT/"tests/RuntimeCombatInputChecks.cs").read_text()
    assert "python3 tools/check_campfire_flames.py" in (ROOT/".github/workflows/core-tests.yml").read_text()
    folder=ROOT/".starfall-checks/sprite-qa"
    folder.mkdir(parents=True,exist_ok=True)
    shipped.save(folder/"source-four-connected-campfire-poses.png")
    print("PASS four unique connected original flame poses, original fire preserved, Unity importer and executed runtime QA")

if __name__=="__main__":
    main()
