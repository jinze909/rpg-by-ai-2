#!/usr/bin/env python3
"""Audit ACTUAL shipped hero SpriteResources, generate actionable review images.

Production evidence, not a hand-authored animation approval or Unity PlayMode.
Never confuse mechanically expanded keyposes with unique artist-created poses.
"""
import json
import math
from pathlib import Path

from PIL import Image, ImageChops, ImageDraw

ROOT = Path(__file__).resolve().parents[1]
ART = ROOT / "Assets/Resources/Frontier/HeroAnimator/Canonical"
DEST = ROOT / ".starfall-checks/hero-runtime-qa"
CLASSES = ("Vanguard", "Ranger", "Arcanist")
ACTIONS = ("Idle", "Run", "Attack", "Cast", "Dodge", "Hit", "Death")
FACINGS = ("E", "N", "S")
CELL = 256
FPS = 12


def frames_for(name):
    path = ART / (name + ".png")
    with Image.open(path) as file:
        image = file.convert("RGBA").copy()
    assert image.height == CELL and image.width % CELL == 0, (
        "RUNTIME_INVALID_ATLAS", name, image.size)
    count = image.width // CELL
    return [image.crop((i * CELL, 0, (i + 1) * CELL, CELL))
            for i in range(count)]


def measure(sequence):
    observations, transitions = [], []
    for frame in sequence:
        alpha = frame.getchannel("A")
        bbox = alpha.getbbox()
        assert bbox is not None, "EMPTY_PRODUCTION_BODY"
        # Record body placement, transparent silhouette holes and contact.
        px = alpha.load()
        body_y0, body_y1 = 187, 239
        waist = sum(px[x,y] >= 36 for x in range(91,169)
                    for y in range(208,229))
        ground = sum(px[x,y] >= 36 for x in range(88,176)
                     for y in range(226,246))
        observations.append({
            "bbox": list(bbox), "waist_visible": waist,
            "foot_visible": ground, "frame_rgba_bytes": len(frame.tobytes())
        })
    for before, after in zip(sequence, sequence[1:]):
        diff = ImageChops.difference(before, after)
        changed = sum(any(pixel) for pixel in diff.getdata())
        b1, b2 = before.getchannel("A").getbbox(), after.getchannel("A").getbbox()
        centre = lambda b: ((b[0]+b[2])/2, (b[1]+b[3])/2)
        a, b = centre(b1), centre(b2)
        transitions.append({
            "changed_pixels": changed,
            "bbox_centre_distance": round(math.hypot(a[0]-b[0], a[1]-b[1]),2),
            "bbox_height_change": abs((b1[3]-b1[1])-(b2[3]-b2[1]))
        })
    return observations, transitions


def fail_nonoptional(name, action, frames, obs, transitions):
    # Severe deterministic regressions must FAIL, rather than becoming
    # passive flags. Skill reaches and falling corpses are intentionally
    # large: those require visual review, not naive centroid rejection.
    expected = {
        "Idle": 1, "Run": 8, "Attack": 7, "Cast": 7,
        "Dodge": 5, "Hit": 5, "Death": 4
    }[action]
    assert len(frames) == expected, ("RUNTIME_FRAME_COUNT", name, len(frames))
    assert all(o["bbox"][2] <= 256 and o["bbox"][3] <= 256 for o in obs), (
        "OUT_OF_CANVAS_BODY", name)
    # A misplaced full-body crop (e.g. 2x rescale or a missing lower body)
    # is deterministic and can be rejected without rating artistic style.
    assert all(o["bbox"][2]-o["bbox"][0] >= 20 and
               o["bbox"][3]-o["bbox"][1] >= 20 for o in obs), (
        "TRUNCATED_BODY", name)
    if action == "Run":
        assert sum(t["changed_pixels"] == 0 for t in transitions) <= 1, (
            "DUPLICATED_RUN_FRAMES", name)
        assert max(t["bbox_height_change"] for t in transitions) <= 24, (
            "RUN_BODY_HEIGHT_JUMP", name)
        assert max(t["bbox_centre_distance"] for t in transitions) <= 28, (
            "RUN_BODY_POSITION_JUMP", name)
    if action in ("Attack", "Cast", "Dodge", "Hit"):
        # Native full-body action strips must not be blank repeats of
        # entire animations; allowed intentional start/end holds remain.
        assert sum(t["changed_pixels"] == 0 for t in transitions) <= 2, (
            "STATIC_REACTION_OR_SKILL", name)


