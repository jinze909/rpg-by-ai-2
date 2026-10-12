// Runs actual FrontierRuntime.Update/OnGUI against controllable Unity declarations.
// This is an offline input regression harness, not Unity Player verification.
using System;
using System.Reflection;
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
        foreach (string boundary in new[] { "pause", "inventory", "application" })
        {
            Prepare(app); ActorAction attack = StartBowAttack(app); Frames(app, 9);
            Input.keys.Add(KeyCode.Q); Update(app);
            Check(app.Game.playerAction == attack && app.Game.skillCooldown <= 0, "The boundary test begins with an unspent queued Q");
            if (boundary == "application")
            {
                Call(app, "OnApplicationPause", true); Frames(app, 20); Call(app, "OnApplicationPause", false);
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
        Console.WriteLine("PASS Runtime pause, inventory and OnApplicationPause discard pending technique intent");
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
        Call(app, "DrawPlayerActor");
        var body = GUI.textureCalls.Find(draw => draw.texture.name.StartsWith("resource:Frontier/Art/")
            && !draw.texture.name.Contains("Effects"));
        Check(body != null && body.texture.name == "resource:Frontier/Art/" + sheet && DrawnFrame(body) == frame,
            "Actual player rendering must select " + sheet + " frame " + frame);
        if (head)
        {
            var identity = GUI.textureCalls.Find(draw => draw.texture.name == "resource:Frontier/Art/WalkVanguard");
            Check(identity != null && DrawnFrame(identity) == GameCore.DirectionRow(direction) * 4,
                "Equipment and action transitions keep the canonical head and its facing source");
        }
        // These checks validate source selection, not anatomy or user art acceptance.
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
            Check(app.Game.PlayerPhase == ActionPhase.Recovery && !app.Game.ActuallyMoving && app.Game.AppearanceWeapon == 0,
                "Standing Q recovery retains the skill's sword/shield appearance");
            int row = GameCore.DirectionRow(direction);
            CheckPlayerDrawing(app, row == 1 ? "PlayerRear" : row == 2 ? "PlayerSouth" : "PlayerVanguard",
                row == 0 ? 7 : 3, direction);
            MoveDuringQRecovery(app, direction);
            Check(app.Game.AppearanceWeapon == equipped, "Moving recovery immediately restores the currently equipped appearance");
            CheckPlayerDrawing(app, equipped == 1 ? "WalkRanger" : "WalkArcanist",
                row * 4 + app.Game.WalkFrame, direction);
        }
        Console.WriteLine("PASS Actual standing Q and moving recovery use skill/equipped body sheets with the same canonical head");
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
                "Reaction/death captures the equipment and facing actually shown during recovery walking");
            int row = GameCore.DirectionRow(direction);
            string sheet = row == 1 ? "PlayerReactionsRear" : row == 2 ? "PlayerReactionsSouth"
                : "Player" + (equipped == 1 ? "Ranger" : "Arcanist");
            CheckPlayerDrawing(app, sheet, row == 0 ? lethal ? 11 : 10 : equipped * 4 + (lethal ? 3 : 2), direction, !lethal);
            Input.Clear(); Mouse(app, game.Position - direction * 4);
            if (lethal) game.Equip(0);
            Frames(app, 2);
            Check(game.AppearanceWeapon == equipped && (game.VisualFacing - direction).Length < .001f,
                "Recoil motion, live cursor changes and later equipment changes cannot replace the captured reaction/death outfit");
            CheckPlayerDrawing(app, sheet, row == 0 ? lethal ? 11 : 10 : equipped * 4 + (lethal ? 3 : 2), direction, !lethal);
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

    public static int Main()
    {
        var app = new FrontierRuntime(); Call(app, "Awake");
        if (app.Game == null) { Console.WriteLine("FAIL Runtime Awake did not load the shipped campaign"); return 1; }
        Action<FrontierRuntime>[] cases = { FreshTechniqueWinsHeldAttack, QueuedTechniqueKeepsAimAndRunsOnce,
            DashAndTechniqueShareAFrame, HudTechniqueCanQueue, MenuAndApplicationPauseDiscardIntent,
            ReleasedTechniqueDrawsContactFrame, RecoveryWalkingRestoresEquippedDrawing,
            RecoveryHitAndDeathRetainVisibleEquipment, CombatOpeningsAndPoiseDrawInActualWorld };
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
