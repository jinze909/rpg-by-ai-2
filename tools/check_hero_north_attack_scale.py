#!/usr/bin/env python3
"""Detect north-facing hero enlargement on the actual installed Unity Sprite PNGs.

The three classes share a fixed 256px cell/boot pivot. A large head growth
between Idle_N and Attack_N is a broken drawing, even when the PNG dimensions
and Unity Sprite import metadata remain valid. This is raster QA, not PlayMode.
"""
import json
from pathlib import Path
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
ART = ROOT / "Assets/Resources/Frontier/HeroAnimator/Canonical"
OUT = ROOT / ".starfall-checks/hero-runtime-qa/north-attack-scale.json"
CLASSES = ("Vanguard", "Ranger", "Arcanist")
CELL = 256


def frame(key, slot):
    with Image.open(ART / (key + ".png")) as file:
        atlas = file.convert("RGBA")
    assert atlas.height == CELL and atlas.width % CELL == 0, (
        "NORTH_ATTACK_INVALID_ATLAS", key, atlas.size)
    assert 0 <= slot < atlas.width // CELL, (key, slot)
    return atlas.crop((slot * CELL, 0, (slot + 1) * CELL, CELL))


def head_metric(image):
    # Isolate the GOLDEN head, not the extended weapon, spell or cape.
    # Central 44px region covers the approved back-of-head silhouette.
    # This does not mistake a raised staff/bow for a 2x larger body.
    pixels = image.load()
    hair = []
    for y in range(146, 198):
        for x in range(106, 155):
            r, g, b, a = pixels[x, y]
            if a >= 70 and r >= 150 and g >= 95 and (
                    r > g * 1.07 and g > b * 1.04):
                hair.append((x, y))
    assert len(hair) >= 25, ("NORTH_ATTACK_HAIR_MISSING", len(hair))
    left = min(x for x, _ in hair)
    right = max(x for x, _ in hair)
    top = min(y for _, y in hair)
    bottom = max(y for _, y in hair)
    return {"width": right - left + 1, "height": bottom - top + 1,
            "pixels": len(hair), "center": [(left + right) / 2,
                                            (top + bottom) / 2]}


def overscale(baseline, current):
    # A small directional head tilt or hand occlusion is allowed; a 1.6x
    # head is not. Compare against SAME class and SAME facing.
    return (current["width"] > baseline["width"] * 1.55 or
            current["height"] > baseline["height"] * 1.55)


def main():
    assert overscale({"width": 25, "height": 30},
                     {"width": 46, "height": 48}), (
                         "NORTH_SCALE_NEGATIVE_CONTROL_BROKEN")
    result = {}
    for klass in CLASSES:
        idle = head_metric(frame(f"Hero_{klass}_Idle_N", 0))
        run = [head_metric(frame(f"Hero_{klass}_Run_N", i))
               for i in range(4)]
        attack_key = f"Hero_{klass}_Attack_N"
        with Image.open(ART / (attack_key + ".png")) as file:
            count = file.width // CELL
        assert count in (6, 7), ("NORTH_ATTACK_FRAME_COUNT", klass, count)
        attack = [head_metric(frame(attack_key, i)) for i in range(count)]
        for i, pose in enumerate(attack):
            assert not overscale(idle, pose), (
                "NORTH_ATTACK_HEAD_ENLARGEMENT", klass, i, idle, pose)
        result[klass] = {"idle_north": idle, "run_north": run,
                         "attack_north": attack,
                         "largest_width_ratio": round(
                             max(p["width"] for p in attack) /
                             idle["width"], 3),
                         "largest_height_ratio": round(
                             max(p["height"] for p in attack) /
                             idle["height"], 3)}
        print("NORTH ATTACK", klass,
              "max head width x", result[klass]["largest_width_ratio"],
              "height x", result[klass]["largest_height_ratio"])
    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text(json.dumps({"status": "raster_proportions_only",
                               "native_unity_verified": False,
                               "professions": result}, indent=2) + "\n",
                   encoding="utf-8")
    print("PASS all 3 class North Attack head proportions; "
          "Animator root scale guarded separately by native C# probe.")
    print("NATIVE_UNVERIFIED: no Unity GameView/PlayMode executed")


if __name__ == "__main__":
    main()
