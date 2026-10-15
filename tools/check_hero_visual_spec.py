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
    # The 56px identity is canonically baked by the OFFLINE asset exporter;
    # the live Unity fallback must never excise and reattach a warrior face.
    assert "Color[] colors = source.GetPixels(" in code
    assert "colors[(Cell - 1 - row) * Cell + col]" not in code
    fullbody = (ROOT / "Assets/Scripts/Frontier/Runtime/FrontierActors.cs").read_text()
    assert "DrawWholeBodyHeroOnGUI" in fullbody
    assert "HumanActor(" not in fullbody[
        fullbody.index("private void DrawPlayerActor()"):
        fullbody.index("private int EnemyPose(")]
    assert "UnifiedActionGait(" not in fullbody
    sheets, heads = imported_sheets(), registered_heads()
    approved = sheets["WalkVanguard"]
    baselines = {}
    for facing, frame in spec["head"]["canonical_frames"].items():
        face = crop_frame(approved, frame).crop((100, 140, 156, 196))
        assert face.getchannel("A").getbbox(), facing
        baselines[facing] = face.tobytes()
    review = []
    for klass in CLASSES:
        for facing in FACINGS:
            f = FACINGS.index(facing)
            refs = [("Walk" + klass, 8)] if facing == "S" else [
                ("Player" + klass, 1 if facing == "N" else 0)]
            name, index = refs[0]
            img = canonical_frame(sheets, heads, name, index, facing, True)
            bounds = img.getchannel("A").getbbox()
            assert bounds and 55 <= bounds[0] < 175 and 105 < bounds[1] < 211
            assert 88 < bounds[2] < 221 and 226 <= bounds[3] <= 252, (klass,facing,bounds)
            canonical = img.crop((100, 140, 156, 196))
            assert canonical.getchannel("A").getbbox(), (klass, facing)
            # Accepted pixel art has semi-transparent eyelashes and hair
            # edges: only ONE source pixel may be fully opaque. Verify the
            # real alpha-composite invariant instead of inventing 100 opaque
            # pixels. Every nonzero canonical source pixel contributes at
            # least its original opacity; truly opaque pixels are exact.
            baseline = baselines[facing]
            pix = canonical.tobytes()
            pixels_checked = opaque_checked = 0
            for pixel in range(56 * 56):
                offset = pixel * 4
                alpha = baseline[offset + 3]
                if alpha == 0:
                    continue
                assert pix[offset + 3] >= alpha, (
                    "Canonical blonde head alpha lost in class compositor",
                    klass, facing, pixel)
                pixels_checked += 1
                if alpha == 255:
                    assert pix[offset:offset + 4] == baseline[offset:offset + 4], (
                        "Fully opaque golden hair/face pixel changed",
                        klass, facing, pixel)
                    opaque_checked += 1
            # North-facing source hair has 1,786 visible pixels and zero
            # alpha=255 pixels: do not invent an opacity floor. Every
            # available opaque pixel is still strictly compared above.
            assert pixels_checked >= 100, (
                "Canonical head source lacks visible portrait coverage",
                klass, facing, pixels_checked, opaque_checked)
            socket = spec["weapon_grip_hints_top_origin_px"][klass][facing]
            assert all(isinstance(x, int) and 60 <= x < 220 for x in socket)
            # Fail if a declared socket is nowhere near this approved outfit.
            nearby = img.getchannel("A").crop((max(0, socket[0] - 38),
                max(0, socket[1] - 38), min(256, socket[0] + 39),
                min(256, socket[1] + 39)))
            assert nearby.getbbox(), "Weapon-hint detached from actual body: " + klass + facing
            review.append({"class":klass,"facing":facing,"opaque_bounds":list(bounds),
                           "reference_socket_px":socket,"foot_px":[128,240],
                           "canonical_head_pixels_preserved":pixels_checked,
                           "fully_opaque_head_pixels_locked":opaque_checked})
    assert len(review) == 9
    DEST.mkdir(parents=True, exist_ok=True)
    report = {"status":"source_pixel_geometry_verified_not_artist_acceptance",
              "canonical_sources":len(baselines),"profile_previews":review,
              "head_source":"WalkVanguard: E0/N4/S8", "native_unity_verified":False,
              "weapon_attachment_semantic_review_pending":True}
    (DEST / "hero-visual-spec-qa.json").write_text(
        json.dumps(report,indent=2)+"\n",encoding="utf-8")
    print("PASS: nine three-class/direction sprites match canonical pixel dimensions and fixed foot contract.")
    print("NATIVE_UNVERIFIED: scene renderer, anatomical grip, subjective motion, GPU/PlayMode.")


if __name__ == "__main__":
    check()
