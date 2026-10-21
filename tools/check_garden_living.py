#!/usr/bin/env python3
"""Gate 8 individually illustrated, planted Moonherb growth frames in actual world."""
from pathlib import Path
from PIL import Image
import runpy
ROOT=Path(__file__).resolve().parents[1]
ART=ROOT/"Assets/Resources/Frontier/Art/GardenLiving.png"

def check(im):
    assert im.mode=="RGBA" and im.size==(128,64),("GARDEN_ATLAS_SIZE",im.size)
    frames=[]
    for row in range(2):
        for frame in range(4):
            cell=im.crop((frame*32,row*32,(frame+1)*32,(row+1)*32))
            box=cell.getchannel("A").getbbox()
            filled=[v for v in cell.getdata() if v[3]>0]
            assert 105<=len(filled)<=225 and box is not None,(
                "GARDEN_NOT_FULLY_ILLUSTRATED",row,frame,len(filled))
            assert box[0]>=3 and box[2]<=31 and box[3]==30 and box[1]>=3,(
                "GARDEN_FOOT_ANCHOR_OR_SOURCE_EDGE",row,frame,box)
            assert len(set(filled))>=4,("GARDEN_COLOR_DETAIL_LOST",row,frame)
            frames.append(cell.tobytes())
    assert len(set(frames))==8,"GARDEN_POSES_DUPLICATED"
    # Four mature poses contain source-authored flower ink absent in sprouts.
    assert all(any(p[0]>200 and p[1]>130 and p[2]>100 and p[3]>0
        for p in im.crop((i*32,32,(i+1)*32,64)).getdata())
        for i in range(4)),"GARDEN_MATURE_BLOOMS_NOT_PAINTED"

def main():
    assert ART.is_file(),"GARDEN_REAL_PRODUCTION_PNG_MISSING"
    with Image.open(ART) as image:
        assert image.format=="PNG"
        art=image.convert("RGBA")
    check(art)
    generated=runpy.run_path(str(ROOT/"tools/generate_garden_living.py"))["build"]()
    assert generated.tobytes()==art.tobytes(),(
        "GARDEN_SOURCE_ORIGINAL_ART_DIFFERENT_FROM_SHIPPED_PNG")
    corrupted=art.copy()
    corrupted.paste(art.crop((0,0,32,32)),(32,0))
    try:check(corrupted)
    except AssertionError as exc:
        assert "GARDEN_POSES_DUPLICATED" in str(exc),exc
    else:raise AssertionError("GARDEN_DUPLICATE_MUTATION_FALSE_PASS")
    meta=Path(str(ART)+".meta").read_text()
    for token in ("guid: 9e30f64a204b4d5ba940d9c7494e9ff6",
                  "filterMode: 0","enableMipMap: 0",
                  "alphaIsTransparency: 1","textureCompression: 0"):
        assert token in meta,("GARDEN_UNITY_SPRITE_IMPORTER_REGRESSION",token)
    world=(ROOT/"Assets/Scripts/Frontier/Runtime/FrontierWorldOrder.cs").read_text()
    runtime=(ROOT/"Assets/Scripts/Frontier/Runtime/FrontierGardenLiving.cs").read_text()
    assert 'if(prop.kind=="garden")' in world and (
        "DrawAuthoredLivingGarden(prop,p,variant,tint);" in world)
    for needle in ('"Frontier/Art/GardenLiving"',"growthStage<=0",
        "incoming.width==128&&","incoming.height==64",
        "Game.save.reducedMotion?0:","stage*4+frame","Atlas(livingGardenSheet,4,2",
        "gardenLivingAlpha","gardenLivingFrameRate"):
        assert needle in runtime,("GARDEN_NOT_LIVE_OR_BROKEN",needle)
    checks=(ROOT/"tests/RuntimeCombatInputChecks.cs").read_text()
    workflow=(ROOT/".github/workflows/core-tests.yml").read_text()
    assert "RenderStateChecks.VerifyLivingGardenInProduction" in checks
    assert "python3 tools/check_garden_living.py" in workflow
    (ROOT/".starfall-checks/sprite-qa").mkdir(parents=True,exist_ok=True)
    art.resize((768,384),Image.Resampling.NEAREST).save(
        ROOT/".starfall-checks/sprite-qa"/"review-original-garden-moonherb-growth.png")
    print("PASS 8 independent planted garden growth poses, fixed soil root, source reproducibility, original WorldProps production path and Sprite import GUID")
if __name__=="__main__":main()
