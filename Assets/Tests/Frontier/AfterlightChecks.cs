using System;
namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        private static void WalkWithAfterlightCharge(GameCore g, Point target)
        {
            for(int frame=0;frame<3400 &&
                (g.Position-target).Length>.84f &&
                g.AfterlightChargeRegion!=0;frame++)
            {
                Point direction=g.DirectionTo(g.Position,target);
                g.Tick(.02f,direction);
            }
            Check((g.Position-target).Length<.95f &&
                g.AfterlightChargeRegion!=0,
                "Canonical main character must actually walk a navigable map path to the remote civic receiver before the 90-second charge expires");
        }
        public static void AfterlightSealedTerminalsCannotSpendStabilizationOrInstallationMaterials(Campaign data)
        {
            MapData original=data.maps[3];
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
                WorldObject source=new WorldObject{id="afterlightanchor3",
                    kind="afterlightanchor3",name="Living power source",
                    en="Living power source",x=10.5f,y=10.5f};
                WorldObject target=new WorldObject{id="afterlightoutlet3",
                    kind="afterlightoutlet3",name="Civic power receiver",
                    en="Civic power receiver",x=20.5f,y=10.5f};
                data.maps[3]=new MapData{id=3,spawnX=12.25f,
                    spawnY=10.5f,rows=rows,
                    objects=new[]{source,target},decor=new Decoration[0]};
                var g=Fresh(data);g.save.stage=3;g.save.ending=3;
                g.save.flags.Add("afterlight_started");
                g.save.flags.Add("afterlight_plan_3_watch");
                g.Enter(3);g.enemies.Clear();g.invulnerable=180f;
                g.save.inventory[1]=8;g.save.inventory[4]=5;
                g.save.x=12.25f;g.save.y=10.5f;
                g.Talk(source);
                int launch=-1;
                for(int i=0;i<g.conversation.choices.Length;i++)
                    if(g.conversation.choices[i].action=="afterlight_stable_3")
                    {launch=i;break;}
                Check(launch>=0&&g.CanStand(g.Position),
                    "The live signed civic source offers its genuine one-crystal 120-second transaction");
                char[] gate=rows[10].ToCharArray();
                gate[11]='#';rows[10]=new string(gate);
                Check(g.CanStand(new Point(source.x,source.y))&&
                    g.CanStand(g.Position)&&g.Blocked(11.5f,10.5f),
                    "Newly sealed solid cover blocks the source from a nearby but legal hero foot");
                g.Choose(launch);
                Check(g.AfterlightChargeRegion==0&&g.save.inventory[1]==8,
                    "Cached stable-start button cannot charge real crystal or create a physical transfer through stone");
                gate[11]='.';rows[10]=new string(gate);
                g.Talk(source);launch=-1;
                for(int i=0;i<g.conversation.choices.Length;i++)
                    if(g.conversation.choices[i].action=="afterlight_stable_3")
                    {launch=i;break;}
                Check(launch>=0,"Opening the same source contact restores the original stable-load choice");
                g.Choose(launch);
                Check(g.AfterlightChargeRegion==3&&g.AfterlightStableCharge&&
                    g.save.inventory[1]==7&&g.AfterlightChargeLeft==120,
                    "The reachable 120-second live charge consumes exactly one genuine crystal");
                g.save.x=22.25f;g.save.y=10.5f;
                g.Talk(target);
                int install=-1;
                for(int i=0;i<g.conversation.choices.Length;i++)
                    if(g.conversation.choices[i].action=="afterlight_install_3")
                    {install=i;break;}
                Check(install>=0&&g.CanStand(g.Position),
                    "The actual distant receiver offers a real material-spending permanent installation");
                int coins=g.save.coins;
                gate[21]='#';rows[10]=new string(gate);
                Check(g.CanStand(new Point(target.x,target.y))&&
                    g.Blocked(21.5f,10.5f),
                    "A second dynamic wall seals a charged receiver after its dialogue was opened");
                g.Choose(install);
                Check(!g.Has("afterlight_done_3")&&g.AfterlightChargeRegion==3&&
                    g.save.inventory[1]==7&&g.save.inventory[4]==5&&
                    g.save.coins==coins,
                    "Stale installation cannot spend crystals/parts, publish a facility or lose the live charge through a wall");
                gate[21]='.';rows[10]=new string(gate);
                g.Talk(target);install=-1;
                for(int i=0;i<g.conversation.choices.Length;i++)
                    if(g.conversation.choices[i].action=="afterlight_install_3")
                    {install=i;break;}
                Check(install>=0,"Unblocked receiver restores the original permanent installation");
                g.Choose(install);
                Check(g.Has("afterlight_done_3")&&g.AfterlightChargeRegion==0&&
                    g.save.inventory[1]==5&&g.save.inventory[4]==4&&
                    g.save.coins==coins+50&&g.save.version==2,
                    "Only accessible full-power receiver charges exact original inputs and awards one legitimate legacy-v2 civic installation");
            }
            finally {data.maps[3]=original;}
        }

        public static void ChapterFiveAfterlightRebuildsRealThreeRegionWorldAfterOriginalEnding(Campaign data)
        {
            var g=Fresh(data);
            Check(g.GetQuest("afterlight").locked &&
                !g.Has("afterlight_started") && g.AfterlightRestored==0,
                "An unfinished first four acts cannot preview or claim post-finale reconstruction");
            var council=Object(g,"afterlightcouncil");
            g.save.x=council.x;g.save.y=council.y;
            g.Talk(council);
            Check(!Array.Exists(g.conversation.choices,c=>c.action=="afterlight_begin"),
                "War reconstruction is gated by a genuine original ending and never substitutes for it");
            g.Close();
            g.save.stage=3;g.save.ending=3;g.save.flags.Add("boss_4");
            Action(g,"afterlightcouncil","afterlight_begin");
            Check(g.Has("afterlight_started") &&
                g.CurrentGuide.id=="afterlight" &&
                g.GetQuest("main").complete,
                "Original ending remains valid and Chapter Five begins as an actual additional tracked story");
            int startingCoins=g.save.coins;
            int startingShards=g.save.shards;
            for(int room=1;room<=3;room++)
            {
                g.Enter(room);g.enemies.Clear();g.invulnerable=1000;
                g.save.inventory[0]=15;g.save.inventory[1]=15;
                g.save.inventory[2]=15;g.save.inventory[4]=15;
                WorldObject anchor=Object(g,"afterlightanchor"+room),
                    outlet=Object(g,"afterlightoutlet"+room);
                Check(g.CanStand(new Point(anchor.x,anchor.y)) &&
                    g.CanStand(new Point(outlet.x,outlet.y)) &&
                    (new Point(anchor.x,anchor.y)-new Point(outlet.x,outlet.y)).Length>5,
                    "Each reconstructed region contains two genuinely separated accessible authored civic work sites");
                g.save.x=outlet.x;g.save.y=outlet.y;
                g.Talk(outlet);
                Check(!Array.Exists(g.conversation.choices,c=>
                    c.action=="afterlight_install_"+room),
                    "The distant receiver cannot be completed without original source calibration");
                g.Close();
                g.save.x=anchor.x;g.save.y=anchor.y;
                int mode=room==1?2:1;
                Action(g,anchor.kind,"afterlight_plan_"+room+
                    (mode==1?"_care":"_watch"));
                Check(g.AfterlightMode(room)==mode &&
                    !g.Has("afterlight_done_"+room),
                    "Resident public-power choice is persisted, but the site is not magically built");
                g.PrepareSave();
                var reload=new GameCore(data,CloneSave(g.save));reload.Start();
                Check(reload.AfterlightMode(room)==mode &&
                    reload.AfterlightChargeRegion==0 &&
                    !reload.Has("afterlight_done_"+room),
                    "Legacy SaveData v2 can preserve a civic decision without transient load or schema changes");
                g.save.x=anchor.x;g.save.y=anchor.y;
                Action(g,anchor.kind,"afterlight_charge_"+room);
                Check(g.AfterlightChargeRegion==room &&
                    g.AfterlightChargeLeft>89,
                    "Real physical remote load transfer begins with a strictly timed 90-second charge");
                int heldCoins=g.save.coins;
                g.Enter(0);
                Check(g.save.room==room && g.AfterlightChargeRegion==room,
                    "Changing maps cannot erase a live reconstruction transfer");
                g.PrepareSave();
                reload=new GameCore(data,CloneSave(g.save));reload.Start();
                Check(reload.AfterlightChargeRegion==0 &&
                    reload.AfterlightMode(room)==mode &&
                    !reload.Has("afterlight_done_"+room),
                    "Reload aborts transient transfer safely, keeping the public plan and no fake reward");
                WalkWithAfterlightCharge(g,new Point(outlet.x,outlet.y));
                g.save.x=outlet.x;g.save.y=outlet.y;
                Check(g.Nearest()==outlet,
                    "A remote receiver remains interactable after real actor traversal");
                int wood=g.save.inventory[0], crystal=g.save.inventory[1],
                    herb=g.save.inventory[2],gear=g.save.inventory[4];
                Action(g,outlet.kind,"afterlight_install_"+room);
                Check(g.Has("afterlight_done_"+room) &&
                    g.AfterlightRestored==room &&
                    g.AfterlightChargeRegion==0 &&
                    g.save.coins==heldCoins+20+room*10,
                    "Only physically delivered and installed regional power writes a permanent achievement and one-time coins");
                Check(g.save.inventory[0]==wood-(mode==1?2:0) &&
                    g.save.inventory[1]==crystal-(mode==2?2:0) &&
                    g.save.inventory[2]==herb-(mode==1?2:0) &&
                    g.save.inventory[4]==gear-(mode==2?1:0),
                    "Actual completed rebuilding spends exactly the visible materials, never a purely narrative cost");
                Action(g,outlet.kind,room==2?"afterlight_ferry_out":"close");
                if(room==2)g.save.x=outlet.x;g.save.y=outlet.y;
                Check(g.AfterlightRestored==room && g.save.coins==heldCoins+20+room*10,
                    "Rereading installed machinery cannot duplicate regional story rewards");
                WorldObject steward=Object(g,"witnessnpc"+room);
                QuestGuide pending=g.GetQuest("afterlight");
                Check(pending.room==room &&
                    (pending.target-new Point(steward.x,steward.y)).Length<.02f,
                    "Once repaired, the real chapter-five guidance directs player to the original living regional witness for stewardship");
                g.save.x=steward.x;g.save.y=steward.y;
                Action(g,steward.kind,"afterlight_steward_"+room);
                Check(g.AfterlightStewards==room && g.Has("afterlight_steward_"+room),
                    "Living original chapter witness must personally sign the specific regional maintenance commitment");
            }
            Check(g.AfterlightRestored==3 && g.AfterlightStewards==3 &&
                !g.Has("afterlight_complete"),
                "Real physical projects are not the same as signing the public charter");
            g.Enter(0);g.enemies.Clear();
            council=Object(g,"afterlightcouncil");
            g.save.x=council.x;g.save.y=council.y;
            int coins=g.save.coins,shards=g.save.shards,points=g.save.skillPoints;
            Action(g,council.kind,"afterlight_publish");
            Check(g.Has("afterlight_complete") && g.GetQuest("afterlight").complete &&
                g.save.coins==coins+120 && g.save.shards==shards+4 &&
                g.save.skillPoints==points+1 &&
                g.GetQuest("main").complete && g.save.ending==3,
                "The full, manually published reconstruction charter rewards exactly once and never changes the player's original ending");
            g.PrepareSave();
            var persisted=new GameCore(data,CloneSave(g.save));persisted.Start();
            Check(persisted.save.version==2 && persisted.AfterlightComplete &&
                persisted.AfterlightRestored==3 && persisted.AfterlightStewards==3 && persisted.save.ending==3,
                "All public services, choices and original ending survive version-two save serialization");
            g.Talk(council);
            Check(!Array.Exists(g.conversation.choices,c=>c.action=="afterlight_publish"),
                "Completed civic charter never mints repeat endgame bonuses");
        }
        public static void ChapterFiveAfterlightTimeoutMaterialShortageAndAbortPreserveRealState(Campaign data)
        {
            var g=Fresh(data);g.save.stage=3;g.save.ending=2;g.save.flags.Add("boss_4");
            WorldObject board=Object(g,"afterlightcouncil");g.save.x=board.x;g.save.y=board.y;
            Action(g,board.kind,"afterlight_begin");g.Enter(1);g.enemies.Clear();
            WorldObject anchor=Object(g,"afterlightanchor1"),outlet=Object(g,"afterlightoutlet1");
            g.save.x=anchor.x;g.save.y=anchor.y;
            Action(g,anchor.kind,"afterlight_plan_1_care");
            g.save.inventory[0]=0;g.save.inventory[2]=0;
            int money=g.save.coins;
            Action(g,anchor.kind,"afterlight_charge_1");
            g.save.x=outlet.x;g.save.y=outlet.y;
            Action(g,outlet.kind,"afterlight_install_1");
            Check(g.AfterlightChargeRegion==1 &&
                !g.Has("afterlight_done_1") && g.save.coins==money,
                "Without actual timber/herbs, installation cannot mint public facilities or steal a failed transfer");
            g.enemies.Clear();g.invulnerable=500;
            Advance(g,91);
            Check(g.AfterlightChargeRegion==0 &&
                g.AfterlightMode(1)==1 && !g.Has("afterlight_done_1") &&
                g.save.coins==money,
                "90-second failure correctly loses only transient charge, never public design or permanent materials");
            g.save.x=anchor.x;g.save.y=anchor.y;
            Action(g,anchor.kind,"afterlight_charge_1");
            Action(g,anchor.kind,"afterlight_abort");
            Check(g.AfterlightChargeRegion==0 && g.AfterlightMode(1)==1 &&
                !g.Has("afterlight_done_1"),
                "Explicit cancel at source is free and preserves the civic plan");
            g.save.inventory[0]=2;g.save.inventory[2]=2;
            Action(g,anchor.kind,"afterlight_charge_1");
            g.save.x=outlet.x;g.save.y=outlet.y;
            Action(g,outlet.kind,"afterlight_install_1");
            Check(g.Has("afterlight_done_1") &&
                g.save.inventory[0]==0 && g.save.inventory[2]==0,
                "Material retry can finish only once using real ingredients");
        }
        public static void ChapterFiveServicesHaveRealRegionalEffectsWithCorrectGates(Campaign data)
        {
            var g=Fresh(data);g.save.stage=3;g.save.ending=1;
            g.Enter(2);g.enemies.Clear();
            g.save.flags.Add("afterlight_started");
            WorldObject anchor=Object(g,"afterlightanchor2"),
                outpost=Object(g,"afterlightoutlet2");
            g.save.x=outpost.x;g.save.y=outpost.y;
            g.Talk(outpost);
            Check(!Array.Exists(g.conversation.choices,c=>c.action=="afterlight_ferry_out"),
                "Unbuilt public ferry cannot bypass the old Drowned Ruins map or its stage gates");
            g.Close();
            g.save.flags.Add("afterlight_started");
            g.save.flags.Add("afterlight_plan_2_care");
            g.save.flags.Add("afterlight_done_2");
            Action(g,outpost.kind,"afterlight_ferry_out");
            Check((g.Position-new Point(anchor.x,anchor.y)).Length<.05f,
                "Actually restored public boat can carry player to other physical shore");
            Action(g,anchor.kind,"afterlight_ferry_back");
            Check((g.Position-new Point(outpost.x,outpost.y)).Length<.05f,
                "Public ferry supports real safe two-way travel");
            g.enemies.Add(new Enemy(88442,0,outpost.x+.8f,outpost.y,30));
            g.Talk(outpost);int oldRoom=g.save.room;
            g.Choose(Array.FindIndex(g.conversation.choices,c=>c.action=="afterlight_ferry_out"));
            Check(g.save.room==oldRoom &&
                (g.Position-new Point(outpost.x,outpost.y)).Length<.05f,
                "Reconstructed ferry cannot serve as a nearby-combat escape exploit");
            g.enemies.Clear();
            g.Enter(3);g.enemies.Clear();
            WorldObject mine=Object(g,"afterlightoutlet3");
            g.save.x=mine.x;g.save.y=mine.y;
            g.save.flags.Add("afterlight_plan_3_care");
            g.save.flags.Add("afterlight_done_3");
            g.save.inventory[0]=3;g.save.inventory[1]=4;g.save.inventory[4]=0;
            Action(g,mine.kind,"afterlight_craft");
            Check(g.save.inventory[0]==2 && g.save.inventory[1]==2 &&
                g.save.inventory[4]==2,
                "Finished mine restoration actually crafts tangible replacement parts from charged real resources");
            Action(g,mine.kind,"afterlight_craft");
            Check(g.save.inventory[0]==1 && g.save.inventory[1]==0 &&
                g.save.inventory[4]==4,
                "Every industrial batch costs inputs and cannot duplicate free gear");
            Action(g,mine.kind,"afterlight_craft");
            Check(g.save.inventory[0]==1 && g.save.inventory[1]==0 &&
                g.save.inventory[4]==4,
                "Missing materials reject new crafting without rewards or coins");
            g.Enter(1);g.enemies.Clear();
            WorldObject forest=Object(g,"afterlightoutlet1");
            g.save.flags.Add("afterlight_plan_1_watch");
            g.save.flags.Add("afterlight_done_1");
            g.save.x=forest.x;g.save.y=forest.y;
            g.save.energy=10;g.invulnerable=80;
            Advance(g,2);
            Check(g.save.energy>42,
                "Actual forest lantern variant adds renewable player energy over the original passive regeneration");
            g.save.energy=10;g.save.x=forest.x-5;g.save.y=forest.y;
            Advance(g,2);
            Check(g.save.energy<42,
                "Forest lantern service is a local installation, never a permanent all-map energy buff");
        }
        public static void ChapterFiveChronicleAndEpilogueReflectOnlySignedActualWorks(Campaign data)
        {
            var g=Fresh(data);g.save.stage=3;g.save.ending=2;
            g.Enter(0);g.enemies.Clear();
            WorldObject book=Object(g,"worldchronicle");
            g.save.x=book.x;g.save.y=book.y;g.Talk(book);
            g.Choose(Array.FindIndex(g.conversation.choices,c=>c.action=="story_section_after"));
            Check(Array.FindIndex(g.conversation.choices,c=>c.action=="story_read_afterlight")<0,
                "Unstarted Chapter Five does not appear as a falsely completed chronicle");
            g.Close();
            g.save.flags.Add("afterlight_started");
            g.save.flags.Add("afterlight_plan_1_care");
            g.save.flags.Add("afterlight_done_1");
            g.Talk(book);
            g.Choose(Array.FindIndex(g.conversation.choices,c=>c.action=="story_section_after"));
            int idx=Array.FindIndex(g.conversation.choices,c=>c.action=="story_read_afterlight");
            Check(idx>=0,"Actual post-finale account now becomes a chronicle page");
            g.Choose(idx);
            Check(g.conversation.text.Contains("荆棘密林") &&
                g.conversation.text.Contains("仍未接通") &&
                !g.conversation.text.Contains("【重建公约已发布】"),
                "Chronicle records the one rebuilt region but does not spoil or claim other public works");
            g.Close();
            g.Epilogue();
            Check(g.conversation.text.Contains("1/3") &&
                g.conversation.textEn.Contains("1/3"),
                "Original preserved second ending admits that only one region is currently repaired");
            g.Close();
            for(int i=2;i<=3;i++){g.save.flags.Add("afterlight_plan_"+i+"_watch");g.save.flags.Add("afterlight_done_"+i);}
            g.save.flags.Add("afterlight_complete");
            g.Epilogue();
            Check(g.conversation.text.Contains("3/3") &&
                g.conversation.text.Contains("三地居民共建的维护公约"),
                "Original ending text changes only after all public works and real charter have been published");
        }

        public static void ChapterFiveCharterRequiresThreeRealLivingStewardSignatures(Campaign data)
        {
            var g=Fresh(data);g.save.stage=3;g.save.ending=1;
            g.save.flags.Add("afterlight_started");g.save.flags.Add("scout_saved");
            for(int i=1;i<=3;i++)
            {
                g.save.flags.Add("afterlight_done_"+i);
                g.save.flags.Add("afterlight_plan_"+i+"_care");
            }
            WorldObject board=Object(g,"afterlightcouncil");
            g.save.x=board.x;g.save.y=board.y;
            g.Talk(board);
            Check(!Array.Exists(g.conversation.choices,c=>c.action=="afterlight_publish") &&
                g.conversation.text.Contains("尚未签字"),
                "Building three functioning machines alone cannot claim the final charter without responsible living stewards");
            g.Close();
            g.Enter(1);g.enemies.Clear();
            WorldObject moya=Object(g,"witnessnpc1");
            g.save.x=24.5f;g.save.y=7.5f;
            Action(g,moya.kind,"afterlight_steward_1");
            Check(!g.Has("afterlight_steward_1"),
                "No proxy or remote forged signatures: original witness must be approached in person");
            g.save.x=moya.x;g.save.y=moya.y;
            Action(g,moya.kind,"afterlight_steward_1");
            Check(g.Has("afterlight_steward_1") &&
                g.AfterlightStewards==1 && g.AfterlightRestored==3,
                "The actual first-act witness makes the regional maintenance pact without repeating evidence missions");
            g.Talk(moya);
            Check(!Array.Exists(g.conversation.choices,c=>c.action=="afterlight_steward_1") &&
                Array.Exists(g.conversation.choices,c=>c.action=="witness_begin_1"),
                "After signing stewardship, the original witness and her prior optional story remain available");
            g.Close();
            for(int i=2;i<=3;i++)g.save.flags.Add("afterlight_steward_"+i);
            g.Enter(0);g.enemies.Clear();
            g.save.x=board.x;g.save.y=board.y;
            g.Talk(board);
            Check(Array.Exists(g.conversation.choices,c=>c.action=="afterlight_publish") &&
                g.AfterlightStewards==3 && !g.AfterlightComplete,
                "Final charter becomes an actual user-facing action only after all real local signatures");
            g.Close();
            Action(g,board.kind,"afterlight_publish");
            Check(g.AfterlightComplete,"Signed civic reconstruction is an earned main-story closure");
        }


        public static void ChapterFiveSignedPublicStarroadsConnectActualTownAndRegionsSafely(Campaign data)
        {
            var g=Fresh(data);g.save.stage=3;g.save.ending=3;
            g.save.flags.Add("afterlight_started");
            for(int region=1;region<=3;region++)
            {
                g.Enter(0);g.enemies.Clear();
                WorldObject waypoint=Object(g,"afterlightwaypoint"+region);
                Point home=new Point(waypoint.x,waypoint.y);
                g.save.x=waypoint.x;g.save.y=waypoint.y;
                Check(g.CanStand(home) && !g.IsObjectPresent(waypoint),
                    "A public road must not appear before a region is actually rebuilt and signed");
                g.save.flags.Add("afterlight_plan_"+region+"_care");
                g.save.flags.Add("afterlight_done_"+region);
                Check(!g.IsObjectPresent(waypoint),
                    "Repairing a machine alone is not public governance; the shortcut stays hidden until the local steward signs");
                g.save.flags.Add("afterlight_steward_"+region);
                Check(g.IsObjectPresent(waypoint) && g.Nearest()==waypoint,
                    "Each signed chapter unlocks its own physical reachable landmark in the original Emberfall map");
                var blocker=new Enemy(88000+region,0,waypoint.x+.7f,waypoint.y,60);
                blocker.stun=60;blocker.timer=60;g.enemies.Add(blocker);
                g.save.x=waypoint.x;g.save.y=waypoint.y;
                Action(g,waypoint.kind,"afterlight_route_"+region);
                Check(g.save.room==0 && 
                    (g.Position-home).Length<.05f,
                    "A public starroad cannot allow travel while a real enemy contests the town landmark");
                g.enemies.Clear();
                Action(g,waypoint.kind,"afterlight_route_"+region);
                Check(g.save.room==region &&
                    (g.Position-new Point(Object(g,"afterlightoutlet"+region).x,
                        Object(g,"afterlightoutlet"+region).y)).Length<.05f,
                    "Signed public starroad really transports the hero to that region's repaired distant receiver");
                g.enemies.Clear();
                WorldObject outlet=Object(g,"afterlightoutlet"+region);
                g.save.x=outlet.x;g.save.y=outlet.y;
                g.Talk(outlet);
                Check(Array.Exists(g.conversation.choices,c=>
                    c.action=="afterlight_return_"+region),
                    "Installed public service remains available and separately offers an actual town return route");
                g.Close();
                Action(g,outlet.kind,"afterlight_return_"+region);
                Check(g.save.room==0 && (g.Position-home).Length<.05f,
                    "Public starroad is truly bidirectional and lands at its corresponding real town landmark");
                g.PrepareSave();
                var reload=new GameCore(data,CloneSave(g.save));reload.Start();
                Check(reload.IsObjectPresent(waypoint) &&
                    reload.AfterlightRestored>=region &&
                    reload.AfterlightStewards>=region,
                    "Real town transport stations survive SaveData v2 without changing other chapter portals");
            }
        }

        public static void AfterlightTransportPreservesFieldMissionsAndValidArrivalGeometry(Campaign data)
        {
            var g=Fresh(data);g.save.stage=3;g.save.ending=3;
            g.save.flags.Add("afterlight_started");
            for(int room=1;room<=3;room++)
            {
                g.save.flags.Add("afterlight_plan_"+room+"_care");
                g.save.flags.Add("afterlight_done_"+room);
                g.save.flags.Add("afterlight_steward_"+room);
            }
            g.Enter(2);g.enemies.Clear();
            WorldObject outlet=Object(g,"afterlightoutlet2"),
                anchor=Object(g,"afterlightanchor2");
            g.save.x=outlet.x;g.save.y=outlet.y;
            var blast=new Hazard{position=new Point(anchor.x,anchor.y),
                timer=9f,radius=1.5f,damage=20,kind="rift"};
            g.hazards.Add(blast);
            Action(g,outlet.kind,"afterlight_ferry_out");
            Check((g.Position-new Point(outlet.x,outlet.y)).Length<.01f,
                "A repaired public ferry rejects a real pending landing explosion");
            g.hazards.Remove(blast);
            var arrow=new Projectile{position=new Point(anchor.x,anchor.y),
                velocity=new Point(),life=9f,damage=11,source="shot"};
            g.projectiles.Add(arrow);
            Action(g,outlet.kind,"afterlight_ferry_out");
            Check((g.Position-new Point(outlet.x,outlet.y)).Length<.01f,
                "A destination enemy projectile also makes civic ferry arrival unsafe");
            g.projectiles.Remove(arrow);
            Action(g,outlet.kind,"afterlight_ferry_out");
            Check((g.Position-new Point(anchor.x,anchor.y)).Length<.01f,
                "Normal two-way civic ferry still works after the danger clears");
            g.Enter(0);g.enemies.Clear();
            WorldObject waypoint=Object(g,"afterlightwaypoint1");
            g.save.x=waypoint.x;g.save.y=waypoint.y;
            Point from=g.Position;
            WorldObject arrival=null;
            foreach(WorldObject o in data.maps[1].objects)
                if(o.kind=="afterlightoutlet1")arrival=o;
            Check(arrival!=null,"Original repaired destination exists");
            int x=(int)Math.Floor(arrival.x),y=(int)Math.Floor(arrival.y);
            string row=data.maps[1].rows[y];
            try
            {
                char[] blocked=row.ToCharArray();blocked[x]='#';
                data.maps[1].rows[y]=new string(blocked);
                Action(g,waypoint.kind,"afterlight_route_1");
                Check(g.save.room==0&&(g.Position-from).Length<.01f,
                    "Blocked original destination prevents map reset before transit");
            }
            finally {data.maps[1].rows[y]=row;}
            Action(g,waypoint.kind,"afterlight_route_1");
            Check(g.save.room==1&&
                (g.Position-new Point(arrival.x,arrival.y)).Length<.01f,
                "Clearing the destination obstruction restores actual signed starroad travel");
            g.PrepareSave();var load=new GameCore(data,CloneSave(g.save));
            Check(load.save.version==2&&load.Has("afterlight_done_1"),
                "Existing reconstruction flags persist without changing SaveData v2");
        }

        public static void AfterlightStableCrystalChargeRespectsFreeRouteAndRealMaterialCosts(Campaign data)
        {
            var g=Fresh(data);g.save.stage=3;g.save.ending=3;
            g.save.flags.Add("afterlight_started");
            g.save.flags.Add("afterlight_plan_1_care");
            g.Enter(1);g.enemies.Clear();g.invulnerable=500;
            WorldObject anchor=Object(g,"afterlightanchor1"),
                outlet=Object(g,"afterlightoutlet1");
            g.save.x=anchor.x;g.save.y=anchor.y;
            g.save.inventory[1]=0;
            Action(g,anchor.kind,"afterlight_stable_1");
            Check(g.AfterlightChargeRegion==0&&!g.AfterlightStableCharge,
                "Stable load requires a real owned crystal and cannot start for free");
            Action(g,anchor.kind,"afterlight_charge_1");
            Check(g.AfterlightChargeRegion==1&&!g.AfterlightStableCharge&&
                g.AfterlightChargeLeft==90,
                "The original free 90-second physically carried civic route remains available");
            Action(g,anchor.kind,"afterlight_abort");
            Check(g.AfterlightChargeRegion==0&&!g.AfterlightStableCharge,
                "Canceling a free transfer resets only the transient timer");
            g.save.inventory[1]=2;g.save.inventory[0]=4;g.save.inventory[2]=4;
            Action(g,anchor.kind,"afterlight_stable_1");
            Check(g.AfterlightChargeRegion==1&&g.AfterlightStableCharge&&
                g.AfterlightChargeLeft==120&&g.save.inventory[1]==1,
                "One real starcrystal purchases precisely 120 seconds of civic load time");
            var choose=typeof(GameCore).GetMethod("ChooseAfterlight",
                System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.NonPublic);
            Check(choose!=null,"Original civic action dispatcher exists");
            choose.Invoke(g,new object[]{"afterlight_stable_1"});
            Check(g.save.inventory[1]==1&&g.AfterlightChargeLeft==120,
                "A forged repeat cannot spend multiple crystals or stack extra time");
            Action(g,anchor.kind,"afterlight_abort");
            Check(g.AfterlightChargeRegion==0&&g.save.inventory[1]==1,
                "An aborted paid stabilization does not refund the explicitly spent optional crystal");
            Action(g,anchor.kind,"afterlight_stable_1");
            Check(g.AfterlightStableCharge&&g.save.inventory[1]==0,
                "Each fresh paid attempt consumes a genuine new crystal");
            WalkWithAfterlightCharge(g,new Point(outlet.x,outlet.y));
            Check(g.AfterlightChargeRegion==1&&g.AfterlightStableCharge,
                "The authorized longer budget still requires physically reaching the distant receiver");
            Action(g,outlet.kind,"afterlight_install_1");
            Check(g.Has("afterlight_done_1")&&g.AfterlightChargeRegion==0&&
                !g.AfterlightStableCharge&&g.save.inventory[1]==0&&
                g.save.inventory[0]==2&&g.save.inventory[2]==2,
                "Real installation pays its original wood/herb costs, not another crystal");
            g.PrepareSave();var saved=new GameCore(data,CloneSave(g.save));
            Check(saved.save.version==2&&saved.Has("afterlight_done_1")&&
                !saved.AfterlightStableCharge&&saved.save.inventory[1]==0,
                "Original v2 save persists repaired civic work without transient stable charge state");
        }

        public static void AfterlightPublicTravelAndPaidChargeCannotMaskExistingLiveFieldWork(Campaign data)
        {
            var g=Fresh(data,0);g.save.stage=3;g.save.ending=3;
            g.save.flags.Add("afterlight_started");
            g.save.flags.Add("afterlight_done_1");
            g.save.flags.Add("afterlight_plan_1_care");
            g.save.flags.Add("afterlight_steward_1");
            g.Enter(0);g.enemies.Clear();g.save.energy=100;
            WorldObject forge=Object(g,"forge"),
                starroad=Object(g,"afterlightwaypoint1");
            g.save.x=forge.x;g.save.y=forge.y;
            Action(g,forge.kind,"swordlesson_start");
            Check(g.SwordLessonActive,
                "The original blacksmith starts a real fieldwork projectile challenge");
            g.save.x=starroad.x;g.save.y=starroad.y;
            Action(g,starroad.kind,"afterlight_route_1");
            Check(g.save.room==0&&g.SwordLessonActive,
                "A working town starroad cannot erase an authentic live tutorial battle");
            g.save.x=forge.x;g.save.y=forge.y;
            Action(g,forge.kind,"swordlesson_abort");
            Check(!g.SwordLessonActive,"The original forge retires its own timed fieldwork");
            g.save.x=starroad.x;g.save.y=starroad.y;
            Action(g,starroad.kind,"afterlight_route_1");
            Check(g.save.room==1,
                "The original authenticated starroad resumes once the physical field task ends");

            var f=Fresh(data);f.save.stage=3;f.save.ending=3;
            f.save.flags.Add("afterlight_started");
            f.save.flags.Add("afterlight_plan_2_watch");
            f.save.flags.Add("floodarchive_public");
            f.save.flags.Add("scout_saved");
            f.Enter(2);f.enemies.Clear();f.save.energy=100;
            f.save.totalTime=36;
            WorldObject west=null,anchor=Object(f,"afterlightanchor2");
            foreach(WorldObject o in f.Map.objects)
                if(o.id=="floodarchive_1")west=o;
            Check(west!=null,"Original west-bank low-tide civic gauge is authored");
            f.save.x=west.x;f.save.y=west.y;
            f.Talk(west);
            int choice=Array.FindIndex(f.conversation.choices,
                c=>c.action=="floodarchive_relief_start");
            Check(choice>=0,"The actual medicine transport is available at low tide");
            f.Choose(choice);
            Check(f.FloodReliefActive,"The medical field mission is physically underway");
            f.save.x=anchor.x;f.save.y=anchor.y;
            int crystals=f.save.inventory[1];
            Action(f,anchor.kind,"afterlight_charge_2");
            Check(f.AfterlightChargeRegion==0&&f.FloodReliefActive,
                "Free afterlight transfer cannot mask an existing live timed ferry delivery");
            f.save.inventory[1]=2;
            Action(f,anchor.kind,"afterlight_stable_2");
            Check(f.AfterlightChargeRegion==0&&f.FloodReliefActive&&
                f.save.inventory[1]==2,
                "Paid stable power is also refused without spending material during other fieldwork");
        }

        public static void AfterlightRealCombatDamagesCarriedChargeWithStableDefense(Campaign data)
        {
            var g=Fresh(data);g.save.stage=3;g.save.ending=3;
            g.save.flags.Add("afterlight_started");
            g.save.flags.Add("afterlight_plan_1_care");
            g.Enter(1);g.enemies.Clear();
            WorldObject anchor=Object(g,"afterlightanchor1");
            g.save.x=anchor.x;g.save.y=anchor.y;
            g.save.energy=100;g.save.inventory[1]=1;
            Action(g,anchor.kind,"afterlight_charge_1");
            Check(g.AfterlightChargeRegion==1&&!g.AfterlightStableCharge&&
                g.AfterlightChargeIntegrity==100,
                "Original 90-second load begins whole and vulnerable to actual enemy hits");
            var hit=typeof(GameCore).GetMethod("Hurt",
                System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.NonPublic);
            Check(hit!=null,"The actual combat damage method still exists");
            g.invulnerable=0;
            float health=g.save.health;
            hit.Invoke(g,new object[]{20f,"shot",null});
            float normalLoss=100-g.AfterlightChargeIntegrity;
            Check(g.save.health<health&&normalLoss>=9.9f&&normalLoss<=30,
                "A genuine post-armor shot hurts both the hero and carried unshielded power");
            g.invulnerable=0;g.hitStop=0;
            float intact=g.AfterlightChargeIntegrity;
            hit.Invoke(g,new object[]{20f,"environment",null});
            Check(g.AfterlightChargeIntegrity==intact,
                "Environmental exposure does not fabricate hostile damage to the carried power");
            g.save.x=anchor.x;g.save.y=anchor.y;
            Action(g,anchor.kind,"afterlight_abort");
            Check(g.AfterlightChargeRegion==0&&g.AfterlightChargeIntegrity==0,
                "Genuine anchor abort retires damaged temporary charge without changing civic plans");
            g.enemies.Clear();g.save.health=g.save.maxHealth;g.invulnerable=0;
            Action(g,anchor.kind,"afterlight_stable_1");
            Check(g.AfterlightStableCharge&&g.AfterlightChargeLeft==120&&
                g.AfterlightChargeIntegrity==100&&g.save.inventory[1]==0,
                "A freshly paid real starcrystal grants its intact stabilized 120-second load");
            health=g.save.health;
            hit.Invoke(g,new object[]{20f,"shot",null});
            float stableLoss=100-g.AfterlightChargeIntegrity;
            Check(g.save.health<health&&
                Math.Abs(stableLoss-normalLoss*.5f)<.02f,
                "The paid stable charge halves the real power shock, not the hero's health damage");
            g.PrepareSave();var loaded=new GameCore(data,CloneSave(g.save));
            Check(loaded.save.version==2&&loaded.AfterlightChargeRegion==0&&
                !loaded.AfterlightStableCharge&&loaded.AfterlightChargeIntegrity==0&&
                loaded.save.inventory[1]==0,
                "Old v2 saved slots preserve spent crystal but never serialize unfinished load health");
            g.enemies.Clear();g.invulnerable=500;
            Advance(g,121f);
            Check(g.AfterlightChargeRegion==0&&g.AfterlightChargeIntegrity==0&&
                g.AfterlightMode(1)==1&&!g.Has("afterlight_done_1"),
                "Even stabilized transport expires honestly rather than inventing completed reconstruction");
        }

        public static void AfterlightDamagedReceiverRequiresPaidPhysicalSalvageBeforeInstallation(Campaign data)
        {
            var g=Fresh(data);g.save.stage=3;g.save.ending=3;
            g.save.flags.Add("afterlight_started");
            g.save.flags.Add("afterlight_plan_1_care");
            g.Enter(1);g.enemies.Clear();
            g.invulnerable=0;g.save.inventory[0]=5;g.save.inventory[2]=5;
            WorldObject anchor=Object(g,"afterlightanchor1"),
                outlet=Object(g,"afterlightoutlet1");
            g.save.x=anchor.x;g.save.y=anchor.y;
            Action(g,anchor.kind,"afterlight_charge_1");
            Check(g.AfterlightChargeRegion==1&&g.AfterlightChargeIntegrity==100,
                "Existing free 90-second transport starts with an intact civic load");
            var hurt=typeof(GameCore).GetMethod("Hurt",
                System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.NonPublic);
            Check(hurt!=null,"Real post-armor combat contact remains authoritative");
            // Stop immediately after reaching the receiver's minimum.
            // Seven hits at the real difficulty can destroy cargo outright,
            // so testing seven unconditional blows cannot exercise salvage.
            int actualHits=0;
            for(int i=0;i<10&&g.AfterlightChargeRegion==1&&
                g.AfterlightChargeIntegrity>=GameCore.AfterlightReceiverThreshold;i++)
            {
                g.save.health=g.save.maxHealth;
                g.invulnerable=0;g.hitStop=0;
                hurt.Invoke(g,new object[]{20f,"shot",null});
                actualHits++;
            }
            Check(actualHits>=2&&g.AfterlightChargeRegion==1&&
                g.AfterlightChargeIntegrity<GameCore.AfterlightReceiverThreshold&&
                g.AfterlightChargeIntegrity>0,
                "True combat left recoverable charge="+g.AfterlightChargeIntegrity+
                " hits="+actualHits+" active="+g.AfterlightChargeRegion);
            g.enemies.Clear();g.invulnerable=500;
            WalkWithAfterlightCharge(g,new Point(outlet.x,outlet.y));
            g.save.x=outlet.x;g.save.y=outlet.y;
            int herb=g.save.inventory[2],wood=g.save.inventory[0];
            Action(g,outlet.kind,"afterlight_install_1");
            Check(!g.Has("afterlight_done_1")&&g.AfterlightChargeRegion==1&&
                g.save.inventory[0]==wood&&g.save.inventory[2]==herb,
                "Low-quality charge cannot mint real public services or deduct installation materials");
            float before=g.AfterlightChargeLeft;
            Action(g,outlet.kind,"afterlight_repair_1");
            Check(g.AfterlightChargeRegion==1&&
                g.AfterlightChargeIntegrity>=35&&
                g.AfterlightChargeLeft==before-8&&
                g.save.inventory[2]==herb-1&&!g.AfterlightReceiverRepairAvailable,
                "One actual herb restores field load integrity at the remote receiver, costing 8 honest seconds");
            var choose=typeof(GameCore).GetMethod("ChooseAfterlight",
                System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.NonPublic);
            choose.Invoke(g,new object[]{"afterlight_repair_1"});
            Check(g.AfterlightChargeLeft==before-8&&g.save.inventory[2]==herb-1,
                "Forged repeated receiver repairs cannot stack time or charge material twice");
            Action(g,outlet.kind,"afterlight_install_1");
            Check(g.Has("afterlight_done_1")&&g.AfterlightChargeRegion==0&&
                g.save.inventory[0]==wood-2&&g.save.inventory[2]==herb-3,
                "Salvaged charge still consumes original two timber and two herbs when it truly powers civic infrastructure");
            g.PrepareSave();var loaded=new GameCore(data,CloneSave(g.save));
            Check(loaded.save.version==2&&loaded.Has("afterlight_done_1")&&
                loaded.AfterlightChargeRegion==0,
                "The genuinely paid reconstruction and field salvage preserve old SaveData v2 semantics");
        }
        public static void AfterlightReceiverRepairRejectsRemoteAndMaterialFreeExploits(Campaign data)
        {
            var g=Fresh(data);g.save.stage=3;g.save.ending=3;
            g.save.flags.Add("afterlight_started");
            g.save.flags.Add("afterlight_plan_3_watch");
            g.Enter(3);g.enemies.Clear();
            WorldObject anchor=Object(g,"afterlightanchor3"),
                outlet=Object(g,"afterlightoutlet3");
            g.save.x=anchor.x;g.save.y=anchor.y;g.save.inventory[2]=0;
            Action(g,anchor.kind,"afterlight_charge_3");
            var integrity=typeof(GameCore).GetField("afterlightChargeIntegrity",
                System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.NonPublic);
            Check(integrity!=null,"Temporary charge integrity exists on real run");
            integrity.SetValue(g,20f);
            var choose=typeof(GameCore).GetMethod("ChooseAfterlight",
                System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.NonPublic);
            choose.Invoke(g,new object[]{"afterlight_repair_3"});
            Check(g.AfterlightChargeIntegrity==20&&g.AfterlightChargeRegion==3,
                "A remote forged action at the source cannot heal depleted power");
            g.save.x=outlet.x;g.save.y=outlet.y;
            float seconds=g.AfterlightChargeLeft;
            choose.Invoke(g,new object[]{"afterlight_repair_3"});
            Check(g.AfterlightChargeIntegrity==20&&g.AfterlightChargeLeft==seconds,
                "Receiver repair without a real herb cannot grant free integrity or duration");
            g.save.inventory[2]=1;
            integrity.SetValue(g,35f);
            choose.Invoke(g,new object[]{"afterlight_repair_3"});
            Check(g.save.inventory[2]==1&&g.AfterlightChargeIntegrity==35,
                "Above-threshold charge cannot waste emergency herbs or stack recovery before real need");
            integrity.SetValue(g,20f);
            choose.Invoke(g,new object[]{"afterlight_repair_3"});
            Check(g.save.inventory[2]==0&&g.AfterlightChargeIntegrity==65&&
                g.AfterlightChargeLeft==seconds-8,
                "Damaged alternate civic mode also supports exactly one paid repair");
            g.PrepareSave();var loaded=new GameCore(data,CloneSave(g.save));
            Check(loaded.save.version==2&&loaded.AfterlightChargeRegion==0&&
                loaded.save.inventory[2]==0&&!loaded.Has("afterlight_done_3"),
                "A half-repaired run cannot be serialized as a completed public work");
        }

        public static void AfterlightRemoteReceiverCanDiscardHopelessLoadWithoutWipingPriorStory(Campaign data)
        {
            var g=Fresh(data);g.save.stage=3;g.save.ending=3;
            g.save.flags.Add("afterlight_started");
            g.save.flags.Add("afterlight_plan_2_watch");
            g.save.flags.Add("afterlight_done_1");
            g.Enter(2);g.enemies.Clear();
            WorldObject anchor=Object(g,"afterlightanchor2"),
                outlet=Object(g,"afterlightoutlet2");
            g.save.x=anchor.x;g.save.y=anchor.y;
            g.save.inventory[1]=4;g.save.inventory[2]=0;
            Action(g,anchor.kind,"afterlight_stable_2");
            Check(g.AfterlightChargeRegion==2&&g.AfterlightStableCharge&&
                g.save.inventory[1]==3,
                "An original post-ending civic anchor pays exactly one starcrystal for stable power");
            var integrity=typeof(GameCore).GetField("afterlightChargeIntegrity",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            Check(integrity!=null,"Temporary live civic charge state exists");
            integrity.SetValue(g,20f);
            var choose=typeof(GameCore).GetMethod("ChooseAfterlight",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            choose.Invoke(g,new object[]{"afterlight_discard_2"});
            Check(g.AfterlightChargeRegion==2&&g.AfterlightChargeIntegrity==20&&
                g.save.inventory[1]==3,
                "A forged discharge from the source cannot prematurely abandon a charged civic run");
            g.enemies.Clear();g.invulnerable=500;
            WalkWithAfterlightCharge(g,new Point(outlet.x,outlet.y));
            g.save.x=outlet.x;g.save.y=outlet.y;
            g.Talk(outlet);
            int option=Array.FindIndex(g.conversation.choices,
                c=>c.action=="afterlight_discard_2");
            Check(option>=0,"The actual physical receiver offers voluntary release for a doomed run");
            g.Choose(option);
            Check(g.AfterlightChargeRegion==0&&g.AfterlightChargeIntegrity==0&&
                !g.AfterlightStableCharge&&g.save.inventory[1]==3&&
                g.AfterlightMode(2)==2&&!g.Has("afterlight_done_2")&&
                g.Has("afterlight_done_1"),
                "Receiver withdrawal clears transient cargo only, preserving older region and planned service");
            choose.Invoke(g,new object[]{"afterlight_discard_2"});
            Check(g.save.inventory[1]==3&&!g.Has("afterlight_done_2"),
                "Repeated discard cannot refund crystals or forge completion flags");
            g.save.x=anchor.x;g.save.y=anchor.y;
            Action(g,anchor.kind,"afterlight_charge_2");
            Check(g.AfterlightChargeRegion==2&&!g.AfterlightStableCharge&&
                g.AfterlightChargeLeft==90&&g.save.inventory[1]==3,
                "After remote withdrawal the original free trip can be started without paying the stabilizing crystal again");
            g.PrepareSave();var saved=new GameCore(data,CloneSave(g.save));
            Check(saved.save.version==2&&saved.Has("afterlight_done_1")&&
                saved.AfterlightMode(2)==2&&saved.AfterlightChargeRegion==0&&
                !saved.Has("afterlight_done_2"),
                "SaveData v2 preserves earned districts and local design but never a canceled cargo run");
        }

        public static void AfterlightReservesRequireHonestPostCharterSuppliesAndImproveRealServices(Campaign data)
        {
            var g=Fresh(data);g.save.stage=3;g.save.ending=3;
            g.save.flags.Add("afterlight_started");
            for(int room=1;room<=3;room++)
            {
                g.save.flags.Add("afterlight_done_"+room);
                g.save.flags.Add("afterlight_steward_"+room);
            }
            g.save.flags.Add("afterlight_plan_1_watch");
            g.save.flags.Add("afterlight_plan_2_care");
            g.save.flags.Add("afterlight_plan_3_care");
            g.Enter(1);g.enemies.Clear();g.invulnerable=500;
            var forest=Object(g,"afterlightoutlet1");
            g.save.x=forest.x;g.save.y=forest.y;
            g.Talk(forest);
            Check(!Array.Exists(g.conversation.choices,c=>c.action=="afterlight_reserve_1"),
                "Finished regional construction alone cannot bypass publishing the entire civic charter");
            g.Close();
            g.save.flags.Add("afterlight_complete");
            g.save.inventory[0]=1;g.save.inventory[2]=1;
            Action(g,forest.kind,"afterlight_reserve_1");
            Check(!g.AfterlightReserve(1) && g.save.inventory[0]==1 &&
                g.save.inventory[2]==1,
                "Insufficient genuine timber rejects reserve upgrades without partial material theft");
            g.save.inventory[0]=2;
            Action(g,forest.kind,"afterlight_reserve_1");
            Check(g.AfterlightReserve(1)&&g.AfterlightReserves==1&&
                g.save.inventory[0]==0&&g.save.inventory[2]==0,
                "Actually investing 2 timber and 1 Moonherb installs the forest reserve once");
            g.Talk(forest);
            Check(!Array.Exists(g.conversation.choices,c=>c.action=="afterlight_reserve_1"),
                "Completed reserve cannot be purchased a second time from the actual receiver menu");
            g.Close();
            var choose=typeof(GameCore).GetMethod("ChooseAfterlight",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            Check(choose!=null,"Physical civic reserve action dispatcher remains available");
            choose.Invoke(g,new object[]{"afterlight_reserve_1"});
            Check(g.AfterlightReserves==1&&g.save.inventory[0]==0&&
                g.save.inventory[2]==0,
                "Forged repeated reserve action cannot buy the same improvement twice");
            var oldForest=CloneSave(g.save);
            oldForest.flags.Remove("afterlight_reserve_1");
            var basic=new GameCore(data,oldForest);basic.Start();
            basic.enemies.Clear();basic.invulnerable=500;
            basic.save.x=forest.x;basic.save.y=forest.y;
            g.save.energy=basic.save.energy=10;
            Advance(g,2);Advance(basic,2);
            Check(g.save.energy>basic.save.energy+3f,
                "Physically provisioned forest lamps replenish actual energy faster than ordinary unstocked lamps");
            g.save.x=basic.save.x=forest.x-5f;
            g.save.y=basic.save.y=forest.y;
            g.save.energy=basic.save.energy=10;
            Advance(g,2);Advance(basic,2);
            Check(Math.Abs(g.save.energy-basic.save.energy)<.2f,
                "Reserve improvements cannot regenerate energy anywhere in the region");

            g.Enter(2);g.enemies.Clear();g.invulnerable=500;
            var ruins=Object(g,"afterlightoutlet2");
            var quay=Object(g,"afterlightanchor2");
            g.save.x=quay.x;g.save.y=quay.y;
            g.save.inventory[0]=1;g.save.inventory[2]=2;
            choose.Invoke(g,new object[]{"afterlight_reserve_2"});
            Check(!g.AfterlightReserve(2),
                "Forged action at original ferry anchor cannot stock the far receiver");
            g.save.x=ruins.x;g.save.y=ruins.y;
            g.enemies.Add(new Enemy(88995,0,ruins.x+.7f,ruins.y,30));
            choose.Invoke(g,new object[]{"afterlight_reserve_2"});
            Check(!g.AfterlightReserve(2)&&g.save.inventory[2]==2,
                "An active nearby patrol blocks material provisioning instead of allowing midcombat escape");
            g.enemies.Clear();
            Action(g,ruins.kind,"afterlight_reserve_2");
            Check(g.AfterlightReserve(2)&&g.AfterlightReserves==2&&
                g.save.inventory[0]==0&&g.save.inventory[2]==0,
                "The second region consumes one timber and two herbs exactly once");
            var oldRuins=CloneSave(g.save);
            oldRuins.flags.Remove("afterlight_reserve_2");
            var ordinary=new GameCore(data,oldRuins);ordinary.Start();
            ordinary.enemies.Clear();ordinary.invulnerable=500;
            g.save.x=ordinary.save.x=quay.x;
            g.save.y=ordinary.save.y=quay.y;
            g.save.health=ordinary.save.health=Math.Min(g.save.maxHealth-40,40);
            Advance(g,2);Advance(ordinary,2);
            Check(g.save.health>ordinary.save.health+2f,
                "Stocked public ferry establishes actual out-of-combat healing at the original far quay");
            g.save.x=ordinary.save.x=quay.x+5f;
            g.save.y=ordinary.save.y=quay.y;
            g.save.health=ordinary.save.health=40;
            Advance(g,2);Advance(ordinary,2);
            Check(Math.Abs(g.save.health-ordinary.save.health)<.2f,
                "Ferry reserve healing remains tightly local, not a remote map-wide passive bonus");

            g.Enter(3);g.enemies.Clear();g.invulnerable=500;
            var mine=Object(g,"afterlightoutlet3");
            g.save.x=mine.x;g.save.y=mine.y;
            g.save.inventory[1]=2;g.save.inventory[4]=1;
            Action(g,mine.kind,"afterlight_reserve_3");
            Check(g.AfterlightReserve(3)&&g.AfterlightReserves==3 &&
                g.save.inventory[1]==0&&g.save.inventory[4]==0,
                "Mine emergency investment really spends two crystals and one machine part");
            g.save.inventory[0]=1;g.save.inventory[1]=2;
            g.Talk(mine);
            Check(Array.Exists(g.conversation.choices,c=>c.action=="afterlight_craft" &&
                c.en.Contains("3 repair parts")),
                "Public workshop dialogue reflects the actual enhanced three-part recipe");
            g.Close();
            Action(g,mine.kind,"afterlight_craft");
            Check(g.save.inventory[0]==0&&g.save.inventory[1]==0 &&
                g.save.inventory[4]==3,
                "A stocked mine workshop produces one extra usable part per genuinely paid batch");
            g.PrepareSave();var restored=new GameCore(data,CloneSave(g.save));restored.Start();
            Check(restored.save.version==2&&restored.AfterlightReserves==3&&
                restored.AfterlightReserve(3)&&restored.Has("afterlight_complete"),
                "All optional civic upgrades survive the original v2 save format and leave original charter intact");
        }

        public static void DefendedReceiversImproveOnlyTheirRealLocalPublicServices(Campaign data)
        {
            // Guarded forest lamps consume exactly the same existing local
            // service and never grant a whole-map combat or stamina boon.
            var forest=Fresh(data);forest.save.stage=3;forest.save.ending=3;
            foreach(string flag in new[]{"afterlight_started","afterlight_complete",
                "afterlight_done_1","afterlight_plan_1_watch","afterlight_reserve_1"})
                forest.save.flags.Add(flag);
            forest.Enter(1);forest.enemies.Clear();
            var lamp=Object(forest,"afterlightoutlet1");
            forest.save.x=lamp.x;forest.save.y=lamp.y;forest.invulnerable=100;
            forest.save.energy=10;Advance(forest,1f);
            float ordinaryLamp=forest.save.energy;
            forest.save.flags.Add("afterlight_watch_done_1");
            forest.save.energy=10;Advance(forest,1f);
            Check(Math.Abs(forest.save.energy-ordinaryLamp-1.5f)<.07f,
                "First physically defended forest adds exactly 1.5 renewable local energy per second");
            forest.save.x=lamp.x+5;forest.save.y=lamp.y;
            forest.save.energy=10;Advance(forest,1f);
            float farEnergy=forest.save.energy;
            forest.save.flags.Remove("afterlight_watch_done_1");
            forest.save.energy=10;Advance(forest,1f);
            Check(Math.Abs(forest.save.energy-farEnergy)<.07f,
                "Forest guard patrol does not leak a permanent stamina buff outside the actual lantern");
            // The same forest's caring nursery improves actual safe healing,
            // but never manufactures health in a hostile combat zone.
            var garden=Fresh(data);garden.save.stage=3;garden.save.ending=3;
            foreach(string flag in new[]{"afterlight_started","afterlight_complete",
                "afterlight_done_1","afterlight_plan_1_care","afterlight_reserve_1"})
                garden.save.flags.Add(flag);
            garden.Enter(1);garden.enemies.Clear();
            var nursery=Object(garden,"afterlightoutlet1");
            garden.save.x=nursery.x;garden.save.y=nursery.y;garden.invulnerable=100;
            garden.save.health=40;Advance(garden,1f);
            float unguarded= garden.save.health;
            garden.save.flags.Add("afterlight_watch_done_1");
            garden.save.health=40;Advance(garden,1f);
            Check(Math.Abs((garden.save.health-unguarded)-1f)<.07f,
                "Care-focused guarded forest refuge gains a genuinely earned +1 HP/s");
            garden.enemies.Add(new Enemy(99761,0,nursery.x+.8f,nursery.y,70));
            garden.save.health=40;Advance(garden,1f);
            Check(Math.Abs(garden.save.health-40)<.07f,
                "Guarded public nursery cannot heal player during real nearby enemy combat");

            // Care-mode Drowned Ruins ferry has TWO real terminals; defense
            // achievement upgrades both and is never applied out of range.
            var ferry=Fresh(data);ferry.save.stage=3;ferry.save.ending=3;
            foreach(string flag in new[]{"afterlight_started","afterlight_complete",
                "afterlight_done_2","afterlight_plan_2_care","afterlight_reserve_2"})
                ferry.save.flags.Add(flag);
            ferry.Enter(2);ferry.enemies.Clear();ferry.invulnerable=100;
            var dock=Object(ferry,"afterlightoutlet2");
            var farDock=Object(ferry,"afterlightanchor2");
            foreach(WorldObject endpoint in new[]{dock,farDock})
            {
                ferry.save.x=endpoint.x;ferry.save.y=endpoint.y;
                ferry.save.health=40;Advance(ferry,1f);float before=ferry.save.health;
                ferry.save.flags.Add("afterlight_watch_done_2");
                ferry.save.health=40;Advance(ferry,1f);
                Check(Math.Abs((ferry.save.health-before)-1f)<.07f,
                    "Signed and defended ferry's local healing increases at both genuine terminals");
                ferry.save.flags.Remove("afterlight_watch_done_2");
            }

            // A staffed mine's parts bonus runs through the ORIGINAL paid
            // recipe. Stack cap is checked before consuming timber/crystal.
            var mine=Fresh(data);mine.save.stage=3;mine.save.ending=3;
            foreach(string flag in new[]{"afterlight_started","afterlight_complete",
                "afterlight_done_3","afterlight_plan_3_care","afterlight_reserve_3"})
                mine.save.flags.Add(flag);
            mine.Enter(3);mine.enemies.Clear();
            var foundry=Object(mine,"afterlightoutlet3");
            mine.save.x=foundry.x;mine.save.y=foundry.y;
            mine.save.inventory[0]=5;mine.save.inventory[1]=10;mine.save.inventory[4]=0;
            Action(mine,foundry.kind,"afterlight_craft");
            Check(mine.save.inventory[0]==4&&mine.save.inventory[1]==8&&
                mine.save.inventory[4]==3,
                "Before actual civic defense the legitimate paid workshop keeps original reserve output");
            mine.save.flags.Add("afterlight_watch_done_3");
            mine.Talk(foundry);
            Check(Array.Exists(mine.conversation.choices,c=>c.action=="afterlight_craft" &&
                c.en.Contains("4")),
                "Live workshop choice advertises its earned actual four-part batch, not the old yield");
            mine.Close();
            Action(mine,foundry.kind,"afterlight_craft");
            Check(mine.save.inventory[0]==3&&mine.save.inventory[1]==6&&
                mine.save.inventory[4]==7,
                "First real mine defense adds one physical machine part to a paid materials batch");
            mine.save.inventory[4]=9996;
            int wood=mine.save.inventory[0],crystals=mine.save.inventory[1];
            Action(mine,foundry.kind,"afterlight_craft");
            Check(mine.save.inventory[0]==wood&&mine.save.inventory[1]==crystals&&
                mine.save.inventory[4]==9996,
                "Four-part guarded output correctly refuses a near-full inventory before any ingredient spending");
            mine.PrepareSave();
            var reloaded=new GameCore(data,CloneSave(mine.save));reloaded.Start();
            Check(reloaded.save.version==2&&reloaded.AfterlightWatchDone(3)&&
                reloaded.AfterlightReserve(3),
                "Guarded production upgrades persist in original v2 save flags, with no changed schema");
        }

        public static void CivicWatchPlacementAvoidsBystandersAndCompanionOnRealGround(Campaign data)
        {
            var choose=typeof(GameCore).GetMethod("AfterlightWatchSpawnPoint",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            Check(choose!=null,"The actual civic defense uses its own live physical spawn preflight");
            for(int room=1;room<=3;room++)
            {
                var g=Fresh(data);g.save.stage=3;g.save.ending=3;
                g.Enter(room);g.enemies.Clear();
                g.save.flags.Add("companion_1");
                g.SelectCompanion(1);
                var receiver=Object(g,"afterlightoutlet"+room);
                g.save.x=receiver.x;g.save.y=receiver.y;
                object[] first={0,1,new Point(-100,-100),new Point()};
                Check((bool)choose.Invoke(g,first),
                    "Each of the three authentic civic receivers supports a legal first-wave combat foothold");
                Point original=(Point)first[3];
                Check(g.CanStand(original),
                    "The physical receiver spawn preflight returns an actually standing actor root");
                var living=new Enemy(92960+room,0,original.x,original.y,900f)
                    {stun=200f,timer=1000f};
                g.enemies.Add(living);
                object[] alternate={0,1,new Point(-100,-100),new Point()};
                Check((bool)choose.Invoke(g,alternate),
                    "A legitimate receiver can choose a second local foot instead of spawning on a living bystander");
                Point second=(Point)alternate[3];
                Check((second-original).Length>=1.55f,
                    "A real hostile actor must displace new reinforcements, not become an overlapping sprite");
                g.companionPosition=second;
                object[] companionAvoided={0,1,new Point(-100,-100),new Point()};
                Check((bool)choose.Invoke(g,companionAvoided),
                    "Civic watch can find another genuine nearby foot while a recruited ally guards the prior one");
                Point third=(Point)companionAvoided[3];
                Check(g.CanStand(third)&&
                    (third-original).Length>=1.55f&&
                    (third-second).Length>=1.55f&&
                    (third-g.Position).Length>1.7f&&
                    g.enemies.Contains(living)&&g.save.version==2,
                    "All three regions avoid the hero, living enemy and canonical companion without displacing actors or changing old saves");
            }
        }

        public static void CivicWatchCrowdedSecondWaveKeepsRealFirstVictoryAndOriginalTimer(Campaign data)
        {
            var g=Fresh(data);g.save.stage=3;g.save.ending=3;
            foreach(string flag in new[]{"afterlight_started",
                "afterlight_complete","afterlight_done_3",
                "afterlight_steward_3","afterlight_reserve_3",
                "afterlight_plan_3_watch"})g.save.flags.Add(flag);
            g.Enter(3);g.enemies.Clear();g.invulnerable=180f;
            WorldObject outlet=Object(g,"afterlightoutlet3");
            g.save.x=outlet.x;g.save.y=outlet.y;
            g.Talk(outlet);
            Action(g,outlet.kind,"afterlight_watch_start_3");
            Check(g.AfterlightWatchActive&&g.AfterlightWatchWave==1&&
                g.enemies.Count==2,
                "An actual stocked Ironreach receiver starts the first real two-raider wave");
            var hit=typeof(GameCore).GetMethod("Hit",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            Check(hit!=null,"The original production lethal contact pathway exists");
            var first=g.enemies.FindAll(e=>e.hp>0);
            float oldCoins=g.save.coins,oldShards=g.save.shards;
            foreach(Enemy guard in first)
                hit.Invoke(g,new object[]{guard,99999f,false,null,null});
            Check(g.AfterlightWatchWave==1&&g.enemies.Count==0,
                "Both authentic first-wave deaths are accounted for but do not prematurely pay defense rewards");
            // Park living, combat-disabled ambient actors on ALL authored
            // second-wave tiles. The room must not invent a fake spare foot.
            Point[] offsets={new Point(-3f,0),new Point(3f,0),
                new Point(0,-3f),new Point(0,3f),
                new Point(-3f,-2f),new Point(3f,-2f),
                new Point(-3f,2f),new Point(3f,2f),
                new Point(-4f,1f),new Point(4f,1f),
                new Point(1f,-4f),new Point(1f,4f),
                new Point(-2f,-4f),new Point(2f,4f),
                new Point(4f,-2f),new Point(-4f,2f)};
            var crowded=new System.Collections.Generic.List<Enemy>();
            foreach(Point offset in offsets)
            {
                Point p=new Point(outlet.x,outlet.y)+offset;
                if(!g.CanStand(p))continue;
                var occupant=new Enemy(92800+crowded.Count,0,
                    p.x,p.y,900){stun=200f,timer=1000f};
                g.enemies.Add(occupant);crowded.Add(occupant);
            }
            Check(crowded.Count>=5,
                "The authored real mine receiver has multiple otherwise legal reinforcement feet occupied by world actors");
            float before=g.AfterlightWatchTime;
            g.hitStop=0;g.Tick(.05f,new Point());
            Check(g.AfterlightWatchActive&&g.AfterlightWatchWave==1&&
                g.enemies.Count==crowded.Count&&
                !g.AfterlightWatchDone(3)&&
                g.save.coins==oldCoins&&g.save.shards==oldShards&&
                g.AfterlightWatchTime<before-.04f,
                "Crowded real receiver preserves two earned first-wave kills and original countdown without paying a premature reward");
            float held=g.AfterlightWatchTime;
            g.Tick(.05f,new Point());
            Check(g.AfterlightWatchActive&&g.AfterlightWatchWave==1&&
                g.AfterlightWatchTime<held-.04f&&
                g.enemies.Count==crowded.Count,
                "Repeated wave-two deployment failure neither restarts the live fight nor freezes its authentic timer");
            g.Talk(outlet);
            Check(g.mode=="dialog"&&g.conversation!=null&&
                g.conversation.text.Contains("第一波已击退")&&
                g.conversation.textEn.Contains("First wave defeated")&&
                g.conversation.textEn.Contains("countdown"),
                "Actual bilingual receiver UI explains the deferred second wave and honest continuing timer instead of displaying ghost enemies");
            g.Close();
            foreach(Enemy occupant in crowded)g.enemies.Remove(occupant);
            g.Tick(.05f,new Point());
            Check(g.AfterlightWatchActive&&g.AfterlightWatchWave==2&&
                g.enemies.Count==2&&!g.AfterlightWatchDone(3),
                "Freeing genuine receiver footing spawns both complete second-wave enemies exactly once");
            var second=g.enemies.FindAll(e=>e.hp>0);
            foreach(Enemy guard in second)
                hit.Invoke(g,new object[]{guard,99999f,false,null,null});
            g.hitStop=0;g.Tick(.05f,new Point());
            Check(g.AfterlightWatchDone(3)&&!g.AfterlightWatchActive&&
                g.save.coins==oldCoins+40&&g.save.shards==oldShards+1&&
                g.save.version==2,
                "Only four real lethal hits across both waves grant the genuine one-time Ironreach public defense reward");
        }

        public static void CivicWatchStaleReceiverMenuCannotStartPatchOrWithdrawThroughWall(Campaign data)
        {
            MapData original=data.maps[3];
            try
            {
                var rows=new string[30];
                for(int y=0;y<30;y++)
                {
                    char[] cells=new string('#',30).ToCharArray();
                    if(y>=2&&y<=27)for(int x=2;x<=27;x++)cells[x]='.';
                    rows[y]=new string(cells);
                }
                WorldObject receiver=new WorldObject{
                    id="afterlightoutlet3",kind="afterlightoutlet3",
                    name="Public civic receiver",en="Public civic receiver",
                    x=10.5f,y=10.5f};
                data.maps[3]=new MapData{id=3,spawnX=12.25f,
                    spawnY=10.5f,rows=rows,objects=new[]{receiver},
                    decor=new Decoration[0]};
                var g=Fresh(data);g.save.stage=3;g.save.ending=3;
                foreach(string flag in new[]{"afterlight_started",
                    "afterlight_complete","afterlight_done_3",
                    "afterlight_steward_3","afterlight_reserve_3",
                    "afterlight_plan_3_watch"})g.save.flags.Add(flag);
                g.Enter(3);g.enemies.Clear();g.invulnerable=180;
                g.save.x=12.25f;g.save.y=10.5f;
                g.save.inventory[0]=2;g.save.inventory[2]=2;
                g.Talk(receiver);
                Check(g.mode=="dialog"&&g.conversation!=null,
                    "The real stocked receiver initially offers a live watch drill on accessible ground");
                char[] wall=rows[10].ToCharArray();
                wall[11]='#';rows[10]=new string(wall);
                Check(g.CanStand(g.Position)&&
                    g.CanStand(new Point(receiver.x,receiver.y))&&
                    g.Blocked(11.5f,10.5f),
                    "A complete physical wall divides an otherwise near public terminal and canonical hero");
                int first=-1;
                for(int i=0;i<g.conversation.choices.Length;i++)
                    if(g.conversation.choices[i].action=="afterlight_watch_start_3")
                    {first=i;break;}
                Check(first>=0,
                    "Before the dynamic closing wall the original real defense start was offered");
                g.Choose(first);
                Check(!g.AfterlightWatchActive&&g.enemies.Count==0,
                    "A cached start button cannot conjure civic raiders on inaccessible terminal ground");
                wall[11]='.';rows[10]=new string(wall);
                g.Talk(receiver);first=-1;
                for(int i=0;i<g.conversation.choices.Length;i++)
                    if(g.conversation.choices[i].action=="afterlight_watch_start_3")
                    {first=i;break;}
                Check(first>=0,"Clearing the same wall restores the real start action");
                g.Choose(first);
                Check(g.AfterlightWatchActive&&g.AfterlightWatchWave==1&&
                    g.enemies.Count==2,
                    "The authentic citizen reserve defense now launches exactly two attackers");
                var sabotage=typeof(GameCore).GetMethod("DamageAfterlightWatchStock",
                    System.Reflection.BindingFlags.Instance|
                    System.Reflection.BindingFlags.NonPublic);
                Check(sabotage!=null,
                    "Actual production stock damage is an authenticated enemy attack callback");
                Enemy raider=g.enemies[0];
                raider.x=receiver.x+.8f;raider.y=receiver.y;
                raider.targetingAfterlightStock=true;
                sabotage.Invoke(g,new object[]{raider});
                Check(g.AfterlightWatchStock<100&&g.AfterlightWatchStock>60,
                    "A real owned raider's original receiver attack damages the active public stock");
                g.Close();g.Talk(receiver);
                int patch=-1;
                for(int i=0;i<g.conversation.choices.Length;i++)
                    if(g.conversation.choices[i].action=="afterlight_watch_patch_3")
                    {patch=i;break;}
                Check(patch>=0,"Genuine damaged reserve offers the original material-consuming repair");
                int wood=g.save.inventory[0],herb=g.save.inventory[2];
                float damaged=g.AfterlightWatchStock;
                wall[11]='#';rows[10]=new string(wall);
                g.Choose(patch);
                Check(g.AfterlightWatchActive&&g.AfterlightWatchStock==damaged&&
                    g.save.inventory[0]==wood&&g.save.inventory[2]==herb,
                    "A stale repair command cannot spend one timber and one herb through solid receiver cover");
                wall[11]='.';rows[10]=new string(wall);
                g.Talk(receiver);patch=-1;
                for(int i=0;i<g.conversation.choices.Length;i++)
                    if(g.conversation.choices[i].action=="afterlight_watch_patch_3")
                    {patch=i;break;}
                Check(patch>=0,"After opening physical access the same repair becomes available");
                g.Choose(patch);
                Check(g.AfterlightWatchStock>damaged&&
                    g.save.inventory[0]==wood-1&&g.save.inventory[2]==herb-1,
                    "The real live receiver accepts exactly one resource transaction only when accessible");
                g.Talk(receiver);int abort=-1;
                for(int i=0;i<g.conversation.choices.Length;i++)
                    if(g.conversation.choices[i].action=="afterlight_watch_abort_3")
                    {abort=i;break;}
                Check(abort>=0,"The actual defense exposes the existing safe withdrawal");
                wall[11]='#';rows[10]=new string(wall);
                g.Choose(abort);
                Check(g.AfterlightWatchActive&&g.enemies.Count==2,
                    "A stale withdrawal behind stone cannot silently despawn live enemies");
                wall[11]='.';rows[10]=new string(wall);
                g.Talk(receiver);abort=-1;
                for(int i=0;i<g.conversation.choices.Length;i++)
                    if(g.conversation.choices[i].action=="afterlight_watch_abort_3")
                    {abort=i;break;}
                Check(abort>=0,"Restored accessible receiver still permits intentional withdrawal");
                g.Choose(abort);
                Check(!g.AfterlightWatchActive&&g.enemies.Count==0&&
                    !g.AfterlightWatchDone(3)&&g.save.version==2,
                    "After reopening, manual withdrawal retires only drill actors without minting a defense clear or changing SaveData");
            }
            finally{data.maps[3]=original;}
        }

        public static void AfterlightCivicDefenseRequiresTwoOwnedCombatWavesAndRealFirstClear(Campaign data)
        {
            var g=Fresh(data);g.save.stage=3;g.save.ending=3;
            g.save.flags.Add("afterlight_started");
            g.save.flags.Add("afterlight_complete");
            for(int room=1;room<=3;room++)
            {
                g.save.flags.Add("afterlight_done_"+room);
                g.save.flags.Add("afterlight_steward_"+room);
                g.save.flags.Add("afterlight_reserve_"+room);
                g.save.flags.Add("afterlight_plan_"+room+"_watch");
            }
            var hit=typeof(GameCore).GetMethod("Hit",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            Check(hit!=null,"Live combat contact and lethal-death pathway available");
            for(int room=1;room<=3;room++)
            {
                g.Enter(room);g.enemies.Clear();g.invulnerable=1000;
                var receiver=Object(g,"afterlightoutlet"+room);
                g.save.x=receiver.x;g.save.y=receiver.y;
                int originalCoins=g.save.coins,originalShards=g.save.shards;
                int originalKilled=g.save.kills[room];
                g.Talk(receiver);
                Check(Array.Exists(g.conversation.choices,c=>c.action=="afterlight_watch_start_"+room),
                    "Repaired, stocked civic receiver exposes genuine playable defense");
                g.Close();
                Action(g,receiver.kind,"afterlight_watch_start_"+room);
                Check(g.AfterlightWatchActive&&g.AfterlightWatchWave==1&&
                    g.AfterlightWatchTime>99&&g.enemies.Count==2,
                    "The original receiver really spawns two live physical attackers in wave one");
                Check(g.CanStand(g.enemies[0].Position)&&g.CanStand(g.enemies[1].Position)&&
                    (g.enemies[0].Position-g.enemies[1].Position).Length>1f &&
                    g.NavigationPath(g.enemies[0].Position).Count>0 &&
                    g.NavigationPath(g.enemies[1].Position).Count>0,
                    "Both authored enemies occupy separate genuinely reachable map tiles, not isolated rooms beyond blocked terrain");
                g.Enter(0);
                Check(g.save.room==room&&g.AfterlightWatchActive,
                    "Live civic combat cannot be skipped with region travel");
                g.ReturnToTown();
                Check(g.save.room==room,"Return-to-town cannot erase an unfinished defense");
                g.PrepareSave();
                Check(g.save.worldEnemies!=null&&g.save.worldEnemies.Length==0,
                    "Temporary civic attackers never poison version-two world snapshots");
                var resumed=new GameCore(data,CloneSave(g.save));resumed.Start();
                Check(!resumed.AfterlightWatchActive&&!resumed.AfterlightWatchDone(room)&&
                    resumed.enemies.Count==0,
                    "Saving during a defense never resurrects trial enemies or fabricates reward flags");
                // Calling the actual lethal-contact transaction is essential:
                // deleting enemy list entries is NOT a victory.
                Enemy lost=g.enemies[0];g.enemies.Remove(lost);
                g.Tick(.02f,new Point());
                Check(!g.AfterlightWatchActive&&!g.AfterlightWatchDone(room)&&
                    g.save.coins==originalCoins&&g.save.shards==originalShards,
                    "Missing enemy without an authentic lethal hit aborts the attempt reward-free");
                g.enemies.Clear();
                Action(g,receiver.kind,"afterlight_watch_start_"+room);
                Check(g.AfterlightWatchActive&&g.enemies.Count==2,
                    "Interrupted actual civic defense can be restarted on site");
                for(int wave=1;wave<=2;wave++)
                {
                    Check(g.AfterlightWatchWave==wave&&g.enemies.Count==2,
                        "Each of the two authored waves fields two real enemies");
                    int expectedA=wave==1?(room==1?0:room==2?1:2):2;
                    int expectedB=wave==1?(room==1?1:0):(room==1?0:1);
                    Check(g.enemies[0].kind==expectedA&&g.enemies[1].kind==expectedB,
                        "Forest, ruined crossing and mine each use their own real enemy tactics across both waves");
                    Enemy a=g.enemies[0], b=g.enemies[1];
                    hit.Invoke(g,new object[]{a,2000f,false,null,null});
                    Check(g.AfterlightWatchActive&&g.AfterlightWatchWave==wave &&
                        !g.AfterlightWatchDone(room),
                        "A single genuine hit kill cannot fake clearing an entire wave");
                    hit.Invoke(g,new object[]{b,2000f,false,null,null});
                    Check(!g.AfterlightWatchDone(room),
                        "The last lethal contact must be settled through the production tick");
                    // Genuine hits trigger a 25ms pause. Consume hit-stop
                    // before expecting the next gameplay wave to activate.
                    g.Tick(.08f,new Point());
                }
                Check(g.AfterlightWatchDone(room)&&!g.AfterlightWatchActive&&
                    g.enemies.Count==0 &&g.save.coins==originalCoins+25+room*5 &&
                    g.save.shards==originalShards+1&&
                    g.save.kills[room]==originalKilled,
                    "Exactly two full real waves award the one-time civic defense reward without farming ordinary kills, loot or bounty");
                g.PrepareSave();var loaded=new GameCore(data,CloneSave(g.save));loaded.Start();
                Check(loaded.save.version==2&&loaded.AfterlightWatchDone(room)&&
                    !loaded.AfterlightWatchActive,
                    "One-time civic defense clearance survives reload without transient attackers");
                int earnedCoins=g.save.coins,earnedShards=g.save.shards,earnedXP=g.save.xp;
                Action(g,receiver.kind,"afterlight_watch_start_"+room);
                Check(g.AfterlightWatchActive&&g.AfterlightWatchWave==1,
                    "Completed defenses offer actual replayable combat");
                for(int wave=1;wave<=2;wave++)
                {
                    hit.Invoke(g,new object[]{g.enemies[0],2000f,false,null,null});
                    hit.Invoke(g,new object[]{g.enemies[0],2000f,false,null,null});
                    g.Tick(.08f,new Point());
                }
                Check(!g.AfterlightWatchActive&&g.save.coins==earnedCoins&&
                    g.save.shards==earnedShards&&g.save.xp==earnedXP,
                    "Veteran repeats yield no additional coins, shards or quest XP");
            }
            Check(g.AfterlightWatchesCleared==3&&g.Has("afterlight_watch_all")&&
                g.save.skillPoints>=1,
                "All three genuine public-defense victories award exactly one campaign-wide talent point");
        }

        public static void AfterlightDefenseRequiresStockAndSupportsHonestWithdrawal(Campaign data)
        {
            var g=Fresh(data);g.save.stage=3;g.save.ending=3;
            g.save.flags.Add("afterlight_started");g.save.flags.Add("afterlight_complete");
            g.save.flags.Add("afterlight_done_2");g.save.flags.Add("afterlight_steward_2");
            g.save.flags.Add("afterlight_plan_2_care");
            g.Enter(2);g.enemies.Clear();g.invulnerable=400;
            var outlet=Object(g,"afterlightoutlet2");
            g.save.x=outlet.x;g.save.y=outlet.y;
            g.Talk(outlet);
            Check(!Array.Exists(g.conversation.choices,c=>c.action=="afterlight_watch_start_2"),
                "Without real purchased reserve the public cannot start the optional defense");
            g.Close();
            var choose=typeof(GameCore).GetMethod("ChooseAfterlight",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            choose.Invoke(g,new object[]{"afterlight_watch_start_2"});
            Check(!g.AfterlightWatchActive,"Forged start before stocking is rejected");
            g.save.flags.Add("afterlight_reserve_2");
            var priorTrial=typeof(GameCore).GetField("outerPatrolRoom",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            Check(priorTrial!=null,"Original relief patrol mission state remains accessible");
            priorTrial.SetValue(g,2);
            Action(g,outlet.kind,"afterlight_watch_start_2");
            Check(!g.AfterlightWatchActive&&g.enemies.Count==0&&
                g.toast.Contains("现场任务"),
                "A live older rescue-outpost defense is rejected with clear player-facing instructions");
            priorTrial.SetValue(g,-1);
            Action(g,outlet.kind,"afterlight_watch_start_2");
            Check(g.AfterlightWatchActive&&g.enemies.Count==2,
                "On-site stocked receiver starts live defense after actual upgrade");
            // Production-generated ranged shots and their warnings belong to
            // this exact fight, not ambient enemies already on the map.
            var fire=typeof(GameCore).GetMethod("Fire",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            var tag=typeof(GameCore).GetMethod("TagEnemyCombatEffect",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            var fade=typeof(GameCore).GetMethod("AddProjectileFade",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            Check(fire!=null&&tag!=null&&fade!=null,
                "Actual enemy arrow release and shot effect ownership paths exist");
            Enemy archer=g.enemies.Find(e=>e.kind==1);
            Check(archer!=null,"Ruins opening wave contains a real archer");
            fire.Invoke(g,new object[]{archer,new Point(1,0),6f});
            Projectile owned=g.projectiles[g.projectiles.Count-1];
            Check(owned.afterlightWatchOwned,
                "Authentic enemy Fire marks the released arrow as belonging to this civic defense");
            var unrelatedShot=new Projectile{position=new Point(15,15),velocity=new Point(1,0),
                life=8,damage=1,source="ambient"};
            g.projectiles.Add(unrelatedShot);
            var ownedSignal=new Effect("enemyRelease",archer.Position,3f);
            tag.Invoke(g,new object[]{ownedSignal,archer,false});
            Check(ownedSignal.afterlightWatchOwned,
                "Real enemy attack warnings retain their specific encounter ownership");
            g.effects.Add(ownedSignal);
            fade.Invoke(g,new object[]{owned});
            Check(g.effects.Exists(e=>e.kind=="projectileFade"&&e.afterlightWatchOwned),
                "Actual expired projectile visual remains owned until cleanup");
            var unrelatedSignal=new Effect("discover",g.Position,3f);
            g.effects.Add(unrelatedSignal);
            Action(g,outlet.kind,"afterlight_watch_abort_2");
            Check(!g.AfterlightWatchActive&&g.enemies.Count==0 &&
                !g.AfterlightWatchDone(2)&&g.AfterlightReserve(2)&&
                !g.projectiles.Contains(owned)&&g.projectiles.Contains(unrelatedShot)&&
                !g.effects.Contains(ownedSignal)&&
                !g.effects.Exists(e=>e.kind=="projectileFade"&&e.afterlightWatchOwned)&&
                g.effects.Contains(unrelatedSignal),
                "Withdrawal removes real duel arrows, releases and fades without erasing unrelated ambient shots and effects");
            g.projectiles.Clear();g.effects.Clear();
            Action(g,outlet.kind,"afterlight_watch_start_2");
            var time=typeof(GameCore).GetField("afterlightWatchTime",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            Check(time!=null,"Authentic defense clock exists");
            time.SetValue(g,.01f);g.Tick(.02f,new Point());
            Check(!g.AfterlightWatchActive&&g.enemies.Count==0&&
                !g.AfterlightWatchDone(2)&&g.AfterlightReserve(2),
                "Timeout retires real waves without forging a region victory or deleting saved infrastructure");
        }

        public static void CivicReserveEmergencyPatchUsesRealSuppliesOncePerActiveDefense(Campaign data)
        {
            var g=Fresh(data);g.save.stage=3;g.save.ending=3;
            g.save.flags.Add("afterlight_started");
            g.save.flags.Add("afterlight_complete");
            g.save.flags.Add("afterlight_done_1");
            g.save.flags.Add("afterlight_steward_1");
            g.save.flags.Add("afterlight_reserve_1");
            g.save.flags.Add("afterlight_plan_1_watch");
            g.Enter(1);g.enemies.Clear();g.invulnerable=1000;
            WorldObject station=Object(g,"afterlightoutlet1");
            g.save.x=station.x;g.save.y=station.y;
            Action(g,station.kind,"afterlight_watch_start_1");
            var stock=typeof(GameCore).GetField("afterlightWatchStock",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            var choose=typeof(GameCore).GetMethod("ChooseAfterlight",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            Check(stock!=null&&choose!=null&&g.AfterlightWatchActive,
                "A real active defense exposes its transient receiver integrity and actions");
            stock.SetValue(g,40f);
            g.save.inventory[0]=0;g.save.inventory[2]=1;
            g.Talk(station);
            Check(Array.Exists(g.conversation.choices,c=>c.action=="afterlight_watch_patch_1"),
                "Damaged live receiver offers an emergency on-site material repair");
            g.Close();
            choose.Invoke(g,new object[]{"afterlight_watch_patch_1"});
            Check(g.AfterlightWatchStock==40f&&g.save.inventory[2]==1,
                "A patch missing genuine timber cannot steal an herb or mint stock health");
            g.save.inventory[0]=1;g.save.x=station.x+5f;
            choose.Invoke(g,new object[]{"afterlight_watch_patch_1"});
            Check(g.AfterlightWatchStock==40f&&g.save.inventory[0]==1,
                "Remote spoofed patch cannot repair distant infrastructure without standing at receiver");
            g.save.x=station.x;g.save.y=station.y;
            Action(g,station.kind,"afterlight_watch_patch_1");
            Check(g.AfterlightWatchStock==70f&&g.save.inventory[0]==0&&
                g.save.inventory[2]==0&&!g.AfterlightWatchPatchAvailable,
                "Emergency patch spends exactly one real timber and herb for 30 stock integrity once");
            g.save.inventory[0]=3;g.save.inventory[2]=3;
            stock.SetValue(g,50f);
            g.Talk(station);
            Check(!Array.Exists(g.conversation.choices,c=>c.action=="afterlight_watch_patch_1"),
                "The one-use repair disappears from actual receiver dialogue");
            g.Close();
            choose.Invoke(g,new object[]{"afterlight_watch_patch_1"});
            Check(g.AfterlightWatchStock==50f&&g.save.inventory[0]==3&&
                g.save.inventory[2]==3,
                "Forged repeat patch cannot heal or consume more paid materials");
            Action(g,station.kind,"afterlight_watch_abort_1");
            Check(!g.AfterlightWatchActive&&g.AfterlightReserve(1)&&
                !g.AfterlightWatchDone(1)&&g.save.inventory[0]==3,
                "Withdrawal preserves permanent reserve and remaining inventory without free completion");
            g.enemies.Clear();
            Action(g,station.kind,"afterlight_watch_start_1");
            Check(g.AfterlightWatchActive&&g.AfterlightWatchStock==100f,
                "A separate new defense attempt begins at full temporary stock integrity");
            stock.SetValue(g,80f);
            Check(g.AfterlightWatchPatchAvailable,
                "One-use repair availability resets for the next real defense, never the old attempt");
        }

        public static void CivicReserveSaboteursMustReachReceiverAndCommitRealWindupBeforeDestroyingStock(Campaign data)
        {
            var g=Fresh(data);g.save.stage=3;g.save.ending=3;
            g.save.flags.Add("afterlight_started");
            g.save.flags.Add("afterlight_complete");
            g.save.flags.Add("afterlight_done_1");
            g.save.flags.Add("afterlight_steward_1");
            g.save.flags.Add("afterlight_reserve_1");
            g.save.flags.Add("afterlight_plan_1_watch");
            g.Enter(1);g.enemies.Clear();g.invulnerable=1000;
            WorldObject station=Object(g,"afterlightoutlet1");
            g.save.x=station.x;g.save.y=station.y;
            int coins=g.save.coins,shards=g.save.shards,herbs=g.save.inventory[2];
            Action(g,station.kind,"afterlight_watch_start_1");
            Check(g.AfterlightWatchActive&&g.AfterlightWatchStock==100f&&
                g.AfterlightWatchWave==1,
                "Public defense now contains a real 100-integrity objective at the original receiver");
            Enemy raider=g.enemies.Find(e=>e.kind==0);
            Enemy archer=g.enemies.Find(e=>e.kind==1);
            Check(raider!=null&&archer!=null,
                "First wave fields an actual station saboteur and a distinct ranged threat");
            // Physically place the real owned attacker on the restored site
            // while the player is away: production AI must choose the stock,
            // commit its normal tell and only then apply honest damage.
            raider.x=station.x;raider.y=station.y;
            raider.timer=0;raider.stun=raider.hitTime=raider.releaseTime=0;
            archer.stun=10;
            g.save.x=station.x+5f;g.save.y=station.y;
            g.Tick(.03f,new Point());
            Check(g.AfterlightWatchActive&&raider.state==1&&
                raider.targetingAfterlightStock&&g.AfterlightWatchStock==100f&&
                g.effects.Exists(e=>e.kind=="danger"&&e.afterlightWatchOwned),
                "An unguarded raider commits a visible real attack windup against the receiver, not instant damage");
            Advance(g,.20f);
            Check(g.AfterlightWatchStock==100f,
                "Still-winding enemy cannot silently drain civic supplies early");
            Advance(g,.55f);
            Check(g.AfterlightWatchStock==86f&&g.AfterlightWatchActive,
                "Only the actual completed attack deals the original melee saboteur's 14 integrity damage");

            // A hero in front of the station genuinely redirects aggression.
            g.save.x=station.x+.6f;g.save.y=station.y;
            raider.timer=0;raider.state=0;raider.stun=0;
            raider.releaseTime=raider.hitTime=0;
            g.Tick(.04f,new Point());
            Check(raider.state==1&&!raider.targetingAfterlightStock&&
                g.AfterlightWatchStock==86f,
                "Bodyguarding beside the receiver redirects the raider's next physical attack toward the hero");
            Advance(g,.70f);
            Check(g.AfterlightWatchStock==86f,
                "Hero-intercepted real contact cannot additionally damage protected stock");

            // Even when a stored reserve is overwhelmed, neither the
            // already-built service nor old save rewards are destroyed.
            var stock=typeof(GameCore).GetField("afterlightWatchStock",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            Check(stock!=null,"Active civic receiver has a transient integrity meter");
            stock.SetValue(g,10f);
            g.save.x=station.x+5f;g.save.y=station.y;
            raider.state=0;raider.timer=raider.hitTime=raider.releaseTime=raider.stun=0;
            g.Tick(.03f,new Point());
            Check(raider.targetingAfterlightStock&&g.AfterlightWatchStock==10f,
                "A second unguarded attack is still a timed telegraph rather than damage at intent selection");
            Advance(g,.70f);
            Check(!g.AfterlightWatchActive&&g.AfterlightWatchStock==0f&&
                !g.AfterlightWatchDone(1)&&g.enemies.Count==0&&
                g.AfterlightReserve(1)&&g.Has("afterlight_done_1")&&
                g.save.coins==coins&&g.save.shards==shards&&g.save.inventory[2]==herbs,
                "Broken temporary stock fails this challenge, removes trial enemies and preserves permanent public infrastructure and materials");
            g.PrepareSave();
            var reopened=new GameCore(data,CloneSave(g.save));reopened.Start();
            Check(reopened.save.version==2&&reopened.AfterlightReserve(1)&&
                !reopened.AfterlightWatchActive&&reopened.AfterlightWatchStock==0f,
                "Legacy version-two reload preserves earned civic reserve but never carries temporary damaged siege integrity");
        }

        public static void AfterlightWorkshopCannotCraftPartsDuringHostileCombat(Campaign data)
        {
            var g=Fresh(data);g.save.stage=3;g.save.ending=3;
            g.save.flags.Add("afterlight_started");g.save.flags.Add("afterlight_plan_3_care");
            g.save.flags.Add("afterlight_done_3");
            g.Enter(3);g.enemies.Clear();
            WorldObject mine=Object(g,"afterlightoutlet3");
            g.save.x=mine.x;g.save.y=mine.y;
            g.save.inventory[0]=2;g.save.inventory[1]=4;g.save.inventory[4]=0;
            g.enemies.Add(new Enemy(88761,0,mine.x+.8f,mine.y,75));
            int coins=g.save.coins;
            Action(g,mine.kind,"afterlight_craft");
            Check(g.save.inventory[0]==2&&g.save.inventory[1]==4&&
                g.save.inventory[4]==0&&g.save.coins==coins&&
                g.toast.Contains("工房"),
                "Workshop refuses combat crafting without charging ingredients and explains the blocked work");
            g.enemies.Clear();g.save.x=mine.x+5;
            var choose=typeof(GameCore).GetMethod("ChooseAfterlight",
                System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            choose.Invoke(g,new object[]{"afterlight_craft"});
            Check(g.save.inventory[0]==2&&g.save.inventory[1]==4,
                "Remote workshop actions never craft off-site");
            g.save.x=mine.x;g.save.y=mine.y;
            Action(g,mine.kind,"afterlight_craft");
            Check(g.save.inventory[0]==1&&g.save.inventory[1]==2&&
                g.save.inventory[4]==2,
                "Once genuinely safe on-site, original two-part recipe still works and costs real stock");
        }

        public static void AfterlightReceiverChoicesRespondToInsufficientPowerAndRemainingTime(Campaign data)
        {
            var g=Fresh(data);g.save.stage=3;g.save.ending=3;
            g.save.flags.Add("afterlight_started");g.save.flags.Add("afterlight_plan_3_watch");
            g.Enter(3);g.enemies.Clear();
            WorldObject anchor=Object(g,"afterlightanchor3"),
                outlet=Object(g,"afterlightoutlet3");
            g.save.x=anchor.x;g.save.y=anchor.y;
            g.save.inventory[2]=1;
            Action(g,anchor.kind,"afterlight_charge_3");
            g.save.x=outlet.x;g.save.y=outlet.y;
            g.Talk(outlet);
            Check(Array.FindIndex(g.conversation.choices,
                c=>c.action=="afterlight_repair_3")<0,
                "Healthy power no longer suggests a nonfunctional emergency repair");
            g.Close();
            var field=typeof(GameCore).GetField("afterlightChargeIntegrity",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            var time=typeof(GameCore).GetField("afterlightChargeLeft",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            Check(field!=null&&time!=null,"True runtime cargo and timer exist");
            field.SetValue(g,20f);
            g.Talk(outlet);
            Check(Array.Exists(g.conversation.choices,c=>c.action=="afterlight_repair_3")&&
                Array.Exists(g.conversation.choices,c=>c.action=="afterlight_discard_3"),
                "Damaged actual receiver offers repair and physical withdrawal choices");
            g.Close();
            Action(g,outlet.kind,"afterlight_install_3");
            Check(g.toast.Contains("35%")&&!g.Has("afterlight_done_3")&&
                g.AfterlightChargeIntegrity==20&&g.save.inventory[2]==1,
                "Insufficient-power install explains the 35% requirement without charging material");
            time.SetValue(g,7f);
            Action(g,outlet.kind,"afterlight_repair_3");
            Check(g.toast.Contains("8")&&g.AfterlightChargeIntegrity==20&&
                g.save.inventory[2]==1&&g.AfterlightChargeLeft==7,
                "A recovery with under eight seconds left clearly refuses to steal a real herb");
            g.Talk(outlet);int withdraw=Array.FindIndex(g.conversation.choices,
                c=>c.action=="afterlight_discard_3");
            Check(withdraw>=0,"Time-expired receiver still provides a physical exit");
            g.Choose(withdraw);
            Check(g.AfterlightChargeRegion==0&&g.AfterlightChargeIntegrity==0&&
                !g.Has("afterlight_done_3"),
                "Physical withdrawal resolves an impossible load without inventing regional completion");
        }


        public static void AfterlightAllyShotsDefendStockOnlyAfterPhysicalContact(Campaign data)
        {
            var g=Fresh(data);g.save.stage=3;g.save.ending=3;
            g.save.flags.Add("afterlight_started");
            g.save.flags.Add("afterlight_complete");
            g.save.flags.Add("afterlight_done_1");
            g.save.flags.Add("afterlight_steward_1");
            g.save.flags.Add("afterlight_reserve_1");
            g.save.flags.Add("afterlight_plan_1_watch");
            g.Enter(1);g.enemies.Clear();g.invulnerable=1000;
            // Party swaps are rightly blocked under active siege pressure.
            // Select the actual companion on quiet ground BEFORE the
            // physical defense; do not bypass this important game rule.
            g.save.flags.Add("companion_1");
            g.SelectCompanion(1);
            Check(g.save.companion==1,
                "A recruited companion joins while the region is safe");
            WorldObject station=Object(g,"afterlightoutlet1");
            g.save.x=station.x;g.save.y=station.y;
            Action(g,station.kind,"afterlight_watch_start_1");
            Check(g.AfterlightWatchActive&&g.enemies.Count==2,
                "Stock protection must use a genuinely started stocked public defense");
            var threat=g.enemies.Find(e=>e.kind==0);
            Check(threat!=null,"First forest defense wave contains the original close-raider archetype");
            Point objective=new Point(station.x,station.y);
            Point[] offsets={new Point(1,0),new Point(-1,0),
                new Point(0,1),new Point(0,-1),
                new Point(1.5f,0),new Point(-1.5f,0)};
            bool placed=false;
            foreach(Point off in offsets)
                if(g.CanStand(objective+off))
                {threat.x=objective.x+off.x;threat.y=objective.y+off.y;placed=true;break;}
            Check(placed,"Defense offers at least one real standable enemy contact foot");
            threat.state=1;threat.timer=.65f;threat.windupDuration=.65f;
            threat.targetingAfterlightStock=true;threat.target=objective;
            threat.Face(objective-threat.Position);
            threat.staggerRecovery=10f;
            Check(System.Object.ReferenceEquals(g.AfterlightWatchIncomingStockThreat,threat),
                "Only a real mission-owned receiver attack is an incoming stock threat");
            var nearest=typeof(GameCore).GetMethod("NearestCompanionThreat",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            var impact=new ImpactProfile(1,0,0,0,0f,false,-1);
            var hit=typeof(GameCore).GetMethod("HitFriendlyProjectileContact",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            Check(nearest!=null&&hit!=null,"Genuine AI selector and swept hit handler exist");
            // A closer idle body may not supersede the receiver's imminent danger.
            Check(g.save.companion==1,
                "Starting public defense preserves the preselected real ally");
            Point[] allyOffsets={new Point(-2,0),new Point(2,0),
                new Point(0,-2),new Point(0,2),
                new Point(-3,0),new Point(3,0)};
            var lane=typeof(GameCore).GetMethod("CompanionAutoFlightLaneClear",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            bool allyReady=false;
            foreach(Point offset in allyOffsets)
            {
                Point candidate=threat.Position+offset;
                if(!g.CanStand(candidate))continue;
                if((bool)lane.Invoke(g,new object[]{candidate,1,threat.Position}))
                {g.companionPosition=candidate;allyReady=true;break;}
            }
            Check(allyReady,"A physically shootable companion foot exists near the receiver");
            Enemy idle=new Enemy(99671,0,threat.x,threat.y+1.6f,500f)
                {timer=500f};
            if(g.CanStand(idle.Position))g.enemies.Add(idle);
            Check(System.Object.ReferenceEquals(nearest.Invoke(g,null),threat),
                "Companion prioritizes committed facility contact over idle foes");
            // Do not rely on a manually forged projectile alone. Drive the
            // actual autonomous command windup, release and source muzzle.
            // Even if the target will later be saved by movement or collide
            // with stone, the shot must already own the exact live mission.
            g.ToggleCompanionHold();
            Check(g.CompanionHoldingPosition,
                "The real ally can plant its legal foot to defend the receiver");
            var cadence=typeof(GameCore).GetField("companionAttack",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            Check(cadence!=null,"The genuine automatic attack cadence is accessible");
            cadence.SetValue(g,0f);
            g.Tick(.02f,new Point());
            Check(g.companionAction!=null&&
                g.companionAction.kind=="attack",
                "A real bow ally starts an automatic windup for a committed stock threat");
            Advance(g,.15f);
            Projectile live=g.projectiles.Find(p=>
                System.Object.ReferenceEquals(p.stockCoverTarget,threat));
            Check(live!=null&&live.friendly&&
                live.stockCoverRoom==g.save.room&&
                System.Object.ReferenceEquals(live.stockCoverSave,g.save)&&
                System.Object.ReferenceEquals(live.stockCoverMap,g.Map)&&
                live.afterlightWatchOwned&&
                g.CompanionStockCoverInFlight,
                "Actual fired bow projectile retains source-foot mission ownership at release");
            var actual=new Projectile {
                position=threat.Position,velocity=new Point(9f,0),
                life=2f,damage=2f,friendly=true,visual="arrow",
                impact=impact,stockCoverTarget=threat,
                stockCoverSave=g.save,stockCoverMap=g.Map,
                stockCoverRoom=g.save.room,afterlightWatchOwned=true
            };
            g.projectiles.Add(actual);
            Check(g.CompanionStockCoverInFlight,
                "Live receiver-defense arrow is observable before contact");
            float intact=g.AfterlightWatchStock;
            hit.Invoke(g,new object[]{actual,threat});
            Check(threat.state==0&&threat.stun>.5f&&
                g.AfterlightWatchStock==intact&&
                actual.stockCoverTarget==null&&
                g.effects.Exists(f=>f.kind=="number"&&
                    f.text=="储备援防打断！"),
                "Real ally arrow impact stops one pending receiver strike without free stock healing");
            g.effects.Clear();
            threat.state=1;threat.timer=.65f;threat.windupDuration=.65f;
            threat.targetingAfterlightStock=true;threat.stun=0f;
            var wrong=new Enemy(threat.id,0,threat.x,threat.y+2f,500f)
                {timer=500f};
            g.enemies.Add(wrong);
            actual.stockCoverTarget=threat;
            hit.Invoke(g,new object[]{actual,wrong});
            Check(threat.state==1&&actual.stockCoverTarget==threat&&
                !g.effects.Exists(f=>f.text=="储备援防打断！"),
                "A different physical enemy reusing the same numeric ID cannot steal cover");
            actual.stockCoverTarget=null;
            threat.targetingAfterlightStock=false;
            Check(g.AfterlightWatchIncomingStockThreat==null&&
                g.save.version==2,
                "Expired hostile intent does not remain a free reserve shield or saved combat state");
        }

    }
}
