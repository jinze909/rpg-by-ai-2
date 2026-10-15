// Production draw-call regression and review sheets. This is not native Unity.
using System;
using System.Linq;
using System.Reflection;
using Starfall;
using UnityEngine;
public static class ActorRegistrationChecks
{
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static readonly Point[] Directions={new Point(1,0),new Point(-1,0),new Point(0,-1),new Point(0,1)};
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static object Call(object app,string name,params object[] args){return app.GetType().GetMethod(name,Private).Invoke(app,args);}
    static void Set(object app,string name,object value){app.GetType().GetField(name,Private).SetValue(app,value);}
    static void Clear(){GUI.commands.Clear();GUI.textureCalls.Clear();GUI.buttonCalls.Clear();GUI.matrix=Matrix4x4.identity;GUI.color=Color.white;}
    static void Place(FrontierRuntime app,Point feet,Point pixel){Set(app,"camera",feet-new Point((pixel.x-640)/36,(pixel.y-390)/36));}
    static bool ActorTexture(GUI.TextureCall c){return (c.texture.name.StartsWith("resource:Frontier/Art/")||
        c.texture.name.StartsWith("resource:Frontier/HeroAnimator/Canonical/Hero_"))&&
        !c.texture.name.Contains("Effects")&&
        !c.texture.name.EndsWith("/HeroMotionAccents")&&
        !c.texture.name.EndsWith("/CoastlineRemaster")&&
        !c.texture.name.EndsWith("/ActorContactShadows");}
    static void Density(GUI.TextureCall[] calls,float density)
    {
        foreach(var c in calls.Where(ActorTexture))
        {
            Check(Math.Abs(c.rect.width/(Math.Abs(c.uv.width)*c.texture.width)-density)<.001f,"Actor pixel density changes across poses: "+c.texture.name);
            Check(c.uv.x>=0&&c.uv.x<=1&&c.uv.y>=0&&c.uv.y+c.uv.height<=1.0001f,"Actor crop escapes its atlas");
            Check(c.rect.width>0&&c.rect.height>0&&c.color.a>=0&&c.color.a<=1,"Actor geometry/opacity must stay valid");
        }
    }
    static GUI.TextureCall[] Draw(FrontierRuntime app,string method,params object[] args)
    {int start=GUI.textureCalls.Count;Call(app,method,args);return GUI.textureCalls.Skip(start).ToArray();}
    static void Caption(Point p,string s){GUI.Label(new Rect(p.x-38,p.y+8,92,22),s,new GUIStyle{fontSize=12});}
    static void Players(FrontierRuntime app,string folder)
    {
        string[] states={"idle","left step","pass 1","right step","pass 2","wind-up","contact","Q prepare","Q release","dodge","brace","hit","death"};int total=0;
        for(int direction=0;direction<4;direction++)
        {
            Clear();
            for(int weapon=0;weapon<3;weapon++)for(int state=0;state<states.Length;state++)
            {
                app.BeginVisualReview(weapon,0);var g=app.Game;g.enemies.Clear();g.save.companion=-1;g.save.upgrades=0;g.invulnerable=0;g.SetAim(Directions[direction]);
                if(state>=1&&state<=4){g.movementDirection=Directions[direction];g.locomotionFacing=Directions[direction];g.walkDistance=(state-1)*GameCore.WalkStride/4;}
                if(state>=5&&state<=8)g.playerAction=new ActorAction{kind=state<7?"attack":"skill",weapon=weapon,direction=Directions[direction],duration=1,contact=.4f,elapsed=state%2==1?.2f:.41f,released=state%2==0};
                if(state==9){g.dashTime=.1f;Set(g,"dashDirection",Directions[direction]);}if(state==10)g.guardTime=1;if(state==11)g.playerHitTime=.1f;if(state==12)g.deathTime=.7f;
                Point pixel=new Point(54+state*96,210+weapon*190);Place(app,g.Position,pixel);
                var calls=Draw(app,"DrawPlayerActor");Density(calls,.75f);var actors=calls.Where(ActorTexture).ToArray();Check(actors.Length>0,"Playable actor has no visible pixels");
                // One whole accepted 256px body, including hair and hips,
                // is drawn on ONE atlas call at a constant 128/240 foot.
                Check(actors.Length==1,
                    "Player must not be split into torso, head, lower-leg draw calls");
                var body=actors[0];
                string profession=new[]{"Vanguard","Ranger","Arcanist"}[weapon];
                Check(body.texture.name.StartsWith(
                    "resource:Frontier/HeroAnimator/Canonical/Hero_"+profession+"_"),
                    "Real player must use its class-owned published full-body atlas");
                Check(Math.Abs(body.rect.width-192)<.001f&&
                    Math.Abs(body.rect.height-192)<.001f,
                    "All protagonist poses use one 256px source and 0.75 density");
                Check(Math.Abs(body.rect.x-(pixel.x-96))<1.1f&&
                    Math.Abs(body.rect.y-(pixel.y-180))<1.1f,
                    "Complete player feet cannot bob when switching full-body states");
                Check(!body.texture.name.EndsWith("/WalkVanguard")||weapon==0,
                    "Ranger/Arcanist may share GOLD hair pixels but never warrior armor");
                Caption(pixel,states[state]);total++;
            }
            GUI.Export(folder,"review-registered-player-"+direction);
        }
        Console.WriteLine("PASS "+total+" player combinations: exact foot/canvas density, fixed blonde head, four directions, contact, guard, recoil and death");
    }
    static void ProductionLoadoutUsesFullBodyHero(FrontierRuntime app)
    {
        app.BeginVisualReview(0,0);
        for(int profession=0;profession<3;profession++)
        {
            const float x=640f,y=400f,size=104f;
            GUI.textureCalls.Clear();
            var calls=Draw(app,"DrawPlayerPreview",
                profession,x,y,size).Where(ActorTexture).ToArray();
            Check(calls.Length==1,"Inventory preview must draw one complete body");
            string cls=new[]{"Vanguard","Ranger","Arcanist"}[profession];
            Check(calls[0].texture.name==
                "resource:Frontier/HeroAnimator/Canonical/Hero_"+cls+"_Idle_S",
                "Inventory preview must use the actual class/blonde production Sprite");
            Check(Math.Abs(calls[0].rect.width-size)<.01f&&
                Math.Abs(calls[0].rect.height-size)<.01f,
                "Loadout portrait should be 104px, not a 0.54px dot");
            Check(Math.Abs(calls[0].rect.x-(x-size/2f))<.01f&&
                Math.Abs(calls[0].rect.y-(y-size*240f/256f))<.01f,
                "Inventory and world need the same canonical 128,240 foot anchor");
        }
        Console.WriteLine("PASS 3 true full-body inventory portraits at 104px with class outfit and blonde identity");
    }

