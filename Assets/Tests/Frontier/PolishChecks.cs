using System;
using System.Collections.Generic;
namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        private static void PracticeRun(GameCore g)
        {
            g.Enter(0); g.Close(); g.StartPractice(); Check(g.practicing, "Practice starts in town");
            g.save.x = 15; g.save.y = 13; g.Attack(new Point(1, 0)); for (int i = 0; i < 30 && g.practiceStage == 0; i++) g.Tick(.05f, new Point());
            Check(g.practiceStage == 1, "Actual attack impact starts the telegraph exercise");
            Enemy warning = g.enemies[0]; warning.state = 1; warning.target = g.Position; warning.timer = .25f; g.invulnerable = 0; g.dashCooldown = 0; g.save.energy = 100;
            g.Dash(new Point(-1, 0)); for (int i = 0; i < 8; i++) g.Tick(.05f, new Point());
            Check(g.practiceStage == 2, "Avoiding a real timed strike advances practice");
            g.save.x = 15; g.save.y = 13; g.aim = new Point(1, 0); g.Skill(); for (int i = 0; i < 40 && g.practicing; i++) g.Tick(.05f, new Point());
            Check(!g.practicing && g.save.practiceBits == 7, "Actual Q impact completes practice");
        }
        public static void PracticeExitRetiresDummyHazardsAndEarnedCounter(Campaign data)
        {
            var g=Fresh(data);
            g.enemies.Clear();g.save.x=17;g.save.y=15;
            Point exit=g.Position;float health=g.save.health;
            g.StartPractice();
            Check(g.practicing&&g.enemies.Count==1,
                "Entering authentic practice creates the temporary dummy");
            var warning=new Hazard{position=exit,radius=1.5f,timer=1.4f,damage=999,kind="rift"};
            g.hazards.Add(warning);
            g.perfectDodgeArmed=true;g.combo=2;g.comboTimer=1f;
            g.riposteTime=1.5f;g.playerFlash=.4f;g.impactShake=.2f;
            g.EndPractice();
            Check(!g.practicing&&g.mode=="world"&&g.hazards.Count==0&&
                  g.enemies.Count==0&&g.projectiles.Count==0&&
                  (g.Position-exit).Length<.001f,
                "Aborting a genuine dummy session restores town location without leaking timed danger");
            Check(g.combo==0&&g.comboTimer==0&&g.riposteTime==0&&
                  !g.perfectDodgeArmed&&g.playerFlash==0&&g.impactShake==0,
                "No fake combo/temporary perfect-dodge or hit-reaction bonus survives exiting practice");
            for(int i=0;i<35;i++)g.Tick(.05f,new Point());
            Check(g.save.health==health&&!g.Dying,
                "Town travel after practice cannot be struck by a retired training explosion");
            PracticeRun(g);
            Check(g.hazards.Count==0&&!g.practicing&&g.riposteTime==0,
                "Completing the entire actual combat tutorial also retires its own red circles");
        }
        public static void InteractivePracticeAndReplay(Campaign data)
        {
            for (int profession = 0; profession < 3; profession++)
            {
                var g = Fresh(data, profession); float health = g.save.health; int coins = g.save.coins, points = g.save.skillPoints, kills = g.save.kills[0];
                g.Attack(new Point(1, 0)); g.Skill(); Check((g.save.tutorialBits & (8 | 32)) == 0, "Empty attacks and skills cannot complete learning");
                PracticeRun(g); Check(g.save.health == health && g.save.kills[0] == kills && g.save.coins == coins + 15 && g.save.skillPoints == points + 1, "Safe practice has no combat loot and awards once");
                PracticeRun(g); Check(g.save.coins == coins + 15 && g.save.skillPoints == points + 1, "Replay cannot farm permanent rewards");
                g.StartPractice(); int stew = g.save.inventory[6]; g.save.tea = 2; g.Eat(); g.DrinkTea(); Check(g.save.inventory[6] == stew && g.save.tea == 2, "Practice does not consume supplies even when their shortcuts are pressed");
                g.StartPractice(); g.Enter(1); Check(!g.practicing && g.save.room == 1, "Travel cleanly ends practice");
            }
        }
        public static void ExplorationSealCannotBeActivatedThroughRealWall(Campaign data)
        {
            MapData original=data.maps[0];
            try
            {
                var rows=new string[25];
                for(int y=0;y<25;y++)
                {
                    char[] cells=new string('#',25).ToCharArray();
                    if(y>=2&&y<=22)
                        for(int x=2;x<=22;x++)cells[x]='.';
                    rows[y]=new string(cells);
                }
                char[] wall=rows[10].ToCharArray();
                wall[11]='#';rows[10]=new string(wall);
                WorldObject hidden=new WorldObject{id="hidden",kind="seal1",
                    name="Blocked seal",en="Blocked seal",x=12.25f,y=10.5f};
                WorldObject reachable=new WorldObject{id="reachable",kind="seal1",
                    name="Near-side seal",en="Near-side seal",x=10.5f,y=12.5f};
                data.maps[0]=new MapData {id=0,spawnX=10.5f,
                    spawnY=10.5f,rows=rows,objects=new[]{hidden},
                    decor=new Decoration[0]};
                var g=Fresh(data);g.enemies.Clear();
                g.save.flags.Add("core_1");
                g.save.x=10.5f;g.save.y=10.5f;
                Check(g.CanStand(g.Position)&&
                    g.CanStand(new Point(hidden.x,hidden.y))&&
                    g.Blocked(11.5f,10.5f),
                    "The hero and seal stand on legal floors, but a real wall separates their interaction lanes");
                g.UseExplorationAbility();
                Check(!g.Has("seal_0")&&
                    !g.effects.Exists(e=>e.kind=="discover"),
                    "Pressing R through a solid wall cannot open a route or publish a false discovery");
                data.maps[0].objects=new[]{hidden,reachable};
                g.UseExplorationAbility();
                Check(g.Has("seal_0")&&
                    g.effects.Exists(e=>e.kind=="discover"&&
                        (e.position-new Point(reachable.x,reachable.y)).Length<.001f),
                    "With two nearby seals, R chooses the reachable fixture instead of the closer one behind cover");
                g.save.flags.Remove("seal_0");g.effects.Clear();
                data.maps[0].objects=new[]{hidden};
                wall[11]='.';rows[10]=new string(wall);
                g.UseExplorationAbility();
                Check(g.Has("seal_0")&&g.effects.Exists(e=>
                    e.kind=="discover"&&
                    (e.position-new Point(hidden.x,hidden.y)).Length<.001f),
                    "Opening the same wall immediately permits the original seal without resetting the world");
                Check(g.save.version==2,
                    "Real seal reachability changes no old save fields, permanent core abilities or asset IDs");
            }
            finally { data.maps[0]=original; }
        }

        public static void PhysicalFieldRefugesDoNotHealAcrossDynamicSolidWall(Campaign data)
        {
            // Test all four already-shipped permanent civic shelters through
            // their ACTUAL GameCore properties and frame-based stat recovery.
            int[] rooms={1,2,3,7};
            string[] kinds={"rootsonestone","floodarchive","mineconvoy","frostsignal"};
            string[] flags={"rootsong_complete","floodarchive_public",
                "mineconvoy_complete","frostsignal_complete"};
            for(int i=0;i<rooms.Length;i++)
            {
                int room=rooms[i];
                MapData original=data.maps[room];
                try
                {
                    var rows=new string[25];
                    for(int y=0;y<25;y++)
                    {
                        char[] cells=new string('#',25).ToCharArray();
                        if(y>=2&&y<=22)
                            for(int x=2;x<=22;x++)cells[x]='.';
                        if(y==10)cells[11]='#';
                        rows[y]=new string(cells);
                    }
                    var station=new WorldObject{
                        id=room==3?"mineconvoy_3":"refuge-test",
                        kind=kinds[i],name="Protected service",
                        en="Protected service",x=12.5f,y=10.5f};
                    data.maps[room]=new MapData{id=room,
                        spawnX=10.5f,spawnY=10.5f,
                        rows=rows,objects=new[]{station},
                        decor=new Decoration[0]};
                    var g=Fresh(data);
                    g.save.stage=3;g.save.flags.Add("desert_report");
                    g.Enter(room);g.enemies.Clear();
                    g.save.flags.Add(flags[i]);
                    // A completed Frostchime run grants either clinic HP
                    // or roadwarden stamina. The HP assertion below must
                    // explicitly unlock the CLINIC outcome, not only mark
                    // the mission complete.
                    if(room==7)g.save.flags.Add("frostsignal_clinic");
                    g.save.x=10.5f;g.save.y=10.5f;
                    g.save.health=40;g.save.energy=0;
                    g.save.cold=40;
                    Check(g.CanStand(g.Position)&&
                        g.CanStand(new Point(station.x,station.y))&&
                        g.Blocked(11.5f,10.5f),
                        "Living hero and civic refuge must have legal ground separated by genuine impassable stone");
                    bool blocked=room==1?g.RootsongRefuge:
                        room==2?g.FloodArchiveRefuge:
                        room==3?g.MineConvoyRefuge:g.FrostSignalRefuge;
                    Check(!blocked,
                        "A completed public service must not activate a remote health, stamina or warmth radius through stone");
                    g.Tick(.05f,new Point());
                    Check(Math.Abs(g.save.health-40)<.0001f,
                        "The actual world tick must not restore health through the closed wall");
                    float blockedEnergy=g.save.energy;
                    char[] opened=rows[10].ToCharArray();
                    opened[11]='.';rows[10]=new string(opened);
                    bool clear=room==1?g.RootsongRefuge:
                        room==2?g.FloodArchiveRefuge:
                        room==3?g.MineConvoyRefuge:g.FrostSignalRefuge;
                    Check(clear&&g.save.version==2,
                        "Removing that wall immediately exposes the already-unlocked refuge without new save fields");
                    g.save.health=40;g.save.energy=0;
                    g.Tick(.05f,new Point());
                    if(room==1)
                        Check(g.save.energy>blockedEnergy+.08f,
                            "Rootsong physical contact grants its genuine stamina recovery above baseline regeneration");
                    else
                        Check(g.save.health>40.04f,
                            "A legitimately reached public clinic/shelter restores actual player HP on the world tick");
                    if(room==7)
                        Check(g.save.cold<40,
                            "A physically reached Frostchime hearth now provides genuine protection from cold");
                }
                finally {data.maps[room]=original;}
            }
        }

        public static void DesertShadeRequiresPhysicalContactAcrossLiveWall(Campaign data)
        {
            // NearKind powers actual heat protection AND other resident
            // stations. This checks one world tick before/after opening a
            // real barrier without any new story flag or save migration.
            MapData previous=data.maps[6];
            try
            {
                var rows=new string[25];
                for(int y=0;y<25;y++)
                {
                    char[] cells=new string('#',25).ToCharArray();
                    if(y>=2&&y<=22)
                        for(int x=2;x<=22;x++)cells[x]='.';
                    if(y==10)cells[11]='#';
                    rows[y]=new string(cells);
                }
                WorldObject shade=new WorldObject{id="shade-test",
                    kind="shade",name="Shade canopy",en="Shade canopy",
                    x=12.5f,y=10.5f};
                data.maps[6]=new MapData{id=6,
                    spawnX=10.5f,spawnY=10.5f,rows=rows,
                    objects=new[]{shade},decor=new Decoration[0]};
                var g=Fresh(data);
                g.save.stage=3;
                // Region 6 is gated by the original harbor report, while
                // the desert report unlocks region 7, not this climate map.
                g.save.flags.Add("harbor_report");
                g.Enter(6);g.enemies.Clear();
                g.save.x=10.5f;g.save.y=10.5f;
                g.save.teaUntil=0;g.save.heat=40f;
                Check(g.save.room==6&&g.CanStand(g.Position)&&
                    g.CanStand(new Point(shade.x,shade.y))&&
                    g.Blocked(11.5f,10.5f),
                    "A genuine desert shade exists nearby but behind a full wall with valid standing feet");
                g.Tick(.05f,new Point());
                Check(g.save.heat>40.04f,
                    "An inaccessible canopy cannot grant free desert cooling through a sealed wall");
                char[] open=rows[10].ToCharArray();
                open[11]='.';rows[10]=new string(open);
                g.Tick(.05f,new Point());
                Check(g.save.heat<40f&&g.save.version==2,
                    "Opening the same route makes the real nearby shade immediately reduce heat without changing save schema");
            }
            finally{data.maps[6]=previous;}
        }

        public static void ExplorationPowersAndDiscoveries(Campaign data)
        {
            var g = Unlocked(data); g.save.flags.RemoveAll(f => f.StartsWith("core_") || f.StartsWith("seal_") || f.StartsWith("cache_"));
            int[] rooms = { 1, 2, 3, 5, 6, 7 }, powers = { 1, 2, 3, 2, 1, 3 };
            for (int i = 0; i < rooms.Length; i++)
            {
                g.Enter(rooms[i]); g.enemies.Clear(); g.save.x = 7.5f; g.save.y = 19.5f; string core = "core_" + powers[i]; g.save.flags.Remove(core);
                Check(g.Blocked(6.5f, 19.5f) && g.NavigationPath(new Point(4.5f, 19.5f)).Count == 0, "Cache requires its exploration power");
                g.UseExplorationAbility(); Check(!g.Has("seal_" + rooms[i]), "Locked powers cannot open a path");
                g.save.flags.Add(core); g.UseExplorationAbility(); Check(!g.Blocked(6.5f, 19.5f), "Gained core opens new traversal");
                var route = g.NavigationPath(new Point(4.5f, 19.5f)); Check(route.Count > 0, "Unlocked cache has a collision-safe route");
                foreach (Point p in route) Check(!g.Blocked(p.x - .22f, p.y - .18f) && !g.Blocked(p.x + .22f, p.y + .18f), "Secret path respects hero footprint");
                g.save.x = 4.5f; g.save.y = 19.5f; int coins = g.save.coins; g.Talk(Object(g, "cache" + powers[i])); Check(g.Has("cache_" + rooms[i]) && g.save.coins == coins + 30, "Discovery has a real reward");
                g.Close(); g.Talk(Object(g, "cache" + powers[i])); Check(g.save.coins == coins + 30, "Caches cannot be looted twice"); g.Close();
                var cache = Object(g, "cache" + powers[i]); Check(g.Has("history_" + cache.lore), "Each discovery unlocks its regional record"); g.OpenCodex(cache.lore); Check(g.conversation != null && g.conversation.text == data.lore[cache.lore].zh, "Discovered notes can be reread from the journal"); g.Close();
            }
            Check(g.GetQuest("secrets").complete, "Six discoveries complete their journal entry");
            g.PrepareSave(); var restored = new GameCore(data, CloneSave(g.save)); Check(restored.GetQuest("secrets").complete && !restored.Blocked(6.5f, 19.5f), "Discovery and route survive a save");
        }
        public static void CombatStylesAndEnemyCounters(Campaign data)
        {
            var g = Fresh(data); g.SetStyle(1); Check(g.Style == 0, "Advanced style has an unlock gate"); g.save.flags.Add("core_1");
            var balanced = g.Profile(0, 0); g.SetStyle(1); var focused = g.Profile(0, 1); Check(focused.damage > balanced.damage && focused.interval > balanced.interval && focused.energy > 0, "Power style trades speed and energy for damage");
            g.save.energy = 3; g.Attack(new Point(1, 0)); Check(g.attackCooldown == 0 && g.save.energy == 3, "Unaffordable style cannot attack");
            g.SetStyle(0); g.save.x = 18; g.save.y = 15; g.enemies.Clear(); var heavy = new Enemy(71, 2, 19, 15, 999) { facing = new Point(-1, 0) }; g.enemies.Add(heavy); g.save.seed = 1; g.Attack(new Point(1, 0)); Advance(g, .2f); float guarded = 999 - heavy.hp;
            heavy.hp = 999; heavy.facing = new Point(1, 0); g.attackCooldown = 0; g.comboTimer = 0; g.save.seed = 1; g.playerAction = null; g.Attack(new Point(1, 0)); Advance(g, .2f); Check(999 - heavy.hp > guarded * 2, "Flanking bypasses frontal armor");
            g.save.inventory[6] = 0; g.enemies.Clear(); var shooter = new Enemy(73, 1, 20, 15, 200); shooter.timer = 99; g.enemies.Add(shooter); g.Tick(.05f, new Point()); Check(shooter.x > 20, "Archers retreat when approached");
            shooter.state = 1; shooter.target = new Point(18, 15); shooter.timer = .01f; g.save.x = 18; g.save.y = 16; g.Tick(.05f, new Point()); Check(g.projectiles.Count > 0 && Math.Abs(g.projectiles[0].velocity.y) < .1f, "Arrows honor the visible locked shot instead of tracking after the tell");
            g.save.coins = 100; g.save.skillPoints = 2; g.Learn(1); g.Learn(2); g.ResetTalents(); Check(g.save.skillPoints == 2 && g.save.talents[1] == 0 && g.save.maxHealth == 120, "Respec refunds points and recomputes health");
        }
        public static void WorldSnapshotsAndChoiceResponses(Campaign data)
        {
            var g = Fresh(data); g.save.stage = 1; g.Enter(2); g.enemies.Clear(); g.enemies.Add(new Enemy(81, 0, 15, 15, 120)); g.enemies[0].hp = 43; g.runeProgress = 2; g.PrepareSave();
            var saved = CloneSave(g.save); var restored = new GameCore(data, saved); Check(restored.enemies.Count == 1 && restored.enemies[0].hp == 43 && restored.runeProgress == 2, "Resume restores active encounter and unfinished rune order");
            restored.enemies.Clear(); restored.PrepareSave(); restored.Load(CloneSave(restored.save)); Check(restored.enemies.Count == 0, "An empty encounter snapshot stays cleared");
            restored.Enter(0); restored.save.flags.Add("scout_saved"); int soup = restored.save.inventory[6]; Action(restored, "returnscout", "scoutgift"); Action(restored, "returnscout", "scoutgift"); Check(restored.save.inventory[6] == soup + 1, "Rescued scout returns home and thanks you once");
            restored.save.reputation[1] = 3; int money = restored.save.coins; Action(restored, "cook", "buywood"); Check(restored.save.coins == money - 8, "Faction support changes actual shop prices");
            for (int i = 0; i < 12; i++) restored.RecordMoment("足迹" + i, "Moment " + i); Check(restored.save.moments.Count == 8 && restored.save.moments[0].en == "Moment 4", "Journal stores a bounded persistent history");
            var next = Fresh(data); next.Tick(.05f, new Point()); next.save.stage = 1; next.Tick(.05f, new Point());
            Check(next.save.moments.Count > 0 && next.save.moments[next.save.moments.Count - 1].zh != next.save.moments[next.save.moments.Count - 1].en, "Quest updates persist both languages");
            int records = next.save.moments.Count; next.save.english = true; next.Tick(.05f, new Point()); Check(next.save.moments.Count == records, "Switching language does not invent another quest event");
            next.Enter(1); next.enemies.Clear(); next.invulnerable = 0; next.save.health = 1; int coins = next.save.coins, herbs = next.save.inventory[2];
            next.projectiles.Add(new Projectile { position = next.Position, damage = 999, source = "shot" }); next.Tick(.05f, new Point()); Check(next.Dying && next.PlayerPose == 11, "Lethal damage first shows the fallen pose"); Advance(next, 1);
            Check(next.save.room == 0 && next.save.health == next.save.maxHealth && next.save.coins == Math.Max(0, coins - 10) && next.save.stage == 1 && next.save.inventory[2] == herbs && next.save.deaths == 1 && next.save.lastFailureEn.Contains("arrows"), "Death restores town control, retains progress and records the cause and response");
            next.PrepareSave(); Check(new GameCore(data, CloneSave(next.save)).save.deaths == 1, "Recovery and its journal survive a restart");
        }
        public static void PredictableMenuFocus(Campaign data)
        {
            var n = new MenuNavigation(); n.Scope("inventory");
            var controls = new[] { new FocusControl { id = "left", x = 0, y = 0, w = 100, h = 40, enabled = true }, new FocusControl { id = "disabled", x = 110, y = 0, w = 100, h = 40, enabled = false }, new FocusControl { id = "right", x = 220, y = 0, w = 100, h = 40, enabled = true }, new FocusControl { id = "down", x = 0, y = 60, w = 100, h = 40, enabled = true } };
            n.SetControls(controls); n.Move(new Point(1, 0)); Check(n.focused == "right" && !n.CanActivate("disabled"), "Arrow navigation skips disabled controls"); n.Move(new Point(-1, 0)); n.Move(new Point(0, 1)); Check(n.focused == "down", "Focus follows visual geometry"); n.Cycle(1); Check(n.focused == "left", "Sequential navigation wraps predictably");
            n.Select("right"); n.Scope("map"); n.SetControls(controls); n.Scope("inventory"); n.SetControls(controls); Check(n.focused == "right", "Returning to a menu restores its focus");
            // Runtime InputScope includes conversation.GetHashCode() and page.
            // Visiting hundreds of NPCs must neither retain one-use pages
            // indefinitely nor steal the currently selected inventory action.
            var cache = (Dictionary<string, string>)typeof(MenuNavigation)
                .GetField("remembered", System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic).GetValue(n);
            for (int dialog = 0; dialog < 500; dialog++)
            {
                n.Scope("dialog/" + dialog + "/0"); n.SetControls(controls);
                Check(n.focused == "left",
                    "Each new dialogue starts with fresh focus rather than a previous speaker's choice");
                n.Select("down");
                n.Scope("dialog/" + dialog + "/1"); n.SetControls(controls);
                n.Scope("inventory"); n.SetControls(controls);
                Check(n.focused == "right",
                    "NPC dialogue never steals the last selected permanent menu control");
            }
            Check(cache.Count <= 3,
                "One-use conversation identities must not accumulate forever in the focus cache");

        }
        public static void SoundFeedbackAndRegionalScores(Campaign data)
        {
            float[] a = AudioSynthesis.Event("hit"), b = AudioSynthesis.Event("hurt"); double difference = 0; foreach (float v in a) Check(!float.IsNaN(v) && Math.Abs(v) <= 1, "Sound sample remains finite and unclipped"); for (int i = 0; i < Math.Min(a.Length, b.Length); i++) difference += Math.Abs(a[i] - b[i]); Check(difference > 50, "Hit and injury cues are distinguishable");
            // Every one of the seven real looping Unity tracks must end
            // smoothly without suppressing its whole final 125ms phrase.
            for(int theme=0;theme<=6;theme++)
            {
                var music=AudioSynthesis.Music(theme);
                Check(music.Length==AudioSynthesis.Rate*16,
                    "Every authored region and combat score retains the existing 16-second loop");
                double lastEnergy=0, priorBarEnergy=0;
                int window=AudioSynthesis.Rate/8;
                for(int i=0;i<window;i++)
                {
                    float tail=music[music.Length-window+i];
                    float prior=music[music.Length-4*AudioSynthesis.Rate-window+i];
                    Check(!float.IsNaN(tail) && !float.IsInfinity(tail) &&
                        Math.Abs(tail)<1,"The generated score must remain finite, unclipped PCM");
                    lastEnergy+=tail*tail;
                    priorBarEnergy+=prior*prior;
                }
                Check(Math.Abs(music[0]-music[music.Length-1])<.005f,
                    "Looped AudioSource has a quiet sample-to-sample musical seam");
                Check(priorBarEnergy>.00001 && lastEnergy>priorBarEnergy*.78,
                    "The full music mix must not fade away during the final eighth-second of every 16-second loop");
            }
            // Match the transient state used by the real Unity audio
            // crossfader. A single living attack must not cause repeated
            // combat/exploration score swaps between recovery animations.
            var combatScore=new CombatMusicLinger();
            Check(!combatScore.Advance(1,false,false,true,.05f),
                "No unsighted fight can manufacture battle music");
            Check(combatScore.Advance(1,true,true,true,.02f) &&
                combatScore.Advance(1,false,true,true,1.2f),
                "A genuinely still-living nearby foe holds battle music through recovery");
            Check(combatScore.Advance(1,false,true,false,300f),
                "Reading a paused journal cannot exhaust an ongoing combat score");
            Check(!combatScore.Advance(1,false,false,true,.02f),
                "Defeating or leaving the last enemy must immediately reverse the partially started combat fade");
            Check(combatScore.Advance(1,true,true,true,0f) &&
                combatScore.Advance(1,false,true,true,1.4f) &&
                !combatScore.Advance(1,false,true,true,1.7f),
                "Even a surviving but idle opponent cannot hold combat audio indefinitely");
            Check(combatScore.Advance(1,true,true,true,0f) &&
                !combatScore.Advance(2,false,false,true,.01f),
                "Changing authored rooms clears a previous region's combat tail");
            Check(combatScore.Advance(2,true,true,true,0f),
                "A new room's genuine threat still starts its own combat score");
            combatScore.Reset();
            Check(!combatScore.Advance(2,false,false,true,.01f),
                "Title/practice reset never preserve combat audio in fresh play");
            var g = Fresh(data); g.sounds.Clear(); g.sound = "hit"; g.sound = "quest"; Check(g.sounds.Count == 2 && g.sounds.Dequeue() == "hit", "Multiple events in one frame retain their audio cues");
        }
        public static void TravelClearanceAndCivicEnemyHudUseExactRealDistances(Campaign data)
        {
            var g=Fresh(data);
            g.enemies.Clear();g.save.x=12;g.save.y=12;
            var foe=new Enemy(454,0,17.999f,12,25);
            g.enemies.Add(foe);
            Check(!g.SafeTravel,"An enemy just inside the normal six-tile safety range blocks travel");
            foe.x=18;
            Check(g.SafeTravel,"An enemy exactly at the six-tile limit permits travel");
            foe.x=17;foe.y=15;
            Check(!g.SafeTravel,"Diagonal enemies use actual two-axis distance, not a box");
            foe.x=17;foe.y=16;
            Check(g.SafeTravel,"Out-of-range diagonal enemies do not block travel");
            g.save.frontierEnding=2;
            foe.x=15.999f;foe.y=12;
            Check(!g.SafeTravel,"The established four-tile ending still uses the smaller warning range");
            foe.x=16;
            Check(g.SafeTravel,"Four-tile range retains its exact exclusive boundary");
            g.enemies.Clear();
            Check(g.SafeTravel,"A cleared battlefield restores ordinary safe travel");
            // The shooter may be outside aggro range while its live arrow
            // remains dangerous next to the player.
            var arrow=new Projectile { position=new Point(13,12),
                velocity=new Point(-6,0),damage=12,life=2,source="shot" };
            g.projectiles.Add(arrow);
            Check(!g.SafeTravel,"A live hostile projectile near the hero blocks instant escape");
            arrow.friendly=true;
            Check(g.SafeTravel,"Our own projectile never locks unrelated fast travel");
            arrow.friendly=false;arrow.life=0;
            Check(g.SafeTravel,"Expired hostile projectile does not block safe travel");
            arrow.life=2;arrow.damage=0;
            Check(g.SafeTravel,"Harmless preview projectiles do not create false travel danger");
            arrow.damage=12;arrow.position=new Point(16,12);
            arrow.velocity=new Point(6,0);
            Check(g.SafeTravel,"Outgoing arrow at exact four-tile boundary is outside danger radius and cannot turn back");
            arrow.velocity=new Point(-6,0);
            Check(!g.SafeTravel,"Incoming arrow at exact boundary can cross the real safe zone before the next travel");
            arrow.position=new Point(15.99f,12);
            Check(!g.SafeTravel,"Approaching arrow already inside boundary is blocked with the same radius as local enemies");
            g.projectiles.Clear();
            var watch=Fresh(data);watch.save.stage=4;
            watch.save.flags.Add("exp_intro");
            watch.save.flags.Add("civic_supply_complete");
            watch.save.flags.Add("harbor_report");
            watch.save.flags.Add("civic_supply_watch_3");
            watch.save.flags.Add("far_shelter_3");
            watch.Enter(3);watch.enemies.Clear();
            var refuge=Object(watch,"farshelter");
            watch.save.x=refuge.x;watch.save.y=refuge.y;
            Action(watch,"farshelter","civic_response_start");
            Check(watch.CivicResponseEnemiesAlive==2,
                "Actual live road-watch correctly counts both mission attackers");
            var unrelated=new Enemy(455,0,refuge.x+20,refuge.y,20);
            watch.enemies.Add(unrelated);
            Check(watch.CivicResponseEnemiesAlive==2,
                "Unrelated regional patrols cannot inflate the transient objective HUD");
            watch.enemies.RemoveAll(e=>e.id==90206);
            Check(watch.CivicResponseEnemiesAlive==1,
                "Mission count changes immediately when a real watch actor retires");
            watch.enemies.Clear();
            Check(watch.CivicResponseEnemiesAlive==0,
                "Zero surviving mission attackers shows zero without a stale display cache");
        }

        public static void FieldCampMenuShowsRestOnlyWhenActuallyAvailable(Campaign data)
        {
            var g=Unlocked(data);g.Enter(5);g.enemies.Clear();
            WorldObject fire=Object(g,"campfire");
            g.save.projects[1]=0;g.Talk(fire);
            Check(g.mode=="dialog"&&Array.FindIndex(g.conversation.choices,
                  c=>c.action=="fieldrest")<0&&g.conversation.textEn.Contains("Build the Emberfall"),
                "An unbuilt waystation cannot advertise free camp rest");
            g.Close();g.save.projects[1]=1;g.Talk(fire);
            Check(Array.FindIndex(g.conversation.choices,
                  c=>c.action=="fieldrest")>=0,
                "Unlocked and peaceful camp offers its real twenty-second rest action");
            g.Close();
            var enemy=new Enemy(843,0,g.save.x+1,g.save.y,80){timer=99};
            g.enemies.Add(enemy);g.Talk(fire);
            Check(Array.FindIndex(g.conversation.choices,
                  c=>c.action=="fieldrest")<0&&g.conversation.textEn.Contains("unsafe"),
                "Active nearby enemies hide the rest choice and explain the danger in the existing dialog");
            g.Close();g.enemies.Clear();
            var warning=new Hazard{position=g.Position,radius=2,damage=10,timer=.2f,kind="rift"};
            g.hazards.Add(warning);g.Talk(fire);
            Check(Array.FindIndex(g.conversation.choices,
                  c=>c.action=="fieldrest")<0,
                "A live damaging red circle also hides resting rather than advertising a denied action");
            g.Close();g.hazards.Clear();g.Talk(fire);
            Check(Array.FindIndex(g.conversation.choices,
                  c=>c.action=="fieldrest")>=0,
                "The normal camp rest option returns as soon as combat ends");
        }
        public static void LiveGlasswindTrialCannotSkipTheFieldRestClock(Campaign data)
        {
            var g=Fresh(data);g.save.stage=3;g.save.level=20;
            foreach(string flag in new[]{"harbor_report","desert_report","gate_6"})
                g.save.flags.Add(flag);
            WorldObject mia=Object(g,"glasswindkeeper");
            g.save.x=mia.x;g.save.y=mia.y;
            Action(g,"glasswindkeeper","glasswind_accept");
            Check(g.Has("glasswind_started"),
                "Test starts the actual accepted optional relay campaign at the town NPC");
            g.Enter(6);g.enemies.Clear();
            var valve=Object(g,"glasswindrelay1");
            g.save.x=valve.x;g.save.y=valve.y;
            Action(g,valve.kind,"glasswind_start_1");
            Check(g.GlasswindSite==1,
                "A physical relay valve starts the timed capture encounter");
            // Captured wave transitions briefly leave no active hostiles;
            // safety alone cannot authorize skipping a still-running mission.
            g.enemies.Clear();g.projectiles.Clear();g.hazards.Clear();
            g.save.projects[1]=2;
            Check(g.SafeTravel,"The crafted gap between waves has no nearby combat pressure");
            float time=g.save.totalTime,health=g.save.health,relayTime=g.GlasswindTime;
            g.conversation=new Conversation("营地","Camp","休整","Rest",
                new Choice("休整","Rest","fieldrest"));g.mode="dialog";g.Choose(0);
            Check(g.GlasswindSite==1&&g.GlasswindTime==relayTime&&
                  g.save.totalTime==time&&g.save.health==health,
                "Field rest cannot advance or erase a paid real-time relay capture between combat waves");
        }
        public static void FieldCampRestAdvancesCombatTimeAndCannotSkipLiveCivilianWork(Campaign data)
        {
            var g=Unlocked(data);g.save.projects[1]=2;g.Enter(5);g.enemies.Clear();
            g.save.flags.Add("companion_0");g.SelectCompanion(0);
            g.save.health=23;g.save.energy=100;g.Skill();g.CommandCompanion();
            Check(g.skillCooldown>0&&g.companionCooldown>0,
                "Real camp setup begins two independent skill cooldowns");
            var shortShot=new Projectile{position=g.Position,friendly=true,
                velocity=new Point(1,0),damage=0,life=4};
            var longShot=new Projectile{position=g.Position,friendly=true,
                velocity=new Point(1,0),damage=0,life=48};
            var hazard=new Hazard{position=g.Position,radius=1,timer=.5f,damage=0,kind="rift"};
            g.projectiles.Add(shortShot);g.projectiles.Add(longShot);g.hazards.Add(hazard);
            float before=g.save.totalTime;
            Action(g,"campfire","fieldrest");
            Check(g.save.totalTime==before+20&&g.save.health==g.save.maxHealth&&
                  g.save.energy==100&&g.skillCooldown==0&&g.companionCooldown==0&&
                  g.playerAction==null&&g.companionAction==null,
                "Actual 20-second field rest ends active skills and their elapsed cooldowns");
            Check(!g.projectiles.Contains(shortShot)&&g.projectiles.Contains(longShot)&&
                  Math.Abs(longShot.life-28)<.001f&&!g.hazards.Contains(hazard),
                "Field camp correctly expires a 4-second shot and red circle while ageing a persistent missile by 20 seconds");
            g.save.tea=999;
            string ration="camptea_"+g.Day;
            g.save.flags.Remove(ration);
            Action(g,"campfire","fieldrest");
            Check(g.save.tea==999&&!g.Has("camptea_"+g.Day),
                "A full saved tea stack does not consume its once-per-day ration");
            g.DrinkTea();
            Action(g,"campfire","fieldrest");
            Check(g.save.tea==999&&g.Has("camptea_"+g.Day),
                "An unclaimed daily camp ration can be collected after making room");

            var mission=Fresh(data);mission.save.stage=4;
            mission.save.flags.Add("last_directive_complete");
            mission.save.flags.Add("last_directive_open");
            mission.Enter(4);mission.enemies.Clear();
            var terminal=Object(mission,"civicconduitmaster");
            mission.save.x=terminal.x;mission.save.y=terminal.y;
            Action(mission,"civicconduitmaster","civic_conduit_start");
            Check(mission.CivicConduitActive,"A real paid conduit must start before testing time-skip exploits");
            mission.save.projects[1]=2;
            float missionTime=mission.save.totalTime,health=mission.save.health;
            mission.conversation=new Conversation("营地","Camp","休整","Rest",
                new Choice("休整","Rest","fieldrest"));mission.mode="dialog";
            mission.Choose(0);
            Check(mission.CivicConduitActive&&mission.save.totalTime==missionTime&&
                  mission.save.health==health,
                "Even a stale field-camp rest command cannot skip the live conductor's timer or silently abort the paid mission");
        }
        public static void InnRestActuallyClearsExpiredCombatCooldowns(Campaign data)
        {
            var g=Fresh(data);
            g.save.flags.Add("companion_0");
            g.SelectCompanion(0);
            g.enemies.Clear();
            g.save.energy=100;
            g.Skill(new Point(1,0));
            Check(g.skillCooldown>0,"A real Q cast starts its usual combat cooldown");
            g.CommandCompanion();
            Check(g.companionCooldown>0,"A real companion order starts a separate cooldown");
            g.dashCooldown=.4f;
            g.attackCooldown=.3f;
            g.guardTime=.5f;
            var ordinaryShot=new Projectile { friendly=true,
                position=g.Position, velocity=new Point(1,0),damage=5,life=3 };
            var persistentShot=new Projectile { friendly=true,
                position=g.Position, velocity=new Point(0,1),damage=0,life=90 };
            g.projectiles.Add(ordinaryShot);
            g.projectiles.Add(persistentShot);
            var flash=new Effect("release",g.Position,.25f);
            g.effects.Add(flash);
            // Harmless expired warning tests time-skip cleanup; the separate
            // area-combat test verifies damaging warnings correctly deny rest.
            var warning=new Hazard{position=g.Position,radius=1.5f,timer=.3f,damage=0,kind="rift"};
            var lasting=new Hazard{position=g.Position+new Point(20,0),radius=1,timer=90,damage=0,kind="rift"};
            g.hazards.Add(warning);g.hazards.Add(lasting);
            g.perfectDodgeArmed=true;
            g.invulnerable=.3f;g.playerFlash=.2f;g.impactShake=.1f;
            g.Close();
            float before=g.save.totalTime;
            Action(g,"rest","rest");
            Check(Math.Abs(g.save.totalTime-before-45)<.001f&&
                  g.skillCooldown==0&&g.dashCooldown==0&&g.attackCooldown==0&&
                  g.companionCooldown==0&&g.guardTime==0&&g.playerAction==null,
                "A 45-second bed rest restores skill, companion, dodge and attack readiness");
            Check(!g.projectiles.Contains(ordinaryShot) &&
                  g.projectiles.Contains(persistentShot) && persistentShot.life==45&&
                  !g.effects.Contains(flash),
                "45-second rest expires short shots and deducts actual elapsed time from longer scripted projectile lifetimes");
            Check(!g.hazards.Contains(warning)&&g.hazards.Contains(lasting)&&
                  lasting.timer==45&&g.invulnerable==0&&!g.perfectDodgeArmed&&
                  g.playerFlash==0&&g.impactShake==0,
                "Real rest expires near-term red warnings, invulnerability and dodge feedback, preserving only long-lived hazards");
            g.save.energy=100;
            g.Skill(new Point(1,0));
            Check(g.skillCooldown>0,
                "The same skill can be used normally after the player has rested");
        }

        public static void CombatCannotBeSkippedThroughInnHealing(Campaign data)
        {
            var g=Fresh(data);g.enemies.Clear();
            g.save.x=18;g.save.y=15;
            var inn=Object(g,"rest");
            g.save.health=18;g.save.energy=11;
            float time=g.save.totalTime;
            var near=new Enemy(9119,0,g.save.x+1,g.save.y,40);
            g.enemies.Add(near);
            g.Talk(inn);
            Check(g.mode=="dialog"&&Array.FindIndex(g.conversation.choices,
                c=>c.action=="rest")<0,
                "Inn menu refuses to advertise free healing while enemies are within the safe-travel radius");
            g.Close();
            g.conversation=new Conversation("旅店","Inn","休息","Rest",
                new Choice("休息","Rest","rest"));g.mode="dialog";g.Choose(0);
            Check(g.save.health==18&&g.save.energy==11&&g.save.totalTime==time,
                "A stale rest command cannot heal during combat or skip world time");
            g.enemies.Clear();
            var arrow=new Projectile{position=g.Position+new Point(1,0),
                velocity=new Point(-5,0),friendly=false,damage=10,life=2};
            g.projectiles.Add(arrow);
            g.Talk(inn);
            Check(g.mode=="dialog"&&Array.FindIndex(g.conversation.choices,
                c=>c.action=="rest")<0,
                "An incoming hostile arrow is also a real rest interruption");
            g.Close();
            g.projectiles.Clear();
            g.save.flags.Add("companion_0");
            g.SelectCompanion(0);
            g.save.energy=100;g.CommandCompanion();
            Check(g.companionAction!=null,"A real companion cast is awaiting release");
            Action(g,"rest","rest");
            Check(g.save.totalTime==time+45&&g.save.health==g.save.maxHealth&&
                  g.save.energy==100&&g.companionAction==null,
                "Clearing combat restores normal rest and cancels obsolete companion spell release");
        }

        public static void TalentRefundCannotOverflowSavedSkillPoints(Campaign data)
        {
            var g=Fresh(data);
            g.save.coins=75;
            g.save.skillPoints=95;
            g.save.talents[0]=5;g.save.talents[1]=5;
            g.save.maxHealth=100+g.save.upgrades*20+60+
                (g.save.profession==0?20:0);
            g.save.health=g.save.maxHealth;
            int originalHealth=(int)g.save.health,coins=g.save.coins;
            Check(!g.CanResetTalents,
                "UI correctly disables a respec whose ten-point refund would exceed the v2 99-point save limit");
            g.ResetTalents();
            Check(g.save.coins==coins&&g.save.skillPoints==95&&
                g.save.talents[0]==5&&g.save.talents[1]==5&&
                g.save.health==originalHealth&&g.toast.Contains("99"),
                "Even a direct respec call must not charge coins, alter stats or discard invested skills on overflow");
            g.save.skillPoints=89;
            Check(g.CanResetTalents,"A complete refund exactly to the 99-point cap is allowed");
            g.ResetTalents();
            Check(g.save.coins==coins-20&&g.save.skillPoints==99&&
                g.save.talents[0]==0&&g.save.talents[1]==0&&
                g.save.health<=g.save.maxHealth,
                "Valid exact-cap respec refunds every point without truncation");
            var reloaded=new GameCore(data,g.save);
            Check(reloaded.save.skillPoints==99&&reloaded.save.talents[0]==0,
                "Saved v2 progression retains the full refunded total after reloading");
        }

        public static void LiveAreaWarningsBlockVoluntaryEscapeAndRest(Campaign data)
        {
            var g=Fresh(data);g.enemies.Clear();
            g.save.x=18;g.save.y=15;
            var inn=Object(g,"rest");
            g.save.health=37;g.save.energy=21;
            float start=g.save.totalTime;
            var hazard=new Hazard { position=g.Position+new Point(6.4f,0),
                radius=1.1f,damage=20,timer=.8f,kind="rift" };
            g.hazards.Add(hazard);
            Check(!g.SafeTravel,"The edge of an active damaging blast intersects the player's travel safety circle");
            g.Talk(inn);
            Check(g.mode=="dialog"&&Array.FindIndex(g.conversation.choices,
                c=>c.action=="rest")<0,
                "Inn menu cannot skip the active area-warning telegraph");
            g.Close();
            g.conversation=new Conversation("旅店","Inn","休息","Rest",
                new Choice("休息","Rest","rest"));g.mode="dialog";g.Choose(0);
            Check(g.save.health==37&&g.save.energy==21&&g.save.totalTime==start,
                "Forged/stale rest action cannot heal or erase a warning mid-combat");
            g.ReturnToTown();
            Check(g.save.room==0&&g.hazards.Contains(hazard),
                "Town rest/fast-return rejection retains the actual warning");
            hazard.position=g.Position+new Point(7.1f,0);
            Check(g.SafeTravel,"A warning outside the combined blast and safety range does not block travel");
            hazard.position=g.Position;hazard.damage=0;
            Check(g.SafeTravel,"Non-damaging tutorial decals cannot block travel");
            hazard.damage=20;hazard.timer=0;
            Check(!g.SafeTravel,"A zero-timer contact stays dangerous until world simulation retires it");
            hazard.timer=-.1f;
            Check(!g.SafeTravel,"Overdue but not yet delivered area damage cannot be bypassed by travel");
            g.hazards.Clear();
            Check(g.SafeTravel,"Removing a resolved warning immediately restores normal travel clearance");
            Action(g,"rest","rest");
            Check(g.save.totalTime==start+45&&g.save.health==g.save.maxHealth,
                "Once danger has actually ended, normal 45-second inn recovery still works");
        }

    }
}
