#!/usr/bin/env python3
"""Hard-failing anatomy + Unity importer audit on ACTUALLY INSTALLED hero PNGs.

Complements source-hash equality, not an aesthetic-motion certificate.
Tests the connected upper/core/pelvis/boot silhouette, stable blond identity,
the 256px shared pivot contract, and published texture/import metadata.
Negative controls mutate production pixels and must be rejected.
"""
import hashlib
import json
import re
from collections import deque
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
ART = ROOT / "Assets/Resources/Frontier/HeroAnimator/Canonical"
REPORT = ROOT / ".starfall-checks/hero-runtime-qa"
CLASSES = ("Vanguard", "Ranger", "Arcanist")
ACTIONS = ("Idle", "Run", "Attack", "Cast", "Dodge", "Hit", "Death")
FACINGS = ("E", "N", "S")
CELL = 256
BODY_RECT = (76, 145, 185, 249)


def hero_components(frame):
    """8-connected *complete body*, without using independent limb layers."""
    alpha = frame.getchannel("A").load()
    x0, y0, x1, y1 = BODY_RECT
    occupied = {(x, y) for y in range(y0, y1) for x in range(x0, x1)
                if alpha[x, y] >= 45}
    components = []
    while occupied:
        seed = occupied.pop()
        queue = deque([seed])
        count, top, bottom = 0, seed[1], seed[1]
        while queue:
            x, y = queue.pop()
            count += 1
            top, bottom = min(top, y), max(bottom, y)
            for dy in (-1, 0, 1):
                for dx in (-1, 0, 1):
                    if dx == dy == 0:
                        continue
                    q = (x + dx, y + dy)
                    if q in occupied:
                        occupied.remove(q)
                        queue.append(q)
        components.append((count, top, bottom))
    return sorted(components, reverse=True)


def verify_frame(frame, key, frame_index, action):
    assert frame.mode == "RGBA" and frame.size == (CELL, CELL), (
        "NON_CANONICAL_RENDER_FRAME", key, frame_index)
    alpha = frame.getchannel("A")
    bounds = alpha.getbbox()
    assert bounds and 35 <= bounds[0] and bounds[2] <= CELL, (
        "OUT_OF_BOUNDS_HERO", key, frame_index, bounds)
    # The torso and hips must be ONE connected raster, not a disjoint upper
    # texture pasted onto moving legs. Transparent dust/weapon may be separate.
    components = hero_components(frame)
    assert components and components[0][0] >= 165, (
        "BODY_CORE_FRAGMENTED", key, frame_index, components[:4])
    size, top, bottom = components[0]
    assert bottom >= 228, ("BODY_WITHOUT_CANONICAL_BOOTS", key, frame_index,
                           components[:3])
    if action in ("Idle", "Run", "Attack", "Cast"):
        assert top <= 209 and bottom >= 232, (
            "TORSO_PELVIS_BOOT_CONNECTION_BROKEN", key, frame_index,
            components[:4])
    # Main avatar blond head remains in the same source-space band for each
    # direction and outfit. Weapon/armor copper cannot replace gold hair.
    upper = frame.crop((75, 134, 185, 209))
    blonde = sum(
        a >= 70 and r >= 150 and g >= 95 and r > g * 1.07 and g > b * 1.04
        for r, g, b, a in upper.getdata())
    if action in ("Idle", "Run", "Attack", "Cast"):
        assert blonde >= 25, (
            "CANONICAL_BLOND_HEAD_MISSING", key, frame_index, blonde)
    return {"largest_connected_body_pixels": size, "body_top": top,
            "body_bottom": bottom, "blond_pixels": blonde,
            "frame_index": frame_index}


def verify_meta(path, key, seen):
    meta = path.with_suffix(".png.meta")
    assert meta.is_file(), ("MISSING_UNITY_META", key)
    content = meta.read_text(encoding="utf-8")
    match = re.search(r"(?m)^guid: ([a-f0-9]{32})$", content)
    assert match, ("INVALID_SPRITE_GUID", key)
    assert match.group(1) not in seen, (
        "DUPLICATE_UNITY_SPRITE_GUID", key, seen.get(match.group(1)))
    seen[match.group(1)] = key
    for entry in ("enableMipMap: 0", "isReadable: 0",
                  "filterMode: 0", "alphaIsTransparency: 1",
                  "textureCompression: 0"):
        assert entry in content, ("UNSAFE_SPRITE_IMPORT_SETTINGS", key, entry)


