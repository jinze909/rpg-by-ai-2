using System;
namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        public static void CivilianCourierHasVisibleTimedObjectiveInEveryRegion(Campaign data)
        {
            var g=Fresh(data);g.save.flags.Add("far_bridge_0");
            g.save.flags.Add("far_shelter_0");
            g.Enter(0);g.enemies.Clear();
            WorldObject bridge=Object(g,"farbridge");
            g.save.x=bridge.x;g.save.y=bridge.y;
            Action(g,"farbridge","far_courier_start");
            Check(g.FarCourierActive,"The real bridge conversation starts parcel transport");
            g.save.english=true;
            Check(g.Conditions().Contains("RELIEF PARCEL")&&
                g.Conditions().Contains("CROSS BRIDGE ON FOOT")&&
                g.Conditions().Contains("95s"),
                "Untracked deliveries expose a visible timed English route objective");
            g.save.english=false;
            Check(g.Conditions().Contains("居民救援包")&&
                g.Conditions().Contains("步行过桥"),
                "Real-time parcel condition is also fully localized");
            g.save.english=true;
            g.save.y=53.0f;
            // A teleport cannot be used to bypass the actual bridge crossing.
            g.Tick(.05f,new Point());
            Check(!g.FarCourierActive,
                "Parcel jumps still fail instead of masquerading as foot travel");
            g.save.x=bridge.x;g.save.y=bridge.y;
            Action(g,"farbridge","far_courier_start");
            Check(g.FarCourierActive,"Delivery may restart after a lost parcel");
            var saved=CloneSave(g.save);g.PrepareSave();
            var resumed=new GameCore(data,saved);resumed.Start();
            Check(!resumed.FarCourierActive&&
                !resumed.Conditions().Contains("RELIEF PARCEL"),
                "Reopening old saves cannot restore a phantom courier timer");
        }
        public static void TimedParcelNavigationOverridesOldQuestsWithoutChangingTracking(Campaign data)
        {
            var g=Fresh(data);
            g.save.flags.Add("far_bridge_0");g.save.flags.Add("far_shelter_0");
            g.Enter(0);g.enemies.Clear();
            g.save.trackedQuest="main";g.save.showGuidance=true;
            WorldObject bridge=Object(g,"farbridge");
            g.save.x=bridge.x;g.save.y=bridge.y;
            Point? normal=g.NavigationTarget();
            Action(g,"farbridge","far_courier_start");
            Check(g.FarCourierActive,"The real bridge interaction starts a timed parcel");
            Point? crossing=g.NavigationTarget();
            Check(crossing.HasValue&&
                (crossing.Value-new Point(111.5f,53.5f)).Length<.01f,
                "An active timed parcel guides toward the actual traversable bridge");
            Check(g.save.trackedQuest=="main",
                "Urgent transport guidance must not overwrite the player's chosen quest");
            // Walk across the actual repaired collision tiles with the game's
            // own tick and movement code; a mere instant coordinate change
            // must not qualify as crossing the bridge.
            for(int frame=0;frame<36&&!g.FarCourierCrossed&&g.FarCourierActive;frame++)
                g.Tick(.05f,new Point(0,1));
            Check(g.FarCourierCrossed,
                "Walking south on the repaired bridge records a real parcel crossing");
            Point? checkpoint=g.NavigationTarget();
            Check(checkpoint.HasValue&&
                (checkpoint.Value-new Point(111.5f,61.5f)).Length<.01f,
                "After crossing, guidance changes to the physical seal checkpoint");
            // A teleport during delivery loses the parcel and immediately
            // restores the original unrelated navigation, without changing
            // story state or granting any package reward.
            g.save.y+=4f;g.Tick(.05f,new Point());
            Check(!g.FarCourierActive&&g.save.trackedQuest=="main"&&
                g.NavigationTarget().HasValue==normal.HasValue,
                "Losing the cargo restores ordinary tracking without a phantom objective");
            Check(!g.FarCourierDelivered(0),
                "Merely following a timed waypoint cannot grant a delivery");
        }
        public static void CourierCargoTakesRealHitsAndCanBeReinforcedOnlyOnce(Campaign data)
        {
            var hurt=typeof(GameCore).GetMethod("Hurt",
                System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            Check(hurt!=null,"Real player damage route exists");
            var g=Fresh(data);g.save.flags.Add("far_bridge_0");
            g.save.flags.Add("far_shelter_0");g.Enter(0);g.enemies.Clear();
            var bridge=Object(g,"farbridge");
            g.save.x=bridge.x;g.save.y=bridge.y;
            g.save.inventory[4]=2;
            Action(g,"farbridge","far_courier_start");
            Check(g.FarCourierActive&&g.FarCourierIntegrity==100,
                "Actual parcel pickup starts a pristine timed delivery");
            g.invulnerable=0;
            hurt.Invoke(g,new object[]{20f,"shot",null});
            float afterHit=g.FarCourierIntegrity;
            Check(afterHit<100&&afterHit>70,
                "A real unblocked enemy shot damages carried cargo after armor");
            hurt.Invoke(g,new object[]{20f,"shot",null});
            Check(g.FarCourierIntegrity==afterHit,
                "A repeated strike within invulnerability cannot damage cargo twice");
            g.invulnerable=0;
            hurt.Invoke(g,new object[]{20f,"environment",null});
            Check(g.FarCourierIntegrity==afterHit,
                "Ambient weather damage cannot silently destroy civilian freight");
            for(int frame=0;frame<95&&!g.FarCourierCrossed&&g.FarCourierActive;frame++)
            {g.hitStop=0;g.Tick(.05f,new Point(0,1));}
            Check(g.FarCourierActive&&g.FarCourierCrossed,
                "Repaired bridge can be crossed while protecting a damaged parcel");
            var checkpoint=Object(g,"farcheck");
            for(int frame=0;frame<110&&g.save.y<checkpoint.y-.8f;frame++)
                {g.hitStop=0;g.Tick(.05f,new Point(0,1));}
            Check((g.Position-new Point(checkpoint.x,checkpoint.y)).Length<=2.2f,
                "The real checkpoint can be reached on foot");
            int parts=g.save.inventory[4];
            Action(g,"farcheck","far_courier_patch");
            Check(g.FarCourierPatched&&g.save.inventory[4]==parts-1&&
                g.FarCourierIntegrity>afterHit,
                "The field patch pays exactly one real spare part and restores cargo");
            float repaired=g.FarCourierIntegrity;
            g.Close();g.Talk(checkpoint);
            Check(!Array.Exists(g.conversation.choices,x=>x.action=="far_courier_patch"),
                "A parcel cannot be reinforced a second time during one run");
            g.Close();
            g.invulnerable=0;g.hitStop=0;
            for(int n=0;n<10&&g.FarCourierActive;n++)
            {
                // Restore the hero between real contacts to isolate cargo
                // failure from the independent existing death mechanic.
                g.save.health=g.save.maxHealth;
                hurt.Invoke(g,new object[]{50f,"shot",null});
                g.invulnerable=0;g.hitStop=0;
            }
            Check(!g.FarCourierActive&&g.FarCourierIntegrity==0&&
                !g.FarCourierDelivered(0)&&!g.Dying,
                "Repeated real nonlethal hits destroy the parcel itself, not just its carrier");
            Check(g.save.inventory[4]==parts-1,
                "Failed trips never refund the one consumed reinforcement material");
            g.PrepareSave();var resumed=new GameCore(data,CloneSave(g.save));resumed.Start();
            Check(!resumed.FarCourierActive&&resumed.FarCourierIntegrity==0,
                "Old v2 save slots do not persist damaged or patched cargo");
        }
        public static void ConvoyWardCostsEnergyAndOnlyProtectsRealCargo(Campaign data)
        {
            var hurt=typeof(GameCore).GetMethod("Hurt",
                System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            var g=Fresh(data);g.save.flags.Add("far_bridge_0");
            g.save.flags.Add("far_shelter_0");g.Enter(0);g.enemies.Clear();
            var bridge=Object(g,"farbridge");
            g.save.x=bridge.x;g.save.y=bridge.y;
            Action(g,"farbridge","far_courier_start");
            for(int i=0;i<95&&!g.FarCourierCrossed;i++)g.Tick(.05f,new Point(0,1));
            Check(g.FarCourierActive&&g.FarCourierCrossed,
                "A real walk over the repaired bridge unlocks convoy defense");
            var checkpoint=Object(g,"farcheck");
            for(int i=0;i<110&&g.save.y<checkpoint.y-.8f;i++)g.Tick(.05f,new Point(0,1));
            Check((g.Position-new Point(checkpoint.x,checkpoint.y)).Length<=2.2f,
                "The convoy shield is purchased only at the real checkpoint");
            g.save.energy=60;
            Action(g,"farcheck","far_courier_guard");
            Check(g.FarCourierGuardLeft==12&&g.save.energy==30,
                "The 12-second one-use escort ward costs exactly 30 real energy");
            g.invulnerable=0;
            float hp=g.save.health;
            hurt.Invoke(g,new object[]{20f,"shot",null});
            float cargoAfterWard=g.FarCourierIntegrity;
            Check(g.save.health<hp&&cargoAfterWard>85&&cargoAfterWard<100,
                "Protection lowers cargo impact but does not grant player invulnerability");
            g.Close();g.Talk(checkpoint);
            Check(!Array.Exists(g.conversation.choices,c=>c.action=="far_courier_guard"),
                "No second convoy ward can be bought in the same run");
            g.Close();g.hitStop=0;
            for(int i=0;i<250;i++)g.Tick(.05f,new Point());
            Check(g.FarCourierActive&&g.FarCourierGuardLeft==0,
                "A convoy ward really expires before the parcel delivery deadline");
            g.save.health=g.save.maxHealth;g.invulnerable=0;
            hurt.Invoke(g,new object[]{20f,"shot",null});
            Check(cargoAfterWard-g.FarCourierIntegrity>20,
                "After the ward expires, the same physical hit damages cargo at full strength");
            g.PrepareSave();var resumed=new GameCore(data,CloneSave(g.save));resumed.Start();
            Check(!resumed.FarCourierActive&&resumed.FarCourierGuardLeft==0,
                "Transient convoy ward never leaks into version-two save slots");
        }
        public static void CargoProtectionUsesActualDifficultyAndArmor(Campaign data)
        {
            var hurt=typeof(GameCore).GetMethod("Hurt",
                System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            float[] healthLoss=new float[3],parcelLoss=new float[3];
            for(int difficulty=0;difficulty<=2;difficulty++)
            {
                var g=Fresh(data);g.save.difficulty=difficulty;
                g.save.flags.Add("far_bridge_0");g.save.flags.Add("far_shelter_0");
                g.Enter(0);g.enemies.Clear();
                var bridge=Object(g,"farbridge");g.save.x=bridge.x;g.save.y=bridge.y;
                Action(g,"farbridge","far_courier_start");
                g.invulnerable=0;
                float hp=g.save.health;
                hurt.Invoke(g,new object[]{20f,"shot",null});
                healthLoss[difficulty]=hp-g.save.health;
                parcelLoss[difficulty]=100-g.FarCourierIntegrity;
                Check(g.FarCourierActive&&g.FarCourierIntegrity>0,
                    "A nonlethal actual strike cannot instantly fabricate cargo failure");
            }
            Check(healthLoss[0]<healthLoss[1]&&healthLoss[1]<healthLoss[2]&&
                parcelLoss[0]<parcelLoss[1]&&parcelLoss[1]<parcelLoss[2],
                "Easy, Normal and Hard protection must scale actual player and cargo loss");
            var armored=Fresh(data);armored.save.flags.Add("far_bridge_0");
            armored.save.flags.Add("far_shelter_0");armored.Enter(0);
            armored.enemies.Clear();var post=Object(armored,"farbridge");
            armored.save.x=post.x;armored.save.y=post.y;
            Action(armored,"farbridge","far_courier_start");
            armored.guardTime=1;armored.invulnerable=0;
            hurt.Invoke(armored,new object[]{20f,"shot",null});
            Check(100-armored.FarCourierIntegrity<parcelLoss[1],
                "Guarding with the hero's existing real armor reduces parcel impact");
        }
        public static void RemoteCourierRunTriggersEightRealCleanableRaiderEncounters(Campaign data)
        {
            var fire=typeof(GameCore).GetMethod("Fire",
                System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            var hit=typeof(GameCore).GetMethod("Hit",
                System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            Check(fire!=null&&hit!=null,"Production bow and attack release exist");
            for(int room=0;room<8;room++)
            {
                var g=Fresh(data);
                g.save.stage=4;g.save.frontierEnding=1;
                g.save.flags.Add("scout_saved");g.save.flags.Add("exp_intro");
                g.save.flags.Add("harbor_report");g.save.flags.Add("desert_report");
                g.save.flags.Add("gate_"+room);
                g.save.flags.Add("far_bridge_"+room);
                g.save.flags.Add("far_shelter_"+room);
                g.Enter(room);g.enemies.Clear();
                Check(g.save.room==room,"Actual civilian bridge region is available: "+room);
                var bridge=Object(g,"farbridge");
                g.save.x=bridge.x;g.save.y=bridge.y;
                Action(g,"farbridge","far_courier_start");
                Check(g.FarCourierRaidersAlive==0,
                    "There are no fabricated raiders before walking over the bridge");
                for(int i=0;i<60&&!g.FarCourierCrossed;i++)g.Tick(.05f,new Point(0,1));
                Check(g.FarCourierActive&&g.FarCourierCrossed&&g.FarCourierRaidersAlive==2,
                    "Two live raiders begin only on actual crossing in region "+room);
                int id=90100+room*2;
                Enemy melee=g.enemies.Find(e=>e.id==id);
                Enemy archer=g.enemies.Find(e=>e.id==id+1);
                Check(melee!=null&&archer!=null&&melee.kind==0&&archer.kind==1&&
                    melee.hp>0&&archer.hp>0&&g.CanStand(melee.Position)&&g.CanStand(archer.Position),
                    "Both attacker archetypes spawn on traversable physical terrain");
                g.PrepareSave();
                Check(g.save.worldEnemies!=null&&
                    !Array.Exists(g.save.worldEnemies,e=>e.id==id||e.id==id+1),
                    "Courier attackers cannot pollute persistent NPC snapshots");
                var resumed=new GameCore(data,CloneSave(g.save));resumed.Start();
                Check(!resumed.FarCourierActive&&
                    !resumed.enemies.Exists(e=>e.id==id||e.id==id+1),
                    "Loading old saves does not restore an unfinished courier raid");
                fire.Invoke(g,new object[]{archer,new Point(-1,0),5f});
                Check(g.projectiles.Exists(p=>p.courierOwned),
                    "Actual archer firing owns a real moving missile");
                var unrelated=new Projectile{
                    position=new Point(10.5f,10.5f),velocity=new Point(0,1),
                    friendly=true,life=3};
                g.projectiles.Add(unrelated);
                g.save.level=10;int kills=g.save.kills[room],coins=g.save.coins;
                int materials=g.save.inventory[1],xp=g.save.xp;
                hit.Invoke(g,new object[]{melee,9000f,false,null,null});
                hit.Invoke(g,new object[]{archer,9000f,false,null,null});
                Check(g.FarCourierRaidersAlive==0&&
                    g.save.kills[room]==kills&&g.save.coins==coins&&
                    g.save.inventory[1]==materials&&g.save.xp==xp,
                    "Courier combat cannot be exploited to farm chapter kills, coins, XP or drops");
                // Deliberate in-world invalid hop aborts the run, without
                // destroying ordinary projectiles belonging to other combat.
                g.hitStop=0;g.save.y+=4;g.Tick(.05f,new Point());
                Check(!g.FarCourierActive&&
                    !g.enemies.Exists(e=>e.id==id||e.id==id+1)&&
                    !g.projectiles.Exists(p=>p.courierOwned)&&
                    g.projectiles.Contains(unrelated),
                    "Abort removes only live courier threats and owned arrows");
            }
        }
        public static void CourierRaidersActuallyWindUpAndCanTearCargoDuringPlay(Campaign data)
        {
            var g=Fresh(data);
            g.save.flags.Add("far_bridge_0");g.save.flags.Add("far_shelter_0");
            g.Enter(0);g.enemies.Clear();
            var bridge=Object(g,"farbridge");
            g.save.x=bridge.x;g.save.y=bridge.y;
            Action(g,"farbridge","far_courier_start");
            for(int i=0;i<65&&!g.FarCourierCrossed;i++)
                g.Tick(.05f,new Point(0,1));
            Check(g.FarCourierRaidersAlive==2,
                "Only traversing the actual bridge starts this battle");
            bool warned=false,hit=false;float health=g.save.health;
            for(int i=0;i<200&&g.FarCourierActive;i++)
            {
                // Approach real contact range, then stand and let ordinary
                // enemy AI choose windups / arrows / collision on its own.
                g.Tick(.05f,i<20?new Point(0,1):new Point());
                if(g.enemies.Exists(e=>e.id>=90100&&e.id<90116&&e.state==1))
                    warned=true;
                if(g.FarCourierIntegrity<99&&g.save.health<health)
                    {hit=true;break;}
            }
            Check(warned,"Live courier attackers must produce actual combat telegraphs");
            Check(hit,"A production enemy attack must physically reach the player and tear cargo");
        }
        public static void CivilianSupplyInquestUsesRealGuestbookAndThreeSites(Campaign data)
        {
            var g=Fresh(data);g.save.stage=4;
            g.save.flags.Add("exp_intro");g.save.flags.Add("harbor_report");
            g.save.flags.Add("far_courier_0");g.save.flags.Add("far_courier_3");
            g.save.flags.Add("far_courier_5");
            foreach(int room in new[]{0,3,5})
                g.save.flags.Add("far_shelter_"+room);
            g.save.inventory[0]=8;g.save.inventory[2]=8;g.save.inventory[4]=8;
            g.Enter(9);g.enemies.Clear();
            var guest=Object(g,"farrefugerecord");
            g.save.x=guest.x;g.save.y=guest.y;
            int xp=g.save.xp;
            g.Talk(guest);
            Check(g.Has("far_refuge_guest_record")&&g.save.xp>xp,
                "Original first-visit guest registry remains intact");
            g.Close();
            Action(g,"farrefugerecord","civic_supply_begin");
            Check(g.CivicSupplyStarted&&!g.CivicSupplyComplete&&
                g.save.trackedQuest=="civic_supply",
                "Authentic published parcel starts a tracked new inquest at the guestbook");
            var quest=g.GetQuest("civic_supply");
            Check(!quest.locked&&!quest.complete&&quest.room==0&&
                (quest.target-new Point(111.5f,61.5f)).Length<.05f,
                "Living guidance points to the real canal inspection checkpoint");
            int gear=g.save.inventory[4];
            int herbs=g.save.inventory[2];
            g.Enter(0);g.enemies.Clear();
            var check=Object(g,"farcheck");
            g.save.x=check.x;g.save.y=check.y;
            Action(g,"farcheck","civic_supply_clinic");
            Check(g.CivicSupplyClinic(0)&&!g.CivicSupplyWatch(0)&&
                g.save.inventory[4]==gear-1&&g.save.inventory[2]==herbs-2,
                "Canal clinic is a single persistent real-material community decision");
            Check(g.GetQuest("civic_supply").room==3,
                "After signing the canal report, tracked inquest moves to Ironreach");
            g.Talk(check);
            Check(!Array.Exists(g.conversation.choices,
                c=>c.action=="civic_supply_watch"||c.action=="civic_supply_clinic"),
                "Cannot buy the opposite choice or farm service rewards twice");
            g.Close();
            g.Enter(3);g.enemies.Clear();
            check=Object(g,"farcheck");g.save.x=check.x;g.save.y=check.y;
            int timber=g.save.inventory[0];gear=g.save.inventory[4];
            Action(g,"farcheck","civic_supply_watch");
            Check(g.CivicSupplyWatch(3)&&!g.CivicSupplyClinic(3)&&
                g.save.inventory[0]==timber-2&&g.save.inventory[4]==gear-1,
                "Ironreach watch spends exactly 2 real timber and 1 spare part");
            g.Enter(5);g.enemies.Clear();
            check=Object(g,"farcheck");g.save.x=check.x;g.save.y=check.y;
            Action(g,"farcheck","civic_supply_clinic");
            Check(g.CivicSupplyResolvedCount==3&&
                g.CivicSupplyClinic(5)&&!g.CivicSupplyComplete,
                "Third physical site completes votes but cannot auto-publish the quest");
            quest=g.GetQuest("civic_supply");
            Check(quest.room==9&&!quest.complete&&
                (quest.target-new Point(guest.x,guest.y)).Length<.05f,
                "Guidance correctly returns to the physical inn registry for publication");
            g.Enter(9);g.enemies.Clear();
            guest=Object(g,"farrefugerecord");g.save.x=guest.x;g.save.y=guest.y;
            int coins=g.save.coins, shards=g.save.shards, skills=g.save.skillPoints;
            g.save.level=30;
            Action(g,"farrefugerecord","civic_supply_publish");
            Check(g.CivicSupplyComplete&&g.save.coins==coins+48&&
                g.save.shards==shards+2&&g.save.skillPoints==skills+1&&
                g.GetQuest("civic_supply").complete,
                "Public record unlocks complete chapter and rewards exactly once");
            g.Talk(guest);
            Check(!Array.Exists(g.conversation.choices,c=>c.action=="civic_supply_publish"),
                "No repeatable final commission reward");
            g.Close();
            g.PrepareSave();
            var reloaded=new GameCore(data,CloneSave(g.save));reloaded.Start();
            Check(reloaded.CivicSupplyComplete&&reloaded.CivicSupplyClinic(0)&&
                reloaded.CivicSupplyWatch(3)&&reloaded.CivicSupplyClinic(5)&&
                reloaded.save.trackedQuest=="civic_supply",
                "All three divergent community outcomes survive original SaveData v2 reload");
        }
        public static void CivicSupplySpoofingAndLocalServicesRespectRealSafety(Campaign data)
        {
            var g=Fresh(data);g.save.stage=4;
            g.save.flags.Add("exp_intro");g.save.flags.Add("harbor_report");
            g.save.flags.Add("far_courier_0");g.save.flags.Add("far_shelter_0");
            g.save.flags.Add("far_shelter_3");
            g.save.flags.Add("far_refuge_guest_record");
            g.save.inventory[0]=10;g.save.inventory[2]=10;g.save.inventory[4]=10;
            var choose=typeof(GameCore).GetMethod("ChooseCivicSupply",
                System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            Check(choose!=null,"Authentic inquest actions are exposed only through dialogue");
            int herbs=g.save.inventory[2];
            choose.Invoke(g,new object[]{"civic_supply_clinic"});
            Check(!g.CivicSupplyClinic(0)&&g.save.inventory[2]==herbs,
                "Forged choices cannot start a clinic before the registry opens the mission");
            g.Enter(9);g.enemies.Clear();
            var guest=Object(g,"farrefugerecord");
            g.save.x=guest.x;g.save.y=guest.y;
            Action(g,"farrefugerecord","civic_supply_begin");
            g.Enter(3);g.enemies.Clear();
            var post=Object(g,"farcheck");g.save.x=post.x;g.save.y=post.y;
            choose.Invoke(g,new object[]{"civic_supply_watch"});
            Check(!g.CivicSupplyWatch(3),
                "Unverified deliveries cannot be bypassed with a forged civic choice");
            g.save.flags.Add("far_courier_3");
            g.save.x=post.x+8;g.save.y=post.y;
            choose.Invoke(g,new object[]{"civic_supply_watch"});
            Check(!g.CivicSupplyWatch(3),
                "Action requires standing beside the actual physical inspection post");
            g.save.x=post.x;g.save.y=post.y;
            Action(g,"farcheck","civic_supply_watch");
            Check(g.CivicSupplyWatch(3),"Real inspected delivery can establish lookout");
            var shelter=Object(g,"farshelter");
            g.save.x=shelter.x;g.save.y=shelter.y;
            g.save.energy=30;g.Tick(.05f,new Point());
            float withWatch=g.save.energy-30;
            g.save.flags.Remove("civic_supply_watch_3");
            g.save.energy=30;g.Tick(.05f,new Point());
            float withoutWatch=g.save.energy-30;
            Check(withWatch>withoutWatch+.15f,
                "Permanent lookout increases actual safe stamina regeneration at its shelter");
            g.save.flags.Add("civic_supply_watch_3");
            g.Enter(0);g.enemies.Clear();
            post=Object(g,"farcheck");g.save.x=post.x;g.save.y=post.y;
            Action(g,"farcheck","civic_supply_clinic");
            shelter=Object(g,"farshelter");
            g.save.x=shelter.x;g.save.y=shelter.y;
            g.save.health=55;g.Tick(.05f,new Point());
            float withClinic=g.save.health-55;
            g.save.flags.Remove("civic_supply_clinic_0");
            g.save.health=55;g.Tick(.05f,new Point());
            float withoutClinic=g.save.health-55;
            Check(withClinic>withoutClinic+.06f,
                "Permanent clinic actually heals only near its own physical shelter");
            g.save.flags.Add("civic_supply_clinic_0");
            g.save.x=shelter.x-8;g.save.y=shelter.y;
            g.save.health=55;g.Tick(.05f,new Point());
            Check(g.save.health-55<withClinic,
                "A clinic cannot heal the hero remotely from another route");
        }
        public static void CivicInquestChronicleRevealsOnlyEarnedLocalDecisions(Campaign data)
        {
            var g=Fresh(data);
            var unlock=typeof(GameCore).GetMethod("StoryUnlocked",
                System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            var chapter=typeof(GameCore).GetMethod("WorldChronicleSection",
                System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            Check(unlock!=null&&chapter!=null,
                "Chronicle uses existing private spoiler gates and physical chapter menus");
            Check(!(bool)unlock.Invoke(g,new object[]{"supply_inquest"}),
                "An unopened civilian inquest cannot leak chapter-nine spoilers");
            g.save.flags.Add("civic_supply_started");
            g.save.flags.Add("civic_supply_clinic_0");
            g.save.flags.Add("civic_supply_watch_3");
            Check((bool)unlock.Invoke(g,new object[]{"supply_inquest"}),
                "Opening the guest registry unlocks its written continuation");
            chapter.Invoke(g,new object[]{"after"});
            Check(g.mode=="dialog"&&
                Array.Exists(g.conversation.choices,c=>c.action=="story_read_supply_inquest"),
                "The real chronicle's Aftermath chapter offers the earned inquest page");
            int option=Array.FindIndex(g.conversation.choices,
                c=>c.action=="story_read_supply_inquest");
            int beforeFlags=g.save.flags.Count, coins=g.save.coins;
            g.Choose(option);
            Check(g.mode=="dialog"&&
                g.conversation.text.Contains("引水渠")&&
                g.conversation.text.Contains("赤铁矿场")&&
                g.conversation.text.Contains("雾港")&&
                g.conversation.text.Contains("尚未签署")&&
                g.conversation.text.Contains("医疗点")&&
                g.conversation.text.Contains("守路岗"),
                "Chapter-nine bilingual narrative records actual distinct outcomes and unfinished votes");
            Check(g.save.flags.Count==beforeFlags&&g.save.coins==coins,
                "Reading story consequences cannot silently change flags or grant resources");
            g.Close();
            g.save.flags.Add("civic_supply_clinic_5");
            g.save.flags.Add("civic_supply_complete");
            chapter.Invoke(g,new object[]{"after"});
            option=Array.FindIndex(g.conversation.choices,
                c=>c.action=="story_read_supply_inquest");
            g.Choose(option);
            Check(g.conversation.text.Contains("公开审计已签发")&&
                !g.conversation.text.Contains("尚未签署"),
                "Published chapter-nine chronicle shows completed choices without pending claims");
        }
        public static void VotedClinicChangesActualTimberRefugeRestWithoutBreakingCharter(Campaign data)
        {
            var g=Fresh(data);g.save.flags.Add("far_shelter_0");
            g.save.flags.Add("far_dial_0");
            g.Enter(0);g.enemies.Clear();
            var refuge=Object(g,"farshelter");
            g.save.x=refuge.x;g.save.y=refuge.y;
            g.save.inventory[0]=6;
            g.save.flags.Add("civic_supply_clinic_0");
            g.save.health=20;int wood=g.save.inventory[0];
            Action(g,"farshelter","far_rest");
            Check(Math.Abs(g.save.health-70)<.01f&&g.save.inventory[0]==wood-1,
                "A genuinely funded clinic improves paid base refuge care by 15 health");
            g.save.flags.Remove("civic_supply_clinic_0");
            g.save.health=20;wood=g.save.inventory[0];
            Action(g,"farshelter","far_rest");
            Check(Math.Abs(g.save.health-55)<.01f&&g.save.inventory[0]==wood-1,
                "Original unmodified refuge continues its legacy +35 health service");
            g.save.flags.Add("far_courier_charter");
            g.save.flags.Add("civic_supply_clinic_0");
            g.save.health=20;wood=g.save.inventory[0];
            Action(g,"farshelter","far_rest");
            Check(Math.Abs(g.save.health-90)<.01f&&g.save.inventory[0]==wood-1,
                "Existing public charter still stacks with the voted medical service");
            g.save.flags.Remove("civic_supply_clinic_0");
            g.save.health=20;wood=g.save.inventory[0];
            Action(g,"farshelter","far_rest");
            Check(Math.Abs(g.save.health-75)<.01f&&g.save.inventory[0]==wood-1,
                "Without a local clinic, upgraded charter retains original +55 HP");
            g.save.flags.Add("civic_supply_clinic_0");
            g.Talk(refuge);
            Check(g.conversation.text.Contains("额外恢复生命 +15")&&
                g.conversation.textEn.Contains("paid rest adds 15 HP"),
                "Interactive bilingual shelter dialogue shows the actual service bonus");
        }
        public static void ChapterNineClinicFieldRunRequiresActualFootTravelAndSavesEarnedUpgrade(Campaign data)
        {
            var g=Fresh(data);g.save.stage=4;g.save.flags.Add("exp_intro");
            g.save.flags.Add("civic_supply_complete");
            g.save.flags.Add("civic_supply_clinic_0");g.save.flags.Add("far_shelter_0");
            g.save.flags.Add("far_dial_0");g.save.flags.Add("far_courier_0");
            g.save.trackedQuest="main";
            g.Enter(0);g.enemies.Clear();g.save.level=30;
            var shelter=Object(g,"farshelter");
            var check=Object(g,"farcheck");
            g.save.x=shelter.x;g.save.y=shelter.y;
            Action(g,"farshelter","civic_response_start");
            Check(g.CivicResponseActive&&g.CivicResponseClinicRun&&
                !g.CivicResponseWatchRun&&g.CivicResponseTimeLeft==75,
                "Real southern refuge starts a 75s carrying mission rather than instant payment");
            Check(g.Conditions().Contains("居民服务行动")&&
                g.GetQuest("civic_services").room==0&&
                (g.NavigationTarget().Value-new Point(check.x,check.y)).Length<.01f&&
                g.save.trackedQuest=="main",
                "Timed delivery overrides guidance and announces its real northward destination without overwriting pinned quest");
            // A direct teleport to the endpoint cannot forge walked distance.
            g.save.x=check.x;g.save.y=check.y;
            var choose=typeof(GameCore).GetMethod("ChooseCivicResponse",
                System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            choose.Invoke(g,new object[]{"civic_response_deliver"});
            Check(!g.CivicResponseDone(0),
                "Nearby coordinates without genuine travel cannot complete medicine delivery");
            g.Tick(.05f,new Point());
            Check(!g.CivicResponseActive,
                "Teleporting between the two actual medical sites aborts the carrying mission");
            g.save.x=shelter.x;g.save.y=shelter.y;
            Action(g,"farshelter","civic_response_start");
            for(int i=0;i<210&&g.CivicResponseActive&&
                g.Position.y>check.y+1.15f;i++)
                g.Tick(.05f,new Point(0,-1));
            Check(g.CivicResponseActive&&g.Position.y<check.y+1.3f&&
                g.CivicResponseTimeLeft>0,
                "Medicine actually traverses the mountain road with game movement and collision");
            Check(g.GetQuest("civic_services").target.y==check.y,
                "The tracked field delivery ends at the true resident checkpoint");
            // Real walking already places the hero within the checkpoint's
            // interaction radius; changing X after the last Tick would
            // correctly invalidate the no-teleport handover check.
            int coins=g.save.coins,shards=g.save.shards,skills=g.save.skillPoints;
            Action(g,"farcheck","civic_response_deliver");
            Check(!g.CivicResponseActive&&g.CivicResponseDone(0)&&
                g.save.coins==coins+24&&g.save.shards==shards+1&&
                g.save.skillPoints==skills,
                "Physical handover grants only the one-time local medical upgrade and reward");
            Check(!g.GetQuest("civic_services").complete,
                "Completing one region does not silently complete the three-region public service campaign");
            choose.Invoke(g,new object[]{"civic_response_deliver"});
            Check(g.save.coins==coins+24,
                "Repeating the handover cannot farm the clinic's one-time reward");
            g.PrepareSave();
            var restored=new GameCore(data,CloneSave(g.save));restored.Start();
            Check(restored.CivicResponseDone(0)&&!restored.CivicResponseActive&&
                restored.save.trackedQuest=="main",
                "Old v2 saves preserve verified service but never resurrect transient medicine");
        }
        public static void ChapterNineWatchDefenseNeedsTwoRealKillsAndDoesNotPolluteLootOrSave(Campaign data)
        {
            var g=Fresh(data);g.save.stage=4;g.save.flags.Add("exp_intro");
            g.save.flags.Add("civic_supply_complete");g.save.flags.Add("harbor_report");
            g.save.flags.Add("civic_supply_watch_3");g.save.flags.Add("far_shelter_3");
            g.Enter(3);g.enemies.Clear();g.save.level=30;
            var shelter=Object(g,"farshelter");
            g.save.x=shelter.x;g.save.y=shelter.y;
            Action(g,"farshelter","civic_response_start");
            int baseId=90206;
            var a=g.enemies.Find(e=>e.id==baseId);
            var b=g.enemies.Find(e=>e.id==baseId+1);
            Check(g.CivicResponseWatchRun&&g.CivicResponseEnemiesAlive==2&&
                a!=null&&b!=null&&a.kind==0&&b.kind==1&&
                g.CanStand(a.Position)&&g.CanStand(b.Position),
                "A genuine local watch deploys melee and bow enemies on traversable authored terrain");
            Check(g.Conditions().Contains("袭击者")&&
                g.GetQuest("civic_services").target.y==shelter.y,
                "Live lookout UI reports the authentic remaining enemies and in-world return point");
            g.PrepareSave();
            Check(!Array.Exists(g.save.worldEnemies,r=>r.id==baseId||r.id==baseId+1),
                "Temporary defense actors are never serialized as ordinary enemy patrols");
            var restored=new GameCore(data,CloneSave(g.save));restored.Start();
            Check(!restored.CivicResponseActive&&
                !restored.enemies.Exists(e=>e.id==baseId||e.id==baseId+1),
                "Legacy world snapshots can never resurrect in-progress civic defense");
            var fire=typeof(GameCore).GetMethod("Fire",
                System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            var hit=typeof(GameCore).GetMethod("Hit",
                System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            fire.Invoke(g,new object[]{b,new Point(0,1),5f});
            Check(g.projectiles.Exists(p=>p.civicResponseOwned),
                "Real security archer spawns a moving mission-owned arrow");
            int originalKills=g.save.kills[3],coins=g.save.coins,
                xp=g.save.xp,parts=g.save.inventory[1];
            var ordinary=new Projectile{friendly=true,position=g.Position,
                velocity=new Point(1,0),life=3};
            g.projectiles.Add(ordinary);
            hit.Invoke(g,new object[]{a,9000f,false,null,null});
            Check(g.CivicResponseEnemiesAlive==1&&!g.CivicResponseDone(3),
                "One authentic combat kill cannot certify a two-attacker watch");
            hit.Invoke(g,new object[]{b,9000f,false,null,null});
            Check(g.CivicResponseEnemiesAlive==0&&!g.CivicResponseDone(3)&&
                g.save.kills[3]==originalKills&&g.save.coins==coins&&
                g.save.xp==xp&&g.save.inventory[1]==parts,
                "Only actual second combat death clears the watch, with no farming of old kill, coin, XP, or parts");
            Action(g,"farshelter","civic_response_claim");
            Check(g.CivicResponseDone(3)&&!g.CivicResponseActive&&
                g.save.coins==coins+24&&g.projectiles.Contains(ordinary)&&
                !g.projectiles.Exists(p=>p.civicResponseOwned),
                "Real claim pays once, selectively retiring arrows but retaining normal projectiles");
            g.hitStop=0;g.save.energy=30;
            float baseEnergy=g.save.energy;
            g.Tick(.05f,new Point());
            float upgrade=g.save.energy-baseEnergy;
            g.save.flags.Remove("civic_response_done_3");
            g.hitStop=0;g.save.energy=30;g.Tick(.05f,new Point());
            float normal=g.save.energy-30;
            Check(upgrade>normal+.08f,
                "A fully defended lookout provides genuinely stronger safe local energy restoration");
        }
        public static void ClinicMedicineOnlyBreaksOnUnblockedRealDamageAndRestartsClean(Campaign data)
        {
            var g=Fresh(data);g.save.flags.Add("civic_supply_complete");
            g.save.flags.Add("civic_supply_clinic_0");g.save.flags.Add("far_shelter_0");
            g.save.flags.Add("far_dial_0");g.Enter(0);g.enemies.Clear();
            var refuge=Object(g,"farshelter");g.save.x=refuge.x;g.save.y=refuge.y;
            Action(g,"farshelter","civic_response_start");
            Check(g.CivicResponseClinicRun&&g.CivicResponseIntegrity==100,
                "Medical assignment equips a real pristine emergency kit");
            var hurt=typeof(GameCore).GetMethod("Hurt",
                System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            g.invulnerable=0;g.save.health=g.save.maxHealth;
            hurt.Invoke(g,new object[]{20f,"shot",null});
            float damaged=g.CivicResponseIntegrity;
            Check(damaged<100&&damaged>65,
                "A genuine enemy shot damages medical supplies after existing armor");
            hurt.Invoke(g,new object[]{20f,"shot",null});
            Check(g.CivicResponseIntegrity==damaged,
                "Invulnerable follow-up hits cannot tear the clinic parcel twice");
            g.invulnerable=0;
            hurt.Invoke(g,new object[]{15f,"environment",null});
            Check(g.CivicResponseIntegrity==damaged,
                "Ambient temperature never arbitrarily destroys civilian medicine");
            int coins=g.save.coins;
            for(int n=0;n<12&&g.CivicResponseActive;n++)
            {
                g.save.health=g.save.maxHealth;g.invulnerable=0;g.hitStop=0;
                hurt.Invoke(g,new object[]{45f,"shot",null});
            }
            Check(!g.CivicResponseActive&&g.CivicResponseIntegrity==0&&
                !g.CivicResponseDone(0)&&g.save.coins==coins,
                "Enough real combat damage destroys supplies without paying an incomplete service");
            g.save.health=g.save.maxHealth;g.invulnerable=0;
            Action(g,"farshelter","civic_response_start");
            Check(g.CivicResponseClinicRun&&g.CivicResponseIntegrity==100,
                "A destroyed medical package can be genuinely reissued from the same refuge");
            g.PrepareSave();var loaded=new GameCore(data,CloneSave(g.save));loaded.Start();
            Check(!loaded.CivicResponseActive&&loaded.CivicResponseIntegrity==0,
                "Existing save schema cannot resurrect unfinished or damaged medical kits");
        }
        public static void CivicLookoutAbortAndDespawnNeverForgeSuccess(Campaign data)
        {
            var g=Fresh(data);g.save.stage=4;g.save.flags.Add("exp_intro");
            g.save.flags.Add("harbor_report");g.save.flags.Add("civic_supply_complete");
            g.save.flags.Add("civic_supply_watch_5");g.save.flags.Add("far_shelter_5");
            g.Enter(5);g.enemies.Clear();
            var refuge=Object(g,"farshelter");g.save.x=refuge.x;g.save.y=refuge.y;
            Action(g,"farshelter","civic_response_start");
            int id=90210;
            var archer=g.enemies.Find(e=>e.id==id+1);
            Check(archer!=null,"Mistport's real watch has a real archer");
            var fire=typeof(GameCore).GetMethod("Fire",
                System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            fire.Invoke(g,new object[]{archer,new Point(0,1),5f});
            Check(g.projectiles.Exists(p=>p.civicResponseOwned),
                "Authentic enemy arrows are correctly owned by the active watch");
            var unrelated=new Projectile{friendly=true,position=g.Position,
                velocity=new Point(1,0),life=3};
            g.projectiles.Add(unrelated);
            Action(g,"farshelter","civic_response_abort");
            Check(!g.CivicResponseActive&&!g.CivicResponseDone(5)&&
                !g.enemies.Exists(e=>e.id==id||e.id==id+1)&&
                !g.projectiles.Exists(p=>p.civicResponseOwned)&&
                g.projectiles.Contains(unrelated),
                "Aborting removes only mission enemies and shots; normal combat shots survive");
            Action(g,"farshelter","civic_response_start");
            Check(g.CivicResponseEnemiesAlive==2,"An aborted watch can restart");
            g.enemies.RemoveAll(e=>e.id==id);
            g.hitStop=0;g.Tick(.05f,new Point());
            Check(!g.CivicResponseActive&&!g.CivicResponseDone(5)&&
                !g.enemies.Exists(e=>e.id==id+1),
                "Despawning a live attacker cannot forge a successful defensive kill");
        }
        public static void ThreeCompletedFieldServicesPublishRealNetworkRecovery(Campaign data)
        {
            var g=Fresh(data);g.save.stage=4;g.save.flags.Add("exp_intro");
            g.save.flags.Add("harbor_report");
            g.save.flags.Add("civic_supply_started");
            g.save.flags.Add("civic_supply_complete");
            g.save.flags.Add("far_refuge_guest_record");
            g.save.flags.Add("far_courier_0");
            g.save.flags.Add("civic_supply_clinic_0");
            g.save.flags.Add("civic_supply_watch_3");
            g.save.flags.Add("civic_supply_clinic_5");
            g.save.flags.Add("far_shelter_5");
            g.save.flags.Add("civic_response_done_0");
            g.save.flags.Add("civic_response_done_3");
            g.save.level=30;
            g.Enter(5);g.enemies.Clear();
            var refuge=Object(g,"farshelter");
            var check=Object(g,"farcheck");
            g.save.x=refuge.x;g.save.y=refuge.y;
            Action(g,"farshelter","civic_response_start");
            for(int i=0;i<210&&g.CivicResponseActive&&
                g.Position.y>check.y+1.15f;i++)
                g.Tick(.05f,new Point(0,-1));
            Check(g.CivicResponseClinicRun&&g.Position.y<check.y+1.3f,
                "Third district must still physically deliver its medical supplies");
            int talent=g.save.skillPoints,coins=g.save.coins,shards=g.save.shards;
            Action(g,"farcheck","civic_response_deliver");
            Check(g.CivicResponseDoneCount==3&&
                g.Has("civic_response_all")&&g.save.skillPoints==talent+1&&
                g.save.coins==coins+24&&g.save.shards==shards+1,
                "The three authentic service completions grant precisely one all-region talent");
            Check(!g.GetQuest("civic_services").complete&&
                g.GetQuest("civic_services").room==9,
                "After three field victories players must still publish the real guest ledger");
            g.Enter(9);g.enemies.Clear();
            var guest=Object(g,"farrefugerecord");
            g.save.x=guest.x;g.save.y=guest.y;
            g.Talk(guest);
            Check(Array.Exists(g.conversation.choices,
                c=>c.action=="civic_response_ledger"),
                "Hearth Inn ledger choice: room="+g.save.room+
                " done="+g.CivicResponseDoneCount+
                " guests="+g.Has("far_refuge_guest_record")+
                " courier="+g.FarCourierDeliveredCount+
                " inquest="+g.CivicSupplyComplete+
                " actions="+string.Join(",",Array.ConvertAll(g.conversation.choices,
                    c=>c.action)));
            int ledgerCoins=g.save.coins,ledgerShards=g.save.shards;
            Action(g,"farrefugerecord","civic_response_ledger");
            Check(g.Has("civic_response_ledger")&&
                g.GetQuest("civic_services").complete&&
                g.save.coins==ledgerCoins+40&&g.save.shards==ledgerShards+2,
                "Publication is a physical, separate, one-time reward with permanent world consequences");
            g.Talk(guest);
            Check(!Array.Exists(g.conversation.choices,
                c=>c.action=="civic_response_ledger"),
                "Published response ledger cannot be rewarded twice");
            g.Close();g.PrepareSave();
            var loaded=new GameCore(data,CloneSave(g.save));loaded.Start();
            Check(loaded.GetQuest("civic_services").complete&&
                loaded.Has("civic_response_ledger")&&loaded.CivicResponseDoneCount==3,
                "Entire chapter-nine field-service epilogue survives original SaveData v2 reload");
            g.save.flags.Add("far_shelter_1");
            g.Enter(1);g.enemies.Clear();
            refuge=Object(g,"farshelter");
            g.save.x=refuge.x;g.save.y=refuge.y;
            g.save.health=40;
            g.save.flags.Remove("civic_response_ledger");g.hitStop=0;
            g.Tick(.05f,new Point());
            float baseline=g.save.health-40;
            g.save.flags.Add("civic_response_ledger");
            g.save.health=40;g.hitStop=0;
            g.Tick(.05f,new Point());
            float upgraded=g.save.health-40;
            Check(upgraded>baseline+.025f,
                "The published report genuinely heals at all rebuilt refuges, not only three named clinics");
            g.save.x=refuge.x+9;g.save.y=refuge.y;
            g.save.health=40;g.hitStop=0;g.Tick(.05f,new Point());
            Check(g.save.health-40<upgraded,
                "Shared care cannot remotely heal a traveler far away from a physical refuge");
        }
        public static void CivicLookoutRaidersReallyTelegraphAndHitWithoutScriptedDamage(Campaign data)
        {
            var g=Fresh(data);g.save.flags.Add("civic_supply_complete");
            g.save.flags.Add("civic_supply_watch_0");
            g.save.flags.Add("far_shelter_0");g.Enter(0);g.enemies.Clear();
            var shelter=Object(g,"farshelter");
            g.save.x=shelter.x;g.save.y=shelter.y;
            Action(g,"farshelter","civic_response_start");
            Check(g.CivicResponseWatchRun&&g.CivicResponseEnemiesAlive==2,
                "Real live watch defense must deploy actual nearby combat AI");
            float health=g.save.health;
            bool warned=false,contact=false;
            for(int i=0;i<180&&g.CivicResponseActive&&!g.Dying;i++)
            {
                g.Tick(.05f,new Point());
                if(g.enemies.Exists(e=>e.id>=90200&&e.id<90216&&e.state==1))
                    warned=true;
                if(g.save.health<health){contact=true;break;}
            }
            Check(warned,
                "Organic watch raiders must visibly wind up with standard enemy AI");
            Check(contact,
                "At least one unforced actual AI attack must contact the player");
            Check(!g.CivicResponseDone(0),
                "Taking real damage during a watch never counts as defeating its attackers");
        }
        public static void ChapterTenFivePhysicalSitesAndTrueTwoWaveSecurity(Campaign data)
        {
            var g=Fresh(data);g.save.stage=4;g.save.flags.Add("civic_response_ledger");
            var board=Object(g,"lastdirectiveboard");
            Check(g.CanStand(new Point(board.x,board.y)),
                "The new Emberfall public council is a true walkable map location");
            g.save.x=board.x;g.save.y=board.y;
            Action(g,"lastdirectiveboard","last_directive_begin");
            Check(g.LastDirectiveStarted&&g.save.trackedQuest=="last_directive"&&
                g.GetQuest("last_directive").room==4,
                "The new authored Chapter Ten genuinely starts and points to the citadel");
            g.Enter(4);g.enemies.Clear();
            for(int i=0;i<3;i++)
            {
                var clue=Object(g,"lastdirectiveclue"+i);
                Check(g.CanStand(new Point(clue.x,clue.y)),
                    "Every physical testimony occupies traversable Citadel terrain");
                g.save.x=clue.x;g.save.y=clue.y;g.Talk(clue);
                Check(g.Has("last_directive_clue_"+i),
                    "Only genuine physical testimony encounters reveal its own historic order");
                g.Close();
            }
            Check(g.LastDirectiveReady&&g.LastDirectiveClues==3,
                "Three authentic field accounts are required for the console cipher");
            var console=Object(g,"lastdirectivecore");
            Check(g.CanStand(new Point(console.x,console.y)),
                "The new encounter terminal is on genuine traversable land");
            g.save.x=console.x;g.save.y=console.y;
            Action(g,"lastdirectivecore","last_directive_code_2");
            Check(g.LastDirectiveWave==0&&g.LastDirectiveEnemies==0,
                "Wrong historical clue ordering never fakes the guardian battle");
            Action(g,"lastdirectivecore","last_directive_code_0");
            Check(g.LastDirectiveWave==1&&g.LastDirectiveEnemies==2&&
                g.enemies.Exists(e=>e.id==90300&&e.kind==0)&&
                g.enemies.Exists(e=>e.id==90301&&e.kind==1),
                "Correct in-world cipher activates melee and ranged live security");
            foreach(var foe in g.enemies)
                if(foe.id>=90300&&foe.id<=90304)
                    Check(g.CanStand(foe.Position),"Real guard AI starts on physically walkable ground");
            g.PrepareSave();
            Check(!Array.Exists(g.save.worldEnemies,r=>r.id>=90300&&r.id<=90304),
                "Legacy world enemy snapshot excludes the new temporary guards");
            var loaded=new GameCore(data,CloneSave(g.save));loaded.Start();
            Check(!loaded.enemies.Exists(e=>e.id>=90300&&e.id<=90304)&&
                loaded.LastDirectiveWave==0,
                "Old SaveData v2 reload preserves testimony but not fake active guardians");
            var hit=typeof(GameCore).GetMethod("Hit",
                System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            int coins=g.save.coins,xp=g.save.xp,kills=g.save.kills[4];
            foreach(int id in new[]{90300,90301})
            {
                g.hitStop=0;hit.Invoke(g,new object[]{g.enemies.Find(e=>e.id==id),10000f,false,null,null});
            }
            g.hitStop=0;g.Tick(.05f,new Point());
            Check(g.LastDirectiveWave==2&&g.LastDirectiveEnemies==0&&
                g.save.coins==coins&&g.save.xp==xp&&g.save.kills[4]==kills,
                "Only actual defeat of both first-wave AI advances; no ordinary kill farming");
            g.save.energy=10;
            Action(g,"lastdirectivecore","last_directive_second");
            Check(g.LastDirectiveWave==2,"Missing energy blocks the authentic second security lock");
            g.save.energy=100;
            Action(g,"lastdirectivecore","last_directive_second");
            Check(g.LastDirectiveWave==3&&g.LastDirectiveEnemies==3&&
                g.save.energy==75&&g.enemies.Exists(e=>e.id==90304&&e.kind==2),
                "Second security phase consumes 25 real energy and spawns armored AI");
            foreach(int id in new[]{90302,90303,90304})
            {
                g.hitStop=0;
                hit.Invoke(g,new object[]{g.enemies.Find(e=>e.id==id),10000f,false,null,null});
            }
            g.hitStop=0;g.Tick(.05f,new Point());
            Check(g.LastDirectiveWave==4&&g.LastDirectiveEnemies==0,
                "Defeating all three actual second-wave guards opens the civic verdict");
            Action(g,"lastdirectivecore","last_directive_open");
            Check(g.LastDirectiveChoice==1&&g.LastDirectiveWave==0,
                "The final live terminal permanently records a distinct public-oversight choice");
            g.Enter(0);g.enemies.Clear();
            board=Object(g,"lastdirectiveboard");g.save.x=board.x;g.save.y=board.y;
            g.save.level=30;
            int before=g.save.coins,shards=g.save.shards,talent=g.save.skillPoints;
            Action(g,"lastdirectiveboard","last_directive_publish");
            Check(g.LastDirectiveComplete&&g.save.coins==before+95&&
                g.save.shards==shards+5&&g.save.skillPoints==talent+1,
                "Town publication: complete="+g.LastDirectiveComplete+" choice="+g.LastDirectiveChoice+" coins="+g.save.coins+" expected="+(before+95)+" shards="+g.save.shards+" expected="+(shards+5)+" talents="+g.save.skillPoints+" expected="+(talent+1));
            g.Talk(board);
            Check(!Array.Exists(g.conversation.choices,c=>c.action=="last_directive_publish"),
                "Published final order cannot be claimed repeatedly");
            g.Close();g.PrepareSave();
            loaded=new GameCore(data,CloneSave(g.save));loaded.Start();
            Check(loaded.LastDirectiveComplete&&loaded.LastDirectiveChoice==1&&
                loaded.LastDirectiveClues==3&&loaded.save.trackedQuest=="last_directive",
                "Original SaveData v2 reload retains authentic completed story and choice");
        }
        public static void ChapterTenCannotForgeCivilianRightsOrLoseOriginalWildlife(Campaign data)
        {
            var g=Fresh(data);g.save.stage=4;g.save.flags.Add("civic_response_ledger");
            var choose=typeof(GameCore).GetMethod("ChooseLastDirective",
                System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            choose.Invoke(g,new object[]{"last_directive_publish"});
            Check(!g.LastDirectiveComplete,
                "A remote forged publication cannot grant Chapter Ten rewards");
            g.Enter(4);g.enemies.Clear();
            var terminal=Object(g,"lastdirectivecore");
            g.save.x=terminal.x;g.save.y=terminal.y;
            choose.Invoke(g,new object[]{"last_directive_open"});
            choose.Invoke(g,new object[]{"last_directive_shelter"});
            choose.Invoke(g,new object[]{"last_directive_code_0"});
            Check(g.LastDirectiveChoice==0&&g.LastDirectiveEnemies==0,
                "Both ending options and combat are locked before the genuine field testimony");
            g.save.flags.Add("last_directive_started");
            for(int i=0;i<3;i++)g.save.flags.Add("last_directive_clue_"+i);
            g.save.x=terminal.x;g.save.y=terminal.y;
            Action(g,"lastdirectivecore","last_directive_code_0");
            var arrow=new Projectile{friendly=true,position=g.Position,velocity=new Point(1,0),life=3};
            g.projectiles.Add(arrow);
            // Despawned security is not counted as a legitimate combat victory.
            g.enemies.RemoveAll(e=>e.id==90300);
            g.hitStop=0;g.Tick(.05f,new Point());
            Check(g.LastDirectiveWave==0&&g.LastDirectiveChoice==0&&
                !g.enemies.Exists(e=>e.id==90301)&&g.projectiles.Contains(arrow),
                "Missing guard resets only the optional encounter and preserves normal projectiles");
            for(int room=1;room<=7;room++)
            {
                var other=Fresh(data);other.save.room=room;
                other.save.flags.Add("boss_"+room);
                other.save.flags.Add("ridge_seen_"+room+"_0");
                other.save.flags.Add("ridge_seen_"+room+"_1");
                var spawn=typeof(GameCore).GetMethod("SpawnRoom",
                    System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
                spawn.Invoke(other,null);
                int count=0;foreach(var foe in other.enemies)
                    if(foe.x>=74&&foe.y>=60)count++;
                Check(count==3,
                    "The five new story interactables do not suppress original regional wildlife spawns");
            }
        }
        public static void ChapterTenLastWaveHasDodgeableRealDamageSurgesAndCleansOnlyOwnedHazards(Campaign data)
        {
            var g=Fresh(data);g.save.stage=4;
            g.save.flags.Add("last_directive_started");
            for(int i=0;i<3;i++)g.save.flags.Add("last_directive_clue_"+i);
            g.Enter(4);g.enemies.Clear();
            var panel=Object(g,"lastdirectivecore");
            g.save.x=panel.x;g.save.y=panel.y;
            Action(g,"lastdirectivecore","last_directive_code_0");
            var hit=typeof(GameCore).GetMethod("Hit",
                System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            foreach(int id in new[]{90300,90301})
            {g.hitStop=0;hit.Invoke(g,new object[]{g.enemies.Find(e=>e.id==id),10000f,false,null,null});}
            g.hitStop=0;g.Tick(.05f,new Point());
            g.save.energy=100;
            Action(g,"lastdirectivecore","last_directive_second");
            Check(g.LastDirectiveWave==3,"Actual verified second wave precedes conduit surges");
            g.save.maxHealth=2000;g.save.health=2000;g.invulnerable=0;
            var timer=typeof(GameCore).GetField("directiveSurgeLeft",
                System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            Check(timer!=null,"Conduit surge uses a timed actual combat warning");
            timer.SetValue(g,.01f);
            g.hitStop=0;g.Tick(.05f,new Point());
            var hazard=g.hazards.Find(h=>h.lastDirectiveOwned);
            Check(hazard!=null&&hazard.timer>0&&hazard.kind=="rift"&&
                hazard.radius>1.3f&&(hazard.position-g.Position).Length<.2f,
                "The second combat wave telegraphs a real positioned timed damage zone");
            float life=g.save.health;g.invulnerable=0;g.hitStop=0;
            hazard.timer=.01f;g.Tick(.05f,new Point());
            Check(!g.hazards.Contains(hazard)&&g.save.health<life,
                "A real unresolved warning deals contact damage at its warned location");
            Check(g.effects.Exists(e=>e.lastDirectiveOwned),
                "Contact warning becomes an owned visible impact effect");
            timer.SetValue(g,.01f);g.hitStop=0;g.invulnerable=0;
            g.Tick(.05f,new Point());
            hazard=g.hazards.Find(h=>h.lastDirectiveOwned);
            Check(hazard!=null,"Final encounter can deliver another readable warning");
            g.save.x=hazard.position.x+4;
            g.save.y=hazard.position.y;
            life=g.save.health;g.hitStop=0;g.invulnerable=0;
            hazard.timer=.01f;g.Tick(.05f,new Point());
            Check(!g.hazards.Contains(hazard)&&g.save.health>=life-1,
                "The player can physically move outside the warning before it resolves");
            var normal=new Hazard{kind="tide",position=g.Position,
                radius=.7f,timer=10,damage=10};
            g.hazards.Add(normal);
            foreach(int id in new[]{90302,90303,90304})
            {g.hitStop=0;hit.Invoke(g,new object[]{g.enemies.Find(e=>e.id==id),10000f,false,null,null});}
            g.hitStop=0;g.Tick(.05f,new Point());
            Check(g.LastDirectiveWave==4&&
                !g.hazards.Exists(h=>h.lastDirectiveOwned)&&
                g.hazards.Contains(normal)&&
                !g.effects.Exists(e=>e.lastDirectiveOwned),
                "Actual three-guardian victory retires only chapter-owned hazards/effects");
        }
        public static void ChapterTenAlternativeShelterEndingHasRealLocalHealing(Campaign data)
        {
            var g=Fresh(data);g.save.stage=4;g.save.flags.Add("civic_response_ledger");
            g.save.flags.Add("last_directive_started");
            for(int i=0;i<3;i++)g.save.flags.Add("last_directive_clue_"+i);
            g.Enter(4);g.enemies.Clear();
            var console=Object(g,"lastdirectivecore");
            g.save.x=console.x;g.save.y=console.y;
            var hit=typeof(GameCore).GetMethod("Hit",
                System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            Action(g,"lastdirectivecore","last_directive_code_0");
            foreach(int id in new[]{90300,90301})
            {g.hitStop=0;hit.Invoke(g,new object[]{g.enemies.Find(e=>e.id==id),10000f,false,null,null});}
            g.hitStop=0;g.Tick(.05f,new Point());
            g.save.energy=100;Action(g,"lastdirectivecore","last_directive_second");
            foreach(int id in new[]{90302,90303,90304})
            {g.hitStop=0;hit.Invoke(g,new object[]{g.enemies.Find(e=>e.id==id),10000f,false,null,null});}
            g.hitStop=0;g.Tick(.05f,new Point());
            Action(g,"lastdirectivecore","last_directive_shelter");
            Check(g.LastDirectiveChoice==2&&!g.LastDirectiveComplete,
                "Distinct physically earned refuge ending cannot auto-publish a civic charter");
            g.save.health=45;g.hitStop=0;g.Tick(.05f,new Point());
            float with=g.save.health-45;
            g.save.flags.Remove("last_directive_shelter");
            g.save.health=45;g.hitStop=0;g.Tick(.05f,new Point());
            float without=g.save.health-45;
            Check(with>without+.08f,
                "Emergency refuge has a genuine safety-limited local healing rate at citadel terminal");
            g.save.flags.Add("last_directive_shelter");
            g.save.x=console.x-8;g.save.y=console.y;
            g.save.health=45;g.hitStop=0;g.Tick(.05f,new Point());
            Check(g.save.health-45<with,
                "The civic refuge benefit is not falsely applied from distant positions");
        }
        public static void ChapterTenPublicOversightActuallyRestoresSafeEnergyAndGuestDialogue(Campaign data)
        {
            var g=Fresh(data);g.save.flags.Add("last_directive_open");
            g.save.flags.Add("last_directive_complete");
            g.save.flags.Add("civic_response_ledger");
            var council=Object(g,"lastdirectiveboard");
            g.save.x=council.x;g.save.y=council.y;g.enemies.Clear();
            g.save.energy=20;g.hitStop=0;g.Tick(.05f,new Point());
            float with=g.save.energy-20;
            g.save.flags.Remove("last_directive_open");
            g.save.energy=20;g.hitStop=0;g.Tick(.05f,new Point());
            float without=g.save.energy-20;
            Check(with>without+.16f,
                "The public oversight choice creates a real, safety-limited town energy effect");
            g.save.flags.Add("last_directive_open");
            g.save.x=council.x+9;g.save.y=council.y;
            g.save.energy=20;g.hitStop=0;g.Tick(.05f,new Point());
            Check(g.save.energy-20<with,
                "The public energy benefit cannot activate remotely across Emberfall");
            g.Enter(9);g.enemies.Clear();
            var guest=Object(g,"innGuest");g.save.x=guest.x;g.save.y=guest.y;
            g.Talk(guest);
            Check(g.conversation.text.Contains("旅人可以质疑过载命令")&&
                g.conversation.textEn.Contains("challenge an overload order"),
                "The original living inn guest acknowledges the player's public-oversight ending in both languages");
            g.Close();
            g.save.flags.Remove("last_directive_open");
            g.save.flags.Add("last_directive_shelter");
            g.Talk(guest);
            Check(g.conversation.text.Contains("无条件的紧急庇护")&&
                g.conversation.textEn.Contains("unconditional emergency refuge"),
                "The alternative emergency-shelter ending has different meaningful NPC dialogue");
            g.Close();
            var chronicle=typeof(GameCore).GetMethod("StoryUnlocked",
                System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            var chapter=typeof(GameCore).GetMethod("WorldChronicleSection",
                System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            g.save.flags.Add("last_directive_started");
            g.save.flags.Add("last_directive_clue_0");
            chapter.Invoke(g,new object[]{"after"});
            Check((bool)chronicle.Invoke(g,new object[]{"lastdirective"})&&
                Array.Exists(g.conversation.choices,c=>c.action=="story_read_lastdirective"),
                "Chapter Ten appears inside the original read-only chronicle only when unlocked");
            int select=Array.FindIndex(g.conversation.choices,c=>c.action=="story_read_lastdirective");
            int savedFlags=g.save.flags.Count,coins=g.save.coins;
            g.Choose(select);
            Check(g.conversation.text.Contains("尚未到访")&&
                g.conversation.text.Contains("无条件民用紧急庇护")&&
                g.save.flags.Count==savedFlags&&g.save.coins==coins,
                "Chronicle reflects witnessed progress and chosen ending without revealing unknown testimony or granting rewards");
        }
        public static void ScoutPresenceMatchesInteractionAndSavedJourney(Campaign data)
        {
            var g=Fresh(data);WorldObject returned=Object(g,"returnscout");g.save.x=returned.x;g.save.y=returned.y;
            Check(g.Nearest()==null||g.Nearest().kind!="returnscout","The returned scout cannot become an invisible town interaction before the rescue");
            g.Interact();Check(g.mode=="world"&&g.conversation==null&&(g.save.tutorialBits&2)==0,"Pressing E at the absent scout cannot credit the resident tutorial");
            g.Talk(returned);Check(g.mode=="world"&&g.conversation==null&&!g.Has("scout_gift"),"Direct talk cannot open an absent returned scout or award thanks");
            g.PrepareSave();var beforeRescue=new GameCore(data,CloneSave(g.save));beforeRescue.Start();
            Check(beforeRescue.Nearest()==null||beforeRescue.Nearest().kind!="returnscout","Loading an unrescued save preserves the empty town position");
            beforeRescue.Interact();Check((beforeRescue.save.tutorialBits&2)==0&&beforeRescue.mode=="world","Loading cannot turn an absent resident into tutorial progress");
            WorldObject lyra=Object(beforeRescue,"guild");beforeRescue.save.x=lyra.x;beforeRescue.save.y=lyra.y;beforeRescue.Interact();
            Check(beforeRescue.mode=="dialog"&&beforeRescue.conversation.speaker==lyra.name&&(beforeRescue.save.tutorialBits&2)!=0,"Lyra's real conversation still completes the resident interaction tutorial");

            g.Enter(1);g.enemies.Clear();g.save.kills[1]=4;WorldObject scout=Object(g,"scout");g.save.x=scout.x;g.save.y=scout.y;
            Check(g.Nearest()==scout,"The stranded scout remains selectable before the rescue");g.Interact();
            Check(g.mode=="dialog"&&g.conversation.speaker==scout.name&&g.conversation.choices.Length==1&&g.conversation.choices[0].action=="scout","Clearing four beasts retains the original rescue conversation");
            int coins=g.save.coins;g.Choose(0);
            Check(g.Has("scout_saved")&&g.GateOpen&&g.save.coins==coins+20,"Rescue still opens the starcore path and awards the original twenty coins");
            g.save.tutorialBits&=~2;
            Check(g.Nearest()==null||g.Nearest().kind!="scout","The rescued scout cannot remain an invisible forest interaction");
            g.Interact();Check(g.mode=="world"&&g.conversation==null&&(g.save.tutorialBits&2)==0,"The vacated forest position cannot produce a conversation or phantom tutorial credit");
            g.Talk(scout);Check(g.mode=="world"&&g.conversation==null&&g.save.coins==coins+20,"Talking to the retired forest scout cannot repeat the rescue reward");
            g.PrepareSave();var afterRescue=new GameCore(data,CloneSave(g.save));afterRescue.Start();
            Check(afterRescue.Has("scout_saved")&&(afterRescue.Nearest()==null||afterRescue.Nearest().kind!="scout"),"Loading the rescued forest save keeps the scout absent");
            afterRescue.Interact();Check((afterRescue.save.tutorialBits&2)==0&&afterRescue.mode=="world","Resumed forest interaction respects the saved rescue state");
            afterRescue.Enter(0);returned=Object(afterRescue,"returnscout");afterRescue.save.x=returned.x;afterRescue.save.y=returned.y;
            Check(afterRescue.Nearest()==returned,"The rescued scout is selectable at the real town homecoming position");afterRescue.Interact();
            Check(afterRescue.mode=="dialog"&&afterRescue.conversation.speaker==returned.name&&afterRescue.conversation.choices[0].action=="scoutgift","The homecoming retains the original thank-you conversation");
            int stew=afterRescue.save.inventory[6];afterRescue.Choose(0);
            Check(afterRescue.Has("scout_gift")&&afterRescue.save.inventory[6]==stew+1,"The scout's thanks awards one stew");
            Action(afterRescue,"returnscout","scoutgift");Check(afterRescue.save.inventory[6]==stew+1,"Repeated homecoming conversations cannot duplicate the thank-you gift");
            afterRescue.PrepareSave();var home=new GameCore(data,CloneSave(afterRescue.save));home.Start();
            Check(home.Nearest()!=null&&home.Nearest().kind=="returnscout","Loading the homecoming save keeps the returned scout present");
            Action(home,"returnscout","scoutgift");Check(home.save.inventory[6]==stew+1,"The consumed gift remains consumed after loading");
        }
        public static void TutorialAndPresentationMigration(Campaign data)
        {
            var old = new SaveData { presentationVersion = 0, frontierFocus = true, stage = 1 }; old.flags.Add("intro"); old.flags.Add("exp_intro");
            var resumed = new GameCore(data, old); Check(resumed.save.trackedQuest == "shore" && resumed.TutorialStep == 8, "Existing players keep story focus without forced onboarding");
            var g = Fresh(data); Check(g.TutorialStep == 0, "New games start with guided movement");
            g.mode = "pause"; for (int i = 0; i < 30; i++) g.Tick(.05f, new Point(0, -1)); Check(g.TutorialStep == 0 && g.save.tutorialDistance == 0, "Paused movement cannot complete onboarding");
            g.Close(); for (int i = 0; i < 16; i++) g.Tick(.05f, new Point(0, -1)); Check(g.TutorialStep == 1 && g.Explored(0, g.save.x, g.save.y), "Actual walking advances onboarding and exploration");
            g.save.x = 18; g.save.y = 16; g.Interact(); Check((g.save.tutorialBits & 2) != 0, "Actual interaction is recorded"); g.Close();
            g.TrackQuest("main"); PracticeRun(g); g.TutorialAction(6); g.Enter(1); g.Tick(.05f, new Point()); Check(g.TutorialStep == 8, "Accepted actions and first expedition complete onboarding");
            g.save.largeText = true; g.save.effectsQuality = 0; g.Load(g.save); Check(g.save.largeText && g.save.effectsQuality == 0 && g.TutorialStep == 8, "Preferences survive load");
            string narrative = "A long line of travelers waited at the harbor.\n\n"; for (int i = 0; i < 6; i++) narrative += "钟声响起，山路上的每个名字都应该被记住。\n\n";
            var pages = DialogueLayout.Pages(narrative, true); Check(pages.Count > 1 && string.Concat(pages) == narrative, "Paged dialogue preserves all characters");
            g.RestartTutorial(); Check(g.TutorialStep == 0, "Onboarding can be replayed"); g.SkipTutorial(); Check(g.TutorialStep == 8, "Onboarding can be skipped");
        }
        public static void QuestTrackingTransitions(Campaign data)
        {
            var g = Fresh(data); var q = g.CurrentGuide; Check(q.room == 1 && g.NavigationTarget().Value.y == 3, "Main quest points to north exit");
            g.TrackQuest("shore"); Check(g.save.trackedQuest == "main", "Locked stories cannot replace tracking"); g.TrackQuest("ally1"); Check(g.CurrentGuide.room == 1, "Insufficient supplies point to gathering");
            g.save.inventory[2] = 6; q = g.CurrentGuide; Check(q.room == 0 && q.target.y == 7, "Gathered herbs point to the herbalist"); Action(g, "garden", "ally1"); Check(g.CurrentGuide.complete, "Delivery completes tracked request");
            g.TrackQuest("main"); g.save.flags.Add("scout_saved"); Check(g.CurrentGuide.target.x == 29 && g.CurrentGuide.Progress > 0, "Rescue switches guidance to altar");
            g.save.flags.Add("boss_1"); g.save.flags.Add("core_1"); q = g.CurrentGuide; Check(q.room == 0 && q.target.y == 17, "Core points back to Lyra"); Action(g, "guild", "deliver"); Check(g.CurrentGuide.room == 2, "Delivery switches chapter");
            g.TrackQuest("shore"); Check(g.save.frontierFocus && g.CurrentGuide.target.x == 26, "Unlocked shore story points to Lune");
            Action(g, "envoy", "exp_begin"); Check(g.save.trackedQuest == "shore" && g.CurrentGuide.room == 5 && g.CurrentGuide.target.x == 15, "Acceptance automatically tracks Mira");
            g.save.flags.Add("harbor_report"); Check(g.CurrentGuide.room == 6, "Harbor report points to desert"); g.save.flags.Add("desert_report"); Check(g.CurrentGuide.room == 7, "Desert report points to highlands");
            g.save.flags.Add("mountain_report"); Check(g.CurrentGuide.room == 5 && g.CurrentGuide.target.x == 18, "Reports point to council"); g.save.frontierEnding = 1; Check(g.CurrentGuide.complete && g.CurrentGuide.Progress == 1, "Settled covenant is complete");
            g.AcceptBounty(0); Check(g.save.trackedQuest == "bounty", "Accepting a contract automatically tracks it"); g.save.trackedQuest = "invalid"; g.save.explored = null; g.Load(g.save); Check(g.save.trackedQuest == "main" && g.save.explored.Length == data.maps.Length, "Malformed fields repaired");
        }

        public static void MissingTravelersOfferSupplyAndCombatRescues(Campaign data)
        {
            var hit = typeof(GameCore).GetMethod("Hit",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Check(hit != null, "Production combat is available for roadside defense");
            var g = Fresh(data); g.save.stage = 3; g.save.level = 10;
            Check(g.GetQuest("wayfarers").locked, "Side quest is locked before the scout rescue");
            g.save.flags.Add("scout_saved");
            int points = g.save.skillPoints;
            for (int room = 1; room <= 3; room++)
            {
                g.Enter(room); g.enemies.Clear();
                var npc = Object(g, "wayfarer" + room);
                g.save.x = npc.x; g.save.y = npc.y;
                Check(g.CanStand(g.Position), "Roadside NPC is placed on valid ground");
                int item = room == 1 ? 2 : room == 2 ? 1 : 4, coins = g.save.coins;
                int faction = room == 1 ? 2 : room == 2 ? 1 : 0, trust = g.save.reputation[faction];
                g.save.inventory[item] = 0;
                Action(g, npc.kind, "wayfarer_supply_" + room);
                Check(!g.Has("wayfarer_" + room) && g.save.coins == coins,
                    "Insufficient supplies cannot grant a rescue reward");
                g.save.inventory[item] = 3;
                Action(g, npc.kind, "wayfarer_supply_" + room);
                Check(g.Has("wayfarer_" + room) && g.save.inventory[item] == 1 &&
                    g.save.coins == coins + 20 &&
                    g.save.reputation[faction] == Math.Min(10, trust + 1),
                    "Each donated supply route awards one relevant faction trust and pays once");
                Action(g, npc.kind, "close");
                Check(g.save.coins == coins + 20, "A completed traveler cannot pay again");
            }
            Check(g.GetQuest("wayfarers").complete && g.Has("wayfarer_complete") &&
                g.save.skillPoints == points + 1,
                "All three travelers award exactly one bonus talent point");
            g.PrepareSave();
            var loaded = new GameCore(data, CloneSave(g.save)); loaded.Start();
            Check(loaded.Has("wayfarer_complete") && loaded.GetQuest("wayfarers").complete,
                "Three-region progress survives an old-schema save round trip");

            var fight = Fresh(data); fight.save.stage = 3; fight.save.level = 10;
            fight.save.flags.Add("scout_saved"); fight.Enter(1); fight.enemies.Clear();
            var healer = Object(fight, "wayfarer1");
            fight.save.x = healer.x; fight.save.y = healer.y;
            Action(fight, healer.kind, "wayfarer_defend_1");
            var foes = fight.enemies.FindAll(e => e.id >= 82000 && e.id < 82100);
            Check(fight.WayfarerEncounterRoom == 1 && fight.WayfarerIntegrity == 100 && foes.Count == 2 &&
                foes.TrueForAll(e => e.timer > 0 && e.timer < 2 && fight.CanStand(e.Position)),
                "A real two-enemy ambush starts on reachable terrain with playable timing");
            fight.PrepareSave(); var resumed = new GameCore(data, CloneSave(fight.save)); resumed.Start();
            Check(resumed.WayfarerEncounterRoom == 0 && resumed.WayfarerIntegrity == 0 && !resumed.Has("wayfarer_1") &&
                !resumed.enemies.Exists(e => e.id >= 82000 && e.id < 82100),
                "Saving mid-ambush never persists temporary attackers");
            int beforeKills = fight.save.kills[1], beforeCoins = fight.save.coins;
            int beforeMastery = fight.save.mastery[fight.save.weapon];
            foreach (Enemy foe in foes)
                hit.Invoke(fight, new object[] { foe, 9999f, false, null, null });
            Check(fight.save.kills[1] == beforeKills && fight.save.coins == beforeCoins,
                "Ambush enemies never farm ordinary drops, bounty kills or story gates");
            // Actual hits induce a brief hit-stop, so the encounter resolves
            // after combat resumes rather than inside the frozen damage frame.
            for (int frame = 0; frame < 8 && fight.WayfarerEncounterRoom != 0; frame++)
                fight.Tick(.05f, new Point());
            Check(fight.WayfarerEncounterRoom == 0 && fight.Has("wayfarer_1") &&
                fight.save.mastery[fight.save.weapon] == Math.Min(100, beforeMastery + 4) &&
                fight.save.coins == beforeCoins + 20,
                "Defeating both foes pays the same one-time rescue reward");

            var retry = Fresh(data); retry.save.stage = 3; retry.save.flags.Add("scout_saved");
            retry.Enter(1); retry.enemies.Clear();
            var traveler = Object(retry, "wayfarer1"); retry.save.x = traveler.x; retry.save.y = traveler.y;
            Action(retry, traveler.kind, "wayfarer_defend_1");
            Action(retry, traveler.kind, "wayfarer_abandon");
            Check(retry.WayfarerEncounterRoom == 0 && retry.WayfarerIntegrity == 0 && !retry.Has("wayfarer_1") &&
                !retry.enemies.Exists(e => e.id >= 82000 && e.id < 82100),
                "Withdrawing removes temporary attackers and leaves the rescue replayable");

            var defend = Fresh(data); defend.save.stage = 3; defend.save.flags.Add("scout_saved");
            defend.Enter(1); defend.enemies.Clear();
            var protectedNpc = Object(defend, "wayfarer1");
            defend.save.x = protectedNpc.x; defend.save.y = protectedNpc.y;
            Action(defend, protectedNpc.kind, "wayfarer_defend_1");
            var raiders = defend.enemies.FindAll(e => e.id >= 82000 && e.id < 82100);
            Check(raiders.Count == 2, "Protected traveler spawns real raiders");
            // Move the hero out of interception range to exercise the NPC target.
            bool foundSafe = false;
            for (int y = 3; y < defend.Map.rows.Length - 3 && !foundSafe; y++)
            for (int x = 3; x < defend.Map.rows[y].Length - 3 && !foundSafe; x++)
            {
                Point far = new Point(x + .5f, y + .5f);
                if ((far - new Point(protectedNpc.x, protectedNpc.y)).Length < 6 || !defend.CanStand(far)) continue;
                defend.save.x = far.x; defend.save.y = far.y; foundSafe = true;
            }
            Check(foundSafe, "Region offers a safe interception test position");
            Point waitingPosition = defend.Position;
            defend.invulnerable = 100;
            raiders[0].x = protectedNpc.x; raiders[0].y = protectedNpc.y;
            raiders[0].timer = 0;
            raiders[1].stun = 100;
            defend.Tick(.02f, new Point());
            Check(raiders[0].state == 1 && raiders[0].targetingWayfarer &&
                (raiders[0].target - new Point(protectedNpc.x, protectedNpc.y)).Length < .01f,
                "Unintercepted raider visibly winds up toward the traveler");
            float safe = defend.WayfarerIntegrity;
            raiders[0].stun = .4f;
            Advance(defend, .3f);
            Check(defend.WayfarerIntegrity == safe, "Staggering a raider delays damage to the traveler");
            Advance(defend, 1.2f);
            Check(defend.WayfarerIntegrity < safe && defend.WayfarerIntegrity > 0,
                "A completed, unobstructed attack actually damages the traveler");
            float injured = defend.WayfarerIntegrity;
            defend.save.x = protectedNpc.x; defend.save.y = protectedNpc.y;
            defend.save.inventory[2] = 1;
            Action(defend, protectedNpc.kind, "wayfarer_aid");
            Check(defend.WayfarerIntegrity == Math.Min(100, injured + 35) &&
                defend.save.inventory[2] == 0, "Field aid restores bounded safety and spends one real herb");
            defend.Talk(protectedNpc);
            Check(!Array.Exists(defend.conversation.choices, choice => choice.action == "wayfarer_aid"),
                "A single ambush cannot repeatedly buy more safety");
            defend.Close();
            defend.save.x = waitingPosition.x; defend.save.y = waitingPosition.y;
            Advance(defend, 25f);
            Check(defend.WayfarerEncounterRoom == 0 && defend.WayfarerIntegrity == 0 &&
                !defend.Has("wayfarer_1") && !defend.enemies.Exists(e => e.id >= 82000 && e.id < 82100),
                "Traveler defeat removes transient raiders and cannot award rescue progress");
            defend.save.x = protectedNpc.x; defend.save.y = protectedNpc.y;
            Action(defend, protectedNpc.kind, "wayfarer_defend_1");
            Check(defend.WayfarerEncounterRoom == 1 && defend.WayfarerIntegrity == 100,
                "Failed defense can restart without a permanent penalty or save migration");
            var interceptor = defend.enemies.Find(e => e.id >= 82000 && e.id < 82100 && e.kind == 0);
            var other = defend.enemies.Find(e => e != interceptor && e.id >= 82000 && e.id < 82100);
            Check(interceptor != null && other != null, "Retry creates the same two enemy archetypes");
            interceptor.x = protectedNpc.x; interceptor.y = protectedNpc.y; interceptor.timer = 0;
            other.stun = 100;
            defend.Tick(.02f, new Point());
            Check(interceptor.state == 1 && !interceptor.targetingWayfarer,
                "A hero holding position beside the traveler draws the raider's windup");
            Advance(defend, .85f);
            Check(defend.WayfarerIntegrity == 100 && defend.WayfarerEncounterRoom == 1,
                "Intercepting the attack protects the traveler without auto-completing the rescue");
        }

        public static void RoadwardenSignalsUnlockOnePersistentTravelReward(Campaign data)
        {
            var g = Fresh(data);
            float baseSpeed = g.Speed;
            var guide = g.GetQuest("signals");
            Check(!guide.locked && !guide.complete && guide.room == 1 && guide.steps.Count == 4, "The exploration quest is visible from a fresh save");
            g.TrackQuest("signals"); Check(g.save.trackedQuest == "signals", "Players can track the signal quest");
            g.Enter(9); g.enemies.Clear(); WorldObject desk = Object(g, "surveyDesk"); g.save.x = desk.x; g.save.y = desk.y;
            int startingCoins = g.save.coins, startingShards = g.save.shards, startingPoints = g.save.skillPoints;
            g.Interact(); Check(g.mode == "dialog" && g.conversation.choices[0].action == "close", "The chart cannot be claimed before finding the signals"); g.Choose(0);
            g.save.stage = 3;
            for (int room = 1; room <= 3; room++)
            {
                g.Enter(room); g.enemies.Clear();
                WorldObject beacon = Object(g, "survey" + room);
                Check(g.CanStand(new Point(beacon.x, beacon.y)), "Survey beacon must have clear footing in region " + room);
                g.save.x = beacon.x; g.save.y = beacon.y;
                g.Interact(); Check(g.Has("survey_" + room) && g.mode == "dialog", "In-world beacon interaction records the signal");
                g.Choose(0);
                int coins = g.save.coins, shards = g.save.shards;
                g.Talk(beacon); g.Choose(0);
                Check(g.save.coins == coins && g.save.shards == shards, "Re-reading a beacon cannot farm supplies");
                if (room < 3) Check(g.GetQuest("signals").room == room + 1, "Guidance advances to the next missing signal");
                if (room == 1) { g.Enter(9); g.Talk(Object(g, "innGuest")); Check(g.conversation.textEn.Contains("Once all three signals") && g.conversation.textEn.Contains("Coming home with a story"), "Traveler offers a mid-quest clue without losing original advice"); g.Close(); }
            }
            Check(g.save.coins == startingCoins + 36 && g.save.shards == startingShards + 3, "Each beacon grants its one-time supplies");
            Check(g.GetQuest("signals").room == 9 && !g.GetQuest("signals").complete, "The collected signals lead to the inn");
            g.Enter(9); g.enemies.Clear(); g.save.x = desk.x; g.save.y = desk.y;
            g.Interact(); Check(g.conversation.choices[0].action == "survey_finish", "Chart completion is an explicit player choice");
            g.Choose(0);
            Check(g.Has("survey_complete") && g.GetQuest("signals").complete, "The chart completes the tracked quest");
            g.save.english = true;
            Check(g.GetQuest("signals").next.Contains("travel to three beacons") && g.GetQuest("signals").reward.Contains("beacon travel"), "Completed quest clearly explains the unlocked travel network");
            g.save.english = false;
            Check(Math.Abs(g.Speed - baseSpeed - .25f) < .001f, "The permanent exploration reward changes actual movement");
            Check(g.save.coins == startingCoins + 96 && g.save.shards == startingShards + 8 && g.save.skillPoints >= startingPoints + 1, "Completion grants the advertised rewards");
            g.Talk(Object(g, "innGuest")); Check(g.conversation.textEn.Contains("helped three late travelers") && g.conversation.textEn.Contains("Coming home with a story"), "Inn traveler acknowledges the completed chart while preserving original guidance"); g.Close();
            int finalCoins = g.save.coins, finalShards = g.save.shards, finalPoints = g.save.skillPoints;
            g.Talk(desk); Check(g.conversation.choices[0].action == "close", "Finished chart cannot be redeemed again"); g.Choose(0);
            Check(g.save.coins == finalCoins && g.save.shards == finalShards && g.save.skillPoints == finalPoints, "Inn revisits cannot duplicate rewards");
            g.PrepareSave(); var resumed = new GameCore(data, CloneSave(g.save)); resumed.Start();
            Check(resumed.Has("survey_complete") && resumed.GetQuest("signals").complete && resumed.save.trackedQuest == "signals", "Existing save schema persists chart flags and tracking");
            Check(Math.Abs(resumed.Speed - baseSpeed - .25f) < .001f, "Movement bonus survives save and load");
        }
        public static void HomewardChartConnectsSafeRegionalTravel(Campaign data)
        {
            var g = Fresh(data);
            g.save.stage = 3;
            g.Enter(9);
            var desk = Object(g, "surveyDesk");
            g.Talk(desk);
            Check(g.conversation.choices.Length == 1, "An unfinished chart cannot offer relay travel");
            g.Close();
            foreach (int region in new[] { 1, 2, 3 }) g.save.flags.Add("survey_" + region);
            g.save.flags.Add("survey_complete");
            int coins = g.save.coins, shards = g.save.shards, points = g.save.skillPoints;
            for (int region = 1; region <= 3; region++)
            {
                g.Talk(desk);
                Check(g.conversation.choices.Length == 4 && g.conversation.choices[region].action == "survey_to_" + region, "Completed chart lists three distinct discovered destinations");
                g.enemies.Add(new Enemy(910 + region, 0, g.save.x, g.save.y, 100));
                g.Choose(region);
                Check(g.save.room == 9, "The relay cannot teleport the player away from nearby enemies");
                g.enemies.Clear();
                g.Talk(desk); g.Choose(region);
                Check(g.save.room == region && g.CanStand(g.Position), "Relay lands on valid walkable ground in region " + region);
                var beacon = Object(g, "survey" + region);
                Check((g.Position - new Point(beacon.x, beacon.y)).Length <= 1.75f, "Arrival is close enough to interact with the correct beacon");
                // Travel must not pick a collision-free tile that is unnecessarily
                // closer to a newly spawned enemy than another adjacent tile.
                Point[] offsets = {
                    new Point(0, 1), new Point(0, -1), new Point(1, 0), new Point(-1, 0),
                    new Point(1, 1), new Point(-1, 1), new Point(1, -1), new Point(-1, -1)
                };
                float landedClearance = 9999;
                foreach (Enemy enemy in g.enemies)
                    if (enemy.hp > 0) landedClearance = Math.Min(landedClearance, (enemy.Position - g.Position).Length);
                foreach (Point offset in offsets)
                {
                    Point candidate = new Point(beacon.x, beacon.y) + offset;
                    if (!g.CanStand(candidate)) continue;
                    float clearance = 9999;
                    foreach (Enemy enemy in g.enemies)
                        if (enemy.hp > 0) clearance = Math.Min(clearance, (enemy.Position - candidate).Length);
                    Check(landedClearance + .001f >= clearance,
                        "Beacon travel chooses the safest adjacent walkable tile instead of spawning beside a patrol");
                }
                g.Interact();
                Check(g.conversation != null && g.conversation.choices.Length == 2 && g.conversation.choices[1].action == "survey_home", "Linked beacon exposes a return route");
                g.enemies.Add(new Enemy(920 + region, 0, g.save.x, g.save.y, 100));
                g.Choose(1);
                Check(g.save.room == region, "Beacon return is blocked during nearby combat");
                g.enemies.Clear();
                g.Talk(beacon); g.Choose(1);
                Check(g.save.room == 9 && g.CanStand(g.Position) && (g.Position - new Point(desk.x, desk.y)).Length <= 1.75f, "Return arrives beside the chart without crossing walls");
                Check(g.save.coins == coins && g.save.shards == shards && g.save.skillPoints == points, "Repeated travel cannot duplicate chart or signal rewards");
            }
            g.save.stage = 0;
            g.Talk(desk); g.Choose(2);
            Check(g.save.room == 9, "A forged completion flag cannot bypass normal regional access requirements");
            g.save.stage = 3;
            g.PrepareSave();
            var restored = new GameCore(data, CloneSave(g.save)); restored.Start();
            restored.Talk(Object(restored, "surveyDesk")); restored.Choose(3);
            Check(restored.save.room == 3 && restored.CanStand(restored.Position), "Linked relay works after loading an existing-format save");
        }
        public static void RoadwardenLettersHaveFairPersistentCipherRewards(Campaign data)
        {
            var g = Fresh(data);
            g.save.stage = 3;
            Check(g.GetQuest("dispatches").locked, "The letter quest requires the restored chart");
            g.TrackQuest("dispatches");
            Check(g.save.trackedQuest != "dispatches", "Locked letters cannot be tracked");
            for (int id = 6; id <= 8; id++)
            {
                g.Enter(id - 5); g.enemies.Clear();
                g.Talk(Object(g, "history" + id));
                Check(g.conversation.choices.Length == 1, "Original lore has its original reading before chart unlock");
                g.Close();
            }
            foreach (int room in new[] { 1, 2, 3 }) g.save.flags.Add("survey_" + room);
            g.save.flags.Add("survey_complete");
            Check(!g.GetQuest("dispatches").locked && g.GetQuest("dispatches").room == 1,
                "The chart unlocks a quest guiding players to the first old letter");
            g.TrackQuest("dispatches");
            // Isolate quest rewards from ordinary XP level-up talents.
            g.save.level = 10; g.save.xp = 0;
            int coins = g.save.coins, shards = g.save.shards, points = g.save.skillPoints;
            int originalStewPrice = g.ShopStewCost;
            for (int id = 6; id <= 8; id++)
            {
                g.Enter(id - 5); g.enemies.Clear();
                WorldObject letter = Object(g, "history" + id);
                g.save.x = letter.x; g.save.y = letter.y;
                g.Talk(letter);
                Check(g.conversation.choices.Length == 2 && g.conversation.choices[1].action == "dispatch_open_" + id,
                    "Existing lore stays readable with an optional cipher choice");
                g.Choose(1);
                Check(g.conversation != null && g.conversation.choices.Length == 3,
                    "Each letter offers a three-answer cipher");
                g.Choose(0);
                Check(!g.Has("dispatch_" + id) && g.save.coins == coins && g.save.shards == shards,
                    "Wrong answers do not consume supplies or grant rewards");
                Check(g.conversation.choices[0].action == "dispatch_open_" + id,
                    "Players can retry a wrong answer");
                g.Choose(0);
                g.Choose(id == 7 ? 2 : 1);
                Check(g.Has("dispatch_" + id) && g.mode == "world",
                    "Correct answer completes the regional cipher");
                coins += 20; shards++;
                if (id == 8) { coins += 40; points++; }
                Check(g.save.coins == coins && g.save.shards == shards && g.save.skillPoints == points,
                    "Each letter and the final talent reward are one-time only");
                g.Talk(letter);
                Check(g.conversation.choices.Length == 1,
                    "Solved letters preserve lore but no longer offer rewards");
                g.Close();
                Check(g.GetQuest("dispatches").room == (id == 8 ? 9 : id - 4),
                    "Quest guidance advances to the next missing letter");
            }
            Check(g.Has("dispatch_complete") && g.GetQuest("dispatches").complete,
                "All three letters complete the exploration quest");
            Check(g.ShopStewCost == originalStewPrice - 2 &&
                g.GetQuest("dispatches").reward.Contains("热汤减价2"),
                "The promise permanently discounts inn stew and explains the benefit");
            g.Enter(9); g.enemies.Clear();
            g.Talk(Object(g, "innGuest"));
            Check(g.conversation.textEn.Contains("discounts stew by two coins"),
                "The inn traveler acknowledges the decoded promise");
            g.Close();
            g.Talk(Object(g, "surveyDesk"));
            Check(g.conversation.choices.Length == 5 && g.conversation.choices[4].action == "dispatch_recap",
                "The chart offers a permanent reread of the promise");
            g.Choose(4);
            Check(g.conversation.textEn.Contains("everyone deserves a way home"),
                "The chart retells the three letters without new rewards");
            g.Close();
            g.PrepareSave();
            var restored = new GameCore(data, CloneSave(g.save)); restored.Start();
            Check(restored.Has("dispatch_complete") && restored.GetQuest("dispatches").complete &&
                restored.save.trackedQuest == "dispatches" && restored.save.skillPoints == points,
                "Original save schema persists the decoded letters");
            restored.Enter(1); restored.enemies.Clear();
            restored.Talk(Object(restored, "history6"));
            Check(restored.conversation.choices.Length == 1 && restored.save.coins == coins,
                "Loading cannot duplicate rewards");
        }

        public static void RoadwardenVigilIsRealTwoWaveCombatWithPersistentOneTimeRewards(Campaign data)
        {
            var g = Fresh(data); g.save.stage = 3; g.save.level = 10;
            Check(g.GetQuest("vigil").locked, "Vigil is locked until the roadwarden promise is decoded");
            foreach (string flag in new[] { "survey_1", "survey_2", "survey_3", "survey_complete", "dispatch_complete" })
                g.save.flags.Add(flag);
            g.Enter(9); g.Talk(Object(g, "surveyDesk"));
            Check(g.conversation.textEn.Contains("two-wave defense drills"),
                "Completed chart tells players where to find the newly unlocked defense challenges");
            g.Close();
            var hit = typeof(GameCore).GetMethod("Hit", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Check(hit != null, "The production hit path is available to the regression");
            int totalCoins = g.save.coins, totalShards = g.save.shards, totalPoints = g.save.skillPoints;
            for (int region = 1; region <= 3; region++)
            {
                g.Enter(region); g.enemies.Clear();
                var beacon = Object(g, "survey" + region);
                g.save.x = beacon.x; g.save.y = beacon.y;
                g.enemies.Add(new Enemy(10000 + region, 0, g.save.x, g.save.y, 100));
                Action(g, beacon.kind, "road_trial_start_" + region);
                Check(!g.enemies.Exists(e => e.id >= 81000 && e.id < 81100),
                    "Cannot start a defense while enemies threaten the beacon");
                g.enemies.Clear();
                Action(g, beacon.kind, "road_trial_start_" + region);
                Check(g.enemies.FindAll(e => e.id >= 81000 && e.id < 81100).Count == 2,
                    "Each beacon starts with two real reachable combatants");
                foreach (var enemy in g.enemies)
                    Check(g.CanStand(enemy.Position) && g.NavigationPath(enemy.Position).Count > 0,
                        "Trial enemies spawn on reachable walkable terrain");
                g.PrepareSave();
                var resumed = new GameCore(data, CloneSave(g.save)); resumed.Start();
                Check(!resumed.Has("road_trial_" + region) &&
                    !resumed.enemies.Exists(e => e.id >= 81000 && e.id < 81100),
                    "Loading mid-drill restarts cleanly without phantom saved enemies");
                int kills = g.save.kills[region], coins = g.save.coins, shards = g.save.shards;
                for (int wave = 1; wave <= 2; wave++)
                {
                    var attackers = g.enemies.FindAll(e => e.id >= 81000 && e.id < 81100);
                    Check(attackers.Count == 2, "Each trial wave has exactly two opponents");
                    Check(attackers.TrueForAll(e => e.timer >= .5f && e.timer <= 2.5f),
                        "High-ID trial foes enter combat in seconds, not hours");
                    if (region == 1 && wave == 1)
                    {
                        bool attacking = false;
                        for (int frame = 0; frame < 80; frame++)
                        {
                            g.Tick(.05f, new Point());
                            if (g.enemies.Exists(e => e.id >= 81000 && e.id < 81100 && e.state == 1) ||
                                g.projectiles.Exists(p => p.roadTrialOwned)) attacking = true;
                            if (attacking) break;
                        }
                        Check(attacking, "Real trial AI starts a telegraphed attack within four seconds");
                    }
                    foreach (var enemy in attackers)
                        hit.Invoke(g, new object[] { enemy, 9999f, false, null, null });
                    Check(g.save.kills[region] == kills && g.save.coins == coins,
                        "Trial combat does not farm chapter kills, bounty credit or ordinary coins");
                    for (int frame = 0; frame < 8; frame++) g.Tick(.05f, new Point());
                }
                Check(g.Has("road_trial_" + region) && g.save.coins == coins + 35 &&
                    g.save.shards == shards + 2 + (region == 3 ? 3 : 0),
                    "Clearing both waves grants exactly one region reward");
                g.Talk(beacon);
                Check(Array.Exists(g.conversation.choices, c => c.action == "road_trial_start_" + region) &&
                    g.conversation.textEn.Contains("rewards cannot be claimed twice"),
                    "Completed beacon unlocks a clearly labeled veteran replay without repeat rewards");
                g.Close();
            }
            Check(g.Has("road_trial_complete") && g.GetQuest("vigil").complete &&
                g.save.coins == totalCoins + 105 && g.save.shards == totalShards + 9 &&
                g.save.skillPoints == totalPoints + 1,
                "All three defenses grant the one-time full-vigil talent reward");
            g.Enter(9); g.Talk(Object(g, "innGuest"));
            Check(g.conversation.textEn.Contains("All three beacons are defended") &&
                g.conversation.textEn.Contains("Coming home with a story"),
                "The inn acknowledges the vigil while preserving original traveler advice");
            g.Close();
            g.PrepareSave(); var loaded = new GameCore(data, CloneSave(g.save)); loaded.Start();
            Check(loaded.Has("road_trial_complete") && loaded.GetQuest("vigil").complete &&
                loaded.save.skillPoints == totalPoints + 1,
                "Vigil completion and talent persist in the original save schema");
        }


        public static void RoadwardenVeteranReplayAddsAThirdWaveWithoutFarmingRewards(Campaign data)
        {
            var hit = typeof(GameCore).GetMethod("Hit",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Check(hit != null, "Veteran drill uses the production hit path");
            for (int region = 1; region <= 3; region++)
            {
                var g = Fresh(data); g.save.stage = 3; g.save.level = 10;
                foreach (string flag in new[] { "survey_1", "survey_2", "survey_3",
                    "survey_complete", "dispatch_complete", "road_trial_1",
                    "road_trial_2", "road_trial_3", "road_trial_complete" })
                    g.save.flags.Add(flag);
                g.Enter(region); g.enemies.Clear();
                var beacon = Object(g, "survey" + region);
                g.save.x = beacon.x; g.save.y = beacon.y;
                int coins = g.save.coins, shards = g.save.shards;
                int points = g.save.skillPoints, xp = g.save.xp, kills = g.save.kills[region];
                Action(g, beacon.kind, "road_trial_start_" + region);
                Check(g.RoadwardenTrialRoom == region && g.RoadwardenTrialTotalWaves == 3,
                    "Previously defended beacon starts an independent three-wave veteran trial");
                for (int wave = 1; wave <= 3; wave++)
                {
                    Check(g.RoadwardenTrialWave == wave, "Veteran drill advances to wave " + wave);
                    var attackers = g.enemies.FindAll(e => e.id >= 81000 && e.id < 81100);
                    Check(attackers.Count == 2 && attackers.TrueForAll(e =>
                        e.maxHp >= 75 + region * 15 + (wave - 1) * 30),
                        "Veteran wave has two stronger, real combatants");
                    if (wave == 3)
                        Check(attackers.Exists(e => e.kind == 2) &&
                            attackers.Exists(e => e.kind == 0),
                            "Final wave combines armored breaker with close-range hunter");
                    foreach (Enemy enemy in attackers)
                        hit.Invoke(g, new object[] { enemy, 9999f, false, null, null });
                    for (int frame = 0; frame < 8; frame++) g.Tick(.05f, new Point());
                }
                Check(g.RoadwardenTrialRoom == 0 && g.enemies.FindAll(e =>
                    e.id >= 81000 && e.id < 81100).Count == 0,
                    "Veteran drill finishes cleanly after its third wave");
                Check(g.save.coins == coins && g.save.shards == shards &&
                    g.save.skillPoints == points && g.save.xp == xp &&
                    g.save.kills[region] == kills,
                    "Veteran clears cannot farm one-time oath rewards or ordinary combat loot");
                Action(g, beacon.kind, "road_trial_start_" + region);
                Check(g.RoadwardenTrialWave == 1, "Completed veteran trial remains replayable");
                Action(g, beacon.kind, "road_trial_abandon");
                Check(g.RoadwardenTrialRoom == 0 && g.hazards.Count == 0 &&
                    !g.enemies.Exists(e => e.id >= 81000 && e.id < 81100),
                    "Abandoning a veteran replay clears its transient combat state");
            }
        }

        public static void RoadwardenBeaconWardIsARealDefendableObjective(Campaign data)
        {
            var g = Fresh(data); g.save.stage = 3; g.save.level = 10;
            foreach (string flag in new[] { "survey_1", "survey_2", "survey_3",
                "survey_complete", "dispatch_complete" }) g.save.flags.Add(flag);
            g.Enter(1); g.enemies.Clear();
            WorldObject beacon = Object(g, "survey1");
            g.save.x = beacon.x; g.save.y = beacon.y;
            g.Talk(beacon);
            Check(g.conversation.textEn.Contains("drain the beacon ward"),
                "Players must be told that the beacon is a vulnerable objective");
            g.Close();
            Action(g, beacon.kind, "road_trial_start_1");
            Check(g.RoadwardenWardIntegrity == 100 && g.RoadwardenTrialRoom == 1,
                "Starting a drill creates a full transient beacon ward");
            // Position the hero away from the ward so the hunter channels
            // rather than interrupting itself to attack the hero.
            Point retreat = new Point(); bool found = false;
            for (int y = 2; y < 22 && !found; y++)
            for (int x = 2; x < 34 && !found; x++)
            {
                Point candidate = new Point(x + .5f, y + .5f);
                if ((candidate - new Point(beacon.x, beacon.y)).Length > 10 &&
                    g.CanStand(candidate) && g.NavigationPath(candidate).Count > 0)
                { retreat = candidate; found = true; }
            }
            Check(found, "The map offers a safe distant observation position");
            g.save.x = retreat.x; g.save.y = retreat.y;
            Enemy hunter = g.enemies.Find(e => e.id >= 81000 && e.id < 81100 && e.kind == 0);
            Check(hunter != null, "Wave one contains a real hunter");
            foreach (Enemy foe in g.enemies) foe.stun = 15;
            hunter.stun = 0; hunter.x = retreat.x; hunter.y = retreat.y; hunter.timer = 20;
            float strandedDistance = (hunter.Position - new Point(beacon.x, beacon.y)).Length;
            g.Tick(.05f, new Point());
            Check((hunter.Position - new Point(beacon.x, beacon.y)).Length < strandedDistance,
                "Trial attackers must pursue the ward even beyond ordinary aggro range");
            hunter.x = beacon.x; hunter.y = beacon.y; hunter.timer = 20;
            float start = g.RoadwardenWardIntegrity;
            for (int frame = 0; frame < 40; frame++) g.Tick(.05f, new Point());
            Check(g.RoadwardenWardIntegrity < start - 5 && g.RoadwardenWardUnderAttack,
                "A living attacker at the ward drains actual integrity over time");
            hunter.stun = 5;
            float interrupted = g.RoadwardenWardIntegrity;
            for (int frame = 0; frame < 20; frame++) g.Tick(.05f, new Point());
            Check(Math.Abs(g.RoadwardenWardIntegrity - interrupted) < .001f &&
                !g.RoadwardenWardUnderAttack, "Staggering an attacker interrupts the siphon");
            g.save.x = beacon.x; g.save.y = beacon.y;
            int energyBeforeRepair = (int)g.save.energy;
            float wardBeforeRepair = g.RoadwardenWardIntegrity;
            g.Talk(beacon);
            int repairChoice = -1;
            for (int i = 0; i < g.conversation.choices.Length; i++)
                if (g.conversation.choices[i].action == "road_trial_repair") repairChoice = i;
            Check(repairChoice >= 0, "Damaged ward offers a clear in-world repair choice");
            g.Choose(repairChoice);
            Check(Math.Abs(g.RoadwardenWardIntegrity - Math.Min(100, wardBeforeRepair + 25)) < .001f &&
                Math.Abs(g.save.energy - (energyBeforeRepair - 35)) < 1.1f &&
                !g.RoadwardenWardRepairAvailable,
                "Repair restores 25 ward for exactly 35 energy once per wave");
            g.Talk(beacon);
            Check(!Array.Exists(g.conversation.choices, c => c.action == "road_trial_repair"),
                "Repair cannot be spammed for free within the same wave");
            g.Close();
            g.save.x = retreat.x; g.save.y = retreat.y;
            hunter.stun = 0; hunter.timer = 20;
            var fire = typeof(GameCore).GetMethod("Fire",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Enemy trialArcher = g.enemies.Find(e => e.id >= 81000 && e.id < 81100 && e.kind == 1);
            Check(fire != null && trialArcher != null, "The trial uses a real archer projectile path");
            fire.Invoke(g, new object[] { trialArcher, new Point(1, 0), 5f });
            Check(g.projectiles.Exists(p => p.roadTrialOwned),
                "Arrows fired by trial archers are identified before leaving the combat state");
            typeof(GameCore).GetField("roadTrialWard",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(g, .1f);
            g.Tick(.05f, new Point());
            Check(g.RoadwardenTrialRoom == 0 && g.RoadwardenWardIntegrity == 0 &&
                !g.enemies.Exists(e => e.id >= 81000 && e.id < 81100) &&
                !g.projectiles.Exists(p => p.roadTrialOwned) &&
                !g.Has("road_trial_1"), "Broken ward fails without rewards, enemies or lingering drill arrows");
            g.save.x = beacon.x; g.save.y = beacon.y;
            Action(g, beacon.kind, "road_trial_start_1");
            Check(g.RoadwardenWardIntegrity == 100, "Retry starts with a fresh ward");
            trialArcher = g.enemies.Find(e => e.id >= 81000 && e.id < 81100 && e.kind == 1);
            Check(trialArcher != null, "Retry still spawns a live archer");
            fire.Invoke(g, new object[] { trialArcher, new Point(1, 0), 5f });
            Check(g.projectiles.Exists(p => p.roadTrialOwned), "Retry arrows are tracked");
            Action(g, beacon.kind, "road_trial_abandon");
            Check(!g.projectiles.Exists(p => p.roadTrialOwned) && g.RoadwardenTrialRoom == 0,
                "Abandoning the trial removes its pending arrows");
            Action(g, beacon.kind, "road_trial_start_1");
            Check(g.RoadwardenWardIntegrity == 100, "Restart after abandonment is healthy");
            g.PrepareSave(); var resumed = new GameCore(data, CloneSave(g.save)); resumed.Start();
            Check(resumed.RoadwardenTrialRoom == 0 && resumed.RoadwardenWardIntegrity == 0 &&
                !resumed.enemies.Exists(e => e.id >= 81000 && e.id < 81100),
                "Existing saves never persist an unfinished ward defense");
        }

        public static void RoadwardenVigilRegionalSurgesAreReadableAndTransient(Campaign data)
        {
            var hit = typeof(GameCore).GetMethod("Hit",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Check(hit != null, "Production combat hit path is available");
            for (int region = 1; region <= 3; region++)
            {
                var g = Fresh(data); g.save.stage = 3; g.save.level = 10;
                foreach (string flag in new[] { "survey_1", "survey_2", "survey_3",
                    "survey_complete", "dispatch_complete" }) g.save.flags.Add(flag);
                g.Enter(region); g.enemies.Clear();
                var beacon = Object(g, "survey" + region);
                g.save.x = beacon.x; g.save.y = beacon.y;
                g.Talk(beacon);
                Check(g.conversation.textEn.Contains(region == 1 ? "thorn pincers" :
                    region == 2 ? "sweeping tides" : "mine bursts"),
                    "Players are warned about their region's unique second wave");
                g.Close();
                Action(g, beacon.kind, "road_trial_start_" + region);
                foreach (Enemy foe in g.enemies.FindAll(e => e.id >= 81000 && e.id < 81100))
                    hit.Invoke(g, new object[] { foe, 9999f, false, null, null });
                for (int frame = 0; frame < 8; frame++) g.Tick(.05f, new Point());
                Check(g.RoadwardenTrialWave == 2, "Defeating wave one starts wave two");
                foreach (Enemy foe in g.enemies) foe.stun = 12;
                for (int frame = 0; frame < 32; frame++) g.Tick(.05f, new Point());
                string kind = region == 1 ? "root" : region == 2 ? "tide" : "glass";
                var warnings = g.hazards.FindAll(h => h.kind == kind);
                Check(warnings.Count == 3, "Region " + region + " did not schedule its hazard; mode=" + g.mode + ", wave=" + g.RoadwardenTrialWave + ", enemies=" + g.enemies.Count + ", all hazards=" + g.hazards.Count + ", hp=" + g.save.health);
                foreach (Hazard warning in warnings)
                    Check(g.CanStand(warning.position) && warning.timer > 0 &&
                        warning.damage > 0 && warning.radius > 0,
                        "Hazards are telegraphed on traversable ground with real damage");
                if (region == 1)
                {
                    Hazard delayedRoot = warnings.Find(h => (h.position - g.Position).Length < .1f);
                    Check(delayedRoot != null, "Forest pincer must threaten the initially safe center after a delay");
                    foreach (Hazard root in warnings)
                        if (root != delayedRoot)
                            Check(delayedRoot.timer > root.timer + .3f,
                                "Forest roots close from the sides before the center erupts");
                }
                // A wave-two pattern must leave a reachable short-dodge escape.
                Point origin = g.Position;
                Point[] exits = { origin, origin + new Point(1.6f, 0),
                    origin + new Point(-1.6f, 0), origin + new Point(0, 1.6f),
                    origin + new Point(0, -1.6f) };
                bool escape = false;
                foreach (Point exit in exits)
                {
                    if (!g.CanStand(exit) || ((exit - origin).Length > .01f &&
                        g.NavigationPath(exit).Count == 0)) continue;
                    bool safe = true;
                    foreach (Hazard warning in warnings)
                        if ((exit - warning.position).Length <= warning.radius + .3f)
                        { safe = false; break; }
                    if (safe) { escape = true; break; }
                }
                Check(escape, "Region " + region + " has no reachable dodge-length escape");
                g.PrepareSave();
                var resumed = new GameCore(data, CloneSave(g.save)); resumed.Start();
                Check(resumed.hazards.Count == 0 && resumed.RoadwardenTrialRoom == 0,
                    "Loading during a defense cannot restore transient hazards");
                Action(g, beacon.kind, "road_trial_abandon");
                Check(g.RoadwardenTrialRoom == 0 && g.hazards.Count == 0 &&
                    !g.enemies.Exists(e => e.id >= 81000 && e.id < 81100),
                    "Abandoning removes both enemies and all pending regional strikes");
            }
        }

        public static void NavigationRespectsWorldCollision(Campaign data)
        {
            var g = Fresh(data); g.save.stage = 3; g.save.frontierEnding = 1;
            foreach (string flag in new[] { "scout_saved", "gate_2", "gate_3", "exp_intro", "harbor_report", "desert_report", "gate_5", "gate_6", "gate_7" }) g.save.flags.Add(flag);
            for (int room = 0; room < 8; room++)
            {
                g.save.flags.Add("seal_" + room); g.Enter(room); g.enemies.Clear(); g.save.totalTime = 0;
                foreach (var o in g.Map.objects)
                {
                    if (o.kind == "shade" || o.kind.StartsWith("castaway")) continue;
                    var target = new Point(o.x, o.y); var route = g.NavigationPath(target); Check(route.Count > 0 || (g.Position - target).Length < 1.8f, "A route must approach " + room + "/" + o.kind);
                    foreach (var p in route) Check(!g.Blocked(p.x, p.y) && !g.Blocked(p.x - .22f, p.y - .18f) && !g.Blocked(p.x + .22f, p.y + .18f), "Guide respects hero collision footprint");
                    if (route.Count > 0) Check((route[route.Count - 1] - target).Length <= 1.8f, "Guide ends in interaction range");
                }
            }
            g.Enter(5); g.save.totalTime = 24; Check(g.NavigationPath(new Point(25, 19)).Count == 0, "High-tide shoals cannot be crossed"); g.save.totalTime = 0; Check(g.NavigationPath(new Point(25, 19)).Count > 0, "Relay reachable at low tide");
            g.Enter(1); g.save.flags.Remove("scout_saved"); foreach (var p in g.NavigationPath(new Point(29, 8))) Check(!g.Blocked(p.x, p.y), "No guide crosses blocked gates");
        }
    }
}
