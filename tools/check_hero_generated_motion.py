#!/usr/bin/env python3
"""Checks OUTPUT PIXELS from the motion-assisted live-production sprite export.

Does not pretend to certify motion anatomy, quality, Unity import or PlayMode.
"""
import json
from pathlib import Path
from PIL import Image
from build_hero_animation_preview import CLASSES, FACINGS, ACTIONS, CELL, ROOT, source_indices
from export_canonical_hero_atlases import DEST, PREVIEW, expected_count, motion_midpoint

def check_solid_midpoint_synthetic():
    """Prove the replacement actually moves pixels without mixing silhouettes."""
    first = Image.new("RGBA", (CELL, CELL), (0, 0, 0, 0))
    next_pose = Image.new("RGBA", (CELL, CELL), (0, 0, 0, 0))
    for y in range(20, 57):  # body moves while boots remain rooted
        for x in range(105, 148):
            first.putpixel((x, CELL - 1 - y), (18, 32, 48, 255))
            next_pose.putpixel((x + 10, CELL - 1 - y), (18, 32, 48, 255))
    for y in range(58, 113):  # canonical head stays put
        for x in range(102, 148):
            color = (240, 223, 154, 255)
            first.putpixel((x, CELL - 1 - y), color)
            next_pose.putpixel((x, CELL - 1 - y), color)
    for y in range(40, 70):  # outstretched weapon changes pose
        for x in range(185, 193):
            first.putpixel((x, CELL - 1 - y), (230, 113, 35, 255))
            next_pose.putpixel((x + 12, CELL - 1 - y - 10), (230, 113, 35, 255))
    midpoint = motion_midpoint(first, next_pose)
    assert midpoint.tobytes() != first.tobytes(), "Motion pass did not create a new pose"
    assert set(midpoint.getdata()) <= set(first.getdata()), "Blended/new colors or double silhouettes"
    assert midpoint.crop((91, 138, 170, 199)).tobytes() == first.crop(
        (91, 138, 170, 199)).tobytes(), "Blonde protagonist's head changed"
    assert midpoint.crop((0, 233, CELL, CELL)).tobytes() == first.crop(
        (0, 233, CELL, CELL)).tobytes(), "Foot contact band shifted"


def check_opposing_limb_motion():
    """Symmetric opposing movement defeated the old single-centroid warp."""
    original = Image.new("RGBA", (CELL, CELL), (0, 0, 0, 0))
    target = Image.new("RGBA", (CELL, CELL), (0, 0, 0, 0))

    def paint(img, x0, x1, y0, y1, color):
        for y in range(y0, y1):
            for x in range(x0, x1):
                img.putpixel((x, CELL - 1 - y), color)

    # Both arm gestures move in opposite directions and heights, while
    # legs counterstep; total frame centroid movement stays near zero.
    paint(original, 59, 70, 77, 105, (193, 84, 49, 255))
    paint(target, 71, 82, 82, 110, (193, 84, 49, 255))
    paint(original, 190, 201, 76, 105, (83, 161, 195, 255))
    paint(target, 178, 189, 70, 99, (83, 161, 195, 255))
    paint(original, 100, 118, 30, 47, (68, 87, 110, 255))
    paint(target, 108, 126, 33, 50, (68, 87, 110, 255))
    paint(original, 141, 159, 30, 47, (131, 105, 79, 255))
    paint(target, 133, 151, 27, 44, (131, 105, 79, 255))
    paint(original, 118, 139, 47, 65, (38, 45, 51, 255))
    paint(target, 118, 139, 47, 65, (38, 45, 51, 255))
    paint(original, 101, 155, 78, 110, (244, 223, 154, 255))
    paint(target, 101, 155, 78, 110, (244, 223, 154, 255))
    midpoint = motion_midpoint(original, target)
    # Verify actual deformations on both sides independently. Do not count
    # a static duplicate with a new filename as authored animation.
    areas = [(45, 138, 110, 202), (157, 138, 222, 202),
             (90, 202, 126, 233), (130, 202, 173, 233)]
    for area in areas:
        assert midpoint.crop(area).tobytes() != original.crop(area).tobytes(), (
            "Opposing limb did not move", area)
    assert midpoint.crop((91, 138, 170, 199)).tobytes() == original.crop(
        (91, 138, 170, 199)).tobytes(), "Identity pixels drifted"
    assert midpoint.crop((0, 233, CELL, CELL)).tobytes() == original.crop(
        (0, 233, CELL, CELL)).tobytes(), "Boot contact drifted"
    assert set(midpoint.getdata()) <= set(original.getdata()), "Palette/alpha ghosting"


