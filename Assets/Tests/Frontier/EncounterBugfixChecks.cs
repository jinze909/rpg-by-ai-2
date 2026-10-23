using System;
namespace Starfall.Tests
{
    public static partial class CoreChecks
    {



        public static void RoadwardenArcherDefendsFromRealRangedHeroLane(Campaign data)
        {
            MapData previous=data.maps[1];
            try
            {
                var rows=new string[34];
                for(int y=0;y<34;y++)
                {
                    char[] line=new string('#',34).ToCharArray();
                    if(y>=2&&y<=31)
                        for(int x=2;x<=31;x++)line[x]='.';
                    rows[y]=new string(line);
                }
                var beacon=new WorldObject{id="survey1",kind="survey1",
                    name="Beacon",en="Beacon",x=10.75f,y=10.5f};
                data.maps[1]=new MapData{id=1,spawnX=12.25f,
                    spawnY=10.5f,rows=rows,
                    objects=new[]{beacon},decor=new Decoration[0]};
                var g=Fresh(data);
                g.save.stage=3;g.save.level=10;
                g.save.flags.Add("dispatch_complete");
                g.save.flags.Add("survey_1");
                g.Enter(1);g.enemies.Clear();
                g.invulnerable=300f;
                g.save.x=12.25f;g.save.y=10.5f;
                Action(g,"survey1","road_trial_start_1");
                Check(g.RoadwardenTrialRoom==1&&g.enemies.Count==2,
                    "Real forest defense must deploy an actual hunter and ranged bowman");
                Enemy hunter=g.enemies.Find(e=>e.kind==0);
                Enemy bow=g.enemies.Find(e=>e.kind==1);
                Check(hunter!=null&&bow!=null,
                    "Wave one genuinely contains different melee and ranged combat roles");
                hunter.x=14.5f;hunter.y=10.5f;hunter.state=0;
                hunter.timer=1f;hunter.stun=0f;hunter.releaseTime=0f;
                bow.x=16f;bow.y=10.5f;bow.state=0;
                bow.timer=1f;bow.stun=0f;bow.releaseTime=0f;
                g.save.x=21.5f;g.save.y=10.5f;
                g.Tick(.05f,new Point());
                Check(hunter.x<14.49f&&hunter.moving&&
                    bow.x>16.01f&&bow.moving&&bow.moveTime>.01f&&
                    !g.RoadwardenWardUnderAttack,
                    "First real combat tick sends hunter WEST toward the ward but bowman EAST to establish a moving distance-locked hero shooting lane");
                // With a player inside the bowman's three-tile dead zone,
                // physical backstep must move AWAY from that player even
                // though this is toward neither the original station goal
                // nor an invented decorative walk pose.
                hunter.stun=100f;
                bow.x=16f;bow.y=10.5f;bow.timer=1f;
                bow.state=0;bow.stun=0;bow.releaseTime=0;
                g.save.x=14.5f;g.save.y=10.5f;
                g.Tick(.05f,new Point());
                Check(bow.backpedaling&&bow.x>16.02f&&bow.moving,
                    "Station archer retreats from the ACTUAL nearby hero, instead of rushing the station with the hunter");
                // Player moves back into a safe legitimate arrow lane. A
                // real attack requires a windup, followed by a flying shot;
                // no fake hitscan or pre-release missile may appear.
                g.save.x=20.5f;g.save.y=10.5f;
                bow.x=16f;bow.y=10.5f;
                bow.timer=.001f;bow.releaseTime=0f;bow.stun=0f;
                bow.state=0;bow.hitTime=0f;
                g.Tick(.05f,new Point());
                Check(bow.state==1&&
                    (bow.target-g.Position).Length<.001f&&
                    !g.projectiles.Exists(p=>p.roadTrialOwned),
                    "Real Roadwarden archer locks an actual player-targeted bow windup, not the protected survey stone");
                for(int i=0;i<15;i++)g.Tick(.05f,new Point());
                Check(g.projectiles.Exists(p=>!p.friendly&&p.roadTrialOwned&&
                    p.visual=="enemyArrow")&&g.save.version==2,
                    "True bow release launches the trial-owned traveling arrow, preserving all original projectile physics and v2 saves");
                // Keep the old, valuable objective pressure mechanic:
                // a bow enemy already beside the beacon still erodes its
                // ward, even though its tactical ranged TARGET is the hero.
                g.projectiles.Clear();
                bow.x=11.5f;bow.y=10.5f;
                bow.state=0;bow.timer=100f;
                bow.stun=bow.releaseTime=bow.hitTime=0f;
                float before=g.RoadwardenWardIntegrity;
                g.Tick(.05f,new Point());
                Check(g.RoadwardenWardUnderAttack&&
                    g.RoadwardenWardIntegrity<before&&
                    g.RoadwardenWardSiphonCount==1&&
                    (g.RoadwardenWardSiphonSource(0)-bow.Position).Length<.001f,
                    "A genuine bowman physically beside the ward still damages it and drives the one true native world tether");
                // The second wave replaces the hunter with a breaker. The
                // bowman's hero-pressure role must survive reinforcement.
                var hit=typeof(GameCore).GetMethod("Hit",
                    System.Reflection.BindingFlags.NonPublic|
                    System.Reflection.BindingFlags.Instance);
                Check(hit!=null,"Trial reinforcement still requires real lethal combat");
                hit.Invoke(g,new object[]{hunter,99999f,false,null,null});
                hit.Invoke(g,new object[]{bow,99999f,false,null,null});
                g.hitStop=0f;g.Tick(.05f,new Point());
                Check(g.RoadwardenTrialRoom==1&&g.RoadwardenTrialWave==2,
                    "Two genuine trial kills still spawn the actual second wave");
                Enemy breaker=g.enemies.Find(e=>e.kind==2);
                Enemy veteranBow=g.enemies.Find(e=>e.kind==1);
                Check(breaker!=null&&veteranBow!=null,
                    "Wave two contains the actual heavy breaker and returning bowman");
                breaker.x=14.5f;breaker.y=10.5f;breaker.state=0;
                breaker.timer=1f;breaker.stun=breaker.releaseTime=0f;
                veteranBow.x=16f;veteranBow.y=10.5f;
                veteranBow.state=0;veteranBow.timer=1f;
                veteranBow.stun=veteranBow.releaseTime=0f;
                g.save.x=21.5f;g.save.y=10.5f;
                g.Tick(.05f,new Point());
                Check(breaker.x<14.49f&&veteranBow.x>16.01f&&
                    breaker.moving&&veteranBow.moving,
                    "Veteran breaker advances on the station while the real second-wave archer keeps hero-focused ranged spacing");
            }
            finally{data.maps[1]=previous;}
        }

        public static void RoadwardenFinalLethalContactClearsOnlyItsOwnLiveSurge(Campaign data)
        {
            MapData original=data.maps[1];
            try
            {
                string[] rows=new string[34];
                for(int y=0;y<34;y++)
                {
                    char[] cells=new string('#',34).ToCharArray();
                    if(y>=2&&y<=31)
                        for(int x=2;x<=31;x++)cells[x]='.';
                    rows[y]=new string(cells);
                }
                var beacon=new WorldObject{id="survey1",kind="survey1",
                    name="Beacon",en="Beacon",x=10.75f,y=10.5f};
                data.maps[1]=new MapData{id=1,spawnX=12.25f,
                    spawnY=10.5f,rows=rows,
                    objects=new[]{beacon},decor=new Decoration[0]};
                var g=Fresh(data);g.save.stage=3;g.save.level=10;
                g.save.flags.Add("dispatch_complete");
                g.save.flags.Add("survey_1");
                g.Enter(1);g.enemies.Clear();g.invulnerable=180f;
                g.save.x=12.25f;g.save.y=10.5f;
                Action(g,beacon.kind,"road_trial_start_1");
                var raiders=g.enemies.FindAll(e=>e.id>=81000&&e.id<81100);
                Check(g.RoadwardenTrialRoom==1&&raiders.Count==2,
                    "Actual Roadwarden defense fields both first-wave attackers");
                var surge=typeof(GameCore).GetMethod("SpawnRoadwardenSurge",
                    System.Reflection.BindingFlags.NonPublic|
                    System.Reflection.BindingFlags.Instance);
                var hit=typeof(GameCore).GetMethod("Hit",
                    System.Reflection.BindingFlags.NonPublic|
                    System.Reflection.BindingFlags.Instance);
                Check(surge!=null&&hit!=null,
                    "Use production-authored strike and genuine lethal combat methods");
                var ambient=new Hazard {position=g.Position,
                    radius=.65f,timer=50f,damage=2f,kind="root"};
                g.hazards.Add(ambient);
                surge.Invoke(g,new object[0]);
                Check(g.hazards.Count==4&&g.hazards.Contains(ambient),
                    "A real regional three-cue pincer coexists with an unrelated, identically typed hazard");
                hit.Invoke(g,new object[]{raiders[0],99999f,false,null,null});
                Check(g.hazards.Count==4&&g.hazards.Contains(ambient),
                    "The first genuine raider death does not pre-empt a still-active partner's strike");
                hit.Invoke(g,new object[]{raiders[1],99999f,false,null,null});
                Check(g.hazards.Count==1&&g.hazards[0]==ambient&&
                    g.RoadwardenTrialRoom==1&&!g.Has("road_trial_1"),
                    "The second real lethal hit immediately retracts exactly the wave-owned pincer, without deleting ambient danger or granting quest rewards");
                g.hitStop=0f;g.Close();g.Tick(.05f,new Point());
                Check(g.RoadwardenTrialRoom==1&&g.RoadwardenTrialWave==2&&
                    g.hazards.Count==1&&g.hazards[0]==ambient&&
                    g.save.version==2,
                    "The next real reinforcement wave cannot inherit old trial circles, while unrelated world hazard and old saves remain");
                g.Close();
                Action(g,beacon.kind,"road_trial_abandon");
                Check(g.RoadwardenTrialRoom==0&&
                    g.hazards.Contains(ambient)&&
                    !g.Has("road_trial_1"),
                    "Authentic beacon withdrawal continues to preserve non-trial hazards and first-clear rewards");
            }
            finally{data.maps[1]=original;}
        }

