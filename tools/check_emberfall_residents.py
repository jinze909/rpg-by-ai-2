#!/usr/bin/env python3
"""CI FAIL gate for published guild steward / blacksmith full-body sprite.

Offline raster QA never substitutes for Unity 2022.3 native GameView review.
"""
import argparse,hashlib
from PIL import Image
from generate_emberfall_residents import (
    ROOT,INSTALLED,STAGED,ACTIONS,SIZE,render
)
from check_editable_visual_candidate import audit_retired_candidate, is_live

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

def emit_animation_review(atlas):
    # Visual storyboard and loop preview for human hand/face/foot assessment.
    # This report does NOT assert that the animation looks natural in GameView.
    from PIL import ImageDraw
    folder=STAGED.parent
    folder.mkdir(parents=True,exist_ok=True)
    bg=(31,42,53,255)
    film=Image.new("RGBA",(4*144,4*164),bg)
    pen=ImageDraw.Draw(film)
    for row,action in enumerate(ACTIONS):
        for col in range(4):
            frame=atlas.crop((col*SIZE,row*SIZE,(col+1)*SIZE,(row+1)*SIZE))
            panel=Image.new("RGBA",(128,128),(42,54,66,255))
            panel.alpha_composite(frame)
            film.alpha_composite(panel,(col*144+8,row*164+23))
            pen.text((col*144+8,row*164+5),f"{action} #{col}",fill=(242,234,215,255))
    film.convert("RGB").save(folder/"resident-filmstrip.png",optimize=True)
    for kind,first in (("guild",0),("forge",2)):
        frames=[]
        for row in (first,first+1):
            for col in range(4):
                frame=atlas.crop((col*SIZE,row*SIZE,(col+1)*SIZE,(row+1)*SIZE))
                board=Image.new("RGBA",(128,128),(42,54,66,255))
                board.alpha_composite(frame)
                frames.append(board.convert("RGB").resize((256,256),Image.Resampling.NEAREST))
        frames[0].save(folder/(kind+"-idle-talk.gif"),save_all=True,
            append_images=frames[1:],duration=[310]*4+[160]*4,
            loop=0,disposal=2,optimize=False)

def source_contract():
    actors=(ROOT/"Assets/Scripts/Frontier/Runtime/FrontierActors.cs").read_text()
    for item in (
        'NpcId(kind)',
        'Game.conversationSource',
    ):
        assert item in actors,("RESIDENT_NOT_LIVE",item)
    # Future redesigned NPC animation may legitimately reuse this resource
    # path. We keep identity/dialogue correctness checks, not a permanent
    # "never use EmberfallResidents" hard ban.
    test=(ROOT/"tests/ActorRegistrationChecks.cs").read_text()
    assert "VerifyResidentAnimation" in test, "RESIDENT_DRAW_TEST_MISSING"
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
    if not is_live(ROOT,"EmberfallResidents"):
        source_contract()
        assert 'npcSheet = Resources.Load<Texture2D>(root + "Npcs")' in (
            ROOT/"Assets/Scripts/Frontier/Runtime/FrontierActors.cs").read_text(), (
                "ORIGINAL_DETAILED_NPC_SOURCE_MISSING")
        audit_retired_candidate(ROOT,"EmberfallResidents",
            "0195faaf10444a4a9456b9fd983e8d22",2048)
        return
    render(STAGED)
    with Image.open(STAGED) as original:
        expected=original.convert("RGBA").copy()
    inspect(expected)
    emit_animation_review(expected)
    source_contract()
    if not INSTALLED.exists():
        assert args.allow_unpublished,"RESIDENT_SHIPPED_PNG_MISSING"
        print("RESIDENT_PENDING_GUARDED_BINARY_PUBLICATION")
        return
    assert INSTALLED.stat().st_size>2048,"RESIDENT_LFS_POINTER_OR_TRUNCATION"
    with Image.open(INSTALLED) as shipped:
        installed=shipped.convert("RGBA").copy()
    inspect(installed)
    # The old generated pixels are NOT the artistic acceptance contract.
    # A future repaint must be free to improve this retained candidate.
    # Negative mutation MUST be rejected, not logged as a warning.
    poisoned=installed.copy()
    poisoned.paste((0,0,0,0),(0,0,128,128))
    try:
        inspect(poisoned)
    except AssertionError as e:
        assert "RESIDENT_WHOLE_BODY_CROPPED" in str(e),e
    else:
        raise AssertionError("RESIDENT_CI_FALSE_PASS_MUTATED_FRAME")
    print("PASS experimental resident art retained for provenance, not active in-game")
    print("PASS detailed original Npcs.png remains available; future candidate repaint is allowed")
    print("NATIVE_UNVERIFIED / ART_REVIEW_REQUIRED: no native Unity Animator/PlayMode")

if __name__=="__main__":
    main()
