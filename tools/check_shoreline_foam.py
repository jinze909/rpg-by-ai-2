#!/usr/bin/env python3
"""Required CI: one real committed pixel atlas, not an isolated preview."""
from pathlib import Path
import runpy
from PIL import Image
ROOT=Path(__file__).resolve().parents[1]
SRC=ROOT/"Assets/Resources/Frontier/Art/ShorelineFoam.png"
META=Path(str(SRC)+".meta")
ENV=(ROOT/"Assets/Scripts/Frontier/Runtime/FrontierEnvironment.cs").read_text()
ACTOR=(ROOT/"Assets/Scripts/Frontier/Runtime/FrontierActors.cs").read_text()
TEST=(ROOT/"tests/RuntimeCombatInputChecks.cs").read_text()

def check(image):
    assert image.size==(128,12),"FOAM_ATLAS_DIMENSIONS"
    allcells=[]
    for k in range(4):
        data=list(image.crop((32*k,0,32*(k+1),12)).getdata())
        opaque=[(i%32,i//32,rgba) for i,rgba in enumerate(data) if rgba[3]]
        assert 40<=len(opaque)<=100,("FOAM_POSE_INCOMPLETE",k,len(opaque))
        assert min(x for x,_,_ in opaque)>=1 and max(x for x,_,_ in opaque)<=30
        assert min(y for _,y,_ in opaque)>=1 and max(y for _,y,_ in opaque)<=10
        assert len({rgba for _,_,rgba in opaque})>=5
        assert any(rgba[3]>=245 for _,_,rgba in opaque)
        allcells.append(tuple(data))
    assert len(set(allcells))==4,"FOAM_NOT_FOUR_DISTINCT_PAINTINGS"

def main():
    assert SRC.exists() and META.exists()
    with Image.open(SRC) as file:
        assert file.format=="PNG" and file.mode=="RGBA"
        image=file.copy()
    check(image)
    renderer=runpy.run_path(str(ROOT/"tools/generate_shoreline_foam.py"))
    assert renderer["build"]().tobytes()==image.tobytes(),(
        "SHIPPED_FOAM_DIFFERS_FROM_REPRODUCIBLE_PAINTED_SOURCE")
    copied=Image.new("RGBA",(128,12))
    for k in range(4):
        copied.paste(image.crop((0,0,32,12)),(k*32,0))
    try:check(copied)
    except AssertionError:pass
    else:raise AssertionError("FOAM_FRAME_DUPLICATION_MUTATION_UNDETECTED")
    for marker in ("guid: 6f34957e7da5489aab74c6ee86eaa13c",
        "filterMode: 0","enableMipMap: 0","isReadable: 0",
        "textureCompression: 0","alphaIsTransparency: 1"):
        assert marker in META.read_text(),("FOAM_IMPORTER",marker)
    for marker in ('"ShorelineFoam"','shorelineFoamSheet = Resources.Load'):
        assert marker in ACTOR,("FOAM_NOT_LOADED_AT_RUNTIME",marker)
    for marker in ("new Rect(cx-16f,cy-6f,32f,12f)",
        "shorelineFoamSheet,foamUv","if(paintedFoam&&foamAlpha>.001f)",
        "Game.save.reducedMotion","IsSurfaceWater(tile,Game.save.room,Game.LowTide)"):
        assert marker in ENV,("FOAM_NOT_IN_REAL_TIDE_WATER_PATH",marker)
    assert "RenderStateChecks.VerifyPaintedShorelineFoamInLiveWorld," in TEST,(
        "FOAM_RENDER_REGRESSION_NOT_IN_EXECUTABLE_CI")
    folder=ROOT/".starfall-checks/sprite-qa"
    folder.mkdir(parents=True,exist_ok=True)
    image.resize((128*6,12*6),Image.Resampling.NEAREST).save(
        folder/"painted-shoreline-foam-source-4-cells.png")
    print("PASS four unique real painted foam pixel poses, negative duplicate mutation, importer GUID and true four-bank gameplay renderer")

if __name__=="__main__":
    main()
