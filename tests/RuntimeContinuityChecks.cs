// Exercises production Update and OnGUI with controllable input declarations.
// This validates code paths and recorded geometry, not native Unity playback.
using System;
using System.Collections.Generic;
using System.Reflection;
using Starfall;
using UnityEngine;
public static class RuntimeContinuityChecks
{
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static void Check(bool v,string message){if(!v)throw new Exception(message);}
    static object Call(object app,string method,params object[] args){return app.GetType().GetMethod(method,Private).Invoke(app,args);}
    static T Get<T>(object app,string field){return (T)app.GetType().GetField(field,Private).GetValue(app);}
    static void Set(object app,string field,object value){app.GetType().GetField(field,Private).SetValue(app,value);}
    static void ClearDraw(){GUI.commands.Clear();GUI.textureCalls.Clear();GUI.buttonCalls.Clear();}
    static void Capture(FrontierRuntime app,string folder,string name){ClearDraw();Call(app,"OnGUI");var textures=GUI.textureCalls.ToArray();var buttons=GUI.buttonCalls.ToArray();GUI.Export(folder,name);GUI.textureCalls.AddRange(textures);GUI.buttonCalls.AddRange(buttons);}
    static void Prepare(FrontierRuntime app,int weapon=0)
    {
        Input.Clear();Screen.width=1280;Screen.height=800;Time.unscaledDeltaTime=.02f;
        app.BeginVisualReview(weapon,1);app.VisualReviewPaused=false;
        var g=app.Game;g.enemies.Clear();g.save.companion=-1;g.save.reducedMotion=true;g.invulnerable=100;g.save.x=12;g.save.y=14;g.toastTime=0;
        Set(app,"camera",g.Position);Set(app,"controllerUsed",false);Set(app,"pointerSeen",false);Set(app,"combatHudTime",0f);Call(app,"Resize");
    }
    static void Mouse(FrontierRuntime app,Point world)
    {Point p=(Point)Call(app,"ScreenPoint",world);Input.mousePosition=new Vector3(p.x,Screen.height-p.y,0);}
    static void Update(FrontierRuntime app){Call(app,"Update");Input.keys.Clear();}
    static void InputDirections(FrontierRuntime app,string folder)
    {
        Point[] headings={new Point(1,0),new Point(-1,0),new Point(0,-1),new Point(0,1)};
        foreach(int weapon in new[]{0,1,2})for(int i=0;i<4;i++)
        {
            Prepare(app,weapon);Mouse(app,app.Game.Position+headings[i]*4);Input.keys.Add(KeyCode.Q);Update(app);
            Check(app.Game.playerAction!=null&&(app.Game.playerAction.direction-headings[i]).Length<.002f,"Q reads the actual world mouse aim in all four directions");
            for(int f=0;f<15;f++)Update(app);
            if(weapon==1)
            {
                ActorAction action=app.Game.playerAction;Point flight=(action.target.Value-GameCore.WeaponOrigin(app.Game.Position,1,action.direction)).Normalized;
                Check(app.Game.projectiles.Count==5&&(app.Game.projectiles[2].velocity.Normalized-flight).Length<.002f&&app.Game.projectiles.TrueForAll(p=>p.velocity.Normalized.x*flight.x+p.velocity.Normalized.y*flight.y>.954f),"Arrow fan center must reach the captured mouse target from its actual bow; all five shots share the intended cone");
            }
            Capture(app,folder,"qa-continuity-Q-"+weapon+"-"+i);
            Prepare(app,weapon);Mouse(app,app.Game.Position+headings[i]*4);Input.held.Add(KeyCode.J);Update(app);Input.held.Clear();
            Check((app.Game.playerAction.direction-headings[i]).Length<.002f,"Keyboard attack shares the cursor direction");
            Prepare(app,weapon);Mouse(app,app.Game.Position+headings[i]*4);Input.buttons.Add(0);Update(app);Input.buttons.Clear();
            Check((app.Game.playerAction.direction-headings[i]).Length<.002f,"Left click shares the same aim source");
        }
        Prepare(app,1);Mouse(app,app.Game.Position+new Point(0,-4));Update(app);
        Input.mousePosition=new Vector3(440,800-744,0);Set(app,"combatHudTime",3f);GUI.queuedClick=new Rect(430,732,78,50);Capture(app,folder,"review-continuity-hotbar-aim");
        Check(GUI.queuedClick==null&&app.Game.playerAction!=null&&app.Game.playerAction.direction.y<-.99f,"Clicking Q preserves the last world aim instead of aiming at the HUD");
        Prepare(app,1);Mouse(app,app.Game.Position+new Point(0,-4));app.Game.SetAim(new Point(1,0));Input.keys.Add(KeyCode.JoystickButton3);Update(app);
        Check(app.Game.playerAction.direction.x>.99f,"Controller Y retains controller facing rather than stale mouse aim");
        Prepare(app,1);Mouse(app,app.Game.Position+new Point(0,-4));Update(app);Input.axes["Horizontal"]=-1;Update(app);Input.keys.Add(KeyCode.JoystickButton3);Update(app);
        Check(Get<bool>(app,"controllerUsed")&&app.Game.playerAction.direction.x<-.99f,"Stick movement alone takes over facing from an idle mouse before Y");
        Prepare(app,1);Mouse(app,app.Game.Position+new Point(0,-4));Update(app);Set(app,"controllerUsed",true);app.Game.SetAim(new Point(1,0));Input.held.Add(KeyCode.J);Update(app);Input.held.Clear();
        Check(!Get<bool>(app,"controllerUsed")&&app.Game.playerAction.direction.y<-.99f,"Returning from controller to J immediately restores mouse aiming");
        Prepare(app,1);var foe=new Enemy(1222,0,17,14,999){stun=99};app.Game.enemies.Add(foe);Point body=(Point)Call(app,"ScreenPoint",foe.Position);Input.mousePosition=new Vector3(body.x,800-body.y+35,0);Input.buttons.Add(0);Update(app);
        Check(app.Game.playerAction.direction.x>.99f&&Math.Abs(app.Game.playerAction.direction.y)<.001f,"Clicking enemy body art aims at its real ground hurtbox rather than missing behind its head");
        Prepare(app,1);Mouse(app,app.Game.Position+new Point(0,-4));app.Game.mode="inventory";Input.keys.Add(KeyCode.Q);Update(app);
        Check(app.Game.playerAction==null,"A menu cannot dispatch a world technique");
        Console.WriteLine("PASS Runtime Q/J/mouse/controller aim, four directions per class, hotbar aim retention and menu input isolation (offline input)");
    }
    static void WorldAndAudio(FrontierRuntime app)
    {
        Prepare(app);var prop=new Decoration{kind="tree",x=10,y=10,variant=0};var rect=new Rect(100,200,100,116);var roots=new List<float>();var tops=new List<float>();
        foreach(float time in new[]{0f,.5f,2f})
        {
            app.Game.save.reducedMotion=false;Set(app,"clock",time);ClearDraw();Call(app,"DrawRootedPlant",prop,rect,Color.white);
            Check(GUI.textureCalls.Count==32,"Canopy motion uses a fixed-root segmented sprite");roots.Add(GUI.textureCalls[31].rect.x);tops.Add(GUI.textureCalls[0].rect.x);
            for(int strip=21;strip<32;strip++)Check(GUI.textureCalls[strip].rect.x==100,"Lower trunk and roots never translate in wind");
        }
        Check(roots[0]==roots[1]&&roots[1]==roots[2]&&Math.Abs(tops[0]-tops[1])+Math.Abs(tops[1]-tops[2])>.05f,"Leaves move while the tree remains attached to world ground");
        ClearDraw();Call(app,"DrawRootedPlant",new Decoration{kind="tree",x=11,y=10},rect,Color.white);Check(Math.Abs(GUI.textureCalls[0].rect.x-tops[2])>.01f,"Neighbouring trees have independent wind phase");
        Set(app,"camera",new Point(12.123f,14.123f));Point p=(Point)Call(app,"ScreenPoint",new Point(10,10)),q=(Point)Call(app,"ScreenPoint",new Point(11,10));Check(q.x-p.x==36&&q.y==p.y,"Tile and actor projection share a single whole-pixel grid");
        Set(app,"camera",new Point(12.1231f,14.1231f));q=(Point)Call(app,"ScreenPoint",new Point(10,10));Check((p-q).Length==0,"Subpixel camera drift cannot move stationary world tiles");
        Prepare(app);app.Game.save.reducedMotion=false;Set(app,"camera",app.Game.Position);Point still=Get<Point>(app,"camera");app.Game.SetAim(new Point(0,-1));Call(app,"FollowCamera",.02f);Check((Get<Point>(app,"camera")-still).Length==0,"Changing direction alone cannot snap the follow camera");
        app.Game.save.x+=1;Call(app,"FollowCamera",.02f);Point following=Get<Point>(app,"camera");Check(following.x>still.x&&following.x<still.x+.3f,"Review and gameplay use the same bounded damping");app.Game.mode="pause";Call(app,"FollowCamera",1f);Check((Get<Point>(app,"camera")-following).Length==0,"Opening a menu freezes world camera follow");
        Prepare(app);var audio=Get<FrontierAudio>(app,"audioSystem");var feet=Get<AudioSource>(audio,"footsteps");feet.heard.Clear();feet.gains.Clear();audio.Update(app.Game,false,.02f);
        for(int f=0;f<120;f++){app.Game.Tick(.02f,new Point(1,0));audio.Update(app.Game,true,.02f);}
        Check(feet.heard.Count==app.Game.footContacts&&feet.heard.Count>5,"Real foot contacts dispatch the actual footstep channel");
        Check(feet.volume<=app.Game.save.effectsVolume*.071f&&feet.pitch>=.94f&&feet.pitch<=1.061f&&feet.gains.TrueForAll(v=>v>=.8f&&v<=.951f),"Footsteps remain quiet and lightly varied");
        int count=feet.heard.Count;app.Game.mode="pause";for(int f=0;f<100;f++){app.Game.Tick(.02f,new Point(1,0));audio.Update(app.Game,true,.02f);}Check(feet.heard.Count==count&&feet.volume==0,"Pause cannot emit phantom contacts");
        app.Game.mode="world";app.Game.save.music=false;audio.Update(app.Game,true,.02f);Check(feet.volume==0,"Audio master also mutes footsteps");
        Prepare(app);ClearDraw();Call(app,"DrawProjectileVisual",new Projectile{position=app.Game.Position,velocity=new Point(12,0),friendly=true,visual="arrow"});Check(GUI.textureCalls.Count==1,"A newborn missile does not already have a full trail behind its bow");
        Console.WriteLine("PASS Fixed roots, independent canopy wind, common pixel grid, age-driven trails and contact-driven quiet footsteps (offline geometry/audio)");
    }

