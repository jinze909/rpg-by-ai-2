using System;
namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        public static void TideboundManifestRequiresTidesPhysicalTravelAndRealCombat(Campaign data)
        {
            var g=Fresh(data);
            Check(g.GetQuest("tide_salvage").locked,
                "The old campaign cannot discover salvage before its harbor report");
            g.save.stage=3;g.save.level=20;g.save.xp=0;
            foreach(string flag in new[]{"exp_intro","harbor_report","gate_5"})
                g.save.flags.Add(flag);
            g.Enter(5);g.enemies.Clear();g.invulnerable=150;
            WorldObject dock=Object(g,"tidedock");
            WorldObject first=Object(g,"tidecargo1");
            WorldObject second=Object(g,"tidecargo2");
            WorldObject third=Object(g,"tidecargo3");
            foreach(WorldObject o in new[]{dock,first,second,third})
            {
                g.save.x=o.x;g.save.y=o.y;
                Check(g.CanStand(g.Position) && g.Nearest()==o,
                    "Every new physical cargo or dock object is reachable and actually interactable");
            }
            g.save.x=dock.x;g.save.y=dock.y;
            g.save.totalTime=23;g.save.tideHeldUntil=0;
            Check(!g.LowTide,"The real natural high-tide period is active");
            g.Talk(dock);
            Check(Array.FindIndex(g.conversation.choices,
                c=>c.action=="tide_salvage_start")<0,
                "No new expedition can bypass a real high tide at the dock");
            g.Close();
            g.save.totalTime=0;
            Action(g,"tidedock","tide_salvage_start");
            Check(g.TideSalvageActive && g.TideSalvagePhase==1 &&
                g.TideSalvageTime>=99 && g.save.trackedQuest=="tide_salvage",
                "Low tide opens a real timed, tracked expedition");
            g.Enter(0);
            Check(g.save.room==5 && g.TideSalvagePhase==1,
                "Travel may never silently delete undelivered cargo");
            int coins=g.save.coins,shards=g.save.shards,kills=g.save.kills[5];
            g.save.x=second.x;g.save.y=second.y;
            g.Talk(second);
            Check(g.TideSalvagePhase==1 && !g.enemies.Exists(g.IsTideSalvageEnemy),
                "Skipping to a later freight manifest cannot spawn enemies or advance progress");
            g.save.x=first.x;g.save.y=first.y;
            Action(g,"tidecargo1","tide_salvage_take_1");
            Check(g.TideSalvagePhase==2 && g.save.coins==coins,
                "Physical retrieval gives no premature money or victory flags");
            g.save.x=second.x;g.save.y=second.y;g.save.totalTime=23;
            g.Talk(second);
            Check(g.TideSalvagePhase==2 && !g.LowTide &&
                g.mode=="world","High water actually prohibits salvage at a real shoal");
            g.save.x=dock.x;g.save.y=dock.y;g.save.shards=1;
            Action(g,"tidedock","tide_salvage_hold");
            Check(g.save.shards==0 && g.LowTide &&
                g.save.tideHeldUntil>=41 && g.TideSalvagePhase==2,
                "A paid emergency 18-second tide hold affects the real environment");
            g.Talk(dock);
            Check(Array.FindIndex(g.conversation.choices,
                c=>c.action=="tide_salvage_hold")<0,
                "The emergency hold may not be bought repeatedly in one sortie");
            g.Close();
            g.save.x=second.x;g.save.y=second.y;
            Action(g,"tidecargo2","tide_salvage_take_2");
            Check(g.TideSalvagePhase==3 &&
                g.enemies.FindAll(g.IsTideSalvageEnemy).Count==2,
                "Taking the second record deploys actual chase enemies");
            g.PrepareSave();
            Check(!Array.Exists(g.save.worldEnemies,r=>r!=null &&
                r.id>=89920 && r.id<89930),
                "Live salvage raiders are excluded from ordinary enemy snapshots");
            var interrupted=new GameCore(data,CloneSave(g.save));interrupted.Start();
            Check(!interrupted.TideSalvageActive &&
                !interrupted.enemies.Exists(interrupted.IsTideSalvageEnemy) &&
                !interrupted.Has("tide_salvage_complete"),
                "Reloading incomplete cargo cannot restore ghost raiders or mint reward flags");
            g.save.totalTime=36;
            g.save.x=third.x;g.save.y=third.y;
            Action(g,"tidecargo3","tide_salvage_take_3");
            Check(g.TideSalvagePhase==4 &&
                g.enemies.FindAll(g.IsTideSalvageEnemy).Count==3 &&
                g.Speed<4.5f,"Third collection adds the final live raider and cargo burden");
            int before=g.save.coins;
            g.save.x=dock.x;g.save.y=dock.y;
            g.Talk(dock);
            Check(Array.FindIndex(g.conversation.choices,
                c=>c.action=="tide_salvage_finish")<0 &&
                g.save.coins==before,
                "Returning without defeating real raiders cannot settle the mission");
            g.Close();
            // The canonical player's actual attacks, not deleting the enemy
            // list or setting progress flags, are required for completion.
            foreach(Enemy foe in g.enemies.FindAll(g.IsTideSalvageEnemy))
            {
                foe.x=dock.x+1.05f;foe.y=dock.y;
                foe.hp=3;foe.stun=200;foe.timer=200;
                g.save.x=dock.x;g.save.y=dock.y;
                for(int frame=0;frame<90 && g.enemies.Contains(foe);frame++)
                {
                    if(g.attackCooldown<=0)g.Attack(new Point(1,0));
                    g.Tick(.02f,new Point());
                }
                Check(!g.enemies.Contains(foe),"Canonical hero combat genuinely defeats salvage raiders");
            }
            Check(g.save.kills[5]==kills && g.save.coins==coins &&
                !g.Has("tide_salvage_complete"),
                "Temporary raiders never farm normal patrol kills, loot or premature rewards");
            int talent=g.save.skillPoints;
            g.save.x=dock.x;g.save.y=dock.y;
            Action(g,"tidedock","tide_salvage_finish");
            Check(g.Has("tide_salvage_complete") &&
                g.GetQuest("tide_salvage").complete && !g.TideSalvageActive &&
                g.save.coins==coins+60 && g.save.shards==3 &&
                g.save.skillPoints==talent+1 && g.TidewatchHoldSeconds==60,
                "Only physical public delivery grants one-time salvage rewards and a real +15s tide hold");
            g.Talk(dock);
            Check(Array.FindIndex(g.conversation.choices,
                c=>c.action=="tide_salvage_finish")<0,
                "A completed delivery removes its own reward action");
            g.Close();
            g.PrepareSave();
            var complete=new GameCore(data,CloneSave(g.save));complete.Start();
            Check(complete.save.version==2 && complete.Has("tide_salvage_complete") &&
                complete.TidewatchHoldSeconds==60 && !complete.TideSalvageActive,
                "Original v2 save flags keep permanent utility without serializing live expedition state");
        }

        public static void TideboundSalvageFailureSafelyRetiresOnlyOwnedThreats(Campaign data)
        {
            var g=Fresh(data);g.save.stage=3;g.save.level=18;
            foreach(string flag in new[]{"exp_intro","harbor_report","gate_5"})
                g.save.flags.Add(flag);
            g.Enter(5);g.enemies.Clear();g.invulnerable=200;
            WorldObject dock=Object(g,"tidedock");
            g.save.x=dock.x;g.save.y=dock.y;
            Action(g,"tidedock","tide_salvage_start");
            g.save.x=Object(g,"tidecargo1").x;
            g.save.y=Object(g,"tidecargo1").y;
            Action(g,"tidecargo1","tide_salvage_take_1");
            WorldObject second=Object(g,"tidecargo2");
            g.save.x=second.x;g.save.y=second.y;
            Action(g,"tidecargo2","tide_salvage_take_2");
            Check(g.enemies.Exists(g.IsTideSalvageEnemy),"Threats are real before abort");
            var persistent=new Projectile{position=new Point(8,14),
                velocity=new Point(1,0),life=10};
            var owned=new Projectile{position=new Point(9,14),
                velocity=new Point(1,0),life=10,tideSalvageOwned=true};
            var unrelated=new Effect("discover",new Point(9,14),10);
            var special=new Effect("discover",new Point(8,14),10)
                {tideSalvageOwned=true};
            g.projectiles.Add(persistent);g.projectiles.Add(owned);
            g.effects.Add(unrelated);g.effects.Add(special);
            g.save.x=dock.x;g.save.y=dock.y;
            Action(g,"tidedock","tide_salvage_abandon");
            Check(!g.TideSalvageActive && !g.enemies.Exists(g.IsTideSalvageEnemy) &&
                !g.projectiles.Contains(owned) && g.projectiles.Contains(persistent) &&
                !g.effects.Contains(special) && g.effects.Contains(unrelated) &&
                !g.Has("tide_salvage_complete"),
                "Aborting removes only encounter-owned threats, preserving normal combat effects");
            g.projectiles.Clear();g.effects.Clear();
            Action(g,"tidedock","tide_salvage_start");
            Check(g.TideSalvageActive,"An abandoned expedition can legitimately restart");
            Advance(g,100.5f);
            Check(!g.TideSalvageActive && !g.Has("tide_salvage_complete") &&
                g.TidewatchHoldSeconds==45,
                "A real timed-out mission resets without falsely improving Tidewatch");
            g.Enter(0);
            Check(g.save.room==0,"Ordinary travel is restored after abandoning or timing out");
        }

        public static void TideboundArchiveTracksWitnessedCluesWithoutClaimingLostCargo(Campaign data)
        {
            var g=Fresh(data);g.save.stage=3;
            foreach(string flag in new[]{"exp_intro","harbor_report","gate_5","fieldstation_5"})
                g.save.flags.Add(flag);
            WorldObject chronicle=Object(g,"worldchronicle");
            g.Talk(chronicle);
            int section=Array.FindIndex(g.conversation.choices,
                c=>c.action=="story_section_farshore");g.Choose(section);
            Check(Array.FindIndex(g.conversation.choices,
                c=>c.action=="story_read_tidebound")<0,
                "The tidebound chronicle page remains hidden until the real expedition begins");
            g.Close();g.Enter(5);g.enemies.Clear();
            WorldObject dock=Object(g,"tidedock");
            g.save.x=dock.x;g.save.y=dock.y;
            Action(g,"tidedock","tide_salvage_start");
            Check(g.Has("tide_salvage_started") &&
                g.Conditions().Contains("潮滩打捞") &&
                g.Conditions().Contains("100s"),
                "The real mission unlocks the chronicle and a continuously visible timer");
            g.save.english=true;
            Check(g.Conditions().Contains("SALVAGE") &&
                g.Conditions().Contains("LOW TIDE"),
                "The live tide/timer HUD is also readable in English");
            g.save.english=false;
            WorldObject first=Object(g,"tidecargo1");
            g.save.x=first.x;g.save.y=first.y;
            Action(g,"tidecargo1","tide_salvage_take_1");
            Check(g.Has("tide_salvage_seen_1") &&
                !g.Has("tide_salvage_seen_2") &&
                !g.Has("tide_salvage_complete"),
                "Seeing the first physical roster writes only its earned evidence flag");
            g.save.x=dock.x;g.save.y=dock.y;
            Action(g,"tidedock","tide_salvage_abandon");
            Check(!g.TideSalvageActive && g.Has("tide_salvage_seen_1"),
                "Withdrawing loses cargo but does not erase genuinely witnessed history");
            g.Enter(0);g.save.x=chronicle.x;g.save.y=chronicle.y;
            g.Talk(chronicle);
            section=Array.FindIndex(g.conversation.choices,
                c=>c.action=="story_section_farshore");g.Choose(section);
            int page=Array.FindIndex(g.conversation.choices,
                c=>c.action=="story_read_tidebound");
            Check(page>=0,"A field-experienced player can read their unlocked archive entry");
            g.Choose(page);
            Check(g.conversation.text.Contains("药品名单已被你亲手找回") &&
                g.conversation.text.Contains("粮食货单还未核实") &&
                !g.conversation.text.Contains("所有物资已经亲自运回码头"),
                "One witnessed manifest cannot fabricate the remaining cargo or public handoff");
            g.Close();
            g.save.flags.Add("tide_salvage_seen_2");
            g.save.flags.Add("tide_salvage_seen_3");
            g.Talk(chronicle);
            section=Array.FindIndex(g.conversation.choices,
                c=>c.action=="story_section_farshore");g.Choose(section);
            page=Array.FindIndex(g.conversation.choices,
                c=>c.action=="story_read_tidebound");g.Choose(page);
            Check(g.conversation.text.Contains("货物尚未完整交付") &&
                !g.conversation.text.Contains("所有物资已经亲自运回码头"),
                "All recovered field evidence still cannot bypass actual physical delivery");
            g.Close();g.save.flags.Add("tide_salvage_complete");
            g.Talk(chronicle);
            section=Array.FindIndex(g.conversation.choices,
                c=>c.action=="story_section_farshore");g.Choose(section);
            page=Array.FindIndex(g.conversation.choices,
                c=>c.action=="story_read_tidebound");g.Choose(page);
            Check(g.conversation.text.Contains("每次稳潮比过去多十五秒") &&
                g.conversation.textEn.Contains("fifteen seconds longer"),
                "Only the completed quest exposes the bilingual public tide model");
        }


        public static void TideboundEnvironmentHudTracksRealTideCyclesAndPublicExtension(Campaign data)
        {
            var g=Fresh(data);g.save.stage=3;
            foreach(string flag in new[]{"exp_intro","harbor_report","gate_5"})
                g.save.flags.Add(flag);
            g.Enter(5);g.enemies.Clear();
            WorldObject dock=Object(g,"tidedock");
            g.save.x=dock.x;g.save.y=dock.y;
            Check(g.TideSalvageTideHint.Contains("涨潮还有 22s") &&
                g.LowTide,"The HUD starts from the exact original ebb cycle");
            Action(g,"tidedock","tide_salvage_start");
            g.save.english=true;
            Check(g.Conditions().Contains("SALVAGE 100s") &&
                g.Conditions().Contains("LOW TIDE") &&
                g.Conditions().Contains("RISE IN 22s"),
                "Player sees the live sortie deadline and genuine rise countdown in both languages");
            g.save.totalTime=23;g.save.tideHeldUntil=0;
            Check(!g.LowTide && g.Conditions().Contains("HIGH TIDE") &&
                g.Conditions().Contains("EBB IN 13s"),
                "Actual flooded terrain produces a truthful ebb countdown");
            g.save.shards=1;
            Action(g,"tidedock","tide_salvage_hold");
            Check(g.LowTide && g.Conditions().Contains("HELD 18s") &&
                g.save.shards==0,
                "The paid tide hold immediately changes both physics and the HUD");
            g.save.totalTime=40.5f;
            Check(g.Conditions().Contains("HELD 1s"),
                "Displayed hold time follows the original total-time clock and never renews itself");
            g.save.totalTime=41.5f;
            Check(g.LowTide && g.Conditions().Contains("RISE IN") &&
                !g.Conditions().Contains("HELD"),
                "After a hold expires, the HUD returns to natural tide prediction");
            g.save.flags.Add("tide_salvage_complete");
            Check(g.TidewatchHoldSeconds==60,
                "Publishing salvage increases the real original station hold by fifteen seconds");
            g.save.flags.Add("farshore_relief_5");
            Check(g.TidewatchHoldSeconds==105,
                "The permanent upgrade composes with a previously upgraded 90-second relief station");
            g.PrepareSave();
            var loaded=new GameCore(data,CloneSave(g.save));loaded.Start();
            Check(loaded.save.version==2 && loaded.TidewatchHoldSeconds==105,
                "The compositional tide upgrade survives old v2 save persistence");
        }


        public static void TideboundRealShoalsStayReachableAndRisingTideRecoversTheHero(Campaign data)
        {
            var g=Fresh(data);g.save.stage=3;
            foreach(string flag in new[]{"exp_intro","harbor_report","gate_5"})
                g.save.flags.Add(flag);
            g.Enter(5);g.enemies.Clear();
            WorldObject dock=Object(g,"tidedock");
            g.save.x=dock.x;g.save.y=dock.y;
            Check(g.LowTide && g.CanStand(g.Position),
                "The original port dock is safe at natural ebb");
            for(int i=1;i<=3;i++)
            {
                WorldObject cache=Object(g,"tidecargo"+i);
                Check(g.Map.rows[(int)cache.y][(int)cache.x]=='%',
                    "All three distinct new cargo crates truly occupy original tidal shoal cells");
                Check(g.NavigationPath(new Point(cache.x,cache.y)).Count>0,
                    "Each shoal must be reachable from the unchanged harbor path network at low tide");
            }
            Action(g,"tidedock","tide_salvage_start");
            WorldObject west=Object(g,"tidecargo1");
            g.save.x=west.x;g.save.y=west.y;
            Point flooded=g.Position;
            g.save.totalTime=21.99f;g.save.tideHeldUntil=0;
            g.Tick(.03f,new Point());
            Check(!g.LowTide && g.CanStand(g.Position) &&
                (g.Position-flooded).Length>.4f &&
                g.TideSalvagePhase==1 && g.TideSalvageActive &&
                !g.Has("tide_salvage_seen_1"),
                "Real rising-tide collision moves the sole hero to dry footing without claiming or deleting undelivered cargo");
            // The next natural ebb reopens the identical unchanged shoal;
            // a washed-back run can continue rather than getting stuck.
            g.save.totalTime=35.99f;
            g.Tick(.03f,new Point());
            Check(g.LowTide && g.CanStand(flooded) &&
                g.TideSalvagePhase==1,
                "The real natural tide cycle reopens salvage routes without changing map tiles or resetting the mission");
        }

        public static void TideboundCargoCannotPublishUnconfirmedRaiderDisappearances(Campaign data)
        {
            var g=Fresh(data);g.save.stage=3;g.save.level=18;
            foreach(string flag in new[]{"exp_intro","harbor_report","gate_5"})
                g.save.flags.Add(flag);
            g.Enter(5);g.enemies.Clear();g.invulnerable=250;
            WorldObject dock=Object(g,"tidedock"),first=Object(g,"tidecargo1"),
                second=Object(g,"tidecargo2"),third=Object(g,"tidecargo3");
            int coins=g.save.coins,shards=g.save.shards,
                talents=g.save.skillPoints;
            g.save.x=dock.x;g.save.y=dock.y;g.save.totalTime=0;
            Action(g,"tidedock","tide_salvage_start");
            g.save.x=first.x;g.save.y=first.y;
            Action(g,"tidecargo1","tide_salvage_take_1");
            g.save.x=second.x;g.save.y=second.y;
            Action(g,"tidecargo2","tide_salvage_take_2");
            Check(g.TideSalvagePhase==3&&
                g.enemies.FindAll(g.IsTideSalvageEnemy).Count==2,
                "Inner shoal begins with two actual raiders");
            g.enemies.Remove(g.enemies.Find(g.IsTideSalvageEnemy));
            g.Tick(.05f,new Point());
            Check(!g.TideSalvageActive&&!g.Has("tide_salvage_complete")&&
                g.save.coins==coins&&g.save.shards==shards,
                "Missing inner-shoal raider resets the expedition without reward");
            g.save.x=dock.x;g.save.y=dock.y;
            Action(g,"tidedock","tide_salvage_start");
            Check(g.TideSalvagePhase==1,
                "Unconfirmed missing encounter can still be retried for free");
            g.save.x=first.x;g.save.y=first.y;
            Action(g,"tidecargo1","tide_salvage_take_1");
            g.save.x=second.x;g.save.y=second.y;
            Action(g,"tidecargo2","tide_salvage_take_2");
            g.save.totalTime=36;g.save.x=third.x;g.save.y=third.y;
            Action(g,"tidecargo3","tide_salvage_take_3");
            Check(g.TideSalvagePhase==4&&
                g.enemies.FindAll(g.IsTideSalvageEnemy).Count==3,
                "Later island cargo spawns the original third raider");
            g.enemies.RemoveAll(g.IsTideSalvageEnemy);
            g.save.x=dock.x;g.save.y=dock.y;g.Talk(dock);
            Check(g.conversation!=null&&
                Array.FindIndex(g.conversation.choices,
                    c=>c.action=="tide_salvage_finish")<0,
                "Dock with full cargo and emptied unverified roster cannot offer delivery");
            g.Close();g.Tick(.05f,new Point());
            Check(!g.TideSalvageActive&&!g.Has("tide_salvage_complete")&&
                g.save.coins==coins&&g.save.shards==shards&&
                g.save.skillPoints==talents&&g.TidewatchHoldSeconds==45,
                "Unconfirmed island raiders cannot grant coins, skill points or permanent tidal upgrades");
            g.PrepareSave();var loaded=new GameCore(data,CloneSave(g.save));
            Check(loaded.save.version==2&&!loaded.Has("tide_salvage_complete")&&
                !loaded.TideSalvageActive,
                "Legacy SaveData v2 never persists a false expedition victory");
        }

    }
}
