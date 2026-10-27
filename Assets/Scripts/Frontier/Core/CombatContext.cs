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
            // A meaningful sound-only investigation owns a distinct,
            // readable alert; actual visual acquisition supersedes it.
            public bool impactOnly;
            // A live patrol first approaches the actual wall impact, then
            // checks distinct near-side floor positions instead of freezing
            // at one point and staring into the wall. Search is per Enemy
            // instance, ephemeral, and never written into v2 saves.
            public int searchStep=-1;
            public Point searchGoal,homeFoot;
            public bool returning;
            public EnemyHitAlert(float time,Point foot,bool impactOnly=false,
                Point? home=null)
            {
                remaining=time;lastKnown=foot;this.impactOnly=impactOnly;
                homeFoot=home.HasValue?home.Value:foot;
            }
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
        // A guard following an unexpected stone impact has a real field
        // of view rather than 360-degree supernatural discovery. Front
        // vision and a close physical bump expose the hero; a rear approach
        // can remain unseen until a committed sword contact. This applies
        // ONLY to sound-only search, never ordinary combat or mission AI.
        private bool ImpactSearchGuardSeesHero(Enemy enemy)
        {
            if(enemy==null||!CanStand(enemy.Position)||
               !CanStand(Position)||
               !ClearAttackPath(enemy.Position,Position))return false;
            Point toward=Position-enemy.Position;
            float distance=toward.Length;
            if(!Finite(distance)||distance>7f)return false;
            if(distance<.8f)return true;
            if(distance<.001f)return true;
            Point facing=enemy.facing.Length>.001f?
                enemy.facing.Normalized:new Point(1,0);
            Point sight=toward*(1f/distance);
            return facing.x*sight.x+facing.y*sight.y>=.12f;
        }
        private bool SoundAmbushGeometry(Enemy enemy,float distance,
            float facingDot)
        {
            return !practicing&&mode=="world"&&
                !Dying&&enemy!=null&&enemy.hp>0&&!enemy.boss&&
                enemy.state!=1&&
                distance>.8f&&distance<2.2f&&facingDot<-.65f&&
                IsEnemyInvestigatingImpact(enemy)&&
                !ImpactSearchGuardSeesHero(enemy)&&
                CanStand(Position)&&CanStand(enemy.Position)&&
                ClearAttackPath(Position,enemy.Position);
        }
        private bool EligibleForSoundAmbush(Enemy enemy,ImpactProfile impact,
            bool technique,float distance,float facingDot)
        {
            return impact!=null&&impact.playerOwned&&
                impact.weapon==0&&!technique&&
                SoundAmbushGeometry(enemy,distance,facingDot);
        }
        // Exact, read-only gameplay affordance at the actual sprite feet.
        // Do not promise a backstab if the player faces away, has the bow
        // equipped, is outside real sword reach or is looking at a Boss.
        public bool CanSoundAmbushNow(Enemy enemy)
        {
            if(save==null||save.weapon!=0||enemy==null)return false;
            Point direction=enemy.Position-Position;
            float distance=direction.Length;
            if(!Finite(distance)||distance<=.001f)return false;
            Point blade=aim.Length>.001f?aim.Normalized:new Point(1,0);
            float aimDot=(direction.x*blade.x+direction.y*blade.y)/distance;
            Point approach=direction*(-1f/distance);
            float facingDot=approach.x*enemy.facing.x+
                approach.y*enemy.facing.y;
            // Preview the NEXT actual sword swing's authored collision
            // radius. A basic slash stops at 1.85 tiles; only the earned
            // third combo finisher reaches 2.2. Never show a green prompt
            // at a distance where pressing attack would whiff.
            int nextCombo=comboTimer>0f?(combo+1)%3:0;
            float actualReach=nextCombo==2?2.2f:1.85f;
            return distance<actualReach&&aimDot>-.15f&&
                SoundAmbushGeometry(enemy,distance,facingDot);
        }
        // A guard who ACTUALLY identifies a hidden archer can call the
        // nearby watch. This is a one-shot world event, not a room-global
        // alert: only present living ordinary teammates who hear the
        // original body through open terrain can respond. Their search
        // remembers the caller's LAST VISIBLE hero foot, never magically
        // tracks an unseen hero after the shout.
        private void NotifyPatrolOfRealSighting(Enemy caller)
        {
            if(caller==null||caller.hp<=0||!enemies.Contains(caller)||
               caller.boss||ArrowSoundCannotOverrideObjective(caller)||
               practicing||Dying||mode!="world")return;
            Point seen=Position;
            bool heard=false;
            foreach(Enemy witness in enemies)
            {
                if(witness==null||Object.ReferenceEquals(witness,caller)||
                   witness.hp<=0||witness.boss||witness.state==1||
                   ArrowSoundCannotOverrideObjective(witness)||
                   !CanStand(witness.Position))continue;
                float voice=(witness.Position-caller.Position).Length;
                float source=(witness.Position-seen).Length;
                if(!Finite(voice)||voice>5.5f||
                   !Finite(source)||source<=8f||source>18f||
                   !ClearAttackPath(caller.Position,witness.Position))
                    continue;
                EnemyHitAlert existing;
                if(enemyHitAlert.TryGetValue(witness,out existing))
                {
                    // Do not overwrite active weapon contact. A guard
                    // merely listening to stone, or heading home after a
                    // decoy, may now follow an actual teammate sighting.
                    if(!existing.impactOnly&&!existing.returning)continue;
                }
                enemyHitAlert[witness]=new EnemyHitAlert(5f,seen);
                effects.Add(new Effect("number",witness.Position,.85f,
                    L("同伴呼警","HEARD SENTRY")));
                heard=true;
            }
            effects.Add(new Effect("number",caller.Position,.85f,
                L("发现行踪！","PLAYER SPOTTED")));
            // The player hears a single whistle at the EXACT sighting,
            // even if this sentry has no available teammates.
            sound="sentryAlarm";
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
               !Finite(range)||range>18f)
            {
                enemyHitAlert.Remove(e);
                return false;
            }
            // A successful harmless distraction is not a permanent removal
            // of a sentry from its post. If still hidden, send it back over
            // the SAME physical AI route. The earlier patrol position is
            // preserved across redirected sounds, but never into SaveData.
            if(alert.returning)
            {
                if(ArrowSoundCannotOverrideObjective(e)||
                   ClearAttackPath(e.Position,Position)||
                   range<=8f||!CanStand(alert.homeFoot)||
                   (e.Position-alert.homeFoot).Length<.42f||
                   alert.remaining<=0f)
                {
                    enemyHitAlert.Remove(e);
                    return false;
                }
                return true;
            }
            if(alert.remaining<=0f)
            {
                if(alert.impactOnly&&CanStand(alert.homeFoot)&&
                   (e.Position-alert.homeFoot).Length>.5f&&
                   !ArrowSoundCannotOverrideObjective(e))
                {
                    alert.returning=true;
                    alert.impactOnly=false;
                    alert.lastKnown=alert.homeFoot;
                    alert.searchStep=-1;
                    alert.remaining=7f;
                    effects.Add(new Effect("number",e.Position,.7f,
                        L("返回哨位","BACK TO POST")));
                    return true;
                }
                enemyHitAlert.Remove(e);
                return false;
            }
            // An actor that changes to a protected objective while a
            // sound search is active must stop advertising INVESTIGATING.
            // The real civilian/ward/stock intention now owns that body.
            if(alert.impactOnly&&ArrowSoundCannotOverrideObjective(e))
            {
                enemyHitAlert.Remove(e);
                return false;
            }
            if(alert.impactOnly)
            {
                // Face-to-face sighting, not mere proximity, ends the
                // sound search. The existing 8-tile awareness still governs
                // ALL normal combat, mission raiders and genuine hit alarms.
                if(ImpactSearchGuardSeesHero(e))
                {
                    alert.lastKnown=Position;
                    alert.impactOnly=false;
                    NotifyPatrolOfRealSighting(e);
                }
            }
            else if(ClearAttackPath(e.Position,Position))
                alert.lastKnown=Position;
            return true;
        }
        // A missed physical arrow still makes a tactical SOUND where
        // it actually strikes solid cover. Nearby ordinary patrols can
        // investigate that real same-side impact, never the unreachable
        // concealed shooter, and only when the original player fired it.
        // A shot blocked at birth, a companion arrow, a shot expiring in air
        // or a confirmed actor hit cannot double-mint this distraction.
        // Civilian, ward and supply attacks own a real priority objective.
        // Noise cannot repaint those attackers as fake search patrols.
        private bool ArrowSoundCannotOverrideObjective(Enemy enemy)
        {
            return (roadTrialRoom==save.room&&IsRoadwardenTrialEnemy(enemy))||
                (wayfarerEncounterRoom==save.room&&IsWayfarerEnemy(enemy))||
                (reliefMissionRoom==save.room&&
                    ((reliefMissionWave==1||reliefMissionWave==2)&&
                        IsReliefMissionEnemy(enemy)||
                     reliefMissionWave==3&&
                        (enemy.Position-reliefFollowerPosition).Length<8f))||
                (IsAfterlightWatchEnemy(enemy)&&enemy.kind!=1)||
                (IsOuterPatrolEnemy(enemy)&&enemy.kind!=1);
        }
        private void InvestigateRealArrowWallImpact(Projectile shot)
        {
            if(shot==null||!shot.friendly||shot.explosive||
               shot.visual!="arrow"||
               shot.blockedAtSpawn||shot.hitActors.Count>0||
               shot.impact==null||!shot.impact.playerOwned||
               !shot.impact.sourceFoot.HasValue||
               practicing||Dying||mode!="world"||
               !Finite(shot.position.x)||!Finite(shot.position.y))
                return;
            Point soundFoot=shot.position;
            Point origin=shot.impact.sourceFoot.Value;
            if(!Finite(origin.x)||!Finite(origin.y)||!Finite(shot.damage)||
               shot.damage<=0f)return;
            // Actual colliding stone is audible to the player even if no
            // living enemy is close enough to respond. Never play it for
            // an expired bolt, a companion shot, magic or a spawn-blocked
            // projectile (all rejected at the source eligibility gate).
            sound="stoneImpact";
            EnsureEnemyHitAlertSession();
            foreach(Enemy enemy in enemies)
            {
                if(enemy==null||enemy.hp<=0||enemy.boss||
                   !CanStand(enemy.Position)||
                   ArrowSoundCannotOverrideObjective(enemy))continue;
                float heard=(enemy.Position-soundFoot).Length;
                float fromHero=(enemy.Position-Position).Length;
                if(!Finite(heard)||heard>4.5f||
                   !Finite(fromHero)||fromHero<=8f||fromHero>18f||
                   // An opaque wall blocks direct impact noise: don't
                   // alarm a soldier sheltered on its far side.
                   !ClearAttackPath(soundFoot,enemy.Position))continue;
                EnemyHitAlert alert;
                if(enemyHitAlert.TryGetValue(enemy,out alert))
                {
                    // A returning sentry may react to a new physical noise
                    // without forgetting its original posted foot. Ordinary
                    // direct combat, in contrast, still outranks distractions.
                    if(alert.returning)
                    {
                        alert.returning=false;
                        alert.impactOnly=true;
                        alert.lastKnown=soundFoot;
                        alert.remaining=3.5f;
                        alert.searchStep=-1;
                        effects.Add(new Effect("number",enemy.Position,.75f,
                            L("又有异响","NEW IMPACT")));
                        continue;
                    }
                    // Real visual/weapon contact still outranks a noise.
                    // A distinct SECOND physical sound may redirect a
                    // searching guard, but repeated fire into the same
                    // stone does not endlessly refresh the alert timer.
                    if(!alert.impactOnly||
                        (alert.lastKnown-soundFoot).Length<1.25f)
                        continue;
                    alert.lastKnown=soundFoot;
                    alert.remaining=3.5f;
                    alert.searchStep=-1;
                    effects.Add(new Effect("number",enemy.Position,.75f,
                        L("新的撞击声","NEW IMPACT")));
                    continue;
                }
                enemyHitAlert[enemy]=new EnemyHitAlert(
                    3.5f,soundFoot,true,enemy.Position);
                effects.Add(new Effect("number",enemy.Position,.75f,
                    L("听见异响","HEARD IMPACT")));
            }
        }
        // The original actor renderer uses this read-only flag to
        // distinguish a real sound investigation from regular combat.
        // Identity, save, current map and finite expiry are all required.
        public bool IsEnemyInvestigatingImpact(Enemy e)
        {
            EnemyHitAlert alert;
            return e!=null&&save!=null&&Map!=null&&
                System.Object.ReferenceEquals(enemyHitAlertSave,save)&&
                System.Object.ReferenceEquals(enemyHitAlertMap,Map)&&
                enemies.Contains(e)&&e.hp>0&&!e.boss&&
                !ArrowSoundCannotOverrideObjective(e)&&
                enemyHitAlert.TryGetValue(e,out alert)&&
                alert.impactOnly&&alert.remaining>0f;
        }
        // Turn a sound investigation into a short, physically navigated
        // near-side sweep. The same map collision and movement sampler used
        // by real enemy pursuit handles each chosen foot. A wall, new sighting
        // or higher-priority civilian duty cannot create a hidden teleporter.
        private Point LiveSoundSearchGoal(Enemy enemy,Point fallback)
        {
            EnemyHitAlert alert;
            if(enemy==null||!enemyHitAlert.TryGetValue(enemy,out alert)||
               !alert.impactOnly)return fallback;
            if(alert.searchStep<0)
            {
                if((enemy.Position-alert.lastKnown).Length>1.3f)
                    return alert.lastKnown;
                alert.searchStep=0;
            }
            else if((enemy.Position-alert.searchGoal).Length<.32f)
                alert.searchStep++;
            // Deliberately sweep the four quadrants, but reject any
            // unreachable, solid or sealed corner in the CURRENT world.
            // No static actor IDs, random teleport or old path cache.
            for(;alert.searchStep<4;alert.searchStep++)
            {
                Point offset;
                switch(alert.searchStep)
                {
                    case 0:offset=new Point(-1.35f,.95f);break;
                    case 1:offset=new Point(-1.35f,-.95f);break;
                    case 2:offset=new Point(1.35f,.95f);break;
                    default:offset=new Point(1.35f,-.95f);break;
                }
                Point target=alert.lastKnown+offset;
                if(!CanStand(target)||
                   !ClearAttackPath(enemy.Position,target)||
                   (target-alert.lastKnown).Length>2.1f)continue;
                alert.searchGoal=target;
                return target;
            }
            // The local area has been checked. Guard this exact spot until
            // expiry or a truly different impact, rather than jittering into
            // an unreachable wall or magically reacquiring the hidden hero.
            return enemy.Position;
        }
        public bool IsEnemyReturningFromImpact(Enemy enemy)
        {
            EnemyHitAlert alert;
            return enemy!=null&&save!=null&&Map!=null&&
                System.Object.ReferenceEquals(enemyHitAlertSave,save)&&
                System.Object.ReferenceEquals(enemyHitAlertMap,Map)&&
                enemies.Contains(enemy)&&enemy.hp>0&&!enemy.boss&&
                !ArrowSoundCannotOverrideObjective(enemy)&&
                enemyHitAlert.TryGetValue(enemy,out alert)&&
                alert.returning&&alert.remaining>0f;
        }
        public bool IsEnemySweepingImpact(Enemy enemy)
        {
            EnemyHitAlert alert;
            return IsEnemyInvestigatingImpact(enemy)&&
                enemyHitAlert.TryGetValue(enemy,out alert)&&
                alert.searchStep>=0;
        }
        private Point LastKnownEnemyHitOrigin(Enemy e,Point fallback)
        {
            EnemyHitAlert state;
            return e!=null&&enemyHitAlert.TryGetValue(e,out state)?
                state.lastKnown:fallback;
        }
        // A snapshot's critical/stats/class identity belongs to input
        // time. A released projectile's physical SOURCE, however, is the
        // actor's planted foot at the actual hand-release frame. Rebase
        // only that immutable field, once per release, never each
        // projectile sweep or after the hero has already dodged away.
        private static ImpactProfile AtActualProjectileRelease(
            ImpactProfile original,Point plantedFoot)
        {
            if(original==null||!original.playerOwned)return original;
            if(original.sourceFoot.HasValue&&
               (original.sourceFoot.Value-plantedFoot).Length<.0001f)
                return original;
            return new ImpactProfile(original.weapon,original.style,
                original.masteryWeapon,original.combo,original.criticalChance,
                original.playerOwned,original.profession,plantedFoot);
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
