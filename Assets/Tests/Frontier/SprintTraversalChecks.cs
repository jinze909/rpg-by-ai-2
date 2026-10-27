using System;

namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        private static GameCore SprintTestWorld(Campaign data,int profession=0)
        {
            var rows=new string[38];
            for(int y=0;y<rows.Length;y++)
            {
                char[] row=new string('#',110).ToCharArray();
                if(y>=2&&y<36)
                    for(int x=2;x<108;x++)row[x]='.';
                rows[y]=new string(row);
            }
            data.maps[1]=new MapData { id=1,rows=rows,
                spawnX=8.5f,spawnY=15.5f,objects=new WorldObject[0],
                decor=new Decoration[0] };
            var g=Fresh(data,profession);
            g.save.stage=3;g.save.flags.Add("scout_saved");
            g.Enter(1);g.enemies.Clear();g.projectiles.Clear();
            g.save.x=8.5f;g.save.y=15.5f;g.save.energy=90f;
            g.invulnerable=90f;
            Check(g.CanStand(g.Position),"Sprint fixture starts on a complete physical foot");
            return g;
        }
        public static void TruePlayerWalkingVelocityNeverIncludesDodgeOrPlantedAttack(Campaign data)
        {
            MapData original=data.maps[1];
            try
            {
                foreach(int profession in new[]{0,1,2})
                {
                    var g=SprintTestWorld(data,profession);
                    g.Tick(.05f,new Point(0,1));
                    Check(g.WalkingVelocity.y>.5f&&
                        Math.Abs(g.WalkingVelocity.x)<.001f,
                        "Walking enemy lead reads actual player swept northward foot translation");
                    g.Tick(.05f,new Point());
                    Check(g.WalkingVelocity.Length<.00001f,
                        "Releasing movement immediately clears previously sampled velocity");
                    g.Tick(.05f,new Point(1,0),null,true);
                    Check(g.WalkingVelocity.x>g.Speed*1.3f&&g.Sprinting,
                        "Enemy aim reads real faster sprint gait rather than ordinary walking intent");
                    g.Dash(new Point(1,0));
                    g.Tick(.05f,new Point());
                    Check(g.WalkingVelocity.Length<.00001f,
                        "Dash displacement never looks like ordinary predictable foot velocity");
                    for(int i=0;i<18;i++)g.Tick(.05f,new Point());
                    Check(g.WalkingVelocity.Length<.00001f,
                        "Stopped player cannot leave ghost sprint or dash velocity");
                    g.Enter(1);
                    Check(g.WalkingVelocity.Length<.00001f&&g.save.version==2,
                        "Crossing room resets the transient target motion, preserving v2 save schema");
                }
            }
            finally{data.maps[1]=original;}
        }

        public static void ArcherWarnsThenFiresAtCommittedPlayerFootLead(Campaign data)
        {
            MapData original=data.maps[1];
            try
            {
                var g=SprintTestWorld(data,1);
                g.save.x=14.5f;g.save.y=15.5f;
                var archer=new Enemy(987,1,20.5f,15.5f,70f);
                archer.state=0;archer.timer=0;
                g.enemies.Add(archer);
                g.Tick(.05f,new Point(0,1));
                Check(g.WalkingVelocity.y>.5f&&archer.state==1&&
                    archer.target.y>g.Position.y+.25f &&
                    archer.target.y<=g.Position.y+1.351f,
                    "Real enemy archer commits a bounded ahead-of-walk bow warning when launching its genuine 650ms windup");
                Check(archer.archerLeading,
                    "World renderer must own an actual announced physical bow-lead marker, not a predicted repaint");
                Point locked=archer.target;
                Point launchFoot=archer.Position;
                Point committedHeading=archer.facing;
                for(int n=0;n<16;n++)g.Tick(.05f,new Point());
                Check(g.projectiles.Exists(p=>!p.friendly&&
                    p.visual=="enemyArrow"&&
                    System.Object.ReferenceEquals(p.hostileShooter,archer)),
                    "Finishing the original bow draw launches a real enemyArrow, not bonus hitscan or ghost danger");
                Check((archer.target-locked).Length<.001f&&
                    (committedHeading-(locked-launchFoot).Normalized).Length<.001f,
                    "Lateral hero dodge during the draw cannot cause last-frame unfair auto-aim; original warning and bow facing stay committed");
                Check(g.save.version==2,
                    "Ordinary enemy predictive windup creates no permanent save fields");

                var stopped=SprintTestWorld(data,0);
                stopped.save.x=14.5f;stopped.save.y=15.5f;
                var waiting=new Enemy(988,1,20.5f,15.5f,70f);
                waiting.timer=0;stopped.enemies.Add(waiting);
                stopped.Tick(.05f,new Point());
                Check(waiting.state==1&&!waiting.archerLeading&&
                    (waiting.target-stopped.Position).Length<.001f,
                    "Enemy ordinary arrow still aims exactly at stationary hero without false leading");
            }
            finally{data.maps[1]=original;}
        }

        public static void EnemyBowWarningStopsAtActualClosingWall(Campaign data)
        {
            MapData original=data.maps[1];
            try
            {
                var g=SprintTestWorld(data,0);
                var bow=new Enemy(1740,1,12.5f,15.5f,90f);
                bow.state=1;bow.timer=.32f;bow.windupDuration=.65f;
                bow.target=new Point(20.5f,15.5f);
                bow.Face(bow.target-bow.Position);
                g.enemies.Add(bow);
                Point clear=g.ArcherWarningPhysicalEnd(bow);
                Check((clear-bow.target).Length<.001f&&
                    g.projectiles.Count==0,
                    "A clear enemy bow warning follows the actual unspent arrow from its physical muzzle to locked impact");
                char[] row=g.Map.rows[15].ToCharArray();
                row[16]='#';g.Map.rows[15]=new string(row);
                Point clipped=g.ArcherWarningPhysicalEnd(bow);
                Check(clipped.x>bow.Position.x&&clipped.x<16f&&
                    Math.Abs(clipped.y-bow.Position.y)<.001f&&
                    g.projectiles.Count==0,
                    "A newly sealed gate clips the genuine enemy bow warning BEFORE its leading arrow head hits the wall");
                g.Tick(.05f,new Point());
                Check(bow.state==0&&!g.projectiles.Exists(p=>
                    !p.friendly&&System.Object.ReferenceEquals(p.hostileShooter,bow)),
                    "Real enemy archer attack retires against the same freshly blocked lane; no ghost missile flies behind the warning");
                Check(g.save.version==2,
                    "Physical telegraph is a read-only world query, not a serialized attack workaround");
            }
            finally{data.maps[1]=original;}
        }

        public static void EnemyBowWarningClipsNewLivingFrontline(Campaign data)
        {
            MapData original=data.maps[1];
            try
            {
                var g=SprintTestWorld(data,0);
                var bow=new Enemy(1744,1,12.5f,15.5f,100f)
                    {state=1,timer=.32f,windupDuration=.65f};
                bow.target=new Point(20.5f,15.5f);
                bow.Face(bow.target-bow.Position);
                g.enemies.Add(bow);
                Check((g.ArcherWarningPhysicalEnd(bow)-bow.target).Length<.001f,
                    "Unobstructed real bow windup paints a full committed target lane");
                var guard=new Enemy(1745,0,16.5f,15.5f,100f)
                    {stun=100f,timer=100f};
                g.enemies.Add(guard);
                Point small=g.ArcherWarningPhysicalEnd(bow);
                Check(small.x>bow.Position.x&&
                    small.x<guard.x-.5f&&
                    Math.Abs(small.y-guard.y)<.001f,
                    "A new living raider clips the genuine warning BEFORE an arrow overlaps its physical front edge");
                guard.hp=0f;
                Check((g.ArcherWarningPhysicalEnd(bow)-bow.target).Length<.001f,
                    "Defeated body immediately releases the real committed sight line");
                guard.hp=100f;guard.y=18.5f;
                Check((g.ArcherWarningPhysicalEnd(bow)-bow.target).Length<.001f,
                    "An off-ray live enemy cannot suppress a real archer warning");
                var boss=new Enemy(1746,0,16.5f,15.5f,200f,true)
                    {stun=100f,timer=100f};
                g.enemies.Add(boss);
                Point broad=g.ArcherWarningPhysicalEnd(bow);
                Check(broad.x<small.x-.3f,
                    "A living Boss blocks the warning with its larger genuine projectile-contact silhouette");
                float cooldownBefore=bow.timer;
                Point duplicate=g.ArcherWarningPhysicalEnd(bow);
                Check((duplicate-broad).Length<.0001f&&
                    bow.timer==cooldownBefore&&g.projectiles.Count==0,
                    "Repeated production warning paints are read-only and do not launch or refresh a bow attack");
                g.Tick(.05f,new Point());
                Check(bow.state==0&&!g.projectiles.Exists(p=>
                    !p.friendly&&System.Object.ReferenceEquals(p.hostileShooter,bow)),
                    "The true enemy release cancels its pending arrow behind the same freshly occupied physical lane");
                Check(g.save.version==2,
                    "Live-body warning cannot change a legacy persistent schema");
            }
            finally{data.maps[1]=original;}
        }

        public static void LiveCivilianContactNeverPassesThroughClosingCover(Campaign data)
        {
            MapData original=data.maps[1];
            try
            {
                // This is a real escort-wave enemy release, not merely a
                // visibility helper: a contact valid at draw start must
                // stop hurting the protected follower when a gate shuts.
                foreach(bool gateClosed in new[]{false,true})
                {
                    var g=SprintTestWorld(data,0);
                    g.save.x=9.5f;g.save.y=12.5f;
                    g.reliefMissionRoom=1;g.reliefMissionWave=3;
                    g.reliefMissionTime=75f;
                    g.reliefFollowerSafety=100f;
                    g.reliefFollowerPosition=new Point(20.5f,15.5f);
                    var heavy=new Enemy(1741,2,18.5f,15.5f,140f)
                    {state=1,timer=.02f,windupDuration=.65f,
                     targetingReliefCivilian=true};
                    heavy.target=g.reliefFollowerPosition;
                    heavy.Face(heavy.target-heavy.Position);
                    g.enemies.Add(heavy);
                    Check(g.EnemyObjectiveContactLaneClear(heavy,heavy.target),
                        "Clear heavy windup initially has a genuinely open objective contact lane");
                    if(gateClosed)
                    {
                        char[] wall=g.Map.rows[15].ToCharArray();
                        wall[19]='#';g.Map.rows[15]=new string(wall);
                    }
                    Check(g.EnemyObjectiveContactLaneClear(heavy,heavy.target)==!gateClosed,
                        "A newly shut gate retracts the same heavy objective warning and damage authority");
                    float priorSafety=g.reliefFollowerSafety;
                    g.Tick(.05f,new Point());
                    // Escort weather always applies independent, small
                    // per-tick safety wear. Compare actual strike-sized
                    // loss, not exact equality, when a fresh wall cancels
                    // only the enemy's attack.
                    float loss=priorSafety-g.reliefFollowerSafety;
                    Check(gateClosed ? loss>=0f&&loss<1f : loss>10f,
                        "Real escort strike causes significant safety damage only on the open lane; sealed cover still permits normal weather wear");
                    Check(g.save.version==2,
                        "Objective cover and warning synchronization change no old save fields");
                }
                // Living enemy cover stops an ordinary raider's objective
                // contact, but cannot nullify the heavy's authored AoE.
                var body=SprintTestWorld(data,0);
                var hunter=new Enemy(1742,0,18.5f,15.5f,90f);
                var guard=new Enemy(1743,0,19.5f,15.5f,90f);
                Point protectedFoot=new Point(20.5f,15.5f);
                body.enemies.Add(hunter);body.enemies.Add(guard);
                Check(!body.EnemyObjectiveContactLaneClear(hunter,protectedFoot),
                    "Living frontline cover also retracts a melee civilian threat before actual release");
                guard.y=18.5f;
                Check(body.EnemyObjectiveContactLaneClear(hunter,protectedFoot),
                    "Removing physical body cover restores the ordinary objective contact lane");
            }
            finally{data.maps[1]=original;}
        }

        public static void HoldingSprintMovesFasterAndChargesRealDistance(Campaign data)
        {
            MapData original=data.maps[1];
            try
            {
                for(int profession=0;profession<3;profession++)
                {
                    var walking=SprintTestWorld(data,profession);
                    var sprint=SprintTestWorld(data,profession);
                    float baseEnergy=walking.save.energy;
                    Point from=walking.Position;
                    walking.Tick(.05f,new Point(1,0));
                    sprint.Tick(.05f,new Point(1,0),null,true);
                    float stride=(walking.Position-from).Length;
                    float fast=(sprint.Position-from).Length;
                    Check(sprint.Sprinting&&!walking.Sprinting&&
                        Math.Abs(fast-stride*GameCore.SprintSpeedFactor)<.001f,
                        "Held Shift actually multiplies full-body physical root translation for every class");
                    Check(Math.Abs((walking.save.energy-sprint.save.energy)-
                        GameCore.SprintEnergyRate*.05f)<.001f,
                        "Sprint pays energy on top of the identical normal passive regeneration");
                    Check(sprint.footContacts>=walking.footContacts&&
                        sprint.walkDistance>walking.walkDistance&&
                        (sprint.VisualFacing-new Point(1,0)).Length<.001f,
                        "Extra traveled ground advances ordinary foot-contact cadence without replacing the canonical hero");
                    Check(Math.Abs(baseEnergy-walking.save.energy)<2f&&
                        sprint.save.version==2,
                        "Movement does not mutate permanent progression or the existing save format");
                }
            }
            finally{data.maps[1]=original;}
        }
        public static void StationaryOrWalledSprintCannotConsumeEnergy(Campaign data)
        {
            MapData original=data.maps[1];
            try
            {
                var g=SprintTestWorld(data);
                g.Tick(.05f,new Point(),null,true);
                Check(!g.Sprinting&&g.walkDistance==0f,
                    "Holding sprint while stationary does not fake movement or footsteps");
                g.save.x=20.5f;g.save.y=15.5f;
                char[] wall=g.Map.rows[15].ToCharArray();
                wall[21]='#';g.Map.rows[15]=new string(wall);
                // This wall extends above and below the entire body.
                for(int y=13;y<=17;y++)
                {
                    char[] row=g.Map.rows[y].ToCharArray();row[21]='#';
                    g.Map.rows[y]=new string(row);
                }
                for(int i=0;i<12;i++)g.Tick(.05f,new Point(1,0),null,true);
                Point stopped=g.Position;
                Check(g.CanStand(stopped)&&stopped.x<21f,
                    "The actor collides with the real vertical wall even during sprint");
                float energy=g.save.energy;
                for(int i=0;i<10;i++)g.Tick(.05f,new Point(1,0),null,true);
                Check((g.Position-stopped).Length<.001f&&!g.Sprinting&&
                    g.save.energy>=energy,
                    "Repeated sprint input into a solid wall costs no energy after travel ends");
                Check(g.footContacts>=0&&g.CanStand(g.Position),
                    "Wall impact never inserts nonphysical footsteps or buries the whole actor");
            }
            finally{data.maps[1]=original;}
        }
        public static void SprintExhaustionRequiresRecoveryAndNeverPersists(Campaign data)
        {
            MapData original=data.maps[1];
            try
            {
                var g=SprintTestWorld(data);
                g.save.energy=10.8f;
                for(int i=0;i<20&&!g.SprintFatigued;i++)
                    g.Tick(.05f,new Point(1,0),null,true);
                Check(g.SprintFatigued,"A physically spent sprint triggers the transient fatigue latch");
                float energy=g.save.energy;Point before=g.Position;
                g.Tick(.05f,new Point(1,0),null,true);
                Check(!g.Sprinting&&(g.Position-before).Length>0&&
                    g.save.energy>energy,
                    "A fatigued runner continues ordinary walking and regenerates instead of exceeding their energy");
                g.save.energy=GameCore.SprintRestartEnergy-.1f;
                g.Tick(.05f,new Point(),null,true);
                Check(!g.SprintFatigued&&!g.Sprinting,
                    "Fatigue clears only once real passive recovery crosses the restart threshold");
                g.Tick(.05f,new Point(1,0),null,true);
                Check(g.Sprinting,"Recovered stamina authorizes a new physical sprint");
                g.PrepareSave();
                var restored=new GameCore(data,CloneSave(g.save));
                restored.Start();
                Check(!restored.Sprinting&&!restored.SprintFatigued&&
                    restored.save.version==2,
                    "Loading an original v2 save does not retain stale button or fatigue state");
            }
            finally{data.maps[1]=original;}
        }
        public static void DashCompletionReturnsUnusedTickToWalkAndSprint(Campaign data)
        {
            MapData original=data.maps[1];
            try
            {
                for(int profession=0;profession<3;profession++)
                    foreach(bool sprint in new[]{false,true})
                    {
                        var g=SprintTestWorld(data,profession);
                        g.Dash(new Point(1,0));
                        for(int i=0;i<3;i++)
                            g.Tick(.05f,new Point());
                        Check(Math.Abs(g.dashTime-.03f)<.0001f &&
                            g.walkDistance==0f && g.footContacts==0,
                            "Original 180ms dodge consumes only its swept movement, without false steps");
                        Point before=g.Position;
                        float beforeEnergy=g.save.energy;
                        float walkSpan=.02f*g.Speed*
                            (sprint?GameCore.SprintSpeedFactor:1f);
                        g.Tick(.05f,new Point(1,0),null,sprint);
                        float expected=.03f*15f+walkSpan;
                        Check(g.dashTime==0f &&
                            Math.Abs(g.Position.x-before.x-expected)<.002f &&
                            g.CanStand(g.Position),
                            "Last 30ms of dodge and next 20ms of held movement each get their real physical speed");
                        Check(Math.Abs(g.walkDistance-walkSpan)<.002f &&
                            g.footContacts==0 &&
                            g.Sprinting==sprint,
                            "The dash itself never fabricates a step; only real post-dodge walking advances gait and sprint status");
                        float recovered=.05f*
                            (profession==2?18f:14f);
                        float spent=sprint?GameCore.SprintEnergyRate*.02f:0f;
                        Check(Math.Abs(g.save.energy-
                            (beforeEnergy+recovered-spent))<.003f,
                            "Post-dodge sprint charges only the usable 20ms, never the preceding dodge time");
                        Check(g.save.version==2,
                            "Subframe locomotion never changes persistent save schema");
                    }
                var reverse=SprintTestWorld(data);
                reverse.Dash(new Point(1,0));
                for(int i=0;i<3;i++)reverse.Tick(.05f,new Point());
                float beforeReverse=reverse.Position.x;
                reverse.Tick(.05f,new Point(-1,0));
                Check(reverse.Position.x>beforeReverse &&
                    Math.Abs(reverse.walkDistance-.02f*reverse.Speed)<.002f &&
                    reverse.locomotionFacing.x<-.9f &&
                    reverse.movementDirection.x<0f,
                    "Reversing input at dash finish plants the new walking direction without stealing dodge distance");
            }
            finally { data.maps[1]=original; }
        }

        public static void SprintCannotMultiplyDodgeCastRecoilOrMenus(Campaign data)
        {
            MapData original=data.maps[1];
            try
            {
                var g=SprintTestWorld(data);
                g.mode="journal";Point before=g.Position;
                g.Tick(.05f,new Point(1,0),null,true);
                Check((g.Position-before).Length<.001f&&!g.Sprinting,
                    "Held Shift in the journal never moves the world hero");
                g.mode="world";
                g.Dash(new Point(1,0));
                Check(g.dashTime>0,"Real dodge begins before movement arbitration");
                g.Tick(.05f,new Point(1,0),null,true);
                Check(!g.Sprinting&&g.dashTime>0,
                    "Dodge retains exact original speed, damage protection and movement rules");
                while(g.dashTime>0)g.Tick(.05f,new Point(),null,true);
                g.save.energy=100;
                g.Skill(new Point(1,0));
                Check(g.playerAction!=null,"A genuine profession skill started");
                g.Tick(.05f,new Point(1,0),null,true);
                Check(!g.Sprinting&&g.playerAction!=null,
                    "Held Shift cannot accelerate the planted live combat action");
                g.playerHitTime=.2f;g.playerRecoil=new Point(-1,0);
                g.Tick(.05f,new Point(1,0),null,true);
                Check(!g.Sprinting,
                    "Actual recoil and hit lock cannot be mistaken for voluntary sprint steps");
            }
            finally{data.maps[1]=original;}
        }
    }
}
