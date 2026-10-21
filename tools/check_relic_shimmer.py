#!/usr/bin/env python3
"""Hard-gate the shipped 4-cell hand-directed relic light overlay.
The original FieldProps bodies remain untouched. This is offline art QA.
"""
from pathlib import Path
from PIL import Image

ROOT=Path(__file__).resolve().parents[1]
PNG=ROOT/"Assets/Resources/Frontier/Art/RelicShimmer.png"
META=Path(str(PNG)+".meta")
RUNTIME=ROOT/"Assets/Scripts/Frontier/Runtime/FrontierEnvironment.cs"
PALETTE=ROOT/"Assets/Resources/Frontier/Visual/WorldLook.txt"
TESTS=ROOT/"tests/RenderStateChecks.cs"

def inspect(art):
    assert art.size==(96,24),"RELIC_SOURCE_ATLAS_SIZE_CHANGED"
    keys=[]
    for i in range(4):
        pixels=list(art.crop((24*i,0,24*(i+1),24)).getdata())
        occupied=[(j%24,j//24,p) for j,p in enumerate(pixels) if p[3]>0]
        assert 18<len(occupied)<135,("RELIC_CELL_MISSING_OR_SOLID",i,len(occupied))
        xs=[p[0] for p in occupied];ys=[p[1] for p in occupied]
        assert min(xs)>=1 and max(xs)<=22 and min(ys)>=1 and max(ys)<=22,(
            "RELIC_SPRITE_CROPPED",i)
        assert len(set(p for _,_,p in occupied))>=5,(
            "RELIC_ART_FLAT_COLOR",i)
        assert any(p[3]>=245 for _,_,p in occupied),(
            "RELIC_CORE_GLOW_MISSING",i)
        keys.append(tuple(pixels))
    assert len(set(keys))==4,"RELIC_ANIMATED_POSES_DUPLICATED"

def main():
    assert PNG.is_file() and META.is_file(),"MISSING_RELIC_ART_OR_GUID"
    with Image.open(PNG) as img:
        assert img.format=="PNG","RELIC_SOURCE_NOT_PNG"
        source=img.convert("RGBA")
    inspect(source)
    # Adversarial QA must detect same frame pasted into all four slots.
    duplicated=Image.new("RGBA",(96,24))
    for i in range(4):
        duplicated.paste(source.crop((0,0,24,24)),(i*24,0))
    try:inspect(duplicated)
    except AssertionError:pass
    else:raise AssertionError("RELIC_ART_MUTATION_WAS_NOT_CAUGHT")
    meta=META.read_text()
    for item in ("guid: 9c782a684b234257ab143608f6d7c154",
        "enableMipMap: 0","isReadable: 0","filterMode: 0",
        "textureCompression: 0","alphaIsTransparency: 1"):
        assert item in meta,("RELIC_SPRITE_IMPORTER",item)
    runtime=RUNTIME.read_text()
    for item in ('"Frontier/Art/RelicShimmer"',
        "imported.width==96&&","imported.height==24",
        "Atlas(relicShimmerSheet,4,1,cell",
        "DrawAuthoredRelicShimmer(id,variant,foot,",
        "id==3||id==4||id==12||id==14",
        "Game.save.reducedMotion"):
        assert item in runtime,("RELIC_NOT_IN_LIVE_GAME",item)
    palette=PALETTE.read_text()
    assert "relicShimmerAlpha=0.24" in palette
    assert "relicShimmerRate=3.8" in palette
    tests=TESTS.read_text()
    assert "AuthoredRelicGlintsStayOnActualDetailedWorldProps(app,folder)" in tests
    combat=(ROOT/"tests/RuntimeCombatInputChecks.cs").read_text()
    assert "RenderStateChecks.VerifyRelicArtAndWorldIllumination," in combat, (
        "RELIC_VISUAL_ASSERTIONS_NEVER_EXECUTED_IN_REAL_CI")
    # Store the ACTUAL committed four-pose source, enlarged only for review.
    folder=ROOT/".starfall-checks/sprite-qa"
    folder.mkdir(parents=True,exist_ok=True)
    source.resize((96*6,24*6),Image.Resampling.NEAREST).save(
        folder/"relic-shimmer-painted-4-poses.png")
    print("PASS 4 independent translucent original pixel relic poses, point import GUID, six live world objects, adversarial art mutation and review atlas; NATIVE_UNVERIFIED")

if __name__=="__main__":
    main()
