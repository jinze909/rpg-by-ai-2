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
        // This is triggered by authentic movement through real collision
        // rather than inserting enemies directly into the renderer.
        g.hitStop=0;
        for(int i=0;i<70&&!g.FarCourierCrossed;i++)
            g.Tick(.05f,new Point(0,1));
        Check(g.FarCourierCrossed&&g.FarCourierRaidersAlive==2,
            "Production crossing populates the visible real danger marker");
        Clear();Call(app,"DrawFarCourierPack");
        Check(GUI.commands.Exists(s=>s.Contains("RAIDERS")),
            "Actual carried-parcel world rendering identifies live interception");
        var guard=typeof(GameCore).GetField("courierGuardLeft",
            BindingFlags.Instance|BindingFlags.NonPublic);
        Check(guard!=null,"A real transient convoy ward field exists");
        g.save.reducedMotion=true;guard.SetValue(g,5f);
        Clear();Call(app,"DrawFarCourierPack");
        Check(GUI.commands.Exists(c=>c.Contains("WARD 5s")),
            "Active convoy wards remain visibly indicated even with reduced motion");
        guard.SetValue(g,0f);
        Clear();Call(app,"DrawFarCourierPack");
        Check(!GUI.commands.Exists(c=>c.Contains("WARD ")),
            "The protective ring and countdown retire at the end of their duration");
        g.save.y+=4;g.Close();g.Tick(.05f,new Point());
        Clear();Call(app,"DrawFarCourierPack");
        Check(!g.FarCourierActive&&GUI.commands.Count==0&&GUI.textureCalls.Count==0,
            "A lost cargo pack and its floating status vanish immediately on route reset");
        Console.WriteLine("PASS attached civilian cargo durability visuals, actual hit and disappearance (offline)");
    }
    public static void MapRasterRegression(FrontierRuntime app)
    { WorldMapRasterUsesOneDrawAndUpdatesBuiltBridges(app); }

    static void PublicRefugeChoicesHaveRealWorldVisualFeedback(FrontierRuntime app)
    {
        app.BeginVisualReview(0,1);
        GameCore g=app.Game;g.save.flags.Add("far_shelter_0");
        var shelter=Array.Find(g.Map.objects,o=>o.kind=="farshelter");
        var checkpoint=Array.Find(g.Map.objects,o=>o.kind=="farcheck");
        Check(shelter!=null&&checkpoint!=null,
            "Existing map contains both a physical public refuge and inspection post");
        Clear();Call(app,"DrawObject",shelter);
        Check(GUI.textureCalls.Count>0,"The original public refuge is actually rendered");
        Color basic=GUI.textureCalls[0].color;
        g.save.flags.Add("civic_supply_clinic_0");
        Clear();Call(app,"DrawObject",shelter);
        Check(GUI.textureCalls.Count>0,
            "A resident-funded clinic keeps its original shelter art");
        Color clinic=GUI.textureCalls[0].color;
        Check(Math.Abs(clinic.r-basic.r)+Math.Abs(clinic.g-basic.g)+
            Math.Abs(clinic.b-basic.b)>.03f,
            "Funded clinic has a distinct visible in-world medical tint");
        g.save.flags.Remove("civic_supply_clinic_0");
        g.save.flags.Add("civic_supply_watch_0");
        Clear();Call(app,"DrawObject",shelter);
        Color watch=GUI.textureCalls[0].color;
        Check(Math.Abs(watch.r-clinic.r)+Math.Abs(watch.g-clinic.g)+
            Math.Abs(watch.b-clinic.b)>.03f,
            "The alternative lookout keeps distinct amber world art");
        Clear();Call(app,"DrawObject",checkpoint);
        Check(GUI.textureCalls.Count>0,
            "The same route's civilian checkpoint remains a real rendered object");
        g.save.flags.Remove("civic_supply_watch_0");
        g.save.flags.Add("civic_supply_started");
        g.save.flags.Add("far_courier_0");
        Clear();Call(app,"DrawObject",checkpoint);
        Check(GUI.textureCalls.Count>0,
            "Pending inquest uses the original physically inspectable route post");
        app.BeginVisualReview(9,1);g=app.Game;
        var guest=Array.Find(g.Map.objects,o=>o.kind=="farrefugerecord");
        Check(guest!=null,"Guest registry is placed in the real Hearth Inn");
        g.save.flags.Add("civic_supply_started");
        Clear();Call(app,"DrawObject",guest);
        Check(GUI.textureCalls.Count>0,
            "Active public inquest highlights the actual guest registry");
        g.save.flags.Add("civic_supply_complete");
        Clear();Call(app,"DrawObject",guest);
        Check(GUI.textureCalls.Count>0,
            "Published records visibly change the registry tint");
        Console.WriteLine("PASS physical inquest checkpoint, clinic and watch art, and live guest registry (offline)");
    }
    static void CivicServiceOverlayFollowsRealHeroAndRevertsAfterFailure(FrontierRuntime app)
    {
        app.BeginVisualReview(0,1);
        var g=app.Game;g.save.english=true;
        g.save.flags.Add("civic_supply_complete");
        g.save.flags.Add("civic_supply_clinic_0");
        g.save.flags.Add("far_shelter_0");
        g.enemies.Clear();
        var refuge=Array.Find(g.Map.objects,o=>o.kind=="farshelter");
        Check(refuge!=null,"An actual public refuge is present on the authored map");
        g.save.x=refuge.x;g.save.y=refuge.y;g.Talk(refuge);
        int begin=Array.FindIndex(g.conversation.choices,
            c=>c.action=="civic_response_start");
        Check(begin>=0,"Physical refuge offers the live medicine assignment");
        g.Choose(begin);
        Check(g.CivicResponseClinicRun&&g.CivicResponseIntegrity==100,
            "The production hero really picks up a pristine medical crate");
        Clear();Call(app,"DrawCivicResponsePack");
        Check(GUI.commands.Exists(c=>c.Contains("MEDICINE 100%"))&&
            GUI.textureCalls.Count>0,
            "Existing canonical hero carries a visible medical kit and real condition label");
        var hurt=typeof(GameCore).GetMethod("Hurt",
            BindingFlags.Instance|BindingFlags.NonPublic);
        g.invulnerable=0;
        hurt.Invoke(g,new object[]{20f,"shot",null});
        Clear();Call(app,"DrawCivicResponsePack");
        Check(g.CivicResponseIntegrity<100&&
            GUI.commands.Exists(c=>c.Contains("MEDICINE "))&&
            !GUI.commands.Exists(c=>c.Contains("MEDICINE 100%")),
            "The attached kit changes to match authentic post-armor combat damage");
        Screen.width=1280;Screen.height=800;Draw(app);
        Check(GUI.commands.Exists(c=>c.Contains("CIVIC SERVICE"))&&
            GUI.commands.Exists(c=>c.Contains("Live Civic Service Trials")),
            "Production HUD overrides unrelated pinned quests and shows the real timed delivery");
        g.hitStop=0;g.save.y+=4;g.Tick(.05f,new Point());
        Clear();Call(app,"DrawCivicResponsePack");
        Check(!g.CivicResponseActive&&GUI.commands.Count==0&&
            GUI.textureCalls.Count==0,
            "Mission abort removes the attached kit without changing canonical character art");
        g.save.flags.Remove("civic_supply_clinic_0");
        g.save.flags.Add("civic_supply_watch_0");
        g.save.x=refuge.x;g.save.y=refuge.y;
        g.enemies.Clear();g.Talk(refuge);
        begin=Array.FindIndex(g.conversation.choices,c=>c.action=="civic_response_start");
        Check(begin>=0,"The alternative watch vote offers its proper battle action");
        g.Choose(begin);
        Clear();Call(app,"DrawCivicResponsePack");
        Check(g.CivicResponseWatchRun&&g.CivicResponseEnemiesAlive==2&&
            GUI.commands.Exists(c=>c.Contains("WATCH RAIDERS 2")),
            "A live defense paints its actual enemy count beside the original hero");
        Console.WriteLine("PASS real chapter-nine timed HUD, hero-carried medicine durability and lookout danger (offline)");
    }
    static void ChapterTenVisualSitesAndSecurityHUD(FrontierRuntime app)
    {
        app.BeginVisualReview(4,1);
        var game=app.Game;
        game.save.flags.Add("last_directive_started");
        game.save.english=true;
        var testimony=Array.Find(game.Map.objects,o=>o.kind=="lastdirectiveclue0");
        var control=Array.Find(game.Map.objects,o=>o.kind=="lastdirectivecore");
        Check(testimony!=null&&control!=null,
            "Four physically authored citadel testimony/console objects are present");
        Clear();Call(app,"DrawObject",testimony);
        Check(GUI.textureCalls.Count>0,
            "Original in-world painter renders the first new story artifact");
        Color unknown=GUI.textureCalls[0].color;
        game.save.flags.Add("last_directive_clue_0");
        Clear();Call(app,"DrawObject",testimony);
        Color known=GUI.textureCalls[0].color;
        Check(Math.Abs(unknown.r-known.r)+Math.Abs(unknown.g-known.g)+
            Math.Abs(unknown.b-known.b)>.02f,
            "Investigated clue changes its in-world tint, not just abstract quest text");
        game.save.flags.Add("last_directive_clue_1");
        game.save.flags.Add("last_directive_clue_2");
        game.enemies.Clear();
        game.save.x=control.x;game.save.y=control.y;
        game.Talk(control);
        int answer=Array.FindIndex(game.conversation.choices,
            c=>c.action=="last_directive_code_0");
        Check(answer>=0,"Actual physical console presents the history-based cipher");
        game.Choose(answer);
        Check(game.LastDirectiveWave==1&&game.LastDirectiveEnemies==2,
            "Correct in-world cipher spawns two live enemy AI units");
        Clear();Call(app,"DrawObject",control);
        Check(GUI.commands.Exists(c=>c.Contains("GUARDIANS 2")),
            "The physically rendered terminal warns of the exact live guardian count");
        Screen.width=1280;Screen.height=800;
        Draw(app);
        Check(GUI.commands.Exists(c=>c.Contains("LAST DIRECTIVE"))&&
            GUI.commands.Exists(c=>c.Contains("Chapter Ten")),
            "Live Chapter Ten security fights appear in original timed conditions panel and quest HUD");
        app.BeginVisualReview(0,1);
        game=app.Game;
        var council=Array.Find(game.Map.objects,o=>o.kind=="lastdirectiveboard");
        Check(council!=null,"Emberfall gained a physically placed public decision desk");
        Clear();Call(app,"DrawObject",council);
        Check(GUI.textureCalls.Count>0,
            "New town council is rendered in the real world scene");
        game.save.flags.Add("last_directive_complete");
        game.save.flags.Add("last_directive_open");
        Clear();Call(app,"DrawObject",council);
        Color publicChoice=GUI.textureCalls[0].color;
        game.save.flags.Remove("last_directive_open");
        game.save.flags.Add("last_directive_shelter");
        Clear();Call(app,"DrawObject",council);
        Color shelterChoice=GUI.textureCalls[0].color;
        Check(Math.Abs(publicChoice.r-shelterChoice.r)+
            Math.Abs(publicChoice.g-shelterChoice.g)+
            Math.Abs(publicChoice.b-shelterChoice.b)>.04f,
            "The ending visibly changes the actual public town monument palette");
        Console.WriteLine("PASS Chapter Ten real world testimony, terminal security warning and HUD (offline)");
    }
    static void ChapterElevenLiveValvePhaseAndFullHUD(FrontierRuntime app)
    {
        app.BeginVisualReview(4,1);
        var g=app.Game;g.enemies.Clear();g.save.english=true;
        g.save.flags.Add("last_directive_complete");
        g.save.flags.Add("last_directive_open");
        g.save.inventory[4]=4;
        var desk=Array.Find(g.Map.objects,o=>o.kind=="civicconduitmaster");
        var first=Array.Find(g.Map.objects,o=>o.kind=="civicconduitvalve");
        Check(desk!=null&&first!=null&&
            g.CanStand(new Point(first.x,first.y)),
            "Physically authored public-grid stations occupy real traversable map tiles");
        g.save.x=desk.x;g.save.y=desk.y;
        g.Talk(desk);
        int start=Array.FindIndex(g.conversation.choices,
            c=>c.action=="civic_conduit_start");
        Check(start>=0,"Real western grid console offers physical fuse activation");
        g.Choose(start);
        Check(g.CivicConduitActive&&g.CivicConduitWindowOpen&&
            g.CivicConduitIntegrity==100,
            "A live, undamaged public circuit begins in the first green phase");
        Clear();Call(app,"DrawCivicConduitCoil");
        Check(GUI.textureCalls.Count>0&&
            GUI.commands.Exists(c=>c.Contains("CONDUCTOR 100%")),
            "Canonical playable hero visibly carries one live 100% charge coil with timer");

        Clear();Call(app,"DrawObject",first);
        Check(GUI.textureCalls.Count>0&&
            GUI.commands.Exists(c=>c.Contains("GREEN")),
            "The actual first field valve renders an explicit green tune-now instruction");
        Color green=GUI.textureCalls[0].color;
        for(int i=0;i<43;i++)g.Tick(.05f,new Point());
        Check(g.CivicConduitActive&&!g.CivicConduitWindowOpen,
            "A real elapsed clock closes the stability window without fake input");
        Clear();Call(app,"DrawCivicConduitCoil");
        Check(GUI.commands.Exists(c=>c.Contains("CONDUCTOR "))&&
            !GUI.commands.Exists(c=>c.Contains("CONDUCTOR 100%")),
            "Hero-mounted coil reflects genuine elapsed conductor degradation");

        Clear();Call(app,"DrawObject",first);
        Check(GUI.commands.Exists(c=>c.Contains("WAIT FOR GREEN")),
            "Closed physical valve visibly teaches the player to wait for green");
        Color locked=GUI.textureCalls[0].color;
        Check(Math.Abs(green.r-locked.r)+Math.Abs(green.g-locked.g)+
            Math.Abs(green.b-locked.b)>.03f,
            "The valve's in-world color changes with the actual game clock");
        Screen.width=1280;Screen.height=800;
        Draw(app);
        Check(GUI.commands.Exists(c=>c.Contains("CIVIC GRID"))&&
            GUI.commands.Exists(c=>c.Contains("Chapter Eleven")),
            "Original production HUD exposes live chapter timing and correct active objective");
        g.save.reducedMotion=true;
        Clear();Call(app,"DrawObject",first);
        Check(GUI.commands.Exists(c=>c.Contains("WAIT FOR GREEN")),
            "Reduced-motion mode still clearly shows a static readable phase cue");
        app.BeginVisualReview(4,1);
        g=app.Game;g.save.flags.Add("civic_conduit_complete");
        var done=Array.Find(g.Map.objects,o=>o.kind=="civicconduitvalve");
        Clear();Call(app,"DrawObject",done);
        Check(GUI.textureCalls.Count>0,
            "A completed physical valve remains visible and does not disappear");
        app.BeginVisualReview(1,1);
        g=app.Game;
        var refuge=Array.Find(g.Map.objects,o=>o.kind=="farshelter");
        Check(refuge!=null,"A true regional public shelter exists");
        g.save.flags.Add("far_shelter_1");
        g.save.flags.Add("civic_conduit_complete");
        g.save.flags.Add("last_directive_open");
        Clear();Call(app,"DrawObject",refuge);
        Check(GUI.commands.Exists(c=>c.Contains("GRID POWER ONLINE")),
            "Every rebuilt refuge visibly shows the choice-specific public energy grid");
        g.save.flags.Remove("last_directive_open");
        g.save.flags.Add("last_directive_shelter");
        Clear();Call(app,"DrawObject",refuge);
        Check(GUI.commands.Exists(c=>c.Contains("PUBLIC CARE ONLINE"))&&
            !GUI.commands.Exists(c=>c.Contains("GRID POWER ONLINE")),
            "The alternative healing network is distinctly labeled in actual regional art");
        g.save.flags.Remove("civic_conduit_complete");
        Clear();Call(app,"DrawObject",refuge);
        Check(!GUI.commands.Exists(c=>c.Contains("PUBLIC CARE ONLINE")),
            "Original legacy shelter art remains unchanged before the actual powered-grid completion");
        Console.WriteLine("PASS chapter-eleven live valve phase colors, readable clock cues and original HUD (offline)");
    }
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
    // These cases previously disagreed across terrain shading, foam and
    // river shimmer: Farshore walkable low tide still looked underwater.
    private static void TideVisualSurfaceConsistency()
    {
        var water=typeof(FrontierRuntime).GetMethod("IsSurfaceWater",
            BindingFlags.NonPublic|BindingFlags.Static);
        Check(water!=null,"Single visual tide classifier missing");
        Check((bool)water.Invoke(null,new object[]{'~',5,true}),
            "Deep water remains water during low tide");
        Check((bool)water.Invoke(null,new object[]{'%',5,false}),
            "Farshore high tide must display water");
        Check(!(bool)water.Invoke(null,new object[]{'%',5,true}),
            "Walkable low tide cannot show waves or ocean foam");
        Check(!(bool)water.Invoke(null,new object[]{'%',4,false}),
            "Other regions' dry ground cannot acquire tide shimmer");
        Check(!(bool)water.Invoke(null,new object[]{'=',5,false}),
            "Road cannot accidentally receive water animation");
        Console.WriteLine("PASS actual shared surface-water decision across low/high tide");
    }
    private static void CoverOcclusionIncludesEveryLiveActor(FrontierRuntime app)
    {
        Prepare(app);
        var g=app.Game;
        var source=Get<Texture2D>(app,"worldProps");
        Check(source!=null,"Accepted authored canopy atlas required");
        var tree=new Decoration{kind="tree",x=12f,y=14f,variant=0};
        // Start far from the cover. All snapshots use the real production
        // DrawWorldDecoration -> DrawRootedPlant -> accepted WorldProps path.
        g.save.x=30;g.save.y=30;g.save.companion=-1;
        g.enemies.Clear();g.fallen.Clear();
        Action<float,string> assertOpacity=(alpha,context)=>{
            Clear();
            Call(app,"DrawWorldDecoration",tree);
            var draw=GUI.textureCalls.Find(t=>object.ReferenceEquals(t.texture,source));
            Check(draw!=null,"Live full-body tree vanished: "+context);
            Check(Math.Abs(draw.color.a-alpha)<.0001f,
                "Canopy transparency lost actual actor occlusion: "+context);
            Check(Math.Abs(draw.uv.x)<.0001f&&
                Math.Abs(draw.uv.width-.25f)<.0001f,
                "Occlusion cannot substitute original detailed tree pixels");
        };
        assertOpacity(1f,"empty");
        g.save.companion=0;g.companionPosition=new Point(12,12);
        assertOpacity(.4f,"companion");
        g.companionPosition=new Point(25,20);
        assertOpacity(1f,"companion outside canopy");
        g.save.companion=-1;
        var defeated=new Enemy(99881,0,12,12,10);
        g.fallen.Add(new FallenActor{actor=defeated,room=g.save.room,
            life=.4f,duration=.65f});
        assertOpacity(.4f,"fallen actor");
        g.fallen.Clear();
        // The NPC must be a REAL world object, not a synthetic source-only
        // unit test that skips the scene's actor/object dispatch.
        MapData map=g.Map;
        WorldObject[] savedObjects=map.objects;
        try
        {
            var added=new WorldObject[savedObjects.Length+1];
            Array.Copy(savedObjects,added,savedObjects.Length);
            var person=new WorldObject{id="cover_qa_resident",
                kind="guild",x=12,y=12};
            added[added.Length-1]=person;
            map.objects=added;
            assertOpacity(.4f,"world NPC");
            // This is the cached resident subset built exactly once per
            // DrawOrderedWorld frame. The same production canopy reader
            // must use this subset, not scan the complete Map on every tree.
            var nearby=Get<System.Collections.Generic.List<WorldObject>>(
                app,"coverResidentsInView");
            nearby.Clear();
            try
            {
                Set(app,"coverResidentsPrepared",true);
                assertOpacity(1f,"frame-local empty visible resident list");
                nearby.Add(person);
                assertOpacity(.4f,"frame-local accepted NPC");
                person.kind="barrel";
                assertOpacity(1f,"filtered nonactor cached entry");
            }
            finally
            {
                Set(app,"coverResidentsPrepared",false);
                nearby.Clear();
                person.kind="guild";
            }
            person.kind="barrel";
            assertOpacity(1f,"nonactor world prop");
        }
        finally {map.objects=savedObjects;}
        g.enemies.Add(new Enemy(99882,1,12,12,30));
        assertOpacity(.4f,"living enemy unchanged");
        g.enemies.Clear();
        g.save.x=12;g.save.y=12;
        assertOpacity(.4f,"hero unchanged");
        g.save.x=30;g.save.y=30;
        assertOpacity(1f,"restored full-opacity scenery");
        Console.WriteLine("PASS original-canopy visibility including per-frame NPC cache, companion, corpse, enemy, hero, and unchanged props");
    }
    private static void WholeWorldPropsFoliageHasOneStableRoot(FrontierRuntime app)
    {
        Prepare(app);
        var g=app.Game;
        Matrix4x4 canvas=Matrix4x4.TRS(new Vector3(51,29,0),
            Quaternion.identity,new Vector3(1.5f,1.5f,1));
        Color incoming=new Color(.33f,.61f,.84f,.8f);
        var original=Get<Texture2D>(app,"worldProps");
        Check(original!=null,"Accepted rich WorldProps.png must be available");
        int count=0;
        foreach(string kind in new[]{"tree","palm","snowpine"})
        foreach(bool reduced in new[]{false,true})
        {
            g.save.reducedMotion=reduced;g.save.effectsQuality=2;
            var plant=new Decoration {kind=kind,x=12.4f,y=14.2f};
            Rect area=new Rect(212,173,111,140);
            float side=Math.Min(area.width,area.height);
            Rect centered=new Rect(area.x+(area.width-side)*.5f,
                area.y+area.height-side,side,side);
            Vector3 foot=new Vector3(centered.x+side*.5f,
                centered.y+side*120f/128f,0f);
            var fixedScreen=canvas.MultiplyPoint3x4(foot);
            GUI.matrix=canvas;GUI.color=incoming;
            Set(app,"clock",7.1f);
            Clear();
            Call(app,"DrawRootedPlant",plant,area,
                new Color(1f,.92f,.84f,.7f));
            Check(GUI.textureCalls.Count==1,
                "Every animated tree/palm/pine must use one WHOLE author Sprite, never 32 sewn strips");
            var first=GUI.textureCalls[0];
            Check(object.ReferenceEquals(first.texture,original),
                "World tree/palm/snowpine must preserve the original detailed PNG");
            Near(first.matrix.MultiplyPoint3x4(foot),fixedScreen,
                "Plant root cannot slide through the terrain in a gust");
            float expectedU=kind=="palm"?.25f:kind=="snowpine"?.5f:0f;
            float expectedV=kind=="tree"?.5f:0f;
            Check(Math.Abs(first.uv.x-expectedU)<.0001f&&
                Math.Abs(first.uv.y-expectedV)<.0001f&&
                Math.Abs(first.uv.width-.25f)<.0001f&&
                Math.Abs(first.uv.height-.5f)<.0001f,
                "Foliage samples wrong original WorldProps art");
            Check(Math.Abs(first.color.a-.7f)<.0001f,
                "Foliage opacity changed during single-Sprite conversion");
            Same(GUI.matrix,canvas,"Whole original foliage must restore letterboxed GUI");
            Same(GUI.color,incoming,"Whole original foliage must restore GUI color");
            Set(app,"clock",9.8f);Clear();
            Call(app,"DrawRootedPlant",plant,area,
                new Color(1f,.92f,.84f,.7f));
            var second=GUI.textureCalls[0];
            Near(second.matrix.MultiplyPoint3x4(foot),fixedScreen,
                "Foliage must keep the same root across animation frames");
            bool changed=Math.Abs(first.matrix.m00-second.matrix.m00)+
                Math.Abs(first.matrix.m01-second.matrix.m01)>.00001f;
            Check(reduced?!changed:changed,
                "Wind must sway a WHOLE painting, and stop with reduced motion");
            count++;
        }
        Console.WriteLine("PASS "+count+
            " original full-body foliage variants: one draw, accurate pivots, reduced motion and preserved GUI state");
    }
    // Covers the PRODUCTION DrawWorldDecoration -> DrawRootedPlant path,
    // not just a hand-constructed direct wind pivot. Every accepted canopy
    // must plant its actual 120/128 sprite pixel at the ground shadow's
    // ScreenPoint while wind changes its rotation.
    private static void WorldCanopyPaintedRootsMatchActualTerrain(
        FrontierRuntime app)
    {
        Prepare(app);
        var game=app.Game;
        bool priorReduced=game.save.reducedMotion;
        int priorQuality=game.save.effectsQuality;
        Matrix4x4 originalMatrix=GUI.matrix;
        Color originalColor=GUI.color;
        var atlas=Get<Texture2D>(app,"worldProps");
        Check(atlas!=null,"Original WorldProps source missing for canopy");
        try
        {
            var canvas=Matrix4x4.TRS(new Vector3(23f,41f,0f),
                Quaternion.identity,new Vector3(1.5f,1.5f,1f));
            GUI.matrix=canvas;
            game.save.effectsQuality=2;
            int count=0;
            foreach(string kind in new[]{"tree","palm","snowpine"})
            foreach(bool reduced in new[]{false,true})
            {
                game.save.reducedMotion=reduced;
                var prop=new Decoration {kind=kind,x=12.45f,y=14.3f,
                    variant=0};
                var ground=(Point)Call(app,"ScreenPoint",
                    new Point(prop.x,prop.y));
                var planted=new Vector3(ground.x,ground.y,0f);
                var expected=canvas.MultiplyPoint3x4(planted);
                for(int frame=0;frame<2;frame++)
                {
                    Set(app,"clock",frame==0?3.5f:8.1f);
                    Clear();
                    Call(app,"DrawWorldDecoration",prop);
                    var body=GUI.textureCalls.Find(c=>
                        object.ReferenceEquals(c.texture,atlas));
                    Check(body!=null&&
                        Math.Abs(body.rect.width-body.rect.height)<.0001f,
                        "Production canopy lost its intact square source Sprite");
                    Check(Math.Abs(body.rect.x+body.rect.width*.5f-
                        planted.x)<.001f &&
                        Math.Abs(body.rect.y+body.rect.height*120f/128f-
                        planted.y)<.001f,
                        "Production canopy's illustrated trunk foot is not on its actual world anchor");
                    Near(body.matrix.MultiplyPoint3x4(planted),expected,
                        "Production canopy wind drifted the base off its shadow");
                    Same(GUI.matrix,canvas,
                        "Production canopy leaked world/camera transform");
                    count++;
                }
            }
            Console.WriteLine("PASS "+count+
                " production canopy frames: original illustrated tree/palm/pine feet planted at exact terrain collision roots through wind and reduced-motion");
        }
        finally
        {
            GUI.matrix=originalMatrix;
            GUI.color=originalColor;
            game.save.reducedMotion=priorReduced;
            game.save.effectsQuality=priorQuality;
        }
    }
    private static void GroundFoliageKeepsSourcePixelsAndRoot(
        FrontierRuntime app)
    {
        app.BeginVisualReview(0,1);
        var g=app.Game;
        var detailed=Get<Texture2D>(app,"detailSheet");
        Check(detailed!=null,"Original ground-detail atlas missing");
        int tileX=-1,tileY=-1,frame=-1,seed=0;
        for(int y=2;y<Math.Min(g.Map.rows.Length-2,36)&&tileX<0;y++)
        for(int x=2;x<Math.Min(g.Map.rows[y].Length-2,48);x++)
        {
            int candidate=(int)Call(app,"PreparedTileSeed",x,y);
            if(candidate%17!=0||(bool)Call(app,"GroundDetailOccluded",x,y))
                continue;
            int[] approved={0,1,3,4,10};
            int selected=approved[(candidate/17)%approved.Length];
            if(selected!=0&&selected!=1&&selected!=10)continue;
            tileX=x;tileY=y;frame=selected;seed=candidate;break;
        }
        Check(tileX>=0,"Could not find original ground foliage review sample");
        float size=frame==10?33f:23f;
        Point planted=new Point(tileX+.35f+seed%5*.07f,tileY+.65f);
        Point foot=(Point)Call(app,"ScreenPoint",planted);
        var rect=new Rect(foot.x-size*.5f,foot.y-size*120f/128f,size,size);
        Matrix4x4 original=GUI.matrix;
        Color oldColor=GUI.color;
        var canvas=Matrix4x4.TRS(new Vector3(31,27,0),
            Quaternion.identity,new Vector3(1.35f,1.35f,1f));
        try
        {
            GUI.matrix=canvas;
            GUI.color=new Color(.3f,.5f,.7f,.8f);
            g.save.effectsQuality=2;g.save.reducedMotion=false;
            Set(app,"clock",5.1f);
            Clear();Call(app,"DrawGroundDetail",
                tileX,tileY,'.',new Point(160,150));
            Check(GUI.textureCalls.Count==1,
                "Ground tuft must draw one full accepted source image");
            var first=GUI.textureCalls[0];
            bool mirrored=(seed&1)==1;
            float sourceU=(frame%4+(mirrored?1:0))/4f;
            float sourceWidth=mirrored?-.25f:.25f;
            Check(object.ReferenceEquals(first.texture,detailed)&&
                Math.Abs(first.rect.x-rect.x)<.001f&&
                Math.Abs(first.rect.y-rect.y)<.001f&&
                Math.Abs(first.rect.width-size)<.001f&&
                Math.Abs(first.uv.x-sourceU)<.0001f&&
                Math.Abs(first.uv.y-(1f-(frame/4+1)/4f))<.0001f&&
                Math.Abs(first.uv.width-sourceWidth)<.0001f&&
                Math.Abs(first.color.a-.8f)<.0001f,
                "Foliage switched from original painted Details sprite or resized");
            Vector3 root=canvas.MultiplyPoint3x4(new Vector3(
                foot.x,foot.y,0));
            Near(first.matrix.MultiplyPoint3x4(new Vector3(
                foot.x,foot.y,0)),root,
                "Waving grass uprooted its painted foot/soil attachment");
            Same(GUI.matrix,canvas,
                "Single-Sprite foliage leaked camera rotation");
            Set(app,"clock",8.4f);
            Clear();Call(app,"DrawGroundDetail",
                tileX,tileY,'.',new Point(160,150));
            Check(GUI.textureCalls.Count==1,
                "Foliage disappeared on later animation frame");
            var second=GUI.textureCalls[0];
            Near(second.matrix.MultiplyPoint3x4(new Vector3(
                foot.x,foot.y,0)),root,"Wind drift moved grass root");
            Check(Math.Abs(second.matrix.m01-first.matrix.m01)>.00001f,
                "Living undergrowth failed to animate authored whole Sprite");
            Check(Math.Abs(second.uv.x-first.uv.x)<.00001f&&
                Math.Abs(second.uv.y-first.uv.y)<.00001f,
                "Foliage wind must not synthesize or exchange atlas cells");
            g.save.reducedMotion=true;
            Clear();Call(app,"DrawGroundDetail",
                tileX,tileY,'.',new Point(160,150));
            Check(GUI.textureCalls.Count==1,
                "Reduced-motion foliage cannot disappear");
            Same(GUI.textureCalls[0].matrix,canvas,
                "Reduced-motion must disable all undergrowth swaying");
            g.save.reducedMotion=false;g.save.effectsQuality=0;
            Clear();Call(app,"DrawGroundDetail",
                tileX,tileY,'.',new Point(160,150));
            Same(GUI.textureCalls[0].matrix,canvas,
                "Lowest FX tier must avoid optional animated undergrowth");
            Check(GUI.textureCalls.Count==1,
                "Low-FX undergrowth must retain its accepted picture");
            Same(GUI.matrix,canvas,"Ground detail leaked GUI matrix state");
            Same(GUI.color,new Color(.3f,.5f,.7f,.8f),
                "Ground detail leaked tint to actor and gameplay HUD");
            Console.WriteLine("PASS rooted one-Sprite ground art; animated flora vs low-FX/reduced-motion, source UV/foot/GPU bypass");
        }
        finally
        {
            GUI.matrix=original;GUI.color=oldColor;
            app.EndVisualReview();Clear();
        }
    }
    private static void StaticSceneryPreservesOriginalSpriteWithoutGpuActorCapture(
        FrontierRuntime app)
    {
        app.BeginVisualReview(0,1);
        var field=Get<Texture2D>(app,"fieldProps");
        var detail=Get<Texture2D>(app,"detailSheet");
        Check(field!=null&&detail!=null,
            "Accepted World/Field Detail PNGs missing in live production");
        Matrix4x4 old=GUI.matrix;
        Color oldColor=GUI.color;
        try
        {
            var canvas=Matrix4x4.TRS(new Vector3(27,29,0),
                Quaternion.identity,new Vector3(1.2f,1.2f,1));
            GUI.matrix=canvas;
            GUI.color=new Color(.4f,.5f,.6f,.8f);
            app.Game.save.reducedMotion=false;
            app.Game.save.effectsQuality=2;
            string[] names={"well","skiff","tent","post","fire",
                "root","herb","bed","crate","barrel","bridge"};
            int[] indices={0,10,9,5,13,11,6,13,6,7,12};
            for(int t=0;t<names.Length;t++)
            {
                Clear();
                bool rendered=(bool)Call(app,"DrawFieldProp",
                    names[t],0,170f,180f,76f,88f,null);
                Check(rendered&&GUI.textureCalls.Count==1,
                    "World object drew absent/multiple bodies: "+names[t]);
                var sprite=GUI.textureCalls[0];
                Texture2D expected=t<7?field:detail;
                int cell=indices[t];
                Check(object.ReferenceEquals(sprite.texture,expected)&&
                    Math.Abs(sprite.uv.x-(cell%4)/4f)<.00001f&&
                    Math.Abs(sprite.uv.y-(1f-(cell/4+1)/4f))<.00001f&&
                    Math.Abs(sprite.uv.width-.25f)<.00001f&&
                    Math.Abs(sprite.rect.width-88f)<.00001f&&
                    Math.Abs(sprite.rect.x-164f)<.00001f&&
                    Math.Abs(sprite.rect.y-(260f-88f*120f/128f))<.00001f,
                    "Direct field prop changed source cell or boot pivot: "+names[t]);
                Same(GUI.matrix,canvas,"Static prop leaked character rotation");
                Same(GUI.color,new Color(.4f,.5f,.6f,.8f),
                    "Static prop leaked tint into live scene");
            }
            // Each planted garden has NINE repeated herbs. A former native
            // actor capture path allocated source 128px targets for those
            // small scenic cells even though their frame never animates.
            Set(app,"clock",3.1f);
            Clear();Call(app,"DrawFieldProp","garden",2,
                170f,180f,76f,88f,null);
            int grown=0;
            int animatedHerbs=0;
            var grownCells=new System.Collections.Generic.List<
                System.Tuple<float,float,float,float>>();
            foreach(var call in GUI.textureCalls)
                if(object.ReferenceEquals(call.texture,field)&&
                    Math.Abs(call.uv.x-.5f)<.00001f&&
                    Math.Abs(call.uv.y-.5f)<.00001f)
                {
                    Check(Math.Abs(call.rect.width-27f)<.00001f,
                        "Mature garden herb lost its official full Sprite size");
                    var anchor=new Vector3(call.rect.x+call.rect.width*.5f,
                        call.rect.y+call.rect.height*120f/128f,0f);
                    Near(call.matrix.MultiplyPoint3x4(anchor),
                        canvas.MultiplyPoint3x4(anchor),
                        "Mature animated garden plant lost its soil pivot");
                    if(Math.Abs(call.matrix.m01-canvas.m01)>.00001f)
                        animatedHerbs++;
                    grownCells.Add(System.Tuple.Create(call.rect.x,
                        call.rect.y,call.matrix.m00,call.matrix.m01));
                    grown++;
                }
            Check(grown==9&&animatedHerbs>0,
                "Mature garden must animate 9 original grounded full-body herbs");
            Set(app,"clock",8.7f);
            Clear();Call(app,"DrawFieldProp","garden",2,
                170f,180f,76f,88f,null);
            int advancing=0;
            foreach(var call in GUI.textureCalls)
                if(object.ReferenceEquals(call.texture,field)&&
                    Math.Abs(call.uv.x-.5f)<.00001f&&
                    Math.Abs(call.uv.y-.5f)<.00001f)
                {
                    Check(advancing<grownCells.Count,
                        "Wind spawned extra unapproved garden Sprites");
                    var oldPlant=grownCells[advancing];
                    Check(Math.Abs(call.rect.x-oldPlant.Item1)<.00001f&&
                        Math.Abs(call.rect.y-oldPlant.Item2)<.00001f&&
                        Math.Abs(call.uv.width-.25f)<.00001f,
                        "Garden wind shifted source/soil or changed pixel art");
                    var anchor=new Vector3(call.rect.x+call.rect.width*.5f,
                        call.rect.y+call.rect.height*120f/128f,0f);
                    Near(call.matrix.MultiplyPoint3x4(anchor),
                        canvas.MultiplyPoint3x4(anchor),
                        "Garden breeze moved the actual planted root");
                    advancing++;
                }
            Check(advancing==9,
                "Garden animation lost one of its nine rooted cells");
            app.Game.save.effectsQuality=0;
            app.Game.save.reducedMotion=true;
            Clear();Call(app,"DrawFieldProp","garden",1,
                170f,180f,76f,88f,null);
            int young=0;
            foreach(var call in GUI.textureCalls)
                if(object.ReferenceEquals(call.texture,field)&&
                    Math.Abs(call.uv.x-.5f)<.00001f&&
                    Math.Abs(call.uv.y-.5f)<.00001f)
                {
                    Check(Math.Abs(call.rect.width-18f)<.00001f,
                        "Young garden source art must retain original planted size");
                    Same(call.matrix,canvas,
                        "Reduced-motion low-FX garden must stop all plant sway");
                    young++;
                }
            Check(young==9,
                "Reduced-motion low-FX garden lost authored plants");
            Console.WriteLine("PASS 11 original non-actor field sprites and 9 softly swaying rooted garden herbs: source pixels, mature/youth stages, accessibility and GUI isolation");
        }
        finally
        {
            GUI.matrix=old;GUI.color=oldColor;
            app.EndVisualReview();Clear();
        }
    }
    private static void WorldLightsRemainAnchoredToDetailedProps(FrontierRuntime app)
    {
        Prepare(app);
        var g=app.Game;
        g.save.effectsQuality=2;
        GUI.matrix=Matrix4x4.TRS(new Vector3(33,27,0),
            Quaternion.identity,new Vector3(1.25f,1.25f,1));
        Matrix4x4 canvas=GUI.matrix;
        GUI.color=new Color(.3f,.5f,.7f,.9f);
        Color incoming=GUI.color;
        int verified=0;
        foreach(string kind in new[]{"lamp","lighthouse","crystal","root",
            "fire","brazier","outerrelay","ore","mirror"})
        {
            g.save.reducedMotion=true;
            var root=new Point(12.25f,14.5f);
            Clear();Set(app,"clock",7.2f);
            Call(app,"DrawWorldEmitter",root,kind,1f,229);
            Check(GUI.textureCalls.Count==2,
                "High-quality world emitter must have one halo and one tight authored glow");
            var outer=GUI.textureCalls[0];var core=GUI.textureCalls[1];
            Check(outer.texture!=null&&object.ReferenceEquals(
                outer.texture,core.texture),
                "Original illustrated glow remains the same in both layers");
            Check(core.rect.width<outer.rect.width*.5f&&
                core.rect.height<outer.rect.height*.5f,
                "World lamps require a bounded core, not a massive flat overlay");
            if(kind=="ore"||kind=="mirror")
            {
                var look=typeof(FrontierRuntime).GetProperty(
                    "CurrentWorldVisualLook",Private).GetValue(app,null);
                var radius=look.GetType().GetField("relicHaloRadius",
                    BindingFlags.Instance|BindingFlags.Public);
                var intensity=look.GetType().GetField("relicHaloAlpha",
                    BindingFlags.Instance|BindingFlags.Public);
                Check(radius!=null&&intensity!=null&&
                    Math.Abs(outer.rect.width-(float)radius.GetValue(look))<.001f,
                    "Reflective relic halo must be tuned from actual Unity WorldLook resource");
                float original=(float)intensity.GetValue(look);
                try
                {
                    intensity.SetValue(look,original*.5f);
                    Clear();Call(app,"DrawWorldEmitter",root,kind,1f,229);
                    Check(GUI.textureCalls.Count==2&&
                        Math.Abs(GUI.textureCalls[0].color.a-
                            outer.color.a*.5f)<.0001f,
                        "Relic light intensity did not control actual world illumination");
                }
                finally{intensity.SetValue(look,original);}
                Clear();Call(app,"DrawWorldEmitter",root,kind,1f,229);
                outer=GUI.textureCalls[0];
                core=GUI.textureCalls[1];
            }
            float offset=kind=="lighthouse"?1.95f:kind=="lamp"?1.12f:
                kind=="crystal"?.65f:kind=="ore"?.66f:
                kind=="mirror"?.70f:kind=="root"?.45f:
                kind=="fire"||kind=="brazier"?.28f:.52f;
            var at=(Point)Call(app,"ScreenPoint",root-new Point(0,offset));
            Vector3 expected=canvas.MultiplyPoint3x4(
                new Vector3(at.x,at.y,0));
            Near(Center(outer),expected,
                "Lighthouse/lamps/crystals need stable source pivots, not a 1-tile generic halo");
            float stillAlpha=outer.color.a;
            Clear();Set(app,"clock",39.1f);
            Call(app,"DrawWorldEmitter",root,kind,1f,229);
            Check(Math.Abs(GUI.textureCalls[0].color.a-stillAlpha)<.00001f,
                "Reduced-motion visual setting must preserve static source illumination");
            g.save.reducedMotion=false;Clear();
            Set(app,"clock",7.2f);
            Call(app,"DrawWorldEmitter",root,kind,1f,229);
            float bright1=GUI.textureCalls[0].color.a;
            Clear();Set(app,"clock",39.1f);
            Call(app,"DrawWorldEmitter",root,kind,1f,229);
            float bright2=GUI.textureCalls[0].color.a;
            Check(Math.Abs(bright1-bright2)>.00001f&&
                Math.Abs(bright1-bright2)<.04f,
                "Environmental flicker should be deterministic, gentle and continuous");
            g.save.effectsQuality=1;Clear();
            Call(app,"DrawWorldEmitter",root,kind,.4f,229);
            Check(GUI.textureCalls.Count==1&&
                GUI.textureCalls[0].color.a < bright2,
                "Medium-quality lighting preserves source halo with reduced cost");
            g.save.effectsQuality=0;Clear();
            Call(app,"DrawWorldEmitter",root,kind,1f,229);
            Check(GUI.textureCalls.Count==0,
                "Low effects quality must avoid optional emitter draws");
            g.save.effectsQuality=2;
            Same(GUI.matrix,canvas,
                "World emitter must not leak its viewport transform");
            Same(GUI.color,incoming,
                "World emitter must not leak tint into HUD or next actor");
            verified++;
        }
        Console.WriteLine("PASS "+verified+
            " production lamplight/crystal/relay sources: attached illumination, stable motion and quality tiers");
    }
    private static void StoneRoadBordersPreserveRichPublishedPixels(
        FrontierRuntime app)
    {
        app.BeginVisualReview(0,1);
        var game=app.Game;
        var map=game.Map;
        var originalColor=GUI.color;
        var originalMatrix=GUI.matrix;
        int oldQuality=game.save.effectsQuality;
        bool oldReduced=game.save.reducedMotion;
        int x=18,y=14;
        string top=map.rows[y-1],center=map.rows[y],
            bottom=map.rows[y+1];
        var stone=Get<Texture2D>(app,"terrainVariants");
        Check(stone!=null&&stone.width==192&&stone.height==192,
            "Accepted cobblestone painting missing");
        Matrix4x4 canvas=Matrix4x4.TRS(new Vector3(16,28,0),
            Quaternion.identity,new Vector3(1.2f,1.2f,1));
        Color originalTint=new Color(.4f,.6f,.8f,.9f);
        try
        {
            game.save.effectsQuality=0;
            game.save.reducedMotion=true;
            GUI.matrix=canvas;GUI.color=originalTint;
            for(int edge=0;edge<4;edge++)
            {
                char[] up=top.ToCharArray();
                char[] mid=center.ToCharArray();
                char[] down=bottom.ToCharArray();
                up[x]=mid[x-1]=mid[x]=mid[x+1]=down[x]='=';
                if(edge==0)up[x]='.';
                else if(edge==1)mid[x+1]='.';
                else if(edge==2)down[x]='.';
                else mid[x-1]='.';
                map.rows[y-1]=new string(up);
                map.rows[y]=new string(mid);
                map.rows[y+1]=new string(down);
                Clear();Call(app,"DrawSurface",x,y,'=',new Point(210,170));
                Check(GUI.textureCalls.Count==2,
                    "Official road draw must include exactly one original edge sample");
                var fill=GUI.textureCalls[0];
                var border=GUI.textureCalls[1];
                bool horizontal=edge==0||edge==2;
                Check(object.ReferenceEquals(fill.texture,stone)&&
                    object.ReferenceEquals(border.texture,stone)&&
                    Math.Abs(border.uv.width-
                        (horizontal?34f:3f)/192f)<.00001f&&
                    Math.Abs(border.uv.height-
                        (horizontal?3f:32f)/192f)<.00001f&&
                    border.uv.y>=48f/192f&&
                    border.uv.y+border.uv.height<=96f/192f,
                    "Road edge reverted to flat primitive or unrelated texture");
                Check(border.rect.width==(horizontal?34f:3f)&&
                    border.rect.height==(horizontal?3f:34f)&&
                    border.color.a>.5f&&border.color.a<.6f,
                    "Road trim should follow actual detailed stone without a thick bar");
                Same(GUI.matrix,canvas,
                    "Material trim must leave letterboxed viewport unchanged");
                Same(GUI.color,originalTint,
                    "Stone material tint leaked into NPC or HUD drawing");
                Clear();Call(app,"DrawSurface",x,y,'.',new Point(210,170));
                Check(GUI.textureCalls.Count==1,
                    "Ordinary grass cannot receive a cobblestone border");
            }
            char[] innerTop=top.ToCharArray();
            char[] inner=center.ToCharArray();
            char[] innerBottom=bottom.ToCharArray();
            innerTop[x]=inner[x-1]=inner[x]=inner[x+1]=
                innerBottom[x]='=';
            map.rows[y-1]=new string(innerTop);
            map.rows[y]=new string(inner);
            map.rows[y+1]=new string(innerBottom);
            Clear();Call(app,"DrawSurface",x,y,'=',new Point(210,170));
            Check(GUI.textureCalls.Count==1,
                "Road interiors should not acquire seams against other road tiles");
        }
        finally
        {
            map.rows[y-1]=top;map.rows[y]=center;
            map.rows[y+1]=bottom;
            game.save.effectsQuality=oldQuality;
            game.save.reducedMotion=oldReduced;
            GUI.matrix=originalMatrix;
            GUI.color=originalColor;
            Clear();
        }
        Console.WriteLine(
            "PASS original 192px stone road samples in all four directions and low-FX/reduced-motion; no flat edge rectangles");
    }

    private static void ShorelineEdgesUsePublishedWaterPixels(FrontierRuntime app)
    {
        Prepare(app);
        var g=app.Game;
        Texture2D approved=Get<Texture2D>(app,"terrainVariants");
        Check(approved!=null&&approved.width==192&&approved.height==192,
            "Published original water source required for all coastline seams");
        int x=18,y=14;
        var map=g.Map;
        string[] saved={
            map.rows[y-1],map.rows[y],map.rows[y+1]
        };
        int originalQuality=g.save.effectsQuality;
        bool originalReduced=g.save.reducedMotion;
        Matrix4x4 originalMatrix=GUI.matrix;
        Color originalColor=GUI.color;
        Matrix4x4 viewport=Matrix4x4.TRS(new Vector3(43,27,0),
            Quaternion.identity,new Vector3(1.2f,1.2f,1));
        Color ambient=new Color(.6f,.5f,.4f,.7f);
        try
        {
            GUI.matrix=viewport;GUI.color=ambient;
            g.save.reducedMotion=true;g.save.effectsQuality=0;
            var placed=new Point(300,225);
            for(int edge=0;edge<4;edge++)
            {
                char[] up=saved[0].ToCharArray();
                char[] mid=saved[1].ToCharArray();
                char[] down=saved[2].ToCharArray();
                up[x]=mid[x-1]=mid[x]=mid[x+1]=down[x]='~';
                if(edge==0)up[x]='.';
                else if(edge==1)mid[x+1]='.';
                else if(edge==2)down[x]='.';
                else mid[x-1]='.';
                map.rows[y-1]=new string(up);
                map.rows[y]=new string(mid);
                map.rows[y+1]=new string(down);
                Clear();Call(app,"DrawTerrainEdge",x,y,'~',placed);
                Check(GUI.textureCalls.Count==1,
                    "Only one original water-material border per exposed side");
                var mark=GUI.textureCalls[0];
                bool horizontal=edge==0||edge==2;
                Check(object.ReferenceEquals(mark.texture,approved)&&
                    mark.color.a>.4f&&mark.color.a<.6f&&
                    Math.Abs(mark.uv.height-
                        (horizontal?4f:30f)/192f)<.00001f&&
                    Math.Abs(mark.uv.width-
                        (horizontal?34f:4f)/192f)<.00001f,
                    "Coastline must preserve approved source material pixels");
                Check(mark.rect.width==(horizontal?TileMinusTwo():4f)&&
                    mark.rect.height==(horizontal?4f:TileMinusTwo()),
                    "Published water pixels must hug the source tile border");
                Check(!GUI.textureCalls.Exists(t=>
                    object.ReferenceEquals(t.texture,Texture2D.whiteTexture)),
                    "Coastline must not regress to flat 4px dark/green bars");
                Same(GUI.matrix,viewport,
                    "Water material edge leaked its viewport matrix");
                Same(GUI.color,ambient,
                    "Water material edge leaked tint into following actors");
                Clear();Call(app,"DrawTerrainEdge",x,y,'=',placed);
                Check(GUI.textureCalls.Count==0,
                    "Dry paths cannot acquire waterline material");
            }
            // A fully surrounded water cell must remain unmarked; its
            // original tile texture should not gain a fake raised border.
            char[] top=saved[0].ToCharArray();
            char[] center=saved[1].ToCharArray();
            char[] bottom=saved[2].ToCharArray();
            top[x]=center[x-1]=center[x]=center[x+1]=bottom[x]='~';
            map.rows[y-1]=new string(top);
            map.rows[y]=new string(center);
            map.rows[y+1]=new string(bottom);
            Clear();Call(app,"DrawTerrainEdge",x,y,'~',placed);
            Check(GUI.textureCalls.Count==0,
                "Deep water must have no artificial coastline");
        }
        finally
        {
            map.rows[y-1]=saved[0];
            map.rows[y]=saved[1];
            map.rows[y+1]=saved[2];
            g.save.effectsQuality=originalQuality;
            g.save.reducedMotion=originalReduced;
            GUI.matrix=originalMatrix;
            GUI.color=originalColor;
            Clear();
        }
        Console.WriteLine(
            "PASS 4 static waterline orientations use published source pixels, stay visible in reduced motion/low FX, and never draw inland");
    }
    private static float TileMinusTwo() { return 34f; }

    private static void ShorelineUsesOriginalWaterArtAndHonorsSettings(FrontierRuntime app)
    {
        app.BeginVisualReview(0,5);
        var game=app.Game;
        var source=Get<Texture2D>(app,"terrainVariants");
        Check(source!=null&&source.width==192&&source.height==192,
            "Production shoreline needs original high-detail water atlas");
        var map=game.Map;
        const int y=14;
        int x=-1;
        for(int candidate=14;candidate<26;candidate++)
            if(((int)Call(app,"PreparedTileSeed",candidate,y)&1)==0)
            {x=candidate;break;}
        Check(x>=0,"Stable shoreline seed not found");
        string[] original=new string[3];
        for(int k=-1;k<=1;k++)original[k+1]=map.rows[y+k];
        var savedQuality=game.save.effectsQuality;
        var savedReduced=game.save.reducedMotion;
        Matrix4x4 canvas=Matrix4x4.TRS(new Vector3(29,43,0),
            Quaternion.identity,new Vector3(1.25f,1.25f,1));
        Color incoming=new Color(.31f,.44f,.57f,.81f);
        try
        {
            game.save.reducedMotion=false;
            game.save.effectsQuality=2;
            GUI.matrix=canvas;GUI.color=incoming;
            Point position=new Point(230,180);
            // Patch only the transient review map and restore each row.
            // Test all four water/land edges using the REAL renderer.
            for(int direction=0;direction<4;direction++)
            {
                char[] top=original[0].ToCharArray();
                char[] middle=original[1].ToCharArray();
                char[] bottom=original[2].ToCharArray();
                top[x]='~';middle[x-1]='~';middle[x]='~';
                middle[x+1]='~';bottom[x]='~';
                if(direction==0)top[x]='.';
                if(direction==1)middle[x+1]='.';
                if(direction==2)bottom[x]='.';
                if(direction==3)middle[x-1]='.';
                map.rows[y-1]=new string(top);
                map.rows[y]=new string(middle);
                map.rows[y+1]=new string(bottom);
                Set(app,"clock",4.2f);
                Clear();
                Call(app,"DrawAuthoredShorelineWash",x,y,'~',position);
                Check(GUI.textureCalls.Count==1,
                    "Water-land edge must draw one subtle existing-art crest");
                var first=GUI.textureCalls[0];
                Check(object.ReferenceEquals(first.texture,source)&&
                    Math.Abs(first.uv.width-24f/192f)<.00001f&&
                    Math.Abs(first.uv.height-7f/192f)<.00001f&&
                    first.color.a>0&&first.color.a<.13f,
                    "Shorebreak cannot substitute coarse pixels or opaque bars");
                bool vertical=direction==1||direction==3;
                Check(vertical?Math.Abs(first.matrix.m01)>.05f:
                    Math.Abs(first.matrix.m01)<.0001f,
                    "Side shorebreak must rotate a whole original pixel sample");
                Same(GUI.matrix,canvas,"Shoreline must restore scene canvas");
                Same(GUI.color,incoming,"Shoreline cannot tint later sprites");
                Set(app,"clock",6.9f);Clear();
                Call(app,"DrawAuthoredShorelineWash",x,y,'~',position);
                Check(GUI.textureCalls.Count==1&&
                    Math.Abs(GUI.textureCalls[0].uv.x-first.uv.x)<.00001f&&
                    Math.Abs(GUI.textureCalls[0].rect.x-first.rect.x)<.0001f&&
                    Math.Abs(GUI.textureCalls[0].color.a-first.color.a)>.00001f,
                    "Water crest must animate light only, never terrain root/UV");
                game.save.reducedMotion=true;Clear();
                Call(app,"DrawAuthoredShorelineWash",x,y,'~',position);
                Check(GUI.textureCalls.Count==0,
                    "Reduced motion must disable moving shoreline");
                game.save.reducedMotion=false;
                game.save.effectsQuality=0;Clear();
                Call(app,"DrawAuthoredShorelineWash",x,y,'~',position);
                Check(GUI.textureCalls.Count==0,
                    "Low FX must avoid additional shoreline draw calls");
                game.save.effectsQuality=2;
                Clear();Call(app,"DrawAuthoredShorelineWash",x,y,'=',position);
                Check(GUI.textureCalls.Count==0,
                    "Dry road cannot receive coastline movement");
            }
            // A river bend exposes north AND east, not only the
            // first branch of a four-way if/else. Both entire authored wave
            // samples must be anchored independently, without passing a
            // previous edge's matrix rotation into the following sample.
            char[] cornerTop=original[0].ToCharArray();
            char[] cornerMiddle=original[1].ToCharArray();
            char[] cornerBottom=original[2].ToCharArray();
            cornerTop[x]='.';
            cornerMiddle[x-1]=cornerMiddle[x]=cornerBottom[x]='~';
            cornerMiddle[x+1]='.';
            map.rows[y-1]=new string(cornerTop);
            map.rows[y]=new string(cornerMiddle);
            map.rows[y+1]=new string(cornerBottom);
            Clear();Call(app,"DrawAuthoredShorelineWash",x,y,'~',
                new Point(230,180));
            Check(GUI.textureCalls.Count==2,
                "River corner must animate BOTH exposed banks");
            var north=GUI.textureCalls[0];
            var east=GUI.textureCalls[1];
            Check(object.ReferenceEquals(north.texture,source)&&
                object.ReferenceEquals(east.texture,source)&&
                Math.Abs(north.matrix.m01)<.00001f&&
                Math.Abs(east.matrix.m01)>.05f&&
                Math.Abs(north.uv.x-east.uv.x)<.00001f&&
                Math.Abs(north.color.a-east.color.a)<.00001f,
                "Corner wave samples must share approved art, not stale rotation");
            Same(GUI.matrix,canvas,
                "Two-sided shoreline inherited its previous rotation");
            Same(GUI.color,incoming,
                "Corner wave paint leaked into later sprites");
            cornerMiddle[x-1]='.';
            cornerBottom[x]='.';
            map.rows[y]=new string(cornerMiddle);
            map.rows[y+1]=new string(cornerBottom);
            Clear();Call(app,"DrawAuthoredShorelineWash",x,y,'~',
                new Point(230,180));
            Check(GUI.textureCalls.Count==4,
                "Water island with four exposed banks must draw four valid crests");
            game.save.reducedMotion=true;
            Clear();Call(app,"DrawAuthoredShorelineWash",x,y,'~',
                new Point(230,180));
            Check(GUI.textureCalls.Count==0,
                "Multiple-bank wave animation must obey reduced-motion preference");
            game.save.reducedMotion=false;
            // Deep water has no adjacent shore, so cannot get fake foam.
            map.rows[y-1]=original[0].Substring(0,x)+"~"+
                original[0].Substring(x+1);
            char[] waterRow=original[1].ToCharArray();
            waterRow[x-1]=waterRow[x]=waterRow[x+1]='~';
            map.rows[y]=new string(waterRow);
            map.rows[y+1]=original[2].Substring(0,x)+"~"+
                original[2].Substring(x+1);
            Clear();Call(app,"DrawAuthoredShorelineWash",x,y,'~',new Point(230,180));
            Check(GUI.textureCalls.Count==0,
                "Ocean center cannot gain a fake island-like foam edge");
        }
        finally
        {
            for(int k=-1;k<=1;k++)map.rows[y+k]=original[k+1];
            game.save.effectsQuality=savedQuality;
            game.save.reducedMotion=savedReduced;
            GUI.matrix=Matrix4x4.identity;
            GUI.color=Color.white;
        }
        Console.WriteLine("PASS authored original-water shoreline: four orientations, stable roots, quality/reduced-motion and no inland foam");
    }
    private static void ProjectileTrailsGrowWithoutFramePopping(
        FrontierRuntime app)
    {
        Prepare(app);
        var g=app.Game;
        var originalQuality=g.save.effectsQuality;
        var originalReduced=g.save.reducedMotion;
        Matrix4x4 originalMatrix=GUI.matrix;
        Color originalColor=GUI.color;
        try
        {
            g.save.effectsQuality=2;
            g.save.reducedMotion=false;
            GUI.matrix=Matrix4x4.TRS(new Vector3(33,21,0),
                Quaternion.identity,new Vector3(1.3f,1.3f,1));
            GUI.color=new Color(.34f,.51f,.71f,.82f);
            Projectile flight=new Projectile{
                position=new Point(13,14),
                velocity=new Point(10,0),
                age=.019f,
                friendly=true,
                visual="arrow"
            };
            Clear();Call(app,"DrawProjectileVisual",flight);
            Check(GUI.textureCalls.Count==2,
                "Newly born arrow trail must fade in before the second cell");
            var early=GUI.textureCalls[0];
            Check(early.texture.name=="resource:Frontier/Art/Effects"&&
                early.color.a>0f&&early.color.a<.04f,
                "First trail suddenly appeared at full opacity");
            flight.age=.03f;
            Clear();Call(app,"DrawProjectileVisual",flight);
            Check(GUI.textureCalls.Count==2&&
                GUI.textureCalls[0].color.a>early.color.a&&
                GUI.textureCalls[0].color.a<.42f,
                "Original tail must grow smoothly on real travel distance");
            flight.age=.09f;
            Clear();Call(app,"DrawProjectileVisual",flight);
            Check(GUI.textureCalls.Count==4,
                "Fully established authored arrow trail lost its three poses");
            var head=GUI.textureCalls[3];
            var expectedHead=(Point)Call(app,"ScreenPoint",flight.position);
            Vector3 expected=GUI.matrix.MultiplyPoint3x4(new Vector3(
                expectedHead.x,expectedHead.y-flight.height,0));
            Near(Center(head),expected,
                "Fading arrow ghosts shifted real projectile impact coordinates");
            for(int i=0;i<3;i++)
                Check(GUI.textureCalls[i].color.a>0f&&
                    GUI.textureCalls[i].color.a<=.42f,
                    "Established trail alpha outside source-approved bounds");
            // Sword guard reverses the SAME simulated projectile: its
            // cumulative age is still old, but its outbound painted tail
            // must restart at the actual guard contact timestamp.
            flight.age=.9f;
            flight.trailResetAt=.9f;
            flight.velocity=new Point(-10f,0f);
            Clear();Call(app,"DrawProjectileVisual",flight);
            Check(GUI.textureCalls.Count==1&&
                object.ReferenceEquals(GUI.textureCalls[0].texture,
                    Get<Texture2D>(app,"effectSheet")),
                "Parried arrow instantly acquired a full inbound-age ghost trail");
            flight.age=.919f;
            Clear();Call(app,"DrawProjectileVisual",flight);
            Check(GUI.textureCalls.Count==2&&
                GUI.textureCalls[0].color.a>0f&&
                GUI.textureCalls[0].color.a<.04f,
                "Outbound original arrow trail must fade in from its new travel origin");
            flight.age=.99f;
            Clear();Call(app,"DrawProjectileVisual",flight);
            Check(GUI.textureCalls.Count==4,
                "Reflected shot failed to regrow the original three-cell authored tail");
            g.save.effectsQuality=1;
            Clear();Call(app,"DrawProjectileVisual",flight);
            Check(GUI.textureCalls.Count==3,
                "Medium quality must preserve arrow head and two source tails");
            g.save.reducedMotion=true;
            Clear();Call(app,"DrawProjectileVisual",flight);
            Check(GUI.textureCalls.Count==1&&
                GUI.textureCalls[0].texture.name==
                    "resource:Frontier/Art/Effects",
                "Reduced-motion must keep authentic arrow head, without ghosts");
            g.save.reducedMotion=false;
            g.save.effectsQuality=0;
            Clear();Call(app,"DrawProjectileVisual",flight);
            Check(GUI.textureCalls.Count==1,
                "Low quality should retain actual flight but omit trail cost");
            g.save.effectsQuality=2;
            flight.velocity=new Point(0,0);
            Clear();Call(app,"DrawProjectileVisual",flight);
            Check(GUI.textureCalls.Count==1,
                "Stationary projectile must not create offset ghost sprites");
            Same(GUI.matrix,Matrix4x4.TRS(new Vector3(33,21,0),
                Quaternion.identity,new Vector3(1.3f,1.3f,1)),
                "Projectile trail rotated unrelated canvas objects");
            Same(GUI.color,new Color(.34f,.51f,.71f,.82f),
                "Projectile fades leaked tint to HUD");
        }
        finally
        {
            g.save.effectsQuality=originalQuality;
            g.save.reducedMotion=originalReduced;
            GUI.matrix=originalMatrix;
            GUI.color=originalColor;
            Clear();
        }
        Console.WriteLine(
            "PASS original 128px flight trail gradual emergence, true projectile anchor, 3 FX tiers and reduced motion");
    }
    private static void AttackWarningsRemainReadableThroughWorldCover(
        FrontierRuntime app)
    {
        app.BeginVisualReview(0,1);
        var game=app.Game;
        game.enemies.Clear();
        game.effects.Clear();
        game.save.effectsQuality=0;
        game.save.reducedMotion=true;
        Set(app,"camera",game.Position);
        Enemy boss=new Enemy(93241,2,game.Position.x+2f,
            game.Position.y,480,true)
        {
            state=1,timer=.42f,windupDuration=1.1f,
            target=game.Position
        };
        game.enemies.Add(boss);
        var source=Get<Texture2D>(app,"effectSheet");
        Check(source!=null,"Original combat warning Sprite atlas missing");
        try
        {
            Clear();Call(app,"DrawWorld");
            int heroIndex=-1,warningIndex=-1;
            for(int i=0;i<GUI.textureCalls.Count;i++)
            {
                var c=GUI.textureCalls[i];
                if(heroIndex<0&&c.texture!=null&&c.texture.name!=null&&
                    c.texture.name.IndexOf(
                        "Frontier/HeroAnimator/Canonical/Hero_Vanguard",
                        StringComparison.Ordinal)>=0)
                    heroIndex=i;
                if(object.ReferenceEquals(c.texture,source)&&
                    Math.Abs(c.uv.x-.5f)<.00001f&&
                    Math.Abs(c.uv.y)<.00001f&&
                    Math.Abs(c.uv.width-.25f)<.00001f&&
                    c.rect.width>40f)
                    warningIndex=i;
            }
            Check(heroIndex>=0&&warningIndex>heroIndex,
                "Boss hit-radius signal disappeared behind foreground hero/cover");
            // Rendering is target-aware. Enemy bodies can be more than a
            // full screen outside the viewport while their LOCKED strikes
            // land directly on the player; warnings must remain visible.
            boss.x=game.Position.x+55f;
            Clear();Call(app,"DrawWorld");
            heroIndex=-1;warningIndex=-1;
            bool drawnOffscreenBody=false;
            for(int i=0;i<GUI.textureCalls.Count;i++)
            {
                var c=GUI.textureCalls[i];
                if(heroIndex<0&&c.texture!=null&&c.texture.name!=null&&
                    c.texture.name.IndexOf(
                        "Frontier/HeroAnimator/Canonical/Hero_Vanguard",
                        StringComparison.Ordinal)>=0)
                    heroIndex=i;
                if(c.texture!=null&&c.texture.name!=null&&
                    c.texture.name.IndexOf("Frontier/Art/BriarBeast",
                        StringComparison.Ordinal)>=0)
                    drawnOffscreenBody=true;
                if(object.ReferenceEquals(c.texture,source)&&
                    Math.Abs(c.uv.x-.5f)<.00001f&&
                    Math.Abs(c.uv.y)<.00001f&&
                    Math.Abs(c.uv.width-.25f)<.00001f&&
                    c.rect.width>40f)
                    warningIndex=i;
            }
            Check(!drawnOffscreenBody&&warningIndex>heroIndex&&heroIndex>=0,
                "Offscreen Boss body culled its still-visible target warning");
            // Original world scene keeps stationary danger readable even
            // when reduced motion and the low-FX setting are enabled.
            Console.WriteLine(
                "PASS production DrawWorld danger layering and offscreen Boss target warning: exact source art, native hero readability, reduced-motion/FX");
        }
        finally
        {
            game.enemies.Clear();
            app.EndVisualReview();Clear();
        }
    }
    private static void LiveHazardOutlinesStayAboveActorsAndClipAtView(
        FrontierRuntime app)
    {
        app.BeginVisualReview(0,1);
        var game=app.Game;
        game.enemies.Clear();game.effects.Clear();game.hazards.Clear();
        game.save.reducedMotion=true;game.save.effectsQuality=0;
        Set(app,"camera",game.Position);
        var hazard=new Hazard{
            position=game.Position,radius=1.2f,timer=1.3f,
            kind="root"
        };
        game.hazards.Add(hazard);
        var tint=PixelArt.C("#82d9a3");
        try
        {
            Clear();Call(app,"DrawWorld");
            int hero=-1,lastRing=-1,segments=0;
            for(int i=0;i<GUI.textureCalls.Count;i++)
            {
                var call=GUI.textureCalls[i];
                if(hero<0&&call.texture!=null&&call.texture.name!=null&&
                    call.texture.name.IndexOf(
                        "Frontier/HeroAnimator/Canonical/Hero_Vanguard",
                        StringComparison.Ordinal)>=0)hero=i;
                if(object.ReferenceEquals(call.texture,Texture2D.whiteTexture)&&
                    Math.Abs(call.color.r-tint.r)<.0001f&&
                    Math.Abs(call.color.g-tint.g)<.0001f&&
                    Math.Abs(call.color.b-tint.b)<.0001f&&
                    Math.Abs(call.rect.height-1.8f)<.0001f)
                {
                    lastRing=i;segments++;
                }
            }
            Check(hero>=0&&lastRing>hero&&segments==36,
                "Actual timed hazard outline was behind the hero/cover or still 32 disconnected dots");
            // A Unity Resources look edit must reach the REAL hazard ring,
            // not just add a decorative unused field or documentation.
            var lookProperty=typeof(FrontierRuntime).GetProperty(
                "CurrentWorldVisualLook",Private);
            Check(lookProperty!=null,"Runtime hazard art-direction profile missing");
            var look=lookProperty.GetValue(app,null);
            var alphaField=look.GetType().GetField("hazardRingAlpha",
                BindingFlags.Public|BindingFlags.Instance);
            Check(alphaField!=null,"Hazard overlay alpha is not artist editable");
            float originalAlpha=(float)alphaField.GetValue(look);
            float firstAlpha=GUI.textureCalls[lastRing].color.a;
            Check(Math.Abs(firstAlpha-originalAlpha)<.00001f,
                "WorldLook transparency not used in actual foreground hazard");
            try
            {
                alphaField.SetValue(look,originalAlpha*.8f);
                Clear();Call(app,"DrawHazardWarnings");
                Check(GUI.textureCalls.Count==36&&
                    Math.Abs(GUI.textureCalls[0].color.a-
                        firstAlpha*.8f)<.00001f,
                    "Changing art direction did not change source-based hazard opacity");
            }
            finally {alphaField.SetValue(look,originalAlpha); }
            // Offscreen warnings with no visible damage footprint should
            // not waste GUI submits; partial edge circles still render.
            hazard.position=game.Position+new Point(55f,0f);
            Clear();Call(app,"DrawHazardWarnings");
            Check(GUI.textureCalls.Count==0,
                "Offscreen hazards issued unrelated warning draw calls");
            hazard.position=game.Position+new Point(18f,0f);
            Clear();Call(app,"DrawHazardWarnings");
            int edgeCount=0;
            foreach(var c in GUI.textureCalls)
                if(object.ReferenceEquals(c.texture,Texture2D.whiteTexture)&&
                   Math.Abs(c.color.r-tint.r)<.0001f&&
                   Math.Abs(c.color.g-tint.g)<.0001f&&
                   Math.Abs(c.rect.height-1.8f)<.0001f)edgeCount++;
            Check(edgeCount==36,
                "Screen-edge hazard lost its real AoE radius warning");
            game.save.reducedMotion=false;game.save.effectsQuality=2;
            hazard.position=game.Position;
            Clear();Call(app,"DrawHazardWarnings");
            Check(GUI.textureCalls.Count==36,
                "High-quality hazard must keep the same damage radius");
            Console.WriteLine("PASS real DrawWorld hazard ordering, connected 36-piece AoE edge, border clipping and reduced-motion/FX");
        }
        finally
        {
            game.hazards.Clear();
            app.EndVisualReview();Clear();
        }
    }

    private static void CombatWarningUsesConnectedTrueRadiusGeometry(
        FrontierRuntime app)
    {
        Prepare(app);
        var game=app.Game;
        Matrix4x4 saved=GUI.matrix;
        Color savedColor=GUI.color;
        var viewport=Matrix4x4.TRS(new Vector3(53,61,0),
            Quaternion.identity,new Vector3(1.375f,1.375f,1));
        GUI.matrix=viewport;
        GUI.color=new Color(.28f,.45f,.67f,.82f);
        try
        {
            Point target=new Point(12.75f,13.25f);
            Color warning=new Color(.9f,.47f,.35f,.63f);
            foreach(float opening in new[]{-.8f,0f,1.4f})
            {
                Clear();
                Call(app,"DrawWarningArc",target,
                    GameCore.EnemyMeleeRadius,opening,1.65f,warning,24);
                Check(GUI.textureCalls.Count==24,
                    "Melee warning arc must be composed of 24 connected strokes");
                var center=(Point)Call(app,"ScreenPoint",target);
                for(int i=0;i<24;i++)
                {
                    var segment=GUI.textureCalls[i];
                    Check(object.ReferenceEquals(segment.texture,
                        Texture2D.whiteTexture)&&
                        Math.Abs(segment.rect.height-1.8f)<.00001f&&
                        segment.rect.width>=2f,
                        "Telegraph should draw continuous hairlines, not square dots");
                    Vector3 logicalMiddle=new Vector3(
                        segment.rect.x+segment.rect.width/2,
                        segment.rect.y+segment.rect.height/2,0);
                    Vector3 expected=viewport.MultiplyPoint3x4(new Vector3(
                        center.x+(float)Math.Cos(opening+(i+.5f)*1.65f/24)*
                            GameCore.EnemyMeleeRadius*36,
                        center.y+(float)Math.Sin(opening+(i+.5f)*1.65f/24)*
                            GameCore.EnemyMeleeRadius*36,0));
                    Vector3 actual=segment.matrix.MultiplyPoint3x4(logicalMiddle);
                    Check(Math.Abs(actual.x-expected.x)<.10f&&
                        Math.Abs(actual.y-expected.y)<.10f,
                        "Connected attack warning diverged from real hit radius");
                }
                Same(GUI.matrix,viewport,
                    "Warning arcs must restore real viewport transform");
                Same(GUI.color,new Color(.28f,.45f,.67f,.82f),
                    "Warnings cannot leak tint into next actor");
            }
            game.save.reducedMotion=true;
            Clear();
            Call(app,"DrawWarningArc",target,1f,0f,
                (float)(Math.PI*2),warning,36);
            Check(GUI.textureCalls.Count==36,
                "Reduced motion must retain legible, stationary danger radius");
            // Real GameCore arrow attack targeting stays at precisely the
            // archer's weapon-origin height, instead of a dotted generic
            // screen-space ray. QA calls actual DrawEnemyWarning, not an
            // isolated helper with invented coordinates.
            foreach(Point facing in new[]{
                new Point(1,0),new Point(-1,0),
                new Point(0,1),new Point(0,-1)})
            {
                Enemy bow=new Enemy(93214,1,target.x,target.y,65){
                    state=1,timer=.4f,windupDuration=.65f,facing=facing,
                    target=target+facing*4f
                };
                Clear();
                Call(app,"DrawEnemyWarning",bow);
                Check(GUI.textureCalls.Count==1,
                    "Enemy arrow lane must draw one connected authored-height line");
                var lane=GUI.textureCalls[0];
                Point lift=new Point(0,
                    GameCore.WeaponHeight(1,facing)/36f);
                Point muzzle=GameCore.WeaponOrigin(bow.Position,1,facing);
                var source=(Point)Call(app,"ScreenPoint",muzzle-lift);
                var destination=(Point)Call(app,"ScreenPoint",bow.target-lift);
                var middle=(source+destination)*.5f;
                var actual=Center(lane);
                var projected=viewport.MultiplyPoint3x4(
                    new Vector3(middle.x,middle.y,0));
                Near(actual,projected,
                    "Actual bow preview must match real projectile target height");
                Check(lane.rect.width>35f&&
                    Math.Abs(lane.rect.height-1.8f)<.00001f,
                    "Bow warning reverted to a square-dot point cloud");
                Same(GUI.matrix,viewport,
                    "Arrow warning must not rotate following characters");
                Same(GUI.color,new Color(.28f,.45f,.67f,.82f),
                    "Warning leaked color into HUD");
            }
            // Degenerate visual warning must never emit a NaN texture.
            Clear();Call(app,"DrawWarningArc",target,0f,0f,1f,warning,24);
            Check(GUI.textureCalls.Count==0,
                "Zero-radius telegraph may not paint a phantom circle");
        }
        finally
        {
            game.save.reducedMotion=false;
            GUI.matrix=saved;
            GUI.color=savedColor;
            Clear();
        }
        Console.WriteLine(
            "PASS rooted continuous attack telegraph: precise melee radius, 3 angles, letterboxed canvas and reduced motion");
    }
    private static void PaintedImpactFlourishRunsInWorld(FrontierRuntime app)
    {
        Prepare(app);
        var game=app.Game;
        int quality=game.save.effectsQuality;
        bool reduced=game.save.reducedMotion;
        Matrix4x4 matrix=GUI.matrix;
        Color color=GUI.color;
        try
        {
            game.save.effectsQuality=2;
            game.save.reducedMotion=false;
            game.effects.Clear();
            foreach(string kind in new[]{"slash","projectileImpact",
                "magicImpact"})
            {
                var hit=new Effect(kind,game.Position,.5f);
                hit.life=.45f;
                hit.direction=new Point(1,0);
                game.effects.Add(hit);
            }
            Clear();Call(app,"DrawAuthoredImpactFlourishes");
            Texture2D atlas=Get<Texture2D>(app,"impactFlourishSheet");
            Check(atlas!=null&&atlas.width==128&&atlas.height==96&&
                GUI.textureCalls.Count==3,
                "Three complete action-specific painted impact families not loaded");
            for(int row=0;row<3;row++)
            {
                var call=GUI.textureCalls[row];
                Check(object.ReferenceEquals(call.texture,atlas)&&
                    Math.Abs(call.uv.x)<.00001f&&
                    Math.Abs(call.uv.y-(1f-(row+1)/3f))<.00001f&&
                    Math.Abs(call.uv.width-.25f)<.00001f&&
                    Math.Abs(call.uv.height-1f/3f)<.00001f&&
                    call.color.a>0f&&call.color.a<.75f,
                    "32px sword/projectile/magic cells lost original UV or opacity");
                if(row>0)
                    Near(Center(call),Center(GUI.textureCalls[0]),
                        "Contact sprites shifted from GameCore hit anchor");
            }
            // All three types obey the same source event clock but use
            // independent hand-authored frames, not procedural resizing.
            foreach(Effect hit in game.effects)hit.life=.15f;
            Clear();Call(app,"DrawAuthoredImpactFlourishes");
            Check(GUI.textureCalls.Count==3&&
                GUI.textureCalls.TrueForAll(c=>
                    object.ReferenceEquals(c.texture,atlas)&&
                    Math.Abs(c.uv.x-.5f)<.00001f),
                "Third source frame not reached by actual hit lifetime");
            var property=typeof(FrontierRuntime).GetProperty(
                "CurrentWorldVisualLook",Private);
            Check(property!=null,"Impact visual config absent from runtime");
            var look=property.GetValue(app,null);
            var intensity=look.GetType().GetField("impactFlourishAlpha",
                BindingFlags.Public|BindingFlags.Instance);
            Check(intensity!=null,"Unity WorldLook missing painted-impact alpha");
            float original=(float)intensity.GetValue(look);
            try
            {
                float old=GUI.textureCalls[0].color.a;
                intensity.SetValue(look,original*.5f);
                Clear();Call(app,"DrawAuthoredImpactFlourishes");
                Check(GUI.textureCalls.Count==3&&
                    Math.Abs(GUI.textureCalls[0].color.a-old*.5f)<.00001f,
                    "Changing source-art opacity did not affect live combat");
            }
            finally{intensity.SetValue(look,original);}
            // Core Hit/Hurt and the true room-specific Boss release now
            // use the same approved source art; ordinary patrol release
            // must NOT masquerade as a magical Boss attack.
            game.effects.Clear();
            foreach(string kind in new[]{"impact","hurt","enemyRelease"})
            {
                var hit=new Effect(kind,game.Position,.24f);
                hit.life=.18f;
                hit.variant=kind=="enemyRelease"?11:0;
                game.effects.Add(hit);
            }
            Clear();Call(app,"DrawAuthoredImpactFlourishes");
            Check(GUI.textureCalls.Count==3&&
                GUI.textureCalls.TrueForAll(c=>
                    object.ReferenceEquals(c.texture,atlas))&&
                Math.Abs(GUI.textureCalls[0].uv.y-2f/3f)<.00001f&&
                Math.Abs(GUI.textureCalls[1].uv.y-2f/3f)<.00001f&&
                Math.Abs(GUI.textureCalls[2].uv.y)<.00001f,
                "Real melee damage or Boss release lost original contact art");
            game.effects[2].variant=1;
            Clear();Call(app,"DrawAuthoredImpactFlourishes");
            Check(GUI.textureCalls.Count==2,
                "Ordinary enemy release got an invented magical Boss effect");
            game.effects.Clear();
            foreach(string kind in new[]{"slash","projectileImpact",
                "magicImpact"})
            {
                var hit=new Effect(kind,game.Position,.5f);
                hit.life=.15f;
                hit.direction=new Point(1f,0f);
                game.effects.Add(hit);
            }
            // Crucial: these art cells must appear in the ACTUAL world
            // draw queue, not just a disconnected reviewer/preview.
            Clear();Call(app,"DrawWorld");
            Check(GUI.textureCalls.FindAll(c=>
                object.ReferenceEquals(c.texture,atlas)).Count==3,
                "New impact art not actually integrated into DrawWorld");
            // The new art is decoration, NOT a combat telegraph: on a
            // sprite painter's stage it belongs behind a nearer real
            // golden-haired actor and above a farther one. Exercise the
            // actual whole-world painter rather than a detached utility.
            game.effects.Clear();
            var spatial=new Effect("magicImpact",
                new Point(game.Position.x,game.Position.y-1f),.5f);
            spatial.life=.15f;
            game.effects.Add(spatial);
            for(int side=0;side<2;side++)
            {
                spatial.position=new Point(game.Position.x,
                    game.Position.y+(side==0?-1f:1f));
                Clear();Call(app,"DrawWorld");
                int paint=GUI.textureCalls.FindIndex(c=>
                    object.ReferenceEquals(c.texture,atlas));
                int protagonist=GUI.textureCalls.FindIndex(c=>
                    c.texture.name.StartsWith(
                        "resource:Frontier/HeroAnimator/Canonical/Hero_"));
                Check(paint>=0&&protagonist>=0&&
                    (side==0?paint<protagonist:paint>protagonist),
                    "World-anchored impact ignored original hero/prop depth");
                Check(GUI.textureCalls.FindAll(c=>
                    object.ReferenceEquals(c.texture,atlas)).Count==1,
                    "Depth painter duplicated one real combat embellishment");
            }
            game.save.reducedMotion=true;
            Clear();Call(app,"DrawAuthoredImpactFlourishes");
            Check(GUI.textureCalls.Count==0,
                "Reduced motion must suppress only supplemental art");
            game.save.reducedMotion=false;
            game.save.effectsQuality=1;
            Clear();Call(app,"DrawAuthoredImpactFlourishes");
            Check(GUI.textureCalls.Count==0,
                "Low/medium effects setting did not bypass optional overlays");
            game.save.effectsQuality=2;
            Set(app,"impactFlourishSheet",
                new Texture2D(1,1,TextureFormat.RGBA32,false));
            Resources.TextureOverrides["Frontier/Art/ImpactFlourish"]=
                new Texture2D(1,1,TextureFormat.RGBA32,false);
            Clear();Call(app,"DrawAuthoredImpactFlourishes");
            Check(GUI.textureCalls.Count==0,
                "Malformed impact atlas must safely fall back to original FX");
            Same(GUI.matrix,matrix,"Impact sprites polluted actor GUI matrix");
            Same(GUI.color,color,"Impact sprites polluted HUD tint");
            Console.WriteLine("PASS 12 painted impact frames in actual sorted DrawWorld: near/far hero occlusion, 6 effects and non-Boss exclusion, UV, world roots and accessibility");
        }
        finally
        {
            Resources.TextureOverrides.Remove("Frontier/Art/ImpactFlourish");
            game.effects.Clear();
            game.save.effectsQuality=quality;
            game.save.reducedMotion=reduced;
            GUI.matrix=matrix;GUI.color=color;
            Clear();
        }
    }
    // A real authored sword uses two fully painted Effects.png contact
    // poses. Their previous instantaneous 38% seam caused a one-frame
    // jump even though the GameCore effect position never moved.
    private static void SlashWholeEffectFramesHaveOneContactPivot(
        FrontierRuntime app,string folder)
    {
        app.BeginVisualReview(0,1);
        var game=app.Game;
        var source=Get<Texture2D>(app,"effectSheet");
        Check(source!=null,
            "Original sword slash Sprite cells missing from production");
        game.save.effectsQuality=2;
        game.save.reducedMotion=false;
        var existing=GUI.matrix;
        var canvas=Matrix4x4.TRS(new Vector3(29,41,0),
            Quaternion.identity,new Vector3(1.17f,1.17f,1));
        var oldColor=GUI.color;
        var look=typeof(FrontierRuntime).GetProperty(
            "CurrentWorldVisualLook",Private).GetValue(app,null);
        var width=look.GetType().GetField("slashBridgeFraction",
            BindingFlags.Instance|BindingFlags.Public);
        Check(width!=null,"Accepted visual settings omitted slash handoff");
        float authored=(float)width.GetValue(look);
        try
        {
            GUI.matrix=canvas;
            GUI.color=new Color(.7f,.8f,.9f,.72f);
            foreach(Point direction in new[]{new Point(1,0),
                new Point(-1,0),new Point(0,1),new Point(0,-1)})
            {
                var slash=new Effect("slash",new Point(13.25f,14.75f),.5f){
                    direction=direction};
                slash.life=.48f; // first original drawn frame
                Clear();Call(app,"DrawAuthoredEffect",slash);
                Check(GUI.textureCalls.Count==1&&
                    object.ReferenceEquals(GUI.textureCalls[0].texture,source)&&
                    Math.Abs(GUI.textureCalls[0].uv.x)<.00001f,
                    "First sword contact must show only original source cell zero");
                slash.life=.31f; // progress .38, midpoint of authored seam
                Clear();Call(app,"DrawAuthoredEffect",slash);
                Check(GUI.textureCalls.Count==2,
                    "Whole sword action must blend instead of teleporting at 38%");
                var from=GUI.textureCalls[0];
                var to=GUI.textureCalls[1];
                Check(object.ReferenceEquals(from.texture,source)&&
                    object.ReferenceEquals(to.texture,source)&&
                    Math.Abs(from.uv.x)<.00001f&&
                    Math.Abs(to.uv.x-.25f)<.00001f&&
                    Math.Abs(from.uv.width-.25f)<.00001f&&
                    Math.Abs(to.uv.width-.25f)<.00001f&&
                    Math.Abs(from.color.a-.5f)<.02f&&
                    Math.Abs(to.color.a-.5f)<.02f,
                    "Sword blend may only use two original full contact paintings");
                Near(Center(from),Center(to),
                    "Blended sword sources moved to two unrelated weapon roots");
                Same(from.matrix,to.matrix,
                    "Original contact cells used different directional rotations");
                Same(GUI.matrix,canvas,
                    "Swords leaked their visual rotation into next world Sprite");
                if(direction.x==1)
                    GUI.Export(folder,"review-illustrated-sword-contact-overlap");
                slash.life=.20f; // fully past authored source handoff
                Clear();Call(app,"DrawAuthoredEffect",slash);
                Check(GUI.textureCalls.Count==1&&
                    Math.Abs(GUI.textureCalls[0].uv.x-.25f)<.00001f,
                    "Recovery must settle on original fully authored second cell");
                slash.life=.33f; // 34%: inside adjustable crossfade
                Clear();Call(app,"DrawAuthoredEffect",slash);
                Check(GUI.textureCalls.Count==2,
                    "Source art seam width did not reach production renderer");
                float oldMix=GUI.textureCalls[1].color.a;
                width.SetValue(look,.10f);
                Clear();Call(app,"DrawAuthoredEffect",slash);
                Check(GUI.textureCalls.Count==2&&
                    GUI.textureCalls[1].color.a<oldMix-.05f,
                    "Changing WorldLook slash duration did not affect live frames");
                width.SetValue(look,authored);
                slash.life=.31f; // exact seam: low-tier disables extra draw
                game.save.reducedMotion=true;
                Clear();Call(app,"DrawAuthoredEffect",slash);
                Check(GUI.textureCalls.Count==1,
                    "Reduced motion must keep a single stable accepted sword Sprite");
                game.save.reducedMotion=false;
                game.save.effectsQuality=0;
                Clear();Call(app,"DrawAuthoredEffect",slash);
                Check(GUI.textureCalls.Count==1,
                    "Minimum visual quality must not allocate extra sword art");
                game.save.effectsQuality=2;
            }
            Console.WriteLine(
                "PASS real slash action 0->1 source-art overlap, four cardinal directions, identical world pivot, 30-frame fallback, editable quality and accessibility");
        }
        finally
        {
            width.SetValue(look,authored);
            GUI.matrix=existing;
            GUI.color=oldColor;
            app.EndVisualReview();
            Clear();
        }
    }

    private static void AuthoredHitContactCrossfadePreservesWorldPivot(
        FrontierRuntime app)
    {
        app.BeginVisualReview(0,1);
        var game=app.Game;
        game.save.effectsQuality=2;
        game.save.reducedMotion=false;
        var accepted=Get<Texture2D>(app,"effectSheet");
        Check(accepted!=null,
            "Contact transition requires original illustrated Effects.png");
        var viewport=Matrix4x4.TRS(new Vector3(31f,27f,0f),
            Quaternion.identity,new Vector3(1.2f,1.2f,1f));
        var originalMatrix=GUI.matrix;
        var incoming=new Color(.2f,.3f,.4f,.75f);
        var originalColor=GUI.color;
        try
        {
            GUI.matrix=viewport;GUI.color=incoming;
            foreach(string kind in new[]{"impact","hurt","projectileImpact"})
            {
                var hit=new Effect(kind,new Point(13.5f,14.5f),.5f);
                hit.life=.45f;
                Clear();Call(app,"DrawAuthoredEffect",hit);
                Check(GUI.textureCalls.Count==1&&
                    object.ReferenceEquals(GUI.textureCalls[0].texture,accepted)&&
                    Math.Abs(GUI.textureCalls[0].uv.x-.75f)<.00001f,
                    "First real contact must show one original spark Sprite");
                hit.life=.34f; // 32% lifetime: the authored cell seam.
                Clear();Call(app,"DrawAuthoredEffect",hit);
                Check(GUI.textureCalls.Count==2,
                    "Hit contact swapped hand-painted Sprite in one frame");
                var spark=GUI.textureCalls[0];
                var release=GUI.textureCalls[1];
                Check(object.ReferenceEquals(spark.texture,accepted)&&
                    object.ReferenceEquals(release.texture,accepted)&&
                    Math.Abs(spark.uv.x-.75f)<.00001f&&
                    Math.Abs(release.uv.x-
                        (kind=="projectileImpact"?0f:.5f))<.00001f&&
                    Math.Abs(spark.uv.width-.25f)<.00001f&&
                    Math.Abs(release.uv.width-.25f)<.00001f,
                    "Crossfade switched to nonauthored effect art");
                Near(Center(spark),Center(release),
                    "Crossfade made two disconnected hit centers");
                if(kind=="impact")
                {
                    // The ACTUAL Unity Resources WorldLook art parameter
                    // must control the opacity exchange in this exact
                    // production DrawAuthoredEffect path.
                    var property=typeof(FrontierRuntime).GetProperty(
                        "CurrentWorldVisualLook",Private);
                    Check(property!=null,
                        "Crossfade look profile is not wired into runtime");
                    var look=property.GetValue(app,null);
                    var width=look.GetType().GetField(
                        "hitContactBlendFraction",
                        BindingFlags.Instance|BindingFlags.Public);
                    Check(width!=null,
                        "Impact overlap duration missing from art resource");
                    float authored=(float)width.GetValue(look);
                    try
                    {
                        hit.life=.35f; // 30% elapsed; safely inside both.
                        Clear();Call(app,"DrawAuthoredEffect",hit);
                        Check(GUI.textureCalls.Count==2,
                            "Impact did not blend at default authored width");
                        float oldMix=GUI.textureCalls[1].color.a;
                        width.SetValue(look,.08f);
                        Clear();Call(app,"DrawAuthoredEffect",hit);
                        Check(GUI.textureCalls.Count==2&&
                            GUI.textureCalls[1].color.a<oldMix-.09f&&
                            GUI.textureCalls[1].color.a>0f,
                            "Editing WorldLook contact duration did not change live opacity");
                    }
                    finally{width.SetValue(look,authored);}
                    hit.life=.34f;
                    Clear();Call(app,"DrawAuthoredEffect",hit);
                    spark=GUI.textureCalls[0];
                    release=GUI.textureCalls[1];
                }
                Check(spark.color.a>.45f&&spark.color.a<.55f&&
                    release.color.a>.45f&&release.color.a<.55f&&
                    Math.Abs(spark.color.a+release.color.a-1f)<.001f,
                    "Short contact overlap popped in brightness/opacity");
                Same(GUI.matrix,viewport,
                    "Crossfading impact leaked Sprite rotation to next actor");
                Same(GUI.color,incoming,
                    "Crossfading impact leaked tint to world/HUD");
                hit.life=.20f; // 60%: fully transitioned.
                Clear();Call(app,"DrawAuthoredEffect",hit);
                Check(GUI.textureCalls.Count==1&&
                    Math.Abs(GUI.textureCalls[0].uv.x-
                        (kind=="projectileImpact"?0f:.5f))<.00001f,
                    "Late hit must use exactly one authored recovery Sprite");
                hit.life=.34f;
                game.save.reducedMotion=true;
                Clear();Call(app,"DrawAuthoredEffect",hit);
                Check(GUI.textureCalls.Count==1,
                    "Reduced-motion must suppress secondary contact motion");
                game.save.reducedMotion=false;
                game.save.effectsQuality=0;
                Clear();Call(app,"DrawAuthoredEffect",hit);
                Check(GUI.textureCalls.Count==1,
                    "Lowest FX tier must retain one real readable impact");
                game.save.effectsQuality=2;
            }
            Console.WriteLine(
                "PASS actual hit/arrow/magic projectile contact: authored 3->2/4 short crossfade, exact pivot and opacity, reduced-motion/FX");
        }
        finally
        {
            GUI.matrix=originalMatrix;GUI.color=originalColor;
            app.EndVisualReview();Clear();
        }
    }

    private static void MagicImpactHasARealContactLightAndStableAnchor(
        FrontierRuntime app)
    {
        Prepare(app);
        app.Game.save.reducedMotion=false;
        app.Game.save.effectsQuality=2;
        var source=Get<Texture2D>(app,"effectSheet");
        Check(source!=null,"Combat magic must use published detailed Effects.png");
        var effect=new Effect("magicImpact",new Point(13.5f,14f),.5f);
        effect.height=46f;
        effect.life=.44f;
        GUI.matrix=Matrix4x4.identity;
        GUI.color=Color.white;
        Clear();Call(app,"DrawAuthoredEffect",effect);
        Check(GUI.textureCalls.Count==2,
            "High FX magic must combine original vortex and contact star");
        var vortex=GUI.textureCalls[0];
        var sparkle=GUI.textureCalls[1];
        Check(object.ReferenceEquals(vortex.texture,source)&&
            object.ReferenceEquals(sparkle.texture,source)&&
            Math.Abs(vortex.uv.x-.25f)<.00001f&&
            Math.Abs(vortex.uv.y-.5f)<.00001f&&
            Math.Abs(sparkle.uv.x-.25f)<.00001f&&
            Math.Abs(sparkle.uv.y)<.00001f,
            "Magic cannot introduce a foreign or low-detail VFX texture");
        Near(Center(vortex),Center(sparkle),
            "Contact sparkle must share vortex's exact projectile anchor");
        Check(sparkle.rect.width<vortex.rect.width*.65f&&
            sparkle.color.a>0&&sparkle.color.a<vortex.color.a,
            "Short contact light cannot wash out original spell silhouette");
        float first=sparkle.color.a;
        effect.life=.27f;
        Clear();Call(app,"DrawAuthoredEffect",effect);
        Check(GUI.textureCalls.Count==2&&
            GUI.textureCalls[1].color.a<first&&
            GUI.textureCalls[1].color.a>0,
            "Contact light must fade along real effect lifetime");
        Near(Center(GUI.textureCalls[0]),Center(GUI.textureCalls[1]),
            "Spell impact must not slide during contact-to-recovery");
        effect.life=.18f;Clear();
        Call(app,"DrawAuthoredEffect",effect);
        Check(GUI.textureCalls.Count==1,
            "Late recovery must shed contact flash but keep vortex art");
        effect.life=.44f;
        app.Game.save.reducedMotion=true;Clear();
        Call(app,"DrawAuthoredEffect",effect);
        Check(GUI.textureCalls.Count==1,
            "Reduced-motion keeps single original spell silhouette");
        app.Game.save.reducedMotion=false;
        app.Game.save.effectsQuality=1;Clear();
        Call(app,"DrawAuthoredEffect",effect);
        Check(GUI.textureCalls.Count==1,
            "Medium quality should not allocate contact light draw");
        Console.WriteLine(
            "PASS original hand-painted magic vortex+contact star: one pivot, real lifecycle fade and reduced-motion/FX tiers");
    }
    private static void DryShoalDetailsFollowActualTide(FrontierRuntime app)
    {
        // The real Farshore terrain renderer draws GroundDetail after Surface.
        // An exposed '%' tile is dry only while GameCore.LowTide is true.
        // Otherwise a ground decal appears to float above the water atlas.
        app.BeginVisualReview(0,5);
        var game=app.Game;
        var detail=Get<Texture2D>(app,"detailSheet");
        Check(detail!=null,"Approved detailed foliage resource missing");
        int pickedX=-1,pickedY=-1;
        for(int y=3;y<30 && pickedX<0;y++)
        for(int x=3;x<40;x++)
        {
            if((int)Call(app,"PreparedTileSeed",x,y)%17!=0 ||
               (bool)Call(app,"GroundDetailOccluded",x,y)) continue;
            pickedX=x;pickedY=y;break;
        }
        Check(pickedX>=0,"No valid dry-shoal detail sample found");
        game.save.tideHeldUntil=0;
        game.save.totalTime=1f;
        Check(game.LowTide,"Setup must exercise the actual ebb interval");
        var point=new Point(250,190);
        Clear();
        Call(app,"DrawGroundDetail",pickedX,pickedY,'%',point);
        Check(GUI.textureCalls.Count==1 &&
              object.ReferenceEquals(GUI.textureCalls[0].texture,detail),
              "Low tide must retain detailed authored shoal vegetation");
        var grounded=GUI.textureCalls[0];
        game.save.totalTime=26f;
        Check(!game.LowTide,"Setup must reach the actual flooded interval");
        Clear();
        Call(app,"DrawGroundDetail",pickedX,pickedY,'%',point);
        Check(GUI.textureCalls.Count==0,
              "Flooded shoal still draws terrestrial foliage above water");
        // Adjacent fixed land never loses its ground detail when the tide rises.
        Clear();
        Call(app,"DrawGroundDetail",pickedX,pickedY,'.',point);
        Check(GUI.textureCalls.Count==1 &&
              object.ReferenceEquals(GUI.textureCalls[0].texture,detail) &&
              Math.Abs(GUI.textureCalls[0].rect.y-grounded.rect.y)<.001f,
              "High tide must not remove dry-land detail or move its root");
        Clear();
        Call(app,"DrawGroundDetail",pickedX,pickedY,'~',point);
        Check(GUI.textureCalls.Count==0,
              "Deep water may never grow a terrestrial decal");
        game.save.totalTime=1f;
        Clear();
        Call(app,"DrawGroundDetail",pickedX,pickedY,'%',point);
        Check(GUI.textureCalls.Count==1,
              "Ebb must restore grounded painted detail without relaunching scene");
        Console.WriteLine("PASS Farshore dry/higher-tide surface details: low-tide rooted land, flood no vegetation, immutable painting and dry-land continuity");
    }

    private static void AtmosphericMotesUseOriginalWorldAnchoredArt(
        FrontierRuntime app)
    {
        // This is the real scene's new atmospheric rendering method,
        // not a disconnected splash preview or fabricated white-box art.
        app.BeginVisualReview(0,1);
        var game=app.Game;
        game.save.x=12;game.save.y=14;
        game.save.effectsQuality=2;
        game.save.reducedMotion=false;
        Set(app,"camera",game.Position);
        Set(app,"worldActorOpacity",1f);
        Set(app,"clock",3.1f);
        Clear();
        Call(app,"DrawAuthoredAtmosphericMotes");
        var original=Get<Texture2D>(app,"weatherWispSheet");
        Check(original!=null&&original.width==96&&original.height==24&&
              GUI.textureCalls.Count>0,
            "forest biome must emit real new 4-motif painted PNG sprites");
        var strokes=GUI.textureCalls.ToArray();
        // The on-disk Unity TextAsset MUST actually control the real
        // painted atmosphere, not merely document unused look constants.
        var profile=typeof(FrontierRuntime).GetProperty(
            "CurrentWorldVisualLook",Private);
        Check(profile!=null,"Mote art direction profile missing");
        var look=profile.GetValue(app,null);
        var dayAlpha=look.GetType().GetField("ambientMoteDayAlpha",
            BindingFlags.Public|BindingFlags.Instance);
        Check(dayAlpha!=null,"Editable source-defined daylight mote absent");
        float authoredDay=(float)dayAlpha.GetValue(look);
        try
        {
            dayAlpha.SetValue(look,authoredDay*.5f);
            Clear();Call(app,"DrawAuthoredAtmosphericMotes");
            Check(GUI.textureCalls.Count==strokes.Length&&
                Math.Abs(GUI.textureCalls[0].color.a-
                    strokes[0].color.a*.5f)<.00001f,
                "WorldLook daylight edit never reached production motes");
        }
        finally { dayAlpha.SetValue(look,authoredDay); }
        Clear();Call(app,"DrawAuthoredAtmosphericMotes");
        Check(GUI.textureCalls.Count==strokes.Length,
            "Visual profile edit invalidated stable biome emitters");
        foreach(var fleck in strokes)
            Check(object.ReferenceEquals(fleck.texture,original)&&
                Math.Abs(fleck.uv.x)<.0001f&&
                Math.Abs(fleck.uv.y)<.0001f&&
                Math.Abs(fleck.uv.width-.25f)<.0001f&&
                Math.Abs(fleck.uv.height-1f)<.0001f&&
                fleck.rect.width>=7f&&fleck.rect.width<=9f&&
                fleck.color.a>0f&&fleck.color.a<.23f,
                "world motes must preserve original Effects cell 13 and low alpha");
        Matrix4x4 firstMatrix=GUI.matrix;
        Color firstColor=GUI.color;
        Set(app,"clock",4.7f);
        Clear();
        Call(app,"DrawAuthoredAtmosphericMotes");
        Check(GUI.textureCalls.Count==strokes.Length,
            "world-grid atmospheric cells must not pop under time updates");
        bool changed=false;
        for(int i=0;i<strokes.Length;i++)
        {
            var before=strokes[i];var after=GUI.textureCalls[i];
            Check(object.ReferenceEquals(after.texture,before.texture)&&
                Math.Abs(after.uv.x-before.uv.x)<.00001f&&
                Math.Abs(after.uv.y-before.uv.y)<.00001f&&
                Math.Abs(after.rect.x-before.rect.x)<3f&&
                Math.Abs(after.rect.y-before.rect.y)<3f,
                "atmosphere teleported, switched pixels or changed footless anchor");
            if(Math.Abs(after.color.a-before.color.a)>.0001f)changed=true;
        }
        Check(changed,"ambient movement never changed illustrated mote opacity");
        Same(GUI.matrix,firstMatrix,
            "ambient sprite rotation leaked into the rest of the GameView");
        Same(GUI.color,firstColor,
            "ambient drawing left GUI tint on the player or HUD");
        // Direct-function QA is insufficient if the actual Frontier scene
        // stops invoking it. Exercise the full depth-sorted DrawWorld path,
        // then require the same approved 8px Effects.png cells in the live
        // scene command buffer, not standalone preview commands.
        Clear();
        Call(app,"DrawWorld");
        int integrated=GUI.textureCalls.FindAll(t=>
            object.ReferenceEquals(t.texture,original)&&
            Math.Abs(t.uv.x)<.0001f&&
            Math.Abs(t.uv.y)<.0001f&&
            Math.Abs(t.rect.width-8f)<.001f&&
            Math.Abs(t.rect.height-8f)<.001f).Count;
        Check(integrated>=strokes.Length && integrated>0,
            "Full production DrawWorld lost its illustrated biome particles");
        // Critical production-layer invariant: atmospheric flecks belong
        // BELOW both the canonical blonde hero and any gameplay warning.
        // A source-correct atlas painted after the actor still makes dots
        // drift over the actor's face and equipped weapon.
        int lastMote=-1, firstCanonicalHero=-1;
        for(int index=0;index<GUI.textureCalls.Count;index++)
        {
            var item=GUI.textureCalls[index];
            if(object.ReferenceEquals(item.texture,original)&&
                Math.Abs(item.uv.x)<.00001f&&
                Math.Abs(item.uv.y)<.00001f&&
                Math.Abs(item.rect.width-8f)<.001f&&
                Math.Abs(item.rect.height-8f)<.001f)
                lastMote=index;
            if(firstCanonicalHero<0&&item.texture!=null&&
                item.texture.name!=null&&item.texture.name.IndexOf(
                    "Frontier/HeroAnimator/Canonical/Hero_Vanguard",
                    StringComparison.Ordinal)>=0)
                firstCanonicalHero=index;
        }
        Check(lastMote>=0&&firstCanonicalHero>lastMote,
            "World particles render OVER the actual hero body/weapon");
        // Test distinctive but rooted regional travel instead of merely
        // checking the palette strings exist in source.
        foreach(int biome in new[]{6,7})
        {
            app.BeginVisualReview(0,biome);
            Set(app,"camera",app.Game.Position);
            Set(app,"clock",3.1f);
            app.Game.save.effectsQuality=2;
            app.Game.save.reducedMotion=false;
            Clear();Call(app,"DrawAuthoredAtmosphericMotes");
            var ambient=GUI.textureCalls.ToArray();
            Check(ambient.Length>0,
                "desert and snow must still expose animated original art");
            Color expected=biome==7?new Color(.82f,.92f,1f):
                new Color(1f,.84f,.61f);
            foreach(var item in ambient)
                Check(object.ReferenceEquals(item.texture,original)&&
                    Math.Abs(item.uv.x-(biome==7?.75f:.25f))<.00001f&&
                    Math.Abs(item.uv.y)<.00001f&&
                    Math.Abs(item.uv.width-.25f)<.00001f&&
                    Math.Abs(item.uv.height-1f)<.00001f&&
                    Math.Abs(item.color.r-expected.r)<.001f&&
                    Math.Abs(item.color.g-expected.g)<.001f&&
                    Math.Abs(item.color.b-expected.b)<.001f&&
                    item.color.a>0f&&item.color.a<=.22f,
                    "biome VFX switched to incompatible palette or primitive");
            Set(app,"clock",4.7f);
            Clear();Call(app,"DrawAuthoredAtmosphericMotes");
            Check(GUI.textureCalls.Count==ambient.Length,
                "wind should not spawn or drop snow/desert sprites by frame");
            for(int i=0;i<ambient.Length;i++)
                Check(Math.Abs(GUI.textureCalls[i].rect.x-ambient[i].rect.x)<
                        12f&&Math.Abs(GUI.textureCalls[i].rect.y-
                        ambient[i].rect.y)<12f,
                    "biome wind disconnected its world-seeded Sprite");
        }
        // A corrupt/missing sheet must fall back to approved Effects.png,
        // then adopt the REPAIRED original art after a bounded import
        // cooldown. A permanently stale source disables the newest art
        // for the entire scene, while loading it every repaint causes jank.
        var oldSource=Get<Texture2D>(app,"effectSheet");
        var invalid=new Texture2D(1,1,TextureFormat.RGBA32,false);
        const string wispPath="Frontier/Art/WeatherWisps";
        Resources.TextureOverrides[wispPath]=invalid;
        Set(app,"weatherWispSheet",invalid);
        app.BeginVisualReview(0,1);
        game=app.Game;
        game.save.effectsQuality=2;
        game.save.reducedMotion=false;
        Set(app,"clock",20f);
        try
        {
            int beforeLoads=Resources.weatherWispImportLoads;
            Clear();Call(app,"DrawAuthoredAtmosphericMotes");
            Check(Resources.weatherWispImportLoads==beforeLoads+1&&
                oldSource!=null&&GUI.textureCalls.Count>0&&
                GUI.textureCalls.TrueForAll(c=>
                    object.ReferenceEquals(c.texture,oldSource)&&
                    Math.Abs(c.uv.x-.25f)<.00001f&&
                    Math.Abs(c.uv.y)<.00001f),
                "Malformed imported weather art lost accepted Effects fallback");
            Resources.TextureOverrides.Remove(wispPath);
            Set(app,"clock",21f);
            Clear();Call(app,"DrawAuthoredAtmosphericMotes");
            Check(Resources.weatherWispImportLoads==beforeLoads+1&&
                GUI.textureCalls.Count>0&&
                object.ReferenceEquals(GUI.textureCalls[0].texture,oldSource),
                "Weather art retried every repaint instead of using fallback");
            Set(app,"clock",22.2f);
            Clear();Call(app,"DrawAuthoredAtmosphericMotes");
            var refreshed=Get<Texture2D>(app,"weatherWispSheet");
            Check(Resources.weatherWispImportLoads==beforeLoads+2&&
                refreshed!=null&&refreshed.width==96&&refreshed.height==24&&
                GUI.textureCalls.Count>0&&
                object.ReferenceEquals(GUI.textureCalls[0].texture,refreshed)&&
                Math.Abs(GUI.textureCalls[0].uv.x)<.00001f,
                "Completed Unity weather import never restored original forest art");
        }
        finally
        {
            Resources.TextureOverrides.Remove(wispPath);
            Set(app,"weatherWispSheet",original);
        }
        // Return to the forest fixture for the reduced-motion guarantee.
        app.BeginVisualReview(0,1);
        game=app.Game;
        game.save.effectsQuality=2;
        game.save.reducedMotion=true;
        Clear();Call(app,"DrawAuthoredAtmosphericMotes");
        Check(GUI.textureCalls.Count==0,
            "reduced-motion setting must remove animated world particles");
        game.save.reducedMotion=false;
        game.save.effectsQuality=0;
        Clear();Call(app,"DrawAuthoredAtmosphericMotes");
        Check(GUI.textureCalls.Count==0,
            "low effects must disable world particle overdraw");
        game.save.effectsQuality=2;
        app.BeginVisualReview(0,4);
        Clear();Call(app,"DrawAuthoredAtmosphericMotes");
        Check(GUI.textureCalls.Count==0,
            "citadel interior cannot receive outdoor forest motes");
        Console.WriteLine(
            "PASS original 4-motif WeatherWisps PNG: forest/sand/snow frame mapping, full DrawWorld integration, fallback, spatial continuity, accessibility and GUI isolation");
    }

    private static void OrientedCoastlineRetainsApprovedWaterArt(
        FrontierRuntime app,string folder)
    {
        // Exercise the real scene's active water renderer, with four bank
        // directions on a temporary map-row fixture (restored in finally).
        app.BeginVisualReview(0,5);
        var g=app.Game;
        var source=Get<Texture2D>(app,"terrainVariants");
        Check(source!=null,"Approved source-painted shore atlas missing");
        string[] rows=g.Map.rows;
        int y=16,x=20;
        while(((int)Call(app,"PreparedTileSeed",x,y)&1)!=0) x++;
        Check(y>1&&y+1<rows.Length&&x+1<rows[y].Length,
            "Shoreline test location must be inside an actual map");
        string n=rows[y-1],c=rows[y],b=rows[y+1];
        bool reduced=g.save.reducedMotion;
        int effects=g.save.effectsQuality;
        Matrix4x4 originalMatrix=GUI.matrix;
        Color originalColor=GUI.color;
        try
        {
            char[] north=n.ToCharArray(),middle=c.ToCharArray(),
                south=b.ToCharArray();
            north[x]='.';south[x]='.';
            middle[x]='~';middle[x-1]='.';middle[x+1]='.';
            rows[y-1]=new string(north);rows[y]=new string(middle);
            rows[y+1]=new string(south);
            g.save.reducedMotion=false;g.save.effectsQuality=2;
            var root=new Point(375,283);
            Set(app,"clock",1.4f);
            Clear();Call(app,"DrawAuthoredShorelineWash",x,y,'~',root);
            Check(GUI.textureCalls.Count==4,
                "Four exposed original water banks must each paint a crest");
            var all=GUI.textureCalls.ToArray();
            foreach(var crest in all)
                Check(object.ReferenceEquals(crest.texture,source)&&
                    Math.Abs(crest.uv.width-24f/192f)<.00001f&&
                    Math.Abs(crest.uv.height-7f/192f)<.00001f&&
                    Math.Abs(crest.rect.width-24f)<.001f&&
                    crest.color.a>0&&crest.color.a<.18f,
                    "Coastline cannot replace detailed water with coarse bars");
            Check(all[0].matrix.m00*all[2].matrix.m00<-.8f&&
                all[1].matrix.m01*all[3].matrix.m01<-.8f,
                "North/south and east/west must face opposing coastlines");
            Same(GUI.matrix,originalMatrix,
                "Shoreline animation leaked rotation onto nearby actors");
            Same(GUI.color,originalColor,
                "Shoreline left tinted HUD, hero or vegetation");
            GUI.Export(folder,"review-four-way-source-painted-shorebreak");
            Set(app,"clock",3.2f);
            Clear();Call(app,"DrawAuthoredShorelineWash",x,y,'~',root);
            Check(GUI.textureCalls.Count==4,
                "Author source bank waves disappeared on next frame");
            bool moving=false;
            for(int i=0;i<4;i++)
            {
                var after=GUI.textureCalls[i];
                Check(Math.Abs(after.uv.x-all[i].uv.x)<.00001f&&
                    Math.Abs(after.uv.y-all[i].uv.y)<.00001f&&
                    Math.Abs(after.rect.x-all[i].rect.x)<=5f&&
                    Math.Abs(after.rect.y-all[i].rect.y)<.001f,
                    "Shoreline jumped, stretched or changed source pixels");
                if(Math.Abs(after.rect.x-all[i].rect.x)>.01f)moving=true;
            }
            Check(moving,"Surf does not animate at all between gameplay frames");
            // No water/land interface: ALL four neighbouring tiles flood.
            north[x]='~';south[x]='~';
            middle[x-1]='~';middle[x+1]='~';
            rows[y-1]=new string(north);rows[y]=new string(middle);
            rows[y+1]=new string(south);
            Clear();Call(app,"DrawAuthoredShorelineWash",x,y,'~',root);
            Check(GUI.textureCalls.Count==0,
                "Open sea center must not draw phantom shore foam");
            north[x]='.';rows[y-1]=new string(north);
            g.save.reducedMotion=true;
            Clear();Call(app,"DrawAuthoredShorelineWash",x,y,'~',root);
            Check(GUI.textureCalls.Count==0,
                "Reduced motion must suppress moving water foam");
            g.save.reducedMotion=false;g.save.effectsQuality=0;
            Clear();Call(app,"DrawAuthoredShorelineWash",x,y,'~',root);
            Check(GUI.textureCalls.Count==0,
                "Zero-effects mode cannot create extra water samples");
            g.save.effectsQuality=2;
            Clear();Call(app,"DrawAuthoredShorelineWash",x,y,'.',root);
            Check(GUI.textureCalls.Count==0,
                "Dry terrain cannot acquire ocean break");
            Console.WriteLine("PASS actual 4-way accepted-water shorebreak orientation, continuous root, UV, low-FX and reduced-motion");
        }
        finally
        {
            rows[y-1]=n;rows[y]=c;rows[y+1]=b;
            g.save.reducedMotion=reduced;g.save.effectsQuality=effects;
            GUI.matrix=originalMatrix;GUI.color=originalColor;
        }
    }

    private static void AuthoredRelicGlintsStayOnActualDetailedWorldProps(
        FrontierRuntime app,string folder)
    {
        app.BeginVisualReview(0,1);
        var game=app.Game;
        bool reduced=game.save.reducedMotion;
        int quality=game.save.effectsQuality;
        var field=Get<Texture2D>(app,"fieldProps");
        var palette=typeof(FrontierRuntime).GetProperty(
            "CurrentWorldVisualLook",Private).GetValue(app,null);
        var alphaField=palette.GetType().GetField("relicShimmerAlpha",
            BindingFlags.Instance|BindingFlags.Public);
        Check(alphaField!=null,"New authored shimmer must be artist-tunable");
        float original=(float)alphaField.GetValue(palette);
        try
        {
            game.save.reducedMotion=false;
            game.save.effectsQuality=2;
            string[] types={"crystal","ore","obelisk","altar","engine","mirror"};
            int[] cells={4,4,3,12,12,14};
            for(int i=0;i<types.Length;i++)
            {
                Set(app,"clock",1.25f+i*.3f);
                Clear();Call(app,"PropImage",types[i],i,
                    320f,280f,86f,90f,Color.white);
                Check(GUI.textureCalls.Count==2,
                    "Actual "+types[i]+" world prop did not receive an authored light cell");
                var body=GUI.textureCalls[0];
                var gleam=GUI.textureCalls[1];
                Check(object.ReferenceEquals(body.texture,field)&&
                    Math.Abs(body.uv.x-cells[i]%4/4f)<.00001f&&
                    Math.Abs(body.uv.y-(1f-(cells[i]/4+1)/4f))<.00001f&&
                    Math.Abs(body.rect.width-90f)<.001f,
                    "Relic lighting replaced the detailed published original body");
                Check(gleam.texture.name=="resource:Frontier/Art/RelicShimmer"&&
                    gleam.texture.width==96&&gleam.texture.height==24&&
                    Math.Abs(Math.Abs(gleam.uv.width)-.25f)<.00001f&&
                    Math.Abs(gleam.uv.height-1f)<.00001f&&
                    gleam.color.a>.01f&&gleam.color.a<=.32f,
                    "Relic glow must be one complete original authored 24px cell");
                Check(gleam.rect.width<=35f&&gleam.rect.height<=35f&&
                    gleam.rect.x>=body.rect.x&&
                    gleam.rect.x+gleam.rect.width<=body.rect.x+body.rect.width&&
                    gleam.rect.y>=body.rect.y&&
                    gleam.rect.y+gleam.rect.height<=body.rect.y+body.rect.height,
                    "Relic glint escaped the parent world object or changed its scale");
                float glowX=gleam.rect.x,glowY=gleam.rect.y;
                float uvBefore=gleam.uv.x;
                float priorOpacity=gleam.color.a;
                alphaField.SetValue(palette,original*.5f);
                Clear();Call(app,"PropImage",types[i],i,
                    320f,280f,86f,90f,Color.white);
                Check(GUI.textureCalls.Count==2&&
                    Math.Abs(GUI.textureCalls[1].color.a-priorOpacity*.5f)<.0001f,
                    "Editable WorldLook relic light alpha did not drive production");
                alphaField.SetValue(palette,original);
                Set(app,"clock",2.15f+i*.3f);
                Clear();Call(app,"PropImage",types[i],i,
                    320f,280f,86f,90f,Color.white);
                var subsequent=GUI.textureCalls[1];
                Check(Math.Abs(subsequent.rect.x-glowX)<.001f&&
                    Math.Abs(subsequent.rect.y-glowY)<.001f&&
                    Math.Abs(subsequent.uv.x-uvBefore)>.00001f,
                    "Relic animation must change painted frames without sliding world root");
                if(i==0)GUI.Export(folder,"review-relic-shimmer-original-world-prop");
            }
            game.save.reducedMotion=true;
            Clear();Call(app,"DrawFieldProp","crystal",0,
                320f,280f,86f,90f,Color.white);
            Check(GUI.textureCalls.Count==1&&
                object.ReferenceEquals(GUI.textureCalls[0].texture,field),
                "Reduced-motion must preserve crystal source body without animation");
            game.save.reducedMotion=false;
            game.save.effectsQuality=0;
            Clear();Call(app,"DrawFieldProp","mirror",0,
                320f,280f,86f,90f,Color.white);
            Check(GUI.textureCalls.Count==1,
                "Zero-FX must preserve original mirror body without extra texture");
            game.save.effectsQuality=2;
            Clear();Call(app,"DrawFieldProp","well",0,
                320f,280f,86f,90f,Color.white);
            Check(GUI.textureCalls.Count==1,
                "Non-magical world furniture acquired an unrelated glow Sprite");
            Console.WriteLine("PASS six real detailed relic objects with new four-frame painted glow, fixed original body/anchor, WorldLook, reduced motion and low FX");
        }
        finally
        {
            alphaField.SetValue(palette,original);
            game.save.reducedMotion=reduced;
            game.save.effectsQuality=quality;
            app.EndVisualReview();Clear();
        }
    }

    private static void OriginalCampfireFlameGetsRootedEmbers(
        FrontierRuntime app,string folder)
    {
        app.BeginVisualReview(0,1);
        var g=app.Game;
        Texture2D fire=Get<Texture2D>(app,"fieldProps");
        Texture2D flashes=Get<Texture2D>(app,"effectSheet");
        Check(fire!=null&&flashes!=null,
            "Actual accepted field and painted effect Sprite resources missing");
        bool reduced=g.save.reducedMotion;
        int quality=g.save.effectsQuality;
        var look=typeof(FrontierRuntime).GetProperty(
            "CurrentWorldVisualLook",Private).GetValue(app,null);
        var field=look.GetType().GetField("emberFlickerAlpha",
            BindingFlags.Instance|BindingFlags.Public);
        Check(field!=null,"Campfire requires live author-editable light intensity");
        float original=(float)field.GetValue(look);
        try
        {
            g.save.reducedMotion=false;
            g.save.effectsQuality=2;
            Set(app,"clock",1.4f);
            Clear();Call(app,"DrawFieldProp","fire",0,
                320f,280f,76f,78f,Color.white);
            Check(GUI.textureCalls.Count==3,
                "Campfire must contain one intact static body and two authored ember cells");
            var before=GUI.textureCalls.ToArray();
            Check(object.ReferenceEquals(before[0].texture,fire)&&
                Math.Abs(before[0].uv.x-.25f)<.00001f&&
                Math.Abs(before[0].uv.y)<.00001f,
                "Fireplace lost the original complete FieldProps.png body");
            Check(object.ReferenceEquals(before[1].texture,flashes)&&
                object.ReferenceEquals(before[2].texture,flashes)&&
                Math.Abs(before[1].uv.x-.25f)<.00001f&&
                Math.Abs(before[1].uv.y)<.00001f&&
                Math.Abs(before[2].uv.x-.75f)<.00001f&&
                Math.Abs(before[2].uv.y-.75f)<.00001f&&
                before[1].color.a>0f&&before[1].color.a<.32f,
                "Ambient embers must use original illustrated contact cells and low alpha");
            GUI.Export(folder,"review-authored-campfire-emissive");
            // The REAL live PropImage(...) call used by quest bonfires and
            // shelter props must reach the same source-preserving producer.
            Clear();Call(app,"PropImage","fire",0,
                320f,280f,76f,78f,Color.white);
            Check(GUI.textureCalls.Count==3&&
                object.ReferenceEquals(GUI.textureCalls[0].texture,fire)&&
                object.ReferenceEquals(GUI.textureCalls[1].texture,flashes)&&
                object.ReferenceEquals(GUI.textureCalls[2].texture,flashes),
                "Production PropImage omitted the actual animated campfire art");
            Set(app,"clock",2.7f);
            Clear();Call(app,"DrawFieldProp","fire",0,
                320f,280f,76f,78f,Color.white);
            Check(GUI.textureCalls.Count==3&&
                Math.Abs(GUI.textureCalls[0].rect.x-before[0].rect.x)<.0001f&&
                Math.Abs(GUI.textureCalls[0].rect.y-before[0].rect.y)<.0001f&&
                Math.Abs(GUI.textureCalls[0].uv.x-before[0].uv.x)<.00001f&&
                Math.Abs(GUI.textureCalls[1].rect.y-before[1].rect.y)<=14.01f&&
                Math.Abs(GUI.textureCalls[1].rect.y-before[1].rect.y)>.02f,
                "Flicker displaced original fire body or animated discontinuously");
            field.SetValue(look,original*.5f);
            Clear();Call(app,"DrawFieldProp","fire",0,
                320f,280f,76f,78f,Color.white);
            // Check ratio against SAME clock, not a different phase.
            float halfAlpha=GUI.textureCalls[1].color.a;
            field.SetValue(look,original);
            Clear();Call(app,"DrawFieldProp","fire",0,
                320f,280f,76f,78f,Color.white);
            Check(Math.Abs(halfAlpha-GUI.textureCalls[1].color.a*.5f)<.0001f,
                "WorldLook ember brightness did not drive actual art");
            g.save.effectsQuality=1;
            Clear();Call(app,"DrawFieldProp","fire",0,
                320f,280f,76f,78f,Color.white);
            Check(GUI.textureCalls.Count==2,
                "Medium effects should keep one subtle source-art ember");
            g.save.reducedMotion=true;
            Clear();Call(app,"DrawFieldProp","fire",0,
                320f,280f,76f,78f,Color.white);
            Check(GUI.textureCalls.Count==1&&
                object.ReferenceEquals(GUI.textureCalls[0].texture,fire),
                "Reduced motion must retain original static fire body without sparks");
            g.save.reducedMotion=false;g.save.effectsQuality=0;
            Clear();Call(app,"DrawFieldProp","fire",0,
                320f,280f,76f,78f,Color.white);
            Check(GUI.textureCalls.Count==1,
                "Low-FX must not remove the campsite nor emit fake embers");
            Clear();Call(app,"DrawFieldProp","well",0,
                320f,280f,76f,78f,Color.white);
            Check(GUI.textureCalls.Count==1,
                "Ordinary solid environment props must not acquire fire particles");
            Console.WriteLine(
                "PASS original complete-body campfire + two source-painted ambient embers: world root, temporal drift, material, low-FX and reduced motion");
        }
        finally
        {
            field.SetValue(look,original);
            g.save.reducedMotion=reduced;
            g.save.effectsQuality=quality;
        }
    }

    private static void WaterKeepsItsHandPaintedTextureAcrossTides(FrontierRuntime app)
    {
        app.BeginVisualReview(5,1);
        var g=app.Game;
        var sheet=Get<Texture2D>(app,"terrainVariants");
        Check(sheet!=null&&sheet.width==192&&sheet.height==192,
            "Detailed accepted TerrainVariants water source is required");
        g.save.effectsQuality=2;
        g.save.reducedMotion=false;
        int x=-1,y=-1;
        for(int yy=0;yy<60&&x<0;yy++)
            for(int xx=0;xx<60;xx++)
            {
                int seed=(int)Call(app,"PreparedTileSeed",xx,yy);
                if(seed%6==0){x=xx;y=yy;break;}
            }
        Check(x>=0,"Water shimmer stable seed sample unavailable");
        var origin=new Point(210,250);
        Set(app,"clock",3.4f);
        Clear();Call(app,"DrawAuthoredWaterShimmer",x,y,'~',origin);
        Check(GUI.textureCalls.Count==1,
            "A valid deep-water tile should draw one AUTHOR water highlight");
        var water=GUI.textureCalls[0];
        Check(object.ReferenceEquals(water.texture,sheet)&&
            Math.Abs(water.rect.width-25f)<.0001f&&
            Math.Abs(water.rect.height-9f)<.0001f&&
            Math.Abs(water.uv.width-25f/192f)<.0001f&&
            Math.Abs(water.uv.height-9f/192f)<.0001f,
            "Water must sample unscaled original water-paint pixels, not 8px bars");
        Check(water.color.a>.05f&&water.color.a<.3f,
            "Water glints must stay below the original illustrated water");
        Set(app,"clock",5.7f);
        Clear();Call(app,"DrawAuthoredWaterShimmer",x,y,'~',origin);
        Check(GUI.textureCalls.Count==1&&
            Math.Abs(GUI.textureCalls[0].color.a-water.color.a)>.00001f&&
            Math.Abs(GUI.textureCalls[0].rect.x-water.rect.x)<.0001f&&
            Math.Abs(GUI.textureCalls[0].uv.x-water.uv.x)<.0001f,
            "Animate only light intensity; source UV and terrain root may not twitch");
        g.save.reducedMotion=true;Clear();
        Call(app,"DrawAuthoredWaterShimmer",x,y,'~',origin);
        Check(GUI.textureCalls.Count==0,
            "Reduced motion keeps all painted water static");
        g.save.reducedMotion=false;
        g.save.effectsQuality=0;Clear();
        Call(app,"DrawAuthoredWaterShimmer",x,y,'~',origin);
        Check(GUI.textureCalls.Count==0,
            "Low-effects mode may not allocate new water glint draws");
        g.save.effectsQuality=2;Clear();
        // Only high tide at Farshore covers percent shoals with water.
        g.save.totalTime=10f; // Core tide state may be room dependent.
        Call(app,"DrawAuthoredWaterShimmer",x,y,'=',origin);
        Check(GUI.textureCalls.Count==0,
            "Road and bridge stones cannot acquire ocean highlights");
        Console.WriteLine("PASS stable wave artwork UV, smooth low-alpha glints, reduced motion and dry-road safety");
    }

    private static void RootedRescueSurvivorHasOneGroundedSprite(
        FrontierRuntime app)
    {
        Prepare(app);
        Texture2D atlas=Get<Texture2D>(app,"npcSheet");
        Check(atlas!=null,"Original 16-NPC civilian atlas required");
        var g=app.Game;
        bool oldReduced=g.save.reducedMotion;
        Matrix4x4 oldMatrix=GUI.matrix;
        Color oldColor=GUI.color;
        var canvas=Matrix4x4.TRS(new Vector3(19,35,0),
            Quaternion.identity,new Vector3(1.25f,1.25f,1));
        Point planted=new Point(280,330);
        try
        {
            g.save.reducedMotion=false;
            GUI.matrix=canvas;GUI.color=new Color(.5f,.6f,.7f,.83f);
            Clear();
            Call(app,"DrawRootedCivilianBody",planted,1.15f,
                Color.white,false);
            Check(GUI.textureCalls.Count==1,
                "Escort body must be one original complete-body Sprite");
            var body=GUI.textureCalls[0];
            Check(object.ReferenceEquals(body.texture,atlas)&&
                Math.Abs(body.rect.width-96)<.0001f&&
                Math.Abs(body.uv.x-.5f)<.00001f&&
                Math.Abs(body.uv.y)<.00001f&&
                Math.Abs(body.uv.width-.25f)<.00001f,
                "Walking survivor changed identity or source atlas cell 14");
            Vector3 root=body.matrix.MultiplyPoint3x4(
                new Vector3(planted.x,planted.y,0));
            Vector3 expected=canvas.MultiplyPoint3x4(
                new Vector3(planted.x,planted.y,0));
            Near(root,expected,
                "Survivor's boots must stay welded to ground when leaning");
            Check(Math.Abs(body.matrix.m01)>.001f,
                "Real rooted body lean must respond to escort movement");
            Same(GUI.matrix,canvas,"Survivor rotated later HUD/NPCs");
            Color prior=GUI.color;
            Set(app,"clock",17.3f);
            Clear();Call(app,"DrawRootedCivilianBody",planted,-1.35f,
                Color.white,true);
            var hurt=GUI.textureCalls[0];
            Near(hurt.matrix.MultiplyPoint3x4(
                new Vector3(planted.x,planted.y,0)),expected,
                "Hit reaction must not levitate or slide planted survivor");
            Check(hurt.uv.width<0f&&
                Math.Abs(hurt.uv.x-.75f)<.00001f,
                "Flipping on injury must keep original civilian face");
            g.save.reducedMotion=true;
            Clear();Call(app,"DrawRootedCivilianBody",planted,2f,
                Color.white,false);
            Check(GUI.textureCalls.Count==1,
                "Reduced motion must preserve visible civilian identity");
            Same(GUI.textureCalls[0].matrix,canvas,
                "Reduced-motion setting did not stop procedural lean");
            Same(GUI.matrix,canvas,"Animation leaked its viewport transform");
            Same(GUI.color,prior,"Civilian body animation leaked its tint");
        }
        finally
        {
            g.save.reducedMotion=oldReduced;
            GUI.matrix=oldMatrix;
            GUI.color=oldColor;
        }
        Console.WriteLine(
            "PASS source-preserved rescued civilian body: no levitating boot, anchored hit lean, mirroring and native-friendly Sprite path");
    }

    private static void GroundedActorsHaveContinuousAcceptedSoftShadows(FrontierRuntime app)
    {
        Prepare(app);
        var source=Get<Texture2D>(app,"actorContactShadows");
        Check(source==null,
            "Old low-detail ActorContactShadows.png cannot re-enter production");
        Matrix4x4 original=Matrix4x4.TRS(new Vector3(21,37,0),
            Quaternion.identity,new Vector3(1.2f,1.2f,1));
        Color incoming=new Color(.3f,.6f,.7f,.8f);
        GUI.matrix=original; GUI.color=incoming;
        int n=0;
        foreach(float width in new[]{34f,72f,142f,240f})
        foreach(float depth in new[]{13f,22f,33f})
        {
            Point root=new Point(280,420);
            Clear();
            Call(app,"DrawActorContactShadow",root,width,depth,7,.78f);
            Check(GUI.textureCalls.Count==1,
                "One continuous feathered source shadow per actor; no bars/segments");
            var shadow=GUI.textureCalls[0];
            Check(shadow.texture.name=="soft_glow",
                "Use the existing stable soft-light texture for shadow, not crude PNGs");
            Check(Math.Abs(shadow.rect.width-width)<.0001f&&
                Math.Abs(shadow.rect.height-depth)<.0001f,
                "Actor shadow width and depth must match its rooted silhouette");
            Vector3 pivot=shadow.matrix.MultiplyPoint3x4(
                new Vector3(shadow.rect.x+shadow.rect.width*.5f,
                    shadow.rect.y+shadow.rect.height*.5f,0));
            Near(pivot,original.MultiplyPoint3x4(
                new Vector3(root.x,root.y,0)),
                "Contact must stay beneath the single GameCore foot");
            Check(shadow.color.a>.2f&&shadow.color.a<.6f,
                "Earth shadow is soft, restrained and transparent");
            Same(GUI.matrix,original,
                "Rooted shadow must restore viewport transform");
            Same(GUI.color,incoming,
                "Rooted shadow must restore GUI tint");
            n++;
        }
        Clear();
        Call(app,"DrawActorContactShadow",new Point(280,420),
            60f,15f,0,0f);
        Check(GUI.textureCalls.Count==0,
            "Invisible actors cannot leave a disconnected shadow ghost");
        Console.WriteLine("PASS "+n+
            " original soft grounded actor samples across NPC/hero/Boss sizes; no rejected contact sprite");
    }

    private static void CanonicalHeroFallbackRecoversAfterPartialArtReimport(
        FrontierRuntime app)
    {
        // The REAL world and inventory fallbacks use one accepted class-owned
        // 256px source. Simulate an Editor reimport with the offline Resources
        // override, not edits to any accepted on-disk hero artwork.
        app.BeginVisualReview(0,1);
        const string key="Hero_Vanguard_Idle_S";
        string path="Frontier/HeroAnimator/Canonical/"+key;
        var overrides=Resources.TextureOverrides;
        Texture2D previousOverride;
        bool had=overrides.TryGetValue(path,out previousOverride);
        var cache=Get<System.Collections.Generic.Dictionary<string,Texture2D>>(
            app,"fullBodyHeroSheetCache");
        var rechecks=Get<System.Collections.Generic.Dictionary<string,float>>(
            app,"fullBodyHeroRecheckAt");
        Texture2D old;
        bool cached=cache.TryGetValue(key,out old);
        float previousClock=Get<float>(app,"clock");
        var freshA=new Texture2D(256,256,TextureFormat.RGBA32,false){
            name="offline-approved-Vanguard-Idle-A"};
        var freshB=new Texture2D(256,256,TextureFormat.RGBA32,false){
            name="offline-approved-Vanguard-Idle-B"};
        var partial=new Texture2D(255,256,TextureFormat.RGBA32,false){
            name="offline-incomplete-Vanguard"};
        try
        {
            cache.Remove(key);rechecks.Remove(key);
            overrides[path]=freshA;
            Set(app,"clock",10000f);
            Clear();Call(app,"DrawPlayerPreview",0,370f,290f,104f);
            Check(GUI.textureCalls.Count==1&&
                object.ReferenceEquals(GUI.textureCalls[0].texture,freshA),
                "Equipped hero preview failed to use first published whole-body atlas");
            overrides[path]=freshB;
            Set(app,"clock",10001f);
            Clear();Call(app,"DrawPlayerPreview",0,370f,290f,104f);
            Check(GUI.textureCalls.Count==1&&
                object.ReferenceEquals(GUI.textureCalls[0].texture,freshA),
                "A hot import overwrote a live accepted Sprite mid-review");
            // A truncated SpriteImport may occur between two stable assets.
            // Keep last complete artwork without publishing a 255px torso.
            overrides[path]=partial;
            Set(app,"clock",10003f);
            Clear();Call(app,"DrawPlayerPreview",0,370f,290f,104f);
            Check(GUI.textureCalls.Count==1&&
                object.ReferenceEquals(GUI.textureCalls[0].texture,freshA),
                "Incomplete canonical art replaced last validated protagonist");
            overrides[path]=freshB;
            Set(app,"clock",10006f);
            Clear();Call(app,"DrawPlayerPreview",0,370f,290f,104f);
            Check(GUI.textureCalls.Count==1&&
                object.ReferenceEquals(GUI.textureCalls[0].texture,freshB)&&
                Math.Abs(GUI.textureCalls[0].rect.width-104f)<.001f,
                "Accepted updated class body never reached real inventory pipeline");
            // The whole-body GameView fallback shares the same atlas
            // publication point; an inventory preview alone is insufficient.
            app.Game.playerAction=null;app.Game.playerHitTime=0f;
            app.Game.dashTime=0f;app.Game.deathTime=0f;
            Clear();Call(app,"DrawWholeBodyHeroOnGUI",
                new Point(330,420),Color.white,false,0,2,false);
            Check(GUI.textureCalls.Count==1&&
                object.ReferenceEquals(GUI.textureCalls[0].texture,freshB)&&
                Math.Abs(GUI.textureCalls[0].rect.width-192f)<.001f,
                "GameView fallback and inventory displayed different hero generations");
            // A new save/review can restart the world clock from zero.
            // Refresh immediately, not after 10000 seconds of gameplay.
            overrides[path]=freshA;
            Set(app,"clock",0f);
            Clear();Call(app,"DrawPlayerPreview",0,370f,290f,104f);
            Check(GUI.textureCalls.Count==1&&
                object.ReferenceEquals(GUI.textureCalls[0].texture,freshA),
                "Newly loaded game held an old class body after clock rewind");
            Console.WriteLine("PASS whole-body hero production fallback hot-reimport: complete atlas publication, truncated-source hold, clock rewind, class identity and world/loadout parity");
        }
        finally
        {
            if(had)overrides[path]=previousOverride;
            else overrides.Remove(path);
            if(cached)cache[key]=old;
            else cache.Remove(key);
            rechecks.Remove(key);
            Set(app,"clock",previousClock);
            app.EndVisualReview();Clear();
        }
    }

    private static void EditableWorldVisualResourceDrivesLiveRender(FrontierRuntime app)
    {
        Prepare(app);
        // These parameters come from an actual Resources TextAsset, and
        // drive the same live DrawRootedPlant/DrawAuthoredWaterShimmer/
        // DrawWorldEmitter methods verified by the previous frame tests.
        var profile=typeof(FrontierRuntime).GetProperty(
            "CurrentWorldVisualLook",Private);
        Check(profile!=null,"World look profile must be loadable by game runtime");
        var current=profile.GetValue(app,null);
        string[] names={"treeSwayDegrees","palmSwayDegrees",
            "snowpineSwayDegrees","waterGlintAlpha","waterGlintRate",
            "shorelineWashAlpha","shorelineTravelPixels",
            "emberFlickerAlpha","emberLiftPixels",
            "relicShimmerAlpha","relicShimmerRate",
            "relicHaloRadius","relicHaloAlpha",
            "lampRadius","nightLightAlpha","dayLightAlpha",
            "npcTalkLeanDegrees","escortWalkLeanDegrees",
            "escortHitLeanDegrees","combatWarningStrokePixels",
            "projectileTrailFadeDistance","companionStrideDistanceFactor",
            "companionFootfallSeconds","companionFacingThreshold",
            "companionAttackPlantSeconds",
            "ambientMoteDayAlpha","ambientMoteNightAlpha",
            "snowMoteWindX","desertMoteWindX","coastMoteLift",
            "groundFoliageSwayDegrees",
            "hazardRingAlpha","hazardCountdownAlpha",
            "hitContactBlendFraction","slashBridgeFraction","impactFlourishAlpha",
            "deathPoseBlendFraction"};
        var authored=Resources.Load<TextAsset>("Frontier/Visual/WorldLook");
        Check(authored!=null&&!String.IsNullOrEmpty(authored.text),
            "Real Unity WorldLook TextAsset cannot be missing or replaced by defaults");
        for(int i=0;i<names.Length;i++)
        {
            var field=current.GetType().GetField(
                names[i],BindingFlags.Public|BindingFlags.Instance);
            string key=names[i];
            string line=Array.Find(authored.text.Split('\n'),item=>
                item.Trim().StartsWith(key+"=",StringComparison.Ordinal));
            Check(field!=null&&line!=null,
                "Unity editable world palette omitted visual parameter: "+key);
            float expected;
            Check(float.TryParse(line.Substring(line.IndexOf('=')+1).Trim(),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,out expected),
                "World palette contains invalid authored numeric color/motion value: "+key);
            Check(Math.Abs((float)field.GetValue(current)-expected)<.00001f,
                "Current runtime did not read an edited WorldLook value: "+key);
        }
        Console.WriteLine("PASS 37 source-controlled WorldLook values including bounded full-Sprite death handoff for all accepted actors, painted impacts, terrain, foliage, ambient motion, hit contact and combat FX");
    }

    // CI-executable resource integrity check, not merely an unused visual
    // review menu helper. Every source-controlled companion gait setting
    // must reach the ACTUAL Unity runtime profile from WorldLook.txt.
    public static void VerifyLiveVisualLookResource(FrontierRuntime app)
    {
        try { EditableWorldVisualResourceDrivesLiveRender(app); }
        finally { app.EndVisualReview(); Clear(); }
    }

    public static void Run(FrontierRuntime app,string folder)
    {
        int width=Screen.width,height=Screen.height;Matrix4x4 matrix=GUI.matrix;Color color=GUI.color;bool enabled=GUI.enabled;float dpi=GUI.pixelsPerPoint;Vector3 mouse=Input.mousePosition;
        try {TideVisualSurfaceConsistency();CoverOcclusionIncludesEveryLiveActor(app);WholeWorldPropsFoliageHasOneStableRoot(app);WorldCanopyPaintedRootsMatchActualTerrain(app);GroundFoliageKeepsSourcePixelsAndRoot(app);StaticSceneryPreservesOriginalSpriteWithoutGpuActorCapture(app);WorldLightsRemainAnchoredToDetailedProps(app);StoneRoadBordersPreserveRichPublishedPixels(app);ShorelineEdgesUsePublishedWaterPixels(app);ShorelineUsesOriginalWaterArtAndHonorsSettings(app);PaintedImpactFlourishRunsInWorld(app);SlashWholeEffectFramesHaveOneContactPivot(app,folder);AuthoredHitContactCrossfadePreservesWorldPivot(app);MagicImpactHasARealContactLightAndStableAnchor(app);CombatWarningUsesConnectedTrueRadiusGeometry(app);ProjectileTrailsGrowWithoutFramePopping(app);AttackWarningsRemainReadableThroughWorldCover(app);LiveHazardOutlinesStayAboveActorsAndClipAtView(app);AtmosphericMotesUseOriginalWorldAnchoredArt(app);DryShoalDetailsFollowActualTide(app);OrientedCoastlineRetainsApprovedWaterArt(app,folder);OriginalCampfireFlameGetsRootedEmbers(app,folder);AuthoredRelicGlintsStayOnActualDetailedWorldProps(app,folder);WaterKeepsItsHandPaintedTextureAcrossTides(app);RootedRescueSurvivorHasOneGroundedSprite(app);GroundedActorsHaveContinuousAcceptedSoftShadows(app);EditableWorldVisualResourceDrivesLiveRender(app);CanonicalHeroFallbackRecoversAfterPartialArtReimport(app);Hotbar(app,folder);Geometry(app,folder);WorldMapRasterUsesOneDrawAndUpdatesBuiltBridges(app);BridgeAndSealMinimapsMatchPhysicalCollision(app);CourierStatusIsVisibleEvenOutsideFarshore(app);CourierPackShowsRealPhysicalDamage(app);PublicRefugeChoicesHaveRealWorldVisualFeedback(app);CivicServiceOverlayFollowsRealHeroAndRevertsAfterFailure(app);ChapterTenVisualSitesAndSecurityHUD(app);ChapterElevenLiveValvePhaseAndFullHUD(app);RestoresState(app);Console.WriteLine("PASS Ready/cooldown/energy/casting/dodge/death/consumable/companion hotbar states at 1x and 2x DPI; enabled and disabled click dispatch; GUI state isolation");}
        finally {app.EndVisualReview();Screen.width=width;Screen.height=height;GUI.matrix=matrix;GUI.color=color;GUI.enabled=enabled;GUI.pixelsPerPoint=dpi;GUI.queuedClick=null;Input.mousePosition=mouse;Clear();Call(app,"Resize");}
    }
}
