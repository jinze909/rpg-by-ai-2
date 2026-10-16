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
            internal float size,displaySize;
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

        private float DrawEnemyAtlasHandoff(Enemy enemy,Texture2D sheet,
            int columns,int rows,int frame,Point foot,float size,Color tint,
            bool flip,int pose)
        {
            // Keep the existing exact-cell Animator/SpriteRenderer path for
            // both the departing and current pose. Outline previews must
            // never alter the cached living snapshot.
            if(enemy==null||sheet==null||drawingOutline)
            {
                Actor(sheet,columns,rows,frame,foot,size,tint,flip);
                return size;
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
                    // A second, rapid atlas switch can interrupt an
                    // unfinished size transition. The next one must begin
                    // from the size actually PAINTED on the previous frame,
                    // not an unseen source atlas's nominal dimensions.
                    state.departingSize=state.displaySize>0f?
                        state.displaySize:state.size;
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
            float drawSize=size;
            if(state.active&&state.departing!=null)
            {
                float progress=Mathf.Clamp01(
                    (clock-state.startedAt)/EnemyAtlasHandoffSeconds);
                if(progress>=1f)state.active=false;
                else
                {
                    // Regional Boss Idle and Walk source paintings use
                    // different accepted native canvases (320 vs 384 px).
                    // Blending their opacity but swapping their size at
                    // once left a 20% HEAD/BODY pop at animation entry.
                    // Smooth the dimensions of the WHOLE source body,
                    // never rescale separate limbs or change game motion.
                    float eased=progress*progress*(3f-2f*progress);
                    drawSize=state.departingSize+
                        (size-state.departingSize)*eased;
                    float alpha=(1f-progress)*.38f;
                    Actor(state.departing,state.departingColumns,
                        state.departingRows,state.departingFrame,foot,
                        drawSize,
                        new Color(state.departingTint.r,
                            state.departingTint.g,state.departingTint.b,
                            state.departingTint.a*alpha),
                        state.departingFlip);
                }
            }
            // Persist the ACTUAL drawn canvas for interrupted transitions.
            // Every complete Sprite shares the original world-foot pivot.
            state.displaySize=drawSize;
            // The authoritative current frame is always visible at full
            // opacity. Only its PRESENTATION size is gently interpolated.
            Actor(sheet,columns,rows,frame,foot,drawSize,tint,flip);
            // The caller must use this SAME visible canvas for phase/hit
            // flash: otherwise mask and body diverge by up to 20% during
            // the crossing between two approved full-body source atlases.
            return drawSize;
        }
    }
}
