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
                Check(PlaceWithinTalkReach(g,npc),"Return to the genuine witness");
                g.Talk(npc);
                account=Array.FindIndex(g.conversation.choices,
                    c=>c.action=="saga_account_"+room);
                Check(account>=0,"The live speaker offers to finish their account");
                g.Choose(account);
                int sign=Array.FindIndex(g.conversation.choices,
                    c=>c.action=="saga_sign_"+room+"_a");
                Check(sign>=0,"The interview reveals an authored moral decision");
                g.Choose(sign);
                Check(g.save.flags.Contains("saga_voice_"+room)&&
                    g.save.flags.Contains("saga_choice_"+room+"_a"),
                    "An original, inspected scene and live speaker sign the choice");
            }
            Check(g.MainSagaVoices==3&&g.MainSagaTraces==3&&
                !g.MainSagaCanConclude,
                "Regional witness scenes alone cannot bypass the council");
            g.save.frontierEnding=1;
            Check(g.MainSagaCanConclude,
                "The real three-party covenant completes the campaign preconditions");
            g.PrepareSave();
            GameCore restored=new GameCore(data,CloneSave(g.save));
            Check(restored.MainSagaActive&&restored.MainSagaVoices==3&&
                restored.MainSagaTraces==3&&restored.save.version==2,
                "All authored story choices survive an ordinary v2 save round-trip");
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
