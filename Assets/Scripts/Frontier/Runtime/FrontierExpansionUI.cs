using System;
using UnityEngine;

namespace Starfall
{
    public sealed partial class FrontierRuntime
    {
        private int journalTab, relicSlot;
        private void DrawRay(Point from, Point to, Color color)
        {
            // All campaign LightRays and the new live Roadwarden enemy-ward
            // siphons use this REAL production path. Huge offscreen-to-
            // offscreen lines previously queued thousands of invisible
            // 4px GUI rectangles in each repaint on expanded 120x80 maps.
            // Clip only the SUBMITTED samples; keep their indices against
            // the ORIGINAL segment so contact diamonds do not pop, shift
            // phase, or change opacity while the camera pans.
            Point a=ScreenPoint(from),b=ScreenPoint(to);
            Point difference=b-a;
            float distance=difference.Length;
            if(float.IsNaN(distance)||float.IsInfinity(distance)||
                distance>10000000f)return;
            int count=Math.Max(1,(int)(distance/5f));
            Point clippedA=a,clippedB=b;
            if(!ClipWarningScreenSegment(ref clippedA,ref clippedB))return;
            float denom=difference.x*difference.x+
                difference.y*difference.y;
            float first=denom>.000001f?
                ((clippedA-a).x*difference.x+
                 (clippedA-a).y*difference.y)/denom:0f;
            float last=denom>.000001f?
                ((clippedB-a).x*difference.x+
                 (clippedB-a).y*difference.y)/denom:1f;
            int firstIndex=Math.Max(0,
                (int)Math.Floor(Math.Max(0f,first)*count));
            int lastIndex=Math.Min(count,
                (int)Math.Ceiling(Math.Min(1f,last)*count));
            for(int i=firstIndex;i<=lastIndex;i++)
            {
                Point p=a+difference*(i/(float)count);
                if(p.x< -38f||p.x>Width+38f||
                   p.y< -38f||p.y>Height+38f)continue;
                Box(p.x-2f,p.y-2f,4f,4f,color);
            }
        }
        // Visual-only full-body gait continuity, never serialized to saves.
        private int companionVisualId=-1;
        private GameCore companionVisualOwner;
        private int companionVisualGeneration=-1;
        private int companionLastWalkFrame;
        private int companionLastWalkFacing;
        private bool companionWasWalking;
        private float companionStopTime;
        private ActorAction companionTrackedAction;
        private bool companionAttackStartedFromWalk;

        // One shared distance phase feeds both the source-painted walk Sprite
        // and its grounded footfalls. Rebase ONLY when a real movement segment
        // resumes, so an idle/action pause never starts the next walk airborne.
        // This is presentation state, not a second movement clock or SaveData.
        private GameCore companionGaitPhaseOwner;
        private SaveData companionGaitPhaseSave;
        private int companionGaitPhaseId=-1;
        private int companionGaitPhaseGeneration=-1;
        private int companionGaitPhaseRoom=-1;
        private float companionGaitPhaseOffset;
        private float companionGaitLastRawDistance;
        private bool companionGaitPhaseMoving;

        private float CompanionGaitDistance(bool commit)
        {
            if(Game==null||Game.save==null||Game.save.companion<0)
            {
                if(commit)
                {
                    companionGaitPhaseOwner=null;
                    companionGaitPhaseSave=null;
                    companionGaitPhaseId=-1;
                    companionGaitPhaseGeneration=-1;
                    companionGaitPhaseRoom=-1;
                    companionGaitPhaseMoving=false;
                }
                return 0f;
            }

            float raw=Game.companionMoveTime;
            if(float.IsNaN(raw)||float.IsInfinity(raw)||raw<0f)raw=0f;
            int id=Game.save.companion;
            bool same=System.Object.ReferenceEquals(companionGaitPhaseOwner,Game)&&
                System.Object.ReferenceEquals(companionGaitPhaseSave,Game.save)&&
                companionGaitPhaseId==id&&
                companionGaitPhaseGeneration==Game.companionPresentationGeneration&&
                companionGaitPhaseRoom==Game.save.room;
            // Outline/selection previews are read-only: they sample the last
            // committed phase (or raw distance for a new owner) and never
            // predict/consume a stop-restart rebase before the real body draw.
            if(!commit)
            {
                float preview=raw-(same?companionGaitPhaseOffset:0f);
                return float.IsNaN(preview)||float.IsInfinity(preview)||
                    preview<0f?0f:preview;
            }
            // A new presentation owner still honors its real accumulated
            // distance. Only a same-owner stop/restart is rebased; a distance
            // rewind is a new gait epoch and starts safely from phase zero.
            bool distanceRewound=same&&
                raw<companionGaitLastRawDistance-.0001f;
            float offset=same?companionGaitPhaseOffset:0f;
            bool wasMoving=same&&companionGaitPhaseMoving;
            if(distanceRewound)
            {
                offset=raw;
                wasMoving=false;
            }

            float stride=GameCore.WalkStride*
                CurrentWorldVisualLook.companionStrideDistanceFactor;
            if(float.IsNaN(stride)||float.IsInfinity(stride)||stride<=.1f)
                stride=GameCore.WalkStride;
            bool walking=Game.companionMoving&&
                Game.companionAction==null;
            if(walking&&!wasMoving&&same&&!distanceRewound)
            {
                int startFrame=0;
                bool sameVisual=companionVisualId==id&&
                    System.Object.ReferenceEquals(companionVisualOwner,Game)&&
                    companionVisualGeneration==Game.companionPresentationGeneration;
                if(sameVisual&&!Game.save.reducedMotion)
                    startFrame=CompanionStopPose(clock-companionStopTime);
                float plantedDistance=startFrame*stride*.25f;
                if(startFrame>0)plantedDistance+=.001f;
                offset=raw-plantedDistance;
            }

            float phaseDistance=raw-offset;
            if(float.IsNaN(phaseDistance)||float.IsInfinity(phaseDistance)||
                phaseDistance<0f)
            {
                offset=raw;
                phaseDistance=0f;
            }
            if(commit)
            {
                companionGaitPhaseOwner=Game;
                companionGaitPhaseSave=Game.save;
                companionGaitPhaseId=id;
                companionGaitPhaseGeneration=Game.companionPresentationGeneration;
                companionGaitPhaseRoom=Game.save.room;
                companionGaitPhaseOffset=offset;
                companionGaitLastRawDistance=raw;
                companionGaitPhaseMoving=walking;
            }
            return phaseDistance;
        }

