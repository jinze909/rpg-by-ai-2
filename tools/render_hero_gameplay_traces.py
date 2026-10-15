#!/usr/bin/env python3
"""Render TRUE game-state-driven, published Sprite traces (NOT Unity screenshots).

Input comes from C# runtime input and the *private production*
ResolveFullBodyHeroFrame selector, not invented GIF playback positions.
A missing or cross-class actual production resource is a HARD CI failure.
Known foot sliding during rooted recovery is a visual-review warning; don't
pretend a pipeline can repair art by duplicating or time-warping pixels.
"""
import csv
import json
from collections import defaultdict
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont, ImageOps

ROOT = Path(__file__).resolve().parents[1]
TRACE = ROOT / ".starfall-checks/hero-gameplay-traces"
ATLAS = ROOT / "Assets/Resources/Frontier/HeroAnimator/Canonical"
CLASSES = ("Vanguard", "Ranger", "Arcanist")
ACTIONS = ("Attack", "Cast")
FACES = ("E", "W", "N", "S")
CELL = 256
FONT = ImageFont.load_default()
BG = (24, 32, 43)
ART_CACHE = {}


def sprite_for(row):
    asset = row["asset"]
    path = ATLAS / (asset + ".png")
    assert path.is_file(), ("LIVE_GAME_REFERENCES_UNSHIPPED_HERO_ATLAS", asset)
    if asset not in ART_CACHE:
        with Image.open(path) as img:
            assert img.format == "PNG" and img.mode == "RGBA"
            ART_CACHE[asset] = img.copy()
    atlas = ART_CACHE[asset]
    index = int(row["cell"])
    count = int(row["cell_count"])
    assert atlas.height == CELL and atlas.width == count * CELL, (
        "LIVE_GAME_ATLAS_LENGTH_MISMATCH", asset, count, atlas.size)
    assert 0 <= index < count, ("LIVE_GAME_CELL_OUT_OF_BOUNDS", row)
    img = atlas.crop((index*CELL, 0, (index+1)*CELL, CELL))
    if row["flip"] == "1":
        img = ImageOps.mirror(img)
    return img


def validate_row(row):
    profession = row["profession"]
    command = row["command"]
    direction = row["direction"]
    assert profession in CLASSES and command in ACTIONS and direction in FACES
    assert row["root_moving"] in ("0", "1")
    assert row["flip"] in ("0", "1")
    assert row["action_live"] in ("0", "1")
    assert row["phase"] in ("None", "Windup", "Active", "Recovery")
    live = row["action_live"] == "1"
    if live:
        assert row["asset"] == f"Hero_{profession}_{command}_{'E' if direction == 'W' else direction}", (
            "LIVE_ACTION_CHANGED_PROFESSION_POSE_OR_DIRECTION", row)
        if row["root_moving"] == "1" and row["phase"] == "Recovery":
            assert abs(float(row["walk_distance"])) < 0.0001, (
                "INVISIBLE_RUN_ADVANCED_WHILE_CAST_PLANTED", row)
    if direction == "W" and live:
        assert row["flip"] == "1", ("WEST_ACTOR_WAS_NOT_MIRRORED", row)
    sprite_for(row)


def reject_corruption():
    template = dict(profession="Vanguard", command="Cast", direction="E",
                    tick="0", phase="Windup", root_x="12", root_y="14",
                    root_moving="0", footsteps="0", walk_distance="0",
                    appearance="0", asset="Hero_Vanguard_Cast_E",
                    cell="0", cell_count="6", flip="0", action_live="1")
    validate_row(template)
    for label, mutation in (
        ("other_profession", {"asset": "Hero_Ranger_Cast_E"}),
        ("wrong_facing", {"asset": "Hero_Vanguard_Cast_N"}),
        ("broken_cell", {"cell": "77"}),
        ("invisible_steps", {"phase": "Recovery", "root_moving": "1",
                             "walk_distance": "0.95"}),
        ("west_not_flipped", {"direction": "W", "flip": "0"}),
    ):
        altered = dict(template, **mutation)
        try:
            validate_row(altered)
        except (AssertionError, ValueError):
            pass
        else:
            raise AssertionError("UNSAFE_CI_ACCEPTED_CORRUPT_GAMEPLAY_TRACE_" + label)


def compact_sprite(row, width=176):
    rgba = sprite_for(row)
    target = rgba.resize((width, width), resample=Image.Resampling.NEAREST)
    page = Image.new("RGBA", target.size, BG + (255,))
    page.alpha_composite(target)
    return page.convert("RGB")