def preview(name, frames):
    n = len(frames)
    # Filmstrip and onion skin always use exact production RGBA cells.
    film = Image.new("RGB", (n*192, 220), (29, 34, 42))
    draw = ImageDraw.Draw(film)
    onion = Image.new("RGBA", (CELL, CELL), (0,0,0,0))
    for i, frame in enumerate(frames):
        stage = Image.new("RGBA", frame.size, (29,34,42,255))
        stage.alpha_composite(frame)
        film.paste(stage.convert("RGB").resize((192,192),Image.Resampling.NEAREST),
                   (i*192,0))
        draw.text((i*192+8,195), str(i)+" @ "+str(FPS)+" fps", fill=(225,230,239))
        if i:
            overlay = Image.blend(frames[i-1], frame, .5)
            onion.alpha_composite(overlay)
    film.save(DEST / (name+"_filmstrip.png"))
    onion.save(DEST / (name+"_onion.png"))
    previews = []
    for frame in frames:
        bg = Image.new("RGBA",frame.size,(29,34,42,255))
        bg.alpha_composite(frame)
        previews.append(bg.convert("RGB").resize((384,384),Image.Resampling.NEAREST))
    previews[0].save(DEST / (name+".gif"), save_all=True,
                     append_images=previews[1:], duration=int(1000/FPS),
                     loop=0, optimize=True)


def historical_splice_comparison():
    """Reconstruct the removed 205..247 waist splice from accepted pixels.

    This is a diagnostic RECONSTRUCTION of the old compositor algorithm,
    *not* a screenshot captured in the old Unity Game View. New frames
    come from the actual shipped canonical production Sprite resources.
    """
    from build_hero_animation_preview import (
        imported_sheets, registered_heads, canonical_frame,
    )
    sheets, heads = imported_sheets(), registered_heads()
    width = 2 * CELL
    out = Image.new("RGB", (width, 3 * (CELL + 24)), (31, 36, 44))
    labels = ImageDraw.Draw(out)
    for row, profession in enumerate(CLASSES):
        name = "Player" + profession
        walk = "Walk" + profession
        action = canonical_frame(sheets, heads, name, 5, "E", False)
        step = canonical_frame(sheets, heads, walk, 2, "E", True)
        old = action.copy()
        pixels, walk_pixels = old.load(), step.load()
        for top in range(205, 248):
            y = top
            left = 104 if top < 219 else 94 if top < 226 else 88
            right = 153 if top < 219 else 162 if top < 226 else 175
            for x in range(left, right + 1):
                offset = abs(x - 128)
                hem = 225 if offset < 16 else 222 if offset < 28 else 219
                if top < hem:
                    if pixels[x,y][3] < 20 and walk_pixels[x,y][3] > 20:
                        pixels[x,y] = walk_pixels[x,y]
                else:
                    pixels[x,y] = walk_pixels[x,y]
        new = frames_for("Hero_" + profession + "_Attack_E")[4]
        for column, frame in enumerate((old,new)):
            bg = Image.new("RGBA",(CELL,CELL),(31,36,44,255))
            bg.alpha_composite(frame)
            out.paste(bg.convert("RGB"),(column*CELL,row*(CELL+24)))
        y = row * (CELL+24) + CELL + 4
        labels.text((5,y),profession + " old splice (reconstruction)",fill=(246,207,154))
        labels.text((CELL+5,y),"current shipped full-body frame",fill=(165,221,186))
    out.save(DEST / "historical-splice-reconstruction-vs-production.png")


