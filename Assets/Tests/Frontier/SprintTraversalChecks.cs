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
