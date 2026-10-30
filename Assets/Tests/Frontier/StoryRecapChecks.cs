using System;
using System.Collections.Generic;

namespace Starfall.Tests
{
    public static partial class CoreChecks
    {

        private static WorldObject NightShiftAt(GameCore game,int room)
        {
            game.mode="world";game.save.room=room;
            game.enemies.Clear();
            WorldObject site=Array.Find(game.Map.objects,
                o=>o!=null&&o.id=="nightledger"+room&&o.kind=="nightledger");
            Check(site!=null,"A real night-shift source exists in room "+room);
            game.save.x=site.x;game.save.y=site.y;
            Check(game.CanStand(game.Position),
                "Night-shift actor stands on authentic walkable ground in region "+room);
            return site;
        }
        private static void NightShiftChoose(GameCore game,WorldObject site,
            string action)
        {
            game.Close();game.mode="world";
            game.Talk(site);
            Check(game.conversation!=null,
                "The real local night-shift source opens a dialog");
            int option=Array.FindIndex(game.conversation.choices,
                choice=>choice.action==action);
            Check(option>=0,"The current genuine source offers "+action);
            game.Choose(option);
        }
        public static void NightShiftFourRealPlacesBranchAndRetainOldSaveV2(Campaign data)
        {
            var g=Fresh(data);
            int originalCoin=g.save.coins;
            Check(!g.NightShiftComplete&&g.NightShiftEvidenceCount==0&&
                g.GetQuest("night_shift").room==0,
                "An untouched older save discovers a voluntary original town story");
            for(int room=0;room<=3;room++)
            {
                WorldObject site=NightShiftAt(g,room);
                Point spawn=new Point(g.Map.spawnX,g.Map.spawnY);
                Check(g.NavigationPathFrom(spawn,new Point(site.x,site.y),true).Count>0,
                    "Night-shift prop is accessible along authentic map collision in room "+room);
            }
            WorldObject town=NightShiftAt(g,0);
            NightShiftChoose(g,town,"nightshift_begin");
            Check(g.Has("nightshift_started")&&
                g.save.trackedQuest=="night_shift"&&
                g.GetQuest("night_shift").steps[0].done,
                "A conscious local acceptance begins the actual four-stop quest");
            // Jumping directly to the final mine spool before reading the
            // other sources can never forge a seal or spend inventory.
            WorldObject mine=NightShiftAt(g,3);
            int held=g.save.inventory[4];
            g.Talk(mine);
            Check(g.conversation!=null&&
                Array.FindIndex(g.conversation.choices,
                    c=>c.action=="nightshift_trace_3")<0&&
                g.save.inventory[4]==held,
                "The final physical record cannot skip the first two timestamps");
            WorldObject forest=NightShiftAt(g,1);
            float baselineSpeed=g.Speed;
            NightShiftChoose(g,forest,"nightshift_trace_1");
            Check(g.NightShiftEvidenceCount==1&&
                g.GetQuest("night_shift").steps[1].done,
                "The original forest evidence updates the read-only quest steps");
            WorldObject ruin=NightShiftAt(g,2);
            NightShiftChoose(g,ruin,"nightshift_false");
            Check(g.NightShiftEvidenceCount==1&&
                !g.Has("nightshift_trace_2"),
                "Copying an impossible official tide time cannot win a false clue");
            NightShiftChoose(g,ruin,"nightshift_trace_2");
            Check(g.NightShiftEvidenceCount==2,
                "The authentic low-tide timestamp advances the second clue");
            mine=NightShiftAt(g,3);
            g.save.inventory[4]=0;
            NightShiftChoose(g,mine,"nightshift_trace_3");
            Check(g.NightShiftEvidenceCount==2&&
                !g.Has("nightshift_trace_3"),
                "A physical sealed spool refuses to open without the real part");
            g.save.inventory[4]=1;
            NightShiftChoose(g,mine,"nightshift_trace_3");
            Check(g.NightShiftEvidenceCount==3&&
                g.save.inventory[4]==0&&
                g.GetQuest("night_shift").steps[3].done,
                "The mine costs exactly one real part and completes the field investigation");
            town=NightShiftAt(g,0);
            NightShiftChoose(g,town,"nightshift_public");
            Check(g.NightShiftComplete&&g.Has("nightshift_public")&&
                !g.Has("nightshift_private")&&
                g.save.coins==originalCoin+40&&
                g.GetQuest("night_shift").complete,
                "The town publication requires all three signatures and pays once");
            g.Close();g.mode="world";
            // A repeat conversation gives only the archived account, not
            // another publish button or duplicate money.
            g.Talk(town);
            Check(Array.FindIndex(g.conversation.choices,
                    c=>c.action=="nightshift_public")<0&&
                g.save.coins==originalCoin+40,
                "Reopening the completed town roll never duplicates rewards");
            g.Close();g.save.room=1;
            Check(g.Speed>=baselineSpeed+.20f&&g.save.version==2,
                "Publicly restoring the old road has an actual measurable field travel benefit");
            g.PrepareSave();
            var restored=new GameCore(data,CloneSave(g.save));
            Check(restored.NightShiftComplete&&
                restored.GetQuest("night_shift").complete&&
                restored.save.version==2&&
                restored.save.trackedQuest=="night_shift",
                "The optional outcome survives an unchanged SaveData v2 reload");
            var privateRun=Fresh(data);
            privateRun.save.flags.Add("nightshift_started");
            for(int r=1;r<=3;r++)
                privateRun.save.flags.Add("nightshift_trace_"+r);
            WorldObject desk=NightShiftAt(privateRun,0);
            float oldMax=privateRun.save.maxHealth;
            int oldPrivateCoins=privateRun.save.coins;
            NightShiftChoose(privateRun,desk,"nightshift_private");
            Check(privateRun.Has("nightshift_private")&&
                !privateRun.Has("nightshift_public")&&
                privateRun.save.maxHealth==oldMax+8&&
                privateRun.save.coins==oldPrivateCoins+40&&
                privateRun.save.version==2,
                "The distinct witness-protection resolution grants real one-time max health");
            privateRun.Close();privateRun.Talk(desk);
            Check(Array.FindIndex(privateRun.conversation.choices,
                c=>c.action=="nightshift_private")<0&&
                privateRun.save.maxHealth==oldMax+8,
                "An old save can revisit the protected records without farming health");
        }

