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
            // One transition-size anchor, not an outgoing full-body Sprite:
            // crossfading two fully illustrated actors renders two heads,
            // belts and weapons even when their planted feet match.
            internal float departingSize,startedAt;
            internal bool active;
        }
        private readonly ConditionalWeakTable<Enemy,EnemyAtlasTransition>
            enemyAtlasTransitions =
            new ConditionalWeakTable<Enemy,EnemyAtlasTransition>();

        // Walking atlases contain real planted contact cells at gait phases
        // 0 and 2, while phases 1 and 3 are passing/airborne silhouettes.
        // When movement ends on an airborne cell, keep the SAME complete
        // authored body until its next planted contact, then return to Idle.
        // This is source-cell selection only: no ghost body, interpolated
        // limbs, delayed combat state, changed collision or new animation clock.
        private sealed class EnemyStopPlantState
        {
            internal int room=-1,pose=-1,directionRow=-1;
            internal Point position;
            internal Texture2D sheet;
            internal int columns,rows,frame;
            internal float size,lastAt=float.NegativeInfinity,stopUntil;
            internal bool flip,stopActive;
            internal Texture2D stopSheet;
            internal int stopColumns,stopRows,stopFrame;
            internal float stopSize;
        }
        private readonly ConditionalWeakTable<Enemy,EnemyStopPlantState>
            enemyStopPlants =
            new ConditionalWeakTable<Enemy,EnemyStopPlantState>();

        private static bool FiniteEnemyStopValue(float value)
        {
            return !float.IsNaN(value)&&!float.IsInfinity(value);
        }

        private void ResolveEnemyStopPlant(Enemy enemy,int pose,
            ref Texture2D sheet,ref int columns,ref int rows,ref int frame,
            ref float size,bool flip)
        {
            // Outline previews are not a live actor repaint and must never
            // advance or cancel the real enemy's gait transition.
            if(enemy==null||sheet==null||drawingOutline)return;
            EnemyStopPlantState state=enemyStopPlants.GetValue(enemy,
                ignored=>new EnemyStopPlantState());
            int directionRow=GameCore.DirectionRow(enemy.facing);
            bool finiteClock=FiniteEnemyStopValue(clock);
            // Update can catch up four 50ms simulation slices in one
            // render. A normal actor may therefore travel about one world
            // unit between painted frames; do not lose its planted contact
            // merely because the display ran at a low frame rate. Real
            // teleports/lunges remain outside this bounded displacement.
            bool sameContext=finiteClock&&state.room==Game.save.room&&
                (enemy.Position-state.position).Length<1.25f&&
                state.directionRow==directionRow&&state.flip==flip&&
                clock>=state.lastAt;
            bool quietIdle=pose==0&&finiteClock&&
                !Game.save.reducedMotion&&
                !Game.Dying&&enemy.hp>0f&&enemy.state!=1&&
                enemy.stun<=0f&&enemy.releaseTime<=0f&&
                enemy.hitTime<=0f&&enemy.flash<=0f;
            if(state.stopActive&&
                (!quietIdle||!sameContext||clock>=state.stopUntil))
                state.stopActive=false;

            if(!state.stopActive&&quietIdle&&sameContext&&
                state.pose==1&&state.sheet!=null&&state.columns==4&&
                state.rows>0)
            {
                int local=(state.frame%4+4)%4;
                if(local==1||local==3)
                {
                    float duration=
                        CurrentWorldVisualLook.nonheroStopPlantSeconds;
                    if(!FiniteEnemyStopValue(duration)||duration<=.001f)
                        duration=.08f;
                    float stopUntil=clock+duration;
                    if(FiniteEnemyStopValue(stopUntil)&&stopUntil>clock)
                    {
                        state.stopSheet=state.sheet;
                        state.stopColumns=state.columns;
                        state.stopRows=state.rows;
                        // Advance only to the next source-painted foot plant:
                        // 1 -> 2, 3 -> 0 within the same authored gait row.
                        state.stopFrame=state.frame-local+
                            (local==1?2:0);
                        EnemyAtlasTransition transition;
                        state.stopSize=
                            enemyAtlasTransitions.TryGetValue(enemy,
                                out transition)&&
                            transition.displaySize>0f&&
                            FiniteEnemyStopValue(transition.displaySize)?
                            transition.displaySize:state.size;
                        state.stopUntil=stopUntil;
                        state.stopActive=state.stopSize>0f&&
                            FiniteEnemyStopValue(state.stopSize);
                    }
                }
            }

            if(state.stopActive&&quietIdle&&sameContext&&
                clock<state.stopUntil)
            {
                sheet=state.stopSheet;
                columns=state.stopColumns;
                rows=state.stopRows;
                frame=state.stopFrame;
                size=state.stopSize;
            }
            else state.stopActive=false;

            // Record the one source that the production draw path will paint.
            // The next decision is keyed by this Enemy instance, never its
            // reusable numeric id; death, turns, teleport and room changes
            // cannot inherit another actor's contact phase.
            state.room=Game.save.room;
            state.pose=pose;
            state.directionRow=directionRow;
            state.position=enemy.Position;
            state.sheet=sheet;
            state.columns=columns;
            state.rows=rows;
            state.frame=frame;
            state.size=size;
            state.flip=flip;
            state.lastAt=clock;
        }

        private float DrawEnemyAtlasHandoff(Enemy enemy,Texture2D sheet,
            int columns,int rows,int frame,Point foot,float size,Color tint,
            bool flip,int pose)
        {
            // Keep the existing exact-cell Animator/SpriteRenderer path for
            // the CURRENT authoritative complete pose only. Outline previews
            // never change the living transition's cached dimensions.
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
            // Only resize a quiet Idle<->Walk handoff. The destination
            // GameCore source Sprite immediately replaces the old one; an
            // outdated head/weapon is never rendered under an actor even at
            // 1% opacity. Combat windup/contact/hit bypass interpolation.
            bool quiet=(pose==0||pose==1)&&
                (state.pose==0||state.pose==1);
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
                    // Rapid Idle<->Walk switches restart from the ONE
                    // canvas size actually painted last repaint; never from
                    // a second ghost image or unseen 384px atlas width.
                    state.departingSize=state.displaySize>0f?
                        state.displaySize:state.size;
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
                    enemy.releaseTime>0f||
                    // Regional Bosses intentionally keep their WalkBoss
                    // source while a hit reaction is active. If the bright
                    // hit flash expires first, a still-running Idle->Walk
                    // blend must not paint an old idle BODY over the actual
                    // hit keyframe. Cancel every non-locomotion pose here.
                    (pose!=0&&pose!=1))
                state.active=false;
            // Record the LAST authored cell for the next true source change.
            // This does not affect death snapshots or actual action clocks.
            state.room=Game.save.room;state.pose=pose;
            state.position=enemy.Position;state.sheet=sheet;
            state.columns=columns;state.rows=rows;state.frame=frame;
            state.size=size;state.tint=tint;state.flip=flip;
            float drawSize=size;
            if(state.active)
            {
                float progress=Mathf.Clamp01(
                    (clock-state.startedAt)/
                    CurrentWorldVisualLook.nonheroAtlasHandoffSeconds);
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
                    // Deliberately NO Actor(departing...). The previous
                    // implementation painted two complete anatomy sets
                    // (heads, torsos, weapons) for up to 105ms. Subtle size
                    // easing of ONE current native Sprite is continuous and
                    // does not invent/clone any artist-approved body pixels.
                }
            }
            // Persist the ACTUAL drawn canvas for interrupted transitions.
            // Every complete Sprite shares the original world-foot pivot.
            state.displaySize=drawSize;
            // Exactly ONE complete current Sprite, even at a transition
            // boundary. Its source/action, alpha and facing remain entirely
            // GameCore-owned; only screen-space whole-body size eases.
            Actor(sheet,columns,rows,frame,foot,drawSize,tint,flip);
            // The caller must use this SAME visible canvas for phase/hit
            // flash: otherwise mask and body diverge by up to 20% during
            // the crossing between two approved full-body source atlases.
            return drawSize;
        }
    }
}
