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
            }
            Check(g.AfterlightRestored==3 &&
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
            reload=new GameCore(data,CloneSave(g.save));reload.Start();
            Check(reload.save.version==2 && reload.AfterlightComplete &&
                reload.AfterlightRestored==3 && reload.save.ending==3,
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
    }
}
