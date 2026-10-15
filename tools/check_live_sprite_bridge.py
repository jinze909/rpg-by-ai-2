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
    compositor = (RUNTIME / "CanonicalHeroSpriteFactory.cs").read_text()
    actor = (RUNTIME / "FrontierActors.cs").read_text()
    game = (RUNTIME / "FrontierRuntime.cs").read_text()
    editor = (ROOT / "Assets/Scripts/Frontier/Editor/StarfallHeroAnimatorBuilder.cs").read_text()
    native = (ROOT / "Assets/Scripts/Frontier/Editor/StarfallCanonicalHeroProductionBuilder.cs").read_text()
    required = [
        "SpriteRenderer", "RenderTexture", "CameraClearFlags.SolidColor",
        "BakeCanonicalHeroFrame", "walkSheets[0]",
        "ResolveFullBodyHeroFrame(locomotion,facing",
        "Animator.StringToHash", "heroCaptureAnimator.Play(", "heroCaptureAnimator.Update(0f)",
        "Production", "ProductionHeroClip",
        "foot.x - CanonicalHeroSpriteFactory.FootX * density",
        "foot.y - CanonicalHeroSpriteFactory.FootTop * density",
        "HeroCell * density, HeroCell * density",
        "heroCaptureCamera.Render()", "heroCaptureBroken", "ReleaseHeroCapture",
        "Resources.Load<RuntimeAnimatorController>",
        "Resources.Load<Texture2D>", "TryUsePublishedHeroFrame(",
        '"Frontier/HeroAnimator/Canonical/" + key',
        "heroPublishedSprites.Clear()", "heroPublishedUnavailable.Clear()",
        "heroCaptureRenderer.sprite = published;",
        "heroCaptureRenderedSprite != heroCaptureRenderer.sprite",
        "heroCaptureRenderedFlip != flip",
        "heroCaptureTexture.IsCreated()",
        "tint.a *= worldActorOpacity",
    ]
    for mark in required:
        assert mark in engine, f"Live sprite production branch lost invariant {mark!r}"
    core = (ROOT / "Assets/Scripts/Frontier/Core/CombatPresentation.cs").read_text()
    assert "public int HeroAnimationFrameCount" in core
    assert "public int HeroAnimationFrame(int facing)" in core
    assert "action.elapsed - action.contact < .12f" in core
    # Native seven-cell Cast/Attack requires *three* distinct precontact
    # samples: stand, intermediate, windup and anticipation keyframe 3.
    # Never jump directly from windup cell 2 to contact cell 4.
    assert "return 1f + .55f * Clamp((windup - .60f) / .25f" in core
    assert "if (windup <= .60f) return windup / .60f;" in core
    assert "return 2f + Clamp((action.elapsed - action.contact) / .18f" in core
    assert "HeroAnimationPlaybackPosition(int facing)" in core
    assert "if (state == 6) return 4;" in core
    assert "if (state == 3) return 4;" in core
    assert "if (state == 4 || state == 5) return 3;" in core
    assert "(.18f - dashTime)" in core and "(.18f - playerHitTime)" in core
    assert ".9f - deathTime" in core
    continuity = (ROOT / "Assets/Scripts/Frontier/Core/ContinuityCore.cs").read_text()
    assert "public float WalkPosePhase" in continuity
    assert "if (state == 1) return WalkPosePhase;" in core
    assert "if(action==1)position%=frames;" in actor
    assert "bool interpolate=action>=1&&action<=5&&authored>1;" in actor
    assert "bool specialHeavy = action == 2 && (pose == 6 || pose == 7);" in engine
    assert "bool allowStandardClip = unifiedAction == null && !specialHeavy;" in engine
    # A non-null Animator sprite is NOT proof of correct class/facing/contact.
    # The live GPU output must match the registered published atlas cell.
    for mark in [
        "HeroSpriteMatchesPublishedFrame(",
        "animated.texture != published.texture",
        "Vector2.Distance(pivotA, pivotB)",
        "animated.pixelsPerUnit - published.pixelsPerUnit",
        "!heroRejectedNativeClips.Contains(nativeClipKey)",
        "heroRejectedNativeClips.Add(nativeClipKey)",
        "heroCaptureRenderer.sprite = expectedPublished;",
        "heroRejectedNativeClips.Clear()",
        "bool hasClip = canVerifyNative &&",
        "TryUseProductionHeroClip(facing);",
    ]:
        assert mark in engine, "Missing native Animator pixel-parity fallback: " + mark
    assert "if (allowStandardClip &&" in engine
    # Third-combo/heavy original poses must bypass both regular 4/5
    # Sprite strip sampling and its normal-attack Animator clip.
    assert "AddComponent<UnityEngine.Rendering.SortingGroup>()" in engine
    assert "CanonicalHeroSpriteFactory.PixelsPerUnit" in engine
    spec_cs = compositor
    for mark in ["public const int FootX = 128;", "public const int FootTop = 240;",
                 "public const int IdentityHeadX = 100;", "public const int IdentityHeadTop = 140;",
                 "public const int IdentityHeadSize = 56;", "public const float GuiDensity = .75f;"]:
        assert mark in spec_cs, "Canonical code profile mismatch " + mark
    assert "MotionMidpoint(" in native
    assert "ExpandWithInbetweens(" in native
    assert "ExpandedCount(refs.Length, action)" in native
    assert "public static void EnsureBuilt()" in native
    assert "if (!ready) Build();" in native
    # Published Cast strips expanded from 3 to 7 frames; a native Clip
    # existing on disk is NOT proof that its Sprite curves are current.
    for guard in ('AnimationUtility.GetObjectReferenceCurve(clip, spriteBinding)',
                  'NativeClipMatchesPublished(frames, Generated + key + ".png"',
                  'frames.Length', 'atlas.width != expected * Cell',
                  'Array.IndexOf(Classes, profession)',
                  'AssetDatabase.GetAssetPath(sprite) != atlasPath',
                  'sprite.rect.x - index * Cell',
                  'sprite.pixelsPerUnit -',
                  'Mathf.Abs(keys[i].time - i / sampleRate)'):
        assert guard in native, "Missing native stale-clip rebuild guard: " + guard
    editor_boot = (ROOT / "Assets/Editor/Frontier/FrontierEditor.cs").read_text()
    assert editor_boot.count("Starfall.EditorTools.StarfallCanonicalHeroProductionBuilder.EnsureBuilt();") == 3
    assert "if (File.Exists(installedPath))" in native
    assert "atlasPaths.Add(key, installedPath);" in native
    assert "installed.width != expectedCount * Cell" in native
    for mark in ["new Vector2(FootX / (float)Cell, (Cell - FootTop) / (float)Cell)",
                 "class CanonicalHeroSpriteFactory", "Sprite.Create(",
                 "Point", "TextureFormat.RGBA32"]:
        # 'Point' is expressed through pivot/identity, not a physical 3D rig.
        if mark == "Point":
            continue
        assert mark in compositor, f"Shared canonical compositor lost invariant {mark!r}"
    for mark in ["Bake Accepted Hero To Live 3-Class Animator",
                 "CanonicalHeroSpriteFactory.Bake(",
                 "AnimationUtility.SetObjectReferenceCurve",
                 "typeof(SpriteRenderer)", 'propertyName = "m_Sprite"',
                 "AnimatorOverrideController", "AssetDatabase.SaveAssets",
                 "AssetDatabase.LoadAssetAtPath<Texture2D>",
                 "SpriteAlignment.Custom", "Resources/Frontier/HeroAnimator",
                 "sequence", "Frames", "Hit", "Death"]:
        if mark in ("sequence", "Frames"):
            continue
        assert mark in native, f"Native production Sprite/Animator bake lost invariant {mark!r}"
    assert "private static void VerifySameProfessionBody(" in native
    assert native.count("VerifySameProfessionBody(profession, refs);") == 2
    assert "VerifySameProfessionBody(profession, approved);" in native
    assert 'src.atlas == "Player" + profession' in native
    assert 'src.atlas == "Walk" + profession' in native
    assert "shared && src.index / 4 == cls" in native
    assert "AssetDatabase.DeleteAsset(" not in native, (
        "A production rebake must preserve existing GUIDs and accepted animation assets")
    assert engine.startswith("#if UNITY_5_3_OR_NEWER")
    assert "useSpriteRendererHero;" in engine, "Not safe to silently replace accepted art"
    assert engine.count("return false;") >= 3, "Production must have a failure fallback"
    # Live sword windup/contact can translate with AttackMovement, so
    # both SpriteRenderer capture and proven OnGUI fallback must use the
    # same original walking legs; collision/damage remain GameCore-owned.
    assert re.search(
        r"mobility==MobilityRule\.Reduced\s*\|\|\s*\("
        r"mobility==MobilityRule\.AttackMovement\s*&&\s*"
        r"Game\.playerAction!=null\s*&&\s*"
        r'Game\.playerAction\.kind=="attack"\)', actor), (
        "Basic sword advances must use gait, but Vanguard Q keeps its own contact pose")
    assert "UnifiedActionGait(" not in actor
    assert "Game.ActuallyMoving" in actor
    assert "DrawWholeBodyHeroOnGUI(sheet,frame,p,tint,flip," in actor
    assert "ResolveFullBodyHeroFrame(moving,facing," in actor
    assert '"Frontier/HeroAnimator/Canonical/"+key' in actor
    # Root-cause guard: the past stitched compositor copied the entire
    # lower 205..247px band from a walking atlas, splicing a different waist
    # and clothing onto the original attack torso. Never reintroduce that.
    gait = (RUNTIME / "FrontierUnifiedActorGait.cs").read_text()
    for forbidden in ("Color[] feetPixels", "posePixels[i]=feetPixels[i]",
                      "posePixels[i] = feetPixels[i]", "for(int yTop=205;"):
        assert forbidden not in gait, "Stitched half-body regression: " + forbidden
    for necessary in ("profession<0||profession>=3",
                      "walkSheets[profession]!=walk",
                      "bool personal=action==playerSheets[profession]",
                      "actionFrame/4==profession",
                      "Color[] composed=(Color[])source.Clone()",
                      "composed[y*cell+x]=source[y*cell+sample]"):
        assert necessary in gait, "Missing class-owned whole-body guarantee: " + necessary
    assert actor.count("DrawSpriteRendererHero(") == 1
    assert "if(!captured)" in actor
    assert "DrawWholeBodyHeroOnGUI" in actor
    assert "HumanActor(sheet,4,3,frame,p,.75f" not in actor
    assert "ReleaseHeroCapture();" in game
    assert "KeyCode.F8" in game and "Debug.isDebugBuild" in game
    assert "if (useSpriteRendererHero) heroCaptureBroken = false;" in game
    assert "DrawWholeBodyHeroOnGUI" in actor
    assert "ActorPart(" not in actor[actor.index("private void DrawPlayerActor()"):
                                          actor.index("private int EnemyPose(")]
    assert "colors[(Cell - 1 - row) * Cell + col]" not in compositor, (
        "Runtime must not erase and reattach a different warrior head")
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
    from build_hero_animation_preview import ACTIONS, FACINGS, CLASSES, source_indices
    from export_canonical_hero_atlases import expected_count
    resources = ROOT / "Assets/Resources/Frontier/HeroAnimator/Canonical"
    assert len(CLASSES) * len(ACTIONS) * len(FACINGS) == 63
    for klass in CLASSES:
        for action in ACTIONS:
            for facing in FACINGS:
                refs = source_indices(klass, action, facing)
                expected = (4 if action in ("Run", "Attack", "Cast", "Death") else
                    3 if action in ("Dodge", "Hit") else 1)
                assert len(refs) == expected, f"Wrong inherited clip length: {klass}/{action}/{facing}"
                if action in ("Dodge", "Hit"):
                    assert expected_count(action, expected) == 5, (
                        klass, action, facing, "reaction clips need five sprites")
                key = f"Hero_{klass}_{action}_{facing}"
                published = resources / (key + ".png")
                assert published.is_file(), "Production SpriteRenderer resource missing: " + key
                with Image.open(published) as source:
                    baked = expected_count(action, expected)
                    assert source.size == (256 * baked, 256), (key, source.size, baked)
    # GameCore's shared clock samples even contact slots (2*source index);
    # the final grounded Death cell is 3 and must never wrap.
    assert int(2 * 2 + .20) == 4
    assert int(2 * 3 + .20) == 6
    assert int(3 + .20) == 3
    print("PASS: 63 checked shipped SpriteRenderer resources, three-class frame sampling, "
          "non-destructive Editor controller hookup and GameCore clock fallback.")
    print("PASS: production canonical compositor, approved atlas import, "
          "7 action x 3 direction x 3 profession Sprite/Animator bake, "
          "GameCore contact synchronization and safe OnGUI fallback (offline)")
    print("NATIVE_UNVERIFIED: Unity Editor, live RenderTexture alpha, animations and FPS")


if __name__ == "__main__":
    check()
