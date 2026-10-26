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

        public static void LivingFerrymanAcknowledgesRealMedicineBattleAndReceipt(Campaign data)
        {
            var g=Fresh(data);g.save.stage=3;g.Enter(2);g.enemies.Clear();
            g.invulnerable=400f;
            g.save.flags.Add("scout_saved");
            g.save.flags.Add("floodarchive_public");
            g.save.flags.Add("wayfarer_2");
            WorldObject west=FerryRecord(g,1),east=FerryRecord(g,3);
            WorldObject ferryman=Object(g,"wayfarer2");
            g.save.totalTime=36;g.save.energy=100;
            g.save.x=west.x;g.save.y=west.y;
            g.Talk(west);
            Check(FerryChoice(g,"floodarchive_relief_start"),
                "The real west-bank scene starts the civilian cargo crossing");
            g.save.x=ferryman.x;g.save.y=ferryman.y;
            g.Talk(ferryman);
            Check(g.mode=="dialog"&&g.conversation!=null&&
                g.conversation.text.Contains("封条碰水")&&
                g.conversation.textEn.Contains("seal out of the river"),
                "The existing physical ferryman talks about the actual carried parcel before boarding");
            g.Close();g.save.x=west.x;g.save.y=west.y;
            g.Talk(west);
            Check(FerryChoice(g,"floodarchive_ferry")&&g.FloodReliefRaiders==2,
                "The genuine ferry commits its enemy interception before the next reaction");
            g.save.x=ferryman.x;g.save.y=ferryman.y;
            g.Talk(ferryman);
            Check(g.conversation!=null&&
                g.conversation.text.Contains("两个劫路")&&
                g.conversation.textEn.Contains("Two raiders"),
                "During the live fight the ferryman names the actual still-living attackers");
            g.Close();
            var hit=typeof(GameCore).GetMethod("Hurt",
                System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.NonPublic);
            Check(hit!=null,"The real post-armor combat handler remains active");
            g.invulnerable=0;g.save.health=g.save.maxHealth;
            hit.Invoke(g,new object[]{35f,"shot",null});
            g.invulnerable=0;
            hit.Invoke(g,new object[]{35f,"shot",null});
            Check(g.FloodReliefIntegrity<=30&&g.FloodReliefIntegrity>0,
                "Two authentic hostile hits cause critical but salvageable seal damage");
            g.Talk(ferryman);
            Check(g.conversation!=null&&
                g.conversation.text.Contains("一份草药")&&
                g.conversation.textEn.Contains("one herb"),
                "With real damage the ferryman offers accurate one-herb rescue guidance");
            g.Close();
            var lethal=typeof(GameCore).GetMethod("Hit",
                System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.NonPublic);
            Check(lethal!=null,"The real raider kill handler is available");
            Enemy melee=g.enemies.Find(e=>e.id==92340);
            Enemy bow=g.enemies.Find(e=>e.id==92341);
            Check(melee!=null&&bow!=null,"Both encounter-owned enemies still live");
            lethal.Invoke(g,new object[]{melee,99999f,false,null,null});
            lethal.Invoke(g,new object[]{bow,99999f,false,null,null});
            g.Talk(ferryman);
            Check(g.FloodReliefSecured&&g.conversation!=null&&
                g.conversation.text.Contains("接收台")&&
                g.conversation.textEn.Contains("receiving desk"),
                "Only two genuine kills change the NPC's instruction from fighting to final handoff");
            g.Close();g.save.x=east.x;g.save.y=east.y;
            g.Talk(east);
            Check(FerryChoice(g,"floodarchive_relief_deliver")&&g.FloodReliefComplete,
                "The existing eastern desk physically receives the secured medicine");
            g.save.x=ferryman.x;g.save.y=ferryman.y;
            g.Talk(ferryman);
            Check(g.conversation!=null&&
                g.conversation.text.Contains("渡口名册")&&
                g.conversation.textEn.Contains("ferry register"),
                "After an actual delivery the same identifiable NPC acknowledges the named families");
            g.PrepareSave();var loaded=new GameCore(data,CloneSave(g.save));
            Check(loaded.save.version==2&&loaded.FloodReliefComplete,
                "Reactive NPC voice neither invents reward flags nor changes original v2 persistence");
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
        public static void FerryAmbushRejectsIsolatedGroundAndOccupiedCompanion(Campaign data)
        {
            MapData original=data.maps[2];
            try
            {
                var rows=new string[45];
                for(int y=0;y<45;y++)
                {
                    char[] cells=new string('#',45).ToCharArray();
                    if(y>=2&&y<=42)
                        for(int x=2;x<=42;x++)cells[x]='.';
                    rows[y]=new string(cells);
                }
                // The first candidate in the old radius-three sweep is a
                // valid standable island, but its surrounding full moat
                // makes a complete body-foot route impossible.
                for(int y=16;y<=18;y++)
                {
                    char[] cells=rows[y].ToCharArray();
                    for(int x=16;x<=18;x++)
                        if(x!=17||y!=17)cells[x]='#';
                    rows[y]=new string(cells);
                }
                data.maps[2]=new MapData{id=2,spawnX=20.5f,
                    spawnY=20.5f,rows=rows,
                    objects=new WorldObject[0],decor=new Decoration[0]};
                var g=Fresh(data);g.save.stage=3;g.Enter(2);
                g.enemies.Clear();g.save.x=20.5f;g.save.y=20.5f;
                g.save.flags.Add("companion_1");
                g.SelectCompanion(1);
                g.companionPosition=new Point(17.5f,20.5f);
                var bystander=new Enemy(91923,0,20.5f,17.5f,300f)
                    {stun=100f,timer=1000f};
                g.enemies.Add(bystander);
                var plan=typeof(GameCore).GetMethod(
                    "TryFloodReliefRaiderSpots",
                    System.Reflection.BindingFlags.NonPublic|
                    System.Reflection.BindingFlags.Instance);
                Check(plan!=null&&g.CanStand(new Point(17.5f,17.5f))&&
                    g.CanStand(g.Position),
                    "The live ferry placement code sees a legal isolated pocket and a normal hero landing");
                var args=new object[]{g.Position,null};
                Check((bool)plan.Invoke(g,args),
                    "A nearby genuine two-raider encounter can still be placed despite one inaccessible pocket");
                var feet=(Point[])args[1];
                Check(feet!=null&&feet.Length==2,
                    "Preflight selects two concrete real landing combat feet atomically");
                foreach(Point foot in feet)
                {
                    Check(g.CanStand(foot)&&
                        (foot-new Point(17.5f,17.5f)).Length>1.1f&&
                        (foot-g.Position).Length>=3f&&
                        (foot-g.companionPosition).Length>=1.4f&&
                        (foot-bystander.Position).Length>=2.2f,
                        "Raider cannot appear in isolated ground or overlap the hero, companion or living foe");
                }
                Check((feet[0]-feet[1]).Length>=3f&&
                    g.enemies.Count==1&&!g.Has("floodarchive_relief"),
                    "Uncommitted placement does not create a half-ambush or mint civilian rescue flags");
            }
            finally{data.maps[2]=original;}
        }

        public static void UnavailableFerryAmbushNeverSpendsStaminaOrLosesParcel(Campaign data)
        {
            var g=Fresh(data);g.save.stage=3;g.Enter(2);
            g.enemies.Clear();g.invulnerable=180;
            g.save.flags.Add("scout_saved");
            g.save.flags.Add("floodarchive_public");
            WorldObject west=FerryRecord(g,1),east=FerryRecord(g,3);
            g.save.totalTime=36;g.save.x=west.x;g.save.y=west.y;
            g.save.energy=100;
            g.Talk(west);
            Check(FerryChoice(g,"floodarchive_relief_start")&&
                g.FloodReliefActive&&!g.FloodReliefCrossed,
                "A genuine archived medicine parcel begins at the physical west-bank ferry");
            int coins=g.save.coins;
            MapData original=data.maps[2];
            try
            {
                // Leave exactly the valid landing foot while closing every
                // potential ambush foothold within the maximum local ring.
                // An ordinary ferry can still land safely here.
                var rows=(string[])original.rows.Clone();
                int cx=(int)Math.Floor(east.x);
                int cy=(int)Math.Floor(east.y);
                for(int y=cy-9;y<=cy+9;y++)
                {
                    char[] tiles=rows[y].ToCharArray();
                    for(int x=cx-9;x<=cx+9;x++)
                        if(x!=cx||y!=cy)tiles[x]='#';
                    rows[y]=new string(tiles);
                }
                data.maps[2]=new MapData {
                    id=2,spawnX=original.spawnX,spawnY=original.spawnY,
                    rows=rows,objects=original.objects,
                    decor=original.decor};
                Check(g.CanStand(new Point(east.x,east.y))&&
                    g.CanStand(g.Position),
                    "Both physical ferry landing tiles remain standable even though the east ambush area is sealed");
                g.Talk(west);
                Check(FerryChoice(g,"floodarchive_ferry"),
                    "The original west dock still exposes the allowed low-tide ferry choice");
                Check((g.Position-new Point(west.x,west.y)).Length<.001f&&
                    g.save.energy==100&&g.FloodReliefActive&&
                    !g.FloodReliefCrossed&&g.FloodReliefTime==65&&
                    g.FloodReliefRaiders==0&&g.save.coins==coins,
                    "A blocked east-bank ambush cannot charge stamina, teleport or erase the original medicine run");
            }
            finally {data.maps[2]=original;}
            // Repair the actual east bank and take the same advertised
            // ferry again, without reaccepting or farming the 65s parcel.
            g.Talk(west);
            Check(FerryChoice(g,"floodarchive_ferry")&&
                (g.Position-new Point(east.x,east.y)).Length<.001f&&
                g.save.energy==86&&g.FloodReliefCrossed&&
                g.FloodReliefRaiders==2&&g.FloodReliefTime==65&&
                !g.FloodReliefComplete&&g.save.version==2,
                "Reopened shoreline commits exactly one full-cost crossing and two real raiders while retaining the active run");
        }


        public static void FloodArchiveProofAndCivicVoteRejectStaleBlockedDialogues(Campaign data)
        {
            MapData original=data.maps[2];
            try
            {
                var rows=new string[32];
                for(int y=0;y<32;y++)
                {
                    char[] tiles=new string('#',32).ToCharArray();
                    if(y>=2&&y<=29)
                        for(int x=2;x<=29;x++)tiles[x]='.';
                    rows[y]=new string(tiles);
                }
                WorldObject gauge=new WorldObject{id="floodarchive_1",
                    kind="floodarchive",name="Gauge",en="Gauge",x=10.5f,y=10.5f};
                WorldObject roster=new WorldObject{id="floodarchive_2",
                    kind="floodarchive",name="Roster",en="Roster",x=16.5f,y=10.5f};
                WorldObject desk=new WorldObject{id="floodarchive_3",
                    kind="floodarchive",name="Desk",en="Desk",x=21.5f,y=10.5f};
                data.maps[2]=new MapData{id=2,spawnX=12.25f,
                    spawnY=10.5f,rows=rows,
                    objects=new[]{gauge,roster,desk},decor=new Decoration[0]};
                var g=Fresh(data);g.save.stage=3;g.Enter(2);
                g.enemies.Clear();g.save.flags.Add("scout_saved");
                g.save.totalTime=24;g.save.x=12.25f;g.save.y=10.5f;
                g.Talk(gauge);
                Check(g.mode=="dialog"&&g.conversation!=null&&
                    g.conversation.choices[0].action=="floodarchive_1",
                    "The original high-tide gauge is reachable before a terrain closure");
                char[] closed=rows[10].ToCharArray();
                closed[11]='#';rows[10]=new string(closed);
                Check(g.Blocked(11.5f,10.5f),
                    "A newly closed physical cell now isolates the archive gauge");
                int oldXp=g.save.xp;
                Check(FerryChoice(g,"floodarchive_1")&&
                    g.FloodArchiveProgress==0&&g.save.xp==oldXp,
                    "Previously valid high-water proof cannot be committed through a newly closed wall");
                g.Close();g.Talk(gauge);
                Check(g.mode!="dialog"||g.conversation==null||
                    !Array.Exists(g.conversation.choices,c=>c.action=="floodarchive_1"),
                    "A sealed post no longer initiates a remote civic evidence dialogue");
                closed[11]='.';rows[10]=new string(closed);
                g.Close();g.Talk(gauge);
                Check(FerryChoice(g,"floodarchive_1")&&
                    g.FloodArchiveProgress==1&&g.save.xp==oldXp+12,
                    "Reopening the actual walkable lane restores legitimate high-tide evidence");
                g.save.flags.Add("floodarchive_2");
                g.save.x=23.25f;g.save.y=10.5f;
                g.Talk(desk);
                Check(g.mode=="dialog"&&g.conversation!=null&&
                    Array.Exists(g.conversation.choices,c=>c.action=="floodarchive_public"),
                    "Both records unlock the real reachable public-hearing choice");
                closed=rows[10].ToCharArray();
                closed[22]='#';rows[10]=new string(closed);
                int oldCoins=g.save.coins;
                Check(FerryChoice(g,"floodarchive_public")&&
                    !g.FloodArchiveComplete&&g.save.coins==oldCoins,
                    "A stale public-vote button cannot publish flags or pay coins across newly sealed ground");
                closed[22]='.';rows[10]=new string(closed);
                g.Close();g.Talk(desk);
                Check(FerryChoice(g,"floodarchive_public")&&
                    g.FloodArchiveComplete&&g.save.coins==oldCoins+50&&
                    g.save.version==2,
                    "The restored route safely commits the original civic outcome once on old v2 saves");
            }
            finally{data.maps[2]=original;}
        }

        public static void PublicFerryStaleDockChoiceCannotCrossClosingPhysicalWall(Campaign data)
        {
            MapData original=data.maps[2];
            try
            {
                var rows=new string[32];
                for(int y=0;y<32;y++)
                {
                    char[] cells=new string('#',32).ToCharArray();
                    if(y>=2&&y<=29)
                        for(int x=2;x<=29;x++)cells[x]='.';
                    rows[y]=new string(cells);
                }
                WorldObject west=new WorldObject{id="floodarchive_1",
                    kind="floodarchive",name="West ferry dock",
                    en="West ferry dock",x=10.5f,y=10.5f};
                WorldObject east=new WorldObject{id="floodarchive_3",
                    kind="floodarchive",name="East ferry dock",
                    en="East ferry dock",x=21.5f,y=10.5f};
                data.maps[2]=new MapData{id=2,spawnX=12.25f,
                    spawnY=10.5f,rows=rows,
                    objects=new[]{west,east},decor=new Decoration[0]};
                var g=Fresh(data);g.save.stage=3;g.Enter(2);
                g.enemies.Clear();
                g.save.flags.Add("scout_saved");
                g.save.flags.Add("floodarchive_public");
                g.save.totalTime=36;g.save.energy=100;
                g.save.x=12.25f;g.save.y=10.5f;
                Check(g.CanStand(g.Position)&&
                    (g.Position-new Point(west.x,west.y)).Length<1.8f,
                    "The real hero starts close enough to select the western ferry");
                g.Talk(west);
                Check(g.conversation!=null&&g.mode=="dialog",
                    "The completed archive presents the genuine ferry transaction");
                // Open the dialogue on clear ground, then close the barrier
                // before submitting its formerly valid cached choice.
                char[] closed=rows[10].ToCharArray();
                closed[11]='#';rows[10]=new string(closed);
                Check(g.CanStand(g.Position)&&
                    g.CanStand(new Point(west.x,west.y))&&
                    g.Blocked(11.5f,10.5f),
                    "A full dynamic wall separates two standing legal actors");
                Check(FerryChoice(g,"floodarchive_ferry")&&
                    g.save.energy==100&&
                    (g.Position-new Point(12.25f,10.5f)).Length<.001f,
                    "A cached ferry choice cannot charge energy or teleport through the newly closed dock wall");
                closed[11]='.';rows[10]=new string(closed);
                g.Talk(west);
                Check(FerryChoice(g,"floodarchive_ferry")&&
                    (g.Position-new Point(east.x,east.y)).Length<.001f&&
                    g.save.energy==86&&g.save.version==2,
                    "Reopening that wall restores real public ferry movement and a single stamina charge");
            }
            finally{data.maps[2]=original;}
        }



        public static void PublicFerryAndCivicGaugeShareTrueHeldTideCountdown(Campaign data)
        {
            var g=Fresh(data);g.save.stage=3;g.Enter(2);g.enemies.Clear();
            g.save.flags.Add("scout_saved");g.save.flags.Add("floodarchive_public");
            WorldObject west=FerryRecord(g,1);
            g.save.x=west.x;g.save.y=west.y;
            g.save.totalTime=21;
            Check(g.LowTide&&Math.Abs(g.PublicFerryPhaseSeconds-1)<.001f&&
                Math.Abs(g.FloodArchiveWait(1)-1)<.001f,
                "One genuine second remains before the natural high-water gauge appears");
            g.save.totalTime=22;
            Check(!g.LowTide&&Math.Abs(g.PublicFerryPhaseSeconds-14)<.001f&&
                Math.Abs(g.FloodArchiveWait(2)-14)<.001f,
                "Both public crossing and submerged roster use the real fourteen-second high-tide wait");
            g.Talk(west);
            Check(g.conversation!=null&&
                g.conversation.text.Contains("14")&&
                g.conversation.textEn.Contains("14"),
                "The original bilingual public-dock dialogue publishes its real high-water wait");
            g.Close();
            g.save.totalTime=25;
            g.save.tideHeldUntil=50;
            Check(g.LowTide&&Math.Abs(g.PublicFerryPhaseSeconds-33)<.001f&&
                Math.Abs(g.FloodArchiveWait(1)-33)<.001f,
                "A sluice hold ending in natural low tide must include the eight further low-water seconds");
            g.Talk(west);
            Check(g.conversation!=null&&
                g.conversation.text.Contains("33")&&
                g.conversation.textEn.Contains("33"),
                "The same true extended tide window appears in active ferry conversation");
            g.Close();
            g.save.totalTime=43;
            Check(g.LowTide&&Math.Abs(g.PublicFerryPhaseSeconds-15)<.001f,
                "The held ebb ticks continuously across a natural high-to-low boundary");
            g.save.totalTime=50;
            Check(g.LowTide&&Math.Abs(g.PublicFerryPhaseSeconds-8)<.001f,
                "Releasing the sluice into natural low water does not fabricate an immediate high tide");
            g.save.totalTime=58;
            Check(!g.LowTide&&Math.Abs(g.PublicFerryPhaseSeconds-14)<.001f,
                "The exact natural flood boundary reopens the original fourteen-second high-water interval");
            g.save.totalTime=25;g.save.tideHeldUntil=31;
            Check(g.LowTide&&Math.Abs(g.PublicFerryPhaseSeconds-6)<.001f&&
                Math.Abs(g.FloodArchiveWait(1)-6)<.001f,
                "A hold ending during genuinely high natural water ends low tide at the actual release");
            g.save.totalTime=31;
            Check(!g.LowTide&&Math.Abs(g.PublicFerryPhaseSeconds-5)<.001f,
                "After release, high water lasts only until the next physical natural ebb");
            Check(g.save.version==2,
                "The entire accurate tide model reuses the existing v2 totalTime/tideHeldUntil fields");
        }


        public static void PublicFerryRefundsOnlyUnreleasedCompanionOrders(Campaign data)
        {
            var g=Fresh(data);g.save.stage=3;g.Enter(2);g.enemies.Clear();
            g.save.flags.Add("scout_saved");g.save.flags.Add("floodarchive_public");
            g.save.flags.Add("companion_1");
            WorldObject west=FerryRecord(g,1),east=FerryRecord(g,3);
            g.save.x=west.x;g.save.y=west.y;
            g.save.totalTime=36;g.save.energy=100;
            g.SelectCompanion(1);
            g.CommandCompanion();
            Check(g.companionCooldown>=12f&&g.save.companion==1,
                "The actual player's tactical command begins an expensive cast before ferry boarding");
            g.Talk(west);
            Check(FerryChoice(g,"floodarchive_ferry")&&
                (g.Position-new Point(east.x,east.y)).Length<.001f&&
                g.companionCooldown==0f&&g.save.energy==86,
                "Boarding during a true unreleased command refunds the wasted skill cooldown but not the ferry fare");
            // The same follower now commands a real volley from the actual
            // east-bank body foot, and its released skill cannot be
            // refreshed by another quick crossing to the western bank.
            g.CommandCompanion();
            Check(g.companionCooldown>0,
                "A refunded companion command can be issued again after physical boarding");
            // GameCore deliberately clamps each simulation step to 50ms:
            // five real combat frames, not one oversized synthetic Tick.
            for(int frame=0;frame<5;frame++)
                g.Tick(.05f,new Point());
            float earned=g.companionCooldown;
            Check(earned>0f&&earned<18f&&
                g.projectiles.Exists(p=>p.friendly&&p.life>0),
                "The live command makes a genuine released arrow volley, not just an unspent cooldown");
            // The actual 250ms of world combat also regenerates some player
            // energy. Ferrying must charge exactly the advertised fare from
            // the CURRENT wallet, not from the precombat 86-energy snapshot.
            float beforeReturn=g.save.energy;
            g.save.totalTime=36;
            g.Talk(east);
            bool returned=FerryChoice(g,"floodarchive_ferry");
            Check(returned&&
                (g.Position-new Point(west.x,west.y)).Length<.001f,
                "The real companion and hero can return via the public ferry after release");
            Check(Math.Abs(g.companionCooldown-earned)<.001f&&
                Math.Abs(g.save.energy-(beforeReturn-14f))<.001f,
                "A paid return must preserve the earned skill cooldown and charge once; cooldown="+
                g.companionCooldown+" earned="+earned+" energy="+
                g.save.energy+" before="+beforeReturn);
            g.PrepareSave();
            Check(g.save.version==2&&g.save.companion==1,
                "Neither refunded nor legitimately charged cooldown adds a serialized party field");
        }

        public static void PublicFerryBoardsRealFollowerWithoutWarpingHeldGuard(Campaign data)
        {
            var g=Fresh(data);g.save.stage=3;g.Enter(2);g.enemies.Clear();
            g.save.flags.Add("scout_saved");g.save.flags.Add("floodarchive_public");
            g.save.flags.Add("companion_1");
            WorldObject west=FerryRecord(g,1),east=FerryRecord(g,3);
            g.save.totalTime=36;g.save.energy=100;
            g.save.x=west.x;g.save.y=west.y;
            g.SelectCompanion(1);
            Check(g.save.companion==1&&g.CanStand(g.companionPosition),
                "The public ferry test boards a genuine unlocked physical companion");
            Point arrival=g.companionPosition;
            g.Talk(west);
            Check(FerryChoice(g,"floodarchive_ferry")&&
                (g.Position-new Point(east.x,east.y)).Length<.001f&&
                (g.companionPosition-g.Position).Length>.7f&&
                (g.companionPosition-g.Position).Length<2.5f&&
                g.CanStand(g.companionPosition)&&
                (g.companionPosition-arrival).Length>4f&&g.save.energy==86,
                "An ordinary live companion boards the paid ferry into a distinct safe east-bank body foot");
            g.save.totalTime=36;
            g.Talk(east);
            Check(FerryChoice(g,"floodarchive_ferry")&&
                (g.Position-new Point(west.x,west.y)).Length<.001f&&
                g.CanStand(g.companionPosition)&&
                (g.companionPosition-g.Position).Length<2.5f&&g.save.energy==72,
                "Return passage carries the original follower back without leaving it on the far shore");
            g.companionPosition=new Point(west.x+8f,west.y);
            Point stranded=g.companionPosition;
            g.Talk(west);
            Check(FerryChoice(g,"floodarchive_ferry")&&
                (g.Position-new Point(west.x,west.y)).Length<.001f&&
                g.save.energy==72&&(g.companionPosition-stranded).Length<.001f,
                "A distant follower cannot board remotely, and rejection spends no stamina or position");
            g.companionPosition=arrival;
            g.ToggleCompanionHold();
            Check(g.CompanionHoldingPosition,
                "A physical tactical hold command remains available before voluntary solo passage");
            Point guard=g.companionPosition;
            g.Talk(west);
            Check(FerryChoice(g,"floodarchive_ferry")&&
                (g.Position-new Point(east.x,east.y)).Length<.001f&&
                (g.companionPosition-guard).Length<.001f&&
                g.CompanionHoldingPosition&&g.save.energy==58,
                "Explicit stay-at-post survives the hero's ferry trip without kidnapping the anchored companion");
            g.PrepareSave();var restored=new GameCore(data,CloneSave(g.save));
            Check(restored.save.version==2&&restored.save.companion==1&&
                restored.FloodArchiveComplete,
                "Crossing changes no legacy v2 party, quest or save-format fields");
        }

        public static void MedicalFerryBoardsAllyWithoutDisplacingRealEastRaiders(Campaign data)
        {
            var g=Fresh(data);g.save.stage=3;g.Enter(2);g.enemies.Clear();
            g.invulnerable=400f;
            g.save.flags.Add("scout_saved");g.save.flags.Add("floodarchive_public");
            g.save.flags.Add("companion_1");
            WorldObject west=FerryRecord(g,1),east=FerryRecord(g,3);
            g.save.totalTime=36;g.save.energy=100;
            g.save.x=west.x;g.save.y=west.y;
            g.SelectCompanion(1);
            g.Talk(west);
            Check(FerryChoice(g,"floodarchive_relief_start")&&g.FloodReliefActive,
                "The original urgent cargo still starts at the western record");
            g.Talk(west);
            Check(FerryChoice(g,"floodarchive_ferry")&&
                g.FloodReliefCrossed&&g.FloodReliefRaiders==2&&
                (g.Position-new Point(east.x,east.y)).Length<.001f&&
                (g.companionPosition-g.Position).Length<2.5f&&
                g.CanStand(g.companionPosition),
                "Actual medicine transport ferries the companion into the live combat scene");
            foreach(Enemy foe in g.enemies)
                if(foe.id==92340||foe.id==92341)
                    Check((foe.Position-g.companionPosition).Length>=1.4f,
                        "Neither genuine raider can spawn inside the boarded companion");
            Check(g.save.energy==86&&g.FloodReliefTime==65&&
                g.save.version==2&&!g.FloodReliefComplete,
                "Preflight preserves actual escort timer, costs, nonfarmable kills and v2 saves");
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
            // An unrelated shot must begin on real passable terrain away
            // from the eastern player. A projectile spawned ON the player
            // naturally collides before ownership cleanup is inspected.
            Check(g.CanStand(new Point(west.x,west.y)),
                "The original west-dock comparison projectile has valid ground collision");
            var otherShot=new Projectile{position=new Point(west.x,west.y),
                velocity=new Point(),damage=9,life=10,source="shot"};
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

        public static void LiveEastBankMedicineWardProtectsCargoNotHeroOnce(Campaign data)
        {
            var g=Fresh(data);g.save.stage=3;g.Enter(2);g.enemies.Clear();
            g.save.flags.Add("scout_saved");g.save.flags.Add("floodarchive_public");
            WorldObject west=FerryRecord(g,1),east=FerryRecord(g,3);
            g.save.totalTime=36;g.save.x=west.x;g.save.y=west.y;
            g.save.energy=100;
            g.Talk(west);Check(FerryChoice(g,"floodarchive_relief_start")&&g.FloodReliefActive,
                "The real west-bank dispatch starts the original 65-second medicine run");
            g.Talk(west);Check(FerryChoice(g,"floodarchive_ferry")&&
                g.FloodReliefCrossed&&g.FloodReliefRaiders==2,
                "The player physically arrives on the ambushed east bank");
            float beforeEnergy=g.save.energy;
            g.Talk(east);
            Check(FerryChoice(g,"floodarchive_relief_ward")&&
                g.save.energy==beforeEnergy-20&&g.FloodReliefWardTime==18f,
                "The genuine eastern civilian chooses and pays for 18 seconds of parcel protection");
            var choose=typeof(GameCore).GetMethod("ChooseFloodArchive",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            choose.Invoke(g,new object[]{"floodarchive_relief_ward"});
            Check(g.save.energy==beforeEnergy-20&&g.FloodReliefWardTime==18f,
                "A repeated direct request cannot purchase a second protective ward");
            var hit=typeof(GameCore).GetMethod("Hurt",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            Check(hit!=null,"Actual post-armor combat damage handler exists");
            g.invulnerable=0;
            float health=g.save.health;
            hit.Invoke(g,new object[]{30f,"shot",null});
            float firstDamage=100-g.FloodReliefIntegrity;
            float fullShock=Math.Max(14,Math.Min(38,(health-g.save.health)*1.15f));
            Check(g.save.health<health&&
                Math.Abs(firstDamage-fullShock*.45f)<.05f&&
                g.FloodReliefRaiders==2,
                "Ward contact: hero="+health+"->"+g.save.health+
                " cargoLost="+firstDamage+" expected="+(fullShock*.45f)+
                " ward="+g.FloodReliefWardTime+
                " raiders="+g.FloodReliefRaiders);
            g.invulnerable=0;
            var ward=typeof(GameCore).GetField("floodReliefWardTime",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            Check(ward!=null,"The live shield has an honest countdown, not a permanent buff");
            ward.SetValue(g,0f);
            hit.Invoke(g,new object[]{30f,"shot",null});
            Check(Math.Abs(g.FloodReliefIntegrity-(100-firstDamage-fullShock))<.05f,
                "After the ward expires, an equal true hit damages the parcel at the full original impact");
            choose.Invoke(g,new object[]{"floodarchive_relief_ward"});
            Check(g.FloodReliefWardTime==0&&g.save.energy==beforeEnergy-20,
                "An exhausted ward can never be reactivated for free or with another charge");
            g.PrepareSave();var loaded=new GameCore(data,CloneSave(g.save));
            Check(loaded.save.version==2&&loaded.FloodReliefWardTime==0&&
                !loaded.FloodReliefActive&&!loaded.FloodReliefComplete,
                "Only the actual spent energy is saved; unfinished combat wards do not enter old v2 slots");
        }

        public static void EmergencyFerryEastDockSupportsRealVoluntaryCombatWithdrawal(Campaign data)
        {
            var g=Fresh(data);g.save.stage=3;g.Enter(2);g.enemies.Clear();
            g.save.flags.Add("scout_saved");g.save.flags.Add("floodarchive_public");
            WorldObject west=FerryRecord(g,1),east=FerryRecord(g,3);
            g.save.totalTime=36;g.save.x=west.x;g.save.y=west.y;g.save.energy=100;
            g.Talk(west);Check(FerryChoice(g,"floodarchive_relief_start"),
                "The genuine western station dispatches the medical rescue");
            g.Talk(west);Check(FerryChoice(g,"floodarchive_ferry")&&
                g.FloodReliefCrossed&&g.FloodReliefRaiders==2,
                "The actual ferry produces a real threatened east-bank landing");
            var choose=typeof(GameCore).GetMethod("ChooseFloodArchive",
                System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.NonPublic);
            Check(choose!=null,"The production rescue choice dispatcher is present");
            g.save.x=west.x;g.save.y=west.y;
            choose.Invoke(g,new object[]{"floodarchive_relief_abort_east"});
            Check(g.FloodReliefActive&&g.FloodReliefRaiders==2,
                "A forged east-dock withdrawal cannot be used remotely from the west");
            g.save.x=east.x;g.save.y=east.y;
            var fire=typeof(GameCore).GetMethod("Fire",
                System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.NonPublic);
            Enemy archer=g.enemies.Find(e=>e.id==92341);
            Check(archer!=null&&archer.kind==1,"The second live attacker is a genuine ranged bow raider");
            fire.Invoke(g,new object[]{archer,new Point(-1,0),6f});
            Check(g.projectiles.Exists(p=>p.floodReliefOwned),
                "The bow attacker has a live in-flight arrow before the civilian withdraws");
            int gold=g.save.coins;
            g.Talk(east);
            Check(FerryChoice(g,"floodarchive_relief_abort_east")&&
                !g.FloodReliefActive&&g.FloodReliefRaiders==0&&
                !g.projectiles.Exists(p=>p.floodReliefOwned)&&
                !g.FloodReliefComplete&&g.save.coins==gold,
                "Voluntary withdrawal at the real eastern post retires only escort actors without paying success rewards");
            g.save.x=west.x;g.save.y=west.y;g.save.totalTime=36;
            g.Talk(west);
            Check(FerryChoice(g,"floodarchive_relief_start")&&g.FloodReliefActive,
                "After withdrawal the original civilian medical mission is fully retryable");
            g.PrepareSave();var copy=new GameCore(data,CloneSave(g.save));
            Check(copy.save.version==2&&copy.FloodArchiveComplete&&
                !copy.FloodReliefActive&&!copy.FloodReliefComplete,
                "Legacy v2 stores neither fake delivery nor abandoned mission actor state");
        }



        public static void RealFerryCompassPursuesOnlySurvivingAmbushBodies(Campaign data)
        {
            var g=Fresh(data);g.save.stage=3;g.save.level=18;
            g.Enter(2);g.enemies.Clear();g.invulnerable=800f;
            g.save.flags.Add("scout_saved");g.save.flags.Add("floodarchive_public");
            WorldObject west=FerryRecord(g,1),east=FerryRecord(g,3);
            g.save.totalTime=36;g.save.energy=100;
            g.save.x=west.x;g.save.y=west.y;
            Point starting=g.GetQuest("ferry_relief").target;
            Check((starting-new Point(west.x,west.y)).Length<.001f,
                "Before loading cargo the production compass marks the physical west dock");
            g.Talk(west);
            Check(FerryChoice(g,"floodarchive_relief_start")&&
                (g.GetQuest("ferry_relief").target-starting).Length<.001f,
                "Carried but unboarded medicine still points to the true launch pier");
            g.Talk(west);
            Check(FerryChoice(g,"floodarchive_ferry")&&g.FloodReliefRaiders==2,
                "One actual crossing deploys the original melee and ranged ambushers");
            Enemy melee=g.enemies.Find(e=>e.id==92340);
            Enemy bow=g.enemies.Find(e=>e.id==92341);
            Check(melee!=null&&bow!=null,"Both genuinely spawned raiders exist");
            Point closest=(melee.Position-g.Position).Length<=
                (bow.Position-g.Position).Length?melee.Position:bow.Position;
            Check((g.GetQuest("ferry_relief").target-closest).Length<.001f&&
                g.GetQuest("ferry_relief").next.Contains("罗盘"),
                "East-bank battle guidance follows the nearest real enemy, not an idle delivery post");
            g.save.x=bow.x;g.save.y=bow.y;
            Check((g.GetQuest("ferry_relief").target-bow.Position).Length<.001f,
                "The quest objective follows live body locations when the player changes position");
            var impostor=new Enemy(92340,0,bow.x+.05f,bow.y+.05f,900f);
            g.enemies.Add(impostor);
            Check((g.GetQuest("ferry_relief").target-bow.Position).Length<.001f,
                "An unrelated enemy with the same spawn ID cannot hijack combat guidance");
            var hit=typeof(GameCore).GetMethod("Hit",
                System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.NonPublic);
            Check(hit!=null,"The production combat defeat callback is reachable");
            hit.Invoke(g,new object[]{bow,99999f,false,null,null});
            Check(g.FloodReliefDefeated==1&&
                (g.GetQuest("ferry_relief").target-melee.Position).Length<.001f,
                "Defeating the marked archer retargets the true surviving melee raider");
            g.enemies.Remove(impostor);
            hit.Invoke(g,new object[]{melee,99999f,false,null,null});
            Point delivered=g.GetQuest("ferry_relief").target;
            Check(g.FloodReliefSecured&&
                (delivered-new Point(east.x,east.y)).Length<.001f&&
                !g.FloodReliefComplete,
                "Only two actual kills return the live compass to the authentic delivery dock");
            g.save.x=east.x;g.save.y=east.y;
            g.Talk(east);
            Check(FerryChoice(g,"floodarchive_relief_deliver")&&
                g.FloodReliefComplete&&
                (g.GetQuest("ferry_relief").target-delivered).Length<.001f&&
                g.save.version==2,
                "Actual handoff persists the old save flag and maintains the physical completed objective");
        }
        public static void FerryReliefSameIdActorsCannotForgeKillsOrRetireBystanders(Campaign data)
        {
            var g=Fresh(data);g.save.stage=3;g.Enter(2);g.enemies.Clear();
            g.invulnerable=800f;
            g.save.flags.Add("scout_saved");
            g.save.flags.Add("floodarchive_public");
            WorldObject west=FerryRecord(g,1);
            g.save.totalTime=36;g.save.energy=100;
            g.save.x=west.x;g.save.y=west.y;
            g.Talk(west);
            Check(FerryChoice(g,"floodarchive_relief_start"),
                "The earned original medicine crossing still opens");
            g.Talk(west);
            Check(FerryChoice(g,"floodarchive_ferry")&&g.FloodReliefRaiders==2,
                "The real low-tide ferry deploys both authentic raiders");
            Enemy melee=g.enemies.Find(e=>e.kind==0&&e.id==92340);
            Enemy archer=g.enemies.Find(e=>e.kind==1&&e.id==92341);
            Check(melee!=null&&archer!=null,"The authored melee and archer bodies exist");
            var isOwned=typeof(GameCore).GetMethod("IsFloodReliefEnemy",
                System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.NonPublic);
            var hit=typeof(GameCore).GetMethod("Hit",
                System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.NonPublic);
            var reset=typeof(GameCore).GetMethod("ResetFloodRelief",
                System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.NonPublic);
            Check(isOwned!=null&&hit!=null&&reset!=null,
                "The actual combat, mission identity and cleanup paths exist");
            var copy=new Enemy(melee.id,melee.kind,
                melee.x+.35f,melee.y+.6f,600f);
            g.enemies.Add(copy);
            Check(!(bool)isOwned.Invoke(g,new object[]{copy})&&
                (bool)isOwned.Invoke(g,new object[]{melee}),
                "Identical numeric ID cannot make a bystander an escort raider");
            hit.Invoke(g,new object[]{copy,10000f,false,null,null});
            Check(g.FloodReliefDefeated==0&&g.FloodReliefRaiders==2&&
                !g.FloodReliefSecured,
                "Defeating a nonmission same-ID actor grants no escorted-raider kill credit");
            // Disappearance cannot be spoofed by a same-ID surviving body.
            var secondCopy=new Enemy(archer.id,archer.kind,
                archer.x+.25f,archer.y+.3f,800f);
            g.enemies.Add(secondCopy);
            g.enemies.Remove(archer);
            g.hitStop=0f;g.Tick(.05f,new Point());
            Check(!g.FloodReliefActive&&!g.FloodReliefComplete&&
                g.enemies.Contains(secondCopy)&&
                !g.enemies.Contains(melee)&&g.save.version==2,
                "Only the exact ambush members retire; a real missing archer aborts even with a forged same-ID substitute");
            g.save.x=west.x;g.save.y=west.y;g.save.totalTime=36;
            g.Talk(west);
            Check(FerryChoice(g,"floodarchive_relief_start")&&g.FloodReliefActive,
                "The previous counterfeiting attempt cannot permanently block a safe retry");
        }

    }
}
