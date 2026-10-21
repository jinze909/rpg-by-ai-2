using System;
using System.Collections.Generic;

namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
        private static void Advance(GameCore g, float seconds)
        { while (seconds > .00001f) { float step = Math.Min(.02f, seconds); g.Tick(step, new Point()); seconds -= step; } }
        private static void AwaitArenaOpening(GameCore g)
        { for(int frame=0;frame<100&&g.GateBarrierActive;frame++)g.Tick(.02f,new Point());Check(!g.GateBarrierActive,"The objective gate must finish opening before its Boss challenge"); }
        private static GameCore Fresh(Campaign data, int profession = 0)
        { var g = new GameCore(data); g.NewGame(profession); g.Close(); return g; }
        private static WorldObject Object(GameCore g, string kind)
        { foreach (var o in g.Map.objects) if (o.kind == kind) return o; throw new Exception("Missing object " + kind); }
        private static Point StandableOutsideTalkReach(GameCore g, WorldObject o)
        {
            Point[] offsets={new Point(-2f,0f),new Point(2f,0f),new Point(0f,-2f),new Point(0f,2f),
                new Point(-1.5f,-1.5f),new Point(-1.5f,1.5f),new Point(1.5f,-1.5f),new Point(1.5f,1.5f)};
            foreach(Point offset in offsets)
            {
                Point p=new Point(o.x+offset.x,o.y+offset.y);
                float d=(p-new Point(o.x,o.y)).Length;
                if(d>=1.75f&&d<=2.2f&&g.CanStand(p))return p;
            }
            throw new Exception("No standable regression point outside physical Talk reach for "+o.id);
        }
        private static Point StandableOutsidePhysicalRadius(GameCore g, WorldObject o,float radius)
        {
            Point[] offsets={new Point(-3f,0f),new Point(3f,0f),new Point(0f,-3f),new Point(0f,3f),
                new Point(-2.5f,-2.5f),new Point(-2.5f,2.5f),new Point(2.5f,-2.5f),new Point(2.5f,2.5f)};
            foreach(Point offset in offsets)
            {
                Point p=new Point(o.x+offset.x,o.y+offset.y);
                if((p-new Point(o.x,o.y)).Length>radius&&g.CanStand(p))return p;
            }
            throw new Exception("No standable regression point beyond radius "+radius+" for "+o.id);
        }
        private static void Action(GameCore g, string kind, string action)
        {
            g.Talk(Object(g, kind));
            Check(g.mode == "dialog", "Expected dialogue at " + kind);
            for (int i = 0; i < g.conversation.choices.Length; i++)
                if (g.conversation.choices[i].action == action) { g.Choose(i); return; }
            throw new Exception("Missing action " + action);
        }
        public static void QueuedDodgeCancelsOnRealSealedFooting(Campaign data)
        {
            MapData original=data.maps[0];
            try
            {
                string[] rows=new string[18];
                for(int y=0;y<rows.Length;y++)
                {
                    char[] floor=new string('.',18).ToCharArray();
                    for(int x=0;x<18;x++)
                        if(x<2||x>=16||y<2||y>=16)floor[x]='#';
                    rows[y]=new string(floor);
                }
                data.maps[0]=new MapData{
                    id=0,spawnX=6.5f,spawnY=8.5f,
                    rows=rows,objects=new WorldObject[0],
                    decor=new Decoration[0]};
                var g=Fresh(data);g.enemies.Clear();
                g.save.x=6.5f;g.save.y=8.5f;
                // Fresh world entry grants an unrelated 1s spawn safety
                // window. Remove it before testing that a sealed queue
                // creates NO new dodge invulnerability.
                g.invulnerable=0f;
                g.save.energy=100f;g.dashCooldown=.10f;
                Check(g.DodgeRootReady&&g.CanBufferDodge,
                    "A physically open floor permits the last 140ms dodge buffer");
                g.Dash(new Point(1,0));
                Check(g.DodgeQueued&&g.dashTime==0f&&
                    g.save.energy==100f,
                    "The first real late dodge tap queues but neither moves nor pays");
                char[] sealedFoot=rows[8].ToCharArray();
                sealedFoot[6]='#';rows[8]=new string(sealedFoot);
                Check(!g.CanStand(g.Position)&&!g.DodgeRootReady&&
                    !g.DodgeQueued&&!g.CanBufferDodge,
                    "A newly sealed foot root must immediately hide pending/available dodge feedback");
                g.Tick(.05f,new Point());
                Check(!g.DodgeQueued&&g.dashTime==0f&&
                    g.save.energy==100f&&g.invulnerable<=0f,
                    "A wall-sealed queue cannot buy phantom invulnerability, movement or stamina cost");
                sealedFoot[6]='.';rows[8]=new string(sealedFoot);
                Check(g.DodgeRootReady&&g.CanBufferDodge,
                    "Reopened foot terrain restores genuine late-dodge eligibility");
                g.Dash(new Point(1,0));
                Check(g.DodgeQueued&&g.save.energy==100f,
                    "Fresh escape after gate reopening creates a NEW valid intent");
                g.Tick(.05f,new Point());
                Check(!g.DodgeQueued&&g.dashTime>0f&&
                    g.save.energy<82f&&g.save.energy>=80f,
                    "Reopened buffer must dispatch exactly one real paid dash");
            }
            finally{data.maps[0]=original;}
        }

        public static void SealedPursuitRoutesToLegalFrontierAndReopensImmediately(Campaign data)
        {
            // Keep all authored campaign maps and story objects unchanged.
            // Temporarily substitute a miniature blocked arena for a real
            // GameCore to exercise the production DirectionTo + Fits path.
            MapData original = data.maps[0];
            try
            {
                string[] rows = new string[16];
                for (int y = 0; y < rows.Length; y++)
                {
                    char[] cells = new string('.', 16).ToCharArray();
                    for (int x = 0; x < 16; x++)
                        if (x < 2 || x >= 14 || y < 2 || y >= 14) cells[x] = '#';
                    if (y >= 2 && y < 14) cells[7] = '#';
                    rows[y] = new string(cells);
                }
                data.maps[0] = new MapData {
                    id = 0, spawnX = 4.5f, spawnY = 8.5f,
                    rows = rows, objects = new WorldObject[0],
                    decor = new Decoration[0]
                };
                GameCore g = new GameCore(data);
                Point target = new Point(10.5f, 8.5f);
                Point approach = g.DirectionTo(new Point(4.5f, 8.5f), target);
                Check(approach.x > .9f && Math.Abs(approach.y) < .1f,
                    "Sealed target first draws pursuit toward the nearest reachable legal frontier");
                Point waiting = g.DirectionTo(new Point(6.5f, 8.5f), target);
                Check(waiting.Length < .001f,
                    "At a sealed frontier the pursuer must not walk straight into the wall or jitter");
                Check(g.NavigationPath(target).Count == 0,
                    "Navigation HUD cannot draw a physically false route through a sealed wall");

                char[] doorway = rows[12].ToCharArray(); doorway[7] = '.';
                rows[12] = new string(doorway);
                Point detour = g.DirectionTo(new Point(6.5f, 8.5f), target);
                Check(detour.y > .9f && Math.Abs(detour.x) < .1f,
                    "Opening a distant legal corridor immediately makes AI seek its detour");
                for (int pass = 0; pass < 4; pass++)
                {
                    List<Point> route = g.NavigationPath(target);
                    Check(route.Count > 6 &&
                        (route[route.Count - 1] - target).Length < 1.2f,
                        "Shared HUD and combat BFS workspace must not retain stale parent links, pass " + pass);
                    Point again = g.DirectionTo(new Point(6.5f, 8.5f), target);
                    Check(again.y > .9f,
                        "Shared BFS generations must never corrupt the following enemy detour");
                }
                char[] crossing = rows[8].ToCharArray(); crossing[7] = '.';
                rows[8] = new string(crossing);
                Point direct = g.DirectionTo(new Point(6.5f, 8.5f), target);
                Check(direct.x > .9f && Math.Abs(direct.y) < .1f,
                    "Opening a direct bridge immediately replaces the detour without stale navigation");
            }
            finally { data.maps[0] = original; }
        }

        public static void RealIncomingHitRetiresUnspentDodgeAndLeavesSafeInput(Campaign data)
        {
            GameCore g=Fresh(data);g.enemies.Clear();
            g.save.energy=100f;g.invulnerable=0f;
            g.dashCooldown=.10f;
            g.Dash(new Point(0,-1));
            Check(g.DodgeQueued&&g.dashTime<=0f&&
                Math.Abs(g.save.energy-100f)<.001f,
                "Queued late dodge must hold stamina until a real accepted mobility action");
            var hurt=typeof(GameCore).GetMethod("Hurt",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            Check(hurt!=null,"Production Hero Hurt transaction must exist");
            g.invulnerable=1f;
            hurt.Invoke(g,new object[]{7f,"shot",null});
            Check(g.DodgeQueued&&g.dashTime<=0f,
                "An avoided arrow cannot cancel an accepted cooldown-buffered dodge");
            g.invulnerable=0f;
            float health=g.save.health;
            hurt.Invoke(g,new object[]{7f,"environment",null});
            Check(g.save.health<health&&g.playerHitTime>0f&&
                !g.DodgeQueued&&g.dashTime<=0f,
                "Genuine damage immediately retires unspent dodge before displaying Hit/recoil");
            float stamina=g.save.energy;
            Advance(g,.2f);
            Check(g.dashTime<=0f&&!g.DodgeQueued&&
                g.save.energy>=stamina,
                "Recovery and cooldown completion cannot start a ghost dodge or spend stamina");

            GameCore harmless=Fresh(data);harmless.enemies.Clear();
            harmless.save.energy=100f;
            harmless.dashCooldown=.09f;
            harmless.Dash(new Point(0,-1));
            hurt.Invoke(harmless,new object[]{0f,"environment",null});
            Check(harmless.DodgeQueued,
                "Zero-power warnings cannot consume defensive input");
            Advance(harmless,.091f);
            Check(harmless.dashTime>0f&&
                harmless.invulnerable>0f&&
                harmless.save.energy<100f,
                "After harmless contact a legitimate queued dodge still executes once");
        }

        public static void RepeatedLateDodgeCannotResetDeadlineOrDirection(Campaign data)
        {
            foreach(float step in new[]{.01f,.02f,.05f})
            {
                GameCore g=Fresh(data);g.enemies.Clear();
                g.save.energy=100f;
                g.dashCooldown=.12f;
                g.Dash(new Point(0,-1));
                Check(g.DodgeQueued&&g.dashTime<=0&&g.save.energy==100f,
                    "Initial cooldown-bound dodge must remain an unpaid real intention");
                Check((g.DodgeQueuedDirection-new Point(0,-1)).Length<.001f,
                    "World cue exposes the genuine accepted escape lane before any stamina or immunity starts");
                var pending=typeof(GameCore).GetField("bufferedDodge",
                    System.Reflection.BindingFlags.Instance|
                    System.Reflection.BindingFlags.NonPublic);
                Check(pending!=null,"Real private dodge intent must remain observable to the test");
                object first=pending.GetValue(g);
                var lifetime=first.GetType().GetField("remaining");
                float initial=(float)lifetime.GetValue(first);
                g.Tick(step,new Point());
                float after=(float)lifetime.GetValue(first);
                Check(after<initial&&after>0f&&g.DodgeQueued,
                    "Buffered dodge's original deadline must decay with real gameplay time");
                g.SetAim(new Point(1f,0f));
                g.Dash(new Point(0,1));
                Check(System.Object.ReferenceEquals(first,pending.GetValue(g))&&
                    Math.Abs((float)lifetime.GetValue(first)-after)<.00001f,
                    "Repeated SPACE cannot refresh buffer expiry or replace the initial escape heading");
                Check((g.DodgeQueuedDirection-new Point(0,-1)).Length<.001f,
                    "Directional dodge preview may not rotate to a later held key or pointer aim");
                for(int i=0;i<30&&g.dashTime<=0f;i++)
                    g.Tick(step,new Point());
                Check(g.dashTime>0f&&!g.DodgeQueued&&
                    g.save.energy>=79.9f&&g.save.energy<=80.1f,
                    "Buffered dodge must eventually launch exactly once at true cooldown readiness");
                Check(g.DodgeQueuedDirection.Length<.001f,
                    "Dodge intent marker must retire as soon as the real movement begins");
                Point heading=(Point)typeof(GameCore).GetField(
                    "dashDirection",System.Reflection.BindingFlags.NonPublic|
                    System.Reflection.BindingFlags.Instance).GetValue(g);
                Check(heading.y<-.99f&&Math.Abs(heading.x)<.01f,
                    "Actual dash must preserve first physical tap direction, not last auto-repeat");
                float spent=g.save.energy;
                Advance(g,.40f);
                Check(!g.DodgeQueued&&g.dashTime<=0f&&
                    g.save.energy>=spent,
                    "Repeated dodge input cannot later fire a second unrequested dash");
            }
        }

        public static void BufferedDodgeKeepsDirectionAndCommitsOnce(Campaign data)
        {
            GameCore g=Fresh(data);g.enemies.Clear();
            g.save.x=12;g.save.y=14;g.save.energy=100;
            g.dashCooldown=.10f;
            g.Dash(new Point(0,-1));
            Check(g.dashTime<=0f&&g.save.energy==100f,
                "Early valid dodge cannot spend stamina or start invulnerability");
            g.SetAim(new Point(1,0));
            g.Tick(.04f,new Point());
            Check(g.dashTime<=0f&&g.save.energy==100f,
                "Queued dodge started before actual cooldown completion");
            Advance(g,.061f);
            Check(g.dashTime>.17f&&g.invulnerable>.30f&&
                Math.Abs(g.save.energy-80f)<.001f,
                "Exactly one buffered dodge must start on cooldown readiness");
            Point savedDirection=(Point)typeof(GameCore).GetField(
                "dashDirection",System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.NonPublic).GetValue(g);
            Check(savedDirection.y<-.99f&&Math.Abs(savedDirection.x)<.01f,
                "Deferred dodge must use original input, not later aim direction");
            g.Tick(.02f,new Point());
            Check(g.save.energy>80f&&g.save.energy<82f,
                "Buffered dodge may not double charge energy on later ticks");

            g=Fresh(data);g.enemies.Clear();g.save.energy=100;
            g.Attack(new Point(1,0));
            Check(g.playerAction!=null,"Dodge interrupt requires actual attack");
            g.dashCooldown=.07f;
            g.Dash(new Point(0,1));
            Check(g.playerAction!=null,
                "Queued cooldown dodge cancelled original windup too early");
            Advance(g,.071f);
            Check(g.dashTime>.17f&&g.playerAction==null,
                "Ready buffered dodge must cancel attack and begin real mobility");

            g=Fresh(data);g.enemies.Clear();g.save.energy=100;
            g.dashCooldown=.09f;g.Dash(new Point(-1,0));
            g.mode="inventory";g.mode="world";
            Advance(g,.10f);
            Check(g.dashTime<=0f&&g.save.energy==100f,
                "Closing menu must retire previously buffered dodge");
            g.dashCooldown=.45f;g.Dash(new Point(-1,0));
            Advance(g,.46f);
            Check(g.dashTime<=0f,
                "Dodge input well outside last 140ms cannot execute later");
            g.save.energy=0;g.dashCooldown=.09f;
            g.Dash(new Point(-1,0));
            g.save.energy=100;Advance(g,.10f);
            Check(g.dashTime<=0f&&g.save.energy==100f,
                "Dodge input without enough energy cannot create a future free action");

            g=Fresh(data);g.enemies.Clear();g.save.energy=100;
            g.dashCooldown=.08f;g.Dash(new Point(0,-1));
            g.Attack(new Point(1,0));
            Check(g.playerAction==null&&g.save.energy==100,
                "Held basic attack cannot spend resources before a queued dodge");
            Advance(g,.081f);
            Check(g.dashTime>.17f&&g.save.energy>=80f&&
                g.save.energy<80.1f,
                "Waiting dodge must outrank a simultaneous ordinary attack");

            g=Fresh(data);g.enemies.Clear();g.save.energy=100;
            g.dashCooldown=.09f;g.Dash(new Point(0,-1));
            g.Skill(new Point(1,0));
            Check(g.playerAction!=null&&g.playerAction.kind=="skill"&&
                g.save.energy==60,
                "Newer deliberate Q must override queued dodge without double payment");
            Advance(g,.10f);
            Check(g.dashTime<=0&&g.playerAction!=null&&
                g.playerAction.kind=="skill",
                "A superseded dodge cannot cancel a newer skill windup");

            g=Fresh(data);g.enemies.Clear();g.save.energy=100;
            g.dashCooldown=.09f;g.Dash(new Point(0,-1));
            g.ClearBufferedCombatInputs();
            Advance(g,.10f);
            Check(g.dashTime<=0f&&g.save.energy==100,
                "Application pause/focus must retire all queued combat inputs");
        }

        public static void DodgeWaitsForHitRecoveryAndCooldown(Campaign data)
        {
            var hitOnly=Fresh(data);hitOnly.enemies.Clear();
            hitOnly.save.energy=100f;hitOnly.invulnerable=0f;
            hitOnly.dashCooldown=0f;hitOnly.playerHitTime=.08f;
            Check(hitOnly.CanBufferDodge,
                "A short real hit recovery exposes a bounded dodge buffer when the cooldown is already ready");
            hitOnly.Dash(new Point(1,0));
            Check(hitOnly.DodgeQueued&&hitOnly.dashTime<=0f&&
                hitOnly.save.energy==100f&&hitOnly.invulnerable<=0f&&
                hitOnly.HeroAnimationState==5&&hitOnly.PlayerPose==10,
                "A dodge tap during hit recovery must keep both production render paths on the actual Hit pose");
            hitOnly.Tick(.04f,new Point());
            Check(hitOnly.playerHitTime>0f&&hitOnly.DodgeQueued&&
                hitOnly.dashTime<=0f&&hitOnly.save.energy==100f&&
                hitOnly.invulnerable<=0f,
                "The buffered dodge cannot move, spend stamina or grant i-frames before the hit pose ends");
            hitOnly.Tick(.05f,new Point());
            Check(hitOnly.playerHitTime<=.00001f&&hitOnly.dashTime>.17f&&
                hitOnly.save.energy<81f&&hitOnly.save.energy>=80f&&
                hitOnly.invulnerable>.30f&&hitOnly.HeroAnimationState==4&&
                hitOnly.PlayerPose==8,
                "A short valid hit-recovery dodge dispatches once after the real recovery boundary");

            var both=Fresh(data);both.enemies.Clear();
            both.save.energy=100f;both.invulnerable=0f;
            both.dashCooldown=.10f;both.playerHitTime=.08f;
            both.Dash(new Point(0,-1));
            Check(both.DodgeQueued&&both.dashTime<=0f&&both.save.energy==100f,
                "Cooldown and hit recovery share one bounded queued dodge intent");
            both.Tick(.04f,new Point());
            both.Tick(.04f,new Point());
            Check(both.playerHitTime<=.00001f&&both.dashCooldown>.01f&&
                both.dashTime<=0f&&both.save.energy==100f&&
                both.invulnerable<=0f,
                "Finishing hit recovery alone cannot bypass the remaining dash cooldown");
            both.Tick(.03f,new Point());
            Check(both.dashTime>.17f&&both.invulnerable>.30f&&
                both.save.energy<81f&&both.save.energy>=80f&&
                !both.DodgeQueued,
                "The same queued input dispatches only after BOTH hit recovery and cooldown finish");

            var frozen=Fresh(data);frozen.enemies.Clear();
            frozen.save.energy=100f;frozen.invulnerable=0f;
            frozen.dashCooldown=0f;frozen.hitStop=.08f;
            Check(frozen.CanBufferDodge,
                "A short hit-stop exposes the same bounded input buffer without starting the dodge early");
            frozen.Dash(new Point(0,-1));
            Check(frozen.DodgeQueued&&frozen.dashTime<=0f&&
                frozen.save.energy==100f&&frozen.invulnerable<=0f,
                "A dodge requested during hit-stop remains unpaid until simulation resumes");
            frozen.Tick(.04f,new Point());
            Check(frozen.hitStop>0f&&frozen.DodgeQueued&&
                frozen.dashTime<=0f&&frozen.save.energy==100f&&
                frozen.invulnerable<=0f,
                "A frozen simulation cannot dispatch the queued dodge or advance its input deadline");
            frozen.Tick(.05f,new Point());
            Check(frozen.hitStop<=.00001f&&frozen.dashTime>.17f&&
                frozen.save.energy<81f&&frozen.save.energy>=80f&&
                frozen.invulnerable>.30f,
                "The queued dodge starts once after hit-stop has fully elapsed");

            var tooEarly=Fresh(data);tooEarly.enemies.Clear();
            tooEarly.save.energy=100f;tooEarly.invulnerable=0f;
            tooEarly.dashCooldown=0f;tooEarly.playerHitTime=.35f;
            tooEarly.Dash(new Point(1,0));
            Check(!tooEarly.DodgeQueued&&tooEarly.dashTime<=0f&&
                tooEarly.save.energy==100f&&tooEarly.invulnerable<=0f,
                "A hit-recovery input outside the 140ms window is rejected, not stored for a later free dodge");
            Advance(tooEarly,.4f);
            Check(tooEarly.dashTime<=0f&&tooEarly.save.energy==100f&&
                tooEarly.invulnerable<=0f,
                "An expired early dodge press cannot execute after recovery ends");

            var longFreeze=Fresh(data);longFreeze.enemies.Clear();
            longFreeze.save.energy=100f;longFreeze.invulnerable=0f;
            longFreeze.dashCooldown=0f;longFreeze.hitStop=.35f;
            longFreeze.Dash(new Point(1,0));
            Check(!longFreeze.DodgeQueued&&longFreeze.dashTime<=0f&&
                longFreeze.save.energy==100f&&longFreeze.invulnerable<=0f,
                "A hit-stop outside the 140ms window cannot store a future free dodge");
            Advance(longFreeze,.4f);
            Check(longFreeze.dashTime<=0f&&longFreeze.save.energy==100f&&
                longFreeze.invulnerable<=0f,
                "An expired early hit-stop dodge cannot start after the world unfreezes");
        }

        public static void BufferedSkillRepeatCannotExtendOrRetargetIntent(Campaign data)
        {
            var g=Fresh(data,1);g.enemies.Clear();
            g.save.energy=100;g.playerHitTime=.55f;
            g.Skill(new Point(0,-1));
            Check(g.SkillQueued&&g.playerAction==null&&g.save.energy==100,
                "A Q pressed during real hit recovery must queue without paying early");
            Advance(g,.12f);
            g.SetAim(new Point(1,0));
            g.Skill(new Point(1,0));
            Check(g.SkillQueued&&g.playerAction==null,
                "Repeated Q cannot force a premature skill through hit recoil");
            Advance(g,.09f);
            Check(!g.SkillQueued&&g.playerAction==null&&g.save.energy==100,
                "Repeated Q cannot extend the original 200ms input window");
            Advance(g,.4f);
            Check(g.playerAction==null&&g.skillCooldown<=0&&g.save.energy==100,
                "Expired buffered Q cannot become a free delayed spell");

            g=Fresh(data,1);g.enemies.Clear();g.save.energy=100;
            g.Dash(new Point(-1,0));
            g.Skill(new Point(0,-1));
            Check(g.SkillQueued&&g.save.energy==80,
                "Dodge then Q must preserve energy until Q is actually accepted");
            Advance(g,.06f);
            Point latestTarget=new Point(21,17);
            g.SetAimTarget(latestTarget);
            g.Skill(new Point(1,0));
            Advance(g,.15f);
            Check(g.playerAction!=null&&g.playerAction.kind=="skill"&&
                g.playerAction.direction.x>.99f&&
                Math.Abs(g.playerAction.direction.y)<.01f&&
                g.playerAction.target.HasValue&&
                (g.playerAction.target.Value-latestTarget).Length<.001f&&
                !g.SkillQueued&&g.skillCooldown>0,
                "Repeated Q must retarget the existing queue, without moving its deadline");
            Check(g.save.energy>=40&&g.save.energy<45,
                "Queued Q commits its cost once, not on every repeated press");
        }

        public static void ClassesAndPause(Campaign data)
        {
            for (int i = 0; i < 3; i++)
            {
                var g = Fresh(data, i); Check(g.save.weapon == i, "Class starter weapon");
                g.save.x = 10; g.save.y = 14; float x = g.save.x;
                g.Tick(.05f, new Point(1, 1)); Check(g.save.x > x, "Movement");
                float time = g.save.totalTime; g.mode = "inventory"; g.Tick(.05f, new Point(1, 0)); Check(g.save.totalTime == time, "Menus must pause the simulation");
                g.Close(); g.save.energy = 100; g.Skill(); Check(g.save.energy == 60 && g.skillCooldown > 0, "Class skill cost");
                g.Skill(); Check(g.save.energy == 60, "Skill cannot repeat during cooldown");
            }
        }
        public static void RepeatablePurchasesCannotConsumeResourcesPastSavedStackCaps(Campaign data)
        {
            var g=Fresh(data);g.enemies.Clear();g.save.coins=500;
            g.save.inventory[0]=9997;g.save.inventory[7]=9998;
            g.save.inventory[6]=9999;g.save.inventory[2]=20;
            g.save.inventory[3]=20;g.save.inventory[1]=20;g.save.tea=999;
            int cash=g.save.coins,wood=g.save.inventory[0],
                seeds=g.save.inventory[7],herbs=g.save.inventory[2],
                fish=g.save.inventory[3],crystal=g.save.inventory[1];
            Action(g,"cook","buywood");
            Action(g,"garden","seeds");
            Action(g,"cook","buymeal");
            Action(g,"cook","cook");
            Action(g,"cook","tea");
            Check(g.save.coins==cash&&g.save.inventory[0]==wood&&
                  g.save.inventory[7]==seeds&&g.save.inventory[6]==9999&&
                  g.save.inventory[2]==herbs&&g.save.inventory[3]==fish&&
                  g.save.inventory[1]==crystal&&g.save.tea==999,
                "Full saved stacks reject timber, seeds, purchased/cooked stew and tea before deducting any ingredients or coins");
            g.save.inventory[0]=9995;g.save.inventory[7]=9996;
            g.save.inventory[6]=9998;g.save.tea=998;
            Action(g,"cook","buywood");
            Action(g,"garden","seeds");
            Action(g,"cook","buymeal");
            Action(g,"cook","tea");
            Check(g.save.inventory[0]==9999&&g.save.inventory[7]==9999&&
                  g.save.inventory[6]==9999&&g.save.tea==999&&
                  g.save.coins==cash-g.ShopTimberCost-5-g.ShopStewCost&&
                  g.save.inventory[2]==herbs-2&&g.save.inventory[1]==crystal-1,
                "Exact last available storage space still permits legitimate paid purchases with correct resource costs");
            g.PrepareSave();var loaded=new GameCore(data,CloneSave(g.save));
            Check(loaded.save.inventory[0]==9999&&loaded.save.inventory[7]==9999&&
                  loaded.save.inventory[6]==9999&&loaded.save.tea==999,
                "Full-stack purchases survive v2 save/load without losing paid inventory");
        }
        public static void Transactions(Campaign data)
        {
            var g = Fresh(data); g.save.inventory[1] = 0;
            int wood = g.save.inventory[0], gear = g.save.inventory[4];
            Action(g, "forge", "upgrade"); Check(g.save.inventory[0] == wood && g.save.inventory[4] == gear && g.save.upgrades == 0, "Failed crafting must be atomic");
            g.save.inventory[1] = 10; g.save.inventory[4] = 10; g.save.inventory[0] = 20;
            Action(g, "forge", "upgrade"); Check(g.save.upgrades == 1 && g.save.maxHealth == 140, "Equipment upgrade");
            Action(g, "forge", "bow"); Check(g.Has("weapon_1") && g.save.weapon == 1, "Craft a bow");
            int after = g.save.inventory[0]; Action(g, "forge", "bow"); Check(g.save.inventory[0] == after, "Re-equipping a crafted weapon must be free");
            g.save.inventory[2] = 20; Action(g, "garden", "ally1");
            int coins = g.save.coins; g.Talk(Object(g, "garden"));
            foreach (var c in g.conversation.choices) Check(c.action != "ally1", "Completed quest cannot reward twice");
            Check(g.save.coins == coins && g.Allies == 1 && g.save.skillPoints == 1, "Commission rewards");
            g.Learn(1); Check(g.save.skillPoints == 0 && g.save.maxHealth == 152, "Talent allocation");
        }
        public static void DailyLife(Campaign data)
        {
            var g = Fresh(data); Action(g, "garden", "plant"); Check(g.save.plantedAt == 0 && g.save.inventory[7] == 2, "Planting");
            Action(g, "garden", "harvest"); Check(g.save.plantedAt >= 0, "Cannot harvest early");
            Action(g, "rest", "rest"); Action(g, "rest", "rest");
            int herbs = g.save.inventory[2]; Action(g, "garden", "harvest"); Check(g.save.inventory[2] == herbs + 5 && g.save.plantedAt < 0, "Harvest after elapsed time");
            for (int fish = 0; fish < 3; fish++)
            {
                Action(g, "fish", "fish");
                for (int hit = 0; hit < 3; hit++)
                {
                    // Wait through the actual timing simulation rather than patching the cursor.
                    int frames = 0; do { g.Tick(.02f, new Point()); } while ((Math.Abs(g.fishCursor - .5f) > .08f || frames++ < 5) && frames < 1000);
                    g.FishHit();
                }
                Check(g.mode == "world", "Fishing should finish after three good pulls");
            }
            Check(g.save.inventory[3] == 3, "Fishing rewards"); Action(g, "fish", "ally2"); Check(g.Has("ally_2") && g.save.inventory[3] == 0, "Fisher's commission");
            Action(g, "fish", "fish"); g.Close(); Check(g.save.inventory[3] == 0, "Cancelling fishing must not mint a reward");
            g.save.inventory[3] = 1; Action(g, "cook", "cook");
            g.save.health = 10; int meal = g.save.inventory[6]; g.Eat(); Check(g.save.health == 65 && g.save.inventory[6] == meal - 1 && g.save.foodUntil > g.save.totalTime, "Cooking and food buff");
        }
        public static void GatesAndGathering(Campaign data)
        {
            var g = Fresh(data); g.Enter(3); Check(g.save.room == 0, "Locked routes must remain locked");
            g.Enter(1); g.Talk(Object(g, "scout")); Check(!g.GateOpen, "Scout cannot open the gate before the rescue"); g.Close();
            int herbs = g.save.inventory[2]; var node = Object(g, "herb"); g.Talk(node); g.Talk(node); Check(g.save.inventory[2] == herbs + 3, "A resource node yields once per rest");
            g.save.kills[1] = 4; Action(g, "scout", "scout"); Check(g.GateOpen, "Rescue opens forest gate");
            g.save.stage = 1; g.Enter(2); g.Talk(Object(g, "rune2")); Check(g.runeProgress == 0, "Wrong rune resets puzzle");
            g.Talk(Object(g, "rune0")); g.Talk(Object(g, "rune2")); g.Talk(Object(g, "rune1")); Check(g.GateOpen, "Correct rune order opens ruins");
            g.save.stage = 2; g.Enter(3); g.save.inventory[4] = 0; Action(g, "engine", "engine"); Check(!g.GateOpen, "Mine repair requires parts");
            g.Talk(Object(g, "ore")); Action(g, "engine", "engine"); Check(g.GateOpen && g.save.inventory[4] == 0, "Gathered parts repair elevator");
            for (int room = 0; room < 5; room++)
            {
                g.save.stage = 3; g.save.flags.Add("seal_" + room); g.Enter(room);
                var queue = new Queue<int>(); var seen = new HashSet<int>();
                int width = g.Map.rows[0].Length, height = g.Map.rows.Length;
                int start = (int)g.save.y * width + (int)g.save.x; queue.Enqueue(start); seen.Add(start);
                while (queue.Count > 0)
                {
                    int cell = queue.Dequeue(), x = cell % width, y = cell / width;
                    int[,] offsets = { { 1, 0 }, { -1, 0 }, { 0, 1 }, { 0, -1 } };
                    for (int i = 0; i < 4; i++)
                    {
                        int xx = x + offsets[i, 0], yy = y + offsets[i, 1], next = yy * width + xx;
                        if (xx < 0 || xx >= width || yy < 0 || yy >= height || g.Blocked(xx + .5f, yy + .5f) || seen.Contains(next)) continue;
                        seen.Add(next); queue.Enqueue(next);
                    }
                }
                foreach (var o in g.Map.objects)
                {
                    bool reachable = false;
                    foreach (int cell in seen) if ((new Point(cell % width + .5f, cell / width + .5f) - new Point(o.x, o.y)).Length < 1.7f) { reachable = true; break; }
                    Check(reachable, "Unreachable object in room " + room + ": " + o.id);
                }
            }
        }
        public static void CorruptEnemySnapshotsCannotInjectOrHideValidPatrols(Campaign data)
        {
            var old=new SaveData {
                room=1,snapshotRoom=1,
                worldEnemies=new [] {
                    new EnemyRecord { id=5,kind=0,x=float.NaN,y=14,hp=30,maxHp=30 },
                    new EnemyRecord { id=5,kind=0,x=13,y=14,hp=20,maxHp=30 },
                    new EnemyRecord { id=6,kind=1,x=9999,y=9999,hp=30,maxHp=30 },
                    new EnemyRecord { id=7,kind=2,x=14,y=14,hp=65,maxHp=80 }
                }
            };
            var restored=new GameCore(data,old);
            Check(restored.enemies.Count==3,
                "Malformed duplicate loses to a valid same-ID record while the old safe-location recovery still saves isolated off-map actors");
            Enemy valid=restored.enemies.Find(e=>e.id==5);
            Check(valid!=null&&valid.hp==20&&valid.maxHp==30&&
                  (valid.Position-new Point(13,14)).Length<.001f,
                "A malformed duplicate cannot reserve an ID before its genuine original saved entry");
            Enemy rescued=restored.enemies.Find(e=>e.id==6);
            Check(restored.enemies.Exists(e=>e.id==7)&&rescued!=null&&
                  rescued.hp==30&&restored.CanStand(rescued.Position),
                "An isolated damaged-position enemy still recovers with its saved identity and health");
            restored.PrepareSave();
            var again=new GameCore(data,CloneSave(restored.save));
            Check(again.enemies.Count==3&&again.enemies.Exists(e=>e.id==5)&&
                  again.enemies.Exists(e=>e.id==6)&&again.enemies.Exists(e=>e.id==7),
                "Deduplicated and rescued patrols persist normally across the next old-format save and reload");
        }
        public static void SaveValidation(Campaign data)
        {
            var bad = new SaveData { room = 99, stage = -1, x = float.NaN, y = -300, health = float.PositiveInfinity, coins = -100, inventory = null, talents = null, kills = null, flags = null };
            var g = new GameCore(data, bad); Check(g.save.room == 0 && g.save.x == 18 && g.save.coins == 0 && g.save.health == 120, "Damaged saves are sanitized");
            bool rejected = false; try { new GameCore(data, new SaveData { version = 900 }); } catch (ArgumentException) { rejected = true; }
            Check(rejected, "Unsupported save schema is rejected");
            var legacyClass=new SaveData {
                profession=500,weapon=2,technique=1,
                flags=new List<string> { "weapon_2", "technique_2" }
            };
            var corrected=new GameCore(data,legacyClass);
            Check(corrected.save.profession==2&&corrected.save.weapon==2&&
                  corrected.save.technique==1,
                "A corrupted profession number is repaired before validating a legitimately unlocked alternate skill");
            var noUnlock=new SaveData { profession=500,technique=1 };
            var denied=new GameCore(data,noUnlock);
            Check(denied.save.profession==2&&denied.save.technique==0,
                "Early class correction must never unlock an absent alternate profession skill");

            var negativeXp=new GameCore(data,new SaveData{level=6,xp=-900});
            Check(negativeXp.save.level==6&&negativeXp.save.xp==0,
                "Negative XP cannot persist in a valid old v2 save");
            var levelCap=new GameCore(data,new SaveData{level=30,xp=int.MaxValue});
            Check(levelCap.save.level==30&&levelCap.save.xp==0,
                "Already-maxed legacy saves cannot preserve overflowing XP");
            var justBefore=new GameCore(data,new SaveData{level=29,xp=1159});
            Check(justBefore.save.xp==1159,
                "Ordinary near-level progress is preserved byte-for-byte on load");
            justBefore.enemies.Clear();
            var finalFoe=new Enemy(88131,0,18,14,30);
            justBefore.enemies.Add(finalFoe);
            var hit=typeof(GameCore).GetMethod("Hit",
                System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            hit.Invoke(justBefore,new object[]{finalFoe,99999f,false,null,null});
            Check(justBefore.save.level==30&&justBefore.save.xp==0,
                "Real combat reaching level 30 resets obsolete excess XP");
            var grant=typeof(GameCore).GetMethod("AddXp",
                System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            grant.Invoke(justBefore,new object[]{int.MaxValue});
            Check(justBefore.save.level==30&&justBefore.save.xp==0,
                "At the cap huge quest/bounty payouts cannot overflow or unlock phantom levels");
            g.Enter(1); g.save.x = 2.3f; g.save.y = 14; g.Dash(new Point(-1, 0));
            for (int i = 0; i < 20; i++) g.Tick(.02f, new Point(-1, 0));
            Check(g.save.x >= 2.2f, "Dash cannot tunnel through the map boundary");
        }
        private static void Combat(GameCore g, bool bossOnly)
        {
            int frames = 0, startingRoom = g.save.room;
            while (g.enemies.Exists(e => !bossOnly || e.boss) && frames++ < 30000)
            {
                Check(g.save.room == startingRoom, "Combat bot died in room " + startingRoom);
                Enemy target = null; float distance = float.MaxValue;
                foreach (Enemy e in g.enemies)
                { if (bossOnly && !e.boss) continue; float d = (e.Position - g.Position).Length; if (d < distance) { target = e; distance = d; } }
                if (target == null) break;
                Point toward = g.DirectionTo(g.Position, target.Position), movement = distance > 1.4f ? toward : new Point();
                if (target.state == 1 && (target.boss || target.kind == 2) && (g.Position - target.target).Length < (target.boss ? 3.4f : 2))
                { movement = (g.Position - target.target).Length > .05f ? (g.Position - target.target).Normalized : new Point(-1, 0); g.Dash(movement); }
                foreach (Hazard h in g.hazards) if (h.timer < .5f && (g.Position - h.position).Length < h.radius + .5f)
                { movement = (g.Position - h.position).Length > .05f ? (g.Position - h.position).Normalized : new Point(-1, 0); g.Dash(movement); break; }
                g.Tick(.02f, movement); g.Attack(target.Position - g.Position);
                if (g.save.health < g.save.maxHealth * .8f) g.CommandCompanion();
                if (distance < 3) g.Skill();
                if (g.save.health < g.save.maxHealth * .6f && g.save.inventory[6] > 0) g.Eat();
                if (g.mode == "dialog" && !g.Has("boss_4")) g.Close();
                if (startingRoom == 4 && g.Has("boss_4")) break;
            }
            string remaining = "";
            foreach (var e in g.enemies) remaining += " enemy " + e.kind + " @" + e.x + "," + e.y + " hp=" + e.hp;
            Check(frames < 30000, "Combat failed to converge in room " + g.save.room + " floor=" + g.save.abyssFloor + " profession=" + g.save.profession + " seed=" + g.save.seed + " mode=" + g.mode + " hitstop=" + g.hitStop + " death=" + g.deathTime + " direction=" + (g.enemies.Count>0?g.DirectionTo(g.Position,g.enemies[0].Position).x:0) + "," + (g.enemies.Count>0?g.DirectionTo(g.Position,g.enemies[0].Position).y:0) + " map=" + string.Join("/", g.Map.rows) + " player=" + g.save.x + "," + g.save.y + remaining);
        }
        public static void CompleteCampaign(Campaign data)
        {
            for (int profession = 0; profession < 3; profession++) CampaignRun(data, profession);
        }
        private static void CampaignRun(Campaign data, int profession)
        {
            var g = Fresh(data, profession); // Standard difficulty and starter equipment, with public gameplay actions.
            for (int room = 1; room <= 4; room++)
            {
                g.Enter(room); Check(g.save.room == room, "Chapter route unlock");
                Combat(g, false);
                if (room == 1) Action(g, "scout", "scout");
                if (room == 2) { g.Talk(Object(g, "rune0")); g.Talk(Object(g, "rune2")); g.Talk(Object(g, "rune1")); }
                if (room == 3) { g.Talk(Object(g, "ore")); Action(g, "engine", "engine"); }
                Check(g.GateOpen, "Chapter gate");
                AwaitArenaOpening(g); // Hit stop pauses the same opening clock; wait for actual collision.
                // The original direct test teleport (28,10) places the
                // hero's full-body footprint partly in room 2's water
                // even though its CENTER tile is dry. Full campaign QA
                // must fight from valid ground like actual player input.
                g.save.x = 28.5f; g.save.y = 10.5f;
                Check(g.CanStand(g.Position),
                    "Complete campaign boss setup must place the real hero footprint on legal dry ground");
                Action(g, "shrine", "boss"); Combat(g, true);
                Check(g.Has("boss_" + room),
                    "Guardian defeated: room=" + room + " profession=" + profession +
                    " actualRoom=" + g.save.room + " dying=" + g.Dying +
                    " mode=" + g.mode + " health=" + g.save.health +
                    " enemies=" + g.enemies.Count + " state=" +
                    (g.enemies.Count > 0 ? g.enemies[0].id.ToString() : "empty"));
                if (room < 4) { Action(g, "shrine", "goto0"); Action(g, "guild", "deliver"); Check(g.save.stage == room, "Core delivered"); }
            }
            for (int frame=0; frame<200 && g.mode=="world"; frame++) g.Tick(.02f,new Point());
            Check(g.mode == "dialog" && !g.fallen.Exists(f=>f.actor.boss), "Final conversation follows the completed Boss death clock, including hit stop");
            int endingChoice = -1;
            for (int i = 0; i < g.conversation.choices.Length; i++) if (g.conversation.choices[i].action == "ending1") endingChoice = i;
            g.Choose(endingChoice); Check(g.save.ending == 1 && g.Has("completed"), "First ending"); g.Choose(0); Check(g.save.room == 0, "Postgame returns to town");
            // Complete the optional commissions after the campaign and verify the earned alternative.
            g.save.inventory[2] += 6; g.save.inventory[3] += 3; g.save.inventory[4] += 5;
            Action(g, "garden", "ally1"); Action(g, "fish", "ally2"); Action(g, "forge", "ally3");
            g.Enter(4); g.Talk(Object(g, "shrine")); endingChoice = -1;
            for (int i = 0; i < g.conversation.choices.Length; i++) if (g.conversation.choices[i].action == "ending3") endingChoice = i;
            Check(endingChoice >= 0, "Three allies unlock the alternative ending"); g.Choose(endingChoice); Check(g.save.ending == 3, "Third ending");
            g.Close(); g.Talk(Object(g, "shrine"));
            for (int i = 0; i < g.conversation.choices.Length; i++) if (g.conversation.choices[i].action == "ending2") { g.Choose(i); break; }
            Check(g.save.ending == 2, "Second ending");
        }


        public static void ExpandedHearthInnArchiveIsConnectedAndRewardsVerifiedExploration(Campaign data)
        {
            MapData inn=data.maps[9];
            Check(inn.rows.Length==40&&inn.rows[0].Length==60,"Expanded Hearth Inn preserves rooms while adding public archive wings");
            GameCore game=new GameCore(data);game.save.room=9;
            var queue=new System.Collections.Generic.Queue<int>();
            var visited=new System.Collections.Generic.HashSet<int>();
            int w=inn.rows[0].Length,h=inn.rows.Length;
            int start=(int)inn.spawnY*w+(int)inn.spawnX;
            queue.Enqueue(start);visited.Add(start);
            int[,] offsets={{1,0},{-1,0},{0,1},{0,-1}};
            while(queue.Count>0)
            {
                int cell=queue.Dequeue(),x=cell%w,y=cell/w;
                for(int d=0;d<4;d++)
                {
                    int xx=x+offsets[d,0],yy=y+offsets[d,1],next=yy*w+xx;
                    if(xx<0||xx>=w||yy<0||yy>=h||visited.Contains(next)||
                        !game.CanStand(new Point(xx+.5f,yy+.5f)))continue;
                    visited.Add(next);queue.Enqueue(next);
                }
            }
            WorldObject archive=Array.Find(inn.objects,o=>o.kind=="annexinnarchive");
            WorldObject letter=Array.Find(inn.objects,o=>o.kind=="annexinnmessage");
            Check(archive!=null&&letter!=null&&visited.Contains((int)archive.y*w+(int)archive.x)&&
                visited.Contains((int)letter.y*w+(int)letter.x),
                "Both new inn annex rooms connect to original spawn through real collision");
            game.save.x=archive.x;game.save.y=archive.y;
            game.Talk(archive);
            int idx=Array.FindIndex(game.conversation.choices,c=>c.action=="annex_archive_file");
            Check(idx>=0,"Inn archive can be filed with a real interactable action");
            int coins=game.save.coins;game.Choose(idx);
            Check(!game.Has("annex_archive_complete")&&game.save.coins==coins,
                "Incomplete outdoor discovery cannot falsely claim the published atlas");
            for(int room=0;room<8;room++)
            {
                game.save.flags.Add("annex_camp_annex_camp_"+room);
                game.save.flags.Add("annex_vista_annex_vista_"+room);
                game.save.flags.Add("annex_cache_annex_cache_"+room+"_a");
                game.save.flags.Add("annex_cache_annex_cache_"+room+"_b");
            }
            game.Talk(archive);
            idx=Array.FindIndex(game.conversation.choices,c=>c.action=="annex_archive_file");
            game.Choose(idx);
            Check(game.Has("annex_archive_complete")&&game.save.coins==coins+60&&
                game.GetQuest("annex_explorer").complete,
                "Physically filing the complete atlas rewards real resources and finishes the quest");
            SaveData save=game.save;
            GameCore loaded=new GameCore(data,save);
            Check(save.version==2&&loaded.Has("annex_archive_complete"),
                "Public atlas persists in old save slots");
            game.Talk(archive);
            idx=Array.FindIndex(game.conversation.choices,c=>c.action=="annex_archive_file");
            game.Choose(idx);
            Check(game.save.coins==save.coins,"Published atlas cannot be farmed by rereading");
        }

        public static void NewAnnexSitesAreRealWalkableAndPersistent(Campaign data)
        {
            for(int region=0;region<8;region++)
            {
                MapData m=data.maps[region];
                GameCore g=new GameCore(data);g.save.room=region;
                g.save.flags.Add("gate_"+region);
                Check(!g.InProtectedArena(new Point(65,9)), "Boss arena must remain at its original x-coordinate");
                int count=0;foreach(WorldObject o in m.objects)
                    if(o.kind=="annexcache"||o.kind=="annexcamp"||o.kind=="annexvista")
                    {count++;Check(g.CanStand(new Point(o.x,o.y)), "New site must occupy real walkable tile: "+o.id);}
                Check(count==4,"Two caches, one camp, one vista per outdoor map");
            }
            GameCore game=new GameCore(data);
            WorldObject cache=Array.Find(data.maps[0].objects,o=>o.id=="annex_cache_0_a");
            int coins=game.save.coins,cacheMaterial=game.save.inventory[0];
            Point outsideCache=StandableOutsideTalkReach(game,cache);
            game.save.x=outsideCache.x;game.save.y=outsideCache.y;
            game.Talk(cache);game.Close();
            Check(!game.Has("annex_cache_"+cache.id)&&game.save.coins==coins&&
                game.save.inventory[0]==cacheMaterial,
                "An out-of-reach direct cache call cannot claim supplies or resources");
            game.save.x=cache.x;game.save.y=cache.y;
            game.Talk(cache);
            Check(game.save.coins==coins+7&&game.Has("annex_cache_"+cache.id),
                "Looting a physically reached cache grants its one-time supplies");
            game.Close();game.Talk(cache);game.Close();
            Check(game.save.coins==coins+7,"Cache cannot duplicate real loot");
            WorldObject vista=Array.Find(data.maps[0].objects,o=>o.id=="annex_vista_0");
            game.save.x=vista.x;game.save.y=vista.y;game.Talk(vista);game.Close();
            Check(game.Has("annex_vista_"+vista.id)&&game.Explored(0,65,40),"Lookout records its nearby 6x6 fog cells");
            WorldObject camp=Array.Find(data.maps[0].objects,o=>o.id=="annex_camp_0");
            game.save.x=camp.x;game.save.y=camp.y;game.Talk(camp);
            int choice=Array.FindIndex(game.conversation.choices,c=>c.action=="annex_rest");
            Check(choice>=0,"Real camp offers rest interaction");
            int wood=game.save.inventory[0];
            game.save.health=30;game.save.energy=8;
            game.Choose(choice);
            Check(game.save.inventory[0]==wood-1&&game.save.health==70&&game.save.energy==100,
                "Camp consumes actual timber, restores HP and energy");
            GameCore reload=new GameCore(data,game.save);
            Check(reload.save.version==2&&reload.Has("annex_camp_"+camp.id)&&reload.Has("annex_cache_"+cache.id)&&reload.Has("annex_vista_"+vista.id),
                "All exploration progress remains in v2 save");
        }

        public static void EightCircuitRoutesUseActualWalkingOrderAndPersist(Campaign data)
        {
            for(int room=0;room<8;room++)
            {
                MapData m=data.maps[room];
                Check(m.rows.Length==80&&m.rows[0].Length==120,
                    "Sixth-ring terrain preserves earlier exploration sites");
                GameCore g=new GameCore(data);
                g.save.room=room;g.save.flags.Add("seal_"+room);
                int width=m.rows[0].Length,height=m.rows.Length;
                var visited=new System.Collections.Generic.HashSet<int>();
                var queue=new System.Collections.Generic.Queue<int>();
                int start=(int)m.spawnY*width+(int)m.spawnX;
                queue.Enqueue(start);visited.Add(start);
                int[,] dir={{1,0},{-1,0},{0,1},{0,-1}};
                while(queue.Count>0)
                {
                    int at=queue.Dequeue(),x=at%width,y=at/width;
                    for(int k=0;k<4;k++)
                    {
                        int xx=x+dir[k,0],yy=y+dir[k,1],key=yy*width+xx;
                        if(xx<0||yy<0||xx>=width||yy>=height||visited.Contains(key)||
                            !g.CanStand(new Point(xx+.5f,yy+.5f)))continue;
                        visited.Add(key);queue.Enqueue(key);
                    }
                }
                WorldObject[] sites=new WorldObject[3];
                for(int k=1;k<=3;k++)
                {
                    sites[k-1]=Array.Find(m.objects,o=>o.id=="trailcircuit_"+room+"_"+k);
                    Check(sites[k-1]!=null &&
                        visited.Contains((int)sites[k-1].y*width+(int)sites[k-1].x),
                        "Real collision and traversal reach every landmark in room "+room);
                }
                int startCoins=g.save.coins,startXp=g.save.xp;
                Point outsideCircuit=StandableOutsideTalkReach(g,sites[0]);
                g.save.x=outsideCircuit.x;g.save.y=outsideCircuit.y;
                g.Talk(sites[0]);g.Close();
                Check(g.TrailCircuitProgress(room)==0&&g.save.coins==startCoins&&
                    g.save.xp==startXp,
                    "A physically standable point outside E-key reach cannot submit a third-ring waypoint");
                g.save.x=sites[2].x;g.save.y=sites[2].y;
                g.Talk(sites[2]);g.Close();
                Check(g.TrailCircuitProgress(room)==0,"Third landmark cannot bypass first two");
                for(int k=0;k<3;k++)
                {
                    g.save.x=sites[k].x;g.save.y=sites[k].y;
                    g.Talk(sites[k]);
                    Check(g.conversation!=null&&g.conversation.text.Length>55&&
                        g.conversation.textEn.Length>55,
                        "Every visited physical waypoint provides real bilingual lore");
                    g.Close();
                    Check(g.TrailCircuitProgress(room)==k+1,"Each visited stop records real ordered progress");
                }
                Check(g.TrailCircuitComplete(room)&&g.save.coins==startCoins+24,
                    "Third distinct visit grants lasting local route completion");
                g.Talk(sites[2]);g.Close();
                Check(g.save.coins==startCoins+24,"Rereading completed circuit cannot farm rewards");
                GameCore restored=new GameCore(data,g.save);
                Check(restored.save.version==2&&restored.TrailCircuitComplete(room),
                    "Existing SaveData v2 reload preserves circuit milestones");
            }
            GameCore all=new GameCore(data);
            for(int r=0;r<8;r++)for(int k=1;k<=3;k++)
                all.save.flags.Add("trail_circuit_"+r+"_"+k);
            Check(all.TrailCircuitCount==8&&all.GetQuest("trail_circuits").complete,
                "Independent eight-area map quest reflects real stage flags");
        }

        public static void ThirdRingResourcePocketsUseRealRenewableHarvesting(Campaign data)
        {
            for(int room=0;room<8;room++)
            {
                GameCore region=new GameCore(data);
                region.save.room=room;
                int count=0;
                foreach(WorldObject o in data.maps[room].objects)
                    if(o.id.StartsWith("trail_forager_"+room+"_"))
                    {
                        count++;
                        Check(region.CanStand(new Point(o.x,o.y)),
                            "Every new gatherable site remains on actual playable terrain");
                    }
                Check(count==3,"Each of eight third-ring maps gets three actual field resources");
            }
            GameCore g=new GameCore(data);
            WorldObject node=Array.Find(data.maps[0].objects,o=>o.id=="trail_forager_0_0");
            int herbs=g.save.inventory[2];
            g.save.x=node.x;g.save.y=node.y;g.Talk(node);
            Check(g.Has("node_"+node.id)&&g.save.inventory[2]==herbs+3,
                "New outdoor node yields actual resource through existing gathering code");
            g.Talk(node);
            Check(g.save.inventory[2]==herbs+3,"Second gather cannot duplicate resource");
            g.save.flags.RemoveAll(flag=>flag.StartsWith("node_"));
            g.Talk(node);
            Check(g.save.inventory[2]==herbs+6,
                "Existing rest refresh makes the new site renewable without new save format");
        }

        public static void CachedHouseCollidersRemainCorrectAcrossMapSwitches(Campaign data)
        {
            GameCore g=new GameCore(data);
            int tested=0;
            for(int room=0;room<8;room++)
            {
                g.save.room=room;
                foreach(Decoration d in data.maps[room].decor)
                    if(d.kind=="house")
                    {
                        tested++;
                        Check(g.Blocked(d.x,d.y-.7f),
                            "Solid house footprint remains blocked after world-decoration caching");
                    }
                foreach(WorldObject o in data.maps[room].objects)
                    if(o.kind=="trailcircuit")
                        Check(g.CanStand(new Point(o.x,o.y)),
                            "Third-ring site remains walkable when switching collision maps");
            }
            Check(tested>=4,"World collision regression exercises existing authored houses");
            g.save.room=0;
            Check(g.Blocked(7,5.3f),"Returning to town restores its original solid house colliders");
        }

        public static void FourthRingRescueSignalsRequireRealSurveyAndParts(Campaign data)
        {
            for(int room=0;room<8;room++)
            {
                MapData m=data.maps[room];
                Check(m.rows.Length==80&&m.rows[0].Length==120,
                    "Expanded sixth-ring geometry retains existing rescue locations");
                GameCore g=new GameCore(data);
                g.save.room=room;
                g.save.inventory[4]=5;
                WorldObject[] sites=new WorldObject[3];
                for(int i=0;i<3;i++)
                {
                    int stage=i;
                    sites[i]=Array.Find(m.objects,o=>o.id=="outer_"+room+"_"+stage);
                    Check(sites[i]!=null&&g.CanStand(new Point(sites[i].x,sites[i].y)),
                        "Every fourth-ring rescue object stands on actual accessible terrain");
                }
                int width=m.rows[0].Length,height=m.rows.Length;
                var queue=new System.Collections.Generic.Queue<int>();
                var seen=new System.Collections.Generic.HashSet<int>();
                int spawn=(int)m.spawnY*width+(int)m.spawnX;
                queue.Enqueue(spawn);seen.Add(spawn);
                int[,] dirs={{1,0},{-1,0},{0,1},{0,-1}};
                while(queue.Count>0)
                {
                    int at=queue.Dequeue(),x=at%width,y=at/width;
                    for(int direction=0;direction<4;direction++)
                    {
                        int xx=x+dirs[direction,0],yy=y+dirs[direction,1];
                        if(xx<0||yy<0||xx>=width||yy>=height)continue;
                        int key=yy*width+xx;
                        if(seen.Contains(key)||!g.CanStand(new Point(xx+.5f,yy+.5f)))continue;
                        seen.Add(key);queue.Enqueue(key);
                    }
                }
                foreach(WorldObject site in sites)
                    Check(seen.Contains((int)site.y*width+(int)site.x),
                        "New signal route must be reachable from original spawn using real collision");
                int coins=g.save.coins,parts=g.save.inventory[4],xp=g.save.xp;
                Point outsideLog=StandableOutsideTalkReach(g,sites[0]);
                g.save.x=outsideLog.x;g.save.y=outsideLog.y;g.Talk(sites[0]);g.Close();
                Check(!g.OuterLogFound(room)&&g.save.xp==xp,
                    "A distant rescue log cannot award first-discovery XP or advance evidence");
                g.save.x=sites[2].x;g.save.y=sites[2].y;
                g.Talk(sites[2]);g.Close();
                Check(!g.OuterCacheRecovered(room)&&g.save.coins==coins,
                    "Relief cache cannot be taken before the signal is repaired");
                g.save.x=sites[1].x;g.save.y=sites[1].y;
                g.Talk(sites[1]);g.Close();
                Check(!g.OuterRelayRestored(room)&&g.save.inventory[4]==parts,
                    "Beacon cannot be repaired without the original field log");
                g.save.x=sites[0].x;g.save.y=sites[0].y;
                g.Talk(sites[0]);g.Close();
                Check(g.OuterLogFound(room),"Reading the southern evidence advances the real quest");
                g.save.x=sites[1].x;g.save.y=sites[1].y;
                g.Talk(sites[1]);
                int choice=Array.FindIndex(g.conversation.choices,c=>c.action=="outer_repair");
                Check(choice>=0,"Physical repair offers a real material-consuming choice");
                Point outsideRelay=StandableOutsideTalkReach(g,sites[1]);
                g.save.x=outsideRelay.x;g.save.y=outsideRelay.y;g.Choose(choice);g.Close();
                Check(!g.OuterRelayRestored(room)&&g.save.inventory[4]==parts,
                    "A stale relay-repair choice cannot spend a part after leaving physical reach");
                g.save.x=sites[1].x;g.save.y=sites[1].y;g.Talk(sites[1]);
                choice=Array.FindIndex(g.conversation.choices,c=>c.action=="outer_repair");
                Check(choice>=0,"Returning to the physical beacon restores the repair choice");
                g.Choose(choice);
                Check(g.OuterRelayRestored(room)&&g.save.inventory[4]==parts-1,
                    "One actual spare part is consumed when the physical beacon is repaired");
                g.Talk(sites[1]);g.Close();
                Check(g.save.inventory[4]==parts-1,"Replaying a repaired signal cannot double-charge");
                g.save.x=sites[2].x;g.save.y=sites[2].y;
                g.Talk(sites[2]);g.Close();
                Check(g.OuterCacheRecovered(room)&&g.save.coins==coins+18,
                    "Final physical relief chest pays the correct one-time reward");
                g.Talk(sites[2]);g.Close();
                Check(g.save.coins==coins+18,"Relief chest cannot pay twice");
                GameCore restored=new GameCore(data,g.save);
                Check(restored.save.version==2&&restored.OuterLogFound(room)&&
                    restored.OuterRelayRestored(room)&&restored.OuterCacheRecovered(room),
                    "All three rescue stages survive a legacy SaveData v2 reload");
            }
            GameCore all=new GameCore(data);
            for(int room=0;room<8;room++)all.save.flags.Add("outer_cache_"+room);
            Check(all.OuterRescueCount==8&&all.GetQuest("outer_rescue").complete,
                "The tracked field mission finishes only after eight genuine chests");
        }

        public static void SmoothTerrainAndPropsRemainContinuousAcrossChunkBorders(Campaign data)
        {
            Check(WorldStreaming.Smooth01(-1)==0&&WorldStreaming.Smooth01(2)==1,
                "Streaming fades clamp safely outside their range");
            Check(Math.Abs(WorldStreaming.Smooth01(.5f)-.5f)<.0001f,
                "Chunk readiness uses symmetric smoothstep");
            for(int i=0;i<100;i++)
            {
                float t=i/100f, v=WorldStreaming.Blend(.15f,.3f,.6f,.9f,t,.45f);
                float u=WorldStreaming.Blend(.15f,.3f,.6f,.9f,t+.0001f,.45f);
                Check(v>=0&&v<=1&&Math.Abs(u-v)<.001f,
                    "Chunk blending stays continuous without square block seams");
            }
            float old=-1;
            for(int px=-130;px<=130;px+=5)
            {
                float opacity=WorldStreaming.EdgeOpacity(px,360,1280,720);
                Check(opacity>=0&&opacity<=1&&opacity>=old,
                    "Scenery gradually fades in on approach without a viewport pop-in");
                old=opacity;
            }
            Check(WorldStreaming.EdgeOpacity(-150,360,1280,720)==0,
                "Distant scenery is completely transparent beyond its prewarm apron");
            Check(WorldStreaming.EdgeOpacity(640,360,1280,720)==1,
                "Center-screen scenery stays fully visible");
        }

        public static void OutdoorRescueWatchStartsRealCombatAndOnlyRewardsRealWins(Campaign data)
        {
            for(int room=0;room<8;room++)
            {
                MapData map=data.maps[room];
                WorldObject o=Array.Find(map.objects,x=>x.id=="outer_watch_"+room);
                GameCore g=new GameCore(data);g.save.room=room;
                Check(o!=null&&g.CanStand(new Point(o.x,o.y)),
                    "Every optional watchpost must be on a walkable physical tile");
                g.save.x=o.x;g.save.y=o.y;
                g.enemies.Clear();
                g.Talk(o);
                Check(g.conversation!=null&&
                    Array.FindIndex(g.conversation.choices,c=>c.action=="outer_watch_start")<0,
                    "Outpost cannot be started without first retrieving the relief supplies");
                g.Close();
                g.save.flags.Add("outer_cache_"+room);
                int coins=g.save.coins;
                g.Talk(o);
                int start=Array.FindIndex(g.conversation.choices,c=>c.action=="outer_watch_start");
                Check(start>=0,"Completed exploration unlocks the voluntary real combat choice");
                if(room==0)
                {
                    Point outsidePost=StandableOutsidePhysicalRadius(g,o,2.2f);
                    g.save.x=outsidePost.x;g.save.y=outsidePost.y;
                    g.Choose(start);g.Close();
                    Check(!g.OuterPatrolActive&&g.enemies.Count==0&&g.save.coins==coins,
                        "A stale outpost dialogue cannot launch a remote defense");
                    g.save.x=o.x;g.save.y=o.y;g.Talk(o);
                    start=Array.FindIndex(g.conversation.choices,c=>c.action=="outer_watch_start");
                    Check(start>=0,"Returning to the physical post restores the defense choice");
                }
                g.Choose(start);
                Check(g.OuterPatrolActive&&g.enemies.Count==2&&
                    !g.OuterPatrolDone(room),"Defense uses actual two spawned enemies, not a text-only battle");
                int id0=g.enemies[0].id,id1=g.enemies[1].id;
                Check(id0!=id1&&g.enemies[0].hp>0&&g.enemies[1].hp>0,
                    "Distinct attackers spawn with real combat health");
                g.Tick(.02f,new Point());
                Check(!g.OuterPatrolDone(room)&&g.save.coins==coins,
                    "Unbeaten enemies cannot award any defense completion");
                // Regression: forcibly removing actors from the list is NOT
                // damage, and must never counterfeit a successful clear.
                g.enemies.Clear();g.Tick(.02f,new Point());
                Check(!g.OuterPatrolDone(room)&&!g.OuterPatrolActive&&g.save.coins==coins,
                    "Enemy despawn must abort rather than award a counterfeit win");
                g.Talk(o);
                start=Array.FindIndex(g.conversation.choices,c=>c.action=="outer_watch_start");
                Check(start>=0,"Aborted encounter can be retried safely");
                g.Choose(start);
                Check(g.enemies.Count==2,"Retry spawns two live combat actors");
                Enemy attackerA=g.enemies[0],attackerB=g.enemies[1];
                var hit=typeof(GameCore).GetMethod("Hit",
                    System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
                Check(hit!=null,"Exercise real combat Hit transaction, not a simulated flag");
                hit.Invoke(g,new object[]{attackerA,10000f,true,null,null});
                // Hit-stop is part of actual combat and temporarily defers
                // quest ticks. Run normal real frames, not a magic giant dt.
                for(int frame=0;frame<10;frame++)g.Tick(.02f,new Point());
                Check(!g.OuterPatrolDone(room)&&g.OuterPatrolActive,
                    "Defeating one real attacker is not enough to clear the defense");
                hit.Invoke(g,new object[]{attackerB,10000f,true,null,null});
                for(int frame=0;frame<10;frame++)g.Tick(.02f,new Point());
                Check(g.OuterPatrolDone(room)&&g.save.coins==coins+14,
                    "Both true lethal Hit transactions award the single defense reward");
                // Recreate the persisted v2 state as a separate slot object.
                // The loader sanitizes its OWN save in place. Passing the
                // active g.save aliases the running hero and a locked-region
                // recovery can move that hero away from this watchpost.
                var persisted = new SaveData {
                    version=g.save.version, room=g.save.room, stage=g.save.stage,
                    x=g.save.x, y=g.save.y, coins=g.save.coins,
                    flags=new List<string>(g.save.flags)
                };
                GameCore reload=new GameCore(data,persisted);
                Check(reload.save.version==2&&reload.OuterPatrolDone(room),
                    "Defended watchposts persist with original v2 flags");
                g.Talk(o);
                Check(Array.FindIndex(g.conversation.choices,c=>c.action=="outer_watch_start")<0,
                    "Completed post remains readable, never starts a duplicating fight");
                g.Close();
                Check(g.save.coins==coins+14,"Repeating a defended post cannot farm rewards");
            }
        }

        public static void CompanionPartySwapCannotInheritAnotherAllysDefenseOrder(Campaign data)
        {
            WorldObject post=Array.Find(data.maps[0].objects,
                o=>o.kind=="outerwatch");
            GameCore g=new GameCore(data);g.mode="world";
            g.save.room=0;g.enemies.Clear();g.projectiles.Clear();
            g.save.flags.Add("outer_cache_0");
            g.save.flags.Add("companion_1");
            g.save.flags.Add("companion_2");
            g.save.companion=1;
            g.save.x=post.x;g.save.y=post.y;
            g.companionPosition=new Point(post.x+1.1f,post.y+1.1f);
            g.Talk(post);
            int start=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_start");
            Check(start>=0,"Real guard trial can start at the physical cache");
            g.Choose(start);
            g.Talk(post);
            int guard=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_guard");
            Check(guard>=0,"The live siege exposes the original guard order");
            g.Choose(guard);
            Check(g.OuterPatrolCompanionGuarding,
                "Original Ivo guard foot remains active before party switch");
            Point postFoot=g.OuterPatrolCompanionGuardPosition;
            g.SelectCompanion(1);
            Check(g.OuterPatrolCompanionGuarding&&
                (g.OuterPatrolCompanionGuardPosition-postFoot).Length<.001f,
                "Repeating same ally selection cannot cancel a valid order");
            Point safe=new Point();bool found=false;
            for(int dx=-11;dx<=11&&!found;dx++)
            for(int dy=-11;dy<=11&&!found;dy++)
            {
                Point p=new Point(post.x+dx,post.y+dy);
                if(!g.CanStand(p)||
                    (p-new Point(post.x,post.y)).Length>11f)continue;
                g.save.x=p.x;g.save.y=p.y;
                if(g.SafeTravel){safe=p;found=true;}
            }
            Check(found,"Real cache map includes a legal safe party-swap foot");
            g.save.x=safe.x;g.save.y=safe.y;
            g.SelectCompanion(2);
            Check(g.save.companion==2&&g.OuterPatrolActive&&
                !g.OuterPatrolCompanionGuarding&&
                !g.OuterPatrolMedicRallying&&g.save.version==2,
                "New Sen party member cannot inherit Ivo's planted stock order");
            var stored=typeof(GameCore).GetField("outerPatrolCompanionGuardFoot",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            Check(stored!=null&&
                ((Point)stored.GetValue(g)).Length<.001f,
                "Retired tactical waypoint is cleared, not orphaned in the world");
            // The new actor joined the party at the player's actual
            // distant safe foot. Returning the hero to the post does NOT
            // teleport this other actor. Give the real world simulation time
            // to walk the companion toward the watch before assigning a
            // NEW physically reachable defensive position.
            g.save.x=post.x;g.save.y=post.y;
            g.invulnerable=100f;
            foreach(Enemy foe in g.enemies)foe.stun=12f;
            for(int frame=0;frame<520&&g.OuterPatrolActive&&
                (g.companionPosition-new Point(post.x,post.y)).Length>3.5f;frame++)
                g.Tick(.02f,new Point());
            Check(g.OuterPatrolActive&&g.CanStand(g.companionPosition)&&
                (g.companionPosition-new Point(post.x,post.y)).Length<4f,
                "New companion physically rejoins the genuine post after a party swap");
            g.Talk(post);
            guard=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_guard");
            Check(guard>=0,"New companion may receive a real fresh watch order");
            g.Choose(guard);
            Check(g.OuterPatrolCompanionGuarding&&
                g.CanStand(g.OuterPatrolCompanionGuardPosition),
                "New party member can actually be stationed again from post");
        }

        public static void RealStationedCompanionHasTwoFinitePhysicalStockGuards(Campaign data)
        {
            WorldObject post=Array.Find(data.maps[0].objects,
                o=>o.kind=="outerwatch");
            GameCore g=new GameCore(data);g.mode="world";
            g.save.room=0;g.enemies.Clear();g.projectiles.Clear();
            g.save.flags.Add("outer_cache_0");
            g.save.flags.Add("outer_patrol_done_0");
            g.save.companion=1;
            g.save.x=post.x;g.save.y=post.y;
            g.companionPosition=new Point(post.x+1.1f,post.y+1.1f);
            g.Talk(post);
            int start=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_veteran");
            Check(start>=0,"The physical cache offers its real veteran siege");
            g.Choose(start);
            g.Talk(post);
            int guard=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_guard");
            Check(guard>=0,"The real fighting party can plant a cache guard");
            g.Choose(guard);
            Check(g.OuterPatrolCompanionGuarding&&
                g.OuterPatrolGuardBlocksLeft==2&&g.OuterPatrolFlawlessEligible,
                "New siege has two finite, unused guard contacts");
            Point foot=g.OuterPatrolCompanionGuardPosition;
            Check(g.CanStand(foot),
                "Original guard target is a physically valid tile");
            // The other companion movement tests exercise SweepActor. This
            // focused contact test places its actor at that already-verified
            // legal endpoint to drive 3 real enemy contact releases.
            g.companionPosition=foot;
            Enemy melee=Array.Find(g.enemies.ToArray(),e=>e.kind==0);
            Check(melee!=null,"Ordinary melee attacker really exists");
            bool positioned=false;
            for(float dx=-2f;dx<=2f&&!positioned;dx+=.5f)
            for(float dy=-2f;dy<=2f&&!positioned;dy+=.5f)
            {
                Point candidate=new Point(post.x+dx,post.y+dy);
                if(!g.CanStand(candidate)||
                    (candidate-new Point(post.x,post.y)).Length>2.2f||
                    (candidate-foot).Length>2f||
                    (candidate-foot).Length<.8f)
                    continue;
                melee.x=candidate.x;melee.y=candidate.y;
                positioned=true;
            }
            Check(positioned,"Actual cache has a physical melee/guard contact foot");
            Point observer=new Point();bool found=false;
            for(int dx=-10;dx<=10&&!found;dx++)
            for(int dy=-10;dy<=10&&!found;dy++)
            {
                Point p=new Point(post.x+dx,post.y+dy);
                if(!g.CanStand(p)||
                    (p-new Point(post.x,post.y)).Length>11f||
                    (p-melee.Position).Length<4.5f)continue;
                observer=p;found=true;
            }
            Check(found,"Defender can physically observe from out of melee");
            g.save.x=observer.x;g.save.y=observer.y;
            var release=typeof(GameCore).GetMethod("DamageOuterPatrolStock",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            var able=typeof(GameCore).GetMethod("OuterPatrolCompanionCanIntercept",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            Check(release!=null&&able!=null,"Actual production stock hit path exists");
            melee.targetingOuterPatrolStock=true;
            if(!(bool)able.Invoke(g,new object[]{melee}))
            {
                // An authored post may have a narrow gate: search a legal
                // raider contact foot only through truly connected paths.
                var clear=typeof(GameCore).GetMethod("ClearAttackPath",
                    System.Reflection.BindingFlags.NonPublic|
                    System.Reflection.BindingFlags.Instance);
                Check(clear!=null,"Use original physical melee LOS");
                positioned=false;
                for(float dx=-2f;dx<=2f&&!positioned;dx+=.25f)
                for(float dy=-2f;dy<=2f&&!positioned;dy+=.25f)
                {
                    Point p=new Point(post.x+dx,post.y+dy);
                    if(!g.CanStand(p)||
                        (p-new Point(post.x,post.y)).Length>2.2f||
                        (p-foot).Length>2.2f||
                        !(bool)clear.Invoke(g,new object[]{p,foot})||
                        !(bool)clear.Invoke(g,new object[]{
                            p,new Point(post.x,post.y)}))continue;
                    melee.x=p.x;melee.y=p.y;positioned=true;
                }
                Check(positioned,"Valid full melee strike exists on authored cache terrain");
            }
            Check((bool)able.Invoke(g,new object[]{melee})&&
                g.OuterPatrolGuardReady,
                "Living posted ally can physically reach a real melee stock strike");
            var exposed=typeof(GameCore).GetMethod(
                "CompanionCacheContactImminent",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            Check(exposed!=null,"Test the real auto-fire emergency priority path");
            melee.state=1;
            Check(!(bool)exposed.Invoke(g,new object[]{melee}),
                "Already blocked stock threat cannot steal urgent ally target");
            float stock=g.OuterPatrolIntegrity;
            release.Invoke(g,new object[]{melee});
            Check(g.OuterPatrolGuardBlocksLeft==1&&g.OuterPatrolIntegrity==stock&&
                !g.OuterPatrolFlawlessEligible,
                "First real intercepted attack consumes finite ally guard and disqualifies no-aid medal");
            release.Invoke(g,new object[]{melee});
            Check(g.OuterPatrolGuardBlocksLeft==0&&g.OuterPatrolIntegrity==stock,
                "Second intercepted stock strike consumes final finite contact");
            Check(!g.OuterPatrolGuardReady&&
                (bool)exposed.Invoke(g,new object[]{melee}),
                "Depleted guard exposes real incoming cache hit to auto-fire priority");
            release.Invoke(g,new object[]{melee});
            Check(g.OuterPatrolGuardBlocksLeft==0&&g.OuterPatrolIntegrity<stock,
                "Third genuine melee release reaches stock without free bodyguard");
            g.save.x=post.x;g.save.y=post.y;
            g.Talk(post);
            int abort=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_abort");
            g.Choose(abort);
            Check(!g.OuterPatrolActive&&g.OuterPatrolGuardBlocksLeft==0&&
                g.save.version==2,"Withdrawal clears unsaved limited guard state");
        }

        public static void GuardMedicPhysicallyRalliesToCriticalHeroAndReturnsToStock(Campaign data)
        {
            WorldObject post=Array.Find(data.maps[0].objects,
                o=>o.kind=="outerwatch");
            GameCore g=new GameCore(data);g.mode="world";
            g.save.room=0;g.enemies.Clear();g.projectiles.Clear();
            g.save.flags.Add("outer_cache_0");
            g.save.companion=0;
            g.save.x=post.x;g.save.y=post.y;
            g.companionPosition=new Point(post.x+1.1f,post.y+1.1f);
            g.Talk(post);
            int start=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_start");
            Check(start>=0,"Medical guard uses a real watchpost defense");
            g.Choose(start);
            foreach(Enemy foe in g.enemies)foe.stun=20f;
            g.Talk(post);
            int guard=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_guard");
            Check(guard>=0,"Mira can be physically assigned to watch stock");
            g.Choose(guard);
            Check(g.OuterPatrolCompanionGuarding,
                "Medical support guard is a valid active tactical order");
            Point foot=g.OuterPatrolCompanionGuardPosition;
            Point remote=new Point();bool found=false;
            for(int dx=-9;dx<=9&&!found;dx++)
            for(int dy=-9;dy<=9&&!found;dy++)
            {
                Point p=new Point(post.x+dx,post.y+dy);
                if(!g.CanStand(p)||
                    (p-foot).Length<7f||
                    (p-new Point(post.x,post.y)).Length>10f)
                    continue;
                remote=p;found=true;
            }
            Check(found,"Real regional defense ring has a separate walkable rescue point");
            g.save.x=remote.x;g.save.y=remote.y;
            g.save.health=g.save.maxHealth*.25f;
            var helper=typeof(GameCore).GetMethod("CompanionOutpostGuardTarget",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            Check(helper!=null,"Actual escort goal is selected by live game logic");
            Point medicTarget=(Point)helper.Invoke(g,null);
            Check((medicTarget-foot).Length>1f&&
                g.OuterPatrolCompanionGuarding&&g.OuterPatrolMedicRallying,
                "Critically injured remote hero draws Mira from her watch foot");
            float before=(g.companionPosition-medicTarget).Length;
            for(int i=0;i<90&&g.OuterPatrolActive;i++)
                g.Tick(.02f,new Point());
            Check(g.OuterPatrolCompanionGuarding&&
                (g.companionPosition-medicTarget).Length<before-.1f,
                "Medic walks toward physical rescue point, never heals globally");
            g.save.health=g.save.maxHealth;
            Point returnFoot=(Point)helper.Invoke(g,null);
            Check((returnFoot-foot).Length<.01f&&
                g.OuterPatrolCompanionGuarding&&!g.OuterPatrolMedicRallying,
                "After real recovery medic resumes original protected post");
            float gap=(g.companionPosition-foot).Length;
            for(int tick=0;tick<60&&g.OuterPatrolActive;tick++)
                g.Tick(.02f,new Point());
            Check(g.OuterPatrolCompanionGuarding&&
                (g.companionPosition-foot).Length<=gap+.02f,
                "Recovery truly resumes walking to the ORIGINAL guard foot");
            g.save.x=post.x;g.save.y=post.y;
            g.Talk(post);
            int abort=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_abort");
            g.Choose(abort);
            Check(!g.OuterPatrolCompanionGuarding&&
                !g.OuterPatrolActive,
                "Withdrawal removes emergency medic and guard states");
        }

        public static void GuardChoosesRealClearMuzzleCoverageAtEveryRegionalCache(Campaign data)
        {
            var select=typeof(GameCore).GetMethod(
                "TryOuterPatrolCompanionGuardFoot",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            var score=typeof(GameCore).GetMethod(
                "OuterPatrolGuardFiringLanes",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            Check(select!=null&&score!=null,
                "Test real guard placement and actual physically swept muzzle");
            for(int room=0;room<8;room++)
            {
                WorldObject post=Array.Find(data.maps[room].objects,
                    o=>o.kind=="outerwatch");
                Check(post!=null,"Map has a real rescue watch in room "+room);
                GameCore g=new GameCore(data);g.mode="world";
                g.save.room=room;g.enemies.Clear();
                g.save.x=post.x;g.save.y=post.y;
                g.save.companion=1;
                g.companionPosition=new Point(post.x,post.y);
                g.save.flags.Add("outer_cache_"+room);
                g.Talk(post);
                int start=Array.FindIndex(g.conversation.choices,
                    c=>c.action=="outer_watch_start");
                Check(start>=0,"Regional raid can start with a true supply cache");
                g.Choose(start);
                Check(g.OuterPatrolActive&&g.enemies.Count==2,
                    "Physical guard coverage test uses real enemy positions");
                Point[] original={
                    new Point(0f,2.5f),new Point(-2.5f,0f),
                    new Point(2.5f,0f),new Point(0f,-2.5f),
                    new Point(-1.7f,1.7f),new Point(1.7f,1.7f),
                    new Point(-1.7f,-1.7f),new Point(1.7f,-1.7f)
                };
                var reachable=typeof(GameCore).GetMethod(
                    "ActorHasLocalRoute",
                    System.Reflection.BindingFlags.NonPublic|
                    System.Reflection.BindingFlags.Instance);
                int maximum=-1;
                foreach(Point offset in original)
                {
                    Point candidate=new Point(post.x+offset.x,post.y+offset.y);
                    if(!g.CanStand(candidate)||
                        (candidate-g.Position).Length<1.15f||
                        g.enemies.Exists(e=>e.hp>0&&
                            (e.Position-candidate).Length<1.2f)||
                        !(bool)reachable.Invoke(g,new object[]{
                            g.companionPosition,candidate,10}))continue;
                    int value=(int)score.Invoke(g,new object[]{candidate});
                    if(value>maximum)maximum=value;
                }
                Check(maximum>=0,
                    "There is a legal reachable guard foot in region "+room);
                object[] args={null};
                bool chosen=(bool)select.Invoke(g,args);
                Check(chosen,"Guard placement finds a real reachable foot");
                Point landing=(Point)args[0];
                Check(g.CanStand(landing)&&
                    (int)score.Invoke(g,new object[]{landing})==maximum,
                    "Guard aims from maximum genuine projectile coverage, region "+room);
            }
        }

        public static void GuardRangerMovesFromWalkableBlindFootToPhysicalFiringLane(Campaign data)
        {
            WorldObject post=Array.Find(data.maps[0].objects,
                o=>o.kind=="outerwatch");
            GameCore g=new GameCore(data);g.mode="world";
            g.save.room=0;g.enemies.Clear();g.projectiles.Clear();
            g.save.flags.Add("outer_cache_0");
            g.save.companion=1;
            g.save.x=post.x;g.save.y=post.y;
            g.companionPosition=new Point(post.x+1.1f,post.y+1.1f);
            g.Talk(post);
            int start=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_start");
            Check(start>=0,"Actual watch encounter is interactable");
            g.Choose(start);
            g.Talk(post);
            int guard=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_guard");
            g.Choose(guard);
            Check(g.OuterPatrolCompanionGuarding,
                "Real ranged guard order was accepted");
            var score=typeof(GameCore).GetMethod("OuterPatrolGuardFiringLanes",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            var route=typeof(GameCore).GetMethod("ActorHasLocalRoute",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            var target=typeof(GameCore).GetMethod("CompanionOutpostGuardTarget",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            var foot=typeof(GameCore).GetField("outerPatrolCompanionGuardFoot",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            var clock=typeof(GameCore).GetField("outerPatrolGuardRouteCheck",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            Check(score!=null&&route!=null&&target!=null&&foot!=null&&clock!=null,
                "Exercise the original shooting and local path APIs");
            int best=(int)score.Invoke(g,new object[]{g.OuterPatrolCompanionGuardPosition});
            Check(best>0,"Current real post guard can fire on initial raiders");
            Point blind=new Point();bool found=false;
            for(int dx=-10;dx<=10&&!found;dx++)
            for(int dy=-10;dy<=10&&!found;dy++)
            {
                Point probe=new Point(post.x+dx,post.y+dy);
                if(!g.CanStand(probe)||
                   (probe-g.OuterPatrolCompanionGuardPosition).Length<3f||
                   (int)score.Invoke(g,new object[]{probe})>0||
                   !(bool)route.Invoke(g,new object[]{
                       probe,g.OuterPatrolCompanionGuardPosition,10}))continue;
                blind=probe;found=true;
            }
            Check(found,"Real map contains a walkable but blind defender tile");
            g.companionPosition=blind;
            foot.SetValue(g,blind);
            clock.SetValue(g,0f);
            Point before=g.companionPosition;
            Point better=(Point)target.Invoke(g,null);
            Check(g.OuterPatrolCompanionGuarding&&
                (better-blind).Length>.5f&&
                (int)score.Invoke(g,new object[]{better})>0&&
                (g.companionPosition-before).Length<.001f,
                "Blind but walkable guard relocates target to real firing lane without warping");
            Check(g.save.version==2&&g.projectiles.Count==0,
                "Firing-lane discovery never spawns fake arrows or changes old saves");
        }

        public static void GuardReplansBlockedWorldRouteWithoutWarpingOrOrphanOrders(Campaign data)
        {
            WorldObject post=Array.Find(data.maps[0].objects,
                o=>o.kind=="outerwatch");
            GameCore g=new GameCore(data);g.mode="world";
            g.save.room=0;g.enemies.Clear();g.projectiles.Clear();
            g.save.flags.Add("outer_cache_0");g.save.companion=1;
            g.save.x=post.x;g.save.y=post.y;
            g.companionPosition=new Point(post.x+1.1f,post.y+1.1f);
            g.Talk(post);
            int start=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_start");
            g.Choose(start);
            g.Talk(post);
            int guard=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_guard");
            g.Choose(guard);
            Check(g.OuterPatrolCompanionGuarding,
                "Real ranged guard is initially stationed");
            var field=typeof(GameCore).GetField("outerPatrolCompanionGuardFoot",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            var clock=typeof(GameCore).GetField("outerPatrolGuardRouteCheck",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            var target=typeof(GameCore).GetMethod("CompanionOutpostGuardTarget",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            var route=typeof(GameCore).GetMethod("ActorHasLocalRoute",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            Check(field!=null&&clock!=null&&target!=null&&route!=null,
                "Test live connectedness, not a fake route flag");
            Point stranded=new Point();bool found=false;
            for(int radius=12;radius<=20&&!found;radius++)
            for(int dx=-radius;dx<=radius&&!found;dx++)
            for(int dy=-radius;dy<=radius&&!found;dy++)
            {
                Point p=new Point(post.x+dx,post.y+dy);
                if(!g.CanStand(p)||
                    (p-g.companionPosition).Length<11f||
                    (bool)route.Invoke(g,new object[]{
                        g.companionPosition,p,10}))continue;
                stranded=p;found=true;
            }
            Check(found,"Real world has walkable but locally disconnected foot");
            Point actorBefore=g.companionPosition;
            field.SetValue(g,stranded);clock.SetValue(g,0f);
            Point updated=(Point)target.Invoke(g,null);
            Check(g.OuterPatrolCompanionGuarding&&
                (updated-stranded).Length>1f&&g.CanStand(updated)&&
                (bool)route.Invoke(g,new object[]{
                    g.companionPosition,updated,10})&&
                (g.companionPosition-actorBefore).Length<.001f,
                "Closed route replans to reachable guard position without teleporting actor");
            Check((g.OuterPatrolCompanionGuardPosition-updated).Length<.01f,
                "World guard marker reads recovered actual reachable destination");
            // A tide-covered or sealed ORIGINAL waypoint must not
            // prematurely abandon a useful guard when another REAL
            // connected shooting foot still exists in the same region.
            field.SetValue(g,new Point(-20f,-20f));
            Point relocated=(Point)target.Invoke(g,null);
            Check(g.OuterPatrolCompanionGuarding&&
                g.CanStand(relocated)&&
                (relocated-new Point(-20f,-20f)).Length>1f&&
                (g.companionPosition-actorBefore).Length<.001f,
                "Blocked stock foot chooses genuine alternate without warping");
            // If even the companion itself is buried in invalid terrain,
            // there is no legal local source from which to certify a route.
            // Retire the command rather than keeping a phantom guard.
            field.SetValue(g,new Point(-20f,-20f));
            g.companionPosition=new Point(-20f,-20f);
            target.Invoke(g,null);
            Check(!g.OuterPatrolCompanionGuarding,
                "No physically reachable guard position safely resumes following");
        }

        public static void RealCompanionGuardsSuppliesWithoutWarpingOrPersisting(Campaign data)
        {
            WorldObject post=Array.Find(data.maps[0].objects,
                o=>o.kind=="outerwatch");
            Check(post!=null,"Regional relief post exists on physical map");
            GameCore g=new GameCore(data);g.mode="world";
            g.save.room=0;g.enemies.Clear();g.projectiles.Clear();
            g.save.x=post.x;g.save.y=post.y;
            g.save.flags.Add("outer_cache_0");
            g.save.companion=1;
            g.companionPosition=new Point(post.x+1.1f,post.y+1.1f);
            Check(g.CanStand(g.companionPosition),
                "Guard begins at a genuine unblocked companion foot");
            g.Talk(post);
            int start=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_start");
            Check(start>=0,"Watchpost has a genuine active defense");
            g.Choose(start);
            g.Talk(post);
            int guard=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_guard");
            Check(guard>=0,"The physical watchpost offers a live companion guard order");
            Point before=g.companionPosition;
            g.Choose(guard);
            Check(g.OuterPatrolCompanionGuarding&&
                g.CanStand(g.OuterPatrolCompanionGuardPosition)&&
                (g.companionPosition-before).Length<.001f,
                "Guard command picks legal foot but never teleports ally");
            Point destination=g.OuterPatrolCompanionGuardPosition;
            // The player can intercept the second raider without dragging
            // the ranged guard away from the vulnerable stock.
            g.save.x=post.x+5f;g.save.y=post.y;
            float distBefore=(g.companionPosition-destination).Length;
            for(int tick=0;tick<90&&g.OuterPatrolActive;tick++)
                g.Tick(.02f,new Point());
            Check(g.OuterPatrolCompanionGuarding&&
                (g.companionPosition-destination).Length<distBefore+.05f&&
                (g.companionPosition-new Point(post.x,post.y)).Length<5f,
                "Guard follows a physical local stock waypoint, not the distant hero");
            g.save.x=post.x;g.save.y=post.y;
            g.Talk(post);
            guard=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_guard");
            Check(guard>=0,"Follow option exists while guard is active");
            g.Choose(guard);
            Check(!g.OuterPatrolCompanionGuarding,
                "Player can restore original ordinary companion follow behavior");
            // Independent guard order can be given again in the same siege,
            // but it must disappear on withdrawal and save round-trip.
            g.Talk(post);
            guard=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_guard");
            g.Choose(guard);
            Check(g.OuterPatrolCompanionGuarding,
                "Guard can be reassigned by an actual valid local order");
            g.Talk(post);
            int abort=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_abort");
            g.Choose(abort);
            Check(!g.OuterPatrolActive&&!g.OuterPatrolCompanionGuarding,
                "Leaving a siege retires ephemeral guard orders");
            SaveData slot=new SaveData {
                version=2,room=0,x=post.x,y=post.y,
                flags=new List<string>(g.save.flags)
            };
            GameCore reload=new GameCore(data,slot);
            Check(!reload.OuterPatrolCompanionGuarding&&
                reload.save.version==2,
                "v2 slots never persist a transient tactical guard command");
        }

        // Older tests asserted "stock windup" on spawned approach actors
        // several meters outside actual melee reach, while the hero was
        // standing in the way. These helpers instead select an AUTHENTIC
        // standable, unobstructed contact plus a real bow muzzle lane.
        private static Point PlaceShootableCacheContact(GameCore g,
            WorldObject post,Enemy attacker,Enemy avoid)
        {
            var flags=System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance;
            var clear=typeof(GameCore).GetMethod("ClearAttackPath",flags);
            var shot=typeof(GameCore).GetMethod("WeaponShot",flags);
            Check(clear!=null&&shot!=null,
                "Use existing combat path and projectile muzzle geometry");
            Point cache=new Point(post.x,post.y);
            for(float dx=-2f;dx<=2f;dx+=.25f)
            for(float dy=-2f;dy<=2f;dy+=.25f)
            {
                Point p=cache+new Point(dx,dy);
                Point aim=p-g.companionPosition;
                if(!g.CanStand(p)||(p-cache).Length>2.2f||
                    aim.Length<1.4f||aim.Length>=6.5f||
                    avoid!=null&&(p-avoid.Position).Length<1.15f||
                    !(bool)clear.Invoke(g,new object[]{p,cache})||
                    !(bool)clear.Invoke(g,new object[]{
                        g.companionPosition,p}))
                    continue;
                Projectile probe=(Projectile)shot.Invoke(g,new object[]{
                    g.companionPosition,1,aim.Normalized,9f,0f,(Point?)p});
                if(probe.blockedAtSpawn)continue;
                attacker.x=p.x;attacker.y=p.y;
                return p;
            }
            throw new Exception(
                "No genuine walkable stock contact with a clear original ally bow muzzle");
        }
        private static void MoveToRealUncoveredStockObserver(GameCore g,
            WorldObject post,Enemy one,Enemy two)
        {
            Point cache=new Point(post.x,post.y);
            for(int dx=-10;dx<=10;dx++)
            for(int dy=-10;dy<=10;dy++)
            {
                Point p=cache+new Point(dx,dy);
                if(!g.CanStand(p)||(p-cache).Length>11f||
                    (p-one.Position).Length<3.5f||
                    two!=null&&(p-two.Position).Length<3.5f)
                    continue;
                g.save.x=p.x;g.save.y=p.y;
                return;
            }
            throw new Exception("No real physical safe observer for outpost warning");
        }

        public static void CompanionInterceptsLiveHeavyWindupBeforeIdleCacheRaider(Campaign data)
        {
            WorldObject post=Array.Find(data.maps[0].objects,
                o=>o.kind=="outerwatch");
            GameCore g=new GameCore(data);g.mode="world";
            g.save.room=0;g.enemies.Clear();g.projectiles.Clear();
            g.save.x=post.x;g.save.y=post.y;
            g.save.companion=1;
            g.companionPosition=new Point(post.x,post.y);
            g.save.flags.Add("outer_cache_0");
            g.save.flags.Add("outer_patrol_done_0");
            g.Talk(post);
            int start=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_veteran");
            Check(start>=0,"An actual veteran three-raider cache siege is available");
            g.Choose(start);
            Check(g.OuterPatrolVeteranActive&&g.enemies.Count==3,
                "Threat priority exercises three authentic enemy bodies");
            Enemy melee=Array.Find(g.enemies.ToArray(),e=>e.kind==0);
            Enemy heavy=Array.Find(g.enemies.ToArray(),e=>e.kind==2);
            Check(melee!=null&&heavy!=null,
                "Live cache contains both melee raider and heavy slammer");
            var select=typeof(GameCore).GetMethod("NearestCompanionThreat",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            Check(select!=null,"Exercise actual bow ally target-selection code");
            Enemy first=(Enemy)select.Invoke(g,null);
            Check(first==melee||first==heavy,
                "Real sabotage-capable actor outranks wandering ranged raider");
            // Prove both actual muzzle lanes are legal before asserting any
            // priority; a blocked shot must never beat an available one.
            var shot=typeof(GameCore).GetMethod("WeaponShot",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            Check(shot!=null,"Real ally projectile sweep is available");
            Point vectorM=(melee.Position-g.companionPosition).Normalized;
            Point vectorH=(heavy.Position-g.companionPosition).Normalized;
            Projectile probeM=(Projectile)shot.Invoke(g,new object[]{
                g.companionPosition,1,vectorM,9f,0f,(Point?)melee.Position});
            Projectile probeH=(Projectile)shot.Invoke(g,new object[]{
                g.companionPosition,1,vectorH,9f,0f,(Point?)heavy.Position});
            Check(!probeM.blockedAtSpawn&&!probeH.blockedAtSpawn,
                "Both actual enemy targets have a valid muzzle lane");
            PlaceShootableCacheContact(g,post,heavy,melee);
            MoveToRealUncoveredStockObserver(g,post,heavy,melee);
            heavy.state=1;heavy.targetingOuterPatrolStock=true;
            Check(g.OuterPatrolIncomingStockThreat==heavy,
                "Heavy actually threatens uncovered stock from its physical contact range");
            Check((Enemy)select.Invoke(g,null)==heavy,
                "Imminent heavy hit wins ally's target over idle melee attacker");
            heavy.targetingOuterPatrolStock=false;
            PlaceShootableCacheContact(g,post,melee,heavy);
            MoveToRealUncoveredStockObserver(g,post,melee,heavy);
            melee.state=1;melee.targetingOuterPatrolStock=true;
            Check(g.OuterPatrolIncomingStockThreat==melee,
                "The ordinary melee attacker also has a real uncovered strike lane");
            Check((Enemy)select.Invoke(g,null)==melee,
                "Threat priority follows the currently committed raider");
            g.enemies.Remove(heavy);
            Check((Enemy)select.Invoke(g,null)==melee,
                "Another registered attacker vanishing never creates a phantom target");
        }

        public static void OutpostWarningsTrackActualCommittedRaiderNotPhantomThreat(Campaign data)
        {
            WorldObject post=Array.Find(data.maps[0].objects,
                o=>o.kind=="outerwatch");
            GameCore g=new GameCore(data);g.mode="world";
            g.save.room=0;g.enemies.Clear();g.projectiles.Clear();
            g.save.flags.Add("outer_cache_0");
            g.save.flags.Add("outer_patrol_done_0");
            g.save.x=post.x;g.save.y=post.y;
            g.Talk(post);
            int start=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_veteran");
            g.Choose(start);
            Enemy melee=Array.Find(g.enemies.ToArray(),e=>e.kind==0);
            Enemy heavy=Array.Find(g.enemies.ToArray(),e=>e.kind==2);
            Check(melee!=null&&heavy!=null&&
                g.OuterPatrolIncomingStockThreat==null,
                "Idle enemies never create counterfeit supply warnings");
            // During a real defense the hero can intercept in melee range.
            // Observe genuine stock threats from a legal but distant post
            // foot, then move inside the raider's actual attack lane to
            // verify the same warning retracts BEFORE genuine release.
            Point observer=new Point();bool foundObserver=false;
            for(int dx=-10;dx<=10&&!foundObserver;dx++)
            for(int dy=-10;dy<=10&&!foundObserver;dy++)
            {
                Point p=new Point(post.x+dx,post.y+dy);
                if(!g.CanStand(p)||
                    (p-new Point(post.x,post.y)).Length>11f||
                    (p-melee.Position).Length<3.5f||
                    (p-heavy.Position).Length<3.5f)continue;
                observer=p;foundObserver=true;
            }
            Check(foundObserver,
                "Actual watch map provides a legal defender observation foot");
            g.save.x=observer.x;g.save.y=observer.y;
            melee.state=1;melee.timer=.8f;
            melee.targetingOuterPatrolStock=true;
            Check(g.OuterPatrolIncomingStockThreat==melee&&
                Math.Abs(g.OuterPatrolIncomingStockSeconds-.8f)<.01f&&
                !g.OuterPatrolIncomingStockHeavy,
                "Active real melee stock windup produces grounded timing");
            g.save.x=melee.x;g.save.y=melee.y;
            Check(g.OuterPatrolIncomingStockThreat==null&&
                g.OuterPatrolIncomingStockSeconds<0f,
                "Hero physically entering an already aimed swing cancels false stock-hit alarm");
            g.save.x=observer.x;g.save.y=observer.y;
            Check(g.OuterPatrolIncomingStockThreat==melee,
                "Warning resumes if the physical defender abandons interception");
            // The initial heavy spawn is a valid approach, not yet a
            // legal contact foot. Move this controlled integration fixture
            // to an actually walkable, unobstructed melee range BEFORE
            // asserting that a stock-hit telegraph is truthful.
            var clear=typeof(GameCore).GetMethod("ClearAttackPath",
                System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.NonPublic);
            Check(clear!=null,"Verify the physical stock strike lane");
            bool contactFoot=false;
            for(float dx=-2f;dx<=2f&&!contactFoot;dx+=.5f)
            for(float dy=-2f;dy<=2f&&!contactFoot;dy+=.5f)
            {
                Point candidate=new Point(post.x+dx,post.y+dy);
                if((candidate-new Point(post.x,post.y)).Length>2.2f||
                    (candidate-melee.Position).Length<1.1f||
                    !g.CanStand(candidate)||
                    !(bool)clear.Invoke(g,new object[]{
                        candidate,new Point(post.x,post.y)}))
                    continue;
                heavy.x=candidate.x;heavy.y=candidate.y;
                contactFoot=true;
            }
            Check(contactFoot,"Actual map supplies an honest second stock contact foot");
            heavy.state=1;heavy.timer=.28f;
            heavy.targetingOuterPatrolStock=true;
            Check(g.OuterPatrolIncomingStockThreat==heavy&&
                g.OuterPatrolIncomingStockHeavy,
                "Warning follows the earliest genuine heavy contact");
            heavy.stun=.8f;
            Check(g.OuterPatrolIncomingStockThreat==melee,
                "Stunned heavy cannot falsely warn of an imminent contact");
            heavy.stun=0f;heavy.targetingOuterPatrolStock=false;
            Check(g.OuterPatrolIncomingStockThreat==melee,
                "Heavy aimed elsewhere cannot counterfeit cache impact");
            melee.hp=0f;
            Check(g.OuterPatrolIncomingStockThreat==null&&
                g.OuterPatrolIncomingStockSeconds<0f,
                "Dead or canceled raiders instantly retract warning");
            // The observer is deliberately away from the cache during
            // warning verification. Return to its real interactable foot
            // before opening the withdrawal dialogue; Talk cannot create
            // an out-of-range conversation by design.
            g.save.x=post.x;g.save.y=post.y;
            g.Talk(post);
            int abort=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_abort");
            g.Choose(abort);
            Check(!g.OuterPatrolActive&&
                g.OuterPatrolIncomingStockThreat==null,
                "Leaving real siege removes all pending stock cues");
        }

        public static void CompanionCancelledAutoContactNeverFiresAtDeadOrLostTarget(Campaign data)
        {
            GameCore g=new GameCore(data);g.mode="world";
            g.save.room=0;g.enemies.Clear();g.projectiles.Clear();
            g.save.companion=1;
            g.save.x=data.maps[0].spawnX;
            g.save.y=data.maps[0].spawnY;
            g.companionPosition=g.Position;
            Point foot=new Point();bool found=false;
            var shot=typeof(GameCore).GetMethod("WeaponShot",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            var target=typeof(GameCore).GetField("companionAutoTarget",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            var next=typeof(GameCore).GetField("companionAttack",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            var tick=typeof(GameCore).GetMethod("TickCompanion",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            Check(shot!=null&&target!=null&&next!=null&&tick!=null,
                "Test existing physical companion production release");
            for(int dx=-5;dx<=5&&!found;dx++)
            for(int dy=-5;dy<=5&&!found;dy++)
            {
                Point p=g.companionPosition+new Point(dx,dy);
                Point delta=p-g.companionPosition;
                if(delta.Length<2f||delta.Length>5f||!g.CanStand(p))
                    continue;
                Projectile probe=(Projectile)shot.Invoke(g,new object[]{
                    g.companionPosition,1,delta.Normalized,9f,0f,(Point?)p});
                if(probe.blockedAtSpawn)continue;
                foot=p;found=true;
            }
            Check(found,"Original town terrain has a genuine clear ally bow lane");
            Enemy foe=new Enemy(920601,0,foot.x,foot.y,30f);
            g.enemies.Add(foe);
            Point aim=(foe.Position-g.companionPosition).Normalized;
            g.companionAction=new ActorAction {
                kind="attack",weapon=1,duration=.42f,contact=.16f,
                damage=12f,direction=aim,target=foe.Position
            };
            target.SetValue(g,foe);next.SetValue(g,2.8f);
            // During the already-paid bow draw the hero killed the mark.
            foe.hp=0f;
            int effects=g.effects.Count;
            tick.Invoke(g,new object[]{.17f});
            Check(g.companionAction.released&&g.projectiles.Count==0&&
                g.effects.Count==effects&&
                (float)next.GetValue(g)<=.22f,
                "A dead auto-target cannot emit a phantom arrow or muzzle flash");

            // Test an occluded/stale release separately from death:
            // moving out of the real seven-unit attack envelope should
            // never still shoot toward the former locked position.
            foe.hp=30f;foe.x=foot.x+20f;
            g.companionAction=new ActorAction {
                kind="attack",weapon=1,duration=.42f,contact=.16f,
                damage=12f,direction=aim,target=foot
            };
            target.SetValue(g,foe);next.SetValue(g,2.8f);
            tick.Invoke(g,new object[]{.17f});
            Check(g.projectiles.Count==0&&g.effects.Count==effects&&
                (float)next.GetValue(g)<=.22f,
                "A target escaping physical range cancels old arrow release");

            // A subsequent genuinely shootable foe remains available,
            // without consuming a second false 2.8s cooldown.
            foe.x=foot.x;foe.y=foot.y;
            g.companionAction=null;next.SetValue(g,0f);
            tick.Invoke(g,new object[]{.02f});
            Check(g.companionAction!=null&&
                g.companionAction.kind=="attack"&&
                (Enemy)target.GetValue(g)==foe,
                "Short retry locks a freshly reachable, living target");
            tick.Invoke(g,new object[]{.16f});
            Check(g.projectiles.Exists(p=>p.friendly&&!p.blockedAtSpawn),
                "A recovered original bow release still creates a real arrow");
        }

        public static void GuardedAllyRetargetsWhenLockedMuzzleBecomesBlocked(Campaign data)
        {
            WorldObject post=Array.Find(data.maps[0].objects,
                o=>o.kind=="outerwatch");
            GameCore g=new GameCore(data);g.mode="world";
            g.save.room=0;g.enemies.Clear();g.projectiles.Clear();
            g.save.flags.Add("outer_cache_0");
            g.save.flags.Add("outer_patrol_done_0");
            g.save.companion=1;
            g.save.x=post.x;g.save.y=post.y;
            g.companionPosition=new Point(post.x+1.1f,post.y+1.1f);
            g.Talk(post);
            int start=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_veteran");
            Check(start>=0,"Real watchpost offers the veteran defense");
            g.Choose(start);
            g.Talk(post);
            int guard=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_guard");
            Check(guard>=0,"Ranger can take the physical guard order");
            g.Choose(guard);
            Check(g.OuterPatrolCompanionGuarding,
                "Release-time retargeting runs only under a real guard order");
            Point guardFoot=g.OuterPatrolCompanionGuardPosition;
            g.enemies.Clear();g.projectiles.Clear();

            var flags=System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance;
            var clear=typeof(GameCore).GetMethod("ClearAttackPath",flags);
            var shot=typeof(GameCore).GetMethod("WeaponShot",flags);
            var tick=typeof(GameCore).GetMethod("TickCompanion",flags);
            var select=typeof(GameCore).GetMethod("NearestCompanionThreat",flags);
            var target=typeof(GameCore).GetField("companionAutoTarget",flags);
            var cooldown=typeof(GameCore).GetField("companionAttack",flags);
            Check(clear!=null&&shot!=null&&tick!=null&&select!=null&&
                target!=null&&cooldown!=null,
                "Exercise real path, muzzle, target and release APIs");

            // The post's preferred guard foot is intentionally an open firing
            // lane. Find a nearby REAL walkable corner for the adversarial
            // case, rather than assuming the best guard tile has a blocked
            // muzzle. A live guard order remains active while the ally is
            // physically walking back to that saved post.
            List<Point> origins=new List<Point>();
            for(float dx=-8f;dx<=8f;dx+=.5f)
            for(float dy=-8f;dy<=8f;dy+=.5f)
            {
                Point candidate=guardFoot+new Point(dx,dy);
                if(!g.CanStand(candidate))continue;
                bool nearBlockedTile=false;
                for(int sx=-1;sx<=1&&!nearBlockedTile;sx++)
                for(int sy=-1;sy<=1;sy++)
                {
                    if(sx==0&&sy==0)continue;
                    if(!g.CanStand(candidate+new Point(sx*.7f,sy*.7f)))
                    {nearBlockedTile=true;break;}
                }
                if(nearBlockedTile)origins.Add(candidate);
            }
            origins.Sort((a,b)=>(a-guardFoot).Length.CompareTo((b-guardFoot).Length));
            Point origin=new Point(),first=new Point(),alternate=new Point(),
                corner=new Point();
            bool foundLayout=false;
            List<Point> open=new List<Point>();
            List<Point> muzzleBlocked=new List<Point>();
            foreach(Point candidateOrigin in origins)
            {
                open.Clear();muzzleBlocked.Clear();
                for(float dx=-6.5f;dx<=6.5f;dx+=.5f)
                for(float dy=-6.5f;dy<=6.5f;dy+=.5f)
                {
                    Point p=candidateOrigin+new Point(dx,dy);
                    float distance=(p-candidateOrigin).Length;
                    if(distance<1.4f||distance>=6.9f||!g.CanStand(p)||
                        !(bool)clear.Invoke(g,new object[]{candidateOrigin,p}))
                        continue;
                    Projectile probe=(Projectile)shot.Invoke(g,new object[]{
                        candidateOrigin,1,(p-candidateOrigin).Normalized,
                        9f,0f,(Point?)p});
                    if(probe.blockedAtSpawn)muzzleBlocked.Add(p);
                    else open.Add(p);
                }
                if(open.Count<2||muzzleBlocked.Count==0)continue;
                open.Sort((a,b)=>(a-candidateOrigin).Length.CompareTo(
                    (b-candidateOrigin).Length));
                Point candidateFirst=open[0],candidateAlternate=new Point(),
                    candidateCorner=new Point();
                bool foundAlternate=false,foundCorner=false;
                float firstDistance=(candidateFirst-candidateOrigin).Length;
                foreach(Point p in open)
                    if((p-candidateOrigin).Length>=firstDistance+1f&&
                        (p-candidateFirst).Length>=1.25f)
                    {candidateAlternate=p;foundAlternate=true;break;}
                foreach(Point p in muzzleBlocked)
                    if((p-candidateOrigin).Length<6.9f&&
                        (p-candidateFirst).Length>1f&&
                        (p-candidateAlternate).Length>1.25f)
                    {candidateCorner=p;foundCorner=true;break;}
                if(foundAlternate&&foundCorner)
                {
                    origin=candidateOrigin;first=candidateFirst;
                    alternate=candidateAlternate;corner=candidateCorner;
                    foundLayout=true;break;
                }
            }
            Check(foundLayout,
                "Find a real walkable origin with both a valid backup and a clear-feet/blocked-muzzle corner");
            g.companionPosition=origin;

            Enemy primary=new Enemy(920610,0,first.x,first.y,30f);
            Enemy backup=new Enemy(920611,0,alternate.x,alternate.y,30f);
            g.enemies.Add(primary);g.enemies.Add(backup);
            Check((Enemy)select.Invoke(g,null)==primary,
                "Automatic target selection chooses the nearest genuinely shootable foe");
            target.SetValue(g,primary);cooldown.SetValue(g,2.8f);
            g.companionAction=new ActorAction {
                kind="attack",weapon=1,duration=.42f,contact=.16f,
                damage=12f,direction=(primary.Position-origin).Normalized,
                target=primary.Position
            };

            primary.x=corner.x;primary.y=corner.y;
            Point blockedAim=primary.Position-origin;
            Projectile blockedProbe=(Projectile)shot.Invoke(g,new object[]{
                origin,1,blockedAim.Normalized,9f,0f,(Point?)primary.Position});
            Check((bool)clear.Invoke(g,new object[]{origin,primary.Position})&&
                blockedProbe.blockedAtSpawn,
                "Locked foe keeps clear foot LOS while its real muzzle becomes blocked");
            tick.Invoke(g,new object[]{.17f});
            Check(g.companionAction!=null&&g.companionAction.released&&
                (Enemy)target.GetValue(g)==backup&&
                g.projectiles.Exists(p=>p.friendly&&!p.blockedAtSpawn),
                "Prepared shot redirects at release to the valid backup instead of wasting its contact");
            Check(g.companionAction.target.HasValue&&
                (g.companionAction.target.Value-backup.Position).Length<.01f,
                "Released projectile target and live aim stay on the replacement foe");

            // Ordinary field combat uses the same release-time muzzle
            // recovery, without relying on an active outpost guard order.
            GameCore field=new GameCore(data);field.mode="world";
            field.save.room=0;field.save.companion=1;
            field.save.x=data.maps[0].spawnX;field.save.y=data.maps[0].spawnY;
            field.companionPosition=origin;
            field.enemies.Clear();field.projectiles.Clear();
            Enemy fieldPrimary=new Enemy(920612,0,first.x,first.y,30f);
            Enemy fieldBackup=new Enemy(920613,0,alternate.x,alternate.y,30f);
            field.enemies.Add(fieldPrimary);field.enemies.Add(fieldBackup);
            var fieldTarget=typeof(GameCore).GetField("companionAutoTarget",flags);
            var fieldCooldown=typeof(GameCore).GetField("companionAttack",flags);
            var fieldTick=typeof(GameCore).GetMethod("TickCompanion",flags);
            Check(fieldTarget!=null&&fieldCooldown!=null&&fieldTick!=null,
                "Ordinary companion combat exposes its production release state");
            fieldTarget.SetValue(field,fieldPrimary);
            fieldCooldown.SetValue(field,2.8f);
            field.companionAction=new ActorAction {
                kind="attack",weapon=1,duration=.42f,contact=.16f,
                damage=12f,direction=(fieldPrimary.Position-origin).Normalized,
                target=fieldPrimary.Position
            };
            fieldPrimary.x=corner.x;fieldPrimary.y=corner.y;
            fieldTick.Invoke(field,new object[]{.17f});
            Check(field.companionAction!=null&&field.companionAction.released&&
                (Enemy)fieldTarget.GetValue(field)==fieldBackup&&
                field.projectiles.Exists(p=>p.friendly&&!p.blockedAtSpawn),
                "Ordinary field auto-fire reuses its prepared contact on the legal backup");
            Check(field.companionAction.target.HasValue&&
                (field.companionAction.target.Value-fieldBackup.Position).Length<.01f,
                "Field release updates aim and projectile target together");

            // A foe can die after the companion has committed its draw.
            // The release must reacquire a living, actually shootable backup
            // instead of firing at the dead foe's stale coordinates.
            GameCore fieldKilled=new GameCore(data);fieldKilled.mode="world";
            fieldKilled.save.room=0;fieldKilled.save.companion=1;
            fieldKilled.companionPosition=origin;
            fieldKilled.enemies.Clear();fieldKilled.projectiles.Clear();
            Enemy doomed=new Enemy(920614,0,first.x,first.y,30f);
            Enemy survivor=new Enemy(920615,0,alternate.x,alternate.y,30f);
            fieldKilled.enemies.Add(doomed);fieldKilled.enemies.Add(survivor);
            fieldTarget.SetValue(fieldKilled,doomed);
            fieldCooldown.SetValue(fieldKilled,2.8f);
            fieldKilled.companionAction=new ActorAction {
                kind="attack",weapon=1,duration=.42f,contact=.16f,
                damage=12f,direction=(doomed.Position-origin).Normalized,
                target=doomed.Position
            };
            fieldKilled.enemies.Remove(doomed);
            fieldTick.Invoke(fieldKilled,new object[]{.17f});
            Check(fieldKilled.companionAction!=null&&
                fieldKilled.companionAction.released&&
                (Enemy)fieldTarget.GetValue(fieldKilled)==survivor&&
                fieldKilled.projectiles.Exists(p=>p.friendly&&!p.blockedAtSpawn),
                "Ordinary field auto-fire reacquires a living backup when its locked foe dies mid-windup");
            Check(fieldKilled.companionAction.target.HasValue&&
                (fieldKilled.companionAction.target.Value-survivor.Position).Length<.01f,
                "Target death cannot leave the replacement arrow aimed at stale coordinates");

            // Automatic recovery must never rewrite an explicit user order.
            // Move its marked foe into the same muzzle-only corner after the
            // command starts: the command still fires along the captured aim.
            GameCore manual=new GameCore(data);manual.mode="world";
            manual.save.room=0;manual.save.companion=1;
            manual.companionPosition=origin;
            manual.enemies.Clear();manual.projectiles.Clear();
            Enemy commanded=new Enemy(920616,0,first.x,first.y,30f);
            Enemy bystander=new Enemy(920617,0,alternate.x,alternate.y,30f);
            manual.enemies.Add(commanded);manual.enemies.Add(bystander);
            manual.SetAimTarget(first);
            manual.CommandCompanion();
            Check(manual.companionAction!=null&&
                manual.companionAction.kind=="command"&&
                manual.companionAction.target.HasValue,
                "Player command captures its explicit world target");
            Point explicitTarget=manual.companionAction.target.Value;
            commanded.x=corner.x;commanded.y=corner.y;
            tick.Invoke(manual,new object[]{.21f});
            Check(manual.companionAction.released&&
                manual.companionAction.target.HasValue&&
                (manual.companionAction.target.Value-explicitTarget).Length<.01f&&
                manual.projectiles.Count==3&&
                manual.projectiles.TrueForAll(p=>p.friendly&&!p.blockedAtSpawn)&&
                target.GetValue(manual)==null,
                "Automatic retargeting never steals an explicit volley or changes its captured aim");
        }

        public static void GuardedAllyRedirectsPreparedRealArrowToIncomingStockThreat(Campaign data)
        {
            WorldObject post=Array.Find(data.maps[0].objects,
                o=>o.kind=="outerwatch");
            GameCore g=new GameCore(data);g.mode="world";
            g.save.room=0;g.enemies.Clear();g.projectiles.Clear();
            g.save.flags.Add("outer_cache_0");
            g.save.flags.Add("outer_patrol_done_0");
            g.save.companion=1;
            g.save.x=post.x;g.save.y=post.y;
            g.companionPosition=new Point(post.x+1.1f,post.y+1.1f);
            g.Talk(post);
            int start=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_veteran");
            Check(start>=0,"Veteran defense is offered at the real post");
            g.Choose(start);
            g.Talk(post);
            int guard=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_guard");
            Check(guard>=0,"Ranger can guard stock during real siege");
            g.Choose(guard);
            Enemy melee=Array.Find(g.enemies.ToArray(),e=>e.kind==0);
            Enemy heavy=Array.Find(g.enemies.ToArray(),e=>e.kind==2);
            Check(melee!=null&&heavy!=null,"Both live melee and heavy exist");
            var selection=typeof(GameCore).GetMethod("NearestCompanionThreat",
                System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.NonPublic);
            var tick=typeof(GameCore).GetMethod("TickCompanion",
                System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.NonPublic);
            var target=typeof(GameCore).GetField("companionAutoTarget",
                System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.NonPublic);
            var cooldown=typeof(GameCore).GetField("companionAttack",
                System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.NonPublic);
            Check(selection!=null&&tick!=null&&target!=null&&cooldown!=null,
                "Regress actual automatic draw, target and released projectile");
            // Both shots must be possible before measuring target priority.
            Enemy available=(Enemy)selection.Invoke(g,null);
            Check(available==melee||available==heavy,
                "Registered stock raider is shootable through a real muzzle");
            Enemy alternate=available==melee?heavy:melee;
            Check(!alternate.targetingOuterPatrolStock,
                "Secondary saboteur starts without a telegraphed stock hit");
            cooldown.SetValue(g,0f);
            tick.Invoke(g,new object[]{.02f});
            Check(g.companionAction!=null&&
                g.companionAction.kind=="attack"&&
                (Enemy)target.GetValue(g)==available,
                "A real ally starts a planted, automatic bow windup");
            // Other live raider commits after bow drawing began; retarget
            // before physical release instead of firing into old stock lane.
            // The guard has already exhausted its finite two real body
            // contacts (tested by the genuine three-hit stock test).
            // This validates urgent auto-fire against an UNGUARDED lane.
            var blocks=typeof(GameCore).GetField("outerPatrolGuardBlocksLeft",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            Check(blocks!=null,"Finite bodyguard shield is registered");
            blocks.SetValue(g,0);
            PlaceShootableCacheContact(g,post,alternate,available);
            MoveToRealUncoveredStockObserver(g,post,alternate,available);
            alternate.state=1;alternate.targetingOuterPatrolStock=true;
            Check(g.OuterPatrolIncomingStockThreat==alternate,
                "The late alternative has a real unprotected and visible stock strike");
            tick.Invoke(g,new object[]{.16f});
            Check(g.companionAction.released&&
                (Enemy)target.GetValue(g)==alternate&&
                g.projectiles.Exists(p=>p.friendly&&!p.blockedAtSpawn),
                "One genuine released projectile changes to urgent stock attacker");
            Check((g.companionAction.target.Value-alternate.Position).Length<.01f,
                "Prepared shot's direction and target both follow actual threat");

            // A second genuine stock attack can become more urgent while
            // the companion is already committed to another stock attacker.
            // Being a valid target is not enough: choose the earlier release.
            PlaceShootableCacheContact(g,post,alternate,available);
            PlaceShootableCacheContact(g,post,available,alternate);
            MoveToRealUncoveredStockObserver(g,post,alternate,available);
            alternate.state=1;alternate.targetingOuterPatrolStock=true;
            available.state=1;available.targetingOuterPatrolStock=true;
            alternate.timer=.9f;available.timer=.12f;
            Check(g.OuterPatrolIncomingStockThreat==available,
                "The second unguarded stock strike has the earlier real deadline");
            target.SetValue(g,alternate);
            cooldown.SetValue(g,2.8f);
            g.companionAction=new ActorAction {
                kind="attack",weapon=1,duration=.42f,contact=.16f,
                damage=12f,direction=(alternate.Position-
                    g.companionPosition).Normalized,target=alternate.Position
            };
            tick.Invoke(g,new object[]{.17f});
            Check(g.companionAction.released&&
                (Enemy)target.GetValue(g)==available&&
                g.projectiles.Exists(p=>p.friendly&&!p.blockedAtSpawn),
                "A faster stock strike supersedes an already-valid slower stock lock");
            Check((g.companionAction.target.Value-available.Position).Length<.01f,
                "Emergency retarget updates the real projectile aim and target together");
        }

        public static void CompanionPrioritizesSoonestAuthenticStockStrike(Campaign data)
        {
            WorldObject post=Array.Find(data.maps[0].objects,
                o=>o.kind=="outerwatch");
            GameCore g=new GameCore(data);g.mode="world";
            g.save.room=0;g.enemies.Clear();g.projectiles.Clear();
            g.save.x=post.x;g.save.y=post.y;g.save.companion=1;
            g.companionPosition=new Point(post.x+1.1f,post.y+1.1f);
            g.save.flags.Add("outer_cache_0");
            g.save.flags.Add("outer_patrol_done_0");
            g.Talk(post);
            int start=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_veteran");
            Check(start>=0,"A real veteran cache defense can be started");
            g.Choose(start);
            Enemy melee=Array.Find(g.enemies.ToArray(),e=>e.kind==0);
            Enemy heavy=Array.Find(g.enemies.ToArray(),e=>e.kind==2);
            Check(melee!=null&&heavy!=null,
                "Both authentic stock-raiding enemy classes exist");
            PlaceShootableCacheContact(g,post,heavy,melee);
            PlaceShootableCacheContact(g,post,melee,heavy);
            MoveToRealUncoveredStockObserver(g,post,heavy,melee);
            heavy.state=1;heavy.targetingOuterPatrolStock=true;
            melee.state=1;melee.targetingOuterPatrolStock=true;
            Enemy near=(heavy.Position-g.companionPosition).Length<
                (melee.Position-g.companionPosition).Length?heavy:melee;
            Enemy far=near==heavy?melee:heavy;
            near.timer=.9f;far.timer=.12f;
            Check(g.OuterPatrolIncomingStockThreat==far,
                "Real stock-warning selector confirms the farther strike lands first");
            var select=typeof(GameCore).GetMethod("NearestCompanionThreat",
                System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.NonPublic);
            Check(select!=null,"Use the production companion target selector");
            Check((Enemy)select.Invoke(g,null)==far,
                "Companion chooses earliest real stock contact over the nearer slower raider");
            near.timer=far.timer=.4f;
            Check((Enemy)select.Invoke(g,null)==near,
                "Equal imminent contact times retain nearest-target tie breaking");
            near.timer=-.02f;far.timer=-.15f;
            Check((Enemy)select.Invoke(g,null)==far,
                "A farther but already-overdue stock hit outranks a less-overdue nearby raider");
        }

        public static void OuterPatrolWarningSelectsFiniteEarliestThreatAndStableTie(Campaign data)
        {
            WorldObject post=Array.Find(data.maps[0].objects,
                o=>o.kind=="outerwatch");
            GameCore g=new GameCore(data);g.mode="world";
            g.save.room=0;g.enemies.Clear();g.projectiles.Clear();
            g.save.x=post.x;g.save.y=post.y;g.save.companion=1;
            g.companionPosition=new Point(post.x+1.1f,post.y+1.1f);
            g.save.flags.Add("outer_cache_0");
            g.save.flags.Add("outer_patrol_done_0");
            g.Talk(post);
            int start=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_veteran");
            Check(start>=0,"A real veteran cache warning can be exercised");
            g.Choose(start);
            Enemy melee=Array.Find(g.enemies.ToArray(),e=>e.kind==0);
            Enemy heavy=Array.Find(g.enemies.ToArray(),e=>e.kind==2);
            Check(melee!=null&&heavy!=null,
                "Real melee and heavy stock attackers are present");
            PlaceShootableCacheContact(g,post,heavy,melee);
            PlaceShootableCacheContact(g,post,melee,heavy);
            MoveToRealUncoveredStockObserver(g,post,heavy,melee);
            heavy.state=melee.state=1;
            heavy.targetingOuterPatrolStock=melee.targetingOuterPatrolStock=true;
            heavy.timer=float.NaN;melee.timer=.2f;
            // Put the malformed timer first to prove it cannot poison the
            // warning selector before a later authentic finite threat.
            g.enemies.Remove(heavy);g.enemies.Insert(0,heavy);
            Check(g.OuterPatrolIncomingStockThreat==melee,
                "A NaN attack clock cannot hide the next real stock impact");
            Check(Math.Abs(g.OuterPatrolIncomingStockSeconds-.2f)<.001f,
                "Incoming-stock HUD exposes a finite countdown for a real strike");
            var select=typeof(GameCore).GetMethod("NearestCompanionThreat",
                System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.NonPublic);
            Check(select!=null,"Use the production companion threat selector");
            Check((Enemy)select.Invoke(g,null)==melee,
                "An invalid stock timer cannot monopolize the companion's target priority");
            heavy.timer=float.PositiveInfinity;
            Check(g.OuterPatrolIncomingStockThreat==melee&&
                (Enemy)select.Invoke(g,null)==melee,
                "Infinite stock timers are excluded from both warnings and emergency target priority");

            // Equal deadlines use physical distance to the protected stock,
            // then favor the heavy if both are at the same distance. Reorder
            // the live actors so list insertion order cannot choose the HUD.
            heavy.timer=melee.timer=.2f;
            Point cache=new Point(post.x,post.y);
            Point h=heavy.Position-cache,m=melee.Position-cache;
            float hd=h.x*h.x+h.y*h.y,md=m.x*m.x+m.y*m.y;
            Enemy expected=Math.Abs(hd-md)<.0001f?heavy:(hd<md?heavy:melee);
            Enemy other=expected==heavy?melee:heavy;
            g.enemies.Remove(other);g.enemies.Remove(expected);
            g.enemies.Insert(0,other);g.enemies.Insert(1,expected);
            Check(g.OuterPatrolIncomingStockThreat==expected,
                "Equal-deadline warnings choose the nearest physical stock contact independent of list order");
            Check(g.OuterPatrolIncomingStockHeavy==(expected.kind==2),
                "Heavy warning metadata matches the deterministically selected threat");

            Enemy fartherCache=Math.Abs(hd-md)<.0001f?melee:
                (hd>md?heavy:melee);
            Enemy nearerCache=fartherCache==heavy?melee:heavy;
            fartherCache.timer=-.15f;nearerCache.timer=-.02f;
            Check(g.OuterPatrolIncomingStockThreat==fartherCache&&
                g.OuterPatrolIncomingStockSeconds==0f,
                "Raw negative deadlines prioritize an overdue strike while the HUD clamps its display to zero");
        }

        public static void OuterPatrolInvalidWindupsRecoverAndRetractWarnings(Campaign data)
        {
            WorldObject post=Array.Find(data.maps[0].objects,
                o=>o.kind=="outerwatch");
            GameCore g=new GameCore(data);g.mode="world";
            g.save.room=0;g.enemies.Clear();g.projectiles.Clear();
            g.save.x=post.x;g.save.y=post.y;g.save.companion=1;
            g.companionPosition=new Point(post.x+1.1f,post.y+1.1f);
            g.save.flags.Add("outer_cache_0");
            g.save.flags.Add("outer_patrol_done_0");
            g.Talk(post);
            int start=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_veteran");
            Check(start>=0,"A real cache defense starts the recovery test");
            g.Choose(start);
            Enemy melee=Array.Find(g.enemies.ToArray(),e=>e.kind==0);
            Enemy heavy=Array.Find(g.enemies.ToArray(),e=>e.kind==2);
            Check(melee!=null&&heavy!=null,
                "The real patrol enemy roster is available");
            PlaceShootableCacheContact(g,post,heavy,melee);
            MoveToRealUncoveredStockObserver(g,post,heavy,melee);
            g.save.companion=-1;
            foreach(Enemy foe in g.enemies)
            {
                if(foe==heavy)continue;
                foe.state=0;foe.timer=5f;
                foe.targetingWayfarer=foe.targetingReliefCivilian=
                    foe.targetingAfterlightStock=foe.targetingOuterPatrolStock=false;
            }
            heavy.state=1;heavy.timer=float.NaN;
            heavy.targetingOuterPatrolStock=true;
            heavy.target=new Point(post.x,post.y);
            float integrity=g.OuterPatrolIntegrity;
            var flags=System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.NonPublic;
            var pendingField=typeof(GameCore).GetField("rescueWindupEffects",flags);
            Check(pendingField!=null,
                "The real pending-windup warning registry is available");
            object table=pendingField.GetValue(g);
            var add=table.GetType().GetMethod("Add");
            Check(add!=null,"Pending warning ownership can be registered");
            Effect nanWarning=new Effect("danger",heavy.target,1f);
            g.effects.Add(nanWarning);
            add.Invoke(table,new object[]{heavy,nanWarning});
            g.Tick(.02f,new Point());
            Check(heavy.state==0&&Math.Abs(heavy.timer-.22f)<.001f&&
                !heavy.targetingOuterPatrolStock,
                "A NaN windup recovers into a short safe reacquisition instead of becoming immortal");
            Check(!g.effects.Contains(nanWarning),
                "The invalid enemy's stale danger warning is removed by owner identity");
            Check(Math.Abs(g.OuterPatrolIntegrity-integrity)<.001f,
                "Invalid enemy clocks cannot cause an untelegraphed stock hit");

            heavy.state=1;heavy.timer=float.PositiveInfinity;
            heavy.targetingOuterPatrolStock=true;
            Effect infiniteWarning=new Effect("danger",heavy.target,1f);
            g.effects.Add(infiniteWarning);
            add.Invoke(table,new object[]{heavy,infiniteWarning});
            g.Tick(.02f,new Point());
            Check(heavy.state==0&&Math.Abs(heavy.timer-.22f)<.001f&&
                !heavy.targetingOuterPatrolStock&&
                !g.effects.Contains(infiniteWarning),
                "An infinite windup also retires its target lock and warning cleanly");
            Check(Math.Abs(g.OuterPatrolIntegrity-integrity)<.001f,
                "Neither malformed clock can bypass the real stock-contact checks");

            // Idle cooldowns use the same transient clock and must recover too;
            // otherwise an enemy silently stops attacking for the rest of combat.
            heavy.state=0;heavy.timer=float.NaN;
            heavy.targetingOuterPatrolStock=true;
            Effect idleWarning=new Effect("danger",heavy.target,1f);
            g.effects.Add(idleWarning);
            add.Invoke(table,new object[]{heavy,idleWarning});
            g.Tick(.02f,new Point());
            Check(heavy.state==0&&Math.Abs(heavy.timer-.22f)<.001f&&
                !heavy.targetingOuterPatrolStock&&!g.effects.Contains(idleWarning),
                "A malformed idle cooldown also resets and retires stale objective cues");
            Check(Math.Abs(g.OuterPatrolIntegrity-integrity)<.001f,
                "Idle timer recovery cannot create an untelegraphed stock hit");

            // A malformed reaction clock must be repaired BEFORE it enters
            // Math.Max/Min lock-budget math. Exercise actual patrol windups and
            // owner-keyed warning cleanup, including the next safe tick.
            System.Reflection.FieldInfo clockField;
            foreach (string clockName in new[] {
                "hitTime", "stun", "releaseTime", "windupDuration" })
            foreach (float malformed in new[] {
                float.NaN, float.PositiveInfinity })
            {
                heavy.state=1;heavy.timer=.8f;heavy.hitTime=0f;
                heavy.stun=0f;heavy.releaseTime=0f;heavy.windupDuration=.7f;
                heavy.flash=.1f;heavy.stagger=0f;heavy.staggerRecovery=0f;
                heavy.moveTime=.2f;heavy.recoil=new Point(1f,0f);
                heavy.targetingWayfarer=heavy.targetingReliefCivilian=false;
                heavy.targetingAfterlightStock=false;
                heavy.targetingOuterPatrolStock=true;
                heavy.target=new Point(post.x,post.y);
                clockField=typeof(Enemy).GetField(clockName);
                Check(clockField!=null,"The production enemy reaction clock exists: "+clockName);
                clockField.SetValue(heavy,malformed);
                Effect warning=new Effect("danger",heavy.target,1f);
                g.effects.Add(warning);add.Invoke(table,new object[]{heavy,warning});
                g.Tick(.02f,new Point());
                Check(heavy.state==0&&Math.Abs(heavy.timer-.22f)<.001f&&
                    !heavy.targetingOuterPatrolStock&&
                    !g.effects.Contains(warning),
                    "Malformed "+clockName+" must cancel the unsafe windup and retract only its own warning");
                Check(!float.IsNaN(heavy.timer)&&!float.IsInfinity(heavy.timer)&&
                    !float.IsNaN(heavy.hitTime)&&!float.IsInfinity(heavy.hitTime)&&
                    !float.IsNaN(heavy.stun)&&!float.IsInfinity(heavy.stun)&&
                    !float.IsNaN(heavy.releaseTime)&&!float.IsInfinity(heavy.releaseTime)&&
                    !float.IsNaN(heavy.windupDuration)&&!float.IsInfinity(heavy.windupDuration),
                    "Reaction-clock repair leaves every action deadline finite");
                Check(Math.Abs(g.OuterPatrolIntegrity-integrity)<.001f,
                    "Malformed "+clockName+" cannot produce an untelegraphed stock impact");
                g.Tick(.02f,new Point());
                Check(!float.IsNaN(heavy.timer)&&!float.IsInfinity(heavy.timer)&&
                    heavy.state==0,
                    "A repaired "+clockName+" remains recoverable on the following real world tick");
            }

            // Cosmetic/poise clocks should be normalized without cancelling a
            // valid, already-readable windup or changing its real deadline.
            foreach (string clockName in new[] {
                "flash", "stagger", "staggerRecovery", "moveTime" })
            foreach (float malformed in new[] {
                float.NaN, float.NegativeInfinity })
            {
                heavy.state=1;heavy.timer=5f;heavy.hitTime=0f;heavy.stun=0f;
                heavy.releaseTime=0f;heavy.windupDuration=.7f;
                heavy.recoil=new Point(1f,0f);
                heavy.targetingOuterPatrolStock=true;
                clockField=typeof(Enemy).GetField(clockName);
                Check(clockField!=null,"The production enemy presentation clock exists: "+clockName);
                clockField.SetValue(heavy,malformed);
                g.Tick(.02f,new Point());
                Check(heavy.state==1&&Math.Abs(heavy.timer-4.98f)<.01f,
                    "Repairing cosmetic/poise "+clockName+" must not interrupt a valid telegraphed attack");
                float repaired=(float)clockField.GetValue(heavy);
                Check(!float.IsNaN(repaired)&&!float.IsInfinity(repaired),
                    "Cosmetic/poise clock "+clockName+" must remain finite after a real tick");
                Check(Math.Abs(g.OuterPatrolIntegrity-integrity)<.001f,
                    "Repairing "+clockName+" cannot alter attack release timing or damage");
            }

            // A malformed recoil vector is just as dangerous as a bad timer:
            // it must not feed NaN into the real swept collision solver.
            foreach (Point malformedRecoil in new[] {
                new Point(float.NaN,0f), new Point(0f,float.PositiveInfinity) })
            {
                heavy.state=1;heavy.timer=.8f;heavy.hitTime=.1f;
                heavy.stun=0f;heavy.releaseTime=0f;heavy.windupDuration=.7f;
                heavy.recoil=malformedRecoil;
                heavy.targetingOuterPatrolStock=true;
                heavy.target=new Point(post.x,post.y);
                Effect warning=new Effect("danger",heavy.target,1f);
                g.effects.Add(warning);add.Invoke(table,new object[]{heavy,warning});
                float x=heavy.x,y=heavy.y;
                g.Tick(.02f,new Point());
                Check(heavy.state==0&&Math.Abs(heavy.timer-.22f)<.001f&&
                    !g.effects.Contains(warning)&&
                    Math.Abs(heavy.x-x)<.0001f&&Math.Abs(heavy.y-y)<.0001f,
                    "Malformed recoil must cancel safely before corrupting the actor's swept world position");
                Check(Math.Abs(g.OuterPatrolIntegrity-integrity)<.001f,
                    "Malformed recoil cannot create a false objective contact");
            }
        }

        public static void CompanionPrioritizesRealOutpostSaboteursOverPassingFoes(Campaign data)
        {
            WorldObject post=Array.Find(data.maps[0].objects,o=>o.kind=="outerwatch");
            GameCore g=new GameCore(data);g.mode="world";
            g.save.room=0;g.save.x=post.x;g.save.y=post.y;
            g.enemies.Clear();
            g.save.flags.Add("outer_cache_0");
            g.Talk(post);
            int start=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_start");
            Check(start>=0,"An eligible real watchpost can start combat");
            g.Choose(start);
            Check(g.OuterPatrolActive&&g.enemies.Count==2,
                "Raider priority depends on a genuine live defense");
            Enemy saboteur=Array.Find(g.enemies.ToArray(),e=>e.kind==0);
            Check(saboteur!=null,"A cache saboteur has a genuine world body");
            g.save.companion=1;
            g.companionPosition=new Point(post.x,post.y);
            var chooser=typeof(GameCore).GetMethod("NearestCompanionThreat",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            Check(chooser!=null,"Actual companion targeting is exercised");
            Enemy picked=(Enemy)chooser.Invoke(g,null);
            Check(picked==saboteur,
                "A visible melee stock saboteur beats a ranged raid distractor");
            // An unrelated foe even closer to Ivo should not draw the
            // bodyguard away from the cache while the defense is live.
            Enemy decoy=new Enemy(8111,0,post.x+.3f,post.y,30f);
            g.enemies.Add(decoy);
            picked=(Enemy)chooser.Invoke(g,null);
            Check(picked==saboteur,
                "Cache defense prioritizes its actual protected objective");
            g.enemies.Remove(decoy);
            // When the actual attack is obstructed, do not aim forever
            // through a closed wall: the standard line/muzzle validation
            // must still choose a legal unrelated enemy.
            g.Talk(post);
            int abort=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_abort");
            Check(abort>=0,"The defended physical post offers withdrawal");
            g.Choose(abort);
            g.enemies.Add(decoy);
            Check((Enemy)chooser.Invoke(g,null)==decoy,
                "After the real defense is withdrawn auto targeting remains normal");
        }

        public static void VeteranFlawlessMedalsRequireRealThreeKillsAndAreEarnableOnReplay(Campaign data)
        {
            var hit=typeof(GameCore).GetMethod("Hit",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            Check(hit!=null,"Flawless medals rely on genuine Hit transactions");
            for(int room=0;room<8;room++)
            {
                WorldObject post=Array.Find(data.maps[room].objects,
                    o=>o.kind=="outerwatch");
                GameCore g=new GameCore(data);g.mode="world";
                g.save.room=room;g.enemies.Clear();g.projectiles.Clear();
                g.save.level=25;g.save.xp=0;
                g.save.x=post.x;g.save.y=post.y;
                g.save.flags.Add("outer_cache_"+room);
                g.save.flags.Add("outer_patrol_done_"+room);
                g.save.inventory[0]=6;g.save.inventory[4]=4;
                int coins=g.save.coins,shards=g.save.shards;
                // Eight-region mastery reward is independent of initial
                // eight-veteran bonuses and ordinary XP leveling.
                if(room==7)
                    for(int i=0;i<7;i++)
                    {
                        g.save.flags.Add("outer_patrol_done_"+i);
                        g.save.flags.Add("outer_patrol_veteran_"+i);
                        g.save.flags.Add("outer_patrol_flawless_"+i);
                    }
                int talent=g.save.skillPoints;
                g.Talk(post);
                int start=Array.FindIndex(g.conversation.choices,
                    c=>c.action=="outer_watch_veteran");
                Check(start>=0,"Veteran fight is physically offered");
                g.Choose(start);
                Check(g.OuterPatrolFlawlessEligible,
                    "Clean challenge is eligible at untouched 100% stock");
                g.Talk(post);
                int brace=Array.FindIndex(g.conversation.choices,
                    c=>c.action=="outer_watch_brace");
                Check(brace>=0,"Veteran post allows voluntary protective cover");
                g.Choose(brace);
                Check(!g.OuterPatrolFlawlessEligible,
                    "Deploying tactical cover forfeits this run's no-assist medal");
                Enemy[] foes=g.enemies.ToArray();
                foreach(Enemy e in foes)
                {
                    hit.Invoke(g,new object[]{e,10000f,true,null,null});
                    for(int i=0;i<12;i++)g.Tick(.02f,new Point());
                }
                Check(g.OuterPatrolVeteranDone(room)&&
                    !g.OuterPatrolFlawlessDone(room),
                    "Three genuine kills with a barricade pay base win but not a medal");
                g.Talk(post);
                start=Array.FindIndex(g.conversation.choices,
                    c=>c.action=="outer_watch_veteran");
                g.Choose(start);
                Check(g.OuterPatrolFlawlessEligible,
                    "Replay has a fresh honest no-assist challenge");
                foes=g.enemies.ToArray();
                foreach(Enemy e in foes)
                {
                    hit.Invoke(g,new object[]{e,10000f,true,null,null});
                    for(int i=0;i<12;i++)g.Tick(.02f,new Point());
                }
                Check(g.OuterPatrolFlawlessDone(room)&&
                    !g.OuterPatrolActive&&g.save.coins==coins+26&&
                    g.save.shards==shards+2,
                    "Clean real replay awards permanent medal without duplicate first-clear loot");
                if(room==7)
                    Check(g.OuterPatrolFlawlessWins==8&&
                        g.Has("outer_patrol_flawless_all")&&
                        g.save.skillPoints==talent+2,
                        "Genuine eighth veteran and eighth flawless each grant one skill point");
                else Check(g.save.skillPoints==talent,
                    "Partial flawless collection cannot farm talent points");
                SaveData persisted=new SaveData{
                    version=2,room=g.save.room,stage=g.save.stage,
                    x=g.save.x,y=g.save.y,
                    presentationVersion=g.save.presentationVersion,
                    flags=new List<string>(g.save.flags),
                    trackedQuest="outer_watch_flawless"
                };
                GameCore reload=new GameCore(data,persisted);
                Check(reload.save.version==2&&
                    reload.OuterPatrolFlawlessDone(room)&&
                    reload.save.trackedQuest=="outer_watch_flawless",
                    "New mastery medal is an ordinary legacy-compatible v2 flag and journal");
                g.Talk(post);
                start=Array.FindIndex(g.conversation.choices,
                    c=>c.action=="outer_watch_veteran");
                g.Choose(start);
                foes=g.enemies.ToArray();
                foreach(Enemy e in foes)
                {
                    hit.Invoke(g,new object[]{e,10000f,true,null,null});
                    for(int i=0;i<12;i++)g.Tick(.02f,new Point());
                }
                Check(g.OuterPatrolFlawlessDone(room)&&
                    g.save.coins==coins+26&&g.save.shards==shards+2,
                    "Third full authentic clean win cannot repeat rewards");
            }
        }

        public static void FlawlessFieldCareActuallyImprovesSafeTickAndStopsDuringRaids(Campaign data)
        {
            WorldObject post=Array.Find(data.maps[0].objects,
                o=>o.kind=="outerwatch");
            GameCore g=new GameCore(data);g.mode="world";
            g.save.room=0;g.enemies.Clear();g.projectiles.Clear();
            g.save.x=post.x;g.save.y=post.y;
            g.save.flags.Add("outer_patrol_done_0");
            g.save.flags.Add("outer_patrol_veteran_0");
            g.save.health=45f;g.save.energy=30f;
            Check(g.OuterPatrolRefuge,"Defended post is a real place of recovery");
            g.Tick(.05f,new Point());
            float veteranHp=g.save.health-45f;
            float veteranEnergy=g.save.energy-30f;
            g.save.flags.Add("outer_patrol_flawless_0");
            g.save.health=45f;g.save.energy=30f;
            g.Tick(.05f,new Point());
            float flawlessHp=g.save.health-45f;
            float flawlessEnergy=g.save.energy-30f;
            Check(flawlessHp>veteranHp+.025f&&
                flawlessEnergy>veteranEnergy+.05f,
                "Real 50ms game tick grants strictly better local care after medal");
            g.enemies.Add(new Enemy(9911,0,post.x+.7f,post.y,32f));
            g.save.health=45f;g.save.energy=30f;
            Check(!g.OuterPatrolRefuge,
                "Real nearby raider disables medal-powered refuge care");
            g.Tick(.05f,new Point());
            float blockedHp=g.save.health;
            float blockedEnergy=g.save.energy;
            // The hero has native combat energy regeneration even with no
            // refuge. Compare the SAME real threat with and without the medal
            // instead of incorrectly requiring all regeneration to stop.
            g.save.flags.Remove("outer_patrol_flawless_0");
            g.save.health=45f;g.save.energy=30f;
            g.Tick(.05f,new Point());
            Check(blockedHp<=45f&&g.save.health<=45f&&
                Math.Abs(blockedEnergy-g.save.energy)<.01f,
                "A hostile raider suppresses medal care without removing ordinary stamina recovery");
            g.save.flags.Add("outer_patrol_flawless_0");
            g.enemies.Clear();
            g.save.x=post.x+20f;g.save.y=post.y+20f;
            Check(!g.OuterPatrolRefuge,
                "Flawless reward never grants region-wide free healing");
            GameCore reloaded=new GameCore(data,new SaveData{
                version=2,room=0,x=post.x,y=post.y,
                flags=new List<string>(g.save.flags),
                presentationVersion=g.save.presentationVersion
            });
            Check(reloaded.OuterPatrolFlawlessDone(0)&&
                reloaded.OuterPatrolRefuge,
                "Old v2 save retains the field care upgrade without new fields");
        }

        public static void LateLiveInterceptionProtectsCacheOnRealEnemyRelease(Campaign data)
        {
            WorldObject post=Array.Find(data.maps[0].objects,
                o=>o.kind=="outerwatch");
            GameCore g=new GameCore(data);g.mode="world";
            g.save.room=0;g.enemies.Clear();g.projectiles.Clear();
            g.save.flags.Add("outer_cache_0");
            g.save.x=post.x;g.save.y=post.y;
            g.Talk(post);
            int start=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_start");
            Check(start>=0,"Defend an actual supply cache");
            g.Choose(start);
            Check(g.OuterPatrolActive&&g.enemies.Count==2,
                "An authentic raid has two real enemy actors");
            Enemy melee=Array.Find(g.enemies.ToArray(),e=>e.kind==0);
            Check(melee!=null&&(melee.Position-new Point(post.x,post.y)).Length<=2.35f,
                "Raid melee attacker starts at genuine contact range");
            // The target lock and telegraph are already committed to stock.
            // A defender enters AFTER the windup began but BEFORE the
            // real release, so it must still be possible to block the cache.
            melee.state=1;melee.timer=.02f;
            melee.target=new Point(post.x,post.y);
            melee.targetingOuterPatrolStock=true;
            melee.Face(melee.target-melee.Position);
            melee.stun=0f;melee.releaseTime=0f;
            float before=g.OuterPatrolIntegrity;
            g.Tick(.05f,new Point());
            Check(g.OuterPatrolActive&&g.OuterPatrolIntegrity==before,
                "Defender arriving during authentic attack windup intercepts its stock hit");
            Check(g.effects.Exists(e=>e.kind=="number"&&
                e.outerPatrolOwned&&e.text.Contains(
                    g.save.english?"INTERCEPTED":"拦截成功")),
                "Actual blocked release gives localized, owned readable combat feedback");
            // The SAME attacker and identical release must damage an
            // unobstructed physical store once the hero leaves the lane.
            Point retreat=new Point();bool found=false;
            for(int dx=-10;dx<=10&&!found;dx++)
            for(int dy=-10;dy<=10&&!found;dy++)
            {
                Point p=new Point(post.x+dx,post.y+dy);
                if((p-new Point(post.x,post.y)).Length>10f||
                    (p-melee.Position).Length<5f||!g.CanStand(p))continue;
                retreat=p;found=true;
            }
            Check(found,"Find genuine unoccupied map floor away from melee");
            g.save.x=retreat.x;g.save.y=retreat.y;
            melee.state=1;melee.timer=.02f;
            melee.target=new Point(post.x,post.y);
            melee.targetingOuterPatrolStock=true;
            melee.Face(melee.target-melee.Position);
            melee.stun=0f;melee.releaseTime=0f;
            g.Tick(.05f,new Point());
            Check(g.OuterPatrolActive&&g.OuterPatrolIntegrity<before,
                "With no defender in range, an identical real contact damages stock");
            // Ordinary regional defense and old saves retain their original
            // one-time reward/failure flags; this is transient interception.
            Check(!g.OuterPatrolDone(0)&&g.save.version==2,
                "Interception is not a forged victory or a save schema migration");
        }

        public static void VeteranBarricadeSpendsRealSuppliesAndAbsorbsReleasedStockHits(Campaign data)
        {
            WorldObject post=Array.Find(data.maps[0].objects,
                o=>o.kind=="outerwatch");
            GameCore g=new GameCore(data);g.mode="world";
            g.save.room=0;g.enemies.Clear();g.projectiles.Clear();
            g.save.x=post.x;g.save.y=post.y;
            g.save.flags.Add("outer_cache_0");
            g.save.flags.Add("outer_patrol_done_0");
            g.save.inventory[0]=8;g.save.inventory[4]=4;
            // The ordinary first-time encounter must not pretend that a
            // veteran barricade is a usable menu command.
            GameCore ordinary=new GameCore(data);ordinary.mode="world";
            ordinary.save.room=0;ordinary.enemies.Clear();
            ordinary.save.x=post.x;ordinary.save.y=post.y;
            ordinary.save.flags.Add("outer_cache_0");
            ordinary.save.inventory[0]=8;ordinary.save.inventory[4]=4;
            ordinary.Talk(post);
            int normal=Array.FindIndex(ordinary.conversation.choices,
                c=>c.action=="outer_watch_start");
            ordinary.Choose(normal);
            ordinary.Talk(post);
            Check(Array.FindIndex(ordinary.conversation.choices,
                c=>c.action=="outer_watch_brace")<0,
                "Standard two-raider raid never advertises veteran-only cover");
            ordinary.Close();
            g.Talk(post);
            int start=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_veteran");
            Check(start>=0,"Completed relief post offers actual veteran raid");
            g.Choose(start);
            Check(g.OuterPatrolVeteranActive&&g.OuterPatrolBraceIntegrity==0&&
                g.OuterPatrolBraceAvailable,"Veteran assault offers an unused physical brace");
            g.Talk(post);
            int brace=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_brace");
            Check(brace>=0,"Barricade is a genuine post interaction");
            g.Choose(brace);
            Check(g.OuterPatrolBraceIntegrity==48f&&
                g.OuterPatrolBraceStanding&&
                g.OuterPatrolBraceHitFlash==0f&&
                !g.OuterPatrolBraceAvailable&&
                g.save.inventory[0]==6&&g.save.inventory[4]==3,
                "First barricade spends two real timber and one spare part");
            g.Talk(post);
            brace=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_brace");
            Check(brace<0,"An already built barricade is not offered again");
            var invoke=typeof(GameCore).GetMethod("ChooseOuterPatrol",
                System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.NonPublic);
            Check(invoke!=null,"Old cached attack menu is still defended");
            invoke.Invoke(g,new object[]{"outer_watch_brace"});
            Check(g.save.inventory[0]==6&&g.save.inventory[4]==3,
                "Forged cached brace choice cannot buy a second barricade");
            // A private handler call bypasses Choose(index)'s automatic
            // Close(); restore gameplay mode before advancing real AI time.
            g.Close();
            Enemy melee=Array.Find(g.enemies.ToArray(),e=>e.kind==0);
            Check(melee!=null,"Veteran raid owns an authentic melee stock attacker");
            Point safe=new Point();bool found=false;
            for(int radius=7;radius<=10&&!found;radius++)
            for(int dx=-radius;dx<=radius&&!found;dx++)
            for(int dy=-radius;dy<=radius&&!found;dy++)
            {
                Point p=new Point(post.x+dx,post.y+dy);
                if((p-new Point(post.x,post.y)).Length>11f||
                    (p-melee.Position).Length<6f||!g.CanStand(p))
                    continue;
                safe=p;found=true;
            }
            Check(found,"A real walkable observation foot exists near cache");
            g.save.x=safe.x;g.save.y=safe.y;
            float stock=g.OuterPatrolIntegrity;
            for(int frame=0;frame<1100&&g.OuterPatrolActive&&
                g.OuterPatrolBraceIntegrity==48f;frame++)
                g.Tick(.02f,new Point());
            Check(g.OuterPatrolActive&&g.OuterPatrolBraceIntegrity<48f,
                "Unintercepted real raider winds up and releases into the barricade");
            Check(g.OuterPatrolBraceStanding&&g.OuterPatrolBraceHitFlash>0f,
                "A real stock contact drives visible barricade impact feedback");
            Check(g.OuterPatrolIntegrity==stock,
                "A real stock strike first consumes physical cover, not protected cache HP");
            g.save.x=post.x;g.save.y=post.y;
            g.Talk(post);
            brace=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_brace");
            Check(brace<0,"Broken or damaged used cover stays unavailable");
            invoke.Invoke(g,new object[]{"outer_watch_brace"});
            Check(g.save.inventory[0]==6&&g.save.inventory[4]==3,
                "A damaged cover cannot be re-purchased during same siege");
            g.Talk(post);
            int abort=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_abort");
            g.Choose(abort);
            Check(!g.OuterPatrolActive&&g.OuterPatrolBraceIntegrity==0f&&
                !g.OuterPatrolBraceStanding&&g.OuterPatrolBraceHitFlash==0f,
                "Withdrawal retires live barricade artwork cues and attackers");
            GameCore restored=new GameCore(data,g.save);
            Check(!restored.OuterPatrolActive&&restored.OuterPatrolBraceIntegrity==0&&
                restored.save.version==2,"Old v2 saves never persist temporary cover");
        }

        public static void VeteranReliefDefenseHasThreeRealKillsOneTimeRewardAndSafeReplay(Campaign data)
        {
            var hit=typeof(GameCore).GetMethod("Hit",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            Check(hit!=null,"Use genuine combat Hit, not fake victory flags");
            for(int room=0;room<8;room++)
            {
                WorldObject post=Array.Find(data.maps[room].objects,
                    o=>o.kind=="outerwatch");
                Check(post!=null,"Veteran post is authored in room "+room);
                GameCore g=new GameCore(data);g.mode="world";
                g.save.room=room;g.enemies.Clear();g.projectiles.Clear();
                // Isolate the distinct veteran "eight defended" talent
                // from ordinary XP level-ups caused by four-to-six real
                // kill/reward transactions in this same integration test.
                // Level 25 begins at zero XP, far from its next threshold.
                g.save.level=25;g.save.xp=0;
                g.save.x=post.x;g.save.y=post.y;
                g.save.flags.Add("outer_cache_"+room);
                g.save.flags.Add("outer_patrol_done_"+room);
                int coins=g.save.coins;
                // Final veteran clear must really award exactly one talent
                // after *eight* regional victories, never before.
                if(room==7)
                    for(int previous=0;previous<7;previous++)
                    {
                        g.save.flags.Add("outer_patrol_done_"+previous);
                        g.save.flags.Add("outer_patrol_veteran_"+previous);
                    }
                int talents=g.save.skillPoints;
                g.Talk(post);
                Check(g.conversation!=null,
                    "Veteran battle is offered at a physical post in region "+room);
                int veteran=Array.FindIndex(g.conversation.choices,
                    c=>c.action=="outer_watch_veteran");
                Check(veteran>=0,
                    "Already guarded supply depot offers actual veteran combat");
                g.Choose(veteran);
                Check(g.OuterPatrolActive&&g.OuterPatrolVeteranActive&&
                    g.OuterPatrolTime>109f&&g.enemies.Count==3,
                    "Veteran raid spawns three genuine enemy bodies and a full cache clock");
                Enemy[] raiders=g.enemies.ToArray();
                Check(raiders[0].kind==0&&raiders[1].kind==1&&
                    raiders[2].kind==2&&raiders[2].hp>raiders[0].hp,
                    "Veteran squad has melee, real ranged, and tougher slammer");
                int shard=g.save.shards;
                hit.Invoke(g,new object[]{raiders[0],10000f,true,null,null});
                for(int n=0;n<12;n++)g.Tick(.02f,new Point());
                hit.Invoke(g,new object[]{raiders[1],10000f,true,null,null});
                for(int n=0;n<12;n++)g.Tick(.02f,new Point());
                Check(!g.OuterPatrolVeteranDone(room)&&g.OuterPatrolActive,
                    "Two kills cannot counterfeit a three-member veteran victory");
                hit.Invoke(g,new object[]{raiders[2],10000f,true,null,null});
                for(int n=0;n<15;n++)g.Tick(.02f,new Point());
                Check(g.OuterPatrolVeteranDone(room)&&
                    !g.OuterPatrolActive&&g.save.coins==coins+26&&
                    g.save.shards==shard+2,
                    "Three genuine lethal transactions award exactly one veteran clear");
                Check(room==7?
                    g.OuterPatrolVeteranWins==8&&
                    g.Has("outer_patrol_veteran_all")&&
                    g.save.skillPoints==talents+1:
                    g.save.skillPoints==talents,
                    "Exactly the eighth genuine regional veteran win awards the extra talent");
                // Loading a NEW hero may sanitize an otherwise locked
                // region. Don't alias the current active hero's SaveData:
                // this check must not teleport the current challenger.
                SaveData persisted=new SaveData {
                    version=g.save.version, room=g.save.room,
                    stage=g.save.stage,x=g.save.x,y=g.save.y,
                    flags=new List<string>(g.save.flags)
                };
                GameCore reloaded=new GameCore(data,persisted);
                Check(reloaded.save.version==2&&
                    reloaded.OuterPatrolVeteranDone(room)&&
                    !reloaded.OuterPatrolVeteranActive,
                    "Veteran clear persists in existing v2 flags with no transient raid leak");
                g.Talk(post);
                Check(g.conversation!=null,"Completed veteran post "+
                    room+" remains physically interactable");
                veteran=Array.FindIndex(g.conversation.choices,
                    c=>c.action=="outer_watch_veteran");
                Check(veteran>=0,"Veteran combat remains playable after victory");
                g.Choose(veteran);
                Check(g.OuterPatrolVeteranActive&&g.enemies.Count==3,
                    "Replay actually spawns combat again, not a free menu reward");
                Enemy[] replay=g.enemies.ToArray();
                foreach(Enemy raider in replay)
                {
                    hit.Invoke(g,new object[]{raider,10000f,true,null,null});
                    for(int n=0;n<12;n++)g.Tick(.02f,new Point());
                }
                Check(!g.OuterPatrolActive&&g.save.coins==coins+26&&
                    g.save.shards==shard+2,
                    "Actual lethal veteran replay produces no duplicate coins or shards");
                g.Talk(post);
                veteran=Array.FindIndex(g.conversation.choices,
                    c=>c.action=="outer_watch_veteran");
                Check(veteran>=0,"Veteran post stays replayable after a full second victory");
                g.Choose(veteran);
                Check(g.OuterPatrolVeteranActive&&g.enemies.Count==3,
                    "A third veteran attempt still creates a real three-raider fight");
                g.enemies.Clear();g.Tick(.02f,new Point());
                Check(!g.OuterPatrolActive&&g.save.coins==coins+26,
                    "Interrupted subsequent attempts cannot farm a reward");
            }
        }

        public static void VeteranOutpostJournalTracksTrueRegionalFlagsAndLiveObjective(Campaign data)
        {
            GameCore g=new GameCore(data);g.mode="world";
            QuestGuide q=g.GetQuest("outer_watch_veteran");
            Check(q.locked&&!q.complete&&q.steps.Count==8,
                "New quest is dormant until a real ordinary supply defense");
            g.save.flags.Add("outer_patrol_done_0");
            q=g.GetQuest("outer_watch_veteran");
            WorldObject post=Array.Find(data.maps[0].objects,
                o=>o.kind=="outerwatch");
            Check(!q.locked&&!q.complete&&q.room==0&&
                (q.target-new Point(post.x,post.y)).Length<.1f,
                "Journal points to the actual unlocked region-zero veteran post");
            g.save.flags.Add("outer_patrol_veteran_0");
            q=g.GetQuest("outer_watch_veteran");
            Check(q.steps[0].done&&q.Progress>.12f&&
                !q.complete,"Genuine permanent veteran flag advances the tracked quest");
            for(int room=1;room<8;room++)
            {
                g.save.flags.Add("outer_patrol_done_"+room);
                g.save.flags.Add("outer_patrol_veteran_"+room);
            }
            q=g.GetQuest("outer_watch_veteran");
            Check(q.complete&&q.Progress==1f,
                "All eight persisted veteran victories complete the in-game quest");
            SaveData persisted=new SaveData {
                version=2,room=0,trackedQuest="outer_watch_veteran",
                // Preserve modern v2 journal presentation. A fresh
                // presentationVersion=0 correctly migrates a LEGACY UI
                // and resets old tracking, as the original game requires.
                presentationVersion=g.save.presentationVersion,
                flags=new List<string>(g.save.flags)
            };
            GameCore restored=new GameCore(data,persisted);
            Check(restored.save.trackedQuest=="outer_watch_veteran"&&
                restored.GetQuest("outer_watch_veteran").complete,
                "Legacy v2 journal selection survives loading a separate slot");

            // A real running siege should outrank the tracked objective
            // while retaining that tracked quest for the next safe moment.
            GameCore siege=new GameCore(data);siege.mode="world";
            siege.save.room=0;siege.enemies.Clear();
            siege.save.x=post.x;siege.save.y=post.y;
            siege.save.flags.Add("outer_cache_0");
            siege.save.trackedQuest="outer_watch_veteran";
            siege.Talk(post);
            int start=Array.FindIndex(siege.conversation.choices,
                c=>c.action=="outer_watch_start");
            Check(start>=0,"Real outpost is combat-ready");
            siege.Choose(start);
            Point? marker=siege.NavigationTarget();
            Check(siege.OuterPatrolActive&&marker.HasValue&&
                (marker.Value-new Point(post.x,post.y)).Length<.1f&&
                siege.save.trackedQuest=="outer_watch_veteran",
                "World objective marker tracks live cache without rewriting saved quest");
        }

        public static void OuterPatrolCacheDefenseUsesRealTimedStockCombat(Campaign data)
        {
            // The existing eight-map witness proves physically playable roots.
            // Here a real melee unit must telegraph, wind up and reach the
            // actual store instead of magically draining it or only chasing
            // a distant hero. Old v2 flags stay unaltered on withdrawal.
            int room=0;
            WorldObject post=Array.Find(data.maps[room].objects,
                o=>o.kind=="outerwatch");
            Check(post!=null,"Canal has a real physical relief watchpost");
            GameCore g=new GameCore(data);
            g.save.room=room;g.enemies.Clear();
            g.save.x=post.x;g.save.y=post.y;
            g.save.flags.Add("outer_cache_"+room);
            g.save.inventory[0]=5;g.save.inventory[2]=5;
            int coins=g.save.coins;
            g.Talk(post);
            int action=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_start");
            Check(action>=0,"Recovered supplies permit a genuine timed siege");
            g.Choose(action);
            Check(g.OuterPatrolActive&&g.OuterPatrolTime>=94f&&
                g.OuterPatrolIntegrity==100f,
                "New relief siege has real unsaved clock and full cache integrity");
            Enemy melee=Array.Find(g.enemies.ToArray(),e=>e.kind==0);
            Check(melee!=null,"A real melee attacker targets the defended cache");
            // Position hero on a legal tile outside melee interception range.
            // The attacker must walk toward the post, not passively drain HP.
            Point safe=new Point();bool found=false;
            for(int radius=7;radius<=10&&!found;radius++)
            for(int x=-radius;x<=radius&&!found;x++)
            for(int y=-radius;y<=radius&&!found;y++)
            {
                Point candidate=new Point(post.x+x,post.y+y);
                if((candidate-new Point(post.x,post.y)).Length>11f||
                    (candidate-melee.Position).Length<6f||!g.CanStand(candidate))
                    continue;
                safe=candidate;found=true;
            }
            Check(found,"Find an actual legal bodyguard withdrawal foot");
            g.save.x=safe.x;g.save.y=safe.y;
            float before=g.OuterPatrolIntegrity;
            for(int frame=0;frame<1250&&g.OuterPatrolActive&&
                    g.OuterPatrolIntegrity==before;frame++)
                g.Tick(.02f,new Point());
            Check(g.OuterPatrolActive&&g.OuterPatrolIntegrity<before,
                "Unintercepted raider actually completes a timed attack against supplies");
            float damaged=g.OuterPatrolIntegrity;
            // Only the physically present guard can apply the once-per-run
            // emergency patch. Do not touch permanent cache flag.
            g.save.x=post.x;g.save.y=post.y;
            g.Talk(post);
            Check(g.conversation!=null,"Live outpost can report raid integrity");
            int patch=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_patch");
            Check(patch>=0,"The ongoing defense offers a player-choice repair");
            g.Choose(patch);
            Check(g.OuterPatrolIntegrity>damaged&&g.OuterPatrolIntegrity<=100f&&
                g.save.inventory[0]==4&&g.save.inventory[2]==4,
                "First legitimate cache patch spends real supplies and repairs durability");
            g.Talk(post);
            patch=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_patch");
            Check(patch>=0,"Patch choice remains visible but is single-use");
            g.Choose(patch);
            Check(g.save.inventory[0]==4&&g.save.inventory[2]==4,
                "Repeating a cached UI action cannot spend a second repair");
            g.Talk(post);
            int abort=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_abort");
            Check(abort>=0,"The post allows explicit safe withdrawal");
            g.Choose(abort);
            Check(!g.OuterPatrolActive&&g.enemies.Count==0&&
                !g.OuterPatrolDone(room)&&g.save.coins==coins&&
                g.OuterCacheRecovered(room),
                "Aborted physical siege retires attackers, pays no false reward and preserves v2 cache");
            GameCore reload=new GameCore(data,g.save);
            Check(!reload.OuterPatrolActive&&reload.OuterCacheRecovered(room)&&
                !reload.OuterPatrolDone(room),
                "Transient siege pressure never contaminates legacy save slots");
        }

        public static void OuterPatrolRealTimeoutAndPerimeterDoNotForgeWins(Campaign data)
        {
            // Timeout is a real world clock, not a local-wall-clock task.
            // Stun actual raiders only to isolate the countdown from their
            // otherwise-tested genuine melee pressure.
            WorldObject post=Array.Find(data.maps[0].objects,
                o=>o.kind=="outerwatch");
            GameCore g=new GameCore(data);
            g.save.room=0;g.enemies.Clear();
            g.save.x=post.x;g.save.y=post.y;
            g.save.flags.Add("outer_cache_0");
            int coins=g.save.coins;
            g.Talk(post);
            int start=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_start");
            Check(start>=0,"Eligible physical outpost offers a real defense");
            g.Choose(start);
            foreach(Enemy attacker in g.enemies)attacker.stun=120f;
            for(int i=0;i<1860;i++)g.Tick(.05f,new Point());
            Check(g.OuterPatrolActive&&g.OuterPatrolTime>1f&&
                g.OuterPatrolTime<3f&&g.OuterPatrolIntegrity==100f,
                "Combat clock advances on genuine simulation time without phantom supply damage");
            for(int i=0;i<80&&g.OuterPatrolActive;i++)
                g.Tick(.05f,new Point());
            Check(!g.OuterPatrolActive&&g.enemies.Count==0&&
                !g.OuterPatrolDone(0)&&g.save.coins==coins&&
                g.OuterCacheRecovered(0),
                "Real timeout retires only its raiders with no forged reward or lost v2 cache");
            g.Talk(post);
            start=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_start");
            Check(start>=0,"Timed-out cache defense can be started again");
            g.Choose(start);
            Point away=new Point();bool found=false;
            for(int y=1;y<g.Map.rows.Length-1&&!found;y++)
            for(int x=1;x<g.Map.rows[y].Length-1&&!found;x++)
            {
                Point p=new Point(x+.5f,y+.5f);
                if(g.CanStand(p)&&(p-new Point(post.x,post.y)).Length>13f)
                {away=p;found=true;}
            }
            Check(found,"Map has legal ground beyond the outpost perimeter");
            g.save.x=away.x;g.save.y=away.y;
            g.Tick(.05f,new Point());
            Check(!g.OuterPatrolActive&&g.enemies.Count==0&&
                !g.OuterPatrolDone(0)&&g.save.coins==coins,
                "Leaving the physical defense radius interrupts the run without paying");
            // The v2 save must never record ephemeral siege HP or clock.
            var reload=new GameCore(data,g.save);
            Check(!reload.OuterPatrolActive&&reload.OuterPatrolDone(0)==false&&
                reload.OuterCacheRecovered(0),
                "Pre-existing cache survives both failure paths without schema migration");
        }

        public static void DefendedOuterCachesBecomeRealSafeFieldRefuges(Campaign data)
        {
            for(int room=0;room<8;room++)
            {
                WorldObject post=Array.Find(data.maps[room].objects,
                    x=>x.kind=="outerwatch");
                Check(post!=null,"Each region has an authored supply post");
                GameCore g=new GameCore(data);g.mode="world";g.save.room=room;
                g.enemies.Clear();g.projectiles.Clear();g.hazards.Clear();
                g.save.x=post.x;g.save.y=post.y;
                g.save.health=Math.Min(g.save.maxHealth-25f,45f);
                g.save.energy=21f;
                Check(!g.OuterPatrolRefuge,
                    "An unprotected outpost must not give free field treatment");
                g.save.flags.Add("outer_patrol_done_"+room);
                Check(g.OuterPatrolRefuge,
                    "Only a legitimately defended reachable outpost becomes a refuge");
                float hp=g.save.health, energy=g.save.energy;
                g.Tick(.05f,new Point());
                Check(g.save.health>hp+.06f&&g.save.energy>energy,
                    "Defended outpost actually replenishes finite combat resources");
                // The status comes from the original v2 flag, not a
                // separate migration or untracked runtime counter.
                GameCore reload=new GameCore(data,g.save);
                reload.save.room=room;reload.enemies.Clear();
                reload.projectiles.Clear();reload.hazards.Clear();
                reload.save.x=post.x;reload.save.y=post.y;
                Check(reload.save.version==2&&reload.OuterPatrolRefuge,
                    "Reloaded legacy save retains a physically usable outpost");
                // A real pursuer forces the field medic to stand down.
                Enemy pursuer=new Enemy(9000,0,post.x+.4f,post.y+.4f,30);
                g.enemies.Add(pursuer);
                Check(!g.OuterPatrolRefuge,
                    "A nearby actual enemy suppresses the otherwise active treatment");
                g.enemies.Clear();
                g.save.flags.Remove("outer_patrol_done_"+room);
                Check(!g.OuterPatrolRefuge,
                    "Removing a victory flag immediately removes its local benefit");
            }
        }

        public static void DefendedOutpostRoutesRespectLiveProgressAndSafeLanding(Campaign data)
        {
            WorldObject[] posts=new WorldObject[8];
            for(int i=0;i<8;i++)
                posts[i]=Array.Find(data.maps[i].objects,o=>o.kind=="outerwatch");
            GameCore g=new GameCore(data);g.mode="world";
            g.save.room=0;g.save.x=posts[0].x;g.save.y=posts[0].y;
            g.enemies.Clear();g.projectiles.Clear();g.hazards.Clear();
            g.save.stage=3;
            g.save.flags.Add("outer_patrol_done_0");
            g.save.flags.Add("outer_patrol_done_1");
            int coin=g.save.coins;
            g.Talk(posts[0]);
            int routes=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_routes");
            Check(routes>=0,"Defended outpost offers a usable travel board");
            g.Choose(routes);
            int west=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_ring_0");
            Check(west>=0,"Travel board has a real regional route selection");
            g.Choose(west);
            int target=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_to_1");
            Check(target>=0,"Another genuinely defended reachable region appears");
            Check(Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_to_2")<0,
                "Unsecured outposts cannot be selected to skip defense");
            g.Choose(target);
            Check(g.save.room==1&&g.CanStand(g.Position)&&
                g.OuterPatrolDone(0)&&g.OuterPatrolDone(1)&&
                g.save.coins==coin,
                "Existing map Enter and walkable landing preserve rewards and flags");
            GameCore reloaded=new GameCore(data,g.save);
            Check(reloaded.save.version==2&&reloaded.OuterPatrolDone(1),
                "Fast travel introduces no new save schema or unlock reset");

            g.save.x=posts[1].x;g.save.y=posts[1].y;
            g.enemies.Clear();g.projectiles.Clear();g.hazards.Clear();
            g.Talk(posts[1]);
            routes=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_routes");
            g.Choose(routes);
            // The safe outpost menu may remain open while a real raider
            // arrives. Stale travel actions must be reauthenticated.
            Enemy attacker=new Enemy(9111,0,posts[1].x+1f,posts[1].y,40f);
            g.enemies.Add(attacker);
            west=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_ring_0");
            g.Choose(west);
            Check(g.save.room==1&&g.save.coins==coin,
                "An arriving enemy blocks cached route selections");
            g.enemies.Clear();
            g.mode="world";
            // A forged/replayed link to a mission-locked region is not
            // authorized by the old SaveData win flag alone.
            var choose=typeof(GameCore).GetMethod("ChooseOuterPatrol",
                System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.NonPublic);
            Check(choose!=null,"Live gameplay owns a guarded route handler");
            choose.Invoke(g,new object[]{"outer_watch_to_6"});
            Check(g.save.room==1,
                "Unsecured destination cannot bypass physical campaign travel");
            g.save.flags.Add("outer_patrol_done_6");
            choose.Invoke(g,new object[]{"outer_watch_to_6"});
            Check(g.save.room==1,
                "A won-but-locked region still cannot bypass its campaign entry");
        }

        public static void DefendedDesertAndHighlandPostsShelterAgainstActualWeather(Campaign data)
        {
            for(int room=6;room<=7;room++)
            {
                WorldObject post=Array.Find(data.maps[room].objects,
                    o=>o.kind=="outerwatch");
                Check(post!=null&&post.x>70f,
                    "The field refuge is out on the real exposed regional ring");
                GameCore g=new GameCore(data);g.mode="world";
                g.save.room=room;g.save.x=post.x;g.save.y=post.y;
                g.enemies.Clear();g.projectiles.Clear();g.hazards.Clear();
                g.save.teaUntil=0f;
                g.save.heat=room==6?65f:0f;
                g.save.cold=room==7?65f:0f;
                float initial=room==6?g.save.heat:g.save.cold;
                g.save.flags.Add("outer_patrol_done_"+room);
                Check(g.OuterPatrolRefuge,
                    "Defended station offers physical local shelter");
                g.Tick(.05f,new Point());
                float cooled=room==6?g.save.heat:g.save.cold;
                Check(cooled<initial,
                    "Real safe outpost suppresses and reduces local heat/cold buildup");
                // The shelter depends on a previously won v2 combat flag.
                // Removing it re-exposes the desert/snow as before.
                g.save.flags.Remove("outer_patrol_done_"+room);
                if(room==6)g.save.heat=initial;
                else g.save.cold=initial;
                g.Tick(.05f,new Point());
                float exposed=room==6?g.save.heat:g.save.cold;
                Check(exposed>initial,
                    "Undeveloped outpost has no free regional weather immunity");
                // Existing combat-threat logic must also suppress refuge.
                g.save.flags.Add("outer_patrol_done_"+room);
                g.enemies.Add(new Enemy(8810,0,post.x+1f,post.y,30f));
                Check(!g.OuterPatrolRefuge,
                    "A true close raider reopens survival pressure even at a rescued post");
            }
        }

        public static void ContinuousBiomeNoiseAvoidsSquareMaterialBoundaries(Campaign data)
        {
            for(int room=0;room<8;room++)
            {
                float first=WorldStreaming.BiomeVariation(44,56,room);
                Check(first>=0&&first<=1&&
                    first==WorldStreaming.BiomeVariation(44,56,room),
                    "Biome tint is deterministic for a given saved map coordinate");
                for(int y=48;y<60;y++)
                    for(int x=69;x<89;x++)
                    {
                        float a=WorldStreaming.BiomeVariation(x,y,room);
                        float b=WorldStreaming.BiomeVariation(x+1,y,room);
                        float c=WorldStreaming.BiomeVariation(x,y+1,room);
                        Check(a>=0&&a<=1&&Math.Abs(a-b)<.3f&&Math.Abs(a-c)<.3f,
                            "Adjacent material tints blend gradually rather than popping at eight-tile edges");
                    }
            }
            Check(Math.Abs(WorldStreaming.BiomeVariation(54,52,1)-
                WorldStreaming.BiomeVariation(54,52,7))>.0001f,
                "Forest and frost maps do not reuse identical material clusters");
        }

        public static void EightRidgeRoutesNeedBothPhysicalLandmarksAndSafeTravel(Campaign data)
        {
            for(int room=0;room<8;room++)
            {
                GameCore g=new GameCore(data);g.save.room=room;
                MapData map=data.maps[room];
                WorldObject east=Array.Find(map.objects,o=>o.id=="ridge_route_"+room+"_0");
                WorldObject south=Array.Find(map.objects,o=>o.id=="ridge_route_"+room+"_1");
                Check(east!=null&&south!=null&&g.CanStand(new Point(east.x,east.y))&&
                    g.CanStand(new Point(south.x,south.y)),
                    "Fifth ring links use two real standable authored locations");
                int coins=g.save.coins,shards=g.save.shards,xp=g.save.xp;
                Point outsideRidge=StandableOutsideTalkReach(g,east);
                g.save.x=outsideRidge.x;g.save.y=outsideRidge.y;
                g.Talk(east);g.Close();
                Check(!g.RidgeSeen(room,0)&&g.save.coins==coins&&
                    g.save.shards==shards&&g.save.xp==xp,
                    "A nearby but out-of-reach ridge marker cannot grant survey XP or shortcut credit");
                g.save.x=east.x;g.save.y=east.y;
                g.Talk(east);
                Check(g.RidgeSeen(room,0)&&!g.RidgeLinked(room),
                    "Visiting only one marker never unlocks a free crossing");
                Check(Array.FindIndex(g.conversation.choices,c=>c.action=="ridge_travel")<0,
                    "An incomplete survey offers no teleport");
                g.Close();
                g.save.x=south.x;g.save.y=south.y;g.Talk(south);
                Check(g.RidgeLinked(room)&&g.save.coins==coins+20&&
                    g.save.shards==shards+1,"Second distinct physical visit enables permanent safe shortcut");
                int choice=Array.FindIndex(g.conversation.choices,c=>c.action=="ridge_travel");
                Check(choice>=0,"Completed survey offers actionable crossing choice");
                g.save.energy=36;g.Choose(choice);
                Check(Math.Abs(g.save.x-east.x)<.01f&&Math.Abs(g.save.y-east.y)<.01f&&
                    Math.Abs(g.save.energy-24)<.01f,"Crossing moves the actual player and costs real energy");
                g.Talk(east);choice=Array.FindIndex(g.conversation.choices,c=>c.action=="ridge_travel");
                g.save.energy=3;g.Choose(choice);
                Check(Math.Abs(g.save.x-east.x)<.01f,
                    "Insufficient energy cannot cross the ridge");
                g.Talk(east);g.Close();
                Check(g.save.coins==coins+20,
                    "Revisiting completed markers never duplicates quest rewards");
                GameCore saved=new GameCore(data,g.save);
                Check(saved.save.version==2&&saved.RidgeLinked(room)&&
                    saved.save.coins==coins+20,
                    "New ridge progress restores from old SaveData v2");
            }
            GameCore all=new GameCore(data);
            for(int i=0;i<8;i++)
            {all.save.flags.Add("ridge_seen_"+i+"_0");all.save.flags.Add("ridge_seen_"+i+"_1");}
            Check(all.RidgeLinkedCount==8&&all.GetQuest("ridge_routes").complete,
                "All eight distinct ridge paths finish the tracked exploration quest");
        }

        public static void FifthRingWildlifeSpawnsAsActualCombatAwayFromQuestMarkers(Campaign data)
        {
            var spawn=typeof(GameCore).GetMethod("SpawnRoom",
                System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            Check(spawn!=null,"Use the real room-spawn transaction");
            for(int room=0;room<8;room++)
            {
                GameCore g=new GameCore(data);
                g.save.room=room;
                spawn.Invoke(g,null);
                int unlinkedFar=0;
                foreach(Enemy e in g.enemies)if(e.x>=74&&e.y>=60)unlinkedFar++;
                Check(unlinkedFar==0,
                    "Original quest rooms retain their pre-expansion enemy requirements");
                if(room>0)
                {
                    g.save.flags.Add("ridge_seen_"+room+"_0");
                    g.save.flags.Add("ridge_seen_"+room+"_1");
                    spawn.Invoke(g,null);
                    int early=0;
                    foreach(Enemy e in g.enemies)if(e.x>=74&&e.y>=60)early++;
                    Check(early==0,
                        "Early ridge discovery must never expand uncleared guardian encounters");
                    g.save.flags.Add("boss_"+room);
                    spawn.Invoke(g,null);
                }
                int far=0;
                foreach(Enemy e in g.enemies)
                    if(e.x>=74&&e.y>=60)
                    {
                        far++;
                        Check(g.CanStand(e.Position),
                            "New fifth-ring wildlife has physical passable collision");
                        foreach(WorldObject o in data.maps[room].objects)
                            Check((e.Position-new Point(o.x,o.y)).Length>=8,
                                "New wildlife remains away from real interaction points");
                        Check(e.hp>0&&e.maxHp>0,
                            "New wildlife consists of actual combat actors with hitpoints");
                    }
                if(room==0)Check(far==0,"Original town remains peaceful");
                else Check(far==3,"All seven dangerous regions gain three remote encounters");
            }
        }

        public static void TerrainVeilNeverCreatesOpaqueLoadingSquares(Campaign data)
        {
            Check(WorldStreaming.TerrainVeilOpacity(0)==.3f,
                "Newly entered world chunks show their actual terrain behind a light atmospheric veil");
            Check(WorldStreaming.TerrainVeilOpacity(1)==0,
                "Fully prewarmed terrain draws without an extra mask");
            float previous=1;
            for(int i=0;i<=100;i++)
            {
                float alpha=WorldStreaming.TerrainVeilOpacity(i/100f);
                Check(alpha>=0&&alpha<=.3f&&alpha<=previous,
                    "Chunk transitions never render opaque blank tiles or increase masking over time");
                previous=alpha;
            }
            for(int region=0;region<8;region++)
                Check(WorldStreaming.BiomeVariation(98,65,region)>=0&&
                    WorldStreaming.BiomeVariation(98,65,region)<=1,
                    "Static biome cache receives legal deterministic material values");
        }

        public static void SixthRingWeatherDialUnlocksRealSheltersAndPreservesSaveV2(Campaign data)
        {
            int[] answers={1,2,0,2,1,0,1,2};
            for(int room=0;room<8;room++)
            {
                GameCore g=new GameCore(data);g.save.room=room;
                MapData m=data.maps[room];
                WorldObject[] sites=new WorldObject[3];
                for(int i=0;i<3;i++)
                {
                    int stage=i;
                    sites[i]=Array.Find(m.objects,o=>o.id=="farsignal_"+room+"_"+stage);
                    Check(sites[i]!=null&&g.CanStand(new Point(sites[i].x,sites[i].y)),
                        "All new sixth-ring weather sites occupy actual passable map tiles");
                }
                g.save.inventory[0]=5;g.save.inventory[4]=5;
                int coins=g.save.coins;
                g.save.x=sites[2].x;g.save.y=sites[2].y;
                g.Talk(sites[2]);g.Close();
                Check(!g.FarShelterBuilt(room),"Untuned refuge cannot be rebuilt");
                g.save.x=sites[1].x;g.save.y=sites[1].y;g.Talk(sites[1]);g.Close();
                Check(!g.FarDialReady(room),"An unread log forbids calibration");
                g.save.x=sites[0].x;g.save.y=sites[0].y;
                g.Talk(sites[0]);g.Close();
                Check(g.FarLogFound(room),"Real east field log unlocks clue");
                g.save.x=sites[1].x;g.save.y=sites[1].y;
                g.Talk(sites[1]);
                int wrong=(answers[room]+1)%3;
                int idx=Array.FindIndex(g.conversation.choices,c=>c.action=="far_dial_"+wrong);
                Check(idx>=0,"Real three-choice puzzle displays incorrect options");
                g.Choose(idx);
                Check(!g.FarDialReady(room),"Incorrect observation cannot unlock shared refuge");
                g.Talk(sites[1]);
                idx=Array.FindIndex(g.conversation.choices,c=>c.action=="far_dial_"+answers[room]);
                Check(idx>=0,"Real field log's answer exists among the choices");
                g.Choose(idx);
                Check(g.FarDialReady(room),"Correct local glyph genuinely calibrates the dial");
                g.save.x=sites[2].x;g.save.y=sites[2].y;
                g.Talk(sites[2]);
                idx=Array.FindIndex(g.conversation.choices,c=>c.action=="far_build");
                Check(idx>=0,"Calibrated shelter offers actual build action");
                g.Choose(idx);
                Check(g.FarShelterBuilt(room)&&g.save.inventory[0]==4&&
                    g.save.inventory[4]==4&&g.save.coins==coins+22,
                    "Building refuge consumes real materials and pays once");
                g.save.health=Math.Max(1,g.save.maxHealth-50);g.save.energy=4;g.save.cold=40;
                g.Talk(sites[2]);
                idx=Array.FindIndex(g.conversation.choices,c=>c.action=="far_rest");
                Check(idx>=0,"Completed real shelter offers reusable warmth");
                g.Choose(idx);
                Check(g.save.inventory[0]==3&&g.save.energy==100&&
                    g.save.cold==15&&g.save.health>g.save.maxHealth-50,
                    "Timber-fed warm shelter restores real player stats");
                GameCore loaded=new GameCore(data,g.save);
                Check(loaded.save.version==2&&loaded.FarShelterBuilt(room)&&
                    loaded.save.coins==coins+22,
                    "Old save v2 persists the actual field puzzle and construction");
                loaded.save.x=sites[2].x;loaded.save.y=sites[2].y;
                loaded.Talk(sites[2]);
                Check(Array.FindIndex(loaded.conversation.choices,c=>c.action=="far_build")<0,
                    "Reading a restored shelter cannot farm the one-time build reward");
            }
            GameCore full=new GameCore(data);
            for(int room=0;room<8;room++)full.save.flags.Add("far_shelter_"+room);
            Check(full.FarShelterCount==8&&!full.GetQuest("far_refuges").complete,
                "Eight restored shelters still need real public filing at the Inn");
            full.save.room=9;
            MapData inn=data.maps[9];
            Check(inn.rows.Length==40&&inn.rows[0].Length==60,
                "Expanded Inn adds a real southern public records wing");
            var reach=new System.Collections.Generic.HashSet<int>();
            var queue=new System.Collections.Generic.Queue<int>();
            int w=inn.rows[0].Length,h=inn.rows.Length;
            int first=(int)inn.spawnY*w+(int)inn.spawnX;
            queue.Enqueue(first);reach.Add(first);
            int[,] dir={{1,0},{-1,0},{0,1},{0,-1}};
            while(queue.Count>0)
            {
                int cell=queue.Dequeue(),x=cell%w,y=cell/w;
                for(int k=0;k<4;k++)
                {
                    int xx=x+dir[k,0],yy=y+dir[k,1],key=yy*w+xx;
                    if(xx<0||yy<0||xx>=w||yy>=h||reach.Contains(key)||
                        !full.CanStand(new Point(xx+.5f,yy+.5f)))continue;
                    reach.Add(key);queue.Enqueue(key);
                }
            }
            WorldObject atlas=Array.Find(inn.objects,o=>o.kind=="faratlasdesk");
            WorldObject guest=Array.Find(inn.objects,o=>o.kind=="farrefugerecord");
            Check(atlas!=null&&guest!=null&&
                reach.Contains((int)atlas.y*w+(int)atlas.x)&&
                reach.Contains((int)guest.y*w+(int)guest.x),
                "Both Inn annex chambers are physically connected through real collision");
            full.save.x=atlas.x;full.save.y=atlas.y;
            int startCoins=full.save.coins;
            full.Talk(atlas);
            Check(full.conversation.text.IndexOf('\n')>=0&&
                full.conversation.textEn.IndexOf('\n')>=0,
                "Public inn atlas dialogue must format paragraphs as genuine newlines");
            int publish=Array.FindIndex(full.conversation.choices,c=>c.action=="far_publish");
            Check(publish>=0,"An actual Inn desk offers civic publication");
            full.Choose(publish);
            Check(full.Has("far_refuge_atlas")&&full.GetQuest("far_refuges").complete&&
                full.save.coins==startCoins+80,
                "In-person publication finishes quest with authentic once-only reward");
            GameCore reloaded=new GameCore(data,full.save);
            Check(reloaded.save.version==2&&reloaded.Has("far_refuge_atlas"),
                "Public civic atlas persists in legacy save slots");
            full.Talk(atlas);publish=Array.FindIndex(full.conversation.choices,
                c=>c.action=="far_publish");
            full.Choose(publish);
            Check(full.save.coins==startCoins+80,"Publishing twice cannot farm another grant");
        }

        public static void CameraLeadPrewarmsMovementWithoutUnboundedMapScans(Campaign data)
        {
            Check(WorldStreaming.PredictPrewarmLead(0)==0,
                "Still camera requests no unneeded directional prewarm");
            Check(WorldStreaming.PredictPrewarmLead(3)==4,
                "Following camera prepares forward chunks before arrival");
            Check(WorldStreaming.PredictPrewarmLead(-4)==-6,
                "Backward movement prepares the trailing viewport side");
            Check(WorldStreaming.PredictPrewarmLead(1000)==8&&
                WorldStreaming.PredictPrewarmLead(-1000)==-8,
                "Teleport or huge map never launches unbounded preloading");
            Check(WorldStreaming.PredictPrewarmLead(float.NaN)==0&&
                WorldStreaming.PredictPrewarmLead(float.PositiveInfinity)==0,
                "Bad camera input cannot corrupt streamed terrain bounds");
        }

        public static void SixthRingHiddenSuppliesAndLookoutsActuallyRevealMapSectors(Campaign data)
        {
            for(int room=0;room<8;room++)
            {
                GameCore g=new GameCore(data);g.save.room=room;
                var hidden=Array.Find(data.maps[room].objects,o=>o.kind=="farhidden");
                var vista=Array.Find(data.maps[room].objects,o=>o.kind=="farlookout");
                Check(hidden!=null&&vista!=null&&
                    g.CanStand(new Point(hidden.x,hidden.y))&&
                    g.CanStand(new Point(vista.x,vista.y)),
                    "Every sixth-ring secret must be physically accessible");
                Check(!g.Explored(room,83,75)&&!g.Explored(room,115,58),
                    "Remote viewpoints and hidden cache begin unexplored");
                Point outsideVista=StandableOutsideTalkReach(g,vista);
                int beforeVistaXp=g.save.xp;
                g.save.x=outsideVista.x;g.save.y=outsideVista.y;g.Talk(vista);g.Close();
                Check(!g.Has("far_vista_"+room)&&!g.Explored(room,83,75)&&
                    g.save.xp==beforeVistaXp,
                    "A lookout cannot award XP or reveal map sectors from outside physical reach");
                g.save.x=vista.x;g.save.y=vista.y;g.Talk(vista);g.Close();
                Check(g.Explored(room,83,75)&&g.Explored(room,80,70)&&
                    !g.Explored(room,115,58),
                    "Real lookout reveals a bounded surrounding region without discovering distant secrets");
                int coins=g.save.coins,resources=g.save.inventory[room%3];
                Point outsideHidden=StandableOutsideTalkReach(g,hidden);
                g.save.x=outsideHidden.x;g.save.y=outsideHidden.y;g.Talk(hidden);g.Close();
                Check(!g.Has("far_secret_"+room)&&g.save.coins==coins&&
                    g.save.inventory[room%3]==resources,
                    "An out-of-reach hidden-cache call cannot claim supplies or flag the secret");
                g.save.x=hidden.x;g.save.y=hidden.y;g.Talk(hidden);g.Close();
                Check(g.save.coins==coins+9&&g.save.inventory[room%3]==resources+2,
                    "Physically reaching the concealed supplies pays its actual one-time materials");
                g.Talk(hidden);g.Close();
                Check(g.save.coins==coins+9,"Hidden cache cannot duplicate rewards");
                GameCore loaded=new GameCore(data,g.save);
                Check(loaded.save.version==2&&loaded.Has("far_secret_"+room)&&
                    loaded.Has("far_vista_"+room)&&loaded.Explored(room,83,75),
                    "Hidden sixth-ring exploration survives legacy v2 save reload");
            }
        }

        public static void MapPreviewScalesToRealDynamicFloorAndRegionDimensions(Campaign data)
        {
            Check(Math.Abs(WorldStreaming.PreviewScale(120,80,306,207)-2.55f)<.001f,
                "Large sixth-ring map fits its world-map preview panel");
            Check(Math.Abs(WorldStreaming.PreviewScale(66,44,306,207)-
                (306f/66f))<.001f,
                "Runtime 66x44 abyss floor uses its real dimensions instead of old stub");
            Check(WorldStreaming.PreviewScale(0,44,306,207)==0,
                "Corrupt map dimensions cannot force an infinite texture allocation");
        }

        private static int BridgeDistance(GameCore g,int sx,int sy,int gx,int gy)
        {
            int w=g.Map.rows[0].Length,h=g.Map.rows.Length;
            var distances=new Dictionary<int,int>();var queue=new Queue<int>();
            int start=sy*w+sx;distances[start]=0;queue.Enqueue(start);
            int[,] directions={{1,0},{-1,0},{0,1},{0,-1}};
            while(queue.Count>0)
            {
                int current=queue.Dequeue(),x=current%w,y=current/w;
                if(x==gx&&y==gy)return distances[current];
                for(int k=0;k<4;k++)
                {
                    int xx=x+directions[k,0],yy=y+directions[k,1],at=yy*w+xx;
                    if(xx<2||yy<2||xx>=w-2||yy>=h-2||distances.ContainsKey(at)||
                        !g.CanStand(new Point(xx+.5f,yy+.5f)))continue;
                    distances[at]=distances[current]+1;queue.Enqueue(at);
                }
            }
            return -1;
        }
        public static void RepairedBridgesOpenRealTerrainAndShortenWalkingDistance(Campaign data)
        {
            for(int room=0;room<8;room++)
            {
                var g=new GameCore(data);g.save.room=room;
                var bridge=Array.Find(g.Map.objects,o=>o.id=="far_bridge_"+room);
                Check(bridge!=null&&g.CanStand(new Point(bridge.x,bridge.y)),
                    "All bridge repair posts are physically accessible");
                Check(g.Blocked(111.5f,52.5f),"Broken spans block actual collision");
                int longRoute=BridgeDistance(g,111,49,112,73);
                Check(longRoute>0,"The old long route is not destructively removed");
                g.save.inventory[0]=5;g.save.inventory[4]=5;g.save.x=bridge.x;g.save.y=bridge.y;
                g.Talk(bridge);
                int choice=Array.FindIndex(g.conversation.choices,c=>c.action=="far_bridge_repair");
                Check(choice>=0,"Bridge has a legitimate material-consuming interaction");
                g.Choose(choice);
                Check(g.FarBridgeOpen(room)&&!g.Blocked(111.5f,52.5f)&&
                    g.save.inventory[0]==3&&g.save.inventory[4]==4,
                    "One repair consumes the actual resources and unlocks actual terrain");
                int shortRoute=BridgeDistance(g,111,49,112,73);
                Check(shortRoute>0&&shortRoute<longRoute/2,
                    "Repair creates a physically shorter route, not a fake quest reward");
                g.Talk(bridge);
                Check(Array.FindIndex(g.conversation.choices,c=>c.action=="far_bridge_repair")<0,
                    "Bridge repair cannot duplicate materials or rewards");
                var reload=new GameCore(data,g.save);
                Check(reload.save.version==2&&reload.FarBridgeOpen(room),
                    "Old v2 save reload preserves the constructed bridge flag even if story gates redirect the player");
                // In an unprogressed test save the normal story gates may
                // return to the safe town; only test collision after entering
                // the matching author's region again.
                reload.save.room=room;
                Check(!reload.Blocked(111.5f,52.5f),
                    "Returning to the reconstructed region restores real walkable collision");
            }
        }

        public static void AllEightRepairedBridgesAppearInQuestTracking(Campaign data)
        {
            var g=new GameCore(data);
            Check(Array.IndexOf(GameCore.QuestIds,"far_bridges")>=0,
                "Real bridge construction has a separate selectable journal quest");
            Check(!g.GetQuest("far_bridges").complete,
                "A new save has never repaired any bridge");
            for(int room=0;room<8;room++)g.save.flags.Add("far_bridge_"+room);
            Check(g.FarBridgeCount==8&&g.GetQuest("far_bridges").complete,
                "All eight repaired routes finish the tracked journal");
        }

        public static void InstantTravelPreparesVisibleWorldWithoutColdChunkPopin(Campaign data)
        {
            Check(!WorldStreaming.LargeCameraJump(1,2),
                "Ordinary walking still uses softened chunk transitions");
            Check(WorldStreaming.LargeCameraJump(20,0)&&
                WorldStreaming.LargeCameraJump(-12,-6),
                "Cross-map and ridge jumps immediately prime visible props");
            Check(!WorldStreaming.LargeCameraJump(float.NaN,0)&&
                !WorldStreaming.LargeCameraJump(float.PositiveInfinity,2),
                "Bad camera state never triggers unbounded terrain work");
        }

        public static void BridgeParcelNeedsRealTravelAndOnlyRewardsDelivery(Campaign data)
        {
            for(int room=0;room<8;room++)
            {
                GameCore g=new GameCore(data);g.save.room=room;
                WorldObject bridge=Array.Find(g.Map.objects,o=>o.kind=="farbridge");
                WorldObject shelter=Array.Find(g.Map.objects,o=>o.kind=="farshelter");
                Check(bridge!=null&&shelter!=null,
                    "Courier actually uses existing physical bridge and refuge");
                if(room==0)
                {
                    g.enemies.Clear();g.save.inventory[0]=2;g.save.inventory[4]=1;
                    g.save.x=bridge.x;g.save.y=bridge.y;g.Talk(bridge);
                    int repair=Array.FindIndex(g.conversation.choices,c=>c.action=="far_bridge_repair");
                    Check(repair>=0,"An unrepaired physical bridge offers its authored repair");
                    Point outsideBridge=StandableOutsideTalkReach(g,bridge);
                    g.save.x=outsideBridge.x;g.save.y=outsideBridge.y;g.Choose(repair);g.Close();
                    Check(!g.FarBridgeOpen(room)&&g.save.inventory[0]==2&&g.save.inventory[4]==1,
                        "A stale bridge dialog cannot spend materials or repair after leaving interaction reach");
                    g.save.x=bridge.x;g.save.y=bridge.y;g.Talk(bridge);
                    repair=Array.FindIndex(g.conversation.choices,c=>c.action=="far_bridge_repair");
                    g.Choose(repair);g.Close();
                    Check(g.FarBridgeOpen(room)&&g.save.inventory[0]==0&&g.save.inventory[4]==0,
                        "Repairing at the bridge consumes real materials and opens the physical crossing");
                }
                g.save.x=bridge.x;g.save.y=bridge.y;
                g.Talk(bridge);
                Check(Array.FindIndex(g.conversation.choices,c=>c.action=="far_courier_start")<0,
                    "Unbuilt bridge or shelter cannot create supply rewards");
                g.Close();g.save.flags.Add("far_bridge_"+room);
                g.save.flags.Add("far_shelter_"+room);
                int coins=g.save.coins,shards=g.save.shards;
                float unloadedSpeed=g.Speed;
                g.Talk(bridge);
                int choose=Array.FindIndex(g.conversation.choices,c=>c.action=="far_courier_start");
                Check(choose>=0,"Repaired bridge offers a physical parcel trip");
                g.Choose(choose);
                Check(g.FarCourierActive&&g.Speed<unloadedSpeed,
                    "A real carried parcel visibly affects character speed");
                g.save.x=shelter.x;g.save.y=shelter.y;
                g.Talk(shelter);g.Close();
                Check(!g.FarCourierDelivered(room)&&g.save.coins==coins,
                    "Teleporting to the shelter cannot fake the foot journey");
                g.Tick(.02f,new Point());
                Check(!g.FarCourierActive&&!g.FarCourierDelivered(room),
                    "Long position jump invalidates transient cargo");
                g.save.x=bridge.x;g.save.y=bridge.y;
                g.Talk(bridge);
                choose=Array.FindIndex(g.conversation.choices,c=>c.action=="far_courier_start");
                g.Choose(choose);
                for(int step=0;step<100&&g.save.y<61.35f;step++)
                    g.Tick(.045f,new Point(0,1));
                Check(g.FarCourierActive&&g.FarCourierCrossed,
                    "Engine ticks record actual southbound contact with rebuilt bridge tiles");
                WorldObject post=Array.Find(g.Map.objects,o=>o.kind=="farcheck");
                Check(post!=null&&g.CanStand(new Point(post.x,post.y))&&
                    (g.Position-new Point(post.x,post.y)).Length<2.2f,
                    "Intermediate cargo inspection is a reachable real-world station");
                g.Talk(post);
                int seal=Array.FindIndex(g.conversation.choices,
                    c=>c.action=="far_courier_stamp");
                Check(seal>=0,"Checkpoint offers a genuine physical seal action");
                g.Choose(seal);
                Check(g.FarCourierStamped,"Civilian inspector actually stamps the carried cargo");
                for(int step=0;step<110&&g.save.y<73.2f;step++)
                    g.Tick(.045f,new Point(0,1));
                Check((g.Position-new Point(shelter.x,shelter.y)).Length<2.2f,
                    "Normal real movement reaches the distant shelter before timeout");
                g.Talk(shelter);
                choose=Array.FindIndex(g.conversation.choices,c=>c.action=="far_courier_deliver");
                Check(choose>=0,"Actual refuge dialogue offers parcel delivery");
                g.Choose(choose);
                Check(g.FarCourierDelivered(room)&&!g.FarCourierActive&&
                    g.save.coins==coins+25&&g.save.shards==shards+1,
                    "Only legitimate foot delivery grants one real permanent reward");
                GameCore reload=new GameCore(data,g.save);
                Check(reload.save.version==2&&reload.FarCourierDelivered(room)&&
                    !reload.FarCourierActive,
                    "Old save v2 restores delivery only, never an invented held parcel");
                g.Talk(shelter);g.Close();
                Check(g.save.coins==coins+25,"Parcel delivery rewards cannot repeat");
            }
            GameCore all=new GameCore(data);
            for(int i=0;i<8;i++)all.save.flags.Add("far_courier_"+i);
            Check(!all.GetQuest("far_couriers").complete&&all.FarCourierDeliveredCount==8,
                "Eight delivered parcels still require formal publication at the existing Inn");
        }

        public static void PublicParcelCharterRequiresEveryGenuineDeliveryAndImprovesShelters(Campaign data)
        {
            GameCore g=new GameCore(data);
            g.save.flags.Add("far_refuge_atlas");
            g.save.room=9;
            WorldObject desk=Array.Find(g.Map.objects,o=>o.kind=="faratlasdesk");
            Check(desk!=null,"Original inn atlas remains an actual interactable desk");
            g.save.x=desk.x;g.save.y=desk.y;
            g.Talk(desk);
            Check(Array.FindIndex(g.conversation.choices,c=>c.action=="far_courier_charter")<0,
                "Cannot publish civic supply charter without physical deliveries");
            g.Close();
            for(int room=0;room<8;room++)g.save.flags.Add("far_courier_"+room);
            int coins=g.save.coins;
            g.Talk(desk);
            int option=Array.FindIndex(g.conversation.choices,
                c=>c.action=="far_courier_charter");
            Check(option>=0,"Completing all deliveries unlocks public filing at the actual inn");
            g.Choose(option);
            Check(g.Has("far_courier_charter")&&g.save.coins==coins+65&&
                g.GetQuest("far_couriers").complete,
                "Real public filing grants a single permanent network upgrade");
            GameCore reload=new GameCore(data,g.save);
            Check(reload.save.version==2&&reload.Has("far_courier_charter"),
                "Completed charter remains in old SaveData v2 after reload");
            g.Talk(desk);
            Check(Array.FindIndex(g.conversation.choices,c=>c.action=="far_courier_charter")<0&&
                g.save.coins==coins+65,
                "Publishing civic accounts twice never duplicates rewards");
            g.Close();
            g.save.room=0;g.save.flags.Add("far_shelter_0");
            g.save.flags.Add("far_dial_0"); // a shelter cannot be built without its earlier real calibration
            WorldObject shelter=Array.Find(g.Map.objects,o=>o.kind=="farshelter");
            g.save.x=shelter.x;g.save.y=shelter.y;
            g.save.inventory[0]=3;g.save.health=Math.Max(1,g.save.maxHealth-75);
            g.save.energy=8;g.save.cold=60;
            g.Talk(shelter);
            option=Array.FindIndex(g.conversation.choices,c=>c.action=="far_rest");
            Check(option>=0,"Existing public refuge still offers a repeatable physical rest");
            g.Choose(option);
            Check(g.save.energy==100&&g.save.cold==20&&
                g.save.health>=g.save.maxHealth-20&&g.save.inventory[0]==2,
                "Charter materially improves real healing and cold relief for the old wood price");
        }

        public static void NewTrailRamblingsKeepSaveV2AndPermanentRewards(Campaign data)
        {
            for (int room = 0; room < 8; room++)
            {
                MapData map = data.maps[room];
                Check(map.rows.Length == 80 && map.rows[0].Length == 120,
                    "Outdoor sixth-ring dimensions must be 120x80 in room " + room);
                foreach (string row in map.rows)
                    Check(row.Length == 120, "Every terrain row remains rectangular");
                Check(map.rows[13][17] == '=' && map.rows[14][18] == '=',
                    "Original central campaign road is retained");
                foreach (WorldObject o in map.objects)
                    if (o.kind == "waytrail")
                    {
                        Check(o.x >= 14 && o.y >= 22,
                            "New exploration marker stays beyond the old central authored area");
                        int x = (int)o.x, y = (int)o.y;
                        Check(map.rows[y][x] != '#' && map.rows[y][x] != '~' &&
                            map.rows[y][x] != 'f', "Trail marker is not walled into impassable terrain");
                    }
                Check(Array.FindAll(map.objects, o => o.kind == "waytrail").Length == 2,
                    "Each outdoor region gets two distinct readable waypoints");
            }
            GameCore game = new GameCore(data);
            WorldObject landmark = Array.Find(data.maps[0].objects, o => o.id == "waytrail0s");
            int coins = game.save.coins, shards = game.save.shards;
            game.save.x = 18f; game.save.y = 18f;
            game.Talk(landmark);
            Check(!game.Has("waytrail_waytrail0s") &&
                game.save.coins == coins && game.save.shards == shards,
                "A direct remote Talk call cannot discover a waypoint or grant its reward");
            game.Close();
            game.save.x = landmark.x; game.save.y = landmark.y;
            game.Talk(landmark);
            Check(game.Has("waytrail_waytrail0s") &&
                game.save.coins == coins + 8 && game.save.shards == shards + 1,
                "Recording a physically reached waypoint grants a one-time survey reward");
            game.Close(); game.Talk(landmark);
            Check(game.save.coins == coins + 8 && game.save.shards == shards + 1,
                "Rereading a waypoint never duplicates rewards");
            SaveData stored = game.save;
            Check(stored.version == 2, "Waypoint flags must not alter SaveData version");
            // Annex fog has independent 6x6 flags; legacy 24-bit interior
            // exploration remains unchanged after the world expands.
            game.Close();
            game.save.x = 14.5f; game.save.y = 30.5f;
            Check(!game.Explored(0, 14, 30), "Unknown annex stays fogged before walking there");
            game.Tick(.02f, new Point());
            Check(game.Explored(0, 14, 30) && !game.Explored(0, 47, 23),
                "Visiting one annex must not reveal the distant eastern branch");
            GameCore loaded = new GameCore(data, stored);
            Check(loaded.Explored(0, 14, 30) && !loaded.Explored(0, 47, 23),
                "Annex exploration persists in existing save string flags");
            Check(loaded.Has("waytrail_waytrail0s") &&
                loaded.save.coins == coins + 8 && loaded.save.shards == shards + 1,
                "Waypoint discovery and rewards survive a normal save reload");
        }

        public static void ChapterEightRoadCharterUsesNewMapsAndChangesActualProgression(Campaign data)
        {
            var g = new GameCore(data);
            g.mode = "world";
            g.save.stage = 3; g.save.ending = 1; g.save.level = 10;
            WorldObject guild = Array.Find(data.maps[0].objects, o => o.kind == "guild");
            g.save.x = guild.x; g.save.y = guild.y;
            g.Talk(guild);
            Check(Array.FindIndex(g.conversation.choices, c => c.action == "roadcharter_guild") < 0,
                "Chapter Eight remains locked before the Chapter Seven hearing");
            g.Close();
            g.save.flags.Add("hearing_complete");
            g.save.flags.Add("hearing_orrin_aid");
            Check(g.RoadCharterReady && !g.RoadCharterStarted, "Signed hearing unlocks the road chapter");
            g.Talk(guild);
            int idx = Array.FindIndex(g.conversation.choices, c => c.action == "roadcharter_guild");
            Check(idx >= 0, "Guild presents a new chapter without removing prior dialogue choices");
            g.Choose(idx);
            Check(g.mode == "dialog", "Chapter Eight introduction is playable dialogue");
            idx = Array.FindIndex(g.conversation.choices, c => c.action == "roadcharter_begin");
            Check(idx >= 0, "Lyra offers real expedition acceptance");
            g.Choose(idx);
            Check(g.RoadCharterStarted && g.save.trackedQuest == "roadcharter" &&
                g.RoadCharterReports == 0, "Starting new main chapter records a trackable objective");
            int preCoins = g.save.coins, preShards = g.save.shards;
            for (int room = 1; room <= 3; room++)
            {
                WorldObject marker = Array.Find(data.maps[room].objects,
                    o => o.id == "waytrail" + room + "e");
                g.save.room = room; g.save.x = 18f; g.save.y = 18f;
                g.mode = "world";
                g.Talk(marker);
                Check(g.RoadCharterReports == room - 1 &&
                    !g.Has("roadcharter_report_" + room) &&
                    !g.Has("waytrail_" + marker.id),
                    "A distant direct Talk call cannot submit Chapter Eight or survey evidence");
                g.Close();
                g.save.x = marker.x; g.save.y = marker.y;
                g.Talk(marker);
                Check(g.RoadCharterReports == room &&
                    g.Has("roadcharter_report_" + room),
                    "Physical eastern survey advances exactly one regional main-story report");
                g.Close();
                g.Talk(marker); g.Close();
                Check(g.RoadCharterReports == room, "Rereading one marker cannot duplicate evidence");
            }
            Check(g.GetQuest("roadcharter").room == 0 &&
                g.GetQuest("roadcharter").target.x == guild.x,
                "Three surveyed regions route player back to Lyra");
            g.save.room = 0; g.save.x = guild.x; g.save.y = guild.y;
            g.Talk(guild); idx = Array.FindIndex(g.conversation.choices, c => c.action == "roadcharter_guild");
            Check(idx >= 0, "Guild retains Chapter Eight resolution");
            g.Choose(idx);
            float priorSpeed = g.Speed;
            idx = Array.FindIndex(g.conversation.choices, c => c.action == "roadcharter_public");
            Check(idx >= 0, "Civilian priority is a real player decision");
            g.Choose(idx);
            Check(g.RoadCharterComplete && g.Has("roadcharter_public") &&
                Math.Abs(g.Speed - priorSpeed - .25f) < .001f &&
                g.save.coins == preCoins + 3 * 8 + 45 &&
                g.save.shards == preShards + 3 + 3,
                "Chapter Eight's first ending rewards once and grants a real speed upgrade");
            SaveData snapshot = g.save;
            var loaded = new GameCore(data, snapshot);
            Check(snapshot.version == 2 && loaded.RoadCharterComplete &&
                loaded.Has("roadcharter_public") && loaded.RoadCharterReports == 3,
                "Road charter and collected testimony persist without a SaveData migration");
            g.Talk(guild);
            idx = Array.FindIndex(g.conversation.choices, c => c.action == "roadcharter_guild");
            g.Choose(idx); g.Close();
            Check(g.save.coins == snapshot.coins && g.save.shards == snapshot.shards,
                "Rereading completed public roads cannot farm the main-story payout");

            var reserve = new GameCore(data);
            reserve.save.flags.Add("hearing_complete");
            reserve.save.flags.Add("hearing_orrin_patrol");
            reserve.save.flags.Add("roadcharter_started");
            for (int room = 1; room <= 3; room++)
                reserve.save.flags.Add("roadcharter_report_" + room);
            reserve.save.stage = 3; reserve.save.ending = 1; reserve.save.level = 10;
            reserve.save.x = guild.x; reserve.save.y = guild.y;
            reserve.mode = "world";
            float speedBefore = reserve.Speed;
            reserve.Talk(guild);
            idx = Array.FindIndex(reserve.conversation.choices, c => c.action == "roadcharter_guild");
            reserve.Choose(idx);
            idx = Array.FindIndex(reserve.conversation.choices, c => c.action == "roadcharter_reserve");
            reserve.Choose(idx);
            Check(reserve.Has("roadcharter_reserve") && reserve.RoadCharterComplete &&
                Math.Abs(reserve.Speed - speedBefore) < .001f &&
                reserve.save.version == 2,
                "Alternative ending grants a distinct choice without quietly changing movement");
            float startEnergy = 15f;
            reserve.save.energy = startEnergy;
            // The production simulation deliberately clamps each frame to
            // 50 ms. Ten ordinary frames, not one artificial 500 ms tick,
            // must accumulate half a second of real regeneration.
            for (int frame = 0; frame < 10; frame++)
                reserve.Tick(.05f, new Point());
            Check(reserve.save.energy >= startEnergy + 8.4f &&
                reserve.save.energy < startEnergy + 8.7f,
                "Emergency-road charter adds +3 per second to real frame-by-frame stamina recovery");
        }

        public static void RidgeCrossingRejectsLiveEscortAndRemoteCombatThreats(Campaign data)
        {
            var g=Fresh(data,0);g.Enter(0);g.enemies.Clear();
            g.save.flags.Add("ridge_seen_0_0");
            g.save.flags.Add("ridge_seen_0_1");
            WorldObject north=Object(g,"ridgemarker");
            WorldObject east=null,west=null;
            foreach(WorldObject o in g.Map.objects)
            {
                if(o.id=="ridge_route_0_0")east=o;
                if(o.id=="ridge_route_0_1")west=o;
            }
            Check(north!=null&&east!=null&&west!=null&&g.RidgeLinked(0),
                "Original linked ridge uses two distinct real map marker objects");
            g.save.x=west.x;g.save.y=west.y;g.save.energy=95;
            float energy=g.save.energy;
            var choose=typeof(GameCore).GetMethod("ChooseRidge",
                System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.NonPublic);
            Check(choose!=null,"Original world crossing has an authoritative action handler");
            var shot=new Projectile{position=new Point(east.x,east.y),
                velocity=new Point(1,0),damage=8,life=12,friendly=false,source="shot"};
            g.projectiles.Add(shot);
            choose.Invoke(g,new object[]{"ridge_travel"});
            Check((g.Position-new Point(west.x,west.y)).Length<.01f&&
                g.save.energy==energy,
                "Incoming live enemy projectile at the destination forbids unsafe remote travel");
            g.projectiles.Remove(shot);
            var warning=new Hazard{position=new Point(east.x,east.y),
                radius=1.2f,timer=4f,damage=15,kind="rift"};
            g.hazards.Add(warning);
            choose.Invoke(g,new object[]{"ridge_travel"});
            Check((g.Position-new Point(west.x,west.y)).Length<.01f&&
                g.save.energy==energy,
                "A still-armed delayed blast at the landing also blocks ridge travel");
            g.hazards.Remove(warning);
            var enemy=new Enemy(92921,0,east.x+1,east.y,70){timer=90};
            g.enemies.Add(enemy);
            choose.Invoke(g,new object[]{"ridge_travel"});
            Check((g.Position-new Point(west.x,west.y)).Length<.01f&&
                g.save.energy==energy,
                "An actual hostile defender at destination blocks shortcut traversal");
            g.enemies.Clear();
            choose.Invoke(g,new object[]{"ridge_travel"});
            Check((g.Position-new Point(east.x,east.y)).Length<.01f&&
                g.save.energy==energy-12,
                "Once the far end is truly safe, the original paid crossing still works");

            // A separate original Noll course is a genuine field mission,
            // even when ordinary enemies and both ridge endpoints are safe.
            var training=Fresh(data,0);training.Enter(0);training.enemies.Clear();
            training.save.flags.Add("ridge_seen_0_0");
            training.save.flags.Add("ridge_seen_0_1");
            WorldObject forge=Object(training,"forge");
            training.save.x=forge.x;training.save.y=forge.y;
            Advance(training,1.1f);training.save.energy=100;
            Action(training,"forge","swordlesson_start");
            Check(training.SwordLessonActive,
                "The actual blacksmith opens a timed class-only projectile lesson");
            training.save.x=west.x;training.save.y=west.y;
            energy=training.save.energy;
            choose.Invoke(training,new object[]{"ridge_travel"});
            Check(training.SwordLessonActive&&
                (training.Position-new Point(west.x,west.y)).Length<.01f&&
                training.save.energy==energy,
                "Even a temporarily nearby shortcut cannot bypass or erase a running live training clock");
            training.PrepareSave();var copy=new GameCore(data,CloneSave(training.save));
            Check(copy.save.version==2&&copy.RidgeLinked(0)&&!copy.SwordLessonActive,
                "SaveData v2 preserves the earned linked route without serializing timed drills");
        }

    }
}