        private int CompanionStopPose(float seconds)
        {
            int from=(companionLastWalkFrame%4+4)%4;
            if(from==0||Game.save.reducedMotion)return 0;
            float interval=CurrentWorldVisualLook.companionFootfallSeconds;
            // A stop is not extra distance: do not cycle a stationary ally
            // through another airborne stride after its feet have planted.
            // Hold only the next source-painted contact (1->2, 3->0; phase 2
            // is already planted) for one bounded footfall interval, then
            // return to the first authored idle-contact cell.
            if(float.IsNaN(interval)||float.IsInfinity(interval)||
                interval<=.001f)interval=.075f;
            if(float.IsNaN(seconds)||float.IsInfinity(seconds)||
                seconds<0f||seconds>=interval)return 0;
            return from==1||from==2?2:0;
        }
        private void DrawCompanionActor()
        {
            Point p = ScreenPoint(Game.companionPosition); int id = Game.save.companion;
            if(id<0||id>=3)return;
            // The imported action atlas may be missing or partial while
            // THIS companion's accepted full-body Walk art remains intact.
            bool actionArtReady=companionSheets!=null&&
                id<companionSheets.Length&&
                ActorAtlasGeometry.IsAcceptedCompanionAtlas(companionSheets[id]);
            bool publishedWalk=companionWalkSheets!=null &&
                id<companionWalkSheets.Length&&
                companionWalkSheets[id]!=null&&
                companionWalkSheets[id].width==4*256&&
                companionWalkSheets[id].height==3*256;
            if(!actionArtReady&&!publishedWalk)
            {
                WarnMissingCompanionBody(id,"action and walk");
                return;
            }
            ActorAction action = Game.companionAction;
            if(action!=null&&!actionArtReady&&!drawingOutline)
                WarnMissingCompanionBody(id,"action; holding own Walk idle");
            Point direction = action == null ? Game.companionAim : action.direction;
            bool north = direction.y < -Math.Abs(direction.x), south = direction.y > Math.Abs(direction.x);
            // The 3x4 source atlas has distinct east, north and south
            // idle cells. If a richer Walk* atlas is unavailable during
            // import recovery, never face a south-looking companion east.
            int frame = north ? 8 : south ? 11 : 0;
            if (action != null)
                frame = north ? (action.released ? 7 : 6) : south ? (action.released ? 10 : 9) : action.released && (action.kind == "command" ||
                    action.kind == "heal") && id != 1 ? 5 :
                    action.released ? 4 : 3;
            DrawActorContactShadow(p,38,14,4+id,.77f);
            // The former 26x3 opaque mint Box sat ON TOP of finished boot
            // pixels. Use the existing feathered, high-quality Glow() source
            // beneath the full-body companion instead. Its single planted
            // pivot shows friendly affiliation and real command state
            // without masking feet, changing identity or animating physics.
            if (!drawingOutline)
            {
                float allyAlpha=action==null?.18f:action.released?.42f:.29f;
                Image(art.Glow(),p.x-24,p.y-6,48,12,
                    new Color(.14f,.72f,.57f,allyAlpha));
            }
            bool overlaps = Math.Abs(Game.companionPosition.x - Game.save.x) < .85f && Game.save.y <= Game.companionPosition.y && Game.save.y > Game.companionPosition.y - 1.8f;
            Color tint = overlaps ? new Color(1, 1, 1, .45f) : Color.white;
            // Companion models ALSO use the complete approved Sprite.
            // Retire the old five-part HumanActor neck/body reattachment
            // without changing companion AI, action phase, or facing logic.
            // A non-null Texture2D can still be a partial/wrong-size
            // import. Accept only the audited 4x3 grid of 256px cells;
            // otherwise fall back to the intact original action atlas.
            int row=GameCore.DirectionRow(direction);
            // The first genuine draw of a new character, game instance or
            // companion presentation generation must use THAT actor's own
            // direction. Previous smoothing inspected only numeric party ID
            // and could reuse a north-facing silhouette on a newly loaded
            // near-diagonal east walk, even after the generation was reset.
            bool samePresentation=companionVisualId==id&&
                System.Object.ReferenceEquals(companionVisualOwner,Game)&&
                companionVisualGeneration==Game.companionPresentationGeneration;
            // Avoid north/east sheet flip-flop at 45-degree pathfinding
            // boundaries, but NEVER preserve a south facing when heading
            // north (or the reverse). A distinct actual direction wins.
            bool sameHemisphere=companionLastWalkFacing==0||
                (companionLastWalkFacing==1&&direction.y<0f)||
                (companionLastWalkFacing==2&&direction.y>0f);
            if(action==null&&Game.companionMoving&&
                samePresentation&&sameHemisphere&&
                Math.Abs(Math.Abs(direction.x)-Math.Abs(direction.y))<
                    CurrentWorldVisualLook.companionFacingThreshold)
            {
                row=companionLastWalkFacing;
                // If the richer Walk* atlas is missing during recovery,
                // keep the base-sheet idle cell in the same smoothed facing.
                if(!publishedWalk)frame=row==1?8:row==2?11:0;
            }
            bool flip=row==0&&direction.x<-.1f;
            // Offscreen outline passes must never establish a new gait
            // owner or reset the actual body's running contact phase.
            if(!drawingOutline&&!samePresentation)
            {
                companionVisualOwner=Game;
                companionVisualGeneration=Game.companionPresentationGeneration;
                companionVisualId=id;
                companionWasWalking=false;
                companionLastWalkFrame=0;
                companionLastWalkFacing=row;
                companionStopTime=clock-.5f;
                companionTrackedAction=null;
                companionAttackStartedFromWalk=false;
            }
            if(!drawingOutline&&(!Game.companionMoving||action!=null))
            {
                // The production BODY also owns its stationary gait epoch.
                // Effects-quality 0/1 skips companion footfall visuals,
                // which previously left companionGaitPhaseMoving stuck TRUE
                // across idle/command frames. The next real stride then
                // resumed mid-air unless a cosmetic ground-decal hook ran.
                // Do not make painted legs depend on optional VFX quality.
                CompanionGaitDistance(true);
            }
            if(!drawingOutline&&action!=companionTrackedAction)
            {
                // An in-flight command is not the same as ordinary idle.
                // Keep one stance for its entire actual GameCore action;
                // drawingOutline or a second OnGUI pass must not restart it.
                companionAttackStartedFromWalk=action!=null&&
                    companionWasWalking;
                companionTrackedAction=action;
            }
            if(Game.companionMoving&&action==null&&publishedWalk)
            {
                // Gait is distance-locked to real companion feet, not an
                // unrelated FPS or decorative elapsed-time oscillator.
                // Slightly longer travel per 4-cell loop makes each source
                // leg-extension pose read as a full step instead of rapid
                // shuffling; it cannot cause in-place foot cycling.
                float stride=GameCore.WalkStride*
                    CurrentWorldVisualLook.companionStrideDistanceFactor;
                int step=GameCore.GaitFrame(
                    CompanionGaitDistance(!drawingOutline),stride);
                if(!drawingOutline)
                {
                    companionLastWalkFrame=step;
                    companionLastWalkFacing=row;
                    companionWasWalking=true;
                }
                Actor(companionWalkSheets[id],4,3,row*4+step,
                    p,192f,tint,flip);
            }
            else if(action==null&&publishedWalk)
            {
                // Idle is the same detailed authored WALK cell family for
                // EAST, NORTH and SOUTH. Old code switched E/N abruptly
                // into a different Companion sheet (only SOUTH was safe).
                // When stopping on an airborne key (1/3), briefly resolve
                // to its nearest planted contact (0/2). Never jump from an
                // airborne foot to an unrelated body/face in one repaint.
                if(companionWasWalking&&!drawingOutline)
                {
                    companionWasWalking=false;
                    companionStopTime=clock;
                }
                // A silhouette preview may run before or after the real
                // repaint. It samples the SAME next planted source cell
                // but does not consume the body-owned stop transition.
                int finish=companionLastWalkFacing==row?
                    CompanionStopPose(companionWasWalking?0f:
                        clock-companionStopTime):0;
                Actor(companionWalkSheets[id],4,3,
                    row*4+finish,p,192f,tint,flip);
            }
            else if(action!=null&&!actionArtReady&&publishedWalk)
            {
                // Only a planted, source-painted, same-identity full body
                // survives a temporary action-atlas import failure.
                // Combat and projectile authority remain in GameCore;
                // no fabricated contact sprite or old companion costume.
                if(!drawingOutline)companionWasWalking=false;
                Actor(companionWalkSheets[id],4,3,row*4,p,192f,tint,flip);
            }
            else if(action!=null&&!action.released&&
                    companionAttackStartedFromWalk&&
                    action.elapsed<Math.Min(
                        CurrentWorldVisualLook.companionAttackPlantSeconds,
                        action.contact*.42f)&&publishedWalk)
            {
                // PREVIOUS DEFECT: the 24%-opaque "boot afterimage" was
                // actually an ENTIRE prior whole-body Walk Sprite, including
                // a second head, torso, hands and sometimes visible bow.
                // The first cast/attack then had TWO overlapping actors,
                // creating a ghost waist/weapon flash at transition.
                // Preserve gait readability only with genuine source-painted
                // contact DIRT at the original planted foot. Exactly ONE
                // authored full-body action Sprite remains on the stage.
                if(!drawingOutline)companionWasWalking=false;
                float duration=Math.Min(
                    CurrentWorldVisualLook.companionAttackPlantSeconds,
                    action.contact*.42f);
                if(!Game.save.reducedMotion&&Game.save.effectsQuality>0&&
                   !drawingOutline&&effectSheet!=null&&duration>.001f)
                {
                    float fade=Mathf.Clamp01(1f-action.elapsed/duration);
                    string surface=FrontierAudio.FootSurface(
                        Game,Game.companionPosition);
                    float width=surface=="snow"?24f:26f;
                    float height=surface=="snow"?18f:13f;
                    int sourceCell=surface=="snow"?6:2;
                    Atlas(effectSheet,4,4,sourceCell,
                        new Rect(p.x-width*.5f,p.y-height*.58f,
                            width,height),
                        FootfallTint(surface,tint.a*.22f*fade),flip);
                }
                // The original action pose immediately replaces all older
                // body pixels. Combat clocks, aim and foot pivot are unchanged.
                Actor(companionSheets[id],3,4,frame,p,192f,tint,flip);
            }
            else
            {
                if(!drawingOutline)companionWasWalking=false;
                Actor(companionSheets[id],3,4,frame,p,192f,tint,flip);
            }
            // This is the navigation solver's actual unreachable-frontier
            // condition, never a timer-based guess from a paused walk pose.
            // Draw ABOVE the existing authored full-body sprite and omit in
            // silhouette/outline passes. Same text with reduced motion.
            if(!drawingOutline&&Game.CompanionRouteBlocked)
                Text(p.x-99,p.y-143,198,22,
                    Game.L("前方道路不通 · 请绕行",
                        "ROUTE BLOCKED · REROUTE"),
                    10,gold,TextAnchor.MiddleCenter);
            else if(!drawingOutline&&Game.CompanionPostRouteBlocked)
                Text(p.x-110,p.y-143,220,22,
                    Game.L("驻守路线受阻 · 原命令已保留",
                        "ALLY POST BLOCKED · ORDER KEPT"),
                    10,gold,TextAnchor.MiddleCenter);
            else if(!drawingOutline&&Game.CompanionHeroCoverInFlight)
                // A live automatic arrow is heading toward an imminent
                // hero attacker. Arrival is NOT guaranteed: collision,
                // stone, death and the enemy release clock still decide.
                Text(p.x-108,p.y-143,216,22,
                    Game.L("援射在途 · 命中才可打断",
                        "COVER SHOT · HIT TO INTERRUPT"),
                    10,mint,TextAnchor.MiddleCenter);
        }
        private void DrawHazardWarnings()
        {
            foreach(Hazard h in Game.hazards)
            {
                if(h.radius<=0f)continue;
                // Include edge-overlap hazards but skip warning canvases
                // completely outside the visible world. Damage continues
                // under GameCore regardless of presentation culling.
                float margin=h.radius*Tile+55f;
                if(!WorldDrawable(h.position.x,h.position.y,
                    margin,margin))continue;
                Color ink=HazardWarningColor(h);
                WorldVisualLook appearance=CurrentWorldVisualLook;
                Color warning=new Color(ink.r,ink.g,ink.b,
                    appearance.hazardRingAlpha);
                // 32 detached 4px debug dots are not a readable area-of-
                // effect circle. The same joined, source-independent thin
                // 36-segment stroke used for enemy telegraphs preserves
                // the exact radius on 1x/2x and ultrawide canvases.
                DrawWarningArc(h.position,h.radius,0f,
                    (float)(Math.PI*2),warning,36);
                Point p=ScreenPoint(h.position);
                Text(p.x-25,p.y-25,50,24,h.timer.ToString("0.0"),
                    13,new Color(ink.r,ink.g,ink.b,
                        appearance.hazardCountdownAlpha),
                    TextAnchor.MiddleCenter);
            }
        }
        private Color HazardWarningColor(Hazard hazard)
        { return hazard.glasswindOwned ? PixelArt.C("#ffb76c") : hazard.kind == "glass" ? gold : hazard.kind == "bell" ? PixelArt.C("#9bd8f5") : hazard.kind == "root" ? PixelArt.C("#82d9a3") : hazard.kind == "rift" ? PixelArt.C("#b9a1d1") : PixelArt.C("#d59583"); }
        private void DrawRescueSurvivorActor()
        {
            if (!Game.ReliefCivilianVisible ||
                !ActorAtlasGeometry.IsAcceptedNpcAtlas(npcSheet)) return;
            Point p = ScreenPoint(Game.reliefFollowerPosition);
            bool walking = Game.ReliefEscortActive && Game.reliefFollowerMoving &&
                !Game.save.reducedMotion;
            float cycle = Game.reliefFollowerWalkDistance * 8.2f;
            float walkLean=CurrentWorldVisualLook.escortWalkLeanDegrees;
            float lean = walking ? (float)Math.Sin(cycle) * walkLean : 0f;
            if (Game.ReliefEscortUnderAttack && !Game.save.reducedMotion)
                lean = -walkLean;
            bool justHit = Game.reliefCivilianHitTime > 0;
            if (justHit && !Game.save.reducedMotion)
                lean = -CurrentWorldVisualLook.escortHitLeanDegrees *
                    (float)GameCore.Clamp(
                    Game.reliefCivilianHitTime * 5f,0f,1f);
            DrawActorContactShadow(p,34,13,7,.78f);
            // The prior "bob + hit reaction" moved a SINGLE painted
            // civilian 3–5 px above her grounded shadow. A nonzero Actor
            // angle also forced the native GPU SpriteRenderer off into GUI
            // fallback, and rotated around canvas CENTRE rather than boots.
            // Both movement and injury must now keep their planted root.
            Color tint=justHit ? PixelArt.C("#ffe2bd") : Color.white;
            DrawRootedCivilianBody(p,lean,tint,Game.reliefFollowerFacingLeft);
            if(!drawingOutline &&
                (Game.ReliefEscortUnderAttack||justHit))
                DrawNpcActionSignal(p,1,14,1f);
        }