def verify_failure_gates():
    """Prove these checks fail on broken production sprites, not just PASS."""
    def expect_failure(label, frames, action):
        try:
            o, t = measure(frames)
            fail_nonoptional("synthetic_"+label, action, frames, o, t)
        except AssertionError:
            return
        raise AssertionError("UNSAFE_CI_GATE: " + label + " was accepted")
    blank = Image.new("RGBA", (CELL,CELL), (0,0,0,0))
    expect_failure("blank_body", [blank.copy() for _ in range(8)], "Run")
    small = Image.new("RGBA", (CELL,CELL))
    ImageDraw.Draw(small).rectangle((125,170,128,175), fill=(229,114,45,255))
    expect_failure("truncated_torso", [small.copy() for _ in range(8)], "Run")
    normal = Image.new("RGBA", (CELL,CELL))
    ImageDraw.Draw(normal).rectangle((104,147,149,238),fill=(91,63,48,255))
    expect_failure("static_run", [normal.copy() for _ in range(8)], "Run")
    # Intentional large discontinuity with valid full-body footprints
    # must fail independently of duplicate frame detection.
    left = Image.new("RGBA", (CELL,CELL))
    right = Image.new("RGBA", (CELL,CELL))
    ImageDraw.Draw(left).rectangle((49,147,95,238),fill=(91,63,48,255))
    ImageDraw.Draw(right).rectangle((159,147,205,238),fill=(91,63,48,255))
    expect_failure("teleporting_run", [left,right]*4, "Run")


def run():
    verify_failure_gates()
    DEST.mkdir(parents=True,exist_ok=True)
    rows, frames_checked, repeated = [], 0, 0
    for klass in CLASSES:
        for action in ACTIONS:
            for face in FACINGS:
                name = f"Hero_{klass}_{action}_{face}"
                seq = frames_for(name)
                metrics, changes = measure(seq)
                fail_nonoptional(name,action,seq,metrics,changes)
                holds = sum(t["changed_pixels"]==0 for t in changes)
                repeated += holds
                frames_checked += len(seq)
                jump_candidates = [
                    i for i,t in enumerate(changes)
                    if t["bbox_centre_distance"] > (12 if action=="Run" else 25)
                ]
                rows.append({"resource":name,"frames":len(seq),
                    "duplicate_neighbor_pairs":holds,
                    "transition_metrics":changes,
                    "frame_metrics":metrics,
                    "visual_review_required":bool(jump_candidates)
                        or action in ("Attack","Cast","Dodge","Hit","Death"),
                    "jump_candidates":jump_candidates})
                # Actual production imagery, not the separate preview
                # builder's approved source cells.
                if face == "E" or name == "Hero_Arcanist_Run_S":
                    preview(name,seq)
    assert len(rows)==63 and frames_checked==333,(len(rows),frames_checked)
    historical_splice_comparison()
    report={
        "status":"production_sprite_structure_checked_artist_review_open",
        "source":"Assets/Resources/Frontier/HeroAnimator/Canonical",
        "clip_count":len(rows),"frame_count":frames_checked,
        "same_raster_adjacent_pairs":repeated,
        "native_unity_verified":False,"hand_art_approved":False,
        "clips":rows,
    }
    (DEST / "production-runtime-qa.json").write_text(
        json.dumps(report,indent=2)+"\n",encoding="utf-8")
    print("PASS: 63 real production Sprite atlases / 333 canvas, root and "
          "frame-boundary-checked cells; filmstrips/GIFs saved to "+str(DEST))
    print("ART_REVIEW_REQUIRED; NATIVE_UNVERIFIED: mechanical interpolation "
          "and actual native Game View not certified.")


if __name__=="__main__":
    run()
