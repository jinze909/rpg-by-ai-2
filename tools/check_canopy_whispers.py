#!/usr/bin/env python3
"""Check shipped non-tiled source-illustrated canopy motion + production wiring."""
from pathlib import Path
import runpy
from PIL import Image

ROOT=Path(__file__).resolve().parents[1]
ART=ROOT/'Assets/Resources/Frontier/Art/CanopyWhispers.png'

def validate(sheet):
    assert sheet.mode=='RGBA' and sheet.size==(256,96),'CANOPY_SOURCE_DIMENSIONS'
    frames=[sheet.crop((x*32,y*32,(x+1)*32,(y+1)*32))
            for y in range(3) for x in range(8)]
    assert len({frame.tobytes() for frame in frames})==24,'CANOPY_DUPLICATE_FRAME'
    for index,frame in enumerate(frames):
        alpha=frame.getchannel('A')
        pixels=sum(int(p>0) for p in alpha.getdata())
        assert 40<=pixels<=205,('CANOPY_EMPTY_OR_OPAQUE',index,pixels)
        bounds=alpha.getbbox()
        assert bounds and bounds[0]>=1 and bounds[1]>=1 and bounds[2]<=31 and bounds[3]<=31,('CANOPY_CLIPPED',index)
        assert len({p for p in frame.getdata() if p[3]>0})>=3,('CANOPY_FLAT_FILL',index)
    assert frames[0].getpixel((6,9))!=frames[8].getpixel((6,9)),'CANOPY_BIOME_PALETTE'
    return frames

def main():
    published=Image.open(ART).convert('RGBA')
    frames=validate(published)
    painted=runpy.run_path(str(ROOT/'tools/generate_canopy_whispers.py'))['build']()
    assert painted.tobytes()==published.tobytes(),'CANOPY_PUBLISHED_SOURCE_DRIFT'
    duplicate=published.copy()
    duplicate.paste(frames[0],(32,0))
    try:validate(duplicate)
    except AssertionError as error:
        assert 'CANOPY_DUPLICATE_FRAME' in str(error),error
    else:raise AssertionError('CANOPY_NEGATIVE_MUTATION_ESCAPED')
    meta=ART.with_suffix('.png.meta').read_text()
    for marker in ('guid: d91b49c28e0d4f31abc6f17eae840612',
                   'enableMipMap: 0','filterMode: 0',
                   'alphaIsTransparency: 1','textureCompression: 0'):
        assert marker in meta,('CANOPY_UNITY_IMPORT',marker)
    src=(ROOT/'Assets/Scripts/Frontier/Runtime/FrontierEnvironment.cs').read_text()
    order=(ROOT/'Assets/Scripts/Frontier/Runtime/FrontierWorldOrder.cs').read_text()
    look=(ROOT/'Assets/Scripts/Frontier/Runtime/FrontierWorldVisualLook.cs').read_text()
    for marker in ('DrawAuthoredCanopyWhispers', '"Frontier/Art/CanopyWhispers"',
                   'canopyWhispersSheet,8,3,row*8+frame',
                   'Game.save.reducedMotion', 'candidate.width==256',
                   'candidate.height==96'):
        assert marker in src,('CANOPY_NOT_LIVE',marker)
    assert 'DrawAuthoredCanopyWhispers(prop,p,side,tint);' in order,'CANOPY_NOT_DEPTH_SORTED'
    assert 'DrawRootedPlant(prop,rect,tint);' in order,'CANOPY_REMOVED_ACCEPTED_TREE'
    assert 'canopyWhisperAlpha' in look and 'canopyWhisperFrameRate' in look,'CANOPY_NO_BOUNDED_LOOK'
    assert 'VerifyCanopyWhispersInProduction(app);' in (ROOT/'tests/RenderStateChecks.cs').read_text(),'CANOPY_NO_EXECUTED_TEST'
    assert 'python3 tools/check_canopy_whispers.py' in (ROOT/'.github/workflows/core-tests.yml').read_text(),'CANOPY_NO_CI'
    target=ROOT/'.starfall-checks/sprite-qa'
    target.mkdir(parents=True,exist_ok=True)
    published.save(target/'canopy-whispers-original-24-frame-sheet.png')
    print('PASS 24 source-authored green/coastal/snow canopy pixel cells, mutation rejection, Unity GUID and actual sorted runtime wire')

if __name__=='__main__':main()
