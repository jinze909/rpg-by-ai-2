#!/usr/bin/env python3
"""Build reviewable, root-registered FULL-BODY hero previews from accepted atlases.

Every frame is a 256x256 PNG, preserving one shared WalkVanguard blonde head.
This is a genuine offline animation QA/repacking pass, NOT newly drawn in-betweens,
new approved art, or Unity Editor / PlayMode validation.
"""
import argparse
import hashlib
import json
import re
from pathlib import Path
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[1]
ART = ROOT / "Assets/Resources/Frontier/Art"
REG = ROOT / "Assets/Scripts/Frontier/Runtime/ActorFrameRegistration.cs"
DEFAULT_OUT = ROOT / ".starfall-checks/hero-animation-qa"
CLASSES = ("Vanguard", "Ranger", "Arcanist")
ACTIONS = ("Idle", "Run", "Attack", "Cast", "Dodge", "Hit", "Death")
FACINGS = ("E", "N", "S")
CELL = 256
FOOT = (128, 240)
STABLE_HAIR = (100, 140, 56, 56)


def registered_heads():
    source = REG.read_text(encoding="utf-8")
    result = {}
    for name, body in re.findall(r'\{\s*"([^"]+)",\s*new\s*\[\]\s*\{([^}]+)\}\s*\}', source):
        coords = [tuple(map(int, m)) for m in re.findall(
            r'new Rect\((\d+),(\d+),(\d+),(\d+)\)', body)]
        if len(coords) == 12:
            result[name] = coords
    mandatory = ["WalkVanguard", "WalkRanger", "WalkArcanist",
                 "PlayerVanguard", "PlayerRanger", "PlayerArcanist",
                 "PlayerRear", "PlayerSouth", "PlayerReactionsRear",
                 "PlayerReactionsSouth"]
    missing = sorted(set(mandatory) - set(result))
    if missing:
        raise AssertionError("Missing actual head registration: " + str(missing))
    return result


def imported_sheets():
    names = ["WalkVanguard", "WalkRanger", "WalkArcanist",
             "PlayerVanguard", "PlayerRanger", "PlayerArcanist",
             "PlayerRear", "PlayerSouth", "PlayerReactionsRear", "PlayerReactionsSouth"]
    sheets = {}
    for name in names:
        path = ART / (name + ".png")
        source = Image.open(path).convert("RGBA")
        if source.size != (4 * CELL, 3 * CELL):
            raise AssertionError(f"Unexpected imported atlas size {name}: {source.size}")
        sheets[name] = source
    return sheets


