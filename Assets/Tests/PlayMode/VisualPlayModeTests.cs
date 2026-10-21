#if UNITY_INCLUDE_TESTS
using System.Collections;
using System.IO;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
#endif
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Starfall.Tests
{
    // Run from Test Runner > PlayMode in a licensed Unity Editor with graphics.
    // These checks are supplied, but cannot run in the offline Mono harness.
    public sealed class VisualPlayModeTests
    {
        private GameObject host;
        private FrontierRuntime app;
        [UnitySetUp] public IEnumerator SetUp()
        {
            app = Object.FindObjectOfType<FrontierRuntime>();
            if (app == null) { host = new GameObject("Starfall native visual check"); app = host.AddComponent<FrontierRuntime>(); }
            yield return null; Assert.NotNull(app.Game, "Campaign and native resources must import");
        }
        [UnityTearDown] public IEnumerator TearDown()
        { if (app != null) app.EndVisualReview(); if (host != null) Object.Destroy(host); yield return null; }
        [UnityTest] public IEnumerator ProductionFrontierSceneUsesTheLiveThreeClassHeroPath()
        {
#if UNITY_EDITOR
            // Do NOT create an isolated preview object and claim the real
            // production scene has migrated. Boot Frontier.unity through
            // Unity Editor's actual in-PlayMode scene-loading entry point.
            var scene = EditorSceneManager.LoadSceneInPlayMode(
                "Assets/Scenes/Frontier.unity",
                new LoadSceneParameters(LoadSceneMode.Single));
            host = null;  // earlier isolated fixture was replaced by scene load
            yield return null;
            yield return new WaitForEndOfFrame();
            Assert.IsTrue(scene.IsValid() && scene.isLoaded);
            Assert.AreEqual("Frontier", SceneManager.GetActiveScene().name);
            var runtimes = Object.FindObjectsOfType<FrontierRuntime>();
            Assert.AreEqual(1, runtimes.Length,
                "Frontier scene and bootstrap must resolve to exactly one runtime");
            app = runtimes[0];
            Assert.NotNull(app,
                "Actual Frontier scene must boot production FrontierRuntime");
            Assert.NotNull(app.Game,
                "The real scene cannot silently fail Campaign/Save initialization");
            var flags = System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance;
            var mode = typeof(FrontierRuntime).GetField("useSpriteRendererHero", flags);
            var capture = typeof(FrontierRuntime).GetField("heroCaptureRenderer", flags);
            var texture = typeof(FrontierRuntime).GetField("heroCaptureTexture", flags);
            var broken = typeof(FrontierRuntime).GetField("heroCaptureBroken", flags);
            Assert.IsTrue((bool)mode.GetValue(app),
                "Production Frontier scene must default to one full-body SpriteRenderer");
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath,
                "../docs/native-captures"));
            Directory.CreateDirectory(folder);
            string[] classes = {"Vanguard", "Ranger", "Arcanist"};
            for (int cls=0;cls<classes.Length;cls++)
            {
                app.BeginVisualReview(cls,1);
                app.Game.SetAim(new Point(1,0));
                yield return null;
                yield return new WaitForEndOfFrame();
                Assert.IsFalse((bool)broken.GetValue(app),
                    "Actual scene failed GPU hero capture: "+classes[cls]);
                SpriteRenderer renderer = capture.GetValue(app) as SpriteRenderer;
                RenderTexture gpu = texture.GetValue(app) as RenderTexture;
                Assert.NotNull(renderer, "Missing live SpriteRenderer in Frontier");
                Assert.NotNull(renderer.sprite, "Actual scene did not draw live hero");
                Assert.NotNull(gpu, "Actual scene did not create hero capture target");
                Assert.IsTrue(gpu.IsCreated());
                Texture2D atlas = Resources.Load<Texture2D>(
                    "Frontier/HeroAnimator/Canonical/Hero_"+classes[cls]+"_Idle_E");
                Assert.AreSame(atlas, renderer.sprite.texture,
                    "Production scene selected wrong character equipment/identity");
                Assert.AreEqual(128f,renderer.sprite.pivot.x,.05f);
                Assert.AreEqual(16f,renderer.sprite.pivot.y,.05f);
                ScreenCapture.CaptureScreenshot(Path.Combine(folder,
                    "real-Frontier-scene-"+classes[cls]+".png"));
                yield return null;
            }
            app.EndVisualReview();
#else
            Assert.Fail("Native production scene verification requires Unity Editor");
            yield break;
#endif
        }

        [UnityTest] public IEnumerator ActualFrontierSceneAttackAndSkillContactFramesKeepOneBody()
        {
#if UNITY_EDITOR
            // This is the ACTUAL production scene, live action clock, live
            // GPU SpriteRenderer and body selection — never a preview prefab.
            var scene = EditorSceneManager.LoadSceneInPlayMode(
                "Assets/Scenes/Frontier.unity",
                new LoadSceneParameters(LoadSceneMode.Single));
            host = null;
            yield return null;
            yield return new WaitForEndOfFrame();
            Assert.IsTrue(scene.isLoaded);
            var runtimes = Object.FindObjectsOfType<FrontierRuntime>();
            Assert.AreEqual(1, runtimes.Length,
                "Frontier scene and bootstrap must resolve to exactly one runtime");
            app = runtimes[0];
            Assert.IsNotNull(app,"Live FrontierRuntime must boot in production scene");
            Assert.IsNotNull(app.Game);
            var flags = System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance;
            var capture = typeof(FrontierRuntime).GetField("heroCaptureRenderer", flags);
            var target = typeof(FrontierRuntime).GetField("heroCaptureTexture", flags);
            var broken = typeof(FrontierRuntime).GetField("heroCaptureBroken", flags);
            var mode = typeof(FrontierRuntime).GetField("useSpriteRendererHero",flags);
            string[] classes={"Vanguard","Ranger","Arcanist"};
            string[] commands={"Attack","Cast"};
            var directions=new[]{new Point(1,0),new Point(-1,0),
                new Point(0,-1),new Point(0,1)};
            var labels=new[]{"E","W","N","S"};
            int nativeProbes=0, rootedRecovery=0;
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,
                "../docs/native-captures"));
            Directory.CreateDirectory(folder);
            for(int vocation=0;vocation<3;vocation++)
            for(int command=0;command<2;command++)
            for(int orientation=0;orientation<4;orientation++)
            {
                app.BeginVisualReview(vocation,1);
                var core=app.Game;
                core.enemies.Clear();
                core.save.companion=-1;
                core.save.energy=100;
                core.save.x=12;core.save.y=14;
                Point aim=directions[orientation];
                core.SetAim(aim);
                Assert.IsTrue((bool)mode.GetValue(app),
                    "Production SpriteRenderer must remain default");
                if(command==0)core.Attack(aim);else core.Skill(aim);
                ActorAction action=core.playerAction;
                Assert.IsNotNull(action,"Actual game action failed to launch");
                int steps=0;
                while(core.playerAction==action&&!action.released&&steps++<60)
                    app.AdvanceVisualReview(.02f,new Point());
                Assert.IsTrue(action.released&&core.playerAction==action,
                    "Contact must precede recovery and follow real GameCore time");
                yield return null;
                yield return new WaitForEndOfFrame();
                Assert.IsFalse((bool)broken.GetValue(app),
                    "Full-body GPU capture failed for "+classes[vocation]);
                SpriteRenderer renderer=capture.GetValue(app) as SpriteRenderer;
                RenderTexture gpu=target.GetValue(app) as RenderTexture;
                Assert.IsNotNull(renderer);Assert.IsNotNull(renderer.sprite);
                Assert.IsNotNull(gpu);Assert.IsTrue(gpu.IsCreated());
                string facing=orientation==2?"N":orientation==3?"S":"E";
                string key="Hero_"+classes[vocation]+"_"+commands[command]+
                    "_"+facing;
                Texture2D atlas=Resources.Load<Texture2D>(
                    "Frontier/HeroAnimator/Canonical/"+key);
                Assert.IsNotNull(atlas);
                Assert.AreSame(atlas,renderer.sprite.texture,
                    "Live body must remain the same class/action/facing at contact: "+key);
                Assert.AreEqual(orientation==1,renderer.flipX,
                    "West uses original east body mirrored as ONE Sprite");
                Assert.AreEqual(128f,renderer.sprite.pivot.x,.05f);
                Assert.AreEqual(16f,renderer.sprite.pivot.y,.05f);
                nativeProbes++;
                steps=0;
                while(core.playerAction==action&&
                    core.PlayerPhase!=ActionPhase.Recovery&&steps++<60)
                    app.AdvanceVisualReview(.02f,new Point());
                Assert.IsTrue(core.playerAction==action&&
                    core.PlayerPhase==ActionPhase.Recovery);
                Point move=new Point(-aim.y,aim.x);
                if(!core.CanStand(core.Position+move*.1f))move=move*-1;
                float oldStep=core.walkDistance;int oldAudio=core.footContacts;
                app.AdvanceVisualReview(.02f,move);
                yield return null;
                yield return new WaitForEndOfFrame();
                Assert.IsFalse((bool)broken.GetValue(app));
                renderer=capture.GetValue(app) as SpriteRenderer;
                Assert.IsNotNull(renderer);
                Assert.IsNotNull(renderer.sprite);
                Assert.AreSame(atlas,renderer.sprite.texture,
                    "Moving recovery must keep WHOLE authored class action, not Run: "+key);
                Assert.AreEqual(oldStep,core.walkDistance,.0001f,
                    "No invisible foot-gait cycle under action body");
                Assert.AreEqual(oldAudio,core.footContacts,
                    "No invisible walk footsteps under action body");
                Assert.Less((core.VisualFacing-aim).Length,.001f,
                    "Strafing cannot rotate committed attack/cast grip");
                if(core.ActuallyMoving)rootedRecovery++;
                if(orientation==0||orientation==3)
                {
                    ScreenCapture.CaptureScreenshot(Path.Combine(folder,
                        "real-scene-"+classes[vocation]+"-"+commands[command]+
                        "-"+labels[orientation]+"-moving-recovery.png"));
                    yield return null;
                }
            }
            Assert.AreEqual(24,nativeProbes,
                "All 3 classes x 2 actions x 4 facings must reach GPU contact");
            Assert.Greater(rootedRecovery,0,
                "Need actual root-motion evidence in contact-anchored recovery");
