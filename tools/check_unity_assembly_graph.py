#!/usr/bin/env python3
"""Catch Unity asmdef compilation blockers hidden by the single-assembly Mono CI.

This is a deterministic compile-reference audit, not Unity Editor compilation.
It ensures that actual references in Editor/PlayMode tests are declared in their
assembly definition, rather than relying on mcs compiling every .cs together.
"""
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
CORE = "Assets/Scripts/Frontier/Core/Starfall.Core.asmdef"
RUNTIME = "Assets/Scripts/Frontier/Runtime/Starfall.Runtime.asmdef"
EDITOR_TESTS = "Assets/Tests/Frontier/Starfall.Tests.asmdef"
PLAY_TESTS = "Assets/Tests/PlayMode/Starfall.VisualTests.asmdef"


def load(path):
    return json.loads((ROOT / path).read_text(encoding="utf-8"))


def require_reference(owner, dependency, defs):
    assert dependency in defs[owner].get("references", []), (
        "UNITY_CS_ASMDEF_MISSING_REFERENCE", owner, dependency,
        "Unity would report CS0246 / CS0012 although flat Mono CI passes"
    )


def inspect(defs, files):
    assert len({d["name"] for d in defs.values()}) == len(defs), (
        "DUPLICATE_UNITY_ASSEMBLY_NAME",)
    for name, d in defs.items():
        for dependency in d.get("references", []):
            assert dependency in {q["name"] for q in defs.values()}, (
                "UNITY_UNRESOLVED_ASSEMBLY", name, dependency
            )
    assert defs[CORE]["name"] == "Starfall.Core"
    assert defs[CORE].get("noEngineReferences") is True
    assert defs[RUNTIME]["name"] == "Starfall.Runtime"
    assert defs[EDITOR_TESTS]["name"] == "Starfall.Tests"
    assert defs[PLAY_TESTS]["name"] == "Starfall.VisualTests"
    assert "Starfall.Core" in defs[RUNTIME].get("references", [])
    assert "Starfall.Core" in defs[EDITOR_TESTS].get("references", [])
    assert "Starfall.Core" in defs[PLAY_TESTS].get("references", [])
    assert "Starfall.Runtime" in defs[PLAY_TESTS].get("references", [])

    # The real Editor test source uses an actual runtime MonoBehaviour:
    # Unity compiles this as Starfall.Tests, a distinct C# assembly.
    assert re.search(r"\btypeof\s*\(\s*FrontierRuntime\s*\)", files["edit"]), (
        "KNOWN_UNITY_EDITOR_TEST_RUNTIME_USE_MISSING",)
    assert re.search(r"\bpublic\s+sealed\s+partial\s+class\s+FrontierRuntime", files["runtime"])
    require_reference(EDITOR_TESTS, "Starfall.Runtime", defs)
    assert "Editor" in defs[EDITOR_TESTS].get("includePlatforms", []), (
        "UNITY_EDITOR_NUNIT_TESTS_IN_PLAYER_BUILD",)
    assert "TestAssemblies" in defs[EDITOR_TESTS].get("optionalUnityReferences", [])
    assert "TestAssemblies" in defs[PLAY_TESTS].get("optionalUnityReferences", [])

    # Editor's test-only classes must not leak into the Player assembly
    # even when the Test Runner package is installed.
    assert files["edit"].lstrip().startswith("#if UNITY_EDITOR")
    assert files["edit"].rstrip().endswith("#endif")
    assert files["play"].lstrip().startswith("#if UNITY_INCLUDE_TESTS")
    assert files["play"].rstrip().endswith("#endif")

    # The scene is bootstrapped from the native runtime, not a preview-only
    # object; a missing dependency here can prevent Unity PlayMode boot.
    assert 'if (scene.name != "Frontier"' in files["boot"]
    assert 'new GameObject("Starfall Frontier Runtime").AddComponent<FrontierRuntime>()' in files["boot"]
    assert "m_EditorVersion: 2022.3.53f1" in files["version"]


def main():
    defs = {key: load(key) for key in (CORE, RUNTIME, EDITOR_TESTS, PLAY_TESTS)}
    paths = {
        "edit": "Assets/Tests/Frontier/UnityCoreTests.cs",
        "play": "Assets/Tests/PlayMode/VisualPlayModeTests.cs",
        "runtime": "Assets/Scripts/Frontier/Runtime/FrontierRuntime.cs",
        "boot": "Assets/Scripts/Frontier/Runtime/FrontierRuntime.cs",
        "version": "ProjectSettings/ProjectVersion.txt",
    }
    files = {key: (ROOT / value).read_text(encoding="utf-8") for key, value in paths.items()}
    inspect(defs, files)

    # Intentional broken Unity assembly graph must FAIL, rather than merely
    # writing a warning and letting a good gameplay test mask the CS0246.
    broken = {key: dict(value) for key, value in defs.items()}
    broken[EDITOR_TESTS] = dict(broken[EDITOR_TESTS])
    broken[EDITOR_TESTS]["references"] = ["Starfall.Core"]
    try:
        inspect(broken, files)
    except AssertionError as error:
        assert "UNITY_CS_ASMDEF_MISSING_REFERENCE" in str(error), error
    else:
        raise AssertionError("UNSAFE_CI: missing Starfall.Runtime still compiles")

    print("PASS: Unity Editor test -> Starfall.Runtime -> Starfall.Core assembly graph")
    print("PASS: intentional missing-asmdef-reference negative control rejected")
    print("NATIVE_UNVERIFIED: an actual Unity Editor compile is still required")


if __name__ == "__main__":
    main()