        public static void RoadwardenWardPressureAndWarningRearmRespectRealFooting(Campaign data)
        {
            MapData original=data.maps[1];
            var rows=(string[])original.rows.Clone();
            try
            {
                data.maps[1]=new MapData{id=1,spawnX=original.spawnX,
                    spawnY=original.spawnY,rows=rows,
                    objects=original.objects,decor=original.decor};
                var g=Fresh(data);g.save.stage=3;g.save.level=10;
                g.save.flags.Add("dispatch_complete");
                g.save.flags.Add("survey_1");
                g.Enter(1);g.enemies.Clear();g.invulnerable=180f;
                WorldObject station=Object(g,"survey1");
                g.save.x=station.x;g.save.y=station.y;
                Action(g,station.kind,"road_trial_start_1");
                var actors=g.enemies.FindAll(e=>e.id>=81000&&e.id<81100);
                Check(g.RoadwardenTrialRoom==1&&actors.Count==2,
                    "The real forest defense is live with both original trial attackers");
                var ward=typeof(GameCore).GetField("roadTrialWard",
                    System.Reflection.BindingFlags.NonPublic|
                    System.Reflection.BindingFlags.Instance);
                var warning=typeof(GameCore).GetField("roadTrialWardWarning",
                    System.Reflection.BindingFlags.NonPublic|
                    System.Reflection.BindingFlags.Instance);
                var trialTick=typeof(GameCore).GetMethod("TickRoadwardenTrial",
                    System.Reflection.BindingFlags.NonPublic|
                    System.Reflection.BindingFlags.Instance);
                Check(ward!=null&&warning!=null&&trialTick!=null,
                    "Real live ward, warning tier and objective combat transaction are available");
                Enemy raider=actors[0],other=actors[1];
                // The gate closes only around the raider's *source* foot:
                // ClearAttackPath skips its origin and can still see the
                // beacon, so centerline-only ward pressure is a false hit.
                raider.x=(float)Math.Floor(station.x)+1.01f;
                raider.y=station.y;raider.state=0;raider.timer=100f;
                raider.stun=0;raider.hitTime=0;raider.releaseTime=0;
                other.stun=100f;
                ward.SetValue(g,70f);
                Check(g.CanStand(raider.Position),
                    "Initially open legal raid footing can physically pressure the beacon");
                trialTick.Invoke(g,new object[]{.05f});
                Check(g.RoadwardenWardIntegrity<70f&&
                    g.RoadwardenWardSiphonCount==1&&
                    (g.RoadwardenWardSiphonSource(0)-raider.Position).Length<.0001f&&
                    (g.RoadwardenWardSiphonTarget-new Point(station.x,station.y)).Length<.0001f,
                    "Only the actual physically draining raider publishes its current live world-rooted ward siphon");
                ward.SetValue(g,70f);
                int x=(int)Math.Floor(raider.x),y=(int)Math.Floor(raider.y);
                char[] cells=rows[y].ToCharArray();char old=cells[x];
                cells[x]='#';rows[y]=new string(cells);
                Check(!g.CanStand(raider.Position)&&g.CanStand(new Point(station.x,station.y)),
                    "Dynamic stone seals only the attacker's entire foot, never the beacon");
                trialTick.Invoke(g,new object[]{.05f});
                Check(Math.Abs(g.RoadwardenWardIntegrity-70f)<.0001f&&
                    g.RoadwardenWardSiphonCount==0&&
                    !g.RoadwardenWardUnderAttack&&g.RoadwardenTrialRoom==1,
                    "A trapped raider cannot invisibly siphon ward integrity through a newly closed gate");
                cells[x]=old;rows[y]=new string(cells);
                // A real one-per-wave terminal repair should re-arm the
                // 30% critical alert, without replaying the 65% warning
                // while the shield is still below that threshold.
                ward.SetValue(g,28f);warning.SetValue(g,2);
                g.save.energy=100f;g.Close();
                Action(g,station.kind,"road_trial_repair");
                Check(Math.Abs(g.RoadwardenWardIntegrity-53f)<.0001f&&
                    g.save.energy==65f&&(int)warning.GetValue(g)==1,
                    "An actual paid beacon repair re-arms only the newly recovered critical warning tier");
                ward.SetValue(g,30.05f);g.Close();
                trialTick.Invoke(g,new object[]{.05f});
                Check((int)warning.GetValue(g)==2&&
                    g.RoadwardenWardIntegrity<=30f,
                    "The second real critical breach warns again after repairing the beacon");
                // A new real wave can restore the shield over both thresholds
                // and must reset the notification state for later enemies.
                ward.SetValue(g,69f);
                var hit=typeof(GameCore).GetMethod("Hit",
                    System.Reflection.BindingFlags.Instance|
                    System.Reflection.BindingFlags.NonPublic);
                Check(hit!=null,"Genuine wave victory requires the shipped lethal Hit transaction");
                foreach(Enemy attacker in actors)
                    hit.Invoke(g,new object[]{attacker,99999f,false,null,null});
                g.hitStop=0;g.Tick(.05f,new Point());
                Check(g.RoadwardenTrialRoom==1&&g.RoadwardenTrialWave==2&&
                    g.RoadwardenWardIntegrity>=86f&&
                    (int)warning.GetValue(g)==0&&
                    !g.Has("road_trial_1")&&g.save.version==2,
                    "Only real deaths deploy wave two; its ward refill re-arms both warnings without premature reward or save migration");
            }
            finally{data.maps[1]=original;}
        }

        public static void RoadwardenTwoRealDefendersAvoidSealedPocketAndFriendlyActors(Campaign data)
        {
            MapData original=data.maps[1];
            try
            {
                var rows=new string[44];
                for(int y=0;y<44;y++)
                {
                    char[] cells=new string('#',44).ToCharArray();
                    if(y>=2&&y<=41)
                        for(int x=2;x<=41;x++)cells[x]='.';
                    rows[y]=new string(cells);
                }
                // First nominal radius-three candidate has free standing
                // space but no body-foot route out of its solid ring.
                for(int y=16;y<=18;y++)
                {
                    char[] cells=rows[y].ToCharArray();
                    for(int x=16;x<=18;x++)
                        if(x!=17||y!=17)cells[x]='#';
                    rows[y]=new string(cells);
                }
                var survey=new WorldObject{id="survey1",kind="survey1",
                    x=20.5f,y=20.5f,name="Beacon",en="Beacon"};
                data.maps[1]=new MapData{id=1,spawnX=20.5f,
                    spawnY=20.5f,rows=rows,
                    objects=new[]{survey},decor=new Decoration[0]};
                var g=Fresh(data);g.save.stage=3;g.Enter(1);
                g.enemies.Clear();g.save.x=survey.x;g.save.y=survey.y;
                g.save.flags.Add("companion_1");g.SelectCompanion(1);
                g.companionPosition=new Point(17.5f,20.5f);
                var bystander=new Enemy(92700,0,20.5f,17.5f,500f)
                    {stun=100f,timer=100f};
                g.enemies.Add(bystander);
                Point isolated=new Point(17.5f,17.5f);
                var method=typeof(GameCore).GetMethod("TryRoadwardenTrialFeet",
                    System.Reflection.BindingFlags.NonPublic|
                    System.Reflection.BindingFlags.Instance);
                Check(method!=null&&g.CanStand(isolated)&&
                    g.CanStand(new Point(survey.x,survey.y)),
                    "The real beacon planner sees a superficially free isolated land cell and a legal objective");
                object[] args={null};
                Check((bool)method.Invoke(g,args),
                    "Despite sealed terrain and occupied candidate feet the real beacon can still spawn a full pair");
                Point[] feet=(Point[])args[0];
                Check(feet!=null&&feet.Length==2&&
                    (feet[0]-feet[1]).Length>=3.5f,
                    "Both actual attackers reserve distinct eligible physical spawn roots atomically");
                foreach(Point foot in feet)
                    Check(g.CanStand(foot)&&
                        (foot-isolated).Length>1.5f&&
                        (foot-g.Position).Length>=2.7f&&
                        (foot-g.companionPosition).Length>=1.55f&&
                        (foot-bystander.Position).Length>=2f,
                        "No raider can spawn within unreachable rock or on hero, existing enemy or recruit");
                Check(g.enemies.Count==1&&
                    g.RoadwardenTrialRoom==0&&!g.Has("road_trial_1")&&
                    g.save.version==2,
                    "Spawn preflight cannot mint combat actors, complete quest flags or migrate saved data");
            }
            finally{data.maps[1]=original;}
        }

        public static void RoadwardenClearedWaveCanResumeAfterBlockedRealArena(Campaign data)
        {
            MapData original=data.maps[1];
            var rows=(string[])original.rows.Clone();
            try
            {
                data.maps[1]=new MapData{id=1,spawnX=original.spawnX,
                    spawnY=original.spawnY,rows=rows,
                    objects=original.objects,decor=original.decor};
                var g=Fresh(data);g.save.stage=3;g.save.level=10;
                g.save.flags.Add("dispatch_complete");
                g.save.flags.Add("survey_1");
                g.Enter(1);g.enemies.Clear();g.invulnerable=180;
                WorldObject station=Object(g,"survey1");
                g.save.x=station.x;g.save.y=station.y;
                Action(g,station.kind,"road_trial_start_1");
                Check(g.RoadwardenTrialRoom==1&&
                    g.RoadwardenTrialWave==1&&
                    g.enemies.FindAll(e=>e.id>=81000&&e.id<81100).Count==2,
                    "The actual unlocked forest beacon launches two first-wave hostile defenders");
                var hit=typeof(GameCore).GetMethod("Hit",
                    System.Reflection.BindingFlags.Instance|
                    System.Reflection.BindingFlags.NonPublic);
                Check(hit!=null,"Authentic lethal contact tracks both current-wave deaths");
                foreach(Enemy attacker in g.enemies.FindAll(e=>e.id>=81000&&e.id<81100))
                    hit.Invoke(g,new object[]{attacker,99999f,false,null,null});
                g.hitStop=0;g.Close();
                var ward=typeof(GameCore).GetField("roadTrialWard",
                    System.Reflection.BindingFlags.Instance|
                    System.Reflection.BindingFlags.NonPublic);
                Check(ward!=null&&
                    g.enemies.FindAll(e=>e.id>=81000&&e.id<81100).Count==0,
                    "The complete first wave has truly died, without list manipulation");
                ward.SetValue(g,57f);
                int cx=(int)station.x,cy=(int)station.y;
                for(int y=cy-6;y<=cy+6;y++)
                {
                    char[] cells=rows[y].ToCharArray();
                    for(int x=cx-6;x<=cx+6;x++)
                        if(x!=cx||y!=cy)cells[x]='#';
                    rows[y]=new string(cells);
                }
                Check(g.CanStand(g.Position)&&
                    g.CanStand(new Point(station.x,station.y)),
                    "The real beacon and gold-haired hero can still stand as all reinforcement feet close");
                int coins=g.save.coins,shards=g.save.shards;
                g.Tick(.05f,new Point());
                Check(g.RoadwardenTrialRoom==1&&
                    g.RoadwardenTrialWave==1&&
                    Math.Abs(g.RoadwardenWardIntegrity-57f)<.001f&&
                    !g.Has("road_trial_1")&&
                    g.save.coins==coins&&g.save.shards==shards,
                    "An unspawnable second wave cannot erase the genuine two kills, grant ward or fake original rewards");
                g.Tick(.05f,new Point());
                Check(g.RoadwardenTrialRoom==1&&
                    g.RoadwardenTrialWave==1&&
                    Math.Abs(g.RoadwardenWardIntegrity-57f)<.001f,
                    "Repeated blocked deployments never duplicate the +18 ward recovery");
                data.maps[1].rows=(string[])original.rows.Clone();
                g.Tick(.05f,new Point());
                Check(g.RoadwardenTrialWave==2&&
                    g.enemies.FindAll(e=>e.id>=81000&&e.id<81100).Count==2&&
                    Math.Abs(g.RoadwardenWardIntegrity-75f)<.001f,
                    "Opening the authentic physical arena launches two second-wave enemies and grants exactly one recovery");
                foreach(Enemy attacker in g.enemies.FindAll(e=>e.id>=81000&&e.id<81100))
                    hit.Invoke(g,new object[]{attacker,99999f,false,null,null});
                g.hitStop=0;g.Tick(.05f,new Point());
                Check(g.RoadwardenTrialRoom==0&&g.Has("road_trial_1")&&
                    g.save.version==2,
                    "Only four authenticated combat deaths complete the existing beacon defense and original v2 quest contract");
            }
            finally {data.maps[1]=original;}
        }

