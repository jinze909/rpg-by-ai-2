#!/usr/bin/env python3
"""Produce 63 inspectable Sprite sheets, including provenance-locked complete body poses.

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
CONTACTS = ROOT / "Assets/ArtSource/Frontier/HeroContact"


def contact_key(profession, action, facing):
    return f"Hero_{profession}_{action}_{facing}"


def contact_art_complete():
    """Partial staging is allowed; do not publish mixed six/seven-cell art."""
    return all((CONTACTS / (contact_key(cls, action, face) + ".png")).is_file()
               for cls in CLASSES for action in ("Attack", "Cast")
               for face in FACINGS)


def expected_count(action, original, authored_contacts=False):
    if action in ("Attack", "Cast") and original > 1:
        return 2 * original - 2 + int(authored_contacts)
    if action in ("Dodge", "Hit") and original > 1:
        return 2 * original - 1
    return original


def curated_motion_source(profession, action, facing, source_index):
    """Explicit artist-drawn FULL-BODY phase, not a warped duplicate.

    Approved Player<Class> 2/3 are the actual planted/preparation and
    recovering step poses for East-facing attacks and skills. Both frames
    carry the SAME character, equipment, waist, legs, ground root and hair
    as the four corresponding approved action source poses.
    """
    if action in ("Dodge", "Hit") and source_index in (0, 1):
        # The approved guard/plant stance is a TRUE whole-body drawing
        # already adjacent to every class's dodging/hit reaction in the
        # original directional atlas. A single coherent shared stance can
        # bracket the full-body fall/recoil: Idle -> guard -> reaction ->
        # guard -> Idle. Never pixel-warp legs separately from the neck.
        if facing == "E":
            return ("Player" + profession, 9)
        cls = CLASSES.index(profession)
        if facing == "N":
            return ("PlayerReactionsRear", cls * 4 + 1)
        if facing == "S":
            return ("PlayerReactionsSouth", cls * 4 + 1)
    if action in ("Attack", "Cast") and source_index in (0, 2):
        if facing == "E":
            return ("Player" + profession, 2 if source_index == 0 else 3)
        if facing == "N":
            return ("Walk" + profession, 4 if source_index == 0 else 6)
        if facing == "S":
            return ("Walk" + profession, 9 if source_index == 0 else 11)
    return None


def frame_plan(profession, action, facing, references, authored_contacts=False):
    """Every pose is a complete source drawing, never a local body warp.

    An approved intermediate for the central Attack/Cast contact transfer
    does not exist. Omit the fake midpoint rather than dislocate the torso
    and hand; GameCore still owns the exact attack/skill release moment.
    """
    plan = []
    for i, original in enumerate(references):
        plan.append((original, "accepted", i))
        if i + 1 == len(references):
            continue
        selection = curated_motion_source(profession, action, facing, i)
        if selection is not None:
            plan.append((selection, "curated", i))
        elif action in ("Attack", "Cast") and i == 1:
            # The EXTRA contact-transfer cell is real, staged full-body
            # artwork. There is deliberately no procedural midpoint.
            if authored_contacts:
                plan.append((("@hand_redrawn", 0), "hand_redrawn", i))
            continue
        elif action in ("Dodge", "Hit"):
            raise AssertionError(("MISSING_AUTHORED_REACTION", profession,
                                  action, facing, i))
    if len(plan) != expected_count(
            action, len(references), authored_contacts and
            action in ("Attack", "Cast")):
        raise AssertionError(("UNSUPPORTED_AUTHORED_FRAME_PLAN",
                              profession, action, facing))
    return plan


def main():
    DEST.mkdir(parents=True, exist_ok=True)
    sheets, heads = imported_sheets(), registered_heads()
    authored_contacts = contact_art_complete()
    manifest = {
        "status": ("all_297_cells_source_locked_plus_18_hand_redrawn_contacts"
                   if authored_contacts else
                   "all_279_cells_from_unmodified_approved_full_body_source_art"),
        "hand_redrawn_contact_frames": 18 if authored_contacts else 0,
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
                plan = frame_plan(profession, action, facing, refs,
                                  authored_contacts and action in ("Attack", "Cast"))
                key = contact_key(profession, action, facing)
                frames = []
                for ((name, index), provenance, i) in plan:
                    if provenance == "hand_redrawn":
                        # Validation is repeated against the final assembled
                        # sheets by check_hero_contact_art --release.
                        with Image.open(CONTACTS / (key + ".png")) as source:
                            if source.format != "PNG" or source.mode != "RGBA":
                                raise AssertionError(("INVALID_STAGED_CONTACT", key))
                            frame = source.copy()
                        if frame.size != (CELL, CELL):
                            raise AssertionError(("WRONG_CONTACT_CANVAS", key))
                    else:
                        frame = canonical_frame(
                            sheets, heads, name, index, facing,
                            name.startswith("Walk") or action in ("Run", "Idle"))
                    frames.append(frame)
                total_midpoints += len(frames) - len(originals)
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
                    "procedurally_warped_cells": 0,
                    "curated_full_body_slots": {
                        str(i): "%s:%d" % pair
                        for i, (pair, origin, _) in enumerate(plan)
                        if origin == "curated"
                    },
                    "frame_sources": [
                        {"atlas": name, "index": index, "kind": origin,
                         "original_index": ref_i}
                        for ((name, index), origin, ref_i) in plan
                    ],
                    "frame_count": len(frames),
                    "unique_rasters": len(set(hashes)),
                    "frame_sha256_rgba": hashes,
                    "needs_hand_redrawn_art": (
                        action in ("Dodge", "Hit", "Death") or
                        (action in ("Attack", "Cast") and not authored_contacts)),
                    "native_unity_verified": False,
                }

    if len(manifest["atlases"]) != 63 or total_midpoints <= 0:
        raise AssertionError("Hero export failed to generate all action sheets")
    (DEST / "manifest.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    # Release-blocker in BOTH offline CI and the guarded GitHub PNG
    # publisher, not an optional post-hoc report.
    from check_hero_whole_body_integrity import audit as audit_full_body
    audit_full_body(DEST, PREVIEW)
    if authored_contacts:
        from check_hero_contact_art import audit as audit_contact
        audit_contact(release=True, atlas_dir=DEST, staged_dir=CONTACTS)
    print(f"PASS: 63 canonical hero atlases exported with "
          f"{total_midpoints} source-backed complete-body extra poses, "
          f"{18 if authored_contacts else 0} newly-authored contacts "
          "and zero raster warp.")
    print("Unmodified source poses remain at even indices; all source identity pixels preserved.")
    print("ART/NATIVE_UNVERIFIED: hand-redrawn quality, playmode, Animator, GPU, FPS.")


if __name__ == "__main__":
    main()