    // Actual Frontier.unity runtime now paints grounded contact sprites in
    // the world depth pass. Tests drive REAL GameCore walk distance/contacts,
    // never a mocked free-running animation or save file modification.
    static void GroundedMaterialFootfalls(FrontierRuntime app,string folder)
    {
        Prepare(app);
        var game=app.Game;
        game.save.effectsQuality=2;
        game.save.reducedMotion=false;
        Set(app,"camera",game.Position);
        Call(app,"AdvanceFootfallVisual",.02f);
        var field=app.GetType().GetField("footfallMarks",Private);
        Check(field!=null,"Material footfall scene producer missing");
        var marks=(System.Collections.IList)field.GetValue(app);
        Check(marks.Count==0,"Stationary actor generated phantom dust");
        int initialContacts=game.footContacts;
        for(int i=0;i<160;i++)
        {
            game.Tick(.02f,new Point(1,0));
            Call(app,"AdvanceFootfallVisual",.02f);
        }
        Check(game.footContacts>initialContacts&&marks.Count>0&&
              marks.Count<=12,
            "Movement-driven world-footprint budget or GameCore contacts broken");
        object drawn=marks[marks.Count-1];
        var paintType=drawn.GetType();
        var at=(Point)paintType.GetField("position",
            BindingFlags.Instance|BindingFlags.NonPublic).GetValue(drawn);
        var surface=(string)paintType.GetField("surface",
            BindingFlags.Instance|BindingFlags.NonPublic).GetValue(drawn);
        Check(surface=="grass"||surface=="stone"||surface=="sand"||
              surface=="snow"||surface=="wood",
            "Footsteps must use real terrain, not arbitrary visual variants");
        ClearDraw();
        Call(app,"DrawFootfallMark",drawn);
        var calls=GUI.textureCalls.ToArray();
        Check(calls.Length==1&&calls[0].texture.name==
            "resource:Frontier/Art/Effects"&&calls[0].color.a>.005f&&
            calls[0].color.a<=.45f,
            "Paint source must remain low-alpha ORIGINAL accepted Effects.png");
        float screenX=calls[0].rect.x, screenY=calls[0].rect.y;
        // Prove the real Frontier.unity depth-sorted production traversal
        // includes the SAME painted source cell. A direct DrawFootfallMark
        // preview alone would not establish that the actual game draws it.
        ClearDraw();
        Call(app,"OnGUI");
        int cell=(int)paintType.GetField("variant",
            BindingFlags.Instance|BindingFlags.NonPublic).GetValue(drawn);
        bool mirrored=(bool)paintType.GetField("alternate",
            BindingFlags.Instance|BindingFlags.NonPublic).GetValue(drawn);
        var worldContact=GUI.textureCalls.Find(c=>
            c.texture.name=="resource:Frontier/Art/Effects"&&
            Math.Abs(c.rect.width-(surface=="snow"?24f:26f))<.001f&&
            Math.Abs(c.rect.height-(surface=="snow"?18f:13f))<.001f&&
            Math.Abs(c.uv.x-(cell%4+(mirrored?1:0))/4f)<.00001f&&
            Math.Abs(c.uv.width-(mirrored?-.25f:.25f))<.00001f&&
            Math.Abs(c.uv.y-(1f-(cell/4+1)/4f))<.00001f);
        Check(worldContact!=null,
            "Accepted rooted footstep source omitted from real ordered world draw");
        GUI.Export(folder,"review-integrated-world-footfalls");
        Set(app,"camera",game.Position+new Point(1,0));
        ClearDraw();Call(app,"DrawFootfallMark",drawn);
        Check(GUI.textureCalls.Count==1&&
            Math.Abs(GUI.textureCalls[0].rect.x-screenX+36f)<.01f&&
            Math.Abs(GUI.textureCalls[0].rect.y-screenY)<.01f&&
            ((Point)paintType.GetField("position",
                BindingFlags.Instance|BindingFlags.NonPublic).GetValue(drawn)-at).Length<.001f,
            "Dust must remain planted in the world while camera moves");
        game.save.reducedMotion=true;
        Call(app,"AdvanceFootfallVisual",.02f);
        Check(marks.Count==0,"Reduced motion left fading ground ghosts behind");
        ClearDraw();Call(app,"DrawFootfallMark",drawn);
        Check(GUI.textureCalls.Count==0,
            "Accessibility mode must suppress contact motion completely");
        game.save.reducedMotion=false;
        game.save.effectsQuality=0;
        for(int i=0;i<80;i++)
        {
            game.Tick(.02f,new Point(1,0));
            Call(app,"AdvanceFootfallVisual",.02f);
        }
        Check(marks.Count==0,"Low-effects mode incorrectly allocates VFX");
        game.save.effectsQuality=2;
        game.mode="pause";
        Call(app,"AdvanceFootfallVisual",.02f);
        Check(marks.Count==0,"Paused menu must never retain dust sprites");
        Console.WriteLine("PASS GameCore-driven terrain foot contacts, source-illustrated world sprites, planted camera root, FX budget, reduced motion and pause");
    }