#else
            Assert.Fail("Native full-body action/scene QA requires Unity Editor");
            yield break;
#endif
        }

        [UnityTest] public IEnumerator ProductionSceneKeepsEquippedHeroUnderEveryCrossSkillAndAttack()
        {
#if UNITY_EDITOR
            // Run the REAL Frontier scene + GPU SpriteRenderer, never an
            // isolated preview or old Animator-only prefab. Test 3 combat
            // professions x 3 equipment sets x LMB/Q x 4 actual directions.
            var scene=EditorSceneManager.LoadSceneInPlayMode(
                "Assets/Scenes/Frontier.unity",
                new LoadSceneParameters(LoadSceneMode.Single));
            host=null;
            yield return null;
            yield return new WaitForEndOfFrame();
            Assert.IsTrue(scene.isLoaded);
            app=Object.FindObjectOfType<FrontierRuntime>();
            Assert.IsNotNull(app);
            var flags=System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.NonPublic;
            var type=typeof(FrontierRuntime);
            var capture=type.GetField("heroCaptureRenderer",flags);
            var broken=type.GetField("heroCaptureBroken",flags);
            var mode=type.GetField("useSpriteRendererHero",flags);
            var oldPlayers=type.GetField("playerSheets",flags);
            var oldWalks=type.GetField("walkSheets",flags);
            Assert.IsNotNull(capture);Assert.IsNotNull(broken);
            Assert.IsNotNull(oldPlayers);Assert.IsNotNull(oldWalks);
            object savedPlayers=oldPlayers.GetValue(app);
            object savedWalks=oldWalks.GetValue(app);
            string[] classes={"Vanguard","Ranger","Arcanist"};
            var aims=new[]{new Point(1,0),new Point(-1,0),
                new Point(0,-1),new Point(0,1)};
            string folder=Path.GetFullPath(Path.Combine(
                Application.dataPath,"../docs/native-captures"));
            Directory.CreateDirectory(folder);
            int verified=0;
            try
            {
                for(int vocation=0;vocation<3;vocation++)
                for(int equipped=0;equipped<3;equipped++)
                for(int command=0;command<2;command++)
                for(int f=0;f<4;f++)
                {
                    app.BeginVisualReview(vocation,1);
                    var g=app.Game;
                    g.enemies.Clear();
                    g.save.companion=-1;g.save.energy=100;
                    g.save.x=12;g.save.y=14;
                    if(equipped>0&&!g.Has("weapon_"+equipped))
                        g.save.flags.Add("weapon_"+equipped);
                    g.Equip(equipped);
                    Assert.AreEqual(equipped,g.save.weapon);
                    // Deliberately remove ALL old readable actor source art.
                    // The normal player, LMB and Q MUST NOT depend on it.
                    oldPlayers.SetValue(app,null);
                    oldWalks.SetValue(app,null);
                    mode.SetValue(app,true);
                    broken.SetValue(app,false);
                    Point aim=aims[f];
                    g.SetAim(aim);
                    if(command==0)g.Attack(aim);
                    else g.Skill(aim);
                    ActorAction selected=g.playerAction;
                    Assert.IsNotNull(selected);
                    Assert.AreEqual(command==0?equipped:vocation,
                        selected.weapon,"Skill mechanic stays vocation-specific");
                    Assert.AreEqual(equipped,selected.equippedWeapon,
                        "Skill appearance stays equipment-specific");
                    int n=0;
                    while(g.playerAction==selected&&!selected.released&&n++<80)
                        app.AdvanceVisualReview(.01f,new Point());
                    Assert.IsTrue(selected.released&&g.playerAction==selected,
                        "Actual damage release must precede the visual contact");
                    yield return null;
                    yield return new WaitForEndOfFrame();
                    Assert.IsFalse((bool)broken.GetValue(app));
                    var renderer=capture.GetValue(app) as SpriteRenderer;
                    Assert.IsNotNull(renderer);
                    Assert.IsNotNull(renderer.sprite);
                    string key="Hero_"+classes[equipped]+"_"+
                        (command==0?"Attack":"Cast")+"_"+(f==2?"N":f==3?"S":"E");
                    Texture2D shipped=Resources.Load<Texture2D>(
                        "Frontier/HeroAnimator/Canonical/"+key);
                    Assert.IsNotNull(shipped);
                    Assert.AreSame(shipped,renderer.sprite.texture,
                        "Actual scene resurrected old actor or skill vocation: "+key);
                    Assert.AreEqual(f==1,renderer.flipX,
                        "Canonical body must mirror west as a single Sprite");
                    Assert.AreEqual(128f,renderer.sprite.pivot.x,.05f);
                    Assert.AreEqual(16f,renderer.sprite.pivot.y,.05f);
                    Assert.AreEqual(128f,renderer.sprite.pixelsPerUnit,.05f);
                    verified++;
                    if(command==1&&f==0)
                    {
                        ScreenCapture.CaptureScreenshot(Path.Combine(folder,
                            "real-equipment-"+classes[equipped]+"-skill-"+
                            classes[vocation]+".png"));
                        yield return null;
                    }
                }
            }
            finally
            {
                oldPlayers.SetValue(app,savedPlayers);
                oldWalks.SetValue(app,savedWalks);
                app.EndVisualReview();
            }
            Assert.AreEqual(72,verified,
                "3 professions x 3 equipment x LMB/Q x 4 directions required");
#else
            Assert.Fail("Cross-equipped production GPU verification requires Unity Editor");
            yield break;
#endif
        }

        [UnityTest] public IEnumerator LiveSpriteRendererCapturesCanonicalHeroPixelsInGameView()
        {
            // This tests the actual FrontierRuntime renderer switch and
            // GPU RenderTexture, not just isolated AnimationClips. The
            // original OnGUI lane is restored in finally even on failure.
            var flags = System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic;
            var mode = typeof(FrontierRuntime).GetField("useSpriteRendererHero", flags);
            var textureField = typeof(FrontierRuntime).GetField("heroCaptureTexture", flags);
            var rendererField = typeof(FrontierRuntime).GetField("heroCaptureRenderer", flags);
            var brokenField = typeof(FrontierRuntime).GetField("heroCaptureBroken", flags);
            Assert.IsNotNull(mode);
            Assert.IsNotNull(textureField);
            Assert.IsNotNull(rendererField);
            Assert.IsNotNull(brokenField);
            bool previous = (bool)mode.GetValue(app);
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath,
                "../docs/native-captures"));
            Directory.CreateDirectory(folder);
            try
            {
                string[] classes = { "Vanguard", "Ranger", "Arcanist" };
                for (int vocation = 0; vocation < 3; vocation++)
                {
                    app.BeginVisualReview(vocation, 1, false);
                    app.Game.SetAim(new Point(1, 0)); // deterministic E idle probe
                    mode.SetValue(app, true);
                    yield return null;
                    yield return new WaitForEndOfFrame();
                    Assert.IsFalse((bool)brokenField.GetValue(app),
                        classes[vocation] + " native capture must not fail to OnGUI");
                    var texture = textureField.GetValue(app) as RenderTexture;
                    var renderer = rendererField.GetValue(app) as SpriteRenderer;
                    Assert.IsNotNull(texture, "No live GPU output for " + classes[vocation]);
                    Assert.IsTrue(texture.IsCreated(), "GPU texture lost");
                    Assert.IsNotNull(renderer);
                    Assert.IsNotNull(renderer.sprite, "No live character frame");
                    Texture2D published = Resources.Load<Texture2D>(
                        "Frontier/HeroAnimator/Canonical/Hero_" +
                        classes[vocation] + "_Idle_E");
                    Assert.IsNotNull(published);
                    Assert.AreSame(published, renderer.sprite.texture,
                        "Live native character cannot show another profession");
                    var readback = new Texture2D(256, 256,
                        TextureFormat.RGBA32, false);
                    var active = RenderTexture.active;
                    try
                    {
                        RenderTexture.active = texture;
                        readback.ReadPixels(new Rect(0, 0, 256, 256), 0, 0);
                        readback.Apply();
                        int visible = 0;
                        foreach (Color32 pixel in readback.GetPixels32())
                            if (pixel.a > 0) visible++;
                        Assert.Greater(visible, 100,
                            "SpriteRenderer camera produced an invisible hero");
                    }
                    finally
                    {
                        RenderTexture.active = active;
                        Object.Destroy(readback);
                    }
                    ScreenCapture.CaptureScreenshot(Path.Combine(folder,
                        "native-canonical-" + classes[vocation] + ".png"));
                    yield return null;
                }
            }
            finally
            {
                mode.SetValue(app, previous);
                app.EndVisualReview();
            }
        }

        [UnityTest] public IEnumerator CanonicalHeroAnimatorMatchesEveryPublishedSprite()
        {
            // Native PlayMode acceptance: sample the real Animator graph,
            // not synthetic off-line key values. Failed profession overrides,
            // wrong frame sampling and wrong foot pivots become test failures.
            const int cell = 256;
            string[] classes = { "Vanguard", "Ranger", "Arcanist" };
            string[] actions = { "Idle", "Run", "Attack", "Cast",
                "Dodge", "Hit", "Death" };
            string[] facings = { "E", "N", "S" };
            var probe = new GameObject("Starfall 63-clip native animation probe");
            probe.transform.position = new Vector3(9000, 9000, 0);
            SpriteRenderer renderer = probe.AddComponent<SpriteRenderer>();
            Animator animator = probe.AddComponent<Animator>();
            try
            {
                yield return null;
                for (int cls = 0; cls < classes.Length; cls++)
                {
                    var controller = Resources.Load<RuntimeAnimatorController>(
                        "Frontier/HeroAnimator/Production" + classes[cls]);
                    Assert.IsNotNull(controller, "Missing native Animator " + classes[cls]);
                    animator.runtimeAnimatorController = controller;
                    animator.enabled = true;
                    animator.speed = 0;
                    animator.Rebind();
                    animator.Update(0f);
                    for (int action = 0; action < actions.Length; action++)
                        for (int facing = 0; facing < facings.Length; facing++)
                        {
                            string key = "Hero_" + classes[cls] + "_" +
                                actions[action] + "_" + facings[facing];
                            Texture2D atlas = Resources.Load<Texture2D>(
                                "Frontier/HeroAnimator/Canonical/" + key);
                            Assert.IsNotNull(atlas, "Missing actual canonical resource " + key);
                            int expected = action == 0 ? 1 :
                                action == 1 ? 4 : action == 4 || action == 5 ? 5 :
                                action == 6 ? 4 : atlas.width / cell;
                            if (action == 2 || action == 3)
                                Assert.IsTrue(expected == 6 || expected == 7,
                                    key + " incomplete or invented contact art");
                            Assert.AreEqual(expected * cell, atlas.width, key + " frame width");
                            Assert.AreEqual(cell, atlas.height, key + " frame height");
                            int state = Animator.StringToHash("Base Layer." +
                                actions[action] + "_" + facings[facing]);
                            for (int index = 0; index < expected; index++)
                            {
                                // The Editor bakes each key at index/12 fps,
                                // plus a final closure/hold at expected/12.
                                animator.SetInteger("Action", action);
                                animator.SetInteger("Facing", facing);
                                animator.Play(state, 0, (index + .2f) / expected);
                                animator.Update(0f);
                                Sprite sprite = renderer.sprite;
                                Assert.IsNotNull(sprite, key + " at frame " + index);
                                Assert.AreSame(atlas, sprite.texture,
                                    key + " has wrong class/direction atlas");
                                Assert.AreEqual(index * cell, sprite.rect.x, .05f,
                                    key + " wrong frame");
                                Assert.AreEqual(0f, sprite.rect.y, .05f,
                                    key + " sprite vertical offset");
                                Assert.AreEqual(cell, sprite.rect.width, .05f, key);
                                Assert.AreEqual(cell, sprite.rect.height, .05f, key);
                                Assert.AreEqual(128f, sprite.pivot.x, .05f,
                                    key + " root x changed");
                                Assert.AreEqual(16f, sprite.pivot.y, .05f,
                                    key + " foot changed");
                                Assert.AreEqual(128f, sprite.pixelsPerUnit, .05f,
                                    key + " class scale changed");
                            }
                        }
                }
            }
            finally
            {
                animator.runtimeAnimatorController = null;
                Object.Destroy(probe);
            }
        }

        [UnityTest] public IEnumerator ClassesRegionsAndNativeFrames()
        {
            GameCore original = app.Game;
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../docs/native-captures")); Directory.CreateDirectory(folder);
            foreach (string name in new[] { "PlayerVanguard", "PlayerRanger", "PlayerArcanist", "PlayerRear", "PlayerSouth", "EnemyArcherVertical", "CompanionMira", "CompanionIvo", "CompanionSen", "Npcs", "Monsters", "Bosses", "Terrain", "TerrainVariants", "Details", "Effects", "Icons", "WalkVanguard", "WalkRanger", "WalkArcanist", "WalkMira", "WalkIvo", "WalkSen", "WalkMonsters", "WalkBossA", "WalkBossB" })
            {
                var texture = Resources.Load<Texture2D>("Frontier/Art/" + name); Assert.NotNull(texture, name);
                Assert.AreEqual(FilterMode.Point, texture.filterMode, name + " must preserve pixel density");
                Assert.AreEqual(1, texture.mipmapCount, name + " must not blur through mipmaps");
                if (name.StartsWith("Player") || name.StartsWith("Walk") || name == "Monsters" || name == "Bosses" || name == "EnemyArcherVertical") Assert.IsTrue(texture.isReadable, name + " hit-flash mask requires alpha readback");
            }
            for (int vocation = 0; vocation < 3; vocation++)
            {
                foreach (int room in new[] { 0, 1, 2, 4, 5, 7, 9 })
                {
                    app.BeginVisualReview(vocation, room, false);
                    Assert.AreEqual(vocation, app.Game.AppearanceWeapon); Assert.AreEqual(room, app.Game.save.room);
                    yield return null; yield return new WaitForEndOfFrame();
                    ScreenCapture.CaptureScreenshot(Path.Combine(folder, "class-" + vocation + "-region-" + room + ".png"));
                    yield return null;
                }
                app.BeginVisualReview(vocation, 1); app.Game.enemies.Clear();
                app.Game.save.x = 12; app.Game.save.y = 14;
                var target = new Enemy(451, 0, vocation == 0 ? 13 : 17, 14, 999) { timer = 9999 }; app.Game.enemies.Add(target);
                app.Game.Attack(new Point(1, 0)); Assert.AreEqual(4, app.Game.PlayerPose); Assert.AreEqual(999, target.hp);
                float contact = app.Game.playerAction.contact;
                for (float elapsed = 0; elapsed < contact - .02f; elapsed += .01f) app.AdvanceVisualReview(.01f, new Point());
                Assert.AreEqual(999, target.hp); app.AdvanceVisualReview(.03f, new Point());
                Assert.IsTrue(app.Game.playerAction.released); Assert.AreEqual(5, app.Game.PlayerPose);
                Assert.IsTrue(vocation == 0 ? target.hp < 999 : app.Game.projectiles.Count > 0);
                yield return null; yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot(Path.Combine(folder, "class-" + vocation + "-contact.png"));
                yield return null;
            }
            app.EndVisualReview(); Assert.AreSame(original, app.Game, "Review must restore the original journey object");
        }
        [UnityTest] public IEnumerator AllBossesWindUpStrikeAndDeath()
        {
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../docs/native-captures")); Directory.CreateDirectory(folder);
            for (int room = 1; room <= 8; room++)
            {
                app.BeginVisualReview(0, room, true); GameCore core = app.Game;
                Enemy boss = core.enemies.Find(e => e.boss); Assert.NotNull(boss, "Real regional boss spawn " + room); core.save.companion = -1;
                Assert.IsTrue(app.SetVisualReviewPosition(new Point(boss.x - 1.1f, boss.y + (room == 2 ? 1.5f : 0)))); Assert.IsTrue(core.CanStand(core.Position)); core.enemies.RemoveAll(e => !e.boss);
                for (int step = 0; step < 240 && boss.state != 1; step++) app.AdvanceVisualReview(1f / 60, new Point());
                Assert.AreEqual(1, boss.state, "Boss must enter its existing AI wind-up");
                float hp = core.save.health;
                while (boss.timer > .08f) app.AdvanceVisualReview(.01f, new Point());
                Assert.AreEqual(hp, core.save.health, "Boss cannot strike before its pose releases");
                yield return null; yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Path.Combine(folder, "boss-" + room + "-windup.png")); yield return null;
                for (int step = 0; step < 10; step++) app.AdvanceVisualReview(.01f, new Point());
                Assert.Greater(boss.releaseTime, 0); Assert.Less(core.save.health, hp);
                yield return null; yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Path.Combine(folder, "boss-" + room + "-strike.png")); yield return null;
                core.hitStop = 0; core.invulnerable = 2; core.playerHitTime = 0; boss.hp = 1; boss.stun = 3;
                Assert.IsTrue(app.SetVisualReviewPosition(new Point(boss.x - .8f, boss.y + (room == 2 ? 1.5f : 0)))); core.attackCooldown = 0; core.Attack((boss.Position - core.Position).Normalized);
                for (int step = 0; step < 20; step++) app.AdvanceVisualReview(.01f, new Point());
                Assert.IsFalse(core.enemies.Contains(boss)); Assert.IsTrue(core.fallen.Exists(f => f.actor == boss));
                yield return null; yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Path.Combine(folder, "boss-" + room + "-death.png")); yield return null;
            }
        }
        [UnityTest] public IEnumerator CanonicalProductionAnimatorFramesAndPivot()
        {
            // Run only after the Editor production bake. An unbaked checkout
            // remains playable via OnGUI, but must NOT be marked native-verified.
            var classes = new[] { "Vanguard", "Ranger", "Arcanist" };
            var directions = new[] { "E", "N", "S" };
            var actions = new[] { "Idle", "Run", "Attack", "Cast", "Dodge", "Hit", "Death" };
            for (int vocation = 0; vocation < classes.Length; vocation++)
            {
                RuntimeAnimatorController controller = Resources.Load<RuntimeAnimatorController>(
                    "Frontier/HeroAnimator/Production" + classes[vocation]);
                Assert.IsNotNull(controller,
                    "Native Animator MISSING: production PlayMode must FAIL, not silently IGNORE an unbaked controller");
                var go = new GameObject("Hero native frame QA " + classes[vocation]);
                var renderer = go.AddComponent<SpriteRenderer>();
                var animator = go.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.speed = 0f; // GameCore supplies the action/attack clock
                for (int facing = 0; facing < directions.Length; facing++)
                {
                    for (int action = 0; action < actions.Length; action++)
                    {
                        int hash = Animator.StringToHash("Base Layer." + actions[action] + "_" + directions[facing]);
                        animator.SetInteger("Action", action);
                        animator.SetInteger("Facing", facing);
                        animator.Play(hash, 0, .1f);
                        animator.Update(0f);
                        Sprite sprite = renderer.sprite;
                        Assert.NotNull(sprite, classes[vocation] + "/" + actions[action] + "/" + directions[facing]);
                        Assert.AreEqual(256f, sprite.rect.width, .01f);
                        Assert.AreEqual(256f, sprite.rect.height, .01f);
                        Assert.AreEqual(128f, sprite.pixelsPerUnit, .01f);
                        Assert.AreEqual(128f, sprite.pivot.x, .01f);
                        Assert.AreEqual(16f, sprite.pivot.y, .01f);
                    }
                    // The four-frame loop MUST sample all four COMPLETE
                    // class-authored Run bodies and NO mechanically warped cells.
                    var steps = new System.Collections.Generic.HashSet<Sprite>();
                    int runHash = Animator.StringToHash("Base Layer.Run_" + directions[facing]);
                    for (int i = 0; i < 4; i++)
                    {
                        animator.SetInteger("Action", 1);
                        animator.SetInteger("Facing", facing);
                        animator.Play(runHash, 0, (i + .2f) / 4f);
                        animator.Update(0f);
                        Assert.NotNull(renderer.sprite);
                        steps.Add(renderer.sprite);
                    }
                    Assert.AreEqual(4, steps.Count, classes[vocation] + "/" + directions[facing] +
                        ": native Run samples must reach every new cell");
                }
                Object.Destroy(go);
                yield return null;
            }
        }


        // Runs ONLY inside licensed Unity PlayMode with the real Frontier.unity
        // scene loaded. The offline C# stubs cannot establish GPU visibility.
        // Unlike atlas-only import tests, exercise the actual scene's private
        // shared SpriteRenderer -> Camera -> RenderTexture actor production
        // method using four accepted, whole-body families.
        [UnityTest] public IEnumerator RealFrontierSceneRendersNonheroWholeBodyPixels()
        {
#if UNITY_EDITOR
            // Exercise the exact source cells used by stop-plant selection
            // through the real Frontier scene's SpriteRenderer + Animator GPU path.
            var scene=EditorSceneManager.LoadSceneInPlayMode(
                "Assets/Scenes/Frontier.unity",
                new LoadSceneParameters(LoadSceneMode.Single));
            host=null;
            yield return null;
            yield return new WaitForEndOfFrame();
            Assert.IsTrue(scene.IsValid()&&scene.isLoaded);
            Assert.AreEqual("Frontier",SceneManager.GetActiveScene().name);
            var runtimes = Object.FindObjectsOfType<FrontierRuntime>();
            Assert.AreEqual(1, runtimes.Length,
                "Frontier scene and bootstrap must resolve to exactly one runtime");
            app = runtimes[0];
            Assert.NotNull(app,"Production scene did not boot FrontierRuntime");
            Assert.NotNull(app.Game,"Production Campaign did not initialize");
            var flags=System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.NonPublic;
            var type=typeof(FrontierRuntime);
            Assert.IsTrue((bool)type.GetField(
                "useSpriteRendererActors",flags).GetValue(app),
                "Scene must enable nonhero SpriteRenderer production bridge");
            var cachedSprites=type.GetField("actorFrameSprites",flags);
            var cachedCaptures=type.GetField("actorCapturedFrames",flags);
            // Includes the production walk atlases, accepted detailed enemies,
            // the 3-phase Boss and other full-body objects. Every 4-column
            // family samples both planted cells (row 1, columns 0 and 2).
            string[] sources={"WalkMonsters","WalkBossA","MossboundRaider",
                "CinderstoneGuard","BriarBeast","Bosses","Npcs","CompanionMira"};
            int[] columns={4,4,4,4,4,4,4,3},rows={3,4,6,6,18,8,4,4};
            for(int family=0;family<sources.Length;family++)
            {
                Texture2D atlas=Resources.Load<Texture2D>(
                    "Frontier/Art/"+sources[family]);
                Assert.NotNull(atlas,"Accepted production art missing: "+sources[family]);
                Assert.AreEqual(0,atlas.width%columns[family]);
                Assert.AreEqual(0,atlas.height%rows[family]);
                int width=atlas.width/columns[family];
                int height=atlas.height/rows[family];
                var probeHost=new GameObject("Starfall GPU actor probe "+
                    sources[family]);
                var probe=probeHost.AddComponent<NativeActorGameViewProbe>();
                probe.runtime=app;probe.atlas=atlas;
                probe.columns=columns[family];probe.rows=rows[family];
                int[] frames=columns[family]==4?
                    new[]{0,4,6,columns[family]*rows[family]-1}:
                    new[]{0,columns[family]*rows[family]-1};
                try
                {
                    foreach(int frame in frames)
                    {
                        probe.frame=frame;probe.flip=false;
                        probe.completed=false;probe.drawn=false;
                        probe.failure=null;probe.capturedSprite=null;
                        probe.capturedFlip=false;
                        for(int attempt=0;attempt<8&&!probe.completed;attempt++)
                        {
                            yield return null;
                            yield return new WaitForEndOfFrame();
                        }
                        Assert.IsTrue(probe.completed,
                            "Native OnGUI/Repaint never submitted "+sources[family]+"/"+frame);
                        Assert.IsNull(probe.failure,
                            "Native whole-body capture threw for "+sources[family]+"/"+frame);
                        Assert.IsTrue(probe.drawn,
                            "Native actor path fell back in actual GameView: "+sources[family]+"/"+frame);
                        string key=atlas.GetInstanceID()+":"+
                            columns[family]+":"+rows[family]+":"+frame;
                        var sprites=(System.Collections.IDictionary)
                            cachedSprites.GetValue(app);
                        var captures=(System.Collections.IDictionary)
                            cachedCaptures.GetValue(app);
                        var sprite=sprites[key] as Sprite;
                        Assert.NotNull(sprite,
                            "Actual GameView did not capture authored source: "+sources[family]+"/"+frame);
                        Assert.AreSame(atlas,sprite.texture,
                            "Captured Sprite used a different actor identity");
                        Assert.AreEqual(frame%columns[family]*width,
                            sprite.rect.x,.05f,"Wrong source-cell X: "+sources[family]+"/"+frame);
                        Assert.AreEqual(atlas.height-
                            (frame/columns[family]+1)*height,
                            sprite.rect.y,.05f,"Wrong source-cell Y: "+sources[family]+"/"+frame);
                        Assert.AreEqual(width,sprite.rect.width,.05f);
                        Assert.AreEqual(height,sprite.rect.height,.05f);
                        Assert.AreEqual(height,sprite.pixelsPerUnit,.05f);
                        Assert.AreEqual(width*.5f,sprite.pivot.x,.05f);
                        Assert.AreEqual(height*8f/128f,sprite.pivot.y,.05f);
                        Assert.AreSame(sprite,probe.capturedSprite,
                            "Actual Animator changed the requested source cell: "+sources[family]+"/"+frame);
                        Assert.IsFalse(probe.capturedFlip,
                            "Unexpected mirrored actor source: "+sources[family]+"/"+frame);
                        var entry=captures[key+":normal"];
                        Assert.NotNull(entry,
                            "GPU target missing from native capture cache: "+sources[family]+"/"+frame);
                        var gpu=(RenderTexture)entry.GetType().GetField(
                            "texture",flags).GetValue(entry);
                        Assert.NotNull(gpu);
                        Assert.IsTrue(gpu.IsCreated());
                        int pixels=Mathf.Max(128,Mathf.Max(width,height));
                        Assert.AreEqual(pixels,gpu.width,
                            "Painted whole-body source was downsampled");
                        Assert.AreEqual(pixels,gpu.height);
                        var previous=RenderTexture.active;
                        var readback=new Texture2D(pixels,pixels,
                            TextureFormat.RGBA32,false);
                        try
                        {
                            RenderTexture.active=gpu;
                            readback.ReadPixels(new Rect(0,0,pixels,pixels),
                                0,0,false);
                            readback.Apply(false,false);
                            bool visible=false;
                            foreach(Color32 color in readback.GetPixels32())
                                if(color.a>16) {visible=true;break;}
                            Assert.IsTrue(visible,
                                "Native GPU body rendered fully transparent: "+sources[family]+"/"+frame);
                        }
                        finally
                        {
                            RenderTexture.active=previous;
                            Object.Destroy(readback);
                        }
                        yield return null;
                    }
                }
                finally
                {
                    Object.Destroy(probeHost);
                }
                yield return null;
            }
#else
            Assert.Fail("Native production scene GPU verification requires Unity Editor");
            yield break;
#endif
        }

        [UnityTest] public IEnumerator NativeWalkingAndPlantedAttackTransitions()
        {
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../docs/native-captures"));Directory.CreateDirectory(folder);
            Point[] headings={new Point(1,0),new Point(-1,0),new Point(0,-1),new Point(0,1)};
            for(int vocation=0;vocation<3;vocation++)foreach(Point direction in headings)
            {
                app.BeginVisualReview(vocation,1);var core=app.Game;core.enemies.Clear();core.save.companion=-1;core.save.x=12;core.save.y=14;
                for(int frame=0;frame<24;frame++)app.AdvanceVisualReview(1f/60,direction);
                Assert.IsTrue(core.ActuallyMoving&&core.PlayerPose>=2&&core.PlayerPose<=3);
                Point start=core.Position;core.Attack(direction);
                while(core.playerAction!=null&&!core.playerAction.released)app.AdvanceVisualReview(.01f,direction);
                Assert.IsTrue(core.playerAction!=null&&core.playerAction.released);Assert.AreEqual(ActionPhase.Active,core.PlayerPhase);
                Assert.IsTrue(vocation==0?(core.Position-start).Length>.1f:(core.Position-start).Length<.001f,"Full body bow/staff preparation plants the feet; sword contact advances the body");
                yield return null;yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(folder,"continuity-contact-"+vocation+"-"+GameCore.DirectionRow(direction)+"-"+direction.x+".png"));yield return null;
                while(core.playerAction!=null&&core.PlayerPhase!=ActionPhase.Recovery)app.AdvanceVisualReview(.01f,new Point());
                float hiddenStride=core.walkDistance;
                int oldFootsteps=core.footContacts;
                app.AdvanceVisualReview(.01f,direction);
                Assert.IsTrue(core.ActuallyMoving&&
                    (core.PlayerPose==5||core.PlayerPose==7)&&
                    core.HeroAnimationState==2,
                    "Released swing must remain one coherent full-body Attack, not Run");
                Assert.AreEqual(hiddenStride,core.walkDistance,.0001f,
                    "Invisible Run steps cannot advance behind a rooted recovery pose");
                Assert.AreEqual(oldFootsteps,core.footContacts,
                    "Do not play hidden footstep audio under a static full-body Attack");
                Assert.Less((core.VisualFacing-direction).Length,.001f);
                yield return null;
                yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot(Path.Combine(folder,
                    "continuity-moving-recovery-"+vocation+"-"+
                    GameCore.DirectionRow(direction)+"-"+direction.x+".png"));
                yield return null;
            }
        }
    }


}
#endif
