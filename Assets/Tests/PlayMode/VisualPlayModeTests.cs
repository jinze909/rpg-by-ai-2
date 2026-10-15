#if UNITY_INCLUDE_TESTS
using System.Collections;
using System.IO;
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
                                action == 1 ? 4 : action == 2 || action == 3 ? 7 :
                                action == 4 || action == 5 ? 5 : 4;
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
                if (controller == null)
                    Assert.Ignore("Native Animator assets not baked: first run Starfall/Animation/Bake Accepted Hero To Live 3-Class Animator");
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
                app.AdvanceVisualReview(.01f,direction);Assert.IsTrue(core.ActuallyMoving&&core.PlayerPose>=2&&core.PlayerPose<=3,"Recovery returns to real locomotion");
            }
        }
    }
}
#endif
