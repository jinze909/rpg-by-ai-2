#!/usr/bin/env python3
"""Gate the genuine 8-frame cold/warm ruin light painting and LIVE scene use."""
from pathlib import Path
from PIL import Image
import runpy

ROOT=Path(__file__).resolve().parents[1]
ART=ROOT/"Assets/Resources/Frontier/Art/RuinSigils.png"

def validate(image):
    assert image.mode=="RGBA" and image.size==(128,64),"RUIN_GLYPH_ATLAS_DIMS"
    cells=[image.crop((i*32,j*32,(i+1)*32,(j+1)*32))
           for j in range(2) for i in range(4)]
    assert len(set(p.tobytes() for p in cells))==8,"RUIN_GLYPH_DUPLICATED_CELLS"
    for i,p in enumerate(cells):
        nonzero=sum(x[3]>0 for x in p.getdata())
        assert 90<=nonzero<=150,("RUIN_GLYPH_EMPTY_OR_OPAQUE",i,nonzero)
        bounds=p.getchannel("A").getbbox()
        assert bounds and bounds[0]>=2 and bounds[1]>=5 and bounds[2]<=30,(
            "RUIN_GLYPH_CLIPPED",i,bounds)
        assert any(0<a<255 for a in p.getchannel("A").getdata()),(
            "RUIN_GLYPH_NO_PAINTED_ALPHA",i)
    assert cells[0].tobytes()!=cells[4].tobytes(),"RUIN_GLYPH_BIOMES_IDENTICAL"
    return cells

def main():
    published=Image.open(ART).convert("RGBA")
    cells=validate(published)
    original=runpy.run_path(str(ROOT/"tools/generate_ruin_sigils.py"))["build"]()
    assert original.tobytes()==published.tobytes(),"RUIN_GLYPH_SOURCE_DRIFT"
    forged=published.copy();forged.paste(cells[0],(32,0))
    try:validate(forged)
    except AssertionError as error:
        assert "RUIN_GLYPH_DUPLICATED" in str(error),error
    else:raise AssertionError("RUIN_GLYPH_MUTATION_FALSE_PASS")
    meta=ART.with_suffix(".png.meta").read_text()
    for required in ("guid: d6a197e831cf43f58373c7422a29d8b6",
        "enableMipMap: 0","filterMode: 0","alphaIsTransparency: 1",
        "textureCompression: 0"):
        assert required in meta,("RUIN_GLYPH_UNITY_IMPORT",required)
    scene=(ROOT/"Assets/Scripts/Frontier/Runtime/FrontierWorldOrder.cs").read_text()
    runtime=(ROOT/"Assets/Scripts/Frontier/Runtime/FrontierEnvironment.cs").read_text()
    visual=(ROOT/"Assets/Scripts/Frontier/Runtime/FrontierWorldVisualLook.cs").read_text()
    for marker in ('"Frontier/Art/RuinSigils"',"DrawAuthoredRuinSigils",
                   "candidate.width==128&&candidate.height==64",
                   "ruinSigilsSheet,4,2,row*4+frame",
                   "Game.save.reducedMotion","Game.save.effectsQuality==0"):
        assert marker in runtime,("RUIN_GLYPH_NOT_LIVE",marker)
    assert 'DrawAuthoredRuinSigils(prop,p,tint);' in scene
    assert 'PropImage(prop.kind,variant,rect.x,rect.y,rect.width,rect.height,tint);' in scene
    assert "ruinSigilAlpha" in visual and "ruinSigilFrameRate" in visual
    assert "VerifyRuinSigilsInProduction(app);" in (
        ROOT/"tests/RenderStateChecks.cs").read_text()
    assert "python3 tools/check_ruin_sigils.py" in (
        ROOT/".github/workflows/core-tests.yml").read_text()
    output=ROOT/".starfall-checks/sprite-qa"
    output.mkdir(parents=True,exist_ok=True)
    published.save(output/"ruin-sigils-original-two-palette-8-frame.png")
    print("PASS 8 original distinct rune Sprite cells, two material palettes, UV/source integrity, Unity importer and sorted world scene")

if __name__=="__main__": main()
