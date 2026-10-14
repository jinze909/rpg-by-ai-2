#!/usr/bin/env python3
"""Checks OUTPUT PIXELS from the motion-assisted live-production sprite export.

Does not pretend to certify motion anatomy, quality, Unity import or PlayMode.
"""
import json
from pathlib import Path
from PIL import Image
from build_hero_animation_preview import CLASSES, FACINGS, ACTIONS, CELL, ROOT, source_indices
from export_canonical_hero_atlases import DEST, PREVIEW, expected_count

def check():
    manifest = json.loads((DEST / "manifest.json").read_text(encoding="utf-8"))
    assert manifest["status"] == "accepted_source_plus_algorithmic_motion_midpoints_not_art_approved"
    assert manifest["top_origin_foot_px"] == [128, 240]
    assert manifest["unity_bottom_origin_pivot"] == [0.5, 0.0625]
    assert not manifest["native_unity_verified"] and not manifest["art_direction_acceptance"]
    tally = midpoints = 0
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
        assert source_indices(profession, "Cast", "E") == [(player, 6), (player, 7)]
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
                for i in range(expected):
                    cell = sheet.crop((i * CELL, 0, (i + 1) * CELL, CELL))
                    assert cell.getchannel("A").getbbox() is not None, (key, i)
                for original_index in range(len(originals)):
                    index = original_index * 2 if action in ("Run", "Attack", "Cast") and len(originals) > 1 else original_index
                    extracted = sheet.crop((index * CELL, 0, (index + 1) * CELL, CELL))
                    with Image.open(PREVIEW / klass / action / f"{facing}_{original_index:02}.png") as source:
                        assert extracted.tobytes() == source.convert("RGBA").tobytes(), (key, original_index)
                tally += 1
                midpoints += row["generated_midpoints"]
                if action == "Attack" and klass == "Vanguard":
                    examples.append(key)
    assert tally == 63 and midpoints > 60, (tally, midpoints)
    report = {
        "status": "offline_raster_invariants_only",
        "clip_count": tally,
        "generated_midpoints": midpoints,
        "approved_source_frames_unchanged": True,
        "native_playmode_verified": False,
        "artist_review_pending": True,
        "sample_atlases": examples,
    }
    (DEST / "generated-motion-qa.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print(f"PASS: {tally} generated atlases / {midpoints} newly synthesized opaque pixel midpoints.")
    print("PASS: original hero poses unchanged in all 63 generated atlases; same canonical root.")
    print("NATIVE_UNVERIFIED: native Unity playback and aesthetic animation quality.")

if __name__ == "__main__":
    check()