        public static void StoryRecapOnlyRevealsChaptersPlayerReached(Campaign data)
        {
            var game=Fresh(data);
            int initialFlags=game.save.flags.Count;
            List<StoryBeat> opening=game.StoryRecap();
            Check(opening.Count==1 && opening[0].chapter==1 &&
                opening[0].questId=="main" && !opening[0].complete &&
                opening[0].next==game.GetQuest("main").next,
                "A new adventure receives only its real first story beat and next objective");
            Check(!opening[0].summary.Contains("星核事件之后") &&
                game.save.flags.Count==initialFlags,
                "Reading story does not spoil postwar outcomes or write any campaign flags");
            game.save.flags.Add("scout_saved");
            var scout=game.StoryRecap();
            Check(scout.Count==1 && scout[0].summary.Contains("侦察员已经找到"),
                "Once the scout is rescued the explanation adds only already discovered evidence");
            game.save.stage=1;
            var second=game.StoryRecap();
            Check(second.Count==2 && second[0].complete &&
                second[1].chapter==2 && !second[1].complete,
                "Completing the first core changes its beat to history and reveals the ruins");
            game.save.stage=2;
            Check(game.StoryRecap().Count==3,
                "The mining chapter remains hidden until the second core has progressed");
            game.save.stage=3;
            var citadel=game.StoryRecap();
            Check(citadel.Count==4 && citadel[3].chapter==4 &&
                !citadel[3].complete && citadel[3].questId=="main",
                "The citadel and three-core investigation appear only on entering chapter four");
            game.save.flags.Add("triad_complete");
            Check(game.StoryRecap()[3].summary.Contains("已经公开"),
                "Publishing the three-core evidence updates the story's real historical explanation");
            Check(game.StoryRecap().Count==4,
                "Research alone does not reveal the consequences of an unchosen ending");
        }

        public static void StoryRecapAftermathFollowsActualUnlocksAndQuestNavigation(Campaign data)
        {
            var game=Fresh(data);
            game.save.stage=3;
            game.save.ending=1;
            var beats=game.StoryRecap();
            Check(beats.Count>=5 && beats[3].complete &&
                beats[4].chapter==5 && beats[4].questId=="afterlight" &&
                !beats[4].complete && beats[4].next==game.GetQuest("afterlight").next,
                "Original final choice reveals only the unlocked reconstruction chapter and its live quest");
            foreach(StoryBeat beat in beats)
            {
                Check(beat.chapter>=1 && beat.chapter<=12 &&
                    !string.IsNullOrEmpty(beat.title) &&
                    !string.IsNullOrEmpty(beat.summary) &&
                    !string.IsNullOrEmpty(beat.next) &&
                    Array.IndexOf(GameCore.QuestIds,beat.questId)>=0,
                    "Every visible story summary is readable and linked to a real trackable quest");
                Check(beat.chapter<=5 || !game.GetQuest(beat.questId).locked,
                    "No later locked chapter can leak into the completed-journey journal");
            }
            game.save.english=true;
            var english=game.StoryRecap();
            Check(english.Count==beats.Count && english[4].title.StartsWith("Afterstory") &&
                english[4].next==game.GetQuest("afterlight").next &&
                !english[4].summary.Contains("星核"),
                "Already unlocked story pages, explanations and goals switch to English together");
            game.save.flags.Add("afterlight_started");
            var updated=game.StoryRecap();
            Check(updated[4].next==game.GetQuest("afterlight").next &&
                updated[4].next!=english[4].next,
                "New reconstruction progress updates the explanation's next step without a saved recap cache");
            game.save.english=false;
            Check(game.StoryRecap()[4].summary.Contains("星核") &&
                game.StoryRecap()[4].chapter==5,
                "Switching back to Chinese preserves the same chapter order and actual progress");
        }

