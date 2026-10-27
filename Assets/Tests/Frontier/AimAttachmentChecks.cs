using System;
using System.Reflection;
namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        public static void ControllerAimAcquiresOnlyRealVisibleRangedTarget(Campaign data)
        {
            MapData original=data.maps[1];
            try
            {
                for(int profession=0;profession<3;profession++)
                {
                    var g=SprintTestWorld(data,profession);
                    g.save.weapon=1;g.enemies.Clear();
                    Point origin=g.Position;
                    var ahead=new Enemy(101,0,origin.x+5f,origin.y+.2f,80);
                    var behind=new Enemy(102,0,origin.x-5f,origin.y,80);
                    var offAxis=new Enemy(103,0,origin.x+3f,origin.y+3f,80);
                    g.enemies.Add(behind);g.enemies.Add(offAxis);g.enemies.Add(ahead);
                    float previousHp=ahead.hp, previousEnergy=g.save.energy;
                    Check(System.Object.ReferenceEquals(
                        g.AcquireRangedStickTarget(new Point(1,0)),ahead),
                        "A ranged right stick finds the first genuinely visible foe inside its tight forward cone");
                    Check(g.AcquireRangedStickTarget(new Point(0,1))==null,
                        "A target outside the narrow aiming cone cannot become an invisible lock");
                    Check(g.AcquireRangedStickTarget(new Point(float.NaN,1))==null&&
                        g.AcquireRangedStickTarget(new Point())==null,
                        "Nonphysical and neutral stick vectors never select a combat target");
                    Check(ahead.hp==previousHp&&g.save.energy==previousEnergy&&
                        g.save.version==2&&g.projectiles.Count==0,
                        "Target selection is read-only and never changes combat or original save authority");
                    char[] row=g.Map.rows[(int)ahead.y].ToCharArray();
                    int wall=(int)origin.x+2; row[wall]='#';
                    g.Map.rows[(int)ahead.y]=new string(row);
                    Check(g.AcquireRangedStickTarget(new Point(1,0))==null,
                        "A live opaque wall cancels a controller lock instead of shooting through stone");
                    row[wall]='.';g.Map.rows[(int)ahead.y]=new string(row);
                    ahead.hp=0;
                    Check(g.AcquireRangedStickTarget(new Point(1,0))==null,
                        "A defeated body immediately leaves aim acquisition");
                    ahead.hp=previousHp;g.save.weapon=0;
                    if(profession!=1)
                        Check(g.AcquireRangedStickTarget(new Point(1,0))==null,
                            "Sword-based area professions keep direct melee facing instead of remote target snap");
                    else
                        Check(System.Object.ReferenceEquals(
                            g.AcquireRangedStickTarget(new Point(1,0)),ahead),
                            "Ranger still acquires a live target for the authored Q volley while holding a sword");
                }
            }
            finally{data.maps[1]=original;}
        }

        public static void ControllerAimHonorsFrontBodiesAndStableLivingIdentity(Campaign data)
        {
            MapData original=data.maps[1];
            try
            {
                var g=SprintTestWorld(data,1);
                g.save.weapon=1;
                g.enemies.Clear();
                Point p=g.Position;
                var far=new Enemy(711,0,p.x+9f,p.y,90f);
                var front=new Enemy(712,0,p.x+3f,p.y+.2f,90f);
                g.enemies.Add(far);g.enemies.Add(front);
                Check(System.Object.ReferenceEquals(
                    g.AcquireRangedStickTarget(new Point(1,0)),front),
                    "A perfectly aligned rear enemy cannot steal an arrow that physically meets the front-row hurt circle");
                Check(System.Object.ReferenceEquals(
                    g.AcquireRangedStickTarget(new Point(1,0),far),front),
                    "An obsolete prior lock cannot override current living-body cover");
                g.enemies.Clear();
                var first=new Enemy(713,0,p.x+7f,p.y,80f);
                var second=new Enemy(714,0,p.x+7f,p.y+.9f,80f);
                g.enemies.Add(second);g.enemies.Add(first);
                Point smallTurn=new Point(1,.09f);
                Check(System.Object.ReferenceEquals(
                    g.AcquireRangedStickTarget(smallTurn),second),
                    "Unbiased stick preference initially chooses the better aligned live actor");
                Check(System.Object.ReferenceEquals(
                    g.AcquireRangedStickTarget(smallTurn,first),first),
                    "A valid previous target survives tiny analog drift without reticle thrash");
                Check(System.Object.ReferenceEquals(
                    g.AcquireRangedStickTarget(new Point(1,.24f),first),second),
                    "A genuine change in stick direction immediately overcomes soft target retention");
                first.hp=0;
                Check(System.Object.ReferenceEquals(
                    g.AcquireRangedStickTarget(smallTurn,first),second),
                    "Defeating the old target clears retention without touching another actor's identity");
                Check(g.save.version==2&&g.projectiles.Count==0,
                    "Front-row aiming and transient target retention preserve actual projectiles and v2 saves");
            }
            finally{data.maps[1]=original;}
        }

        public static void QuickEquipmentUsesOnlyRealUnlockedCombatProfiles(Campaign data)
        {
            foreach(int profession in new[]{0,1,2})
            {
                var g=Fresh(data,profession);
                g.enemies.Clear();
                g.save.flags.RemoveAll(f=>f=="weapon_1"||f=="weapon_2");
                g.save.weapon=0;
                float oldEnergy=g.save.energy;
                int oldRevision=g.revision;
                g.Equip(2);
                Check(g.save.weapon==0&&g.revision==oldRevision&&
                    g.save.energy==oldEnergy,
                    "Locked weapon refuses equip without effects or consumption");
                g.save.flags.Add("weapon_1");
                g.save.flags.Add("weapon_2");
                g.Equip(2);
                Check(g.save.weapon==2&&g.revision==oldRevision+1,
                    "Unlocked staff changes only through native weapon transaction");
                oldRevision=g.revision;
                g.Attack(new Point(1,0));
                ActorAction attack=g.playerAction;
                Check(attack!=null&&attack.equippedWeapon==2,
                    "The actual equipped full-body staff generates authentic class-specific attack");
                float spent=g.save.energy;
                g.Equip(2);
                Check(g.playerAction==attack&&g.revision==oldRevision&&
                    g.save.energy==spent,
                    "Reselecting current weapon preserves the unreleased full-body action and its damage expense");
                g.Equip(1);
                Check(g.playerAction==null&&g.save.weapon==1&&
                    g.save.energy==spent&&g.save.version==2,
                    "A distinct unlocked weapon cancels old attack and combo without refunding or duplicating stamina or altering saves");
            }
        }

        public static void EnemyVelocityComesFromTrueFootTranslation(Campaign data)
        {
            MapData original=data.maps[1];
            try
            {
                var g=SprintTestWorld(data,1);
                g.save.x=24.5f;g.save.y=15.5f;
                var ranger=new Enemy(501,1,17.5f,15.5f,80f);
                ranger.timer=1f;ranger.state=0;
                g.enemies.Add(ranger);
                Point before=ranger.Position;
                g.Tick(.05f,new Point());
                Point movement=ranger.Position-before;
                Check(movement.Length>.002f&&ranger.moving&&
                    (ranger.moveVelocity-movement*20f).Length<.001f,
                    "Pursuing enemies expose actual physical foot displacement / elapsed time, not intended heading");
                Check(ranger.moveVelocity.x>0f,
                    "A rushing archer's live velocity follows the occupied ground rather than the hero's old input");
                ranger.stun=2f;
                g.Tick(.05f,new Point());
                Check(ranger.moveVelocity.Length<.00001f,
                    "No phantom target leading may remain when a live stun stops motion");
                Check(g.save.version==2&&g.save.worldEnemies==null,
                    "Velocity samples are transient and do not mutate v2 world encounter snapshots");
            }
            finally{data.maps[1]=original;}
        }

        public static void PredictiveRangedLeadRespectsTrueVelocityCoverAndSave(Campaign data)
        {
            MapData previous=data.maps[1];
            try
            {
                for(int profession=0;profession<3;profession++)
                {
                    var g=SprintTestWorld(data,profession);
                    g.save.weapon=1;
                    var moving=new Enemy(1203,0,g.Position.x+7f,g.Position.y,60f);
                    g.enemies.Add(moving);
                    moving.moving=true;moving.moveVelocity=new Point(0,2f);
                    Point visible=moving.Position;
                    Point lead=g.PredictRangedTargetPoint(moving,12f,.24f);
                    Check(lead.y>visible.y+.5f&&lead.y<visible.y+1.71f&&
                        Math.Abs(lead.x-visible.x)<.001f,
                        "Physical moving target receives bounded lateral projectile flight lead for every class");
                    Check(moving.hp==60f&&g.projectiles.Count==0&&
                        g.save.version==2,
                        "Read-only projectile leading never emits contacts or changes an original save");
                    moving.moving=false;
                    Check((g.PredictRangedTargetPoint(moving,12f,.24f)-
                        visible).Length<.00001f,
                        "A stopped target immediately resumes exact ordinary aimed shots");
                    moving.moving=true;moving.moveVelocity=new Point(float.NaN,0);
                    Check((g.PredictRangedTargetPoint(moving,12f,.24f)-
                        visible).Length<.00001f,
                        "Malformed physical motion cannot produce NaN projectiles or ghost targets");
                    moving.moveVelocity=new Point(0,2f);
                    char[] wall=g.Map.rows[(int)visible.y+1].ToCharArray();
                    for(int x=(int)visible.x-1;x<=(int)visible.x+2;x++)wall[x]='#';
                    g.Map.rows[(int)visible.y+1]=new string(wall);
                    Check((g.PredictRangedTargetPoint(moving,12f,.24f)-
                        visible).Length<.00001f,
                        "Lead prediction falls back to the physical current center instead of targeting through a closed wall");
                    wall=g.Map.rows[(int)visible.y+1].ToCharArray();
                    for(int x=(int)visible.x-1;x<=(int)visible.x+2;x++)wall[x]='.';
                    g.Map.rows[(int)visible.y+1]=new string(wall);
                    g.save.weapon=0;
                    Check((g.PredictRangedTargetPoint(moving,12f,.24f)-
                        visible).Length<.00001f,
                        "Sword melee never receives an inappropriate remote ballistic aim lead");
                    g.save.weapon=2;
                    Point magic=g.PredictRangedTargetPoint(moving,8f,.24f);
                    Check(magic.y>visible.y+.5f&&magic.y<=visible.y+1.46f,
                        "Slower actual staff projectile receives its own capped travel lead");
                }
            }
            finally{data.maps[1]=previous;}
        }

        public static void CapturedTargetKeepsBodyAndMuzzleTogether(Campaign data)
        {
            var shotMethod=typeof(GameCore).GetMethod("WeaponShot",BindingFlags.Instance|BindingFlags.NonPublic);
            var g=Fresh(data);g.enemies.Clear();Point feet=g.Position;
            for(int weapon=1;weapon<=2;weapon++)for(int degrees=0;degrees<360;degrees++)foreach(float distance in new[]{2f,6f,12f})
            {
                double angle=degrees*Math.PI/180;Point body=new Point((float)Math.Cos(angle),(float)Math.Sin(angle)),target=feet+body*distance;
                var shot=(Projectile)shotMethod.Invoke(g,new object[]{feet,weapon,body,9f,10f,(Point?)target});
                Point ray=target-shot.position;
                Check((ray.Normalized-shot.velocity.Normalized).Length<.0001f,"Flight direction must reach the target from the exact created muzzle at every row boundary");
                Check(Math.Abs(shot.launchHeight-GameCore.WeaponHeight(weapon,body))<.001f,"Continuous flight correction must not select a second weapon/body height");
                if(!shot.blockedAtSpawn)Check((shot.position-GameCore.WeaponOrigin(feet,weapon,body)).Length<.001f,"A continuous aim vector cannot relocate a four-direction weapon");
                Check(Math.Abs(shot.velocity.Length-9)<.001f,"Target correction must retain projectile speed");
            }
            g.SetAimTarget(feet+new Point(5,-5));g.Attack(new Point(1,-1));var action=g.playerAction;Point stored=action.target.Value;
            g.SetAimTarget(feet+new Point(-5,5));g.SetAim(new Point(-1,1));
            Check((action.target.Value-stored).Length==0&&action.direction.x>0&&action.direction.y<0,"Mouse changes during preparation cannot swap the action's muzzle or target");
            g.Enter(0);Check(!g.aimTarget.HasValue,"Travel cannot carry a previous room's target into a new action");
            g.SetAimTarget(new Point(float.NaN,0));Check(!g.aimTarget.HasValue,"Invalid target coordinates cannot reach projectile geometry");
        }
    }
}
