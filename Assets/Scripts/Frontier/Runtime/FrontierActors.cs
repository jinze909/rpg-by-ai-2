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
            // HP becomes zero before DrawFallen after a lethal strike. Hold
            // the phase and complete Sprite actually drawn on the last live
            // repaint, not a new phase inferred from the now-dead actor.
            internal int lastVisibleRoom = -1;
            internal int lastVisiblePhase;
            internal int lastBriarCell = -1;
            internal bool lastBriarFlip;
            // The other six-plus regional Bosses use accepted Bosses/WalkBoss
            // art, NOT the candidate Briar atlas. Capture the exact approved
            // source cell, foot scale, facing and flash tint last painted.
            // HP-zero and AI retirement may change ALL those live fields.
            internal Texture2D lastRegionalSheet;
            internal int lastRegionalRows, lastRegionalFrame;
            internal float lastRegionalSize;
            internal bool lastRegionalFlip;
            internal Color lastRegionalTint;
        }
        private readonly ConditionalWeakTable<Enemy, BossPhaseCue> bossPhaseCues =
            new ConditionalWeakTable<Enemy, BossPhaseCue>();
        // Death may retire AI windup, gait and facing BEFORE the first corpse
        // render. One weakly keyed, presentation-only last-visible snapshot
        // keeps each enemy's exact accepted whole-body source cell for the
        // initial handoff, without changing Enemy/SaveData or holding despawns.
        private sealed class EnemyPaint
        {
            internal int room, columns, rows, frame;
            internal Texture2D sheet;
            internal float size;
            internal Color tint;
            internal bool flip;
        }
        private readonly ConditionalWeakTable<Enemy, EnemyPaint> enemyPaints =
            new ConditionalWeakTable<Enemy, EnemyPaint>();
        private void RememberEnemyPaint(Enemy enemy, Texture2D sheet,
            int columns,int rows,int frame,float size,Color tint,bool flip)
        {
            if(drawingOutline || sheet==null || enemy.hp<=0)return;
            EnemyPaint paint=enemyPaints.GetValue(enemy, ignored=>new EnemyPaint());
            paint.room=Game.save.room;
            paint.sheet=sheet;paint.columns=columns;paint.rows=rows;
            paint.frame=frame;paint.size=size;paint.tint=tint;paint.flip=flip;
        }
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
                            // A six-to-seven-cell art publication (or rollback)
                            // invalidates the whole family. Runtime Sprite.Create
                            // wrappers belong to us, unlike imported Unity
                            // Sprite sub-assets. Remove and dispose ONLY owned
                            // wrappers; otherwise every hot art-family swap
                            // leaks up to 18 complete sets of Sprite objects.
                            Sprite[] displaced;
                            if(heroPublishedSprites.TryGetValue(key,out displaced))
                            {
                                heroPublishedSprites.Remove(key);
                                foreach(Sprite sprite in displaced)
                                    if(sprite!=null&&heroOwnedPublishedSprites.Remove(sprite))
                                    {
                                        if(heroCaptureRenderer!=null&&
                                            heroCaptureRenderer.sprite==sprite)
                                            heroCaptureRenderer.sprite=null;
                                        Destroy(sprite);
                                    }
                            }
                            heroPublishedRecheckAt.Remove(key);
                            heroPublishedUnavailable.Remove(key);
                            heroPublishedIncomplete.Remove(key);
