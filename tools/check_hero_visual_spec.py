#!/usr/bin/env python3
"""Verify actual accepted hero raster geometry against the LIVE compositor spec.

This catches accidental identity imports, per-class scaling and foot-root
changes. Approximate weapon socket hints are visual inspection references,
NOT projectile hitbox offsets or proof of closed hand grips.
"""
import json
import re
from pathlib import Path

from PIL import Image
from build_hero_animation_preview import (
    ART, CELL, CLASSES, FACINGS, ROOT, canonical_frame, crop_frame,
    imported_sheets, registered_heads,
)
from export_canonical_hero_atlases import DEST

SPEC = ROOT / "docs/HERO-VISUAL-SPEC.json"
CODE = ROOT / "Assets/Scripts/Frontier/Runtime/CanonicalHeroSpriteFactory.cs"


def check():
    spec = json.loads(SPEC.read_text(encoding="utf-8"))
    assert spec["unity_version"] == "2022.3.53f1"
    assert spec["canvas_px"] == [CELL, CELL]
    assert spec["pixels_per_unit"] == 128 and spec["gui_density"] == .75
    assert spec["foot_top_origin_px"] == [128, 240]
    assert spec["foot_bottom_origin_px"] == [128, 16]
    assert spec["pivot_normalized"] == [.5, .0625]
    assert tuple(spec["professions"]) == CLASSES
    assert tuple(spec["facings"]) == FACINGS
    assert spec["head"]["source_rect_top_px"] == [100, 140, 56, 56]
    code = CODE.read_text(encoding="utf-8")
    for item, value in (("Cell", 256), ("FootX", 128), ("FootTop", 240),
                        ("IdentityHeadX", 100), ("IdentityHeadTop", 140),
                        ("IdentityHeadSize", 56)):
        assert re.search(r"public const int " + item + r" = " + str(value) + r";", code), item
    assert "public const float PixelsPerUnit = 128f;" in code
    assert "public const float GuiDensity = .75f;" in code
    # Both the offline producer and Unity runtime preserve the full original
    # character's blonde hair, connected neck and class outfit. No face swap.
    assert "Color[] colors = source.GetPixels(" in code
    assert "colors[(Cell - 1 - row) * Cell + col]" not in code
    fullbody = (ROOT / "Assets/Scripts/Frontier/Runtime/FrontierActors.cs").read_text()
    assert "DrawWholeBodyHeroOnGUI" in fullbody
    assert "HumanActor(" not in fullbody[
        fullbody.index("private void DrawPlayerActor()"):
        fullbody.index("private int EnemyPose(")]
    assert "UnifiedActionGait(" not in fullbody
    sheets, heads = imported_sheets(), registered_heads()
    review = []
    source_pixels_verified = blonde_pixels_verified = 0
    for klass in CLASSES:
        for facing in FACINGS:
            refs = [("Walk" + klass, 8)] if facing == "S" else [
                ("Player" + klass, 1 if facing == "N" else 0)]
            name, index = refs[0]
            approved = crop_frame(sheets[name], index)
            img = canonical_frame(sheets, heads, name, index, facing, True)
            # No crop, alpha mask, or separate Vanguard head is allowed.
            # The RAW class-owned source already includes its blonde hair,
            # attached neck, weapon and whole torso/hip/body anatomy.
            assert img.tobytes() == approved.tobytes(), (
                "FOREIGN_HEAD_OR_SPLIT_BODY", klass, facing)
            source_pixels_verified += CELL * CELL
            bounds = img.getchannel("A").getbbox()
            assert bounds and 55 <= bounds[0] < 175 and 105 < bounds[1] < 211
            assert 88 < bounds[2] < 221 and 226 <= bounds[3] <= 252, (
                klass, facing, bounds)
            x,y,w,h = heads[name][index]
            assert 15 <= w <= 90 and 15 <= h <= 90, (
                "HEAD_BODY_PROPORTION",klass,facing,(x,y,w,h))
            head = img.crop((x,y,x+w,y+h))
            # Real blonde hair in each class-owned head, not an pasted
            # identical Warrior head. Inspect color only inside its
            # registered head region so warm armor cannot fake identity.
            blonde = 0
            for r,g,b,a in head.getdata():
                if (a >= 70 and r >= 150 and g >= 95 and
                    r > g * 1.07 and g > b * 1.04):
                    blonde += 1
            assert blonde >= 35, ("BLONDE_IDENTITY_MISSING",
                                  klass,facing,blonde)
            blonde_pixels_verified += blonde
            socket = spec["weapon_grip_hints_top_origin_px"][klass][facing]
            assert all(isinstance(x,int) and 60 <= x < 220 for x in socket)
            nearby = img.getchannel("A").crop((
                max(0,socket[0]-38),max(0,socket[1]-38),
                min(256,socket[0]+39),min(256,socket[1]+39)))
            assert nearby.getbbox(), (
                "Weapon hint detached from full body:",klass,facing)
            review.append({"class":klass,"facing":facing,
                           "opaque_bounds":list(bounds),
                           "head_source":f"{name}:{index}",
                           "head_bounds":[x,y,w,h],
                           "blonde_hair_pixels":blonde,
                           "full_body_rgba_unchanged":True,
                           "reference_socket_px":socket,"foot_px":[128,240]})
    # Adversarial fixture: a covered head must fail the independent blonde
    # detector without relying on a downstream soft diagnostic.
    fake = Image.new("RGBA",(56,56),(34,44,60,255))
    assert not any(a>=70 and r>=150 and g>=95 and
                   r>g*1.07 and g>b*1.04
                   for r,g,b,a in fake.getdata()), (
        "BLONDE_IDENTITY_NEGATIVE_TEST_FAILED")
    assert len(review) == 9
    DEST.mkdir(parents=True, exist_ok=True)
    report = {"status":"source_pixel_geometry_verified_not_artist_acceptance",
              "class_body_sources":9,"profile_previews":review,
              "original_full_body_pixels_verified":source_pixels_verified,
              "blonde_hair_pixels_verified":blonde_pixels_verified,
              "head_source":"original registered class-owned full-body sprite; no face cutout",
              "native_unity_verified":False,
              "weapon_attachment_semantic_review_pending":True}
    (DEST / "hero-visual-spec-qa.json").write_text(
        json.dumps(report,indent=2)+"\n",encoding="utf-8")
    print("PASS: nine three-class/direction sprites match canonical pixel dimensions and fixed foot contract.")
    print("NATIVE_UNVERIFIED: scene renderer, anatomical grip, subjective motion, GPU/PlayMode.")


if __name__ == "__main__":
    check()
