#!/usr/bin/env python3
"""Release-blocking hero full-body provenance, waist seam and anatomy checks.

This is source/pixel integrity QA, NOT proof of hand-authored animation
quality or native Unity PlayMode. Rejects a foreign-class body or a hard
upper/lower crop before the generated PNGs can be published.
"""
import json
from pathlib import Path
from PIL import Image
from build_hero_animation_preview import (
    ACTIONS, CELL, CLASSES, FACINGS, ROOT, source_indices,
)

OUT = ROOT / ".starfall-checks/hero-production-atlases"
PREVIEW = ROOT / ".starfall-checks/hero-animation-qa"
SHARED = {"PlayerRear", "PlayerSouth",
          "PlayerReactionsRear", "PlayerReactionsSouth"}
ANIMATED = {"Run", "Attack", "Cast", "Dodge", "Hit"}


def assert_class_source(profession, name, frame):
    klass = CLASSES.index(profession)
    if name in SHARED:
        assert frame // 4 == klass, (
            "CROSS_CLASS_SHARED_ROW", profession, name, frame)
    else:
        assert name in {"Walk" + profession, "Player" + profession}, (
            "CROSS_CLASS_BODY_SOURCE", profession, name, frame)


def _body_features(sprite):
    """Measure connected waist/hips from the same full-body silhouette."""
    rgba = sprite.load()
    def samples(y_start, y_end):
        values = []
        for y in range(y_start, y_end):
            for x in range(89, 173, 2):
                if rgba[x, y][3] >= 36:
                    values.append((x, y))
        return values
    top, hip = samples(195, 215), samples(218, 237)
    def centre(points):
        return sum(x for x, _ in points) / len(points) if points else None
    def gap(left, right):
        if not left or not right:
            return None
        # Nearest two sections of the central garment; excludes outside weapon,
        # cape and extreme arm geometry from the anatomical estimate.
        lx = {x for x, _ in left}
        rx = {x for x, _ in right}
        return min(abs(x-y) for x in lx for y in rx)
    return centre(top), centre(hip), gap(top, hip), len(top), len(hip)


def _verify_waist_and_ratio(image, reference, key, index):
    a = _body_features(image)
    b = _body_features(reference)
    # Very low/prone silhouettes may not traverse both upright scan bands.
    # Detect NEW holes where an approved source was continuous, rather than
    # falsely rejecting established death/dodge/recoil reference poses.
    if b[3] >= 12 and b[4] >= 12:
        assert a[3] >= 8 and a[4] >= 8, (
            "WAIST_DISCONNECT", key, index, a, b)
        assert a[2] is not None and a[2] <= max(9, b[2] + 4), (
            "WAIST_SEAM_GAP", key, index, a[2], b[2])
        # Relative torso-to-hip registration, not an arbitrary fixed x=128
        # torso shared across different outfits. Allow 8px total motion.
        relative = abs((a[0]-a[1])-(b[0]-b[1]))
        assert relative <= 8.0, ("TORSO_HIP_RATIO_DRIFT", key, index, relative)
    # Reject an accidental scaled half-body crop, but permit authored
    # extremes such as a low horizontal Dodge/Hit or prone Death.
    bounds, baseline = image.getchannel("A").getbbox(), reference.getchannel("A").getbbox()
    assert bounds and baseline, ("EMPTY_BODY", key, index)
    assert abs((bounds[3]-bounds[1])-(baseline[3]-baseline[1])) <= 13, (
        "BODY_HEIGHT_RATIO", key, index, bounds, baseline)


def audit(atlas_dir=OUT, preview_dir=PREVIEW):
    manifest = json.loads((atlas_dir / "manifest.json").read_text(encoding="utf-8"))
    audit_rows = []
    checked_frames = 0
    for profession in CLASSES:
        for action in ACTIONS:
            for facing in FACINGS:
                key = f"Hero_{profession}_{action}_{facing}"
                row = manifest["atlases"][key]
                references = source_indices(profession, action, facing)
                for source_name, source_index in references:
                    assert_class_source(profession, source_name, source_index)
                baselines = []
                for i in range(len(references)):
                    with Image.open(preview_dir / profession / action / f"{facing}_{i:02}.png") as f:
                        baselines.append(f.convert("RGBA").copy())
                expanded = action in ANIMATED
                with Image.open(atlas_dir / row["path"]) as f:
                    sheet = f.convert("RGBA").copy()
                for index in range(row["frame_count"]):
                    cropped = sheet.crop((index * CELL, 0, (index+1) * CELL, CELL))
                    base_i = index // 2 if expanded else index
                    baseline = baselines[base_i]
                    # This complete approved body is the single source of the
                    # candidate midpoint. The one deliberately shared golden
                    # head was composited BEFORE this contract.
                    if expanded and index % 2 == 1:
                        candidate_palette = set(cropped.getdata())
                        authorized = set(baseline.getdata()) | {(0, 0, 0, 0)}
                        assert candidate_palette <= authorized, (
                            "CROSS_CLASS_PALETTE", key, index,
                            len(candidate_palette - authorized))
                    else:
                        assert cropped.tobytes() == baseline.tobytes(), (
                            "ACCEPTED_FULL_BODY_CHANGED", key, index)
                    _verify_waist_and_ratio(cropped, baseline, key, index)
                    checked_frames += 1
                audit_rows.append({"key": key, "frames": row["frame_count"],
                                   "source_classes": [n for n,_ in references]})
    assert len(audit_rows) == 63 and checked_frames >= 333, (
        "INCOMPLETE_CLASS_AUDIT",len(audit_rows),checked_frames)
    report = {
        "status":"source_owner_palette_waist_and_ratio_verified",
        "hero_atlases":len(audit_rows),
        "checked_frames":checked_frames,
        "head_source_exception":"Approved canonical blonde head mask only",
        "native_unity_verified":False,
        "artist_approved":False,
        "source_audit":audit_rows,
    }
    (atlas_dir / "full-body-provenance-qa.json").write_text(
        json.dumps(report,indent=2)+"\n",encoding="utf-8")
    print("PASS: source-locked full-body hero QA — 63 class-owned atlases / "+
          str(checked_frames)+" waist/palette/ratio-checked frames.")
    return report


if __name__ == "__main__":
    audit()
