using System;
using System.Collections.Generic;

namespace Starfall
{
    // Persistent gameplay upgrades may use existing v2 flags, but deployed
    // traps themselves are strictly live world actors, never serialized.
    public sealed class FieldSnare
    {
        public readonly Point foot;
        public float seconds,arming;
        public FieldSnare(Point p)
        { foot=p;seconds=25f;arming=.45f; }
    }
    public sealed partial class GameCore
    {
        private readonly List<FieldSnare> fieldSnares=new List<FieldSnare>(3);
        private readonly Dictionary<Enemy,Point> fieldSnarePreviousFeet=
            new Dictionary<Enemy,Point>();
        private int fieldSnareRoom=-1;
        private MapData fieldSnareMap;
        public IList<FieldSnare> FieldSnares
        {
            get
            {
                if(fieldSnareRoom!=save.room||
                    !Object.ReferenceEquals(fieldSnareMap,Map))
                    return new FieldSnare[0];
                return fieldSnares.AsReadOnly();
            }
        }
        public int FieldSnareLimit {get{return 2;}}
        public bool CanLayFieldSnare
        {
            get
            {
                return mode=="world"&&!Dying&&!practicing&&!racing&&
                    !ActionTransitionBlocked&&CanStand(Position)&&
                    fieldSnares.Count<FieldSnareLimit&&
                    save.inventory[0]>=1&&save.inventory[4]>=1;
            }
        }
        private void ResetFieldSnares()
        {
            fieldSnares.Clear();fieldSnarePreviousFeet.Clear();
            fieldSnareRoom=-1;fieldSnareMap=null;
        }
        private void ValidateFieldSnareWorld()
        {
            if(fieldSnareRoom==save.room&&
                Object.ReferenceEquals(fieldSnareMap,Map))return;
            ResetFieldSnares();
            fieldSnareRoom=save.room;fieldSnareMap=Map;
        }
        public bool LayFieldSnare(Point direction)
        {
            if(mode!="world"||Dying||practicing||racing||
                ActionTransitionBlocked||!CanStand(Position))return false;
            ValidateFieldSnareWorld();
            if(fieldSnares.Count>=FieldSnareLimit)
            {
                Notify("已布置两道绊索；等待敌人触发或自然失效。",
                    "Two snares are set. Wait for contact or expiry.");
                return false;
            }
            if(!Finite(direction.x)||!Finite(direction.y)||
               direction.Length<.05f)direction=aim;
            if(!Finite(direction.x)||!Finite(direction.y)||
               direction.Length<.05f)return false;
            Point foot=Position+direction.Normalized*1.35f;
            if(!CanStand(foot)||!ClearAttackPath(Position,foot))
            {
                Notify("绊索必须布设在无遮挡、可以站立的真实地面。",
                    "Lay a snare on unobstructed, walkable ground.");
                return false;
            }
            foreach(FieldSnare existing in fieldSnares)
                if((existing.foot-foot).Length<1.1f)
                {
                    Notify("陷阱间距不足，请选择另一段通路。",
                        "Leave space between nearby snares.");
                    return false;
                }
            // Never spend supplies before the full terrain and distance
            // checks, and never spawn a trap on top of a living body.
            foreach(Enemy foe in enemies)
                if(foe!=null&&foe.hp>0f&&
                    (foe.Position-foot).Length<.65f)return false;
            if(save.companion>=0&&
                (companionPosition-foot).Length<.65f)return false;
            if(!Spend(1,0,0,0,1))return false;
            fieldSnares.Add(new FieldSnare(foot));
            effects.Add(new Effect("number",foot,.75f,
                L("绊索已布置","SNARE SET")));
            sound="quest";revision++;
            return true;
        }
        private void CaptureFieldSnareEnemyFooting()
        {
            ValidateFieldSnareWorld();
            fieldSnarePreviousFeet.Clear();
            if(fieldSnares.Count==0)return;
            foreach(Enemy e in enemies)
                if(e!=null&&e.hp>0f)
                    fieldSnarePreviousFeet[e]=e.Position;
        }
        // Exact closest point along the true enemy stride prevents fast
        // movers from tunneling across the narrow triggered footprint.
        private bool FieldSnareCrossed(FieldSnare snare,Enemy e)
        {
            if(e==null||e.hp<=0f||e.boss||
               !enemies.Contains(e)||ArrowSoundCannotOverrideObjective(e)||
               !CanStand(e.Position)||!CanStand(snare.foot))return false;
            Point start;
            if(!fieldSnarePreviousFeet.TryGetValue(e,out start)||
               !Finite(start.x)||!Finite(start.y))start=e.Position;
            Point end=e.Position,delta=end-start;
            float squared=delta.x*delta.x+delta.y*delta.y;
            float fraction=squared>.00000001f?
                Clamp(((snare.foot.x-start.x)*delta.x+
                       (snare.foot.y-start.y)*delta.y)/squared,0f,1f):0f;
            Point nearest=start+delta*fraction;
            return (nearest-snare.foot).Length<=.55f&&
                ClearAttackPath(snare.foot,nearest);
        }
        private void TickFieldSnares(float dt)
        {
            ValidateFieldSnareWorld();
            for(int i=fieldSnares.Count-1;i>=0;i--)
            {
                FieldSnare snare=fieldSnares[i];
                snare.seconds-=dt;
                snare.arming=Math.Max(0f,snare.arming-dt);
                if(snare.seconds<=0f){fieldSnares.RemoveAt(i);continue;}
                if(snare.arming>0f)continue;
                Enemy triggered=null;
                foreach(Enemy e in enemies)
                    if(FieldSnareCrossed(snare,e)){triggered=e;break;}
                if(triggered==null)continue;
                fieldSnares.RemoveAt(i);
                // A grounded, non-lethal contact interrupts an actual
                // enemy. It creates NO boss credit, damage, loot, or kill.
                triggered.stun=Math.Max(triggered.stun,1.5f);
                triggered.armorOpenTime=Math.Max(triggered.armorOpenTime,.7f);
                triggered.state=0;triggered.timer=Math.Max(triggered.timer,.35f);
                effects.Add(new Effect("burst",snare.foot,.36f)
                    {radius=.8f,variant=0});
                effects.Add(new Effect("number",triggered.Position,.8f,
                    L("绊索束缚！","SNARED!")));
                sound="counter";revision++;
            }
        }
    }
}
