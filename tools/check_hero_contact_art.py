#!/usr/bin/env python3
"""Block fake hero contact art and distinguish source QA from art release.

Existing six-cell Attack/Cast clips remain playable while 18 *new* hand-drawn
whole-body contact transfers are missing. CI must not call them completed.
Source candidates are staged individually, but release requires the complete
three-profession/two-action/three-facing set and the actually installed seven
cell atlases. No old raster, speed trick or duplicate is acceptable.
"""
import argparse
import hashlib
import json
from pathlib import Path

from PIL import Image, ImageChops

ROOT = Path(__file__).resolve().parents[1]
STAGED = ROOT / "Assets/ArtSource/Frontier/HeroContact"
ATLASES = ROOT / ".starfall-checks/hero-production-atlases"
CLASSES = ("Vanguard", "Ranger", "Arcanist")
ACTIONS = ("Attack", "Cast")
FACINGS = ("E", "N", "S")
CELL = 256


def raster_hash(image):
    return hashlib.sha256(image.tobytes()).hexdigest()


def validate_contact(image, key, forbidden, original_poses=()):
    """Validate structure and novelty, not subjective animation smoothness."""
    assert image.size == (CELL, CELL) and image.mode == "RGBA", (
        "INVALID_CONTACT_CANVAS_OR_MODE", key, image.size, image.mode)
    alpha = image.getchannel("A")
    bbox = alpha.getbbox()
    assert bbox and bbox[1] < 205 and bbox[3] >= 233, (
        "CONTACT_BODY_OR_FOOT_MISSING", key, bbox)
    # Ground attachment must not disappear on the keyframe.
    ground = alpha.crop((88, 225, 176, 249))
    assert ground.getbbox(), ("CONTACT_LOST_CANONICAL_FOOT", key)
    hair = image.crop((75, 100, 185, 212))
    blonde = sum(
        a >= 70 and r >= 150 and g >= 95 and r > g * 1.07 and g > b * 1.04
        for r, g, b, a in hair.getdata()
    )
    assert blonde >= 25, ("CONTACT_LOST_BLONDE_IDENTITY", key, blonde)
    digest = raster_hash(image)
    assert digest not in forbidden, (
        "REUSED_OLD_POSE_AS_NEW_CONTACT", key, forbidden.get(digest))
    # Hash equality alone is NOT a meaningful art gate: recoloring a
    # single pixel or globally tinting an old pose kept exactly the same
    # silhouette and previously passed as a "newly painted" contact.
    # Both full-body silhouette and meaningful pixels must change relative
    # to every shipped pose. This is a minimum novelty invariant, NOT
    # subjective approval of anatomy, hand grip, or animation smoothness.
    candidate_alpha = alpha
    candidate_rgb = image.convert("RGB")
    for original_key, original in original_poses:
        old_alpha = original.getchannel("A")
        different_shape = sum(
            ImageChops.difference(candidate_alpha, old_alpha).histogram()[1:])
        assert different_shape >= 36, (
            "CONTACT_REUSED_OLD_SILHOUETTE", key, original_key,
            different_shape)
        different_color = sum(
            ImageChops.difference(candidate_rgb, original.convert("RGB"))
            .convert("L").histogram()[1:])
        assert different_color >= 120, (
            "CONTACT_ALMOST_IDENTICAL_TO_OLD_POSE", key,
            original_key, different_color)
    return digest


def _check_existing_row(row, key):
    """Legacy approved six-cell layout, or completed seven-cell art layout."""
    sources = row["frame_sources"]
    count = row["frame_count"]
    assert len(sources) == count, ("CONTACT_PROVENANCE_LENGTH", key)
    if count == 6:
        assert [i for i, s in enumerate(sources) if s["kind"] == "accepted"] == [
            0, 2, 3, 5], ("CONTACT_ORIGINAL_POSES_CHANGED", key)
        assert [i for i, s in enumerate(sources) if s["kind"] == "curated"] == [
            1, 4], ("CONTACT_GUARD_AND_RECOVERY_CHANGED", key)
        return False
    if count == 7:
        assert [i for i, s in enumerate(sources) if s["kind"] == "accepted"] == [
            0, 2, 4, 6], ("CONTACT_ORIGINAL_POSES_CHANGED", key)
        assert [i for i, s in enumerate(sources) if s["kind"] == "curated"] == [
            1, 5], ("CONTACT_GUARD_AND_RECOVERY_CHANGED", key)
        assert sources[3]["kind"] == "hand_redrawn", (
            "CONTACT_NOT_GENUINE_NEW_ART", key)
        return True
    raise AssertionError(("INVALID_ATTACK_CAST_CELL_COUNT", key, count))


