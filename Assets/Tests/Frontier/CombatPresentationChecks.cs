using System;
namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        public static void ReliefCivilianInteractionsNeedPhysicalOpenLane(Campaign data)
        {
            MapData previous=data.maps[1];
            try
            {
                var rows=new string[25];
                for(int y=0;y<25;y++)
                {
                    char[] tiles=new string('#',25).ToCharArray();
                    if(y>=2&&y<=22)for(int x=2;x<=22;x++)tiles[x]='.';
                    rows[y]=new string(tiles);
                }
                char[] wall=rows[10].ToCharArray();
                wall[11]='#';rows[10]=new string(wall);
                data.maps[1]=new MapData{
                    id=1,spawnX=10.5f,spawnY=10.5f,rows=rows,
                    objects=new WorldObject[0],decor=new Decoration[0]};
                foreach(int wave in new[]{1,3})
                {
                    var g=Fresh(data,0);g.enemies.Clear();
                    g.save.room=1;g.save.x=10.77f;g.save.y=10.5f;
                    g.save.reducedMotion=true;
                    g.reliefMissionRoom=1;g.reliefMissionWave=wave;
                    g.reliefFollowerPosition=new Point(12.23f,10.5f);
                    g.reliefFollowerSafety=65f;g.reliefMissionTime=90f;
                    Check(g.CanStand(g.Position)&&
                        g.CanStand(g.reliefFollowerPosition)&&
                        g.Blocked(11.5f,10.5f),
                        "Real relief guard and rescued escort both stand on legal ground, but an intervening solid gate prevents conversation");
                    g.Interact();
                    Check(g.mode=="world"&&g.conversation==null,
                        "E cannot open wave-one/two defense or wave-three escort dialogue through a real wall");
                    wall[11]='.';rows[10]=new string(wall);
                    g.Interact();
                    Check(g.mode=="dialog"&&g.conversation!=null,
                        "Opening the same gate immediately restores genuine nearby field-guard or survivor dialogue");
                    g.Close();
                    wall[11]='#';rows[10]=new string(wall);
                }
            }
            finally{data.maps[1]=previous;}
        }

        public static void AuthoredWallEmbeddedPropsRemainInteractableFromTheirNearSide(Campaign data)
        {
            // Frostwood and the port's chart are authored within blocking
            // scenic tiles, and cannot be deleted or made into ghost props
            // merely to prevent an E-key interaction through distant cover.
            Check(data.maps[7].objects!=null&&
                Array.Exists(data.maps[7].objects,o=>o.id=="frostwood")&&
                data.maps[7].rows[18][6]=='#'&&
                data.maps[5].objects!=null&&
                Array.Exists(data.maps[5].objects,o=>o.id=="portlog")&&
                data.maps[5].rows[18][6]=='#',
                "Real story content deliberately owns interactables mounted within blocked environment cells");
            MapData previous=data.maps[0];
            try
            {
                var rows=new string[25];
                for(int y=0;y<25;y++)
                {
                    char[] tiles=new string('#',25).ToCharArray();
                    if(y>=2&&y<=22)
                        for(int x=2;x<=22;x++)tiles[x]='.';
                    rows[y]=new string(tiles);
                }
                var tile=rows[10].ToCharArray();
                tile[12]='#';rows[10]=new string(tile);
                var mounted=new WorldObject{
                    id="mounted",kind="sign",name="Wall-mounted map",
                    en="Wall-mounted map",x=12.5f,y=10.5f};
                data.maps[0]=new MapData {
                    id=0,spawnX=11.5f,spawnY=10.5f,
                    rows=rows,objects=new[]{mounted},
                    decor=new Decoration[0]};
                var g=Fresh(data,0);g.enemies.Clear();
                g.save.x=11.5f;g.save.y=10.5f;
                Check(g.CanStand(g.Position)&&
                    g.Blocked(mounted.x,mounted.y)&&
                    g.Nearest()==mounted,
                    "An actual wall-mounted prop remains selectable from a valid immediately adjacent interaction foot");
                // A wall BETWEEN the hero and the same object must still
                // block the action; only the object's own endpoint can be
                // ignored within the authored short contact margin.
                g.save.x=10.5f;
                tile=rows[10].ToCharArray();
                tile[11]='#';rows[10]=new string(tile);
                Check(g.CanStand(g.Position)&&
                    g.Nearest()==null,
                    "Two consecutive wall cells are not bypassed by the legitimate contact-radius exception");
            }
            finally{data.maps[0]=previous;}
        }

        public static void RealInteractionSelectionRejectsObjectsBehindSolidWalls(Campaign data)
        {
            MapData previous=data.maps[0];
            try
            {
                var rows=new string[25];
                for(int y=0;y<25;y++)
                {
                    char[] tiles=new string('#',25).ToCharArray();
                    if(y>=2&&y<=22)for(int x=2;x<=22;x++)tiles[x]='.';
                    rows[y]=new string(tiles);
                }
                char[] gate=rows[10].ToCharArray();
                gate[11]='#';rows[10]=new string(gate);
                var blocked=new WorldObject {
                    id="blocked",kind="sign",name="Sealed archive sign",
                    en="Sealed archive sign",x=12.23f,y=10.5f};
                var accessible=new WorldObject {
                    id="accessible",kind="sign",name="Open path sign",
                    en="Open path sign",x=10.5f,y=12.24f};
                data.maps[0]=new MapData {
                    id=0,spawnX=10.5f,spawnY=10.5f,rows=rows,
                    objects=new[]{blocked,accessible},
                    decor=new Decoration[0]};
                var g=Fresh(data,0);
                g.enemies.Clear();
                g.save.x=10.5f;g.save.y=10.5f;
                Check(g.CanStand(g.Position)&&
                    g.CanStand(new Point(blocked.x,blocked.y))&&
                    g.CanStand(new Point(accessible.x,accessible.y))&&
                    g.Blocked(11.5f,10.5f),
                    "Both physical objects and the hero stand on valid floor, but a real gate separates the closer archive sign");
                Check(g.Nearest()==accessible,
                    "The live E-key target chooser rejects the closer object behind a wall and selects the farther truly reachable object");
                gate[11]='.';rows[10]=new string(gate);
                Check(g.Nearest()==blocked,
                    "Opening the same wall immediately restores selection of the closest now-reachable story object");
                gate[11]='#';rows[10]=new string(gate);
                data.maps[0].objects=new[]{blocked};
                Check(g.Nearest()==null,
                    "A solid wall cannot be bypassed just because the only nearby interactable is behind it");
                Check(g.save.version==2,
                    "World-object selection uses no new persistent field or altered SaveData schema");
            }
            finally{data.maps[0]=previous;}
        }

        public static void SealedHeroFootCannotSpendPhantomDodgeOrEarnCounter(Campaign data)
        {
            MapData previous=data.maps[0];
            try
            {
                var rows=new string[25];
                for(int y=0;y<25;y++)
                {
                    char[] row=new string('#',25).ToCharArray();
                    if(y>=2&&y<=22)
                        for(int x=2;x<=22;x++)row[x]='.';
                    rows[y]=new string(row);
                }
                data.maps[0]=new MapData{
                    id=0,spawnX=10.5f,spawnY=10.5f,rows=rows,
                    objects=new WorldObject[0],decor=new Decoration[0]};
                var g=Fresh(data,0);g.enemies.Clear();
                g.save.x=10.5f;g.save.y=10.5f;
                g.save.energy=100f;g.save.reducedMotion=true;
                g.invulnerable=0;
                Point before=g.Position;
                Check(g.CanStand(before),
                    "The hero legitimately stands in the open before the tide/gate collision changes");
                char[] sealedTile=rows[10].ToCharArray();
                sealedTile[10]='#';rows[10]=new string(sealedTile);
                Check(!g.CanStand(before),
                    "Dynamic solid world terrain truly seals the full player footprint before dash input");
                g.Dash(new Point(1,0));
                Check(g.dashTime<=0&&g.dashCooldown<=0&&
                    g.invulnerable<=0&&g.save.energy==100f&&
                    !g.perfectDodgeArmed&&
                    !g.effects.Exists(e=>e.kind=="dash")&&
                    (g.Position-before).Length<.001f,
                    "An impossible dodge neither spends real stamina nor grants invisible i-frames, a false counter or visual movement");
                sealedTile[10]='.';rows[10]=new string(sealedTile);
                Check(g.CanStand(before),
                    "The same scene physically reopens the hero's grounded dash lane");
                g.Dash(new Point(1,0));
                Check(g.dashTime>.15f&&g.dashCooldown>.6f&&
                    g.invulnerable>.3f&&g.save.energy==80f&&
                    g.perfectDodgeArmed&&
                    g.effects.Exists(e=>e.kind=="dash"),
                    "A legal replacement dash retains its real stamina cost, recovery and invulnerability only on actual startup");
                Advance(g,.20f);
                Check(g.Position.x>before.x+2.4f&&
                    Math.Abs(g.Position.y-before.y)<.01f&&
                    g.dashTime<=0,
                    "Restored ground permits the original physically swept dash distance without inventing footfalls");
            }
            finally{data.maps[0]=previous;}
        }

        public static void DynamicTerrainCannotReleaseHeroAttacksFromBuriedRoot(Campaign data)
        {
            MapData previous=data.maps[0];
            try
            {
                var rows=new string[25];
                for(int y=0;y<25;y++)
                {
                    char[] cells=new string('#',25).ToCharArray();
                    if(y>=2&&y<=22)for(int x=2;x<=22;x++)cells[x]='.';
                    rows[y]=new string(cells);
                }
                data.maps[0]=new MapData{
                    id=0,spawnX=10.5f,spawnY=10.5f,rows=rows,
                    objects=new WorldObject[0],decor=new Decoration[0]};
                foreach(int weapon in new[]{0,1,2})
                {
                    var g=Fresh(data,weapon);g.enemies.Clear();
                    g.save.x=10.5f;g.save.y=10.5f;
                    g.save.reducedMotion=true;g.save.energy=100;
                    g.riposteTime=4f;
                    var foe=new Enemy(99301+weapon,0,12f,10.5f,10000f)
                        {stun=100f,timer=1000f,staggerRecovery=100f};
                    g.enemies.Add(foe);
                    g.SetAimTarget(foe.Position);
                    Check(g.CanStand(g.Position)&&g.CanStand(foe.Position),
                        "All three equipped-weapon attack trials begin with valid physics and a real nearby foe");
                    g.Attack(new Point(1,0));
                    ActorAction abandoned=g.playerAction;
                    Check(abandoned!=null&&!abandoned.released&&abandoned.riposteArmed,
                        "A player LMB action has a genuine pending windup and earned counter");
                    char[] blocked=rows[10].ToCharArray();
                    blocked[10]='#';rows[10]=new string(blocked);
                    Check(!g.CanStand(g.Position),"Dynamic collision buries the player only AFTER a legal attack starts");
                    Advance(g,abandoned.contact+.02f);
                    Check(abandoned.released&&abandoned.blockedAtContact&&
                        !abandoned.riposteArmed&&foe.hp==foe.maxHp&&g.projectiles.Count==0&&
                        !g.effects.Exists(f=>f.kind=="release"||f.kind=="slash")&&
                        g.effects.Exists(f=>f.kind=="projectileImpact")&&
                        g.riposteTime>0,
                        "An invalid hero foot cancels sword/bow/staff actual contact and its fake art but preserves the saved earned counter");
                    blocked[10]='.';rows[10]=new string(blocked);
                    Advance(g,.75f);
                    g.SetAimTarget(foe.Position);
                    g.Attack(new Point(1,0));
                    ActorAction retry=g.playerAction;
                    Check(retry!=null&&retry!=abandoned&&!retry.released&&
                        retry.riposteArmed,"Reopening the floor allows a new real attack with the unspent counter");
                    Advance(g,.7f);
                    Check(retry.released&&g.riposteTime<=0&&foe.hp<foe.maxHp,
                        "The same weapon subsequently lands actual empowered combat damage after the floor opens");
                }
            }
            finally{data.maps[0]=previous;}
        }

        public static void DynamicTerrainCannotReleaseHeroSkillsFromBuriedRoot(Campaign data)
        {
            MapData previous=data.maps[0];
            try
            {
                var rows=new string[25];
                for(int y=0;y<25;y++)
                {
                    char[] cells=new string('#',25).ToCharArray();
                    if(y>=2&&y<=22)for(int x=2;x<=22;x++)cells[x]='.';
                    rows[y]=new string(cells);
                }
                data.maps[0]=new MapData{
                    id=0,spawnX=10.5f,spawnY=10.5f,rows=rows,
                    objects=new WorldObject[0],decor=new Decoration[0]};
                foreach(int profession in new[]{0,1,2})
                {
                    var g=Fresh(data,profession);g.enemies.Clear();
                    g.save.x=10.5f;g.save.y=10.5f;
                    g.save.reducedMotion=true;g.save.energy=100;
                    var foe=new Enemy(99311+profession,0,12f,10.5f,10000f)
                        {stun=100f,timer=1000f,staggerRecovery=100f};
                    g.enemies.Add(foe);
                    g.SetAimTarget(foe.Position);
                    g.Skill(new Point(1,0));
                    ActorAction cast=g.playerAction;
                    Check(cast!=null&&cast.kind=="skill"&&!cast.released,
                        "Each real profession Q begins its authored delayed release");
                    char[] tile=rows[10].ToCharArray();
                    tile[10]='#';rows[10]=new string(tile);
                    Advance(g,cast.contact+.02f);
                    Check(cast.released&&cast.blockedAtContact&&
                        foe.hp==foe.maxHp&&
                        g.projectiles.Count==0&&g.skillCooldown>0&&
                        g.effects.Exists(f=>f.kind=="projectileImpact")&&
                        !g.effects.Exists(f=>f.kind=="burst"||f.kind=="release"||f.kind=="frost"),
                        "All three Q disciplines refuse phantom damage or area effects when dynamic terrain buries the caster at contact");
                    tile[10]='.';rows[10]=new string(tile);
                    Advance(g,3.7f);
                    g.save.energy=100;
                    g.SetAimTarget(foe.Position);
                    g.Skill(new Point(1,0));
                    ActorAction retry=g.playerAction;
                    Check(retry!=null&&retry!=cast&&!retry.released,
                        "After legitimate Q cooldown the player can recast from restored legal ground");
                    Advance(g,.65f);
                    Check(retry.released&&foe.hp<foe.maxHp,
                        "A reopened player Q deals genuine damage on its own profession's production release path");
                }
            }
            finally{data.maps[0]=previous;}
        }

        public static void HeavyDodgeCounterRequiresRealUnblockedWindupLane(Campaign data)
        {
            // Preserve the existing heavy warning/release state machine.
            // A gate can seal the lane after a valid ground-ring warning
            // but before a player dashes outside the actual 1.5-tile
            // heavy contact radius. That is NOT a real dodge if the enemy
            // cannot have struck the original foot through solid world.
            MapData previous=data.maps[0];
            try
            {
                string[] rows=new string[25];
                for(int y=0;y<25;y++)
                {
                    char[] tiles=new string('#',25).ToCharArray();
                    if(y>=2&&y<=22)
                        for(int x=2;x<=22;x++)tiles[x]='.';
                    rows[y]=new string(tiles);
                }
                data.maps[0]=new MapData{
                    id=0,spawnX=10.5f,spawnY=10.5f,rows=rows,
                    objects=new WorldObject[0],decor=new Decoration[0]};
                foreach(bool sealLane in new[]{true,false})
                {
                    char[] tile=rows[11].ToCharArray();
                    tile[10]='.';rows[11]=new string(tile);
                    var g=Fresh(data,0);g.enemies.Clear();
                    g.save.x=10.5f;g.save.y=10.5f;
                    g.save.reducedMotion=true;g.save.energy=100f;
                    var heavy=new Enemy(99521,2,10.5f,12.5f,10000f)
                        {timer=0f};
                    g.enemies.Add(heavy);
                    g.Tick(.02f,new Point());
                    Check(heavy.state==1&&
                        g.effects.Exists(f=>f.kind=="danger")&&
                        (heavy.target-g.Position).Length<.001f,
                        "The real heavy first locks a legitimate open-lane world target and publishes a physical danger ring");
                    // Compress this already-authored windup solely within
                    // the test, keeping a real AI release while the player's
                    // 300ms dodge-escape clock is still eligible.
                    heavy.timer=.22f;heavy.windupDuration=.22f;
                    if(sealLane)
                    {
                        tile=rows[11].ToCharArray();
                        tile[10]='#';rows[11]=new string(tile);
                    }
                    Check(g.CanStand(g.Position)&&g.CanStand(heavy.Position)&&
                        (sealLane?g.Blocked(10.5f,11.5f):
                            !g.Blocked(10.5f,11.5f)),
                        "Dynamic cover changes only the attack's live path while the player and actual hostile feet remain legal");
                    Point origin=g.Position;
                    g.Dash(new Point(1,0));
                    Check(g.perfectDodgeArmed&&g.dashTime>.17f,
                        "The player must actually commit a stamina-paid dodge out of the locked ring");
                    Advance(g,.26f);
                    Check(heavy.state==0&&
                        (g.Position-origin).Length>2.4f&&
                        (g.Position-heavy.target).Length>GameCore.EnemyAreaRadius(heavy)&&
                        g.perfectDodgeArmed==sealLane,
                        "The heavy completed its genuine attack and the dodge originated in its visible radius");
                    if(sealLane)
                        Check(g.riposteTime<=0f&&
                            !g.effects.Exists(f=>f.text=="闪避反击"||
                                f.text=="COUNTER"),
                            "A dodge from an already protected foot cannot earn a false heavy-attack riposte");
                    else
                        Check(g.riposteTime>1.1f&&
                            g.effects.Exists(f=>f.text=="闪避反击"||
                                f.text=="COUNTER"),
                            "A dash out of the identical open-lane heavy warning still earns the original real riposte counter");
                }
            }
            finally{data.maps[0]=previous;}
        }

        public static void GroundHazardDodgeRewardRequiresRealExposedStartingFoot(Campaign data)
        {
            MapData previous=data.maps[0];
            try
            {
                var rows=new string[25];
                for(int y=0;y<25;y++)
                {
                    char[] row=new string('#',25).ToCharArray();
                    if(y>=2&&y<=22)for(int x=2;x<=22;x++)row[x]='.';
                    rows[y]=new string(row);
                }
                data.maps[0]=new MapData {
                    id=0,spawnX=10.5f,spawnY=10.5f,rows=rows,
                    objects=new WorldObject[0],decor=new Decoration[0] };
                Point blast=new Point(10.5f,12.5f);
                foreach(bool wallClosed in new[]{true,false})
                {
                    char[] gate=rows[11].ToCharArray();
                    gate[10]=wallClosed?'#':'.';
                    rows[11]=new string(gate);
                    var g=Fresh(data,0);g.enemies.Clear();
                    g.save.x=10.5f;g.save.y=10.5f;
                    g.save.reducedMotion=true;
                    g.hazards.Add(new Hazard{
                        position=blast,timer=.20f,radius=3f,
                        damage=20f,kind="tide"});
                    Check(g.CanStand(g.Position)&&
                        (g.Position-blast).Length<3f&&
                        (wallClosed?g.SafeTravel:!g.SafeTravel),
                        "The two real dodge trials start within a telegraphed radius; only an open line threatens the player");
                    g.Dash(new Point(1,0));
                    Check(g.dashTime>0&&g.perfectDodgeArmed,
                        "Both cases use an actual paid player dash instead of manually setting a reward flag");
                    Advance(g,.23f);
                    Check((g.Position-blast).Length>3f&&
                        g.hazards.Count==0&&
                        g.effects.Exists(e=>e.kind=="danger"&&
                            (e.position-blast).Length<.01f),
                        "Both genuine dashes exit the same radius before the real hazard contact and keep the original impact cue");
                    if(wallClosed)
                        Check(g.riposteTime<=0f&&
                            !g.effects.Exists(e=>e.text=="闪避反击"||
                                e.text=="COUNTER"),
                            "Dashing away from a hazard already blocked by stone cannot farm a perfect dodge counter");
                    else
                        Check(g.riposteTime>1.2f&&
                            g.effects.Exists(e=>e.text=="闪避反击"||
                                e.text=="COUNTER"),
                            "An identical dash away from a physically exposed hazard still grants its genuine earned counter");
                }
            }
            finally{data.maps[0]=previous;}
        }

        public static void DelayedGroundBlastRespectsDynamicCoverAndSafety(Campaign data)
        {
            MapData previous=data.maps[0];
            try
            {
                var rows=new string[25];
                for(int y=0;y<25;y++)
                {
                    char[] tiles=new string('#',25).ToCharArray();
                    if(y>=2&&y<=22)
                        for(int x=2;x<=22;x++)tiles[x]='.';
                    rows[y]=new string(tiles);
                }
                data.maps[0]=new MapData{
                    id=0,spawnX=10.5f,spawnY=10.5f,rows=rows,
                    objects=new WorldObject[0],decor=new Decoration[0]};
                var g=Fresh(data,0);g.enemies.Clear();
                g.save.x=10.5f;g.save.y=10.5f;
                g.save.health=100;g.save.reducedMotion=true;
                g.invulnerable=0;
                Point root=new Point(10.5f,12.5f);
                Check(g.CanStand(g.Position)&&g.CanStand(root)&&g.SafeTravel,
                    "Legal open hero and ground-blast roots begin without phantom fieldwork danger");
                g.hazards.Add(new Hazard{
                    position=root,timer=.30f,radius=3f,damage=20f,kind="tide"});
                Check(!g.SafeTravel,
                    "Pending unblocked delayed area blast prevents erasing real danger via quick travel");
                // Dynamic wall closes AFTER the warning was scheduled but
                // BEFORE physical contact, while neither foot is buried.
                char[] sealedRow=rows[11].ToCharArray();
                sealedRow[10]='#';rows[11]=new string(sealedRow);
                Check(g.CanStand(g.Position)&&g.CanStand(root)&&g.SafeTravel,
                    "Solid cover shields pending blast and restores safe travel without removing its world timer");
                Advance(g,.34f);
                Check(g.save.health==100f&&g.hazards.Count==0&&
                    g.effects.Exists(e=>e.kind=="danger"&&
                        (e.position-root).Length<.01f),
                    "Blocked ground explosion still emits its local actual blast cue but cannot hurt the sheltered player");
                sealedRow[10]='.';rows[11]=new string(sealedRow);
                g.hazards.Add(new Hazard{
                    position=root,timer=.18f,radius=3f,damage=20f,kind="tide"});
                Check(!g.SafeTravel,
                    "Reopened physical lane again counts the pending damage as a genuine threat");
                g.invulnerable=0;
                Advance(g,.22f);
                Check(g.save.health<100f&&g.save.health>75f&&
                    g.hazards.Count==0,
                    "An unobstructed explosion actually reaches and damages the same living hero on its true delayed contact");
            }
            finally{data.maps[0]=previous;}
        }

        public static void SimultaneousGroundHazardsOnlyDamageThroughClearLane(Campaign data)
        {
            MapData previous=data.maps[0];
            try
            {
                var rows=new string[25];
                for(int y=0;y<25;y++)
                {
                    char[] tiles=new string('#',25).ToCharArray();
                    if(y>=2&&y<=22)for(int x=2;x<=22;x++)tiles[x]='.';
                    rows[y]=new string(tiles);
                }
                char[] wall=rows[11].ToCharArray();
                wall[10]='#';rows[11]=new string(wall);
                data.maps[0]=new MapData{
                    id=0,spawnX=10.5f,spawnY=10.5f,rows=rows,
                    objects=new WorldObject[0],decor=new Decoration[0]};
                var g=Fresh(data,0);g.enemies.Clear();
                g.save.x=10.5f;g.save.y=10.5f;
                g.save.health=100f;g.save.reducedMotion=true;
                g.invulnerable=0;
                Point sheltered=new Point(10.5f,12.5f);
                Point exposed=new Point(12.5f,10.5f);
                g.hazards.Add(new Hazard{
                    position=sheltered,timer=.12f,radius=3f,damage=20f,kind="tide"});
                g.hazards.Add(new Hazard{
                    position=exposed,timer=.12f,radius=3f,damage=20f,kind="glass"});
                Check(g.SafeTravel==false&&g.CanStand(sheltered)&&
                    g.CanStand(exposed),
                    "The second genuinely visible blast must still block travel even though the first is protected by cover");
                Advance(g,.16f);
                Check(Math.Abs(g.save.health-80f)<.001f&&
                    g.effects.Exists(e=>e.kind=="danger"&&
                        (e.position-sheltered).Length<.01f)&&
                    g.effects.Exists(e=>e.kind=="danger"&&
                        (e.position-exposed).Length<.01f)&&
                    g.hazards.Count==0,
                    "Simultaneous hazards preserve both distinct visual contacts; only the unobstructed blast deals its true 20-point standard-difficulty damage");
            }
            finally{data.maps[0]=previous;}
        }

        public static void HeroPhysicalMuzzleObstructionRetainsEarnedRiposte(Campaign data)
        {
            // Test both production LMB projectile weapons and their
            // physical attachment clearance at actual animation contact.
            MapData original=data.maps[0];
            try
            {
                var rows=new string[25];
                for(int y=0;y<25;y++)
                {
                    char[] tiles=new string('#',25).ToCharArray();
                    if(y>=2&&y<=22)for(int x=2;x<=22;x++)tiles[x]='.';
                    if(y==10)tiles[9]='#';
                    rows[y]=new string(tiles);
                }
                data.maps[0]=new MapData{
                    id=0,spawnX=10.5f,spawnY=10.5f,rows=rows,
                    objects=new WorldObject[0],decor=new Decoration[0]};
                foreach(int weapon in new[]{1,2})
                {
                    var g=Fresh(data,weapon);g.enemies.Clear();
                    g.save.x=10.5f;g.save.y=10.5f;
                    g.save.reducedMotion=true;g.save.energy=100;
                    g.save.weapon=weapon;g.riposteTime=3f;
                    var foe=new Enemy(98911+weapon,0,10.5f,7.5f,10000f)
                        {stun=100,timer=1000,staggerRecovery=100};
                    g.enemies.Add(foe);
                    g.SetAimTarget(foe.Position);
                    Check(g.CanStand(g.Position)&&g.CanStand(foe.Position)&&
                        g.Blocked(9.5f,10.5f),
                        "Real cover blocks a side muzzle beside legal player feet and an unobstructed enemy");
                    g.Attack(new Point(0,-1));
                    ActorAction invalid=g.playerAction;
                    Check(invalid!=null&&!invalid.released&&invalid.riposteArmed,
                        "Physical ranged attack reserves the earned counter, without paying before contact");
                    Advance(g,.45f);
                    Check(invalid.released&&g.projectiles.Count==0&&
                        !g.effects.Exists(f=>f.kind=="release")&&
                        !g.effects.Exists(f=>f.text=="RIPOSTE RELEASE"||
                            f.text=="反击释放！")&&
                        g.effects.Exists(f=>f.kind=="projectileImpact")&&
                        g.riposteTime>2.3f&&
                        !invalid.riposteArmed,
                        "A wall-born bow/staff projectile never launches, fakes VFX or spends the genuine earned counter");
                    char[] tile=rows[10].ToCharArray();
                    tile[9]='.';rows[10]=new string(tile);
                    Advance(g,.42f);
                    g.SetAimTarget(foe.Position);
                    g.Attack(new Point(0,-1));
                    ActorAction valid=g.playerAction;
                    Check(valid!=null&&valid!=invalid&&valid.riposteArmed&&
                        !valid.released,
                        "Counter remains genuinely available for the next physical ranged attack after the wall opens");
                    float power=valid.damage;
                    Advance(g,.45f);
                    Check(valid.released&&!valid.riposteArmed&&
                        Math.Abs(valid.damage-power*1.5f)<.001f&&
                        g.riposteTime<=0&&
                        (g.projectiles.Exists(p=>p.friendly&&!p.blockedAtSpawn&&
                            Math.Abs(p.damage-valid.damage*
                                (weapon==1&&valid.style==2?.65f:1f))<.001f) ||
                            // With a short shot-to-target lane the missile
                            // may already have completed physical contact
                            // before this sampled recovery frame.
                            (foe.hp<foe.maxHp&&
                             g.effects.Exists(f=>f.kind=="impact"||f.kind=="spark"))),
                        "An unobstructed contact pays the original exactly-once +50 percent riposte into a real flying projectile");
                    Advance(g,.6f);
                    Check(foe.hp<foe.maxHp,
                        "An actual recovered counter travels to and damages the living enemy");
                    tile[9]='#';rows[10]=new string(tile);
                }
            }
            finally{data.maps[0]=original;}
        }

        public static void EarnedRiposteCommitsOnTrueMeleeArrowOrStaffContact(Campaign data)
        {
            for(int weapon=0;weapon<3;weapon++)
            {
                var g=Fresh(data,weapon);g.enemies.Clear();g.invulnerable=100;
                g.save.energy=100;g.riposteTime=1.5f;
                g.save.x=11;g.save.y=12;
                var target=new Enemy(93420+weapon,0,g.save.x+1.2f,g.save.y,900);
                target.stun=10;g.enemies.Add(target);
                g.Attack(new Point(1,0));
                ActorAction action=g.playerAction;
                Check(action!=null&&action.kind=="attack"&&action.riposteArmed,
                    "Each equipped sword/bow/staff action captures a legitimately available riposte at acceptance");
                float baseDamage=action.damage;
                Check(g.riposteTime>=1.49f&&baseDamage>0&&
                    !action.released,
                    "Pressing LMB does not charge or prematurely consume a counter before the real contact frame");
                Advance(g,Math.Min(.08f,action.contact*.40f));
                Check(g.riposteTime>0&&
                    Math.Abs(action.damage-baseDamage)<.001f&&!action.released,
                    "Visible windup preserves the original bonus and untouched power");
                // Finish preparation with a single contact frame. Any shot
                // released THIS frame is not allowed to fly before the next,
                // so the real projectile profile remains observable here.
                Advance(g,Math.Max(0,action.contact-action.elapsed-.04f));
                g.Tick(.05f,new Point());
                Check(action.released&&!action.riposteArmed&&g.riposteTime<=0&&
                    Math.Abs(action.damage-baseDamage*1.5f)<.001f,
                    "The sole original release frame commits exactly one +50 percent earned riposte");
                if(weapon==0)
                    Check(target.hp<target.maxHp,
                        "The released, empowered sword swing genuinely hits the physical target");
                else
                    Check(g.projectiles.Exists(p=>p.friendly&&
                        Math.Abs(p.damage-action.damage)<.001f&&
                        p.impact!=null&&p.impact.playerOwned),
                        "Released bow/staff snapshot carries the correct riposte damage and original player ownership");
                float committed=action.damage;
                Advance(g,.025f);
                Check(Math.Abs(action.damage-committed)<.001f,
                    "Action recovery cannot reapply an already consumed riposte multiplier");
            }
        }

        public static void InterruptedCounterWindupsCannotWasteEarnedReward(Campaign data)
        {
            var g=Fresh(data,0);g.enemies.Clear();g.save.energy=100;
            // Actual active dodge cancellation of a fully accepted swing.
            g.riposteTime=1.5f;
            g.Attack(new Point(1,0));
            ActorAction abandoned=g.playerAction;
            float originalDamage=abandoned==null?0:abandoned.damage;
            Check(abandoned!=null&&abandoned.riposteArmed&&g.riposteTime>0,
                "A legitimate sword windup begins with the earned bonus reserved but unpaid");
            g.Dash(new Point(1,0));
            Check(g.playerAction==null&&g.riposteTime>0&&
                !abandoned.released&&
                Math.Abs(abandoned.damage-originalDamage)<.001f,
                "Dodge-cancel before actual release preserves the riposte window instead of consuming it");
            Advance(g,.22f);
            Check(g.riposteTime>0,
                "A canceled attempt retains the time-limited bonus for the next legitimate action");
            // A different equipment profile also retires only the unspent
            // windup; it cannot inject stale contact or extra projectile.
            var switching=Fresh(data,0);switching.enemies.Clear();
            switching.save.flags.Add("weapon_1");
            switching.riposteTime=1.5f;switching.Attack(new Point(1,0));
            var old=switching.playerAction;
            switching.Equip(1);
            Check(switching.playerAction==null&&switching.riposteTime>0&&
                !old.released&&switching.projectiles.Count==0,
                "Weapon swap before contact cannot spend counter or fire the retired old-weapon action");
            switching.save.energy=100;
            switching.attackCooldown=0;
            switching.Attack(new Point(1,0));
            Check(switching.playerAction!=null&&
                switching.playerAction.riposteArmed,
                "The real next equipped bow attack can inherit the still-valid earned counter");
            // Old cooldown/expired openings must not become free damage.
            var expired=Fresh(data,2);expired.enemies.Clear();
            expired.riposteTime=0;expired.Attack(new Point(1,0));
            Check(expired.playerAction!=null&&!expired.playerAction.riposteArmed,
                "A zero-time counter cannot be revived by clicking attack");
            // No buff or old action may leak to a different save slot.
            switching.NewGame(2);switching.Close();
            Check(switching.riposteTime<=0&&switching.playerAction==null,
                "Changing heroes erases transient counter intents and all old contact state");
        }

        public static void CompanionFrostUsesPhysicalCasterAndNeverReplacesArcanistField(Campaign data)
        {
            MapData original=data.maps[0];
            try
            {
                string[] rows=new string[25];
                for(int y=0;y<25;y++)
                {
                    char[] tiles=new string('#',25).ToCharArray();
                    if(y>=2&&y<=22)
                        for(int x=2;x<=22;x++)tiles[x]='.';
                    rows[y]=new string(tiles);
                }
                data.maps[0]=new MapData{
                    id=0,spawnX=10.5f,spawnY=11.5f,rows=rows,
                    objects=new WorldObject[0],decor=new Decoration[0]};
                var g=Fresh(data,2);g.enemies.Clear();g.save.energy=100;
                g.save.reducedMotion=true;
                g.save.x=10.5f;g.save.y=11.5f;
                g.save.flags.Add("companion_2");
                g.SelectCompanion(2);
                g.save.technique=1;
                var nearHero=new Enemy(98401,0,10.5f,13.5f,10000f)
                    {stun=10f,timer=100f,staggerRecovery=100f};
                var nearAlly=new Enemy(98402,0,19.5f,13.5f,10000f)
                    {stun=10f,timer=100f,staggerRecovery=100f};
                g.enemies.Add(nearHero);g.enemies.Add(nearAlly);
                g.Skill(new Point(1,0));
                Advance(g,.36f);
                var playerField=typeof(GameCore).GetField("frost",
                    System.Reflection.BindingFlags.Instance|
                    System.Reflection.BindingFlags.NonPublic);
                var companionField=typeof(GameCore).GetField("companionFrost",
                    System.Reflection.BindingFlags.Instance|
                    System.Reflection.BindingFlags.NonPublic);
                Check(playerField!=null&&companionField!=null&&
                    playerField.GetValue(g)!=null,
                    "An actual Arcanist alternate-Q establishes a live player-owned persistent spell");
                object originalPlayer=playerField.GetValue(g);
                g.companionPosition=new Point(19.5f,11.5f);
                float heroHpBefore=nearHero.hp,allyHpBefore=nearAlly.hp;
                g.CommandCompanion();
                Check(g.companionAction!=null&&
                    g.companionAction.weapon==2,
                    "Real unlocked Sen accepts the physical companion order");
                Advance(g,.23f);
                Check(ReferenceEquals(playerField.GetValue(g),originalPlayer)&&
                    companionField.GetValue(g)!=null&&
                    !ReferenceEquals(companionField.GetValue(g),originalPlayer),
                    "The companion's genuine cast never replaces an in-flight player's Q field");
                Check(nearAlly.hp<allyHpBefore&&nearHero.hp<=heroHpBefore,
                    "Sen's original damaging pulse actually affects enemies around the ally");
                Check(g.effects.Exists(e=>e.kind=="release"&&e.variant==2&&
                    (e.position-GameCore.WeaponOrigin(g.companionPosition,2,g.companionAim)).Length<.01f),
                    "An actual companion caster visibly releases from his staff, not from the hero's muzzle");
                var published=g.effects.FindLast(e=>e.kind=="frost");
                Check(published!=null&&
                    (published.position-g.companionPosition).Length<.01f,
                    "Sen's visible frost ring is authored at the ally's CURRENT feet, not invisibly under the hero");
                float heroCurrent=nearHero.hp,allyCurrent=nearAlly.hp;
                // Each real frost pulse is 800ms apart. Advance safely
                // past both caster schedules instead of testing exactly at
                // the boundary of combat hit-stop or per-frame rounding.
                Advance(g,1.12f);
                Check(nearHero.hp<heroCurrent&&nearAlly.hp<allyCurrent,
                    "Both independently scheduled player and companion frost fields continue genuine later damage pulses");
                Check(g.enemies.Count==2&&g.save.version==2,
                    "Dual frost changes transient combat only and does not falsify kills or the v2 save format");
                g.PrepareSave();var reopened=new GameCore(data,CloneSave(g.save));
                reopened.Start();
                Check(playerField.GetValue(reopened)==null&&
                    companionField.GetValue(reopened)==null,
                    "Neither caster's unfinished attack may survive a saved-game reload");
            }
            finally{data.maps[0]=original;}
        }

        public static void BlockedCompanionFrostCannotTeleportToHeroOrChargeCooldown(Campaign data)
        {
            var g=Fresh(data,2);g.enemies.Clear();
            g.save.flags.Add("companion_2");g.SelectCompanion(2);
            g.save.health=40;g.save.energy=100;
            var field=typeof(GameCore).GetField("companionFrost",
                System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.NonPublic);
            Check(field!=null,"Real companion spell has its own transient cast ownership");
            // The caster's old foot became solid (gate/tide/room repair).
            // Preserve the actual windup, but reject an impossible release.
            g.companionPosition=new Point(.5f,.5f);
            Check(!g.CanStand(g.companionPosition),"Test caster occupies genuinely blocked terrain");
            int beforeFx=g.effects.Count;
            g.CommandCompanion();
            Check(g.companionAction!=null&&g.companionCooldown>0,
                "Normal player input accepts the original half-second companion action");
            Advance(g,.23f);
            Check(g.companionAction!=null&&g.companionAction.released&&
                field.GetValue(g)==null&&g.companionCooldown==0f&&
                !g.effects.Exists(e=>e.kind=="frost"),
                "Invalid real caster footing refunds the full manual cooldown instead of creating a free hero-centered frost");
            Check(g.effects.Count==beforeFx,
                "Invalid remote companion contact emits no fake player hit or arena effect");
            g.Enter(1);
            Check(field.GetValue(g)==null&&g.save.version==2,
                "Room travel retires any pending ally magic without editing SaveData v2");
        }

        public static void RecastFrostCannotLeaveFalseOldWorldRing(Campaign data)
        {
            // At Q cooldown 3.5s the old four-second field can still own a
            // visible final pulse. Recasting at another valid foot replaces
            // gameplay, and must atomically replace only its old visual.
            MapData previous=data.maps[0];
            try
            {
                string[] rows=new string[25];
                for(int y=0;y<25;y++)
                {
                    char[] tiles=new string('#',25).ToCharArray();
                    if(y>=2&&y<=22)
                        for(int x=2;x<=22;x++)tiles[x]='.';
                    rows[y]=new string(tiles);
                }
                data.maps[0]=new MapData{
                    id=0,spawnX=10.5f,spawnY=11.5f,rows=rows,
                    objects=new WorldObject[0],decor=new Decoration[0]};
                var g=Fresh(data,2);g.enemies.Clear();
                g.save.x=10.5f;g.save.y=11.5f;
                g.save.energy=100;g.save.technique=1;
                g.save.reducedMotion=true;
                var frost=typeof(GameCore).GetField("frost",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic);
                g.Skill(new Point(1,0));
                Advance(g,.36f);
                object first=frost.GetValue(g);
                Check(first!=null&&
                    g.effects.Exists(e=>e.kind=="frost"&&
                        (e.position-new Point(10.5f,11.5f)).Length<.01f),
                    "First real player Q publishes a grounded frost field and its matching live ring");
                Advance(g,3.2f);
                Check(ReferenceEquals(first,frost.GetValue(g))&&
                    g.skillCooldown<=.001f&&
                    g.effects.Exists(e=>e.kind=="frost"&&
                        (e.position-new Point(10.5f,11.5f)).Length<.01f),
                    "Old spell still owns its late pulse when the genuine Q cooldown completes");
                g.save.x=19.5f;g.save.y=11.5f;g.save.energy=100;
                g.Skill(new Point(1,0));
                Check(g.playerAction!=null&&
                    g.playerAction.kind=="skill"&&!g.playerAction.released,
                    "A legal second Q begins a different physical release at the new hero foot");
                Advance(g,.34f);
                Check(frost.GetValue(g)!=null&&
                    !ReferenceEquals(first,frost.GetValue(g))&&
                    !g.effects.Exists(e=>e.kind=="frost"&&
                        (e.position-new Point(10.5f,11.5f)).Length<.01f)&&
                    g.effects.Exists(e=>e.kind=="frost"&&
                        (e.position-new Point(19.5f,11.5f)).Length<.01f),
                    "A legitimate recast retires the obsolete former-world ring while preserving its new damage field and visible source");
                Check(g.save.version==2,
                    "Recast visual ownership remains transient and does not alter save schema");
            }
            finally{data.maps[0]=previous;}
        }

        public static void DynamicTerrainSealsOnlyTheBuriedFrostField(Campaign data)
        {
            // A ground spell may outlast the player moving away, but it
            // cannot keep dealing damage through a newly sealed origin.
            // Independent Sen frost must survive the other caster's loss.
            MapData previous = data.maps[0];
            try
            {
                var rows = new string[25];
                for(int y=0;y<25;y++)
                {
                    char[] tiles = new string('#',25).ToCharArray();
                    if(y>=2&&y<=22)
                        for(int x=2;x<=22;x++)tiles[x]='.';
                    rows[y] = new string(tiles);
                }
                data.maps[0] = new MapData {
                    id=0,spawnX=10.5f,spawnY=11.5f,rows=rows,
                    objects=new WorldObject[0],decor=new Decoration[0] };
                var g=Fresh(data,2);g.enemies.Clear();
                g.save.x=10.5f;g.save.y=11.5f;
                g.save.reducedMotion=true;g.save.energy=100;
                g.save.technique=1;
                g.save.flags.Add("companion_2");g.SelectCompanion(2);
                var heroFoe=new Enemy(98861,0,10.5f,13.5f,10000)
                    {stun=100f,timer=1000f,staggerRecovery=100f};
                var senFoe=new Enemy(98862,0,19.5f,13.5f,10000)
                    {stun=100f,timer=1000f,staggerRecovery=100f};
                g.enemies.Add(heroFoe);g.enemies.Add(senFoe);
                var playerField=typeof(GameCore).GetField("frost",
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance);
                var allyField=typeof(GameCore).GetField("companionFrost",
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance);
                g.Skill(new Point(1,0)); Advance(g,.36f);
                g.companionPosition=new Point(19.5f,11.5f);
                g.CommandCompanion(); Advance(g,.23f);
                Check(playerField.GetValue(g)!=null&&allyField.GetValue(g)!=null&&
                    heroFoe.hp<heroFoe.maxHp&&senFoe.hp<senFoe.maxHp,
                    "Two independently authored real-world frost fields first deal their legitimate caster-local contact");
                // Keep the player alive on a separate valid tile before
                // closing the original spell's physical world root.
                g.save.x=15.5f;g.save.y=11.5f;
                char[] sealedRow=rows[11].ToCharArray();
                sealedRow[10]='#';rows[11]=new string(sealedRow);
                Check(!g.CanStand(new Point(10.5f,11.5f))&&
                    g.CanStand(new Point(19.5f,11.5f))&&
                    g.CanStand(g.Position),
                    "A real dynamic solid tile has buried only the player's spell root, not the other field or living player");
                float firstHp=heroFoe.hp, secondHp=senFoe.hp;
                // Ring and damage must retire in the SAME physical frame.
                // Sen's separate, identical-looking frost art is not owned
                // by the buried player's field.
                Advance(g,.02f);
                Check(!g.effects.Exists(e=>e.kind=="frost"&&
                    (e.position-new Point(10.5f,11.5f)).Length<.01f)&&
                    g.effects.Exists(e=>e.kind=="frost"&&
                    (e.position-new Point(19.5f,11.5f)).Length<.01f),
                    "Invalidated ground frost must retract only its own live ring immediately; Sen's still-legal ring remains visible");
                // A free Sen auto attack would confound damage attribution.
                var auto=typeof(GameCore).GetField("companionAttack",
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance);
                auto.SetValue(g,99f);
                Advance(g, .96f);
                Check(playerField.GetValue(g)==null&&allyField.GetValue(g)!=null,
                    "The buried player frost retires promptly without canceling Sen's separate surviving aura");
                Check(heroFoe.hp==firstHp&&senFoe.hp<secondHp,
                    "Solidified spell root deals no remote later damage, while legal Sen pulses still genuinely damage enemies");
                Advance(g,3.5f);
                Check(allyField.GetValue(g)==null&&g.save.version==2,
                    "The surviving independent field expires normally without changing the v2 save schema");
            }
            finally{data.maps[0]=previous;}
        }

        public static void PlayerAndCompanionFrostExpireOnIndependentRealWorldClocks(Campaign data)
        {
            MapData old=data.maps[0];
            try
            {
                string[] tiles=new string[25];
                for(int y=0;y<25;y++)
                {
                    char[] row=new string('#',25).ToCharArray();
                    if(y>=2&&y<=22)
                        for(int x=2;x<=22;x++)row[x]='.';
                    tiles[y]=new string(row);
                }
                data.maps[0]=new MapData{
                    id=0,spawnX=10.5f,spawnY=11.5f,rows=tiles,
                    objects=new WorldObject[0],decor=new Decoration[0]};
                var g=Fresh(data,2);g.enemies.Clear();
                g.save.x=10.5f;g.save.y=11.5f;
                g.save.reducedMotion=true;g.save.energy=100;
                g.save.flags.Add("companion_2");g.SelectCompanion(2);
                g.save.technique=1;
                var foe=new Enemy(98521,0,19.5f,13.5f,10000)
                    {stun=100f,timer=1000f,staggerRecovery=100f};
                g.enemies.Add(foe);
                var normal=typeof(GameCore).GetField("frost",
                    System.Reflection.BindingFlags.Instance|
                    System.Reflection.BindingFlags.NonPublic);
                var ally=typeof(GameCore).GetField("companionFrost",
                    System.Reflection.BindingFlags.Instance|
                    System.Reflection.BindingFlags.NonPublic);
                g.Skill(new Point(1,0));Advance(g,.36f);Advance(g,1.55f);
                Check(normal.GetValue(g)!=null&&ally.GetValue(g)==null,
                    "The original Arcanist Q remains independently active before a later Sen cast");
                g.companionPosition=new Point(19.5f,11.5f);
                g.CommandCompanion();Advance(g,.23f);
                Check(normal.GetValue(g)!=null&&ally.GetValue(g)!=null,
                    "Delayed real companion spell coexists with original player Q");
                Advance(g,2.18f);
                Check(normal.GetValue(g)==null&&ally.GetValue(g)!=null,
                    "Old player's spell expires on its own fourth second without erasing newer companion magic");
                float hp=foe.hp;
                Advance(g,1.12f);
                Check(foe.hp<hp&&normal.GetValue(g)==null,
                    "The companion-owned aura continues a genuine later damage pulse after the player's field has retired");
                Advance(g,.82f);
                Check(ally.GetValue(g)==null&&normal.GetValue(g)==null,
                    "Each authored spell expires only after its own original four seconds, with no permanent aura");
            }
            finally{data.maps[0]=old;}
        }

        public static void CompanionFrostCannotHitThroughWallsEvenWithinAreaRadius(Campaign data)
        {
            MapData old=data.maps[0];
            try
            {
                string[] tiles=new string[25];
                for(int y=0;y<25;y++)
                {
                    char[] row=new string('#',25).ToCharArray();
                    if(y>=2&&y<=22)
                        for(int x=2;x<=22;x++)row[x]='.';
                    row[12]='#';
                    tiles[y]=new string(row);
                }
                // Entire x=13 column is a solid separator in the playable
                // actor footprint, despite both enemies being within 4 tiles.
                for(int y=2;y<=22;y++)
                {
                    char[] row=tiles[y].ToCharArray();row[13]='#';
                    tiles[y]=new string(row);
                }
                data.maps[0]=new MapData{
                    id=0,spawnX=10.5f,spawnY=12.5f,rows=tiles,
                    objects=new WorldObject[0],decor=new Decoration[0]};
                var g=Fresh(data,2);g.enemies.Clear();
                g.save.x=10.5f;g.save.y=12.5f;
                g.save.reducedMotion=true;
                g.save.flags.Add("companion_2");g.SelectCompanion(2);
                g.companionPosition=new Point(11.5f,12.5f);
                var exposed=new Enemy(98631,0,11.5f,14.5f,10000)
                    {stun=100f,timer=1000f,staggerRecovery=100f};
                var blocked=new Enemy(98632,0,14.5f,12.5f,10000)
                    {stun=100f,timer=1000f,staggerRecovery=100f};
                Check(g.CanStand(g.companionPosition)&&g.CanStand(blocked.Position),
                    "Both real actors occupy valid physical feet on opposing sides of the wall");
                g.enemies.Add(exposed);g.enemies.Add(blocked);
                g.CommandCompanion();Advance(g,.24f);
                Check(exposed.hp<10000f&&blocked.hp==10000f,
                    "Sen's immediate real cast damages his own side but not a foe behind solid cover");
                Advance(g,.92f);
                Check(blocked.hp==10000f,
                    "Repeated damaging pulses cannot leak through sealed walls while still within the numerical radius");
                for(int y=2;y<=22;y++)
                {
                    char[] row=tiles[y].ToCharArray();
                    row[12]=row[13]='.';
                    tiles[y]=new string(row);
                }
                Advance(g,.85f);
                Check(blocked.hp<10000f,
                    "A genuinely opened corridor permits the still-existing physical frost field's next legal pulse");
            }
            finally{data.maps[0]=old;}
        }

        public static void RangerCompanionCannotShootFromBlockedFloorAndRetriesRealContact(Campaign data)
        {
            MapData old=data.maps[0];
            try
            {
                var rows=new string[25];
                for(int y=0;y<25;y++)
                {
                    char[] row=new string('#',25).ToCharArray();
                    if(y>=2&&y<=22)for(int x=2;x<=22;x++)row[x]='.';
                    rows[y]=new string(row);
                }
                data.maps[0]=new MapData{
                    id=0,spawnX=12.5f,spawnY=12.5f,rows=rows,
                    objects=new WorldObject[0],decor=new Decoration[0]};
                var g=Fresh(data,1);g.enemies.Clear();
                g.save.flags.Add("companion_1");g.SelectCompanion(1);
                Point legitimate=g.companionPosition;
                Check(g.CanStand(legitimate),"Production ranger companion initially stands on real playable ground");
                g.companionPosition=new Point(.5f,.5f);
                Check(!g.CanStand(g.companionPosition),"Ranger's prior physical foot was blocked by real terrain");
                g.CommandCompanion();Advance(g,.23f);
                Check(g.companionCooldown==0f&&g.projectiles.Count==0&&
                    !g.effects.Exists(e=>e.kind=="release"&&e.variant==1),
                    "A manual three-arrow volley cannot spawn out of a solid wall, falsely animate or charge an 18-second cooldown");
                g.companionPosition=legitimate;
                g.CommandCompanion();Advance(g,.23f);
                Check(g.projectiles.Count==3&&
                    g.projectiles.TrueForAll(p=>p.friendly&&p.piercing)&&
                    g.effects.Exists(e=>e.kind=="release"&&e.variant==1),
                    "A refunded command can immediately issue the genuine three-projectile volley from a legal companion footing");

                var auto=Fresh(data,1);auto.enemies.Clear();
                auto.save.flags.Add("companion_1");auto.SelectCompanion(1);
                auto.save.reducedMotion=true;
                Point valid=auto.companionPosition;
                var foe=new Enemy(98703,0,valid.x+2.3f,valid.y,10000f)
                    {stun=100f,timer=1000f,staggerRecovery=100f};
                auto.enemies.Add(foe);
                Check(auto.CanStand(foe.Position),
                    "Automatic companion projectile test has a reachable, valid enemy foot");
                var cadence=typeof(GameCore).GetField("companionAttack",
                    System.Reflection.BindingFlags.NonPublic|
                    System.Reflection.BindingFlags.Instance);
                Check(cadence!=null,"Real ally cooldown scheduling is present");
                cadence.SetValue(auto,0f);
                auto.Tick(.02f,new Point());
                Check(auto.companionAction!=null&&
                    auto.companionAction.kind=="attack"&&
                    !auto.companionAction.released,
                    "Live Ivo AI began a real auto-attack preparation against its selected foe");
                auto.companionPosition=new Point(.5f,.5f);
                Advance(auto,.20f);
                Check(auto.projectiles.Count==0&&
                    (float)cadence.GetValue(auto)<=.23f,
                    "A ranged auto attack interrupted by solid ground neither fires through walls nor wastes its full attack cooldown");
                auto.companionPosition=valid;
                Advance(auto,.65f);
                Check(foe.hp<10000f||auto.projectiles.Exists(p=>p.friendly),
                    "The real companion AI can resume and shoot after returning to valid accessible footing");

                // Side-adjacent stone can catch an arrow's 0.14-tile
                // hit radius while leaving the actor LOS and hand clear.
                var flank=Fresh(data,1);flank.enemies.Clear();
                flank.save.flags.Add("companion_1");flank.SelectCompanion(1);
                flank.save.reducedMotion=true;
                Point archerFoot=new Point(10.5f,10.88f);
                Point victimFoot=new Point(15.5f,10.88f);
                flank.companionPosition=archerFoot;
                flank.save.x=15.5f;flank.save.y=13.5f;
                var victim=new Enemy(98704,0,
                    victimFoot.x,victimFoot.y,10000f){
                    stun=100f,timer=1000f,staggerRecovery=100f
                };
                flank.enemies.Add(victim);
                var nearest=typeof(GameCore).GetMethod(
                    "NearestCompanionThreat",
                    System.Reflection.BindingFlags.Instance|
                    System.Reflection.BindingFlags.NonPublic);
                var center=typeof(GameCore).GetMethod(
                    "ClearAttackPath",
                    System.Reflection.BindingFlags.Instance|
                    System.Reflection.BindingFlags.NonPublic);
                Check(nearest!=null&&center!=null,
                    "Production companion targeting and real actor sight checks absent");
                Check(flank.CanStand(archerFoot)&&flank.CanStand(victimFoot)&&
                    (bool)center.Invoke(flank,new object[]{
                        archerFoot,victimFoot})&&
                    System.Object.ReferenceEquals(nearest.Invoke(flank,null),victim),
                    "Genuinely unobstructed target must be available for ally auto-fire");
                string lower=rows[11];
                try
                {
                    char[] edge=lower.ToCharArray();edge[12]='#';
                    rows[11]=new string(edge);
                    Check((bool)center.Invoke(flank,new object[]{
                            archerFoot,victimFoot})&&
                        nearest.Invoke(flank,null)==null,
                        "Ranger ally selected a foe behind side-wall projectile-radius cover");
                    rows[11]=lower;
                    var attackTimer=typeof(GameCore).GetField(
                        "companionAttack",
                        System.Reflection.BindingFlags.Instance|
                        System.Reflection.BindingFlags.NonPublic);
                    attackTimer.SetValue(flank,0f);
                    flank.Tick(.02f,new Point());
                    Check(flank.companionAction!=null&&
                        flank.companionAction.kind=="attack"&&
                        !flank.companionAction.released,
                        "Removing arrow collision cover failed to resume a real ally windup");
                    rows[11]=new string(edge);
                    Advance(flank,.20f);
                    Check(flank.projectiles.Count==0&&
                        (float)attackTimer.GetValue(flank)<=.23f&&
                        !flank.effects.Exists(e=>e.kind=="release"&&
                            e.variant==1),
                        "Wall closure during auto-draw minted a false arrow/release or spent full cooldown");
                    rows[11]=lower;
                    Advance(flank,.70f);
                    Check(victim.hp<10000f||
                        flank.projectiles.Exists(p=>p.friendly&&
                            p.visual=="arrow"),
                        "Reopening side-wall corridor failed to restore actual ally arrow attack");
                }
                finally{rows[11]=lower;}
            }
            finally{data.maps[0]=old;}
        }

        public static void FrostPulsesPreserveLongerExistingEnemyControl(Campaign data)
        {
            // Drive the REAL Arcanist alternate-Q and world update loop:
            // all five field pulses must deal damage without shortening an
            // existing 2.4s stagger/Break timer to just 0.45s.
            var game=Fresh(data,2);
            game.Enter(1);game.enemies.Clear();
            game.save.x=12;game.save.y=14;
            game.save.technique=1;game.save.energy=100;
            var foe=new Enemy(96110,0,13,14,10000)
                {stun=2.4f,timer=9999f};
            game.enemies.Add(foe);
            game.Skill(new Point(1,0));
            Check(game.playerAction!=null&&
                game.playerAction.kind=="skill",
                "Arcanist alternate-Q was not accepted");
            Advance(game,.35f);
            Check(game.effects.Exists(e=>e.kind=="frost")&&
                foe.stun>1.95f&&foe.stun<2.4f,
                "First real frost contact incorrectly shortened previous Break stun");
            float afterFirst=foe.hp;
            Advance(game,.82f);
            Check(foe.hp<afterFirst&&foe.stun>1.1f,
                "Second damaging frost pulse overrode longer stun");
            // Exercise the ACTUAL internal pulse scheduler after setting up
            // the field through Q. No guessed wall-clock timing: hit-stop
            // legitimately steals slices from Tick and delays late pulses.
            foe.stun=0f;
            foe.stagger=0f;
            foe.staggerRecovery=100f;
            var pulse=typeof(GameCore).GetMethod("TickFrost",
                System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.NonPublic);
            Check(pulse!=null,"Actual frost field pulse scheduler missing");
            pulse.Invoke(game,new object[]{1.7f});
            Check(foe.stun>.44f&&foe.stun<.46f,
                "Later frost pulse failed to reapply exact 450ms chill after previous stun expired");
        }


        public static void EnemyArcherFindsMultiTileLateralFiringCorridor(Campaign data)
        {
            MapData original=data.maps[0];
            try
            {
                var rows=new string[25];
                for(int y=0;y<25;y++)
                {
                    char[] tiles=new string('.',50).ToCharArray();
                    for(int x=0;x<50;x++)
                        if(x<2||x>=48||y<2||y>=23)tiles[x]='#';
                    rows[y]=new string(tiles);
                }
                data.maps[0]=new MapData{id=0,
                    spawnX=10.5f,spawnY=9.5f,
                    rows=rows,objects=new WorldObject[0],
                    decor=new Decoration[0]};
                var g=Fresh(data);g.enemies.Clear();
                g.save.room=0;g.mode="world";g.invulnerable=100f;
                var sight=typeof(GameCore).GetMethod("ClearAttackPath",
                    System.Reflection.BindingFlags.Instance|
                    System.Reflection.BindingFlags.NonPublic);
                var lane=typeof(GameCore).GetMethod("PhysicalBowLaneFromFoot",
                    System.Reflection.BindingFlags.Instance|
                    System.Reflection.BindingFlags.NonPublic);
                var search=typeof(GameCore).GetMethod("ArcherPhysicalCornerSidestep",
                    System.Reflection.BindingFlags.Instance|
                    System.Reflection.BindingFlags.NonPublic);
                Check(sight!=null&&lane!=null&&search!=null,
                    "Exercise the original physical missile radius and real AI search, not a synthetic geometry estimate");
                Point from=new Point(),target=new Point(),heading=new Point();
                bool found=false;
                // The last stone just short of an otherwise reachable
                // victim can keep the arrow's radius in cover even AFTER
                // a first 0.55-tile sidestep. Scan fractional standing
                // roots on the actual GameCore projectile geometry to
                // select one deterministic, genuinely extended case.
                for(int wallColumn=13;wallColumn<=15&&!found;wallColumn++)
                for(int n=0;n<=18&&!found;n++)
                {
                    float y=9.70f+n*.015f;
                    rows[10]=new string('#',2)+new string('.',46)+new string('#',2);
                    char[] rock=rows[10].ToCharArray();
                    rock[wallColumn]='#';rows[10]=new string(rock);
                    Point foot=new Point(10.5f,y);
                    Point goal=new Point(16.5f,y);
                    if(!g.CanStand(foot)||!g.CanStand(goal)||
                        !(bool)sight.Invoke(g,new object[]{foot,goal})||
                        (bool)lane.Invoke(g,new object[]{foot,goal}))
                        continue;
                    Point north=foot+new Point(0,-.55f);
                    Point south=foot+new Point(0,.55f);
                    // The old one-tile-only scanner cannot solve this.
                    if((bool)lane.Invoke(g,new object[]{north,goal})||
                       (bool)lane.Invoke(g,new object[]{south,goal}))
                        continue;
                    Point candidate=(Point)search.Invoke(g,
                        new object[]{foot,goal});
                    if(candidate.Length<.5f)continue;
                    bool realExtended=false;
                    for(int ring=2;ring<=4;ring++)
                    {
                        Point end=foot+candidate*(ring*.55f);
                        if((bool)lane.Invoke(g,new object[]{end,goal}))
                        {realExtended=true;break;}
                    }
                    if(!realExtended)continue;
                    from=foot;target=goal;heading=candidate;found=true;
                }
                Check(found,
                    "The extended bow AI must find a real distant side-foot beyond both previously unusable 0.55-tile shots");
                Check(Math.Abs(heading.x)<.05f&&
                    Math.Abs(Math.Abs(heading.y)-1f)<.05f,
                    "Physical arrow search selects lateral motion instead of blindly crowding cover");
                Point outOfRange=new Point(42.5f,from.y);
                Check((bool)sight.Invoke(g,new object[]{from,outOfRange})&&
                    !(bool)lane.Invoke(g,new object[]{from,outOfRange})&&
                    ((Point)search.Invoke(g,new object[]{from,outOfRange})).Length<.001f,
                    "Beyond real arrow flight lifetime, ordinary archers must not run an irrelevant sideways aiming search");
                for(float step=.10f;step<.55f;step+=.10f)
                    Check(g.CanStand(from+heading*step),
                        "Long-range firing search cannot plan through a buried intermediate body");
                g.save.x=target.x;g.save.y=target.y;
                var archer=new Enemy(99147,1,from.x,from.y,500f)
                    {timer=.001f};
                archer.Face(target-from);g.enemies.Add(archer);
                g.Tick(.05f,new Point());
                // The first genuine movement Tick must publish an actor-
                // instance-owned destination. The current running foot is
                // never allowed to inherit a recycled spawn ID's plan.
                var cacheField=typeof(GameCore).GetField(
                    "archerCornerPlans",
                    System.Reflection.BindingFlags.Instance|
                    System.Reflection.BindingFlags.NonPublic);
                Check(cacheField!=null,"Extended ranged AI owns real transient per-enemy foot plans");
                object cache=cacheField.GetValue(g);
                var lookup=cache.GetType().GetMethod("TryGetValue");
                object[] planArguments={archer,null};
                Check((bool)lookup.Invoke(cache,planArguments),
                    "The live archer saved its physically verified side destination after moving");
                object plan=planArguments[1];
                var privateFields=System.Reflection.BindingFlags.Instance|
                    System.Reflection.BindingFlags.NonPublic|
                    System.Reflection.BindingFlags.Public;
                var planType=plan.GetType();
                var goalField=planType.GetField("goal",privateFields);
                var activeField=planType.GetField("active",privateFields);
                var targetField=planType.GetField("target",privateFields);
                Check(goalField!=null&&activeField!=null&&targetField!=null,
                    "The archer's real route plan contains goal, activity and target");
                Point committedFoot=(Point)goalField.GetValue(plan);
                Check((bool)activeField.GetValue(plan)&&
                    (committedFoot-from).Length>.55f&&
                    g.CanStand(committedFoot)&&
                    (bool)lane.Invoke(g,new object[]{committedFoot,target}),
                    "Real lateral pursuit must commit an extended, standable and physically shootable destination");
                Check(archer.state==0&&archer.moving&&archer.moveTime>0f&&
                    g.CanStand(archer.Position)&&
                    (archer.Position-from).Length>.015f&&
                    (archer.Position-from).y*heading.y>0f&&
                    !g.projectiles.Exists(p=>!p.friendly),
                    "Actual ranged enemy advances along the safe extended side path without firing early or teleporting");
                bool acquired=false;
                for(int tick=0;tick<80;tick++)
                {
                    g.Tick(.05f,new Point());
                    if(archer.state==0&&(bool)activeField.GetValue(plan))
                        Check(((Point)goalField.GetValue(plan)-committedFoot).Length<.001f,
                            "Lateral attacker must retain its original approved firing foot across real walk frames, not flip between aim directions each Tick");
                    Check(g.CanStand(archer.Position),
                        "Extended sideways approach must never bury the live enemy body in terrain");
                    if(archer.state==1){acquired=true;break;}
                }
                Check(acquired&&
                    (bool)lane.Invoke(g,new object[]{archer.Position,target})&&
                    g.save.version==2,
                    "Physically swept ordinary bow enemy eventually starts its real telegraphed ranged action only from a genuinely clear arrow lane, without save migration");
                // Even an earned extended side corridor is not a permanent
                // shooting license. A gate can seal the target lane after
                // its readable bow draw starts; the unpaid arrow must stop.
                Point midpoint=archer.Position+
                    (target-archer.Position)*.55f;
                int closedY=(int)Math.Floor(midpoint.y);
                int closedX=(int)Math.Floor(midpoint.x);
                string openLine=rows[closedY];
                char[] gate=openLine.ToCharArray();
                Check(gate[closedX]=='.',
                    "The pending archer ray has a real currently open center tile to close");
                gate[closedX]='#';rows[closedY]=new string(gate);
                Check(g.CanStand(archer.Position)&&g.CanStand(g.Position),
                    "Dynamic mid-windup stone preserves both live actor roots");
                g.Tick(.02f,new Point());
                Check(archer.state==0&&
                    !g.projectiles.Exists(p=>!p.friendly),
                    "An actual late closing wall cancels the extended-lane bow windup instead of releasing an invisible shot");
                rows[closedY]=openLine;
                archer.timer=.001f;
                g.Tick(.05f,new Point());
                Check(archer.state==1&&g.save.version==2,
                    "After the true side firing corridor reopens, the original ranged enemy immediately earns a new telegraphed aim");
                // Preserve the same Enemy instance, then move its target
                // beyond genuine projectile flight lifetime. A stale
                // intermediate walk plan must NEVER keep guiding it.
                var follow=typeof(GameCore).GetMethod("ArcherFollowCornerPlan",
                    System.Reflection.BindingFlags.Instance|
                    System.Reflection.BindingFlags.NonPublic);
                Check(follow!=null,"Actual live AI uses the retained-plan mover");
                Point farAway=new Point(42.5f,archer.y);
                Point obsoleteHeading=(Point)follow.Invoke(g,
                    new object[]{archer,farAway});
                Check(obsoleteHeading.Length<.01f&&
                    !(bool)activeField.GetValue(plan),
                    "When the target moves past true bow flight range the previously active side-foot route must retire immediately");
                var replacement=new Enemy(archer.id,1,from.x,from.y,100f);
                Point newActorHeading=(Point)follow.Invoke(g,
                    new object[]{replacement,target});
                object[] separatePlan={replacement,null};
                Check(newActorHeading.Length>.5f&&
                    (bool)lookup.Invoke(cache,separatePlan)&&
                    !Object.ReferenceEquals(plan,separatePlan[1]),
                    "A respawned foe reusing the same numeric ID cannot inherit an older actor's lateral attack route");
            }
            finally {data.maps[0]=original;}
        }

        public static void OccludedArcherLaneCancelsOnlyItsOwnRealArrow(Campaign data)
        {
            MapData previous=data.maps[0];
            try
            {
                var rows=new string[25];
                for(int y=0;y<25;y++)
                {
                    char[] cells=new string('#',25).ToCharArray();
                    if(y>=2&&y<=22)for(int x=2;x<=22;x++)cells[x]='.';
                    rows[y]=new string(cells);
                }
                data.maps[0]=new MapData{
                    id=0,spawnX=14.5f,spawnY=11.5f,rows=rows,
                    objects=new WorldObject[0],decor=new Decoration[0]};
                var g=Fresh(data,0);g.enemies.Clear();
                g.save.x=14.5f;g.save.y=11.5f;
                g.save.reducedMotion=true;
                var blockedArcher=new Enemy(99401,1,10.5f,10.5f,10000f)
                    {timer=0};
                var clearArcher=new Enemy(99402,1,10.5f,12.5f,10000f)
                    {timer=0};
                g.enemies.Add(blockedArcher);g.enemies.Add(clearArcher);
                g.Tick(.02f,new Point());
                Check(blockedArcher.state==1&&clearArcher.state==1&&
                    g.CanStand(blockedArcher.Position)&&
                    g.CanStand(clearArcher.Position),
                    "Two live bow enemies lock the same physically visible player in real AI windups");
                // Close only the upper diagonal. Both archers and the hero
                // remain on legal ground, and the LOWER archer keeps LOS.
                char[] closed=rows[10].ToCharArray();
                closed[12]='#';rows[10]=new string(closed);
                Check(g.Blocked(12.5f,10.5f)&&
                    g.CanStand(blockedArcher.Position)&&
                    g.CanStand(clearArcher.Position)&&g.CanStand(g.Position),
                    "Dynamic wall interrupts the upper locked arrow, not either caster's footing");
                g.Tick(.02f,new Point());
                Check(blockedArcher.state==0&&clearArcher.state==1&&
                    g.projectiles.Count==0,
                    "Only newly occluded archer abandons its unspent windup while the other continues");
                Advance(g,.64f);
                Check(g.projectiles.Exists(p=>!p.friendly&&
                    p.visual=="enemyArrow"&&!p.blockedAtSpawn)&&
                    !g.effects.Exists(f=>f.kind=="enemyRelease"&&
                        (f.position-blockedArcher.Position).Length<.1f),
                    "Unobstructed archer genuinely launches a traveling missile while the blocked archer emits no false release");
            }
            finally{data.maps[0]=previous;}
        }

        public static void EnemyArcherCannotCommitAttackFromBuriedBowHand(Campaign data)
        {
            MapData previous=data.maps[0];
            try
            {
                var rows=new string[25];
                for(int y=0;y<25;y++)
                {
                    char[] tiles=new string('#',25).ToCharArray();
                    if(y>=2&&y<=22)
                        for(int x=2;x<=22;x++)tiles[x]='.';
                    if(y==10)tiles[9]='#';
                    rows[y]=new string(tiles);
                }
                data.maps[0]=new MapData{
                    id=0,spawnX=10.5f,spawnY=7.5f,rows=rows,
                    objects=new WorldObject[0],decor=new Decoration[0]};
                var g=Fresh(data,0);
                g.enemies.Clear();
                g.save.x=10.5f;g.save.y=7.5f;
                g.save.reducedMotion=true;
                var archer=new Enemy(99511,1,10.5f,10.5f,10000f)
                    {timer=0f};
                g.enemies.Add(archer);
                Check(g.CanStand(g.Position)&&g.CanStand(archer.Position)&&
                    g.Blocked(9.5f,10.5f),
                    "Real side stone occupies an upward archer's bow hand, not the legal hero or archer feet");
                g.Tick(.02f,new Point());
                // With actual swept-missile availability checked BEFORE
                // attacking, a buried bow hand must not claim a locked
                // warning or start a 650ms cast it cannot finish.
                Check(archer.state==0&&g.projectiles.Count==0&&
                    !g.effects.Exists(f=>f.kind=="enemyRelease")&&
                    !g.effects.Exists(f=>f.kind=="danger"),
                    "Physically buried enemy bow must not commit an impossible warning or phantom release");
                char[] open=rows[10].ToCharArray();
                open[9]='.';rows[10]=new string(open);
                Advance(g,.25f);
                Check(archer.state==1,
                    "Restoring side-muzzle clearance accepts genuine archer re-aiming on its 220ms retry clock");
                Advance(g,.67f);
                Check(g.projectiles.Exists(p=>!p.friendly&&
                    p.visual=="enemyArrow"&&!p.blockedAtSpawn),
                    "A recovered ordinary enemy bow launches an actual traveling hostile arrow from the unobstructed weapon hand");
            }
            finally{data.maps[0]=previous;}
        }

        public static void OccludedEnemyArcherRetiresUnspentAttackAndRecovers(Campaign data)
        {
            MapData previous=data.maps[0];
            try
            {
                var rows=new string[25];
                for(int y=0;y<25;y++)
                {
                    char[] cells=new string('#',25).ToCharArray();
                    if(y>=2&&y<=22)for(int x=2;x<=22;x++)cells[x]='.';
                    rows[y]=new string(cells);
                }
                data.maps[0]=new MapData{
                    id=0,spawnX=14.5f,spawnY=10.5f,rows=rows,
                    objects=new WorldObject[0],decor=new Decoration[0]};
                var g=Fresh(data,0);g.enemies.Clear();
                g.save.x=14.5f;g.save.y=10.5f;
                g.save.reducedMotion=true;
                var archer=new Enemy(99411,1,10.5f,10.5f,10000f)
                    {timer=0};
                g.enemies.Add(archer);
                g.Tick(.02f,new Point());
                Check(archer.state==1&&g.CanStand(archer.Position),
                    "The physical archer acquired and locked an unobstructed real target");
                char[] closed=rows[10].ToCharArray();
                closed[12]='#';rows[10]=new string(closed);
                Check(g.CanStand(archer.Position)&&g.CanStand(g.Position),
                    "The newly closed wall lies only between caster and target, not on their feet");
                Advance(g,.12f);
                Check(archer.state==0&&g.projectiles.Count==0&&
                    archer.timer<=.22f &&
                    !g.effects.Exists(f=>f.kind=="enemyRelease"),
                    "Closing a lane cancels the 650ms draw and refunds prompt AI reacquisition without phantom arrows");
                closed[12]='.';rows[10]=new string(closed);
                // No manual timer reset: the repaired AI must naturally
                // re-evaluate the newly opened lane promptly.
                Advance(g,.25f);
                Check(archer.state==1,
                    "Restoring original line of sight accepts a genuine new windup within the 220ms retry cadence");
                Advance(g,.70f);
                Check(g.projectiles.Exists(p=>!p.friendly&&
                    p.visual=="enemyArrow"&&!p.blockedAtSpawn),
                    "After reopening, a true traveling hostile arrow launches from physical enemy bow");
            }
            finally{data.maps[0]=previous;}
        }

        public static void SealedEnemyGroundCancelsOnlyItsOwnCombatWarning(Campaign data)
        {
            // Create two REAL simultaneous heavy-enemy AI windups against
            // the same point. Only one foot becomes impassable mid-windup.
            // The other warning and attack must still land normally.
            MapData previous=data.maps[0];
            try
            {
                var rows=new string[25];
                for(int y=0;y<25;y++)
                {
                    char[] row=new string('#',25).ToCharArray();
                    if(y>=2&&y<=22)for(int x=2;x<=22;x++)row[x]='.';
                    rows[y]=new string(row);
                }
                data.maps[0]=new MapData{
                    id=0,spawnX=10.5f,spawnY=11.5f,rows=rows,
                    objects=new WorldObject[0],decor=new Decoration[0]};
                var g=Fresh(data,0);
                g.enemies.Clear();
                // The heavy slam's actual ground-radius is 1.5 tiles.
                // Stand inside the second live attack's true contact ring,
                // not merely within the 2.5-tile AI acquisition distance.
                g.save.x=10.5f;g.save.y=11.5f;
                g.save.health=100f;g.save.reducedMotion=true;
                // NewGame deliberately grants one second of spawn i-frames.
                // Expire only that protection so the surviving AI strike
                // proves actual damage, rather than a visual-only release.
                g.invulnerable=0f;
                var buried=new Enemy(99201,2,9.5f,10.5f,10000f){timer=0};
                var live=new Enemy(99202,2,11.5f,10.5f,10000f){timer=0};
                g.enemies.Add(buried);g.enemies.Add(live);
                g.Tick(.02f,new Point());
                var originalCues=g.effects.FindAll(f=>f.kind=="danger");
                Check(buried.state==1&&live.state==1&&
                    originalCues.Count==2&&
                    (originalCues[0].position-originalCues[1].position).Length<.001f,
                    "Two live AI heavy attacks issued overlapping REAL warnings against the player");
                char[] tile=rows[10].ToCharArray();
                tile[9]='#';rows[10]=new string(tile);
                Check(!g.CanStand(buried.Position)&&g.CanStand(live.Position)&&
                    g.CanStand(g.Position),
                    "Dynamic terrain closes only the first attacker's real feet, not the other opponent or hero");
                g.Tick(.02f,new Point());
                Check(buried.state==0&&live.state==1&&
                    !g.effects.Contains(originalCues[0])&&
                    g.effects.Contains(originalCues[1])&&
                    !buried.targetingReliefCivilian&&!buried.targetingWayfarer,
                    "Sealing the original caster retracts precisely its unspent warning and keeps the other identical danger ring");
                float before=g.save.health;
                Advance(g,.78f);
                Check(g.save.health<before&&g.save.health>=before-14.01f&&
                    buried.state==0&&g.projectiles.Count==0,
                    "The legitimate heavy enemy still lands one actual world hit while the buried attacker emits no phantom damage");
                // Let the same actor resume normally after terrain clears.
                tile[9]='.';rows[10]=new string(tile);
                buried.timer=0f;
                g.Tick(.02f,new Point());
                Check(buried.state==1&&g.CanStand(buried.Position)&&
                    g.effects.Exists(f=>f.kind=="danger"),
                    "Reopening terrain permits normal live AI telegraph and attack readiness again");
            }
            finally{data.maps[0]=previous;}
        }

        public static void SealedEnemyArcherCannotFireUntilGroundReopens(Campaign data)
        {
            MapData previous=data.maps[0];
            try
            {
                var rows=new string[25];
                for(int y=0;y<25;y++)
                {
                    char[] row=new string('#',25).ToCharArray();
                    if(y>=2&&y<=22)for(int x=2;x<=22;x++)row[x]='.';
                    rows[y]=new string(row);
                }
                data.maps[0]=new MapData{
                    id=0,spawnX=10.5f,spawnY=13.5f,rows=rows,
                    objects=new WorldObject[0],decor=new Decoration[0]};
                var g=Fresh(data,0);g.enemies.Clear();
                g.save.x=10.5f;g.save.y=13.5f;
                g.save.reducedMotion=true;
                var archer=new Enemy(99211,1,10.5f,9.5f,10000f){timer=0};
                g.enemies.Add(archer);
                g.Tick(.02f,new Point());
                Check(archer.state==1&&g.CanStand(archer.Position),
                    "An open-lane enemy archer begins its actual aimed windup");
                char[] sealedRow=rows[9].ToCharArray();
                sealedRow[10]='#';rows[9]=new string(sealedRow);
                Check(!g.CanStand(archer.Position),
                    "A real dynamic tile buries the archer only after its accepted windup");
                Advance(g,.90f);
                Check(archer.state==0&&g.projectiles.Count==0&&
                    !g.effects.Exists(f=>f.kind=="enemyRelease"),
                    "Enemy cannot shoot any hostile arrow, release cue or contact while physically trapped inside a wall");
                sealedRow[10]='.';rows[9]=new string(sealedRow);
                archer.timer=0f;
                g.Tick(.02f,new Point());
                Check(archer.state==1,
                    "The same archer can begin another genuine attack after its tile opens");
                Advance(g,.70f);
                Check(g.projectiles.Exists(p=>!p.friendly&&p.visual=="enemyArrow"&&
                    !p.blockedAtSpawn),
                    "Restored archer attack creates a true traveling hostile arrow from legal ground");
            }
            finally{data.maps[0]=previous;}
        }

        public static void DamageEarnedBreakDoesNotShortenExistingRecovery(Campaign data)
        {
            // Exercise an actual flying combat hit, not only timer fields.
            // A pre-existing long pause and a live warning that needs
            // canceling must each keep the correct post-Break semantics.
            MapData previous=data.maps[0];
            try
            {
                string[] rows=new string[25];
                for(int y=0;y<25;y++)
                {
                    char[] tiles=new string('#',25).ToCharArray();
                    if(y>=2&&y<=22)
                        for(int x=2;x<=22;x++)tiles[x]='.';
                    rows[y]=new string(tiles);
                }
                data.maps[0]=new MapData{
                    id=0,spawnX=10.5f,spawnY=11.5f,rows=rows,
                    objects=new WorldObject[0],decor=new Decoration[0]};
                for(int caseId=0;caseId<2;caseId++)
                {
                    var g=Fresh(data,0);g.enemies.Clear();
                    g.save.x=10.5f;g.save.y=11.5f;
                    g.save.reducedMotion=true;
                    var e=new Enemy(98881+caseId,0,12.5f,11.5f,10000f)
                    {
                        stagger=29f,
                        staggerRecovery=0f,
                        // Case 0: already under a longer earned stun/recovery.
                        // Case 1: windup is still committed and must be broken.
                        state=caseId==0?0:1,
                        timer=caseId==0?6.5f:.75f,
                        stun=caseId==0?2f:0f,
                        windupDuration=caseId==0?0f:.75f
                    };
                    g.enemies.Add(e);
                    var shot=new Projectile{
                        position=e.Position,velocity=new Point(1,0),
                        friendly=true,visual="arrow",life=1f,damage=10f};
                    g.projectiles.Add(shot);
                    g.Tick(.02f,new Point());
                    Check(e.hp<e.maxHp&&e.staggerRecovery>1.95f&&
                        g.effects.Exists(fx=>fx.kind=="break"&&
                            (fx.position-e.Position).Length<.01f),
                        "Real arrow damage earns exactly the original combat Break feedback and recovery");
                    Check(e.state==0&&e.stun>1.65f,
                        "Both a recovering enemy and a winding attacker receive true stagger interruption");
                    if(caseId==0)
                        Check(e.timer>6f,
                            "An already recovering enemy must not be awakened early by the hard-earned Break reward");
                    else
                        Check(e.timer>=1f&&e.releaseTime<=0f,
                            "A genuine active windup is still canceled and earns its full one-second post-Break window");
                }
            }
            finally{data.maps[0]=previous;}
        }

        public static void SenCommandPreservesLongEnemyRecoveryAndCancelsRealWindup(Campaign data)
        {
            MapData previous=data.maps[0];
            try
            {
                string[] rows=new string[25];
                for(int y=0;y<25;y++)
                {
                    char[] tiles=new string('#',25).ToCharArray();
                    if(y>=2&&y<=22)
                        for(int x=2;x<=22;x++)tiles[x]='.';
                    rows[y]=new string(tiles);
                }
                data.maps[0]=new MapData { id=0,
                    spawnX=10.5f,spawnY=11.5f,rows=rows,
                    objects=new WorldObject[0],decor=new Decoration[0] };
                var g=Fresh(data,2);g.enemies.Clear();
                g.save.x=10.5f;g.save.y=11.5f;
                g.save.reducedMotion=true;g.save.energy=100;
                g.save.flags.Add("companion_2");g.SelectCompanion(2);
                g.companionPosition=new Point(12.5f,11.5f);
                var recovering=new Enemy(98871,0,14.5f,11.5f,10000f)
                    {timer=6.5f,staggerRecovery=100f};
                var winding=new Enemy(98872,0,12.5f,13.5f,10000f)
                    {state=1,timer=.8f,windupDuration=.8f,
                     staggerRecovery=100f};
                g.enemies.Add(recovering);g.enemies.Add(winding);
                Check(g.CanStand(g.companionPosition)&&
                    g.CanStand(recovering.Position)&&
                    g.CanStand(winding.Position),
                    "Both a recovering enemy and an active attacker stand within Sen's genuine unobstructed caster radius");
                g.CommandCompanion();
                ActorAction cast=g.companionAction;
                Check(cast!=null&&cast.kind=="command"&&
                    cast.weapon==2&&!cast.released,
                    "Sen starts a real physical spell command with its original windup");
                Advance(g,.23f);
                Check(cast.released&&recovering.stun>1.15f&&
                    recovering.timer>6f&&recovering.state==0,
                    "Sen's real frost contact extends stun without shortening an enemy's longer preexisting recovery");
                Check(winding.state==0&&winding.timer>=1.15f&&
                    winding.stun>1.15f&&winding.releaseTime<=0f,
                    "The same command still interrupts a real attack windup and grants its intended 1.2s recovery");
                float hpRecovering=recovering.hp, hpWinding=winding.hp;
                Advance(g,.9f);
                Check(recovering.hp<hpRecovering&&winding.hp<hpWinding&&
                    recovering.timer>4.9f,
                    "Later true frost damage pulses still affect both targets without waking the originally resting enemy early");
            }
            finally{data.maps[0]=previous;}
        }

        public static void SkillCompanionAndBreakControlNeverShortensExistingStun(Campaign data)
        {
            // Real normal Q across sword and mage. An already earned longer
            // interrupt must never become a shorter one on the next contact.
            foreach(int profession in new[]{0,2})
            {
                var g=Fresh(data,profession);g.Enter(1);g.enemies.Clear();
                g.save.x=12;g.save.y=14;g.save.energy=100;
                var foe=new Enemy(96200+profession,0,13,14,10000)
                    {stun=3.2f,timer=9999f,staggerRecovery=100f};
                g.enemies.Add(foe);
                g.Skill(new Point(1,0));Advance(g,.35f);
                Check(foe.stun>2.75f&&foe.stun<=3.2f,
                    "Original profession "+profession+
                    " skill replaced a stronger existing stagger with a shorter stun");
            }
            // Vanguard alternate technique is a different real release path.
            {
                var g=Fresh(data,0);g.Enter(1);g.enemies.Clear();
                g.save.x=12;g.save.y=14;g.save.energy=100;
                g.save.technique=1;
                var foe=new Enemy(96211,0,13,14,10000)
                    {stun=3.3f,timer=9999f,staggerRecovery=100f};
                g.enemies.Add(foe);g.Skill(new Point(1,0));Advance(g,.35f);
                Check(foe.stun>2.85f,
                    "Vanguard alternate guard shockwave prematurely ended longer enemy control");
            }
            // Sen's genuine commanded field has its own instant 1.4s stop,
            // in addition to the later Q-like frost pulse damage.
            {
                var g=Fresh(data,2);g.Enter(1);g.enemies.Clear();
                g.save.x=12;g.save.y=14;
                g.save.flags.Add("companion_2");
                g.SelectCompanion(2);
                var foe=new Enemy(96212,0,13,14,10000)
                    {stun=3.5f,timer=9999f,staggerRecovery=100f};
                g.enemies.Add(foe);g.CommandCompanion();Advance(g,.23f);
                Check(foe.stun>3.1f,
                    "Sen companion command cut the already established stun to 1.4 seconds");
            }
            // A Break is earned by accumulated real damage, but should
            // never erase an even longer stun from a different source.
            {
                var g=Fresh(data);g.Enter(1);g.enemies.Clear();
                g.save.x=12;g.save.y=14;
                var foe=new Enemy(96213,0,13,14,10000)
                    {stun=4f,timer=9999f,stagger=29f};
                g.enemies.Add(foe);
                g.projectiles.Add(new Projectile{position=foe.Position,
                    velocity=new Point(1,0),life=1f,damage=40f,
                    friendly=true,visual="arrow"});
                g.Tick(.01f,new Point());
                Check(foe.staggerRecovery>0f&&foe.stun>3.8f,
                    "Real damage Break overwrote longer existing control with only 1.7 seconds");
            }
        }

        public static void ControlEffectsInterruptEnemyWindupWithoutBlindRelease(Campaign data)
        {
            // An Arcanist attack interrupts the real damage-timed warning;
            // no stale one-frame windup can release as soon as stun expires.
            foreach(int profession in new[]{0,2})
            {
                var g=Fresh(data,profession);g.Enter(1);g.enemies.Clear();
                g.save.x=12;g.save.y=14;g.save.energy=100;
                var foe=new Enemy(96300+profession,0,13,14,10000)
                {
                    state=1,timer=.31f,windupDuration=.65f,
                    stun=0f,target=new Point(12,14),
                    facing=new Point(-1,0),targetingWayfarer=true
                };
                g.enemies.Add(foe);
                g.Skill(new Point(1,0));Advance(g,.34f);
                Check(foe.state==0&&foe.timer>.35f&&foe.stun>.34f&&
                    !foe.targetingWayfarer,
                    "Real Q control failed to cancel frozen enemy attack warning and civilian lock");
                Advance(g,.65f);
                Check(foe.state==0,
                    "Enemy immediately fired a stale near-contact telegraph after stun wore off");
            }
            // A non-control hit is not a magical interrupt: keep its
            // already-locked windup (original combat/hit-flash contract).
            {
                var g=Fresh(data);g.Enter(1);g.enemies.Clear();
                g.save.x=12;g.save.y=14;
                var foe=new Enemy(96305,0,13,14,10000)
                    {state=1,timer=.5f,windupDuration=.65f,
                     facing=new Point(-1,0),target=new Point(12,14)};
                g.enemies.Add(foe);
                g.projectiles.Add(new Projectile{position=foe.Position,
                    velocity=new Point(1,0),life=1,damage=1f,
                    friendly=true,visual="arrow"});
                g.Tick(.01f,new Point());
                Check(foe.state==1&&foe.stun<=0,
                    "Ordinary cosmetic hit flash should not cancel actual attack windup");
            }
        }

        public static void EnemyReactionsConsumeOnlyTheirRemainingFrameBudget(Campaign data)
        {
            // A hit-recoil, stun and post-release wait overlap. Their union
            // blocks AI for max(10,40,20)ms, not for the whole 50ms or the
            // sum of all timers. The remaining 10ms moves the real raider.
            var g=Fresh(data);g.Enter(1);g.enemies.Clear();
            g.save.x=12;g.save.y=14;
            var foe=new Enemy(96901,0,18,14,10000)
            {
                stun=.04f,hitTime=.01f,releaseTime=.02f,
                recoil=new Point(0,1),timer=1f
            };
            g.enemies.Add(foe);
            float beforeMove=foe.moveTime;
            g.Tick(.05f,new Point());
            Check(foe.x<18f&&foe.x>17.96f,
                "Enemy moved a full 50ms instead of 10ms after stun expired");
            Check(foe.y>14.005f&&foe.y<14.02f,
                "Last 10ms of real hit knockback vanished at frame boundary");
            Check(foe.timer>.98f&&foe.timer<.995f,
                "Enemy cooldown spent blocked stun time as if it were active");
            Check(foe.moveTime>beforeMove&&foe.moveTime<beforeMove+.05f,
                "Enemy native gait advanced farther than actual resumed body root");
            // A 35ms-ready archer cannot begin a new attack when its
            // 40ms recovery leaves only 10ms active in the same 50ms tick.
            g.enemies.Clear();
            var archer=new Enemy(96902,1,17,14,10000)
            {
                stun=.04f,timer=.035f
            };
            g.enemies.Add(archer);
            g.Tick(.05f,new Point());
            Check(archer.state==0&&archer.timer>.02f,
                "Archer started an untelegraphed shot using time spent stunned");
            g.Tick(.05f,new Point());
            Check(archer.state==1&&archer.windupDuration>.5f,
                "Enemy failed to begin a genuine warning once its free recovery elapsed");
        }

        public static void EnemyInterruptRetractsOnlyItsOwnRescueDanger(Campaign data)
        {
            var g=Fresh(data);g.Enter(1);g.enemies.Clear();
            var first=new Enemy(96915,2,15,14,10000)
                {state=1,timer=.32f,stun=0f,targetingReliefCivilian=true};
            var second=new Enemy(96916,2,15.5f,14,10000)
                {state=1,timer=.32f,stun=0f,targetingReliefCivilian=true};
            g.enemies.Add(first);g.enemies.Add(second);
            // Same target and mission category: location/tag removal would
            // destroy both real warnings, unfairly hiding the second strike.
            Point target=new Point(12,14);
            var owned=new Effect("danger",target,.32f)
                {radius=1.5f,reliefMissionOwned=true};
            var unrelated=new Effect("danger",target,.32f)
                {radius=1.5f,reliefMissionOwned=true};
            var environmental=new Effect("danger",target,.32f)
                {radius=1.5f};
            g.effects.Add(owned);g.effects.Add(unrelated);
            g.effects.Add(environmental);
            var flags=System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.NonPublic;
            var field=typeof(GameCore).GetField("rescueWindupEffects",flags);
            Check(field!=null,
                "Production rescue windup warning must have identity-safe ownership");
            var table=(System.Runtime.CompilerServices.ConditionalWeakTable<
                Enemy,Effect>)field.GetValue(g);
            table.Add(first,owned);table.Add(second,unrelated);
            var interrupt=typeof(GameCore).GetMethod("ApplyEnemyControl",flags);
            Check(interrupt!=null,"Production enemy control path missing");
            interrupt.Invoke(g,new object[]{first,1.4f});
            Check(first.state==0&&!first.targetingReliefCivilian&&
                !g.effects.Contains(owned)&&g.effects.Contains(unrelated)&&
                g.effects.Contains(environmental)&&second.state==1,
                "Interrupted rescue attack left a ghost ring or erased unrelated warnings");
            // The other real attacking enemy still has its own danger cue.
            interrupt.Invoke(g,new object[]{second,.45f});
            Check(!g.effects.Contains(unrelated)&&
                g.effects.Contains(environmental)&&
                !second.targetingReliefCivilian&&second.state==0,
                "Second stunned enemy failed to retire only its own live telegraph");

            // The OTHER entry point into full control is damage-earned
            // poise Break, which must retract exactly the same pending
            // civilian target circle. This actually executes projectile ->
            // Hit -> stagger threshold -> control helper -> cue ownership.
            var staggered=new Enemy(96917,0,13,14,10000)
            {
                state=1,timer=.21f,windupDuration=.65f,
                target=target,targetingReliefCivilian=true,
                stagger=29f,staggerRecovery=0f
            };
            g.enemies.Add(staggered);
            var staggerCue=new Effect("danger",target,.21f)
                {radius=1.5f,reliefMissionOwned=true};
            g.effects.Add(staggerCue);
            table.Add(staggered,staggerCue);
            g.projectiles.Add(new Projectile
            {
                position=staggered.Position,velocity=new Point(1,0),
                life=1f,damage=20f,friendly=true,visual="arrow"
            });
            g.Tick(.01f,new Point());
            Check(staggered.staggerRecovery>0f&&staggered.state==0&&
                !staggered.targetingReliefCivilian&&
                !g.effects.Contains(staggerCue)&&
                g.effects.Contains(environmental),
                "Real projectile poise Break failed to retract its canceled civilian danger ring");
        }

        public static void LethalHitRetiresOwnedRescueWarningWithoutFakeBreak(Campaign data)
        {
            var g=Fresh(data);g.Enter(1);g.enemies.Clear();
            var dying=new Enemy(96930,0,13,14,7f)
            {
                state=1,timer=.4f,windupDuration=.65f,
                target=new Point(12,14),targetingReliefCivilian=true,
                // A previously spent stagger gate prevents a second Break.
                // The cue must be cleared by actual DEATH, not the Break path.
                staggerRecovery=100f
            };
            g.enemies.Add(dying);
            var warning=new Effect("danger",dying.target,.4f)
                {radius=1.5f,reliefMissionOwned=true};
            var other=new Effect("danger",dying.target,.4f)
                {radius=1.5f,reliefMissionOwned=true};
            g.effects.Add(warning);g.effects.Add(other);
            var field=typeof(GameCore).GetField("rescueWindupEffects",
                System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.NonPublic);
            Check(field!=null,"Missing enemy-identity warning owner");
            var owners=(System.Runtime.CompilerServices.ConditionalWeakTable<
                Enemy,Effect>)field.GetValue(g);
            owners.Add(dying,warning);
            g.projectiles.Add(new Projectile
            {
                position=dying.Position,velocity=new Point(1,0),
                life=1f,damage=30f,friendly=true,visual="arrow"
            });
            g.Tick(.01f,new Point());
            Check(!g.enemies.Contains(dying)&&
                !g.effects.Contains(warning)&&g.effects.Contains(other)&&
                g.fallen.Exists(body=>body.actor==dying)&&
                !g.effects.Exists(effect=>effect.kind=="break"),
                "A lethal unbroken raider left a phantom rescue warning or lost its normal corpse");
        }

        public static void PlayerHitRecoveryUsesOnlyTrueFreeMovementAndContactTime(Campaign data)
        {
            var g=Fresh(data);g.Enter(1);g.enemies.Clear();
            g.save.x=12f;g.save.y=14f;
            g.playerRecoil=new Point(0,1);
            g.recoilSpeed=1f;g.playerHitTime=.04f;
            float stride=g.walkDistance;
            g.Tick(.05f,new Point(1,0));
            Check(g.save.x>12.035f&&g.save.x<12.075f&&
                g.save.y>14.03f&&g.save.y<14.05f,
                "Last 40ms of hit recoil granted 50ms free walking or lost the final push");
            Check(g.ActuallyMoving&&g.PlayerPose>=2&&g.PlayerPose<=3&&
                g.walkDistance>stride&&g.walkDistance<stride+.075f,
                "Completed hit recoil did not resume the rooted walk for ONLY 10ms");

            var noInput=Fresh(data);noInput.Enter(1);noInput.enemies.Clear();
            noInput.save.x=12f;noInput.save.y=14f;
            noInput.playerHitTime=.04f;
            noInput.playerRecoil=new Point(0,1);
            noInput.recoilSpeed=1f;
            int contacts=noInput.footContacts;
            noInput.Tick(.05f,new Point());
            Check(noInput.save.y>14.03f&&!noInput.ActuallyMoving&&
                noInput.footContacts==contacts&&
                noInput.HeroAnimationState==0,
                "Final recoil generated a phantom run pose or unearned footsteps");

            // A sword action already approaching its contact must be
            // PAUSED by real hit recovery, without releasing damage early.
            var attack=Fresh(data);attack.Enter(1);attack.enemies.Clear();
            attack.save.x=12f;attack.save.y=14f;
            attack.Attack(new Point(1,0));
            ActorAction action=attack.playerAction;
            Check(action!=null,"Actual hero swing missing");
            action.elapsed=action.contact-.02f;
            attack.playerHitTime=.04f;
            attack.playerRecoil=new Point(0,0);
            attack.Tick(.05f,new Point());
            Check(attack.playerAction==action&&!action.released&&
                action.elapsed<action.contact-.005f,
                "Player contact crossed 20ms boundary after only 10ms of free time");
            attack.Tick(.02f,new Point());
            Check(action.released,
                "Deferred hero contact never released on the next real free tick");
        }

        public static void LockedFarEnemyAttacksPreventUnsafeFastTravel(Campaign data)
        {
            // Main campaign paved road, not an invented empty collision
            // fixture. The Boss stands eight tiles away, beyond normal
            // SafeTravel proximity, but its real target is the hero.
            var g=Fresh(data);g.Enter(1);g.enemies.Clear();
            g.projectiles.Clear();g.hazards.Clear();
            g.save.x=12f;g.save.y=14f;
            var enemy=new Enemy(97601,2,20f,14f,10000,true)
            {
                state=1,timer=.31f,windupDuration=1.1f,
                target=new Point(12f,14f),facing=new Point(-1,0)
            };
            Check(g.CanStand(g.Position)&&g.CanStand(enemy.Position),
                "Long-range warning test requires legal road footprints");
            g.enemies.Add(enemy);
            Check(!g.SafeTravel,
                "Remote Boss locked damage at hero but fast travel erased its windup");
            enemy.target=new Point(24f,14f);
            Check(g.SafeTravel,
                "A remote Boss attacking far away should not block safe travel");
            enemy.target=g.Position;
            enemy.state=0;
            Check(g.SafeTravel,
                "A distant idle enemy with no committed strike cannot block travel");
            enemy.state=1;enemy.boss=false;enemy.kind=1;
            Check(!g.SafeTravel,
                "Remote archer has already locked an incoming shot but travel remained enabled");
            enemy.state=0;
            Check(g.SafeTravel,
                "Completed archer attack must restore travel eligibility");
        }

        public static void ApproachingFarMissileBlocksTravelButOutgoingDoesNot(Campaign data)
        {
            var g=Fresh(data);g.Enter(1);g.enemies.Clear();
            g.hazards.Clear();g.projectiles.Clear();
            g.save.x=12f;g.save.y=14f;
            var incoming=new Projectile
            {
                position=new Point(20f,14f),
                velocity=new Point(-10f,0),
                life=1.2f,damage=14f,visual="enemyArrow",
                friendly=false
            };
            Check(g.CanStand(incoming.position)&&g.SafeTravel,
                "Projectile interception test needs a real open road and safe baseline");
            g.projectiles.Add(incoming);
            Check(!g.SafeTravel,
                "Fast travel erased a distant arrow that will cross the hero's real vicinity");
            incoming.velocity=new Point(10f,0);
            Check(g.SafeTravel,
                "Already outbound hostile arrow must not prevent unrelated travel");
            incoming.velocity=new Point(0,-10f);
            Check(g.SafeTravel,
                "A parallel projectile whose path misses the hero may not stop travel");
            incoming.velocity=new Point(-10f,0);
            incoming.life=.1f;
            Check(g.SafeTravel,
                "Expiring arrow unable to reach the safe zone must not block travel");
            incoming.life=1.2f;incoming.friendly=true;
            Check(g.SafeTravel,
                "A friendly or sword-reflected projectile may not block its owner");
            incoming.friendly=false;incoming.damage=0f;
            Check(g.SafeTravel,
                "A harmless visual projectile may not masquerade as active combat");
        }

        public static void CanonicalHeroAnimatorTracksRealCoreActionsAcrossProfessions(Campaign data)
        {
            for (int profession = 0; profession < 3; profession++)
            {
                GameCore g = Quiet(data, profession);
                g.enemies.Clear();
                Check(g.HeroAnimationState == 0,
                    "Canonical blond hero must start in idle before any actual player action");
                g.Attack(new Point(1, 0));
                Check(g.playerAction != null && g.HeroAnimationState == 2,
                    "All classes enter attack state from an actual accepted combat input");
                if (profession == 0)
                {
                    // Sword heavy/combo attacks also use visual pose 6/7;
                    // pose numbers must NOT be confused with mage casting.
                    g.playerAction.combo = 2;
                    Check(g.PlayerPose >= 6 && g.HeroAnimationState == 2,
                        "Heavy sword contact remains an attack, not a fake spell state");
                }
                g.Dash(new Point(1, 0));
                Check(g.HeroAnimationState == 4 && g.playerAction == null,
                    "Real dodge overrides and cancels previous attack animation");
                Advance(g, .85f);
                Check(g.HeroAnimationState == 0,
                    "Completed dodge recovers to idle without phantom action");
                g.Skill(new Point(0, -1));
                Check(g.playerAction != null && g.playerAction.kind == "skill" &&
                      g.HeroAnimationState == 3,
                    "All class skills use the real cast action state");
                g.playerHitTime = .18f;
                Check(g.HeroAnimationState == 5,
                    "Taking damage interrupts cast with a visible hurt pose");
                g.deathTime = .9f;
                Check(g.HeroAnimationState == 6,
                    "Real player death has higher animation priority than recoil");
            }
        }

        public static void ReactionClockTraversesBothNewMotionCells(Campaign data)
        {
            // Run the existing three-original-pose GameCore clock through
            // the new FIVE-visual-Sprite response: 0,1,2,3,4.
            // Dash travel, invulnerability and knockback remain untouched.
            var game = Quiet(data, 0);
            game.enemies.Clear();
            game.Dash(new Point(1, 0));
            Check(game.HeroAnimationState == 4 &&
                game.HeroAnimationFrameCount(0) == 3,
                "Dodge keeps the authoritative three-pose gameplay clock");
            float[] timerSamples = { .18f, .135f, .09f, .045f, .001f };
            int last = -1;
            foreach (float remaining in timerSamples)
            {
                game.dashTime = remaining;
                float position = game.HeroAnimationPlaybackPosition(0);
                int spriteIndex = (int)Math.Floor(position * 2f + .20f);
                Check(spriteIndex >= last && spriteIndex >= 0 && spriteIndex <= 4,
                    "Dodge must progress through five rooted Sprite cells");
                last = spriteIndex;
            }
            Check(last == 4, "Dodge must return to accepted idle frame");
            game.dashTime = 0f;
            game.playerHitTime = .18f;
            Check(game.HeroAnimationState == 5 &&
                game.HeroAnimationFrameCount(0) == 3,
                "Hit stun keeps the authoritative three-pose gameplay clock");
            last = -1;
            foreach (float remaining in timerSamples)
            {
                game.playerHitTime = remaining;
                int spriteIndex = (int)Math.Floor(
                    game.HeroAnimationPlaybackPosition(0) * 2f + .20f);
                Check(spriteIndex >= last && spriteIndex >= 0 && spriteIndex <= 4,
                    "Hit recovery must progress through five rooted Sprite cells");
                last = spriteIndex;
            }
            Check(last == 4, "Hit recovery must return to accepted idle frame");
        }

        public static void HeroWindupActuallySamplesAnticipationBeforeContact(Campaign data)
        {
            // The production 7-cell Attack/Cast strips are arranged:
            // idle, intermediate, windup, anticipation, RELEASE, recovery, idle.
            // Contact must remain hidden until GameCore emits real damage.
            for (int profession = 0; profession < 3; profession++)
                foreach (bool skill in new[] { false, true })
                {
                    var g = Quiet(data, profession);
                    g.enemies.Clear();
                    if (skill) g.Skill(new Point(1, 0));
                    else g.Attack(new Point(1, 0));
                    ActorAction a = g.playerAction;
                    Check(a != null && !a.released, "Real action must begin in windup");
                    a.elapsed = a.contact * .88f;
                    for (int facing = 0; facing < 3; facing++)
                    {
                        float position = g.HeroAnimationPlaybackPosition(facing);
                        int nativeIndex = (int)Math.Floor(position * 2f + .20f);
                        Check(g.HeroAnimationState == (skill ? 3 : 2) &&
                            g.HeroAnimationFrame(facing) < 2 &&
                            position > 1.4f && position < 2f &&
                            nativeIndex == 3,
                            "All three facings must show anticipation cell 3, "
                            + "never contact cell 4, before release");
                    }
                    g.Tick(a.contact - a.elapsed + .005f, new Point());
                    Check(a.released && g.playerAction == a,
                        "Authoritative GameCore must release at the actual contact time");
                    for (int facing = 0; facing < 3; facing++)
                    {
                        float position = g.HeroAnimationPlaybackPosition(facing);
                        Check(position >= 2f && g.HeroAnimationFrame(facing) >= 2,
                            "Only the actual damage contact may display release cell 4");
                    }
                }
        }

        public static void RealHeroAnimatorFramesFollowActionReleaseAtAllFrameRates(Campaign data)
        {
            foreach (float dt in new[] { .01f, .02f, .05f })
                for (int profession = 0; profession < 3; profession++)
                {
                    GameCore game = Quiet(data, profession);
                    game.enemies.Clear();
                    Check(game.HeroAnimationFrameCount(0) == 1 &&
                        game.HeroAnimationFrame(0) == 0, "Idle has one rooted accepted frame");
                    game.Attack(new Point(1, 0));
                    ActorAction attack = game.playerAction;
                    Check(attack != null && game.HeroAnimationState == 2,
                        "An actual accepted attack starts production Animator's attack state");
                    Check(game.HeroAnimationFrameCount(0) == 4 &&
                        game.HeroAnimationFrameCount(1) == 4 &&
                        game.HeroAnimationFrameCount(2) == 4,
                        "All three attack facings use four phases: idle, windup, contact, recovery");
                    int windupSamples = 0, releasedSamples = 0;
                    for (int step = 0; step < 50 && game.playerAction == attack; step++)
                    {
                        if (!attack.released)
                        {
                            Check(game.HeroAnimationFrame(0) < 2 &&
                                game.HeroAnimationFrame(1) < 2,
                                "No facing may display a contact pose before real damage release");
                            windupSamples++;
                        }
                        else
                        {
                            Check(game.HeroAnimationFrame(0) >= 2 &&
                                game.HeroAnimationFrame(1) >= 2,
                                "Every attack contact/recovery frame follows GameCore damage release");
                            releasedSamples++;
                        }
                        game.Tick(dt, new Point());
                    }
                    Check(windupSamples > 0 && releasedSamples > 0,
                        "Each profession has windup AND release animation at " + dt);
                    GameCore caster = Quiet(data, profession);
                    caster.enemies.Clear();
                    caster.Skill(new Point(1, 0));
                    Check(caster.playerAction != null && caster.HeroAnimationState == 3 &&
                        caster.HeroAnimationFrameCount(0) == 4 &&
                        caster.HeroAnimationFrameCount(1) == 4 &&
                        caster.HeroAnimationFrameCount(2) == 4 &&
                        caster.HeroAnimationFrame(0) == 0,
                        "Skill begins at the rooted idle pose in all three four-phase Cast clips");
                    for (int frame = 0; frame < 50 && !caster.playerAction.released; frame++)
                    {
                        Check(caster.HeroAnimationFrame(0) < 2,
                            "Windup cannot show a skill contact before authoritative release");
                        caster.Tick(dt, new Point());
                    }
                    Check(caster.HeroAnimationState == 3 && caster.HeroAnimationFrame(0) == 2,
                        "Cast contact pose follows only the real GameCore skill release");
                    ActorAction castAction = caster.playerAction;
                    for (int frame = 0; frame < 50 && caster.playerAction == castAction &&
                        castAction.elapsed < castAction.contact + .21f; frame++)
                        caster.Tick(dt, new Point());
                    Check(caster.playerAction == castAction &&
                        caster.HeroAnimationFrame(0) == 3,
                        "Cast finishes on identical rooted idle pixels before action expiration");
                    GameCore runner = Quiet(data, profession);
                    runner.save.x = 10; runner.save.y = 14;
                    runner.Tick(.05f, new Point(1, 0));
                    Check(runner.HeroAnimationState == 1 &&
                        runner.HeroAnimationFrame(0) == runner.WalkFrame % 4,
                        "Animator run samples the real walking distance-derived frame");
                }
        }

        private static float OpeningStrike(Campaign data, float step, bool boss, Point facing, float recovery, out GameCore game, out Enemy target)
        {
            game = Quiet(data);
            game.save.seed = 1; game.save.english = true;
            target = new Enemy(88001, boss ? 0 : 2, 13, 14, 900, boss)
            { facing = facing, timer = 999, releaseTime = recovery };
            game.enemies.Add(target);
            game.Attack(new Point(1, 0));
            for (int frame = 0; frame < 60 && target.hp >= 900; frame++)
                game.Tick(step, new Point());
            Check(target.hp < 900, "Real sword contact must hit the production enemy");
            return 900 - target.hp;
        }

        public static void TimedRecoveryAndArmoredFlanksCreateCombatOpenings(Campaign data)
        {
            foreach (float step in new[] { .01f, .02f, .05f })
            {
                GameCore normalGame, flankGame, guardedGame, bossGame, openGame, staleGame;
                Enemy normal, flank, guarded, boss, opening, stale;
                float neutral = OpeningStrike(data, step, false, new Point(0, 1), 0, out normalGame, out normal);
                float rear = OpeningStrike(data, step, false, new Point(1, 0), 0, out flankGame, out flank);
                float front = OpeningStrike(data, step, false, new Point(-1, 0), 0, out guardedGame, out guarded);
                Check(rear > neutral * 1.24f && rear < neutral * 1.27f && front < neutral * .42f,
                    "Heavy armor must reward a rear flank without weakening frontal guard");
                Check(flank.stagger >= normal.stagger + 5.9f && flankGame.effects.Exists(e => e.text == "FLANK"),
                    "Flanking adds poise pressure and player feedback");
                float ordinaryBoss = OpeningStrike(data, step, true, new Point(0, 1), 0, out bossGame, out boss);
                float recoveryBoss = OpeningStrike(data, step, true, new Point(0, 1), .5f, out openGame, out opening);
                float expiredBoss = OpeningStrike(data, step, true, new Point(0, 1), .04f, out staleGame, out stale);
                Check(recoveryBoss > ordinaryBoss * 1.24f && recoveryBoss < ordinaryBoss * 1.27f &&
                    Math.Abs(expiredBoss - ordinaryBoss) < .001f,
                    "Only a contact during live post-attack recovery earns the opening");
                Check(openGame.effects.Exists(e => e.text == "OPEN") &&
                    !bossGame.effects.Exists(e => e.text == "OPEN") &&
                    !staleGame.effects.Exists(e => e.text == "OPEN"),
                    "Opening feedback is never awarded to stale contacts");
            }
        }

        public static void HeldAttacksPreserveSkillContactPoses(Campaign data)
        {
            foreach(float step in new[]{.01f,.02f,.05f}) for(int profession=0;profession<3;profession++)
            {
                var g=Quiet(data,profession);g.Skill(new Point(1,0));ActorAction skill=g.playerAction;
                bool sawContact=false,sawFollowup=false;
                for(int frame=0;frame<100;frame++)
                {
                    g.Tick(step,new Point());bool active=g.playerAction==skill&&g.PlayerPhase==ActionPhase.Active;
                    g.Attack(new Point(0,-1)); // Production input samples held attack after Tick.
                    if(active)
                    {
                        sawContact=true;
                        Check(g.playerAction==skill&&g.PlayerPose==7,"Held attack cannot replace Q's visible contact for profession "+profession+" at timestep "+step);
                    }
                    if(g.playerAction!=skill)
                    {
                        Check(skill.released&&skill.elapsed+.00001f>=skill.contact+.1f,"A followup waits for the complete skill contact interval");
                        sawFollowup=g.playerAction!=null&&g.playerAction.kind=="attack";break;
                    }
                }
                Check(sawContact&&sawFollowup,"Q shows its release pose, then continuous attack resumes without another button press");
            }
            var sameFrame=Quiet(data);sameFrame.Skill(new Point(0,-1));sameFrame.Attack(new Point(1,0));
            Check(sameFrame.playerAction.kind=="skill"&&sameFrame.save.energy==60,"Discrete Q has priority over the held attack sampled in the same frame");
        }
        public static void BufferedSkillsRetainIntentAndJoinRecovery(Campaign data)
        {
            foreach(float step in new[]{.01f,.02f,.05f}) for(int profession=0;profession<3;profession++) foreach(bool afterContact in new[]{false,true})
            {
                var g=Quiet(data,profession);g.Attack(new Point(1,0));ActorAction attack=g.playerAction;
                Advance(g,attack.contact+(afterContact?.02f:-.04f));
                Point target=new Point(21,12),direction=(target-g.Position).Normalized;
                g.SetAimTarget(target);g.Skill(direction);
                Check(g.playerAction==attack&&g.skillCooldown<=0&&g.save.energy==100,"Queuing Q during preparation or contact cannot cancel the visible attack or spend energy early");
                Check(g.SkillQueued&&(g.SkillQueuedDirection-direction).Length<.001f&&
                    g.SkillQueuedTarget.HasValue&&
                    (g.SkillQueuedTarget.Value-target).Length<.001f,
                    "Buffered Q world reticle must expose the actual accepted Ranger target without paying for an early cast");
                g.SetAimTarget(new Point(5,18));g.SetAim(new Point(-1,0));g.save.talents[0]++;
                ActorAction skill=null;
                for(int frame=0;frame<100;frame++)
                {
                    g.Tick(step,new Point(),new Point(-1,0));g.Attack(new Point(-1,0));
                    if(g.playerAction!=attack)
                    {
                        skill=g.playerAction;
                        Check(attack.elapsed+.00001f>=attack.contact+.1f,"A buffered Q preserves the preceding attack's complete contact interval");break;
                    }
                }
                Check(skill!=null&&skill.kind=="skill","One discrete Q near contact starts at recovery despite continuously held attack");
                Check((skill.direction-direction).Length<.001f&&skill.target.HasValue&&(skill.target.Value-target).Length<.001f,"Buffered Q keeps the original direction and pointer target");
                Check(Math.Abs(skill.damage-g.Damage)<.001f&&Math.Abs(skill.impact.criticalChance-.085f)<.001f,"Combat values are captured when Q actually starts, after the queued intent");
                Check(Math.Abs(g.save.energy-60)<.001f&&Math.Abs(g.skillCooldown-3.5f)<.001f,"Buffered Q pays its original cost and starts cooldown exactly once");
                if(profession==1)
                {
                    while(!skill.released)g.Tick(.01f,new Point(),new Point(-1,0));
                    var volley=g.projectiles.FindAll(p=>p.technique);
                    Point expected=(target-GameCore.WeaponOrigin(g.Position,1,direction)).Normalized;
                    Check(volley.Count==5&&(volley[2].velocity.Normalized-expected).Length<.001f,"The actual queued volley uses the retained target and authored muzzle");
                }
            }
            var latest=Quiet(data,1);latest.Dash(new Point(1,0));latest.Skill(new Point(0,-1));
            Point lastTarget=new Point(20,16);latest.SetAimTarget(lastTarget);latest.Skill(new Point(0,1));
            Check(latest.SkillQueued&&latest.SkillQueuedTarget.HasValue&&
                (latest.SkillQueuedTarget.Value-lastTarget).Length<.001f&&
                (latest.SkillQueuedDirection-new Point(0,1)).Length<.001f,
                "Explicit second Q moves the REAL queued volley target marker without creating another skill");
            Advance(latest,.2f);
            Check(latest.playerAction!=null&&latest.playerAction.kind=="skill"&&latest.playerAction.direction.y==1&&latest.playerAction.target.HasValue&&(latest.playerAction.target.Value-lastTarget).Length<.001f,"Repeated Q replaces one pending intent with the latest direction and target");
            Check(!latest.SkillQueued&&!latest.SkillQueuedTarget.HasValue,
                "Queued skill target marker clears the instant its world action dispatches");
            Advance(latest,4.5f);Check(latest.playerAction==null&&latest.skillCooldown<=0,"A consumed input cannot recast after cooldown");
        }
        public static void QueuedAreaSkillsPreviewTrueDamageReach(Campaign data)
        {
            for(int profession=0;profession<3;profession++)
            {
                var g=Quiet(data,profession);g.enemies.Clear();
                g.Attack(new Point(1f,0f));
                Advance(g,.12f);g.Skill(new Point(0,-1));
                float expected=profession==0?3.1f:
                    profession==2?4.5f:0f;
                Check(g.SkillQueued&&
                    Math.Abs(g.SkillQueuedAreaReach-expected)<.0001f,
                    "The live pending Q must preview only its actual Vanguard/Arcanist damage reach, class "+profession);
                g.Dash(new Point(1f,0f));
                Check(!g.SkillQueued&&g.SkillQueuedAreaReach==0f,
                    "Dodge cancels the real area skill and its world preview without spending Q");
            }
            foreach(int profession in new[]{0,1,2})
            {
                var alternate=Quiet(data,profession);alternate.enemies.Clear();
                alternate.save.technique=1;
                alternate.Attack(new Point(1,0));Advance(alternate,.12f);
                alternate.Skill(new Point(0,-1));
                Check(alternate.SkillQueued&&
                    alternate.SkillQueuedAreaReach==0f,
                    "Alternate technique cannot borrow standard AoE damage-range art");
            }
            var dispatched=Quiet(data,2);dispatched.enemies.Clear();
            dispatched.Attack(new Point(1,0));Advance(dispatched,.18f);
            dispatched.Skill(new Point(0,-1));
            Check(dispatched.SkillQueued&&
                Math.Abs(dispatched.SkillQueuedAreaReach-4.5f)<.0001f,
                "Queued Arcanist Q initially shows true area radius");
            for(int frame=0;frame<30&&dispatched.SkillQueued;frame++)
                dispatched.Tick(.02f,new Point());
            Check(!dispatched.SkillQueued&&
                dispatched.SkillQueuedAreaReach==0f&&
                dispatched.playerAction!=null&&
                dispatched.playerAction.kind=="skill",
                "The area preview disappears at the authoritative cast action, not the later contact damage");
        }
        public static void QueuedRangerVolleyReportsLiveObstruction(Campaign data)
        {
            MapData original=data.maps[0];
            try
            {
                var rows=new string[18];
                for(int y=0;y<18;y++)
                {
                    char[] tiles=new string('.',18).ToCharArray();
                    for(int x=0;x<18;x++)
                        if(x<2||x>=16||y<2||y>=16||x==8)
                            tiles[x]='#';
                    rows[y]=new string(tiles);
                }
                data.maps[0]=new MapData{
                    id=0,rows=rows,spawnX=6.5f,spawnY=8.5f,
                    objects=new WorldObject[0],decor=new Decoration[0]};
                var g=Fresh(data,1);g.enemies.Clear();
                g.save.x=6.5f;g.save.y=8.5f;
                Point target=new Point(10.5f,8.5f);
                g.SetAimTarget(target);
                g.Attack(new Point(1f,0f));
                float attackPaidEnergy=g.save.energy;
                g.Skill(new Point(1f,0f));
                Check(g.SkillQueued&&g.SkillQueuedTarget.HasValue&&
                    (g.SkillQueuedTarget.Value-target).Length<.001f&&
                    !g.SkillQueuedRangerLaneClear,
                    "A Ranger volley cannot claim its genuine center lane crosses a solid gate");
                Check(g.projectiles.Count==0&&g.save.energy==attackPaidEnergy,
                    "A range/cover preview cannot mint early missiles or charge the deferred Q");
                char[] gate=rows[8].ToCharArray();gate[8]='.';
                rows[8]=new string(gate);
                Check(g.SkillQueuedRangerLaneClear&&g.SkillQueued&&
                    (g.SkillQueuedTarget.Value-target).Length<.001f,
                    "Opening the actual flight corridor updates the same already committed Q target");
                // No actual projectile can fly farther than its finite
                // lifetime, even through an unobstructed empty corridor.
                Point distant=new Point(90f,8.5f);
                g.SetAimTarget(distant);g.Skill(new Point(1f,0f));
                Check(!g.SkillQueuedRangerLaneClear&&
                    g.SkillQueuedTarget.HasValue&&
                    (g.SkillQueuedTarget.Value-distant).Length<.001f,
                    "Committed Ranger Q warns when its real finite flight lifetime cannot reach the live target");
                g.SetAimTarget(target);g.Skill(new Point(1f,0f));
                Check(g.SkillQueuedRangerLaneClear,
                    "Explicit retarget back inside real range restores a valid center lane");
                gate[8]='#';rows[8]=new string(gate);
                Check(!g.SkillQueuedRangerLaneClear,
                    "Closing the gate again immediately retracts an unsafe center shot claim");
                g.Dash(new Point(1f,0f));
                Check(!g.SkillQueued&&!g.SkillQueuedRangerLaneClear,
                    "Cancelling a Q also clears its blocked-range status without changing damage or saves");
            }
            finally{data.maps[0]=original;}
        }

        public static void ReleasedAreaSkillRespectsCoverInDamageAndVisualRange(Campaign data)
        {
            MapData original=data.maps[0];
            try
            {
                var rows=new string[18];
                for(int y=0;y<18;y++)
                {
                    char[] tiles=new string('.',18).ToCharArray();
                    for(int x=0;x<18;x++)
                        if(x<2||x>=16||y<2||y>=16||x==8)
                            tiles[x]='#';
                    rows[y]=new string(tiles);
                }
                data.maps[0]=new MapData{id=0,rows=rows,
                    spawnX=6.5f,spawnY=8.5f,
                    objects=new WorldObject[0],decor=new Decoration[0]};
                var g=Fresh(data,2);g.enemies.Clear();
                g.save.x=6.5f;g.save.y=8.5f;
                var visible=new Enemy(82101,0,5f,8.5f,400f)
                    {stun=99f,timer=999f,staggerRecovery=99f};
                var sheltered=new Enemy(82102,0,10.5f,8.5f,400f)
                    {stun=99f,timer=999f,staggerRecovery=99f};
                g.enemies.Add(visible);g.enemies.Add(sheltered);
                Check(g.CanStand(g.Position)&&
                    g.CanStand(visible.Position)&&
                    g.CanStand(sheltered.Position),
                    "AoE release has a legal physical actor on both sides of solid cover");
                g.Skill(new Point(1f,0f));
                Advance(g,.30f);
                Effect cast=g.effects.Find(e=>e.kind=="burst"&&
                    e.playerAreaSkill&&Math.Abs(e.radius-4.5f)<.001f);
                Check(cast!=null&&visible.hp<400f&&sheltered.hp==400f,
                    "One real Arcanist Q tags only its own release burst and hurts only the exposed enemy");
                Point west=new Point(4.5f,8.5f);
                Point east=new Point(11f,8.5f);
                Check(g.PlayerAreaEffectContactVisible(cast,west)&&
                    !g.PlayerAreaEffectContactVisible(cast,east),
                    "Post-contact skill footprint cannot promise an eastward hit through a real wall");
                char[] gate=rows[8].ToCharArray();gate[8]='.';
                rows[8]=new string(gate);
                Check(g.PlayerAreaEffectContactVisible(cast,east)&&
                    g.PlayerAreaEffectContactVisible(cast,west)&&
                    sheltered.hp==400f,
                    "An opened gate updates only the remaining visual release outline, never retroactively mints damage");
                Check(!g.PlayerAreaEffectContactVisible(
                        new Effect("burst",g.Position,.4f){radius=4.5f},east),
                    "An unrelated enemy-world burst must not masquerade as the tagged player skill");
            }
            finally{data.maps[0]=original;}
        }

        public static void QueuedAreaPreviewRespectsRealCover(Campaign data)
        {
            MapData original=data.maps[0];
            try
            {
                var rows=new string[22];
                for(int y=0;y<rows.Length;y++)
                {
                    char[] tiles=new string('.',22).ToCharArray();
                    for(int x=0;x<22;x++)
                        if(x<2||x>=20||y<2||y>=20||x==11)
                            tiles[x]='#';
                    rows[y]=new string(tiles);
                }
                data.maps[0]=new MapData{
                    id=0,rows=rows,spawnX=8f,spawnY=10.5f,
                    objects=new WorldObject[0],decor=new Decoration[0]};
                var g=Fresh(data,2);g.enemies.Clear();
                g.save.x=8f;g.save.y=10.5f;
                g.Attack(new Point(1,0));g.Skill(new Point(1,0));
                Check(g.SkillQueued&&
                    Math.Abs(g.SkillQueuedAreaReach-4.5f)<.001f,
                    "Actual Arcanist Q stores its 4.5-tile reach near a sealed wall");
                Point open=new Point(6f,10.5f);
                Point occluded=new Point(12f,10.5f);
                Check(g.SkillQueuedAreaContactVisible(open)&&
                    !g.SkillQueuedAreaContactVisible(occluded),
                    "Queued Q contact map must match actual ClearAttackPath: near-side damage exists, far-side damage does not");
                // A moving tide/gate may reopen without requiring a second
                // Q press. Its displayed reach must immediately reflect
                // the changed physical lane and retain the same input clock.
                for(int y=7;y<=14;y++)
                {
                    char[] wall=rows[y].ToCharArray();wall[11]='.';
                    rows[y]=new string(wall);
                }
                Check(g.SkillQueuedAreaContactVisible(occluded)&&
                    g.SkillQueuedAreaContactVisible(open),
                    "Opening real terrain updates Q preview without replacing the committed skill intent");
                // Close the gate around the hero only AFTER confirming
                // that the same live queued Q could reach through it.
                char[] sealedRow=rows[10].ToCharArray();
                sealedRow[11]='#';rows[10]=new string(sealedRow);
                g.save.x=11.5f;
                Check(g.SkillQueuedAreaReach==0f&&
                    !g.SkillQueuedAreaContactVisible(open),
                    "A hero sealed into solid terrain cannot claim an area cast with a false damage outline");
            }
            finally{data.maps[0]=original;}
        }
        public static void BufferedSkillsHaveBoundedLifetimeAndResourceChecks(Campaign data)
        {
            var early=Quiet(data,2);early.Attack(new Point(1,0));early.Skill(new Point(0,-1));Advance(early,1);
            Check(early.skillCooldown<=0&&early.save.energy==100&&!early.effects.Exists(e=>e.kind=="burst"),"Q pressed at the start of long staff preparation expires instead of autocasting much later");
            var frozen=Quiet(data,1);frozen.hitStop=.5f;frozen.Skill(new Point(0,-1));
            for(int frame=0;frame<9;frame++)frozen.Tick(.05f,new Point());
            Check(frozen.playerAction==null&&frozen.skillCooldown==0&&frozen.save.energy==100,"Hitstop freezes a ready Q intent without spending or advancing its short lifetime");
            for(int frame=0;frame<5&&frozen.playerAction==null;frame++)frozen.Tick(.05f,new Point());
            Check(frozen.playerAction!=null&&frozen.playerAction.kind=="skill"&&frozen.save.energy==60,"The retained Q starts once when hitstop ends");
            foreach(float step in new[]{.01f,.02f,.05f})
            {
                var dodge=Quiet(data);dodge.Dash(new Point(1,0));dodge.Skill(new Point(0,-1));
                for(int frame=0;frame<30&&dodge.playerAction==null;frame++)dodge.Tick(step,new Point());
                Check(dodge.dashTime==0&&dodge.playerAction!=null&&dodge.playerAction.kind=="skill"&&Math.Abs(dodge.save.energy-42.8f)<1,"Same-frame Dash then Q waits for the dodge and spends once at timestep "+step);
            }
            var cancelled=Quiet(data);cancelled.Attack(new Point(1,0));Advance(cancelled,.12f);cancelled.Skill();cancelled.Dash(new Point(1,0));Advance(cancelled,.5f);
            Check(cancelled.skillCooldown<=0&&cancelled.playerAction==null,"A new dodge clears the earlier queued Q");
            foreach(bool cooldownFailure in new[]{false,true})
            {
                var rejected=Quiet(data);rejected.Attack(new Point(1,0));Advance(rejected,.12f);
                rejected.skillCooldown=cooldownFailure?.01f:0;rejected.save.energy=cooldownFailure?100:39;rejected.toast="";float energy=rejected.save.energy;
                rejected.Skill();Check(rejected.save.energy==energy&&rejected.toast.Length>0,"Unavailable Q gives feedback and does not spend while an attack blocks it");
                Advance(rejected,.7f);
                Check(rejected.skillCooldown<=0&&!rejected.effects.Exists(e=>e.kind=="burst"),"Cooldown expiry or later energy regeneration cannot revive a rejected Q");
            }
            var spent=Quiet(data);spent.Attack(new Point(1,0));Advance(spent,.12f);spent.Skill();spent.save.energy=0;Advance(spent,.5f);
            Check(spent.skillCooldown<=0&&!spent.effects.Exists(e=>e.kind=="burst"),"Buffered Q rechecks affordability at dispatch after other spending");
        }
        public static void BufferedSkillsRespectVariableFrameDeadlines(Campaign data)
        {
            float[][] timesteps={new[]{.033f},new[]{.016f,.033f,.05f},new[]{.01f}};
            for(int cadence=0;cadence<timesteps.Length;cadence++) foreach(bool withinWindow in new[]{true,false})
            {
                var g=Quiet(data,2);g.Attack(new Point(1,0));
                // Staff recovery starts at .379 seconds. .18 leaves .199,
                // while .17 leaves .209 and must outlive the .20 input window.
                Advance(g,withinWindow?.18f:.17f);g.Skill(new Point(0,-1));bool cast=false;
                for(int frame=0;frame<100;frame++)
                {
                    g.Tick(timesteps[cadence][frame%timesteps[cadence].Length],new Point());
                    if(g.playerAction!=null&&g.playerAction.kind=="skill") {cast=true;break;}
                }
                Check(cast==withinWindow,"Buffered Q deadline follows the actual readiness time within a variable frame, cadence "+cadence+" eligible "+withinWindow);
                if(cast)Check(g.save.energy==60&&g.skillCooldown==3.5f,"Crossing the deadline in a long frame still dispatches the eligible Q exactly once");
                else Check(g.skillCooldown<=0&&g.save.energy==100,"A readiness time beyond the window cannot gain extra grace from a long frame");
            }
            var boundary=Quiet(data,2);boundary.Attack(new Point(1,0));Advance(boundary,.179f);boundary.Skill(new Point(0,-1));
            for(int frame=0;frame<20;frame++)boundary.Tick(.01f,new Point());
            Check(boundary.playerAction!=null&&boundary.playerAction.kind=="skill"&&boundary.save.energy==60,"An exact .20-second deadline tolerates accumulated .01-step float residue without dropping Q");
        }
        public static void BufferedSkillsClearAtWorldAndProfileBoundaries(Campaign data)
        {
            System.Action<GameCore>[] transitions={
                g=>{g.mode="pause";g.Close();},
                g=>{g.mode="dialog";g.Close();},
                g=>g.Equip(0),
                g=>{g.save.weaponStyles[1]=1;},
                g=>{g.save.technique=1;},
                g=>{g.save.profession=2;},
                g=>g.Enter(0),
                g=>{g.PrepareSave();g.Load(CloneSave(g.save));},
                g=>{g.save.health=1;g.invulnerable=0;g.projectiles.Add(new Projectile{position=g.Position,damage=999,source="shot"});g.Tick(.01f,new Point());Check(g.Dying,"Boundary test takes an actual lethal contact");}
            };
            for(int boundary=0;boundary<transitions.Length;boundary++)
            {
                var g=Quiet(data,1);g.Attack(new Point(1,0));Advance(g,.2f);g.Skill(new Point(0,-1));
                Check(g.skillCooldown<=0&&g.save.energy==100,"Boundary scenario first has an unspent short Q intent");
                transitions[boundary](g);Advance(g,1.3f);
                Check(g.skillCooldown<=0&&g.playerAction==null,"Menu, equipment, profile, travel, load or death cannot release a stale Q, boundary "+boundary);
                Check(!g.projectiles.Exists(p=>p.technique),"No queued volley escapes into another profile or world, boundary "+boundary);
            }
        }
        public static void EffectOriginsAndSources(Campaign data)
        {
            for (int profession = 0; profession < 3; profession++)
            {
                var g = Fresh(data, profession); g.Enter(1); g.enemies.Clear(); g.save.reducedMotion = true;
                g.save.x = 12; g.save.y = 14; g.Skill();
                Check(g.effects.Count == 0 && g.projectiles.Count == 0, "Preparing Q cannot create an early effect or projectile");
                Advance(g, .3f);
                if (profession == 1)
                    Check(g.projectiles.Count == 5 && g.effects.Exists(e => e.kind == "release" && (e.position-GameCore.WeaponOrigin(g.Position,1,g.aim)).Length < .01f) && !g.effects.Exists(e => e.kind == "burst"), "Archer volley releases from its bow, with no unrelated ground explosion");
                else
                {
                    Effect burst = g.effects.Find(e => e.kind == "burst");
                    Check(burst != null && burst.variant == profession && (burst.position-g.Position).Length < .01f && Math.Abs(burst.radius-(profession == 0 ? 3.1f : 4.5f)) < .001f, "AoE carries its actual class, origin and unchanged damage radius to the renderer");
                }
            }
            var guard = Fresh(data); guard.Enter(1); guard.enemies.Clear(); guard.save.technique = 1; guard.Skill(); Advance(guard,.3f);
            Check(guard.effects.Exists(e => e.kind == "burst" && e.variant == 0 && e.radius == 3.8f), "Alternate guard skill retains its own AoE size");
            var marksman = Fresh(data,1); marksman.Enter(1); marksman.enemies.Clear(); marksman.save.technique = 1; marksman.Skill(); Advance(marksman,.3f);
            Check(marksman.projectiles.Count == 1 && marksman.effects.Exists(e => e.kind == "release") && !marksman.effects.Exists(e => e.kind == "burst"), "Alternate shot has a projectile release rather than a class-independent explosion");

            var killer = Fresh(data,2); killer.Enter(1); killer.enemies.Clear(); killer.save.reducedMotion = true;
            killer.save.x = 12; killer.save.y = 14; var foe = new Enemy(931,0,14,14,1) {timer=9999}; killer.enemies.Add(foe);
            killer.projectiles.Add(new Projectile {position=foe.Position,friendly=true,visual="arrow",damage=50}); killer.Tick(.01f,new Point());
            Check(killer.effects.Exists(e => e.kind == "defeat" && (e.position-foe.Position).Length < .01f) && !killer.effects.Exists(e => e.kind == "burst"), "Enemy death remains at the enemy and cannot impersonate the player's vocation skill");
            Check(killer.fallen.Count == 1 && killer.effects.FindAll(e => e.kind == "loot").Count == 1, "Death art still awards a corpse and loot once");

            for (int companion = 0; companion < 3; companion++)
            {
                var g = Fresh(data,2); g.Enter(1); g.enemies.Clear(); g.save.reducedMotion = true; g.save.x = 12; g.save.y = 14;
                g.save.flags.Add("companion_" + companion); g.SelectCompanion(companion); g.save.health = 20; g.CommandCompanion(); Advance(g,.22f);
                Check(!g.effects.Exists(e => e.kind == "burst"), "Companion command cannot borrow the mage player's attack explosion");
                if (companion == 0) Check(g.save.health == 38 && g.effects.Exists(e => e.kind == "heal" && (e.position-g.Position).Length < .01f), "Mira heals with a healing effect at the recipient");
                if (companion == 1) Check(g.projectiles.Count == 3 && g.effects.Exists(e => e.kind == "release" && (e.position-GameCore.WeaponOrigin(g.companionPosition,1,g.companionAim)).Length < .01f), "Ivo's volley releases from Ivo's bow");
                if (companion == 2) Check(g.effects.Exists(e => e.kind == "frost" && (e.position-g.companionPosition).Length < .01f), "Sen's command now renders at the actual caster instead of teleporting its frost to the hero");
            }
            var magic = Fresh(data,2); magic.Enter(1); magic.enemies.Clear(); magic.save.reducedMotion = true; magic.save.x = 12; magic.save.y = 14;
            Point collision = new Point(14,14); magic.enemies.Add(new Enemy(932,0,14,14,999) {stun=20,timer=9999});
            magic.projectiles.Add(new Projectile {position=collision,friendly=true,explosive=true,visual="starfire",damage=20}); magic.Tick(.01f,new Point());
            Check(magic.projectiles.Count == 0 && magic.effects.FindAll(e => e.kind == "magicImpact").Count == 1 && !magic.effects.Exists(e => e.kind == "burst"), "Starfire collision emits exactly one impact, without a duplicate class explosion");
            Effect impact = magic.effects.Find(e => e.kind == "magicImpact"); Check((impact.position-collision).Length < .01f && impact.radius == 1.8f, "Explosive impact carries the actual collision and splash footprint");
            var dodge = Fresh(data); dodge.Dash(new Point(0,-1)); Check(dodge.effects.Exists(e => e.kind == "dash" && e.direction.y == -1 && e.direction.x == 0), "Dodge trail remembers the actual dodge direction");
        }
        public static void InnRoomAndCollisionPersistence(Campaign data)
        {
            var g = Fresh(data); int stage = g.save.stage;
            Check(data.maps.Length == 10 && data.maps[9].id == 9, "Inn is appended without renumbering existing regions");
            WorldObject door = Object(g, "portal9"); g.save.x = door.x; g.save.y = door.y; g.Interact();
            Check(g.save.room == 9 && g.enemies.Count == 0 && !g.Blocked(g.save.x, g.save.y), "The actual town door enters a safe, navigable interior");
            Check(g.Blocked(15.5f, 8.5f) && !g.Blocked(15.5f, 10.5f), "Inn furniture blocks its footprint while the aisle remains open");
            g.PrepareSave(); var loaded = new GameCore(data, CloneSave(g.save));
            Check(loaded.save.room == 9 && loaded.save.stage == stage, "Interior saves restore without advancing or resetting quests");
            g.save.health = 10; Action(g, "rest", "rest");
            Check(g.save.health == g.save.maxHealth, "Interior rest uses the existing recovery transaction");
            g.Close(); g.Enter(9); Action(g, "history16", "close"); Check(g.Has("history_16"), "Inn ledger is recorded in the existing codex");
            g.Close(); WorldObject exit = Object(g, "portal0"); g.save.x = exit.x; g.save.y = exit.y; g.Interact();
            Check(g.save.room == 0 && g.save.stage == stage, "The inn exit returns to the same town journey");
        }
        public static void ContactFramesAndCancellation(Campaign data)
        {
            for (int weapon = 0; weapon < 3; weapon++)
            {
                var g = Fresh(data, weapon); g.Enter(1); g.enemies.Clear(); g.save.reducedMotion = true;
                g.save.x = 12; g.save.y = 14; var foe = new Enemy(41, 0, weapon == 0 ? 13 : 17, 14, 999) { timer = 9999 }; g.enemies.Add(foe);
                g.Attack(new Point(1, 0)); var action = g.playerAction;
                Check(action != null && g.PlayerPose == 4 && foe.hp == 999 && g.projectiles.Count == 0, "Input starts wind-up without early damage or missiles");
                Advance(g, action.contact - .01f); Check(!action.released && foe.hp == 999 && g.projectiles.Count == 0, "No contact before the actual release boundary");
                g.mode = "pause"; Advance(g, 1); Check(!action.released, "Pausing freezes the action and its damage"); g.Close();
                g.Tick(.02f, new Point()); Check(action.released && g.PlayerPose == 5, "Contact and release pose share the same clock");
                if (weapon == 0) { float hp = foe.hp; Check(hp < 999, "Sword contacts at the strike frame"); Advance(g, .1f); Check(foe.hp == hp, "A swing cannot repeatedly damage the same enemy"); }
                else Check(g.projectiles.Count > 0, "Ranged weapons spawn only at their release frame");
                g.Enter(0); g.Close(); g.attackCooldown = 0; g.Attack(new Point(1, 0));
                // Weapon 1 is already equipped for the ranger. Exercise a
                // genuine loadout change, not re-selecting the same icon.
                int alternateWeapon = weapon == 1 ? 0 : 1;
                g.save.flags.Add("weapon_1"); g.Equip(alternateWeapon); Advance(g, .8f);
                Check(g.playerAction == null && g.projectiles.Count == 0, "Changing to a different weapon cancels the stored attack");
                g.Attack(new Point(1, 0)); g.Enter(1); Advance(g, .8f); Check(g.projectiles.Count == 0, "Travel does not release an old attack in the new region");
            }
            var caster = Fresh(data, 2); caster.Enter(1); caster.enemies.Clear(); caster.save.reducedMotion = true;
            caster.save.x = 12; caster.save.y = 14; var target = new Enemy(50, 0, 13, 14, 999) { timer = 9999 }; caster.enemies.Add(target);
            caster.Skill(); Check(target.hp == 999 && caster.PlayerPose == 6, "Q first channels visibly"); Advance(caster, .3f);
            Check(target.hp < 999 && caster.PlayerPose == 7, "Q damage follows its release pose");
            var struck = Fresh(data); struck.Enter(1); struck.enemies.Clear(); struck.save.x = 12; struck.save.y = 14; struck.save.reducedMotion = true;
            var foeHit = new Enemy(51, 0, 13, 14, 999) { timer = 9999 }; struck.enemies.Add(foeHit);
            struck.Attack(new Point(1, 0)); struck.Tick(.04f, new Point());
            struck.invulnerable = 0; struck.projectiles.Add(new Projectile { position = struck.Position, damage = 5, source = "shot" }); struck.Tick(.01f, new Point());
            Advance(struck, .1f); Check(foeHit.hp == 999 && struck.PlayerPose == 10 && !struck.playerAction.released, "Recoil cannot conceal a pending damage event");
            Advance(struck, .25f); Check(foeHit.hp < 999 && struck.playerAction.released && struck.PlayerPose == 5, "After recoil the visible swing resumes before contact");
        }
        public static void MalformedLiveProjectilesCannotCorruptCombat(Campaign data)
        {
            var g=Fresh(data,1);g.Enter(1);g.enemies.Clear();
            g.save.x=12f;g.save.y=14f;g.save.reducedMotion=true;
            var target=new Enemy(86731,0,13.5f,14f,400f)
                {stun=99f,timer=999f,staggerRecovery=99f};
            Check(g.CanStand(target.Position),
                "Malformed-projectile regression uses a real walkable enemy foot");
            g.enemies.Add(target);
            var bad=new[]{
                new Projectile{position=target.Position,
                    velocity=new Point(float.NaN,10f),
                    friendly=true,visual="arrow",damage=300f},
                new Projectile{position=target.Position,
                    velocity=new Point(float.PositiveInfinity,0f),
                    friendly=true,visual="arrow",damage=300f},
                new Projectile{position=target.Position,
                    velocity=new Point(400f,0f),
                    friendly=true,visual="arrow",damage=300f},
                new Projectile{position=new Point(float.NaN,14f),
                    velocity=new Point(1f,0f),
                    friendly=true,visual="arrow",damage=300f},
                new Projectile{position=target.Position,
                    velocity=new Point(1f,0f),life=float.NaN,
                    friendly=true,visual="arrow",damage=300f},
                new Projectile{position=target.Position,
                    velocity=new Point(1f,0f),
                    friendly=true,visual="arrow",damage=float.PositiveInfinity}
            };
            foreach(var malformed in bad)g.projectiles.Add(malformed);
            // Stationary but FINITE projectile contacts exist in genuine
            // scripted combat; do not suppress that gameplay when rejecting
            // corrupt speed samples.
            var valid=new Projectile{position=target.Position,
                velocity=new Point(0f,0f),friendly=true,
                visual="arrow",life=1f,damage=20f};
            g.projectiles.Add(valid);
            g.Tick(.01f,new Point());
            Check(target.hp<400f&&valid.hits.Count==1&&
                valid.hits[0]==target.id&&!g.projectiles.Contains(valid),
                "Valid stationary scripted shot still deals its single real hit");
            Check(g.projectiles.Count==0&&
                Array.TrueForAll(bad,p=>p.hits.Count==0)&&
                g.effects.FindAll(e=>e.kind=="projectileImpact").Count==1,
                "Nonfinite positions, life, damage or velocities and unphysical speed cannot hit, stall or invent impact visuals");
            float after=target.hp;
            g.Tick(.05f,new Point());
            Check(target.hp==after&&g.projectiles.Count==0,
                "Malformed shots cannot resume later after a normal world tick");
        }
        public static void ProjectilesAndEnemyContact(Campaign data)
        {
            var g = Fresh(data, 1); g.Enter(1); g.enemies.Clear(); g.save.reducedMotion = true;
            g.save.x = 12; g.save.y = 14; var archer = new Enemy(60, 1, 17, 14, 999) { timer = 9999 }; g.enemies.Add(archer);
            g.Attack(new Point(1, 0)); Advance(g, .26f); Check(g.projectiles.Count == 1 && archer.hp == 999, "An arrow has a visible flight before a distant hit");
            float first = g.projectiles[0].position.x; Advance(g, .12f); Check(g.projectiles[0].position.x > first && archer.hp == 999, "The arrow travels instead of applying instant ranged damage");
            for (int i = 0; i < 50 && archer.hp == 999; i++) g.Tick(.02f, new Point()); Check(archer.hp < 999 && g.effects.Exists(e => e.kind == "projectileImpact"), "Arrow collision gives damage and a disappearance effect");
            g.projectiles.Clear(); g.invulnerable = 0; var heavy = new Enemy(61, 2, 13, 14, 999) { state = 1, timer = .4f, windupDuration = .4f, target = g.Position }; g.enemies.Clear(); g.enemies.Add(heavy);
            float health = g.save.health; Advance(g, .38f); Check(g.save.health == health && heavy.state == 1, "Readable enemy preparation cannot damage early");
            g.Tick(.03f, new Point()); Check(g.save.health < health && heavy.releaseTime > 0 && g.PlayerPose == 10, "Enemy contact triggers the strike and player recoil together");
        }
        public static void DeathPresentationAndResume(Campaign data)
        {
            var g = Fresh(data); g.Enter(1); g.enemies.Clear(); g.save.reducedMotion = true; g.save.health = 1; g.invulnerable = 0;
            g.projectiles.Add(new Projectile { position = g.Position, damage = 999, source = "shot" }); g.Tick(.02f, new Point());
            Check(g.Dying && g.save.room == 1 && g.PlayerPose == 11, "The player falls before being returned home");
            g.PrepareSave(); var resumed = new GameCore(data, CloneSave(g.save)); Check(resumed.save.room == 0 && resumed.save.health == resumed.save.maxHealth && resumed.save.deaths == 1, "Saving during death safely resumes at the inn without charging death twice");
            Advance(g, 1); Check(!g.Dying && g.save.room == 0 && g.save.deaths == 1, "Death completion restores control exactly once");
            g.Enter(1); g.enemies.Clear(); g.save.x = 12; g.save.y = 14; var foe = new Enemy(71, 0, 13, 14, 1) { timer = 9999 }; g.enemies.Add(foe);
            int kills = g.save.kills[1]; g.Attack(new Point(1, 0)); Advance(g, .2f);
            Check(g.enemies.Count == 0 && g.fallen.Count == 1 && g.save.kills[1] == kills + 1, "Death removes combat participation but preserves a visible corpse");
            g.PrepareSave(); var loaded = new GameCore(data, CloneSave(g.save)); Check(loaded.enemies.Count == 0, "A visual corpse cannot respawn as a living loot source");
            Advance(g, 1); Check(g.fallen.Count == 0 && g.save.kills[1] == kills + 1, "The corpse fades without awarding twice");
        }
        public static void AllyKillsRewardPartyButNotUnwieldedHeroMastery(Campaign data)
        {
            var g=Fresh(data);g.Enter(1);g.enemies.Clear();
            g.save.reducedMotion=true;
            g.save.x=12;g.save.y=14;
            g.save.weapon=0;
            g.save.mastery[0]=13;g.save.mastery[1]=17;
            g.save.mastery[2]=19;
            int kills=g.save.kills[g.save.room];
            int money=g.save.coins;
            int xp=g.save.xp;
            var allyTarget=new Enemy(79711,0,14,14,1){stun=99,timer=999};
            g.enemies.Add(allyTarget);
            var allyShot=new Projectile {
                position=allyTarget.Position,velocity=new Point(1,0),
                friendly=true,visual="arrow",damage=30,life=1,
                impact=new ImpactProfile(1,0,1,0,0,false)
            };
            g.projectiles.Add(allyShot);
            g.Tick(.01f,new Point());
            Check(!g.enemies.Contains(allyTarget) &&
                g.save.mastery[0]==13&&g.save.mastery[1]==17&&
                g.save.mastery[2]==19,
                "Companion-owned real arrow cannot train any unoperated hero weapon");
            Check(g.save.kills[g.save.room]==kills+1 &&
                g.save.coins==money+6&&g.save.xp==xp+12 &&
                g.effects.Exists(e=>e.kind=="loot"),
                "Ally killing blow must still grant existing party kill, coins, XP and loot");

            // A player-owned bow/staff flight credits the weapon whose
            // authored release spawned the projectile, not whatever
            // weapon the player switched to before impact.
            var playerTarget=new Enemy(79712,0,14.3f,14,1){
                stun=99,timer=999};
            g.enemies.Add(playerTarget);
            g.save.weapon=0;
            var playerShot=new Projectile {
                position=playerTarget.Position,
                velocity=new Point(1,0),friendly=true,
                visual="starfire",damage=30,life=1,
                impact=new ImpactProfile(2,0,2,0,0,true)
            };
            g.projectiles.Add(playerShot);g.Tick(.01f,new Point());
            Check(!g.enemies.Contains(playerTarget)&&
                g.save.mastery[2]==21&&g.save.mastery[0]==13&&
                g.save.mastery[1]==17,
                "A real player-authored projectile kill still trains its released weapon");
            Check(g.save.kills[g.save.room]==kills+2&&
                g.save.coins==money+12&&g.save.xp==xp+24,
                "Two authentic kills retain exactly two party rewards");
        }

        public static void EscortArrowsStrikeFirstPhysicalActor(Campaign data)
        {
            // An incoming raider arrow overlaps both hit circles on ONE
            // authored 100ms/movement sweep slice. A civilian's 0.45f
            // hurt radius may enter the flight before the hero's 0.38f
            // radius even though the civilian root is farther away.
            // Hero-first list ordering used to erase this real escort hit.
            foreach(bool mirrored in new[]{false,true})
            foreach(bool civilianFirst in new[]{false,true})
            {
                var g=Fresh(data);g.Enter(1);g.enemies.Clear();
                g.save.x=12;g.save.y=14;
                g.save.reducedMotion=true;
                g.invulnerable=0;
                g.reliefMissionRoom=1;g.reliefMissionWave=3;
                g.reliefMissionTime=90;
                g.reliefFollowerSafety=100;
                g.reliefFollowerPosition=new Point(
                    mirrored?(civilianFirst?11.98f:11.75f):
                        (civilianFirst?12.02f:12.25f),14);
                float oldHealth=g.save.health;
                var projectile=new Projectile{
                    position=new Point(mirrored?
                        (civilianFirst?12.45f:12.25f):
                        (civilianFirst?11.55f:11.75f),14),
                    velocity=new Point(mirrored?-10:10,0),
                    life=1,damage=14,source="shot",
                    visual="enemyArrow",reliefMissionOwned=true
                };
                g.projectiles.Add(projectile);
                g.Tick(.01f,new Point());
                Check(!g.projectiles.Contains(projectile),
                    "The first actual body contact must consume the hostile arrow");
                if(civilianFirst)
                    Check(g.reliefFollowerSafety<99f&&
                        Math.Abs(g.save.health-oldHealth)<.001f,
                        "Civilian nearer along swept arrow must lose safety, not distant hero HP");
                else
                    Check(g.save.health<oldHealth&&
                        Math.Abs(g.reliefFollowerSafety-100f)<.1f,
                        "Hero physically ahead of civilian must really intercept the enemy arrow");
                Check(g.effects.FindAll(e=>e.kind=="projectileImpact").Count==1&&
                    g.effects.Exists(e=>e.kind=="projectileImpact"&&
                        e.position.x>11f&&e.position.x<13f),
                    "One authoritative body hit must draw exactly ONE projectile impact, not duplicate civilian and generic sprites");
                Check(g.effects.Exists(e=>e.kind=="number"&&
                        e.text==g.L("护卫拦截","INTERCEPT"))==!civilianFirst,
                    "Real arrow interception feedback appears only when the hero physically protected civilians");
            }
        }

        public static void EscortGuardReflectionPreservesRealCivilianSafety(Campaign data)
        {
            var g=Fresh(data);g.Enter(1);g.enemies.Clear();
            g.save.x=12;g.save.y=14;
            g.save.profession=0;g.save.weapon=0;
            g.save.reducedMotion=true;g.save.energy=100;
            g.SetAim(new Point(-1,0));g.guardTime=2;
            g.invulnerable=0;
            g.reliefMissionRoom=1;g.reliefMissionWave=3;
            g.reliefMissionTime=90;
            g.reliefFollowerSafety=100;
            g.reliefFollowerPosition=new Point(12.25f,14);
            float health=g.save.health;
            var arrow=new Projectile{
                position=new Point(11.75f,14),
                velocity=new Point(10,0),
                friendly=false,life=1,damage=14,
                visual="enemyArrow",source="shot",
                reliefMissionOwned=true
            };
            g.projectiles.Add(arrow);
            g.Tick(.01f,new Point());
            Check(g.projectiles.Contains(arrow)&&
                arrow.friendly&&arrow.velocity.x<-.1f,
                "Frontal sword guard must reflect the authentic inbound escort arrow");
            Check(Math.Abs(g.save.health-health)<.001f&&
                Math.Abs(g.reliefFollowerSafety-100f)<.1f&&
                g.save.energy>=93&&g.save.energy<94,
                "One real guard intercept pays seven energy but protects hero HP and civilian safety");
            Check(g.effects.Exists(e=>e.kind=="number"&&
                    e.text==g.L("护卫拦截","INTERCEPT"))&&
                g.effects.Exists(e=>e.kind=="number"&&
                    e.text==g.L("剑盾弹反","GUARD REFLECT")),
                "The same physical arrow must show both rescue-interception and sword-guard feedback");
            Check(g.effects.FindAll(e=>e.kind=="projectileImpact").Count==0,
                "A reflected shot stays in flight and must never show a fake contact impact");
            for(int i=0;i<4;i++)g.Tick(.02f,new Point());
            Check(g.reliefFollowerSafety>=99f,
                "Outbound friendly reflection cannot injure the escorted survivor");
        }

        public static void StarfireDetonatesOnNearSideOfRealCover(Campaign data)
        {
            MapData original=data.maps[0];
            try
            {
                // Two fully traversable chambers divided by a solid wall.
                // The same 1.8-tile detonation radius overlaps targets on
                // both sides; only the visible side may receive splash.
                var rows=new string[16];
                for(int y=0;y<16;y++)
                {
                    char[] tiles=new string('.',16).ToCharArray();
                    for(int x=0;x<16;x++)
                        if(x<2||x>=14||y<2||y>=14||x==8)
                            tiles[x]='#';
                    rows[y]=new string(tiles);
                }
                data.maps[0]=new MapData{id=0,rows=rows,
                    spawnX=4.5f,spawnY=8.5f,
                    objects=new WorldObject[0],
                    decor=new Decoration[0]};
                GameCore g=Fresh(data,2);
                g.enemies.Clear();g.save.reducedMotion=true;
                var exposed=new Enemy(77711,0,7f,9.5f,400){stun=99,timer=999};
                var sheltered=new Enemy(77712,0,9.5f,8.5f,400){stun=99,timer=999};
                g.enemies.Add(exposed);g.enemies.Add(sheltered);
                Check(g.CanStand(exposed.Position)&&g.CanStand(sheltered.Position)&&
                    g.Blocked(8.1f,8.5f),
                    "Starfire test enemies must have real safe feet on opposite sides of a solid wall");
                var bolt=new Projectile{position=new Point(6.5f,8.5f),
                    velocity=new Point(20,0),life=1f,damage=70,
                    friendly=true,explosive=true,visual="starfire"};
                g.projectiles.Add(bolt);
                for(int i=0;i<4 && g.projectiles.Contains(bolt);i++)
                    g.Tick(.05f,new Point());
                Check(!g.projectiles.Contains(bolt)&&
                    exposed.hp<400&&sheltered.hp==400,
                    "Solid cover must cause real near-side starfire splash without damaging the occluded enemy");
                Check(g.effects.Exists(e=>e.kind=="magicImpact"&&
                    Math.Abs(e.radius-1.8f)<.001f&&e.position.x<8f),
                    "Wall contact must generate the real 1.8-tile impact on the near face");
                Effect actualSplash=g.effects.Find(e=>
                    e.kind=="magicImpact"&&e.radius>0f);
                Check(actualSplash!=null&&
                    g.MagicSplashVisualContactVisible(actualSplash,
                        actualSplash.position+new Point(-1.2f,0f))&&
                    !g.MagicSplashVisualContactVisible(actualSplash,
                        actualSplash.position+new Point(1.2f,0f)),
                    "The same real fireball that damaged only the exposed side must draw no splash footprint through solid wall");
                int effects=g.effects.FindAll(e=>e.kind=="magicImpact").Count;
                for(int i=0;i<5;i++)g.Tick(.05f,new Point());
                Check(g.effects.FindAll(e=>e.kind=="magicImpact").Count<=effects,
                    "A terrain impact must not repeat damage or create duplicate explosions");

                // A projectile born in cover or one without an explosive
                // profile must not bypass walls via an area hit.
                g.enemies.Clear();g.enemies.Add(exposed);exposed.hp=400;
                g.effects.Clear();g.projectiles.Clear();
                var invalid=new Projectile{position=new Point(7.5f,8.5f),
                    velocity=new Point(20,0),life=1,damage=70,
                    friendly=true,explosive=true,visual="starfire",
                    blockedAtSpawn=true};
                g.projectiles.Add(invalid);
                g.Tick(.05f,new Point());
                Check(exposed.hp==400&&
                    !g.effects.Exists(e=>e.kind=="magicImpact")&&
                    g.effects.FindAll(e=>e.kind=="projectileImpact"&&
                        e.radius==0).Count==1,
                    "A wall-obstructed muzzle keeps one small contact spark, never a fake mage explosion");
                g.projectiles.Clear();g.effects.Clear();
                g.projectiles.Add(new Projectile{
                    position=new Point(7.5f,8.5f),
                    velocity=new Point(20,0),life=1,damage=70,
                    friendly=true,visual="arrow"});
                g.Tick(.05f,new Point());
                Check(exposed.hp==400&&
                    !g.effects.Exists(e=>e.kind=="magicImpact"),
                    "A blocked ordinary arrow must not gain the mage's area attack");
                g.projectiles.Clear();g.effects.Clear();
                g.projectiles.Add(new Projectile{
                    position=new Point(6.5f,8.5f),
                    velocity=new Point(10,0),life=0,
                    friendly=true,visual="starfire",explosive=true});
                g.Tick(.01f,new Point());
                Check(g.projectiles.Count==0&&
                    g.effects.FindAll(e=>e.kind=="projectileFade").Count==1&&
                    !g.effects.Exists(e=>e.kind=="magicImpact"),
                    "A range-expired starfire must retain one quiet fade without a phantom explosion");

                // Cover-adjacent body collision: the shot is still outside
                // solid terrain at x=7.82, but its NEXT radius sample touches
                // the x=8 wall. An ordinary arrow must not lose the actual
                // near-side enemy contact that already precedes that wall.
                g.effects.Clear();g.projectiles.Clear();g.enemies.Clear();
                var coverFront=new Enemy(77731,0,7.3f,8.5f,400f)
                    {stun=99f,timer=999f,staggerRecovery=99f};
                var coverBack=new Enemy(77732,0,9.5f,8.5f,400f,true)
                    {stun=99f,timer=999f,staggerRecovery=99f};
                g.enemies.Add(coverBack);g.enemies.Add(coverFront);
                var grazedArrow=new Projectile{
                    position=new Point(7.82f,8.5f),
                    velocity=new Point(10f,0),life=1f,damage=24f,
                    friendly=true,visual="arrow"};
                Check(!g.Blocked(grazedArrow.position.x,grazedArrow.position.y)&&
                    g.CanStand(coverFront.Position)&&g.CanStand(coverBack.Position)&&
                    (coverFront.Position-grazedArrow.position).Length<
                        .5f+grazedArrow.Radius,
                    "Near-wall arrow fixture begins in a real exposed body's hit circle and outside solid cover");
                g.projectiles.Add(grazedArrow);g.Tick(.05f,new Point());
                Check(coverFront.hp<400f&&coverBack.hp==400f&&
                    grazedArrow.hits.Count==1&&grazedArrow.hits[0]==coverFront.id&&
                    !g.projectiles.Contains(grazedArrow)&&
                    g.effects.Exists(e=>e.kind=="projectileImpact"&&
                        e.position.x<8f),
                    "Wall-leading-edge collision cannot eat an earlier real exposed body hit or damage a sheltered Boss");
                // The exposed enemy is absent in this second shot. Even
                // though the large Boss circle intersects the near wall,
                // cover still prevents a body hit across solid stone.
                g.effects.Clear();g.projectiles.Clear();g.enemies.Clear();
                g.enemies.Add(coverBack);
                var blockedArrow=new Projectile{
                    position=new Point(7.82f,8.5f),
                    velocity=new Point(10f,0),life=1f,damage=24f,
                    friendly=true,visual="arrow"};
                g.projectiles.Add(blockedArrow);g.Tick(.05f,new Point());
                Check(coverBack.hp==400f&&blockedArrow.hits.Count==0&&
                    !g.projectiles.Contains(blockedArrow),
                    "First-body wall resolver must not grant contact to a sheltered Boss across solid cover");

                // A piercing Ranger volley has a stronger obligation:
                // multiple real near-wall hurt circles receive ONE damage
                // event each, in geometric front-to-back order, before the
                // same wall finally retires its spent projectile.
                g.effects.Clear();g.projectiles.Clear();g.enemies.Clear();
                var firstNear=new Enemy(77741,0,7.3f,8.5f,400f)
                    {stun=99f,timer=999f,staggerRecovery=99f};
                var secondNear=new Enemy(77742,0,7.45f,8.5f,400f)
                    {stun=99f,timer=999f,staggerRecovery=99f};
                g.enemies.Add(secondNear);g.enemies.Add(coverBack);
                g.enemies.Add(firstNear); // reversed physical contact order
                var piercingNearWall=new Projectile{
                    position=new Point(7.82f,8.5f),
                    velocity=new Point(10f,0),life=1f,damage=24f,
                    friendly=true,piercing=true,visual="arrow"};
                g.projectiles.Add(piercingNearWall);
                g.Tick(.05f,new Point());
                Check(firstNear.hp<400f&&secondNear.hp<400f&&
                    coverBack.hp==400f&&piercingNearWall.hits.Count==2&&
                    piercingNearWall.hits[0]==firstNear.id&&
                    piercingNearWall.hits[1]==secondNear.id&&
                    !g.projectiles.Contains(piercingNearWall),
                    "Piercing arrow must hit both real front-side bodies once, in physical entry order, before wall contact");

                // Enemy projectiles share the same cover-first weakness.
                // A live shot already overlapping the hero's near-side
                // hurt circle must not fizzle without damage merely because
                // its NEXT leading-edge sample touches the wall.
                g.effects.Clear();g.projectiles.Clear();g.enemies.Clear();
                g.save.x=7.55f;g.save.y=8.5f;
                g.save.health=g.save.maxHealth;g.invulnerable=0f;
                g.save.reducedMotion=true;
                var wallSideArrow=new Projectile{
                    position=new Point(7.82f,8.5f),
                    velocity=new Point(10f,0),life=1f,damage=12f,
                    friendly=false,visual="enemyArrow",source="shot"};
                Check(g.CanStand(g.Position)&&
                    (g.Position-wallSideArrow.position).Length<.38f,
                    "Hostile cover-edge regression starts on a legal hero root inside the real arrow contact circle");
                float beforeHealth=g.save.health;
                g.projectiles.Add(wallSideArrow);g.Tick(.05f,new Point());
                Check(g.save.health<beforeHealth&&
                    !g.projectiles.Contains(wallSideArrow)&&
                    g.effects.Exists(e=>e.kind=="projectileImpact"&&
                        e.position.x<8f),
                    "Enemy arrow must deal one real near-side contact before cover consumes its projectile");

                // A second enemy shot cannot punch through that same wall.
                g.effects.Clear();g.projectiles.Clear();
                g.save.x=9.5f;g.save.y=8.5f;
                g.invulnerable=0f;g.playerHitTime=0f;
                var hiddenHeroShot=new Projectile{
                    position=new Point(7.82f,8.5f),
                    velocity=new Point(10f,0),life=1f,damage=12f,
                    friendly=false,visual="enemyArrow",source="shot"};
                float shelteredHealth=g.save.health;
                g.projectiles.Add(hiddenHeroShot);g.Tick(.05f,new Point());
                Check(g.save.health==shelteredHealth&&
                    !g.projectiles.Contains(hiddenHeroShot),
                    "Shared hostile near-wall resolver still stops enemy arrows at cover instead of damaging a hidden hero");

                // Simulate a tide/arena wall sealing around a bolt AFTER
                // its valid muzzle launch. Its stored blockedAtSpawn flag
                // remains false, but the CURRENT source tile is solid.
                // This cannot produce a free 1.8-tile splash, even though
                // an exposed victim is still reachable from the wall edge.
                g.effects.Clear();g.projectiles.Clear();
                g.enemies.Clear();g.enemies.Add(exposed);exposed.hp=400f;
                var sealedBolt=new Projectile{
                    position=new Point(8.02f,9.5f),
                    velocity=new Point(20,0),life=1f,damage=70f,
                    friendly=true,explosive=true,visual="starfire",
                    blockedAtSpawn=false};
                Check(g.Blocked(sealedBolt.position.x,sealedBolt.position.y)&&
                    g.CanStand(exposed.Position)&&!sealedBolt.blockedAtSpawn,
                    "Tide-sealed starfire regression uses a genuinely buried in-flight source and a live exposed victim");
                g.projectiles.Add(sealedBolt);
                g.Tick(.05f,new Point());
                Check(exposed.hp==400f&&!g.projectiles.Contains(sealedBolt)&&
                    !g.effects.Exists(e=>e.kind=="magicImpact")&&
                    g.effects.Exists(e=>e.kind=="projectileImpact"&&e.radius==0f),
                    "A dynamically sealed starfire bolt must fizzle on its true buried start instead of awarding phantom splash damage");
            }
            finally{data.maps[0]=original;}
        }

        public static void PhysicalAttacksAndDodgeRespectWalls(Campaign data)
        {
            var g = Fresh(data); g.Enter(1); g.enemies.Clear(); g.save.reducedMotion = true;
            g.save.x = 9.5f; g.save.y = 8.8f;
            var foe = new Enemy(90, 0, 9.5f, 10.2f, 999) { timer = 9999 }; g.enemies.Add(foe);
            Check(!g.Blocked(g.save.x, g.save.y) && !g.Blocked(foe.x, foe.y) && g.Blocked(9.5f, 9.5f), "A real forest wall separates two otherwise valid positions");
            g.Attack(new Point(0, 1)); Advance(g, .25f);
            Check(foe.hp == 999, "A visible sword strike cannot damage through an intervening wall");
            foe.state = 1; foe.timer = .04f; foe.target = g.Position; foe.facing = new Point(0, -1);
            float health = g.save.health; Advance(g, .06f);
            Check(g.save.health == health && foe.releaseTime > 0, "A creature can finish its lunge without biting through stone");

            g.Enter(0); g.enemies.Clear(); g.save.x = 7; g.save.y = 6.3f;
            g.Dash(new Point(0, -1));
            for (int i = 0; i < 5; i++)
            {
                g.Tick(.05f, new Point());
                Check(g.save.y >= 5.97f && !g.Blocked(g.save.x, g.save.y), "A fast dodge stops outside the unchanged house footprint");
            }
            g.Enter(1); g.enemies.Clear(); g.save.x = 12; g.save.y = 14;
            var open = new Enemy(91, 0, 12, 15.3f, 999) { timer = 9999 }; g.enemies.Add(open);
            g.Attack(new Point(0, 1)); Advance(g, .25f);
            Check(open.hp < 999, "The obstruction fix preserves normal open-lane melee contacts");
        }
        public static void MiraCommandCannotHealThroughWallOrWasteCooldown(Campaign data)
        {
            // Real forest obstacle already blocks Mira's auto heal. The
            // explicit 18s command must obey the same line at CONTACT,
            // including a hero who escaped behind a wall during windup.
            var g=Fresh(data);g.Enter(1);g.enemies.Clear();
            g.save.flags.Add("companion_0");g.SelectCompanion(0);
            g.save.x=9.5f;g.save.y=8.8f;
            g.companionPosition=new Point(10.5f,8.8f);
            g.save.health=30f;g.save.energy=20f;
            Check(g.CanStand(g.Position)&&g.CanStand(g.companionPosition),
                "Mira command source/recipient must start on legal ground");
            g.CommandCompanion();
            ActorAction cast=g.companionAction;
            Check(cast!=null&&!cast.released&&g.companionCooldown>17f,
                "Mira command starts a real delayed cast and reserves cooldown");
            // Cross the production wall before the same action contact.
            g.save.x=9.5f;g.save.y=10.2f;
            Check(g.Blocked(9.5f,9.5f),
                "Forest command test requires actual solid line-of-sight cover");
            Advance(g,.22f);
            Check(cast.released&&g.companionAction==cast&&
                g.save.health==30f&&g.save.energy<24f&&
                g.guardTime<=0f&&g.companionCooldown<=0f&&
                !g.effects.Exists(e=>e.kind=="heal"),
                "Mira's invalid remote heal crossed the wall, granted a shield or spent 18s");
            // The same player can command again on a clear physical lane,
            // without waiting through a cooldown for the rejected heal.
            g.save.x=9.5f;g.save.y=8.8f;
            g.companionPosition=new Point(10.5f,8.8f);
            g.CommandCompanion();
            ActorAction retry=g.companionAction;
            Check(retry!=null&&retry!=cast&&!retry.released,
                "Rejected Mira command did not become payable again");
            Advance(g,.22f);
            Check(retry.released&&g.save.health==48f&&
                g.save.energy>20f&&g.guardTime>2.7f&&
                g.companionCooldown>17f&&
                g.effects.Exists(e=>e.kind=="heal"),
                "Real unobstructed Mira command lost its heal, energy, guard or cooldown");
        }

        public static void CompanionAutoFireCannotSpendCadenceOnExpiredEnemy(Campaign data)
        {
            // The real hero can kill an enemy during a companion's .16s
            // windup. The ally must keep the full-body recovery pose, not
            // release a phantom arrow and burn another 2.8-second cycle.
            var g=Fresh(data,1);g.Enter(1);g.enemies.Clear();
            g.save.x=13.5f;g.save.y=14;
            g.save.flags.Add("companion_1");g.SelectCompanion(1);
            g.companionPosition=new Point(12,14);
            var enemy=new Enemy(77881,0,15,14,25){stun=99,timer=999};
            g.enemies.Add(enemy);
            var ready=typeof(GameCore).GetField("companionAttack",
                System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.NonPublic);
            ready.SetValue(g,0f);g.Tick(.02f,new Point());
            var attack=g.companionAction;
            Check(attack!=null&&attack.kind=="attack"&&
                attack.target.HasValue,
                "Automatic archer begins an authentic locked-target bow windup");

            // Deliver one actual player-owned projectile contact BEFORE
            // the ally's authored contact frame.
            g.projectiles.Add(new Projectile{position=enemy.Position,
                friendly=true,visual="arrow",damage=999,life=1f});
            g.Tick(.01f,new Point());
            Check(!g.enemies.Contains(enemy)&&!attack.released,
                "Real projectile kills the locked foe before companion release");
            g.effects.Clear();g.projectiles.Clear();
            Advance(g,.18f);
            Check(attack.released&&g.projectiles.Count==0&&
                !g.effects.Exists(e=>e.kind=="release")&&
                (float)ready.GetValue(g)<=.22f,
                "A dead target cannot create phantom companion arrows or consume 2.8s cooldown");
            Check(g.companionAction==attack,
                "Canceled auto-shot retains the complete authored bow recovery pose");

            // A live foe moving behind actual stone during the same
            // windup must not receive a newly created shot through cover.
            var occluded=Fresh(data,1);occluded.Enter(1);
            occluded.enemies.Clear();occluded.save.x=9.5f;
            occluded.save.y=8.8f;occluded.save.flags.Add("companion_1");
            occluded.SelectCompanion(1);
            occluded.companionPosition=new Point(9.5f,8.8f);
            var mover=new Enemy(77882,0,10.5f,8.8f,500){
                stun=99,timer=999};
            occluded.enemies.Add(mover);
            ready.SetValue(occluded,0f);occluded.Tick(.02f,new Point());
            attack=occluded.companionAction;
            Check(attack!=null&&attack.kind=="attack",
                "Ally prepares a real visible auto-attack in the near forest lane");
            mover.x=9.5f;mover.y=10.2f;
            Check(occluded.Blocked(9.5f,9.5f)&&
                occluded.CanStand(mover.Position),
                "The target moved into an accessible tile behind genuine forest stone");
            Advance(occluded,.16f);
            Check(attack.released&&occluded.projectiles.Count==0&&
                (float)ready.GetValue(occluded)<=.22f,
                "A target behind a newly obstructed lane cannot waste the actual companion volley");
            Check(occluded.companionAction==attack,
                "Blocked late release still respects the original full-body action recovery");
        }

        public static void CompanionAutoReleaseTracksMovingVisibleEnemy(Campaign data)
        {
            // A real enemy can sidestep during the ally's 160ms bow windup.
            // LOS/range validation already uses its current feet; release
            // direction and target must use the SAME current contact point.
            MapData previous = data.maps[0];
            try
            {
                var rows = new string[25];
                for (int y = 0; y < 25; y++)
                {
                    char[] row = new string('#', 25).ToCharArray();
                    if (y >= 2 && y <= 22)
                        for (int x = 2; x <= 22; x++) row[x] = '.';
                    rows[y] = new string(row);
                }
                data.maps[0] = new MapData { id = 0, spawnX = 10.5f,
                    spawnY = 10.5f, rows = rows,
                    objects = new WorldObject[0], decor = new Decoration[0] };
                var g = Fresh(data, 1); g.enemies.Clear();
                g.save.x = 10.5f; g.save.y = 10.5f;
                g.save.flags.Add("companion_1"); g.SelectCompanion(1);
                g.companionPosition = new Point(12.5f, 10.5f);
                var foe = new Enemy(98831, 0, 16.5f, 10.5f, 10000f)
                    { stun = 100f, timer = 1000f, staggerRecovery = 100f };
                g.enemies.Add(foe);
                var cadence = typeof(GameCore).GetField("companionAttack",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic);
                cadence.SetValue(g, 0f);
                g.Tick(.02f, new Point());
                ActorAction prepared = g.companionAction;
                Check(prepared != null && prepared.kind == "attack" &&
                    !prepared.released && prepared.target.HasValue,
                    "Ivo physically begins the original locked-target bow windup");
                Point oldAim = prepared.direction;
                // Move on a real open route, but well off the original ray.
                foe.x = 14.5f; foe.y = 13.5f;
                Point actualAim = (foe.Position - g.companionPosition).Normalized;
                Check(g.CanStand(foe.Position) &&
                    (actualAim - oldAim).Length > .35f,
                    "The living target sidestepped into a distinctly new valid line");
                // GameCore caps each individual Tick at 50ms; use real
                // stepped world time to reach the physical 160ms contact.
                Advance(g, .17f);
                Check(prepared.released && g.companionAction == prepared &&
                    g.projectiles.Count == 1,
                    "The original release frame emits exactly one real Ivo arrow");
                Projectile shot = g.projectiles[0];
                Point expected = (foe.Position - g.companionPosition).Normalized;
                // Arrows leave the physical muzzle, NOT the companion's
                // feet. WeaponShot correctly corrects the flying vector
                // from its swept hand attachment toward the live target.
                Point flightAim = (foe.Position - shot.position).Normalized;
                Check((prepared.direction - expected).Length < .002f &&
                    prepared.target.HasValue &&
                    (prepared.target.Value - foe.Position).Length < .002f &&
                    (shot.velocity.Normalized - flightAim).Length < .002f,
                    "Real muzzle-to-live-target velocity and target snapshot follow the sidestepping foe rather than the old windup ray");
                Check((g.companionAim - expected).Length < .002f &&
                    g.effects.Exists(e => e.kind == "release" &&
                        (e.position - GameCore.WeaponOrigin(g.companionPosition,
                            1, expected)).Length < .002f),
                    "Ivo's physical bow release visual, weapon origin and authoritative aim stay aligned");
                // Validate the real game result, not only coherent vectors.
                // The old stale forward shot misses this distant sidestep.
                Advance(g, .65f);
                Check(foe.hp < foe.maxHp,
                    "The physically guided companion arrow actually hits the moved enemy on subsequent world ticks");
            }
            finally { data.maps[0] = previous; }
        }

        public static void RangerSkillsCannotLaunchProjectilesFromBuriedPhysicalMuzzle(Campaign data)
        {
            // The real two Ranger Q release paths share the hand geometry
            // regardless of 5-shot or charged piercing mechanics.
            MapData original=data.maps[0];
            try
            {
                var rows=new string[25];
                for(int y=0;y<25;y++)
                {
                    char[] tiles=new string('#',25).ToCharArray();
                    if(y>=2&&y<=22)for(int x=2;x<=22;x++)tiles[x]='.';
                    if(y==10)tiles[9]='#';
                    rows[y]=new string(tiles);
                }
                data.maps[0]=new MapData {
                    id=0,spawnX=10.5f,spawnY=10.5f,rows=rows,
                    objects=new WorldObject[0],decor=new Decoration[0]};
                for(int technique=0;technique<=1;technique++)
                {
                    var g=Fresh(data,1);g.enemies.Clear();
                    g.save.x=10.5f;g.save.y=10.5f;
                    g.save.reducedMotion=true;g.save.energy=100;
                    g.save.technique=technique;g.save.weapon=1;
                    var foe=new Enemy(98901+technique,0,10.5f,7.5f,10000f)
                        {stun=100,timer=1000,staggerRecovery=100};
                    g.enemies.Add(foe);
                    g.aim=new Point(0,-1);g.aimTarget=foe.Position;
                    Check(g.CanStand(g.Position)&&g.CanStand(foe.Position)&&
                        g.Blocked(9.5f,10.5f),
                        "An actual wall occupies only the player's left-side bow hand, not the player or target feet");
                    g.Skill(new Point(0,-1));
                    ActorAction first=g.playerAction;
                    Check(first!=null&&first.kind=="skill"&&!first.released,
                        "Both normal and charged Ranger Q start a genuine delayed skill");
                    Advance(g,.30f);
                    Check(first.released&&g.projectiles.Count==0&&
                        !g.effects.Exists(f=>f.kind=="release")&&
                        g.effects.Exists(f=>f.kind=="projectileImpact")&&
                        g.skillCooldown>0&&g.save.energy<100,
                        "Blocked Ranger Q contact gives a physical wall spark but no false five-arrow/charged volley, while preserving skill cost");
                    // Open only the obstructing tile, then use the same
                    // production skill path after ordinary recovery.
                    char[] opened=rows[10].ToCharArray();
                    opened[9]='.';rows[10]=new string(opened);
                    Advance(g,.45f);
                    g.skillCooldown=0;g.save.energy=100;
                    g.Skill(new Point(0,-1));
                    ActorAction retry=g.playerAction;
                    Check(retry!=null&&retry!=first&&!retry.released,
                        "After the wall opens a new Ranger Q is accepted");
                    Advance(g,.30f);
                    int count=technique==0?5:1;
                    Check(retry.released&&g.projectiles.Count==count&&
                        g.projectiles.TrueForAll(p=>p.friendly&&p.technique&&
                            !p.blockedAtSpawn)&&
                        g.effects.Exists(f=>f.kind=="release"),
                        "Unobstructed normal Ranger Q fires five real arrows, charged Q one real piercing arrow, with genuine muzzle VFX");
                    Advance(g,.65f);
                    Check(foe.hp<foe.maxHp,
                        "A reopened physical firing lane delivers real enemy damage, not just non-colliding sprites");
                    // Repair modified data before the next technique case.
                    opened[9]='#';rows[10]=new string(opened);
                }
            }
            finally{data.maps[0]=original;}
        }

        public static void ManualCompanionVolleyCannotSpendCooldownFromBlockedMuzzle(Campaign data)
        {
            // Reuse the real ranger bow's full-foot/muzzle geometry, but
            // exercise the EXPLICIT 18-second three-arrow player command.
            MapData original = data.maps[0];
            try
            {
                var rows = new string[25];
                for (int y = 0; y < 25; y++)
                {
                    char[] row = new string('#', 25).ToCharArray();
                    if (y >= 2 && y <= 22)
                        for (int x = 2; x <= 22; x++) row[x] = '.';
                    if (y == 10) row[9] = '#';
                    rows[y] = new string(row);
                }
                data.maps[0] = new MapData { id = 0,
                    spawnX = 8.5f, spawnY = 10.5f, rows = rows,
                    objects = new WorldObject[0], decor = new Decoration[0] };
                var g = Fresh(data, 1); g.enemies.Clear();
                g.save.x = 8.5f; g.save.y = 10.5f;
                g.save.flags.Add("companion_1"); g.SelectCompanion(1);
                g.companionPosition = new Point(10.5f, 10.5f);
                var foe = new Enemy(98851, 0, 10.5f, 7.5f, 10000f)
                    { stun = 100f, timer = 1000f, staggerRecovery = 100f };
                g.enemies.Add(foe);
                g.aim = new Point(0, -1);
                g.aimTarget = foe.Position;
                Check(g.CanStand(g.companionPosition) && g.CanStand(foe.Position) &&
                    g.Blocked(9.9f, 10.02f),
                    "Real nearby stone blocks Ivo's bow hand, not his legal standing foot or the centerline to the foe");
                g.CommandCompanion();
                ActorAction canceled = g.companionAction;
                Check(canceled != null && canceled.kind == "command" &&
                    g.companionCooldown > 17f,
                    "Physical player input reserves the genuine long-cooldown three-arrow command");
                Advance(g, .23f);
                Check(canceled.released && g.companionAction == canceled &&
                    g.projectiles.Count == 0 && g.companionCooldown == 0 &&
                    !g.effects.Exists(e => e.kind == "release"),
                    "All three wall-born arrows are canceled with no phantom VFX or wasted long cooldown");
                char[] open = rows[10].ToCharArray();
                open[9] = '.';
                rows[10] = new string(open);
                g.CommandCompanion();
                ActorAction successful = g.companionAction;
                Check(successful != null && successful != canceled && !successful.released,
                    "The repaired physical muzzle accepts a genuine new manual order immediately");
                Advance(g, .23f);
                Check(successful.released && g.projectiles.Count == 3 &&
                    g.projectiles.TrueForAll(p => p.friendly && p.piercing && !p.blockedAtSpawn) &&
                    g.companionCooldown > 17f &&
                    g.effects.Exists(e => e.kind == "release" && e.variant == 1),
                    "A clear bow releases all three real piercing arrows and spends its normal cooldown");
                Advance(g, .5f);
                Check(foe.hp < foe.maxHp,
                    "The unblocked manual volley physically damages its living target instead of merely passing a projectile count check");
            }
            finally { data.maps[0] = original; }
        }

        public static void CompanionAutoMuzzleObstructionRefundsCadenceAndRetries(Campaign data)
        {
            // A new side-wall can cover Ivo's upper-facing bow HAND even
            // after the ally starts an authentic visible attack windup
            // while the center-line ray to the enemy remains clear. Validate
            // the actual projectile's swept muzzle, not only tile LOS.
            MapData previous = data.maps[0];
            try
            {
                var rows = new string[25];
                for (int y = 0; y < 25; y++)
                {
                    char[] row = new string('#', 25).ToCharArray();
                    if (y >= 2 && y <= 22)
                        for (int x = 2; x <= 22; x++) row[x] = '.';
                    // The side-wall closes only AFTER the ally's real windup.
                    rows[y] = new string(row);
                }
                data.maps[0] = new MapData { id = 0,
                    spawnX = 8.5f, spawnY = 10.5f, rows = rows,
                    objects = new WorldObject[0], decor = new Decoration[0] };
                var g = Fresh(data, 1); g.enemies.Clear();
                g.save.x = 8.5f; g.save.y = 10.5f;
                g.save.flags.Add("companion_1"); g.SelectCompanion(1);
                g.companionPosition = new Point(10.5f, 10.5f);
                var foe = new Enemy(98841, 0, 10.5f, 7.5f, 10000f)
                    { stun = 100f, timer = 1000f, staggerRecovery = 100f };
                g.enemies.Add(foe);
                var cadence = typeof(GameCore).GetField("companionAttack",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic);
                cadence.SetValue(g, 0f);
                g.Tick(.02f, new Point());
                ActorAction prepared = g.companionAction;
                Check(prepared != null && prepared.kind == "attack" &&
                    g.CanStand(g.companionPosition) && g.CanStand(foe.Position),
                    "Ivo acquires a live foe across a real open aisle while both actors have valid physical feet");
                Point hand = GameCore.WeaponOrigin(g.companionPosition,
                    1, prepared.direction);
                Check(!g.Blocked(hand.x, hand.y),
                    "The initial physical bow muzzle must be free before acquiring the target");
                char[] closed = rows[10].ToCharArray();
                closed[9] = '#';
                rows[10] = new string(closed);
                Check(g.Blocked(hand.x, hand.y),
                    "Side wall genuinely intersects Ivo's upward bow muzzle while the center ray and caster foot remain valid");
                Advance(g, .17f);
                Check(prepared.released && g.projectiles.Count == 0 &&
                    (float)cadence.GetValue(g) <= .23f &&
                    !g.effects.Exists(e => e.kind == "release"),
                    "A wall-born auto arrow is rejected before spawn with no phantom release or full cooldown cost");
                // Open only the lateral side wall. The original target,
                // damage, projectile collision and action schedule all run.
                char[] open = rows[10].ToCharArray();
                open[9] = '.';
                rows[10] = new string(open);
                Advance(g, .85f);
                Check(foe.hp < foe.maxHp,
                    "The same physical companion retries after wall removal and really damages the reachable foe");
            }
            finally { data.maps[0] = previous; }
        }

        public static void CompanionAutoTargetsNearestReachableThreat(Campaign data)
        {
            // Combat uses the actual, ordered roster: the original Find()
            // always chose the oldest spawned enemy, even when a closer
            // attacker was about to strike from the opposite direction.
            var g=Fresh(data);
            g.Enter(1);g.enemies.Clear();
            g.save.x=13.5f;g.save.y=14f;
            g.save.flags.Add("companion_1");
            g.SelectCompanion(1);
            g.companionPosition=new Point(12,14);
            var far=new Enemy(74111,0,17,14,500){stun=99,timer=99};
            var close=new Enemy(74112,0,14,14,500){stun=99,timer=99};
            Check(g.CanStand(far.Position)&&g.CanStand(close.Position),
                "Companion nearest-target scenario has genuine traversable enemy feet");
            g.enemies.Add(far);g.enemies.Add(close);
            typeof(GameCore).GetField("companionAttack",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance).SetValue(g,0f);
            g.Tick(.02f,new Point());
            ActorAction action=g.companionAction;
            Check(action!=null&&action.kind=="attack"&&
                action.target.HasValue&&
                (action.target.Value-close.Position).Length<.001f,
                "Ivo must select the nearest visible enemy, not roster spawn order");
            Check((action.direction-(close.Position-g.companionPosition).Normalized).Length<.001f,
                "Companion projectile wind-up and authored facing must aim at the actual closest target");
            Advance(g,.17f);
            Check(action.released,
                "Closest-target aim drives a genuine companion bow release, not a HUD-only selection");
        }

        public static void CompanionAutoSkipsBlockedMuzzleForClearSecondThreat(Campaign data)
        {
            // A blocked bow hand must be considered during AUTO target choice,
            // not only at the late contact frame: otherwise a close enemy can
            // monopolize every 220ms retry while an exposed threat attacks.
            MapData previous=data.maps[0];
            try
            {
                var rows=new string[25];
                for(int y=0;y<rows.Length;y++)
                {
                    char[] cells=new string('#',25).ToCharArray();
                    if(y>=2&&y<=22)
                        for(int x=2;x<=22;x++)cells[x]='.';
                    if(y==10)cells[9]='#'; // Covers the upward bow hand only.
                    rows[y]=new string(cells);
                }
                data.maps[0]=new MapData { id=0,
                    spawnX=8.5f,spawnY=10.5f,rows=rows,
                    objects=new WorldObject[0],decor=new Decoration[0] };
                var g=Fresh(data,1);g.enemies.Clear();
                g.save.x=8.5f;g.save.y=10.5f;
                g.save.flags.Add("companion_1");g.SelectCompanion(1);
                g.companionPosition=new Point(10.5f,10.5f);
                var blockedNear=new Enemy(98842,0,10.5f,7.5f,1000f)
                    {stun=100f,timer=1000f,staggerRecovery=100f};
                var openFar=new Enemy(98843,0,14.5f,10.5f,1000f)
                    {stun=100f,timer=1000f,staggerRecovery=100f};
                g.enemies.Add(blockedNear);g.enemies.Add(openFar);
                Check(g.CanStand(g.companionPosition)&&
                    g.CanStand(blockedNear.Position)&&g.CanStand(openFar.Position)&&
                    g.Blocked(9.9f,10.02f),
                    "The nearest enemy has a valid centerline but the planted archer's actual upward bow hand overlaps stone");
                var cadence=typeof(GameCore).GetField("companionAttack",
                    System.Reflection.BindingFlags.NonPublic|
                    System.Reflection.BindingFlags.Instance);
                cadence.SetValue(g,0f);
                g.Tick(.02f,new Point());
                ActorAction action=g.companionAction;
                Check(action!=null&&action.kind=="attack"&&
                    action.target.HasValue&&
                    (action.target.Value-openFar.Position).Length<.001f,
                    "Ivo auto-selects a real clear bow lane instead of repeatedly winding up at the nearer blocked hand");
                Advance(g,.17f);
                Check(action.released&&g.projectiles.Exists(p=>
                    p.friendly&&!p.blockedAtSpawn),
                    "The chosen farther enemy receives a physically spawned companion arrow");
                Advance(g,.7f);
                Check(openFar.hp<openFar.maxHp&&
                    blockedNear.hp==blockedNear.maxHp,
                    "The usable auto-target takes genuine projectile damage without shooting through a side wall at the nearer foe");
            }
            finally { data.maps[0]=previous; }
        }

        public static void CompanionAutoHealRequiresPhysicalReachAndRetiresBlockedRelease(Campaign data)
        {
            var g=Fresh(data);g.Enter(1);g.enemies.Clear();
            g.save.flags.Add("companion_0");
            g.SelectCompanion(0);
            g.save.x=9.5f;g.save.y=8.8f;
            g.companionPosition=new Point(9.5f,10.2f);
            g.save.health=30;g.save.reducedMotion=true;
            Check(g.CanStand(g.Position)&&g.CanStand(g.companionPosition)&&
                g.Blocked(9.5f,9.5f),
                "Healing line-of-sight fixture retains the genuine forest wall");
            var cooldown=typeof(GameCore).GetField("companionAttack",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            cooldown.SetValue(g,0f);
            g.Tick(.02f,new Point());
            Check(g.companionAction==null&&g.save.health==30,
                "An ally across solid forest stone cannot start a remote automatic heal");
            Check((float)cooldown.GetValue(g)>0&&
                (float)cooldown.GetValue(g)<=.22f,
                "Invalid healing path cannot silently consume six seconds of ally action readiness");

            // Move the ally into the actual reachable side of the wall.
            g.companionPosition=new Point(10.5f,8.8f);
            Check(g.CanStand(g.companionPosition),
                "Nearby healing partner must occupy a legal foot collider");
            Advance(g,.24f);
            ActorAction heal=g.companionAction;
            Check(heal!=null&&heal.kind=="heal"&&!heal.released,
                "After regaining real line of sight the ally can start its normal heal");

            // A fast transition after cast preparation cannot pay health
            // through the old wall at the release frame.
            g.save.y=10.2f;g.save.x=9.5f;
            Advance(g,.18f);
            Check(heal.released&&g.save.health==30&&
                !g.effects.Exists(e=>e.kind=="number"&&e.text=="+HP"),
                "Healing contact is canceled if the hero crosses a wall before release");
            Check((float)cooldown.GetValue(g)<=.22f,
                "Interrupted healing release cannot pay a full six-second cooldown");
            Advance(g,.35f);
            g.save.y=8.8f;g.save.x=9.5f;
            g.companionPosition=new Point(10.5f,8.8f);
            Advance(g,.25f);
            heal=g.companionAction;
            Check(heal!=null&&heal.kind=="heal",
                "Same ally reattempts healing when the real player returns to range");
            Advance(g,.18f);
            Check(g.save.health>30&&g.effects.Exists(e=>
                e.kind=="number"&&e.text=="+HP"),
                "Genuine unobstructed ally healing still restores HP on its contact frame");
        }

        public static void CompanionIdleReadinessReactsToRealTargets(Campaign data)
        {
            var g=Fresh(data);g.Enter(1);g.enemies.Clear();
            g.save.x=13.5f;g.save.y=14f;
            g.save.flags.Add("companion_1");g.SelectCompanion(1);
            g.companionPosition=new Point(12,14);
            var timer=typeof(GameCore).GetField("companionAttack",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            timer.SetValue(g,0f);
            g.Tick(.02f,new Point());
            Check(g.companionAction==null&&
                (float)timer.GetValue(g)>0&&
                (float)timer.GetValue(g)<=.22f,
                "Ivo cannot burn an entire 2.8-second volley cooldown without an enemy");
            var incoming=new Enemy(74114,0,15,14,800){stun=99,timer=99};
            g.enemies.Add(incoming);
            Advance(g,.24f);
            ActorAction attack=g.companionAction;
            Check(attack!=null&&attack.kind=="attack"&&
                attack.target.HasValue&&
                (attack.target.Value-incoming.Position).Length<.001f,
                "Entering a real firing lane awakens companion AI in the next 220ms");
            Check((float)timer.GetValue(g)>2.3f,
                "An accepted, real companion attack still consumes normal volley cooldown");
            Advance(g,.18f);
            Check(attack.released,
                "Newly acquired target fires from a genuine timed bow contact");
        }

        public static void CompanionNarrowBridgesKeepSeparateSafeFeet(Campaign data)
        {
            // Real collision geometry in a single-tile stone causeway:
            // original diagonal follow sockets are all blocked, but a
            // narrow horizontal standing slot is physically open.
            MapData original=data.maps[0];
            try
            {
                var rows=new string[16];
                for(int y=0;y<rows.Length;y++)
                {
                    char[] cells=new string('#',16).ToCharArray();
                    if(y==8)for(int x=2;x<14;x++)cells[x]='.';
                    rows[y]=new string(cells);
                }
                data.maps[0]=new MapData{
                    id=0,spawnX=8.5f,spawnY=8.5f,rows=rows,
                    objects=new WorldObject[0],
                    decor=new Decoration[0]
                };
                var g=Fresh(data);g.enemies.Clear();
                g.save.flags.Add("companion_1");g.SelectCompanion(1);
                Check(g.save.companion==1&&
                    g.CanStand(g.companionPosition)&&
                    (g.companionPosition-g.Position).Length>1.05f&&
                    Math.Abs(g.companionPosition.y-g.Position.y)<.01f,
                    "Actual ally spawned beside the hero within a narrow bridge, never inside hero feet or solid walls");

                Point root=g.companionPosition;
                var follow=typeof(GameCore).GetMethod("CompanionFollowTarget",
                    System.Reflection.BindingFlags.NonPublic|
                    System.Reflection.BindingFlags.Instance);
                for(int k=0;k<8;k++)
                {
                    Point desired=(Point)follow.Invoke(g,null);
                    Check((desired-root).Length<.001f,
                        "Existing safe foot root must not snap between fallback slots when nothing moves");
                }
                g.save.x+=.22f;
                for(int i=0;i<6;i++)g.Tick(.05f,new Point());
                Check(g.CanStand(g.companionPosition)&&
                    (g.companionPosition-g.Position).Length>.85f,
                    "Real companion follow simulation remains a separate physical actor on a tight route");
            }
            finally{data.maps[0]=original;}
        }

        public static void CompanionPreparationAndSafeFollowing(Campaign data)
        {
            for (int id = 0; id < 3; id++)
            {
                var g = Fresh(data); g.save.flags.Add("companion_" + id); g.SelectCompanion(id);
                Check((g.companionPosition - g.Position).Length > 1.4f && g.CanStand(g.companionPosition), "A companion starts beside the hero on a valid footprint");
                g.save.health = 20; g.aim = new Point(0, 1); g.CommandCompanion();
                var action = g.companionAction; Check(action != null && !action.released && g.projectiles.Count == 0 && g.save.health == 20, "Companion command prepares before applying its effect");
                g.mode = "pause"; Advance(g, 1); Check(action.elapsed == 0, "Companion casting pauses with the same world clock"); g.Close();
                Advance(g, .22f); Check(action.released, "Companion pose and command release use the same boundary");
                if (id == 0) Check(g.save.health == 38, "Mira retains the original 18 HP command strength");
                if (id == 1) Check(g.projectiles.Count == 3 && g.projectiles.TrueForAll(p => p.velocity.y > 0 && p.piercing), "Ivo's actual volley flies in the locked south direction");
                if (id == 2) Check(g.effects.Exists(e => e.kind == "frost"), "Sen's command starts its frost field on release");
                g.Enter(1); Check(g.companionAction == null && g.CanStand(g.companionPosition), "Travel cancels a previous companion action and restores safe following");
            }
        }
        public static void ShortHitStopReleasesUnusedFrameTime(Campaign data)
        {
            var reference=Fresh(data);
            var stopped=Fresh(data);
            reference.enemies.Clear();stopped.enemies.Clear();
            reference.save.x=stopped.save.x=12;
            reference.save.y=stopped.save.y=14;
            stopped.hitStop=.012f;
            float before=stopped.save.totalTime;
            reference.Tick(.038f,new Point(1,0));
            stopped.Tick(.05f,new Point(1,0));
            Check(Math.Abs(stopped.save.totalTime-before-.038f)<.0002f&&
                  Math.Abs(stopped.save.x-reference.save.x)<.001f&&
                  Math.Abs(stopped.save.y-reference.save.y)<.001f&&stopped.hitStop==0,
                "A 12ms impact freeze releases the unused 38ms for normal movement and world time");
            var multi=Fresh(data);
            reference=Fresh(data);
            multi.enemies.Clear();reference.enemies.Clear();
            multi.save.x=reference.save.x=12;multi.save.y=reference.save.y=14;
            multi.hitStop=.07f;
            before=multi.save.totalTime;
            multi.Tick(.05f,new Point(1,0));
            Check(Math.Abs(multi.save.totalTime-before)<.00001f&&multi.save.x==12&&
                  Math.Abs(multi.hitStop-.02f)<.00001f,
                "A longer hit-stop completely freezes its first frame and retains exactly the remainder");
            multi.Tick(.05f,new Point(1,0));
            reference.Tick(.03f,new Point(1,0));
            Check(Math.Abs(multi.save.totalTime-before-.03f)<.0002f&&
                  Math.Abs(multi.save.x-reference.save.x)<.001f&&multi.hitStop==0,
                "The next frame uses only the un-frozen 30ms; damage feedback never grants a free slow-motion penalty");
        }


        public static void BossFinaleCannotInterruptDeathRecovery(Campaign data)
        {
            var g=Fresh(data);
            g.save.stage=3;
            g.Enter(4);g.enemies.Clear();
            g.save.flags.Add("boss_4");
            var delay=typeof(GameCore).GetField("finaleDelay",
                System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
            delay.SetValue(g,.025f);
            var hurt=typeof(GameCore).GetMethod("Hurt",
                System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
            g.save.health=1;g.invulnerable=0;
            hurt.Invoke(g,new object[]{999f,"shot",null});
            Check(g.Dying && g.mode=="world",
                "Real lethal damage enters the death state with a pending final-boss epilogue");
            g.Tick(.05f,new Point());
            Check(g.Dying && g.mode=="world" && (float)delay.GetValue(g)>.001f,
                "Boss finale must not open dialogue while the death clock is active");
            for(int i=0;i<28&&g.Dying;i++)g.Tick(.05f,new Point());
            Check(!g.Dying&&g.save.room==0&&g.mode=="world"&&g.save.health>0,
                "The player always reaches a safe town respawn even when the epilogue delay was pending");
            Check((float)delay.GetValue(g)==0,
                "Dead-player finale delay is retired on respawn, not fired inside the old region");
        }

        public static void EquippingIdenticalWeaponCannotEraseActiveAttack(Campaign data)
        {
            var g=Fresh(data);
            g.enemies.Clear();
            g.save.energy=100;
            g.Attack(new Point(1,0));
            ActorAction ongoing=g.playerAction;
            Check(ongoing!=null&&ongoing.kind=="attack",
                "An authentic sword attack starts its visible windup");
            int beforeRevision=g.revision, originalWeapon=g.save.weapon;
            g.Equip(originalWeapon);
            Check(g.playerAction==ongoing&&g.revision==beforeRevision,
                "Re-selecting the same equipped weapon preserves the prepared attack and does not dirty the save");
            for(int i=0;i<8;i++)g.Tick(.05f,new Point());
            Check(ongoing.released,
                "Real attack contact still completes after same-weapon selection");
            // Dash offers a deterministic real input-buffer window independent
            // of weapon windup frame boundaries.
            var queued=Fresh(data);
            queued.save.energy=100;queued.enemies.Clear();
            queued.Dash(new Point(1,0));
            queued.Skill(new Point(0,1));
            Check(queued.playerAction==null&&queued.skillCooldown==0&&queued.dashTime>0,
                "A real dodge buffers Q without starting or paying for its skill yet");
            int queuedRevision=queued.revision;
            queued.Equip(queued.save.weapon);
            Check(queued.revision==queuedRevision&&queued.skillCooldown==0,
                "Same-weapon selection preserves buffered Q through the dodge");
            bool cast=false;
            for(int i=0;i<16;i++)
            {
                queued.Tick(.05f,new Point());
                if(queued.playerAction!=null&&queued.playerAction.kind=="skill")
                    cast=true;
            }
            Check(cast&&queued.skillCooldown>0,
                "The queued class technique still executes exactly once after the real dodge");
            // An actual loadout change must continue to cancel both old
            // windups and buffered intent; it is not an idempotent click.
            var changed=Fresh(data);
            changed.save.flags.Add("weapon_1");
            changed.save.energy=100;
            changed.Attack(new Point(1,0));
            Check(changed.playerAction!=null,"Old loadout has an attack in preparation");
            changed.Equip(1);
            Check(changed.save.weapon==1&&changed.playerAction==null,
                "Genuine weapon swap retires the old sword windup");
            var canceled=Fresh(data);
            canceled.save.flags.Add("weapon_1");
            canceled.save.energy=100;
            canceled.Dash(new Point(1,0));
            canceled.Skill(new Point(0,1));
            canceled.Equip(1);
            for(int i=0;i<16;i++)canceled.Tick(.05f,new Point());
            Check(canceled.save.weapon==1&&canceled.playerAction==null&&
                  canceled.skillCooldown<=0,
                "Genuine equipment swap discards previously buffered dodge Q rather than replaying it");
        }

        public static void SwitchingSaveDuringDialogueResumesNewWorldWithoutStaleModal(Campaign data)
        {
            // The constructor/title path must remain intact, while a live
            // slot load from a prior modal must fully restore world input.
            var g=new GameCore(data,new SaveData());
            Check(g.mode=="title"&&g.conversation==null,
                "Constructing a fresh slot still leaves first boot on the genuine title screen");
            foreach(int version in new[]{1,2})
            {
                g.Start();
                Check(g.mode=="dialog"&&g.conversation!=null,
                    "First world entry shows the authentic intro dialogue before changing save slots");
                SaveData next=new SaveData{version=version};
                next.flags.Add("intro"); // do not open another intro
                next.coins=91+version;
                g.Load(next);
                Check(g.mode=="world"&&g.conversation==null&&
                    g.conversationSource==null&&g.save.coins==91+version&&
                    g.save.version==2&&g.save.room==0,
                    "Loading either v1 or v2 during old dialogue restores the world; valid legacy v1 migrates to the existing canonical v2 save schema");
                float start=g.save.totalTime;
                g.Tick(.04f,new Point());
                Check(g.save.totalTime>start,
                    "Newly selected world slot immediately advances genuine game time instead of remaining frozen behind the previous dialogue");
                g.Close(); // leave game ready for the next controlled intro
                g.save.flags.Remove("intro");
            }
        }

        public static void SwitchingSaveSlotsDoesNotInheritOldAimOrFishingState(Campaign data)
        {
            var g=Fresh(data,1);
            g.enemies.Clear();
            g.SetAim(new Point(0,-1));
            g.SetAimTarget(new Point(77,16));
            g.fishTime=3.2f;g.fishCursor=.72f;g.fishHits=2;g.fishMisses=1;
            g.Notify("旧旅程的消息","Prior journey notification");
            g.sound="attack";g.sound="hurt";
            Check(g.toastTime>0&&g.sounds.Count>=2,
                "Prior journey has a pending notification and authentic queued combat audio");
            SaveData replacement=new SaveData();
            replacement.flags.Add("intro");
            g.Load(replacement);g.Start();
            Check((g.aim-new Point(1,0)).Length<.001f&&
                  g.fishTime==0&&g.fishCursor==0&&g.fishHits==0&&g.fishMisses==0&&
                  g.toastTime==0&&g.toast==""&&g.sounds.Count==0&&g.sound=="",
                "A loaded slot starts without inherited aiming, fishing, HUD or combat audio state");
            g.sound="travel";
            Check(g.sounds.Count==1,
                "New journey audio continues to enqueue its own distinct travel cue");
            g.sounds.Clear();
            g.Skill();
            Check(g.playerAction!=null&&g.playerAction.kind=="skill"&&
                  (g.playerAction.direction-new Point(1,0)).Length<.001f&&
                  !g.playerAction.target.HasValue,
                "The newly loaded hero's first authentic skill cannot reuse a prior slot's cursor target");
            g.SetAimTarget(new Point(23,20));g.SetAim(new Point(-1,0));
            g.NewGame(2);g.Close();g.Skill();
            Check(g.playerAction!=null&&
                  (g.playerAction.direction-new Point(1,0)).Length<.001f&&
                  !g.playerAction.target.HasValue,
                "Starting a new class also clears the previous hero's targeting state");
        }

        public static void NonpiercingProjectileContactsFrontEnemyBeforeBackRoster(Campaign data)
        {
            // The two real enemy footprints deliberately overlap within
            // ONE flight subdivision. List order must not alter the single
            // projectile's physically first contact.
            foreach(bool reverseRoster in new[]{false,true})
            {
                GameCore g=Fresh(data,1);g.Enter(1);g.enemies.Clear();
                g.save.x=12;g.save.y=14;g.save.reducedMotion=true;
                var front=new Enemy(77911,0,13.8f,14f,300){
                    stun=99,timer=999};
                var back=new Enemy(77912,0,14.0f,14f,300){
                    stun=99,timer=999};
                Check(g.CanStand(front.Position)&&g.CanStand(back.Position),
                    "Overlapping hit-circle regression has two actual legal enemy foot positions");
                if(reverseRoster)
                {g.enemies.Add(front);g.enemies.Add(back);}
                else
                {g.enemies.Add(back);g.enemies.Add(front);}
                var shot=new Projectile{position=new Point(13.45f,14f),
                    velocity=new Point(10f,0),life=1f,damage=22f,
                    friendly=true,visual="arrow"};
                g.projectiles.Add(shot);
                g.Tick(.01f,new Point());
                Check(!g.projectiles.Contains(shot)&&front.hp<300&&back.hp==300,
                    "One nonpiercing arrow must hit the first collision surface, not the first roster entry");
                Check(shot.hits.Count==1&&shot.hits[0]==front.id,
                    "First-contact resolver cannot count a back-row phantom hit");
                Check(g.effects.Exists(e=>e.kind=="projectileImpact"),
                    "Physical first-contact correction retains the real arrow impact effect");
            }
            // The established piercing volley intentionally contacts both;
            // front-only ordering is specific to nonpiercing projectiles.
            GameCore piercing=Fresh(data,1);piercing.Enter(1);
            piercing.enemies.Clear();piercing.save.reducedMotion=true;
            piercing.enemies.Add(new Enemy(77920,0,13.8f,14f,300){
                stun=99,timer=999});
            piercing.enemies.Add(new Enemy(77921,0,14f,14f,300){
                stun=99,timer=999});
            var bolt=new Projectile{position=new Point(13.45f,14f),
                velocity=new Point(10f,0),life=1f,damage=22f,
                friendly=true,piercing=true,visual="arrow"};
            piercing.projectiles.Add(bolt);piercing.Tick(.01f,new Point());
            Check(bolt.hits.Count==2&&piercing.enemies.TrueForAll(e=>e.hp<300),
                "Piercing arrows still contact every overlapping real enemy once");
        }

        public static void BossSummonWarningsStayWithPhysicalCasterAcrossReusedIds(Campaign data)
        {
            // Two genuine support-call windups may share an encounter ID.
            // Canceling the first must never hide the second one's warning.
            MapData previous=data.maps[3];
            try
            {
                string[] rows=new string[25];
                for(int y=0;y<rows.Length;y++)
                {
                    char[] tiles=new string('#',25).ToCharArray();
                    if(y>=2&&y<=22)
                        for(int x=2;x<=22;x++)tiles[x]='.';
                    rows[y]=new string(tiles);
                }
                data.maps[3]=new MapData {id=3,
                    spawnX=12f,spawnY=12f,rows=rows,
                    objects=new WorldObject[0],decor=new Decoration[0]};
                var g=Fresh(data,0);g.Enter(3);g.enemies.Clear();
                g.effects.Clear();g.save.room=3;
                g.save.x=12f;g.save.y=12f;
                g.save.reducedMotion=true;
                g.save.maxHealth=1000f;g.save.health=1000f;
                var first=new Enemy(8651,0,10f,12f,400f,true)
                    {hp=120f,cycle=2,timer=.01f};
                var second=new Enemy(8651,0,14f,12f,400f,true)
                    {hp=120f,cycle=2,timer=.01f};
                g.enemies.Add(first);g.enemies.Add(second);
                Check(g.CanStand(first.Position)&&g.CanStand(second.Position),
                    "Concurrent support callers need real legal Boss positions");
                g.Tick(.05f,new Point());
                Check(first.state==1&&second.state==1,
                    "Both real Bosses must actually start a telegraphed attack");
                Check(g.effects.Exists(fx=>fx.bossSummonCueOwner==first&&
                        fx.bossSummonCueOwnerId==first.id)&&
                    g.effects.Exists(fx=>fx.bossSummonCueOwner==second&&
                        fx.bossSummonCueOwnerId==second.id),
                    "Each physical Boss must own one readable upcoming reinforcement call");
                var unrelated=new Effect("number",new Point(12,12),3f,"ORDINARY")
                    {bossSummonCueOwnerId=first.id};
                g.effects.Add(unrelated);
                var interrupt=typeof(GameCore).GetMethod("ApplyEnemyControl",
                    System.Reflection.BindingFlags.Instance|
                    System.Reflection.BindingFlags.NonPublic);
                Check(interrupt!=null,"The real control/contact interruption must exist");
                interrupt.Invoke(g,new object[]{first,.8f});
                Check(first.state==0&&
                    !g.effects.Exists(fx=>fx.bossSummonCueOwner==first)&&
                    g.effects.Exists(fx=>fx.bossSummonCueOwner==second)&&
                    g.effects.Contains(unrelated),
                    "Interrupting one Boss cannot erase another identical-ID Boss's warning or an unrelated number");
                second.timer=.01f;
                g.Tick(.05f,new Point());
                Check(second.state==0&&second.cycle==3&&
                    !g.effects.Exists(fx=>fx.bossSummonCueOwner==second)&&
                    g.effects.Contains(unrelated),
                    "A surviving Boss's real release must retire its own call at contact without erasing other numbers");

                // ID zero is a valid ephemeral actor identity; it must
                // neither match unowned effects nor suppress an actual cue.
                g.enemies.Clear();g.effects.Clear();
                var zero=new Enemy(0,0,14f,12f,400f,true)
                    {hp=120f,cycle=2,timer=.01f};
                g.enemies.Add(zero);g.Tick(.05f,new Point());
                Check(zero.state==1&&
                    g.effects.Exists(fx=>fx.bossSummonCueOwner==zero&&
                        fx.bossSummonCueOwnerId==0),
                    "An actual zero-ID Boss still warns players of its pending support wave");
                var ordinary=new Effect("number",zero.Position,3f,"UNCHANGED");
                g.effects.Add(ordinary);
                interrupt.Invoke(g,new object[]{zero,.5f});
                Check(!g.effects.Exists(fx=>fx.bossSummonCueOwner==zero)&&
                    g.effects.Contains(ordinary),
                    "Canceling a zero-ID Boss cannot remove ordinary unowned VFX");
            }
            finally {data.maps[3]=previous;}
        }

        public static void PiercingProjectilesContactBodiesInPhysicalEntryOrder(Campaign data)
        {
            // Same sweep sample, intentionally REVERSED saved/spawn roster:
            // a penetrating arrow cannot damage the rear foe before the
            // physically earlier front body. Both remain alive after impact
            // so effects and trace order are independently observable.
            var g=Fresh(data,1);g.Enter(1);g.enemies.Clear();
            g.save.x=12f;g.save.y=14f;
            g.save.reducedMotion=true;
            var rear=new Enemy(94102,0,12.53f,14f,500f)
                {stun=99f,timer=999f,staggerRecovery=99f};
            var front=new Enemy(94101,0,12.45f,14f,500f)
                {stun=99f,timer=999f,staggerRecovery=99f};
            g.enemies.Add(rear);g.enemies.Add(front);
            var shot=new Projectile{
                position=new Point(12f,14f),
                velocity=new Point(20f,0),life=3f,
                damage=25f,friendly=true,piercing=true,visual="arrow"};
            g.projectiles.Add(shot);
            g.Tick(.05f,new Point());
            Check(rear.hp<500f&&front.hp<500f&&
                shot.hitActors.Count==2&&shot.hits.Count==2,
                "One actual swept piercing shot must still damage both overlapping live enemies");
            Check(shot.hits[0]==front.id&&shot.hits[1]==rear.id,
                "Penetrating arrows must process the NEAREST physical collision first, even with reverse enemy array order");
            float frontHp=front.hp,rearHp=rear.hp;
            g.Tick(.05f,new Point());
            Check(front.hp==frontHp&&rear.hp==rearHp,
                "Sorted penetration must retain per-actor one-hit immunity across later flight samples");

            // Radius matters, not just center distance. A boss with a
            // larger physical hurt circle can enter BEFORE an ordinary foe
            // whose center is nearer. Order by signed ray-circle entry.
            var mixed=Fresh(data,1);mixed.Enter(1);
            mixed.enemies.Clear();mixed.save.reducedMotion=true;
            mixed.save.x=12f;mixed.save.y=14f;
            var small=new Enemy(94204,0,12.95f,14f,600f)
                {stun=99f,timer=999f,staggerRecovery=99f};
            var boss=new Enemy(94203,0,13.1f,14f,600f,true)
                {stun=99f,timer=999f,staggerRecovery=99f};
            mixed.enemies.Add(small);mixed.enemies.Add(boss);
            var second=new Projectile{
                position=new Point(12f,14f),
                velocity=new Point(20f,0),life=3f,
                damage=24f,friendly=true,piercing=true,visual="arrow"};
            mixed.projectiles.Add(second);
            mixed.Tick(.05f,new Point());
            Check(second.hits.Count==2&&
                second.hits[0]==boss.id&&second.hits[1]==small.id&&
                small.hp<600f&&boss.hp<600f,
                "Piercing order must honor the larger Boss collision circle instead of sorting only by actor center or roster");
        }

        public static void PiercingProjectilesTrackEnemyIdentityAcrossReusedSpawnIds(Campaign data)
        {
            // Numeric enemy IDs are spawn/save slots, NOT the physical
            // identity of an opponent. Ivo's pierce and Ranger volleys
            // must contact two different enemies even when their IDs match.
            var g=Fresh(data,1);g.Enter(1);g.enemies.Clear();
            g.save.x=12;g.save.y=14;g.save.reducedMotion=true;
            var first=new Enemy(73201,0,12.5f,14f,500f)
                {stun=99f,timer=999f,staggerRecovery=99f};
            var second=new Enemy(73201,0,13.7f,14f,500f)
                {stun=99f,timer=999f,staggerRecovery=99f};
            g.enemies.Add(first);g.enemies.Add(second);
            var shot=new Projectile {position=new Point(12f,14f),
                velocity=new Point(20f,0f),life=3f,damage=25f,
                friendly=true,piercing=true,visual="arrow"};
            g.projectiles.Add(shot);
            for(int i=0;i<3;i++)g.Tick(.05f,new Point());
            Check(first.hp<500f&&second.hp<500f&&
                shot.hitActors.Count==2&&
                shot.hitActors.Contains(first)&&shot.hitActors.Contains(second),
                "One piercing arrow must hit each physically distinct enemy even when IDs are reused");
            Check(shot.hits.Count==2&&shot.hits[0]==first.id&&
                shot.hits[1]==second.id,
                "Existing hit-ID trace must still record BOTH real contacts without hiding duplicate IDs");
            float firstHp=first.hp,secondHp=second.hp;
            for(int i=0;i<4;i++)g.Tick(.05f,new Point());
            Check(first.hp==firstHp&&second.hp==secondHp&&
                shot.hitActors.Count==2&&shot.hits.Count==2,
                "Piercing arrow must never damage the same actor twice while staying in flight");

            // A live projectile can outlast a defeated opponent; the next
            // wave is a NEW physical body even when it reuses the same ID.
            var respawn=Fresh(data,1);respawn.Enter(1);
            respawn.enemies.Clear();respawn.save.reducedMotion=true;
            respawn.save.x=12;respawn.save.y=14;
            var oldEnemy=new Enemy(73202,0,12.5f,14f,15f)
                {stun=99f,timer=999f,staggerRecovery=99f};
            respawn.enemies.Add(oldEnemy);
            var continuing=new Projectile {position=new Point(12f,14f),
                velocity=new Point(20f,0f),life=3f,damage=40f,
                friendly=true,piercing=true,visual="arrow"};
            respawn.projectiles.Add(continuing);
            respawn.Tick(.05f,new Point());
            Check(oldEnemy.hp<=0&&continuing.hitActors.Contains(oldEnemy)&&
                respawn.projectiles.Contains(continuing),
                "Piercing arrow must remain in flight after its real first kill");
            var newEnemy=new Enemy(73202,0,14f,14f,500f)
                {stun=99f,timer=999f,staggerRecovery=99f};
            respawn.enemies.Add(newEnemy);
            for(int i=0;i<3;i++)respawn.Tick(.05f,new Point());
            Check(newEnemy.hp<500f&&continuing.hitActors.Contains(newEnemy)&&
                continuing.hits.Count==2&&continuing.hitActors.Count==2,
                "A later new enemy with a reused ID must be hittable by an existing piercing shot");
            float newHp=newEnemy.hp;
            for(int i=0;i<3;i++)respawn.Tick(.05f,new Point());
            Check(newEnemy.hp==newHp&&continuing.hitActors.Count==2,
                "A genuinely hit respawned actor must be immune to duplicate contacts from the same bolt");
        }

        public static void PiercingVolleysKeepEveryContactWithReusableTargetSnapshot(Campaign data)
        {
            var g=Fresh(data);g.Enter(1);g.enemies.Clear();
            g.save.x=12;g.save.y=14;g.save.reducedMotion=true;
            var front=new Enemy(6671,0,12.5f,14,20){stun=99,timer=999};
            var middle=new Enemy(6672,0,13.35f,14,20){stun=99,timer=999};
            var rear=new Enemy(6673,0,14.7f,14,999){stun=99,timer=999};
            g.enemies.Add(front);g.enemies.Add(middle);g.enemies.Add(rear);
            var arrow=new Projectile { position=new Point(12,14),
                velocity=new Point(20,0),life=3,damage=60,
                friendly=true,piercing=true,visual="arrow" };
            g.projectiles.Add(arrow);
            for(int i=0;i<5;i++)g.Tick(.05f,new Point());
            Check(!g.enemies.Contains(front)&&!g.enemies.Contains(middle)&&
                  g.enemies.Contains(rear)&&rear.hp<999,
                "Piercing sweep still kills both front targets and damages the survivor across multiple substeps");
            Check(arrow.hits.Contains(front.id)&&arrow.hits.Contains(middle.id)&&
                  arrow.hits.Contains(rear.id)&&arrow.hits.Count==3,
                "A piercing projectile never double-hits a dead/recoiling enemy when target snapshots are reused");
            float remaining=rear.hp;
            for(int i=0;i<5;i++)g.Tick(.05f,new Point());
            Check(rear.hp==remaining&&arrow.hits.Count==3,
                "Already-hit targets are not damaged again by later arrow flight frames");
        }

        public static void NonDamagingContactsCannotGrantFreeHitImmunity(Campaign data)
        {
            var g=Fresh(data);g.enemies.Clear();g.save.reducedMotion=true;
            g.save.health=50;g.invulnerable=0;g.playerHitTime=0;
            int revision=g.revision;int effects=g.effects.Count;
            var hurt=typeof(GameCore).GetMethod("Hurt",
                System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            foreach(float amount in new []{0f,-10f,float.NaN,float.PositiveInfinity})
            {
                hurt.Invoke(g,new object[]{amount,"area",null});
                Check(g.save.health==50&&g.invulnerable==0&&
                      g.playerHitTime==0&&g.effects.Count==effects&&!g.Dying&&
                      g.revision==revision,
                    "Invalid and cosmetic zero-area contacts do not stagger or grant invulnerability");
            }
            // Real damage remains effective on the very next contact; the
            // inert preview must not have consumed the hero's vulnerability.
            hurt.Invoke(g,new object[]{12f,"area",null});
            Check(g.save.health<50&&g.invulnerable>0&&g.playerHitTime>0&&
                  g.effects.Count>effects,
                "Authentic positive strike still deals damage and shows the existing hit reaction");
        }

        public static void ForcedShorelineRescueResetsHeroWalkCycleWithoutPhantomFootsteps(Campaign data)
        {
            MapData previous=data.maps[5];
            try
            {
                var rows=new string[25];
                for(int y=0;y<25;y++)
                {
                    char[] cells=new string('#',25).ToCharArray();
                    if(y>=2&&y<=22)for(int x=2;x<=22;x++)cells[x]='.';
                    if(y==10)cells[11]='%';
                    rows[y]=new string(cells);
                }
                data.maps[5]=new MapData{
                    id=5,spawnX=10.5f,spawnY=10.5f,rows=rows,
                    objects=new WorldObject[0],decor=new Decoration[0]};
                var g=Fresh(data,0);g.enemies.Clear();
                g.save.room=5;
                g.save.x=11.5f;g.save.y=10.5f;
                g.save.totalTime=21.98f;g.save.reducedMotion=true;
                g.walkDistance=8.7f;g.footContacts=17;
                Check(g.LowTide&&g.CanStand(g.Position)&&
                    g.WalkPosePhase>.01f,
                    "Real exposed-tide hero begins with a nonzero accumulated physical walk-sprite phase");
                Point flooded=g.Position;
                g.Tick(.04f,new Point());
                Check(!g.LowTide&&g.CanStand(g.Position)&&
                    (g.Position-flooded).Length>.3f,
                    "An actual high-tide collision forces shoreline recovery of the previously grounded hero");
                Check(g.walkDistance==0f&&g.WalkFrame==0&&
                    g.WalkPosePhase==0f&&g.footContacts==17,
                    "Safety teleport creates neither a phantom footstep nor a reused leg frame; next walk starts from planted canonical pose");
                Check(g.save.version==2&&g.mode=="world",
                    "Temporary walk-reset stays independent of legacy saves, level state and active world play");
            }
            finally{data.maps[5]=previous;}
        }

        public static void ForcedPlayerTideEvacuationCancelsOnlyPhantomDodgeReward(Campaign data)
        {
            MapData previous=data.maps[5];
            try
            {
                var rows=new string[25];
                for(int y=0;y<25;y++)
                {
                    char[] cells=new string('#',25).ToCharArray();
                    if(y>=2&&y<=22)
                        for(int x=2;x<=22;x++)cells[x]='.';
                    if(y==10)cells[11]='%';
                    rows[y]=new string(cells);
                }
                data.maps[5]=new MapData{
                    id=5,spawnX=10.5f,spawnY=10.5f,rows=rows,
                    objects=new WorldObject[0],decor=new Decoration[0]};
                var escapeField=typeof(GameCore).GetField("dodgeEscapeTime",
                    System.Reflection.BindingFlags.Instance|
                    System.Reflection.BindingFlags.NonPublic);
                var originField=typeof(GameCore).GetField("dashOrigin",
                    System.Reflection.BindingFlags.Instance|
                    System.Reflection.BindingFlags.NonPublic);
                var g=Fresh(data,0);g.enemies.Clear();
                g.save.room=5;g.save.totalTime=21.98f;
                g.save.x=11.5f;g.save.y=10.5f;
                g.save.energy=100f;g.save.reducedMotion=true;
                Check(g.LowTide&&g.CanStand(g.Position),
                    "Hero starts in the playable real shoal while outgoing tide is still low");
                Point endangered=g.Position;
                g.Dash(new Point(1,0));
                Check(g.perfectDodgeArmed&&(float)escapeField.GetValue(g)>.29f&&
                    g.dashTime>.17f&&g.dashCooldown>.7f&&
                    g.save.energy==80f,
                    "A physical charged player dash is legitimately committed before the tidal transition");
                g.Tick(.04f,new Point());
                Check(!g.LowTide&&g.CanStand(g.Position)&&
                    (g.Position-endangered).Length>.3f&&
                    g.dashTime<=0f&&(float)escapeField.GetValue(g)<=0f&&
                    !g.perfectDodgeArmed&&
                    (((Point)originField.GetValue(g))-g.Position).Length<.001f,
                    "Actual tide-forced player teleport cancels invalid active dodge and its unearned counter eligibility");
                Check(g.dashCooldown>.6f&&
                    g.save.energy<81f&&g.save.energy>=80f&&
                    g.invulnerable>0f&&g.save.version==2,
                    "Tidal safety recovery does not refund a paid dash, erase legitimate remaining impact protection or mutate old saves");
            }
            finally{data.maps[5]=previous;}
        }

        public static void ActualRisingTideClearsStrandedCivilianGaitAndEnemyStockIntent(Campaign data)
        {
            MapData previous=data.maps[5];
            try
            {
                var rows=new string[25];
                for(int y=0;y<25;y++)
                {
                    char[] cells=new string('#',25).ToCharArray();
                    if(y>=2&&y<=22)for(int x=2;x<=22;x++)cells[x]='.';
                    if(y==10)cells[11]='%';
                    rows[y]=new string(cells);
                }
                data.maps[5]=new MapData{
                    id=5,spawnX=10.5f,spawnY=10.5f,rows=rows,
                    objects=new WorldObject[0],decor=new Decoration[0]};
                var g=Fresh(data,0);
                g.enemies.Clear();
                g.save.room=5;g.save.totalTime=21.98f;
                g.save.x=10.5f;g.save.y=10.5f;
                g.save.reducedMotion=true;
                g.reliefMissionRoom=5;g.reliefMissionWave=3;
                g.reliefMissionTime=100f;g.reliefFollowerSafety=90f;
                g.reliefFollowerPosition=new Point(11.5f,10.5f);
                g.reliefFollowerWaiting=true;
                g.reliefFollowerMoving=true;
                g.reliefFollowerWalkDistance=42f;
                var raider=new Enemy(99771,2,11.5f,10.5f,10000f)
                {
                    state=1,timer=.5f,
                    targetingWayfarer=true,
                    targetingReliefCivilian=true,
                    targetingAfterlightStock=true
                };
                g.enemies.Add(raider);
                Check(g.LowTide&&g.CanStand(g.reliefFollowerPosition)&&
                    g.CanStand(raider.Position)&&g.ReliefEscortActive,
                    "Both real actors stand on a formerly exposed tidal bank during an active wave-three civilian escort");
                float safety=g.reliefFollowerSafety;
                float time=g.reliefMissionTime;
                g.Tick(.04f,new Point());
                Check(!g.LowTide&&g.CanStand(g.Position)&&
                    g.CanStand(g.reliefFollowerPosition)&&
                    g.CanStand(raider.Position),
                    "Actual world Tick crosses rising tide and recovers both stranded actors onto legal physical footing");
                Check(!g.reliefFollowerMoving&&g.reliefFollowerWalkDistance<.01f&&
                    g.reliefFollowerWaiting&&g.ReliefEscortActive&&
                    g.reliefFollowerSafety<=safety&&
                    g.reliefFollowerSafety>safety-.1f&&
                    g.reliefMissionTime>time-.1f,
                    "A rescued civilian teleported onto shore clears old walk animation, keeps its waiting order and mission, and incurs at most the ordinary real-time weather loss");
                Check(raider.state==0&&raider.releaseTime<=0&&
                    !raider.targetingWayfarer&&!raider.targetingReliefCivilian&&
                    !raider.targetingAfterlightStock,
                    "The same flood cancels every old raider attack target, including supply-station intent, without generating false mission damage");
                Check(g.save.version==2,
                    "All recovery state remains transient with the exact old save schema");
            }
            finally{data.maps[5]=previous;}
        }

        public static void RisingTideCancelsUnfiredCompanionAutoShotWithoutFullCadence(Campaign data)
        {
            MapData previous=data.maps[5];
            try
            {
                var rows=new string[25];
                for(int y=0;y<25;y++)
                {
                    char[] cells=new string('#',25).ToCharArray();
                    if(y>=2&&y<=22)
                        for(int x=2;x<=22;x++)cells[x]='.';
                    if(y==10)cells[11]='%';
                    rows[y]=new string(cells);
                }
                data.maps[5]=new MapData{
                    id=5,spawnX=10.5f,spawnY=10.5f,rows=rows,
                    objects=new WorldObject[0],decor=new Decoration[0]};
                var attackClock=typeof(GameCore).GetField("companionAttack",
                    System.Reflection.BindingFlags.Instance|
                    System.Reflection.BindingFlags.NonPublic);
                var targetField=typeof(GameCore).GetField("companionAutoTarget",
                    System.Reflection.BindingFlags.Instance|
                    System.Reflection.BindingFlags.NonPublic);
                foreach(bool firedBeforeTide in new[]{false,true})
                {
                    var g=Fresh(data,0);g.enemies.Clear();
                    g.save.x=10.5f;g.save.y=10.5f;
                    g.save.reducedMotion=true;g.save.totalTime=21f;
                    g.save.flags.Add("companion_1");
                    g.SelectCompanion(1);
                    g.save.room=5;
                    g.companionPosition=new Point(11.5f,10.5f);
                    var enemy=new Enemy(99601,0,14.5f,10.5f,10000f)
                        {stun=99f,timer=999f,staggerRecovery=100f};
                    g.enemies.Add(enemy);
                    attackClock.SetValue(g,0f);
                    g.Tick(.02f,new Point());
                    ActorAction action=g.companionAction;
                    Check(action!=null&&action.kind=="attack"&&
                        !action.released&&
                        (float)attackClock.GetValue(g)>2.6f&&
                        targetField.GetValue(g)==enemy&&
                        g.CanStand(g.companionPosition),
                        "Real ranged companion automatically begins its own 2.8s paid draw from the exposed tidal bank");
                    if(firedBeforeTide)
                    {
                        Advance(g,.17f);
                        Check(action.released&&g.projectiles.Exists(p=>p.friendly&&
                            p.visual=="arrow"),
                            "The ordinary automatic shot must actually leave the bow before testing a spent attack cadence");
                    }
                    // Recovery may be followed by one VALID walking step
                    // in the same world frame. Seed an obsolete prior gait
                    // phase to prove it cannot leak through the teleport.
                    g.companionMoveTime=25f;
                    g.save.totalTime=21.98f;
                    g.Tick(.04f,new Point());
                    Check(!g.LowTide&&g.CanStand(g.companionPosition)&&
                        g.companionAction==null&&
                        targetField.GetValue(g)==null&&
                        g.companionMoveTime<.35f,
                        "True rising tide evacuates the companion, drops stale targeting and resets the old airborne leg phase");
                    float cadence=(float)attackClock.GetValue(g);
                    if(firedBeforeTide)
                        Check(cadence>2f,
                            "A volley already physically released before rising tide retains its normal spent automatic cooldown");
                    else
                        Check(cadence>=0f&&cadence<=.23f&&
                            !action.released&&g.projectiles.Count==0,
                            "An interrupted un-fired automatic shot retries promptly instead of charging 2.8s for nothing");
                }
            }
            finally{data.maps[5]=previous;}
        }

        public static void RisingTideRefundsOnlyUnreleasedCompanionOrders(Campaign data)
        {
            // Room-five collision really changes at totalTime%36 == 22.
            // RecoverTideActors must not bill an order interrupted BEFORE
            // contact, but a committed arrow volley remains paid even
            // though its caster is evacuated before recovery animation.
            MapData original=data.maps[5];
            try
            {
                var rows=new string[25];
                for(int y=0;y<25;y++)
                {
                    char[] row=new string('#',25).ToCharArray();
                    if(y>=2&&y<=22)
                        for(int x=2;x<=22;x++)row[x]='.';
                    if(y==10)row[11]='%';
                    rows[y]=new string(row);
                }
                data.maps[5]=new MapData{
                    id=5,spawnX=10.5f,spawnY=10.5f,rows=rows,
                    objects=new WorldObject[0],decor=new Decoration[0]};
                foreach(bool releaseBeforeTide in new[]{false,true})
                {
                    var g=Fresh(data,0);g.enemies.Clear();
                    g.save.x=10.5f;g.save.y=10.5f;
                    g.save.reducedMotion=true;g.save.totalTime=21f;
                    g.save.flags.Add("companion_1");
                    g.SelectCompanion(1);
                    g.save.room=5;
                    g.companionPosition=new Point(11.5f,10.5f);
                    Check(g.save.companion==1&&g.LowTide&&
                        g.CanStand(g.companionPosition),
                        "Ivo genuinely stands on the still-exposed tidal tile and the player stands on a permanent legal shore");
                    g.CommandCompanion();
                    ActorAction order=g.companionAction;
                    Check(order!=null&&order.kind=="command"&&
                        !order.released&&g.companionCooldown>17f,
                        "The physical manual three-arrow command reserves its normal 18-second cooldown");
                    if(releaseBeforeTide)
                    {
                        Advance(g,.23f);
                        Check(order.released&&g.projectiles.Count==3&&
                            g.companionCooldown>17f,
                            "A real bow volley leaves Ivo's weapon while the tide is still low");
                    }
                    else
                        Check(g.projectiles.Count==0,
                            "A prepared but uncommitted ally order has not generated any projectile");
                    g.save.totalTime=21.98f;
                    g.Tick(.04f,new Point());
                    Check(!g.LowTide&&g.CanStand(g.Position)&&
                        g.CanStand(g.companionPosition)&&
                        g.companionAction==null,
                        "Actual rising tide evacuates the ally to a physical legal tile and cancels its old cast pose");
                    if(releaseBeforeTide)
                        Check(g.companionCooldown>17f,
                            "Tide evacuation cannot duplicate or refund a previously released three-arrow skill");
                    else
                        Check(g.companionCooldown<=0f&&
                            !order.released&&g.projectiles.Count==0,
                            "A tidal interruption before contact must refund an unspent manual order with no phantom attack");
                }
            }
            finally{data.maps[5]=original;}
        }

        public static void SafeCompanionSwapRefundsUnreleasedButNotSpentOrder(Campaign data)
        {
            // A safe party swap is genuine player input. Test a bow order
            // and Sen's independently owned grounded spell, including their
            // actual 200ms contact frames and the global shared cooldown.
            MapData previous=data.maps[0];
            try
            {
                var rows=new string[25];
                for(int y=0;y<25;y++)
                {
                    char[] row=new string('#',25).ToCharArray();
                    if(y>=2&&y<=22)for(int x=2;x<=22;x++)row[x]='.';
                    rows[y]=new string(row);
                }
                data.maps[0]=new MapData {
                    id=0,spawnX=10.5f,spawnY=11.5f,rows=rows,
                    objects=new WorldObject[0],decor=new Decoration[0]};
                foreach(int id in new[]{1,2})
                {
                    var g=Fresh(data,0);g.enemies.Clear();
                    g.save.x=10.5f;g.save.y=11.5f;
                    g.save.reducedMotion=true;
                    g.save.flags.Add("companion_0");
                    g.save.flags.Add("companion_"+id);
                    g.SelectCompanion(id);
                    Check(g.save.companion==id&&g.SafeTravel,
                        "Actual unlocked bow/mage ally can be selected safely with no active combat");
                    g.CommandCompanion();
                    ActorAction abandoned=g.companionAction;
                    Check(abandoned!=null&&!abandoned.released&&
                        abandoned.weapon==id&&g.companionCooldown>11f,
                        "A genuine manual ally command reserves the long global cooldown before physical release");
                    g.SelectCompanion(0);
                    Check(g.save.companion==0&&g.companionAction==null&&
                        !abandoned.released&&g.companionCooldown<=0f&&
                        g.projectiles.Count==0,
                        "A player-canceled unspent ally command changes party but refunds exactly its unreleased cooldown");
                    g.SelectCompanion(id);
                    g.CommandCompanion();
                    ActorAction released=g.companionAction;
                    Check(released!=null&&released!=abandoned&&
                        !released.released,
                        "Returning to the original class accepts a genuinely new order without cooldown bypass");
                    Advance(g,.23f);
                    Check(released.released&&g.companionCooldown>11f&&
                        (id==1 ? g.projectiles.Count==3 :
                            g.effects.Exists(f=>f.kind=="frost")),
                        "The legal rescued command really releases three arrows or a grounded frost pulse and charges its ordinary cooldown");
                    float paid=g.companionCooldown;
                    g.SelectCompanion(0);
                    Check(g.save.companion==0&&g.companionAction==null&&
                        g.companionCooldown==paid,
                        "Switching AFTER a real companion skill contact cannot erase its already spent shared cooldown");
                    g.CommandCompanion();
                    Check(g.companionAction==null,
                        "The other companion cannot circumvent a legitimately earned long cooldown");
                }
            }
            finally{data.maps[0]=previous;}
        }

        public static void ReselectingSameCompanionPreservesInFlightCommand(Campaign data)
        {
            var g=Fresh(data);g.enemies.Clear();
            g.save.flags.Add("companion_0");g.save.flags.Add("companion_1");
            g.SelectCompanion(0);
            g.save.health=35;g.save.energy=100;
            g.CommandCompanion();
            ActorAction active=g.companionAction;
            Check(active!=null&&active.kind=="command"&&g.companionCooldown>0,
                "An authentic healer command has a pending release and a charged cooldown");
            int revision=g.revision;
            Point position=g.companionPosition;
            float cooldown=g.companionCooldown;
            g.SelectCompanion(0);
            Check(g.companionAction==active&&g.revision==revision&&
                  g.companionCooldown==cooldown&&
                  (g.companionPosition-position).Length<.001f,
                "Selecting the already-active healer cannot cancel a charged heal or teleport the ally");
            Advance(g,.22f);
            Check(active.released&&g.save.health>35,
                "The authentic healer release completes even after a redundant party selection");
            g.SelectCompanion(1);
            Check(g.save.companion==1&&g.companionAction==null&&
                  g.revision>revision,
                "Selecting another unlocked ally still performs a genuine party change");
            g.CommandCompanion();
            Check(g.companionAction==null,
                "An earlier command cooldown cannot be bypassed by swapping companions");
        }

        public static void InvalidOffensiveDamageNeverHealsOrCreatesPhantomHits(Campaign data)
        {
            var g=Fresh(data);g.enemies.Clear();g.save.reducedMotion=true;
            var e=new Enemy(784,0,13,14,80){stun=999,timer=999};
            g.enemies.Add(e);
            int seed=g.save.seed,revision=g.revision;
            var hit=typeof(GameCore).GetMethod("Hit",
                System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            foreach(float amount in new []{0f,-8f,float.NaN,float.NegativeInfinity})
            {
                hit.Invoke(g,new object[]{e,amount,false,null,null});
                Check(e.hp==80&&e.stagger==0&&g.save.seed==seed&&
                      g.effects.Count==0&&g.revision==revision&&
                      g.enemies.Contains(e),
                    "An invalid player damage number cannot heal or stagger foes or spend the crit seed");
            }
            hit.Invoke(g,new object[]{e,15f,false,null,null});
            Check(e.hp<80&&g.effects.Count>0&&g.save.seed!=seed,
                "A genuine subsequent positive hit still uses the complete damage and visual pipeline");
        }

        public static void RealWeaponChangesRestartWeaponComboButReselectDoesNot(Campaign data)
        {
            var g=Fresh(data);g.enemies.Clear();g.save.energy=100;
            g.save.flags.Add("weapon_1");
            g.Attack(new Point(1,0));
            Check(g.playerAction!=null&&g.comboTimer>0&&g.combo==0,
                "Sword contact starts the actual uninterrupted combo window");
            g.playerAction=null;g.attackCooldown=0;
            g.Attack(new Point(1,0));
            Check(g.combo==1&&g.comboTimer>0,
                "Two real sword attacks are counted in the same combo");
            g.Equip(0);
            Check(g.combo==1&&g.comboTimer>0,
                "Reselecting the exact same sword cannot reset earned combo progress");
            g.Equip(1);
            Check(g.save.weapon==1&&g.combo==0&&g.comboTimer==0&&
                  g.playerAction==null,
                "A genuine switch to the unlocked bow discards obsolete sword combo state");
            g.Equip(0);g.attackCooldown=0;
            g.Attack(new Point(1,0));
            Check(g.playerAction!=null&&g.combo==0,
                "Returning to the sword starts a first hit rather than carrying a phantom third-hit boost");
        }

        public static void StyleChangesRestartCombosButRepeatedStylesPreserveAttacks(Campaign data)
        {
            var g=Fresh(data);g.enemies.Clear();g.save.flags.Add("core_1");
            g.save.energy=100;
            g.Attack(new Point(1,0));
            Check(g.playerAction!=null&&g.combo==0&&g.comboTimer>0,
                "A real starter sword swing begins a live combat sequence");
            ActorAction first=g.playerAction;
            int previous=g.revision;
            g.SetStyle(0);
            Check(g.playerAction==first&&g.comboTimer>0&&g.revision==previous,
                "Clicking the existing balanced style does not cancel an attack or mutate the save");
            g.playerAction=null;g.attackCooldown=0;
            g.Attack(new Point(1,0));
            Check(g.combo==1&&g.comboTimer>0,
                "A second real sword swing advances the existing chain");
            g.SetStyle(1);
            Check(g.Style==1&&g.playerAction==null&&g.combo==0&&g.comboTimer==0,
                "Changing to unlocked focused sword style retires the old combo");
            g.SetStyle(0);g.attackCooldown=0;g.Attack(new Point(1,0));
            Check(g.combo==0&&g.playerAction!=null,
                "Returning to balanced style begins a genuine first strike, not a borrowed third-hit bonus");
        }


        public static void DamageInterruptsLateAttackButSafeContactDoesNot(Campaign data)
        {
            var g=Quiet(data);g.enemies.Clear();
            g.Skill(new Point(1,0));
            Advance(g,.54f);
            ActorAction cast=g.playerAction;
            Check(cast!=null&&cast.kind=="skill"&&cast.released,
                "A real completed Q contact remains live through recovery");
            g.Attack(new Point(0,-1));
            Check(g.AttackQueued&&g.playerAction==cast,
                "Authentic late LMB has been accepted without spending attack energy");

            var hurt=typeof(GameCore).GetMethod("Hurt",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            Check(hurt!=null,"Production GameCore must own real incoming damage");
            float before=g.save.health;
            g.invulnerable=1f;
            hurt.Invoke(g,new object[]{8f,"shot",null});
            Check(g.AttackQueued&&Math.Abs(g.save.health-before)<.0001f,
                "A successfully ignored arrow cannot cancel an unrelated genuine input buffer");
            // A real hit starts its own single whole-body Hit state and
            // must retire the previous Cast-to-LMB confirmation immediately.
            g.invulnerable=0f;
            hurt.Invoke(g,new object[]{8f,"environment",null});
            Check(g.save.health<before&&g.playerHitTime>0f&&
                !g.AttackQueued,
                "Accepted damage must cancel buffered LMB before painting the hit/recoil body");
            Advance(g,.9f);
            Check(g.playerAction==null&&g.combo==0&&
                !g.AttackQueued,
                "A hit-interrupted queued attack must never reappear after stun and recovery");

            var harmless=Quiet(data);harmless.enemies.Clear();
            harmless.Skill(new Point(1,0));Advance(harmless,.54f);
            harmless.Attack(new Point(0,-1));
            Check(harmless.AttackQueued,
                "Invalid incoming damage must be tested with one real accepted pending LMB");
            hurt.Invoke(harmless,new object[]{0f,"shot",null});
            hurt.Invoke(harmless,new object[]{-9f,"environment",null});
            Check(harmless.AttackQueued,
                "Cosmetic warnings and malformed damage must not silently discard an accepted buffered press");
            Advance(harmless,.3f);
            Check(harmless.playerAction!=null&&
                harmless.playerAction.kind=="attack",
                "After harmless contacts the original buffered Q-to-LMB executes once at its genuine post-Cast boundary");
        }

        public static void LateAttackAfterSkillKeepsWholeBodyRecovery(Campaign data)
        {
            // The prior LMB path considered Cast recovery "unblocked" and
            // started a new Attack immediately once attackCooldown hit zero.
            // That truncated the canonical golden-haired whole-body Cast
            // and could replace its recovery pixels mid-frame.
            foreach(float step in new[]{.01f,.02f,.05f})
            {
                var g=Quiet(data);g.enemies.Clear();
                g.Skill(new Point(1,0));
                ActorAction cast=g.playerAction;
                Check(cast!=null&&cast.kind=="skill",
                    "Real Q must create a canonical actor skill action");
                while(cast.elapsed<cast.duration-.10f&&
                      g.playerAction==cast)
                    g.Tick(step,new Point());
                Check(g.playerAction==cast&&cast.released,
                    "Q remains visually authoritative through late released recovery");
                float committedEnergy=g.save.energy;
                g.Attack(new Point(0,-1));
                Check(g.playerAction==cast&&
                    Math.Abs(g.save.energy-committedEnergy)<.0001f,
                    "LMB in released Q recovery must not cut its body or pay attack energy early");
                g.SetAim(new Point(-1,0));
                ActorAction follow=null;
                for(int i=0;i<70;i++)
                {
                    g.Tick(step,new Point());
                    if(g.playerAction!=cast)
                    {follow=g.playerAction;break;}
                }
                Check(follow!=null&&follow.kind=="attack"&&
                    follow.direction.y<-.99f&&g.combo==0,
                    "Buffered Q-to-LMB must begin one authentic attack after the previous complete Cast, using the original LMB direction");
                Advance(g,.9f);
                Check(g.playerAction==null&&g.combo==0,
                    "One late Q-to-LMB tap never repeats as an extra combo attack");
            }

            var early=Quiet(data);early.enemies.Clear();
            early.Skill(new Point(1,0));ActorAction original=early.playerAction;
            Advance(early,.33f);
            Check(original.released&&early.playerAction==original,
                "Early Q recovery begins after real skill release");
            early.Attack(new Point(0,-1));
            Check(early.playerAction==original,
                "Even an unbufferable early LMB cannot overwrite source-authored Cast recovery");
            Advance(early,.7f);
            Check(early.playerAction==null&&early.combo==0,
                "Input outside the final 160ms cannot fire a delayed phantom strike");

            var change=Quiet(data);change.enemies.Clear();
            change.save.flags.Add("weapon_1");
            change.Skill(new Point(1,0));ActorAction preceding=change.playerAction;
            Advance(change,.54f);change.Attack(new Point(0,-1));
            Check(change.playerAction==preceding,
                "Late Q-to-LMB starts only as a pending intent, not a new actor action");
            change.Equip(1);
            Advance(change,.7f);
            Check(change.playerAction==null&&change.combo==0,
                "Equipment change cancels buffered Q-to-LMB rather than changing the hero model or spending a phantom attack");

            var dodge=Quiet(data);dodge.enemies.Clear();
            dodge.Skill(new Point(1,0));
            Advance(dodge,.54f);dodge.Attack(new Point(0,-1));
            dodge.Dash(new Point(1,0));
            Check(dodge.dashTime>0f&&
                (dodge.playerAction==null||dodge.playerAction.kind!="attack"),
                "Actual dodge cancels Q-to-LMB and cannot leave a second committed attack");
            Advance(dodge,.7f);
            Check(dodge.playerAction==null&&dodge.combo==0,
                "Canceled queued attack does not reappear after dodge recovery");
        }

        public static void RepeatedLateLmbCannotReplaceCommittedDirectionOrDeadline(Campaign data)
        {
            foreach(float step in new[]{.01f,.02f,.05f})
            {
                var g=Quiet(data);g.enemies.Clear();
                g.Attack(new Point(1f,0f));
                ActorAction previous=g.playerAction;
                while(previous.elapsed<previous.duration-.10f&&
                      g.playerAction==previous)
                    g.Tick(step,new Point());
                Check(previous.released&&g.playerAction==previous,
                    "Repeated LMB fixture requires real released attack recovery");
                Point committedTarget=new Point(20f,14f);
                g.SetAimTarget(committedTarget);
                g.Attack(new Point(0f,-1f));
                Check(g.AttackQueued&&g.playerAction==previous,
                    "Original LMB must create a single deferred attack");
                Check((g.AttackQueuedDirection-new Point(0f,-1f)).Length<.001f&&
                    g.AttackQueuedTarget.HasValue&&
                    (g.AttackQueuedTarget.Value-committedTarget).Length<.001f,
                    "In-game queued attack preview must expose the REAL first accepted aim and pointer without a new action");
                var pending=typeof(GameCore).GetField("bufferedAttack",
                    System.Reflection.BindingFlags.Instance|
                    System.Reflection.BindingFlags.NonPublic);
                Check(pending!=null,"Real private buffered LMB intent must exist");
                object intent=pending.GetValue(g);
                var remaining=intent.GetType().GetField("remaining");
                float initial=(float)remaining.GetValue(intent);
                g.Tick(step,new Point());
                float elapsed=(float)remaining.GetValue(intent);
                Check(elapsed<initial&&elapsed>0f,
                    "Actual Tick must consume, not reset, first tap lifetime");
                // Changing both cursor and subsequent button direction
                // cannot rotate the already committed source action.
                g.SetAim(new Point(1f,0f));
                g.SetAimTarget(new Point(33f,17f));
                g.Attack(new Point(0f,1f));
                Check(System.Object.ReferenceEquals(intent,pending.GetValue(g))&&
                    Math.Abs((float)remaining.GetValue(intent)-elapsed)<.00001f,
                    "Repeated held LMB must not replace intent or extend original 160ms deadline");
                Check((g.AttackQueuedDirection-new Point(0f,-1f)).Length<.001f&&
                    g.AttackQueuedTarget.HasValue&&
                    (g.AttackQueuedTarget.Value-committedTarget).Length<.001f,
                    "Continuous later LMB samples must not swivel the committed ranged target/facing reticle");
                ActorAction next=null;
                for(int i=0;i<70;i++)
                {
                    g.Tick(step,new Point());
                    if(g.playerAction!=previous)
                    {next=g.playerAction;break;}
                }
                Check(next!=null&&next.kind=="attack"&&
                    next.direction.y<-.99f&&next.direction.x<.01f&&
                    next.target.HasValue&&
                    (next.target.Value-committedTarget).Length<.001f,
                    "One physical follow-up must keep FIRST pressed LMB aim AND real target regardless of repeated later taps");
                Check(!g.AttackQueued&&g.AttackQueuedDirection.Length<.001f&&
                    !g.AttackQueuedTarget.HasValue,
                    "Committed LMB world marker must vanish immediately after its actual dispatch");
                Advance(g,.9f);
                Check(g.playerAction==null&&g.combo==1,
                    "Repeated late taps must dispatch exactly one following hit, never a third ghost swing");
            }
        }

        public static void LateBasicAttacksBufferOnceAndRespectCombatProfiles(Campaign data)
        {
            foreach (float step in new[] { .01f, .02f, .03f, .05f })
            {
                var g = Quiet(data); g.enemies.Clear();
                g.Attack(new Point(1, 0));
                ActorAction first = g.playerAction;
                while (first.elapsed < first.duration - .10f && g.playerAction == first)
                    g.Tick(step, new Point());
                Check(g.playerAction == first && first.released,
                    "A late input still belongs to a real released sword attack");
                g.Attack(new Point(0, -1));
                Check(g.playerAction == first && g.combo == 0,
                    "Pressing during recovery never cuts off the old contact pose");
                g.SetAim(new Point(1, 0));
                ActorAction next = null;
                for (int frame = 0; frame < 50; frame++)
                {
                    g.Tick(step, new Point());
                    if (g.playerAction != first) { next = g.playerAction; break; }
                }
                Check(next != null && next.kind == "attack" && g.combo == 1 &&
                    next.direction.y < -.99f,
                    "A single late press chains one real second strike with its original facing at step " + step);
                for (int frame = 0; frame < 70; frame++) g.Tick(step, new Point());
                Check(g.playerAction == null && g.combo == 1,
                    "A buffered tap must never self-repeat as a third strike");
            }
            var early = Quiet(data); early.enemies.Clear(); early.Attack(new Point(1, 0));
            early.Tick(.05f, new Point()); early.Attack(new Point(0, -1));
            Advance(early, .7f);
            Check(early.playerAction == null && early.combo == 0,
                "An early press during the first windup cannot queue a delayed phantom attack");
            var same = Quiet(data); same.enemies.Clear(); same.Attack(new Point(1, 0));
            Advance(same, .27f); same.Attack(new Point(0, -1));
            same.Equip(0); same.SetStyle(0);
            Advance(same, .12f);
            Check(same.playerAction != null && same.combo == 1,
                "Re-selecting an unchanged weapon and style preserves the late attack");
            var changed = Quiet(data); changed.enemies.Clear();
            changed.save.flags.Add("core_1"); changed.Attack(new Point(1, 0));
            Advance(changed, .27f); changed.Attack(new Point(0, -1)); changed.SetStyle(1);
            Advance(changed, .5f);
            Check(changed.playerAction == null && changed.combo == 0 &&
                changed.Style == 1, "A genuine style change drops the obsolete buffered attack and combo");
            var equipped = Quiet(data); equipped.enemies.Clear();
            equipped.save.flags.Add("weapon_1"); equipped.Attack(new Point(1, 0));
            Advance(equipped, .27f); equipped.Attack(new Point(0, -1)); equipped.Equip(1);
            Advance(equipped, .5f);
            Check(equipped.playerAction == null && equipped.combo == 0,
                "Changing weapon cannot replay the old sword buffer as a bow attack");
            var priority = Quiet(data); priority.enemies.Clear();
            priority.Attack(new Point(1, 0)); Advance(priority, .27f);
            priority.Attack(new Point(0, -1)); priority.Skill(new Point(1, 0));
            Check(priority.playerAction != null && priority.playerAction.kind == "skill",
                "A real Q input takes priority over the pending basic strike");
            Advance(priority, 1f);
            Check(priority.playerAction == null && priority.combo == 0,
                "A queued basic strike does not fire after the skill");
            var limited = Quiet(data); limited.enemies.Clear(); limited.save.flags.Add("core_1");
            limited.SetStyle(1); limited.Attack(new Point(1, 0));
            Advance(limited, .34f); limited.Attack(new Point(0, -1));
            limited.save.energy = 0;
            Advance(limited, .4f);
            Check(limited.playerAction == null && limited.combo == 0,
                "Buffered focused strike checks energy when dispatched, not when queued");
            var loaded = Quiet(data); loaded.enemies.Clear();
            loaded.Attack(new Point(1, 0)); Advance(loaded, .27f);
            loaded.Attack(new Point(0, -1));
            loaded.PrepareSave(); loaded.Load(CloneSave(loaded.save));
            Advance(loaded, .7f);
            Check(loaded.playerAction == null && loaded.combo == 0,
                "Transient buffer is never carried into a newly loaded v2 save");
        }


        public static void RangedThirdHitsProduceDistinctRealProjectiles(Campaign data)
        {
            foreach (float frameStep in new[] { .01f, .02f, .03f, .05f })
            foreach (int weapon in new[] { 1, 2 })
            foreach (int style in new[] { 0, 1, 2 })
            {
                var g = Fresh(data, weapon); g.enemies.Clear();
                g.save.flags.Add("core_1"); g.save.energy = 100; g.SetStyle(style);
                Projectile[] shots = new Projectile[3];
                for (int index = 0; index < 3; index++)
                {
                    Check(g.attackCooldown <= .0001f && g.playerAction == null,
                        "Each ranger/mage attack starts after the actual preceding release");
                    g.projectiles.Clear();
                    g.Attack(new Point(1, 0));
                    ActorAction attack = g.playerAction;
                    Check(attack != null && attack.kind == "attack" && attack.combo == index,
                        "Real ranged chain advances through each of its three consecutive attacks");
                    for (int frame = 0; frame < 100 && !attack.released; frame++)
                        g.Tick(frameStep, new Point());
                    Check(attack.released, "Ranged attack reaches its visible contact before producing shots");
                    shots[index] = g.projectiles.Find(p => p.friendly);
                    Check(shots[index] != null && shots[index].impact != null &&
                        shots[index].impact.combo == index, "Finisher characteristics follow a real released attack");
                    Advance(g, Math.Max(0, attack.duration - attack.elapsed) + .04f);
                }
                float bonus = weapon == 1 ? 1.15f : 1.12f;
                Check(Math.Abs(shots[2].damage / shots[0].damage - bonus) < .001f &&
                    Math.Abs(shots[1].damage / shots[0].damage - 1f) < .001f,
                    "Only the true third ranged release earns the intended moderate damage payoff");
                if (weapon == 1)
                    Check(shots[0].piercing == (style == 1) &&
                        shots[2].piercing == (style != 2),
                        "Balanced third arrow pierces, Focused retains every-shot pierce, Flow keeps its spread balance");
                else
                    Check(shots[0].slowing == (style == 2) && shots[2].slowing,
                        "Staff third hit uses real projectile slow without removing Flow's existing all-hit slow");
                Check(g.effects.Exists(e => e.kind == "number" && e.text == "CHAIN FINISH") ||
                    !g.save.english, "Third-hit confirmation is emitted by the live release pipeline");
            }
        }

        public static void ArcherCompanionCannotWasteVolleyOnItsOwnFootTarget(Campaign data)
        {
            var g=Fresh(data);g.enemies.Clear();g.save.flags.Add("companion_1");
            g.SelectCompanion(1);
            g.SetAim(new Point(0,-1));
            Point ally=g.companionPosition;
            g.SetAimTarget(ally);
            g.CommandCompanion();
            ActorAction action=g.companionAction;
            Check(action!=null&&action.kind=="command"&&
                  (action.direction-new Point(0,-1)).Length<.001f&&
                  !action.target.HasValue,
                "Clicking the companion itself falls back to the facing rather than a zero-length firing direction");
            Advance(g,.23f);
            Check(g.projectiles.Count==3&&g.projectiles.TrueForAll(p=>p.velocity.Length>10&&p.velocity.y<0),
                "All actual commanded arrows fly away from the ally after an accidental self-target click");
            g.projectiles.Clear();
            g.companionCooldown=0;
            Point valid=g.companionPosition+new Point(4,0);
            g.SetAimTarget(valid);
            g.CommandCompanion();
            Check(g.companionAction!=null&&g.companionAction.target.HasValue&&
                  (g.companionAction.direction-new Point(1,0)).Length<.001f,
                "A genuinely different cursor target still controls the next aimed volley");
        }

    }
}
