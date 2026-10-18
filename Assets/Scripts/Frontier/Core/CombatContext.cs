using System;
using System.Collections.Generic;

namespace Starfall
{
    // One immutable action identity follows direct, flying and persistent hits.
    // This is simulation-only state; the persisted SaveData schema is unchanged.
    public sealed class ImpactProfile
    {
        public readonly int weapon, style, masteryWeapon, combo;
        public readonly float criticalChance;
        public readonly bool playerOwned;
        public ImpactProfile(int weapon, int style, int masteryWeapon, int combo, float criticalChance, bool playerOwned)
        { this.weapon=weapon;this.style=style;this.masteryWeapon=masteryWeapon;this.combo=combo;this.criticalChance=criticalChance;this.playerOwned=playerOwned; }
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
        private ImpactProfile CaptureImpact(int weapon,int attackCombo=0,bool playerOwned=true)
        {
            weapon=(int)Clamp(weapon,0,2);
            return new ImpactProfile(weapon,save.weaponStyles[weapon],save.weapon,attackCombo,
                Clamp(.06f+save.talents[0]*.025f+(Relic(5)?.12f:0),0,1),playerOwned);
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
