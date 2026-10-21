using System;
using UnityEngine;

namespace Starfall
{
    public sealed partial class FrontierRuntime
    {
        // Two hand-painted four-pose flower/leaf families. The ORIGINAL
        // high-detail garden/soil WorldProps body is always rendered first.
        // This only paints rooted growth inside an actually planted garden:
        // no new harvest action, timer, collider, quest or save-field.
        private Texture2D livingGardenSheet;
        private float livingGardenCheckedAt=float.NegativeInfinity;
        private float livingGardenNextCheck=float.NegativeInfinity;

        private void DrawAuthoredLivingGarden(Decoration garden,Point foot,
            int growthStage,Color originalTint)
        {
            if(drawingOutline || growthStage<=0 ||
                Game.save.effectsQuality==0)return;
            if(clock<livingGardenCheckedAt || clock>=livingGardenNextCheck)
            {
                livingGardenCheckedAt=clock;
                livingGardenNextCheck=clock+2.5f;
                var incoming=Resources.Load<Texture2D>(
                    "Frontier/Art/GardenLiving");
                // Keep the last complete original family while a PNG
                // importer publishes half a revision during Editor Play.
                if(incoming!=null&&incoming.width==128&&
                   incoming.height==64)livingGardenSheet=incoming;
            }
            if(livingGardenSheet==null||
               livingGardenSheet.width!=128||
               livingGardenSheet.height!=64)return;
            int stage=growthStage==1?0:1;
            int seed=TileSeed((int)(garden.x*7),
                (int)(garden.y*7),Game.save.room);
            int frame=Game.save.reducedMotion?0:
                ((int)Math.Floor(clock*
                CurrentWorldVisualLook.gardenLivingFrameRate+
                (seed%31)*.11f)%4+4)%4;
            float alpha=CurrentWorldVisualLook.gardenLivingAlpha*
                originalTint.a*(Game.Night?.89f:1f)*
                (Game.save.effectsQuality==1?.60f:1f);
            // The two rows have exactly equal source footholds. Fix the
            // destination rectangle through every frame and growth stage:
            // all leaf movement is in the ORIGINAL 32x32 painted cells.
            // Atlas applies worldActorOpacity exactly once.
            Rect leaves=new Rect(foot.x-23f,foot.y-52f,46f,46f);
            Atlas(livingGardenSheet,4,2,stage*4+frame,leaves,
                new Color(1f,1f,1f,alpha));
        }
    }
}
