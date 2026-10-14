using System;
namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        private static WorldObject MineCartTestPost(GameCore g,int stage)
        {
            foreach(WorldObject o in g.Map.objects)
                if(o.id=="mineconvoy_"+stage)return o;
            throw new Exception("Missing physical minecart station "+stage);
        }
        private static bool MineCartTestChoice(GameCore g,string action)
        {
            if(g.mode!="dialog"||g.conversation==null)return false;
            for(int i=0;i<g.conversation.choices.Length;i++)
                if(g.conversation.choices[i].action==action){g.Choose(i);return true;}
            return false;
        }
        private static void WalkBesideMinecart(GameCore g,Point target,int stage)
        {
            // The single canon hero follows the physically moving cart using
            // the real Tick locomotion, not teleportation or a stage flag.
            for(int frame=0;frame<1200&&g.MineConvoyStage==stage;frame++)
            {
                Point forward=(target-g.MineCartPosition).Normalized;
                Point desired=g.MineCartPosition+forward*1.05f;
                Point motion=(desired-g.Position).Length>.18f?
                    (desired-g.Position).Normalized:new Point();
                g.Tick(.05f,motion);
            }
        }
        private static void DefeatMinecartRaiders(GameCore g,WorldObject relay)
        {
            foreach(Enemy e in g.enemies.FindAll(g.IsMineConvoyEnemy))
            {
                e.stun=200;e.timer=200;e.hp=3;
                e.x=relay.x+1.05f;e.y=relay.y;
                g.save.x=relay.x;g.save.y=relay.y;
                for(int frame=0;frame<135&&g.enemies.Contains(e);frame++)
                {
                    if(g.attackCooldown<=0)g.Attack(new Point(1,0));
                    g.Tick(.02f,new Point());
                }
                Check(!g.enemies.Contains(e),
                    "Actual player combat defeats each minecart ambusher");
            }
        }
        public static void MinecartCivicTimberSavingsActuallyReachTownForge(Campaign data)
        {
            foreach(bool temper in new[]{false,true})
            {
                var before=Fresh(data);
                if(temper)before.save.flags.Add("iron_temper_complete");
                int baseCost=temper?2:3;
                Check(before.TemperUpgradeTimber==baseCost,
                    "Original old-save smithing recipe stays unchanged before escort completion");
                var g=Fresh(data);
                if(temper)g.save.flags.Add("iron_temper_complete");
                g.save.flags.Add("mineconvoy_complete");
                int savedCost=baseCost-1;
                Check(g.TemperUpgradeTimber==savedCost&&g.TemperUpgradeTimber>=1,
                    "The successful escort saves exactly one timber, stacking with the earned temper recipe");
                g.save.inventory[0]=savedCost;
                g.save.inventory[1]=temper?1:2;
                g.save.inventory[4]=temper?1:2;
                int shards=g.save.shards,coins=g.save.coins;
                WorldObject noll=Object(g,"forge");
                g.save.x=noll.x;g.save.y=noll.y;
                g.Talk(noll);
                bool shown=false;
                foreach(Choice choice in g.conversation.choices)
                    if(choice.action=="upgrade"&&
                        choice.en.Contains(savedCost+" timber"))shown=true;
                Check(shown&&g.conversation.text.Contains("最后一班矿工安全归来"),
                    "The original Noll smith menu and narrative advertise the actual upgrade discount");
                Check(MineCartTestChoice(g,"upgrade")&&
                    g.save.upgrades==1&&g.save.inventory[0]==0&&
                    g.save.inventory[1]==0&&g.save.inventory[4]==0,
                    "The authentic game upgrade transaction consumes exactly discounted timber and original crystals/parts");
                Check(g.save.coins==coins&&g.save.shards==shards,
                    "Returning reusable mine timber does not create free money or duplicate quest loot");
                g.PrepareSave();var restored=new GameCore(data,CloneSave(g.save));
                Check(restored.save.version==2&&restored.TemperUpgradeTimber==savedCost&&
                    restored.save.upgrades==1,
                    "The new workshop economy and prior equipment upgrades survive unchanged version-two save reload");
            }
        }

        public static void MinecartEngineeringReallyReducesSteamDamageAndAbortIsSelective(Campaign data)
        {
            float[] damages=new float[2];
            for(int branch=0;branch<2;branch++)
            {
                var g=Fresh(data);g.save.stage=3;g.Enter(3);g.enemies.Clear();
                g.save.flags.Add("boss_3");g.save.flags.Add("gate_3");
                g.invulnerable=120;
                WorldObject start=MineCartTestPost(g,1);
                WorldObject relay=MineCartTestPost(g,2);
                g.save.x=start.x;g.save.y=start.y;g.Talk(start);
                Check(MineCartTestChoice(g,"mineconvoy_start"),"The authored cart can launch");
                WalkBesideMinecart(g,new Point(relay.x,relay.y),1);
                Check(g.MineConvoyStage==2,"The cart must physically reach the switchyard");
                DefeatMinecartRaiders(g,relay);
                g.save.inventory[4]=1;
                g.save.x=relay.x;g.save.y=relay.y;g.Talk(relay);
                Check(MineCartTestChoice(g,branch==1?"mineconvoy_brace":"mineconvoy_go"),
                    "Players can choose the real repair or spare-conservation route");
                Check(g.MineConvoyStage==3,"Both strategies launch the southern escort");
                g.save.x=start.x;g.save.y=start.y;
                float original=g.MineConvoyIntegrity;
                for(int frame=0;frame<140;frame++)g.Tick(.05f,new Point());
                damages[branch]=original-g.MineConvoyIntegrity;
                Check(g.MineConvoyStage==3&&g.MineConvoyTime>0,
                    "Cart pauses while the player is remote but genuine steam keeps pulsing");
                var unrelated=new Hazard{position=new Point(21,14),radius=1f,
                    damage=9,timer=2.5f,kind="other"};
                g.hazards.Add(unrelated);
                g.Talk(start);
                Check(MineCartTestChoice(g,"mineconvoy_abort")&&
                    !g.MineConvoyActive&&!g.MineConvoyComplete,
                    "Physical western dispatch explicitly stops the active escort");
                Check(g.hazards.Contains(unrelated)&&
                    !g.hazards.Exists(h=>h.mineConvoyOwned)&&
                    !g.effects.Exists(e=>e.mineConvoyOwned)&&
                    !g.projectiles.Exists(p=>p.mineConvoyOwned),
                    "Aborting retires only escort-owned actors and warnings, never unrelated threats");
                Check(!g.Has("mineconvoy_braced"),
                    "Aborted optional repairs never incorrectly persist as successful evacuation history");
                g.PrepareSave();var loaded=new GameCore(data,CloneSave(g.save));
                Check(!loaded.MineConvoyActive&&loaded.save.version==2,
                    "After-abort old-version saves never inherit ghost cart warnings");
            }
            Check(Math.Abs(damages[0]-12f)<.15f&&
                Math.Abs(damages[1]-6f)<.15f,
                "Spending one part really halves precisely the same first steam contact from 12 to 6 hull");
        }

        public static void IronreachMinecartEscortProtectsRealWorkers(Campaign data)
        {
            var g=Fresh(data);
            Check(g.GetQuest("mine_convoy").locked,
                "Old chapter and original guardian gate the optional workers' rescue");
            g.save.stage=3;g.Enter(3);g.enemies.Clear();
            WorldObject dispatch=MineCartTestPost(g,1);
            WorldObject switchyard=MineCartTestPost(g,2);
            WorldObject depot=MineCartTestPost(g,3);
            foreach(WorldObject o in new[]{dispatch,switchyard,depot})
            {
                Check(g.CanStand(new Point(o.x,o.y)),
                    "A real minecart waypoint stands on original legal terrain");
                g.save.x=data.maps[3].spawnX;g.save.y=data.maps[3].spawnY;
                Check(g.NavigationPath(new Point(o.x,o.y)).Count>0,
                    "Each waypoint is reachable from the unchanged Ironreach spawn");
            }
            g.save.x=dispatch.x;g.save.y=dispatch.y;
            g.Talk(dispatch);
            Check(g.conversation!=null&&g.conversation.choices.Length==1,
                "Old guardian cannot be skipped by the new civilian railcart");g.Close();
            g.save.flags.Add("boss_3");g.save.flags.Add("gate_3");
            g.invulnerable=120;
            int beforeMoney=g.save.coins,oldKills=g.save.kills[3];
            g.Talk(dispatch);
            Check(MineCartTestChoice(g,"mineconvoy_start")&&g.MineConvoyStage==1&&
                g.MineConvoyTime==110&&g.MineConvoyIntegrity==80,
                "Physical dispatch starts a real countdown and cart hull");
            g.Enter(0);
            Check(g.save.room==3&&g.MineConvoyActive,
                "Direct region travel never discards the active civilian cart");
            g.ReturnToTown();
            Check(g.save.room==3,"Quick return cannot bypass the escort");
            g.PrepareSave();
            var reloadedMidway=new GameCore(data,CloneSave(g.save));
            Check(!reloadedMidway.MineConvoyActive&&
                !reloadedMidway.MineConvoyComplete&&reloadedMidway.save.version==2,
                "Unfinished carts never serialize into old SaveData v2");
            WalkBesideMinecart(g,new Point(switchyard.x,switchyard.y),1);
            Check(g.MineConvoyStage==2&&
                (g.MineCartPosition-new Point(switchyard.x,switchyard.y)).Length<.01f&&
                g.enemies.FindAll(g.IsMineConvoyEnemy).Count==2,
                "True escort progression brings the cart to two genuine combat raiders");
            g.PrepareSave();
            Check(!Array.Exists(g.save.worldEnemies,e=>e!=null&&e.id>=91300&&e.id<91310),
                "Authored attackers never leak into persistent ordinary enemy snapshots");
            g.save.x=switchyard.x;g.save.y=switchyard.y;
            g.Talk(switchyard);
            Check(!MineCartTestChoice(g,"mineconvoy_go")&&g.MineConvoyStage==2,
                "Raiders must really die before player can dispatch the car");
            g.Close();
            DefeatMinecartRaiders(g,switchyard);
            Check(g.save.coins==beforeMoney&&g.save.kills[3]==oldKills,
                "Minecart enemy deaths cannot farm ordinary story money/kills");
            g.save.inventory[4]=1;
            float oldHull=g.MineConvoyIntegrity;
            g.Talk(switchyard);
            Check(MineCartTestChoice(g,"mineconvoy_brace")&&
                g.MineConvoyStage==3&&g.save.inventory[4]==0&&
                g.MineConvoyIntegrity>=oldHull,
                "Spending one genuine spare part reinforces the workers' cart");
            Check(!g.Has("mineconvoy_braced"),
                "An attempted brace is not permanent until the workers reach safety");
            WalkBesideMinecart(g,new Point(depot.x,depot.y),3);
            Check(g.MineConvoyStage==4&&g.MineConvoyIntegrity>0,
                "Actual player movement and cart motion reach shelter despite steam");
            g.save.x=depot.x;g.save.y=depot.y;g.Talk(depot);
            Check(MineCartTestChoice(g,"mineconvoy_finish")&&
                g.MineConvoyComplete&&g.Has("mineconvoy_braced")&&
                g.save.coins==beforeMoney+60&&g.save.shards>=2,
                "Only physical final dispatch grants the one-time escort reward and saved brace story");
            g.Talk(depot);
            Check(g.conversation.choices.Length==1,
                "No finished civilian evacuation can pay its completion reward twice");
            g.Close();
            g.enemies.Clear();g.hazards.Clear();g.projectiles.Clear();
            g.save.health=60;g.Tick(.05f,new Point());
            Check(g.MineConvoyRefuge&&g.save.health>60.07f,
                "Restored depot heals only when the actual player is physically nearby and safe");
            g.save.x=data.maps[3].spawnX;g.save.y=data.maps[3].spawnY;
            Check(!g.MineConvoyRefuge,
                "The completed mine depot never gives free health remotely");
            g.PrepareSave();
            var reloaded=new GameCore(data,CloneSave(g.save));
            Check(reloaded.MineConvoyComplete&&reloaded.save.version==2&&
                reloaded.GetQuest("mine_convoy").complete,
                "All completed rescue and depot service flags survive original version-two saves");
        }
    }
}
