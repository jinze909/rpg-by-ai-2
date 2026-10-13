using System;
namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        public static void CampaignWitnessesArePhysicalThreeChapterStoryNotFreeFlags(Campaign data)
        {
            var g=Fresh(data);
            Check(g.GetQuest("campaign_witness").locked &&
                g.WitnessesFiled==0,"An untouched save must not grant unseen citizens a testimony");
            for(int region=1;region<=3;region++)
            {
                // Mirror chapter unlocks; never pretend the previous chapter
                // was completed by the new optional story interaction.
                g.save.stage=region-1;
                if(region==1)g.save.flags.Add("scout_saved");
                else g.save.flags.Add("gate_"+region);
                g.Enter(region);g.enemies.Clear();g.invulnerable=200;
                WorldObject npc=Object(g,"witnessnpc"+region),
                    first=Object(g,"witnesssite1"+region),
                    second=Object(g,"witnesssite2"+region);
                foreach(WorldObject o in new[]{npc,first,second})
                {
                    g.save.x=o.x;g.save.y=o.y;
                    Check(g.CanStand(g.Position) && g.Nearest()==o,
                        "Every newly authored chapter witness site is physically usable on original terrain: "+o.kind);
                }
                g.save.x=second.x;g.save.y=second.y;
                g.Talk(second);
                Check(g.conversation==null &&
                    !g.Has("witness_sealed_"+region),
                    "A field marker cannot fabricate a witness record before meeting its owner");
                g.save.x=npc.x;g.save.y=npc.y;
                Action(g,npc.kind,"witness_begin_"+region);
                Check(g.Has("witness_started_"+region) &&
                    g.GetQuest("campaign_witness").id=="campaign_witness",
                    "A real named NPC opens an actual tracked main story investigation");
                g.save.x=first.x;g.save.y=first.y;
                Check(g.Nearest()==first,"First independent record must be interactable");
                float initialEnergy=g.save.energy;
                int wrong=region==1?0:1;
                Action(g,first.kind,"witness_answer_"+region+"_"+wrong);
                Check(g.save.energy==initialEnergy-8 &&
                    !g.Has("witness_read_"+region),
                    "Wrong historical reasoning costs energy, never invents original evidence");
                // Supply a real field medicine/part where the plot requires one.
                if(region==1)g.save.inventory[2]=Math.Max(1,g.save.inventory[2]);
                if(region==3)g.save.inventory[4]=Math.Max(1,g.save.inventory[4]);
                int item=region==1?2:region==3?4:-1;
                int prior=item>=0?g.save.inventory[item]:0;
                Action(g,first.kind,"witness_answer_"+region+"_"+(region==1?1:2));
                Check(g.Has("witness_read_"+region) && 
                    (item<0 || g.save.inventory[item]==prior-1),
                    "Right action physically restores the first record with genuine chapter-specific materials");
                g.save.x=second.x;g.save.y=second.y;
                Check(g.Nearest()==second,"Final evidence seal must be usable on real map");
                int oldCoins=g.save.coins,oldKills=g.save.kills[region];
                Action(g,second.kind,"witness_defend_"+region);
                Check(g.WitnessDefenseRoom==region &&
                    g.WitnessDefenseTime>=64 &&
                    g.enemies.FindAll(g.IsWitnessEnemy).Count==2,
                    "Live chapter defense spawns two actual patrol guards within 65 seconds");
                g.Enter(0);
                Check(g.save.room==region && g.WitnessDefenseRoom==region,
                    "A story combat cannot be evaded by opening fast travel");
                g.PrepareSave();
                Check(!Array.Exists(g.save.worldEnemies,x=>x!=null && 
                    x.id>=89972 && x.id<=89977),
                    "Field-story encounter guards do not pollute ordinary enemy saves");
                var reloaded=new GameCore(data,CloneSave(g.save));reloaded.Start();
                Check(reloaded.WitnessDefenseRoom==0 &&
                    !reloaded.enemies.Exists(reloaded.IsWitnessEnemy) &&
                    reloaded.Has("witness_read_"+region) &&
                    !reloaded.Has("witness_defeated_"+region),
                    "Old v2 serialization preserves verified memory but never teleports ghost guards into a new session");
                g.save.x=second.x;g.save.y=second.y;
                g.Talk(second);
                Check(Array.FindIndex(g.conversation.choices,c=>
                    c.action=="witness_seal_"+region)<0 &&
                    !g.Has("witness_defeated_"+region),
                    "Reading and accepting a quest cannot skip the combat objective");
                g.Close();
                foreach(Enemy e in g.enemies.FindAll(g.IsWitnessEnemy))
                {
                    e.stun=100;e.timer=100;e.hp=3;
                    e.x=second.x+1.05f;e.y=second.y;
                    g.save.x=second.x;g.save.y=second.y;
                    for(int tick=0;tick<100 && g.enemies.Contains(e);tick++)
                    {
                        if(g.attackCooldown<=0)g.Attack(new Point(1,0));
                        g.Tick(.02f,new Point());
                    }
                    Check(!g.enemies.Contains(e),
                        "Canonical gold-haired hero must physically defeat each chapter security guard");
                }
                g.Tick(.02f,new Point());
                Check(g.WitnessDefenseRoom==0 &&
                    g.Has("witness_defeated_"+region) &&
                    !g.Has("witness_complete_"+region) &&
                    g.save.coins==oldCoins && g.save.kills[region]==oldKills,
                    "Only combat clears security; no premature story rewards or farming chapter kills");
                g.save.x=second.x;g.save.y=second.y;
                Action(g,second.kind,"witness_seal_"+region);
                Check(g.Has("witness_sealed_"+region) &&
                    !g.Has("witness_complete_"+region),
                    "A separate field archive action is needed after the genuine fight");
                g.save.x=npc.x;g.save.y=npc.y;
                Action(g,npc.kind,"witness_finish_"+region);
                Check(g.Has("witness_complete_"+region) &&
                    g.WitnessesFiled==region &&
                    g.save.coins==oldCoins+15+region*10,
                    "Actual living witness signs one truthful testimony and receives one chapter reward");
                g.Talk(npc);
                Check(Array.FindIndex(g.conversation.choices,c=>
                    c.action=="witness_finish_"+region)<0,
                    "A completed witness cannot duplicate the chapter reward");
                g.Close();
            }
            Check(g.AllWitnessesFiled &&
                g.GetQuest("campaign_witness").complete,
                "Three fully experienced chapters culminate in three publicly signed accounts");
            g.PrepareSave();
            var persisted=new GameCore(data,CloneSave(g.save));persisted.Start();
            Check(persisted.save.version==2 && persisted.AllWitnessesFiled,
                "Full three-witness main story survives SaveData v2 serialization without a schema change");
        }

        public static void CampaignWitnessesWeakenOnlyRealChapterFourSecurity(Campaign data)
        {
            var g=Fresh(data);g.save.stage=3;
            foreach(string f in new[]{"core_1","core_2","core_3","triad_started",
                "triad_evidence_1","triad_evidence_2","triad_evidence_3"})
                g.save.flags.Add(f);
            g.Enter(4);g.enemies.Clear();
            WorldObject desk=Object(g,"triadnexus");
            g.save.x=desk.x;g.save.y=desk.y;
            Action(g,desk.kind,"triad_defense_start");
            Enemy original=g.enemies.Find(g.IsTriadEnemy);
            Check(original!=null && Math.Abs(original.maxHp-210)<.1f,
                "Original untouched saves retain the established 210-HP security boss");
            g.save.x=desk.x;g.save.y=desk.y;
            Action(g,desk.kind,"triad_abort");g.enemies.Clear();
            for(int i=1;i<=3;i++)g.save.flags.Add("witness_complete_"+i);
            Action(g,desk.kind,"triad_defense_start");
            Enemy reduced=g.enemies.Find(g.IsTriadEnemy);
            Check(reduced!=null && Math.Abs(reduced.maxHp-165)<.1f &&
                reduced.hp>0 && !g.Has("triad_defense_clear"),
                "Three physical testimony completions weaken real security from 210 to 165 without a victory skip");
        }

        public static void CampaignWitnessesAbortTimeoutAndChroniclePreserveOnlyEarnedHistory(Campaign data)
        {
            var g=Fresh(data);g.save.flags.Add("scout_saved");g.Enter(1);
            g.enemies.Clear();WorldObject npc=Object(g,"witnessnpc1"),
                first=Object(g,"witnesssite11"),second=Object(g,"witnesssite21");
            g.save.x=npc.x;g.save.y=npc.y;
            Action(g,npc.kind,"witness_begin_1");
            g.save.x=first.x;g.save.y=first.y;
            Action(g,first.kind,"witness_answer_1_1");
            g.save.x=second.x;g.save.y=second.y;g.invulnerable=120;
            Action(g,second.kind,"witness_defend_1");
            foreach(Enemy e in g.enemies.FindAll(g.IsWitnessEnemy))
                {e.stun=200;e.timer=200;}
            Advance(g,3.5f);
            Check(g.hazards.Exists(h=>h.witnessOwned && h.timer>0 && h.damage>0),
                "A real delayed forest field hazard appears and would hurt a stationary hero");
            var other=new Hazard{position=new Point(12,14),timer=90,
                radius=.6f,damage=2,kind="environment"};
            var effect=new Effect("discover",new Point(11,14),90);
            g.hazards.Add(other);g.effects.Add(effect);
            g.save.x=second.x;g.save.y=second.y;
            Action(g,second.kind,"witness_abort_1");
            Check(g.WitnessDefenseRoom==0 && !g.enemies.Exists(g.IsWitnessEnemy) &&
                !g.hazards.Exists(h=>h.witnessOwned) &&
                g.hazards.Contains(other) && g.effects.Contains(effect) &&
                g.Has("witness_read_1") && !g.Has("witness_defeated_1"),
                "Field withdrawal retires only its owned combat resources, never unrelated hazards or legitimate evidence");
            g.hazards.Clear();g.effects.Clear();g.enemies.Clear();
            Action(g,second.kind,"witness_defend_1");
            Check(g.WitnessDefenseRoom==1,"A real free retry can start");
            foreach(Enemy e in g.enemies.FindAll(g.IsWitnessEnemy))
                {e.stun=200;e.timer=200;}
            g.invulnerable=200;Advance(g,65.3f);
            Check(g.WitnessDefenseRoom==0 && !g.Has("witness_defeated_1") &&
                !g.enemies.Exists(g.IsWitnessEnemy),
                "Timeout retires its enemy state without inventing a witness victory");
            g.Enter(0);g.save.x=13.5f;g.save.y=9.5f;
            WorldObject book=Object(g,"worldchronicle");
            g.Talk(book);
            int section=Array.FindIndex(g.conversation.choices,c=>c.action=="story_section_campaign");
            g.Choose(section);
            int entry=Array.FindIndex(g.conversation.choices,c=>c.action=="story_read_testimonies");
            Check(entry>=0,"The chronicle unlocks only after genuine interaction with a witness");
            g.Choose(entry);
            Check(g.conversation.text.Contains("莫芽尚未署名") &&
                !g.conversation.text.Contains("莫芽署名："),
                "Unfinished evidence must not be described as a living resident's signature");
            g.Close();
            g.save.flags.Add("witness_complete_1");
            g.Talk(book);
            section=Array.FindIndex(g.conversation.choices,c=>c.action=="story_section_campaign");
            g.Choose(section);
            entry=Array.FindIndex(g.conversation.choices,c=>c.action=="story_read_testimonies");
            g.Choose(entry);
            Check(g.conversation.text.Contains("莫芽署名：") &&
                !g.conversation.text.Contains("阿菱署名："),
                "Bilingual campaign archive publishes only truly signed chapter witnesses");
        }
    }
}