        public static void BeaconCachedStartAndEnergyRepairRequireOpenPhysicalLane(Campaign data)
        {
            MapData original=data.maps[1];
            try
            {
                var rows=new string[34];
                for(int y=0;y<34;y++)
                {
                    char[] cells=new string('#',34).ToCharArray();
                    if(y>=2&&y<=31)
                        for(int x=2;x<=31;x++)cells[x]='.';
                    rows[y]=new string(cells);
                }
                WorldObject station=new WorldObject{id="survey1",
                    kind="survey1",name="Open beacon",
                    en="Open beacon",x=10.75f,y=10.5f};
                data.maps[1]=new MapData{id=1,spawnX=12.25f,
                    spawnY=10.5f,rows=rows,
                    objects=new[]{station},decor=new Decoration[0]};
                var g=Fresh(data);g.save.stage=3;g.save.level=10;
                g.save.flags.Add("dispatch_complete");
                g.save.flags.Add("survey_1");
                g.Enter(1);g.enemies.Clear();g.invulnerable=200;
                g.save.x=12.25f;g.save.y=10.5f;
                g.Talk(station);
                Check(g.mode=="dialog"&&g.conversation!=null,
                    "Actual roadwarden beacon opens its authentic defense dialogue from reachable nearby ground");
                int start=-1;
                for(int i=0;i<g.conversation.choices.Length;i++)
                    if(g.conversation.choices[i].action=="road_trial_start_1")
                    {start=i;break;}
                Check(start>=0,"Unlocked beacon offers the original physical two-wave challenge");
                char[] wall=rows[10].ToCharArray();
                wall[11]='#';rows[10]=new string(wall);
                Check(g.CanStand(g.Position)&&g.Blocked(11.5f,10.5f),
                    "A solid dynamic gate cuts access to the trial after the initial dialogue opens");
                g.Choose(start);
                Check(g.RoadwardenTrialRoom==0&&
                    g.enemies.FindAll(e=>e.id>=81000&&e.id<81100).Count==0,
                    "A stale trial button cannot conjure armed guards on the wrong side of a closed wall");
                wall[11]='.';rows[10]=new string(wall);
                g.Talk(station);start=-1;
                for(int i=0;i<g.conversation.choices.Length;i++)
                    if(g.conversation.choices[i].action=="road_trial_start_1")
                    {start=i;break;}
                Check(start>=0,"Reopened beacon resumes the exact original defense challenge");
                g.Choose(start);
                Check(g.RoadwardenTrialRoom==1&&
                    g.enemies.FindAll(e=>e.id>=81000&&e.id<81100).Count==2,
                    "Only reachable interaction launches two genuine beacon attackers");
                var ward=typeof(GameCore).GetField("roadTrialWard",
                    System.Reflection.BindingFlags.NonPublic|
                    System.Reflection.BindingFlags.Instance);
                Check(ward!=null,"True active beacon owns original transient ward integrity");
                ward.SetValue(g,50f);g.save.energy=100f;
                g.Close();g.Talk(station);
                int patch=-1;
                for(int i=0;i<g.conversation.choices.Length;i++)
                    if(g.conversation.choices[i].action=="road_trial_repair")
                    {patch=i;break;}
                Check(patch>=0,"The real damaged beacon offers its once-per-wave 35-energy ward repair");
                wall[11]='#';rows[10]=new string(wall);
                g.Choose(patch);
                Check(g.RoadwardenTrialRoom==1&&
                    Math.Abs(g.RoadwardenWardIntegrity-50f)<.001f&&
                    g.save.energy==100f,
                    "Stale beacon repair cannot spend energy or heal a real wall-isolated ward");
                wall[11]='.';rows[10]=new string(wall);
                g.Talk(station);patch=-1;
                for(int i=0;i<g.conversation.choices.Length;i++)
                    if(g.conversation.choices[i].action=="road_trial_repair")
                    {patch=i;break;}
                Check(patch>=0,"Reopened physical beacon offers the same valid repair again");
                g.Choose(patch);
                Check(Math.Abs(g.RoadwardenWardIntegrity-75f)<.001f&&
                    g.save.energy==65f&&g.save.version==2,
                    "Only an actually reachable repair heals exactly 25 and consumes 35 energy without new save fields");
            }
            finally{data.maps[1]=original;}
        }

        public static void RoadwardenHazardCannotTreatSealedPocketAsEscapeRoute(Campaign data)
        {
            MapData original=data.maps[2];
            try
            {
                var rows=new string[26];
                for(int y=0;y<26;y++)
                {
                    char[] cells=new string('#',26).ToCharArray();
                    if(y>=2&&y<=23)
                        for(int x=2;x<=23;x++)cells[x]='.';
                    rows[y]=new string(cells);
                }
                // One nominal 1.6-tile dodge foot is truly standable
                // BEYOND a solid separating row, while the opposite foot
                // is buried. Neither offers a reachable dodge corridor.
                foreach(int y in new[]{9,11})
                {
                    char[] cells=rows[y].ToCharArray();
                    for(int x=2;x<=23;x++)cells[x]='#';
                    rows[y]=new string(cells);
                }
                data.maps[2]=new MapData{id=2,
                    spawnX=10.5f,spawnY=10.7f,rows=rows,
                    objects=new WorldObject[0],
                    decor=new Decoration[0]};
                var g=Fresh(data);g.save.stage=3;g.Enter(2);
                g.enemies.Clear();g.save.x=10.5f;g.save.y=10.7f;
                var room=typeof(GameCore).GetField("roadTrialRoom",
                    System.Reflection.BindingFlags.NonPublic|
                    System.Reflection.BindingFlags.Instance);
                var cycle=typeof(GameCore).GetField("roadTrialSurgeCount",
                    System.Reflection.BindingFlags.NonPublic|
                    System.Reflection.BindingFlags.Instance);
                var surge=typeof(GameCore).GetMethod("SpawnRoadwardenSurge",
                    System.Reflection.BindingFlags.NonPublic|
                    System.Reflection.BindingFlags.Instance);
                Check(room!=null&&cycle!=null&&surge!=null&&
                    g.CanStand(g.Position)&&
                    g.CanStand(new Point(10.5f,12.3f))&&
                    !g.CanStand(new Point(10.5f,9.1f)),
                    "Tide surge has exactly one physically standable candidate beyond a solid disconnected wall, while the other is impassable");
                room.SetValue(g,2);cycle.SetValue(g,0);
                surge.Invoke(g,new object[0]);
                Check(g.hazards.Count==0,
                    "The trial must never draw three real damage circles when its only supposedly safe exits cannot be reached");
                char[] opened=rows[11].ToCharArray();
                opened[10]='.';rows[11]=new string(opened);
                cycle.SetValue(g,0);
                surge.Invoke(g,new object[0]);
                Check(g.hazards.Count==3&&
                    g.hazards.TrueForAll(h=>h.kind=="tide")&&
                    g.save.version==2,
                    "Opening an actual walkable dodge route admits the full original three-cue river barrage without save changes");
            }
            finally{data.maps[2]=original;}
        }

        public static void BeaconWithdrawalCannotEraseFoesRemotelyAndAllResetPathsCleanOwnActors(Campaign data)
        {
            MapData original=data.maps[1];
            try
            {
                var rows=new string[34];
                for(int y=0;y<34;y++)
                {
                    char[] cells=new string('#',34).ToCharArray();
                    if(y>=2&&y<=31)
                        for(int x=2;x<=31;x++)cells[x]='.';
                    rows[y]=new string(cells);
                }
                WorldObject beacon=new WorldObject{id="survey1",kind="survey1",
                    name="Beacon",en="Beacon",x=10.75f,y=10.5f};
                data.maps[1]=new MapData{id=1,spawnX=12.25f,
                    spawnY=10.5f,rows=rows,
                    objects=new[]{beacon},decor=new Decoration[0]};
                var g=Fresh(data);g.save.stage=3;
                g.save.flags.Add("dispatch_complete");
                g.save.flags.Add("survey_1");
                g.Enter(1);g.enemies.Clear();
                g.invulnerable=180;g.save.x=12.25f;g.save.y=10.5f;
                Action(g,beacon.kind,"road_trial_start_1");
                Check(g.RoadwardenTrialRoom==1&&
                    g.enemies.FindAll(e=>e.id>=81000&&e.id<81100).Count==2,
                    "The first real forest beacon wave is active before withdrawal");
                var ambient=new Enemy(93075,0,19.5f,10.5f,100f)
                    {stun=180,timer=180};
                g.enemies.Add(ambient);
                var shot=new Projectile{
                    position=new Point(10.5f,10.5f),
                    velocity=new Point(1,0),damage=5,
                    roadTrialOwned=true};
                var unrelatedShot=new Projectile{
                    position=new Point(18.5f,10.5f),
                    velocity=new Point(1,0),damage=5};
                g.projectiles.Add(shot);g.projectiles.Add(unrelatedShot);
                var wardWarning=new Effect("danger",
                    new Point(10.5f,10.5f),.6f)
                    {roadTrialOwned=true};
                var ambientEffect=new Effect("spark",
                    new Point(18.5f,10.5f),.6f);
                g.effects.Add(wardWarning);g.effects.Add(ambientEffect);
                g.Talk(beacon);
                int choice=-1;
                for(int i=0;i<g.conversation.choices.Length;i++)
                    if(g.conversation.choices[i].action=="road_trial_abandon")
                    {choice=i;break;}
                Check(choice>=0,"The active beacon exposes the original withdraw choice");
                char[] wall=rows[10].ToCharArray();
                wall[11]='#';rows[10]=new string(wall);
                Check(g.CanStand(g.Position)&&g.Blocked(11.5f,10.5f),
                    "A solid wall cuts the physical signal terminal off after dialogue creation");
                g.Choose(choice);
                Check(g.RoadwardenTrialRoom==1&&
                    g.enemies.FindAll(e=>e.id>=81000&&e.id<81100).Count==2&&
                    g.projectiles.Contains(shot)&&g.effects.Contains(wardWarning),
                    "A cached withdraw command cannot erase real fighters, arrows or warnings across cover");
                wall[11]='.';rows[10]=new string(wall);
                g.Talk(beacon);choice=-1;
                for(int i=0;i<g.conversation.choices.Length;i++)
                    if(g.conversation.choices[i].action=="road_trial_abandon")
                    {choice=i;break;}
                Check(choice>=0,"Reopened terminal still supports intentional retreat");
                g.Choose(choice);
                Check(g.RoadwardenTrialRoom==0&&
                    g.enemies.Count==1&&g.enemies.Contains(ambient)&&
                    !g.projectiles.Contains(shot)&&
                    g.projectiles.Contains(unrelatedShot)&&
                    !g.effects.Contains(wardWarning)&&
                    g.effects.Contains(ambientEffect)&&
                    !g.Has("road_trial_1"),
                    "Real withdrawal removes ONLY owned actors, missiles and cues, preserving unrelated threats and no reward");
                g.enemies.Remove(ambient);
                g.projectiles.Clear();g.effects.Clear();
                g.Talk(beacon);
                Action(g,beacon.kind,"road_trial_start_1");
                Check(g.RoadwardenTrialRoom==1&&
                    g.enemies.FindAll(e=>e.id>=81000&&e.id<81100).Count==2,
                    "A fully cleaned up real beacon can be started again");
                var reset=typeof(GameCore).GetMethod("ResetRoadwardenTrial",
                    System.Reflection.BindingFlags.Instance|
                    System.Reflection.BindingFlags.NonPublic);
                Check(reset!=null,"All original abnormal termination paths use the shared reset");
                reset.Invoke(g,new object[0]);
                Check(g.RoadwardenTrialRoom==0&&g.enemies.Count==0&&
                    !g.Has("road_trial_1")&&g.save.version==2,
                    "A non-dialogue abnormal reset cannot leave ghost trial attackers in a live room");
            }
            finally{data.maps[1]=original;}
        }

