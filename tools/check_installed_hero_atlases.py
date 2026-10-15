#!/usr/bin/env python3
"""Validate the actual 63 published Unity Sprite sheets, not unrelated LFS assets.

Run AFTER export_canonical_hero_atlases.py and install_canonical_hero_atlases.py.
Checks real PNG pixels, all authored-source hashes, stable Unity metadata and
the exact generated manifest. Native Unity Editor/PlayMode is still required.
"""
import hashlib
import json
import re

from PIL import Image

from build_hero_animation_preview import CELL, CLASSES, FACINGS, ACTIONS
from export_canonical_hero_atlases import DEST, expected_count
from install_canonical_hero_atlases import ASSETS


def check():
    manifest = json.loads((DEST / "manifest.json").read_text(encoding="utf-8"))
    extra = manifest.get("hand_redrawn_contact_frames", 0)
    assert extra in (0, 18)
    assert manifest["status"] == (
        "all_297_cells_source_locked_plus_18_hand_redrawn_contacts"
        if extra else
        "all_279_cells_from_unmodified_approved_full_body_source_art")
    assert manifest["top_origin_foot_px"] == [128, 240]
    assert manifest["unity_bottom_origin_pivot"] == [0.5, 0.0625]
    expected = {
        f"Hero_{klass}_{action}_{facing}"
        for klass in CLASSES for action in ACTIONS for facing in FACINGS
    }
    assert set(manifest["atlases"]) == expected and len(expected) == 63
    published = {path.stem for path in ASSETS.glob("Hero_*.png")}
    assert published == expected, f"Missing or untracked installed hero sheets: {expected ^ published}"
    seen_guids = set()
    checked_frames = 0
    for key in sorted(expected):
        row = manifest["atlases"][key]
        path = ASSETS / (key + ".png")
        candidate = DEST / row["path"]
        assert row["path"] == key + ".png"
        assert path.is_file() and candidate.is_file(), key
        payload = path.read_bytes()
        assert payload.startswith(bytes.fromhex("89504e470d0a1a0a")), key
        assert hashlib.sha256(payload).digest() == hashlib.sha256(candidate.read_bytes()).digest(), key
        meta_path = path.with_name(path.name + ".meta")
        assert meta_path.is_file(), key
        meta = meta_path.read_text(encoding="utf-8")
        match = re.search(r"^guid: ([a-f0-9]{32})$", meta, re.M)
        assert match and match.group(1) not in seen_guids, (key, "missing/duplicate GUID")
        seen_guids.add(match.group(1))
        for mark in ("TextureImporter:", "enableMipMap: 0", "filterMode: 0", "textureCompression: 0"):
            assert mark in meta, (key, mark)
        with Image.open(path) as source:
            source.verify()
        with Image.open(path) as source:
            image = source.convert("RGBA")
        action = key.split("_")[2]
        count = expected_count(action, row["original_count"],
                               bool(extra and action in ("Attack","Cast")))
        assert count == row["frame_count"], key
        assert image.size == (count * CELL, CELL), key
        assert len(row["frame_sha256_rgba"]) == count, key
        for i, sha in enumerate(row["frame_sha256_rgba"]):
            frame = image.crop((i * CELL, 0, (i + 1) * CELL, CELL))
            assert frame.getchannel("A").getbbox() is not None, (key, i, "blank")
            assert hashlib.sha256(frame.tobytes()).hexdigest() == sha, (key, i, "pixel mismatch")
            checked_frames += 1
    print(f"PASS: {len(expected)} installed hero Sprite atlases / {checked_frames} pixel-verified frames; "
          "63 unique stable Unity GUIDs and fixed 256px cell geometry.")
    print("NATIVE_UNVERIFIED: Unity SpriteImporter slicing, PlayMode, weapon grip aesthetics, runtime FPS.")


if __name__ == "__main__":
    check()
