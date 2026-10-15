using System;
using UnityEngine;
namespace Starfall
{
    public sealed partial class FrontierRuntime
    {
        private Texture2D[] playerSheets;
        private Texture2D[] companionSheets;
        private Texture2D[] walkSheets, companionWalkSheets, bossWalkSheets;
        private Texture2D monsterWalkSheet;
        // Real pixel-art accent sheet, drawn over the accepted imported terrain.
        private Texture2D terrainAccentRemaster;
        private Texture2D combatRhythmRemaster;
        // Authored four-stage sword, bow, magic and foot-contact pixel accents.
        // Separate resource: never overwrite accepted protagonist body sheets.
        private Texture2D heroMotionAccents;
        // Edge-continuous 32px water-to-land pixel material, never collision.
        private Texture2D coastlineRemaster;
        // Palette-authored transparent contact patches for actors and buildings.
        private Texture2D actorContactShadows;
        // The wider canvas leaves space for weapons while keeping the same
        // logical body scale and 120/128 foot anchor in every pose.
        private static float BossCanvasSize(int room) { return room == 5 ? 264 : 240; }
        private Texture2D playerPortraits, playerRear, playerSouth, playerReactionsRear, playerReactionsSouth, enemyArcherVertical, npcSheet, monsterSheet, bossSheet, fieldProps, terrainSheet, terrainVariants, detailSheet, effectSheet, iconSheet;
        private void LoadVisuals()
        {
            string root = "Frontier/Art/";
            playerSheets = new[] { Resources.Load<Texture2D>(root + "PlayerVanguard"), Resources.Load<Texture2D>(root + "PlayerRanger"), Resources.Load<Texture2D>(root + "PlayerArcanist") };
            companionSheets = new[] { Resources.Load<Texture2D>(root + "CompanionMira"), Resources.Load<Texture2D>(root + "CompanionIvo"), Resources.Load<Texture2D>(root + "CompanionSen") };
            walkSheets = new[] { Resources.Load<Texture2D>(root + "WalkVanguard"), Resources.Load<Texture2D>(root + "WalkRanger"), Resources.Load<Texture2D>(root + "WalkArcanist") };
            companionWalkSheets = new[] { Resources.Load<Texture2D>(root + "WalkMira"), Resources.Load<Texture2D>(root + "WalkIvo"), Resources.Load<Texture2D>(root + "WalkSen") };
            bossWalkSheets = new[] { Resources.Load<Texture2D>(root + "WalkBossA"), Resources.Load<Texture2D>(root + "WalkBossB") };
            monsterWalkSheet = Resources.Load<Texture2D>(root + "WalkMonsters");
            playerPortraits = Resources.Load<Texture2D>(root + "PlayerPortraits"); npcSheet = Resources.Load<Texture2D>(root + "Npcs");
            playerRear = Resources.Load<Texture2D>(root + "PlayerRear");
            playerSouth = Resources.Load<Texture2D>(root + "PlayerSouth");
            playerReactionsRear=Resources.Load<Texture2D>(root+"PlayerReactionsRear");
            playerReactionsSouth=Resources.Load<Texture2D>(root+"PlayerReactionsSouth");
            enemyArcherVertical = Resources.Load<Texture2D>(root + "EnemyArcherVertical");
            monsterSheet = Resources.Load<Texture2D>(root + "Monsters"); bossSheet = Resources.Load<Texture2D>(root + "Bosses");
            // Load immutable GPU masks before play, rather than on a first hit.
            foreach(Texture2D sheet in playerSheets) if(sheet!=null) { art.FlashMask(sheet); art.OutlineMask(sheet); }
            foreach(Texture2D sheet in companionSheets) if(sheet!=null) { art.FlashMask(sheet); art.OutlineMask(sheet); }
            foreach(Texture2D sheet in walkSheets) if(sheet!=null) { art.FlashMask(sheet); art.OutlineMask(sheet); }
            foreach(Texture2D sheet in companionWalkSheets) if(sheet!=null) { art.FlashMask(sheet); art.OutlineMask(sheet); }
            foreach(Texture2D sheet in bossWalkSheets) if(sheet!=null) { art.FlashMask(sheet); art.OutlineMask(sheet); }
            foreach(Texture2D sheet in new[]{playerRear,playerSouth,playerReactionsRear,playerReactionsSouth,enemyArcherVertical,monsterSheet,bossSheet,monsterWalkSheet})
                if(sheet!=null) { art.FlashMask(sheet); art.OutlineMask(sheet); }
            fieldProps = Resources.Load<Texture2D>(root + "FieldProps"); terrainSheet = Resources.Load<Texture2D>(root + "Terrain");
            terrainVariants = Resources.Load<Texture2D>(root + "TerrainVariants");
            terrainAccentRemaster=Resources.Load<Texture2D>(root+"TerrainAccentRemaster");
            combatRhythmRemaster=Resources.Load<Texture2D>(root+"CombatRhythmRemaster");
            heroMotionAccents=Resources.Load<Texture2D>(root+"HeroMotionAccents");
            coastlineRemaster=Resources.Load<Texture2D>(root+"CoastlineRemaster");
            actorContactShadows=Resources.Load<Texture2D>(root+"ActorContactShadows");
            detailSheet = Resources.Load<Texture2D>(root + "Details"); effectSheet = Resources.Load<Texture2D>(root + "Effects"); iconSheet = Resources.Load<Texture2D>(root + "Icons");
        }
        private void Atlas(Texture2D sheet, int columns, int rows, int index, Rect rect, Color color, bool flip = false, float angle = 0)
        {
            if (sheet == null) return;
            index = Math.Max(0, Math.Min(columns * rows - 1, index));
            var matrix = GUI.matrix; Color previousColor = GUI.color;
            try
            {
                if (angle != 0) GUI.matrix = GuiGeometry.RotateInCanvas(matrix, rect, angle);
                color.a*=worldActorOpacity; GUI.color = color;
                GUI.DrawTextureWithTexCoords(rect, sheet, new Rect((index % columns + (flip ? 1 : 0)) / (float)columns, 1 - (index / columns + 1) / (float)rows, (flip ? -1 : 1) / (float)columns, 1f / rows));
            }
            finally { GUI.color = previousColor; GUI.matrix = matrix; }
        }
        private void Actor(Texture2D sheet, int columns, int rows, int frame, Point foot, float size, Color color, bool flip = false, float angle = 0)
        {
            if (drawingOutline && sheet != null) { sheet = art.OutlineMask(sheet); color = new Color(1,.56f,.35f,.9f); }
            Atlas(sheet, columns, rows, frame, new Rect(foot.x - size / 2, foot.y - size * 120 / 128, size, size), color, flip, angle);
        }
        private void DrawActorContactShadow(Point foot,float width,float depth,
            int index,float opacity=1f)
        {
            if(drawingOutline)return;
            if(actorContactShadows!=null)
            {
                // Same ground anchor for walk, spell, hit and dead poses. No
                // head bob, puppet joint or camera translation can shift it.
                Atlas(actorContactShadows,4,4,index,
                    new Rect(foot.x-width/2f,foot.y-depth*.72f,width,depth),
                    new Color(1,1,1,opacity));
            }
            else Box(foot.x-width/2,foot.y-3,width,7,
                new Color(.025f,.04f,.07f,.34f*opacity));
        }
        // Both the Unity SpriteRenderer capture and the OnGUI safety path
        // use the same authoritative GameCore clock and published sprite cell.
        // The row/column invariant is shared with the Editor production bake.
        private static readonly string[] FullHeroClasses =
            {"Vanguard","Ranger","Arcanist"};
        private static readonly string[] FullHeroActions =
            {"Idle","Run","Attack","Cast","Dodge","Hit","Death"};
        private static readonly string[] FullHeroFacings = {"E","N","S"};
        private readonly System.Collections.Generic.Dictionary<string,Texture2D>
            fullBodyHeroSheetCache =
            new System.Collections.Generic.Dictionary<string,Texture2D>();

