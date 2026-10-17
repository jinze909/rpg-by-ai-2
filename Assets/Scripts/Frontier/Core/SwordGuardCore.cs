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
        private bool TryParryRealMelee(string source,Point? incoming)
        {
            // A sword-guard blocks the CONTACT of an actual lunge, never the
            // abstract warning circle from a shockwave. Frontal contact and
            // nine spare energy are required; rear hits retain normal armor.
            if(source!="lunge"||!SwordGuardReady||save.energy<9||
                !incoming.HasValue||!Finite(incoming.Value.x)||
                !Finite(incoming.Value.y)||incoming.Value.Length<.5f)return false;
            Point threat=incoming.Value.Normalized*-1;
            Point face=aim.Normalized;
            if(face.Length<.1f||face.x*threat.x+face.y*threat.y<.55f)
                return false;
            save.energy-=9;
            deflectCooldown=.36f;deflectFlash=.42f;
            riposteTime=Math.Max(riposteTime,1.5f);
            effects.Add(new Effect("number",Position,.7f,
                L("破招！立即反击","PARRY · RIPOSTE")));
            effects.Add(new Effect("burst",Position,.25f)
                {radius=.8f,variant=0,direction=threat});
            sound="counter";revision++;
            return true;
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
            // A deflected projectile starts an entirely NEW outbound visual
            // wake; retaining inbound age creates three fully bright ghost
            // arrows on the first reflected frame. Keep original real
            // lifetime/position/collision unchanged.
            p.trailResetAt=p.age;
            p.velocity=fromEnemy*Math.Max(7f,p.velocity.Length);
            p.damage=Math.Max(14f,Math.Min(68f,Damage*.85f));
            p.life=Math.Min(1.6f,p.life);
            p.piercing=false;p.explosive=false;p.slowing=false;
            // Reflection is the warrior's real impact, not a delayed attack
            // using whichever equipment the hero happens to wear on arrival.
            // Snapshot the sword profile at CONTACT, preserving mastery,
            // stagger and critical semantics even if the player switches
            // loadouts while the returned arrow travels.
            p.impact=CaptureImpact(0,0);
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
            CountSwordLessonDeflection(p);
            return true;
        }
    }
}
