#!/usr/bin/env python3
"""Measure 3-class hero preview continuity without claiming the inherited art is finished.

The preview builder deliberately reuses accepted legacy action cells. This audit
provides transparent, reviewable animation shortcomings, not fabricated passes.
"""
import argparse
import json
import math
from pathlib import Path
from PIL import Image, ImageChops

ROOT = Path(__file__).resolve().parents[1]
DEFAULT = ROOT / ".starfall-checks/hero-animation-qa"
CLASSES = ("Vanguard", "Ranger", "Arcanist")
ACTIONS = ("Idle", "Run", "Attack", "Cast", "Dodge", "Hit", "Death")
FACINGS = ("E", "N", "S")
CELL = (256, 256)
ANCHOR = (128, 240)


def frame_info(path):
    with Image.open(path) as loaded:
        frame = loaded.convert("RGBA")
    if frame.size != CELL:
        raise AssertionError(f"{path}: wrong 256x256 canonical canvas: {frame.size}")
    alpha = frame.getchannel("A")
    bounds = alpha.getbbox()
    if bounds is None:
        raise AssertionError(f"{path}: empty protagonist frame")
    # Pixel centroid is a diagnostic. Large movement is intentional in
    # attacks and dodges, therefore never globally reject on this metric.
    pixels = alpha.load()
    weight = xsum = ysum = 0
    for y in range(bounds[1], bounds[3]):
        for x in range(bounds[0], bounds[2]):
            if pixels[x, y]:
                w = pixels[x, y]
                weight += w
                xsum += x * w
                ysum += y * w
    center = (xsum / weight, ysum / weight)
    return frame, {"bounds": list(bounds), "centroid": [round(c, 3) for c in center],
                   "visible_pixels": sum(a > 0 for a in alpha.getdata())}


def sequence_report(paths, action):
    frames = []
    measures = []
    for path in paths:
        frame, metric = frame_info(path)
        frames.append(frame)
        measures.append(metric)
    hashes = [f.tobytes() for f in frames]
    duplicate_pairs = [[i, j] for i in range(len(frames))
                       for j in range(i + 1, len(frames)) if hashes[i] == hashes[j]]
    displacement = []
    changed_fraction = []
    for before, after, prev, curr in zip(frames, frames[1:], measures, measures[1:]):
        ca, cb = prev["centroid"], curr["centroid"]
        displacement.append(round(math.hypot(cb[0] - ca[0], cb[1] - ca[1]), 3))
        diff = ImageChops.difference(before, after)
        changed_fraction.append(round(sum(1 for rgba in diff.getdata() if any(rgba))
                                      / (CELL[0] * CELL[1]), 5))
    accel = [round(abs(displacement[i + 1] - displacement[i]), 3)
             for i in range(len(displacement) - 1)]
    flags = []
    if duplicate_pairs:
        flags.append("duplicated_raster_review_timing_holds")
    if len(frames) < (3 if action in ("Attack", "Cast", "Dodge") else 4 if action == "Run" else 1):
        flags.append("insufficient_distinct_action_poses")
    if action in ("Cast", "Dodge", "Hit", "Death"):
        flags.append("legacy_shared_reaction_requires_true_redraw")
    if displacement and max(displacement) > (18 if action in ("Attack", "Cast", "Dodge") else 11):
        flags.append("centroid_jump_inspect_weapon_and_body")
    if accel and max(accel) > (15 if action in ("Attack", "Cast", "Dodge") else 8):
        flags.append("uneven_motion_inspect_in_betweens")
    return {"frame_count": len(frames), "unique_rasters": len(frames) - len(
                {j for pair in duplicate_pairs for j in pair[1:]}),
            "frames": measures, "centroid_step_px": displacement,
            "centroid_step_change_px": accel,
            "changed_fraction": changed_fraction, "duplicate_pairs": duplicate_pairs,
            "review_flags": flags}


def check(root):
    preview = root / "report.json"
    if not preview.is_file():
        raise AssertionError(f"Missing actual 3-class preview export: {preview}")
    source = json.loads(preview.read_text(encoding="utf-8"))
    if source.get("canonical_pivot") != list(ANCHOR):
        raise AssertionError("Canonical 128,240 root contract changed")
    report = {"status": "preview_diagnostics_only", "native_unity_verified": False,
              "source_art": source.get("art_origin"), "classes": list(CLASSES),
              "actions": list(ACTIONS), "facings": list(FACINGS), "clips": {},
              "milestone_three_accepted": False}
    for klass in CLASSES:
        for action in ACTIONS:
            for facing in FACINGS:
                key = f"{klass}/{action}/{facing}"
                manifest = source["action_sets"].get(key)
                if not manifest:
                    raise AssertionError(f"Missing three-profession action entry {key}")
                folder = root / klass / action
                paths = sorted(folder.glob(f"{facing}_[0-9][0-9].png"))
                if len(paths) != manifest["frames"]:
                    raise AssertionError(f"{key}: missing preview frames")
                metrics = sequence_report(paths, action)
                if metrics["unique_rasters"] != manifest["unique_rasters"]:
                    raise AssertionError(f"{key}: changed/duplicated raster provenance")
                metrics["inherited_source_frames"] = manifest["source"]
                metrics["source_warnings"] = manifest["warnings"]
                report["clips"][key] = metrics
    if len(report["clips"]) != 63:
        raise AssertionError("Missing a class/action/direction combination")
    destination = root / "temporal-qa.json"
    destination.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n",
                           encoding="utf-8")
    warnings = sum(bool(c["review_flags"] or c["source_warnings"])
                   for c in report["clips"].values())
    print(f"PASS: measured all 63 inherited hero animation sequences; {warnings} need art review.")
    print("Milestone three is NOT accepted: no newly authored interpolation or Unity PlayMode proof.")
    return report


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=DEFAULT)
    args = parser.parse_args()
    check(args.root)