def tile(row, first, width=176):
    panel = Image.new("RGB", (width + 12, width + 85), BG)
    art = compact_sprite(row, width)
    panel.paste(art, (6, 21))
    draw = ImageDraw.Draw(panel)
    x0, y0 = float(first["root_x"]), float(first["root_y"])
    dx, dy = float(row["root_x"]) - x0, float(row["root_y"]) - y0
    moving = row["action_live"] == "1" and row["phase"] == "Recovery" and row["root_moving"] == "1"
    draw.text((5, 4), f't={int(row["tick"])*.02:.2f}  {row["phase"]}', font=FONT,
              fill=(235, 238, 245))
    draw.text((5, width + 29), f'{row["asset"].split("_")[2]} #{row["cell"]}  foot pivot 128,240',
              font=FONT, fill=(199, 216, 228))
    draw.text((5, width + 43), f'world displacement ({dx:+.2f},{dy:+.2f})',
              font=FONT, fill=(214, 207, 180))
    draw.text((5, width + 58),
              "ROOT MOVED / FOOT SLIDE REVIEW" if moving else
              f'audio footsteps {row["footsteps"]}', font=FONT,
              fill=(235, 144, 125) if moving else (170, 206, 188))
    return panel, moving


def create():
    reject_corruption()
    file = TRACE / "gameplay-trace.csv"
    assert file.is_file(), ("ACTUAL_GAMEPLAY_TRACE_MISSING", file)
    with file.open(newline="", encoding="utf-8") as handle:
        rows = list(csv.DictReader(handle))
    assert len(rows) == 960, ("INCOMPLETE_LIVE_HERO_GAMEPLAY_TRACE", len(rows))
    groups = defaultdict(list)
    slip_count = 0
    for row in rows:
        validate_row(row)
        group = (row["profession"], row["command"], row["direction"])
        groups[group].append(row)
        if row["action_live"] == "1" and row["phase"] == "Recovery" and row["root_moving"] == "1":
            slip_count += 1
    assert len(groups) == 24, ("MISSING_3_CLASS_2_ACTION_4_DIRECTION_TRACE", len(groups))
    TRACE.mkdir(parents=True, exist_ok=True)
    report = {
        "status": "VERIFIED_REAL_GAMECORE_PRODUCTION_SPRITE_SELECTOR_TRACE",
        "actual_simulated_gameplay_ticks": len(rows), "scenarios": len(groups),
        "source": "C# FrontierRuntime.AdvanceVisualReview and private ResolveFullBodyHeroFrame",
        "artist_approved": False, "native_unity_verified": False,
        "moving_root_under_planted_action_frames": slip_count,
        "moving_root_caution": "Root translation during an unchanged grounded full-body skill frame still needs new authored moving-recovery art",
        "adversarial_rejections": [
            "wrong profession", "wrong facing", "out-of-range Sprite cell",
            "hidden gait beneath action", "unmirrored west-facing character"],
        "groups": [],
    }
    for profession in CLASSES:
        for command in ACTIONS:
            for direction in FACES:
                key = (profession, command, direction)
                series = groups[key]
                assert len(series) == 40 and [int(r["tick"]) for r in series] == list(range(40)), (
                    "CORRUPT_REAL_FRAME_ORDER", key)
                assert any(r["phase"] == "Recovery" for r in series), (
                    "ACTION_CONTACT_OR_RECOVERY_NOT_REACHED", key)
                initial = series[0]
                keys = (0, 4, 8, 12, 16, 20, 24, 29, 34, 39)
                filmstrip = Image.new("RGB", (5*188, 2*261), BG)
                warning = 0
                for pos, idx in enumerate(keys):
                    rendered, sliding = tile(series[idx], initial)
                    warning += int(sliding)
                    filmstrip.paste(rendered, ((pos % 5)*188, (pos // 5)*261))
                name = f"{profession}_{command}_{direction}"
                filmstrip.save(TRACE / (name + "_actual-gameplay-filmstrip.png"))
                report["groups"].append({
                    "profession": profession, "action": command, "facing": direction,
                    "ticks": len(series),
                    "art_review_required": True,
                    "root_motion_warning_samples": sum(
                        r["action_live"] == "1" and r["phase"] == "Recovery" and
                        r["root_moving"] == "1" for r in series),
                    "filmstrip": name + "_actual-gameplay-filmstrip.png",
                })
                if direction == "E":
                    # Real game-controlled action timing, no invented frame
                    # interpolation, and no missing sprite duplicates.
                    frames = []
                    for idx in range(0, 40, 2):
                        panel, _ = tile(series[idx], initial)
                        frames.append(panel.quantize(colors=128))
                    frames[0].save(
                        TRACE / (name + "_actual-gameplay.gif"),
                        save_all=True, append_images=frames[1:], duration=110,
                        loop=0, disposal=2, optimize=False)
    (TRACE / "gameplay-trace-qa.json").write_text(
        json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print("PASS: 960 REAL game-sampled sprite references, 24 character/action/facing "
          f"filmstrips, six actual action GIFs. {slip_count} moving-root "
          "samples retained as ART REVIEW (not falsified as fixed).")
    print("NATIVE_UNVERIFIED: offline GameCore+actual authored Sprite trace; "
          "not real GPU or GameView screenshots.")


if __name__ == "__main__":
    create()