    static void NonheroGroundContactTracking(FrontierRuntime app)
    {
        Prepare(app);
        var game=app.Game;
        game.save.effectsQuality=2;
        game.save.reducedMotion=false;
        Call(app,"AdvanceFootfallVisual",.02f);
        var field=app.GetType().GetField("footfallMarks",Private);
        var marks=(System.Collections.IList)field.GetValue(app);
        // Companion movement is measured from its own world position;
        // merely toggling an Animator gait flag cannot generate dust.
        game.save.companion=0;
        game.companionPosition=new Point(12,14);
        game.companionMoving=true;
        Call(app,"AdvanceFootfallVisual",.02f);
        game.companionPosition=new Point(12.25f,14);
        Call(app,"AdvanceFootfallVisual",.02f);
        Check(marks.Count==0,"Companion foot animation spawned dust without a contact");
        game.companionPosition=new Point(12.53f,14);
        Call(app,"AdvanceFootfallVisual",.02f);
        Check(marks.Count==1,"Companion failed to plant a real traveled footstep");
        object buddy=marks[0];
        var type=buddy.GetType();
        var coord=type.GetField("position",Private);
        var strength=type.GetField("intensity",Private);
        Check(((Point)coord.GetValue(buddy)-game.companionPosition).Length<.001f&&
            Math.Abs((float)strength.GetValue(buddy)-.84f)<.001f,
            "Companion planted contact at protagonist instead of own boots");
        game.companionMoving=false;
        game.companionPosition=new Point(13.12f,14);
        Call(app,"AdvanceFootfallVisual",.02f);
        Check(marks.Count==1,
            "Stopped companion spawned a phantom gait/teleport footstep");
        // Newly spawned two enemies with identical numeric IDs must own
        // separate weak-keyed travel histories and real world anchors.
        var first=new Enemy(99955,0,14,14,100){moving=true};
        var second=new Enemy(99955,2,18,14,100){moving=true};
        game.enemies.Clear();
        game.enemies.Add(first);game.enemies.Add(second);
        Call(app,"AdvanceFootfallVisual",.02f);
        first.x+=.25f;second.x+=.24f;
        Call(app,"AdvanceFootfallVisual",.02f);
        Check(marks.Count==1,"Enemy pre-contact falsely displayed gait dust");
        first.x+=.24f;second.x+=.24f;
        Call(app,"AdvanceFootfallVisual",.02f);
        Check(marks.Count==3,
            "Different enemies with matching numeric ID shared contact history");
        int x0=0,x1=0;
        foreach(object mark in marks)
        {
            Point position=(Point)coord.GetValue(mark);
            if((position-first.Position).Length<.001f)x0++;
            if((position-second.Position).Length<.001f)x1++;
        }
        Check(x0==1&&x1==1,
            "Enemy marks must remain at individual source feet");
        first.state=1;first.x+=.52f;
        second.hp=0f;second.x+=.52f;
        Call(app,"AdvanceFootfallVisual",.02f);
        Check(marks.Count==3,
            "Boss windup or corpse generated extra decorative steps");
        game.save.reducedMotion=true;
        Call(app,"AdvanceFootfallVisual",.02f);
        Check(marks.Count==0,
            "Reduced motion must clear ALL enemy and companion contact FX");
        Console.WriteLine("PASS world-rooted moving companion and two same-ID enemies, no idle/windup/death ghosts, correct anchors and accessibility");
    }

