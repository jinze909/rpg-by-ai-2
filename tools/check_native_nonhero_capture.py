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
GEOMETRY = RUNTIME / "ActorAtlasGeometry.cs"
STUB = ROOT / "tests/NativeHeroCaptureStubs.cs"
ART = ROOT / "Assets/Resources/Frontier/Art"

def compile_source(path, output):
    mcs = os.environ.get("STARFALL_MCS", shutil.which("mcs"))
    if not mcs:
        raise AssertionError("NONHERO_SPRITERENDERER_COMPILE_REQUIRES_MCS")
    return subprocess.run([mcs, "-define:UNITY_5_3_OR_NEWER",
        "-target:library", "-out:" + str(output), str(STUB), str(GEOMETRY), str(path)],
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
    world_order = (RUNTIME / "FrontierWorldOrder.cs").read_text(encoding="utf-8")
    effects = (RUNTIME / "FrontierEffects.cs").read_text(encoding="utf-8")
    presentation = (RUNTIME / "FrontierPresentation.cs").read_text(encoding="utf-8")
    core = (ROOT / "Assets/Scripts/Frontier/Core/GameCore.cs").read_text(encoding="utf-8")
    native = CAPTURE.read_text()
    geometry = GEOMETRY.read_text(encoding="utf-8")
    assert "private bool CanDrawFallen(FallenActor fallen)" in world_order
    assert "fallen != null && fallen.IsValid" in world_order
    assert "fallen.room == Game.save.room" in world_order
    assert "ValidEffectForDraw(effect)" in world_order
    assert "private static bool ValidEffectForDraw(Effect effect)" in effects
    assert "if (!ValidEffectForDraw(effect)) return false;" in effects
    assert "if (!ValidEffectForDraw(e)) return;" in presentation
    assert "private void RepairTransientEffects()" in core
    assert "private void TickEffects(float dt)" in core
    # The Unity-only native clip builders must never preserve an old
    # m_LocalScale/m_LocalPosition animation on an otherwise correct Sprite.
    # This source contract supplements (not replaces) Editor PlayMode QA.
    for builder_name in ("StarfallNativeActorAnimatorBuilder.cs",
                         "StarfallAcceptedActorAnimatorBuilder.cs"):
        builder = (ROOT / "Assets/Scripts/Frontier/Editor" /
                   builder_name).read_text(encoding="utf-8")
        for required in ("AnimationUtility.GetCurveBindings(clip)",
                         "AnimationUtility.SetEditorCurve(clip,stale,null)",
                         "AnimationUtility.GetObjectReferenceCurveBindings(clip)",
                         "AnimationUtility.SetObjectReferenceCurve("):
            assert required in builder, ("NONHERO_STALE_SCALE_GUARD_MISSING",
                                         builder_name, required)
    assert "if (drawingOutline && sheet != null)" in actor
    # Flash/outline masks and environmental art use direct lossless Atlas,
    # never expensive SpriteRenderer GPU capture as a faux actor.
    assert "if (NativeActorEligible(sheet, columns, rows))" in actor
    assert "DrawSpriteRendererActor(" in actor
    assert "sheet, columns, rows, frame, foot, size," in actor
    assert "if (nativeActorDrawn) return;" in actor
    # Death paint must quarantine invalid transient records before touching
    # their actor reference, and consume the bounded lifetime progress.
    assert "if (body == null || !body.IsValid) return;" in actor, (
        "FALLEN_ACTOR_INVALID_RECORD_GUARD_MISSING")
    assert "float progress = body.Progress;" in actor, (
        "FALLEN_ACTOR_BOUNDED_PROGRESS_MISSING")
    # Runtime imports may be downscaled, but every selected cell must still
    # be a complete square in the approved grid. Shipped PNG sizes are pinned
    # exactly by the source-art audit below.
    assert "private static bool HasSquareGrid(Texture2D sheet, int columns, int rows)" in geometry
    assert "sheet.width >= columns * 32" in geometry
    assert "sheet.height >= rows * 32" in geometry
    assert "sheet.width % columns == 0" in geometry
    assert "sheet.height % rows == 0" in geometry
    assert "sheet.width / columns == sheet.height / rows" in geometry
    for method, expression in (
        ("IsAcceptedMonstersAtlas", "HasSquareGrid(sheet, 6, 3)"),
        ("IsAcceptedBossesAtlas", "HasSquareGrid(sheet, 4, 8)"),
        ("IsAcceptedBriarBeastAtlas", "HasSquareGrid(sheet, 4, 18)"),
        ("IsAcceptedMossboundRaiderAtlas", "HasSquareGrid(sheet, 4, 6)"),
        ("IsAcceptedCinderstoneGuardAtlas", "HasSquareGrid(sheet, 4, 6)"),
        ("IsAcceptedNpcAtlas", "HasSquareGrid(sheet, 4, 4)"),
        ("IsAcceptedCompanionAtlas", "HasSquareGrid(sheet, 3, 4)"),
        ("IsAcceptedWalkMonstersAtlas", "HasSquareGrid(sheet, 4, 3)"),
        ("IsAcceptedRegionalBossWalkAtlas", "HasSquareGrid(sheet, 4, 4)"),
        ("IsAcceptedVerticalArcherAtlas", "HasSquareGrid(sheet, 3, 2)"),
    ):
        assert "internal static bool " + method + "(Texture2D sheet)" in geometry
        assert "return " + expression + ";" in geometry
    assert actor.count("ActorAtlasGeometry.IsAcceptedMonstersAtlas(monsterSheet)") == 3, (
        "MONSTERS_LIVE_PRACTICE_AND_DEATH_GEOMETRY_GATE_MISSING")
    assert actor.count("ActorAtlasGeometry.IsAcceptedBossesAtlas(bossSheet)") == 2, (
        "BOSSES_LIVE_AND_DEATH_GEOMETRY_GATE_MISSING")
    assert actor.count("ActorAtlasGeometry.IsAcceptedBriarBeastAtlas(briarBeastSheet)") == 4, (
        "BRIAR_BEAST_LIVE_AND_DEATH_GEOMETRY_GATE_MISSING")
    assert actor.count("ActorAtlasGeometry.IsAcceptedMossboundRaiderAtlas(mossboundRaiderSheet)") == 2, (
        "MOSSBOUND_LIVE_AND_DEATH_GEOMETRY_GATE_MISSING")
    assert actor.count("ActorAtlasGeometry.IsAcceptedCinderstoneGuardAtlas(cinderstoneGuardSheet)") == 2, (
        "CINDERSTONE_LIVE_AND_DEATH_GEOMETRY_GATE_MISSING")
    # The original 4x4 Npcs atlas is the sole approved base body for normal
    # residents and the rescue civilian; never sample a partial non-null grid.
    assert "internal static bool IsAcceptedNpcAtlas(Texture2D sheet)" in geometry
    assert "return HasSquareGrid(sheet, 4, 4);" in geometry
    assert actor.count("ActorAtlasGeometry.IsAcceptedNpcAtlas(npcSheet)") == 1, (
        "NPC_PRODUCTION_ATLAS_GEOMETRY_GATE_MISSING")
    expansion = (RUNTIME / "FrontierExpansionUI.cs").read_text(encoding="utf-8")
    assert expansion.count("ActorAtlasGeometry.IsAcceptedNpcAtlas(npcSheet)") == 2, (
        "RESCUE_CIVILIAN_ATLAS_GEOMETRY_GATE_MISSING")
    # Live and lethal transitions must share strict accepted source geometry.
    assert "internal static bool IsAcceptedWalkMonstersAtlas(Texture2D sheet)" in geometry
    assert "return HasSquareGrid(sheet, 4, 3);" in geometry
    assert "internal static bool IsAcceptedRegionalBossWalkAtlas(Texture2D sheet)" in geometry
    assert "return HasSquareGrid(sheet, 4, 4);" in geometry
    assert "internal static bool IsAcceptedVerticalArcherAtlas(Texture2D sheet)" in geometry
    assert "return HasSquareGrid(sheet, 3, 2);" in geometry
    assert "IsAcceptedRegionalBossWalkAtlas(bossWalkSheets[atlasIndex])" in actor
    assert actor.count("HasAcceptedRegionalBossWalkAtlas(walkAtlas)") == 2, (
        "BOSS_LIVE_AND_DEATH_WALK_GEOMETRY_GATE_MISSING")
    assert actor.count("IsAcceptedWalkMonstersAtlas(monsterWalkSheet)") == 2, (
        "MONSTER_LIVE_AND_DEATH_WALK_GEOMETRY_GATE_MISSING")
    assert actor.count("IsAcceptedVerticalArcherAtlas(enemyArcherVertical)") == 5, (
        "ARCHER_LIVE_GROUND_AND_DEATH_GEOMETRY_GATE_MISSING")
    for name, expected in (
        ("Npcs", (512, 512)),
        ("Monsters", (768, 384)),
        ("Bosses", (1280, 2560)),
        ("BriarBeast", (1280, 5760)),
        ("MossboundRaider", (512, 768)),
        ("CinderstoneGuard", (512, 768)),
        ("WalkMonsters", (768, 576)),
        ("WalkBossA", (1536, 1536)),
        ("WalkBossB", (1536, 1536)),
        ("EnemyArcherVertical", (576, 384)),
    ):
        with Image.open(ART / (name + ".png")) as source:
            assert source.size == expected, (
                "NONHERO_WALK_ATLAS_GEOMETRY_REGRESSION", name,
                source.size, expected)
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
    assert "EvictUnusedActorCapture(capturePixels);" in native
    assert "actorCachedBytes + incomingBytes > MaxCachedActorBytes" in native
    assert "item.Value.lastUsedFrame < Time.frameCount" in native
    assert "RefreshActorCaptureEpoch();" in native
    assert "entry.lastUsedFrame = -1;" in native
    assert "entry.sourceDirty = true;" in native
    assert "actorCaptureTexture" not in native
    assert "entry.sourceUpdateCount != sheet.updateCount" in native
    assert "entry.sourceUpdateCount = sheet.updateCount;" in native
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
        str(STUB), str(GEOMETRY), str(CAPTURE),
        str(ROOT / "tests/NativeNonheroCaptureChecks.cs")],
        cwd=ROOT, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True)
    assert probe.returncode == 0, ("NONHERO_PROBE_COMPILE", probe.stdout)
    executed = subprocess.run([mono, str(OUT / "nonhero-probe.exe")],
        cwd=ROOT, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True)
    assert executed.returncode == 0, ("NONHERO_PROBE_FAILED", executed.stdout)
    print(executed.stdout.strip())
    # Memory-limit mutation: removing the actual GPU byte-pressure
    # condition must fail the executed 512px multi-actor stress probe.
    no_budget=native.replace(
        "actorCachedBytes + incomingBytes > MaxCachedActorBytes",
        "false",1)
    assert no_budget!=native,"GPU_BYTE_BUDGET_SOURCE_GATE_MISSING"
    no_budget_source=OUT/"faulted-uncapped-gpu.cs"
    no_budget_source.write_text(no_budget)
    no_budget_exe=OUT/"faulted-uncapped-gpu.exe"
    no_budget_build=compile_source(no_budget_source,OUT/"faulted-uncapped-gpu.dll")
    assert no_budget_build.returncode==0,(
        "GPU_BYTE_BUDGET_MUTATION_BUILD_FAILED",no_budget_build.stdout)
    unbounded=subprocess.run([os.environ.get("STARFALL_MCS",shutil.which("mcs")),
        "-define:UNITY_5_3_OR_NEWER","-out:"+str(no_budget_exe),
        str(STUB), str(GEOMETRY),str(no_budget_source),
        str(ROOT/"tests/NativeNonheroCaptureChecks.cs")],
        cwd=ROOT,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,text=True)
    assert unbounded.returncode==0,("GPU_BUDGET_PROBE_BUILD_FAILED",unbounded.stdout)
    missed=subprocess.run([mono,str(no_budget_exe)],
        cwd=ROOT,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,text=True)
    assert missed.returncode!=0 and "NONHERO_CAPTURE_REGRESSION" in missed.stdout,(
        "NONHERO_GPU_BUDGET_MUTATION_FALSE_PASS",missed.stdout)
    print("PASS GPU byte-budget removal mutation hard-fails actual 512px capture stress")
    # A true in-place Texture2D.Apply/import can keep the same Sprite
    # references; removing revision detection must make the executed
    # normal/mirrored GPU-capture probe hard-fail.
    revision_signal = "if(entry.sourceUpdateCount != sheet.updateCount)"
    assert native.count(revision_signal) == 1, "NONHERO_TEXTURE_REVISION_GATE_MISSING"
    stale_pixels = native.replace(revision_signal, "if(false)", 1)
    mutation_path = OUT / "faulted-stale-texture-pixels.cs"
    mutation_path.write_text(stale_pixels)
    mutation_exe = OUT / "faulted-stale-texture-pixels.exe"
    mutation_compiled = subprocess.run([
        os.environ.get("STARFALL_MCS", shutil.which("mcs")),
        "-define:UNITY_5_3_OR_NEWER", "-out:" + str(mutation_exe),
        str(STUB), str(GEOMETRY), str(mutation_path),
        str(ROOT / "tests/NativeNonheroCaptureChecks.cs")],
        cwd=ROOT, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True)
    assert mutation_compiled.returncode == 0, (
        "NONHERO_SOURCE_UPLOAD_MUTATION_BUILD", mutation_compiled.stdout)
    stale = subprocess.run([mono, str(mutation_exe)], cwd=ROOT,
        stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True)
    assert stale.returncode != 0 and "NONHERO_CAPTURE_REGRESSION" in stale.stdout, (
        "NONHERO_SAME_SOURCE_PIXEL_MUTATION_FALSE_PASS", stale.stdout)
    print("PASS removal of source-pixel revision guard fails executable nonhero GPU probe")
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
        str(STUB), str(GEOMETRY), str(downgraded_path),
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
    retry_guard = ("if (Time.frameCount < actorCaptureRetryAt &&\n"
                   "                    Time.frameCount >= actorCaptureRetryAt - 120) return false;")
    assert native.count(retry_guard) == 1, "NONHERO_GPU_RETRY_GUARD_MISSING"
    stuck = native.replace(retry_guard, "if (true) return false;", 1)
    stuck_path = OUT / "faulted-permanent-fallback.cs"
    stuck_path.write_text(stuck)
    stuck_exe = OUT / "faulted-permanent-fallback.exe"
    stuck_compile = subprocess.run([
        os.environ.get("STARFALL_MCS", shutil.which("mcs")),
        "-define:UNITY_5_3_OR_NEWER", "-out:" + str(stuck_exe),
        str(STUB), str(GEOMETRY), str(stuck_path),
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
    # Each cooldown belongs to the frame-count epoch that created it.
    # Regressing ANY one to the old clock-agnostic wait must make the
    # executable same-Controller/same-Sprite recovery witness fail.
    for label, epoch_guard in (
        ("capture", "Time.frameCount >= actorCaptureRetryAt - 120"),
        ("controller", "Time.frameCount >= retryAt - 120"),
        ("rejected-pose", "Time.frameCount >= rejectedUntil - 120"),
    ):
        assert native.count(epoch_guard) == 1, (
            "NONHERO_EPOCH_GUARD_MISSING", label)
        old_epoch = native.replace(epoch_guard, "true", 1)
        mutation = OUT / ("faulted-old-epoch-" + label + ".cs")
        mutation.write_text(old_epoch)
        exe = OUT / ("faulted-old-epoch-" + label + ".exe")
        compiled = subprocess.run([
            os.environ.get("STARFALL_MCS", shutil.which("mcs")),
            "-define:UNITY_5_3_OR_NEWER", "-out:" + str(exe),
            str(STUB), str(GEOMETRY), str(mutation),
            str(ROOT / "tests/NativeNonheroCaptureChecks.cs")],
            cwd=ROOT, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True)
        assert compiled.returncode == 0, (
            "NONHERO_EPOCH_FAULT_COMPILE", label, compiled.stdout)
        observed = subprocess.run([mono, str(exe)], cwd=ROOT,
            stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True)
        assert observed.returncode != 0 and (
            "NONHERO_CAPTURE_REGRESSION" in observed.stdout), (
            "NONHERO_EPOCH_REWIND_FALSE_PASS", label, observed.stdout)
    print("PASS three independent actor clock-rewind cooldown removal mutations hard-fail")
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
        str(STUB), str(GEOMETRY),str(wrong_path),
        str(ROOT/"tests/NativeNonheroCaptureChecks.cs")],
        cwd=ROOT,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,text=True)
    assert wrong_build.returncode==0,("NONHERO_BAD_POSE_TEST_BUILD",wrong_build.stdout)
    refused=subprocess.run([mono,str(wrong_exe)],cwd=ROOT,
        stdout=subprocess.PIPE,stderr=subprocess.STDOUT,text=True)
    assert refused.returncode!=0 and (
        "NONHERO_CAPTURE_REGRESSION" in refused.stdout),(
        "NONHERO_CI_ACCEPTED_INDEPENDENT_IDLE_ANIMATOR",refused.stdout)
    subprocess.run([sys.executable,
        str(ROOT/"tools/check_native_accepted_actor_builder.py")],
        cwd=ROOT,check=True)
    print("PASS regression mutation: forced Idle Animator row hard-fails")
    # Regressing to the obsolete 4-column-only Animator loses the original
    # 6-column monster attacks and 3-column north/south archer poses.
    known = "columns == NativeActorColumnCounts[kind]"
    assert native.count(known)==1,"ORIGINAL_ACTOR_NATIVE_COLUMNS_MISSING"
    wrong=native.replace(known,"columns == 4",1)
    wrongPath=OUT/"faulted-four-column-only.cs"
    wrongPath.write_text(wrong)
    wrongExe=OUT/"faulted-four-column-only.exe"
    wrongBuilt=subprocess.run([
        os.environ.get("STARFALL_MCS",shutil.which("mcs")),
        "-define:UNITY_5_3_OR_NEWER","-out:"+str(wrongExe),
        str(STUB), str(GEOMETRY),str(wrongPath),
        str(ROOT/"tests/NativeNonheroCaptureChecks.cs")],
        cwd=ROOT,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,text=True)
    assert wrongBuilt.returncode==0,("ORIGINAL_ACTOR_COLUMNS_MUTATION_BUILD",wrongBuilt.stdout)
    wrongRun=subprocess.run([mono,str(wrongExe)],cwd=ROOT,
        stdout=subprocess.PIPE,stderr=subprocess.STDOUT,text=True)
    assert wrongRun.returncode!=0 and (
        "NONHERO_CAPTURE_REGRESSION" in wrongRun.stdout),(
        "ORIGINAL_ACTOR_COLUMNS_MUTATION_WAS_NOT_REJECTED",wrongRun.stdout)
    print("PASS obsolete fixed-four-column Animator mutation hard-fails")
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
