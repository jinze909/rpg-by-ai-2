// Executes production Core and records production OnGUI. It is not a Unity GPU test.
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Starfall;
using UnityEngine;
public static class VisualRuntimeChecks
{
    const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    static void Capture(FrontierRuntime app, string folder, string name)
    { GUI.commands.Clear(); app.Game.toastTime = 0; typeof(FrontierRuntime).GetMethod("OnGUI", Private).Invoke(app, null); GUI.Export(folder, name); }
    public static void Run(FrontierRuntime app, string folder)
    {
        string originalPath = Application.persistentDataPath;
        string safePath = Path.Combine(Path.GetTempPath(), "starfall-review-check-" + Guid.NewGuid());
        Application.persistentDataPath = safePath;
        app.Game.NewGame(0); app.Game.Close(); GameCore original = app.Game;
        original.PrepareSave(); Check(SaveStore.Write(original.save), "Write the fixture's existing journey");
        var disk = new Dictionary<string, string>(); foreach (string file in Directory.GetFiles(safePath, "*", SearchOption.AllDirectories)) disk[file] = Convert.ToBase64String(File.ReadAllBytes(file));
        try
        {
            for (int vocation = 0; vocation < 3; vocation++)
            {
                app.BeginVisualReview(vocation, 0);
                Check(app.Game != original && app.Game.save.profession == vocation && app.Game.AppearanceWeapon == vocation, "Class selection matches the playable outfit");
                Capture(app, folder, "review-class-" + vocation);
                app.BeginVisualReview(vocation, 1); app.Game.enemies.Clear(); app.Game.save.companion = -1;
                app.Game.save.x = 12; app.Game.save.y = 14;
                var foe = new Enemy(801, 0, vocation == 0 ? 13 : 17, 14, 180) { stun = 20, timer = 9999 }; app.Game.enemies.Add(foe);
                for (int frame = 0; frame < 50; frame++)
                {
                    if (frame == 0) app.Game.Attack(new Point(1, 0));
                    if (frame == 15) app.Game.Skill();
                    if (frame == 36) app.Game.Dash(new Point(0, 1));
                    app.AdvanceVisualReview(.04f, new Point());
                    Capture(app, folder, "qa-motion-class-" + vocation + "-" + frame.ToString("D2"));
                }
                app.Game.save.flags.Add("weapon_1"); app.Game.Equip(1);
                Check(app.Game.AppearanceWeapon == 1, "Primary equipment change updates world and portrait look");
                Point[] directions = { new Point(1, 0), new Point(-1, 0), new Point(0, -1), new Point(0, 1) };
                for (int direction = 0; direction < 4; direction++)
                {
                    app.BeginVisualReview(vocation, 1); app.Game.enemies.Clear(); app.Game.save.x = 12; app.Game.save.y = 14;
                    app.Game.Attack(directions[direction]);
                    for (int step = 0; step < 26; step++) app.AdvanceVisualReview(.01f, new Point());
                    Capture(app, folder, "qa-direction-class-" + vocation + "-" + direction);
                    string commands = File.ReadAllText(Path.Combine(folder, "qa-direction-class-" + vocation + "-" + direction + ".json"));
                    if (direction == 2 || direction == 3) Check(commands.Contains("resource:Frontier/Art/Player" + (direction == 2 ? "Rear" : "South")), "Attack pose follows vertical hitbox direction");
                }
            }
            for (int region = 0; region < 10; region++)
            {
                app.BeginVisualReview(region % 3, region);
                Check(app.Game.CanStand(app.Game.Position) && app.Game.CanStand(app.Game.companionPosition), "Region review actors stand on real ground " + region);
                Capture(app, folder, "review-region-" + region);
            }
            // Offline render command capture checks that combat openings and
            // ordinary-enemy poise are visible without relying on animation.
            app.BeginVisualReview(0, 1);
            app.Game.enemies.Clear(); app.Game.save.x = 12; app.Game.save.y = 14;
            app.Game.save.reducedMotion = true;
            app.Game.enemies.Add(new Enemy(88001, 2, 13, 14, 200)
            { releaseTime = .22f, stagger = 18, timer = 999 });
            GUI.textureCalls.Clear();
            Capture(app, folder, "qa-combat-opening-and-poise");
            Check(GUI.textureCalls.FindAll(t => Math.Abs(t.color.r - .48f) < .001f &&
                Math.Abs(t.color.g - 1f) < .001f && Math.Abs(t.color.b - .79f) < .001f &&
                Math.Abs(t.color.a - .85f) < .001f).Count >= 24,
                "The recovery opening has a static mint ring even with reduced motion");
            Check(GUI.textureCalls.Exists(t => Math.Abs(t.rect.height - 3) < .001f &&
                t.rect.width > 15 && t.rect.width < 25 &&
                Math.Abs(t.color.r - 122f / 255) < .002f &&
                Math.Abs(t.color.g - 202f / 255) < .002f),
                "An engaged ordinary enemy shows the actual 18/30 poise progress");
            for (int companion = 0; companion < 3; companion++)
            {
                app.BeginVisualReview(0, 0); GameCore g = app.Game;
                g.save.flags.Add("companion_" + companion); g.SelectCompanion(companion); g.save.health = 20;
                for (int frame = 0; frame < 40; frame++)
                {
                    if (frame == 10) { g.aim = companion == 1 ? new Point(0, -1) : new Point(1, 0); g.CommandCompanion(); }
                    app.AdvanceVisualReview(.04f, frame < 8 ? new Point(1, 0) : new Point());
                    if (frame == 10) Check(g.companionAction != null && !g.companionAction.released && g.projectiles.Count == 0, "Companion prep remains visible before effects");
                    if (frame == 16)
                    {
                        Check(g.companionAction != null && g.companionAction.released, "Companion's visible release crosses the real contact boundary");
                        if (companion == 0) Check(g.save.health > 20, "Mira's actual cast heals");
                        if (companion == 1) Check(g.projectiles.Count == 3 && g.projectiles.TrueForAll(p => p.velocity.y < 0), "Ivo's North pose actually launches North arrows");
                    }
                    Capture(app, folder, "qa-motion-companion-" + companion + "-" + frame.ToString("D2"));
                }
            }
            for (int vertical = 0; vertical < 2; vertical++)
            {
                app.BeginVisualReview(0, 1); GameCore g = app.Game; g.enemies.Clear(); g.save.companion = -1;
                g.save.x = 12; g.save.y = vertical == 0 ? 11 : 17;
                var archer = new Enemy(840 + vertical, 1, 12, 14, 120) { state = 1, timer = .2f, windupDuration = .65f, target = g.Position, facing = new Point(0, vertical == 0 ? -1 : 1) };
                archer.facing=GameCore.AimFromWeapon(archer.Position,1,g.Position);
                g.enemies.Add(archer);
                string name = "review-archer-" + (vertical == 0 ? "north" : "south");
                Capture(app, folder, name + "-windup");
                string commands = File.ReadAllText(Path.Combine(folder, name + "-windup.json"));
                Check(commands.Contains("resource:Frontier/Art/EnemyArcherVertical"), "The archer actually uses the vertical bow preparation in the world");
                Check(commands.Contains("\\"uv\\":[") || commands.Contains("\\"uv\":"), "Real archer frame crop must be inspectable in visual QA");
                for (int step = 0; step < 22; step++) app.AdvanceVisualReview(.01f, new Point());
                Check(archer.releaseTime > 0 && g.projectiles.Count == 1 && (g.projectiles[0].velocity.Normalized-archer.facing).Length < .001f && g.projectiles[0].velocity.y * archer.facing.y > 0, "Vertical bow release follows its locked muzzle-to-target direction");
                Capture(app, folder, name + "-release");
            }
            for (int room = 1; room <= 8; room++)
            {
                app.BeginVisualReview(0, room, true); GameCore g = app.Game; g.save.companion = -1;
                var boss = g.enemies.Find(e => e.boss); Check(boss != null, "Normal boss spawning in review region " + room);
                g.enemies.RemoveAll(e => !e.boss); Check(app.SetVisualReviewPosition(new Point(boss.x - 1.1f, boss.y + (room == 2 ? 1.5f : 0))) && g.CanStand(g.Position), "Boss fixture uses a real walkable footprint " + room);
                for (int step = 0; step < 240 && boss.state != 1; step++) app.AdvanceVisualReview(1f / 60, new Point());
                Check(boss.state == 1, "Existing boss AI reaches wind-up " + room);
                float health = g.save.health;
                while (boss.timer > .08f) app.AdvanceVisualReview(.01f, new Point());
                Check(g.save.health == health, "No pre-contact boss damage " + room);
                Capture(app, folder, "review-boss-" + room + "-windup");
                var camera = (Point)typeof(FrontierRuntime).GetField("camera", Private).GetValue(app);
                float bossScreen = 640 + (boss.x - camera.x) * 36;
                Check(bossScreen > 420 && bossScreen < 915, "Boss silhouette must fit between HUD panels " + room);
                for (int step = 0; step < 10; step++) app.AdvanceVisualReview(.01f, new Point());
                Check(boss.releaseTime > 0 && g.save.health < health, "Boss contact, pose and actual damage synchronize " + room);
                Capture(app, folder, "qa-boss-" + room + "-release");
                g.projectiles.Clear(); g.hazards.Clear(); g.hitStop = 0; g.invulnerable = 2; g.playerHitTime = 0;
                Check(app.SetVisualReviewPosition(new Point(boss.x - .8f, boss.y + (room == 2 ? 1.5f : 0))), "Valid boss counterattack footing " + room); boss.hp = 1; boss.stun = 3; g.attackCooldown = 0; g.Attack((boss.Position - g.Position).Normalized);
                for (int step = 0; step < 20; step++) app.AdvanceVisualReview(.01f, new Point());
                Check(!g.enemies.Contains(boss) && g.fallen.Exists(f => f.actor == boss), "Boss death remains visible without living hitboxes " + room);
                Capture(app, folder, "qa-boss-" + room + "-death");
            }
            // A staged encounter uses actual actor/effect/HUD rendering and real AI.
            // Only positions are arranged; this screenshot is explicitly an offline QA fixture.
            app.BeginVisualReview(2, 5, true); var combined = app.Game;
            var beast = combined.enemies.Find(e => e.boss); beast.x = 22.2f; beast.y = 14; beast.timer = 0;
            combined.enemies.RemoveAll(e => !e.boss);
            combined.enemies.Add(new Enemy(812, 2, 20.8f, 16.8f, 120) { timer = 9999 });
            combined.enemies.Add(new Enemy(813, 1, 17, 16.8f, 110) { timer = 9999 });
            combined.save.x = 18.8f; combined.save.y = 14; combined.companionPosition = new Point(17, 14.4f);
            combined.Skill(); for (int step = 0; step < 50; step++) app.AdvanceVisualReview(.01f, new Point());
            Capture(app, folder, "review-combined");
            typeof(FrontierRuntime).GetMethod("Save", Private).Invoke(app, null);
            typeof(FrontierRuntime).GetMethod("WriteSlot", Private).Invoke(app, new object[] { 1 });
            Check(Directory.GetFiles(safePath, "*", SearchOption.AllDirectories).Length == disk.Count, "Review cannot create manual or automatic saves");
            foreach (var pair in disk) Check(Convert.ToBase64String(File.ReadAllBytes(pair.Key)) == pair.Value, "Review cannot overwrite an existing save");
            app.EndVisualReview(); Check(app.Game == original && !app.VisualReviewActive, "Review restores the original core including quest and encounter state");
            Console.WriteLine("PASS Three class outfits, ten regions, eight actual boss timelines and review save isolation (offline renderer)");
        }
        finally { app.EndVisualReview(); Application.persistentDataPath = originalPath; if (Directory.Exists(safePath)) Directory.Delete(safePath, true); }
    }
}
