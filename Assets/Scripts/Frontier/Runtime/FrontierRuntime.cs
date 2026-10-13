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
        private bool hasSave, replacePrompt, moving, applicationPaused;
        private Point camera;
        // Authored map textures remain shared; lightweight tile-chunk readiness
        // prewarms nearby terrain before it crosses the viewport edge.
        private const int TerrainChunkTiles = 6;
        private readonly Dictionary<int, float> terrainChunkWarmAt = new Dictionary<int, float>();
        private int terrainStreamingRoom = -1, terrainStreamingFloor = -1;
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
            if (Game == null || applicationPaused || VisualReviewActive && VisualReviewPaused) return;
            clock += Time.unscaledDeltaTime; Resize();
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
            Game.Tick(Time.unscaledDeltaTime, movement, InputAim()); UpdatePresentation(Time.unscaledDeltaTime); UpdateMenuInput(Time.unscaledDeltaTime);
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
            FollowCamera(Time.unscaledDeltaTime);
            PrewarmTerrain();
            audioSystem.Update(Game, moving, Time.unscaledDeltaTime); Game.sound = "";
            saveTimer += Time.unscaledDeltaTime;
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
        { if (!focused && Game != null && Game.mode == "world") { Game.mode = "pause"; Save(); } }
        private void OnApplicationQuit() { Save(); }
        private void OnDestroy() { if (art != null) art.Dispose(); if (audioSystem != null) audioSystem.Dispose(); }

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
        { if (drawingOutline) return; Color previous = GUI.color; try { GUI.color = color; GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture); } finally { GUI.color = previous; } }
        private void Frame(float x, float y, float w, float h, Color color)
        { Box(x, y, w, 1, color); Box(x, y + h - 1, w, 1, color); Box(x, y, 1, h, color); Box(x + w - 1, y, 1, h, color); }
        private void Text(float x, float y, float w, float h, string text, int size, Color color, TextAnchor anchor = TextAnchor.UpperLeft)
        {
            textStyle.fontSize = Game != null && Game.save.largeText && size < 20 ? size + 1 : size; textStyle.normal.textColor = color; textStyle.alignment = anchor;
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
        { Color previous = GUI.color; try { GUI.color = tint ?? Color.white; GUI.DrawTexture(new Rect(x, y, w, h), texture); } finally { GUI.color = previous; } }
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
            if(Game.mode!="world")return;
            float follow=Game.save.reducedMotion?1:1-Mathf.Exp(-Math.Max(0,dt)*12);
            Point target=CameraTarget();camera=new Point(Mathf.Lerp(camera.x,target.x,follow),Mathf.Lerp(camera.y,target.y,follow));
        }
        private void Ring(Point p, float radius, Color color, int segments = 32)
        {
            Point s = ScreenPoint(p);
            for (int i = 0; i < segments; i++)
            { double a = i * Math.PI * 2 / segments; Box(s.x + (float)Math.Cos(a) * radius * Tile - 2, s.y + (float)Math.Sin(a) * radius * Tile - 2, 4, 4, color); }
        }
        private void PrewarmTerrain()
        {
            if (Game == null) return;
            bool firstView = terrainStreamingRoom != Game.save.room ||
                terrainStreamingFloor != Game.save.abyssFloor;
            if (firstView)
            {
                terrainChunkWarmAt.Clear();
                terrainStreamingRoom = Game.save.room;
                terrainStreamingFloor = Game.save.abyssFloor;
            }
            Point origin = WorldOrigin;
            int width = Game.Map.rows[0].Length, height = Game.Map.rows.Length;
            // Ten tiles of overscan preload the next region long before the
            // camera reaches its first visible pixel, even at dash speeds.
            int fromX = Math.Max(0, (int)Math.Floor((-Tile - origin.x) / Tile) - 10);
            int toX = Math.Min(width - 1, (int)Math.Ceiling((Width + Tile - origin.x) / Tile) + 10);
            int fromY = Math.Max(0, (int)Math.Floor((-Tile - origin.y) / Tile) - 10);
            int toY = Math.Min(height - 1, (int)Math.Ceiling((Height + Tile - origin.y) / Tile) + 10);
            int chunksWide = (width + TerrainChunkTiles - 1) / TerrainChunkTiles;
            for (int y = fromY / TerrainChunkTiles; y <= toY / TerrainChunkTiles; y++)
                for (int x = fromX / TerrainChunkTiles; x <= toX / TerrainChunkTiles; x++)
                {
                    int key = y * chunksWide + x;
                    if (!terrainChunkWarmAt.ContainsKey(key))
                        terrainChunkWarmAt.Add(key, firstView ? clock - 2f : clock);
                }
        }
        private float TerrainTileOpacity(int x, int y)
        {
            if (Game.save.reducedMotion || Game.save.effectsQuality == 0) return 1f;
            int wide = (Game.Map.rows[0].Length + TerrainChunkTiles - 1) / TerrainChunkTiles;
            float at;
            if (!terrainChunkWarmAt.TryGetValue(y / TerrainChunkTiles * wide + x / TerrainChunkTiles, out at))
                return 1f; // Safe direct OnGUI capture before the first Update.
            float delay = TileSeed(x, y, Game.save.room) % 9 * .012f;
            float phase = Mathf.Clamp01((clock - at - delay) / .42f);
            return phase * phase * (3 - 2 * phase);
        }
        private void DrawWorld()
        {
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
                char tile = Game.Map.rows[y][x]; if (tile == '%' && !Game.LowTide) tile = '~';
                if ((tile == 'v' || tile == 'w' || tile == 'b') && Game.Has("seal_" + Game.save.room)) tile = '=';
                DrawSurface(x, y, tile, p); DrawGroundDetail(x, y, tile, p); DrawTerrainEdge(x, y, tile, p);
                if (tile == '~' && !Game.save.reducedMotion && Game.save.effectsQuality > 0) Box(p.x + 3 + (x * 5 + clock * 4) % 17, p.y + 12 + y % 5, 8, 1, new Color(.65f, .8f, .85f, .13f));
                float reveal = TerrainTileOpacity(x, y);
                if (reveal < .999f)
                    Box(p.x, p.y, Tile + .5f, Tile + .5f, new Color(fadeBase.r, fadeBase.g, fadeBase.b, 1f - reveal));
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
