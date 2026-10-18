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

    }
}
