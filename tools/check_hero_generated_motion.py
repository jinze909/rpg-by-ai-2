#!/usr/bin/env python3
"""Prove the COMPLETE 3-class Sprite production contains zero warped puppet cells.

The 279 published cells must each be a byte-for-byte copy of an APPROVED
full-body original. This does not imply visual/artist acceptance or native QA.
"""
import hashlib
import json
from PIL import Image

from build_hero_animation_preview import (
    CLASSES, FACINGS, ACTIONS, CELL, ROOT, source_indices, crop_frame,
    imported_sheets, registered_heads
)
from export_canonical_hero_atlases import (
    DEST, CONTACTS, frame_plan, expected_count
)


def source_exact(candidate, expected, key, index):
    """A hard failing gate, including head, weapon, neck, torso and boots."""
    assert candidate.size == (CELL, CELL), ("INVALID_BODY_CANVAS", key, index)
    assert candidate.tobytes() == expected.tobytes(), (
        "PROCEDURALLY_SYNTHESIZED_OR_WRONG_CLASS_FRAME", key, index
    )


def negative_control():
    """Demonstrate the same release gate rejects a severed head/hip."""
    original = Image.new("RGBA", (CELL, CELL), (0, 0, 0, 0))
    pixels = original.load()
    for y in range(155, 239):
        for x in range(112, 145):
            pixels[x,y] = (121, 94, 61, 255)
    broken = original.copy()
    for x in range(112, 145):
        broken.putpixel((x, 211), (0, 0, 0, 0))
    try:
        source_exact(broken, original, "fixture", 0)
    except AssertionError as error:
        assert "PROCEDURALLY_SYNTHESIZED_OR_WRONG_CLASS_FRAME" in str(error)
    else:
        raise AssertionError("UNSAFE_CI_GATE: wrong waist accepted")


