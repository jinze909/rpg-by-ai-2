using System;
namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        private static WorldObject FerryRecord(GameCore g,int stage)
        {
            foreach(WorldObject o in g.Map.objects)
                if(o.id=="floodarchive_"+stage)return o;
            throw new Exception("Missing public ferry record "+stage);
        }
        private static bool FerryChoice(GameCore g,string action)
        {
            if(g.mode!="dialog"||g.conversation==null)return false;
            for(int i=0;i<g.conversation.choices.Length;i++)
                if(g.conversation.choices[i].action==action)
                {g.Choose(i);return true;}
            return false;
        }
        public static void DrownedFloodArchiveRequiresRealTidesAndChoice(Campaign data)
        {
            foreach(int ending in new[]{1,2})
            {
                var g=Fresh(data);g.save.stage=1;g.Enter(2);g.enemies.Clear();
                var first=FerryRecord(g,1);
                var second=FerryRecord(g,2);
                var third=FerryRecord(g,3);
                Check(g.GetQuest("flood_archive").locked,
                    "The original missing scout must be rescued to unlock the civic tide evidence");
                foreach(WorldObject o in new[]{first,second,third})
                {
                    Check(g.CanStand(new Point(o.x,o.y)),
                        "The new archive markers occupy valid, original collision-safe map terrain");
                    g.save.x=data.maps[2].spawnX;g.save.y=data.maps[2].spawnY;
                    Check(g.NavigationPath(new Point(o.x,o.y)).Count>0,
                        "Every public ferry archive marker is physically reachable from the original map spawn");
                }
                g.save.x=third.x;g.save.y=third.y;g.Talk(third);
                Check(g.conversation!=null&&g.conversation.choices.Length==1,
                    "Without the scout, the final hearing cannot grant a free reward");
                g.Close();g.save.flags.Add("scout_saved");
                Check(!g.GetQuest("flood_archive").locked,"The real scout unlocks the new quest");
                g.save.totalTime=0;
                g.save.x=first.x;g.save.y=first.y;g.Talk(first);
                Check(g.conversation.choices.Length==1&&
                    !FerryChoice(g,"floodarchive_1"),
                    "At low tide the high-water evidence is visibly unavailable");
                g.Close();
                g.save.totalTime=24;g.Talk(first);
                Check(g.conversation.choices.Length==1,
                    "Only the properly visible high waterline can be transcribed");
                // Revalidate the tide at choice dispatch, even if the view was stale.
                g.save.totalTime=0;
                Check(FerryChoice(g,"floodarchive_1")&&g.FloodArchiveProgress==0,
                    "Changing tide before commit must reject a forged high-water claim");
                g.save.totalTime=24;g.Talk(first);
                Check(FerryChoice(g,"floodarchive_1")&&g.FloodArchiveProgress==1,
                    "A real high-tide interaction records the original bridge gauge");
                g.Talk(first);Check(g.conversation.choices.Length==1,
                    "Previously read evidence cannot yield another reward");g.Close();
                g.PrepareSave();g.Load(CloneSave(g.save));g.enemies.Clear();
                Check(g.FloodArchiveProgress==1&&g.save.version==2,
                    "First tide stage persists in the unchanged old v2 save format");
                g.save.x=third.x;g.save.y=third.y;g.Talk(third);
                Check(g.conversation.choices.Length==1&&g.FloodArchiveProgress==1,
                    "The public hearing cannot skip the low-tide ferry names");g.Close();
                g.save.x=second.x;g.save.y=second.y;g.save.totalTime=24;
                g.Talk(second);
                Check(!FerryChoice(g,"floodarchive_2")&&
                    g.conversation.choices.Length==1,
                    "High tide hides the historical submerged ferry roster");
                g.Close();
                g.save.totalTime=36;g.Talk(second);
                Check(FerryChoice(g,"floodarchive_2")&&g.FloodArchiveProgress==2,
                    "Actual low tide reveals the second civilian record in order");
                int money=g.save.coins,shards=g.save.shards;
                g.save.x=third.x;g.save.y=third.y;g.Talk(third);
                string outcome=ending==1?"floodarchive_public":"floodarchive_chart";
                Check(FerryChoice(g,outcome)&&g.FloodArchiveChoice==ending&&
                    g.FloodArchiveComplete&&g.save.coins==money+50&&
                    g.save.shards==shards+2,
                    "The physical final hearing completes one of two meaningful and non-farmable outcomes");
                var q=g.GetQuest("flood_archive");
                Check(q.complete&&q.steps.Count==3&&q.steps.TrueForAll(s=>s.done),
                    "The questbook displays the real three-stop ferry restoration");
                g.Talk(third);Check(g.conversation.choices.Length==1,
                    "Published decisions are reread-only and cannot be chosen again");g.Close();
                g.save.health=50;g.save.energy=0;g.enemies.Clear();
                g.projectiles.Clear();g.hazards.Clear();
                g.Tick(.05f,new Point());
                Check(g.FloodArchiveRefuge&&
                    (ending==1?g.save.health>50.05f:g.save.energy>.75f),
                    "Each original-player choice grants a different, real and nearby safe-rest service");
                g.save.x=data.maps[2].spawnX;g.save.y=data.maps[2].spawnY;
                Check(!g.FloodArchiveRefuge,"Distant map locations never grant free regional regeneration");
                g.PrepareSave();var reloaded=new GameCore(data,CloneSave(g.save));
                Check(reloaded.save.version==2&&reloaded.FloodArchiveChoice==ending&&
                    reloaded.FloodArchiveComplete&&reloaded.GetQuest("flood_archive").complete,
                    "Existing version-two saves retain the player's chosen branch and quest completion");
            }
        }
    }
}