#endif
                        }
            }
            heroContactFamilyComplete=complete;
            return complete;
        }

        // The native Animator path rechecks installed Sprite families
        // after an Editor import, but the exact whole-body GUI fallback
        // previously held its FIRST Texture2D reference indefinitely.
        // Recovery from a GPU failure could therefore resurrect stale
        // Attack/Skill art until the next scene reload. Publish a complete
        // replacement atomically, and keep the last validated body while
        // an incoming import is missing or geometrically incomplete.
        private readonly System.Collections.Generic.Dictionary<string,float>
            fullBodyHeroRecheckAt =
            new System.Collections.Generic.Dictionary<string,float>();
        private Texture2D PublishedFullBodyHeroAtlas(string key,int frames)
        {
            int expectedWidth=frames*256;
            Texture2D accepted;
            bool cached=fullBodyHeroSheetCache.TryGetValue(key,out accepted)&&
                accepted!=null&&accepted.width==expectedWidth&&
                accepted.height==256;
            float next;
            // Gameplay time may reset on load or a new review session.
            // Without rewind detection, a previous long-running save would
            // hold an old Sprite throughout the next newly loaded scene.
            if(!cached || !fullBodyHeroRecheckAt.TryGetValue(key,out next) ||
                clock>=next || clock<next-2.5f)
            {
                Texture2D candidate=Resources.Load<Texture2D>(
                    "Frontier/HeroAnimator/Canonical/"+key);
                // Never expose a partially imported 6/7-frame atlas,
                // different class, or broken boot/cell canvas.
                if(candidate!=null&&candidate.width==expectedWidth&&
                    candidate.height==256)
                {
                    fullBodyHeroSheetCache[key]=candidate;
                    accepted=candidate;
                    cached=true;
                }
                // A failed source import does not delete the last valid
                // whole-body class-owned Sprite. While missing entirely,
                // retry on each draw (as before), so import recovery is fast.
                if(cached)fullBodyHeroRecheckAt[key]=clock+2.5f;
                else fullBodyHeroRecheckAt.Remove(key);
            }
            return cached?accepted:null;
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
                Texture2D atlas=PublishedFullBodyHeroAtlas(key,frames);
                if(atlas!=null)
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
            Texture2D idle=PublishedFullBodyHeroAtlas(idleKey,1);
            if(idle!=null)
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
            Texture2D published=PublishedFullBodyHeroAtlas(key,1);
            Rect bounds=new Rect(x-size/2f,y-size*240f/256f,size,size);
            if(published!=null)
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
            // A legitimate hit flash cannot CANCEL the authored windup or
            // contact drawing while GameCore still owns its live attack
            // clock. The former hit-first priority swapped a real Boss or
            // bowman to unrelated idle/Hurt art for 1-2 frames, then back.
            // Actual stagger clears state in GameCore, so cancelled attacks
            // STILL use the original reactive hit pose.
            if (e.state == 1) return 2;
            if (e.releaseTime > 0) return 3;
            if (e.hitTime > 0) return 4;
            return e.moving ? 1 : 0;
        }
        // Share the EXACT GameCore-selected Boss source pose between live
        // combat and the first corpse repaint. A Boss killed during a
        // windup/contact should not flash to a different idle silhouette
        // before its authored 4-keyframe Death row begins.
        private int BriarBeastLiveCell(Enemy e)
        {
            int bodyPhase = Math.Min(2, Math.Max(0, e.BossPhase - 1));
            int action = e.state == 1 ? 2 :
                e.releaseTime > 0 ? 3 : e.hitTime > 0 ? 4 :
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
            return (bodyPhase * 6 + action) * 4 + keyframe;
        }

        // Regional Bosses already wear a phase tint while alive. A lethal
        // hit must not repaint the same accepted full-body armor bright white
        // on the first death frame, before the rooted death crossfade begins.
        // Share this palette choice across the live and corpse renderers.
        private static Color RegionalBossPhaseTint(Color tint, Enemy enemy,
            int room,int lastVisiblePhase=0)
        {
            int phase=lastVisiblePhase>0?lastVisiblePhase:
                enemy!=null?enemy.BossPhase:1;
            if(enemy!=null&&enemy.boss&&room!=1&&phase>=2)
            {
                tint.g*=phase==2?.94f:.82f;
                tint.b*=phase==2?.90f:.83f;
            }
            return tint;
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
            tint=RegionalBossPhaseTint(tint,e,Game.save.room);
            float phaseCue = e.boss ? BossPhaseCueOpacity(e) : 0f;
            float paintedBossCanvas=size;
            if (e.boss)
            {
                // Capture the most recently drawn living HP stage. Lethal
                // damage mutates hp before the corpse renderer ever runs.
                BossPhaseCue lastVisible=bossPhaseCues.GetValue(e,
                    ignored => new BossPhaseCue());
                lastVisible.lastVisibleRoom=Game.save.room;
                lastVisible.lastVisiblePhase=e.BossPhase;
                lastVisible.lastBriarCell=-1;
                lastVisible.lastBriarFlip=e.VisualFlip;
                lastVisible.lastRegionalSheet=null;
                // Keep base body and hit-flash on the identical authored
                // frame, dimensions and foot pivot. Painting the idle sheet
                // over a moving boss previously created a second drifting
                // silhouette whenever a hit-flash outlived the hit pose.
                if (Game.save.room == 1 && briarBeastSheet != null &&
                    briarBeastSheet.width == 1280 && briarBeastSheet.height == 5760)
                {
                    // Boss phase, windup and contact use the same GameCore
                    // Sprite in living draws and death handoff.
                    int liveCell = BriarBeastLiveCell(e);
                    lastVisible.lastBriarCell=liveCell;
                    float paintedBossSize=DrawEnemyAtlasHandoff(
                        e,briarBeastSheet,4,18,liveCell,
                        p + offset,size,tint,e.VisualFlip,pose);
                    paintedBossCanvas=paintedBossSize;
                    // On an HP phase transition the *current* whole-body
                    // silhouette glows briefly, even after the ordinary hit
                    // flash expires. No second offset body or old-phase pose.
                    float flashAlpha = Math.Max(e.flash / .09f * .86f, phaseCue);
                    if (!drawingOutline && flashAlpha > 0f)
                        Actor(art.FlashMask(briarBeastSheet), 4, 18,
                            liveCell, p + offset, paintedBossSize,
                            new Color(1, 1, 1, flashAlpha * tint.a),
                            e.VisualFlip);
                }
                else
                {
                int row = Math.Min(7, Math.Max(0, Game.save.room - 1));
                int frame = pose == 2 ? 1 : pose == 3 ? 2 : 0;
                int walkAtlas = row / 4;
                // A moving Boss hit previously swapped its complete
                // WalkBoss body for the static Bosses.png idle silhouette
                // for ~0.18 s, then snapped back. Hold the EXACT accepted
                // moving source cell and matching flash mask through a
                // non-attacking hit reaction; real windup/release still
                // choose their authored Bosses combat cells.
                bool walking = (pose == 1 ||
                    (pose == 4 && e.moving &&
                     e.state != 1 && e.releaseTime <= 0f)) &&
                    bossWalkSheets != null &&
                    walkAtlas < bossWalkSheets.Length &&
                    bossWalkSheets[walkAtlas] != null;
                Texture2D visibleBossSheet = walking ?
                    bossWalkSheets[walkAtlas] : bossSheet;
                int visibleBossRows = walking ? 4 : 8;
                int visibleBossFrame = walking ?
                    row % 4 * 4 + GameCore.GaitFrame(e.moveTime, 2.8f) :
                    row * 4 + frame;
                float visibleBossSize = walking ? size * 384 / 320f : size;
                // Save the ACTUAL whole-body source selected for this repaint.
                // A lethal attack can clear moving/state/facing before the
                // first corpse draw, so reconstructing from dead Enemy is wrong.
                lastVisible.lastRegionalSheet=visibleBossSheet;
                lastVisible.lastRegionalRows=visibleBossRows;
                lastVisible.lastRegionalFrame=visibleBossFrame;
                lastVisible.lastRegionalSize=visibleBossSize;
                lastVisible.lastRegionalFlip=e.VisualFlip;
                lastVisible.lastRegionalTint=tint;
                float paintedBossSize=DrawEnemyAtlasHandoff(
                    e,visibleBossSheet,4,visibleBossRows,visibleBossFrame,
                    p + offset,visibleBossSize,tint,e.VisualFlip,pose);
                paintedBossCanvas=paintedBossSize;
                float flashAlpha = Math.Max(e.flash / .09f * .86f, phaseCue);
                if (!drawingOutline && flashAlpha > 0f && visibleBossSheet != null)
                    Actor(art.FlashMask(visibleBossSheet), 4, visibleBossRows,
                        visibleBossFrame, p + offset, paintedBossSize,
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
                // A NORTH/SOUTH bowman is one identity in ALL gameplay
                // states. Previous idle/run/isolated Hit reverted to
                // WalkMonsters/Monsters, producing a 1-2 frame unrelated
                // creature before (or after) its authored bow attack.
                // Only two complete upright poses exist in the approved
                // 3x2 archer atlas, so locomotion/idle hold the SAME
                // painted bow body; never invent moving limbs or reuse
                // the genuine cell-5 fallen silhouette as a walk cycle.
                // E/W keeps the established accepted Monsters artwork.
                vertical = e.kind == 1 && enemyArcherVertical != null &&
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
                bool renderFlip=!vertical && e.VisualFlip;
                RememberEnemyPaint(e,sheet,columns,rows,frame,canvas,tint,renderFlip);
                float paintedEnemySize=DrawEnemyAtlasHandoff(
                    e,sheet,columns,rows,frame,
                    p + offset,canvas,tint,renderFlip,pose);
                if (!drawingOutline && e.flash > 0 && sheet != null) Actor(art.FlashMask(sheet), columns, rows, frame, p + offset, paintedEnemySize, new Color(1, 1, 1, e.flash / .09f * .86f), renderFlip);
            }
            // A real HP threshold crossing, not an arbitrary idle clock,
            // briefly exposes the Boss's new stage with two SMALL painted
            // Effects.png motes. Bind their shoulder positions to the
            // actually VISIBLE whole-body canvas (320->384 easing), never
            // the not-yet-displayed target atlas or an unrelated projectile.
            // This is intentionally an ambient VFX, not a fake Animator
            // contact or gameplay hitbox.
            if(e.boss&&phaseCue>.001f&&!drawingOutline&&
               !Game.save.reducedMotion&&
               Game.save.effectsQuality>0&&effectSheet!=null)
            {
                float scale=Math.Min(28f,paintedBossCanvas*.12f);
                float beamY=p.y-paintedBossCanvas*.60f;
                float spread=paintedBossCanvas*.23f;
                Color gleam=e.BossPhase>=3?
                    new Color(1f,.69f,.44f,
                        phaseCue*CurrentWorldVisualLook.bossPhaseMoteAlpha*tint.a):
                    new Color(.56f,.94f,1f,
                        phaseCue*CurrentWorldVisualLook.bossPhaseMoteAlpha*tint.a);
                Atlas(effectSheet,4,4,13,
                    new Rect(p.x-spread-scale*.5f,
                        beamY-scale*.5f,scale,scale),gleam);
                if(Game.save.effectsQuality>=2)
                    Atlas(effectSheet,4,4,3,
                        new Rect(p.x+spread-scale*.5f,
                            beamY-scale*.5f,scale,scale),
                        new Color(gleam.r,gleam.g,gleam.b,
                            gleam.a*.72f));
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
            // One bounded, artist-authored whole-Sprite transition length
            // for all mobile Bosses and common foes. Core death/loot time
            // stays unchanged; reduced-motion retains one stable corpse.
            float deathBlendFraction=CurrentWorldVisualLook.deathPoseBlendFraction;
            Color tint = new Color(1, 1, 1, Math.Min(1, body.life / (body.duration * .45f)));
            BossPhaseCue previousBoss=null;
            if(body.actor.boss)
            {
                BossPhaseCue candidate;
                if(bossPhaseCues.TryGetValue(body.actor,out candidate) &&
                    candidate.lastVisibleRoom==body.room &&
                    candidate.lastVisiblePhase>0)
                    previousBoss=candidate;
            }
            tint=RegionalBossPhaseTint(tint,body.actor,body.room,
                previousBoss!=null?previousBoss.lastVisiblePhase:0);
            EnemyPaint lastEnemyPaint=null;
            EnemyPaint observed;
            if(!body.actor.boss && enemyPaints.TryGetValue(body.actor,out observed) &&
                observed.room==body.room && observed.sheet!=null &&
                observed.columns>0 && observed.rows>0 && observed.size>0f)
                lastEnemyPaint=observed;
            // Freeze death orientation to the last full-body painting; an
            // AI retreat/contact update after HP-zero may turn the Enemy.
            bool corpseFlip=lastEnemyPaint!=null?lastEnemyPaint.flip:
                previousBoss!=null && previousBoss.lastBriarCell>=0?
                    previousBoss.lastBriarFlip:
                previousBoss!=null && previousBoss.lastRegionalSheet!=null?
                    previousBoss.lastRegionalFlip:body.actor.VisualFlip;
            float corpseMix=1f;
            if(lastEnemyPaint!=null && !Game.save.reducedMotion &&
                progress<deathBlendFraction)
                corpseMix=Mathf.Clamp01(progress/
                    Math.Max(.01f,deathBlendFraction));
            // The contact shadow belongs BEHIND the full-body Sprite. If
            // we draw a remembered pose before this, the next shadow is
            // painted across its boots during lethal hit and death fade.
            DrawActorContactShadow(p,body.actor.boss?BossCanvasSize(body.room)*.57f:
                    body.actor.kind==2?60:54,body.actor.boss?24:16,
                body.actor.boss?8:body.actor.kind==0?4:body.actor.kind==1?5:6,
                tint.a*.84f);
            if(lastEnemyPaint!=null && corpseMix<1f)
                Actor(lastEnemyPaint.sheet,lastEnemyPaint.columns,
                    lastEnemyPaint.rows,lastEnemyPaint.frame,p,
                    lastEnemyPaint.size,
                    new Color(lastEnemyPaint.tint.r,lastEnemyPaint.tint.g,
                        lastEnemyPaint.tint.b,
                        lastEnemyPaint.tint.a*tint.a*(1f-corpseMix)),
                    lastEnemyPaint.flip);
            if (body.actor.boss && body.room == 1 &&
                briarBeastSheet != null && briarBeastSheet.width == 1280 &&
                briarBeastSheet.height == 5760)
            {
                // Preserve the FINAL real full-body combat keypose during
                // the lethal frame, then fade into the 4 authored Death cells.
                // Both Sprites use the same canvas, facing and planted pivot.
                // Reduced motion still presents one fixed corpse immediately.
                bool handoff=progress<deathBlendFraction &&
                    !Game.save.reducedMotion;
                float mix=handoff?
                    Mathf.Clamp01(progress/Math.Max(.01f,deathBlendFraction)):1f;
                if(handoff&&mix<1f)
                    Actor(briarBeastSheet,4,18,
                        previousBoss!=null&&previousBoss.lastBriarCell>=0?
                            previousBoss.lastBriarCell:
                            BriarBeastLiveCell(body.actor),
                        p,BossCanvasSize(body.room),
                        new Color(tint.r,tint.g,tint.b,tint.a*(1f-mix)),
                        corpseFlip);
                if(mix>0f)
                {
                    int stage=(int)GameCore.Clamp(progress*4f,0f,3f);
                    Actor(briarBeastSheet,4,18,17*4+stage,
                        p,BossCanvasSize(body.room),
                        new Color(tint.r,tint.g,tint.b,tint.a*mix),
                        corpseFlip);
                }
            }
            else if (body.actor.boss)
            {
                // Keep a moving regional Boss's ORIGINAL accepted full-body
                // walking identity at the first death frame; then exchange
                // WHOLE Sprite poses on the same anchored source boots.
                // Never splice upper/lower halves or affect damage/death.
                int row=Math.Min(7,Math.Max(0,body.room-1));
                int walkAtlas=row/4;
                bool transition=progress<deathBlendFraction &&
                    !Game.save.reducedMotion && bossSheet!=null;
                int livePose=EnemyPose(body.actor);
                bool moving=body.actor.moving &&
                    (livePose==1 || livePose==4 &&
                     body.actor.state!=1 &&
                     body.actor.releaseTime<=0f) &&
                    bossWalkSheets!=null &&
                    walkAtlas<bossWalkSheets.Length &&
                    bossWalkSheets[walkAtlas]!=null;
                float change=transition?
                    Mathf.Clamp01(progress/deathBlendFraction):1f;
                if(transition&&change<1f)
                {
                    // The stationary body MUST retain its genuine Bosses
                    // windup, released, or idle cell until death takes over.
                    // A pre-existing hit flash does not authorize changing
                    // armor or silhouette on the first corpse repaint.
                    int stride=GameCore.GaitFrame(body.actor.moveTime,2.8f);
                    // If we painted this Boss alive, preserve THAT EXACT
                    // accepted whole-body source cell across HP-zero. The
                    // dead Enemy's state, gait and facing are no longer an
                    // authoritative record of what the player actually saw.
                    bool remembered=previousBoss!=null &&
                        previousBoss.lastRegionalSheet!=null &&
                        previousBoss.lastRegionalRows>0 &&
                        previousBoss.lastRegionalSize>0f;
                    Texture2D source=remembered?
                        previousBoss.lastRegionalSheet:
                        moving?bossWalkSheets[walkAtlas]:bossSheet;
                    int rows=remembered?previousBoss.lastRegionalRows:
                        moving?4:8;
                    int pose=remembered?previousBoss.lastRegionalFrame:
                        moving?row%4*4+stride:
                        row*4+(livePose==2?1:livePose==3?2:0);
                    float sourceSize=remembered?previousBoss.lastRegionalSize:
                        moving?BossCanvasSize(body.room)*384f/320f:
                        BossCanvasSize(body.room);
                    bool sourceFlip=remembered?
                        previousBoss.lastRegionalFlip:body.actor.VisualFlip;
                    Color sourceTint=remembered?
                        previousBoss.lastRegionalTint:tint;
                    Actor(source,4,rows,pose,p,sourceSize,
                        new Color(sourceTint.r,sourceTint.g,sourceTint.b,
                            sourceTint.a*(1f-change)),sourceFlip);
                }
                if(change>0f)
                    Actor(bossSheet,4,8,row*4+3,p,
                        BossCanvasSize(body.room),
                        new Color(tint.r,tint.g,tint.b,tint.a*change),
                        corpseFlip);
            }
            else if (body.actor.kind == 0 && mossboundRaiderSheet != null &&
                mossboundRaiderSheet.width == 512 && mossboundRaiderSheet.height == 768)
            {
                int phase = (int)GameCore.Clamp(progress * 4f, 0f, 3f);
                if(corpseMix>0f)
                    Actor(mossboundRaiderSheet, 4, 6, 20 + phase, p, 96,
                        new Color(tint.r,tint.g,tint.b,tint.a*corpseMix),
                        corpseFlip);
            }
            else if (body.actor.kind == 2 && cinderstoneGuardSheet != null &&
                cinderstoneGuardSheet.width == 512 && cinderstoneGuardSheet.height == 768)
            {
                int phase = (int)GameCore.Clamp(progress * 4f, 0f, 3f);
                if(corpseMix>0f)
                    Actor(cinderstoneGuardSheet, 4, 6, 20 + phase, p, 106,
                        new Color(tint.r,tint.g,tint.b,tint.a*corpseMix),
                        corpseFlip);
            }
            else if (body.actor.kind == 1 && enemyArcherVertical != null &&
                enemyArcherVertical.width % 3 == 0 &&
                enemyArcherVertical.height % 2 == 0 &&
                (lastEnemyPaint!=null?
                    lastEnemyPaint.sheet==enemyArcherVertical:
                    Math.Abs(body.actor.facing.y)>Math.Abs(body.actor.facing.x)))
            {
                // The accepted 3x2 archer painting reserves cell 5 for its
                // original fallen full-body silhouette. Use it only when the
                // living archer used this N/S atlas; horizontal archers keep
                // the same approved Monsters atlas through death.
                // The old generic
                // Monsters death swapped face/outfit immediately on death.
                // Keep the SAME canvas density and foot root as the live
                // north/south archer's production SpriteRenderer pose.
                // North/south bowmen own a distinct illustrated full
                // body, so a lethal hit must not jump directly from bow
                // release into cell 5's fallen painting in ONE repaint.
                // Handoff the two source-authored WHOLE cells; every pose
                // uses the same 120/128 boot anchor and no invented limbs.
                float fraction=CurrentWorldVisualLook.deathPoseBlendFraction;
                bool blend=lastEnemyPaint==null && progress<fraction &&
                    !Game.save.reducedMotion;
                float mix=lastEnemyPaint!=null?corpseMix:
                    blend?Mathf.Clamp01(progress/fraction):1f;
                float canvas=ActorCanvas(enemyArcherVertical,3,96f/128f);
                if(blend&&mix<1f)
                {
                    int pose=EnemyPose(body.actor);
                    int start=(body.actor.facing.y<0?0:3)+
                        (pose==3 || pose==4 &&
                         body.actor.releaseTime>0f?1:0);
                    Actor(enemyArcherVertical,3,2,start,p,canvas,
                        new Color(tint.r,tint.g,tint.b,
                            tint.a*(1f-mix)),false);
                }
                if(mix>0f)
                    Actor(enemyArcherVertical,3,2,5,p,canvas,
                        new Color(tint.r,tint.g,tint.b,tint.a*mix),false);
            }
            else
            {
                // Ordinary raiders, guards and E/W archers can die during
                // a moving footfall. Start from their last accepted whole
                // WalkMonsters frame, then pass the SAME foot root to the
                // actual Monsters death cell. Never cut or re-rig anatomy.
                bool transition=lastEnemyPaint==null &&
                    progress<deathBlendFraction &&
                    !Game.save.reducedMotion && monsterSheet!=null;
                bool walking=body.actor.moving && monsterWalkSheet!=null;
                float mix=lastEnemyPaint!=null?corpseMix:transition?
                    Mathf.Clamp01(progress/deathBlendFraction):1f;
                float size=body.actor.kind==2?106f:96f;
                int kind=Math.Min(2,body.actor.kind);
                if(transition && mix<1f)
                {
                    if(walking)
                    {
                        int gait=GameCore.GaitFrame(body.actor.moveTime,
                            body.actor.kind==0?1.35f:1.8f);
                        int step=body.actor.backpedaling?(4-gait)%4:gait;
                        Actor(monsterWalkSheet,4,3,kind*4+step,p,
                            ActorCanvas(monsterWalkSheet,4,size/128f),
                            new Color(tint.r,tint.g,tint.b,tint.a*(1f-mix)),
                            corpseFlip);
                    }
                    else
                    {
                        // Match the SAME current idle/windup/contact/hit
                        // source cell last used by DrawEnemyActor, instead
                        // of immediately posing a different corpse body.
                        int sourcePose=Math.Min(4,EnemyPose(body.actor));
                        Actor(monsterSheet,6,3,kind*6+sourcePose,p,size,
                            new Color(tint.r,tint.g,tint.b,tint.a*(1f-mix)),
                            corpseFlip);
                    }
                }
                if(mix>0f)
                    Actor(monsterSheet,6,3,kind*6+5,p,size,
                        new Color(tint.r,tint.g,tint.b,tint.a*mix),
                        // Death body uses the same accepted flip as the
                        // lethal attack. Postmortem Enemy.Face() is not
                        // visual authority for this corpse.
                        corpseFlip);
            }
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
            // ONLY the pinned conversation target carries speech motion.
            // Keep the original detailed NPC body untouched and paint a
            // separately authored, complete 24x20 transparent cue ABOVE it.
            if(isTalking&&!drawingOutline)
                DrawNpcActionSignal(p,0,npcKey);
        }
    }
}
