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
    }
}
