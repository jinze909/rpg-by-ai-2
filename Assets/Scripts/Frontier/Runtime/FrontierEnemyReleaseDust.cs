using System;
using UnityEngine;

namespace Starfall
{
    public sealed partial class FrontierRuntime
    {
        // Three genuinely painted dust materials x four connected contact
        // snapshots, attached to an actual living enemy RELEASE event.
        // Never replace/rig an actor or manufacture another game hit.
        private Texture2D enemyReleaseDustSheet;
        private float enemyReleaseDustLastCheck=float.NegativeInfinity;
        private float enemyReleaseDustNextCheck=float.NegativeInfinity;

        private void DrawAuthoredEnemyReleaseDust(Enemy enemy,Point foot,
            float fullBodySize)
        {
            if(enemy==null||enemy.hp<=0f||enemy.state==1||
                enemy.releaseTime<=.001f||enemy.stun>0f||
                drawingOutline||Game.practicing||
                Game.save.reducedMotion||Game.save.effectsQuality<2)
                return;
            if(clock<enemyReleaseDustLastCheck||
               clock>=enemyReleaseDustNextCheck)
            {
                enemyReleaseDustLastCheck=clock;
                enemyReleaseDustNextCheck=clock+2.5f;
                Texture2D incoming=Resources.Load<Texture2D>(
                    "Frontier/Art/EnemyReleaseDust");
                // An incomplete Editor PNG reimport must never tear an
                // already accepted full three-material sprite family.
                if(incoming!=null&&incoming.width==192&&
                   incoming.height==72)
                    enemyReleaseDustSheet=incoming;
            }
            if(enemyReleaseDustSheet==null||
               enemyReleaseDustSheet.width!=192||
               enemyReleaseDustSheet.height!=72)return;

            // GameCore is the single source for hit/contact timing. Boss
            // attack releases use a longer actual clock than common foes.
            float releaseSpan=enemy.boss?.38f:.24f;
            float progress=Mathf.Clamp01(
                1f-enemy.releaseTime/releaseSpan);
            int material=enemy.boss?
                (Game.save.room==1?0:Game.save.room>=5?2:1):
                (enemy.kind==0?0:enemy.kind==2?1:2);
            int first=material*4;
            float extent=enemy.boss?
                Math.Min(82f,fullBodySize*.34f):
                Math.Min(52f,fullBodySize*.49f);
            float height=extent*.50f;
            // This remains UNDER the full-body actor and on its original
            // planted screen foot through windup -> release -> recovery.
            Rect grounded=new Rect(foot.x-extent*.5f,
                foot.y-height*.88f,extent,height);
            float opacity=CurrentWorldVisualLook.enemyReleaseDustAlpha*
                (Game.Night?.84f:1f)*(1f-progress*.38f);
            float sample=progress*4f;
            int frame=Math.Min(3,Math.Max(0,(int)Math.Floor(sample)));
            float half=CurrentWorldVisualLook.enemyReleaseDustSeam;
            int seam=(int)Math.Floor(sample+.5f);
            if(seam>=1&&seam<=3&&
                sample>seam-half&&sample<seam+half)
            {
                // Blend two intact original 48px paintings around the real
                // source-frame boundary, with one unchanged world/pivot.
                // This softens the previous one-frame material pop without
                // smoothing/warping a single pixel of character anatomy.
                float blend=Mathf.Clamp01(
                    (sample-seam+half)/(2f*half));
                Atlas(enemyReleaseDustSheet,4,3,first+seam-1,
                    grounded,new Color(1f,1f,1f,opacity*(1f-blend)));
                Atlas(enemyReleaseDustSheet,4,3,first+seam,
                    grounded,new Color(1f,1f,1f,opacity*blend));
            }
            else
                Atlas(enemyReleaseDustSheet,4,3,first+frame,
                    grounded,new Color(1f,1f,1f,opacity));
        }
    }
}
