using System;
using System.Collections.Generic;
using UnityEngine;

namespace Starfall
{
    public sealed partial class FrontierRuntime
    {
        // Purely transient source-Sprite environment contacts. The simulation
        // already reports real footContacts; this never changes the canonical
        // 256px hero body, gait timer, terrain, physics or SaveData schema.
        private sealed class FootfallMark
        {
            internal Point position;
            internal int room, variant;
            internal string surface;
            internal float age;
            internal bool alternate;
        }

        private readonly List<FootfallMark> footfallMarks =
            new List<FootfallMark>(12);
        private int paintedFootContact = -1, paintedFootRoom = -1;
        private const int MaxFootfallMarks = 12;

        private void AdvanceFootfallVisual(float delta)
        {
            if(Game == null) return;
            int room=Game.save.room;
            int contacts=Game.footContacts;
            if(paintedFootRoom!=room || paintedFootContact<0 ||
               contacts<paintedFootContact)
            {
                // No ghosts across teleport, save reload, death or scene
                // transition. Never replay past footsteps as fresh VFX.
                footfallMarks.Clear();
                paintedFootRoom=room;
                paintedFootContact=contacts;
                return;
            }
            if(Game.mode!="world" || Game.save.reducedMotion ||
               Game.save.effectsQuality==0 || Game.Dying)
            {
                footfallMarks.Clear();
                paintedFootContact=contacts;
                return;
            }
            float lifetime=CurrentWorldVisualLook.footfallLifetime;
            for(int i=footfallMarks.Count-1;i>=0;--i)
            {
                footfallMarks[i].age+=Math.Max(0,delta);
                if(footfallMarks[i].age>=lifetime)
                    footfallMarks.RemoveAt(i);
            }
            // One repaint can contain several 50ms physics slices. A huge
            // teleport-style contact jump must not emit a pile of decals.
            if(contacts>paintedFootContact && Game.ActuallyMoving &&
               Game.dashTime<=0 && Game.playerAction==null)
            {
                string surface=FrontierAudio.FootSurface(Game);
                var mark=new FootfallMark{
                    position=Game.Position,room=room,
                    surface=surface,age=0f,
                    alternate=(contacts&1)!=0,
                    variant=surface=="snow"?6:2
                };
                // Keep only an actual last contact at the new coordinate.
                // Previously drawn marks remain fixed even as the camera
                // and hero walk away from their source position.
                if(footfallMarks.Count==MaxFootfallMarks)
                    footfallMarks.RemoveAt(0);
                footfallMarks.Add(mark);
            }
            paintedFootContact=contacts;
        }

        private static Color FootfallTint(string surface,float opacity)
        {
            switch(surface)
            {
                case "snow":return new Color(.73f,.88f,1f,opacity);
                case "sand":return new Color(.96f,.82f,.59f,opacity);
                case "wood":return new Color(.78f,.68f,.55f,opacity);
                case "stone":return new Color(.76f,.78f,.78f,opacity);
                default:return new Color(.73f,.86f,.64f,opacity);
            }
        }

        private void DrawFootfallMark(FootfallMark mark)
        {
            if(effectSheet==null || mark.room!=Game.save.room ||
               Game.save.reducedMotion || Game.save.effectsQuality==0)
                return;
            float life=CurrentWorldVisualLook.footfallLifetime;
            float progress=Mathf.Clamp01(mark.age/Math.Max(.01f,life));
            float opacity=CurrentWorldVisualLook.footfallAlpha*
                (1f-progress)*(1f-progress);
            if(opacity<=.001f)return;
            Point p=ScreenPoint(mark.position);
            // One original source cell and one planted position. This is
            // world-ground detail: NOT a SpriteRenderer character, limb
            // animation, input event, gameplay projectile or raised HUD FX.
            float width=mark.surface=="snow"?24f:26f;
            float height=mark.surface=="snow"?18f:13f;
            Atlas(effectSheet,4,4,mark.variant,
                new Rect(p.x-width/2f,p.y-height*.58f,width,height),
                FootfallTint(mark.surface,opacity),mark.alternate);
        }
    }
}
