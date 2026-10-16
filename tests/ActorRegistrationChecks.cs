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
            g.companionAction=null;
            for(int phase=0;phase<4;phase++)
            {
                g.companionMoving=true;
                g.companionAim=Directions[heading];
                g.companionMoveTime=GameCore.WalkStride*stride*
                    (phase+.5f)/4f;
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

                // Stop must finish FORWARD through actual artist-approved
                // complete-body cells (1->2->3->0, 2->3->0, 3->0).
                g.companionMoving=false;
                Set(app,"clock",10.01f+phase);
                Clear();
                var firstStop=Draw(app,"DrawCompanionActor").Single(ActorTexture);
                int first=(int)Math.Round(Math.Min(firstStop.uv.x,
                    firstStop.uv.x+firstStop.uv.width)*4f);
                Check(first==phase&&firstStop.texture.name==approved&&
                    Math.Abs(firstStop.rect.y-strideDraw.rect.y)<.001f,
                    "Abrupt stop jumped across gait cycle or floated boots");
                for(int n=1;n<=4;n++)
                {
                    Set(app,"clock",10.01f+phase+.075f*n+.002f);
                    Clear();
                    var stepped=Draw(app,"DrawCompanionActor").Single(ActorTexture);
                    int actual=(int)Math.Round(Math.Min(stepped.uv.x,
                        stepped.uv.x+stepped.uv.width)*4f);
                    int expected=phase==0||n>=4-phase?
                        0:(phase+n)%4;
                    Check(actual==expected&&stepped.texture.name==approved&&
                        Math.Abs(stepped.rect.y-strideDraw.rect.y)<.001f,
                        "Stop recovery reversed the stride or shifted the planted root");
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
                // full-body authored anticipation, never hold the old gait
                // instead. An optional 24%-opacity rooted planted step
                // is allowed behind the action for only a few action ticks.
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
                Check(bodies.Length==2,
                    "Walking companion hid its authoritative first cast/attack frame");
                var foot=bodies[0];
                var windup=bodies[1];
                int footFrame=(int)Math.Round(Math.Min(foot.uv.x,
                    foot.uv.x+foot.uv.width)*4f);
                int contact=phase==1?2:phase==3?0:phase;
                Check(foot.texture.name==approved&&
                    footFrame==contact&&foot.color.a>0f&&
                    foot.color.a<=.241f&&
                    windup.texture.name==actions&&
                    windup.color.a>foot.color.a&&
                    Math.Abs(foot.rect.y-strideDraw.rect.y)<.001f&&
                    Math.Abs(windup.rect.y-strideDraw.rect.y)<.001f&&
                    Math.Abs(windup.rect.width-192)<.001f,
                    "Walk->command must keep one exact boot root and full current cast body");
                g.companionAction.elapsed=.035f;
                Clear();
                var middle=Draw(app,"DrawCompanionActor")
                    .Where(ActorTexture).ToArray();
                Check(middle.Length==2&&
                    middle[0].texture.name==approved&&
                    middle[0].color.a<foot.color.a&&
                    middle[1].texture.name==actions&&
                    Math.Abs(middle[1].rect.y-windup.rect.y)<.001f,
                    "Gait afterimage failed to fade while true skill windup continued");
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
        Console.WriteLine("PASS 3 companions x 4 facings x 4 original gait phases: forward footfall recovery, immediate full action frames and faded planted contacts, selection-generation reset and idle");
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
                // The same rich high-detail full-body source now has a
                // tiny dialogue weight-shift around the correct planted foot.
                // No Sprite cell, class face, render bounds or hitbox changes.
                Set(app,"clock",.48f);
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
                Set(app,"clock",1.25f);
                Clear();
                var later=Draw(app,"DrawNpc",npc.kind,
                    new Point(npc.x,npc.y),-1).First(c=>c.texture.name==original);
                Check(Math.Abs(later.matrix.m01-speaking.matrix.m01)>.0001f &&
                    Math.Abs(later.uv.x-speaking.uv.x)<.0001f,
                    "Dialogue animation must evolve without changing authored cells");
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
            var run=entering.FirstOrDefault(c=>c.texture.name==movingName);
            var depart=entering.FirstOrDefault(c=>c.texture.name==idleName);
            Check(run!=null&&depart!=null&&
                depart.color.a>.10f&&depart.color.a<.40f,
                "State transition must retain brief low-alpha source-complete Idle body");
            Check(Math.Abs(run.rect.x-depart.rect.x)<.001f&&
                Math.Abs(run.rect.y+run.rect.height*.9375f-
                    depart.rect.y-depart.rect.height*.9375f)<.001f,
                "Old and new whole-body sprites must plant the SAME boot root");
            Check(!entering.Any(c=>c.texture.name.Contains("PlayerVanguard")||
                c.texture.name.Contains("PlayerRanger")),
                "Nonhero crossfade must not borrow player anatomy");
            GUI.Export(folder,boss?"review-boss-walk-cross-atlas":"review-monster-walk-cross-atlas");
            Set(app,"clock",10.08f);
            Clear();var fading=Draw(app,"DrawEnemyActor",enemy);
            var softer=fading.FirstOrDefault(c=>c.texture.name==idleName);
            Check(softer!=null&&softer.color.a<depart.color.a&&
                softer.color.a>0f,
                "Atlas handoff must decay continuously on real animation time");
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
            Check(restart.Any(c=>c.texture.name==idleName)&&
                restart.Any(c=>c.texture.name==movingName),
                "Expected a new planted atlas handoff before facing change");
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
            Check(newBlend.Any(c=>c.texture.name==idleName),
                "Expected actual source-pose handoff before white hit flash");
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
        }
        Console.WriteLine("PASS complete-body NPC-compatible native Sprite actor lane: common/Boss Idle->Walk planted cross-atlas timing, attack interrupt, face-safe old-art fade and accessibility");
    }

    // This is called by the executable core C# suite, not only by
    // ActorRegistrationChecks.Run's optional review-gallery entrypoint.
    // The accepted regional Boss Idle (320px) and Walk (384px) canvases
    // previously popped 20% in one repaint despite a fade of two bodies.
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
            var leaving=entering.Single(c=>c.texture.name==idleName);
            Check(Math.Abs(initial.rect.width-sourceSize)<.001f&&
                Math.Abs(leaving.rect.width-sourceSize)<.001f,
                "Regional Boss instantly grew 20 percent at Idle->Walk entry");
            Check(Math.Abs(initial.rect.y+initial.rect.height*120f/128f-
                leaving.rect.y-leaving.rect.height*120f/128f)<.001f,
                "Original Boss body and moving overlay lost their shared foot");
            Set(app,"clock",30.0725f);
            Clear();
            var middle=Draw(app,"DrawEnemyActor",boss);
            var running=middle.Single(c=>c.texture.name==walkName);
            var old=middle.Single(c=>c.texture.name==idleName);
            Check(running.rect.width>sourceSize*1.04f&&
                running.rect.width<sourceSize*1.17f&&
                Math.Abs(running.rect.width-old.rect.width)<.001f,
                "The complete Boss body size must smoothly interpolate 320-to-384");
            float worldFoot=running.rect.y+running.rect.height*120f/128f;
            float oldFoot=old.rect.y+old.rect.height*120f/128f;
            Check(Math.Abs(worldFoot-oldFoot)<.001f,
                "Intermediate transition distorted the original foot pivot");
            GUI.Export(".starfall-checks/sprite-qa",
                "review-boss-full-body-canvas-continuity");
            // Interrupt midway: last actually painted size is the new
            // transition start, not the unseen 384px nominal source.
            boss.moving=false;
            Set(app,"clock",30.079f);
            Clear();
            var reverse=Draw(app,"DrawEnemyActor",boss);
            var resumed=reverse.Single(c=>c.texture.name==idleName);
            var prior=reverse.Single(c=>c.texture.name==walkName);
            Check(Math.Abs(resumed.rect.width-running.rect.width)<.001f&&
                Math.Abs(prior.rect.width-resumed.rect.width)<.001f,
                "Rapid Run->Idle reversal returned to old nominal canvas size");
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
            Console.WriteLine("PASS actual 320/384 Boss Sprite body scales: anchored eased walk entry, mid-attack reversals, final source, no-FX immediate");
        }
        finally
        {
            Call(app,"OnDestroy");
            Clear();
        }
    }

    public static void VerifyCompanionGaitContinuity(FrontierRuntime app)
    {
        try { CompanionGaitActuallyPlantsBothFeet(app); }
        finally { app.EndVisualReview(); Clear(); }
    }
    public static void VerifyUnifiedGaitPixelComposites(FrontierRuntime app) { FullBodyHeroProductionFrames(app); ProductionLoadoutUsesFullBodyHero(app); }
    public static void VerifyBossMovingFlash(FrontierRuntime app) { BossFlashFollowsActualMovingSilhouette(app); }
    public static void Run(FrontierRuntime app,string folder)
    {
        try{PreparedMasks();FullBodyHeroProductionFrames(app);ProductionLoadoutUsesFullBodyHero(app);Players(app,folder);Companions(app,folder);CompanionGaitActuallyPlantsBothFeet(app);VerticalArcherKeepsFacingThroughContact(app,folder);EnemyAtlasHandoffsKeepOneGroundedIdentity(app,folder);BossFlashFollowsActualMovingSilhouette(app);BriarBeastDeathPreservesLastCombatFrame(app);RegionalBossDeathKeepsLivePhasePalette(app);RegionalBossDeathPreservesLastRenderedPose(app);MovingBossDeathStaysOnOneRoot(app);EnemyLethalSnapshotSurvivesRetiredAI(app);PatrolDeathRetainsWholeBodyFootfall(app);StationaryDeathUsesActualLastPose(app);VerticalArcherDeathHandoffUsesBowIdentity(app);EnemyAttackFramesHoldThroughFlash(app);Enemies(app,folder);Muzzles(app,folder);}
        finally{Screen.width=1280;Screen.height=800;app.EndVisualReview();Clear();}
    }
}