def check():
    check_solid_midpoint_synthetic()
    check_opposing_limb_motion()
    manifest = json.loads((DEST / "manifest.json").read_text(encoding="utf-8"))
    assert manifest["status"] == "accepted_source_plus_algorithmic_motion_midpoints_not_art_approved"
    assert manifest["top_origin_foot_px"] == [128, 240]
    assert manifest["unity_bottom_origin_pivot"] == [0.5, 0.0625]
    assert not manifest["native_unity_verified"] and not manifest["art_direction_acceptance"]
    tally = midpoints = 0
    identical_motion_cells = 0
    distinct_motion_cells = 0
    identical_by_action = {}
    identical_holds = []
    examples = []
    # Preserve intent of the actual authored source pose slots:
    # 4/5 normal attack, 6/7 class skill, south 0/1 normal, 2/3 skill.
    for profession in CLASSES:
        player, walk = "Player" + profession, "Walk" + profession
        n = CLASSES.index(profession)
        assert source_indices(profession, "Attack", "E") == [
            (player, 0), (player, 4), (player, 5), (player, 0)]
        assert source_indices(profession, "Attack", "S") == [
            (walk, 8), ("PlayerSouth", n * 4), ("PlayerSouth", n * 4 + 1), (walk, 8)]
        assert source_indices(profession, "Attack", "N") == [
            (player, 1), ("PlayerRear", n * 4 + 2),
            ("PlayerRear", n * 4 + 3), (player, 1)]
        assert source_indices(profession, "Cast", "E") == [
            (player, 0), (player, 6), (player, 7), (player, 0)]
        assert source_indices(profession, "Cast", "N") == [
            (player, 1), ("PlayerRear", n * 4 + 2),
            ("PlayerRear", n * 4 + 3), (player, 1)]
        assert source_indices(profession, "Cast", "S") == [
            (walk, 8), ("PlayerSouth", n * 4 + 2),
            ("PlayerSouth", n * 4 + 3), (walk, 8)]
        assert source_indices(profession, "Dodge", "N") == [
            (player, 1), ("PlayerReactionsRear", n * 4), (player, 1)]
        assert source_indices(profession, "Hit", "S") == [
            (walk, 8), ("PlayerReactionsSouth", n * 4 + 2), (walk, 8)]
        assert source_indices(profession, "Death", "N") == [
            (player, 1), ("PlayerReactionsRear", n * 4 + 2),
            ("PlayerReactionsRear", n * 4), ("PlayerReactionsRear", n * 4 + 3)]
    for klass in CLASSES:
        for action in ACTIONS:
            for facing in FACINGS:
                key = f"Hero_{klass}_{action}_{facing}"
                row = manifest["atlases"][key]
                originals = source_indices(klass, action, facing)
                expected = expected_count(action, len(originals))
                assert row["original_count"] == len(originals), key
                assert row["frame_count"] == expected, key
                assert row["generated_midpoints"] == expected - len(originals), key
                with Image.open(DEST / row["path"]) as raw:
                    sheet = raw.convert("RGBA")
                assert sheet.size == (expected * CELL, CELL), key
                assert sheet.mode == "RGBA", key
                if action in ("Dodge", "Hit"):
                    assert len(originals) == 3 and expected == 5, key
                    assert row["generated_midpoints"] == 2, key
                if action == "Cast":
                    assert len(originals) == 4 and expected == 7, key
                    # The final pose EXACTLY rejoins the accepted rooted idle.
                    first = sheet.crop((0, 0, CELL, CELL))
                    last = sheet.crop(((expected - 1) * CELL, 0, expected * CELL, CELL))
                    assert first.tobytes() == last.tobytes(), (key, "cast recovery does not rejoin idle")
                for i in range(expected):
                    cell = sheet.crop((i * CELL, 0, (i + 1) * CELL, CELL))
                    assert cell.getchannel("A").getbbox() is not None, (key, i)
                for original_index in range(len(originals)):
                    index = original_index * 2 if action in ("Run", "Attack", "Cast", "Dodge", "Hit") and len(originals) > 1 else original_index
                    extracted = sheet.crop((index * CELL, 0, (index + 1) * CELL, CELL))
                    with Image.open(PREVIEW / klass / action / f"{facing}_{original_index:02}.png") as source:
                        assert extracted.tobytes() == source.convert("RGBA").tobytes(), (key, original_index)
                    if action in ("Run", "Attack", "Cast", "Dodge", "Hit") and len(originals) > 1:
                        if original_index < len(originals) - 1 or action == "Run":
                            midpoint = sheet.crop(((index + 1) * CELL, 0, (index + 2) * CELL, CELL))
                            if midpoint.tobytes() == extracted.tobytes():
                                identical_motion_cells += 1
                                identical_by_action[action] = identical_by_action.get(action, 0) + 1
                                identical_holds.append({"class":klass, "action":action,
                                    "facing":facing, "source_index":original_index})
                            else:
                                distinct_motion_cells += 1
                            # Bottom-origin y 57..117 -> Pillow top y 138..198.
                            # The FULL canonical head guard must be unchanged,
                            # including transparent bangs/head silhouette edges.
                            box = (91, 138, 170, 199)
                            assert midpoint.crop(box).tobytes() == extracted.crop(box).tobytes(), (
                                key, original_index, "canonical blonde head altered")
                            # One approved silhouette only: no checkerboard splice
                            # of two weapons/legs or synthetic blended colors.
                            assert set(midpoint.getdata()) <= (
                                set(extracted.getdata()) | {(0, 0, 0, 0)}
                            ), (key, original_index, "non-source colors / raster ghosting")
                            # Boots + contact shadow always use the original root.
                            assert midpoint.crop((0, 233, CELL, CELL)).tobytes() == extracted.crop(
                                (0, 233, CELL, CELL)).tobytes(), (
                                    key, original_index, "foot-band slide")
                tally += 1
                midpoints += row["generated_midpoints"]
                if action == "Attack" and klass == "Vanguard":
                    examples.append(key)
    assert tally == 63 and midpoints == 126, (tally, midpoints)
    assert distinct_motion_cells + identical_motion_cells == midpoints, (
        "Motion cell audit missed a generated sprite")
    # A geometry pass should actually move most limbs; report any remaining
    # held duplicates rather than marking their presence as artistic success.
    assert distinct_motion_cells >= midpoints // 2, (
        "Most in-betweens are static repeats", distinct_motion_cells, midpoints)
    report = {
        "status": "offline_raster_invariants_only",
        "clip_count": tally,
        "generated_midpoints": midpoints,
        "distinct_generated_midpoints": distinct_motion_cells,
        "identical_hold_midpoints": identical_motion_cells,
        "identical_hold_by_action": identical_by_action,
        "identical_hold_sources": identical_holds,
        "approved_source_frames_unchanged": True,
        "native_playmode_verified": False,
        "artist_review_pending": True,
        "sample_atlases": examples,
    }
    (DEST / "generated-motion-qa.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print(f"PASS: {tally} atlases; {distinct_motion_cells}/{midpoints} non-identical rooted motion poses, "
          f"{identical_motion_cells} detected holds {identical_holds}.")
    print("PASS: original hero poses unchanged in all 63 generated atlases; same canonical root.")
    print("NATIVE_UNVERIFIED: native Unity playback and aesthetic animation quality.")

if __name__ == "__main__":
    check()