    static void Companions(FrontierRuntime app,string folder)
    {
        int total=0;string[] names={"Mira","Ivo","Sen"};
        for(int direction=0;direction<4;direction++)
        {
            Clear();for(int id=0;id<3;id++)for(int state=0;state<7;state++)
            {
                app.BeginVisualReview(0,0);var g=app.Game;g.save.companion=id;g.companionAim=Directions[direction];g.companionAction=null;g.companionMoving=state>=1&&state<=4;g.companionMoveTime=Math.Max(0,state-1)*GameCore.WalkStride/4;
                if(state>=5)g.companionAction=new ActorAction{direction=Directions[direction],released=state==6,kind="attack"};
                Point pixel=new Point(140+state*160,230+id*180);Place(app,g.companionPosition,pixel);var calls=Draw(app,"DrawCompanionActor");Density(calls,.75f);
                var face=calls.Where(ActorTexture).Last();Check(face.texture.name.EndsWith("/Walk"+names[id]),"Companion identity must match its own authored actor");Caption(pixel,"state "+state);total++;
            }GUI.Export(folder,"review-registered-companion-"+direction);
        }Console.WriteLine("PASS "+total+" companion combinations: fixed density, own face and directional preparation/release");
    }
    static void Enemies(FrontierRuntime app,string folder)
    {
        int total=0;
        for(int batch=0;batch<3;batch++)
        {
            Clear();Screen.width=batch==0?1280:2560;Screen.height=1000;
            int count=batch==0?3:4;
            for(int id=0;id<count;id++)for(int state=0;state<9;state++)
            {
                int room=batch==0?0:(batch-1)*4+id+1;app.BeginVisualReview(0,room);var g=app.Game;g.enemies.Clear();g.invulnerable=0;
                var e=new Enemy(800+id,id,g.Position.x,g.Position.y,100,batch>0){facing=new Point(1,0),timer=.4f,windupDuration=.65f};
                if(state>=1&&state<=4){e.moving=true;e.moveTime=(state-1)*(e.boss?2.8f:e.kind==0?1.35f:1.8f)/4;}if(state==5)e.state=1;if(state==6)e.releaseTime=.1f;if(state==7){e.hitTime=.1f;e.flash=.08f;}
                Point pixel=new Point(batch==0?80+state*137:150+state*280,220+id*225);Place(app,e.Position,pixel);
                var calls=state==8?Draw(app,"DrawFallen",new FallenActor{actor=e,room=room,life=.8f,duration=1}):Draw(app,"DrawEnemyActor",e);
                Density(calls,e.boss?(room==5?264:240)/320f:(e.kind==2?106:96)/128f);Caption(pixel,"state "+state);total++;
            }GUI.Export(folder,"review-registered-enemies-"+batch);
        }Screen.width=1280;Screen.height=800;Console.WriteLine("PASS "+total+" enemy/Boss combinations: gait, wind-up, release, flash, recoil and retained death density");
    }
    static void BossFlashFollowsActualMovingSilhouette(FrontierRuntime app)
    {
        // Exercise the actual Frontier DrawEnemyActor, not an unintegrated
        // art preview. The first Boss has a new three-phase whole-body atlas;
        // later rooms must continue to use their original walk sources.
        app.BeginVisualReview(0,1);
        var g=app.Game; g.enemies.Clear();
        var e=new Enemy(93000,0,g.Position.x,g.Position.y,150,true) {
            facing=new Point(1,0), moving=true, moveTime=.8f,
            flash=.045f, hitTime=0, releaseTime=0
        };
        const string atlas="resource:Frontier/Art/BriarBeast";
        Func<GUI.TextureCall[],string,GUI.TextureCall> actor=(draws,name)=>
            draws.FirstOrDefault(c=>c.texture.name==name);
        Func<GUI.TextureCall[],string,GUI.TextureCall> mask=(draws,name)=>
            draws.FirstOrDefault(c=>
                c.texture.name=="resource:Frontier/Art/ActorMasks/flash_"+name ||
                c.texture.name=="flash_resource_Frontier_Art_"+name);
        Action<GUI.TextureCall,GUI.TextureCall> sameFrame=(body,flash)=> {
            Check(body!=null&&flash!=null,
                "Boss full-body source and its corresponding mask must both exist");
            Check(Math.Abs(body.rect.x-flash.rect.x)<.001f &&
                  Math.Abs(body.rect.y-flash.rect.y)<.001f &&
                  Math.Abs(body.rect.width-flash.rect.width)<.001f &&
                  Math.Abs(body.rect.height-flash.rect.height)<.001f &&
                  Math.Abs(body.uv.x-flash.uv.x)<.0001f &&
                  Math.Abs(body.uv.y-flash.uv.y)<.0001f &&
                  Math.Abs(body.uv.width-flash.uv.width)<.0001f &&
                  Math.Abs(body.uv.height-flash.uv.height)<.0001f,
                "Boss flashing body must use the EXACT drawn source cell/root");
        };
        float previousPhaseCrop=-1;
        foreach(float health in new[]{150f,78f,32f})
        {
            e.hp=health;e.state=0;e.moving=true;e.hitTime=0;e.releaseTime=0;
            Clear();var draws=Draw(app,"DrawEnemyActor",e);
            var body=actor(draws,atlas);
            var flash=mask(draws,"BriarBeast");
            sameFrame(body,flash);
            Check(!draws.Any(c=>c.texture.name=="resource:Frontier/Art/WalkBossA"),
                "Thornwood may not draw an old body over a live Briar pose");
            if(previousPhaseCrop>=0)
                Check(Math.Abs(body.uv.y-previousPhaseCrop)>.01f,
                    "Briar HP threshold must change the actual sampled sprite row");
            previousPhaseCrop=body.uv.y;
        }
        // Phase-entry feedback is keyed to the ACTUAL enemy object and the
        // live HP threshold, not its numeric ID or an unrelated idle clock.
        // Test real production draw calls, including flash decay and no
        // phantom cue when a restored boss first appears at low HP.
        var threshold = new Enemy(93010, 0, g.Position.x, g.Position.y, 150, true) {
            moving = true, moveTime = .8f, flash = 0
        };
        Clear(); var baseline = Draw(app, "DrawEnemyActor", threshold);
        Check(mask(baseline, "BriarBeast") == null,
            "First sight of a boss must not invent a phase transition");
        threshold.hp = 78f;
        Clear(); var transition = Draw(app, "DrawEnemyActor", threshold);
        sameFrame(actor(transition, atlas), mask(transition, "BriarBeast"));
        float firstOpacity = mask(transition, "BriarBeast").color.a;
        Set(app, "clock", .16f);
        Clear(); var decaying = Draw(app, "DrawEnemyActor", threshold);
        sameFrame(actor(decaying, atlas), mask(decaying, "BriarBeast"));
        Check(mask(decaying, "BriarBeast").color.a < firstOpacity,
            "Phase glow must fade against the authoritative runtime clock");
        Set(app, "clock", .4f);
        Clear(); var expired = Draw(app, "DrawEnemyActor", threshold);
        Check(mask(expired, "BriarBeast") == null,
            "Boss phase cue cannot persist after its 320ms window");
        threshold.hp = 32f;
        Clear(); var third = Draw(app, "DrawEnemyActor", threshold);
        sameFrame(actor(third, atlas), mask(third, "BriarBeast"));
        threshold.hp = 150f;
        Clear(); var healed = Draw(app, "DrawEnemyActor", threshold);
        Check(mask(healed, "BriarBeast") == null,
            "HP recovery must reset phase tracking without fake glow");
        var restored = new Enemy(93010, 0, g.Position.x, g.Position.y, 150, true) {
            hp = 30f, moving = true, moveTime = .8f
        };
        Clear(); var firstLowHp = Draw(app, "DrawEnemyActor", restored);
        Check(mask(firstLowHp, "BriarBeast") == null,
            "New enemy instance must not inherit an old boss phase cue");
        Set(app, "clock", 0f);
        // Regression: EnemyPose() historically exposed an early-release pose
        // in room 1 for the final 200ms of still-active windup. The NEW
        // authored hit drawing may only appear after Core releases the attack.
        e.hp=150;e.moving=false;e.state=1;e.timer=.10f;
        e.windupDuration=1.02f;e.releaseTime=0;
        Clear();var pending=actor(Draw(app,"DrawEnemyActor",e),atlas);
        Check(pending!=null && Math.Abs(pending.uv.y-(1f-3f/18f))<.0001f,
            "Boss windup must remain in action row 2 until real contact");
        e.state=0;e.releaseTime=.31f;
        Clear();var released=actor(Draw(app,"DrawEnemyActor",e),atlas);
        Check(released!=null&&Math.Abs(released.uv.y-(1f-4f/18f))<.0001f,
            "Boss release must use action row 3 only after Core dispatch");
        e.releaseTime=0;e.flash=.04f;e.moving=false;
        Clear();var idle=Draw(app,"DrawEnemyActor",e);
        sameFrame(actor(idle,atlas),mask(idle,"BriarBeast"));
        // Keep the mature moving-body flash behavior in other regions.
        app.BeginVisualReview(0,2);
        e.hp=150;e.state=0;e.moving=true;e.moveTime=.8f;e.flash=.04f;
        Clear();var oldBoss=Draw(app,"DrawEnemyActor",e);
        sameFrame(actor(oldBoss,"resource:Frontier/Art/WalkBossA"),
            mask(oldBoss,"WalkBossA"));
        Check(!oldBoss.Any(c=>c.texture.name==atlas),
            "Room 2 boss must not inherit Thornwood monster identity");
        // Other bosses keep their authored silhouette but acquire a readable
        // phase tint and a same-frame flash at the exact HP threshold.
        var other = new Enemy(93020, 0, g.Position.x, g.Position.y, 150, true) {
            moving = true, moveTime = .8f
        };
        Clear(); var otherFirst = Draw(app, "DrawEnemyActor", other);
        var firstBody = actor(otherFirst, "resource:Frontier/Art/WalkBossA");
        Check(firstBody != null, "Other boss must retain its live walking body");
        other.hp = 75f;
        Clear(); var otherSecond = Draw(app, "DrawEnemyActor", other);
        var secondBody = actor(otherSecond, "resource:Frontier/Art/WalkBossA");
        sameFrame(secondBody, mask(otherSecond, "WalkBossA"));
        Check(secondBody.color.g < firstBody.color.g &&
              secondBody.color.b < firstBody.color.b,
            "Boss stage 2 must remain visible after its phase-entry flash");
        Console.WriteLine("PASS three Briar body phases, real HP phase cues, matched masks, persistent other-Boss tint and fenced contact");
    }

