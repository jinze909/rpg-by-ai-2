#!/usr/bin/env python3
"""Install verified 63-clip hero Sprite atlas pixels inside the REAL Unity Assets tree.

Source sprites never change. Stable folder/PNG GUIDs survive all later rebuilds.
The Unity Editor baker still creates imported Sprite slices, Animator clips and
controllers; this tool does not pretend to have run Unity or hand-redrawn art.
"""
import hashlib
import json
from pathlib import Path
from shutil import copyfile

from export_canonical_hero_atlases import DEST, ROOT
from sync_unity_meta import main as ensure_meta

ASSETS = ROOT / "Assets/Resources/Frontier/HeroAnimator/Canonical"


def main():
    manifest = json.loads((DEST / "manifest.json").read_text(encoding="utf-8"))
    if manifest["status"] != "all_279_cells_from_unmodified_approved_full_body_source_art":
        raise AssertionError("Refusing unverified/replaced identity source")
    atlases = manifest["atlases"]
    if len(atlases) != 63:
        raise AssertionError("Incomplete three-class production sheet batch")
    ASSETS.mkdir(parents=True, exist_ok=True)
    changed = 0
    for key, row in sorted(atlases.items()):
        source = DEST / row["path"]
        if not source.is_file() or source.name != key + ".png":
            raise AssertionError("Missing approved export: " + key)
        target = ASSETS / source.name
        payload = source.read_bytes()
        if not payload.startswith(bytes.fromhex("89504e470d0a1a0a")):
            raise AssertionError("Production resource is not PNG: " + key)
        if len(row["frame_sha256_rgba"]) != row["frame_count"]:
            raise AssertionError("Missing exact frame checksums: " + key)
        if not target.exists() or hashlib.sha256(target.read_bytes()).digest() != hashlib.sha256(payload).digest():
            copyfile(source, target)
            changed += 1
    ensure_meta()
    for key in atlases:
        filename = ASSETS / (key + ".png")
        assert filename.is_file() and filename.with_name(filename.name + ".meta").is_file()
    print(f"PASS: {len(atlases)} actual Sprite-sheet PNGs installed into Unity Assets, "
          f"{changed} changed, with stable .meta GUIDs.")
    print("NATIVE_UNVERIFIED: Unity sprite slicing, imported controllers, runtime GPU/PlayMode and art acceptance.")


if __name__ == "__main__":
    main()