        public static void RoadwardenCipherRewardCannotBeCollectedThroughClosingWall(Campaign data)
        {
            MapData original=data.maps[1];
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
                WorldObject letter=new WorldObject{id="letter6",
                    kind="history6",name="Old roadwarden letter",
                    en="Old roadwarden letter",x=10.5f,y=10.5f};
                data.maps[1]=new MapData{id=1,spawnX=12.23f,
                    spawnY=10.5f,rows=rows,
                    objects=new[]{letter},decor=new Decoration[0]};
                var g=Fresh(data);g.save.stage=3;g.Enter(1);
                // Stand 1.73 units from the letter: 1.75 is exclusive, while
                // the player's 0.22-unit collision footprint stays clear of x=11.
                g.enemies.Clear();g.save.x=12.23f;g.save.y=10.5f;
                g.save.flags.Add("survey_complete");
                // The other two actual dispatch letters were already
                // decoded in the persisted world: this final real letter
                // is allowed to unlock the whole roadwarden oath.
                g.save.flags.Add("dispatch_7");
                g.save.flags.Add("dispatch_8");
                g.Talk(letter);
                Check(g.mode=="dialog"&&g.Has("history_6")&&
                    g.conversation!=null,
                    "Reading the original accessible letter records its lore and shows the genuine cipher path");
                int open=-1;
                for(int i=0;i<g.conversation.choices.Length;i++)
                    if(g.conversation.choices[i].action=="dispatch_open_6")
                    {open=i;break;}
                Check(open>=0,"The actual history document offers the original roadwarden cipher");
                g.Choose(open);
                int answer=-1;
                for(int i=0;i<g.conversation.choices.Length;i++)
                    if(g.conversation.choices[i].action=="dispatch_answer_6_1")
                    {answer=i;break;}
                Check(answer>=0,"A genuine, correct decoded answer is available after the initial puzzle");
                int coin=g.save.coins,shard=g.save.shards,talent=g.save.skillPoints;
                char[] barrier=rows[10].ToCharArray();
                barrier[11]='#';rows[10]=new string(barrier);
                Check(g.CanStand(g.Position)&&
                    g.CanStand(new Point(letter.x,letter.y))&&
                    g.Blocked(11.5f,10.5f),
                    "An actual closing wall separates a still-standable history object from the player");
                g.Choose(answer);
                Check(!g.Has("dispatch_6")&&!g.Has("dispatch_complete")&&
                    g.save.coins==coin&&g.save.shards==shard&&
                    g.save.skillPoints==talent,
                    "A cached correct choice across solid cover cannot mint a real letter, shared coins or an irreversible talent");
                barrier[11]='.';rows[10]=new string(barrier);
                g.Talk(letter);
                open=-1;
                for(int i=0;i<g.conversation.choices.Length;i++)
                    if(g.conversation.choices[i].action=="dispatch_open_6")
                    {open=i;break;}
                Check(open>=0,"Reopening the same path restores the original decipher interaction");
                g.Choose(open);answer=-1;
                for(int i=0;i<g.conversation.choices.Length;i++)
                    if(g.conversation.choices[i].action=="dispatch_answer_6_1")
                    {answer=i;break;}
                Check(answer>=0,"The same authentic correct letter answer still works");
                g.Choose(answer);
                Check(g.Has("dispatch_6")&&g.Has("dispatch_complete")&&
                    g.save.coins==coin+60&&g.save.shards==shard+1&&
                    g.save.skillPoints==talent+1&&g.save.version==2,
                    "Only reachable cipher completion can award the exact original letter and all-three oath bonus once");
            }
            finally{data.maps[1]=original;}
        }

        public static void EightRegionOutpostRetiresOrphanRaidersAndOwnArrowsWithoutFakeRescue(Campaign data)
        {
            MapData original=data.maps[0];
            try
            {
                var rows=new string[34];
                for(int y=0;y<34;y++)
                {
                    char[] cells=new string('#',34).ToCharArray();
                    if(y>=2&&y<=31)
                        for(int x=2;x<=31;x++)cells[x]='.';
                    rows[y]=new string(cells);
                }
                var outpost=new WorldObject{id="outer_watch_0",
                    kind="outerwatch",name="Relief outpost",
                    en="Relief outpost",x=10.5f,y=10.5f};
                data.maps[0]=new MapData{id=0,spawnX=12.25f,
                    spawnY=10.5f,rows=rows,
                    objects=new[]{outpost},decor=new Decoration[0]};
                var g=Fresh(data);g.save.stage=3;g.Enter(0);
                g.enemies.Clear();
                g.save.flags.Add("outer_cache_0");
                // Stay strictly inside the same 1.75-unit radius as Nearest();
                // a position exactly 1.75 away is intentionally not interactable.
                g.save.x=12.0f;g.save.y=10.5f;
                g.Talk(outpost);
                Check(g.mode=="dialog"&&g.conversation!=null,
                    "The original recovered relief box unlocks an authentic optional outpost defense");
                int start=-1;
                for(int i=0;i<g.conversation.choices.Length;i++)
                    if(g.conversation.choices[i].action=="outer_watch_start")
                    {start=i;break;}
                Check(start>=0,"The actual outpost offers its real two-attacker challenge");
                char[] wall=rows[10].ToCharArray();
                wall[11]='#';rows[10]=new string(wall);
                g.Choose(start);
                Check(!g.OuterPatrolActive&&g.enemies.Count==0,
                    "A stale outpost start cannot spawn hostile creatures through a newly closed physical wall");
                wall[11]='.';rows[10]=new string(wall);
                g.Talk(outpost);
                start=-1;
                for(int i=0;i<g.conversation.choices.Length;i++)
                    if(g.conversation.choices[i].action=="outer_watch_start")
                    {start=i;break;}
                Check(start>=0,"Opening the same wall restores legitimate outpost combat access");
                g.Choose(start);
                var raiders=g.enemies.FindAll(e=>e.hp>0);
                Check(g.OuterPatrolActive&&raiders.Count==2&&
                    (raiders[0].Position-raiders[1].Position).Length>=2.7f,
                    "A complete pair of physically reachable combat raiders is spawned, not a partial enemy list");
                foreach(Enemy foe in raiders)
                    Check(g.CanStand(foe.Position)&&
                        (foe.Position-g.Position).Length>=1.55f,
                        "Neither real raider materializes inside blocking ground or on the hero");
                var ambient=new Enemy(93110,0,20.5f,18.5f,500f)
                    {stun=200,timer=200};
                g.enemies.Add(ambient);
                var ownedShot=new Projectile{position=new Point(10.5f,10.5f),
                    velocity=new Point(1,0),damage=7,outerPatrolOwned=true};
                var unrelatedShot=new Projectile{position=new Point(20.5f,18.5f),
                    velocity=new Point(1,0),damage=7};
                g.projectiles.Add(ownedShot);g.projectiles.Add(unrelatedShot);
                var warning=new Effect("danger",raiders[0].Position,.75f)
                    {outerPatrolOwned=true};
                var unrelated=new Effect("spark",ambient.Position,.75f);
                g.effects.Add(warning);g.effects.Add(unrelated);
                int coins=g.save.coins,shards=g.save.shards;
                // Disappearing from the live list without an actual lethal
                // Hit callback must abort, NOT complete or leave ghosts.
                g.enemies.Remove(raiders[1]);g.Close();
                g.Tick(.05f,new Point());
                Check(!g.OuterPatrolActive&&
                    !g.enemies.Contains(raiders[0])&&
                    g.enemies.Contains(ambient)&&
                    !g.projectiles.Contains(ownedShot)&&
                    g.projectiles.Contains(unrelatedShot)&&
                    !g.effects.Contains(warning)&&
                    g.effects.Contains(unrelated)&&
                    !g.OuterPatrolDone(0)&&
                    g.save.coins==coins&&g.save.shards==shards,
                    "An abnormal missing raider removes ONLY this outpost's living foes, shots and warnings with no false victory");
                g.projectiles.Clear();g.effects.Clear();
                g.enemies.Remove(ambient);
                g.Talk(outpost);start=-1;
                for(int i=0;i<g.conversation.choices.Length;i++)
                    if(g.conversation.choices[i].action=="outer_watch_start")
                    {start=i;break;}
                Check(start>=0,"After a genuine abort the recovered outpost can start a fresh real defense");
                g.Choose(start);
                raiders=g.enemies.FindAll(e=>e.hp>0);
                Check(g.OuterPatrolActive&&raiders.Count==2,
                    "Retry deploys two new original combat opponents without reusing old identities");
                var hit=typeof(GameCore).GetMethod("Hit",
                    System.Reflection.BindingFlags.Instance|
                    System.Reflection.BindingFlags.NonPublic);
                Check(hit!=null,"The original contact pathway is available for real registered combat deaths");
                foreach(Enemy foe in raiders)
                    hit.Invoke(g,new object[]{foe,99999f,false,null,null});
                g.hitStop=0;g.Close();g.Tick(.05f,new Point());
                Check(g.OuterPatrolDone(0)&&!g.OuterPatrolActive&&
                    g.save.coins>=coins+14&&g.save.shards>=shards+1&&
                    g.save.version==2,
                    "Exactly two authentic lethal hits settle a retry and preserve one legitimate persistent relief clear");
            }
            finally{data.maps[0]=original;}
        }

        public static void EveryAuthoredOutpostRetainsTwoAccessibleRegionalRaiders(Campaign data)
        {
            // Exercise all eight real outdoor ring maps, not just a
            // simplified synthetic arena. Companion occupancy must not
            // make the physical two-attacker rescue impossible.
            var method=typeof(GameCore).GetMethod("TryOuterPatrolFeet",
                System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.NonPublic);
            Check(method!=null,"One production preflight owns all eight authentic outpost formations");
            var g=Fresh(data);
            g.save.stage=8;
            g.save.flags.Add("companion_1");
            g.SelectCompanion(1);
            for(int room=0;room<8;room++)
            {
                g.save.room=room;
                g.enemies.Clear();
                WorldObject post=Object(g,"outerwatch");
                Check(post!=null&&post.kind=="outerwatch"&&
                    g.CanStand(new Point(post.x,post.y)),
                    "Every authored outdoor relief post has its genuine accessible center");
                g.save.x=post.x;g.save.y=post.y;
                g.companionPosition=new Point(post.x-2f,post.y);
                object[] args={post,null};
                Check((bool)method.Invoke(g,args),
                    "Every authentic ring-region relief site retains a complete pair of physically reachable combat feet");
                Point[] feet=(Point[])args[1];
                Check(feet!=null&&feet.Length==2&&
                    (feet[0]-feet[1]).Length>=2.7f,
                    "Both real region attackers must have independently reserved actor roots");
                foreach(Point foot in feet)
                    Check(g.CanStand(foot)&&
                        (foot-g.Position).Length>=1.55f&&
                        (foot-g.companionPosition).Length>=1.55f,
                        "Authored relief attacks never materialize on blocked terrain, the canonical hero or their recruit");
            }
            Check(!g.Has("outer_patrol_all")&&g.save.version==2,
                "Eight-region preflight alone neither mints a fake rescue nor alters the legacy save schema");
        }

