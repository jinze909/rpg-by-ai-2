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
            internal int room, material;
            internal string surface;
            internal float age;
            internal bool alternate;
            internal float intensity;
        }

        private readonly List<FootfallMark> footfallMarks =
            new List<FootfallMark>(18);
        private int paintedFootContact = -1, paintedFootRoom = -1;
        private int paintedHeroContactIndex=-1;
        private float paintedHeroWalkDistance;
        private Point paintedHeroPosition;
        private GameCore paintedHeroOwner;
        // GameCore.Load can replace SaveData without changing the GameCore
        // instance or room. Old physical prints belong to the old journey.
        private SaveData paintedHeroSave;
        private const int MaxFootfallMarks = 18;
        private Texture2D groundContactSheet;
        private float groundContactLastCheck=float.NegativeInfinity;
        private float groundContactNextCheck=float.NegativeInfinity;

        private static int GroundContactMaterial(string surface)
        {
            switch(surface)
            {
                case "stone":return 1;
                case "sand":return 2;
                case "snow":return 3;
                case "wood":return 4;
                default:return 0;
            }
        }

        private void RefreshGroundContactSource()
        {
            if(clock<groundContactLastCheck||clock>=groundContactNextCheck)
            {
                groundContactLastCheck=clock;
                groundContactNextCheck=clock+2.5f;
                Texture2D incoming=Resources.Load<Texture2D>(
                    "Frontier/Art/GroundContactFrames");
                // Publish only a complete five-material, four-frame source.
                if(incoming!=null&&incoming.width==128&&
                    incoming.height==100)groundContactSheet=incoming;
            }
        }
        private sealed class StepTracker
        {
            internal Point previous;
            internal int room=-1;
            internal float lastMoveTime;
            internal int contactIndex=-1;
        }
        // Per-actor ID is NOT a stable identity across saved/respawned enemies.
        // Weak object references isolate identical IDs and avoid accumulating
        // old regional encounters in the transient visual renderer.
        private readonly ConditionalWeakTable<Enemy,StepTracker> enemySteps =
            new ConditionalWeakTable<Enemy,StepTracker>();

        // Contact 0 and contact 2 are the two grounded cells in every
        // accepted four-frame Walk sprite row. Use the SAME distance phase
        // as the source's GameCore.GaitFrame, not an unrelated dust cadence.
        private static int PlantedFootfallIndex(float distance,float stride)
        {
            if(float.IsNaN(distance)||float.IsInfinity(distance)||
               float.IsNaN(stride)||float.IsInfinity(stride)||
               distance<0f||stride<=.1f)return -1;
            double index=Math.Floor((double)distance*2d/stride+1e-7d);
            return index>=int.MaxValue?int.MaxValue:(int)index;
        }
        private Point companionPrevious;
        private int companionRoom=-1,companionId=-1;
        private GameCore companionFootfallOwner;
        private int companionFootfallGeneration=-1;
        private float companionLastMoveTime;
        private int companionContactIndex=-1;


        private static bool IsValidFootfallMark(FootfallMark mark)
        {
            return mark!=null &&
                FiniteWorldPoint(mark.position) &&
                FiniteWorldCoordinate(mark.age) && mark.age>=0f &&
                FiniteWorldCoordinate(mark.intensity) && mark.intensity>0f;
        }

        private void AdvanceFootfallVisual(float delta)
        {
            if(Game == null) return;
            int room=Game.save.room;
            int contacts=Game.footContacts;
            if(paintedFootRoom!=room || paintedFootContact<0 ||
               contacts<paintedFootContact ||
               !System.Object.ReferenceEquals(paintedHeroOwner,Game) ||
               !System.Object.ReferenceEquals(paintedHeroSave,Game.save))
            {
                // No ghosts across teleport, save reload, death or scene
                // transition. Never replay past footsteps as fresh VFX.
                footfallMarks.Clear();
                paintedFootRoom=room;
                paintedFootContact=contacts;
                // A same-room save switch is still an entirely new gait
                // owner, even if its audio step counter happens to match.
                companionRoom=-1;
                paintedHeroSave=Game.save;
                paintedHeroOwner=Game;
                paintedHeroWalkDistance=Game.walkDistance;
                paintedHeroPosition=Game.Position;
                paintedHeroContactIndex=PlantedFootfallIndex(
                    Game.walkDistance,GameCore.WalkStride);
                return;
            }
            if(Game.mode!="world" || Game.save.reducedMotion ||
               Game.save.effectsQuality==0 || Game.Dying)
            {
                footfallMarks.Clear();
                paintedFootContact=contacts;
                paintedHeroSave=Game.save;
                paintedHeroOwner=Game;
                paintedHeroWalkDistance=Game.walkDistance;
                paintedHeroPosition=Game.Position;
                paintedHeroContactIndex=PlantedFootfallIndex(
                    Game.walkDistance,GameCore.WalkStride);
                companionRoom=-1;
                return;
            }
            float lifetime=CurrentWorldVisualLook.footfallLifetime;
            float elapsed=FiniteWorldCoordinate(delta)?Math.Max(0f,delta):0f;
            for(int i=footfallMarks.Count-1;i>=0;--i)
            {
                FootfallMark mark=footfallMarks[i];
                if(!IsValidFootfallMark(mark))
                {
                    footfallMarks.RemoveAt(i);
                    continue;
                }
                mark.age+=elapsed;
                if(!FiniteWorldCoordinate(mark.age)||mark.age>=lifetime)
                    footfallMarks.RemoveAt(i);
            }
            // Hero audio contact count is a separate cadence from the
            // authentic four-frame walking BODY. Ground decals must appear
            // only as its painted sole arrives at cells 2/0, never during
            // the airborne 1/3 frames; leave sound and physics unchanged.
            int planted=PlantedFootfallIndex(
                Game.walkDistance,GameCore.WalkStride);
            bool sameHero=System.Object.ReferenceEquals(
                paintedHeroOwner,Game);
            bool continued=Game.walkDistance>=
                paintedHeroWalkDistance-.0001f;
            // A same-room teleport may retain monotonic gait phase. Match
            // the companion/enemy footprint rule: only genuine bounded
            // physical travel may leave a painted planted-foot contact.
            Point currentFoot=Game.Position;
            float physicalStep=(currentFoot-paintedHeroPosition).Length;
            bool physicalContact=physicalStep>=.003f&&physicalStep<=.95f;
            if(sameHero&&continued&&physicalContact&&planted>=0&&
               paintedHeroContactIndex>=0&&
               planted>paintedHeroContactIndex&&
               planted-paintedHeroContactIndex<=2&&
               Game.ActuallyMoving&&Game.dashTime<=0&&
               Game.playerAction==null)
            {
                EmitFootfall(Game.Position,room,
                    FrontierAudio.FootSurface(Game),
                    (planted&1)!=0,1f);
            }
            paintedHeroOwner=Game;
            paintedHeroSave=Game.save;
            paintedHeroContactIndex=planted;
            paintedHeroWalkDistance=Game.walkDistance;
            paintedHeroPosition=currentFoot;
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
                alternate=alternate,material=GroundContactMaterial(surface),
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
            if(id<0){companionRoom=-1;companionFootfallOwner=null;return;}
            // DrawCompanionActor samples this same four-cell source family:
            // gait phases 0/2 are grounded and appear every HALF stride.
            float stride=GameCore.WalkStride*
                CurrentWorldVisualLook.companionStrideDistanceFactor;
            // Use the exact same rebased source-frame distance as
            // DrawCompanionActor; otherwise a stop/restart could show a
            // planted body while the dirt/grass footfall fires mid-air.
            float gaitDistance=CompanionGaitDistance(true);
            int contact=PlantedFootfallIndex(gaitDistance,stride);
            if(companionRoom!=room || companionId!=id ||
               !System.Object.ReferenceEquals(companionFootfallOwner,Game) ||
               companionFootfallGeneration!=Game.companionPresentationGeneration)
            {
                companionId=id;companionRoom=room;
                companionFootfallOwner=Game;
                companionFootfallGeneration=Game.companionPresentationGeneration;
                companionPrevious=now;
                companionLastMoveTime=gaitDistance;
                companionContactIndex=contact;return;
            }
            float distance=(now-companionPrevious).Length;
            bool rewound=gaitDistance<
                companionLastMoveTime-.0001f;
            companionPrevious=now;
            companionLastMoveTime=gaitDistance;
            int prior=companionContactIndex;
            companionContactIndex=contact;
            // Paused spells, collisions, party swaps and teleport recovery
            // must not replay old foot contacts or add ghost footsteps.
            if(!Game.companionMoving || Game.companionAction!=null ||
               distance<.003f || distance>.95f || rewound ||
               contact<0 || prior<0 || contact<=prior ||
               contact-prior>2)return;
            EmitFootfall(now,room,FrontierAudio.FootSurface(Game,now),
                (contact&1)!=0,.84f);
        }

        private void AdvanceEnemyFootfalls(int room)
        {
            foreach(Enemy enemy in Game.enemies)
            {
                StepTracker tracker=enemySteps.GetValue(enemy,
                    ignored=>new StepTracker());
                Point now=enemy.Position;
                // The exact source Sprite selected in DrawEnemyActor uses
                // 2.8 units per Boss walk cycle and 1.35/1.8 for enemies.
                // Its planted cells are 0/2, one contact per HALF cycle.
                float stride=enemy.boss?2.8f:
                    enemy.kind==0?1.35f:1.8f;
                int contact=PlantedFootfallIndex(enemy.moveTime,stride);
                if(tracker.room!=room)
                {
                    tracker.room=room;tracker.previous=now;
                    tracker.lastMoveTime=enemy.moveTime;
                    tracker.contactIndex=contact;continue;
                }
                float distance=(now-tracker.previous).Length;
                bool rewound=enemy.moveTime<tracker.lastMoveTime-.0001f;
                tracker.previous=now;
                tracker.lastMoveTime=enemy.moveTime;
                int prior=tracker.contactIndex;
                tracker.contactIndex=contact;
                // Only source-painted foot plants produce ground contacts.
                // A lunge, collision warp, pause, recoil or skipped giant
                // frame never manufactures prints at an airborne keyframe.
                if(!enemy.moving || enemy.state!=0 ||
                   enemy.releaseTime>0f || enemy.stun>0f ||
                   enemy.hp<=0f || distance<.003f || distance>.95f ||
                   rewound || contact<0 || prior<0 ||
                   contact<=prior || contact-prior>2)
                    continue;
                EmitFootfall(now,room,FrontierAudio.FootSurface(Game,now),
                    (contact&1)!=0,enemy.boss?.90f:.65f);
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
            if(!IsValidFootfallMark(mark)||mark.room!=Game.save.room||
               Game.save.reducedMotion||Game.save.effectsQuality==0)
                return;
            float life=CurrentWorldVisualLook.footfallLifetime;
            float progress=Mathf.Clamp01(mark.age/Math.Max(.01f,life));
            float opacity=CurrentWorldVisualLook.footfallAlpha*
                mark.intensity*(1f-progress)*(1f-progress);
            if(opacity<=.001f)return;
            Point p=ScreenPoint(mark.position);
            RefreshGroundContactSource();
            if(groundContactSheet!=null)
            {
                // Each material has four real 32x20 contact keyframes.
                // Root stays fixed under the actor; GameCore owns movement.
                int frame=Game.save.effectsQuality==1?0:
                    Math.Min(3,(int)Math.Floor(progress*4f));
                float width=mark.intensity>.88f?44f:
                    mark.intensity<.75f?30f:36f;
                float height=width*20f/32f;
                Rect onGround=new Rect(p.x-width*.5f,
                    p.y-height*.70f,width,height);
                Atlas(groundContactSheet,4,5,mark.material*4+frame,
                    onGround,new Color(1f,1f,1f,opacity),
                    mark.alternate);
                return;
            }
            // Missing art falls back to prior source without mutating
            // movement, attack timing, terrain or old save data.
            if(effectSheet==null)return;
            float legacyWidth=mark.surface=="snow"?24f:26f;
            float legacyHeight=mark.surface=="snow"?18f:13f;
            Atlas(effectSheet,4,4,mark.surface=="snow"?6:2,
                new Rect(p.x-legacyWidth/2f,
                    p.y-legacyHeight*.58f,legacyWidth,legacyHeight),
                FootfallTint(mark.surface,opacity),mark.alternate);
        }
    }
}