def audit(release=False, atlas_dir=ATLASES, staged_dir=STAGED):
    manifest = json.loads((atlas_dir / "manifest.json").read_text(
        encoding="utf-8"))
    assert not manifest["native_unity_verified"], (
        "FALSE_NATIVE_UNITY_VERIFICATION")
    assert not manifest["art_direction_acceptance"], (
        "FALSE_ART_DIRECTION_ACCEPTANCE")
    forbidden = {}
    original_poses = []
    keys = [f"Hero_{cls}_{action}_{facing}"
            for cls in CLASSES for action in ACTIONS for facing in FACINGS]
    completed = {}
    # Compare candidates against EVERY published cell from all three
    # professions and all actions, not merely adjacent contact cells.
    for row in manifest["atlases"].values():
        with Image.open(atlas_dir / row["path"]) as f:
            atlas = f.convert("RGBA")
        assert atlas.height == CELL and atlas.width == CELL * row["frame_count"]
        for i in range(row["frame_count"]):
            # A successfully installed original contact necessarily matches
            # its staged master. Exclude ONLY that slot from old-art hashes;
            # duplicate masters across clips are checked separately.
            if row["frame_count"] == 7 and i == 3 and (
                    "_Attack_" in row["path"] or "_Cast_" in row["path"]):
                continue
            cell = atlas.crop((i * CELL, 0, (i + 1) * CELL, CELL))
            label = f'{row["path"]}:{i}'
            forbidden.setdefault(raster_hash(cell), label)
            original_poses.append((label, cell))
    for key in keys:
        completed[key] = _check_existing_row(manifest["atlases"][key], key)
    staged, missing, seen = [], [], {}
    for key in keys:
        path = staged_dir / (key + ".png")
        if not path.is_file():
            missing.append(key)
            continue
        with Image.open(path) as file:
            assert file.format == "PNG", ("CONTACT_MUST_BE_PNG", key)
            candidate = file.copy()
        digest = validate_contact(candidate, key, forbidden, original_poses)
        assert digest not in seen, (
            "DUPLICATED_CROSS_CLASS_OR_ACTION_CONTACT", key, seen.get(digest))
        seen[digest] = key
        staged.append(key)
        if completed[key]:
            with Image.open(atlas_dir / manifest["atlases"][key]["path"]) as f:
                atlas = f.convert("RGBA")
            installed = atlas.crop((3 * CELL, 0, 4 * CELL, CELL))
            assert raster_hash(installed) == digest, (
                "STAGED_CONTACT_NOT_INSTALLED", key)
    # No false release based on simply uploading staged source PNGs:
    # all eighteen must actually occur in the production atlas manifest.
    integrated = [key for key in keys if completed[key]]
    if release:
        assert not missing, ("MISSING_18_CONTACT_DRAWINGS", missing)
        assert len(integrated) == len(keys), (
            "CONTACT_DRAWINGS_NOT_IN_PRODUCTION_ATLASES",
            [key for key in keys if key not in integrated])
    status = ("READY_FOR_NATIVE_ART_REVIEW" if not missing and
              len(integrated) == len(keys) else
              "BLOCKED_INCOMPLETE_HAND_DRAWN_CONTACTS")
    report = {
        "status": status,
        "required_unique_drawings": len(keys),
        "valid_staged_drawings": len(staged),
        "integrated_production_contacts": len(integrated),
        "missing_drawings": missing,
        "not_integrated": [key for key in keys if not completed[key]],
        "native_unity_verified": False,
        "art_direction_approved": False,
        "gate_mode": "release" if release else "staging",
    }
    atlas_dir.mkdir(parents=True, exist_ok=True)
    (atlas_dir / "contact-art-qa.json").write_text(
        json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print(f'{status}: {len(staged)}/18 distinct staged drawings; '
          f'{len(integrated)}/18 installed contacts. Native/art UNVERIFIED.')
    return report


def negative_controls():
    # A transparent or duplicated cell must raise, not merely warn.
    blank = Image.new("RGBA", (CELL, CELL))
    try:
        validate_contact(blank, "negative_blank", {})
    except AssertionError:
        pass
    else:
        raise AssertionError("UNSAFE_GATE: empty contact accepted")
    canvas = Image.new("RGBA", (CELL, CELL))
    for y in range(150, 241):
        for x in range(110, 147):
            canvas.putpixel((x, y), (226, 155, 71, 255))
    old = raster_hash(canvas)
    try:
        validate_contact(canvas, "negative_reused_pose", {old: "old_raster"})
    except AssertionError as error:
        assert "REUSED_OLD_POSE_AS_NEW_CONTACT" in str(error)
    else:
        raise AssertionError("UNSAFE_GATE: duplicate contact accepted")
    # A trivially recolored old complete-body pose must not pass as newly
    # painted action contact despite different SHA256 and valid hair/boots.
    cloned = canvas.copy()
    cloned.putpixel((125, 222), (224, 153, 71, 255))
    for description, candidate in (("tiny_color_edit", cloned),
                                   ("old_silhouette_new_palette",
                                    Image.new("RGBA", (CELL, CELL),
                                              (0, 0, 0, 0)))):
        if description == "old_silhouette_new_palette":
            candidate = canvas.copy()
            # Different all pixels' colors but exactly the SAME body shape.
            px = candidate.load()
            for y in range(150, 241):
                for x in range(110, 147):
                    px[x, y] = (239, 157, 74, 255)
        try:
            validate_contact(candidate, "negative_"+description, {},
                             [("old_pose", canvas)])
        except AssertionError as error:
            assert "CONTACT_REUSED_OLD_SILHOUETTE" in str(error)
        else:
            raise AssertionError("UNSAFE_GATE: fake novel contact accepted")


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--release", action="store_true",
                        help="Fail until all 18 new drawings are integrated.")
    args = parser.parse_args()
    negative_controls()
    audit(release=args.release)
