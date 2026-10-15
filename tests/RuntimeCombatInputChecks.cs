// Runs actual FrontierRuntime.Update/OnGUI against controllable Unity declarations.
// This is an offline input regression harness, not Unity Player verification.
using System;
using System.Reflection;
using System.IO;
using System.Text;
using System.Globalization;
using Starfall;
using UnityEngine;

public static class RuntimeCombatInputChecks
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    private static void Check(bool value, string message)
    { if (!value) throw new Exception(message); }

    private static object Call(FrontierRuntime app, string method, params object[] args)
    { return typeof(FrontierRuntime).GetMethod(method, Private).Invoke(app, args); }

    private static void Set(FrontierRuntime app, string field, object value)
    { typeof(FrontierRuntime).GetField(field, Private).SetValue(app, value); }

    private static void Prepare(FrontierRuntime app, int profession = 1)
    {
        Input.Clear(); GUI.queuedClick = null;
        GUI.commands.Clear(); GUI.textureCalls.Clear(); GUI.buttonCalls.Clear();
        Screen.width = 1280; Screen.height = 800; Time.unscaledDeltaTime = .02f;
        app.BeginVisualReview(profession, 1); app.VisualReviewPaused = false;
        var game = app.Game;
        game.enemies.Clear(); game.save.companion = -1; game.save.reducedMotion = true;
        game.invulnerable = 100; game.save.x = 12; game.save.y = 14;
        game.save.energy = 100; game.toastTime = 0;
        Set(app, "camera", game.Position); Set(app, "controllerUsed", false);
        Set(app, "pointerSeen", false); Set(app, "combatHudTime", 0f);
        Set(app, "applicationPaused", false); Call(app, "Resize");
        Mouse(app, game.Position + new Point(1, 0) * 4);
    }

    private static void Mouse(FrontierRuntime app, Point target)
    {
        Point screen = (Point)Call(app, "ScreenPoint", target);
        Input.mousePosition = new Vector3(screen.x, Screen.height - screen.y, 0);
    }

    private static void Update(FrontierRuntime app)
    { Call(app, "Update"); Input.keys.Clear(); }

    private static void Frames(FrontierRuntime app, int count)
    { for (int frame = 0; frame < count; frame++) Update(app); }

    private static void HoldAttack(int source)
    {
        if (source == 0) Input.held.Add(KeyCode.J);
        else if (source == 1) Input.buttons.Add(0);
        else Input.held.Add(KeyCode.JoystickButton2);
    }

    private static void FreshTechniqueWinsHeldAttack(FrontierRuntime app)
    {
        for (int profession = 0; profession < 3; profession++)
        for (int source = 0; source < 3; source++)
        {
            Prepare(app, profession); HoldAttack(source);
            app.Game.SetAim(new Point(1, 0));
            Input.keys.Add(source == 2 ? KeyCode.JoystickButton3 : KeyCode.Q);
            Update(app);
            ActorAction cast = app.Game.playerAction;
            Check(cast != null && cast.kind == "skill", "Fresh Q/Y must beat a held attack in the same Update (class " + profession + ", input " + source + ")");
            Check(Math.Abs(app.Game.save.energy - 60) < .001f, "The winning technique spends energy exactly once");
            bool sawActive = false;
            for (int frame = 0; frame < 18; frame++)
            {
                Update(app);
                Check(app.Game.playerAction == cast, "Held attack must preserve the Q windup and full contact pose");
                if (app.Game.PlayerPhase == ActionPhase.Active) sawActive = true;
            }
            Check(cast.released && sawActive, "The real technique reaches its release and Active phase while attack remains held");
        }
        Console.WriteLine("PASS Fresh keyboard Q, mouse+Q and controller Y win held attacks for all classes; full Q contact pose is retained");
    }

    private static ActorAction StartBowAttack(FrontierRuntime app)
    {
        Input.held.Add(KeyCode.J); Update(app); Input.held.Clear();
        ActorAction attack = app.Game.playerAction;
        Check(attack != null && attack.kind == "attack", "The production keyboard path must start the setup attack");
        return attack;
    }

    private static void QueuedTechniqueKeepsAimAndRunsOnce(FrontierRuntime app)
    {
        Prepare(app); ActorAction attack = StartBowAttack(app);
        Frames(app, 9);
        Point target = app.Game.Position + new Point(0, -4);
        Mouse(app, target); Input.keys.Add(KeyCode.Q); Input.held.Add(KeyCode.J); Update(app);
        Check(app.Game.playerAction == attack && app.Game.skillCooldown <= 0 && Math.Abs(app.Game.save.energy - 100) < .001f,
            "Q near attack recovery queues without cancelling the attack or spending energy early");
        Mouse(app, app.Game.Position + new Point(0, 4));
        ActorAction cast = null; int starts = 0;
        for (int frame = 0; frame < 230; frame++)
        {
            ActorAction before = app.Game.playerAction; Update(app);
            ActorAction after = app.Game.playerAction;
            if (after != before && after != null && after.kind == "skill")
            {
                starts++; cast = after;
                Check(cast.direction.y < -.999f && Math.Abs(cast.direction.x) < .001f,
                    "Queued Q retains the pressed direction after the cursor moves");
                Check(cast.target.HasValue && (cast.target.Value - target).Length < .001f,
                    "Queued Q retains the original world target rather than using the live cursor");
            }
        }
        Check(cast != null && cast.released && starts == 1,
            "One Q press must be consumed once even when attacks stay held beyond the entire skill cooldown");
        Console.WriteLine("PASS Q queues near attack recovery, captures direction/target and consumes one input once");
    }

    private static void DashAndTechniqueShareAFrame(FrontierRuntime app)
    {
        Prepare(app); Input.axes["Horizontal"] = 1; Input.held.Add(KeyCode.D);
        Mouse(app, app.Game.Position + new Point(0, -4));
        Input.keys.Add(KeyCode.Space); Input.keys.Add(KeyCode.Q); Input.held.Add(KeyCode.J);
        Update(app);
        Check(app.Game.dashTime > 0 && app.Game.playerAction == null && app.Game.skillCooldown <= 0,
            "Same-frame dodge+Q must start the dodge and retain a fresh technique intent");
        Check(Math.Abs(app.Game.save.energy - 80) < .001f, "Only the dodge is charged before queued Q starts");
        ActorAction cast = null;
        for (int frame = 0; frame < 12 && cast == null; frame++)
        {
            Update(app);
            if (app.Game.playerAction != null && app.Game.playerAction.kind == "skill") cast = app.Game.playerAction;
        }
        Check(cast != null && app.Game.dashTime <= 0 && cast.direction.y < -.999f,
            "Queued Q starts after the real dodge, before held J can take over");
        Console.WriteLine("PASS Same-frame dodge+Q queues and casts after the dodge with held attack priority preserved");
    }

    private static void HudTechniqueCanQueue(FrontierRuntime app)
    {
        foreach (bool compact in new[] { false, true })
        {
            Prepare(app); ActorAction attack = StartBowAttack(app); Frames(app, 8);
            Point target = app.Game.Position + new Point(0, -4);
            Mouse(app, target); Update(app);
            Rect slot = compact ? new Rect(430, 732, 78, 50) : new Rect(354, 699, 109, 75);
            Input.mousePosition = new Vector3(slot.x + 10, Screen.height - slot.y - 10, 0);
            Set(app, "combatHudTime", compact ? 3f : 0f); GUI.queuedClick = slot;
            GUI.commands.Clear(); GUI.textureCalls.Clear(); GUI.buttonCalls.Clear(); Call(app, "OnGUI");
            Check(GUI.queuedClick == null, "Actual " + (compact ? "combat" : "exploration") + " Q HUD control remains clickable during an ordinary attack");
            Check(app.Game.playerAction == attack && app.Game.skillCooldown <= 0,
                "Clicking the Q HUD during windup queues without cancelling contact or spending early");
            for (int frame = 0; frame < 12 && app.Game.playerAction == attack; frame++) Update(app);
            ActorAction cast = app.Game.playerAction;
            Check(cast != null && cast.kind == "skill" && cast.target.HasValue && (cast.target.Value - target).Length < .001f,
                "HUD Q queue starts with the last world aim instead of the HUD screen position");
        }
        GUI.queuedClick = null; GUI.commands.Clear(); GUI.textureCalls.Clear(); GUI.buttonCalls.Clear();
        Console.WriteLine("PASS Both actual Q HUD layouts queue during ordinary attack and preserve world aim");
    }

    private static void MenuAndApplicationPauseDiscardIntent(FrontierRuntime app)
    {
        foreach (string boundary in new[] { "pause", "inventory", "application", "focus" })
        {
            Prepare(app); ActorAction attack = StartBowAttack(app); Frames(app, 9);
            Input.keys.Add(KeyCode.Q); Update(app);
            Check(app.Game.playerAction == attack && app.Game.skillCooldown <= 0, "The boundary test begins with an unspent queued Q");
            if (boundary == "application")
            {
                Call(app, "OnApplicationPause", true); Frames(app, 20); Call(app, "OnApplicationPause", false);
            }
            else if (boundary == "focus")
            {
                Call(app, "OnApplicationFocus", false);
                Check(app.Game.mode == "pause",
                    "Real window focus loss pauses the active game");
                Frames(app, 20);
                Call(app, "OnApplicationFocus", true);
                app.Game.Close();
            }
            else
            {
                Input.keys.Add(boundary == "pause" ? KeyCode.Escape : KeyCode.I); Update(app);
                Check(app.Game.mode == boundary, "Actual runtime input opens the requested menu boundary");
                Frames(app, 20); app.Game.Close();
            }
            Frames(app, 30);
            Check(app.Game.skillCooldown <= 0 && app.Game.playerAction == null && Math.Abs(app.Game.save.energy - 100) < .001f,
                "A queued technique must not launch or charge energy after returning from " + boundary);
        }
        Console.WriteLine("PASS Runtime pause, inventory, application pause and window focus loss discard pending Q intent");
    }

    private static readonly Point[] ActorDirections = {
        new Point(1, 0), new Point(-1, 0), new Point(0, -1), new Point(0, 1)
    };

    private static int DrawnFrame(GUI.TextureCall draw)
    {
        float left = Math.Min(draw.uv.x, draw.uv.x + draw.uv.width);
        int column = (int)Math.Floor(left * draw.texture.width / 256 + .0001f);
        int row = (int)Math.Floor((1 - draw.uv.y - draw.uv.height) * draw.texture.height / 256 + .0001f);
        return row * 4 + column;
    }

    private static void CheckPlayerDrawing(FrontierRuntime app, string sheet, int frame, Point direction, bool head = true)
    {
        GUI.commands.Clear(); GUI.textureCalls.Clear(); GUI.buttonCalls.Clear();
        Call(app,"DrawPlayerActor");
        int profession=app.Game.AppearanceWeapon;
        string klass=new[]{"Vanguard","Ranger","Arcanist"}[profession];
        int facing=GameCore.DirectionRow(direction);
        int pose=app.Game.PlayerPose;
        // Rendering (as opposed to physics) never switches a still-active
        // attack/cast body to Run merely because recovery allows movement.
        bool locomotion=pose>=2&&pose<=3;
        int action=locomotion?1:app.Game.HeroAnimationState;
        string animation=new[]{"Idle","Run","Attack","Cast","Dodge","Hit","Death"}[action];
        string dir=new[]{"E","N","S"}[facing];
        string prefix="resource:Frontier/HeroAnimator/Canonical/Hero_"+
            klass+"_"+animation+"_"+dir;
        var rendered=GUI.textureCalls.Find(draw=>
            draw.texture.name.StartsWith("resource:Frontier/HeroAnimator/Canonical/Hero_")
            || draw.texture.name.StartsWith("resource:Frontier/Art/Player")
            || draw.texture.name.StartsWith("resource:Frontier/Art/Walk"));
        Check(rendered!=null,"Game world must render a visible full-body hero");
        {
            Check(rendered.texture.name==prefix,
                "Production player used another class/animation/facing: expected "+prefix+
                " actual "+rendered.texture.name);
            int authored=locomotion?4:app.Game.HeroAnimationFrameCount(facing);
            bool attackSix=(action==2||action==3)&&authored==4;
            bool reactionFive=(action==4||action==5)&&authored==3;
            int count=attackSix?6:reactionFive?5:authored;
            float phase=locomotion?app.Game.WalkPosePhase:
                app.Game.HeroAnimationPlaybackPosition(facing);
            float progress=attackSix?(phase<2f?Math.Min(2f,phase*2f):phase*2f-1f):
                reactionFive?phase*2f:phase;
            if(attackSix&&app.Game.playerAction!=null&&
                !app.Game.playerAction.released)
                progress=Math.Min(progress,2.60f);
            if(action==1)progress%=count;
            int expected=Math.Max(0,Math.Min(count-1,(int)Math.Floor(progress+.20f)));
            Check(DrawnFrame(rendered)==expected,
                "Full-body frame must match authoritative release/dash/death clock");
        }
        Check(Math.Abs(rendered.rect.width-192)<.01f&&
            Math.Abs(rendered.rect.height-192)<.01f,
            "Full-body pixel density must stay identical in every state");
        var parts=GUI.textureCalls.FindAll(t=>t.texture.name==
            "resource:Frontier/Art/WalkVanguard");
        Check(parts.Count==0,
            "The real hero must never draw a separate warrior head/neck overlay");
    }

    private static void BeginCrossEquippedQ(FrontierRuntime app, int equipped, Point direction)
    {
        Prepare(app, 0); app.Game.Equip(equipped);
        Mouse(app, app.Game.Position + direction * 4); Input.keys.Add(KeyCode.Q); Update(app);
        Check(app.Game.playerAction != null && app.Game.playerAction.kind == "skill" && app.Game.playerAction.weapon == 0,
            "Cross-equipped Vanguard Q still starts its class skill");
    }

    private static void ReleasedTechniqueDrawsContactFrame(FrontierRuntime app)
    {
        for (int profession = 0; profession < 3; profession++)
        foreach (Point direction in ActorDirections)
        {
            Prepare(app, profession); Mouse(app, app.Game.Position + direction * 4);
            Input.keys.Add(KeyCode.Q); Update(app); Frames(app, 14);
            ActorAction action = app.Game.playerAction;
            Check(action != null && action.released, "Fourteen real 20ms ticks reach Q's contact event");
            Check(app.Game.PlayerPhase == ActionPhase.Active && app.Game.PlayerPose == 7,
                "The exact released contact tick must display Active/release rather than preparation");
            int row = GameCore.DirectionRow(direction);
            string sheet = row == 1 ? "PlayerRear" : row == 2 ? "PlayerSouth"
                : "Player" + new[] { "Vanguard", "Ranger", "Arcanist" }[profession];
            CheckPlayerDrawing(app, sheet, row == 0 ? 7 : profession * 4 + 3, direction);
        }
        Console.WriteLine("PASS Actual Q contact rendering at the 20ms float boundary for all class/direction selections");
    }

    private static void DirectionalLmbAndSkillContactNeverFlashBeforeRelease(
        FrontierRuntime app)
    {
        // The old six-cell selector exposed contact frame #3 before
        // ActorAction.released: it rounded a late windup near position 2.8
        // into contact via +.20. Test both real actor Sprite samples and
        // the exact GameCore physical release, including cross-class skills.
        MethodInfo resolver=typeof(FrontierRuntime).GetMethod(
            "ResolveFullBodyHeroFrame",Private);
        Check(resolver!=null,"Missing real renderer frame selector");
        int audited=0;
        foreach(int vocation in new[]{0,1,2})
        foreach(int equipment in new[]{0,1,2})
        foreach(int command in new[]{0,1})
        foreach(Point aim in new[]{new Point(1,0),new Point(-1,0),
                                   new Point(0,-1),new Point(0,1)})
        {
            Prepare(app,vocation);
            GameCore g=app.Game;
            if(equipment>0&&!g.Has("weapon_"+equipment))
                g.save.flags.Add("weapon_"+equipment);
            g.Equip(equipment);
            Check(g.save.weapon==equipment,"Cross-equipment setup failed");
            g.SetAim(aim);
            if(command==0)g.Attack(aim);else g.Skill(aim);
            ActorAction selected=g.playerAction;
            Check(selected!=null&&!selected.released,
                "Actual LMB/Q must begin in the windup pose");
            int facing=GameCore.DirectionRow(aim);
            int expectedAction=command==0?2:3;
            for(int stage=0;stage<2;stage++)
            {
                if(stage==1)selected.elapsed=
                    Math.Max(0,selected.contact-.005f);
                object[] args={false,facing,0,0,0,0f};
                Check((bool)resolver.Invoke(app,args),"No published windup");
                int state=(int)args[2],count=(int)args[3],
                    frame=(int)args[4];
                Check(state==expectedAction&&count==6&&frame<=2,
                    "PRE_RELEASE_CONTACT_FLASH: "+vocation+"/"+equipment+
                    "/"+command+"/"+facing+"/"+frame);
                Check(g.AppearanceWeapon==equipment,
                    "Windup changed outfit instead of obeying equipment");
                CheckPlayerDrawing(app,"",0,aim);
                audited++;
            }
            // Advance REAL combat simulation across the damage boundary.
            g.Tick(.01f,new Point());
            Check(selected.released&&g.playerAction==selected,
                "Real damage/contact boundary was not reached");
            object[] released={false,facing,0,0,0,0f};
            Check((bool)resolver.Invoke(app,released),"No contact frame");
            Check((int)released[2]==expectedAction&&(int)released[4]==3,
                "Actual first released keypose must be whole-body contact #3");
            Check(g.AppearanceWeapon==equipment,
                "Released body changed equipment instead of skill mechanics");
            CheckPlayerDrawing(app,"",0,aim);
            audited++;
        }
        Check(audited==216,"All 216 E/W/N/S LMB/Q first/late/release "
            +"frame probes must execute");
        Console.WriteLine("PASS 216 E/W/N/S LMB/Q first, last windup "
            +"and real released contact frames retain one whole-body identity");
    }

    private static void MoveDuringQRecovery(FrontierRuntime app, Point direction)
    {
        Input.axes["Horizontal"] = direction.x; Input.axes["Vertical"] = -direction.y;
        Input.held.Add(Math.Abs(direction.x) > 0 ? direction.x > 0 ? KeyCode.D : KeyCode.A
            : direction.y < 0 ? KeyCode.W : KeyCode.S);
        Update(app);
        Check(app.Game.playerAction != null && app.Game.PlayerPhase == ActionPhase.Recovery && app.Game.ActuallyMoving,
            "The real Q recovery permits actual Vanguard locomotion");
    }

    private static void RecoveryWalkingRestoresEquippedDrawing(FrontierRuntime app)
    {
        foreach (int equipped in new[] { 1, 2 })
        foreach (Point direction in ActorDirections)
        {
            BeginCrossEquippedQ(app, equipped, direction); Frames(app, 20);
            Check(app.Game.PlayerPhase == ActionPhase.Recovery && !app.Game.ActuallyMoving && app.Game.AppearanceWeapon == equipped,
                "Standing Q recovery retains equipped actor, not skill-class armor");
            int row = GameCore.DirectionRow(direction);
            CheckPlayerDrawing(app, row == 1 ? "PlayerRear" : row == 2 ? "PlayerSouth" :
                equipped == 1 ? "PlayerRanger" : "PlayerArcanist",
                row == 0 ? 7 : equipped * 4 + 3, direction);
            MoveDuringQRecovery(app, direction);
            Check(app.Game.AppearanceWeapon == equipped && app.Game.HeroAnimationState == 3,
                "Root-only movement must retain equipped outfit during Vanguard skill");
            CheckPlayerDrawing(app, row == 1 ? "PlayerRear" : row == 2 ?
                "PlayerSouth" : equipped == 1 ? "PlayerRanger" : "PlayerArcanist",
                row == 0 ? 7 : equipped * 4 + 3, direction);
            // Movement changes the GameCore world root, not the authored
            // action frame, facing or equipment during its life.
            Check((app.Game.VisualFacing-direction).Length < .001f,
                "Recovery strafing cannot rotate the active cast keypose");
        }
        Console.WriteLine("PASS Moving Q recovery retains full-body cast, class identity, grip and locked direction");
    }

    private static void StrafeDuringReleasedCastKeepsBodyAndAim(FrontierRuntime app)
    {
        // Old recovery code swapped the ENTIRE action actor to a Run body and
        // rotated it towards locomotion. A sideways or backwards strafe while
        // the same spell was releasing showed a different weapon, shoulder
        // placement and facing. The action and physical movement are separate.
        foreach (int profession in new[] { 0, 1, 2 })
        {
            Prepare(app, profession);
            var g = app.Game;
            g.enemies.Clear();
            Point attackDirection = new Point(1, 0);
            Mouse(app, g.Position + attackDirection * 4);
            Input.keys.Add(KeyCode.Q);
            Update(app);
            Frames(app, 20);
            Check(g.playerAction != null && g.playerAction.released &&
                g.PlayerPhase == ActionPhase.Recovery,
                "Cast reaches a real contact and recovery before strafe");
            Point old = g.Position;
            Input.axes["Horizontal"] = -1; Input.held.Add(KeyCode.A);
            Update(app);
            Check(g.ActuallyMoving && g.PlayerMobility == MobilityRule.Reduced,
                "Released cast must still allow collision-safe movement");
            Check((g.VisualFacing - attackDirection).Length < .001f,
                "Opposite movement must not rotate an active cast's hand, bow or staff");
            Check(g.AppearanceWeapon == profession && g.HeroAnimationState == 3 &&
                g.PlayerPose == 7,
                "Root-only cast recovery preserves correct profession and full action body");
            string source = profession == 0 ? "PlayerVanguard" :
                profession == 1 ? "PlayerRanger" : "PlayerArcanist";
            CheckPlayerDrawing(app, source, 7, attackDirection);
            Check((g.Position-old).Length <= .5f,
                "Changing animation cannot teleport the collision-governed world root");
        }
        Console.WriteLine("PASS Three-class opposite-direction recovery strafe preserves whole cast body, weapon and locked aim");
    }

    private static void RecoveryHitAndDeathRetainVisibleEquipment(FrontierRuntime app)
    {
        foreach (int equipped in new[] { 1, 2 })
        foreach (Point direction in ActorDirections)
        foreach (bool lethal in new[] { false, true })
        {
            BeginCrossEquippedQ(app, equipped, direction); Frames(app, 20); MoveDuringQRecovery(app, direction);
            var game = app.Game; game.invulnerable = 0;
            if (lethal) game.save.health = 1;
            game.projectiles.Add(new Projectile { position = game.Position, damage = lethal ? 999 : 1,
                velocity = new Point(.01f, 0), visual = "enemyOrb", source = "shot" });
            Update(app);
            Check(lethal ? game.Dying : game.playerHitTime > 0 && !game.Dying,
                "A real hostile projectile triggers the intended reaction/death boundary");
            Check(game.AppearanceWeapon == equipped && (game.VisualFacing - direction).Length < .001f,
                "Reaction/death must snapshot the equipped cast outfit and facing");
            int row = GameCore.DirectionRow(direction);
            string sheet = row == 1 ? "PlayerReactionsRear" : row == 2 ? "PlayerReactionsSouth"
                : equipped == 1 ? "PlayerRanger" : "PlayerArcanist";
            CheckPlayerDrawing(app, sheet, row == 0 ? lethal ? 11 : 10 :
                equipped * 4 + (lethal ? 3 : 2), direction, !lethal);
            Input.Clear(); Mouse(app, game.Position - direction * 4);
            if (lethal) game.Equip(0);
            Frames(app, 2);
            Check(game.AppearanceWeapon == equipped && (game.VisualFacing - direction).Length < .001f,
                "Reaction/death must keep its captured equipped outfit after other input/equipment changes");
            CheckPlayerDrawing(app, sheet, row == 0 ? lethal ? 11 : 10 :
                equipped * 4 + (lethal ? 3 : 2), direction, !lethal);
        }
        Console.WriteLine("PASS Actual hostile projectile reactions and deaths preserve the visible recovery equipment/facing");
    }

    private static void CombatOpeningsAndPoiseDrawInActualWorld(FrontierRuntime app)
    {
        Prepare(app, 0);
        var game = app.Game;
        var heavy = new Enemy(88102, 2, 13, 14, 200)
        { releaseTime = .22f, stagger = 18, timer = 999 };
        game.enemies.Add(heavy);
        Point screen = (Point)Call(app, "ScreenPoint", heavy.Position);
        Event.current.type = EventType.Repaint;
        GUI.textureCalls.Clear(); Call(app, "OnGUI");
        int mintRing = GUI.textureCalls.FindAll(t =>
            Math.Abs(t.color.r - .48f) < .001f &&
            Math.Abs(t.color.g - 1f) < .001f &&
            Math.Abs(t.color.b - .79f) < .001f &&
            Math.Abs(t.color.a - .85f) < .001f).Count;
        Check(mintRing >= 24, "The live recovery window draws a readable static mint ring");
        Check(GUI.textureCalls.Exists(t =>
            Math.Abs(t.rect.x - (screen.x - 16)) < .1f &&
            Math.Abs(t.rect.y - (screen.y - 106 * .72f + 1)) < .1f &&
            Math.Abs(t.rect.width - 19.2f) < .1f &&
            Math.Abs(t.rect.height - 3) < .01f),
            "The ordinary enemy's 18/30 poise draws a contextual 60% fill");
        heavy.releaseTime = 0; heavy.stagger = 0;
        GUI.textureCalls.Clear(); Call(app, "OnGUI");
        Check(!GUI.textureCalls.Exists(t =>
            Math.Abs(t.color.r - .48f) < .001f &&
            Math.Abs(t.color.g - 1f) < .001f &&
            Math.Abs(t.color.b - .79f) < .001f &&
            Math.Abs(t.color.a - .85f) < .001f),
            "Expired openings remove the mint ring instead of leaving a false cue");
        Console.WriteLine("PASS Real OnGUI draws and retires the opening ring and enemy poise meter (offline)");
    }


    // Real disk I/O through production SaveStore, exercising both crash recovery candidates.
    private static void SaveRecoveryUsesNewestVerifiedCopy(FrontierRuntime app)
    {
        string previousPath = Application.persistentDataPath;
        string directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "starfall-recovery-" + Guid.NewGuid().ToString("N"));
        Application.persistentDataPath = directory;
        try
        {
            System.IO.Directory.CreateDirectory(directory);
            string main = SaveStore.PathFor(0);
            Action<string, int, string> put = (path, coins, timestamp) =>
            {
                var state = new SaveData { coins = coins, savedAtUtc = timestamp };
                string payload = JsonUtility.ToJson(state);
                string checksum;
                using (var sha = System.Security.Cryptography.SHA256.Create())
                    checksum = BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(payload))).Replace("-", "");
                System.IO.File.WriteAllText(path, JsonUtility.ToJson(new SaveEnvelope { payload = payload, checksum = checksum }));
            };
            put(main, 10, "2026-01-01T00:00:00.0000000Z");
            put(main + ".bak", 20, "2026-01-02T00:00:00.0000000Z");
            put(main + ".tmp", 30, "2026-01-03T00:00:00.0000000Z");
            string warning;
            SaveData restored = SaveStore.Read(0, out warning);
            Check(restored != null && restored.coins == 10 && warning == null,
                "A valid primary save must win over newer recovery artifacts.");
            System.IO.File.WriteAllText(main, "{corrupt");
            restored = SaveStore.Read(0, out warning);
            Check(restored != null && restored.coins == 30 && warning == "Recovered interrupted save.",
                "A newer, checksum-valid interrupted write must outrank an older valid backup.");
            put(main + ".bak", 40, "2026-01-04T00:00:00.0000000Z");
            restored = SaveStore.Read(0, out warning);
            Check(restored != null && restored.coins == 40 && warning == "Recovered backup.",
                "An older stale temp must not roll back a newer verified backup.");
            System.IO.File.WriteAllText(main + ".bak", "{corrupt");
            restored = SaveStore.Read(0, out warning);
            Check(restored != null && restored.coins == 30 && warning == "Recovered interrupted save.",
                "A broken backup must not hide a still-verifiable interrupted save.");
            System.IO.File.WriteAllText(main + ".tmp", "{corrupt");
            restored = SaveStore.Read(0, out warning);
            Check(restored == null && warning != null,
                "A damaged primary and two damaged recovery files must not manufacture a save.");
            put(main + ".bak", 50, "2026-01-05T00:00:00.0000000Z");
            System.IO.File.Delete(main + ".tmp");
            restored = SaveStore.Read(0, out warning);
            Check(restored != null && restored.coins == 50 && warning == "Recovered backup.",
                "Existing backup-only recovery remains backward compatible.");
            System.IO.File.WriteAllText(main, "{}");
            restored = SaveStore.Read(0, out warning);
            Check(restored != null && restored.coins == 50 && warning == "Recovered backup.",
                "A structurally empty JSON object cannot silently become a fresh default character.");
            System.IO.File.WriteAllText(main, JsonUtility.ToJson(
                new SaveEnvelope { payload = null, checksum = "invalid" }));
            restored = SaveStore.Read(0, out warning);
            Check(restored != null && restored.coins == 50 && warning == "Recovered backup.",
                "A null-payload envelope must recover the verified backup, never masquerade as a legacy save.");
            System.IO.File.WriteAllText(main, "{\"version\":2,\"flags\":[\"inventory\"]}");
            restored = SaveStore.Read(0, out warning);
            Check(restored != null && restored.coins == 50 && warning == "Recovered backup.",
                "The word inventory inside a string cannot substitute for the required root inventory.");
            System.IO.File.WriteAllText(main, JsonUtility.ToJson(
                new SaveData { version = 1, coins = 77 }));
            restored = SaveStore.Read(0, out warning);
            Check(restored != null && restored.coins == 77 && restored.version == 1 && warning == null,
                "Unwrapped original v1 saves remain readable without migration or extra metadata.");
            System.IO.File.WriteAllText(main, JsonUtility.ToJson(
                new SaveData { version = 2, coins = 88 }));
            restored = SaveStore.Read(0, out warning);
            Check(restored != null && restored.coins == 88 && restored.version == 2 && warning == null,
                "Unwrapped original v2 saves also remain readable.");
            Console.WriteLine("PASS Real SaveStore I/O: crash recovery, schema rejection, truncated envelope, and original v1/v2 compatibility");
        }
        finally
        {
            Application.persistentDataPath = previousPath;
            if (System.IO.Directory.Exists(directory)) System.IO.Directory.Delete(directory, true);
        }
    }


    private static void PortableSaveCommitNeverLosesLastGoodPrimary(FrontierRuntime app)
    {
        // Exercise the REAL fallback used when File.Replace is unsupported.
        // The old fallback did File.Delete(primary) before File.Move(temp),
        // leaving a crash window with no primary, and no restore path when
        // a move failed. This test does actual disk I/O, not a mock store.
        string previous = Application.persistentDataPath;
        string folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "starfall-portable-save-" + Guid.NewGuid().ToString("N"));
        Application.persistentDataPath = folder;
        try
        {
            System.IO.Directory.CreateDirectory(folder);
            var original = new SaveData { coins = 31 };
            Check(SaveStore.Write(original, 1), "Initial valid save is written");
            string path = SaveStore.PathFor(1);
            string temp = path + ".tmp", backup = path + ".bak";
            Func<int, string> encode = coins =>
            {
                var data = new SaveData { coins = coins,
                    savedAtUtc = DateTime.UtcNow.AddSeconds(coins).ToString("o") };
                string payload = JsonUtility.ToJson(data);
                string sum;
                using(var hash=System.Security.Cryptography.SHA256.Create())
                    sum=BitConverter.ToString(hash.ComputeHash(
                        System.Text.Encoding.UTF8.GetBytes(payload))).Replace("-","");
                return JsonUtility.ToJson(
                    new SaveEnvelope { payload = payload, checksum = sum });
            };
            var substitute = typeof(SaveStore).GetMethod("PortableReplace",
                BindingFlags.Static | BindingFlags.NonPublic);
            Check(substitute != null, "Portable safe save transaction is implemented");
            System.IO.File.WriteAllText(temp, encode(52));
            substitute.Invoke(null, new object[] { temp, path, backup });
            string warning;
            Check(SaveStore.Read(1, out warning).coins == 52 &&
                warning == null && System.IO.File.Exists(backup) &&
                !System.IO.File.Exists(path + ".precommit"),
                "Portable save committed new primary without a stale displaced file");
            // Force a second-stage rename failure. A failed transaction
            // must restore the previous complete primary, not erase it.
            bool rejected = false;
            try { substitute.Invoke(null, new object[] {
                temp + ".missing", path, backup }); }
            catch(TargetInvocationException) { rejected = true; }
            Check(rejected && SaveStore.Read(1, out warning).coins == 52 &&
                warning == null && System.IO.File.Exists(path),
                "A failed portable move leaves the last valid primary playable");
            // Simulate process death AFTER the first move, BEFORE promoting
            // the verified temp. Only .precommit remains; the recovery path
            // must recognize it instead of constructing a blank new game.
            if(System.IO.File.Exists(backup)) System.IO.File.Delete(backup);
            System.IO.File.Move(path, path + ".precommit");
            Check(!System.IO.File.Exists(path), "Crash simulation displaced the primary");
            var recovered = SaveStore.Read(1, out warning);
            Check(recovered != null && recovered.coins == 52 &&
                warning == "Recovered backup." && SaveStore.Exists(1),
                "An interrupted portable move recovers its verified .precommit");
            System.IO.File.WriteAllText(temp, encode(63));
            substitute.Invoke(null, new object[] { temp, path, backup });
            Check(SaveStore.Read(1, out warning).coins == 63 && warning == null,
                "A later portable write heals the displaced-primary crash state");
            Console.WriteLine("PASS Atomic portable save: update, failed rename rollback, crash recovery and subsequent heal");
        }
        finally
        {
            Application.persistentDataPath = previous;
            if(System.IO.Directory.Exists(folder))
                System.IO.Directory.Delete(folder, true);
        }
    }

    private static void StalledFramesAdvanceActualWorldTimeWithoutRunawayCatchup(FrontierRuntime app)
    {
        Prepare(app);
        var game = app.Game;
        float startTime = game.save.totalTime;
        float startX = game.save.x;
        Time.unscaledDeltaTime = .18f;
        Input.axes["Horizontal"] = 1;
        Update(app);
        Check(Math.Abs(game.save.totalTime - startTime - .18f) < .0002f,
            "One slow 180ms render frame advances actual game time by four bounded simulation slices");
        float lowFpsX = game.save.x;
        Check(lowFpsX > startX + game.Speed * .13f,
            "Slow frame does not artificially halve real-distance movement");
        Prepare(app);
        game = app.Game;
        startX = game.save.x;
        foreach (float dt in new[] { .05f, .05f, .05f, .03f })
            game.Tick(dt, new Point(1, 0));
        Check(Math.Abs((game.save.x - startX) - (lowFpsX - 12f)) < .001f,
            "Slow frame matches four ordinary GameCore movement ticks on the original map");
        Prepare(app);
        game = app.Game;
        startTime = game.save.totalTime;
        Time.unscaledDeltaTime = 3f;
        Input.axes["Horizontal"] = 0;
        Update(app);
        Check(Math.Abs(game.save.totalTime - startTime - .20f) < .0002f,
            "A multi-second OS stall is capped to four updates, not hundreds of catch-up actions");
        foreach(float invalid in new[]{float.NaN,float.PositiveInfinity,-1f})
        {
            startTime=game.save.totalTime;
            Time.unscaledDeltaTime=invalid;
            Update(app);
            Check(game.save.totalTime==startTime,
                "Corrupted or negative external delta cannot poison world time");
        }
        Console.WriteLine("PASS Bounded substeps preserve low-FPS movement, timers, catch-up cap, and invalid-delta safety");
    }


    private static void TitleSaveManagerNeverOverwritesAnotherJourney(FrontierRuntime original)
    {
        string previous = Application.persistentDataPath;
        string folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "starfall-title-slots-" + Guid.NewGuid().ToString("N"));
        FrontierRuntime app = null;
        Application.persistentDataPath = folder;
        try
        {
            System.IO.Directory.CreateDirectory(folder);
            var autosave = new SaveData { coins = 91 };
            autosave.flags.Add("intro");
            var manual = new SaveData { coins = 17 };
            manual.flags.Add("intro");
            Check(SaveStore.Write(autosave) && SaveStore.Write(manual, 0),
                "Test creates real isolated autosave and manual slot");
            app = new FrontierRuntime();
            Call(app, "Awake");
            Check(app.Game != null && app.Game.save.coins == 91,
                "Title reads the authentic preexisting autosave without starting it");
            app.Game.mode = "saves";
            Set(app, "settingsParent", "title");
            GUI.buttonCalls.Clear();
            Event.current.type = EventType.Repaint;
            Call(app, "OnGUI"); // Style initialization and control scoping happen here.
            var manualButton = GUI.buttonCalls.Find(c =>
                Math.Abs(c.rect.x - 869) < .01f && Math.Abs(c.rect.y - 326) < .01f);
            Check(manualButton != null && !manualButton.enabled,
                "The title save manager visibly disables manual writes for all users");
            Call(app, "WriteSlot", 0);
            string warning;
            Check(SaveStore.Read(0, out warning).coins == 17,
                "Even an unexpected title save callback cannot overwrite the manual slot");
            Call(app, "LoadSlot", 0);
            Check(app.Game.mode == "world" && app.Game.save.coins == 17,
                "Title load starts the selected real manual journey");
            Check(SaveStore.Read(out warning).coins == 91,
                "Loading from title does not first overwrite the independent autosave");
            // Mid-game switch retains the documented insurance autosave.
            app.Game.save.coins = 48;
            app.Game.mode = "saves";
            Set(app, "settingsParent", "world");
            Call(app, "LoadSlot", 0);
            Check(app.Game.save.coins == 17 && SaveStore.Read(out warning).coins == 48,
                "In-game load still autosaves current progress before switching slots");
            Console.WriteLine("PASS Title slots are read-only; title loads preserve autosave, in-game loads insure current progress");
        }
        finally
        {
            if (app != null) Call(app, "OnDestroy");
            Application.persistentDataPath = previous;
            if (System.IO.Directory.Exists(folder)) System.IO.Directory.Delete(folder, true);
        }
    }


    private static void FishingSuspendsOnFocusLossAndResumesOnReturn(FrontierRuntime app)
    {
        Prepare(app);
        var game = app.Game;
        game.mode = "fish";
        game.fishTime = 1.25f;
        game.fishHits = 1;
        game.fishMisses = 1;
        float before = game.fishTime;
        Call(app, "OnApplicationFocus", false);
        Frames(app, 25);
        Check(game.mode == "fish" && game.fishTime == before &&
              game.fishHits == 1 && game.fishMisses == 1,
            "Losing window focus freezes the live fishing cursor without forfeiting prior catches");
        Call(app, "OnApplicationFocus", true);
        Update(app);
        Check(game.mode == "fish" && game.fishTime > before &&
              game.fishHits == 1 && game.fishMisses == 1,
            "Returning to the same window resumes fishing at its saved in-memory phase");
        game.Close();
        Console.WriteLine("PASS Fishing freezes off-window and resumes with its catches intact");
    }

    private static void MaxLevelProgressionShowsFinalState(FrontierRuntime app)
    {
        Prepare(app);
        var game=app.Game;
        game.save.english=true;
        game.save.level=30;
        game.save.xp=0;
        game.mode="talents";
        Event.current.type=EventType.Repaint;
        GUI.commands.Clear();Call(app,"OnGUI");
        Check(GUI.commands.Exists(c=>c.Contains("XP MAX")),
            "The actual talents menu renders a final XP MAX state instead of an unreachable next-level progress bar");
        Console.WriteLine("PASS Max-level talents menu shows XP MAX without inventing prestige levels");
    }

    private static void RespecButtonExplainsSkillPointCap(FrontierRuntime app)
    {
        Prepare(app);
        app.Game.mode="talents";
        app.Game.save.coins=100;
        app.Game.save.skillPoints=95;
        app.Game.save.talents[0]=5;
        app.Game.save.talents[1]=5;
        app.Game.save.english=true;
        GUI.commands.Clear();GUI.buttonCalls.Clear();Event.current.type=EventType.Repaint;
        Call(app,"OnGUI");
        var respec=GUI.buttonCalls.Find(c=>Math.Abs(c.rect.y-704)<.01f &&
            Math.Abs(c.rect.x-279)<.01f);
        Check(respec!=null&&!respec.enabled,
            "The real talents screen disables reset when refunded skill points would overflow");
        Check(GUI.commands.Exists(c=>c.Contains("Refund exceeds 99-point cap")),
            "The disabled talents reset tells the player how to make the action available");
        app.Game.save.skillPoints=89;
        GUI.commands.Clear();GUI.buttonCalls.Clear();
        Call(app,"OnGUI");
        respec=GUI.buttonCalls.Find(c=>Math.Abs(c.rect.y-704)<.01f &&
            Math.Abs(c.rect.x-279)<.01f);
        Check(respec!=null&&respec.enabled,
            "The same respec button re-enables when a full refund fits in v2");
        Console.WriteLine("PASS Actual talents UI blocks overflowing respec and explains the point-cap remedy");
    }


    private static void DeathRecoveryCannotBeInterruptedByMenuOrFocus(FrontierRuntime app)
    {
        Prepare(app);
        var game=app.Game;
        game.Enter(1);game.enemies.Clear();game.mode="world";
        game.save.health=1;game.invulnerable=0;
        typeof(GameCore).GetMethod("Hurt", Private).Invoke(game,new object[]{999f,"shot",null});
        Check(game.Dying&&game.mode=="world"&&game.save.room==1,
            "Authentic lethal damage begins a visible recovery at the original battlefield");
        Input.keys.Add(KeyCode.I);Update(app);
        Check(game.mode=="world"&&game.Dying,
            "Pressing inventory during death must not pause mandatory recovery");
        Input.keys.Add(KeyCode.Tab);Update(app);
        Check(game.mode=="world"&&game.Dying,
            "Pressing journal during death must not pause mandatory recovery");
        Call(app,"OpenMenu","pause");
        Check(game.mode=="world"&&game.Dying,
            "GUI or controller pause calls cannot interrupt a dead player's recovery");
        Call(app,"OnApplicationFocus",false);
        Check(game.mode=="world",
            "Window focus loss during death may not leave a hidden pause menu behind");
        float frozen=game.deathTime;
        Frames(app,7);
        Check(game.deathTime==frozen,
            "Window focus loss still freezes the recovery clock while actually away");
        Call(app,"OnApplicationFocus",true);
        Frames(app,55);
        Check(!game.Dying&&game.mode=="world"&&game.save.room==0&&
              game.save.health==game.save.maxHealth,
            "Returning focus completes safe inn respawn without any special unpause action");
        Console.WriteLine("PASS Real death pose rejects menu toggles and recovers after window focus resumes");
    }

    private static void RealMovingSpellCompositesAndBossFrames(FrontierRuntime app)
    {
        // Moving recovery translates the ONE complete active class-owned
        // Attack/Cast body until the authoritative action finishes; neither
        // leg grafts nor premature running-pose swaps are allowed.
        foreach(int vocation in new[]{1,2})
        {
            Prepare(app,vocation);app.Game.enemies.Clear();
            Input.keys.Add(KeyCode.Q);Update(app);Frames(app,22);
            var g=app.Game;
            Check(g.playerAction!=null&&g.PlayerPhase==ActionPhase.Recovery,
                "Actual Q must release real damage before movement recovery");
            Input.axes["Horizontal"]=1;Input.held.Add(KeyCode.D);Update(app);
            Check(g.ActuallyMoving&&g.PlayerMobility==MobilityRule.Reduced,
                "Released spell recovery must still allow gameplay movement");
            GUI.textureCalls.Clear();Call(app,"DrawPlayerActor");
            string klass=vocation==1?"Ranger":"Arcanist";
            Check(GUI.textureCalls.Exists(t=>t.texture.name==
                "resource:Frontier/HeroAnimator/Canonical/Hero_"+klass+"_Cast_E"),
                "A moving skill must retain its original full-body Cast drawing until release recovery ends");
            Check(!GUI.textureCalls.Exists(t=>t.texture.name.StartsWith(
                "FullBodyGait_")||t.texture.name.Contains("/WalkVanguard")),
                "Runtime cannot construct moving spell torso/leg or head joins");
        }
        Prepare(app,0);
        var blade=app.Game;blade.Attack(new Point(1,0));Frames(app,14);
        Check(blade.PlayerPhase==ActionPhase.Recovery&&!blade.ActuallyMoving,
            "Sword contact/damage time remains owned by GameCore");
        Input.axes["Horizontal"]=1;Input.held.Add(KeyCode.D);Update(app);
        Check(blade.ActuallyMoving&&blade.PlayerMobility==MobilityRule.Reduced,
            "Sword recovery must retain gameplay locomotion");
        GUI.textureCalls.Clear();Call(app,"DrawPlayerActor");
        Check(GUI.textureCalls.Exists(t=>t.texture.name==
            "resource:Frontier/HeroAnimator/Canonical/Hero_Vanguard_Attack_E"),
            "Moving sword recovery must retain the complete Vanguard swing, not swap to Run");
        Prepare(app,0);
        var player=app.Game;
        player.enemies.Clear();
        var normal=new Enemy(9891,2,player.Position.x+1.2f,player.Position.y,150){
            state=1,timer=.3f,windupDuration=.65f
        };
        GUI.textureCalls.Clear();Call(app,"DrawEnemyActor",normal);
        var key="resource:Frontier/Art/CombatRhythmRemaster";
        Check(GUI.textureCalls.Exists(x=>x.texture.name==key),
            "Ordinary enemies draw real authored winding phases in actual renderer");
        normal.state=0;normal.releaseTime=.15f;
        GUI.textureCalls.Clear();Call(app,"DrawEnemyActor",normal);
        Check(GUI.textureCalls.Exists(x=>x.texture.name==key),
            "Real enemy release displays distinct authored impact frames");
        var boss=new Enemy(9892,2,player.Position.x+1,player.Position.y,400,true){
            state=1,timer=.6f,windupDuration=1.1f
        };
        GUI.textureCalls.Clear();Call(app,"DrawEnemyActor",boss);
        Check(GUI.textureCalls.Exists(x=>x.texture.name==key),
            "Actual boss windup uses its own rune sequence without changing the original boss sprite");
        Console.WriteLine("PASS full-body released spell/sword recovery with class-owned pixels, canonical blonde head and boss/enemy cues");
    }

    private static void HeroActionAccentsFollowRealDirectionalWeaponMount(FrontierRuntime app)
    {
        // Old art drew ALL effects at a constant camera-space +22/-63
        // offset, including north/south bow and staff attacks. The body
        // turned but the particle appeared detached from the real hand and
        // projectile emitter. Test the actual DrawPlayerActor GUI output.
        foreach(int vocation in new[]{0,1,2})
        foreach(Point facing in ActorDirections)
        {
            Prepare(app,vocation);
            GameCore g=app.Game;
            g.Skill(facing);
            Check(g.playerAction!=null&&g.playerAction.kind=="skill",
                "Real class skill needs a weapon attachment visual");
            g.movementDirection=new Point();
            GUI.textureCalls.Clear();
            Call(app,"DrawPlayerActor");
            var active=GUI.textureCalls.FindAll(t =>
                t.texture.name.Contains("HeroMotionAccents"));
            Check(active.Count==1,
                "Exactly one action-matched effect and no fake walking feet in the stationary windup");
            Point anchor=(Point)Call(app,"ScreenPoint",
                GameCore.WeaponOrigin(g.Position,g.AppearanceWeapon,g.VisualFacing));
            float y=anchor.y-GameCore.WeaponHeight(g.AppearanceWeapon,g.VisualFacing);
            Check(Math.Abs(active[0].rect.x+active[0].rect.width*.5f-anchor.x)<.02f&&
                  Math.Abs(active[0].rect.y+active[0].rect.height*.5f-y)<.02f,
                "Vanguard/Ranger/Arcanist E/W/N/S skill accent must share GameCore's real weapon emitter");
            // Render direction changes must not synthesize extra independent
            // animation sprites while movement is collision-restricted.
            Check(!GUI.textureCalls.Exists(t=>t.texture.name.Contains("FullBodyGait_")),
                "No puppet layer may be revived for an attached weapon effect");
        }
        Console.WriteLine("PASS 12 real profession/facing action VFX mounts share weapon origin/height and whole-body art");
    }

    private static void ProduceVerifiedGameplayHeroTrace(FrontierRuntime app)
    {
        // This calls REAL GameCore/FrontierRuntime action, movement, and
        // the production ResolveFullBodyHeroFrame selector. Offline GameView
        // is simulated, so this is not a native GPU capture.
        string[] classes={"Vanguard","Ranger","Arcanist"};
        string[] actions={"Idle","Run","Attack","Cast","Dodge","Hit","Death"};
        string[] faces={"E","N","S"};
        var points=new[]{
            new Point(1,0),new Point(-1,0),
            new Point(0,-1),new Point(0,1)};
        MethodInfo resolver=typeof(FrontierRuntime).GetMethod(
            "ResolveFullBodyHeroFrame",Private);
        Check(resolver!=null,"Production hero frame resolver is missing");
        var text=new StringBuilder("profession,command,direction,tick,phase,root_x,root_y,root_moving,footsteps,walk_distance,appearance,asset,cell,cell_count,flip,action_live\n");
        int observations=0, movingRecoverySamples=0;
        for(int cls=0;cls<3;cls++)
        for(int cmd=0;cmd<2;cmd++)
        for(int face=0;face<points.Length;face++)
        {
            Prepare(app,cls);
            GameCore g=app.Game;
            Point heading=points[face];
            Point strafe=new Point(-heading.y,heading.x);
            if(cmd==0)g.Attack(heading);else g.Skill(heading);
            Check(g.playerAction!=null,
                "Actual Attack/Cast must start before tracing its live frames");
            ActorAction selected=g.playerAction;
            bool sawReleased=false;
            for(int tick=0;tick<40;tick++)
            {
                if(tick>0)
                {
                    // Real reduced-mobility recovery moves the ROOT, never
                    // replaces the full-body action with fake running legs.
                    Point travel=g.playerAction!=null &&
                        g.PlayerPhase==ActionPhase.Recovery ?
                        strafe : new Point();
                    app.AdvanceVisualReview(.02f,travel);
                }
                bool hasAction=g.playerAction==selected;
                if(hasAction&&selected.released)sawReleased=true;
                bool rootMoving=g.ActuallyMoving;
                bool running=g.PlayerPose>=2&&g.PlayerPose<=3;
                int facing=GameCore.DirectionRow(g.VisualFacing);
                object[] args={running,facing,0,0,0,0f};
                bool resolved=(bool)resolver.Invoke(app,args);
                Check(resolved,"Production full-body Sprite state cannot resolve");
                int actionId=(int)args[2],cells=(int)args[3],cell=(int)args[4];
                int look=g.AppearanceWeapon;
                Check(look>=0&&look<3&&actionId>=0&&actionId<7&&
                    facing>=0&&facing<3&&cell>=0&&cell<cells,
                    "Real game selected an impossible actor/action/frame");
                if(hasAction)
                {
                    Check(look==cls&&actionId==(cmd==0?2:3),
                        "Active action changed its class or entire body unexpectedly");
                    if(!selected.released&&(actionId==2||actionId==3))
                        Check(cell<3,"Old pre-release contact frame flashed");
                    if(rootMoving&&g.PlayerPhase==ActionPhase.Recovery)
                    {
                        movingRecoverySamples++;
                        Check(g.walkDistance<.00001f,
                            "An invisible stride advanced under the released whole-body pose");
                    }
                }
                string asset="Hero_"+classes[look]+"_"+actions[actionId]+
                    "_"+faces[facing];
                bool flip=facing==0&&g.VisualFacing.x<-.1f;
                string[] columns={
                    classes[cls],cmd==0?"Attack":"Cast",
                    face==0?"E":face==1?"W":face==2?"N":"S",
                    tick.ToString(),
                    g.PlayerPhase.ToString(),
                    g.Position.x.ToString("R",CultureInfo.InvariantCulture),
                    g.Position.y.ToString("R",CultureInfo.InvariantCulture),
                    rootMoving?"1":"0",g.footContacts.ToString(),
                    g.walkDistance.ToString("R",CultureInfo.InvariantCulture),
                    look.ToString(),asset,cell.ToString(),cells.ToString(),
                    flip?"1":"0",hasAction?"1":"0"
                };
                text.AppendLine(string.Join(",",columns));
                observations++;
            }
            Check(sawReleased, "Actual weapon release must occur in every motion trace");
        }
        Check(observations==3*2*4*40&&movingRecoverySamples>0,
            "No real three-class moving-recovery frames audited");
        string folder=".starfall-checks/hero-gameplay-traces";
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder,"gameplay-trace.csv"),text.ToString());
        Console.WriteLine("PASS 960 REAL combat/movement sprite selections audited; "+
            movingRecoverySamples+" moving-recovery snapshots; NATIVE_UNVERIFIED");
    }

    private static void AllEquippedBodiesRemainCanonicalDuringAttackAndSkill(FrontierRuntime app)
    {
        // 3 player disciplines x 3 independently equipped weapons x 2
        // actions x 4 directions. An action.weapon is the MECHANIC,
        // never the costume/model. Heavy LMB used to force old Player art.
        int fullBodyChecks=0;
        foreach(int discipline in new[]{0,1,2})
        foreach(int equipped in new[]{0,1,2})
        foreach(bool skill in new[]{false,true})
        foreach(Point direction in ActorDirections)
        {
            Prepare(app,discipline);
            var g=app.Game;
            if(equipped>0 && !g.Has("weapon_"+equipped))
                g.save.flags.Add("weapon_"+equipped);
            g.Equip(equipped);
            Check(g.save.weapon==equipped,
                "Requested weapon must be equipped before animation checks");
            // Focused sword was the real heavy combo escape hatch back to
            // legacy original Player art. Force it for every sword LMB.
            if(!skill && equipped==0)
                g.save.weaponStyles[0]=1;
            if(skill)g.Skill(direction); else g.Attack(direction);
            var action=g.playerAction;
            Check(action!=null&&action.kind==(skill?"skill":"attack"),
                "Real gameplay must begin requested action");
            Check(action.weapon==(skill?discipline:equipped)&&
                action.equippedWeapon==equipped,
                "Skill discipline and visual equipment remain distinct");
            Check(g.AppearanceWeapon==equipped,
                "First frame must retain the current equipped actor");
            CheckPlayerDrawing(app,"",0,direction);
            fullBodyChecks++;
            int ticks=0;
            while(g.playerAction==action&&!action.released&&ticks++<75)
            {
                g.Tick(.01f,new Point());
                if(g.playerAction==action && !action.released && ticks==8)
                {
                    Check(g.AppearanceWeapon==equipped,
                        "Windup may not switch to old class model");
                    CheckPlayerDrawing(app,"",0,direction);
                    fullBodyChecks++;
                }
            }
            Check(g.playerAction==action&&action.released&&ticks<75,
                "Authoritative action must reach contact");
            Check(g.AppearanceWeapon==equipped,
                "Contact must retain currently equipped model");
            CheckPlayerDrawing(app,"",0,direction);
            fullBodyChecks++;
            while(g.playerAction==action&&
                g.PlayerPhase!=ActionPhase.Recovery&&ticks++<95)
                g.Tick(.01f,new Point());
            Check(g.playerAction==action&&g.PlayerPhase==ActionPhase.Recovery,
                "Action must reach recovery in real game clock");
            g.Tick(.01f,new Point());
            Check(g.AppearanceWeapon==equipped,
                "Recovery must not restore a different profession costume");
            CheckPlayerDrawing(app,"",0,direction);
            fullBodyChecks++;
            if(skill && discipline==1)
            {
                Check(g.projectiles.Count>0,
                    "Cross-equipped Ranger volley preserves original combat mechanic");
                foreach(var projectile in g.projectiles)
                {
                    Check(Math.Abs(projectile.launchHeight -
                        GameCore.WeaponHeight(equipped,direction))<.001f,
                        "Ranger skill projectile launch height must match equipped visual muzzle");
                }
            }
        }
        Check(fullBodyChecks>=216,
            "All 72 profession/equipment/action/direction combinations sampled");
        Console.WriteLine("PASS "+fullBodyChecks+
            " true gameplay full-body previews across 72 equipped LMB/Q scenarios; no old model");
    }

    private static void CanonicalHeroNeverDependsOnLegacyPlayerImports(FrontierRuntime app)
    {
        // In an actual Unity import/update these older readable 4x3 Player*
        // textures can temporarily be absent even while all 63 production
        // canonical atlases are installed. A missing legacy model MUST NOT
        // gate the new actor or switch LMB/Q back to old art.
        var type=typeof(FrontierRuntime);
        object originalPlayers=type.GetField("playerSheets",Private).GetValue(app);
        object originalWalks=type.GetField("walkSheets",Private).GetValue(app);
        int checks=0;
        try
        {
            foreach(int discipline in new[]{0,1,2})
            foreach(int equipped in new[]{0,1,2})
            foreach(bool skill in new[]{false,true})
            foreach(Point direction in ActorDirections)
            {
                Prepare(app,discipline);
                var g=app.Game;
                if(equipped>0&&!g.Has("weapon_"+equipped))
                    g.save.flags.Add("weapon_"+equipped);
                g.Equip(equipped);
                Check(g.save.weapon==equipped,
                    "Equipped identity must be ready for missing-legacy test");
                // The idle pre-attack sample must first face the same
                // requested compass direction as the subsequent action.
                // Otherwise its correct default East Idle is mislabelled N/S.
                g.SetAim(direction);
                Set(app,"playerSheets",null);
                Set(app,"walkSheets",null);
                CheckPlayerDrawing(app,"",0,direction);
                checks++;
                if(skill)g.Skill(direction);else g.Attack(direction);
                var a=g.playerAction;
                Check(a!=null,
                    "LMB/Q must start despite absent older source art");
                CheckPlayerDrawing(app,"",0,direction);
                checks++;
                int ticks=0;
                while(g.playerAction==a&&!a.released&&ticks++<80)
                    g.Tick(.01f,new Point());
                Check(a.released&&g.playerAction==a,
                    "Combat release remains driven by GameCore without old art");
                Check(g.AppearanceWeapon==equipped,
                    "Combat must retain equipped full-body identity");
                CheckPlayerDrawing(app,"",0,direction);
                checks++;
                // Occlusion previously drew a separate legacy Player head,
                // armor and body. Verify the *real rendering pass* now uses
                // this SAME canonical action atlas instead of old outlines.
                Set(app,"drawingOutline",true);
                try { CheckPlayerDrawing(app,"",0,direction); }
                finally { Set(app,"drawingOutline",false); }
                // Prepare restores gameplay fixtures; never restore old art
                // until the whole test has completed every variant.
            }
        }
        finally
        {
            Set(app,"playerSheets",originalPlayers);
            Set(app,"walkSheets",originalWalks);
        }
        Check(checks==216,
            "All 3 professions x 3 equipments x 2 actions x 4 facings "
            +"must draw Idle, windup and release from published full-body art");
        Console.WriteLine("PASS "+checks+" production hero frames remain canonical "
            +"without ANY legacy Player/Walk import dependency");
    }

    private static void LiveEquipmentPreviewUsesTheSameCanonicalBody(FrontierRuntime app)
    {
        // The HUD/loadout/inventory MUST display the same modern equipment
        // as gameplay. A previous emergency path showed old Walk*.png.
        object players=typeof(FrontierRuntime).GetField("playerSheets",Private).GetValue(app);
        object walks=typeof(FrontierRuntime).GetField("walkSheets",Private).GetValue(app);
        int checks=0;
        try
        {
            for(int cls=0;cls<3;cls++)
            for(int outlined=0;outlined<2;outlined++)
            {
                Prepare(app,cls);
                Set(app,"playerSheets",null);
                Set(app,"walkSheets",null);
                Set(app,"drawingOutline",outlined==1);
                try
                {
                    GUI.textureCalls.Clear();
                    Call(app,"DrawPlayerPreview",cls,512f,335f,104f);
                    string key="resource:Frontier/HeroAnimator/Canonical/Hero_"+
                        new[]{"Vanguard","Ranger","Arcanist"}[cls]+"_Idle_S";
                    var shown=GUI.textureCalls.Find(t=>t.texture.name==key);
                    Check(shown!=null,
                        "Equipment preview must never borrow old Walk/Player body: "+key);
                    Check(Math.Abs(shown.rect.width-104)<.01f&&
                          Math.Abs(shown.rect.height-104)<.01f&&
                          Math.Abs(shown.rect.y-(335f-104f*240f/256f))<.01f,
                        "HUD preview uses the identical 256px foot root and body scale");
                    Check(!GUI.textureCalls.Exists(t=>t.texture.name.StartsWith(
                        "resource:Frontier/Art/Walk")||
                        t.texture.name.StartsWith("resource:Frontier/Art/Player")),
                        "Old protagonist leaked into inventory/outline preview");
                    checks++;
                }
                finally { Set(app,"drawingOutline",false); }
            }
        }
        finally
        {
            Set(app,"playerSheets",players);
            Set(app,"walkSheets",walks);
        }
        Check(checks==6,"Three equipment classes x normal/outline previews");
        Console.WriteLine("PASS "+checks+" inventory/loadout canonical body previews with no old model");
    }

    private static void GroundContactArtSharesPhysicalFootPivot(FrontierRuntime app)
    {
        Prepare(app,0);
        var g=app.Game;g.enemies.Clear();g.save.companion=-1;
        const string path="resource:Frontier/Art/ActorContactShadows";
        GUI.textureCalls.Clear();Call(app,"DrawPlayerActor");
        var p=(Point)Call(app,"ScreenPoint",g.Position);
        var shadow=GUI.textureCalls.Find(t=>t.texture.name==path);
        Check(shadow!=null&&Math.Abs(shadow.rect.x+shadow.rect.width/2-p.x)<.01f&&
            Math.Abs(shadow.rect.y+shadow.rect.height*.72f-p.y)<.01f,
            "The hero's real shadow must land on one stable physical foot pivot");
        Check(shadow.texture.width==128&&shadow.texture.height==128,
            "New pixel shadow atlas must load from shipped binary resources");
        GUI.textureCalls.Clear();Call(app,"DrawEnemyActor",
            new Enemy(97211,1,g.Position.x+2,g.Position.y,100));
        Check(GUI.textureCalls.Exists(t=>t.texture.name==path),
            "An ordinary enemy's real renderer must use contact art");
        GUI.textureCalls.Clear();Call(app,"DrawEnemyActor",
            new Enemy(97212,0,g.Position.x+3,g.Position.y,300,true));
        Check(GUI.textureCalls.Exists(t=>t.texture.name==path),
            "The full-size Boss must stand on a contact patch");
        GUI.textureCalls.Clear();Call(app,"DrawNpc","forge",g.Position,-1);
        Check(GUI.textureCalls.Exists(t=>t.texture.name==path),
            "NPCs must use the same material contact language");
        g.save.companion=0;GUI.textureCalls.Clear();Call(app,"DrawCompanionActor");
        Check(GUI.textureCalls.Exists(t=>t.texture.name==path),
            "A party companion must share the grounded rendering pipeline");
        GUI.textureCalls.Clear();Call(app,"DrawFallen",new FallenActor{
            actor=new Enemy(97213,0,g.Position.x,g.Position.y,100),
            room=g.save.room,life=.3f,duration=1f});
        var fading=GUI.textureCalls.Find(t=>t.texture.name==path);
        Check(fading!=null&&fading.color.a>0&&fading.color.a<.84f,
            "A corpse must keep its real foot shadow and fade it with the body");
        Console.WriteLine("PASS hero, enemy, Boss, NPC and companion contact shadows use actual atlas and root");
    }

    public static int Main()
    {
        var app = new FrontierRuntime(); Call(app, "Awake");
        if (app.Game == null) { Console.WriteLine("FAIL Runtime Awake did not load the shipped campaign"); return 1; }
        Action<FrontierRuntime>[] cases = { FreshTechniqueWinsHeldAttack, QueuedTechniqueKeepsAimAndRunsOnce,
            DashAndTechniqueShareAFrame, HudTechniqueCanQueue, MenuAndApplicationPauseDiscardIntent,
            ReleasedTechniqueDrawsContactFrame,
            DirectionalLmbAndSkillContactNeverFlashBeforeRelease,
            ActorRegistrationChecks.VerifyUnifiedGaitPixelComposites,
            ActorRegistrationChecks.VerifyBossMovingFlash,
            ActorRegistrationChecks.VerifyResidentAnimation,
            RealMovingSpellCompositesAndBossFrames, HeroActionAccentsFollowRealDirectionalWeaponMount,
            AllEquippedBodiesRemainCanonicalDuringAttackAndSkill,
            CanonicalHeroNeverDependsOnLegacyPlayerImports,
            LiveEquipmentPreviewUsesTheSameCanonicalBody,
            ProduceVerifiedGameplayHeroTrace, GroundContactArtSharesPhysicalFootPivot,
            RecoveryWalkingRestoresEquippedDrawing,
            StrafeDuringReleasedCastKeepsBodyAndAim,
            RecoveryHitAndDeathRetainVisibleEquipment, CombatOpeningsAndPoiseDrawInActualWorld,
            RenderStateChecks.MapRasterRegression, SaveRecoveryUsesNewestVerifiedCopy,
            PortableSaveCommitNeverLosesLastGoodPrimary,
            MaxLevelProgressionShowsFinalState,
            RespecButtonExplainsSkillPointCap,
            TitleSaveManagerNeverOverwritesAnotherJourney,
            FishingSuspendsOnFocusLossAndResumesOnReturn,
            DeathRecoveryCannotBeInterruptedByMenuOrFocus,
            StalledFramesAdvanceActualWorldTimeWithoutRunawayCatchup };
        int failures = 0;
        foreach (var test in cases)
        {
            try { test(app); }
            catch (Exception error)
            {
                failures++; Console.WriteLine("FAIL " + test.Method.Name + ": " + (error.InnerException ?? error).Message);
            }
        }
        Call(app, "OnDestroy");
        Console.WriteLine("Runtime combat input: " + (cases.Length - failures) + "/" + cases.Length + " groups passed; NATIVE_UNVERIFIED");
        return failures == 0 ? 0 : 1;
    }
}
