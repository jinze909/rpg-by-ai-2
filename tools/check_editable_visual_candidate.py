#!/usr/bin/env python3
"""Validate retained, inactive art candidates without freezing old raster art.

This is NOT a release approval. When a repainted atlas becomes the active
game asset, the strict pose/cell/muzzle/root audits in its dedicated checker
run again and must be updated to the new approved animation specification.
"""
from pathlib import Path
from PIL import Image

ART = Path("Assets/Resources/Frontier/Art")
ACTOR_SOURCE = Path("Assets/Scripts/Frontier/Runtime/FrontierActors.cs")
BLOB_RULES = Path(".gitattributes")

def audit_retired_candidate(root, name, guid, max_texture_size):
    art = root / ART / (name + ".png")
    assert art.is_file() and art.stat().st_size > 1024, (
        "ARCHIVED_CANDIDATE_MISSING_OR_LFS_POINTER", name)
    # Allow higher-resolution re-paints, different body proportions and
    # richer frame grids: requiring old 128px cells defeated the rollback.
    with Image.open(art) as image:
        assert image.format == "PNG", ("ARCHIVED_CANDIDATE_NOT_PNG",name)
        image.verify()
    with Image.open(art) as original:
        w, h = original.size
        assert 64 <= w <= max_texture_size and 64 <= h <= max_texture_size, (
            "ARCHIVED_CANDIDATE_EXCEEDS_UNITY_IMPORT_BOUNDS",name,(w,h))
        rgba = original.convert("RGBA")
        alpha = rgba.getchannel("A").getextrema()
        assert alpha[0] < 255 and alpha[1] > 0, (
            "ARCHIVED_CANDIDATE_NO_CHARACTER_TRANSPARENCY",name,alpha)
    metadata = (root / ART / (name + ".png.meta")).read_text()
    for item in (
        "guid: " + guid,
        "enableMipMap: 0",
        "filterMode: 0",
        "textureCompression: 0",
    ):
        assert item in metadata, ("ARCHIVED_UNITY_META_REGRESSION", name, item)
    attrs = (root / BLOB_RULES).read_text()
    explicit = "/Assets/Resources/Frontier/Art/" + name + ".png -filter -diff -merge"
    wildcard = "/Assets/Resources/Frontier/Art/*.png -filter -diff -merge"
    assert explicit in attrs or wildcard in attrs, (
        "ARCHIVED_CANDIDATE_MUST_HAVE_REAL_GIT_PNG", name)
    print("PASS editable retired candidate "+name+
          ": valid transparent PNG, Unity GUID, import settings and Git blob")
    print("ART_REVIEW_REQUIRED: no old-pixel hash or obsolete frame-size lock")

def is_live(root, name):
    source = (root / ACTOR_SOURCE).read_text()
    return 'Resources.Load<Texture2D>(root + "' + name + '")' in source or (
        'Resources.Load<Texture2D>(root+"'+name+'")' in source)
