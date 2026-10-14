#!/usr/bin/env python3
"""Produce 63 inspectable Sprite sheets, including motion-assisted intermediate poses.

The originals are the user's ACCEPTED source art and canonical blonde head.
Midpoints are reproducible algorithmic scaffolding: NOT hand-drawn, not visually
approved, and not proof of native Unity gameplay/Animator performance.
The live Unity production Editor builder performs the corresponding motion pass.
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
ORDERED4 = (0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5)


def expected_count(action, original):
    return (2 * original - (0 if action == "Run" else 1)
            if action in ("Run", "Attack", "Cast") and original > 1 else original)


def motion_midpoint(first, second):
    """Port of the Editor body's bounded movement and opaque Bayer compositor.

    Internal anchor mathematics uses Unity bottom-origin y; Image coordinates
    are top-origin. Displaced HEAD pixels come only from existing golden heads.
    """
    if first.size != (CELL, CELL) or second.size != (CELL, CELL):
        raise AssertionError("Motion midpoint requires canonical 256px cells")
    a, b = first.load(), second.load()
    ax = ay = aw = bx = by = bw = 0.0
    for y in range(28, 118, 2):
        top = CELL - 1 - y
        for x in range(86, 172, 2):
            aa, bb = a[x, top][3] / 255.0, b[x, top][3] / 255.0
            ax += aa * x
            ay += aa * y
            aw += aa
            bx += bb * x
            by += bb * y
            bw += bb
    dx = max(-6, min(6, round(((bx / bw) - (ax / aw)) * 0.5))) if min(aw, bw) > 1 else 0
    dy = max(-5, min(5, round(((by / bw) - (ay / aw)) * 0.5))) if min(aw, bw) > 1 else 0
    transparent = (0, 0, 0, 0)

    def pixel(img, x, bottom_y):
        return img[x, CELL - 1 - bottom_y] if 0 <= x < CELL and 0 <= bottom_y < CELL else transparent

    output = Image.new("RGBA", (CELL, CELL), transparent)
    dest = output.load()
    for y in range(CELL):
        root_weight = max(0.0, min(1.0, (y - 18.0) / 64.0))
        ox = round(dx * root_weight)
        oy = round(dy * root_weight)
        for x in range(CELL):
            pa, pb = pixel(a, x - ox, y - oy), pixel(b, x + ox, y + oy)
            if pa[3] > 127 and pb[3] > 127:
                if 91 <= x < 170 and 57 <= y < 118:
                    result = pa   # no ghosted duplicate face
                else:
                    result = tuple(round((u + v) * .5) for u, v in zip(pa, pb))
            else:
                second_pixel = ORDERED4[(y & 3) * 4 + (x & 3)] < 8
                result = pb if second_pixel else pa
            dest[x, CELL - 1 - y] = result
    if not output.getchannel("A").getbbox():
        raise AssertionError("Empty generated motion pose")
    return output


def expand_frames(originals, action):
    result = []
    for i, source in enumerate(originals):
        result.append(source)
        if action in ("Run", "Attack", "Cast") and len(originals) > 1:
            if i + 1 < len(originals) or action == "Run":
                result.append(motion_midpoint(source, originals[(i + 1) % len(originals)]))
    if len(result) != expected_count(action, len(originals)):
        raise AssertionError("Generator/Animator count mismatch")
    return result


def main():
    DEST.mkdir(parents=True, exist_ok=True)
    sheets, heads = imported_sheets(), registered_heads()
    manifest = {
        "status": "accepted_source_plus_algorithmic_motion_midpoints_not_art_approved",
        "canvas_per_frame": [CELL, CELL],
        "top_origin_foot_px": list(FOOT),
        "unity_bottom_origin_pivot": [0.5, 0.0625],
        "pixels_per_unit": 128,
        "origin": "Assets/Resources/Frontier/Art (accepted)",
        "unity_editor_bake": "Starfall/Animation/Bake Accepted Hero To Live 3-Class Animator",
        "native_unity_verified": False,
        "art_direction_acceptance": False,
        "atlases": {},
    }
    total_midpoints = 0
    for profession in CLASSES:
        for action in ACTIONS:
            for facing in FACINGS:
                refs = source_indices(profession, action, facing)
                originals = []
                for i, (name, index) in enumerate(refs):
                    frame = canonical_frame(sheets, heads, name, index, facing,
                        name.startswith("Walk") or action in ("Idle", "Run")
                        or (name == "Player" + profession and index <= 1),
                        death=(action == "Death" and i == len(refs) - 1))
                    if frame.size != (CELL, CELL):
                        raise AssertionError("Wrong canonical frame canvas")
                    reference = PREVIEW / profession / action / f"{facing}_{i:02}.png"
                    with Image.open(reference) as sampled:
                        if frame.tobytes() != sampled.convert("RGBA").tobytes():
                            raise AssertionError("Original/QA pose mismatch: " + str(reference))
                    originals.append(frame)
                frames = expand_frames(originals, action)
                total_midpoints += len(frames) - len(originals)
                key = f"Hero_{profession}_{action}_{facing}"
                output = DEST / (key + ".png")
                atlas = strip(frames)
                atlas.save(output, optimize=True)
                with Image.open(output) as check:
                    if check.size != (CELL * len(frames), CELL) or check.mode != "RGBA":
                        raise AssertionError("Atlas format did not survive export: " + str(output))
                hashes = [hashlib.sha256(f.tobytes()).hexdigest() for f in frames]
                manifest["atlases"][key] = {
                    "path": output.name,
                    "source_frames": [f"{n}:{i}" for n, i in refs],
                    "original_count": len(refs),
                    "generated_midpoints": len(frames) - len(refs),
                    "frame_count": len(frames),
                    "unique_rasters": len(set(hashes)),
                    "frame_sha256_rgba": hashes,
                    "needs_hand_redrawn_art": action in ("Cast", "Dodge", "Hit", "Death"),
                    "native_unity_verified": False,
                }

    if len(manifest["atlases"]) != 63 or total_midpoints <= 0:
        raise AssertionError("Hero export failed to generate all action sheets")
    (DEST / "manifest.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    print(f"PASS: 63 canonical hero atlases exported with {total_midpoints} motion-assisted midpoints.")
    print("Unmodified source poses remain at even indices; all source identity pixels preserved.")
    print("ART/NATIVE_UNVERIFIED: hand-redrawn quality, playmode, Animator, GPU, FPS.")


if __name__ == "__main__":
    main()
