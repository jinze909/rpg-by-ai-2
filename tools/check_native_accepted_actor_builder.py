#!/usr/bin/env python3
"""Compile and audit native Animator import of accepted illustrated actor art.

No Unity Editor/GPU exists in hosted CI. Real PlayMode remains NATIVE_UNVERIFIED.
The 13 accepted original atlas families must not be replaced with archived low-res candidates.
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
    ("EnemyArcherVertical",3,2),
    ("CompanionMira",3,4),("CompanionIvo",3,4),("CompanionSen",3,4),
    ("WalkMira",4,3),("WalkIvo",4,3),("WalkSen",4,3)
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
        "Texture2D resourceTexture = Resources.Load<Texture2D>(",
        "if (source == null || resourceTexture == null ||",
        "recover.textureType = originalType;",
        "recover.spritePixelsPerUnit = originalPpu;",
        "recover.SaveAndReimport();",
        "Mathf.Abs(sprite.pixelsPerUnit - height) > .05f",
        "Vector2.Distance(sprite.pivot,",
        "AnimationUtility.SetObjectReferenceCurve(clip,",
        "new ObjectReferenceKeyframe[spec.columns + 1]",
        "value = imported[row * spec.columns + column]",
        "AnimationUtility.GetObjectReferenceCurve(clip,",
        "keys[col].value != imported[spriteIndex]",
        "UnityEditor.Build.IPreprocessBuildWithReport",
        "if (state != PlayModeStateChange.ExitingEditMode) return;",
        "try { EnsureBuilt(); }",
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
    # The old C# guard demanded 13 entries but only 7 distinct names:
    # it rejected the actual immutable 13-source manifest on every import.
    # Audit exact identity, order AND dimensions against the production
    # Editor builder, not only against this Python mirror.
    def csharp_array(name, kind):
        match=re.search(r"private static readonly "+kind+r"\\[\\] "+name+
            r"\\s*=\\s*\\{([^}]+)\\};",source,re.S)
        assert match,("ACCEPTED_SOURCE_SPEC_MISSING",name)
        if kind=="string":
            return re.findall(r'"([A-Za-z0-9]+)"',match.group(1))
        return [int(v) for v in re.findall(r"\\d+",match.group(1))]
    assert csharp_array("AcceptedSourceNames","string")==[x[0] for x in APPROVED], (
        "NATIVE_EDITOR_ACCEPTED_IDENTITY_MISMATCH")
    assert csharp_array("AcceptedSourceColumns","int")==[x[1] for x in APPROVED], (
        "NATIVE_EDITOR_ACCEPTED_COLUMN_MISMATCH")
    assert csharp_array("AcceptedSourceRows","int")==[x[2] for x in APPROVED], (
        "NATIVE_EDITOR_ACCEPTED_ROW_MISMATCH")
    for clause in ("manifest.sources.Length != AcceptedSourceNames.Length",
        "source.name != AcceptedSourceNames[i]",
        "source.columns != AcceptedSourceColumns[i]",
        "source.rows != AcceptedSourceRows[i]"):
        assert clause in source,("NATIVE_EDITOR_ACCEPTED_MANIFEST_CHECK_MISSING",clause)
    for dangerous in (
        source.replace("manifest.sources.Length != AcceptedSourceNames.Length",
                       "manifest.sources.Length != 7",1),
        source.replace("source.columns != AcceptedSourceColumns[i]",
                       "source.columns == AcceptedSourceColumns[i]",1),
        source.replace("importer.spritePixelsPerUnit = height;",
                       "importer.spritePixelsPerUnit = 256;",1),
        source.replace("keys[col].value != imported[spriteIndex]",
                       "false",1),
        source.replace("StarfallAcceptedActorAnimatorBuilder.EnsureBuilt();",
                       "StarfallAcceptedActorAnimatorBuilder.ToString();",1),
        source.replace("Texture2D resourceTexture = Resources.Load<Texture2D>(",
                       "Texture2D resourceTexture = null; //",1),
        source.replace("recover.textureType = originalType;",
                       "recover.textureType = TextureImporterType.Sprite;",1),
        source.replace("if (state != PlayModeStateChange.ExitingEditMode) return;",
                       "if (false) return;",1),
    ):
        try:
            ensure_builder_contract(dangerous)
            for clause in ("manifest.sources.Length != AcceptedSourceNames.Length",
                "source.name != AcceptedSourceNames[i]",
                "source.columns != AcceptedSourceColumns[i]",
                "source.rows != AcceptedSourceRows[i]"):
                assert clause in dangerous
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
    # Keep an inspectable filmstrip from the exact original source PNGs,
    # not from generated 32px test substitutes or idle GIFs. Duplicate
    # deliberate holds are allowed; blank actor cells are not.
    from PIL import ImageDraw
    import hashlib
    import json as _json
    review=ROOT/".starfall-checks/sprite-qa/accepted-original-actors"
    review.mkdir(parents=True,exist_ok=True)
    reviewed=[]
    for name,columns,rows in APPROVED:
        png=ROOT/"Assets/Resources/Frontier/Art"/(name+".png")
        with png.open("rb") as handle:
            header=handle.read(80)
        if header.startswith(b"version https://git-lfs.github.com/spec/v1"):
            reviewed.append({"family":name,"raster":"LFS_NOT_DOWNLOADED",
                "artReview":"NATIVE_UNVERIFIED"})
            continue
        with Image.open(png) as loaded:
            atlas=loaded.convert("RGBA")
        cw,ch=atlas.width//columns,atlas.height//rows
        film=Image.new("RGBA",(atlas.width,atlas.height+rows*18),
            (27,35,46,255))
        draw=ImageDraw.Draw(film)
        cells=[]
        for row in range(rows):
            for col in range(columns):
                rect=(col*cw,row*ch,(col+1)*cw,(row+1)*ch)
                tile=atlas.crop(rect)
                alpha=tile.getchannel("A")
                bounds=alpha.getbbox()
                assert bounds is not None,(
                    "ACCEPTED_FULL_BODY_FRAME_MISSING",name,row,col)
                cells.append({"row":row,"cell":col,
                    "opaqueBounds":list(bounds),
                    "sha256":hashlib.sha256(tile.tobytes()).hexdigest()[:16]})
                # Keep every pixel at exact authored coordinates. No
                # resizing, face/leg synthesis, tint, downscaling or pixel
                # repaint in the visual QA output.
                film.alpha_composite(tile,(col*cw,row*(ch+18)))
                draw.text((col*cw+3,row*(ch+18)+ch+2),
                    name+" "+str(row)+":"+str(col),fill=(220,231,237,255))
        film.save(review/(name+"-source-grid.png"))
        reviewed.append({"family":name,"dimensions":[atlas.width,atlas.height],
            "cell":[cw,ch],"frames":cells,
            "artReview":"ART_REVIEW_REQUIRED"})
    (review/"source-identity.json").write_text(
        _json.dumps(reviewed,indent=2)+"\n")
    print("PASS "+str(sum(len(x.get("frames",[])) for x in reviewed))+
          " authored original Sprite cells with no blank frames; full-resolution grids emitted")
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
    print("PASS 13 ORIGINAL illustrated actor + companion atlas layouts; no candidate promotion")
    print("PASS UnityEditor conditional importer/Player build gate compiled under offline declarations")
    print("PASS attack-source GUID, foot pivot, 3/4/6-col Sprite curve and adversarial mutation checks")
    print("NATIVE_UNVERIFIED: licensed Unity 2022.3 Editor, actual Sprite subassets and GameView not executed")

if __name__=="__main__":
    main()
