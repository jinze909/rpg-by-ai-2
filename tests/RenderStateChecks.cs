// Production renderer regression checks with 2D affine math and a simulated
// editor button skin. This is not a substitute for native Unity GPU checks.
using System;
using System.Reflection;
using Starfall;
using UnityEngine;

public static class RenderStateChecks
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static void Check(bool pass, string message) { if (!pass) throw new Exception(message); }
    static object Call(FrontierRuntime app, string method, params object[] args)
    { return typeof(FrontierRuntime).GetMethod(method, Private).Invoke(app, args); }
    static void Set(FrontierRuntime app, string field, object value) { typeof(FrontierRuntime).GetField(field, Private).SetValue(app, value); }
    static T Get<T>(FrontierRuntime app, string field) { return (T)typeof(FrontierRuntime).GetField(field, Private).GetValue(app); }
    static void Clear() { GUI.commands.Clear(); GUI.textureCalls.Clear(); GUI.buttonCalls.Clear(); }
    static void Draw(FrontierRuntime app) { Clear(); Call(app, "OnGUI"); }
    static GUI.ButtonCall Slot(float x)
    { var call = GUI.buttonCalls.Find(b => b.rect.x == x && b.rect.y == 699); if(call==null) { float compact = x == 354 ? 430 : x == 818 ? 770 : -1; call=GUI.buttonCalls.Find(b=>b.rect.x==compact&&b.rect.y==732); } Check(call != null, "Hotbar must have a real control surface in exploration or combat"); return call; }
    static GUI.TextureCall Icon(float x)
    { var call = GUI.textureCalls.Find(t => t.texture.name == "resource:Frontier/Art/Icons" && t.rect.x == x && t.rect.y == 704); Check(call != null, "Hotbar must draw its authored icon"); return call; }
    static bool Label(string text) { return GUI.commands.Exists(c => c.Contains("\"text\":" + GUI.S(text))); }
    static void Same(Color a, Color b, string why)
    { Check(Math.Abs(a.r-b.r)+Math.Abs(a.g-b.g)+Math.Abs(a.b-b.b)+Math.Abs(a.a-b.a) < .0001f, why); }
    static void Same(Matrix4x4 a, Matrix4x4 b, string why)
    { Check(Math.Abs(a.m00-b.m00)+Math.Abs(a.m01-b.m01)+Math.Abs(a.m03-b.m03)+Math.Abs(a.m10-b.m10)+Math.Abs(a.m11-b.m11)+Math.Abs(a.m13-b.m13) < .001f, why); }
    static Vector3 Center(GUI.TextureCall call)
    { return call.matrix.MultiplyPoint3x4(new Vector3(call.rect.x+call.rect.width/2, call.rect.y+call.rect.height/2, 0)); }
    static void Near(Vector3 a, Vector3 b, string why)
    { Check(Math.Abs(a.x-b.x) < .035f && Math.Abs(a.y-b.y) < .035f, why + ": " + a.x + "," + a.y + " expected " + b.x + "," + b.y); }
    static GUIStyleState[] States(GUIStyle style)
    { return new[] {style.normal,style.hover,style.active,style.focused,style.onNormal,style.onHover,style.onActive,style.onFocused}; }
    static void Prepare(FrontierRuntime app)
    {
        app.BeginVisualReview(0, 1); app.Game.enemies.Clear(); app.Game.effects.Clear(); app.Game.projectiles.Clear();
        app.Game.save.x = 12; app.Game.save.y = 14; app.Game.save.energy = 100;
        app.Game.save.inventory[6] = 2; app.Game.save.tea = 2; app.Game.toastTime = 0;
        Set(app, "camera", app.Game.Position);
    }
    static void Hotbar(FrontierRuntime app, string folder)
    {
        Screen.width = 1280; Screen.height = 800;
        // Reproduce the original high-DPI failure in the harness: clearing only
        // the ordinary skin backgrounds leaves its high-DPI bitmap alive.
        var inherited = new GUIStyle(GUI.skin.button);
        foreach (var state in States(inherited)) state.background = null;
        GUI.pixelsPerPoint = 2; Clear(); GUI.Button(new Rect(354,699,109,75), "", inherited);
        Check(GUI.textureCalls.Exists(t => t.texture.name == "native-button-background"), "Harness must reproduce the inherited high-DPI overlay");
        foreach (float dpi in new[] { 1f, 2f })
        {
            GUI.pixelsPerPoint = dpi; Prepare(app); Draw(app);
            foreach (string field in new[] { "textStyle", "buttonStyle", "disabledButtonStyle" })
                foreach (var state in States(Get<GUIStyle>(app, field)))
                    Check(state.background == null && (state.scaledBackgrounds == null || state.scaledBackgrounds.Length == 0), "Authored UI cannot inherit native skin backgrounds at any state or DPI");
            Check(Slot(354).enabled && Slot(354).background == null, "Ready Q is enabled and uncovered");
            Same(Icon(393).color, Color.white, "Ready Q retains its full authored color");
            Check(!GUI.textureCalls.Exists(t => t.texture.name == "native-button-background"), "No native bitmap can cover any menu or hotbar content");
            GUI.Export(folder, "review-hud-ready-dpi" + dpi);
            app.Game.skillCooldown = 2.4f; Draw(app);
            Check(!Slot(354).enabled && Label("3s") && Icon(393).color.r < .8f, "Cooldown shows seconds and a muted, disabled icon");
            if (dpi == 2) GUI.Export(folder, "review-hud-cooldown");
            app.Game.skillCooldown = 0; app.Game.save.energy = 10; Draw(app);
            Check(!Slot(354).enabled && !Slot(818).enabled && Label("耐力不足"), "A finished cooldown cannot conceal missing energy");
            if (dpi == 2) GUI.Export(folder, "review-hud-no-energy");
            app.Game.save.energy = 100; app.Game.Attack(new Point(1,0)); Draw(app);
            Check(!Slot(354).enabled && Label("动作中"), "Q indicates the active wind-up that really prevents casting");
            app.Game.playerAction = null; app.Game.dashTime = .1f; Draw(app);
            Check(!Slot(354).enabled && Label("动作中"), "A dodge cannot misleadingly advertise an available cast");
            app.Game.dashTime = 0; app.Game.save.companion = -1; app.Game.save.inventory[6] = 0; app.Game.save.tea = 0; Draw(app);
            Check(!Slot(470).enabled && !Slot(586).enabled && !Slot(702).enabled, "Empty consumables and absent companion stay disabled");
            app.Game.deathTime = .7f; Draw(app);
            Check(!Slot(354).enabled && !Slot(818).enabled, "Death disables combat controls");
            Prepare(app); GUI.queuedClick = new Rect(354,699,109,75); Draw(app);
            Check(GUI.queuedClick == null && app.Game.playerAction != null && app.Game.playerAction.kind == "skill" && app.Game.save.energy == 60, "Ready mouse click casts once and pays exactly one energy cost");
            app.Game.playerAction = null; GUI.queuedClick = new Rect(354,699,109,75); float energy = app.Game.save.energy; Draw(app);
            Check(GUI.queuedClick.HasValue && app.Game.playerAction == null && app.Game.save.energy == energy, "A disabled cooldown button cannot cast or spend resources"); GUI.queuedClick = null;
            Prepare(app); Draw(app); Check(Icon(878).rect.x > 818+5+45, "SPACE label and dodge icon do not overlap");
        }
    }
    static Vector3 Expected(Point world, Point camera, float height, float scale, float ox, float oy)
    { return new Vector3(ox+(640-(float)Math.Round(camera.x*36)+(float)Math.Round(world.x*36)+3)*scale, oy+(390-(float)Math.Round(camera.y*36)+(float)Math.Round(world.y*36)-4-height)*scale, 0); }
    static void Geometry(FrontierRuntime app, string folder)
    {
        Prepare(app); int[,] viewports = {{1280,800},{1920,1080},{2560,1440},{800,600},{3840,2160},{900,1600},{3440,1440}};
        Point camera = new Point(15.3f,14.8f), source = new Point(16.8f,13.4f);
        Set(app, "camera", camera); Set(app, "shakeX", 3f); Set(app, "shakeY", -4f);
        Color incoming = new Color(.2f,.4f,.6f,.8f); int cases = 0;
        for (int viewport = 0; viewport < viewports.GetLength(0); viewport++)
        {
            Screen.width = viewports[viewport,0]; Screen.height = viewports[viewport,1]; Call(app,"Resize");
            float scale = Math.Min(Screen.width/1280f,Screen.height/800f), ox = (Screen.width-1280*scale)/2, oy = (Screen.height-800*scale)/2;
            Matrix4x4 canvas = Matrix4x4.TRS(new Vector3(ox,oy,0),Quaternion.identity,new Vector3(scale,scale,1));
            for (int heading = 0; heading < 8; heading++)
            {
                double angle = heading*Math.PI/4; Point direction = new Point((float)Math.Cos(angle),(float)Math.Sin(angle));
                GUI.matrix = canvas; GUI.color = incoming; Clear();
                Call(app,"DrawAuthoredEffect",new Effect("slash",source,.2f) {direction=direction});
                Check(GUI.textureCalls.Count == 1, "A swing has one anchored slash texture"); var slash = GUI.textureCalls[0];
                Near(Center(slash), Expected(source,camera,30,scale,ox,oy), "Rotation must leave the slash on its world source at every viewport and heading");
                Vector3 tip = slash.matrix.MultiplyPoint3x4(new Vector3(slash.rect.x+slash.rect.width,slash.rect.y+slash.rect.height/2,0));
                Vector3 center = Center(slash); Near(tip,new Vector3(center.x+direction.x*slash.rect.width*.5f*scale,center.y+direction.y*slash.rect.width*.5f*scale,0), "Slash direction follows the locked attack heading");
                Same(GUI.matrix,canvas,"Atlas restores the enclosing canvas"); Same(GUI.color,incoming,"Atlas restores the enclosing tint");
                Clear(); Call(app,"DrawProjectileVisual",new Projectile {position=source,velocity=direction*12,friendly=true,visual="arrow",age=.2f});
                Check(GUI.textureCalls.Count == 4,"Flying arrow draws three trails and one head");
                for (int trail = 0; trail < 3; trail++) Near(Center(GUI.textureCalls[trail]),Expected(source-direction*(3-trail)*.18f,camera,32,scale,ox,oy),"Projectile trail stays behind its actual position");
                var head = GUI.textureCalls[3]; Near(Center(head),Expected(source,camera,32,scale,ox,oy),"Projectile head stays on the missile, not on the player or an old canvas pivot");
                Vector3 arrowTip = head.matrix.MultiplyPoint3x4(new Vector3(head.rect.x+head.rect.width,head.rect.y+head.rect.height/2,0)); center=Center(head);
                Near(arrowTip,new Vector3(center.x+direction.x*head.rect.width*.5f*scale,center.y+direction.y*head.rect.width*.5f*scale,0),"The right-facing authored arrow rotates along its flight");
                Same(GUI.matrix,canvas,"Projectile trails cannot leak their rotation"); Same(GUI.color,incoming,"Projectile trails cannot leak their tint");
                cases++;
            }
            foreach (string kind in new[] {"impact","magicImpact","heal","defeat","discover"})
            {
                Clear(); Call(app,"DrawAuthoredEffect",new Effect(kind,source,.5f));
                float height = kind == "discover" ? 0 : kind == "defeat" ? 18 : 30;
                Near(Center(GUI.textureCalls[0]),Expected(source,camera,height,scale,ox,oy),"Impact, death, healing and discovery keep their own world anchors");
            }
            Clear(); Call(app,"DrawProjectileVisual",new Projectile {position=source,velocity=new Point(0,-8),visual="enemyOrb",theme=7,age=.09f});
            Near(Center(GUI.textureCalls[3]),Expected(source,camera,32,scale,ox,oy),"Boss missiles keep a continuous flight height without a release jump");
            Point logical=(Point)Call(app,"ScreenPoint",source);
            Input.mousePosition=new Vector3(ox+logical.x*scale,Screen.height-oy-logical.y*scale,0);
            Point pointer=(Point)Call(app,"MouseVirtual"); Check((pointer-logical).Length<.001f,"Mouse aiming inverts the same letterboxed canvas at every resolution");
            Check((bool)Call(app,"PointerOverInterface",new Point(-1,390)),"Letterbox bars cannot cause world attacks");
            if (viewport == 1 || viewport == 3 || viewport == 5)
            {
                app.Game.effects.Clear(); app.Game.effects.Add(new Effect("slash",app.Game.Position,.25f){direction=new Point(viewport==3?0:1,viewport==3?-1:0)});
                app.Game.projectiles.Clear(); app.Game.projectiles.Add(new Projectile {position=app.Game.Position+new Point(1,0),velocity=new Point(9,0),friendly=true,visual="arrow"});
                Set(app,"camera",app.Game.Position); Draw(app); GUI.Export(folder,"review-render-"+Screen.width+"x"+Screen.height); Set(app,"camera",camera);
            }
        }
        Screen.width=Screen.height=0; Call(app,"Resize"); Point minimized=(Point)Call(app,"MouseVirtual");
        Check(!float.IsNaN(minimized.x)&&!float.IsInfinity(minimized.x)&&!float.IsNaN(minimized.y)&&!float.IsInfinity(minimized.y),"A minimized zero-size viewport cannot create invalid aim coordinates");
        Console.WriteLine("PASS " + cases + " viewport/heading combinations, real effect and projectile anchors, trail directions and mouse inversion (offline 2D affine checks)");
    }
    static void WorldMapRasterUsesOneDrawAndUpdatesBuiltBridges(FrontierRuntime app)
    {
        app.BeginVisualReview(0,1);
        var map=app.Game.data.maps[0];
        const int x=111,y=52;
        Check(map.rows[y][x]=='?', "Bridge geometry must exist in authored map");
        Clear();
        Call(app,"DrawTerrainPreview",map,0,false,888f,264f,2f);
        var first=Get<Texture2D>(app,"terrainPreview");
        Check(first!=null&&first.width==120&&first.height==80,
            "Large sixth-ring terrain is actually rasterized at the correct size");
        Check(GUI.textureCalls.Count==1,
            "Large map menu renders its backdrop in one image, not 9600 per-cell calls");
        var blocked=first.pixels[(first.height-1-y)*first.width+x];
        Clear();
        Call(app,"DrawTerrainPreview",map,0,false,888f,264f,2f);
        Check(object.ReferenceEquals(first,Get<Texture2D>(app,"terrainPreview"))&&
            GUI.textureCalls.Count==1,
            "Repeated GUI painting reuses the same cached map texture");
        // Unrelated combat/story updates must not rebuild an expensive map.
        app.Game.revision++;
        Clear();
        Call(app,"DrawTerrainPreview",map,0,false,888f,264f,2f);
        Check(object.ReferenceEquals(first,Get<Texture2D>(app,"terrainPreview")),
            "Unrelated gameplay revisions preserve the existing cached map raster");
        app.Game.save.flags.Add("far_bridge_0");
        app.Game.revision++;
        Clear();
        Call(app,"DrawTerrainPreview",map,0,false,888f,264f,2f);
        var opened=Get<Texture2D>(app,"terrainPreview");
        var road=opened.pixels[(opened.height-1-y)*opened.width+x];
        Check(!object.ReferenceEquals(first,opened)&&first.destroyed,
            "Construction invalidates and disposes the old menu-map raster");
        Check(Math.Abs(blocked.r-road.r)+Math.Abs(blocked.g-road.g)+
            Math.Abs(blocked.b-road.b)>.01f,
            "Actual repaired bridge changes raster color from blocked rock to path");
        // Fogged current-map previews have a different dependency set:
        // only its own discoveries must invalidate, not unrelated dialogue.
        Clear();
        Call(app,"DrawTerrainPreview",map,0,true,888f,264f,2f);
        var hidden=Get<Texture2D>(app,"terrainPreview");
        const int fogX=110,fogY=68;
        var shaded=hidden.pixels[(hidden.height-1-fogY)*hidden.width+fogX];
        app.Game.save.flags.Add("totally_unrelated_story_note");
        app.Game.revision++;
        Clear();
        Call(app,"DrawTerrainPreview",map,0,true,888f,264f,2f);
        Check(object.ReferenceEquals(hidden,Get<Texture2D>(app,"terrainPreview")),
            "New story or combat flags cannot regenerate fogged world-map textures");
        app.Game.save.flags.Add("annex_seen_0_18_11");
        Clear();
        Call(app,"DrawTerrainPreview",map,0,true,888f,264f,2f);
        var revealed=Get<Texture2D>(app,"terrainPreview");
        var visible=revealed.pixels[(revealed.height-1-fogY)*revealed.width+fogX];
        Check(!object.ReferenceEquals(hidden,revealed)&&hidden.destroyed,
            "Real nearby exploration triggers a fresh cached fog image");
        Check(Math.Abs(shaded.r-visible.r)+Math.Abs(shaded.g-visible.g)+
            Math.Abs(shaded.b-visible.b)>.03f,
            "Discovered sixth-ring sectors actually brighten on the map");
        // Every pixel must still reflect the exact existing SaveData v2
        // sector semantics, including the legacy 36x24 discovery bits.
        Clear();
        Call(app,"DrawTerrainPreview",map,0,false,888f,264f,2f);
        var allVisible=Get<Texture2D>(app,"terrainPreview");
        int checkedPixels=0;
        for(int sy=0;sy<allVisible.height;sy++)
            for(int sx=0;sx<allVisible.width;sx++)
            {
                int n=(allVisible.height-1-sy)*allVisible.width+sx;
                Color a=revealed.pixels[n],b=allVisible.pixels[n];
                bool same=Math.Abs(a.r-b.r)+Math.Abs(a.g-b.g)+
                    Math.Abs(a.b-b.b)<.0001f;
                Check(same==app.Game.Explored(0,sx,sy),
                    "Fogged terrain must match saved sector visibility at "+sx+","+sy);
                checkedPixels++;
            }
        Check(checkedPixels==9600,"Raster parity checks every real outdoor-map tile");
        // Cleanup is handled by the normal EndVisualReview / OnDestroy path.
        Console.WriteLine("PASS real 120x80 map raster caching, draw-call reduction and repaired-bridge preview (offline)");
    }
    static void BridgeAndSealMinimapsMatchPhysicalCollision(FrontierRuntime app)
    {
        app.BeginVisualReview(0,1);
        var game=app.Game;
        var map=game.data.maps[0];
        game.save.flags.Remove("far_bridge_0");
        game.save.flags.Add("annex_seen_0_18_8");
        const int x=111,y=52;
        Check(map.rows[y][x]=='?'&&!game.FarBridgeOpen(0),
            "The tested physical bridge is genuinely blocked");
        Check((char)Call(app,"TerrainPreviewGlyph",map,0,x,y)=='#',
            "The unrepaired bridge is marked as impassable on the map");
        game.save.x=111;game.save.y=52;
        Clear();Call(app,"MiniTerrainWindow",1024f,42f);
        var blocked=GUI.textureCalls.Find(c=>c.rect.x==1159f&&c.rect.y==105f&&
            c.rect.width==9f&&c.rect.height==9f);
        Check(blocked!=null,"The physical bridge appears inside the live minimap");
        Same(blocked.color,PixelArt.C("#566879"),
            "An unrepaired bridge must be drawn as blocked rock, not walkable grass");
        game.save.flags.Add("far_bridge_0");
        Check((char)Call(app,"TerrainPreviewGlyph",map,0,x,y)=='=',
            "The same crossing becomes road when repaired");
        Clear();Call(app,"MiniTerrainWindow",1024f,42f);
        var opened=GUI.textureCalls.Find(c=>c.rect.x==1159f&&c.rect.y==105f&&
            c.rect.width==9f&&c.rect.height==9f);
        Check(opened!=null,"The reopened bridge remains inside the live minimap");
        Same(opened.color,PixelArt.C("#a09877"),
            "Repaired bridge must be shown as the real walkable road");
        var gate=game.data.maps[1];int gx=-1,gy=-1;
        for(int yy=0;yy<gate.rows.Length&&gx<0;yy++)
            for(int xx=0;xx<gate.rows[yy].Length;xx++)
                if(gate.rows[yy][xx]=='v'||gate.rows[yy][xx]=='w'||
                    gate.rows[yy][xx]=='b'){gx=xx;gy=yy;break;}
        Check(gx>=0,"A real sealed terrain tile exists in the campaign");
        game.save.flags.Remove("seal_1");
        Check((char)Call(app,"TerrainPreviewGlyph",gate,1,gx,gy)=='#',
            "Locked seal tiles must be represented as blocked on the map");
        Clear();Call(app,"DrawTerrainPreview",gate,1,false,888f,264f,2f);
        var sealedRaster=Get<Texture2D>(app,"terrainPreview");
        Color sealedPixel=sealedRaster.pixels[(sealedRaster.height-1-gy)*sealedRaster.width+gx];
        game.save.flags.Add("seal_1");
        Check((char)Call(app,"TerrainPreviewGlyph",gate,1,gx,gy)=='=',
            "Opened seals must be represented as traversable road");
        Clear();Call(app,"DrawTerrainPreview",gate,1,false,888f,264f,2f);
        var openRaster=Get<Texture2D>(app,"terrainPreview");
        Color openPixel=openRaster.pixels[(openRaster.height-1-gy)*openRaster.width+gx];
        Check(!object.ReferenceEquals(sealedRaster,openRaster)&&sealedRaster.destroyed,
            "Unlocking a sealed gate invalidates and disposes the full-map terrain raster");
        Check(Math.Abs(openPixel.r-sealedPixel.r)+Math.Abs(openPixel.g-sealedPixel.g)+
            Math.Abs(openPixel.b-sealedPixel.b)>.03f,
            "The full map physically changes the sealed cell from stone to road");
        Console.WriteLine("PASS live minimap bridge repair and sealed gate collision colors (offline)");
    }
    static void CourierStatusIsVisibleEvenOutsideFarshore(FrontierRuntime app)
    {
        app.BeginVisualReview(0,1);
        app.Game.save.english=true;
        // Use the same transient flag activated by the real courier action;
        // the CoreChecks regression exercises its complete start/cancel path.
        var field=typeof(GameCore).GetField("courierRoom",
            BindingFlags.Instance|BindingFlags.NonPublic);
        Check(field!=null,"The active delivery is a genuine core runtime state");
        field.SetValue(app.Game,0);
        Clear();Call(app,"OnGUI");
        Check(GUI.commands.Exists(c=>c.Contains("RELIEF PARCEL")&&
            c.Contains("CROSS BRIDGE ON FOOT")),
            "The early outdoor-region HUD renders the parcel's live timed objective");
        Check(GUI.commands.Exists(c=>c.Contains("Homeward Deliveries"))&&
            GUI.commands.Exists(c=>c.Contains("Cross the real bridge on foot")),
            "The objective panel follows the timed parcel without changing saved quest tracking");
        field.SetValue(app.Game,-1);
        Clear();Call(app,"OnGUI");
        Check(!GUI.commands.Exists(c=>c.Contains("RELIEF PARCEL")),
            "Completing or cancelling delivery removes the now-stale HUD prompt");
        Check(!GUI.commands.Exists(c=>c.Contains("Homeward Deliveries")),
            "The normal pinned objective panel returns after parcel transport ends");
        Console.WriteLine("PASS live civilian courier timed status in early-region production HUD (offline)");
    }
    static void CourierPackShowsRealPhysicalDamage(FrontierRuntime app)
    {
        app.BeginVisualReview(0,1);
        var g=app.Game;
        g.save.flags.Add("far_bridge_0");g.save.flags.Add("far_shelter_0");
        g.save.english=true;g.enemies.Clear();
        var bridge=Array.Find(g.Map.objects,o=>o.kind=="farbridge");
        Check(bridge!=null,"Authored parcel bridge exists in the real map");
        g.save.x=bridge.x;g.save.y=bridge.y;
        g.Talk(bridge);
        int pickup=Array.FindIndex(g.conversation.choices,c=>c.action=="far_courier_start");
        Check(pickup>=0,"The actual bridge presents a real package pickup");
        g.Choose(pickup);
        Check(g.FarCourierActive&&g.FarCourierIntegrity==100,
            "Actual pickup equips an undamaged transient cargo pack");
        Clear();Call(app,"DrawFarCourierPack");
        Check(GUI.commands.Exists(c=>c.Contains("PARCEL 100%")),
            "World-space parcel label shows real cargo condition on the canonical hero");
        var hurt=typeof(GameCore).GetMethod("Hurt",
            BindingFlags.Instance|BindingFlags.NonPublic);
        g.invulnerable=0;
        hurt.Invoke(g,new object[]{20f,"shot",null});
        Clear();Call(app,"DrawFarCourierPack");
        Check(g.FarCourierIntegrity<100&&
            GUI.commands.Exists(c=>c.Contains("PARCEL 76%")),
            "Production parcel render updates the physical cargo indicator after an actual hit");
        g.save.y+=4;g.Close();g.Tick(.05f,new Point());
        Clear();Call(app,"DrawFarCourierPack");
        Check(!g.FarCourierActive&&GUI.commands.Count==0&&GUI.textureCalls.Count==0,
            "A lost cargo pack and its floating status vanish immediately on route reset");
        Console.WriteLine("PASS attached civilian cargo durability visuals, actual hit and disappearance (offline)");
    }
    public static void MapRasterRegression(FrontierRuntime app)
    { WorldMapRasterUsesOneDrawAndUpdatesBuiltBridges(app); }

    static void RestoresState(FrontierRuntime app)
    {
        Screen.width=1920; Screen.height=1080; Prepare(app);
        Matrix4x4 matrix=Matrix4x4.TRS(new Vector3(51,29,0),Quaternion.Euler(0,0,15),new Vector3(1.3f,1.3f,1)); Color color=new Color(.3f,.7f,.8f,.9f);
        GUI.matrix=matrix;GUI.color=color;GUI.enabled=false;Draw(app);
        Same(GUI.matrix,matrix,"Normal OnGUI restores the caller's transform");Same(GUI.color,color,"Normal OnGUI restores the caller's tint");Check(!GUI.enabled,"Normal OnGUI restores the caller's enabled state");
        GameCore game=app.Game;var property=typeof(FrontierRuntime).GetProperty("Game");
        try { property.SetValue(app,null,null);Draw(app);Same(GUI.matrix,matrix,"Boot error path cannot leak a scaled matrix");Same(GUI.color,color,"Boot error path cannot leak white tint");Check(!GUI.enabled,"Boot error path cannot enable another UI"); }
        finally {property.SetValue(app,game,null);}
    }
    public static void Run(FrontierRuntime app,string folder)
    {
        int width=Screen.width,height=Screen.height;Matrix4x4 matrix=GUI.matrix;Color color=GUI.color;bool enabled=GUI.enabled;float dpi=GUI.pixelsPerPoint;Vector3 mouse=Input.mousePosition;
        try {Hotbar(app,folder);Geometry(app,folder);WorldMapRasterUsesOneDrawAndUpdatesBuiltBridges(app);BridgeAndSealMinimapsMatchPhysicalCollision(app);CourierStatusIsVisibleEvenOutsideFarshore(app);CourierPackShowsRealPhysicalDamage(app);RestoresState(app);Console.WriteLine("PASS Ready/cooldown/energy/casting/dodge/death/consumable/companion hotbar states at 1x and 2x DPI; enabled and disabled click dispatch; GUI state isolation");}
        finally {app.EndVisualReview();Screen.width=width;Screen.height=height;GUI.matrix=matrix;GUI.color=color;GUI.enabled=enabled;GUI.pixelsPerPoint=dpi;GUI.queuedClick=null;Input.mousePosition=mouse;Clear();Call(app,"Resize");}
    }
}