        public static void StoryRecapDistinguishesTheThreeActualEndings(Campaign data)
        {
            var game=Fresh(data);
            game.save.stage=3;
            game.save.flags.Add("triad_complete");
            Check(!game.StoryRecap()[3].summary.Contains("归还星核") &&
                !game.StoryRecap()[3].summary.Contains("保留了屏障"),
                "The finale does not reveal unchosen ending consequences");
            game.save.ending=1;
            string returning=game.StoryRecap()[3].summary;
            game.save.ending=2;
            string keeping=game.StoryRecap()[3].summary;
            game.save.ending=3;
            string sharing=game.StoryRecap()[3].summary;
            Check(returning.Contains("河流与土地开始恢复") &&
                keeping.Contains("对城外人开放庇护") &&
                sharing.Contains("公共水泵") &&
                returning!=keeping && keeping!=sharing,
                "Every saved original ending receives its own truthful account of the lasting decision");
            game.save.english=true;
            game.save.ending=2;
            Check(game.StoryRecap()[3].summary.Contains("You kept the shield") &&
                !game.StoryRecap()[3].summary.Contains("你保留了屏障"),
                "The specific past ending is also readable in English without a duplicated Chinese suffix");
        }

        public static void ChapterTwelveStoryRecapTracksActualThreeRegionAudit(Campaign data)
        {
            var game=Fresh(data);
            game.save.stage=3;game.save.ending=1;
            Check(!game.StoryRecap().Exists(b=>b.chapter==12),
                "The new story recap never reveals Chapter Twelve before the genuine completed public grid");
            game.save.flags.Add("civic_conduit_complete");
            List<StoryBeat> opened=game.StoryRecap();
            StoryBeat chapter=opened[opened.Count-1];
            Check(chapter.chapter==12&&chapter.questId=="civic_aftercare"&&
                chapter.title.Contains("后日谈")&&!chapter.complete&&
                chapter.next==game.GetQuest("civic_aftercare").next&&
                chapter.summary.Contains("真实路碑"),
                "The completed public grid reveals a voluntary physical marker survey, not another required tavern chapter");
            game.save.flags.Add("civic_aftercare_started");
            List<StoryBeat> started=game.StoryRecap();
            Check(started[started.Count-1].chapter==12&&
                started[started.Count-1].summary.Contains("巡路簿")&&
                started[started.Count-1].next==
                    game.GetQuest("civic_aftercare").next,
                "Chapter Twelve speaks of the actual accepted road ledger, not an unstarted task");
            game.save.flags.Add("civic_aftercare_waytrail_0");
            Check(game.StoryRecap()[game.StoryRecap().Count-1].next==
                game.GetQuest("civic_aftercare").next&&
                game.GetQuest("civic_aftercare").room==3,
                "The story recap follows the real second regional waypoint after Canal is surveyed");
            game.save.flags.Add("civic_aftercare_waytrail_3");
            game.save.flags.Add("civic_aftercare_waytrail_5");
            StoryBeat ready=game.StoryRecap()[game.StoryRecap().Count-1];
            Check(!ready.complete&&ready.next==
                game.GetQuest("civic_aftercare").next&&
                game.GetQuest("civic_aftercare").room==9,
                "Three visited landmarks are not falsely counted as a published report; the live goal returns to the Inn");
            game.save.english=true;
            StoryBeat english=game.StoryRecap()[game.StoryRecap().Count-1];
            Check(english.title.StartsWith("Afterstory")&&
                english.summary.Contains("Hearth Inn")&&
                !english.summary.Contains("夜行旅人")&&
                english.next==game.GetQuest("civic_aftercare").next,
                "The same earned chapter and actual quest goal render entirely in English");
            game.save.flags.Add("civic_aftercare_published");
            StoryBeat complete=game.StoryRecap()[game.StoryRecap().Count-1];
            Check(complete.chapter==12&&complete.complete&&
                complete.summary.Contains("road ledger")&&
                complete.next.Contains("stock the waystations")&&
                game.StoryRecapTrackableQuestId(complete)=="civic_waystations",
                "Real public publication retires the road audit and records its permanent field-kit legacy");
            int count=game.save.flags.Count,coins=game.save.coins;
            game.PrepareSave();
            var reloaded=new GameCore(data,CloneSave(game.save));
            reloaded.Start();
            List<StoryBeat> resumed=reloaded.StoryRecap();
            Check(reloaded.save.version==2&&resumed[resumed.Count-1].chapter==12&&
                resumed[resumed.Count-1].complete&&
                reloaded.save.flags.Count==count&&reloaded.save.coins==coins,
                "No Chapter Twelve cache or SaveData migration is required to restore the final public road ledger");
        }

