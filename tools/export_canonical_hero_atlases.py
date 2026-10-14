#!/usr/bin/env python3
"""Export 63 native Unity-importable sprite sheets from ACCEPTED protagonist pixels.

Each atlas uses the same canonical blonde identity and registered 256px root as
the live game. This is genuine game-ready sprite repacking, not falsely claimed
new hand-authored animation or Unity Editor PlayMode verification.
The Unity Editor production builder bakes the equivalent outputs into Assets.
"""
import hashlib
import json
from pathlib import Path
from PIL import Image

from build_hero_animation_preview import (
    ACTIONS, CELL, CLASSES, FACINGS, FOOT, ROOT, canonical_frame,
    imported_sheets, registered_heads, source_indices, strip,
)

DEST = ROOT / ".starfall-checks/hero-production-atlases"
PREVIEW = ROOT / ".starfall-checks/hero-animation-qa"


def main():
    DEST.mkdir(parents=True, exist_ok=True)
    sheets, heads = imported_sheets(), registered_heads()
    manifest = {
        "status": "accepted_original_source_repacked_not_new_in_betweens",
        "canvas_per_frame": [CELL, CELL],
        "top_origin_foot_px": list(FOOT),
        "unity_bottom_origin_pivot": [0.5, 0.0625],
        "pixels_per_unit": 128,
        "origin": "Assets/Resources/Frontier/Art (accepted)",
        "unity_editor_bake": "Starfall/Animation/Bake Accepted Hero To Live 3-Class Animator",
        "native_unity_verified": False,
        "atlases": {}
    }
    for klass in CLASSES:
        for action in ACTIONS:
            for facing in FACINGS:
                refs = source_indices(klass, action, facing)
                frames = []
                for i, (name, index) in enumerate(refs):
                    frame = canonical_frame(sheets, heads, name, index, facing,
                                             name.startswith("Walk") or action in ("Idle", "Run"))
                    if frame.size != (CELL, CELL):
                        raise AssertionError("Inconsistent canonical frame canvas")
                    # These cells must be byte-for-byte identical to the
                    # QA previews, or the in-game and inspection rigs diverged.
                    preview = PREVIEW / klass / action / f"{facing}_{i:02}.png"
                    with Image.open(preview) as source:
                        if frame.tobytes() != source.convert("RGBA").tobytes():
                            raise AssertionError("Export/QA mismatched pixel rig: " + str(preview))
                    frames.append(frame)
                assert frames and all(f.getchannel("A").getbbox() for f in frames)
                atlas = strip(frames)
                key = f"Hero_{klass}_{action}_{facing}"
                out = DEST / (key + ".png")
                atlas.save(out, optimize=True)
                with Image.open(out) as check:
                    assert check.size == (CELL * len(refs), CELL)
                    assert check.mode == "RGBA"
                raster_hashes = [hashlib.sha256(frame.tobytes()).hexdigest() for frame in frames]
                manifest["atlases"][key] = {
                    "path": out.name,
                    "source_frames": [f"{name}:{index}" for name, index in refs],
                    "frame_count": len(frames),
                    "unique_rasters": len(set(raster_hashes)),
                    "frame_sha256_rgba": raster_hashes,
                    "needs_new_art": action in ("Cast", "Dodge", "Hit", "Death")
                }
    if len(manifest["atlases"]) != 63:
        raise AssertionError("Expected 3 classes x 7 actions x 3 facings")
    (DEST / "manifest.json").write_text(
        json.dumps(manifest, indent=2, ensure_ascii=False) + "\n", encoding="utf-8"
    )
    print(f"PASS: 63 production-importable canonical accepted-source sprite sheets exported to {DEST}.")
    print("Quality note: source poses include sparse cast/dodge/hit/death; no invented in-betweens.")
    print("NATIVE_UNVERIFIED: generated Unity import, Animator playback, in-game visuals and FPS.")


if __name__ == "__main__":
    main()