        // Shared by the production survivor draw and direct render QA.
        // Whole authored Npcs.png cell 14 (including hair, feet and clothes)
        // remains one Sprite; visual-only, no hitbox or save mutation.
        private void DrawRootedCivilianBody(Point foot,float lean,
            Color tint,bool flip)
        {
            if (!ActorAtlasGeometry.IsAcceptedNpcAtlas(npcSheet)) return;
            Matrix4x4 original=GUI.matrix;
            try
            {
                if(!Game.save.reducedMotion && Math.Abs(lean)>.001f)
                    GUI.matrix=GuiGeometry.RotateAroundPivot(original,
                        new Vector2(foot.x,foot.y),lean);
                // angle=0 means the SAME native SpriteRenderer-first path
                // as any other complete-body NPC, even while moving.
                Actor(npcSheet,4,4,14,foot,96,tint,flip);
            }
            finally {GUI.matrix=original;}
        }
        private void DrawExpansionWorld()
        {
            if(Game.mode=="world"&&Game.GlassLoomActive&&!drawingOutline)
            {
                Point target=Game.GlassLoomTarget;
                Color ink=Game.GlassLoomStage==3?mint:
                    Game.Night?new Color(.55f,.76f,1f,1f):gold;
                if(WorldDrawable(target.x,target.y,160f,160f))
                {
                    Ring(target,.94f,ink,30);
                    Point screen=ScreenPoint(target);
                    Text(screen.x-132,screen.y-90,264,21,
                        Game.GlassLoomObjective,11,ink,TextAnchor.MiddleCenter);
                }
                Text(35,416,520,24,Game.L("归光阵 · ","GLASSWELL · ")+
                    Game.GlassLoomStage+"/3 · "+
                    Game.GlassLoomTime.ToString("0")+"s · "+
                    (Game.Night?Game.L("真实夜间","NIGHT"):
                        Game.L("真实白昼","DAYLIGHT")),12,ink);
                if(Game.GlassLoomStage==1)
                    Text(35,440,535,24,
                        Game.L("三镜 ","LENSES ")+
                        Game.GlassLoomOrientation(0)+"—"+
                        Game.GlassLoomOrientation(1)+"—"+
                        Game.GlassLoomOrientation(2)+
                        Game.L(" · 目标 2—0—1"," · TARGET 2—0—1"),
                        12,ink);
                if(Game.GlassLoomBeamActive)
                {
                    // Actual daytime center-line light, with walkable
                    // adjacent sidesteps; never imply a map-wide aura.
                    Color beam=new Color(.97f,.82f,.44f,.82f);
                    for(int x=61;x<=75;x++)
                    {
                        Point glint=new Point(x+.5f,50.5f);
                        if(WorldDrawable(glint.x,glint.y,70f,70f))
                            Ring(glint,.18f,beam,12);
                    }
                    Text(35,465,530,23,
                        Game.L("正午强光照射中轴 · 从旁边道路绕行",
                            "SUNBEAM ON CENTER LANE · USE THE SIDE PATH"),
                        11,beam);
                }
                if(Game.GlassLoomStage>=2)
                {
                    Point[] ray={
                        new Point(60.5f,50.5f),new Point(64.5f,50.5f),
                        new Point(68.5f,50.5f),new Point(72.5f,50.5f),
                        new Point(76.5f,50.5f)};
                    foreach(Point lens in ray)
                        if(WorldDrawable(lens.x,lens.y,120f,120f))
                            Ring(lens,.29f,ink,14);
                }
            }

            // The civilian countertide beacon follows the actual original
            // Mistport sites and genuine live sea level, not a static HUD quest.
            if(Game.mode=="world"&&Game.CountertideActive&&!drawingOutline)
            {
                Point foot=Game.CountertideTarget;
                Color ink=Game.CountertideRemaining<30f?
                    new Color(1f,.57f,.37f,1f):
                    Game.LowTide?mint:gold;
                if(WorldDrawable(foot.x,foot.y,185f,185f))
                {
                    Ring(foot,.72f,ink,32);
                    Point p=ScreenPoint(foot);
                    Text(p.x-138,p.y-81,276,22,
                        Game.CountertideObjective,12,ink,
                        TextAnchor.MiddleCenter);
                }
                Point delta=foot-Game.Position;
                Text(35,346,510,23,
                    Game.L("逆潮航标 · ","COUNTERTIDE · ")+
                    Game.CountertideStage+"/4 · "+
                    Game.CountertideRemaining.ToString("0")+"s",
                    13,ink);
                Text(35,370,510,23,
                    Game.CountertideObjective+" · "+
                    Game.L("直线距离 ","BEARING ONLY ")+
                    delta.Length.ToString("0"),12,ink);
                Text(35,394,510,20,
                    (Game.LowTide?Game.L("当前低潮","CURRENT EBB"):
                        Game.L("当前高潮","CURRENT FLOOD"))+
                    Game.L(" · 下次转潮 "," · NEXT SHIFT ")+
                    Game.CountertideTideTurnSeconds.ToString("0.0")+"s"+
                    Game.L(" · 错音 "," · MISNOTES ")+
                    Game.CountertideMistakes+
                    Game.L(" · 潮谱 2-3-1"," · CODE 2-3-1"),
                    11,ink);
            }

            if(Game.mode=="world"&&Game.ThornwatchActive&&!drawingOutline)
            {
                Point target=Game.ThornwatchTarget;
                Color ink=Game.ThornwatchTime<25f?
                    new Color(1f,.55f,.40f,1f):mint;
                if(WorldDrawable(target.x,target.y,170f,170f))
                {
                    Ring(target,.75f,ink,32);
                    Point p=ScreenPoint(target);
                    Text(p.x-135,p.y-83,270,20,
                        Game.ThornwatchStepText,12,ink,
                        TextAnchor.MiddleCenter);
                }
                if(Game.ThornwatchGroundedSeconds>0f)
                {
                    Point camp=new Point(61.5f,36.5f);
                    if(WorldDrawable(camp.x,camp.y,150f,150f))
                        Ring(camp,1.1f,new Color(.46f,.86f,.91f,.78f),30);
                    Text(35,256,490,20,
                        Game.L("归途根钟已平息 · ","BELL GROUNDED · ")+
                        Game.ThornwatchGroundedSeconds.ToString("0.0")+"s",
                        12,mint);
                }
                if(Game.ThornwatchStage==3)
                {
                    if(Game.ThornwatchEliteEnraged)
                        Text(35,316,450,18,
                            Game.L("末段 · 避开三向根震后延迟爆发的中心！",
                                   "FINAL · SPOKES THEN DELAYED CENTER BLAST!"),
                            11,new Color(1f,.67f,.48f,1f));
                    Text(35,279,295,19,
                        Game.L("根缚精英 · ","ROOTBOUND ELITE · ")+
                        Game.ThornwatchEliteHealth.ToString("0")+"/"+
                        Game.ThornwatchEliteMaxHealth.ToString("0"),
                        12,ink);
                    Bar(35,302,270,
                        Game.ThornwatchEliteHealth/Game.ThornwatchEliteMaxHealth,
                        new Color(.73f,.48f,.86f,1f),5);
                }
                Text(35,231,485,23,Game.L("归途根钟 · ","ROOT BELL · ")+
                    Game.ThornwatchStage+"/4 · "+
                    Game.L("剩余 ","LEFT ")+Game.ThornwatchTime.ToString("0")+"s"+
                    Game.L(" · 敌人 "," · FOES ")+Game.ThornwatchSurvivors,
                    12,ink);
            }

            // Live three-site Last Lantern signal. The map marker follows
            // authentic objective position, not a fabricated static waypoint.
            if(Game.mode=="world"&&Game.LastLanternActive&&!drawingOutline)
            {
                Point target=Game.LastLanternObjective;
                Color signal=Game.LastLanternSeconds<25f?
                    new Color(1f,.53f,.4f,1f):gold;
                if(WorldDrawable(target.x,target.y,175f,175f))
                {
                    Ring(target,.70f,signal,30);
                    Point marker=ScreenPoint(target);
                    Text(marker.x-117f,marker.y-76f,234f,21f,
                        Game.LastLanternObjectiveText,11,signal,
                        TextAnchor.MiddleCenter);
                }
                Point delta=target-Game.Position;
                Text(35,114,500,24,
                    Game.L("最后的灯讯 · ","THE LAST LANTERN · ")+
                    Game.LastLanternStage+"/3 · "+
                    Game.LastLanternSeconds.ToString("0")+"s",14,signal);
                Text(35,138,500,23,
                    Game.LastLanternObjectiveText+" · "+
                    Game.L("直线距离 ","BEARING ONLY ")+
                    delta.Length.ToString("0")+
                    Game.L(" 格"," tiles"),12,signal);
            }
            // Real, paid field traps occupy the same terrain coordinates
            // that the original enemy AI uses for ground contacts.
            // Markers are screen projections, not a duplicated hero/actor.
            if(Game.mode=="world"&&!drawingOutline)
                foreach(FieldSnare snare in Game.FieldSnares)
                {
                    Point foot=snare.foot;
                    if(!WorldDrawable(foot.x,foot.y,100f,100f))continue;
                    Color tint=snare.arming>0f?gold:mint;
                    Ring(foot,.50f,tint,18);
                    Point p=ScreenPoint(foot);
                    Box(p.x-12,p.y-2,24,3,tint);
                    Box(p.x-11,p.y-7,3,12,tint);
                    Box(p.x+8,p.y-7,3,12,tint);
                    if((foot-Game.Position).Length<4.5f)
                        Text(p.x-70,p.y-48,140,18,
                            snare.arming>0f?
                            Game.L("安装中","ARMING"):
                            Game.L("绊索 · "+Math.Ceiling(snare.seconds)+"秒",
                                   "SNARE · "+Math.Ceiling(snare.seconds)+"s"),
                            10,tint,TextAnchor.MiddleCenter);
                }
            if(Game.mode=="world"&&Game.AfterglowActive&&!drawingOutline)
            {
                Point fire=Game.AfterglowCampFoot;
                Color ink=Game.AfterglowPhase==2?mint:
                    Game.AfterglowTime<=15f?
                    new Color(1f,.47f,.33f,1f):gold;
                if(WorldDrawable(fire.x,fire.y,240f,240f))
                {
                    Ring(fire,1f,ink,32);
                    Point screen=ScreenPoint(fire);
                    Text(screen.x-116f,screen.y-95f,232f,20f,
                        Game.AfterglowPhase==2?
                            Game.L("回营地选择战术","CHOOSE TACTIC AT CAMP"):
                            Game.L("余烬夜袭 · 守住营火","AFTERGLOW RAID · DEFEND"),
                        12,ink,TextAnchor.MiddleCenter);
                }
                Text(35,94,490,24,
                    Game.L("守望 ","WATCH ")+Game.AfterglowPhase+
                    "/3 · "+Game.L("敌人 ","FOES ")+Game.AfterglowRemaining+
                    (Game.AfterglowPhase==2?" · "+Game.L("选择战术","CHOOSE TACTIC"):
                    " · "+Game.AfterglowTime.ToString("0")+"s"),
                    13,ink);
            }
            // The Unlisted chapter tracks existing source-bound clues, not
            // guessed routes through walls or invented task landmarks.
            if(Game.mode=="world"&&
                (Game.UnlistedFieldObjectiveActive||
                 Game.UnlistedStewardObjectiveActive)&&!drawingOutline)
            {
                bool investigating=Game.UnlistedFieldObjectiveActive;
                Point objective=investigating?
                    Game.UnlistedFieldTarget:Game.UnlistedStewardFieldTarget;
                string objectiveText=investigating?
                    Game.UnlistedFieldObjective:Game.UnlistedStewardFieldObjective;
                if(WorldDrawable(objective.x,objective.y,170f,170f))
                {
                    Ring(objective,.65f,mint,30);
                    Point screen=ScreenPoint(objective);
                    Text(screen.x-114f,screen.y-85f,228f,19f,
                        objectiveText,11,mint,
                        TextAnchor.MiddleCenter);
                }
                Point bearing=objective-Game.Position;
                string east=Game.L(bearing.x>=0f?"东":"西",
                    bearing.x>=0f?"E":"W");
                string south=Game.L(bearing.y>=0f?"南":"北",
                    bearing.y>=0f?"S":"N");
                Text(35,177,480,23,
                    Game.L("未列名者 · ","THE UNLISTED · ")+
                    objectiveText,13,mint);
                Text(35,201,480,20,
                    Game.L("直线方位 ","BEARING ONLY ")+
                    east+Math.Abs(bearing.x).ToString("0")+" · "+
                    south+Math.Abs(bearing.y).ToString("0")+
                    " · "+bearing.Length.ToString("0")+
                    Game.L(" 格"," tiles"),11,mint);
            }
            // Scene-connected visual of the live paid field defense. These
            // tiny edge sparks describe real wall-clipped projectile reach;
            // they never impersonate a newly accepted painted Sprite.
            if(Game.mode=="world"&&Game.CampWardActive&&!drawingOutline)
            {
                Point fire=Game.CampWardFoot;
                if(WorldDrawable(fire.x,fire.y,300f,300f))
                {
                    Color flame=new Color(.98f,.76f,.39f,.74f);
                    for(int sector=0;sector<36;sector++)
                    {
                        double angle=sector*Math.PI*2/36;
                        Point edge=fire+new Point(
                            (float)Math.Cos(angle),
                            (float)Math.Sin(angle))*3.25f;
                        if(!Game.CampWardVisualSample(edge))continue;
                        Point screenEdge=ScreenPoint(edge);
                        Box(screenEdge.x-2f,screenEdge.y-2f,4f,4f,flame);
                    }
                    Point screen=ScreenPoint(fire);
                    Text(screen.x-95f,screen.y-84f,190f,19f,
                        Game.L("守火 ","FIREWARD ")+
                        Game.CampWardCharges+
                        Game.L(" 次 · "," HITS · ")+
                        Game.CampWardSeconds.ToString("0")+"s",
                        11,gold,TextAnchor.MiddleCenter);
                }
            }
            if(Game.mode=="world"&&Game.SignalRelayActive&&!drawingOutline)
            {
                Point target=Game.SignalRelayTarget;
                // One quest-owned target points to the original physical
                // lookout/camp; no straight-line fake path through terrain.
                if(WorldDrawable(target.x,target.y,140f,140f))
                {
                    Ring(target,.67f,new Color(.98f,.75f,.39f,.83f),28);
                    Point screen=ScreenPoint(target);
                    Text(screen.x-103f,screen.y-75f,206f,19f,
                        Game.SignalRelayStage==1?
                            Game.L("高地交接 · 按 E","RIDGE HANDOFF · E"):
                            Game.L("交还回执 · 按 E","RETURN REPLY · E"),
                        11,gold,TextAnchor.MiddleCenter);
                }
                Color packetInk=Game.SignalRelayIntegrity==1?
                    new Color(1f,.48f,.39f,1f):gold;
                Text(35,123,430,24,
                    Game.L(Game.SignalRelayStage==1?"前往高地路标":"返回原营地",
                        Game.SignalRelayStage==1?"TO RIDGE MARKER":"RETURN TO CAMP")+
                    " · "+Game.SignalRelayIntegrity+"/3 · "+
                    Game.SignalRelaySeconds.ToString("0")+"s",
                    13,packetInk);
                // A distant target can be entirely off camera. Display
                // honest straight-line bearings rather than inventing a
                // route segment through locked gates or stone walls.
                Point bearing=target-Game.Position;
                string east=Game.L(bearing.x>=0f?"东":"西",
                    bearing.x>=0f?"E":"W");
                string south=Game.L(bearing.y>=0f?"南":"北",
                    bearing.y>=0f?"S":"N");
                Text(35,150,430,20,
                    Game.L("直线方位 ","BEARING ONLY ")+
                    east+Math.Abs(bearing.x).ToString("0")+" · "+
                    south+Math.Abs(bearing.y).ToString("0")+
                    " · "+bearing.Length.ToString("0")+
                    Game.L(" 格"," tiles"),
                    11,mint);
            }
            // The wounded courier's parcel stays exactly where the real
            // post-armor combat blow dislodged it. No quest prop teleport,
            // imported sprite mutation, or saved secondary position.
            if(Game.mode=="world"&&Game.OuterDeliveryParcelDropped)
            {
                Point dropped=Game.OuterDeliveryParcelFoot;
                Point screen=ScreenPoint(dropped);
                bool available=Game.OuterDeliveryCanRecoverParcel;
                Color ink=available?mint:gold;
                Ring(dropped,.46f,ink,24);
                if(!drawingOutline)
                {
                    Image(art.Glow(),screen.x-27,screen.y-7,54,15,
                        new Color(.93f,.72f,.38f,.27f));
                    Box(screen.x-13,screen.y-23,26,12,
                        PixelArt.C("#e5d7ba"));
                    Box(screen.x-13,screen.y-18,26,3,
                        PixelArt.C("#b88055"));
                    Text(screen.x-106,screen.y-67,212,20,
                        available?Game.L("掉落药布 · 按 E 捡回",
                            "DRESSINGS · E TO RECOVER"):
                            Game.L("药布掉落位置 · 返回拾取",
                            "DROPPED DRESSINGS · RECOVER"),
                        11,ink,TextAnchor.MiddleCenter);
                }
            }
            // Chapter Five receiver defense is an ACTUAL physical stance
            // puzzle. Draw only the active NORMAL raider's committed lane:
            // heavy area slams cannot be intercepted this way, and nearby
            // idle NPCs must not paint fake threat beams. Both geometry
            // and green hero cue use the exact core-owned damage predicate.
            if(Game.mode=="world"&&Game.AfterlightWatchActive)
            {
                Enemy bodyguarded=Game.AfterlightWatchBodyguardedThreat;
                Enemy incoming=bodyguarded??
                    Game.AfterlightWatchIncomingStockThreat;
                if(incoming!=null&&incoming.kind==0)
                {
                    Point station=Game.AfterlightWatchReceiverFoot;
                    DrawRay(incoming.Position,station,
                        bodyguarded!=null?
                        new Color(.37f,.91f,.75f,.68f):
                        new Color(1f,.63f,.31f,.64f));
                    if(bodyguarded!=null)
                        Ring(Game.Position,.51f,mint,24);
                }
            }
            // A normal relief-watch raider's genuine locked melee attack
            // now visibly follows its physical source-to-cache corridor.
            // Hero in-line is green: ready to defend, NOT an early hit.
            if(Game.mode=="world"&&Game.OuterPatrolActive)
            {
                Enemy guarded=Game.OuterPatrolBodyguardedThreat;
                Enemy incoming=guarded??Game.OuterPatrolIncomingStockThreat;
                if(incoming!=null&&incoming.kind==0)
                {
                    DrawRay(incoming.Position,Game.OuterPatrolCacheFoot,
                        guarded!=null?
                            new Color(.37f,.91f,.75f,.68f):
                            new Color(1f,.63f,.31f,.64f));
                    if(guarded!=null)
                        Ring(Game.Position,.51f,mint,24);
                }
            }
            if(Game.ShadeBreezeSeconds>0f && !Game.ShadedInDesert)
            {
                Point screen=ScreenPoint(Game.Position);
                Text(screen.x-82,screen.y-118,164,19,
                    Game.L("绿洲余凉 ", "OASIS BREEZE ")+
                    Game.ShadeBreezeSeconds.ToString("0.0")+"s",
                    11,mint,TextAnchor.MiddleCenter);
            }
            if(Game.BrazierEmberSeconds>0f && !Game.LitBrazierRefuge)
            {
                // Real nonserialized climate protection during one short
                // retreat, not another fixed halo attached to the player.
                Point screen=ScreenPoint(Game.Position);
                Text(screen.x-82,screen.y-118,164,19,
                    Game.L("火盆余温 ", "EMBER WARMTH ")+
                    Game.BrazierEmberSeconds.ToString("0.0")+"s",
                    11,gold,TextAnchor.MiddleCenter);
            }
            if(Game.SwordLessonActive)
            {
                Point launcher=GameCore.SwordLessonLauncher;
                Point fire=ScreenPoint(launcher);
                Ring(launcher,.42f,gold,24);
                Box(fire.x-10,fire.y-32,20,26,PixelArt.C("#66798b"));
                Box(fire.x-17,fire.y-37,34,7,PixelArt.C("#c39d68"));
                Text(fire.x-70,fire.y-66,140,20,
                    Game.L("实弹发箭器","LIVE ARROW LAUNCHER"),
                    11,gold,TextAnchor.MiddleCenter);
                Point hero=ScreenPoint(Game.Position);
                Text(hero.x-79,hero.y-148,158,20,
                    Game.L("诺尔三箭 ","NOLL'S ARROWS ")+
                    Game.SwordLessonHits+"/3 · "+Game.SwordLessonTime.ToString("0")+"s",
                    12,gold,TextAnchor.MiddleCenter);
            }
            // The defensive skill has a genuine directional reaction, not
            // a passive immunity bubble. Always show its active front.
            if(Game.save.profession==0&&Game.save.weapon==0&&Game.guardTime>0)
            {
                Point hero=ScreenPoint(Game.Position);
                bool ready=Game.SwordGuardReady;
                Color tint=Game.deflectFlash>0?gold:ready?mint:muted;
                Ring(Game.Position,Game.deflectFlash>0?.96f:.68f,tint,26);
                Text(hero.x-85,hero.y+17,170,20,
                    Game.deflectFlash>0?Game.L("剑盾弹反！","GUARD REFLECT!"):
                    ready?Game.L("剑盾弹箭7 / 挡近战9","REFLECT 7 / PARRY 9"):
                    Game.L("剑盾防护 · 弹反冷却中","GUARD · REFLECT RECOVERY"),
                    11,tint,TextAnchor.MiddleCenter);
            }
            if(Game.FrostSignalActive)
            {
                // Before delivery the flame travels on the hero. Once a
                // real hearth accepts it, the published objective and visual
                // must remain at the STATION, not follow an empty-handed hero.
                Point emberFoot=Game.FrostSignalAtHearth?
                    Game.FrostSignalBankedFoot:Game.Position;
                Point screen=ScreenPoint(emberFoot);
                Color tint=Game.FrostSignalAtHearth?mint:
                    Game.FrostSignalFlame<25?PixelArt.C("#ef826b"):
                    Game.FrostSignalFlame<50?gold:mint;
                Ring(emberFoot,.58f,tint,22);
                Box(screen.x-10,screen.y-73,20,17,PixelArt.C("#895938"));
                Box(screen.x-4,screen.y-87,8,15,PixelArt.C("#f2b568"));
                Bar(screen.x-31,screen.y-94,62,
                    Game.FrostSignalFlame/100f,tint,6);
                Text(screen.x-78,screen.y-125,156,20,
                    Game.FrostSignalAtHearth?
                        Game.L("暖站已接火","HEARTH SECURED"):
                        Game.L("民用余火 ","CIVIC EMBER ")+
                            Game.FrostSignalTime.ToString("0")+"s",
                    12,tint,TextAnchor.MiddleCenter);
            }
            if(Game.MineConvoyActive)
            {
                Point cart=Game.MineCartPosition,screen=ScreenPoint(cart);
                Color color=Game.MineConvoyIntegrity<25?
                    PixelArt.C("#ed7d67"):Game.MineConvoyIntegrity<50?gold:mint;
                Box(screen.x-23,screen.y-28,46,25,PixelArt.C("#596878"));
                Box(screen.x-18,screen.y-40,36,13,PixelArt.C("#ad835e"));
                Box(screen.x-14,screen.y-47,28,8,PixelArt.C("#e2b16e"));
                Box(screen.x-22,screen.y-4,10,6,PixelArt.C("#394353"));
                Box(screen.x+12,screen.y-4,10,6,PixelArt.C("#394353"));
                Ring(cart,.55f,color,25);
                Bar(screen.x-27,screen.y+11,54,
                    Game.MineConvoyIntegrity/80f,color,6);
                // Stage four is an actual delivered cart awaiting the
                // player's physical registration, not a running escort.
                // The timer deliberately stops at the depot: never show
                // a misleading failure countdown above safely arrived NPCs.
                Text(screen.x-79,screen.y-94,158,20,
                    Game.MineConvoyStage==4?
                        Game.L("矿车到站 · 请登记","CART ARRIVED · REGISTER"):
                        Game.L("矿车护送 ","CART ESCORT ")+
                            Game.MineConvoyTime.ToString("0")+"s · "+
                            Game.MineConvoyStage+"/4",
                    12,gold,TextAnchor.MiddleCenter);
            }
            if(Game.BlackoutActive && Game.BlackoutFollowing!=0)
            {
                Point pos=Game.BlackoutFollowerPosition,screen=ScreenPoint(pos);
                DrawNpc("innGuest",pos,Game.BlackoutFollowing+11);
                Ring(pos,.42f,Game.BlackoutFollowerHealth<18?
                    PixelArt.C("#e27a78"):mint,18);
                Bar(screen.x-19,screen.y+9,38,
                    Game.BlackoutFollowerHealth/48f,mint,5);
            }
            if(Game.StormTuning)
            {
                Point pos=ScreenPoint(Game.Position);
                Text(pos.x-80,pos.y-126,160,20,
                    Game.L("星灯调谐 ","GRID TUNING ")+Game.StormTuneTime.ToString("0")+"s · "+
                    Game.StormTuneStep+"/3",12,gold,TextAnchor.MiddleCenter);
            }
            if(Game.BlackoutActive && !Game.save.reducedMotion)
            {
                Point p=ScreenPoint(Game.Position);
                Text(p.x-72,p.y-110,144,19,
                    Game.L("长夜撤离 "+Game.BlackoutTime.ToString("0")+"s",
                        "BLACKOUT "+Game.BlackoutTime.ToString("0")+"s"),
                    12,gold,TextAnchor.MiddleCenter);
            }
            if (Game.racing)
            {
                Point target = GameCore.RaceRoute[Game.raceCheckpoint]; Ring(target, .65f, gold, 20); Point p = ScreenPoint(target);
                Text(p.x - 20, p.y - 44, 40, 35, (Game.raceCheckpoint + 1).ToString(), 23, gold, TextAnchor.MiddleCenter);
                Box(430, 112, 420, 48, panel); Text(449, 124, 382, 30, Game.L("风旗竞速 ", "WIND RACE ") + Game.raceTime.ToString("0.0") + " / 30s   ·   " + Game.raceCheckpoint + "/5", 17, gold, TextAnchor.MiddleCenter);
            }
            if (Game.reliefMissionRoom != 0 &&
                Game.reliefMissionRoom == Game.save.room)
            {
                Point rescue = Game.reliefMissionWave == 0 ?
                    Game.FieldSurveyTarget(Game.save.room) : Game.reliefFollowerPosition;
                Point? shelter=Game.reliefMissionWave==3?
                    Game.ReliefEscortShelterWaypoint:null;
                if(Game.reliefMissionWave==3)
                {
                    if(shelter.HasValue)rescue=shelter.Value;
                    else foreach (WorldObject o in Game.Map.objects)
                        if (o.kind == "fieldstation" + Game.save.room)
                        { rescue = new Point(o.x, o.y); break; }
                }
                // A genuinely urgent detour and its journal/compass target
                // are now the SAME actual world waypoint. Town never shows
                // a phantom rescue marker while no mission is active.
                Ring(rescue,.55f,shelter.HasValue?gold:
                    Game.reliefMissionWave==3?mint:gold,20);
                Point rp=ScreenPoint(rescue);
                Text(rp.x-68,rp.y-65,136,24,
                    shelter.HasValue?Game.L("先带居民避难","ESCORT SHELTER"):
                    Game.reliefMissionWave==3?Game.L("护送返程","ESCORT HOME"):
                    Game.reliefMissionWave==0?Game.L("寻找居民","FIND CIVILIANS"):
                    Game.L("守护居民","DEFEND CIVILIANS"),
                    12,shelter.HasValue?gold:mint,TextAnchor.MiddleCenter);
                if (Game.reliefMissionWave >= 1 && Game.reliefMissionWave <= 2)
                {
                    if ((Game.Position - rescue).Length < 1.7f &&
                        Game.ReliefFieldGuardAvailable)
                        Text(rp.x - 72, rp.y - 94, 144, 23,
                            ControlKey("E", "B") + Game.L(" · 紧急护卫", " · Rally guard"),
                            11, gold, TextAnchor.MiddleCenter);
                    if (Game.reliefWardTime > 0)
                    {
                        Ring(rescue, .8f, mint, 24);
                        Text(rp.x - 40, rp.y - 103, 80, 22,
                            Game.L("护卫 ", "GUARD ") +
                                Game.reliefWardTime.ToString("0.0") + "s",
                            11, mint, TextAnchor.MiddleCenter);
                    }
                    Color protection = Game.reliefFollowerSafety < 30 ? PixelArt.C("#f27d7d") :
                        Game.reliefFollowerSafety < 65 ? gold : mint;
                    Bar(rp.x - 33, rp.y + 10, 66,
                        Game.reliefFollowerSafety / 100f, protection, 6);
                    Text(rp.x - 42, rp.y + 17, 84, 18,
                        Game.L("安全 ", "SAFE ") + (int)Math.Ceiling(Game.reliefFollowerSafety) + "%",
                        10, protection, TextAnchor.MiddleCenter);
                }
            }
            if (Game.ReliefEscortActive)
            {
                Point escort = Game.reliefFollowerPosition;
                if(Game.ReliefCompanionArrowGuardPositioned)
                {
                    // World-space readiness must come from the ORIGINAL
                    // planted companion foot, never a decorative shield at
                    // the civilian or a passive invulnerability indicator.
                    bool ready=Game.ReliefCompanionArrowGuardReady;
                    Point blocker=Game.companionPosition;
                    Ring(blocker,.4f,ready?mint:gold,20);
                    Point badge=ScreenPoint(blocker);
                    Text(badge.x-58,badge.y-104,116,22,
                        ready?Game.L("挡箭就绪","ARROW GUARD"):
                            Game.L("挡箭冷却 ","GUARD IN ")+
                                Game.ReliefCompanionArrowGuardCooldown.ToString("0.0")+"s",
                        11,ready?mint:gold,TextAnchor.MiddleCenter);
                }
                // Sprite is depth-sorted with the world, not drawn over hero.
                Ring(escort, .35f, mint, 16);
                if (Game.reliefClimateKitTime > 0)
                {
                    Ring(escort, 1.1f, PixelArt.C("#7dc8d4"), 24);
                    Point weatherHud = ScreenPoint(escort);
                    Text(weatherHud.x - 45, weatherHud.y - 150, 90, 20,
                        Game.L("气候防护 ", "SHELTER ") +
                            Game.reliefClimateKitTime.ToString("0.0") + "s",
                        11, mint, TextAnchor.MiddleCenter);
                }
                if (Game.reliefHurryTime > 0)
                {
                    Point pace = ScreenPoint(escort);
                    Text(pace.x - 41, pace.y - 130, 82, 20,
                        Game.L("急行 ", "HURRY ") +
                            Game.reliefHurryTime.ToString("0.0") + "s",
                        11, gold, TextAnchor.MiddleCenter);
                }
                if (Game.reliefWardTime > 0)
                {
                    Ring(escort, .88f, mint, 24);
                    Point guardScreen = ScreenPoint(escort);
                    Text(guardScreen.x - 44, guardScreen.y - 110, 88, 23,
                        Game.L("护卫 ", "GUARD ") + Game.reliefWardTime.ToString("0.0") + "s",
                        11, mint, TextAnchor.MiddleCenter);
                }
                Point escortScreen = ScreenPoint(escort);
                if(Game.ReliefEscortRouteStalled)
                    Text(escortScreen.x-106,escortScreen.y-224,212,22,
                        Game.L("居民通路堵塞 · 请绕路",
                            "SURVIVOR PATH BLOCKED"),
                        11,PixelArt.C("#f4b46f"),TextAnchor.MiddleCenter);
                if(Game.ReliefEscortReturnObstructed)
                    Text(escortScreen.x-100,escortScreen.y-202,200,22,
                        Game.L("站台通路受阻 · 请绕到开放侧",
                            "STATION PATH BLOCKED · FIND OPEN SIDE"),
                        11,PixelArt.C("#f4b46f"),TextAnchor.MiddleCenter);
                // The green physical cover really restores civilian safety.
                // A stationed shelter with a hostile closing in is not a
                // secure resting stop: distinguish both states on the
                // actual moving NPC, not on the player HUD.
                if (Game.ReliefEscortSecureStop)
                    Text(escortScreen.x-88,escortScreen.y-174,176,20,
                        Game.L("掩体驻留 · 安全恢复", "SHELTERED REST · SAFE +"),
                        11,mint,TextAnchor.MiddleCenter);
                else if (Game.ReliefEscortSheltered)
                    Text(escortScreen.x-85,escortScreen.y-174,170,20,
                        Game.L("居民已进入避难区", "CIVILIANS SHELTERED"),
                        11,mint,TextAnchor.MiddleCenter);
                else if (Game.reliefClimateKitTime<=0)
                    Text(escortScreen.x-85,escortScreen.y-174,170,20,
                        Game.L("居民正暴露于恶劣天气", "EXPOSED TO WEATHER"),
                        11,gold,TextAnchor.MiddleCenter);
                if (Game.ReliefEscortUnderAttack)
                {
                    Text(escortScreen.x - 60, escortScreen.y - 89, 120, 26,
                        Game.L("警戒！来袭", "INCOMING"), 11, gold,
                        TextAnchor.MiddleCenter);
                    if (Game.ReliefEscortGap < 1.7f &&
                        Game.ReliefEscortEvadeAvailable)
                        Text(escortScreen.x-100,escortScreen.y-126,200,23,
                            ControlKey("E", "B")+
                                Game.L(" · 指挥闪避 · 耐力15",
                                    " · ORDER EVASION · 15 ENERGY"),
                            11,mint,TextAnchor.MiddleCenter);
                }
                else if (Game.ReliefEscortGap < 1.7f)
                    Text(escortScreen.x - 60, escortScreen.y - 89, 120, 26,
                        ControlKey("E", "B") + Game.L(" · 指挥 / 敷药", " · Command / care"),
                        11, gold, TextAnchor.MiddleCenter);
                else
                    Text(escortScreen.x - 50, escortScreen.y - 84, 100, 22,
                        Game.L(Game.reliefFollowerWaiting ? "等待接应" : "获救居民",
                            Game.reliefFollowerWaiting ? "WAITING" : "SURVIVORS"),
                        11, mint, TextAnchor.MiddleCenter);
                if (Game.reliefFollowerWaiting)
                    Text(escortScreen.x - 22, escortScreen.y - 99, 44, 20,
                        "II", 15, gold, TextAnchor.MiddleCenter);
                Color safetyColor = Game.reliefFollowerSafety < 30 ? PixelArt.C("#f27d7d") : mint;
                Bar(escortScreen.x - 30, escortScreen.y + 10, 60,
                    Game.reliefFollowerSafety / 100f, safetyColor, 6);
            }
            if (Game.save.room == 7 && !Game.save.reducedMotion && Game.save.effectsQuality > 0)
                for (int i = 0; i < 28; i++) { float x = (i * 91 + clock * 19) % Width, y = (i * 67 + clock * 32) % Height; Box(x, y, 3, 3, new Color(.9f, .96f, 1, .58f)); }
        }
        private bool DrawExpansionObject(WorldObject o)
        {
            string kind = o.kind; Point p = ScreenPoint(new Point(o.x, o.y));
            // Overlay proof state onto the EXISTING source artwork without
            // replacing any sprites, pivots or the world base renderer.
            // The permanent six-floor archive reveals all available notes,
            // while new adventurers see the closest guided record only.
            int recordState = Game.AbyssTestimonyRecordState(o);
            if (recordState != 0)
            {
                WorldObject selectedRecord = Game.AbyssTestimonyGuidedSite;
                bool selected = System.Object.ReferenceEquals(selectedRecord, o);
                bool closest = selectedRecord == null ?
                    System.Object.ReferenceEquals(Game.AbyssTestimonySuggestedSite,o) :
                    selected;
                if (recordState == 2)
                {
                    Ring(new Point(o.x,o.y),.47f,mint,14);
                    Text(p.x-53,p.y-84,106,19,
                        Game.L("证词已归档 ✓","ACCOUNT FILED ✓"),
                        10,mint,TextAnchor.MiddleCenter);
                }
                else if (recordState == 3)
                {
                    Ring(new Point(o.x,o.y),.45f,muted,14);
                    Text(p.x-70,p.y-88,140,19,
                        Game.L("先调查南侧残碑","READ SOUTH TABLET FIRST"),
                        9,muted,TextAnchor.MiddleCenter);
                }
                else if (closest || Game.AbyssTestimonyLegacyComplete)
                {
                    Color marker = closest ? gold : muted;
                    Ring(new Point(o.x,o.y),closest?.86f:.58f,
                        marker,closest?20:16);
                    Text(p.x-66,p.y-97,132,21,
                        selected ? Game.L("追踪证词 · E","TRACKED · E") :
                        closest ? Game.L("证词目标 · E","TESTIMONY · E") :
                            Game.L("待归档证词","UNFILED ACCOUNT"),
                        11,marker,TextAnchor.MiddleCenter);
                }
                // Fall through so the original high-fidelity object
                // texture still renders through the canonical draw path.
            }
            if(kind=="frostsignal")
            {
                int stage=0;
                for(int i=1;i<=3;i++)if(o.id=="frostsignal_"+i)stage=i;
                bool complete=Game.FrostSignalComplete;
                bool bright=complete&&Game.FrostSignalHearthBright;
                bool active=Game.FrostSignalActive&&stage==Game.FrostSignalStage;
                Color tint=bright?gold:complete?mint:active?gold:muted;
                Box(p.x-17,p.y-31,34,27,PixelArt.C("#736662"));
                Box(p.x-10,p.y-41,20,12,PixelArt.C("#b27b51"));
                if(complete||active)Box(p.x-4,p.y-54,8,13,
                    bright?PixelArt.C("#ffe3a1"):PixelArt.C("#edb76a"));
                // A steady gold halo is an immediately readable upgrade cue,
                // including with reduced motion. The accepted world art and
                // original physical hearth anchor remain unchanged.
                if(bright)Ring(new Point(o.x,o.y),.67f,gold,22);
                Ring(new Point(o.x,o.y),active?.84f:.55f,tint,22);
                Text(p.x-82,p.y-81,164,20,
                    bright?Game.L("公共暖站火势稳定","STEADY PUBLIC FIRE"):
                    complete?Game.L("公共暖站已重燃","PUBLIC FIRE RESTORED"):
                    active&&stage==2?Game.L("警戒 · 解决守卫","ALERT · CLEAR GUARDS"):
                    active&&stage==3?Game.L("交付火种 · 选择","DELIVER EMBER · CHOOSE"):
                    active?Game.L("带着火种前进","CARRY THE EMBER"):
                    stage==1?Game.L("旧西部守灯岗","WESTERN SIGNAL"):
                    Game.L("未点燃的公共火堆","UNLIT PUBLIC HEARTH"),
                    11,tint,TextAnchor.MiddleCenter);
                return true;
            }
            if(kind=="mineconvoy")
            {
                int phase=0;
                for(int i=1;i<=3;i++)if(o.id=="mineconvoy_"+i)phase=i;
                bool done=Game.MineConvoyComplete,active=Game.MineConvoyActive;
                bool next=active&&(phase==1&&Game.MineConvoyStage==1||
                    phase==2&&Game.MineConvoyStage==2||
                    phase==3&&Game.MineConvoyStage==4);
                Color color=done?mint:next?gold:muted;
                Box(p.x-18,p.y-32,36,28,PixelArt.C("#596a73"));
                Box(p.x-13,p.y-39,26,8,PixelArt.C("#b79a79"));
                Ring(new Point(o.x,o.y),next?.73f:.55f,color,23);
                Text(p.x-82,p.y-77,164,20,
                    done?Game.L("矿工安全归来","WORKERS SAFE"):
                    next&&phase==2?Game.L("击退袭击者 · 转轨","DEFEAT RAIDERS · SWITCH"):
                    next&&phase==3?Game.L("矿车已到站 · 登记","CART ARRIVED · REGISTER"):
                    next?Game.L("继续护送","KEEP ESCORTING"):
                    phase==1?Game.L("矿车发车岗","CART DISPATCH"):
                    Game.L("矿工安全矿棚","MINERS' SHELTER"),
                    11,color,TextAnchor.MiddleCenter);
                return true;
            }
            if(kind=="floodarchive")
            {
                int stage=0;
                for(int i=1;i<=3;i++)if(o.id=="floodarchive_"+i)stage=i;
                bool complete=Game.FloodArchiveComplete;
                bool recorded=complete||stage>0&&stage<=Game.FloodArchiveProgress;
                bool current=stage==Game.FloodArchiveProgress+1&&Game.Has("scout_saved");
                float wait=Game.FloodArchiveWait(stage);
                bool ready=current&&wait<=0;
                // The same actual world marker now reflects the live parcel
                // and physical two-raider crossing. Avoid the old misleading
                // "ferry open" sign while wounded civilians await delivery.
                bool medicine=Game.FloodReliefActive&&
                    (stage==1&&!Game.FloodReliefCrossed||
                     stage==3&&Game.FloodReliefCrossed);
                bool clearForHandoff=medicine&&Game.FloodReliefSecured;
                Color tint=medicine?
                    (clearForHandoff?mint:Game.FloodReliefIntegrity<=25?
                     PixelArt.C("#fa9d83"):gold):
                    recorded?mint:ready?PixelArt.C("#8de3f0"):current?gold:muted;
                Box(p.x-15,p.y-38,30,34,PixelArt.C("#3a6179"));
                Box(p.x-22,p.y-43,44,8,PixelArt.C("#7d9cae"));
                Ring(new Point(o.x,o.y),medicine?1.02f:ready?.94f:.63f,tint,24);
                Text(p.x-79,p.y-77,158,20,
                    Game.FloodReliefRewardPending&&stage==3?
                        Game.L("东岸医药奖励 · 待领取","EAST RECEIPT · CLAIM"):
                    medicine?stage==1?
                        Game.L("急救包已装船 · 立即渡河","MEDICINE LOADED · BOARD"):
                        clearForHandoff?
                            Game.L("拦截者已败 · 交付药品","RAIDERS DOWN · DELIVER"):
                            Game.L("东岸拦截战 · 已击败","EAST BANK RAIDERS · DEFEATED")+
                                " "+Game.FloodReliefDefeated+"/2":
                    complete&&(stage==1||stage==3)?
                        (Game.LowTide?Game.L("低潮渡船 · 可通行","LOW TIDE · FERRY OPEN"):
                                      Game.L("渡船 · 等待退潮","FERRY · WAIT LOW")):
                    recorded?Game.L("已记录的民用档案","CIVIC RECORD FOUND"):
                    current&&stage==1?ready?Game.L("涨潮水尺 · 可读取","HIGH WATER · READ"):
                                          Game.L("涨潮水尺 · 等潮","GAUGE · WAIT HIGH"):
                    current&&stage==2?ready?Game.L("退潮名册 · 可读取","LOW TIDE · READ"):
                                          Game.L("退潮名册 · 等潮","ROSTER · WAIT LOW"):
                    current?Game.L("公开渡口 · 选择","PUBLIC FERRY · CHOOSE"):
                            Game.L("尚未解锁","LOCKED"),11,tint,TextAnchor.MiddleCenter);
                if(medicine)
                {
                    Text(p.x-91,p.y-97,182,19,
                        Game.L("封条 ","CARGO ")+
                        (int)Math.Ceiling(Game.FloodReliefIntegrity)+"% · "+
                        (int)Math.Ceiling(Game.FloodReliefTime)+"s",
                        11,tint,TextAnchor.MiddleCenter);
                    if(stage==3&&Game.FloodReliefWardTime>0)
                        Text(p.x-91,p.y-115,182,18,
                            Game.L("药品护盾 ","PARCEL WARD ")+
                            (int)Math.Ceiling(Game.FloodReliefWardTime)+"s",
                            10,mint,TextAnchor.MiddleCenter);
                }
                else if(complete&&(stage==1||stage==3))
                    Text(p.x-77,p.y-97,154,18,
                        (Game.LowTide?Game.L("距涨潮 ","HIGH IN "):
                                      Game.L("距退潮 ","LOW IN "))+
                        (int)Math.Ceiling(Game.PublicFerryPhaseSeconds)+"s",
                        11,tint,TextAnchor.MiddleCenter);
                else if(current&&stage<=2&&!ready)
                    Text(p.x-38,p.y-96,76,18,wait.ToString("0")+"s",12,gold,TextAnchor.MiddleCenter);
                return true;
            }
            if(kind=="rootsonestone")
            {
                int stage=0;
                for(int i=1;i<=3;i++)if(o.id=="rootsong_"+i)stage=i;
                bool done=stage>0&&Game.RootsongProgress>=stage;
                bool next=stage>0&&Game.Has("scout_saved")&&Game.RootsongProgress+1==stage;
                bool green=next&&Game.RootsongOpen(stage);
                Color tint=done?mint:green?PixelArt.C("#86eba7"):next?gold:muted;
                Box(p.x-8,p.y-39,16,36,PixelArt.C("#395a45"));
                Box(p.x-14,p.y-46,28,10,PixelArt.C("#718c70"));
                Ring(new Point(o.x,o.y),green?.91f:.66f,tint,25);
                Text(p.x-77,p.y-79,154,21,
                    done?Game.L("古根 · 已归档","ROOT RESTORED"):
                    green?Game.L("根鸣泛绿 · 现在调谐","GREEN · TUNE NOW"):
                    next?Game.L("等待绿光","WAIT FOR GREEN"):
                         Game.L("沉寂的古根","SILENT ROOT"),11,tint,TextAnchor.MiddleCenter);
                if(next&&!green)
                    Text(p.x-39,p.y-99,78,18,
                        Game.RootsongWait(stage).ToString("0.0")+"s",
                        12,gold,TextAnchor.MiddleCenter);
                return true;
            }
            if(kind=="hearingdais")
            {
                Color tint=Game.HearingComplete?mint:
                    Game.Has("hearing_started")?gold:muted;
                PropImage("hearingdais",Game.HearingVerdict,p.x-37,p.y-82,74,90,
                    Game.HearingComplete?Color.white:muted);
                Ring(new Point(o.x,o.y),Game.HearingComplete?.95f:.7f,tint,28);
                Text(p.x-82,p.y-107,164,21,
                    Game.HearingComplete?Game.L("公民公约已公布","CIVIC VERDICT RATIFIED"):
                    Game.L("第七章 · 公开听证","CHAPTER SEVEN · HEARING"),
                    11,tint,TextAnchor.MiddleCenter);
                if(Game.Has("hearing_started") && !Game.HearingComplete)
                    Text(p.x-61,p.y-126,122,20,
                        Game.HearingSignatures+"/3 "+Game.L("署名","SIGNED"),
                        12,tint,TextAnchor.MiddleCenter);
                return true;
            }
            if(kind.StartsWith("hearingplaque"))
            {
                int region;
                if(!int.TryParse(kind.Substring("hearingplaque".Length),out region) ||
                    region<1 || region>3)return true;
                Color tint=Game.HearingVoice(region)==1?mint:gold;
                PropImage("hearingplaque",region,p.x-28,p.y-71,56,76,Color.white);
                Ring(new Point(o.x,o.y),.82f,tint,23);
                Text(p.x-80,p.y-92,160,20,
                    Game.L(new[]{"莫芽 · 活根誓言","阿菱 · 河岸誓言","维恩 · 工房誓言"}[region-1],
                        new[]{"MOYA · ROOT OATH","ARIN · RIVER OATH","VENN · FOUNDRY OATH"}[region-1]),
                    11,tint,TextAnchor.MiddleCenter);
                return true;
            }
            if(kind=="shrine" && Game.save.room==4 &&
                Game.StormChapterComplete)
            {
                Color tint=Game.HearingOrrinResponsibility!=0?mint:
                    Game.HearingComplete?gold:
                    Game.Has("storm_orrin_open")||Game.Has("storm_orrin_guard")?
                    mint:gold;
                PropImage("stormlamp",0,p.x-30,p.y-76,60,79,Color.white);
                Ring(new Point(o.x,o.y),.78f,tint,26);
                Text(p.x-81,p.y-99,162,22,
                    Game.HearingOrrinResponsibility==1?
                        Game.L("厄洛 · 公民医护责任","ORRIN · CIVIC MEDICS"):
                    Game.HearingOrrinResponsibility==2?
                        Game.L("厄洛 · 公共巡路责任","ORRIN · PUBLIC ROADWARDENS"):
                    Game.HearingComplete?
                        Game.L("厄洛 · 请兑现公约","ORRIN · ANSWER THE CHARTER"):
                    Game.Has("storm_orrin_open")?
                        Game.L("厄洛 · 公共避难之光","ORRIN · OPEN REFUGE"):
                    Game.Has("storm_orrin_guard")?
                        Game.L("厄洛 · 民用应急灯","ORRIN · CIVILIAN BACKUP"):
                        Game.L("厄洛 · 余量协议","ORRIN · CIVIC RESERVE"),
                    11,tint,TextAnchor.MiddleCenter);
                return true;
            }
            if(kind=="stormcouncil")
            {
                Color tint=Game.StormChapterComplete?mint:
                    Game.Has("storm_started")?gold:muted;
                PropImage("witnessledger",0,p.x-30,p.y-70,60,73,Color.white);
                Ring(new Point(o.x,o.y),Game.StormTuning?1.12f:.74f,tint,27);
                Text(p.x-78,p.y-91,156,20,
                    Game.StormChapterComplete?Game.L("星雨公约生效","STORM CHARTER IN EFFECT"):
                    Game.L("第六章 · 星雨议事","FIRST STORM COUNCIL"),
                    11,tint,TextAnchor.MiddleCenter);
                if(Game.Has("storm_started")&&!Game.StormChapterComplete)
                    Text(p.x-78,p.y-110,156,19,
                        Game.StormBudgetSpent+"/8 · "+Game.StormAllocations+"/3",
                        12,tint,TextAnchor.MiddleCenter);
                return true;
            }
            if(kind.StartsWith("stormrelay"))
            {
                int region;
                if(!int.TryParse(kind.Substring("stormrelay".Length),out region)||
                    region<1||region>3)return true;
                int policy=Game.StormPolicy(region);
                Color tint=policy==1?mint:policy==2?PixelArt.C("#b5cce5"):
                    Game.Has("storm_started")?PixelArt.C("#eaa381"):muted;
                PropImage("stormrelay",region,p.x-30,p.y-73,60,75,Color.white);
                Ring(new Point(o.x,o.y),policy!=0?.95f:1.17f,tint,23);
                Text(p.x-76,p.y-94,152,19,
                    policy==1?Game.L("公共庇护已通电","SHELTER PROTECTED"):
                    policy==2?Game.L("公共供能已通电","UTILITIES PROTECTED"):
                    Game.L("星雨配电未稳定","STORM GRID UNSTABLE"),
                    11,tint,TextAnchor.MiddleCenter);
                return true;
            }
            if(kind.StartsWith("stormnode"))
            {
                int region;
                if(!int.TryParse(kind.Substring("stormnode".Length),out region)||
                    region<1||region>3)return true;
                Color tint=Game.StormChapterComplete||Game.Has("storm_tuned")?mint:
                    Game.StormTuning&&Game.StormNextNode==region?gold:muted;
                PropImage("stormlamp",region,p.x-27,p.y-74,54,78,Color.white);
                Ring(new Point(o.x,o.y),Game.StormTuning&&Game.StormNextNode==region?1.1f:.65f,tint,23);
                Text(p.x-74,p.y-94,148,20,
                    Game.StormTuning&&Game.StormNextNode==region?
                        Game.L("下一个节点 · 按E","NEXT GRID NODE · E"):
                    Game.StormChapterComplete?
                        Game.L("公共星灯运行中","CIVIC LAMP ONLINE"):
                        Game.L("公共星灯","CIVIC LAMP"),
                    11,tint,TextAnchor.MiddleCenter);
                return true;
            }
            if(kind.StartsWith("afterlightwaypoint"))
            {
                int region;
                if(!int.TryParse(kind.Substring("afterlightwaypoint".Length),out region) ||
                    region<1||region>3)return true;
                PropImage("afterlightanchor",region,p.x-26,p.y-69,52,69,Color.white);
                Ring(new Point(o.x,o.y),.67f,mint,26);
                Text(p.x-78,p.y-91,156,22,
                    Game.L(new[]{"密林公共星灯","遗迹公共星灯","矿场公共星灯"}[region-1],
                        new[]{"GROVE STARROAD","RUINS STARROAD","MINE STARROAD"}[region-1]),
                    11,mint,TextAnchor.MiddleCenter);
                return true;
            }
            if(kind=="afterlightcouncil")
            {
                Color tint=Game.Has("afterlight_complete")?mint:
                    Game.Has("afterlight_started")?gold:muted;
                PropImage("witnessledger",0,p.x-31,p.y-67,62,72,Color.white);
                Ring(new Point(o.x,o.y),Game.Has("afterlight_complete")?.95f:.65f,tint,27);
                Text(p.x-78,p.y-90,156,22,
                    Game.Has("afterlight_complete")?Game.L("三地重建公约","RECONSTRUCTION CHARTER"):
                    Game.L("战后重建议事台","AFTERLIGHT COUNCIL"),
                    11,tint,TextAnchor.MiddleCenter);
                return true;
            }
            if(kind=="afterlightanchor"+Game.save.room ||
                kind=="afterlightoutlet"+Game.save.room)
            {
                int region=Game.save.room;
                bool anchor=kind=="afterlightanchor"+region;
                bool complete=Game.Has("afterlight_done_"+region);
                bool active=Game.AfterlightChargeRegion==region;
                bool reserve=Game.AfterlightReserve(region);
                bool civicFight=!anchor&&Game.AfterlightWatchActive;
                bool defended=Game.AfterlightWatchDone(region);
                Color tint=civicFight?
                    (Game.AfterlightWatchStock<=30?PixelArt.C("#f17368"):gold):
                    complete?(!anchor&&Game.AfterlightComplete&&!reserve?gold:mint):
                    active?PixelArt.C("#f1d39a"):
                    Game.AfterlightMode(region)!=0?gold:muted;
                PropImage(anchor?"afterlightanchor":"afterlightoutlet",region,
                    p.x-32,p.y-78,64,83,complete?Color.white:muted);
                Ring(new Point(o.x,o.y),complete?.9f:active?1.2f:.62f,tint,22);
                Text(p.x-79,p.y-104,158,22,
                    civicFight?Game.L("公共储备防卫中","CIVIC DEFENSE ACTIVE"):
                    complete?Game.L(anchor?"公共原始锚点":
                            defended?"巡路防卫已完成":reserve?"应急储备已备妥":"公共设施运转中",
                        anchor?"CIVIC ROOT ANCHOR":
                            defended?"CIVIC ROUTE GUARDED":
                            reserve?"RESERVE STOCKED":"PUBLIC SERVICE ONLINE"):
                    active?Game.L(anchor?"余光输送中":"稳定负载待接通",
                        anchor?"TRANSFER ACTIVE":"CHARGE ARRIVING"):
                    Game.L(anchor?"旧星根供能锚点":"断开的公共接收台",
                        anchor?"OLD STARROOT ANCHOR":"CIVIC RECEIVER"),
                    11,tint,TextAnchor.MiddleCenter);
                if(active)
                    Text(p.x-47,p.y-122,94,19,
                        Game.AfterlightChargeLeft.ToString("0")+"s",
                        12,tint,TextAnchor.MiddleCenter);
                if(civicFight)
                    Text(p.x-79,p.y-123,158,18,
                        Game.AfterlightWatchWave+"/2 · "+Math.Ceiling(Game.AfterlightWatchTime)+"s · "+
                            Math.Ceiling(Game.AfterlightWatchStock)+"%",
                        11,tint,TextAnchor.MiddleCenter);
                else if(complete&&!anchor&&Game.AfterlightComplete&&!reserve)
                    Text(p.x-79,p.y-123,158,18,
                        Game.L("应急物资待补充 · E","STOCK SUPPLIES · E"),
                        10,gold,TextAnchor.MiddleCenter);
                else if(complete&&!anchor&&reserve&&!defended)
                    Text(p.x-79,p.y-123,158,18,
                        Game.L("两波防卫待检验 · E","DEFEND THE RESERVE · E"),
                        10,mint,TextAnchor.MiddleCenter);
                return true;
            }
            if(kind=="witnessnpc"+Game.save.room)
            {
                int chapter=Game.save.room;
                bool filed=Game.Has("witness_complete_"+chapter);
                bool repaired=Game.Has("afterlight_done_"+chapter);
                bool steward=Game.Has("afterlight_steward_"+chapter);
                bool stormActive=Game.Has("storm_started")&&Game.StormPolicy(chapter)!=0;
                bool helped=Game.Has("storm_relief_"+chapter);
                Color tint=steward?mint:repaired?gold:filed?mint:Game.Has("witness_started_"+chapter)?gold:muted;
                DrawNpc("innGuest",new Point(o.x,o.y),11+chapter);
                Text(p.x-66,p.y-86,132,20,
                    stormActive?helped?Game.L("星雨热汤已送达","STORM RELIEF DELIVERED"):
                        Game.L("邻居需要热汤","NEIGHBORS NEED STEW"):
                    repaired?steward?Game.L("维护承诺已署名","STEWARD SIGNED"):
                        Game.L("维护者待署名","SIGN MAINTENANCE"):
                    filed?Game.L("证词已公开","TESTIMONY FILED"):
                    Game.L(new[]{"受伤根系证人","渡口幸存证人","矿工停机证人"}[chapter-1],
                        new[]{"GROVE WITNESS","CROSSING WITNESS","MINER WITNESS"}[chapter-1]),
                    11,tint,TextAnchor.MiddleCenter);
                return true;
            }
            if(kind=="witnesssite1"+Game.save.room ||
                kind=="witnesssite2"+Game.save.room)
            {
                int chapter=Game.save.room;
                bool second=kind=="witnesssite2"+chapter;
                bool done=second?Game.Has("witness_sealed_"+chapter):
                    Game.Has("witness_read_"+chapter);
                bool active=Game.WitnessDefenseRoom==chapter && second;
                Color tint=done?mint:active?PixelArt.C("#dd9678"):gold;
                PropImage("witnessledger",chapter,p.x-28,p.y-65,56,68,done?Color.white:muted);
                Ring(new Point(o.x,o.y),active?1f:.55f,tint,24);
                Text(p.x-70,p.y-88,140,20,
                    done?Game.L("原始证据已保全","RECORD PRESERVED"):
                    active?Game.L("击退封锁卫兵","DEFEAT SECURITY"):
                    second?Game.L("现场保全印记","FIELD PRESERVATION"):
                    Game.L("证人原始记录","WITNESS LEDGER"),
                    11,tint,TextAnchor.MiddleCenter);
                if(active)
                    Text(p.x-48,p.y-108,96,19,
                        Game.WitnessDefenseTime.ToString("0")+"s",
                        12,tint,TextAnchor.MiddleCenter);
                return true;
            }
            if(kind=="blackoutshelter")
            {
                Color tint=Game.Has("blackout_complete")?mint:
                    Game.BlackoutActive?PixelArt.C("#f7c783"):gold;
                PropImage("blackoutrefuge",0,p.x-38,p.y-75,76,77,Color.white);
                Ring(new Point(o.x,o.y),Game.BlackoutActive?1.25f:.75f,tint,28);
                Text(p.x-76,p.y-98,152,23,
                    Game.Has("blackout_complete")?Game.L("公开民用避难所","PUBLIC REFUGE"):
                    Game.L("长夜公共避难所","BLACKOUT SHELTER"),
                    12,tint,TextAnchor.MiddleCenter);
                if(Game.BlackoutActive)
                    Text(p.x-80,p.y-117,160,20,
                        Game.BlackoutTime.ToString("0")+"s · "+
                        Game.BlackoutCount+"/3",12,tint,TextAnchor.MiddleCenter);
                return true;
            }
            if(kind.StartsWith("blackoutcivilian"))
            {
                int id;
                if(!int.TryParse(kind.Substring("blackoutcivilian".Length),out id)
                    || id<1 || id>3)return true;
                if(!Game.BlackoutCivilianVisible(id))return true;
                DrawNpc("innGuest",new Point(o.x,o.y),id+11);
                Color tint=Game.BlackoutActive?gold:muted;
                Ring(new Point(o.x,o.y),.56f,tint,22);
                Text(p.x-72,p.y-80,144,20,
                    Game.L(new[]{"莱娜 · 母亲","伊恩 · 孩子","奥姆 · 守灯人"}[id-1],
                        new[]{"LENA · MOTHER","IAN · CHILD","ORME · LAMPKEEPER"}[id-1]),
                    11,tint,TextAnchor.MiddleCenter);
                return true;
            }
            if(kind.StartsWith("triadmemory"))
            {
                int site;
                if(!int.TryParse(kind.Substring("triadmemory".Length),out site) ||
                    site<1 || site>3)return true;
                bool seen=Game.Has("triad_evidence_"+site);
                Color tint=seen?mint:Game.Has("triad_started")?gold:muted;
                PropImage("column",site%2,p.x-27,p.y-65,54,72,seen?Color.white:muted);
                Ring(new Point(o.x,o.y),seen?.75f:.5f,tint,24);
                Text(p.x-67,p.y-85,134,20,
                    seen?Game.L("原始星核账已记录","EVIDENCE SECURED"):
                    Game.L(new[]{"森林供能记录","遗迹水闸记录","矿场机器记录"}[site-1],
                        new[]{"FOREST LEDGER","SLUICE LEDGER","MINE LEDGER"}[site-1]),
                    11,tint,TextAnchor.MiddleCenter);
                return true;
            }
            if(kind=="triadnexus")
            {
                Color tint=Game.TriadBossInterruptReady?PixelArt.C("#efc57e"):
                    Game.Has("triad_sealed")?mint:
                    Game.TriadDefenseActive?PixelArt.C("#d28b86"):gold;
                PropImage("altar",0,p.x-30,p.y-73,60,80,
                    Game.Has("triad_sealed")?Color.white:muted);
                Ring(new Point(o.x,o.y),Game.TriadDefenseActive?1.3f:.7f,tint,30);
                Text(p.x-75,p.y-90,150,20,
                    Game.TriadBossInterruptReady?Game.L("按E · 中断屏障","E · INTERRUPT SHIELD"):
                    Game.Has("triad_sealed")?Game.L("真相已保全","AUDIT PRESERVED"):
                    Game.TriadDefenseActive?Game.L("保密防卫启动","SECURITY ACTIVE"):
                    Game.L("三核中央审计","THREE-CORE AUDIT"),
                    11,tint,TextAnchor.MiddleCenter);
                if(Game.TriadDefenseActive)
                    Text(p.x-43,p.y-110,86,19,Game.TriadTime.ToString("0")+"s",
                        12,tint,TextAnchor.MiddleCenter);
                return true;
            }
            if(kind=="ironfoundry")
            {
                bool done=Game.Has("iron_temper_complete");
                Color tint=done?mint:Game.TemperActive?PixelArt.C("#e3a66b"):gold;
                PropImage("foundry",0,p.x-35,p.y-74,70,82,done?Color.white:muted);
                Ring(new Point(o.x,o.y),Game.TemperActive?1.1f:.6f,tint,22);
                Text(p.x-70,p.y-91,140,22,done?
                    Game.L("公共回火工房","PUBLIC FOUNDRY"):
                    Game.L("旧矿场锻炉","OLD FOUNDRY"),
                    12,tint,TextAnchor.MiddleCenter);
                if(Game.TemperActive)
                {
                    Bar(p.x-42,p.y+10,84,Game.TemperPressure/95f,tint,7);
                    Text(p.x-70,p.y-112,140,19,
                        Game.TemperTime.ToString("0")+"s · "+
                        Game.TemperPressure.ToString("0")+"/95",11,tint,
                        TextAnchor.MiddleCenter);
                }
                return true;
            }
            if(kind=="ironvent1" || kind=="ironvent2")
            {
                int stage=kind=="ironvent1"?1:2;
                bool done=Game.Has("iron_temper_complete") ||
                    Game.TemperActive && Game.TemperStage>stage;
                bool next=Game.TemperActive && Game.TemperStage==stage;
                Color tint=done?mint:next?PixelArt.C("#f3ad75"):muted;
                PropImage("pressurevent",0,p.x-25,p.y-59,50,65,done?Color.white:muted);
                Ring(new Point(o.x,o.y),next?.85f:.42f,tint,20);
                Text(p.x-67,p.y-79,134,20,
                    done?Game.L("管线已降温","PRESSURE RELEASED"):
                    next?Game.L("击败守卫，手动泄压","CLEAR GUARDS · VENT"):
                    Game.L("封锁的散热管","LOCKED COOLANT"),
                    11,tint,TextAnchor.MiddleCenter);
                return true;
            }
            if(kind=="glasswindkeeper")
            {
                DrawNpc("innGuest",new Point(o.x,o.y),9);
                Text(p.x-30,p.y-85,60,20,
                    Game.Has("glasswind_complete")?"✓":Game.Has("desert_report")?"!":"·",
                    17,Game.Has("glasswind_complete")?mint:gold,TextAnchor.MiddleCenter);
                return true;
            }
            if(kind.StartsWith("glasswindrelay"))
            {
                int id;
                if(!int.TryParse(kind.Substring("glasswindrelay".Length),out id) ||
                    id<1 || id>3) return true;
                bool done=Game.Has("glasswind_"+id),active=Game.GlasswindSite==id;
                Color tint=done?mint:active?PixelArt.C("#faaf70"):gold;
                PropImage("column",0,p.x-29,p.y-67,58,76,done?Color.white:muted);
                Ring(new Point(o.x,o.y),active?2.55f:done?1f:.62f,tint,32);
                Text(p.x-61,p.y-89,122,20,
                    done?Game.L("公共避暑风脉","PUBLIC SHADE"):
                    active?Game.L("风脉正在校准","RELAY CHARGING"):
                    Game.L("旧风压中继","WIND RELAY"),
                    11,tint,TextAnchor.MiddleCenter);
                if(active)
                {
                    Bar(p.x-41,p.y+11,82,Game.GlasswindCharge/8f,tint,7);
                    Text(p.x-45,p.y-109,90,20,
                        Game.GlasswindTime.ToString("0")+"s · "+
                        Game.GlasswindWave+"/2",11,tint,TextAnchor.MiddleCenter);
                }
                return true;
            }
            if(kind=="tidedock")
            {
                DrawNpc("innGuest",new Point(o.x,o.y),11);
                Color tint=Game.Has("tide_salvage_complete")?mint:gold;
                Text(p.x-63,p.y-85,126,20,
                    Game.Has("tide_salvage_complete")?Game.L("公共货单已归档","CARGO PUBLIC"):
                    Game.TideSalvageActive?Game.L("等待运回货物","DELIVER CARGO"):
                    Game.L("潮汐打捞码头","TIDAL SALVAGE"),
                    11,tint,TextAnchor.MiddleCenter);
                if(Game.TideSalvageActive)
                {
                    Text(p.x-50,p.y-106,100,20,
                        Game.TideSalvageTime.ToString("0")+"s · "+
                        (Game.TideSalvagePhase-1)+"/3",12,gold,TextAnchor.MiddleCenter);
                }
                return true;
            }
            if(kind.StartsWith("tidecargo"))
            {
                int site;
                if(!int.TryParse(kind.Substring("tidecargo".Length),out site) ||
                    site<1 || site>3)return true;
                bool done=Game.Has("tide_salvage_complete") ||
                    Game.TideSalvageActive && Game.TideSalvagePhase>site;
                bool next=Game.TideSalvageActive && Game.TideSalvagePhase==site;
                Color tint=done?mint:next?gold:muted;
                PropImage("cache",0,p.x-22,p.y-41,44,49,
                    done?Color.white:Game.LowTide?Color.white:muted);
                Ring(new Point(o.x,o.y),next?.78f:.42f,tint,20);
                Text(p.x-65,p.y-71,130,20,
                    done?Game.L("已打捞","RECOVERED"):
                    next?Game.LowTide?Game.L("可收回货单","SALVAGE NOW"):
                    Game.L("等待退潮","WAIT FOR EBB"):
                    Game.L("封存货箱","SEALED CRATE"),
                    11,tint,TextAnchor.MiddleCenter);
                return true;
            }
            if(kind=="skywatcher")
            {
                DrawNpc("innGuest",new Point(o.x,o.y),12);
                Text(p.x-30,p.y-86,60,22,Game.Has("skywatch_complete")?"✓":"!",
                    18,Game.Has("skywatch_complete")?mint:gold,TextAnchor.MiddleCenter);
                return true;
            }
            if(kind.StartsWith("skyprism"))
            {
                string id=kind.Substring("skyprism".Length);
                bool aligned=Game.Has("skywatch_prism_"+id);
                Color light=aligned?mint:gold;
                PropImage("altar",0,p.x-29,p.y-57,58,67,
                    aligned?Color.white:muted);
                Ring(new Point(o.x,o.y),aligned?.72f:.5f,light,20);
                if(Game.SkywatchEchoRefuge)
                    Ring(new Point(o.x,o.y),1.12f,mint,24);
                string legend=!aligned?Game.L("观测棱镜","SKY PRISM"):
                    Game.Has("skywatch_afterglow_complete")?
                        Game.L("永久避寒","WARM REFUGE"):
                    Game.Has("skywatch_complete") &&
                        !Game.Has("skywatch_afterglow_"+id)?
                        Game.L("余辉待收集","AFTERGLOW"):Game.L("已校准","ALIGNED");
                Text(p.x-53,p.y-85,106,21,legend,
                    11,light,TextAnchor.MiddleCenter);
                return true;
            }
            if(kind=="skyobservatory")
            {
                bool active=Game.SkywatchRoom==7;
                bool sealedSky=Game.Has("skywatch_defeated");
                Color tint=active?PixelArt.C("#e8b4ff"):sealedSky?mint:gold;
                PropImage("column",0,p.x-34,p.y-76,68,82,Color.white);
                Ring(new Point(o.x,o.y),active?.95f:.65f,tint,24);
                Text(p.x-70,p.y-104,140,23,
                    active?Game.L("古观星守卫","SKYWATCH GUARDIAN"):
                    sealedSky?Game.L("守卫已停止","GUARDIAN DISABLED"):
                    Game.L("古观星台","OBSERVATORY"),12,tint,TextAnchor.MiddleCenter);
                if(active)
                {
                    Enemy boss=Game.enemies.Find(Game.IsSkywatchEnemy);
                    if(boss!=null) Bar(p.x-43,p.y+12,86,boss.hp/Math.Max(1,boss.maxHp),tint,7);
                    Text(p.x-43,p.y-124,86,18,Game.SkywatchTime.ToString("0")+"s",
                        11,tint,TextAnchor.MiddleCenter);
                    if (Game.SkywatchGroundingTime>0)
                    {
                        Ring(new Point(o.x,o.y),1.2f,mint,24);
                        Text(p.x-63,p.y-143,126,17,
                            Game.L("接地 ", "GROUNDED ") +
                                Game.SkywatchGroundingTime.ToString("0.0")+"s",
                            10,mint,TextAnchor.MiddleCenter);
                    }
                    else if (Game.SkywatchGroundingAvailable)
                        Text(p.x-70,p.y-143,140,18,
                            Game.L("E · 棱镜接地", "E · Ground prism"),
                            10,gold,TextAnchor.MiddleCenter);
                }
                return true;
            }
            if (kind == "commonswitness")
            {
                DrawNpc("innGuest", new Point(o.x, o.y), 5);
                Text(p.x - 35, p.y - 85, 70, 22,
                    Game.Has("commons_relay_complete") ? "✓" :
                    Game.Has("boss_4") ? "!" : "·", 18,
                    Game.Has("commons_relay_complete") ? mint : gold,
                    TextAnchor.MiddleCenter);
                return true;
            }
            if (kind.StartsWith("commonsrelay"))
            {
                int circuit;
                if (!int.TryParse(kind.Substring("commonsrelay".Length), out circuit) ||
                    circuit < 1 || circuit > 3) return true;
                bool done = Game.Has("commons_relay_sealed") ||
                    Game.CommonsRelayRoom == Game.save.room && Game.CommonsRelayStep > circuit;
                bool next = !done && Game.Has("commons_relay_started") &&
                    (Game.CommonsRelayRoom == 0 ? circuit == 1 :
                        Game.CommonsRelayStep == circuit);
                Color light = done ? mint : next ? gold : muted;
                PropImage("column", 0, p.x - 30, p.y - 65, 60, 73,
                    done || next ? Color.white : muted);
                Ring(new Point(o.x,o.y),done?.8f:.55f,
                    new Color(light.r,light.g,light.b,.75f),20);
                Text(p.x - 55,p.y - 94,110,23,
                    done ? Game.L("公共线路在线","PUBLIC CIRCUIT") :
                    next ? Game.L("等待重连","RECONNECT") :
                           Game.L("封闭通道","LOCKED"),
                    11,light,TextAnchor.MiddleCenter);
                if (next && Game.CommonsRelayRoom == Game.save.room)
                    Text(p.x - 40,p.y - 113,80,22,
                        Game.CommonsRelayTime.ToString("0") + "s",
                        12,gold,TextAnchor.MiddleCenter);
                return true;
            }
            if (kind == "archiveecho")
            {
                bool completed = Game.Has("archive_echo_complete"), active = Game.AtlasEchoRoom == Game.save.room;
                Color tint = active ? PixelArt.C("#e7b0f2") : completed ? mint : gold;
                PropImage("column", 0, p.x - 28, p.y - 65, 56, 73, completed ? muted : Color.white);
                Ring(new Point(o.x, o.y), .55f, tint, 20);
                Text(p.x - 53, p.y - 92, 106, 22,
                    active ? Game.L("回声激活", "ECHO ACTIVE") :
                    completed ? Game.L("已完成", "CALIBRATED") :
                    Game.L("方位试炼", "COMPASS TRIAL"),
                    12, tint, TextAnchor.MiddleCenter);
                Enemy guardian = Game.enemies.Find(enemy => enemy.id == 83001);
                if (active && guardian != null)
                    Bar(p.x - 37, p.y + 12, 74,
                        guardian.hp / Math.Max(1, guardian.maxHp), tint, 6);
                return true;
            }
            if (kind == "archivekeeper" || kind.StartsWith("archivefragment"))
            {
                bool collected = kind == "archivekeeper" ? Game.Has("archive_complete") :
                    Game.Has("archive_fragment_" + kind.Substring("archivefragment".Length));
                PropImage(kind == "archivekeeper" ? "post" : "altar", 0,
                    p.x - 26, p.y - 55, 52, 63, collected ? muted : Color.white);
                Ring(new Point(o.x, o.y), .48f, collected ? mint : gold, 16);
                Text(p.x - 18, p.y - 69, 36, 25, collected ? "✓" : "!", 19,
                    collected ? mint : gold, TextAnchor.MiddleCenter);
                return true;
            }
            if (kind.StartsWith("fieldstation"))
            {
                int station;
                if (!int.TryParse(kind.Substring("fieldstation".Length), out station) ||
                    station < 5 || station > 7) return false;
                bool powered = Game.Has("fieldstation_" + station);
                bool reinforced = Game.Has("farshore_relief_" + station);
                Color light = reinforced ? PixelArt.C("#a7e8da") : powered ? mint : gold;
                PropImage("lamp", 0, p.x - 26, p.y - 54, 52, 65, powered ? Color.white : muted);
                Ring(new Point(o.x, o.y), .55f, light, 16);
                Text(p.x - 55, p.y - 86, 110, 24,
                    reinforced ? Game.L("民用避难区", "RELIEF REFUGE") :
                    powered ? Game.L("安全观测站", "SHELTER ONLINE") :
                    Game.L("等待维修", "REPAIR STATION"), 11, light, TextAnchor.MiddleCenter);
                if (powered && station >= 6)
                {
                    Point site=new Point(o.x,o.y);
                    float radius=Game.FieldStationRefugeRadius(station);
                    bool sheltered=Game.FieldStationSheltersHero(o);
                    bool relevant=(station==6?Game.save.heat>=65f:
                        Game.save.cold>=65f)||
                        (Game.Position-site).Length<radius+2f;
                    if(relevant)
                    {
                        // Draw the real 4.2/6.5 world-tile service boundary,
                        // not the old decorative 1.1/1.4 ring that implied
                        // shelter ended several tiles too early.
                        Ring(site,radius,
                            new Color(light.r,light.g,light.b,
                                sheltered?.68f:.28f),36);
                        Text(p.x-103,p.y-110,206,19,
                            sheltered?
                                Game.L("气候保护生效","CLIMATE SHELTER ACTIVE"):
                                Game.L("进入气候避难区","ENTER STATION REFUGE"),
                            10,sheltered?mint:gold,
                            TextAnchor.MiddleCenter);
                    }
                }
                if (reinforced)
                    Text(p.x - 32, p.y + 13, 64, 21, station == 5 ? "90s" : "6.5", 13,
                        light, TextAnchor.MiddleCenter);
                return true;
            }
            if (kind.StartsWith("mirror"))
            {
                int id = int.Parse(kind.Substring(6)); PropImage("mirror", id, p.x - 34, p.y - 68, 68, 79);
                Text(p.x - 25, p.y - 92, 50, 25, new[] { "N", "E", "S", "W" }[Game.save.mirrors[id]], 17, gold, TextAnchor.MiddleCenter); return true;
            }
            if (kind.StartsWith("bell"))
            {
                int id = int.Parse(kind.Substring(4)); PropImage("bell", id, p.x - 36, p.y - 73, 72, 84);
                Text(p.x - 30, p.y - 94, 60, 25, new[] { "I", "II", "III", "IV" }[Game.save.bells[id]], 17, gold, TextAnchor.MiddleCenter); return true;
            }
            if (kind.StartsWith("relay"))
            { bool lit = Game.Has("relay_" + kind.Substring(5)); PropImage("lamp", 0, p.x - 24, p.y - 54, 48, 56, lit ? Color.white : muted); if (lit) Ring(new Point(o.x, o.y), .4f, mint, 12); return true; }
            if (kind == "campfire" || kind == "brazier")
            {
                bool lit = kind == "campfire" || Game.Has("warm_" + o.id);
                PropImage("fire", 0, p.x - 28, p.y - 44, 56, 65, lit ? Color.white : muted);
                if (kind == "brazier" && lit)
                {
                    bool sheltered = Game.BrazierWarmsHero(o);
                    // The original fire artwork stays intact; the ring is
                    // the real 3.5-tile protection boundary, not a fake
                    // global aura that passes through cliffs.
                    Ring(new Point(o.x,o.y),3.5f,
                        sheltered ? mint : PixelArt.C("#af8d58"),28);
                    if (sheltered)
                        Text(p.x-94,p.y-85,188,21,
                            Game.L("火盆暖圈 · 安全时休整",
                                "WARM REFUGE · REST WHEN SAFE"),
                            10,mint,TextAnchor.MiddleCenter);
                }
                return true;
            }
            if (kind.StartsWith("history") || kind == "manifest")
            {
                PropImage("post", 0, p.x - 24, p.y - 55, 48, 56);
                int letter = -1;
                bool cipher = kind.StartsWith("history") && int.TryParse(kind.Substring(7), out letter) &&
                    letter >= 6 && letter <= 8 && Game.Has("survey_complete");
                bool decoded = cipher && Game.Has("dispatch_" + letter);
                if (cipher) Ring(new Point(o.x, o.y), .43f, decoded ? mint : gold, 14);
                Text(p.x - 16, p.y - 51, 32, 23, cipher ? decoded ? "✓" : "!" : "?", 17,
                    decoded ? mint : gold, TextAnchor.MiddleCenter);
                return true;
            }
            if (kind.StartsWith("abyssCircuit"))
            {
                int node;
                if (!int.TryParse(kind.Substring("abyssCircuit".Length), out node) ||
                    node < 0 || node > 2) return true;
                bool visited = Game.AbyssCircuitVisited(node);
                bool next = Game.AbyssCircuitNextNode == node;
                Color light = visited || Game.AbyssCircuitComplete ? mint :
                    next ? gold : muted;
                PropImage("lamp", 0, p.x - 26, p.y - 61, 52, 70,
                    visited || next ? Color.white : muted);
                if (visited)
                    Ring(new Point(o.x, o.y),
                        GameCore.AbyssCircuitShelterRadius,
                        new Color(mint.r,mint.g,mint.b,.34f),32);
                Ring(new Point(o.x, o.y),
                    next ? .9f : .48f, light, 16);
                Text(p.x - 46, p.y - 91, 92, 23,
                    (node == 0 ? "A" : node == 1 ? "B" : "C") +
                    (visited ? " ✓" : next ? " · E" : " · ?"),
                    14, light, TextAnchor.MiddleCenter);
                if (Game.AbyssCircuitComplete)
                {
                    Text(p.x - 82, p.y - 118, 164, 21,
                        Game.Has("abyss_annex_circuit_ground_" + Game.save.abyssFloor) ?
                            Game.L("E · 光路网络", "E · Route network") :
                            Game.L("E · 接地 / 光路", "E · Ground / routes"),
                        9, mint, TextAnchor.MiddleCenter);
                    Text(p.x - 82, p.y - 138, 164, 18,
                        Game.L("附近待探索 ",
                            "Nearby sites left ") + Game.AbyssCircuitUnvisitedSites(node),
                        9, light, TextAnchor.MiddleCenter);
                }
                return true;
            }
            if (kind == "abyssRiftWard")
            {
                Color glow = Game.AbyssWardActive ? mint : gold;
                PropImage("column", 0, p.x - 32, p.y - 71, 64, 79,
                    Game.AbyssWardActive ? Color.white : muted);
                Ring(new Point(o.x, o.y),
                    Game.AbyssWardActive ? 1.6f : .6f, glow, 20);
                // The green radius is real shelter geography, not a
                // decorative light. Communicate entry/exit directly in the
                // production field-object label for all three professions.
                Text(p.x - 97, p.y - 95, 194, 24,
                    Game.AbyssWardShelter ?
                        Game.L("镇流庇护 · 已进入", "PYLON SHELTER ACTIVE") :
                    Game.AbyssWardActive ?
                        Game.L("靠近绿圈可避落印", "ENTER GREEN SHELTER") :
                        Game.L("镇流碑 · E", "WARD PYLON · E"),
                    10, glow, TextAnchor.MiddleCenter);
                return true;
            }
            if (kind == "abyssRootwell")
            {
                bool depleted = Game.Has("abyss_annex_rootwell_" + Game.save.abyssFloor);
                Color light = depleted ? muted : mint;
                PropImage("root", 0, p.x - 32, p.y - 66, 64, 75,
                    depleted ? muted : Color.white);
                Ring(new Point(o.x, o.y), depleted ? .45f : .8f, light, 20);
                Text(p.x - 59, p.y - 93, 118, 22,
                    depleted ? Game.L("本层已使用", "CHARGE SPENT") :
                    Game.L("复苏泉 · E", "ROOTWELL · E"),
                    11, light, TextAnchor.MiddleCenter);
                return true;
            }
            if (kind == "expAltar" || kind == "receiver" || kind == "sluice" || kind == "abyssGate" || kind == "abyssEntry" || kind == "abyssExit")
            {
                bool active = kind == "expAltar" ? Game.GateOpen : kind == "receiver" ? Game.Has("mirrors_lit") : kind == "abyssEntry" ? Game.CanEnter(8) : true;
                PropImage(kind.StartsWith("abyss") ? "root" : "altar", 0, p.x - 34, p.y - 68, 68, 79, active ? Color.white : muted);
                Ring(new Point(o.x, o.y), .6f, active ? mint : muted, 16); return true;
            }
            if (kind == "shade")
            {
                // Trees and canopies retain their existing map decoration.
                // These cues appear only as heat becomes consequential and
                // their green state uses the real unobstructed cooling rule.
                if(Game.save.heat>=65f)
                {
                    Point site=new Point(o.x,o.y);
                    bool cooling=Game.ShadeCoolsHero(o);
                    Ring(site,3f,cooling?mint:gold,28);
                    if(cooling)
                        Text(p.x-92,p.y-85,184,20,
                            Game.CanRestInDesertShade ?
                                Game.L("树荫恢复中","SHADE COOLING · REST") :
                                Game.L("已进入遮阴区","SHADE COOLING"),
                            10,mint,TextAnchor.MiddleCenter);
                    else if((Game.Position-site).Length<6f)
                        Text(p.x-92,p.y-85,184,20,
                            Game.L("前往树荫避暑","ENTER SHADE"),
                            10,gold,TextAnchor.MiddleCenter);
                }
                return true;
            }
            if (kind == "skiff") return true;
            if (kind == "townboard" || kind == "contract")
            { PropImage("post", 0, p.x - 30, p.y - 66, 60, 70); Text(p.x - 14, p.y - 55, 28, 26, kind == "contract" ? "N" : "B", 16, gold, TextAnchor.MiddleCenter); return true; }
            if (kind == "envoy" || kind == "captain" || kind == "tinker" || kind == "keeper" || kind == "council" || kind.StartsWith("castaway"))
            {
                DrawNpc(kind, new Point(o.x, o.y));
                bool complete = kind == "captain" ? Game.Has("harbor_report") : kind == "tinker" ? Game.Has("desert_report") : kind == "keeper" ? Game.Has("mountain_report") : false;
                Text(p.x - 12, p.y - 86, 24, 25, complete ? "+" : "!", 22, complete ? mint : gold, TextAnchor.MiddleCenter); return true;
            }
            return false;
        }
    }
}
