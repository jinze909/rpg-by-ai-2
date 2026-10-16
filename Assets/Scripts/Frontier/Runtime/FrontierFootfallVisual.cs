using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
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
            internal float intensity;
        }

        private readonly List<FootfallMark> footfallMarks =
            new List<FootfallMark>(18);
        private int paintedFootContact = -1, paintedFootRoom = -1;
        private const int MaxFootfallMarks = 18;
        private sealed class StepTracker
        {
            internal Point previous;
            internal int room=-1;
            internal float travel;
        }
        // Per-actor ID is NOT a stable identity across saved/respawned enemies.
        // Weak object references isolate identical IDs and avoid accumulating
        // old regional encounters in the transient visual renderer.
        private readonly ConditionalWeakTable<Enemy,StepTracker> enemySteps =
            new ConditionalWeakTable<Enemy,StepTracker>();
        private Point companionPrevious;
        private int companionRoom=-1,companionId=-1;
        private float companionTravel;


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
                companionRoom=-1;
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
                EmitFootfall(Game.Position,room,
                    FrontierAudio.FootSurface(Game),(contacts&1)!=0,1f);
            }
            paintedFootContact=contacts;
            if(Game.save.effectsQuality>1)
            {
                AdvanceCompanionFootfalls(room);
                AdvanceEnemyFootfalls(room);
            }
        }

        private void EmitFootfall(Point at,int room,string surface,
            bool alternate,float intensity)
        {
            var mark=new FootfallMark{
                position=at,room=room,surface=surface,age=0f,
                alternate=alternate,variant=surface=="snow"?6:2,
                intensity=intensity
            };
            // Bounded even in multi-enemy fights. Never reallocate a texture
            // or mutate an NPC/Boss body to simulate individual steps.
            if(footfallMarks.Count==MaxFootfallMarks)
                footfallMarks.RemoveAt(0);
            footfallMarks.Add(mark);
        }

        private void AdvanceCompanionFootfalls(int room)
        {
            Point now=Game.companionPosition;
            int id=Game.save.companion;
            if(id<0){companionRoom=-1;return;}
            if(companionRoom!=room || companionId!=id)
            {
                companionId=id;companionRoom=room;
                companionPrevious=now;companionTravel=0f;return;
            }
            float distance=(now-companionPrevious).Length;
            companionPrevious=now;
            if(!Game.companionMoving || Game.companionAction!=null ||
                distance<.003f || distance>.95f)
            {
                companionTravel=0f;return;
            }
            companionTravel+=distance;
            if(companionTravel>=.48f)
            {
                companionTravel%=.48f;
                EmitFootfall(now,room,FrontierAudio.FootSurface(Game,now),
                    ((int)(Game.companionMoveTime*4f)&1)!=0,.84f);
            }
        }

        private void AdvanceEnemyFootfalls(int room)
        {
            foreach(Enemy enemy in Game.enemies)
            {
                StepTracker tracker=enemySteps.GetValue(enemy,
                    ignored=>new StepTracker());
                Point now=enemy.Position;
                if(tracker.room!=room)
                {
                    tracker.room=room;tracker.previous=now;
                    tracker.travel=0f;continue;
                }
                float distance=(now-tracker.previous).Length;
                tracker.previous=now;
                // Movement must come from real translated feet. A stagger,
                // windup, knockback or teleport cannot emit phantom steps.
                if(!enemy.moving || enemy.state!=0 ||
                   enemy.releaseTime>0f || enemy.stun>0f ||
                   enemy.hp<=0f || distance<.003f || distance>.95f)
                {
                    tracker.travel=0f;continue;
                }
                tracker.travel+=distance;
                float stride=enemy.boss?.78f:.46f;
                if(tracker.travel>=stride)
                {
                    tracker.travel%=stride;
                    EmitFootfall(now,room,FrontierAudio.FootSurface(Game,now),
                        ((int)(enemy.moveTime*4f)&1)!=0,
                        enemy.boss?.90f:.65f);
                }
            }
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
                mark.intensity*(1f-progress)*(1f-progress);
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
