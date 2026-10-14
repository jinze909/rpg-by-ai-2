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
def expected_count(action, original):
    return (2 * original - (0 if action == "Run" else 1)
            if action in ("Run", "Attack", "Cast") and original > 1 else original)


def motion_midpoint(first, second):
    """A coherent source-silhouette motion pass: NO Bayer ghost outlines.

    Each interpolated frame is a pixel-preserving deformation of ONE approved
    pose, guided by body/outer-arm motion toward the next approved pose. Never
    average two competing silhouettes or draw a second weapon/leg outline.
    Lock the complete golden head, preserve the grounded boot contact band,
    and taper deformation near the neck. This is still generated scaffolding,
    NOT a hand-drawn transition or native Unity art acceptance.
    """
    if first.size != (CELL, CELL) or second.size != (CELL, CELL):
        raise AssertionError("Motion midpoint requires canonical 256px cells")
    a, b = first.load(), second.load()
    transparent = (0, 0, 0, 0)

    def centroid(pixels, x0, x1, y0, y1, outer):
        xsum = ysum = weight = 0.0
        for y in range(y0, y1, 2):
            top = CELL - 1 - y
            for x in range(x0, x1, 2):
                if outer and 94 <= x < 162:
                    continue  # ignore the torso/head when tracking the weapon arm
                w = pixels[x, top][3]
                if w < 128:
                    continue
                xsum += x * w
                ysum += y * w
                weight += w
        return (xsum / weight, ysum / weight) if weight else None

    def movement(area, limit_x, limit_y):
        origin = centroid(a, *area)
        target = centroid(b, *area)
        if origin is None or target is None:
            return (0, 0)
        return (
            max(-limit_x, min(limit_x, round((target[0] - origin[0]) * .5))),
            max(-limit_y, min(limit_y, round((target[1] - origin[1]) * .5))),
        )

    # Separate motion estimates prevent a swinging weapon from dragging the
    # torso/head, or a stepping body from pinning the weapon in place.
    body = movement((86, 172, 22, 64, False), 5, 4)
    reach = movement((20, 236, 30, 122, True), 12, 9)
    if reach == (0, 0):
        reach = body

    output = Image.new("RGBA", (CELL, CELL), transparent)
    dest = output.load()
    for y in range(CELL):  # y is measured from Unity's BOTTOM origin
        grounded = max(0.0, min(1.0, (y - 22.0) / 44.0))
        guard_y = (max(0.0, min(1.0, (57.0 - y) / 12.0))
                   if y < 57 else
                   max(0.0, min(1.0, (y - 117.0) / 12.0)) if y >= 118 else 0.0)
        for x in range(CELL):
            if 91 <= x < 170 and 57 <= y < 118:
                dest[x, CELL - 1 - y] = a[x, CELL - 1 - y]
                continue
            outside_body = max(0.0, min(1.0, (abs(x - 128.0) - 26.0) / 44.0))
            guard_x = max(0.0, min(1.0, (abs(x - 130.0) - 39.0) / 12.0))
            amount = grounded * max(guard_x, guard_y)
            dx = round((body[0] * (1.0 - outside_body) +
                        reach[0] * outside_body) * amount)
            dy = round((body[1] * (1.0 - outside_body) +
                        reach[1] * outside_body) * amount)
            sample_x, sample_y = x - dx, y - dy
            dest[x, CELL - 1 - y] = (
                a[sample_x, CELL - 1 - sample_y]
                if 0 <= sample_x < CELL and 0 <= sample_y < CELL
                else transparent
            )
    if output.getchannel("A").getbbox() is None:
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
