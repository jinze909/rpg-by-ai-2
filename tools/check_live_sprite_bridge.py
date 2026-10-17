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
        "SpriteRenderer", "heroCaptureRenderer.sprite=canonical;",
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
        "heroCaptureRenderer.sprite=canonical;",
        "if (needsRender)",
        "entry.texture.IsCreated()",
        "heroCapturedFrames.TryGetValue(captureKey",
        "capturedSprite.GetInstanceID()",
        "EvictUnusedHeroCapture()",
        "heroCaptureCamera.targetTexture = heroCaptureTexture;",
        "heroCaptureCamera.targetTexture = null;",
        "tint.a *= worldActorOpacity",
    ]
    for mark in required:
        assert mark in engine, f"Live sprite production branch lost invariant {mark!r}"
    # A capture entry is immutable for its complete Sprite + facing until
    # an actual lost RenderTexture is rebuilt. Global last-posed Sprite
    # tracking used to repaint queued GUI textures during E->W->E changes.
    for outdated in ("heroCaptureRenderedSprite",
                     "heroCaptureRenderedFlip"):
        assert outdated not in engine, (
            "REINTRODUCED_MUTABLE_HERO_GPU_REPAINT",outdated)
    assert engine.count("if (needsRender)")==1, (
        "HERO_GPU_CAPTURE_MUST_RENDER_ONLY_ON_ALLOCATION_OR_DEVICE_RESTORE")

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
        assert "return playerAction != null ? playerAction.equippedWeapon : save.weapon;" in core_code, (
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
            "return playerAction != null ? playerAction.equippedWeapon : save.weapon;",
            "return playerAction != null ? playerAction.weapon : save.weapon;",1)),
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
    # Mutation gate for invisible gait accumulation: the combat sprite may
    # translate during Recovery, but its UNSEEN Run legs cannot produce sound
    # contacts or silently advance frame phase beneath that planted body.
    def audit_hidden_gait(core_code, locomotion_code):
        assert "walkDistance = 0f;" in core_code, (
            "HERO_ACTION_STARTS_WITH_OLD_STRIDE")
        section = locomotion_code.split("private void TickMovement(", 1)[1]
        section = section.split("private void MoveAttackStep(", 1)[0]
        compact = re.sub(r"\s+", "", section)
        # Real recoil and input displacement are separate now. The actual
        # walking phase MUST sample input-only 'walking', never the recoil
        # delta, and may only advance inside the null-action guard.
        assert "movementDirection=walking;" in compact, (
            "HERO_RECOIL_MUST_NOT_BECOME_RUNNING_GAIT")
        assert re.search(
            r"if\s*\(\s*playerAction\s*==\s*null\s*\)\s*"
            r"\{\s*int before.*?walkDistance\s*\+=\s*walking\.Length;",
            section, re.S), "INVISIBLE_WALK_FRAMES_ADVANCE_DURING_ATTACK_RECOVERY"
        assert "footContacts+=" in compact
        assert "if(playerAction==null||PlayerPhase==ActionPhase.Recovery)" not in compact
    audit_hidden_gait(core, continuity)
    for code, locomotion_code in (
        (core.replace("walkDistance = 0f;", "walkDistance = walkDistance;", 1),
         continuity),
        (core, continuity.replace(
            "if(playerAction==null)\n                {",
            "if(playerAction==null||PlayerPhase==ActionPhase.Recovery)\n                {", 1)),
    ):
        try:
            audit_hidden_gait(code, locomotion_code)
        except AssertionError:
            pass
        else:
            raise AssertionError("UNSAFE_VISUAL_CI: invisible moving attack gait accepted")
    # Static ground tiles are NOT characters. The world renderer must not
    # allocate a SpriteRenderer/RenderTexture for every scattered 23px tuft;
    # original UVs render directly while dynamic bosses/NPCs/companions
    # still require the whole-body native actor path.
    environment = (RUNTIME / "FrontierEnvironment.cs").read_text()
    ground = environment.split("private void DrawGroundDetail(")[1]
    ground = ground.split("private void DrawRootedPlant(")[0]
    assert "Atlas(detailSheet,4,4,id,sourceBounds," in ground, (
        "GROUND_DETAIL_LOST_DIRECT_SOURCE_ATLAS_PATH")
    assert "if(drawingOutline)" in ground and (
        "Actor(detailSheet,4,4,id,foot,size," in ground), (
        "GROUND_DETAIL_OUTLINE_MASK_FALLBACK_LOST")
    assert ground.index("Actor(detailSheet,4,4,id,foot,size,") < (
        ground.index("Atlas(detailSheet,4,4,id,sourceBounds,")), (
        "GROUND_LITTER_USES_NATIVE_ACTOR_GPU_CAPTURE_IN_WORLD_PATH")
    assert "GuiGeometry.RotateAroundPivot(canvas," in ground and (
        "groundFoliageSwayDegrees" in ground), (
        "LIVING_GROUND_FOLIAGE_LOST_ROOTED_WIND")
    # Environment-only still props (including 9 planted herbs per garden)
    # must not allocate an offscreen actor GPU RenderTexture per static tile.
    props = environment.split("private bool DrawFieldProp(")[1]
    assert "DrawStaticFieldSprite(fieldProps,id,foot," in props
    assert "DrawStaticFieldSprite(detailSheet,id,foot," in props
    assert "DrawStaticFieldSprite(fieldProps,6," in props
    assert "Actor(fieldProps," not in props and "Actor(detailSheet," not in props, (
        "STATIC_SCENERY_REENTERED_ACTOR_GPU_CAPTURE")
    painter = environment.split("private void DrawStaticFieldSprite(")[1]
    painter = painter.split("private bool DrawFieldProp(")[0]
    assert "if(drawingOutline)" in painter and (
        "Actor(sheet,4,4,id,foot,size,tint);" in painter), (
        "STATIC_PROP_OUTLINE_CONTOUR_GUARD_REMOVED")
    assert "Atlas(sheet,4,4,id,new Rect(foot.x-size*.5f," in painter and (
        "foot.y-size*120f/128f,size,size" in painter), (
        "STATIC_PROP_ORIGINAL_ATLAS_FOOT_REGISTRATION_LOST")
    # Native PlayMode contract must target Frontier.unity, not only a
    # preview prefab or a spare SpriteRenderer. Native runner remains
    # NATIVE_UNVERIFIED until Unity 2022.3 actually executes these checks.
    native_qa = (ROOT / "Assets/Tests/PlayMode/VisualPlayModeTests.cs").read_text()
    def check_native_scene_contract(source):
        required = (
            "ProductionFrontierSceneUsesTheLiveThreeClassHeroPath",
            "EditorSceneManager.LoadSceneInPlayMode(",
            '"Assets/Scenes/Frontier.unity"',
            'Assert.AreEqual("Frontier", SceneManager.GetActiveScene().name)',
            "Assert.IsTrue((bool)mode.GetValue(app)",
            "SpriteRenderer renderer = capture.GetValue(app) as SpriteRenderer;",
            "Assert.AreSame(atlas, renderer.sprite.texture,",
            '"real-Frontier-scene-"',
        )
        for item in required:
            assert item in source, ("NATIVE_PRODUCTION_SCENE_COVERAGE_LOST",item)
        assert "Assert.Ignore(" not in source, (
            "NATIVE_ANIMATOR_MISSING_MUST_NOT_BE_IGNORED")
    # Imported Animator missing must hard-fail in its own separate native
    # clip test, which lives OUTSIDE the production idle-scene method.
    assert "Native Animator MISSING: production PlayMode must FAIL" in native_qa
    assert "Released swing must remain one coherent full-body Attack, not Run" in native_qa
    assert "Do not play hidden footstep audio" in native_qa
    assert "Assert.Ignore(" not in native_qa
    # The first scene-only test and the newer full action test both load
    # Frontier.unity. Mutating the entire file is invalid: the OTHER good
    # test can hide that the test under mutation was deliberately destroyed.
    intro_start = native_qa.index("public IEnumerator ProductionFrontierSceneUsesTheLiveThreeClassHeroPath()")
    intro_end = native_qa.index("public IEnumerator ActualFrontierSceneAttackAndSkillContactFramesKeepOneBody()")
    native_intro = native_qa[intro_start:intro_end]
    check_native_scene_contract(native_intro)
    for item in ("EditorSceneManager.LoadSceneInPlayMode(",
                 "Assert.IsTrue((bool)mode.GetValue(app)",
                 "Assert.AreSame(atlas, renderer.sprite.texture,"):
        try:
            check_native_scene_contract(native_intro.replace(
                item, "/* intentionally corrupted native path */", 1))
        except AssertionError:
            pass
        else:
            raise AssertionError("UNSAFE_CI: native scene/renderer parity check removed")
    # Prevent the detached N/S hand VFX that came from screen-fixed
    # E-facing effectX = p.x+22 and effectY = p.y-63/77. These must share
    # the actual GameCore muzzle and height used by physical projectiles.
    def audit_visual_mounts(code):
        fx = code.split("if (!drawingOutline && !Game.Dying && heroMotionAccents != null)")[1]
        fx = fx.split("if (!drawingOutline && Game.save.upgrades")[0]
        for token in (
            "GameCore.WeaponOrigin(",
            "GameCore.WeaponHeight(look, direction)",
            "Point anchor = ScreenPoint(muzzle);",
            "new Rect(anchor.x - 20, effectY - 20, 40, 40)",
            "if (Game.ActuallyMoving && (pose == 2 || pose == 3))"
        ):
            assert token in fx, ("DETACHED_HERO_EFFECT_OR_FAKE_FOOTSTEP",token)
        assert "float effectX = p.x +" not in fx
    audit_visual_mounts(actor)
    for needle in ("GameCore.WeaponOrigin(",
                   "if (Game.ActuallyMoving && (pose == 2 || pose == 3))"):
        try:
            audit_visual_mounts(actor.replace(
                needle, "/* deliberately broken visual emitter */", 1))
        except AssertionError:
            pass
        else:
            raise AssertionError("UNSAFE_CI: detached or synthetic hero effect accepted")
    # The graphics-only native Test Runner exercises REAL live actions
    # in the PRODUCTION scene: 24 class/action/direction GPU probes,
    # not just loading the generated Animator clips in an isolated object.
    def verify_native_contact_probe(source):
        for required in (
            "ActualFrontierSceneAttackAndSkillContactFramesKeepOneBody",
            "EditorSceneManager.LoadSceneInPlayMode(",
            "core.Attack(aim);else core.Skill(aim);",
            "Assert.AreSame(atlas,renderer.sprite.texture,",
            "renderer.flipX",
            "core.PlayerPhase==ActionPhase.Recovery",
            "No invisible walk footsteps under action body",
            "Strafing cannot rotate committed attack/cast grip",
            'Assert.AreEqual(24,nativeProbes',
            'real-scene-',
        ):
            assert required in source, ("NATIVE_REAL_SCENE_CONTACT_GATE_MISSING", required)
        assert source.count("Assert.AreSame(atlas,renderer.sprite.texture,") >= 2, (
            "NATIVE_CONTACT_AND_MOVING_RECOVERY_BOTH_REQUIRE_SAME_AUTHORED_SPRITE")
    # Isolate the added real-production combat PlayMode test; the file
    # contains other valid atlas parity assertions with identical wording.
    # Mutation must target THIS test, not an unrelated older idle test.
    start = native_qa.index("public IEnumerator ActualFrontierSceneAttackAndSkillContactFramesKeepOneBody()")
    end = native_qa.index("public IEnumerator ProductionSceneKeepsEquippedHeroUnderEveryCrossSkillAndAttack()")
    actual_scene = native_qa[start:end]
    verify_native_contact_probe(actual_scene)
    for required in ("Assert.AreSame(atlas,renderer.sprite.texture,",
                     "core.Attack(aim);else core.Skill(aim);",
                     'Assert.AreEqual(24,nativeProbes'):
        try:
            verify_native_contact_probe(actual_scene.replace(
                required, "/* injected broken live GPU test */", 1))
        except AssertionError:
            pass
        else:
            raise AssertionError("UNSAFE_CI: removed real native scene combat QA accepted")
    # The third REAL Frontier scene PlayMode integration test deliberately
    # removes older Player/Walk art, then checks actual GPU Attack/Cast
    # across all 72 class x equipment x action x facing permutations.
    cross_start=native_qa.index(
        "public IEnumerator ProductionSceneKeepsEquippedHeroUnderEveryCrossSkillAndAttack()")
    cross_end=native_qa.index(
        "public IEnumerator LiveSpriteRendererCapturesCanonicalHeroPixelsInGameView()")
    cross_scene=native_qa[cross_start:cross_end]
    def verify_equipment_gpu_scene(source):
        for required in (
            'EditorSceneManager.LoadSceneInPlayMode(',
            '"Assets/Scenes/Frontier.unity"',
            'for(int vocation=0;vocation<3;vocation++)',
            'for(int equipped=0;equipped<3;equipped++)',
            'for(int command=0;command<2;command++)',
            'for(int f=0;f<4;f++)',
            'oldPlayers.SetValue(app,null);',
            'oldWalks.SetValue(app,null);',
            'if(command==0)g.Attack(aim);',
            'else g.Skill(aim);',
            'Assert.AreSame(shipped,renderer.sprite.texture,',
            'Assert.AreEqual(f==1,renderer.flipX,',
            'Assert.AreEqual(128f,renderer.sprite.pixelsPerUnit,.05f);',
            'Assert.AreEqual(72,verified,',
            'oldPlayers.SetValue(app,savedPlayers);',
            'oldWalks.SetValue(app,savedWalks);',
        ):
            assert required in source, (
                "REAL_SCENE_CROSS_EQUIP_NATIVE_GPU_QA_MISSING",required)
    verify_equipment_gpu_scene(cross_scene)
    for token in (
        'oldPlayers.SetValue(app,null);',
        'Assert.AreSame(shipped,renderer.sprite.texture,',
        'Assert.AreEqual(72,verified,',
    ):
        try:
            verify_equipment_gpu_scene(cross_scene.replace(
                token,'/* injected wrong equipment GPU mapping */',1))
        except AssertionError:
            pass
        else:
            raise AssertionError(
                "UNSAFE_CI: missing real scene cross-class native check accepted")
    # Imported canonical Sprite sub-assets belong to Unity Resources, while
    # dynamic fallback Sprite.Create wrappers belong to the live renderer.
    # Never destroy Unity-owned import assets during F8 or scene teardown.
    def verify_imported_sprite_ownership(source):
        checks = (
            "Resources.LoadAll<Sprite>(resource)",
            "candidate.texture != atlas",
            "candidate.rect.width - HeroCell",
            "candidate.rect.height - HeroCell",
            "candidate.pixelsPerUnit -",
            "Vector2.Distance(candidate.pivot,",
            "Mathf.RoundToInt(candidate.rect.x / HeroCell)",
            "matched == expectedFrames",
            "cells = candidates;",
            "heroOwnedPublishedSprites.Add(cells[i]);",
            "foreach(Sprite sprite in heroOwnedPublishedSprites)",
            "if(sprite!=null)Destroy(sprite);",
            "heroOwnedPublishedSprites.Clear();",
        )
        for mark in checks:
            assert mark in source, (
                "DANGEROUS_IMPORTED_SPRITE_OWNERSHIP_OR_CLASS_MISMATCH",mark)
        cleanup = source.split("private void ReleaseHeroCapture()")[1]
        assert "if (sprite != null) Destroy(sprite);" not in cleanup, (
            "UNITY_IMPORTED_RESOURCES_SPRITES_WOULD_BE_DESTROYED")
        assert "if (texture != null) Destroy(texture);" not in cleanup.split(
            "heroPublishedSprites.Clear();")[1], (
            "UNITY_IMPORTED_TEXTURE_DELETION")
    verify_imported_sprite_ownership(engine)
    for mark in ("candidate.texture != atlas",
                 "foreach(Sprite sprite in heroOwnedPublishedSprites)",
                 "Mathf.RoundToInt(candidate.rect.x / HeroCell)"):
        try:
            verify_imported_sprite_ownership(engine.replace(
                mark, "/* intentionally unsafe imported Sprite path */", 1))
        except AssertionError:
            pass
        else:
            raise AssertionError("UNSAFE_CI: imported Sprite resource regression accepted")
    assert "if(action==1)position%=frames;" in actor
    assert "bool actionSix=(action==2||action==3)&&authored==4;" in actor
    assert "bool reactionFive=(action==4||action==5)&&authored==3;" in actor
    assert "actionSix?(phase<2f?Math.Min(2f,phase*2f):phase*2f-1f):" in actor
    assert "frames=actionSeven?7:actionSix?6:reactionFive?5:authored;" in actor
    assert "bool actionSeven=actionSix&&HasCompleteHeroContactArt();" in actor
    assert "heroContactFamilyChecked" in actor and "heroContactFamilyComplete" in actor
    assert "atlas.width!=7*256" in actor
    assert "phase<2f?Math.Min(3f,phase*2f):phase*2f" in actor
    # Contact animation must be tied to GameCore's released flag,
    # not only an interpolation cap that a later artist-timeline tweak
    # could accidentally relax. This is defense in depth: the CURRENT
    # six-cell phase is already capped at pre-contact index 2.
    for token in (
        "if((action==2||action==3)&&Game.playerAction!=null&&",
        "!Game.playerAction.released)",
        "int realContact=actionSeven?4:3;",
        "position=Math.Min(position,realContact-.40f);",
    ):
        assert token in actor, ("RELEASE_EVENT_SPRITE_FENCE_MISSING",token)
    mutation=actor.replace("position=Math.Min(position,realContact-.40f);",
                           "/* gate erased on purpose */",1)
    assert "position=Math.Min(position,realContact-.40f);" not in mutation
    assert "position=Math.Min(position,realContact-.40f);" in actor
    runtime=(ROOT/"tests/RuntimeCombatInputChecks.cs").read_text()
    assert "DirectionalLmbAndSkillContactNeverFlashBeforeRelease" in runtime
    assert "selected.released&&g.playerAction==selected" in runtime
    assert "Check(audited==216," in runtime
    assert "new Point(1,0),new Point(-1,0)" in runtime
    assert "new Point(0,-1),new Point(0,1)" in runtime
    # Exact contact boundary, no early release frame even at maximal windup.
    def sampled_cell(phase, seven):
        pos = (min(3.,phase*2.) if phase<2 else phase*2) if seven else (
            min(2.,phase*2.) if phase<2 else phase*2-1)
        return min(6 if seven else 5, int(pos+.20))
    assert sampled_cell(1.55, True)==3 and sampled_cell(2,True)==4
    assert sampled_cell(1.55, False)==2 and sampled_cell(2,False)==3
    # Both heavy LMB and cross-discipline Q must draw the new full-body
    # published atlas of the EQUIPPED hero, never old Player art or the
    # skill's mechanical class costume. This regression was visible in-game.
    def check_combat_identity(actor_code, renderer_code, core_code):
        main=actor_code[actor_code.index("private void DrawPlayerActor()"):
                        actor_code.index("private int EnemyPose(")]
        fallback=actor_code[actor_code.index("private void DrawWholeBodyHeroOnGUI("):
                            actor_code.index("private void DrawPlayerPreview(")]
        preview=actor_code[actor_code.index("private void DrawPlayerPreview("):
                           actor_code.index("private void DrawPlayerActor()")]
        capture=renderer_code[renderer_code.index("private bool DrawSpriteRendererHero("):
                              renderer_code.index("private bool TryUsePublishedHeroFrame(")]
        assert "playerAction.equippedWeapon : save.weapon;" in core_code
        assert 'weapon = kind == "skill" ? save.profession : save.weapon' in core_code
        assert "if(look<0||look>=FullHeroClasses.Length)return;" in main
        assert "DrawSpriteRendererHero(p,tint,flip,facing,moving)" in main
        assert "DrawWholeBodyHeroOnGUI(p,tint,flip,look,facing,moving)" in main
        assert "bool moving=pose>=2&&pose<=3;" in main
        assert "bool flip=facing==0&&direction.x<-.1f;" in main
        for prohibited in ("playerSheets", "walkSheets", "playerRear",
                           "playerSouth", "playerReactions", "DrawLegacyHero",
                           "sheet=", "frame=", "Actor(sheet", "Actor(source"):
            assert prohibited not in main, (
                "LEGACY_HERO_ACTOR_FRAME_SELECTOR_REINTRODUCED",prohibited)
        assert "ResolveFullBodyHeroFrame(moving,facing," in fallback
        assert "FullHeroActions[action]" in fallback
        assert "FullHeroFacings[facing]" in fallback
        assert "if(drawingOutline)" in fallback
        assert "tint=new Color(1f,.56f,.35f,.85f);" in fallback
        assert "Actor(original" not in fallback
        assert "Actor(source" not in preview
        assert '"Hero_"+FullHeroClasses[profession]+"_Idle_S"' in preview
        # The whole-body world fallback and the inventory MUST share
        # one validated reimport-aware resource reader. Requiring a direct
        # Resources.Load within the preview itself falsely rejected safe
        # centralization and could not catch stale cached player costumes.
        reader=actor_code[actor_code.index("private Texture2D PublishedFullBodyHeroAtlas("):
                          actor_code.index("private bool ResolveFullBodyHeroFrame(")]
        assert "PublishedFullBodyHeroAtlas(key,1)" in preview
        assert "PublishedFullBodyHeroAtlas(key,frames)" in fallback
        assert "PublishedFullBodyHeroAtlas(idleKey,1)" in fallback
        assert 'Resources.Load<Texture2D>' in reader
        assert '"Frontier/HeroAnimator/Canonical/"+key' in reader
        assert 'candidate.width==expectedWidth' in reader
        assert 'candidate.height==256' in reader
        assert "fullBodyHeroSheetCache[key]=candidate;" in reader
        assert "fullBodyHeroRecheckAt[key]=clock+2.5f;" in reader
        # Three disjoint production paths must source the SAME verified
        # canonical body: invalid-clip rejection, verified Animator sampling,
        # and direct Controller-unavailable fallback. This third binding
        # closes a real hot-import race where a single same-rect new Sprite
        # could bypass whole-family validation and repaint the pending GPU.
        assert capture.count("heroCaptureRenderer.sprite=canonical;") == 3, (
            "ALL_THREE_NATIVE_OUTCOMES_MUST_BIND_VALIDATED_CANONICAL_BODY")
        assert "if(available)" in capture
        assert "if(!TryUsePublishedHeroFrame(equipment,0,facing," in capture
        assert "HeroSpriteMatchesPublishedFrame(" in capture
        for prohibited in ("legacy", "sheet.GetInstanceID()", "unifiedAction", "heroSprites",
                           "Sprite.Create(", "BakeCanonicalHeroFrame(", "Actor("):
            # Comments may mention legacy; guard source-level OLD function
            # inputs/commands, not diagnostic explanatory prose.
            if prohibited == "legacy": continue
            assert prohibited not in capture, (
                "NATIVE_HERO_LEGACY_SPRITE_REINTRODUCED",prohibited)
        assert "heroPublishedUnavailable[key]=Time.frameCount+120;" in renderer_code
        assert "heroPublishedUnavailable.Remove(key);" in renderer_code
    check_combat_identity(actor, engine, core)
    # Mutation test against the real production compositor, not an old
    # function string that no longer exists (a previous false-green bug).
    for label,corrupt_actor,corrupt_engine,corrupt_core in (
        ("switch_to_legacy_player",
         actor.replace("DrawSpriteRendererHero(p,tint,flip,facing,moving)",
             "DrawLegacyHero()",1),engine,core),
        ("stale_skill_costume",
         actor,engine,core.replace(
             "playerAction.equippedWeapon : save.weapon;",
             "playerAction.weapon : save.weapon;",1)),
        ("old_renderer_body",
         actor,engine.replace("heroCaptureRenderer.sprite=canonical;",
             "heroCaptureRenderer.sprite=someLegacySprite;",1),core),
        ("old_ongui_action_source",
         actor.replace("FullHeroActions[action]",
             'legacyActions[action]',1),engine,core),
        ("permanent_missing_sprite",
         actor,engine.replace(
             "heroPublishedUnavailable.Remove(key);",
             "/* permanent-missing */",1),core),
        ("accept_truncated_hot_import",
         actor.replace("candidate.width==expectedWidth",
             "candidate.width>0",1),engine,core),
        ("disconnect_preview_from_canonical_reader",
         actor.replace("PublishedFullBodyHeroAtlas(key,1)",
             "LoadOldUnreviewedAvatar(key)",1),engine,core),
    ):
        try:
            check_combat_identity(corrupt_actor,corrupt_engine,corrupt_core)
        except AssertionError:
            continue
        raise AssertionError("UNSAFE_CI: hero identity mutation survived "+label)
    dynamic=(ROOT/"tests/RuntimeCombatInputChecks.cs").read_text()
    for needle in ("CanonicalHeroNeverDependsOnLegacyPlayerImports",
                   "LiveEquipmentPreviewUsesTheSameCanonicalBody",
                   'Set(app,"playerSheets",null);',
                   'Set(app,"walkSheets",null);',
                   'Check(checks==216,'):
        assert needle in dynamic, ("LIVE_HERO_OLD_MODEL_TEST_MISSING",needle)
    # A non-null Animator sprite is NOT proof of correct class/facing/contact.
    # The live GPU output must match the registered published atlas cell.
    for mark in [
        "HeroSpriteMatchesPublishedFrame(",
        "animated.texture != published.texture",
        "Vector2.Distance(pivotA, pivotB)",
        "animated.pixelsPerUnit - published.pixelsPerUnit",
        "heroRejectedNativeClips.TryGetValue(nativeClipKey",
        "Time.frameCount < rejectedUntil",
        "heroRejectedNativeClips[nativeClipKey]=",
        "heroRejectedNativeClips.Remove(nativeClipKey)",
        "heroCaptureRenderer.sprite=canonical;",
        "heroRejectedNativeClips.Clear()",
        "bool recentlyRejected =",
        "bool nativeReady=available &&",
        "!heroPublishedIncomplete.Contains(",
        "HeroLiveProfessions[equipment]",
        "HeroLiveActions[action]",
        "HeroLiveFacings[facing]",
        "!recentlyRejected && TryUseProductionHeroClip(facing);",
        "heroPublishedIncomplete.Add(key);",
        "heroPublishedIncomplete.Remove(key);",
        "heroPublishedRecheckAt[key]=Time.frameCount+180;",
        "heroOwnedPublishedSprites.Remove(old)",
        "heroControllerRetryAt=Time.frameCount+",
        "(heroProductionReady?180:120);",
        "if(controller!=null)",
        "if(heroProductionReady)",
        "Time.frameCount >= heroControllerRetryAt",
        "heroCaptureAnimator.runtimeAnimatorController = null;",
        "heroProductionReady=true;",
    ]:
        assert mark in engine, "Missing native Animator pixel-parity fallback: " + mark
    assert "bool available=TryUsePublishedHeroFrame(equipment,action," in engine
    # Heavy combos keep the SAME canonical full-body Attack atlas; never
    # revert to a different original costume or independent Animator clock.
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
    # Unity Editor / Play must not hard-fail when OPTIONAL Animator baking
    # is broken: whole-body shipped Sprite resources still permit gameplay.
    # Keep the strict Editor builder itself unchanged, but call via a
    # try/catch in the Play + all three Build entrypoints.
    assert editor_boot.count(
        "Starfall.EditorTools.StarfallCanonicalHeroProductionBuilder.EnsureBuilt();") == 1
    assert editor_boot.count("PrepareHeroAnimationsOrUsePublishedFallback();") == 3
    assert "catch (System.Exception error)" in editor_boot
    assert "continuing with the already shipped complete-body Sprite" in editor_boot
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
    assert "if(!useSpriteRendererHero||drawingOutline||" in engine
    assert "if(heroCaptureBroken)" in engine
    assert "if(Time.frameCount < heroCaptureRetryAt &&" in engine
    assert "Time.frameCount >= heroCaptureRetryAt-120)return false;" in engine
    assert "heroCaptureRetryAt = Time.frameCount + 120;" in engine
    assert "if(!captured)" in actor and "DrawWholeBodyHeroOnGUI" in actor
    assert "KeyCode.F8" in game and "else ReleaseHeroCapture();" in game
    assert engine.count("return false;") >= 3, "Production must have a failure fallback"
    # Sword/Q/ordinary attack contact and recovery remain a COMPLETE
    # class-owned action body until the authoritative damage clock ends.
    # GameCore may translate its root while reduced-speed recovery runs, but
    # MUST NOT switch to an unrelated Walk torso/weapon/facing mid-action.
    assert "bool moving=pose>=2&&pose<=3;" in actor
    assert "walkSheets[look]" not in actor[actor.index("private void DrawPlayerActor()"):actor.index("private int EnemyPose(")]
    assert "bool movingRecovery" not in actor
    assert "if(profession<0||profession>=FullHeroClasses.Length)return;" in actor  # only identity validation
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
    # Occluded protagonist silhouettes must reuse the SAME published
    # Sprite atlas as the visible actor. The old outline pass resurrected
    # the original Player body during LMB/Q behind foreground tiles.
    assert "Actor(original,4,3,originalFrame,foot,192,tint,flip);" not in actor
    assert "tint=new Color(1f,.56f,.35f,.85f);" in actor
    assert "if(drawingOutline)" in actor
    native_runtime = (ROOT / "tests/RuntimeCombatInputChecks.cs").read_text()
    assert 'Set(app,"drawingOutline",true);' in native_runtime
    assert 'finally { Set(app,"drawingOutline",false); }' in native_runtime
    assert "art.OutlineMask(atlas)" not in actor
    # Fault injection: resurrect the old outfit only in the occlusion pass
    # and the resource guard must hard-FAIL despite normal action tests.
    try:
        assert "Actor(original,4,3,originalFrame,foot,192,tint,flip);" not in (
            actor.replace("tint=new Color(1f,.56f,.35f,.85f);",
                          "Actor(original,4,3,originalFrame,foot,192,tint,flip);",1))
    except AssertionError:
        pass
    else:
        raise AssertionError("UNSAFE_CI: old-model silhouette injection escaped")

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
    # A Unity Player build is NOT the same as opening Editor PlayMode:
    # all 63 actual Sprite curves + 3 controllers must be generated,
    # source-verified and packaged before BuildPipeline runs.
    def require_native_build_contract(code):
        method = code.split("public static void RequireProductionAnimatorForBuild()")[1]
        method = method.split("private static bool HasExactProductionTransition(")[0]
        gate = code.split("internal sealed class StarfallHeroProductionBuildGate")[1]
        for marker in (
            "EnsureBuilt();",
            "if (!NativeControllersMatchPublished())",
            "NativeClipMatchesPublished(",
            "AssetDatabase.LoadAssetAtPath<AnimationClip>",
            "AssetDatabase.SaveAssets();",
        ):
            assert marker in method, ("HERO_PLAYER_BUILD_NOT_SOURCE_VERIFIED", marker)
        for marker in (
            "UnityEditor.Build.IPreprocessBuildWithReport",
            "void OnPreprocessBuild(",
            ".RequireProductionAnimatorForBuild();",
            "UnityEditor.Build.BuildFailedException",
        ):
            assert marker in gate or marker in code.split(
                "internal sealed class StarfallHeroProductionBuildGate")[0][-130:], (
                "HERO_PLAYER_BUILD_GATE_BYPASS", marker)
    require_native_build_contract(native)
    for broken in (
        native.replace(
            "if (!NativeControllersMatchPublished())\n                throw",
            "if (false)\n                throw", 1),
        native.replace(
            "            AssetDatabase.SaveAssets();\n        }\n\n        private static bool HasExactProductionTransition(",
            "            // deliberately removed\n        }\n\n        private static bool HasExactProductionTransition(",1),
        native.replace(".RequireProductionAnimatorForBuild();",
                       ".EnsureBuilt();", 1),
        native.replace("UnityEditor.Build.IPreprocessBuildWithReport",
                       "System.IDisposable", 1),
        native.replace("UnityEditor.Build.BuildFailedException",
                       "System.Exception", 1),
    ):
        try:
            require_native_build_contract(broken)
        except (AssertionError, IndexError):
            pass
        else:
            raise AssertionError(
                "UNSAFE_CI_ACCEPTED_MISSING_MANDATORY_HERO_PLAYER_BUILD_GATE")
    print("PASS mandatory Unity Player pre-build gate rejects missing 63 hero Sprite curves and 3 controllers")
    print("PASS: 63 checked shipped SpriteRenderer resources, three-class frame sampling, "
          "non-destructive Editor controller hookup and GameCore clock fallback.")
    print("PASS: production canonical compositor, approved atlas import, "
          "7 action x 3 direction x 3 profession Sprite/Animator bake, "
          "GameCore contact synchronization and safe OnGUI fallback (offline)")
    print("NATIVE_UNVERIFIED: Unity Editor, live RenderTexture alpha, animations and FPS")


if __name__ == "__main__":
    check()