        private bool ResolveFullBodyHeroFrame(bool moving,int facing,
            out int action,out int frames,out int index,out float position)
        {
            action=moving?1:Game.HeroAnimationState;
            frames=0;index=0;position=0;
            if(action<0||action>6||facing<0||facing>2)return false;
            int authored=moving?4:Game.HeroAnimationFrameCount(facing);
            if(authored<=0)return false;
            bool interpolate=action>=2&&action<=5&&authored>1;
            frames=interpolate?authored*2-1:authored;
            position=(moving?Game.WalkPosePhase:
                Game.HeroAnimationPlaybackPosition(facing))*(interpolate?2f:1f);
            if(action==1)position%=frames;
            index=Math.Max(0,Math.Min(frames-1,(int)Math.Floor(position+.20f)));
            return true;
        }

        private void DrawWholeBodyHeroOnGUI(Texture2D original,int originalFrame,
            Point foot,Color tint,bool flip,int profession,int facing,
            int pose,bool moving)
        {
            // This is the production OnGUI FALLBACK, not the old HumanActor
            // puppet. Draw ONE complete, accepted RGBA sprite with one Pivot.
            // Never mix a warrior head overlay, action torso, and walking legs.
            // Production Sprite PNGs are GPU-only (isReadable: 0). The
            // outline system calls GetPixels, so sample the already-prewarmed
            // readable original for that PASS, never the GPU-only PNG.
            if(drawingOutline)
            {
                Actor(original,4,3,originalFrame,foot,192,tint,flip);
                return;
            }
            int action,frames,index;
            float position;
            bool standard=!(!moving&&(pose==6||pose==7)&&
                Game.HeroAnimationState==2);
            if(standard&&profession>=0&&profession<FullHeroClasses.Length&&
                ResolveFullBodyHeroFrame(moving,facing,
                    out action,out frames,out index,out position))
            {
                string key="Hero_"+FullHeroClasses[profession]+"_"+
                    FullHeroActions[action]+"_"+FullHeroFacings[facing];
                Texture2D atlas;
                if(!fullBodyHeroSheetCache.TryGetValue(key,out atlas))
                {
                    atlas=Resources.Load<Texture2D>(
                        "Frontier/HeroAnimator/Canonical/"+key);
                    fullBodyHeroSheetCache[key]=atlas;
                }
                if(atlas!=null&&atlas.width==frames*256&&atlas.height==256)
                {
                    Atlas(atlas,frames,1,index,
                        new Rect(foot.x-96,foot.y-180,192,192),
                        tint,flip);
                    return;
                }
            }
            // Missing/stale resource or an unbaked heavy-combo motion:
            // still a SINGLE complete class-owned original, never a
            // composited upper/lower half or detached head Sprite.
            if(original!=null)
                Actor(original,4,3,originalFrame,foot,192,tint,flip);
        }