def crop_frame(source, frame):
    x, y = (frame % 4) * CELL, (frame // 4) * CELL
    return source.crop((x, y, x + CELL, y + CELL))


def source_indices(profession, action, facing):
    c = CLASSES.index(profession)
    f = FACINGS.index(facing)
    player, walk = "Player" + profession, "Walk" + profession
    if action == "Idle":
        return [(walk, 8)] if f == 2 else [(player, 1 if f == 1 else 0)]
    if action == "Run":
        return [(walk, f * 4 + i) for i in range(4)]
    if action == "Attack":
        if f == 1:
            return [(player, 1), ("PlayerRear", c * 4 + 2),
                    ("PlayerRear", c * 4 + 3), (player, 1)]
        if f == 2:
            # South frames 2/3 are SKILL poses, not normal recovery.
            return [(walk, 8), ("PlayerSouth", c * 4),
                    ("PlayerSouth", c * 4 + 1), (walk, 8)]
        # Side frames 6/7 are heavy/skill poses, not normal recovery.
        return [(player, 0), (player, 4), (player, 5), (player, 0)]
    # Rooted skill timeline: accepted Idle -> windup -> contact -> Idle recovery.
    # The last source raster is IDENTICAL to the first (no body/head snap).
    if action == "Cast":
        idle = (walk, 8) if f == 2 else (player, 1 if f == 1 else 0)
        if f == 1:
            return [idle, ("PlayerRear", c * 4 + 2),
                    ("PlayerRear", c * 4 + 3), idle]
        if f == 2:
            return [idle, ("PlayerSouth", c * 4 + 2),
                    ("PlayerSouth", c * 4 + 3), idle]
        return [idle, (player, 6), (player, 7), idle]
    reaction = "PlayerReactionsRear" if f == 1 else "PlayerReactionsSouth" if f == 2 else player
    dodge = c * 4 if f != 0 else 8
    hit = c * 4 + 2 if f != 0 else 10
    fallen = c * 4 + 3 if f != 0 else 11
    idle = (walk, 8) if f == 2 else (player, 1 if f == 1 else 0)
    if action == "Dodge":
        return [idle, (reaction, dodge), idle]
    if action == "Hit":
        return [idle, (reaction, hit), idle]
    if action == "Death":
        return [idle, (reaction, hit), (reaction, dodge), (reaction, fallen)]
    raise AssertionError(f"Unknown action {action}")


def canonical_frame(sheets, heads, name, index, facing, locomotion, death=False):
    # The production IMGUI HumanActor cuts out the source head and draws
    # WalkVanguard's canonical head in its place. Bake the identical identity
    # into one whole-body cell BEFORE migrating to SpriteRenderer.
    canvas = crop_frame(sheets[name], index)
    if death:
        # The crouch->fallen sequence keeps the APPROVED lying blonde head;
        # upright identity overlaid on a prone body would be detached.
        return canvas
    hx, hy, hw, hh = heads[name][index]
    if min(hx, hy, hw, hh) < 0 or hx + hw > CELL or hy + hh > CELL:
        raise AssertionError(f"Invalid face cutout: {name}/{index}")
    identity = crop_frame(sheets["WalkVanguard"], FACINGS.index(facing) * 4)
    rx, ry, rw, rh = STABLE_HAIR
    golden_head = identity.crop((rx, ry, rx + rw, ry + rh))
    if not golden_head.getchannel("A").getbbox():
        raise AssertionError("Canonical blonde hero head is blank")
    # Do NOT cover the equipment silhouettes around the head area.
    canvas.paste((0, 0, 0, 0), (hx, hy, hx + hw, hy + hh))
    dx = round((128 if locomotion else hx + hw / 2) - rw / 2)
    dy = round(ry if locomotion else hy + hh - rh)
    canvas.alpha_composite(golden_head, (dx, dy))
    if canvas.getchannel("A").getbbox() is None:
        raise AssertionError(f"Composited blank hero {name}/{index}")
    return canvas


def strip(images):
    result = Image.new("RGBA", (len(images) * CELL, CELL), (0, 0, 0, 0))
    for i, frame in enumerate(images):
        result.alpha_composite(frame, (i * CELL, 0))
    return result


def build(destination):
    sheets, heads = imported_sheets(), registered_heads()
    destination.mkdir(parents=True, exist_ok=True)
    report = {"canvas": [256, 256], "canonical_pivot": list(FOOT),
              "art_origin": "accepted-existing-atlas; no new in-betweens",
              "native_unity_verified": False, "action_sets": {}}
    overview = Image.new("RGB", (7 * 256, 3 * 3 * 256), (22, 27, 34))
    for c, profession in enumerate(CLASSES):
        for f, facing in enumerate(FACINGS):
            for a, action in enumerate(ACTIONS):
                frames = []
                references = source_indices(profession, action, facing)
                for i, (name, index) in enumerate(references):
                    frame = canonical_frame(sheets, heads, name, index, facing,
                        name.startswith("Walk") or action in ("Idle", "Run")
                        or (name == "Player" + profession and index <= 1),
                        death=(action == "Death" and i == len(references) - 1))
                    frames.append(frame)
                hashes = [hashlib.sha256(im.tobytes()).hexdigest() for im in frames]
                unique = len(set(hashes))
                key = f"{profession}/{action}/{facing}"
                warnings = []
                if unique < len(frames):
                    warnings.append("identical physical pose in sequence; hold timing instead")
                if action in ("Cast", "Dodge", "Hit", "Death"):
                    warnings.append("shared legacy reaction cells, needs authored redraw")
                if len(frames) < 3 and action != "Idle":
                    warnings.append("insufficient unique in-betweens for third-stage acceptance")
                report["action_sets"][key] = {
                    "source": [f"{name}:{index}" for name, index in references],
                    "frames": len(frames), "unique_rasters": unique,
                    "warnings": warnings}
                action_dir = destination / profession / action
                action_dir.mkdir(parents=True, exist_ok=True)
                for i, frame in enumerate(frames):
                    frame.save(action_dir / f"{facing}_{i:02}.png")
                strip(frames).save(action_dir / f"{facing}_strip.png")
                # Matrix is inspectable without unity: all 3 professions,
                # all directions, all seven actions, same root and scale.
                overview.paste(frames[0], (a * CELL, (c * 3 + f) * CELL), frames[0])
    overview.save(destination / "three_profession_action_matrix.png")
    (destination / "report.json").write_text(json.dumps(report, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    total = len(report["action_sets"])
    print(f"PASS: generated {total} three-class root-registered action previews at {destination}")
    print(f"Source art is inherited, not a completed new animation set. Pivot={FOOT}; native Unity unverified.")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--out", type=Path, default=DEFAULT_OUT)
    args = parser.parse_args()
    build(args.out)
