#!/usr/bin/env python3
"""Compile actual Frontier Editor menus/review/import code against offline stubs.

Unity's Animator/AssetDatabase builder and native PlayMode tests are deliberately
NOT compiled by these limited stubs; see assembly graph, source guards and
NATIVE_UNVERIFIED until a licensed Unity 2022.3.53f1 Editor runs.
"""
from pathlib import Path
import os
import shutil
import subprocess
import json

from run_core_tests import fixture

ROOT = Path(__file__).resolve().parents[1]


def main():
    mono = os.environ.get("STARFALL_MONO", shutil.which("mono"))
    mcs = os.environ.get("STARFALL_MCS", shutil.which("mcs"))
    if not mono or not mcs:
        raise SystemExit("Mono is required for the offline Editor compile check.")
    checks = ROOT / ".starfall-checks"
    checks.mkdir(parents=True, exist_ok=True)
    (checks / "CampaignFixture.cs").write_text(
        fixture(json.loads((ROOT / "Assets/Resources/Frontier/Campaign.json").read_text())),
        encoding="utf-8")
    # The real production Animator builder requires UnityEditor.Animations,
    # real sprite importers, and AssetDatabase. Never invent mock success for
    # it: isolate the Editor menu entrypoints and give them a compile-only
    # signature for that external Unity Editor dependency.
    (checks / "EditorAnimatorSignature.cs").write_text(
        "namespace Starfall.EditorTools { public static class "
        "StarfallCanonicalHeroProductionBuilder { "
        "public static void EnsureBuilt() {} } }\n", encoding="utf-8")
    sources = (
        sorted((ROOT / "Assets/Scripts/Frontier/Core").glob("*.cs"))
        + sorted((ROOT / "Assets/Scripts/Frontier/Runtime").glob("*.cs"))
        + sorted((ROOT / "Assets/Editor/Frontier").glob("*.cs"))
        + [
            ROOT / "tests/UnityLayoutStubs.cs",
            ROOT / "tests/UnityEditorSyntaxStubs.cs",
            ROOT / "tests/OfflineJson.cs",
            ROOT / "tests/RuntimePolishChecks.cs",
            ROOT / "tests/VisualRuntimeChecks.cs",
            ROOT / "tests/RenderStateChecks.cs",
            ROOT / "tests/RuntimeContinuityChecks.cs",
            ROOT / "tests/ActorRegistrationChecks.cs",
            checks / "CampaignFixture.cs",
            checks / "EditorAnimatorSignature.cs",
        ]
    )
    compiler = [mono, mcs] if mcs.endswith(".exe") else [mcs]
    common = compiler + ["-define:UNITY_EDITOR"] + [str(p) for p in sources]
    subprocess.run(common[:len(compiler)+1] +
                   ["-target:library", "-out:" + str(checks / "editor-syntax.dll")]
                   + common[len(compiler)+1:], check=True)
    print("PASS Frontier Editor menu/review/import C# compiled with offline declarations")
    exe = checks / "editor-import-checks.exe"
    subprocess.run(common[:len(compiler)+1] +
                   ["-main:EditorImportChecks", "-out:" + str(exe)]
                   + common[len(compiler)+1:] +
                   [str(ROOT / "tests/EditorImportChecks.cs")], check=True)
    subprocess.run([mono, str(exe)], check=True, cwd=ROOT)
    print("NATIVE_UNVERIFIED: real UnityEditor.Animations and PlayMode cannot be "
          "represented by these offline test doubles.")


if __name__ == "__main__":
    main()
