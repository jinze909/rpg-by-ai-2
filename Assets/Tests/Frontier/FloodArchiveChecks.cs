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
        public static void PublicFerryEmergencyRunRequiresRealCrossingAndBothRaiderKills(Campaign data)
        {
            var g=Fresh(data);g.save.stage=3;g.save.level=18;
            g.Enter(2);g.enemies.Clear();g.invulnerable=400;
            g.save.flags.Add("scout_saved");g.save.flags.Add("floodarchive_public");
            WorldObject west=FerryRecord(g,1),east=FerryRecord(g,3);
            g.save.totalTime=36;g.save.x=west.x;g.save.y=west.y;g.save.energy=100;
            int cash=g.save.coins,shards=g.save.shards,kills=g.save.kills[2];
            g.Talk(west);
            Check(FerryChoice(g,"floodarchive_relief_start")&&
                g.FloodReliefActive&&g.FloodReliefTime==65&&
                g.FloodReliefIntegrity==100&&g.save.trackedQuest=="ferry_relief"&&
                g.GetQuest("ferry_relief").steps.Count==3,
                "The original western archive dock launches a tracked three-stage tide medicine mission");
            g.Talk(east);g.Close();
            var choose=typeof(GameCore).GetMethod("ChooseFloodArchive",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            choose.Invoke(g,new object[]{"floodarchive_relief_deliver"});
            Check(!g.FloodReliefComplete&&g.FloodReliefActive,
                "Remote delivery cannot forge the physically carried parcel");
            g.Talk(west);
            Check(FerryChoice(g,"floodarchive_ferry")&&
                g.FloodReliefCrossed&&g.FloodReliefRaiders==2&&
                (g.Position-new Point(east.x,east.y)).Length<.01f,
                "Actual existing low-tide ferry transports medicine and deploys two distinct enemies");
            var hit=typeof(GameCore).GetMethod("Hit",
                System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.NonPublic);
            Check(hit!=null,"The game has an authoritative lethal-hit callback");
            var a=g.enemies.Find(e=>e.id==92340);
            var b=g.enemies.Find(e=>e.id==92341);
            Check(a!=null&&b!=null&&g.CanStand(a.Position)&&g.CanStand(b.Position)&&
                a.kind==0&&b.kind==1&&g.FloodReliefDefeated==0,
                "The actual east-bank battle deploys distinct melee and bow combat profiles");
            choose.Invoke(g,new object[]{"floodarchive_relief_deliver"});
            Check(!g.FloodReliefComplete&&g.save.coins==cash,
                "Ferry arrival alone does not grant the combat-and-delivery reward");
            hit.Invoke(g,new object[]{a,99999f,false,null,null});
            Check(g.FloodReliefActive&&g.FloodReliefRaiders==1&&
                g.FloodReliefDefeated==1&&!g.FloodReliefSecured&&
                g.GetQuest("ferry_relief").next.Contains("1/2"),
                "One genuine kill updates the playable quest compass but cannot publish delivery");
            hit.Invoke(g,new object[]{b,99999f,false,null,null});
            g.hitStop=0;g.Tick(.05f,new Point());
            Check(g.FloodReliefActive&&g.FloodReliefRaiders==0&&
                g.FloodReliefSecured&&g.FloodReliefDefeated==2&&
                g.GetQuest("ferry_relief").next.Contains("交付")&&
                g.save.coins==cash&&g.save.shards==shards&&
                g.save.kills[2]==kills,
                "Two genuine quest kills preserve the parcel without farmable normal loot or bounty kills");
            g.Talk(east);
            Check(FerryChoice(g,"floodarchive_relief_deliver")&&
                g.FloodReliefComplete&&!g.FloodReliefActive&&
                g.save.coins==cash+45&&g.save.shards==shards+1&&
                g.GetQuest("ferry_relief").complete,
                "The actual eastern dock pays only the first earned civic reward and completes the tracked objective");
            choose.Invoke(g,new object[]{"floodarchive_relief_deliver"});
            Check(g.save.coins==cash+45,"Forged or repeated action cannot farm relief rewards");
            g.PrepareSave();var restored=new GameCore(data,CloneSave(g.save));
            Check(restored.save.version==2&&restored.FloodReliefComplete&&
                !restored.FloodReliefActive,
                "Existing version-two slot persists delivered medicine without temporary raider state");
        }

        public static void PublicFerryReliefDoesNotRewardDisappearingRaidersOrExpiredCargo(Campaign data)
        {
            var g=Fresh(data);g.save.stage=3;g.Enter(2);g.enemies.Clear();
            g.save.flags.Add("scout_saved");g.save.flags.Add("floodarchive_public");
            WorldObject west=FerryRecord(g,1);
            g.save.totalTime=36;g.save.x=west.x;g.save.y=west.y;g.save.energy=100;
            g.Talk(west);Check(FerryChoice(g,"floodarchive_relief_start"),
                "A normal medicine run is available after original archive completion");
            g.Talk(west);Check(FerryChoice(g,"floodarchive_ferry")&&g.FloodReliefRaiders==2,
                "Two real attackers intercept the delivered parcel");
            g.enemies.Remove(g.enemies.Find(e=>e.id==92340));
            g.Tick(.05f,new Point());
            Check(!g.FloodReliefActive&&!g.FloodReliefComplete&&
                g.FloodReliefRaiders==0,
                "An unconfirmed disappearing attacker aborts the attempt and cleans up its own roster");
            g.save.x=west.x;g.save.y=west.y;
            g.Talk(west);Check(FerryChoice(g,"floodarchive_relief_start"),
                "A failed combat crossing may be picked up again without destroying public archive");
            for(int i=0;i<1500&&g.FloodReliefActive;i++)g.Tick(.05f,new Point());
            Check(!g.FloodReliefActive&&!g.FloodReliefComplete,
                "A full 65-second run timeout never mints a fake emergency delivery");
            g.PrepareSave();var loaded=new GameCore(data,CloneSave(g.save));
            Check(loaded.save.version==2&&loaded.FloodArchiveComplete&&
                !loaded.FloodReliefActive&&!loaded.FloodReliefComplete,
                "Failed medicine runs are transient and do not alter old completed archive saves");
        }

        public static void PublicFerryActiveCrossingResetsOnOriginalSlotReload(Campaign data)
        {
            var g=Fresh(data);g.save.stage=3;g.Enter(2);g.enemies.Clear();
            g.save.flags.Add("scout_saved");g.save.flags.Add("floodarchive_public");
            WorldObject west=FerryRecord(g,1);
            g.save.x=west.x;g.save.y=west.y;g.save.totalTime=36;g.save.energy=100;
            g.Talk(west);
            Check(FerryChoice(g,"floodarchive_relief_start")&&
                g.FloodReliefActive&&g.save.trackedQuest=="ferry_relief",
                "Unfinished medical crossing has a real tracked objective before saving");
            g.PrepareSave();
            var saved=CloneSave(g.save);
            g.Load(saved);g.Start();
            Check(!g.FloodReliefActive&&!g.FloodReliefCrossed&&
                g.FloodReliefRaiders==0&&g.save.version==2&&
                g.save.trackedQuest=="ferry_relief"&&
                !g.GetQuest("ferry_relief").complete,
                "Reloading an existing slot retires temporary cargo, enemies and countdown but keeps the selected mission journal");
            g.enemies.Clear();g.save.totalTime=36;g.save.x=west.x;g.save.y=west.y;
            g.Talk(west);
            Check(FerryChoice(g,"floodarchive_relief_start")&&g.FloodReliefActive,
                "After reload the archived public dock offers a legitimate fresh medicine attempt");
        }

        public static void PublicFerryMedicineSupportsOneActualHerbalSealRepair(Campaign data)
        {
            var g=Fresh(data);g.save.stage=3;g.Enter(2);g.enemies.Clear();
            g.save.flags.Add("scout_saved");g.save.flags.Add("floodarchive_public");
            WorldObject west=FerryRecord(g,1),east=FerryRecord(g,3);
            g.save.totalTime=36;g.save.x=west.x;g.save.y=west.y;
            g.save.energy=100;g.save.inventory[2]=0;
            g.Talk(west);Check(FerryChoice(g,"floodarchive_relief_start")&&g.FloodReliefActive,
                "Normal public ferry launch is available before optional repair");
            g.Talk(west);Check(FerryChoice(g,"floodarchive_ferry")&&g.FloodReliefCrossed,
                "The genuine ferry reaches the physical east-bank medical post");
            var hurt=typeof(GameCore).GetMethod("Hurt",
                System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.NonPublic);
            Check(hurt!=null,"The real post-armor hit handler is available");
            g.invulnerable=0;
            hurt.Invoke(g,new object[]{30f,"shot",null});
            float damaged=g.FloodReliefIntegrity,time=g.FloodReliefTime;
            Check(damaged<100&&damaged>0&&!g.FloodReliefRepairAvailable,
                "A real hostile hit harms the actual medical parcel, but no herbs are owned");
            g.Talk(east);
            Check(FerryChoice(g,"floodarchive_relief_patch")&&
                !g.FloodReliefRepairAvailable&&g.FloodReliefIntegrity==damaged,
                "A missing herb cannot mint free medicine, restore the timer or fabricate a repair");
            g.save.inventory[2]=2;
            g.Talk(east);Check(FerryChoice(g,"floodarchive_relief_patch"),
                "The east-bank medics offer a physically located one-herb repair");
            Check(g.FloodReliefActive&&
                Math.Abs(g.FloodReliefIntegrity-Math.Min(100,damaged+40))<.01f&&
                g.save.inventory[2]==1&&
                g.FloodReliefTime==time&&!g.FloodReliefRepairAvailable&&
                g.FloodReliefRaiders==2,
                "A one-time actual herb patch restores only parcel health, never the clock or real pursuers");
            var choose=typeof(GameCore).GetMethod("ChooseFloodArchive",
                System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.NonPublic);
            choose.Invoke(g,new object[]{"floodarchive_relief_patch"});
            Check(g.save.inventory[2]==1&&g.FloodReliefActive,
                "Direct repeated action requests cannot drain a second herb or stack repairs");
            g.PrepareSave();var restore=new GameCore(data,CloneSave(g.save));
            Check(restore.save.version==2&&!restore.FloodReliefActive&&
                !restore.FloodReliefRepairAvailable&&restore.save.inventory[2]==1,
                "The actual material spent remains in legacy v2 inventory, while temporary repairs are not saved");
        }

        public static void SecuredPublicFerryDiscountPreservesBothLandingsAndSafety(Campaign data)
        {
            var g=Fresh(data);g.save.stage=3;g.Enter(2);g.enemies.Clear();
            g.save.flags.Add("scout_saved");g.save.flags.Add("floodarchive_public");
            WorldObject west=FerryRecord(g,1),east=FerryRecord(g,3);
            g.save.x=west.x;g.save.y=west.y;g.save.totalTime=36;g.save.energy=11;
            Check(g.PublicFerryEnergyCost==14,
                "Restored original ferry retains its 14-energy price before confirmed relief");
            g.Talk(west);Check(FerryChoice(g,"floodarchive_ferry")&&
                (g.Position-new Point(west.x,west.y)).Length<.01f&&g.save.energy==11,
                "Unsecured public crossing remains blocked below its original cost");
            g.save.flags.Add("floodarchive_relief");
            Check(g.PublicFerryEnergyCost==10,
                "The actual completed relief permanently reduces ferry energy cost");
            g.Talk(west);Check(FerryChoice(g,"floodarchive_ferry")&&
                (g.Position-new Point(east.x,east.y)).Length<.01f&&g.save.energy==1,
                "New cost unlocks a genuinely cheaper crossing through the original dock");
            g.save.energy=10;
            var hazard=new Hazard{position=new Point(west.x,west.y),
                radius=2f,timer=5f,damage=12,kind="rift"};
            g.hazards.Add(hazard);
            g.Talk(east);Check(FerryChoice(g,"floodarchive_ferry")&&
                (g.Position-new Point(east.x,east.y)).Length<.01f&&g.save.energy==10,
                "Discount cannot bypass a true delayed impact on the far shore");
            g.hazards.Remove(hazard);
            g.Talk(east);Check(FerryChoice(g,"floodarchive_ferry")&&
                (g.Position-new Point(west.x,west.y)).Length<.01f&&g.save.energy==0,
                "With a safe landing the same permanent fare applies in both directions");
            g.PrepareSave();var loaded=new GameCore(data,CloneSave(g.save));
            Check(loaded.save.version==2&&loaded.FloodReliefComplete&&
                loaded.PublicFerryEnergyCost==10,
                "Older SaveData v2 slots inherit the earned fare without any new saved fields");
        }

        public static void FailedMedicineRunRetiresOnlyItsOwnLiveProjectilesAndWarnings(Campaign data)
        {
            var g=Fresh(data);g.save.stage=3;g.Enter(2);g.enemies.Clear();
            g.save.flags.Add("scout_saved");g.save.flags.Add("floodarchive_public");
            WorldObject west=FerryRecord(g,1),east=FerryRecord(g,3);
            g.save.x=west.x;g.save.y=west.y;g.save.energy=100;g.save.totalTime=36;
            g.Talk(west);Check(FerryChoice(g,"floodarchive_relief_start"),
                "A real medicine mission begins at the original western dock");
            g.Talk(west);Check(FerryChoice(g,"floodarchive_ferry")&&
                g.FloodReliefRaiders==2,
                "Two actually spawned raiders own this one escort's combat");
            Enemy attacker=g.enemies.Find(e=>e.id==92341);
            Check(attacker!=null,"An authored ranged mission attacker exists");
            var fire=typeof(GameCore).GetMethod("Fire",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            var tag=typeof(GameCore).GetMethod("TagEnemyCombatEffect",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            Check(fire!=null&&tag!=null,
                "Original projectile and telegraph constructors remain available");
            fire.Invoke(g,new object[]{attacker,new Point(-1,0),6f});
            var owned=g.projectiles.Find(p=>p.floodReliefOwned);
            Check(owned!=null&&!owned.friendly&&owned.life>0,
                "Genuine archer-created enemy projectile carries the escort's ownership");
            var warning=new Effect("danger",attacker.Position,1.4f){radius=1.2f};
            tag.Invoke(g,new object[]{warning,attacker,false});
            g.effects.Add(warning);
            Check(warning.floodReliefOwned,
                "A real enemy telegraph belongs to the same transport encounter");
            var otherShot=new Projectile{position=new Point(east.x,east.y),
                velocity=new Point(1,0),damage=9,life=10,source="shot"};
            var otherWarning=new Effect("danger",new Point(east.x+3,east.y),3f);
            g.projectiles.Add(otherShot);g.effects.Add(otherWarning);
            // Force only the mission's first actor to leave without a lethal
            // Hit callback, exercising the genuine automatic failure path.
            g.enemies.Remove(g.enemies.Find(e=>e.id==92340));
            g.Tick(.01f,new Point());
            Check(!g.FloodReliefActive&&g.FloodReliefRaiders==0&&
                !g.projectiles.Exists(p=>p.floodReliefOwned)&&
                !g.effects.Exists(e=>e.floodReliefOwned),
                "A missing raider aborts the transport and retires all owned arrows and warnings");
            Check(g.projectiles.Contains(otherShot)&&g.effects.Contains(otherWarning),
                "Independent world projectiles and effects cannot be erased by the failed rescue");
            g.PrepareSave();var loaded=new GameCore(data,CloneSave(g.save));
            Check(loaded.save.version==2&&!loaded.FloodReliefActive&&
                !loaded.FloodReliefComplete,
                "Encounter ownership and failed escorts remain transient across legacy slot reload");
        }

    }
}