def check():
    negative_control()
    # Exercise both six-cell legacy and true seven-cell source plan even
    # before the 18 genuinely new contact paintings are delivered.
    for profession in CLASSES:
        for action in ("Attack", "Cast"):
            for face in FACINGS:
                refs = source_indices(profession, action, face)
                legacy = frame_plan(profession, action, face, refs, False)
                expanded = frame_plan(profession, action, face, refs, True)
                assert len(legacy) == 6 and len(expanded) == 7
                assert expanded[3] == (("@hand_redrawn",0), "hand_redrawn",1)
                assert [pose for i, pose in enumerate(expanded) if i != 3] == legacy
    sheets, heads = imported_sheets(), registered_heads()
    manifest = json.loads((DEST / "manifest.json").read_text(encoding="utf-8"))
    hand_count = manifest.get("hand_redrawn_contact_frames", 0)
    assert hand_count in (0, 18), ("PARTIAL_CONTACT_SOURCE_RELEASE", hand_count)
    assert manifest["status"] == (
        "all_297_cells_source_locked_plus_18_hand_redrawn_contacts"
        if hand_count else
        "all_279_cells_from_unmodified_approved_full_body_source_art")
    assert manifest["top_origin_foot_px"] == [128, 240]
    assert manifest["unity_bottom_origin_pivot"] == [0.5, .0625]
    assert not manifest["native_unity_verified"] and not manifest["art_direction_acceptance"]
    audited = curated = cells = new_contacts = 0
    source_errors = []
    for klass in CLASSES:
        for action in ACTIONS:
            for facing in FACINGS:
                key = f"Hero_{klass}_{action}_{facing}"
                row = manifest["atlases"][key]
                refs = source_indices(klass, action, facing)
                added = bool(hand_count and action in ("Attack", "Cast"))
                plan = frame_plan(klass, action, facing, refs, added)
                count = expected_count(action, len(refs), added)
                assert len(plan) == count == row["frame_count"], (
                    "INVALID_ANIMATION_FRAME_COUNT",key)
                assert row["procedurally_warped_cells"] == 0, (
                    "ILLEGAL_RASTER_WARP", key)
                assert row["generated_midpoints"] == count - len(refs), key
                if action == "Run":
                    assert count == 4 and not row["curated_full_body_slots"], (
                        "FOUR_AUTHENTIC_RUN_CELLS_REQUIRED",key)
                elif action in ("Attack", "Cast"):
                    assert count == (7 if hand_count else 6), (
                        "ACTION_CONTACT_ART_FRAME_COUNT",key)
                elif action in ("Dodge", "Hit"):
                    assert count == 5, ("FIVE_SOURCE_CURATED_REACTION_CELLS_REQUIRED",key)
                with Image.open(DEST / row["path"]) as sheet:
                    atlas = sheet.convert("RGBA").copy()
                assert atlas.size == (CELL * count, CELL), ("INVALID_ATLAS",key)
                assert len(row["frame_sources"]) == count, ("MISSING_PROVENANCE",key)
                digest_list = []
                for i, ((name,index), kind, original_i) in enumerate(plan):
                    current = atlas.crop((i*CELL, 0, (i+1)*CELL, CELL))
                    if kind == "hand_redrawn":
                        assert name == "@hand_redrawn" and index == 0 and i == 3
                        with Image.open(CONTACTS / (key + ".png")) as file:
                            assert file.format == "PNG" and file.mode == "RGBA"
                            expected = file.copy()
                        new_contacts += 1
                    else:
                        assert name in sheets and name in heads, (
                            "INVALID_SOURCE_CLASS", key, i, name)
                        expected = crop_frame(sheets[name], index)
                    source_exact(current, expected, key, i)
                    declared = row["frame_sources"][i]
                    assert (declared["atlas"], declared["index"],
                            declared["kind"], declared["original_index"]) == (
                        name,index,kind,original_i), ("FORGED_SOURCE_ROW",key,i)
                    if kind == "curated":
                        assert row["curated_full_body_slots"][str(i)] == f"{name}:{index}"
                        curated += 1
                    digest_list.append(hashlib.sha256(current.tobytes()).hexdigest())
                    cells += 1
                assert digest_list == row["frame_sha256_rgba"], (
                    "ARTISTIC_FRAME_MANIFEST_MISMATCH",key)
                if action in ("Attack","Cast","Dodge","Hit","Run"):
                    repeats=sum(a == b for a,b in zip(digest_list,digest_list[1:]))
                    if repeats:
                        source_errors.append({"clip":key,"adjacent_identical":repeats})
                audited += 1
    assert (audited,cells,curated,new_contacts) == (
        63, 279 + hand_count, 72, hand_count), (
        "INCOMPLETE_SOURCE_BACKED_THREE_CLASS_RELEASE",
        audited,cells,curated,new_contacts)
    # Reject a frozen animation as a release failure, not a passive warning.
    assert not source_errors, ("DUPLICATED_ADJACENT_SOURCE_FRAME",source_errors)
    report={
        "status":"all_release_cells_exact_source_locked_original_or_new_contact_art",
        "atlases":audited,"whole_body_frames":cells,
        "curated_transition_frames":curated,
        "new_contact_drawings":new_contacts,
        "synthetic_raster_midpoints":0,
        "adjacent_identical_motion_frames":source_errors,
        "full_source_identity_protected":True,
        "native_playmode_verified":False,
        "art_director_approved":False,
    }
    (DEST / "generated-motion-qa.json").write_text(
        json.dumps(report,indent=2)+"\n",encoding="utf-8")
    print(f"PASS: {audited} shipped classes/actions/facings; {cells} WHOLE source-art "
          f"Sprite cells, {curated} approved-source transition poses; zero raster-warp cells.")
    print(f"CONTACT_ART: {new_contacts}/18 genuinely new source drawings installed.")
    print("ART/NATIVE_UNVERIFIED: native PlayMode, artistic motion acceptance.")


if __name__=="__main__":
    check()
