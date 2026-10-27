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
                if(region==1)
                {
                    g.save.x=first.x;g.save.y=first.y;g.Talk(first);
                    int stale=Array.FindIndex(g.conversation.choices,
                        c=>c.action=="witness_answer_"+region+"_"+wrong);
                    Check(stale>=0,"The physical first record exposes a real interpretation choice");
                    Point outside=StandableOutsideTalkReach(g,first);
                    float energyBeforeStale=g.save.energy;
                    g.save.x=outside.x;g.save.y=outside.y;g.Choose(stale);g.Close();
                    Check(!g.Has("witness_read_"+region)&&g.save.energy==energyBeforeStale,
                        "A stale witness-answer dialog cannot change evidence or drain energy remotely");
                    g.save.x=first.x;g.save.y=first.y;
                }
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
                SetOuterPatrolLull(g,region);
                Check(g.OuterPatrolActive&&g.SafeTravel,
                    "The testimony marker is locally safe during an unrelated patrol lull");
                Action(g,second.kind,"witness_defend_"+region);
                Check(g.WitnessDefenseRoom==0&&!g.enemies.Exists(g.IsWitnessEnemy),
                    "Witness security cannot start over another active field mission");
                ClearOuterPatrolLull(g);
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
                if(region==1)
                {
                    g.Talk(npc);
                    int finish=Array.FindIndex(g.conversation.choices,
                        c=>c.action=="witness_finish_"+region);
                    Check(finish>=0,"The living witness offers a real signature choice after field preservation");
                    Point outsideNpc=StandableOutsideTalkReach(g,npc);
                    int beforeCoins=g.save.coins,beforeXp=g.save.xp;
                    g.save.x=outsideNpc.x;g.save.y=outsideNpc.y;g.Choose(finish);g.Close();
                    Check(!g.Has("witness_complete_"+region)&&g.save.coins==beforeCoins&&
                        g.save.xp==beforeXp,
                        "A stale witness-signature dialog cannot award chapter rewards remotely");
                    g.save.x=npc.x;g.save.y=npc.y;
                }
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

        public static void SignedCampaignWitnessesOpenOnlySafeOnSitePublicRefuges(Campaign data)
        {
            var g=Fresh(data);
            for(int region=1;region<=3;region++)
            {
                g.save.stage=region-1;
                if(region==1)g.save.flags.Add("scout_saved");
                else g.save.flags.Add("gate_"+region);
                g.Enter(region);g.enemies.Clear();
                WorldObject marker=Object(g,"witnesssite2"+region);
                g.save.x=marker.x;g.save.y=marker.y;
                g.save.health=40;g.invulnerable=50;
                Advance(g,2);
                Check(g.save.health==40,
                    "An unfiled witness cannot secretly create a public healing refuge");
                g.save.flags.Add("witness_complete_"+region);
                Advance(g,2);
                Check(g.save.health>42 && g.save.health<45,
                    "A genuinely signed chapter story turns its actual field seal into modest out-of-combat recovery");
                g.save.health=40;
                g.save.x=marker.x+5f;g.save.y=marker.y;
                Advance(g,2);
                Check(g.save.health==40,
                    "Witness field refuge has real radius limits, never heals the entire map");
                g.save.x=marker.x;g.save.y=marker.y;
                Enemy attacker=new Enemy(88123+region,0,marker.x+.7f,marker.y,20);
                attacker.stun=200;attacker.timer=200;g.enemies.Add(attacker);
                g.save.health=40;
                Advance(g,2);
                Check(g.save.health==40,
                    "A public story refuge cannot erase combat risk while an enemy contests its location");
                g.enemies.Clear();
            }
        }

        public static void CampaignWitnessSignaturesActuallyChangeLyraAndFinaleSpeech(Campaign data)
        {
            var g=Fresh(data);
            WorldObject lyra=Object(g,"guild");
            g.save.x=lyra.x;g.save.y=lyra.y;
            g.save.flags.Add("core_1");
            g.Talk(lyra);
            Check(!g.conversation.text.Contains("莫芽的证词"),
                "First core delivery must not claim unearned resident testimony");
            g.Close();
            g.save.flags.Add("witness_complete_1");
            g.Talk(lyra);
            Check(g.conversation.text.Contains("莫芽的证词") &&
                g.conversation.textEn.Contains("Moya's testimony"),
                "Lyra acknowledges the first chapter's actual signed account during original core delivery");
            g.Close();
            g.save.stage=3;
            foreach(string f in new[]{"core_2","core_3","witness_complete_2","witness_complete_3"})
                g.save.flags.Add(f);
            g.Talk(lyra);
            Check(g.conversation.text.Contains("三位证人") &&
                g.conversation.textEn.Contains("signed their original field testimonies"),
                "The new living witnesses are named when Lyra proposes the true Chapter Four audit");
            g.Close();
            g.save.flags.Add("boss_4");
            g.Enter(4);g.enemies.Clear();
            WorldObject shrine=Object(g,"shrine");
            g.save.x=shrine.x;g.save.y=shrine.y;
            g.Talk(shrine);
            Check(g.conversation.text.Contains("莫芽、阿菱和维恩") &&
                g.conversation.textEn.Contains("Moya, Arin and Venn"),
                "Final confrontation includes the real people found earlier in the game");
            g.Close();
            g.save.ending=1;g.Epilogue();
            Check(g.conversation.text.Contains("密林的莫芽") &&
                g.conversation.textEn.Contains("Moya of Thornwood"),
                "Original ending remains intact and reflects real first-three-chapter survivors");
        }


        public static void ActiveChapterWitnessGuidanceTracksTheActualCurrentCharacter(Campaign data)
        {
            var g=Fresh(data);
            g.save.stage=2;
            foreach(string f in new[]{"scout_saved","gate_2","gate_3"})
                g.save.flags.Add(f);
            g.Enter(2);g.enemies.Clear();
            WorldObject arin=Object(g,"witnessnpc2"),
                first=Object(g,"witnesssite12"),second=Object(g,"witnesssite22");
            g.save.x=arin.x;g.save.y=arin.y;
            Action(g,arin.kind,"witness_begin_2");
            QuestGuide quest=g.CurrentGuide;
            Check(quest.id=="campaign_witness" && quest.room==2 &&
                (quest.target-new Point(first.x,first.y)).Length<.01f,
                "Starting Arin's live chapter must navigate to her first site, not an earlier unfinished Moya quest");
            g.save.flags.Add("witness_read_2");
            quest=g.CurrentGuide;
            Check(quest.room==2 &&
                (quest.target-new Point(second.x,second.y)).Length<.01f,
                "After authentic field evidence, live guidance points to the physical defense location");
            g.save.flags.Add("witness_sealed_2");
            quest=g.CurrentGuide;
            Check(quest.room==2 &&
                (quest.target-new Point(arin.x,arin.y)).Length<.01f,
                "A preserved testimony correctly routes back to the actual living survivor for signature");
            g.save.flags.Add("witness_complete_2");
            quest=g.CurrentGuide;
            Check(quest.room==1 && !quest.complete,
                "Finishing a middle chapter leaves earlier and later stories available without fabricating completion");
            g.Enter(3);g.enemies.Clear();
            g.save.flags.Add("witness_started_3");
            quest=g.CurrentGuide;
            Check(quest.room==3 &&
                quest.next.Contains("第一处原始记录"),
                "A newly undertaken miner investigation outranks incomplete earlier optional testimony");
        }





        public static void WitnessSecurityNeverSpawnsInsidePhysicalCompanion(Campaign data)
        {
            for(int region=1;region<=3;region++)
            {
                var g=Fresh(data);g.save.stage=region;
                g.save.flags.Add(region==1?"scout_saved":"gate_"+region);
                g.save.flags.Add("companion_1");
                g.Enter(region);g.enemies.Clear();g.invulnerable=200;
                var npc=Object(g,"witnessnpc"+region);
                var source=Object(g,"witnesssite1"+region);
                var archive=Object(g,"witnesssite2"+region);
                g.save.x=npc.x;g.save.y=npc.y;
                Action(g,npc.kind,"witness_begin_"+region);
                g.save.x=source.x;g.save.y=source.y;
                if(region==1)g.save.inventory[2]=Math.Max(1,g.save.inventory[2]);
                if(region==3)g.save.inventory[4]=Math.Max(1,g.save.inventory[4]);
                Action(g,source.kind,"witness_answer_"+region+"_"+(region==1?1:2));
                g.save.x=archive.x;g.save.y=archive.y;
                g.save.companion=-1;
                Action(g,archive.kind,"witness_defend_"+region);
                Check(g.WitnessDefenseRoom==region&&
                    g.enemies.FindAll(g.IsWitnessEnemy).Count==2,
                    "The real witness encounter can place its two original connected guards");
                Point authored=g.enemies.Find(g.IsWitnessEnemy).Position;
                Action(g,archive.kind,"witness_abort_"+region);
                Check(g.WitnessDefenseRoom==0,"Original archive can safely abort the first encounter");
                g.save.companion=1;
                g.companionPosition=authored;
                Action(g,archive.kind,"witness_defend_"+region);
                if(g.WitnessDefenseRoom==region)
                {
                    var guards=g.enemies.FindAll(g.IsWitnessEnemy);
                    Check(guards.Count==2&&
                        (guards[0].Position-guards[1].Position).Length>=1.8f,
                        "Both genuine guards have distinct original deployment feet");
                    foreach(Enemy e in guards)
                        Check((e.Position-g.companionPosition).Length>=1.6f&&
                            g.CanStand(e.Position)&&
                            (e.Position-g.Position).Length>=1.6f,
                            "A physically stationed recruited ally can never share a newly spawned hostile foot");
                    Action(g,archive.kind,"witness_abort_"+region);
                }
                else
                {
                    Check(g.enemies.FindAll(e=>e.id>=89972&&e.id<=89977).Count==0,
                        "If an ally occupies the only safe formation, no partially spawned guardians remain");
                }
                g.companionPosition=g.Position;
                Action(g,archive.kind,"witness_defend_"+region);
                Check(g.WitnessDefenseRoom==region&&
                    g.enemies.FindAll(g.IsWitnessEnemy).Count==2,
                    "Repositioning the ally restores the authored two-guardian challenge with no lost evidence");
            }
        }

        public static void WitnessAbortRetiresOnlyActualGuardsAfterRegionTransition(Campaign data)
        {
            var g=Fresh(data);g.save.flags.Add("scout_saved");
            g.Enter(1);g.enemies.Clear();
            WorldObject npc=Object(g,"witnessnpc1");
            WorldObject first=Object(g,"witnesssite11");
            WorldObject archive=Object(g,"witnesssite21");
            g.save.x=npc.x;g.save.y=npc.y;
            Action(g,npc.kind,"witness_begin_1");
            g.save.x=first.x;g.save.y=first.y;
            g.save.inventory[2]=Math.Max(1,g.save.inventory[2]);
            Action(g,first.kind,"witness_answer_1_1");
            g.save.x=archive.x;g.save.y=archive.y;
            Action(g,archive.kind,"witness_defend_1");
            var owned=g.enemies.FindAll(g.IsWitnessEnemy);
            Check(owned.Count==2,"Original forest witness defense registers two real live actors");
            var other=new Enemy(owned[0].id,owned[0].kind,
                owned[0].x,owned[0].y,65);
            g.enemies.Add(other);
            var hazard=new Hazard{position=g.Position,damage=5,
                radius=1f,timer=7f,kind="root",witnessOwned=true};
            var unrelated=new Hazard{position=g.Position,damage=3,
                radius=.8f,timer=7f,kind="environment"};
            g.hazards.Add(hazard);g.hazards.Add(unrelated);
            // A faulted transition can change the active map before the
            // source encounter is reset. Nothing from that encounter may
            // be carried forward, and the unrelated actor must survive.
            g.save.room=0;
            var reset=typeof(GameCore).GetMethod("ResetWitnessDefense",
                System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.NonPublic);
            Check(reset!=null,"Use the original mission teardown path");
            reset.Invoke(g,null);
            Check(g.WitnessDefenseRoom==0&&
                !g.enemies.Contains(owned[0])&&!g.enemies.Contains(owned[1])&&
                g.enemies.Contains(other)&&!g.hazards.Contains(hazard)&&
                g.hazards.Contains(unrelated),
                "Teardown after room change removes only genuinely registered guards and owned warnings, not a foreign same-ID actor");
            Check(g.save.version==2&&!g.Has("witness_defeated_1"),
                "Canceled evidence combat never changes persistent signed history or save format");
        }

        public static void SignedWitnessFieldObjectsNeverSpeakAsRemoteCitizens(Campaign data)
        {
            string[] npcSpeakers={"","密林巡路员 · 莫芽",
                "遗迹渡口幸存者 · 阿菱","赤铁矿工 · 维恩"};
            for(int region=1;region<=3;region++)
            {
                var g=Fresh(data);g.save.stage=region;
                g.save.flags.Add(region==1?"scout_saved":"gate_"+region);
                g.save.flags.Add("witness_started_"+region);
                g.save.flags.Add("witness_read_"+region);
                g.save.flags.Add("witness_sealed_"+region);
                g.save.flags.Add("witness_complete_"+region);
                g.Enter(region);g.enemies.Clear();
                WorldObject resident=Object(g,"witnessnpc"+region);
                WorldObject first=Object(g,"witnesssite1"+region);
                WorldObject archive=Object(g,"witnesssite2"+region);
                g.save.x=resident.x;g.save.y=resident.y;
                g.Talk(resident);
                Check(g.conversation!=null&&
                    g.conversation.speaker==npcSpeakers[region],
                    "Original completed living NPC retains genuine dialogue instead of an anonymous object speaker");
                g.Close();
                foreach(WorldObject site in new[]{first,archive})
                {
                    g.save.x=site.x;g.save.y=site.y;
                    g.Talk(site);
                    Check(g.conversation!=null&&
                        g.conversation.speaker==site.name&&
                        g.conversation.en==site.en&&
                        g.conversation.speaker!=npcSpeakers[region]&&
                        g.conversation.choices.Length==1&&
                        g.conversation.choices[0].action=="close",
                        "Reading a completed original scene keeps that archive's actual speaker, not a remote civilian NPC portrait");
                    Check(site==first?
                        g.conversation.textEn.Contains("Moya")||
                        g.conversation.textEn.Contains("Arin")||
                        g.conversation.textEn.Contains("Venn"):
                        g.conversation.textEn.Contains("archive")||
                        g.conversation.textEn.Contains("preserved"),
                        "Site-specific archive prose truthfully describes this original field evidence and its continuing civic effect");
                    g.Close();
                }
                Check(g.Has("witness_complete_"+region)&&
                    g.save.version==2,
                    "Revisiting physical witness sites cannot reset real signed progress or add save fields");
            }
        }

        public static void ActiveWitnessSecurityGuidesActualTimerAndOwnedGuardCount(Campaign data)
        {
            for(int region=1;region<=3;region++)
            {
                var g=Fresh(data);g.save.stage=region;
                g.save.flags.Add(region==1?"scout_saved":"gate_"+region);
                g.Enter(region);g.enemies.Clear();g.invulnerable=200;
                WorldObject npc=Object(g,"witnessnpc"+region);
                WorldObject first=Object(g,"witnesssite1"+region);
                WorldObject site=Object(g,"witnesssite2"+region);
                g.save.x=npc.x;g.save.y=npc.y;
                Action(g,npc.kind,"witness_begin_"+region);
                g.save.x=first.x;g.save.y=first.y;
                if(region==1)g.save.inventory[2]=Math.Max(1,g.save.inventory[2]);
                if(region==3)g.save.inventory[4]=Math.Max(1,g.save.inventory[4]);
                Action(g,first.kind,"witness_answer_"+region+"_"+(region==1?1:2));
                g.save.x=site.x;g.save.y=site.y;
                Action(g,site.kind,"witness_defend_"+region);
                Check(g.WitnessDefenseRoom==region,
                    "Real second witness site starts the authentic chapter security timer");
                var quest=g.GetQuest("campaign_witness");
                Check(quest.room==region&&
                    (quest.target-new Point(site.x,site.y)).Length<.01f&&
                    quest.next.Contains("守卫 2/2")&&quest.next.Contains("65秒"),
                    "In-progress witness mission sends hero to its real site with live 65s bilingual guidance");
                var hit=typeof(GameCore).GetMethod("Hit",
                    System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
                Check(hit!=null,"Actual lethal attack path is testable");
                Enemy firstGuard=g.enemies.Find(g.IsWitnessEnemy);
                hit.Invoke(g,new object[]{firstGuard,99999f,false,null,null});
                Check(g.GetQuest("campaign_witness").next.Contains("守卫 1/2"),
                    "Only actual owned guard death updates the real-time chapter objective");
                g.save.english=true;
                Check(g.GetQuest("campaign_witness").next.Contains("GUARDS 1/2")&&
                    g.GetQuest("campaign_witness").next.Contains("WITNESS DEFENSE"),
                    "English quest overlay agrees with the actual original combat roster");
                var secondGuard=g.enemies.Find(g.IsWitnessEnemy);
                hit.Invoke(g,new object[]{secondGuard,99999f,false,null,null});
                g.Tick(.05f,new Point());
                Check(g.WitnessDefenseRoom==0&&
                    !g.GetQuest("campaign_witness").next.Contains("WITNESS DEFENSE"),
                    "After true combat settlement the original quest guidance returns to physical evidence sealing");
            }
        }

        public static void ThreeRegionWitnessDefenseRejectsForeignMatchingIds(Campaign data)
        {
            var hit=typeof(GameCore).GetMethod("Hit",
                System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.NonPublic);
            Check(hit!=null,"Witness battles use real lethal combat contacts");
            for(int region=1;region<=3;region++)
            {
                var g=Fresh(data);g.save.stage=region;g.invulnerable=200;
                g.save.flags.Add(region==1?"scout_saved":"gate_"+region);
                g.Enter(region);g.enemies.Clear();
                var npc=Object(g,"witnessnpc"+region);
                var source=Object(g,"witnesssite1"+region);
                var archive=Object(g,"witnesssite2"+region);
                g.save.x=npc.x;g.save.y=npc.y;
                Action(g,npc.kind,"witness_begin_"+region);
                g.save.x=source.x;g.save.y=source.y;
                if(region==1)g.save.inventory[2]=Math.Max(1,g.save.inventory[2]);
                if(region==3)g.save.inventory[4]=Math.Max(1,g.save.inventory[4]);
                Action(g,source.kind,"witness_answer_"+region+"_"+(region==1?1:2));
                g.save.x=archive.x;g.save.y=archive.y;
                Action(g,archive.kind,"witness_defend_"+region);
                Check(g.WitnessDefenseRoom==region&&
                    g.enemies.FindAll(g.IsWitnessEnemy).Count==2,
                    "Each original real regional testimony places exactly two true guardian instances");
                var genuine=g.enemies.FindAll(g.IsWitnessEnemy);
                Enemy forged=new Enemy(genuine[0].id,genuine[0].kind,
                    genuine[0].x,genuine[0].y,30){stun=150f,timer=150f};
                g.enemies.Add(forged);
                Check(!g.IsWitnessEnemy(forged)&&
                    g.enemies.FindAll(g.IsWitnessEnemy).Count==2,
                    "An attacker with an equal 89972-series ID cannot acquire signed witness ownership");
                int coins=g.save.coins,shards=g.save.shards;
                hit.Invoke(g,new object[]{forged,99999f,false,null,null});
                // Foreign live enemies keep their ordinary 6-coin loot.
                // Only mission-owned enemies are excluded from normal
                // combat economy and may record witness guard deaths.
                int afterOrdinaryKill=g.save.coins;
                Check(afterOrdinaryKill==coins+6,
                    "The forged-ID world enemy is an ordinary foe and still pays its normal unrelated combat reward");
                Check(g.WitnessDefenseRoom==region&&
                    g.enemies.FindAll(g.IsWitnessEnemy).Count==2&&
                    !g.Has("witness_defeated_"+region),
                    "A counterfeit enemy's genuine death never contributes to an original guard seal");
                // A replacement with a matching ID cannot conceal a real
                // defender removed without a legitimate damage callback.
                g.enemies.Remove(genuine[0]);
                Enemy replacement=new Enemy(genuine[0].id,genuine[0].kind,
                    genuine[0].x,genuine[0].y,40){stun=150f};
                g.enemies.Add(replacement);
                g.Tick(.05f,new Point());
                Check(g.WitnessDefenseRoom==0&&
                    !g.Has("witness_defeated_"+region)&&
                    !g.Has("witness_sealed_"+region)&&
                    g.enemies.Contains(replacement)&&
                    !g.enemies.Contains(genuine[1])&&
                    g.save.coins==afterOrdinaryKill&&g.save.shards==shards,
                    "Real guardian disappearance resets only its owned fight, without deleting an unrelated same-ID enemy or minting evidence");
                g.enemies.Clear();
                g.save.x=archive.x;g.save.y=archive.y;
                Action(g,archive.kind,"witness_defend_"+region);
                Check(g.WitnessDefenseRoom==region&&
                    g.enemies.FindAll(g.IsWitnessEnemy).Count==2,
                    "Free retry after source-authenticated cancellation restores the actual two-actor fight");
                foreach(Enemy e in g.enemies.FindAll(g.IsWitnessEnemy))
                    hit.Invoke(g,new object[]{e,99999f,false,null,null});
                g.Tick(.05f,new Point());
                Check(g.WitnessDefenseRoom==0&&
                    g.Has("witness_defeated_"+region)&&
                    !g.Has("witness_complete_"+region),
                    "Exactly two real damage-confirmed kills permit authentic evidence sealing, not remote reward");
                Action(g,archive.kind,"witness_seal_"+region);
                g.save.x=npc.x;g.save.y=npc.y;
                Action(g,npc.kind,"witness_finish_"+region);
                Check(g.Has("witness_complete_"+region)&&
                    g.save.coins==afterOrdinaryKill+region*10+15&&
                    g.save.shards==shards+1&&g.save.version==2,
                    "Original living regional witness still signs one irreversible story reward under unchanged v2 save");
            }
        }

        public static void UnconfirmedWitnessGuardDisappearanceCannotForgeLivingTestimony(Campaign data)
        {
            var g = Fresh(data);
            g.save.stage = 0;
            g.save.flags.Add("scout_saved");
            g.Enter(1); g.enemies.Clear(); g.invulnerable = 100;
            var witness = Object(g, "witnessnpc1");
            var source = Object(g, "witnesssite11");
            var archive = Object(g, "witnesssite21");
            g.save.x = witness.x; g.save.y = witness.y;
            Action(g, witness.kind, "witness_begin_1");
            g.save.x = source.x; g.save.y = source.y;
            g.save.inventory[2] = Math.Max(1, g.save.inventory[2]);
            Action(g, source.kind, "witness_answer_1_1");
            g.save.x = archive.x; g.save.y = archive.y;
            Action(g, archive.kind, "witness_defend_1");
            Check(g.WitnessDefenseRoom == 1 &&
                g.enemies.FindAll(g.IsWitnessEnemy).Count == 2,
                "A signed testimony requires the original live two-guard security encounter");
            int coins = g.save.coins, shards = g.save.shards;
            // A rogue cleanup is not a player kill. The pending record
            // must not be marked secured merely because its guards vanished.
            g.enemies.RemoveAll(g.IsWitnessEnemy);
            g.Tick(.05f, new Point());
            Check(g.WitnessDefenseRoom == 0 &&
                !g.Has("witness_defeated_1") &&
                !g.Has("witness_sealed_1") &&
                !g.Has("witness_complete_1") &&
                g.save.coins == coins && g.save.shards == shards,
                "An unconfirmed enemy-list disappearance resets the actual witness defense without forging evidence or rewards");
            g.Talk(archive);
            Check(g.conversation != null &&
                Array.Exists(g.conversation.choices,
                    c => c.action == "witness_defend_1") &&
                !Array.Exists(g.conversation.choices,
                    c => c.action == "witness_seal_1"),
                "The existing original field marker offers a free retry instead of a counterfeit sealed record");
            g.Close();
            Action(g, archive.kind, "witness_defend_1");
            Check(g.WitnessDefenseRoom == 1 &&
                g.enemies.FindAll(g.IsWitnessEnemy).Count == 2,
                "A fresh legitimate battle remains playable with both authored security enemies");
            var hit = typeof(GameCore).GetMethod("Hit",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);
            Check(hit != null, "Witness defeat uses the original live combat damage path");
            foreach (Enemy guard in g.enemies.FindAll(g.IsWitnessEnemy))
                hit.Invoke(g, new object[] { guard, 99999f, false, null, null });
            g.Tick(.05f, new Point());
            Check(g.WitnessDefenseRoom == 0 && g.Has("witness_defeated_1") &&
                g.save.coins == coins && g.save.shards == shards,
                "Both authentic lethal contacts earn the security unlock without paying before signature");
            Action(g, archive.kind, "witness_seal_1");
            g.save.x = witness.x; g.save.y = witness.y;
            Action(g, witness.kind, "witness_finish_1");
            Check(g.Has("witness_sealed_1") &&
                g.Has("witness_complete_1") &&
                g.save.coins == coins + 25,
                "Verified defense still yields the original genuine signed first-chapter testimony and once-only reward");
            g.PrepareSave();
            var loaded = new GameCore(data, CloneSave(g.save));
            Check(loaded.Has("witness_complete_1") &&
                loaded.save.coins == g.save.coins,
                "Signed and verified civilian testimony survives unchanged v2 save reload");
        }

    }
}
