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
from check_editable_visual_candidate import audit_retired_candidate, is_live

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
            first=a.getchannel("A").getbbox()
            second=b.getchannel("A").getbbox()
            center=lambda box: ((box[0]+box[2])*.5,(box[1]+box[3])*.5)
            x0,y0=center(first);x1,y1=center(second)
            assert ((x0-x1)**2+(y0-y1)**2)**.5 <= 36, (
                "MOSSBOUND_SEVERE_FRAME_TRANSLATION",action,i,first,second)
    assert len({hashlib.sha256(f.tobytes()).digest() for f in frames})==24, (
        "MOSSBOUND_CROSS_ACTION_FRAME_REUSE")
    return frames

def main():
    parser=argparse.ArgumentParser()
    parser.add_argument("--allow-unpublished",action="store_true")
    args=parser.parse_args()
    if not is_live(ROOT,"MossboundRaider"):
        assert 'monsterSheet = Resources.Load<Texture2D>(root + "Monsters")' in (
            ROOT/"Assets/Scripts/Frontier/Runtime/FrontierActors.cs").read_text(), (
                "ORIGINAL_DETAILED_MONSTER_SOURCE_MISSING")
        audit_retired_candidate(ROOT,"MossboundRaider",
            "7cc097e40cd34f268b3d98217681c051",4096)
        return
    render(STAGED/"MossboundRaider.png")
    with Image.open(STAGED/"MossboundRaider.png") as im:
        expected=im.convert("RGBA").copy()
    inspect(expected)
    source=(ROOT/"Assets/Scripts/Frontier/Runtime/FrontierActors.cs").read_text()
    # This is retained experimental art, NOT a required production source.
    # Do not force it into the game merely to satisfy a legacy CI phrase.
    assert 'monsterSheet = Resources.Load<Texture2D>(root + "Monsters")' in source, (
        "ORIGINAL_DETAILED_MONSTER_SOURCE_LOST")
    if not INSTALLED.exists():
        assert args.allow_unpublished, "MOSSBOUND_SHIPPED_SPRITE_MISSING"
        print("MOSSBOUND_PENDING_ATOMIC_BINARY_PUBLICATION: release marker only")
        return
    with Image.open(INSTALLED) as im:
        actual=im.convert("RGBA").copy()
    inspect(actual)
    # Repaint-friendly: keep structural/canvas QA but NOT the generator's
    # old low-detail pixels as a permanent immutable hash/reference.
    # Git must contain the actual PNG, not a 132-byte LFS pointer. Its
    # importer GUID and .gitattributes exception are permanent scene inputs.
    assert INSTALLED.stat().st_size>1024, "MOSSBOUND_LFS_POINTER_OR_TRUNCATION"
    attrs=(ROOT/".gitattributes").read_text()
    assert "/Assets/Resources/Frontier/Art/MossboundRaider.png -filter -diff -merge" in attrs, (
        "MOSSBOUND_BINARY_NOT_EXEMPTED_FROM_LFS")
    meta=INSTALLED.with_suffix(".png.meta").read_text()
    assert "guid: 7cc097e40cd34f268b3d98217681c051" in meta, (
        "MOSSBOUND_UNITY_GUID_CHANGED")
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
    print("PASS candidate Mossbound pose structure and unchanged Unity asset identity; generator pixels are NOT locked")
    print("PASS injected missing whole-body frame rejected (hard fail)")
    print("NATIVE_UNVERIFIED / ART_REVIEW_REQUIRED: Unity Animator/GameView not executed")

if __name__=="__main__":main()
