using System;
namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        public static void GlasswindCircuitHasRealCaptureWavesAndPermanentShade(Campaign data)
        {
            var g=Fresh(data);
            Check(g.GetQuest("glasswind").locked,"Old saves cannot start the optional quest without the desert report");
            WorldObject mia=Object(g,"glasswindkeeper");
            g.save.x=mia.x;g.save.y=mia.y;
            Check(g.CanStand(g.Position) && g.Nearest()==mia,"Mia must appear on walkable town ground");
            g.Talk(mia);
            Check(Array.FindIndex(g.conversation.choices,c=>c.action=="glasswind_accept")<0,
                "Mia cannot bypass the original main campaign desert investigation");
            g.Close();
            g.save.stage=3;g.save.level=20;g.save.xp=0;
            g.save.flags.Add("harbor_report");g.save.flags.Add("desert_report");
            Action(g,"glasswindkeeper","glasswind_accept");
            Check(g.Has("glasswind_started") && g.save.trackedQuest=="glasswind",
                "The physical NPC starts the actual tracked quest");
            g.Enter(6);g.enemies.Clear();g.invulnerable=250;
            int initialCoins=g.save.coins,initialShards=g.save.shards,
                normalKills=g.save.kills[6];
            for(int site=1;site<=3;site++)
            {
                WorldObject valve=Object(g,"glasswindrelay"+site);
                g.save.x=valve.x;g.save.y=valve.y;
                Check(g.CanStand(g.Position) && g.Nearest()==valve,
                    "Every authored valve is accessible and interactable");
                Action(g,valve.kind,"glasswind_start_"+site);
                Check(g.GlasswindSite==site && g.GlasswindWave==1 &&
                    g.enemies.FindAll(g.IsGlasswindEnemy).Count==2,
                    "First wave must actually deploy two reachable enemies");
                g.Enter(0);
                Check(g.save.room==6 && g.GlasswindSite==site,
                    "Ordinary portal/travel calls cannot erase an active capture mission");
                g.PrepareSave();
                Check(!Array.Exists(g.save.worldEnemies,r=>r!=null &&
                    r.id>=89810 && r.id<89900),
                    "Live trial enemies may not be stored as ordinary patrols");
                var interrupted=new GameCore(data,CloneSave(g.save));
                interrupted.Start();
                Check(interrupted.GlasswindSite==0 &&
                    !interrupted.enemies.Exists(interrupted.IsGlasswindEnemy) &&
                    !interrupted.Has("glasswind_"+site),
                    "Reload restarts incomplete trials without ghost enemies or fake clears");
                foreach(Enemy e in g.enemies.FindAll(g.IsGlasswindEnemy))
                {e.stun=200;e.timer=200;}
                g.save.x=valve.x+4.5f;g.save.y=valve.y;
                Advance(g,1);
                Check(g.GlasswindCharge==0,"Waiting far from a valve cannot capture it");
                g.save.x=valve.x;g.save.y=valve.y;
                Advance(g,4.25f);
                Check(g.GlasswindWave==2 &&
                    g.enemies.FindAll(g.IsGlasswindEnemy).Count==4,
                    "Half-charged territory produces a genuine second enemy wave");
                foreach(Enemy e in g.enemies.FindAll(g.IsGlasswindEnemy))
                {e.stun=200;e.timer=200;}
                Advance(g,4.2f);
                Check(g.GlasswindCharge>=8 &&
                    !g.Has("glasswind_"+site),
                    "A full capture meter alone cannot bypass the real combat objective");
                int coins=g.save.coins;
                Action(g,valve.kind,"glasswind_withdraw");
                Check(g.GlasswindSite==0 && !g.enemies.Exists(g.IsGlasswindEnemy) &&
                    g.save.coins==coins && !g.Has("glasswind_"+site),
                    "Withdrawal retires enemies without fake loot, flags or rewards");
                Action(g,valve.kind,"glasswind_start_"+site);
                Check(g.GlasswindSite==site,"Withdrawal allows a real free retry");
                foreach(Enemy e in g.enemies.FindAll(g.IsGlasswindEnemy))
                {e.stun=200;e.timer=200;}
                Advance(g,4.25f);
                foreach(Enemy e in g.enemies.FindAll(g.IsGlasswindEnemy))
                {e.stun=200;e.timer=200;}
                Advance(g,4.2f);
                Check(g.GlasswindWave==2 && g.GlasswindCharge>=8,
                    "Replayed fight must still require both waves and full capture");
                // Use the canonical player's actual combat input to finish,
                // rather than granting quest progress by deleting enemies.
                var opponents=g.enemies.FindAll(g.IsGlasswindEnemy);
                foreach(Enemy e in opponents)
                {
                    e.x=valve.x+1.05f;e.y=valve.y;e.hp=3;
                    e.stun=200;e.timer=200;
                    g.save.x=valve.x;g.save.y=valve.y;
                    g.save.energy=100;
                    for(int frame=0;frame<100 && g.enemies.Contains(e);frame++)
                    {
                        if(g.attackCooldown<=0)g.Attack(new Point(1,0));
                        g.Tick(.02f,new Point());
                    }
                    Check(!g.enemies.Contains(e),"Normal hero attacks must defeat trial machines");
                }
                Check(g.save.kills[6]==normalKills && g.save.coins==coins,
                    "Scripted combat cannot farm chapter kills or ordinary coins");
                Action(g,valve.kind,"glasswind_seal");
                Check(g.Has("glasswind_"+site) && g.GlasswindSite==0 &&
                    g.save.coins==coins+15 && g.save.shards==initialShards+site,
                    "Only actual physical sealing grants one site's reward");
                Check(g.GlasswindRefuge,"Sealed valves offer genuine local heat protection");
                g.save.heat=50;Advance(g,1);
                Check(g.save.heat<50,"Heat falls under a restored valve in the real climate tick");
                g.Talk(valve);
                Check(Array.FindIndex(g.conversation.choices,c=>c.action=="glasswind_seal")<0 &&
                    g.save.coins==coins+15,
                    "The completed valve removes its reward action instead of paying twice");
                g.Close();
            }
            Check(g.Has("glasswind_all_sealed") && !g.Has("glasswind_complete") &&
                g.save.coins==initialCoins+45,
                "Three site clears do not silently grant the final town reward");
            g.Enter(0);g.save.x=mia.x;g.save.y=mia.y;
            int finalCoins=g.save.coins,finalShards=g.save.shards,
                finalTalent=g.save.skillPoints;
            Action(g,"glasswindkeeper","glasswind_publish");
            Check(g.GetQuest("glasswind").complete &&
                g.save.coins==finalCoins+65 && g.save.shards==finalShards+2 &&
                g.save.skillPoints==finalTalent+1,
                "Public handoff grants the final resources and one talent exactly once");
            g.Talk(mia);
            Check(Array.FindIndex(g.conversation.choices,c=>c.action=="glasswind_publish")<0 &&
                g.save.coins==finalCoins+65,
                "Mia withdraws the one-time publication action after completion");
            g.Close();
            g.PrepareSave();
            var loaded=new GameCore(data,CloneSave(g.save));loaded.Start();
            Check(loaded.save.version==2 && loaded.Has("glasswind_complete") &&
                loaded.Has("glasswind_1") && loaded.Has("glasswind_3"),
                "Older v2 storage preserves all new permanent flags without a schema change");
        }
    }
}
