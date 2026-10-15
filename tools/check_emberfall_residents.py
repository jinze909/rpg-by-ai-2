#!/usr/bin/env python3
"""CI FAIL gate for published guild steward / blacksmith full-body sprite.

Offline raster QA never substitutes for Unity 2022.3 native GameView review.
"""
import argparse,hashlib
from PIL import Image
from generate_emberfall_residents import (
    ROOT,INSTALLED,STAGED,ACTIONS,SIZE,render
)

def inspect(atlas):
    assert atlas.mode=="RGBA" and atlas.size==(512,512),(
        "RESIDENT_GRID_OR_MODE",atlas.mode,atlas.size)
    all_frames=[]
    for row,action in enumerate(ACTIONS):
        frames=[]
        bounds=[]
        for col in range(4):
            im=atlas.crop((col*SIZE,row*SIZE,(col+1)*SIZE,(row+1)*SIZE))
            bbox=im.getchannel("A").getbbox()
            assert bbox is not None and min(bbox[0],bbox[1],128-bbox[2],128-bbox[3])>=2,(
                "RESIDENT_WHOLE_BODY_CROPPED",action,col,bbox)
            assert bbox[2]-bbox[0]>=53 and bbox[3]-bbox[1]>=67,(
                "RESIDENT_ANATOMY_COLLAPSED",action,col,bbox)
            assert im.getpixel((64,118))[3]>0,(
                "RESIDENT_LOST_FOOT_ANCHOR",action,col)
            bounds.append(bbox)
            frames.append(im)
            all_frames.append(im)
        assert len(set(hashlib.sha256(f.tobytes()).digest() for f in frames))==4,(
            "RESIDENT_STALE_FRAMES",action)
        for a,b in zip(bounds,bounds[1:]):
            center=lambda p:((p[0]+p[2])/2,(p[1]+p[3])/2)
            ax,ay=center(a);bx,by=center(b)
            assert ((ax-bx)**2+(ay-by)**2)**.5<=26,(
                "RESIDENT_UNNATURAL_ROOT_JUMP",action,a,b)
    assert len(set(hashlib.sha256(f.tobytes()).digest() for f in all_frames))==16,(
        "RESIDENT_CROSS_IDENTITY_OR_ACTION_FRAME_REUSE")
    # Two actors must have fundamentally different structure/palette.
    a,b=all_frames[0],all_frames[8]
    assert a.getchannel("A").tobytes()!=b.getchannel("A").tobytes(),(
        "RESIDENT_IDENTITY_LOST")
    return all_frames

def source_contract():
    actors=(ROOT/"Assets/Scripts/Frontier/Runtime/FrontierActors.cs").read_text()
    for item in (
        "emberfallResidentsSheet",
        'Resources.Load<Texture2D>(root + "EmberfallResidents")',
        'NpcId(kind)',
        'Game.conversationSource',
        'Math.Abs(interlocutor.x - position.x) < .05f',
        'Math.Abs(interlocutor.y - position.y) < .05f',
        'npcKey == 0 || npcKey == 2',
        'Actor(emberfallResidentsSheet, 4, 4, frame, p, 96,',
    ):
        assert item in actors,("RESIDENT_NOT_LIVE",item)
    test=(ROOT/"tests/ActorRegistrationChecks.cs").read_text()
    assert "VerifyResidentAnimation" in test, "RESIDENT_MISSING_REAL_DRAW_TEST"
    meta=INSTALLED.with_suffix(".png.meta").read_text()
    for marker in ("guid: 0195faaf10444a4a9456b9fd983e8d22",
        "enableMipMap: 0","filterMode: 0","isReadable: 1",
        "textureCompression: 0"):
        assert marker in meta,("RESIDENT_IMPORTER_REGRESSION",marker)
    attrs=(ROOT/".gitattributes").read_text()
    assert "/Assets/Resources/Frontier/Art/EmberfallResidents.png -filter -diff -merge" in attrs,(
        "RESIDENT_REQUIRES_REAL_GIT_BLOB")

def main():
    p=argparse.ArgumentParser()
    p.add_argument("--allow-unpublished",action="store_true")
    args=p.parse_args()
    render(STAGED)
    with Image.open(STAGED) as original:
        expected=original.convert("RGBA").copy()
    inspect(expected)
    source_contract()
    if not INSTALLED.exists():
        assert args.allow_unpublished,"RESIDENT_SHIPPED_PNG_MISSING"
        print("RESIDENT_PENDING_GUARDED_BINARY_PUBLICATION")
        return
    assert INSTALLED.stat().st_size>2048,"RESIDENT_LFS_POINTER_OR_TRUNCATION"
    with Image.open(INSTALLED) as shipped:
        installed=shipped.convert("RGBA").copy()
    inspect(installed)
    assert expected.tobytes()==installed.tobytes(),(
        "RESIDENT_PUBLISHED_PIXELS_DIFFER_FROM_SOURCE")
    # Negative mutation MUST be rejected, not logged as a warning.
    poisoned=installed.copy()
    poisoned.paste((0,0,0,0),(0,0,128,128))
    try:
        inspect(poisoned)
    except AssertionError as e:
        assert "RESIDENT_WHOLE_BODY_CROPPED" in str(e),e
    else:
        raise AssertionError("RESIDENT_CI_FALSE_PASS_MUTATED_FRAME")
    print("PASS 16 shipped guild/forge full-body poses and 4-frame idle/talk loops")
    print("PASS fixed foot, importer GUID, raster parity, unique identity, mutation rejection")
    print("NATIVE_UNVERIFIED / ART_REVIEW_REQUIRED: no native Unity Animator/PlayMode")

if __name__=="__main__":
    main()