        private void DrawPlayerPreview(int equipment, float x, float y, float size)
        {
            int profession=(int)GameCore.Clamp(equipment,0,2);
            // Loadout and inventory are PRODUCTION character appearances too:
            // the same front-facing full-body, blonde identity and foot Pivot
            // must appear here as in the world. Former code divided 'size'
            // by 192 before calling Actor(), accidentally drawing a ~0.5px
            // avatar for the inventory's 104px preview.
            string key="Hero_"+FullHeroClasses[profession]+"_Idle_S";
            Texture2D published;
            if(!fullBodyHeroSheetCache.TryGetValue(key,out published))
            {
                published=Resources.Load<Texture2D>(
                    "Frontier/HeroAnimator/Canonical/"+key);
                fullBodyHeroSheetCache[key]=published;
            }
            Rect bounds=new Rect(x-size/2f,y-size*240f/256f,size,size);
            if(published!=null&&published.width==256&&published.height==256
                &&!drawingOutline)
            {
                Atlas(published,1,1,0,bounds,Color.white);
                return;
            }
            // Offline GPU-only sheets are deliberately not readable for
            // PixelArt.OutlineMask. Preserve a complete readable source
            // silhouette for the outline-only or missing-atlas fallback.
            Texture2D source=walkSheets!=null?walkSheets[profession]:null;
            if(source!=null)
                Actor(source,4,3,8,new Point(x,y),size,Color.white,false);
        }

