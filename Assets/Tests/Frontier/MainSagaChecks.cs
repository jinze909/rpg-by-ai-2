using System;
using System.Reflection;
namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        public static void MainSagaRequiresRealEvidenceWitnessAndCouncilBeforeFinale(Campaign data)
        {
            GameCore g=Fresh(data);
            g.mode="world";g.save.stage=3;g.save.room=0;
            WorldObject guild=Array.Find(g.Map.objects,o=>o.kind=="guild");
            Check(guild!=null&&PlaceWithinTalkReach(g,guild),
                "Original Lyra guild counter is physically accessible");
            g.Talk(guild);
            int start=Array.FindIndex(g.conversation.choices,c=>c.action=="saga_begin");
            Check(start>=0,"Veteran chapter-four saves can enter the expanded story");
            g.Choose(start);
            Check(g.MainSagaActive&&g.save.version==2&&
                g.save.trackedQuest=="main",
                "Second act is durable v2 data and becomes the tracked story");
            g.Close();
            g.save.room=4;g.save.flags.Add("boss_4");g.mode="world";
            WorldObject shrine=Array.Find(g.Map.objects,o=>o.kind=="shrine");
            Check(shrine!=null&&PlaceWithinTalkReach(g,shrine),
                "Original Citadel confrontation remains a physical stage");
            g.Talk(shrine);
            Check(g.conversation!=null&&
                Array.FindIndex(g.conversation.choices,c=>c.action=="ending1")<0,
                "An extended story cannot end at Chapter Four");
            for(int room=5;room<=7;room++)
            {
                g.Close();g.save.room=room;g.mode="world";g.enemies.Clear();
                g.save.flags.Add(room==5?"harbor_report":
                    room==6?"desert_report":"mountain_report");
                WorldObject npc=Array.Find(g.Map.objects,o=>o.id==
                    (room==5?"mira":room==6?"ivo":"sen"));
                Check(npc!=null&&PlaceWithinTalkReach(g,npc),
                    "Existing regional witnesses are playable characters");
                g.Talk(npc);
                int account=Array.FindIndex(g.conversation.choices,
                    c=>c.action=="saga_account_"+room);
                Check(account>=0,"True regional report unlocks a story interview");
                g.Choose(account);
                Check(!g.save.flags.Contains("saga_voice_"+room),
                    "Talking without inspecting the scene does not forge testimony");
                g.Close();
                WorldObject evidence=Array.Find(g.Map.objects,o=>o.id==
                    (room==5?"passengers":room==6?"receiver":"bell2"));
                Check(evidence!=null&&PlaceWithinTalkReach(g,evidence),
                    "An original on-map evidence object is physically accessible");
                g.Talk(evidence);
                Check(g.save.flags.Contains("saga_trace_"+room)&&
                    g.conversation!=null,
                    "Only interacting at the real evidence object earns the trace");
                g.Close();
                WorldObject outer=Array.Find(g.Map.objects,o=>o.id==
                    "outer_"+room+"_0");
                Check(outer!=null&&
                    (new Point(outer.x,outer.y)-new Point(evidence.x,evidence.y)).Length>25f&&
                    g.NavigationPathFrom(new Point(evidence.x,evidence.y),
                        new Point(outer.x,outer.y),true).Count>0,
                    "The second testimony is a distinct, genuinely walkable distant site");
                Check(PlaceWithinTalkReach(g,outer),
                    "Original farshore journal can be physically reached");
                g.Talk(outer);
                Check(!g.save.flags.Contains("saga_outer_"+room)&&
                    g.conversation!=null,
                    "Reading an eyewitness account cannot silently complete a human rescue");
                int help=Array.FindIndex(g.conversation.choices,
                    c=>c.action=="saga_field_aid_"+room);
                int preserve=Array.FindIndex(g.conversation.choices,
                    c=>c.action=="saga_field_record_"+room);
                Check(help>=0&&preserve>=0,
                    "The second physical evidence site offers two meaningful responses");
                int material=room==5?0:room==6?4:2;
                g.save.inventory[material]=2;
                g.Choose(help);
                Check(g.save.flags.Contains("saga_outer_"+room)&&
                    g.save.flags.Contains("saga_field_aid_done_"+room)&&
                    g.save.inventory[material]==1,
                    "The actual live aid choice spends correct real inventory and retains both accounts");
                g.Close();
                Check(PlaceWithinTalkReach(g,npc),"Return to the genuine witness");
                g.Talk(npc);
                account=Array.FindIndex(g.conversation.choices,
                    c=>c.action=="saga_account_"+room);
                Check(account>=0,"The live speaker offers to finish their account");
                g.Choose(account);
                Check(Array.FindIndex(g.conversation.choices,
                    c=>c.action=="saga_sign_"+room+"_a")<0,
                    "Unsigned evidence cannot skip a physical two-wave witness defense");
                int begin=Array.FindIndex(g.conversation.choices,
                    c=>c.action=="saga_guard_begin");
                Check(begin>=0,"Real witness offers a playable defense before signing");
                g.Choose(begin);
                Check(g.SagaGuardActive&&g.SagaGuardWave==1&&
                    g.SagaGuardRemaining==2,
                    "First defender wave exists as genuine game enemies");
                Check(PlaceWithinTalkReach(g,npc),"The original speaker can operate the local defense");
                g.Talk(npc);
                account=Array.FindIndex(g.conversation.choices,
                    c=>c.action=="saga_account_"+room);
                Check(account>=0,"Actual NPC remains available during the encounter");
                g.Choose(account);
                int device=Array.FindIndex(g.conversation.choices,
                    c=>c.action=="saga_guard_device");
                Check(device>=0&&g.SagaGuardDeviceReady,
                    "Each region exposes its own physically operated combat mechanism");
                g.save.inventory[0]=5;g.save.inventory[2]=5;g.save.energy=100f;
                int woodBefore=g.save.inventory[0],herbBefore=g.save.inventory[2];
                float staminaBefore=g.save.energy;
                g.Choose(device);
                Check(g.SagaGuardDeviceUsed&&!g.SagaGuardDeviceReady&&
                    g.save.inventory[0]==woodBefore-(room==5?1:0)&&
                    g.save.inventory[2]==herbBefore-(room==7?1:0)&&
                    (room!=6||g.save.energy==staminaBefore-18f),
                    "Real regional mechanism consumes the correct resources exactly once");
                var hit=typeof(GameCore).GetMethod("Hit",
                    BindingFlags.NonPublic|BindingFlags.Instance);
                Check(hit!=null,"Combat must use production damage and defeat hooks");
                for(int wave=1;wave<=2;wave++)
                {
                    Enemy[] attackers=g.enemies.ToArray();
                    Check(attackers.Length==2,"Each distinct defensive wave has two live foes");
                    foreach(Enemy foe in attackers)
                    {
                        Check(g.CanStand(foe.Position)&&
                            g.NavigationPathFrom(g.Position,foe.Position,true).Count>0,
                            "Every attacker approaches through a real traversable original map");
                        hit.Invoke(g,new object[]{foe,9999f,true,null,null});
                    }
                    // Production contact applies real hit-stop. A single
                    // 20ms tick may intentionally freeze all world missions;
                    // simulate enough actual frames to release that impact
                    // and let the second wave enter genuine world time.
                    Advance(g,.65f);
                    if(wave==1)
                        Check(g.SagaGuardActive&&g.SagaGuardWave==2&&
                            g.SagaGuardRemaining==2&&!g.Has("saga_guard_"+room),
                            "First real cleared wave triggers a separate second combat phase in region "+
                                room+": active="+g.SagaGuardActive+
                                " wave="+g.SagaGuardWave+
                                " left="+g.SagaGuardRemaining+
                                " toast="+g.toast);
                }
                Check(!g.SagaGuardActive&&g.Has("saga_guard_"+room)&&
                    !g.Has("saga_voice_"+room),
                    "Only defeating all four actual attackers secures the still unsigned original");
                Check(PlaceWithinTalkReach(g,npc),"Actual regional speaker remains reachable");
                g.Talk(npc);
                account=Array.FindIndex(g.conversation.choices,
                    c=>c.action=="saga_account_"+room);
                Check(account>=0,"The genuine witness reopens their moral decision");
                g.Choose(account);
                int sign=Array.FindIndex(g.conversation.choices,
                    c=>c.action=="saga_sign_"+room+"_a");
                Check(sign>=0,"Secured evidence unlocks the authored moral decision");
                g.Choose(sign);
                Check(g.save.flags.Contains("saga_voice_"+room)&&
                    g.save.flags.Contains("saga_choice_"+room+"_a"),
                    "An original, inspected scene and live speaker sign the choice");
            }
            Check(g.MainSagaVoices==3&&g.MainSagaTraces==3&&
                !g.MainSagaCanConclude,
                "Regional witness scenes alone cannot bypass the council");
            // Complete the actual Mistport council conversation, not a
            // synthetic save.frontierEnding assignment.
            g.Close();g.save.room=5;g.mode="world";g.enemies.Clear();
            WorldObject council=Array.Find(g.Map.objects,o=>o.kind=="council");
            Check(council!=null&&PlaceWithinTalkReach(g,council),
                "The existing Mistport council is physically accessible");
            g.Talk(council);
            Check(g.conversation!=null&&
                g.conversation.textEn.Contains("Mira speaks first")&&
                g.conversation.textEn.Contains("Ivo answers")&&
                g.conversation.textEn.Contains("Sen sets down"),
                "Live council hearing debates all three genuine first-hand choices");
            string publicDebate=g.conversation.textEn;
            g.save.flags.Remove("saga_choice_5_a");
            g.save.flags.Add("saga_choice_5_b");
            g.Talk(council);
            Check(g.conversation.textEn!=publicDebate&&
                g.conversation.textEn.Contains("families read the original manifest"),
                "Same real council speaker responds differently to the protected original record");
            g.save.flags.Remove("saga_choice_5_b");
            g.save.flags.Add("saga_choice_5_a");
            g.Talk(council);
            int covenant=Array.FindIndex(g.conversation.choices,
                c=>c.action=="covenant1");
            Check(covenant>=0,"Original three-party council can sign after all voices");
            g.Choose(covenant);
            Check(g.save.frontierEnding==1&&g.MainSagaCouncilReady&&
                !g.MainSagaCanConclude&&g.conversation!=null&&
                g.conversation.speaker.Contains("第二幕"),
                "The covenant opens a playable citadel finale, not instant credits");
            Check(g.conversation.textEn.Contains("The shared water order reserves"),
                "Ratified council policy changes the physical supply agreement");
            g.Close();g.save.room=4;g.mode="world";g.enemies.Clear();
            string[] gateActions={"saga_lastgate_1","saga_lastgate_2",
                "saga_lastgate_public"};
            for(int step=1;step<=3;step++)
            {
                WorldObject station=Array.Find(g.Map.objects,
                    o=>o.id=="triadmemory"+step);
                Check(station!=null&&PlaceWithinTalkReach(g,station),
                    "Live original citadel evidence station is physically reachable");
                g.Talk(station);
                int choice=Array.FindIndex(g.conversation.choices,
                    c=>c.action==gateActions[step-1]);
                Check(choice>=0,"Each distinct citadel station has a live operation");
                if(step==2)g.save.energy=100f;
                float energy=g.save.energy;
                g.Choose(choice);
                Check(g.MainSagaLastGateStep==step+1,
                    "Only the correct physical station advances the last chapter");
                if(step==2)Check(g.save.energy==energy-22f,
                    "Reconnecting the real circuit spends the advertised stamina");
                g.Close();
            }
            Check(g.MainSagaCanConclude&&
                g.save.flags.Contains("saga_lastgate_open"),
                "The civic testimony and real three-stage citadel route unlock the final choice");
            Check(PlaceWithinTalkReach(g,shrine),
                "Return to the physical original citadel shrine");
            g.Talk(shrine);
            int ending=Array.FindIndex(g.conversation.choices,
                c=>c.action=="ending1");
            Check(ending>=0&&g.conversation.text.Contains("调度印"),
                "Actual Orrin finale opens with the older supply-order revelation");
            g.Choose(ending);
            Check(g.save.ending==1&&g.Has("completed")&&
                g.conversation.text.Contains("米拉"),
                "The expanded main ends through a real player choice and remembers witnesses");
            g.PrepareSave();
            GameCore restored=new GameCore(data,CloneSave(g.save));
            Check(restored.MainSagaActive&&restored.MainSagaVoices==3&&
                restored.MainSagaTraces==3&&restored.save.version==2,
                "All authored story choices survive an ordinary v2 save round-trip");
        }
        public static void EarlyRescueTacticsPayOffWithRealLaterWitnessDefenseSupport(Campaign data)
        {
            for(int room=5;room<=7;room++)
            {
                GameCore g=Fresh(data);g.mode="world";g.save.stage=3;
                g.save.room=room;g.enemies.Clear();
                g.save.flags.Add("saga_act_two");g.save.flags.Add("boss_4");
                g.save.flags.Add(room==5?"harbor_report":
                    room==6?"desert_report":"mountain_report");
                g.save.flags.Add("saga_trace_"+room);
                g.save.flags.Add("saga_outer_"+room);
                g.save.flags.Add("chapter_road_diverted_"+(room-3));
                WorldObject host=Array.Find(g.Map.objects,o=>o.id==
                    (room==5?"mira":room==6?"ivo":"sen"));
                Check(host!=null&&PlaceWithinTalkReach(g,host),
                    "The live named regional witness is physically reachable");
                g.Talk(host);
                int account=Array.FindIndex(g.conversation.choices,
                    c=>c.action=="saga_account_"+room);
                Check(account>=0,"Original witness interview is preserved");
                g.Choose(account);
                Check(g.conversation.textEn.Contains(room==5?"empty raft":
                    room==6?"mine apprentice":"refugee cart driver"),
                    "Later character remembers the early chapter's specific costly diversion");
                int defend=Array.FindIndex(g.conversation.choices,
                    c=>c.action=="saga_guard_begin");
                Check(defend>=0,"Earlier rescue cannot skip real two-wave defense");
                g.Choose(defend);
                float clock=room==5?200f:room==6?210f:225f;
                Check(g.SagaGuardActive&&g.SagaGuardClock>=clock-.1f,
                    "Saved civilian support grants a genuine 25-second defense advantage");
                g.PrepareSave();
                GameCore restored=new GameCore(data,CloneSave(g.save));
                Check(restored.save.version==2&&
                    restored.Has("chapter_road_diverted_"+(room-3)),
                    "Cross-chapter consequences survive old-format v2 save roundtrip");
            }
        }
        public static void FieldEvidencePreservationIsARealNonPurchaseAlternative(Campaign data)
        {
            GameCore g=Fresh(data);g.mode="world";g.save.stage=3;
            g.save.room=5;g.enemies.Clear();
            g.save.flags.Add("saga_act_two");g.save.flags.Add("boss_4");
            g.save.flags.Add("harbor_report");g.save.flags.Add("saga_trace_5");
            WorldObject log=Array.Find(g.Map.objects,o=>o.id=="outer_5_0");
            Check(log!=null&&PlaceWithinTalkReach(g,log),
                "Real remote wreck journal is an accessible world object");
            g.save.inventory[0]=0;
            g.Talk(log);
            int aid=Array.FindIndex(g.conversation.choices,
                c=>c.action=="saga_field_aid_5");
            Check(aid>=0,"Live rescue choice exists");
            g.Choose(aid);
            Check(!g.Has("saga_outer_5")&&g.save.inventory[0]==0,
                "No timber cannot fabricate a completed rescue");
            g.Close();Check(PlaceWithinTalkReach(g,log));g.Talk(log);
            int record=Array.FindIndex(g.conversation.choices,
                c=>c.action=="saga_field_record_5");
            Check(record>=0,"Securing originals is an equally valid story response");
            g.Choose(record);
            Check(g.Has("saga_outer_5")&&g.Has("saga_field_record_done_5")&&
                !g.Has("saga_field_aid_done_5")&&g.save.inventory[0]==0,
                "Physical preservation completes evidence without consuming fictional supplies");
            g.Close();
            WorldObject mira=Array.Find(g.Map.objects,o=>o.id=="mira");
            Check(mira!=null&&PlaceWithinTalkReach(g,mira),
                "Return to Mira's original physical NPC position");
            g.Talk(mira);
            int account=Array.FindIndex(g.conversation.choices,
                c=>c.action=="saga_account_5");
            Check(account>=0,"Original witness dialogue remains available");
            g.Choose(account);
            Check(g.conversation.textEn.Contains("gathering wood"),
                "Mira responds to the real repair that remains unfinished");
            g.PrepareSave();
            GameCore restored=new GameCore(data,CloneSave(g.save));
            Check(restored.save.version==2&&restored.Has("saga_field_record_done_5"),
                "Persistent field response requires no SaveData schema migration");
        }
        public static void ThirdCoreDeliveryAutomaticallyEntersExtendedMainStory(Campaign data)
        {
            GameCore g=Fresh(data);g.mode="world";g.save.room=0;
            g.save.stage=2;g.save.flags.Add("core_3");
            WorldObject guild=Array.Find(g.Map.objects,o=>o.kind=="guild");
            Check(guild!=null&&PlaceWithinTalkReach(g,guild),
                "Third core must be delivered to the actual living guild NPC");
            g.Talk(guild);
            int deliver=Array.FindIndex(g.conversation.choices,
                c=>c.action=="deliver");
            Check(deliver>=0,"Original chapter-three guild scene offers a real core handoff");
            g.Choose(deliver);
            Check(g.save.stage==3&&g.MainSagaActive&&
                g.save.trackedQuest=="main"&&g.save.version==2,
                "Third original starcore automatically starts expanded main story");
            Check(g.GetQuest("main").category.Contains("第二幕")||
                g.GetQuest("main").category.Contains("ACT TWO"),
                "Real main journal, not a detached afterstory, tracks Act Two");
        }
        public static void ExpandedSagaChronicleRevealsOnlyRealReachedActs(Campaign data)
        {
            GameCore g=Fresh(data);g.save.stage=3;
            g.save.flags.Add("saga_act_two");
            Check(g.StoryRecap().Count==4,
                "An unfinished citadel battle does not spoil the remote story");
            g.save.flags.Add("boss_4");
            Check(g.StoryRecap().Count==5&&
                g.StoryRecap()[4].chapter==5&&
                g.StoryRecap()[4].questId=="main",
                "True citadel transition opens Chapter Five on the real main tracker");
            g.save.flags.Add("saga_voice_5");
            Check(g.StoryRecap().Count==6&&
                g.StoryRecap()[5].chapter==6,
                "The first signed remote witness unlocks the desert chapter");
            g.save.flags.Add("saga_voice_6");
            Check(g.StoryRecap().Count==7&&
                g.StoryRecap()[6].chapter==7,
                "The second signed remote witness unlocks Frostchime");
            g.save.flags.Add("saga_voice_7");
            Check(g.StoryRecap().Count==8&&
                g.StoryRecap()[7].chapter==8,
                "All three witnesses unlock the genuine council chapter");
            g.save.frontierEnding=1;g.save.ending=2;
            g.save.flags.Add("afterlight_started");
            var completed=g.StoryRecap();
            Check(completed[7].complete&&
                completed[7].chapter==8&&
                !completed.Exists(beat=>beat.chapter==5&&
                    beat.questId=="afterlight"),
                "Extended main chapters remain historical and postcredits work cannot impersonate them");
        }
        public static void EndingChoicesBecomeRealLocalResidentServices(Campaign data)
        {
            var afterword=typeof(GameCore).GetMethod("TickMainSagaAfterword",
                BindingFlags.NonPublic|BindingFlags.Instance);
            Check(afterword!=null,"Postgame resident service is wired into real gameplay");
            for(int room=5;room<=7;room++)
            for(int route=0;route<=1;route++)
            {
                GameCore g=Fresh(data);
                g.mode="world";g.save.stage=3;g.save.room=room;
                g.enemies.Clear();g.save.ending=1;
                g.save.flags.Add("saga_act_two");
                g.save.flags.Add("saga_voice_"+room);
                g.save.flags.Add("saga_choice_"+room+"_"+(route==0?"a":"b"));
                WorldObject npc=Array.Find(g.Map.objects,
                    o=>o.id==(room==5?"mira":room==6?"ivo":"sen"));
                Check(npc!=null&&PlaceWithinTalkReach(g,npc),
                    "Postgame continues at the genuine original regional NPC");
                g.Talk(npc);
                Check(g.conversation!=null,
                    "The existing mature NPC conversation remains available");
                int after=Array.FindIndex(g.conversation.choices,
                    c=>c.action=="saga_afterword_"+room);
                Check(after>=0,"Optional postgame dialogue is appended, not substituted");
                g.Choose(after);
                Check(g.mode=="dialog"&&g.conversation!=null&&
                    g.conversation.speaker==npc.name&&
                    g.conversation.textEn.Length>120,
                    "The actual regional witness delivers a substantive personal epilogue");
                g.Close();
                Check(PlaceWithinTalkReach(g,npc),
                    "Local support requires physically standing beside its speaker");
                g.save.health=40f;g.save.energy=40f;
                g.save.heat=50f;g.save.cold=50f;
                float oldHealth=g.save.health,oldEnergy=g.save.energy;
                float oldHeat=g.save.heat,oldCold=g.save.cold;
                afterword.Invoke(g,new object[]{1f});
                if(room==5)
                    Check(g.save.health>oldHealth&&g.save.energy>oldEnergy,
                        "Mistport's signed rescue story maintains a genuine field-aid station");
                if(room==6)
                    Check(g.save.heat<oldHeat&&g.save.energy>oldEnergy,
                        "The desert's chosen water policy operates a real cooling station");
                if(room==7)
                    Check(g.save.cold<oldCold&&g.save.health>oldHealth,
                        "Frostchime keeps a genuine warm haven for travelers");
                g.save.health=40f;g.save.energy=40f;
                g.save.heat=50f;g.save.cold=50f;
                g.save.ending=0;
                afterword.Invoke(g,new object[]{1f});
                Check(g.save.health==40f&&g.save.energy==40f&&
                    g.save.heat==50f&&g.save.cold==50f,
                    "Pre-finale saves cannot claim postgame benefits");
                g.save.ending=1;g.save.x=70f;g.save.y=70f;
                afterword.Invoke(g,new object[]{1f});
                Check(g.save.health==40f&&g.save.energy==40f&&
                    g.save.heat==50f&&g.save.cold==50f,
                    "A remote or unreachable NPC cannot heal an absent player");
                GameCore legacy=Fresh(data);
                legacy.mode="world";legacy.save.room=room;
                legacy.save.ending=2;legacy.enemies.Clear();
                Check(PlaceWithinTalkReach(legacy,npc),
                    "A legacy-ending character can visit the original NPC");
                legacy.Talk(npc);
                Check(legacy.conversation!=null&&
                    Array.FindIndex(legacy.conversation.choices,
                        c=>c.action=="saga_afterword_"+room)<0,
                    "Legacy four-chapter endings gain no invented second-act testimony");
            }
        }
        public static void CompletedLegacyFinaleRemainsCompletedWithoutActTwo(Campaign data)
        {
            GameCore g=Fresh(data);
            g.save.stage=3;g.save.room=4;g.save.flags.Add("boss_4");
            g.save.ending=2;g.save.flags.Add("completed");g.mode="world";
            Check(!g.MainSagaActive&&g.save.ending==2&&
                g.GetQuest("main").complete,
                "Existing four-chapter ending is not reopened by new content");
        }
    }
}
