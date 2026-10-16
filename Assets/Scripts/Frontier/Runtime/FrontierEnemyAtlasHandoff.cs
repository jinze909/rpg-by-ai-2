using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Starfall
{
    public sealed partial class FrontierRuntime
    {
        // Full-body-only cross-atlas handoff: common foes and regional
        // Bosses have differently painted Idle and Walk atlases. Their
        // sudden one-frame replacement looks like a body-proportion pop
        // even when the Animator samples the correct GameCore cell.
        // Never compose individual body parts or create missing keyframes.
        private sealed class EnemyAtlasTransition
        {
            internal int room=-1,pose=-1;
            internal Point position;
            internal Texture2D sheet;
            internal int columns,rows,frame;
            internal float size;
            internal Color tint;
            internal bool flip;
            internal Texture2D departing;
            internal int departingColumns,departingRows,departingFrame;
            internal float departingSize,startedAt;
            internal Color departingTint;
            internal bool departingFlip,active;
        }
        private readonly ConditionalWeakTable<Enemy,EnemyAtlasTransition>
            enemyAtlasTransitions =
            new ConditionalWeakTable<Enemy,EnemyAtlasTransition>();
        private const float EnemyAtlasHandoffSeconds=.105f;

        private void DrawEnemyAtlasHandoff(Enemy enemy,Texture2D sheet,
            int columns,int rows,int frame,Point foot,float size,Color tint,
            bool flip,int pose)
        {
            // Keep the existing exact-cell Animator/SpriteRenderer path for
            // both the departing and current pose. Outline previews must
            // never alter the cached living snapshot.
            if(enemy==null||sheet==null||drawingOutline)
            {
                Actor(sheet,columns,rows,frame,foot,size,tint,flip);
                return;
            }
            EnemyAtlasTransition state=enemyAtlasTransitions.GetValue(enemy,
                ignored=>new EnemyAtlasTransition());
            bool sameRoom=state.room==Game.save.room;
            bool closeEnough=sameRoom&&
                (enemy.Position-state.position).Length<.68f;
            bool crossAtlas=state.sheet!=null&&
                state.sheet!=sheet;
            // Only crossfade a nonviolent posture handoff. An actual
            // windup/contact remains instantaneous and fully readable at
            // its GameCore-selected source key. No old attack sprite can
            // conceal a projectile, parry cue or Boss phase transition.
            bool quiet=(pose==0||pose==1)&&
                (state.pose==0||state.pose==1||state.pose==4);
            bool allow=!Game.save.reducedMotion&&
                Game.save.effectsQuality>0&&!Game.Dying&&
                enemy.hp>0f&&enemy.state!=1&&
                enemy.releaseTime<=0f&&enemy.flash<=0f&&
                closeEnough&&quiet&&state.flip==flip&&
                clock>=state.startedAt;
            if(crossAtlas)
            {
                state.active=allow;
                if(allow)
                {
                    state.departing=state.sheet;
                    state.departingColumns=state.columns;
                    state.departingRows=state.rows;
                    state.departingFrame=state.frame;
                    state.departingSize=state.size;
                    state.departingTint=state.tint;
                    state.departingFlip=state.flip;
                    state.startedAt=clock;
                }
            }
            else if(!sameRoom||!closeEnough||clock<state.startedAt||
                    Game.save.reducedMotion||Game.save.effectsQuality==0||
                    // A live hit-flash, turned actor or ongoing contact must
                    // not inherit a stale old-facing silhouette under the
                    // next fully authored frame. Cancel, never rewind.
                    state.flip!=flip||enemy.flash>0f||
                    enemy.hp<=0f||enemy.state==1||
                    enemy.releaseTime>0f)
                state.active=false;
            // Record the LAST authored cell for the next true source change.
            // This does not affect death snapshots or actual action clocks.
            state.room=Game.save.room;state.pose=pose;
            state.position=enemy.Position;state.sheet=sheet;
            state.columns=columns;state.rows=rows;state.frame=frame;
            state.size=size;state.tint=tint;state.flip=flip;
            if(state.active && state.departing!=null)
            {
                float progress=Mathf.Clamp01(
                    (clock-state.startedAt)/EnemyAtlasHandoffSeconds);
                if(progress>=1f)state.active=false;
                else
                {
                    // Retain one complete previous body at exactly the
                    // CURRENT world foot. This is presentation only, less
                    // than 106ms, no extra movement or gameplay input.
                    float alpha=(1f-progress)*.38f;
                    Actor(state.departing,state.departingColumns,
                        state.departingRows,state.departingFrame,foot,
                        state.departingSize,
                        new Color(state.departingTint.r,
                            state.departingTint.g,state.departingTint.b,
                            state.departingTint.a*alpha),
                        state.departingFlip);
                }
            }
            // The active frame is ALWAYS drawn at full gameplay-authorized
            // opacity over the decaying afterimage. Weapon contact, hurt
            // geometry and source frame identity remain authoritative.
            Actor(sheet,columns,rows,frame,foot,size,tint,flip);
        }
    }
}
