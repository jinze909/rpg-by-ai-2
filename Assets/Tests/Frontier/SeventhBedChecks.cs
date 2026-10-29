using System;
namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        private static WorldObject SeventhSite(GameCore g,string id)
        {
            foreach(WorldObject o in g.Map.objects)
                if(o.id==id)return o;
            throw new Exception("Missing original authored inn evidence site "+id);
        }
        private static bool SeventhChoice(GameCore g,string action)
        {
            if(g.mode!="dialog"||g.conversation==null)return false;
            int n=Array.FindIndex(g.conversation.choices,c=>c.action==action);
            if(n<0)return false;
            g.Choose(n);return true;
        }
        private static GameCore SeventhReady(Campaign data)
        {
            GameCore g=Fresh(data);
            g.save.stage=1;
            g.Enter(9);g.enemies.Clear();g.invulnerable=900f;
            Check(g.save.room==9&&g.SeventhBedUnlocked&&
                !g.SeventhBedStarted&&!g.SeventhBedComplete,
                "An ordinary post-first-core character reaches the real Hearth Inn without fake completion flags");
            return g;
        }
        private static void SeventhBegin(GameCore g)
        {
            WorldObject notice=SeventhSite(g,"seventh_notice");
            Check(g.CanStand(new Point(notice.x,notice.y))&&
                PlaceWithinTalkReach(g,notice),
                "Actual opening notice is on reachable floor with valid dialogue footing");
            g.Talk(notice);
            Check(SeventhChoice(g,"seventh_begin")&&g.SeventhBedStarted&&
                g.save.trackedQuest=="seventh_bed"&&
                g.GetQuest("seventh_bed").steps.Count==7,
                "The physical notice starts the full seven-step tracked witness investigation");
        }
        private static void SeventhEvidence(GameCore g)
        {
            string[] ids={"seventh_stamp","seventh_cot","seventh_reply"};
            string[] actions={"seventh_take_stamp","seventh_take_blanket","seventh_take_letter"};
            for(int i=0;i<3;i++)
            {
                WorldObject site=SeventhSite(g,ids[i]);
                Check(g.CanStand(new Point(site.x,site.y))&&
                    PlaceWithinTalkReach(g,site),
                    "Every original guest room and annex evidence object is walkable and reachable: "+ids[i]);
                g.Talk(site);
                Check(SeventhChoice(g,actions[i])&&
                    g.SeventhBedEvidenceCount==i+1&&
                    g.GetQuest("seventh_bed").steps[i+1].done,
                    "The original "+ids[i]+" physically grants exactly its own independent authored clue");
                g.Talk(site);
                Check(!SeventhChoice(g,actions[i])&&g.SeventhBedEvidenceCount==i+1,
                    "Already-read evidence is not farmable by repeated source interactions");
            }
            WorldObject cook=SeventhSite(g,"innCook");
            Check(PlaceWithinTalkReach(g,cook),
                "Original cook remains physically reachable and is NOT replaced by quest NPC");
            g.Talk(cook);
            Check(SeventhChoice(g,"seventh_ask_ari")&&
                g.SeventhBedCook&&g.SeventhBedEvidenceCount==4,
                "Real chef provides the fourth eyewitness account without replacing older cooking dialogue");
            WorldObject traveler=SeventhSite(g,"innTraveler");
            Check(PlaceWithinTalkReach(g,traveler),"Original night traveler can be questioned");
            g.Talk(traveler);
            Check(SeventhChoice(g,"seventh_traveler")&&
                g.conversation!=null&&
                g.conversation.textEn.Contains("No chains")&&
                g.conversation.speaker==traveler.name,
                "Original night traveler has a coherent physical optional witness conversation");
            g.Close();
        }
        public static void SeventhBedAnnexLetterIsReallyReachableByLiveInnWorldWalking(Campaign data)
        {
            var g=SeventhReady(data);
            SeventhBegin(g);
            WorldObject notice=SeventhSite(g,"seventh_notice"),
                annex=SeventhSite(g,"seventh_reply");
            Check(PlaceWithinTalkReach(g,notice),
                "The real first clue routes from the original near-side inn hall");
            float firstDistance=(g.Position-new Point(annex.x,annex.y)).Length;
            Check(firstDistance>15f,
                "The distant annex source is a genuinely separated investigation destination");
            int steps=0;
            Point target=new Point(annex.x,annex.y);
            for(;steps<2600&&(g.Position-target).Length>1.1f;steps++)
            {
                Point direction=g.DirectionTo(g.Position,target);
                if(direction.Length<.001f)break;
                g.Tick(.05f,direction);
            }
            Check((g.Position-target).Length<1.75f&&steps>55&&
                g.CanStand(g.Position),
                "True terrain-steered player movement crosses the original inn corridors and reaches the annex without teleporting through walls");
            g.Talk(annex);
            Check(SeventhChoice(g,"seventh_take_letter")&&
                g.SeventhBedLetter&&!g.SeventhBedBlanket&&
                !g.SeventhBedStamp,
                "Reaching the far annex by real world walking grants ONLY the letter, never invents other clues");
            Check(g.SeventhBedEvidenceCount==1,
                "Physical investigation remains flexible rather than falsely enforcing a locked linear quest menu");
        }
        public static void SeventhBedRejectsUnstartedAndStaleDialogueAndPersistsPartialEvidence(Campaign data)
        {
            var untouched=Fresh(data);
            Check(!untouched.SeventhBedUnlocked&&!untouched.SeventhBedStarted&&
                untouched.GetQuest("seventh_bed").locked,
                "Earlier old v2 save shows an available future chapter, not fabricated completed evidence");
            var g=SeventhReady(data);
            WorldObject stamp=SeventhSite(g,"seventh_stamp");
            Check(PlaceWithinTalkReach(g,stamp),"Real first piece of evidence is accessible");
            g.Talk(stamp);
            Check(!SeventhChoice(g,"seventh_take_stamp")&&!g.SeventhBedStamp,
                "Reading outside a source-bound investigation never banks a fake signature");
            WorldObject notice=SeventhSite(g,"seventh_notice");
            Check(PlaceWithinTalkReach(g,notice),"Return to original inn notice");
            g.Talk(notice);
            int start=Array.FindIndex(g.conversation.choices,c=>c.action=="seventh_begin");
            Check(start>=0,"Original notice has a real offer");
            g.save.x=44.5f;g.save.y=13.5f;
            g.Choose(start);
            Check(!g.SeventhBedStarted,
                "A remote/stale dialogue choice may not start a quest without physical contact");
            SeventhBegin(g);
            Check(PlaceWithinTalkReach(g,stamp),"An original nearby clue can be inspected after start");
            g.Talk(stamp);
            Check(SeventhChoice(g,"seventh_take_stamp")&&g.SeventhBedStamp,
                "An earned physical receipt records its real dated evidence");
            g.PrepareSave();
            var restored=new GameCore(data,CloneSave(g.save));
            Check(restored.save.version==2&&restored.SeventhBedStarted&&
                restored.SeventhBedStamp&&!restored.SeventhBedBlanket&&
                !restored.SeventhBedComplete,
                "Partial case progress survives exact legacy SaveData v2 with no new schema");
        }
        public static void SeventhBedTrueDeductionHasTwoEarnedCivicOutcomesAndCleanSkill(Campaign data)
        {
            for(int branch=0;branch<2;branch++)
            {
                var g=SeventhReady(data);SeventhBegin(g);SeventhEvidence(g);
                WorldObject notice=SeventhSite(g,"seventh_notice"),
                    cook=SeventhSite(g,"innCook");
                Check(PlaceWithinTalkReach(g,notice),
                    "All four independent testimonies must return to the original notice");
                g.Talk(notice);
                Check(SeventhChoice(g,"seventh_reconstruct")&&
                    g.SeventhBedDeduction&&!g.SeventhBedFalseAccusation&&
                    !g.SeventhBedComplete&&
                    g.GetQuest("seventh_bed").steps[5].done,
                    "The correct player deduction marks only the earned truth, never an instant outcome");
                g.PrepareSave();
                var noCleanSave=CloneSave(g.save);
                noCleanSave.flags.Add("seventh_bed_misread");
                var ordinary=new GameCore(data,noCleanSave);
                ordinary.Start();ordinary.enemies.Clear();
                WorldObject ordinaryCook=SeventhSite(ordinary,"innCook");
                Check(PlaceWithinTalkReach(ordinary,ordinaryCook),
                    "The matched ordinary v2 control reaches the same original cook for equivalent XP");
                ordinary.Talk(ordinaryCook);
                string finale=branch==0?"seventh_announce":"seventh_shield";
                Check(SeventhChoice(ordinary,finale)&&ordinary.SeventhBedComplete,
                    "Ordinary rewarded v2 control also earns the exact same chapter and level-XP outcome");
                Check(PlaceWithinTalkReach(g,cook),
                    "Original cook physically receives the completed source-based reasoning");
                int coins=g.save.coins,shards=g.save.shards;
                g.Talk(cook);
                Check(SeventhChoice(g,finale)&&g.SeventhBedComplete&&
                    (branch==0?g.SeventhBedPublic&&!g.SeventhBedPrivate:
                        g.SeventhBedPrivate&&!g.SeventhBedPublic)&&
                    g.save.coins==coins+45&&g.save.shards==shards+1&&
                    g.save.level==ordinary.save.level&&
                    g.save.xp==ordinary.save.xp&&
                    g.save.skillPoints==Math.Min(99,ordinary.save.skillPoints+1),
                    "Clean deduction yields exactly one bonus talent above identical normal XP and two exclusive endings");
                Check(g.GetQuest("seventh_bed").complete,
                    "Chapter completion is recorded in the actual live seven-stage quest guide");
                g.Talk(cook);
                Check(!SeventhChoice(g,finale)&&g.save.coins==coins+45,
                    "Repeat cook conversations cannot bank a second reward");
                ReadInnNews(g,SeventhSite(g,"innTraveler"));
                Check(branch==0?
                    g.conversation.textEn.Contains("Seven reached shelter alive"):
                    g.conversation.textEn.Contains("seventh bed stays unassigned"),
                    "Actual traveler news honors only the earned resolution, not a fabricated global narration");
                g.Close();g.PrepareSave();
                var loaded=new GameCore(data,CloneSave(g.save));
                Check(loaded.save.version==2&&loaded.SeventhBedComplete&&
                    (branch==0?loaded.SeventhBedPublic:loaded.SeventhBedPrivate),
                    "Both ethical outcomes persist through the unchanged SaveData v2 save format");
                // The earned inn service must function on real unblocked
                // guest-room / kitchen ground, not as a universal buff.
                WorldObject service=SeventhSite(g,branch==0?"innRest":"innCook");
                Check(PlaceWithinTalkReach(g,service),
                    "Completed service binds to the original physical room/cook");
                var controlSave=CloneSave(g.save);
                controlSave.flags.Remove(branch==0?
                    "seventh_bed_public":"seventh_bed_private");
                var control=new GameCore(data,controlSave);
                control.Start();control.enemies.Clear();g.enemies.Clear();
                control.save.x=g.save.x;control.save.y=g.save.y;
                if(branch==0)g.save.health=control.save.health=10f;
                else g.save.energy=control.save.energy=20f;
                for(int i=0;i<25;i++)
                {g.Tick(.05f,new Point());control.Tick(.05f,new Point());}
                Check(branch==0?g.save.health>control.save.health+1.6f:
                    g.save.energy>control.save.energy+3.1f,
                    "The unique completed civic ending grants physical local safe relief beyond baseline recovery");
            }
        }
        public static void SeventhBedWrongAccusationIsRecoverableButCannotEarnFlawlessTalent(Campaign data)
        {
            var g=SeventhReady(data);SeventhBegin(g);SeventhEvidence(g);
            WorldObject notice=SeventhSite(g,"seventh_notice"),
                cook=SeventhSite(g,"innCook");
            Check(PlaceWithinTalkReach(g,notice),"Original notice is accessible after all clues");
            g.Talk(notice);
            Check(SeventhChoice(g,"seventh_accuse")&&
                !g.SeventhBedDeduction&&g.SeventhBedFalseAccusation,
                "Unjustified theft accusation is an actual rejectable player decision with durable cost");
            g.Talk(notice);
            Check(SeventhChoice(g,"seventh_abduct")&&
                !g.SeventhBedDeduction&&g.SeventhBedFalseAccusation,
                "Independent mistaken patrol accusation does not silently resolve case or erase first mistake");
            g.Talk(notice);
            Check(SeventhChoice(g,"seventh_reconstruct")&&g.SeventhBedDeduction,
                "A player can reconsider all four facts and still complete the social mystery");
            g.PrepareSave();var ordinary=CloneSave(g.save);
            ordinary.flags.Remove("seventh_bed_misread");
            var clean=new GameCore(data,ordinary);
            clean.Start();clean.enemies.Clear();
            WorldObject cleanCook=SeventhSite(clean,"innCook");
            Check(PlaceWithinTalkReach(clean,cleanCook),"Clean comparison has same real cook and reward");
            clean.Talk(cleanCook);
            Check(SeventhChoice(clean,"seventh_shield")&&clean.SeventhBedComplete,
                "The identical but unaccused witness path permits true clean completion");
            Check(PlaceWithinTalkReach(g,cook),"The accused branch remains returnable to original cook");
            g.Talk(cook);
            Check(SeventhChoice(g,"seventh_shield")&&
                g.SeventhBedComplete&&g.save.level==clean.save.level&&
                g.save.skillPoints==clean.save.skillPoints-1,
                "Wrong accusation never prevents completion but permanently forfeits exactly one bonus skill");
        }
    }
}
