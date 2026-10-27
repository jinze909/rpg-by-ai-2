using System;
using System.Collections.Generic;

namespace Starfall
{
    // One immutable action identity follows direct, flying and persistent hits.
    // This is simulation-only state; the persisted SaveData schema is unchanged.
    public sealed class ImpactProfile
    {
        public readonly int weapon, style, masteryWeapon, combo;
        // Immutable source profession for VFX ownership after an in-flight
        // weapon/profession change. Not a SaveData field or a new damage rule.
        public readonly int profession;
        public readonly float criticalChance;
        public readonly bool playerOwned;
        // The immutable actual projectile/attack launch foot. A player
        // can run behind cover before the shot arrives; a delayed hit must
        // not publish that new hidden position as the shot's source.
        public readonly Point? sourceFoot;
        public ImpactProfile(int weapon, int style, int masteryWeapon, int combo,
            float criticalChance, bool playerOwned, int profession=-1,
            Point? sourceFoot=null)
        {
            this.weapon=weapon;this.style=style;
            this.masteryWeapon=masteryWeapon;this.combo=combo;
            this.criticalChance=criticalChance;this.playerOwned=playerOwned;
            this.profession=profession;
            this.sourceFoot=sourceFoot;
        }
    }
    internal sealed class FrostField
    {
        public const int PulseCount=5;
        public const float Duration=4, Interval=Duration/PulseCount, Radius=4;
        public readonly Point center;
        public readonly float damage;
        public readonly ImpactProfile impact;
        public float age;
        public int pulses;
        // Exact transient visual ownership, not a save field. Two casters
        // can create indistinguishable "frost" effects simultaneously.
        public readonly List<Effect> visuals = new List<Effect>(PulseCount);
        public FrostField(Point center,float damage,ImpactProfile impact)
        {this.center=center;this.damage=damage;this.impact=impact;}
    }
    public sealed partial class GameCore
    {
        private FrostField frost, companionFrost;
        // Only a real player-owned, nonlethal contact beyond ordinary
        // local aggro grants a bounded pursuit response. The physical
        // Enemy reference owns the alarm, never its reuseable numeric ID.
        // This is a runtime reaction: no SaveData or historical aggro.
        private sealed class EnemyHitAlert
        {
            public float remaining;
            // Last PHYSICALLY observed player location, not the hero's
            // hidden live coordinate beyond new walls or doors.
            public Point lastKnown;
            public EnemyHitAlert(float time,Point foot)
            {remaining=time;lastKnown=foot;}
        }
        private readonly Dictionary<Enemy,EnemyHitAlert> enemyHitAlert =
            new Dictionary<Enemy,EnemyHitAlert>();
        private SaveData enemyHitAlertSave;
        private MapData enemyHitAlertMap;
        private const float EnemyHitAlertSeconds=6f;
        private void EnsureEnemyHitAlertSession()
        {
            if(!System.Object.ReferenceEquals(enemyHitAlertSave,save)||
               !System.Object.ReferenceEquals(enemyHitAlertMap,Map))
            {
                enemyHitAlert.Clear();
                enemyHitAlertSave=save;
                enemyHitAlertMap=Map;
            }
        }
        private void ResetEnemyHitAlert()
        {
            enemyHitAlert.Clear();
            enemyHitAlertSave=null;
            enemyHitAlertMap=null;
        }
        private void NotifyEnemyOfActualDistantPlayerHit(Enemy e,ImpactProfile impact)
        {
            if(e==null||impact==null||!impact.playerOwned||
               e.boss||practicing||mode!="world"||
               !enemies.Contains(e))return;
            Point launchFoot=impact.sourceFoot.HasValue?
                impact.sourceFoot.Value:Position;
            float range=(e.Position-launchFoot).Length;
            if(!Finite(range)||range<=8f||range>18f)return;
            EnsureEnemyHitAlertSession();
            // Lethal sniping still produces a real witnessed strike:
            // the original enemy dies and cannot pursue, but companions
            // who saw the fatal contact may react normally.
            if(e.hp>0)
            {
                if(!enemyHitAlert.ContainsKey(e))
                    effects.Add(new Effect("number",e.Position,.7f,
                        L("察觉袭击","UNDER ATTACK")));
                enemyHitAlert[e]=new EnemyHitAlert(
                    EnemyHitAlertSeconds,launchFoot);
            }
            // Nearby living patrol mates can see a comrade get struck.
            // This is not a magical room-wide alarm: only neighbors
            // within three and a half physical tiles with a genuinely
            // clear line to the original hit acquire a shorter response.
            // Matching IDs, distant actors, solid-walled rooms and Bosses
            // cannot borrow the attacked body's alarm.
            foreach(Enemy witness in enemies)
            {
                if(witness==null||System.Object.ReferenceEquals(witness,e)||
                   witness.hp<=0||witness.boss)continue;
                float between=(witness.Position-e.Position).Length;
                float originDistance=(witness.Position-launchFoot).Length;
                if(!Finite(between)||between>3.5f||
                   !Finite(originDistance)||originDistance>18f||
                   originDistance<=8f||
                   !CanStand(witness.Position)||
                   !ClearAttackPath(e.Position,witness.Position))continue;
                if(!enemyHitAlert.ContainsKey(witness))
                {
                    effects.Add(new Effect("number",witness.Position,.7f,
                        L("同伴示警","PATROL ALERT")));
                }
                EnemyHitAlert oldWitness;
                if(enemyHitAlert.TryGetValue(witness,out oldWitness))
                {
                    oldWitness.remaining=Math.Max(oldWitness.remaining,4f);
                    if(ClearAttackPath(witness.Position,Position))
                        oldWitness.lastKnown=Position;
                    else if(ClearAttackPath(witness.Position,launchFoot))
                        oldWitness.lastKnown=launchFoot;
                }
                else
                    enemyHitAlert[witness]=new EnemyHitAlert(4f,
                        ClearAttackPath(witness.Position,Position)?
                        Position:
                        ClearAttackPath(witness.Position,launchFoot)?
                        launchFoot:e.Position);
            }
        }
        private bool TickEnemyHitAlert(Enemy e,float dt)
        {
            EnsureEnemyHitAlertSession();
            EnemyHitAlert alert;
            if(e==null||!enemyHitAlert.TryGetValue(e,out alert))
                return false;
            alert.remaining-=Math.Max(0f,dt);
            float range=(e.Position-Position).Length;
            if(!enemies.Contains(e)||e.hp<=0||e.boss||
               !Finite(range)||range>18f||alert.remaining<=0f)
            {
                enemyHitAlert.Remove(e);
                return false;
            }
            if(ClearAttackPath(e.Position,Position))
                alert.lastKnown=Position;
            return true;
        }
        private Point LastKnownEnemyHitOrigin(Enemy e,Point fallback)
        {
            EnemyHitAlert state;
            return e!=null&&enemyHitAlert.TryGetValue(e,out state)?
                state.lastKnown:fallback;
        }
        private ImpactProfile CaptureImpact(int weapon,int attackCombo=0,bool playerOwned=true)
        {
            weapon=(int)Clamp(weapon,0,2);
            return new ImpactProfile(weapon,save.weaponStyles[weapon],save.weapon,attackCombo,
                Clamp(.06f+save.talents[0]*.025f+(Relic(5)?.12f:0),0,1),
                playerOwned,playerOwned?save.profession:-1,
                playerOwned?(Point?)Position:null);
        }
        private void RetireFrostVisuals(FrostField field)
        {
            if(field==null)return;
            // Remove by the actual effect instances, never by kind, position
            // or radius: an independent caster's identical ring stays alive.
            foreach(Effect visual in field.visuals)effects.Remove(visual);
            field.visuals.Clear();
        }
        private void BeginFrost(Point center,float baseDamage,ImpactProfile impact)
        {
            RetireFrostVisuals(frost);
            frost=new FrostField(center,baseDamage*.4f,impact);
        }
        private void BeginCompanionFrost(Point center,float baseDamage,ImpactProfile impact)
        {
            RetireFrostVisuals(companionFrost);
            companionFrost=new FrostField(center,baseDamage*.4f,impact);
        }
        private void ClearFrost()
        {
            RetireFrostVisuals(frost);
            RetireFrostVisuals(companionFrost);
            frost=companionFrost=null;
        }
        private bool IsCurrentFrost(FrostField field)
        {return frost==field||companionFrost==field;}
        private void TickFrost(float dt)
        {
            // Each living caster owns its own physical effect: Sen's command
            // must never erase a still-active Arcanist Q, or vice versa.
            TickOneFrost(frost,dt);
            TickOneFrost(companionFrost,dt);
        }
        private void TickOneFrost(FrostField field,float dt)
        {
            if(field==null||!IsCurrentFrost(field)||Dying||mode!="world")return;
            // The spell is rooted to actual world ground, NOT a remotely
            // ticking global timer. Dynamic gates, rising tide or collapsed
            // terrain can seal its center AFTER an otherwise valid cast.
            // Stop only that caster's now-buried source; the other living
            // field must continue its own pulse clock and combat ownership.
            if(!CanStand(field.center))
            {
                RetireFrostVisuals(field);
                if(frost==field)frost=null;
                if(companionFrost==field)companionFrost=null;
                return;
            }
            field.age=Math.Min(FrostField.Duration,field.age+dt);
            // Schedule five contacts at 0, .8, 1.6, 2.4 and 3.2. Neither
            // accumulated float residue nor a long final frame creates a sixth.
            while(field.pulses<FrostField.PulseCount&&field.pulses*FrostField.Interval<=field.age+.00001f)
            {
                field.pulses++;
                Effect pulseVisual=new Effect("frost",field.center,Math.Min(FrostField.Interval,Math.Max(.001f,FrostField.Duration-field.age))){radius=FrostField.Radius};
                effects.Add(pulseVisual);
                field.visuals.Add(pulseVisual);
                foreach(Enemy e in enemies.ToArray())
                {
                    if(!IsCurrentFrost(field)||Dying||mode!="world")return;
                    if((e.Position-field.center).Length<FrostField.Radius&&ClearAttackPath(field.center,e.Position))
                    {
                        // A fresh chill pulse can EXTEND control but must not
                        // CANCEL a longer earned Break / companion stun.
                        // Keep the original five damage pulses and timing.
                        ApplyEnemyControl(e,.45f);
                        Hit(e,field.damage,true,
                            (e.Position-field.center).Normalized,field.impact);
                    }
                }
            }
            if(field.age>=FrostField.Duration)
            {
                if(frost==field)frost=null;
                if(companionFrost==field)companionFrost=null;
            }
        }
    }
}
