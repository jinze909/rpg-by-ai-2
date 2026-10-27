using System;
using System.Runtime.CompilerServices;
using UnityEngine;
namespace Starfall
{
    public sealed partial class FrontierRuntime
    {
        private Texture2D[] playerSheets;
        private Texture2D[] companionSheets;
        private readonly System.Collections.Generic.HashSet<string>
            missingCompanionBodyWarnings =
                new System.Collections.Generic.HashSet<string>();
        private void WarnMissingCompanionBody(int id,string missing)
        {
            string name=id==0?"Mira":id==1?"Ivo":"Sen";
            if(missingCompanionBodyWarnings.Add(name+":"+missing))
                Debug.LogWarning("Starfall companion "+name+
                    " visual import incomplete ("+missing+
                    "); rendering only this ally's accepted full-body source.");
        }
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
            // Outline/canopy preview passes observe the CURRENT body but
            // are not a completed living paint. A HP-stage transition
            // discovered only through an outline must not consume its
            // actual 320ms flash or alter the corpse's final visible phase.
            if(drawingOutline)return 0f;
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
            shorelineFoamSheet = Resources.Load<Texture2D>(root + "ShorelineFoam");
            waterRipplesSheet = Resources.Load<Texture2D>(root + "WaterRipples");
            campfireFlamesSheet = Resources.Load<Texture2D>(root + "CampfireFlames");
            detailSheet = Resources.Load<Texture2D>(root + "Details"); effectSheet = Resources.Load<Texture2D>(root + "Effects"); iconSheet = Resources.Load<Texture2D>(root + "Icons");
        }
        // Recover high-detail authored HUD/combat atlases after a temporary
        // Resources import failure. Previously a single missing Icons.png or
        // Effects.png on Awake left the entire session on plain text/dots.
        // One retry per second is sufficient; no per-icon resource polling.
        // Unity can finish importing actor Resources AFTER Awake. A missing
        // original companion sheet must recover while playing, independently
        // of whether Icons and Terrain have already loaded successfully.
        // Never publish a partial atlas or reload a valid accepted identity.
        private float companionVisualRecoveryAt;
        private void RecoverCompanionVisualAtlases()
        {
            if(companionSheets==null||companionWalkSheets==null||
               companionSheets.Length<3||companionWalkSheets.Length<3)
                return;
            bool missing=false;
            for(int i=0;i<3;i++)
                if(!ActorAtlasGeometry.IsAcceptedCompanionAtlas(companionSheets[i])||
                   !AcceptedCompanionWalkSheet(companionWalkSheets[i]))
                    {missing=true;break;}
            if(!missing)return;
            if(clock<companionVisualRecoveryAt)return;
            companionVisualRecoveryAt=clock+1f;
            string[] names={"Mira","Ivo","Sen"};
            for(int id=0;id<3;id++)
            {
                if(!ActorAtlasGeometry.IsAcceptedCompanionAtlas(
                    companionSheets[id]))
                {
                    Texture2D action=Resources.Load<Texture2D>(
                        "Frontier/Art/Companion"+names[id]);
                    if(ActorAtlasGeometry.IsAcceptedCompanionAtlas(action))
                    {
                        art.FlashMask(action);art.OutlineMask(action);
                        companionSheets[id]=action;
                    }
                }
                if(!AcceptedCompanionWalkSheet(companionWalkSheets[id]))
                {
                    Texture2D walk=Resources.Load<Texture2D>(
                        "Frontier/Art/Walk"+names[id]);
                    if(AcceptedCompanionWalkSheet(walk))
                    {
                        art.FlashMask(walk);art.OutlineMask(walk);
                        companionWalkSheets[id]=walk;
                    }
                }
            }
        }
        private static bool AcceptedCompanionWalkSheet(Texture2D source)
        {return source!=null&&source.width==4*256&&
            source.height==3*256;}
        private float publishedVisualRetryAt;
        private void RecoverPublishedVisualAtlases()
        {
            RecoverCompanionVisualAtlases();
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
            if (drawingOutline && sheet != null)
            {
                Texture2D outline = art.OutlineMask(sheet);
                if(outline != null)
                {
                    sheet=outline;
                    color=new Color(1f,.56f,.35f,.9f);
                }
                else
                {
                    // On imported GPU-only NPCs without a published mask,
                    // draw ONE lightly highlighted instance of the SAME
                    // approved full-body Sprite. No GetPixels exception,
                    // opaque orange replacement, old character atlas,
                    // extra pose, rigging or shifted planted-foot anchor.
                    color=new Color(1f,.65f,.40f,.34f);
                }
            }
#if UNITY_5_3_OR_NEWER
            // Formal SpriteRenderer/Animator capture is ONLY for approved
            // entire living/corpse character atlases. Flash/outline masks,
            // glyphs and decorative sources otherwise create large offscreen
            // render targets (including 320px Boss masks) without ever being
            // eligible for a native Controller. Those exact source pixels
            // belong on the ordinary world Atlas presentation path.
            if (NativeActorEligible(sheet, columns, rows))
            {
                bool nativeActorDrawn = DrawSpriteRendererActor(
                    sheet, columns, rows, frame, foot, size,
                    color, flip, angle);
                if (nativeActorDrawn) return;
            }
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

        // Preserve a source-authored planted Run contact for a few frames
        // when the hero releases movement on a passing gait cell. This state
        // is presentation-only and shared by SpriteRenderer and OnGUI through
        // ResolveFullBodyHeroFrame; it never moves the GameCore root or alters
        // attack, projectile, collision, or foot-contact clocks.
        private const float HeroStopPlantSeconds=.075f;
        private GameCore heroGaitOwner;
        // SaveData is replaced on Load even when GameCore, room, weapon
        // and foot coordinates are identical. A previous slot's last
        // running contact is never valid in the next character's Idle.
        private SaveData heroGaitSave;
        private int heroGaitRoom=-1,heroGaitAppearance=-1,heroGaitFacing=-1;
        private bool heroGaitMirrored;
        private int heroGaitFrame=-1;
        private Point heroGaitPosition;
        private float heroGaitLastAt=float.NegativeInfinity;
        private bool heroGaitWasMoving,heroStopPlantActive;
        private int heroStopPlantFrame;
        private float heroStopPlantUntil=float.NegativeInfinity;

        // The last genuine, released source-painted Attack/Cast recovery
        // is allowed to settle for one restrained world-frame interval.
        // Keep the SAME entire 256px body; no limb graft, synthetic cell,
        // second hero, new damaging action or actor-root translation.
        // Scoped to the exact GameCore, SaveData, room, equipment, facing
        // and ground pivot, so character swaps / dodges never leak art.
        private const float HeroContactSettleSeconds=.07f;
        private GameCore heroContactSettleOwner;
        private SaveData heroContactSettleSave;
        private ActorAction heroContactSettleAction;
        private int heroContactSettleRoom,heroContactSettleEquipment,
            heroContactSettleFacing,heroContactSettleBody,
            heroContactSettleFrames,heroContactSettleCell;
        private bool heroContactSettleMirrored;
        private Point heroContactSettleFoot;
        private float heroContactSettleAt=float.NegativeInfinity;

        // Use the ORIGINAL five-frame whole-body Hit recovery (not a
        // synthetic overpaint) to bridge the 180ms GameCore hit recoil back
        // into standing idle. Every actual dash, fresh attack, movement,
        // save switch or reduced-motion toggle cancels this visual handoff.
        private const float HeroHitSettleSeconds=.055f;
        private GameCore heroHitSettleOwner;
        private SaveData heroHitSettleSave;
        private int heroHitSettleRoom,heroHitSettleEquipment,
            heroHitSettleFacing,heroHitSettleCell;
        private bool heroHitSettleMirrored;
        private Point heroHitSettleFoot;
        private float heroHitSettleAt=float.NegativeInfinity;
        private bool heroHitSettleActive;

        // East and west share a source row but NOT a facing contract. Cancel
        // painted Hit, contact and gait handoffs on a physical E/W reversal.
        // A contact transfer is considered live only when the COMPLETE set
        // of 18 seven-cell actions exists: no mixed professions/facings.
        // An incomplete art drop leaves all established six-cell art intact.
        private bool heroContactFamilyChecked;
        private bool heroContactFamilyComplete;
        private float heroContactLastCheckedAt=float.NegativeInfinity;
        // Real six/seven-frame family publication waits until an already
        // rendered Attack/Cast's last source contact and recovery finish.
        // No half-action Sprite switch after an Editor hot-reimport.
        private bool heroContactFamilyPending;
        private ActorAction heroContactFamilyDeferredAction;

        private bool HasCompleteHeroContactArt()
        {
            // Art release is atomic for all equipment/directions. Recover
            // after Editor reimport without a scene restart, but never query
            // 18 Resources files on every IMGUI repaint.
            bool sameDeferredAction=heroContactFamilyPending&&
                Game!=null&&Game.playerAction!=null&&
                System.Object.ReferenceEquals(Game.playerAction,
                    heroContactFamilyDeferredAction);
            bool settling=heroContactFamilyPending&&
                Game!=null&&Game.playerAction==null&&
                heroContactSettleAction!=null&&
                clock>=heroContactSettleAt&&
                clock-heroContactSettleAt<HeroContactSettleSeconds;
            if(sameDeferredAction||settling)
                return heroContactFamilyComplete;
            if(heroContactFamilyChecked&&!heroContactFamilyPending&&
                clock>=heroContactLastCheckedAt&&
                clock-heroContactLastCheckedAt<2f)
                return heroContactFamilyComplete;
            bool previouslyChecked=heroContactFamilyChecked;
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
                        string path="Frontier/HeroAnimator/Canonical/"+key;
                        Texture2D atlas=Resources.Load<Texture2D>(path);
                        if(atlas==null||atlas.height!=256||atlas.width!=7*256)
                        {
                            complete=false;
                            continue;
                        }
#if UNITY_5_3_OR_NEWER
                        // Dimensions alone are not an atomic art publication:
                        // Unity can expose a freshly imported 7-cell Texture2D
                        // while its Sprite sub-assets still describe the old
                        // six-cell family. Do not switch every class to seven
                        // cells until every facing has all seven correctly
                        // rooted, class-owned imported Sprite rectangles.
                        Sprite[] imported=Resources.LoadAll<Sprite>(path);
                        bool[] present=new bool[7];
                        int matched=0;
                        foreach(Sprite candidate in imported)
                        {
                            if(candidate==null||candidate.texture!=atlas||
                                Mathf.Abs(candidate.rect.y)>.05f||
                                Mathf.Abs(candidate.rect.width-CanonicalHeroSpriteFactory.Cell)>.05f||
                                Mathf.Abs(candidate.rect.height-CanonicalHeroSpriteFactory.Cell)>.05f||
                                Mathf.Abs(candidate.pixelsPerUnit-
                                    CanonicalHeroSpriteFactory.PixelsPerUnit)>.05f||
                                Vector2.Distance(candidate.pivot,new Vector2(
                                    CanonicalHeroSpriteFactory.FootX,
                                    CanonicalHeroSpriteFactory.Cell-
                                        CanonicalHeroSpriteFactory.FootTop))>.05f)
                                continue;
                            int cell=Mathf.RoundToInt(
                                candidate.rect.x/CanonicalHeroSpriteFactory.Cell);
                            if(cell<0||cell>=present.Length||
                                Mathf.Abs(candidate.rect.x-
                                    cell*CanonicalHeroSpriteFactory.Cell)>.05f||
                                present[cell])continue;
                            present[cell]=true;
                            matched++;
                        }
                        if(matched!=present.Length)complete=false;
#endif
                    }
            if(complete!=heroContactFamilyComplete)
            {
                ActorAction active=Game==null?null:Game.playerAction;
                if(previouslyChecked&&!heroContactFamilyPending&&
                    active!=null&&
                    (active.kind=="attack"||active.kind=="skill"))
                {
                    // Accept a staged complete family, but keep ALL old
                    // cells alive until this exact gameplay action retires.
                    // Next distinct action may immediately publish it.
                    heroContactFamilyPending=true;
                    heroContactFamilyDeferredAction=active;
                    return heroContactFamilyComplete;
                }
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
            heroContactFamilyPending=false;
            heroContactFamilyDeferredAction=null;
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

        private void ApplyHeroStopPlant(bool moving,int facing,
            ref int action,ref int frames,ref int index,ref float position)
        {
            bool validClock=!float.IsNaN(clock)&&!float.IsInfinity(clock);
            int appearance=Game.AppearanceWeapon;
            bool sameContext=validClock&&
                System.Object.ReferenceEquals(heroGaitOwner,Game)&&
                System.Object.ReferenceEquals(heroGaitSave,Game.save)&&
                heroGaitRoom==Game.save.room&&
                heroGaitAppearance==appearance&&heroGaitFacing==facing&&
                heroGaitMirrored==(facing==0&&Game.VisualFacing.x<-.1f)&&
                (Game.Position-heroGaitPosition).Length<1.5f&&
                clock>=heroGaitLastAt;
            if(heroStopPlantActive&&
                (!sameContext||moving||action!=0||
                 Game.save.reducedMotion||clock>=heroStopPlantUntil))
                heroStopPlantActive=false;

            if(moving&&action==1)
            {
                heroStopPlantActive=false;
                if(validClock)
                {
                    heroGaitOwner=Game;heroGaitSave=Game.save;
                    heroGaitRoom=Game.save.room;
                    heroGaitAppearance=appearance;heroGaitFacing=facing;
                    heroGaitMirrored=facing==0&&Game.VisualFacing.x<-.1f;
                    heroGaitPosition=Game.Position;heroGaitLastAt=clock;
                    heroGaitFrame=index;heroGaitWasMoving=true;
                }
                else
                {
                    heroGaitOwner=null;heroGaitSave=null;
                    heroGaitLastAt=float.NegativeInfinity;
                    heroGaitWasMoving=false;
                }
                return;
            }

            if(!moving&&action==0)
            {
                if(heroStopPlantActive&&sameContext&&
                    clock<heroStopPlantUntil&&!Game.save.reducedMotion)
                {
                    action=1;frames=4;index=heroStopPlantFrame;
                    position=heroStopPlantFrame;
                    return;
                }
                heroStopPlantActive=false;
                if(heroGaitWasMoving&&sameContext&&
                    !Game.save.reducedMotion)
                {
                    int previous=heroGaitFrame;
                    heroGaitWasMoving=false;
                    // Authored gait contacts are cells 0 and 2. Keep the
                    // next actual planted cell only; do not replay airborne
                    // cell 3 after the world root has stopped.
                    int planted=previous==1?2:previous==3?0:
                        previous==2?2:-1;
                    float until=clock+HeroStopPlantSeconds;
                    if(planted>=0&&validClock&&
                        !float.IsNaN(until)&&!float.IsInfinity(until)&&
                        until>clock)
                    {
                        heroStopPlantFrame=planted;
                        heroStopPlantUntil=until;
                        heroStopPlantActive=true;
                        action=1;frames=4;index=planted;
                        position=planted;
                        return;
                    }
                }
                else heroGaitWasMoving=false;
                return;
            }

            // Attack, Cast, Dodge, Hit, Death, a direction change, and
            // Reduced Motion immediately regain their own frame authority.
            heroStopPlantActive=false;
            heroGaitWasMoving=false;
            if(validClock)
            {
                heroGaitOwner=Game;heroGaitSave=Game.save;
                    heroGaitRoom=Game.save.room;
                heroGaitAppearance=appearance;heroGaitFacing=facing;
                heroGaitMirrored=facing==0&&Game.VisualFacing.x<-.1f;
                heroGaitPosition=Game.Position;heroGaitLastAt=clock;
            }
            else
            {
                heroGaitOwner=null;heroGaitSave=null;
                    heroGaitLastAt=float.NegativeInfinity;
            }
        }

        // Shared action->Idle handoff for native SpriteRenderer and full
        // source-body OnGUI. This does NOT delay GameCore's actual action
        // completion or hide the next real input; it holds only the last
        // already-rendered recovery cell for <=70ms while stationary.
        private void ApplyHeroContactSettle(bool moving,int facing,
            ref int action,ref int frames,ref int index,ref float position)
        {
            if(drawingOutline)return;
            bool clockValid=!float.IsNaN(clock)&&
                !float.IsInfinity(clock);
            ActorAction live=Game.playerAction;
            if((action==2||action==3)&&live!=null&&live.released&&
                !live.blockedAtContact&&
                clockValid&&!Game.save.reducedMotion&&
                index >= (frames==7?5:frames==6?4:int.MaxValue))
            {
                heroContactSettleOwner=Game;
                heroContactSettleSave=Game.save;
                heroContactSettleAction=live;
                heroContactSettleRoom=Game.save.room;
                heroContactSettleEquipment=Game.AppearanceWeapon;
                heroContactSettleFacing=facing;
                heroContactSettleMirrored=facing==0&&Game.VisualFacing.x<-.1f;
                heroContactSettleBody=action;
                heroContactSettleFrames=frames;
                heroContactSettleCell=index;
                heroContactSettleFoot=Game.Position;
                heroContactSettleAt=clock;
                return;
            }
            // No handoff from an unreleased windup, defeated character,
            // defensive reaction or genuine movement. Never replay old
            // contact art when a different attack is already starting.
            if(live!=null||action!=0||moving||Game.Dying||
                Game.save.reducedMotion||
                // Vanguard Q grants a genuine standing guard AFTER Cast.
                // Pose 9 is a planted guard stance, not a new damaging
                // attack or active locomotion: finish its source-painted
                // Cast recovery before returning to the guarded Idle.
                (Game.PlayerPose>1&&Game.PlayerPose!=9))
            {
                heroContactSettleAction=null;
                return;
            }
            if(heroContactSettleAction==null)return;
            bool same=clockValid&&
                System.Object.ReferenceEquals(heroContactSettleOwner,Game)&&
                System.Object.ReferenceEquals(heroContactSettleSave,Game.save)&&
                heroContactSettleRoom==Game.save.room&&
                heroContactSettleEquipment==Game.AppearanceWeapon&&
                heroContactSettleFacing==facing&&
                heroContactSettleMirrored==(facing==0&&Game.VisualFacing.x<-.1f)&&
                (Game.Position-heroContactSettleFoot).Length<.075f&&
                clock>=heroContactSettleAt&&
                clock-heroContactSettleAt<HeroContactSettleSeconds;
            if(!same)
            {
                heroContactSettleAction=null;
                return;
            }
            action=heroContactSettleBody;
            frames=heroContactSettleFrames;
            index=heroContactSettleCell;
            position=index;
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
            ApplyHeroContactSettle(moving,facing,ref action,ref frames,
                ref index,ref position);
            ApplyHeroHitSettle(moving,facing,ref action,ref frames,
                ref index,ref position);
            ApplyHeroStopPlant(moving,facing,ref action,ref frames,
                ref index,ref position);
            return true;
        }

        private void ApplyHeroHitSettle(bool moving,int facing,
            ref int action,ref int frames,ref int index,ref float position)
        {
            if(drawingOutline)return;
            bool validClock=!float.IsNaN(clock)&&
                !float.IsInfinity(clock);
            if(action==5&&Game.playerHitTime>0f&&
                !Game.Dying&&Game.playerAction==null&&
                !Game.save.reducedMotion&&validClock&&
                frames==5&&index>=3)
            {
                heroHitSettleOwner=Game;
                heroHitSettleSave=Game.save;
                heroHitSettleRoom=Game.save.room;
                heroHitSettleEquipment=Game.AppearanceWeapon;
                heroHitSettleFacing=facing;
                heroHitSettleMirrored=facing==0&&Game.VisualFacing.x<-.1f;
                heroHitSettleFoot=Game.Position;
                heroHitSettleCell=index;
                heroHitSettleAt=clock;
                heroHitSettleActive=true;
                return;
            }
            // No old hit frame may mask a new real state. The source
            // Hit pose is stationary and does not accompany a moving
            // player, alternate equipment or a new damaging action.
            if(action!=0||moving||Game.playerAction!=null||
                Game.ActuallyMoving||Game.Dying||Game.mode!="world"||
                Game.save.reducedMotion)
            {
                heroHitSettleActive=false;
                return;
            }
            if(!heroHitSettleActive)return;
            bool same=validClock&&
                System.Object.ReferenceEquals(heroHitSettleOwner,Game)&&
                System.Object.ReferenceEquals(heroHitSettleSave,Game.save)&&
                heroHitSettleRoom==Game.save.room&&
                heroHitSettleEquipment==Game.AppearanceWeapon&&
                heroHitSettleFacing==facing&&
                heroHitSettleMirrored==(facing==0&&Game.VisualFacing.x<-.1f)&&
                (Game.Position-heroHitSettleFoot).Length<.10f&&
                clock>=heroHitSettleAt&&
                clock-heroHitSettleAt<HeroHitSettleSeconds;
            if(!same){heroHitSettleActive=false;return;}
            action=5;frames=5;index=heroHitSettleCell;
            position=index;
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
            // Confirm a real buffered dodge with a STATIC planted cyan cue.
            // Do not animate/swivel character limbs or invent a second dash.
            // Remains readable with reduced-motion accessibility enabled.
            if(!drawingOutline&&Game.DodgeQueued&&!Game.Dying)
            {
                Ring(Game.Position,.49f,
                    new Color(.48f,.78f,1f,.66f),16);
                // A directional cyan marker names the actually committed
                // escape lane, not any newer stick/key position. It is fixed
                // under reduced motion and retires with the buffer, before
                // the real dash begins animating.
                Point dodgeHeading=Game.DodgeQueuedDirection;
                if(dodgeHeading.Length>.05f)
                    Ring(Game.Position+dodgeHeading.Normalized*.85f,.13f,
                        new Color(.59f,.86f,1f,.82f),8);
            }
            // A real Q-to-LMB (or LMB-to-LMB) buffered press deserves the
            // same immediate feedback as a queued dodge. Twelve fixed amber
            // points sit BELOW the one approved complete-body Sprite, never
            // pose as a swing, never animate in reduced-motion, and vanish
            // the instant gameplay consumes or cancels the pending attack.
            if(!drawingOutline&&Game.AttackQueued&&!Game.Dying)
            {
                Ring(Game.Position,.56f,
                    new Color(.94f,.72f,.42f,.70f),12);
                // Display the committed NEXT attack aim, not the current
                // pointer or the old visible swing. Sword uses a short facing
                // confirmation; ranged loadouts also show the actual saved
                // pointer target. Nothing here spawns a shot or a second body.
                Point followAim=Game.AttackQueuedDirection;
                if(followAim.Length>.05f)
                    Ring(Game.Position+followAim.Normalized*.99f,.14f,
                        new Color(1f,.79f,.48f,.78f),8);
                Point? followTarget=Game.AttackQueuedTarget;
                if(Game.save.weapon>0&&followTarget.HasValue)
                    Ring(followTarget.Value,.23f,
                        new Color(1f,.79f,.48f,.68f),12);
            }
            // The accepted, unpaid 200ms Q queue owns its own static
            // violet contact confirmation. Unlike an attack flash, it
            // cannot imply a spell was released before its authoritative
            // ActorAction contact. Draw beneath the SAME one-piece blonde
            // hero, including during a genuine Hit reaction, without ever
            // changing the Sprite/Animator action or spending energy.
            if(!drawingOutline&&Game.SkillQueued&&!Game.Dying)
            {
                Ring(Game.Position,.63f,
                    new Color(.73f,.56f,1f,.68f),18);
                // The small forward reticle displays the most recent
                // player-approved queued Q aim, NOT a fake skill impact.
                // It remains rooted in world space during Hit/recovery,
                // and immediately follows genuine Q retargeting without
                // a second hero body or an invented projectile.
                Point heading=Game.SkillQueuedDirection;
                if(heading.Length>.05f)
                    Ring(Game.Position+heading.Normalized*1.05f,.17f,
                        new Color(.83f,.67f,1f,.78f),8);
                // Only the standard Ranger volley steers its projectiles
                // toward a world target. Close-range aura skills keep just
                // the foot cue instead of promising a false area impact.
                Point? skillTarget=Game.SkillQueuedTarget;
                if(Game.save.profession==1&&Game.save.technique==0&&
                    skillTarget.HasValue)
                {
                    // Violet means ALL five live spread lanes are clear;
                    // warm amber warns that center can fire but a side
                    // projectile will collide with nearby cover; coral
                    // means even the real center arrow is obstructed.
                    // Read the same five-ray snapshot once for ring+pips.
                    int lanes=Game.SkillQueuedRangerVolleyLaneMask;
                    Color volleyColor=(lanes&4)==0?
                        new Color(1f,.43f,.40f,.78f):
                        lanes==31?
                        new Color(.83f,.67f,1f,.72f):
                        new Color(.98f,.71f,.35f,.77f);
                    Ring(skillTarget.Value,.26f,volleyColor,12);
                    // Five small pips show exactly which projectile paths
                    // reach target depth. No second invented hit location,
                    // altered collision or animation-timer authority.
                    Point dotRoot=ScreenPoint(skillTarget.Value);
                    for(int lane=0;lane<5;lane++)
                        Box(dotRoot.x-13+lane*6,dotRoot.y-18,4,4,
                            (lanes&(1<<lane))!=0?
                            new Color(.83f,.67f,1f,.86f):
                            new Color(1f,.43f,.40f,.86f));
                }
                // A queued default Vanguard/Arcanist Q is an actual
                // player-centered area strike, NOT the Ranger's targeted
                // volley. Preview the SAME physical damage reach using a
                // quiet, nonanimated contour, without painting a fake hit,
                // inventing a spell release, or obscuring the full-body hero.
                float areaReach=Game.SkillQueuedAreaReach;
                if(areaReach>0f)
                {
                    // Trace only the actual unobstructed contact spokes.
                    // Do not paint violet damage-range dots through walls:
                    // the authoritative burst checks ClearAttackPath too.
                    Color reachTint=new Color(.70f,.56f,1f,.25f);
                    for(int i=0;i<32;i++)
                    {
                        double radians=i*Math.PI*2/32;
                        Point contact=Game.Position+new Point(
                            (float)Math.Cos(radians)*areaReach,
                            (float)Math.Sin(radians)*areaReach);
                        if(!Game.SkillQueuedAreaContactVisible(contact))
                            continue;
                        Point mark=ScreenPoint(contact);
                        Box(mark.x-2,mark.y-2,4,4,reachTint);
                    }
                    // Highlight only living, actually visible enemies in
                    // this queued area's CURRENT physical damage footprint.
                    // Static floor-level outlines add tactical readability
                    // without painting another hero, a premature hit, or
                    // altering the future contact/damage transaction.
                    Color candidate=new Color(.96f,.76f,.34f,.54f);
                    foreach(Enemy enemy in Game.enemies)
                        if(Game.SkillQueuedAreaWouldHit(enemy))
                            Ring(enemy.Position,.42f,candidate,12);
                }
            }
            DrawActorContactShadow(p,44,16,Game.WalkFrame,.94f);
            if (Game.racing && !drawingOutline)
                PropImage("skiff", 0, p.x - 51, p.y - 68, 102, 118);
            int look=Game.AppearanceWeapon,pose=Game.PlayerPose;
            if(look<0||look>=FullHeroClasses.Length)return;
            Point direction=Game.VisualFacing;
            Color tint=Game.playerFlash>0 ?
                new Color(1.5f,1.5f,1.5f) : Color.white;
            if(Game.invulnerable>0&&!Game.Dying&&Game.playerFlash<=0)
                // Preserve the clear physical invulnerability signal, but
                // never strobe the blonde full-body Sprite at ~16 Hz while
                // Reduce Motion is enabled. Both formal Animator capture
                // and source-pixel OnGUI use this exact accessibility tint.
                tint.a=Game.save.reducedMotion?.82f:
                    .65f+.35f*(float)Math.Abs(Math.Sin(clock*16));
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

        // Region-specific Boss walking art is a 4x4 grid of complete
        // 384px bodies. Never trust a non-null partial import for live or
        // lethal transition poses; fall back to the accepted Bosses atlas.
        private bool HasAcceptedRegionalBossWalkAtlas(int atlasIndex)
        {
            return bossWalkSheets != null && atlasIndex >= 0 &&
                atlasIndex < bossWalkSheets.Length &&
                ActorAtlasGeometry.IsAcceptedRegionalBossWalkAtlas(bossWalkSheets[atlasIndex]);
        }

        private void DrawEnemyActor(Enemy e)
        {
            if(!IsValidWorldEnemy(e))return;
            bool acceptedBriarBody=e.boss && Game.save.room==1 &&
                ActorAtlasGeometry.IsAcceptedBriarBeastAtlas(briarBeastSheet);
            if(e.boss)
            {
                if(!acceptedBriarBody &&
                    !ActorAtlasGeometry.IsAcceptedBossesAtlas(bossSheet)) return;
            }
            else if(Game.practicing)
            {
                if(!ActorAtlasGeometry.IsAcceptedMonstersAtlas(monsterSheet)) return;
            }
            else
            {
                bool acceptedMossboundBody=e.kind==0 &&
                    ActorAtlasGeometry.IsAcceptedMossboundRaiderAtlas(mossboundRaiderSheet);
                bool acceptedCinderstoneBody=e.kind==2 &&
                    ActorAtlasGeometry.IsAcceptedCinderstoneGuardAtlas(cinderstoneGuardSheet);
                bool acceptedVerticalArcherBody=e.kind==1 &&
                    ActorAtlasGeometry.IsAcceptedVerticalArcherAtlas(enemyArcherVertical) &&
                    Math.Abs(e.facing.y)>Math.Abs(e.facing.x);
                if(!acceptedMossboundBody&&!acceptedCinderstoneBody&&
                    !acceptedVerticalArcherBody&&
                    !ActorAtlasGeometry.IsAcceptedMonstersAtlas(monsterSheet)) return;
            }
            Point p = ScreenPoint(e.Position); float size = e.boss ? BossCanvasSize(Game.save.room) : e.kind == 2 ? 106 : 96;
            // GameCore-release ground contact paints BEFORE the native
            // whole-body Sprite; original boots, weapons and hit frames win.
            // A moving archer can preserve a previous left-facing look
            // while its approved North/South bow Sprite is UNMIRRORED.
            // The ground-release painting follows that real source facing.
            bool uprightArcher=!e.boss&&e.kind==1&&
                ActorAtlasGeometry.IsAcceptedVerticalArcherAtlas(enemyArcherVertical)&&
                Math.Abs(e.facing.y)>Math.Abs(e.facing.x);
            float coverAlpha=e.boss&&
                Math.Abs(e.x-Game.save.x)<1.4f&&
                Game.save.y<e.y-.01f&&Game.save.y>e.y-2.7f?
                .55f:1f;
            DrawAuthoredEnemyReleaseDust(e,p,size,
                e.VisualFlip&&!uprightArcher,coverAlpha);
            DrawActorContactShadow(p,size*.57f,e.boss?24:16,
                e.boss?8+GameCore.GaitFrame(e.moveTime,2.8f):
                    e.kind==0?4:e.kind==1?5:6,.84f);
            // The foot-ring is the only addition; the canonical authored
            // monster/archer/guard full-body sprite remains the same. The
            // same live enemy instance also drives its patrol gait, AI and
            // tracked-journal waypoint. Static dots remain legible with
            // reduced-motion and vanish after the actual credited kill.
            if(!drawingOutline&&Game.IsActiveRidgeWatchPatrol(e))
            {
                // Green: an ordinary quest patrol. Amber: this exact
                // surviving actor has heard a real whistle and is moving
                // to investigate. No new whole-body frames or fake NPC.
                Color patrolTint=Game.IsRidgeWatchResponder(e)?
                    PixelArt.C("#e6b46e"):PixelArt.C("#7acabb");
                Ring(e.Position,.63f,patrolTint,16);
            }
            // The ordinary guard is genuinely investigating an audible
            // physical stone contact. This read-only planted marker lives
            // on its REAL monster body, not on a forged patrol NPC/ID.
            // Body atlas, palette, foot pivot and original movement remain
            // unchanged; no OnGUI outline duplicate or blinking animation.
            if(!drawingOutline&&Game.IsEnemyInvestigatingImpact(e))
            {
                Color suspicion=PixelArt.C("#e6b46e");
                Ring(e.Position,.57f,suspicion,16);
                Text(p.x-74,p.y-125,148,19,
                    Game.L("调查异响","INVESTIGATING"),
                    10,suspicion,TextAnchor.MiddleCenter);
            }
            // Only the living caller owns this painted warning over its
            // original native full-body Sprite. The existing danger atlas
            // animates above its planted feet; a kill, stun or room change
            // removes the signal without leaving an unrelated body marked.
            if(!drawingOutline&&Game.IsRidgeWatchCaller(e))
            {
                Color amber=PixelArt.C("#e6b46e");
                Ring(e.Position,.77f,new Color(
                    amber.r,amber.g,amber.b,.85f),20);
                DrawNpcActionSignal(p,1,e.id,coverAlpha);
                Text(p.x-78,p.y-151,156,22,
                    Game.L("呼援中 · ","CALLING · ")+
                    Game.RidgeWatchCallSecondsLeft.ToString("0.0")+
                    Game.L("秒","s"),12,amber,TextAnchor.MiddleCenter);
            }
            // The courier's single REAL ambusher gets an authored-world
            // warning beside its planted combat feet. Do not swap the
            // enemy's full-body SpriteRenderer frames or create a fake NPC.
            if(!drawingOutline&&Game.IsCivicCourierGuard(e))
            {
                Color warning=PixelArt.C("#e6a45d");
                Ring(e.Position,.78f,warning,20);
                Text(p.x-88,p.y-153,176,21,
                    Game.L("拦路弓手 · 药包伏击","ROAD AMBUSH · MEDIC"),
                    11,warning,TextAnchor.MiddleCenter);
            }
            if(!drawingOutline&&Game.IsCompanionFocusTarget(e))
            {
                // The mark belongs to the SAME living enemy used by the
                // companion's real auto-target logic; no marker for stale
                // numeric IDs, expired orders or unreachable saved actors.
                Ring(e.Position,.82f,PixelArt.C("#f3bd67"),20);
                Text(p.x-83,p.y-131,166,24,
                    Game.L("同伴集火 · "+Game.CompanionFocusSeconds.ToString("0")+"秒",
                        "FOCUS FIRE · "+Game.CompanionFocusSeconds.ToString("0")+"s"),
                    12,gold,TextAnchor.MiddleCenter);
                // Directly reflect the two REAL damage contacts, not a
                // scripted cutscene or newly authored monster paint.
                Text(p.x-86,p.y-110,172,22,
                    Game.CompanionFocusComboSpent ?
                        Game.L("协同命中","COMBO LANDED") :
                        Game.CompanionFocusExposed ?
                        Game.L("破绽已制造 · 等待同伴命中","OPENING · ALLY HIT NEEDED") :
                        Game.L("主角命中制造破绽","HERO HIT TO EXPOSE"),
                    11,Game.CompanionFocusComboSpent?mint:gold,
                    TextAnchor.MiddleCenter);
            }
            if(!drawingOutline&&Game.IsSpecialistDrillTarget(e))
            {
                // Honest class practice shows the actual target's physical
                // feet, real skill contact count and remaining clock. No
                // alternate character or fabricated animated dummy atlas.
                Ring(e.Position,.77f,PixelArt.C("#e7b76b"),20);
                Text(p.x-82,p.y-120,164,22,
                    Game.L("Q实战靶 "+Game.SpecialistDrillHits+"/3",
                        "LIVE Q TARGET "+Game.SpecialistDrillHits+"/3"),
                    12,gold,TextAnchor.MiddleCenter);
                Text(p.x-67,p.y-98,134,20,
                    Game.L("剩余 "+Game.SpecialistDrillTime.ToString("0")+"秒",
                        Game.SpecialistDrillTime.ToString("0")+"s left"),
                    11,mint,TextAnchor.MiddleCenter);
            }
            // The two-wave roadwarden attackers are real combat enemies, but
            // their warm foot ring distinguishes them from roaming patrols.
            // A fixed ring is legible even with reduced-motion effects.
            // Distinguish the REAL two combat jobs at the Roadwarden
            // beacon without replacing mature NPC/monster/Boss sprites:
            // amber hunters rush the ward, violet breakers smash it, and
            // cyan bowmen seek a hero-facing firing lane. Every marker is
            // planted on the SAME physical foot as its whole-body actor
            // and stays legible with accessibility/reduced FX enabled.
            if (!drawingOutline && e.id >= 81000 && e.id < 81100 &&
                Game.RoadwardenTrialRoom == Game.save.room)
                Ring(e.Position, .52f, e.kind==1?
                    PixelArt.C("#8bd6eb"):e.kind==2?
                    PixelArt.C("#cd9deb"):PixelArt.C("#e6a06c"),16);
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
            tint.a=coverAlpha;
            // Other-room bosses lack three distinct authored body rows.
            // Their existing identity is preserved while later HP phases
            // receive a stable, readable warm tint (no extra GUI primitive).
            tint=RegionalBossPhaseTint(tint,e,Game.save.room);
            float phaseCue = e.boss ? BossPhaseCueOpacity(e) : 0f;
            float paintedBossCanvas=size;
            float paintedActorCanvas=size;
            // Keep secondary impact pixels on the SAME rendered facing as
            // the approved whole-body Sprite (upright archer art is unflipped).
            bool paintedActorFlip=e.VisualFlip;
            if (e.boss)
            {
                // Capture the most recently drawn living HP stage. Lethal
                // damage mutates hp before the corpse renderer ever runs.
                BossPhaseCue lastVisible=null;
                if(!drawingOutline)
                {
                    lastVisible=bossPhaseCues.GetValue(e,
                        ignored => new BossPhaseCue());
                    lastVisible.lastVisibleRoom=Game.save.room;
                    lastVisible.lastVisiblePhase=e.BossPhase;
                    lastVisible.lastBriarCell=-1;
                    lastVisible.lastBriarFlip=e.VisualFlip;
                    lastVisible.lastRegionalSheet=null;
                }
                // Keep base body and hit-flash on the identical authored
                // frame, dimensions and foot pivot. Painting the idle sheet
                // over a moving boss previously created a second drifting
                // silhouette whenever a hit-flash outlived the hit pose.
                if (Game.save.room == 1 && ActorAtlasGeometry.IsAcceptedBriarBeastAtlas(briarBeastSheet))
                {
                    // Boss phase, windup and contact use the same GameCore
                    // Sprite in living draws and death handoff.
                    int liveCell = BriarBeastLiveCell(e);
                    Texture2D liveSheet=briarBeastSheet;
                    int liveColumns=4,liveRows=18;
                    float liveSize=size;
                    bool liveFlip=e.VisualFlip;
                    ResolveEnemyStopPlant(e,pose,ref liveSheet,
                        ref liveColumns,ref liveRows,ref liveCell,
                        ref liveSize,liveFlip);
                    if(!drawingOutline)
                    {
                        lastVisible.lastBriarCell=liveCell;
                        lastVisible.lastBriarFlip=liveFlip;
                    }
                    float paintedBossSize=DrawEnemyAtlasHandoff(
                        e,liveSheet,liveColumns,liveRows,liveCell,
                        p + offset,liveSize,tint,liveFlip,pose);
                    paintedBossCanvas=paintedBossSize;
                    // On an HP phase transition the *current* whole-body
                    // silhouette glows briefly, even after the ordinary hit
                    // flash expires. No second offset body or old-phase pose.
                    float flashAlpha = Math.Max(e.flash / .09f * .86f, phaseCue);
                    if (!drawingOutline && flashAlpha > 0f)
                        Actor(art.FlashMask(liveSheet), liveColumns, liveRows,
                            liveCell, p + offset, paintedBossSize,
                            new Color(1, 1, 1, flashAlpha * tint.a),
                            liveFlip);
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
                    HasAcceptedRegionalBossWalkAtlas(walkAtlas);
                Texture2D visibleBossSheet = walking ?
                    bossWalkSheets[walkAtlas] : bossSheet;
                int visibleBossColumns=4;
                int visibleBossRows = walking ? 4 : 8;
                int visibleBossFrame = walking ?
                    row % 4 * 4 + GameCore.GaitFrame(e.moveTime, 2.8f) :
                    row * 4 + frame;
                float visibleBossSize = walking ? size * 384 / 320f : size;
                bool visibleBossFlip=e.VisualFlip;
                ResolveEnemyStopPlant(e,pose,ref visibleBossSheet,
                    ref visibleBossColumns,ref visibleBossRows,
                    ref visibleBossFrame,ref visibleBossSize,
                    visibleBossFlip);
                // Save the ACTUAL whole-body source selected for this repaint.
                // A lethal attack can clear moving/state/facing before the
                // first corpse draw, so reconstructing from dead Enemy is wrong.
                if(!drawingOutline)
                {
                    lastVisible.lastRegionalSheet=visibleBossSheet;
                    lastVisible.lastRegionalRows=visibleBossRows;
                    lastVisible.lastRegionalFrame=visibleBossFrame;
                    // Provisional nominal canvas until real rooted handoff.
                    lastVisible.lastRegionalSize=visibleBossSize;
                    lastVisible.lastRegionalFlip=visibleBossFlip;
                    lastVisible.lastRegionalTint=tint;
                }
                float paintedBossSize=DrawEnemyAtlasHandoff(
                    e,visibleBossSheet,visibleBossColumns,visibleBossRows,
                    visibleBossFrame,p + offset,visibleBossSize,tint,
                    visibleBossFlip,pose);
                paintedBossCanvas=paintedBossSize;
                // A lethal strike DURING the 320 -> 384 canvas handoff
                // must remember the body pixels the player actually saw,
                // not the not-yet-reached walk atlas's nominal 384px size.
                // DrawFallen consumes this exact size on its first repaint.
                if(!drawingOutline)
                    lastVisible.lastRegionalSize=paintedBossSize;
                float flashAlpha = Math.Max(e.flash / .09f * .86f, phaseCue);
                if (!drawingOutline && flashAlpha > 0f && visibleBossSheet != null)
                    Actor(art.FlashMask(visibleBossSheet),
                        visibleBossColumns,visibleBossRows,visibleBossFrame,
                        p + offset,paintedBossSize,
                        new Color(1,1,1,flashAlpha*tint.a),visibleBossFlip);
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
                if (pose == 1 && ActorAtlasGeometry.IsAcceptedWalkMonstersAtlas(monsterWalkSheet)) { sheet = monsterWalkSheet; columns = 4; rows = 3; int gait=GameCore.GaitFrame(e.moveTime,e.kind==0?1.35f:1.8f);frame=Math.Min(2,e.kind)*4+(e.backpedaling?(4-gait)%4:gait); }
                // A NORTH/SOUTH bowman is one identity in ALL gameplay
                // states. Previous idle/run/isolated Hit reverted to
                // WalkMonsters/Monsters, producing a 1-2 frame unrelated
                // creature before (or after) its authored bow attack.
                // Only two complete upright poses exist in the approved
                // 3x2 archer atlas, so locomotion/idle hold the SAME
                // painted bow body; never invent moving limbs or reuse
                // the genuine cell-5 fallen silhouette as a walk cycle.
                // E/W keeps the established accepted Monsters artwork.
                vertical = e.kind == 1 && ActorAtlasGeometry.IsAcceptedVerticalArcherAtlas(enemyArcherVertical) &&
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
                ResolveEnemyStopPlant(e,pose,ref sheet,ref columns,
                    ref rows,ref frame,ref canvas,renderFlip);
                paintedActorFlip=renderFlip;
                RememberEnemyPaint(e,sheet,columns,rows,frame,canvas,tint,renderFlip);
                float paintedEnemySize=DrawEnemyAtlasHandoff(
                    e,sheet,columns,rows,frame,
                    p + offset,canvas,tint,renderFlip,pose);
                paintedActorCanvas=paintedEnemySize;
                if (!drawingOutline && e.flash > 0 && sheet != null) Actor(art.FlashMask(sheet), columns, rows, frame, p + offset, paintedEnemySize, new Color(1, 1, 1, e.flash / .09f * .86f), renderFlip);
            }
            // SOURCE-painted impact fragments respond ONLY to a real
            // GameCore hit reaction. Their pivot follows the actual eased
            // whole-body Boss size and never paints a second head or torso.
            DrawAuthoredEnemyHitSparks(e,p,e.boss?
                paintedBossCanvas:paintedActorCanvas,
                paintedActorFlip,tint.a);
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
            // An enemy-owned, unsaved armor breach countdown sits just above
            // its original poise bar. The real actor remains SpriteRenderer-
            // first; this is the existing combat overlay, not body artwork.
            if(!drawingOutline&&!e.boss&&e.kind==2&&e.armorOpenTime>.001f)
            {
                float y=p.y-size*.72f-7f;
                Box(p.x-17,y,34,5,new Color(.04f,.09f,.12f,.72f));
                Box(p.x-16,y+1,32*Mathf.Clamp01(e.armorOpenTime/GameCore.ArmorBreachSeconds),3,
                    new Color(1f,.69f,.33f,.92f));
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
            if (body == null || !body.IsValid) return;
            EnemyPaint lastEnemyPaint=null;
            EnemyPaint observed;
            if(!body.actor.boss && enemyPaints.TryGetValue(body.actor,out observed) &&
                observed.room==body.room && observed.sheet!=null &&
                observed.columns>0 && observed.rows>0 && observed.size>0f)
                lastEnemyPaint=observed;
            bool acceptedBriarBody=body.actor.boss && body.room==1 &&
                ActorAtlasGeometry.IsAcceptedBriarBeastAtlas(briarBeastSheet);
            if(body.actor.boss)
            {
                if(!acceptedBriarBody &&
                    !ActorAtlasGeometry.IsAcceptedBossesAtlas(bossSheet)) return;
            }
            else
            {
                bool acceptedMossboundBody=body.actor.kind==0 &&
                    ActorAtlasGeometry.IsAcceptedMossboundRaiderAtlas(mossboundRaiderSheet);
                bool acceptedCinderstoneBody=body.actor.kind==2 &&
                    ActorAtlasGeometry.IsAcceptedCinderstoneGuardAtlas(cinderstoneGuardSheet);
                bool acceptedVerticalArcherBody=body.actor.kind==1 &&
                    ActorAtlasGeometry.IsAcceptedVerticalArcherAtlas(enemyArcherVertical) &&
                    (lastEnemyPaint!=null?
                        lastEnemyPaint.sheet==enemyArcherVertical:
                        Math.Abs(body.actor.facing.y)>Math.Abs(body.actor.facing.x));
                if(!acceptedMossboundBody&&!acceptedCinderstoneBody&&
                    !acceptedVerticalArcherBody&&
                    !ActorAtlasGeometry.IsAcceptedMonstersAtlas(monsterSheet)) return;
            }
            Point p = ScreenPoint(body.actor.Position); float progress = body.Progress;
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
                ActorAtlasGeometry.IsAcceptedBriarBeastAtlas(briarBeastSheet))
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
                    HasAcceptedRegionalBossWalkAtlas(walkAtlas);
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
            else if (body.actor.kind == 1 && ActorAtlasGeometry.IsAcceptedVerticalArcherAtlas(enemyArcherVertical) &&
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
                bool walking=body.actor.moving && ActorAtlasGeometry.IsAcceptedWalkMonstersAtlas(monsterWalkSheet);
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
        // Visual-only conversation lifetime. A new speech instance must
        // enter from upright, not from whatever absolute game-clock angle
        // a bystander happened to occupy when dialogue opened.
        private Conversation npcTrackedConversation;
        private WorldObject npcTrackedInterlocutor;
        private float npcTalkStartedAt=float.NegativeInfinity;

        private void DrawNpc(string kind, Point position, int id = -1)
        {
            if (!ActorAtlasGeometry.IsAcceptedNpcAtlas(npcSheet)) return;
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
            // Conversation entrance is the first REAL displayed body
            // frame, not a cover/selection outline. The old preview path
            // started this presentation clock before the NPC was painted,
            // so an interposed UI pass could skip the upright talk entry
            // and make the entire detailed source Sprite snap into a sway.
            if(isTalking&&!drawingOutline&&(
                !System.Object.ReferenceEquals(
                    npcTrackedConversation,Game.conversation)||
                !System.Object.ReferenceEquals(
                    npcTrackedInterlocutor,interlocutor)||
                clock<npcTalkStartedAt))
            {
                npcTrackedConversation=Game.conversation;
                npcTrackedInterlocutor=interlocutor;
                npcTalkStartedAt=clock;
            }
            Matrix4x4 originalMatrix = GUI.matrix;
            try
            {
                if(isTalking && !Game.save.reducedMotion && !drawingOutline)
                {
                    float elapsed=(float)Math.Max(0,clock-npcTalkStartedAt);
                    float duration=CurrentWorldVisualLook.npcTalkEntrySeconds;
                    float entrance=Mathf.Clamp01(elapsed/duration);
                    entrance=entrance*entrance*(3f-2f*entrance);
                    // An intact FULL body sways around the same planted
                    // foot; the first conversation frame stays upright.
                    // Different resident voices vary rhythm, not body art.
                    float tempo=3.2f+(npcKey%3)*.16f;
                    float lean=CurrentWorldVisualLook.npcTalkLeanDegrees*
                        entrance*(float)Math.Sin(elapsed*tempo);
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
                DrawNpcActionSignal(p,0,npcKey,tint.a);
        }
    }
}