        public static void FullInventoryAndWalletRemainStableThroughActualEnemyLoot(Campaign data)
        {
            var g = Fresh(data); g.Enter(1); g.enemies.Clear();
            g.save.x=12;g.save.y=14;g.save.reducedMotion=true;
            g.save.coins=999999;g.save.inventory[2]=9999;g.save.kills[1]=99999;
            int xp=g.save.xp;
            var raider=new Enemy(765,0,13,14,1) { stun=99 };
            g.enemies.Add(raider);
            g.Attack(new Point(1,0));
            Advance(g,.45f);
            Check(!g.enemies.Contains(raider) && g.save.xp>xp &&
                g.effects.Exists(e=>e.kind=="loot"),
                "A full bag must not suppress a real enemy defeat, XP or contact");
            Check(g.save.coins==999999 && g.save.inventory[2]==9999 &&
                g.save.kills[1]==99999,
                "Actual combat rewards cannot exceed the on-disk coin, item and kill caps");
            g.PrepareSave();
            var loaded=new GameCore(data,CloneSave(g.save));
            Check(loaded.save.coins==999999 && loaded.save.inventory[2]==9999 &&
                loaded.save.kills[1]==99999,
                "A full reward state roundtrips without hidden reload-only loss");
            // Partial capacity still awards the usual loot exactly once.
            g=Fresh(data);g.Enter(1);g.enemies.Clear();
            g.save.x=12;g.save.y=14;g.save.reducedMotion=true;
            g.save.coins=999998;g.save.inventory[2]=9998;g.save.kills[1]=99998;
            g.enemies.Add(new Enemy(766,0,13,14,1){stun=99});
            g.Attack(new Point(1,0));Advance(g,.45f);
            Check(g.save.coins==999999 && g.save.inventory[2]==9999 &&
                g.save.kills[1]==99999,
                "Last available resource slots are filled, never skipped or overrun");
        }

        public static void FullBagNeverConsumesGatherNodesOrReadyCrops(Campaign data)
        {
            var g=Fresh(data);g.Enter(1);g.enemies.Clear();
            var plant=new WorldObject { id="qa-herb-stack", kind="herb", x=12, y=14, name="Moonherb", en="Moonherb" };
            g.save.x=plant.x;g.save.y=plant.y;
            g.save.inventory[2]=9999;
            g.Talk(plant);
            Check(!g.Has("node_"+plant.id) && g.save.inventory[2]==9999,
                "A full herb stack leaves the physical gathering node available");
            g.save.inventory[2]=9996;
            g.Talk(plant);
            Check(g.Has("node_"+plant.id) && g.save.inventory[2]==9999,
                "Freed capacity allows the same real node to deliver its three herbs");
            var ore=new WorldObject { id="qa-ore-stack", kind="ore", x=12, y=14, name="Ore", en="Ore" };
            g.save.inventory[1]=9996;g.save.inventory[4]=9999;
            g.save.bounty=1;g.save.bountyProgress=0;
            g.Talk(ore);
            Check(!g.Has("node_"+ore.id) && g.save.inventory[1]==9996 &&
                g.save.bountyProgress==0,
                "Multi-resource ore node cannot spend itself or bounty credit on partial capacity");
            g.save.inventory[4]=9996;g.Talk(ore);
            Check(g.Has("node_"+ore.id) && g.save.inventory[1]==9999 &&
                g.save.inventory[4]==9999 && g.save.bountyProgress==1,
                "Full ore and parts reward and actual bounty credit arrive together");
            g=Fresh(data);g.save.plantedAt=0;g.save.totalTime=61;
            g.save.inventory[2]=9999;g.save.inventory[7]=9997;
            int coins=g.save.coins;
            Action(g,"garden","harvest");
            Check(g.save.plantedAt==0 && g.save.coins==coins &&
                g.save.inventory[7]==9997,
                "Ready garden cannot consume a harvest if Moonherbs are full");
            g.save.inventory[2]=9994;g.save.inventory[7]=9999;
            Action(g,"garden","harvest");
            Check(g.save.plantedAt==0 && g.save.coins==coins &&
                g.save.inventory[2]==9994,
                "Ready garden remains available if only the seed stack is full");
            g.save.inventory[7]=9997;
            Action(g,"garden","harvest");
            Check(g.save.plantedAt==-1 && g.save.inventory[2]==9999 &&
                g.save.inventory[7]==9999 && g.save.coins==coins+8,
                "A mature saved crop pays its complete reward exactly once after freeing space");
        }

        public static void FullFishStackStopsWastedCastWithoutBreakingCatch(Campaign data)
        {
            var g=Fresh(data);
            g.save.inventory[3]=9999;g.save.bounty=2;g.save.bountyProgress=0;
            Action(g,"fish","fish");
            Check(g.mode=="world" && g.save.bountyProgress==0,
                "Full fish stack refuses minigame before any wasted cast");
            g.save.inventory[3]=9998;g.save.coins=999999;
            Action(g,"fish","fish");
            Check(g.mode=="fish","A freed stack can resume normal fishing");
            for (int i=0;i<3;i++)
            {
                ReachFishingBite(g,true);
                g.FishHit();
            }
            Check(g.mode=="world" && g.save.inventory[3]==9999 &&
                g.save.coins==999999 && g.save.bountyProgress==1,
                "Normal three-hit catch awards one bounded fish and real bounty progress");
        }

        public static void BoundedEnemyOpeningDelaysAndRestoredPatrols(Campaign data)
        {
            // The original early patrol rhythm must remain unchanged.
            for (int id = 1; id <= 8; id++)
            {
                Enemy early = new Enemy(id, id % 3, 14, 14, 40);
                Check(Math.Abs(early.timer - (1.3f + id * .13f)) < .0001f,
                    "Original early patrol opening cadence remains unchanged");
            }
            // IDs of snapshots and authored encounters are identifiers, not seconds.
            foreach (int id in new[] { 9, 450, 1905, 81012, 82032, 83001, 99999 })
            {
                Enemy candidate = new Enemy(id, 0, 14, 14, 50);
                Check(candidate.timer >= 1.42f && candidate.timer <= 2.35f,
                    "Large ID " + id + " must not delay its first attack for minutes or hours");
            }
            var g = Fresh(data); g.save.stage = 3; g.Enter(3);
            g.enemies.Clear(); g.PrepareSave();
            g.save.snapshotRoom = 3;
            g.save.worldEnemies = new[] {
                new EnemyRecord { id = 50000, kind = 1, x = 9.5f, y = 14.5f,
                    hp = 44, maxHp = 44, boss = false }
            };
            var loaded = new GameCore(data, CloneSave(g.save)); loaded.Start();
            Enemy patrol = loaded.enemies.Find(e => e.id == 50000);
            Check(patrol != null && patrol.timer >= 1.42f && patrol.timer <= 2.35f,
                "Loading a high-ID patrol restores a playable opening attack delay");
        }

