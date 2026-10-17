using System;

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
        public FrostField(Point center,float damage,ImpactProfile impact)
        {this.center=center;this.damage=damage;this.impact=impact;}
    }
    public sealed partial class GameCore
    {
        private FrostField frost;
        private ImpactProfile CaptureImpact(int weapon,int attackCombo=0,bool playerOwned=true)
        {
            weapon=(int)Clamp(weapon,0,2);
            return new ImpactProfile(weapon,save.weaponStyles[weapon],save.weapon,attackCombo,
                Clamp(.06f+save.talents[0]*.025f+(Relic(5)?.12f:0),0,1),playerOwned);
        }
        private void BeginFrost(Point center,float baseDamage,ImpactProfile impact)
        {frost=new FrostField(center,baseDamage*.4f,impact);}
        private void ClearFrost(){frost=null;}
        private void TickFrost(float dt)
        {
            FrostField field=frost;if(field==null)return;
            field.age=Math.Min(FrostField.Duration,field.age+dt);
            // Schedule five contacts at 0, .8, 1.6, 2.4 and 3.2. Neither
            // accumulated float residue nor a long final frame creates a sixth.
            while(field.pulses<FrostField.PulseCount&&field.pulses*FrostField.Interval<=field.age+.00001f)
            {
                field.pulses++;
                effects.Add(new Effect("frost",field.center,Math.Min(FrostField.Interval,Math.Max(.001f,FrostField.Duration-field.age))){radius=FrostField.Radius});
                foreach(Enemy e in enemies.ToArray())
                {
                    if(frost!=field||Dying||mode!="world")return;
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
            if(frost==field&&field.age>=FrostField.Duration)ClearFrost();
        }
    }
}
