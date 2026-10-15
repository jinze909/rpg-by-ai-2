#!/usr/bin/env python3
"""Release-blocking phase-2 whole-body sprite checks against shipped art.
The original model-produced PNG is compared at the raster level, not a warning.
"""
from pathlib import Path
import hashlib
import argparse
import subprocess
import sys
from PIL import Image, ImageChops
from generate_mossbound_raider import ROOT, STAGED, INSTALLED, CELL, PHASES, render

def inspect(image):
    assert image.size==(512,768), ("MOSSBOUND_INVALID_ATLAS_SIZE",image.size)
    assert image.mode=="RGBA", ("MOSSBOUND_NON_RGBA",image.mode)
    frames=[]
    for row,action in enumerate(PHASES):
        for col in range(4):
            frame=image.crop((col*CELL,row*CELL,(col+1)*CELL,(row+1)*CELL))
            bbox=frame.getchannel("A").getbbox()
            assert bbox is not None, ("MOSSBOUND_BLANK",action,col)
            assert bbox[0]>=2 and bbox[2]<=CELL-2 and bbox[1]>=2 and bbox[3]<=CELL-2, (
                "MOSSBOUND_CLIPPED_ACTOR",action,col,bbox)
            assert bbox[2]-bbox[0]>=42 and bbox[3]-bbox[1]>=62, (
                "MOSSBOUND_MISSING_FULL_BODY",action,col,bbox)
            # Root shadow and both boots must remain grounded inside one cell.
            assert frame.getpixel((64,118))[3]>0, ("MOSSBOUND_LOST_GROUND",action,col)
            frames.append(frame)
    for row,action in enumerate(PHASES):
        this=frames[row*4:row*4+4]
        hashes=[hashlib.sha256(f.tobytes()).digest() for f in this]
        assert len(set(hashes))==4,("MOSSBOUND_STALE_DUPLICATE_POSES",action)
        for i,(a,b) in enumerate(zip(this,this[1:])):
            changed=ImageChops.difference(a,b)
            assert changed.getbbox() is not None, ("MOSSBOUND_FROZEN_ACTION",action,i)
    assert len({hashlib.sha256(f.tobytes()).digest() for f in frames})==24, (
        "MOSSBOUND_CROSS_ACTION_FRAME_REUSE")
    return frames

def main():
    parser=argparse.ArgumentParser()
    parser.add_argument("--allow-unpublished",action="store_true")
    args=parser.parse_args()
    render(STAGED/"MossboundRaider.png")
    with Image.open(STAGED/"MossboundRaider.png") as im:
        expected=im.convert("RGBA").copy()
    inspect(expected)
    source=(ROOT/"Assets/Scripts/Frontier/Runtime/FrontierActors.cs").read_text()
    for contract in ('mossboundRaiderSheet', 'e.kind == 0',
                     'MossboundRaider', 'GameCore.GaitFrame(e.moveTime, 1.35f)',
                     'DrawFallen'):
        assert contract in source, ("MOSSBOUND_NOT_CONNECTED_TO_GAME",contract)
    if not INSTALLED.exists():
        assert args.allow_unpublished, "MOSSBOUND_SHIPPED_SPRITE_MISSING"
        print("MOSSBOUND_PENDING_ATOMIC_BINARY_PUBLICATION: release marker only")
        return
    with Image.open(INSTALLED) as im:
        actual=im.convert("RGBA").copy()
    inspect(actual)
    assert actual.tobytes() == expected.tobytes(), (
        "MOSSBOUND_SHIPPED_ASSET_DIFFERS_FROM_AUTHORED_SOURCE")
    meta=INSTALLED.with_suffix(".png.meta").read_text()
    for required in ("enableMipMap: 0","filterMode: 0",
                     "textureCompression: 0","isReadable: 1"):
        assert required in meta, ("MOSSBOUND_BAD_UNITY_IMPORT",required)
    # Prove the published-resource validator actually REJECTS altered frames.
    sabotaged=actual.copy()
    sabotaged.paste((0,0,0,0),(0,0,128,128))
    try:
        inspect(sabotaged)
    except AssertionError as error:
        assert "MOSSBOUND_BLANK" in str(error), error
    else:
        raise AssertionError("MOSSBOUND_MUTATED_PNG_WAS_ACCEPTED")
    print("PASS 24 distinct shipped Mossbound poses / strict source-raster equality")
    print("PASS injected missing whole-body frame rejected (hard fail)")
    print("NATIVE_UNVERIFIED / ART_REVIEW_REQUIRED: Unity Animator/GameView not executed")

if __name__=="__main__":main()
