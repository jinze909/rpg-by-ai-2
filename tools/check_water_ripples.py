#!/usr/bin/env python3
"""Hard gate actual shipped water-ripple RGBA art + scene runtime integration."""
from pathlib import Path
from PIL import Image
import runpy

ROOT=Path(__file__).resolve().parents[1]
PATH=ROOT/"Assets/Resources/Frontier/Art/WaterRipples.png"
META=PATH.with_suffix(".png.meta")
SRC=ROOT/"Assets/Scripts/Frontier/Runtime/FrontierEnvironment.cs"
RUNTIME=ROOT/"Assets/Scripts/Frontier/Runtime/FrontierRuntime.cs"
ACTORS=ROOT/"Assets/Scripts/Frontier/Runtime/FrontierActors.cs"
WORKFLOW=ROOT/".github/workflows/core-tests.yml"
TESTS=ROOT/"tests/RuntimeCombatInputChecks.cs"

def cells(image):
    return [image.crop((x*24,y*16,(x+1)*24,(y+1)*16))
            for y in range(2) for x in range(4)]

def validate(image):
    assert image.mode=="RGBA" and image.size==(96,32),"RIPPLE_SHEET_BAD_FORMAT"
    poses=cells(image)
    pixels=[p.tobytes() for p in poses]
    assert len(set(pixels))==8,"RIPPLE_DUPLICATED_OR_REUSED_FRAMES"
    for i,p in enumerate(poses):
        alpha=[v[3] for v in p.getdata()]
        nonzero=sum(bool(a) for a in alpha)
        assert 18<=nonzero<=80,("RIPPLE_POSE_EMPTY_OR_FILLED",i,nonzero)
        assert alpha.count(0)>=24*16-80,("RIPPLE_NOT_TRANSPARENT",i)
        assert any(0<a<255 for a in alpha),("RIPPLE_NO_ARTIST_ALPHA",i)
    # Two truly different painted palette rows, not one palette tinted at render time.
    assert poses[0].getpixel((9,7))!=poses[4].getpixel((9,7)),"RIPPLE_NO_COLD_PALETTE"

def main():
    shipped=Image.open(PATH).convert("RGBA")
    validate(shipped)
    painted=runpy.run_path(str(ROOT/"tools/generate_water_ripples.py"))["build"]()
    assert shipped.tobytes()==painted.tobytes(),"SHIPPED_RIPPLES_DIFFER_FROM_AUTHORED_SOURCE"
    forged=shipped.copy()
    forged.paste(cells(shipped)[0],(24,0))
    try:validate(forged)
    except AssertionError as error:
        assert "RIPPLE_DUPLICATED" in str(error),error
    else:raise AssertionError("DUPLICATE_RIPPLE_MUTATION_ESCAPED_QA")
    meta=META.read_text()
    for item in ("guid: bf3da10e74054e788201abb3b0f481ce",
                 "enableMipMap: 0","filterMode: 0","wrapU: 1",
                 "alphaIsTransparency: 1","textureCompression: 0"):
        assert item in meta,("RIPPLE_IMPORT_SETTINGS",item)
    src=SRC.read_text()
    for item in ("DrawAuthoredWaterRipples",'"Frontier/Art/WaterRipples"',
                 "waterRipplesSheet.width!=96","candidate.width==96&&candidate.height==32",
                 "Game.save.reducedMotion","IsSurfaceWater(tile,Game.save.room,Game.LowTide)",
                 "Atlas(waterRipplesSheet,4,2,row*4+frame"):
        assert item in src,("RIPPLE_NOT_IN_PRODUCTION",item)
    assert "DrawAuthoredWaterRipples(x,y,tile,p);" in RUNTIME.read_text()
    assert 'root + "WaterRipples"' in ACTORS.read_text()
    assert "VerifyOriginalWaterRippleArtAndLiveTide" in TESTS.read_text()
    assert "python3 tools/check_water_ripples.py" in WORKFLOW.read_text()
    output=ROOT/".starfall-checks/sprite-qa"
    output.mkdir(parents=True,exist_ok=True)
    shipped.save(output/"source-two-biome-water-ripples.png")
    print("PASS eight genuinely distinct hand-painted water ripple cells, cold/warm palettes, partial-import safety and real DrawWorld integration")

if __name__=="__main__":
    main()