def expect_failure(frame, name, action):
    try:
        verify_frame(frame, "negative_" + name, 0, action)
    except AssertionError:
        return
    raise AssertionError("UNSAFE_ANATOMY_GATE_ACCEPTED_" + name.upper())


def negative_controls():
    with Image.open(ART / "Hero_Vanguard_Idle_E.png") as inp:
        original = inp.convert("RGBA").crop((0, 0, CELL, CELL))
    verify_frame(original, "negative_reference", 0, "Idle")
    # Break neck/torso from pelvis and lower-body along the old 205-247
    # horizontal splice family. Mutate the SAME shipped image being audited.
    severed = original.copy()
    pixels = severed.load()
    for y in range(211, 230):
        for x in range(76, 185):
            pixels[x, y] = (0, 0, 0, 0)
    expect_failure(severed, "upper_lower_body_separation", "Idle")
    headless = original.copy()
    pixels = headless.load()
    for y in range(134, 209):
        for x in range(75, 185):
            pixels[x, y] = (0, 0, 0, 0)
    expect_failure(headless, "lost_blond_head", "Idle")
    bootless = original.copy()
    pixels = bootless.load()
    for y in range(226, CELL):
        for x in range(76, 185):
            pixels[x, y] = (0, 0, 0, 0)
    expect_failure(bootless, "floating_missing_boots", "Idle")


def check():
    negative_controls()
    rows, guids, inspected = [], {}, 0
    for klass in CLASSES:
        for action in ACTIONS:
            for face in FACINGS:
                key = f"Hero_{klass}_{action}_{face}"
                path = ART / (key + ".png")
                assert path.is_file(), ("MISSING_PRODUCTION_HERO_SHEET", key)
                verify_meta(path, key, guids)
                with Image.open(path) as source:
                    assert source.format == "PNG" and source.mode == "RGBA", (
                        "NON_RGBA_PRODUCTION_PNG", key)
                    atlas = source.copy()
                assert atlas.height == CELL and atlas.width % CELL == 0, (
                    "INVALID_PRODUCTION_SHEET_GEOMETRY", key, atlas.size)
                n = atlas.width // CELL
                expected = {"Idle": (1,), "Run": (4,),
                            "Attack": (6, 7), "Cast": (6, 7),
                            "Dodge": (5,), "Hit": (5,),
                            "Death": (4,)}[action]
                assert n in expected, ("WRONG_PRODUCTION_FRAME_COUNT", key, n)
                evidence = []
                for i in range(n):
                    frame = atlas.crop((i*CELL, 0, (i+1)*CELL, CELL))
                    evidence.append(verify_frame(frame, key, i, action))
                    inspected += 1
                rows.append({"sheet": key, "frame_count": n,
                             "anatomy": evidence})
    assert len(rows) == 63, ("INCOMPLETE_PROFESSION_ACTION_DIRECTION_MATRIX",
                            len(rows))
    contacts = [r for r in rows if "_Attack_" in r["sheet"] or
                "_Cast_" in r["sheet"]]
    seven = sum(r["frame_count"] == 7 for r in contacts)
    assert seven in (0, 18), ("PARTIAL_NEW_CONTACT_ART_RELEASE", seven)
    spec = json.loads((ROOT / "docs/HERO-VISUAL-SPEC.json").read_text())
    assert spec["pivot_normalized"] == [0.5, 0.0625]
    assert spec["foot_top_origin_px"] == [128, 240]
    REPORT.mkdir(parents=True, exist_ok=True)
    summary = {
        "status": "HARD_GATED_ACTUAL_PRODUCTION_RASTER_ANATOMY",
        "sheet_count": len(rows), "frame_count": inspected,
        "distinct_import_guids": len(guids),
        "seven_cell_contact_sheets": seven,
        "native_unity_verified": False, "artist_motion_approved": False,
        "negative_controls": ["severed_upper_lower", "missing_blond_head",
                              "missing_boots"],
        "sheets": rows,
    }
    (REPORT / "production-anatomy-qa.json").write_text(
        json.dumps(summary, indent=2) + "\n", encoding="utf-8")
    print(f"PASS: {len(rows)} published sheets, {inspected} actual "
          "complete-body raster checks, importer GUIDs, 3 mutation rejections.")
    print("NATIVE_UNVERIFIED: this is structural QA, not smoothness acceptance.")


if __name__ == "__main__":
    check()
