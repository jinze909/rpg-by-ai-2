using System;
using UnityEngine;

namespace Starfall
{
    public sealed partial class FrontierRuntime
    {
        // Combat telegraph art lives as a small, world-anchored original
        // pixel glyph. The authoritative GameCore locked target and radius
        // ring still signal the full actual damage area; this does NOT
        // change windup time, projectile release, enemy physics or SaveData.
        private Texture2D castSigilSource;
        private float castSigilNextImport=float.NegativeInfinity;
        private float castSigilLastImport=float.NegativeInfinity;

        private Texture2D PublishedCastSigils()
        {
            if(clock<castSigilLastImport||clock>=castSigilNextImport)
            {
                castSigilLastImport=clock;
                castSigilNextImport=clock+2.5f;
                Texture2D imported=Resources.Load<Texture2D>(
                    "Frontier/Art/CastSigils");
                // Unity Editor may temporarily expose a half imported
                // atlas; retain our last validated 8-pose source instead
                // of publishing a different silhouette in the next tick.
                if(imported!=null&&imported.width==128&&
                    imported.height==64)castSigilSource=imported;
            }
            return castSigilSource;
        }

        private void DrawCastSigil(Enemy enemy,float progress)
        {
            if(enemy==null||enemy.state!=1||
                Game.save.effectsQuality==0)return;
            Texture2D sheet=PublishedCastSigils();
            bool boss=enemy.boss;
            int row=boss?0:1;
            // Frame progress is owned by actual GameCore windup. Never
            // run an ambient Animator clock out of phase with impact.
            int frame=Game.save.reducedMotion?0:
                Math.Max(0,Math.Min(3,(int)Math.Floor(
                    Mathf.Clamp01(progress)*4f)));
            float size=boss?
                CurrentWorldVisualLook.castSigilBossPixels:
                CurrentWorldVisualLook.castSigilBossPixels*.72f;
            Point target=ScreenPoint(enemy.target);
            Rect canvas=new Rect(target.x-size*.5f,
                target.y-size*.5f,size,size);
            float alpha=CurrentWorldVisualLook.castSigilAlpha*
                (.53f+.29f*Mathf.Clamp01(progress));
            if(Game.save.effectsQuality==1)alpha*=.70f;
            if(sheet!=null)
                Atlas(sheet,4,2,row*4+frame,canvas,
                    new Color(1f,1f,1f,alpha));
            else if(effectSheet!=null)
            {
                // Preserve a compact accepted source visual while the new
                // authored family is not yet imported. NEVER resurrect the
                // arena-wide stretched-fog overlay.
                Atlas(effectSheet,4,4,14,canvas,
                    new Color(.87f,.54f,.41f,alpha*.65f));
            }
        }
    }
}
