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
        public static void RestoredFerrymanRespondsToActualCivicArchiveChoice(Campaign data)
        {
            foreach(int ending in new[]{0,1,2})
            {
                var g=Fresh(data);g.save.stage=1;g.Enter(2);g.enemies.Clear();
                g.save.flags.Add("scout_saved");
                g.save.flags.Add("floodarchive_1");
                if(ending==0)g.save.flags.Add("floodarchive_2");
                if(ending==1)g.save.flags.Add("floodarchive_public");
                if(ending==2)g.save.flags.Add("floodarchive_chart");
                WorldObject ferryman=Object(g,"wayfarer2");
                g.save.x=ferryman.x;g.save.y=ferryman.y;
                int coins=g.save.coins;
                g.Talk(ferryman);
                Check(g.conversation!=null&&g.mode=="dialog"&&
                    (ending==0?g.conversation.text.Contains("公共议事台"):
                    ending==1?g.conversation.text.Contains("真实姓名"):
                              g.conversation.text.Contains("原始水图")),
                    "The existing actual ferry NPC acknowledges precisely the documented progress or branch");
                Check(g.save.coins==coins,
                    "Local reactions are actual dialogue with no duplicate money or rewards");
                g.Close();
                g.save.english=true;
                g.Talk(ferryman);
                Check(g.conversation!=null&&
                    (ending==0?g.conversation.textEn.Contains("public desk"):
                    ending==1?g.conversation.textEn.Contains("ferry register"):
                              g.conversation.textEn.Contains("flood charts")),
                    "The same story consequence is authored in real English too");
                g.Close();
            }
        }

        public static void RestoredPublicFerryHasRealSafetyAndLowTideGates(Campaign data)
        {
            var g=Fresh(data);g.save.stage=1;g.Enter(2);g.enemies.Clear();
            g.save.flags.Add("scout_saved");
            g.save.flags.Add("floodarchive_1");g.save.flags.Add("floodarchive_2");
            g.save.flags.Add("floodarchive_public");
            WorldObject west=FerryRecord(g,1),east=FerryRecord(g,3);
            g.save.x=west.x;g.save.y=west.y;g.save.totalTime=24;g.save.energy=100;
            g.Talk(west);Check(!FerryChoice(g,"floodarchive_ferry"),
                "No public ferry choice is presented at high tide");g.Close();
            int revision=g.revision;
            g.save.totalTime=36;g.Talk(west);
            Check(FerryChoice(g,"floodarchive_ferry")&&
                (g.Position-new Point(east.x,east.y)).Length<.01f&&
                g.save.energy==86&&g.revision>revision,
                "A genuine low-tide crossing moves the original hero to the opposite bank and charges once");
            g.save.totalTime=24;g.Talk(east);
            Check(!FerryChoice(g,"floodarchive_ferry"),
                "High tide blocks the return trip even on a completed route");g.Close();
            g.save.totalTime=36;g.save.energy=13;g.Talk(east);
            Check(FerryChoice(g,"floodarchive_ferry")&&g.save.energy==13&&
                (g.Position-new Point(east.x,east.y)).Length<.01f,
                "Insufficient stamina prevents crossing, teleport and resource loss");
            g.save.energy=100;
            var guard=new Enemy(90880,0,west.x+.8f,west.y,120){timer=99};
            g.enemies.Add(guard);
            g.Talk(east);Check(FerryChoice(g,"floodarchive_ferry")&&
                g.save.energy==100&&g.save.x==east.x,
                "A hostile enemy at the destination blocks the physical crossing");
            g.enemies.Clear();
            g.projectiles.Add(new Projectile{position=new Point(west.x,west.y),damage=10,life=2,source="shot"});
            g.Talk(east);Check(FerryChoice(g,"floodarchive_ferry")&&
                g.save.energy==100&&g.save.x==east.x,
                "Active hostile shots at the landing cannot vanish through ferry travel");
            g.projectiles.Clear();
            g.hazards.Add(new Hazard{position=new Point(west.x,west.y),damage=14,timer=.3f,radius=2});
            g.Talk(east);Check(FerryChoice(g,"floodarchive_ferry")&&
                g.save.energy==100&&g.save.x==east.x,
                "An incoming explosion at the landing blocks ferry traversal");
            g.hazards.Clear();
            g.Talk(east);
            Check(FerryChoice(g,"floodarchive_ferry")&&
                g.save.energy==86&&(g.Position-new Point(west.x,west.y)).Length<.01f,
                "With both banks safe, the route works in both directions");
            g.PrepareSave();var restored=new GameCore(data,CloneSave(g.save));
            Check(restored.FloodArchiveComplete&&restored.save.version==2,
                "Unlocked public ferry persists without altering SaveData v2");
        }

        public static void DrownedFloodArchiveRequiresRealTidesAndChoice(Campaign data)
        {
            var novice=Fresh(data);novice.save.flags.Add("scout_saved");
            Check(novice.GetQuest("flood_archive").locked,
                "Rescuing the scout cannot spoil a still-locked Drowned Ruins chapter");
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
                g.Talk(third);
                foreach(Choice option in g.conversation.choices)
                    Check(option.action!="floodarchive_public"&&option.action!="floodarchive_chart",
                        "A completed public decision can never be chosen again or yield extra rewards");
                // The earned low-tide ferry is allowed as a distinct,
                // non-reward crossing after final publication.
                g.Close();
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
