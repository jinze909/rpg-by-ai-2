#!/usr/bin/env python3
"""Offline release gate for 24 original Cinderstone Guard action cells."""
from pathlib import Path
import hashlib
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
ART = ROOT / "Assets/Resources/Frontier/Art/CinderstoneGuard.png"
SOURCE = ROOT / "Assets/Scripts/Frontier/Runtime/FrontierActors.cs"
GUID = "2f8b8a420c8f40c6b3dd8e5e18a79423"
ACTIONS = ("Idle", "Move", "Windup", "Attack", "Hit", "Death")

def audit(image):
    assert image.size == (512, 768), ("CINDERSTONE_ATLAS_SIZE", image.size)
    image = image.convert("RGBA")
    frames = []
    for row, action in enumerate(ACTIONS):
        for col in range(4):
            frame = image.crop((col*128, row*128, (col+1)*128, (row+1)*128))
            bounds = frame.getchannel("A").getbbox()
            assert bounds, ("CINDERSTONE_BLANK_FRAME", action, col)
            assert bounds[0] >= 1 and bounds[1] >= 12, (
                "CINDERSTONE_CLIPPED_TOP_LEFT", action, col, bounds)
            assert bounds[2] - bounds[0] >= 35, (
                "CINDERSTONE_MISSING_BODY_WIDTH", action, col, bounds)
            assert bounds[3] >= 118, ("CINDERSTONE_FEET_FLOAT", action, col, bounds)
            if row != 5:
                assert bounds[3] - bounds[1] >= 75, (
                    "CINDERSTONE_MISSING_FULL_BODY", action, col, bounds)
            frames.append(frame)
    # Mirrored middle idle frames may intentionally hold a breathing pose.
    # Movement, windup, attack, hit and death must change on every cell.
    for row, action in enumerate(ACTIONS[1:], 1):
        hashes = {hashlib.sha256(f.tobytes()).digest()
                  for f in frames[row*4:(row+1)*4]}
        assert len(hashes) == 4, ("CINDERSTONE_FROZEN_ACTION", action)
    assert len({hashlib.sha256(f.tobytes()).digest() for f in frames}) >= 22, (
        "CINDERSTONE_CROSS_ACTION_REUSE")
    return len(frames)

def main():
    assert ART.is_file() and ART.stat().st_size > 10000, (
        "CINDERSTONE_MISSING_OR_LFS_POINTER")
    with Image.open(ART) as image:
        assert image.format == "PNG", "CINDERSTONE_NOT_PNG"
        count = audit(image)
        broken = image.convert("RGBA").copy()
    broken.paste((0,0,0,0), (0,0,128,128))
    try:
        audit(broken)
    except AssertionError as error:
        assert "CINDERSTONE_BLANK_FRAME" in str(error), error
    else:
        raise AssertionError("CINDERSTONE_MUTATION_NOT_REJECTED")
    runtime = SOURCE.read_text()
    for fragment in (
        'monsterSheet = Resources.Load<Texture2D>(root + "Monsters")',
        "e.kind == 2 && cinderstoneGuardSheet != null",
        "GameCore.GaitFrame(e.moveTime, 1.8f)",
        "frame = row * 4 + phase;",
        "Actor(cinderstoneGuardSheet, 4, 6, 20 + phase, p, 106",
        "DrawSpriteRendererActor(sheet, columns, rows,",
    ):
        assert fragment in runtime, ("CINDERSTONE_NOT_LIVE", fragment)
    meta = ART.with_suffix(".png.meta").read_text()
    for fragment in (f"guid: {GUID}", "enableMipMap: 0",
                     "filterMode: 0", "textureCompression: 0", "isReadable: 1"):
        assert fragment in meta, ("CINDERSTONE_IMPORTER", fragment)
    print(f"PASS {count} editable Cinderstone candidate cells; original Monsters atlas is the current game default")
    print("PASS injected missing frame rejected (hard FAIL)")
    print("NATIVE_UNVERIFIED / ART_REVIEW_REQUIRED: Unity Animator and GameView not run")

if __name__ == "__main__":
    main()
