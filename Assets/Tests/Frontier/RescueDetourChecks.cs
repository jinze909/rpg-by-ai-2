using System;
using System.Collections.Generic;

namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        private static string[] RescueDetourArena(bool lowerGap, bool upperGap)
        {
            string[] rows = new string[18];
            for (int y = 0; y < rows.Length; y++)
            {
                char[] tiles = new string('.', 18).ToCharArray();
                for (int x = 0; x < tiles.Length; x++)
                    if (x < 2 || x >= 16 || y < 2 || y >= 16)
                        tiles[x] = '#';
                if (y >= 2 && y < 16 && !(lowerGap && y == 13) &&
                    !(upperGap && y == 4)) tiles[8] = '#';
                rows[y] = new string(tiles);
            }
            return rows;
        }
        private static GameCore RescueDetourGame(Campaign data, string[] rows)
        {
            var game = Fresh(data);
            game.save.stage = 4;
            game.save.frontierEnding = 3;
            game.save.flags.Add("exp_intro");
            game.save.flags.Add("harbor_report");
            game.save.flags.Add("farshore_relief_6");
            game.Enter(6);
            game.mode = "world";
            game.enemies.Clear();
            data.maps[6] = new MapData {
                id = 6, spawnX = 5.5f, spawnY = 8.5f,
                rows = rows, objects = new WorldObject[0],
                decor = new Decoration[0]
            };
            game.save.x = 11.5f;
            game.save.y = 8.5f;
            game.save.teaUntil = game.save.totalTime + 300;
            game.save.health = game.save.maxHealth;
            game.reliefMissionRoom = 6;
            game.reliefMissionWave = 3;
            game.reliefMissionTime = 110f;
            game.reliefFollowerPosition = new Point(5.5f, 8.5f);
            game.reliefFollowerSafety = 85f;
            return game;
        }
        public static void EscortWaitsAtClosedWallThenTraversesReopenedRealDetour(Campaign data)
        {
            MapData old = data.maps[6];
            try
            {
                string[] rows = RescueDetourArena(false, false);
                var game = RescueDetourGame(data, rows);
                Point initial = game.reliefFollowerPosition;
                for (int i=0;i<32;i++) game.Tick(.05f,new Point());
                Check(game.ReliefEscortActive && game.ReliefEscortRouteStalled &&
                      (game.reliefFollowerPosition-initial).Length < .001f,
                    "A sealed wall stalls a real civilian rather than hiding a teleport or invalid route");
                // Open a distant route at the far lower end of the dividing wall.
                char[] doorway = rows[13].ToCharArray();
                doorway[8]='.'; rows[13]=new string(doorway);
                float traveled = 0;
                int frames = 0;
                for (;frames<560 &&
                    (game.Position-game.reliefFollowerPosition).Length>1.6f;frames++)
                {
                    Point prior = game.reliefFollowerPosition;
                    game.Tick(.05f,new Point());
                    Point next = game.reliefFollowerPosition;
                    float segment = (next-prior).Length;
                    Check(game.CanStand(next) && segment<.24f,
                        "Replanned civilian route uses legal actor feet and incremental swept walking");
                    // Check the actual wall crossing, not later travel
                    // northward on the safe opposite bank.
                    if (next.x>8.3f && next.x<8.8f)
                        Check(next.y>12.2f && next.y<14.8f,
                            "Civilian physically crosses the wall only at the reopened lower passage; x="+next.x+" y="+next.y);
                    traveled += segment;
                }
                Check(game.ReliefEscortActive &&
                      (game.Position-game.reliefFollowerPosition).Length<1.7f &&
                      !game.ReliefEscortRouteStalled && traveled>12f,
                    "Reopened route completes a genuinely longer walk and clears the blocked warning");
            }
            finally { data.maps[6]=old; }
        }

        public static void EscortReplansWhenTheOldGateClosesAndAnotherOpens(Campaign data)
        {
            MapData old = data.maps[6];
            try
            {
                string[] rows = RescueDetourArena(true, false);
                var game = RescueDetourGame(data, rows);
                for (int i=0;i<34;i++) game.Tick(.05f,new Point());
                Check(game.reliefFollowerPosition.y>8.8f,
                    "A civilian initially follows the real lower detour with a committed route");
                char[] closed=rows[13].ToCharArray(); closed[8]='#';
                rows[13]=new string(closed);
                char[] opened=rows[4].ToCharArray(); opened[8]='.';
                rows[4]=new string(opened);
                float minY=game.reliefFollowerPosition.y;
                for(int i=0;i<620 &&
                    (game.Position-game.reliefFollowerPosition).Length>1.6f;i++)
                {
                    Point prior=game.reliefFollowerPosition;
                    game.Tick(.05f,new Point());
                    Point next=game.reliefFollowerPosition;
                    minY=Math.Min(minY,next.y);
                    Check(game.CanStand(next) && (next-prior).Length<.24f,
                        "Switching real gates never clips or teleports the escorted resident");
                    if(next.x>8.3f && next.x<8.8f)
                        Check(next.y>3.2f && next.y<5.9f,
                            "After a gate closes, the resident crosses only the newly opened upper passage; x="+next.x+" y="+next.y);
                }
                Check(minY<5.8f &&
                      (game.Position-game.reliefFollowerPosition).Length<1.7f &&
                      game.ReliefEscortActive,
                    "Live gate collision invalidates old route and recovers via the new detour");
            }
            finally { data.maps[6]=old; }
        }

        public static void PhysicalRouteRejectsBrokenCoordinatesAndClosedGoalFeet(Campaign data)
        {
            MapData old=data.maps[6];
            try
            {
                string[] rows=RescueDetourArena(false,false);
                var game=RescueDetourGame(data,rows);
                Point from=game.reliefFollowerPosition,to=game.Position;
                Check(game.NavigationPathFrom(new Point(float.NaN,8.5f),to,true).Count==0 &&
                    game.NavigationPathFrom(new Point(float.PositiveInfinity,8.5f),to,true).Count==0 &&
                    game.NavigationPathFrom(from,new Point(float.NegativeInfinity,8.5f),true).Count==0 &&
                    game.NavigationPathFrom(from,new Point(float.NaN,8.5f),false).Count==0,
                    "Broken actor and quest coordinates cannot become a valid physical navigation tile");
                Check(game.NavigationPathFrom(new Point(-.1f,8.5f),to,false).Count==0,
                    "Negative fractional out-of-bounds feet cannot silently truncate into column zero");
                Check(game.NavigationPathFrom(from,to,true).Count==0,
                    "A closed real wall never grants the civilian a physically finished route");
                char[] door=rows[13].ToCharArray();door[8]='.';
                rows[13]=new string(door);
                List<Point> route=game.NavigationPathFrom(from,to,true);
                Check(route.Count>10 && game.CanStand(route[route.Count-1]),
                    "The same live goal immediately offers a long detour after the original gate opens");
                Check((bool)typeof(GameCore).GetMethod("ReliefActorLineWalkable",
                    System.Reflection.BindingFlags.NonPublic|
                    System.Reflection.BindingFlags.Instance).Invoke(
                        game,new object[]{route[route.Count-1],to}),
                    "The final segment can carry a full actor, not just a point-like missile");
            }
            finally { data.maps[6]=old; }
        }

        public static void CivilianFullFootprintRejectsCornerGrazingCenterRays(Campaign data)
        {
            MapData old=data.maps[6];
            try
            {
                string[] rows=RescueDetourArena(true,true);
                // Only a single stone corner may catch the BODY: the thin
                // horizontal ray above it remains entirely unobstructed.
                for(int y=2;y<16;y++)
                {
                    char[] clear=rows[y].ToCharArray(); clear[8]='.';
                    rows[y]=new string(clear);
                }
                char[] wall=rows[8].ToCharArray(); wall[8]='#';
                rows[8]=new string(wall);
                var game=RescueDetourGame(data,rows);
                Point from=new Point(6.5f,7.85f),to=new Point(10.5f,7.85f);
                Check(game.CanStand(from) && game.CanStand(to),
                    "The two actor endpoints are independently safe in the corner fixture");
                var flags=System.Reflection.BindingFlags.NonPublic|
                    System.Reflection.BindingFlags.Instance;
                var thin=typeof(GameCore).GetMethod("ClearAttackPath",flags);
                var full=typeof(GameCore).GetMethod("ReliefActorLineWalkable",flags);
                Check((bool)thin.Invoke(game,new object[]{from,to}) &&
                    !(bool)full.Invoke(game,new object[]{from,to}),
                    "Projectile centerline is clear but the civilian footprint genuinely hits corner stone");
                game.reliefFollowerPosition=from;
                game.save.x=to.x;game.save.y=to.y;
                float walked=0f;
                for(int i=0;i<270 &&
                    (game.Position-game.reliefFollowerPosition).Length>1.6f;i++)
                {
                    Point prior=game.reliefFollowerPosition;
                    game.Tick(.05f,new Point());
                    Point next=game.reliefFollowerPosition;
                    Check(game.CanStand(next) && (next-prior).Length<.24f,
                        "Actual corner detour never enters a tile blocked to the whole actor");
                    walked+=(next-prior).Length;
                }
                Check(walked>2.3f &&
                      (game.Position-game.reliefFollowerPosition).Length<1.7f,
                    "Real civilian routes around a corner which only a point-like center ray could clear; walked="+walked+" x="+game.reliefFollowerPosition.x+" y="+game.reliefFollowerPosition.y);
            }
            finally { data.maps[6]=old; }
        }

        public static void CompanionHeroCoverRequiresActualAutomaticShotContact(Campaign data)
        {
            var game=Fresh(data,1);
            game.Enter(0);game.enemies.Clear();game.projectiles.Clear();
            game.save.flags.Add("companion_1");
            game.SelectCompanion(1);
            game.save.reducedMotion=true;
            game.save.x=15.5f;game.save.y=10.88f;
            game.companionPosition=new Point(10.5f,10.88f);
            game.ToggleCompanionHold();
            Check(game.CompanionHoldingPosition&&game.CanStand(game.companionPosition),
                "A real deployed bow ally must be planted on legal ground");
            var threat=new Enemy(99601,2,14.4f,10.88f,10000f)
                {state=1,timer=1.3f,windupDuration=1.4f,
                 target=game.Position,facing=new Point(1,0)};
            Check(game.CanStand(threat.Position),
                "Hero-threat interruption fixture is truly standable");
            game.enemies.Add(threat);
            var attackTimer=typeof(GameCore).GetField("companionAttack",
                System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.NonPublic);
            Check(attackTimer!=null,"Actual auto-attack timer exists in core");
            attackTimer.SetValue(game,0f);
            game.Tick(.02f,new Point());
            Check(game.companionAction!=null&&
                game.companionAction.kind=="attack",
                "Automatic ally commits a real ranged windup");
            Advance(game,.16f);
            Projectile shot=game.projectiles.Find(p=>
                System.Object.ReferenceEquals(p.heroCoverTarget,threat));
            Check(shot!=null&&shot.friendly&&shot.impact!=null&&
                !shot.impact.playerOwned&&threat.state==1,
                "Only a real released ally projectile is tagged to the exact imminent hero threat");
            Check(game.CompanionHeroCoverInFlight,
                "The released protective shot is observable until its physical contact");
            for(int frame=0;frame<50&&shot.heroCoverTarget!=null;frame++)
                game.Tick(.02f,new Point());
            Check(threat.state==0&&threat.stun>.05f&&
                game.effects.Exists(f=>f.kind=="number"&&
                    f.text=="援射打断！")&&
                !game.CompanionHeroCoverInFlight,
                "Actual swept bow impact interrupts the pending attack, cancels its intent and retires protection");
            Check(game.save.version==2,
                "Projectile interception never changes old save schema");
        }

        public static void CompanionHeroCoverCannotProtectFromLateMissOrReusedEnemyId(Campaign data)
        {
            var game=Fresh(data,1);
            game.Enter(0);game.enemies.Clear();game.projectiles.Clear();
            game.save.flags.Add("companion_1");
            game.SelectCompanion(1);
            game.save.x=15.5f;game.save.y=10.88f;
            game.companionPosition=new Point(10.5f,10.88f);
            var threat=new Enemy(99621,2,14.4f,10.88f,10000f)
                {state=1,timer=.9f,windupDuration=1f,
                 target=game.Position,facing=new Point(1,0)};
            var impersonator=new Enemy(99621,2,13.5f,10.88f,10000f)
                {state=1,timer=.9f,windupDuration=1f,
                 target=game.Position,facing=new Point(1,0)};
            game.enemies.Add(threat);game.enemies.Add(impersonator);
            var handoff=typeof(GameCore).GetMethod("HitFriendlyProjectileContact",
                System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.NonPublic);
            Check(handoff!=null,"The live physical projectile contact handles ally interruptions");
            Projectile shot=new Projectile {
                position=impersonator.Position,velocity=new Point(9f,0f),
                damage=2f,life=2f,friendly=true,visual="arrow",
                impact=new ImpactProfile(1,0,0,0,0f,false,-1),
                heroCoverTarget=threat,heroCoverSave=game.save,
                heroCoverMap=game.Map,heroCoverRoom=game.save.room
            };
            game.projectiles.Add(shot);
            handoff.Invoke(game,new object[]{shot,impersonator});
            Check(impersonator.state==1&&threat.state==1&&
                System.Object.ReferenceEquals(shot.heroCoverTarget,threat),
                "Hitting a different real actor with the same reused spawn ID cannot claim cover");
            handoff.Invoke(game,new object[]{shot,threat});
            Check(threat.state==0&&shot.heroCoverTarget==null,
                "Only actual contact against the reference-matched pending threat may interrupt");
            // A new contact has its own cue lifetime; isolate the next
            // negative assertion from the genuine prior protection effect.
            game.effects.Clear();
            var late=new Enemy(99622,2,14.4f,10.88f,10000f)
                {state=0,timer=1f,target=game.Position,facing=new Point(1,0)};
            game.enemies.Add(late);
            var stale=new Projectile {
                velocity=new Point(9f,0f),damage=2f,life=2f,
                friendly=true,visual="arrow",
                impact=new ImpactProfile(1,0,0,0,0f,false,-1),
                heroCoverTarget=late,heroCoverSave=game.save,
                heroCoverMap=game.Map,heroCoverRoom=game.save.room
            };
            game.projectiles.Add(stale);
            handoff.Invoke(game,new object[]{stale,late});
            Check(late.state==0&&late.stun==0f&&
                !game.effects.Exists(f=>f.kind=="number"&&
                    f.text=="援射打断！"&&
                    (f.position-late.Position).Length<.0001f),
                "A hit after the windup ended must deal normal damage without false protection");
            var original=game.save;
            game.save=CloneSave(original);
            Check(!game.CompanionHeroCoverInFlight,
                "A different save instance invalidates old automatic projectile coverage");
        }


        public static void ReliefWatchActualActorOwnershipRejectsForgedSameIdRaider(Campaign data)
        {
            for(int variant=0;variant<2;variant++)
            {
                var g=Fresh(data,0);g.Enter(0);g.enemies.Clear();
                var post=Array.Find(g.Map.objects,o=>o.kind=="outerwatch");
                Check(post!=null,"The actual regional relief outpost is on this map");
                g.save.x=post.x;g.save.y=post.y;
                g.save.flags.Add("outer_cache_0");
                if(variant==1)g.save.flags.Add("outer_patrol_done_0");
                g.Talk(post);
                string start=variant==1?"outer_watch_veteran":"outer_watch_start";
                int choice=Array.FindIndex(g.conversation.choices,c=>c.action==start);
                Check(choice>=0,"Both genuine standard and veteran relief defenses are offered");
                g.Choose(choice);
                Check(g.OuterPatrolActive&&g.enemies.Count==(variant==1?3:2),
                    "The real outpost spawns its original physical attacker count");
                Enemy first=g.enemies[0],second=g.enemies[1];
                Enemy third=variant==1?g.enemies[2]:null;
                var isMine=typeof(GameCore).GetMethod("IsOuterPatrolEnemy",
                    System.Reflection.BindingFlags.Instance|
                    System.Reflection.BindingFlags.NonPublic);
                var harm=typeof(GameCore).GetMethod("DamageOuterPatrolStock",
                    System.Reflection.BindingFlags.Instance|
                    System.Reflection.BindingFlags.NonPublic);
                var hit=typeof(GameCore).GetMethod("Hit",
                    System.Reflection.BindingFlags.Instance|
                    System.Reflection.BindingFlags.NonPublic);
                Check(isMine!=null&&harm!=null&&hit!=null,
                    "Production stock, encounter-ownership and lethal-hit entrypoints exist");
                var impersonator=new Enemy(first.id,0,first.x+.14f,first.y+.14f,300f);
                impersonator.targetingOuterPatrolStock=true;
                g.enemies.Add(impersonator);
                Check((bool)isMine.Invoke(g,new object[]{first})&&
                    !(bool)isMine.Invoke(g,new object[]{impersonator}),
                    "A distinct raider cannot own this outpost solely by sharing a numeric ID");
                float intact=g.OuterPatrolIntegrity;
                harm.Invoke(g,new object[]{impersonator});
                Check(g.OuterPatrolIntegrity==intact,
                    "A same-ID impostor cannot damage the protected cache");
                hit.Invoke(g,new object[]{impersonator,2000f,false,null,null});
                Check(g.OuterPatrolActive&&g.enemies.Contains(first)&&
                    !g.OuterPatrolDone(0) || variant==1&&g.OuterPatrolActive,
                    "A genuine non-mission kill cannot forge a standard/veteran win");
                var survivor=new Enemy(third!=null?third.id:second.id,0,
                    second.x+.12f,second.y+.1f,500f);
                g.enemies.Add(survivor);
                g.Talk(post);
                int abort=Array.FindIndex(g.conversation.choices,
                    c=>c.action=="outer_watch_abort");
                Check(abort>=0,"Current live outpost can still be withdrawn");
                g.Choose(abort);
                Check(!g.OuterPatrolActive&&g.enemies.Contains(survivor)&&
                    !g.enemies.Contains(first)&&!g.enemies.Contains(second)&&
                    (third==null||!g.enemies.Contains(third))&&
                    g.save.version==2,
                    "Withdrawal retires only real siege actors, never a living same-ID bystander");
            }
        }


        public static void ReliefWatchArrowCoverOnlyInterruptsActualUncoveredCacheContact(Campaign data)
        {
            var g=Fresh(data,0);g.Enter(0);g.enemies.Clear();
            var post=Array.Find(g.Map.objects,o=>o.kind=="outerwatch");
            Check(post!=null,"A real relief cache stands on this map");
            g.save.flags.Add("outer_cache_0");
            g.save.x=post.x;g.save.y=post.y;
            g.Talk(post);
            int begin=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_start");
            Check(begin>=0,"Real relief outpost offers an earned physical defense");
            g.Choose(begin);
            Check(g.OuterPatrolActive&&g.enemies.Count==2,
                "A real standard relief watch starts before the support shot");
            Enemy raider=g.enemies.Find(e=>e.kind==0);
            Check(raider!=null,"Standard outpost owns a real melee raider");
            Point station=new Point(post.x,post.y);
            var uncovered=typeof(GameCore).GetMethod("OuterPatrolUncoveredStockStrike",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            var collision=typeof(GameCore).GetMethod("HitFriendlyProjectileContact",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            Check(uncovered!=null&&collision!=null,
                "Actual cache threat and friendly swept-contact production methods exist");
            Point[] offsets={new Point(-2,0),new Point(2,0),
                new Point(0,-2),new Point(0,2),
                new Point(1,-1),new Point(-1,1)};
            bool realThreat=false;
            foreach(Point step in offsets)
            {
                Point source=station+step;
                if(!g.CanStand(source))continue;
                raider.x=source.x;raider.y=source.y;
                raider.state=1;raider.timer=.7f;raider.windupDuration=.7f;
                raider.stun=0f;raider.hitTime=0f;
                raider.targetingOuterPatrolStock=true;raider.target=station;
                raider.Face(station-raider.Position);
                foreach(Point escape in new[]{new Point(4,0),new Point(-4,0),
                    new Point(0,4),new Point(0,-4)})
                {
                    Point hero=station+escape;
                    if(!g.CanStand(hero)||
                        (hero-raider.Position).Length<=2.6f)continue;
                    g.save.x=hero.x;g.save.y=hero.y;
                    if((bool)uncovered.Invoke(g,new object[]{raider}))
                    {realThreat=true;break;}
                }
                if(realThreat)break;
            }
            Check(realThreat,
                "Real outpost has an open cache strike neither hero nor living ally blocks");
            var shot=new Projectile {
                position=raider.Position,velocity=new Point(9f,0f),
                life=2f,damage=2f,friendly=true,visual="arrow",
                impact=new ImpactProfile(1,0,0,0,0f,false,-1),
                patrolCoverTarget=raider,patrolCoverSave=g.save,
                patrolCoverMap=g.Map,patrolCoverRoom=g.save.room,
                outerPatrolOwned=true
            };
            g.projectiles.Add(shot);
            Check(g.CompanionPatrolCoverInFlight,
                "A released tagged shot may be seen without claiming it has already hit");
            float stock=g.OuterPatrolIntegrity;
            collision.Invoke(g,new object[]{shot,raider});
            Check(g.OuterPatrolActive&&raider.state==0&&raider.stun>.2f&&
                g.OuterPatrolIntegrity==stock&&shot.patrolCoverTarget==null&&
                g.effects.Exists(e=>e.outerPatrolOwned&&
                    e.text=="物资援射拦截！"),
                "Physical contact interrupts the exact real threat without creating stock or a win");
            g.effects.Clear();
            raider.state=1;raider.timer=.7f;raider.windupDuration=.7f;
            raider.stun=0f;raider.hitTime=0f;
            raider.targetingOuterPatrolStock=true;raider.target=station;
            var unrelated=new Enemy(raider.id,0,raider.x+.4f,
                raider.y+1.7f,900f);
            g.enemies.Add(unrelated);
            shot.patrolCoverTarget=raider;
            collision.Invoke(g,new object[]{shot,unrelated});
            Check(raider.state==1&&shot.patrolCoverTarget==raider&&
                !g.effects.Exists(e=>e.text=="物资援射拦截！"),
                "A separate enemy with the same ID cannot steal the real projectile's target");
            raider.targetingOuterPatrolStock=false;
            collision.Invoke(g,new object[]{shot,raider});
            Check(raider.state==1&&
                !g.effects.Exists(e=>e.text=="物资援射拦截！")&&
                g.OuterPatrolIntegrity==stock&&g.save.version==2,
                "Stale objective intent never becomes a free shot guard or a save migration");
        }

    }
}
