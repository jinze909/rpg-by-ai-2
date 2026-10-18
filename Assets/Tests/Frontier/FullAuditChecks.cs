using System;
using System.Reflection;
namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        public static void EveryEncounterSpawnHasValidFooting(Campaign data)
        {
            foreach(int room in new[]{1,2,3,4,5,6,7}) foreach(float time in new[]{0f,28f})
            {
                var g=Unlocked(data);g.save.totalTime=time;g.Enter(room);
                foreach(Enemy enemy in g.enemies) Check(g.CanStand(enemy.Position),"Initial enemy footprint intersects a wall/water in region "+room+" at "+enemy.x+","+enemy.y);
                g.PrepareSave();var loaded=new GameCore(data,CloneSave(g.save));
                foreach(Enemy enemy in loaded.enemies)Check(loaded.CanStand(enemy.Position),"Restored enemy footprint must also be valid in region "+room);
            }
            for(int seed=1;seed<=6;seed++)for(int floor=1;floor<=6;floor++)
            {
                var g=Unlocked(data);g.save.room=8;g.save.abyssFloor=floor;g.save.abyssSeed=seed*137;g.Load(CloneSave(g.save));
                foreach(Enemy enemy in g.enemies)Check(g.CanStand(enemy.Position),"Abyss spawn must fit its whole footprint on floor "+floor+" / seed "+seed);
            }
            var malformed=Unlocked(data);malformed.Enter(3);malformed.save.snapshotRoom=3;
            malformed.save.worldEnemies=new[]{
                new EnemyRecord{id=1844,kind=0,x=float.MaxValue,y=float.MaxValue,hp=12,maxHp=99},
                new EnemyRecord{id=1845,kind=1,x=float.MinValue,y=12,hp=13,maxHp=99},
                new EnemyRecord{id=1846,kind=2,x=float.NaN,y=12,hp=14,maxHp=99},
                new EnemyRecord{id=1847,kind=0,x=float.PositiveInfinity,y=12,hp=15,maxHp=99}};
            malformed.Load(CloneSave(malformed.save));Check(malformed.enemies.Count==4,"Invalid saved coordinates recover rather than discarding valid health/progress or crashing load");
            foreach(Enemy enemy in malformed.enemies)Check(malformed.CanStand(enemy.Position)&&enemy.hp>=12&&enemy.hp<=15,"Extreme and non-finite saved positions recover with original actor health");
            var reinforcement=Quiet(data); Point edge=new Point(); bool found=false;
            for(int y=3;y<reinforcement.Map.rows.Length-3&&!found;y++)for(int x=3;x<reinforcement.Map.rows[y].Length-3&&!found;x++)
            { Point p=new Point(x+.5f,y+.5f);if(reinforcement.CanStand(p)&&!reinforcement.CanStand(p+new Point(-2,1))){edge=p;found=true;} }
            Check(found,"A real encounter must include an obstructed reinforcement offset");
            var boss=new Enemy(1840,3,edge.x,edge.y,999,true){hp=100,state=1,timer=.001f,cycle=2,target=reinforcement.Position};reinforcement.enemies.Add(boss);
            reinforcement.Tick(.01f,new Point());Check(reinforcement.enemies.Count==2,"The existing third-cycle reinforcement still spawns");
            foreach(Enemy enemy in reinforcement.enemies)Check(reinforcement.CanStand(enemy.Position),"Boss reinforcements cannot spawn inside an adjacent wall or locked arena");
            var opening=Quiet(data);opening.save.kills[1]=4;Action(opening,"scout","scout");opening.Talk(Object(opening,"shrine"));
            Check(opening.GateBarrierActive&&opening.mode=="world"&&!opening.enemies.Exists(e=>e.boss),"A logically completed objective cannot start a Boss through a still-dissolving collision boundary");
            Advance(opening,.6f);Action(opening,"shrine","boss");Check(opening.enemies.Exists(e=>e.boss&&opening.InProtectedArena(e.Position)&&opening.CanStand(e.Position)),"After the real opening finishes the Boss appears inside its valid arena");
        }
        public static void BossReinforcementsOccupyFreeReachableLocalFootingOrSkipSafely(Campaign data)
        {
            var spawn=typeof(GameCore).GetMethod("TrySpawnBossReinforcement",
                BindingFlags.NonPublic|BindingFlags.Instance);
            Check(spawn!=null,"Live Boss AI uses its dedicated real-world local summon transaction");
            foreach(int room in new[]{0,1,2,3,4})
            {
                var g=Unlocked(data);if(room!=0)g.Enter(room);
                g.enemies.Clear();
                Point origin=new Point(g.Map.spawnX,g.Map.spawnY);
                // Keep the player away from the immediate summon socket so
                // either a legitimate first slot or an alternative is possible.
                g.save.x=origin.x;g.save.y=origin.y;
                Point bossFoot=new Point(origin.x+2f,origin.y+2f);
                if(!g.CanStand(bossFoot))bossFoot=origin;
                var boss=new Enemy(88510+room,3,bossFoot.x,bossFoot.y,999,true)
                    {hp=100,cycle=2,state=0,timer=99};
                g.enemies.Add(boss);
                bool created=(bool)spawn.Invoke(g,new object[]{boss});
                if(created)
                {
                    Check(g.enemies.Count==2,
                        "Local Boss wave creates exactly one real recruit without duplicating enemies");
                    Enemy recruit=g.enemies[1];
                    Check(g.CanStand(recruit.Position)&&
                        (recruit.Position-boss.Position).Length<=4.1f&&
                        (recruit.Position-boss.Position).Length>=1.2f&&
                        (recruit.Position-g.Position).Length>=1.25f&&
                        recruit.stun>=.34f,
                        "Legitimate reinforcement has actual locally reachable footing, stays clear of both actor bodies, and receives an honest entry animation window");
                    Check(g.effects.Exists(e=>e.kind=="burst"&&
                        (e.position-recruit.Position).Length<.001f),
                        "A real summoned fighter has a visible original encounter entrance cue");
                    bool createdTwice=(bool)spawn.Invoke(g,new object[]{boss});
                    if(createdTwice)
                        Check((g.enemies[2].Position-recruit.Position).Length>=1.15f,
                            "A subsequent reinforcement cannot appear in a living earlier recruit's body");
                }
                else Check(g.enemies.Count==1,
                    "No legal local footing must skip the recruit without teleporting one elsewhere");
            }
            // A genuinely sealed one-cell chamber must not seek distant
            // fallback tiles. Test this without touching any user's save
            // data or replacing the production authored campaign permanently.
            MapData original=data.maps[0];
            try
            {
                var rows=new string[15];
                for(int y=0;y<15;y++)
                {
                    char[] tiles=new string('#',15).ToCharArray();
                    if(y==7)tiles[7]='.';
                    rows[y]=new string(tiles);
                }
                data.maps[0]=new MapData{
                    id=0,spawnX=7.5f,spawnY=7.5f,rows=rows,
                    objects=new WorldObject[0],
                    decor=new Decoration[0]
                };
                var enclosed=Fresh(data);enclosed.enemies.Clear();
                var boss=new Enemy(99117,3,7.5f,7.5f,1000,true);
                enclosed.enemies.Add(boss);
                int effects=enclosed.effects.Count;
                Check(!(bool)spawn.Invoke(enclosed,new object[]{boss})&&
                    enclosed.enemies.Count==1&&enclosed.effects.Count==effects,
                    "A trapped Boss never teleports its reinforcements across sealed terrain or creates a false summon effect");
            }
            finally { data.maps[0]=original; }
        }

        public static void BossSummonsRejectVisibleButUnreachableDiagonalPocket(Campaign data)
        {
            // An unobstructed thin ray can pass exactly between two corner
            // walls while NO full-footprint actor can traverse a 4-way route.
            // Old summon code checked only a ray and accepted this pocket.
            var reach=typeof(GameCore).GetMethod("BossSummonHasLocalRoute",
                BindingFlags.NonPublic|BindingFlags.Instance);
            var sight=typeof(GameCore).GetMethod("ClearAttackPath",
                BindingFlags.NonPublic|BindingFlags.Instance);
            Check(reach!=null&&sight!=null,
                "Boss summon route and original visible attack ray are distinct production checks");
            MapData original=data.maps[0];
            try
            {
                string[] rows=new string[17];
                for(int y=0;y<rows.Length;y++)
                    rows[y]=new string('#',17);
                char[] fromRow=rows[7].ToCharArray();
                fromRow[7]='.';rows[7]=new string(fromRow);
                char[] toRow=rows[8].ToCharArray();
                toRow[8]='.';rows[8]=new string(toRow);
                data.maps[0]=new MapData{
                    id=0,spawnX=7.5f,spawnY=7.5f,rows=rows,
                    objects=new WorldObject[0],
                    decor=new Decoration[0]
                };
                var sealedPocket=Fresh(data);sealedPocket.enemies.Clear();
                Point left=new Point(7.5f,7.5f);
                Point diagonal=new Point(8.5f,8.5f);
                Check(sealedPocket.CanStand(left)&&
                    sealedPocket.CanStand(diagonal)&&
                    (bool)sight.Invoke(sealedPocket,new object[]{left,diagonal}),
                    "Both cells are physically standable and the diagonal projectile ray genuinely passes between corner walls");
                Check(!(bool)reach.Invoke(sealedPocket,new object[]{left,diagonal}),
                    "A visually open diagonal corner is NOT a navigable enemy corridor and cannot spawn isolated reinforcements");
                var row=rows[8].ToCharArray();row[7]='.';
                rows[8]=new string(row);
                var linked=Fresh(data);linked.enemies.Clear();
                Check((bool)reach.Invoke(linked,new object[]{left,diagonal}),
                    "Opening an actual cardinal foot corridor immediately enables reachable reinforcement sites");
                Check(!(bool)reach.Invoke(linked,new object[]{
                    left,new Point(14.5f,14.5f)}),
                    "A distant map cell cannot be legitimized by searching outside the bounded Boss arena");
            }
            finally {data.maps[0]=original;}
        }

        public static void BossReinforcementWindupTelegraphRetractsOnRealControlAndReleasesWithWave(Campaign data)
        {
            // Real enemy AI, actual player-contact stun and full Boss
            // attack/release timeline. No shortcut that inserts fake units.
            MapData original=data.maps[0];
            try
            {
                var rows=new string[17];
                for(int y=0;y<17;y++)
                {
                    char[] cells=new string('#',17).ToCharArray();
                    if(y>=2&&y<=14)for(int x=2;x<=14;x++)cells[x]='.';
                    rows[y]=new string(cells);
                }
                data.maps[0]=new MapData{
                    id=0,spawnX=10.5f,spawnY=8.5f,rows=rows,
                    objects=new WorldObject[0],decor=new Decoration[0]};
                var g=Fresh(data);g.enemies.Clear();g.invulnerable=100;
                var boss=new Enemy(99136,3,7.5f,8.5f,999,true)
                    {hp=190,cycle=2,timer=0,stagger=94};
                g.enemies.Add(boss);
                var otherEffect=new Effect("number",g.Position,4f,"unrelated")
                    {bossSummonCueOwnerId=99139};
                g.effects.Add(otherEffect);
                g.Tick(.02f,new Point());
                Check(boss.state==1&&g.enemies.Count==1&&
                    g.effects.Exists(e=>e.bossSummonCueOwnerId==boss.id&&
                        e.text.Contains("呼叫增援")),
                    "Upcoming phase-three summon is explicitly readable during normal live Boss attack windup");
                var hit=typeof(GameCore).GetMethod("Hit",
                    BindingFlags.NonPublic|BindingFlags.Instance);
                Check(hit!=null,"Real lethal/contact code available for interrupt test");
                hit.Invoke(g,new object[]{boss,5f,false,(Point?)new Point(1,0),null});
                Check(boss.hp>0&&boss.state==0&&boss.stun>0&&
                    !g.effects.Exists(e=>e.bossSummonCueOwnerId==boss.id)&&
                    g.effects.Contains(otherEffect)&&g.enemies.Count==1,
                    "Genuine combat Break cancels the unspent Boss call, retracts ONLY its own warning and spawns nobody");
                boss.stun=0;boss.hitTime=0;boss.releaseTime=0;boss.timer=0;
                g.Tick(.02f,new Point());
                Check(boss.state==1&&g.effects.Exists(e=>
                    e.bossSummonCueOwnerId==boss.id),
                    "Boss can start a second honest call after recovering from interruption");
                Advance(g,1.35f);
                Check(boss.cycle==3&&g.enemies.Count==2&&
                    !g.effects.Exists(e=>e.bossSummonCueOwnerId==boss.id)&&
                    g.enemies[1].stun>0,
                    "The later genuinely completed Boss release retracts its warning and produces a physical recuperating minion");
                g.PrepareSave();
                var loaded=new GameCore(data,CloneSave(g.save));loaded.Start();
                Check(loaded.save.version==2&&!loaded.effects.Exists(e=>
                    e.bossSummonCueOwnerId!=0),
                    "Transient pending summoning warnings never leak into original v2 saves");
            }
            finally{data.maps[0]=original;}
        }

        public static void LateBossCyclesSpawnVisibleMeleeAndRangedSupportWithoutActorOverlap(Campaign data)
        {
            var summon=typeof(GameCore).GetMethod("TrySpawnBossReinforcement",
                BindingFlags.NonPublic|BindingFlags.Instance);
            Check(summon!=null,"Production Boss support planner available");
            foreach(int cycle in new[]{3,6,9})
            {
                var g=Quiet(data);g.enemies.Clear();g.invulnerable=999;
                // Choose genuine open stage flooring with a legal nearby
                // spawn, avoiding a constructed/fake arena collision.
                Point open=new Point();bool found=false;
                for(int y=4;y<g.Map.rows.Length-4&&!found;y++)
                for(int x=4;x<g.Map.rows[y].Length-4&&!found;x++)
                {
                    Point p=new Point(x+.5f,y+.5f);
                    Point local=p+new Point(-2,1);
                    if(g.CanStand(p)&&g.CanStand(local)&&
                        (g.Position-local).Length>1.4f &&
                        (g.Position-p).Length>1.4f)
                    {open=p;found=true;}
                }
                Check(found,"Real campaign region includes room for a fair physical Boss support wave");
                var boss=new Enemy(99400+cycle,3,open.x,open.y,999,true)
                    {hp=100,cycle=cycle};
                g.enemies.Add(boss);
                Check((bool)summon.Invoke(g,new object[]{boss})&&g.enemies.Count==2,
                    "Each genuine later cycle can create one local support actor in a real opened arena");
                Enemy recruit=g.enemies[1];
                int expectedKind=cycle==6?1:0;
                Check(recruit.kind==expectedKind&&recruit.stun>=.34f&&
                    g.CanStand(recruit.Position),
                    "First/third phase reinforcement cycles use melee troops; later middle cycle uses real bow AI with an entry windup");
                Point face=(g.Position-recruit.Position).Normalized;
                Check(recruit.facing.x*face.x+recruit.facing.y*face.y>.99f,
                    "Every new support actor faces the real player's latest position on the first physical animation frame");
                // Normal AI is still authoritative once the summoning pause
                // expires. Do not short-circuit its real windup / timing.
                float before=recruit.stun;g.Tick(.02f,new Point());
                Check(recruit.stun<before&&recruit.stun>0,
                    "Actual AI consumes the locally spawned soldier's safety/entrance recovery over real frame time");
            }
        }

        public static void LateBossArcherActuallyPerformsTimedRealArrowCombat(Campaign data)
        {
            // Deterministic physical, unobstructed tile arena; no mock arrows,
            // bypassed enemy AI or fake damage/effect injection.
            MapData original=data.maps[0];
            try
            {
                var rows=new string[17];
                for(int y=0;y<rows.Length;y++)
                {
                    char[] tiles=new string('#',17).ToCharArray();
                    if(y>=2&&y<=14)for(int x=2;x<=14;x++)tiles[x]='.';
                    rows[y]=new string(tiles);
                }
                data.maps[0]=new MapData{
                    id=0,spawnX=10.5f,spawnY=8.5f,rows=rows,
                    objects=new WorldObject[0],
                    decor=new Decoration[0]
                };
                var g=Fresh(data);g.enemies.Clear();g.invulnerable=100;
                var boss=new Enemy(99126,3,7.5f,8.5f,999,true)
                    {hp=100,cycle=6,stun=100};
                g.enemies.Add(boss);
                var summon=typeof(GameCore).GetMethod("TrySpawnBossReinforcement",
                    BindingFlags.NonPublic|BindingFlags.Instance);
                Check((bool)summon.Invoke(g,new object[]{boss})&&
                    g.enemies.Count==2&&g.enemies[1].kind==1,
                    "Real late-phase Boss uses its production six-cycle summon to deploy an actual archer in the current arena");
                Enemy archer=g.enemies[1];
                Check(g.CanStand(archer.Position)&&
                    (archer.Position-g.Position).Length<7f&&
                    archer.stun>=.34f,
                    "Summoned archer actually begins on legal visible nearby terrain with a genuine entrance recovery");
                int beforeShots=g.projectiles.Count;
                g.Tick(.02f,new Point());
                Check(archer.stun>0 && g.projectiles.Count==beforeShots,
                    "Summoned archer cannot immediately fire or create a fake projectile during its entry frames");
                bool fired=false;
                for(int frame=0;frame<270&&!fired;frame++)
                {
                    g.Tick(.02f,new Point());
                    fired=g.projectiles.Exists(p=>!p.friendly&&
                        p.visual=="enemyArrow"&&p.source=="shot"&&
                        p.velocity.Length>1f);
                }
                Check(fired,
                    "The spawned ranged support must travel through normal patrol AI, real windup and live Fire() before an actual enemy arrow exists");
                Check(g.enemies.Contains(archer)&&!g.Dying&&g.save.version==2,
                    "The real firing test never manufactures a Boss defeat, removes old actors or alters save format");
            }
            finally { data.maps[0]=original; }
        }

        public static void BossSupportCannotSpawnOnTheLivingCompanion(Campaign data)
        {
            MapData original=data.maps[0];
            try
            {
                var rows=new string[17];
                for(int y=0;y<rows.Length;y++)
                {
                    char[] tiles=new string('#',17).ToCharArray();
                    if(y>=2&&y<=14)for(int x=2;x<=14;x++)tiles[x]='.';
                    rows[y]=new string(tiles);
                }
                data.maps[0]=new MapData{
                    id=0,spawnX=10.5f,spawnY=8.5f,rows=rows,
                    objects=new WorldObject[0],
                    decor=new Decoration[0]
                };
                var g=Fresh(data);g.enemies.Clear();
                g.save.flags.Add("companion_1");g.SelectCompanion(1);
                Check(g.save.companion==1,"Physical Boss reinforcements test uses a real selected party companion");
                Point intended=new Point(5.5f,9.5f);
                g.companionPosition=intended;
                var boss=new Enemy(99271,3,7.5f,8.5f,999,true)
                    {hp=100,cycle=3};
                g.enemies.Add(boss);
                var summon=typeof(GameCore).GetMethod("TrySpawnBossReinforcement",
                    BindingFlags.NonPublic|BindingFlags.Instance);
                Check((bool)summon.Invoke(g,new object[]{boss})&&g.enemies.Count==2,
                    "The Boss's preferred occupied reinforcement tile still allows an alternate genuine local spawn");
                Enemy recruit=g.enemies[1];
                Check((recruit.Position-intended).Length>=1.2f&&
                    (recruit.Position-g.Position).Length>=1.25f &&
                    g.CanStand(recruit.Position),
                    "Summoned support must not appear inside companion, hero or an obstructed footprint");
                Check(recruit.kind==0,
                    "Original third-cycle reinforcements keep their actual melee identity when avoiding party characters");
            }
            finally { data.maps[0]=original; }
        }

        public static void TideTransitionPrecedesAllAttackContacts(Campaign data)
        {
            var g=Unlocked(data);g.save.totalTime=21.995f;g.Enter(5);g.enemies.Clear();g.invulnerable=100;
            Point shoal=new Point();bool found=false;
            for(int y=2;y<g.Map.rows.Length-2&&!found;y++)for(int x=2;x<g.Map.rows[y].Length-2&&!found;x++)
            {Point p=new Point(x+.5f,y+.5f);if(g.Map.rows[y][x]=='%'&&g.CanStand(p)){shoal=p;found=true;}}
            Check(found&&g.LowTide,"The production harbor must contain an accessible low-tide shoal");
            g.save.x=shoal.x;g.save.y=shoal.y;g.save.companion=1;g.companionPosition=shoal;
            g.companionAction=new ActorAction{kind="attack",weapon=1,direction=new Point(1,0),duration=.52f,contact=.2f,elapsed=.195f,damage=99};
            var enemy=new Enemy(1841,1,shoal.x,shoal.y,99){state=1,timer=.001f,windupDuration=.65f,facing=new Point(1,0)};g.enemies.Add(enemy);
            g.Skill(new Point(1,0));g.playerAction.elapsed=g.playerAction.contact-.005f;
            g.Tick(.01f,new Point());
            Check(!g.LowTide&&g.CanStand(g.Position)&&g.CanStand(enemy.Position)&&g.CanStand(g.companionPosition),"The tide recovers all complete actor footprints before simulation advances");
            Check(g.playerAction==null&&g.companionAction==null&&enemy.state==0&&g.projectiles.Count==0,"Retreat cancels pending releases rather than firing from either old water or the new bank");
            Check(g.walkDistance==0&&g.footContacts==0&&!g.ActuallyMoving,"A forced environmental retreat cannot synthesize walking contacts or a sliding pose");
            g=Quiet(data);g.invulnerable=0;g.save.reducedMotion=false;
            g.hazards.Add(new Hazard{position=g.Position,radius=1.35f,timer=.001f,damage=3,kind="tide"});g.Tick(.01f,new Point());
            Check(Math.Abs(g.playerRecoil.Length-1)<.001f&&g.impactShake==0,"Exact-centered regional hits have coherent recoil without borrowing Boss-level screen shake");
        }
        public static void RegionalHazardsKeepActualFootprint(Campaign data)
        {
            foreach (string kind in new[] { "tide", "glass", "rift" })
            foreach (float radius in new[] { 1.1f, 1.35f, 2f })
            foreach (bool inside in new[] { true, false })
            {
                var g = Quiet(data); g.invulnerable = 0;
                Point center = g.Position + new Point(inside ? radius * .8f : radius + .1f, 0);
                var h = new Hazard { position = center, radius = radius, timer = .015f, damage = 3, kind = kind };
                g.hazards.Add(h); float health = g.save.health;
                g.Tick(.01f, new Point()); Check(g.effects.Count == 0 && g.save.health == health, "A warning cannot hit or show an impact early");
                g.Tick(.01f, new Point()); Effect impact = g.effects.Find(e => e.kind == "danger");
                Check(g.hazards.Count == 0 && impact != null && impact.radius == radius && (impact.position-center).Length < .001f, "Danger release retains the exact telegraphed center and radius");
                Check(impact.variant == (kind == "tide" ? 5 : kind == "glass" ? 6 : 8), "Tide, glass and rift impacts retain their authored regional material");
                Check((g.save.health < health) == inside, "Visual radius and damage radius agree at both sides of the edge");
                if (inside) Check(g.playerRecoil.x < -.99f, "A hazard pushes away from its actual center rather than the cursor");
            }
        }
        public static void LethalContactStopsRemainingSimulation(Campaign data)
        {
            var g = Quiet(data); g.invulnerable = 0; g.save.health = 1;
            var killer = new Enemy(1810,2,13,14,999) { state=1,timer=.001f,windupDuration=.65f,target=g.Position };
            var next = new Enemy(1811,1,15,14,999) { state=1,timer=.001f,windupDuration=.65f,target=g.Position };
            var victim = new Enemy(1812,0,17,14,1) { timer=999 };
            g.enemies.Add(killer); g.enemies.Add(next); g.enemies.Add(victim);
            var friendly = new Projectile { position=victim.Position, friendly=true,visual="arrow",damage=999 }; g.projectiles.Add(friendly);
            var hazard = new Hazard { position=g.Position,radius=2,timer=1,kind="rift",damage=3 }; g.hazards.Add(hazard);
            g.Tick(.01f,new Point());
            Check(g.Dying && g.save.health == 0 && next.state == 1 && next.timer == .001f, "Lethal enemy contact cannot let later enemies fire during the same frame");
            Check(friendly.age == 0 && victim.hp == 1 && hazard.timer == 1, "The projectile and environmental passes cannot run after death begins");
            g = Quiet(data); g.invulnerable=0;g.save.health=1;
            victim = new Enemy(1813,0,17,14,1) { stun=99 };g.enemies.Add(victim);
            g.projectiles.Add(new Projectile {position=g.Position,damage=999,visual="enemyOrb"});
            friendly = new Projectile {position=victim.Position,friendly=true,damage=999,visual="arrow"};g.projectiles.Add(friendly);
            g.Tick(.01f,new Point());Check(g.Dying && victim.hp==1 && friendly.age==0, "A lethal first projectile cannot be followed by a posthumous attack in the same pass");
            g = Quiet(data);g.invulnerable=0;g.save.health=1;
            hazard = new Hazard {position=g.Position,radius=2,timer=.01f,damage=999,kind="rift"};g.hazards.Add(hazard);
            var later = new Hazard {position=g.Position,radius=2,timer=.01f,damage=999,kind="glass"};g.hazards.Add(later);
            g.Tick(.02f,new Point());Check(g.Dying && g.hazards.Count==1 && later.timer==.01f, "Only the hazard that starts death releases; later hazards retain their preparation");
            g.PrepareSave();var resumed=new GameCore(data,CloneSave(g.save));Check(resumed.save.room==0 && resumed.save.health==resumed.save.maxHealth && resumed.save.deaths==1,"Immediate quit after lethal contact still resumes once, safely");
            g=Quiet(data);g.invulnerable=0;g.save.health=1;
            var boss=new Enemy(1842,3,12,14,999,true){hp=100,state=1,timer=.001f,cycle=2,target=g.Position};g.enemies.Add(boss);g.Tick(.01f,new Point());
            Check(g.Dying&&g.projectiles.Count==0&&g.enemies.Count==1&&boss.cycle==2,"A lethal Boss contact stops its own remaining salvo, hazards and reinforcement release in the same frame");
            g=Quiet(data);g.save.room=4;g.save.flags.Add("boss_4");
            var target=new Enemy(1843,0,13,14,99){stun=99};g.enemies.Add(target);g.Attack(new Point(1,0));g.playerAction.elapsed=g.playerAction.contact-.005f;
            float actionTime=g.playerAction.elapsed,worldTime=g.save.totalTime;
            typeof(GameCore).GetField("finaleDelay",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(g,.001f);
            g.Tick(.01f,new Point(1,0));
            Check(g.mode=="dialog"&&target.hp==99&&g.playerAction.elapsed==actionTime&&g.save.totalTime==worldTime,"Opening the finale during the visual clock pauses all remaining world/contact work in that same frame");
        }
        public static void PracticeExitCancelsAllPendingPresentation(Campaign data)
        {
            for(int profession=0;profession<3;profession++)
            {
                var g=Fresh(data,profession);Point home=g.Position;g.StartPractice();g.practiceStage=2;
                g.enemies.Clear();var target=new Enemy(1820,0,16,13,999){stun=99};g.enemies.Add(target);
                g.Skill(new Point(1,0));g.projectiles.Add(new Projectile{position=target.Position,friendly=true,technique=true,visual="arrow",damage=1});
                g.Tick(.01f,new Point());
                Check(!g.practicing && (g.Position-home).Length<.001f && g.playerAction==null && g.companionAction==null,"Completing practice restores position and cancels the unfinished action for every class");
                Check(g.projectiles.Count==0 && g.effects.Count==0 && !g.ActuallyMoving && g.walkDistance==0,"Retired practice shots cannot leave remote impacts, sliding poses or false walking distance");
                int coins=g.save.coins;Advance(g,1);Check(g.projectiles.Count==0 && g.save.coins==coins,"A cancelled practice action cannot release again after returning");
                g.StartPractice();g.Attack(new Point(1,0));g.EndPractice();Check(g.playerAction==null && g.projectiles.Count==0,"Manual practice exit obeys the same cancellation rule");
            }
        }
        public static void SharedPathWorkspaceKeepsOwnedRoutes(Campaign data)
        {
            var g=Quiet(data);g.save.x=9.5f;g.save.y=8.8f;Point target=new Point(9.5f,10.2f);
            Check(g.Blocked(9.5f,9.5f),"Path regression crosses an actual forest wall");
            var route=g.NavigationPath(target);Check(route.Count>0,"A real detour exists around the wall");Point[] retained=route.ToArray();
            FieldInfo parentField=typeof(GameCore).GetField("pathParents",BindingFlags.Instance|BindingFlags.NonPublic),queueField=typeof(GameCore).GetField("pathQueue",BindingFlags.Instance|BindingFlags.NonPublic);
            object parents=parentField.GetValue(g),queue=queueField.GetValue(g);
            for(int i=0;i<500;i++)
            {
                Point direction=g.DirectionTo(g.Position,target);Check(direction.Length>.99f,"Pursuit still obtains a real detour direction");
                if(i%10==0)g.NavigationPath(new Point(20,14));
                Check(ReferenceEquals(parents,parentField.GetValue(g)) && ReferenceEquals(queue,queueField.GetValue(g)),"Repeated obstructed pursuit reuses bounded search arrays");
            }
            Check(route.Count==retained.Length,"An enemy search cannot overwrite the HUD's owned route");
            for(int i=0;i<route.Count;i++)Check((route[i]-retained[i]).Length==0 && g.CanStand(route[i]),"Owned route coordinates and footprint remain valid after interleaved searches");
        }
        public static void JournalRecordsRefreshOnlyOnStateChanges(Campaign data)
        {
            var g=Quiet(data);g.Tick(.01f,new Point());
            FieldInfo record=typeof(GameCore).GetField("previousGuide",BindingFlags.NonPublic|BindingFlags.Instance);object signature=record.GetValue(g);int moments=g.save.moments.Count;
            for(int i=0;i<300;i++)g.Tick(.02f,new Point());
            Check(ReferenceEquals(signature,record.GetValue(g)) && g.save.moments.Count==moments,"Unchanged exploration cannot repeatedly rebuild bilingual journal signatures");
            g.save.stage=1;g.Tick(.01f,new Point());Check(g.save.moments.Count==moments+1 && g.save.moments[g.save.moments.Count-1].zh!=g.save.moments[g.save.moments.Count-1].en,"A chapter change records its two real language descriptions immediately");
            moments=g.save.moments.Count;g.save.english=true;g.Tick(.01f,new Point());Check(g.save.moments.Count==moments,"Switching language cannot fabricate a new quest update");
            g.TrackQuest("ally1");g.Tick(.01f,new Point());g.save.inventory[2]=6;g.revision++;g.Tick(.01f,new Point());Check(g.CurrentGuide.room==0 && g.save.moments[g.save.moments.Count-1].en.Contains("Deliver"),"A real supply change still refreshes the next step and journal");
        }
        public static void HarborCampRestRecoversCompanionFromRisingTideImmediately(Campaign data)
        {
            var g = Unlocked(data);
            g.save.totalTime = 5f;
            g.save.projects[1] = 1;
            g.Enter(5); g.enemies.Clear();
            g.save.tideHeldUntil = 0;
            Point shoal = new Point(); bool found = false;
            for (int y = 2; y < g.Map.rows.Length - 2 && !found; y++)
            for (int x = 2; x < g.Map.rows[y].Length - 2 && !found; x++)
            {
                Point candidate = new Point(x + .5f, y + .5f);
                if (g.Map.rows[y][x] != '%' || !g.CanStand(candidate)) continue;
                shoal = candidate; found = true;
            }
            Check(found && g.LowTide,
                "A legal harbor camp begins at low tide with an actual reachable shoal");
            var fire = Object(g, "campfire");
            g.save.x = fire.x; g.save.y = fire.y;
            g.save.companion = 0;
            g.companionPosition = shoal;
            g.companionAction = new ActorAction { kind = "attack",
                duration = .5f, contact = .2f, elapsed = .05f };
            Check(g.CanStand(g.companionPosition),
                "Active partner really stands on land that will be submerged during twenty-minute rest");
            Action(g, fire.kind, "fieldrest");
            Check(Math.Abs(g.save.totalTime - 25f) < .001f &&
                !g.LowTide && g.CanStand(g.Position) &&
                g.CanStand(g.companionPosition) &&
                g.companionAction == null,
                "Real camp rest advances the full twenty minutes and recovers the companion out of newly flooded tiles BEFORE another world Tick");
            g.PrepareSave();
            var reloaded = new GameCore(data, CloneSave(g.save));
            Check(reloaded.save.room == 5 && !reloaded.LowTide &&
                reloaded.CanStand(reloaded.Position),
                "Rest and tidal collision state remain legal when saved immediately and reopened");
        }

        public static void RisingTideWithdrawsUnspentEnemyWarningBeforeNextAttack(Campaign data)
        {
            var g = Unlocked(data);
            g.save.totalTime = 21.92f;
            g.Enter(5); g.enemies.Clear(); g.effects.Clear();
            g.invulnerable = 100;
            Point shoal = new Point(); bool found = false;
            for (int y = 2; y < g.Map.rows.Length - 2 && !found; y++)
            for (int x = 2; x < g.Map.rows[y].Length - 2 && !found; x++)
            {
                Point p = new Point(x + .5f, y + .5f);
                if (g.Map.rows[y][x] == '%' && g.CanStand(p))
                { shoal = p; found = true; }
            }
            Check(found && g.LowTide,
                "A real accessible tidal shoal exists before the attack windup");
            g.save.x = shoal.x; g.save.y = shoal.y;
            var raider = new Enemy(18490, 2, shoal.x, shoal.y, 120);
            raider.timer = 0;
            g.enemies.Add(raider);
            g.Tick(.005f, new Point());
            Effect warning = g.effects.Find(e => e.kind == "danger" &&
                (e.position - raider.target).Length < .01f);
            Check(raider.state == 1 && warning != null,
                "Actual heavy enemy arms its contact-linked warning while low-tide ground is still legal");
            g.save.totalTime = 21.995f;
            g.Tick(.01f, new Point());
            Check(!g.LowTide && g.CanStand(raider.Position) &&
                g.CanStand(g.Position) && raider.state == 0 &&
                !g.effects.Contains(warning),
                "Tide relocation retracts the interrupted warning at the same simulation boundary as the enemy movement");
            Check(!raider.targetingWayfarer && !raider.targetingReliefCivilian &&
                raider.timer >= .3f,
                "Environmentally aborted attackers have no stale civilian target and receive readable recovery");
        }

    }
}
