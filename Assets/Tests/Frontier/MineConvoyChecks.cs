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
        public static void MinecartJunctionGuardsAvoidActualCombatActorOccupancy(Campaign data)
        {
            var g=Fresh(data);g.save.stage=3;g.Enter(3);
            g.enemies.Clear();
            g.save.flags.Add("boss_3");g.save.flags.Add("gate_3");
            g.save.flags.Add("companion_1");g.SelectCompanion(1);
            g.invulnerable=120;
            WorldObject west=MineCartTestPost(g,1);
            WorldObject junction=MineCartTestPost(g,2);
            g.save.x=west.x;g.save.y=west.y;g.Talk(west);
            Check(MineCartTestChoice(g,"mineconvoy_start")&&
                g.MineConvoyStage==1,
                "The authentic Ironreach civilian cart starts at its old western dispatch");
            // A genuine final cart arrival (not a synthetic stage-2 flag)
            // triggers the guarded spawn transaction in the next Tick.
            var positionField=typeof(GameCore).GetField(
                "<MineCartPosition>k__BackingField",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);
            Check(positionField!=null,
                "The live minecart still owns its real position and motion state");
            positionField.SetValue(g,new Point(junction.x,junction.y));
            // Both preferred authored feet are occupied by distinct actors.
            // The enemy is unrelated to the escort and must not be moved,
            // killed or converted into an authored 91300-series combatant.
            var bystander=new Enemy(91823,0,62.5f,40.5f,800f)
                {stun=200f,timer=1000f};
            g.enemies.Add(bystander);
            g.save.x=junction.x;g.save.y=junction.y;
            g.companionPosition=new Point(65.5f,42.5f);
            Point allyBefore=g.companionPosition;
            g.Tick(.05f,new Point());
            var guards=g.enemies.FindAll(g.IsMineConvoyEnemy);
            Check(g.MineConvoyStage==2&&guards.Count==2&&
                g.enemies.Contains(bystander)&&
                (bystander.Position-new Point(62.5f,40.5f)).Length<.001f,
                "Cart reaches live contested junction with two actual guards and untouched pre-existing fighter");
            Check(guards[0].id==91300&&guards[1].id==91301,
                "Existing pair of kill-confirmed minecart enemy identities is stable");
            foreach(Enemy guard in guards)
            {
                Check(g.CanStand(guard.Position)&&
                    (guard.Position-bystander.Position).Length>=1.25f&&
                    (guard.Position-g.Position).Length>=1.25f&&
                    (guard.Position-g.companionPosition).Length>=1.25f&&
                    (guard.Position-g.MineCartPosition).Length>=1.25f&&
                    g.NavigationPath(guard.Position).Count>0,
                    "Each actual ambusher stands apart from the cart and actors on walkable reachable junction floor");
            }
            Check((guards[0].Position-guards[1].Position).Length>=1.25f&&
                (allyBefore-g.companionPosition).Length<1f,
                "New enemies may not overlap each other or teleport an unrelated recruited ally");
            Check(g.save.version==2&&!g.MineConvoyComplete,
                "An in-progress combat ambush cannot leak a permanent quest result or alter old saves");
        }

        public static void MinecartHullOnlyDrainsFromUnobstructedLivingGuards(Campaign data)
        {
            // The mission's authored three rail posts remain in place, but
            // the switchyard has one genuinely dynamic solid barrier.
            // The cart arrives by actual player movement, not a stage setter.
            MapData original=data.maps[3];
            try
            {
                var rows=new string[80];
                for(int y=0;y<80;y++)
                {
                    char[] cells=new string('#',120).ToCharArray();
                    if(y>=2&&y<=77)
                        for(int x=2;x<=117;x++)cells[x]='.';
                    rows[y]=new string(cells);
                }
                data.maps[3]=new MapData{id=3,
                    spawnX=original.spawnX,spawnY=original.spawnY,
                    rows=rows,objects=original.objects,
                    decor=new Decoration[0]};
                var g=Fresh(data);g.save.stage=3;g.Enter(3);
                g.enemies.Clear();
                g.save.flags.Add("boss_3");
                g.save.flags.Add("gate_3");
                g.invulnerable=120;
                WorldObject west=MineCartTestPost(g,1);
                WorldObject junction=MineCartTestPost(g,2);
                g.save.x=west.x;g.save.y=west.y;g.Talk(west);
                Check(MineCartTestChoice(g,"mineconvoy_start"),
                    "A real cart dispatch is required to test physical escort damage");
                WalkBesideMinecart(g,new Point(junction.x,junction.y),1);
                var guards=g.enemies.FindAll(g.IsMineConvoyEnemy);
                Check(g.MineConvoyStage==2&&guards.Count==2&&
                    g.MineConvoyIntegrity>0,
                    "The cart physically traverses the route and reaches its true two-raider encounter");
                Enemy threat=guards[0],other=guards[1];
                threat.x=junction.x+3f;threat.y=junction.y;
                threat.stun=100;threat.timer=1000;
                other.x=junction.x+12f;other.y=junction.y+8f;
                other.stun=100;other.timer=1000;
                // Test ordinary unguarded raider pressure from outside the
                // new, deliberately playable hero bodyguard lane. The
                // interception mechanic has its own real-position test.
                g.save.x=junction.x;g.save.y=junction.y+1.35f;
                Check(!g.MineConvoyBodyguarding,
                    "The old hull-pressure case does not already physically intercept its attacker");
                char[] closed=rows[(int)junction.y].ToCharArray();
                closed[(int)junction.x+1]='#';
                rows[(int)junction.y]=new string(closed);
                Check(g.CanStand(threat.Position)&&
                    g.CanStand(g.MineCartPosition)&&
                    g.Blocked(junction.x+1f,junction.y)&&
                    (threat.Position-g.MineCartPosition).Length<5.3f,
                    "A standing real guard is in damage range, but a dynamic wall seals its entire approach to the cart");
                float hull=g.MineConvoyIntegrity;
                g.Tick(.05f,new Point());
                Check(g.MineConvoyStage==2&&
                    Math.Abs(g.MineConvoyIntegrity-hull)<.0001f,
                    "Live timed junction combat must not drain civilian cart health through sealed cover");
                closed[(int)junction.x+1]='.';
                rows[(int)junction.y]=new string(closed);
                g.Tick(.05f,new Point());
                Check(Math.Abs(g.MineConvoyIntegrity-hull)<.0001f,
                    "A newly open corridor does not let a still-stunned guard sabotage the cart while unable to act");
                // A genuine combat stagger expires DURING this 50ms
                // frame. Only the 25ms remaining active slice can damage
                // the cart, not a free complete frame of pressure.
                threat.stun=.025f;
                g.Tick(.05f,new Point());
                float partial=hull-g.MineConvoyIntegrity;
                Check(partial>.055f&&partial<.11f,
                    "Stun ending halfway through a frame restores precisely partial civilian hull pressure");
                threat.stun=0;
                g.Tick(.05f,new Point());
                float active=partial-(hull-g.MineConvoyIntegrity);
                Check(active<-.12f&&active>-.23f&&
                    g.enemies.Contains(threat)&&g.enemies.Contains(other),
                    "Fully recovered raider again drains the original 3.3 HP/s cart integrity without erasing actors");
                Check(!g.MineConvoyComplete&&g.save.version==2,
                    "Physical collision adjustment changes no final reward, guard kill bit or old save version");
            }
            finally {data.maps[3]=original;}
        }


        public static void MinecartHeroCanPhysicallyBodyguardTheSwitchyard(Campaign data)
        {
            MapData original=data.maps[3];
            try
            {
                var rows=new string[80];
                for(int y=0;y<80;y++)
                {
                    char[] cells=new string('#',120).ToCharArray();
                    if(y>=2&&y<=77)
                        for(int x=2;x<=117;x++)cells[x]='.';
                    rows[y]=new string(cells);
                }
                data.maps[3]=new MapData{id=3,spawnX=original.spawnX,
                    spawnY=original.spawnY,rows=rows,objects=original.objects,
                    decor=new Decoration[0]};
                var g=Fresh(data);g.save.stage=3;g.Enter(3);
                g.enemies.Clear();g.save.flags.Add("boss_3");
                g.save.flags.Add("gate_3");g.invulnerable=120;
                WorldObject start=MineCartTestPost(g,1);
                WorldObject relay=MineCartTestPost(g,2);
                g.save.x=start.x;g.save.y=start.y;
                g.Talk(start);
                Check(MineCartTestChoice(g,"mineconvoy_start"),
                    "Real railway NPC starts the existing civilian rescue");
                WalkBesideMinecart(g,new Point(relay.x,relay.y),1);
                Check(g.MineConvoyStage==2&&g.MineConvoyIntegrity>0,
                    "Physical cart arrived at the contested switchyard");
                var actors=g.enemies.FindAll(g.IsMineConvoyEnemy);
                Check(actors.Count==2,"Existing two real saboteurs remain in the encounter");
                Enemy threat=actors[0],other=actors[1];
                other.x=relay.x+14;other.y=relay.y+9;
                other.stun=100;other.timer=100;
                threat.x=relay.x+3;threat.y=relay.y;
                threat.stun=0;threat.timer=100;
                g.save.x=relay.x+1.25f;g.save.y=relay.y;
                Check(g.MineConvoyBodyguarding &&
                    g.Conditions().Contains("正在拦截") &&
                    g.GetQuest("mine_convoy").next.Contains("挡住矿车破坏"),
                    "Hero standing directly in the real sabotage lane gets actionable live HUD and journal guidance");
                float hull=g.MineConvoyIntegrity;
                g.Tick(.05f,new Point());
                Check(g.MineConvoyBodyguarding&&
                    Math.Abs(g.MineConvoyIntegrity-hull)<.0001f &&
                    g.effects.Exists(e=>e.mineConvoyOwned&&
                        e.kind=="number"&&e.text=="挡住破坏"),
                    "Genuine pressure is prevented and a visible cue is emitted without deleting enemies");
                // Both saboteurs remain independently dangerous.
                // Blocking the eastern one never prevents the northern
                // guard from damaging the live civilian carriage.
                other.x=relay.x;other.y=relay.y+3f;
                other.stun=0;other.timer=100;
                Check(g.MineConvoyThreatLanes==2 &&
                    g.MineConvoyProtectedLanes==1 &&
                    g.Conditions().Contains("1/2"),
                    "One protected lane of two is counted in the actual tactical HUD");
                hull=g.MineConvoyIntegrity;
                g.Tick(.05f,new Point());
                Check(hull-g.MineConvoyIntegrity>.12f &&
                    hull-g.MineConvoyIntegrity<.23f,
                    "The unblocked second raider damages the cart despite the valid first interception");
                other.x=relay.x+14;other.y=relay.y+9;
                other.stun=100;other.timer=100;
                g.save.y=relay.y+1.5f;
                Check(!g.MineConvoyBodyguarding &&
                    g.Conditions().Contains("挡住袭击者破坏路线") &&
                    g.GetQuest("mine_convoy").next.Contains("站在袭击者"),
                    "Stepping out of the lane loses protection and visibly explains how to regain it");
                hull=g.MineConvoyIntegrity;g.Tick(.05f,new Point());
                Check(g.MineConvoyIntegrity<hull-.12f,
                    "The same living attacker immediately damages the unprotected civilian cart");
                g.save.x=relay.x+1.25f;g.save.y=relay.y;
                char[] barrier=rows[(int)relay.y].ToCharArray();
                barrier[(int)relay.x+1]='#';
                rows[(int)relay.y]=new string(barrier);
                Check(!g.MineConvoyBodyguarding,
                    "An invalid blocked root and sealed corridor cannot claim phantom bodyguard credit");
                float before=g.MineConvoyIntegrity;
                g.Tick(.05f,new Point());
                Check(Math.Abs(g.MineConvoyIntegrity-before)<.0001f,
                    "A closed wall cannot generate damage or paid bodyguard success");
                barrier[(int)relay.x+1]='.';
                rows[(int)relay.y]=new string(barrier);
                g.Tick(.05f,new Point());
                Check(g.MineConvoyBodyguarding&&
                    Math.Abs(g.MineConvoyIntegrity-before)<.0001f,
                    "Reopening the physical sabotage lane immediately restores real positioning play");
                Check(g.save.version==2&&!g.MineConvoyComplete,
                    "Transient hero protection never edits saves or pays the original quest reward");
            }
            finally{data.maps[3]=original;}
        }

        public static void MinecartStaleRailwayActionsCannotSpendOrRewardThroughClosedWall(Campaign data)
        {
            MapData original=data.maps[3];
            var rows=(string[])original.rows.Clone();
            try
            {
                data.maps[3]=new MapData {id=3,
                    spawnX=original.spawnX,spawnY=original.spawnY,
                    rows=rows,objects=original.objects,decor=original.decor};
                var g=Fresh(data);g.save.stage=3;g.Enter(3);
                g.enemies.Clear();g.save.flags.Add("boss_3");
                g.save.flags.Add("gate_3");g.invulnerable=180;
                WorldObject west=MineCartTestPost(g,1),
                    relay=MineCartTestPost(g,2),end=MineCartTestPost(g,3);
                g.save.x=west.x+1.75f;g.save.y=west.y;
                g.Talk(west);
                Check(g.mode=="dialog"&&g.CanStand(g.Position),
                    "The real western railway dispatch offers the original cart start on accessible ground");
                int wy=(int)Math.Floor(west.y),wx=(int)Math.Floor(west.x)+1;
                char[] blocked=rows[wy].ToCharArray();
                blocked[wx]='#';rows[wy]=new string(blocked);
                Check(g.CanStand(g.Position)&&g.Blocked(wx+.5f,wy+.5f),
                    "A real solid mine wall seals the dispatch while the hero stands beyond it");
                Check(MineCartTestChoice(g,"mineconvoy_start")&&
                    !g.MineConvoyActive&&!g.Has("mineconvoy_started"),
                    "Previously offered start cannot materialize a cart on the other side of solid cover");
                blocked[wx]='.';rows[wy]=new string(blocked);
                g.Talk(west);
                Check(MineCartTestChoice(g,"mineconvoy_start")&&
                    g.MineConvoyActive&&g.MineConvoyStage==1,
                    "Opening the exact wall starts the authentic railway mission");
                WalkBesideMinecart(g,new Point(relay.x,relay.y),1);
                Check(g.MineConvoyStage==2&&
                    g.enemies.FindAll(g.IsMineConvoyEnemy).Count==2,
                    "The physical cart reaches the original guarded switchyard");
                DefeatMinecartRaiders(g,relay);
                int coins=g.save.coins;
                g.save.x=relay.x+1.75f;g.save.y=relay.y;
                Check(g.CanStand(g.Position),
                    "The living hero may reach the east side of the original switchyard post");
                g.Talk(relay);
                int ry=(int)Math.Floor(relay.y),rx=(int)Math.Floor(relay.x)+1;
                char[] sw=rows[ry].ToCharArray();sw[rx]='#';
                rows[ry]=new string(sw);
                Check(g.CanStand(g.Position)&&g.Blocked(rx+.5f,ry+.5f),
                    "The switchyard now has a real full-wall divide between worker and hero");
                Check(MineCartTestChoice(g,"mineconvoy_go")&&
                    g.MineConvoyStage==2&&g.MineConvoyActive&&
                    g.save.coins==coins,
                    "A cached switch lever cannot move a defended cart forward through closed scenery");
                sw[rx]='.';rows[ry]=new string(sw);
                g.Talk(relay);
                Check(MineCartTestChoice(g,"mineconvoy_go")&&
                    g.MineConvoyStage==3,
                    "Opening the exact barrier resumes the legitimate full railway escort");
                WalkBesideMinecart(g,new Point(end.x,end.y),3);
                Check(g.MineConvoyStage==4&&g.MineConvoyIntegrity>0,
                    "The surviving real cart reaches the authored civilian shelter without a teleport");
                g.save.x=end.x+1.75f;g.save.y=end.y;
                Check(g.CanStand(g.Position),
                    "The original depot has standable eastern approach terrain");
                g.Talk(end);
                int ey=(int)Math.Floor(end.y),ex=(int)Math.Floor(end.x)+1;
                char[] finalWall=rows[ey].ToCharArray();
                finalWall[ex]='#';rows[ey]=new string(finalWall);
                Check(g.CanStand(g.Position)&&g.Blocked(ex+.5f,ey+.5f),
                    "A newly sealed last wall separates the opened completion panel and physical depot");
                Check(MineCartTestChoice(g,"mineconvoy_finish")&&
                    !g.MineConvoyComplete&&g.MineConvoyStage==4&&
                    g.save.coins==coins,
                    "Cached depot registry cannot mint rescues, currency or upgrade discounts through a wall");
                finalWall[ex]='.';rows[ey]=new string(finalWall);
                g.Talk(end);
                Check(MineCartTestChoice(g,"mineconvoy_finish")&&
                    g.MineConvoyComplete&&g.save.coins==coins+60&&
                    g.save.version==2,
                    "Reopening real depot contact awards exactly one valid civilian rescue without altering existing saves");
            }
            finally {data.maps[3]=original;}
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

        public static void CombatSignalsRetireOnlyTheirOwnImpactAndWarningEffects(Campaign data)
        {
            var g=Fresh(data);g.save.stage=3;g.Enter(3);g.enemies.Clear();
            g.save.flags.Add("boss_3");g.save.flags.Add("gate_3");
            WorldObject west=MineCartTestPost(g,1);
            g.save.x=west.x;g.save.y=west.y;g.invulnerable=120;
            g.Talk(west);
            Check(MineCartTestChoice(g,"mineconvoy_start"),
                "Real minecart must be active before its owned combat effects exist");
            g.projectiles.Add(new Projectile {
                position=new Point(west.x+1,west.y),velocity=new Point(),
                life=0,mineConvoyOwned=true,visual="enemyArrow" });
            g.projectiles.Add(new Projectile {
                position=new Point(west.x+2,west.y),velocity=new Point(),
                life=0,frostSignalOwned=true,visual="enemyArrow" });
            Enemy raider=new Enemy(91303,2,west.x+1.4f,west.y,100);
            raider.state=1;raider.timer=.01f;raider.target=g.Position;
            raider.Face(new Point(-1,0));
            g.enemies.Add(raider);
            g.Tick(.05f,new Point());
            Check(g.effects.Exists(e=>e.kind=="projectileFade"&&e.mineConvoyOwned)&&
                g.effects.Exists(e=>e.kind=="projectileFade"&&e.frostSignalOwned),
                "Real projectile expiration tags the correct authored combat source");
            Check(g.effects.Exists(e=>e.kind=="enemyRelease"&&e.mineConvoyOwned)&&
                g.effects.Exists(e=>e.kind=="burst"&&e.mineConvoyOwned)&&
                !g.effects.Exists(e=>e.kind=="danger"&&e.mineConvoyOwned),
                "Minecart raider release and post-contact burst inherit mission ownership without a phantom danger warning");
            g.Talk(west);
            Check(MineCartTestChoice(g,"mineconvoy_abort"),
                "The western civilian post can actually cancel its active fight");
            Check(!g.effects.Exists(e=>e.mineConvoyOwned)&&
                g.effects.Exists(e=>e.frostSignalOwned)&&
                !g.projectiles.Exists(p=>p.mineConvoyOwned),
                "Aborting clears only owned windups, impacts and fading shots; unrelated effects survive");
        }


        public static void MinecartDepotArrivalBanksOnPhysicalContactBeforeNextTimeout(Campaign data)
        {
            var reflection=System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance;
            var timer=typeof(GameCore).GetField("<MineConvoyTime>k__BackingField",reflection);
            var cart=typeof(GameCore).GetField("<MineCartPosition>k__BackingField",reflection);
            var hit=typeof(GameCore).GetMethod("Hit",reflection);
            Check(timer!=null&&cart!=null&&hit!=null,
                "Use actual worker cart, deadline field and real lethal enemy transaction");
            for(int scenario=0;scenario<3;scenario++)
            {
                var g=Fresh(data);g.save.stage=3;g.Enter(3);
                g.enemies.Clear();g.projectiles.Clear();g.invulnerable=180;
                g.save.reducedMotion=true;
                g.save.flags.Add("boss_3");g.save.flags.Add("gate_3");
                var dispatch=MineCartTestPost(g,1);
                var switchyard=MineCartTestPost(g,2);
                var depot=MineCartTestPost(g,3);
                g.save.x=dispatch.x;g.save.y=dispatch.y;g.Talk(dispatch);
                Check(MineCartTestChoice(g,"mineconvoy_start"),
                    "Workers genuinely dispatch this live cart on the original railway");
                WalkBesideMinecart(g,new Point(switchyard.x,switchyard.y),1);
                Check(g.MineConvoyStage==2&&
                    g.enemies.FindAll(g.IsMineConvoyEnemy).Count==2,
                    "Both actual switchyard raiders are alive before the physical fight");
                foreach(Enemy raider in g.enemies.FindAll(g.IsMineConvoyEnemy))
                    hit.Invoke(g,new object[]{raider,99999f,false,null,null});
                g.save.x=switchyard.x;g.save.y=switchyard.y;g.Talk(switchyard);
                Check(MineCartTestChoice(g,"mineconvoy_go")&&
                    g.MineConvoyStage==3,
                    "Only real lethal fight clearance releases the rescued cart for its final leg");
                int coins=g.save.coins;
                g.save.x=depot.x;g.save.y=depot.y;
                Point destination=new Point(depot.x,depot.y);
                Point placed=scenario==0?new Point(depot.x-.10f,depot.y):
                    scenario==1?new Point(depot.x-.20f,depot.y):
                    new Point(depot.x-2.5f,depot.y);
                Check(g.CanStand(placed)&&g.CanStand(destination),
                    "Real mine depot and final approach have walkable cart feet");
                cart.SetValue(g,placed);
                timer.SetValue(g,scenario==1?.055f:.012f);
                g.Tick(.05f,new Point());
                if(scenario==2)
                {
                    Check(!g.MineConvoyActive&&!g.MineConvoyComplete&&
                        g.save.coins==coins,
                        "Expired distant cart cannot fake an arrival or a worker reward");
                    continue;
                }
                Check(g.MineConvoyStage==4&&g.MineConvoyIntegrity>0&&
                    !g.MineConvoyComplete&&g.save.coins==coins&&
                    (g.MineCartPosition-destination).Length<.001f,
                    "The original depot banks a "+(scenario==0?"previous-frame arrival":"same-frame final step")+
                    " without automatically awarding the unsigned escort");
                // Actual worker paperwork is still required and pays once.
                g.Talk(depot);
                Check(MineCartTestChoice(g,"mineconvoy_finish")&&
                    g.MineConvoyComplete&&g.save.coins==coins+60,
                    "Only an authentic in-person depot handoff releases the original one-time reward");
                g.Tick(.05f,new Point());
                Check(g.save.coins==coins+60&&g.save.version==2,
                    "A banked and signed mine rescue never double-pays on later ticks");
            }
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
        public static void MinecartRequiresBothRealLethalKillsBeforeSwitchAndRewards(Campaign data)
        {
            var g=Fresh(data);g.save.stage=3;g.Enter(3);g.enemies.Clear();
            g.save.flags.Add("boss_3");g.save.flags.Add("gate_3");
            g.invulnerable=180;
            WorldObject west=MineCartTestPost(g,1),relay=MineCartTestPost(g,2),
                end=MineCartTestPost(g,3);
            int coins=g.save.coins,shards=g.save.shards;
            g.save.x=west.x;g.save.y=west.y;g.Talk(west);
            Check(MineCartTestChoice(g,"mineconvoy_start"),
                "Authentic western worker dispatch begins the escort");
            WalkBesideMinecart(g,new Point(relay.x,relay.y),1);
            Check(g.MineConvoyStage==2&&
                g.enemies.FindAll(g.IsMineConvoyEnemy).Count==2,
                "Actual cart reaches two live switchyard guards");
            // Removing either genuine foe without a lethal player callback
            // must not unlock repair, dispatch, or permanent compensation.
            var guards=g.enemies.FindAll(g.IsMineConvoyEnemy);
            g.enemies.Remove(guards[0]);
            g.save.x=relay.x;g.save.y=relay.y;g.Talk(relay);
            Check(!MineCartTestChoice(g,"mineconvoy_go")&&
                !MineCartTestChoice(g,"mineconvoy_brace"),
                "Missing guard without a verified defeat cannot offer a switch action");
            g.Close();
            g.Tick(.05f,new Point());
            Check(!g.MineConvoyActive&&!g.MineConvoyComplete &&
                g.save.coins==coins&&g.save.shards==shards &&
                !g.enemies.Exists(g.IsMineConvoyEnemy),
                "First missing hostile resets the cart and only its temporary actors");
            g.save.x=west.x;g.save.y=west.y;g.Talk(west);
            Check(MineCartTestChoice(g,"mineconvoy_start"),
                "Interrupted work escort can restart without charging resources");
            WalkBesideMinecart(g,new Point(relay.x,relay.y),1);
            Check(g.MineConvoyStage==2,
                "The retry physically reaches the same switchyard");
            // The canonical Hit() lethal path, not list manipulation,
            // authorizes both individual enemy identities for dispatch.
            var hit=typeof(GameCore).GetMethod("Hit",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            Check(hit!=null,"Combat death callback must be available");
            guards=g.enemies.FindAll(g.IsMineConvoyEnemy);
            hit.Invoke(g,new object[]{guards[0],99999f,false,null,null});
            Check(g.MineConvoyStage==2&&
                g.enemies.FindAll(g.IsMineConvoyEnemy).Count==1,
                "First real kill alone does not clear the mine switch");
            g.save.x=relay.x;g.save.y=relay.y;g.Talk(relay);
            Check(!MineCartTestChoice(g,"mineconvoy_go"),
                "Surviving second raider continues to guard the switch");
            g.Close();
            hit.Invoke(g,new object[]{guards[1],99999f,false,null,null});
            g.save.x=relay.x;g.save.y=relay.y;g.Talk(relay);
            Check(MineCartTestChoice(g,"mineconvoy_go")&&
                g.MineConvoyStage==3,
                "Two confirmed lethal combat kills enable supply-conserving transit");
            WalkBesideMinecart(g,new Point(end.x,end.y),3);
            Check(g.MineConvoyStage==4&&g.MineConvoyIntegrity>0,
                "The authentic second leg reaches its original depot");
            g.save.x=end.x;g.save.y=end.y;g.Talk(end);
            Check(MineCartTestChoice(g,"mineconvoy_finish")&&
                g.MineConvoyComplete&&g.save.coins==coins+60&&
                g.save.shards==shards+2,
                "The genuine two-kill route retains its original one-time rescue reward");
            g.PrepareSave();var loaded=new GameCore(data,CloneSave(g.save));
            Check(loaded.save.version==2&&loaded.MineConvoyComplete &&
                !loaded.MineConvoyActive,
                "SaveData v2 persists earned evacuation but no transient kill counters");
        }

    }
}
