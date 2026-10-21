using System;
namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        public static void FrostSkywatchIsPlayableOrderedMysteryWithRealBoss(Campaign data)
        {
            var g=Fresh(data);
            Check(g.GetQuest("skywatch").locked,
                "Original early saves discover the skywatch without bypassing story gates");
            WorldObject yilan=Object(g,"skywatcher");
            g.save.x=yilan.x;g.save.y=yilan.y;
            Check(g.CanStand(g.Position) && g.Nearest()==yilan,
                "The new astronomer occupies a real reachable town location");
            g.Talk(yilan);
            Check(Array.FindIndex(g.conversation.choices,
                c=>c.action=="skywatch_accept")<0,
                "The ancient highland mission does not bypass the original mountain report");
            g.Close();
            g.save.stage=3;g.save.level=12;g.save.xp=0;
            g.save.flags.Add("mountain_report");
            // Preserve the original farshore chapter gate: Frostchime opens
            // only after the prior desert report, not from a new side quest.
            g.save.flags.Add("desert_report");
            // The protected east-side arena opens only after the original
            // highland chime gate, independently of this optional story.
            g.save.flags.Add("gate_7");
            g.save.x=yilan.x;g.save.y=yilan.y;
            Action(g,"skywatcher","skywatch_accept");
            Check(g.Has("skywatch_started") && !g.GetQuest("skywatch").locked &&
                g.save.trackedQuest=="skywatch",
                "Accepting Yilan's dialogue starts and tracks the real new quest");
            g.Enter(7);g.enemies.Clear();
            for(int i=1;i<=3;i++)
            {
                WorldObject lens=Object(g,"skyprism"+i);
                g.save.x=lens.x;g.save.y=lens.y;
                Check(g.CanStand(g.Position) && g.Nearest()==lens,
                    "Each unique authored lens is reachable and physically interactable");
            }
            WorldObject second=Object(g,"skyprism2");
            g.save.x=second.x;g.save.y=second.y;
            g.Talk(second);
            Check(Array.FindIndex(g.conversation.choices,
                c=>c.action=="skywatch_align_2_2")<0,
                "The east lens cannot bypass the unsolved northern reading");
            g.Close();
            WorldObject first=Object(g,"skyprism1");
            g.save.x=first.x;g.save.y=first.y;
            float energy=g.save.energy;
            Action(g,"skyprism1","skywatch_align_1_2");
            Check(g.save.energy==energy-8 && !g.Has("skywatch_prism_1"),
                "A wrong physical puzzle choice spends real energy without awarding progress");
            Action(g,"skyprism1","skywatch_align_1_1");
            Check(g.Has("skywatch_prism_1"),"Correct frost reading persists");
            g.save.x=second.x;g.save.y=second.y;
            Action(g,"skyprism2","skywatch_align_2_2");
            WorldObject third=Object(g,"skyprism3");
            g.save.x=third.x;g.save.y=third.y;
            Action(g,"skyprism3","skywatch_align_3_3");
            Check(g.Has("skywatch_prism_1") && g.Has("skywatch_prism_2") &&
                g.Has("skywatch_prism_3") && !g.GetQuest("skywatch").complete,
                "Three real calibrated prisms unlock the observatory but not its reward");
            WorldObject altar=Object(g,"skyobservatory");
            g.save.x=altar.x;g.save.y=altar.y;g.enemies.Clear();
            Check(g.CanStand(g.Position) && g.Nearest()==altar,
                "The new observatory exists on real accessible Frostchime ground");
            int coins=g.save.coins, shards=g.save.shards, talent=g.save.skillPoints;
            int kills=g.save.kills[4];
            Action(g,"skyobservatory","skywatch_start");
            Enemy boss=g.enemies.Find(g.IsSkywatchEnemy);
            Check(g.SkywatchRoom==7 && boss!=null && boss.maxHp==240 &&
                boss.boss && g.SkywatchTime>84 &&
                g.CanStand(boss.Position) &&
                g.NavigationPath(boss.Position).Count>0,
                "The skywatch trial starts a genuine reachable boss with normal combat AI");
            Check(g.save.coins==coins && g.save.shards==shards &&
                !g.Has("skywatch_defeated"),
                "Starting the fight never pays unearned first-clear rewards");
            // Save/reload during a live optional boss must not create a
            // questless 89701 enemy or count a silent first clear.
            g.PrepareSave();
            Check(!Array.Exists(g.save.worldEnemies,
                record=>record!=null && record.id==89701),
                "A live scripted observatory boss is not serialized as an ordinary world patrol");
            var interrupted=new GameCore(data,CloneSave(g.save));
            interrupted.Start();
            Check(interrupted.SkywatchRoom==0 &&
                !interrupted.enemies.Exists(interrupted.IsSkywatchEnemy) &&
                !interrupted.Has("skywatch_defeated") &&
                !interrupted.Has("boss_7"),
                "Reload safely resets the unfinished observatory rather than resurrecting a phantom elite");
            // Defeat with the actual golden-haired hero's melee input. This
            // also catches combat-hit integration and anti-farming regressions.
            boss.stun=120;boss.timer=120;
            g.save.x=boss.x-1.05f;g.save.y=boss.y;
            g.save.maxHealth=3000;g.save.health=3000;g.invulnerable=120;
            float originalHp=boss.hp;
            for(int turn=0;turn<45 && g.enemies.Contains(boss);turn++)
            {
                // Recreate a repeatable range in the real arena. The test
                // calls the exact player attack API, not private damage hooks;
                // it suppresses opponent locomotion to isolate hero hits.
                boss.stun=12; boss.timer=12;
                g.save.x=boss.x-1.05f;g.save.y=boss.y;
                g.save.energy=100;
                g.Attack(new Point(1,0));
                for(int tick=0;tick<16;tick++) g.Tick(.05f,new Point());
            }
            Check(boss.hp<originalHp && !g.enemies.Contains(boss),
                "Actual playable melee must finish the elite (hp " +
                    boss.hp.ToString("0.0") + " of " + originalHp.ToString("0.0") + ")");
            for(int i=0;i<5;i++)g.Tick(.05f,new Point());
            Check(g.Has("skywatch_defeated") && g.SkywatchRoom==0 &&
                !g.Has("boss_7") && !g.hazards.Exists(h=>h.skywatchOwned) &&
                g.save.coins==coins && g.save.kills[4]==kills,
                "Combat clear has no normal loot, chapter kill or lingering hazard");
            g.Enter(0);g.save.x=yilan.x;g.save.y=yilan.y;
            Action(g,"skywatcher","skywatch_publish");
            Check(g.Has("skywatch_complete") && g.GetQuest("skywatch").complete &&
                g.save.coins==coins+95 && g.save.shards==shards+3 &&
                g.save.skillPoints>=talent+1,
                "Public spectrum archives exactly one final permanent reward");
            g.Talk(yilan);
            Check(Array.FindIndex(g.conversation.choices,
                c=>c.action=="skywatch_publish")<0,
                "Completed story cannot pay out another first-clear prize");
            g.Close();g.PrepareSave();
            var loaded=new GameCore(data,CloneSave(g.save));loaded.Start();
            Check(loaded.Has("skywatch_complete") && loaded.GetQuest("skywatch").complete &&
                loaded.save.coins==g.save.coins,
                "The new ending survives old-format save/reload without schema migration");
            loaded.Enter(7);loaded.enemies.Clear();
            loaded.save.x=altar.x;loaded.save.y=altar.y;
            Check(loaded.SkywatchRefuge,"Completed observatory opens persistent cold refuge");
            loaded.save.cold=40;
            loaded.Tick(.05f,new Point());
            Check(loaded.save.cold<40,
                "Refuge changes actual Frostchime cold exposure, not only the icon");
        }
        public static void FrostSkywatchPrismGroundingCreatesRealOneUseBossOpening(Campaign data)
        {
            var g=Fresh(data);g.save.stage=3;g.save.level=12;
            foreach(string flag in new[]{"mountain_report","desert_report",
                "gate_7","skywatch_started","skywatch_prism_1",
                "skywatch_prism_2","skywatch_prism_3"})
                g.save.flags.Add(flag);
            g.Enter(7);g.enemies.Clear();
            WorldObject altar=Object(g,"skyobservatory");
            g.save.x=altar.x;g.save.y=altar.y;
            Check(g.CanStand(g.Position) && g.Nearest()==altar,
                "The grounding action belongs to a reachable authored observatory");
            Action(g,"skyobservatory","skywatch_start");
            Enemy guardian=g.enemies.Find(g.IsSkywatchEnemy);
            Check(g.SkywatchRoom==7 && guardian!=null && g.SkywatchGroundingAvailable,
                "The real live guardian enables one affordable tactical action");
            int originalEnergy=(int)g.save.energy;
            float originalHp=guardian.hp;
            var unrelated=new Hazard { position=g.Position,
                timer=5,radius=.5f,kind="rift",damage=2 };
            g.hazards.Add(unrelated);
            g.hazards.Add(new Hazard { position=g.Position,
                timer=1.1f,radius=1f,kind="glass",damage=14,skywatchOwned=true });
            Action(g,"skyobservatory","skywatch_ground");
            Check(g.SkywatchGroundingTime>7.9f &&
                !g.SkywatchGroundingAvailable &&
                g.save.energy==originalEnergy-25 &&
                guardian.stun>=1.8f && guardian.hp==originalHp,
                "A real paid grounding interrupts rather than secretly killing the boss");
            Check(!g.hazards.Exists(h=>h.skywatchOwned) &&
                g.hazards.Contains(unrelated),
                "The countermeasure clears only the guardian's current frost hazards");
            g.Talk(altar);
            Check(Array.FindIndex(g.conversation.choices,
                    c=>c.action=="skywatch_ground")<0,
                "Once used, the action is removed from the actual dialogue");
            g.Close();
            g.hazards.Remove(unrelated);
            for(int i=0;i<20;i++)g.Tick(.05f,new Point());
            Check(g.SkywatchRoom==7 && g.SkywatchGroundingTime>6.8f &&
                g.hazards.Find(h=>h.skywatchOwned)==null &&
                g.enemies.Contains(guardian),
                "Grounding grants a bounded and genuinely playable quiet period");
            g.save.x=altar.x;g.save.y=altar.y;
            Action(g,"skyobservatory","skywatch_withdraw");
            Check(g.SkywatchRoom==0 && g.SkywatchGroundingTime==0 &&
                !g.Has("skywatch_defeated"),
                "Withdrawal cannot turn a temporary stun into a victory");
            g.enemies.Clear();g.save.energy=100;
            Action(g,"skyobservatory","skywatch_start");
            Check(g.SkywatchRoom==7 && g.SkywatchGroundingAvailable,
                "A new legitimate attempt may buy one new grounding, without paying rewards");
        }

        public static void AfterglowExpeditionHasRealFieldVisitsRewardsAndWarming(Campaign data)
        {
            var g=Fresh(data);
            Check(g.GetQuest("skywatch_afterglow").locked,
                "The new aftermath is not visible before the public weather report");
            g.save.stage=3;g.save.level=12;
            foreach(string flag in new[]{"mountain_report","desert_report","gate_7",
                "skywatch_started","skywatch_prism_1","skywatch_prism_2",
                "skywatch_prism_3","skywatch_defeated","skywatch_complete"})
                g.save.flags.Add(flag);
            Check(!g.GetQuest("skywatch_afterglow").locked &&
                !g.GetQuest("skywatch_afterglow").complete,
                "Publishing the original skywatch opens a distinct playable aftermath");
            WorldObject yilan=Object(g,"skywatcher");
            g.save.x=yilan.x;g.save.y=yilan.y;
            Action(g,"skywatcher","skywatch_afterglow_track");
            Check(g.save.trackedQuest=="skywatch_afterglow",
                "The original astronomer introduces and tracks the new expedition through real dialogue");
            g.Enter(7);g.enemies.Clear();
            WorldObject altar=Object(g,"skyobservatory");
            int coins=g.save.coins,shards=g.save.shards;
            g.save.x=altar.x;g.save.y=altar.y;
            g.Talk(altar);
            Check(Array.FindIndex(g.conversation.choices,
                c=>c.action=="skywatch_afterglow_publish")<0,
                "Players must actually investigate three distant lenses before archival rewards");
            g.Close();
            for(int i=1;i<=3;i++)
            {
                WorldObject lens=Object(g,"skyprism"+i);
                g.save.x=lens.x;g.save.y=lens.y;
                Check(g.CanStand(g.Position) && g.Nearest()==lens,
                    "New afterglow evidence lives at a real walkable Frostchime lens");
                Action(g,"skyprism"+i,"skywatch_afterglow_"+i);
                Check(g.Has("skywatch_afterglow_"+i) &&
                    g.save.coins==coins && g.save.shards==shards,
                    "The record is persistent but does not mint premature resources");
                g.Talk(lens);
                Check(Array.FindIndex(g.conversation.choices,
                    c=>c.action=="skywatch_afterglow_"+i)<0,
                    "A recovered afterglow cannot be collected or farmed twice");
                g.Close();
            }
            Check(g.GetQuest("skywatch_afterglow").steps.Count==4 &&
                !g.GetQuest("skywatch_afterglow").complete,
                "Three real recovered field clues leave one actual archive objective");
            g.save.x=altar.x;g.save.y=altar.y;
            Action(g,"skyobservatory","skywatch_afterglow_publish");
            Check(g.Has("skywatch_afterglow_complete") &&
                g.GetQuest("skywatch_afterglow").complete &&
                g.save.coins==coins+45 && g.save.shards==shards+2,
                "The final observatory archive grants one earned reward, not three farmable pickups");
            g.Close();
            for(int i=1;i<=3;i++)
            {
                WorldObject lens=Object(g,"skyprism"+i);
                g.save.x=lens.x;g.save.y=lens.y;
                g.enemies.Clear();
                Check(g.SkywatchEchoRefuge,
                    "Every restored lens is a live permanent cold refuge on actual map geometry");
                g.save.cold=40;
                g.Tick(.05f,new Point());
                Check(g.save.cold<40,
                    "The completed post-story network actually warms players, not merely displays a ring");
            }
            g.save.x=altar.x;g.save.y=altar.y;
            g.Talk(altar);
            Check(Array.FindIndex(g.conversation.choices,
                c=>c.action=="skywatch_afterglow_publish")<0,
                "A completed public archive cannot pay its final reward again");
            g.Close();g.PrepareSave();
            var loaded=new GameCore(data,CloneSave(g.save));loaded.Start();
            Check(loaded.Has("skywatch_afterglow_complete") &&
                loaded.GetQuest("skywatch_afterglow").complete &&
                loaded.save.coins==g.save.coins,
                "Old-version save/restore preserves the afterglow expedition and reward exactly");
        }

        public static void FrostSkywatchChronicleOnlyRevealsObservedEvidence(Campaign data)
        {
            var g=Fresh(data);
            WorldObject book=Object(g,"worldchronicle");
            g.Talk(book);
            int section=Array.FindIndex(g.conversation.choices,
                c=>c.action=="story_section_after");
            Check(section>=0,"Original chronicle has an aftermath section");
            g.Choose(section);
            Check(Array.FindIndex(g.conversation.choices,
                c=>c.action=="story_read_skywatch")<0,
                "The new observatory chapter is hidden before Yilan's commission");
            g.Close();g.save.flags.Add("skywatch_started");
            g.Talk(book);
            section=Array.FindIndex(g.conversation.choices,
                c=>c.action=="story_section_after");
            g.Choose(section);
            int page=Array.FindIndex(g.conversation.choices,
                c=>c.action=="story_read_skywatch");
            Check(page>=0,"Accepting the real quest unlocks a playable-chronicle chapter");
            g.Choose(page);
            Check(g.conversation.text.Contains("读数尚未取回") &&
                !g.conversation.text.Contains("观星台旁留下永久避寒穹顶"),
                "The book does not spoil readings or claim an unearned final refuge");
            g.Close();
            g.save.flags.Add("skywatch_prism_1");
            g.save.flags.Add("skywatch_prism_2");
            g.Talk(book);section=Array.FindIndex(g.conversation.choices,
                c=>c.action=="story_section_after");g.Choose(section);
            page=Array.FindIndex(g.conversation.choices,
                c=>c.action=="story_read_skywatch");g.Choose(page);
            Check(g.conversation.text.Contains("第一场冻雨") &&
                g.conversation.text.Contains("并非太阳") &&
                !g.conversation.text.Contains("已经停止攻击旅人"),
                "Only actually recovered prism clues appear in the bilingual story archive");
            g.Close();g.save.flags.Add("skywatch_prism_3");
            g.save.flags.Add("skywatch_defeated");g.save.flags.Add("skywatch_complete");
            g.Talk(book);section=Array.FindIndex(g.conversation.choices,
                c=>c.action=="story_section_after");g.Choose(section);
            page=Array.FindIndex(g.conversation.choices,
                c=>c.action=="story_read_skywatch");g.Choose(page);
            Check(g.conversation.text.Contains("永久避寒穹顶") &&
                g.conversation.textEn.Contains("permanent shelter") &&
                !g.conversation.text.Contains("三座棱镜成为人人可用"),
                "The public original forecast cannot spoil uncompleted afterglow records");
            g.Close();g.save.flags.Add("skywatch_afterglow_1");
            g.Talk(book);section=Array.FindIndex(g.conversation.choices,
                c=>c.action=="story_section_after");g.Choose(section);
            page=Array.FindIndex(g.conversation.choices,
                c=>c.action=="story_read_skywatch");g.Choose(page);
            Check(g.conversation.text.Contains("一部分旅人余辉") &&
                !g.conversation.text.Contains("三座棱镜成为人人可用") &&
                g.conversation.textEn.Contains("Some travelers' afterglows"),
                "Partial post-story evidence appears without claiming an unearned refuge network");
            g.Close();g.save.flags.Add("skywatch_afterglow_2");
            g.save.flags.Add("skywatch_afterglow_3");
            g.save.flags.Add("skywatch_afterglow_complete");
            g.Talk(book);section=Array.FindIndex(g.conversation.choices,
                c=>c.action=="story_section_after");g.Choose(section);
            page=Array.FindIndex(g.conversation.choices,
                c=>c.action=="story_read_skywatch");g.Choose(page);
            Check(g.conversation.text.Contains("三座棱镜成为人人可用") &&
                g.conversation.textEn.Contains("Three public warming beacons"),
                "Only after a genuinely completed archive does the chronicle show the epilogue's full result");
        }

        public static void FrostSkywatchHazardsAreDodgeableAndWithdrawalIsSafe(Campaign data)
        {
            var g=Fresh(data);g.save.stage=3;g.save.level=12;
            foreach(string flag in new[]{"mountain_report","desert_report","skywatch_started",
                "skywatch_prism_1","skywatch_prism_2","skywatch_prism_3","gate_7"})
                g.save.flags.Add(flag);
            g.Enter(7);g.enemies.Clear();
            WorldObject altar=Object(g,"skyobservatory");
            g.save.x=altar.x;g.save.y=altar.y;
            Action(g,"skyobservatory","skywatch_start");
            Enemy boss=g.enemies.Find(g.IsSkywatchEnemy);
            Check(boss!=null,"A live guardian owns timed environmental hazards");
            boss.stun=90;boss.timer=90;g.invulnerable=90;
            Point original=g.Position;
            for(int tick=0;tick<65;tick++)g.Tick(.05f,new Point());
            Hazard warning=g.hazards.Find(h=>h.skywatchOwned);
            Check(warning!=null && (warning.position-original).Length<.3f &&
                warning.timer>0 && warning.radius>=.9f,
                "Guardian warns of an actual delayed contact at the hero's feet");
            Point[] escapes={new Point(2.1f,0),new Point(-2.1f,0),
                new Point(0,2.1f),new Point(0,-2.1f)};
            bool escaped=false;
            foreach(Point delta in escapes)
                if(g.CanStand(original+delta) &&
                    (original+delta-warning.position).Length>warning.radius+.5f)
                { g.save.x=original.x+delta.x;g.save.y=original.y+delta.y;escaped=true;break; }
            Check(escaped,"The authentic highland arena offers a reachable dodge exit");
            float hp=g.save.health;g.invulnerable=0;
            for(int i=0;i<35;i++)g.Tick(.05f,new Point());
            Check(g.save.health>=hp-.01f,
                "Moving out of the warned footprint avoids the ordinary damaging hazard");
            var ordinary=new Hazard{position=g.Position,timer=4,radius=.5f,
                kind="rift",damage=2};
            g.hazards.Add(ordinary);
            g.save.x=altar.x;g.save.y=altar.y;
            Action(g,"skyobservatory","skywatch_withdraw");
            Check(g.SkywatchRoom==0 && !g.enemies.Exists(g.IsSkywatchEnemy) &&
                !g.hazards.Exists(h=>h.skywatchOwned) &&
                g.hazards.Contains(ordinary) && !g.Has("skywatch_defeated"),
                "Voluntary withdrawal removes only the skywatch encounter and never gives rewards");
            g.hazards.Remove(ordinary);g.enemies.Clear();
            Action(g,"skyobservatory","skywatch_start");
            Check(g.SkywatchRoom==7,"The observatory supports a real free retry");
            typeof(GameCore).GetProperty("SkywatchTime")
                .GetSetMethod(true).Invoke(g,new object[]{.01f});
            g.Tick(.05f,new Point());
            Check(g.SkywatchRoom==0 && !g.Has("skywatch_defeated") &&
                !g.enemies.Exists(g.IsSkywatchEnemy) &&
                !g.hazards.Exists(h=>h.skywatchOwned),
                "Time expiry never grants a fake boss clear or leaves invisible combat behind");
            g.PrepareSave();
            var loaded=new GameCore(data,CloneSave(g.save));loaded.Start();
            Check(loaded.Has("skywatch_prism_3") && !loaded.Has("skywatch_defeated") &&
                loaded.SkywatchRoom==0,
                "Reload preserves completed exploration but not an unfinished boss fight");
        }
        public static void GroundedFrostGuardianCannotLeaveLiveOrbsOrFalseWarnings(Campaign data)
        {
            for (int test = 0; test < 2; test++)
            {
                var g = Fresh(data); g.save.stage = 3;
                foreach (string flag in new[] { "mountain_report",
                    "desert_report", "gate_7", "skywatch_started",
                    "skywatch_prism_1", "skywatch_prism_2",
                    "skywatch_prism_3" }) g.save.flags.Add(flag);
                g.Enter(7); g.enemies.Clear(); g.effects.Clear();
                g.projectiles.Clear();
                var observatory = Object(g, "skyobservatory");
                g.save.x = observatory.x; g.save.y = observatory.y;
                g.invulnerable = 100;
                Action(g, observatory.kind, "skywatch_start");
                Enemy guardian = g.enemies.Find(g.IsSkywatchEnemy);
                Check(guardian != null && g.SkywatchGroundingAvailable,
                    "Actual skywatch fight is live with one original grounded defense");
                // Begin real enemy AI at a legal west-side position, rather
                // than synthesizing a red circle or an owned missile in tests.
                g.save.x = guardian.x - 2.5f;
                g.save.y = guardian.y;
                Check(g.CanStand(g.Position),
                    "The actual skywatch guardian has legal adjacent ground for its attack");
                if (test == 0)
                {
                    guardian.state = 1;
                    guardian.timer = .005f;
                    guardian.target = g.Position;
                    guardian.Face(g.Position - guardian.Position);
                    g.Tick(.05f, new Point());
                    Check(g.projectiles.Exists(p => p.skywatchOwned),
                        "The real live Boss release generated airborne observatory-owned missiles");
                }
                else
                {
                    guardian.state = 0; guardian.timer = 0;
                    g.Tick(.05f, new Point());
                    Check(guardian.state == 1 &&
                        g.effects.Exists(e => e.kind == "danger" && e.skywatchOwned),
                        "The real Boss windup emitted its full-duration owned danger circle before attack contact");
                }
                var unrelated = new Effect("discover", new Point(12,14), 4);
                g.effects.Add(unrelated);
                var arrow = new Projectile { position = new Point(12,14),
                    velocity = new Point(1,0), friendly = true, life = 3,
                    visual = "arrow", damage = 0 };
                g.projectiles.Add(arrow);
                g.save.x = observatory.x; g.save.y = observatory.y;
                Action(g, observatory.kind, "skywatch_ground");
                Check(g.SkywatchGroundingTime > 7.9f &&
                    guardian.state == 0 && guardian.stun >= 1.8f &&
                    !g.projectiles.Exists(p => p.skywatchOwned) &&
                    !g.effects.Exists(e => e.skywatchOwned) &&
                    g.effects.Contains(unrelated) && g.projectiles.Contains(arrow),
                    "A single authentic paid grounding interrupts the Boss and retires only its live missiles and unspent warnings");
                g.Close();
                g.Tick(.05f, new Point());
                Check(g.projectiles.Contains(arrow) &&
                    !g.projectiles.Exists(p => p.skywatchOwned),
                    "Unrelated player shots continue normally without resurrecting pre-grounding observatory missiles");
            }
        }

        public static void SkywatchBossDisappearanceCannotForgeAnAstronomerVictory(Campaign data)
        {
            var g = Fresh(data); g.save.stage = 3; g.save.level = 12;
            foreach (string flag in new[] {
                "mountain_report", "desert_report", "gate_7",
                "skywatch_started", "skywatch_prism_1",
                "skywatch_prism_2", "skywatch_prism_3" })
                g.save.flags.Add(flag);
            g.Enter(7); g.enemies.Clear(); g.projectiles.Clear();
            var altar = Object(g, "skyobservatory");
            g.save.x = altar.x; g.save.y = altar.y;
            Action(g, altar.kind, "skywatch_start");
            var guardian = g.enemies.Find(g.IsSkywatchEnemy);
            Check(g.SkywatchRoom == 7 && guardian != null,
                "The astronomer's real calibrated observatory starts its original armored boss");
            g.enemies.Remove(guardian);
            g.Tick(.05f, new Point());
            Check(g.SkywatchRoom == 0 && !g.Has("skywatch_defeated") &&
                !g.Has("skywatch_complete"),
                "A vanished but undefeated boss cannot counterfeit an earned public spectrum");
            g.Talk(altar);
            Check(g.conversation != null &&
                Array.Exists(g.conversation.choices,
                    c => c.action == "skywatch_start"),
                "The physical observatory offers a legitimate free retry after an abnormal reset");
            g.Close(); g.enemies.Clear();
            Action(g, altar.kind, "skywatch_start");
            guardian = g.enemies.Find(g.IsSkywatchEnemy);
            Check(g.SkywatchRoom == 7 && guardian != null,
                "A fresh legitimate guardian spawns with the same authored combat");
            var hit = typeof(GameCore).GetMethod("Hit",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic);
            Check(hit != null, "Original actual lethal damage is the only source of victory");
            hit.Invoke(g, new object[] { guardian, 99999f, false, null, null });
            g.Tick(.05f, new Point());
            Check(g.Has("skywatch_defeated") &&
                g.SkywatchRoom == 0 && !g.Has("boss_7"),
                "Real lethal skywatch combat confirms the optional boss without forging the unrelated main story gate");
            g.PrepareSave();
            var resumed = new GameCore(data, CloneSave(g.save));
            Check(resumed.Has("skywatch_defeated") &&
                !resumed.Has("skywatch_complete"),
                "Verified defeat survives the original v2 slot and still needs Yilan's distinct publication");
        }

    }
}
