using System;
namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        public static void ChapterSevenHearingRequiresThreeIndependentLivingVotesAndActualCouncil(Campaign data)
        {
            var g=Fresh(data);g.save.stage=3;g.save.ending=3;
            WorldObject dais=Object(g,"hearingdais");
            g.save.x=dais.x;g.save.y=dais.y;
            g.Talk(dais);
            Check(g.conversation.text.Contains("莉雅") &&
                g.conversation.textEn.Contains("Lyra") &&
                !g.conversation.text.Contains("第五章") &&
                !g.conversation.textEn.Contains("Chapter Five"),
                "Prerequisites are narrated through an in-world note rather than a quest checklist");
            Check(!Array.Exists(g.conversation.choices,c=>c.action=="hearing_begin"),
                "The post-storm public hearing cannot begin on an unfinished campaign or before Orrin's accord");
            g.Close();
            foreach(string f in new[]{"afterlight_complete","afterlight_started","storm_started",
                "storm_complete","storm_orrin_open","scout_saved","gate_2","gate_3",
                "storm_region_1_shelter","storm_region_2_shelter","storm_region_3_shelter"})
                g.save.flags.Add(f);
            Action(g,dais.kind,"hearing_begin");
            Check(g.Has("hearing_started") && g.CurrentGuide.id=="hearing" &&
                !g.HearingComplete && g.HearingSignatures==0 &&
                g.GetQuest("main").complete,
                "Chapter Seven is a genuine tracked continuation after previous actual main story rather than a replacement");
            for(int region=1;region<=3;region++)
            {
                g.Enter(region);g.Close();g.enemies.Clear();g.invulnerable=150;
                WorldObject witness=Object(g,"witnessnpc"+region);
                WorldObject plaque=Object(g,"hearingplaque"+region);
                Check(g.CanStand(new Point(plaque.x,plaque.y)) &&
                    !g.IsObjectPresent(plaque),
                    "All authored civic oath monuments are on walkable maps but hidden before authentic hearing resolution");
                Point outsideWitness=StandableOutsideTalkReach(g,witness);
                g.save.x=outsideWitness.x;g.save.y=outsideWitness.y;
                g.Talk(witness);
                Check(g.mode!="dialog",
                    "A direct Talk call cannot open a resident vote outside current physical reach");
                g.save.x=witness.x;g.save.y=witness.y;
                g.Talk(witness);
                int stale=Array.FindIndex(g.conversation.choices,
                    c=>c.action=="hearing_voice_"+region+"_care");
                Check(stale>=0,"The resident presents a genuine choice while physically present");
                Check(!g.conversation.text.Contains("游戏世界") &&
                    !g.conversation.textEn.Contains("game world") &&
                    g.conversation.text.Length>35 && g.conversation.textEn.Length>35,
                    "Regional witnesses deliver concrete bilingual testimony without metagame narration");
                g.save.x=outsideWitness.x;g.save.y=outsideWitness.y;
                g.Choose(stale);g.Close();
                Check(g.HearingVoice(region)==0,
                    "A stale hearing dialog cannot remotely forge an original resident's independent vote");
                g.save.x=witness.x;g.save.y=witness.y;
                string decision="hearing_voice_"+region+(region==2?"_watch":"_care");
                Action(g,witness.kind,decision);
                Check(g.HearingVoice(region)==(region==2?2:1) &&
                    g.HearingSignatures==region,
                    "A named regional resident personally signs the exact chosen long-term policy");
                g.Talk(witness);
                Check(!Array.Exists(g.conversation.choices,c=>
                    c.action=="hearing_voice_"+region+"_care") &&
                    !Array.Exists(g.conversation.choices,c=>
                    c.action=="hearing_voice_"+region+"_watch"),
                    "The hearing does not allow overwriting an already publicly signed vote");
                Check(g.conversation!=null && g.conversation.choices.Length>0,
                    "The original resident's earlier testimony or storm relief dialogue remains accessible");
                int recall=Array.FindIndex(g.conversation.choices,c=>c.action=="hearing_witness_storm");
                Check(recall>=0 && g.conversation.text.Contains(new[]{"","莫芽","阿菱","维恩"}[region]),
                    "Signed resident responds to the policy with new region-specific dialogue");
                g.Choose(recall);
                Check(g.mode=="dialog" && Array.Exists(g.conversation.choices,c=>c.action=="storm_oldstory_"+region),
                    "Signed resident retains guarded access to the original storm testimony");
                g.Close();
            }
            g.Enter(0);g.enemies.Clear();
            g.save.x=dais.x;g.save.y=dais.y;
            int coins=g.save.coins,shards=g.save.shards,ending=g.save.ending;
            g.Talk(dais);
            int staleVerdict=Array.FindIndex(g.conversation.choices,
                c=>c.action=="hearing_verdict_public");
            Check(g.conversation.text.Contains("莉雅") &&
                g.conversation.textEn.Contains("Lyra") &&
                !g.conversation.text.Contains("不是谁对谁错") &&
                !g.conversation.textEn.Contains("trick answer") &&
                g.conversation.text.Contains("医护") && g.conversation.text.Contains("巡路"),
                "Final hearing speaks of competing practical needs instead of grading player choices");
            Check(staleVerdict>=0 &&
                Array.Exists(g.conversation.choices,c=>c.action=="hearing_verdict_reserve"),
                "Actual council presents two distinct civic tradeoffs only after all three independent signatures");
            Point outsideDais=StandableOutsideTalkReach(g,dais);
            g.save.x=outsideDais.x;g.save.y=outsideDais.y;
            g.Choose(staleVerdict);g.Close();
            Check(!g.HearingComplete&&g.save.coins==coins&&g.save.shards==shards,
                "A stale council choice cannot ratify a verdict or grant its rewards outside physical reach");
            g.save.x=dais.x;g.save.y=dais.y;
            Action(g,dais.kind,"hearing_verdict_public");
            Check(g.HearingComplete && g.HearingVerdict==1 &&
                g.save.coins==coins+85 && g.save.shards==shards+2 &&
                g.save.ending==ending && g.GetQuest("hearing").complete,
                "Public ratification gives a one-time real reward and preserves original ending");
            for(int region=1;region<=3;region++)
            {
                g.Enter(region);g.enemies.Clear();
                Check(g.IsObjectPresent(Object(g,"hearingplaque"+region)),
                    "Real regional oath appears only after a fully ratified public vote");
                WorldObject resident=Object(g,"witnessnpc"+region);
                g.save.x=resident.x;g.save.y=resident.y;g.Talk(resident);
                Check(g.mode=="dialog" && g.conversation!=null &&
                    g.conversation.text.Contains("医护") && g.conversation.textEn.Contains("medics") &&
                    Array.Exists(g.conversation.choices,c=>c.action=="hearing_witness_storm") &&
                    !Array.Exists(g.conversation.choices,c=>c.action.StartsWith("hearing_voice_")),
                    "After ratification each witness recognizes the town verdict but cannot sign twice");
                g.Close();
            }
            g.PrepareSave();
            var persisted=new GameCore(data,CloneSave(g.save));persisted.Start();
            Check(persisted.save.version==2 && persisted.HearingComplete &&
                persisted.HearingSignatures==3 && persisted.HearingVerdict==1 &&
                persisted.save.ending==3,
                "Full independent votes, council result and original finale persist in unmodified SaveData v2");
            g.Enter(0);g.enemies.Clear();g.save.x=dais.x;g.save.y=dais.y;
            g.Talk(dais);
            Check(!Array.Exists(g.conversation.choices,c=>c.action=="hearing_verdict_public"),
                "Finished Chapter Seven cannot repeat public rewards or reselect a contradictory verdict");
            g.Close();
            g.Epilogue();
            Check(g.conversation.text.Contains("第七章的听证") &&
                g.conversation.textEn.Contains("Chapter Seven did not"),
                "Earned public verdict actually changes original bilingual ending epilogue");
        }

        public static void UnfinishedCivicHearingNamesOnlyMissingWitnessesInWorld(Campaign data)
        {
            var g=Fresh(data);g.save.stage=3;g.save.ending=3;
            foreach(string flag in new[]{"afterlight_complete","afterlight_started",
                "storm_started","storm_complete","storm_orrin_open",
                "hearing_started","hearing_voice_1_care","hearing_voice_3_watch"})
                g.save.flags.Add(flag);
            WorldObject dais=Object(g,"hearingdais");
            g.save.x=dais.x;g.save.y=dais.y;
            Check(g.HearingReady&&g.HearingSignatures==2,
                "A genuine partly signed hearing has exactly one missing regional witness");
            g.Talk(dais);
            Check(g.conversation!=null &&
                g.conversation.text.Contains("阿菱") &&
                !g.conversation.text.Contains("莫芽") &&
                !g.conversation.text.Contains("维恩") &&
                g.conversation.textEn.Contains("Arin") &&
                !g.conversation.textEn.Contains("Moya") &&
                !g.conversation.textEn.Contains("Venn") &&
                Array.Exists(g.conversation.choices,c=>c.action=="hearing_track"),
                "Lyra asks only for the actual absent signatory instead of repeating all three names");
            QuestGuide q=g.GetQuest("hearing");
            Check(q.room==2 && q.description.Contains("停机钥匙") &&
                !q.description.Contains("真正的") &&
                !q.description.Contains("游戏"),
                "The journal points physically to the missing speaker with grounded, non-meta quest prose");
        }

        public static void ChapterSevenRegionalPoliciesActuallyAlterHealingEnergyAndWorkshopYields(Campaign data)
        {
            var g=Fresh(data);g.save.stage=3;g.save.ending=1;
            g.Enter(1);g.enemies.Clear();g.invulnerable=150;
            WorldObject forest=Object(g,"hearingplaque1");
            g.save.x=forest.x;g.save.y=forest.y;g.save.health=40;
            Advance(g,2);
            float baseline=g.save.health;
            g.save.flags.Add("hearing_complete");
            g.save.flags.Add("hearing_voice_1_care");
            g.save.health=40;Advance(g,2);
            Check(g.save.health>baseline+2.5f && g.IsObjectPresent(forest),
                "Moya's signed care policy genuinely heals the player at its physical forest plaque");
            g.save.health=40;g.save.x=forest.x+7;g.save.y=forest.y;
            Advance(g,2);
            Check(g.save.health<=baseline+.2f,
                "Forest health service has a real location limit and is not global regen");
            g.save.x=forest.x;g.save.y=forest.y;
            g.save.health=40;
            var enemy=new Enemy(86801,0,forest.x+.75f,forest.y,60);
            enemy.stun=100;enemy.timer=100;g.enemies.Add(enemy);
            Advance(g,2);
            Check(g.save.health<=baseline+.2f,
                "Living-root care does not erase danger during actual nearby combat");
            g.enemies.Clear();
            g.Enter(2);g.enemies.Clear();g.invulnerable=150;
            WorldObject river=Object(g,"hearingplaque2");
            g.save.x=river.x;g.save.y=river.y;
            g.save.energy=10;
            Advance(g,2);float normalEnergy=g.save.energy;
            g.save.flags.Add("hearing_voice_2_watch");
            g.save.energy=10;Advance(g,2);
            Check(g.save.energy>normalEnergy+4 &&
                g.IsObjectPresent(river),
                "Arin's emergency-flood policy genuinely powers a local public stamina lantern");
            g.Enter(3);g.enemies.Clear();
            g.save.flags.Add("hearing_voice_3_care");
            g.save.flags.Add("afterlight_plan_3_care");
            g.save.flags.Add("afterlight_done_3");
            g.save.flags.Add("afterlight_started");
            WorldObject foundry=Object(g,"afterlightoutlet3");
            g.save.x=foundry.x;g.save.y=foundry.y;
            g.save.inventory[0]=2;g.save.inventory[1]=5;g.save.inventory[4]=0;
            Action(g,foundry.kind,"afterlight_craft");
            Check(g.save.inventory[4]==3 &&
                g.save.inventory[0]==1 && g.save.inventory[1]==3,
                "Venn's real workers-first policy upgrades the pre-existing workshop from two to three craftable parts, still consuming genuine materials");
        }

        public static void ChapterSevenReserveAndOpenAccessHaveDistinctActualTownEffects(Campaign data)
        {
            foreach(bool publicHealth in new[]{true,false})
            {
                var g=Fresh(data);g.save.stage=3;g.save.ending=2;
                foreach(string f in new[]{"afterlight_complete","storm_complete",
                    "storm_orrin_guard","hearing_started"})
                    g.save.flags.Add(f);
                for(int i=1;i<=3;i++)g.save.flags.Add("hearing_voice_"+i+"_watch");
                WorldObject dais=Object(g,"hearingdais");
                g.save.x=dais.x;g.save.y=dais.y;
                Action(g,dais.kind,publicHealth?"hearing_verdict_public":"hearing_verdict_reserve");
                Check(g.HearingComplete && g.HearingVerdict==(publicHealth?1:2),
                    "Public hearing has two genuinely different permanent civic resolutions");
                g.enemies.Clear();g.invulnerable=100;
                g.save.health=40;g.save.energy=10;
                Advance(g,2);
                float safeEnergy=g.save.energy;
                if(publicHealth)
                    Check(g.save.health>43 && g.save.energy<100,
                        "Open-access vote operates a visible, real nearby medical lamp");
                else
                    Check(g.save.energy>20 && g.save.health<44,
                        "Emergency-reserve vote instead operates a genuine local stamina lamp");
                var enemy=new Enemy(86810,0,dais.x+.7f,dais.y,45);
                enemy.stun=100;enemy.timer=100;g.enemies.Add(enemy);
                g.save.health=40;g.save.energy=10;
                Advance(g,2);
                if(publicHealth)
                    Check(g.save.health<=40.2f,
                        "Political victory never provides healing while a real monster occupies the public dais");
                else
                    Check(g.save.energy<safeEnergy-4.5f,
                        "Reserve lantern supplies no added energy in combat beyond preexisting passive regeneration");
                g.enemies.Clear();
                g.save.x=Object(g,"guild").x;
                g.save.y=Object(g,"guild").y;
                g.save.health=40;g.save.energy=10;
                Advance(g,2);
                if(publicHealth)
                    Check(g.save.health<43,
                        "The new civic lamp must not heal the entire town from anywhere");
            }
        }

        public static void ChapterSevenChronicleDoesNotSpoilUnsignedResidentsOrUnchosenVerdict(Campaign data)
        {
            var g=Fresh(data);g.save.stage=3;g.save.ending=1;
            g.Enter(0);g.enemies.Clear();
            WorldObject book=Object(g,"worldchronicle");
            g.save.x=book.x;g.save.y=book.y;g.Talk(book);
            int sec=Array.FindIndex(g.conversation.choices,c=>c.action=="story_section_after");
            Check(sec>=0,"Existing aftermath chronicle section remains reachable");
            g.Choose(sec);
            Check(!Array.Exists(g.conversation.choices,c=>c.action=="story_read_hearing"),
                "The seventh chapter must never leak into untouched historical records");
            g.Close();
            g.save.flags.Add("hearing_started");
            g.save.flags.Add("hearing_voice_1_care");
            g.Talk(book);sec=Array.FindIndex(g.conversation.choices,c=>c.action=="story_section_after");
            g.Choose(sec);
            int entry=Array.FindIndex(g.conversation.choices,c=>c.action=="story_read_hearing");
            Check(entry>=0,"Chapter Seven appears only after actual public hearing begins");
            g.Choose(entry);
            Check(g.conversation.text.Contains("莫芽") &&
                g.conversation.text.Contains("尚未亲自签署") &&
                !g.conversation.text.Contains("【公开议决已通过】"),
                "Chronicle displays Moya's actual recorded vote without claiming absent citizens or a fabricated verdict");
        }
        public static void ChapterSevenOrrinTakesActualPostHearingResponsibilityWithoutOverwritingTheOldAccord(Campaign data)
        {
            foreach(bool relief in new[]{true,false})
            {
                var g=Fresh(data);g.save.stage=3;g.save.ending=2;
                foreach(string f in new[]{"afterlight_complete","storm_started",
                    "storm_complete","storm_orrin_open","hearing_started",
                    "hearing_complete","hearing_public","boss_4",
                    "hearing_voice_1_care","hearing_voice_2_watch","hearing_voice_3_care"})
                    g.save.flags.Add(f);
                g.Enter(4);g.enemies.Clear();g.invulnerable=100;
                WorldObject shrine=Object(g,"shrine"),
                    shelter=Object(g,"blackoutshelter");
                g.save.x=relief?shelter.x:shrine.x;
                g.save.y=relief?shelter.y:shrine.y;
                g.save.health=40;g.save.energy=10;
                Advance(g,2);
                float baselineHealth=g.save.health,baselineEnergy=g.save.energy;
                int firstItem=relief?6:4;
                int before=g.save.inventory[firstItem];
                g.save.x=shrine.x;g.save.y=shrine.y;
                g.Talk(shrine);
                Check(Array.Exists(g.conversation.choices,c=>c.action=="hearing_orrin_aid") &&
                    Array.Exists(g.conversation.choices,c=>c.action=="hearing_orrin_patrol") &&
                    Array.Exists(g.conversation.choices,c=>c.action=="hearing_orrin_review"),
                    "After authentic public hearing Orrin offers two real responsibilities while leaving his original civilian accord reviewable");
                g.Close();
                Action(g,shrine.kind,relief?"hearing_orrin_aid":"hearing_orrin_patrol");
                Check(g.HearingOrrinResponsibility==(relief?1:2) &&
                    g.save.inventory[firstItem]==before+2 &&
                    g.save.ending==2 && g.HearingComplete &&
                    !g.Has(relief?"hearing_orrin_patrol":"hearing_orrin_aid"),
                    "Orrin transfers exactly two genuine resources once and does not modify the old ending or prior military reserve decision");
                g.Talk(shrine);
                Check(!Array.Exists(g.conversation.choices,c=>
                    c.action=="hearing_orrin_aid"||c.action=="hearing_orrin_patrol") &&
                    Array.Exists(g.conversation.choices,c=>c.action=="hearing_orrin_review"),
                    "Completed civilian responsibility is no longer farmable but old story remains accessible");
                g.Close();
                Action(g,shrine.kind,"hearing_orrin_review");
                Check(g.mode=="dialog" && g.conversation!=null &&
                    g.conversation.speaker.Contains("厄洛"),
                    "The original Orrin post-storm agreement remains readable after the new accountability scene");
                g.Close();
                g.save.x=relief?shelter.x:shrine.x;
                g.save.y=relief?shelter.y:shrine.y;
                g.save.health=40;g.save.energy=10;Advance(g,2);
                if(relief)
                    Check(g.save.health>baselineHealth+2.5f,
                        "The medical-responsibility route actually operates a new public healing refuge in the citadel");
                else
                    Check(g.save.energy>baselineEnergy+6f,
                        "The volunteer-patrol route actually operates a new local public energy station");
                g.PrepareSave();
                var again=new GameCore(data,CloneSave(g.save));again.Start();
                Check(again.save.version==2 && again.HearingOrrinResponsibility==(relief?1:2) &&
                    again.save.ending==2 && again.Has("storm_orrin_open"),
                    "Old SaveData v2 keeps the original Orrin accord and the distinct real post-hearing follow-through");
                g.Epilogue();
                Check(g.conversation.text.Contains(relief?"两份热汤":"两份实用零件"),
                    "Existing original ending acknowledges the actual humanitarian or roadwarden follow-through");
            }
        }


        public static void FullWalletCannotCorruptEitherChapterSevenPublicVerdict(Campaign data)
        {
            foreach (bool open in new[] { true, false })
            {
                var g = Fresh(data);
                g.save.stage = 3; g.save.ending = 2;
                foreach (string flag in new[] { "afterlight_complete", "storm_complete",
                    "storm_orrin_guard", "hearing_started",
                    "hearing_voice_1_care", "hearing_voice_2_watch",
                    "hearing_voice_3_care" }) g.save.flags.Add(flag);
                var dais = Object(g, "hearingdais");
                g.save.x = dais.x; g.save.y = dais.y;
                g.save.coins = 999997; g.save.shards = 99998;
                int startingXp = g.save.xp;
                Action(g, dais.kind, open ? "hearing_verdict_public" :
                    "hearing_verdict_reserve");
                Check(g.HearingComplete && g.HearingVerdict == (open ? 1 : 2) &&
                    g.save.coins == 999999 && g.save.shards == 99999 &&
                    g.save.xp > startingXp && g.save.ending == 2,
                    "Actual civic hearing ratifies the independent votes and XP without overflowing either money field");
                g.PrepareSave();
                var loaded = new GameCore(data, CloneSave(g.save));
                Check(loaded.HearingComplete && loaded.HearingVerdict == (open ? 1 : 2) &&
                    loaded.HearingSignatures == 3 &&
                    loaded.save.coins == 999999 && loaded.save.shards == 99999 &&
                    loaded.save.ending == 2,
                    "Completed medical or reserve ruling and legal cash survive unmodified v2 save format");
                loaded.Talk(dais);
                Check(loaded.conversation != null &&
                    !Array.Exists(loaded.conversation.choices,
                        c => c.action == "hearing_verdict_public" ||
                             c.action == "hearing_verdict_reserve"),
                    "Both previously selected hearing results are irreversible after reload, without repeat payments");
            }
        }

    }
}
