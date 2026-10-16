using System;
using System.Runtime.CompilerServices;
using UnityEngine;
namespace Starfall
{
    public sealed partial class FrontierRuntime
    {
        private Texture2D[] playerSheets;
        private Texture2D[] companionSheets;
        private Texture2D[] walkSheets, companionWalkSheets, bossWalkSheets;
        private Texture2D monsterWalkSheet;
        private Texture2D mossboundRaiderSheet;
        private Texture2D cinderstoneGuardSheet;
        // Phase-entry cues are presentation-only and keyed by Enemy identity:
        // two bosses with the same numeric spawn ID never inherit a flash.
        // Weak keys prevent old save/review encounters from accumulating.
        private sealed class BossPhaseCue
        {
            internal int phase;
            internal float changedAt = float.NegativeInfinity;
        }
        private readonly ConditionalWeakTable<Enemy, BossPhaseCue> bossPhaseCues =
            new ConditionalWeakTable<Enemy, BossPhaseCue>();
        private float BossPhaseCueOpacity(Enemy enemy)
        {
            BossPhaseCue cue = bossPhaseCues.GetValue(enemy,
                ignored => new BossPhaseCue());
            int phase = enemy.BossPhase;
            if (cue.phase == 0) { cue.phase = phase; return 0f; }
            if (phase > cue.phase) cue.changedAt = clock;
            else if (phase < cue.phase || clock < cue.changedAt)
                cue.changedAt = float.NegativeInfinity;
            cue.phase = phase;
            float elapsed = clock - cue.changedAt;
            return elapsed >= 0f && elapsed < .32f ?
                (1f - elapsed / .32f) * .78f : 0f;
        }
        private Texture2D briarBeastSheet;
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
            // Visual quality rollback: default to the published detailed
            // Monsters, Bosses, Npcs, Terrain, Effects and Icons atlases.
            // Rough replacement PNGs are retained AS EDITABLE CANDIDATES,
            // with intact GUIDs, and can be repainted and reconnected later.
            // This is not a permanent art ban.
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
            if(briarBeastSheet!=null) { art.FlashMask(briarBeastSheet); art.OutlineMask(briarBeastSheet); }
            // The first raider hit must not synchronously read and repaint a
            // 512x768 atlas while GameCore is dispatching combat contact.
            // Match the other production actors: prepare both reusable masks
            // during visual load, before the first gameplay frame.
            if(mossboundRaiderSheet!=null) {
                art.FlashMask(mossboundRaiderSheet);
                art.OutlineMask(mossboundRaiderSheet);
            }
            if(cinderstoneGuardSheet!=null) { art.FlashMask(cinderstoneGuardSheet); art.OutlineMask(cinderstoneGuardSheet); }
            foreach(Texture2D sheet in new[]{playerRear,playerSouth,playerReactionsRear,playerReactionsSouth,enemyArcherVertical,monsterSheet,bossSheet,monsterWalkSheet})
                if(sheet!=null) { art.FlashMask(sheet); art.OutlineMask(sheet); }
            fieldProps = Resources.Load<Texture2D>(root + "FieldProps"); terrainSheet = Resources.Load<Texture2D>(root + "Terrain");
            terrainVariants = Resources.Load<Texture2D>(root + "TerrainVariants");
            detailSheet = Resources.Load<Texture2D>(root + "Details"); effectSheet = Resources.Load<Texture2D>(root + "Effects"); iconSheet = Resources.Load<Texture2D>(root + "Icons");
        }
        // Recover high-detail authored HUD/combat atlases after a temporary
        // Resources import failure. Previously a single missing Icons.png or
        // Effects.png on Awake left the entire session on plain text/dots.
        // One retry per second is sufficient; no per-icon resource polling.
        private float publishedVisualRetryAt;
        private void RecoverPublishedVisualAtlases()
        {
            if (iconSheet != null && effectSheet != null &&
                worldProps != null && fieldProps != null &&
                terrainSheet != null && terrainVariants != null &&
                detailSheet != null) return;
            if (clock < publishedVisualRetryAt) return;
            publishedVisualRetryAt = clock + 1f;
            if (iconSheet == null)
                iconSheet = Resources.Load<Texture2D>("Frontier/Art/Icons");
            if (effectSheet == null)
                effectSheet = Resources.Load<Texture2D>("Frontier/Art/Effects");
            if (worldProps == null)
                worldProps = Resources.Load<Texture2D>("Frontier/Art/WorldProps");
            if (fieldProps == null)
                fieldProps = Resources.Load<Texture2D>("Frontier/Art/FieldProps");
            if (terrainSheet == null)
                terrainSheet = Resources.Load<Texture2D>("Frontier/Art/Terrain");
            if (terrainVariants == null)
                terrainVariants = Resources.Load<Texture2D>("Frontier/Art/TerrainVariants");
            if (detailSheet == null)
                detailSheet = Resources.Load<Texture2D>("Frontier/Art/Details");
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
#if UNITY_5_3_OR_NEWER
            bool nativeActorDrawn = DrawSpriteRendererActor(sheet, columns, rows,
                frame, foot, size, color, flip, angle);
            if (nativeActorDrawn) return;
#endif
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
            else if(opacity>.001f)
            {
                // Mature full-body paintings are restored. Ground them with
                // the SAME soft 64px feathered texture that lights use,
                // tinted warm/cool earth-shadow; never resurrect the old
                // rejected ActorContactShadows.png or flat dark rectangles.
                // One full shadow per actor, with exact foot pivot shared by
                // idle/walk/attack/cast/hit/death.
                Image(art.Glow(),foot.x-width*.5f,foot.y-depth*.5f,
                    width,depth,new Color(.045f,.07f,.105f,
                        .58f*opacity));
            }
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
        private readonly System.Collections.Generic.HashSet<string>
            missingHeroBodyWarnings = new System.Collections.Generic.HashSet<string>();


        // A contact transfer is considered live only when the COMPLETE set
        // of 18 seven-cell actions exists: no mixed professions/facings.
        // An incomplete art drop leaves all established six-cell art intact.
        private bool heroContactFamilyChecked;
        private bool heroContactFamilyComplete;
        private float heroContactLastCheckedAt=float.NegativeInfinity;

        private bool HasCompleteHeroContactArt()
        {
            // Art release is atomic for all equipment/directions. Recover
            // after Editor reimport without a scene restart, but never query
            // 18 Resources files on every IMGUI repaint.
            if(heroContactFamilyChecked && clock>=heroContactLastCheckedAt &&
                clock-heroContactLastCheckedAt<2f)
                return heroContactFamilyComplete;
            heroContactFamilyChecked=true;
            heroContactLastCheckedAt=clock;
            bool complete=true;
            foreach(string profession in FullHeroClasses)
                foreach(string action in new[]{"Attack","Cast"})
                    foreach(string direction in FullHeroFacings)
                    {
                        string key="Hero_"+profession+"_"+action+"_"+direction;
                        // Bypass previously cached six-cell atlases: those
                        // would permanently hide newly imported 7-cell art.
                        Texture2D atlas=Resources.Load<Texture2D>(
                            "Frontier/HeroAnimator/Canonical/"+key);
                        if(atlas==null||atlas.height!=256||atlas.width!=7*256)
                            complete=false;
                    }
            if(complete!=heroContactFamilyComplete)
            {
                // Never destroy an in-flight GPU target or imported Sprite.
                // Only invalidate references; cached RenderTextures retire
                // through the existing previous-frame eviction policy.
                foreach(string profession in FullHeroClasses)
                    foreach(string action in new[]{"Attack","Cast"})
                        foreach(string direction in FullHeroFacings)
                        {
                            string key="Hero_"+profession+"_"+action+"_"+direction;
                            fullBodyHeroSheetCache.Remove(key);
#if UNITY_5_3_OR_NEWER
                            heroPublishedSprites.Remove(key);
#endif
                        }
            }
            heroContactFamilyComplete=complete;
            return complete;
        }

        private bool ResolveFullBodyHeroFrame(bool moving,int facing,
            out int action,out int frames,out int index,out float position)
        {
            action=moving?1:Game.HeroAnimationState;
            frames=0;index=0;position=0;
            if(action<0||action>6||facing<0||facing>2)return false;
            int authored=moving?4:Game.HeroAnimationFrameCount(facing);
            if(authored<=0)return false;
            bool actionSix=(action==2||action==3)&&authored==4;
            bool actionSeven=actionSix&&HasCompleteHeroContactArt();
            bool reactionFive=(action==4||action==5)&&authored==3;
            frames=actionSeven?7:actionSix?6:reactionFive?5:authored;
            float phase=moving?Game.WalkPosePhase:
                Game.HeroAnimationPlaybackPosition(facing);
            // Attack/Cast source pose #2 is the LAST windup pose. An
            // artificial contact-transition has been removed: do not show
            // the release keypose early just to conceal a missing drawing.
            // The GameCore source phase >=2, not an interpolated limb,
            // selects the actual contact frame.
            // Seven-cell: 0 idle, 1 guard, 2 windup, 3 NEW hand-painted
            // anticipation, 4 real contact, 5 recovery, 6 idle.
            // Before ActorAction.released the contact cell is unreachable.
            // Six-cell legacy keeps the identical prior damage-clock mapping.
            position=actionSeven?(phase<2f?Math.Min(3f,phase*2f):phase*2f):
                actionSix?(phase<2f?Math.Min(2f,phase*2f):phase*2f-1f):
                reactionFive?phase*2f:phase;
            // Defensive invariant: even if future authored animation
            // phases change the numeric interpolation, it is NEVER valid
            // for Attack/Cast to display its contact pose before the REAL
            // GameCore ActorAction.released event. The existing six-cell
            // phase Math.Min(2f,...) already prevented the early hit; this
            // additional state gate protects both native Animator sampling
            // and a future 7-cell authored-contact upgrade from regression.
            // The actual 1–2-frame North/South visual flash came from
            // walking pose Sprites inserted at slot 1 and slot 4 (producer).
            if((action==2||action==3)&&Game.playerAction!=null&&
                !Game.playerAction.released)
            {
                int realContact=actionSeven?4:3;
                position=Math.Min(position,realContact-.40f);
            }
            if(action==1)position%=frames;
            index=Math.Max(0,Math.Min(frames-1,(int)Math.Floor(position+.20f)));
            return true;
        }

        // Only a canonical, whole-body published Sprite may render the player.
        // The GPU capture and OnGUI fallback share this exact frame selector;
        // neither owns a separate head, torso, waist or legacy attack timeline.
        private void DrawWholeBodyHeroOnGUI(Point foot, Color tint, bool flip,
            int profession, int facing, bool moving)
        {
            if(profession<0||profession>=FullHeroClasses.Length)return;
            if(drawingOutline)
                tint=new Color(1f,.56f,.35f,.85f);
            int action,frames,index;
            float position;
            if(ResolveFullBodyHeroFrame(moving,facing,
                out action,out frames,out index,out position))
            {
                string key="Hero_"+FullHeroClasses[profession]+"_"+
                    FullHeroActions[action]+"_"+FullHeroFacings[facing];
                Texture2D atlas;
                if(!fullBodyHeroSheetCache.TryGetValue(key,out atlas)||atlas==null)
                {
                    atlas=Resources.Load<Texture2D>(
                        "Frontier/HeroAnimator/Canonical/"+key);
                    // Do not cache a missing import: allow Editor rebakes and
                    // asset refresh to recover without restarting the scene.
                    if(atlas!=null)fullBodyHeroSheetCache[key]=atlas;
                }
                if(atlas!=null&&atlas.width==frames*256&&atlas.height==256)
                {
                    Atlas(atlas,frames,1,index,
                        new Rect(foot.x-96,foot.y-180,192,192),tint,flip);
                    return;
                }
            }
            // Missing action atlas: retain the equipped FULL-BODY Idle.
            // Never resurface pre-remaster Player/Walk/Rear/South sprites.
            string idleKey="Hero_"+FullHeroClasses[profession]+"_Idle_"+
                FullHeroFacings[facing];
            Texture2D idle;
            if(!fullBodyHeroSheetCache.TryGetValue(idleKey,out idle)||idle==null)
            {
                idle=Resources.Load<Texture2D>(
                    "Frontier/HeroAnimator/Canonical/"+idleKey);
                if(idle!=null)fullBodyHeroSheetCache[idleKey]=idle;
            }
            if(idle!=null&&idle.width==256&&idle.height==256)
            {
                Atlas(idle,1,1,0,
                    new Rect(foot.x-96,foot.y-180,192,192),tint,flip);
                return;
            }
            // This is a corrupted installation, caught by CI. Fail closed
            // rather than swapping in a different face/equipment identity.
            string absent="Hero_"+FullHeroClasses[profession]+"_"+
                FullHeroActions[Game.HeroAnimationState]+"_"+FullHeroFacings[facing];
            if(missingHeroBodyWarnings.Add(absent))
                Debug.LogWarning("Starfall missing canonical full-body hero "+
                    absent+"; rejecting old character art (check import/CI).");
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
            if(!fullBodyHeroSheetCache.TryGetValue(key,out published)||published==null)
            {
                published=Resources.Load<Texture2D>(
                    "Frontier/HeroAnimator/Canonical/"+key);
                if(published!=null)fullBodyHeroSheetCache[key]=published;
            }
            Rect bounds=new Rect(x-size/2f,y-size*240f/256f,size,size);
            if(published!=null&&published.width==256&&published.height==256)
            {
                Atlas(published,1,1,0,bounds,
                    drawingOutline?new Color(1f,.56f,.35f,.85f):Color.white);
                return;
            }
            if(missingHeroBodyWarnings.Add(key))
                Debug.LogWarning("Starfall missing canonical hero preview "+
                    key+"; rejecting old preview model.");
        }

        private void DrawPlayerActor()
        {
            Point p = ScreenPoint(Game.Position);
            DrawActorContactShadow(p,44,16,Game.WalkFrame,.94f);
            if (Game.racing && !drawingOutline)
                PropImage("skiff", 0, p.x - 51, p.y - 68, 102, 118);
            int look=Game.AppearanceWeapon,pose=Game.PlayerPose;
            if(look<0||look>=FullHeroClasses.Length)return;
            Point direction=Game.VisualFacing;
            Color tint=Game.playerFlash>0 ?
                new Color(1.5f,1.5f,1.5f) : Color.white;
            if(Game.invulnerable>0&&!Game.Dying&&Game.playerFlash<=0)
                tint.a=.65f+.35f*(float)Math.Abs(Math.Sin(clock*16));
            int facing=GameCore.DirectionRow(direction);
            bool moving=pose>=2&&pose<=3;
            bool flip=facing==0&&direction.x<-.1f;
            // SpriteRenderer is the production hero path. No call-site
            // or renderer branch may select a previous-generation Player,
            // Walk, Rear, South, reaction or heavy-combo model anymore.
            // The exact same complete authored frame is used by both lanes.
            bool captured=false;
#if UNITY_5_3_OR_NEWER
            captured=DrawSpriteRendererHero(p,tint,flip,facing,moving);
#endif
            if(!captured)
                DrawWholeBodyHeroOnGUI(p,tint,flip,look,facing,moving);
            // Real 32px authored accents track the SAME live action/release
            // timestamps as the composed body; not an independent decorative
            // animation clock. Every pose keeps the actor's ground pivot.
            if (!drawingOutline && !Game.Dying && heroMotionAccents != null)
            {
                // Only visibly running feet may drive footstep accents.
                // During action recovery the body and its feet are authored
                // as one complete sprite: world-root movement must not fake
                // an independent foot cycle beneath it.
                if (Game.ActuallyMoving && (pose == 2 || pose == 3))
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
                    // Follow the SAME directional weapon origin and launch
                    // height as GameCore projectile/contact geometry. Fixed
                    // east-facing +22px offsets made N/S bows, magic and
                    // sword effects visibly float away from the actual hand.
                    Point muzzle = GameCore.WeaponOrigin(
                        Game.Position, look, direction);
                    Point anchor = ScreenPoint(muzzle);
                    float effectY = anchor.y -
                        GameCore.WeaponHeight(look, direction);
                    Atlas(heroMotionAccents, 4, 4, look * 4 + phase,
                        new Rect(anchor.x - 20, effectY - 20, 40, 40),
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
            // Keep the actual windup frame until Core releases damage.
            if (e.state == 1) return 2;
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
            // Other-room bosses lack three distinct authored body rows.
            // Their existing identity is preserved while later HP phases
            // receive a stable, readable warm tint (no extra GUI primitive).
            if (e.boss && Game.save.room != 1 && e.BossPhase >= 2)
            {
                tint.g *= e.BossPhase == 2 ? .94f : .82f;
                tint.b *= e.BossPhase == 2 ? .90f : .83f;
            }
            float phaseCue = e.boss ? BossPhaseCueOpacity(e) : 0f;
            if (e.boss)
            {
                // Keep base body and hit-flash on the identical authored
                // frame, dimensions and foot pivot. Painting the idle sheet
                // over a moving boss previously created a second drifting
                // silhouette whenever a hit-flash outlived the hit pose.
                if (Game.save.room == 1 && briarBeastSheet != null &&
                    briarBeastSheet.width == 1280 && briarBeastSheet.height == 5760)
                {
                    // The Briar Beast changes real silhouette at HP thresholds.
                    // Damage and contact remain exclusively on GameCore's clock.
                    int bodyPhase = Math.Min(2, Math.Max(0, e.BossPhase - 1));
                    // Room 1 previously showed release art during the final
                    // 0.2 seconds of windup. Never expose contact until release.
                    int action = e.hitTime > 0 ? 4 :
                        e.state == 1 ? 2 : e.releaseTime > 0 ? 3 :
                        e.moving ? 1 : 0;
                    float fraction = action == 2 ?
                        1f - e.timer / Math.Max(.01f, e.windupDuration) :
                        action == 3 ? 1f - e.releaseTime / .38f :
                        action == 4 ? 1f - e.hitTime / .18f : 0f;
                    int keyframe = action == 1 ?
                        GameCore.GaitFrame(e.moveTime, 2.8f) :
                        action == 0 ? (int)(clock * 2.6f) % 4 :
                        (int)GameCore.Clamp(fraction * 4f, 0f, 3f);
                    if (keyframe < 0) keyframe += 4;
                    int liveCell = (bodyPhase * 6 + action) * 4 + keyframe;
                    Actor(briarBeastSheet, 4, 18, liveCell,
                        p + offset, size, tint, e.VisualFlip);
                    // On an HP phase transition the *current* whole-body
                    // silhouette glows briefly, even after the ordinary hit
                    // flash expires. No second offset body or old-phase pose.
                    float flashAlpha = Math.Max(e.flash / .09f * .86f, phaseCue);
                    if (!drawingOutline && flashAlpha > 0f)
                        Actor(art.FlashMask(briarBeastSheet), 4, 18,
                            liveCell, p + offset, size,
                            new Color(1, 1, 1, flashAlpha * tint.a),
                            e.VisualFlip);
                }
                else
                {
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
                float flashAlpha = Math.Max(e.flash / .09f * .86f, phaseCue);
                if (!drawingOutline && flashAlpha > 0f && visibleBossSheet != null)
                    Actor(art.FlashMask(visibleBossSheet), 4, visibleBossRows,
                        visibleBossFrame, p + offset, visibleBossSize,
                        new Color(1, 1, 1, flashAlpha * tint.a),
                        e.VisualFlip);
                }
                if (!drawingOutline && e.state == 1) Atlas(effectSheet,4,4,13,new Rect(p.x-19,p.y-GameCore.BossEmissionHeight(Game.save.room)-19,38,38),new Color(1,.79f,.49f,charge*.6f));
            }
            else
            {
                Texture2D sheet = monsterSheet; int columns = 6, rows = 3, frame = Math.Min(2, e.kind) * 6 + pose;
                bool vertical = false;
                // Phase-2 original full-body art for common melee raiders.
                // Authoritative GameCore intent and release time select every
                // windup/contact frame; this never changes collision/damage.
                if (e.kind == 0 && mossboundRaiderSheet != null &&
                    mossboundRaiderSheet.width == 512 && mossboundRaiderSheet.height == 768)
                {
                    int row = pose == 1 ? 1 : pose == 2 ? 2 :
                        pose == 3 ? 3 : pose == 4 ? 4 : 0;
                    float fraction = row == 2 ? 1f - e.timer /
                        Math.Max(.01f, e.windupDuration) :
                        row == 3 ? 1f - e.releaseTime / .24f :
                        row == 4 ? 1f - e.hitTime / .18f : 0f;
                    int phase = row == 1 ?
                        GameCore.GaitFrame(e.moveTime, 1.35f) :
                        row == 0 ? (int)(clock * 2.8f) % 4 :
                        (int)GameCore.Clamp(fraction * 4f, 0f, 3f);
                    if (phase < 0) phase += 4;
                    sheet = mossboundRaiderSheet; columns = 4; rows = 6;
                    frame = row * 4 + phase;
                }
                else if (e.kind == 2 && cinderstoneGuardSheet != null &&
                    cinderstoneGuardSheet.width == 512 && cinderstoneGuardSheet.height == 768)
                {
                    // Full-body guard poses use the same authoritative GameCore
                    // windup, release, hit and gait clocks as actual combat.
                    int row = pose == 1 ? 1 : pose == 2 ? 2 :
                        pose == 3 ? 3 : pose == 4 ? 4 : 0;
                    float fraction = row == 2 ? 1f - e.timer /
                        Math.Max(.01f, e.windupDuration) :
                        row == 3 ? 1f - e.releaseTime / .24f :
                        row == 4 ? 1f - e.hitTime / .18f : 0f;
                    int phase = row == 1 ? GameCore.GaitFrame(e.moveTime, 1.8f) :
                        row == 0 ? (int)(clock * 2.2f) % 4 :
                        (int)GameCore.Clamp(fraction * 4f, 0f, 3f);
                    if (phase < 0) phase += 4;
                    sheet = cinderstoneGuardSheet; columns = 4; rows = 6;
                    frame = row * 4 + phase;
                }
                else
                {
                if (pose == 1 && monsterWalkSheet != null) { sheet = monsterWalkSheet; columns = 4; rows = 3; int gait=GameCore.GaitFrame(e.moveTime,e.kind==0?1.35f:1.8f);frame=Math.Min(2,e.kind)*4+(e.backpedaling?(4-gait)%4:gait); }
                // A projectile/contact hit flash must not abruptly turn a
                // north/south bowman into the unrelated Monsters body for
                // 0.1 seconds before restoring the illustrated bow pose.
                // Keep the locked native bow cell during an interrupted
                // windup/release; unrelated idle hits retain original art.
                vertical = e.kind == 1 && enemyArcherVertical != null &&
                    (pose == 2 || pose == 3 ||
                     (pose == 4 && (e.state == 1 || e.releaseTime > 0))) &&
                    Math.Abs(e.facing.y) > Math.Abs(e.facing.x);
                if (vertical)
                {
                    sheet = enemyArcherVertical; columns = 3; rows = 2;
                    // Original atlas: north windup/release in row 0
                    // (cells 0/1), south windup/release in row 1
                    // (cells 3/4). Starting south at cell 2 crossed the
                    // whole-body row boundary at contact and flashed a
                    // north-facing pose one frame before south-facing art.
                    // Never sample cell 5's unrelated fallen silhouette.
                    frame = (e.facing.y < 0 ? 0 : 3) +
                        (pose == 3 || pose == 4 && e.releaseTime > 0 ? 1 : 0);
                }
                }
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
            if (body.actor.boss && body.room == 1 &&
                briarBeastSheet != null && briarBeastSheet.width == 1280 &&
                briarBeastSheet.height == 5760)
            {
                int stage = (int)GameCore.Clamp(progress * 4f, 0f, 3f);
                Actor(briarBeastSheet, 4, 18, 17 * 4 + stage,
                    p, BossCanvasSize(body.room), tint, body.actor.VisualFlip);
            }
            else if (body.actor.boss)
                Actor(bossSheet, 4, 8, Math.Max(0, body.room - 1) * 4 + 3,
                    p, BossCanvasSize(body.room), tint, body.actor.VisualFlip);
            else if (body.actor.kind == 0 && mossboundRaiderSheet != null &&
                mossboundRaiderSheet.width == 512 && mossboundRaiderSheet.height == 768)
            {
                int phase = (int)GameCore.Clamp(progress * 4f, 0f, 3f);
                Actor(mossboundRaiderSheet, 4, 6, 20 + phase, p, 96,
                    tint, body.actor.VisualFlip);
            }
            else if (body.actor.kind == 2 && cinderstoneGuardSheet != null &&
                cinderstoneGuardSheet.width == 512 && cinderstoneGuardSheet.height == 768)
            {
                int phase = (int)GameCore.Clamp(progress * 4f, 0f, 3f);
                Actor(cinderstoneGuardSheet, 4, 6, 20 + phase, p, 106,
                    tint, body.actor.VisualFlip);
            }
            else if (body.actor.kind == 1 && enemyArcherVertical != null &&
                enemyArcherVertical.width % 3 == 0 &&
                enemyArcherVertical.height % 2 == 0 &&
                Math.Abs(body.actor.facing.y) > Math.Abs(body.actor.facing.x))
            {
                // The accepted 3x2 archer painting reserves cell 5 for its
                // original fallen full-body silhouette. Use it only when the
                // living archer used this N/S atlas; horizontal archers keep
                // the same approved Monsters atlas through death.
                // The old generic
                // Monsters death swapped face/outfit immediately on death.
                // Keep the SAME canvas density and foot root as the live
                // north/south archer's production SpriteRenderer pose.
                Actor(enemyArcherVertical, 3, 2, 5, p,
                    ActorCanvas(enemyArcherVertical, 3, 96f / 128f),
                    tint, false);
            }
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
            bool occludes = Math.Abs(position.x - Game.save.x) < .85f &&
                Game.save.y < position.y && Game.save.y > position.y - 1.8f;
            int npcKey = id < 0 ? NpcId(kind) : id;
            // Conversation target is PINNED when the interaction starts:
            // never animate another bystander who happens to share the type.
            WorldObject interlocutor = Game.conversationSource;
            bool isTalking = Game.conversation != null &&
                interlocutor != null && interlocutor.kind == kind &&
                Math.Abs(interlocutor.x - position.x) < .05f &&
                Math.Abs(interlocutor.y - position.y) < .05f;
            Color tint = occludes ? new Color(1,1,1,.45f) : Color.white;
            // VISUAL RESTORATION: the temporary 64px-vector-looking
            // EmberfallResidents replacement erased the richer original
            // 16-character Npcs.png detail for the guild and smith. Preserve
            // the original per-person full-body atlas (and dialogue identity)
            // until a visually approved, truly higher-fidelity replacement
            // exists. The experimental resource is retained in Git, NOT drawn.
            // Conversation now has an actual, source-preserving character
            // response. Rotate the ONE authored full-body Sprite very gently
            // around its planted foot; never rig a separate head/waist/arm.
            // The animation belongs only to the pinned interlocutor. It is
            // disabled in reduced-motion, outline and ordinary bystander
            // views, and cannot shift a foot or dialogue/world hitbox.
            Matrix4x4 originalMatrix = GUI.matrix;
            try
            {
                if(isTalking && !Game.save.reducedMotion && !drawingOutline)
                {
                    float lean=CurrentWorldVisualLook.npcTalkLeanDegrees*
                        (float)Math.Sin(clock*3.2f+npcKey*.37f);
                    GUI.matrix=GuiGeometry.RotateAroundPivot(originalMatrix,
                        new Vector2(p.x,p.y),lean);
                }
                Actor(npcSheet, 4, 4, npcKey, p, 96, tint);
            }
            finally { GUI.matrix=originalMatrix; }
        }
    }
}
