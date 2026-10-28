using System;
using System.Reflection;
namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        private static GameCore PressureworksReady(Campaign data)
        {
            var g=Fresh(data);g.save.stage=3;g.save.level=9;
            g.Enter(3);
            Check(g.save.room==3,"True chapter travel reaches Ironreach work district");
            g.save.flags.Add("boss_3");g.save.flags.Add("gate_3");
            g.enemies.Clear();g.projectiles.Clear();g.invulnerable=150f;
            return g;
        }
        private static WorldObject PressureworksPost(GameCore g,string id)
        {
            foreach(WorldObject o in g.Map.objects)if(o.id==id)return o;
            throw new Exception("Missing authored pressureworks machine "+id);
        }
        private static bool PressureworksChoice(GameCore g,string action)
        {
            if(g.mode!="dialog"||g.conversation==null)return false;
            int slot=Array.FindIndex(g.conversation.choices,c=>c.action==action);
            if(slot<0)return false;
            g.Choose(slot);return true;
        }
        private static void SetPressureworksFoot(GameCore g,WorldObject o)
        {
            Check(g.CanStand(new Point(o.x,o.y)),
                "Every real production machine is anchored in standable collision");
            g.save.x=o.x;g.save.y=o.y;
        }
        private static void KillPressureworksGuards(GameCore g)
        {
            var hit=typeof(GameCore).GetMethod("Hit",
                BindingFlags.NonPublic|BindingFlags.Instance);
            Check(hit!=null,"Original combat death method is available");
            int killed=0;
            foreach(Enemy e in g.enemies.FindAll(g.PressureworksOwnedGuard))
            {
                e.hp=6f;e.stun=160f;e.timer=160f;
                hit.Invoke(g,new object[]{e,9000f,false,null,null});
                killed++;
            }
            Check(killed==2,"Two distinct hostile actors must exist in the engine room");
            g.Tick(.05f,new Point());
        }
        private static GameCore StartPressureworksRun(Campaign data)
        {
            var g=PressureworksReady(data);
            var notice=PressureworksPost(g,"pressureworks_notice");
            var first=PressureworksPost(g,"pressureworks_valve1");
            var second=PressureworksPost(g,"pressureworks_valve2");
            SetPressureworksFoot(g,notice);g.Talk(notice);
            Check(PressureworksChoice(g,"pressureworks_begin")&&
                g.PressureworksStage==1&&g.PressureworksTime>170f,
                "Distinct real factory notice starts a simulated timed boiler");
            Check((g.GetQuest("pressureworks").target-new Point(first.x,first.y)).Length<.01f,
                "Journal routes to actual first wheel on real map");
            g.Enter(0);
            Check(g.save.room==3&&g.PressureworksStage==1,
                "Active pressureworks blocks unrelated world travel");
            SetPressureworksFoot(g,first);g.Talk(first);
            Check(PressureworksChoice(g,"pressureworks_valve1")&&
                g.PressureworksStage==2&&g.PressureworksPressure<30f,
                "First physically reached coolant wheel vents exactly 16 pressure");
            SetPressureworksFoot(g,second);g.Talk(second);
            Check(PressureworksChoice(g,"pressureworks_valve2")&&
                g.PressureworksStage==3&&g.PressureworksGuardsAlive==2,
                "Second real valve authorizes two actual hostile enemy bodies");
            Check((g.GetQuest("pressureworks").target-
                new Point(67.5f,44.5f)).Length<.01f,
                "Journal now tracks the genuinely guarded engine room");
            return g;
        }
        public static void PressureworksTwoPhysicalValvesLiveSteamCombatAndWorkerCooperative(Campaign data)
        {
            var g=StartPressureworksRun(data);
            var engine=PressureworksPost(g,"pressureworks_engine");
            foreach(Enemy e in g.enemies.FindAll(g.PressureworksOwnedGuard))
            {e.stun=160f;e.timer=160f;}
            bool realSteam=false;
            for(int i=0;i<85;i++)
            {
                g.Tick(.05f,new Point());
                if(g.hazards.Exists(h=>h.pressureworksOwned&&
                    h.kind=="glass"&&h.damage>=12f&&h.timer>0f))
                    realSteam=true;
            }
            Check(realSteam&&g.PressureworksStage==3,
                "Engine creates real damage-bearing telegraphed steam hazards in combat");
            g.save.inventory[4]=2;
            SetPressureworksFoot(g,engine);g.Talk(engine);
            int choice=Array.FindIndex(g.conversation.choices,c=>c.action=="pressureworks_purge");
            Check(choice>=0,"The production engine exposes paid manual pressure purge");
            float pre=g.PressureworksPressure;
            g.save.x=engine.x+8f;g.Choose(choice);
            Check(g.save.inventory[4]==2&&Math.Abs(g.PressureworksPressure-pre)<.001f,
                "Cached engine choice cannot remotely purge or spend a part");
            SetPressureworksFoot(g,engine);g.Talk(engine);
            Check(PressureworksChoice(g,"pressureworks_purge")&&
                g.save.inventory[4]==1&&g.PressureworksPressure<=pre-21.9f&&
                !g.hazards.Exists(h=>h.pressureworksOwned),
                "One real spare part lowers pressure and clears ONLY owned steam");
            g.Talk(engine);PressureworksChoice(g,"pressureworks_purge");
            Check(g.save.inventory[4]==1,
                "One attempt cannot buy repeated free steam shields");
            g.Close();
            KillPressureworksGuards(g);
            Check(g.PressureworksStage==4&&g.PressureworksGuardsAlive==0&&
                !g.hazards.Exists(h=>h.pressureworksOwned),
                "Two real confirmed combat kills retire the pressure hazards and stage exit");
            int coins=g.save.coins,shards=g.save.shards;
            float beforeEnergy=g.save.energy;
            SetPressureworksFoot(g,engine);g.Talk(engine);
            Check(PressureworksChoice(g,"pressureworks_workers")&&
                g.PressureworksComplete&&g.PressureworksWorkers&&
                !g.PressureworksCaravan&&g.save.coins==coins+70&&
                g.save.shards==shards+3,
                "Physical engine awards exactly one permanent worker-owned civic ending");
            g.save.energy=30f;
            g.Tick(1f,new Point());
            Check(g.save.energy>44f&&g.save.energy<=100f,
                "Worker-owned pressureworks permanently improves actual local stamina recovery");
            g.PrepareSave();
            var reloaded=new GameCore(data,CloneSave(g.save));
            Check(reloaded.save.version==2&&reloaded.PressureworksComplete&&
                reloaded.PressureworksWorkers&&!reloaded.PressureworksActive,
                "Original v2 save retains the earned worker policy but no combat clock");
        }
        public static void PressureworksAlternativeCaravanPowerAndCounterfeitGuardProtection(Campaign data)
        {
            var g=StartPressureworksRun(data);
            WorldObject engine=PressureworksPost(g,"pressureworks_engine");
            var original=g.enemies.Find(g.PressureworksOwnedGuard);
            Check(original!=null,"At least one physically owned guard is alive");
            var fake=new Enemy(original.id,original.kind,original.x,original.y,
                original.maxHp);
            g.enemies.Remove(original);g.enemies.Add(fake);
            g.Tick(.05f,new Point());
            Check(!g.PressureworksActive&&!g.PressureworksComplete&&
                g.enemies.Contains(fake),
                "Numerically cloned guard cannot replace missing original or fake confirmed kills");
            g.enemies.Remove(fake);
            g=StartPressureworksRun(data);
            SetPressureworksFoot(g,engine=PressureworksPost(g,"pressureworks_engine"));
            int initialDamage=(int)g.Damage;
            KillPressureworksGuards(g);
            g.Talk(engine);
            Check(PressureworksChoice(g,"pressureworks_caravan")&&
                g.PressureworksCaravan&&!g.PressureworksWorkers&&
                g.Damage>=initialDamage+2.9f,
                "Alternative public schematics unlock real regional attack bonus");
            g.Talk(engine);
            Check(!PressureworksChoice(g,"pressureworks_workers")&&
                !g.PressureworksWorkers,
                "Completed mutually exclusive factory chapter cannot duplicate the opposite payout");
            g.PrepareSave();var next=new GameCore(data,CloneSave(g.save));
            Check(next.save.version==2&&next.PressureworksCaravan&&
                next.Damage>=initialDamage+2.9f,
                "Caravan branch remains effective through old v2 save round-trip");
        }
        public static void PressureworksPermanentlyChangesOriginalLiveMinecartSteamAndSafeRoute(Campaign data)
        {
            float[] steamLoss=new float[2];
            for(int branch=0;branch<2;branch++)
            {
                var g=PressureworksReady(data);
                if(branch==1)g.save.flags.Add("pressureworks_workers");
                WorldObject dispatch=MineCartTestPost(g,1),
                    relay=MineCartTestPost(g,2);
                SetPressureworksFoot(g,dispatch);g.Talk(dispatch);
                Check(MineCartTestChoice(g,"mineconvoy_start")&&
                    g.MineConvoyTime==110&&g.MineConvoyStage==1,
                    "Normal civilian cart still uses its honest 110-second route");
                WalkBesideMinecart(g,new Point(relay.x,relay.y),1);
                Check(g.MineConvoyStage==2,
                    "Original physically escorted minecart reaches its live ambush");
                DefeatMinecartRaiders(g,relay);
                SetPressureworksFoot(g,relay);g.Talk(relay);
                Check(MineCartTestChoice(g,"mineconvoy_go")&&
                    g.MineConvoyStage==3,"Original cart resumes after two authentic kills");
                g.save.x=dispatch.x;g.save.y=dispatch.y;
                float hull=g.MineConvoyIntegrity;
                for(int i=0;i<140;i++)g.Tick(.05f,new Point());
                steamLoss[branch]=hull-g.MineConvoyIntegrity;
                Check(g.MineConvoyStage==3&&steamLoss[branch]>0f,
                    "Actual timed minecart steam harms the carriage while the hero stands away");
            }
            Check(Math.Abs(steamLoss[0]-12f)<.1f&&
                Math.Abs(steamLoss[1]-8f)<.1f,
                "Public worker-operated coolant cuts true cart steam hull damage 12 to 8 without erasing live risks");
            var caravan=PressureworksReady(data);
            caravan.save.flags.Add("pressureworks_caravan");
            var west=MineCartTestPost(caravan,1);
            SetPressureworksFoot(caravan,west);caravan.Talk(west);
            Check(MineCartTestChoice(caravan,"mineconvoy_start")&&
                caravan.MineConvoyTime==125f&&caravan.MineConvoyStage==1,
                "Published civilian route drawings extend the authentic minecart deadline by 15 seconds");
            caravan.PrepareSave();
            var loaded=new GameCore(data,CloneSave(caravan.save));
            Check(loaded.save.version==2&&loaded.PressureworksCaravan&&
                !loaded.MineConvoyActive,
                "Regional outcome is durable in original v2 slots, while an in-flight cart is transient");
        }

        public static void PressureworksRealPressureOverloadAndOriginalBossGate(Campaign data)
        {
            var g=PressureworksReady(data);
            var notice=PressureworksPost(g,"pressureworks_notice");
            var valve=PressureworksPost(g,"pressureworks_valve1");
            g.save.flags.Remove("boss_3");SetPressureworksFoot(g,notice);
            g.Talk(notice);
            Check(!PressureworksChoice(g,"pressureworks_begin")&&
                g.PressureworksStage==0,
                "Original Ironreach guardian remains mandatory before the industrial chapter");
            g.save.flags.Add("boss_3");g.Talk(notice);
            int start=Array.FindIndex(g.conversation.choices,c=>c.action=="pressureworks_begin");
            Check(start>=0,"The actual separate notice offers a start after Boss clear");
            SetPressureworksFoot(g,valve);g.Choose(start);
            Check(!g.PressureworksActive,
                "Stale remote notice cannot bypass real physical reach");
            SetPressureworksFoot(g,notice);g.Talk(notice);
            PressureworksChoice(g,"pressureworks_begin");
            Check(g.PressureworksStage==1,"Real notice starts the pressure clock");
            for(int i=0;i<200&&g.PressureworksActive;i++)g.Tick(1f,new Point());
            Check(!g.PressureworksActive&&!g.PressureworksComplete,
                "Unfinished timed pressure run really fails and never pays permanent rewards");
        }
    }
}
