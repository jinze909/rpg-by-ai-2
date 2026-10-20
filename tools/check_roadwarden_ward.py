#!/usr/bin/env python3
"""Source-pixel, Unity importer and production-use gate for Roadwarden ward art."""
from pathlib import Path
import runpy
from PIL import Image

ROOT=Path(__file__).resolve().parents[1]
ART=ROOT/"Assets/Resources/Frontier/Art/RoadwardenWard.png"

def validate(im):
    assert im.mode=="RGBA" and im.size==(256,128),"ROADWARDEN_WARD_WRONG_LAYOUT"
    frames=[im.crop((x*64,y*64,(x+1)*64,(y+1)*64))
            for y in range(2) for x in range(4)]
    assert len({frame.tobytes() for frame in frames})==8,"ROADWARDEN_WARD_DUPLICATE_KEYPOSE"
    for number,frame in enumerate(frames):
        pixels=list(frame.getdata())
        ink=sum(px[3]>0 for px in pixels)
        assert 390<=ink<=550,("ROADWARDEN_WARD_EMPTY_OR_FILLED",number,ink)
        assert frame.getpixel((32,8))[3]>0,("ROADWARDEN_WARD_MISSING_TOP_ANCHOR",number)
        assert frame.getpixel((32,56))[3]>0,("ROADWARDEN_WARD_MISSING_BOTTOM_ANCHOR",number)
    stable=frames[0].getpixel((10,32))
    critical=frames[4].getpixel((10,32))
    assert frames[0].tobytes()!=frames[4].tobytes(),"ROADWARDEN_WARD_MISSING_DAMAGE_STATE"
    return frames

def main():
    shipped=Image.open(ART)
    frames=validate(shipped)
    generated=runpy.run_path(str(ROOT/"tools/generate_roadwarden_ward.py"))["build"]()
    assert shipped.tobytes()==generated.tobytes(),"ROADWARDEN_WARD_ART_SOURCE_MISMATCH"
    counterfeit=shipped.copy()
    counterfeit.paste(frames[0],(64,0))
    try:validate(counterfeit)
    except AssertionError as exc:
        assert "DUPLICATE_KEYPOSE" in str(exc),exc
    else:raise AssertionError("ROADWARDEN_WARD_DUPLICATE_MUTATION_ESCAPED")
    meta=ART.with_suffix(".png.meta").read_text()
    for marker in ("guid: 0e52adef5fdb4a239e11c0d5f6fa8871",
                   "enableMipMap: 0","filterMode: 0",
                   "alphaIsTransparency: 1","textureCompression: 0"):
        assert marker in meta,("ROADWARDEN_WARD_BAD_IMPORTER",marker)
    ui=(ROOT/"Assets/Scripts/Frontier/Runtime/FrontierPolishUI.cs").read_text()
    runtime=(ROOT/"Assets/Scripts/Frontier/Runtime/FrontierRuntime.cs").read_text()
    for marker in ('"Frontier/Art/RoadwardenWard"',
                   'DrawAuthoredRoadwardenWard',
                   'Atlas(roadwardenWardSheet,4,2,row*4+phase',
                   'Game.save.reducedMotion',
                   'Game.RoadwardenWardIntegrity',
                   'Game.RoadwardenWardUnderAttack'):
        assert marker in ui,("ROADWARDEN_WARD_NOT_IN_LIVE_RENDERER",marker)
    assert 'DrawOrderedWorld();' in runtime and 'DrawPolishObject(o)' in runtime,(
        "ROADWARDEN_WARD_MISSING_PRODUCTION_SCENE")
    workflow=(ROOT/".github/workflows/core-tests.yml").read_text()
    assert 'python3 tools/check_roadwarden_ward.py' in workflow
    print("PASS eight source-authored stable/critical ward sprites, importer, live world and adversarial pixel mutation")

if __name__=='__main__':
    main()
