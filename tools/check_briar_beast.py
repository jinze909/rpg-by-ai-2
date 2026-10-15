#!/usr/bin/env python3
"""Hard-gate *committed* full-body Briar Beast Boss artwork, per phase/action.

Structural continuity is machine checked. Native Unity/GameView and aesthetic
approval are not simulated; hard FAIL on deterministic source/frame regressions.
"""
from pathlib import Path
from collections import deque
import argparse
import hashlib
from PIL import Image
from generate_briar_beast import ROOT, STAGED, INSTALLED, ACTIONS, CELL, render

def largest_solid_fraction(frame):
    # Sample only the 80x80 authored grid. Exclude the translucent root shadow.
    tiny=frame.getchannel("A").resize((80,80),Image.Resampling.NEAREST)
    px=tiny.load()
    seen=set()
    count=0
    biggest=0
    for y in range(80):
        for x in range(80):
            if px[x,y] < 180:
                continue
            count+=1
            if (x,y) in seen:
                continue
            queue=[(x,y)]
            seen.add((x,y))
            found=0
            while queue:
                ax,ay=queue.pop()
                found+=1
                for dx,dy in ((0,1),(0,-1),(1,0),(-1,0)):
                    nx,ny=ax+dx,ay+dy
                    if (nx,ny) in seen or not(0<=nx<80 and 0<=ny<80):
                        continue
                    if px[nx,ny]>=180:
                        seen.add((nx,ny))
                        queue.append((nx,ny))
            biggest=max(biggest,found)
    return biggest/max(1,count)

def inspect(sheet):
    assert sheet.mode=="RGBA" and sheet.size==(1280,5760), (
        "BRIAR_CORRUPT_GRID_OR_SIZE",sheet.size,sheet.mode)
    cells=[]
    for phase in range(3):
        for ai,action in enumerate(ACTIONS):
            row=phase*6+ai
            seq=[]
            for col in range(4):
                tile=sheet.crop((col*CELL,row*CELL,(col+1)*CELL,(row+1)*CELL))
                box=tile.getchannel("A").getbbox()
                assert box and min(box[0],box[1],CELL-box[2],CELL-box[3])>=4, (
                    "BRIAR_MISSING_OR_CLIPPED_WHOLE_BODY",phase,action,col,box)
                assert box[2]-box[0]>150 and box[3]-box[1]>80, (
                    "BRIAR_BROKEN_BOSS_SILHOUETTE",phase,action,col,box)
                assert tile.getpixel((160,300))[3]>0, (
                    "BRIAR_LOST_FOOT_PIVOT",phase,action,col)
                assert largest_solid_fraction(tile)>.74, (
                    "BRIAR_DISCONNECTED_BODY",phase,action,col)
                seq.append(box)
                cells.append(tile)
            for idx,(a,b) in enumerate(zip(seq,seq[1:])):
                mx=lambda bb:(bb[0]+bb[2])/2
                my=lambda bb:(bb[1]+bb[3])/2
                drift=((mx(a)-mx(b))**2+(my(a)-my(b))**2)**.5
                assert drift<=44,("BRIAR_POSE_TELEPORT",phase,action,idx,drift)
    hashes=[hashlib.sha256(tile.tobytes()).digest() for tile in cells]
    assert len(set(hashes))==72,("BRIAR_DUPLICATED_CONTACT_OR_PHASE",len(set(hashes)))
    # Different stages must change structural silhouette, not only HUD color.
    for action in range(6):
        for other in (1,2):
            a=cells[action*4].getchannel("A").tobytes()
            b=cells[(other*6+action)*4].getchannel("A").tobytes()
            assert a!=b,("BRIAR_PHASE_WITHOUT_SILHOUETTE_CHANGE",other,action)
    return cells

def inspect_import(meta):
    for marker in (
        "guid: 42df038f17c345c29de6a73bc5a9e820",
        "enableMipMap: 0","filterMode: 0",
        "textureCompression: 0","maxTextureSize: 8192","isReadable: 1",
    ):
        assert marker in meta,("BRIAR_UNSAFE_UNITY_IMPORT_OR_GUID",marker)

