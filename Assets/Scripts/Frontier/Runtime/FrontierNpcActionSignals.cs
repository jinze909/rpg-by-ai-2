using System;
using UnityEngine;

namespace Starfall
{
    public sealed partial class FrontierRuntime
    {
        // Decor-only 4x2 miniature pixel-art signal atlas, separate from
        // native whole-body actors. No animator, head rig or changed NPC pose.
        // Row zero: real pinned conversation. Row one: actual escort distress.
        private Texture2D npcActionSignalSheet;
        private float npcSignalRetryAt=float.NegativeInfinity;
        private float npcSignalLastCheck=float.NegativeInfinity;

        private void DrawNpcActionSignal(Point plantedFoot,int row,int seed,
            float actorOpacity)
        {
            if(drawingOutline||Game.save.effectsQuality==0)return;
            if(clock<npcSignalLastCheck||clock>=npcSignalRetryAt)
            {
                npcSignalLastCheck=clock;
                npcSignalRetryAt=clock+2.5f;
                Texture2D imported=Resources.Load<Texture2D>(
                    "Frontier/Art/NpcActionSignals");
                // Keep previous fully imported signal during Editor rebake.
                if(imported!=null&&imported.width==96&&
                    imported.height==40)npcActionSignalSheet=imported;
            }
            if(npcActionSignalSheet==null)return;
            WorldVisualLook style=CurrentWorldVisualLook;
            float alpha=row==0?style.npcSignalSpeechAlpha:
                style.npcSignalDangerAlpha;
            alpha*=Game.save.effectsQuality==1?.70f:1f;
            // A speaker or escorted civilian can be partially obscured by
            // a foreground tree/wall or entering the viewport. Fade this
            // original painted signal WITH the rooted body, never leave a
            // bright detached label where the full-body source has faded.
            // Atlas() already applies worldActorOpacity once for EVERY
            // Sprite. This signal inherits its actor's distinct occlusion
            // tint only; multiplying the world fade again made the speech
            // icon disproportionately dark near cover and viewport edges.
            alpha*=Mathf.Clamp01(actorOpacity);
            // Low-motion users retain exactly one static communicative cue.
            // All four micro-pixel frames share one stationary world root.
            int frame=Game.save.reducedMotion?0:
                ((int)Math.Floor(clock*style.npcSignalFrameRate+
                    seed*.29f)%4+4)%4;
            Atlas(npcActionSignalSheet,4,2,row*4+frame,
                new Rect(plantedFoot.x-15f,plantedFoot.y-119f,
                    30f,25f),
                new Color(1f,1f,1f,alpha));
        }
    }
}
