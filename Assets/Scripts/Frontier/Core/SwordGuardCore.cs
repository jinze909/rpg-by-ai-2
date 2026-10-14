using System;
namespace Starfall
{
    // A real defensive interaction for the original sword-bearing hero.
    // The projectile changes teams in the actual swept flight pipeline.
    // No SaveData fields, companion identity, or authored enemy waves change.
    public sealed partial class GameCore
    {
        public float deflectCooldown { get; private set; }
        public float deflectFlash { get; private set; }
        public bool SwordGuardReady
        {
            get { return mode=="world"&&!Dying&&!practicing&&
                save.profession==0&&save.weapon==0&&guardTime>0&&
                deflectCooldown<=0&&save.energy>=7&&invulnerable<=0; }
        }
        private void ResetSwordGuard()
        { deflectCooldown=deflectFlash=0; }
        private void TickSwordGuard(float dt)
        {
            deflectCooldown=Math.Max(0,deflectCooldown-dt);
            deflectFlash=Math.Max(0,deflectFlash-dt);
        }
        private bool TryDeflectHostileProjectile(Projectile p)
        {
            if(!SwordGuardReady||p==null||p.friendly||p.explosive||
                p.blockedAtSpawn||!Finite(p.damage)||p.damage<=0||
                !Finite(p.velocity.x)||!Finite(p.velocity.y)||
                p.velocity.Length<.5f)return false;
            Point fromEnemy=p.velocity.Normalized*-1;
            // The current sword guard faces the attack; grazing shots from
            // the back or side must still be able to hit normally.
            Point forward=aim.Normalized;
            if(forward.Length<.1f||forward.x*fromEnemy.x+
                forward.y*fromEnemy.y<.55f)return false;
            save.energy-=7;
            deflectCooldown=.28f;deflectFlash=.34f;
            p.friendly=true;
            p.velocity=fromEnemy*Math.Max(7f,p.velocity.Length);
            p.damage=Math.Max(14f,Math.Min(68f,Damage*.85f));
            p.life=Math.Min(1.6f,p.life);
            p.piercing=false;p.explosive=false;p.slowing=false;
            p.technique=true;p.source="deflected";
            // Do not turn an arcane orb into an arrow in the renderer.
            // The existing friendly starfire frame depicts reflected magic.
            p.visual=p.visual=="enemyOrb"?"starfire":"arrow";
            p.hits.Clear();
            effects.Add(new Effect("number",Position,.7f,
                L("剑盾弹反","GUARD REFLECT")));
            effects.Add(new Effect("burst",Position,.27f)
                {radius=.85f,variant=0,direction=fromEnemy});
            sound="counter";revision++;
            return true;
        }
    }
}
