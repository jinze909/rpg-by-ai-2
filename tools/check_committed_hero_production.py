#!/usr/bin/env python3
"""Fail on tampering in ACTUAL git-committed hero production Sprite PNGs.

Must run after actions/checkout and BEFORE install_canonical_hero_atlases.py.
The normal CI regenerates and overwrites these 63 files; without an independent
pre-install audit, a malicious/corrupt committed PNG could get a green build.
This is source/raster identity QA; Unity GameView remains NATIVE_UNVERIFIED.
"""
import hashlib
import json
import re
from pathlib import Path

from PIL import Image

from build_hero_animation_preview import (
    CELL, CLASSES, FACINGS, ACTIONS, ROOT, imported_sheets,
    source_indices, crop_frame,
)
from export_canonical_hero_atlases import CONTACTS, frame_plan, contact_key

DEST = ROOT / "Assets/Resources/Frontier/HeroAnimator/Canonical"
REPORT = ROOT / ".starfall-checks/hero-committed-qa"


def exact_frame(candidate, source, key, index):
    assert candidate.mode == "RGBA" and candidate.size == (CELL, CELL), (
        "COMMITTED_HERO_INVALID_CELL", key, index)
    assert source.mode == "RGBA" and source.size == (CELL, CELL)
    assert candidate.tobytes() == source.tobytes(), (
        "COMMITTED_HERO_RASTER_MUTATION", key, index)


def negative_controls(source):
    original = crop_frame(source["PlayerVanguard"], 0)
    exact_frame(original, original, "negative_unchanged", 0)
    broken = original.copy()
    x, y = 120, 215
    r, g, b, a = broken.getpixel((x, y))
    broken.putpixel((x, y), ((r+1)%256, g, b, a))
    try:
        exact_frame(broken, original, "negative_one_pixel_corruption", 0)
    except AssertionError as error:
        assert "COMMITTED_HERO_RASTER_MUTATION" in str(error)
    else:
        raise AssertionError("UNSAFE_CI_GATE: modified committed source frame accepted")
    amputated = original.copy()
    pixels = amputated.load()
    for y in range(210, 229):
        for x in range(90, 170):
            pixels[x, y] = (0, 0, 0, 0)
    try:
        exact_frame(amputated, original, "negative_pelvis_cut", 0)
    except AssertionError as error:
        assert "COMMITTED_HERO_RASTER_MUTATION" in str(error)
    else:
        raise AssertionError("UNSAFE_CI_GATE: committed pelvis splice accepted")


def check():
    source = imported_sheets()
    negative_controls(source)
    expected = {
        contact_key(profession, action, facing)
        for profession in CLASSES for action in ACTIONS for facing in FACINGS
    }
    actual = {p.stem for p in DEST.glob("Hero_*.png")}
    assert actual == expected, (
        "COMMITTED_HERO_RESOURCE_MATRIX_MISSING_OR_EXTRA", sorted(actual^expected))
    counts, sheets = {}, {}
    for key in sorted(expected):
        with Image.open(DEST / (key + ".png")) as file:
            assert file.format == "PNG" and file.mode == "RGBA", (
                "COMMITTED_HERO_PNG_FORMAT", key)
            sheet = file.copy()
        assert sheet.height == CELL and sheet.width % CELL == 0, (
            "COMMITTED_HERO_ATLAS_GEOMETRY", key, sheet.size)
        counts[key] = sheet.width // CELL
        sheets[key] = sheet
    contact_keys = [contact_key(prof, action, face)
                    for prof in CLASSES for action in ("Attack", "Cast")
                    for face in FACINGS]
    seven = sum(counts[key] == 7 for key in contact_keys)
    assert seven in (0, len(contact_keys)), (
        "COMMITTED_PARTIAL_7_CELL_CONTACT_ROLLOUT", seven)
    # 7 cells MUST be tied to actual source art; a PNG alone is not allowed
    # to declare that missing transition artwork exists.
    if seven:
        assert all((CONTACTS / (key + ".png")).is_file()
                   for key in contact_keys), (
            "COMMITTED_UNSOURCED_7_CELL_HERO_ART", seven)
    guids, inspected, identity = set(), 0, {}
    for profession in CLASSES:
        for action in ACTIONS:
            for facing in FACINGS:
                key = contact_key(profession, action, facing)
                refs = source_indices(profession, action, facing)
                frame_list = frame_plan(
                    profession, action, facing, refs,
                    seven == 18 and action in ("Attack", "Cast"))
                assert counts[key] == len(frame_list), (
                    "COMMITTED_HERO_WRONG_FRAME_COUNT", key,
                    counts[key], len(frame_list))
                sheet = sheets[key]
                hashes = []
                for i, ((name, frame), kind, _) in enumerate(frame_list):
                    candidate = sheet.crop((
                        i * CELL, 0, (i + 1) * CELL, CELL))
                    if kind == "hand_redrawn":
                        assert i == 3 and name == "@hand_redrawn"
                        with Image.open(CONTACTS / (key + ".png")) as staged:
                            assert staged.format == "PNG" and staged.mode == "RGBA"
                            original = staged.copy()
                    else:
                        assert name in source and kind in ("accepted", "curated")
                        original = crop_frame(source[name], frame)
                    exact_frame(candidate, original, key, i)
                    hashes.append(hashlib.sha256(candidate.tobytes()).hexdigest())
                    inspected += 1
                meta = (DEST / (key + ".png.meta")).read_text(encoding="utf-8")
                guid = re.search(r"(?m)^guid: ([0-9a-f]{32})$", meta)
                assert guid and guid.group(1) not in guids, (
                    "COMMITTED_INVALID_OR_DUPLICATE_UNITY_GUID", key)
                guids.add(guid.group(1))
                identity[key] = hashes
    assert len(guids) == 63 and inspected == 279 + seven, (
        "COMMITTED_HERO_3_CLASS_MATRIX_INCOMPLETE",
        len(guids), inspected)
    REPORT.mkdir(parents=True, exist_ok=True)
    report = {
        "status": "CHECKED_COMMITTED_ORIGINAL_PRODUCTION_PNG_BEFORE_OVERWRITE",
        "read_only_resources": str(DEST.relative_to(ROOT)),
        "source_anchored_frames": inspected,
        "production_atlases": len(expected),
        "seven_cell_attack_cast_count": seven,
        "negative_controls": ["one_pixel_recolor", "transparent_hip_cut"],
        "native_unity_verified": False,
        "art_direction_approved": False,
        "per_atlas_frame_sha256_rgba": identity,
    }
    (REPORT / "committed-hero-qa.json").write_text(
        json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print(f"PASS: committed (not generated) 63 Sprite PNGs / {inspected} "
          "byte-exact original-source whole-body cells and 63 unique GUIDs.")
    print("NATIVE_UNVERIFIED: non-image Unity Animator and GameView still require Unity.")


if __name__ == "__main__":
    check()