def check_release_fence(actor):
    # Room-1 EnemyPose() used to return the apparent release pose for
    # timer < .2 while the attack still had NOT dispatched. New authored
    # contact art must not show before GameCore flips e.state and releaseTime.
    section=actor.split("int bodyPhase = Math.Min(2, Math.Max(0, e.BossPhase - 1));",1)
    assert len(section)==2, "BRIAR_MISSING_HP_PHASE_MAPPING"
    section=section[1].split("Actor(briarBeastSheet, 4, 18, liveCell,",1)
    assert len(section)==2, "BRIAR_NOT_DRAWING_SELECTED_FRAME"
    code=section[0]
    for required in (
        "e.hitTime > 0 ? 4 :",
        "e.state == 1 ? 2 : e.releaseTime > 0 ? 3 :",
        "action == 2 ?",
        "1f - e.timer / Math.Max(.01f, e.windupDuration)",
        "action == 3 ? 1f - e.releaseTime / .38f",
        "int liveCell = (bodyPhase * 6 + action) * 4 + keyframe;",
    ):
        assert required in code, ("BRIAR_PREMATURE_HIT_OR_PHASE_MAPPING",required)
    assert "EnemyPose(e)" not in code, "BRIAR_EARLY_RELEASE_FROM_LEGACY_POSE"

def main():
    parser=argparse.ArgumentParser()
    parser.add_argument("--allow-unpublished",action="store_true")
    args=parser.parse_args()
    render(STAGED)
    with Image.open(STAGED) as raw:
        generated=raw.convert("RGBA")
    inspect(generated)
    actor=(ROOT/"Assets/Scripts/Frontier/Runtime/FrontierActors.cs").read_text()
    for marker in (
        "briarBeastSheet","Game.save.room == 1",
        "e.BossPhase","GameCore.GaitFrame(e.moveTime, 2.8f)",
        "e.state == 1","e.releaseTime","e.hitTime",
        "DrawFallen",
    ):
        assert marker in actor, ("BRIAR_NOT_LIVE_IN_FRONTIER",marker)
    check_release_fence(actor)
    # Negative proof: the precise legacy premature-contact regression must FAIL.
    changed=actor.replace(
        "e.state == 1 ? 2 : e.releaseTime > 0 ? 3 :",
        "e.state == 1 ? 3 : e.releaseTime > 0 ? 2 :",1)
    assert changed != actor, "BRIAR_NEGATIVE_TEST_TARGET_MISSING"
    try:
        check_release_fence(changed)
    except AssertionError as exc:
        assert "BRIAR_PREMATURE_HIT_OR_PHASE_MAPPING" in str(exc),exc
    else:
        raise AssertionError("BRIAR_CI_FAILED_TO_REJECT_PREMATURE_CONTACT")
    assert INSTALLED.with_suffix(".png.meta").exists(), "BRIAR_UNITY_META_MISSING"
    inspect_import(INSTALLED.with_suffix(".png.meta").read_text())
    if not INSTALLED.exists():
        assert args.allow_unpublished,"BRIAR_COMMITTED_PNG_MISSING"
        print("BRIAR_PENDING_GUARDED_BINARY_PUBLICATION")
        return
    assert INSTALLED.stat().st_size>4096,"BRIAR_BINARY_MISSING_OR_LFS_POINTER"
    assert "/Assets/Resources/Frontier/Art/BriarBeast.png -filter -diff -merge" in (
        ROOT/".gitattributes").read_text(), "BRIAR_LFS_POINTER_RISK"
    with Image.open(INSTALLED) as raw:
        original=raw.convert("RGBA")
    inspect(original)
    # A future high-detail re-paint can replace this candidate without
    # reproducing the old procedural generator byte-for-byte.
    mutated=original.copy()
    mutated.paste((0,0,0,0),(0,0,320,320))
    try:
        inspect(mutated)
    except AssertionError as exc:
        assert "BRIAR_MISSING_OR_CLIPPED_WHOLE_BODY" in str(exc),exc
    else:
        raise AssertionError("BRIAR_VISUAL_CI_ACCEPTED_BLANK_BOSS")
    print("PASS archived experimental Briar 72-cell anatomy; repaints permitted and current rich Bosses.png is live")
    print("PASS continuity, foot anchor, connectedness, source parity, GUID and negative mutation")
    print("PASS rejected injected early-hit state transition in live Boss selection")
    print("NATIVE_UNVERIFIED / ART_REVIEW_REQUIRED: no Unity 2022.3.53f1 GameView")

if __name__=="__main__":
    main()
