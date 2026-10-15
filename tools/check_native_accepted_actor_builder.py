#!/usr/bin/env python3
"""Compile and audit native Animator import of accepted illustrated actor art.

No Unity Editor/GPU exists in hosted CI. Real PlayMode remains NATIVE_UNVERIFIED.
The seven original PNGs must not be replaced with archived low-res candidates.
"""
from __future__ import annotations
import json
import re
import os
import shutil
import subprocess
from pathlib import Path
from PIL import Image

ROOT=Path(__file__).resolve().parents[1]
BUILDER=ROOT/"Assets/Scripts/Frontier/Editor/StarfallAcceptedActorAnimatorBuilder.cs"
STUB=ROOT/"tests/NativeAcceptedEditorStubs.cs"
MANIFEST=ROOT/"Assets/Resources/Frontier/Actors/Native/AcceptedActorSources.json"
RUNTIME=ROOT/"Assets/Scripts/Frontier/Runtime/FrontierActorSpriteCapture.cs"
OUT=ROOT/".starfall-checks/native-accepted-editor"
APPROVED=[
    ("Monsters",6,3),("WalkMonsters",4,3),("Bosses",4,8),
    ("WalkBossA",4,4),("WalkBossB",4,4),("Npcs",4,4),
    ("EnemyArcherVertical",3,2)
]
DEPRECATED={"MossboundRaider","BriarBeast","CinderstoneGuard",
            "EmberfallResidents"}

def ensure_builder_contract(code):
    for fragment in (
        "SourceManifest manifest =",
        "JsonUtility.FromJson<SourceManifest>(manifestAsset.text)",
        "AssetDatabase.AssetPathToGUID(png)",
        "importer.spriteImportMode = SpriteImportMode.Multiple;",
        "importer.spritePixelsPerUnit = height;",
        "importer.spritesheet = cells;",
        "importer.SaveAndReimport();",
        "if (source == null || AssetDatabase.AssetPathToGUID(png) != guid)",
        "Mathf.Abs(sprite.pixelsPerUnit - height) > .05f",
        "Vector2.Distance(sprite.pivot,",
        "AnimationUtility.SetObjectReferenceCurve(clip,",
        "new ObjectReferenceKeyframe[spec.columns + 1]",
        "value = imported[row * spec.columns + column]",
        "AnimationUtility.GetObjectReferenceCurve(clip,",
        "keys[col].value != imported[spriteIndex]",
        "UnityEditor.Build.IPreprocessBuildWithReport",
        "StarfallAcceptedActorAnimatorBuilder.EnsureBuilt();",
        "UnityEditor.Build.BuildFailedException",
    ):
        assert fragment in code,("NATIVE_ACCEPTED_SOURCE_OR_BUILD_GATE_MISSING",fragment)

def main():
    manifest=json.loads(MANIFEST.read_text())
    specs=[(x["name"],x["columns"],x["rows"])
           for x in manifest["sources"]]
    assert manifest["version"]==1 and specs==APPROVED, (
        "ACCEPTED_ACTOR_MANIFEST_MISMATCH",specs)
    assert not DEPRECATED.intersection(x[0] for x in specs), (
        "LOW_DETAIL_CANDIDATE_ENTERED_ACCEPTED_MANIFEST")
    source=BUILDER.read_text()
    ensure_builder_contract(source)
    for dangerous in (
        source.replace("importer.spritePixelsPerUnit = height;",
                       "importer.spritePixelsPerUnit = 256;",1),
        source.replace("keys[col].value != imported[spriteIndex]",
                       "false",1),
        source.replace("StarfallAcceptedActorAnimatorBuilder.EnsureBuilt();",
                       "StarfallAcceptedActorAnimatorBuilder.ToString();",1),
        source.replace("if (source == null || AssetDatabase.AssetPathToGUID(png) != guid)",
                       "if (source == null)",1),
    ):
        try: ensure_builder_contract(dangerous)
        except AssertionError: pass
        else: raise AssertionError(
            "NATIVE_ACCEPTED_CI_FALSE_PASS_ON_UNSAFE_BUILDER_MUTATION")
    native=RUNTIME.read_text()
    for namespec in APPROVED:
        name,cols,rows=namespec
        assert '"'+name+'"' in native, (
            "ACCEPTED_ATLAS_UNWIRED_FROM_RUNTIME",name)
        png=ROOT/"Assets/Resources/Frontier/Art"/(name+".png")
        assert png.exists(),("ACCEPTED_SOURCE_PNG_MISSING",name)
        meta=Path(str(png)+".meta")
        assert meta.exists() and re.search(
            r"(?m)^guid: [0-9a-f]{32}$",meta.read_text()), (
                "ACCEPTED_ATLAS_GUID_MISSING",name)
        with png.open("rb") as fh: header=fh.read(80)
        if header.startswith(b"version https://git-lfs.github.com/spec/v1"):
            # A CI checkout with LFS=false has pointers for the detailed
            # Bosses/World assets, NOT native raster data to visually inspect.
            assert b"oid sha256:" in header, (
                "INVALID_ACCEPTED_LFS_POINTER",name)
            continue
        with Image.open(png) as image:
            assert image.format=="PNG",("ACCEPTED_ATLAS_NOT_PNG",name)
            assert image.width % cols==0 and image.height % rows==0, (
                "ACCEPTED_ATLAS_UNSLICEABLE",name,(image.width,image.height))
            assert image.width//cols>=64 and image.height//rows>=64, (
                "ACCEPTED_ATLAS_LOW_SOURCE_RESOLUTION",name)
    for fragment in (
        "NativeActorColumnCounts",
        "NativeActorRowCounts",
        "columns == NativeActorColumnCounts[kind]",
        "rows == NativeActorRowCounts[kind]",
        "entry.nativeClipVerified",
        "bool alreadySubmitted = entry.lastUsedFrame == Time.frameCount;",
    ):
        assert fragment in native,("ACCEPTED_NATIVE_RUNTIME_GUARD_MISSING",fragment)
    compiler=os.environ.get("STARFALL_MCS") or shutil.which("mcs")
    assert compiler, "NATIVE_ACCEPTED_EDITOR_COMPILER_MISSING"
    OUT.mkdir(parents=True,exist_ok=True)
    built=subprocess.run([compiler,"-target:library","-define:UNITY_EDITOR",
        "-out:"+str(OUT/"accepted-actor-editor.dll"),str(STUB),str(BUILDER)],
        cwd=ROOT,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,text=True)
    assert built.returncode==0,("ACCEPTED_UNITY_EDITOR_SOURCE_COMPILE_FAILED",built.stdout)
    print("PASS seven ORIGINAL high-detail source atlas layouts; no candidate promotion")
    print("PASS UnityEditor conditional importer/Player build gate compiled under offline declarations")
    print("PASS attack-source GUID, foot pivot, 3/4/6-col Sprite curve and adversarial mutation checks")
    print("NATIVE_UNVERIFIED: licensed Unity 2022.3 Editor, actual Sprite subassets and GameView not executed")

if __name__=="__main__":
    main()
