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
            if action in ("Run", "Attack", "Cast", "Dodge", "Hit") and original > 1 else original)


def motion_midpoint(first, second):
    """One-silhouette, anatomical-region motion toward the next approved pose.

    Earlier interpolation measured every extended limb as a single 'reach',
    often producing an identical midpoint or sliding a bow/staff in the
    wrong direction. Track LEFT/RIGHT leg and weapon/arm separately; deform
    only the corresponding region, blend motion smoothly into the fixed
    belt/torso, protect all hair/face pixels and anchor the boot contact.
    This is raster motion assistance, NOT hand-painted animation approval.
    """
    if first.size != (CELL, CELL) or second.size != (CELL, CELL):
        raise AssertionError("Motion midpoint requires canonical 256px cells")
    a, b = first.load(), second.load()
    transparent = (0, 0, 0, 0)

    def clamp(v, lo=0.0, hi=1.0):
        return max(lo, min(hi, v))

    def region_shift(bounds, limit_x, limit_y):
        x0, x1, y0, y1 = bounds

        def center(pixels):
            sx = sy = mass = 0.0
            for y in range(y0, y1, 2):
                top = CELL - 1 - y
                for x in range(x0, x1, 2):
                    # The canonical head must NEVER influence hand/body
                    # registration: its position is deliberately immutable.
                    if 91 <= x < 170 and 57 <= y < 118:
                        continue
                    alpha = pixels[x, top][3]
                    if alpha < 128:
                        continue
                    sx += x * alpha
                    sy += y * alpha
                    mass += alpha
            return (sx / mass, sy / mass) if mass else None

        p, q = center(a), center(b)
        if p is None or q is None:
            return (0, 0)
        return (round(clamp((q[0] - p[0]) / 2.0, -limit_x, limit_x)),
                round(clamp((q[1] - p[1]) / 2.0, -limit_y, limit_y)))

    torso = region_shift((103, 154, 30, 64), 3, 3)
    left_leg = region_shift((89, 128, 23, 58), 7, 5)
    right_leg = region_shift((129, 171, 23, 58), 7, 5)
    left_arm = region_shift((34, 121, 47, 145), 14, 10)
    right_arm = region_shift((140, 230, 47, 145), 14, 10)

    # Inverse-warp a SINGLE pose to avoid alpha mosaics or duplicated
    # outlines. Spatial motion gates never change canvas/Pivot or palette.
    output = Image.new("RGBA", (CELL, CELL), transparent)
    dest = output.load()
    for y in range(CELL):  # canonical bottom-origin coordinate
        legs = clamp((y - 22) / 23.0) * clamp((66 - y) / 20.0)
        shoulders = clamp((y - 42) / 22.0)
        torso_gate = clamp((y - 22) / 28.0) * clamp((73 - y) / 18.0)
        for x in range(CELL):
            if 91 <= x < 170 and 57 <= y < 118:
                dest[x, CELL - 1 - y] = a[x, CELL - 1 - y]
                continue
            side = clamp((abs(x - 128) - 13) / 29.0)
            right = clamp((x - 122) / 14.0)
            left_leg_w = legs * (1.0 - right)
            right_leg_w = legs * right
            left_arm_w = shoulders * side * (1.0 - right)
            right_arm_w = shoulders * side * right
            body_w = torso_gate * (1.0 - side) * (1.0 - legs)
            total = left_leg_w + right_leg_w + left_arm_w + right_arm_w
            if total > 1.0:
                left_leg_w /= total
                right_leg_w /= total
                left_arm_w /= total
                right_arm_w /= total
            shift_x = (torso[0] * body_w + left_leg[0] * left_leg_w +
                       right_leg[0] * right_leg_w + left_arm[0] * left_arm_w +
                       right_arm[0] * right_arm_w)
            shift_y = (torso[1] * body_w + left_leg[1] * left_leg_w +
                       right_leg[1] * right_leg_w + left_arm[1] * left_arm_w +
                       right_arm[1] * right_arm_w)
            sx, sy = x - round(shift_x), y - round(shift_y)
            dest[x, CELL - 1 - y] = (
                a[sx, CELL - 1 - sy]
                if 0 <= sx < CELL and 0 <= sy < CELL else transparent
            )
    if output.getchannel("A").getbbox() is None:
        raise AssertionError("Empty generated motion pose")
    return output


def expand_frames(originals, action):
    result = []
    for i, source in enumerate(originals):
        result.append(source)
        if action in ("Run", "Attack", "Cast", "Dodge", "Hit") and len(originals) > 1:
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
    # Release-blocker in BOTH offline CI and the guarded GitHub PNG
    # publisher, not an optional post-hoc report.
    from check_hero_whole_body_integrity import audit as audit_full_body
    audit_full_body(DEST, PREVIEW)
    print(f"PASS: 63 canonical hero atlases exported with {total_midpoints} motion-assisted midpoints.")
    print("Unmodified source poses remain at even indices; all source identity pixels preserved.")
    print("ART/NATIVE_UNVERIFIED: hand-redrawn quality, playmode, Animator, GPU, FPS.")


if __name__ == "__main__":
    main()
