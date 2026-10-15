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
    print("PASS actual UNITY_5_3_OR_NEWER SpriteRenderer + canonical "
          "Sprite factory compiled in separate C# assembly")
    print("PASS fault-injected invalid SpriteRenderer API rejected by mcs")
    print("NATIVE_UNVERIFIED: Unity Editor, imported art, camera GPU and Animator "
          "remain unexecuted")


if __name__=="__main__":
    run()