        public static void ChapterTwelveJournalNavigatesToRealWaystationsAfterAudit(Campaign data)
        {
            var game=Fresh(data);
            game.save.stage=3;game.save.ending=3;
            game.save.flags.Add("civic_conduit_complete");
            StoryBeat pending=game.StoryRecap()[game.StoryRecap().Count-1];
            Check(!pending.complete&&
                game.StoryRecapTrackableQuestId(pending)=="civic_aftercare",
                "The Journey panel first tracks the real unfinished three-region road audit");
            game.save.flags.Add("civic_aftercare_started");
            game.save.flags.Add("civic_aftercare_waytrail_0");
            game.save.flags.Add("civic_aftercare_waytrail_3");
            game.save.flags.Add("civic_aftercare_waytrail_5");
            game.save.flags.Add("civic_aftercare_published");
            StoryBeat published=game.StoryRecap()[game.StoryRecap().Count-1];
            Check(published.chapter==12&&published.complete&&
                game.StoryRecapTrackableQuestId(published)=="civic_waystations"&&
                published.next.Contains(game.GetQuest("civic_waystations").next),
                "The finished audit still gives a clickable, truthful task and destination for outstanding waystation construction");
            int initialCoins=game.save.coins,flags=game.save.flags.Count;
            game.TrackQuest(game.StoryRecapTrackableQuestId(published));
            Check(game.save.trackedQuest=="civic_waystations"&&
                game.save.coins==initialCoins&&game.save.flags.Count==flags,
                "The actual Journey navigation selects live civic resupply without handing out any supplies or resurrecting the completed audit");
            game.save.english=true;
            StoryBeat english=game.StoryRecap()[game.StoryRecap().Count-1];
            Check(english.next.Contains("stock the waystations")&&
                english.next.Contains(game.GetQuest("civic_waystations").next)&&
                !english.next.Contains("路站"),
                "The continued road-network goal is bilingual and resolves the real local quest target");
            game.save.flags.Add("civic_waystation_stocked_0");
            game.save.flags.Add("civic_waystation_stocked_3");
            StoryBeat shuttle=game.StoryRecap()[game.StoryRecap().Count-1];
            Check(shuttle.next.Contains(game.GetQuest("civic_waystations").next)&&
                shuttle.next.Contains("Shuttles now run"),
                "After two paid outposts the same chapter follows real unlocked shuttles and the final unbuilt station");
            game.save.flags.Add("civic_waystation_stocked_5");
            game.save.flags.Add("civic_waystations_complete");
            StoryBeat finished=game.StoryRecap()[game.StoryRecap().Count-1];
            Check(game.StoryRecapTrackableQuestId(finished)==null&&
                finished.next.Contains("herbal field kits")&&
                !finished.next.Contains("stock the waystations"),
                "After all three genuine outposts are complete the Journey button returns to a harmless historical record");
            var previous=game.StoryRecap()[3];
            Check(game.StoryRecapTrackableQuestId(previous)==null&&
                game.StoryRecapTrackableQuestId(null)==null&&
                game.save.version==2,
                "Old completed chapters and missing pages never offer ghost task tracking");
        }

        public static void StoryRecapNeverRewritesExistingSaveOrQuestTracking(Campaign data)
        {
            var game=Fresh(data);
            game.save.stage=3;
            game.save.ending=1;
            game.save.trackedQuest="main";
            int coins=game.save.coins,shards=game.save.shards,skills=game.save.skillPoints;
            int moments=game.save.moments.Count,revision=game.revision;
            for(int i=0;i<9;i++)
            {
                var beats=game.StoryRecap();
                Check(beats[0].chapter==1 && beats[beats.Count-1].chapter>=5,
                    "Repeated journal reads remain historically ordered");
            }
            Check(game.save.trackedQuest=="main" && game.save.coins==coins &&
                game.save.shards==shards && game.save.skillPoints==skills &&
                game.save.moments.Count==moments && game.revision==revision,
                "Viewing a past chapter never awards rewards, changes quest tracking or mutates save data");
            game.PrepareSave();
            var restored=new GameCore(data,CloneSave(game.save));
            restored.Start();
            Check(restored.StoryRecap().Count==game.StoryRecap().Count &&
                restored.StoryRecap()[4].questId=="afterlight",
                "Existing SaveData v2 is sufficient to reconstruct all chapter context after reload");
        }
    }
}