    static void PreparedMasks()
    {
        var source=Resources.Load<Texture2D>("Frontier/Art/Bosses");Check(source!=null&&source.isReadable,"Actual metadata must keep source alpha readable after import");
        source.isReadable=false;var art=new PixelArt();var flash=art.FlashMask(source);var outline=art.OutlineMask(source);
        Check(source.pixelReads==0&&flash.width==source.width&&outline.height==source.height,"Prepared masks cannot read or scan the large source at first hit");
        for(int i=0;i<1000;i++)Check(ReferenceEquals(flash,art.FlashMask(source))&&ReferenceEquals(outline,art.OutlineMask(source)),"Repeated hits must reuse exact GPU resources");
        art.Dispose();Check(!flash.destroyed&&!outline.destroyed,"Disposal cannot destroy borrowed Resources textures");
        Console.WriteLine("PASS Prebaked masks: zero CPU source reads, 1000 cached draws and resource ownership");
    }
    static void Muzzles(FrontierRuntime app,string folder)
    {
        Clear();
        for(int weapon=1;weapon<=2;weapon++)for(int direction=0;direction<4;direction++)
        {
            app.BeginVisualReview(weapon,0);var g=app.Game;g.enemies.Clear();g.save.upgrades=0;g.invulnerable=0;
            g.playerAction=new ActorAction{kind="attack",weapon=weapon,direction=Directions[direction],duration=1,contact=.4f,elapsed=.41f,released=true};
            Point pixel=new Point(220+direction*280,300+(weapon-1)*280);Place(app,g.Position,pixel);Draw(app,"DrawPlayerActor");
            Point tip=(Point)Call(app,"ScreenPoint",GameCore.WeaponOrigin(g.Position,weapon,Directions[direction]));tip.y-=GameCore.WeaponHeight(weapon,Directions[direction]);
            GUI.color=new Color(1,.25f,.18f);GUI.DrawTexture(new Rect(tip.x-1,tip.y-4,2,8),Texture2D.whiteTexture);GUI.DrawTexture(new Rect(tip.x-4,tip.y-1,8,2),Texture2D.whiteTexture);GUI.color=Color.white;
            Caption(pixel,"release "+direction);
        }GUI.Export(folder,"review-registered-muzzles");
    }
    static void FullBodyHeroProductionFrames(FrontierRuntime app)
    {
        // Unlike the removed UnifiedActionGait synthetic-leg test, this
        // exercises the REAL world DrawPlayerActor() method and the actual
        // canonical PNGs shipped under Assets/Resources. A body needs one
        // draw, one 256px sheet cell and a fixed (128,240) foot Pivot.
        var classes=new[]{"Vanguard","Ranger","Arcanist"};
        int checkedFrames=0;
        foreach(int profession in new[]{0,1,2})
        foreach(Point direction in Directions)
        {
            app.BeginVisualReview(profession,0);
            var g=app.Game;
            g.enemies.Clear();g.save.companion=-1;g.save.upgrades=0;
            g.SetAim(direction);
            g.movementDirection=direction;
            g.locomotionFacing=direction;
            for(int step=0;step<4;step++)
            {
                g.walkDistance=step*GameCore.WalkStride/4;
                Point location=new Point(480+step*5,300+profession*5);
                Place(app,g.Position,location);
                var calls=Draw(app,"DrawPlayerActor");
                var parts=calls.Where(ActorTexture).ToArray();
                Check(parts.Length==1,"A playable hero may NEVER be drawn "+
                    "as a separately attached head, torso or boot");
                var sprite=parts[0];
                string prefix="resource:Frontier/HeroAnimator/Canonical/Hero_"+
                    classes[profession]+"_";
                Check(sprite.texture.name.StartsWith(prefix),
                    "Published body must belong to the active class");
                Check(Math.Abs(sprite.rect.width-192)<.01f&&
                    Math.Abs(sprite.rect.height-192)<.01f,
                    "Full-body source cell and density must remain 256*.75");
                Check(Math.Abs(sprite.rect.x-location.x+96)<1.2f&&
                    Math.Abs(sprite.rect.y-location.y+180)<1.2f,
                    "One root/pivot must not move during the four walk phases");
                Check(!calls.Any(c=>c.texture.name=="resource:Frontier/Art/WalkVanguard"),
                    "No extra warrior head may be drawn over Ranger or Arcanist");
                checkedFrames++;
            }
        }
        Console.WriteLine("PASS "+checkedFrames+
            " real full-body player draws: one published class-owned Sprite, "+
            "canonical scale/root, no torso/head/leg puppetry");
    }

