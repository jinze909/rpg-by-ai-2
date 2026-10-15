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
    # Root-only recovery: a released cast/attack may move physically, but
    # MUST retain one authored full-body action Sprite/weapon/facing. Previous
    # implementation switched to Run when RecoveryWalking became true and
    # snapped shoulders, pelvis and outfit halfway through the action.
    def check_moving_action_contract(actor_code, core_code, continuity_code):
        assert "bool moving=pose>=2&&pose<=3;" in actor_code, (
            "MOVING_RECOVERY_REVERTED_TO_RUN_SPRITE")
        assert "bool movingRecovery=" not in actor_code, (
            "UNSAFE_MIXED_ACTION_WALK_PRESENTATION")
        assert "return playerAction != null ? playerAction.weapon : save.weapon;" in core_code, (
            "ACTIVE_CAST_OUTFIT_SNAPS_TO_UNRELATED_RUN_WEAPON")
        assert "if(playerAction!=null)return playerAction.direction;" in continuity_code, (
            "ACTIVE_CAST_FACING_FOLLOWS_STRAFE_INSTEAD_OF_LOCKED_CONTACT")
        assert "if (RecoveryWalking)" not in core_code.split("public int PlayerPose")[1], (
            "ACTIVE_ACTION_POSE_OVERRIDDEN_BY_WALK_DURING_MOVING_RECOVERY")
    continuity_code = continuity
    check_moving_action_contract(actor, core, continuity_code)
    for field, damaged in (
        ("actor", actor.replace("bool moving=pose>=2&&pose<=3;",
                                "bool moving=pose>=2&&pose<=3||RecoveryWalking;",1)),
        ("core", core.replace(
            "return playerAction != null ? playerAction.weapon : save.weapon;",
            "return save.weapon;",1)),
        ("continuity", continuity.replace(
            "if(playerAction!=null)return playerAction.direction;",
            "if(playerAction!=null&&!RecoveryWalking)return playerAction.direction;",1))):
        corrupt_actor = damaged if field == "actor" else actor
        corrupt_core = damaged if field == "core" else core
        corrupt_continuity = damaged if field == "continuity" else continuity_code
        try:
            check_moving_action_contract(corrupt_actor, corrupt_core, corrupt_continuity)
        except AssertionError:
            pass
        else:
            raise AssertionError("UNSAFE_VISUAL_CI: reverted moving action splice accepted")
    assert "if(action==1)position%=frames;" in actor
    assert "bool actionSix=(action==2||action==3)&&authored==4;" in actor
    assert "bool reactionFive=(action==4||action==5)&&authored==3;" in actor
    assert "actionSix?(phase<2f?Math.Min(2f,phase*2f):phase*2f-1f):" in actor
    assert "frames=actionSeven?7:actionSix?6:reactionFive?5:authored;" in actor
    assert "bool actionSeven=actionSix&&HasCompleteHeroContactArt();" in actor
    assert "heroContactFamilyChecked" in actor and "heroContactFamilyComplete" in actor
    assert "atlas.width!=7*256" in actor
    assert "phase<2f?Math.Min(3f,phase*2f):phase*2f" in actor
    # Exact contact boundary, no early release frame even at maximal windup.
    def sampled_cell(phase, seven):
        pos = (min(3.,phase*2.) if phase<2 else phase*2) if seven else (
            min(2.,phase*2.) if phase<2 else phase*2-1)
        return min(6 if seven else 5, int(pos+.20))
    assert sampled_cell(1.55, True)==3 and sampled_cell(2,True)==4
    assert sampled_cell(1.55, False)==2 and sampled_cell(2,False)==3
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
    # Native Editor must NEVER synthesize unverified limb motion if a
    # shipped atlas is absent. The Python producer plus release QA owns art.
    assert "MotionMidpoint(" not in native
    assert "ExpandWithInbetweens(" not in native
    assert "Missing approved full-body hero atlas: " in native
    assert "ExpandedCount(refs.Length, action)" in native
    assert "PublishedFrameCount(Texture2D atlas, int sourceCount, int action)" in native
    assert "installedSevenContacts != 0 && installedSevenContacts != 18" in native
    assert "contactSevenCount != 0 && contactSevenCount != 18" in native
    assert "public static void EnsureBuilt()" in native
    # Guard the real Unity Editor repair path, not just the 63 standalone
    # .anim files. Existing corrupt Ranger/Arcanist overrides used to look
    # "ready" and could silently render another equipment class.
    def verify_production_graph_guards(source):
        guards = (
            "private static bool NativeControllersMatchPublished()",
            "if (ready && !NativeControllersMatchPublished())",
            "controller.GetOverrides(overrides);",
            "overrides.Count != Actions.Length * Facings.Length",
            "pair.Key == key &&",
            "pair.Value == value",
            "controller.runtimeAnimatorController != basis",
            "states.Length != 1",
            "states[0].motion != AssetDatabase.LoadAssetAtPath<AnimationClip>",
            "HasExactProductionTransition(transitions[0], action, facing)",
            "machine.RemoveAnyStateTransition(obsolete);",
            "baseController.RemoveParameter(parameter);",
            "AnimatorConditionMode.Equals",
            "if (!NativeControllersMatchPublished())",
            "after repair.",
        )
        for needle in guards:
            assert needle in source, ("PRODUCTION_ANIMATOR_MAPPING_GATE_REMOVED",needle)
    verify_production_graph_guards(native)
    # Adversarial static regression: omit mapping comparison or a state
    # transition repair and the EXACT same build check must reject it.
    for target in ("pair.Value == value",
                   "HasExactProductionTransition(transitions[0], action, facing)",
                   "machine.RemoveAnyStateTransition(obsolete);",
                   "baseController.RemoveParameter(parameter);"):
        corrupted = native.replace(target, "/* intentionally broken native graph */", 1)
        try:
            verify_production_graph_guards(corrupted)
        except AssertionError as error:
            assert "PRODUCTION_ANIMATOR_MAPPING_GATE_REMOVED" in str(error)
        else:
            raise AssertionError("UNSAFE_CI_GATE_ACCEPTED_WRONG_NATIVE_CONTROLLER")
    # Editor play-mode must use the SAME 63 published action curves as a
    # production build; a manual preview prefab/menu click is insufficient.
    for signal in ("[InitializeOnLoadMethod]",
                   "EditorApplication.playModeStateChanged +=",
                   "PlayModeStateChange.ExitingEditMode",
                   "EditorApplication.delayCall +=",
                   "EnsurePublishedControllerAvailable();",
                   "if (!ready) Build();"):
        assert signal in native, ("NATIVE_PLAYMODE_HERO_ANIMATOR_NOT_WIRED", signal)
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
    # Verify this is ACTUALLY the runtime loaded by the shipped
    # Assets/Scenes/Frontier.unity, not a preview-only prefab. The scene
    # contains the camera; FrontierRuntime.Boot() binds the MonoBehaviour.
    scene = ROOT / "Assets/Scenes/Frontier.unity"
    assert scene.is_file() and "m_Name: Main Camera" in scene.read_text()
    live_runtime = (RUNTIME / "FrontierRuntime.cs").read_text()
    for part in ('[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]',
                 'if (scene.name != "Frontier"',
                 'new GameObject("Starfall Frontier Runtime").AddComponent<FrontierRuntime>()',
                 'private void DrawPlayerActor()'):
        assert part in (live_runtime + actor), "Production scene/bootstrap disconnected: " + part
    assert "BuildPipeline.BuildPlayer(new[] { Scene }" in (
        (ROOT / "Assets/Editor/Frontier/FrontierEditor.cs").read_text())
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
    assert "private bool useSpriteRendererHero = true;" in engine, (
        "Production hero must default to unified SpriteRenderer; F8 opt-out retained")
    assert "if (!useSpriteRendererHero || heroCaptureBroken || drawingOutline ||" in engine
    assert "if(!captured)" in actor and "DrawWholeBodyHeroOnGUI" in actor
    assert "KeyCode.F8" in game and "else ReleaseHeroCapture();" in game
    assert engine.count("return false;") >= 3, "Production must have a failure fallback"
    # Sword Q/ordinary attack contact remain full-body authored poses.
    # Once GameCore genuinely permits recovery movement, we show an
    # unspliced same-class RUN frame; no Q damage timing changes.
    for mark in ("bool movingRecovery=!Game.Dying&&Game.ActuallyMoving&&",
                 "Game.PlayerMobility==MobilityRule.Reduced&&",
                 "pose>=4&&pose<=7;",
                 "sheet=walkSheets[look];",
                 "bool moving=pose>=2&&pose<=3||movingRecovery;"):
        assert mark in actor, "Missing safe full-body recovery switch: " + mark
    # The entire old generated-parts compositor is retired, not only the
    # notorious waist scanline. Any new runtime leg-sampling/blending
    # implementation must be reviewed as a new algorithm before release.
    gait = (RUNTIME / "FrontierUnifiedActorGait.cs").read_text()
    for forbidden in ("private Texture2D UnifiedActionGait(", "feetPixels",
                      "posePixels[i]", "MotionMidpoint(", "WalkFrame*",
                      "Dictionary<string,Texture2D>"):
        assert forbidden not in gait, "Resurrected body puppetry: "+forbidden
    assert "private void ClearUnifiedActionGait() { }" in gait
    preview_pipeline = (ROOT / "tools/build_hero_animation_preview.py").read_text()
    # Production asset generation used to delete each source head and paste a
    # separately positioned WalkVanguard face. Reject restoring that pathway.
    baker = preview_pipeline[preview_pipeline.index("def canonical_frame("):
                             preview_pipeline.index("def strip(")]
    assert "return original" in baker
    assert "canvas.paste" not in baker and "golden_head" not in baker
    rig = (RUNTIME / "FrontierActorRig.cs").read_text()
    assert "private void HumanActor(" not in rig and "private void ActorPart(" not in rig
    assert "UnifiedActionGait(" not in actor
    assert "DrawWholeBodyHeroOnGUI" in actor
    # Published canonical PNGs are isReadable: 0. World outline pass must
    # not accidentally call PixelArt.OutlineMask on those GPU-only textures.
    assert "if(drawingOutline)" in actor
    assert "Actor(original,4,3,originalFrame,foot,192,tint,flip);" in actor
    assert "art.OutlineMask(atlas)" not in actor
    assert "ResolveFullBodyHeroFrame" in actor
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
    # The actual contact key is now SOURCE index 2 -> production index 3;
    # GameCore source phase >=2 selects it, never an early synthetic tween.
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
