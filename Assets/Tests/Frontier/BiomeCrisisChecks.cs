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
