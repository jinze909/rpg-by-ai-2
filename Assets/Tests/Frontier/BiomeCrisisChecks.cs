using System;
using System.Collections.Generic;
namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        private static void RealReliefChoice(GameCore g,string action)
        {
            Check(g.mode=="dialog"&&g.conversation!=null,
                "Relief runs through an actual physically spoken dialogue");
            int i=Array.FindIndex(g.conversation.choices,c=>c.action==action);
            Check(i>=0,"Missing in-world emergency relief option "+action);
            g.Choose(i);
        }
        public static void EightEmergencyReliefRunsHavePhysicalRaidersHandoffsAndV2Receipts(Campaign data)
        {
            var hit=typeof(GameCore).GetMethod("Hit",
                System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.NonPublic);
            Check(hit!=null,"Real enemy hits are processed by production GameCore combat");
            for(int room=0;room<8;room++)
            {
                var g=new GameCore(data);
                g.Close();g.save.room=room;g.enemies.Clear();
                g.save.flags.Add("biome_field_done_"+room);
                bool medical=room%2==0;
                g.save.flags.Add("biome_supply_"+(medical?"medicine":"road")+"_"+room);
                WorldObject origin=Array.Find(data.maps[room].objects,
                    o=>o.id=="biomeSurvey"+room);
                WorldObject target=Array.Find(data.maps[room].objects,
                    o=>o.id=="biomeAnswer"+room);
                Check(origin!=null&&target!=null&&
                    (new Point(origin.x,origin.y)-new Point(target.x,target.y)).Length>45f&&
                    g.CanStand(new Point(origin.x,origin.y))&&
                    g.CanStand(new Point(target.x,target.y)),
                    "All relief runs span real walkable and distinct regional landmarks");
                g.save.x=origin.x;g.save.y=origin.y;
                g.Talk(origin);RealReliefChoice(g,"biome_crisis_begin");
                Check(g.BiomeCrisisActive&&g.BiomeCrisisStage==1&&
                    g.BiomeCrisisIntegrity==3&&g.BiomeCrisisRemaining==2&&
                    g.BiomeCrisisSeconds==300f,
                    "Actual region "+room+" starts a live 300s two-raider relief mission");
                var actors=new List<Enemy>(g.enemies);
                Check(actors.Count==2&&
                    actors.TrueForAll(e=>e!=null&&
                        (e.Position-new Point(target.x,target.y)).Length<12f),
                    "Real encounter attackers surround the authentic far monument");
                // Physical near-source and near-destination safety has no
                // virtual avatar or menu-only enemies: exactly two real rosters.
                g.save.x=target.x;g.save.y=target.y;
                g.Talk(target);
                Check(Array.FindIndex(g.conversation.choices,
                    c=>c.action=="biome_crisis_deliver")<0,
                    "A player cannot hand over materials without actual combat victories");
                g.Close();
                foreach(Enemy e in actors)
                    hit.Invoke(g,new object[]{e,10000f,false,null,null});
                for(int k=0;k<5;k++)g.Tick(.05f,new Point());
                Check(g.BiomeCrisisStage==2&&g.BiomeCrisisRemaining==0,
                    "Two lethal enemy callbacks and removal unlock the true handoff");
                g.save.x=target.x;g.save.y=target.y;
                int slot=medical?2:0;
                g.save.inventory[slot]=5;
                int beforeMaterials=g.save.inventory[slot];
                g.Talk(target);RealReliefChoice(g,"biome_crisis_deliver");
                Check(g.BiomeCrisisStage==3&&g.save.inventory[slot]==beforeMaterials-1,
                    "Remote relief physically consumes the right medical or road material");
                int coins=g.save.coins,shards=g.save.shards;
                g.save.x=origin.x;g.save.y=origin.y;
                g.Talk(origin);RealReliefChoice(g,"biome_crisis_report");
                Check(!g.BiomeCrisisActive&&g.BiomeCrisisWins==1&&
                    g.Has("biome_crisis_done_"+room)&&g.save.coins==coins+38&&
                    g.save.shards==shards+1,
                    "Only an actual full circuit pays the unique registered region reward");
                g.Talk(origin);
                Check(Array.FindIndex(g.conversation.choices,
                    c=>c.action=="biome_crisis_begin")<0,
                    "Cleared region cannot farm an instant repeat raid");
                g.PrepareSave();
                GameCore restored=new GameCore(data,g.save);
                Check(restored.save.version==2&&
                    restored.Has("biome_crisis_done_"+room)&&
                    restored.BiomeCrisisWins==1&&!restored.BiomeCrisisActive,
                    "Old SaveData v2 preserves cleared public receipts but no live enemy roster");
            }
        }
        public static void FinalEightReliefCouncilHasTwoRealPermanentAndExclusiveEndings(Campaign data)
        {
            for(int policy=0;policy<2;policy++)
            {
                GameCore g=new GameCore(data);
                g.Close();g.save.room=9;g.enemies.Clear();
                for(int room=0;room<8;room++)
                    g.save.flags.Add("biome_crisis_done_"+room);
                g.save.flags.Add("biome_crisis_all");
                Check(g.BiomeCrisisWins==8,
                    "Final council requires eight actually numbered signed receipts");
                WorldObject archive=Array.Find(g.Map.objects,
                    o=>o.kind=="annexinnarchive");
                Check(archive!=null,
                    "Final civic policy is debated at the real archived Hearth Inn desk");
                g.save.x=archive.x;g.save.y=archive.y;
                int before=g.save.coins,shards=g.save.shards;
                var spoof=typeof(GameCore).GetMethod("ChooseBiomeCrisisCouncil",
                    System.Reflection.BindingFlags.Instance|
                    System.Reflection.BindingFlags.NonPublic);
                Check(spoof!=null,"Real in-world relief council handler exists");
                spoof.Invoke(g,new object[]{"biome_crisis_council_roads"});
                Check(!g.Has("biome_crisis_council_roads")&&
                    g.save.coins==before,
                    "Direct council invocation outside an actual archive conversation cannot select a policy");
                g.Talk(archive);
                RealReliefChoice(g,"biome_crisis_council");
                string action=policy==0?
                    "biome_crisis_council_shelters":
                    "biome_crisis_council_roads";
                RealReliefChoice(g,action);
                Check(g.Has(action)&&
                    !g.Has(policy==0?"biome_crisis_council_roads":
                        "biome_crisis_council_shelters")&&
                    g.save.coins==before+60&&g.save.shards==shards+2,
                    "In-person Hearth Inn decision selects exactly one permanent policy and one reward");
                g.Close();g.save.room=9;
                g.save.x=archive.x;g.save.y=archive.y;g.Talk(archive);
                Check(Array.FindIndex(g.conversation.choices,
                    c=>c.action=="biome_crisis_council")<0,
                    "One-time public council cannot be repeated from the archive");
                g.PrepareSave();
                var reloaded=new GameCore(data,g.save);
                Check(reloaded.save.version==2&&reloaded.Has(action),
                    "Final branching public policy persists without a SaveData schema change");
                g.Close();g.Enter(0);g.enemies.Clear();
                if(policy==0)
                {
                    WorldObject camp=Array.Find(g.Map.objects,
                        o=>o.id=="annex_camp_0");
                    Check(camp!=null,"Public shelter policy uses the authored original camp");
                    g.save.x=camp.x;g.save.y=camp.y;
                    g.save.health=10f;g.save.energy=20f;
                    g.save.inventory[0]=5;
                    g.Talk(camp);RealReliefChoice(g,"annex_rest");
                    Check(g.save.health==60f&&g.save.energy==100f&&
                        g.save.inventory[0]==4,
                        "Public shelters truly increase an otherwise ordinary 40-HP paid rest to 50 HP");
                }
                else
                {
                    g.save.flags.Add("biome_field_done_0");
                    g.save.flags.Add("biome_supply_road_0");
                    g.save.flags.Add("biome_crisis_done_0");
                    WorldObject start=Array.Find(g.Map.objects,
                        o=>o.id=="biomeSurvey0");
                    WorldObject dest=Array.Find(g.Map.objects,
                        o=>o.id=="biomeAnswer0");
                    Check(start!=null&&dest!=null,
                        "Public road improvement uses the original two physically authored endpoints");
                    g.save.x=start.x;g.save.y=start.y;
                    g.save.energy=50f;g.save.coins=50;
                    g.Talk(start);RealReliefChoice(g,"biome_relay");
                    Check((g.Position-new Point(dest.x,dest.y)).Length<2f&&
                        g.save.energy==49f&&g.save.coins==49,
                        "Public road council actually reduces a signed route from 3 to 1 energy, still charges a fare");
                    GameCore baseline=new GameCore(data);
                    baseline.Close();baseline.save.room=0;
                    baseline.save.flags.Add("biome_supply_road_0");
                    baseline.save.flags.Add("biome_crisis_done_0");
                    float speed=baseline.Speed;
                    Check(Math.Abs(g.Speed-speed-.10f)<.001f,
                        "Public road council adds only its explicit permanent +0.10 movement speed");
                }
            }
        }

        public static void EmergencyReliefCannotBeSpoofedOrSurviveAbortedCombat(Campaign data)
        {
            var g=new GameCore(data);
            g.Close();g.save.room=0;g.enemies.Clear();
            g.save.flags.Add("biome_field_done_0");
            g.save.flags.Add("biome_supply_medicine_0");
            WorldObject start=Array.Find(data.maps[0].objects,
                o=>o.id=="biomeSurvey0");
            var direct=typeof(GameCore).GetMethod("ChooseBiomeCrisis",
                System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.NonPublic);
            Check(direct!=null,"Production relief uses an authenticated dialogue action");
            direct.Invoke(g,new object[]{"biome_crisis_begin"});
            Check(!g.BiomeCrisisActive&&g.enemies.Count==0,
                "Forged direct relief without origin conversation cannot spawn enemies");
            g.save.x=start.x;g.save.y=start.y;g.Talk(start);
            RealReliefChoice(g,"biome_crisis_begin");
            Check(g.BiomeCrisisActive&&g.BiomeCrisisRemaining==2,
                "Real spoken relief creates genuine combat state");
            int coins=g.save.coins,shards=g.save.shards;
            Enemy vanished=g.enemies[0];
            g.enemies.Remove(vanished);
            for(int t=0;t<5;t++)g.Tick(.05f,new Point());
            Check(!g.BiomeCrisisActive&&
                !g.Has("biome_crisis_done_0")&&
                g.save.coins==coins&&g.save.shards==shards,
                "A vanished attacker invalidates the contract without credit or loot");
            g.enemies.Clear();g.save.x=start.x;g.save.y=start.y;
            g.Talk(start);RealReliefChoice(g,"biome_crisis_begin");
            Check(g.BiomeCrisisRemaining==2,"The same real mission may be retried");
            g.Enter(1);
            Check(g.save.room==0&&g.BiomeCrisisActive,
                "Active escorted relief blocks regional teleport escape");
            g.save.x=start.x;g.save.y=start.y;
            g.Talk(start);RealReliefChoice(g,"biome_crisis_abort");
            Check(!g.BiomeCrisisActive&&g.enemies.Count==0&&
                g.save.coins==coins&&!g.Has("biome_crisis_done_0"),
                "Physical source withdrawal removes only its two attackers with no payout");
            g.Talk(start);RealReliefChoice(g,"biome_crisis_begin");
            var deadline=typeof(GameCore).GetField("biomeCrisisSeconds",
                System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.NonPublic);
            Check(deadline!=null,"Timed rescue runs on actual in-world time");
            deadline.SetValue(g,.01f);
            for(int t=0;t<5;t++)g.Tick(.05f,new Point());
            Check(!g.BiomeCrisisActive&&!g.Has("biome_crisis_done_0"),
                "World-time expiration ends the real raid without a reward");
        }
    }
}
