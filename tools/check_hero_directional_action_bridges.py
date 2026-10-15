#!/usr/bin/env python3
"""Reject North/South Attack/Cast clips that flash a walking body for 1–2 frames.

Audits the REAL 12 published 6-cell directional action Sprite sheets and
generates honest 'old walk-bridge' versus 'planted guard' review strips.
All accepted cells come from existing WHOLE-BODY class-matching paintings.
This is not a new animation drawing or Unity GameView acceptance.
"""
import hashlib
import json
from pathlib import Path
from PIL import Image, ImageDraw

from build_hero_animation_preview import (
    ROOT, CELL, CLASSES, FACINGS, imported_sheets, source_indices, crop_frame,
)
from export_canonical_hero_atlases import frame_plan, DEST
from install_canonical_hero_atlases import ASSETS

REPORT = ROOT / ".starfall-checks/hero-runtime-qa/directional-bridge-qa.json"
ACTIONS = ("Attack", "Cast")
DIRECTIONS = ("N", "S")


def pixels(frame):
    assert frame.size == (CELL, CELL)
    return hashlib.sha256(frame.tobytes()).hexdigest()


def validate(klass, action, direction, source, manifest, installed, broken=None):
    key = f"Hero_{klass}_{action}_{direction}"
    frames = frame_plan(klass, action, direction,
                        source_indices(klass, action, direction), False)
    assert len(frames) == 6, ("MISSING_DIRECTIONAL_ACTION_SLOTS", key)
    reaction = "PlayerReactionsRear" if direction == "N" else "PlayerReactionsSouth"
    guard = (reaction, CLASSES.index(klass)*4+1)
    for i in (1, 4):
        assert frames[i][:2] == (guard, "curated"), (
            "WALK_FRAME_INSERTED_IN_ATTACK_CAST", key, i, frames[i])
    atlas = installed if broken is None else broken
    assert atlas.size == (6*CELL, CELL), ("BAD_DIRECTIONAL_ATLAS", key, atlas.size)
    expected = crop_frame(source[reaction], guard[1])
    hashes = []
    for i, ((name, index), kind, _) in enumerate(frames):
        img = atlas.crop((CELL*i,0,CELL*(i+1),CELL))
        truth = crop_frame(source[name],index)
        assert img.tobytes() == truth.tobytes(), (
            "VISIBLE_OLD_MOTION_OR_FOREIGN_BODY", key, i, name, index)
        hashes.append(pixels(img))
    assert all(a != b for a, b in zip(hashes, hashes[1:])), (
        "FAKE_ADJACENT_FRAME_HOLD", key)
    assert atlas.crop((CELL,0,2*CELL,CELL)).tobytes() == expected.tobytes()
    assert atlas.crop((4*CELL,0,5*CELL,CELL)).tobytes() == expected.tobytes()
    row = manifest["atlases"][key]
    assert row["frame_count"] == 6 and row["curated_full_body_slots"] == {
        "1":f"{guard[0]}:{guard[1]}",
        "4":f"{guard[0]}:{guard[1]}",
    }, ("OLD_WALK_BRIDGE_IN_PRODUCTION_MANIFEST", key)
    assert row["frame_sha256_rgba"] == hashes, ("MISREPORTED_RASTER_HASH",key)
    # Before art: striding source poses at both positions. The new guard
    # is deliberately different, not a copied Walk pose or a fabricated
    # head/legs interpolation.
    walk = source["Walk"+klass]
    before = (4,6) if direction == "N" else (9,11)
    for stride in before:
        assert crop_frame(walk,stride).tobytes() != expected.tobytes(), (
            "UNEXPECTED_IDENTICAL_OLD_ACTION_STRIDE", key, stride)
    return {"asset":key, "curated_full_body_guard":f"{reaction}:{guard[1]}",
            "replaced_original_walk_slots":[before[0], before[1]],
            "all_six_cells_source_locked":True,
            "actual_new_original_contact_drawings":0}, frames


def diagnostic(klass, action, direction, sources, after, plan, out):
    walk=sources["Walk"+klass]
    old_frames=[]
    before=(4,6) if direction=="N" else (9,11)
    for i,((name,index),_,_) in enumerate(plan):
        if i in (1,4):
            old_frames.append(crop_frame(walk,before[0] if i==1 else before[1]))
        else:
            old_frames.append(crop_frame(sources[name],index))
    sheet=Image.new("RGB",(6*CELL,2*CELL+52),(24,29,38))
    sheet=sheet.convert("RGBA")
    draw=ImageDraw.Draw(sheet)
    draw.text((12,6),f"BEFORE  {klass} {action} {direction}: old walking silhouette flashes",fill=(255,167,119))
    draw.text((12,28),f"AFTER   same painted identity/foot Pivot: class guard, NOT new art",fill=(168,232,189))
    for row,parts in enumerate((old_frames,after)):
        for i,frame in enumerate(parts):
            sheet.alpha_composite(frame,(i*CELL,52+row*CELL))
    sheet.convert("RGB").save(out,optimize=True)


def check():
    source=imported_sheets()
    manifest=json.loads((DEST/"manifest.json").read_text(encoding="utf-8"))
    REPORT.parent.mkdir(parents=True,exist_ok=True)
    checks=[]
    total=0
    for klass in CLASSES:
        for action in ACTIONS:
            for direction in DIRECTIONS:
                key=f"Hero_{klass}_{action}_{direction}"
                path=ASSETS/(key+".png")
                assert path.is_file(), ("MISSING_REAL_PRODUCTION_ACTION",key)
                with Image.open(path) as file:
                    installed=file.convert("RGBA").copy()
                row,plan=validate(klass,action,direction,source,manifest,installed)
                checks.append(row)
                after=[installed.crop((i*CELL,0,(i+1)*CELL,CELL))
                       for i in range(6)]
                diagnostic(klass,action,direction,source,after,plan,
                           REPORT.parent/(key+"-BEFORE-AFTER.png"))
                # Adversarial negative: intentionally replace the first
                # guard with the OLD walking PNG at exactly the trigger
                # frame. The SAME validator must abort with a hard failure.
                bad=installed.copy()
                frame=4 if direction=="N" else 9
                bad.paste(crop_frame(source["Walk"+klass],frame),(CELL,0))
                try:
                    validate(klass,action,direction,source,manifest,installed,bad)
                except AssertionError as e:
                    assert "VISIBLE_OLD_MOTION_OR_FOREIGN_BODY" in str(e), e
                else:
                    raise AssertionError(("UNSAFE_CI_OLD_WALK_FLASH_ACCEPTED",key))
                total+=1
    assert total==12
    REPORT.write_text(json.dumps({
        "status":"NO_OLD_WALK_FRAMES_IN_NORTH_SOUTH_ATTACK_OR_CAST",
        "published_assets_checked":12,
        "retired_flash_cells":24,
        "source_originality":"existing approved, complete-body reaction guard",
        "new_original_art_drawn":0,
        "native_unity_verified":False,
        "art_direction_approved":False,
        "diagnosis":checks,
    },ensure_ascii=False,indent=2)+"\n",encoding="utf-8")
    print("PASS 12 REAL north/south Attack/Cast sheets; retired 24 old "
          "walking-body flashes; guarded actual 256px whole-body poses")
    print("PASS 12 injected old-source flash regressions rejected")
    print("ART_REVIEW_REQUIRED / NATIVE_UNVERIFIED: source-backed pose repair "
          "is not newly drawn motion or Unity PlayMode")


if __name__=="__main__":
    check()
