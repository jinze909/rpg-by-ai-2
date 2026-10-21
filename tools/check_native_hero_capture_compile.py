#!/usr/bin/env python3
"""Compile REAL #if UNITY_5_3_OR_NEWER hero renderer, without a Unity license.

Mono's normal GameCore/Editor stubs don't define UNITY_5_3_OR_NEWER, so
FrontierHeroSpriteCapture.cs was silently excluded from CI for months.
This compiles that checked-in C# and the canonical Sprite factory against
a strict, compile-only Unity API surface, then proves a damaged call fails.
Does NOT execute graphics, Animator, SpriteImporter or native Unity PlayMode.
"""
from pathlib import Path
import os
import shutil
import subprocess

ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/".starfall-checks/native-hero-compile"
CAPTURE=ROOT/"Assets/Scripts/Frontier/Runtime/FrontierHeroSpriteCapture.cs"
FACTORY=ROOT/"Assets/Scripts/Frontier/Runtime/CanonicalHeroSpriteFactory.cs"
STUB=ROOT/"tests/NativeHeroCaptureStubs.cs"
ACTORS=ROOT/"Assets/Scripts/Frontier/Runtime/FrontierActors.cs"
GATE=OUT/"real-hero-contact-family.cs"


def prepare_actual_contact_gate():
    """Compile the actual, extracted FrontierActors publication method in
    the strict native Sprite probe. Do not copy or reimplement its logic.
    FrontierActors' unrelated world/UI methods need the full game runtime,
    so only its unchanged real method body is injected into a partial class.
    """
    actors=ACTORS.read_text(encoding="utf-8")
    begin="        private bool HasCompleteHeroContactArt()"
    end="        private bool ResolveFullBodyHeroFrame("
    assert actors.count(begin)==1 and actors.count(end)==1, (
        "NATIVE_HERO_CONTACT_GATE_MOVED_OR_REMOVED",)
    body=begin+actors.split(begin,1)[1].split(end,1)[0]
    boilerplate=(
        "using System;\nusing System.Collections.Generic;\n"
        "using UnityEngine;\nnamespace Starfall {\n"
        "public sealed partial class FrontierRuntime {\n"
        "private float clock;\n"
        "private const float HeroStopPlantSeconds=.075f;\n"
        "private GameCore heroGaitOwner;\n"
        # The compile-only NativeProbeSave mirrors GameCore.save here.
        # Keep this owned reference state alongside the REAL extracted
        # ApplyHeroStopPlant method; never reimplement its save test.
        "private NativeProbeSave heroGaitSave;\n"
        "private int heroGaitRoom=-1,heroGaitAppearance=-1,heroGaitFacing=-1;\n"
         "private bool heroGaitMirrored;\n"
        "private int heroGaitFrame=-1;\n"
        "private Point heroGaitPosition;\n"
        "private float heroGaitLastAt=float.NegativeInfinity;\n"
        "private bool heroGaitWasMoving,heroStopPlantActive;\n"
        "private int heroStopPlantFrame;\n"
        "private float heroStopPlantUntil=float.NegativeInfinity;\n"
        # Mirror ONLY state shapes, and compile the REAL extracted
        # ApplyHeroContactSettle/ApplyHeroStopPlant logic unmodified.
        # Original art, GameCore damage clock and real native renderer
        # remain the authority in their respective build checks.
        "private const float HeroContactSettleSeconds=.07f;\n"
        "private GameCore heroContactSettleOwner;\n"
        "private NativeProbeSave heroContactSettleSave;\n"
        "private ActorAction heroContactSettleAction;\n"
        "private int heroContactSettleRoom,heroContactSettleEquipment,\n"
        "    heroContactSettleFacing,heroContactSettleBody,\n"
        "    heroContactSettleFrames,heroContactSettleCell;\n"
         "private bool heroContactSettleMirrored;\n"
        "private Point heroContactSettleFoot;\n"
        "private float heroContactSettleAt=float.NegativeInfinity;\n"
        "private const float HeroHitSettleSeconds=.055f;\n"
         "private GameCore heroHitSettleOwner;\n"
         "private NativeProbeSave heroHitSettleSave;\n"
         "private int heroHitSettleRoom,heroHitSettleEquipment,\n"
         "    heroHitSettleFacing,heroHitSettleCell;\n"
         "private bool heroHitSettleMirrored;\n"
         "private Point heroHitSettleFoot;\n"
         "private float heroHitSettleAt=float.NegativeInfinity;\n"
         "private bool heroHitSettleActive;\n"
         "private bool heroContactFamilyChecked;\n"
        "private bool heroContactFamilyComplete;\n"
        "private float heroContactLastCheckedAt=float.NegativeInfinity;\n"
        "private bool heroContactFamilyPending;\n"
        "private ActorAction heroContactFamilyDeferredAction;\n"
        "private readonly Dictionary<string,Texture2D> fullBodyHeroSheetCache = "
        "new Dictionary<string,Texture2D>();\n"
        "private static readonly string[] FullHeroClasses = "
        "{\"Vanguard\",\"Ranger\",\"Arcanist\"};\n"
        "private static readonly string[] FullHeroFacings = {\"E\",\"N\",\"S\"};\n"
    )
    GATE.write_text(boilerplate+body+"\n}\n}\n",encoding="utf-8")




