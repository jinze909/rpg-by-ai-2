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
        !c.texture.name.EndsWith("/ActorContactShadows")&&
        !c.texture.name.EndsWith("/EnemyReleaseDust")&&
        !c.texture.name.EndsWith("/EnemyHitSparks");}
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
    static int AtlasFrame(GUI.TextureCall call,int columns,int rows)
    {
        int column=(int)Math.Round(Math.Min(call.uv.x,
            call.uv.x+call.uv.width)*columns);
        int row=(int)Math.Round((1f-call.uv.y-
            Math.Abs(call.uv.height))*rows);
        return row*columns+column;
    }

    static void CompanionGaitRestartUsesPlantedSourceContact(FrontierRuntime app)
    {
        string[] names={"Mira","Ivo","Sen"};
        object style=Call(app,"get_CurrentWorldVisualLook");
        float factor=(float)style.GetType().GetField(
            "companionStrideDistanceFactor").GetValue(style);
        float stride=GameCore.WalkStride*factor;
        int total=0;
        foreach(int id in new[]{0,1,2})
        foreach(int heading in new[]{0,1,2,3})
        foreach(int passing in new[]{1,3})
        {
            app.BeginVisualReview(0,0);
            var game=app.Game;
            game.save.companion=id;
            game.save.reducedMotion=false;
            game.save.effectsQuality=2;
            game.companionAction=null;
            game.companionAim=Directions[heading];
            game.companionMoving=true;
            float raw=stride*(passing+.5f)/4f;
            game.companionMoveTime=raw;
            Point root=game.companionPosition;
            Point pixel=new Point(360,350);
            Place(app,root,pixel);
            Set(app,"clock",50f);
            int row=GameCore.DirectionRow(Directions[heading]);

            // Seed a genuine in-flight source phase, then stop on an airborne
            // key. This isolates the restart transition from arbitrary phase
            // selection used by the broader four-cell gait test.
            Set(app,"companionVisualId",id);
            Set(app,"companionVisualOwner",game);
            Set(app,"companionVisualGeneration",game.companionPresentationGeneration);
            Set(app,"companionLastWalkFrame",passing);
            Set(app,"companionLastWalkFacing",row);
            Set(app,"companionWasWalking",true);
            Set(app,"companionStopTime",49.99f);
            Set(app,"companionTrackedAction",null);
            Set(app,"companionAttackStartedFromWalk",false);
            Set(app,"companionGaitPhaseOwner",game);
            Set(app,"companionGaitPhaseSave",game.save);
            Set(app,"companionGaitPhaseId",id);
            Set(app,"companionGaitPhaseGeneration",game.companionPresentationGeneration);
            Set(app,"companionGaitPhaseRoom",game.save.room);
            Set(app,"companionGaitPhaseOffset",0f);
            Set(app,"companionGaitLastRawDistance",raw);
            Set(app,"companionGaitPhaseMoving",true);

            var moving=Draw(app,"DrawCompanionActor").Single(ActorTexture);
            Check(moving.texture.name.EndsWith("/Walk"+names[id])&&
                AtlasFrame(moving,4,3)%4==passing,
                "Companion restart fixture must begin on its real passing source cell");

            game.companionMoving=false;
            Set(app,"clock",50.01f);
            Call(app,"CompanionGaitDistance",true);
            var stopped=Draw(app,"DrawCompanionActor").Single(ActorTexture);
            int planted=passing==1?2:0;
            Check(AtlasFrame(stopped,4,3)%4==planted&&
                stopped.texture.name.EndsWith("/Walk"+names[id]),
                "Companion must stop on the next genuine planted source cell");

            game.companionMoving=true;
            game.companionMoveTime+=.02f;
            Set(app,"clock",50.03f);
            float phase=(float)Call(app,"CompanionGaitDistance",true);
            Check(GameCore.GaitFrame(phase,stride)==planted,
                "Companion restart phase must remain aligned with its planted source frame");
            var resumed=Draw(app,"DrawCompanionActor").Single(ActorTexture);
            Check(AtlasFrame(resumed,4,3)%4==planted&&
                resumed.texture.name.EndsWith("/Walk"+names[id])&&
                Math.Abs(resumed.rect.y-stopped.rect.y)<.001f&&
                Math.Abs(game.companionPosition.x-root.x)<.0001f&&
                Math.Abs(game.companionPosition.y-root.y)<.0001f,
                "Companion stop/restart must preserve one source body, foot root and real movement authority");
            total++;
        }
        Console.WriteLine("PASS companion source gait restart: "+total+
            " stop/restart cases rebase to planted cells 0/2 and share phase with footfall VFX");
    }

    // The canonical blonde actor must stop on a genuine planted Run source
    // cell before Idle, using the same frame resolver for native SpriteRenderer
    // and the whole-body GUI fallback. No root translation or action clock is
    // changed; all classes, cardinal facings, gait phases and interrupts run
    // through the real DrawPlayerActor call.
    static void HeroStopPlantNeverLeaksAcrossSameRoomSaveSwitch(FrontierRuntime app)
    {
        foreach(bool plantInProgress in new[]{false,true})
        {
            app.BeginVisualReview(0,0);
            var game=app.Game;
            game.enemies.Clear();
            game.save.weapon=0;game.save.profession=0;
            game.save.reducedMotion=false;
            game.save.effectsQuality=2;
            game.playerAction=null;
            game.playerHitTime=game.dashTime=game.deathTime=0f;
            game.locomotionFacing=Directions[0];
            game.SetAim(Directions[0]);
            game.movementDirection=Directions[0];
            game.walkDistance=GameCore.WalkStride*.375f;
            float start=930f+(plantInProgress?1f:0f);
            Set(app,"clock",start);
            Clear();var running=Draw(app,"DrawPlayerActor").Single(ActorTexture);
            Check(running.texture.name.EndsWith("Hero_Vanguard_Run_E")&&
                AtlasFrame(running,4,1)==1,
                "Saved-slot isolation begins with one genuine in-flight run keypose");
            if(plantInProgress)
            {
                game.movementDirection=new Point();
                Set(app,"clock",start+.01f);Clear();
                var planted=Draw(app,"DrawPlayerActor").Single(ActorTexture);
                Check(planted.texture.name.EndsWith("Hero_Vanguard_Run_E")&&
                    AtlasFrame(planted,4,1)==2,
                    "A genuine previously planted stop contact must be active before slot replacement");
            }
            int room=game.save.room;
            Point foot=game.Position;
            SaveData original=game.save;
            var next=new SaveData{
                room=room,x=foot.x,y=foot.y,
                profession=0,weapon=0,effectsQuality=2,
                reducedMotion=false
            };
            game.Load(next);
            Check(System.Object.ReferenceEquals(game.save,next)&&
                !System.Object.ReferenceEquals(original,next)&&
                game.save.room==room&&
                (game.Position-foot).Length<.01f,
                "A real load must replace the same-room SaveData without replacing the GameCore instance or moving hero feet");
            Set(app,"clock",start+.02f);
            Clear();var idle=Draw(app,"DrawPlayerActor").Single(ActorTexture);
            Check(idle.texture.name.EndsWith("Hero_Vanguard_Idle_E"),
                "Same-room save switch must not inherit an old running gait or previously held stop-plant body");
        }
        Console.WriteLine("PASS canonical hero stop-plant state retires on real same-room Load even when GameCore, appearance and foot position match");
    }

    static void HeroWalkStopUsesNextPlantedSourceCell(
        FrontierRuntime app,string folder)
    {
        string[] classes={"Vanguard","Ranger","Arcanist"};
        string[] facings={"E","E","N","S"};
        for(int outfit=0;outfit<3;outfit++)
        for(int heading=0;heading<4;heading++)
        for(int phase=0;phase<4;phase++)
        {
            app.BeginVisualReview(outfit,0);
            var game=app.Game;
            game.enemies.Clear();game.save.companion=-1;
            game.save.weapon=outfit;game.save.reducedMotion=false;
            game.save.effectsQuality=2;game.playerAction=null;
            game.playerHitTime=game.playerFlash=game.deathTime=
                game.dashTime=game.guardTime=0f;
            game.movementDirection=Directions[heading];
            game.locomotionFacing=Directions[heading];
            game.SetAim(Directions[heading]);
            game.walkDistance=GameCore.WalkStride*(phase+.5f)/4f;
            float start=180f+outfit*20f+heading*5f+phase;
            Set(app,"clock",start);
            Point pixel=new Point(500,350);
            Place(app,game.Position,pixel);
            Clear();
            var moving=Draw(app,"DrawPlayerActor").Single(ActorTexture);
            string runName="resource:Frontier/HeroAnimator/Canonical/Hero_"+
                classes[outfit]+"_Run_"+facings[heading];
            Check(moving.texture.name==runName&&
                AtlasFrame(moving,4,1)==phase,
                "Hero stop fixture must begin on the selected canonical Run keypose");
            Check((moving.uv.width<0)==(heading==1),
                "Horizontal facing must retain the canonical left/right mirror");

            game.movementDirection=new Point();
            float stopAt=start+.01f;
            Set(app,"clock",stopAt);Clear();
            var stop=Draw(app,"DrawPlayerActor").Single(ActorTexture);
            int planted=phase==1?2:phase==3?0:phase==2?2:-1;
            string idleName="resource:Frontier/HeroAnimator/Canonical/Hero_"+
                classes[outfit]+"_Idle_"+facings[heading];
            if(planted>=0)
                Check(stop.texture.name==runName&&AtlasFrame(stop,4,1)==planted,
                    "Hero must stop on the next authored planted Run frame, not an airborne pose");
            else
                Check(stop.texture.name==idleName,
                    "Already-planted phase zero should return directly to Idle");
            Check(Math.Abs(stop.rect.x-moving.rect.x)<.001f&&
                Math.Abs(stop.rect.y-moving.rect.y)<.001f&&
                Math.Abs(stop.rect.width-moving.rect.width)<.001f,
                "Hero stop transition must keep the canonical full-body world root and scale");

            Set(app,"clock",stopAt+.035f);Clear();
            var held=Draw(app,"DrawPlayerActor").Single(ActorTexture);
            if(planted>=0)
                Check(held.texture.name==runName&&AtlasFrame(held,4,1)==planted,
                    "Hero must hold its planted contact instead of replaying the airborne stride");
            else
                Check(held.texture.name==idleName,
                    "Phase-zero hero idle must not acquire a gait afterimage");

            // A planted WEST run and an EAST idle share the same atlas
            // row (E). Turning during the 75ms hold must discard the old
            // western foot plant, not mirror that obsolete whole-body cell.
            if(heading==1&&planted>=0)
            {
                game.locomotionFacing=Directions[0];
                game.SetAim(Directions[0]);
                Check(game.VisualFacing.x>.9f,
                    "Hero gait reversal fixture did not establish real east-facing Idle");
                Set(app,"clock",stopAt+.05f);Clear();
                var reversed=Draw(app,"DrawPlayerActor").Single(ActorTexture);
                Check(reversed.texture.name=="resource:Frontier/HeroAnimator/Canonical/Hero_"+
                    classes[outfit]+"_Idle_E"&&reversed.uv.width>0,
                    "West-to-east stop must cancel the old mirrored Run foot contact");
            }
            Set(app,"clock",stopAt+.09f);Clear();
            var idle=Draw(app,"DrawPlayerActor").Single(ActorTexture);
            Check(idle.texture.name==idleName&&
                Math.Abs(idle.rect.y-moving.rect.y)<.001f,
                "Hero stop plant must return to the equipped Idle atlas after its bounded hold");
            if(outfit==0&&heading==0&&phase==1)
                GUI.Export(folder,"review-hero-stop-planted-contact");
        }

        // Accessibility bypass: reduced motion never retains a running body.
        app.BeginVisualReview(0,0);
        var reduced=app.Game;
        reduced.save.weapon=0;reduced.save.reducedMotion=false;
        reduced.playerAction=null;reduced.movementDirection=Directions[0];
        reduced.locomotionFacing=Directions[0];reduced.SetAim(Directions[0]);
        reduced.walkDistance=GameCore.WalkStride*.875f;
        Set(app,"clock",400f);Clear();Draw(app,"DrawPlayerActor");
        reduced.movementDirection=new Point();reduced.save.reducedMotion=true;
        Set(app,"clock",400.01f);Clear();
        var quiet=Draw(app,"DrawPlayerActor").Single(ActorTexture);
        Check(quiet.texture.name==
            "resource:Frontier/HeroAnimator/Canonical/Hero_Vanguard_Idle_E",
            "Reduced motion must bypass hero stop-plant holds immediately");

        // Real combat action interrupts any pending gait contact; after it
        // ends the stale Run cell must not restart from a retired walk.
        reduced.save.reducedMotion=false;
        reduced.movementDirection=Directions[0];
        reduced.walkDistance=GameCore.WalkStride*.875f;
        Set(app,"clock",401f);Clear();Draw(app,"DrawPlayerActor");
        reduced.movementDirection=new Point();
        Set(app,"clock",401.01f);Clear();Draw(app,"DrawPlayerActor");
        reduced.playerAction=new ActorAction{
            kind="attack",weapon=0,equippedWeapon=0,profession=0,
            direction=Directions[0],duration=1f,contact=.4f
        };
        Set(app,"clock",401.02f);Clear();
        var attack=Draw(app,"DrawPlayerActor").Single(ActorTexture);
        Check(attack.texture.name==
            "resource:Frontier/HeroAnimator/Canonical/Hero_Vanguard_Attack_E",
            "A real attack must cancel the pending hero gait contact immediately");
        reduced.playerAction=null;
        Set(app,"clock",401.03f);Clear();
        var afterAttack=Draw(app,"DrawPlayerActor").Single(ActorTexture);
        Check(afterAttack.texture.name==
            "resource:Frontier/HeroAnimator/Canonical/Hero_Vanguard_Idle_E",
            "A completed attack must not resurrect the prior running frame");
        Console.WriteLine("PASS canonical hero stop-plant continuity: 48 class/facing/gait cases, 9 west-to-east reversal interrupts, reduced-motion and real attack interrupts");
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
                var glows=calls.Where(c=>c.texture.name=="soft_glow").ToArray();
                Check(glows.Length==2,"Companion must keep mature ground shadow and one soft ally marker");
                var ally=glows[1];
                Check(Math.Abs(ally.rect.width-48)<.0001f&&
                    Math.Abs(ally.rect.height-12)<.0001f&&
                    Math.Abs(ally.rect.x-(pixel.x-24))<1f&&
                    Math.Abs(ally.rect.y-(pixel.y-6))<1f&&
                    ally.color.g>ally.color.r&&
                    ally.color.a<.45f,
                    "Ally affiliation must read as subtle grounded light, not an opaque overpaint");
                float expectedAlpha=state<5?.18f:state==6?.42f:.29f;
                Check(Math.Abs(ally.color.a-expectedAlpha)<.00001f,
                    "Companion contact glow must communicate actual windup/release state");
                var face=calls.Where(ActorTexture).Last();Check(face.texture.name.EndsWith("/Walk"+names[id]),"Companion identity must match its own authored actor");Caption(pixel,"state "+state);total++;
            }GUI.Export(folder,"review-registered-companion-"+direction);
        }Console.WriteLine("PASS "+total+" companion bodies: same source face, fixed density and foot-rooted animated ally cue");
    }
    // Real production OnGUI output, not helper-only math. Four poses x four
    // facings x three separate original full-body art identities.
    static void CompanionGaitActuallyPlantsBothFeet(FrontierRuntime app)
    {
        string[] names={"Mira","Ivo","Sen"};
        for(int id=0;id<3;id++)
        for(int heading=0;heading<4;heading++)
        {
            app.BeginVisualReview(0,0);
            var g=app.Game;
            g.save.companion=id;
            g.save.reducedMotion=false;
            g.save.effectsQuality=2;
            g.companionAim=Directions[heading];
            Point pixel=new Point(325,350);
            Place(app,g.companionPosition,pixel);
            int row=GameCore.DirectionRow(Directions[heading]);
            string approved="resource:Frontier/Art/Walk"+names[id];
            string actions="resource:Frontier/Art/Companion"+names[id];
            const float stride=1.20f;
            // Static state: no need to artificially wait for a footstep
            // when drawing a bow or casting from standing still.
            g.companionMoving=false;
            g.companionAction=null;
            Set(app,"clock",9f);
            Clear();
            var initial=Draw(app,"DrawCompanionActor");
            Check(initial.Count(ActorTexture)==1&&
                initial.Single(ActorTexture).texture.name==approved,
                "Standing companion must share its approved full-body walk identity");
            var standingAction=new ActorAction{
                direction=Directions[heading],kind="command",
                contact=.20f,duration=.52f
            };
            g.companionAction=standingAction;
            Clear();var standCombat=Draw(app,"DrawCompanionActor");
            Check(standCombat.Single(ActorTexture).texture.name==actions,
                "Stationary companion commands must NOT freeze in walking pose");
            // Mira's real automatic heal has its own authored horizontal
            // staff-release cell (5). Previously only "command" selected it,
            // so a successful heal showed the generic sword/attack contact.
            if(id==0)
            {
                g.companionAction=new ActorAction{
                    direction=Directions[heading],kind="heal",
                    contact=.16f,duration=.42f,released=true
                };
                Clear();
                var healing=Draw(app,"DrawCompanionActor").Single(ActorTexture);
                int healColumn=(int)Math.Round(Math.Min(healing.uv.x,
                    healing.uv.x+healing.uv.width)*3f);
                int healRow=(int)Math.Round((1f-healing.uv.y-
                    Math.Abs(healing.uv.height))*4f);
                int healFrame=healRow*3+healColumn;
                int expectedHealFrame=heading==2?7:heading==3?10:5;
                Check(healFrame==expectedHealFrame&&
                    healing.texture.name==actions,
                    "Mira's real heal must select its authored magic-release cell for E/W/N/S");
            }
            g.companionAction=null;
            for(int phase=0;phase<4;phase++)
            {
                g.companionMoving=true;
                g.companionAim=Directions[heading];
                g.companionMoveTime=GameCore.WalkStride*stride*
                    (phase+.5f)/4f;
                Set(app,"companionGaitPhaseOwner",g);
                Set(app,"companionGaitPhaseSave",g.save);
                Set(app,"companionGaitPhaseId",id);
                Set(app,"companionGaitPhaseGeneration",g.companionPresentationGeneration);
                Set(app,"companionGaitPhaseRoom",g.save.room);
                Set(app,"companionGaitPhaseOffset",0f);
                Set(app,"companionGaitLastRawDistance",g.companionMoveTime);
                Set(app,"companionGaitPhaseMoving",true);
                Set(app,"clock",10f+phase);
                Clear();
                var moving=Draw(app,"DrawCompanionActor");
                var strideDraw=moving.Single(ActorTexture);
                Check(strideDraw.texture.name==approved,
                    "Moving companion lost its original source body");
                int source=(int)Math.Round(Math.Min(strideDraw.uv.x,
                    strideDraw.uv.x+strideDraw.uv.width)*4f);
                Check(source==phase&&
                    Math.Abs(strideDraw.uv.y-
                        (1f-(row+1)/3f))<.00001f,
                    "Distance-based animation skipped/duplicated a leg key");
                Check(Math.Abs(strideDraw.rect.width-192)<.01f&&
                    Math.Abs(strideDraw.rect.height-192)<.01f,
                    "Moving companion suddenly changed overall dimensions");

                // A stop may advance only to the next source-painted
                // foot contact, then must hold it briefly and settle. The old
                // 1->2->3->0 / 2->3->0 timer replayed airborne frame 3 while
                // the world root was stationary, visibly skating the boots.
                g.companionMoving=false;
                float stopAt=10.01f+phase;
                Set(app,"clock",stopAt);
                Call(app,"CompanionGaitDistance",true);
                Clear();
                var style=Call(app,"get_CurrentWorldVisualLook");
                float settle=(float)style.GetType().GetField(
                    "companionFootfallSeconds").GetValue(style);
                int plantedPhase=phase==1||phase==2?2:0;
                var firstStop=Draw(app,"DrawCompanionActor").Single(ActorTexture);
                int first=(int)Math.Round(Math.Min(firstStop.uv.x,
                    firstStop.uv.x+firstStop.uv.width)*4f);
                Check(first==plantedPhase&&firstStop.texture.name==approved&&
                    Math.Abs(firstStop.rect.y-strideDraw.rect.y)<.001f,
                    "Abrupt stop must choose the next authored planted source cell without shifting boots");
                Set(app,"clock",stopAt+settle*.5f);
                Clear();
                var held=Draw(app,"DrawCompanionActor").Single(ActorTexture);
                int heldPhase=(int)Math.Round(Math.Min(held.uv.x,
                    held.uv.x+held.uv.width)*4f);
                Check(heldPhase==plantedPhase&&held.texture.name==approved&&
                    Math.Abs(held.rect.y-strideDraw.rect.y)<.001f,
                    "Stop must hold the planted contact instead of replaying airborne gait");
                for(int n=1;n<=4;n++)
                {
                    Set(app,"clock",stopAt+settle*n+.002f);
                    Clear();
                    var settled=Draw(app,"DrawCompanionActor").Single(ActorTexture);
                    int actual=(int)Math.Round(Math.Min(settled.uv.x,
                        settled.uv.x+settled.uv.width)*4f);
                    Check(actual==0&&settled.texture.name==approved&&
                        Math.Abs(settled.rect.y-strideDraw.rect.y)<.001f,
                        "Stationary companion must settle to idle after one planted interval");
                }
                g.companionAction=new ActorAction{
                    direction=Directions[heading],kind="attack",
                    contact=.16f,duration=.42f
                };
                Clear();
                var afterStop=Draw(app,"DrawCompanionActor");
                Check(afterStop.Single(ActorTexture).texture.name==actions,
                    "Already grounded ally must immediately show real attack windup");
                g.companionAction=null;

                // Genuine walking -> action MUST immediately show the
                // full-body anticipation. A 24%-alpha entire previous
                // WALK body once made a second head/torso/weapon ghost;
                // only a tiny, grounded original Effects.png SOIL cell is
                // permitted underneath the single action-body Sprite.
                g.companionMoving=true;
                g.companionMoveTime=GameCore.WalkStride*stride*
                    (phase+.5f)/4f;
                Set(app,"clock",12f+phase);
                Clear();
                Draw(app,"DrawCompanionActor");
                g.companionMoving=false;
                g.companionAction=new ActorAction{
                    direction=Directions[heading],kind="command",
                    contact=.20f,duration=.52f
                };
                Clear();
                var planted=Draw(app,"DrawCompanionActor");
                var bodies=planted.Where(ActorTexture).ToArray();
                var marks=planted.Where(c=>c.texture.name==
                    "resource:Frontier/Art/Effects").ToArray();
                Check(bodies.Length==1&&marks.Length==1,
                    "Walk->skill rendered duplicate FULL bodies or lost real foot dust");
                var windup=bodies[0];
                var boot=marks[0];
                float realFoot=windup.rect.y+
                    windup.rect.height*120f/128f;
                Check(windup.texture.name==actions&&
                    boot.rect.width<=26f&&boot.rect.height<=18f&&
                    Math.Abs(boot.rect.y+boot.rect.height*.58f-realFoot)<.001f&&
                    boot.color.a>0f&&boot.color.a<=.221f&&
                    Math.Abs(windup.rect.y-strideDraw.rect.y)<.001f&&
                    Math.Abs(windup.rect.width-192f)<.001f,
                    "Walk->command must show ONLY current 192px body with grounded source-painted foot contact");
                g.companionAction.elapsed=.035f;
                Clear();
                var middle=Draw(app,"DrawCompanionActor");
                var middleActors=middle.Where(ActorTexture).ToArray();
                var fadedBoot=middle.Where(c=>c.texture.name==
                    "resource:Frontier/Art/Effects").ToArray();
                Check(middleActors.Length==1&&
                    middleActors[0].texture.name==actions&&
                    fadedBoot.Length==1&&
                    fadedBoot[0].color.a<boot.color.a&&
                    Math.Abs(fadedBoot[0].rect.y-boot.rect.y)<.001f&&
                    Math.Abs(middleActors[0].rect.y-windup.rect.y)<.001f,
                    "Boot contact faded while the single authored cast body and pivot stayed fixed");
                g.companionAction.elapsed=.10f;
                Clear();
                var firing=Draw(app,"DrawCompanionActor");
                var armed=firing.Single(ActorTexture);
                Check(armed.texture.name==actions&&
                    Math.Abs(armed.rect.y-strideDraw.rect.y)<.001f&&
                    Math.Abs(armed.rect.width-192)<.001f,
                    "Original bow/cast windup lost whole-body scale or boots");
                // Reduced motion skips old gait entirely but must still
                // paint the authoritative action body on its FIRST frame.
                g.companionAction=null;
                g.companionMoving=true;
                Clear();Draw(app,"DrawCompanionActor");
                g.companionMoving=false;
                g.companionAction=new ActorAction{
                    direction=Directions[heading],kind="command",
                    contact=.20f,duration=.52f
                };
                g.save.reducedMotion=true;
                Clear();var reduced=Draw(app,"DrawCompanionActor");
                Check(reduced.Count(ActorTexture)==1&&
                    reduced.Single(ActorTexture).texture.name==actions,
                    "Reduced motion must render current companion action without legacy gait");
                g.save.reducedMotion=false;
                g.companionAction=null;
            }
            // Regression: the old Mira->dismiss (no render)->Mira
            // sequence reused the same visualId and left the previous
            // airborne leg visible even though GameCore reset distance.
            g.companionMoving=true;
            g.companionMoveTime=GameCore.WalkStride*stride*.875f;
            Set(app,"clock",28f);
            Clear();Draw(app,"DrawCompanionActor");
            g.companionPresentationGeneration++;
            g.companionMoving=false;
            g.companionMoveTime=0f;
            Set(app,"clock",28.02f);
            Clear();
            var fresh=Draw(app,"DrawCompanionActor").Single(ActorTexture);
            int freshCell=(int)Math.Round(Math.Min(fresh.uv.x,
                fresh.uv.x+fresh.uv.width)*4f);
            Check(fresh.texture.name==approved&&freshCell==0,
                "Re-selected same companion must not retain hidden old airborne pose");
        }
        CompanionGaitRestartUsesPlantedSourceContact(app);
        Console.WriteLine("PASS 3 companions x 4 facings x 4 original gait phases: forward footfall recovery, exactly one whole-body action, source-painted soil contacts (no second head/weapon), reset and idle");
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
    static void VerticalArcherKeepsFacingThroughContact(FrontierRuntime app,string folder)
    {
        // Inspect the ACTUAL FrontierActors.DrawEnemyActor path using the
        // original 3x2 full-body EnemyArcherVertical.png, not an isolated
        // sprite-sheet calculator. All poses share the same grounded rect.
        app.BeginVisualReview(0,1);
        for(int heading=0;heading<2;heading++)
        {
            Point face=new Point(0,heading==0?-1:1);
            var foe=new Enemy(88810+heading,1,12,14,120){
                facing=face,state=1,timer=.18f,windupDuration=.65f,
                flash=.04f
            };
            GUI.TextureCall previous=null;
            for(int phase=0;phase<2;phase++)
            {
                if(phase==1) {
                    foe.state=0;foe.timer=0;foe.releaseTime=.12f;
                }
                Clear();Place(app,foe.Position,new Point(410+heading*300,350));
                var calls=Draw(app,"DrawEnemyActor",foe);
                var body=calls.FirstOrDefault(c=>c.texture.name==
                    "resource:Frontier/Art/EnemyArcherVertical");
                Check(body!=null,"Vertical archer must render original whole-body art");
                int index=(heading==0?0:3)+phase;
                float expectedU=(index%3)/3f;
                float expectedV=1f-(index/3+1)/2f;
                Check(Math.Abs(body.uv.x-expectedU)<.00001f &&
                    Math.Abs(body.uv.y-expectedV)<.00001f &&
                    Math.Abs(body.uv.width-1f/3f)<.00001f &&
                    Math.Abs(body.uv.height-.5f)<.00001f,
                    "Archer north/south windup and release crossed Sprite rows");
                Check(Math.Abs(body.rect.x-(410+heading*300-body.rect.width/2))<1.1f &&
                    Math.Abs(body.rect.y-(350-body.rect.height*120f/128f))<1.1f,
                    "Directional archer foot moved on contact");
                var flash=calls.FirstOrDefault(c=>c.texture.name.Contains(
                    "flash_EnemyArcherVertical")||c.texture.name.Contains(
                    "flash_resource_Frontier_Art_EnemyArcherVertical"));
                Check(flash!=null&&
                    Math.Abs(flash.uv.x-body.uv.x)<.00001f&&
                    Math.Abs(flash.uv.y-body.uv.y)<.00001f&&
                    Math.Abs(flash.rect.y-body.rect.y)<.001f,
                    "Archer hit flash and source body disagree on facing");
                if(previous!=null)
                    Check(Math.Abs(previous.uv.y-body.uv.y)<.00001f &&
                        Math.Abs(previous.rect.y-body.rect.y)<.001f,
                        "Vertical combat crosses character identity row");
                previous=body;
                GUI.Export(folder,"review-archer-"+heading+"-action-"+phase);
            }
            // Reproduce a true damage-reaction overlap with the in-flight
            // north/south arrow release. The combat pose selector chooses Hit
            // first, but the whole-body bow identity must not flash back to
            // a different monster for one reaction frame.
            foe.hitTime=.1f;
            foe.flash=.08f;
            foe.releaseTime=.12f;
            Clear();Place(app,foe.Position,new Point(410+heading*300,350));
            var interrupted=Draw(app,"DrawEnemyActor",foe);
            var contact=interrupted.FirstOrDefault(c=>c.texture.name==
                "resource:Frontier/Art/EnemyArcherVertical");
            var contactFlash=interrupted.FirstOrDefault(c=>c.texture.name.Contains(
                "flash_EnemyArcherVertical")||c.texture.name.Contains(
                "flash_resource_Frontier_Art_EnemyArcherVertical"));
            Check(contact!=null&&contactFlash!=null&&
                !interrupted.Any(c=>c.texture.name=="resource:Frontier/Art/Monsters"),
                "Hit during vertical arrow contact flashed a generic creature body");
            Check(Math.Abs(contact.uv.y-previous.uv.y)<.00001f&&
                Math.Abs(contact.uv.x-previous.uv.x)<.00001f&&
                Math.Abs(contactFlash.uv.x-contact.uv.x)<.00001f&&
                Math.Abs(contactFlash.uv.y-contact.uv.y)<.00001f&&
                Math.Abs(contact.rect.y-previous.rect.y)<.001f,
                "Vertical bow hit flash must retain release facing, source cell and boots");
            GUI.Export(folder,"review-archer-"+heading+"-contact-hit");
            foe.hitTime=0;
            foe.flash=0;
            foe.releaseTime=0;
            // The SAME accepted upright bowman must survive standstill,
            // actual GameCore walk, and isolated hit recovery. Before this
            // production fix, moving N/S swapped to an entirely unrelated
            // painted monster even though the bow was visible on attack.
            var anchoredCell=heading==0?0:3;
            for(int posture=0;posture<3;posture++)
            {
                foe.moving=posture==1;
                foe.moveTime=posture==1?.35f:0f;
                foe.hitTime=posture==2?.09f:0f;
                foe.flash=posture==2?.07f:0f;
                Clear();Place(app,foe.Position,
                    new Point(410+heading*300,350));
                var postureCalls=Draw(app,"DrawEnemyActor",foe);
                var sameBow=postureCalls.FirstOrDefault(c=>
                    c.texture.name==
                    "resource:Frontier/Art/EnemyArcherVertical");
                Check(sameBow!=null&&
                    !postureCalls.Any(c=>
                        c.texture.name=="resource:Frontier/Art/Monsters"||
                        c.texture.name=="resource:Frontier/Art/WalkMonsters")&&
                    Math.Abs(sameBow.uv.x-(anchoredCell%3)/3f)<.00001f&&
                    Math.Abs(sameBow.uv.y-
                        (1f-(anchoredCell/3+1)/2f))<.00001f&&
                    Math.Abs(sameBow.rect.y-
                        (350-sameBow.rect.height*120f/128f))<1.1f,
                    "Vertical archer idle/walk/hit changed class or lost source-pixel boots");
                if(posture==2)
                {
                    var flash=postureCalls.FirstOrDefault(c=>
                        c.texture.name.Contains("flash_EnemyArcherVertical")||
                        c.texture.name.Contains(
                            "flash_resource_Frontier_Art_EnemyArcherVertical"));
                    Check(flash!=null&&
                        Math.Abs(flash.uv.x-sameBow.uv.x)<.00001f&&
                        Math.Abs(flash.rect.y-sameBow.rect.y)<.001f,
                        "Isolated archer hit briefly flashed generic monster armor");
                }
                GUI.Export(folder,"review-archer-"+heading+
                    "-continuous-"+posture);
            }
            foe.moving=false;
            foe.hitTime=0;foe.flash=0;
            // The sixth cell on the same accepted atlas is a genuine
            // authored fallen archer. Death must not turn into a generic
            // monster/another face when live hitboxes are removed.
            Clear();
            var fallen=new FallenActor{actor=foe,room=1,life=.36f,duration=.65f};
            var corpse=Draw(app,"DrawFallen",fallen);
            var death=corpse.FirstOrDefault(c=>c.texture.name==
                "resource:Frontier/Art/EnemyArcherVertical");
            Check(death!=null&&
                Math.Abs(death.uv.x-2f/3f)<.00001f&&
                Math.Abs(death.uv.y)<.00001f&&
                Math.Abs(death.uv.width-1f/3f)<.00001f&&
                Math.Abs(death.uv.height-.5f)<.00001f,
                "Archer corpse must use its original full-body fallen cell 5");
            Check(!corpse.Any(c=>c.texture.name=="resource:Frontier/Art/Monsters"),
                "Death must not change archer into a generic monster");
            Check(Math.Abs(death.rect.y-(350-death.rect.height*120f/128f))<1.1f,
                "Fallen archer cannot slide away from its fixed foot root");
            GUI.Export(folder,"review-archer-"+heading+"-death");
        }
        // Same accepted Atlas used by the LIVING horizontal archer must
        // remain its identity on death. The N/S-only dead painting is not
        // a valid facing-independent fallback for E/W actors.
        foreach(float facingX in new[]{-1f,1f})
        {
            var foe=new Enemy(88920+(int)(facingX+1),1,12,14,120){
                facing=new Point(facingX,0),releaseTime=.12f
            };
            Clear();Place(app,foe.Position,new Point(520,350));
            var living=Draw(app,"DrawEnemyActor",foe);
            var before=living.FirstOrDefault(c=>c.texture.name==
                "resource:Frontier/Art/Monsters");
            Check(before!=null,"Horizontal archer must use accepted E/W body");
            Clear();
            var dead=new FallenActor{actor=foe,room=1,life=.36f,duration=.65f};
            var calls=Draw(app,"DrawFallen",dead);
            var after=calls.FirstOrDefault(c=>c.texture.name==
                "resource:Frontier/Art/Monsters");
            Check(after!=null&&!calls.Any(c=>c.texture.name==
                "resource:Frontier/Art/EnemyArcherVertical"),
                "Horizontal archer death suddenly changed to N/S-only painting");
            Check(Math.Abs(before.rect.x-after.rect.x)<.001f&&
                Math.Abs(before.rect.y-after.rect.y)<.001f&&
                Math.Abs(before.rect.width-after.rect.width)<.001f&&
                Math.Abs(after.uv.x-5f/6f)<.00001f,
                "Horizontal archer corpse lost original canvas/foot or death cell");
            GUI.Export(folder,"review-archer-horizontal-"+(facingX<0?"west":"east")+"-death");
        }
        Console.WriteLine("PASS north/south bowman identity through idle/run/hit/windup/contact/death and two original E/W rooted deaths");
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
        var stride=actor(moving,"WalkBossA");
        boss.hitTime=.09f;
        boss.flash=.08f;
        Clear();var impacted=Draw(app,"DrawEnemyActor",boss);
        var hitBody=actor(impacted,"WalkBossA");
        sameFrame(hitBody,mask(impacted,"WalkBossA"));
        Check(hitBody!=null&&
            Math.Abs(hitBody.uv.x-stride.uv.x)<.00001f&&
            Math.Abs(hitBody.uv.y-stride.uv.y)<.00001f&&
            Math.Abs(hitBody.rect.x-stride.rect.x)<.001f&&
            Math.Abs(hitBody.rect.y-stride.rect.y)<.001f&&
            Math.Abs(hitBody.rect.width-stride.rect.width)<.001f&&
            !impacted.Any(c=>c.texture.name==
                "resource:Frontier/Art/Bosses"),
            "Moving Boss hit reaction changed entire body, boots or GPU art identity");
        boss.hitTime=0;
        boss.flash=0;
        Clear();var resumed=Draw(app,"DrawEnemyActor",boss);
        var walkAfter=actor(resumed,"WalkBossA");
        Check(walkAfter!=null&&
            Math.Abs(walkAfter.uv.x-stride.uv.x)<.00001f&&
            Math.Abs(walkAfter.rect.y-stride.rect.y)<.001f,
            "Boss hit recovery jumped to a different moving source frame");
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
        Console.WriteLine("PASS source-consistent walking Boss hit/recovery + original windup/release, flash, footprint and room-2 HP tint");
    }

    // The 72-cell Briar Beast production path previously selected Death_0
    // on the lethal repaint, even if GameCore was drawing a real windup or
    // released-contact Sprite one frame earlier. Test the ACTUAL renderer
    // and full-body atlas UVs, not a stand-alone animation clock.
    static void BriarBeastDeathPreservesLastCombatFrame(FrontierRuntime app)
    {
        app.BeginVisualReview(0,1);
        var game=app.Game;
        bool originalReduced=game.save.reducedMotion;
        var field=typeof(FrontierRuntime).GetField("briarBeastSheet",Private);
        Check(field!=null,"Briar Beast production Sprite field missing");
        var original=(Texture2D)field.GetValue(app);
        var source=new Texture2D(1280,5760,TextureFormat.RGBA32,false){
            name="resource:Frontier/Art/BriarBeast"};
        field.SetValue(app,source);
        try
        {
            game.save.reducedMotion=false;
            var boss=new Enemy(94001,0,game.Position.x,game.Position.y,
                260f,true){
                state=1,timer=.3f,windupDuration=1f,
                facing=new Point(1,0),hitTime=.08f,flash=.06f
            };
            Place(app,boss.Position,new Point(500,350));
            Clear();
            var live=Draw(app,"DrawEnemyActor",boss)
                .FirstOrDefault(c=>c.texture==source);
            Check(live!=null,"Real Briar Beast live windup was not drawn");
            // Simulate an actual killing hit: the GameCore mutates HP before
            // its first corpse paint. Last living HP phase and contact must
            // not be recomputed from the new hp=0 phase-three silhouette.
            boss.hp=0f;
            boss.Face(new Point(-1,0)); // Death must retain rendered east pose.
            var fallen=new FallenActor{actor=boss,room=1,
                life=1f,duration=1f};
            Clear();
            var first=Draw(app,"DrawFallen",fallen)
                .Where(c=>c.texture==source).ToArray();
            Check(first.Length==1 &&
                Math.Abs(first[0].uv.x-live.uv.x)<.00001f &&
                Math.Abs(first[0].uv.y-live.uv.y)<.00001f &&
                Math.Abs(first[0].uv.width-live.uv.width)<.00001f &&
                Math.Abs(first[0].rect.y-live.rect.y)<.001f &&
                Math.Abs(first[0].rect.width-live.rect.width)<.001f,
                "First Boss death repaint flashed idle/dead instead of real windup");
            var visual=app.GetType().GetProperty(
                "CurrentWorldVisualLook",Private).GetValue(app,null);
            float fraction=(float)visual.GetType().GetField(
                "deathPoseBlendFraction",BindingFlags.Public|
                BindingFlags.Instance).GetValue(visual);
            Check(fraction>.01f&&fraction<.9f,
                "Missing bounded author-controlled full-body death blend");
            fallen.life=1f-fraction*.5f;
            Clear();
            var blended=Draw(app,"DrawFallen",fallen)
                .Where(c=>c.texture==source).ToArray();
            Check(blended.Length==2 &&
                Math.Abs(blended[0].uv.x-live.uv.x)<.00001f &&
                Math.Abs(blended[0].uv.y-live.uv.y)<.00001f &&
                Math.Abs(blended[0].color.a-blended[1].color.a)<.0001f &&
                Math.Abs(blended[0].rect.x-blended[1].rect.x)<.001f &&
                Math.Abs(blended[0].rect.y-blended[1].rect.y)<.001f &&
                Math.Abs(blended[0].uv.width-live.uv.width)<.00001f &&
                Math.Abs(blended[1].uv.width-live.uv.width)<.00001f &&
                Math.Abs(blended[1].uv.y)<.00001f,
                "Briar handoff used mismatched identity, facing, feet or alpha");
            fallen.life=1f-fraction-.05f;
            Clear();
            var finished=Draw(app,"DrawFallen",fallen)
                .Where(c=>c.texture==source).ToArray();
            Check(finished.Length==1&&
                Math.Abs(finished[0].uv.y)<.00001f,
                "Finished Briar animation retained a living attack body");
            game.save.reducedMotion=true;
            fallen.life=1f;
            Clear();
            var accessible=Draw(app,"DrawFallen",fallen)
                .Where(c=>c.texture==source).ToArray();
            Check(accessible.Length==1&&
                Math.Abs(accessible[0].uv.y)<.00001f,
                "Reduced motion must skip Boss death interpolation");
            Console.WriteLine(
                "PASS Briar Beast HP-zero lethal windup->Death: last actually drawn source cell retained, 18-row atlas, planted foot and reduced-motion corpse");
        }
        finally
        {
            field.SetValue(app,original);
            game.save.reducedMotion=originalReduced;
        }
    }

    // Real production path: the accepted detailed WalkBossA/B and Bosses
    // atlases are loaded by LoadVisuals. An injury-phase Boss used to turn
    // bright white on its lethal frame, even though the unchanged whole-body
    // Sprite and fixed boots were otherwise correctly crossfaded.

    // A cover/HUD outline samples the very same Boss actor at any HP stage,
    // but NEVER constitutes a finished visible paint. If the Boss is killed
    // before the next actual body pass, death must continue from the last
    // real full-body Sprite, not from a preview-only phase or pose.
    static void BossOutlineCannotRewriteLastLiveBodyOrStage(FrontierRuntime app)
    {
        var flags=BindingFlags.Instance|BindingFlags.NonPublic|
            BindingFlags.Public;
        var bossField=typeof(FrontierRuntime).GetField("bossPhaseCues",
            BindingFlags.Instance|BindingFlags.NonPublic);
        Check(bossField!=null,"Use the genuine living Boss weak snapshot cache");
        foreach(int room in new[]{1,2,5})
        {
            app.BeginVisualReview(0,room);
            var g=app.Game;
            bool oldMotion=g.save.reducedMotion;
            int oldQuality=g.save.effectsQuality;
            try
            {
                g.enemies.Clear();
                g.save.reducedMotion=false;g.save.effectsQuality=2;
                var boss=new Enemy(97500+room,0,g.Position.x,
                    g.Position.y,200f,true){
                    moving=true,moveTime=.65f,
                    facing=new Point(1,0)
                };
                Place(app,boss.Position,new Point(530f,355f));
                Set(app,"clock",710f+room*4f);
                Clear();
                var living=Draw(app,"DrawEnemyActor",boss)
                    .Where(ActorTexture).FirstOrDefault(t=>
                        t.texture.name.Contains("/BriarBeast")||
                        t.texture.name.Contains("/WalkBossA")||
                        t.texture.name.Contains("/WalkBossB"));
                Check(living!=null,"A genuine original full-body Boss is visible in room "+room);
                var lookup=bossField.GetValue(app);
                var tryGet=lookup.GetType().GetMethod("TryGetValue");
                object[] key={boss,null};
                Check((bool)tryGet.Invoke(lookup,key),"Living draw publishes a Boss snapshot");
                var snapshot=key[1];
                var fields=new[]{"phase","changedAt","lastVisibleRoom",
                    "lastVisiblePhase","lastBriarCell","lastBriarFlip",
                    "lastRegionalSheet","lastRegionalRows",
                    "lastRegionalFrame","lastRegionalSize",
                    "lastRegionalFlip","lastRegionalTint"}
                    .Select(name=>snapshot.GetType().GetField(name,flags))
                    .ToArray();
                Check(fields.All(f=>f!=null),"Boss keeps all authoritative live body fields");
                object[] prior=fields.Select(f=>f.GetValue(snapshot)).ToArray();
                Check((int)prior[2]==room&&
                    (int)prior[3]==boss.BossPhase,
                    "Last painted Boss identity and HP phase belong to this room");
                // A previous player action can change Boss HP and facing
                // before the next live repaint. Multiple cover outlines
                // must not consume the stage-cue or overwrite the exact
                // last walking source (including the final size and tint).
                boss.hp=20f;boss.moveTime+=.31f;
                boss.Face(new Point(-1,0));
                boss.moving=false;boss.state=1;
                boss.windupDuration=1f;boss.timer=.6f;
                Set(app,"clock",711f+room*4f);
                Set(app,"drawingOutline",true);
                for(int preview=0;preview<8;preview++)
                {Clear();Draw(app,"DrawEnemyActor",boss);}
                for(int field=0;field<fields.Length;field++)
                    Check(System.Object.Equals(prior[field],
                        fields[field].GetValue(snapshot)),
                        "Outline-only Boss phase/whole-body capture mutated "+
                        fields[field].Name+" before the REAL paint in room "+room);
                Set(app,"drawingOutline",false);
                boss.hp=0f;
                var dead=new FallenActor{
                    actor=boss,room=room,life=1f,duration=1f
                };
                Clear();
                var corpse=Draw(app,"DrawFallen",dead)
                    .FirstOrDefault(t=>t.texture==living.texture);
                Check(corpse!=null&&
                    Math.Abs(corpse.uv.x-living.uv.x)<.00001f&&
                    Math.Abs(corpse.uv.y-living.uv.y)<.00001f&&
                    Math.Abs(corpse.rect.width-living.rect.width)<.001f&&
                    Math.Abs(corpse.rect.y-living.rect.y)<.001f&&
                    Math.Abs(corpse.color.g-living.color.g)<.0001f,
                    "Actual lethal first body must replay the LAST living full-body Boss frame after preview-only HP/facing changes in room "+room);
                // Independent Boss: a preview that sees phase-three health
                // must not use up the 320ms flash before any live paint.
                var phaseBoss=new Enemy(97600+room,0,g.Position.x,
                    g.Position.y,200f,true){
                    moving=true,moveTime=.52f,
                    facing=new Point(1,0)
                };
                Set(app,"clock",740f+room*4f);
                Clear();Draw(app,"DrawEnemyActor",phaseBoss);
                key=new object[]{phaseBoss,null};
                Check((bool)tryGet.Invoke(lookup,key),
                    "Second genuine boss acquired its independent stage cue");
                snapshot=key[1];
                var phaseField=fields[0];var changedField=fields[1];
                phaseBoss.hp=20f;
                Set(app,"clock",741f+room*4f);
                Set(app,"drawingOutline",true);
                for(int preview=0;preview<8;preview++)
                {Clear();Draw(app,"DrawEnemyActor",phaseBoss);}
                Check((int)phaseField.GetValue(snapshot)==1&&
                    float.IsNegativeInfinity((float)changedField.GetValue(snapshot)),
                    "Preview-only Boss damage must not consume stage transition before live animation");
                Set(app,"drawingOutline",false);
                Clear();Draw(app,"DrawEnemyActor",phaseBoss);
                Check((int)phaseField.GetValue(snapshot)==phaseBoss.BossPhase&&
                    Math.Abs((float)changedField.GetValue(snapshot)-
                        (741f+room*4f))<.0001f,
                    "Actual first full-body repaint must start the still-unspent stage cue at its true visual contact time");
            }
            finally
            {
                Set(app,"drawingOutline",false);
                g.save.reducedMotion=oldMotion;
                g.save.effectsQuality=oldQuality;
                app.EndVisualReview();Clear();
            }
        }
        Console.WriteLine("PASS BriarBeast and both accepted WalkBoss sources: outline-only phase/pose changes never overwrite lethal body frame or consume still-unshown HP stage cue");
    }

    static void RegionalBossDeathKeepsLivePhasePalette(FrontierRuntime app)
    {
        app.BeginVisualReview(0,2);
        var game=app.Game;
        bool savedReduced=game.save.reducedMotion;
        try
        {
            game.save.reducedMotion=false;
            var boss=new Enemy(94003,0,game.Position.x,game.Position.y,200,true){
                facing=new Point(1,0),moving=true,moveTime=.65f
            };
            Place(app,boss.Position,new Point(530,360));
            for(int phase=1;phase<=3;phase++)
            {
                boss.hp=phase==1?200f:phase==2?100f:20f;
                float green=phase==1?1f:phase==2?.94f:.82f;
                float blue=phase==1?1f:phase==2?.90f:.83f;
                Clear();
                var alive=Draw(app,"DrawEnemyActor",boss)
                    .FirstOrDefault(c=>c.texture.name==
                        "resource:Frontier/Art/WalkBossA");
                Check(alive!=null&&Math.Abs(alive.color.g-green)<.0001f&&
                    Math.Abs(alive.color.b-blue)<.0001f,
                    "Live high-detail Boss palette changed unexpectedly");
                // The killing hit may overkill from ANY remaining HP stage.
                // Actual DrawFallen must not jump to phase-three palette.
                boss.hp=0f;
                var dead=new FallenActor{actor=boss,room=2,life=1f,duration=1f};
                Clear();
                var first=Draw(app,"DrawFallen",dead)
                    .Where(c=>c.texture.name==
                        "resource:Frontier/Art/WalkBossA").ToArray();
                Check(first.Length==1&&
                    Math.Abs(first[0].color.g-alive.color.g)<.0001f&&
                    Math.Abs(first[0].color.b-alive.color.b)<.0001f&&
                    Math.Abs(first[0].rect.y-alive.rect.y)<.001f,
                    "Lethal frame flashed white on accepted WalkBoss Sprite");
                dead.life=.88f;
                Clear();
                var blend=Draw(app,"DrawFallen",dead)
                    .Where(c=>c.texture.name==
                        "resource:Frontier/Art/WalkBossA"||
                        c.texture.name=="resource:Frontier/Art/Bosses")
                    .ToArray();
                Check(blend.Length==2&&
                    Math.Abs(blend[0].color.g-green)<.0001f&&
                    Math.Abs(blend[1].color.g-green)<.0001f&&
                    Math.Abs(blend[0].color.b-blue)<.0001f&&
                    Math.Abs(blend[1].color.b-blue)<.0001f&&
                    Math.Abs(blend[0].rect.y+blend[0].rect.height*120f/128f-
                        blend[1].rect.y-blend[1].rect.height*120f/128f)<.001f,
                    "Boss phase palette or planted foot changed during death fade");
                dead.life=.65f;
                Clear();
                var corpse=Draw(app,"DrawFallen",dead)
                    .FirstOrDefault(c=>c.texture.name==
                        "resource:Frontier/Art/Bosses");
                Check(corpse!=null&&
                    Math.Abs(corpse.color.g-green)<.0001f&&
                    Math.Abs(corpse.color.b-blue)<.0001f,
                    "Dead Boss suddenly reset phase-three armor color");
                game.save.reducedMotion=true;
                dead.life=1f;
                Clear();
                var fixedBody=Draw(app,"DrawFallen",dead)
                    .Where(c=>c.texture.name==
                        "resource:Frontier/Art/Bosses").ToArray();
                Check(fixedBody.Length==1&&
                    Math.Abs(fixedBody[0].color.g-green)<.0001f&&
                    Math.Abs(fixedBody[0].color.b-blue)<.0001f,
                    "Reduced-motion Boss death lost authentic phase tint");
                game.save.reducedMotion=false;
            }
            Console.WriteLine("PASS three actual regional Boss stages killed down to zero HP: last live palette retained throughout grounded death handoff and reduced-motion accessibility");
        }
        finally {game.save.reducedMotion=savedReduced;}
    }


    // Real scene-path regression: a regional Boss can be killed during any
    // action. HP-zero/AI cleanup changes the Enemy before DrawFallen; corpse
    // handoff must replay the LAST whole body actually painted, not a fresh
    // idle frame inferred from dead state. Includes both regional walk sheets,
    // flipped poses and the hit-flash tint that existed on the killing frame.
    static void RegionalBossDeathPreservesLastRenderedPose(FrontierRuntime app)
    {
        var game=app.Game;
        bool originalReduced=game.save.reducedMotion;
        try
        {
            game.save.reducedMotion=false;
            foreach(int room in new[]{2,5})
            {
                app.BeginVisualReview(0,room);
                for(int phase=1;phase<=3;phase++)
                for(int action=0;action<4;action++)
                {
                    var boss=new Enemy(94900+room*100+phase*10+action,0,
                        game.Position.x,game.Position.y,200,true);
                    boss.hp=phase==1?200f:phase==2?100f:20f;
                    boss.Face(new Point(-1,0));
                    boss.moving=action==0||action==3;
                    boss.moveTime=.65f+action*.17f;
                    boss.state=action==1?1:0;
                    boss.timer=.3f;boss.windupDuration=1f;
                    boss.releaseTime=action==2?.21f:0f;
                    boss.hitTime=action==3?.08f:0f;
                    boss.flash=action==3?.045f:0f;
                    Place(app,boss.Position,new Point(520,350));
                    Clear();
                    var alive=Draw(app,"DrawEnemyActor",boss)
                        .FirstOrDefault(c=>
                            c.texture.name=="resource:Frontier/Art/Bosses"||
                            c.texture.name=="resource:Frontier/Art/WalkBossA"||
                            c.texture.name=="resource:Frontier/Art/WalkBossB");
                    Check(alive!=null,"Missing real regional Boss live source");
                    // Killing damage may retire its attack, stop motion and
                    // rotate the enemy BEFORE the corpse's first repaint.
                    boss.hp=0f;
                    boss.state=0;boss.releaseTime=0;boss.hitTime=0;
                    boss.moving=false;boss.moveTime+=2.1f;
                    boss.Face(new Point(1,0));
                    var dead=new FallenActor{actor=boss,room=room,
                        life=1f,duration=1f};
                    Clear();
                    var first=Draw(app,"DrawFallen",dead)
                        .Where(c=>
                            c.texture.name=="resource:Frontier/Art/Bosses"||
                            c.texture.name=="resource:Frontier/Art/WalkBossA"||
                            c.texture.name=="resource:Frontier/Art/WalkBossB")
                        .ToArray();
                    Check(first.Length==1,
                        "HP-zero Boss must start from one real live Sprite");
                    var actual=first[0];
                    Check(actual.texture.name==alive.texture.name&&
                        Math.Abs(actual.uv.x-alive.uv.x)<.00001f&&
                        Math.Abs(actual.uv.y-alive.uv.y)<.00001f&&
                        Math.Abs(actual.uv.width-alive.uv.width)<.00001f&&
                        Math.Abs(actual.uv.height-alive.uv.height)<.00001f&&
                        Math.Abs(actual.rect.x-alive.rect.x)<.001f&&
                        Math.Abs(actual.rect.y-alive.rect.y)<.001f&&
                        Math.Abs(actual.rect.width-alive.rect.width)<.001f&&
                        Math.Abs(actual.rect.height-alive.rect.height)<.001f&&
                        Math.Abs(actual.color.r-alive.color.r)<.0001f&&
                        Math.Abs(actual.color.g-alive.color.g)<.0001f&&
                        Math.Abs(actual.color.b-alive.color.b)<.0001f&&
                        Math.Abs(actual.color.a-alive.color.a)<.0001f,
                        "Lethal Boss handoff changed source cell, facing, scale, foot or tint");
                    dead.life=.88f;
                    Clear();
                    var blended=Draw(app,"DrawFallen",dead)
                        .Where(c=>c.texture.name==
                            "resource:Frontier/Art/Bosses"||
                            c.texture.name=="resource:Frontier/Art/WalkBossA"||
                            c.texture.name=="resource:Frontier/Art/WalkBossB")
                        .ToArray();
                    Check(blended.Length==2&&
                        blended[0].texture.name==alive.texture.name&&
                        Math.Abs(blended[0].uv.x-alive.uv.x)<.00001f&&
                        Math.Abs(blended[0].uv.y-alive.uv.y)<.00001f&&
                        Math.Abs(blended[0].uv.width-alive.uv.width)<.00001f&&
                        Math.Abs(blended[1].uv.width-alive.uv.width)<.00001f&&
                        Math.Abs(blended[0].rect.y-alive.rect.y)<.001f,
                        "Regional Boss death crossfade switched the original cell");
                }
            }
            Console.WriteLine("PASS 24 live-to-death regional Boss transitions: two native art families, HP phases, walk/windup/release/hit, facing, foot and color");
        }
        finally{game.save.reducedMotion=originalReduced;}
    }

    static void MovingBossDeathStaysOnOneRoot(FrontierRuntime app)
    {
        app.BeginVisualReview(0,2);
        var g=app.Game;g.save.reducedMotion=false;
        var boss=new Enemy(93999,0,g.Position.x,g.Position.y,200,true){
            facing=new Point(-1f,0f),moving=true,moveTime=.8f
        };
        Point pixel=new Point(520f,350f);Place(app,boss.Position,pixel);
        Clear();var alive=Draw(app,"DrawEnemyActor",boss);
        var living=alive.FirstOrDefault(c=>c.texture.name==
            "resource:Frontier/Art/WalkBossA");
        Check(living!=null,"Approved walking Boss sheet missing");
        var corpse=new FallenActor{actor=boss,room=2,life=1f,duration=1f};
        Clear();var initial=Draw(app,"DrawFallen",corpse);
        var original=initial.FirstOrDefault(c=>c.texture.name==
            "resource:Frontier/Art/WalkBossA");
        Check(original!=null&&
            !initial.Any(c=>c.texture.name=="resource:Frontier/Art/Bosses")&&
            Math.Abs(original.rect.x-living.rect.x)<.001f&&
            Math.Abs(original.rect.y-living.rect.y)<.001f&&
            Math.Abs(original.rect.width-living.rect.width)<.001f&&
            Math.Abs(original.uv.x-living.uv.x)<.00001f&&
            Math.Abs(original.uv.y-living.uv.y)<.00001f,
            "Moving Boss first death frame swapped body/canvas/boots");
        corpse.life=.88f; // 12% elapsed; halfway through handoff
        Clear();var middle=Draw(app,"DrawFallen",corpse);
        var walk=middle.FirstOrDefault(c=>c.texture.name==
            "resource:Frontier/Art/WalkBossA");
        var fallen=middle.FirstOrDefault(c=>c.texture.name==
            "resource:Frontier/Art/Bosses");
        Check(walk!=null&&fallen!=null&&
            Math.Abs(walk.color.a-fallen.color.a)<.0001f&&
            walk.color.a>.4f&&walk.color.a<.6f&&
            Math.Abs(walk.rect.x+walk.rect.width*.5f-
                (fallen.rect.x+fallen.rect.width*.5f))<.001f&&
            Math.Abs(walk.rect.y+walk.rect.height*120f/128f-
                (fallen.rect.y+fallen.rect.height*120f/128f))<.001f,
            "Boss full-body death crossfade displaced original foot root");
        // An actual edit to the Unity Resources WorldLook value must
        // alter the same gameplay-rooted death image, not only a config.
        var property=typeof(FrontierRuntime).GetProperty(
            "CurrentWorldVisualLook",Private);
        Check(property!=null,"Death art-direction resource not connected");
        var look=property.GetValue(app,null);
        var tuning=look.GetType().GetField("deathPoseBlendFraction",
            BindingFlags.Instance|BindingFlags.Public);
        Check(tuning!=null,"Death blend width not artist editable");
        float originalDuration=(float)tuning.GetValue(look);
        try
        {
            tuning.SetValue(look,.16f);
            corpse.life=.88f;
            Clear();var shorter=Draw(app,"DrawFallen",corpse);
            var next=shorter.FirstOrDefault(c=>c.texture.name==
                "resource:Frontier/Art/Bosses");
            Check(next!=null&&next.color.a>.72f&&next.color.a<.78f,
                "Changing accepted art resource did not alter real Boss death");
        }
        finally{tuning.SetValue(look,originalDuration);}
        corpse.life=.72f;Clear();var settled=Draw(app,"DrawFallen",corpse);
        Check(!settled.Any(c=>c.texture.name==
                "resource:Frontier/Art/WalkBossA")&&
            settled.Any(c=>c.texture.name==
                "resource:Frontier/Art/Bosses"),
            "Finished Boss death retained standing walking Sprite");
        g.save.reducedMotion=true;
        corpse.life=1f;Clear();var accessible=Draw(app,"DrawFallen",corpse);
        Check(!accessible.Any(c=>c.texture.name==
                "resource:Frontier/Art/WalkBossA")&&
            accessible.Any(c=>c.texture.name==
                "resource:Frontier/Art/Bosses"),
            "Reduced-motion must skip Boss death handoff");
        // Lethal damage can land DURING an Idle -> Walk cross-atlas size
        // interpolation. The previous implementation remembered the NEXT
        // 384px walk canvas even though DrawEnemyAtlasHandoff had only
        // displayed the initial 320px whole-body size. The first corpse
        // frame then expanded the Boss by 20% in one repaint.
        g.save.reducedMotion=false;
        g.save.effectsQuality=2;
        var interrupted=new Enemy(94001,0,g.Position.x,g.Position.y,200,true){
            moving=false,moveTime=.12f,facing=new Point(1f,0f)
        };
        Set(app,"clock",33f);Clear();
        var ready=Draw(app,"DrawEnemyActor",interrupted);
        Check(ready.Any(c=>c.texture.name==
            "resource:Frontier/Art/Bosses"),"Boss Idle source not ready");
        interrupted.moving=true;interrupted.moveTime=.36f;
        Set(app,"clock",33.02f);Clear();
        var crossing=Draw(app,"DrawEnemyActor",interrupted);
        var paintedCrossing=crossing.FirstOrDefault(c=>c.texture.name==
            "resource:Frontier/Art/WalkBossA");
        Check(paintedCrossing!=null&&
            crossing.Any(c=>c.texture.name==
                "resource:Frontier/Art/Bosses"),
            "Must reproduce a REAL cross-atlas whole-body Boss handoff");
        interrupted.hp=0f;
        var suddenDeath=new FallenActor{
            actor=interrupted,room=2,life=1f,duration=1f
        };
        Clear();var sameFrameCorpse=Draw(app,"DrawFallen",suddenDeath);
        var rememberedCrossing=sameFrameCorpse.FirstOrDefault(c=>
            c.texture.name=="resource:Frontier/Art/WalkBossA");
        Check(rememberedCrossing!=null&&
            !sameFrameCorpse.Any(c=>c.texture.name==
                "resource:Frontier/Art/Bosses")&&
            Math.Abs(rememberedCrossing.rect.width-
                paintedCrossing.rect.width)<.001f&&
            Math.Abs(rememberedCrossing.rect.height-
                paintedCrossing.rect.height)<.001f&&
            Math.Abs(rememberedCrossing.rect.x-
                paintedCrossing.rect.x)<.001f&&
            Math.Abs(rememberedCrossing.rect.y-
                paintedCrossing.rect.y)<.001f&&
            Math.Abs(rememberedCrossing.uv.x-
                paintedCrossing.uv.x)<.00001f&&
            Math.Abs(rememberedCrossing.uv.y-
                paintedCrossing.uv.y)<.00001f,
            "Lethal hit during Boss size easing popped the corpse head/body canvas");
        suddenDeath.life=.88f;Clear();
        var dyingCrossfade=Draw(app,"DrawFallen",suddenDeath);
        Check(dyingCrossfade.Any(c=>c.texture.name==
            "resource:Frontier/Art/WalkBossA")&&
            dyingCrossfade.Any(c=>c.texture.name==
            "resource:Frontier/Art/Bosses"),
            "Mid-handoff lethal Boss did not continue into published death Sprite");
        Console.WriteLine("PASS lethal Boss during Idle/Walk canvas easing: exact last visible painted size, planted corpse, no 20% frame pop");
        Console.WriteLine("PASS moving Boss death: accepted stride/boots, rooted whole-body transition and accessible fixed corpse");
    }

    static void EnemyLethalSnapshotSurvivesRetiredAI(FrontierRuntime app)
    {
        app.BeginVisualReview(0,1);
        var g=app.Game;bool previousReduced=g.save.reducedMotion;
        try
        {
            g.save.reducedMotion=false;
            // All established detailed normal enemies; two independently
            // painted bow directions must never select another atlas after
            // AI clears attack/release/movement on HP-zero.
            for(int form=0;form<5;form++)
            {
                int kind=form<3?form:1;
                var foe=new Enemy(95900+form,kind,
                    g.Position.x,g.Position.y,120);
                foe.Face(form==3?new Point(0,-1):
                    form==4?new Point(0,1):new Point(-1,0));
                foe.moving=form==0||form==2;
                foe.moveTime=.69f;
                foe.state=form==1?1:0;
                foe.windupDuration=.8f;foe.timer=.2f;
                foe.releaseTime=form>=3?.12f:0;
                foe.hitTime=form==2?.08f:0;
                Place(app,foe.Position,new Point(430+form*65,350));
                Clear();
                var alive=Draw(app,"DrawEnemyActor",foe)
                    .FirstOrDefault(c=>c.texture.name==
                        "resource:Frontier/Art/Monsters"||
                        c.texture.name=="resource:Frontier/Art/WalkMonsters"||
                        c.texture.name==
                        "resource:Frontier/Art/EnemyArcherVertical");
                Check(alive!=null,"Missing accepted whole-body enemy art");
                foe.hp=0f;foe.state=0;foe.releaseTime=0;
                foe.hitTime=0;foe.moving=false;foe.moveTime+=1.8f;
                foe.Face(new Point(1,0));
                var dead=new FallenActor{actor=foe,room=1,
                    life=1f,duration=1f};
                Clear();
                var firstDraw=Draw(app,"DrawFallen",dead);
                // Native SpriteRenderer/Animator capture is a complete-body
                // source, never a separate upper/lower puppet. The accepted
                // shadow MUST render first, UNDER the planted boots.
                Check(firstDraw.Length>=2&&
                    firstDraw[0].texture.name=="soft_glow"&&
                    firstDraw[1].texture.name==alive.texture.name,
                    "Lethal shadow layered over boots or first pose displaced");
                var atContact=firstDraw
                    .Where(c=>c.texture.name==
                        "resource:Frontier/Art/Monsters"||
                        c.texture.name=="resource:Frontier/Art/WalkMonsters"||
                        c.texture.name==
                        "resource:Frontier/Art/EnemyArcherVertical")
                    .ToArray();
                Check(atContact.Length==1,
                    "Initial lethal enemy repaint must retain a single source body");
                var first=atContact[0];
                Check(first.texture.name==alive.texture.name&&
                    Math.Abs(first.uv.x-alive.uv.x)<.00001f&&
                    Math.Abs(first.uv.y-alive.uv.y)<.00001f&&
                    Math.Abs(first.uv.width-alive.uv.width)<.00001f&&
                    Math.Abs(first.rect.x-alive.rect.x)<.001f&&
                    Math.Abs(first.rect.y-alive.rect.y)<.001f&&
                    Math.Abs(first.rect.width-alive.rect.width)<.001f&&
                    Math.Abs(first.rect.height-alive.rect.height)<.001f,
                    "Killing hit changed enemy face, atlas, gait or planted feet");
                dead.life=.88f;Clear();
                var crossfade=Draw(app,"DrawFallen",dead)
                    .Where(c=>c.texture.name==
                        "resource:Frontier/Art/Monsters"||
                        c.texture.name=="resource:Frontier/Art/WalkMonsters"||
                        c.texture.name==
                        "resource:Frontier/Art/EnemyArcherVertical")
                    .ToArray();
                Check(crossfade.Length==2&&
                    crossfade[0].texture.name==alive.texture.name&&
                    Math.Abs(crossfade[0].uv.x-alive.uv.x)<.00001f&&
                    Math.Abs(crossfade[0].uv.y-alive.uv.y)<.00001f&&
                    Math.Abs(crossfade[0].uv.width-alive.uv.width)<.00001f&&
                    Math.Abs(crossfade[1].uv.width-alive.uv.width)<.00001f,
                    "Enemy corpse crossfade changed its published facing or action");
                dead.life=.68f;Clear();
                var settled=Draw(app,"DrawFallen",dead);
                Check(settled.Count(c=>c.texture.name==
                    "resource:Frontier/Art/EnemyArcherVertical"||
                    c.texture.name=="resource:Frontier/Art/Monsters")==1,
                    "Enemy failed to settle into one actual authored death body");
                // Reduced-motion must not reinsert the remembered attack
                // Sprite or run a synthetic interpolation phase. Keep the
                // genuinely painted, class-matching fixed corpse instead.
                g.save.reducedMotion=true;dead.life=1f;Clear();
                var accessible=Draw(app,"DrawFallen",dead);
                string deathSource=form>=3?
                    "resource:Frontier/Art/EnemyArcherVertical":
                    "resource:Frontier/Art/Monsters";
                Check(accessible.Count(c=>c.texture.name==deathSource)==1&&
                    accessible.Count(c=>c.texture.name==
                        "resource:Frontier/Art/WalkMonsters")==0,
                    "Reduced-motion corpse used an old combat frame or wrong source");
                g.save.reducedMotion=false;
            }
            Console.WriteLine("PASS five lethal regular enemy handoffs: exact accepted whole-body frames, shadow behind boots, facing, and reduced-motion corpse");
        }
        finally{g.save.reducedMotion=previousReduced;}
    }

    static void PatrolDeathRetainsWholeBodyFootfall(FrontierRuntime app)
    {
        app.BeginVisualReview(0,1);
        var game=app.Game;game.save.reducedMotion=false;
        foreach(int kind in new[]{0,1,2})
        {
            // Vertical archers have their own source-authored fallen art
            // and are already validated separately; use E/W here.
            var foe=new Enemy(94600+kind,kind,game.Position.x,
                game.Position.y,96f){
                facing=new Point(-1f,0f),moving=true,
                moveTime=.68f,backpedaling=kind==1
            };
            Point feet=new Point(420+kind*160,350);
            Place(app,foe.Position,feet);
            Clear();var live=Draw(app,"DrawEnemyActor",foe);
            var living=live.FirstOrDefault(c=>c.texture.name==
                "resource:Frontier/Art/WalkMonsters");
            Check(living!=null,"Accepted moving enemy body missing");
            var dead=new FallenActor{actor=foe,room=1,
                life=.65f,duration=.65f};
            Clear();var initial=Draw(app,"DrawFallen",dead);
            var first=initial.FirstOrDefault(c=>c.texture.name==
                "resource:Frontier/Art/WalkMonsters");
            Check(first!=null&&
                !initial.Any(c=>c.texture.name==
                    "resource:Frontier/Art/Monsters")&&
                Math.Abs(first.uv.x-living.uv.x)<.00001f&&
                Math.Abs(first.uv.y-living.uv.y)<.00001f&&
                Math.Abs(first.rect.x-living.rect.x)<.001f&&
                Math.Abs(first.rect.y-living.rect.y)<.001f,
                "First death frame popped a different raider/guard/bowman");
            dead.life=.572f; // ~12% of .65sec death
            Clear();var blend=Draw(app,"DrawFallen",dead);
            var stride=blend.FirstOrDefault(c=>c.texture.name==
                "resource:Frontier/Art/WalkMonsters");
            var corpse=blend.FirstOrDefault(c=>c.texture.name==
                "resource:Frontier/Art/Monsters");
            Check(stride!=null&&corpse!=null&&
                Math.Abs(stride.color.a-corpse.color.a)<.0001f&&
                Math.Abs(stride.rect.x+stride.rect.width/2-
                    corpse.rect.x-corpse.rect.width/2)<.001f&&
                Math.Abs(stride.rect.y+
                    stride.rect.height*120f/128f-corpse.rect.y-
                    corpse.rect.height*120f/128f)<.001f,
                "Walking enemy death handoff lost one stable root or alpha");
            dead.life=.40f;
            Clear();var settled=Draw(app,"DrawFallen",dead);
            Check(!settled.Any(c=>c.texture.name==
                    "resource:Frontier/Art/WalkMonsters")&&
                settled.Any(c=>c.texture.name==
                    "resource:Frontier/Art/Monsters"),
                "Finished death retained a standing patrol body");
            game.save.reducedMotion=true;
            dead.life=.65f;
            Clear();var fixedFrame=Draw(app,"DrawFallen",dead);
            Check(!fixedFrame.Any(c=>c.texture.name==
                    "resource:Frontier/Art/WalkMonsters")&&
                fixedFrame.Any(c=>c.texture.name==
                    "resource:Frontier/Art/Monsters"),
                "Accessible death must show one original fixed corpse");
            game.save.reducedMotion=false;
        }
        Console.WriteLine("PASS 3 actual raider/archer/guard moving deaths: stable pixel feet, accepted full-body handoff, backpedal and reduced-motion");
    }
    static void StationaryDeathUsesActualLastPose(FrontierRuntime app)
    {
        // Most lethal strikes set hitTime even on a stationary actor.
        // Test the ACTUAL production DrawEnemyActor source frame just
        // before death, not merely an assumed generic Idle picture.
        app.BeginVisualReview(0,2);
        var g=app.Game;
        g.save.reducedMotion=false;
        var boss=new Enemy(94721,0,g.Position.x,g.Position.y,170,true){
            facing=new Point(-1,0),moving=false,hitTime=.09f
        };
        Point bossFeet=new Point(520,360);
        Place(app,boss.Position,bossFeet);
        Clear();var before=Draw(app,"DrawEnemyActor",boss);
        var live=before.FirstOrDefault(c=>
            c.texture.name=="resource:Frontier/Art/Bosses");
        Check(live!=null&&Math.Abs(live.uv.x)<.00001f,
            "Stationary live Boss source not available");
        var dead=new FallenActor{actor=boss,room=2,life=1f,duration=1f};
        Clear();var start=Draw(app,"DrawFallen",dead);
        var first=start.FirstOrDefault(c=>
            c.texture.name=="resource:Frontier/Art/Bosses");
        Check(first!=null&&start.Count(c=>
                c.texture.name=="resource:Frontier/Art/Bosses")==1&&
            Math.Abs(first.uv.x-live.uv.x)<.00001f&&
            Math.Abs(first.uv.y-live.uv.y)<.00001f&&
            Math.Abs(first.rect.x-live.rect.x)<.001f&&
            Math.Abs(first.rect.y-live.rect.y)<.001f,
            "First stationary Boss death frame changed complete body/root");
        dead.life=.88f;
        Clear();var dual=Draw(app,"DrawFallen",dead).Where(c=>
            c.texture.name=="resource:Frontier/Art/Bosses").ToArray();
        Check(dual.Length==2&&
            Math.Abs(dual[0].uv.x)<.00001f&&
            Math.Abs(dual[1].uv.x-.75f)<.00001f&&
            Math.Abs(dual[0].color.a-dual[1].color.a)<.0001f&&
            Math.Abs(dual[0].rect.y-dual[1].rect.y)<.001f,
            "Standing Boss death failed original full-body rooted handoff");
        dead.life=.7f;
        Clear();var complete=Draw(app,"DrawFallen",dead).Where(c=>
            c.texture.name=="resource:Frontier/Art/Bosses").ToArray();
        Check(complete.Length==1&&
            Math.Abs(complete[0].uv.x-.75f)<.00001f,
            "Standing Boss death never finishes on genuine corpse");
        g.save.reducedMotion=true;
        dead.life=1f;
        Clear();var accessible=Draw(app,"DrawFallen",dead).Where(c=>
            c.texture.name=="resource:Frontier/Art/Bosses").ToArray();
        Check(accessible.Length==1&&
            Math.Abs(accessible[0].uv.x-.75f)<.00001f,
            "Reduced-motion standing Boss death must use one corpse");
        g.save.reducedMotion=false;
        app.BeginVisualReview(0,1);
        g=app.Game;g.save.reducedMotion=false;
        for(int kind=0;kind<3;kind++)
        {
            var foe=new Enemy(94730+kind,kind,g.Position.x,
                g.Position.y,120){
                facing=new Point(1,0),moving=false,hitTime=.09f
            };
            Place(app,foe.Position,new Point(420+kind*120,350));
            Clear();var original=Draw(app,"DrawEnemyActor",foe)
                .FirstOrDefault(c=>
                    c.texture.name=="resource:Frontier/Art/Monsters");
            Check(original!=null,"Stationary enemy hit picture missing");
            var corpse=new FallenActor{actor=foe,room=1,
                life=.65f,duration=.65f};
            Clear();var firstFrame=Draw(app,"DrawFallen",corpse)
                .Where(c=>c.texture.name==
                    "resource:Frontier/Art/Monsters").ToArray();
            Check(firstFrame.Length==1&&
                Math.Abs(firstFrame[0].uv.x-original.uv.x)<.00001f&&
                Math.Abs(firstFrame[0].uv.y-original.uv.y)<.00001f&&
                Math.Abs(firstFrame[0].rect.y-original.rect.y)<.001f,
                "Stationary raider/archer/guard death replaced the hit body");
            corpse.life=.572f;
            Clear();var blend=Draw(app,"DrawFallen",corpse)
                .Where(c=>c.texture.name==
                    "resource:Frontier/Art/Monsters").ToArray();
            Check(blend.Length==2&&
                Math.Abs(blend[0].color.a-blend[1].color.a)<.0001f&&
                Math.Abs(blend[0].rect.y-blend[1].rect.y)<.001f&&
                Math.Abs(blend[1].uv.x-5f/6f)<.00001f,
                "Stationary enemy full-body pose did not fade into corpse");
            g.save.reducedMotion=true;corpse.life=.65f;
            Clear();var fixedCorpse=Draw(app,"DrawFallen",corpse)
                .Where(c=>c.texture.name==
                    "resource:Frontier/Art/Monsters").ToArray();
            Check(fixedCorpse.Length==1&&
                Math.Abs(fixedCorpse[0].uv.x-5f/6f)<.00001f,
                "Reduced-motion enemy death did not use accepted corpse");
            g.save.reducedMotion=false;
        }
        Console.WriteLine("PASS stationary Boss + three enemy classes: real last-hit full-body Sprite, two rooted death frames, final corpse, reduced motion");
    }
    static void VerticalArcherDeathHandoffUsesBowIdentity(FrontierRuntime app)
    {
        app.BeginVisualReview(0,1);
        var g=app.Game;
        g.save.reducedMotion=false;
        for(int direction=0;direction<2;direction++)
        for(int release=0;release<2;release++)
        {
            int row=direction==0?0:1;
            int baseCell=row*3;
            var foe=new Enemy(94880+direction*2+release,1,
                g.Position.x,g.Position.y,100){
                facing=new Point(0,direction==0?-1f:1f),
                releaseTime=release==1?.12f:0,
                hitTime=.09f,flash=.06f
            };
            Place(app,foe.Position,new Point(420+direction*160,345));
            Clear();var before=Draw(app,"DrawEnemyActor",foe)
                .FirstOrDefault(c=>c.texture.name==
                    "resource:Frontier/Art/EnemyArcherVertical");
            Check(before!=null&&
                Math.Abs(before.uv.x-
                    (float)(release==1?1:0)/3f)<.00001f,
                "Living archer release pose not sourced from its own atlas");
            var fallen=new FallenActor{actor=foe,room=1,
                life=.65f,duration=.65f};
            Clear();var start=Draw(app,"DrawFallen",fallen)
                .Where(c=>c.texture.name==
                    "resource:Frontier/Art/EnemyArcherVertical").ToArray();
            Check(start.Length==1&&
                Math.Abs(start[0].uv.x-before.uv.x)<.00001f&&
                Math.Abs(start[0].uv.y-before.uv.y)<.00001f&&
                Math.Abs(start[0].rect.x-before.rect.x)<.001f&&
                Math.Abs(start[0].rect.y-before.rect.y)<.001f,
                "North/south archer swapped face/bow or foot on lethal hit");
            fallen.life=.572f;
            Clear();var mid=Draw(app,"DrawFallen",fallen)
                .Where(c=>c.texture.name==
                    "resource:Frontier/Art/EnemyArcherVertical").ToArray();
            Check(mid.Length==2&&
                Math.Abs(mid[0].uv.x-before.uv.x)<.00001f&&
                Math.Abs(mid[1].uv.x-2f/3f)<.00001f&&
                Math.Abs(mid[1].uv.y)<.00001f&&
                Math.Abs(mid[0].color.a-mid[1].color.a)<.0001f&&
                Math.Abs(mid[0].rect.y-mid[1].rect.y)<.001f,
                "Death crossfade abandoned same-atlas archer silhouette");
            fallen.life=.40f;
            Clear();var complete=Draw(app,"DrawFallen",fallen)
                .Where(c=>c.texture.name==
                    "resource:Frontier/Art/EnemyArcherVertical").ToArray();
            Check(complete.Length==1&&
                Math.Abs(complete[0].uv.x-2f/3f)<.00001f,
                "Late archer death should show only genuine fallen body");
            g.save.reducedMotion=true;
            fallen.life=.65f;
            Clear();var accessible=Draw(app,"DrawFallen",fallen)
                .Where(c=>c.texture.name==
                    "resource:Frontier/Art/EnemyArcherVertical").ToArray();
            Check(accessible.Length==1&&
                Math.Abs(accessible[0].uv.x-2f/3f)<.00001f,
                "Reduced-motion archer corpse cannot animate");
            g.save.reducedMotion=false;
        }
        Console.WriteLine(
            "PASS N/S bowman lethal hit and contact: source bow identity, rooted whole Sprite handoff, same atlas authentic fallen cell and reduced motion");
    }
    static void EnemyAttackFramesHoldThroughFlash(FrontierRuntime app)
    {
        app.BeginVisualReview(0,2);
        var g=app.Game;
        foreach(int kind in new[]{0,1,2,3})
        {
            // All original enemy families including a regional Boss: the
            // live windup/release must survive a damage flash. A genuinely
            // cancelled attack (stagger resets state) uses Hit as normal.
            var foe=new Enemy(94930+kind,kind==3?0:kind,
                g.Position.x,g.Position.y,250f,kind==3){
                facing=kind==1?new Point(0,-1):new Point(1,0),
                windupDuration=1.1f,timer=.45f,state=1,
                hitTime=.09f,flash=.08f
            };
            string name=kind==3?"Bosses":
                kind==1?"EnemyArcherVertical":"Monsters";
            Clear();var preparing=Draw(app,"DrawEnemyActor",foe);
            var body=preparing.FirstOrDefault(c=>c.texture.name==
                "resource:Frontier/Art/"+name);
            int col=kind==3?1:kind==1?0:2;
            int columns=kind==3?4:kind==1?3:6;
            Check(body!=null&&Math.Abs(body.uv.x-
                (float)col/columns)<.00001f,
                "Enemy windup reverted to Hit/Idle pose during flash: "+name);
            var outline=preparing.FirstOrDefault(c=>
                c.texture.name=="resource:Frontier/Art/ActorMasks/flash_"+name||
                c.texture.name=="flash_resource_Frontier_Art_"+name);
            Check(outline!=null&&
                Math.Abs(body.uv.x-outline.uv.x)<.00001f&&
                Math.Abs(body.rect.y-outline.rect.y)<.001f,
                "Damage flash masked an unrelated attack Sprite: "+name);
            foe.state=0;foe.releaseTime=.14f;
            Clear();var releasing=Draw(app,"DrawEnemyActor",foe);
            var contact=releasing.FirstOrDefault(c=>c.texture.name==
                "resource:Frontier/Art/"+name);
            Check(contact!=null&&
                Math.Abs(contact.uv.x-
                    (float)(kind==3?2:kind==1?1:3)/columns)<.00001f,
                "Hit briefly cancelled an already released attack frame: "+name);
            foe.releaseTime=0;foe.hitTime=.09f;
            Clear();var canceled=Draw(app,"DrawEnemyActor",foe);
            var reactive=canceled.FirstOrDefault(c=>c.texture.name==
                "resource:Frontier/Art/"+name);
            int expected=kind==3?0:kind==1?0:4;
            Check(reactive!=null&&
                Math.Abs(reactive.uv.x-(float)expected/columns)<.00001f,
                "Stagger-cancelled attack did not recover to Hit frame: "+name);
        }
        Console.WriteLine("PASS 4 real enemy/Boss classes: windup/release source frames persist through actual hit flash and cancelled attacks still react");
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

    // CI MUST execute the real DrawNpc production branch (not only an
    // isolated frame preview), and verify the speaker is the pinned
    // dialogue source. No low-resolution body replacement is allowed.
    public static void VerifyNpcActionSignalProduction(FrontierRuntime app)
    {
        app.BeginVisualReview(0,2);
        var game=app.Game;
        string original="resource:Frontier/Art/Npcs";
        string signals="resource:Frontier/Art/NpcActionSignals";
        var npc=new WorldObject{
            id="speech-qa",kind="guild",
            x=game.Position.x+1f,y=game.Position.y+.5f
        };
        var conversation=game.conversation;
        var source=game.conversationSource;
        bool reduced=game.save.reducedMotion;
        int quality=game.save.effectsQuality;
        try
        {
            game.save.effectsQuality=2;
            game.save.reducedMotion=false;
            game.conversation=null;game.conversationSource=null;
            Set(app,"clock",2.02f);Clear();
            var idle=Draw(app,"DrawNpc","guild",
                new Point(npc.x,npc.y),-1);
            Check(idle.Count(c=>c.texture.name==original)==1&&
                !idle.Any(c=>c.texture.name==signals),
                "Ordinary detailed resident must not emit a fake conversation");
            game.conversation=new Conversation("guild","guild","hello","hello");
            game.conversationSource=npc;
            Clear();
            var active=Draw(app,"DrawNpc","guild",
                new Point(npc.x,npc.y),-1);
            var body=active.Single(c=>c.texture.name==original);
            var cue=active.Single(c=>c.texture.name==signals);
            Check(cue.texture.width==96&&cue.texture.height==40&&
                Math.Abs(cue.uv.width-.25f)<.00001f&&
                Math.Abs(cue.uv.height-.5f)<.00001f&&
                Math.Abs(cue.uv.y-.5f)<.00001f&&
                Math.Abs(cue.rect.width-30f)<.001f&&
                Math.Abs(cue.rect.height-25f)<.001f&&
                cue.color.a>.60f&&cue.color.a<.8f,
                "Real NPC speech must sample one original top-row 24x20 pixel cell");
            float foot=body.rect.y+body.rect.height*120f/128f;
            Check(Math.Abs(cue.rect.y+119f-foot)<.001f&&
                cue.rect.y+cue.rect.height<body.rect.y&&
                Math.Abs(cue.rect.x+cue.rect.width*.5f-
                    (body.rect.x+body.rect.width*.5f))<.001f,
                "NPC signal must stay above the same rooted body, not overdraw face or feet");
            // Preserve the REAL production world-body plus its animated
            // speaker overlay in the CI visual artifact, not just a
            // disconnected 8-cell resource gallery.
            GUI.Export(".starfall-checks/sprite-qa",
                "review-actual-NPC-guild-pinned-dialogue");
            // The same pinned NPC body fades beneath foreground scenery.
            // The original four-frame speech cue must fade with that SAME
            // whole body instead of floating brightly over the covering wall.
            float priorX=game.save.x,priorY=game.save.y;
            float priorWorldOpacity=(float)typeof(FrontierRuntime).GetField(
                "worldActorOpacity",Private).GetValue(app);
            try
            {
                game.save.x=npc.x;
                game.save.y=npc.y-.5f;
                Set(app,"worldActorOpacity",.6f);
                Clear();
                var covered=Draw(app,"DrawNpc","guild",
                    new Point(npc.x,npc.y),-1);
                var coveredBody=covered.Single(c=>c.texture.name==original);
                var coveredCue=covered.Single(c=>c.texture.name==signals);
                Check(Math.Abs(coveredCue.color.a-cue.color.a*.45f*.6f)<
                    .0001f&&coveredBody.color.a<body.color.a&&
                    coveredCue.color.a>0f,
                    "Occluded dialogue icon stayed bright over a faded NPC body");
            }
            finally
            {
                game.save.x=priorX;game.save.y=priorY;
                Set(app,"worldActorOpacity",priorWorldOpacity);
            }
            var newClock=2.35f;
            Set(app,"clock",newClock);
            Clear();
            var later=Draw(app,"DrawNpc","guild",
                new Point(npc.x,npc.y),-1);
            var nextCue=later.Single(c=>c.texture.name==signals);
            Check(Math.Abs(nextCue.uv.x-cue.uv.x)>.00001f&&
                Math.Abs(nextCue.rect.y-cue.rect.y)<.001f,
                "Four source speech poses must animate while world root stays fixed");
            var bystander=new Point(npc.x+4f,npc.y);
            Clear();var other=Draw(app,"DrawNpc","guild",bystander,-1);
            Check(other.Any(c=>c.texture.name==original)&&
                !other.Any(c=>c.texture.name==signals),
                "Same-kind bystander inherited another resident's pinned dialogue");
            game.save.reducedMotion=true;Clear();
            var still=Draw(app,"DrawNpc","guild",
                new Point(npc.x,npc.y),-1);
            var fixedCue=still.Single(c=>c.texture.name==signals);
            Set(app,"clock",2.82f);Clear();
            var stillLater=Draw(app,"DrawNpc","guild",
                new Point(npc.x,npc.y),-1);
            Check(stillLater.Single(c=>c.texture.name==signals).uv.x==
                fixedCue.uv.x,
                "Reduced-motion must keep one stable readable speech symbol");
            game.save.reducedMotion=false;
            game.save.effectsQuality=0;Clear();
            var disabled=Draw(app,"DrawNpc","guild",
                new Point(npc.x,npc.y),-1);
            Check(disabled.Any(c=>c.texture.name==original)&&
                !disabled.Any(c=>c.texture.name==signals),
                "Zero visual effects must preserve original NPC and skip only cue");
            game.save.effectsQuality=2;game.conversation=null;
            // Row-one amber danger belongs to a genuine escort distress
            // branch; exercise the same renderer with both asset rows.
            Clear();Call(app,"DrawNpcActionSignal",
                new Point(320,360),1,14,1f);
            var alert=GUI.textureCalls.Single(c=>c.texture.name==signals);
            Check(Math.Abs(alert.uv.y)<.00001f&&
                Math.Abs(alert.uv.width-.25f)<.00001f&&
                alert.color.a>.6f&&alert.color.r>=.99f,
                "Rescue danger symbol must use its own approved bottom pixel row");
            // Escort distress icons also share the current depth-pass fade
            // without shifting their original art, row or collision root.
            float originalWorldAlpha=(float)typeof(FrontierRuntime).GetField(
                "worldActorOpacity",Private).GetValue(app);
            try
            {
                Set(app,"worldActorOpacity",.35f);
                Clear();Call(app,"DrawNpcActionSignal",
                    new Point(320,360),1,14,1f);
                var faded=GUI.textureCalls.Single(c=>c.texture.name==signals);
                Check(Math.Abs(faded.color.a-alert.color.a*.35f)<.0001f&&
                    Math.Abs(faded.uv.y-alert.uv.y)<.00001f,
                    "Escorted civilian distress icon ignored original scene fade");
            }
            finally {Set(app,"worldActorOpacity",originalWorldAlpha);}
            // Simulate Unity's two-stage SpriteImporter replacement.
            // A half-published atlas MUST NOT replace the valid whole
            // 8-cell signal source in already running conversations.
            const string overrideKey="Frontier/Art/NpcActionSignals";
            Texture2D previousImport;
            bool overridden=Resources.TextureOverrides.TryGetValue(
                overrideKey,out previousImport);
            Texture2D originalSheet=alert.texture;
            try
            {
                Resources.TextureOverrides[overrideKey]=new Texture2D(
                    95,40,TextureFormat.RGBA32,false){
                    name="partially-imported-npc-cues"};
                Set(app,"clock",12f);Clear();
                Call(app,"DrawNpcActionSignal",new Point(320,360),1,14,1f);
                Check(object.ReferenceEquals(
                    GUI.textureCalls.Single().texture,originalSheet),
                    "Partial imported NPC art replaced a validated signal mid-scene");
                var completed=new Texture2D(
                    96,40,TextureFormat.RGBA32,false){
                    name="validated-republished-npc-cues"};
                Resources.TextureOverrides[overrideKey]=completed;
                Set(app,"clock",15f);Clear();
                Call(app,"DrawNpcActionSignal",new Point(320,360),1,14,1f);
                Check(object.ReferenceEquals(
                    GUI.textureCalls.Single().texture,completed),
                    "Complete late NPC art import failed to appear without scene reload");
                // A new game or visual review can start at clock zero.
                Resources.TextureOverrides[overrideKey]=originalSheet;
                Set(app,"clock",0f);Clear();
                Call(app,"DrawNpcActionSignal",new Point(320,360),0,0,1f);
                Check(object.ReferenceEquals(
                    GUI.textureCalls.Single().texture,originalSheet),
                    "Clock rewind kept NPC icon generation from previous save");
            }
            finally
            {
                if(overridden)Resources.TextureOverrides[overrideKey]=previousImport;
                else Resources.TextureOverrides.Remove(overrideKey);
                Set(app,"npcActionSignalSheet",originalSheet);
                Set(app,"npcSignalRetryAt",float.NegativeInfinity);
            }
            Console.WriteLine("PASS production NPC speech/escort 8 painted frames, source pinning, feet/identity, low FX, reduced motion and atomic hot-reimport recovery");
        }
        finally
        {
            game.conversation=conversation;
            game.conversationSource=source;
            game.save.reducedMotion=reduced;
            game.save.effectsQuality=quality;
            app.EndVisualReview();Clear();
        }
    }


    // A scene-cover outline can be drawn BEFORE the dialogue body even
    // appears in the real depth-sorted pass. Only the first REAL body
    // paint may start a new speech weight-shift timeline.
    static void NpcOutlineCannotConsumeUprightDialogueEntry(
        FrontierRuntime app)
    {
        var g=app.Game;
        var flags=BindingFlags.Instance|BindingFlags.NonPublic;
        var runtime=typeof(FrontierRuntime);
        var conversationField=runtime.GetField("npcTrackedConversation",flags);
        var speakerField=runtime.GetField("npcTrackedInterlocutor",flags);
        var clockField=runtime.GetField("npcTalkStartedAt",flags);
        Check(conversationField!=null&&speakerField!=null&&clockField!=null,
            "NPC speech entrance uses original instance-scoped art state");
        object previousConversation=conversationField.GetValue(app);
        object previousSpeaker=speakerField.GetValue(app);
        object previousStart=clockField.GetValue(app);
        var savedConversation=g.conversation;
        var savedSource=g.conversationSource;
        bool savedMotion=g.save.reducedMotion;
        int savedEffects=g.save.effectsQuality;
        try
        {
            g.save.reducedMotion=false;g.save.effectsQuality=2;
            // Reproduce native GPU-only imports in the OFFLINE renderer.
            // The same source Sprite still paints a full-resolution body
            // but cannot furnish CPU alpha pixels for a new mask.
            var unreadable=new Texture2D(32,32,TextureFormat.RGBA32,
                false){name="never-readable-unpublished-npc"};
            unreadable.isReadable=false;
            var isolatedMasks=new PixelArt();
            try
            {
                Check(isolatedMasks.OutlineMask(unreadable)==null,
                    "A GPU-only Sprite without a published outline must decline CPU GetPixels cleanly");
            }
            finally{isolatedMasks.Dispose();}
            foreach(string kind in new[]{"guild","forge"})
            {
                var npc=new WorldObject{id="outline-speech-"+kind,
                    kind=kind,x=g.Position.x+2f,y=g.Position.y+1f};
                var current=new Conversation(kind,kind,
                    "Hello again","Hello again");
                g.conversation=current;g.conversationSource=npc;
                Set(app,"clock",1170f+(kind=="guild"?0f:5f));
                var initialConversation=conversationField.GetValue(app);
                var initialSpeaker=speakerField.GetValue(app);
                var initialStart=clockField.GetValue(app);
                try
                {
                    Set(app,"drawingOutline",true);
                    for(int pass=0;pass<5;pass++)
                    {
                        Set(app,"clock",1170f+
                            (kind=="guild"?0f:5f)+pass*.35f);
                        Clear();
                        var preview=Draw(app,"DrawNpc",kind,
                            new Point(npc.x,npc.y),-1);
                        Check(preview.Any(t=>t.rect.width==96f&&
                            t.rect.height==96f&&t.color.a>0f),
                            "A real original whole-body resident remains legible during GPU-only outline selection instead of disappearing or crashing");
                    }
                    Check(System.Object.ReferenceEquals(
                        conversationField.GetValue(app),initialConversation)&&
                        System.Object.ReferenceEquals(
                            speakerField.GetValue(app),initialSpeaker)&&
                        System.Object.Equals(
                            clockField.GetValue(app),initialStart),
                        "Five cover/selection outlines cannot consume or adopt a new NPC conversation timeline");
                    // The visible body enters much later than its previews,
                    // but its FIRST official frame must still be upright.
                    float firstPaint=1190f+(kind=="guild"?0f:5f);
                    Set(app,"drawingOutline",false);
                    Set(app,"clock",firstPaint);
                    Clear();
                    var first=Draw(app,"DrawNpc",kind,
                        new Point(npc.x,npc.y),-1)
                        .Single(t=>t.texture.name=="resource:Frontier/Art/Npcs");
                    Check(Math.Abs((float)clockField.GetValue(app)-
                        firstPaint)<.00001f&&
                        System.Object.ReferenceEquals(
                            conversationField.GetValue(app),current)&&
                        Math.Abs(first.matrix.m01)<.00001f&&
                        Math.Abs(first.matrix.m10)<.00001f,
                        "First REAL NPC body must start a fresh upright painted entry independent of previews");
                    Set(app,"clock",firstPaint+.40f);
                    Clear();
                    var swaying=Draw(app,"DrawNpc",kind,
                        new Point(npc.x,npc.y),-1)
                        .Single(t=>t.texture.name=="resource:Frontier/Art/Npcs");
                    Check(Math.Abs(swaying.matrix.m01)>.0001f&&
                        Math.Abs(swaying.uv.x-first.uv.x)<.00001f&&
                        Math.Abs(swaying.rect.y-first.rect.y)<.001f,
                        "After the genuine dialogue entry, original full-body NPC must sway from fixed boots without a swapped low-quality cell");
                    // Another NPC of the same type and another conversation
                    // cannot alter the first speaker's old genuine phase.
                    Clear();Draw(app,"DrawNpc",kind,
                        new Point(npc.x+4f,npc.y),-1);
                    Check(System.Object.ReferenceEquals(
                        conversationField.GetValue(app),current)&&
                        Math.Abs((float)clockField.GetValue(app)-
                            firstPaint)<.00001f,
                        "Same-kind bystander stole the pinned NPC's pose clock");
                    var reopened=new Conversation(kind,kind,
                        "More dialogue","More dialogue");
                    g.conversation=reopened;
                    Set(app,"drawingOutline",true);
                    Set(app,"clock",firstPaint+1.7f);
                    Clear();Draw(app,"DrawNpc",kind,
                        new Point(npc.x,npc.y),-1);
                    Check(System.Object.ReferenceEquals(
                        conversationField.GetValue(app),current),
                        "Outline-only dialogue reentry consumed a second conversation");
                    Set(app,"drawingOutline",false);
                    Clear();
                    var restarted=Draw(app,"DrawNpc",kind,
                        new Point(npc.x,npc.y),-1)
                        .Single(t=>t.texture.name=="resource:Frontier/Art/Npcs");
                    Check(System.Object.ReferenceEquals(
                        conversationField.GetValue(app),reopened)&&
                        Math.Abs(restarted.matrix.m01)<.00001f,
                        "Restarted real NPC dialogue must not arrive mid-sway after a preview-only repaint");
                }
                finally {Set(app,"drawingOutline",false);}
            }
            Console.WriteLine(
                "PASS original guild and forge dialogue: repeated cover outlines cannot skip the upright first real full-body frame, swallow a reentry or move foot pivots");
        }
        finally
        {
            Set(app,"drawingOutline",false);
            conversationField.SetValue(app,previousConversation);
            speakerField.SetValue(app,previousSpeaker);
            clockField.SetValue(app,previousStart);
            g.conversation=savedConversation;
            g.conversationSource=savedSource;
            g.save.reducedMotion=savedMotion;
            g.save.effectsQuality=savedEffects;
            Clear();
        }
    }


    public static void VerifyGpuOnlyMonsterHitAndSelectionFallback(
        FrontierRuntime app)
    {
        app.BeginVisualReview(0,1);
        var g=app.Game;
        var monsterField=typeof(FrontierRuntime).GetField(
            "monsterSheet",BindingFlags.Instance|BindingFlags.NonPublic);
        Check(monsterField!=null,"Production monster renderer uses original loaded whole-body sheet");
        object original=monsterField.GetValue(app);
        bool reduced=g.save.reducedMotion;
        try
        {
            g.save.reducedMotion=false;
            // Synthetic GPU-only ART INPUT for an adversarial import,
            // not a published replacement of mature Monster sprites.
            // A newly imported 6x3 sheet has no prepared CPU masks.
            var source=new Texture2D(384,192,TextureFormat.RGBA32,false)
                {name="gpu-only-monster-hit-simulation"};
            source.isReadable=false;
            var masks=new PixelArt();
            try
            {
                Check(masks.OutlineMask(source)==null&&
                    masks.FlashMask(source)==null,
                    "Unreadable monster imports with no published masks must not attempt CPU pixel reads");
            }
            finally{masks.Dispose();}
            monsterField.SetValue(app,source);
            var foe=new Enemy(99832,1,g.Position.x+1f,
                g.Position.y+.5f,80f){
                facing=new Point(1f,0f),flash=.08f,
                hitTime=.05f
            };
            Place(app,foe.Position,new Point(520,370));
            Set(app,"drawingOutline",false);
            Clear();
            var alive=Draw(app,"DrawEnemyActor",foe);
            Check(alive.Count(t=>System.Object.ReferenceEquals(
                    t.texture,source))==1&&
                alive.Any(t=>System.Object.ReferenceEquals(
                    t.texture,source)&&t.rect.width>0f),
                "Actual GPU-only monster hit must retain exactly one authored body instead of throwing on its missing optional flash mask");
            Set(app,"drawingOutline",true);
            Clear();
            var marked=Draw(app,"DrawEnemyActor",foe);
            var selected=marked.Where(t=>System.Object.ReferenceEquals(
                t.texture,source)).ToArray();
            Check(selected.Length==1&&
                Math.Abs(selected[0].color.a-.34f)<.0001f&&
                Math.Abs(selected[0].rect.x-alive.First(t=>
                    System.Object.ReferenceEquals(t.texture,source)).rect.x)<.001f&&
                Math.Abs(selected[0].rect.y-alive.First(t=>
                    System.Object.ReferenceEquals(t.texture,source)).rect.y)<.001f,
                "GPU-only monster preview must use the SAME exact whole-body crop and fixed sole with a safe translucent highlight, no old model or invisible selection");
            // A different adversarial import has a non-null Texture2D but
            // cannot be divided into the approved 6x3 square-cell grid.
            // Quarantine it before any body, contact shadow, dust or flash
            // can leave a misleading ghost behind.
            var malformed = new Texture2D(768, 512, TextureFormat.RGBA32, false)
                {name="malformed-monster-grid"};
            monsterField.SetValue(app,malformed);
            Clear();
            var rejected=Draw(app,"DrawEnemyActor",foe);
            Check(rejected.Count()==0,
                "Malformed non-null monster grids must be rejected before painting body, shadow or combat effects");
            Console.WriteLine("PASS GPU-only scaled 6x3 monster grid stays drawable; malformed grid is quarantined before all paint");
        }
        finally
        {
            monsterField.SetValue(app,original);
            g.save.reducedMotion=reduced;
            Set(app,"drawingOutline",false);
            app.EndVisualReview();Clear();
        }
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
                float dialogueStart=20f+expectedIndex;
                Set(app,"clock",dialogueStart);
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
                Check(Math.Abs(spoken.matrix.m01)<.00001f&&
                    Math.Abs(spoken.matrix.m10)<.00001f,
                    "Actual first dialogue repaint must NEVER start with an arbitrary tilted waist");
                // The same rich high-detail full-body source now has a
                // tiny dialogue weight-shift around the correct planted foot.
                // No Sprite cell, class face, render bounds or hitbox changes.
                Set(app,"clock",dialogueStart+.48f);
                Clear();
                var motion=Draw(app,"DrawNpc",npc.kind,
                    new Point(npc.x,npc.y),-1);
                var speaking=motion.First(c=>c.texture.name==original);
                Check(Math.Abs(speaking.matrix.m01)>.0001f &&
                    Math.Abs(speaking.matrix.m10)>.0001f &&
                    Math.Abs(speaking.uv.x-body.uv.x)<.0001f,
                    "Pinned speaker must tilt one original full-body Sprite");
                Check(Math.Abs(GUI.matrix.m01)<.00001f &&
                    Math.Abs(GUI.matrix.m10)<.00001f,
                    "Dialogue motion leaked transform into subsequent actor/UI");
                game.save.reducedMotion=true;
                Clear();
                var still=Draw(app,"DrawNpc",npc.kind,
                    new Point(npc.x,npc.y),-1).First(c=>c.texture.name==original);
                Check(Math.Abs(still.matrix.m01)<.00001f &&
                    Math.Abs(still.matrix.m10)<.00001f,
                    "Reduced-motion player preference must disable NPC body sway");
                game.save.reducedMotion=false;
                Set(app,"clock",dialogueStart+1.25f);
                Clear();
                var later=Draw(app,"DrawNpc",npc.kind,
                    new Point(npc.x,npc.y),-1).First(c=>c.texture.name==original);
                Check(Math.Abs(later.matrix.m01-speaking.matrix.m01)>.0001f &&
                    Math.Abs(later.uv.x-speaking.uv.x)<.0001f,
                    "Dialogue animation must evolve without changing authored cells");
                // Opening another conversation with the SAME speaker is a
                // separate event, not a stale continuation of old body sway.
                Set(app,"clock",dialogueStart+1.5f);
                game.conversation=new Conversation("speaker","speaker",
                    "new lines","new lines");
                Clear();
                var restarted=Draw(app,"DrawNpc",npc.kind,
                    new Point(npc.x,npc.y),-1);
                var fresh=restarted.Single(c=>c.texture.name==original);
                Check(Math.Abs(fresh.matrix.m01)<.00001f&&
                    Math.Abs(fresh.matrix.m10)<.00001f,
                    "New dialogue reused previous absolute-clock tilt instead of easing in");
                Set(app,"clock",dialogueStart+1.62f);
                Clear();
                var growing=Draw(app,"DrawNpc",npc.kind,
                    new Point(npc.x,npc.y),-1).Single(c=>c.texture.name==original);
                Check(Math.Abs(growing.matrix.m01)>.0001f&&
                    Math.Abs(growing.uv.x-fresh.uv.x)<.00001f,
                    "New dialogue failed to resume stable whole-body foot sway");

            }
            game.conversation=new Conversation("speaker","speaker","hello","hello");
            game.conversationSource=guild;
            Clear();
            var bystander=Draw(app,"DrawNpc","guild",new Point(21,17),-1);
            Check(bystander.Any(c=>c.texture.name==original),
                "Another guild member must not inherit a rejected dialogue avatar");
            var other=bystander.First(c=>c.texture.name==original);
            Check(Math.Abs(other.matrix.m01)<.00001f,
                "A nearby same-kind bystander must not inherit conversation motion");
            Clear();
            var garden=Draw(app,"DrawNpc","garden",new Point(10,10),-1);
            Check(garden.Any(c=>c.texture.name==original),
                "Unmodified rich source NPCs must stay on the accepted atlas");
            Console.WriteLine("PASS original 16-NPC sprites, pinned dialogue whole-body foot-lean, reduced motion and still bystanders");
            NpcOutlineCannotConsumeUprightDialogueEntry(app);
        }
        finally
        {
            game.conversation=savedConversation;
            game.conversationSource=savedSource;
        }
    }

    // The ACTIVE DrawEnemyActor/Actor production chain, not a detached
    // atlas preview, must preserve a complete planted body when GameCore
    // swaps the enemy's Idle and Run art. No enemy combat contact is delayed.
    static void EnemyAtlasHandoffsKeepOneGroundedIdentity(
        FrontierRuntime app,string folder)
    {
        foreach(bool boss in new[]{false,true})
        {
            app.BeginVisualReview(0,2);
            var game=app.Game;
            game.enemies.Clear();
            game.save.effectsQuality=2;
            game.save.reducedMotion=false;
            Set(app,"clock",10f);
            var enemy=new Enemy(boss?99431:99430,0,
                game.Position.x+1f,game.Position.y+.5f,120f,boss);
            enemy.moving=false;
            enemy.moveTime=.1f;
            game.enemies.Add(enemy);
            var still=Draw(app,"DrawEnemyActor",enemy);
            string idleName="resource:Frontier/Art/"+(boss?"Bosses":"Monsters");
            string movingName="resource:Frontier/Art/"+(boss?"WalkBossA":"WalkMonsters");
            Check(still.Count(c=>c.texture.name==idleName)==1&&
                !still.Any(c=>c.texture.name==movingName),
                "Idle must start on only its published whole-body atlas");
            enemy.moving=true;
            enemy.moveTime=.36f;
            Set(app,"clock",10.02f);
            Clear();
            var entering=Draw(app,"DrawEnemyActor",enemy);
            var run=entering.Single(c=>c.texture.name==movingName);
            var oldBody=still.Single(c=>c.texture.name==idleName);
            Check(!entering.Any(c=>c.texture.name==idleName)&&
                Math.Abs(run.color.a-oldBody.color.a)<.00001f,
                "Idle->Run must paint exactly ONE authoritative whole-body actor at its true scene opacity, never a second ghost head or weapon");
            Check(Math.Abs(run.rect.x+run.rect.width*.5f-
                    oldBody.rect.x-oldBody.rect.width*.5f)<.001f&&
                Math.Abs(run.rect.y+run.rect.height*.9375f-
                    oldBody.rect.y-oldBody.rect.height*.9375f)<.001f,
                "Single source Sprite and old whole body must plant the SAME world foot");
            Check(!entering.Any(c=>c.texture.name.Contains("PlayerVanguard")||
                c.texture.name.Contains("PlayerRanger")),
                "Nonhero crossfade must not borrow player anatomy");
            GUI.Export(folder,boss?"review-boss-walk-cross-atlas":"review-monster-walk-cross-atlas");
            Set(app,"clock",10.08f);
            Clear();var fading=Draw(app,"DrawEnemyActor",enemy);
            var progressBody=fading.Single(c=>c.texture.name==movingName);
            Check(!fading.Any(c=>c.texture.name==idleName)&&
                Math.Abs(progressBody.color.a-oldBody.color.a)<.00001f&&
                Math.Abs(progressBody.rect.y+progressBody.rect.height*.9375f-
                    run.rect.y-run.rect.height*.9375f)<.001f,
                "Transition cannot paint a second fading torso, helmet or weapon");
            // A walking Boss retains its WalkBoss atlas during Hit,
            // even when the hit flash has expired. The old IDLE silhouette
            // must not bleed through the same-source hit keyframe while an
            // Idle->Walk crossfade remains active in its 105ms window.
            // This is the real production DrawEnemyActor/Actor path.
            if(boss)
            {
                enemy.hitTime=.07f;
                enemy.flash=0f;
                Set(app,"clock",10.09f);Clear();
                var hurtDuringBlend=Draw(app,"DrawEnemyActor",enemy);
                Check(hurtDuringBlend.Any(c=>c.texture.name==movingName)&&
                    !hurtDuringBlend.Any(c=>c.texture.name==idleName),
                    "Boss hit after flash expiry kept an obsolete idle body over its authored WalkBoss hit pose");
                enemy.hitTime=0f;
            }
            Set(app,"clock",10.15f);
            Clear();var settled=Draw(app,"DrawEnemyActor",enemy);
            Check(settled.Any(c=>c.texture.name==movingName)&&
                !settled.Any(c=>c.texture.name==idleName),
                "Transition afterimage must disappear after bounded window");
            // A genuine attack is NEVER blended with a false old foot pose.
            enemy.state=1;enemy.timer=.35f;enemy.windupDuration=.65f;
            Set(app,"clock",10.17f);
            Clear();var windup=Draw(app,"DrawEnemyActor",enemy);
            Check(windup.Any(c=>c.texture.name==idleName)&&
                !windup.Any(c=>c.texture.name==movingName),
                "New GameCore windup must immediately own combat body art");
            enemy.state=0;enemy.releaseTime=.18f;
            Set(app,"clock",10.18f);
            Clear();var released=Draw(app,"DrawEnemyActor",enemy);
            Check(released.Any(c=>c.texture.name==idleName)&&
                !released.Any(c=>c.texture.name==movingName),
                "Contact/release may not be obscured by a walking afterimage");
            enemy.releaseTime=0f;enemy.moving=false;
            enemy.hitTime=0f;
            Set(app,"clock",10.2f);Clear();
            Draw(app,"DrawEnemyActor",enemy);
            game.save.reducedMotion=true;
            enemy.moving=true;
            Set(app,"clock",10.22f);
            Clear();var quiet=Draw(app,"DrawEnemyActor",enemy);
            Check(quiet.Any(c=>c.texture.name==movingName)&&
                !quiet.Any(c=>c.texture.name==idleName),
                "Reduced motion must disable new transition ghosts");
            // Enter a fresh, ordinary Idle->Run transition, then turn:
            // the old-facing ghost must vanish IMMEDIATELY despite the
            // former fade clock still being inside its 105ms budget.
            game.save.reducedMotion=false;
            Set(app,"clock",11.0f);
            enemy.moving=false;
            Clear();Draw(app,"DrawEnemyActor",enemy);
            Set(app,"clock",11.20f);
            Clear();Draw(app,"DrawEnemyActor",enemy);
            enemy.moving=true;
            Set(app,"clock",11.22f);
            Clear();
            var restart=Draw(app,"DrawEnemyActor",enemy);
            Check(restart.Count(c=>c.texture.name==movingName)==1&&
                !restart.Any(c=>c.texture.name==idleName),
                "Restarted planted atlas handoff drew more than one complete actor");
            enemy.Face(new Point(-1,0));
            Set(app,"clock",11.23f);Clear();
            var turned=Draw(app,"DrawEnemyActor",enemy);
            Check(turned.Any(c=>c.texture.name==movingName)&&
                !turned.Any(c=>c.texture.name==idleName),
                "A real turn may not reveal an obsolete facing silhouette");
            // The enemy's flash is hit feedback, not a second walk cycle.
            // Use another valid atlas swap and interrupt it with real hit
            // flash without changing its current pose/authoritative sprite.
            enemy.Face(new Point(1,0));
            enemy.moving=false;
            Set(app,"clock",11.40f);Clear();
            Draw(app,"DrawEnemyActor",enemy);
            Set(app,"clock",11.60f);Clear();
            Draw(app,"DrawEnemyActor",enemy);
            enemy.moving=true;
            Set(app,"clock",11.62f);Clear();
            var newBlend=Draw(app,"DrawEnemyActor",enemy);
            Check(newBlend.Any(c=>c.texture.name==movingName)&&
                !newBlend.Any(c=>c.texture.name==idleName),
                "Actual source-pose handoff must use the CURRENT actor before white hit flash");
            enemy.flash=.075f;
            Set(app,"clock",11.63f);Clear();
            var interrupted=Draw(app,"DrawEnemyActor",enemy);
            Check(interrupted.Any(c=>c.texture.name==movingName)&&
                !interrupted.Any(c=>c.texture.name==idleName),
                "Hit flash must cancel stale body ghosts immediately");
            // Teleport/reentry is not a foot motion, so no extra old atlas
            // can appear underneath the new body's registered world foot.
            enemy.flash=0f;
            enemy.moving=false;
            Set(app,"clock",11.80f);Clear();
            Draw(app,"DrawEnemyActor",enemy);
            enemy.x+=2f;
            enemy.moving=true;
            Set(app,"clock",11.82f);Clear();
            var warped=Draw(app,"DrawEnemyActor",enemy);
            Check(warped.Any(c=>c.texture.name==movingName)&&
                !warped.Any(c=>c.texture.name==idleName),
                "Teleport cannot crossfade a body from an old world coordinate");
            if(boss)
            {
                // A genuine nonlethal Boss hit keeps the SAME accepted
                // moving whole-body atlas while stunned. Immediately after
                // hitTime reaches zero it changes to authored Idle. The old
                // Hit silhouette must NEVER linger under it via a calm
                // Idle/Walk crossfade. This failed with state.pose==4.
                var recovering=new Enemy(99433,0,
                    game.Position.x+1f,game.Position.y+.5f,120f,true);
                recovering.moving=true;recovering.moveTime=.38f;
                recovering.flash=0f;
                Set(app,"clock",12f);Clear();
                var stride=Draw(app,"DrawEnemyActor",recovering);
                Check(stride.Any(c=>c.texture.name==movingName)&&
                    !stride.Any(c=>c.texture.name==idleName),
                    "Regression setup must start on a real accepted Boss walking Sprite");
                recovering.hitTime=.07f;
                Set(app,"clock",12.02f);Clear();
                var recoiling=Draw(app,"DrawEnemyActor",recovering);
                Check(recoiling.Any(c=>c.texture.name==movingName),
                    "Moving Boss must keep its accepted full-body hit silhouette");
                recovering.hitTime=0f;
                recovering.moving=false;
                Set(app,"clock",12.04f);Clear();
                var recovered=Draw(app,"DrawEnemyActor",recovering);
                Check(recovered.Any(c=>c.texture.name==idleName)&&
                    !recovered.Any(c=>c.texture.name==movingName),
                    "Old Boss moving Hit body ghosted under post-recoil Idle for one or more frames");
            }
        }
        Console.WriteLine("PASS common/Boss single-body Idle->Walk production lane: rooted source size easing without duplicate head/weapon, attack interrupt, facing safety and accessibility");
    }

    private static float MoveTimeForGait(int target,float stride)
    {
        for(int i=1;i<400;i++)
        {
            float distance=stride*i/400f;
            if(GameCore.GaitFrame(distance,stride)==target)return distance;
        }
        throw new Exception("Could not select a source-authored gait phase "+target);
    }
    private static int FrameColumn(GUI.TextureCall call,int columns)
    { return (int)Math.Round(call.uv.x*columns); }
    private static int FrameRow(GUI.TextureCall call,int rows)
    { return (int)Math.Round((1f-call.uv.y-call.uv.height)*rows); }

    // Stopping on an airborne walk key used to jump directly to Idle on the
    // next repaint. Exercise the REAL production actor draw for both custom
    // same-atlas enemies and cross-atlas archers/Bosses, on phases 1 and 3.
    // The only allowed bridge is the next original planted source cell, with
    // one complete body, a fixed foot and a bounded return to Idle.
    static void EnemyWalkStopUsesNextPlantedSourceCell(
        FrontierRuntime app,string folder)
    {
        var cases=new[]{
            new {room=0,kind=0,boss=false,walk="MossboundRaider",
                idle="MossboundRaider",stride=1.35f,walkRow=1,idleRow=0,
                columns=4,rows=6,idleRows=6},
            new {room=0,kind=2,boss=false,walk="CinderstoneGuard",
                idle="CinderstoneGuard",stride=1.8f,walkRow=1,idleRow=0,
                columns=4,rows=6,idleRows=6},
            new {room=0,kind=1,boss=false,walk="WalkMonsters",
                idle="Monsters",stride=1.8f,walkRow=1,idleRow=1,
                columns=4,rows=3,idleRows=3},
            new {room=2,kind=0,boss=true,walk="WalkBossA",
                idle="Bosses",stride=2.8f,walkRow=1,idleRow=1,
                columns=4,rows=4,idleRows=8},
            new {room=1,kind=0,boss=true,walk="BriarBeast",
                idle="BriarBeast",stride=2.8f,walkRow=1,idleRow=0,
                columns=4,rows=18,idleRows=18}
        };
        foreach(var item in cases)
        foreach(int gait in new[]{1,3})
        foreach(int effectsQuality in new[]{0,2})
        {
            app.BeginVisualReview(0,item.room);
            var game=app.Game;
            game.enemies.Clear();game.save.companion=-1;
            game.save.effectsQuality=effectsQuality;game.save.reducedMotion=false;
            Set(app,"clock",10f);
            var enemy=new Enemy(99500+item.kind+item.room*10,
                item.kind,game.Position.x+1f,game.Position.y+.5f,
                160f,item.boss)
            {facing=new Point(1,0),moving=true,
                moveTime=MoveTimeForGait(gait,item.stride)};
            game.enemies.Add(enemy);
            Place(app,enemy.Position,new Point(500,350));
            Clear();
            var walking=Draw(app,"DrawEnemyActor",enemy);
            string walkName="resource:Frontier/Art/"+item.walk;
            string idleName="resource:Frontier/Art/"+item.idle;
            var walkBody=walking.Single(c=>c.texture.name==walkName);
            Check(FrameRow(walkBody,item.rows)==item.walkRow&&
                FrameColumn(walkBody,item.columns)==gait,
                "Regression setup must paint the selected real walking keyframe for "+item.walk);

            // Simulate the largest ordinary displacement expected
            // between painted frames when Update catches up a 200ms stall.
            // Camera re-centering keeps the same visible foot while the
            // actor-keyed state sees the real .9-world-unit translation.
            enemy.x+=.90f;
            Place(app,enemy.Position,new Point(500,350));
            enemy.moving=false;
            Set(app,"clock",10.02f);Clear();
            var stopping=Draw(app,"DrawEnemyActor",enemy);
            var contact=stopping.Single(c=>c.texture.name==walkName);
            int planted=gait==1?2:0;
            Check(FrameRow(contact,item.rows)==item.walkRow&&
                FrameColumn(contact,item.columns)==planted,
                "Stopping on gait "+gait+" must advance only to the next authored planted cell for "+item.walk);
            Check(stopping.Count(ActorTexture)==1&&
                Math.Abs(contact.rect.x+contact.rect.width*.5f-
                    walkBody.rect.x-walkBody.rect.width*.5f)<.001f&&
                Math.Abs(contact.rect.y+contact.rect.height*.9375f-
                    walkBody.rect.y-walkBody.rect.height*.9375f)<.001f,
                "A stop-plant transition must retain one complete body and the exact world foot root");

            Set(app,"clock",10.05f);Clear();
            var holding=Draw(app,"DrawEnemyActor",enemy);
            var held=holding.Single(c=>c.texture.name==walkName);
            Check(FrameRow(held,item.rows)==item.walkRow&&
                FrameColumn(held,item.columns)==planted&&
                holding.Count(ActorTexture)==1,
                "Planted contact must remain authoritative through the short bounded stop window");

            Set(app,"clock",10.8f);Clear();
            var idle=Draw(app,"DrawEnemyActor",enemy);
            var idleBody=idle.Single(c=>c.texture.name==idleName);
            Check(FrameRow(idleBody,item.idleRows)==item.idleRow&&
                idle.Count(ActorTexture)==1,
                "After the bounded plant window, "+item.walk+" gait "+gait+
                " expected Idle row "+item.idleRow+", observed row "+
                FrameRow(idleBody,item.idleRows)+" with "+
                idle.Count(ActorTexture)+" actor draws");
            GUI.Export(folder,"review-enemy-stop-plant-"+item.walk+"-phase-"+gait+"-quality-"+effectsQuality);
        }
        Console.WriteLine("PASS 20 live enemy/Boss stop transitions at effects quality 0 and 2: authored next planted gait cell, one body, stable feet, bounded Idle return and all five production atlas families");
    }

    // Stop-plant is its own bounded source-pose hold, not a side effect of
    // whole-body canvas easing. A Boss must return to the correct Idle atlas
    // after the plant duration even when atlas-size easing is deliberately longer.
    static void EnemyStopPlantDurationIsIndependent(FrontierRuntime app)
    {
        app.BeginVisualReview(0,2);
        object look=null;
        System.Reflection.FieldInfo handoffField=null,stopField=null;
        float previousHandoff=0f,previousStop=0f;
        bool havePrevious=false;
        try
        {
            var property=typeof(FrontierRuntime).GetProperty(
                "CurrentWorldVisualLook",Private);
            Check(property!=null,"WorldLook must expose the live stop-plant timing source");
            look=property.GetValue(app,null);
            handoffField=look.GetType().GetField("nonheroAtlasHandoffSeconds");
            stopField=look.GetType().GetField("nonheroStopPlantSeconds");
            Check(handoffField!=null&&stopField!=null,
                "Stop-plant and atlas-handoff timings must be independent settings");
            previousHandoff=(float)handoffField.GetValue(look);
            previousStop=(float)stopField.GetValue(look);
            havePrevious=true;
            handoffField.SetValue(look,.15f);
            stopField.SetValue(look,.07f);

            var game=app.Game;
            game.enemies.Clear();game.save.companion=-1;
            game.save.effectsQuality=2;game.save.reducedMotion=false;
            Set(app,"clock",40f);
            var enemy=new Enemy(99772,0,game.Position.x+1f,
                game.Position.y+.5f,160f,true)
            {facing=new Point(1,0),moving=true,
                moveTime=MoveTimeForGait(1,2.8f)};
            game.enemies.Add(enemy);
            Place(app,enemy.Position,new Point(500,350));Clear();
            string walkName="resource:Frontier/Art/WalkBossA";
            string idleName="resource:Frontier/Art/Bosses";
            var walking=Draw(app,"DrawEnemyActor",enemy);
            var walkBody=walking.Single(c=>c.texture.name==walkName);
            Check(FrameRow(walkBody,4)==1&&FrameColumn(walkBody,4)==1,
                "Native Boss stop-plant fixture must start on WalkBossA's real passing cell");

            enemy.x+=.90f;
            Place(app,enemy.Position,new Point(500,350));
            enemy.moving=false;
            Set(app,"clock",40.02f);Clear();
            var stopping=Draw(app,"DrawEnemyActor",enemy);
            var contact=stopping.Single(c=>c.texture.name==walkName);
            Check(FrameRow(contact,4)==1&&FrameColumn(contact,4)==2&&
                stopping.Count(ActorTexture)==1,
                "Boss stop must select the next painted foot plant as one whole body");

            Set(app,"clock",40.05f);Clear();
            var holding=Draw(app,"DrawEnemyActor",enemy);
            var held=holding.Single(c=>c.texture.name==walkName);
            Check(FrameRow(held,4)==1&&FrameColumn(held,4)==2&&
                holding.Count(ActorTexture)==1,
                "Boss planted contact must remain stable inside its own hold window");

            // 90ms after stopping exceeds the 70ms plant window but is still
            // inside the deliberately longer 150ms atlas-size easing window.
            Set(app,"clock",40.11f);Clear();
            var idle=Draw(app,"DrawEnemyActor",enemy);
            var idleBody=idle.Single(c=>c.texture.name==idleName);
            Check(FrameRow(idleBody,8)==1&&idle.Count(ActorTexture)==1,
                "Boss must return to the correct 8-row Idle atlas when stop-plant expires, independent of size easing");
            Console.WriteLine("PASS independent Boss stop-plant hold vs atlas-size easing: authored contact, bounded Idle return, one whole body");
        }
        finally
        {
            if(havePrevious)
            {
                handoffField.SetValue(look,previousHandoff);
                stopField.SetValue(look,previousStop);
            }
            app.EndVisualReview();Clear();
        }
    }

    // Stop-plant transitions are presentation-only: combat and accessibility
    // interrupts must immediately regain ownership of the source body.
    static void EnemyStopPlantInterruptions(FrontierRuntime app,string folder)
    {
        app.BeginVisualReview(0,0);
        var game=app.Game;
        game.enemies.Clear();game.save.companion=-1;
        game.save.effectsQuality=2;game.save.reducedMotion=false;
        Set(app,"clock",20f);
        var raider=new Enemy(99601,0,game.Position.x+1f,
            game.Position.y+.5f,160f)
        {facing=new Point(1,0),moving=true,
            moveTime=MoveTimeForGait(3,1.35f)};
        game.enemies.Add(raider);
        Place(app,raider.Position,new Point(500,350));
        Clear();Draw(app,"DrawEnemyActor",raider);
        raider.moving=false;
        Set(app,"clock",20.02f);Clear();
        var planted=Draw(app,"DrawEnemyActor",raider)
            .Single(c=>c.texture.name=="resource:Frontier/Art/MossboundRaider");
        Check(FrameRow(planted,6)==1&&FrameColumn(planted,4)==0,
            "Phase 3 must reach its next authored planted contact");
        raider.stun=.2f;
        Set(app,"clock",20.04f);Clear();
        var stunned=Draw(app,"DrawEnemyActor",raider);
        var body=stunned.Single(c=>c.texture.name=="resource:Frontier/Art/MossboundRaider");
        Check(FrameRow(body,6)==0&&stunned.Count(ActorTexture)==1,
            "A real stun cancels the pending walk plant immediately");
        raider.stun=0f;raider.state=1;raider.timer=.3f;raider.windupDuration=.65f;
        Set(app,"clock",20.06f);Clear();
        var windup=Draw(app,"DrawEnemyActor",raider);
        body=windup.Single(c=>c.texture.name=="resource:Frontier/Art/MossboundRaider");
        Check(FrameRow(body,6)==2&&windup.Count(ActorTexture)==1,
            "A real attack windup cannot be hidden by an old planted gait cell");

        app.BeginVisualReview(0,0);
        game=app.Game;game.enemies.Clear();
        game.save.effectsQuality=2;game.save.reducedMotion=false;
        Set(app,"clock",30f);
        var archer=new Enemy(99602,1,game.Position.x+1f,
            game.Position.y+.5f,160f)
        {facing=new Point(1,0),moving=true,
            moveTime=MoveTimeForGait(1,1.8f)};
        game.enemies.Add(archer);
        Place(app,archer.Position,new Point(500,350));
        Clear();Draw(app,"DrawEnemyActor",archer);
        archer.moving=false;
        Set(app,"clock",30.02f);Clear();
        var archerStop=Draw(app,"DrawEnemyActor",archer);
        Check(archerStop.Any(c=>c.texture.name=="resource:Frontier/Art/WalkMonsters"),
            "Archer stop should use its source-authored planted walk cell");
        archer.Face(new Point(-1,0));
        Set(app,"clock",30.04f);Clear();
        var turned=Draw(app,"DrawEnemyActor",archer);
        Check(turned.Any(c=>c.texture.name=="resource:Frontier/Art/Monsters")&&
            !turned.Any(c=>c.texture.name=="resource:Frontier/Art/WalkMonsters")&&
            turned.Count(ActorTexture)==1,
            "A real facing change cancels the previous walk source immediately");

        app.BeginVisualReview(0,0);
        game=app.Game;game.enemies.Clear();
        game.save.effectsQuality=2;game.save.reducedMotion=false;
        Set(app,"clock",40f);
        var guard=new Enemy(99603,2,game.Position.x+1f,
            game.Position.y+.5f,160f)
        {facing=new Point(1,0),moving=true,
            moveTime=MoveTimeForGait(1,1.8f)};
        game.enemies.Add(guard);
        Place(app,guard.Position,new Point(500,350));
        Clear();Draw(app,"DrawEnemyActor",guard);
        guard.moving=false;game.save.reducedMotion=true;
        Set(app,"clock",40.02f);Clear();
        var reduced=Draw(app,"DrawEnemyActor",guard);
        body=reduced.Single(c=>c.texture.name=="resource:Frontier/Art/CinderstoneGuard");
        Check(FrameRow(body,6)==0&&reduced.Count(ActorTexture)==1,
            "Reduced motion returns to Idle immediately without holding a gait phase");
        Console.WriteLine("PASS enemy stop-plant interruption: stun, attack, archer turn and reduced-motion gates");
    }

    // This is called by the executable core C# suite, not only by
    // ActorRegistrationChecks.Run's optional review-gallery entrypoint.
    // The accepted regional Boss Idle (320px) and Walk (384px) canvases
    // previously popped 20% in one repaint despite a fade of two bodies.
    // This tests the ACTUAL DrawEnemyActor production method and all three
    // region-material pixel families, not a disconnected atlas gallery.
    public static void VerifyOriginalEnemyReleaseDust(FrontierRuntime app)
    {
        app.BeginVisualReview(0,1);
        var game=app.Game;
        int originalQuality=game.save.effectsQuality;
        bool originalReduced=game.save.reducedMotion;
        float originalClock=(float)typeof(FrontierRuntime).GetField(
            "clock",Private).GetValue(app);
        string key="Frontier/Art/EnemyReleaseDust";
        Texture2D priorImport;
        bool hadImport=Resources.TextureOverrides.TryGetValue(
            key,out priorImport);
        try
        {
            game.save.effectsQuality=2;
            game.save.reducedMotion=false;
            // First event is a normal forest attacker: middle of a true
            // GameCore quarter-source seam, hence TWO complete source cells.
            var forest=new Enemy(99460,0,12,14,125){
                releaseTime=.18f,facing=new Point(1,0),state=0};
            Set(app,"clock",3f);
            Clear();var living=Draw(app,"DrawEnemyActor",forest);
            var dust=living.Where(c=>c.texture.name==
                "resource:Frontier/Art/EnemyReleaseDust").ToArray();
            Check(dust.Length==2&&
                dust[0].texture.width==192&&dust[0].texture.height==72&&
                Math.Abs(dust[0].uv.y-2f/3f)<.00001f&&
                Math.Abs(dust[1].uv.y-2f/3f)<.00001f&&
                Math.Abs(dust[0].uv.x)<.00001f&&
                Math.Abs(dust[1].uv.x-.25f)<.00001f&&
                Math.Abs(dust[0].color.a-dust[1].color.a)<.00001f,
                "Moss enemy release did not crossfade two complete original 48px soil cells");
            var originalSheet=dust[0].texture;
            Check(dust[0].rect.x==dust[1].rect.x&&
                dust[0].rect.y==dust[1].rect.y&&
                dust[0].rect.width==dust[1].rect.width&&
                dust[0].rect.height==dust[1].rect.height&&
                living.Any(c=>c.texture.name==
                    "resource:Frontier/Art/MossboundRaider"||
                    c.texture.name=="resource:Frontier/Art/Monsters"),
                "Release dust hid or shifted the genuine mossbound whole-body actor");
            Check(Math.Abs(forest.releaseTime-.18f)<.00001f&&
                forest.state==0,
                "Decorative dust advanced authoritative attack timing or gameplay");
            // Force the same literal source sheet to be selected through
            // archer cold ash and cinder guard, without inventing damage.
            foreach(int kind in new[]{1,2})
            {
                var foe=new Enemy(99461+kind,kind,12,14,125){
                    releaseTime=.12f,state=0,facing=new Point(1,0)};
                Clear();var painted=Draw(app,"DrawEnemyActor",foe);
                var pair=painted.Where(c=>c.texture==originalSheet).ToArray();
                int row=kind==1?2:1;
                Check(pair.Length==2&&
                    Math.Abs(pair[0].uv.y-(2-row)/3f)<.00001f&&
                    Math.Abs(pair[1].uv.y-(2-row)/3f)<.00001f,
                    "Archer/guard painted dust sampled wrong independent regional source row");
            }
            // Boss idle->windup blends must not determine ground shock pose:
            // only the actual Boss releaseTime controls visual contact.
            var boss=new Enemy(99469,0,12,14,400,true){
                releaseTime=.285f,state=0,facing=new Point(1,0)};
            Clear();var bossRender=Draw(app,"DrawEnemyActor",boss);
            var bossDust=bossRender.Where(c=>c.texture==originalSheet).ToArray();
            Check(bossDust.Length==2&&
                bossDust[0].rect.width>dust[0].rect.width&&
                Math.Abs(bossDust[0].uv.y-2f/3f)<.00001f&&
                bossRender.Any(c=>c.texture.name==
                    "resource:Frontier/Art/BriarBeast"||
                    c.texture.name=="resource:Frontier/Art/Bosses"),
                "Live original Boss release lost rooted material dust or whole-body animation");
            // Ground contact fragments must use the same actually painted
            // facing as each 48px whole-body source, including the two
            // distinct native North/South archer cells.
            var left=new Enemy(99475,0,12,14,125){
                releaseTime=.18f,facing=new Point(-1,0)};
            Clear();var leftDust=Draw(app,"DrawEnemyActor",left)
                .Where(c=>c.texture==originalSheet).ToArray();
            Check(leftDust.Length==2&&leftDust.All(c=>
                    Math.Abs(c.uv.width+1f/4f)<.00001f),
                "Left-facing enemy released unmirrored world-contact dust");
            var bowman=new Enemy(99476,1,12,14,125){releaseTime=.12f};
            bowman.Face(new Point(-1,0));bowman.Face(new Point(0,-1));
            Check(bowman.VisualFlip,"Bow fixture lost its stored left look");
            Clear();var north=Draw(app,"DrawEnemyActor",bowman)
                .Where(c=>c.texture==originalSheet).ToArray();
            Check(north.Length==2&&north.All(c=>
                    Math.Abs(c.uv.width-1f/4f)<.00001f),
                "North/South archer retained stale side-facing contact dust");
            // The same foreground cover that dims the Boss's complete body
            // must dim its ground release wave; it cannot float at full alpha.
            float savedX=game.save.x,savedY=game.save.y;
            try
            {
                game.save.x=boss.x+4f;game.save.y=boss.y+4f;
                Clear();var clearWave=Draw(app,"DrawEnemyActor",boss)
                    .Where(c=>c.texture==originalSheet).ToArray();
                game.save.x=boss.x;game.save.y=boss.y-1f;
                Clear();var dimWave=Draw(app,"DrawEnemyActor",boss)
                    .Where(c=>c.texture==originalSheet).ToArray();
                Check(clearWave.Length==2&&dimWave.Length==2&&
                    Math.Abs(dimWave[0].color.a-clearWave[0].color.a*.55f)<.00001f&&
                    Math.Abs(dimWave[1].color.a-clearWave[1].color.a*.55f)<.00001f,
                    "Boss ground shock remained opaque behind a faded body");
            }
            finally { game.save.x=savedX;game.save.y=savedY; }
            // No pre-hit telegraph, stun or corpse may emit fake contact.
            forest.state=1;
            Clear();Check(!Draw(app,"DrawEnemyActor",forest).Any(c=>
                c.texture==originalSheet),
                "Windup falsely acquired post-hit contact dust");
            forest.state=0;forest.stun=.1f;
            Clear();Check(!Draw(app,"DrawEnemyActor",forest).Any(c=>
                c.texture==originalSheet),
                "Interrupted enemy kept a striking dust silhouette");
            forest.stun=0;forest.releaseTime=0;
            Clear();Check(!Draw(app,"DrawEnemyActor",forest).Any(c=>
                c.texture==originalSheet),
                "Ground dust looped after the real gameplay release ended");
            forest.releaseTime=.18f;
            game.save.reducedMotion=true;
            Clear();Check(!Draw(app,"DrawEnemyActor",forest).Any(c=>
                c.texture==originalSheet),
                "Reduced motion failed to suppress only optional ground wave");
            game.save.reducedMotion=false;
            game.save.effectsQuality=1;
            Clear();Check(!Draw(app,"DrawEnemyActor",forest).Any(c=>
                c.texture==originalSheet),
                "Medium settings must preserve body but omit optional ground overdraw");
            game.save.effectsQuality=2;
            // Incomplete editor imports must not overwrite the last full
            // three-material source, then a corrected atlas must hot-reload.
            Resources.TextureOverrides[key]=new Texture2D(
                191,72,TextureFormat.RGBA32,false){
                name="partial-enemy-release-paint"};
            Set(app,"clock",7f);Clear();
            var partial=Draw(app,"DrawEnemyActor",forest);
            Check(partial.Any(c=>c.texture==originalSheet),
                "Partial imported Boss/enemy dust replaced approved full atlas");
            var revised=new Texture2D(192,72,TextureFormat.RGBA32,false){
                name="corrected-original-release-paint"};
            Resources.TextureOverrides[key]=revised;
            Set(app,"clock",10f);Clear();
            var corrected=Draw(app,"DrawEnemyActor",forest);
            Check(corrected.Any(c=>c.texture==revised),
                "Corrected original sprite source did not hot reload in live world");
            Resources.TextureOverrides[key]=originalSheet;
            Set(app,"clock",0f);Clear();
            var rewound=Draw(app,"DrawEnemyActor",forest);
            Check(rewound.Any(c=>c.texture==originalSheet),
                "Restarted gameplay clock kept stale release contact art");
            Console.WriteLine("PASS 12 original enemy/Boss ground contact cells: actual release clock, 3 palettes, whole body, seam blend, frozen root, interruptions, accessibility, atomic reimport");
        }
        finally
        {
            if(hadImport)Resources.TextureOverrides[key]=priorImport;
            else Resources.TextureOverrides.Remove(key);
            Set(app,"clock",originalClock);
            game.save.effectsQuality=originalQuality;
            game.save.reducedMotion=originalReduced;
            app.EndVisualReview();Clear();
        }
    }

    public static void VerifyBossWholeBodySizeAcrossIdleRun(FrontierRuntime ignored)
    {
        var app=new FrontierRuntime();
        Call(app,"Awake");
        try
        {
            app.BeginVisualReview(0,2);
            var game=app.Game;
            game.enemies.Clear();
            game.save.effectsQuality=2;
            game.save.reducedMotion=false;
            var boss=new Enemy(99435,0,
                game.Position.x+1f,game.Position.y+.5f,120f,true);
            game.enemies.Add(boss);
            string idleName="resource:Frontier/Art/Bosses";
            string walkName="resource:Frontier/Art/WalkBossA";
            boss.moving=false;
            Set(app,"clock",30f);
            Clear();
            var idle=Draw(app,"DrawEnemyActor",boss)
                .Single(c=>c.texture.name==idleName);
            float sourceSize=idle.rect.width;
            boss.moving=true;boss.moveTime=.31f;
            Set(app,"clock",30.02f);
            Clear();
            var entering=Draw(app,"DrawEnemyActor",boss);
            var initial=entering.Single(c=>c.texture.name==walkName);
            Check(!entering.Any(c=>c.texture.name==idleName)&&
                Math.Abs(initial.rect.width-sourceSize)<.001f,
                "Regional Boss instantly grew 20 percent or drew a second obsolete Idle torso");
            Check(Math.Abs(initial.rect.y+initial.rect.height*120f/128f-
                (idle.rect.y+idle.rect.height*120f/128f))<.001f,
                "Original Boss whole-body and moving source lost their common planted foot");
            Set(app,"clock",30.0725f);
            Clear();
            var middle=Draw(app,"DrawEnemyActor",boss);
            var running=middle.Single(c=>c.texture.name==walkName);
            Check(!middle.Any(c=>c.texture.name==idleName)&&
                running.rect.width>sourceSize*1.04f&&
                running.rect.width<sourceSize*1.17f,
                "The SINGLE complete Boss body must smoothly interpolate 320-to-384");
            float worldFoot=running.rect.y+running.rect.height*120f/128f;
            float idleFoot=idle.rect.y+idle.rect.height*120f/128f;
            Check(Math.Abs(worldFoot-idleFoot)<.001f,
                "Intermediate single-body transition distorted the original foot pivot");
            GUI.Export(".starfall-checks/sprite-qa",
                "review-boss-full-body-canvas-continuity");
            // Stage change during live idle/run SOURCE handoff: Bosses
            // legitimately glow without having an e.flash or attack
            // interrupt. That glow must share the actual eased canvas,
            // not the 384px destination dimensions.
            boss.hp=boss.maxHp*.5f;
            Set(app,"clock",30.074f);
            Clear();
            var phasePaint=Draw(app,"DrawEnemyActor",boss);
            var phaseBody=phasePaint.Single(
                c=>c.texture.name==walkName);
            var phaseFlash=phasePaint.FirstOrDefault(c=>
                c.texture.name.Contains("flash_resource_Frontier_Art_WalkBossA"));
            Check(phaseFlash!=null&&
                Math.Abs(phaseBody.rect.x-phaseFlash.rect.x)<.001f&&
                Math.Abs(phaseBody.rect.y-phaseFlash.rect.y)<.001f&&
                Math.Abs(phaseBody.rect.width-phaseFlash.rect.width)<.001f&&
                Math.Abs(phaseBody.uv.x-phaseFlash.uv.x)<.00001f&&
                Math.Abs(phaseBody.uv.y-phaseFlash.uv.y)<.00001f,
                "Boss phase flash used nominal 384px while body eased from 320px");
            var phaseMotes=phasePaint.Where(c=>c.texture.name==
                "resource:Frontier/Art/Effects").ToArray();
            Check(phaseMotes.Length==2&&
                phaseMotes[0].color.a>.05f&&
                phaseMotes[0].color.a<.30f&&
                phaseMotes[1].color.a<phaseMotes[0].color.a&&
                Math.Abs(phaseMotes[0].uv.x-.25f)<.00001f&&
                Math.Abs(phaseMotes[1].uv.x-.75f)<.00001f,
                "Boss real HP stage change must paint TWO restrained original-source motes");
            float bodyCenter=phaseBody.rect.x+phaseBody.rect.width*.5f;
            float phaseFoot=phaseBody.rect.y+
                phaseBody.rect.height*120f/128f;
            float phaseScale=Math.Min(28f,phaseBody.rect.width*.12f);
            Check(Math.Abs(phaseMotes[0].rect.x+phaseScale*.5f-
                    (bodyCenter-phaseBody.rect.width*.23f))<.001f&&
                Math.Abs(phaseMotes[1].rect.x+phaseScale*.5f-
                    (bodyCenter+phaseBody.rect.width*.23f))<.001f&&
                Math.Abs(phaseMotes[0].rect.y+phaseScale*.5f-
                    (phaseFoot-phaseBody.rect.width*.60f))<.001f,
                "Phase VFX displaced relative to the physically rendered Boss body");
            GUI.Export(".starfall-checks/sprite-qa",
                "review-boss-phase-source-painted-motes");
            // These live artist parameters must REACH the real world
            // renderer. A resource read alone is not sufficient.
            var visual=typeof(FrontierRuntime).GetProperty(
                "CurrentWorldVisualLook",
                BindingFlags.Instance|BindingFlags.NonPublic)
                .GetValue(app,null);
            var visualType=visual.GetType();
            var lightField=visualType.GetField("bossPhaseMoteAlpha");
            var durationField=visualType.GetField(
                "nonheroAtlasHandoffSeconds");
            Check(lightField!=null&&durationField!=null,
                "Boss rendering no longer exposes bounded WorldLook controls");
            float originalLight=(float)lightField.GetValue(visual);
            float originalDuration=(float)durationField.GetValue(visual);
            try
            {
                lightField.SetValue(visual,originalLight*.5f);
                Clear();
                var dimmed=Draw(app,"DrawEnemyActor",boss);
                var dimMote=dimmed.First(c=>c.texture.name==
                    "resource:Frontier/Art/Effects");
                Check(Math.Abs(dimMote.color.a-
                    phaseMotes[0].color.a*.5f)<.0001f,
                    "WorldLook Boss phase brightness was never used");
                lightField.SetValue(visual,originalLight);
                durationField.SetValue(visual,.15f);
                Clear();
                var slower=Draw(app,"DrawEnemyActor",boss);
                float slowerWidth=slower.Single(c=>
                    c.texture.name==walkName).rect.width;
                Check(slowerWidth>sourceSize&&
                    slowerWidth<phaseBody.rect.width-1f,
                    "WorldLook nonhero transition duration does not drive real frame size");
            }
            finally
            {
                lightField.SetValue(visual,originalLight);
                durationField.SetValue(visual,originalDuration);
                Clear();Draw(app,"DrawEnemyActor",boss);
            }
            // Interrupt midway: last actually painted size is the new
            // transition start, not the unseen 384px nominal source.
            boss.moving=false;
            Set(app,"clock",30.079f);
            Clear();
            var reverse=Draw(app,"DrawEnemyActor",boss);
            var resumed=reverse.Single(c=>c.texture.name==idleName);
            Check(!reverse.Any(c=>c.texture.name==walkName)&&
                Math.Abs(resumed.rect.width-phaseBody.rect.width)<.001f,
                "Rapid Run->Idle reversal jumped in size or painted two complete bodies");
            Set(app,"clock",30.14f);
            Clear();
            var shrinking=Draw(app,"DrawEnemyActor",boss);
            float sizeNow=shrinking.Single(c=>c.texture.name==idleName).rect.width;
            Check(sizeNow>sourceSize&&sizeNow<running.rect.width,
                "Reverse animation should decrease one complete-body canvas");
            Set(app,"clock",30.22f);
            Clear();
            var settled=Draw(app,"DrawEnemyActor",boss);
            Check(settled.Count(c=>c.texture.name==idleName)==1&&
                !settled.Any(c=>c.texture.name==walkName)&&
                Math.Abs(settled.Single(c=>c.texture.name==idleName).rect.width-
                    sourceSize)<.001f,
                "Boss transition still paints stale afterimage after 105ms");
            // Native alpha and gameplay contact remain fully authoritative:
            // zero FX must select immediate full-size source without blend.
            game.save.effectsQuality=0;
            boss.moving=true;
            Set(app,"clock",30.25f);Clear();
            var staticWalk=Draw(app,"DrawEnemyActor",boss);
            Check(staticWalk.Count(c=>c.texture.name==walkName)==1&&
                !staticWalk.Any(c=>c.texture.name==idleName)&&
                Math.Abs(staticWalk.Single(c=>c.texture.name==walkName).
                    rect.width-sourceSize*384f/320f)<.001f,
                "Low FX must retain original walk canvas without transition");
            Check(!staticWalk.Any(c=>c.texture.name==
                    "resource:Frontier/Art/Effects"),
                "Zero effects must not add optional Boss stage-change particles");
            game.save.effectsQuality=2;
            game.save.reducedMotion=true;
            boss.hp=24f; // real stage-three HP threshold from stage two
            Set(app,"clock",30.26f);Clear();
            var stable=Draw(app,"DrawEnemyActor",boss);
            Check(!stable.Any(c=>c.texture.name==
                    "resource:Frontier/Art/Effects")&&
                stable.Any(c=>c.texture.name==walkName),
                "Reduced motion must keep original Boss body/phase without staged VFX");
            Console.WriteLine("PASS 320/384 Boss one-body Sprite easing, flash/mote alignment, reversal without stale full torso, low FX and reduced motion");
        }
        finally
        {
            Call(app,"OnDestroy");
            Clear();
        }
    }

    // Main CI now runs actual common-foe and regional-Boss Idle/Walk,
    // interruption and 2-direction identity checks as an executable test,
    // not just an unused optional visual contact sheet.
    // Exercise the REAL enemy/Boss DrawEnemyActor body and genuine
    // Enemy.hitTime, never just an isolated VFX gallery.
    public static void VerifyQueuedSkillHudSingleDispatch(FrontierRuntime app)
    {
        foreach(bool compact in new[]{false,true})
        {
            app.BeginVisualReview(0,1);
            var g=app.Game;g.enemies.Clear();
            int clicks=0;
            try
            {
                Set(app,"combatHudTime",compact?2f:0f);
                g.save.energy=100;g.Skill(new Point(0,-1));
                // BeginVisualReview starts a neutral world; fresh Q is an
                // action. Reset the live action and start an actual attack
                // so the next Q enters its original 200ms buffered path.
                g.ClearBufferedCombatInputs();
                g.playerAction=null;g.skillCooldown=0f;
                g.Attack(new Point(1,0));
                g.Skill(new Point(0,-1));
                Check(g.SkillQueued&&g.playerAction!=null&&
                    g.playerAction.kind=="attack",
                    "Q HUD fixture requires an actual pending combat skill");
                Action action=()=>{clicks++;g.Skill(new Point(1,0));};
                float y=compact?732f:699f,w=compact?78f:109f,
                    h=compact?50f:75f;
                Clear();GUI.queuedClick=new Rect(354f,y,w,h);
                Call(app,"ActionSlot",354f,"Q","Skill","sword",0f,
                    true,action,3.5f,"Low energy",false);
                Check(clicks==1&&g.SkillQueued&&
                    GUI.buttonCalls.Count>0&&
                    GUI.buttonCalls[GUI.buttonCalls.Count-1].enabled,
                    "Queued Q HUD must allow deliberate retargeting without a second cast");
                object intent=typeof(GameCore).GetField("bufferedSkill",Private)
                    .GetValue(g);
                var flags=BindingFlags.Instance|BindingFlags.Public;
                Point updated=(Point)intent.GetType().GetField("direction",flags)
                    .GetValue(intent);
                Check(updated.x>.99f&&Math.Abs(updated.y)<.01f,
                    "Repeated HUD Q press did not update its original queued aim");
                Check(string.Join(",",GUI.commands.ToArray()).Contains(g.L("已排队","QUEUED")),
                    "Q HUD must display its actively retargetable queued skill");
                GUI.queuedClick=null;
                g.ClearBufferedCombatInputs();
                Clear();GUI.queuedClick=new Rect(354f,y,w,h);
                Call(app,"ActionSlot",354f,"Q","Skill","sword",0f,
                    true,action,3.5f,"Low energy",false);
                Check(clicks==2&&g.SkillQueued,
                    "After cancellation the same real HUD Q button must requeue");
            }
            finally
            {
                GUI.queuedClick=null;
                app.EndVisualReview();Clear();
            }
        }
        Console.WriteLine("PASS both production HUD layouts display queued Q, permit last-aim retarget and requeue after cancellation");
    }

    public static void VerifyDodgeHudEarlyClick(FrontierRuntime app)
    {
        // Invoke the actual shared production HUD ActionSlot and its
        // GUI.Button event, not an invented test-only acceptance helper.
        foreach(bool compact in new[]{false,true})
        {
            app.BeginVisualReview(0,1);
            var g=app.Game;
            bool reduced=g.save.reducedMotion;
            try
            {
                g.enemies.Clear();g.save.companion=-1;
                g.save.energy=100;g.dashCooldown=.10f;
                g.save.reducedMotion=true;g.SetAim(new Point(0,-1));
                Set(app,"combatHudTime",compact?2f:0f);
                g.movementDirection=new Point();
                Point stationary=(Point)Call(app,"HudDodgeDirection");
                Check(stationary.y<-.99f,
                    "Stationary HUD dodge must retain actual aim direction");
                g.movementDirection=new Point(-1,0);
                g.playerAction=new ActorAction{kind="attack",
                    duration=.5f,contact=.3f};
                Point duringAttack=(Point)Call(app,"HudDodgeDirection");
                Check(duringAttack.y<-.99f,
                    "Attack-root animation cannot falsely steer the HUD dodge");
                g.playerAction=null;
                int count=0;
                Action click=()=>{count++;
                    g.Dash((Point)Call(app,"HudDodgeDirection"));};
                float y=compact?732f:699f,w=compact?78f:109f,
                    h=compact?50f:75f;
                Check(g.CanBufferDodge&&!g.DodgeQueued,
                    "HUD starting fixture must be inside real late dodge window");
                Clear();GUI.queuedClick=new Rect(818f,y,w,h);
                Call(app,"ActionSlot",818f,"SPACE","Dodge","shield",
                    g.dashCooldown,true,click,.75f,"Low energy",false);
                Check(count==1&&g.DodgeQueued&&
                    g.dashTime==0f&&g.save.energy==100f,
                    "Actual HUD click must queue once without paying energy early");
                Check(string.Join(",",GUI.commands.ToArray()).Contains(g.L("可预输入","BUFFER")),
                    "Ready-to-buffer HUD must explain its blue active state");
                Clear();GUI.queuedClick=new Rect(818f,y,w,h);
                Call(app,"ActionSlot",818f,"SPACE","Dodge","shield",
                    g.dashCooldown,true,click,.75f,"Low energy",false);
                Check(count==1&&g.DodgeQueued&&
                    string.Join(",",GUI.commands.ToArray()).Contains(g.L("已排队","QUEUED")),
                    "Queued HUD button must show confirmation, not spend twice");
                Check(GUI.buttonCalls.Count>0&&
                    !GUI.buttonCalls[GUI.buttonCalls.Count-1].enabled,
                    "Queued HUD should disable repeated click until resolved");
                GUI.queuedClick=null;
                g.Tick(.05f,new Point());g.Tick(.05f,new Point());
                Point actualDash=(Point)typeof(GameCore).GetField(
                    "dashDirection",Private).GetValue(g);
                Check(!g.DodgeQueued&&g.dashTime>0f&&
                    g.save.energy>=80f&&g.save.energy<81.5f&&
                    actualDash.x<-.99f&&Math.Abs(actualDash.y)<.01f,
                    "HUD queuing must dodge along real moving feet, not unrelated aim");
                // Outside the final window the mouse click remains disabled.
                g.dashTime=0f;g.dashCooldown=.42f;
                Clear();GUI.queuedClick=new Rect(818f,y,w,h);
                Call(app,"ActionSlot",818f,"SPACE","Dodge","shield",
                    g.dashCooldown,true,click,.75f,"Low energy",false);
                Check(count==1&&!g.DodgeQueued,
                    "Cooldown outside buffer window incorrectly accepted HUD click");
                // The same UI must not grant a dodge before the energy cost.
                g.dashCooldown=.09f;g.save.energy=0;
                Clear();GUI.queuedClick=new Rect(818f,y,w,h);
                Call(app,"ActionSlot",818f,"SPACE","Dodge","shield",
                    g.dashCooldown,false,click,.75f,"Low energy",false);
                Check(count==1&&!g.DodgeQueued,
                    "HUD accepted free early dodge at zero energy");
                // A real gate can close OVER the current player root.
                // Both HUD layouts must then disable fresh and late
                // buffered dodge clicks, and say why the move is unavailable.
                g.save.energy=100f;g.dashCooldown=.10f;
                int row=(int)g.Position.y,column=(int)g.Position.x;
                string openRow=g.Map.rows[row];
                try
                {
                    char[] closed=openRow.ToCharArray();
                    closed[column]='#';g.Map.rows[row]=new string(closed);
                    Check(!g.DodgeRootReady&&!g.CanBufferDodge,
                        "Actual HUD fixture places the playable hero on genuinely sealed ground");
                    foreach(float cooldown in new[]{.10f,0f})
                    {
                        g.dashCooldown=cooldown;
                        Clear();GUI.queuedClick=new Rect(818f,y,w,h);
                        Call(app,"ActionSlot",818f,"SPACE","Dodge","shield",
                            g.dashCooldown,true,click,.75f,"Low energy",false);
                        Check(count==1&&!g.DodgeQueued&&
                            GUI.buttonCalls.Count>0&&
                            !GUI.buttonCalls[GUI.buttonCalls.Count-1].enabled&&
                            string.Join(",",GUI.commands.ToArray()).Contains(
                                g.L("地形封锁","GROUND BLOCKED")),
                            "Sealed ground must disable the live HUD dodge for both final cooldown and ready-now states");
                    }
                }
                finally{g.Map.rows[row]=openRow;}
                g.dashCooldown=.10f;
                Check(g.DodgeRootReady&&g.CanBufferDodge,
                    "A reopened floor restores the live HUD's real buffering eligibility");
                Clear();GUI.queuedClick=new Rect(818f,y,w,h);
                Call(app,"ActionSlot",818f,"SPACE","Dodge","shield",
                    g.dashCooldown,true,click,.75f,"Low energy",false);
                Check(count==2&&g.DodgeQueued&&g.save.energy==100f,
                    "Reopened HUD must queue a genuine fresh dodge exactly once without early spending");
            }
            finally
            {
                GUI.queuedClick=null;
                g.save.reducedMotion=reduced;
                app.EndVisualReview();Clear();
            }
        }
        Console.WriteLine("PASS normal/compact HUD clicks queue one real dodge, visibly confirm, retire, and respect cooldown/stamina limits");
    }

    public static void VerifyQueuedSkillWorldCueOnlyForActualIntent(FrontierRuntime app)
    {
        app.BeginVisualReview(0,1);
        var game=app.Game;
        bool reduced=game.save.reducedMotion;
        int quality=game.save.effectsQuality;
        float originalOpacity=(float)typeof(FrontierRuntime)
            .GetField("worldActorOpacity",Private).GetValue(app);
        try
        {
            Set(app,"worldActorOpacity",1f);
            game.enemies.Clear();
            game.save.energy=100f;
            game.save.reducedMotion=true;
            game.save.effectsQuality=1;
            game.playerHitTime=.15f;
            game.Skill(new Point(0,-1));
            Check(game.SkillQueued&&game.playerAction==null&&
                game.save.energy==100f,
                "A hit-recovery Q must queue one real unpaid skill, not publish its contact");
            Clear();
            var render=Draw(app,"DrawPlayerActor");
            var marks=render.Where(c=>c.rect.width==4f&&
                c.rect.height==4f&&
                Math.Abs(c.color.r-.73f)<.00001f&&
                Math.Abs(c.color.g-.56f)<.00001f&&
                Math.Abs(c.color.b-1f)<.00001f&&
                Math.Abs(c.color.a-.68f)<.00001f).ToArray();
            Check(marks.Length==18,
                "Exactly 18 violet confirmation points must appear for an authentic buffered Q");
            Point grounded=(Point)Call(app,"ScreenPoint",game.Position);
            foreach(var mark in marks)
            {
                float dx=mark.rect.x+2f-grounded.x;
                float dy=mark.rect.y+2f-grounded.y;
                Check(Math.Abs((float)Math.Sqrt(dx*dx+dy*dy)-.63f*36f)<.01f,
                    "Queued Q confirmation must stay rooted to canonical hero feet");
            }
            var directionMarks=render.Where(c=>c.rect.width==4f&&
                c.rect.height==4f&&
                Math.Abs(c.color.r-.83f)<.00001f&&
                Math.Abs(c.color.g-.67f)<.00001f&&
                Math.Abs(c.color.b-1f)<.00001f&&
                Math.Abs(c.color.a-.78f)<.00001f).ToArray();
            Check(directionMarks.Length==8&&
                game.SkillQueuedDirection.y<-.99f,
                "The eight forward reticle points must reflect real queued Q aim");
            Point north=(Point)Call(app,"ScreenPoint",
                game.Position+new Point(0,-1.05f));
            foreach(var dot in directionMarks)
            {
                float dx=dot.rect.x+2f-north.x;
                float dy=dot.rect.y+2f-north.y;
                Check(Math.Abs((float)Math.Sqrt(dx*dx+dy*dy)-.17f*36f)<.01f,
                    "Queued Q aim reticle must follow real input in world coordinates");
            }
            game.Skill(new Point(1,0));
            Check(game.SkillQueued&&game.SkillQueuedDirection.x>.99f&&
                game.playerAction==null&&game.save.energy==100f,
                "Repeated Q changes preview direction without paying or firing early");
            Clear();
            var changed=Draw(app,"DrawPlayerActor").Where(c=>
                c.rect.width==4f&&c.rect.height==4f&&
                Math.Abs(c.color.r-.83f)<.00001f&&
                Math.Abs(c.color.g-.67f)<.00001f&&
                Math.Abs(c.color.b-1f)<.00001f&&
                Math.Abs(c.color.a-.78f)<.00001f).ToArray();
            Check(changed.Length==8,
                "Retargeting should preserve exactly one eight-point Q aim preview");
            Point east=(Point)Call(app,"ScreenPoint",
                game.Position+new Point(1.05f,0));
            foreach(var dot in changed)
            {
                float dx=dot.rect.x+2f-east.x,dy=dot.rect.y+2f-east.y;
                Check(Math.Abs((float)Math.Sqrt(dx*dx+dy*dy)-.17f*36f)<.01f,
                    "Repeated queued Q must update its reticle to the new player-selected facing");
            }
            Check(!render.Any(c=>c.rect.width==4f&&
                Math.Abs(c.color.r-.94f)<.00001f&&
                Math.Abs(c.color.g-.72f)<.00001f),
                "Q must not draw an ordinary attack's amber confirmation");
            game.ClearBufferedCombatInputs();
            Clear();
            Check(!game.SkillQueued&&!Draw(app,"DrawPlayerActor").Any(c=>
                c.rect.width==4f&&
                Math.Abs(c.color.r-.73f)<.00001f&&
                Math.Abs(c.color.g-.56f)<.00001f),
                "Canceled or expired Q must immediately retire its world cue");
            game.Skill(new Point(1,0));
            Check(game.SkillQueued,
                "A fresh Q pressed during valid recoil still becomes a genuine queue");
            game.mode="pause";
            Clear();
            Check(!game.SkillQueued&&!Draw(app,"DrawPlayerActor").Any(c=>
                c.rect.width==4f&&
                Math.Abs(c.color.r-.73f)<.00001f&&
                Math.Abs(c.color.g-.56f)<.00001f),
                "Pausing must clear Q and its contact confirmation together");
            Console.WriteLine("PASS real deferred Q shows 18 fixed rooted violet pixels through Hit, no phantom spell, and immediate cancel/pause cleanup");
        }
        finally
        {
            game.save.reducedMotion=reduced;
            game.save.effectsQuality=quality;
            Set(app,"worldActorOpacity",originalOpacity);
            app.EndVisualReview();Clear();
        }
    }

    public static void VerifyQueuedAttackWorldCueFollowsRealGameCore(FrontierRuntime app)
    {
        app.BeginVisualReview(0,1);
        var g=app.Game;
        bool reduced=g.save.reducedMotion;
        int quality=g.save.effectsQuality;
        float opacity=(float)typeof(FrontierRuntime)
            .GetField("worldActorOpacity",Private).GetValue(app);
        try
        {
            Set(app,"worldActorOpacity",1f);
            g.enemies.Clear();
            g.save.energy=100f;
            g.save.reducedMotion=true;
            g.save.effectsQuality=1;
            g.Skill(new Point(1,0));
            ActorAction oldCast=g.playerAction;
            Check(oldCast!=null&&oldCast.kind=="skill",
                "The world attack-confirmation cue must begin with a REAL Q action");
            for(int i=0;i<11;i++)g.Tick(.05f,new Point());
            Check(oldCast.released&&g.playerAction==oldCast,
                "The action still belongs to an actual released canonical Cast");
            g.Attack(new Point(0,-1));
            Check(g.AttackQueued&&g.playerAction==oldCast,
                "Only the real queued attack may paint amber foot confirmation");
            Clear();
            var drawings=Draw(app,"DrawPlayerActor");
            var marks=drawings.Where(c=>c.rect.width==4f&&
                c.rect.height==4f&&
                Math.Abs(c.color.r-.94f)<.00001f&&
                Math.Abs(c.color.g-.72f)<.00001f&&
                Math.Abs(c.color.b-.42f)<.00001f&&
                Math.Abs(c.color.a-.70f)<.00001f).ToArray();
            Check(marks.Length==12,
                "Twelve real fixed amber input-confirmation points must accompany the source-authored hero body");
            Point foot=(Point)Call(app,"ScreenPoint",g.Position);
            foreach(var mark in marks)
            {
                float dx=mark.rect.x+2f-foot.x,dy=mark.rect.y+2f-foot.y;
                Check(Math.Abs((float)Math.Sqrt(dx*dx+dy*dy)-.56f*36f)<.01f,
                    "A queued attack marker must remain rooted beneath the canonical hero instead of floating beside the contact Sprite");
            }
            g.ClearBufferedCombatInputs();
            Clear();
            Check(!g.AttackQueued&&!Draw(app,"DrawPlayerActor").Any(c=>
                c.rect.width==4f&&
                Math.Abs(c.color.r-.94f)<.00001f&&
                Math.Abs(c.color.g-.72f)<.00001f),
                "An interrupted/replaced attack immediately removes its visual queue confirmation");
            g.Attack(new Point(0,-1));
            Check(g.AttackQueued,"The authentic released Cast can still accept another last-window LMB");
            g.mode="pause";
            Check(!g.AttackQueued,
                "Pause and conversation transitions must retire the queued attack and its visual indicator");
            Console.WriteLine("PASS real Q-to-LMB gives 12 planted amber confirmation points, no fake release, respects reduced motion, and vanishes on cancel/pause");
        }
        finally
        {
            g.save.reducedMotion=reduced;
            g.save.effectsQuality=quality;
            Set(app,"worldActorOpacity",opacity);
            app.EndVisualReview();Clear();
        }
    }

    public static void VerifyBufferedDodgeWorldCue(FrontierRuntime app)
    {
        app.BeginVisualReview(0,1);
        var game=app.Game;
        bool reduced=game.save.reducedMotion;
        int quality=game.save.effectsQuality;
        float previousOpacity=(float)typeof(FrontierRuntime)
            .GetField("worldActorOpacity",Private).GetValue(app);
        try
        {
            Set(app,"worldActorOpacity",1f);
            game.enemies.Clear();
            game.save.energy=100;game.save.reducedMotion=true;
            game.save.effectsQuality=1;
            game.dashCooldown=.10f;
            game.Dash(new Point(0,-1));
            Check(game.DodgeQueued&&game.dashTime<=0f,
                "Dodge confirmation must represent a real queued input");
            Clear();
            var pending=Draw(app,"DrawPlayerActor");
            var marks=pending.Where(c=>c.rect.width==4f&&
                c.rect.height==4f&&
                Math.Abs(c.color.r-.48f)<.00001f&&
                Math.Abs(c.color.g-.78f)<.00001f&&
                Math.Abs(c.color.b-1f)<.00001f&&
                Math.Abs(c.color.a-.66f)<.00001f).ToArray();
            Check(marks.Length==16,
                "Production hero does not show its 16 planted dodge-confirmation pixels");
            Point foot=(Point)Call(app,"ScreenPoint",game.Position);
            foreach(var mark in marks)
            {
                float dx=mark.rect.x+2f-foot.x;
                float dy=mark.rect.y+2f-foot.y;
                float distance=(float)Math.Sqrt(dx*dx+dy*dy);
                Check(Math.Abs(distance-.49f*36f)<.01f,
                    "Buffered dodge confirmation drifted from rooted hero feet");
            }
            game.ClearBufferedCombatInputs();
            Clear();
            Check(!game.DodgeQueued&&!Draw(app,"DrawPlayerActor").Any(c=>
                c.rect.width==4f&&
                Math.Abs(c.color.r-.48f)<.00001f&&
                Math.Abs(c.color.g-.78f)<.00001f),
                "Cancelled dodge must immediately retire its world confirmation");
            game.dashCooldown=.10f;game.Dash(new Point(1,0));
            game.mode="pause";
            Check(!game.DodgeQueued,"Pause must retire queued dodge cue and input");
            Console.WriteLine("PASS queued dodge is visibly confirmed beneath actual hero; no fake dash, static reduced-motion cue, anchored 16-pixel ring and immediate cancellation");
        }
        finally
        {
            game.save.reducedMotion=reduced;
            game.save.effectsQuality=quality;
            Set(app,"worldActorOpacity",previousOpacity);
            app.EndVisualReview();Clear();
        }
    }

    public static void VerifyEnemyFootfallsFollowGenuinePaintedContacts(FrontierRuntime app)
    {
        app.BeginVisualReview(0,1);
        var game=app.Game;
        try
        {
            var marks=(System.Collections.IList)typeof(FrontierRuntime)
                .GetField("footfallMarks",Private).GetValue(app);
            var previous=game.save.effectsQuality;
            game.save.effectsQuality=2;
            try
            {
                foreach(int kind in new[]{0,1,2,3})
                {
                    marks.Clear();game.enemies.Clear();
                    bool boss=kind==3;
                    float stride=boss?2.8f:kind==0?1.35f:1.8f;
                    var enemy=new Enemy(991100+kind,kind==3?0:kind,
                        12f,12f,300f,boss)
                        {timer=999f,stun=0f,moveTime=0f,state=0,releaseTime=0f};
                    game.enemies.Add(enemy);
                    Call(app,"AdvanceEnemyFootfalls",game.save.room);
                    Check(marks.Count==0,"A newly observed actor has no fake prior footstep");
                    int expected=0;
                    int priorSourceFrame=GameCore.GaitFrame(0f,stride);
                    for(int step=1;step<=16;step++)
                    {
                        float moved=stride*step/8f;
                        enemy.moveTime=moved;
                        enemy.x=12f+moved;
                        enemy.moving=true;
                        Call(app,"AdvanceEnemyFootfalls",game.save.room);
                        // Float rounding can leave moveTime just before an
                        // ideal half-stride boundary. Judge contacts against
                        // the actual four-cell Sprite frame selected by the
                        // renderer, not the mathematical step counter.
                        int sourceFrame=GameCore.GaitFrame(enemy.moveTime,stride);
                        int beforeExpected=expected;
                        if(sourceFrame!=priorSourceFrame &&
                           (sourceFrame==0||sourceFrame==2))
                            expected++;
                        priorSourceFrame=sourceFrame;
                        Check(marks.Count==expected,
                            "Enemy "+kind+" step "+step+" expected "+expected+
                            " contact marks, found "+marks.Count+" (moveTime="+enemy.moveTime+
                            ", stride="+stride+", sourceFrame="+sourceFrame+
                            ", state="+enemy.state+", release="+enemy.releaseTime+
                            ", stun="+enemy.stun+", moving="+enemy.moving+
                            "); only actual planted Walk Sprite cells 0/2 may own contacts");
                        if(expected>beforeExpected)
                        {
                            object mark=marks[marks.Count-1];
                            bool flipped=(bool)mark.GetType().GetField("alternate",
                                BindingFlags.Instance|BindingFlags.NonPublic|
                                BindingFlags.Public).GetValue(mark);
                            Check(flipped==((expected&1)!=0),
                                "Painted left/right ground contact must alternate at source footfall, not wall-clock phase");
                        }
                    }
                    int frozen=marks.Count;
                    enemy.moving=false;enemy.moveTime+=stride;
                    Call(app,"AdvanceEnemyFootfalls",game.save.room);
                    Check(marks.Count==frozen,
                        "Inactive or windup Boss cannot leave stationary gait footprints");
                    enemy.moveTime=0f;enemy.x=12f;enemy.moving=true;
                    Call(app,"AdvanceEnemyFootfalls",game.save.room);
                    Check(marks.Count==frozen,
                        "A new gait epoch or teleport cannot emit old contact events");
                }
            }
            finally {game.save.effectsQuality=previous;}
            Console.WriteLine("PASS native-compatible enemy and Boss footfall VFX are phase-locked to actual planted walk cells across four actor families");
        }
        finally {app.EndVisualReview();Clear();}
    }

    public static void VerifyCompanionFootfallsMatchAuthoredWalkContacts(FrontierRuntime app)
    {
        app.BeginVisualReview(0,1);
        var game=app.Game;
        int savedId=game.save.companion;
        int savedQuality=game.save.effectsQuality;
        try
        {
            var marks=(System.Collections.IList)typeof(FrontierRuntime)
                .GetField("footfallMarks",Private).GetValue(app);
            game.save.effectsQuality=2;
            float stride=GameCore.WalkStride*1.20f;
            foreach(int companion in new[]{0,1,2})
            {
                marks.Clear();
                game.save.companion=companion;
                game.companionPosition=new Point(12f,12f);
                game.companionMoveTime=0f;
                // Start this source-cell sweep with a moving gait epoch.
                // The real stop->move rebase is covered independently by
                // VerifyCompanionFootfallsFollowGaitRestart.
                game.companionMoving=true;
                game.companionAction=null;
                Call(app,"AdvanceCompanionFootfalls",game.save.room);
                Check(marks.Count==0,
                    "Newly selected companion must not inherit any previously painted footprints");
                for(int step=1;step<=16;step++)
                {
                    float moved=stride*step/8f;
                    game.companionPosition=new Point(12f+moved,12f);
                    game.companionMoveTime=moved;
                    game.companionMoving=true;
                    Call(app,"AdvanceCompanionFootfalls",game.save.room);
                    int expected=step/4;
                    Check(marks.Count==expected,
                        "Companion "+companion+" left a footmark away from its original Walk cell 0/2 grounded contacts");
                    if(step%4==0)
                    {
                        object mark=marks[marks.Count-1];
                        bool alternate=(bool)mark.GetType().GetField("alternate",
                            BindingFlags.Instance|BindingFlags.NonPublic|
                            BindingFlags.Public).GetValue(mark);
                        Check(alternate==((expected&1)!=0),
                            "Companion contact alternation must follow original source art");
                    }
                }
                int before=marks.Count;
                game.companionMoving=false;
                game.companionMoveTime+=stride;
                Call(app,"AdvanceCompanionFootfalls",game.save.room);
                Check(marks.Count==before,
                    "Stationary or commanded companion must not cycle ghost foot contacts");
                game.companionPosition=new Point(12f,12f);
                game.companionMoveTime=0f;
                game.companionMoving=true;
                Call(app,"AdvanceCompanionFootfalls",game.save.room);
                Check(marks.Count==before,
                    "Companion root recovery must not replay stale ground VFX");
            }
            Console.WriteLine("PASS all three actual companion source-walk families emit ground marks at physical gait cells 0/2 and reset across party swaps");
        }
        finally
        {
            game.save.companion=savedId;
            game.save.effectsQuality=savedQuality;
            app.EndVisualReview();Clear();
        }
    }

    public static void VerifyCompanionFootfallsFollowGaitRestart(FrontierRuntime app)
    {
        app.BeginVisualReview(0,1);
        var game=app.Game;
        int savedId=game.save.companion;
        int savedQuality=game.save.effectsQuality;
        var marks=(System.Collections.IList)typeof(FrontierRuntime)
            .GetField("footfallMarks",Private).GetValue(app);
        object style=Call(app,"get_CurrentWorldVisualLook");
        float stride=GameCore.WalkStride*(float)style.GetType().GetField(
            "companionStrideDistanceFactor").GetValue(style);
        string[] names={"Mira","Ivo","Sen"};
        int total=0;
        try
        {
            game.save.effectsQuality=2;
            game.save.reducedMotion=false;
            foreach(int id in new[]{0,1,2})
            foreach(int heading in new[]{0,1,2,3})
            foreach(int passing in new[]{1,3})
            {
                marks.Clear();
                game.save.companion=id;
                game.companionPresentationGeneration++;
                game.companionAction=null;
                game.companionAim=Directions[heading];
                game.companionMoving=true;
                game.companionPosition=new Point(12f,12f);
                float raw=stride*(passing+.5f)/4f;
                game.companionMoveTime=raw;
                int row=GameCore.DirectionRow(Directions[heading]);
                Set(app,"clock",900f+id*20f+heading*2f+passing*.1f);
                Set(app,"companionVisualId",id);
                Set(app,"companionVisualOwner",game);
                Set(app,"companionVisualGeneration",game.companionPresentationGeneration);
                Set(app,"companionLastWalkFrame",passing);
                Set(app,"companionLastWalkFacing",row);
                Set(app,"companionWasWalking",true);
                Set(app,"companionStopTime",800f);
                Set(app,"companionTrackedAction",null);
                Set(app,"companionAttackStartedFromWalk",false);
                Set(app,"companionGaitPhaseOwner",game);
                Set(app,"companionGaitPhaseSave",game.save);
                Set(app,"companionGaitPhaseId",id);
                Set(app,"companionGaitPhaseGeneration",game.companionPresentationGeneration);
                Set(app,"companionGaitPhaseRoom",game.save.room);
                Set(app,"companionGaitPhaseOffset",0f);
                Set(app,"companionGaitLastRawDistance",raw);
                Set(app,"companionGaitPhaseMoving",true);
                Call(app,"AdvanceCompanionFootfalls",game.save.room);
                Check(marks.Count==0,
                    "Restart fixture must establish its real prior planted-contact index");
                Clear();
                var moving=Draw(app,"DrawCompanionActor").Single(ActorTexture);
                Check(AtlasFrame(moving,4,3)%4==passing&&
                    moving.texture.name.EndsWith("/Walk"+names[id]),
                    "Footfall restart fixture must begin on its real source gait key");

                game.companionMoving=false;
                Set(app,"clock",900.01f+id*20f+heading*2f+passing*.1f);
                Call(app,"AdvanceCompanionFootfalls",game.save.room);
                Check(marks.Count==0,
                    "A stationary stop must not manufacture a companion footstep");
                Clear();
                var stopped=Draw(app,"DrawCompanionActor").Single(ActorTexture);
                int planted=passing==1?2:0;
                Check(AtlasFrame(stopped,4,3)%4==planted,
                    "Stopped companion must hold the next original planted source cell");

                game.companionMoving=true;
                game.companionMoveTime+=.02f;
                game.companionPosition=new Point(12.02f,12f);
                Set(app,"clock",900.03f+id*20f+heading*2f+passing*.1f);
                Call(app,"AdvanceCompanionFootfalls",game.save.room);
                int expectedMarks=passing==1?1:0;
                Check(marks.Count==expectedMarks,
                    "Ground VFX must follow the same rebased contact index as the resumed body");
                if(expectedMarks==1)
                {
                    object mark=marks[0];
                    bool alternate=(bool)mark.GetType().GetField("alternate",
                        BindingFlags.Instance|BindingFlags.NonPublic|
                        BindingFlags.Public).GetValue(mark);
                    Point position=(Point)mark.GetType().GetField("position",
                        BindingFlags.Instance|BindingFlags.NonPublic|
                        BindingFlags.Public).GetValue(mark);
                    Check(alternate&&Math.Abs(position.x-game.companionPosition.x)<.0001f&&
                        Math.Abs(position.y-game.companionPosition.y)<.0001f,
                        "A resumed frame-2 contact emits one correctly rooted, alternating footfall");
                }
                Clear();
                var resumed=Draw(app,"DrawCompanionActor").Single(ActorTexture);
                Check(AtlasFrame(resumed,4,3)%4==planted&&
                    resumed.texture.name.EndsWith("/Walk"+names[id])&&
                    Math.Abs(resumed.rect.y-stopped.rect.y)<.001f,
                    "Resumed full-body gait and footfall must share the same planted source cell and pivot");
                total++;
            }
            Console.WriteLine("PASS companion planted-contact/footfall restart: "+total+
                " production draw + VFX cases; no airborne or stationary ghost steps");
        }
        finally
        {
            game.save.companion=savedId;
            game.save.effectsQuality=savedQuality;
            app.EndVisualReview();Clear();
        }
    }

    public static void VerifyCanonicalHeroFootfallsMatchWalkCellContacts(FrontierRuntime app)
    {
        app.BeginVisualReview(0,1);
        var game=app.Game;
        try
        {
            var marks=(System.Collections.IList)typeof(FrontierRuntime)
                .GetField("footfallMarks",Private).GetValue(app);
            marks.Clear();game.enemies.Clear();
            game.save.companion=-1;
            game.save.reducedMotion=false;
            game.save.effectsQuality=2;
            game.walkDistance=0f;game.footContacts=0;
            game.movementDirection=new Point();
            Set(app,"paintedFootRoom",-1);
            Set(app,"paintedFootContact",-1);
            Call(app,"AdvanceFootfallVisual",.05f);
            Check(marks.Count==0,
                "First seen canonical hero must not replay past footsteps");
            for(int step=1;step<=16;step++)
            {
                float moved=GameCore.WalkStride*step/8f;
                game.walkDistance=moved;
                game.save.x=12f+moved;
                game.movementDirection=new Point(1f,0f);
                game.footContacts++;
                // This assertion measures contact emission, not decal expiry.
                Call(app,"AdvanceFootfallVisual",0f);
                Check(marks.Count==step/4,
                    "Canonical blonde hero ground contact must match ONLY original planted Walk frames 2/0");
            }
            // An 8-tile same-room teleport must not produce a boot print
            // even when source-authored gait phase advances naturally.
            // The very next legitimate physical foot plant still can.
            int beforeTeleport=marks.Count;
            game.walkDistance+=GameCore.WalkStride*.5f;
            game.save.x+=8f;
            game.footContacts++;
            Call(app,"AdvanceFootfallVisual",.001f);
            Check(marks.Count==beforeTeleport,
                "Same-room hero teleport emitted an impossible planted-foot decal");
            game.walkDistance+=GameCore.WalkStride*.5f;
            game.save.x+=.19f;
            game.footContacts++;
            Call(app,"AdvanceFootfallVisual",.001f);
            Check(marks.Count==beforeTeleport+1,
                "Ground contact did not recover after a physical post-teleport stride");
            int painted=marks.Count;
            game.movementDirection=new Point();
            game.walkDistance+=GameCore.WalkStride;
            game.footContacts+=2;
            Call(app,"AdvanceFootfallVisual",.05f);
            Check(marks.Count==painted,
                "Stationary hero cannot emit new stamps from audio contacts");
            game.walkDistance=0f;
            game.movementDirection=new Point(1f,0f);
            game.footContacts++;
            Call(app,"AdvanceFootfallVisual",.05f);
            Check(marks.Count==painted,
                "Hero teleport/recovery walk-cycle rewind cannot paint old phantom footfalls");
            game.save.reducedMotion=true;
            game.walkDistance=GameCore.WalkStride;
            game.footContacts++;
            Call(app,"AdvanceFootfallVisual",.05f);
            Check(marks.Count==0,
                "Reduced-motion retains its existing no-extra-footprint contract");
            // A loaded save can carry the same room and foot-contact
            // count. That is not permission to reuse old journey decals.
            game.save.reducedMotion=false;
            Call(app,"EmitFootfall",game.Position,game.save.room,
                "stone",false,1f);
            Check(marks.Count==1,"A planted contact marker is available for same-room owner isolation");
            Set(app,"paintedHeroSave",null);
            Call(app,"AdvanceFootfallVisual",.05f);
            Check(marks.Count==0,
                "Loading another save in the same room must retire last journey's physical ground contacts");
            Call(app,"EmitFootfall",game.Position,game.save.room,
                "stone",false,1f);
            Set(app,"paintedHeroOwner",null);
            Call(app,"AdvanceFootfallVisual",.05f);
            Check(marks.Count==0,
                "A replacement GameCore instance must not inherit old combat footsteps even with matching foot counters");
            Console.WriteLine("PASS canonical hero world foot contacts use actual planted Sprite frames, not independent audio cadence; stationary, rewind and reduced-motion stay safe");
        }
        finally {app.EndVisualReview();Clear();}
    }

    public static void VerifyMaterialGroundContactFrames(FrontierRuntime app)
    {
        app.BeginVisualReview(0,1);
        var g=app.Game;
        float originalClock=(float)typeof(FrontierRuntime)
            .GetField("clock",Private).GetValue(app);
        bool reduced=g.save.reducedMotion;
        int quality=g.save.effectsQuality;
        try
        {
            g.save.reducedMotion=false;g.save.effectsQuality=2;
            Set(app,"clock",16f);
            var list=(System.Collections.IList)typeof(FrontierRuntime)
                .GetField("footfallMarks",Private).GetValue(app);
            string[] terrain={"grass","stone","sand","snow","wood"};
            for(int material=0;material<terrain.Length;material++)
            {
                list.Clear();
                Call(app,"EmitFootfall",new Point(12,14),1,
                    terrain[material],material%2==0,1f);
                var mark=list[0];
                var age=mark.GetType().GetField("age",
                    BindingFlags.Instance|BindingFlags.NonPublic|
                    BindingFlags.Public);
                Check(age!=null,"Footfall missing age clock");
                float rootX=0,rootY=0,oldAlpha=float.PositiveInfinity;
                for(int frame=0;frame<4;frame++)
                {
                    age.SetValue(mark,.32f*(frame+.1f)/4f);
                    Clear();
                    var cells=Draw(app,"DrawFootfallMark",mark);
                    Check(cells.Length==1&&cells[0].texture.name==
                        "resource:Frontier/Art/GroundContactFrames"&&
                        cells[0].texture.width==128&&
                        cells[0].texture.height==100,
                        "Production footstep skipped painted material atlas");
                    var sprite=cells[0];
                    float expectedX=(frame+(material%2==0?1:0))/4f;
                    float expectedY=(4-material)/5f;
                    Check(Math.Abs(sprite.uv.x-expectedX)<.00001f&&
                        Math.Abs(sprite.uv.y-expectedY)<.00001f&&
                        Math.Abs(Math.Abs(sprite.uv.width)-.25f)<.00001f&&
                        Math.Abs(sprite.uv.height-.2f)<.00001f,
                        "Material timeline/mirroring selected wrong authored frame");
                    float x=sprite.rect.x+sprite.rect.width*.5f;
                    float y=sprite.rect.y+sprite.rect.height*.70f;
                    if(frame>0)Check(Math.Abs(x-rootX)<.00001f&&
                        Math.Abs(y-rootY)<.00001f,
                        "Real actor foot root drifted between contact keyframes");
                    rootX=x;rootY=y;
                    Check(sprite.color.a<=oldAlpha,
                        "Footstep grows brighter after the planted contact");
                    oldAlpha=sprite.color.a;
                }
            }
            list.Clear();
            Call(app,"EmitFootfall",new Point(12,14),1,"snow",false,.84f);
            var low=list[0];
            low.GetType().GetField("age",
                BindingFlags.Instance|BindingFlags.NonPublic|
                BindingFlags.Public).SetValue(low,.22f);
            g.save.effectsQuality=1;Clear();
            var still=Draw(app,"DrawFootfallMark",low);
            Check(still.Length==1&&Math.Abs(still[0].uv.x)<.00001f&&
                Math.Abs(still[0].uv.y-.2f)<.00001f,
                "Medium quality did not retain static snow contact Sprite");
            g.save.reducedMotion=true;Clear();
            Check(Draw(app,"DrawFootfallMark",low).Length==0,
                "Reduced motion drew optional contact particles");
            g.save.reducedMotion=false;g.save.effectsQuality=2;
            const string key="Frontier/Art/GroundContactFrames";
            Texture2D prior;
            bool had=Resources.TextureOverrides.TryGetValue(key,out prior);
            try
            {
                var accepted=(Texture2D)typeof(FrontierRuntime).GetField(
                    "groundContactSheet",Private).GetValue(app);
                Resources.TextureOverrides[key]=new Texture2D(
                    127,100,TextureFormat.RGBA32,false){name="partial-contact"};
                Set(app,"clock",19f);Clear();
                var partial=Draw(app,"DrawFootfallMark",low);
                Check(partial.Length==1&&partial[0].texture==accepted,
                    "Partial Editor import dropped last complete 20-cell atlas");
                var updated=new Texture2D(
                    128,100,TextureFormat.RGBA32,false){name="complete-contact"};
                Resources.TextureOverrides[key]=updated;
                Set(app,"clock",22f);Clear();
                Check(Draw(app,"DrawFootfallMark",low)[0].texture==updated,
                    "Reimport did not replace old contact atlas in live rendering");
            }
            finally
            {
                if(had)Resources.TextureOverrides[key]=prior;
                else Resources.TextureOverrides.Remove(key);
            }
            Console.WriteLine("PASS 20 authentic terrain contacts: 5 materials, four phases, stable foot roots, mirroring, low FX and Editor import");
        }
        finally
        {
            Set(app,"clock",originalClock);
            g.save.reducedMotion=reduced;g.save.effectsQuality=quality;
            app.EndVisualReview();Clear();
        }
    }

    public static void VerifyAuthoredEnemyHitSparks(FrontierRuntime app)
    {
        app.BeginVisualReview(0,1);
        var g=app.Game;
        int quality=g.save.effectsQuality;
        bool reduced=g.save.reducedMotion;
        float now=(float)typeof(FrontierRuntime).GetField(
            "clock",Private).GetValue(app);
        float opacity=(float)typeof(FrontierRuntime).GetField(
            "worldActorOpacity",Private).GetValue(app);
        const string key="Frontier/Art/EnemyHitSparks";
        Texture2D prior;
        bool had=Resources.TextureOverrides.TryGetValue(key,out prior);
        try
        {
            g.save.effectsQuality=2;
            g.save.reducedMotion=false;
            Set(app,"worldActorOpacity",1f);
            Set(app,"clock",5f);
            var enemy=new Enemy(99671,0,12,14,125){
                state=0,hitTime=.135f,facing=new Point(1,0)};
            Clear();var live=Draw(app,"DrawEnemyActor",enemy);
            var sparks=live.Where(c=>c.texture.name==
                "resource:Frontier/Art/EnemyHitSparks").ToArray();
            Check(sparks.Length==2&&sparks[0].texture.width==96&&
                sparks[0].texture.height==72&&
                Math.Abs(sparks[0].uv.y-2f/3f)<.00001f&&
                Math.Abs(sparks[1].uv.y-2f/3f)<.00001f&&
                Math.Abs(sparks[0].uv.x)<.00001f&&
                Math.Abs(sparks[1].uv.x-.25f)<.00001f&&
                Math.Abs(sparks[0].rect.x-sparks[1].rect.x)<.00001f&&
                Math.Abs(sparks[0].rect.y-sparks[1].rect.y)<.00001f,
                "Real mossbound reaction did not crossfade original full 24px hit particles");
            var accepted=sparks[0].texture;
            Check(live.Any(c=>c.texture.name==
                    "resource:Frontier/Art/Monsters"||
                    c.texture.name=="resource:Frontier/Art/MossboundRaider")&&
                Math.Abs(enemy.hitTime-.135f)<.00001f,
                "Impact overlay replaced original character or changed authoritative hit timer");
            float firstY=sparks[0].rect.y;
            foreach(int kind in new[]{1,2})
            {
                var foe=new Enemy(99672+kind,kind,12,14,125){
                    hitTime=.135f,state=0,facing=new Point(1,0)};
                Clear();var paint=Draw(app,"DrawEnemyActor",foe);
                var cells=paint.Where(c=>c.texture==accepted).ToArray();
                int row=kind==1?2:1;
                Check(cells.Length==2&&
                    Math.Abs(cells[0].uv.y-(2-row)/3f)<.00001f&&
                    cells[0].rect.width<=34f,
                    "Archer or cinder guard changed hit material source or full body");
            }
            var boss=new Enemy(99677,0,12,14,400,true){
                hitTime=.135f,state=0,facing=new Point(1,0)};
            Clear();var bossPaint=Draw(app,"DrawEnemyActor",boss);
            var bossCells=bossPaint.Where(c=>c.texture==accepted).ToArray();
            Check(bossCells.Length==2&&
                bossCells[0].rect.width>sparks[0].rect.width&&
                bossPaint.Any(c=>c.texture.name==
                    "resource:Frontier/Art/Bosses"||
                    c.texture.name=="resource:Frontier/Art/BriarBeast"),
                "Boss flash dropped its accepted full-body Sprite or spark anchors");
            // The published material fragments are asymmetric: the effect
            // must mirror with the actual living whole-body Sprite, never
            // from a separate cosmetic clock or a stale facing direction.
            var left=new Enemy(99679,0,12,14,125){
                hitTime=.135f,facing=new Point(-1,0)};
            Clear();var leftPaint=Draw(app,"DrawEnemyActor",left);
            var leftSparks=leftPaint.Where(c=>c.texture==accepted).ToArray();
            Check(leftSparks.Length==2&&leftSparks.All(c=>
                    Math.Abs(c.uv.width+.25f)<.00001f)&&
                Math.Abs(leftSparks[0].rect.x-sparks[0].rect.x)<.00001f,
                "Left-facing monster hit particles ignored complete-body Sprite flip");
            // The vertical bow atlas paints distinct upright silhouettes;
            // an old leftward orientation cannot flip North/South sprites.
            var upright=new Enemy(99680,1,12,14,125){hitTime=.135f};
            upright.Face(new Point(-1,0));upright.Face(new Point(0,-1));
            Check(upright.VisualFlip,"Vertical archer fixture lost previous leftward facing");
            Clear();var uprightPaint=Draw(app,"DrawEnemyActor",upright);
            var uprightSparks=uprightPaint.Where(c=>c.texture==accepted).ToArray();
            Check(uprightSparks.Length==2&&uprightSparks.All(c=>
                    Math.Abs(c.uv.width-.25f)<.00001f),
                "Upright archer hit particles used stale horizontal facing");
            // When the hero passes behind a translucent Boss, particles
            // must fade WITH the whole-body Sprite, not float at full alpha.
            float oldHeroX=g.save.x,oldHeroY=g.save.y;
            g.save.x=boss.x+5f;g.save.y=boss.y+5f;
            Clear();var visibleBossSparks=Draw(app,"DrawEnemyActor",boss)
                .Where(c=>c.texture==accepted).ToArray();
            g.save.x=boss.x;g.save.y=boss.y-1f;
            Clear();var occludedBossSparks=Draw(app,"DrawEnemyActor",boss)
                .Where(c=>c.texture==accepted).ToArray();
            g.save.x=oldHeroX;g.save.y=oldHeroY;
            Check(visibleBossSparks.Length==2&&occludedBossSparks.Length==2&&
                Math.Abs(occludedBossSparks[0].color.a-
                    visibleBossSparks[0].color.a*.55f)<.00001f&&
                Math.Abs(occludedBossSparks[1].color.a-
                    visibleBossSparks[1].color.a*.55f)<.00001f,
                "Boss hit sparks stayed opaque while its full-body Sprite faded");
            enemy.hitTime=.09f;
            Clear();var changing=Draw(app,"DrawEnemyActor",enemy);
            var moved=changing.Where(c=>c.texture==accepted).ToArray();
            Check(moved.Length==2&&
                Math.Abs(moved[0].rect.y-firstY)<.00001f&&
                Math.Abs(moved[0].uv.x-.25f)<.00001f&&
                Math.Abs(moved[1].uv.x-.5f)<.00001f,
                "Hit motion drifted off character body or skipped source contact seam");
            Set(app,"worldActorOpacity",.5f);Clear();
            var faded=Draw(app,"DrawEnemyActor",enemy)
                .Where(c=>c.texture==accepted).ToArray();
            Check(faded.Length==2&&
                Math.Abs(faded[0].color.a-moved[0].color.a*.5f)<.00001f,
                "Hit sparks multiplied occlusion opacity twice or missed body fade");
            Set(app,"worldActorOpacity",1f);
            enemy.state=1;Clear();
            Check(!Draw(app,"DrawEnemyActor",enemy).Any(c=>c.texture==accepted),
                "Interruptible windup manufactured a new reaction spark");
            enemy.state=0;enemy.releaseTime=.15f;Clear();
            Check(!Draw(app,"DrawEnemyActor",enemy).Any(c=>c.texture==accepted),
                "Combat release incorrectly drew separate hit-feedback sparks");
            enemy.releaseTime=0f;enemy.hitTime=0f;Clear();
            Check(!Draw(app,"DrawEnemyActor",enemy).Any(c=>c.texture==accepted),
                "Hit sparks continued after real hitTime reached zero");
            enemy.hitTime=.135f;g.save.reducedMotion=true;Clear();
            Check(!Draw(app,"DrawEnemyActor",enemy).Any(c=>c.texture==accepted),
                "Reduced motion failed to remove optional secondary sparks");
            g.save.reducedMotion=false;g.save.effectsQuality=1;Clear();
            Check(!Draw(app,"DrawEnemyActor",enemy).Any(c=>c.texture==accepted),
                "Medium FX setting must retain actor but omit hit particles");
            g.save.effectsQuality=2;
            Resources.TextureOverrides[key]=new Texture2D(
                95,72,TextureFormat.RGBA32,false){name="partial-hit-art"};
            Set(app,"clock",8f);Clear();
            Check(Draw(app,"DrawEnemyActor",enemy).Any(c=>c.texture==accepted),
                "An incomplete Editor source import replaced validated hit sparks");
            var revised=new Texture2D(
                96,72,TextureFormat.RGBA32,false){name="reimported-hit-art"};
            Resources.TextureOverrides[key]=revised;
            Set(app,"clock",11f);Clear();
            Check(Draw(app,"DrawEnemyActor",enemy).Any(c=>c.texture==revised),
                "Completed original hit atlas failed to hot reload in combat");
            Resources.TextureOverrides[key]=accepted;
            Set(app,"clock",0f);Clear();
            Check(Draw(app,"DrawEnemyActor",enemy).Any(c=>c.texture==accepted),
                "Rewound combat capture kept stale hit sparks");
            Console.WriteLine("PASS 12 independently painted enemy/Boss hit sparks: GameCore hit-only, 3 rows, anchored four-frame seams, native body, FX, alpha and importer");
        }
        finally
        {
            if(had)Resources.TextureOverrides[key]=prior;
            else Resources.TextureOverrides.Remove(key);
            Set(app,"clock",now);
            Set(app,"worldActorOpacity",opacity);
            g.save.effectsQuality=quality;
            g.save.reducedMotion=reduced;
            app.EndVisualReview();Clear();
        }
    }

    public static void VerifyEnemySingleBodyHandoffs(FrontierRuntime app)
    {
        try { EnemyAtlasHandoffsKeepOneGroundedIdentity(
            app,".starfall-checks/sprite-qa"); }
        finally {app.EndVisualReview();Clear();}
    }

    public static void VerifyHeroStopPlantContinuity(FrontierRuntime app)
    {
        try { HeroWalkStopUsesNextPlantedSourceCell(
            app,".starfall-checks/sprite-qa"); }
        finally { app.EndVisualReview(); Clear(); }
    }

    public static void VerifyEnemyStopPlantContinuity(FrontierRuntime app)
    {
        var raiderField=typeof(FrontierRuntime).GetField(
            "mossboundRaiderSheet",Private);
        var guardField=typeof(FrontierRuntime).GetField(
            "cinderstoneGuardSheet",Private);
        var briarField=typeof(FrontierRuntime).GetField(
            "briarBeastSheet",Private);
        object originalRaider=raiderField.GetValue(app);
        object originalGuard=guardField.GetValue(app);
        object originalBriar=briarField.GetValue(app);
        try
        {
            // These three full-body sheets are retained as editable review
            // candidates and deliberately NOT loaded by production LoadVisuals.
            // Inject them only for the candidate preview lanes in this QA
            // verifier; the published WalkMonsters/WalkBossA lanes are still
            // exercised by the same real DrawEnemyActor path below.
            Texture2D raider=Resources.Load<Texture2D>(
                "Frontier/Art/MossboundRaider");
            Texture2D guard=Resources.Load<Texture2D>(
                "Frontier/Art/CinderstoneGuard");
            Texture2D briar=Resources.Load<Texture2D>(
                "Frontier/Art/BriarBeast");
            Check(raider!=null&&raider.width==512&&raider.height==768,
                "Mossbound review candidate missing or malformed");
            Check(guard!=null&&guard.width==512&&guard.height==768,
                "Cinderstone review candidate missing or malformed");
            Check(briar!=null&&briar.width==1280&&briar.height==5760,
                "Briar Beast review candidate missing or malformed");
            raiderField.SetValue(app,raider);
            guardField.SetValue(app,guard);
            briarField.SetValue(app,briar);
            EnemyWalkStopUsesNextPlantedSourceCell(
                app,".starfall-checks/sprite-qa");
            EnemyStopPlantDurationIsIndependent(app);
            EnemyStopPlantInterruptions(app,".starfall-checks/sprite-qa");
        }
        finally
        {
            raiderField.SetValue(app,originalRaider);
            guardField.SetValue(app,originalGuard);
            briarField.SetValue(app,originalBriar);
            app.EndVisualReview();Clear();
        }
    }


    // Cover silhouettes and OnGUI previews run the same DRAW function as
    // the physical published companion Sprite. They are observations, not
    // another tick. Repeated preview passes must leave every transition
    // field untouched even as a walk stops or an attack begins.
    static void CompanionOutlinePassCannotConsumeGaitOrAttack(FrontierRuntime app)
    {
        string[] names={"Mira","Ivo","Sen"};
        var flags=BindingFlags.Instance|BindingFlags.NonPublic;
        string[] stateNames={"companionLastWalkFrame",
            "companionLastWalkFacing","companionWasWalking",
            "companionStopTime","companionTrackedAction",
            "companionAttackStartedFromWalk","companionVisualGeneration"};
        var fields=stateNames.Select(n=>
            typeof(FrontierRuntime).GetField(n,flags)).ToArray();
        Check(fields.All(f=>f!=null),
            "Production companion animation has instance-owned gait/action state");
        foreach(int id in new[]{0,1,2})
        foreach(int direction in new[]{0,1,2,3})
        {
            app.BeginVisualReview(0,0);
            var g=app.Game;
            g.save.companion=id;
            g.save.effectsQuality=2;
            g.save.reducedMotion=false;
            g.companionAim=Directions[direction];
            g.companionAction=null;
            g.companionMoving=true;
            g.companionMoveTime=GameCore.WalkStride*1.2f*.375f;
            g.companionPresentationGeneration++;
            Set(app,"clock",304f+id*16f+direction);
            try
            {
                Set(app,"drawingOutline",false);
                Clear();
                var walking=Draw(app,"DrawCompanionActor")
                    .Single(ActorTexture);
                Check(AtlasFrame(walking,4,3)%4==1&&
                    walking.texture.name.EndsWith("/Walk"+names[id]),
                    "Real companion starts on its original airborne whole-body walk key");
                var initial=fields.Select(f=>f.GetValue(app)).ToArray();
                Check((bool)initial[2],
                    "A real moving paint records the pending foot plant");
                // Outline arrives on a newer movement sample, and then on
                // a stopped foot. It must not consume either transition.
                Set(app,"drawingOutline",true);
                g.companionMoveTime=GameCore.WalkStride*1.2f*.625f;
                for(int repeat=0;repeat<3;repeat++)
                {Clear();Draw(app,"DrawCompanionActor");}
                g.companionMoving=false;
                for(int repeat=0;repeat<3;repeat++)
                {Clear();Draw(app,"DrawCompanionActor");}
                for(int i=0;i<fields.Length;i++)
                    Check(System.Object.Equals(initial[i],fields[i].GetValue(app)),
                        "Outline mutated live "+stateNames[i]+
                        " for companion "+names[id]+" facing "+direction);
                Set(app,"drawingOutline",false);
                Clear();
                var stopping=Draw(app,"DrawCompanionActor")
                    .Single(ActorTexture);
                Check(stopping.texture.name.EndsWith("/Walk"+names[id])&&
                    AtlasFrame(stopping,4,3)%4==2&&
                    !(bool)fields[2].GetValue(app),
                    "The FIRST real stop paint must land the source foot 1->2 even after six outline previews");
                // A new walk ends directly in a real command windup.
                g.companionMoving=true;
                g.companionAction=null;
                g.companionMoveTime=GameCore.WalkStride*1.2f*.375f;
                Clear();Draw(app,"DrawCompanionActor");
                Check((bool)fields[2].GetValue(app),
                    "Subsequent actual walking restores the real walk-to-cast transition");
                var command=new ActorAction{
                    direction=Directions[direction],kind="command",
                    elapsed=.025f,contact=.18f,duration=.50f
                };
                g.companionMoving=false;
                g.companionAction=command;
                initial=fields.Select(f=>f.GetValue(app)).ToArray();
                Set(app,"drawingOutline",true);
                for(int repeat=0;repeat<4;repeat++)
                {Clear();Draw(app,"DrawCompanionActor");}
                for(int i=0;i<fields.Length;i++)
                    Check(System.Object.Equals(initial[i],fields[i].GetValue(app)),
                        "Outline swallowed the original walk-to-cast "+stateNames[i]);
                Set(app,"drawingOutline",false);
                Clear();var casting=Draw(app,"DrawCompanionActor");
                Check((bool)fields[5].GetValue(app)&&
                    System.Object.ReferenceEquals(fields[4].GetValue(app),command)&&
                    casting.Count(ActorTexture)==1&&
                    casting.Single(ActorTexture).texture.name.EndsWith(
                        "/Companion"+names[id]),
                    "The next actual combat draw starts ONE approved action body from walking, not an outline-modified idle or ghost silhouette");
                command.released=true;
                initial=fields.Select(f=>f.GetValue(app)).ToArray();
                Set(app,"drawingOutline",true);
                Clear();Draw(app,"DrawCompanionActor");
                for(int i=0;i<fields.Length;i++)
                    Check(System.Object.Equals(initial[i],fields[i].GetValue(app)),
                        "Post-contact cover outline reset a real companion action");
                // A same-room saved-slot/party-generation switch keeps
                // the same companion NUMBER and GameCore object. The old
                // near-diagonal smoothing cannot inherit the previous
                // profile's full-body source facing even if a preview
                // happened first.
                Set(app,"drawingOutline",false);
                g.companionAction=null;
                g.companionMoving=true;
                g.companionAim=new Point(0,-1);
                g.companionMoveTime=GameCore.WalkStride*1.2f*.375f;
                Set(app,"clock",400f+id*10f+direction);
                Clear();var north=Draw(app,"DrawCompanionActor")
                    .Single(ActorTexture);
                Check(AtlasFrame(north,4,3)/4==1,
                    "Companion starts its previous presentation facing north");
                int previousGeneration=g.companionPresentationGeneration;
                g.companionPresentationGeneration++;
                g.companionAim=new Point(.72f,-.69f);
                Set(app,"drawingOutline",true);
                Clear();Draw(app,"DrawCompanionActor");
                Check((int)fields[6].GetValue(app)==previousGeneration,
                    "A preview cannot adopt the next saved-slot generation early");
                Set(app,"drawingOutline",false);
                Clear();var freshEast=Draw(app,"DrawCompanionActor")
                    .Single(ActorTexture);
                Check(AtlasFrame(freshEast,4,3)/4==0&&
                    (int)fields[6].GetValue(app)==g.companionPresentationGeneration,
                    "Newly selected identical-number companion must walk EAST from its own live facing, not inherit a previous near-diagonal NORTH whole-body sheet");
                // The SAME owner may legitimately keep its east row as
                // the aim wobbles across the 45-degree threshold.
                g.companionAim=new Point(.69f,-.72f);
                Clear();var stabilized=Draw(app,"DrawCompanionActor")
                    .Single(ActorTexture);
                Check(AtlasFrame(stabilized,4,3)/4==0,
                    "Same-owner north/east wobble still uses authored source-facing hysteresis");
                g.companionPresentationGeneration++;
                Clear();var nextNorth=Draw(app,"DrawCompanionActor")
                    .Single(ActorTexture);
                Check(AtlasFrame(nextNorth,4,3)/4==1,
                    "A NEW presentation generation must immediately choose its real north body, even on unchanged GameCore and party ID");
            }
            finally
            {
                Set(app,"drawingOutline",false);
                app.EndVisualReview();Clear();
            }
        }
        Console.WriteLine(
            "PASS all 3 companions x 4 source facings: repeated walk/stop/cast outlines are read-only, planted full bodies and single action identities survive");
    }


    public static void VerifyCompanionSurvivesPartialActionAtlasImport(
        FrontierRuntime app)
    {
        string[] names={"Mira","Ivo","Sen"};
        for(int id=0;id<3;id++)
        for(int dir=0;dir<4;dir++)
        for(int state=0;state<3;state++)
        {
            app.BeginVisualReview(0,0);
            GameCore game=app.Game;
            game.save.companion=id;
            game.companionAim=Directions[dir];
            game.companionPosition=new Point(12.5f,12.5f);
            game.companionMoving=state==1;
            game.companionMoveTime=GameCore.WalkStride*.35f;
            game.companionAction=state==2?
                new ActorAction{kind="command",weapon=id,
                    direction=Directions[dir],contact=.16f,
                    duration=.42f,elapsed=.23f,released=true}:null;
            var field=typeof(FrontierRuntime).GetField("companionSheets",Private);
            var walkField=typeof(FrontierRuntime).GetField("companionWalkSheets",Private);
            var actionSheets=(Texture2D[])field.GetValue(app);
            var walkSheets=(Texture2D[])walkField.GetValue(app);
            Check(actionSheets!=null&&walkSheets!=null&&
                walkSheets[id]!=null&&walkSheets[id].width==1024&&
                walkSheets[id].height==768,
                "The original full-body Walk source is required for this import-recovery test");
            Texture2D originalAction=actionSheets[id];
            Texture2D originalWalk=walkSheets[id];
            try
            {
                actionSheets[id]=new Texture2D(133,121,
                    TextureFormat.RGBA32,false){
                    name="bad-partial-companion-"+names[id]};
                Point screen=new Point(520f,375f);
                Place(app,game.companionPosition,screen);
                Clear();var calls=Draw(app,"DrawCompanionActor");
                var actors=calls.Where(ActorTexture).ToArray();
                Check(actors.Length==1&&actors[0].texture==originalWalk,
                    "Partial action import must draw exactly the SAME companion's published Walk body: "+names[id]);
                var body=actors[0];
                Check(Math.Abs(body.rect.width-192f)<.001f&&
                    Math.Abs(body.rect.x-(screen.x-96f))<1.2f&&
                    Math.Abs(body.rect.y-(screen.y-180f))<1.2f,
                    "Fallback keeps full-body source density and unchanged 120/128 planted foot root");
                if(state==2)Check(FrameColumn(body,4)==0,
                    "A missing action sheet may only hold the same companion's painted idle, not fabricated contact art");
                actionSheets[id]=null;
                Clear();var missing=Draw(app,"DrawCompanionActor")
                    .Where(ActorTexture).ToArray();
                Check(missing.Length==1&&missing[0].texture==originalWalk,
                    "A null action import must keep the original actor visible without generic NPC art");
                walkSheets[id]=new Texture2D(301,174,
                    TextureFormat.RGBA32,false){name="bad-partial-walk"};
                Clear();var noIdentity=Draw(app,"DrawCompanionActor")
                    .Where(ActorTexture).ToArray();
                Check(noIdentity.Length==0,
                    "If both original companion atlases fail, never borrow an unrelated identity");
            }
            finally
            {
                actionSheets[id]=originalAction;
                walkSheets[id]=originalWalk;
            }
        }
        Console.WriteLine("PASS companion import recovery: 3 original allies x 4 facings x 3 real poses survive partial/null action art without mismatched costumes");
    }

    public static void VerifyCompanionActionAndWalkAtlasesHotRecover(
        FrontierRuntime app)
    {
        string[] names={"Mira","Ivo","Sen"};
        for(int id=0;id<3;id++)
        {
            app.BeginVisualReview(0,0);
            GameCore game=app.Game;
            game.save.companion=id;
            game.companionAim=Directions[2];
            game.companionMoving=false;
            game.companionAction=new ActorAction{kind="attack",
                weapon=id,direction=Directions[2],released=true,
                elapsed=.25f,duration=.42f,contact=.16f};
            var actionField=typeof(FrontierRuntime).GetField(
                "companionSheets",Private);
            var walkField=typeof(FrontierRuntime).GetField(
                "companionWalkSheets",Private);
            var actions=(Texture2D[])actionField.GetValue(app);
            var walks=(Texture2D[])walkField.GetValue(app);
            Texture2D oldAction=actions[id],oldWalk=walks[id];
            Check(oldAction!=null&&oldWalk!=null,
                "Each test companion needs both original published source sprites");
            try
            {
                // Real import failure: no action body and corrupt walk body.
                actions[id]=null;
                walks[id]=new Texture2D(309,151,
                    TextureFormat.RGBA32,false){name="corrupt-walk"};
                Set(app,"clock",7000f+id*4f);
                Call(app,"RecoverPublishedVisualAtlases");
                Check(actions[id]!=null&&walks[id]!=null&&
                    ActorAtlasGeometry.IsAcceptedCompanionAtlas(actions[id])&&
                    walks[id].width==1024&&walks[id].height==768&&
                    actions[id].name.Contains("Companion"+names[id])&&
                    walks[id].name.Contains("Walk"+names[id]),
                    "Live recovery restores both approved resource atlases for THIS companion without a restart");
                Texture2D recoveredAction=actions[id],recoveredWalk=walks[id];
                Set(app,"clock",7001f+id*4f);
                Call(app,"RecoverPublishedVisualAtlases");
                Check(System.Object.ReferenceEquals(actions[id],recoveredAction)&&
                    System.Object.ReferenceEquals(walks[id],recoveredWalk),
                    "Later ordinary game frames must retain accepted source texture instances, never reimport or reslice valid sprites");
                Place(app,game.companionPosition,new Point(515f,370f));
                Clear();var visible=Draw(app,"DrawCompanionActor")
                    .Where(ActorTexture).ToArray();
                Check(visible.Length==1&&visible[0].texture==recoveredAction,
                    "Restored action art resumes the genuine painted combat keyframe, not a permanent Walk-idle fallback");
            }
            finally
            {
                actions[id]=oldAction;
                walks[id]=oldWalk;
            }
        }
        Console.WriteLine("PASS all three original companions: runtime import recovery reinstalls authentic action/Walk resources and restores painted combat keyframes without scene reload");
    }
    public static void VerifyCompanionGaitContinuity(FrontierRuntime app)
    {
        try
        {
            CompanionGaitActuallyPlantsBothFeet(app);
            VerifyCompanionSurvivesPartialActionAtlasImport(app);
            VerifyCompanionActionAndWalkAtlasesHotRecover(app);
            VerifyCompanionFootfallsFollowGaitRestart(app);
            CompanionOutlinePassCannotConsumeGaitOrAttack(app);
        }
        finally { app.EndVisualReview(); Clear(); }
    }
    public static void VerifyUnifiedGaitPixelComposites(FrontierRuntime app) { FullBodyHeroProductionFrames(app); ProductionLoadoutUsesFullBodyHero(app); }
    public static void VerifyBossMovingFlash(FrontierRuntime app) { BossFlashFollowsActualMovingSilhouette(app); }
    public static void Run(FrontierRuntime app,string folder)
    {
        try{PreparedMasks();VerifyQueuedSkillWorldCueOnlyForActualIntent(app);VerifyQueuedAttackWorldCueFollowsRealGameCore(app);VerifyEnemyFootfallsFollowGenuinePaintedContacts(app);VerifyCompanionFootfallsMatchAuthoredWalkContacts(app);VerifyCompanionFootfallsFollowGaitRestart(app);VerifyCanonicalHeroFootfallsMatchWalkCellContacts(app);FullBodyHeroProductionFrames(app);ProductionLoadoutUsesFullBodyHero(app);Players(app,folder);VerifyGpuOnlyMonsterHitAndSelectionFallback(app);HeroWalkStopUsesNextPlantedSourceCell(app,folder);HeroStopPlantNeverLeaksAcrossSameRoomSaveSwitch(app);Companions(app,folder);CompanionGaitActuallyPlantsBothFeet(app);CompanionOutlinePassCannotConsumeGaitOrAttack(app);VerticalArcherKeepsFacingThroughContact(app,folder);EnemyAtlasHandoffsKeepOneGroundedIdentity(app,folder);EnemyWalkStopUsesNextPlantedSourceCell(app,folder);EnemyStopPlantInterruptions(app,folder);BossFlashFollowsActualMovingSilhouette(app);BriarBeastDeathPreservesLastCombatFrame(app);RegionalBossDeathKeepsLivePhasePalette(app);RegionalBossDeathPreservesLastRenderedPose(app);BossOutlineCannotRewriteLastLiveBodyOrStage(app);MovingBossDeathStaysOnOneRoot(app);EnemyLethalSnapshotSurvivesRetiredAI(app);PatrolDeathRetainsWholeBodyFootfall(app);StationaryDeathUsesActualLastPose(app);VerticalArcherDeathHandoffUsesBowIdentity(app);EnemyAttackFramesHoldThroughFlash(app);Enemies(app,folder);Muzzles(app,folder);}
        finally{Screen.width=1280;Screen.height=800;app.EndVisualReview();Clear();}
    }
}
