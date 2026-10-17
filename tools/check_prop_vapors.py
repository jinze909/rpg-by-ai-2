#!/usr/bin/env python3
"""Eight original campfire/altar vapor cell pixels and production integration."""
from pathlib import Path
from PIL import Image
import runpy
ROOT=Path(__file__).resolve().parents[1]
ART=ROOT/"Assets/Resources/Frontier/Art/PropVapors.png"
def validate(im):
    assert im.mode=="RGBA" and im.size==(128,96),("VAPOR_DIMENSIONS",im.size)
    frames=[]
    for row in range(2):
        for phase in range(4):
            cell=im.crop((phase*32,row*48,(phase+1)*32,(row+1)*48))
            alpha=cell.getchannel("A")
            bounds=alpha.getbbox()
            opaque=[p for p in cell.getdata() if p[3]>0]
            assert bounds is not None and 175<len(opaque)<360,(
                "VAPOR_SPARSE_OR_SOLID",row,phase,len(opaque))
            assert bounds[0]>=4 and bounds[2]<=29 and bounds[1]>=5 and bounds[3]<=46,(
                "VAPOR_TOUCHES_FRAME",row,phase,bounds)
            assert 5<=len(set(opaque))<18,(
                "VAPOR_LOST_PAINT_DETAIL",row,phase)
            frames.append(cell.tobytes())
    assert len(set(frames))==8,"VAPOR_REUSED_OR_FLAT_CELLS"

def main():
    assert ART.exists(),"VAPOR_ORIGINAL_ART_MISSING"
    with Image.open(ART) as handle:
        assert handle.format=="PNG"
        art=handle.convert("RGBA")
    validate(art)
    generated=runpy.run_path(str(ROOT/"tools/generate_prop_vapors.py"))["build"]()
    assert generated.tobytes()==art.tobytes(),"SHIPPED_VAPOR_DIFFERS_FROM_SOURCE"
    broken=art.copy()
    broken.paste(art.crop((0,0,32,48)),(32,0))
    try: validate(broken)
    except AssertionError as e: assert "VAPOR_REUSED" in str(e),e
    else: raise AssertionError("VAPOR_DUPLICATE_MUTATION_NOT_REJECTED")
    meta=ART.with_suffix(".png.meta").read_text()
    for token in ("guid: 78163ac415f748bca8d973e72f03de51",
                  "enableMipMap: 0","filterMode: 0",
                  "alphaIsTransparency: 1","textureCompression: 0"):
        assert token in meta,("VAPOR_UNITY_META",token)
    environment=(ROOT/"Assets/Scripts/Frontier/Runtime/FrontierEnvironment.cs").read_text()
    for token in ('"Frontier/Art/PropVapors"',
        "candidate.width==128&&","candidate.height==96",
        "Atlas(propVaporsSheet,4,2,row*4+frame,plume,",
        "DrawAuthoredPropVapors(id,foot,Math.Max(w,h)",
        "Game.save.reducedMotion","Game.save.effectsQuality<2"):
        assert token in environment,("VAPOR_NOT_IN_REAL_WORLD",token)
    workflow=(ROOT/".github/workflows/core-tests.yml").read_text()
    assert "python3 tools/check_prop_vapors.py" in workflow
    checks=(ROOT/"tests/RuntimeCombatInputChecks.cs").read_text()
    assert "VerifyPropVaporsInProduction" in checks
    folder=ROOT/".starfall-checks/sprite-qa"
    folder.mkdir(parents=True,exist_ok=True)
    art.resize((768,576),Image.Resampling.NEAREST).save(
        folder/"review-original-two-prop-vapor-families.png")
    print("PASS 8 independent warm/cold 32x48 vapor paintings, source reproducibility, GUID, production runtime hooks and CI")
if __name__=="__main__": main()