    static void HudReadability(FrontierRuntime app,string folder)
    {
        Point[] pixels={new Point(220,100),new Point(1210,90),new Point(470,775),new Point(620,85),new Point(180,159)};
        for(int region=0;region<pixels.Length;region++)
        {
            Prepare(app);var g=app.Game;g.save.reducedMotion=false;g.toast="命中反馈：HUD 后方的敌人仍应可见。";g.toastTime=2;
            Point world=new Point(13,14);var foe=new Enemy(1200+region,0,world.x,world.y,100){timer=999,flash=.08f,hitTime=.15f};g.enemies.Add(foe);
            if(region==3)foe.boss=true;
            Point camera=new Point(world.x-(pixels[region].x-640)/36,world.y-(pixels[region].y-390)/36);Set(app,"camera",camera);Set(app,"combatHudTime",3f);
            Capture(app,folder,"review-continuity-hud-"+region);
            Check((bool)Call(app,"PointerOverInterface",new Point(region==2?450:region==1?1100:region==3?600:100,region==2?750:region==4?140:50))==(region==2),"Read-only HUD panels pass aiming through; real skill controls block accidental world clicks");
            Check((bool)Call(app,"PointerOverInterface",new Point(1200,66)),"The compact journal button remains a real protected control");
            Check(GUI.textureCalls.Exists(t=>(t.texture.name.StartsWith("outline_")||t.texture.name.Contains("/outline_"))&&t.color.a>.5f),"HUD overlap redraws only the real actor alpha outline");
            Check(!GUI.textureCalls.Exists(t=>t.texture.name.Contains("flash_outline")||t.texture.name.Contains("outline_outline")),"An overlapping hit flash cannot create a second CPU mask inside HUD redraw");
            Rect panel=region==2?new Rect(426,730,428,54):region==1?new Rect(1030,22,230,70):region==3?new Rect(380,22,520,65):region==4?new Rect(20,108,260,62):new Rect(20,20,260,77);
            Check(!(GUI.textureCalls.Exists(t=>t.rect.x==panel.x&&t.rect.y==panel.y&&t.rect.width==panel.width&&t.rect.height==panel.height&&t.color.a>.2f)),"A combat panel cannot hide a threat behind an opaque background");
            Check(!(bool)Call(app,"PointerOverInterface",new Point(1000,400)),"Collapsed combat sidebar returns its space to world interaction");
        }
        Prepare(app,2);Set(app,"combatHudTime",3f);Capture(app,folder,"review-continuity-combat-ready");
        Check(GUI.textureCalls.Exists(t=>t.texture.name=="resource:Frontier/Art/Icons"&&t.rect.y==736&&t.color.r>.99f),"Compact ready actions retain authored color");
        Console.WriteLine("PASS Combat HUD overlap for status, objective, hotbar, Boss bar and toast; threat silhouettes, transparent panels and shared pointer regions (offline GUI)");
    }
    static void TransitionTimeline(FrontierRuntime app,string folder)
    {
        for(int weapon=0;weapon<3;weapon++)
        {
            Prepare(app,weapon);Set(app,"combatHudTime",10f);app.Game.save.reducedMotion=false;app.Game.invulnerable=0;
            for(int frame=0;frame<64;frame++)
            {
                if(frame==10)app.Game.Attack(new Point(1,0));if(frame==32)app.Game.Skill(new Point(0,-1));
                Point motion=frame<32?new Point(1,0):new Point(0,-1);app.AdvanceVisualReview(.04f,motion);
                Capture(app,folder,"qa-continuity-motion-"+weapon+"-"+frame.ToString("D2"));
            }
            for(int direction=0;direction<4;direction++)
            {
                Prepare(app,weapon);Point d=new[]{new Point(1,0),new Point(-1,0),new Point(0,-1),new Point(0,1)}[direction];
                for(int step=0;step<20;step++)app.AdvanceVisualReview(.02f,d);
                Capture(app,folder,"qa-continuity-walk-"+weapon+"-"+direction);
            }
        }
        Console.WriteLine("PASS Recorded production Walk -> Attack -> Walk -> Q -> Walk timelines for all existing classes (offline renderer)");
    }
    static void CompleteBossFight(FrontierRuntime app,string folder)
    {
        app.BeginVisualReview(0,1,true);var g=app.Game;g.save.companion=-1;g.enemies.RemoveAll(e=>!e.boss);g.save.reducedMotion=false;g.save.difficulty=0;g.save.inventory[6]=3;
        Enemy boss=g.enemies.Find(e=>e.boss);Check(boss!=null,"Boss fight starts through the original altar");float health=boss.hp;int frames=0,captures=0;bool warning=false,contact=false;var durations=new List<int>();
        while(g.enemies.Contains(boss)&&frames++<6000)
        {
            Check(g.save.room==1&&!g.Dying,"The complete Boss fight does not secretly reset on player death");
            float distance=(boss.Position-g.Position).Length;Point movement=distance>1.4f?g.DirectionTo(g.Position,boss.Position):new Point();
            if(boss.state==1){warning=true;if((g.Position-boss.target).Length<3.4f){movement=(g.Position-boss.target).Length>.05f?(g.Position-boss.target).Normalized:new Point(-1,0);g.Dash(movement);}}
            app.AdvanceVisualReview(.02f,movement);g.Attack(boss.Position-g.Position);if(distance<3)g.Skill();if(g.save.health<g.save.maxHealth*.6f&&g.save.inventory[6]>0)g.Eat();
            contact|=boss.releaseTime>0;
            if(frames%8==0){Capture(app,folder,"qa-continuity-bossfight-"+(captures++).ToString("D3"));durations.Add(160);}
        }
        Check(frames<6000&&warning&&contact&&health>0&&g.Has("boss_1")&&g.fallen.Exists(f=>f.actor==boss),"One uninterrupted real Boss AI fight reaches victory and a retained death body");
        Capture(app,folder,"review-continuity-boss-victory");
        while(g.fallen.Exists(f=>f.actor==boss)){app.AdvanceVisualReview(.04f,new Point());if(captures<300){Capture(app,folder,"qa-continuity-bossfight-"+(captures++).ToString("D3"));durations.Add(40);}}
        System.IO.File.WriteAllText(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(folder),"continuity-boss-timeline.json"),"["+string.Join(",",durations)+"]");
        Check(g.CanStand(g.Position)&&g.projectiles.TrueForAll(p=>p.friendly),"Victory finishes death and hostile missile trails on valid ground");
        Console.WriteLine("PASS Complete forest Boss fight: "+frames+" simulation frames, "+captures+" recorded frames; original AI, warning, contact, dodge, recovery, victory and death (offline Core/GUI)");
    }
    static void DrawStateContinuity(FrontierRuntime app,string folder)
    {
        int overlayStates=0,actorStates=0;
        foreach(int weapon in new[]{0,1,2})foreach(Point direction in new[]{new Point(1,0),new Point(-1,0),new Point(0,-1),new Point(0,1)})for(int step=0;step<4;step++)
        {
            Prepare(app,weapon);var g=app.Game;g.invulnerable=0;g.save.upgrades=2;g.guardTime=1;g.movementDirection=direction*.1f;g.locomotionFacing=direction;g.walkDistance=GameCore.WalkStride*step/4;
            ClearDraw();Call(app,"DrawPlayerActor");
            Check(GUI.textureCalls.FindAll(c=>c.texture.name=="resource:Frontier/Art/Effects"&&c.rect.width==14).Count==1,"Every walk phase retains one enhancement marker");
            Check(GUI.textureCalls.FindAll(c=>c.texture==Texture2D.whiteTexture&&c.rect.width==4&&Math.Abs(c.color.a-.65f)<.001f).Count==20,"Guard protection remains visible while walking");
            Set(app,"drawingOutline",true);ClearDraw();Call(app,"DrawPlayerActor");Set(app,"drawingOutline",false);
            Check(!GUI.textureCalls.Exists(c=>c.texture.name=="resource:Frontier/Art/Effects"||c.texture==Texture2D.whiteTexture),"HUD silhouettes cannot duplicate enhancement effects, floor shadows or protection rings");overlayStates++;
        }
        for(int id=0;id<11;id++)foreach(bool left in new[]{false,true})for(int state=0;state<4;state++)
        {
            int room=id<3?1:id-2;Prepare(app);app.BeginVisualReview(0,room);var g=app.Game;g.enemies.Clear();g.save.companion=-1;
            var e=new Enemy(1500+id,id<3?id:0,g.Position.x,g.Position.y,999,id>=3);e.Face(new Point(left?-1:1,0));
            if(state==0){e.moving=true;e.moveTime=.6f;e.backpedaling=id==1;}
            if(state==1){e.state=1;e.timer=.3f;e.windupDuration=.65f;}
            if(state==2)e.releaseTime=.15f;if(state==3)e.hitTime=.15f;
            ClearDraw();Call(app,"DrawEnemyActor",e);Rect hud=(Rect)Call(app,"EnemyHudBounds",e);
            var sprite=GUI.textureCalls.Find(c=>c.texture.name.StartsWith("resource:Frontier/Art/")&&!c.texture.name.Contains("Effects"));
            Check(sprite!=null&&(sprite.uv.width<0)==left,"Alive direction is retained in the selected frame UV");
            int sourceLeft=(int)Math.Round(Math.Min(sprite.uv.x,sprite.uv.x+sprite.uv.width)*sprite.texture.width),sourceBottom=(int)Math.Round(sprite.uv.y*sprite.texture.height),cell=(int)Math.Round(Math.Abs(sprite.uv.width)*sprite.texture.width);
            int xMin=cell,yMin=cell,xMax=-1,yMax=-1;
            for(int y=0;y<cell;y++)for(int x=0;x<cell;x++)if(sprite.texture.pixels[(sourceBottom+y)*sprite.texture.width+sourceLeft+x].a>0){xMin=Math.Min(xMin,x);xMax=Math.Max(xMax,x);yMin=Math.Min(yMin,cell-1-y);yMax=Math.Max(yMax,cell-1-y);}
            float scale=sprite.rect.width/cell,visibleLeft=sprite.rect.x+(left?cell-xMax-1:xMin)*scale,visibleTop=sprite.rect.y+yMin*scale;
            Check(hud.x<=visibleLeft&&hud.y<=visibleTop&&hud.x+hud.width>=visibleLeft+(xMax-xMin+1)*scale&&hud.y+hud.height>=visibleTop+(yMax-yMin+1)*scale,"HUD protection includes every visible weapon/body pixel, both facings and all states");
            ClearDraw();Call(app,"DrawFallen",new FallenActor{actor=e,room=room,duration=1,life=.8f});
            var corpse=GUI.textureCalls.Find(c=>c.texture.name.StartsWith("resource:Frontier/Art/")&&!c.texture.name.Contains("Effects"));
            Check((corpse.uv.width<0)==left,"Death cannot mirror the actor to the opposite side");actorStates++;
        }
        Prepare(app);var foe=new Enemy(1600,0,13,14,100){state=1,timer=.3f,windupDuration=.65f};foe.Face(new Point(-1,0));foe.target=app.Game.Position;app.Game.enemies.Add(foe);Capture(app,folder,"review-continuity-melee-warning");
        ClearDraw();Call(app,"DrawEnemyWarning",foe);Check(GUI.textureCalls.FindAll(c=>c.rect.width==2).Count==33,"Claw wind-up renders a directional open sector rather than an unrelated full circle");
        foreach(Point direction in new[]{new Point(1,0),new Point(-1,0),new Point(0,-1),new Point(0,1)})
        {
            foe.kind=1;foe.Face(direction);foe.target=foe.Position+direction*4;ClearDraw();Call(app,"DrawEnemyWarning",foe);
            Point muzzle=(Point)Call(app,"ScreenPoint",GameCore.WeaponOrigin(foe.Position,1,direction)-new Point(0,GameCore.WeaponHeight(1,direction)/36));
            var first=GUI.textureCalls[0];Check(Math.Abs(first.rect.x+2-muzzle.x)<.001f&&Math.Abs(first.rect.y+2-muzzle.y)<.001f,"Enemy shot preview begins at the actual directional bow attachment");
        }
        Prepare(app);foe=new Enemy(1601,0,13,14,100){state=1,timer=.3f,windupDuration=.65f,target=new Point(13,15)};foe.Face(new Point(0,1));app.Game.enemies.Add(foe);Set(app,"camera",new Point(foe.x-(620-640)/36f,foe.y-(706-390)/36f));
        Rect hotbar=new Rect(426,730,428,54);Check((bool)Call(app,"ThreatOver",hotbar),"A melee sector alone can fade a HUD panel even when the actor is above it");ClearDraw();Call(app,"DrawHudThreatWitness");Check(GUI.textureCalls.FindAll(c=>c.rect.width==2).Count==33,"HUD witnesses preserve an overlapped melee warning as well as actors");
        Capture(app,folder,"review-continuity-warning-under-hud");
        Console.WriteLine("PASS "+overlayStates+" moving-buff states, "+actorStates+" pixel-bound/retained-death states and four bow warnings (offline production draw)");
    }
    static void StableSceneOrdering(FrontierRuntime app)
    {
        Prepare(app);for(int i=0;i<24;i++)app.Game.enemies.Add(new Enemy(1700+i,0,12+i*.13f,14,999){timer=999});
        object storage=app.GetType().GetField("worldDrawEntries",Private).GetValue(app);
        for(int pass=0;pass<8;pass++)
        {
            ClearDraw();Call(app,"DrawOrderedWorld");var actors=GUI.textureCalls.FindAll(c=>c.texture.name=="resource:Frontier/Art/Monsters");
            Check(actors.Count==24,"Dense scene retains every actor");for(int i=1;i<actors.Count;i++)Check(actors[i-1].rect.x<=actors[i].rect.x,"Equal-depth actors keep insertion order across repeated passes");
            Check(ReferenceEquals(storage,app.GetType().GetField("worldDrawEntries",Private).GetValue(app)),"Scene sort storage is reused rather than recreated for each GUI event");
        }
        object regions=Call(app,"HudRegions"),pointer=Call(app,"HudPointerRegions");
        for(int pass=0;pass<8;pass++){Check(ReferenceEquals(regions,Call(app,"HudRegions")),"HUD visibility regions reuse storage");Check(ReferenceEquals(pointer,Call(app,"HudPointerRegions")),"HUD pointer controls reuse separate storage");}
        Check(!(bool)Call(app,"PointerOverInterface",new Point(150,50))&&(bool)Call(app,"PointerOverInterface",new Point(450,750)),"Reused regions preserve transparent world aiming and real skill input protection");
        Console.WriteLine("PASS Repeated 24-actor equal-depth scenes: stable order, retained draw/region storage and correct pointer controls (offline renderer; no native frame-time claim)");
    }
    static void FullAuditPresentation(FrontierRuntime app,string folder)
    {
        Rect[] overlays={new Rect(24,228,298,90),new Rect(352,205,576,98),new Rect(414,111,452,56)};
        for(int i=0;i<overlays.Length;i++)
        {
            Prepare(app);var g=app.Game;Rect panel=overlays[i];var enemy=new Enemy(1850+i,0,13,14,999){state=1,timer=.3f,windupDuration=.65f,target=g.Position};g.enemies.Add(enemy);
            Set(app,"combatHudTime",3f);Set(app,"resumeTime",i==1?3f:0f);Set(app,"resumeBrief","回到祭坛，继续当前委托。");Set(app,"arrivalTime",i==2?2f:0f);
            Point pixel=new Point(panel.x+panel.width/2,panel.y+panel.height-10);Set(app,"camera",new Point(enemy.x-(pixel.x-640)/36,enemy.y-(pixel.y-390)/36));
            Capture(app,folder,"review-continuity-world-overlay-"+i);
            var regions=(List<Rect>)Call(app,"HudRegions");Check(regions.Exists(r=>r.x==panel.x&&r.y==panel.y&&r.width==panel.width&&r.height==panel.height),"World-stage text panels participate in HUD threat witnessing");
            Check(!GUI.textureCalls.Exists(c=>c.rect.x==panel.x&&c.rect.y==panel.y&&c.rect.width==panel.width&&c.rect.height==panel.height&&c.color.a>.2f),"Arrival, resume and enemy hint backgrounds cannot obscure a combat actor");
            Check(GUI.textureCalls.Exists(c=>c.texture.name.Contains("outline_Monsters")),"World-stage overlay redraws the actual enemy silhouette");
            Check(!(bool)Call(app,"PointerOverInterface",new Point(panel.x+20,panel.y+20)),"Read-only world messages permit aiming and attacking behind them");
        }
        Prepare(app);app.BeginVisualReview(0,0);app.Game.Close();app.Game.StartPractice();ClearDraw();Call(app,"OnGUI");
        Check(!(bool)Call(app,"PointerOverInterface",new Point(500,220)) && (bool)Call(app,"PointerOverInterface",new Point(820,260)),"Practice protects its actual exit button, not its entire instruction panel");
        Prepare(app);Set(app,"combatHudTime",3f);var hazard=new Hazard{position=new Point(13,14),radius=1.35f,timer=.6f,kind="tide",damage=17};app.Game.hazards.Add(hazard);
        Set(app,"camera",new Point(hazard.position.x-(620-640)/36f,hazard.position.y-(754-390)/36f));Capture(app,folder,"review-continuity-hazard-under-hud");
        ClearDraw();Call(app,"DrawHudThreatWitness");Check(GUI.textureCalls.FindAll(c=>c.texture==Texture2D.whiteTexture&&c.rect.width==4&&Math.Abs(c.color.a-.8f)<.001f).Count==48,"A regional hazard retains a complete visible footprint above the hotbar");
        Prepare(app);Texture2D effects=Get<Texture2D>(app,"effectSheet");Set(app,"effectSheet",null);
        try
        {
            var slash=new Effect("slash",app.Game.Position,.22f){direction=new Point(0,-1)};app.Game.SetAim(new Point(1,0));ClearDraw();Call(app,"DrawCombatEffect",slash);
            Point pixel=(Point)Call(app,"ScreenPoint",slash.position);Check(GUI.textureCalls.Count==18 && GUI.textureCalls.TrueForAll(c=>c.rect.y<pixel.y),"Fallback slash retains the captured north attack after live aim changes east");
            var impact=new Effect("danger",app.Game.Position,.45f){radius=1.35f};ClearDraw();Call(app,"DrawCombatEffect",impact);
            Check(Math.Abs(GUI.textureCalls[0].rect.x+2-pixel.x-impact.radius*36)<.001f,"Fallback impact uses the real footprint instead of a generic expansion");
        }
        finally{Set(app,"effectSheet",effects);}
        Color tint=new Color(.2f,.4f,.6f,.3f);GUI.color=tint;Call(app,"PropImage","house",0,100f,100f,170f,190f,(Color?)Color.white);Check(GUI.color.r==tint.r && GUI.color.a==tint.a,"A scenery draw cannot overwrite its parent's GUI tint/alpha");GUI.color=Color.white;
        int extendedClicks=0;
        for(int room=1;room<=8;room++)
        {
            Prepare(app);app.BeginVisualReview(0,room);var g=app.Game;g.enemies.Clear();var enemy=new Enemy(1860+room,0,g.Position.x+2,g.Position.y,999,true){state=1,timer=.4f,windupDuration=1};g.enemies.Add(enemy);ClearDraw();Call(app,"DrawEnemyActor",enemy);
            var sprite=GUI.textureCalls.Find(c=>c.texture.name=="resource:Frontier/Art/Bosses");Rect old=(Rect)Call(app,"ActorBounds",enemy.Position,true);bool checkedClick=false;
            int left=(int)Math.Round(sprite.uv.x*sprite.texture.width),bottom=(int)Math.Round(sprite.uv.y*sprite.texture.height),cell=(int)Math.Round(sprite.uv.width*sprite.texture.width);
            for(int y=0;y<cell&&!checkedClick;y++)for(int x=0;x<cell&&!checkedClick;x++)
            {
                if(sprite.texture.pixels[(bottom+cell-1-y)*sprite.texture.width+left+x].a<=.1f)continue;
                Point click=new Point(sprite.rect.x+(x+.5f)*sprite.rect.width/cell,sprite.rect.y+(y+.5f)*sprite.rect.height/cell);
                if(click.x>=old.x&&click.x<old.x+old.width&&click.y>=old.y&&click.y<old.y+old.height)continue;
                Point aimed=(Point)Call(app,"AimTargetAtPointer",click);Check((aimed-enemy.Position).Length<.001f,"Clicking an extended actual Boss weapon maps to its real hurtbox instead of empty background");checkedClick=true;extendedClicks++;
            }
        }
        Check(extendedClicks>0,"The test includes actual authored pixels outside the former fixed Boss rectangle");
        Console.WriteLine("PASS Full audit: 3 world overlays, practice input, regional HUD warnings, fallback direction/radius, nested GUI state and "+extendedClicks+" extended Boss click footprints (offline production GUI)");
    }
    static int NavigationRouteDots(float size)
    {
        Color expected = PixelArt.C("#e8bd62");
        int count = 0;
        foreach (var call in GUI.textureCalls)
            if (call.texture == Texture2D.whiteTexture &&
                Math.Abs(call.rect.width - size) < .001f &&
                Math.Abs(call.rect.height - size) < .001f &&
                Math.Abs(call.color.r - expected.r) < .001f &&
                Math.Abs(call.color.g - expected.g) < .001f &&
                Math.Abs(call.color.b - expected.b) < .001f) count++;
        return count;
    }
    static int NavigationEdgeArrowPixels()
    {
        Color expected = PixelArt.C("#f2d48b");
        int count = 0;
        foreach (var call in GUI.textureCalls)
            if (call.texture == Texture2D.whiteTexture &&
                Math.Abs(call.rect.width - 3f) < .001f &&
                Math.Abs(call.rect.height - 3f) < .001f &&
                Math.Abs(call.color.r - expected.r) < .001f &&
                Math.Abs(call.color.g - expected.g) < .001f &&
                Math.Abs(call.color.b - expected.b) < .001f) count++;
        return count;
    }
    static bool NavigationEdgeArrowStaysInPlayableView(float bottom)
    {
        Color expected = PixelArt.C("#f2d48b");
        foreach (var call in GUI.textureCalls)
        {
            if (call.texture != Texture2D.whiteTexture ||
                Math.Abs(call.rect.width - 3f) >= .001f ||
                Math.Abs(call.rect.height - 3f) >= .001f ||
                Math.Abs(call.color.r - expected.r) >= .001f ||
                Math.Abs(call.color.g - expected.g) >= .001f ||
                Math.Abs(call.color.b - expected.b) >= .001f) continue;
            if (call.rect.x < 350 || call.rect.x + call.rect.width > 970 ||
                call.rect.y < 125 || call.rect.y + call.rect.height > bottom) return false;
        }
        return true;
    }
    static bool NavigationEdgeArrowNearLeft()
    {
        Color expected = PixelArt.C("#f2d48b");
        int count = 0;
        foreach (var call in GUI.textureCalls)
        {
            if (call.texture != Texture2D.whiteTexture ||
                Math.Abs(call.rect.width - 3f) >= .001f ||
                Math.Abs(call.rect.height - 3f) >= .001f ||
                Math.Abs(call.color.r - expected.r) >= .001f ||
                Math.Abs(call.color.g - expected.g) >= .001f ||
                Math.Abs(call.color.b - expected.b) >= .001f) continue;
            count++;
            if (call.rect.x + call.rect.width > 400) return false;
        }
        return count == 4;
    }
    static int NavigationBlockedPixels()
    {
        Color expected = PixelArt.C("#ff6961");
        int count = 0;
        foreach (var call in GUI.textureCalls)
            if (call.texture == Texture2D.whiteTexture &&
                Math.Abs(call.rect.width - 3f) < .001f &&
                Math.Abs(call.rect.height - 3f) < .001f &&
                Math.Abs(call.color.r - expected.r) < .001f &&
                Math.Abs(call.color.g - expected.g) < .001f &&
                Math.Abs(call.color.b - expected.b) < .001f) count++;
        return count;
    }
    static void NavigationRouteGuidance(FrontierRuntime app)
    {
        Prepare(app, 0);
        GameCore game = app.Game;
        game.save.room = 1; game.save.x = 12; game.save.y = 14;
        game.save.trackedQuest = "main"; game.save.showGuidance = true;
        game.enemies.Clear(); Set(app, "camera", game.Position);
        Point? goal = game.NavigationTarget();
        Check(goal.HasValue, "The active story has a real in-world navigation target");
        List<Point> route = game.NavigationPath(goal.Value);
        Check(route.Count > 0, "The active story target has a collision-valid walking route");

        int width = game.Map.rows[0].Length, height = game.Map.rows.Length;
        int left = Math.Max(0, Math.Min(width - 24, (int)game.save.x - 12));
        int top = Math.Max(0, Math.Min(height - 15, (int)game.save.y - 7));
        Point visible = new Point(); bool hasVisible = false;
        foreach (Point point in route)
        {
            int x = (int)Math.Floor(point.x), y = (int)Math.Floor(point.y);
            if (x >= left && x < left + 24 && y >= top && y < top + 15)
            { visible = point; hasVisible = true; break; }
        }
        Check(hasVisible, "The reachable route intersects the live minimap viewport");

        ClearDraw(); Call(app, "MiniTerrainWindow", 1024f, 42f);
        int minimapDots = NavigationRouteDots(3f);
        Check(minimapDots > 0 && minimapDots <= route.Count,
            "The HUD minimap draws a bounded gold breadcrumb for actual path tiles");
        Color routeColor = PixelArt.C("#e8bd62");
        float dotX = 1024f + (visible.x - left) * 9f - 1.5f;
        float dotY = 42f + (visible.y - top) * 9f - 1.5f;
        bool exactStep = false;
        foreach (var call in GUI.textureCalls)
            if (call.texture == Texture2D.whiteTexture &&
                Math.Abs(call.rect.x - dotX) < .001f &&
                Math.Abs(call.rect.y - dotY) < .001f &&
                Math.Abs(call.rect.width - 3f) < .001f &&
                Math.Abs(call.color.r - routeColor.r) < .001f &&
                Math.Abs(call.color.g - routeColor.g) < .001f &&
                Math.Abs(call.color.b - routeColor.b) < .001f)
            { exactStep = true; break; }
        Check(exactStep, "The drawn breadcrumb matches a real walkable tile from NavigationPath");

        game.save.showGuidance = false;
        ClearDraw(); Call(app, "MiniTerrainWindow", 1024f, 42f);
        Check(NavigationRouteDots(3f) == 0,
            "Disabling route guidance removes breadcrumbs without hiding terrain");
        game.save.showGuidance = true;
        ClearDraw(); Call(app, "MiniTerrain", 888f, 264f, 2f);
        Check(NavigationRouteDots(1.5f) > 0,
            "The full local map also draws the same route at its own tile scale");

        List<Point> cachedRoute = Get<List<Point>>(app, "navigationRouteCache");
        int revisionBeforeIdleTick = game.revision;
        game.Tick(.05f, new Point());
        Check(game.revision != revisionBeforeIdleTick,
            "The test advanced a real world simulation tick");
        Call(app, "NavigationRoute");
        Check(object.ReferenceEquals(cachedRoute, Get<List<Point>>(app, "navigationRouteCache")),
            "The route cache survives ordinary simulation ticks without another BFS rebuild");

        string bridgeFlag = "far_bridge_" + game.save.room;
        game.save.flags.RemoveAll(flag => flag == bridgeFlag);
        Call(app, "NavigationRoute");
        List<Point> sealedRoute = Get<List<Point>>(app, "navigationRouteCache");
        Call(game, "Flag", bridgeFlag);
        Call(app, "NavigationRoute");
        Check(!object.ReferenceEquals(sealedRoute, Get<List<Point>>(app, "navigationRouteCache")),
            "A newly opened collision bridge invalidates the route cache immediately");

        game.save.x = 12; game.save.y = 14; game.mode = "world";
        Set(app, "camera", game.Position);
        ClearDraw(); Call(app, "DrawNavigationTargetMarker");
        Check(NavigationRouteDots(4f) == 12,
            "A visible tracked objective gets one compact twelve-point world-space marker");
        game.save.showGuidance = false;
        ClearDraw(); Call(app, "DrawNavigationTargetMarker");
        Check(NavigationRouteDots(4f) == 0,
            "The world marker obeys the existing route-guidance toggle");
        game.save.showGuidance = true;
        Point[] offscreenStarts = {
            new Point(80, 14), new Point(-40, 14),
            new Point(12, 80), new Point(12, -40)
        };
        string[] edgeNames = { "left", "right", "top", "bottom" };
        for (int i = 0; i < offscreenStarts.Length; i++)
        {
            game.save.x = offscreenStarts[i].x; game.save.y = offscreenStarts[i].y;
            Set(app, "camera", game.Position);
            ClearDraw(); Call(app, "DrawNavigationTargetMarker");
            Check(NavigationRouteDots(4f) == 0,
                "Off-screen " + edgeNames[i] + " objective does not draw a misleading on-world ring");
            Check(NavigationEdgeArrowPixels() == 4 &&
                NavigationEdgeArrowStaysInPlayableView(630f),
                "Off-screen " + edgeNames[i] + " objective receives a four-pixel directional cue outside HUD panels");
        }
        Point? bossBarTarget = game.NavigationTarget();
        Check(bossBarTarget.HasValue, "Lower-playfield fixture has a real objective");
        game.save.x = bossBarTarget.Value.x;
        game.save.y = bossBarTarget.Value.y - 7.2f;
        Set(app, "camera", game.Position);
        ClearDraw(); Call(app, "DrawNavigationTargetMarker");
        Check(NavigationRouteDots(4f) == 12 && NavigationEdgeArrowPixels() == 0,
            "A lower on-screen objective keeps its ring when no Boss HUD occupies that space");
        game.enemies.Add(new Enemy(1222, 0, 17, 14, 999) { boss = true });
        ClearDraw(); Call(app, "DrawNavigationTargetMarker");
        Check(NavigationRouteDots(4f) == 0 && NavigationEdgeArrowPixels() == 4 &&
            NavigationEdgeArrowStaysInPlayableView(600f),
            "A target behind the centered Boss bar is rerouted to a visible safe-edge cue");
        game.enemies.Clear();

        Point? bearingTarget = game.NavigationTarget();
        Check(bearingTarget.HasValue, "Clamped-camera bearing fixture has a real objective");
        game.save.x = bearingTarget.Value.x - 2f;
        game.save.y = bearingTarget.Value.y;
        Set(app, "camera", new Point(game.save.x + 16f, game.save.y));
        ClearDraw(); Call(app, "DrawNavigationTargetMarker");
        Check(NavigationEdgeArrowNearLeft(),
            "A target beyond the left screen edge still points left when the camera is clamped away from the hero");

        game.mode = "inventory";
        ClearDraw(); Call(app, "DrawNavigationTargetMarker");
        Check(NavigationEdgeArrowPixels() == 0,
            "World edge cues never leak into inventory overlays");
        game.mode = "world";

        Point next = route[0]; game.save.x = next.x; game.save.y = next.y;
        ClearDraw(); Call(app, "MiniTerrainWindow", 1024f, 42f);
        Check(Get<int>(app, "navigationRouteStartX") == (int)game.save.x &&
            Get<int>(app, "navigationRouteStartY") == (int)game.save.y,
            "The cached route is rebuilt when the player enters the next tile");

        // Enclose the real tracked objective behind a five-tile collision
        // pocket. An empty path must be communicated as blocked, not rendered
        // as a successful gold objective. Restore the actual campaign map even
        // when an assertion fails; this is a temporary fixture, not world art.
        MapData blockedMap = game.Map;
        string[] originalRows = blockedMap.rows;
        float originalX = game.save.x, originalY = game.save.y;
        try
        {
            Point? unreachable = game.NavigationTarget();
            Check(unreachable.HasValue, "The blocked-route fixture starts from a real tracked objective");
            int targetX = (int)Math.Floor(unreachable.Value.x);
            int targetY = (int)Math.Floor(unreachable.Value.y);
            int mapWidth = originalRows[0].Length, mapHeight = originalRows.Length;
            Check(targetX >= 5 && targetY >= 5 && targetX < mapWidth - 5 &&
                targetY < mapHeight - 5,
                "The real objective leaves enough map space for an isolated collision fixture");
            Point farStart = game.Position;
            float farthest = -1f;
            for (int y = 3; y < mapHeight - 3; y += 2)
                for (int x = 3; x < mapWidth - 3; x += 2)
                {
                    Point candidate = new Point(x + .5f, y + .5f);
                    float distance = (candidate - unreachable.Value).Length;
                    if (distance > farthest && distance > 6f && game.CanStand(candidate))
                    { farthest = distance; farStart = candidate; }
                }
            Check(farthest > 6f,
                "The collision fixture can keep the player safely outside the blocked pocket");
            game.save.x = farStart.x; game.save.y = farStart.y;
            string[] blockedRows = (string[])originalRows.Clone();
            for (int y = targetY - 2; y <= targetY + 2; y++)
            {
                char[] row = blockedRows[y].ToCharArray();
                for (int x = targetX - 2; x <= targetX + 2; x++) row[x] = '#';
                blockedRows[y] = new string(row);
            }
            blockedMap.rows = blockedRows;
            Set(app, "camera", unreachable.Value);
            List<Point> unreachableRoute = (List<Point>)Call(app, "NavigationRoute");
            Check(unreachableRoute.Count == 0,
                "A complete collision pocket produces no fabricated walking path");
            ClearDraw(); Call(app, "DrawNavigationTargetMarker");
            Check(NavigationBlockedPixels() == 5 && NavigationRouteDots(4f) == 0,
                "An unreachable visible objective gets a coral broken-route mark instead of the gold ring");

            Set(app, "camera", new Point(unreachable.Value.x + 16f, unreachable.Value.y));
            ClearDraw(); Call(app, "DrawNavigationTargetMarker");
            Check(NavigationBlockedPixels() == 9 && NavigationEdgeArrowPixels() == 0 &&
                NavigationRouteDots(4f) == 0,
                "An unreachable off-screen objective keeps directional bearing but marks the edge cue blocked");
        }
        finally
        {
            blockedMap.rows = originalRows;
            game.save.x = originalX; game.save.y = originalY;
            Set(app, "camera", game.Position);
            Call(app, "NavigationRoute");
        }
        Console.WriteLine("PASS tracked-quest routes, blocked-objective cues, HUD/local maps, guidance toggle and route cache invalidation");
    }
    public static void Run(FrontierRuntime app,string folder)
    {
        try{InputDirections(app,folder);WorldAndAudio(app);GroundedMaterialFootfalls(app,folder);NonheroGroundContactTracking(app);HudReadability(app,folder);NavigationRouteGuidance(app);DrawStateContinuity(app,folder);StableSceneOrdering(app);FullAuditPresentation(app,folder);TransitionTimeline(app,folder);CompleteBossFight(app,folder);}
        finally{app.EndVisualReview();Input.Clear();GUI.queuedClick=null;Screen.width=1280;Screen.height=800;Time.unscaledDeltaTime=.02f;ClearDraw();Call(app,"Resize");}
    }
}
