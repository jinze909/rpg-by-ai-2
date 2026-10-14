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
            foreach (int room in new[] { 0, 1, 5, 6, 7, 8 }) { var music = AudioSynthesis.Music(AudioSynthesis.Theme(room, false)); Check(music.Length == AudioSynthesis.Rate * 16 && Math.Abs(music[0]) < .01f && Math.Abs(music[music.Length - 1]) < .01f, "Regional music fades cleanly at loop boundaries"); }
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
            Check(g.SafeTravel,"Active arrow at the exact four-tile boundary remains outside danger radius");
            arrow.position=new Point(15.99f,12);
            Check(!g.SafeTravel,"Approaching arrow is blocked with the same radius as local enemies");
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
