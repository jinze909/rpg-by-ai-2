#!/usr/bin/env python3
"""Hard-gate the actual nonhero SpriteRenderer runtime and imported actor PNGs.

Offline C# compile and pixel checks only; Unity/GPU/Animator are NOT verified.
"""
from pathlib import Path
import os
import shutil
import subprocess
import sys
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
    assert "DrawSpriteRendererActor(sheet, columns, rows,\n                frame," in actor
    assert "if (nativeActorDrawn) return;" in actor
    # The cue must be pinned to the actual Talk() target, not a moving
    # Nearest() query that can switch to a different same-kind resident.
    core = (ROOT / "Assets/Scripts/Frontier/Core/GameCore.cs").read_text()
    assert "WorldObject interlocutor = Game.conversationSource;" in actor
    assert "WorldObject interlocutor = Game.Nearest();" not in actor
    assert "conversationSource = o;" in core
    assert "conversation = null; conversationSource = null;" in core
    assert "interlocutor.kind == kind" in actor
    assert "Math.Abs(interlocutor.x - position.x) < .05f" in actor
    assert "Math.Abs(interlocutor.y - position.y) < .05f" in actor
    assert "ReleaseActorCapture();" in scene
    assert native.count("actorCaptureCamera.Render();") == 1
    assert "actorCapturedFrames" in native
    assert "actorCaptureCamera.targetTexture = entry.texture;" in native
    assert "entry.texture);" in native
    assert "EvictUnusedActorCapture();" in native
    assert "actorCaptureTexture" not in native
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
    # Exercise the REAL #if UNITY_5_3_OR_NEWER production method with
    # instrumented render targets, not just grep or compile-only success.
    mono = os.environ.get("STARFALL_MONO", shutil.which("mono"))
    assert mono, "NONHERO_RUNTIME_PROBE_REQUIRES_MONO"
    probe = subprocess.run([os.environ.get("STARFALL_MCS", shutil.which("mcs")),
        "-define:UNITY_5_3_OR_NEWER", "-out:" + str(OUT / "nonhero-probe.exe"),
        str(STUB), str(CAPTURE),
        str(ROOT / "tests/NativeNonheroCaptureChecks.cs")],
        cwd=ROOT, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True)
    assert probe.returncode == 0, ("NONHERO_PROBE_COMPILE", probe.stdout)
    executed = subprocess.run([mono, str(OUT / "nonhero-probe.exe")],
        cwd=ROOT, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True)
    assert executed.returncode == 0, ("NONHERO_PROBE_FAILED", executed.stdout)
    print(executed.stdout.strip())
    # A historical fixed-size 256px framebuffer MUST cause this real
    # runtime probe to FAIL on the 320px Boss source, not warn-and-pass.
    downgraded = native.replace(
        "new RenderTexture(capturePixels, capturePixels, 16,",
        "new RenderTexture(256, 256, 16,", 1)
    assert downgraded != native, "NONHERO_RESOLUTION_GUARD_MISSING"
    downgraded_path = OUT / "faulted-resolution.cs"
    downgraded_path.write_text(downgraded)
    downgraded_exe = OUT / "faulted-resolution.exe"
    downgraded_compile = subprocess.run([
        os.environ.get("STARFALL_MCS", shutil.which("mcs")),
        "-define:UNITY_5_3_OR_NEWER", "-out:" + str(downgraded_exe),
        str(STUB), str(downgraded_path),
        str(ROOT / "tests/NativeNonheroCaptureChecks.cs")],
        cwd=ROOT, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True)
    assert downgraded_compile.returncode == 0, (
        "NONHERO_FAULT_INJECTION_FAILED_TO_COMPILE",
        downgraded_compile.stdout)
    rejected = subprocess.run([mono, str(downgraded_exe)], cwd=ROOT,
        stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True)
    assert rejected.returncode != 0 and (
        "NONHERO_CAPTURE_REGRESSION" in rejected.stdout), (
        "NONHERO_CI_FALSE_PASS_256PX_DOWNSAMPLE", rejected.stdout)
    print("PASS regression mutation: forced 256px Boss downsampling hard-fails")
    # An exception used to permanently disable every actor's native
    # renderer. Deliberately restore that unsafe behaviour: CI must reject.
    retry_guard = "if (Time.frameCount < actorCaptureRetryAt) return false;"
    assert native.count(retry_guard) == 1, "NONHERO_GPU_RETRY_GUARD_MISSING"
    stuck = native.replace(retry_guard, "if (true) return false;", 1)
    stuck_path = OUT / "faulted-permanent-fallback.cs"
    stuck_path.write_text(stuck)
    stuck_exe = OUT / "faulted-permanent-fallback.exe"
    stuck_compile = subprocess.run([
        os.environ.get("STARFALL_MCS", shutil.which("mcs")),
        "-define:UNITY_5_3_OR_NEWER", "-out:" + str(stuck_exe),
        str(STUB), str(stuck_path),
        str(ROOT / "tests/NativeNonheroCaptureChecks.cs")],
        cwd=ROOT, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True)
    assert stuck_compile.returncode == 0, (
        "NONHERO_FAULTED_RETRY_BUILD", stuck_compile.stdout)
    blocked = subprocess.run([mono, str(stuck_exe)], cwd=ROOT,
        stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True)
    assert blocked.returncode != 0 and (
        "NONHERO_CAPTURE_REGRESSION" in blocked.stdout), (
        "NONHERO_PERMANENT_GPU_FALLBACK_FALSE_PASS", blocked.stdout)
    print("PASS regression mutation: permanent nonhero GPU fallback hard-fails")
    # Mutation: force every row to Idle despite GameCore choosing Attack,
    # phase changes, talking or death. The same real native C# probe MUST
    # fail instead of green-lighting a placeholder Animator component.
    row_signal = 'actorCaptureAnimator.SetInteger("Pose", row);'
    assert native.count(row_signal)==1, "NONHERO_NATIVE_POSE_BRIDGE_MISSING"
    wrong_row = native.replace(row_signal,
        'actorCaptureAnimator.SetInteger("Pose", 0);',1)
    wrong_path=OUT/"faulted-nonhero-native-pose.cs"
    wrong_path.write_text(wrong_row)
    wrong_exe=OUT/"faulted-nonhero-native-pose.exe"
    wrong_build=subprocess.run([
        os.environ.get("STARFALL_MCS",shutil.which("mcs")),
        "-define:UNITY_5_3_OR_NEWER", "-out:"+str(wrong_exe),
        str(STUB),str(wrong_path),
        str(ROOT/"tests/NativeNonheroCaptureChecks.cs")],
        cwd=ROOT,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,text=True)
    assert wrong_build.returncode==0,("NONHERO_BAD_POSE_TEST_BUILD",wrong_build.stdout)
    refused=subprocess.run([mono,str(wrong_exe)],cwd=ROOT,
        stdout=subprocess.PIPE,stderr=subprocess.STDOUT,text=True)
    assert refused.returncode!=0 and (
        "NONHERO_CAPTURE_REGRESSION" in refused.stdout),(
        "NONHERO_CI_ACCEPTED_INDEPENDENT_IDLE_ANIMATOR",refused.stdout)
    print("PASS regression mutation: forced Idle Animator row hard-fails")
    # Each indexed atlas listed below is ACTUALLY sampled in FrontierActors.
    atlases = [
        ("Npcs", 4, 4, range(16)),
        ("Monsters", 6, 3, range(18)),
        ("WalkMonsters", 4, 3, range(12)),
        ("Bosses", 4, 8, range(32)),
        ("WalkBossA", 4, 4, range(16)),
        ("WalkBossB", 4, 4, range(16)),
        ("EnemyArcherVertical", 3, 2, range(6)),
        ("CinderstoneGuard", 4, 6, range(24)),
    ]
    count = sum(audit_art(*spec) for spec in atlases)
    # Deterministic hard gate for all 24 shipped guard action cells, including
    # a destructive missing-frame fault injection and live render mapping.
    subprocess.run([sys.executable, str(ROOT / "tools/check_cinderstone_guard.py")],
                   check=True, cwd=ROOT)
    # Core CI already installs Mono here. Run the actual production DrawNpc
    # regression as a mandatory hard gate, not merely a source-string check.
    # Keep the independent PNG-only art publisher free of Mono requirements.
    subprocess.run([sys.executable, str(ROOT / "tools/run_layout_checks.py"),
                    "--resident-only"], check=True, cwd=ROOT)
    print("PASS live nonhero SpriteRenderer C# compiled; deliberate API mutation FAILED")
    print("PASS %d indexed monster/Boss/NPC frames validated in real shipped PNGs" % count)
    print("NATIVE_UNVERIFIED: no Unity GameView/PlayMode or imported Animator playback")

if __name__ == "__main__":
    main()
