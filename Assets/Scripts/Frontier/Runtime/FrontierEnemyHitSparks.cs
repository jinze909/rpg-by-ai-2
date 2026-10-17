using System;
using UnityEngine;

namespace Starfall
{
    public sealed partial class FrontierRuntime
    {
        // Three complete four-frame SOURCE paintings: mossleaf, ember and
        // arcane crystal fragments. This is purely a short-lived hit REACTION,
        // never an extra GameCore strike, stun or projectile.
        private Texture2D enemyHitSparksSheet;
        private float enemyHitSparksLastChecked=float.NegativeInfinity;
        private float enemyHitSparksNextCheck=float.NegativeInfinity;

        private void DrawAuthoredEnemyHitSparks(Enemy enemy,Point foot,
            float paintedBodySize,bool mirror,float bodyAlpha)
        {
            if(enemy==null||enemy.hp<=0f||enemy.hitTime<=.001f||
                enemy.state==1||enemy.releaseTime>.001f||
                Game.practicing||drawingOutline||
                Game.save.reducedMotion||Game.save.effectsQuality<2)
                return;
            if(clock<enemyHitSparksLastChecked||
                clock>=enemyHitSparksNextCheck)
            {
                enemyHitSparksLastChecked=clock;
                enemyHitSparksNextCheck=clock+2.5f;
                Texture2D incoming=Resources.Load<Texture2D>(
                    "Frontier/Art/EnemyHitSparks");
                // A temporarily partial Editor import cannot replace the
                // last complete, approved 3-material source atlas.
                if(incoming!=null&&incoming.width==96&&
                    incoming.height==72)
                    enemyHitSparksSheet=incoming;
            }
            if(enemyHitSparksSheet==null||
                enemyHitSparksSheet.width!=96||
                enemyHitSparksSheet.height!=72)return;

            float progress=Mathf.Clamp01(1f-enemy.hitTime/.18f);
            int family=enemy.boss?
                (Game.save.room==1?0:Game.save.room>=5?2:1):
                (enemy.kind==0?0:enemy.kind==2?1:2);
            int first=family*4;
            // The sparks are a small shoulder-area material response, not
            // a second full-body silhouette. The origin uses the ACTUALLY
            // PAINTED Boss canvas size, including 320->384 easing.
            float size=enemy.boss?Math.Min(51f,paintedBodySize*.30f):
                Math.Min(34f,paintedBodySize*.34f);
            Rect area=new Rect(foot.x-size*.5f,
                foot.y-paintedBodySize*.51f-size*.5f,size,size);
            float alpha=CurrentWorldVisualLook.enemyHitSparkAlpha*
                Mathf.Clamp01(bodyAlpha)*(1f-progress*.54f);
            float sample=progress*4f;
            int frame=Math.Min(3,Math.Max(0,(int)Math.Floor(sample)));
            int seam=(int)Math.Floor(sample+.5f);
            float half=CurrentWorldVisualLook.enemyHitSparkSeam;
            if(seam>=1&&seam<=3&&
                sample>seam-half&&sample<seam+half)
            {
                // Preserve BOTH authentic 24px cells. Crossfade only the
                // visual sparks at a shared center and screen-space root;
                // never interpolate torso/weapon or change a damage clock.
                float blend=Mathf.Clamp01(
                    (sample-seam+half)/(2f*half));
                Atlas(enemyHitSparksSheet,4,3,first+seam-1,area,
                    new Color(1,1,1,alpha*(1f-blend)),mirror);
                Atlas(enemyHitSparksSheet,4,3,first+seam,area,
                    new Color(1,1,1,alpha*blend),mirror);
            }
            else
                Atlas(enemyHitSparksSheet,4,3,first+frame,area,
                    new Color(1,1,1,alpha),mirror);
        }
    }
}
