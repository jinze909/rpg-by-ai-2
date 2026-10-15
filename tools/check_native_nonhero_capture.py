#!/usr/bin/env python3
"""Hard-gate the actual nonhero SpriteRenderer runtime and imported actor PNGs.

Offline C# compile and pixel checks only; Unity/GPU/Animator are NOT verified.
"""
from pathlib import Path
import os
import shutil
import subprocess
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / ".starfall-checks/native-nonhero-compile"
RUNTIME = ROOT / "Assets/Scripts/Frontier/Runtime"
CAPTURE = RUNTIME / "FrontierActorSpriteCapture.cs"
STUB = ROOT / "tests/NativeHeroCaptureStubs.cs"
ART = ROOT / "Assets/Resources/Frontier/Art"

def compile_source(path, output):
    mcs = os.environ.get("STARFALL_MCS", shutil.which("mcs"))
    if not mcs:
        raise AssertionError("NONHERO_SPRITERENDERER_COMPILE_REQUIRES_MCS")
    return subprocess.run([mcs, "-define:UNITY_5_3_OR_NEWER",
        "-target:library", "-out:" + str(output), str(STUB), str(path)],
        cwd=ROOT, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True)

def audit_art(name, columns, rows, used):
    filename = ART / (name + ".png")
    assert filename.is_file(), ("MISSING_LIVE_NONHERO_ATLAS", name)
    with Image.open(filename) as source:
        assert source.format == "PNG", ("NONHERO_NOT_PNG", name)
        image = source.convert("RGBA")
    assert image.width % columns == 0 and image.height % rows == 0, (
        "NONHERO_GRID_TRUNCATED", name, image.size)
    w, h = image.width // columns, image.height // rows
    assert w >= 32 and h >= 32, ("NONHERO_CANVAS_TOO_SMALL", name, (w,h))
    for index in used:
        assert 0 <= index < columns * rows, ("NONHERO_FRAME_OOB",name,index)
        x, y = index % columns * w, index // columns * h
        body = image.crop((x,y,x+w,y+h)).getchannel("A").getbbox()
        assert body is not None and body[2]-body[0] >= 8 and body[3]-body[1] >= 8, (
            "NONHERO_MISSING_FULL_BODY_CELL", name, index, body)
    meta = filename.with_suffix(".png.meta").read_text()
    assert "filterMode: 0" in meta and "enableMipMap: 0" in meta, (
        "NONHERO_PIXEL_IMPORT_REGRESSION", name)
    return len(used)

def main():
    OUT.mkdir(parents=True, exist_ok=True)
    actor = (RUNTIME / "FrontierActors.cs").read_text()
    scene = (RUNTIME / "FrontierRuntime.cs").read_text()
    native = CAPTURE.read_text()
    assert "if (drawingOutline && sheet != null)" in actor
    assert "DrawSpriteRendererActor(sheet, columns, rows, frame," in actor
    assert "if (nativeActorDrawn) return;" in actor
    assert "ReleaseActorCapture();" in scene
    assert native.count("actorCaptureCamera.Render();") == 1
    assert "new Vector2(.5f, 1f - 120f / 128f)" in native
    assert "new Vector3((float)height / width, 1f, 1f)" in native
    assert "actorCaptureRenderer.flipX = flip;" in native
    assert "if (Event.current.type != EventType.Repaint) return true;" in native
    assert "if (drawingOutline" not in native  # outlines must stay on legacy lane
    real = compile_source(CAPTURE, OUT / "nonhero-source.dll")
    assert real.returncode == 0, ("NONHERO_NATIVE_CSHARP_COMPILE", real.stdout)
    fault = native.replace("actorCaptureRenderer.flipX = flip;",
        "actorCaptureRenderer.unsafeDeprecatedLimbMerge = flip;", 1)
    assert fault != native
    broken = OUT / "faulted-nonhero.cs"
    broken.write_text(fault)
    failed = compile_source(broken, OUT / "faulted.dll")
    assert failed.returncode != 0 and "CS1061" in failed.stdout, (
        "NONHERO_INVALID_RENDERER_API_WAS_ACCEPTED", failed.stdout)
    # Each indexed atlas listed below is ACTUALLY sampled in FrontierActors.
    atlases = [
        ("Npcs", 4, 4, range(16)),
        ("Monsters", 6, 3, range(18)),
        ("WalkMonsters", 4, 3, range(12)),
        ("Bosses", 4, 8, range(32)),
        ("WalkBossA", 4, 4, range(16)),
        ("WalkBossB", 4, 4, range(16)),
        ("EnemyArcherVertical", 3, 2, range(6)),
    ]
    count = sum(audit_art(*spec) for spec in atlases)
    print("PASS live nonhero SpriteRenderer C# compiled; deliberate API mutation FAILED")
    print("PASS %d indexed monster/Boss/NPC frames validated in real shipped PNGs" % count)
    print("NATIVE_UNVERIFIED: no Unity GameView/PlayMode or imported Animator playback")

if __name__ == "__main__":
    main()