        public static void EliteVictoryRetiresWarningsBeforeNextDamageStep(Campaign data)
        {
            var g = Fresh(data); g.save.stage = 3; g.save.level = 12;
            g.save.flags.Add("archive_complete");
            g.Enter(3); g.enemies.Clear();
            WorldObject altar = Object(g, "archiveecho");
            g.save.x = altar.x; g.save.y = altar.y;
            Action(g, altar.kind, "archive_echo_start");
            Enemy echo = g.enemies.Find(e => e.id == 83001);
            Check(echo != null, "Atlas echo is actually spawned by the story altar");
            var echoWarning = new Hazard { position = g.Position, radius = 1.2f,
                damage = 999, timer = .01f, kind = "bell", atlasEchoOwned = true };
            var unrelated = new Hazard { position = new Point(4, 4), radius = .2f,
                damage = 1, timer = 10, kind = "root" };
            g.hazards.Add(echoWarning); g.hazards.Add(unrelated);
            var hit = typeof(GameCore).GetMethod("Hit",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            hit.Invoke(g, new object[] { echo, 9999f, false, null, null });
            Check(!g.enemies.Contains(echo) &&
                !g.hazards.Exists(h => h.atlasEchoOwned) &&
                g.hazards.Contains(unrelated) &&
                g.AtlasEchoOpenTime == 0,
                "The final actual combat hit retires imminent echo hazards immediately, without erasing world hazards");
            g.save.reducedMotion = true;
            for (int frame = 0; frame < 6; frame++) g.Tick(.05f, new Point());
            Check(g.Has("archive_echo_complete"),
                "The same-frame safety cleanup still allows a proper one-time echo clear");
        }

        public static void OptionalEncountersNeverStartInsideEachOther(Campaign data)
        {
            var g = Fresh(data); g.save.stage = 3; g.save.level = 12;
            foreach (string flag in new[] { "archive_complete", "scout_saved",
                "dispatch_complete", "survey_3" }) g.save.flags.Add(flag);
            g.Enter(3); g.enemies.Clear();
            WorldObject altar = Object(g, "archiveecho");
            g.save.x = altar.x; g.save.y = altar.y;
            Action(g, altar.kind, "archive_echo_start");
            Enemy echo = g.enemies.Find(e => e.id == 83001);
            Check(g.AtlasEchoRoom == 3 && echo != null, "Elite challenge begins normally");
            // Move the echo away so that SafeTravel alone would allow another
            // encounter. The explicit mutex must still deny it.
            echo.x = 5; echo.y = 5;
            WorldObject beacon = Object(g, "survey3");
            g.save.x = beacon.x; g.save.y = beacon.y;
            Action(g, beacon.kind, "road_trial_start_3");
            Check(g.RoadwardenTrialRoom == 0 && g.AtlasEchoRoom == 3 &&
                !g.enemies.Exists(e => e.id >= 81000 && e.id < 81100),
                "Beacon defense cannot start while the atlas echo is active");
            WorldObject traveler = Object(g, "wayfarer3");
            g.save.x = traveler.x; g.save.y = traveler.y;
            Action(g, traveler.kind, "wayfarer_defend_3");
            Check(g.WayfarerEncounterRoom == 0 && g.AtlasEchoRoom == 3 &&
                !g.enemies.Exists(e => e.id >= 82000 && e.id < 82100),
                "Roadside ambush cannot start during an atlas echo challenge");
        }
        public static void FullStackStoryRewardsPreserveQuestProgress(Campaign data)
        {
            var g = Fresh(data);
            g.save.coins = 999995;
            g.save.inventory[0] = g.save.inventory[1] = 9998;
            g.save.inventory[6] = 9999;
            g.save.flags.Add("core_1");
            Action(g, "guild", "deliver");
            Check(g.save.stage == 1 && g.save.coins == 999999 &&
                g.save.inventory[0] == 9999 && g.save.inventory[1] == 9999 &&
                g.save.inventory[6] == 9999,
                "Main story delivery progresses while each resource stays within its saved cap");
            g.PrepareSave();
            var restored = new GameCore(data, CloneSave(g.save));
            Check(restored.save.stage == 1 && restored.save.coins == 999999 &&
                restored.save.inventory[0] == 9999 && restored.save.inventory[6] == 9999,
                "New chapter and legal inventory persist exactly across the v2 save roundtrip");

            g = Fresh(data);
            g.save.inventory[2] = 6; g.save.inventory[6] = 9999;
            g.save.coins = 999999; g.save.skillPoints = 99;
            Action(g, "garden", "ally1");
            Check(g.Has("ally_1") && g.save.inventory[2] == 0 &&
                g.save.inventory[6] == 9999 && g.save.coins == 999999 &&
                g.save.skillPoints == 99,
                "Commission consumes its materials, marks the ally and cannot overflow stew, cash or skill points");
            g = Fresh(data); g.Enter(1);
            g.save.coins = 999999; g.save.inventory[1] = 9999;
            TalkNear(g, "lore");
            Check(!g.Has("treasure_1") && g.Has("lore_1") &&
                g.save.coins == 999999 && g.save.inventory[1] == 9999,
                "First lore discovery persists narrative history without consuming a blocked physical relic reward");
            g.Close(); g.save.kills[1] = 4; g.save.coins = 999999;
            Action(g, "scout", "scout");
            Check(g.Has("scout_saved") && g.save.coins == 999999,
                "Scout rescue unlocks the road without overfilling the saved wallet");
        }

        public static void FullWalletBountyAndAbyssBankingDoNotOverflow(Campaign data)
        {
            var g = Fresh(data);
            g.AcceptBounty(0);
            g.save.bountyProgress = GameCore.BountyTargets[0];
            g.save.coins = 999999; g.save.shards = 99999;
            int beforeXp = g.save.xp;
            Check(g.ClaimBounty() && g.save.bounty == -1 &&
                g.save.coins == 999999 && g.save.shards == 99999 &&
                g.save.xp > beforeXp,
                "Completed bounty pays experience and clears its contract at full monetary caps");
            var abyss = Fresh(data);
            abyss.save.frontierEnding = 1;
            abyss.StartAbyss(0);
            Check(abyss.InAbyss, "Legal covenant actually starts a real procedural abyss run");
            abyss.enemies.Clear();
            abyss.save.flags.Add("abyss_clear_1");
            abyss.save.coins = 999998;
            abyss.save.shards = 99998;
            abyss.save.runCoins = int.MaxValue;
            abyss.save.runShards = int.MaxValue;
            abyss.TakeBoon(0);
            Check(abyss.save.abyssFloor == 2 && abyss.save.coins == 999999 &&
                abyss.save.shards == 99999 && abyss.save.runCoins == 0 &&
                abyss.save.runShards == 0 && abyss.save.boons[0] == 1,
                "Real Abyss floor banking saturates oversized unbanked wealth and still grants the next floor boon");
        }

        public static void StaleAbyssBankChoiceNeverDropsPendingFloorLoot(Campaign data)
        {
            var g = Fresh(data);
            g.save.frontierEnding = 1;
            g.StartAbyss(0);
            Check(g.InAbyss, "Authorized covenant must start a real floor before settlement testing");
            g.enemies.Clear();
            g.save.runCoins = 43; g.save.runShards = 2; g.save.runXp = 0;
            int originalCoins = g.save.coins, originalShards = g.save.shards;
            TalkNear(g, "abyssGate");
            Check(g.mode == "dialog" && g.Has("abyss_clear_1"),
                "The actual gate offers banking only after a cleared floor");
            int bank = -1;
            for (int i = 0; i < g.conversation.choices.Length; i++)
                if (g.conversation.choices[i].action == "abyssBank") bank = i;
            Check(bank >= 0, "Live Abyss gate offers the expected bank choice");
            // Simulate a hostile spawned between opening the dialogue and
            // selecting its stale bank command. BankFloor previously returned,
            // but Choose still ended the expedition and erased its resources.
            var lateHostile = new Enemy(49001, 0, 17, 14, 44);
            g.enemies.Add(lateHostile);
            g.Choose(bank);
            Check(g.InAbyss && g.save.abyssFloor == 1 &&
                g.save.runCoins == 43 && g.save.runShards == 2 &&
                g.save.coins == originalCoins && g.save.shards == originalShards,
                "Stale bank menu must not close a live floor or erase pending coins and shards");

            g.enemies.Clear();
            TalkNear(g, "abyssGate");
            bank = -1;
            for (int i = 0; i < g.conversation.choices.Length; i++)
                if (g.conversation.choices[i].action == "abyssBank") bank = i;
            Check(bank >= 0, "A safe cleared gate can be reopened normally");
            g.save.flags.Remove("abyss_clear_1");
            g.Choose(bank);
            Check(g.InAbyss && g.save.runCoins == 43 && g.save.runShards == 2,
                "A stale floor-clear flag cannot end an unbanked expedition either");

            // Completing the floor again legitimately banks exactly once.
            Action(g, "abyssGate", "abyssBank");
            Check(!g.InAbyss && g.save.room == 0 &&
                g.save.coins == originalCoins + 43 &&
                g.save.shards == originalShards + 2 &&
                g.save.runCoins == 0 && g.save.runShards == 0,
                "A renewed genuine root-gate decision still banks and extracts the entire reward");
        }

        public static void AbyssPendingRewardsStayWithinSaveCapsUntilCollected(Campaign data)
        {
            var g = Fresh(data);
            g.save.frontierEnding = 1;
            g.StartAbyss(0);
            Check(g.InAbyss && g.enemies.Count > 0,
                "Procedural Abyss creates live opponents for the pending-reward regression");
            g.save.runCoins = g.save.runShards = g.save.runXp = 9998;
            Enemy foe = g.enemies[0];
            var hit = typeof(GameCore).GetMethod("Hit",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            hit.Invoke(g, new object[] { foe, 9999f, false, null, null });
            Check(!g.enemies.Contains(foe) &&
                g.save.runCoins == 9999 && g.save.runShards == 9999 &&
                g.save.runXp == 9999,
                "Actual Abyss enemy death never overflows any unbanked v2 resource");

            g.save.x = 47.5f; g.save.y = 28.5f;
            g.Talk(Object(g, "abyssCache"));
            Check(g.Has("abyss_annex_cache_1") &&
                g.save.runCoins == 9999 && g.save.runShards == 9999,
                "Actual side-chamber loot stays at cap while its discovery is still recorded");
            g.Close();
            g.save.x = 12.5f; g.save.y = 28.5f;
            g.Talk(Object(g, "abyssSurvey"));
            Check(g.Has("abyss_annex_survey_1") && g.save.runXp == 9999,
                "Floor's survey journal cannot overflow pending experience");
            g.Close();
            g.PrepareSave();
            var restored = new GameCore(data, CloneSave(g.save));
            Check(restored.InAbyss && restored.save.runCoins == 9999 &&
                restored.save.runShards == 9999 && restored.save.runXp == 9999 &&
                restored.Has("abyss_annex_cache_1"),
                "Legitimate capped unbanked resources and explored cache persist across actual v2 reload");
            restored.enemies.Clear();
            restored.save.coins = 999999; restored.save.shards = 99999;
            Action(restored, "abyssGate", "abyssBank");
            Check(!restored.InAbyss && restored.save.room == 0 &&
                restored.save.coins == 999999 && restored.save.shards == 99999 &&
                restored.save.runCoins == 0 && restored.save.runShards == 0 &&
                restored.save.runXp == 0,
                "Reloaded capped rewards settle once without bank overflow or a stranded Abyss run");
        }

        public static void OneTimeStorySuppliesWaitForActualStorageCapacity(Campaign data)
        {
            var g = Fresh(data);
            g.save.flags.Add("scout_saved");
            g.save.inventory[6] = 9999;
            int history = g.save.moments.Count;
            Action(g, "returnscout", "scoutgift");
            Check(!g.Has("scout_gift") && g.save.inventory[6] == 9999 &&
                g.save.moments.Count == history,
                "Full stew stack must not burn scout's one-time gift or fabricate a gift journal entry");
            g.save.inventory[6] = 9998;
            Action(g, "returnscout", "scoutgift");
            Check(g.Has("scout_gift") && g.save.inventory[6] == 9999 &&
                g.save.moments.Count == history + 1,
                "Returning after using stew grants the real gift once");
            g.PrepareSave();
            var loaded = new GameCore(data, CloneSave(g.save));
            loaded.save.inventory[6] = 9998;
            Action(loaded, "returnscout", "scoutgift");
            Check(loaded.save.inventory[6] == 9998 && loaded.Has("scout_gift"),
                "Previously received scout gift stays claimed after a v2 reload");

            g = Fresh(data);
            g.Enter(1);
            g.save.coins = 999999; g.save.inventory[1] = 9999;
            TalkNear(g, "lore");
            Check(g.Has("lore_1") && !g.Has("treasure_1") &&
                g.save.coins == 999999 && g.save.inventory[1] == 9999 &&
                g.conversation.textEn.Contains("remain"),
                "Full wallet and crystals must preserve the region's lore and leave its first loot recoverable");
            g.Close();
            g.save.coins = 999979; g.save.inventory[1] = 9998;
            TalkNear(g, "lore");
            Check(!g.Has("treasure_1") && g.save.coins == 999979 &&
                g.save.inventory[1] == 9998,
                "Partial crystal capacity may not consume an atomic two-crystal relic prize");
            g.Close();
            g.save.inventory[1] = 9997;
            TalkNear(g, "lore");
            Check(g.Has("treasure_1") && g.save.coins == 999999 &&
                g.save.inventory[1] == 9999,
                "Making exactly enough room awards all first relic resources together");
            g.Close(); TalkNear(g, "lore");
            Check(g.save.coins == 999999 && g.save.inventory[1] == 9999,
                "Repeated lore interactions do not mint duplicate rewards");
            g.PrepareSave();
            var resumed = new GameCore(data, CloneSave(g.save));
            Check(resumed.Has("lore_1") && resumed.Has("treasure_1") &&
                resumed.save.coins == 999999 && resumed.save.inventory[1] == 9999,
                "Both lore discovery and the legitimately collected treasure survive reload");
        }

        public static void PhysicalFrontierCachesStayClaimableWithFullStacks(Campaign data)
        {
            var g = Fresh(data);
            // The physical rescue box remains locked behind its relay.
            g.save.flags.Add("outer_relay_0");
            var cache = Object(g, "outercache");
            g.save.x = cache.x; g.save.y = cache.y;
            g.save.coins = 999999; g.save.inventory[0] = 9999;
            g.Talk(cache);
            Check(!g.OuterCacheRecovered(0) && g.save.coins == 999999 &&
                g.save.inventory[0] == 9999,
                "Full rescue materials and wallet must not consume a one-time fourth-ring relief chest");
            g.save.coins = 999981; g.save.inventory[0] = 9998;
            g.Talk(cache);
            Check(!g.OuterCacheRecovered(0) && g.save.coins == 999981,
                "One missing material slot keeps both resources waiting in the real rescue chest");
            g.save.inventory[0] = 9997; g.Talk(cache);
            Check(g.OuterCacheRecovered(0) && g.save.coins == 999999 &&
                g.save.inventory[0] == 9999,
                "Freeing all required capacity awards the actual rescue chest atomically");
            g.Close(); g.Talk(cache);
            Check(g.save.coins == 999999 && g.save.inventory[0] == 9999,
                "Claimed fourth-ring relief box cannot duplicate its reward");

            g = Fresh(data);
            var hidden = Object(g, "farhidden");
            g.save.x = hidden.x; g.save.y = hidden.y;
            g.save.coins = 999999; g.save.inventory[0] = 9999;
            g.Talk(hidden);
            Check(!g.Has("far_secret_0") && g.save.inventory[0] == 9999,
                "Remote sixth-ring cache survives its first full-stack interaction");
            g.save.coins = 999990; g.save.inventory[0] = 9997;
            g.Talk(hidden);
            Check(g.Has("far_secret_0") && g.save.coins == 999999 &&
                g.save.inventory[0] == 9999,
                "Sixth-ring cache pays all nine coins and both materials once space is available");

            g = Fresh(data);
            var annex = Object(g, "annexcache");
            g.save.x = annex.x; g.save.y = annex.y;
            g.save.coins = 999999; g.save.inventory[0] = 9999;
            g.Talk(annex);
            Check(!g.Has("annex_cache_" + annex.id),
                "Full fourth-ring annex cache remains uncollected instead of erasing resources");
            g.save.coins = 999992; g.save.inventory[0] = 9997;
            g.Talk(annex);
            Check(g.Has("annex_cache_" + annex.id) &&
                g.save.coins == 999999 && g.save.inventory[0] == 9999,
                "Actual annex exploration pickup awards both saved resources after capacity is freed");
            g.PrepareSave();
            var loaded = new GameCore(data, CloneSave(g.save));
            Check(loaded.Has("annex_cache_" + annex.id) &&
                loaded.save.coins == 999999 && loaded.save.inventory[0] == 9999,
                "Collected cache flags and fully accounted inventories roundtrip through v2");
        }

        public static void PublicWorkshopCannotConsumeIngredientsForOverfullParts(Campaign data)
        {
            var g = Fresh(data);
            g.save.stage = 3;
            g.save.ending = 3; // Public post-finale workshops are unavailable before the real ending.
            g.save.flags.Add("boss_4");
            g.save.flags.Add("afterlight_started");
            g.save.flags.Add("afterlight_plan_3_care");
            g.save.flags.Add("afterlight_done_3");
            g.Enter(3); g.enemies.Clear();
            WorldObject foundry = Object(g, "afterlightoutlet3");
            g.save.x = foundry.x; g.save.y = foundry.y;
            g.save.inventory[0] = 3; g.save.inventory[1] = 5;
            g.save.inventory[4] = 9999;
            Action(g, foundry.kind, "afterlight_craft");
            Check(g.save.inventory[0] == 3 && g.save.inventory[1] == 5 &&
                g.save.inventory[4] == 9999,
                "Real public recycling cannot spend timber and starcrystal for parts discarded beyond the save cap");
            g.save.inventory[4] = 9998;
            Action(g, foundry.kind, "afterlight_craft");
            Check(g.save.inventory[0] == 3 && g.save.inventory[1] == 5,
                "Recycling also rejects partial room for an indivisible two-part batch");
            g.save.inventory[4] = 9997;
            Action(g, foundry.kind, "afterlight_craft");
            Check(g.save.inventory[0] == 2 && g.save.inventory[1] == 3 &&
                g.save.inventory[4] == 9999,
                "The same real workshop charges one full recipe only after both produced parts fit");
        }

        public static void OrrinPublicReliefResponsibilityWaitsForActualGoods(Campaign data)
        {
            foreach(bool aid in new[] { true, false })
            {
                var g = Fresh(data);
                g.save.stage = 3; g.save.ending = 2;
                foreach(string flag in new[] { "afterlight_complete", "storm_started",
                    "storm_complete", "storm_orrin_open", "hearing_started",
                    "hearing_complete", "hearing_public", "boss_4",
                    "hearing_voice_1_care", "hearing_voice_2_watch",
                    "hearing_voice_3_care" }) g.save.flags.Add(flag);
                g.Enter(4); g.enemies.Clear();
                WorldObject shrine = Object(g, "shrine");
                g.save.x = shrine.x; g.save.y = shrine.y;
                int slot = aid ? 6 : 4;
                string choice = aid ? "hearing_orrin_aid" : "hearing_orrin_patrol";
                g.save.inventory[slot] = 9999;
                Action(g, shrine.kind, choice);
                Check(g.HearingOrrinResponsibility == 0 && !g.Has(choice) &&
                    g.save.inventory[slot] == 9999 && g.save.ending == 2,
                    "Full relief stack may not consume Orrin's irreversible civilian responsibility choice");
                g.save.inventory[slot] = 9998;
                Action(g, shrine.kind, choice);
                Check(g.HearingOrrinResponsibility == 0 &&
                    g.save.inventory[slot] == 9998,
                    "Public accountability transfer remains pending if only one slot is available");
                g.save.inventory[slot] = 9997;
                Action(g, shrine.kind, choice);
                Check(g.HearingOrrinResponsibility == (aid ? 1 : 2) &&
                    g.Has(choice) && g.save.inventory[slot] == 9999 &&
                    g.save.ending == 2,
                    "Both public supplies are transferred only when their full inventory capacity exists");
                g.PrepareSave();
                var restored = new GameCore(data, CloneSave(g.save));
                Check(restored.HearingOrrinResponsibility == (aid ? 1 : 2) &&
                    restored.save.inventory[slot] == 9999,
                    "Completed civic responsibility and tangible supplies persist in a v2 save");
            }
        }

        public static void LateCampaignPublicationsKeepRewardsSaveCompatible(Campaign data)
        {
            var atlas = Fresh(data);
            atlas.save.coins = 999999; atlas.save.skillPoints = 99;
            atlas.save.flags.Add("archive_started");
            foreach(int region in new[] { 1, 2, 3 })
                atlas.save.flags.Add("archive_fragment_" + region);
            int originalXp = atlas.save.xp;
            WorldObject keeper = Object(atlas, "archivekeeper");
            atlas.save.x = keeper.x; atlas.save.y = keeper.y;
            Action(atlas, "archivekeeper", "archive_complete");
            Check(atlas.Has("archive_complete") &&
                atlas.save.coins == 999999 && atlas.save.skillPoints == 99 &&
                atlas.save.xp > originalXp,
                "Authentic chapter-spanning atlas publication grants XP and story progress without overfilling rewards");

            var civic = Fresh(data);
            civic.save.stage = 3; civic.save.ending = 3;
            civic.save.flags.Add("boss_4");
            civic.save.flags.Add("afterlight_started");
            for(int region=1; region<=3; region++)
            {
                civic.save.flags.Add("afterlight_done_" + region);
                civic.save.flags.Add("afterlight_steward_" + region);
            }
            civic.save.coins = 999990;
            civic.save.shards = 99998;
            civic.save.skillPoints = 99;
            WorldObject council = Object(civic, "afterlightcouncil");
            civic.save.x = council.x; civic.save.y = council.y;
            Action(civic, "afterlightcouncil", "afterlight_publish");
            Check(civic.Has("afterlight_complete") &&
                civic.save.coins == 999999 && civic.save.shards == 99999 &&
                civic.save.skillPoints == 99 && civic.save.ending == 3,
                "Actually publishing the public reconstruction charter caps all persistent rewards without changing the original ending");
            civic.PrepareSave();
            var restored = new GameCore(data, CloneSave(civic.save));
            Check(restored.Has("afterlight_complete") &&
                restored.save.coins == 999999 && restored.save.shards == 99999 &&
                restored.save.skillPoints == 99,
                "Max-cap public charter rewards roundtrip through the original v2 save model");
        }

        public static void DistantRidgeWayfindingRemainsSaveBounded(Campaign data)
        {
            var g = Fresh(data);
            g.save.coins = 999999; g.save.shards = 99999;
            g.save.skillPoints = 99;
            var north = Array.Find(data.maps[0].objects,
                o => o.id == "ridge_route_0_0");
            var south = Array.Find(data.maps[0].objects,
                o => o.id == "ridge_route_0_1");
            Check(north != null && south != null,
                "Both actual sixth-ring navigation markers must remain authored");
            g.save.x = north.x; g.save.y = north.y; g.Talk(north);
            Check(g.RidgeSeen(0, 0) && !g.RidgeLinked(0),
                "First marker records the real route without minting its two-point completion");
            g.Close(); g.save.x = south.x; g.save.y = south.y; g.Talk(south);
            Check(g.RidgeLinked(0) &&
                g.save.coins == 999999 && g.save.shards == 99999 &&
                g.save.skillPoints == 99,
                "Completing the real ridge connection preserves a full persistent wallet and shard ledger");
            g.Close(); g.Talk(south);
            Check(g.save.coins == 999999 && g.save.shards == 99999,
                "Returning to an already surveyed route cannot duplicate its grant");
            g.PrepareSave();
            var reload = new GameCore(data, CloneSave(g.save));
            Check(reload.RidgeLinked(0) && reload.save.coins == 999999 &&
                reload.save.shards == 99999,
                "World-route completion and legal coin/shard totals persist through the original v2 save");
        }

        public static void DeepAbyssGalleryRewardsRespectRealMidrunSaveCaps(Campaign data)
        {
            var g = Fresh(data);
            g.save.frontierEnding = 1;
            g.StartAbyss(0);
            Check(g.InAbyss, "A real authorized expedition starts before gallery reward tests");
            WorldObject survey = Object(g, "abyssFarSurvey");
            WorldObject cache = Object(g, "abyssFarCache");
            WorldObject echo = Object(g, "abyssFarEcho");
            g.save.runXp = g.save.runCoins = g.save.runShards = 9998;
            g.save.x = survey.x; g.save.y = survey.y;
            g.Talk(survey); g.Close();
            Check(g.Has("abyss_annex_far_survey_1") &&
                g.save.runXp == 9999,
                "Physical lower-gallery testimony caps pending XP without erasing its discovery");
            g.save.x = cache.x; g.save.y = cache.y;
            g.Talk(cache); g.Close();
            Check(g.Has("abyss_annex_far_cache_1") &&
                g.save.runCoins == 9999 && g.save.runShards == 9999,
                "Real remote supply cache limits both unbanked physical resources to the persisted v2 bounds");
            g.save.x = echo.x; g.save.y = echo.y;
            g.Talk(echo); g.Close();
            Check(g.Has("abyss_annex_far_echo_1") && g.save.runXp == 9999,
                "Echo evidence completes the narrative without overflowing pending XP");
            g.Talk(echo); g.Close();
            Check(g.save.runXp == 9999,
                "Repeated dialogue cannot duplicate lower-gallery pending rewards");
            g.PrepareSave();
            var restored = new GameCore(data, CloneSave(g.save));
            Check(restored.InAbyss && restored.save.runXp == 9999 &&
                restored.save.runCoins == 9999 && restored.save.runShards == 9999 &&
                restored.Has("abyss_annex_far_cache_1") &&
                restored.Has("abyss_annex_far_echo_1"),
                "All three original-save-format unbanked resources and discovery flags survive reload");
            restored.enemies.Clear();
            int coins = restored.save.coins, shards = restored.save.shards;
            Action(restored, "abyssGate", "abyssBank");
            Check(!restored.InAbyss && restored.save.room == 0 &&
                restored.save.coins == coins + 9999 &&
                restored.save.shards == shards + 9999,
                "Authentic cleared-floor extraction banks preserved gallery loot exactly once");
        }

        public static void PublishedGlasswindRoutesRemainValidAtMaximumSavedRewards(Campaign data)
        {
            var g = Fresh(data);
            g.save.flags.Add("desert_report");
            g.save.flags.Add("glasswind_all_sealed");
            g.save.coins = 999999; g.save.shards = 99999;
            g.save.skillPoints = 99;
            var author = Object(g, "glasswindkeeper");
            g.save.x = author.x; g.save.y = author.y;
            int xp = g.save.xp;
            g.Talk(author);
            Check(g.mode == "dialog" && g.conversation != null &&
                Array.Exists(g.conversation.choices, c => c.action == "glasswind_publish"),
                "Physical wind surveyor must offer publication after the authentic desert report and three seals; flags=" +
                g.Has("desert_report") + "/" + g.Has("glasswind_all_sealed") +
                " choices=" + (g.conversation == null ? "<none>" :
                    string.Join(",", Array.ConvertAll(g.conversation.choices, c => c.action))));
            g.Close();
            Action(g, author.kind, "glasswind_publish");
            Check(g.Has("glasswind_complete") &&
                g.save.coins == 999999 && g.save.shards == 99999 &&
                g.save.skillPoints == 99 && g.save.xp > xp,
                "Physically publishing the completed public wind chart records its real achievement and XP without exceeding old saved currency caps");
            g.PrepareSave();
            var restored = new GameCore(data, CloneSave(g.save));
            Check(restored.Has("glasswind_complete") &&
                restored.save.coins == 999999 && restored.save.shards == 99999 &&
                restored.save.skillPoints == 99,
                "Public wind charter and bounded one-time payout survive a genuine old-format save reload");
            restored.Talk(author);
            Check(restored.mode == "dialog" && restored.conversation != null &&
                !Array.Exists(restored.conversation.choices,
                    c => c.action == "glasswind_publish") &&
                restored.save.coins == 999999 && restored.save.shards == 99999 &&
                restored.save.skillPoints == 99,
                "After publication the real NPC removes its one-shot claim option and cannot issue another payout");
        }

        public static void TriadAuditPublicationCreditsRealStoryWithoutSaveOverflow(Campaign data)
        {
            var g = Fresh(data);
            g.save.stage = 3;
            foreach (string flag in new[] { "core_1", "core_2", "core_3",
                "triad_started", "triad_sealed" }) g.save.flags.Add(flag);
            g.save.coins = 999999; g.save.shards = 99999;
            var guild = Object(g, "guild");
            g.save.x = guild.x; g.save.y = guild.y;
            int xp = g.save.xp;
            Action(g, guild.kind, "triad_publish");
            Check(g.Has("triad_complete") &&
                g.save.coins == 999999 && g.save.shards == 99999 &&
                g.save.xp > xp,
                "Real fourth-chapter three-core audit can be published at the wallet limits without corrupting persistent totals");
            g.PrepareSave();
            var loaded = new GameCore(data, CloneSave(g.save));
            Check(loaded.Has("triad_complete") && loaded.save.coins == 999999 &&
                loaded.save.shards == 99999 && !loaded.Has("boss_4"),
                "Actual evidence publication and original ending gate survive the v2 save roundtrip");
        }

        public static void EightPhysicalTrailCircuitsPreserveFinalTalentAtSaveLimits(Campaign data)
        {
            var g = Fresh(data); g.save.stage = 3;
            for (int region = 1; region <= 7; region++)
                g.save.flags.Add("trail_circuit_" + region + "_3");
            g.save.coins = 999998; g.save.shards = 99998;
            g.save.skillPoints = 99;
            Check(g.TrailCircuitCount == 7 && !g.Has("trail_circuits_all"),
                "Seven independently completed regions are not an earned full roadwarden network");
            int xp = g.save.xp;
            for (int step = 1; step <= 3; step++)
            {
                WorldObject bearing = null;
                foreach (WorldObject o in g.Map.objects)
                    if (o.id == "trailcircuit_0_" + step && o.kind == "trailcircuit")
                    { bearing = o; break; }
                Check(bearing != null, "Original Emberfall map retains authored walkable field bearing " + step);
                g.save.x = bearing.x; g.save.y = bearing.y;
                g.Talk(bearing);
                Check(g.Has("trail_circuit_0_" + step) &&
                    g.TrailCircuitProgress(0) == step,
                    "Physically reading the next original roadmarker advances this region's real ordered evidence");
                g.Close();
            }
            Check(g.TrailCircuitCount == 8 && g.Has("trail_circuits_all") &&
                g.save.coins == 999999 && g.save.shards == 99999 &&
                g.save.skillPoints == 99 && g.save.xp > xp,
                "Eight fully surveyed routes earn the original capped currency, XP, regional rest, and final talent milestone");
            g.PrepareSave();
            var restored = new GameCore(data, CloneSave(g.save));
            Check(restored.TrailCircuitCount == 8 &&
                restored.Has("trail_circuits_all") &&
                restored.save.coins == 999999 &&
                restored.save.shards == 99999 && restored.save.skillPoints == 99,
                "The complete regional walking network remains identical through legacy v2 save reload");
        }

        public static void PublishedSkywatchAndFinalDirectiveRespectV2RewardCaps(Campaign data)
        {
            var sky = Fresh(data); sky.save.stage = 3; sky.save.level = 12;
            foreach (string flag in new[] { "desert_report", "mountain_report",
                "gate_7", "skywatch_started", "skywatch_defeated",
                "skywatch_prism_1", "skywatch_prism_2", "skywatch_prism_3" })
                sky.save.flags.Add(flag);
            sky.save.coins = 999998; sky.save.shards = 99998;
            sky.save.skillPoints = 99;
            var observer = Object(sky, "skywatcher");
            sky.save.x = observer.x; sky.save.y = observer.y;
            int xp = sky.save.xp;
            Action(sky, observer.kind, "skywatch_publish");
            Check(sky.Has("skywatch_complete") &&
                sky.save.coins == 999999 && sky.save.shards == 99999 &&
                sky.save.skillPoints == 99 && sky.save.xp > xp,
                "The original public sky report completes and grants real XP without exceeding v2 currency and talent caps");
            sky.Enter(7); sky.enemies.Clear();
            var altar = Object(sky, "skyobservatory");
            sky.save.x = altar.x; sky.save.y = altar.y;
            foreach (int n in new[] {1,2,3}) sky.save.flags.Add("skywatch_afterglow_" + n);
            int afterglowXp = sky.save.xp;
            Action(sky, altar.kind, "skywatch_afterglow_publish");
            Check(sky.Has("skywatch_afterglow_complete") &&
                sky.save.coins == 999999 && sky.save.shards == 99999 &&
                sky.save.xp > afterglowXp,
                "The actual three-prism aftermath publication grants real XP and bounded rewards, not an overwritten save");
            sky.PrepareSave();
            var reloadedSky = new GameCore(data, CloneSave(sky.save));
            Check(reloadedSky.Has("skywatch_complete") &&
                reloadedSky.Has("skywatch_afterglow_complete") &&
                reloadedSky.save.coins == 999999 && reloadedSky.save.shards == 99999 &&
                reloadedSky.save.skillPoints == 99,
                "Both earned skywatch chapters persist in the original save format at maximum capacity");

            foreach (string policy in new[] {"last_directive_open","last_directive_shelter"})
            {
                var g = Fresh(data); g.save.stage = 4;
                g.save.flags.Add("civic_response_ledger"); g.save.flags.Add(policy);
                g.save.coins = 999998; g.save.shards = 99998;
                g.save.skillPoints = 99;
                var board = Object(g, "lastdirectiveboard");
                g.save.x = board.x; g.save.y = board.y;
                int beforeXp = g.save.xp;
                Action(g, board.kind, "last_directive_publish");
                Check(g.LastDirectiveComplete && g.LastDirectiveChoice ==
                    (policy == "last_directive_open" ? 1 : 2) &&
                    g.save.coins == 999999 && g.save.shards == 99999 &&
                    g.save.skillPoints == 99 && g.save.xp > beforeXp,
                    "The real civilian oversight/shelter policy is published once with bounded final rewards and original XP");
                g.PrepareSave();
                var resumed = new GameCore(data, CloneSave(g.save));
                Check(resumed.LastDirectiveComplete && resumed.save.coins == 999999 &&
                    resumed.save.shards == 99999 && resumed.save.skillPoints == 99,
                    "A published chapter ten order and capped goods survive old v2 save reload");
                resumed.Talk(board);
                Check(resumed.conversation != null &&
                    !Array.Exists(resumed.conversation.choices,
                        c => c.action == "last_directive_publish"),
                    "Both already published policies remove their claim choice and cannot duplicate permanent rewards");
            }
        }

        public static void CompleteAtlasEchoAndCitadelRelayRetainHighValueRewardsInV2(Campaign data)
        {
            var atlas = Fresh(data);
            atlas.save.stage = 3;
            atlas.save.flags.Add("archive_complete");
            atlas.Enter(3); atlas.enemies.Clear();
            WorldObject compass = Object(atlas, "archiveecho");
            atlas.save.x = compass.x; atlas.save.y = compass.y;
            atlas.save.coins = 999998; atlas.save.shards = 99998;
            int beforeXp = atlas.save.xp;
            atlas.invulnerable = 100;
            Action(atlas, compass.kind, "archive_echo_start");
            Enemy guardian = atlas.enemies.Find(e => e.id == 83001);
            Check(guardian != null,
                "A real physical atlas-calibration encounter must spawn its genuine elite");
            var hit = typeof(GameCore).GetMethod("Hit",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);
            Check(hit != null, "The original combat damage path is available");
            hit.Invoke(atlas, new object[] { guardian, 99999f, false, null, null });
            for (int tick = 0; tick < 12 && !atlas.Has("archive_echo_complete"); tick++)
                atlas.Tick(.05f, new Point());
            Check(atlas.Has("archive_echo_complete") &&
                atlas.save.coins == 999999 && atlas.save.shards == 99999 &&
                atlas.save.xp > beforeXp,
                "A genuinely defeated elite still finishes its quest with bounded permanent money and XP");
            atlas.PrepareSave();
            var atlasResume = new GameCore(data, CloneSave(atlas.save));
            Check(atlasResume.Has("archive_echo_complete") &&
                atlasResume.save.coins == 999999 && atlasResume.save.shards == 99999,
                "Completed original Atlas Echo defense survives legacy v2 save reload without reward loss");

            var g = CommonsReady(data);
            g.save.coins = 999998; g.save.shards = 99998;
            g.save.skillPoints = 99;
            int xp = g.save.xp;
            CommonsActivate(g, 1);
            Check(g.CommonsRelayRoom == 4 && g.enemies.Exists(g.IsCommonsRelayEnemy),
                "Physical food circuit spawns its first real security wave");
            foreach (Enemy e in g.enemies.FindAll(g.IsCommonsRelayEnemy))
                hit.Invoke(g, new object[] { e, 99999f, false, null, null });
            CommonsActivate(g, 2);
            Check(g.CommonsRelayStep == 3 && g.enemies.Exists(g.IsCommonsRelayEnemy),
                "Winning first real combat opens the second original security wave");
            foreach (Enemy e in g.enemies.FindAll(g.IsCommonsRelayEnemy))
                hit.Invoke(g, new object[] { e, 99999f, false, null, null });
            CommonsActivate(g, 3);
            Check(g.Has("commons_relay_sealed") && g.CommonsRelayRoom == 0 &&
                g.save.coins == 999999 && g.save.shards == 99999 &&
                g.save.xp > xp,
                "Winning three live console interactions grants bounded sealed-circuit rewards");
            g.Enter(0);
            WorldObject witness = Object(g, "commonswitness");
            g.save.x = witness.x; g.save.y = witness.y;
            Action(g, witness.kind, "commons_publish");
            Check(g.Has("commons_relay_complete") &&
                g.save.coins == 999999 && g.save.shards == 99999 &&
                g.save.skillPoints == 99,
                "Ayla's physical public authorization earns the original permanent energy upgrade without exceeding the talent cap");
            g.PrepareSave();
            var restored = new GameCore(data, CloneSave(g.save));
            Check(restored.Has("commons_relay_sealed") &&
                restored.Has("commons_relay_complete") &&
                restored.save.coins == 999999 && restored.save.shards == 99999 &&
                restored.save.skillPoints == 99,
                "Entire three-stage civic defense and permanent final outcome survive unchanged v2 persistence");
            restored.Talk(witness);
            Check(restored.conversation != null &&
                !Array.Exists(restored.conversation.choices,
                    c => c.action == "commons_publish"),
                "Published city access key cannot pay another reward after resuming");
        }

    }
}
