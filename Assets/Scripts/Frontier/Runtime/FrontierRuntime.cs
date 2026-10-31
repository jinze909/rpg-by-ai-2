using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Starfall
{
    public sealed partial class FrontierRuntime : MonoBehaviour
    {
        private const float Width = 1280, Height = 800, Tile = 36;
        public GameCore Game { get; private set; }
        private PixelArt art;
        private FrontierAudio audioSystem;
        private Texture2D hero, titleArt, portraits, worldProps;
        private Font font;
        private GUIStyle textStyle, buttonStyle, disabledButtonStyle;
        private float scale = 1, offsetX, offsetY, clock, saveTimer;
        private int lastRevision = -1, selectedClass;
        private bool hasSave, replacePrompt, moving, applicationPaused, applicationFocused = true;
        private Point camera;
        // Authored map textures remain shared; lightweight tile-chunk readiness
        // prewarms nearby terrain before it crosses the viewport edge.
        private const int TerrainChunkTiles = 6;
        // Each prepared chunk carries actual precomputed tile variants and
        // object-clearance flags; no per-frame mass object scans at render time.
        private sealed class PreparedTerrainChunk
        {
            public float warmAt;
            public readonly int[] seeds = new int[TerrainChunkTiles * TerrainChunkTiles];
            public readonly float[] biome = new float[TerrainChunkTiles * TerrainChunkTiles];
            public readonly bool[] detailOccluded = new bool[TerrainChunkTiles * TerrainChunkTiles];
        }
        private readonly Dictionary<int, PreparedTerrainChunk> preparedTerrainChunks =
            new Dictionary<int, PreparedTerrainChunk>(128);
        // Chunk-specific nearby object bins. A 120x80 map may contain
        // hundreds of props but tile preparation only scans local neighbors.
        private readonly Dictionary<int, List<WorldObject>> terrainOccluderBins =
            new Dictionary<int, List<WorldObject>>(160);
        private static readonly List<WorldObject> emptyTerrainOccluders = new List<WorldObject>(0);
        private void IndexTerrainOccluders(MapData map)
        {
            terrainOccluderBins.Clear();
            int chunksWide=(map.rows[0].Length+TerrainChunkTiles-1)/TerrainChunkTiles;
            int chunksHigh=(map.rows.Length+TerrainChunkTiles-1)/TerrainChunkTiles;
            foreach(WorldObject item in map.objects)
            {
                int centerX=(int)Math.Floor(item.x/TerrainChunkTiles);
                int centerY=(int)Math.Floor(item.y/TerrainChunkTiles);
                // A 1.4 tile object-clearance radius can only overlap
                // the owning 6x6 chunk and its immediate neighbors.
                for(int cy=Math.Max(0,centerY-1);cy<=Math.Min(chunksHigh-1,centerY+1);cy++)
                    for(int cx=Math.Max(0,centerX-1);cx<=Math.Min(chunksWide-1,centerX+1);cx++)
                    {
                        int key=cy*chunksWide+cx;
                        List<WorldObject> candidates;
                        if(!terrainOccluderBins.TryGetValue(key,out candidates))
                        {
                            candidates=new List<WorldObject>(8);
                            terrainOccluderBins.Add(key,candidates);
                        }
                        candidates.Add(item);
                    }
            }
        }
        private int terrainStreamingRoom = -1, terrainStreamingFloor = -1;
        private MapData terrainStreamingMap;
        private bool cameraSkippedTerrain;
        // Only world scenery and interactive props are faded. Player, enemies,
        // their telegraphs, HUD, menus and dialogue remain fully legible.
        private float worldActorOpacity = 1f;
        private Point lastPointer;
        private bool pointerSeen, pointerAimActive;
        // Live enemy reference only; never a serialized lock or reused spawn ID.
        private Enemy controllerAimEnemy, controllerPinnedEnemy, pointerAimEnemy;
        private SaveData controllerPinSave;
        private int controllerPinRoom=-1, controllerPinFloor=-1;
        private static readonly KeyCode[] keyboardControls={KeyCode.Q,KeyCode.Space,KeyCode.E,KeyCode.F,KeyCode.T,KeyCode.Z,KeyCode.P,KeyCode.H,KeyCode.R,KeyCode.V,KeyCode.X,KeyCode.Y,KeyCode.O,KeyCode.U,KeyCode.LeftControl,KeyCode.Escape,KeyCode.Return,KeyCode.Tab,KeyCode.I,KeyCode.K,KeyCode.M,KeyCode.L,KeyCode.B,KeyCode.C,KeyCode.G,KeyCode.N,KeyCode.F1};
        private string bootError;
        private readonly Color ink = PixelArt.C("#101a2b"), panel = PixelArt.C("#19283c"), paper = PixelArt.C("#f2ead7"), muted = PixelArt.C("#a4b4c5"), gold = PixelArt.C("#ebc58b"), mint = PixelArt.C("#7acabb");
        private WorldObject worldLabelOwner,worldLabelNearest;
        private bool worldLabelCullingActive;
        private Point? worldLabelGoal;
        // World props are still depth-sorted, but captions are queued and
        // resolved after all visible actors have proposed their screen-space
        // rectangles. Recycle tickets on each IMGUI pass to avoid GC spikes.
        private sealed class QueuedWorldLabel
        {
            public readonly WorldLabelSlot slot=new WorldLabelSlot();
            public string content;
            public int fontSize;
            public Color color;
            public TextAnchor align;
            public float opacity;
        }
        private readonly List<QueuedWorldLabel> worldLabelTickets=
            new List<QueuedWorldLabel>(80);
        private readonly List<WorldLabelSlot> worldLabelSlots=
            new List<WorldLabelSlot>(80);
        private readonly List<int> worldLabelSelection=new List<int>(80);
        private readonly List<int> worldLabelScratch=new List<int>(80);
        private int queuedWorldLabelCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }
        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != "Frontier" || UnityEngine.Object.FindObjectOfType<FrontierRuntime>() != null) return;
            new GameObject("Starfall Frontier Runtime").AddComponent<FrontierRuntime>();
        }
        private void Awake()
        {
            Application.targetFrameRate = 60;
            try
            {
                TextAsset json = Resources.Load<TextAsset>("Frontier/Campaign");
                if (json == null) throw new InvalidOperationException("Missing Frontier/Campaign.json.");
                string warning; SaveData saved = SaveStore.Read(out warning); hasSave = saved != null;
                Game = new GameCore(JsonUtility.FromJson<Campaign>(json.text), saved);
                if (warning != null) Game.Notify("存档已恢复或重置。", warning);
                art = new PixelArt(); hero = Resources.Load<Texture2D>("Frontier/ViolaMoves");
                titleArt = Resources.Load<Texture2D>("Frontier/Art/TitleVista"); portraits = Resources.Load<Texture2D>("Frontier/Art/Portraits"); worldProps = Resources.Load<Texture2D>("Frontier/Art/WorldProps");
                LoadVisuals();
                font = Resources.Load<Font>("Frontier/Fonts/NotoSansSC-Regular");
                if (font == null) font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "PingFang SC", "Noto Sans CJK SC", "Arial" }, 18);
                audioSystem = new FrontierAudio(gameObject); selectedClass = Game.save.profession;
                camera = CameraTarget();
                PrewarmTerrain();
            }
            catch (Exception ex) { bootError = ex.ToString(); Debug.LogError(bootError); }
        }
        private void Update()
        {
            if (Game == null || applicationPaused || !applicationFocused || VisualReviewActive && VisualReviewPaused) return;
            // Catch up a bounded stalled frame in <=50 ms simulation slices.
            // A single GameCore.Tick deliberately caps its dt at .05, so
            // passing an entire 120 ms frame silently slows walking, enemy
            // attacks, timed quests and cooldowns below 20 FPS.
            float frameDelta = Time.unscaledDeltaTime;
            if (float.IsNaN(frameDelta) || float.IsInfinity(frameDelta) || frameDelta < 0)
                frameDelta = 0;
            frameDelta = Math.Min(frameDelta, .20f); // cap severe stalls; avoid runaway catch-up
            clock += frameDelta; Resize();
#if UNITY_5_3_OR_NEWER
            // Production uses the complete SpriteRenderer hero by default.
            // F8 is a developer safety opt-out to the old COMPLETE OnGUI
            // full-body renderer; it never restores the retired body splice.
            // This boot path is the actual Assets/Scenes/Frontier.unity game.
            if ((Application.isEditor || Debug.isDebugBuild) && Input.GetKeyDown(KeyCode.F8))
            {
                useSpriteRendererHero = !useSpriteRendererHero;
                if (useSpriteRendererHero) heroCaptureBroken = false;
                else ReleaseHeroCapture();
                Game.Notify(useSpriteRendererHero ? "已恢复 SpriteRenderer 完整人物渲染" : "已切换完整人物 OnGUI 安全回退",
                    useSpriteRendererHero ? "Production full-body SpriteRenderer resumed" : "Full-body OnGUI safety fallback enabled");
            }
#endif
            Point movement = new Point(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical") * -1);
            moving = movement.Length > .1f;
            bool beganInWorld = Game.mode == "world";
            Point pointer = MouseVirtual();
            bool pointerMoved=pointerSeen&&(pointer-lastPointer).Length>2;
            bool keyboardMove=Input.GetKey(KeyCode.W)||Input.GetKey(KeyCode.A)||Input.GetKey(KeyCode.S)||Input.GetKey(KeyCode.D)||Input.GetKey(KeyCode.UpArrow)||Input.GetKey(KeyCode.DownArrow)||Input.GetKey(KeyCode.LeftArrow)||Input.GetKey(KeyCode.RightArrow);
            if(movement.Length>.19f&&!keyboardMove||!keyboardMove&&
                (Math.Abs(Input.GetAxisRaw("Combat Aim Horizontal"))>.28f||
                 Math.Abs(Input.GetAxisRaw("Combat Aim Vertical"))>.28f||
                 Math.Abs(Input.GetAxisRaw("Debug Horizontal"))>.5f||
                 Math.Abs(Input.GetAxisRaw("Debug Vertical"))>.5f))controllerUsed=true;
            bool keyboardAction=Input.GetKey(KeyCode.J);
            foreach(KeyCode key in keyboardControls)keyboardAction|=Input.GetKeyDown(key);
            if (pointerMoved || Input.GetMouseButton(0)||keyboardMove||keyboardAction) controllerUsed = false;
            lastPointer = pointer; pointerSeen = true;
            for (int i = 0; i < 10; i++) if (PadDown(i)) controllerUsed = true;
            Point? frameAim = InputAim();
            // Hold-to-sprint never changes modal menu focus. Continuous
            // input is sampled once per rendered frame and replayed through
            // exactly the same bounded substeps as ordinary walking.
            bool sprintHeld=beganInWorld&&
                (Input.GetKey(KeyCode.LeftShift)||
                 Input.GetKey(KeyCode.RightShift)||
                 Input.GetKey(KeyCode.JoystickButton8));
            bool quietHeld=beganInWorld&&
                Input.GetKey(KeyCode.LeftControl)&&!sprintHeld;
            float timeLeft = frameDelta;
            while (timeLeft > .00001f)
            {
                float step = Math.Min(.05f, timeLeft);
                Game.Tick(step, movement, frameAim, sprintHeld, quietHeld);
                timeLeft -= step;
            }
            // Spawn from actual GameCore foot contacts, never from IMGUI
            // repaints or a free-running artificial walking animation.
            AdvanceFootfallVisual(frameDelta);
            UpdatePresentation(frameDelta); UpdateMenuInput(frameDelta);
            if(Game.mode!="world"||!beganInWorld)companionPadGesture.Cancel();
            if (Game.mode == "world" && beganInWorld)
            {
                Point mouse = MouseVirtual();
                // Direct weapon selection is an actual world action, NOT a
                // menu shortcut: choose the equipped full-body weapon before
                // this same rendered frame's attack or Q starts. Equip() is
                // the single authoritative unlock/action/combination guard.
                // Dialogue numbers remain scoped to their original choices.
                bool holdingAlt=Input.GetKey(KeyCode.LeftAlt)||
                    Input.GetKey(KeyCode.RightAlt);
                bool selectingNearbyWithAlt=holdingAlt&&
                    Game.SafeTravel&&Game.NearbyInteractionCount()>1;
                if(selectingNearbyWithAlt)
                {
                    // Press the printed nearby-panel row directly, without
                    // accidentally changing the equipped weapon or
                    // triggering an E/quest effect on that same frame.
                    for(int row=0;row<4;row++)
                        if(Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1+row)))
                        {Game.FocusCrowdedInteractionRow(row);break;}
                }
                else if(!holdingAlt)
                {
                    // Releasing a nearby list or entering combat cannot
                    // turn a held Alt+number into an unintended loadout
                    // swap. Plain 1/2/3 retain their existing meaning.
                    for(int slot=0;slot<3;slot++)
                        if(Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1+slot)))
                        {Game.Equip(slot);break;}
                }
                // R3 is a deliberate physical target-lock toggle. This
                // selects only a visible, attackable live enemy reference;
                // no serialization, target IDs, damage or aim-through-walls.
                // Process it before a same-frame Q / attack captures aim.
                bool pressedR3=PadDown(9);
                bool padSnareChord=pressedR3&&
                    Input.GetKey(KeyCode.JoystickButton8)&&
                    Input.GetKey(KeyCode.JoystickButton3);
                bool padRecoverChord=pressedR3&&
                    Input.GetKey(KeyCode.JoystickButton8)&&
                    Input.GetKey(KeyCode.JoystickButton4);
                if(pressedR3)
                {
                    // L3+R3 is an intentional controller throw chord.
                    // Consume R3 here so it cannot also select another
                    // speaker or toggle combat target lock in this frame.
                    // R3 alone retains every existing original behavior.
                    if(Input.GetKey(KeyCode.JoystickButton8))
                    {
                        if(padRecoverChord)Game.RecoverFieldSnare();
                        else if(padSnareChord)Game.LayFieldSnare(Game.aim);
                        else Game.ThrowPebble(Game.aim);
                    }
                    else
                    {
                    // R3 keeps enemy lock during combat. In a peaceful,
                    // crowded scene it instead cycles the actual E target,
                    // including two speakers sharing the same physical foot.
                    if(Game.SafeTravel&&Game.NearbyInteractionCount()>1)
                        Game.CycleNearbyInteraction();
                    else ToggleControllerTargetLock();
                    }
                }
                if(Input.GetKeyDown(KeyCode.O))
                {
                    // Shift+O selects the ONE reachable, authenticated
                    // current quest source, never pressing E on the player's
                    // behalf. Plain O keeps its existing crowd cycle.
                    if(Input.GetKey(KeyCode.LeftShift)||
                       Input.GetKey(KeyCode.RightShift))
                        Game.FocusNearbyQuestInteraction();
                    else Game.CycleNearbyInteraction();
                }
                // Explicit field continuation shortcut. Shift+Tab is
                // consumed HERE in world mode; plain Tab still opens the
                // complete journal. The same GameCore physical preflight
                // as the mouse HUD decides whether a route is valid.
                if(Input.GetKeyDown(KeyCode.Tab)&&
                   (Input.GetKey(KeyCode.LeftShift)||
                    Input.GetKey(KeyCode.RightShift)))
                    Game.FollowRecommendedAdventure();
                // Right-click an actual nearby actor in the WORLD, not an
                // obstructed prop or UI button. It selects a speaker only;
                // ordinary E / B remains the sole dialogue/quest action.
                if(Input.GetMouseButtonDown(1)&&Game.SafeTravel&&
                   !PointerOverInterface(mouse))
                    Game.FocusNearbyInteractionAt(WorldAtPointer(mouse));
                // The shipped Unity InputManager has a real Mouse ScrollWheel
                // axis. One physical scroll notch advances through the
                // UNLOCKED weapon roster, never an unearned future weapon.
                // Ignore scroll over buttons/inventory to preserve IMGUI's
                // own focus and browsing. This remains a standard Equip().
                float wheel=Input.GetAxisRaw("Mouse ScrollWheel");
                // Shift+wheel over the actual crowded world selects the
                // next visible E source without accidentally changing
                // weapons. An UNMODIFIED wheel outside the nearby list
                // retains all original weapon switching controls.
                bool worldInteractionWheel=Game.SafeTravel&&
                    (Input.GetKey(KeyCode.LeftShift)||
                     Input.GetKey(KeyCode.RightShift))&&
                    Game.NearbyInteractionCount()>1&&
                    !PointerOverInterface(mouse);
                if(Math.Abs(wheel)>.001f&&Game.SafeTravel&&
                   (PointerOverNearbyChoices(mouse)||worldInteractionWheel))
                {
                    // Down selects the next real candidate, up the previous.
                    // The wheel already belonged to this panel; it must not
                    // silently equip a weapon or change the active quest.
                    Game.CycleNearbyInteraction(wheel>0f);
                    controllerUsed=false;
                }
                else if(Math.Abs(wheel)>.001f&&!PointerOverInterface(mouse))
                {
                    int step=wheel>0f?1:-1;
                    for(int offset=1;offset<3;offset++)
                    {
                        int weapon=(Game.save.weapon+3+step*offset)%3;
                        if(weapon!=0&&!Game.Has("weapon_"+weapon))
                            continue;
                        Game.Equip(weapon);
                        break;
                    }
                    // Physical mouse input supersedes a controller-only
                    // voluntary lock and its one unpersisted target ref.
                    ClearControllerTargetLock();
                    controllerUsed=false;
                }
                // Discrete actions take priority over continuously held attacks.
                // A fresh Q after a dodge is queued; a dodge clears older intent.
                if ((Input.GetKeyDown(KeyCode.Space) || PadDown(0))) Game.Dash(movement);
                if ((Input.GetKeyDown(KeyCode.Q) || PadDown(3))&&!padSnareChord)
                    Game.Skill(InputAim(true));
                if (Input.GetMouseButton(0) && !PointerOverInterface(mouse))
                    Game.Attack(InputAim() ?? Game.aim);
                if ((Input.GetKey(KeyCode.J) || Input.GetKey(KeyCode.JoystickButton2))) Game.Attack(InputAim() ?? Game.aim);
                // Shift+E / LB+B is a deliberate close-range nonlethal
                // restraint, NOT a replacement for ordinary NPC E / B.
                // The real snare and still-living enemy are revalidated by
                // GameCore at commit, including the stamina cost and LOS.
                bool bindSnaredKeyboard=Input.GetKeyDown(KeyCode.E)&&
                    (Input.GetKey(KeyCode.LeftShift)||
                     Input.GetKey(KeyCode.RightShift));
                bool bindSnaredPad=PadDown(1)&&
                    Input.GetKey(KeyCode.JoystickButton4);
                if(bindSnaredKeyboard||bindSnaredPad)
                    Game.BindSnaredEnemy();
                else if(Input.GetKeyDown(KeyCode.E)||PadDown(1))
                    Game.Interact();
                if (Input.GetKeyDown(KeyCode.F)) Game.Eat();
                if (Input.GetKeyDown(KeyCode.T)) Game.DrinkTea();
                if (Input.GetKeyDown(KeyCode.Z)) Game.CommandCompanion();
                if (Input.GetKeyDown(KeyCode.P)) Game.ToggleCompanionHold();
                if (Input.GetKeyDown(KeyCode.H)) Game.ReturnToTown();
                if (Input.GetKeyDown(KeyCode.R)) Game.UseExplorationAbility();
                // The noncombat lure has its own real projectile, stamina
                // budget and cooldown; its only alert comes at physical
                // impact, never from a menu or cosmetic button press.
                if (Input.GetKeyDown(KeyCode.U))
                {
                    // Ctrl+Shift+U recovers the nearest untriggered
                    // stake; Shift+U lays; U alone keeps its lure throw.
                    bool shift=Input.GetKey(KeyCode.LeftShift)||
                        Input.GetKey(KeyCode.RightShift);
                    bool ctrl=Input.GetKey(KeyCode.LeftControl)||
                        Input.GetKey(KeyCode.RightControl);
                    if(shift&&ctrl)Game.RecoverFieldSnare();
                    else if(shift)Game.LayFieldSnare(InputAim()??Game.aim);
                    else Game.ThrowPebble(InputAim()??Game.aim);
                }
                // Keep the shipped device's priority. If absent, the real
                // active road marker uses the same immediate Shift+G; G
                // alone and E retain their original menu/dialog ownership.
                if (Input.GetKeyDown(KeyCode.G)&&
                    (Input.GetKey(KeyCode.LeftShift)||
                     Input.GetKey(KeyCode.RightShift)))
                {
                    if(!Game.QuickActivateFieldTactic())
                        Game.QuickActivateRemoteRoadSignal();
                }
                if (Input.GetKeyDown(KeyCode.V)) Game.QuickBraceOuterPatrol();
                if (Input.GetKeyDown(KeyCode.X)) Game.QuickPatchOuterPatrol();
                if (Input.GetKeyDown(KeyCode.Y)) Game.QuickToggleOuterPatrolCompanionGuard();
                // L is reserved for an actual actively defended traveler.
                // No focus/modal/pause switch: the shout is a physical
                // world action, with the very same cost/range/LOS as E.
                if (Input.GetKeyDown(KeyCode.L))
                    Game.QuickDrawWayfarerFire();
                if (PadDown(4)&&!padRecoverChord&&!bindSnaredPad)
                    Game.Eat();
                CompanionButtonAction companionPadAction=companionPadGesture.Advance(
                    Input.GetKey(KeyCode.JoystickButton5),frameDelta);
                if(companionPadAction==CompanionButtonAction.Command)
                    Game.CommandCompanion();
                else if(companionPadAction==CompanionButtonAction.ToggleHold&&
                    Game.save.companion>=0&&!Game.practicing)
                    Game.ToggleCompanionHold();
                if (PadDown(6)) OpenMenu("journal"); if (PadDown(7)) { OpenMenu("pause"); Save(); }
            }
            else if(Game.mode=="fish")
            {
                if(Input.GetKeyDown(KeyCode.F))Game.BraceTidalLine();
                if(Input.GetKeyDown(KeyCode.E)||Input.GetKeyDown(KeyCode.Space))Game.FishHit();
            }
            else if (Game.mode == "dialog" && !dialogueHistoryOpen)
            {
                int count = Game.conversation == null || !DialogueChoicesVisible() ? 0 : Game.conversation.choices.Length;
                // A long chronicle section may span multiple choice pages.
                // 1-6 select only the visible rows, never an off-page entry.
                int start = dialogueChoicePage * DialogueChoicesPerPage;
                for (int i = 0; i < Math.Min(DialogueChoicesPerPage, count - start); i++)
                    if (Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1 + i)))
                    { ChooseDialogue(start + i); break; }
            }
            Toggle(KeyCode.I, "inventory"); Toggle(KeyCode.Tab, "journal"); Toggle(KeyCode.K, "talents"); Toggle(KeyCode.M, "map");
            Toggle(KeyCode.B, "camp"); Toggle(KeyCode.C, "companions"); Toggle(KeyCode.G, "loadout"); Toggle(KeyCode.N, "bounties"); Toggle(KeyCode.F1, "help");
            FollowCamera(frameDelta);
            PrewarmTerrain();
            audioSystem.Update(Game, moving, frameDelta); Game.sound = "";
            saveTimer += frameDelta;
            if (Game.Has("intro") && Game.mode != "title" && (saveTimer > 12 || (Game.revision != lastRevision && saveTimer > 2))) Save();
        }
        private void Toggle(KeyCode key, string mode)
        {
            if (replacePrompt || saveConfirmation != null || key == KeyCode.Tab && Game.mode != "world") return;
            // World Shift+Tab is a single physical Follow action, not a
            // simultaneous click opening the journal on the same frame.
            if(key==KeyCode.Tab&&
               (Input.GetKey(KeyCode.LeftShift)||
                Input.GetKey(KeyCode.RightShift)))return;
            if (settingsParent == "title" && (Game.mode == "pause" || Game.mode == "saves")) return;
            if (!Input.GetKeyDown(key) || Game.mode == "dialog" || Game.mode == "fish" || Game.mode == "title") return;
            // Shift+G is consumed by the live battlefield contraption, not
            // a second click that accidentally opens the loadout modal.
            if(key==KeyCode.G&&
                (Input.GetKey(KeyCode.LeftShift)||Input.GetKey(KeyCode.RightShift)))
                return;
            if (Game.mode == mode) Game.Close(); else OpenMenu(mode);
        }
        private void Resize()
        { scale = Mathf.Max(.01f, Mathf.Min(Screen.width / Width, Screen.height / Height)); offsetX = (Screen.width - Width * scale) / 2; offsetY = (Screen.height - Height * scale) / 2; }
        private Point MouseVirtual()
        { return new Point((Input.mousePosition.x - offsetX) / scale, (Screen.height - Input.mousePosition.y - offsetY) / scale); }
        private bool Save()
        {
            if (VisualReviewActive) return true;
            if (Game == null || !Game.Has("intro")) return false;
            Game.PrepareSave();
            if (SaveStore.Write(Game.save)) { lastRevision = Game.revision; saveTimer = 0; hasSave = true; return true; }
            else { saveTimer = 0; Game.Notify("存档失败，请检查磁盘空间或目录权限。", "Save failed. Check disk space and folder permissions."); return false; }
        }
        private void OnApplicationPause(bool paused) { applicationPaused = paused; if (paused) { if (Game != null) Game.ClearBufferedCombatInputs(); Save(); } }
        private void OnApplicationFocus(bool focused)
        {
            applicationFocused = focused;
            if (!focused && Game != null)
            {
                // Focus loss freezes interactive non-world modes (such as
                // fishing) instead of letting their cursor run unseen.
                // World combat additionally displays the existing pause menu.
                Game.ClearBufferedCombatInputs();
                // Off-window updates already freeze. Do not convert an
                // active death pose to a persistent pause menu: on returning,
                // mandatory respawn must continue without another keypress.
                if (Game.mode == "world" && !Game.Dying) { Game.mode = "pause"; Save(); }
            }
        }
        private void OnApplicationQuit() { Save(); }
        private void OnDestroy()
        {
            ReleaseTerrainPreview();
#if UNITY_5_3_OR_NEWER
            ReleaseActorCapture();
#endif
            if (art != null) art.Dispose();
            ClearUnifiedActionGait();
#if UNITY_5_3_OR_NEWER
            ReleaseHeroCapture();
#endif
            if (audioSystem != null) audioSystem.Dispose();
        }

        private void OnGUI()
        {
            Matrix4x4 previousMatrix = GUI.matrix; Color previousColor = GUI.color; bool previousEnabled = GUI.enabled;
            try { GUI.color = Color.white; GUI.enabled = true; DrawGUI(); }
            finally { GUI.matrix = previousMatrix; GUI.color = previousColor; GUI.enabled = previousEnabled; }
        }
        private void DrawGUI()
        {
            Resize(); GUI.matrix = Matrix4x4.TRS(new Vector3(offsetX, offsetY, 0), Quaternion.identity, new Vector3(scale, scale, 1));
            if (Game == null) { GUI.Label(new Rect(30, 30, 1100, 500), bootError ?? "Loading Starfall Frontier…"); return; }
            // Update handles keyboard navigation; IMGUI must not activate a second focused button.
            if (Event.current.type == EventType.KeyDown || Event.current.type == EventType.KeyUp) Event.current.Use();
            BeginControls();
            if (textStyle == null)
            {
                // Fresh styles cannot inherit the editor skin's high-DPI
                // scaledBackgrounds, which otherwise cover authored controls.
                textStyle = new GUIStyle { font = font, wordWrap = true, richText = false, alignment = TextAnchor.UpperLeft };
                buttonStyle = new GUIStyle { font = font, fontSize = 16, wordWrap = true, richText = false, alignment = TextAnchor.MiddleCenter, padding = new RectOffset(6, 6, 4, 4) };
                buttonStyle.normal.textColor = buttonStyle.hover.textColor = buttonStyle.active.textColor = buttonStyle.focused.textColor = paper;
                disabledButtonStyle = new GUIStyle(buttonStyle);
                disabledButtonStyle.normal.textColor = disabledButtonStyle.hover.textColor = disabledButtonStyle.active.textColor = disabledButtonStyle.focused.textColor = muted;
            }
            Box(0, 0, Width, Height, ink);
            if (Game.mode == "title" && titleArt != null) Image(titleArt, 0, 0, Width, Height);
            else
            {
                DrawWorld();
                if (Game.mode == "world") DrawNavigationTargetMarker();
            }
            if (Game.mode == "title") DrawTitle();
            else
            {
                if (Game.mode == "world" || Game.mode == "dialog" || Game.mode == "fish") DrawHud();
                if (Game.mode == "dialog") DrawDialogue();
                if (Game.mode == "fish") DrawFishing();
                if (Game.mode == "inventory") DrawInventory();
                if (Game.mode == "talents") DrawTalents();
                if (Game.mode == "journal") DrawJournal();
                if (Game.mode == "map") DrawMap();
                if (Game.mode == "pause") DrawPause();
                if (Game.mode == "saves") DrawSaves();
                if (Game.mode == "camp") DrawCamp();
                if (Game.mode == "companions") DrawCompanions();
                if (Game.mode == "loadout") DrawLoadout();
                if (Game.mode == "bounties") DrawBounties();
                if (Game.mode == "help") DrawHelp();
            }
            if (Game.toastTime > 0 && Game.mode != "title") DrawToast();
            DrawHudThreatWitness();
            EndControls();
            GUI.matrix = Matrix4x4.identity; GUI.color = Color.white;
        }
        private void Box(float x, float y, float w, float h, Color color)
        {
            if (drawingOutline) return;
            Color previous = GUI.color;
            try { color.a*=worldActorOpacity; GUI.color = color;
                GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture); }
            finally { GUI.color = previous; }
        }
        private void Frame(float x, float y, float w, float h, Color color)
        { Box(x, y, w, 1, color); Box(x, y + h - 1, w, 1, color); Box(x, y, 1, h, color); Box(x + w - 1, y, 1, h, color); }
        private void Text(float x, float y, float w, float h, string text, int size, Color color, TextAnchor anchor = TextAnchor.UpperLeft)
        {
            textStyle.fontSize = Game != null && Game.save.largeText && size < 20 ? size + 1 : size; color.a*=worldActorOpacity; textStyle.normal.textColor = color; textStyle.alignment = anchor;
            GUI.Label(new Rect(x, y, w, h), text, textStyle);
        }
        // The same label system serves all authored props, expansion sites
        // and polished NPCs. Priority belongs to the actual objective and
        // interaction winner, not to every point of interest in the atlas.
        private void WorldObjectLabel(float x,float y,float w,float h,
            string content,int size,Color color,
            TextAnchor anchor=TextAnchor.UpperLeft)
        {
            if(worldLabelOwner==null||String.IsNullOrEmpty(content))return;
            // Editor review/preview draws the actual source directly: only
            // the live world pass has the complete candidates for sorting.
            if(!worldLabelCullingActive)
            {Text(x,y,w,h,content,size,color,anchor);return;}
            Point foot=new Point(worldLabelOwner.x,worldLabelOwner.y);
            float distance=(foot-Game.Position).Length;
            bool objective=worldLabelGoal.HasValue&&
                (foot-worldLabelGoal.Value).Length<1.25f;
            int priority=WorldLabelPolicy.Priority(distance,objective,
                System.Object.ReferenceEquals(worldLabelNearest,worldLabelOwner));
            if(priority==0&&distance<=5.5f)priority=1;
            string kind=worldLabelOwner.kind;
            bool urgent=(kind=="unsentrelay"&&Game.UnsentWarningRunActive)||
                (kind.StartsWith("wayfarer")&&
                    Game.WayfarerEncounterRoom==Game.save.room)||
                (kind.StartsWith("fieldstation")&&
                    Game.FieldStationSheltersHero(worldLabelOwner))||
                (kind=="outerrelay"&&Game.OuterSignalRouteReady)||
                (kind=="waytrail"&&Game.Has("civic_aftercare_published")&&
                    Game.Has("civic_aftercare_waytrail_"+Game.save.room));
            if(urgent)priority=4;
            if(priority==0||x+w<16||x>Width-16||y+h<16||y>Height-16)return;
            if(queuedWorldLabelCount==worldLabelTickets.Count)
                worldLabelTickets.Add(new QueuedWorldLabel());
            QueuedWorldLabel entry=worldLabelTickets[queuedWorldLabelCount++];
            entry.slot.owner=worldLabelOwner;
            entry.slot.x=x;entry.slot.y=y;
            entry.slot.width=w;entry.slot.height=h;
            entry.slot.distance=distance;
            entry.slot.priority=priority;entry.slot.urgent=urgent;
            entry.content=content;entry.fontSize=size;
            entry.color=color;entry.align=anchor;
            entry.opacity=worldActorOpacity;
            worldLabelSlots.Add(entry.slot);
        }
        private void FlushWorldLabels()
        {
            // Reserve the highest-value captions in screen space before
            // rendering a single line. An ordinary sign can never cover a
            // live quest objective merely because it had a lower map index.
            WorldLabelPolicyExtensions.Select(worldLabelSlots,
                worldLabelSelection,worldLabelScratch);
            foreach(int index in worldLabelSelection)
            {
                QueuedWorldLabel entry=worldLabelTickets[index];
                Color tint=entry.color;
                tint.a*=entry.opacity;
                Text(entry.slot.x,entry.slot.y,
                    entry.slot.width,entry.slot.height,
                    entry.content,entry.fontSize,tint,entry.align);
            }
        }
        private bool Button(float x, float y, float w, float h, string text, bool active = false, bool enabled = true)
        {
            if ((replacePrompt || saveConfirmation != null) && !drawingModal) enabled = false;
            Rect r = new Rect(x, y, w, h); Point mouse = MouseVirtual(); bool hover = mouse.x >= x && mouse.x < x + w && mouse.y >= y && mouse.y < y + h;
            string id = ControlId(x, y, w, h); bool keyboard = focusVisible && navigation.focused == id && enabled;
            bool press = enabled && ((hover && Input.GetMouseButton(0)) || pressedTime > 0 && pressedControl == id);
            Color fill = !enabled ? PixelArt.C("#182636") : press ? PixelArt.C("#49636b") : keyboard || hover ? PixelArt.C("#304458") : active ? PixelArt.C("#36444c") : PixelArt.C("#213247");
            Panel(x, y, w, h, fill, keyboard ? mint : active ? gold : hover && enabled ? mint : PixelArt.C("#405269")); if (active) Box(x + 1, y + 7, 3, h - 14, gold); if (keyboard) Frame(x + 3, y + 3, w - 6, h - 6, mint);
            bool confirm = RegisterControl(x, y, w, h, enabled);
            bool previousEnabled = GUI.enabled; bool click;
            try { GUI.enabled = enabled; click = GUI.Button(r, text, enabled ? buttonStyle : disabledButtonStyle); }
            finally { GUI.enabled = previousEnabled; }
            if (click) { focusVisible = false; navigation.Select(id); Game.sound = "ui"; } return enabled && (click || confirm);
        }
        private void Image(Texture2D texture, float x, float y, float w, float h, Color? tint = null)
        { Color previous = GUI.color; try { Color actual=tint??Color.white;
                actual.a*=worldActorOpacity; GUI.color=actual;
                GUI.DrawTexture(new Rect(x,y,w,h),texture); }
            finally { GUI.color = previous; } }
        private Point WorldOrigin { get { return new Point(Width / 2 - (float)Math.Round(camera.x * Tile) + (float)Math.Round(shakeX), 390 - (float)Math.Round(camera.y * Tile) + (float)Math.Round(shakeY)); } }
        private Point ScreenPoint(Point p)
        { Point origin = WorldOrigin; return origin + new Point((float)Math.Round(p.x * Tile), (float)Math.Round(p.y * Tile)); }
        private Point WorldAtPointer(Point mouse) { Point origin = WorldOrigin; return (mouse - origin) * (1f / Tile); }
        private Point AimTargetAtPointer(Point mouse)
        {
            Point target=WorldAtPointer(mouse);float best=float.MaxValue;
            pointerAimEnemy=null;
            // The art is drawn above its ground-space hurtbox. Clicking its
            // torso must aim at that actor, not at empty ground behind its head.
            // Preserve exact live reference so ranged hand attachments may
            // predict its REAL motion, never nearby ground or a dead sprite.
            foreach(Enemy enemy in Game.enemies)
            {
                if(enemy.hp<=0f||
                   !Contains(EnemyHudBounds(enemy),mouse))continue;
                Point center=ScreenPoint(enemy.Position)+
                    new Point(0,enemy.boss?-90:-35);
                float distance=(center-mouse).Length;
                if(distance<best)
                {best=distance;target=enemy.Position;pointerAimEnemy=enemy;}
            }
            return target;
        }
        private void ClearControllerTargetLock()
        {
            controllerPinnedEnemy=null;controllerPinSave=null;
            controllerPinRoom=controllerPinFloor=-1;
        }
        private bool ControllerTargetLockValid()
        {
            if(controllerPinnedEnemy==null)return false;
            if(!System.Object.ReferenceEquals(controllerPinSave,Game.save)||
                controllerPinRoom!=Game.save.room||
                controllerPinFloor!=Game.save.abyssFloor||
                !Game.enemies.Contains(controllerPinnedEnemy)||
                controllerPinnedEnemy.hp<=0f)
                return false;
            Point to=controllerPinnedEnemy.Position-Game.Position;
            return System.Object.ReferenceEquals(
                Game.AcquireRangedStickTarget(to,controllerPinnedEnemy),
                controllerPinnedEnemy);
        }
        private void ToggleControllerTargetLock()
        {
            if(controllerPinnedEnemy!=null)
            {
                ClearControllerTargetLock();
                // Return to ordinary twin-stick aim on the same frame.
                InputAim();
                return;
            }
            Point stick=new Point(Input.GetAxisRaw("Combat Aim Horizontal"),
                -Input.GetAxisRaw("Combat Aim Vertical"));
            Point look=stick.Length>=.28f?stick:Game.aim;
            Enemy target=Game.AcquireRangedStickTarget(look);
            if(target==null)
            {
                Game.Notify("前方没有可锁定的远程目标。",
                    "No visible ranged target in front.");
                return;
            }
            controllerPinnedEnemy=target;
            controllerPinSave=Game.save;
            controllerPinRoom=Game.save.room;
            controllerPinFloor=Game.save.abyssFloor;
            controllerAimEnemy=target;
            Game.SetAimTarget(target.Position);
            Game.SetAim((target.Position-Game.Position).Normalized);
        }
        private Point? InputAim(bool technique=false)
        {
            if(Game.mode!="world")
            {
                ClearControllerTargetLock();
                pointerAimEnemy=null;
                controllerAimEnemy=null;Game.SetAimTarget(null);return null;
            }
            if(controllerPinnedEnemy!=null&&!ControllerTargetLockValid())
                ClearControllerTargetLock();
            if(controllerUsed)
            {
                pointerAimEnemy=null;
                // A voluntary R3 lock tracks an actual visible enemy even
                // after the right stick returns to neutral. At invalidation
                // instantly fall back to normal free-angle twin-stick aim.
                Point stick=new Point(
                    Input.GetAxisRaw("Combat Aim Horizontal"),
                    -Input.GetAxisRaw("Combat Aim Vertical"));
                controllerAimEnemy=controllerPinnedEnemy!=null?
                    controllerPinnedEnemy:
                    Game.AcquireRangedStickTarget(stick,controllerAimEnemy);
                if(controllerAimEnemy!=null)
                {
                    Point lockedTarget=controllerAimEnemy.Position;
                    // Only flying ranged attacks receive predictive lead.
                    // Standard Vanguard/Arcanist Q is rooted at the hero,
                    // and sword strikes must keep the true visible center.
                    float rangedSpeed=Game.RangedAimProjectileSpeed(technique);
                    if(rangedSpeed>0f)
                        lockedTarget=Game.PredictRangedTargetPoint(
                            controllerAimEnemy,rangedSpeed,
                            Game.RangedAimWindupSeconds(technique),
                            technique);
                    Game.SetAimTarget(lockedTarget);
                    return (lockedTarget-Game.Position).Normalized;
                }
                Game.SetAimTarget(null);
                return stick.Length>=.28f?(Point?)stick.Normalized:null;
            }
            ClearControllerTargetLock();
            controllerAimEnemy=null;
            Point mouse=MouseVirtual();
            if(PointerOverInterface(mouse))
                return pointerAimActive?(Point?)Game.aim:null;
            pointerAimActive=true;
            Point target=AimTargetAtPointer(mouse);
            // Mouse aim keeps free ground clicks pixel-precise. Only when
            // the cursor is actually over a LIVE enemy does its bow/staff
            // shot gain the same physically verified leading solution as
            // controller aim. No new auto-target radius or virtual damage.
            float pointerSpeed=Game.RangedAimProjectileSpeed(technique);
            if(pointerAimEnemy!=null&&pointerSpeed>0f)
                target=Game.PredictRangedTargetPoint(
                    pointerAimEnemy,pointerSpeed,
                    Game.RangedAimWindupSeconds(technique),
                    technique);
            Point d=target-Game.Position;
            Game.SetAimTarget(target);
            return d.Length>.05f?d:Game.aim;
        }
        private Point CameraTarget()
        {
            // The safe world view lies between the left status/hint panels and
            // the right quest panel. Clamping to the full window pinned actors
            // at the eastern boss arenas underneath the HUD.
            float left = (Width / 2 - 330) / Tile, right = (988 - Width / 2) / Tile;
            float top = (390 - 105) / Tile, bottom = (675 - 390) / Tile;
            return new Point(GameCore.Clamp(Game.save.x, left, Game.Map.rows[0].Length - right), GameCore.Clamp(Game.save.y, top, Game.Map.rows.Length - bottom));
        }
        private void FollowCamera(float dt)
        {
            if(Game.mode!="world"){cameraSkippedTerrain=false;return;}
            Point before=camera;
            Point target=CameraTarget();
            // Snap actual room-scale travel immediately. The same pure
            // shared rule remains smooth during ordinary movement, and the
            // existing streaming jump detection below prewarms the new view.
            camera=WorldStreaming.FollowCamera(before,target,dt,
                Game.save.reducedMotion);
            // A long ridge shortcut, fast-travel jump or accessibility snap
            // must never reveal an empty mid-screen tile waiting for a fade.
            cameraSkippedTerrain=Game.save.reducedMotion||
                WorldStreaming.LargeCameraJump(camera.x-before.x,camera.y-before.y);
        }
        private void Ring(Point p, float radius, Color color, int segments = 32)
        {
            Point s = ScreenPoint(p);
            for (int i = 0; i < segments; i++)
            { double a = i * Math.PI * 2 / segments; Box(s.x + (float)Math.Cos(a) * radius * Tile - 2, s.y + (float)Math.Sin(a) * radius * Tile - 2, 4, 4, color); }
        }
        private void DrawDroppedCourierMedicine()
        {
            if(!Game.OuterDeliveryParcelDropped)return;
            Point foot=Game.OuterDeliveryParcelFoot;
            Point p=ScreenPoint(foot);
            // Draw near the camera only. The existing quest navigation
            // already guides distant players to this exact lost foot.
            if(p.x< -85f||p.y< -95f||
               p.x>Width+85f||p.y>Height+45f)return;
            bool reachable=Game.OuterDeliveryCanRecoverParcel;
            Color color=reachable?mint:gold;
            Ring(foot,.45f,new Color(color.r,color.g,color.b,.85f),18);
            // Reuse actual installed medical-herb Icon art. This is an
            // in-world objective marker, not an extra actor/OnGUI hero.
            Icon("herb",p.x-17f,p.y-38f,34f,Color.white);
            Text(p.x-103f,p.y-76f,206f,21f,
                reachable?
                    Game.L("E · 捡回药布","E · RECOVER DRESSINGS"):
                    Game.L("遗失药布 · 返回落点","LOST DRESSINGS · RETURN"),
                11,color,TextAnchor.MiddleCenter);
        }

        // The visible window is surrounded by precomputed terrain, not a
        // timestamp-only pretend load. Movement sees the next chunks while
        // they are still at least ten tiles beyond the edge of the screen.
        private void PrewarmTerrain()
        {
            if (Game == null) return;
            bool firstView = terrainStreamingRoom != Game.save.room ||
                terrainStreamingFloor != Game.save.abyssFloor ||
                !object.ReferenceEquals(terrainStreamingMap,Game.Map);
            if (firstView)
            {
                preparedTerrainChunks.Clear();
                IndexTerrainOccluders(Game.Map);
                terrainStreamingRoom = Game.save.room;
                terrainStreamingFloor = Game.save.abyssFloor;
                terrainStreamingMap = Game.Map;
            }
            Point origin = WorldOrigin;
            int width = Game.Map.rows[0].Length, height = Game.Map.rows.Length;
            const int preloadTiles = 12;
            // Prewarm the side the camera is moving *toward*, not just a
            // symmetric strip around its delayed follow position.
            Point target=CameraTarget();
            int leadX=WorldStreaming.PredictPrewarmLead(target.x-camera.x);
            int leadY=WorldStreaming.PredictPrewarmLead(target.y-camera.y);
            int left = Math.Max(0, (int)Math.Floor((-Tile - origin.x) / Tile) - preloadTiles + Math.Min(0,leadX));
            int right = Math.Min(width - 1, (int)Math.Ceiling((Width + Tile - origin.x) / Tile) + preloadTiles + Math.Max(0,leadX));
            int top = Math.Max(0, (int)Math.Floor((-Tile - origin.y) / Tile) - preloadTiles + Math.Min(0,leadY));
            int bottom = Math.Min(height - 1, (int)Math.Ceiling((Height - origin.y) / Tile) + preloadTiles + Math.Max(0,leadY));
            int chunksWide = (width + TerrainChunkTiles - 1) / TerrainChunkTiles;
            for (int cy = top / TerrainChunkTiles; cy <= bottom / TerrainChunkTiles; cy++)
                for (int cx = left / TerrainChunkTiles; cx <= right / TerrainChunkTiles; cx++)
                {
                    int key = cy * chunksWide + cx;
                    if (preparedTerrainChunks.ContainsKey(key)) continue;
                    PreparedTerrainChunk chunk = new PreparedTerrainChunk();
                    chunk.warmAt = firstView || cameraSkippedTerrain ? clock-2f : clock;
                    List<WorldObject> closeObjects;
                    if(!terrainOccluderBins.TryGetValue(key,out closeObjects))
                        closeObjects=emptyTerrainOccluders;
                    for (int localY = 0; localY < TerrainChunkTiles; localY++)
                        for (int localX = 0; localX < TerrainChunkTiles; localX++)
                        {
                            int x = cx * TerrainChunkTiles + localX;
                            int y = cy * TerrainChunkTiles + localY;
                            if (x >= width || y >= height) continue;
                            int offset = localY * TerrainChunkTiles + localX;
                            int seed = TileSeed(x, y, Game.save.room);
                            chunk.seeds[offset] = seed;
                            chunk.biome[offset] = WorldStreaming.BiomeVariation(x,y,Game.save.room);
                            if (seed % 17 != 0) continue;
                            float footX = x + .35f + seed % 5 * .07f;
                            float footY = y + .65f;
                            foreach (WorldObject item in closeObjects)
                            {
                                float dx = footX - item.x, dy = footY - item.y;
                                if (dx * dx + dy * dy < 1.96f)
                                { chunk.detailOccluded[offset] = true; break; }
                            }
                        }
                    preparedTerrainChunks.Add(key, chunk);
                }
            cameraSkippedTerrain=false;
        }
        private PreparedTerrainChunk FindTerrainChunk(int x, int y)
        {
            if(terrainStreamingRoom!=Game.save.room||
                terrainStreamingFloor!=Game.save.abyssFloor||
                !object.ReferenceEquals(terrainStreamingMap,Game.Map))return null;
            PreparedTerrainChunk state;
            int wide = (Game.Map.rows[0].Length + TerrainChunkTiles - 1) / TerrainChunkTiles;
            preparedTerrainChunks.TryGetValue(y / TerrainChunkTiles * wide +
                x / TerrainChunkTiles, out state);
            return state;
        }
        private int PreparedTileSeed(int x, int y)
        {
            PreparedTerrainChunk chunk = FindTerrainChunk(x, y);
            return chunk == null ? TileSeed(x, y, Game.save.room) :
                chunk.seeds[y % TerrainChunkTiles * TerrainChunkTiles + x % TerrainChunkTiles];
        }
        private float PreparedBiomeVariation(int x,int y)
        {
            PreparedTerrainChunk chunk=FindTerrainChunk(x,y);
            if(chunk!=null)return chunk.biome[y%TerrainChunkTiles*TerrainChunkTiles+
                x%TerrainChunkTiles];
            // Fallback for standalone editor IMGUI paint without Update.
            return WorldStreaming.BiomeVariation(x,y,Game.save.room);
        }
        private bool GroundDetailOccluded(int x, int y)
        {
            PreparedTerrainChunk chunk = FindTerrainChunk(x, y);
            if (chunk != null)
                return chunk.detailOccluded[y % TerrainChunkTiles * TerrainChunkTiles + x % TerrainChunkTiles];
            // Offline OnGUI screenshot may run without Update. Preserve the
            // exact prior behavior without claiming an uncached tile is clear.
            int seed = TileSeed(x, y, Game.save.room);
            float wx = x + .35f + seed % 5 * .07f, wy = y + .65f;
            foreach (WorldObject item in Game.Map.objects)
            {
                float dx = wx - item.x, dy = wy - item.y;
                if (dx * dx + dy * dy < 1.96f) return true;
            }
            return false;
        }
        private float TerrainChunkOpacity(int cx, int cy)
        {
            if(terrainStreamingRoom!=Game.save.room||
                terrainStreamingFloor!=Game.save.abyssFloor||
                !object.ReferenceEquals(terrainStreamingMap,Game.Map))return 1f;
            if (cx < 0 || cy < 0) return 1f;
            int width = (Game.Map.rows[0].Length + TerrainChunkTiles - 1) / TerrainChunkTiles;
            int height = (Game.Map.rows.Length + TerrainChunkTiles - 1) / TerrainChunkTiles;
            if (cx >= width || cy >= height) return 1f;
            PreparedTerrainChunk chunk;
            if (!preparedTerrainChunks.TryGetValue(cy * width + cx, out chunk))
                return 1f; // Render ready rather than leaving missing black squares.
            return WorldStreaming.Smooth01((clock-chunk.warmAt)/.45f);
        }
        private float TerrainTileOpacity(int x, int y)
        {
            if (Game.save.reducedMotion || Game.save.effectsQuality == 0) return 1f;
            // Blend four neighboring chunk maturities continuously across the
            // boundary. A single cold chunk cannot produce a 6x6 square pop-in.
            float fx = (x + .5f) / TerrainChunkTiles - .5f;
            float fy = (y + .5f) / TerrainChunkTiles - .5f;
            int cx = (int)Math.Floor(fx), cy = (int)Math.Floor(fy);
            return WorldStreaming.Blend(
                TerrainChunkOpacity(cx,cy), TerrainChunkOpacity(cx+1,cy),
                TerrainChunkOpacity(cx,cy+1), TerrainChunkOpacity(cx+1,cy+1),
                fx-cx,fy-cy);
        }
        private void DrawWorld()
        {
            // An incomplete first Editor Resources import must not leave
            // authored ground, rivers, lamps and signposts as missing/flat
            // pixels for the remainder of this gameplay session.
            RecoverPublishedVisualAtlases();
            // An interaction may enter another room between Update and the
            // first IMGUI repaint. Prime the actual viewport BEFORE painting,
            // so a missing chunk never arrives as an opaque block.
            PrewarmTerrain();
            worldOverlayRegions.Clear();
            worldLabelCullingActive=true;
            worldLabelSlots.Clear();
            queuedWorldLabelCount=0;
            worldLabelOwner=null;
            worldLabelNearest=Game.Nearest();
            QuestGuide currentObjective=Game.RecommendedContinuationGuide;
            worldLabelGoal=currentObjective!=null&&
                currentObjective.room==Game.save.room&&
                !currentObjective.complete&&!currentObjective.locked?
                (Point?)currentObjective.target:null;
            Point? navigation=Game.NavigationTarget();
            if(navigation.HasValue)worldLabelGoal=navigation;
            Color ground = PixelArt.C(Game.Map.color);
            Box(0, 0, Width, Height, Color.Lerp(ground, ink, .55f));
            // Screen-bounded terrain work. The old full-map scan needlessly
            // visited offscreen cells for every IMGUI pass as maps expanded.
            Point origin = WorldOrigin;
            int minX = Math.Max(0, (int)Math.Floor((-Tile - origin.x) / Tile));
            int maxX = Math.Min(Game.Map.rows[0].Length - 1, (int)Math.Ceiling((Width - origin.x) / Tile));
            int minY = Math.Max(0, (int)Math.Floor((-Tile - origin.y) / Tile));
            int maxY = Math.Min(Game.Map.rows.Length - 1, (int)Math.Ceiling((Height - origin.y) / Tile));
            Color fadeBase = Color.Lerp(ground, ink, .55f);
            for (int y = minY; y <= maxY; y++) for (int x = minX; x <= maxX; x++)
            {
                Point p = ScreenPoint(new Point(x, y));
                char tile = Game.Map.rows[y][x];
                if(tile=='?')tile=Game.FarBridgeOpen(Game.save.room)?'=':'#';
                if(tile=='!')tile=Game.RidgeBreachOpen(Game.save.room)?'=':
                    (Game.save.room==2||Game.save.room==5?'~':'#');
                if (tile == '%' && !Game.LowTide) tile = '~';
                if ((tile == 'v' || tile == 'w' || tile == 'b') && Game.Has("seal_" + Game.save.room)) tile = '=';
                // Keep original full-detail terrain and tile edges as the
                // only baseline ground art. Auxiliary biome rectangles were
                // dulling authored road, snow and river textures.
                DrawSurface(x, y, tile, p);
                DrawGroundDetail(x, y, tile, p); DrawTerrainEdge(x, y, tile, p);
                DrawAuthoredWaterShimmer(x,y,tile,p);
                DrawAuthoredWaterRipples(x,y,tile,p);
                DrawAuthoredShorelineWash(x,y,tile,p);
                float reveal = TerrainTileOpacity(x, y);
                if (reveal < .999f)
                    Box(p.x, p.y, Tile + .5f, Tile + .5f, new Color(fadeBase.r,fadeBase.g,fadeBase.b,
                        WorldStreaming.TerrainVeilOpacity(reveal)));
            }
            // Atmosphere belongs in the world background. Painting motes
            // AFTER the depth-sorted cast made stars crawl over faces,
            // equipped weapons, hit flashes and red combat telegraphs.
            // Terrain is already in place; all game actors/props/HUD now
            // occlude these subtle authored particles naturally.
            DrawAuthoredAtmosphericMotes();
            // Shutter darkness belongs to TERRAIN and weather, not to the
            // canonical gold-haired hero or any complete-body actor Sprite.
            // Paint it BEFORE the sorted character pass.
            DrawSilentLedgerWorldLighting();
            DrawArenaBoundary();
            // Sort scenery and actors by their ground position, for proper overlap.
            foreach (Effect floorEffect in Game.effects) if (IsGroundEffect(floorEffect)) DrawCombatEffect(floorEffect);
            DrawOrderedWorld();
            // Show the repaired PUBLIC signal as an actual nearby world
            // interaction, not an invisible menu-only perk. Its outline
            // follows the existing authored station's real physical foot.
            // Only ONE of the widely separated devices appears nearby.
            if(Game.mode=="world"&&Game.save.room>=0&&
                Game.save.room<8&&Game.RegionalMachineDone(Game.save.room))
            {
                foreach(WorldObject station in Game.Map.objects)
                {
                    if(station==null||station.kind!="regionalMachine")
                        continue;
                    Point foot=new Point(station.x,station.y);
                    if((foot-Game.Position).Length>5.4f)continue;
                    float cooldown=Game.RegionalSignalSecondsLeft(station);
                    if(cooldown<0f)continue;
                    Color tone=cooldown>.01f?
                        new Color(.60f,.72f,.81f,.7f):mint;
                    Ring(foot,.72f,tone,20);
                    Point loc=ScreenPoint(foot);
                    string cue=cooldown>.01f?
                        Game.L("警号复位 "+(int)Math.Ceiling(cooldown)+"秒",
                            "SIGNAL RESET "+(int)Math.Ceiling(cooldown)+"s"):
                        ControlKey("E","B")+"  "+
                        Game.L("启动公共警号","SOUND PUBLIC SIGNAL");
                    Text(loc.x-98,loc.y-88,196,18,cue,10,tone,
                        TextAnchor.MiddleCenter);
                }
            }
            // World-space, read-only record defense overlay. This uses the
            // exact same witness foot and transient safety as the live
            // mission, never an invented Sprite or unrelated journal flag.
            // The wide faint outline is an evidence-interception perimeter,
            // NOT a hostile damage telegraph.
            if(Game.mode=="world"&&Game.SagaGuardActive)
            {
                Point site=Game.SagaGuardWitnessPosition;
                int safety=Game.SagaGuardEvidenceSafety;
                Color safetyTint=Game.SagaGuardBodyBlocking?mint:safety<=40?
                    new Color(1f,.4f,.36f,.9f):
                    safety<=75?gold:new Color(.59f,.85f,1f,.78f);
                Ring(site,7.5f,new Color(safetyTint.r,safetyTint.g,
                    safetyTint.b,.16f),48);
                Ring(site,.95f,safetyTint,24);
                Point labelFoot=ScreenPoint(site);
                Text(labelFoot.x-98,labelFoot.y-105,196,19,
                    Game.L("证据安全 "+safety+"%",
                        "EVIDENCE SAFE "+safety+"%"),
                    11,safetyTint,TextAnchor.MiddleCenter);
                // A real imminent contact gets its OWN narrow highlight,
                // separate from the warning's ground impact ring. The
                // companion's counter-shot label appears only after the
                // ally genuinely releases a still-flying mission-tagged
                // projectile, never on aim selection or cooldown alone.
                Enemy saboteur=Game.IncomingWitnessRecordThreat;
                if(saboteur!=null)
                {
                    Color warning=new Color(1f,.46f,.37f,.95f);
                    Ring(saboteur.Position,.78f,warning,24);
                    Point raiderFoot=ScreenPoint(saboteur.Position);
                    Text(raiderFoot.x-98,raiderFoot.y-81,196,18,
                        Game.L("即将抢夺证据！","RECORDS TARGETED!"),
                        10,warning,TextAnchor.MiddleCenter);
                }
                if(Game.CompanionWitnessCoverInFlight)
                    Text(labelFoot.x-98,labelFoot.y-85,196,18,
                        Game.L("同伴援护弹道在途","ALLY SHOT IN FLIGHT"),
                        10,mint,TextAnchor.MiddleCenter);
            }
            // Persistent real-foot recovery cue for an authentic hit-spilled
            // 90-second medical parcel. The original E, collision and
            // delivery reward code remains the only transaction authority.
            DrawDroppedCourierMedicine();
            // Make the new tactical hold legible in the actual world pass:
            // a ground ring marks the saved-in-memory anchor, not a fake actor.
            if(Game.CompanionHoldingPosition)
            {
                Point anchor=Game.CompanionHoldPosition;
                Ring(anchor,.62f,new Color(.48f,.9f,1f,.82f),18);
                Point heldScreen=ScreenPoint(anchor);
                Text(heldScreen.x-36,heldScreen.y-42,72,16,
                    Game.L("驻守","HOLD"),10,
                    new Color(.65f,.96f,1f),TextAnchor.MiddleCenter);
            }
            // Authoritative enemy-to-ward pressure above the depth pass,
            // distinct from damage telegraphs and unowned ambient effects.
            DrawRoadwardenWardSiphons();
            // Timed hazards are an active GAMEPLAY warning, never floor
            // decoration. Render their lean outlines after solid scenery
            // and actor bodies, before flying projectiles and impacts.
            DrawHazardWarnings();
            // A forewarned OPTIONAL crossing raid must have a visible,
            // motion-safe world-space warning, not just a fading toast.
            // The ring is tied to the real field cut and existing enemy
            // objects; no fake silhouette or HUD-only combat target.
            if(Game.mode=="world"&&
                (Game.CrossingWatchPending||Game.CrossingWatchActive))
            {
                Point watchFoot=Game.CrossingWatchGroundFoot;
                if((watchFoot-Game.Position).Length<15f)
                {
                    Color tone=Game.CrossingWatchPending?
                        new Color(1f,.77f,.39f,.85f):
                        new Color(1f,.48f,.40f,.83f);
                    Ring(watchFoot,Game.CrossingWatchPending?1.45f:1.9f,
                        tone,28);
                    Point marker=ScreenPoint(watchFoot);
                    Text(marker.x-86f,marker.y-76f,172f,18f,
                        Game.CrossingWatchPending?
                            Game.L("伏击预警 · 可后撤",
                                "AMBUSH WARNING · WITHDRAW"):
                            Game.L("拦路者 "+Game.CrossingWatchRemaining+
                                " 名","CROSSING RAIDERS "+Game.CrossingWatchRemaining),
                        10,tone,TextAnchor.MiddleCenter);
                }
                if(Game.CrossingWatchActive)
                    foreach(Enemy watchFoe in Game.enemies)
                        if(watchFoe!=null&&watchFoe.hp>0f&&
                            Game.IsCrossingWatchEnemy(watchFoe))
                            Ring(watchFoe.Position,.52f,
                                new Color(1f,.61f,.40f,.76f),16);
            }
            // Courier attackers are never allowed to materialize during
            // the scouting beat. Surface its real checkpoint footprint so
            // players can read the danger and choose a physical retreat.
            if(Game.mode=="world"&&
               Game.FieldDispatchAmbushWarningSeconds>0f)
            {
                Point station=Game.FieldDispatchAmbushOrigin;
                if((station-Game.Position).Length<16f)
                {
                    Color warn=new Color(1f,.69f,.34f,.87f);
                    Ring(station,1.3f,warn,28);
                    Point p=ScreenPoint(station);
                    Text(p.x-108f,p.y-85f,216f,18f,
                        Game.L("伏击将至 · 远离中继站",
                            "RAID INBOUND · LEAVE THE STATION"),
                        10,warn,TextAnchor.MiddleCenter);
                    Text(p.x-50f,p.y-67f,100f,16f,
                        (int)Math.Ceiling(Game.FieldDispatchAmbushWarningSeconds)+
                            Game.L(" 秒"," SEC"),
                        10,warn,TextAnchor.MiddleCenter);
                }
            }
            // A restrained world-space sight ring tells gamepad users which
            // actual foe will receive the snapped projectile. It follows a
            // live actor reference, never a reused spawn ID or stored flag.
            if(Game.mode=="world"&&controllerUsed&&
               controllerAimEnemy!=null&&controllerAimEnemy.hp>0f&&
               Game.enemies.Contains(controllerAimEnemy)&&
               Game.aimTarget.HasValue)
            {
                Point target=controllerAimEnemy.Position;
                Point landing=Game.aimTarget.Value;
                // The full target ring follows the REAL living actor. A
                // separate smaller circle shows where the physical bow or
                // staff is aiming ahead of a genuinely moving foe; never
                // mislabel that predicted point as an enemy's actual body.
                if((landing-target).Length<=1.75f&&
                   Game.CanStand(landing))
                {
                    bool pinned=System.Object.ReferenceEquals(
                        controllerPinnedEnemy,controllerAimEnemy);
                    Ring(target,controllerAimEnemy.boss?1.05f:.68f,
                        pinned?new Color(1f,.8f,.38f,.94f):
                            new Color(.58f,.98f,.81f,.84f),24);
                    if((landing-target).Length>.12f)
                    {
                        Ring(landing,.18f,
                            new Color(.95f,.85f,.48f,.82f),12);
                        Point leadAt=ScreenPoint(landing);
                        Text(leadAt.x-26,leadAt.y-32,52,16,
                            Game.L("提前量","LEAD"),10,
                            new Color(.95f,.85f,.48f,.82f),
                            TextAnchor.MiddleCenter);
                    }
                    Point at=ScreenPoint(target);
                    Text(at.x-37,at.y-(controllerAimEnemy.boss?94:72),
                        74,17,pinned?Game.L("锁定","LOCK"):
                            Game.L("瞄准","FOCUS"),11,
                        pinned?new Color(1f,.91f,.57f):
                            new Color(.67f,1f,.87f),
                        TextAnchor.MiddleCenter);
                }
            }
            // Keyboard/mouse players receive the same truthful lead cue
            // ONLY while pointing at the live actor and the real grounded
            // projectile intercept is distinct from its current body.
            if(Game.mode=="world"&&!controllerUsed&&
               pointerAimEnemy!=null&&pointerAimEnemy.hp>0f&&
               Game.enemies.Contains(pointerAimEnemy)&&
               !PointerOverInterface(MouseVirtual())&&
               Game.aimTarget.HasValue)
            {
                Point landing=Game.aimTarget.Value;
                Point live=pointerAimEnemy.Position;
                float drift=(landing-live).Length;
                if(drift>.12f&&drift<=1.75f&&Game.CanStand(landing))
                {
                    Ring(landing,.18f,new Color(.95f,.85f,.48f,.82f),12);
                    Point at=ScreenPoint(landing);
                    Text(at.x-26,at.y-32,52,16,
                        Game.L("提前量","LEAD"),10,
                        new Color(.95f,.85f,.48f,.82f),
                        TextAnchor.MiddleCenter);
                }
            }
            foreach (Projectile p in Game.projectiles) DrawProjectileVisual(p);
            foreach (Effect e in Game.effects) if (!IsGroundEffect(e)) DrawCombatEffect(e);
            // Source-painted optional impact cells are already drawn in the
            // WORLD depth pass; painting them here again would put every
            // slash above roofs, NPCs and even nearer equipped weapons.
            DrawExpansionWorld();
            DrawAtmosphere(); DrawPolishWorld();
            WorldObject near = Game.Nearest();
            if (Game.mode == "world" && Game.ReliefCivilianIsInteractionTarget(near))
            {
                // Match the SAME winner used by Interact/E. Never show a
                // different NPC's name above a rescue command.
                Point s=ScreenPoint(Game.reliefFollowerPosition);
                string name=Game.reliefMissionWave==3 ?
                    Game.L("获救居民","Rescued civilian"):
                    Game.L("平民紧急集结点","Civilian rally point");
                bool crowded=Game.NearbyInteractionCount()>1;
                Panel(s.x-(crowded?155:125),s.y+14,crowded?310:250,
                    crowded?54:35,new Color(.04f,.075f,.12f,.94f),gold);
                Text(s.x-(crowded?147:117),s.y+18,
                    crowded?294:234,27,
                    ControlKey("E","B")+"  "+name,14,paper,
                    TextAnchor.MiddleCenter);
                if(crowded)
                    Text(s.x-147,s.y+42,294,19,
                        Game.L("右键点选 · O / R3 · 切换交互对象",
                               "RIGHT-CLICK · O / R3 · CYCLE INTERACTION"),
                        11,muted,TextAnchor.MiddleCenter);
            }
            else if (near != null && Game.mode == "world" && Game.SafeTravel)
            {
                Point s = ScreenPoint(new Point(near.x, near.y)); string label = ControlKey("E", "B") + "  " + Game.L(near.name, near.en);
                bool crowded=Game.NearbyInteractionCount()>1;
                if(crowded)
                {
                    Panel(s.x-155,s.y+14,310,54,
                        new Color(.04f,.075f,.12f,.94f),gold);
                    Text(s.x-147,s.y+17,294,26,label,14,paper,
                        TextAnchor.MiddleCenter);
                    Text(s.x-147,s.y+42,294,19,
                        Game.CrowdedInteractionConfirmationPending?
                            Game.L("再次按 E 确认当前目标 · O / R3 切换",
                                "PRESS E AGAIN TO CONFIRM · O / R3 CYCLE"):
                            Game.L("右键点选 · O / R3 · 切换交互对象",
                                "RIGHT-CLICK · O / R3 · CYCLE INTERACTION"),
                        11,Game.CrowdedInteractionConfirmationPending?mint:muted,
                        TextAnchor.MiddleCenter);
                }
                else
                {
                    Panel(s.x-125,s.y+14,250,35,
                        new Color(.04f,.075f,.12f,.94f),gold);
                    Text(s.x-117,s.y+18,234,27,label,14,paper,
                        TextAnchor.MiddleCenter);
                }
            }
            worldLabelCullingActive=false;
            DrawCrowdedInteractionChoices();
        }
        private void DrawCrowdedInteractionChoices()
        {
            if(Game==null||Game.mode!="world"||!Game.SafeTravel)return;
            List<WorldObject> options=Game.NearbyInteractionChoices();
            if(options.Count<2)return;
            WorldObject current=Game.Nearest();
            WorldObject objective=Game.NearbyQuestInteraction();
            bool civilian=Game.ReliefCivilianIsInteractionTarget(current);
            int at=civilian?options.IndexOf(null):options.IndexOf(current);
            if(at<0)at=0;
            // World-space number pips correspond EXACTLY to the nearby
            // list below. Crowded NPCs/props no longer require a player to
            // guess which ground dot belongs to the selected speaker.
            // Mark only a bounded window to avoid creating another layer
            // of visual clutter in an already crowded district. The
            // original sprite pixels, map objects and E authority remain
            // untouched. A moving civilian uses its actual live foot.
            // A single authored objective glows in the crowded field even
            // when it falls beyond the visible four-row selector. Never
            // highlight a guess, an inaccessible source or a retired prop.
            if(objective!=null)
                Ring(new Point(objective.x,objective.y),.76f,gold,24);
            // Distinct E sources can use exactly the same authored foot.
            // Painting their badges separately made every number and ring
            // overlap; one readable cluster now shows the selected source
            // and its count, while all source rows remain separately live.
            var drawn=new bool[options.Count];
            int emitted=0;
            for(int slot=0;slot<options.Count&&emitted<6;slot++)
            {
                int index=(at+slot)%options.Count;
                if(drawn[index])continue;
                WorldObject obj=options[index];
                Point foot=obj==null?Game.reliefFollowerPosition:
                    new Point(obj.x,obj.y);
                int overlapCount=0;
                bool containsObjective=false;
                for(int other=0;other<options.Count;other++)
                {
                    if(drawn[other])continue;
                    WorldObject member=options[other];
                    Point memberFoot=member==null?Game.reliefFollowerPosition:
                        new Point(member.x,member.y);
                    if(!GameCore.WorldInteractionSilhouettesOverlap(foot,memberFoot))
                        continue;
                    drawn[other]=true;overlapCount++;
                    if(objective!=null&&
                        System.Object.ReferenceEquals(member,objective))
                        containsObjective=true;
                }
                bool chosen=index==at;
                Color ink=chosen?mint:containsObjective?gold:
                    PixelArt.C("#9aaeb4");
                Ring(foot,chosen?.62f:.44f,ink,chosen?22:16);
                Point screen=ScreenPoint(foot);
                int badgeWidth=overlapCount>1?43:23;
                Box(screen.x+9,screen.y-26,badgeWidth,21,
                    new Color(.035f,.075f,.12f,.9f));
                Text(screen.x+10,screen.y-26,badgeWidth-2,20,
                    (index+1).ToString()+
                        (overlapCount>1?" ×"+overlapCount:""),
                    12,ink,TextAnchor.MiddleCenter);
                emitted++;
            }
            int displayed=Math.Min(4,options.Count);
            float x=56f,y=516f,width=366f;
            Panel(x,y,width,49f+displayed*25f+
                (objective==null?0f:26f),
                new Color(.045f,.08f,.13f,.92f),PixelArt.C("#637d82"));
            Text(x+12,y+7,width-24,25,
                Game.L("附近交互 · 点击 / 面板滚轮 / 世界Shift+滚轮 / O/R3",
                    "NEARBY · CLICK / PANEL WHEEL / SHIFT+WHEEL / O/R3")+
                    "  "+(at+1)+"/"+options.Count,
                12,gold);
            for(int row=0;row<displayed;row++)
            {
                int index=(at+row)%options.Count;
                WorldObject target=options[index];
                bool chosen=row==0;
                if(chosen)Box(x+8,y+33+row*25,3,22,mint);
                string label=target==null?
                    Game.L("移动中的获救居民","Moving rescued civilian"):
                    Game.L(target.name,target.en);
                if(Button(x+18,y+34+row*25,width-32,23,
                    "Alt+"+(row+1)+"  "+(chosen?"◆  ":"   ")+
                    (objective!=null&&System.Object.ReferenceEquals(target,objective)?"★  ":"")+label,chosen))
                    Game.FocusNearbyInteraction(index);
            }
            if(objective!=null&&
                Button(x+18,y+35+displayed*25,width-32,23,
                    Game.L("Shift+O · 聚焦当前任务目标",
                        "SHIFT+O · FOCUS QUEST OBJECTIVE"),
                    System.Object.ReferenceEquals(current,objective)))
                Game.FocusNearbyQuestInteraction();
            if(options.Count>displayed)
                Text(x+width-54,y+10,45,20,"…",12,muted);
        }
        private void DrawHero() { DrawPlayerActor(); }
        private void DrawLegacyHero()
        {
            Point p = ScreenPoint(Game.Position); Box(p.x - 15, p.y - 1, 30, 7, new Color(.04f, .1f, .15f, .3f));
            if (Game.racing) PropImage("skiff", 1, p.x - 42, p.y - 41, 84, 56);
            if (Game.guardTime > 0) Ring(Game.Position, .8f, mint, 20);
            if (hero == null) { PropImage("npc", 1, p.x - 28, p.y - 55, 56, 65); return; }
            int row = Math.Abs(Game.aim.x) > Math.Abs(Game.aim.y) ? (Game.aim.x < 0 ? 1 : 2) : (Game.aim.y < 0 ? 3 : 0);
            int frame = moving ? (int)(clock * 7) % 3 : 1;
            GUI.color = Game.invulnerable > 0 && (int)(clock * 12) % 2 == 0 ? new Color(1, 1, 1, .5f) : Color.white;
            GUI.DrawTextureWithTexCoords(new Rect(p.x - 23, p.y - 67, 46, 77), hero, new Rect(frame / 3f, 1 - (row + 1) / 4f, 1 / 3f, 1 / 4f)); GUI.color = Color.white;
            Point tip = ScreenPoint(Game.Position + Game.aim * .6f); Box(tip.x - 2, tip.y - 2, 4, 4, gold);
        }
        private void DrawObject(WorldObject o)
        {
            if (!Game.IsObjectPresent(o)) return;
            worldLabelOwner=o;
            Point p = ScreenPoint(new Point(o.x, o.y)); string kind = o.kind;

            if(kind=="nightvent")
            {
                bool available=Game.Has("nightshift_started")&&
                    Game.NightShiftEvidenceCount==2&&!Game.NightShiftComplete;
                bool cooling=Game.NightShiftVentSeconds>0f;
                Color ink=cooling?mint:available?gold:muted;
                PropImage("barrel",0,p.x-38,p.y-77,76,84,ink);
                Ring(new Point(o.x,o.y),cooling?.75f:.5f,ink,22);
                if(available)
                    WorldObjectLabel(p.x-112,p.y-116,224,19,
                        cooling?Game.L("泄压中 · "+(int)Game.NightShiftVentSeconds+"秒",
                            "COOLING · "+(int)Game.NightShiftVentSeconds+"S"):
                        Game.L("废轨手轮 · 体力18","RAIL VALVE · 18 ENERGY"),
                        10,ink,TextAnchor.MiddleCenter);
                return;
            }
            if(kind=="nightledger")
            {
                bool finished=Game.NightShiftComplete;
                int station=Game.save.room;
                bool reviewed=station>0&&Game.Has("nightshift_trace_"+station);
                bool actionable=station==0?
                    (!Game.Has("nightshift_started")||
                     Game.NightShiftEvidenceCount==3&&!finished):
                    Game.Has("nightshift_started")&&!reviewed&&
                    Game.NightShiftEvidenceCount==station-1;
                // Unlike the resolved original case, the final guild handoff
                // is still a live interaction once all three physical field
                // receipts are signed. Give that actual world source a clear
                // cue, not the misleading 'roll filed' label.
                bool reportReady=finished&&station==0&&
                    !Game.Has("nightshift_reported")&&
                    Game.Has("nightshift_followup_1")&&
                    Game.Has("nightshift_followup_2")&&
                    Game.Has("nightshift_followup_3");
                Color ink=reportReady?gold:finished?mint:reviewed?muted:
                    actionable?gold:new Color(.60f,.77f,.86f);
                PropImage(station==0?"bookshelf":
                    station==3?"barrel":"post",0,
                    p.x-34,p.y-76,68,80,ink);
                Ring(new Point(o.x,o.y),
                    reportReady?.77f:actionable?.65f:.40f,ink,22);
                if(actionable||reportReady)Diamond(p.x,p.y-87,6,ink);
                if(reportReady)
                {
                    Ring(new Point(o.x,o.y),.88f,gold,26);
                    WorldObjectLabel(p.x-115,p.y-143,230,19,
                        Game.L("三地回执待交接 · 按 E",
                            "THREE REPORTS READY · E"),
                        10,gold,TextAnchor.MiddleCenter);
                }
                if(finished&&station>=1&&station<=3&&
                   !Game.Has("nightshift_followup_"+station))
                {
                    Ring(new Point(o.x,o.y),.77f,mint,26);
                    WorldObjectLabel(p.x-110,p.y-143,220,19,
                        Game.Has("nightshift_public")?
                            Game.L("公共巡路物资 · 按 E",
                                "PUBLIC ROAD SUPPLIES · E"):
                            Game.L("匿名现场援助 · 按 E",
                                "ANONYMOUS FIELD AID · E"),
                        10,mint,TextAnchor.MiddleCenter);
                }
                if(station==2&&Game.Has("nightshift_started")&&
                   Game.NightShiftEvidenceCount==1&&!reviewed&&!finished)
                    WorldObjectLabel(p.x-115,p.y-140,230,19,
                        Game.LowTide?
                            Game.L("退潮 · 原始刻痕可读",
                                "LOW TIDE · ORIGINAL MARK EXPOSED"):
                            Game.L("涨潮 · 等待 / 木材1搭架",
                                "HIGH TIDE · WAIT / 1 TIMBER"),
                        10,Game.LowTide?mint:gold,TextAnchor.MiddleCenter);
                if(station==3&&Game.NightShiftVentSeconds>0f)
                    WorldObjectLabel(p.x-111,p.y-140,222,19,
                        Game.L("泄压开封 · 倒计时"+(int)Game.NightShiftVentSeconds+"秒",
                            "COOLING SPOOL · "+(int)Game.NightShiftVentSeconds+"S"),
                        10,mint,TextAnchor.MiddleCenter);
                if(station==1&&Game.NightShiftRaidActive)
                {
                    Ring(new Point(o.x,o.y),.90f,gold,28);
                    WorldObjectLabel(p.x-112,p.y-140,224,19,
                        Game.NightShiftRaidReady?
                            Game.L("追兵已击退 · 返回原件",
                                "TRACKERS DEFEATED · VERIFY"):
                            Game.L("正在保护班表 · 敌人"+Game.NightShiftRaidRemaining,
                                "DEFEND SHIFT CARD · "+Game.NightShiftRaidRemaining+" LEFT"),
                        10,gold,TextAnchor.MiddleCenter);
                }
                if(actionable||reviewed||finished)
                    WorldObjectLabel(p.x-98,p.y-113,196,19,
                        reportReady?Game.L("终章交接","FINAL DEBRIEF"):
                        finished?Game.L("名册结案","ROLL FILED"):
                        reviewed?Game.L("现场已核验","EVIDENCE CHECKED"):
                        station==0?Game.L("夜班名册 · 按 E","NIGHT ROLL · E"):
                                   Game.L("核实原始时间 · 按 E","VERIFY RECORD · E"),
                        10,ink,TextAnchor.MiddleCenter);
                return;
            }
            if(kind=="fielddevice")
            {
                int room=Game.save.room;
                float cd=Game.FieldTacticCooldown(o);
                bool known=Game.Has("field_tactic_used_"+room);
                int threats=Game.FieldTacticLiveThreatCount(o);
                bool trial=Game.FieldTacticTrialAt(o);
                bool trialReady=trial&&Game.FieldTacticTrialReady;
                bool trialSigned=Game.FieldTacticTrialDone(room);
                // Signing an earned battle report outranks an ordinary
                // cooldown label. Otherwise GameView can say "RESET 18s"
                // exactly when the player should press E to finish a quest.
                Color ink=trialReady||trialSigned?mint:trial?gold:
                    cd>0f?muted:threats>0?gold:known?mint:muted;
                PropImage(room==1||room==5?"post":
                    room==2||room==7?"column":
                    room==3?"barrel":"crystal",0,
                    p.x-36,p.y-75,72,78,ink);
                if(room==1)
                {Box(p.x-27,p.y-75,54,5,ink);Diamond(p.x,p.y-87,8,ink);}
                else if(room==2)
                {Box(p.x-25,p.y-48,50,6,ink);Ring(new Point(o.x,o.y),.62f,ink,20);}
                else if(room==3)
                {Box(p.x-4,p.y-89,8,22,ink);Diamond(p.x,p.y-96,6,gold);}
                else if(room==4)
                {Diamond(p.x,p.y-96,11,ink);Box(p.x-28,p.y-69,56,3,ink);}
                else if(room==5)
                {Box(p.x-27,p.y-84,54,5,ink);Ring(new Point(o.x,o.y),.75f,ink,24);}
                else if(room==6)
                {Diamond(p.x,p.y-96,12,ink);Box(p.x-22,p.y-49,44,5,gold);}
                else
                {Box(p.x-25,p.y-82,50,5,ink);Diamond(p.x-15,p.y-94,6,ink);Diamond(p.x+15,p.y-94,6,ink);}
                if(!Game.save.reducedMotion&&cd<=0f)
                    Ring(new Point(o.x,o.y),.56f+
                        .05f*Mathf.Sin(clock*2.3f),ink,24);
                WorldObjectLabel(p.x-121,p.y-122,242,22,
                    trialReady?
                        Game.L("演练已完成 · 按 E 签回执",
                               "DRILL CLEARED · E SIGN REPORT"):
                    trial&&!Game.FieldTacticTrialUsed?
                        Game.L("演练敌人 "+Game.FieldTacticTrialRemaining+
                               " · Shift+G 使用装置",
                               "DRILL "+Game.FieldTacticTrialRemaining+
                               " · SHIFT+G COUNTER"):
                    trial&&Game.FieldTacticTrialChainSecondsLeft>0f?
                        Game.L("追击破绽 · 剩余"+
                               (int)Math.Ceiling(Game.FieldTacticTrialChainSecondsLeft)+"秒",
                               "FOLLOW UP · "+
                               (int)Math.Ceiling(Game.FieldTacticTrialChainSecondsLeft)+"s"):
                    trial&&Game.FieldTacticTrialChainEarned?
                        Game.L("限时反击成功！· 继续击退敌人",
                               "BONUS EARNED · DEFEAT REMAINING"):
                    trial?
                        Game.L("演练剩余 "+Game.FieldTacticTrialRemaining+" 名 · 击退敌人",
                               "DRILL "+Game.FieldTacticTrialRemaining+" LEFT · DEFEAT"):
                    trialSigned?
                        Game.L("本地实战已签收 · 按 E 查看",
                               "TRIAL SIGNED · E INSPECT"):
                    cd>0f?Game.L("装置复位 "+(int)Math.Ceiling(cd)+"秒",
                                    "RESET "+(int)Math.Ceiling(cd)+"s"):
                    threats>0?Game.L("敌情 "+threats+" · Shift+G 快捷反制 / E查看",
                                      "THREATS "+threats+" · SHIFT+G QUICK / E INSPECT"):
                    known?Game.L("战术装置 · 暂无目标","FIELD TACTIC · NO TARGET"):
                        Game.L("战术装置 · 等敌接近","FIELD DEVICE · AWAIT FOES"),
                    11,ink,TextAnchor.MiddleCenter);
                return;
            }
            if(kind=="wildernessHunt")
            {
                int region=Game.save.room;
                bool done=Game.WildernessHuntFinished(region);
                Color ink=done?mint:
                    Game.WildernessHuntActive?new Color(1f,.51f,.37f,.95f):gold;
                string prop=region==5?"post":region==6?"obelisk":"barrel";
                PropImage(prop,0,p.x-32,p.y-77,64,76,ink);
                // Three distinct, authored recognizable investigation sites:
                // the harbour's broken towline, desert stone tracks, and
                // Frostbell's ruined low rescue chime. Existing resources
                // and their Sprite provenance remain intact.
                if(region==5)
                {
                    Frame(p.x-23,p.y-68,46,8,ink);
                    Box(p.x-30,p.y-56,36,3,ink);
                }
                else if(region==6)
                {
                    Diamond(p.x,p.y-82,8,ink);
                    Box(p.x-27,p.y-42,54,3,ink);
                }
                else
                {
                    Box(p.x-3,p.y-91,6,32,ink);
                    Diamond(p.x,p.y-94,9,ink);
                }
                Ring(new Point(o.x,o.y),done?.47f:.75f,ink,22);
                string state=done?
                    Game.L("居民路口 · 已结案","CROSSING SECURED"):
                    Game.WildernessHuntActive?
                    Game.L("荒野追踪 · 第"+Game.WildernessHuntWave+"/2波 · 剩"+
                        Game.WildernessHuntRemaining+"名",
                        "LIVE HUNT · WAVE "+Game.WildernessHuntWave+
                        "/2 · "+Game.WildernessHuntRemaining+" LEFT"):
                    Game.Has("wilderness_hunt_fought_"+region)?
                    Game.L("两波胜利 · 回来决定处置","BATTLE WON · DECIDE OUTCOME"):
                    Game.L("未知足迹 · 可实地调查","UNKNOWN TRACKS · INVESTIGATE");
                WorldObjectLabel(p.x-143,p.y-114,286,19,state,10,ink,
                    TextAnchor.MiddleCenter);
                return;
            }
            if(kind=="ridgeBreach")
            {
                int region=Game.save.room;
                bool open=Game.RidgeBreachOpen(region);
                Color tint=open?mint:gold;
                PropImage(region==2||region==5?"post":
                    region==3?"barrel":"ruin",0,
                    p.x-36,p.y-78,72,80,tint);
                Ring(new Point(o.x,o.y),open?.52f:.8f,tint,26);
                if(!open)
                {
                    Box(p.x-31,p.y-42,62,8,tint);
                    Diamond(p.x,p.y-86,8,gold);
                }
                WorldObjectLabel(p.x-131,p.y-110,262,18,open?
                    Game.L("居民捷径 · 已实际贯通","CIVILIAN PASSAGE · WALKABLE"):
                    Game.L("实体障碍 · 可修复近路","PHYSICAL BLOCKADE · REPAIR"),
                    10,tint,TextAnchor.MiddleCenter);
                return;
            }
            if(kind=="ridgeEcho")
            {
                int region=Game.save.room;
                int stage=-1;
                for(int i=0;i<3;i++)
                    if(o.id=="ridge_echo_"+region+"_"+i){stage=i;break;}
                if(stage<0)return;
                bool complete=Game.RidgeEchoComplete(region);
                Point current=Game.RidgeEchoObjective(region);
                bool goal=!complete&&Game.Has("biome_field_done_"+region)&&
                    (current-new Point(o.x,o.y)).Length<.01f;
                Color ink=complete?mint:goal?gold:
                    region==1?PixelArt.C("#84bb8d"):
                    region==2?PixelArt.C("#77b9b9"):
                    region==3?PixelArt.C("#d7a66e"):
                    region==4?PixelArt.C("#b0a8ce"):
                    region==5?PixelArt.C("#82b9ca"):
                    region==6?PixelArt.C("#e1ba78"):
                    PixelArt.C("#a8cbe1");
                string prop=stage==0?"obelisk":stage==1?"post":"barrel";
                PropImage(prop,0,p.x-36,p.y-80,72,80,ink);
                // Each stage has a distinct physical silhouette, not a
                // placeholder icon detached from the world collision.
                if(stage==0)
                {
                    Frame(p.x-25,p.y-79,50,17,ink);
                    Box(p.x-16,p.y-72,32,2,ink);
                }
                else if(stage==1)
                {
                    Box(p.x-3,p.y-89,6,43,ink);
                    Diamond(p.x,p.y-94,10,ink);
                }
                else
                {
                    Box(p.x-28,p.y-62,56,5,ink);
                    Frame(p.x-19,p.y-91,38,26,ink);
                }
                Ring(new Point(o.x,o.y),goal?.85f:.52f,ink,22);
                if(goal)Diamond(p.x,p.y-102,7,gold);
                WorldObjectLabel(p.x-145,p.y-120,290,17,
                    Game.RidgeEchoRunActive&&stage==2?
                        Game.L("民用传令 · "+Game.RidgeEchoSeconds+"秒",
                            "OVERLAND RELAY · "+Game.RidgeEchoSeconds+"S"):
                    complete&&stage==2?
                        Game.L("民用远端营地 · 安全恢复","CIVILIAN REFUGE · HEAL & REST"):
                    Game.L(o.name,o.en),10,ink,TextAnchor.MiddleCenter);
                return;
            }
            if(kind=="mineGhostTrain")
            {
                int stage=Game.GhostTrainChapter;
                bool active=stage>0&&stage<5;
                Point destination=Game.GhostTrainTarget;
                bool objective=active&&
                    (destination-new Point(o.x,o.y)).Length<.01f;
                Color ink=stage==5?(Game.GhostTrainPublic?mint:gold):
                    objective?gold:stage==0?muted:PixelArt.C("#91b9d8");
                // Distinct public-site silhouettes using existing original
                // production art, not a placeholder painted on the atlas.
                string prop=o.id=="mine_ghost_dispatch"?"post":
                    o.id=="mine_ghost_manifest"?"obelisk":
                    o.id=="mine_ghost_switch"?"barrel":"post";
                float size=o.id=="mine_ghost_switch"?88f:76f;
                PropImage(prop,0,p.x-size*.5f,p.y-size*1.1f,
                    size,size*1.2f,ink);
                if(o.id=="mine_ghost_manifest")
                {
                    Frame(p.x-22,p.y-82,44,28,ink);
                    Box(p.x-18,p.y-77,36,3,ink);
                    Box(p.x-18,p.y-69,29,3,ink);
                }
                else if(o.id=="mine_ghost_switch")
                {
                    Box(p.x-32,p.y+3,64,6,ink);
                    Box(p.x-2,p.y-67,5,67,ink);
                    Diamond(p.x+4,p.y-70,9,ink);
                }
                else if(o.id=="mine_ghost_medbay")
                {
                    Box(p.x-19,p.y-72,38,8,ink);
                    Box(p.x-4,p.y-87,8,36,ink);
                }
                if(objective)
                {
                    Ring(new Point(o.x,o.y),.93f,gold,26);
                    Diamond(p.x,p.y-104,9,gold);
                }
                else Ring(new Point(o.x,o.y),.51f,ink,22);
                WorldObjectLabel(p.x-122,p.y-118,244,17,
                    objective?
                        Game.L(Game.GhostTrainBattleActive?
                            "真实战斗 · "+Game.GhostTrainEnemiesLeft+"名守卫":
                            "第十七趟列车 · 实地线索 "+stage+"/4",
                            Game.GhostTrainBattleActive?
                            "TRUE RAIL BATTLE · "+Game.GhostTrainEnemiesLeft+
                                " GUARDS":
                            "TRAIN SEVENTEEN · LIVE SITE "+stage+"/4"):
                        Game.GhostTrainFreight&&
                        o.id!="mine_ghost_manifest"?
                            Game.L("第十七趟车 · 客运站",
                                "TRAIN 17 · PASSENGER STOP"):
                            Game.L(o.name,o.en),
                    10,ink,TextAnchor.MiddleCenter);
                return;
            }
            // These are NEW real pieces of interactive regional engineering,
            // rather than recycled foliage or cosmetic map-only symbols.
            if(kind=="regionalMachine")
            {
                int region=Game.save.room;
                int station=-1;
                int.TryParse(o.id.Substring(
                    ("regional_machine_"+region+"_").Length),out station);
                bool complete=Game.RegionalMachineDone(region);
                bool commissioned=Game.Has("regional_machine_stage_"+
                    region+"_"+station);
                bool next=Game.RegionalMachineNextIndex(region)==station&&
                    Game.BiomeFieldDone;
                Color tint=complete?mint:commissioned?
                    new Color(.5f,.78f,.8f):next?gold:muted;
                PropImage(station==0?"post":station==1?"barrel":"obelisk",
                    0,p.x-35,p.y-75,70,75,tint);
                Ring(new Point(o.x,o.y),.52f,tint,22);
                if(next||complete)
                    Diamond(p.x,p.y-84,6,tint);
                bool vigilTarget=Game.RegionalVigilActive&&
                    Game.RegionalVigilNextStation==station;
                if(vigilTarget)
                {
                    tint=gold;
                    Ring(new Point(o.x,o.y),.88f,gold,28);
                }
                bool courierTarget=Game.FieldDispatchIsTarget(o);
                if(courierTarget)
                    Ring(new Point(o.x,o.y),.93f,gold,28);
                WorldObjectLabel(p.x-110,p.y-108,220,18,
                    courierTarget?
                        Game.L("补给中继站 · 剩余"+
                            (int)Game.FieldDispatchSeconds+"秒",
                            "CARGO CHECKPOINT · "+
                            (int)Game.FieldDispatchSeconds+"S"):
                    vigilTarget?
                        Game.L(Game.RegionalVigilStage==3?"归还工程回执":
                            "紧急巡护目标 · "+(int)Game.RegionalVigilSeconds+"秒",
                            Game.RegionalVigilStage==3?"RETURN WATCH RECEIPT":
                            "WATCH TARGET · "+(int)Game.RegionalVigilSeconds+"S"):
                    complete?
                        Game.L("公共修复 · 安全休憩","REPAIRED FIELD SHELTER"):
                        commissioned?
                        Game.L("装置已调好","MECHANISM ALIGNED"):
                        next?
                        Game.L("调试装置 · "+(Game.RegionalMachineProgress(region)+1)+"/3",
                            "REPAIR DEVICE · "+(Game.RegionalMachineProgress(region)+1)+"/3"):
                        Game.L("故障的公共装置","DAMAGED PUBLIC DEVICE"),
                    10,tint,TextAnchor.MiddleCenter);
                return;
            }
            // Authored field sites are true walkable WorldObjects, rendered
            // through the production depth queue rather than map-only pins.
            // Their form differs from ordinary random terrain decorations.
            if(kind=="fieldKit")
            {
                int room=Game.save.room;
                bool recovered=Game.FieldKitRecovered(room);
                bool active=Game.FieldOutpostActive;
                bool ward=Game.FieldOutpostWard(room);
                bool forge=Game.FieldOutpostForge(room);
                Color ink=ward?mint:forge?new Color(.55f,.83f,1f):
                    active?gold:recovered?new Color(.77f,.84f,.62f):gold;
                PropImage(room==2||room==5?"barrel":
                    room==4?"post":"ruin",0,
                    p.x-34,p.y-77,68,79,ink);
                Ring(new Point(o.x,o.y),recovered?.45f:.64f,ink,24);
                if(!recovered)Diamond(p.x,p.y-87,6,ink);
                WorldObjectLabel(p.x-112,p.y-111,224,18,ward?
                    Game.L("安全前线 · 医疗站","SAFE OUTPOST · FIELD AID"):
                    forge?Game.L("安全前线 · 绊索工坊",
                        "SAFE OUTPOST · TRAP WORKSHOP"):
                    active?Game.L(Game.FieldOutpostReady?
                          "守住了道路 · 回来建站":
                          "据点遭袭 · 剩余"+Game.FieldOutpostRemaining+"名",
                          Game.FieldOutpostReady?
                          "OUTPOST SECURED · BUILD HERE":
                          "OUTPOST UNDER ATTACK · "+Game.FieldOutpostRemaining+" LEFT"):
                    recovered?Game.L("工具已回收 · 可防守建站",
                        "KIT RECOVERED · DEFEND TO BUILD"):
                    Game.L("遗失工具箱 · 木材"+Game.FieldKitTimber(room)+
                        " / 零件"+Game.FieldKitParts(room),
                        "LOST KIT · "+Game.FieldKitTimber(room)+
                        " TIMBER / "+Game.FieldKitParts(room)+" PARTS"),
                    10,ink,TextAnchor.MiddleCenter);
                // Do not hide the permanent tactical choice behind a
                // one-time quest toast. Display the SAME charged return
                // fare that the actual physical shuttle transaction uses.
                // Keep the aid/workshop identity visible above this row.
                if((ward||forge)&&Game.FieldDispatchDone(room))
                    WorldObjectLabel(p.x-112,p.y-88,224,18,
                        Game.L("返程 "+Game.FieldOutpostReturnFare(room)+
                                "硬币 · "+Game.FieldOutpostReturnEnergy(room)+"耐力",
                            "RETURN "+Game.FieldOutpostReturnFare(room)+
                                "C / "+Game.FieldOutpostReturnEnergy(room)+"ST"),
                        10,ink,TextAnchor.MiddleCenter);
                if(active)
                    Ring(new Point(o.x,o.y),.92f,gold,28);
                else if(ward||forge)
                    Diamond(p.x,p.y-92,7,ink);
                if(Game.FieldDispatchActive)
                    WorldObjectLabel(p.x-112,p.y-139,224,18,
                        Game.L("民间补给 · 正在运送",
                            "CIVILIAN CARGO · IN TRANSIT"),
                        10,gold,TextAnchor.MiddleCenter);
                else if(Game.FieldDispatchDone(room))
                    WorldObjectLabel(p.x-112,p.y-139,224,18,
                        Game.L("真实运送已签收 · 补给升级",
                            "VERIFIED COURIER · STOCK UPGRADED"),
                        10,mint,TextAnchor.MiddleCenter);
                return;
            }
            if(kind=="biomeSurvey"||kind=="biomeAnswer")
            {
                bool second=kind=="biomeAnswer";
                bool finished=Game.BiomeFieldDone;
                bool ready=Game.BiomeFieldStarted;
                bool medicine=Game.BiomeSupplyMedicineBuilt(Game.save.room);
                bool road=Game.BiomeSupplyRoadBuilt(Game.save.room);
                Color ink=finished?
                    (medicine?mint:road?gold:new Color(.63f,.87f,.96f)):
                    second?(ready?gold:muted):new Color(.63f,.87f,.96f);
                PropImage(second?"obelisk":"post",0,
                    p.x-36,p.y-82,72,84,ink);
                Ring(new Point(o.x,o.y),second?.59f:.46f,ink,24);
                if(finished)
                {
                    // Keep existing supply/medicine information AND quote
                    // the exact live relay price on a separate line. Replacing
                    // the supply status with a fare hides real world progress.
                    string status=medicine?
                        Game.L("公共药箱 · 营地治疗增强",
                            "PUBLIC TRIAGE · CAMP HEALING"):
                        road?
                        Game.L("居民通路 · 已修复",
                            "PUBLIC ROAD · OPEN"):
                        Game.L("远路物资 "+Game.BiomeSupplySampleCount(Game.save.room)+
                            "/2","SUPPLIES "+Game.BiomeSupplySampleCount(Game.save.room)+"/2");
                    bool crisisHere=Game.BiomeCrisisActive;
                    if(crisisHere)
                    {
                        bool isTarget=Game.BiomeCrisisStage==3?!second:second;
                        if(isTarget)
                        {
                            status=Game.L("紧急护送目标 · 剩"+
                                (int)Game.BiomeCrisisSeconds+"秒",
                                "RELIEF TARGET · "+(int)Game.BiomeCrisisSeconds+"S");
                            Ring(new Point(o.x,o.y),.85f,gold,28);
                        }
                    }
                    else if(Game.Has("biome_crisis_done_"+Game.save.room))
                        status=Game.L("居民救援已签收 · 永久增益",
                            "RELIEF SIGNED · LASTING BONUS");
                    WorldObjectLabel(p.x-117,p.y-114,234,18,status,10,
                        crisisHere?gold:ink,TextAnchor.MiddleCenter);
                    // This figure uses the same calculator as actual
                    // biome_relay payment; no stale hardcoded road prices.
                    WorldObjectLabel(p.x-117,p.y-95,234,17,
                        Game.L("接驳：硬币"+Game.FieldRelayFareHere+
                            " / 耐力"+Game.FieldRelayEnergyHere,
                            "RELAY · "+Game.FieldRelayFareHere+
                            "C/"+Game.FieldRelayEnergyHere+"ST"),
                        9,ink,TextAnchor.MiddleCenter);
                    if(medicine||road)
                        Diamond(p.x,p.y-79,6,ink);
                    // The physical FIRST survey post owns the patrol.
                    // Distinct persistent marks distinguish captured
                    // testimony from a violent completion without changing
                    // original Sprite pixels or scene GUIDs.
                    if(!second&&Game.FieldPatrolActive)
                    {
                        Ring(new Point(o.x,o.y),.93f,gold,28);
                        WorldObjectLabel(p.x-117,p.y-141,234,18,
                            Game.FieldPatrolVeteranActive?
                                Game.L("进阶巡护战斗 · 尚余"+Game.FieldPatrolRemaining+"名",
                                    "ELITE PATROL · "+Game.FieldPatrolRemaining+" LEFT"):
                                Game.L("巡兵仍在现场 · "+Game.FieldPatrolRemaining+"名",
                                    "PATROL ACTIVE · "+Game.FieldPatrolRemaining+" LEFT"),
                            10,gold,TextAnchor.MiddleCenter);
                    }
                    else if(!second)
                    {
                        // Keep the earned first-clear and all-living route
                        // status visible. Elite availability is an ADDITIONAL
                        // prompt, never a replacement for the old travel perk.
                        bool signed=Game.FieldPatrolDone(Game.save.room);
                        bool spared=Game.FieldPatrolSpared(Game.save.room);
                        bool veteranSigned=Game.FieldPatrolVeteranDone(Game.save.room);
                        if(spared)
                            Ring(new Point(o.x,o.y),.93f,mint,28);
                        if(signed)
                            WorldObjectLabel(p.x-117,p.y-141,234,18,
                                spared?
                                    Game.L("全员活捉 · 安全路况已公开",
                                        "ALL CAPTURED · SAFE ROUTE KNOWN"):
                                    Game.L("巡护安全回执已登记",
                                        "SIGNED PATROL RECEIPT"),
                                10,spared?mint:ink,TextAnchor.MiddleCenter);
                        if(veteranSigned)
                            WorldObjectLabel(p.x-117,p.y-162,234,18,
                                Game.FieldPatrolVeteranAllLiving(Game.save.room)?
                                    Game.L("进阶精锐 · 全员活捉",
                                        "ELITE DEFENSE · ALL RESTRAINED"):
                                    Game.L("进阶精锐 · 回执已签",
                                        "ELITE DEFENSE · SIGNED"),
                                10,mint,TextAnchor.MiddleCenter);
                        else if(signed)
                        {
                            Ring(new Point(o.x,o.y),1.03f,gold,28);
                            WorldObjectLabel(p.x-117,p.y-162,234,18,
                                Game.L("进阶巡护可挑战 · 按 E",
                                    "ELITE PATROL AVAILABLE · E"),
                                10,gold,TextAnchor.MiddleCenter);
                        }
                    }
                }
                if(!finished&&(!second||ready))
                {
                    Diamond(p.x,p.y-89,6,ink);
                    WorldObjectLabel(p.x-102,p.y-113,204,18,
                        second?Game.L("远方实地判断","FIELD INTERPRETATION"):
                            Game.L("探查本区线索","READ REGIONAL CLUE"),
                        10,ink,TextAnchor.MiddleCenter);
                }
                if(Game.FieldDispatchIsTarget(o))
                {
                    Ring(new Point(o.x,o.y),.94f,gold,28);
                    WorldObjectLabel(p.x-115,p.y-155,230,18,
                        Game.L("民间补给终点 · 剩余"+
                            (int)Game.FieldDispatchSeconds+"秒",
                            "CARGO DELIVERY · "+(int)Game.FieldDispatchSeconds+"S"),
                        10,gold,TextAnchor.MiddleCenter);
                }
                return;
            }
            if (DrawPolishObject(o) || DrawExpansionObject(o)) return;
            if(kind=="tidefishery")
            {
                Color tint=Game.Has("harbor_report")?
                    (Game.LowTide?mint:muted):new Color(.5f,.56f,.61f,1f);
                PropImage("post",0,p.x-31,p.y-76,62,78,tint);
                Ring(new Point(o.x,o.y),.48f,tint,20);
                if(Game.Has("mistport_tidal_first"))
                    WorldObjectLabel(p.x-66,p.y-91,132,19,Game.L("退潮渔路 ✓","TIDAL ROUTE ✓"),
                        11,mint,TextAnchor.MiddleCenter);
                else Diamond(p.x,p.y-68,5,tint);
                return;
            }
            if(kind=="glassloomsource"||kind=="glassloommirror"||
               kind=="glassloomreceiver")
            {
                int lens=-1;
                if(kind=="glassloommirror")
                    for(int k=0;k<3;k++)
                        if(o.id=="glassloom_mirror_"+(k+1))lens=k;
                bool aligned=lens>=0&&Game.GlassLoomAligned(lens);
                bool charged=Game.GlassLoomStage>=2||Game.GlassLoomComplete;
                bool shuttered=lens==1&&Game.GlassLoomStage==2&&Game.GlassLoomShutterClosed;
                Color tint=Game.GlassLoomPrivate&&Game.Night?
                    new Color(.72f,.66f,1f,1f):
                    Game.GlassLoomPublic&&!Game.Night?
                    new Color(1f,.9f,.55f,1f):
                    charged?new Color(.60f,.90f,1f,1f):
                    aligned?mint:Game.GlassLoomActive?gold:
                    new Color(.8f,.77f,.7f,1f);
                if(shuttered)tint=new Color(.58f,.64f,.72f,1f);
                PropImage(kind=="glassloommirror"?"crystal":
                    kind=="glassloomsource"?"obelisk":"post",0,
                    p.x-33f,p.y-80f,66f,83f,tint);
                Ring(new Point(o.x,o.y),charged?.72f:.43f,tint,22);
                if(lens>=0)
                    WorldObjectLabel(p.x-37,p.y-91,74,21,
                        lens==1&&Game.GlassLoomStage==2?
                            Game.L(shuttered?"遮光":"开光",shuttered?"SHUT":"OPEN"):
                            Game.GlassLoomOrientation(lens).ToString()+
                            (aligned?" ✓":""),
                        14,tint,TextAnchor.MiddleCenter);
                else if(Game.GlassLoomActive||Game.GlassLoomComplete)
                    WorldObjectLabel(p.x-65,p.y-101,130,20,
                        kind=="glassloomsource"?
                        Game.L("归光井","GLASSWELL"):
                        Game.L("接收器","RECEIVER"),11,tint,
                        TextAnchor.MiddleCenter);
                if(kind=="glassloomsource"&&Game.GlassLoomActive)
                    Diamond(p.x,p.y-89,6,gold);
                return;
            }
            if(kind=="abyssFarSurvey"||kind=="abyssFarCache"||kind=="abyssFarEcho")
            {
                bool survey=kind=="abyssFarSurvey";
                bool cache=kind=="abyssFarCache";
                string flag="abyss_annex_far_"+(survey?"survey_":cache?"cache_":"echo_")+
                    Game.save.abyssFloor;
                bool recorded=Game.Has(flag);
                PropImage(survey?"obelisk":cache?"barrel":"crystal",0,
                    p.x-36,p.y-77,72,84,recorded?
                        new Color(.66f,.88f,.82f,.8f):Color.white);
                if(!recorded)Diamond(p.x,p.y-73,6,gold);
                return;
            }
            if(kind=="abyssCache"||kind=="abyssSurvey")
            {
                bool found=Game.Has("abyss_annex_"+(kind=="abyssCache"?"cache_":"survey_")+Game.save.abyssFloor);
                PropImage(kind=="abyssCache"?"barrel":"obelisk",0,p.x-33,p.y-70,66,78,
                    found?new Color(.66f,.8f,.78f,.72f):Color.white);
                if(!found)Diamond(p.x,p.y-70,6,gold);
                return;
            }
            if(kind=="annexinnarchive"||kind=="annexinnmessage")
            {
                bool complete=kind=="annexinnarchive"?Game.Has("annex_archive_complete"):
                    Game.Has("annex_inn_letter");
                PropImage(kind=="annexinnarchive"?"bookshelf":"table",0,
                    p.x-43,p.y-79,86,85);
                if(!complete)Diamond(p.x,p.y-74,5,gold);
                return;
            }
            if(kind=="civicconduitmaster"||kind=="civicconduitvalve")
            {
                bool master=kind=="civicconduitmaster";
                int slot=master?-1:Game.CivicConduitValveIndex(o);
                bool finished=Game.CivicConduitComplete;
                bool active=Game.CivicConduitActive;
                bool tuned=active&&slot>=0&&slot<Game.CivicConduitStage;
                bool next=active&&slot==Game.CivicConduitStage;
                bool green=next&&Game.CivicConduitWindowOpen;
                Color pipe=finished?new Color(.5f,.95f,.81f):
                    tuned?new Color(.55f,.77f,.88f):
                    green?new Color(.47f,.98f,.55f):
                    next?new Color(1f,.54f,.34f):
                    new Color(.84f,.72f,.52f);
                PropImage(master?"obelisk":"post",0,
                    p.x-32,p.y-81,64,81,pipe);
                // Static readable phase rings work with reduced-motion enabled.
                Ring(new Point(o.x,o.y),next?.72f:.5f,pipe,18);
                if(next)
                    WorldObjectLabel(p.x-68,p.y-111,136,20,
                        Game.L(green?"稳定 · 现在操作":"等待绿色周期",
                            green?"GREEN · TUNE NOW":"WAIT FOR GREEN"),
                        11,pipe,TextAnchor.MiddleCenter);
                if(active&&master)
                    WorldObjectLabel(p.x-73,p.y-108,146,20,
                        Game.L("线路剩余 "+(int)Game.CivicConduitTimeLeft+"秒",
                            "GRID "+(int)Game.CivicConduitTimeLeft+"s"),
                        11,pipe,TextAnchor.MiddleCenter);
                if(!finished&&!tuned&&!active)
                    Diamond(p.x,p.y-77,5,gold);
                return;
            }
            if(kind=="lastdirectiveboard"||kind=="lastdirectivecore"||
                kind.StartsWith("lastdirectiveclue"))
            {
                bool board=kind=="lastdirectiveboard",control=kind=="lastdirectivecore";
                int clue=control||board?-1:
                    int.Parse(kind.Substring("lastdirectiveclue".Length));
                bool discovered=board?Game.LastDirectiveStarted:
                    control?Game.LastDirectiveChoice>0:
                    Game.Has("last_directive_clue_"+clue);
                Color tint=Game.LastDirectiveComplete?
                    (Game.LastDirectiveChoice==1?
                        new Color(.53f,.86f,.99f):new Color(.99f,.82f,.54f)):
                    discovered?new Color(.68f,.86f,.97f):
                    new Color(.98f,.76f,.45f);
                PropImage(board?"table":control?"obelisk":
                    clue==2?"fire":clue==1?"bookshelf":"post",
                    0,p.x-38,p.y-86,76,90,tint);
                Ring(new Point(o.x,o.y),.56f,tint,16);
                if(!discovered)Diamond(p.x,p.y-82,6,gold);
                if(control&&Game.LastDirectiveEnemies>0)
                    WorldObjectLabel(p.x-78,p.y-111,156,20,
                        Game.L("保密卫兵 "+Game.LastDirectiveEnemies,
                            "GUARDIANS "+Game.LastDirectiveEnemies),
                        12,new Color(.97f,.4f,.32f),TextAnchor.MiddleCenter);
                return;
            }
            if(kind=="chapterroad")
            {
                int room=Game.save.room,slot=Game.ChapterRoadNodeIndex(o);
                bool done=Game.ChapterRoadDone(room);
                bool live=Game.ChapterRoadActive;
                bool next=live&&Game.ChapterRoadStage==slot+1;
                Color ink=done?mint:next?gold:
                    live?new Color(.63f,.85f,1f):new Color(.95f,.72f,.39f);
                PropImage(slot==0?"post":slot==1?
                    (room==3?"ruin":"obelisk"):
                    room==1?"barrel":room==2?"post":"bookshelf",
                    0,p.x-34,p.y-78,68,83,
                    done?new Color(.72f,.9f,.82f,.9f):Color.white);
                Ring(new Point(o.x,o.y),next?.7f:.46f,ink,22);
                if(next||!done&&slot==0)
                    Diamond(p.x,p.y-82,next?7:5,ink);
                if(live&&slot==1&&Game.ChapterRoadDeviceUsed)
                    WorldObjectLabel(p.x-103,p.y-127,206,17,
                        Game.L("现场装置已启动","FIELD DEVICE ACTIVATED"),
                        10,mint,TextAnchor.MiddleCenter);
                // This line is the REAL radius of the active regional
                // danger, not a decorative broad quest ring. The tide ring
                // disappears at low water and the steam ring ends when the
                // original attackers are contained.
                if(live&&Game.ChapterRoadStage==2&&
                   Game.ChapterRoadRemaining>0&&slot==1&&
                   !Game.ChapterRoadDeviceUsed&&
                   (room==3||room==2&&!Game.LowTide))
                {
                    Color warning=room==3?
                        new Color(.96f,.42f,.28f,.75f):
                        new Color(.49f,.78f,1f,.73f);
                    Ring(new Point(o.x,o.y),6f,warning,48);
                    WorldObjectLabel(p.x-96,p.y-158,192,18,
                        room==3?Game.L("蒸汽高温区 · 保持移动",
                            "STEAM HAZARD · KEEP MOVING"):
                            Game.L("涨潮寒流区 · 保持移动",
                            "FLOOD CHILL · KEEP MOVING"),
                        10,warning,TextAnchor.MiddleCenter);
                }
                if(live&&Game.ChapterRoadSeconds>0&&slot==1)
                    WorldObjectLabel(p.x-86,p.y-133,172,18,
                        Game.L("潮位 / 热压任务 · "+
                                (int)Game.ChapterRoadSeconds+"秒",
                            "TIDE / PRESSURE · "+
                                (int)Game.ChapterRoadSeconds+"S"),
                        10,gold,TextAnchor.MiddleCenter);
                WorldObjectLabel(p.x-107,p.y-109,214,20,
                    done?Game.L("本章居民行动完成 ✓","CHAPTER ROUTE COMPLETE ✓"):
                    live?Game.L(Game.ChapterRoadStage==2?
                        "道路遭袭 · 剩余"+Game.ChapterRoadRemaining+"名":
                        next?"真实路线目标 · 按 E":"沿路寻找下一证据",
                        Game.ChapterRoadStage==2?
                        "AMBUSH · "+Game.ChapterRoadRemaining+" REMAIN":
                        next?"REAL ROUTE TARGET · E":"FOLLOW THE NEXT CLUE"):
                    Game.L(slot==0?"主线事件 · 按 E":"实地调查记录",
                        slot==0?"STORY EVENT · E":"FIELD EVIDENCE"),
                    11,ink,TextAnchor.MiddleCenter);
                return;
            }
            if(kind=="farcheck")
            {
                bool verified=Game.FarCourierStamped;
                bool clinic=Game.CivicSupplyClinic(Game.save.room);
                bool watch=Game.CivicSupplyWatch(Game.save.room);
                Color care=new Color(.46f,.94f,.88f);
                Color shade=clinic?care:watch?new Color(1f,.75f,.39f):
                    verified?new Color(.65f,.93f,.78f):Color.white;
                PropImage("post",0,p.x-32,p.y-76,64,79,shade);
                if(clinic||watch)
                    Ring(new Point(o.x,o.y),.56f,shade,16);
                else if(Game.FarCourierActive&&!verified ||
                    Game.CivicSupplyStarted&&Game.FarCourierDelivered(Game.save.room)&&
                    (Game.save.room==0||Game.save.room==3||Game.save.room==5))
                    Diamond(p.x,p.y-71,6,gold);
                return;
            }
            if(kind=="farbridge")
            {
                bool open=Game.FarBridgeOpen(Game.save.room);
                PropImage(open?"post":"ruin",0,p.x-38,p.y-81,76,86,
                    open?new Color(.71f,.96f,.79f):Color.white);
                if(!open)Diamond(p.x,p.y-75,6,gold);
                return;
            }
            if(kind=="farhidden"||kind=="farlookout")
            {
                bool shown=kind=="farhidden"?Game.Has("far_secret_"+Game.save.room):
                    Game.Has("far_vista_"+Game.save.room);
                PropImage(kind=="farhidden"?"barrel":"obelisk",0,
                    p.x-35,p.y-75,70,82,
                    shown?new Color(.7f,.85f,.8f,.8f):Color.white);
                if(kind=="farlookout")
                {
                    int room=Game.save.room;
                    bool aid=Game.VantageWatchAid(room),signal=Game.VantageWatchSignal(room);
                    Color ink=aid?mint:signal?new Color(.6f,.83f,1f):
                        Game.VantageWatchActive?gold:shown?muted:gold;
                    if(aid||signal||Game.VantageWatchActive)
                        Ring(new Point(o.x,o.y),.78f,ink,22);
                    if(Game.VantageWatchActive||aid||signal)
                        WorldObjectLabel(p.x-99,p.y-110,198,19,
                            Game.VantageWatchActive?
                                Game.L(Game.VantageWatchReady?"返回登记巡护记录":
                                    "守望 · 敌人 "+Game.VantageWatchRemaining,
                                    Game.VantageWatchReady?"FILE WATCH REPORT":
                                    "LOOKOUT RAIDERS · "+Game.VantageWatchRemaining):
                                aid?Game.L("公共医疗瞭望站","CIVILIAN AID LOOKOUT"):
                                    Game.L("公共信号瞭望站","CIVILIAN SIGNAL LOOKOUT"),
                            11,ink,TextAnchor.MiddleCenter);
                }
                if(!shown)Diamond(p.x,p.y-72,5,gold);
                return;
            }
            if(kind=="faratlasdesk"||kind=="farrefugerecord")
            {
                bool done=kind=="faratlasdesk"?Game.Has("far_refuge_atlas"):
                    Game.Has("far_refuge_guest_record");
                bool inquest=kind=="farrefugerecord"&&Game.CivicSupplyStarted;
                bool responsePublished=kind=="farrefugerecord"&&
                    Game.Has("civic_response_ledger");
                bool published=inquest&&Game.CivicSupplyComplete;
                PropImage(kind=="faratlasdesk"?"table":"bookshelf",0,p.x-40,p.y-80,80,86,
                    responsePublished?new Color(.96f,.79f,.46f):
                    published?new Color(.51f,.98f,.83f):
                    done?new Color(.73f,.9f,.86f):Color.white);
                if(inquest&&!published)
                    Diamond(p.x,p.y-77,6,new Color(.48f,.91f,1f));
                else if(!done)Diamond(p.x,p.y-77,6,gold);
                return;
            }
            if(kind=="farledger"||kind=="farcalibrate"||kind=="farshelter")
            {
                int room=Game.save.room;
                bool ready=kind=="farledger"?Game.FarLogFound(room):
                    kind=="farcalibrate"?Game.FarDialReady(room):Game.FarShelterBuilt(room);
                string prop=kind=="farledger"?"bookshelf":
                    kind=="farcalibrate"?"obelisk":ready?"fire":"ruin";
                bool clinic=kind=="farshelter"&&Game.CivicSupplyClinic(room);
                bool watch=kind=="farshelter"&&Game.CivicSupplyWatch(room);
                Color upkeep=clinic?new Color(.47f,.98f,.9f):
                    watch?new Color(1f,.78f,.43f):ready?new Color(.74f,.96f,.82f):Color.white;
                PropImage(prop,0,p.x-38,p.y-83,76,88,upkeep);
                Ring(new Point(o.x,o.y),.49f,
                    clinic||watch?upkeep:ready?mint:gold,14);
                if(clinic||watch)
                    Diamond(p.x,p.y-85,4,upkeep);
                if(kind=="farshelter"&&ready&&Game.CivicConduitComplete)
                {
                    Color network=Game.LastDirectiveChoice==1?
                        new Color(.54f,.82f,1f):new Color(1f,.78f,.49f);
                    // Outer network ring preserves prior local clinic/watch tints.
                    Ring(new Point(o.x,o.y),.77f,network,18);
                    WorldObjectLabel(p.x-69,p.y-112,138,20,
                        Game.L(Game.LastDirectiveChoice==1?
                            "公共电网 · 补充耐力":"庇护电网 · 基础疗养",
                            Game.LastDirectiveChoice==1?
                            "GRID POWER ONLINE":"PUBLIC CARE ONLINE"),
                        11,network,TextAnchor.MiddleCenter);
                }
                // Hot meals have reached only the three genuine stocked
                // far-ring shelters. Keep all original clinic/watch/grid
                // overlays, prop atlas and world anchor untouched.
                if(kind=="farshelter"&&Game.SoupRouteDelivered(room))
                {
                    Color supper=new Color(1f,.77f,.41f);
                    Ring(new Point(o.x,o.y),.95f,supper,20);
                    WorldObjectLabel(p.x-73,p.y-136,146,22,
                        Game.L("热汤已送到","HOT STEW DELIVERED"),
                        11,supper,TextAnchor.MiddleCenter);
                }
                if(!ready)Diamond(p.x,p.y-79,6,gold);
                return;
            }
            if(kind=="ridgemarker")
            {
                int district=o.id.EndsWith("_0")?0:1;
                bool read=Game.RidgeSeen(Game.save.room,district);
                bool linked=Game.RidgeLinked(Game.save.room);
                PropImage("obelisk",0,p.x-34,p.y-84,68,90,
                    linked?new Color(.63f,.95f,.85f):read?
                        new Color(.73f,.83f,.91f):Color.white);
                Ring(new Point(o.x,o.y),.51f,
                    linked?mint:read?muted:gold,16);
                if(!read)Diamond(p.x,p.y-78,5,gold);
                // These outer roads are genuinely contested in the world
                // simulation. Show ONLY this marker's live encounter, not
                // the other district's foes or a new fictional quest. Keep
                // the earlier ridge/story pips and original Sprite unchanged.
                if(Game.RemoteRoadActiveDistrict==district)
                {
                    int visible=Game.RemoteRoadSignalThreatCount(o);
                    bool used=Game.RemoteRoadSignalUsed;
                    // The signal belongs to this authentic in-combat
                    // marker, not to distant copies or a fictional prop.
                    // Keep the original enemy countdown when the terrain
                    // has no valid counter target.
                    float follow=Game.RemoteRoadCounterOpportunitySeconds;
                    Color encounterInk=used?muted:visible>0?gold:muted;
                    Ring(new Point(o.x,o.y),.94f,encounterInk,28);
                    WorldObjectLabel(p.x-118,p.y-135,236,20,
                        follow>0f?
                            Game.L("追击目标 · "+follow.ToString("0.0")+"秒 · 命中回耐力",
                                "FOLLOW UP · "+follow.ToString("0.0")+"s · HIT TO REGAIN"):
                        used?
                            Game.L("路标反制已用 · 尚余"+Game.RemoteRoadRemaining+"名",
                                "SIGNAL SPENT · "+Game.RemoteRoadRemaining+" LEFT"):
                        visible>0?
                            Game.L("路标反制 · Shift+G / E · 尚余"+Game.RemoteRoadRemaining+"名",
                                "COUNTER · SHIFT+G / E · "+Game.RemoteRoadRemaining+" LEFT"):
                            Game.L("远路拦截 · 尚余"+Game.RemoteRoadRemaining+"名",
                                "OUTER AMBUSH · "+Game.RemoteRoadRemaining+" LEFT"),
                        10,encounterInk,TextAnchor.MiddleCenter);
                }
                else if(Game.RemoteRoadCleared(Game.save.room,district))
                {
                    Ring(new Point(o.x,o.y),.75f,mint,22);
                    WorldObjectLabel(p.x-93,p.y-135,186,19,
                        Game.L("远路安全 · 已清除伏击","SAFE ROAD · AMBUSH CLEARED"),
                        10,mint,TextAnchor.MiddleCenter);
                }
                // A reopened ridge carries a real combat survey as well as
                // its original two-end travel link. Three tiny grounded pips
                // above the accepted obelisk show verified patrol contacts;
                // never replace source art or imply that untouched guardians
                // have a quest waiting on their still-locked ridge.
                if(linked&&Game.Has("boss_"+Game.save.room)&&
                    Game.save.room>=1&&Game.save.room<=7)
                {
                    int cleared=Game.RidgeWatchProgress(Game.save.room);
                    for(int i=0;i<3;i++)
                        Box(p.x-9+i*7,p.y-96,5,5,
                            i<cleared?mint:muted);
                    if(Game.RidgeWatchComplete(Game.save.room))
                        Diamond(p.x,p.y-105,4,mint);
                }
                return;
            }
            if(kind=="outerwatch")
            {
                bool done=Game.OuterPatrolDone(Game.save.room);
                bool flawless=Game.OuterPatrolFlawlessDone(Game.save.room);
                PropImage("post",0,p.x-34,p.y-80,68,83,
                    flawless?new Color(1f,.91f,.68f):
                    done?new Color(.68f,.93f,.78f):Color.white);
                // The constructed barricade is now physically legible in
                // the same live gameplay field as its real stock contacts.
                // Reuse accepted original wooden prop artwork rather than
                // faking a new asset or mounting an unverified Sprite.
                // Only actual unspent structural HP draws this foreground
                // line of cover. A true released raider strike briefly
                // brightens it; depleted cover vanishes automatically.
                if(Game.OuterPatrolBraceStanding)
                {
                    float guardHp=Game.OuterPatrolBraceIntegrity;
                    Color wooden=Game.OuterPatrolBraceHitFlash>0f?
                        new Color(1f,.85f,.46f):
                        guardHp<=16f?new Color(.83f,.48f,.39f):
                        guardHp<=32f?new Color(.91f,.68f,.49f):
                        new Color(.83f,.87f,.79f);
                    PropImage("barrel",0,p.x-34,p.y-41,33,40,wooden);
                    PropImage("barrel",0,p.x+1,p.y-41,33,40,wooden);
                    Ring(new Point(o.x,o.y),.67f,
                        Game.OuterPatrolBraceHitFlash>0f?
                            new Color(1f,.83f,.42f):wooden,18);
                }
                // Highlight ONLY the authentic attacker whose committed
                // contact is about to hit the actual supply objective.
                // The footprint is the living actor's, never a fake enemy
                // or a replacement animation. Reduced motion gets the
                // identical stationary, readable warning ring.
                Enemy breaker=Game.OuterPatrolIncomingStockThreat;
                if(breaker!=null)
                {
                    Color imminent=breaker.kind==2?
                        new Color(1f,.37f,.33f):
                        new Color(1f,.75f,.35f);
                    Ring(breaker.Position,breaker.kind==2?.75f:.62f,
                        imminent,20);
                }
                // Veteran fights occur at already-repaired posts: the
                // live danger color MUST outrank the old green-clear tint.
                Color guardColor=Game.OuterPatrolActive?
                    new Color(1,.4f,.34f):flawless?gold:done?mint:gold;
                Ring(new Point(o.x,o.y),.43f,guardColor,14);
                if(Game.OuterPatrolActive||!done)
                    Diamond(p.x,p.y-79,5,guardColor);
                if(Game.OuterPatrolCompanionGuarding)
                {
                    // A guard is an actual foot waypoint beside the stock:
                    // visually mark the destination, not a second ally,
                    // copied sprite or a fake detached attack body.
                    Ring(Game.OuterPatrolCompanionGuardPosition,.35f,
                        Game.OuterPatrolMedicRallying?
                            new Color(1f,.73f,.43f):
                            Game.OuterPatrolGuardBlocksLeft==0?
                            new Color(.64f,.63f,.66f):
                            new Color(.48f,.9f,1f),12);
                    // The additional ring appears only on the physical
                    // planted ally when finite interception is actually
                    // ready; never represent an approaching/remote guard
                    // or a medic rushing away as an active bodyguard.
                    if(Game.OuterPatrolGuardReady)
                        Ring(Game.companionPosition,.53f,
                            new Color(.46f,1f,.78f),16);
                    if(Game.OuterPatrolMedicRallying)
                        // This is an actual healing rally, not a second
                        // companion sprite, phantom projectile or teleport.
                        Ring(Game.Position,.45f,
                            new Color(.56f,.98f,.76f),12);
                }
                if(done&&Game.OuterDeliveryDone(Game.save.room))
                    WorldObjectLabel(p.x-98,p.y-129,196,18,
                        Game.L("急送药布已到 · 医疗补给增强",
                            "DELIVERED · IMPROVED MEDICAL CARE"),
                        10,mint,TextAnchor.MiddleCenter);
                if(Game.OuterPatrolActive)
                    WorldObjectLabel(p.x-79,p.y-107,158,20,
                        Math.Ceiling(Game.OuterPatrolTime)+"s · "+
                        Math.Ceiling(Game.OuterPatrolIntegrity)+"%"+
                        (Game.OuterPatrolVeteranActive?
                            " · "+Math.Ceiling(Game.OuterPatrolBraceIntegrity)+"B":""),
                        11,Game.OuterPatrolIntegrity<=30f?
                            new Color(1,.4f,.34f):gold,TextAnchor.MiddleCenter);
                else if(Game.OuterDeliveryActive&&Game.OuterDeliveryStage==2)
                    WorldObjectLabel(p.x-87,p.y-112,174,22,
                        Game.L("把药布交给值守员","Deliver dressings here"),
                        11,gold,TextAnchor.MiddleCenter);
                else if(done)
                    WorldObjectLabel(p.x-79,p.y-104,158,18,
                        flawless?Game.L("无损守卫徽章 · 荣誉补给岗",
                            "FLAWLESS MEDAL · HONOR POST"):
                        Game.OuterPatrolVeteranDone(Game.save.room)?
                            Game.L("精英补给岗 · 强化医疗","VETERAN POST · IMPROVED CARE"):
                            Game.L("安全补给 · 恢复与避寒暑",
                                "SAFE OUTPOST · REST & SHELTER"),
                        10,mint,TextAnchor.MiddleCenter);
                return;
            }
            if(kind=="outerlog"||kind=="outerrelay"||kind=="outercache")
            {
                bool recorded=kind=="outerlog"?Game.OuterLogFound(Game.save.room):
                    kind=="outerrelay"?Game.OuterRelayRestored(Game.save.room):
                    Game.OuterCacheRecovered(Game.save.room);
                if(kind=="outerrelay")
                {
                    PropImage("lamp",0,p.x-29,p.y-85,58,89,
                        recorded?new Color(.85f,1f,.69f):new Color(.48f,.58f,.62f));
                    if(recorded&&!Game.save.reducedMotion&&Game.save.effectsQuality>0)
                    {
                        float pulse=.18f+.09f*(1+Mathf.Sin(clock*2.4f));
                        Ring(new Point(o.x,o.y-1),.8f,new Color(mint.r,mint.g,mint.b,pulse),18);
                    }
                    if(Game.OuterDeliveryActive)
                        WorldObjectLabel(p.x-106,p.y-111,212,20,
                            Game.L("急送中 · 剩余 ","MEDICINE RUN · ")+
                            Math.Ceiling(Game.OuterDeliverySeconds)+"s",
                            11,gold,TextAnchor.MiddleCenter);
                    // The genuine recovery service appears in GameView only
                    // while the player occupies its safe, unobstructed zone.
                    // Always show this fixed-size cue in reduced-motion mode.
                    if(recorded&&Game.OuterRelayRestRefuge)
                    {
                        Ring(new Point(o.x,o.y),.4f,
                            new Color(mint.r,mint.g,mint.b,.48f),16);
                        WorldObjectLabel(p.x-87,p.y-107,174,19,
                            Game.L("可以在灯下歇脚","Rest beside the light"),
                            11,mint,TextAnchor.MiddleCenter);
                        // This is a material-priced active field choice, not
                        // the free passive refuge pulse. Only advertise it
                        // when the hero has wood, a physically reachable lamp
                        // and a genuine combat-safe field moment.
                        if(Game.OuterSignalCampReady)
                            WorldObjectLabel(p.x-95,p.y-147,190,19,
                                Game.L("可以搭棚短休 · 木材×1",
                                    "Field rest ready · 1 timber"),
                                10,mint,TextAnchor.MiddleCenter);
                        // Show travel only when at least one OTHER restored
                        // signal is actually unlocked AND the local source
                        // can be interacted with. Reduced Motion retains
                        // the steady legible label without animated blinking.
                        if(Game.OuterSignalRouteReady)
                            WorldObjectLabel(p.x-100,p.y-126,200,18,
                                Game.L("巡路队的灯路已连通",
                                    "Warden signal roads connected"),
                                10,mint,TextAnchor.MiddleCenter);
                    }
                }
                else
                {
                    PropImage(kind=="outerlog"?"bookshelf":"barrel",0,
                        p.x-34,p.y-71,68,76,
                        recorded?new Color(.68f,.82f,.75f,.82f):Color.white);
                    if(kind=="outercache"&&Game.OuterDeliveryActive&&
                        Game.OuterDeliveryStage==1)
                        WorldObjectLabel(p.x-95,p.y-112,190,20,
                            Game.L("取走待送的药布","Collect the dressings"),
                            11,gold,TextAnchor.MiddleCenter);
                }
                if(!recorded)Diamond(p.x,p.y-68,5,gold);
                return;
            }
            if(kind=="trailcircuit")
            {
                int stage=0;
                for(int j=1;j<=3;j++)
                    if(o.id=="trailcircuit_"+Game.save.room+"_"+j)stage=j;
                int progress=Game.TrailCircuitProgress(Game.save.room);
                bool read=stage>0&&progress>=stage;
                PropImage(stage==2?"post":"obelisk",0,p.x-32,p.y-77,64,79,
                    read?new Color(.6f,.85f,.77f,.85f):Color.white);
                Ring(new Point(o.x,o.y),.38f,read?mint:
                    stage==progress+1?gold:muted,12);
                if(!read)WorldObjectLabel(p.x-26,p.y-84,52,20,stage.ToString(),16,
                    stage==progress+1?gold:muted,TextAnchor.MiddleCenter);
                // The world actor, not a navigation-only map popup, tells
                // the player which real E step is currently owned by their
                // deliberately accepted three-stop walking expedition.
                if(Game.TrailCircuitIsCurrentFollowTarget(o))
                {
                    Ring(new Point(o.x,o.y),.76f,gold,20);
                    WorldObjectLabel(p.x-114,p.y-115,228,20,
                        Game.L("连续巡路目标 · 按 E 核对",
                            "GUIDED TRAIL · PRESS E TO SURVEY"),
                        10,gold,TextAnchor.MiddleCenter);
                }
                return;
            }
            if(kind=="annexcache")
            {
                bool used=Game.Has("annex_cache_"+o.id);
                PropImage("barrel",0,p.x-27,p.y-52,54,56,used?new Color(.57f,.67f,.65f,.7f):Color.white);
                if(!used)Diamond(p.x,p.y-43,5,gold);
                return;
            }
            if(kind=="annexcamp")
            {
                PropImage("fire",0,p.x-38,p.y-70,76,78);
                Ring(new Point(o.x,o.y),.45f,Game.SafeTravel?mint:muted,12);
                return;
            }
            if(kind=="annexvista")
            {
                bool seen=Game.Has("annex_vista_"+o.id);
                PropImage("obelisk",0,p.x-33,p.y-76,66,80,seen?new Color(.74f,.91f,.84f):Color.white);
                if(!seen)Diamond(p.x,p.y-75,5,gold);
                return;
            }

            if(kind=="ashbanksignal")
            {
                // Existing authored pixel props, no placeholder assets.
                bool dispatch=o.id=="ashbank_dispatch";
                bool tracks=o.id=="ashbank_tracks";
                bool gauge=o.id=="ashbank_inscription";
                bool finished=Game.Has("ashbank_settled");
                bool read=dispatch?Game.Has("ashbank_dispatch"):
                    tracks?Game.Has("ashbank_tracks"):
                    gauge?Game.Has("ashbank_inscription"):finished;
                PropImage(dispatch?"barrel":tracks?"post":gauge?"obelisk":"lamp",
                    0,p.x-32,p.y-77,64,79,
                    read?new Color(.74f,.90f,.82f,.93f):Color.white);
                Ring(new Point(o.x,o.y),.43f,
                    read?mint:gold,12);
                if(!read)Diamond(p.x,p.y-81,5,gold);
                return;
            }
            if (kind == "waytrail")
            {
                // Preserve the original survey's per-marker checkmark. The
                // Chapter Twelve audit is regional, so it gets a separate,
                // localized badge rather than overwriting legacy map progress.
                bool recorded = Game.Has("waytrail_" + o.id);
                PropImage("obelisk", 0, p.x - 31, p.y - 71, 62, 73,
                    recorded ? new Color(.65f, .87f, .79f, .8f) : Color.white);
                Ring(new Point(o.x, o.y), .43f, recorded ? mint : gold, 12);
                WorldObjectLabel(p.x - 46, p.y - 83, 92, 21, recorded ? "✓" : "?", 17,
                    recorded ? mint : gold, TextAnchor.MiddleCenter);
                bool auditRegion =
                    (Game.CivicAftercareActive ||
                     Game.Has("civic_aftercare_published")) &&
                    (Game.save.room == 0 || Game.save.room == 3 ||
                     Game.save.room == 5);
                if (auditRegion)
                {
                    bool audited = Game.Has("civic_aftercare_waytrail_" + Game.save.room);
                    WorldObjectLabel(p.x - 31, p.y - 103, 62, 17,
                        audited ? Game.L("复核 ✓", "AUDIT ✓") :
                                  Game.L("复核 ?", "AUDIT ?"),
                        9, audited ? mint : gold, TextAnchor.MiddleCenter);
                    // Show an actual civilian field-kit service only AFTER
                    // the public record is published. Draw on the authored
                    // world marker itself; do not add a second actor, alter
                    // golden-hero sprites or fake a free/unconditional heal.
                    // Static geometry remains legible in reduced-motion mode.
                    if(audited && Game.Has("civic_aftercare_published"))
                    {
                        Ring(new Point(o.x,o.y),.59f,mint,16);
                        WorldObjectLabel(p.x-76,p.y-121,152,18,
                            Game.L("居民急救 · 药草1","FIELD KIT · 1 HERB"),
                            10,mint,TextAnchor.MiddleCenter);
                        // Existing illustrated obelisk remains untouched.
                        // Source-bound civic supplies alter only the small
                        // clear completion ring and readable world caption.
                        bool supplied=Game.Has("civic_waystation_stocked_"+Game.save.room);
                        if(supplied)Ring(new Point(o.x,o.y),.82f,
                            new Color(.51f,.84f,.9f,.8f),20);
                        WorldObjectLabel(p.x-89,p.y-143,178,19,
                            supplied?Game.L("路站已补给 ✓","WAYSTATION ✓"):
                                Game.L("补给 · 木材2 药草1",
                                    "SUPPLY · 2 TIMBER 1 HERB"),
                            10,supplied?mint:gold,TextAnchor.MiddleCenter);
                        // Show the genuine available PUBLIC transport only
                        // after another independent region has been funded.
                        // Small static label: no replacing the authored
                        // obelisk sprite, no fake vehicle or moving marker.
                        if(supplied)
                        {
                            string courierLabel=Game.CivicCourierActive?
                                Game.Has("civic_courier_active_"+Game.save.room+"_"+o.id)?
                                    Game.L("药包始发站 · 可退货","PARCEL ORIGIN · RETURN"):
                                    Game.CivicCourierGuardCleared(Game.save.room)?
                                        Game.L("药包送达点 · E","DELIVER MEDICINE · E"):
                                        Game.L("拦路伏击者尚在","AMBUSHER STILL ACTIVE"):
                                Game.Has("civic_courier_done_"+Game.save.room)?
                                    Game.L("居民送药已完成 ✓","MEDICINE DELIVERED ✓"):
                                    recorded?
                                        Game.L("按 E 接徒步送药委托","E · ON-FOOT MEDICINE"):
                                        "";
                            if(courierLabel.Length>0)
                                WorldObjectLabel(p.x-90,p.y-164,180,19,
                                    courierLabel,10,
                                    Game.CivicCourierActive?gold:mint,
                                    TextAnchor.MiddleCenter);
                            if(Game.CivicWaystationCount>=2&&!Game.CivicCourierActive)
                                WorldObjectLabel(p.x-89,p.y-184,178,19,
                                    Game.L("按 E 查看接驳线路",
                                        "E · PUBLIC SHUTTLES"),
                                    10,gold,TextAnchor.MiddleCenter);
                        }
                    }
                }
                return;
            }

            if(kind=="scout"&&Game.ScoutDefenseActive)
            {
                Color safety=Game.ScoutDefenseSafety<35?
                    PixelArt.C("#ed8c82"):
                    Game.ScoutDefenseUnderAttack?gold:mint;
                Ring(new Point(o.x,o.y),.84f,safety,28);
                Bar(p.x-38,p.y-102,76,Game.ScoutDefenseSafety/100f,safety,6);
                WorldObjectLabel(p.x-88,p.y-128,176,19,
                    Game.L("侦察员安全度 ","SCOUT SAFETY ")+
                    Game.ScoutDefenseSafety+"%",11,safety,
                    TextAnchor.MiddleCenter);
            }
            if (kind.StartsWith("portal"))
            {
                int destination = int.Parse(kind.Substring(6)); bool available = Game.CanEnter(destination);
                Ring(new Point(o.x, o.y), .55f, available ? gold : muted, 16);
                Box(p.x - 3, p.y - 24, 6, 18, available ? gold : muted);
                WorldObjectLabel(p.x - 90, p.y + 18, 180, 38, Game.L(o.name.Replace("前往", ""), o.en), 13, paper, TextAnchor.MiddleCenter); return;
            }
            if (kind == "shrine" || kind == "lore" || kind.StartsWith("rune") || kind == "engine")
            {
                bool opened = kind == "shrine" ? Game.Has("core_" + Game.save.room) : kind == "lore" ? Game.Has("lore_" + Game.save.room) : Game.GateOpen;
                PropImage(kind == "engine" ? "engine" : kind == "shrine" ? "altar" : "column", 0, p.x - 32, p.y - 64, 64, 74);
                Color color = opened ? mint : gold;
                Box(p.x - 5, p.y - 31, 10, 10, color); Ring(new Point(o.x, o.y), .35f + (Game.save.reducedMotion ? 0 : Mathf.Sin(clock * 2) * .06f), color, 12);
                string mark = kind.StartsWith("rune") ? new[] { "SUN", "TIDE", "MOON" }[int.Parse(kind.Substring(4))] : kind == "lore" ? "?" : "*";
                WorldObjectLabel(p.x - 45, p.y - 64, 90, 25, mark, 13, paper, TextAnchor.MiddleCenter); return;
            }
            if (kind == "herb" || kind == "ore" || kind == "wood")
            { PropImage(kind, 0, p.x - 27, p.y - 55, 54, 63, Game.Has("node_" + o.id) ? new Color(.6f, .6f, .6f, .45f) : Color.white); return; }
            if (kind == "rest") { if (Game.save.room != 9) PropImage("table", 0, p.x - 27, p.y - 33, 54, 42); return; }
            if (kind == "routes") return; // The sign and well are already scenery.
            // Production speech motion is tied to THIS actual object,
            // not a same-kind resident sharing the same world coordinates.
            // Clear the draw scope even when the rendering bridge declines
            // a GPU-only resource or an imported texture throws.
            WorldObject priorNpc=npcRenderSource;
            npcRenderSource=o;
            try { DrawNpc(kind,new Point(o.x,o.y)); }
            finally { npcRenderSource=priorNpc; }
            bool quest = kind == "guild" || (kind == "scout" && !Game.Has("scout_saved")) || (kind == "garden" && !Game.Has("ally_1")) || (kind == "fish" && !Game.Has("ally_2")) || (kind == "forge" && !Game.Has("ally_3")) || (kind == "innGuest" && Game.CivicConduitComplete && !Game.Has("civic_aftercare_published"));
            if (quest) WorldObjectLabel(p.x - 10, p.y - 77, 20, 26, "!", 22, gold, TextAnchor.MiddleCenter);
        }
    }
}
