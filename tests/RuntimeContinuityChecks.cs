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

    static void CombatMusicReversesWhenActualThreatLeaves()
    {
        // Drive the PRODUCTION audio mixer against live AudioSource stubs:
        // entering combat then leaving during the original fade must NOT
        // finish playing combat music after the threat is gone.
        var g=new GameCore(CampaignFixture.Create());
        g.NewGame(0);g.Close();g.mode="world";g.enemies.Clear();
        g.save.music=true;g.save.room=0;
        using(var audio=new FrontierAudio(new GameObject("combat-audio-transition")))
        {
            audio.Update(g,false,1.6f);
            Check(Get<int>(audio,"theme")==0&&Get<int>(audio,"pending")==-1,
                "A settled town song has an actual active audio channel");
            AudioSource[] sources=Get<AudioSource[]>(audio,"music");
            int original=Get<int>(audio,"active");
            AudioSource town=sources[original],battle=sources[1-original];
            var threat=new Enemy(70921,1,g.Position.x+2,g.Position.y,100,true);
            g.enemies.Add(threat);
            audio.Update(g,false,.6f);
            float partial=battle.volume;
            Check(Get<int>(audio,"pending")==6&&partial>0&&
                partial<g.save.musicVolume*.28f&&battle.isPlaying,
                "A genuine nearby boss begins the in-flight combat crossfade");
            g.enemies.Remove(threat);
            audio.Update(g,false,.2f);
            Check(Get<int>(audio,"pending")==6&&battle.volume<partial&&
                town.volume>g.save.musicVolume*.28f-partial,
                "Leaving real combat reverses the partial fade instead of continuing the wrong music");
            audio.Update(g,false,.7f);
            Check(Get<int>(audio,"pending")==-1&&Get<int>(audio,"theme")==0&&
                Get<int>(audio,"active")==original&&!battle.isPlaying&&
                battle.volume==0&&town.isPlaying,
                "A vanished threat restores the same uninterrupted town channel, not a delayed combat track");
            // A fresh real battle must still be able to finish its transfer.
            g.enemies.Add(threat);
            audio.Update(g,false,.6f);
            g.enemies.Remove(threat);
            audio.Update(g,false,.2f);
            g.enemies.Add(threat);
            audio.Update(g,false,.35f);
            Check(Get<int>(audio,"pending")==6&&battle.isPlaying&&
                battle.volume>0&&battle.volume<g.save.musicVolume*.28f,
                "If combat returns during reversal, the existing battle channel resumes smoothly");
            audio.Update(g,false,2f);
            Check(Get<int>(audio,"theme")==6&&Get<int>(audio,"pending")==-1&&
                Get<int>(audio,"active")==1-original&&
                !town.isPlaying&&battle.isPlaying,
                "A persistent real battle completes the original audio source handoff once");
        }
        Console.WriteLine("PASS In-flight combat music reverses on genuine threat exit and resumes on re-entry (offline audio sources)");
    }

    // Registered in RuntimeCombatInputChecks.Main: its explicit case roster
    // is the CI entry point (the optional visual Run() is not invoked there).
    public static void VerifyCombatMusicReversesOnThreatDeparture(FrontierRuntime app)
    {
        CombatMusicReversesWhenActualThreatLeaves();
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
    static bool NavigationBlockedCueStaysInPlayableView(float bottom)
    {
        Color expected = PixelArt.C("#ff6961");
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
            if (call.rect.x < 350 || call.rect.x + call.rect.width > 970 ||
                call.rect.y < 125 || call.rect.y + call.rect.height > bottom) return false;
        }
        // The blocked state renders four directional arrow pixels plus the
        // five-pixel broken-route glyph; all nine must remain in the safe view.
        return count == 9;
    }
    public static void OptedInRemoteRouteRendersWithGeneralGuidanceOff(FrontierRuntime app)
    {
        Prepare(app);
        var g=app.Game;
        g.save.flags.Add("intro");g.save.flags.Add("scout_saved");
        g.save.stage=4;g.Enter(9);g.enemies.Clear();
        g.save.english=true;g.save.showGuidance=false;g.mode="world";
        Check(!g.PlayerRequestedNavigationActive,
            "Turning off generic guidance alone must not create player-owned navigation");
        ClearDraw();Call(app,"MiniTerrainWindow",1024f,42f);
        Check(NavigationRouteDots(3f)==0,
            "Ordinary story route stays hidden when general guidance is off");
        g.TrackQuest("trail_circuits");g.mode="journal";
        Check(g.GetQuest("trail_circuits").room!=g.save.room&&
            g.PlaceQuestApproachWaymark("trail_circuits"),
            "The original regional survey offers a real accepted remote journal route");
        g.mode="world";
        Check(g.PlayerRequestedNavigationActive&&
            !g.PersonalNavigationActive&&g.QuestApproachTravelHandoffPending&&
            !g.save.showGuidance,
            "An approved distant route exists before any local pin even with general guidance off");
        Point? target=g.NavigationTarget();
        Check(target.HasValue,"An actual map portal is the accepted quest's present bearing");
        // Inn spawn can be already inside the portal's interaction reach:
        // an empty path then means "arrived", not a blocked crossing.
        // Position the actual player at a separately standable tile with a
        // verified longer walking route before checking breadcrumbs.
        bool distantFoot=false;
        for(int y=2;y<g.Map.rows.Length-2&&!distantFoot;y+=2)
        for(int x=2;x<g.Map.rows[0].Length-2&&!distantFoot;x+=2)
        {
            Point p=new Point(x+.5f,y+.5f);
            if(!g.CanStand(p))continue;
            var walk=g.NavigationPathFrom(p,target.Value,true);
            if(walk.Count<=3)continue;
            g.save.x=p.x;g.save.y=p.y;distantFoot=true;
        }
        Check(distantFoot,"An accessible real Inn path extends beyond immediate portal interaction reach");
        var physical=g.NavigationPath(target.Value);
        Check(physical.Count>0,"A true longer Inn route reaches the exit interaction side");
        ClearDraw();Call(app,"MiniTerrainWindow",1024f,42f);
        Check(NavigationRouteDots(3f)>0,
            "Live minimap draws actual walking breadcrumbs for the approved remote route");
        ClearDraw();Call(app,"MiniTerrain",888f,264f,2f);
        Check(NavigationRouteDots(1.5f)>0,
            "Local full map also draws approved remote route independently of generic guidance");
        Set(app,"camera",target.Value);
        ClearDraw();Call(app,"DrawNavigationTargetMarker");
        Check(NavigationRouteDots(4f)==12||NavigationEdgeArrowPixels()==4,
            "World renderer draws real opted-in objective marker even while general guidance stays OFF");
        Check(g.ClearLocalWaymark()&&!g.PlayerRequestedNavigationActive,
            "Canceling remote auto-follow restores ordinary guidance preference");
        ClearDraw();Call(app,"MiniTerrainWindow",1024f,42f);
        Check(NavigationRouteDots(3f)==0&&!g.HasLocalWaymark&&
            g.save.trackedQuest=="trail_circuits"&&!g.save.showGuidance&&
            g.save.version==2,
            "Cancellation hides the route without changing tracking, preferences or SaveData v2");
        g.Close();
        Console.WriteLine("PASS Explicit distant quest route renders genuine HUD/full-map/world navigation with general guidance disabled");
    }

    public static void RemoteJourneyDoesNotPresentSealedPortalAsOpenRoute(FrontierRuntime app)
    {
        Prepare(app);
        var g=app.Game;
        g.save.flags.Add("intro");g.save.flags.Add("scout_saved");
        g.save.stage=4;g.Enter(9);g.enemies.Clear();
        g.save.english=true;
        WorldObject portal=null;
        foreach(var actor in g.Map.objects)
            if(actor.kind=="portal0"){portal=actor;break;}
        Check(portal!=null,"Actual Inn map owns the outbound regional portal");
        Point previous=new Point(portal.x,portal.y);
        string[] savedRows=(string[])g.Map.rows.Clone();
        try
        {
            Point center=new Point();
            bool found=false;
            for(int y=5;y<g.Map.rows.Length-5&&!found;y++)
            for(int x=5;x<g.Map.rows[0].Length-5&&!found;x++)
            {
                Point p=new Point(x+.5f,y+.5f);
                if(!g.CanStand(p)||(p-g.Position).Length<8f)continue;
                var full=g.NavigationPathFrom(g.Position,p,true);
                if(full.Count<=5)continue;
                center=p;found=true;
            }
            Check(found,"The shipped Inn offers a genuinely reachable distant interior test site");
            portal.x=center.x;portal.y=center.y;
            // Build a fully closed five-by-five tile ring around the
            // relocated ORIGINAL portal, keeping the center standable.
            int cx=(int)center.x,cy=(int)center.y;
            for(int y=cy-2;y<=cy+2;y++)
            {
                char[] cells=g.Map.rows[y].ToCharArray();
                for(int x=cx-2;x<=cx+2;x++)
                    if(x==cx-2||x==cx+2||y==cy-2||y==cy+2)
                        cells[x]='#';
                g.Map.rows[y]=new string(cells);
            }
            g.TrackQuest("trail_circuits");
            g.save.showGuidance=true;g.mode="world";
            Point? goal=g.NavigationTarget();
            Check(goal.HasValue&&(goal.Value-center).Length<.001f,
                "The original authored quest uses the relocated original portal");
            Check(g.NavigationPathFrom(g.Position,center,true).Count==0,
                "The sealed original portal has no complete physical walking approach");
            var preview=(List<Point>)Call(app,"NavigationRoute");
            Check(preview.Count==g.NavigationPath(center).Count&&
                !Get<bool>(app,"navigationRouteStrictQuestFinish"),
                "Generic optional guidance retains its original best-effort policy, including valid empty fallbacks");
            g.mode="journal";g.save.showGuidance=false;
            Check(g.PlaceQuestApproachWaymark("trail_circuits"),
                "Player explicitly accepts the same genuine remote quest");
            g.mode="world";
            var strict=(List<Point>)Call(app,"NavigationRoute");
            Check(strict.Count==0&&g.PlayerRequestedNavigationActive&&
                Get<bool>(app,"navigationRouteStrictQuestFinish"),
                "Player-approved navigation switches the live cache to strict physical finish and rejects sealed entry");
            Check((bool)Call(app,"HasBlockedNavigationRoute",center),
                "A sealed player-approved portal is an actual blocked route, never an arrived route");
            // Actual practice combat owns the world compass during an
            // encounter. Even if its target happens to occupy the same
            // world coordinate as the exit, that target cannot inherit the
            // player quest's route policy. Use a separate physical target
            // to exercise the renderer's owner boundary.
            g.practicing=true;g.enemies.Add(new Enemy(9127,0,center.x+.5f,center.y,200));
            Point? combatGoal=g.NavigationTarget();
            Check(combatGoal.HasValue&&
                (combatGoal.Value-center).Length>.1f&&
                !g.PlayerRequestedRemoteExitTarget(combatGoal.Value),
                "Real practice combat overrides the pending quest's portal target");
            var combatPath=(List<Point>)Call(app,"NavigationRoute");
            Check(!Get<bool>(app,"navigationRouteStrictQuestFinish")&&
                combatPath.Count==g.NavigationPath(combatGoal.Value).Count,
                "Combat targets retain their original best-effort navigation instead of an unrelated strict portal search");
            g.practicing=false;g.enemies.Clear();
            var restoredPolicy=(List<Point>)Call(app,"NavigationRoute");
            Check(Get<bool>(app,"navigationRouteStrictQuestFinish")&&
                restoredPolicy.Count==0,
                "After practice the pending quest immediately regains strict exit validation");
            ClearDraw();Call(app,"DrawMinimap");
            Check(GUI.commands.Exists(s=>s.Contains("ROUTE BLOCKED"))&&
                NavigationRouteDots(3f)==0,
                "Real production HUD shows blocked status without fake breadcrumbs");
            // The gate physically reopens; cached rows must invalidate on
            // their original identities and the approved route must recover.
            g.Map.rows=savedRows;
            var clear=(List<Point>)Call(app,"NavigationRoute");
            Check(clear.Count>0&&
                g.NavigationPathFrom(g.Position,center,true).Count>0,
                "Physically opened destination restores the real complete walking route");
            ClearDraw();Call(app,"DrawMinimap");
            Check(!GUI.commands.Exists(s=>s.Contains("ROUTE BLOCKED"))&&
                NavigationRouteDots(3f)>0,
                "Minimap returns to walkable route only after actual collision geometry opens");
        }
        finally
        {
            g.Map.rows=savedRows;
            portal.x=previous.x;portal.y=previous.y;
        }
        g.Close();
        Console.WriteLine("PASS Requested regional route refuses a sealed portal frontier, invalidates permissive cache, recovers on reopening");
    }

    public static void NavigationRouteGuidance(FrontierRuntime app)
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

        ClearDraw(); Call(app, "DrawMinimap");
        string blockedLabel = game.L("路线受阻", "ROUTE BLOCKED");
        Check(!GUI.commands.Exists(command => command.Contains(blockedLabel)),
            "A reachable tracked objective never shows the minimap blocked-route label");
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
        // This assertion is for the on-screen marker specifically. The quest
        // objective may be outside the hero-centered viewport, so center the
        // camera on the known target rather than assuming its authored region
        // lies within the playable view.
        Set(app, "camera", goal.Value);
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
            bool reachableArrow = NavigationEdgeArrowPixels() == 4 &&
                NavigationEdgeArrowStaysInPlayableView(630f);
            bool blockedArrow = NavigationBlockedPixels() == 9 &&
                NavigationBlockedCueStaysInPlayableView(630f);
            Check(reachableArrow || blockedArrow,
                "Off-screen " + edgeNames[i] + " objective receives a safe directional cue in either route state");
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
        bool originalPractice = game.practicing;
        Enemy[] originalEnemies = game.enemies.ToArray();
        Point blockedPredecessor = new Point();
        try
        {
            Point? campaignTarget = game.NavigationTarget();
            Check(campaignTarget.HasValue,
                "The blocked-route fixture begins with the real tracked campaign objective");
            int mapWidth = originalRows[0].Length, mapHeight = originalRows.Length;
            Point blockedObjective = new Point();
            bool foundInteriorRoute = false;
            // The tracked campaign goal can be near a map edge or close to the
            // player. Search standable interior tiles from farthest to nearest,
            // and accept only a destination with an actual BFS route. This
            // avoids both border assumptions and disconnected open pockets.
            List<Point> interiorCandidates = new List<Point>();
            for (int y = 5; y < mapHeight - 5; y++)
                for (int x = 5; x < mapWidth - 5; x++)
                {
                    Point candidate = new Point(x + .5f, y + .5f);
                    if ((candidate - game.Position).Length > 6f && game.CanStand(candidate))
                        interiorCandidates.Add(candidate);
                }
            interiorCandidates.Sort((a, b) =>
                (b - game.Position).Length.CompareTo((a - game.Position).Length));
            foreach (Point candidate in interiorCandidates)
            {
                List<Point> candidateRoute = game.NavigationPath(candidate);
                if (candidateRoute.Count == 0) continue;
                int candidateX = (int)Math.Floor(candidate.x);
                int candidateY = (int)Math.Floor(candidate.y);
                for (int i = candidateRoute.Count - 1; i >= 0; i--)
                {
                    int px = (int)Math.Floor(candidateRoute[i].x);
                    int py = (int)Math.Floor(candidateRoute[i].y);
                    if (Math.Abs(px - candidateX) + Math.Abs(py - candidateY) != 1) continue;
                    blockedObjective = candidate;
                    blockedPredecessor = candidateRoute[i];
                    foundInteriorRoute = true;
                    break;
                }
                if (foundInteriorRoute) break;
            }
            Check(foundInteriorRoute,
                "The map has a standable, connected interior waypoint for the collision fixture");
            game.practicing = true;
            game.enemies.Clear();
            game.enemies.Add(new Enemy(991233, 0,
                blockedObjective.x, blockedObjective.y, 100));
            Point? unreachable = game.NavigationTarget();
            Check(unreachable.HasValue &&
                (unreachable.Value - blockedObjective).Length < .001f,
                "The fixture uses a real live practice target as its tracked in-world objective");
            int targetX = (int)Math.Floor(unreachable.Value.x);
            int targetY = (int)Math.Floor(unreachable.Value.y);
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
            ClearDraw(); Call(app, "DrawMinimap");
            Check(GUI.commands.Exists(command => command.Contains(blockedLabel)),
                "The minimap explicitly explains why the missing route has no gold breadcrumbs");
            game.save.showGuidance = false;
            ClearDraw(); Call(app, "DrawMinimap");
            Check(!GUI.commands.Exists(command => command.Contains(blockedLabel)),
                "The blocked-route label obeys the existing guidance accessibility toggle");
            game.save.showGuidance = true;
            ClearDraw(); Call(app, "DrawNavigationTargetMarker");
            Check(NavigationBlockedPixels() == 5 && NavigationRouteDots(4f) == 0,
                "An unreachable visible objective gets a coral broken-route mark instead of the gold ring");

            Set(app, "camera", new Point(unreachable.Value.x + 16f, unreachable.Value.y));
            ClearDraw(); Call(app, "DrawNavigationTargetMarker");
            Check(NavigationBlockedPixels() == 9 && NavigationEdgeArrowPixels() == 0 &&
                NavigationRouteDots(4f) == 0,
                "An unreachable off-screen objective keeps directional bearing but marks the edge cue blocked");

            // The outer map array stays the same while one immutable row is
            // replaced in-place. Reference-checking only the array would keep
            // the empty route forever; the snapshot must notice this change.
            List<Point> closedRouteCache = (List<Point>)Call(app, "NavigationRoute");
            int predecessorX = (int)Math.Floor(blockedPredecessor.x);
            int predecessorY = (int)Math.Floor(blockedPredecessor.y);
            if (predecessorX < targetX)
            {
                char[] openCorridor = blockedRows[targetY].ToCharArray();
                for (int x = targetX - 2; x <= targetX; x++) openCorridor[x] = '.';
                blockedRows[targetY] = new string(openCorridor);
            }
            else if (predecessorX > targetX)
            {
                char[] openCorridor = blockedRows[targetY].ToCharArray();
                for (int x = targetX; x <= targetX + 2; x++) openCorridor[x] = '.';
                blockedRows[targetY] = new string(openCorridor);
            }
            else if (predecessorY < targetY)
            {
                for (int y = targetY - 2; y <= targetY; y++)
                {
                    char[] row = blockedRows[y].ToCharArray();
                    row[targetX] = '.'; blockedRows[y] = new string(row);
                }
            }
            else
            {
                for (int y = targetY; y <= targetY + 2; y++)
                {
                    char[] row = blockedRows[y].ToCharArray();
                    row[targetX] = '.'; blockedRows[y] = new string(row);
                }
            }
            List<Point> reopenedRoute = (List<Point>)Call(app, "NavigationRoute");
            Check(reopenedRoute.Count > 0 &&
                !object.ReferenceEquals(closedRouteCache, reopenedRoute),
                "Replacing one terrain row inside the same rows array invalidates the cached BFS immediately");
            Set(app, "camera", unreachable.Value);
            ClearDraw(); Call(app, "DrawNavigationTargetMarker");
            Check(NavigationBlockedPixels() == 0 && NavigationRouteDots(4f) == 12,
                "Opening a real walkable corridor restores the ordinary gold objective ring");
            ClearDraw(); Call(app, "DrawMinimap");
            Check(!GUI.commands.Exists(command => command.Contains(blockedLabel)),
                "The minimap clears its blocked warning as soon as terrain becomes reachable");
        }
        finally
        {
            blockedMap.rows = originalRows;
            game.practicing = originalPractice;
            game.enemies.Clear(); game.enemies.AddRange(originalEnemies);
            game.save.x = originalX; game.save.y = originalY;
            Set(app, "camera", game.Position);
            Call(app, "NavigationRoute");
        }
        // A moving objective may travel a substantial distance without
        // leaving its whole map tile. Rebuild on half-tile boundaries while
        // preserving the cached BFS for small sub-bucket movement.
        bool oldPractice = game.practicing;
        float movingOldX = game.save.x, movingOldY = game.save.y;
        Enemy[] oldEnemies = game.enemies.ToArray();
        try
        {
            game.save.x = 12; game.save.y = 14;
            Point anchor = route[route.Count / 2];
            int movingTileX = (int)Math.Floor(anchor.x);
            int movingTileY = (int)Math.Floor(anchor.y);
            game.enemies.Clear();
            Enemy movingTarget = new Enemy(991234, 0,
                movingTileX + .1f, movingTileY + .5f, 100);
            game.enemies.Add(movingTarget);
            game.practicing = true;
            Set(app, "camera", game.Position);
            List<Point> initialMovingRoute = (List<Point>)Call(app, "NavigationRoute");
            Check(initialMovingRoute.Count > 0,
                "A moving practice objective has a real collision-valid route");
            Check(Get<int>(app, "navigationRouteTargetX") ==
                (int)Math.Floor(movingTarget.x * 2f),
                "The route cache records the target's half-tile bucket");
            movingTarget.x = movingTileX + .3f;
            List<Point> subBucketRoute = (List<Point>)Call(app, "NavigationRoute");
            Check(object.ReferenceEquals(initialMovingRoute, subBucketRoute),
                "Small target motion inside one half-tile bucket reuses the existing BFS");
            movingTarget.x = movingTileX + .8f;
            List<Point> advancedMovingRoute = (List<Point>)Call(app, "NavigationRoute");
            Check(!object.ReferenceEquals(subBucketRoute, advancedMovingRoute) &&
                Get<int>(app, "navigationRouteTargetX") ==
                    (int)Math.Floor(movingTarget.x * 2f),
                "A moving target is re-routed after moving within the same whole tile");
            Console.WriteLine("PASS moving-objective half-tile route refresh and stable sub-bucket cache");
        }
        finally
        {
            game.practicing = oldPractice;
            game.enemies.Clear(); game.enemies.AddRange(oldEnemies);
            game.save.x = movingOldX; game.save.y = movingOldY;
            Set(app, "camera", game.Position);
            Call(app, "NavigationRoute");
        }
        Console.WriteLine("PASS tracked-quest routes, blocked-objective cues, HUD/local maps, guidance toggle and route cache invalidation");
    }

    static void RidgeWatchIsDrawnOnTheActualWorldMarker(FrontierRuntime app)
    {
        Prepare(app);
        GameCore game=app.Game;
        game.save.room=1;
        WorldObject marker=Array.Find(game.Map.objects,
            o=>o.kind=="ridgemarker"&&o.id.EndsWith("_0"));
        Check(marker!=null,"Ridge presentation must use a real authored map actor");
        var markerPoint=new Point(marker.x,marker.y);
        Set(app,"camera",markerPoint);
        game.save.x=marker.x;game.save.y=marker.y;
        Point p=(Point)Call(app,"ScreenPoint",markerPoint);
        game.save.flags.Remove("ridge_seen_1_0");
        game.save.flags.Remove("ridge_seen_1_1");
        game.save.flags.Remove("boss_1");
        ClearDraw();Call(app,"DrawObject",marker);
        Check(!GUI.textureCalls.Exists(t=>t.texture==Texture2D.whiteTexture&&
            t.rect.width==5f&&t.rect.height==5f&&
            Math.Abs(t.rect.y-(p.y-96))<.01f),
            "Locked high trail must not advertise an unavailable optional watch");
        game.save.flags.Add("ridge_seen_1_0");
        game.save.flags.Add("ridge_seen_1_1");
        game.save.flags.Add("boss_1");
        ClearDraw();Call(app,"DrawObject",marker);
        var empty=GUI.textureCalls.FindAll(t=>t.texture==Texture2D.whiteTexture&&
            t.rect.width==5f&&t.rect.height==5f&&
            Math.Abs(t.rect.y-(p.y-96))<.01f);
        Check(empty.Count==3&&empty[0].color.g<.8f,
            "Unlocked route renders three pending grounded survey pips");
        game.save.flags.Add("ridge_watch_1_0");
        ClearDraw();Call(app,"DrawObject",marker);
        var partial=GUI.textureCalls.FindAll(t=>t.texture==Texture2D.whiteTexture&&
            t.rect.width==5f&&t.rect.height==5f&&
            Math.Abs(t.rect.y-(p.y-96))<.01f);
        Check(partial.Count==3&&partial[0].color.g>empty[0].color.g&&
            partial[1].color.g==empty[1].color.g,
            "Only actually surveyed lanes become green; untouched patrols remain pending");
        game.save.flags.Add("ridge_watch_1_1");
        game.save.flags.Add("ridge_watch_1_2");
        game.save.flags.Add("ridge_watch_complete_1");
        ClearDraw();Call(app,"DrawObject",marker);
        var finished=GUI.textureCalls.FindAll(t=>t.texture==Texture2D.whiteTexture&&
            t.rect.width==5f&&t.rect.height==5f&&
            Math.Abs(t.rect.y-(p.y-96))<.01f);
        Check(finished.Count==3&&finished.TrueForAll(t=>
            Math.Abs(t.color.g-partial[0].color.g)<.0001f),
            "Finished fieldwork draws the complete watch on the actual marked ridge");
        Console.WriteLine("PASS production ridge-watch marker: locked, pending, partial and cleared world draw cues");
    }


    static void RidgePatrolRingUsesActualLivingWorldActor(FrontierRuntime app)
    {
        Prepare(app);
        var g=app.Game;
        g.save.flags.Add("boss_1");
        g.save.flags.Add("ridge_seen_1_0");
        g.save.flags.Add("ridge_seen_1_1");
        g.Enter(1);
        var idField=typeof(GameCore).GetField("ridgeWatchIds",Private);
        var tracked=(Dictionary<int,int>)idField.GetValue(g);
        Enemy enemy=null;int slot=-1;
        foreach(Enemy e in g.enemies)
            if(tracked.TryGetValue(e.id,out slot)&&slot==0){enemy=e;break;}
        Check(enemy!=null&&g.IsActiveRidgeWatchPatrol(enemy),
            "Only the real live ridge enemy may receive a world patrol ring");
        Set(app,"camera",enemy.Position);
        g.save.reducedMotion=true;
        ClearDraw();Call(app,"DrawEnemyActor",enemy);
        int ringed=GUI.textureCalls.FindAll(c=>c.texture==Texture2D.whiteTexture&&
            Math.Abs(c.rect.width-4f)<.001f&&Math.Abs(c.rect.height-4f)<.001f).Count;
        int body=GUI.textureCalls.FindAll(c=>c.texture!=Texture2D.whiteTexture).Count;
        Check(ringed>=16&&body>0,
            "Actual reduced-motion monster render retains real body and visible fixed quest ring");
        g.save.flags.Add("ridge_watch_1_0");
        ClearDraw();Call(app,"DrawEnemyActor",enemy);
        int credited=GUI.textureCalls.FindAll(c=>c.texture==Texture2D.whiteTexture&&
            Math.Abs(c.rect.width-4f)<.001f&&Math.Abs(c.rect.height-4f)<.001f).Count;
        Check(ringed-credited==16&&!g.IsActiveRidgeWatchPatrol(enemy)&&
            GUI.textureCalls.FindAll(c=>c.texture!=Texture2D.whiteTexture).Count==body,
            "Credited patrol drops exactly one ring without changing painted monster frames");
        g.save.flags.Remove("ridge_watch_1_0");
        g.save.flags.Add("ridge_watch_complete_1");
        ClearDraw();Call(app,"DrawEnemyActor",enemy);
        int complete=GUI.textureCalls.FindAll(c=>c.texture==Texture2D.whiteTexture&&
            Math.Abs(c.rect.width-4f)<.001f&&Math.Abs(c.rect.height-4f)<.001f).Count;
        Check(complete==credited,
            "Fully cleared watch never draws a misleading hostile objective ring");
        Console.WriteLine("PASS actual ridge watch combat body, fixed ring, credited removal and sprite preservation");
    }

    public static void Run(FrontierRuntime app,string folder)
    {
        try{InputDirections(app,folder);WorldAndAudio(app);CombatMusicReversesWhenActualThreatLeaves();GroundedMaterialFootfalls(app,folder);NonheroGroundContactTracking(app);HudReadability(app,folder);NavigationRouteGuidance(app);RidgeWatchIsDrawnOnTheActualWorldMarker(app);RidgePatrolRingUsesActualLivingWorldActor(app);DrawStateContinuity(app,folder);StableSceneOrdering(app);FullAuditPresentation(app,folder);TransitionTimeline(app,folder);CompleteBossFight(app,folder);}
        finally{app.EndVisualReview();Input.Clear();GUI.queuedClick=null;Screen.width=1280;Screen.height=800;Time.unscaledDeltaTime=.02f;ClearDraw();Call(app,"Resize");}
    }
}