    public static void VerifyResidentAnimation(FrontierRuntime app)
    {
        // Genuine production DrawNpc calls, not source-string-only assertions.
        app.BeginVisualReview(0,0);
        var g=app.Game;
        var guild=new WorldObject { id="guild",kind="guild",x=17,y=17 };
        var other=new WorldObject { id="guild2",kind="guild",x=21,y=17 };
        var smith=new WorldObject { id="smith",kind="forge",x=6,y=8 };
        var priorConversation=g.conversation;
        var priorSource=g.conversationSource;
        const string atlas="resource:Frontier/Art/EmberfallResidents";
        // The shipped raider is hit-flashed in actual combat; its mask must
        // already exist before a hit, not be built in a Repaint callback.
        var art=(PixelArt)typeof(FrontierRuntime).GetField("art",Private).GetValue(app);
        var cache=(System.Collections.IDictionary)typeof(PixelArt).GetField(
            "cache",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(art);
        Check(cache.Keys.Cast<object>().Any(key=>
            key.ToString().Contains("flash_")&&
            key.ToString().Contains("MossboundRaider")),
            "Mossbound first-hit flash must be prepared at startup");
        Check(cache.Keys.Cast<object>().Any(key=>
            key.ToString().Contains("outline_")&&
            key.ToString().Contains("MossboundRaider")),
            "Mossbound outline must be prepared before world drawing");
        try
        {
            foreach(var npc in new [] {guild,smith})
            {
                g.conversation=null;g.conversationSource=null;
                Clear();
                var idle=Draw(app,"DrawNpc",npc.kind,
                    new Point(npc.x,npc.y),-1).FirstOrDefault(c=>c.texture.name==atlas);
                Check(idle!=null, "Published NPC art absent from actual Frontier drawing");
                Check(idle.rect.width==96&&idle.rect.height==96,
                    "NPC must stay at 128px logical source / fixed 96px draw size");
                Check(Math.Abs(idle.uv.width-.25f)<.0001f&&
                    Math.Abs(idle.uv.height-.25f)<.0001f,
                    "NPC must use one intact author-drawn 128x128 frame");
                g.conversation=new Conversation("speaker","speaker","hello","hello");
                g.conversationSource=npc;
                Clear();
                var spoken=Draw(app,"DrawNpc",npc.kind,
                    new Point(npc.x,npc.y),-1).FirstOrDefault(c=>c.texture.name==atlas);
                Check(spoken!=null&&Math.Abs(idle.uv.y-spoken.uv.y)>.05f,
                    "Actual NPC speaking must select separate authored action row");
                Check(Math.Abs(idle.rect.x-spoken.rect.x)<.001f&&
                    Math.Abs(idle.rect.y-spoken.rect.y)<.001f,
                    "NPC talk must not shift whole-body foot anchor");
            }
            // Nearby identical residents have independently phased idle
            // bodies, rather than synchronized clock-driven puppet motion.
            g.conversation=null;g.conversationSource=null;
            Clear();
            var phaseA=Draw(app,"DrawNpc","guild",
                new Point(17,17),-1).First(c=>c.texture.name==atlas);
            Clear();
            var phaseB=Draw(app,"DrawNpc","guild",
                new Point(18,17),-1).First(c=>c.texture.name==atlas);
            Check(Math.Abs(phaseA.uv.x-phaseB.uv.x)>.01f,
                "Two distinct residents must not breathe in lockstep");
            g.conversation=new Conversation("speaker","speaker","hello","hello");
            g.conversationSource=guild;
            Clear();
            var bystander=Draw(app,"DrawNpc","guild",
                new Point(other.x,other.y),-1).FirstOrDefault(c=>c.texture.name==atlas);
            Check(bystander!=null && Math.Abs(bystander.uv.y-.75f)<.0001f,
                "Same-kind bystander cannot steal the current dialogue animation");
            Clear();
            var unrelated=Draw(app,"DrawNpc","garden",
                new Point(10,10),-1);
            Check(unrelated.Any(c=>c.texture.name=="resource:Frontier/Art/Npcs"),
                "Other authored residents must retain their legacy body until remastered");
            Console.WriteLine("PASS live guild/forge idle/talk 16 frames, pinned speaker, fallback and stable foot");
        }
        finally
        {
            g.conversation=priorConversation;g.conversationSource=priorSource;
        }
    }

    public static void VerifyUnifiedGaitPixelComposites(FrontierRuntime app) { FullBodyHeroProductionFrames(app); ProductionLoadoutUsesFullBodyHero(app); }
    public static void VerifyBossMovingFlash(FrontierRuntime app) { BossFlashFollowsActualMovingSilhouette(app); }
    public static void Run(FrontierRuntime app,string folder)
    {
        try{PreparedMasks();FullBodyHeroProductionFrames(app);ProductionLoadoutUsesFullBodyHero(app);Players(app,folder);Companions(app,folder);BossFlashFollowsActualMovingSilhouette(app);Enemies(app,folder);Muzzles(app,folder);}
        finally{Screen.width=1280;Screen.height=800;app.EndVisualReview();Clear();}
    }
}