        private void DrawPlayerActor()
        {
            Point p = ScreenPoint(Game.Position);
            DrawActorContactShadow(p,44,16,Game.WalkFrame,.94f);
            if (Game.racing && !drawingOutline)
                PropImage("skiff", 0, p.x - 51, p.y - 68, 102, 118);
            int look=Game.AppearanceWeapon,pose=Game.PlayerPose;
            if (look<0||look>=3||playerSheets==null||
                playerSheets[look]==null)
            {
                DrawLegacyHero();
                return;
            }
            Point direction=Game.VisualFacing;
            Color tint=Game.playerFlash>0 ?
                new Color(1.5f,1.5f,1.5f) : Color.white;
            if (Game.invulnerable>0&&!Game.Dying&&Game.playerFlash<=0)
                tint.a=.65f+.35f*(float)Math.Abs(Math.Sin(clock*16));
            int facing=GameCore.DirectionRow(direction);
            // The authoritative released recovery moves with the COMPLETE
            // same-class Walk sprite. Never weld an action torso to walking
            // boots or warp one ankle while the pelvis stays frozen.
            bool movingRecovery=!Game.Dying&&Game.ActuallyMoving&&
                Game.PlayerMobility==MobilityRule.Reduced&&
                pose>=4&&pose<=7;
            bool moving=pose>=2&&pose<=3||movingRecovery;
            bool rear=false,south=false,resting=!moving&&pose<=1;
            Texture2D sheet=playerSheets[look];
            int frame=pose;
            if(moving&&walkSheets!=null&&walkSheets[look]!=null)
            {
                sheet=walkSheets[look];
                frame=facing*4+Game.WalkFrame;
            }
            else
            {
                if(resting)
                {
                    frame=facing==1?1:0;
                    if(facing==2&&walkSheets!=null&&walkSheets[look]!=null)
                    {
                        sheet=walkSheets[look];
                        frame=8;
                    }
                }
                rear=playerRear!=null&&facing==1&&pose>=4&&pose<=7;
                if(rear)
                {
                    sheet=playerRear;
                    frame=look*4+(pose==4||pose==6?2:3);
                }
                south=playerSouth!=null&&facing==2&&pose>=4&&pose<=7;
                if(south)
                {
                    sheet=playerSouth;
                    frame=look*4+pose-4;
                }
                if(pose>=8&&facing!=0)
                {
                    Texture2D reaction=facing==1?playerReactionsRear:playerReactionsSouth;
                    if(reaction!=null)
                    {
                        sheet=reaction;
                        frame=look*4+(pose==8?0:pose==9?1:pose==10?2:3);
                    }
                }
            }
            bool flip=!rear&&!south&&facing==0&&direction.x<-.1f;
            // Full-frame authority: both native capture and legacy OnGUI
            // sample the exact same class/action/facing published atlas.
            // No Dynamic UnifiedActionGait, HumanActor head splice or
            // independently translated body part participates in gameplay.
            bool captured=false;
#if UNITY_5_3_OR_NEWER
            captured=DrawSpriteRendererHero(sheet,frame,p,tint,flip,
                facing,pose,moving,Game.Dying,null);
#endif
            if(!captured)
                DrawWholeBodyHeroOnGUI(sheet,frame,p,tint,flip,
                    look,facing,pose,moving);
            // Real 32px authored accents track the SAME live action/release
            // timestamps as the composed body; not an independent decorative
            // animation clock. Every pose keeps the actor's ground pivot.
            if (!drawingOutline && !Game.Dying && heroMotionAccents != null)
            {
                if (Game.ActuallyMoving && pose >= 2 && pose <= 7)
                {
                    int step = Game.WalkFrame;
                    Atlas(heroMotionAccents, 4, 4, 12 + step,
                        new Rect(p.x - 18, p.y - 15, 36, 27),
                        new Color(1, 1, 1, .40f), direction.x < -.1f);
                }
                ActorAction activeAction = Game.playerAction;
                if (activeAction != null && pose >= 4 && pose <= 7)
                {
                    int phase;
                    if (!activeAction.released)
                        phase = activeAction.elapsed < activeAction.contact * .56f ? 0 : 1;
                    else
                        phase = activeAction.elapsed - activeAction.contact < .12f ? 2 : 3;
                    float axis = direction.x < -.1f ? -1 : 1;
                    float effectX = p.x + axis * 22;
                    float effectY = p.y - (look == 2 ? 77 : 63);
                    Atlas(heroMotionAccents, 4, 4, look * 4 + phase,
                        new Rect(effectX - 20, effectY - 20, 40, 40),
                        new Color(1, 1, 1, phase == 3 ? .55f : .78f),
                        direction.x < -.1f);
                }
            }
            if (!drawingOutline && Game.save.upgrades > 0 && !Game.Dying)
            { Point rune=p+new Point(direction.x<0?-24:24,-26);Atlas(effectSheet,4,4,13,new Rect(rune.x-7,rune.y-7,14,14),new Color(1,.83f,.54f,Math.Min(.8f,.3f+Game.save.upgrades*.09f))); }
            if (!drawingOutline && Game.guardTime > 0 && !Game.Dying) Ring(Game.Position, .44f, new Color(.76f, .87f, .73f, .65f), 20);
        }
        private int EnemyPose(Enemy e)
        {
            if (e.hitTime > 0) return 4;
            if (e.state == 1) return e.boss && Game.save.room == 1 && e.timer < .2f ? 3 : 2;
            if (e.releaseTime > 0) return 3;
            return e.moving ? 1 : 0;
        }
        private void DrawEnemyActor(Enemy e)
        {
            Point p = ScreenPoint(e.Position); float size = e.boss ? BossCanvasSize(Game.save.room) : e.kind == 2 ? 106 : 96;
            DrawActorContactShadow(p,size*.57f,e.boss?24:16,
                e.boss?8+GameCore.GaitFrame(e.moveTime,2.8f):
                    e.kind==0?4:e.kind==1?5:6,.84f);
            // The two-wave roadwarden attackers are real combat enemies, but
            // their warm foot ring distinguishes them from roaming patrols.
            // A fixed ring is legible even with reduced-motion effects.
            if (!drawingOutline && e.id >= 81000 && e.id < 81100)
                Ring(e.Position, .52f, PixelArt.C("#e6a06c"), 16);
            if (!drawingOutline && e.id >= 82000 && e.id < 82100)
                Ring(e.Position, .52f, PixelArt.C("#f27d7d"), 16);
            if (!drawingOutline && e.id == 83001)
                Ring(e.Position, .72f, PixelArt.C("#e7b0f2"), 24);
            // Static mint outline also works with reduced-motion enabled.
            if (!drawingOutline && !Game.practicing && e.releaseTime > .001f && e.stun <= 0)
                Ring(e.Position, e.boss ? .86f : .54f, new Color(.48f, 1f, .79f, .85f), 24);
            if (Game.practicing)
            {
                // The safe training rig still demonstrates a bodily wind-up and
                // release. A static sign plus a red circle cannot teach timing.
                Actor(monsterSheet, 6, 3, 12 + EnemyPose(e), p, 106, Color.white, e.VisualFlip);
                if(!drawingOutline)Box(p.x - 16, p.y + 2, 32, 3, mint); return;
            }
            int pose = EnemyPose(e);
            float charge = e.state == 1 ? 1 - e.timer / Math.Max(.01f, e.windupDuration > 0 ? e.windupDuration : e.boss ? 1.1f : .65f) : 0;
            Point offset = new Point();
            // Recoil is swept in Core rather than sliding the drawing through walls.
            Color tint = e.flash > 0 ? new Color(1.55f, 1.55f, 1.55f) : Color.white;
            if (e.boss && Math.Abs(e.x - Game.save.x) < 1.4f && Game.save.y < e.y - .01f && Game.save.y > e.y - 2.7f) tint.a = .55f;
            if (e.boss)
            {
                // Keep base body and hit-flash on the identical authored
                // frame, dimensions and foot pivot. Painting the idle sheet
                // over a moving boss previously created a second drifting
                // silhouette whenever a hit-flash outlived the hit pose.
                int row = Math.Min(7, Math.Max(0, Game.save.room - 1));
                int frame = pose == 2 ? 1 : pose == 3 ? 2 : 0;
                int walkAtlas = row / 4;
                bool walking = pose == 1 && bossWalkSheets != null &&
                    walkAtlas < bossWalkSheets.Length &&
                    bossWalkSheets[walkAtlas] != null;
                Texture2D visibleBossSheet = walking ?
                    bossWalkSheets[walkAtlas] : bossSheet;
                int visibleBossRows = walking ? 4 : 8;
                int visibleBossFrame = walking ?
                    row % 4 * 4 + GameCore.GaitFrame(e.moveTime, 2.8f) :
                    row * 4 + frame;
                float visibleBossSize = walking ? size * 384 / 320f : size;
                Actor(visibleBossSheet, 4, visibleBossRows, visibleBossFrame,
                    p + offset, visibleBossSize, tint, e.VisualFlip);
                if (!drawingOutline && e.flash > 0 && visibleBossSheet != null)
                    Actor(art.FlashMask(visibleBossSheet), 4, visibleBossRows,
                        visibleBossFrame, p + offset, visibleBossSize,
                        new Color(1, 1, 1, e.flash / .09f * .86f * tint.a),
                        e.VisualFlip);
                if (!drawingOutline && e.state == 1) Atlas(effectSheet,4,4,13,new Rect(p.x-19,p.y-GameCore.BossEmissionHeight(Game.save.room)-19,38,38),new Color(1,.79f,.49f,charge*.6f));
            }
            else
            {
                Texture2D sheet = monsterSheet; int columns = 6, rows = 3, frame = Math.Min(2, e.kind) * 6 + pose;
                if (pose == 1 && monsterWalkSheet != null) { sheet = monsterWalkSheet; columns = 4; rows = 3; int gait=GameCore.GaitFrame(e.moveTime,e.kind==0?1.35f:1.8f);frame=Math.Min(2,e.kind)*4+(e.backpedaling?(4-gait)%4:gait); }
                bool vertical = e.kind == 1 && enemyArcherVertical != null && (pose == 2 || pose == 3) && Math.Abs(e.facing.y) > Math.Abs(e.facing.x);
                if (vertical) { sheet = enemyArcherVertical; columns = 3; rows = 2; frame = (e.facing.y < 0 ? 0 : 2) + pose - 2; }
                float canvas=ActorCanvas(sheet,columns,size/128);
                Actor(sheet, columns, rows, frame, p + offset, canvas, tint, !vertical && e.VisualFlip);
                if (!drawingOutline && e.flash > 0 && sheet != null) Actor(art.FlashMask(sheet), columns, rows, frame, p + offset, canvas, new Color(1, 1, 1, e.flash / .09f * .86f), !vertical && e.VisualFlip);
            }
            // 16 real authored supplemental frames, synchronized to the
            // exact game-core enemy state and attack-release timestamps:
            // no animation loops running without a real attack event.
            if(!drawingOutline&&combatRhythmRemaster!=null)
            {
                int fx=-1;
                float alpha=.8f;
                if(e.state==1&&e.stun<=0)
                {
                    float prep=1-e.timer/Math.Max(.01f,e.windupDuration>0?
                        e.windupDuration:e.boss?1.1f:.65f);
                    int stage=(int)GameCore.Clamp(prep*4,0,3);
                    fx=e.boss?4+stage:stage;
                }
                else if(e.releaseTime>0)
                {
                    int stage=(int)GameCore.Clamp((1-e.releaseTime/
                        (e.boss?.38f:.24f))*4,0,3);
                    fx=8+stage;alpha=.74f;
                }
                if(fx>=0)
                {
                    float iconSize=e.boss?52:32;
                    Atlas(combatRhythmRemaster,4,4,fx,
                        new Rect(p.x-iconSize/2,p.y-size*.78f-iconSize/2,
                            iconSize,iconSize),
                        new Color(1,1,1,alpha),e.VisualFlip);
                }
            }
            // The boss already has a HUD poise meter. Give ordinary enemies a
            // tiny contextual meter only after the player has built stagger.
            if (!drawingOutline && !e.boss && e.stagger > .01f && e.staggerRecovery <= 0)
            {
                float y = p.y - size * .72f;
                Box(p.x - 17, y, 34, 5, new Color(.04f, .09f, .12f, .72f));
                Box(p.x - 16, y + 1, 32 * Mathf.Clamp01(e.stagger / 30f), 3, mint);
            }
        }
        private void DrawFallen(FallenActor body)
        {
            Point p = ScreenPoint(body.actor.Position); float progress = 1 - body.life / body.duration;
            Color tint = new Color(1, 1, 1, Math.Min(1, body.life / (body.duration * .45f)));
            // Keep the contact patch as a corpse fades out; a death frame
            // otherwise snaps from a solid grounded sprite into a floating one.
            DrawActorContactShadow(p,body.actor.boss?BossCanvasSize(body.room)*.57f:
                    body.actor.kind==2?60:54,body.actor.boss?24:16,
                body.actor.boss?8:body.actor.kind==0?4:body.actor.kind==1?5:6,
                tint.a*.84f);
            if (body.actor.boss) Actor(bossSheet, 4, 8, Math.Max(0, body.room - 1) * 4 + 3, p, BossCanvasSize(body.room), tint,body.actor.VisualFlip);
            else Actor(monsterSheet, 6, 3, Math.Min(2, body.actor.kind) * 6 + 5, p, body.actor.kind == 2 ? 106 : 96, tint,body.actor.VisualFlip);
            if (body.actor.boss && progress < .6f) Atlas(effectSheet, 4, 4, 15, new Rect(p.x - 52, p.y - 80 - progress * 40, 104, 104), new Color(.8f, .92f, .84f, .7f * (1 - progress)));
        }
        private int NpcId(string kind)
        {
            if (kind == "guild") return 0; if (kind == "garden") return 1; if (kind == "forge") return 2;
            if (kind == "cook" || kind == "rest") return 3; if (kind == "fish") return 4;
            if (kind == "scout" || kind == "returnscout") return 5; if (kind == "mentor") return 6;
            if (kind == "envoy") return 7; if (kind == "captain") return 8; if (kind == "tinker") return 9;
            if (kind == "keeper") return 10; if (kind == "council") return 11;
            if (kind.StartsWith("castaway")) return 12 + (kind.EndsWith("1") ? 1 : 0);
            return kind == "innGuest" ? 15 : 14;
        }
        private void DrawNpc(string kind, Point position, int id = -1)
        {
            Point p = ScreenPoint(position);
            DrawActorContactShadow(p,34,13,7,.78f);
            bool occludes = Math.Abs(position.x - Game.save.x) < .85f && Game.save.y < position.y && Game.save.y > position.y - 1.8f;
            Actor(npcSheet, 4, 4, id < 0 ? NpcId(kind) : id, p, 96,
                occludes ? new Color(1, 1, 1, .45f) : Color.white);
            // Existing authored NPC body remains intact. A short, locally
            // tethered speaking sigil shows whose real-world interaction is
            // active; the NPC never floats, scales or changes identity.
            if(!drawingOutline&&combatRhythmRemaster!=null&&
                Game.conversation!=null&&
                (position-Game.Position).Length<1.8f)
            {
                int phase=((int)(clock*6))%4;
                if(phase<0)phase+=4;
                Atlas(combatRhythmRemaster,4,4,12+phase,
                    new Rect(p.x-11,p.y-105,22,22),
                    new Color(1,1,1,.75f));
            }
        }
    }
}
