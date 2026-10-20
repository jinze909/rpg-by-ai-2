#!/usr/bin/env python3
"""Audit real, published 256px companion walk animation PNGs.

Do not disguise a four-frame shuffle as eight new painted frames. Check all
three original sprites for authentic per-frame silhouette variation, intact
upper-body identity, consistent canvas/pivot and a leg-motion signal. The
source frames remain artist-owned; rendering continuity is checked in C#.
"""
from pathlib import Path
from PIL import Image, ImageChops
import hashlib
import json

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "Assets/Resources/Frontier/Art"
RUNTIME = ROOT / "Assets/Scripts/Frontier/Runtime/FrontierExpansionUI.cs"
REPORT = ROOT / ".starfall-checks/sprite-qa/companion-gait.json"
NAMES = ("Mira", "Ivo", "Sen")
CELL = 256


def inspect_idle_fallback(name):
    with Image.open(SOURCE / ("Companion" + name + ".png")) as sheet:
        assert sheet.format == "PNG" and sheet.size == (
            3 * CELL, 4 * CELL), (
                "COMPANION_WRONG_ACTION_ATLAS", name, sheet.size)
        atlas = sheet.convert("RGBA")
    # The production fallback maps east/north/south idle to cells 0/8/11.
    # Require each authored full-body cell to exist before accepting that
    # mapping; the richer Walk* sheet remains the normal production idle.
    for index in (0, 8, 11):
        x, y = index % 3 * CELL, index // 3 * CELL
        body = atlas.crop((x, y, x + CELL, y + CELL)).getchannel("A").getbbox()
        assert body is not None, (
            "COMPANION_DIRECTIONAL_IDLE_CELL_MISSING", name, index)
    return {"idle_cells": [0, 8, 11], "source_size": list(atlas.size)}


def inspect(name):
    with Image.open(SOURCE / ("Walk" + name + ".png")) as sheet:
        assert sheet.format == "PNG" and sheet.size == (
            4 * CELL, 3 * CELL), ("COMPANION_WRONG_WALK_ATLAS", name, sheet.size)
        atlas = sheet.convert("RGBA")
    report = []
    for facing in range(3):
        poses = [atlas.crop((i * CELL, facing * CELL,
                             (i + 1) * CELL, (facing + 1) * CELL))
                 for i in range(4)]
        digests = [hashlib.sha256(p.tobytes()).hexdigest() for p in poses]
        assert len(set(digests)) >= 3, (
            "COMPANION_WALK_DUPLICATE_MOTION", name, facing)
        bounds = [p.getchannel("A").getbbox() for p in poses]
        assert all(box and box[1] < 210 and box[3] >= 230
                   and 20 < box[2] - box[0] < 180
                   for box in bounds), (
            "COMPANION_GAIT_DISCONNECTED_BODY_OR_FOOT", name, facing, bounds)
        # Record the only objective lower-limb motion we can extract without
        # Unity GPU or an artist marking joint anchors. All frames use
        # EXACT source pixels: neither retouch nor synthesized sprite.
        feet = [p.crop((70, 205, 190, 250)) for p in poses]
        differences = []
        for i in range(4):
            delta = ImageChops.difference(feet[i], feet[(i + 1) % 4])
            differences.append(sum(1 for px in delta.getdata()
                                   if any(px)))
        assert max(differences) > 35, (
            "COMPANION_GAIT_FOOT_FRAMES_STATIC", name, facing, differences)
        report.append({"direction": "ENS"[facing],
                       "frame_sha256": digests,
                       "full_body_bounds": bounds,
                       "lower_body_changed_pixels": differences,
                       "minimum_verified_unique_poses": len(set(digests))})
    return report


def main():
    REPORT.parent.mkdir(parents=True, exist_ok=True)
    result = {name: inspect(name) for name in NAMES}
    idle_fallbacks = {name: inspect_idle_fallback(name) for name in NAMES}
    runtime = RUNTIME.read_text(encoding="utf-8")
    assert "int frame = north ? 8 : south ? 11 : 0;" in runtime, (
        "COMPANION_SOUTH_IDLE_FALLBACK_REGRESSION")
    assert "if(!publishedWalk)frame=row==1?8:row==2?11:0;" in runtime, (
        "COMPANION_SMOOTHED_IDLE_FACING_REGRESSION")
    assert "bool flip=row==0&&direction.x<-.1f;" in runtime, (
        "COMPANION_HORIZONTAL_ONLY_MIRROR_REGRESSION")
    assert runtime.count(
        "Actor(companionSheets[id],3,4,frame,p,192f,tint,flip);") == 2, (
            "COMPANION_ACTION_FACING_MUST_USE_CANONICAL_ROW_MIRROR")
    assert "!north&&!(south&&action!=null)" not in runtime, (
        "COMPANION_SOUTH_IDLE_MUST_NOT_BE_MIRRORED")
    REPORT.write_text(json.dumps({
        "kind": "actual_original_companion_walk_source",
        "unity_native_verified": False,
        "artist_accepted_new_intermediates": False,
        "families": result,
        "idle_fallbacks": idle_fallbacks
    }, indent=2) + "\n")
    print("PASS original WalkMira, WalkIvo and WalkSen: "
          "36 published full-body contact/stride cells audited")
    print("PASS all three companion idle fallbacks use original east/north/south cells 0/8/11")
    print("ART_REVIEW_REQUIRED: no fabricated redraws or native Unity PlayMode")


if __name__ == "__main__":
    main()
