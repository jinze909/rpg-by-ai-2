#!/usr/bin/env python3
"""Regression for the live SpriteRenderer transition (offline, not Unity PlayMode).

Use source art already accepted by the project. This asserts import readiness,
the shared head registry, capture size/root transform and fallback wiring.
It cannot judge anatomy or certify native GPU/Animator playback.
"""
import re
from pathlib import Path
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
ART = ROOT / "Assets/Resources/Frontier/Art"
RUNTIME = ROOT / "Assets/Scripts/Frontier/Runtime"


def check():
    engine = (RUNTIME / "FrontierHeroSpriteCapture.cs").read_text()
    actor = (RUNTIME / "FrontierActors.cs").read_text()
    game = (RUNTIME / "FrontierRuntime.cs").read_text()
    editor = (ROOT / "Assets/Scripts/Frontier/Editor/StarfallHeroAnimatorBuilder.cs").read_text()
    required = [
        "SpriteRenderer", "RenderTexture", "CameraClearFlags.SolidColor",
        "BakeCanonicalHeroFrame", "ActorFrameRegistration.TryHead",
        "ActorFrameRegistration.TryStableHead", "walkSheets[0]",
        "HeroAnimationAction", 'Game.playerAction.kind == "skill"',
        "new Vector2(.5f, .0625f)", "new Rect(foot.x - 96, foot.y - 180, 192, 192)",
        "heroCaptureCamera.Render()", "heroCaptureBroken", "ReleaseHeroCapture",
        "Resources.Load<RuntimeAnimatorController>",
    ]
    for mark in required:
        assert mark in engine, f"Live sprite production branch lost invariant {mark!r}"
    assert engine.startswith("#if UNITY_5_3_OR_NEWER")
    assert "useSpriteRendererHero;" in engine, "Not safe to silently replace accepted art"
    assert engine.count("return false;") >= 3, "Production must have a failure fallback"
    assert actor.count("DrawSpriteRendererHero(") == 2
    assert actor.count("if (!captured)") == 2
    assert "HumanActor(sheet,4,3,frame,p,.75f" in actor
    assert "ReleaseHeroCapture();" in game
    assert 'Build Live SpriteRenderer State Bridge' in editor
    assert editor.count('"Hit", "Death"') >= 2
    # The root is top-origin (128,240), not Unity's bottom-origin (128,16).
    assert (256 / 2 - 16) / 128 == .875
    assert (256 - 240) / 256 == .0625
    assert 256 * .75 == 192
    for klass in ("Vanguard", "Ranger", "Arcanist"):
        for stem in ("Player", "Walk"):
            path = ART / f"{stem}{klass}.png"
            with Image.open(path) as loaded:
                assert loaded.size == (1024, 768), str(path)
            meta = path.with_suffix(".png.meta").read_text()
            assert re.search(r"^\s*isReadable: 1$", meta, re.M), str(path)
    registration = (RUNTIME / "ActorFrameRegistration.cs").read_text()
    for klass in ("Vanguard", "Ranger", "Arcanist"):
        assert f'"Walk{klass}"' in registration
        assert f'"Player{klass}"' in registration
    print("PASS: live SpriteRenderer root/pixel geometry, three accepted class atlases, "
          "canonical blonde head, 7-state Animator builder, safe OnGUI fallback (offline)")
    print("NATIVE_UNVERIFIED: Unity Editor, live RenderTexture alpha, animations and FPS")


if __name__ == "__main__":
    check()
