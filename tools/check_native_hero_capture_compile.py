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
        str(FACTORY),
        str(source),
    ]
    return subprocess.run(cmd,cwd=ROOT,stdout=subprocess.PIPE,
                          stderr=subprocess.STDOUT,text=True)


def run():
    OUT.mkdir(parents=True,exist_ok=True)
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
        str(STUB),str(FACTORY),str(CAPTURE),str(probe)],
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
        str(STUB),str(FACTORY),str(bad_path),str(probe)],
        cwd=ROOT,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,text=True)
    assert build_bad.returncode==0,(
        "NATIVE_HERO_FAULTED_BUILD",build_bad.stdout)
    rejected=subprocess.run([mono,str(bad_exe)],cwd=ROOT,
        stdout=subprocess.PIPE,stderr=subprocess.STDOUT,text=True)
    assert rejected.returncode!=0 and "HERO_CAPTURE_REGRESSION" in rejected.stdout,(
        "UNSAFE_CI_ACCEPTED_MUTABLE_EW_GPU_BUFFER",rejected.stdout)
    print("PASS injected shared left/right/action GPU buffer hard-FAIL")
    print("PASS actual UNITY_5_3_OR_NEWER SpriteRenderer + canonical "
          "Sprite factory compiled in separate C# assembly")
    print("PASS fault-injected invalid SpriteRenderer API rejected by mcs")
    print("NATIVE_UNVERIFIED: Unity Editor, imported art, camera GPU and Animator "
          "remain unexecuted")


if __name__=="__main__":
    run()