def compiler():
    mcs=os.environ.get("STARFALL_MCS",shutil.which("mcs"))
    mono=os.environ.get("STARFALL_MONO",shutil.which("mono"))
    if not mcs:
        raise AssertionError("NATIVE_HERO_COMPILE_CHECK_CANNOT_FIND_MONO_MCS")
    return [mono,mcs] if mcs.endswith(".exe") else [mcs]


def compile_native(source,out):
    cmd=compiler()+[
        "-define:UNITY_5_3_OR_NEWER",
        "-target:library",
        "-out:"+str(out),
        str(STUB),
        str(GATE),
        str(FACTORY),
        str(source),
    ]
    return subprocess.run(cmd,cwd=ROOT,stdout=subprocess.PIPE,
                          stderr=subprocess.STDOUT,text=True)


def run():
    OUT.mkdir(parents=True,exist_ok=True)
    prepare_actual_contact_gate()
    assert CAPTURE.is_file() and FACTORY.is_file() and STUB.is_file()
    good=compile_native(CAPTURE,OUT/"native-hero-real-source.dll")
    assert good.returncode==0, (
        "ACTUAL_UNITY_NATIVE_SPRITERENDERER_CSHARP_COMPILE_FAILED",
        good.stdout[-8000:])
    original=CAPTURE.read_text(encoding="utf-8")
    required="heroCaptureRenderer.flipX = flip;"
    assert original.count(required)==1, (
        "ACTUAL_NATIVE_RENDERER_ORIENTATION_CODE_CHANGED",)
    broken=original.replace(
        required,"heroCaptureRenderer.unsafeLegacyLimbController = flip;",1)
    candidate=OUT/"broken-native-capture.cs"
    candidate.write_text(broken,encoding="utf-8")
    negative=compile_native(candidate,OUT/"should-not-compile.dll")
    assert negative.returncode!=0 and (
        "CS1061" in negative.stdout or "CS0117" in negative.stdout), (
        "UNSAFE_CI_ACCEPTED_INJECTED_INVALID_UNITY_SPRITERENDERER_API",
        negative.stdout[-1200:])
    # Compile AND execute real native-conditional C# through instrumented
    # Unity objects, then adversarially force a single shared capture.
    mono=os.environ.get("STARFALL_MONO",shutil.which("mono"))
    assert mono, "HERO_CAPTURE_PROBE_REQUIRES_MONO"
    probe=ROOT/"tests/NativeHeroCaptureChecks.cs"
    exe=OUT/"native-hero-capture.exe"
    good_probe=subprocess.run(compiler()+[
        "-define:UNITY_5_3_OR_NEWER","-out:"+str(exe),
        str(STUB),str(GATE),str(FACTORY),str(CAPTURE),str(probe)],
        cwd=ROOT,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,text=True)
    assert good_probe.returncode==0,(
        "NATIVE_HERO_PROBE_COMPILE",good_probe.stdout)
    observed=subprocess.run([mono,str(exe)],cwd=ROOT,
        stdout=subprocess.PIPE,stderr=subprocess.STDOUT,text=True)
    assert observed.returncode==0,(
        "NATIVE_HERO_LIVE_CAPTURE_PROBE",observed.stdout)
    print(observed.stdout.strip())
    stamp='string captureKey = capturedSprite.GetInstanceID() +'
    assert original.count(stamp)==1,"HERO_CACHE_KEY_MISSING"
    # This reproduces the pre-fix shared framebuffer across all classes,
    # actions and facings. It must fail, not merely log a warning.
    bad_source=original.replace(stamp,
        'string captureKey = "one shared buffer" +',1).replace(
        '(flip ? ":left" : ":right");','(flip ? "" : "");',1)
    bad_path=OUT/"broken-shared-hero-capture.cs"
    bad_path.write_text(bad_source,encoding="utf-8")
    bad_exe=OUT/"broken-shared-hero-capture.exe"
    build_bad=subprocess.run(compiler()+[
        "-define:UNITY_5_3_OR_NEWER","-out:"+str(bad_exe),
        str(STUB),str(GATE),str(FACTORY),str(bad_path),str(probe)],
        cwd=ROOT,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,text=True)
    assert build_bad.returncode==0,(
        "NATIVE_HERO_FAULTED_BUILD",build_bad.stdout)
    rejected=subprocess.run([mono,str(bad_exe)],cwd=ROOT,
        stdout=subprocess.PIPE,stderr=subprocess.STDOUT,text=True)
    assert rejected.returncode!=0 and "HERO_CAPTURE_REGRESSION" in rejected.stdout,(
        "UNSAFE_CI_ACCEPTED_MUTABLE_EW_GPU_BUFFER",rejected.stdout)
    print("PASS injected shared left/right/action GPU buffer hard-FAIL")
    # Mutation: old fractional playback allows Unity's discrete Sprite curve
    # to drift away from the physical GameCore selected contact frame.
    selected="float normalized=(publishedFrame+.5f)/bakedFrames;"
    assert original.count(selected)==1, "HERO_NATIVE_INTEGER_FRAME_SAMPLE_MISSING"
    sample_old=original.replace(selected,
        "float normalized=(cellPosition+.20f)/bakedFrames;",1)
    wrong_path=OUT/"broken-hero-action-timing.cs"
    wrong_path.write_text(sample_old,encoding="utf-8")
    wrong_exe=OUT/"broken-hero-action-timing.exe"
    built_wrong=subprocess.run(compiler()+[
        "-define:UNITY_5_3_OR_NEWER","-out:"+str(wrong_exe),
        str(STUB),str(GATE),str(FACTORY),str(wrong_path),str(probe)],
        cwd=ROOT,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,text=True)
    assert built_wrong.returncode==0,(
        "HERO_ACTION_TIMING_MUTATION_INVALID",built_wrong.stdout)
    denied=subprocess.run([mono,str(wrong_exe)],cwd=ROOT,
        stdout=subprocess.PIPE,stderr=subprocess.STDOUT,text=True)
    assert denied.returncode!=0 and "HERO_CAPTURE_REGRESSION" in denied.stdout,(
        "UNSAFE_CI_ACCEPTED_FRACTIONAL_ANIMATOR_FRAME_DRIFT",denied.stdout)
    print("PASS fractional Animator sample mutation hard-FAIL")
    # The old global "last Sprite/flip" check repainted a previously
    # submitted RenderTexture whenever the hero returned to an earlier pose.
    # Mutation restores that bug: the real same-frame and next-frame
    # repaint probe must fail, not simply accept immutable cache keys.
    trigger="if (needsRender)"
    assert original.count(trigger)==1, "HERO_IMMUTABLE_TARGET_GUARD_MISSING"
    unstable=original.replace(trigger,
        "if (needsRender || heroCaptureTexture != null)",1)
    unstablePath=OUT/"broken-hero-repaint-cached-pose.cs"
    unstablePath.write_text(unstable,encoding="utf-8")
    unstableExe=OUT/"broken-hero-repaint-cached-pose.exe"
    unstableCompile=subprocess.run(compiler()+[
        "-define:UNITY_5_3_OR_NEWER","-out:"+str(unstableExe),
        str(STUB),str(GATE),str(FACTORY),str(unstablePath),str(probe)],
        cwd=ROOT,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,text=True)
    assert unstableCompile.returncode==0,(
        "HERO_REPAINT_MUTATION_BUILD_FAILED",unstableCompile.stdout)
    unstableResult=subprocess.run([mono,str(unstableExe)],
        cwd=ROOT,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,text=True)
    assert unstableResult.returncode!=0 and (
        "HERO_CAPTURE_REGRESSION" in unstableResult.stdout),(
        "HERO_MUTABLE_POSE_REPAINT_FALSE_PASS",unstableResult.stdout)
    print("PASS cached hero RenderTexture repaint mutation hard-FAIL")
    # A permanently disabled clip should fail the live native recovery test,
    # even if an apparently safe Sprite fallback keeps drawing a body.
    reenable="Time.frameCount < rejectedUntil"
    assert original.count(reenable)==1,"HERO_STALE_ANIMATOR_RECOVERY_MISSING"
    stuck=original.replace(reenable,"true",1)
    stuckPath=OUT/"broken-hero-animator-stuck.cs"
    stuckPath.write_text(stuck,encoding="utf-8")
    stuckExe=OUT/"broken-hero-animator-stuck.exe"
    builtStuck=subprocess.run(compiler()+[
        "-define:UNITY_5_3_OR_NEWER","-out:"+str(stuckExe),
        str(STUB),str(GATE),str(FACTORY),str(stuckPath),str(probe)],
        cwd=ROOT,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,text=True)
    assert builtStuck.returncode==0,(
        "HERO_STALE_ANIMATOR_MUTATION_INVALID",builtStuck.stdout)
    rejectedStuck=subprocess.run([mono,str(stuckExe)],cwd=ROOT,
        stdout=subprocess.PIPE,stderr=subprocess.STDOUT,text=True)
    assert rejectedStuck.returncode!=0 and "HERO_CAPTURE_REGRESSION" in rejectedStuck.stdout,(
        "UNSAFE_CI_ACCEPTED_PERMANENT_ANIMATOR_DISABLE",rejectedStuck.stdout)
    print("PASS permanently disabled Animator retry mutation hard-FAIL")
    # Mutate the new controller loader into the historical one-shot loading
    # behaviour: initial class selection still works, but a Controller that
    # appears after the first failed read can never recover. CI MUST reject.
    controllerCondition="Time.frameCount >= heroControllerRetryAt"
    assert original.count(controllerCondition)==1, (
        "MISSING_HERO_CONTROLLER_IMPORT_RETRY_GUARD")
    oneShot=original.replace(controllerCondition,
        "(Time.frameCount >= heroControllerRetryAt && heroControllerRetryAt == 0)",
        1)
    oneShotPath=OUT/"broken-hero-controller-one-shot.cs"
    oneShotPath.write_text(oneShot,encoding="utf-8")
    oneShotExe=OUT/"broken-hero-controller-one-shot.exe"
    builtOneShot=subprocess.run(compiler()+[
        "-define:UNITY_5_3_OR_NEWER","-out:"+str(oneShotExe),
        str(STUB),str(GATE),str(FACTORY),str(oneShotPath),str(probe)],
        cwd=ROOT,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,text=True)
    assert builtOneShot.returncode==0,(
        "HERO_CONTROLLER_RETRY_MUTATION_INVALID",builtOneShot.stdout)
    deniedOneShot=subprocess.run([mono,str(oneShotExe)],cwd=ROOT,
        stdout=subprocess.PIPE,stderr=subprocess.STDOUT,text=True)
    assert deniedOneShot.returncode!=0 and (
        "HERO_CAPTURE_REGRESSION" in deniedOneShot.stdout), (
        "UNSAFE_CI_ACCEPTED_UNRECOVERABLE_CONTROLLER_IMPORT",
        deniedOneShot.stdout)
    print("PASS one-shot native Controller import regression hard-FAIL")
    # A lost GPU RenderTexture may recover at runtime. Returning false
    # forever (old heroCaptureBroken latch) must fail executable C# tests.
    gpuGuard=("if(Time.frameCount < heroCaptureRetryAt &&\n"
              "                   Time.frameCount >= heroCaptureRetryAt-120)return false;")
    assert original.count(gpuGuard)==1, "HERO_GPU_RECOVERY_GUARD_MISSING"
    permanent=original.replace(gpuGuard,"if(true)return false;",1)
    permanentPath=OUT/"broken-permanent-hero-gpu.cs"
    permanentPath.write_text(permanent,encoding="utf-8")
    permanentExe=OUT/"broken-permanent-hero-gpu.exe"
    gpuCompile=subprocess.run(compiler()+[
        "-define:UNITY_5_3_OR_NEWER","-out:"+str(permanentExe),
        str(STUB),str(GATE),str(FACTORY),str(permanentPath),str(probe)],
        cwd=ROOT,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,text=True)
    assert gpuCompile.returncode==0,(
        "HERO_GPU_RECOVERY_MUTATION_INVALID",gpuCompile.stdout)
    gpuDenied=subprocess.run([mono,str(permanentExe)],cwd=ROOT,
        stdout=subprocess.PIPE,stderr=subprocess.STDOUT,text=True)
    assert gpuDenied.returncode!=0 and "HERO_CAPTURE_REGRESSION" in gpuDenied.stdout,(
        "UNSAFE_CI_ACCEPTED_PERMANENT_GPU_FALLBACK",gpuDenied.stdout)
    print("PASS permanent GPU fallback regression hard-FAIL")
    # Removing epoch invalidation must make the real gold-haired hero
    # recapture/51-pose memory test fail on a new Play session.
    for label,old,replacement in (
        ("frame-rewind","RefreshHeroCaptureEpoch();",""),
        ("same-id-texture-upload",
            "if(entry.sourceUpdateCount!=capturedSprite.texture.updateCount)",
            "if(false)"),
        ("pixel-reimport","if(previous!=null)","if(false)"),
        ("controller-hot-reimport","if(controller!=null)",
            "if(!heroProductionReady&&controller!=null)"),
        ("gpu-backlog","                Destroy(old);\n            }",
            "                Destroy(old);\n                break;\n            }"),
    ):
        assert original.count(old)==1,("HERO_VISUAL_MUTATION_SIGNAL",label)
        brokenSource=OUT/("broken-hero-"+label+".cs")
        brokenSource.write_text(original.replace(old,replacement,1),
            encoding="utf-8")
        brokenExe=OUT/("broken-hero-"+label+".exe")
        compiled=subprocess.run(compiler()+[
            "-define:UNITY_5_3_OR_NEWER","-out:"+str(brokenExe),
            str(STUB),str(GATE),str(FACTORY),str(brokenSource),str(probe)],
            cwd=ROOT,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,text=True)
        assert compiled.returncode==0,(
            "HERO_VISUAL_MUTATION_COMPILE",label,compiled.stdout)
        observed=subprocess.run([mono,str(brokenExe)],cwd=ROOT,
            stdout=subprocess.PIPE,stderr=subprocess.STDOUT,text=True)
        assert observed.returncode!=0 and (
            "HERO_CAPTURE_REGRESSION" in observed.stdout),(
            "HERO_VISUAL_MUTATION_FALSE_PASS",label,observed.stdout)
    print("PASS 5 hero GPU epoch, same-source pixels, import, Animator graph and cache-budget mutations hard-fail")
    print("PASS actual UNITY_5_3_OR_NEWER SpriteRenderer + canonical "
          "Sprite factory compiled in separate C# assembly")
    print("PASS fault-injected invalid SpriteRenderer API rejected by mcs")
    print("NATIVE_UNVERIFIED: Unity Editor, imported art, camera GPU and Animator "
          "remain unexecuted")


if __name__=="__main__":
    run()
