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

    }
}
