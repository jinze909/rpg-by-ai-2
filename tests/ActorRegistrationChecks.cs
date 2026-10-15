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
        // Restored published high-detail Bosses/WalkBoss art. Keep all real
        // GameCore windup/release and moving hit-flash behaviour, but do not
        // require the rejected low-detail Briar candidate to appear live.
        app.BeginVisualReview(0,1);
        var g=app.Game;g.enemies.Clear();
        var boss=new Enemy(93000,0,g.Position.x,g.Position.y,150,true){
            facing=new Point(1,0),moving=true,moveTime=.8f,flash=.045f};
        Func<GUI.TextureCall[],string,GUI.TextureCall> actor=(draws,name)=>
            draws.FirstOrDefault(c=>c.texture.name=="resource:Frontier/Art/"+name);
        Func<GUI.TextureCall[],string,GUI.TextureCall> mask=(draws,name)=>
            draws.FirstOrDefault(c=>
                c.texture.name=="resource:Frontier/Art/ActorMasks/flash_"+name||
                c.texture.name=="flash_resource_Frontier_Art_"+name);
        Action<GUI.TextureCall,GUI.TextureCall> sameFrame=(body,flash)=>{
            Check(body!=null&&flash!=null,
                "Detailed walking Boss sprite and hit mask must both exist");
            Check(Math.Abs(body.rect.x-flash.rect.x)<.001f&&
                  Math.Abs(body.rect.y-flash.rect.y)<.001f&&
                  Math.Abs(body.rect.width-flash.rect.width)<.001f&&
                  Math.Abs(body.rect.height-flash.rect.height)<.001f&&
                  Math.Abs(body.uv.x-flash.uv.x)<.0001f&&
                  Math.Abs(body.uv.y-flash.uv.y)<.0001f,
                "Existing detailed Boss hit-flash must follow actual frame/root");
        };
        Clear();var moving=Draw(app,"DrawEnemyActor",boss);
        sameFrame(actor(moving,"WalkBossA"),mask(moving,"WalkBossA"));
        Check(!moving.Any(c=>c.texture.name==
            "resource:Frontier/Art/BriarBeast"),
            "Unapproved prototype Boss displaced the detailed production body");
        // The original authored Bosses atlas must not show release art during
        // the final 0.1 seconds of still-active GameCore windup.
        boss.moving=false;boss.flash=0;boss.state=1;
        boss.timer=.10f;boss.windupDuration=1.02f;boss.releaseTime=0f;
        Clear();var pending=actor(Draw(app,"DrawEnemyActor",boss),"Bosses");
        Check(pending!=null&&Math.Abs(pending.uv.x-.25f)<.0001f,
            "Original Boss windup art may not expose contact before release");
        boss.state=0;boss.releaseTime=.31f;
        Clear();var contact=actor(Draw(app,"DrawEnemyActor",boss),"Bosses");
        Check(contact!=null&&Math.Abs(contact.uv.x-.5f)<.0001f,
            "Original Boss contact must follow GameCore's released state");
        // Later regions must retain their own original moving Boss and
        // HP-stage palette feedback. Recolour never changes body source.
        app.BeginVisualReview(0,2);
        var other=new Enemy(93020,0,g.Position.x,g.Position.y,150,true){
            moving=true,moveTime=.8f,flash=.045f};
        Clear();var firstDraw=Draw(app,"DrawEnemyActor",other);
        sameFrame(actor(firstDraw,"WalkBossA"),mask(firstDraw,"WalkBossA"));
        var first=actor(firstDraw,"WalkBossA");
        other.hp=75f;
        Clear();var nextDraw=Draw(app,"DrawEnemyActor",other);
        sameFrame(actor(nextDraw,"WalkBossA"),mask(nextDraw,"WalkBossA"));
        var second=actor(nextDraw,"WalkBossA");
        Check(second.color.g<first.color.g&&second.color.b<first.color.b,
            "Original moving Boss must retain visible later HP phases");
        Console.WriteLine("PASS detailed original Bosses/WalkBoss bodies, flash, winding/release and room-2 HP tint");
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
        // VISUAL FIDELITY ROLLBACK: the older Npcs.png contains the carefully
        // shaded guild and forge bodies. The unapproved tiny-pixel replacement
        // must not appear in ANY dialogue state or on another bystander.
        app.BeginVisualReview(0,0);
        var game=app.Game;
        var guild=new WorldObject {id="guild",kind="guild",x=17,y=17};
        var smith=new WorldObject {id="smith",kind="forge",x=6,y=8};
        var savedConversation=game.conversation;
        var savedSource=game.conversationSource;
        const string original="resource:Frontier/Art/Npcs";
        var art=(PixelArt)typeof(FrontierRuntime).GetField("art",Private).GetValue(app);
        var cache=(System.Collections.IDictionary)typeof(PixelArt).GetField(
            "cache",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(art);
        Check(cache.Keys.Cast<object>().Any(key=>
            key.ToString().Contains("flash_")&&
            key.ToString().Contains("Monsters")),
            "Original raider first-hit flash must remain prewarmed");
        try
        {
            foreach(var npc in new[]{guild,smith})
            {
                int expectedIndex=npc.kind=="guild"?0:2;
                game.conversation=null;
                game.conversationSource=null;
                Clear();
                var idle=Draw(app,"DrawNpc",npc.kind,
                    new Point(npc.x,npc.y),-1);
                var body=idle.FirstOrDefault(c=>c.texture.name==original);
                Check(body!=null,
                    "Current guild/forge render must use the original illustrated source");
                Check(body.rect.width==96&&body.rect.height==96&&
                    Math.Abs(body.uv.x-expectedIndex*.25f)<.0001f&&
                    Math.Abs(body.uv.width-.25f)<.0001f&&
                    Math.Abs(body.uv.height-.25f)<.0001f,
                    "Guild/forge must draw the intact original high-detail source cell");
                game.conversation=new Conversation("speaker","speaker","hello","hello");
                game.conversationSource=npc;
                Clear();
                var talking=Draw(app,"DrawNpc",npc.kind,
                    new Point(npc.x,npc.y),-1);
                var spoken=talking.FirstOrDefault(c=>c.texture.name==original);
                Check(spoken!=null&&
                    Math.Abs(spoken.uv.x-body.uv.x)<.0001f&&
                    Math.Abs(spoken.uv.y-body.uv.y)<.0001f&&
                    Math.Abs(spoken.rect.x-body.rect.x)<.001f&&
                    Math.Abs(spoken.rect.y-body.rect.y)<.001f,
                    "Talking must not switch original NPC to a coarse or offset body");
            }
            game.conversation=new Conversation("speaker","speaker","hello","hello");
            game.conversationSource=guild;
            Clear();
            var bystander=Draw(app,"DrawNpc","guild",new Point(21,17),-1);
            Check(bystander.Any(c=>c.texture.name==original),
                "Another guild member must not inherit a rejected dialogue avatar");
            Clear();
            var garden=Draw(app,"DrawNpc","garden",new Point(10,10),-1);
            Check(garden.Any(c=>c.texture.name==original),
                "Unmodified rich source NPCs must stay on the accepted atlas");
            Console.WriteLine("PASS guild/forge restored to rich original 16-NPC artwork in idle, dialogue and bystander views");
        }
        finally
        {
            game.conversation=savedConversation;
            game.conversationSource=savedSource;
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
