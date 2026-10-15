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
        private static readonly KeyCode[] keyboardControls={KeyCode.Q,KeyCode.Space,KeyCode.E,KeyCode.F,KeyCode.T,KeyCode.Z,KeyCode.H,KeyCode.R,KeyCode.Escape,KeyCode.Return,KeyCode.Tab,KeyCode.I,KeyCode.K,KeyCode.M,KeyCode.B,KeyCode.C,KeyCode.G,KeyCode.N,KeyCode.F1};
        private string bootError;
        private readonly Color ink = PixelArt.C("#101a2b"), panel = PixelArt.C("#19283c"), paper = PixelArt.C("#f2ead7"), muted = PixelArt.C("#a4b4c5"), gold = PixelArt.C("#ebc58b"), mint = PixelArt.C("#7acabb");

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
            if(movement.Length>.19f&&!keyboardMove||!keyboardMove&&(Math.Abs(Input.GetAxisRaw("Debug Horizontal"))>.5f||Math.Abs(Input.GetAxisRaw("Debug Vertical"))>.5f))controllerUsed=true;
            bool keyboardAction=Input.GetKey(KeyCode.J);
            foreach(KeyCode key in keyboardControls)keyboardAction|=Input.GetKeyDown(key);
            if (pointerMoved || Input.GetMouseButton(0)||keyboardMove||keyboardAction) controllerUsed = false;
            lastPointer = pointer; pointerSeen = true;
            for (int i = 0; i < 8; i++) if (PadDown(i)) controllerUsed = true;
            Point? frameAim = InputAim();
            float timeLeft = frameDelta;
            while (timeLeft > .00001f)
            {
                float step = Math.Min(.05f, timeLeft);
                Game.Tick(step, movement, frameAim);
                timeLeft -= step;
            }
            UpdatePresentation(frameDelta); UpdateMenuInput(frameDelta);
            if (Game.mode == "world" && beganInWorld)
            {
                Point mouse = MouseVirtual();
                // Discrete actions take priority over continuously held attacks.
                // A fresh Q after a dodge is queued; a dodge clears older intent.
                if ((Input.GetKeyDown(KeyCode.Space) || PadDown(0))) Game.Dash(movement);
                if ((Input.GetKeyDown(KeyCode.Q) || PadDown(3))) Game.Skill(InputAim(true));
                if (Input.GetMouseButton(0) && !PointerOverInterface(mouse))
                    Game.Attack(InputAim() ?? Game.aim);
                if ((Input.GetKey(KeyCode.J) || Input.GetKey(KeyCode.JoystickButton2))) Game.Attack(InputAim() ?? Game.aim);
                if ((Input.GetKeyDown(KeyCode.E) || PadDown(1))) Game.Interact();
                if (Input.GetKeyDown(KeyCode.F)) Game.Eat();
                if (Input.GetKeyDown(KeyCode.T)) Game.DrinkTea();
                if (Input.GetKeyDown(KeyCode.Z)) Game.CommandCompanion();
                if (Input.GetKeyDown(KeyCode.H)) Game.ReturnToTown();
                if (Input.GetKeyDown(KeyCode.R)) Game.UseExplorationAbility();
                if (PadDown(4)) Game.Eat(); if (PadDown(5)) Game.CommandCompanion();
                if (PadDown(6)) OpenMenu("journal"); if (PadDown(7)) { OpenMenu("pause"); Save(); }
            }
            else if (Game.mode == "fish" && (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Space))) Game.FishHit();
            else if (Game.mode == "dialog")
            {
                int count = Game.conversation == null || !DialogueChoicesVisible() ? 0 : Game.conversation.choices.Length;
                for (int i = 0; i < Math.Min(5, count); i++) if (Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1 + i))) ChooseDialogue(i);
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
            if (settingsParent == "title" && (Game.mode == "pause" || Game.mode == "saves")) return;
            if (!Input.GetKeyDown(key) || Game.mode == "dialog" || Game.mode == "fish" || Game.mode == "title") return;
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
        private void OnApplicationPause(bool paused) { applicationPaused = paused; if (paused) { if (Game != null) Game.ClearBufferedSkill(); Save(); } }
        private void OnApplicationFocus(bool focused)
        {
            applicationFocused = focused;
            if (!focused && Game != null)
            {
                // Focus loss freezes interactive non-world modes (such as
                // fishing) instead of letting their cursor run unseen.
                // World combat additionally displays the existing pause menu.
                Game.ClearBufferedSkill();
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
            if (Game.mode == "title" && titleArt != null) Image(titleArt, 0, 0, Width, Height); else DrawWorld();
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
            // The art is drawn above its ground-space hurtbox. Clicking its
            // torso must aim at that actor, not at empty ground behind its head.
            foreach(Enemy enemy in Game.enemies)
            {
                if(!Contains(EnemyHudBounds(enemy),mouse))continue;
                Point center=ScreenPoint(enemy.Position)+new Point(0,enemy.boss?-90:-35);float distance=(center-mouse).Length;
                if(distance<best){best=distance;target=enemy.Position;}
            }
            return target;
        }
        private Point? InputAim(bool technique=false)
        {
            if(controllerUsed||Game.mode!="world"){Game.SetAimTarget(null);return null;}Point mouse=MouseVirtual();if(PointerOverInterface(mouse))return pointerAimActive?(Point?)Game.aim:null;pointerAimActive=true;
            Point target=AimTargetAtPointer(mouse),d=target-Game.Position;
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
            float follow=Game.save.reducedMotion?1:1-Mathf.Exp(-Math.Max(0,dt)*12);
            Point target=CameraTarget();
            camera=new Point(Mathf.Lerp(camera.x,target.x,follow),
                Mathf.Lerp(camera.y,target.y,follow));
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
            // An interaction may enter another room between Update and the
            // first IMGUI repaint. Prime the actual viewport BEFORE painting,
            // so a missing chunk never arrives as an opaque block.
            PrewarmTerrain();
            worldOverlayRegions.Clear();
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
                if (tile == '%' && !Game.LowTide) tile = '~';
                if ((tile == 'v' || tile == 'w' || tile == 'b') && Game.Has("seal_" + Game.save.room)) tile = '=';
                DrawSurface(x, y, tile, p); DrawAnnexBiomeLayers(x, y, tile, p);
                DrawGroundDetail(x, y, tile, p); DrawTerrainEdge(x, y, tile, p);
                if (tile == '~' && !Game.save.reducedMotion && Game.save.effectsQuality > 0) Box(p.x + 3 + (x * 5 + clock * 4) % 17, p.y + 12 + y % 5, 8, 1, new Color(.65f, .8f, .85f, .13f));
                float reveal = TerrainTileOpacity(x, y);
                if (reveal < .999f)
                    Box(p.x, p.y, Tile + .5f, Tile + .5f, new Color(fadeBase.r,fadeBase.g,fadeBase.b,
                        WorldStreaming.TerrainVeilOpacity(reveal)));
            }
            DrawArenaBoundary();
            // Sort scenery and actors by their ground position, for proper overlap.
            DrawHazardWarnings();
            foreach (Effect floorEffect in Game.effects) if (IsGroundEffect(floorEffect)) DrawCombatEffect(floorEffect);
            DrawOrderedWorld();
            foreach (Projectile p in Game.projectiles) DrawProjectileVisual(p);
            foreach (Effect e in Game.effects) if (!IsGroundEffect(e)) DrawCombatEffect(e);
            DrawExpansionWorld();
            if (!Game.save.reducedMotion && Game.save.effectsQuality > 0)
                for (int i = 0; i < 18; i++) { float x = (i * 137 + Mathf.Sin(clock * .35f + i) * 45 + Width) % Width, y = (i * 83 - clock * 6 + Height * 100) % Height; Box(x, y, 2, 2, new Color(.96f, .87f, .6f, .45f)); }
            DrawAtmosphere(); DrawPolishWorld();
            WorldObject near = Game.Nearest();
            if (near != null && Game.mode == "world" && Game.SafeTravel)
            {
                Point s = ScreenPoint(new Point(near.x, near.y)); string label = ControlKey("E", "B") + "  " + Game.L(near.name, near.en);
                Panel(s.x - 125, s.y + 14, 250, 35, new Color(.04f, .075f, .12f, .94f), gold); Text(s.x - 117, s.y + 18, 234, 27, label, 14, paper, TextAnchor.MiddleCenter);
            }
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
            Point p = ScreenPoint(new Point(o.x, o.y)); string kind = o.kind;
            if (DrawPolishObject(o) || DrawExpansionObject(o)) return;
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
                    Text(p.x-68,p.y-111,136,20,
                        Game.L(green?"稳定 · 现在操作":"等待绿色周期",
                            green?"GREEN · TUNE NOW":"WAIT FOR GREEN"),
                        11,pipe,TextAnchor.MiddleCenter);
                if(active&&master)
                    Text(p.x-73,p.y-108,146,20,
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
                    Text(p.x-78,p.y-111,156,20,
                        Game.L("保密卫兵 "+Game.LastDirectiveEnemies,
                            "GUARDIANS "+Game.LastDirectiveEnemies),
                        12,new Color(.97f,.4f,.32f),TextAnchor.MiddleCenter);
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
                    Text(p.x-69,p.y-112,138,20,
                        Game.L(Game.LastDirectiveChoice==1?
                            "公共电网 · 补充耐力":"庇护电网 · 基础疗养",
                            Game.LastDirectiveChoice==1?
                            "GRID POWER ONLINE":"PUBLIC CARE ONLINE"),
                        11,network,TextAnchor.MiddleCenter);
                }
                if(!ready)Diamond(p.x,p.y-79,6,gold);
                return;
            }
            if(kind=="ridgemarker")
            {
                bool read=Game.RidgeSeen(Game.save.room,
                    o.id.EndsWith("_0")?0:1);
                bool linked=Game.RidgeLinked(Game.save.room);
                PropImage("obelisk",0,p.x-34,p.y-84,68,90,
                    linked?new Color(.63f,.95f,.85f):read?
                        new Color(.73f,.83f,.91f):Color.white);
                Ring(new Point(o.x,o.y),.51f,
                    linked?mint:read?muted:gold,16);
                if(!read)Diamond(p.x,p.y-78,5,gold);
                return;
            }
            if(kind=="outerwatch")
            {
                bool done=Game.OuterPatrolDone(Game.save.room);
                PropImage("post",0,p.x-34,p.y-80,68,83,
                    done?new Color(.68f,.93f,.78f):Color.white);
                Ring(new Point(o.x,o.y),.43f,done?mint:
                    Game.OuterPatrolActive?new Color(1,.4f,.34f):gold,14);
                if(!done)Diamond(p.x,p.y-79,5,
                    Game.OuterPatrolActive?new Color(1,.4f,.34f):gold);
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
                }
                else
                {
                    PropImage(kind=="outerlog"?"bookshelf":"barrel",0,
                        p.x-34,p.y-71,68,76,
                        recorded?new Color(.68f,.82f,.75f,.82f):Color.white);
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
                if(!read)Text(p.x-26,p.y-84,52,20,stage.ToString(),16,
                    stage==progress+1?gold:muted,TextAnchor.MiddleCenter);
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
            if (kind == "waytrail")
            {
                bool recorded = Game.Has("waytrail_" + o.id);
                PropImage("obelisk", 0, p.x - 31, p.y - 71, 62, 73,
                    recorded ? new Color(.65f, .87f, .79f, .8f) : Color.white);
                Ring(new Point(o.x, o.y), .43f, recorded ? mint : gold, 12);
                Text(p.x - 46, p.y - 83, 92, 21, recorded ? "✓" : "?", 17,
                    recorded ? mint : gold, TextAnchor.MiddleCenter);
                return;
            }

            if (kind.StartsWith("portal"))
            {
                int destination = int.Parse(kind.Substring(6)); bool available = Game.CanEnter(destination);
                Ring(new Point(o.x, o.y), .55f, available ? gold : muted, 16);
                Box(p.x - 3, p.y - 24, 6, 18, available ? gold : muted);
                Text(p.x - 90, p.y + 18, 180, 38, Game.L(o.name.Replace("前往", ""), o.en), 13, paper, TextAnchor.MiddleCenter); return;
            }
            if (kind == "shrine" || kind == "lore" || kind.StartsWith("rune") || kind == "engine")
            {
                bool opened = kind == "shrine" ? Game.Has("core_" + Game.save.room) : kind == "lore" ? Game.Has("lore_" + Game.save.room) : Game.GateOpen;
                PropImage(kind == "engine" ? "engine" : kind == "shrine" ? "altar" : "column", 0, p.x - 32, p.y - 64, 64, 74);
                Color color = opened ? mint : gold;
                Box(p.x - 5, p.y - 31, 10, 10, color); Ring(new Point(o.x, o.y), .35f + (Game.save.reducedMotion ? 0 : Mathf.Sin(clock * 2) * .06f), color, 12);
                string mark = kind.StartsWith("rune") ? new[] { "SUN", "TIDE", "MOON" }[int.Parse(kind.Substring(4))] : kind == "lore" ? "?" : "*";
                Text(p.x - 45, p.y - 64, 90, 25, mark, 13, paper, TextAnchor.MiddleCenter); return;
            }
            if (kind == "herb" || kind == "ore" || kind == "wood")
            { PropImage(kind, 0, p.x - 27, p.y - 55, 54, 63, Game.Has("node_" + o.id) ? new Color(.6f, .6f, .6f, .45f) : Color.white); return; }
            if (kind == "rest") { if (Game.save.room != 9) PropImage("table", 0, p.x - 27, p.y - 33, 54, 42); return; }
            if (kind == "routes") return; // The sign and well are already scenery.
            DrawNpc(kind, new Point(o.x, o.y));
            bool quest = kind == "guild" || (kind == "scout" && !Game.Has("scout_saved")) || (kind == "garden" && !Game.Has("ally_1")) || (kind == "fish" && !Game.Has("ally_2")) || (kind == "forge" && !Game.Has("ally_3"));
            if (quest) Text(p.x - 10, p.y - 77, 20, 26, "!", 22, gold, TextAnchor.MiddleCenter);
        }
    }
}
