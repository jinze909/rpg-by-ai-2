// Runs actual FrontierRuntime.Update/OnGUI against controllable Unity declarations.
// This is an offline input regression harness, not Unity Player verification.
using System;
using System.Collections.Generic;
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
    private static object CallCore(GameCore game, string method, params object[] args)
    { return typeof(GameCore).GetMethod(method, Private).Invoke(game, args); }
    private static object CoreField(GameCore game, string field)
    { return typeof(GameCore).GetField(field, Private).GetValue(game); }

    private static void Prepare(FrontierRuntime app, int profession = 1)
    {
        Input.Clear(); GUI.queuedClick = null;
        // Each scenario owns its own input session. A failed prior
        // assertion must not strand a transient map cursor over the next
        // scenario's otherwise valid physical mouse hit surface.
        ((MapTileCursor)typeof(FrontierRuntime).GetField(
            "mapTileCursor",Private).GetValue(app)).Cancel();
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


    private static void HudQLateCooldownInputTriggersOneRealSkill(FrontierRuntime app)
    {
        foreach(int profession in new[]{0,1,2})
        foreach(bool compact in new[]{false,true})
        {
            Prepare(app,profession);
            GameCore g=app.Game;
            g.enemies.Clear();g.projectiles.Clear();
            g.playerAction=null;
            g.skillCooldown=.16f;g.save.energy=100f;
            Rect slot=compact?new Rect(430,732,78,50):
                new Rect(354,699,109,75);
            Input.mousePosition=new Vector3(slot.x+10,
                Screen.height-slot.y-10,0);
            Set(app,"combatHudTime",compact?3f:0f);
            GUI.queuedClick=slot;GUI.commands.Clear();
            Call(app,"OnGUI");
            Check(GUI.queuedClick==null&&g.SkillQueued&&
                g.playerAction==null&&g.save.energy==100f&&
                Math.Abs(g.skillCooldown-.16f)<.0001f,
                "Both live Q HUD layouts must accept genuine last-window input for class "+profession);
            int starts=0;
            for(int i=0;i<14;i++)
            {
                ActorAction before=g.playerAction;
                Update(app);
                ActorAction after=g.playerAction;
                if(after!=null&&after!=before&&after.kind=="skill")starts++;
            }
            Check(starts==1&&!g.SkillQueued&&g.skillCooldown>3.1f&&
                g.save.energy>=59.9f&&g.save.energy<65f,
                "Late HUD Q dispatches once on legitimate cooldown expiry for class "+profession);
        }
        Prepare(app,1);
        GameCore early=app.Game;early.enemies.Clear();
        early.skillCooldown=.4f;early.save.energy=100f;
        Rect unavailable=new Rect(354,699,109,75);
        Set(app,"combatHudTime",0f);
        Input.mousePosition=new Vector3(unavailable.x+10,
            Screen.height-unavailable.y-10,0);
        GUI.queuedClick=unavailable;Call(app,"OnGUI");
        Check(GUI.queuedClick!=null&&!early.SkillQueued&&
            early.playerAction==null&&early.save.energy==100f,
            "HUD Q stays unavailable before its authentic late-cooldown window");
        GUI.queuedClick=null;
        Console.WriteLine("PASS Both Q HUD layouts buffer one real last-window cast for all three classes");
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

    private static void ReducedMotionHeroInvulnerabilityStaysVisibleWithoutStrobing(
        FrontierRuntime app)
    {
        string[] classes={"Vanguard","Ranger","Arcanist"};
        foreach(int cls in new[]{0,1,2})
        {
            Prepare(app,cls);
            var g=app.Game;
            g.save.weapon=cls;g.playerAction=null;
            g.playerHitTime=0f;g.playerFlash=0f;
            g.dashTime=g.deathTime=0f;
            g.invulnerable=.47f;g.save.reducedMotion=true;
            float quietAlpha=-1f;
            foreach(float time in new[]{75f,75.1f,75.2f})
            {
                Set(app,"clock",time);GUI.textureCalls.Clear();
                Call(app,"DrawPlayerActor");
                var hero=GUI.textureCalls.FindAll(c=>c.texture.name.StartsWith(
                    "resource:Frontier/HeroAnimator/Canonical/Hero_"));
                Check(hero.Count==1&&hero[0].texture.name==
                    "resource:Frontier/HeroAnimator/Canonical/Hero_"+
                    classes[cls]+"_Idle_E",
                    "Reduced Motion must still render exactly one equipped original body");
                float alpha=hero[0].color.a;
                if(quietAlpha<0f)quietAlpha=alpha;
                else Check(Math.Abs(alpha-quietAlpha)<.00001f,
                    "Reduced Motion invulnerability pulse still strobes the protagonist");
                Check(alpha>.7f&&alpha<.9f,
                    "Reduced Motion invulnerability must remain readable, not hide the actor");
            }
            g.save.reducedMotion=false;
            Set(app,"clock",75f);GUI.textureCalls.Clear();
            Call(app,"DrawPlayerActor");
            var regularA=GUI.textureCalls.Find(c=>c.texture.name.StartsWith(
                "resource:Frontier/HeroAnimator/Canonical/Hero_"));
            Check(regularA!=null,"Standard motion hero missing");
            float first=regularA.color.a;
            Set(app,"clock",75.1f);GUI.textureCalls.Clear();
            Call(app,"DrawPlayerActor");
            var regularB=GUI.textureCalls.Find(c=>c.texture.name.StartsWith(
                "resource:Frontier/HeroAnimator/Canonical/Hero_"));
            Check(regularB!=null&&Math.Abs(regularB.color.a-first)>.20f,
                "Standard motion must retain the established invulnerability feedback");
        }
        Console.WriteLine("PASS 3 equipment-specific original hero Sprite paths: reduced-motion invulnerability stays steady while ordinary combat feedback remains intact; NATIVE_UNVERIFIED");
    }

    private static void CanonicalHeroHitRecoversOnSameWholeBody(FrontierRuntime app)
    {
        int reviewed=0;
        string[] equipment={"Vanguard","Ranger","Arcanist"};
        foreach(int cls in new[]{0,1,2})
        foreach(Point facing in new[]{
            new Point(1,0),new Point(-1,0),
            new Point(0,-1),new Point(0,1)})
        {
            Prepare(app,cls);
            var game=app.Game;game.save.reducedMotion=false;
            game.SetAim(facing);
            game.invulnerable=0f;
            float beforeHealth=game.save.health;
            var hurt=typeof(GameCore).GetMethod("Hurt",Private);
            Check(hurt!=null,"The actual C# hero Hurt contact method is required");
            hurt.Invoke(game,new object[]{9f,"environment",
                (Point?)new Point(1,0)});
            Check(game.save.health<beforeHealth&&
                game.HeroAnimationState==5&&game.playerHitTime>0f,
                "Real physical damage must enter the single-body Hit reaction");
            for(int i=0;i<30&&game.playerHitTime>.022f;i++)
                game.Tick(.02f,new Point());
            Check(game.HeroAnimationState==5,
                "The actual 180ms hit recoil must still be visible before its last painted recovery");
            Set(app,"clock",60f);GUI.textureCalls.Clear();
            Call(app,"DrawPlayerActor");
            var prior=GUI.textureCalls.Find(c=>c.texture.name.StartsWith(
                "resource:Frontier/HeroAnimator/Canonical/Hero_"));
            Check(prior!=null&&prior.texture.name.Contains("_Hit_"),
                "The active reaction still draws exactly one canonical full-body hit Sprite");
            for(int i=0;i<5&&game.playerHitTime>0f;i++)
                game.Tick(.02f,new Point());
            Set(app,"clock",60.025f);GUI.textureCalls.Clear();
            Call(app,"DrawPlayerActor");
            var rest=GUI.textureCalls.Find(c=>c.texture.name.StartsWith(
                "resource:Frontier/HeroAnimator/Canonical/Hero_"));
            Check(game.playerHitTime==0f&&rest!=null&&
                rest.texture.name==prior.texture.name&&
                Math.Abs(rest.uv.x-prior.uv.x)<.00001f,
                "Damage reaction must leave its original painted sole on screen briefly, never snap or repaint the body");
            Check(GUI.textureCalls.FindAll(c=>c.texture.name.StartsWith(
                "resource:Frontier/HeroAnimator/Canonical/Hero_")).Count==1,
                "Hit handoff cannot produce a second blond protagonist");
            // A W-to-E turn reuses the same source atlas row, but must
            // invalidate the old west-facing full-body Hit handoff.
            if(facing.x<0)
            {
                game.SetAim(new Point(1,0));
                Check(game.VisualFacing.x>.9f,
                    "Damage recovery turn setup did not change actual east aim");
                Set(app,"clock",60.036f);GUI.textureCalls.Clear();
                Call(app,"DrawPlayerActor");
                var turned=GUI.textureCalls.Find(c=>c.texture.name.StartsWith(
                    "resource:Frontier/HeroAnimator/Canonical/Hero_"));
                Check(turned!=null&&turned.texture.name==
                    "resource:Frontier/HeroAnimator/Canonical/Hero_"+
                    equipment[cls]+"_Idle_E",
                    "E/W reversal must cancel stale Hit even on the shared E atlas row");
            }
            // A real new attack may begin BEFORE the handoff expires.
            if(facing.x>0)
            {
                game.attackCooldown=0f;
                game.Attack(facing);
                Set(app,"clock",60.036f);GUI.textureCalls.Clear();
                Call(app,"DrawPlayerActor");
                var newBody=GUI.textureCalls.Find(c=>c.texture.name.StartsWith(
                    "resource:Frontier/HeroAnimator/Canonical/Hero_"));
                Check(game.playerAction!=null&&newBody!=null&&
                    newBody.texture.name.Contains("_Attack_"),
                    "A new real Attack must instantly interrupt painted Hit->Idle recovery");
            }
            else
            {
                Set(app,"clock",60.11f);GUI.textureCalls.Clear();
                Call(app,"DrawPlayerActor");
                var idle=GUI.textureCalls.Find(c=>c.texture.name.StartsWith(
                    "resource:Frontier/HeroAnimator/Canonical/Hero_"));
                Check(idle!=null&&idle.texture.name==
                    "resource:Frontier/HeroAnimator/Canonical/Hero_"+
                    equipment[cls]+"_Idle_"+
                    (Math.Abs(facing.y)>0f?(facing.y<0?"N":"S"):"E"),
                    "Bounded Hit recovery must return to authentic equipment-matched Idle");
            }
            reviewed++;
        }
        Console.WriteLine("PASS "+reviewed+
            " actual real-damage full-body Hit recovery handoffs across 3 equipment classes/4 facings, source pixels, early Attack interrupt and expiry; NATIVE_UNVERIFIED");
    }

    private static void CanonicalCombatRecoverySettlesWithoutExtraBody(FrontierRuntime app)
    {
        int checkedCases=0;
        string[] equipment={"Vanguard","Ranger","Arcanist"};
        foreach(int profession in new[]{0,1,2})
        foreach(bool skill in new[]{false,true})
        foreach(Point face in new[]{
            new Point(1,0),new Point(-1,0),
            new Point(0,-1),new Point(0,1)})
        {
            Prepare(app,profession);
            var g=app.Game;g.save.reducedMotion=false;
            g.SetAim(face);
            if(skill)g.Skill(face);else g.Attack(face);
            ActorAction action=g.playerAction;
            Check(action!=null&&!action.released,
                "Live combat must start an authored full-body windup");
            for(int step=0;step<90&&
                action.elapsed<action.duration-.042f;step++)
                g.Tick(.02f,new Point());
            Check(g.playerAction==action&&action.released&&
                action.elapsed<action.duration,
                "Action is in the real released late recovery, not a fabricated post-contact pose");
            Set(app,"clock",31f);
            GUI.textureCalls.Clear();Call(app,"DrawPlayerActor");
            var prior=GUI.textureCalls.Find(t=>t.texture.name.StartsWith(
                "resource:Frontier/HeroAnimator/Canonical/Hero_"));
            Check(prior!=null&&prior.texture.name.Contains("_"+
                (skill?"Cast":"Attack")+"_"),
                "Last painted combat recovery retains the actual current equipment's entire body");
            g.Tick(.05f,new Point());Set(app,"clock",31.025f);
            Check(g.playerAction==null,
                "Authoritative C# recovery must complete on schedule, regardless of visual handoff");
            GUI.textureCalls.Clear();Call(app,"DrawPlayerActor");
            var settle=GUI.textureCalls.Find(t=>t.texture.name.StartsWith(
                "resource:Frontier/HeroAnimator/Canonical/Hero_"));
            Check(settle!=null&&settle.texture.name==prior.texture.name&&
                Math.Abs(settle.uv.x-prior.uv.x)<.00001f&&
                Math.Abs(settle.uv.width-prior.uv.width)<.00001f,
                "One exact source-painted recovery must settle at class "+profession+
                ", skill "+skill+", facing "+face.x+","+face.y+
                " (previous "+prior.texture.name+", actual "+
                (settle==null?"none":settle.texture.name)+")");
            Check(GUI.textureCalls.FindAll(t=>t.texture.name.StartsWith(
                "resource:Frontier/HeroAnimator/Canonical/Hero_")).Count==1,
                "Short handoff may NEVER duplicate the blonde hero body");
            if(face.x<0)
            {
                g.SetAim(new Point(1,0));
                Check(g.VisualFacing.x>.9f,
                    "Contact recovery turn setup did not change actual east aim");
                Set(app,"clock",31.037f);GUI.textureCalls.Clear();
                Call(app,"DrawPlayerActor");
                var turned=GUI.textureCalls.Find(t=>t.texture.name.StartsWith(
                    "resource:Frontier/HeroAnimator/Canonical/Hero_"));
                Check(turned!=null&&turned.texture.name==
                    "resource:Frontier/HeroAnimator/Canonical/Hero_"+
                    equipment[profession]+"_Idle_E",
                    "E/W reversal must cancel prior Attack/Cast instead of mirroring recovery");
            }
            Set(app,"clock",31.16f);
            GUI.textureCalls.Clear();Call(app,"DrawPlayerActor");
            var idle=GUI.textureCalls.Find(t=>t.texture.name.StartsWith(
                "resource:Frontier/HeroAnimator/Canonical/Hero_"));
            Check(idle!=null&&idle.texture.name==
                "resource:Frontier/HeroAnimator/Canonical/Hero_"+
                equipment[profession]+"_Idle_"+
                (Math.Abs(face.y)>0f?(face.y<0?"N":"S"):"E"),
                "After the bounded settle interval return to the same equipment's real Idle, no permanent recovery lock");
            checkedCases++;
        }
        Console.WriteLine("PASS "+checkedCases+
            " actual full-body Attack/Cast stationary source recovery->Idle handoffs across 3 equipment sets and 4 facings (no duplicate body, unchanged game contact clock)");
    }

    private static void CombatSettleNeverMasksNewPhysicalAction(FrontierRuntime app)
    {
        int count=0;
        foreach(int equipment in new[]{0,1,2})
        foreach(bool skill in new[]{false,true})
        {
            Prepare(app,equipment);
            var g=app.Game;
            g.save.reducedMotion=false;
            g.SetAim(new Point(1f,0f));
            if(skill)g.Skill(new Point(1f,0f));
            else g.Attack(new Point(1f,0f));
            var a=g.playerAction;
            Check(a!=null,"Live action to prime painted settle missing");
            for(int i=0;i<90&&a.elapsed<a.duration-.042f;i++)
                g.Tick(.02f,new Point());
            Set(app,"clock",42f);GUI.textureCalls.Clear();
            Call(app,"DrawPlayerActor");
            g.Tick(.05f,new Point());
            Set(app,"clock",42.025f);GUI.textureCalls.Clear();
            Call(app,"DrawPlayerActor");
            var settled=GUI.textureCalls.Find(x=>x.texture.name.StartsWith(
                "resource:Frontier/HeroAnimator/Canonical/Hero_"));
            Check(settled!=null&&settled.texture.name.Contains(
                skill?"_Cast_":"_Attack_"),
                "A source-painted late contact actually reached visual settling");
            g.Dash(new Point(0f,-1f));
            Set(app,"clock",42.035f);GUI.textureCalls.Clear();
            Call(app,"DrawPlayerActor");
            var dodge=GUI.textureCalls.Find(x=>x.texture.name.StartsWith(
                "resource:Frontier/HeroAnimator/Canonical/Hero_"));
            Check(dodge!=null&&dodge.texture.name.Contains("_Dodge_"),
                "New physical dodge must immediately replace stationary recovery without a stale weapon body");
            for(int i=0;i<16;i++)g.Tick(.02f,new Point());
            g.attackCooldown=0;g.save.energy=100;
            g.Attack(new Point(0f,-1f));
            Set(app,"clock",42.43f);GUI.textureCalls.Clear();
            Call(app,"DrawPlayerActor");
            var fresh=GUI.textureCalls.Find(x=>x.texture.name.StartsWith(
                "resource:Frontier/HeroAnimator/Canonical/Hero_"));
            Check(g.playerAction!=null&&fresh!=null&&
                fresh.texture.name=="resource:Frontier/HeroAnimator/Canonical/Hero_"+
                    new[]{"Vanguard","Ranger","Arcanist"}[equipment]+"_Attack_N",
                "Fresh physical attack must select its new facing and the same equipped full body");
            count++;
        }
        Console.WriteLine("PASS "+count+
            " real released combat bodies stop cleanly on immediate Dodge and new Attack inputs without stale costume/pose");
    }

    private static void InvalidPhysicalContactCannotFakeHeroSettle(FrontierRuntime app)
    {
        int checkedCases=0;
        foreach(int equipment in new[]{0,1,2})
        foreach(bool skill in new[]{false,true})
        {
            Prepare(app,equipment);
            var g=app.Game;g.save.reducedMotion=false;
            g.SetAim(new Point(1f,0f));
            if(skill)g.Skill(new Point(1f,0f));
            else g.Attack(new Point(1f,0f));
            ActorAction action=g.playerAction;
            Check(action!=null&&!action.released,
                "Real physical action must prepare BEFORE its floor becomes obstructed");
            int row=(int)g.Position.y,col=(int)g.Position.x;
            string original=g.Map.rows[row];
            try
            {
                char[] stone=original.ToCharArray();
                stone[col]='#';g.Map.rows[row]=new string(stone);
                for(int i=0;i<40&&!action.released;i++)
                    g.Tick(.02f,new Point());
                Check(action.released&&action.blockedAtContact&&
                    g.effects.Exists(e=>e.kind=="projectileImpact"),
                    "Invalid caster footprint must create a genuine blocked contact instead of a successful swing/Q");
                g.Map.rows[row]=original;
                for(int i=0;i<45&&g.playerAction!=null&&
                    action.elapsed<action.duration-.042f;i++)
                    g.Tick(.02f,new Point());
                Set(app,"clock",71f);GUI.textureCalls.Clear();
                Call(app,"DrawPlayerActor");
                // The visibly released action itself remains one authored
                // complete body. Only its FALSE lingering celebration must
                // vanish after C# finishes the physically failed attempt.
                for(int i=0;i<7&&g.playerAction!=null;i++)
                    g.Tick(.02f,new Point());
                Set(app,"clock",71.025f);GUI.textureCalls.Clear();
                Call(app,"DrawPlayerActor");
                var body=GUI.textureCalls.Find(x=>x.texture.name.StartsWith(
                    "resource:Frontier/HeroAnimator/Canonical/Hero_"));
                string label=new[]{"Vanguard","Ranger","Arcanist"}[equipment];
                Check(g.playerAction==null&&body!=null&&body.texture.name==
                    "resource:Frontier/HeroAnimator/Canonical/Hero_"+
                    label+"_Idle_E",
                    "The failed "+label+(skill?" Q":" LMB")+
                    " must return directly to a complete painted Idle, without falsely lingering as a successful contact");
                checkedCases++;
            }
            finally {g.Map.rows[row]=original;}
        }
        Console.WriteLine("PASS "+checkedCases+
            " physical blocked Attack/Q actions never mint a visually successful post-contact hero settle");
    }

    private static void QueuedAreaPreviewDrawsRealWorldRadius(FrontierRuntime app)
    {
        foreach(int profession in new[]{0,1,2})
        {
            Prepare(app,profession);
            var g=app.Game;
            g.Attack(new Point(1f,0f));
            g.Skill(new Point(0f,-1f));
            Check(g.SkillQueued,
                "Actual player must own an unpaid Q intent before testing its rendered damage reach");
            GUI.textureCalls.Clear();
            Call(app,"DrawPlayerActor");
            // The actual production Ring draws 32 grounded 4px contour
            // contacts in this precisely authored translucent violet tint.
            // Compare the EAST contact to ScreenPoint of the real damage
            // range instead of trusting a HUD label or property-only test.
            float reach=profession==0?3.1f:profession==2?4.5f:0f;
            Point anchor=(Point)Call(app,"ScreenPoint",g.Position);
            Point edge=(Point)Call(app,"ScreenPoint",
                g.Position+new Point(reach,0));
            bool painted=GUI.textureCalls.Exists(c=>
                Math.Abs(c.color.r-.70f)<.001f&&
                Math.Abs(c.color.g-.56f)<.001f&&
                Math.Abs(c.color.b-1f)<.001f&&
                Math.Abs(c.color.a-.25f)<.001f&&
                Math.Abs(c.rect.x-(edge.x-2))<2f&&
                Math.Abs(c.rect.y-(anchor.y-2))<2f);
            Check(painted==(reach>0f),
                "Actual world draw must show only the physically correct queued Vanguard/Arcanist area radius, class "+profession);
            g.Dash(new Point(1f,0f));
            GUI.textureCalls.Clear();Call(app,"DrawPlayerActor");
            Check(!GUI.textureCalls.Exists(c=>
                Math.Abs(c.color.r-.70f)<.001f&&
                Math.Abs(c.color.g-.56f)<.001f&&
                Math.Abs(c.color.a-.25f)<.001f),
                "Real dodge interrupt must remove every stale area contour before the next frame");
        }
        // Draw through the production Sprite/OnGUI fallback at a real
        // wall. The contour to the EAST is occluded; the WEST spoke
        // remains visible without changing the same Q intent or pose.
        Prepare(app,2);
        var live=app.Game;
        live.Attack(new Point(1f,0f));
        live.Skill(new Point(0f,-1f));
        string original=live.Map.rows[14];
        try
        {
            char[] terrain=original.ToCharArray();
            terrain[14]='#';live.Map.rows[14]=new string(terrain);
            Check(live.SkillQueued&&live.CanStand(live.Position),
                "Production cover-test hero stays on legal foot terrain with a real queued Q");
            GUI.textureCalls.Clear();Call(app,"DrawPlayerActor");
            Point east=(Point)Call(app,"ScreenPoint",live.Position+new Point(4.5f,0f));
            Point west=(Point)Call(app,"ScreenPoint",live.Position+new Point(-4.5f,0f));
            Func<Point,bool> marker=screen=>GUI.textureCalls.Exists(c=>
                Math.Abs(c.color.r-.70f)<.001f&&
                Math.Abs(c.color.g-.56f)<.001f&&
                Math.Abs(c.color.b-1f)<.001f&&
                Math.Abs(c.color.a-.25f)<.001f&&
                Math.Abs(c.rect.x-(screen.x-2))<2f&&
                Math.Abs(c.rect.y-(screen.y-2))<2f);
            Check(!marker(east)&&marker(west),
                "Actual queued Q must not render through a real wall while keeping its reachable rear arc");
            live.Map.rows[14]=original;
            GUI.textureCalls.Clear();Call(app,"DrawPlayerActor");
            Check(marker(east)&&marker(west)&&live.SkillQueued,
                "Opening the same real wall restores Q's eastern contour without re-queuing the action");
        }
        finally{live.Map.rows[14]=original;}
        Console.WriteLine("PASS live player draw: exact damage-reach AoE, real-wall clipping, reopening and dodge cleanup");
    }

    private static void ProjectileWallContactKeepsOriginalSourceLight(FrontierRuntime app)
    {
        float[,] palette={
            {1f,1f,1f},
            {1f,.47f,.40f},
            {.60f,.91f,.72f},
            {.67f,.76f,1f},
            {.98f,.80f,.48f}
        };
        for(int light=0;light<=4;light++)
        {
            Prepare(app,0);
            GameCore g=app.Game;
            var shot=new Projectile{
                position=g.Position+new Point(1f,0),
                velocity=new Point(10f,0f),damage=12f,life=1.5f,
                friendly=light!=1,blockedAtSpawn=true,
                visual=light==1?"enemyArrow":
                    light==3?"starfire":"arrow",
                source=light==4?"deflected":
                    light==1?"shot":"attack",
                impact=light==0||light==1?null:
                    new ImpactProfile(light==2?1:light==3?2:0,
                        0,0,0,.1f,true,light==2?1:light==3?2:0)
            };
            // The authoritative blocked-at-muzzle transaction still
            // enters UpdateProjectiles and paints exactly one near-side
            // contact; do not fabricate a hit while the missile is flying.
            g.projectiles.Add(shot);
            g.Tick(.02f,new Point());
            Effect contact=g.effects.FindLast(e=>
                e.kind=="projectileImpact"&&
                (e.position-shot.position).Length<.01f);
            Check(contact!=null&&contact.variant==light&&
                !g.projectiles.Contains(shot),
                "Real GameCore blocked projectile lost its immutable "+
                "hostile/bow/staff/parry source contact "+light);
            GUI.textureCalls.Clear();
            Check((bool)Call(app,"DrawAuthoredEffect",contact)&&
                GUI.textureCalls.Exists(c=>
                    c.texture!=null&&
                    c.texture.name=="resource:Frontier/Art/Effects"&&
                    Math.Abs(c.color.r-palette[light,0])<.0001f&&
                    Math.Abs(c.color.g-palette[light,1])<.0001f&&
                    Math.Abs(c.color.b-palette[light,2])<.0001f),
                "Original published missile contact Sprite lost source light "+light);
            // A shot that already expired is a fade, not a fake
            // physically impactful hit with this glowing palette.
            g.effects.Clear();
            g.projectiles.Add(new Projectile{
                position=g.Position+new Point(1f,0f),
                velocity=new Point(10f,0f),friendly=true,
                life=0f,visual="arrow",impact=shot.impact
            });
            g.Tick(.02f,new Point());
            Check(!g.effects.Exists(e=>e.kind=="projectileImpact"),
                "Expired arrow fabricated a second wall contact glow");
        }
        Console.WriteLine("PASS five real GameCore missile contact ownership paints from existing Effects sprites: hostile, bow, staff, parry, neutral and no false expiry hits");
    }

    private static void EnemyArcherRepositionsForTrueArrowRadiusAndCancelsNewCover(FrontierRuntime app)
    {
        Prepare(app,0);
        var g=app.Game;
        int row=-1,col=-1;
        Point attacker=new Point(),victim=new Point();
        // Find two legal feet on an authentic straight firing corridor.
        // Paint a real corner immediately below (not IN) the sight ray.
        // The centerline must remain clear while the arrow's 0.14-tile
        // collision footprint strikes the wall beside that line.
        var lane=typeof(GameCore).GetMethod(
            "EnemyBowFlightLaneClear",Private);
        var sight=typeof(GameCore).GetMethod("ClearAttackPath",Private);
        Check(lane!=null&&sight!=null,
            "Actual bow radius-sweep firing gate is not in the game core");
        for(int y=4;y<g.Map.rows.Length-5&&row<0;y++)
        for(int x=4;x<g.Map.rows[y].Length-9&&row<0;x++)
        {
            bool open=true;
            for(int k=0;k<=6;k++)
                if(g.Map.rows[y][x+k]!='.'||
                   g.Map.rows[y+1][x+k]!='.')
                {open=false;break;}
            if(!open)continue;
            Point from=new Point(x+.5f,y+.88f);
            Point to=new Point(x+5.5f,y+.88f);
            if(!g.CanStand(from)||!g.CanStand(to))continue;
            var probe=new Enemy(99211,1,from.x,from.y,90f);
            if(!(bool)lane.Invoke(g,new object[]{probe,to}))continue;
            char[] blocked=g.Map.rows[y+1].ToCharArray();
            char old=blocked[x+2];
            blocked[x+2]='#';
            string oldRow=g.Map.rows[y+1];
            g.Map.rows[y+1]=new string(blocked);
            bool centerOpen=(bool)sight.Invoke(g,new object[]{from,to});
            bool shotOpen=(bool)lane.Invoke(g,new object[]{probe,to});
            g.Map.rows[y+1]=oldRow;
            if(centerOpen&&!shotOpen)
            {row=y+1;col=x+2;attacker=from;victim=to;}
        }
        Check(row>=0,"No genuine real-world arrow-side-corner corridor found");
        string original=g.Map.rows[row];
        try
        {
            var enemy=new Enemy(99212,1,attacker.x,attacker.y,90f){
                timer=.001f
            };
            enemy.Face(new Point(1,0));
            g.enemies.Add(enemy);
            g.save.x=victim.x;g.save.y=victim.y;
            char[] wall=original.ToCharArray();wall[col]='#';
            g.Map.rows[row]=new string(wall);
            Check((bool)sight.Invoke(g,new object[]{attacker,victim})&&
                !(bool)lane.Invoke(g,new object[]{enemy,victim}),
                "Archer cover scenario must be clear for actor LOS but physically block the arrow");
            g.Tick(.02f,new Point());
            Check(enemy.state!=1&&!g.projectiles.Exists(p=>!p.friendly),
                "Archer began a fully telegraphed attack into a known side-wall missile collision");
            Check(enemy.y<attacker.y-.005f&&
                enemy.x<attacker.x+.005f&&g.CanStand(enemy.Position),
                "Enemy archer failed to step SIDEWAYS to a physically shootable footing and instead kept walking into the same arrow-blocking corner");
            g.Map.rows[row]=original;
            enemy.timer=0f;enemy.stun=0f;enemy.releaseTime=0f;
            enemy.x=attacker.x;enemy.y=attacker.y;
            Check((bool)lane.Invoke(g,new object[]{enemy,victim}),
                "Removing side-wall obstruction did not free real archer arrow flight");
            g.Tick(.02f,new Point());
            Check(enemy.state==1&&enemy.windupDuration>.5f,
                "Archer did not reacquire a genuine ranged windup when physical flight reopened");
            g.Map.rows[row]=new string(wall);
            g.Tick(.02f,new Point());
            Check(enemy.state==0&&!g.projectiles.Exists(p=>!p.friendly),
                "A newly closed side wall did not interrupt an unpaid archer warning before shot release");
            Console.WriteLine("PASS actual enemy bow AI avoids side-wall swept-radius shots, repositions/reacquires an open lane, and cancels locked windups when cover closes");
        }
        finally
        {
            g.Map.rows[row]=original;
        }
    }

    private static void MeleeSwordContactStopsAtRealSolidCover(FrontierRuntime app)
    {
        foreach(bool wall in new[]{false,true})
        {
            Prepare(app,0);
            GameCore g=app.Game;
            g.save.weapon=0;
            // Find a genuinely open physical contact channel in the live
            // town map. A named floor tile is not necessarily clear:
            // static building colliders can occupy space over its art.
            Point planted=new Point();
            int row=-1,wallTile=-1;
            for(int y=4;y<g.Map.rows.Length-4&&row<0;y++)
            for(int x=4;x<g.Map.rows[y].Length-4&&row<0;x++)
            {
                if(g.Map.rows[y][x]!='.'||
                    g.Map.rows[y][x+1]!='.')continue;
                Point foot=new Point(x+.55f,y+.5f);
                if(!g.CanStand(foot))continue;
                bool clear=true;
                for(int sub=1;sub<=12;sub++)
                    if(g.Blocked(foot.x+.7f*sub/12f,foot.y))
                    {clear=false;break;}
                if(clear){planted=foot;row=y;wallTile=x+1;}
            }
            Check(row>=0,"No genuine free original sword contact lane exists in test town");
            g.save.x=planted.x;g.save.y=planted.y;
            string original=g.Map.rows[row];
            try
            {
                char[] cells=original.ToCharArray();
                cells[wallTile]=wall?'#':'.';
                g.Map.rows[row]=new string(cells);
                Check(g.CanStand(g.Position),
                    "Near-wall sword test must keep the actual hero on legal ground");
                g.Attack(new Point(1f,0f));
                bool released=false;
                for(int frame=0;frame<25;frame++)
                {
                    g.Tick(.02f,new Point());
                    if(g.effects.Exists(e=>e.kind=="slash"||
                        e.kind=="projectileImpact"))
                    {released=true;break;}
                }
                Check(released,
                    "Actual GameCore sword swing never reached an authored contact frame");
                Effect swordContact=g.effects.Find(e=>
                    e.kind==(wall?"projectileImpact":"slash"));
                Check(swordContact!=null,
                    "Sword VFX did not switch between original slash and wall impact");
                Check(!g.effects.Exists(e=>
                    e.kind==(wall?"slash":"projectileImpact")),
                    "A ghost full-body slash or collision spark is displayed on the wrong terrain");
                // Real attack contact happens AFTER the authored .34-tile
                // rooted advance in TickMovement/MoveAttackStep. Never
                // mistake this intended physical step for sprite drift:
                // the world-space spark is rooted to the hero's ACTUAL
                // release feet, not the original button-press position.
                Point actualReleaseRoot=g.Position;
                Check(Math.Abs(swordContact.position.y-actualReleaseRoot.y)<.001f&&
                    (wall?
                        swordContact.position.x<wallTile&&
                        swordContact.position.x>=actualReleaseRoot.x:
                        Math.Abs(swordContact.position.x-
                            (actualReleaseRoot.x+.7f))<.001f),
                    "Sword contact pivot crossed real stone or drifted from real release foot: blocked="+
                    wall+" pressRoot="+planted.x+","+planted.y+
                    " releaseRoot="+actualReleaseRoot.x+","+actualReleaseRoot.y+
                    " contact="+swordContact.position.x+","+swordContact.position.y+
                    " wallTile="+wallTile);
                GUI.textureCalls.Clear();
                Check((bool)Call(app,"DrawAuthoredEffect",swordContact)&&
                    GUI.textureCalls.Exists(c=>c.texture!=null&&
                        c.texture.name=="resource:Frontier/Art/Effects"),
                    "Real wall-safe sword contact failed to render original published Effects art");
            }
            finally{g.Map.rows[row]=original;}
        }
        Console.WriteLine("PASS actual sword LMB resolves full original slash on legal 0.7-tile contact, blocked near-side painted collision on stone, live map and true Sprite path");
    }

    private static void RealBasicWeaponHitsRetainFlightElementAfterEquipmentSwap(FrontierRuntime app)
    {
        foreach(int weapon in new[]{0,1,2})
        {
            Prepare(app,weapon);
            GameCore game=app.Game;
            game.save.weapon=weapon;
            game.save.profession=weapon;
            float distance=weapon==0?1.30f:3.3f;
            var target=new Enemy(92110+weapon,0,
                game.Position.x+distance,game.Position.y,2000f)
                {stun=40f,timer=40f};
            game.enemies.Add(target);
            // An authentic hit VFX stays rooted to contact while the enemy
            // body recoils in UpdateEnemies later in the SAME game tick.
            // Compare the painted contact to the immutable pre-hit root,
            // not the now-moved actor body's post-recoil position.
            Point contactRoot=target.Position;
            game.SetAimTarget(target.Position);
            game.Attack(new Point(1f,0f));
            bool launched=false,damaged=false;
            for(int frame=0;frame<120;frame++)
            {
                game.Tick(.02f,new Point());
                if(weapon>0&&!launched&&game.projectiles.Count>0)
                {
                    launched=true;
                    // The physical missile must keep its source weapon's
                    // hit light, even though GameCore's future avatar
                    // now displays a different equipped full-body class.
                    game.save.weapon=0;
                    game.save.profession=0;
                }
                if(target.hp>=2000f)continue;
                damaged=true;
                Effect contact=game.effects.FindLast(e=>
                    (e.kind=="impact"||e.kind=="spark")&&
                    (e.position-contactRoot).Length<.03f);
                Check(contact!=null&&
                    contact.variant==(weapon==0?0:weapon+1),
                    "Actual basic weapon "+weapon+
                    " lost its physical source-colored impact after the projectile flew");
                GUI.textureCalls.Clear();
                Call(app,"DrawAuthoredEffect",contact);
                Color expected=weapon==1?
                    new Color(.60f,.91f,.72f):
                    new Color(.67f,.76f,1f);
                if(weapon>0)
                    Check(GUI.textureCalls.Exists(c=>
                        c.texture!=null&&c.texture.name==
                        "resource:Frontier/Art/Effects"&&
                        Math.Abs(c.color.r-expected.r)<.0001f&&
                        Math.Abs(c.color.g-expected.g)<.0001f&&
                        Math.Abs(c.color.b-expected.b)<.0001f),
                        "Real bow/staff contact did not reuse its original painted element");
                else
                    Check(GUI.textureCalls.Exists(c=>
                        c.texture!=null&&c.texture.name==
                        "resource:Frontier/Art/Effects"&&
                        Math.Abs(c.color.r-1f)<.0001f&&
                        Math.Abs(c.color.g-1f)<.0001f&&
                        Math.Abs(c.color.b-1f)<.0001f),
                        "Physical sword impact no longer retains classic neutral artwork");
                break;
            }
            Check(damaged&&(weapon==0||launched),
                "Genuine sword/bow/staff basic attack did not reach actual target through authoritative flight");
        }
        Console.WriteLine("PASS actual sword, bow and starstaff basic attacks keep original white/green/blue source-painted physical hit contacts after midflight equipment change");
    }

    private static void RealProfessionQHitsTintOnlyDamageConfirmedContact(FrontierRuntime app)
    {
        float[,] palette={
            {.98f,.80f,.48f},
            {.60f,.91f,.72f},
            {.67f,.76f,1f}
        };
        for(int profession=0;profession<3;profession++)
        {
            Prepare(app,profession);
            GameCore g=app.Game;
            // All three skills make real physical attacks; a companion's
            // buff or a future preview must NEVER pretend this contact.
            g.enemies.Add(new Enemy(91121,0,14f,14f,2000f));
            g.enemies.Add(new Enemy(91122,2,10f,14f,2000f));
            g.Skill(new Point(1f,0f));
            for(int frame=0;frame<16;frame++)
            {
                g.Tick(.02f,new Point());
                if(frame<3)Check(!g.effects.Exists(e=>
                    (e.kind=="impact"||e.kind=="spark")&&
                    e.variant==profession+1),
                    "A player Q minted a premature technique contact before real release");
            }
            bool found=false;
            foreach(Effect contact in g.effects)
            {
                if(contact.kind!="impact"&&contact.kind!="spark")
                    continue;
                // Ranger Q genuinely launches flight projectiles; by
                // this tick they MAY have arrived and damaged enemies.
                // Their contact should then share the captured skill's
                // original green impact, never be mistaken for a fake hit.
                Check(contact.variant==profession+1,
                    "The actually damaged enemy lost its profession-owned technique contact");
                GUI.textureCalls.Clear();
                Call(app,"DrawAuthoredEffect",contact);
                Check(GUI.textureCalls.Exists(c=>
                    c.texture!=null&&c.texture.name==
                        "resource:Frontier/Art/Effects"&&
                    Math.Abs(c.color.r-palette[profession,0])<.0001f&&
                    Math.Abs(c.color.g-palette[profession,1])<.0001f&&
                    Math.Abs(c.color.b-palette[profession,2])<.0001f),
                    "Actual painted impact/spark Sprite did not display its owner technique tint");
                found=true;
            }
            if(profession!=1)Check(found,
                "True Vanguard/Arcanist Q dealt damage without an authentic contact Sprite");
        }
        // A genuine Ranger arrow can still be in flight when the player
        // changes vocation. Its physical damage and green source-owned
        // contact must not become the new Arcanist's blue.
        Prepare(app,1);
        var delayed=app.Game;
        var distant=new Enemy(91127,0,14f,14f,2000f)
            {stun=40f,timer=20f};
        delayed.enemies.Add(distant);
        delayed.SetAimTarget(distant.Position);
        delayed.Skill(new Point(1f,0f));
        bool launched=false;
        for(int frame=0;frame<25;frame++)
        {
            delayed.Tick(.02f,new Point());
            if(delayed.projectiles.Count>0){launched=true;break;}
        }
        Check(launched,
            "Actual Ranger Q failed to produce a physically flying arrow for hit-ownership test");
        delayed.save.profession=2;
        bool preserved=false;
        for(int frame=0;frame<45;frame++)
        {
            delayed.Tick(.02f,new Point());
            if(delayed.effects.Exists(e=>
                (e.kind=="impact"||e.kind=="spark")&&e.variant==2))
            {preserved=true;break;}
        }
        Check(preserved&&distant.hp<2000f,
            "Real Ranger arrow lost its launch-time green profession contact after switching to Arcanist");

        // Original idle/normal attack effects have no profession tint.
        Prepare(app,0);
        var ordinary=app.Game;
        ordinary.enemies.Add(new Enemy(91125,0,13.5f,14f,2000f));
        ordinary.Attack(new Point(1f,0f));
        for(int frame=0;frame<15;frame++)ordinary.Tick(.02f,new Point());
        Check(ordinary.effects.Exists(e=>
            (e.kind=="impact"||e.kind=="spark")&&e.variant==0),
            "Normal physical attacks lost their original neutral impact identity");
        Console.WriteLine("PASS real three-profession Q combat contacts tint only damage-confirmed approved Sprite impacts, original ordinary attack remains unchanged");
    }

    private static void QueuedAreaTagsOnlyPhysicalLivingEnemies(FrontierRuntime app)
    {
        for(int profession=0;profession<3;profession++)
        {
            Prepare(app,profession);
            var g=app.Game;
            Enemy east=new Enemy(88114,0,14.4f,14f,90f);
            Enemy west=new Enemy(88115,0,10.0f,14f,90f);
            g.enemies.Add(east);g.enemies.Add(west);
            g.Attack(new Point(1f,0f));
            g.Skill(new Point(0f,-1f));
            Check(g.SkillQueued,
                "Actual area test requires a deferred unpaid Q cast");
            Func<Enemy,bool> painted=enemy=>{
                Point screen=(Point)Call(app,"ScreenPoint",enemy.Position);
                return GUI.textureCalls.Exists(c=>
                    Math.Abs(c.color.r-.96f)<.001f&&
                    Math.Abs(c.color.g-.76f)<.001f&&
                    Math.Abs(c.color.b-.34f)<.001f&&
                    Math.Abs(c.color.a-.54f)<.001f&&
                    Math.Abs(c.rect.x-(screen.x+.42f*32f-2f))<2f&&
                    Math.Abs(c.rect.y-(screen.y-2f))<2f);
            };
            GUI.textureCalls.Clear();Call(app,"DrawPlayerActor");
            bool area=profession!=1;
            Check(painted(east)==area&&painted(west)==area,
                "Only Vanguard and Arcanist may outline living, visible, in-range enemies with real area Q");
            if(area)
            {
                int y=(int)g.Position.y;
                string original=g.Map.rows[y];
                try
                {
                    char[] terrain=original.ToCharArray();
                    terrain[(int)g.Position.x+1]='#';
                    g.Map.rows[y]=new string(terrain);
                    Check(!g.SkillQueuedAreaWouldHit(east)&&
                        g.SkillQueuedAreaWouldHit(west),
                        "Area Q target prediction must share live occlusion with actual strike");
                    GUI.textureCalls.Clear();Call(app,"DrawPlayerActor");
                    Check(!painted(east)&&painted(west),
                        "Enemy outline cannot remain beyond a newly shut real wall");
                    g.Map.rows[y]=original;
                    east.hp=0f;
                    GUI.textureCalls.Clear();Call(app,"DrawPlayerActor");
                    Check(!painted(east)&&painted(west),
                        "Defeated enemy is still shown as a payable Q contact");
                    g.Dash(new Point(1f,0f));
                    GUI.textureCalls.Clear();Call(app,"DrawPlayerActor");
                    Check(!painted(east)&&!painted(west),
                        "Dodge must retire all pending Q enemy target previews");
                }
                finally {g.Map.rows[y]=original;}
            }
        }
        Console.WriteLine("PASS both standard melee/mage Q abilities preview only actual living reachable enemies; live walls, deaths, Ranger exclusion and Dodge cancellation use real game draw");
    }

    private static void ReleasedAreaVfxStopsAtActualWorldCover(FrontierRuntime app)
    {
        foreach(int profession in new[]{0,2})
        {
            Prepare(app,profession);
            GameCore g=app.Game;
            g.save.x=12f;g.save.y=14f;
            g.SetAim(new Point(1f,0f));
            g.Skill(new Point(1f,0f));
            for(int frame=0;frame<16;frame++)g.Tick(.02f,new Point());
            Effect burst=g.effects.Find(e=>e.kind=="burst"&&
                e.playerAreaSkill);
            Check(burst!=null&&burst.radius>3f&&g.playerAction!=null&&
                g.playerAction.released,
                "Real Vanguard/Arcanist Q must own the ACTUAL released area effect on its contact frame");
            float radius=burst.radius;
            Point east=(Point)Call(app,"ScreenPoint",burst.position+
                new Point(radius,0f));
            Point west=(Point)Call(app,"ScreenPoint",burst.position+
                new Point(-radius,0f));
            Func<Point,bool> mark=screen=>GUI.textureCalls.Exists(c=>
                Math.Abs(c.rect.width-4f)<.01f&&
                Math.Abs(c.rect.height-4f)<.01f&&
                Math.Abs(c.rect.x-(screen.x-2f))<2f&&
                Math.Abs(c.rect.y-(screen.y-2f))<2f);
            int y=(int)g.Position.y,blockX=(int)g.Position.x+2;
            string old=g.Map.rows[y];
            try
            {
                char[] tiles=old.ToCharArray();
                tiles[blockX]='#';g.Map.rows[y]=new string(tiles);
                GUI.textureCalls.Clear();
                Check((bool)Call(app,"DrawAuthoredEffect",burst),
                    "Production source atlas must render an actual released Q contact");
                Check(!mark(east)&&mark(west),
                    "Released Q damage footprint cannot claim a wall-hidden east lane and must retain the real west arc");
                Check(GUI.textureCalls.Exists(c=>
                    c.rect.width>=60f&&c.rect.width<=105f),
                    "The published source VFX must stay a compact contact painting rather than scaling 32px pixels across the entire combat arena");
                g.Map.rows[y]=old;
                GUI.textureCalls.Clear();
                Call(app,"DrawAuthoredEffect",burst);
                Check(mark(east)&&mark(west),
                    "The same authentic released Q FX must show both legal directions after a real gate opens");
            }
            finally{g.Map.rows[y]=old;}
        }
        Console.WriteLine("PASS real released Vanguard/Arcanist AoE VFX keeps a painted center contact and live wall-clipped outer range");
    }

    private static void ExplosiveWallSplashDrawsOnlyPhysicalNearSide(FrontierRuntime app)
    {
        Prepare(app,2);
        GameCore g=app.Game;
        g.projectiles.Clear();g.effects.Clear();g.enemies.Clear();
        g.save.x=12f;g.save.y=14f;
        int row=(int)g.Position.y,blockX=14;
        string original=g.Map.rows[row];
        try
        {
            char[] blocked=original.ToCharArray();
            blocked[blockX]='#';g.Map.rows[row]=new string(blocked);
            Check(g.CanStand(g.Position)&&g.Blocked(14.5f,14.5f),
                "Authentic starfire cover test uses solid stone ahead of a legal source");
            var bolt=new Projectile{
                position=g.Position,velocity=new Point(20f,0f),
                friendly=true,explosive=true,visual="starfire",
                damage=20f,life=1f};
            g.projectiles.Add(bolt);
            // GameCore deliberately clamps each gameplay tick to 50ms.
            // Do not assume a 150ms call grants three frames of travel:
            // actually advance until the real swept bolt meets stone.
            for(int frame=0;frame<6&&g.projectiles.Contains(bolt);frame++)
                g.Tick(.05f,new Point());
            Effect impact=g.effects.Find(e=>e.kind=="magicImpact"&&
                Math.Abs(e.radius-1.8f)<.001f);
            Check(impact!=null&&!g.projectiles.Contains(bolt)&&
                impact.position.x<14f,
                "The actual Starfall projectile must first collide on the REAL near wall face");
            Point east=(Point)Call(app,"ScreenPoint",
                impact.position+new Point(impact.radius,0f));
            Point west=(Point)Call(app,"ScreenPoint",
                impact.position+new Point(-impact.radius,0f));
            Func<Point,bool> dot=screen=>GUI.textureCalls.Exists(c=>
                Math.Abs(c.rect.width-4f)<.01f&&
                Math.Abs(c.rect.height-4f)<.01f&&
                Math.Abs(c.rect.x-(screen.x-2f))<2f&&
                Math.Abs(c.rect.y-(screen.y-2f))<2f);
            GUI.textureCalls.Clear();
            Check((bool)Call(app,"DrawAuthoredEffect",impact),
                "Production renderer must display the real magic hit");
            Check(!dot(east)&&dot(west),
                "A REAL starfire splash must retain near-side feedback without drawing through stone");
            Check(GUI.textureCalls.Exists(c=>c.rect.width>=60f&&
                c.rect.width<=95f),
                "The source-painted starfire impact stays a compact contact glyph rather than a wall-piercing blurry disc");
            g.Map.rows[row]=original;
            GUI.textureCalls.Clear();Call(app,"DrawAuthoredEffect",impact);
            Check(dot(east)&&dot(west),
                "Opening the gate must restore both physically clear impact perimeter spokes");
        }
        finally{g.Map.rows[row]=original;}
        Console.WriteLine("PASS actual source-painted Starfall explosion uses a near-wall contact and dynamic 1.8-tile line-of-sight splash ring");
    }

    private static void QueuedRangerVolleyMarksLiveObstructedTarget(FrontierRuntime app)
    {
        Prepare(app,1);
        var g=app.Game;
        Point target=g.Position+new Point(5.5f,0f);
        g.SetAimTarget(target);
        g.Attack(new Point(1f,0f));
        g.Skill(new Point(1f,0f));
        Check(g.SkillQueued&&g.SkillQueuedTarget.HasValue&&
            (g.SkillQueuedTarget.Value-target).Length<.001f,
            "The actual Ranger draw starts with a real unpaid Q target");
        int row=(int)g.Position.y;
        string original=g.Map.rows[row];
        Point edge=(Point)Call(app,"ScreenPoint",target+new Point(.26f,0f));
        Func<Color,bool> marker=tint=>GUI.textureCalls.Exists(c=>
            Math.Abs(c.color.r-tint.r)<.001f&&
            Math.Abs(c.color.g-tint.g)<.001f&&
            Math.Abs(c.color.b-tint.b)<.001f&&
            Math.Abs(c.color.a-tint.a)<.001f&&
            Math.Abs(c.rect.x-(edge.x-2))<2f&&
            Math.Abs(c.rect.y-(edge.y-2))<2f);
        try
        {
            char[] cells=original.ToCharArray();
            cells[(int)g.Position.x+3]='#';
            g.Map.rows[row]=new string(cells);
            Check(!g.SkillQueuedRangerLaneClear&&
                (g.SkillQueuedRangerVolleyLaneMask&4)==0,
                "Live Q cover must close its real central arrow and physical five-ray preview");
            GUI.textureCalls.Clear();Call(app,"DrawPlayerActor");
            Check(marker(new Color(1f,.43f,.40f,.78f))&&
                !marker(new Color(.83f,.67f,1f,.72f)),
                "Real DrawPlayerActor must mark occluded queued Ranger Q with coral, not a false clear-lane violet");
            g.Map.rows[row]=original;
            Check(g.SkillQueuedRangerLaneClear&&
                (g.SkillQueuedRangerVolleyLaneMask&4)!=0,
                "Real Q center arrow must re-open after the wall is removed");
            int unobstructed=g.SkillQueuedRangerVolleyLaneMask;
            GUI.textureCalls.Clear();Call(app,"DrawPlayerActor");
            Check(marker(unobstructed==31?
                    new Color(.83f,.67f,1f,.72f):
                    new Color(.98f,.71f,.35f,.77f))&&
                !marker(new Color(1f,.43f,.40f,.78f))&&
                g.SkillQueued,
                "Opening real wall must restore a non-red Ranger target, without consuming Q");
            // One side of the five-shot arc now has a real wall while
            // the center still reaches the unchanged target. No generic
            // green-center reticle may hide this actual spread obstruction.
            string sideOriginal=g.Map.rows[row+1];
            try
            {
                char[] flank=sideOriginal.ToCharArray();
                flank[(int)g.Position.x+3]='#';
                g.Map.rows[row+1]=new string(flank);
                int physicalMask=g.SkillQueuedRangerVolleyLaneMask;
                Check((physicalMask&4)!=0&&(physicalMask&16)==0,
                    "Live Ranger volley did not distinguish clear center from blocked outer shot");
                GUI.textureCalls.Clear();Call(app,"DrawPlayerActor");
                Check(marker(new Color(.98f,.71f,.35f,.77f))&&
                    !marker(new Color(.83f,.67f,1f,.72f)),
                    "Partially blocked actual Ranger spread must warn amber, not falsely claim five clear shots");
                Point world=(Point)Call(app,"ScreenPoint",target);
                Check(GUI.textureCalls.Exists(c=>
                    Math.Abs(c.rect.x-(world.x-13+4*6))<2f&&
                    Math.Abs(c.rect.y-(world.y-18))<2f&&
                    Math.Abs(c.color.r-1f)<.001f&&
                    Math.Abs(c.color.g-.43f)<.001f&&
                    Math.Abs(c.color.a-.86f)<.001f),
                    "Production Ranger Q world cue did not mark its blocked outer spread lane");
            }
            finally{g.Map.rows[row+1]=sideOriginal;}
        }
        finally{g.Map.rows[row]=original;}
        Console.WriteLine("PASS real queued Ranger Q world target switches coral/violet with live projectile-radius wall collision");
    }

    private static void RiposteHudFollowsRealParryIntentAndUnreleasedContact(FrontierRuntime app)
    {
        Prepare(app,0);
        var g=app.Game;g.save.english=false;g.save.energy=100;
        g.invulnerable=0;g.Dash(new Point(1,0));
        var hurt=typeof(GameCore).GetMethod("Hurt",Private);
        Check(hurt!=null&&g.perfectDodgeArmed,
            "Real Dodge and enemy hurt contact route must be available");
        hurt.Invoke(g,new object[]{10f,"lunge",(Point?)new Point(-1,0)});
        Check(g.riposteTime>1.4f&&!g.perfectDodgeArmed,
            "True enemy contact inside real dodge invulnerability awards the timed counter");
        Set(app,"combatHudTime",0f);
        GUI.commands.Clear();GUI.buttonCalls.Clear();Call(app,"OnGUI");
        Check(GUI.commands.Exists(c=>c.Contains("反击就绪"))&&
            GUI.buttonCalls.Exists(c=>Math.Abs(c.rect.x-430)<.5f&&
                Math.Abs(c.rect.y-732)<.5f),
            "Actual Chinese compact HUD shows earned counter even with no nearby enemies, without blocking Q");
        g.save.english=true;g.save.teaUntil=g.save.totalTime+35f;
        GUI.commands.Clear();Call(app,"OnGUI");
        Check(GUI.commands.Exists(c=>c.Contains("RIPOSTE READY"))&&
            GUI.commands.Exists(c=>c.Contains("Tea ward")),
            "English counter HUD coexists with physical climate badge");
        var y=(float)typeof(FrontierRuntime).GetProperty("CounterHudY",Private)
            .GetValue(app,null);
        var toastY=(float)typeof(FrontierRuntime).GetProperty("CombatToastY",Private)
            .GetValue(app,null);
        Check(y==136f&&toastY>=y+27f,
            "Climate and riposte panels never overlap each other or the actual combat toast");
        g.dashTime=g.dashCooldown=g.attackCooldown=0;
        g.Attack(new Point(1,0));
        ActorAction swing=g.playerAction;
        Check(swing!=null&&swing.riposteArmed,
            "A real attack accepts an earned counter and holds the original contact clock");
        g.riposteTime=.001f;
        g.Tick(.02f,new Point());
        Check(g.riposteTime<=0&&swing.riposteArmed&&!swing.released,
            "The earned release stays armed even after its preparation outlasts the original countdown");
        GUI.commands.Clear();Call(app,"OnGUI");
        Check(GUI.commands.Exists(c=>c.Contains("RIPOSTE CHARGED"))&&
            !GUI.commands.Exists(c=>c.Contains("RIPOSTE READY")),
            "Actual English HUD never hides a committed-but-unreleased counter windup");
        for(int i=0;i<60&&!swing.released;i++)g.Tick(.02f,new Point());
        Check(swing.released&&!swing.riposteArmed,
            "The real action releases its stored bonus through production combat frames");
        GUI.commands.Clear();Call(app,"OnGUI");
        Check(!GUI.commands.Exists(c=>c.Contains("RIPOSTE READY"))&&
            !GUI.commands.Exists(c=>c.Contains("RIPOSTE CHARGED")),
            "No stale earned counter panel survives a genuine spent-contact frame");
        Console.WriteLine("PASS production combat HUD tracks actual earned and charged riposte across language, climate and contact");
    }



    private static void ActualRepairedSignalRoadOnlyShowsAtSafeLinkedWorldMast(FrontierRuntime app)
    {
        Prepare(app,0);
        var g=app.Game;g.enemies.Clear();g.save.stage=3;
        foreach(string flag in new[]{"intro","exp_intro","harbor_report",
            "desert_report","outer_relay_5"})
            if(!g.Has(flag))g.save.flags.Add(flag);
        // VisualReview starts inside its selected region, not necessarily
        // Emberfall. Both the source flag and its real Map must agree.
        foreach(string flag in new[]{"outer_log_","outer_relay_"})
            if(!g.Has(flag+g.save.room))g.save.flags.Add(flag+g.save.room);
        WorldObject mast=null;
        foreach(WorldObject o in g.Map.objects)
            if(o.kind=="outerrelay"){mast=o;break;}
        Check(mast!=null,"Original town contains its real rendered remote signal light");
        g.save.x=mast.x;g.save.y=mast.y;
        Set(app,"camera",g.Position);
        Check(g.OuterSignalRouteReady,
            "Two actually restored and unlocked lights allow a real courier route");
        GUI.commands.Clear();Call(app,"OnGUI");
        Check(GUI.commands.Exists(c=>c.Contains("巡路队的灯路已连通")),
            "Production world visibly marks the ACTUAL ready courier route beside its repaired source");
        g.save.english=true;
        GUI.commands.Clear();Call(app,"OnGUI");
        Check(GUI.commands.Exists(c=>c.Contains("Warden signal roads connected")),
            "Signal-road availability has a bilingual actual GameView marker");
        g.enemies.Add(new Enemy(97131,0,mast.x+1,mast.y,300f)
            {stun=99f,timer=999f});
        Check(!g.OuterSignalRouteReady,
            "A real attacker suspends the signal departure option");
        GUI.commands.Clear();Call(app,"OnGUI");
        Check(!GUI.commands.Exists(c=>c.Contains("Warden signal roads connected")),
            "Production world does not advertise a departure while raiders threaten the signal");
        g.enemies.Clear();
        g.save.flags.Remove("outer_relay_5");
        Check(!g.OuterSignalRouteReady,
            "A lone lit mast never advertises a destination that has not been restored");
        GUI.commands.Clear();Call(app,"OnGUI");
        Check(!GUI.commands.Exists(c=>c.Contains("Warden signal roads connected")),
            "World marker vanishes after the second endpoint is no longer authorized");
        g.save.flags.Add("outer_relay_5");
        g.save.x=mast.x+10f;
        Check(!g.OuterSignalRouteReady,
            "A distant player cannot operate the same physical courier lamp remotely");
        GUI.commands.Clear();Call(app,"OnGUI");
        Check(!GUI.commands.Exists(c=>c.Contains("Warden signal roads connected")),
            "GameView does not show a false reachable route when the hero has walked away");
        Console.WriteLine("PASS actual bilingual signal road marker follows live source, unlocked endpoint, real enemies and hero distance");
    }



    private static void UrgentRoadwardenDeliveryUsesOriginalWorldWaypointsAndLabels(FrontierRuntime app)
    {
        Prepare(app,0);
        var g=app.Game;g.enemies.Clear();
        int room=g.save.room;
        foreach(string flag in new[]{"outer_log_","outer_relay_",
            "outer_cache_","outer_patrol_done_"})
            if(!g.Has(flag+room))g.save.flags.Add(flag+room);
        WorldObject lamp=Array.Find(g.Map.objects,o=>o.kind=="outerrelay"),
            chest=Array.Find(g.Map.objects,o=>o.kind=="outercache"),
            post=Array.Find(g.Map.objects,o=>o.kind=="outerwatch");
        Check(lamp!=null&&chest!=null&&post!=null,
            "All three authentic shipped delivery sources exist in the chosen visual-review map");
        g.save.x=lamp.x;g.save.y=lamp.y;
        g.Talk(lamp);
        int start=Array.FindIndex(g.conversation.choices,
            c=>c.action=="outer_delivery_start");
        Check(start>=0,"Restored lamp exposes its authentic live courier choice");
        g.Choose(start);
        Check(g.OuterDeliveryActive&&g.save.trackedQuest=="outer_delivery"&&
            (g.NavigationTarget().Value-new Point(chest.x,chest.y)).Length<.01f,
            "Real source starts delivery and moves the actual pinned compass to the real chest");
        Set(app,"camera",new Point(lamp.x,lamp.y));
        GUI.commands.Clear();Call(app,"OnGUI");
        Check(GUI.commands.Exists(c=>c.Contains("急送中")),
            "Live region world draws its real remaining-time label over the originating signal");
        g.save.english=true;
        GUI.commands.Clear();Call(app,"OnGUI");
        Check(GUI.commands.Exists(c=>c.Contains("MEDICINE RUN")),
            "Production signal mission clock has a genuine English translation");
        g.save.x=chest.x;g.save.y=chest.y;
        g.Talk(chest);
        int pickup=Array.FindIndex(g.conversation.choices,
            c=>c.action=="outer_delivery_pickup");
        Check(pickup>=0,"Only the physical cache gives the mission pickup action");
        g.Choose(pickup);
        Check(g.OuterDeliveryStage==2&&
            (g.NavigationTarget().Value-new Point(post.x,post.y)).Length<.01f,
            "Actual pickup redirects the production compass to the defended post");
        Set(app,"camera",new Point(post.x,post.y));
        GUI.commands.Clear();Call(app,"OnGUI");
        Check(GUI.commands.Exists(c=>c.Contains("Deliver dressings here")),
            "Live watch scene shows the stage-two handoff rather than a fake chest pickup");
        g.save.x=post.x;g.save.y=post.y;g.Talk(post);
        g.Choose(Array.FindIndex(g.conversation.choices,
            c=>c.action=="outer_delivery_deliver"));
        Check(!g.OuterDeliveryActive&&g.OuterDeliveryDone(room),
            "Actual production dialogue completes the original post handoff");
        GUI.commands.Clear();Call(app,"OnGUI");
        Check(!GUI.commands.Exists(c=>c.Contains("Deliver dressings here"))&&
            !GUI.commands.Exists(c=>c.Contains("MEDICINE RUN")),
            "Mission visuals stop immediately after authentic one-time completion");
        Console.WriteLine("PASS true three-stop timed medicine courier uses bilingual world markers and two real stage waypoints");
    }

    private static void RealSignalSideCampCueRequiresMaterialSafeAndReachableMast(FrontierRuntime app)
    {
        Prepare(app,0);
        var g=app.Game;g.enemies.Clear();
        var mast=g.Map.objects==null?null:
            Array.Find(g.Map.objects,o=>o.kind=="outerrelay");
        Check(mast!=null,"Actual visual-review scene contains an original rescue-light mast");
        g.save.flags.Add("outer_relay_"+g.save.room);
        g.save.inventory[0]=2;
        g.save.x=mast.x;g.save.y=mast.y;
        Set(app,"camera",g.Position);
        Check(g.OuterSignalCampReady,
            "Restored and physically reached signal makes a real costed field stop available");
        GUI.commands.Clear();Call(app,"OnGUI");
        Check(GUI.commands.Exists(x=>x.Contains("可以搭棚短休")),
            "Chinese game world paints the legitimate one-timber signal camping service");
        g.save.english=true;
        GUI.commands.Clear();Call(app,"OnGUI");
        Check(GUI.commands.Exists(x=>x.Contains("Field rest ready")),
            "Same in-world camp is legible in English without touching combat or save data");
        g.save.inventory[0]=0;
        Check(!g.OuterSignalCampReady,
            "A missing actual timber disables material-priced rest without taking it");
        GUI.commands.Clear();Call(app,"OnGUI");
        Check(!GUI.commands.Exists(x=>x.Contains("Field rest ready")),
            "The visual must not promise a camp when timber is exhausted");
        g.save.inventory[0]=1;
        g.enemies.Add(new Enemy(97011,0,mast.x+1,mast.y,150f)
            {stun=100f,timer=100f});
        Check(!g.OuterSignalCampReady,
            "A real nearby enemy cancels the payable respite before a hit occurs");
        GUI.commands.Clear();Call(app,"OnGUI");
        Check(!GUI.commands.Exists(x=>x.Contains("Field rest ready")),
            "World cue disappears during dangerous enemy approach");
        g.enemies.Clear();
        g.save.x=mast.x+10f;g.save.y=mast.y;
        Check(!g.OuterSignalCampReady,
            "A remote player cannot claim this material-consuming source");
        GUI.commands.Clear();Call(app,"OnGUI");
        Check(!GUI.commands.Exists(x=>x.Contains("Field rest ready")),
            "No false rest marker remains after walking away from the mast");
        Console.WriteLine("PASS live bilingual material-priced rescue-lamp camp cue follows authentic reach, inventory and combat");
    }

    private static void RepairedReliefSignalShowsOnlyGenuineWorldRest(FrontierRuntime app)
    {
        Prepare(app,0);
        var g=app.Game;g.enemies.Clear();
        WorldObject mast=null;
        foreach(WorldObject o in g.Map.objects)
            if(o.kind=="outerrelay"){mast=o;break;}
        Check(mast!=null&&g.CanStand(new Point(mast.x,mast.y)),
            "Shipped playable map contains the real walkable eight-region rescue signal");
        g.save.x=mast.x;g.save.y=mast.y;
        Set(app,"camera",g.Position);
        GUI.commands.Clear();Call(app,"OnGUI");
        Check(!GUI.commands.Exists(line=>line.Contains("可以在灯下歇脚")),
            "Unrestored production lamp cannot falsely advertise free field care");
        g.save.flags.Add("outer_relay_"+g.save.room);
        Check(g.OuterRelayRestRefuge,
            "Genuine saved repair activates field care at the actual map mast");
        GUI.commands.Clear();Call(app,"OnGUI");
        Check(GUI.commands.Exists(line=>line.Contains("可以在灯下歇脚")),
            "Real production GameView reveals a readable Chinese safe-rest marker at the repaired lamp");
        g.save.english=true;
        GUI.commands.Clear();Call(app,"OnGUI");
        Check(GUI.commands.Exists(line=>line.Contains("Rest beside the light")),
            "The same physical repair shows a natural English cue without touching save text");
        g.enemies.Add(new Enemy(98401,0,mast.x+1f,mast.y,1000)
            {stun=99f,timer=999f});
        Check(!g.OuterRelayRestRefuge,
            "World marker responds immediately when live attackers make recovery unsafe");
        GUI.commands.Clear();Call(app,"OnGUI");
        Check(!GUI.commands.Exists(line=>line.Contains("Rest beside the light")),
            "GameView never advertises restoration during actual enemy combat");
        g.enemies.Clear();g.save.english=false;
        Console.WriteLine("PASS shipped rescue lamp only paints contextual safe-rest cue for repaired, locally safe, bilingual gameplay");
    }

    private static void ActualBossSupportCallIsVisibleAndRetractsAfterCombatBreak(FrontierRuntime app)
    {
        Prepare(app,0);
        var g=app.Game;g.enemies.Clear();g.save.english=false;
        g.invulnerable=100;g.save.reducedMotion=true;
        var sight=typeof(GameCore).GetMethod("ClearAttackPath",Private);
        Check(sight!=null,"Real Boss summon visual test uses the current production LOS");
        Point hero=new Point(),bossFoot=new Point();bool found=false;
        for(int y=4;y<g.Map.rows.Length-4&&!found;y++)
        for(int x=4;x<g.Map.rows[y].Length-4&&!found;x++)
        {
            Point p=new Point(x+.5f,y+.5f),b=p+new Point(-3,0);
            if(g.CanStand(p)&&g.CanStand(b)&&
                (bool)sight.Invoke(g,new object[]{b,p}))
            {hero=p;bossFoot=b;found=true;}
        }
        Check(found,"A real playable town scene contains a visible legal Boss/player pair");
        g.save.x=hero.x;g.save.y=hero.y;
        Set(app,"camera",hero);
        var boss=new Enemy(99201,3,bossFoot.x,bossFoot.y,999,true)
            {hp=190,cycle=2,timer=0,stagger=94};
        g.enemies.Add(boss);
        g.Tick(.02f,new Point());
        Check(boss.state==1&&g.effects.Exists(e=>e.bossSummonCueOwnerId==boss.id),
            "Production Boss AI commits the next third-cycle support windup");
        GUI.commands.Clear();GUI.textureCalls.Clear();GUI.buttonCalls.Clear();
        Call(app,"OnGUI");
        Check(GUI.commands.Exists(s=>s.Contains("正在呼叫增援")),
            "Real runtime GameView paints the upcoming summoning cue in Chinese above the active Boss, not merely in Core state");
        var hit=typeof(GameCore).GetMethod("Hit",Private);
        hit.Invoke(g,new object[]{boss,5f,false,(Point?)new Point(1,0),null});
        Check(boss.hp>0&&boss.state==0&&
            !g.effects.Exists(e=>e.bossSummonCueOwnerId==boss.id),
            "Actual combat Break interrupts the summoning Boss and retracts the pending message");
        GUI.commands.Clear();Call(app,"OnGUI");
        Check(!GUI.commands.Exists(s=>s.Contains("正在呼叫增援")),
            "Real runtime paints no stale announcement for a canceled summon");
        g.save.english=true;boss.stun=0;boss.hitTime=0;boss.releaseTime=0;boss.timer=0;
        g.Tick(.05f,new Point());
        Check(boss.state==1&&g.effects.Exists(e=>e.bossSummonCueOwnerId==boss.id),
            "Real second Boss attack starts after consuming actual impact hit-stop");
        GUI.commands.Clear();Call(app,"OnGUI");
        Check(GUI.commands.Exists(s=>s.Contains("CALLING REINFORCEMENTS!")),
            "Production runtime re-renders a genuine English support call after the original attack recovers");
        for(int k=0;k<65&&boss.cycle<3;k++)g.Tick(.02f,new Point());
        Check(boss.cycle==3&&g.enemies.Count==2&&
            !g.effects.Exists(e=>e.bossSummonCueOwnerId==boss.id),
            "The announced support is actually born only after timed Boss release");
        GUI.commands.Clear();Call(app,"OnGUI");
        Check(!GUI.commands.Exists(s=>s.Contains("CALLING REINFORCEMENTS!")),
            "A fulfilled support call also retires its real-world warning");
        Console.WriteLine("PASS actual bilingual Boss support cue, selective live Break retraction and real combat summon in FrontierRuntime.OnGUI");
    }

    private static void BossSupportCannotSpawnAcrossAnIsolatedArena(FrontierRuntime app)
    {
        Prepare(app,0);
        GameCore g=app.Game;
        g.save.english=true;g.Enter(9);g.enemies.Clear();
        Check(g.save.room==9,"Original Hearth Inn scene supplies a real test arena");
        MapData map=g.Map;
        string[] oldRows=(string[])map.rows.Clone();
        int width=map.rows[0].Length,height=map.rows.Length;
        Point hero=new Point(),bossFoot=new Point();bool found=false;
        Point[] summons={
            new Point(-2,1),new Point(2,1),new Point(-2,-1),new Point(2,-1)};
        for(int y=6;y<height-6&&!found;y++)
        for(int x=10;x<width-8&&!found;x++)
        {
            Point player=new Point(x+.5f,y+.5f);
            Point enemy=player+new Point(-4,0);
            if(!g.CanStand(player)||!g.CanStand(enemy)||
                g.NavigationPathFrom(enemy,player,true).Count==0)continue;
            bool validSummon=false;
            foreach(Point offset in summons)
            {
                Point foot=enemy+offset;
                if((foot-player).Length<1.25f||!g.CanStand(foot)||
                    !(bool)CallCore(g,"BossSummonHasLocalRoute",enemy,foot))
                    continue;
                validSummon=true;break;
            }
            if(!validSummon)continue;
            hero=player;bossFoot=enemy;found=true;
        }
        Check(found,"Authored Inn has a genuine open melee arena for Boss reinforcement");
        g.save.x=hero.x;g.save.y=hero.y;
        Enemy boss=new Enemy(97531,3,bossFoot.x,bossFoot.y,999,true)
            {hp=900,cycle=2,stagger=99};
        g.enemies.Add(boss);
        try
        {
            Check((bool)CallCore(g,"TrySpawnBossReinforcement",boss)&&
                g.enemies.Count==2,
                "An ordinary reachable Boss can still summon an actual reinforcement");
            g.enemies.RemoveAt(1);
            int partition=(int)Math.Floor(hero.x)-2;
            for(int y=0;y<height;y++)
            {
                char[] row=map.rows[y].ToCharArray();
                row[partition]='#';
                map.rows[y]=new string(row);
            }
            Check(g.CanStand(hero)&&g.CanStand(bossFoot)&&
                g.NavigationPathFrom(bossFoot,hero,true).Count==0,
                "A real full-height collision barrier isolates the hero from Boss combat");
            int before=g.enemies.Count;
            Check(!(bool)CallCore(g,"TrySpawnBossReinforcement",boss)&&
                g.enemies.Count==before,
                "Boss cannot populate an unreachable closed combat island with unusable summons");
            map.rows=oldRows;
            Check(g.NavigationPathFrom(bossFoot,hero,true).Count>0&&
                (bool)CallCore(g,"TrySpawnBossReinforcement",boss)&&
                g.enemies.Count==before+1,
                "Physically reopening the barrier allows the legitimate support summon again");
            Check(g.save.version==2&&g.save.room==9,
                "Summoning and barrier recovery do not modify SaveData schema or campaign room");
        }
        finally {map.rows=oldRows;}
        Console.WriteLine("PASS Boss summons require a real walking route to the hero, rejecting closed-wall islands and resuming after reopening");
    }

    private static void NearWallBossSummonDoesNotBypassPhysicalArenaIsolation(FrontierRuntime app)
    {
        Prepare(app,0);
        GameCore g=app.Game;
        g.Enter(9);g.enemies.Clear();
        MapData m=g.Map;string[] authored=(string[])m.rows.Clone();
        Point hero=new Point(),bossFoot=new Point();int wall=-1;bool found=false;
        // The hero/Boss stand less than 1.8 tiles apart, while each has
        // complete clearance on its own side of the original Inn terrain.
        for(int x=5;x<m.rows[0].Length-5&&!found;x++)
        for(int y=4;y<m.rows.Length-4&&!found;y++)
        {
            Point left=new Point(x-.27f,y+.5f),
                right=new Point(x+1.27f,y+.5f);
            Point recruit=left+new Point(-2f,1f);
            if(!g.CanStand(left)||!g.CanStand(right)||!g.CanStand(recruit)||
                !(bool)CallCore(g,"BossSummonHasLocalRoute",left,recruit))
                continue;
            // This is a genuinely authored, walkable local position;
            // only the temporary full-height wall can seal the arena.
            bossFoot=left;hero=right;wall=x;found=true;
        }
        Check(found,"Published Hearth Inn has a close physical Boss/player pair with a legal recruit foot");
        g.save.x=hero.x;g.save.y=hero.y;
        Enemy boss=new Enemy(97811,3,bossFoot.x,bossFoot.y,999,true)
            {hp=700,cycle=2,stagger=99};
        g.enemies.Add(boss);
        try
        {
            for(int y=0;y<m.rows.Length;y++)
            {
                char[] cells=m.rows[y].ToCharArray();
                cells[wall]='#';m.rows[y]=new string(cells);
            }
            Check(g.CanStand(hero)&&g.CanStand(bossFoot)&&
                (hero-bossFoot).Length<1.8f&&
                g.NavigationPathFrom(bossFoot,hero,true).Count==0,
                "Real solid wall separates two fully legal combat feet within old 1.8-tile shortcut");
            Check(!(bool)CallCore(g,"ActorHasLocalRoute",bossFoot,hero,5),
                "Original enemy movement cannot reach the near-side hero across the wall");
            int before=g.enemies.Count;
            Check(!(bool)CallCore(g,"TrySpawnBossReinforcement",boss)&&
                g.enemies.Count==before,
                "Boss refuses support when the near hero is physically on the other side of a wall");
            m.rows=authored;
            Check((bool)CallCore(g,"ActorHasLocalRoute",bossFoot,hero,5)&&
                (bool)CallCore(g,"TrySpawnBossReinforcement",boss)&&
                g.enemies.Count==before+1,
                "Reopened original arena permits the same legitimate Boss support call");
            Check(g.save.version==2,"Arena closure and recovery never change old save schema");
        }
        finally {m.rows=authored;}
        g.Close();
        Console.WriteLine("PASS Close-range Boss reinforcement requires real shared arena even across thin authored walls");
    }

    private static void SealedBossReleaseCannotPlantHazardsAcrossNewWalls(FrontierRuntime app)
    {
        Prepare(app,0);
        GameCore g=app.Game;
        // Enter the shipped regional maps through their real progression
        // gates: no synthetic room ID and no replacement collision scene.
        g.save.stage=4;
        if(!g.Has("exp_intro"))g.save.flags.Add("exp_intro");
        if(!g.Has("harbor_report"))g.save.flags.Add("harbor_report");
        if(!g.Has("desert_report"))g.save.flags.Add("desert_report");
        foreach(int region in new[]{5,6,7})
        {
            g.Enter(region);
            Check(g.save.room==region,"Regional Boss hazard test entered the actual arena");
            g.enemies.Clear();g.hazards.Clear();g.projectiles.Clear();
            MapData map=g.Map;
            string[] original=(string[])map.rows.Clone();
            Point bossFoot=new Point(),hero=new Point();
            int wall=-1;bool found=false;
            for(int x=5;x<map.rows[0].Length-5&&!found;x++)
            for(int y=5;y<map.rows.Length-5&&!found;y++)
            {
                Point left=new Point(x-.5f,y+.5f);
                Point right=new Point(x+1.5f,y+.5f);
                if(!g.CanStand(left)||!g.CanStand(right)||
                    !(bool)CallCore(g,"ClearAttackPath",left,right))
                    continue;
                bossFoot=left;hero=right;wall=x;found=true;
            }
            Check(found,"Published regional arena provides exposed legal Boss and hero feet");
            g.save.x=hero.x;g.save.y=hero.y;
            Enemy boss=new Enemy(98170+region,3,bossFoot.x,bossFoot.y,999,true)
                {hp=100,target=hero,cycle=2};
            g.enemies.Add(boss);
            try
            {
                boss.state=1;boss.timer=.01f;boss.windupDuration=1.1f;
                g.effects.Clear();
                g.Tick(.02f,new Point());
                int originalCount=g.hazards.Count;
                Check(g.effects.Exists(fx=>fx.kind=="burst" &&
                    (fx.position-hero).Length<.01f),
                    "An exposed real locked target still receives the authored impact flash");
                Check(boss.cycle==3&&originalCount>0,
                    "Live regional Boss releases every physically exposed strike, without requiring side-wall penetration");
                g.hazards.Clear();g.projectiles.Clear();
                for(int y=0;y<map.rows.Length;y++)
                {
                    char[] row=map.rows[y].ToCharArray();
                    row[wall]='#';map.rows[y]=new string(row);
                }
                Check(g.CanStand(hero)&&g.CanStand(bossFoot)&&
                    !(bool)CallCore(g,"ClearAttackPath",bossFoot,hero),
                    "Closing full-height wall isolates two still-valid feet after Boss warning");
                boss.state=1;boss.timer=.01f;boss.windupDuration=1.1f;
                boss.releaseTime=0f;boss.stun=0f;boss.hitTime=0f;
                boss.cycle=2; // Replay the identical phase/rotation across topology changes.
                g.effects.Clear();
                g.Tick(.02f,new Point());
                Check(!g.effects.Exists(fx=>fx.kind=="burst" &&
                    (fx.position-hero).Length<.01f),
                    "A sealed real arena cannot paint an impact flash on an isolated target");
                Check(boss.cycle==3&&g.hazards.Count==0,
                    "Actual sealed Boss release advances its cycle but creates no remote hazards");
                map.rows=original;
                Check((bool)CallCore(g,"ClearAttackPath",bossFoot,hero),
                    "Restoring the regional arena also restores its physical firing lane");
                boss.state=1;boss.timer=.01f;boss.windupDuration=1.1f;
                boss.releaseTime=0f;boss.stun=0f;boss.hitTime=0f;
                boss.cycle=2;
                g.hazards.Clear();g.projectiles.Clear();
                g.effects.Clear();
                g.Tick(.02f,new Point());
                Check(g.effects.Exists(fx=>fx.kind=="burst" &&
                    (fx.position-hero).Length<.01f),
                    "Reopening the genuine arena restores the original live impact painting");
                Check(boss.cycle==3&&g.hazards.Count==originalCount,
                    "Reopened authentic Boss release resumes every delayed regional strike");
                Check(g.save.version==2,"No migration or new SaveData fields during combat");
            }
            finally {map.rows=original;}
        }
        g.Close();
        Console.WriteLine("PASS real harbor, glass and highland Boss releases stay physical across closed and reopened arenas");
    }

    private static void LockedFrostBossStrikeRemainsDodgeableAfterPlayerMoves(FrontierRuntime app)
    {
        Prepare(app,0);
        GameCore g=app.Game;
        g.save.stage=4;
        if(!g.Has("exp_intro"))g.save.flags.Add("exp_intro");
        if(!g.Has("harbor_report"))g.save.flags.Add("harbor_report");
        if(!g.Has("desert_report"))g.save.flags.Add("desert_report");
        g.Enter(7);
        Check(g.save.room==7,"Actual published frost pass is available for live combat");
        g.enemies.Clear();g.hazards.Clear();g.projectiles.Clear();
        Point locked=new Point(),caster=new Point(),dodge=new Point();
        bool found=false;
        for(int y=8;y<g.Map.rows.Length-9&&!found;y++)
        for(int x=8;x<g.Map.rows[y].Length-9&&!found;x++)
        {
            Point target=new Point(x+.5f,y+.5f);
            Point source=target+new Point(-3f,0f);
            Point moved=target+new Point(5f,0f);
            if(!g.CanStand(target)||!g.CanStand(source)||
                !g.CanStand(moved)||
                !(bool)CallCore(g,"ClearAttackPath",source,target)||
                !(bool)CallCore(g,"ClearAttackPath",target,moved))
                continue;
            Point[] sides={new Point(0,3.5f),new Point(-3.5f,0),
                            new Point(0,-3.5f)};
            bool exposed=true;
            foreach(Point side in sides)
                if(!(bool)CallCore(g,"ClearAttackPath",source,target+side))
                    exposed=false;
            if(!exposed)continue;
            locked=target;caster=source;dodge=moved;found=true;
        }
        Check(found,"Shipped highland combat contains an exposed multi-bell arena and a reachable dodge lane");
        Enemy boss=new Enemy(99619,3,caster.x,caster.y,1000,true)
            {hp=550,target=locked,state=1,timer=.01f,
             windupDuration=1.1f,cycle=0};
        g.enemies.Add(boss);
        // The casting warning was fixed at locked before the hero moved;
        // the player reached genuine ground through an unobstructed exit.
        g.save.x=dodge.x;g.save.y=dodge.y;
        g.Tick(.02f,new Point());
        Check(boss.cycle==1&&g.hazards.Count==4,
            "A real phase-two frost Boss releases one mark and three delayed bells");
        Hazard mark=g.hazards.Find(h=>h.kind=="rift"&&
            System.Object.ReferenceEquals(h.bossOwner,boss));
        Check(mark!=null&&(mark.position-locked).Length<.001f&&
            (mark.position-dodge).Length>4f,
            "The live warning contact stays where it was aimed, not on the escaping hero");
        Check(!g.hazards.Exists(h=>(h.position-dodge).Length<1.5f),
            "Dodge across the unmarked side does not spawn a new homing frost strike");
        Check(g.save.version==2,"Target lock is combat-only and does not migrate old saves");
        g.Close();
        Console.WriteLine("PASS real frost Boss keeps its locked mark and rotating bells after a legal player dodge");
    }

    private static void BossSecondaryStrikesStayOnCasterSideOfLiveCover(FrontierRuntime app)
    {
        Prepare(app,0);
        GameCore g=app.Game;
        g.save.stage=4;
        if(!g.Has("exp_intro"))g.save.flags.Add("exp_intro");
        g.Enter(5);
        Check(g.save.room==5,"The shipped harbor arena is actually running");
        g.enemies.Clear();g.hazards.Clear();g.projectiles.Clear();
        MapData map=g.Map;
        Point center=new Point(),caster=new Point();
        int wallX=-1,wallY=-1;bool found=false;
        // Find a real open casting lane whose right-hand tide circle
        // becomes covered by a newly raised ONE-tile side wall. Both the
        // primary and left lanes must remain physically accessible.
        for(int y=7;y<map.rows.Length-7&&!found;y++)
        for(int x=7;x<map.rows[y].Length-7&&!found;x++)
        {
            Point target=new Point(x+.5f,y+.5f);
            Point source=target+new Point(0,-3f);
            Point left=target+new Point(-2f,0);
            Point right=target+new Point(2f,0);
            if(!g.CanStand(target)||!g.CanStand(source)||
                !g.CanStand(left)||!g.CanStand(right)||
                !(bool)CallCore(g,"ClearAttackPath",source,target)||
                !(bool)CallCore(g,"ClearAttackPath",source,left)||
                !(bool)CallCore(g,"ClearAttackPath",source,right))
                continue;
            int barrierX=x+1,barrierY=y-1;
            string original=map.rows[barrierY];
            char[] row=original.ToCharArray();row[barrierX]='#';
            map.rows[barrierY]=new string(row);
            bool split=g.CanStand(source)&&g.CanStand(target)&&
                g.CanStand(left)&&g.CanStand(right)&&
                (bool)CallCore(g,"ClearAttackPath",source,target)&&
                (bool)CallCore(g,"ClearAttackPath",source,left)&&
                !(bool)CallCore(g,"ClearAttackPath",source,right);
            map.rows[barrierY]=original;
            if(!split)continue;
            center=target;caster=source;
            wallX=barrierX;wallY=barrierY;found=true;
        }
        Check(found,"Shipped harbor has a genuine open lane with a flank-blocking single wall");
        g.save.x=center.x;g.save.y=center.y;
        Enemy boss=new Enemy(99601,3,caster.x,caster.y,1000f,true)
            {hp=1000f,target=center,cycle=0};
        g.enemies.Add(boss);
        string authored=map.rows[wallY];
        try
        {
            // First show the authentic unrestricted three-wave release.
            boss.state=1;boss.timer=.01f;boss.windupDuration=1.1f;
            g.Tick(.02f,new Point());
            Check(boss.cycle==1&&g.hazards.Count==3,
                "Original exposed tide Boss still releases all three combat circles");
            Check(g.hazards.TrueForAll(h=>Math.Abs(h.timer-1.2f)<.001f),
                "A genuine mid-frame harbor release retains all 1.2 seconds of its authored warning");
            g.Tick(.02f,new Point());
            Check(g.hazards.Count==3&&
                g.hazards.TrueForAll(h=>Math.Abs(h.timer-1.18f)<.001f),
                "The warned circle ages only in the first real frame after release");
            g.hazards.Clear();g.projectiles.Clear();
            char[] row=map.rows[wallY].ToCharArray();
            row[wallX]='#';map.rows[wallY]=new string(row);
            Check((bool)CallCore(g,"ClearAttackPath",caster,center)&&
                !(bool)CallCore(g,"ClearAttackPath",caster,center+new Point(2,0)),
                "The new wall obstructs just the side circle, not the telegraphed center");
            boss.state=1;boss.timer=.01f;boss.windupDuration=1.1f;
            boss.stun=boss.hitTime=boss.releaseTime=0;
            // Phase 1 stays horizontal regardless of cycle.
            g.Tick(.02f,new Point());
            Check(boss.cycle==2&&g.hazards.Count==2&&
                g.hazards.Exists(h=>(h.position-center).Length<.01f)&&
                g.hazards.Exists(h=>(h.position-(center+new Point(-2,0))).Length<.01f)&&
                !g.hazards.Exists(h=>(h.position-(center+new Point(2,0))).Length<.01f),
                "Real combat retains two exposed tide strikes but cannot cast the blocked flank");
            map.rows[wallY]=authored;
            boss.state=1;boss.timer=.01f;boss.windupDuration=1.1f;
            boss.stun=boss.hitTime=boss.releaseTime=0;
            g.hazards.Clear();g.projectiles.Clear();
            g.Tick(.02f,new Point());
            Check(boss.cycle==3&&g.hazards.Count==3,
                "Reopening the same real wall restores the complete Boss attack");
            Check(g.save.version==2,"No saved terrain or combat-schema migration");
        }
        finally {map.rows[wallY]=authored;}
        g.Close();
        Console.WriteLine("PASS live harbor three-lane tide Boss clips only the physically occluded flank and restores it after reopening");
    }

    private static void CompanionAutoDefenseDistinguishesSealedFromLiveBossAttacks(FrontierRuntime app)
    {
        Prepare(app,0);
        GameCore g=app.Game;
        g.save.stage=4;
        if(!g.Has("exp_intro"))g.save.flags.Add("exp_intro");
        g.Enter(5);
        Check(g.save.room==5,"The real published harbor arena is active");
        g.enemies.Clear();g.hazards.Clear();g.projectiles.Clear();
        g.save.companion=1;
        MapData map=g.Map;
        Point hero=new Point(),caster=new Point(),ally=new Point(),realCaster=new Point();
        int blockedX=-1,blockedY=-1;bool found=false;
        for(int y=8;y<map.rows.Length-8&&!found;y++)
        for(int x=8;x<map.rows[y].Length-8&&!found;x++)
        {
            Point h=new Point(x+.5f,y+.5f),a=h+new Point(-3,0),
                f=h+new Point(0,-2),b=h+new Point(0,3),
                locked=h+new Point(1,0);
            if(!g.CanStand(h)||!g.CanStand(a)||!g.CanStand(f)||
                !g.CanStand(b)||!g.CanStand(locked)||
                !(bool)CallCore(g,"ClearAttackPath",a,h)||
                !(bool)CallCore(g,"ClearAttackPath",b,h)||
                !(bool)CallCore(g,"ClearAttackPath",f,a)||
                !(bool)CallCore(g,"ClearAttackPath",f,b)||
                !(bool)CallCore(g,"ClearAttackPath",a,locked)||
                !(bool)CallCore(g,"ClearAttackPath",f,locked))
                continue;
            string original=map.rows[y];
            char[] row=original.ToCharArray();row[x+1]='#';
            map.rows[y]=new string(row);
            bool split=g.CanStand(h)&&g.CanStand(a)&&g.CanStand(f)&&
                g.CanStand(b)&&
                (bool)CallCore(g,"ClearAttackPath",a,h)&&
                !(bool)CallCore(g,"ClearAttackPath",a,locked)&&
                (bool)CallCore(g,"ClearAttackPath",b,h);
            map.rows[y]=original;
            if(!split)continue;
            hero=h;caster=a;ally=f;realCaster=b;
            blockedX=x+1;blockedY=y;found=true;
        }
        Check(found,"Authentic harbor map has a defensible casting lane and separate threatened flank");
        g.save.x=hero.x;g.save.y=hero.y;
        g.companionPosition=ally;
        Enemy sealedBoss=new Enemy(99901,3,caster.x,caster.y,900,true)
            {state=1,timer=.10f,windupDuration=1.1f,target=hero+new Point(1,0)};
        Enemy realBoss=new Enemy(99902,3,realCaster.x,realCaster.y,900,true)
            {state=1,timer=.22f,windupDuration=1.1f,target=hero};
        g.enemies.Add(sealedBoss);g.enemies.Add(realBoss);
        string authored=map.rows[blockedY];
        try
        {
            Check((bool)CallCore(g,"CompanionCommittedHeroStrike",sealedBoss)&&
                (bool)CallCore(g,"CompanionCommittedHeroStrike",realBoss),
                "Both authentic unobstructed Boss warnings initially threaten the player's physical foot");
            char[] row=authored.ToCharArray();row[blockedX]='#';
            map.rows[blockedY]=new string(row);
            Check(!(bool)CallCore(g,"CompanionCommittedHeroStrike",sealedBoss)&&
                (bool)CallCore(g,"CompanionCommittedHeroStrike",realBoss),
                "A new side wall removes only the occluded Boss threat, not the exposed guardian");
            Enemy selected=(Enemy)CallCore(g,"NearestCompanionThreat");
            Check(System.Object.ReferenceEquals(selected,realBoss),
                "Actual ally threat selection ignores faster false AoE and reserves its attack for genuine hero danger");
            map.rows[blockedY]=authored;
            Check((bool)CallCore(g,"CompanionCommittedHeroStrike",sealedBoss),
                "Reopening the side wall restores the originally valid locked Boss strike");
            Check(g.save.version==2,
                "Ally threat priorities use only ephemeral terrain and attack state");
        }
        finally {map.rows[blockedY]=authored;}
        g.Close();
        Console.WriteLine("PASS real harbor companion prioritizes physically exposed incoming Boss area over a faster occluded warning");
    }

    private static void MalformedBossIntentCancelsAndRecoversRealCombat(FrontierRuntime app)
    {
        Prepare(app,0);
        GameCore g=app.Game;
        g.save.stage=4;
        if(!g.Has("exp_intro"))g.save.flags.Add("exp_intro");
        g.Enter(5);
        Check(g.save.room==5,"Actual published harbor encounter supports recovered Boss intent");
        g.enemies.Clear();g.hazards.Clear();g.projectiles.Clear();g.effects.Clear();
        Point target=new Point(),caster=new Point();bool found=false;
        for(int y=7;y<g.Map.rows.Length-7&&!found;y++)
        for(int x=7;x<g.Map.rows[y].Length-7&&!found;x++)
        {
            Point center=new Point(x+.5f,y+.5f);
            Point foot=center+new Point(-3f,0);
            if(!g.CanStand(center)||!g.CanStand(foot)||
                !(bool)CallCore(g,"ClearAttackPath",foot,center))
                continue;
            target=center;caster=foot;found=true;
        }
        Check(found,"A shipped harbor lane supplies finite legitimate caster and locked target feet");
        g.save.x=target.x;g.save.y=target.y;
        Enemy boss=new Enemy(99731,3,caster.x,caster.y,1000f,true)
        {
            state=1,timer=.01f,windupDuration=1.1f,
            target=new Point(float.NaN,target.y),
            facing=new Point(float.NegativeInfinity,0),
            attackOrigin=new Point(float.NaN,0),
            attackDestination=new Point(float.PositiveInfinity,0)
        };
        g.enemies.Add(boss);
        var warning=new Effect("number",caster,.8f,"CALLING")
            {bossSummonCueOwner=boss};
        g.effects.Add(warning);
        g.Tick(.02f,new Point());
        Check(boss.state==0&&boss.timer>.2f&&boss.timer<.23f&&
            !g.effects.Contains(warning)&&g.hazards.Count==0,
            "Malformed action is cancelled and only its own live summons warning is retracted");
        Check((boss.target-caster).Length<.01f&&
            (boss.attackOrigin-caster).Length<.01f&&
            (boss.attackDestination-caster).Length<.01f&&
            Math.Abs(boss.facing.x-1)<.001f&&Math.Abs(boss.facing.y)<.001f,
            "Nonfinite target, facing and attack endpoints recover to real finite feet");
        Check(!g.effects.Exists(fx=>fx.kind=="burst"),
            "Corrupted Boss intent cannot emit an unrelated hit flash");
        boss.target=target;boss.state=1;boss.timer=.01f;
        boss.windupDuration=1.1f;boss.stun=boss.hitTime=boss.releaseTime=0;
        g.Tick(.02f,new Point());
        Check(boss.cycle==1&&g.hazards.Count>0,
            "Recovered Boss still casts its original physical delayed attack on the next valid windup");
        Check(g.save.version==2,
            "Enemy intent repair remains ephemeral without save format changes");
        g.Close();
        Console.WriteLine("PASS invalid live Boss target, facing and dash endpoints cancel safely then recover genuine tide combat");
    }

    private static void RealGroundHazardsRejectCorruptTransientContacts(FrontierRuntime app)
    {
        Prepare(app,0);
        GameCore g=app.Game;
        g.save.stage=4;
        if(!g.Has("exp_intro"))g.save.flags.Add("exp_intro");
        g.Enter(5);
        Check(g.save.room==5,"Shipped harbor world is available for ground-hazard contact");
        g.enemies.Clear();g.hazards.Clear();g.projectiles.Clear();
        Point foot=new Point();bool found=false;
        for(int y=6;y<g.Map.rows.Length-6&&!found;y++)
        for(int x=6;x<g.Map.rows[y].Length-6&&!found;x++)
        {
            Point p=new Point(x+.5f,y+.5f);
            if(!g.CanStand(p))continue;
            foot=p;found=true;
        }
        Check(found,"The real harbor map offers an actual collision-valid impact tile");
        g.save.x=foot.x;g.save.y=foot.y;
        g.save.maxHealth=g.save.health=1000;g.invulnerable=0;
        Hazard future=new Hazard{position=foot,radius=1.1f,
            timer=.4f,damage=9f,kind="tide"};
        Hazard imminent=new Hazard{position=foot,radius=1.1f,
            timer=.01f,damage=12f,kind="tide"};
        Hazard badClock=new Hazard{position=foot,radius=1f,
            timer=float.NaN,damage=90,kind="tide"};
        Hazard badPosition=new Hazard{position=new Point(float.PositiveInfinity,foot.y),
            radius=1f,timer=.01f,damage=90,kind="tide"};
        Hazard badRadius=new Hazard{position=foot,radius=float.NaN,
            timer=.01f,damage=90,kind="tide"};
        Hazard badDamage=new Hazard{position=foot,radius=1f,
            timer=.01f,damage=float.PositiveInfinity,kind="tide"};
        Hazard negativeDamage=new Hazard{position=foot,radius=1f,
            timer=.01f,damage=-500,kind="tide"};
        Hazard absentKind=new Hazard{position=foot,radius=1f,
            timer=.01f,damage=90,kind=null};
        g.hazards.Add(null);
        g.hazards.Add(badClock);g.hazards.Add(badPosition);
        g.hazards.Add(badRadius);g.hazards.Add(badDamage);
        g.hazards.Add(negativeDamage);g.hazards.Add(absentKind);
        g.hazards.Add(future);g.hazards.Add(imminent);
        float before=g.save.health;
        g.Tick(.02f,new Point());
        Check(g.hazards.Count==1&&g.hazards[0]==future&&
            future.timer>.3f&&future.timer<.4f,
            "Only the valid future warning remains; invalid or expired contacts are retired");
        Check(g.save.health<before&&g.save.health>before-20f,
            "Only the one genuine tide contact can harm the player, never nonfinite or negative attacks");
        Check(g.save.version==2,
            "Hazard validation is transient and does not alter SaveData v2");
        g.Close();
        Console.WriteLine("PASS real harbor hazard clock validates live positions/radii/damage and resolves just the legitimate circle");
    }

    private static void DefeatedBossRetiresOnlyItsOwnDelayedGroundAttacks(FrontierRuntime app)
    {
        Prepare(app,0);
        GameCore g=app.Game;
        g.save.stage=4;
        if(!g.Has("exp_intro"))g.save.flags.Add("exp_intro");
        g.Enter(5);
        Check(g.save.room==5,"The actual harbor arena is unlocked for Boss retirement");
        g.enemies.Clear();g.hazards.Clear();
        Point hero=new Point(),one=new Point(),two=new Point();bool found=false;
        for(int x=7;x<g.Map.rows[0].Length-7&&!found;x++)
        for(int y=6;y<g.Map.rows.Length-6&&!found;y++)
        {
            Point p=new Point(x+.5f,y+.5f);
            Point a=p+new Point(-3,0),b=p+new Point(-3,2);
            if(!g.CanStand(p)||!g.CanStand(a)||!g.CanStand(b)||
                !(bool)CallCore(g,"ClearAttackPath",a,p)||
                !(bool)CallCore(g,"ClearAttackPath",b,p))
                continue;
            hero=p;one=a;two=b;found=true;
        }
        Check(found,"The shipped harbor map has two distinct legitimate Boss casting feet");
        g.save.x=hero.x;g.save.y=hero.y;
        Enemy defeated=new Enemy(99101,3,one.x,one.y,999,true)
            {hp=10,target=hero,cycle=2};
        Enemy survivor=new Enemy(99102,3,two.x,two.y,999,true)
            {hp=700,target=hero,cycle=2};
        g.enemies.Add(defeated);g.enemies.Add(survivor);
        // Use the exact production spell generator for both guardians.
        CallCore(g,"BossExpansionAttack",defeated);
        CallCore(g,"BossExpansionAttack",survivor);
        var ambient=new Hazard{position=hero,timer=2f,radius=1f,
            damage=5f,kind="tide"};
        g.hazards.Add(ambient);
        Check(g.hazards.FindAll(h=>System.Object.ReferenceEquals(h.bossOwner,defeated)).Count==3&&
              g.hazards.FindAll(h=>System.Object.ReferenceEquals(h.bossOwner,survivor)).Count==3,
            "Two independent Bosses have separately owned real delayed tide sweeps");
        CallCore(g,"Hit",defeated,1200f,false,null,null);
        Check(!g.enemies.Contains(defeated)&&g.enemies.Contains(survivor)&&
            !g.hazards.Exists(h=>System.Object.ReferenceEquals(h.bossOwner,defeated))&&
            g.hazards.FindAll(h=>System.Object.ReferenceEquals(h.bossOwner,survivor)).Count==3&&
            g.hazards.Contains(ambient),
            "Genuine lethal combat clears only the killed Boss's pending contacts");
        Check(g.Has("boss_5")&&g.save.version==2,
            "Normal Boss progression and legacy save version survive exact hazard retirement");
        g.Close();
        Console.WriteLine("PASS lethal guardian contact retracts only its owned delay circles while another Boss and world hazards remain");
    }

    private static void SpawnAndRecoveredAbyssEnemiesDoNotStackOnOneFoot(FrontierRuntime app)
    {
        Prepare(app,0);
        GameCore g=app.Game;
        g.save.frontierEnding=1;
        g.save.abyssFloor=1;
        g.save.abyssSnapshotFloor=-1;
        g.Enter(8);
        Check(g.save.room==8&&g.enemies.Count>0,
            "Actual procedurally generated Abyss contains living combat actors");
        Point at=new Point();bool found=false;
        foreach(Enemy original in g.enemies)
        {
            Point p=original.Position;
            if(!g.CanStand(p))continue;
            for(int dy=-1;dy<=1&&!found;dy++)
            for(int dx=-1;dx<=1&&!found;dx++)
            {
                if(dx==0&&dy==0)continue;
                Point candidate=new Point(
                    (float)Math.Floor(p.x)+dx+.5f,
                    (float)Math.Floor(p.y)+dy+.5f);
                if(g.CanStand(candidate)&&
                    (bool)CallCore(g,"ActorHasLocalRoute",p,candidate,4))
                {at=p;found=true;}
            }
            if(found)break;
        }
        Check(found,"A real generated Abyss foot has an accessible separate neighbor");
        g.enemies.Clear();
        Enemy first=(Enemy)CallCore(g,"CreatePlacedEnemy",8101,0,at,80f,false);
        g.enemies.Add(first);
        Enemy second=(Enemy)CallCore(g,"CreatePlacedEnemy",8102,1,at,85f,false);
        Check((first.Position-second.Position).Length>=.95f&&
            g.CanStand(second.Position)&&
            (bool)CallCore(g,"ActorHasLocalRoute",first.Position,second.Position,4),
            "Second production spawn avoids the first on the same reachable side of actual terrain");
        g.enemies.Add(second);
        Check(g.enemies.Count==2&&first.id==8101&&second.id==8102,
            "Both combatants keep their own living identities and attack bodies");
        g.enemies.Clear();
        g.save.abyssSnapshotFloor=1;
        g.save.abyssEnemies=new []{
            new EnemyRecord{id=9001,kind=0,x=at.x,y=at.y,hp=37,maxHp=50},
            new EnemyRecord{id=9002,kind=1,x=at.x,y=at.y,hp=44,maxHp=60}
        };
        int coins=g.save.coins,shards=g.save.shards;
        Check((bool)CallCore(g,"RestoreAbyssEnemies")&&
            g.enemies.Count==2,
            "Real Abyss snapshot recovery restores both independently identified fighters");
        Enemy a=g.enemies[0],b=g.enemies[1];
        Check(a.id==9001&&b.id==9002&&
            Math.Abs(a.hp-37f)<.001f&&Math.Abs(b.hp-44f)<.001f&&
            Math.Abs(a.maxHp-50f)<.001f&&Math.Abs(b.maxHp-60f)<.001f,
            "Recovery preserves the unique original enemy IDs, types and exact unspent health");
        Check((a.Position-b.Position).Length>=.95f&&
            (bool)CallCore(g,"ActorHasLocalRoute",a.Position,b.Position,4),
            "Interrupted legacy snapshot's overlapping enemies recover to two connected physical combat tiles");
        Check(g.save.version==2&&g.save.coins==coins&&g.save.shards==shards,
            "Enemy recovery never modifies SaveData version or awards loot");
        g.Close();
        Console.WriteLine("PASS Shipped enemy spawning and real Abyss recovery separate overlapping live actors without lost IDs, HP or rewards");
    }

    private static void AbyssSaveRecoveryAvoidsHeroAndCompanionMaterialization(FrontierRuntime app)
    {
        Prepare(app,0);
        GameCore g=app.Game;
        g.save.frontierEnding=1;g.save.abyssFloor=1;
        g.save.abyssSnapshotFloor=-1;
        g.Enter(8);
        Check(g.save.room==8&&g.enemies.Count>0,
            "An actual procedural Abyss combat map exists");
        Point p=new Point();bool found=false;
        for(int y=4;y<g.Map.rows.Length-4&&!found;y++)
        for(int x=4;x<g.Map.rows[y].Length-4&&!found;x++)
        {
            Point q=new Point(x+.5f,y+.5f);
            if(!g.CanStand(q))continue;
            int exits=0;
            for(int dy=-2;dy<=2;dy++)
            for(int dx=-2;dx<=2;dx++)
            {
                if(dx==0&&dy==0)continue;
                Point n=q+new Point(dx,dy);
                if(g.CanStand(n)&&
                    (bool)CallCore(g,"ActorHasLocalRoute",q,n,4))
                    exits++;
            }
            if(exits>=4){p=q;found=true;}
        }
        Check(found,"Published Abyss map has enough local dry room to separate physical hurtboxes");
        g.enemies.Clear();
        g.save.x=p.x;g.save.y=p.y;g.save.companion=-1;
        g.save.abyssSnapshotFloor=1;
        g.save.abyssEnemies=new[]{
            new EnemyRecord{id=9011,kind=2,x=p.x,y=p.y,hp=41,maxHp=60}
        };
        int beforeCoins=g.save.coins,beforeShards=g.save.shards;
        Check((bool)CallCore(g,"RestoreAbyssEnemies")&&g.enemies.Count==1,
            "Real saved Abyss enemy restores even when its saved feet coincided with hero");
        Enemy foe=g.enemies[0];
        Check(foe.id==9011&&Math.Abs(foe.hp-41f)<.001f&&
            (foe.Position-g.Position).Length>=1.05f&&
            (bool)CallCore(g,"ActorHasLocalRoute",p,foe.Position,4),
            "Recovery preserves HP and combat identity but never materializes an enemy inside the hero");
        g.enemies.Clear();
        g.save.flags.Add("companion_0");
        g.save.companion=0;
        g.companionPosition=p;
        g.save.x=p.x+4f;g.save.y=p.y;
        // Restore the same interrupted record beside the genuinely active
        // ally rather than the hero: both bodies need their own footing.
        Check((bool)CallCore(g,"RestoreAbyssEnemies")&&g.enemies.Count==1,
            "Real Abyss restore accepts the existing companion-party footprint");
        foe=g.enemies[0];
        Check((foe.Position-g.companionPosition).Length>=1.05f&&
            (bool)CallCore(g,"ActorHasLocalRoute",p,foe.Position,4)&&
            foe.id==9011&&Math.Abs(foe.hp-41f)<.001f,
            "Restored enemy avoids allied collision without an impossible cross-wall placement");
        Check(g.save.version==2&&g.save.coins==beforeCoins&&
            g.save.shards==beforeShards,
            "Physical spawn recovery grants no rewards and keeps existing save format");
        g.Close();
        Console.WriteLine("PASS Interrupted Abyss saves do not spawn enemies inside hero or companion hurtboxes");
    }

    private static void RisingMistportTideSeparatesLiveCombatantsOnDryBanks(FrontierRuntime app)
    {
        Prepare(app,0);
        GameCore g=app.Game;
        g.save.flags.Add("exp_intro");g.save.flags.Add("companion_0");
        g.save.stage=4;g.Enter(5);g.enemies.Clear();
        Check(g.save.room==5,"Actual authored Mistport supports tidal combat terrain");
        g.save.tideHeldUntil=0f;
        Point submerged=new Point(),bank=new Point();bool found=false;
        for(int y=3;y<g.Map.rows.Length-3&&!found;y++)
        for(int x=3;x<g.Map.rows[y].Length-3&&!found;x++)
        {
            Point tile=new Point(x+.5f,y+.5f);
            g.save.totalTime=10f;
            if(!g.CanStand(tile))continue;
            g.save.totalTime=25f;
            if(g.CanStand(tile))continue;
            Point safe=(Point)CallCore(g,"SafeActorFoot",tile);
            if(!g.CanStand(safe)||(safe-g.Position).Length<3f)continue;
            int candidates=0;
            for(int dy=-3;dy<=3;dy++)
            for(int dx=-3;dx<=3;dx++)
            {
                if(dx==0&&dy==0)continue;
                Point p=new Point((float)Math.Floor(safe.x)+dx+.5f,
                    (float)Math.Floor(safe.y)+dy+.5f);
                if(g.CanStand(p)&&
                    (bool)CallCore(g,"ActorHasLocalRoute",safe,p,4)&&
                    (p-g.Position).Length>=1.05f)candidates++;
            }
            if(candidates<3)continue;
            submerged=tile;bank=safe;found=true;
        }
        Check(found,"A genuine Mistport shoal floods at high tide beside connected safe-bank combat feet");
        g.save.totalTime=25f;
        Check(!g.LowTide&&!g.CanStand(submerged)&&g.CanStand(bank),
            "The chosen real shoal is submerged under the original high-tide collision rules");
        g.save.companion=0;g.companionPosition=submerged;
        Enemy first=new Enemy(99131,0,submerged.x,submerged.y,70);
        Enemy second=new Enemy(99132,1,submerged.x,submerged.y,80);
        first.state=second.state=1;
        g.enemies.Add(first);g.enemies.Add(second);
        // These were valid before the surge but must not survive a
        // forced relocation into different collision geometry.
        var staleAllyRoute=(Queue<Point>)CoreField(g,"companionFollowRoute");
        staleAllyRoute.Enqueue(new Point(submerged.x+3,submerged.y));
        typeof(GameCore).GetField("companionUnreachableGoal",Private)
            .SetValue(g,true);
        typeof(GameCore).GetField("companionBlockedRouteRetry",Private)
            .SetValue(g,2.5f);
        int coins=g.save.coins,shards=g.save.shards;
        CallCore(g,"RecoverTideActors");
        Check(staleAllyRoute.Count==0&&
            !(bool)CoreField(g,"companionUnreachableGoal")&&
            (float)CoreField(g,"companionBlockedRouteRetry")==0f,
            "Forced allied relocation discards obsolete footpath and stale failure cooldown");
        Check(g.CanStand(first.Position)&&g.CanStand(second.Position)&&
            g.CanStand(g.companionPosition),
            "Rising water evacuates both real enemies and the companion to playable dry ground");
        // Actor placement rejects separation BELOW .95 tile; distinct
        // neighboring tile centers are exactly 1.0 tile apart and valid.
        // Requiring 1.05 here would incorrectly report two neighboring
        // safe cells as overlapping even though the actual hurtboxes do not.
        Check((first.Position-second.Position).Length>=.95f&&
            (first.Position-g.companionPosition).Length>=.95f&&
            (second.Position-g.companionPosition).Length>=.95f&&
            (g.companionPosition-g.Position).Length>=.95f,
            "Dry-bank evacuation reserves physically separate live collision footprints (>=.95 tile)");
        Check((bool)CallCore(g,"ActorHasLocalRoute",first.Position,second.Position,4)&&
            (bool)CallCore(g,"ActorHasLocalRoute",first.Position,g.companionPosition,4),
            "Relocations remain on the connected bank rather than jumping to another terrain island");
        Check(first.id==99131&&second.id==99132&&
            Math.Abs(first.hp-70f)<.001f&&Math.Abs(second.hp-80f)<.001f&&
            first.state==0&&second.state==0&&
            g.save.coins==coins&&g.save.shards==shards&&g.save.version==2,
            "Real tide aborts unsafe attacks while preserving each foe, progress, loot and old save format");
        g.Close();
        Console.WriteLine("PASS Real Mistport flood evacuates multiple enemies and ally to separate reachable dry feet without phantom attacks or rewards");
    }

    private static void FloodRescueCivilianEvacuatesWithoutMaterializingInsideCombatants(FrontierRuntime app)
    {
        Prepare(app,0);
        GameCore g=app.Game;
        g.save.stage=4;g.save.flags.Add("exp_intro");
        g.save.flags.Add("companion_0");g.Enter(5);g.enemies.Clear();
        Check(g.save.room==5,"Live Mistport map supports tide-stranded rescue residents");
        g.save.tideHeldUntil=0f;
        Point shoal=new Point(),bank=new Point(),allyBank=new Point();
        bool found=false;
        for(int y=3;y<g.Map.rows.Length-3&&!found;y++)
        for(int x=3;x<g.Map.rows[y].Length-3&&!found;x++)
        {
            Point wet=new Point(x+.5f,y+.5f);
            g.save.totalTime=10f;
            if(!g.CanStand(wet))continue;
            g.save.totalTime=25f;
            if(g.CanStand(wet))continue;
            Point dry=(Point)CallCore(g,"SafeActorFoot",wet);
            if(!g.CanStand(dry)||(dry-g.Position).Length<4f)continue;
            int open=0;Point ally=new Point();
            for(int dy=-2;dy<=2;dy++)
            for(int dx=-2;dx<=2;dx++)
            {
                if(dx==0&&dy==0)continue;
                Point option=new Point((float)Math.Floor(dry.x)+dx+.5f,
                    (float)Math.Floor(dry.y)+dy+.5f);
                if(g.CanStand(option)&&
                    (bool)CallCore(g,"ActorHasLocalRoute",dry,option,4)&&
                    (option-g.Position).Length>=1.05f)
                {open++;if(open==1)ally=option;}
            }
            if(open<4)continue;
            shoal=wet;bank=dry;allyBank=ally;found=true;
        }
        Check(found,"An actual tidal shoal has a dry, sufficiently spacious connected bank");
        g.save.totalTime=25f;
        g.save.companion=0;g.companionPosition=allyBank;
        Enemy guard=new Enemy(99191,0,bank.x,bank.y,75);
        g.enemies.Add(guard);
        g.reliefMissionRoom=5;g.reliefMissionWave=3;
        g.reliefMissionTime=73f;g.reliefFollowerSafety=48f;
        g.reliefFollowerPosition=shoal;
        g.reliefFollowerWaiting=true;
        g.reliefFollowerMoving=true;g.reliefFollowerWalkDistance=2.3f;
        var staleCivilianRoute=(List<Point>)CoreField(g,"reliefFollowerRoute");
        staleCivilianRoute.Add(new Point(shoal.x+4,shoal.y));
        typeof(GameCore).GetField("reliefFollowerBlockedSeconds",Private)
            .SetValue(g,2.5f);
        int coins=g.save.coins,shards=g.save.shards;
        Check(g.ReliefEscortActive&&!g.CanStand(shoal)&&
            (bank-allyBank).Length>=.95f,
            "A real waiting evacuee is submerged beside separately positioned enemies and companion");
        CallCore(g,"RecoverTideActors");
        Point resident=g.reliefFollowerPosition;
        Check(staleCivilianRoute.Count==0&&
            (float)CoreField(g,"reliefFollowerBlockedSeconds")==0f&&
            g.reliefFollowerWaiting,
            "Rising-tide evacuee drops obsolete walking and stuck-route caches without losing WAIT order");
        Check(g.CanStand(resident)&&
            (resident-guard.Position).Length>=.95f&&
            (resident-g.companionPosition).Length>=.95f&&
            (resident-g.Position).Length>=.95f,
            "Civilian lands on dry playable ground without overlapping the enemy, hero or companion");
        Check((bool)CallCore(g,"ActorHasLocalRoute",bank,resident,4),
            "The resident cannot be evacuated through a sealed wall onto a distant island");
        Check(g.reliefMissionRoom==5&&g.reliefMissionWave==3&&
            Math.Abs(g.reliefMissionTime-73f)<.001f&&
            Math.Abs(g.reliefFollowerSafety-48f)<.001f&&
            g.reliefFollowerWaiting&&
            !g.reliefFollowerMoving&&
            Math.Abs(g.reliefFollowerWalkDistance)<.001f,
            "Evacuation retains timed rescue, resident safety, WAIT order and reset gait presentation");
        Check(g.save.coins==coins&&g.save.shards==shards&&
            g.save.version==2&&guard.id==99191,
            "A real high-tide rescue relocation never invents loot or changes save/enemy identities");
        g.Close();
        Console.WriteLine("PASS Real Mistport tide relocates waiting rescue resident to unoccupied reachable dry bank preserving escort state");
    }

    private static void LiveEscortResidentOwnsFootAgainstFreshFoesAndBossSupport(FrontierRuntime app)
    {
        Prepare(app,0);
        GameCore g=app.Game;
        g.save.stage=4;g.save.flags.Add("exp_intro");
        g.Enter(5);g.enemies.Clear();
        Check(g.save.room==5,"Original Mistport hosts real protected civilian rescue combat");
        g.save.totalTime=10f;g.save.tideHeldUntil=0f;
        Point patient=new Point(),bossFoot=new Point(),hero=new Point();
        bool found=false;
        for(int y=5;y<g.Map.rows.Length-6&&!found;y++)
        for(int x=5;x<g.Map.rows[y].Length-7&&!found;x++)
        {
            Point p=new Point(x+.5f,y+.5f);
            Point boss=p+new Point(2,-1),fighter=boss+new Point(0,4);
            Point alternate=boss+new Point(2,1);
            if(!g.CanStand(p)||!g.CanStand(boss)||
                !g.CanStand(fighter)||!g.CanStand(alternate)||
                (bool)CallCore(g,"BossSummonHasLocalRoute",boss,p)==false||
                (bool)CallCore(g,"BossSummonHasLocalRoute",boss,alternate)==false||
                g.NavigationPathFrom(boss,fighter,true).Count==0||
                !(bool)CallCore(g,"ClearAttackPath",boss,p)||
                !(bool)CallCore(g,"ClearAttackPath",boss,alternate))
                continue;
            patient=p;bossFoot=boss;hero=fighter;found=true;
        }
        Check(found,"Mistport has an original fully connected two-summon arena and valid civilian foot");
        g.save.x=hero.x;g.save.y=hero.y;
        g.reliefMissionRoom=5;g.reliefMissionWave=3;
        g.reliefMissionTime=64f;g.reliefFollowerSafety=73f;
        g.reliefFollowerPosition=patient;g.reliefFollowerWaiting=true;
        Check(g.ReliefEscortActive,"A real escort in WAIT mode owns its current physical position");
        Enemy recovered=(Enemy)CallCore(g,"CreatePlacedEnemy",
            99801,0,patient,75f,false);
        Check((recovered.Position-patient).Length>=1.05f&&
            g.CanStand(recovered.Position)&&
            (bool)CallCore(g,"ActorHasLocalRoute",patient,recovered.Position,4),
            "New field enemy avoids resident instead of sharing the exact rescue collision foot");
        g.enemies.Clear();
        Enemy bossEnemy=new Enemy(99802,3,bossFoot.x,bossFoot.y,999,true)
            {hp=900,cycle=2,stagger=99};
        g.enemies.Add(bossEnemy);
        int coins=g.save.coins,shards=g.save.shards;
        // Prove the first authored summoning offset is ordinarily legal.
        g.reliefMissionWave=0;
        Check((bool)CallCore(g,"TrySpawnBossReinforcement",bossEnemy)&&
            g.enemies.Count==2&&
            (g.enemies[1].Position-patient).Length<.001f,
            "Unprotected original Boss picks the first valid authored summon position");
        g.enemies.RemoveAt(1);
        g.reliefMissionWave=3;
        Check((bool)CallCore(g,"TrySpawnBossReinforcement",bossEnemy)&&
            g.enemies.Count==2&&
            (g.enemies[1].Position-patient).Length>=1.25f&&
            g.CanStand(g.enemies[1].Position),
            "During a living rescue Boss uses another genuine summon foot instead of spawning on evacuee");
        Check(g.reliefMissionTime==64f&&g.reliefFollowerSafety==73f&&
            g.reliefFollowerWaiting&&g.reliefFollowerPosition.x==patient.x&&
            g.save.coins==coins&&g.save.shards==shards&&g.save.version==2,
            "Protecting the original resident never mutates escort countdown, WAIT order, rewards or old saves");
        g.Close();
        Console.WriteLine("PASS Authentic escort civilian reserves collision foot against new enemy and Boss summoned reinforcements");
    }

    private static void CrowdedCombatCorridorFindsDistantConnectedFootWithoutWallTeleport(FrontierRuntime app)
    {
        Prepare(app,0);
        GameCore g=app.Game;
        g.Enter(9);g.enemies.Clear();
        MapData map=g.Map;
        string[] oldRows=map.rows;
        Decoration[] oldDecor=map.decor;
        int width=map.rows[0].Length,height=map.rows.Length;
        try
        {
            // Actual gameplay pathfinder and footprint rules operate on a
            // one-tile corridor in the original region's active map.
            // Seven living enemies fill every reachable cell within four
            // tiles of the original spawn. A legal connected empty foot is
            // still present five steps along that very same corridor.
            string[] rows=new string[height];
            for(int y=0;y<height;y++)
                rows[y]=new string('#',width);
            char[] lane=rows[13].ToCharArray();
            for(int x=8;x<=28;x++)lane[x]='.';
            rows[13]=new string(lane);
            map.rows=rows;map.decor=new Decoration[0];
            g.save.x=26.5f;g.save.y=13.5f;
            for(int x=8;x<=14;x++)
                g.enemies.Add(new Enemy(97000+x,0,x+.5f,13.5f,45f));
            Point original=new Point(10.5f,13.5f);
            Check(g.CanStand(original)&&g.CanStand(g.Position)&&
                g.enemies.Count==7&&
                (bool)CallCore(g,"ActorHasLocalRoute",original,
                    new Point(15.5f,13.5f),12),
                "The game collision graph has a real fully occupied four-ring corridor with an open fifth step");
            int coins=g.save.coins,shards=g.save.shards;
            Enemy extra=(Enemy)CallCore(g,"CreatePlacedEnemy",
                97999,1,original,80f,false);
            Check(Math.Abs(extra.Position.x-15.5f)<.001f&&
                Math.Abs(extra.Position.y-13.5f)<.001f&&
                g.CanStand(extra.Position)&&
                (bool)CallCore(g,"ActorHasLocalRoute",
                    original,extra.Position,12),
                "An additional real combatant finds the fifth reachable tile instead of stacking on a live enemy");
            foreach(Enemy occupied in g.enemies)
                Check((occupied.Position-extra.Position).Length>=.95f,
                    "Extended path search cannot overlap any of the seven already living enemies");
            g.enemies.Add(extra);
            // A full-width wall splits the farther open corridor: never
            // return the tempting empty tile beyond the new solid gate.
            rows=(string[])map.rows.Clone();
            lane=rows[15].ToCharArray(); // row 15 is already fully sealed
            rows[13]=rows[13].Substring(0,16)+"#"+
                rows[13].Substring(17);
            map.rows=rows;
            Check(!g.CanStand(new Point(16.5f,13.5f))&&
                (bool)CallCore(g,"ActorHasLocalRoute",original,
                    new Point(18.5f,13.5f),12)==false,
                "Actual wall isolates the remaining free corridor from the original combat pocket");
            Enemy blocked=(Enemy)CallCore(g,"CreatePlacedEnemy",
                97998,2,original,65f,false);
            Check((blocked.Position-new Point(18.5f,13.5f)).Length>1f&&
                (blocked.Position-original).Length<12f,
                "No enemy is relocated through a sealed wall to fabricate available space");
            Check(g.save.version==2&&g.save.coins==coins&&
                g.save.shards==shards&&extra.id==97999,
                "Crowded combat recovery preserves actor identity, rewards and save schema");
        }
        finally
        {
            map.rows=oldRows;map.decor=oldDecor;
        }
        g.Close();
        Console.WriteLine("PASS Crowded live actor corridor finds connected fifth-step foot without crossing a newly sealed wall");
    }

    private static void CivicReserveCombatHudTracksGenuineReceiverObjective(FrontierRuntime app)
    {
        Prepare(app,0);
        var g=app.Game;
        g.save.stage=3;g.save.ending=3;g.save.english=false;
        foreach (string flag in new[]{
            "afterlight_started","afterlight_complete","afterlight_done_1",
            "afterlight_steward_1","afterlight_reserve_1",
            "afterlight_plan_1_watch"})
            if(!g.Has(flag))g.save.flags.Add(flag);
        g.Enter(1);g.enemies.Clear();
        WorldObject receiver=null;
        foreach(WorldObject o in g.Map.objects)
            if(o.kind=="afterlightoutlet1"){receiver=o;break;}
        Check(receiver!=null,"Published Thornwood public receiver actually exists");
        g.save.x=receiver.x;g.save.y=receiver.y;
        g.Talk(receiver);
        Check(g.conversation!=null,"The rebuilt public receiver opens real dialogue");
        int start=-1;
        for(int i=0;i<g.conversation.choices.Length;i++)
            if(g.conversation.choices[i].action=="afterlight_watch_start_1"){start=i;break;}
        Check(start>=0,"Public stock provides an authentic defense start choice");
        g.Choose(start);
        Check(g.AfterlightWatchActive&&g.AfterlightWatchWave==1&&
            g.AfterlightWatchStock==100f&&g.AfterlightWatchTime==100f,
            "Real siege starts with complete timed receiver objective");

        // The hero and camera may move away from the receiver. A genuinely
        // live siege must keep its status in the combat layout itself, even
        // with the ordinary combat HUD grace timer at zero.
        Set(app,"combatHudTime",0f);
        Set(app,"camera",new Point(13,14));
        GUI.commands.Clear();GUI.textureCalls.Clear();GUI.buttonCalls.Clear();
        Call(app,"OnGUI");
        Check(GUI.commands.Exists(c=>c.Contains("公共储备防卫 · 第1/2")),
            "Real combat HUD exposes the active wave instead of hiding it at the offscreen station");
        Check(GUI.commands.Exists(c=>c.Contains(GUI.S("设施耐久 100%"))),
            "Real HUD shows the actual live objective percentage");
        Check(GUI.buttonCalls.Exists(c=>Math.Abs(c.rect.x-430)<.5f&&
            Math.Abs(c.rect.y-732)<.5f),
            "Civic-defense HUD retains the actual compact combat skill input instead of blocking Q");

        var integrity=typeof(GameCore).GetField("afterlightWatchStock",
            Private);
        var patched=typeof(GameCore).GetField("afterlightWatchPatched",
            Private);
        Check(integrity!=null&&patched!=null,
            "Real civilian stock and one-use repair availability are produced by GameCore");
        integrity.SetValue(g,22.5f);
        g.save.inventory[0]=2;g.save.inventory[2]=2;
        GUI.commands.Clear();Call(app,"OnGUI");
        Check(GUI.commands.Exists(c=>c.Contains(GUI.S("设施耐久 23% · 抢修 E"))),
            "Critical objective turns into a readable 23%-integrity prompt with a truthful E repair hint");

        g.save.english=true;
        GUI.commands.Clear();Call(app,"OnGUI");
        Check(GUI.commands.Exists(c=>c.Contains(GUI.S("STOCK INTEGRITY 23% · PATCH E"))),
            "Active English combat HUD reports the identical real stock condition");
        patched.SetValue(g,true);
        GUI.commands.Clear();Call(app,"OnGUI");
        Check(GUI.commands.Exists(c=>c.Contains(GUI.S("STOCK INTEGRITY 23%")))&&
            !GUI.commands.Exists(c=>c.Contains(GUI.S("STOCK INTEGRITY 23% · PATCH E"))),
            "Consumed repair is never falsely advertised as available");
        g.save.english=false;
        g.save.x=receiver.x;g.save.y=receiver.y;
        g.Talk(receiver);
        int abort=-1;
        for(int i=0;i<g.conversation.choices.Length;i++)
            if(g.conversation.choices[i].action=="afterlight_watch_abort_1"){abort=i;break;}
        Check(abort>=0,"Public receiver supports actual on-site siege withdrawal");
        g.Choose(abort);
        g.enemies.Clear();
        GUI.commands.Clear();Call(app,"OnGUI");
        Check(!GUI.commands.Exists(c=>c.Contains("公共储备防卫 · 第")),
            "No stale combat HUD after a real withdrawal");
        Console.WriteLine("PASS actual civic stock siege draws bilingual offscreen HUD, honest patch hints, input-safe compact controls and cleanup");
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
            // West has no independent atlas: the actual GUI cell must be
            // mirrored, not simply selected from the same East resource.
            // North/South must NOT inherit the horizontal weapon flip.
            bool expectedMirror=facing==0&&direction.x<-.1f;
            Check((rendered.uv.width<0f)==expectedMirror,
                "HERO_FACING_FLIP_REGRESSION: wrong source UV for "+
                klass+"/"+animation+"/"+direction.x+","+direction.y);
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
            Check(restored != null && restored.coins == 30 && warning == "Recovered interrupted save.",
                "A newer checksum-valid interrupted write must recover even while the old primary remains valid.");
            put(main, 35, "2026-01-04T00:00:00.0000000Z");
            put(main + ".tmp", 30, "2026-01-03T00:00:00.0000000Z");
            restored = SaveStore.Read(0, out warning);
            Check(restored != null && restored.coins == 35 && warning == null,
                "A newer valid primary must beat a stale temporary file.");
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

            // Simulate process death after a unique candidate was flushed but
            // before it was promoted. Recovery must recognize it without
            // overwriting the older primary or the legacy sidecar paths.
            string staged = main + ".stage-recovery-fixture";
            DateTime stagedAt = DateTime.UtcNow.AddMinutes(5);
            put(staged, 89, stagedAt.ToString("o", CultureInfo.InvariantCulture));
            restored = SaveStore.Read(0, out warning);
            Check(restored != null && restored.coins == 89 &&
                warning == "Recovered interrupted save.",
                "A complete unpromoted unique staging file is a recoverable interrupted save.");
            string stageOnlyMain = SaveStore.PathFor(2);
            string stageOnly = stageOnlyMain + ".stage-only-fixture";
            put(stageOnly, 123, DateTime.UtcNow.AddMinutes(7).ToString("o", CultureInfo.InvariantCulture));
            Check(SaveStore.Exists(2),
                "The title/save existence query recognizes a valid stage-only recovery candidate.");
            restored = SaveStore.Read(2, out warning);
            Check(restored != null && restored.coins == 123 &&
                warning == "Recovered interrupted save.",
                "A flushed staging candidate remains loadable when no primary, backup or legacy temp exists.");
            System.IO.File.Delete(stageOnly);
            System.IO.File.WriteAllText(stageOnly, "{partial");
            Check(!SaveStore.Exists(2),
                "A partial stage-only file cannot falsely enable Continue when no valid save exists.");
            restored = SaveStore.Read(2, out warning);
            Check(restored == null && warning != null,
                "A partial first-write stage cannot be loaded as a default character.");
            System.IO.File.Delete(stageOnly);

            var invalidCandidate = new SaveData { version = 2, coins = 90,
                inventory = new int[7] };
            Check(!SaveStore.Write(invalidCandidate, 0) &&
                System.IO.File.Exists(staged),
                "A rejected write cannot truncate an older recoverable staging candidate.");
            restored = SaveStore.Read(0, out warning);
            Check(restored != null && restored.coins == 89 &&
                warning == "Recovered interrupted save.",
                "The only complete staging candidate survives a failed new write.");
            System.IO.File.Delete(staged);

            // A damaged clock or hand-edited metadata must not make a backup
            // outrank an otherwise newer sane copy forever.
            put(main, 100, "9999-12-31T23:59:59.9999999Z");
            System.IO.File.SetLastWriteTimeUtc(main, DateTime.UtcNow.AddMinutes(-10));
            DateTime saneBackupTime = DateTime.UtcNow.AddMinutes(-5);
            put(main + ".bak", 99, saneBackupTime.ToString("o", CultureInfo.InvariantCulture));
            System.IO.File.SetLastWriteTimeUtc(main + ".bak", saneBackupTime);
            restored = SaveStore.Read(0, out warning);
            Check(restored != null && restored.coins == 99 && warning == "Recovered backup.",
                "An implausible year-9999 save timestamp cannot permanently outrank a sane backup.");

            // Simulate a machine clock being corrected backwards after an
            // earlier save was written with a future-but-representable clock.
            put(main, 90, "2099-01-01T00:00:00.0000000Z");
            put(main + ".bak", 89, "2098-01-01T00:00:00.0000000Z");
            Check(SaveStore.Write(new SaveData { version = 2, coins = 91 }, 0),
                "A new save succeeds after the wall clock moves backwards.");
            restored = SaveStore.Read(0, out warning);
            Check(restored != null && restored.coins == 91 && warning == null,
                "A new write advances beyond the newest persisted timestamp and cannot be rolled back to the old future-dated backup.");
            Check(!System.IO.File.Exists(main + ".tmp") &&
                System.IO.Directory.GetFiles(directory,
                    System.IO.Path.GetFileName(main) + ".stage-*").Length == 0,
                "Successful commit cleans stale temporary/staging candidates only after the primary is verified.");

            var cleanup = typeof(SaveStore).GetMethod("CleanupCommittedRecoveryPaths",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);

            // Concurrent writers can observe the same wall-clock tick.
            // A later verified stage must win even when its payload timestamp
            // ties the primary, and post-commit cleanup must not delete it.
            string tieStage = main + ".stage-equal-timestamp";
            string tieTimestamp = restored.savedAtUtc;
            put(tieStage, 93, tieTimestamp);
            DateTime tiedClock;
            Check(DateTime.TryParse(tieTimestamp, CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind, out tiedClock),
                "The committed timestamp is parseable for concurrency regression.");
            System.IO.File.SetLastWriteTimeUtc(main, tiedClock.AddMinutes(-2));
            System.IO.File.SetLastWriteTimeUtc(tieStage, tiedClock.AddMinutes(-1));
            restored = SaveStore.Read(0, out warning);
            Check(restored != null && restored.coins == 93 &&
                warning == "Recovered interrupted save.",
                "A later complete stage wins a same-savedAtUtc race by verified file-write order.");
            cleanup.Invoke(null, new object[] { main });
            Check(System.IO.File.Exists(tieStage),
                "Commit cleanup preserves a same-timestamp stage written after the primary.");
            System.IO.File.Delete(tieStage);
            System.IO.File.SetLastWriteTimeUtc(main, DateTime.UtcNow);

            string newerStage = main + ".stage-concurrent-writer";
            put(newerStage, 92, "2100-01-01T00:00:00.0000000Z");
            cleanup.Invoke(null, new object[] { main });
            Check(System.IO.File.Exists(newerStage),
                "Commit cleanup preserves a valid newer stage owned by a concurrent writer.");
            System.IO.File.Delete(newerStage);
            string partialStage = main + ".stage-inflight";
            System.IO.File.WriteAllText(partialStage, "{partial");
            cleanup.Invoke(null, new object[] { main });
            Check(System.IO.File.Exists(partialStage),
                "Commit cleanup never deletes a fresh unreadable candidate that may still be in flight.");
            System.IO.File.SetLastWriteTimeUtc(partialStage,
                DateTime.UtcNow.AddHours(-1));
            cleanup.Invoke(null, new object[] { main });
            Check(!System.IO.File.Exists(partialStage),
                "A stale invalid unique stage is pruned after the grace period and exclusive-read check.");

            // Race independent writers against the same previously empty slot.
            // Every valid save must finish, and the final primary must decode as one
            // of those writes rather than losing the slot to a staging/replace race.
            var concurrentStart = new System.Threading.ManualResetEvent(false);
            var concurrentThreads = new System.Threading.Thread[12];
            var concurrentResults = new bool[concurrentThreads.Length];
            for (int i = 0; i < concurrentThreads.Length; i++)
            {
                int writerIndex = i;
                concurrentThreads[i] = new System.Threading.Thread(() =>
                {
                    concurrentStart.WaitOne();
                    concurrentResults[writerIndex] = SaveStore.Write(
                        new SaveData { version = 2, coins = 200 + writerIndex }, 1);
                });
                concurrentThreads[i].IsBackground = true;
                concurrentThreads[i].Start();
            }
            concurrentStart.Set();
            bool allJoined = true;
            for (int i = 0; i < concurrentThreads.Length; i++)
                if (!concurrentThreads[i].Join(5000)) allJoined = false;
            concurrentStart.Dispose();
            Check(allJoined,
                "Concurrent save writers finish within the bounded lock wait.");
            bool allWritesSucceeded = true;
            for (int i = 0; i < concurrentResults.Length; i++)
                if (!concurrentResults[i]) allWritesSucceeded = false;
            Check(allWritesSucceeded,
                "Per-slot writer locking serializes concurrent transactions without dropping a valid write.");
            restored = SaveStore.Read(1, out warning);
            Check(restored != null && restored.coins >= 200 && restored.coins < 212 &&
                warning == null,
                "The final primary remains one of the fully verified concurrent writes.");
            Console.WriteLine("PASS Real SaveStore I/O: unique staging recovery, failed-write preservation, monotonic timestamps, clock rollback, corrupt future metadata, schema rejection and v1/v2 compatibility");
        }
        finally
        {
            Application.persistentDataPath = previousPath;
            if (System.IO.Directory.Exists(directory)) System.IO.Directory.Delete(directory, true);
        }
    }


    private static void FrostFieldsAreClearedWhenLoadingAnotherSlot(FrontierRuntime app)
    {
        Prepare(app, 2);
        GameCore game = app.Game;
        game.mode = "world";
        Point center = game.Position;
        var impact = new ImpactProfile(2, 0, 2, 0, .06f, true, 2);
        game.enemies.Clear();
        game.enemies.Add(new Enemy(992300, 0, center.x + 1f, center.y, 100f));
        CallCore(game, "BeginFrost", center, 20f, impact);
        CallCore(game, "BeginCompanionFrost", center, 20f, impact);
        CallCore(game, "TickFrost", .01f);
        Check(CoreField(game, "frost") != null &&
            CoreField(game, "companionFrost") != null,
            "Player and companion own two live FrostFields before slot replacement");
        Check(game.effects.Exists(effect => effect.kind == "frost"),
            "Active FrostFields own their real transient frost visuals");

        // Reuse the same room and world coordinates: without an explicit reset,
        // the stale pulse clocks would still be physically valid after Load().
        game.Load(new SaveData {
            version = 2, room = 1, x = center.x, y = center.y, profession = 2
        });
        Check(CoreField(game, "frost") == null &&
            CoreField(game, "companionFrost") == null,
            "Loading a replacement save clears both player and companion frost ownership");
        Check(!game.effects.Exists(effect => effect.kind == "frost"),
            "No old frost visual survives the new save's transient effect reset");

        var freshTarget = new Enemy(992301, 0, center.x + 1f, center.y, 100f);
        game.enemies.Clear();
        game.enemies.Add(freshTarget);
        CallCore(game, "TickFrost", 1f);
        Check(Math.Abs(freshTarget.hp - 100f) < .001f,
            "A previous slot's player or companion pulse cannot damage a new session");
        Console.WriteLine("PASS player/companion FrostField ownership is retired on save-slot replacement");
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





    private static void RemoteSoupRouteDrawsLiveShelterDeliveryInRealWorld(FrontierRuntime app)
    {
        Prepare(app);
        var g=app.Game;
        g.save.flags.Add("fish_kitchen_supplied");
        g.save.flags.Add("far_dial_1");
        g.save.flags.Add("far_shelter_1");
        g.save.stage=3;
        g.Enter(1);g.enemies.Clear();g.projectiles.Clear();
        Check(g.save.room==1,"Actual production scene permits entering the first real regional refuge");
        WorldObject shelter=Array.Find(g.Map.objects,o=>o.kind=="farshelter");
        Check(shelter!=null,"Real far-ring refuge has its authored world object");
        g.save.x=shelter.x;g.save.y=shelter.y;
        Set(app,"camera",new Point(shelter.x,shelter.y));
        g.save.inventory[6]=1;g.save.english=true;
        GUI.commands.Clear();Call(app,"OnGUI");
        Check(!GUI.commands.Exists(c=>c.Contains("HOT STEW DELIVERED")),
            "Unstocked original shelter never displays the earned hot-meal marker");
        g.Talk(shelter);
        int index=Array.FindIndex(g.conversation.choices,
            c=>c.action=="soup_route_deliver");
        Check(index>=0&&
            Array.FindIndex(g.conversation.choices,c=>c.action=="far_rest")>=0,
            "Production shelter retains timber rest and adds the actual relief choice");
        g.Choose(index);
        Check(g.SoupRouteDelivered(1)&&g.save.inventory[6]==0,
            "Player's actual stocked meal is exchanged for a persistent physical refuge service");
        GUI.commands.Clear();Call(app,"OnGUI");
        Check(GUI.commands.Exists(c=>c.Contains("HOT STEW DELIVERED")),
            "Real world view now marks only genuinely supplied refuge with a distinct warm-meal overlay");
        g.save.english=false;
        GUI.commands.Clear();Call(app,"OnGUI");
        Check(GUI.commands.Exists(c=>c.Contains("热汤已送到")),
            "The actual world marker has a corresponding Chinese version");
        g.mode="world";
        Console.WriteLine("PASS Real distant refuge soup handoff and bilingual earned world marker");
    }



    private static void ManualCompanionFocusDisplaysAndRetiresRealWorldMarker(FrontierRuntime app)
    {
        Prepare(app,2);
        var g=app.Game;g.Enter(0);g.enemies.Clear();
        g.save.flags.Add("companion_1");
        g.SelectCompanion(1);
        g.save.x=15.5f;g.save.y=10.88f;
        g.companionPosition=new Point(10.5f,10.88f);
        var foe=new Enemy(99601,0,14.4f,10.88f,10000f)
            {stun=100f,timer=1000f};
        Check(g.CanStand(g.companionPosition)&&g.CanStand(foe.Position),
            "Actual Emberfall scene has physical line-of-sight feet for a manual ally focus");
        g.enemies.Add(foe);
        g.aim=new Point(1,0);g.aimTarget=foe.Position;
        Set(app,"camera",g.Position);
        g.save.english=true;
        GUI.commands.Clear();Call(app,"OnGUI");
        Check(!GUI.commands.Exists(c=>c.Contains("FOCUS FIRE")),
            "Unmarked enemy never displays an earned tactical focus indicator");
        g.CommandCompanion();
        for(int tick=0;tick<5;tick++)g.Tick(.05f,new Point());
        Check(g.CompanionFocusActive&&g.IsCompanionFocusTarget(foe),
            "Actual Z ally order with three emitted arrows marks its real enemy object");
        GUI.commands.Clear();Call(app,"OnGUI");
        Check(GUI.commands.Exists(c=>c.Contains("FOCUS FIRE"))&&
            GUI.commands.Exists(c=>c.Contains("HERO HIT TO EXPOSE")),
            "Production English world draw shows real focus and its physical first-hit requirement");
        // A genuine Arcanist Q area contact, not a scripted change of
        // mark state, must flip the rendered instruction.
        g.projectiles.Clear();
        g.save.energy=100;g.Skill(new Point(-1,0));
        for(int tick=0;tick<9;tick++)g.Tick(.05f,new Point());
        Check(g.CompanionFocusExposed&&!g.CompanionFocusComboSpent,
            "Real player Q contact primes the actual enemy's coordinated opening");
        GUI.commands.Clear();Call(app,"OnGUI");
        Check(GUI.commands.Exists(c=>c.Contains("OPENING"))&&
            GUI.commands.Exists(c=>c.Contains("ALLY HIT NEEDED")),
            "Production GameView advances from focus advice to authentic exposed target state");
        g.save.english=false;
        GUI.commands.Clear();Call(app,"OnGUI");
        Check(GUI.commands.Exists(c=>c.Contains("同伴集火"))&&
            GUI.commands.Exists(c=>c.Contains("破绽已制造")),
            "Chinese world draw shows the actual earned first stage of a coordinated contact");
        g.enemies.Remove(foe);
        GUI.commands.Clear();Call(app,"OnGUI");
        Check(!GUI.commands.Exists(c=>c.Contains("同伴集火")),
            "An enemy's real death immediately retires the old marker before any new world frame");
        Console.WriteLine("PASS original-world companion focus marker, bilingual feedback and identity-safe retirement");
    }

    private static void LiveSpecialistClassTrialUsesActualTargetAndGameView(FrontierRuntime app)
    {
        foreach(int profession in new[]{1,2})
        {
            Prepare(app,profession);
            var g=app.Game;g.Enter(0);g.enemies.Clear();
            WorldObject mentor=Array.Find(g.Map.objects,o=>o.kind=="mentor");
            Check(mentor!=null,"Actual Emberfall retains its original training instructor");
            g.save.x=mentor.x;g.save.y=mentor.y;
            g.Talk(mentor);
            int choice=Array.FindIndex(g.conversation.choices,
                c=>c.action=="specialist_drill_start");
            Check(choice>=0,"Both actual nonwarrior players have access to the advanced class exam");
            g.Choose(choice);
            Check(g.SpecialistDrillActive&&g.enemies.Count==1,
                "Real mentor selection instantiates a physically damageable actor in the live world");
            Enemy dummy=g.enemies[0];
            Check(g.IsSpecialistDrillTarget(dummy),"Only the genuine spawned dummy owns the live marker");
            Set(app,"camera",g.Position);
            g.save.english=true;
            GUI.commands.Clear();Call(app,"OnGUI");
            Check(GUI.commands.Exists(x=>x.Contains("LIVE Q TARGET 0/3"))&&
                GUI.commands.Exists(x=>x.Contains("s left")),
                "Production English GameView paints challenge progress over its actual living dummy");
            g.save.english=false;
            GUI.commands.Clear();Call(app,"OnGUI");
            Check(GUI.commands.Exists(x=>x.Contains("Q实战靶 0/3")),
                "Production Chinese GameView labels the same target without replacing original actor art");
            g.save.english=true;
            g.Skill(new Point(1,0));
            for(int frame=0;frame<83;frame++)g.Tick(.05f,new Point());
            Check(g.SpecialistDrillHits==1&&g.IsSpecialistDrillTarget(dummy),
                "Actual class Q release and physical hit advances exactly one visible practice contact");
            GUI.commands.Clear();Call(app,"OnGUI");
            Check(GUI.commands.Exists(x=>x.Contains("LIVE Q TARGET 1/3")),
                "The GameView follows the genuine damaged actor's authoritative contact count");
            g.save.x=mentor.x;g.save.y=mentor.y;g.Talk(mentor);
            int cancel=Array.FindIndex(g.conversation.choices,
                c=>c.action=="specialist_drill_abort");
            Check(cancel>=0,"Instructor can recall the actual unfinished training actor");
            g.Choose(cancel);
            Check(!g.SpecialistDrillActive&&!g.enemies.Contains(dummy),
                "Exit retires the same actor instead of leaving a ghost target in the town");
            GUI.commands.Clear();Call(app,"OnGUI");
            Check(!GUI.commands.Exists(x=>x.Contains("LIVE Q TARGET")),
                "Live practice marker disappears when its actual owning combat actor retires");
        }
        Console.WriteLine("PASS live two-class advanced Q training actor and bilingual contact HUD");
    }

    private static void GardenMedicConversationFollowsClinicAndCropState(FrontierRuntime app)
    {
        Prepare(app,0);
        GameCore g=app.Game;g.Enter(0);
        WorldObject medic=Array.Find(g.Map.objects,o=>o.kind=="garden");
        Check(medic!=null,"Published home-town Moonherb medic exists");
        g.save.x=medic.x;g.save.y=medic.y;
        g.save.totalTime=100f;g.save.plantedAt=-1f;
        g.save.inventory[2]=0;
        g.Talk(medic);
        Check(g.conversation!=null&&
            g.conversation.text.Contains("六株月光草")&&
            Array.Exists(g.conversation.choices,c=>c.action=="ally1"),
            "Unfinished clinic request and six-herb handoff remain available");
        g.Close();
        g.save.inventory[2]=6;
        g.Talk(medic);
        Check(g.conversation.text.Contains("六株凑齐")&&
            g.conversation.textEn.Contains("all six"),
            "Medic notices genuine carried quest materials without generic exposition");
        g.Close();
        g.save.flags.Add("ally_1");
        g.save.inventory[2]=0;
        g.Talk(medic);
        Check(g.conversation.text.Contains("已经熬成药")&&
            !g.conversation.text.Contains("我需要六株")&&
            !Array.Exists(g.conversation.choices,c=>c.action=="ally1")&&
            Array.Exists(g.conversation.choices,c=>c.action=="plant"),
            "Fulfilled clinic never repeats a dead commission; productive garden survives");
        g.Close();
        g.save.plantedAt=90f;
        g.Talk(medic);
        Check(g.conversation.text.Contains("还差50秒")&&
            g.conversation.textEn.Contains("50 seconds")&&
            Array.Exists(g.conversation.choices,c=>c.action=="harvest"),
            "Living garden explains the remaining growth period in both languages");
        g.Close();
        g.save.totalTime=160f;
        g.Talk(medic);
        Check(g.conversation.text.Contains("长成了")&&
            g.conversation.textEn.Contains("Ready to pick")&&
            Array.Exists(g.conversation.choices,c=>c.action=="harvest")&&
            g.save.version==2,
            "Ready crop has a distinct harvest line with unchanged actions and save schema");
        g.Close();
        Console.WriteLine("PASS actual medic clinic fulfillment and crop-growth dialogue match persistent world state in both languages");
    }

    private static void ForgeDialogueAcknowledgesPumpRepairAndVeteranTraining(FrontierRuntime app)
    {
        Prepare(app,0);
        GameCore g=app.Game;g.Enter(0);
        WorldObject smith=Array.Find(g.Map.objects,o=>o.kind=="forge");
        Check(smith!=null,"Published town smith and public water-pump commission exist");
        g.save.x=smith.x;g.save.y=smith.y;
        g.save.inventory[4]=0;
        g.Talk(smith);
        Check(g.conversation!=null&&
            g.conversation.text.Contains("五个零件")&&
            Array.Exists(g.conversation.choices,c=>c.action=="ally3")&&
            Array.Exists(g.conversation.choices,c=>c.action=="upgrade"),
            "Unrepaired water pump still needs five actual parts; smith upgrades remain interactive");
        g.Close();
        g.save.inventory[4]=5;
        g.Talk(smith);
        Check(g.conversation.text.Contains("都找齐了")&&
            g.conversation.textEn.Contains("All five parts"),
            "Smith recognizes materials ready for the repair rather than repeating a collection request");
        g.Close();
        g.save.flags.Add("ally_3");
        g.save.inventory[4]=0;
        g.Talk(smith);
        Check(g.conversation.text.Contains("今天没再漏")&&
            !g.conversation.text.Contains("还差五个")&&
            !Array.Exists(g.conversation.choices,c=>c.action=="ally3")&&
            Array.Exists(g.conversation.choices,c=>c.action=="bow")&&
            Array.Exists(g.conversation.choices,c=>c.action=="staff"),
            "Repaired municipal pump changes in-world story, preserving forge equipment actions");
        g.Close();
        g.save.flags.Add("mineconvoy_complete");
        g.save.profession=g.save.weapon=0;
        g.Talk(smith);
        Check(g.conversation.text.Contains("最后一班矿工安全归来")&&
            g.conversation.textEn.Contains("One less plank")&&
            g.conversation.text.Contains(g.SwordGuardArrowEnergy+"点耐力")&&
            g.conversation.textEn.Contains(g.SwordGuardMeleeEnergy+""),
            "Rescued miners change wood-discount dialogue and live sword defense costs remain accurate");
        g.Close();
        Check(g.save.version==2,
            "Forge scene progression is driven by existing quest flags without save migration");
        Console.WriteLine("PASS authored smith dialogue follows fixed pump, returned miners and live sword-guard costs");
    }

    private static void ActualTownCommissionHandoffStaysWithOriginalSpeaker(FrontierRuntime app)
    {
        Prepare(app,0);
        GameCore g=app.Game;g.Enter(0);
        var reach=typeof(GameCore).GetMethod("HasPhysicalInteractionReach",
            Private,null,new[]{typeof(WorldObject)},null);
        Check(reach!=null,"Physical conversation source check exists in real game");
        string[] kinds={"garden","fish","forge"};
        string[] tokens={"药锅","银鳍鱼","水沿着管壁"};
        string[] actions={"ally1","ally2","ally3"};
        int[] materials={2,3,4};
        int[] amounts={6,3,5};
        for(int index=0;index<kinds.Length;index++)
        {
            WorldObject source=Array.Find(g.Map.objects,o=>o.kind==kinds[index]);
            Check(source!=null,"Published town commission speaker exists");
            bool reachable=false;
            for(int dy=-6;dy<=6&&!reachable;dy++)
            for(int dx=-6;dx<=6&&!reachable;dx++)
            {
                Point foot=new Point(source.x+dx*.25f,source.y+dy*.25f);
                if(!g.CanStand(foot))continue;
                g.save.x=foot.x;g.save.y=foot.y;
                if((bool)reach.Invoke(g,new object[]{source}))reachable=true;
            }
            Check(reachable,"Actual town commission source has a collision-legal interactive foot");
            g.save.inventory[materials[index]]=amounts[index];
            int beforeCoins=g.save.coins;
            g.Talk(source);
            Check(g.mode=="dialog"&&g.conversation!=null&&
                g.conversation.speaker==source.name,
                "Original real NPC owns the initial commission dialogue");
            int option=Array.FindIndex(g.conversation.choices,
                c=>c.action==actions[index]);
            Check(option>=0,"Published town commission remains an actionable choice");
            g.Choose(option);
            Check(g.Has("ally_"+(index+1))&&
                g.save.inventory[materials[index]]==0&&
                g.save.coins==beforeCoins+40,
                "Genuine choice consumes exact materials and grants unchanged currency reward");
            Check(g.mode=="dialog"&&g.conversation!=null&&
                g.conversation.speaker==source.name&&
                g.conversation.en==source.en&&
                g.conversation.text.Contains(tokens[index])&&
                System.Object.ReferenceEquals(g.conversationSource,source),
                "Success handoff keeps the real accepted speaker and source instead of jumping to another NPC");
            g.Close();
            g.Talk(source);
            Check(!Array.Exists(g.conversation.choices,
                c=>c.action==actions[index]),
                "Handed-in quest disappears from the same NPC while continuing services");
            g.Close();
        }
        Check(g.save.version==2,
            "NPC handoff is transient, preserving all old save data fields");
        Console.WriteLine("PASS three real town commissions spend materials and hand off localized reward conversations to their correct NPCs");
    }

    private static void PublicKitchenRealNpcAndBilingualFoodInventory(FrontierRuntime app)
    {
        Prepare(app);
        var g=app.Game;g.Enter(0);g.enemies.Clear();
        WorldObject pond=Array.Find(g.Map.objects,o=>o.kind=="fish");
        Check(pond!=null,"Original playable lakeside supply station must exist");
        g.save.x=pond.x;g.save.y=pond.y;
        g.save.english=true;
        g.save.flags.Add("ally_2");
        g.save.inventory[3]=2;g.save.inventory[6]=0;
        g.Talk(pond);
        int supply=Array.FindIndex(g.conversation.choices,
            c=>c.action=="fish_kitchen");
        Check(supply>=0,"Actual in-world fisherman opens the new repeatable kitchen trade");
        // The fisher now has enough story text to require multiple reader
        // pages. The production UI intentionally hides every action until
        // its last story page; don't mistake page one for a missing choice.
        GUI.commands.Clear();Call(app,"OnGUI");
        int pages=DialogueLayout.Pages(g.conversation.textEn,
            g.save.largeText).Count;
        for(int page=1;page<pages;page++)Call(app,"AdvanceDialogue");
        GUI.commands.Clear();Call(app,"OnGUI");
        Check(GUI.commands.Exists(x=>x.Contains("Public kitchen"))&&
            GUI.commands.Exists(x=>x.Contains("2 fish")),
            "After reading the real NPC story, the shipped English dialogue exposes the two-fish supply choice");
        g.Choose(supply);
        Check(g.PublicFishKitchenStocked&&g.save.inventory[3]==0&&
            g.save.inventory[6]==1&&g.StewHealing==65,
            "Clicking the authentic NPC trade changes persistent food strength and inventory");
        g.mode="inventory";
        Set(app,"selectedItem",6);
        GUI.commands.Clear();Call(app,"OnGUI");
        Check(GUI.commands.Exists(x=>x.Contains("Restores 65 health"))&&
            GUI.commands.Exists(x=>x.Contains("105 seconds")),
            "Actual English inventory panel reports earned stronger stew, not stale base numbers");
        g.save.english=false;
        GUI.commands.Clear();Call(app,"OnGUI");
        Check(GUI.commands.Exists(x=>x.Contains("恢复65生命"))&&
            GUI.commands.Exists(x=>x.Contains("105秒")),
            "Actual Chinese item description updates after real civic supply delivery");
        g.mode="world";
        Console.WriteLine("PASS Live fisherman trade, v2 kitchen upgrade and bilingual inventory buff presentation");
    }

    private static void PrecisionFishingInLiveGameViewDistinguishesBothRealRules(FrontierRuntime app)
    {
        Prepare(app);
        var g=app.Game;g.Enter(0);g.enemies.Clear();g.save.english=true;
        WorldObject pond=Array.Find(g.Map.objects,o=>o.kind=="fish");
        Check(pond!=null,"Shipped town scene contains the real fishing source");
        g.save.x=pond.x;g.save.y=pond.y;
        g.Talk(pond);
        int expert=Array.FindIndex(g.conversation.choices,
            c=>c.action=="fish_expert");
        Check(expert>=0,"Real NPC dialogue exposes optional precision fishing");
        g.Choose(expert);
        Check(g.mode=="fish"&&g.FishExpertCast&&
            Math.Abs(g.FishWindowRadius-.10f)<.001f,
            "Authentic expert action starts a narrow-bite physical minigame");
        GUI.commands.Clear();GUI.buttonCalls.Clear();Call(app,"OnGUI");
        Check(GUI.commands.Exists(x=>x.Contains("PRECISION CAST"))&&
            GUI.commands.Exists(x=>x.Contains("Precision fishing by the lake"))&&
            GUI.commands.Exists(x=>x.Contains("two Silverfin")),
            "Actual English GameView displays the expert cast and earned two-fish goal");
        var reel=GUI.buttonCalls.Find(c=>Math.Abs(c.rect.x-473)<.01f&&
            Math.Abs(c.rect.y-601)<.01f);
        Check(reel!=null&&!reel.enabled,
            "Expert mode does not invent a ready hook before the first real splash");
        for(int i=0;i<90&&!g.FishLineReady;i++)Update(app);
        Check(g.FishLineReady,"Expert mode reaches a genuine bite through runtime Update");
        GUI.commands.Clear();GUI.buttonCalls.Clear();Call(app,"OnGUI");
        reel=GUI.buttonCalls.Find(c=>Math.Abs(c.rect.x-473)<.01f&&
            Math.Abs(c.rect.y-601)<.01f);
        Check(reel!=null&&reel.enabled,
            "Actual expert reel activates only for a live bite");
        int before=g.fishHits+g.fishMisses;
        Input.keys.Add(KeyCode.E);Update(app);
        Check(g.fishHits+g.fishMisses==before+1,
            "Production E key commits at most one honest expert hook or miss");
        g.Close();g.save.english=false;
        g.Talk(pond);expert=Array.FindIndex(g.conversation.choices,
            c=>c.action=="fish_expert");
        g.Choose(expert);
        GUI.commands.Clear();Call(app,"OnGUI");
        Check(GUI.commands.Exists(x=>x.Contains("湖畔 · 精准垂钓"))&&
            GUI.commands.Exists(x=>x.Contains("银鳍鱼 ×2")),
            "Production Chinese HUD names the actual narrower two-fish challenge");
        g.Close();g.Talk(pond);
        int normal=Array.FindIndex(g.conversation.choices,c=>c.action=="fish");
        g.Choose(normal);
        Check(g.mode=="fish"&&!g.FishExpertCast,
            "Leaving expert mode cannot silently make the later standard cast difficult");
        g.save.english=true;
        GUI.commands.Clear();Call(app,"OnGUI");
        Check(GUI.commands.Exists(x=>x.Contains("Fishing by the lake"))&&
            !GUI.commands.Exists(x=>x.Contains("PRECISION CAST")),
            "Existing fishing GameView retains original standard tutorial without leaked expert copy");
        g.Close();
        Console.WriteLine("PASS Bilingual production fishing HUD accurately exposes narrower expert mode and live input");
    }

    private static void LiveFishingReelUsesRealBiteAndProductionHud(FrontierRuntime app)
    {
        Prepare(app);
        var game=app.Game;
        game.Enter(0);game.enemies.Clear();
        WorldObject pond=Array.Find(game.Map.objects,o=>o.kind=="fish");
        Check(pond!=null,"The actual town lake still has a playable fishing source");
        game.save.x=pond.x;game.save.y=pond.y;
        game.Talk(pond);
        Check(game.conversation!=null,"The original lake source opens a real fishing conversation");
        int choice=Array.FindIndex(game.conversation.choices,c=>c.action=="fish");
        Check(choice>=0,"The original lake conversation still offers Cast the line");
        game.Choose(choice);
        Check(game.mode=="fish"&&!game.FishLineReady,
            "Authentic cast starts before the first real bite");
        GUI.commands.Clear();GUI.buttonCalls.Clear();Call(app,"OnGUI");
        var reel=GUI.buttonCalls.Find(c=>Math.Abs(c.rect.x-473)<.01f &&
            Math.Abs(c.rect.y-601)<.01f);
        Check(reel!=null&&!reel.enabled &&
            GUI.commands.Exists(x=>x.Contains("等待咬钩")||x.Contains("Wait for bite")),
            "Shipped fishing HUD disables the real reel while the line is not ready");
        Input.keys.Add(KeyCode.E);Update(app);
        Check(game.fishHits==0&&game.fishMisses==0,
            "Live E input cannot claim a premature bite");
        for(int i=0;i<90&&!game.FishLineReady;i++)Update(app);
        Check(game.FishLineReady,"Real Update() reaches the ready fishing bite");
        GUI.commands.Clear();GUI.buttonCalls.Clear();Call(app,"OnGUI");
        reel=GUI.buttonCalls.Find(c=>Math.Abs(c.rect.x-473)<.01f&&
            Math.Abs(c.rect.y-601)<.01f);
        Check(reel!=null&&reel.enabled,
            "Actual IMGUI Reel button becomes interactable when the fish bites");
        int before=game.fishHits+game.fishMisses;
        Input.keys.Add(KeyCode.E);Update(app);
        Check(game.fishHits+game.fishMisses==before+1&&!game.FishLineReady,
            "Live keyboard E resolves precisely one genuine bite and enters recovery");
        GUI.queuedClick=new Rect(473,601,543,56);Call(app,"OnGUI");
        GUI.queuedClick=null;
        Check(game.fishHits+game.fishMisses==before+1,
            "Same-frame pointer click cannot duplicate the already resolved E reel");
        for(int i=0;i<90&&!game.FishLineReady;i++)Update(app);
        Check(game.FishLineReady,"Actual Update prepares the second separate bite");
        GUI.queuedClick=new Rect(473,601,543,56);Call(app,"OnGUI");
        Check(!GUI.queuedClick.HasValue &&
            game.fishHits+game.fishMisses==before+2,
            "Physical UI pointer Reel consumes the next real bite exactly once");
        game.Close();
        Console.WriteLine("PASS Real fishing dock, E and pointer bite gating with actual IMGUI enabled state");
    }




    private static void JournalAvailableQuestFilterHidesLockedAndCompleteButKeepsRealProgress(
        FrontierRuntime app)
    {
        Prepare(app);
        GameCore game=app.Game;
        game.save.english=true;
        game.save.flags.Add("intro");
        game.mode="journal";
        game.save.trackedQuest="main";
        Set(app,"journalTab",0);
        Set(app,"selectedQuest",0);Set(app,"questPage",0);
        Set(app,"journalOnlyActionable",false);
        int lockedIndex=-1,activeCount=0;
        for(int i=0;i<GameCore.QuestIds.Length;i++)
        {
            QuestGuide guide=game.GetQuest(GameCore.QuestIds[i]);
            if(guide.locked&&lockedIndex<0)lockedIndex=i;
            if(!guide.locked&&!guide.complete)activeCount++;
        }
        Check(lockedIndex>=0&&activeCount>0&&
            activeCount<GameCore.QuestIds.Length,
            "Original quest catalogue contains both available and locked/finished missions");
        int coins=game.save.coins,flags=game.save.flags.Count;
        Set(app,"selectedQuest",lockedIndex);
        Set(app,"questPage",lockedIndex/14);
        GUI.queuedClick=new Rect(279,690,321,28);
        Call(app,"OnGUI");
        GUI.queuedClick=null;
        bool filtered=(bool)typeof(FrontierRuntime).GetField(
            "journalOnlyActionable",Private).GetValue(app);
        int selected=(int)typeof(FrontierRuntime).GetField(
            "selectedQuest",Private).GetValue(app);
        Check(filtered&&selected>=0&&
            !game.GetQuest(GameCore.QuestIds[selected]).locked&&
            !game.GetQuest(GameCore.QuestIds[selected]).complete,
            "Filtering a locked selected quest chooses a real available mission rather than an inaccessible row");
        GUI.commands.Clear();GUI.buttonCalls.Clear();Call(app,"OnGUI");
        Check(GUI.commands.Exists(command=>command.Contains("AVAILABLE ONLY"))&&
            !GUI.commands.Exists(command=>command.Contains(
                game.GetQuest(GameCore.QuestIds[lockedIndex]).title)),
            "Actual IMGUI displays only actionable quest rows, never a future locked title");
        GUI.queuedClick=new Rect(279,690,321,28);
        Call(app,"OnGUI");
        Check(!(bool)typeof(FrontierRuntime).GetField(
            "journalOnlyActionable",Private).GetValue(app)&&
            game.save.trackedQuest=="main"&&
            game.save.coins==coins&&game.save.flags.Count==flags&&
            game.save.version==2,
            "Switching back to the full journal preserves the tracked quest, rewards and SaveData v2");
        game.Close();
        Console.WriteLine("PASS Live journal available/all filter hides inaccessible quests without altering original campaign records");
    }


    private static void MapRegionQuestBrowserOpensCorrectJournalEntryWithoutChangingSaves(
        FrontierRuntime app)
    {
        Prepare(app);
        GameCore game=app.Game;
        game.save.english=true;
        game.save.flags.Add("intro");
        game.save.trackedQuest="main";
        game.mode="map";
        Set(app,"selectedRegion",game.save.room);
        Set(app,"mapInspectedQuestId",null);
        List<string> options=(List<string>)Call(app,"AvailableMapQuestIds",game.save.room);
        Check(options.Count>0,
            "The live starting region has at least one available, unfinished quest");
        foreach(string id in options)
        {
            QuestGuide candidate=game.GetQuest(id);
            Check(!candidate.locked&&!candidate.complete&&candidate.room==game.save.room,
                "Regional map list never contains a locked, finished, or foreign objective");
        }
        Check(((List<string>)Call(app,"AvailableMapQuestIds",-1)).Count==0&&
            ((List<string>)Call(app,"AvailableMapQuestIds",game.data.maps.Length)).Count==0,
            "Invalid region indices cannot expose quest records");
        for(int room=0;room<game.data.maps.Length;room++)
        {
            List<string> region=(List<string>)Call(app,"AvailableMapQuestIds",room);
            Check(room==game.save.room||game.CanEnter(room)||region.Count==0,
                "Only actual open regions can expose actionable quests");
            foreach(string id in region)
            {
                QuestGuide candidate=game.GetQuest(id);
                Check(!candidate.locked&&!candidate.complete&&candidate.room==room,
                    "Every visible regional objective is unlocked, unfinished and in its indicated map");
            }
        }
        Point foot=game.Position;
        Point pin=new Point((float)Math.Floor(foot.x)+.5f,
            (float)Math.Floor(foot.y)+.5f);
        Check(game.PlaceLocalWaymark(pin)&&game.HasLocalWaymark,
            "Personal navigation already exists before browsing regional missions");
        Point savedPin=game.LocalWaymark.Value;
        int coins=game.save.coins,flags=game.save.flags.Count;
        GUI.queuedClick=new Rect(379,672,77,24);
        Call(app,"OnGUI");GUI.queuedClick=null;
        Check((string)typeof(FrontierRuntime).GetField(
                "mapInspectedQuestId",Private).GetValue(app)==options[0],
            "Next regional quest chooses the first physically available authored mission");
        GUI.commands.Clear();Call(app,"OnGUI");
        Check(GUI.commands.Exists(x=>x.Contains("INSPECTING:"))&&
            GUI.commands.Exists(x=>x.Contains(game.GetQuest(options[0]).title)),
            "Actual IMGUI map exposes the selected regional mission and its destination");
        GUI.queuedClick=new Rect(291,672,77,24);
        Call(app,"OnGUI");GUI.queuedClick=null;
        string last=options[options.Count-1];
        Check((string)typeof(FrontierRuntime).GetField(
                "mapInspectedQuestId",Private).GetValue(app)==last,
            "Previous from first mission wraps to the last regional objective");
        // Follow the real UI to the precise journal page even when an old
        // journal view-only filter was previously enabled.
        Set(app,"journalOnlyActionable",true);
        GUI.queuedClick=new Rect(467,672,169,24);
        Call(app,"OnGUI");GUI.queuedClick=null;
        int selected=Array.IndexOf(GameCore.QuestIds,last);
        Check(game.mode=="journal"&&
            (int)typeof(FrontierRuntime).GetField(
                "selectedQuest",Private).GetValue(app)==selected&&
            (int)typeof(FrontierRuntime).GetField(
                "questPage",Private).GetValue(app)==selected/14&&
            !(bool)typeof(FrontierRuntime).GetField(
                "journalOnlyActionable",Private).GetValue(app),
            "Open in journal selects the exact original quest and correct page");
        Check(game.save.trackedQuest=="main"&&
            game.HasLocalWaymark&&
            (game.LocalWaymark.Value-savedPin).Length<.001f&&
            game.save.coins==coins&&game.save.flags.Count==flags&&
            game.save.version==2,
            "Browsing and jumping to the journal never overwrites saved tracking, personal pin, or rewards");
        game.Close();
        Console.WriteLine("PASS Live region quest map browser cycles accessible objectives and opens their exact journal entries without save mutations");
    }

    private static void LiveMapObjectiveClickInspectsQuestWithoutMovingPersonalPin(
        FrontierRuntime app)
    {
        Prepare(app);
        GameCore game=app.Game;
        game.save.flags.Add("intro");
        game.save.english=true;
        game.save.trackedQuest="main";
        game.mode="map";
        Set(app,"selectedRegion",game.save.room);
        Set(app,"mapInspectedQuestId",null);
        List<string> candidates=(List<string>)Call(app,
            "AvailableMapQuestIds",game.save.room);
        QuestGuide target=null;
        foreach(string id in candidates)
        {
            QuestGuide q=game.GetQuest(id);
            if(q.target.x>=0&&q.target.y>=0&&
                q.target.x<game.Map.rows[0].Length&&
                q.target.y<game.Map.rows.Length)
            {target=q;break;}
        }
        Check(target!=null,
            "The real region contains a drawable, actionable map target");
        float mapScale=WorldStreaming.PreviewScale(
            game.Map.rows[0].Length,game.Map.rows.Length,306,207);
        Check(mapScale>0f,"The production terrain has a valid real map scale");
        Point origin=game.Position;
        Point pin=new Point((float)Math.Floor(origin.x)+.5f,
            (float)Math.Floor(origin.y)+.5f);
        Check(game.PlaceLocalWaymark(pin),
            "The actual player waypoint is set before quest map selection");
        Point retained=game.LocalWaymark.Value;
        int coins=game.save.coins,flags=game.save.flags.Count;
        // Use the real screen map click surface, not a direct helper call.
        Input.mousePosition=new Vector3(
            888+target.target.x*mapScale,
            800-(264+target.target.y*mapScale),0);
        GUI.queuedClick=new Rect(888,264,
            game.Map.rows[0].Length*mapScale,
            game.Map.rows.Length*mapScale);
        Call(app,"OnGUI");
        Check(!GUI.queuedClick.HasValue,
            "A real IMGUI terrain click is consumed by the live map");
        string selected=(string)typeof(FrontierRuntime).GetField(
            "mapInspectedQuestId",Private).GetValue(app);
        Check(candidates.Contains(selected),
            "Clicking an available teal objective inspects a real available mission");
        Check((game.GetQuest(selected).target-target.target).Length*mapScale<=8f,
            "The inspected mission belongs to the clicked map marker");
        Check(game.HasLocalWaymark&&
            (game.LocalWaymark.Value-retained).Length<.001f&&
            game.save.trackedQuest=="main"&&game.save.coins==coins&&
            game.save.flags.Count==flags&&game.save.version==2,
            "Selecting a quest marker cannot replace a personal pin, track automatically or grant rewards");
        // Empty map terrain still retains its original physical pin feature.
        Point clear=new Point(-100,-100);
        Check(!(bool)Call(app,"TryInspectMapQuestAt",clear,mapScale,candidates),
            "Clicks outside every available objective cannot select a fake quest");
        Set(app,"selectedRegion",game.data.maps.Length);
        Check(!(bool)Call(app,"TryInspectMapQuestAt",target.target,mapScale,
            new List<string>()),
            "No remote/invalid mission can be inspected from an empty region");
        game.Close();
        Console.WriteLine("PASS Live map objective clicks inspect unlocked missions without replacing personal navigation");
    }

    private static void JournalCanPinRealLocalQuestWithoutRetargetingOrSpoofingMap(
        FrontierRuntime app)
    {
        Prepare(app);
        GameCore game=app.Game;
        game.save.flags.Add("intro");
        game.save.english=true;
        game.save.showGuidance=false;
        game.save.trackedQuest="main";
        game.mode="map";
        Point origin=game.Position;
        Point initial=new Point((float)Math.Floor(origin.x)+.5f,
            (float)Math.Floor(origin.y)+.5f);
        Check(game.PlaceLocalWaymark(initial),
            "Existing personal map destination is placed before reading journal");
        Point retained=game.LocalWaymark.Value;
        int coins=game.save.coins,flags=game.save.flags.Count;
        game.mode="journal";
        Check(!game.PlaceQuestApproachWaymark("not_an_authored_quest")&&
            game.HasLocalWaymark&&
            (game.LocalWaymark.Value-retained).Length<.001f,
            "The journal cannot pin invented quest IDs or erase valid personal navigation");
        string questId=null;Point destination=new Point();
        foreach(string id in GameCore.QuestIds)
        {
            QuestGuide q=game.GetQuest(id);
            if(q.locked||q.complete||q.room!=game.save.room)continue;
            List<Point> path=game.NavigationPathFrom(origin,q.target,true);
            if(path.Count==0)continue;
            Point approach=path[path.Count-1];
            if(game.InspectLocalWaymark(approach)!=WaymarkRouteStatus.Available)
                continue;
            questId=id;destination=approach;break;
        }
        Check(questId!=null,
            "A currently unlocked, unfinished local quest has a real walkable journal approach");
        int index=Array.IndexOf(GameCore.QuestIds,questId);
        Set(app,"journalTab",0);Set(app,"journalOnlyActionable",false);
        Set(app,"selectedQuest",index);Set(app,"questPage",index/14);
        GUI.buttonCalls.Clear();GUI.commands.Clear();Call(app,"OnGUI");
        var action=GUI.buttonCalls.Find(b=>Math.Abs(b.rect.x-650)<.001f&&
            Math.Abs(b.rect.y-693)<.001f&&
            Math.Abs(b.rect.width-540)<.001f);
        Check(action!=null&&action.enabled&&
            GUI.commands.Exists(x=>x.Contains("REPLACE PIN")),
            "The actual journal shows an explicit enabled local navigation replacement");
        GUI.queuedClick=new Rect(650,693,540,32);
        Call(app,"OnGUI");GUI.queuedClick=null;
        Check(game.mode=="journal"&&game.HasLocalWaymark&&
            (game.LocalWaymark.Value-destination).Length<.001f&&
            game.save.trackedQuest=="main"&&
            game.save.coins==coins&&game.save.flags.Count==flags&&
            game.save.version==2,
            "The real journal click establishes the physically reachable objective approach without changing mode, tracking or rewards");
        game.mode="world";
        Check(!game.PlaceQuestApproachWaymark(questId)&&
            (game.LocalWaymark.Value-destination).Length<.001f,
            "World attacks or modal transitions cannot fake a journal pin operation");
        game.Close();
        Console.WriteLine("PASS Journal local-quest route button pins a real physical approach and preserves mission tracking and SaveData v2");
    }

    private static void QuestWaymarkFollowsRealTrailStagesWithoutChangingManualPins(
        FrontierRuntime app)
    {
        Prepare(app);
        GameCore game=app.Game;
        game.save.english=true;game.save.flags.Add("intro");
        int room=game.save.room;
        Check(room>=0&&room<8,"The review scenario is an outdoor survey region");
        game.save.flags.Remove("trail_circuit_"+room+"_1");
        game.save.flags.Remove("trail_circuit_"+room+"_2");
        game.save.flags.Remove("trail_circuit_"+room+"_3");
        game.save.trackedQuest="main";
        Point origin=game.Position;
        WorldObject first=null,second=null;
        foreach(WorldObject o in game.data.maps[room].objects)
        {
            if(o.id=="trailcircuit_"+room+"_1")first=o;
            if(o.id=="trailcircuit_"+room+"_2")second=o;
        }
        Check(first!=null&&second!=null,"The campaign has two genuine sequential route markers");
        Point oldFirst=new Point(first.x,first.y),oldSecond=new Point(second.x,second.y);
        try
        {
            // Exercise real authored quest stage logic on real collision
            // terrain; only relocate objects inside the disposable fixture.
            List<Point> candidates=new List<Point>();
            for(int radius=3;radius<=13&&candidates.Count<2;radius++)
                for(int dx=-radius;dx<=radius&&candidates.Count<2;dx++)
                    for(int dy=-radius;dy<=radius;dy++)
                    {
                        if(Math.Abs(dx)!=radius&&Math.Abs(dy)!=radius)continue;
                        Point candidate=new Point(
                            (float)Math.Floor(origin.x)+dx+.5f,
                            (float)Math.Floor(origin.y)+dy+.5f);
                        if(candidate.x<2||candidate.y<2||
                           candidate.x>=game.Map.rows[0].Length-2||
                           candidate.y>=game.Map.rows.Length-2||
                           !game.CanStand(candidate))continue;
                        List<Point> route=game.NavigationPathFrom(origin,candidate,true);
                        if(route.Count<2)continue;
                        if(game.InspectLocalWaymark(route[route.Count-1])!=
                            WaymarkRouteStatus.Available)continue;
                        if(candidates.Count>0&&
                            (candidate-candidates[0]).Length<4)continue;
                        candidates.Add(candidate);break;
                    }
            Check(candidates.Count==2,
                "Two separate physically reachable bearings exist in the live terrain");
            first.x=candidates[0].x;first.y=candidates[0].y;
            second.x=candidates[1].x;second.y=candidates[1].y;
            QuestGuide start=game.GetQuest("trail_circuits");
            Check(!start.locked&&!start.complete&&start.room==game.save.room&&
                (start.target-candidates[0]).Length<.001f,
                "Authentic trail quest targets its first unfinished marker");
            game.mode="journal";
            Check(game.PlaceQuestApproachWaymark("trail_circuits")&&
                game.QuestApproachNavigationActive,
                "Journal-created waypoint retains transient provenance for its authored quest");
            Point initial=game.LocalWaymark.Value;
            int coins=game.save.coins,originalFlags=game.save.flags.Count;
            game.mode="world";
            game.save.flags.Add("trail_circuit_"+room+"_1");
            QuestGuide advanced=game.GetQuest("trail_circuits");
            Check((advanced.target-candidates[1]).Length<.001f,
                "First survey advances the genuine guide to the second marker");
            List<Point> physical=game.NavigationPathFrom(origin,advanced.target,true);
            Check(physical.Count>0,"Advanced stage has a real physically reachable path");
            Point expected=physical[physical.Count-1];
            for(int i=0;i<28;i++)game.Tick(.05f,new Point());
            Check(game.QuestApproachNavigationActive&&
                (game.LocalWaymark.Value-expected).Length<.001f&&
                (game.LocalWaymark.Value-initial).Length>1f&&
                game.save.trackedQuest=="main"&&game.save.coins==coins&&
                game.save.flags.Count==originalFlags+1,
                "Quest route follows next physical bearing without altering saved tracking or rewards");
            game.mode="map";
            Check(game.PlaceLocalWaymark(initial)&&
                !game.QuestApproachNavigationActive,
                "Explicit manual pin replacement revokes story-follow ownership");
            game.mode="world";
            game.save.flags.Add("trail_circuit_"+room+"_2");
            for(int i=0;i<28;i++)game.Tick(.05f,new Point());
            Check(game.HasLocalWaymark&&
                (game.LocalWaymark.Value-initial).Length<.001f&&
                game.save.version==2,
                "Further story progress cannot retarget a hand-placed pin or change SaveData v2");
            game.mode="map";game.ClearLocalWaymark();
            game.save.flags.Remove("trail_circuit_"+room+"_2");
            game.mode="journal";
            Check(game.PlaceQuestApproachWaymark("trail_circuits"),
                "An authorized quest pin can be created independently");
            game.mode="world";
            for(int region=0;region<8;region++)
                game.save.flags.Add("trail_circuit_"+region+"_3");
            Check(game.GetQuest("trail_circuits").complete,
                "The authentic fully surveyed field quest is completed");
            for(int i=0;i<28;i++)game.Tick(.05f,new Point());
            Check(!game.QuestApproachNavigationActive&&!game.HasLocalWaymark,
                "Quest-owned navigation retires after its original quest is complete");
        }
        finally
        {
            first.x=oldFirst.x;first.y=oldFirst.y;
            second.x=oldSecond.x;second.y=oldSecond.y;
        }
        game.Close();
        Console.WriteLine("PASS Quest-owned navigation follows real field survey stages, preserves manual pins and retires on quest completion");
    }

    private static void TrackedQuestAutoFollowSurvivesRealCrossRegionStageChange(
        FrontierRuntime app)
    {
        Prepare(app);
        GameCore g=app.Game;
        g.save.english=true;g.save.flags.Add("intro");
        g.save.flags.Add("scout_saved");g.save.stage=4;
        int room=g.save.room;
        Check(room>=0&&room<8,"Use a live regional outdoor expedition");
        WorldObject first=null;
        foreach(WorldObject o in g.Map.objects)
            if(o.id=="trailcircuit_"+room+"_1"){first=o;break;}
        Check(first!=null,"An existing campaign survey has a physical first-stage site");
        Point original=new Point(first.x,first.y);
        for(int stage=1;stage<=3;stage++)
            g.save.flags.Remove("trail_circuit_"+room+"_"+stage);
        try
        {
            bool found=false;
            for(int radius=3;radius<=12&&!found;radius++)
                for(int dx=-radius;dx<=radius&&!found;dx++)
                    for(int dy=-radius;dy<=radius;dy++)
                    {
                        if(Math.Abs(dx)!=radius&&Math.Abs(dy)!=radius)continue;
                        Point p=new Point((float)Math.Floor(g.Position.x)+dx+.5f,
                            (float)Math.Floor(g.Position.y)+dy+.5f);
                        if(p.x<2||p.y<2||p.x>=g.Map.rows[0].Length-2||
                           p.y>=g.Map.rows.Length-2||!g.CanStand(p)||
                           g.InspectLocalWaymark(p)!=WaymarkRouteStatus.Available)
                            continue;
                        first.x=p.x;first.y=p.y;
                        if(g.GetQuest("trail_circuits").room==room)
                        {found=true;break;}
                    }
            Check(found,"The real first-stage marker has an authentic local physical approach");
            g.TrackQuest("trail_circuits");
            g.mode="journal";
            Check(g.PlaceQuestApproachWaymark("trail_circuits")&&
                g.QuestApproachNavigationActive,
                "Player explicitly enables quest navigation while locally tracking the field survey");
            int coin=g.save.coins,shards=g.save.shards;
            g.mode="world";
            for(int stage=1;stage<=3;stage++)
                g.save.flags.Add("trail_circuit_"+room+"_"+stage);
            QuestGuide next=g.GetQuest("trail_circuits");
            Check(!next.complete&&!next.locked&&next.room!=room&&
                g.CanEnter(next.room),
                "Original authored quest turns into an unlocked distant objective after the regional survey");
            for(int i=0;i<28;i++)g.Tick(.05f,new Point());
            Check(g.QuestApproachTravelHandoffPending&&!g.HasLocalWaymark&&
                g.save.trackedQuest=="trail_circuits",
                "Auto-follow converts the stale local pin into cross-region navigation when the next stage changes region");
            int destination=next.room;
            g.Enter(destination);
            Check(g.save.room==destination&&
                g.save.trackedQuest=="trail_circuits",
                "Travel occurs through the real region transition without retargeting a saved quest");
            if(g.HasLocalWaymark)
                Check(g.QuestApproachNavigationActive&&
                    !g.QuestApproachTravelHandoffPending&&
                    g.InspectLocalWaymark(g.LocalWaymark.Value)==WaymarkRouteStatus.Available,
                    "Authoritative destination route pins only physically walkable quest approach");
            else Check(g.QuestApproachTravelHandoffPending,
                "A still-sealed destination retains only pending navigation, not a fictional route");
            Check(g.save.version==2&&g.save.coins==coin&&g.save.shards==shards,
                "Regional quest-follow never grants loot or migrates SaveData");
        }
        finally{first.x=original.x;first.y=original.y;}
        g.Close();
        Console.WriteLine("PASS Tracked multi-region quest stage follows genuine regional gates and verifies new destination approach");
    }

    private static void QuestStageClosureDoesNotAdvertiseStaleArrivalAndRecovers(
        FrontierRuntime app)
    {
        Prepare(app);
        GameCore g=app.Game;
        g.save.flags.Add("intro");g.save.english=true;
        int room=g.save.room;
        Check(room>=0&&room<8,"Test uses actual outdoor review room");
        WorldObject first=null,second=null;
        foreach(WorldObject o in g.Map.objects)
        {
            if(o.id=="trailcircuit_"+room+"_1")first=o;
            if(o.id=="trailcircuit_"+room+"_2")second=o;
        }
        Check(first!=null&&second!=null,"Both physical bearing objects exist");
        Point oldA=new Point(first.x,first.y),oldB=new Point(second.x,second.y);
        g.save.flags.Remove("trail_circuit_"+room+"_1");
        g.save.flags.Remove("trail_circuit_"+room+"_2");
        g.save.flags.Remove("trail_circuit_"+room+"_3");
        try
        {
            Point origin=g.Position,chosen=new Point();
            bool found=false;
            for(int r=3;r<13&&!found;r++)
                for(int dx=-r;dx<=r&&!found;dx++)
                    for(int dy=-r;dy<=r;dy++)
                    {
                        if(Math.Abs(dx)!=r&&Math.Abs(dy)!=r)continue;
                        Point p=new Point((float)Math.Floor(origin.x)+dx+.5f,
                            (float)Math.Floor(origin.y)+dy+.5f);
                        if(p.x<2||p.y<2||p.x>=g.Map.rows[0].Length-2||
                            p.y>=g.Map.rows.Length-2||!g.CanStand(p))continue;
                        var path=g.NavigationPathFrom(origin,p,true);
                        if(path.Count<2)continue;
                        chosen=p;found=true;break;
                    }
            Check(found,"A genuinely walkable first marker can be reached");
            first.x=chosen.x;first.y=chosen.y;
            g.mode="journal";
            Check(g.PlaceQuestApproachWaymark("trail_circuits"),
                "Authored live quest pins first-stage route");
            Point oldPin=g.LocalWaymark.Value;
            // Stand precisely at the validated approach: an arrival
            // notification becomes visible before the stage advances.
            g.save.x=oldPin.x;g.save.y=oldPin.y;g.mode="world";
            g.Tick(.05f,new Point());
            Check(g.QuestApproachAwaitingInteraction,
                "Actual arrival waits for first marker interaction");
            // Next real authored stage becomes unreachable. A stale prior
            // arrival MUST NOT remain actionable while the new road is shut.
            second.x=-40;second.y=-40;
            g.save.flags.Add("trail_circuit_"+room+"_1");
            for(int i=0;i<28;i++)g.Tick(.05f,new Point());
            Check(g.QuestApproachNavigationActive&&
                g.QuestApproachNextStageBlocked&&
                !g.QuestApproachAwaitingInteraction&&
                g.LocalWaymarkRouteBlocked&&
                (g.LocalWaymark.Value-oldPin).Length<.001f&&
                !g.NavigationTarget().HasValue&&!g.PersonalNavigationActive,
                "Unreachable successor preserves fallback only as a record, never as the live objective");
            GUI.commands.Clear();Call(app,"OnGUI");
            Check(GUI.commands.Exists(x=>x.Contains("NEXT QUEST STEP BLOCKED"))&&
                !GUI.commands.Exists(x=>x.Contains("AT QUEST"))&&
                !GUI.commands.Exists(x=>x.Contains("ROUTE BLOCKED")),
                "World HUD explicitly identifies the inaccessible NEW stage instead of routing to an obsolete site");
            g.mode="map";Set(app,"selectedRegion",g.save.room);
            GUI.commands.Clear();Call(app,"OnGUI");
            Check(GUI.commands.Exists(x=>x.Contains("Quest step blocked")&&
                x.Contains("Clear old pin")),
                "Actual map distinguishes the retained obsolete quest marker from a valid personal route");
            g.mode="world";
            // The live objective can return to the former physical site
            // after a retry. Previously the unchanged target coordinate
            // bypassed the only recovery branch and stranded blocked state.
            second.x=first.x;second.y=first.y;
            for(int i=0;i<28;i++)g.Tick(.05f,new Point());
            Check(!g.QuestApproachNextStageBlocked&&
                !g.LocalWaymarkRouteBlocked&&g.QuestApproachNavigationActive&&
                g.PersonalNavigationActive,
                "A returned and genuinely walkable objective clears pending next-stage obstruction");
            second.x=oldB.x;second.y=oldB.y;
            for(int i=0;i<28;i++)g.Tick(.05f,new Point());
            if(g.QuestApproachNextStageBlocked)
                Check(g.LocalWaymarkRouteBlocked,
                    "A later still-inaccessible real objective stays blocked");
            Check(g.save.version==2,"Quest road recovery changes no persistent save schema");
        }
        finally
        {first.x=oldA.x;first.y=oldA.y;second.x=oldB.x;second.y=oldB.y;}
        g.Close();
        Console.WriteLine("PASS Newly blocked quest stage cancels stale arrival, preserves fallback and checks physical recovery");
    }

    private static void QuestApproachAtContactShowsInteractionAndKeepsAutoFollow(
        FrontierRuntime app)
    {
        Prepare(app);
        GameCore g=app.Game;
        g.save.flags.Add("intro");
        g.save.english=true;
        g.save.showGuidance=false;
        g.mode="journal";
        string chosen=null;
        foreach(string id in GameCore.QuestIds)
        {
            QuestGuide q=g.GetQuest(id);
            if(q.locked||q.complete||q.room!=g.save.room)continue;
            if(g.NavigationPathFrom(g.Position,q.target,true).Count>0)
            {chosen=id;break;}
        }
        Check(chosen!=null,"An authentic local quest has an actual walkable approach");
        Check(g.PlaceQuestApproachWaymark(chosen)&&
            g.QuestApproachNavigationActive,
            "Real journal interaction creates a quest-owned transient waypoint");
        Point site=g.LocalWaymark.Value;
        // Reproduce genuine arrival at the verified standing tile; unlike
        // a plain map pin it must remain until the quest is interacted with.
        g.save.x=site.x;g.save.y=site.y;
        g.mode="world";
        g.Tick(.05f,new Point());
        Check(g.QuestApproachNavigationActive&&
            g.QuestApproachAwaitingInteraction&&
            g.HasLocalWaymark&&g.PersonalNavigationActive,
            "Arriving at a quest waypoint retains its stage ownership and requests interaction");
        GUI.commands.Clear();Call(app,"OnGUI");
        Check(GUI.commands.Exists(x=>x.Contains("AT QUEST")&&x.Contains("INTERACT")),
            "The actual world minimap explains that the next action is to interact");
        g.mode="map";Set(app,"selectedRegion",g.save.room);
        GUI.commands.Clear();GUI.buttonCalls.Clear();Call(app,"OnGUI");
        Check(GUI.commands.Exists(x=>x.Contains("Cancel quest route auto-follow")),
            "The real local map offers a distinct cancellation action for quest-follow navigation");
        GUI.queuedClick=new Rect(888,571,306,35);
        Call(app,"OnGUI");GUI.queuedClick=null;
        Check(!g.QuestApproachNavigationActive&&!g.HasLocalWaymark&&
            g.save.version==2,
            "Map cancellation retires only transient auto-follow and never migrates old saves");
        g.Close();
        Console.WriteLine("PASS Quest arrival retains interaction route and exposes auto-follow/cancel state in live world and map UI");
    }

    private static void QuestApproachPinUsesVerifiedPhysicalRouteAndPreservesTracking(
        FrontierRuntime app)
    {
        Prepare(app);
        GameCore game=app.Game;
        game.save.flags.Add("intro");
        game.save.english=true;game.save.showGuidance=false;
        game.mode="map";
        Set(app,"selectedRegion",game.save.room);
        game.save.trackedQuest="main";
        Point origin=game.Position;
        Point oldPin=new Point((float)Math.Floor(origin.x)+.5f,
            (float)Math.Floor(origin.y)+.5f);
        Check(game.PlaceLocalWaymark(oldPin)&&game.HasLocalWaymark,
            "An existing personal destination is preserved unless replacement succeeds");
        Point retained=game.LocalWaymark.Value;
        int coins=game.save.coins,flags=game.save.flags.Count;
        QuestGuide probe=null;
        List<Point> expected=null;
        // Use an ACTUAL authored quest, not a made-up object that would
        // bypass the same-ID, same-stage validations as the live UI.
        foreach(string id in GameCore.QuestIds)
        {
            QuestGuide candidate=game.GetQuest(id);
            if(candidate.locked||candidate.complete||
                candidate.room!=game.save.room)continue;
            List<Point> path=game.NavigationPathFrom(origin,candidate.target,true);
            if(path.Count==0||game.InspectLocalWaymark(
                path[path.Count-1])!=WaymarkRouteStatus.Available)continue;
            probe=candidate;expected=path;break;
        }
        Check(probe!=null,
            "The live map has a current, unlocked quest with a physically walkable approach");
        Point predicted=expected[expected.Count-1];
        QuestGuide forged=new QuestGuide {id="not_an_authored_quest",
            room=game.save.room,target=probe.target};
        QuestGuide locked=new QuestGuide {id=probe.id,room=game.save.room,
            target=probe.target,locked=true};
        QuestGuide completed=new QuestGuide {id=probe.id,room=game.save.room,
            target=probe.target,complete=true};
        QuestGuide remote=new QuestGuide {id=probe.id,room=game.save.room+1,
            target=probe.target};
        Check(!(bool)Call(app,"PinQuestWalkingApproach",forged)&&
            !(bool)Call(app,"PinQuestWalkingApproach",locked)&&
            !(bool)Call(app,"PinQuestWalkingApproach",completed)&&
            !(bool)Call(app,"PinQuestWalkingApproach",remote)&&
            game.HasLocalWaymark&&
            (game.LocalWaymark.Value-retained).Length<.001f,
            "Map entry rejects forged, locked, finished and remote objectives without changing a valid pin");
        // The ACTUAL map button must call the same GameCore operation as
        // the journal button, without changing the tracked quest.
        Set(app,"mapInspectedQuestId",probe.id);
        GUI.commands.Clear();GUI.buttonCalls.Clear();Call(app,"OnGUI");
        var action=GUI.buttonCalls.Find(b=>Math.Abs(b.rect.x-888)<.001f&&
            Math.Abs(b.rect.y-693)<.001f&&Math.Abs(b.rect.width-306)<.001f);
        Check(action!=null&&action.enabled&&
            GUI.commands.Exists(x=>x.Contains("REPLACE PIN")),
            "Production map presents an enabled explicit route replacement for the reachable quest");
        GUI.queuedClick=new Rect(888,693,306,32);
        Call(app,"OnGUI");GUI.queuedClick=null;
        Check(game.HasLocalWaymark&&
            (game.LocalWaymark.Value-predicted).Length<.001f,
            "The actual map pin button reaches the same validated physical tile as the journal");
        Check(game.PersonalNavigationActive&&
            game.NavigationTarget().HasValue&&
            (game.NavigationTarget().Value-predicted).Length<.001f,
            "A quest approach pin actually takes over normal in-world compass routing");
        Check(game.save.trackedQuest=="main"&&game.save.coins==coins&&
            game.save.flags.Count==flags&&game.save.version==2,
            "Map quest route pin does not change tracked quest, story rewards or SaveData v2");
        game.Close();
        Console.WriteLine("PASS Map and journal share authored-quest physical waypoint routing with live map click and save safety");
    }

    private static void InspectedQuestRouteUsesReachablePhysicalApproachWithoutRetargeting(
        FrontierRuntime app)
    {
        Prepare(app);
        GameCore game=app.Game;
        game.save.english=true;game.mode="map";
        Set(app,"selectedRegion",game.save.room);
        game.save.trackedQuest="main";
        int coins=game.save.coins,flags=game.save.flags.Count;
        Point origin=game.Position;
        QuestGuide probe=new QuestGuide {id="preview_probe",room=game.save.room};
        List<Point> expected=null;
        // Pick a real walkable destination in the existing loaded scene,
        // avoiding test-only geometry that bypasses the production collision.
        for(int radius=2;radius<=8&&expected==null;radius++)
            for(int dx=-radius;dx<=radius&&expected==null;dx++)
                for(int dy=-radius;dy<=radius;dy++)
                {
                    if(Math.Abs(dx)!=radius&&Math.Abs(dy)!=radius)continue;
                    Point target=new Point(
                        (float)Math.Floor(origin.x)+dx+.5f,
                        (float)Math.Floor(origin.y)+dy+.5f);
                    if(target.x<1||target.y<1||
                        target.x>=game.Map.rows[0].Length-1||
                        target.y>=game.Map.rows.Length-1)continue;
                    List<Point> attempt=game.NavigationPathFrom(origin,target,true);
                    if(attempt.Count>=2){expected=attempt;probe.target=target;break;}
                }
        Check(expected!=null,"The real scene has a physically reachable route for inspected quest preview");
        var first=(List<Point>)Call(app,"InspectedQuestWalkingRoute",probe);
        Check(first.Count==expected.Count&&
            (first[first.Count-1]-expected[expected.Count-1]).Length<.001f,
            "Quest preview follows the real full-finish collision path, not a straight line");
        var again=(List<Point>)Call(app,"InspectedQuestWalkingRoute",probe);
        Check(object.ReferenceEquals(first,again),
            "Repeated Layout/Repaint reuses the inspected route instead of rerunning pathfinding");
        QuestGuide different=new QuestGuide {id="another_preview",room=probe.room,
            target=probe.target};
        var next=(List<Point>)Call(app,"InspectedQuestWalkingRoute",different);
        Check(!object.ReferenceEquals(first,next)&&next.Count==expected.Count,
            "Changing inspected quest invalidates the cached route even at the same coordinates");
        QuestGuide impossible=new QuestGuide {id="blocked_preview",room=probe.room,
            target=new Point(-10,-10)};
        var blocked=(List<Point>)Call(app,"InspectedQuestWalkingRoute",impossible);
        Check(blocked.Count==0,
            "An invalid or inaccessible objective never paints a fictitious route");
        different.room=game.save.room==0?1:0;
        Check(((List<Point>)Call(app,"InspectedQuestWalkingRoute",different)).Count==0,
            "Inspecting a different region cannot reuse a local walking route");
        Check(game.save.trackedQuest=="main"&&game.save.coins==coins&&
            game.save.flags.Count==flags&&game.save.version==2,
            "The quest preview never changes tracked mission, rewards or SaveData v2");
        // Exercise the actual Frontier.unity IMGUI map renderer, not only
        // the helper. Its status must be visible and distinguish reachable
        // targets from a blocked or adjacent objective.
        QuestGuide displayed=null;
        foreach(string id in GameCore.QuestIds)
        {
            QuestGuide candidate=game.GetQuest(id);
            if(!candidate.locked&&!candidate.complete&&
                candidate.room==game.save.room){displayed=candidate;break;}
        }
        Check(displayed!=null,"At least one unlocked quest can be inspected on the active map");
        Set(app,"mapInspectedQuestId",displayed.id);
        GUI.commands.Clear();GUI.buttonCalls.Clear();
        Call(app,"OnGUI");
        Check(GUI.commands.Exists(x=>x.Contains("WALKABLE APPROACH")||
            x.Contains("NEAR OBJECTIVE")||
            x.Contains("NO VERIFIED WALKING APPROACH")),
            "The live map identifies whether the inspected objective has a verified walkable approach");
        Check(game.save.trackedQuest=="main"&&game.save.coins==coins&&
            game.save.flags.Count==flags,
            "Displaying route availability does not redirect player navigation or mutate quest progress");
        game.Close();
        Console.WriteLine("PASS Inspected map quest draws cached physically reachable routes without retargeting or bypassing gates");
    }

    private static void InspectedQuestCanBeTrackedByExplicitMapActionWithoutErasingPersonalPin(
        FrontierRuntime app)
    {
        Prepare(app);
        GameCore game=app.Game;
        game.save.flags.Add("intro");game.save.english=true;
        game.save.trackedQuest="main";
        int index=Array.IndexOf(GameCore.QuestIds,"trail_circuits");
        Check(index>=0,"Original field expedition is registered for map quest promotion");
        game.mode="journal";
        Set(app,"journalTab",0);Set(app,"selectedQuest",index);
        Set(app,"questPage",index/14);
        GUI.queuedClick=new Rect(927,647,263,31);
        Call(app,"OnGUI");GUI.queuedClick=null;
        Check(game.mode=="map"&&game.save.trackedQuest=="main",
            "The selected quest is first inspected independently of player quest tracking");
        Point origin=game.Position;
        Point tile=new Point((float)Math.Floor(origin.x)+.5f,
            (float)Math.Floor(origin.y)+.5f);
        Check(game.PlaceLocalWaymark(tile)&&game.HasLocalWaymark,
            "A player-authored physical map pin can coexist with quest inspection");
        Point savedPin=game.LocalWaymark.Value;
        GUI.buttonCalls.Clear();GUI.commands.Clear();Call(app,"OnGUI");
        var action=GUI.buttonCalls.Find(b=>Math.Abs(b.rect.x-888)<.01f&&
            Math.Abs(b.rect.y-541)<.01f);
        Check(action!=null&&action.enabled&&
            GUI.commands.Exists(x=>x.Contains("TRACK INSPECTED QUEST")),
            "Live map exposes an explicit, enabled track control for the inspected unfinished mission");
        int coins=game.save.coins;
        GUI.queuedClick=new Rect(888,541,306,24);
        Call(app,"OnGUI");
        Check(!GUI.queuedClick.HasValue&&
            game.save.trackedQuest=="trail_circuits"&&
            game.HasLocalWaymark&&
            (game.LocalWaymark.Value-savedPin).Length<.001f&&
            game.save.coins==coins&&game.save.version==2,
            "Only the deliberately clicked track control changes the tracked mission; personal pin and saved economy remain intact");
        GUI.buttonCalls.Clear();GUI.commands.Clear();Call(app,"OnGUI");
        action=GUI.buttonCalls.Find(b=>Math.Abs(b.rect.x-888)<.01f&&
            Math.Abs(b.rect.y-541)<.01f);
        Check(action!=null&&!action.enabled&&
            GUI.commands.Exists(x=>x.Contains("TRACKING THIS QUEST")),
            "The same inspected map shows tracked status without offering a duplicate action");
        game.Close();
        Console.WriteLine("PASS Explicit map quest tracking preserves personal waypoint, economy, and SaveData v2");
    }

    private static void JournalShowDestinationInspectsSelectedQuestWithoutChangingTracking(
        FrontierRuntime app)
    {
        Prepare(app);
        GameCore game=app.Game;
        game.save.flags.Add("intro");game.save.english=true;
        game.save.trackedQuest="main";
        int selection=Array.IndexOf(GameCore.QuestIds,"trail_circuits");
        Check(selection>=0,"The original eight-region field quest is registered");
        QuestGuide expected=game.GetQuest("trail_circuits");
        Check(!expected.locked&&expected.room>=0&&
            expected.room<game.data.maps.Length,
            "Journal test uses a genuine unlocked field quest and its real region");
        game.mode="journal";
        Set(app,"journalTab",0);
        Set(app,"selectedQuest",selection);
        Set(app,"questPage",selection/14);
        int coins=game.save.coins,flagCount=game.save.flags.Count;
        GUI.queuedClick=new Rect(927,647,263,31);
        Call(app,"OnGUI");
        Check(game.mode=="map"&&
            (string)typeof(FrontierRuntime).GetField(
                "mapInspectedQuestId",Private).GetValue(app)=="trail_circuits"&&
            game.save.trackedQuest=="main"&&
            (int)typeof(FrontierRuntime).GetField(
                "selectedRegion",Private).GetValue(app)==expected.room,
            "Show destination opens the SELECTED quest's region without silently changing the tracked main story");
        GUI.commands.Clear();Call(app,"OnGUI");
        Check(GUI.commands.Exists(command=>command.Contains("INSPECTING:"))&&
            GUI.commands.Exists(command=>command.Contains(expected.title)),
            "The actual map labels the inspected field quest rather than only the old tracked quest");
        Check(game.save.trackedQuest=="main"&&
            game.save.coins==coins&&game.save.flags.Count==flagCount,
            "Inspecting the independent quest marker neither awards resources nor changes v2 quest flags");
        Call(app,"OpenMenu","map");
        Check(typeof(FrontierRuntime).GetField(
            "mapInspectedQuestId",Private).GetValue(app)==null,
            "A normal map open cannot inherit a stale journal-only inspected target");
        game.Close();
        Console.WriteLine("PASS Journal Show destination maps selected physical quest independently of tracked objective");
    }

    private static void LiveJourneyJournalExplainsAndTracksRealStory(FrontierRuntime app)
    {
        Prepare(app);
        var game=app.Game;
        game.save.english=true;
        game.save.stage=3;
        game.save.ending=1;
        game.save.trackedQuest="main";
        game.mode="journal";
        Set(app,"journalTab",3);
        Set(app,"journeyShowRecap",true);
        Set(app,"journeyRecapIndex",-1);
        GUI.commands.Clear();GUI.buttonCalls.Clear();
        Event.current.type=EventType.Repaint;
        Call(app,"OnGUI");
        Check(GUI.commands.Exists(x=>x.Contains("Chapter Five")) &&
            GUI.commands.Exists(x=>x.Contains("After the core decision")) &&
            GUI.commands.Exists(x=>x.Contains("Next:")),
            "The actual Journey screen explains the unlocked historical cause and a live next step");
        Check(!GUI.commands.Exists(x=>x.Contains("Chapter Eleven")),
            "A new postwar save never exposes future chapter names on the Journey screen");
        var track=GUI.buttonCalls.Find(x=>Math.Abs(x.rect.x-480)<.01f &&
            Math.Abs(x.rect.y-652)<.01f);
        Check(track!=null&&track.enabled,
            "The actual story recap provides an enabled Track button for an unlocked live chapter");
        GUI.queuedClick=new Rect(480,652,210,30);
        Call(app,"OnGUI");
        Check(!GUI.queuedClick.HasValue &&
            game.save.trackedQuest=="afterlight",
            "Clicking real Story recap Track routes into the existing quest compass");
        int coins=game.save.coins,flags=game.save.flags.Count;
        GUI.queuedClick=new Rect(297,652,83,30);
        Call(app,"OnGUI");
        GUI.commands.Clear();GUI.buttonCalls.Clear();Call(app,"OnGUI");
        Check(GUI.commands.Exists(x=>x.Contains("Chapter Four")) &&
            game.save.trackedQuest=="afterlight",
            "Reading a completed chapter moves back through real history without changing tracking");
        track=GUI.buttonCalls.Find(x=>Math.Abs(x.rect.x-480)<.01f &&
            Math.Abs(x.rect.y-652)<.01f);
        Check(track!=null&&!track.enabled,
            "Completed historical chapters cannot restart old rewards via a disabled Track button");
        GUI.queuedClick=new Rect(497,285,193,33);
        Call(app,"OnGUI");
        GUI.commands.Clear();Call(app,"OnGUI");
        Check(GUI.commands.Exists(x=>x.Contains("Changes you made")),
            "Switching tabs reveals the preserved original world-response report");
        GUI.queuedClick=new Rect(297,285,189,33);
        Call(app,"OnGUI");
        GUI.commands.Clear();Call(app,"OnGUI");
        Check(GUI.commands.Exists(x=>x.Contains("Chapter Four")) &&
            game.save.coins==coins && game.save.flags.Count==flags,
            "Returning to the dynamic story reader leaves prior progress and inventory untouched");
        Console.WriteLine("PASS Live Journey story recap, spoiler gate, historical pages and physical Track/UI toggle");
    }


    private static void LiveFishingBiteClockAndHudPreventInfiniteWaiting(FrontierRuntime app)
    {
        Prepare(app);
        var g=app.Game;g.Enter(0);g.enemies.Clear();
        WorldObject pond=Array.Find(g.Map.objects,o=>o.kind=="fish");
        Check(pond!=null,"The real Emberfall lake owns the timed fishing encounter");
        g.save.x=pond.x;g.save.y=pond.y;g.Talk(pond);
        int cast=Array.FindIndex(g.conversation.choices,c=>c.action=="fish");
        Check(cast>=0,"Real lake interaction opens the fishing button");
        g.Choose(cast);
        int pulses=0;
        while(!g.FishLineReady&&pulses++<80)Update(app);
        Check(g.FishLineReady && g.FishBiteTimeRemaining>2.5f,
            "A real keyboard/controller fishing window opens for a finite playable interval");
        GUI.commands.Clear();Call(app,"OnGUI");
        Check(GUI.commands.Exists(x=>x.Contains("鱼口剩余")||
            x.Contains("BITE TIME")),
            "The production IMGUI paints the same countdown the GameCore enforces");
        float initial=g.FishBiteTimeRemaining;
        for(int i=0;i<8;i++)Update(app);
        Check(g.FishBiteTimeRemaining<initial-.1f &&
            g.fishHits==0&&g.fishMisses==0,
            "The active lake timer drains without automatically charging an early miss");
        for(int lost=1;lost<=3;lost++)
        {
            int safety=0;
            while(g.fishMisses<lost && g.mode=="fish" && safety++<240)
                Update(app);
            Check(g.fishMisses==lost&&g.fishHits==0,
                "Waiting too long costs exactly one genuine unanswered bite");
            if(lost<3)Check(g.mode=="fish"&&!g.FishLineReady,
                "After a time-out the physical button waits for a new bite");
        }
        Check(g.mode=="world"&&g.fishHits==0,
            "Three genuinely unanswered bites return from the minigame to the live world");
        Console.WriteLine("PASS Real fishing bite countdown, IMGUI and unattended miss limits");
    }

    private static void FishingSuspendsOnFocusLossAndResumesOnReturn(FrontierRuntime app)
    {
        Prepare(app);
        var game = app.Game;
        game.Enter(0);game.enemies.Clear();
        WorldObject pond=Array.Find(game.Map.objects,o=>o.kind=="fish");
        Check(pond!=null,"Original pond exists for real fishing focus continuity");
        game.save.x=pond.x;game.save.y=pond.y;
        game.Talk(pond);
        int cast=Array.FindIndex(game.conversation.choices,c=>c.action=="fish");
        Check(cast>=0,"Real fishing conversation offers a cast");
        game.Choose(cast);
        Check(game.mode=="fish","Focus test begins with real bite expiry initialization");
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
        Check(GUI.textureCalls.Exists(x=>x.texture.name==
            "resource:Frontier/Art/Monsters")&&
            !GUI.textureCalls.Exists(x=>x.texture.name==key),
            "Normal enemy should retain illustrated Monsters.png without crude overlay");
        normal.state=0;normal.releaseTime=.15f;
        GUI.textureCalls.Clear();Call(app,"DrawEnemyActor",normal);
        Check(GUI.textureCalls.Exists(x=>x.texture.name==
            "resource:Frontier/Art/Monsters")&&
            !GUI.textureCalls.Exists(x=>x.texture.name==key),
            "Enemy contact must keep original detailed combat painting");
        var boss=new Enemy(9892,2,player.Position.x+1,player.Position.y,400,true){
            state=1,timer=.6f,windupDuration=1.1f
        };
        GUI.textureCalls.Clear();Call(app,"DrawEnemyActor",boss);
        Check(GUI.textureCalls.Exists(x=>x.texture.name==
            "resource:Frontier/Art/Bosses")&&
            !GUI.textureCalls.Exists(x=>x.texture.name==key),
            "Boss windup should keep detailed original boss body and combat tell");
        Console.WriteLine("PASS full-body released spell/sword recovery with class-owned pixels, canonical blonde head and boss/enemy cues");
    }

    private static void HeroActionAccentsFollowRealDirectionalWeaponMount(FrontierRuntime app)
    {
        // Rough HeroMotionAccents overpaint is removed; GameCore keeps all
        // real E/W/N/S Q skills, actual weapon origin and class-owned body.
        int checks=0;
        foreach(int vocation in new[]{0,1,2})
        foreach(Point facing in ActorDirections)
        {
            Prepare(app,vocation);
            var g=app.Game;
            g.Skill(facing);
            Check(g.playerAction!=null&&g.playerAction.kind=="skill",
                "Real Q must still trigger authoritative combat");
            GUI.textureCalls.Clear();Call(app,"DrawPlayerActor");
            string cls=new[]{"Vanguard","Ranger","Arcanist"}[vocation];
            Check(GUI.textureCalls.Exists(t=>t.texture.name.StartsWith(
                "resource:Frontier/HeroAnimator/Canonical/Hero_"+cls+"_Cast_")),
                "Q must retain the canonical complete-body Cast Sprite");
            Check(!GUI.textureCalls.Exists(t=>
                t.texture.name.Contains("HeroMotionAccents")||
                t.texture.name.Contains("FullBodyGait_")),
                "Low-resolution overlays or synthetic leg layers must not be drawn");
            Point origin=GameCore.WeaponOrigin(
                g.Position,g.AppearanceWeapon,g.VisualFacing);
            Check(!double.IsNaN(origin.x)&&!double.IsNaN(origin.y),
                "Core weapon attachment must remain valid across four directions");
            checks++;
        }
        Console.WriteLine("PASS "+checks+
            " four-direction full-body Q actions without unapproved 32px effect overpaint");
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
        // The original detailed actor Sprite atlases already carry grounded
        // feet. Removing coarse contact ovals cannot remove an actual body.
        Prepare(app,0);
        var g=app.Game;g.enemies.Clear();g.save.companion=-1;
        const string patch="resource:Frontier/Art/ActorContactShadows";
        GUI.textureCalls.Clear();Call(app,"DrawPlayerActor");
        Check(GUI.textureCalls.Exists(t=>t.texture.name.StartsWith(
            "resource:Frontier/HeroAnimator/Canonical/Hero_Vanguard_"))&&
            !GUI.textureCalls.Exists(t=>t.texture.name==patch),
            "One whole blonde protagonist and no coarse foot shadow");
        GUI.textureCalls.Clear();Call(app,"DrawEnemyActor",
            new Enemy(97211,1,g.Position.x+2,g.Position.y,100));
        Check(GUI.textureCalls.Exists(t=>t.texture.name==
            "resource:Frontier/Art/Monsters")&&
            !GUI.textureCalls.Exists(t=>t.texture.name==patch),
            "Original illustrated monster must keep its own feet");
        GUI.textureCalls.Clear();Call(app,"DrawEnemyActor",
            new Enemy(97212,0,g.Position.x+3,g.Position.y,300,true));
        Check(GUI.textureCalls.Exists(t=>t.texture.name==
            "resource:Frontier/Art/Bosses")&&
            !GUI.textureCalls.Exists(t=>t.texture.name==patch),
            "Original detailed boss must keep its rooted Sprite");
        GUI.textureCalls.Clear();Call(app,"DrawNpc","forge",g.Position,-1);
        Check(GUI.textureCalls.Exists(t=>t.texture.name==
            "resource:Frontier/Art/Npcs"),
            "Original detailed guild/forge citizens must remain live");
        g.save.companion=0;GUI.textureCalls.Clear();
        Call(app,"DrawCompanionActor");
        Check(GUI.textureCalls.Exists(t=>
            t.texture.name=="resource:Frontier/Art/CompanionMira"||
            t.texture.name=="resource:Frontier/Art/WalkMira"),
            "Detailed accepted Mira body must remain visible whether idle or walking");
        Console.WriteLine("PASS original detailed hero, monster, Boss, NPC, companion roots with no coarse contact overlay");
    }

    private static void OriginalAbilityAndStewIconsUsePublishedArt(FrontierRuntime app)
    {
        Prepare(app,0);
        string[] icons={"sword","bow","staff","stew","tea"};
        int[] slots={0,1,2,10,15};
        for(int k=0;k<icons.Length;k++)
        {
            GUI.textureCalls.Clear();
            Call(app,"Icon",icons[k],80f,90f,42f,(Color?)null);
            var painted=GUI.textureCalls.FindLast(t=>
                t.texture.name=="resource:Frontier/Art/Icons");
            Check(painted!=null,
                "Q sword/bow/staff, F stew and tea must use original illustrated Icons.png");
            int n=slots[k];
            Check(Math.Abs(painted.uv.x-(n%4)/4f)<.0001f&&
                Math.Abs(painted.uv.y-(3-n/4)/4f)<.0001f,
                "Q/F icon atlas indexing selected wrong art");
            Check(!GUI.textureCalls.Exists(t=>
                t.texture.name.StartsWith("icon_")),
                "Icon regressed into low-detail procedural fallback");
        }
        // Simulate import having failed during Awake but succeeding while the
        // same live scene is drawing its action bar. The runtime must restore
        // the actual 4x4 source atlas, never permanently show text/dot art.
        Set(app,"iconSheet",null);
        Set(app,"publishedVisualRetryAt",0f);
        GUI.textureCalls.Clear();
        Call(app,"Icon","stew",80f,90f,42f,(Color?)null);
        Check(GUI.textureCalls.Exists(t=>
            t.texture.name=="resource:Frontier/Art/Icons"),
            "Late-imported detailed stew icon did not recover without restart");
        Set(app,"effectSheet",null);
        Set(app,"publishedVisualRetryAt",0f);
        GUI.textureCalls.Clear();
        var strike = new Effect("slash",app.Game.Position,1f);
        var authored=(bool)Call(app,"DrawAuthoredEffect",strike);
        Check(authored && GUI.textureCalls.Exists(t=>
            t.texture.name=="resource:Frontier/Art/Effects"),
            "Late-imported illustrated attack FX failed to recover in the same scene");
        // A loot pickup must remain VISIBLE even while Icons.png is missing.
        // Exhaust the retry budget to emulate a temporarily unavailable PNG.
        Set(app,"iconSheet",null);
        Set(app,"publishedVisualRetryAt",999999f);
        GUI.textureCalls.Clear();
        var loot = new Effect("loot",app.Game.Position,1f){variant=1};
        bool fallback=(bool)Call(app,"DrawAuthoredEffect",loot);
        Check(fallback&&GUI.textureCalls.Exists(t=>
            t.texture.name=="resource:Frontier/Art/Effects"),
            "Pickup effects disappeared when the detailed icon atlas was unavailable");
        Set(app,"publishedVisualRetryAt",0f);
        GUI.textureCalls.Clear();
        bool restored=(bool)Call(app,"DrawAuthoredEffect",loot);
        Check(restored&&GUI.textureCalls.Exists(t=>
            t.texture.name=="resource:Frontier/Art/Icons"),
            "Pickup never recovered its original detailed resource icon");
        // Inverse import order: original item icons are ALREADY loaded but
        // Effects.png is not. Loot must still use Icons.png, not a dull ring.
        Set(app,"effectSheet",null);
        Set(app,"publishedVisualRetryAt",999999f);
        GUI.textureCalls.Clear();
        bool independent=(bool)Call(app,"DrawAuthoredEffect",loot);
        Check(independent&&GUI.textureCalls.Exists(t=>
            t.texture.name=="resource:Frontier/Art/Icons"),
            "An unavailable FX sheet incorrectly suppressed the valid loot icon");
        // Finally recover the authored projectile/attack sheet in-session.
        Set(app,"publishedVisualRetryAt",0f);
        GUI.textureCalls.Clear();
        bool attackRestored=(bool)Call(app,"DrawAuthoredEffect",strike);
        Check(attackRestored&&GUI.textureCalls.Exists(t=>
            t.texture.name=="resource:Frontier/Art/Effects"),
            "Authored slash must recover after late Effects.png import");
        // The exact same import timing bug could turn intricate WorldProps
        // lamps and signs into the old tiny synthetic PixelArt.Prop images.
        Set(app,"worldProps",null);
        Set(app,"publishedVisualRetryAt",0f);
        GUI.textureCalls.Clear();
        Call(app,"PropImage","lamp",0,60f,70f,70f,92f,(Color?)null);
        Check(GUI.textureCalls.Exists(t=>t.texture.name==
            "resource:Frontier/Art/WorldProps")&&
            !GUI.textureCalls.Exists(t=>t.texture.name.StartsWith("prop_")),
            "A restored WorldProps.png did not bring back the detailed lamp");

        // Terrain, road/river variants and FieldProps all share the scene
        // import lifecycle. Confirm they recover in the actual DrawWorld
        // entrypoint instead of showing empty tiles until the next restart.
        Prepare(app,0);
        foreach(string field in new[]{"terrainSheet","terrainVariants",
            "detailSheet","fieldProps","worldProps"})
            Set(app,field,null);
        Set(app,"publishedVisualRetryAt",0f);
        GUI.textureCalls.Clear();
        Call(app,"DrawWorld");
        foreach(string field in new[]{"terrainSheet","terrainVariants",
            "detailSheet","fieldProps","worldProps"})
            Check(typeof(FrontierRuntime).GetField(field,Private)
                .GetValue(app)!=null,
                "Detailed original world art did not recover in DrawWorld: "+field);
        Console.WriteLine("PASS authored combat, skill/stew, loot, lamps, signs, roads and river sprites recover after delayed PNG import");
    }

    private static void OriginalCombatVfxTransitionsUseActualPaintedFrames(FrontierRuntime app)
    {
        Prepare(app,0);
        var atlas=(Texture2D)typeof(FrontierRuntime).GetField(
            "effectSheet",Private).GetValue(app);
        Check(atlas!=null,"Original painted Effects.png is absent");
        int reviewed=0;
        foreach(string kind in new[]{"slash","impact","hurt","projectileImpact"})
        {
            var effect=new Effect(kind,app.Game.Position,1f){
                direction=new Point(1,0)};
            GUI.textureCalls.Clear();
            effect.life=.92f; // first authored anticipation/contact painting
            bool early=(bool)Call(app,"DrawAuthoredEffect",effect);
            var before=GUI.textureCalls.Find(t=>ReferenceEquals(t.texture,atlas));
            Check(early&&before!=null,
                "Accepted hand-painted attack FX must draw at windup onset");
            int earlyCell=3,lateCell=kind=="projectileImpact"?4:2;
            if(kind=="slash"){earlyCell=0;lateCell=1;}
            Func<GUI.TextureCall,int,bool> matches=(call,index)=>
                Math.Abs(call.uv.x-(index%4)*.25f)<.0001f&&
                Math.Abs(call.uv.y-(3-index/4)*.25f)<.0001f;
            Check(matches(before,earlyCell),
                "First impact key must sample actual opening Effects.png cell: "+kind);
            GUI.textureCalls.Clear();
            effect.life=.39f; // the original artwork follows through
            bool late=(bool)Call(app,"DrawAuthoredEffect",effect);
            var after=GUI.textureCalls.Find(t=>ReferenceEquals(t.texture,atlas));
            Check(late&&after!=null&&matches(after,lateCell),
                "Second impact key must sample original follow-through painting: "+kind);
            Check(Math.Abs((before.rect.x+before.rect.width*.5f)-
                 (after.rect.x+after.rect.width*.5f))<.0001f&&
                Math.Abs((before.rect.y+before.rect.height*.5f)-
                 (after.rect.y+after.rect.height*.5f))<.0001f,
                "Visual swing/contact center must not teleport between original frames");
            Check(after.rect.width>=before.rect.width&&
                after.rect.width<=before.rect.width*1.5f,
                "Painted impact follow-through may grow slightly but cannot jump in scale");
            reviewed++;
        }
        Console.WriteLine("PASS "+reviewed+
            " actual sword/impact/projectile effect lifetimes switch between authored Effects.png paintings without root drift");
    }

    private static void AtomicHeroContactImportRecovery(FrontierRuntime app)
    {
        Prepare(app,0);
        var installed=Resources.TextureOverrides;
        var cache=(System.Collections.Generic.Dictionary<string,Texture2D>)
            typeof(FrontierRuntime).GetField(
                "fullBodyHeroSheetCache",Private).GetValue(app);
        var actions=new System.Collections.Generic.List<string>();
        const string root="Frontier/HeroAnimator/Canonical/";
        foreach(string profession in new[]{"Vanguard","Ranger","Arcanist"})
        foreach(string action in new[]{"Attack","Cast"})
        foreach(string face in new[]{"E","N","S"})
            actions.Add("Hero_"+profession+"_"+action+"_"+face);
        try
        {
            installed.Clear();cache.Clear();
            Set(app,"heroContactFamilyChecked",false);
            Set(app,"clock",1f);
            Check(!(bool)Call(app,"HasCompleteHeroContactArt"),
                "Existing six-frame hero art cannot masquerade as new contacts");
            for(int i=0;i<17;i++)
                installed[root+actions[i]]=new Texture2D(7*256,256,
                    TextureFormat.RGBA32,false){name="staged:"+actions[i]};
            Set(app,"clock",4f);
            Check(!(bool)Call(app,"HasCompleteHeroContactArt"),
                "17-of-18 import must not switch any class to seven frames");
            // A live scene may still hold the older six-cell texture.
            // Completing the final import must remove stale entries.
            cache[actions[0]]=new Texture2D(6*256,256,
                TextureFormat.RGBA32,false){name="stale-six-cell"};
            installed[root+actions[17]]=new Texture2D(7*256,256,
                TextureFormat.RGBA32,false){name="staged:"+actions[17]};
            Set(app,"clock",7f);
            Check((bool)Call(app,"HasCompleteHeroContactArt"),
                "Complete contact family never became live after Unity reimport");
            Check(!cache.ContainsKey(actions[0]),
                "Seven-frame switch reused stale six-cell cached art");
            app.Game.SetAim(new Point(1,0));
            app.Game.Attack(new Point(1,0));
            GUI.textureCalls.Clear();Call(app,"DrawPlayerActor");
            Check(GUI.textureCalls.Exists(t=>t.texture.name==
                    "staged:Hero_Vanguard_Attack_E"),
                "Actual player draw did not switch to fully imported action art");
            installed.Remove(root+actions[17]);
            Set(app,"clock",10f);
            Check((bool)Call(app,"HasCompleteHeroContactArt")&&
                app.Game.playerAction!=null,
                "A real in-flight Attack must retain its entire seven-cell body even when a file vanishes mid-pose");
            Check(cache.ContainsKey(actions[0])&&
                (bool)Call(app,"HasCompleteHeroContactArt"),
                "Repeated IMGUI calls must not tear down the in-flight imported hero Sprite");
            app.Game.playerAction=null;
            Set(app,"clock",10.2f);
            Check(!(bool)Call(app,"HasCompleteHeroContactArt"),
                "After the real action ends, incomplete art must roll the entire family back to six frames");
            Check(!cache.ContainsKey(actions[0]),
                "Rollback must invalidate every retired seven-frame render cache");
            app.Game.attackCooldown=0f;
            app.Game.Attack(new Point(1f,0f));
            Check(app.Game.playerAction!=null,
                "The next actual hero Attack starts on the already published six-frame family");
            for(int i=0;i<18;i++)
                installed[root+actions[i]]=new Texture2D(7*256,256,
                    TextureFormat.RGBA32,false){name="staged-again:"+actions[i]};
            Set(app,"clock",13f);
            Check(!(bool)Call(app,"HasCompleteHeroContactArt"),
                "Completing all 18 imports cannot swap the middle of a real six-frame attack");
            app.Game.playerAction=null;
            Set(app,"clock",13.15f);
            Check((bool)Call(app,"HasCompleteHeroContactArt"),
                "First safe idle boundary publishes all 18 new seven-frame whole bodies together");
            Console.WriteLine(
                "PASS 18-contact all-or-none hot-import, real game draw and safe reimport rollback");
        }
        finally
        {
            installed.Clear();cache.Clear();
            Set(app,"heroContactFamilyChecked",false);
            Set(app,"heroContactFamilyComplete",false);
            Set(app,"heroContactLastCheckedAt",float.NegativeInfinity);
            app.EndVisualReview();
        }
    }

    private static void RejectedRegionLoadReturnsToRealInn(FrontierRuntime app)
    {
        Campaign world = CampaignFixture.Create();
        var game = new GameCore(world);
        game.NewGame(0); game.Close();
        // A new hero cannot enter the final citadel, even if legacy save
        // coordinates happen to be legal floor tiles in the home town.
        SaveData slot = game.save;
        slot.room = 4; slot.x = 12; slot.y = 14; slot.coins = 73;
        slot.snapshotRoom = 4; slot.worldEnemies = new EnemyRecord[] { new EnemyRecord() };
        game.Load(slot);
        Check(game.save.room == 0 &&
            Math.Abs(game.save.x - world.maps[0].spawnX) < .0001f &&
            Math.Abs(game.save.y - world.maps[0].spawnY) < .0001f,
            "Locked-region save must respawn at the real inn, not an old world coordinate");
        Check(game.save.snapshotRoom == -1 && game.save.worldEnemies == null &&
            game.save.coins == 73,
            "Rejected region must retire stale enemy snapshot but preserve economy");
        // A corrupt region index previously collapsed to town before the
        // locked-room check, retaining the same invalid source coordinates.
        slot = game.save;
        slot.room = int.MaxValue; slot.x = 12; slot.y = 14;
        slot.snapshotRoom = 4; slot.worldEnemies = new EnemyRecord[] { new EnemyRecord() };
        game.Load(slot);
        Check(game.save.room == 0 &&
            Math.Abs(game.save.x - world.maps[0].spawnX) < .0001f &&
            Math.Abs(game.save.y - world.maps[0].spawnY) < .0001f &&
            game.save.snapshotRoom == -1 && game.save.worldEnemies == null,
            "Out-of-range region must be treated as a rejected placement too");
        // Legal saves must not be forced back to their spawn each load.
        slot = game.save;
        slot.room = 0; slot.x = 12; slot.y = 14;
        game.Load(slot);
        Check(game.save.room == 0 &&
            Math.Abs(game.save.x - 12) < .0001f &&
            Math.Abs(game.save.y - 14) < .0001f,
            "Valid walkable town saves must retain their exact player position");
        Console.WriteLine("PASS locked/out-of-range saved region returns to inn without stale enemies or lost items");
    }

    private static void LiveYKeyOrdersPhysicalCompanionWatchAndRestoresFollow(FrontierRuntime app)
    {
        Prepare(app,1);
        var g=app.Game;g.mode="world";
        g.save.room=0;g.enemies.Clear();g.projectiles.Clear();
        var post=Array.Find(g.Map.objects,o=>o.kind=="outerwatch");
        Check(post!=null,"Y key uses a real authored relief watch");
        g.save.x=post.x;g.save.y=post.y;
        g.save.flags.Add("outer_cache_0");
        g.save.flags.Add("outer_patrol_done_0");
        g.save.companion=1;
        g.companionPosition=new Point(post.x+1.1f,post.y+1.1f);
        Check(g.CanStand(g.companionPosition),
            "The physical ranger has a legal start foot");
        g.Talk(post);
        int start=Array.FindIndex(g.conversation.choices,
            c=>c.action=="outer_watch_veteran");
        Check(start>=0,"A real veteran siege is available at this post");
        g.Choose(start);
        Point allyBefore=g.companionPosition;
        Input.keys.Add(KeyCode.Y);Update(app);
        Check(g.OuterPatrolCompanionGuarding&&
            g.CanStand(g.OuterPatrolCompanionGuardPosition)&&
            (g.companionPosition-allyBefore).Length<1f,
            "Pressing Y places a tactical guard waypoint without warping ally");
        Point guardFoot=g.OuterPatrolCompanionGuardPosition;
        g.save.x=post.x+6f;g.save.y=post.y;
        Point beforeRecall=g.companionPosition;
        Input.keys.Add(KeyCode.Y);Update(app);
        Check(!g.OuterPatrolCompanionGuarding&&
            (g.companionPosition-beforeRecall).Length<1f,
            "Defender recalls from the live perimeter without teleport");
        Input.keys.Add(KeyCode.Y);Update(app);
        Check(!g.OuterPatrolCompanionGuarding,
            "Remote keyboard Y cannot PLACE a new outpost guard");
        g.save.x=post.x;g.save.y=post.y;
        Input.keys.Add(KeyCode.Y);Update(app);
        Check(g.OuterPatrolCompanionGuarding&&
            g.CanStand(g.OuterPatrolCompanionGuardPosition),
            "Local Y can physically reassign a reachable outpost guard");
        g.Talk(post);
        Input.keys.Add(KeyCode.Y);Update(app);
        Check(g.OuterPatrolCompanionGuarding,
            "Keyboard Y cannot bypass a paused dialogue input context");
        g.Close();
        g.Talk(post);
        int abort=Array.FindIndex(g.conversation.choices,
            c=>c.action=="outer_watch_abort");
        Check(abort>=0,"Original withdrawal remains reachable");
        g.Choose(abort);
        Check(!g.OuterPatrolActive&&!g.OuterPatrolCompanionGuarding,
            "Original withdrawal clears real keyboard tactical state");
        Console.WriteLine("PASS actual Y keyboard companion guard/follow uses walkable stock waypoint");
    }

    private static void LivePKeyHoldsAndReleasesPhysicalCompanion(FrontierRuntime app)
    {
        Prepare(app,1);
        var g=app.Game;
        g.Enter(0);g.mode="world";g.enemies.Clear();g.projectiles.Clear();
        g.save.flags.Add("companion_0");
        g.SelectCompanion(0);
        Check(g.save.companion==0&&g.CanStand(g.companionPosition),
            "The P input test begins with a real selected companion on valid terrain");

        Input.keys.Add(KeyCode.P);Update(app);
        Check(g.CompanionHoldingPosition,
            "The production FrontierRuntime.Update maps keyboard P to a physical hold order");
        g.save.english=true;
        GUI.commands.Clear();Call(app,"OnGUI");
        Check(GUI.commands.Exists(command=>command.Contains("HOLD")),
            "The actual production world renderer labels the hold anchor");
        Input.keys.Add(KeyCode.P);Update(app);
        Check(!g.CompanionHoldingPosition,
            "A second P input resumes following through the production input path");
        GUI.commands.Clear();Call(app,"OnGUI");
        Check(!GUI.commands.Exists(command=>command.Contains("HOLD")),
            "The hold marker disappears after following resumes");

        g.mode="dialog";
        Input.keys.Add(KeyCode.P);Update(app);
        Check(!g.CompanionHoldingPosition,
            "P cannot issue a hold order through a paused dialogue input context");
        Console.WriteLine("PASS P hold/follow input, world anchor marker and dialogue gating in production runtime");
    }

    private static void LiveRBButtonTapAndHoldCompanionOrders(FrontierRuntime app)
    {
        KeyCode rb=KeyCode.JoystickButton5;
        Prepare(app,2);
        var g=app.Game;g.Enter(0);g.mode="world";g.enemies.Clear();g.projectiles.Clear();
        g.save.flags.Add("companion_2");g.SelectCompanion(2);
        Check(g.save.companion==2,"RB gesture test selects a real unlocked companion");
        Input.held.Add(rb);Input.keys.Add(rb);Update(app);
        Check(g.companionAction==null&&g.companionCooldown<=0,
            "RB press waits for tap/hold classification rather than dispatching immediately");
        Input.held.Remove(rb);Update(app);
        Check(g.companionAction!=null&&g.companionAction.kind=="command"&&
            g.companionCooldown>17.8f,
            "A short RB tap dispatches exactly one actual companion command");

        Prepare(app,2);
        g=app.Game;g.Enter(0);g.mode="world";g.enemies.Clear();g.projectiles.Clear();
        g.save.flags.Add("companion_2");g.SelectCompanion(2);
        Input.held.Add(rb);Input.keys.Add(rb);Update(app);
        for(int i=0;i<24;i++)Update(app);
        Check(g.CompanionHoldingPosition&&g.companionAction==null&&
            g.companionCooldown<=0,
            "Holding RB toggles the real physical anchor without spending a command cooldown");
        Input.held.Remove(rb);Update(app);
        Check(g.CompanionHoldingPosition&&g.companionCooldown<=0,
            "Releasing a hold gesture cannot also dispatch the tap command");
        g.ToggleCompanionHold();
        Check(!g.CompanionHoldingPosition,"The test returns the companion to normal follow before context gating");
        g.mode="companions";
        Input.held.Add(rb);Input.keys.Add(rb);Update(app);
        for(int i=0;i<24;i++)Update(app);
        Check(!g.CompanionHoldingPosition&&g.companionCooldown<=0&&
            g.mode!="world",
            "RB held in a menu remains menu navigation and cannot toggle the world hold order");
        Input.held.Remove(rb);Update(app);
        g.mode="dialog";
        Input.held.Add(rb);Input.keys.Add(rb);Update(app);
        for(int i=0;i<24;i++)Update(app);
        Check(!g.CompanionHoldingPosition&&g.companionCooldown<=0,
            "A held RB gesture outside world mode cannot issue a companion order");
        Input.held.Remove(rb);Update(app);
        Console.WriteLine("PASS RB tap command, long-press physical hold/follow and dialogue cancellation in production runtime");
    }

    private static void LiveXKeyPatchCannotRepairRemoteOrTwice(FrontierRuntime app)
    {
        Prepare(app,0);
        var g=app.Game;g.mode="world";
        g.save.room=0;g.enemies.Clear();g.projectiles.Clear();
        var post=Array.Find(g.Map.objects,o=>o.kind=="outerwatch");
        Check(post!=null,"Real regional post exists in shipped game");
        g.save.x=post.x;g.save.y=post.y;
        g.save.flags.Add("outer_cache_0");
        g.save.inventory[0]=5;g.save.inventory[2]=5;
        g.Talk(post);
        int start=Array.FindIndex(g.conversation.choices,
            c=>c.action=="outer_watch_start");
        Check(start>=0,"Unwon real cache challenge offers genuine defense");
        g.Choose(start);
        Check(g.OuterPatrolRaidersRemaining==2&&
            !g.OuterPatrolPatchAvailable,"Full stock cannot be patched");

        Point retreat=new Point();bool found=false;
        for(int dx=-10;dx<=10&&!found;dx++)
        for(int dy=-10;dy<=10&&!found;dy++)
        {
            Point p=new Point(post.x+dx,post.y+dy);
            if(!g.CanStand(p)||
                (p-new Point(post.x,post.y)).Length>10f||
                (p-new Point(post.x-2f,post.y)).Length<5f)continue;
            retreat=p;found=true;
        }
        Check(found,"Shipped map provides a real safe observation foot");
        g.save.x=retreat.x;g.save.y=retreat.y;
        for(int i=0;i<1000&&g.OuterPatrolActive&&
                g.OuterPatrolIntegrity==100f;i++)
            g.Tick(.02f,new Point());
        Check(g.OuterPatrolActive&&g.OuterPatrolIntegrity<100f&&
            g.OuterPatrolPatchAvailable,
            "Real timed raider attacks eventually damage physical stores");

        float damaged=g.OuterPatrolIntegrity;
        Input.keys.Add(KeyCode.X);Update(app);
        Check(g.save.inventory[0]==5&&g.save.inventory[2]==5,
            "X cannot buy an emergency repair from a distant location");
        g.save.x=post.x;g.save.y=post.y;
        Input.keys.Add(KeyCode.X);Update(app);
        Check(g.OuterPatrolIntegrity>damaged&&
            g.save.inventory[0]==4&&g.save.inventory[2]==4,
            "Real runtime X input repairs damaged cache with one real timber and herb");
        Input.keys.Add(KeyCode.X);Update(app);
        Check(g.save.inventory[0]==4&&g.save.inventory[2]==4&&
            !g.OuterPatrolPatchAvailable,
            "Repeated X key cannot duplicate one-use healing or spend resources");
        Console.WriteLine("PASS live X repair respects damage, physical range and one-use stock");
    }

    private static void LiveVKeyBarricadeUsesRealOutpostGuard(FrontierRuntime app)
    {
        Prepare(app,1);
        var g=app.Game;g.mode="world";
        g.save.room=0;g.enemies.Clear();g.projectiles.Clear();
        var post=Array.Find(g.Map.objects,o=>o.kind=="outerwatch");
        Check(post!=null,"Input test targets a real playable outer cache post");
        g.save.x=post.x;g.save.y=post.y;
        g.save.flags.Add("outer_cache_0");
        g.save.flags.Add("outer_patrol_done_0");
        g.save.inventory[0]=7;g.save.inventory[4]=3;
        g.Talk(post);
        int veteran=Array.FindIndex(g.conversation.choices,
            c=>c.action=="outer_watch_veteran");
        Check(veteran>=0,"A finished watchpost offers the live veteran siege");
        g.Choose(veteran);
        Check(g.OuterPatrolVeteranActive&&g.OuterPatrolBraceIntegrity==0f,
            "Real veteran fight starts with no prepaid barricade");

        g.save.x=post.x+6f;g.save.y=post.y;
        Input.keys.Add(KeyCode.V);Update(app);
        Check(g.OuterPatrolBraceIntegrity==0f&&
            g.save.inventory[0]==7&&g.save.inventory[4]==3,
            "Real keyboard V cannot build cover remotely from outside the post");

        g.save.x=post.x;g.save.y=post.y;
        Input.keys.Add(KeyCode.V);Update(app);
        Check(g.OuterPatrolBraceStanding&&
            g.OuterPatrolBraceIntegrity==48f&&
            g.save.inventory[0]==5&&g.save.inventory[4]==2,
            "Keyboard V directly buys and places one genuine 48-HP stock barrier");
        Input.keys.Add(KeyCode.V);Update(app);
        Check(g.OuterPatrolBraceIntegrity==48f&&
            g.save.inventory[0]==5&&g.save.inventory[4]==2,
            "Repeated keyboard activation cannot pay/build the same barricade twice");
        g.Talk(post);
        Input.keys.Add(KeyCode.V);Update(app);
        Check(g.save.inventory[0]==5&&g.save.inventory[4]==2,
            "Keyboard cannot bypass dialog-mode action restrictions");
        g.Close();
        Console.WriteLine("PASS keyboard V constructs only one nearby real veteran barricade");
    }

    private static void CivicAftermathChronicleReflectsLiveInfrastructure(FrontierRuntime app)
    {
        Prepare(app);
        GameCore g=app.Game;
        var originalFlags=new System.Collections.Generic.List<string>(g.save.flags);
        int originalCoins=g.save.coins,originalXp=g.save.xp,originalShards=g.save.shards;
        int originalSkillPoints=g.save.skillPoints;
        int[] originalInventory=(int[])g.save.inventory.Clone();
        string originalTrackedQuest=g.save.trackedQuest;
        try
        {
            g.save.flags.Clear();
            Check(!(bool)CallCore(g,"StoryUnlocked","civic_aftermath"),
                "Chapter Twelve stays locked until the real public grid is complete");
            CallCore(g,"WorldChronicleSection","after");
            Check(Array.FindIndex(g.conversation.choices,c=>c.action=="story_read_civic_aftermath")<0,
                "The locked epilogue is absent from the actual chronicle menu");

            g.save.flags.Add("civic_conduit_complete");
            Check((bool)CallCore(g,"StoryUnlocked","civic_aftermath"),
                "Completing all three physical grid valves unlocks the epilogue");
            CallCore(g,"WorldChronicleSection","after");
            int entry=Array.FindIndex(g.conversation.choices,c=>c.action=="story_read_civic_aftermath");
            Check(entry>=0,"The unlocked Chapter Twelve is selectable from the real chronicle");
            CallCore(g,"WorldChroniclePage","civic_aftermath");
            Check(g.conversation.speaker.Contains("第十二章")&&
                g.conversation.en.Contains("Chapter Twelve"),
                "The new page presents a bilingual Chapter Twelve title");
            Check(g.conversation.text.Contains("0/8")&&g.conversation.text.Contains("0/3")&&
                g.conversation.text.Contains("尚未签署"),
                "The page reports missing refuges and unsigned community choices instead of inventing completion");

            for(int room=0;room<8;room++)g.save.flags.Add("far_shelter_"+room);
            g.save.flags.Add("far_refuge_atlas");
            g.save.flags.Add("civic_supply_started");
            g.save.flags.Add("civic_supply_complete");
            g.save.flags.Add("civic_supply_clinic_0");
            g.save.flags.Add("civic_supply_watch_3");
            g.save.flags.Add("civic_supply_clinic_5");
            g.save.flags.Add("civic_response_ledger");
            g.save.flags.Add("civic_response_done_0");
            g.save.flags.Add("civic_response_done_3");
            g.save.flags.Add("civic_response_done_5");
            g.save.flags.Add("last_directive_open");
            g.save.flags.Add("last_directive_complete");
            g.save.flags.Add("afterlight_complete");
            for(int region=1;region<=3;region++)
            {
                g.save.flags.Add("afterlight_done_"+region);
                g.save.flags.Add("afterlight_steward_"+region);
            }
            var beforeRead=new System.Collections.Generic.List<string>(g.save.flags);
            int coins=g.save.coins,xp=g.save.xp,shards=g.save.shards,skillPoints=g.save.skillPoints;
            int[] inventory=(int[])g.save.inventory.Clone();
            string trackedQuest=g.save.trackedQuest;
            CallCore(g,"WorldChroniclePage","civic_aftermath");
            Check(g.conversation.text.Contains("8/8")&&g.conversation.text.Contains("3/3")&&
                g.conversation.text.Contains("医疗点")&&g.conversation.text.Contains("守路岗"),
                "The page reflects the actual refuge count and each region's distinct signed service");
            Check(g.conversation.textEn.Contains("8/8")&&g.conversation.textEn.Contains("3/3")&&
                g.conversation.textEn.Contains("clinic")&&g.conversation.textEn.Contains("road watch")&&
                g.conversation.textEn.Contains("Public oversight")&&
                g.conversation.textEn.Contains("named local stewards"),
                "The English page reflects the same public choices and lasting responsibilities");
            Check(string.Join("|",beforeRead)==string.Join("|",g.save.flags)&&
                coins==g.save.coins&&xp==g.save.xp&&shards==g.save.shards&&
                skillPoints==g.save.skillPoints&&trackedQuest==g.save.trackedQuest&&
                Array.TrueForAll(EnumerableIndices(inventory.Length),i=>inventory[i]==g.save.inventory[i]),
                "Reading the chronicle never mutates flags, inventory, rewards or tracked quest");
            Console.WriteLine("PASS Chapter Twelve chronicle unlock, bilingual live civic outcomes and read-only save behavior");
        }
        finally
        {
            g.save.flags.Clear();g.save.flags.AddRange(originalFlags);
            g.save.coins=originalCoins;g.save.xp=originalXp;g.save.shards=originalShards;
            g.save.skillPoints=originalSkillPoints;g.save.trackedQuest=originalTrackedQuest;
            Array.Copy(originalInventory,g.save.inventory,Math.Min(originalInventory.Length,g.save.inventory.Length));
        }
    }

    private static void ReadOriginalInnNews(GameCore game,WorldObject inn)
    {
        game.Talk(inn);
        Check(game.conversation!=null&&game.conversationSource==inn,
            "The original inn traveler is the current living speaker");
        int news=Array.FindIndex(game.conversation.choices,
            c=>c.action=="inn_news");
        Check(news>=0,"Real inn dialogue offers an optional historical news topic");
        game.Choose(news);
        Check(game.mode=="dialog"&&game.conversation!=null&&
            game.conversationSource==inn,
            "The current physical NPC retains the original historical dialogue");
    }

    private static void CivicAftercareRoadAuditIsTrackableAndOneTime(FrontierRuntime app)
    {
        Prepare(app);
        GameCore g=app.Game;
        var originalFlags=new System.Collections.Generic.List<string>(g.save.flags);
        var originalMoments=new System.Collections.Generic.List<JournalMoment>(g.save.moments);
        int originalRoom=g.save.room,originalCoins=g.save.coins,originalXp=g.save.xp;
        int originalShards=g.save.shards,originalLevel=g.save.level;
        int originalSkillPoints=g.save.skillPoints,originalTutorialBits=g.save.tutorialBits;
        float originalX=g.save.x,originalY=g.save.y;
        int[] originalInventory=(int[])g.save.inventory.Clone();
        string originalTrackedQuest=g.save.trackedQuest;
        bool originalFrontierFocus=g.save.frontierFocus;
        try
        {
            g.save.flags.Clear();
            g.save.room=9;
            WorldObject inn=Array.Find(g.Map.objects,o=>o.kind=="innGuest");
            Check(inn!=null,"Hearth Inn retains the original night traveler as quest giver");
            g.save.x=inn.x;g.save.y=inn.y;
            g.Talk(inn);
            Check(Array.FindIndex(g.conversation.choices,c=>c.action=="civic_aftercare_begin")<0,
                "The new road audit cannot start before the public grid is restored");

            g.save.flags.Add("civic_conduit_complete");
            ReadOriginalInnNews(g,inn);
            Check(g.conversation.text.Contains("旧路线图")&&
                g.conversation.textEn.Contains("old route chart")&&
                g.conversation.text.Contains("可选的三地巡路复核")&&
                g.conversation.textEn.Contains("optional three-region audit"),
                "The Inn introduces the optional field audit in both languages after the grid is restored");
            int begin=Array.FindIndex(g.conversation.choices,c=>c.action=="civic_aftercare_begin");
            Check(begin>=0,"The original night traveler offers the optional audit after grid completion");
            int coinsBeforeStart=g.save.coins,shardsBeforeStart=g.save.shards;
            g.Choose(begin);
            Check(g.save.flags.Contains("civic_aftercare_started")&&
                g.save.trackedQuest=="civic_aftercare"&&
                g.save.coins==coinsBeforeStart&&g.save.shards==shardsBeforeStart,
                "Accepting the audit persists and tracks the quest without granting its completion reward");
            QuestGuide guide=g.GetQuest("civic_aftercare");
            Check(!guide.locked&&guide.steps.Count==5&&guide.room==0,
                "The real journal guide targets the first unresolved region");

            int[] rooms={0,3,5};
            for(int i=0;i<rooms.Length;i++)
            {
                int room=rooms[i];
                g.save.room=room;
                WorldObject waypoint=Array.Find(g.Map.objects,o=>o.kind=="waytrail");
                Check(waypoint!=null,"Region "+room+" contains an authored route marker");
                g.save.x=waypoint.x;g.save.y=waypoint.y;
                g.Talk(waypoint);
                Check(g.save.flags.Contains("civic_aftercare_waytrail_"+room)&&
                    g.CivicAftercareWaypointCount==i+1,
                    "Physical interaction records exactly one field audit for region "+room);
                if(i==0)
                {
                    g.Talk(waypoint);
                    Check(g.CivicAftercareWaypointCount==1,
                        "Revisiting a recorded marker cannot double-count the region");
                }
                if(i==1)
                {
                    g.save.room=9;g.save.x=inn.x;g.save.y=inn.y;ReadOriginalInnNews(g,inn);
                    Check(g.conversation.text.Contains("2/3")&&
                        g.conversation.textEn.Contains("2/3"),
                        "The Inn's actual dialogue reports the saved two-of-three field audit progress");
                    Check(Array.FindIndex(g.conversation.choices,c=>c.action=="civic_aftercare_publish")<0&&
                        Array.FindIndex(g.conversation.choices,c=>c.action=="civic_aftercare_status")>=0,
                        "The Inn cannot publish a two-of-three audit");
                }
            }
            guide=g.GetQuest("civic_aftercare");
            Check(guide.room==9&&!guide.complete&&g.CivicAftercareWaypointCount==3,
                "All three verified markers move the live quest target back to Hearth Inn");
            g.save.room=9;g.save.x=inn.x;g.save.y=inn.y;g.Talk(inn);
            int publish=Array.FindIndex(g.conversation.choices,c=>c.action=="civic_aftercare_publish");
            Check(publish>=0,"The Inn offers publication only after all three field checks");
            int coinsBeforePublish=g.save.coins,shardsBeforePublish=g.save.shards;
            g.Choose(publish);
            Check(g.save.flags.Contains("civic_aftercare_published")&&
                g.GetQuest("civic_aftercare").complete&&
                g.save.coins==coinsBeforePublish+35&&g.save.shards==shardsBeforePublish+1,
                "Publishing settles the one-time reward and completes the persistent quest");
            int coinsAfterPublish=g.save.coins,shardsAfterPublish=g.save.shards;
            g.save.room=5;
            WorldObject auditedMarker=Array.Find(g.Map.objects,o=>o.kind=="waytrail");
            Check(auditedMarker!=null,"The third region keeps its original physical route marker after publication");
            g.save.x=auditedMarker.x;g.save.y=auditedMarker.y;
            g.Talk(auditedMarker);
            Check(g.conversation.text.Contains("公开记录")&&
                g.conversation.textEn.Contains("public record"),
                "A field marker continues to acknowledge its region's published audit after quest completion");
            g.save.room=9;g.save.x=inn.x;g.save.y=inn.y;
            ReadOriginalInnNews(g,inn);
            Check(g.conversation.text.Contains("公开记录")&&
                g.conversation.textEn.Contains("public record")&&
                Array.FindIndex(g.conversation.choices,c=>c.action=="civic_aftercare_status")>=0,
                "The Inn acknowledges the published field record and offers a review instead of a repeat reward");
            CallCore(g,"ChooseCivicAftercare","civic_aftercare_publish");
            Check(g.save.coins==coinsAfterPublish&&g.save.shards==shardsAfterPublish,
                "A stale second publication action cannot duplicate the reward");
            Console.WriteLine("PASS Chapter Twelve road audit uses real route markers, journal guidance and one-time publication");
        }
        finally
        {
            g.save.flags.Clear();g.save.flags.AddRange(originalFlags);
            g.save.moments.Clear();g.save.moments.AddRange(originalMoments);
            g.save.room=originalRoom;g.save.x=originalX;g.save.y=originalY;
            g.save.coins=originalCoins;g.save.xp=originalXp;g.save.shards=originalShards;
            g.save.level=originalLevel;g.save.skillPoints=originalSkillPoints;
            g.save.tutorialBits=originalTutorialBits;g.save.trackedQuest=originalTrackedQuest;
            g.save.frontierFocus=originalFrontierFocus;
            Array.Copy(originalInventory,g.save.inventory,Math.Min(originalInventory.Length,g.save.inventory.Length));
        }
    }






























    private static void RealWayfarerCoverIsVisibleInWorldAndJournal(FrontierRuntime app)
    {
        Prepare(app);
        var g=app.Game;
        g.save.stage=3;
        g.save.flags.Add("scout_saved");
        g.Enter(1);
        g.enemies.Clear();
        WorldObject npc=null;
        foreach(WorldObject o in g.Map.objects)
            if(o.kind=="wayfarer1") {npc=o;break;}
        Check(npc!=null,"The real Thornwood rescue actor exists");
        g.save.x=npc.x;g.save.y=npc.y;
        g.save.energy=70;
        g.save.english=true;
        g.Talk(npc);
        int start=Array.FindIndex(g.conversation.choices,
            c=>c.action=="wayfarer_defend_1");
        Check(start>=0,"Actual civilian dialogue offers its original ambush");
        g.Choose(start);
        Check(g.WayfarerEncounterRoom==1&&g.mode=="world",
            "A real two-raider rescue is running before a cover command");
        g.Talk(npc);
        Check(g.conversation!=null&&
            Array.Exists(g.conversation.choices,
                c=>c.action=="wayfarer_cover"),
            "The active rescue's actual NPC conversation offers live cover");
        GUI.commands.Clear();Call(app,"OnGUI");
        Check(GUI.commands.Exists(c=>c.Contains("Raider")||c.Contains("raider")),
            "Actual modal dialogue explains the raiders and protection mechanic");
        int index=Array.FindIndex(g.conversation.choices,
            c=>c.action=="wayfarer_cover");
        g.Choose(index);
        Check(g.WayfarerCoverTime==8f&&g.mode=="world"&&g.save.energy==50,
            "Real dialog action pays exactly 20 energy and returns to playable combat");
        Set(app,"camera",g.Position);
        GUI.commands.Clear();Call(app,"OnGUI");
        Check(GUI.commands.Exists(c=>c.Contains("COVER 8s")),
            "Shipped world presentation shows an actual 8-second cover aura over the protected traveler");
        var guide=g.GetQuest("wayfarers");
        Check(guide.next.Contains("Cover 8s"),
            "The live quest compass exposes the same true protection timer");
        g.save.english=false;
        GUI.commands.Clear();Call(app,"OnGUI");
        Check(GUI.commands.Exists(c=>c.Contains("掩护 8s")),
            "Chinese mode uses the same live covered NPC and legible remaining seconds");
        foreach(Enemy foe in g.enemies) {foe.stun=100f;foe.timer=100f;}
        g.mode="world";
        for(int i=0;i<163;i++)g.Tick(.05f,new Point());
        Check(g.WayfarerCoverTime<=.0001f&&g.WayfarerEncounterRoom==1,
            "The live combat clock expires the eight-second protection without terminating rescue");
        GUI.commands.Clear();Call(app,"OnGUI");
        Check(!GUI.commands.Exists(c=>c.Contains("掩护 8s")||
            c.Contains("COVER 8s")),
            "Original world drawing clears the protection cue after its actual expiry");
        g.Talk(npc);
        Check(!Array.Exists(g.conversation.choices,
            c=>c.action=="wayfarer_cover"),
            "Expired protection cannot be replayed by re-opening the world NPC dialogue");
        g.Close();
        Console.WriteLine("PASS Live wayfarer cover NPC option, real bilingual world aura and quest timer");
    }






    private static void TacticalCompanionPostMouseClickDoesNotOverwritePersonalPin(FrontierRuntime app)
    {
        Prepare(app);
        GameCore g=app.Game;
        g.save.flags.Add("companion_0");
        g.SelectCompanion(0);g.enemies.Clear();
        g.save.english=true;g.mode="map";
        Set(app,"selectedRegion",g.save.room);
        Point originalPersonal=new Point(
            (float)Math.Floor(g.Position.x)+.5f,
            (float)Math.Floor(g.Position.y)+.5f);
        Check(g.PlaceLocalWaymark(originalPersonal),
            "The real player map can contain an existing personal destination during tactical planning");
        Point oldPin=g.LocalWaymark.Value;
        GUI.queuedClick=new Rect(888,665,306,21);
        Call(app,"OnGUI");GUI.queuedClick=null;
        var cursor=(MapTileCursor)typeof(FrontierRuntime).GetField(
            "mapTileCursor",Private).GetValue(app);
        Check(cursor.Active,"Mouse player opens the same tactical companion map picker");
        Point post=new Point();bool found=false;
        for(int dy=-5;dy<=5&&!found;dy++)
        for(int dx=-5;dx<=5&&!found;dx++)
        {
            Point candidate=g.companionPosition+new Point(dx,dy);
            candidate=new Point((float)Math.Floor(candidate.x)+.5f,
                (float)Math.Floor(candidate.y)+.5f);
            if((candidate-g.companionPosition).Length<2f||
                !g.InspectCompanionHoldPost(candidate))continue;
            post=candidate;found=true;
        }
        Check(found,"A second genuine reachable map cell exists for a mouse-defined tactical post");
        float scale=WorldStreaming.PreviewScale(g.Map.rows[0].Length,
            g.Map.rows.Length,306,207);
        float mx=888+post.x*scale,my=264+post.y*scale;
        Input.mousePosition=new Vector3(mx,Screen.height-my,0);
        GUI.queuedClick=new Rect(888,264,
            g.Map.rows[0].Length*scale,g.Map.rows.Length*scale);
        Call(app,"OnGUI");
        Check(GUI.queuedClick==null&&!cursor.Active&&
            g.CompanionHoldingPosition&&
            (g.CompanionHoldPosition-post).Length<.001f&&
            g.LocalWaymark.HasValue&&
            (g.LocalWaymark.Value-oldPin).Length<.001f,
            "Clicking actual map terrain commands only the ally, preserving the independent player pin");
        GUI.queuedClick=null;g.Close();
        Console.WriteLine("PASS Mouse tactical ally post uses actual authored map tile while preserving personal map pin");
    }

    private static void CompanionMenuOpensPhysicalPostSelectorWithoutAlteringParty(FrontierRuntime app)
    {
        Prepare(app);
        GameCore game=app.Game;
        game.save.flags.Add("companion_0");
        game.SelectCompanion(0);
        game.mode="companions";
        Point before=game.companionPosition;
        GUI.queuedClick=new Rect(756,702,461,29);
        Call(app,"OnGUI");
        GUI.queuedClick=null;
        var cursor=(MapTileCursor)typeof(FrontierRuntime).GetField(
            "mapTileCursor",Private).GetValue(app);
        bool edit=(bool)typeof(FrontierRuntime).GetField(
            "mapCursorCompanionPost",Private).GetValue(app);
        Check(game.mode=="map"&&cursor.Active&&edit&&
            game.save.companion==0&&!game.CompanionHoldingPosition&&
            (game.companionPosition-before).Length<.001f,
            "Companion page opens tactical-map editing without teleporting the selected ally or auto-issuing an order");
        Input.keys.Add(KeyCode.Escape);Update(app);
        Check(!cursor.Active&&!game.CompanionHoldingPosition,
            "Canceling a map post from the companion page leaves existing party orders untouched");
        game.Close();
        Console.WriteLine("PASS Companion party page opens real tactical-map post editor without side effects");
    }

    private static void TacticalCompanionPostMapControlsRespectRealPhysicalOrders(FrontierRuntime app)
    {
        Prepare(app);
        GameCore g=app.Game;
        g.save.flags.Add("companion_0");
        g.SelectCompanion(0);
        g.enemies.Clear();g.projectiles.Clear();
        g.save.english=true;g.mode="map";
        Set(app,"selectedRegion",g.save.room);
        Point foot=g.companionPosition;
        GUI.queuedClick=new Rect(888,665,306,21);
        Call(app,"OnGUI");GUI.queuedClick=null;
        var cursor=(MapTileCursor)typeof(FrontierRuntime).GetField(
            "mapTileCursor",Private).GetValue(app);
        Check(cursor.Active&&(cursor.Destination-
            new Point((float)Math.Floor(foot.x)+.5f,
                (float)Math.Floor(foot.y)+.5f)).Length<.001f,
            "Actual game map offers a companion tactical-post picker at recruited ally's real foot");
        GUI.commands.Clear();Call(app,"OnGUI");
        Check(GUI.commands.Exists(command=>command.Contains("ALLY ROUTE OPEN"))&&
            !g.CompanionHoldingPosition,
            "Live map visually previews an allied route before spending the actual order");
        Point post=new Point();bool found=false;
        for(int dy=-4;dy<=4&&!found;dy++)
        for(int dx=-4;dx<=4&&!found;dx++)
        {
            Point candidate=cursor.Destination+new Point(dx,dy);
            if((candidate-foot).Length<2f||
                !g.InspectCompanionHoldPost(candidate))continue;
            post=candidate;found=true;
        }
        Check(found,"The real map has a reachable alternate companion post");
        cursor.Move((int)(post.x-cursor.Destination.x),
            (int)(post.y-cursor.Destination.y));
        Input.keys.Add(KeyCode.JoystickButton0);Update(app);
        Check(!cursor.Active&&g.CompanionHoldingPosition&&
            (g.CompanionHoldPosition-post).Length<.001f,
            "Actual pad A assigns companion post without moving the real ally instantaneously");
        Point old=g.CompanionHoldPosition;
        Input.keys.Add(KeyCode.P);Update(app);
        Check(cursor.Active,"Map P directly reopens the existing companion tactical post");
        cursor.Move(2,1);
        Input.keys.Add(KeyCode.JoystickButton1);Update(app);
        Check(!cursor.Active&&g.CompanionHoldingPosition&&
            (g.CompanionHoldPosition-old).Length<.001f,
            "Canceling the new post keeps the prior order and its physical endpoint");
        Input.keys.Add(KeyCode.JoystickButton2);Update(app);
        Check(cursor.Active,"Gamepad X can edit the tactical order without changing the player pin");
        cursor.Cancel();
        g.Close();
        Console.WriteLine("PASS Live companion map post: GUI button, real route preview, pad confirm, keyboard and gamepad edit, cancellation safety");
    }

    private static void MapKeyboardPadCanRepositionSavedPinWithoutClearingOrLongWalk(FrontierRuntime app)
    {
        Prepare(app);
        GameCore game=app.Game;
        game.enemies.Clear();game.projectiles.Clear();
        game.save.english=true;game.mode="map";
        Set(app,"selectedRegion",game.save.room);
        Point original=new Point(
            (float)Math.Floor(game.Position.x)+.5f,
            (float)Math.Floor(game.Position.y)+.5f);
        Check(game.PlaceLocalWaymark(original),
            "A real original local pin can be placed before testing re-pin controls");
        int flags=game.save.flags.Count;
        Input.keys.Add(KeyCode.R);Update(app);
        var f=typeof(FrontierRuntime).GetField("mapTileCursor",Private);
        MapTileCursor cursor=(MapTileCursor)f.GetValue(app);
        Check(cursor.Active&&(cursor.Destination-original).Length<.001f&&
            game.HasLocalWaymark,
            "R opens a physical tile editor at the previous pin without destroying that destination");
        Point before=cursor.Destination;
        Input.held.Add(KeyCode.LeftShift);
        Input.keys.Add(KeyCode.RightArrow);Update(app);
        Input.held.Clear();
        Point jumped=cursor.Destination;
        int maxX=game.Map.rows[0].Length-1;
        Check(Math.Abs(jumped.x-(Math.Min(maxX,(int)before.x+6)+.5f))<.001f,
            "Shift+Right jumps six bounded map tiles rather than one tiny step");
        Input.keys.Add(KeyCode.JoystickButton4);Update(app);
        Point shoulder=cursor.Destination;
        Check(Math.Abs(shoulder.y-(Math.Max(0,(int)jumped.y-6)+.5f))<.001f,
            "Gamepad LB rapidly moves the selected physical tile vertically");
        Input.keys.Add(KeyCode.JoystickButton1);Update(app);
        Check(!cursor.Active&&game.HasLocalWaymark&&
            (game.LocalWaymark.Value-original).Length<.001f,
            "Canceling a moved map cursor never discards the previous player route");
        Input.keys.Add(KeyCode.JoystickButton3);Update(app);
        Check(cursor.Active&&(cursor.Destination-original).Length<.001f,
            "Gamepad Y opens the same editor at the still-existing original destination");
        cursor.Move(2,0);
        Point blocked=cursor.Destination;
        int bx=(int)blocked.x,by=(int)blocked.y;
        string[] rows=game.Map.rows;
        string originalRow=rows[by];
        char[] row=originalRow.ToCharArray();
        try
        {
            row[bx]='#';rows[by]=new string(row);
            Input.keys.Add(KeyCode.JoystickButton0);Update(app);
            Check(cursor.Active&&game.HasLocalWaymark&&
                (game.LocalWaymark.Value-original).Length<.001f,
                "An invalid re-pin confirmation must leave the last usable route intact");
        }
        finally
        {
            rows[by]=originalRow;
        }
        cursor.Move(-2,0);
        Point second=new Point();bool found=false;
        for(int dy=-4;dy<=4&&!found;dy++)
        for(int dx=-4;dx<=4&&!found;dx++)
        {
            Point candidate=original+new Point(dx,dy);
            if((candidate-original).Length<.1f||
                game.InspectLocalWaymark(candidate)!=WaymarkRouteStatus.Available)continue;
            second=candidate;found=true;
        }
        Check(found,"The real map has a different reachable destination for a successful edit");
        cursor.Move((int)(second.x-original.x),(int)(second.y-original.y));
        Input.keys.Add(KeyCode.JoystickButton0);Update(app);
        Check(!cursor.Active&&game.HasLocalWaymark&&
            (game.LocalWaymark.Value-second).Length<.001f&&
            game.save.flags.Count==flags&&game.save.version==2,
            "A second real pin commits once without an intervening clear or story/save mutation");
        game.Close();
        Console.WriteLine("PASS R/Y re-pin, keyboard rapid map travel, LB scrolling, cancel/reject preservation and valid replacement");
    }

    private static void LiveMapTilePreviewUsesPhysicalRouteAndRepaintCache(FrontierRuntime app)
    {
        Prepare(app);
        var game=app.Game;
        game.enemies.Clear();game.projectiles.Clear();
        game.save.english=true;
        game.mode="map";
        Set(app,"selectedRegion",game.save.room);
        GUI.queuedClick=new Rect(888,571,306,35);
        Call(app,"OnGUI");
        GUI.queuedClick=null;
        var field=typeof(FrontierRuntime).GetField("mapTileCursor",Private);
        var cursor=(MapTileCursor)field.GetValue(app);
        Check(cursor.Active,"Controller-friendly tile selection opens from the actual map button");
        GUI.commands.Clear();Call(app,"OnGUI");
        Check(GUI.commands.Exists(command=>command.Contains("WALKABLE"))&&
            (WaymarkRouteStatus)typeof(FrontierRuntime).GetField(
                "mapCursorPreviewStatus",Private).GetValue(app)==WaymarkRouteStatus.Available,
            "Real map selector reports reachable footing before accepting a pin");
        var list=(System.Collections.Generic.List<Point>)typeof(FrontierRuntime)
            .GetField("mapCursorPreviewPath",Private).GetValue(app);
        Check(list.Count>0&&(list[list.Count-1]-cursor.Destination).Length<.001f,
            "Preview route reaches the selected actual cell, not just a remote approximate quest marker");
        var existing=list;
        GUI.commands.Clear();Call(app,"OnGUI");
        Check(object.ReferenceEquals(existing,typeof(FrontierRuntime).GetField(
                "mapCursorPreviewPath",Private).GetValue(app)),
            "Idling on the map reuses the same physical-preview path storage");
        MapData m=game.Map;
        string[] old=m.rows;
        try
        {
            // The initial selected tile is the HERO's own tile. Blocking
            // it should correctly report hero footing, not a bad target.
            // Move to another genuinely reachable cell before sealing it.
            Point nearby=new Point();bool nearbyFound=false;
            for(int dy=-3;dy<=3&&!nearbyFound;dy++)
            for(int dx=-3;dx<=3&&!nearbyFound;dx++)
            {
                Point candidate=game.Position+new Point(dx,dy);
                if((candidate-game.Position).Length<1.1f||
                    game.InspectLocalWaymark(candidate)!=WaymarkRouteStatus.Available)
                    continue;
                nearby=candidate;nearbyFound=true;
            }
            Check(nearbyFound,"The original map offers a second distinct reachable preview cell");
            cursor.Move((int)(nearby.x-cursor.Destination.x),
                (int)(nearby.y-cursor.Destination.y));
            int x=(int)cursor.Destination.x,y=(int)cursor.Destination.y;
            var rows=(string[])old.Clone();
            char[] cells=rows[y].ToCharArray();
            cells[x]='#';rows[y]=new string(cells);m.rows=rows;
            GUI.commands.Clear();Call(app,"OnGUI");
            Check(GUI.commands.Exists(command=>command.Contains("BLOCKED TILE"))&&
                (WaymarkRouteStatus)typeof(FrontierRuntime).GetField(
                    "mapCursorPreviewStatus",Private).GetValue(app)==WaymarkRouteStatus.DestinationBlocked&&
                list.Count==0&&!game.HasLocalWaymark,
                "Terrain changing under a selected tile immediately invalidates the preview without inventing an accepted pin");
            m.rows=old;
            GUI.commands.Clear();Call(app,"OnGUI");
            Check(GUI.commands.Exists(command=>command.Contains("WALKABLE"))&&list.Count>0,
                "Reopening the same terrain restores preview and physical breadcrumbs");
        }
        finally{m.rows=old;}
        cursor.Cancel();
        game.Close();
        Console.WriteLine("PASS Live controller map preview: physical full-body route, blocked terrain, unchanged pin and repaint cache");
    }

    private static void LocalMapClickPinDrawsRealWorldRouteAndCanClear(FrontierRuntime app)
    {
        Prepare(app);
        var g=app.Game;
        g.enemies.Clear();g.projectiles.Clear();
        g.save.showGuidance=false;g.save.english=true;
        Point chosen=new Point();bool found=false;
        int w=g.Map.rows[0].Length,h=g.Map.rows.Length;
        for(int y=3;y<h-3&&!found;y++)
        for(int x=3;x<w-3&&!found;x++)
        {
            Point candidate=new Point(x+.5f,y+.5f);
            if((candidate-g.Position).Length<5f||!g.CanStand(candidate)||
               g.NavigationPathFrom(g.Position,candidate,true).Count==0)
                continue;
            chosen=candidate;found=true;
        }
        Check(found,"The genuine Hearth map contains a reachable nontrivial personal destination");
        g.mode="map";Set(app,"selectedRegion",g.save.room);
        float scale=WorldStreaming.PreviewScale(w,h,306,207);
        float sx=888+chosen.x*scale,sy=264+chosen.y*scale;
        Input.mousePosition=new UnityEngine.Vector3(sx,Screen.height-sy,0);
        GUI.queuedClick=new Rect(888,264,w*scale,h*scale);
        GUI.buttonCalls.Clear();GUI.commands.Clear();
        Call(app,"OnGUI");
        Check(g.HasLocalWaymark&&
            (g.LocalWaymark.Value-chosen).Length<.001f&&
            GUI.buttonCalls.Exists(c=>Math.Abs(c.rect.x-888)<.001f&&
                Math.Abs(c.rect.y-264)<.001f),
            "Clicking actual authored terrain, not a generic placeholder button, pins its physical tile");
        Point original=g.LocalWaymark.Value;
        Set(app,"selectedRegion",g.save.room==0?1:0);
        GUI.queuedClick=new Rect(888,264,w*scale,h*scale);
        Call(app,"OnGUI");
        GUI.queuedClick=null;
        Check(g.HasLocalWaymark&&(g.LocalWaymark.Value-original).Length<.001f,
            "Previewing another region cannot forge a remote personal destination");
        Set(app,"selectedRegion",g.save.room);
        g.Close();GUI.commands.Clear();Call(app,"OnGUI");
        Check(g.PersonalNavigationActive&&
            GUI.commands.Exists(c=>c.Contains("PERSONAL PIN")),
            "Live world HUD identifies the user-selected route even when quest guidance is hidden");
        var nav=typeof(FrontierRuntime).GetMethod("NavigationRoute",
            System.Reflection.BindingFlags.Instance|
            System.Reflection.BindingFlags.NonPublic);
        Check(nav!=null,"Actual runtime uses its existing cached physical route");
        var path=(System.Collections.Generic.List<Point>)nav.Invoke(app,null);
        Check(path!=null&&path.Count>0,
            "Personal waypoint drives genuine world-navigation BFS rather than a fake marker");
        // A live destination can become unwalkable after a changing gate or
        // tide. Exercise the real in-game map and world HUD rather than only
        // trusting the transient GameCore flag.
        MapData pinnedMap=g.Map;string[] originalRows=pinnedMap.rows;
        try
        {
            string[] sealedRows=(string[])originalRows.Clone();
            int px=(int)Math.Floor(chosen.x),py=(int)Math.Floor(chosen.y);
            char[] blocked=sealedRows[py].ToCharArray();
            blocked[px]='#';sealedRows[py]=new string(blocked);
            pinnedMap.rows=sealedRows;
            for(int i=0;i<32;i++)g.Tick(.05f,new Point());
            Check(g.LocalWaymarkRouteBlocked&&g.HasLocalWaymark,
                "Blocked live destination holds the player's original personal pin");
            GUI.commands.Clear();Call(app,"OnGUI");
            Check(GUI.commands.Exists(c=>c.Contains("ROUTE BLOCKED")) &&
                !GUI.commands.Exists(c=>c.Contains("PERSONAL PIN")),
                "Actual world mini HUD advertises the closed route instead of misleading normal guidance");
            pinnedMap.rows=originalRows;
            for(int i=0;i<32;i++)g.Tick(.05f,new Point());
            GUI.commands.Clear();Call(app,"OnGUI");
            Check(!g.LocalWaymarkRouteBlocked&&
                GUI.commands.Exists(c=>c.Contains("PERSONAL PIN")),
                "Original real-world HUD destination and route cues return when footing reopens");
        }
        finally{pinnedMap.rows=originalRows;}
        g.mode="map";
        GUI.buttonCalls.Clear();GUI.commands.Clear();
        GUI.queuedClick=new Rect(888,571,306,35);
        Call(app,"OnGUI");
        Check(!g.HasLocalWaymark&&!g.PersonalNavigationActive&&
            g.save.version==2,
            "Real visible clear-pin control retires the temporary path with old save compatibility");
        g.Close();GUI.commands.Clear();Call(app,"OnGUI");
        Check(!GUI.commands.Exists(c=>c.Contains("PERSONAL PIN")),
            "Cleared personal destination never leaves an orphan HUD cue");
        Console.WriteLine("PASS Live authored map click/pin, route overlay, region isolation and clear control");
    }

    private static void LiveSprintKeyAndL3MaintainPhysicalHudAndFatigue(FrontierRuntime app)
    {
        Prepare(app);
        var g=app.Game;
        g.mode="world";g.save.room=0;g.save.english=true;
        g.enemies.Clear();g.projectiles.Clear();g.hazards.Clear();
        g.save.energy=100f;
        bool corridor=false;
        for(int y=5;y<23&&!corridor;y++)
        for(int x=5;x<28&&!corridor;x++)
        {
            var foot=new Point(x+.5f,y+.5f);
            bool walkable=true;
            for(int i=0;i<=25;i++)
                if(!g.CanStand(foot+new Point(i*.1f,0)))
                { walkable=false;break; }
            if(!walkable)continue;
            g.save.x=foot.x;g.save.y=foot.y;corridor=true;
        }
        Check(corridor,"The live Emberfall scene has a real clear lane for an L3 sprint test");
        Set(app,"camera",g.Position);
        Input.Clear();Input.axes["Horizontal"]=1f;
        Input.held.Add(KeyCode.LeftShift);
        Point beginning=g.Position;
        float baseline=g.Speed*.02f;
        Update(app);
        float run=(g.Position-beginning).Length;
        Check(g.Sprinting&&run>baseline*1.44f&&run<baseline*1.55f&&
            g.save.energy<100f,
            "Actual runtime Shift hold produces real boosted movement and charges stamina");
        GUI.commands.Clear();Call(app,"OnGUI");
        Check(GUI.commands.Exists(c=>c.Contains("SPRINT ×1.5")),
            "Production HUD indicates real full-body sprint and live energy drain");
        Input.held.Remove(KeyCode.LeftShift);
        beginning=g.Position;Update(app);
        Check(!g.Sprinting&&(g.Position-beginning).Length<baseline*1.08f,
            "Releasing keyboard sprint immediately restores ordinary world walking");
        Input.held.Add(KeyCode.JoystickButton8);
        beginning=g.Position;Update(app);
        Check(g.Sprinting&&(g.Position-beginning).Length>baseline*1.44f,
            "Holding genuine controller L3 uses the same physical player sprint");
        g.save.energy=10.1f;
        for(int i=0;i<6&&!g.SprintFatigued;i++)Update(app);
        // The last PAID sprint sample can trigger fatigue at its end.
        // Its actual traveled footstep is still a sprint until the next
        // simulation slice applies the now-locked normal walking speed.
        Check(g.SprintFatigued,
            "Paid last sprint segment crosses the real fatigue threshold");
        Update(app);
        Check(g.SprintFatigued&&!g.Sprinting,
            "An actual controller-held sprint runs out of usable stamina and stops accelerating");
        GUI.commands.Clear();Call(app,"OnGUI");
        Check(GUI.commands.Exists(c=>c.Contains("SPRINT TIRED")),
            "Production HUD explains fatigue and the minimum restart energy");
        g.save.energy=39f;
        Update(app);
        Check(!g.SprintFatigued&&g.Sprinting,
            "Recovering energy with L3 still held safely resumes voluntary sprint");
        Input.Clear();g.mode="inventory";beginning=g.Position;
        Input.held.Add(KeyCode.LeftShift);
        Update(app);
        Check((g.Position-beginning).Length<.0001f,
            "Modal inventory focus never interprets the held sprint button as world movement");
        Console.WriteLine("PASS Physical runtime Shift/L3 sprint, HUD, fatigue and modal input isolation");
    }

    private static void InnTravelerOffersSourceBoundRoadTopicsAndPreservesChronicle(FrontierRuntime app)
    {
        Prepare(app);
        GameCore g=app.Game;
        g.save.room=9;g.mode="world";g.save.english=true;
        WorldObject guest=null;
        foreach(WorldObject o in g.Map.objects)
            if(o.kind=="innGuest"){guest=o;break;}
        Check(guest!=null,"The authored Hearth Inn has its original night traveler");
        bool found=false;
        for(float dx=-1.5f;dx<=1.5f&&!found;dx+=.25f)
        for(float dy=-1.5f;dy<=1.5f;dy+=.25f)
        {
            g.save.x=guest.x+dx;g.save.y=guest.y+dy;
            if(g.CanStand(g.Position)&&g.Nearest()==guest)
            {found=true;break;}
        }
        Check(found,"The real night traveler is physically reachable by world E");
        int coins=g.save.coins,shards=g.save.shards,flags=g.save.flags.Count;
        g.Interact();
        Check(g.mode=="dialog"&&g.conversationSource==guest&&
            g.conversation.speaker==guest.name&&
            !g.conversation.textEn.Contains("The warden's letter finally"),
            "The actual E interaction opens the concise traveler instead of dumping all chapters");
        int road=-1,news=-1,journal=-1;
        for(int i=0;i<g.conversation.choices.Length;i++)
        {
            string action=g.conversation.choices[i].action;
            if(action=="inn_routelead")road=i;
            if(action=="inn_news")news=i;
            if(action=="inn_openjournal")journal=i;
        }
        Check(road>=0&&news>=0&&journal>=0,
            "Three actual authored topics preserve a direct journal handoff");
        g.Choose(road);
        Check(g.mode=="dialog"&&g.conversationSource==guest&&
            g.conversation.speaker==guest.name&&
            g.conversation.textEn.Contains("journal"),
            "Road advice uses current live quest context and the original traveler identity");
        int back=-1;
        for(int i=0;i<g.conversation.choices.Length;i++)
            if(g.conversation.choices[i].action=="inn_topics")back=i;
        Check(back>=0,"The branch has an explicit way back to the same speaker");
        g.Choose(back);
        news=-1;
        for(int i=0;i<g.conversation.choices.Length;i++)
            if(g.conversation.choices[i].action=="inn_news")news=i;
        Check(news>=0,"The original published chronicle remains discoverable");
        g.Choose(news);
        Check(g.conversationSource==guest&&
            g.conversation.textEn.Contains("The warden's letter finally found")&&
            g.conversation.speaker==guest.name,
            "The full legacy chapter aftermath is retained as opt-in dialogue");
        back=-1;
        for(int i=0;i<g.conversation.choices.Length;i++)
            if(g.conversation.choices[i].action=="inn_topics")back=i;
        Check(back>=0,"Chapter news can return without re-opening another NPC");
        g.Choose(back);
        journal=-1;
        for(int i=0;i<g.conversation.choices.Length;i++)
            if(g.conversation.choices[i].action=="inn_openjournal")journal=i;
        Check(journal>=0,"The topical menu keeps an actual journal action");
        g.Choose(journal);
        Check(g.mode=="journal"&&g.conversation==null&&
            g.save.coins==coins&&g.save.shards==shards&&
            g.save.flags.Count==flags&&g.save.version==2,
            "Journal handoff never changes quest selection, rewards, old saves or story flags");
        Set(app,"journalTab",1);Set(app,"journalOnlyActionable",true);
        Set(app,"selectedQuest",7);Set(app,"questPage",2);
        GUI.commands.Clear();Call(app,"OnGUI");
        int expectedIndex=Array.IndexOf(GameCore.QuestIds,g.save.trackedQuest);
        Check(GUI.buttonCalls.Count>0&&
            (int)typeof(FrontierRuntime).GetField("journalTab",Private).GetValue(app)==0&&
            (int)typeof(FrontierRuntime).GetField("selectedQuest",Private).GetValue(app)==expectedIndex&&
            (int)typeof(FrontierRuntime).GetField("questPage",Private).GetValue(app)==expectedIndex/14&&
            !(bool)typeof(FrontierRuntime).GetField("journalOnlyActionable",Private).GetValue(app),
            "The live NPC journal action focuses the exact tracked quest instead of an unrelated previous tab/page");
        g.Close();g.save.english=false;g.Interact();
        Check(g.mode=="dialog"&&g.conversation.text.Contains("旅人把热汤")&&
            g.conversationSource==guest,
            "Chinese speaker reuses the same concise authored interaction");
        g.Close();g.save.english=true;g.Interact();
        int stale=-1;
        for(int i=0;i<g.conversation.choices.Length;i++)
            if(g.conversation.choices[i].action=="inn_news")stale=i;
        Check(stale>=0,"The traveler has a source-bound news action");
        WorldObject[] original=g.Map.objects;
        try
        {
            g.Map.objects=Array.FindAll(original,o=>!object.ReferenceEquals(o,guest));
            g.Choose(stale);
            Check(g.mode=="world"&&g.conversation==null&&
                g.save.coins==coins&&g.save.flags.Count==flags,
                "Removing the exact source rejects an old topic instead of opening an unrelated NPC");
        }
        finally{g.Map.objects=original;}
        Console.WriteLine("PASS Original inn traveler E topics, source-bound dialogue, bilingual prose, optional chronicle and live journal handoff");
    }

    private static void InnTravelerJobBoardTracksOnlyOpenUnfinishedCampaignWork(
        FrontierRuntime app)
    {
        Prepare(app);
        GameCore g=app.Game;
        g.mode="world";g.save.room=9;g.save.stage=4;g.save.english=true;
        WorldObject guest=null;
        foreach(WorldObject o in g.Map.objects)
            if(o.kind=="innGuest"){guest=o;break;}
        Check(guest!=null,"Published Hearth Inn traveler owns the new mission board");
        bool found=false;
        for(float dx=-1.5f;dx<=1.5f&&!found;dx+=.25f)
        for(float dy=-1.5f;dy<=1.5f;dy+=.25f)
        {
            g.save.x=guest.x+dx;g.save.y=guest.y+dy;
            if(g.CanStand(g.Position)&&g.Nearest()==guest)
            {found=true;break;}
        }
        Check(found,"Board can be reached by ordinary physical world interaction");
        g.mode="map";
        Check(g.PlaceLocalWaymark(g.Position)&&g.HasLocalWaymark,
            "A genuine player-owned map pin exists before accepting new work");
        Point originalPin=g.LocalWaymark.Value;
        g.mode="world";
        int beforeCoins=g.save.coins,beforeShards=g.save.shards,
            beforeFlags=g.save.flags.Count;
        int[] inventory=(int[])g.save.inventory.Clone();
        string originallyTracked=g.save.trackedQuest;
        g.Interact();
        int board=Array.FindIndex(g.conversation.choices,
            c=>c.action=="inn_jobboard");
        Check(board>=0,"Real NPC conversation exposes the fieldwork board");
        g.Choose(board);
        Check(g.mode=="dialog"&&g.conversationSource==guest,
            "Board retains the physical original speaker identity");
        int first=-1,more=-1;
        string chosen=null;
        for(int i=0;i<g.conversation.choices.Length;i++)
        {
            string action=g.conversation.choices[i].action;
            if(action.StartsWith("inn_job_")&&first<0)
            {first=i;chosen=action.Substring("inn_job_".Length);}
            if(action=="inn_jobs_next")more=i;
        }
        Check(first>=0&&Array.IndexOf(GameCore.QuestIds,chosen)>=0,
            "The board lists genuine authored quest IDs, not decorative missions");
        QuestGuide quest=g.GetQuest(chosen);
        Check(g.conversation.choices[first].label!=g.conversation.choices[first].en&&
            !g.conversation.choices[first].label.Contains(" · nearby")&&
            !g.conversation.choices[first].en.Contains(" · 附近"),
            "Live job board presents separate localized quest titles and map labels");
        Check(!quest.locked&&!quest.complete&&g.CanEnter(quest.room)&&
            chosen!=originallyTracked,
            "Only currently open, unfinished, accessible work may be offered");
        if(more>=0)
        {
            string originalFirst=chosen;
            g.Choose(more);
            Check(g.mode=="dialog"&&g.conversationSource==guest,
                "More jobs opens the next real source-bound dialogue page");
            int prev=Array.FindIndex(g.conversation.choices,
                c=>c.action=="inn_jobs_prev");
            Check(prev>=0,"Page two provides an actual Previous option");
            g.Choose(prev);
            first=Array.FindIndex(g.conversation.choices,
                c=>c.action=="inn_job_"+originalFirst);
            Check(first>=0,"Returning to page one restores the original quest choices");
        }
        g.Choose(first);
        Check(g.conversation.text.Contains("下一步：")&&
            g.conversation.text.Contains("报酬：")&&
            g.conversation.textEn.Contains("Next: ")&&
            g.conversation.textEn.Contains("Reward: ")&&
            !g.conversation.text.Contains("Next: ")&&
            !g.conversation.textEn.Contains("下一步：")&&g.save.english,
            "Inn mission details keep true Chinese and English records independently without toggling the player's setting");
        Check(g.mode=="dialog"&&g.conversationSource==guest&&
            g.save.trackedQuest==originallyTracked&&
            g.conversation.textEn.Contains("Next:")&&
            g.conversation.textEn.Contains("Reward:")&&
            g.conversation.textEn.Contains(quest.title)&&
            g.save.coins==beforeCoins&&g.save.shards==beforeShards&&
            g.save.flags.Count==beforeFlags,
            "Browsing opens true mission details and rewards without changing tracked quests");
        int confirm=Array.FindIndex(g.conversation.choices,
            c=>c.action=="inn_confirm_"+chosen);
        Check(confirm>=0,"Only the explicit confirmation tracks a board mission");
        g.Choose(confirm);
        Check(g.save.trackedQuest==chosen&&g.mode=="dialog"&&
            g.conversationSource==guest&&
            Array.Exists(g.conversation.choices,c=>c.action=="inn_openjournal")&&
            g.save.coins==beforeCoins&&g.save.shards==beforeShards&&
            g.save.flags.Count==beforeFlags&&g.save.version==2,
            "Explicit confirmed job tracks only the original quest and never grants unearned loot");
        for(int i=0;i<inventory.Length;i++)
            Check(inventory[i]==g.save.inventory[i],
                "Quest discovery must never issue supplies or consume crafting materials");
        Check(g.HasLocalWaymark&&
            (g.LocalWaymark.Value-originalPin).Length<.001f,
            "Tracking a new job alone does not steal a hand-placed personal pin");
        int follow=Array.FindIndex(g.conversation.choices,
            c=>c.action=="inn_follow_"+chosen);
        Check(follow>=0,"NPC offers an explicit, separate route-ownership choice");
        g.Choose(follow);
        Check(g.save.trackedQuest==chosen&&
            g.save.coins==beforeCoins&&g.save.flags.Count==beforeFlags,
            "Changing the navigation owner neither changes chosen job nor rewards");
        if(quest.room!=g.save.room)
            Check(!g.HasLocalWaymark,
                "Cross-region job route clears the old pin only after explicit player consent");
        else if(g.HasLocalWaymark)
            Check(g.QuestApproachNavigationActive&&
                g.PersonalNavigationActive,
                "Nearby eligible quest uses a freshly validated physical approach rather than an old manual destination");
        else Check(g.conversation.textEn.Contains("blocked"),
            "Inaccessible local quest leaves a meaningful navigation-blocked explanation");
        int open=Array.FindIndex(g.conversation.choices,
            c=>c.action=="inn_openjournal");
        Check(open>=0,"After assigning route the exact journal remains accessible");
        g.Choose(open);
        Check(g.mode=="journal"&&g.conversation==null,
            "Selecting the job permits the authenticated journal handoff");
        Set(app,"journalTab",2);Set(app,"journalOnlyActionable",true);
        Set(app,"selectedQuest",0);Set(app,"questPage",3);
        GUI.commands.Clear();Call(app,"OnGUI");
        int expected=Array.IndexOf(GameCore.QuestIds,chosen);
        Check((int)typeof(FrontierRuntime).GetField("selectedQuest",Private).GetValue(app)==expected&&
            (int)typeof(FrontierRuntime).GetField("journalTab",Private).GetValue(app)==0&&
            (int)typeof(FrontierRuntime).GetField("questPage",Private).GetValue(app)==expected/14,
            "Production journal focuses the accepted real quest after NPC board selection");
        g.Close();g.Interact();
        int again=Array.FindIndex(g.conversation.choices,
            c=>c.action=="inn_jobboard");
        Check(again>=0,"Jobs remain discoverable after selecting a track");
        g.Choose(again);
        Check(!Array.Exists(g.conversation.choices,
            c=>c.action=="inn_job_"+chosen),
            "The currently tracked quest no longer fills an alternate board slot");
        g.Close();
        // Distant route handoff must operate through the same real inn
        // dialogue, map transitions and physical route implementation.
        g.Interact();
        int distantBoard=Array.FindIndex(g.conversation.choices,
            c=>c.action=="inn_jobboard");
        Check(distantBoard>=0,"Inn remains an actual repeatable fieldwork service");
        g.Choose(distantBoard);
        string distantId=null;int distantChoice=-1;
        for(int page=0;page<30&&distantId==null;page++)
        {
            for(int i=0;i<g.conversation.choices.Length;i++)
            {
                string action=g.conversation.choices[i].action;
                if(!action.StartsWith("inn_job_",StringComparison.Ordinal))continue;
                string id=action.Substring("inn_job_".Length);
                QuestGuide candidate=g.GetQuest(id);
                if(candidate.room==9||candidate.room==8||
                   !g.CanEnter(candidate.room))continue;
                distantId=id;distantChoice=i;break;
            }
            if(distantId!=null)break;
            int next=Array.FindIndex(g.conversation.choices,
                c=>c.action=="inn_jobs_next");
            if(next<0)break;
            g.Choose(next);
        }
        Check(distantId!=null&&distantChoice>=0,
            "The actual Inn board offers a reachable distant campaign job");
        int distantRoom=g.GetQuest(distantId).room;
        g.Choose(distantChoice);
        int distantConfirm=Array.FindIndex(g.conversation.choices,
            c=>c.action=="inn_confirm_"+distantId);
        Check(distantConfirm>=0,"Distant job still requires player confirmation");
        g.Choose(distantConfirm);
        int distantFollow=Array.FindIndex(g.conversation.choices,
            c=>c.action=="inn_follow_"+distantId);
        Check(distantFollow>=0,"Distant job offers explicit route ownership");
        g.Choose(distantFollow);
        Check(g.QuestApproachTravelHandoffPending&&!g.HasLocalWaymark&&
            g.save.trackedQuest==distantId,
            "Consent queues a transient cross-region route without a fake current-room pin");
        if(distantRoom!=0)
        {
            g.Enter(0);
            Check(g.save.room==0&&g.QuestApproachTravelHandoffPending&&
                !g.HasLocalWaymark,
                "Passing through a real intermediate hub preserves distant route intent only");
        }
        g.Enter(distantRoom);
        Check(g.save.room==distantRoom&&g.save.trackedQuest==distantId,
            "Real region transition preserves the original authored tracked quest");
        if(g.HasLocalWaymark)
        {
            Check(g.QuestApproachNavigationActive&&
                !g.QuestApproachTravelHandoffPending&&
                g.InspectLocalWaymark(g.LocalWaymark.Value)==WaymarkRouteStatus.Available,
                "Open destination installs a fresh physically walkable quest approach");
        }
        else Check(g.QuestApproachTravelHandoffPending,
            "Unreachable destination leaves route intent pending rather than forging a pin");
        g.mode="map";
        Check(g.PlaceLocalWaymark(g.Position)&&
            !g.QuestApproachTravelHandoffPending&&
            !g.QuestApproachNavigationActive,
            "Deliberate new manual map pin cancels automatic quest handoff");
        g.Close();g.Enter(9);
        Check(!g.HasLocalWaymark&&!g.QuestApproachTravelHandoffPending&&
            g.save.version==2,
            "Travel cannot resurrect a route canceled by the player or change save format");
        Console.WriteLine("PASS Hearth Inn fieldwork board, explicit cross-region route handoff, walkability and manual override without reward bypass");
    }

    private static void JournalRemoteQuestFollowsRealRegionTransition(FrontierRuntime app)
    {
        Prepare(app);
        GameCore g=app.Game;
        g.save.flags.Add("intro");g.save.flags.Add("scout_saved");
        g.save.stage=4;g.save.english=true;
        g.Enter(9);
        Check(g.save.room==9,"Real permitted Hearth Inn entrance is the remote journal fixture");
        g.mode="map";
        Check(g.PlaceLocalWaymark(g.Position)&&g.HasLocalWaymark,
            "Player manual pin exists before remote journal route request");
        Point savedPin=g.LocalWaymark.Value;
        string id=null;int index=-1;
        for(int i=0;i<GameCore.QuestIds.Length;i++)
        {
            string questId=GameCore.QuestIds[i];
            if(questId==g.save.trackedQuest)continue;
            QuestGuide q=g.GetQuest(questId);
            if(q.locked||q.complete||q.room==9||q.room==8||
                !g.CanEnter(q.room))continue;
            id=questId;index=i;break;
        }
        Check(id!=null,"A real accessible distant mission appears in the journal");
        g.mode="journal";
        Check(!g.PlaceQuestApproachWaymark(id)&&g.HasLocalWaymark&&
            (g.LocalWaymark.Value-savedPin).Length<.001f,
            "Untracked remote mission cannot silently take over a manual map pin");
        g.TrackQuest(id);
        Set(app,"selectedQuest",index);
        Set(app,"questPage",index/14);
        Set(app,"journalTab",0);
        Set(app,"journalOnlyActionable",false);
        GUI.commands.Clear();GUI.buttonCalls.Clear();
        Call(app,"OnGUI");
        Check(GUI.commands.Exists(s=>s.Contains("FOLLOW REMOTE QUEST")),
            "Production journal offers active remote navigation for its tracked unlocked job");
        int beforeFlags=g.save.flags.Count,coins=g.save.coins;
        GUI.queuedClick=new Rect(650,693,540,32);
        Call(app,"OnGUI");GUI.queuedClick=null;
        Check(g.QuestApproachTravelHandoffPending&&!g.HasLocalWaymark&&
            g.save.trackedQuest==id&&g.save.flags.Count==beforeFlags&&
            g.save.coins==coins&&g.save.version==2,
            "Journal click queues only user-approved remote handoff, without loot or save mutation");
        GUI.commands.Clear();Call(app,"OnGUI");
        Check(GUI.commands.Exists(s=>s.Contains("CANCEL REMOTE QUEST AUTO-FOLLOW")),
            "The live journal exposes an explicit cancel action for the pending remote route");
        g.mode="map";Set(app,"selectedRegion",g.GetQuest(id).room);
        GUI.commands.Clear();GUI.buttonCalls.Clear();Call(app,"OnGUI");
        Check(GUI.commands.Exists(s=>s.Contains("Cancel pending remote quest route")),
            "A different-region map still exposes pending-route cancellation without a local pin");
        GUI.queuedClick=new Rect(888,571,306,35);
        Call(app,"OnGUI");GUI.queuedClick=null;
        Check(!g.QuestApproachTravelHandoffPending&&!g.HasLocalWaymark&&
            g.save.trackedQuest==id&&g.save.flags.Count==beforeFlags&&
            g.save.coins==coins,
            "Canceling from the remote map clears only transient guidance, never quest or rewards");
        g.mode="journal";Set(app,"selectedQuest",index);
        Set(app,"journalTab",0);Set(app,"questPage",index/14);
        GUI.queuedClick=new Rect(650,693,540,32);
        Call(app,"OnGUI");GUI.queuedClick=null;
        Check(g.QuestApproachTravelHandoffPending,
            "The player can intentionally resume the same distant mission after cancellation");
        g.mode="world";GUI.commands.Clear();Call(app,"OnGUI");
        Check(GUI.commands.Exists(s=>s.Contains("REMOTE QUEST")&&
            s.Contains("AUTO-ROUTE")),
            "Real minimap labels the pending cross-region navigation while the physical pin is absent");
        int destination=g.GetQuest(id).room;
        if(destination!=0)
        {
            g.Enter(0);
            Check(g.QuestApproachTravelHandoffPending&&!g.HasLocalWaymark,
                "Real intermediate hub travel preserves explicit, still-distant journal route");
        }
        g.Enter(destination);
        Check(g.save.room==destination,"Actual unlocked target region was reached");
        if(g.HasLocalWaymark)
            Check(g.QuestApproachNavigationActive&&g.PersonalNavigationActive&&
                !g.QuestApproachTravelHandoffPending&&
                g.InspectLocalWaymark(g.LocalWaymark.Value)==WaymarkRouteStatus.Available,
                "Target region creates only a validated walking approach");
        else Check(g.QuestApproachTravelHandoffPending,
            "Unreachable physical target remains pending instead of fabricating a walking route");
        g.mode="map";
        Check(g.PlaceLocalWaymark(g.Position)&&
            !g.QuestApproachTravelHandoffPending&&
            !g.QuestApproachNavigationActive,
            "Manual destination intentionally cancels journal travel auto-follow");
        g.Close();
        Console.WriteLine("PASS Journal remote quest routing, explicit pin consent, unlocked portals, live HUD and verified arrival approach");
    }

    private static void SealedArrivalNeverPretendsRemoteQuestIsReachable(FrontierRuntime app)
    {
        Prepare(app);
        GameCore g=app.Game;
        g.save.flags.Add("intro");g.save.flags.Add("scout_saved");
        g.save.stage=4;g.save.tutorialBits=255;g.save.english=true;
        g.Enter(9);g.enemies.Clear();
        Check(g.save.room==9,"Hearth Inn starts the remote-route arrival fixture");
        WorldObject target=null;
        foreach(WorldObject o in g.data.maps[0].objects)
            if(o.id=="trailcircuit_0_1"){target=o;break;}
        Check(target!=null,"Shipped first regional survey marker is the real objective");
        Point old=new Point(target.x,target.y);
        for(int i=1;i<=3;i++)g.save.flags.Remove("trail_circuit_0_"+i);
        try
        {
            g.TrackQuest("trail_circuits");
            Check(g.GetQuest("trail_circuits").room==0&&g.CanEnter(0),
                "The authored unlocked quest genuinely leads from the Inn to town");
            g.mode="journal";
            Check(g.PlaceQuestApproachWaymark("trail_circuits")&&
                g.QuestApproachTravelHandoffPending,
                "Remote quest navigation is explicitly accepted from the real journal");
            target.x=-40;target.y=-40;
            g.Enter(0);
            Check(g.save.room==0&&g.QuestApproachTravelHandoffPending&&
                g.QuestApproachTravelArrivalBlocked&&!g.HasLocalWaymark&&
                !g.NavigationTarget().HasValue,
                "Sealed arrival never invents a walking pin or points beyond the terrain");
            GUI.commands.Clear();Call(app,"OnGUI");
            Check(GUI.commands.Exists(c=>c.Contains("QUEST ARRIVAL BLOCKED")),
                "World HUD distinguishes sealed arrival from traveling");
            g.mode="map";Set(app,"selectedRegion",0);
            GUI.commands.Clear();Call(app,"OnGUI");
            Check(GUI.commands.Exists(c=>c.Contains("Approach blocked")&&
                c.Contains("Cancel quest auto-follow")),
                "Actual map offers a cancel action for the unreachable quest");
            g.mode="world";
            Point recovered=new Point();
            bool found=false;
            Point hero=g.Position;
            for(int radius=3;radius<=12&&!found;radius++)
            for(int dx=-radius;dx<=radius&&!found;dx++)
            for(int dy=-radius;dy<=radius&&!found;dy++)
            {
                if(Math.Abs(dx)!=radius&&Math.Abs(dy)!=radius)continue;
                Point p=new Point((float)Math.Floor(hero.x)+dx+.5f,
                    (float)Math.Floor(hero.y)+dy+.5f);
                if(p.x<2||p.y<2||
                    p.x>=g.Map.rows[0].Length-2||p.y>=g.Map.rows.Length-2||
                    !g.CanStand(p)||
                    g.InspectLocalWaymark(p)!=WaymarkRouteStatus.Available)
                    continue;
                recovered=p;found=true;
            }
            Check(found,"A different physically reachable site exists when road opens");
            target.x=recovered.x;target.y=recovered.y;
            for(int i=0;i<30;i++)g.Tick(.05f,new Point());
            Check(!g.QuestApproachTravelHandoffPending&&
                !g.QuestApproachTravelArrivalBlocked&&
                g.QuestApproachNavigationActive&&g.HasLocalWaymark&&
                g.InspectLocalWaymark(g.LocalWaymark.Value)==WaymarkRouteStatus.Available,
                "Opening the physical road automatically enables a checked quest approach");
            Check(g.save.version==2&&g.save.trackedQuest=="trail_circuits",
                "Arrival recovery preserves the SaveData schema and quest");
        }
        finally{target.x=old.x;target.y=old.y;}
        g.Close();
        Console.WriteLine("PASS Blocked remote arrival presents real HUD/map status and resumes only on an open walking path");
    }

    private static void SwitchingTrackedQuestImmediatelyRetiresRemoteRoute(FrontierRuntime app)
    {
        Prepare(app);
        GameCore g=app.Game;
        g.save.stage=4;g.save.flags.Add("intro");
        g.save.flags.Add("scout_saved");g.save.english=true;
        g.Enter(9);
        Check(g.save.room==9,"Original Hearth Inn is available for tracked-quest arbitration");
        g.TrackQuest("trail_circuits");
        Check(!g.GetQuest("trail_circuits").locked&&
            g.GetQuest("trail_circuits").room!=9,
            "An active real regional survey is remote from the Inn");
        g.mode="journal";
        Check(g.PlaceQuestApproachWaymark("trail_circuits")&&
            g.QuestApproachTravelHandoffPending,
            "Tracking the distant survey creates a real pending navigation handoff");
        int coins=g.save.coins,shards=g.save.shards,flags=g.save.flags.Count;
        g.TrackQuest("main");
        Check(g.save.trackedQuest=="main"&&
            !g.QuestApproachTravelHandoffPending&&
            !g.QuestApproachTravelArrivalBlocked&&
            !g.HasLocalWaymark,
            "Tracking another mission cancels the old handoff immediately without a world tick");
        Check(g.save.coins==coins&&g.save.shards==shards&&
            g.save.flags.Count==flags,
            "Switching mission focus never changes rewards or story progress");
        g.mode="map";
        Check(g.PlaceLocalWaymark(g.Position)&&g.HasLocalWaymark,
            "A separate reachable player-owned map pin can still be placed");
        Point personal=g.LocalWaymark.Value;
        g.TrackQuest("trail_circuits");
        Check(g.HasLocalWaymark&&
            (g.LocalWaymark.Value-personal).Length<.001f&&
            !g.QuestApproachTravelHandoffPending,
            "Ordinary quest tracking never steals an explicit manual pin");
        g.TrackQuest("main");
        Check(g.HasLocalWaymark&&
            (g.LocalWaymark.Value-personal).Length<.001f&&
            g.save.version==2,
            "Switching back still preserves the player's independent destination and SaveData");
        g.Close();
        Console.WriteLine("PASS TrackQuest immediately retires abandoned cross-region handoff without losing manual pins");
    }

    private static void InspectedRemoteMapQuestCanAcquireRealCrossRegionRoute(FrontierRuntime app)
    {
        Prepare(app);
        GameCore g=app.Game;
        g.save.flags.Add("intro");g.save.flags.Add("scout_saved");g.save.stage=4;
        g.save.english=true;g.Enter(9);g.enemies.Clear();
        Check(g.save.room==9&&g.GetQuest("trail_circuits").room==0,
            "Real Inn fieldwork survey exposes unlocked distant world-map objective");
        g.mode="map";
        Check(g.PlaceLocalWaymark(g.Position),
            "An independent personal destination exists before changing map quest navigation");
        Point previous=g.LocalWaymark.Value;
        Set(app,"selectedRegion",0);
        Set(app,"mapInspectedQuestId","trail_circuits");
        g.TrackQuest("main");
        GUI.commands.Clear();Call(app,"OnGUI");
        Check(!GUI.commands.Exists(s=>s.Contains("FOLLOW INSPECTED REMOTE QUEST")),
            "Browsing a remote map quest never grants route ownership while another job is tracked");
        Check(!g.PlaceQuestApproachWaymark("trail_circuits")&&g.HasLocalWaymark&&
            (g.LocalWaymark.Value-previous).Length<.001f,
            "Core rejects a forged remote map route without a matching tracked quest");
        g.TrackQuest("trail_circuits");
        int beforeFlags=g.save.flags.Count,coins=g.save.coins;
        GUI.commands.Clear();GUI.buttonCalls.Clear();Call(app,"OnGUI");
        Check(GUI.commands.Exists(s=>s.Contains("FOLLOW INSPECTED REMOTE QUEST"))&&
            g.HasLocalWaymark&&!g.QuestApproachTravelHandoffPending,
            "World atlas offers a distinct consent button but browsing leaves the old pin untouched");
        GUI.queuedClick=new Rect(888,571,306,35);
        Call(app,"OnGUI");GUI.queuedClick=null;
        Check(g.QuestApproachTravelHandoffPending&&!g.HasLocalWaymark&&
            g.save.trackedQuest=="trail_circuits"&&
            g.save.coins==coins&&g.save.flags.Count==beforeFlags,
            "Clicking the map's remote follow button releases old pin without rewards or quest mutations");
        g.Enter(0);
        Check(g.save.room==0&&g.save.trackedQuest=="trail_circuits",
            "Original real region crossing is required; map button cannot teleport");
        if(g.HasLocalWaymark)
            Check(g.QuestApproachNavigationActive&&
                !g.QuestApproachTravelHandoffPending&&
                g.InspectLocalWaymark(g.LocalWaymark.Value)==WaymarkRouteStatus.Available,
                "Physical arrival creates the real quest walking approach");
        else Check(g.QuestApproachTravelHandoffPending,
            "Closed region approach remains pending rather than faking walking access");
        Check(g.save.version==2,"Map-triggered distant quest routing preserves old save schema");
        g.Close();
        Console.WriteLine("PASS Inspected remote map objective can explicitly start real regional quest route without stealing manual pins");
    }

    private static void RealOuterShoreNavigationFollowsActualTransitLegs(FrontierRuntime app)
    {
        Prepare(app);
        GameCore g=app.Game;
        g.save.flags.Add("intro");g.save.flags.Add("exp_intro");
        g.save.flags.Add("harbor_report");g.save.flags.Add("desert_report");
        g.save.stage=4;g.save.english=true;g.save.showGuidance=false;
        Check(g.CanEnter(5)&&g.CanEnter(6)&&g.CanEnter(7),
            "The actual three outer-shore regions are story-unlocked");
        for(int room=0;room<=6;room++)
            g.save.flags.Add("trail_circuit_"+room+"_3");
        g.save.flags.Remove("trail_circuit_7_1");
        g.save.flags.Remove("trail_circuit_7_2");
        g.save.flags.Remove("trail_circuit_7_3");
        int coins=g.save.coins,shards=g.save.shards;
        g.Enter(5);g.enemies.Clear();
        g.TrackQuest("trail_circuits");
        Check(g.GetQuest("trail_circuits").room==7,
            "The original multi-region expedition correctly points to Frostchime");
        WorldObject desert=null,home=null;
        foreach(WorldObject o in g.Map.objects)
        {
            if(o.kind=="portal6")desert=o;
            if(o.kind=="portal0")home=o;
        }
        Check(desert!=null&&home!=null,"Mistport has an eastbound desert crossing and a separate town return");
        Point? first=g.NavigationTarget();
        Check(first.HasValue&&(first.Value-new Point(desert.x,desert.y)).Length<.001f&&
            (first.Value-new Point(home.x,home.y)).Length>5f,
            "From Mistport to Frostchime, travel east via Glass Desert rather than returning to town");
        g.mode="journal";
        Check(g.PlaceQuestApproachWaymark("trail_circuits")&&
            g.QuestApproachTravelHandoffPending,
            "The real journal explicitly accepts the correct multihop quest");
        g.mode="world";
        Check(g.PlayerRequestedRemoteExitTarget(first.Value)&&
            g.RemoteQuestNextStop.Contains("GLASS DESERT"),
            "The physically authored intermediate crossing displays the true next stop");
        g.mode="world";GUI.commands.Clear();Call(app,"OnGUI");
        Check(GUI.commands.Exists(s=>s.Contains("NEXT: GLASS DESERT")),
            "The actual world minimap explains the multi-hop transfer to the player");
        g.practicing=true;
        g.enemies.Add(new Enemy(9157,0,13,14,180));
        Point? combatTarget=g.NavigationTarget();
        Check(combatTarget.HasValue&&
            !g.PlayerRequestedRemoteExitTarget(combatTarget.Value),
            "Real training combat takes over the navigation compass, not the pending quest portal");
        GUI.commands.Clear();Call(app,"DrawMinimap");
        Check(!GUI.commands.Exists(s=>s.Contains("NEXT: GLASS DESERT"))&&
            !GUI.commands.Exists(s=>s.Contains("REMOTE QUEST · AUTO-ROUTE"))&&
            g.QuestApproachTravelHandoffPending,
            "Production minimap prioritizes active combat without canceling the queued quest");
        g.practicing=false;g.enemies.Clear();
        GUI.commands.Clear();Call(app,"DrawMinimap");
        Check(GUI.commands.Exists(s=>s.Contains("NEXT: GLASS DESERT")),
            "Distant journey reappears when the real higher-priority combat target ends");
        g.mode="map";GUI.commands.Clear();Call(app,"OnGUI");
        Check(GUI.commands.Exists(s=>s.Contains("NEXT: GLASS DESERT")),
            "The full world map shows the same physical next transfer");
        g.Enter(6);g.enemies.Clear();
        WorldObject frost=null;
        foreach(WorldObject o in g.Map.objects)
            if(o.kind=="portal7"){frost=o;break;}
        Check(frost!=null,"The actual Glass Desert map has a Frostchime portal");
        Point? second=g.NavigationTarget();
        Check(second.HasValue&&(second.Value-new Point(frost.x,frost.y)).Length<.001f&&
            g.QuestApproachTravelHandoffPending&&
            g.RemoteQuestNextStop.Contains("FROSTCHIME"),
            "On reaching the intermediate region, guidance names the newly reachable Frostchime crossing");
        g.Enter(7);g.enemies.Clear();
        Check(g.save.room==7&&
            (g.QuestApproachNavigationActive||g.QuestApproachTravelHandoffPending),
            "Real transition to Frostchime either validates a walking approach or explicitly awaits reopening");
        // The reverse journey has no direct Frostchime-to-Desert crossing.
        // Finish the current fieldwork in the test fixture and require
        // the AUTHORED Mistport return leg rather than town detours.
        g.mode="map";g.ClearLocalWaymark();
        g.save.flags.Add("trail_circuit_7_3");
        g.save.flags.Remove("trail_circuit_6_3");
        g.TrackQuest("trail_circuits");g.mode="world";
        Check(g.GetQuest("trail_circuits").room==6,
            "The same original survey now needs a real Desert stage");
        WorldObject port=null,returnTown=null;
        foreach(WorldObject o in g.Map.objects)
        {
            if(o.kind=="portal5")port=o;
            if(o.kind=="portal0")returnTown=o;
        }
        Check(port!=null&&returnTown!=null,"Frostchime has a Mistport crossing and a distinct town return");
        Point? reverse=g.NavigationTarget();
        Check(reverse.HasValue&&
            (reverse.Value-new Point(port.x,port.y)).Length<.001f&&
            (reverse.Value-new Point(returnTown.x,returnTown.y)).Length>5f,
            "From Frostchime to Desert, navigate via Mistport, not a loop through town");
        g.Enter(5);g.enemies.Clear();
        Point? finalLeg=g.NavigationTarget();
        Check(finalLeg.HasValue&&
            (finalLeg.Value-new Point(desert.x,desert.y)).Length<.001f,
            "The return transit uses Mistport's REAL Desert portal");
        g.Enter(0);g.enemies.Clear();
        WorldObject envoy=null;
        foreach(WorldObject o in g.Map.objects)if(o.kind=="envoy"){envoy=o;break;}
        Check(envoy!=null,"The shipped Emberfall scene includes the actual courier Lune");
        Point? fromTown=g.NavigationTarget();
        Check(fromTown.HasValue&&
            (fromTown.Value-new Point(envoy.x,envoy.y)).Length<.001f,
            "Long-distance outer-shore travel from Emberfall leads to its REAL courier contact");
        g.mode="journal";
        Check(g.PlaceQuestApproachWaymark("trail_circuits")&&
            g.RemoteQuestNextStop.Contains("SPEAK WITH LUNE"),
            "From town an explicitly followed outer-shore quest tells the player to speak with its real courier");
        g.save.english=false;
        Check(g.RemoteQuestNextStop.Contains("露恩"),
            "Chinese travel instructions name the same original character and crossing");
        Check(g.save.version==2&&g.save.coins==coins&&g.save.shards==shards,
            "Multi-hop navigation changes neither rewards nor SaveData structure");
        g.Close();
        Console.WriteLine("PASS Real Mistport/Desert/Frostchime two-way quest itinerary and town courier handoff without teleport or save mutation");
    }

    private static void WorldAtlasTrackAndFollowIsExplicitAndAtomic(FrontierRuntime app)
    {
        Prepare(app);
        GameCore g=app.Game;
        g.save.flags.Add("intro");g.save.flags.Add("scout_saved");
        g.save.stage=4;g.save.english=true;g.Enter(9);g.enemies.Clear();
        g.mode="map";
        Check(g.PlaceLocalWaymark(g.Position)&&g.HasLocalWaymark,
            "A real player-authored pin exists before atlas mission selection");
        Point prior=g.LocalWaymark.Value;
        string originalQuest=g.save.trackedQuest;
        Set(app,"selectedRegion",0);
        Set(app,"mapInspectedQuestId","trail_circuits");
        Check(!g.GetQuest("trail_circuits").locked&&
            !g.GetQuest("trail_circuits").complete&&
            g.GetQuest("trail_circuits").room==0&&
            g.save.trackedQuest!="trail_circuits",
            "The shipped survey is available but not previously tracked");
        int beforeCoins=g.save.coins,flags=g.save.flags.Count;
        GUI.commands.Clear();GUI.buttonCalls.Clear();Call(app,"OnGUI");
        Check(GUI.commands.Exists(s=>s.Contains("TRACK & FOLLOW INSPECTED QUEST"))&&
            g.HasLocalWaymark&&
            (g.LocalWaymark.Value-prior).Length<.001f&&
            g.save.trackedQuest==originalQuest,
            "Browsing the map offers a combined action without mutating quest or manual pin");
        Check(g.GetQuest("frost_signal").locked,
            "The unearned Frost Signal mission is genuinely locked in this fixture");
        Check(!g.TrackAndFollowRemoteQuest("not_an_authored_quest")&&
            !g.TrackAndFollowRemoteQuest("frost_signal")&&
            g.save.trackedQuest==originalQuest&&g.HasLocalWaymark,
            "Invalid or locked missions cannot silently take map pin ownership");
        GUI.queuedClick=new Rect(888,571,306,35);
        Call(app,"OnGUI");GUI.queuedClick=null;
        Check(g.save.trackedQuest=="trail_circuits"&&
            g.QuestApproachTravelHandoffPending&&!g.HasLocalWaymark&&
            g.save.flags.Count==flags&&g.save.coins==beforeCoins&&
            g.save.version==2,
            "One actual map click authenticates mission tracking and its real transit without loot or save migration");
        GUI.commands.Clear();Call(app,"OnGUI");
        Check(GUI.commands.Exists(s=>s.Contains("Cancel pending remote quest route")),
            "After combined acceptance the same atlas exposes explicit route cancellation");
        g.mode="world";g.Enter(0);
        Check(g.save.room==0&&g.save.trackedQuest=="trail_circuits",
            "Original real region travel remains necessary after map acceptance");
        if(g.HasLocalWaymark)
            Check(g.QuestApproachNavigationActive&&
                g.InspectLocalWaymark(g.LocalWaymark.Value)==WaymarkRouteStatus.Available,
                "Arrival produces an actual verified objective approach");
        else Check(g.QuestApproachTravelHandoffPending,
            "A closed approach retains only pending intent without fake walking access");
        g.Close();
        Console.WriteLine("PASS One-click world atlas track-and-follow accepts only real unlocked remote quests, preserves browsing, save and physical travel");
    }

    private static void NearbyNpcHudMatchesRealKeyboardInteraction(FrontierRuntime app)
    {
        Prepare(app);
        var g=app.Game;g.mode="world";g.save.room=0;
        g.save.english=false;g.save.x=14;g.save.y=11;
        g.enemies.Clear();g.projectiles.Clear();g.hazards.Clear();
        Set(app,"camera",g.Position);
        WorldObject archivist=null,witness=null;
        foreach(WorldObject o in g.Map.objects)
        {
            if(o.kind=="archivekeeper")archivist=o;
            if(o.kind=="commonswitness")witness=o;
        }
        Check(archivist!=null&&witness!=null&&g.CanStand(g.Position),
            "The actual guild contains adjacent talking actors on playable footing");
        Mouse(app,g.Position+new Point(4,4));
        Input.Clear();Call(app,"Update");
        GUI.commands.Clear();Call(app,"OnGUI");
        Check(GUI.commands.Exists(x=>x.Contains("E  "+archivist.name)),
            "World E prompt uses the actual archivist speaker under forward aim");
        Input.keys.Add(KeyCode.E);Update(app);
        Check(g.conversation!=null&&g.conversation.speaker==archivist.name &&
            g.conversationSource==archivist,
            "Real keyboard E selects exactly the speaker advertised above the actor");
        g.Close();Input.Clear();
        Mouse(app,g.Position+new Point(-4,-4));
        Update(app);GUI.commands.Clear();Call(app,"OnGUI");
        Check(GUI.commands.Exists(x=>x.Contains("E  "+witness.name)),
            "Turning to the adjacent witness updates the actual world prompt");
        Input.keys.Add(KeyCode.E);Update(app);
        Check(g.conversation!=null&&g.conversation.speaker==witness.name &&
            g.conversationSource==witness,
            "Real input switches to the visibly targeted witness rather than reopening another NPC");
        Console.WriteLine("PASS Actual NPC E prompt and keyboard dialogue retain exact speaker identity");
    }

    private static void LiveFrostCompanionEmberScreenIsVisibleInCombatHud(FrontierRuntime app)
    {
        Prepare(app);
        var g=app.Game;g.save.english=true;g.save.room=7;
        g.save.stage=3;g.mode="world";
        g.save.flags.Add("desert_report");g.save.flags.Add("boss_7");
        g.save.flags.Add("companion_1");g.save.companion=1;
        g.enemies.Clear();g.hazards.Clear();g.projectiles.Clear();
        WorldObject junction=null;
        foreach(WorldObject post in g.Map.objects)
            if(post.id=="frostsignal_2")junction=post;
        Check(junction!=null,
            "The shipped Frostchime scene contains the physical mid-route flame station");
        g.save.x=junction.x;g.save.y=junction.y;
        g.companionPosition=new Point(junction.x+1.5f,junction.y);
        var guard=new Enemy(91400,0,junction.x+3f,junction.y,95f)
            {stun=90f,timer=90f};
        g.enemies.Add(guard);
        typeof(GameCore).GetField("<FrostSignalStage>k__BackingField",Private)
            .SetValue(g,2);
        typeof(GameCore).GetField("<FrostSignalTime>k__BackingField",Private)
            .SetValue(g,81f);
        typeof(GameCore).GetField("<FrostSignalFlame>k__BackingField",Private)
            .SetValue(g,72f);
        Check(g.CanStand(g.Position)&&g.CanStand(g.companionPosition)&&
            g.FrostSignalGuardsScreened==1&&
            g.Conditions().Contains("ALLY SCREENING 1"),
            "Live recruited body physically protects a real guard lane on authored ground");
        Set(app,"camera",g.Position);
        GUI.commands.Clear();Call(app,"OnGUI");
        Check(GUI.commands.Exists(x=>x.Contains("FROSTCHIME EMBER"))&&
            GUI.commands.Exists(x=>x.Contains("ALLY SCREENING 1"))&&
            GUI.commands.Exists(x=>x.Contains("Frostchime")),
            "The actual GameView combat panel shows current ember and companion guard state");
        g.companionPosition=new Point(junction.x,junction.y+2f);
        Check(g.FrostSignalGuardsScreened==0,
            "Physically leaving the guard approach removes protection immediately");
        GUI.commands.Clear();Call(app,"OnGUI");
        Check(GUI.commands.Exists(x=>x.Contains("PLACE ALLY BETWEEN GUARD & EMBER")),
            "The same live combat HUD explains where to place an unshielding companion");
        g.save.english=false;g.companionPosition=new Point(junction.x+1.5f,junction.y);
        GUI.commands.Clear();Call(app,"OnGUI");
        Check(GUI.commands.Exists(x=>x.Contains("霜钟余火护送"))&&
            GUI.commands.Exists(x=>x.Contains("同伴挡风 1")),
            "The shipped battle HUD includes readable Chinese companion screen feedback");
        Console.WriteLine("PASS Physical companion fire screen stays visible in real bilingual Frostchime combat");
    }


    private static void BrightHighlandServiceHasHonestWorldVisual(FrontierRuntime app)
    {
        Prepare(app);
        var g=app.Game;g.save.english=true;g.save.room=7;g.mode="world";
        g.save.flags.Add("desert_report");g.save.flags.Add("boss_7");
        g.save.flags.Add("frostsignal_clinic");
        g.save.flags.Add("frostsignal_complete");
        g.save.flags.Remove("frostsignal_bright");
        g.enemies.Clear();g.hazards.Clear();g.projectiles.Clear();
        WorldObject hearth=null;
        foreach(WorldObject o in g.Map.objects)
            if(o.kind=="frostsignal"&&o.id=="frostsignal_3")hearth=o;
        Check(hearth!=null,"Original authored south public hearth remains present");
        g.save.x=hearth.x;g.save.y=hearth.y;
        Set(app,"camera",g.Position);
        Check(g.FrostSignalComplete&&!g.FrostSignalHearthBright,
            "Existing dim completed v2 fire is a valid permanent service");
        GUI.commands.Clear();Call(app,"OnGUI");
        Check(GUI.commands.Exists(x=>x.Contains("PUBLIC FIRE RESTORED")) &&
            !GUI.commands.Exists(x=>x.Contains("STEADY PUBLIC FIRE")),
            "Actual production world prop does not misrepresent a dim older hearth as upgraded");
        g.save.flags.Add("frostsignal_bright");
        Check(g.FrostSignalHearthBright,"Honest saved quality flag upgrades the actual civic service");
        GUI.commands.Clear();Call(app,"OnGUI");
        Check(GUI.commands.Exists(x=>x.Contains("STEADY PUBLIC FIRE")) &&
            !GUI.commands.Exists(x=>x.Contains("PUBLIC FIRE RESTORED")),
            "Production world prop shows the real stronger public hearth after permanent upgrade");
        g.save.english=false;
        GUI.commands.Clear();Call(app,"OnGUI");
        Check(GUI.commands.Exists(x=>x.Contains("公共暖站火势稳定")),
            "Actual Chinese GameView overlay distinguishes steady fire too");
        g.save.flags.Remove("frostsignal_bright");
        GUI.commands.Clear();Call(app,"OnGUI");
        Check(GUI.commands.Exists(x=>x.Contains("公共暖站已重燃")) &&
            !GUI.commands.Exists(x=>x.Contains("公共暖站火势稳定")),
            "Presentation follows the authoritative saved outcome instead of caching a false upgrade");
        Console.WriteLine("PASS Physical public hearth art cue tracks actual service upgrade in bilingual production rendering");
    }
    private static void BankedFrostEmberMovesWorldIndicatorToHearth(FrontierRuntime app)
    {
        Prepare(app);
        var g=app.Game;g.save.english=true;g.save.room=7;g.mode="world";
        // The actual highland public quest only unlocks after the desert
        // report and its existing guardian: otherwise GetQuest correctly
        // displays the locked objective instead of the active handoff.
        g.save.flags.Add("desert_report");
        g.save.flags.Add("boss_7");
        g.enemies.Clear();g.hazards.Clear();g.projectiles.Clear();
        WorldObject hearth=null;
        foreach(WorldObject o in g.Map.objects)
            if(o.kind=="frostsignal"&&o.id=="frostsignal_3")
                hearth=o;
        Check(hearth!=null,"Shipped scene has its real southern civic hearth");
        g.save.x=hearth.x;g.save.y=hearth.y;
        Check(g.CanStand(g.Position),"Civic hearth is reachable real ground");
        typeof(GameCore).GetField("<FrostSignalStage>k__BackingField",Private)
            .SetValue(g,3);
        typeof(GameCore).GetField("<FrostSignalTime>k__BackingField",Private)
            .SetValue(g,.04f);
        typeof(GameCore).GetField("<FrostSignalFlame>k__BackingField",Private)
            .SetValue(g,54f);
        Set(app,"camera",g.Position);
        GUI.commands.Clear();Call(app,"OnGUI");
        Check(GUI.commands.Exists(s=>s.Contains("CIVIC EMBER")),
            "Production render uses a timed carried-ember indicator before arrival");
        CallCore(g,"TickFrostSignal",.05f);
        Check(g.FrostSignalAtHearth&&!g.FrostSignalComplete&&
            (g.FrostSignalBankedFoot-g.Position).Length<.001f,
            "The genuine stage-three update captures the visible hearth position without awarding service");
        var q=g.GetQuest("frost_signal");
        Check(q.steps.Count>=4&&q.steps[2].done&&!q.steps[3].done&&
            q.next.Contains("Ember safely deposited")&&
            (q.target-g.Position).Length<.001f,
            "Real journal shows physical delivery complete and choice still pending");
        GUI.commands.Clear();Call(app,"OnGUI");
        Check(GUI.commands.Exists(s=>s.Contains("HEARTH SECURED"))&&
            !GUI.commands.Exists(s=>s.Contains("CIVIC EMBER")),
            "The actual GameView overlay changes from ticking flame to safe hearth");
        Console.WriteLine("PASS Live frost ember joins the civic hearth with accurate task and world cues");
    }


    private static void LiveMinecartBodyguardIsVisibleInRealWorldHud(FrontierRuntime app)
    {
        Prepare(app);
        var g=app.Game;g.mode="world";g.save.english=true;
        g.save.stage=3;g.save.room=3;
        g.save.flags.Add("boss_3");g.save.flags.Add("gate_3");
        g.save.flags.Remove("mineconvoy_complete");
        g.enemies.Clear();g.hazards.Clear();g.projectiles.Clear();
        WorldObject junction=null;
        foreach(WorldObject site in g.Map.objects)
            if(site.id=="mineconvoy_2")junction=site;
        Check(junction!=null,"Ironreach still contains the actual contested rail junction");
        Point cart=new Point(junction.x,junction.y);
        g.save.x=cart.x+1.25f;g.save.y=cart.y;
        var raider=new Enemy(91300,0,cart.x+3f,cart.y,95f)
            {stun=100f,timer=100f};
        Check(g.CanStand(cart)&&g.CanStand(g.Position)&&
            g.CanStand(raider.Position),
            "Hero, original cart station and real sabotage lane fit authored Ironreach terrain");
        g.enemies.Add(raider);
        typeof(GameCore).GetField("<MineConvoyStage>k__BackingField",Private).SetValue(g,2);
        typeof(GameCore).GetField("<MineCartPosition>k__BackingField",Private).SetValue(g,cart);
        typeof(GameCore).GetField("<MineConvoyIntegrity>k__BackingField",Private).SetValue(g,74f);
        typeof(GameCore).GetField("<MineConvoyTime>k__BackingField",Private).SetValue(g,88f);
        Check(g.MineConvoyBodyguarding && g.Conditions().Contains("INTERCEPTING"),
            "Actual staged game core recognizes a real playable bodyguard position");
        Set(app,"camera",cart);
        GUI.commands.Clear();Call(app,"OnGUI");
        Check(GUI.commands.Exists(s=>s.Contains("MINECART ESCORT")) &&
            GUI.commands.Exists(s=>s.Contains("INTERCEPTING")) &&
            GUI.commands.Exists(s=>s.Contains("The Miners' Last Shift")),
            "Actual GameView draws emergency cart protection in its HUD and quest guidance");
        g.save.y=cart.y+1.5f;
        Check(!g.MineConvoyBodyguarding,"Walking outside the real impact lane ends interception");
        GUI.commands.Clear();Call(app,"OnGUI");
        Check(GUI.commands.Exists(s=>s.Contains("STAND BETWEEN RAIDER AND CART")) &&
            GUI.commands.Exists(s=>s.Contains("The Miners' Last Shift")),
            "The production HUD reacts immediately when the hero leaves the defensive lane");
        g.save.english=false;
        GUI.commands.Clear();Call(app,"OnGUI");
        Check(GUI.commands.Exists(s=>s.Contains("矿工护送")) &&
            GUI.commands.Exists(s=>s.Contains("挡在袭击者与矿车之间")),
            "The same shipped world screen provides real Chinese tactical feedback");
        Console.WriteLine("PASS Live minecart positional bodyguard and bilingual tactical overlay");
    }


    private static void LiveTideSalvageCombatHudPreservesMissionAndThreat(FrontierRuntime app)
    {
        Prepare(app);
        var g=app.Game;g.mode="world";g.save.english=true;
        g.save.room=5;g.save.stage=3;
        g.save.flags.Add("exp_intro");g.save.flags.Add("harbor_report");
        g.enemies.Clear();g.hazards.Clear();g.projectiles.Clear();
        WorldObject second=null;
        foreach(WorldObject o in g.Map.objects)
            if(o.kind=="tidecargo2")second=o;
        Check(second!=null,"The actual harbor map contains the second manifest");
        g.save.x=second.x;g.save.y=second.y;
        typeof(GameCore).GetField("<TideSalvagePhase>k__BackingField",Private)
            .SetValue(g,3);
        typeof(GameCore).GetField("<TideSalvageTime>k__BackingField",Private)
            .SetValue(g,71f);
        g.save.trackedQuest="main";
        var raider=new Enemy(89924,0,second.x+2f,second.y+1f,85f);
        g.enemies.Add(raider);
        Check(g.TideSalvageActive&&g.TideSalvageRaidersAlive==1&&
            g.GetQuest("tide_salvage").room==5,
            "Actual field quest exposes a living authored raider");
        Set(app,"camera",g.Position);
        GUI.commands.Clear();Call(app,"OnGUI");
        Check(GUI.commands.Exists(s=>s.Contains("TIDEBOUND SALVAGE"))&&
            GUI.commands.Exists(s=>s.Contains("RAIDERS LEFT 1")),
            "Actual combat mode shows timed cargo progress and the physical remaining attacker");
        g.save.english=false;
        GUI.commands.Clear();Call(app,"OnGUI");
        Check(GUI.commands.Exists(s=>s.Contains("潮滩打捞"))&&
            GUI.commands.Exists(s=>s.Contains("追兵剩余 1")),
            "Chinese combat HUD does not erase the live salvage objective");
        g.enemies.Remove(raider);
        typeof(GameCore).GetField("<TideSalvagePhase>k__BackingField",Private)
            .SetValue(g,4);
        g.save.english=true;
        GUI.commands.Clear();Call(app,"OnGUI");
        Check(!GUI.commands.Exists(s=>s.Contains("CARGO SECURED")),
            "A vanished raider without real defeat never displays a false earned cargo handoff");
        Console.WriteLine("PASS Live tidebound combat overlay retains honest cargo, tide and enemy state");
    }

    private static void MineConvoySafeArrivalHoldsUntilPhysicalHandoff(FrontierRuntime app)
    {
        Prepare(app);
        var g=app.Game;g.save.english=true;g.save.room=3;g.mode="world";
        g.enemies.Clear();g.hazards.Clear();g.projectiles.Clear();
        WorldObject shelter=null;
        foreach(WorldObject o in g.Map.objects)
            if(o.kind=="mineconvoy"&&o.id=="mineconvoy_3")
                shelter=o;
        Check(shelter!=null,
            "The playable Ironreach scene contains its authored physical depot");
        Point safe=new Point(shelter.x,shelter.y);
        Check(g.CanStand(safe),"The delivered cart rests on actual solid route ground");
        g.save.flags.Remove("mineconvoy_complete");
        g.save.flags.Remove("mineconvoy_braced");
        g.save.x=58.5f;g.save.y=35.5f;
        typeof(GameCore).GetField("<MineConvoyStage>k__BackingField",Private)
            .SetValue(g,3);
        typeof(GameCore).GetField("<MineCartPosition>k__BackingField",Private)
            .SetValue(g,safe);
        typeof(GameCore).GetField("<MineConvoyTime>k__BackingField",Private)
            .SetValue(g,.6f);
        typeof(GameCore).GetField("<MineConvoyIntegrity>k__BackingField",Private)
            .SetValue(g,66f);
        typeof(GameCore).GetField("mineConvoySteam",Private).SetValue(g,10f);
        // This is the actual time-step transition, not a forged stage-4
        // reward. The cart was driven to the authored depot before this tick.
        CallCore(g,"TickMineConvoy",.05f);
        Check(g.MineConvoyStage==4&&g.MineConvoyTime>0,
            "Genuine safe arrival transitions into an awaiting-handoff stage");
        for(int t=0;t<160;t++)
            CallCore(g,"TickMineConvoy",.25f);
        Check(g.MineConvoyStage==4&&g.MineConvoyTime>0&&
            g.MineConvoyIntegrity==66f&&!g.MineConvoyComplete,
            "Workers remain safe after the journey deadline, with no automatic reward");
        Set(app,"camera",g.MineCartPosition);
        GUI.commands.Clear();Call(app,"OnGUI");
        Check(GUI.commands.Exists(s=>s.Contains("CART ARRIVED")) &&
            !GUI.commands.Exists(s=>s.Contains("CART ESCORT")),
            "Actual world cart overlay shows secure arrival rather than an expiring stage-four clock");
        QuestGuide handoff=g.GetQuest("mine_convoy");
        Check(handoff.steps.Count>=4&&handoff.steps[2].done&&
            !handoff.steps[3].done &&
            handoff.next.Contains("Cart safely delivered!")&&
            !handoff.next.Contains("s · Hull") &&
            g.NavigationTarget().HasValue &&
            (g.NavigationTarget().Value-safe).Length<.001f,
            "Production quest and active compass distinguish secure arrival from physical paperwork");
        CallCore(g,"ChooseMineConvoy","mineconvoy_finish");
        Check(g.MineConvoyStage==4&&!g.MineConvoyComplete,
            "Remote handoff never completes or grants the protected rescue");
        g.save.x=safe.x;g.save.y=safe.y;
        float before=g.save.coins;
        CallCore(g,"ChooseMineConvoy","mineconvoy_finish");
        Check(g.MineConvoyComplete&&g.MineConvoyStage==0&&
            g.save.coins>=before+60,
            "A physical depot interaction finally registers earned worker safety");
        QuestGuide completed=g.GetQuest("mine_convoy");
        Check(completed.complete&&completed.steps.Count>=4&&
            completed.steps[2].done&&completed.steps[3].done,
            "The permanent completed quest records arrival and handoff without a new save field");
        float paid=g.save.coins;
        CallCore(g,"ChooseMineConvoy","mineconvoy_finish");
        Check(g.save.coins==paid,
            "Previously registered rescue cannot pay its reward again");
        Console.WriteLine("PASS Physical minecart arrival preserves earned safe handoff without remote or repeat reward");
    }

    private static void StewCooldownAndAttackBuffAreVisibleInActualBilingualHud(FrontierRuntime app)
    {
        Prepare(app);
        var g=app.Game;
        g.mode="world";g.save.english=true;g.save.inventory[6]=3;
        g.save.health=10;g.save.energy=20;
        int start=g.save.inventory[6];
        g.Eat();
        Check(g.save.inventory[6]==start-1&&!g.CanEatStew&&
            g.StewCooldownLeft>=2.99f,
            "Real food use starts one recovery and cannot spend another portion");
        GUI.commands.Clear();Call(app,"OnGUI");
        Check(GUI.commands.Exists(s=>s.Contains("STEW ATTACK BOOST")&&s.Contains("90s")) &&
            GUI.commands.Exists(s=>s.Contains("3s")),
            "Real English exploration HUD shows both live temporary damage buff and food recovery");
        g.mode="inventory";
        GUI.commands.Clear();Call(app,"OnGUI");
        Check(GUI.commands.Exists(s=>s.Contains("Recovering 3s")) &&
            GUI.commands.Exists(s=>s.Contains("Attack boost left 90s")),
            "Inventory independently exposes its real disabled cooldown and remaining damage buff");
        g.mode="world";g.save.english=false;
        GUI.commands.Clear();Call(app,"OnGUI");
        Check(GUI.commands.Exists(s=>s.Contains("炖汤增伤")&&s.Contains("90s")),
            "Chinese world HUD shows the same simulation-owned timed benefit");
        g.Eat();
        Check(g.save.inventory[6]==start-1,
            "Pressing F again through the real-world control cannot spend extra food");
        Console.WriteLine("PASS Real bilingual HUD/inventory convey three-second stew recovery and attack buff without extra consumption");
    }

    private static void CompanionBlockedRouteCueIsWorldBoundAndBilingual(FrontierRuntime app)
    {
        Prepare(app);
        var g=app.Game;
        g.mode="world";g.enemies.Clear();
        g.save.flags.Add("companion_0");g.SelectCompanion(0);
        Check(g.save.companion==0,"The real world view uses an authentic recruited companion");
        g.companionPosition=new Point(g.save.x+3f,g.save.y);
        Set(app,"camera",g.companionPosition);
        g.save.english=true;
        GUI.commands.Clear();Call(app,"OnGUI");
        Check(!GUI.commands.Exists(s=>s.Contains("ROUTE BLOCKED")),
            "A perfectly ordinary following companion has no fake route-blocked warning");
        var flags=System.Reflection.BindingFlags.NonPublic|
            System.Reflection.BindingFlags.Instance;
        Action<string,object> set=(key,value)=>
            typeof(GameCore).GetField(key,flags).SetValue(g,value);
        set("companionBlockedRouteSave",g.save);
        set("companionBlockedRouteMap",g.Map);
        set("companionBlockedRouteRoom",g.save.room);
        set("companionBlockedRouteGeneration",g.companionPresentationGeneration);
        set("companionBlockedRouteRetry",.55f);
        set("companionUnreachableGoal",true);
        Check(g.CompanionRouteBlocked,
            "The real-world warning binds only to the currently saved owner, map and physical companion");
        GUI.commands.Clear();Call(app,"OnGUI");
        Check(GUI.commands.Exists(s=>s.Contains("ROUTE BLOCKED")&&s.Contains("REROUTE")),
            "The actual production companion renderer draws the earned English route obstruction cue");
        g.save.english=false;
        GUI.commands.Clear();Call(app,"OnGUI");
        Check(GUI.commands.Exists(s=>s.Contains("前方道路不通")&&s.Contains("请绕行")),
            "The same real world companion has a readable Chinese instruction");
        set("companionUnreachableGoal",false);
        GUI.commands.Clear();Call(app,"OnGUI");
        Check(!GUI.commands.Exists(s=>s.Contains("前方道路不通")),
            "The world cue immediately disappears when the real route opens again");
        Console.WriteLine("PASS Bilingual in-world companion obstruction cue tracks the actual current route solver");
    }

    private static void PublishedCivicFieldAidHasAnHonestWorldMarker(FrontierRuntime app)
    {
        Prepare(app);
        var g=app.Game;g.mode="world";g.save.english=true;
        // The visual review starts in region ONE by default; actual civilian
        // first-aid is earned in regions 0, 3 and 5. Enter the real Canal
        // rather than falsely expecting a region-one marker to show it.
        g.Enter(0);g.enemies.Clear();
        WorldObject marker=Array.Find(g.Map.objects,o=>o.kind=="waytrail");
        Check(marker!=null,"Production world contains original physical surveyed trail");
        g.save.x=marker.x;g.save.y=marker.y;
        Set(app,"camera",new Point(marker.x,marker.y));
        GUI.commands.Clear();Call(app,"OnGUI");
        Check(!GUI.commands.Exists(s=>s.Contains("FIELD KIT")),
            "An unaudited region never shows an invented staffed treatment site");
        g.save.flags.Add("civic_aftercare_waytrail_0");
        GUI.commands.Clear();Call(app,"OnGUI");
        Check(!GUI.commands.Exists(s=>s.Contains("FIELD KIT")),
            "Unpublished audit evidence does not fabricate a public field service");
        g.save.flags.Add("civic_aftercare_published");
        GUI.commands.Clear();Call(app,"OnGUI");
        Check(GUI.commands.Exists(s=>s.Contains("FIELD KIT")&&s.Contains("1 HERB")),
            "The actual Canal world view shows the new field kit on its earned authored marker");
        g.save.english=false;
        GUI.commands.Clear();Call(app,"OnGUI");
        Check(GUI.commands.Exists(s=>s.Contains("居民急救")&&s.Contains("药草1")),
            "Live Chinese GameView displays the same original marker's real service cost");
        g.save.flags.Remove("civic_aftercare_waytrail_0");
        GUI.commands.Clear();Call(app,"OnGUI");
        Check(!GUI.commands.Exists(s=>s.Contains("居民急救")),
            "Revoking local evidence retires the treatment marker without hiding the old road survey art");
        Console.WriteLine("PASS Published civic first-aid world marker is localized, bilingual and never appears before honest authorization");
    }

    private static void LiveCourierMedicineQualityShowsWithoutCoveringWard(FrontierRuntime app)
    {
        Prepare(app);
        var g=app.Game;g.mode="world";g.save.english=true;
        try
        {
            typeof(GameCore).GetField("courierRoom",Private).SetValue(g,g.save.room);
            typeof(GameCore).GetField("courierIntegrity",Private).SetValue(g,100f);
            Set(app,"camera",g.Position);
            GUI.commands.Clear();Call(app,"OnGUI");
            Check(GUI.commands.Exists(s=>s.Contains("MEDICINE INTACT")&&s.Contains("+10"))&&
                GUI.commands.Exists(s=>s.Contains("PARCEL 100%")),
                "The real world GameView exposes the earned intact-medicine threshold alongside cargo integrity");
            typeof(GameCore).GetField("courierIntegrity",Private).SetValue(g,89f);
            GUI.commands.Clear();Call(app,"OnGUI");
            Check(GUI.commands.Exists(s=>s.Contains("MEDICINE DAMAGED"))&&
                !GUI.commands.Exists(s=>s.Contains("MEDICINE INTACT")),
                "Crossing below the true 90-percent threshold retires the promise immediately");
            typeof(GameCore).GetField("courierGuardLeft",Private).SetValue(g,6f);
            GUI.commands.Clear();Call(app,"OnGUI");
            Check(GUI.commands.Exists(s=>s.Contains("WARD 6s"))&&
                !GUI.commands.Exists(s=>s.Contains("MEDICINE DAMAGED")),
                "The actively protecting ward clock owns its one visual row without overlapping cargo condition");
            typeof(GameCore).GetField("courierGuardLeft",Private).SetValue(g,0f);
            typeof(GameCore).GetField("courierIntegrity",Private).SetValue(g,100f);
            g.save.english=false;
            GUI.commands.Clear();Call(app,"OnGUI");
            Check(GUI.commands.Exists(s=>s.Contains("药品完好")&&s.Contains("+10")),
                "The actual carried condition is readable in the Chinese production GameView");
            Console.WriteLine("PASS Live bilingual courier medicine quality and ward overlay share one honest production row");
        }
        finally
        {
            CallCore(g,"ResetFarCourier");
        }
    }

    private static void LiveCourierAmbushNeverSpawnsOnCompanion(FrontierRuntime app)
    {
        Prepare(app);
        var g=app.Game;g.save.english=true;g.save.room=6;g.mode="world";
        g.enemies.Clear();g.hazards.Clear();g.projectiles.Clear();
        MapData map=g.Map;string[] originalRows=map.rows;
        WorldObject[] oldObjects=map.objects;Decoration[] oldDecor=map.decor;
        try
        {
            string[] rows=(string[])originalRows.Clone();
            for(int y=49;y<=64;y++)
            {
                char[] line=rows[y].ToCharArray();
                for(int x=106;x<=117;x++)line[x]='.';
                rows[y]=new string(line);
            }
            map.rows=rows;map.decor=new Decoration[0];
            map.objects=new WorldObject[0];
            g.save.x=111.5f;g.save.y=53.5f;
            g.save.companion=0;
            g.companionPosition=new Point(111.5f,57.5f);
            Check(g.CanStand(g.Position)&&g.CanStand(g.companionPosition),
                "Hero and living companion occupy genuine passable bridge ground");
            // Invoke the exact live branch used at the real bridge crossing.
            typeof(GameCore).GetField("courierRoom",Private).SetValue(g,6);
            typeof(GameCore).GetField("courierRaided",Private).SetValue(g,false);
            CallCore(g,"SpawnFarCourierRaiders");
            Check(g.FarCourierRaidersAlive==2,
                "The actual crossing deploys both physical courier enemies");
            Enemy first=null,second=null;
            foreach(Enemy e in g.enemies)
            {
                if(e.id==90112)first=e;
                if(e.id==90113)second=e;
            }
            Check(first!=null&&second!=null,
                "Real crossing retains two distinct mission-owned enemy identities");
            Check((first.Position-g.companionPosition).Length>=1.25f &&
                (second.Position-g.companionPosition).Length>=1.25f &&
                (first.Position-second.Position).Length>=2.6f &&
                (first.Position-g.Position).Length>=2.2f &&
                (second.Position-g.Position).Length>=2.2f,
                "Neither combatant overlaps the live companion, hero, or other raider");
            Check(g.CanStand(first.Position)&&g.CanStand(second.Position) &&
                g.NavigationPathFrom(g.Position,first.Position,true).Count>0 &&
                g.NavigationPathFrom(g.Position,second.Position,true).Count>0,
                "Both raiders can actually pursue the carrier through real ground");
            Check(g.effects.Exists(e=>e.courierOwned&&e.kind=="danger") &&
                !g.FarCourierSecuredInFlight,
                "A real ambush carries authored danger cues and grants no unearned victory bonus");
            Console.WriteLine("PASS Live courier bridge ambush spawns two reachable enemies without overlapping the companion");
        }
        finally
        {
            CallCore(g,"ResetFarCourier");
            map.rows=originalRows;map.objects=oldObjects;map.decor=oldDecor;
        }
    }

    private static void CourierRaidersRequireReachableLiveSpawn(FrontierRuntime app)
    {
        Prepare(app);
        var g=app.Game;g.save.english=true;g.save.room=6;g.mode="world";
        g.enemies.Clear();g.hazards.Clear();g.projectiles.Clear();
        MapData map=g.Map;string[] originals=map.rows;
        WorldObject[] objects=map.objects;Decoration[] decor=map.decor;
        try
        {
            int bx=10,by=14,wall=bx+10;
            string[] rows=(string[])originals.Clone();
            for(int y=0;y<rows.Length;y++)
            {
                char[] cells=rows[y].ToCharArray();
                if(y>=by-6&&y<=by+6)
                    for(int x=bx;x<=bx+19;x++)cells[x]='.';
                cells[wall]='#';
                rows[y]=new string(cells);
            }
            map.rows=rows;map.decor=new Decoration[0];
            map.objects=new WorldObject[0];
            g.save.x=bx+5.5f;g.save.y=by+.5f;
            Point authored=new Point(bx+12.5f,by+.5f);
            Check(g.CanStand(authored)&&
                g.NavigationPathFrom(g.Position,authored,true).Count==0,
                "Authored courier ambush stands on legal but disconnected terrain");
            object[] args={authored,new Point(),false,new Point()};
            bool placed=(bool)CallCore(g,"FindCourierRaiderPosition",args);
            Point chosen=(Point)args[3];
            Check(placed&&chosen.x<wall &&
                g.CanStand(chosen)&&
                g.NavigationPathFrom(g.Position,chosen,true).Count>0,
                "Courier ambush picks a truly reachable local fallback, never the isolated authored cell");
            Point farSide=new Point(bx+18.5f,by+.5f);
            args=new object[]{farSide,new Point(),false,new Point()};
            placed=(bool)CallCore(g,"FindCourierRaiderPosition",args);
            Check(!placed,
                "A fully separated ambush with no reachable local fallback must not spawn a phantom raider");
            // Reopen the SAME geometry. The preferred authored location
            // must immediately become a legal combat position.
            char[] open=rows[by].ToCharArray();
            open[wall]='.';rows[by]=new string(open);
            args=new object[]{authored,new Point(),false,new Point()};
            placed=(bool)CallCore(g,"FindCourierRaiderPosition",args);
            chosen=(Point)args[3];
            Check(placed&&(chosen-authored).Length<.001f &&
                g.NavigationPathFrom(g.Position,authored,true).Count>0,
                "Opening the real corridor restores the exact authored ambush foot, without stale path cache");
            Console.WriteLine("PASS Courier ambush respects real connected ground and opens immediately after terrain repairs");
        }
        finally
        {
            map.rows=originals;map.objects=objects;map.decor=decor;
        }
    }

    private static void SealedEscortRouteRejectsFalseNearSideDestination(FrontierRuntime app)
    {
        Prepare(app);
        var g=app.Game;g.save.english=true;g.save.room=6;g.mode="world";
        g.enemies.Clear();g.hazards.Clear();g.projectiles.Clear();
        MapData map=g.Map;string[] originalRows=map.rows;
        WorldObject[] originalObjects=map.objects;
        Decoration[] originalDecor=map.decor;
        try
        {
            int bx=4,by=6,wall=bx+6;
            string[] rows=(string[])originalRows.Clone();
            // Seal the entire map-height divider: there is NO civilian
            // crossing. Make nearby footing on both sides unambiguous.
            for(int y=0;y<rows.Length;y++)
            {
                char[] row=rows[y].ToCharArray();
                for(int x=bx;x<=bx+13;x++)
                    if(y>=by-3&&y<=by+3&&x!=wall)row[x]='.';
                row[wall]='#';rows[y]=new string(row);
            }
            map.rows=rows;map.objects=new WorldObject[0];
            map.decor=new Decoration[0];
            Point start=new Point(bx+2.5f,by+.5f);
            g.save.x=wall+1.27f;g.save.y=by+.5f;
            g.reliefMissionRoom=6;g.reliefMissionWave=3;
            g.reliefMissionTime=90;g.reliefFollowerSafety=95;
            g.reliefFollowerWaiting=false;g.reliefFollowerPosition=start;
            g.reliefCivilianHitTime=0;
            Check(g.CanStand(start)&&g.CanStand(g.Position),
                "Resident and hero each stand on legitimate but separated ground");
            Check(g.NavigationPathFrom(start,g.Position).Count>0,
                "Legacy compass remains permitted to show nearest reachable frontier");
            Check(g.NavigationPathFrom(start,g.Position,true).Count==0,
                "Physical escort refuses the fake near-side route across an unbroken wall");
            for(int tick=0;tick<29;tick++)CallCore(g,"TickReliefMission",.05f);
            Check((g.reliefFollowerPosition-start).Length<.001f &&
                g.ReliefEscortRouteStalled && g.reliefMissionRoom==6 &&
                g.reliefMissionTime<90 &&
                (g.reliefFollowerPosition-g.Position).Length>1.3f,
                "No phantom wall-crossing, no false reunion and no paused rescue clock");
            Console.WriteLine("PASS Closed wall cannot masquerade as a completed civilian escort route");
        }
        finally
        {
            map.rows=originalRows;map.objects=originalObjects;
            map.decor=originalDecor;
        }
    }

    private static void CivilianEscortNavigatesRealWallDetour(FrontierRuntime app)
    {
        Prepare(app);
        var g=app.Game;g.save.english=true;g.save.room=6;g.mode="world";
        g.enemies.Clear();g.hazards.Clear();g.projectiles.Clear();
        MapData map=g.Map;string[] originalRows=map.rows;
        WorldObject[] originalObjects=map.objects;
        Decoration[] originalDecor=map.decor;
        try
        {
            int bx=4,by=6;
            string[] rows=(string[])originalRows.Clone();
            for(int y=by-4;y<=by+4;y++)
            {
                char[] row=rows[y].ToCharArray();
                for(int x=bx;x<=bx+15;x++)row[x]='.';
                rows[y]=new string(row);
            }
            // A real solid partition with a long detour at both ends.
            // Direct civilian->hero movement is impossible; the world graph
            // still contains a continuous, footprint-clear walking route.
            for(int y=by-2;y<=by+2;y++)
            {
                char[] row=rows[y].ToCharArray();
                row[bx+6]='#';rows[y]=new string(row);
            }
            map.rows=rows;map.decor=new Decoration[0];
            map.objects=new[]{new WorldObject{id="route_detour_station",
                kind="fieldstation6",x=bx+14.5f,y=by+.5f,
                name="Shelter",en="Shelter"}};
            Point start=new Point(bx+3.5f,by+.5f);
            g.reliefMissionRoom=6;g.reliefMissionWave=3;
            g.reliefMissionTime=100;g.reliefFollowerSafety=100;
            g.reliefFollowerWaiting=false;g.reliefFollowerPosition=start;
            g.reliefCivilianHitTime=0;
            g.save.x=bx+11.5f;g.save.y=by+.5f;
            Check(g.NavigationPathFrom(start,g.Position).Count>0,
                "Solid partition has a legal but nonstraight civilian route");
            float furthestSideStep=0;
            for(int tick=0;tick<270 &&
                (g.reliefFollowerPosition-g.Position).Length>1.4f;tick++)
            {
                CallCore(g,"TickReliefMission",.05f);
                furthestSideStep=Math.Max(furthestSideStep,
                    Math.Abs(g.reliefFollowerPosition.y-start.y));
                Check(g.CanStand(g.reliefFollowerPosition),
                    "Civilian remains on collision-valid ground throughout detour");
            }
            Check(furthestSideStep>2f &&
                g.reliefFollowerPosition.x>bx+7.5f &&
                (g.reliefFollowerPosition-g.Position).Length<1.6f &&
                g.reliefFollowerWalkDistance>(g.Position-start).Length &&
                g.reliefMissionRoom==6,
                "Escort walks around the real partition and actually rejoins the hero");
            Check(!g.ReliefEscortRouteStalled && g.reliefFollowerSafety>0 &&
                g.reliefMissionTime>0,
                "Reachable detour does not falsely fail a timed civilian rescue");
            Console.WriteLine("PASS Live civilian escort routes around real walls without teleporting or false stalls");
        }
        finally
        {
            map.rows=originalRows;map.objects=originalObjects;
            map.decor=originalDecor;
        }
    }

    private static void EscortDodgeChoosesFarSideOfLivingAttacker(FrontierRuntime app)
    {
        Prepare(app);
        var g=app.Game;g.save.english=true;g.save.room=6;g.mode="world";
        g.enemies.Clear();g.hazards.Clear();g.projectiles.Clear();
        MapData map=g.Map;string[] orig=map.rows;
        WorldObject[] objects=map.objects;
        Decoration[] decors=map.decor;
        try
        {
            int bx=4,by=6;string[] rows=(string[])orig.Clone();
            for(int y=by-2;y<=by+2;y++)
            {
                char[] row=rows[y].ToCharArray();
                for(int x=bx;x<=bx+9;x++)row[x]='.';
                rows[y]=new string(row);
            }
            map.rows=rows;map.decor=new Decoration[0];
            map.objects=new[]{new WorldObject{id="retreat_post",
                kind="fieldstation6",x=bx+8,y=by+.5f,
                name="Station",en="Station"}};
            Point start=new Point(bx+4.5f,by+.5f);
            g.reliefMissionRoom=6;g.reliefMissionWave=3;
            g.reliefMissionTime=65;g.reliefFollowerSafety=70;
            g.reliefFollowerWaiting=true;g.reliefFollowerPosition=start;
            g.save.energy=60;g.save.x=start.x-.3f;g.save.y=start.y;
            var attacker=new Enemy(3443,0,start.x+1.2f,start.y,90);
            attacker.state=1;attacker.targetingReliefCivilian=true;
            attacker.timer=.5f;attacker.windupDuration=.65f;
            attacker.target=start;attacker.Face(start-attacker.Position);
            g.enemies.Add(attacker);
            Check(g.ReliefEscortEvadeAvailable,
                "An actual living right-hand raider commits to the same real resident");
            CallCore(g,"TalkRescuedCivilian");
            int select=-1;
            for(int i=0;i<g.conversation.choices.Length;i++)
                if(g.conversation.choices[i].action=="relief_evasive_step")
                    select=i;
            Check(select>=0,"Real civilian interaction makes tactical dodge available");
            g.Choose(select);
            Point after=g.reliefFollowerPosition;
            Check(after.x<start.x-.65f &&
                (after-attacker.Position).Length >
                    (start-attacker.Position).Length &&
                g.save.energy==45,
                "Evasion selects the opposite walkable side of a committed melee charge");
            Check(g.reliefFollowerSafety==70 &&
                !g.ReliefEscortEvadeAvailable,
                "Threat-aware movement grants no free safety recovery and respects cooldown");
            Console.WriteLine("PASS Threat-aware civilian dodge uses the safe open side of a live attacker");
        }
        finally
        {
            map.rows=orig;map.objects=objects;map.decor=decors;
        }
    }

    private static void SealedEscortDodgeCannotSpendEnergyOrTeleport(FrontierRuntime app)
    {
        Prepare(app);
        var g=app.Game;g.save.english=true;g.save.room=6;g.mode="world";
        g.enemies.Clear();g.hazards.Clear();g.projectiles.Clear();
        MapData map=g.Map;string[] oldRows=map.rows;
        WorldObject[] oldObjects=map.objects;
        Decoration[] oldDecor=map.decor;
        try
        {
            int bx=4,by=6;
            var rows=(string[])oldRows.Clone();
            for(int y=by-2;y<=by+2;y++)
            {
                char[] line=rows[y].ToCharArray();
                for(int x=bx;x<=bx+7;x++)line[x]='.';
                rows[y]=new string(line);
            }
            map.rows=rows;map.decor=new Decoration[0];
            map.objects=new[]{new WorldObject{id="evade_no_path_station",
                kind="fieldstation6",x=bx+7,y=by+.5f,
                name="Station",en="Station"}};
            Point prisoner=new Point(bx+3.5f,by+.5f);
            for(int y=by-1;y<=by+1;y++)
            {
                char[] line=rows[y].ToCharArray();
                for(int x=bx+2;x<=bx+4;x++)
                    if(x==bx+2||x==bx+4||y==by-1||y==by+1)
                        line[x]='#';
                rows[y]=new string(line);
            }
            g.reliefMissionRoom=6;g.reliefMissionWave=3;
            g.reliefMissionTime=90;g.reliefFollowerSafety=75;
            g.reliefFollowerWaiting=true;
            g.reliefFollowerPosition=prisoner;
            g.save.x=prisoner.x+.2f;g.save.y=prisoner.y;
            g.save.energy=40;
            var attacker=new Enemy(3442,0,prisoner.x-.2f,
                prisoner.y,60);
            attacker.state=1;attacker.targetingReliefCivilian=true;
            attacker.timer=.5f;attacker.windupDuration=.65f;
            attacker.target=prisoner;attacker.Face(prisoner-attacker.Position);
            g.enemies.Add(attacker);
            Check(g.ReliefEscortEvadeAvailable,
                "A committed attack still offers the manual tactical command while surrounded");
            CallCore(g,"TalkRescuedCivilian");
            int selection=-1;
            for(int i=0;i<g.conversation.choices.Length;i++)
                if(g.conversation.choices[i].action=="relief_evasive_step")
                    selection=i;
            Check(selection>=0,"Trapped NPC exposes the same real action without special-case UI cheats");
            g.Choose(selection);
            Check((g.reliefFollowerPosition-prisoner).Length<.001f &&
                g.save.energy==40 && g.ReliefEscortEvadeAvailable &&
                g.reliefFollowerSafety==75,
                "Solid walls forbid the emergency movement and do not debit scarce player energy");
            Console.WriteLine("PASS Civilian evade refuses closed-world motion and never spends on failure");
        }
        finally
        {
            map.rows=oldRows;map.objects=oldObjects;map.decor=oldDecor;
        }
    }

    private static void CivilianEvadeUsesRealContactAndCostsOnlyWhenMoved(FrontierRuntime app)
    {
        Prepare(app);
        var g=app.Game;g.save.english=true;g.save.room=6;g.mode="world";
        g.enemies.Clear();g.hazards.Clear();g.projectiles.Clear();
        MapData map=g.Map;string[] originalRows=map.rows;
        WorldObject[] originalObjects=map.objects;
        Decoration[] originalDecor=map.decor;
        try
        {
            int bx=4,by=6;
            string[] rows=(string[])originalRows.Clone();
            for(int y=by-3;y<=by+3;y++)
            {
                char[] row=rows[y].ToCharArray();
                for(int x=bx;x<=bx+10;x++)row[x]='.';
                rows[y]=new string(row);
            }
            map.rows=rows;map.decor=new Decoration[0];
            map.objects=new[]{new WorldObject{id="evade_station",
                kind="fieldstation6",x=bx+9.5f,y=by+.5f,
                name="Return",en="Return"}};
            Point original=new Point(bx+3.5f,by+.5f);
            g.reliefMissionRoom=6;g.reliefMissionWave=3;
            g.reliefMissionTime=80;g.reliefFollowerSafety=65;
            g.reliefFollowerPosition=original;
            g.reliefFollowerWaiting=true;
            g.save.x=original.x+.5f;g.save.y=original.y;
            g.save.energy=60;
            var raider=new Enemy(3441,0,original.x-1.2f,original.y,90);
            raider.state=1;raider.targetingReliefCivilian=true;
            raider.timer=.5f;raider.windupDuration=.65f;
            raider.target=original;raider.Face(original-raider.Position);
            g.enemies.Add(raider);
            Check(g.ReliefEscortUnderAttack&&g.ReliefEscortEvadeAvailable,
                "A physically committed threat enables the rescue evasive order");
            Set(app,"camera",g.reliefFollowerPosition);
            GUI.commands.Clear();Call(app,"OnGUI");
            Check(GUI.commands.Exists(c=>c.Contains("ORDER EVASION")),
                "Live world scene shows real actionable E/gamepad evasion cue only during reachable danger");
            CallCore(g,"TalkRescuedCivilian");
            Check(g.mode=="dialog",
                "E interaction still reaches the actual living NPC");
            int option=-1;
            for(int i=0;i<g.conversation.choices.Length;i++)
                if(g.conversation.choices[i].action=="relief_evasive_step")
                    option=i;
            Check(option>=0,"Production civilian dialogue offers an actual tactical evade choice");
            g.Choose(option);
            Point changed=g.reliefFollowerPosition;
            Check((changed-original).Length>=.7f &&
                (changed-original).Length<=1.11f &&
                g.CanStand(changed)&&
                g.save.energy==45 &&
                g.reliefFollowerSafety==65,
                "Swept world dodge moves civilian without teleport, invulnerability or a fake heal");
            Check(!g.ReliefEscortEvadeAvailable,
                "An actual dash has a cooldown and cannot be spammed within one hostile windup");
            float energy=g.save.energy;
            CallCore(g,"ChooseRescuedCivilian","relief_evasive_step");
            Check(g.save.energy==energy &&
                (g.reliefFollowerPosition-changed).Length<.001f,
                "Replayed action cannot double-charge or relocate during cooldown");

            // A noncombat dialog must never fabricate a free movement
            // command even when the player has enough energy.
            raider.state=0;
            Check(!g.ReliefEscortEvadeAvailable,
                "No committed attack means no evade skill to exploit");
            Console.WriteLine("PASS Civilian tactical evade uses real swept displacement, enemy windup and paid cooldown");
        }
        finally
        {
            map.rows=originalRows;map.objects=originalObjects;
            map.decor=originalDecor;
        }
    }

    private static void LivingEscortStallWarnsOnlyOnRealBlockedMovement(FrontierRuntime app)
    {
        Prepare(app);
        var g=app.Game;g.save.english=true;g.mode="world";g.save.room=6;
        g.enemies.Clear();g.hazards.Clear();g.projectiles.Clear();
        MapData map=g.Map;string[] oldRows=map.rows;
        WorldObject[] oldObjects=map.objects;
        Decoration[] oldDecor=map.decor;
        try
        {
            int bx=4,by=6;
            var rows=(string[])oldRows.Clone();
            for(int y=by-2;y<=by+2;y++)
            {
                char[] line=rows[y].ToCharArray();
                for(int x=bx;x<=bx+11;x++)line[x]='.';
                rows[y]=new string(line);
            }
            map.rows=rows;map.decor=new Decoration[0];
            map.objects=new[]{new WorldObject{id="stalled_station",
                kind="fieldstation6",x=bx+10.5f,y=by+.5f,
                name="Return",en="Return"}};
            g.reliefMissionRoom=6;g.reliefMissionWave=3;
            g.reliefMissionTime=100f;g.reliefFollowerSafety=95f;
            g.reliefFollowerWaiting=false;g.reliefCivilianHitTime=0;
            g.reliefFollowerPosition=new Point(bx+2.5f,by+.5f);
            g.save.x=bx+9.5f;g.save.y=by+.5f;
            // Seal the resident inside a ring on collision-valid terrain.
            for(int y=by-1;y<=by+1;y++)
            {
                char[] line=rows[y].ToCharArray();
                for(int x=bx+1;x<=bx+3;x++)
                    if(x==bx+1||x==bx+3||y==by-1||y==by+1)
                        line[x]='#';
                rows[y]=new string(line);
            }
            Point first=g.reliefFollowerPosition;
            for(int i=0;i<28;i++)CallCore(g,"TickReliefMission",.05f);
            Check(g.ReliefEscortRouteStalled &&
                (g.reliefFollowerPosition-first).Length<.001f &&
                g.reliefMissionTime<99f,
                "Genuine trapped survivor triggers blocked status while timed rescue continues");
            Check(g.GetQuest("relief_rescue").next.Contains("path blocked") &&
                (g.NavigationTarget().Value-g.reliefFollowerPosition).Length<.001f,
                "Active rescue quest and compass guide back to a truly immobile resident");
            Set(app,"camera",g.reliefFollowerPosition);
            GUI.commands.Clear();Call(app,"OnGUI");
            Check(GUI.commands.Exists(s=>s.Contains("SURVIVOR PATH BLOCKED")),
                "Production world visibly diagnoses physical civilian route obstruction");

            // Nonmoving WAIT is a deliberate command, not a broken route.
            g.reliefFollowerWaiting=true;
            CallCore(g,"TickReliefMission",.05f);
            Check(!g.ReliefEscortRouteStalled,
                "Explicit civilian wait immediately clears misleading blocked route warning");

            // An open passage restores genuine displacement and clears
            // the one-shot notification, not a lasting failure state.
            g.reliefFollowerWaiting=false;
            for(int y=by-1;y<=by+1;y++)
            {
                char[] line=rows[y].ToCharArray();
                for(int x=bx+1;x<=bx+3;x++)line[x]='.';
                rows[y]=new string(line);
            }
            CallCore(g,"TickReliefMission",.05f);
            Check(g.reliefFollowerMoving &&
                !g.ReliefEscortRouteStalled &&
                (g.reliefFollowerPosition-first).Length>.001f,
                "Reopened world crossing resumes actor movement and removes false obstruction");
            GUI.commands.Clear();Call(app,"OnGUI");
            Check(!GUI.commands.Exists(s=>s.Contains("SURVIVOR PATH BLOCKED")),
                "After genuine movement resumes there is no stale blocked HUD warning");
            Console.WriteLine("PASS Timed civilian follow diagnoses true blocked paths and recovers without teleport");
        }
        finally
        {
            map.rows=oldRows;map.objects=oldObjects;map.decor=oldDecor;
        }
    }

    private static void EvacuationFinalHandoffRequiresBothLivingActorsOnOpenSide(FrontierRuntime app)
    {
        Prepare(app);
        var g=app.Game;g.save.english=true;g.mode="world";g.save.room=6;
        g.enemies.Clear();g.hazards.Clear();g.projectiles.Clear();
        MapData map=g.Map;
        string[] originalRows=map.rows;
        WorldObject[] originalObjects=map.objects;
        Decoration[] originalDecor=map.decor;
        try
        {
            int bx=4,by=6;
            var rows=(string[])originalRows.Clone();
            for(int y=by-2;y<=by+2;y++)
            {
                char[] line=rows[y].ToCharArray();
                for(int x=bx;x<=bx+6;x++)line[x]='.';
                rows[y]=new string(line);
            }
            map.rows=rows;map.decor=new Decoration[0];
            WorldObject station=new WorldObject{id="real_return",
                kind="fieldstation6",x=bx+2f,y=by+.5f,
                name="Return station",en="Return station"};
            map.objects=new[]{station};
            g.save.flags.Add("fieldstation_6");
            g.save.flags.Add("farshore_relief_6");
            g.save.flags.Remove("relief_rescue_6");
            g.save.totalTime=55;
            g.save.teaUntil=0;
            g.reliefMissionRoom=6;g.reliefMissionWave=3;
            g.reliefMissionTime=62f;g.reliefFollowerSafety=90f;
            g.reliefFollowerWaiting=true;
            // Both are within the legacy completion radius, yet a live
            // stone tile separates the hero from the return station.
            g.save.x=bx+.5f;g.save.y=by+.5f;
            g.reliefFollowerPosition=new Point(bx+2.5f,by+.5f);
            char[] rock=rows[by].ToCharArray();
            rock[bx+1]='#';rows[by]=new string(rock);
            int originalCoins=g.save.coins;
            Check(g.ReliefEscortReturnObstructed,
                "Hero inside handoff radius but behind stone receives an explicit blocked-return status");
            Set(app,"camera",g.Position);
            GUI.commands.Clear();Call(app,"OnGUI");
            Check(GUI.commands.Exists(c=>c.Contains("STATION PATH BLOCKED")),
                "Production world shows why the final rescue handoff cannot occur through a wall");
            CallCore(g,"TickReliefMission",.05f);
            Check(g.reliefMissionRoom==6&&!g.Has("relief_rescue_6")&&
                g.save.coins==originalCoins,
                "Hero across solid wall cannot remotely complete a real civilian handoff");

            // Now the hero is on the station side but the resident remains
            // behind the same wall: the completion must still be denied.
            g.save.x=bx+2.5f;g.reliefFollowerPosition=new Point(bx+.5f,by+.5f);
            CallCore(g,"TickReliefMission",.05f);
            Check(g.reliefMissionRoom==6&&!g.Has("relief_rescue_6")&&
                g.save.coins==originalCoins,
                "Civilian across solid wall cannot be claimed as successfully evacuated");

            // The passage is genuinely opened; the original public reward
            // and stage completion remain fully usable without new save keys.
            rock[bx+1]='.';rows[by]=new string(rock);
            Check(!g.ReliefEscortReturnObstructed,
                "An opened route immediately removes the physical obstruction warning");
            CallCore(g,"TickReliefMission",.05f);
            Check(g.reliefMissionRoom==0&&g.Has("relief_rescue_6")&&
                g.save.coins==originalCoins+45,
                "Both civilians and hero physically reaching station preserve normal first-clear rewards");
            int earnedCoins=g.save.coins;
            g.reliefMissionRoom=6;g.reliefMissionWave=3;
            g.reliefMissionTime=62;g.reliefFollowerWaiting=true;
            g.reliefFollowerSafety=90;
            // Completing the first mission clears its transient follower.
            // Recreate the actual return actor for a legitimate replay,
            // instead of accidentally testing an empty no-civilian mission.
            g.reliefFollowerPosition=new Point(bx+.5f,by+.5f);
            CallCore(g,"TickReliefMission",.05f);
            Check(g.reliefMissionRoom==0&&g.save.coins==earnedCoins,
                "Repeating an actual successful return cannot double-award rescued residents");
            Console.WriteLine("PASS Real civilian return needs hero and resident reach, solid-wall rejection, opened passage and no repeat reward");
        }
        finally
        {
            map.rows=originalRows;map.objects=originalObjects;
            map.decor=originalDecor;
        }
    }

    private static void NoPhantomRescueMarkersWithoutMission(FrontierRuntime app)
    {
        Prepare(app);
        var g=app.Game;g.save.english=true;g.mode="world";
        g.save.room=0;g.reliefMissionRoom=0;g.reliefMissionWave=0;
        Set(app,"camera",g.Position);
        GUI.commands.Clear();Call(app,"OnGUI");
        Check(!GUI.commands.Exists(c=>c.Contains("FIND CIVILIANS")||
            c.Contains("ESCORT HOME")||c.Contains("ESCORT SHELTER")),
            "A town scene without live rescued civilians must never show phantom rescue objectives");
        Console.WriteLine("PASS No phantom civilian rescue marker in normal town world");
    }

    private static void ActorOriginNavigationPreservesHeroAndCollision(FrontierRuntime app)
    {
        Prepare(app);
        var g=app.Game;g.save.room=6;g.mode="world";
        g.enemies.Clear();g.hazards.Clear();g.projectiles.Clear();
        MapData map=g.Map;string[] oldRows=map.rows;
        Decoration[] oldDecor=map.decor;
        try
        {
            int bx=4,by=6;
            var rows=(string[])oldRows.Clone();
            for(int y=by-2;y<=by+2;y++)
            {
                char[] row=rows[y].ToCharArray();
                for(int x=bx;x<=bx+12;x++)row[x]='.';
                rows[y]=new string(row);
            }
            map.rows=rows;map.decor=new Decoration[0];
            Point origin=new Point(bx+.5f,by+.5f);
            Point end=new Point(bx+11.5f,by+.5f);
            g.save.x=origin.x;g.save.y=origin.y;
            var existing=g.NavigationPath(end);
            var fromHero=g.NavigationPathFrom(origin,end);
            Check(existing.Count>0&&existing.Count==fromHero.Count,
                "Refactored actor-origin path preserves original hero route length");
            for(int i=0;i<existing.Count;i++)
                Check((existing[i]-fromHero[i]).Length<.001f,
                    "Hero navigation never changes walking cells after reusable origin extraction");

            Point civilian=new Point(bx+2.5f,by+.5f);
            var civilianPath=g.NavigationPathFrom(civilian,end);
            Check(civilianPath.Count>0&&civilianPath.Count<fromHero.Count,
                "Civilian foot origin generates its own shorter real path");
            Check((g.Position-origin).Length<.001f,
                "Computing a civilian route cannot move the hero or alter the saved slot");

            // Seal the civilian inside a solid stone loop. The hero outside
            // still has a path around it, but the civilian must be rejected.
            for(int y=by-1;y<=by+1;y++)
            {
                char[] row=rows[y].ToCharArray();
                for(int x=bx+1;x<=bx+3;x++)
                    if(x==bx+1||x==bx+3||y==by-1||y==by+1)
                        row[x]='#';
                rows[y]=new string(row);
            }
            Check(g.NavigationPathFrom(civilian,end).Count==0,
                "An enclosed rescue actor cannot pretend to reach a shelter through walls");
            Check(g.NavigationPath(end).Count>0,
                "Hero route remains traversable around a civilian's enclosure");
            Check((g.Position-origin).Length<.001f,
                "Two sequential world navigation searches cannot corrupt hero coordinates");
            Console.WriteLine("PASS Actor-origin terrain pathfinding preserves hero and rejects trapped civilian");
        }
        finally
        {
            map.rows=oldRows;map.decor=oldDecor;
        }
    }

    private static void LowSafetyEscortUsesReachableShelterBeforeReturn(FrontierRuntime app)
    {
        Prepare(app);
        var g=app.Game;g.save.english=true;g.mode="world";g.save.room=6;
        g.enemies.Clear();g.hazards.Clear();g.projectiles.Clear();
        MapData map=g.Map;
        string[] oldRows=map.rows;WorldObject[] oldObjects=map.objects;
        Decoration[] oldDecor=map.decor;
        try
        {
            int bx=4,by=6;
            var rows=(string[])oldRows.Clone();
            for(int y=by-3;y<=by+3;y++)
            {
                char[] line=rows[y].ToCharArray();
                for(int x=bx;x<=bx+17;x++)line[x]='.';
                rows[y]=new string(line);
            }
            map.rows=rows;map.decor=new Decoration[0];
            var natural=new WorldObject{id="escort_safe_oasis",kind="shade",
                x=bx+5.5f,y=by+.5f,name="Open shade",en="Open shade"};
            var returnStation=new WorldObject{id="escort_return_6",
                kind="fieldstation6",x=bx+14.5f,y=by+.5f,
                name="Return",en="Return"};
            map.objects=new[]{returnStation,natural};
            g.save.flags.Add("fieldstation_6");
            g.save.flags.Add("farshore_relief_6");
            g.reliefMissionRoom=6;g.reliefMissionWave=3;
            g.reliefMissionTime=65;g.reliefFollowerSafety=28f;
            g.reliefFollowerWaiting=false;g.reliefClimateKitTime=0;
            g.save.x=bx+.5f;g.save.y=by+.5f;
            g.reliefFollowerPosition=g.Position;
            g.save.trackedQuest="main";
            Point? target=g.NavigationTarget();
            Check(target.HasValue&&(target.Value-
                new Point(natural.x,natural.y)).Length<.001f,
                "Critical civilian safety makes the live world compass detour to nearby real shade");
            QuestGuide urgent=g.GetQuest("relief_rescue");
            Check((urgent.target-target.Value).Length<.001f &&
                urgent.next.Contains("reachable shelter first"),
                "Journal and live compass share the actual low-safety refuge instead of giving contradictory goals");
            Set(app,"camera",g.Position);
            GUI.commands.Clear();Call(app,"OnGUI");
            Check(GUI.commands.Exists(c=>c.Contains("ESCORT SHELTER")),
                "The actual world objective shows the same urgent physical refuge instead of a false return marker");
            var route=g.NavigationPath(target.Value);
            Check(route.Count>0 &&
                (route[route.Count-1]-target.Value).Length<1.75f,
                "Survival detour has an actual playable navigation path");

            // A hero CAN walk around this sealed loop, but the living
            // civilian trapped inside cannot. The emergency arrow must NOT
            // point toward a refuge reachable by the wrong actor only.
            string[] beforeCivilianWall=(string[])rows.Clone();
            g.reliefFollowerPosition=new Point(bx+2.5f,by+.5f);
            for(int y=by-1;y<=by+1;y++)
            {
                char[] row=rows[y].ToCharArray();
                for(int x=bx+1;x<=bx+3;x++)
                    if(x==bx+1||x==bx+3||y==by-1||y==by+1)
                        row[x]='#';
                rows[y]=new string(row);
            }
            Check(g.NavigationPath(new Point(natural.x,natural.y)).Count>0 &&
                g.NavigationPathFrom(g.reliefFollowerPosition,
                    new Point(natural.x,natural.y)).Count==0,
                "Adversarial test traps only the real escorted survivor, never the hero");
            target=g.NavigationTarget();
            Check(target.HasValue&&(target.Value-
                new Point(returnStation.x,returnStation.y)).Length<.001f,
                "Unreachable civilian refuge cannot steal critical escort navigation");
            for(int y=by-1;y<=by+1;y++)rows[y]=beforeCivilianWall[y];
            g.reliefFollowerPosition=g.Position;

            // One sealed shade cannot steal the arrow from the reachable
            // rescue station even if much closer by Euclidean distance.
            for(int y=by-1;y<=by+1;y++)
            {
                char[] line=rows[y].ToCharArray();
                for(int x=bx+4;x<=bx+6;x++)
                    if(x==bx+4||x==bx+6||y==by-1||y==by+1)
                        line[x]='#';
                rows[y]=new string(line);
            }
            target=g.NavigationTarget();
            Check(target.HasValue&&(target.Value-
                new Point(returnStation.x,returnStation.y)).Length<.001f,
                "Blocking physical access to shade leaves a reachable repaired return station");
            g.reliefMissionTime=18f;
            target=g.NavigationTarget();
            Check(target.HasValue&&(target.Value-
                new Point(returnStation.x,returnStation.y)).Length<.001f,
                "With fewer than 22 seconds left, do not demand an optional shelter detour");
            QuestGuide urgentTime=g.GetQuest("relief_rescue");
            Check((urgentTime.target-target.Value).Length<.001f &&
                urgentTime.next.Contains("Return survivors to the weather station"),
                "When time is short the quest and compass both switch back to the mandatory station");
            g.reliefMissionTime=65;g.reliefFollowerSafety=50;
            target=g.NavigationTarget();
            Check((target.Value-
                new Point(returnStation.x,returnStation.y)).Length<.001f,
                "Once safety recovers above danger threshold, finish actual rescue route");
            g.reliefFollowerSafety=28;g.reliefFollowerWaiting=true;
            Check((g.NavigationTarget().Value-
                g.reliefFollowerPosition).Length<.001f,
                "Waiting survivor takes priority over every optional shelter");
            g.reliefFollowerWaiting=false;g.save.trackedQuest="main";
            Check(g.save.trackedQuest=="main",
                "Emergency detours never rewrite the pinned journal quest");
            Console.WriteLine("PASS Critical civilian safety uses real cover route and retains timed rescue and quest priorities");
        }
        finally
        {
            map.rows=oldRows;map.objects=oldObjects;map.decor=oldDecor;
        }
    }

    private static void TimedRescueCompassOverridesPinnedQuestWithoutMutatingIt(FrontierRuntime app)
    {
        Prepare(app);
        var g=app.Game;g.save.room=6;g.mode="world";
        g.enemies.Clear();g.projectiles.Clear();
        g.save.trackedQuest="main";g.reliefMissionRoom=6;
        g.reliefMissionWave=0;g.reliefMissionTime=100;
        Point field=g.FieldSurveyTarget(6);
        Check(g.NavigationTarget().HasValue&&
            (g.NavigationTarget().Value-field).Length<.001f,
            "Live field rescue outranks an unrelated pinned story waypoint");
        g.reliefMissionWave=1;
        Check((g.NavigationTarget().Value-field).Length<.001f,
            "The first real ambush stays routed to its own survey marker");
        g.reliefMissionWave=2;
        Check((g.NavigationTarget().Value-field).Length<.001f,
            "The second wave retains its real combat destination");
        g.reliefMissionWave=3;
        // This case verifies the ordinary return leg, not the separate
        // low-safety emergency shelter override tested above.
        g.reliefFollowerSafety=90f;
        g.reliefFollowerPosition=new Point(9.5f,9.5f);
        g.reliefFollowerWaiting=true;g.save.x=18;g.save.y=15;
        Check((g.NavigationTarget().Value-g.reliefFollowerPosition).Length<.001f,
            "A waiting civilian outranks the station; hero must actually return");
        g.reliefFollowerWaiting=false;
        Check((g.NavigationTarget().Value-g.reliefFollowerPosition).Length<.001f,
            "Falling more than 6.5 tiles behind retains the living civilian waypoint");
        g.save.x=g.reliefFollowerPosition.x;
        g.save.y=g.reliefFollowerPosition.y;
        Point station=Array.Find(g.Map.objects,o=>o.kind=="fieldstation6")!=null?
            new Point(Array.Find(g.Map.objects,o=>o.kind=="fieldstation6").x,
                      Array.Find(g.Map.objects,o=>o.kind=="fieldstation6").y):
            new Point(-100,-100);
        Check(station.x>=0&&(g.NavigationTarget().Value-station).Length<.001f,
            "Recovered follower restores the existing real return-station objective");
        Check(g.save.trackedQuest=="main",
            "An emergency timed mission NEVER overwrites the player's pinned journal quest");
        g.reliefMissionRoom=0;g.reliefMissionWave=0;
        Check(!g.NavigationTarget().HasValue||
            (g.NavigationTarget().Value-station).Length>.001f,
            "After mission ends, temporary rescue waypoint releases the compass");
        Console.WriteLine("PASS Timed rescue compass follows physical waves, waiting survivor and station without quest mutation");
    }

    private static void ShelterRestCommandHealsRealEscortOnlyWhenSecure(FrontierRuntime app)
    {
        Prepare(app);
        var g=app.Game;g.save.english=true;g.mode="world";
        g.save.room=6;g.enemies.Clear();g.hazards.Clear();g.projectiles.Clear();
        MapData map=g.Map;
        string[] oldRows=map.rows;WorldObject[] oldObjects=map.objects;
        Decoration[] oldDecor=map.decor;
        try
        {
            int bx=4,by=6;
            var rows=(string[])oldRows.Clone();
            for(int y=by-2;y<=by+2;y++)
            {
                char[] row=rows[y].ToCharArray();
                for(int x=bx;x<=bx+10;x++)row[x]='.';
                rows[y]=new string(row);
            }
            map.rows=rows;map.decor=new Decoration[0];
            map.objects=new[]{new WorldObject{id="escort_shade",kind="shade",
                x=bx+3,y=by+.5f,name="Real shade",en="Real shade"}};
            g.reliefMissionRoom=6;g.reliefMissionWave=3;
            g.reliefMissionTime=90;g.reliefFollowerSafety=50;
            g.reliefFollowerPosition=new Point(bx+.5f,by+.5f);
            g.reliefFollowerWaiting=true;g.reliefClimateKitTime=0;
            g.save.x=bx+9.5f;g.save.y=by+.5f;
            Check(g.ReliefEscortSecureStop,
                "A real waiting civilian can use an unblocked shade rest while hero is far away");
            CallCore(g,"TickReliefMission",.05f);
            Check(Math.Abs(g.reliefFollowerSafety-50.17f)<.001f,
                "Secure physical shelter repairs exactly 3.4 civilian safety per second");
            Set(app,"camera",g.reliefFollowerPosition);
            GUI.commands.Clear();Call(app,"OnGUI");
            Check(GUI.commands.Exists(c=>c.Contains("SHELTERED REST")),
                "Production moving civilian scene visibly reports the earned sheltered recovery");

            g.reliefFollowerSafety=50;
            g.reliefFollowerWaiting=false;
            g.save.x=g.reliefFollowerPosition.x;
            g.save.y=g.reliefFollowerPosition.y;
            CallCore(g,"TickReliefMission",.05f);
            Check(Math.Abs(g.reliefFollowerSafety-50.07f)<.001f,
                "Following normally keeps existing 1.4 safety recovery, not the stop bonus");

            g.reliefFollowerWaiting=true;
            g.reliefFollowerSafety=50;
            g.enemies.Add(new Enemy(1882,0,bx+2,by+.5f,80f));
            Check(!g.ReliefEscortSecureStop,
                "A real living patrol within four tiles interrupts sheltered rest");
            CallCore(g,"TickReliefMission",.05f);
            Check(Math.Abs(g.reliefFollowerSafety-50.07f)<.001f,
                "Hostile presence removes only extra recovery; actual canopy still protects");
            g.enemies.Clear();

            char[] stone=rows[by].ToCharArray();
            stone[bx+1]='#';rows[by]=new string(stone);
            Check(!g.ReliefEscortSecureStop&&!g.ReliefEscortSheltered,
                "Rock wall invalidates protected rest based on civilian feet");
            g.reliefFollowerSafety=50;
            CallCore(g,"TickReliefMission",.05f);
            Check(Math.Abs(g.reliefFollowerSafety-49.925f)<.001f,
                "Blocked cover restores real exposure loss rather than granting free safety");
            GUI.commands.Clear();Call(app,"OnGUI");
            Check(GUI.commands.Exists(c=>c.Contains("EXPOSED TO WEATHER")) &&
                !GUI.commands.Exists(c=>c.Contains("SHELTERED REST")),
                "Production rescue display marks the actual stranded civilian exposed");
            Console.WriteLine("PASS Real escort rest: covered stop, active patrol, moving fallback, wall and visible danger");
        }
        finally
        {
            map.rows=oldRows;map.objects=oldObjects;map.decor=oldDecor;
        }
    }

    private static void ActualCivilianClimateSheltersCannotPassThroughWalls(FrontierRuntime app)
    {
        Prepare(app);
        var g=app.Game;g.save.english=true;g.mode="world";
        g.enemies.Clear();g.hazards.Clear();g.projectiles.Clear();
        for(int region=5;region<=7;region++)
        {
            g.save.room=region;
            MapData map=g.Map;
            string[] originalRows=map.rows;
            WorldObject[] originalObjects=map.objects;
            Decoration[] originalDecor=map.decor;
            try
            {
                Check(Array.Exists(originalObjects,
                    o=>o.kind=="fieldstation"+region),
                    "Shipped rescue map contains its authored weather station");
                int bx=4,by=6;
                string[] rows=(string[])originalRows.Clone();
                for(int y=by-2;y<=by+2;y++)
                {
                    char[] line=rows[y].ToCharArray();
                    for(int x=bx;x<=bx+7;x++)line[x]='.';
                    rows[y]=new string(line);
                }
                map.rows=rows;map.decor=new Decoration[0];
                WorldObject station=new WorldObject {
                    id="escort_station_"+region,kind="fieldstation"+region,
                    x=bx+3,y=by+.5f,name="Shelter",en="Shelter"};
                WorldObject natural=new WorldObject {
                    id="escort_natural_"+region,
                    kind=region==7?"brazier":"shade",
                    x=bx+3,y=by+.5f,name="Field refuge",en="Field refuge"};
                map.objects=region==5?new[]{station}:new[]{station,natural};
                g.reliefMissionRoom=region;g.reliefMissionWave=3;
                g.reliefFollowerPosition=new Point(bx+.5f,by+.5f);
                // Rescue shelter belongs to the civilian, not hero.
                g.save.x=bx+9.5f;g.save.y=by+.5f;
                g.save.totalTime=25f;g.save.tideHeldUntil=0f;
                g.save.flags.Add("farshore_relief_"+region);
                g.save.flags.Add("fieldstation_"+region);
                if(region==7)g.save.flags.Add("warm_"+natural.id);
                Check(g.ReliefEscortSheltered,
                    "Real resident feet on unobstructed ground receive physical climate protection");
                // Seal the direct lane while preserving both feet and the
                // station at their original still-close coordinates.
                char[] rock=rows[by].ToCharArray();
                rock[bx+1]='#';rows[by]=new string(rock);
                Check(!g.ReliefEscortSheltered,
                    "Neither upgraded station nor natural fire/shade can shield a civilian through stone");
                if(region==5)
                {
                    g.save.totalTime=5f;
                    Check(g.ReliefEscortSheltered,
                        "Mistport low tide itself remains an authentic environmental shelter");
                    g.save.totalTime=25f;
                }
                rock[bx+1]='.';rows[by]=new string(rock);
                Check(g.ReliefEscortSheltered,
                    "Opening the original crossing immediately restores civilian refuge");
                g.save.flags.Remove("fieldstation_"+region);
                if(region==5)
                    Check(!g.ReliefEscortSheltered,
                        "An unpowered port station cannot protect through an imaginary network");
                else
                    Check(g.ReliefEscortSheltered,
                        "Natural tree shade and actually lit fire remain meaningful without station power");
                if(region==7)
                {
                    g.save.flags.Remove("warm_"+natural.id);
                    Check(!g.ReliefEscortSheltered,
                        "Extinguished alpine fire cannot protect a stranded resident");
                }
                else if(region==6)
                {
                    map.objects=new[]{station};
                    Check(!g.ReliefEscortSheltered,
                        "An unpowered desert fieldstation alone is not an oasis");
                }
                g.reliefMissionWave=0;
                Check(!g.ReliefEscortSheltered,
                    "Survivor refuge ends outside the live return-escort stage");
            }
            finally
            {
                map.rows=originalRows;map.objects=originalObjects;
                map.decor=originalDecor;
            }
        }
        Console.WriteLine("PASS Real civilian escort shelters respect survivor feet, living facilities, walls and tides");
    }

    private static void RepairedClimateStationProtectsOnlyItsPhysicalSide(FrontierRuntime app)
    {
        Prepare(app);
        var g=app.Game;g.save.english=true;g.save.room=6;g.mode="world";
        g.enemies.Clear();g.hazards.Clear();g.projectiles.Clear();
        MapData map=g.Map;
        string[] oldRows=map.rows;WorldObject[] oldObjects=map.objects;
        Decoration[] oldDecor=map.decor;
        try
        {
            Check(Array.Exists(oldObjects,o=>o.kind=="fieldstation6"),
                "The live Glass Desert must contain a real authored cooling station");
            int bx=3,by=5;
            string[] rows=(string[])oldRows.Clone();
            for(int y=by-2;y<=by+2;y++)
            {
                char[] line=rows[y].ToCharArray();
                for(int x=bx;x<=bx+6;x++)line[x]='.';
                rows[y]=new string(line);
            }
            map.rows=rows;map.decor=new Decoration[0];
            map.objects=new[]{new WorldObject{id="climate_real",kind="fieldstation6",
                name="Cooling post",en="Cooling post",x=bx+3.5f,y=by+.5f}};
            g.save.flags.Add("fieldstation_6");
            g.save.teaUntil=0;g.save.totalTime=15;
            g.save.x=bx+.5f;g.save.y=by+.5f;g.save.heat=45f;
            Check(g.NearActiveFieldStation(6,g.FieldStationRefugeRadius(6)) &&
                g.FieldStationSheltersHero(map.objects[0]),
                "An activated station still shelters the hero on an open physical path");
            g.save.heat=90f;Set(app,"camera",g.Position);
            GUI.commands.Clear();Call(app,"OnGUI");
            Check(GUI.commands.Exists(c=>c.Contains("CLIMATE SHELTER ACTIVE")),
                "Production field station displays a genuinely accessible full-radius shelter");
            g.save.heat=45f;
            CallCore(g,"TickExpansion",.05f);
            Check(g.save.heat<45f,
                "Genuine cooling from repaired station still lowers exposure");

            char[] wall=rows[by].ToCharArray();
            wall[bx+1]='#';rows[by]=new string(wall);
            Check(!g.NearActiveFieldStation(6,g.FieldStationRefugeRadius(6)) &&
                !g.FieldStationSheltersHero(map.objects[0]),
                "An equally near post behind a sealed wall cannot cool through stone");
            g.save.heat=90f;
            GUI.commands.Clear();Call(app,"OnGUI");
            Check(!GUI.commands.Exists(c=>c.Contains("CLIMATE SHELTER ACTIVE")) &&
                GUI.commands.Exists(c=>c.Contains("ENTER STATION REFUGE")),
                "Behind-wall station halo honestly shows unavailable protection");
            g.save.heat=45f;
            CallCore(g,"TickExpansion",.05f);
            Check(g.save.heat>45f,
                "Solid cliff restores real desert exposure despite a nearby repaired station");
            g.save.flags.Remove("fieldstation_6");
            wall[bx+1]='.';rows[by]=new string(wall);
            Check(!g.NearActiveFieldStation(6,g.FieldStationRefugeRadius(6)),
                "Unbuilt station provides no free climate shelter");
            Console.WriteLine("PASS Production fieldstation rejects fake radius-only climate cover through solid walls");
        }
        finally
        {
            map.rows=oldRows;map.objects=oldObjects;map.decor=oldDecor;
        }
    }

    private static void LiveOasisShadeRestIsPhysicalAndSafe(FrontierRuntime app)
    {
        Prepare(app);
        var g=app.Game;g.save.english=true;g.save.room=6;g.mode="world";
        g.enemies.Clear();g.hazards.Clear();g.projectiles.Clear();
        WorldObject canopy=Array.Find(g.Map.objects,o=>o.kind=="shade");
        Check(canopy!=null,"Production Glass Desert contains a real oasis shelter");
        g.save.x=canopy.x;g.save.y=canopy.y;
        g.save.totalTime=36f;g.save.heat=90f;g.save.health=50f;g.save.energy=20f;
        g.Talk(canopy);
        Check(g.mode=="dialog"&&g.conversation.choices.Length>=2&&
            g.conversation.choices[0].action=="shadeRest:"+canopy.id,
            "Existing oasis now offers a real clickable shade respite");
        g.Choose(0);
        Check(g.mode=="world"&&Math.Abs(g.save.totalTime-48f)<.001f&&
            Math.Abs(g.save.heat-42f)<.001f&&
            Math.Abs(g.save.health-64f)<.001f&&
            Math.Abs(g.save.energy-44f)<.001f,
            "Unobstructed safe shade advances real world time and grants capped partial recovery");

        float recoveredAt=g.save.totalTime;
        g.Talk(canopy);g.save.x=canopy.x+10;g.save.y=canopy.y;
        g.Choose(0);
        Check(g.save.totalTime==recoveredAt&&
            g.save.health==64f&&g.save.energy==44f,
            "Leaving an open shade conversation cannot remotely heal or skip time");

        g.save.x=canopy.x;g.save.y=canopy.y;
        g.enemies.Add(new Enemy(1191,0,canopy.x+1,canopy.y,90));
        g.Talk(canopy);g.Choose(0);
        Check(g.save.totalTime==recoveredAt&&g.save.health==64f,
            "Enemy ambush prevents shade rest even beneath the canopy");
        g.enemies.Clear();
        g.racing=true;g.Talk(canopy);g.Choose(0);
        Check(g.save.totalTime==recoveredAt&&g.save.health==64f,
            "Timed skiff race forbids skipping its contest clock at shade");
        g.racing=false;
        Console.WriteLine("PASS Physical oasis resting: real timed recovery, closed range, combat and mission isolation");
    }

    private static void CriticalDesertHeatGuidesActualReachableShade(FrontierRuntime app)
    {
        Prepare(app);
        var g=app.Game;g.save.english=true;g.save.room=6;g.mode="world";
        g.enemies.Clear();g.hazards.Clear();g.projectiles.Clear();
        CallCore(g,"ResetExpansionTransient");
        MapData map=g.Map;string[] originalRows=map.rows;
        WorldObject[] originalObjects=map.objects;
        Decoration[] originalDecor=map.decor;
        try
        {
            Check(Array.FindAll(originalObjects,o=>o.kind=="shade").Length>=2,
                "The production Glass Desert actually contains two independently drawn shade posts");
            int cx=3,cy=5;
            string[] open=(string[])originalRows.Clone();
            for(int y=cy-2;y<=cy+2;y++)
            {
                char[] row=open[y].ToCharArray();
                for(int x=cx;x<=cx+12;x++)row[x]='.';
                open[y]=new string(row);
            }
            map.rows=open;map.decor=new Decoration[0];
            var near=new WorldObject{kind="shade",id="heat_near",
                name="Oasis canopy",en="Oasis canopy",x=cx+5.5f,y=cy+.5f};
            var far=new WorldObject{kind="shade",id="heat_far",
                name="Desert canopy",en="Desert canopy",x=cx+10.5f,y=cy+.5f};
            map.objects=new[]{near,far};
            g.save.x=cx+.5f;g.save.y=cy+.5f;
            g.save.heat=90;g.save.teaUntil=0;g.save.totalTime=20;
            Point? target=g.NavigationTarget();
            Check(target.HasValue&&(target.Value-new Point(near.x,near.y)).Length<.001f,
                "Dangerous desert heat picks the nearest reachable authored-type shade");
            var station=new WorldObject{id="desert_weather_qa",
                kind="fieldstation6",name="Cooler",en="Cooler",
                x=cx+5f,y=cy+.5f};
            map.objects=new[]{station,near,far};
            g.save.flags.Add("fieldstation_6");
            target=g.NavigationTarget();
            Check(target.HasValue&&(target.Value-new Point(station.x,station.y)).Length<.001f,
                "Restored nearby cooler outranks a longer exposed walk to an oasis");
            g.save.flags.Remove("fieldstation_6");
            target=g.NavigationTarget();
            Check(target.HasValue&&(target.Value-new Point(near.x,near.y)).Length<.001f,
                "An unbuilt cooling station is never advertised as a rescue route");
            var path=g.NavigationPath(target.Value);
            Check(path.Count>0&&(path[path.Count-1]-target.Value).Length<1.75f,
                "Existing minmap/compass uses a real traversable path to cooling shade");

            // Stone enclosure makes the apparently nearer shade unreachable.
            for(int y=cy-1;y<=cy+1;y++)
            {
                char[] cells=open[y].ToCharArray();
                for(int x=cx+4;x<=cx+6;x++)
                    if(x==cx+4||x==cx+6||y==cy-1||y==cy+1)cells[x]='#';
                open[y]=new string(cells);
            }
            target=g.NavigationTarget();
            Check(target.HasValue&&(target.Value-new Point(far.x,far.y)).Length<.001f,
                "A sealed near canopy never beats a genuinely accessible distant shade");

            g.save.teaUntil=g.save.totalTime+20;
            target=g.NavigationTarget();
            Check(!target.HasValue||(target.Value-new Point(far.x,far.y)).Length>.001f,
                "Active heat-protective tea restores ordinary tracked quest guidance");
            g.save.teaUntil=0;g.save.heat=84;
            target=g.NavigationTarget();
            Check(!target.HasValue||(target.Value-new Point(far.x,far.y)).Length>.001f,
                "Once cooled below danger, the compass releases the emergency route");
            g.save.heat=90;g.save.x=far.x;g.save.y=far.y;
            Check(!g.NavigationTarget().HasValue||
                (g.NavigationTarget().Value-new Point(near.x,near.y)).Length>.001f,
                "Already shaded hero does not get a stale rescue route");
            Check(g.ShadeCoolsHero(far) && !g.ShadeCoolsHero(near),
                "Only an exposed, reachable physical canopy cools the hero");
            // Real world ticks now let a safe explorer recuperate *only*
            // beneath accessible shade; hostile presence suspends recovery
            // without removing the passive temperature protection.
            g.save.health=65;g.save.energy=20;g.save.heat=90;
            CallCore(g,"TickExpansion",.05f);
            Check(g.save.health>65&&g.save.energy>20&&g.save.heat<90,
                "A safe, physically shaded hero regains health/energy while cooling");
            g.enemies.Add(new Enemy(1419,0,far.x+1,far.y,70f));
            g.save.health=65;g.save.energy=20;g.save.heat=90;
            CallCore(g,"TickExpansion",.05f);
            Check(g.save.health==65&&g.save.energy==20&&g.save.heat<90,
                "Combat removes shade healing and energy recovery but not physical cooling");
            g.enemies.Clear();
            g.racing=true;g.save.health=65;g.save.energy=20;
            CallCore(g,"TickExpansion",.05f);
            Check(g.save.health==65&&g.save.energy==20,
                "Time-critical desert races never mint shelter resources");
            g.racing=false;
            Set(app,"camera",g.Position);
            GUI.commands.Clear();Call(app,"OnGUI");
            Check(GUI.commands.Exists(c=>c.Contains("SHADE COOLING · REST")),
                "Production desert renderer shows both real cooling and safe-rest availability");
            g.save.x=cx+.5f;g.save.y=cy+.5f;
            Set(app,"camera",g.Position);
            GUI.commands.Clear();Call(app,"OnGUI");
            Check(!GUI.commands.Exists(c=>c.Contains("SHADE COOLING")) &&
                !g.ShadeCoolsHero(near),
                "Sealed nearest canopy never displays a false active shelter");

            // The last real canopy grants a SHORT walking buffer, not a
            // new global healing radius. Crossing a sealed wall cannot
            // re-arm it; the value runs down on the real world clock.
            Check(g.ShadeBreezeSeconds>2.4f && !g.ShadedInDesert,
                "Leaving a genuine canopy earns only the short departing breeze");
            g.save.heat=65;g.save.health=65;g.save.energy=20;
            CallCore(g,"TickExpansion",.05f);
            Check(Math.Abs(g.save.heat-65f)<.0001f &&
                g.save.health==65&&g.save.energy==20,
                "Breeze arrests new heat briefly but never grants out-of-shade recovery");
            Set(app,"camera",g.Position);
            GUI.commands.Clear();Call(app,"OnGUI");
            Check(GUI.commands.Exists(c=>c.Contains("OASIS BREEZE")),
                "Production world shows the actual short shade-carry countdown");
            for(int frame=0;frame<52;frame++)
                CallCore(g,"TickExpansion",.05f);
            Check(g.ShadeBreezeSeconds==0f&&g.save.heat>65f &&
                g.save.health==65&&g.save.energy==20,
                "Expired shade carry lets genuine desert heat resume without healing");
            CallCore(g,"ResetExpansionTransient");
            Check(g.ShadeBreezeSeconds==0f,
                "Load, travel and new sessions cannot inherit old breeze protection");
            Console.WriteLine("PASS Production desert shade: recover only when safe, carry brief breeze, stop on wall and expire");
        }
        finally
        {
            map.rows=originalRows;map.objects=originalObjects;
            map.decor=originalDecor;
        }
    }

    private static void CriticalAlpineColdGuidesReachablePhysicalBrazier(FrontierRuntime app)
    {
        Prepare(app);
        var g=app.Game;g.save.english=true;g.save.room=7;g.mode="world";
        g.enemies.Clear();g.hazards.Clear();g.projectiles.Clear();
        CallCore(g,"ResetExpansionTransient");
        var m=g.Map;
        string[] oldRows=m.rows;WorldObject[] oldObjects=m.objects;
        Decoration[] oldDecor=m.decor;
        try
        {
            int w=oldRows[0].Length,h=oldRows.Length;
            Check(w>18&&h>10&&Array.Exists(oldObjects,o=>o.kind=="brazier"),
                "The shipped mountain must be large enough for a physical shelter route");
            int bx=3,by=5;
            var rows=(string[])oldRows.Clone();
            for(int y=by-2;y<=by+2;y++)
            {
                char[] row=rows[y].ToCharArray();
                for(int x=bx;x<=bx+12;x++)row[x]='.';
                rows[y]=new string(row);
            }
            m.rows=rows;m.decor=new Decoration[0];
            var near=new WorldObject{id="nav_ember_near",kind="brazier",
                name="Near brazier",en="Near brazier",x=bx+5.5f,y=by+.5f};
            var far=new WorldObject{id="nav_ember_far",kind="brazier",
                name="Far brazier",en="Far brazier",x=bx+10.5f,y=by+.5f};
            m.objects=new[]{near,far};
            g.save.x=bx+.5f;g.save.y=by+.5f;
            g.save.teaUntil=0;g.save.totalTime=20f;g.save.cold=88f;
            g.save.inventory[0]=2;
            g.save.flags.Add("warm_"+near.id);
            g.save.flags.Add("warm_"+far.id);
            Point? target=g.NavigationTarget();
            Check(target.HasValue&&(target.Value-new Point(near.x,near.y)).Length<.001f,
                "Dangerous cold redirects existing compass to closest WALKABLE lit fire");
            var station=new WorldObject{id="alpine_weather_qa",
                kind="fieldstation7",name="Warming post",en="Warming post",
                x=bx+5f,y=by+.5f};
            m.objects=new[]{station,near,far};
            g.save.flags.Add("fieldstation_7");
            target=g.NavigationTarget();
            Check(target.HasValue&&(target.Value-new Point(station.x,station.y)).Length<.001f,
                "Already repaired weather post is a reachable alternative to lit fire");
            g.save.flags.Remove("fieldstation_7");
            target=g.NavigationTarget();
            Check(target.HasValue&&(target.Value-new Point(near.x,near.y)).Length<.001f,
                "Unavailable mountain weather post does not steal an existing fire route");
            var routed=g.NavigationPath(target.Value);
            Check(routed.Count>0 &&
                (routed[routed.Count-1]-target.Value).Length<1.75f,
                "Current mountain pathfinder generates actual steps to the refuge");

            // A stone-surrounded fire is NOT reachable merely because it is
            // closer in euclidean space than another genuine route.
            for(int y=by-1;y<=by+1;y++)
            {
                char[] chars=rows[y].ToCharArray();
                for(int x=bx+4;x<=bx+6;x++)
                    if(x==bx+4||x==bx+6||y==by-1||y==by+1)chars[x]='#';
                rows[y]=new string(chars);
            }
            target=g.NavigationTarget();
            Check(target.HasValue&&(target.Value-new Point(far.x,far.y)).Length<.001f,
                "A nearby fire sealed in stone loses to the real traversable distant brazier");

            g.save.flags.Remove("warm_"+far.id);
            g.save.inventory[0]=2;
            target=g.NavigationTarget();
            Check(target.HasValue&&(target.Value-new Point(far.x,far.y)).Length<.001f,
                "Two timber lets the survival compass locate an unlit but affordable safe fire");
            g.save.inventory[0]=1;
            target=g.NavigationTarget();
            Check(!target.HasValue||(target.Value-new Point(far.x,far.y)).Length>.001f,
                "Without timber the arrow never advertises a cold refuge the player cannot ignite");

            g.save.inventory[0]=2;g.save.flags.Add("warm_"+far.id);
            g.save.cold=84f;
            target=g.NavigationTarget();
            Check(!target.HasValue||(target.Value-new Point(far.x,far.y)).Length>.001f,
                "Below danger threshold normal tracked quest guidance is restored");
            g.save.cold=88f;g.save.x=far.x;g.save.y=far.y;
            Check(g.LitBrazierRefuge &&
                (!g.NavigationTarget().HasValue||
                 (g.NavigationTarget().Value-new Point(near.x,near.y)).Length>.001f),
                "Already sheltered hero gets no stale emergency detour");

            Console.WriteLine("PASS Critical mountain cold routes only to physically reachable, affordable braziers");
        }
        finally
        {
            m.rows=oldRows;m.objects=oldObjects;m.decor=oldDecor;
        }
    }

    private static void RealAlpineBrazierRestRequiresSafePhysicalContact(FrontierRuntime app)
    {
        Prepare(app);
        var g=app.Game;g.save.english=true;g.save.room=7;
        g.mode="world";g.enemies.Clear();g.projectiles.Clear();g.hazards.Clear();
        WorldObject fire=Array.Find(g.Map.objects,o=>o.kind=="brazier");
        Check(fire!=null,"Existing mountain contains real camp braziers");
        g.save.x=fire.x;g.save.y=fire.y;
        g.save.flags.Add("warm_"+fire.id);
        g.save.totalTime=37f;g.save.cold=85f;g.save.health=60f;g.save.energy=14f;
        g.Talk(fire);
        Check(g.mode=="dialog"&&g.conversation.choices.Length>0&&
            g.conversation.choices[0].action=="brazierRest:"+fire.id,
            "Lit production-world brazier offers a real player-selectable rest");
        g.Choose(0);
        Check(g.mode=="world"&&Math.Abs(g.save.totalTime-49f)<.001f&&
            Math.Abs(g.save.cold-37f)<.001f&&
            Math.Abs(g.save.health-78f)<.001f&&
            Math.Abs(g.save.energy-40f)<.001f,
            "Real 12-second safe rest skips world time and restores capped partial resources");
        Check(g.BrazierEmberSeconds>2f,
            "Physical rest also refreshes only the transient departing warmth");
        float later=g.save.totalTime;
        g.Talk(fire);
        g.save.x=fire.x+9;g.save.y=fire.y;
        g.Choose(0);
        Check(g.save.totalTime==later&&
            g.save.health==78f&&g.save.energy==40f,
            "Stale conversation cannot rest or heal remotely after hero walks away");
        g.save.x=fire.x;g.save.y=fire.y;
        g.enemies.Add(new Enemy(933,0,fire.x+1,fire.y,90f));
        g.Talk(fire);g.Choose(0);
        Check(g.save.totalTime==later&&g.save.health==78f&&
            g.save.energy==40f,
            "Nearby live enemy prevents a fraudulent time skip");
        g.enemies.Clear();
        g.racing=true;
        g.Talk(fire);g.Choose(0);
        Check(g.save.totalTime==later&&g.save.health==78f&&
            g.save.energy==40f,
            "Ongoing timed field activities cannot be bypassed by a mountain rest");
        g.racing=false;
        g.save.flags.Remove("warm_"+fire.id);
        g.Talk(fire);
        Check(g.conversation.choices[0].action=="warm:"+fire.id,
            "Unlit brazier still requires the original two-timber ignition");
        Console.WriteLine("PASS Real mountain brazier dialogue: safe timed rest, physical reach, active combat and event isolation");
    }

    private static void LitAlpineBraziersGivePhysicalSafeRest(FrontierRuntime app)
    {
        Prepare(app);
        var g=app.Game;g.save.english=true;g.save.room=7;
        g.mode="world";g.enemies.Clear();g.hazards.Clear();g.projectiles.Clear();
        g.save.flags.Clear();g.save.teaUntil=0;g.save.totalTime=12;
        WorldObject brazier=Array.Find(g.Map.objects,o=>o.kind=="brazier");
        Check(brazier!=null,"Production alpine scene contains an authored brazier");
        MapData map=g.Map;
        string[] originalRows=map.rows;
        Decoration[] originalDecor=map.decor;
        try
        {
            // Preserve the original world objects and exercise its real
            // collision checker against a reversible pass/wall fixture.
            int x=(int)brazier.x,y=(int)brazier.y;
            Check(x>=5&&y>=5&&x+4<map.rows[0].Length&&
                y+4<map.rows.Length,"Shipped brazier must occupy usable mountain terrain");
            string[] pass=(string[])originalRows.Clone();
            for(int row=y-2;row<=y+2;row++)
            {
                char[] chars=pass[row].ToCharArray();
                for(int col=x-2;col<=x+3;col++)chars[col]='.';
                pass[row]=new string(chars);
            }
            map.rows=pass;map.decor=new Decoration[0];
            g.save.x=x+2.5f;g.save.y=y+.5f;
            Check(g.CanStand(g.Position),"Resting hero stands on genuine collision-valid terrain");

            g.save.cold=40;g.save.health=60;g.save.energy=10;
            CallCore(g,"TickExpansion",.05f);
            Check(g.save.cold>40f&&g.save.health==60f&&g.save.energy==10f,
                "Unlit fire cannot give free warmth, health or energy");
            g.save.flags.Add("warm_"+brazier.id);
            Check(g.BrazierWarmsHero(brazier)&&g.LitBrazierRefuge,
                "A lit fire across an open mountain pass gives genuine local protection");
            g.save.cold=40;g.save.health=60;g.save.energy=10;
            CallCore(g,"TickExpansion",.05f);
            Check(g.save.cold<40f&&g.save.health>60f&&g.save.energy>10f,
                "Lit fire stops chill and gives safe, low-rate health and energy respite");
            Set(app,"camera",g.Position);
            GUI.commands.Clear();Call(app,"OnGUI");
            Check(GUI.commands.Exists(c=>c.Contains("WARM REFUGE")),
                "Production mountain scene reports a genuine warm field");

            // Insert a real opaque tile between the hero and fireplace:
            // the same euclidean radius must no longer grant shelter.
            char[] barrier=pass[y].ToCharArray();
            barrier[x+1]='#';
            pass[y]=new string(barrier);
            Check(!g.BrazierWarmsHero(brazier)&&!g.LitBrazierRefuge,
                "A sealed cliff blocks warmth, not just the visual route");
            g.save.cold=40;g.save.health=60;g.save.energy=10;
            CallCore(g,"TickExpansion",.05f);
            Check(Math.Abs(g.save.cold-40f)<.0001f&&
                g.save.health==60f&&g.save.energy==10f&&
                g.BrazierEmberSeconds>2.4f,
                "Only earned carried warmth pauses new cold; a wall blocks HP and energy rest");
            GUI.commands.Clear();Call(app,"OnGUI");
            Check(!GUI.commands.Exists(c=>c.Contains("WARM REFUGE")) &&
                GUI.commands.Exists(c=>c.Contains("EMBER WARMTH")),
                "World feedback distinguishes fading embers from an accessible fire");
            for(int i=0;i<52;i++)CallCore(g,"TickExpansion",.05f);
            Check(g.BrazierEmberSeconds==0&&g.save.cold>40f&&
                g.save.health==60f&&g.save.energy==10f,
                "Carry expires after a bounded traversal; sealed walls cannot grant perpetual warming");

            // Reopen only the injected tile, preserving all other terrain.
            barrier[x+1]='.';
            pass[y]=new string(barrier);
            Check(g.BrazierWarmsHero(brazier),"Opening the authored corridor restores shelter");
            g.enemies.Add(new Enemy(959,0,g.save.x+1,g.save.y,100));
            g.save.cold=40;g.save.health=60;g.save.energy=10;
            CallCore(g,"TickExpansion",.05f);
            Check(g.save.cold<40f&&g.save.health==60f&&g.save.energy==10f,
                "Fire warmth survives combat but healing and energy rest do not");
            g.enemies.Clear();
            // The same reset runs on save-slot replacement and real travel.
            CallCore(g,"ResetExpansionTransient");
            Check(g.BrazierEmberSeconds==0,
                "Transient ember carry must not survive a world/slot transition");
            Console.WriteLine("PASS Production alpine brazier: physical refuge, bounded ember carry, real wall, combat and reset isolation");
        }
        finally
        {
            map.rows=originalRows;map.decor=originalDecor;
        }
    }

    private static void LiveAffordableWardUsesActualCaveNavigation(FrontierRuntime app)
    {
        Prepare(app);
        var g=app.Game;g.save.english=true;
        g.save.frontierEnding=1;g.StartAbyss(0);
        g.save.abyssFloor=3;CallCore(g,"GenerateAbyss");
        g.enemies.Clear();g.enemies.Add(new Enemy(998,0,20,20,100));
        var ward=Array.Find(g.Map.objects,o=>o.kind=="abyssRiftWard");
        Check(ward!=null,"A real deep-cave ward must exist for navigation");
        Point site=new Point(ward.x,ward.y),gate=new Point(31,14);
        g.save.x=5;g.save.y=14;
        g.save.runShards=2;
        Check((g.NavigationTarget().Value-gate).Length<.001f,
            "One shard short cannot divert the normal banking compass");
        g.save.runShards=3;
        Point? objective=g.NavigationTarget();
        Check(objective.HasValue&&(objective.Value-site).Length<.001f,
            "The existing live minimap and world compass target the affordable physical pylon");
        var path=g.NavigationPath(objective.Value);
        Check(path.Count>0&&(path[path.Count-1]-site).Length<=1.21f,
            "Existing collision-aware cave route really reaches the chosen pylon");

        // Combat resolution and tuning must release the optional target.
        g.enemies.Clear();
        Check((g.NavigationTarget().Value-gate).Length<.001f,
            "Once enemies are cleared, a refuge must not override the reward gate");
        g.enemies.Add(new Enemy(999,0,20,20,100));
        g.save.x=ward.x;g.save.y=ward.y;
        g.Talk(ward);g.Choose(0);
        Check(g.AbyssWardActive&&g.save.runShards==0&&
            (g.NavigationTarget().Value-gate).Length<.001f,
            "Real pylon tuning consumes only current-floor loot and restores the original gate route");

        // A second floor does not inherit the previous floor's sanctuary.
        g.save.abyssFloor=4;CallCore(g,"GenerateAbyss");
        g.save.runShards=3;
        var next=Array.Find(g.Map.objects,o=>o.kind=="abyssRiftWard");
        Check(next!=null&&!g.AbyssWardActive&&
            (g.NavigationTarget().Value-new Point(next.x,next.y)).Length<.001f,
            "Next floor gets its own discoverable, independently paid ward");
        Console.WriteLine("PASS Existing cave minimap and world arrow guide to affordable physical wards, then release the route");
    }

    private static void LiveWardRestoresEnergyOnlyFromPhysicalAmbientImpact(FrontierRuntime app)
    {
        Prepare(app);
        var g=app.Game;g.save.english=true;
        g.save.frontierEnding=1;g.StartAbyss(0);
        g.save.abyssFloor=3;CallCore(g,"GenerateAbyss");
        g.enemies.Clear();g.hazards.Clear();g.projectiles.Clear();
        WorldObject ward=Array.Find(g.Map.objects,o=>o.kind=="abyssRiftWard");
        Check(ward!=null,"Storm conversion needs the real generated floor-three ward");
        g.save.x=ward.x;g.save.y=ward.y;g.save.runShards=3;
        g.Talk(ward);g.Choose(0);
        Check(g.AbyssWardShelter,"Physical shard purchase enables the actual ward");

        // A distant expiry must not charge stamina, play guard or award dodge.
        g.save.energy=25;g.effects.Clear();g.sound="";
        g.hazards.Add(new Hazard {position=g.Position+new Point(4,0),
            radius=1.05f,timer=.01f,damage=20,kind="rift",abyssStormOwned=true});
        CallCore(g,"TickExpansion",.02f);
        Check(Math.Abs(g.save.energy-25f)<.001f&&g.sound!="guard"&&
            !g.effects.Exists(e=>e.text=="WARD ENERGY"),
            "A storm missing the hero must never grant a defensive pulse");

        // Only the real, physically intercepted ambient contact is converted.
        g.hazards.Clear();g.effects.Clear();g.sound="";
        g.hazards.Add(new Hazard {position=g.Position,radius=1.05f,
            timer=.01f,damage=20,kind="rift",abyssStormOwned=true});
        float beforeHealth=g.save.health;
        CallCore(g,"TickExpansion",.02f);
        Check(g.AbyssWardShelter&&Math.Abs(g.save.energy-35f)<.001f&&
            Math.Abs(g.save.health-beforeHealth)<.001f&&g.sound=="guard"&&
            g.effects.Exists(e=>e.text=="WARD ENERGY"),
            "Real sheltered storm contact must absorb damage and restore exactly ten energy");

        // Recharge cannot overflow 100 or misreport a bonus at full energy.
        g.hazards.Clear();g.effects.Clear();g.save.energy=96;
        g.hazards.Add(new Hazard {position=g.Position,radius=1.05f,
            timer=.01f,damage=20,kind="rift",abyssStormOwned=true});
        CallCore(g,"TickExpansion",.02f);
        Check(Math.Abs(g.save.energy-100f)<.001f,
            "A second sheltered impact saturates energy without overflow");
        g.hazards.Clear();g.effects.Clear();
        g.hazards.Add(new Hazard {position=g.Position,radius=1.05f,
            timer=.01f,damage=20,kind="rift",abyssStormOwned=true});
        CallCore(g,"TickExpansion",.02f);
        Check(g.save.energy==100f&&!g.effects.Exists(e=>e.text=="WARD ENERGY"),
            "A full-energy hero gets no fake reward popup");

        // Boss rifts visually share the same kind but have different origin.
        g.hazards.Clear();g.effects.Clear();g.save.energy=25;
        g.invulnerable=0;g.save.health=100f;
        g.hazards.Add(new Hazard {position=g.Position,radius=1.05f,
            timer=.01f,damage=20,kind="rift",abyssStormOwned=false});
        CallCore(g,"TickExpansion",.02f);
        Check(g.save.energy==25f&&g.save.health<100f&&
            !g.effects.Exists(e=>e.text=="WARD ENERGY"),
            "Boss/mission-owned rifts retain real damage and cannot charge the pylon");

        Console.WriteLine("PASS Tuned pylon converts only real absorbed ambient strikes into capped energy");
    }

    private static void LiveTunedAbyssPylonShowsPhysicalShelterAndExit(FrontierRuntime app)
    {
        Prepare(app);
        var game=app.Game;game.save.english=true;
        game.save.frontierEnding=1;game.StartAbyss(0);
        game.save.abyssFloor=3;
        CallCore(game,"GenerateAbyss");
        game.enemies.Clear();game.hazards.Clear();game.projectiles.Clear();
        var ward=Array.Find(game.Map.objects,o=>o.kind=="abyssRiftWard");
        Check(ward!=null,"Production ward is placed in the live third-floor game map");
        game.save.x=ward.x;game.save.y=ward.y;game.save.runShards=3;
        game.Talk(ward);game.Choose(0);
        Check(game.AbyssWardShelter && game.save.runShards==0,
            "Actual physical interaction purchases and activates the shelter");
        Set(app,"camera",game.Position);
        GUI.commands.Clear();Call(app,"OnGUI");
        Check(GUI.commands.Exists(c=>c.Contains("PYLON SHELTER ACTIVE")),
            "Real world GUI announces the defensive area when hero stands beside the tuned pylon");
        game.save.x=ward.x+3;game.save.y=ward.y;
        Check(!game.AbyssWardShelter,"Leaving the 1.6-tile shelter radius disables the effect");
        Set(app,"camera",game.Position);
        GUI.commands.Clear();Call(app,"OnGUI");
        Check(GUI.commands.Exists(c=>c.Contains("ENTER GREEN SHELTER")) &&
            !GUI.commands.Exists(c=>c.Contains("PYLON SHELTER ACTIVE")),
            "World field label switches to directional shelter guidance outside the physical aura");
        Console.WriteLine("PASS Production Abyss pylon GUI presents real shelter enter and exit states");
    }

    private static void LiveAbyssRouteMenusBackToGateWithoutBanking(FrontierRuntime app)
    {
        Prepare(app);
        var game=app.Game;
        game.save.english=true;game.save.frontierEnding=1;
        game.StartAbyss(0);
        game.enemies.Clear();game.hazards.Clear();game.projectiles.Clear();
        game.save.flags.Add("abyss_clear_1");
        game.save.flags.Add("abyss_annex_circuit_complete_1");
        game.save.runCoins=23;game.save.runShards=5;
        var gate=Array.Find(game.Map.objects,o=>o.kind=="abyssGate");
        game.save.x=gate.x;game.save.y=gate.y;
        int beforeCoins=game.save.coins,beforeShards=game.save.shards;
        game.Talk(gate);
        game.Choose(Array.FindIndex(game.conversation.choices,
            c=>c.action=="abyssTestimonyList"));
        Check(game.mode=="dialog" &&
            game.conversation.choices[game.conversation.choices.Length-1].action=="abyssTestimonyGateMenu",
            "Actual testimony survey is a sourced child of the east gate");
        Input.keys.Add(KeyCode.Escape);Update(app);
        Check(game.mode=="dialog" &&
            game.conversation.choices[game.conversation.choices.Length-1].action=="abyssBank",
            "Escape returns from real testimony planner to its banking menu");
        game.Choose(Array.FindIndex(game.conversation.choices,
            c=>c.action=="abyssCircuitRoutes"));
        Check(game.mode=="dialog" &&
            game.conversation.choices[game.conversation.choices.Length-1].action=="abyssCircuitGateMenu",
            "Actual relay network is another sourced child of the east gate");
        Input.keys.Add(KeyCode.JoystickButton1);Update(app);
        Check(game.mode=="dialog" &&
            game.conversation.choices[game.conversation.choices.Length-1].action=="abyssBank" &&
            game.save.coins==beforeCoins && game.save.shards==beforeShards &&
            game.save.runCoins==23 && game.save.runShards==5,
            "Gamepad B returns to banking without committing either pending reward");
        game.Choose(Array.FindIndex(game.conversation.choices,
            c=>c.action=="abyssTestimonyList"));
        game.save.x=5;game.save.y=14;
        Input.keys.Add(KeyCode.Escape);Update(app);
        Check(game.mode=="world" && game.InAbyss &&
            game.save.runShards==5 && game.save.coins==beforeCoins,
            "Leaving a source before pressing Back cannot teleport to a stale gate interaction");
        Console.WriteLine("PASS Real Abyss gate testimony/circuit Esc and gamepad B back hierarchy");
    }

    private static void LiveSevenChoiceAbyssGateBanksFromSecondKeyboardPage(FrontierRuntime app)
    {
        Prepare(app);
        var game=app.Game;
        game.save.english=true;
        game.save.frontierEnding=1;
        game.StartAbyss(0);
        Check(game.InAbyss,"Real expedition starts using the existing campaign unlock");
        game.enemies.Clear();game.hazards.Clear();game.projectiles.Clear();
        game.save.flags.Add("abyss_clear_1");
        game.save.flags.Add("abyss_annex_circuit_complete_1");
        game.save.runCoins=19;game.save.runShards=7;
        var gate=Array.Find(game.Map.objects,o=>o.kind=="abyssGate");
        Check(gate!=null,"Production east banking gate is physically placed");
        game.save.x=gate.x;game.save.y=gate.y;
        int savedCoins=game.save.coins,savedShards=game.save.shards;
        game.Talk(gate);
        Check(game.mode=="dialog" && game.conversation.choices.Length==7 &&
            game.conversation.choices[6].action=="abyssBank",
            "Seven live choices place banking on page two, after boons, tracking and route networks");
        GUI.buttonCalls.Clear();Call(app,"OnGUI");
        int pages=DialogueLayout.Pages(game.conversation.textEn,
            game.save.largeText).Count;
        for(int p=1;p<pages;p++)Call(app,"AdvanceDialogue");
        GUI.commands.Clear();GUI.buttonCalls.Clear();Call(app,"OnGUI");
        Check(GUI.buttonCalls.FindAll(b=>b.rect.y>=539&&b.rect.y<670).Count==6,
            "First live gate page draws six actual production buttons");
        Input.keys.Add(KeyCode.PageDown);Update(app);
        GUI.commands.Clear();GUI.buttonCalls.Clear();Call(app,"OnGUI");
        Check((int)typeof(FrontierRuntime).GetField("dialogueChoicePage",Private).GetValue(app)==1 &&
            GUI.buttonCalls.FindAll(b=>b.rect.y>=539&&b.rect.y<670).Count==1,
            "PageDown exposes just the seventh actual gate choice inside the dialog card");
        Input.keys.Add(KeyCode.Alpha1);Update(app);
        Check(!game.InAbyss && game.save.room==0 &&
            game.save.coins==savedCoins+19 &&
            game.save.shards>=savedShards+7 &&
            game.save.runCoins==0 && game.save.runShards==0,
            "Page-two keyboard key 1 banks exactly once and returns to town, rather than activating a hidden boon");
        Console.WriteLine("PASS Real seven-option Abyss gate page-two keyboard banking");
    }

    private static void LateChronicleChoicePagesRemainPlayable(FrontierRuntime app)
    {
        Prepare(app);
        GameCore game = app.Game;
        game.save.english = true;
        // Unlock actual campaign chapter entries using their existing
        // progression flags, not fabricated button objects.
        string[] flags = { "witness_started_1", "core_1", "core_2",
            "core_3", "iron_temper_started", "triad_started",
            "blackout_started", "boss_4" };
        foreach (string flag in flags) game.save.flags.Add(flag);
        CallCore(game, "WorldChronicleSection", "campaign");
        Check(game.mode == "dialog" && game.conversation.choices.Length == 9,
            "The real unlocked chapter menu has eight entries plus back");
        string seventh = game.conversation.choices[6].action;
        Check(seventh.StartsWith("story_read_"),
            "Page-two entry is a genuine readable chapter, not a fake test choice");
        GUI.commands.Clear(); GUI.buttonCalls.Clear();
        Call(app, "OnGUI");
        Check((int)typeof(FrontierRuntime).GetField("dialogueChoicePage", Private).GetValue(app) == 0,
            "New conversation starts on the first real choice page");
        Check(GUI.buttonCalls.FindAll(b=>b.rect.y>=539 && b.rect.y<670).Count==6,
            "First choice page draws exactly six reachable controls inside the dialog");
        Check(!GUI.commands.Exists(c=>c.Contains(
                game.conversation.choices[6].en)),
            "No hidden off-page story choice is painted beyond the dialog");
        Input.keys.Add(KeyCode.PageDown); Update(app);
        GUI.commands.Clear(); GUI.buttonCalls.Clear(); Call(app, "OnGUI");
        Check((int)typeof(FrontierRuntime).GetField("dialogueChoicePage", Private).GetValue(app)==1 &&
            GUI.buttonCalls.FindAll(b=>b.rect.y>=539 && b.rect.y<670).Count==3,
            "PageDown shows the final three real chapter actions within the panel");
        Check(GUI.commands.Exists(c=>c.Contains(game.conversation.choices[6].en)),
            "Seventh chapter becomes visible on the actual second page");
        Input.keys.Add(KeyCode.Alpha1); Update(app);
        Check(game.mode=="dialog" && game.conversation != null &&
            game.conversation.choices.Length <= 5 &&
            game.conversation.choices[0].action == "story_read_triad",
            "Visible key 1 activates absolute choice seven, not hidden choice one");
        Console.WriteLine("PASS Long real chronicle menu pages and keyboard selects visible late chapters");
    }


    private static void ThreePageChronicleSupportsPointerHomeEndAndRealBack(FrontierRuntime app)
    {
        Prepare(app);
        var game=app.Game;
        game.save.english=true;
        string[] flags={
            "survey_complete", "archive_complete", "root_harmony_started",
            "commons_relay_started", "skywatch_started", "afterlight_started",
            "storm_started", "hearing_started", "roadcharter_started",
            "last_directive_started", "civic_conduit_started",
            "civic_conduit_complete" };
        foreach(string flag in flags)game.save.flags.Add(flag);
        game.save.frontierEnding=1;game.save.ending=1;
        CallCore(game,"WorldChronicleSection","after");
        int count=game.conversation.choices.Length;
        Check(count>=13 &&
            game.conversation.choices[count-1].action=="story_menu",
            "A late-game native chapter listing truly spans at least three choice pages");
        GUI.commands.Clear();GUI.buttonCalls.Clear();Call(app,"OnGUI");
        // Pointer presses run through the same production Button() hit path.
        GUI.queuedClick=new Rect(920,682,231,34);
        Call(app,"OnGUI");
        Check(GUI.queuedClick==null &&
            (int)typeof(FrontierRuntime).GetField("dialogueChoicePage",Private).GetValue(app)==1,
            "Visible mouse Next advances the real nine-plus-choice chronicle");
        Input.keys.Add(KeyCode.End);Update(app);
        GUI.commands.Clear();GUI.buttonCalls.Clear();Call(app,"OnGUI");
        int lastPage=(int)typeof(FrontierRuntime).GetField("dialogueChoicePage",Private).GetValue(app);
        Check(lastPage==2 && GUI.buttonCalls.FindAll(b=>
            b.rect.y>=539&&b.rect.y<670).Count==count-12,
            "End reveals the last page without painting choices outside its card");
        Input.keys.Add(KeyCode.Home);Update(app);
        Call(app,"OnGUI");
        Check((int)typeof(FrontierRuntime).GetField("dialogueChoicePage",Private).GetValue(app)==0,
            "Home restores the original first page and navigation scope");
        Input.keys.Add(KeyCode.End);Update(app);
        Call(app,"OnGUI");
        // The final entry is the real chapter-back action, not a duplicate.
        Input.keys.Add((KeyCode)((int)KeyCode.Alpha1+count-13));
        Update(app);
        Check(game.mode=="dialog"&&game.conversation.choices.Length==8 &&
            game.conversation.choices[0].action=="story_section_origins" &&
            game.conversation.choices[1].action=="story_section_campaign" &&
            game.conversation.choices[4].action=="story_catchup" &&
            game.conversation.choices[7].action=="close",
            "Last-page numeric shortcut returns to the canonical four chapters plus new readable story guides");
        Console.WriteLine("PASS Three-page late chronicle pointer, Home/End and real final back action");
    }


    private static void ChronicleBackReturnsThroughActualUnlockedChapters(FrontierRuntime app)
    {
        Prepare(app);
        var game=app.Game;
        string[] flags={ "witness_started_1", "core_1", "core_2",
            "core_3", "iron_temper_started", "triad_started",
            "blackout_started", "boss_4" };
        foreach(string flag in flags)game.save.flags.Add(flag);
        CallCore(game,"WorldChronicleSection","campaign");
        GUI.commands.Clear();GUI.buttonCalls.Clear();Call(app,"OnGUI");
        int originalShards=game.save.shards;
        int originalCoins=game.save.coins;
        Input.keys.Add(KeyCode.PageDown);Update(app);Call(app,"OnGUI");
        Check(game.mode=="dialog" && game.conversation.choices.Length==9,
            "Late chapter directory remains a genuine paginated interaction");
        Input.keys.Add(KeyCode.JoystickButton1);Update(app);
        Check(game.mode=="dialog" &&
            game.conversation.choices[0].action=="story_section_origins",
            "Controller B returns from the real campaign section to chronicle index");
        game.Choose(1); // Reopen the original campaign section.
        Check(game.mode=="dialog" && game.conversation.choices.Length==9,
            "Backtracking never clears the actual unlocked story chapters");
        game.Choose(0); // Read a real unlocked chapter.
        Check(game.mode=="dialog" &&
            Array.Exists(game.conversation.choices,c=>c.action=="story_section_campaign"),
            "The chronicle page offers its existing chapter parent");
        Input.keys.Add(KeyCode.Escape);Update(app);
        Check(game.mode=="dialog" &&
            game.conversation.choices[0].action=="story_read_testimonies",
            "Escape from a story page returns to its original chapter menu");
        Input.keys.Add(KeyCode.JoystickButton1);Update(app);
        Check(game.mode=="dialog" &&
            game.conversation.choices[0].action=="story_section_origins",
            "Controller B from a chapter climbs one level, never silently exits");
        Input.keys.Add(KeyCode.Escape);Update(app);
        Check(game.mode=="world" && game.save.shards==originalShards &&
            game.save.coins==originalCoins,
            "Escape from the top-level archive closes normally without rewards or save mutations");
        Console.WriteLine("PASS Real chronicle chapter/page B and Escape back hierarchy");
    }


    private static void ControllerShouldersPageRealChronicleAndSelectVisibleItem(FrontierRuntime app)
    {
        Prepare(app);
        var game=app.Game;
        game.save.english=true;
        string[] flags={ "witness_started_1", "core_1", "core_2",
            "core_3", "iron_temper_started", "triad_started",
            "blackout_started", "boss_4" };
        foreach(string flag in flags)game.save.flags.Add(flag);
        CallCore(game,"WorldChronicleSection","campaign");
        Call(app,"OnGUI");
        int originalCoins=game.save.coins;
        Input.keys.Add(KeyCode.JoystickButton5);Update(app);
        GUI.commands.Clear();GUI.buttonCalls.Clear();Call(app,"OnGUI");
        Check((int)typeof(FrontierRuntime).GetField("dialogueChoicePage",Private).GetValue(app)==1 &&
            GUI.commands.Exists(s=>s.Contains("Next RB")),
            "Gamepad RB moves to the second real choice page with a controller-aware hint");
        Input.keys.Add(KeyCode.JoystickButton4);Update(app);
        Call(app,"OnGUI");
        Check((int)typeof(FrontierRuntime).GetField("dialogueChoicePage",Private).GetValue(app)==0,
            "Gamepad LB restores page one instead of switching unrelated menus");
        Input.keys.Add(KeyCode.JoystickButton5);Update(app);Call(app,"OnGUI");
        // Update queues focused activation; the production IMGUI pass
        // consumes that queued A press when it registers the real button.
        Input.keys.Add(KeyCode.JoystickButton0);Update(app);Call(app,"OnGUI");
        Check(game.mode=="dialog" &&
            game.conversation.choices[0].action=="story_read_triad" &&
            game.save.coins==originalCoins,
            "Gamepad A selects the first visible entry on page two (absolute index six)");
        Console.WriteLine("PASS Long real chronicle supports LB/RB paging and gamepad A");
    }

    private static void DialogueChoiceFocusMatchesActualRenderedButton(FrontierRuntime app)
    {
        foreach (int total in new[] { 7, 8, 13 })
        {
            Prepare(app);
            int page = (total - 1) / 6;
            int firstOnPage = page * 6;
            var options = new Choice[total];
            for (int i = 0; i < total; i++)
                options[i] = new Choice("选择 " + i, "Choice " + i,
                    i == firstOnPage ? "close" : "unused");
            app.Game.mode = "dialog";
            app.Game.conversation = new Conversation("港口记录", "Harbor log",
                "查看此页的选项。", "Choose an entry on this page.", options);
            Set(app, "lastConversation", app.Game.conversation);
            Set(app, "dialoguePage", 0);
            Set(app, "dialogueChoicePage", page);
            GUI.commands.Clear(); GUI.buttonCalls.Clear(); Call(app, "OnGUI");
            int visible = total - firstOnPage;
            float width = visible == 1 ? 832f : 409f;
            string expected = (string)Call(app, "ControlId", 320f, 539f, width, 37f);
            var focus = (MenuNavigation)typeof(FrontierRuntime)
                .GetField("navigation", Private).GetValue(app);
            Check(focus.focused == expected && focus.CanActivate(expected),
                "Real page " + page + " of " + total +
                " choices must focus its actual first clickable button");
            Input.keys.Add(KeyCode.JoystickButton0); Update(app); Call(app, "OnGUI");
            Check(app.Game.mode == "world",
                "Controller A activates that last-page button, never a phantom half-width target");
        }
        // Replacing an old paged conversation must synchronize its focus
        // identity BEFORE the new speaker's first IMGUI draw.
        Prepare(app);
        var game = app.Game;
        game.mode = "dialog";
        game.conversation = new Conversation("新说话者", "New speaker",
            "新的对话。", "A new conversation.", new Choice("结束", "Close", "close"));
        Set(app, "lastConversation", new Conversation("旧说话者", "Previous speaker",
            "旧页。", "Old page."));
        Set(app, "dialoguePage", 3);
        Set(app, "dialogueChoicePage", 2);
        GUI.commands.Clear(); GUI.buttonCalls.Clear(); Call(app, "OnGUI");
        Check((int)typeof(FrontierRuntime).GetField("dialoguePage", Private).GetValue(app) == 0 &&
              (int)typeof(FrontierRuntime).GetField("dialogueChoicePage", Private).GetValue(app) == 0,
              "New speaker always starts on page one on its first visible frame");
        var navigation = (MenuNavigation)typeof(FrontierRuntime)
            .GetField("navigation", Private).GetValue(app);
        string first = (string)Call(app, "ControlId", 320f, 539f, 832f, 37f);
        Check(navigation.focused == first && navigation.CanActivate(first),
            "New speaker first-frame focus matches its actual full-width choice");
        Input.keys.Add(KeyCode.JoystickButton0); Update(app); Call(app, "OnGUI");
        Check(game.mode == "world",
            "New speaker first-frame controller choice closes the intended conversation");
        Console.WriteLine("PASS One/two-option trailing dialogue pages and new-speaker first-frame controller focus");
    }

    private static void DialogueReaderCanStepBackwardWithoutTriggeringChoices(FrontierRuntime app)
    {
        Prepare(app);
        var game = app.Game;
        // Other UI suites toggle localization and accessibility. This test
        // must use its own known layout, not inherit a previous case's save.
        game.save.english = false;
        game.save.largeText = false;
        int coins = game.save.coins;
        int flags = game.save.flags.Count;
        game.mode = "dialog";
        game.conversation = new Conversation("边境记录", "Frontier record",
            new string('星', 850), new string('a', 850),
            new Choice("离开", "Leave", "close"));
        int pages = DialogueLayout.Pages(game.conversation.text, false).Count;
        Check(pages >= 3, "Reader regression must use a genuinely long multi-page dialogue");
        GUI.commands.Clear(); GUI.buttonCalls.Clear(); Call(app, "OnGUI");
        Check((int)typeof(FrontierRuntime).GetField("dialoguePage", Private).GetValue(app) == 0,
            "New record opens on its first page");
        Input.keys.Add(KeyCode.Space); Update(app); Call(app, "OnGUI");
        Check((int)typeof(FrontierRuntime).GetField("dialoguePage", Private).GetValue(app) == 1,
            "Space genuinely advances one text page");
        // Backspace is KeyCode 8 in Unity, not a movement key. Guard
        // against an accidental alias in the offline key declaration.
        Check(KeyCode.Backspace != KeyCode.W,
            "Offline Backspace must not alias the W movement key");
        Input.keys.Add(KeyCode.W); Update(app); Call(app, "OnGUI");
        Check((int)typeof(FrontierRuntime).GetField("dialoguePage", Private).GetValue(app) == 1,
            "Ordinary movement-key presses must never rewind story pages");
        Input.keys.Add(KeyCode.PageUp); Update(app); Call(app, "OnGUI");
        Check((int)typeof(FrontierRuntime).GetField("dialoguePage", Private).GetValue(app) == 0,
            "PageUp returns to the preceding text page");
        Input.keys.Add(KeyCode.Space); Update(app); Call(app, "OnGUI");
        Input.keys.Add(KeyCode.Backspace); Update(app); Call(app, "OnGUI");
        Check((int)typeof(FrontierRuntime).GetField("dialoguePage", Private).GetValue(app) == 0,
            "Backspace also returns one page without closing the conversation");
        Input.keys.Add(KeyCode.Space); Update(app); Call(app, "OnGUI");
        GUI.queuedClick = new Rect(686, 494, 178, 34);
        Call(app, "OnGUI");
        Check(!GUI.queuedClick.HasValue &&
            (int)typeof(FrontierRuntime).GetField("dialoguePage", Private).GetValue(app) == 0,
            "Production IMGUI Previous button moves back a page on real pointer activation");
        Check(game.mode == "dialog" && game.conversation != null &&
              game.save.coins == coins && game.save.flags.Count == flags,
              "Reading backwards does not silently fire choices or mutate campaign rewards");
        Console.WriteLine("PASS Long dialogue PageUp, Backspace and production pointer previous-page navigation");
    }


    private static void ControllerBRewindsLongPhysicalStoryWithoutClosingOrChoosing(FrontierRuntime app)
    {
        Prepare(app);
        var game=app.Game;
        game.save.english=false;game.save.largeText=false;
        int coins=game.save.coins, flags=game.save.flags.Count;
        game.mode="dialog";
        game.conversation=new Conversation("求援记录","Rescue record",
            new string('星',850),new string('a',850),
            new Choice("结束","Close","close"));
        Call(app,"OnGUI");
        Check(DialogueLayout.Pages(game.conversation.text).Count>=3,
            "Real controller reader fixture spans multiple authored text cards");
        for(int i=0;i<2;i++)
        {
            Input.keys.Add(KeyCode.JoystickButton0);
            Update(app);Call(app,"OnGUI");
        }
        Check((int)typeof(FrontierRuntime).GetField("dialoguePage",Private).GetValue(app)==2,
            "Controller A truly advances to the third dialogue card");
        Input.keys.Add(KeyCode.JoystickButton1);Update(app);Call(app,"OnGUI");
        Check(game.mode=="dialog" &&
            (int)typeof(FrontierRuntime).GetField("dialoguePage",Private).GetValue(app)==1,
            "Controller B returns one card instead of throwing away the conversation");
        Input.keys.Add(KeyCode.JoystickButton1);Update(app);Call(app,"OnGUI");
        Check(game.mode=="dialog" &&
            (int)typeof(FrontierRuntime).GetField("dialoguePage",Private).GetValue(app)==0,
            "Controller B can reach the first card without selecting any answer");
        Input.keys.Add(KeyCode.JoystickButton1);Update(app);
        Check(game.mode=="world" && game.save.coins==coins &&
            game.save.flags.Count==flags,
            "A final B on the first card exits normally, with no phantom quest actions");
        Prepare(app);
        game=app.Game;game.mode="dialog";
        game.conversation=new Conversation("漫长来信","Long letter",
            new string('星',850),new string('a',850),
            new Choice("离开","Leave","close"));
        Call(app,"OnGUI");
        Input.keys.Add(KeyCode.JoystickButton0);Update(app);Call(app,"OnGUI");
        Input.keys.Add(KeyCode.Escape);Update(app);
        Check(game.mode=="world",
            "Keyboard Escape remains a direct dismiss even on a later story card");
        Console.WriteLine("PASS Controller B rewinds actual dialogue text; first-card B and Escape close safely");
    }

    // Exercises the same bilingual reader used in Frontier.unity, not an
    // alternate test-only pagination widget. Narrative and choices survive.
    private static void DialoguePaginationPreservesBilingualStoryAndChoice(FrontierRuntime app)
    {
        var chinese=new StringBuilder();
        var english=new StringBuilder();
        for(int i=0;i<34;i++)
        {
            chinese.Append("巡夜人说：“东门的灯还亮着。”我们沿城墙继续巡查。");
            english.Append("The watch captain checked the eastern gate before dawn. ");
        }
        string original=chinese.ToString(),translated=english.ToString();
        var cn=DialogueLayout.Pages(original);
        var en=DialogueLayout.Pages(translated);
        Check(cn.Count>1 && en.Count>1,"Both shipped localizations must require genuine reader pagination");
        Check(string.Concat(cn.ToArray())==original && string.Concat(en.ToArray())==translated,
            "Every sentence, quote, punctuation mark and space survives dialogue page turns");
        Check(DialogueLayout.Pages(original,true).Count>=cn.Count &&
              DialogueLayout.Pages(translated,true).Count>=en.Count,
            "Accessible large text must never be allocated fewer pages");
        for(int i=1;i<cn.Count;i++)
            Check("，。、！？：；）】”’」』".IndexOf(cn[i][0])<0,
                "Chinese closing punctuation and quotation cannot begin a reading page");
        for(int i=1;i<en.Count;i++)
            Check(!(char.IsLetter(en[i-1][en[i-1].Length-1])&&char.IsLetter(en[i][0])),
                "A normal-length English word cannot be torn in half across pages");
        string unicode=new string('星',340)+"🕯️\r\n"+new string('光',360);
        var unicodePages=DialogueLayout.Pages(unicode);
        Check(string.Concat(unicodePages.ToArray())==unicode,
            "Astral glyphs, combining marks and explicit CRLF remain lossless");
        for(int i=1;i<unicodePages.Count;i++)
            Check(!char.IsLowSurrogate(unicodePages[i][0]) &&
                  !char.IsHighSurrogate(unicodePages[i-1][unicodePages[i-1].Length-1]) &&
                  !(unicodePages[i-1].EndsWith("\r") && unicodePages[i].StartsWith("\n")),
                "A page may never bisect a Unicode surrogate or CRLF pair");

        Prepare(app);
        var game=app.Game;
        game.save.english=false;game.save.largeText=false;
        game.mode="dialog";
        game.conversation=new Conversation("巡夜人","Watch captain",original,translated,
            new Choice("我去检查东门","I'll check the gate","close"));
        GUI.commands.Clear();GUI.buttonCalls.Clear();Call(app,"OnGUI");
        Check(GUI.buttonCalls.FindAll(b=>b.rect.y>=539 && b.rect.y<670).Count==0,
            "The production UI cannot expose a conversation response before its full story");
        // Repaint reuses the actual page objects, but preferences and
        // localization must invalidate them immediately, without an NPC swap.
        var pageField=typeof(FrontierRuntime).GetField("dialoguePages",Private);
        object originalLayout=pageField.GetValue(app);
        Call(app,"OnGUI");
        Check(System.Object.ReferenceEquals(originalLayout,pageField.GetValue(app)),
            "An unchanged production NPC repaint must reuse its page layout");
        game.save.largeText=true;Call(app,"OnGUI");
        object accessibleLayout=pageField.GetValue(app);
        Check(!System.Object.ReferenceEquals(originalLayout,accessibleLayout),
            "Turning on large dialogue text rebuilds the real reader immediately");
        game.save.english=true;Call(app,"OnGUI");
        var englishLayout=(System.Collections.Generic.List<string>)pageField.GetValue(app);
        Check(!System.Object.ReferenceEquals(accessibleLayout,englishLayout) &&
              string.Concat(englishLayout.ToArray())==translated,
            "Switching language in the same conversation rebuilds translated pages");
        game.save.largeText=false;game.save.english=false;
        Call(app,"OnGUI");
        for(int i=1;i<cn.Count;i++)Call(app,"AdvanceDialogue");
        GUI.commands.Clear();GUI.buttonCalls.Clear();Call(app,"OnGUI");
        Check(GUI.buttonCalls.FindAll(b=>b.rect.y>=539 && b.rect.y<670).Count==1,
            "The final page must still reveal its real actionable response");
        Console.WriteLine("PASS Bilingual narrative boundaries, complete text and production dialogue choice");
    }

    private static void PlantedAllyRescueArrowGuardHasHonestBilingualWorldCue(FrontierRuntime app)
    {
        Prepare(app,1);
        var g=app.Game;g.Enter(1);g.mode="world";g.enemies.Clear();
        g.save.flags.Add("companion_1");g.SelectCompanion(1);
        g.save.x=16.5f;g.save.y=10.5f;
        g.companionPosition=new Point(12.5f,10.5f);
        g.reliefMissionRoom=1;g.reliefMissionWave=3;
        g.reliefMissionTime=70f;g.reliefFollowerSafety=70f;
        g.reliefFollowerPosition=new Point(13.75f,10.5f);
        g.reliefFollowerWaiting=true;
        g.ToggleCompanionHold();
        Check(g.ReliefCompanionArrowGuardReady,
            "Real hold and current civilian geometry arm world arrow guard");
        g.save.english=true;GUI.commands.Clear();Call(app,"OnGUI");
        Check(GUI.commands.Exists(c=>c.Contains("ALLY GUARD READY"))&&
              GUI.commands.Exists(c=>c.Contains("ARROW GUARD")),
            "Actual combat and world-space GUIs show an eligible English planted ally");
        var guardCooldown=typeof(GameCore).GetField(
            "reliefCompanionArrowGuardCooldown",Private);
        Check(guardCooldown!=null,"Live companion guard is a transient C# cooldown");
        guardCooldown.SetValue(g,2.4f);
        GUI.commands.Clear();Call(app,"OnGUI");
        Check(GUI.commands.Exists(c=>c.Contains("GUARD RECHARGE 2.4s"))&&
              GUI.commands.Exists(c=>c.Contains("GUARD IN 2.4s"))&&
              !GUI.commands.Exists(c=>c.Contains("ALLY GUARD READY")),
            "A spent interception paints its genuine live recharge rather than a fake ready badge");
        g.save.english=false;GUI.commands.Clear();Call(app,"OnGUI");
        Check(GUI.commands.Exists(c=>c.Contains("挡箭冷却 2.4s")),
            "The same spent guard presents truthful Chinese recharge");
        g.ToggleCompanionHold();
        GUI.commands.Clear();Call(app,"OnGUI");
        Check(!GUI.commands.Exists(c=>c.Contains("挡箭冷却"))&&
              !GUI.commands.Exists(c=>c.Contains("挡箭就绪"))&&
              !GUI.commands.Exists(c=>c.Contains("ARROW GUARD")),
            "Real companion recall removes the actual planted world guard, with no phantom indicator");
        Console.WriteLine("PASS Planted civilian arrow guard world and compact combat HUD in both languages");
    }

    private static void LiveRescueCombatHudKeepsSafetyThreatAndAllyCareVisible(FrontierRuntime app)
    {
        Prepare(app,1);
        var g=app.Game;
        g.Enter(1);g.mode="world";g.enemies.Clear();g.projectiles.Clear();
        g.save.english=false;
        g.reliefMissionRoom=1;g.reliefMissionWave=3;
        g.reliefMissionTime=64.8f;g.reliefFollowerSafety=38f;
        g.reliefFollowerPosition=new Point(15.5f,12.5f);
        g.reliefFollowerWaiting=true;
        GUI.commands.Clear();Call(app,"OnGUI");
        Check(GUI.commands.Exists(c=>c.Contains("居民救援"))&&
              GUI.commands.Exists(c=>c.Contains("护送返程"))&&
              GUI.commands.Exists(c=>c.Contains("居民安全"))&&
              GUI.commands.Exists(c=>c.Contains("38%")),
            "Real world combat HUD shows active Chinese escort stage, countdown and wounded resident safety");
        var region=(System.Collections.Generic.List<Rect>)Call(app,"HudRegions");
        Check(region.Exists(rect=>rect.x==380&&rect.y==22&&rect.width==520),
            "Compact rescue HUD participates in the same live warning-overlay bounds as other defended objectives");
        g.save.english=true;GUI.commands.Clear();Call(app,"OnGUI");
        Check(GUI.commands.Exists(c=>c.Contains("CIVILIAN RESCUE"))&&
              GUI.commands.Exists(c=>c.Contains("ESCORT HOME"))&&
              GUI.commands.Exists(c=>c.Contains("CIVILIAN SAFETY")),
            "English combat mode keeps the same true safety and rescue objective");
        Check(!GUI.commands.Exists(c=>c.Contains("COVER HIT!")),
            "No arrow hit means no misleading claimed rescue success");
        g.reliefMissionRoom=0;
        GUI.commands.Clear();Call(app,"OnGUI");
        Check(!GUI.commands.Exists(c=>c.Contains("CIVILIAN RESCUE")),
            "Finished or abandoned rescue cannot leave a phantom combat objective");
        Console.WriteLine("PASS Genuine bilingual rescue combat HUD and real warning-safe screen bounds");
    }

    private static int[] EnumerableIndices(int count)
    {
        int[] result=new int[count];
        for(int i=0;i<count;i++)result[i]=i;
        return result;
    }

    public static int Main()
    {
        var app = new FrontierRuntime(); Call(app, "Awake");
        if (app.Game == null) { Console.WriteLine("FAIL Runtime Awake did not load the shipped campaign"); return 1; }
        Action<FrontierRuntime>[] cases = { TacticalCompanionPostMouseClickDoesNotOverwritePersonalPin, CompanionMenuOpensPhysicalPostSelectorWithoutAlteringParty, TacticalCompanionPostMapControlsRespectRealPhysicalOrders, MapKeyboardPadCanRepositionSavedPinWithoutClearingOrLongWalk, LiveMapTilePreviewUsesPhysicalRouteAndRepaintCache, LocalMapClickPinDrawsRealWorldRouteAndCanClear, LiveSprintKeyAndL3MaintainPhysicalHudAndFatigue, RealWayfarerCoverIsVisibleInWorldAndJournal, InnTravelerOffersSourceBoundRoadTopicsAndPreservesChronicle, InnTravelerJobBoardTracksOnlyOpenUnfinishedCampaignWork, JournalRemoteQuestFollowsRealRegionTransition, RealOuterShoreNavigationFollowsActualTransitLegs, WorldAtlasTrackAndFollowIsExplicitAndAtomic, InspectedRemoteMapQuestCanAcquireRealCrossRegionRoute, SealedArrivalNeverPretendsRemoteQuestIsReachable, SwitchingTrackedQuestImmediatelyRetiresRemoteRoute, NearbyNpcHudMatchesRealKeyboardInteraction, BrightHighlandServiceHasHonestWorldVisual, BankedFrostEmberMovesWorldIndicatorToHearth, ManualCompanionFocusDisplaysAndRetiresRealWorldMarker, LiveSpecialistClassTrialUsesActualTargetAndGameView, RemoteSoupRouteDrawsLiveShelterDeliveryInRealWorld, GardenMedicConversationFollowsClinicAndCropState, ForgeDialogueAcknowledgesPumpRepairAndVeteranTraining, ActualTownCommissionHandoffStaysWithOriginalSpeaker, PublicKitchenRealNpcAndBilingualFoodInventory, PrecisionFishingInLiveGameViewDistinguishesBothRealRules, LiveFrostCompanionEmberScreenIsVisibleInCombatHud, LiveMinecartBodyguardIsVisibleInRealWorldHud, LiveTideSalvageCombatHudPreservesMissionAndThreat, MineConvoySafeArrivalHoldsUntilPhysicalHandoff, CompanionBlockedRouteCueIsWorldBoundAndBilingual, PublishedCivicFieldAidHasAnHonestWorldMarker, StewCooldownAndAttackBuffAreVisibleInActualBilingualHud, LiveCourierMedicineQualityShowsWithoutCoveringWard, LiveCourierAmbushNeverSpawnsOnCompanion, CourierRaidersRequireReachableLiveSpawn, SealedEscortRouteRejectsFalseNearSideDestination, CivilianEscortNavigatesRealWallDetour, EscortDodgeChoosesFarSideOfLivingAttacker, SealedEscortDodgeCannotSpendEnergyOrTeleport, CivilianEvadeUsesRealContactAndCostsOnlyWhenMoved, LivingEscortStallWarnsOnlyOnRealBlockedMovement, EvacuationFinalHandoffRequiresBothLivingActorsOnOpenSide, NoPhantomRescueMarkersWithoutMission, ActorOriginNavigationPreservesHeroAndCollision, LowSafetyEscortUsesReachableShelterBeforeReturn, TimedRescueCompassOverridesPinnedQuestWithoutMutatingIt, ShelterRestCommandHealsRealEscortOnlyWhenSecure, ActualCivilianClimateSheltersCannotPassThroughWalls, RepairedClimateStationProtectsOnlyItsPhysicalSide, LiveOasisShadeRestIsPhysicalAndSafe, CriticalDesertHeatGuidesActualReachableShade, CriticalAlpineColdGuidesReachablePhysicalBrazier, RealAlpineBrazierRestRequiresSafePhysicalContact, LitAlpineBraziersGivePhysicalSafeRest, LiveAffordableWardUsesActualCaveNavigation, LiveWardRestoresEnergyOnlyFromPhysicalAmbientImpact, LiveTunedAbyssPylonShowsPhysicalShelterAndExit, LiveAbyssRouteMenusBackToGateWithoutBanking, LiveSevenChoiceAbyssGateBanksFromSecondKeyboardPage, LateChronicleChoicePagesRemainPlayable, ThreePageChronicleSupportsPointerHomeEndAndRealBack, ChronicleBackReturnsThroughActualUnlockedChapters, ControllerShouldersPageRealChronicleAndSelectVisibleItem, DialogueChoiceFocusMatchesActualRenderedButton, DialogueReaderCanStepBackwardWithoutTriggeringChoices, ControllerBRewindsLongPhysicalStoryWithoutClosingOrChoosing, DialoguePaginationPreservesBilingualStoryAndChoice, LiveYKeyOrdersPhysicalCompanionWatchAndRestoresFollow, LivePKeyHoldsAndReleasesPhysicalCompanion, LiveRBButtonTapAndHoldCompanionOrders, LiveXKeyPatchCannotRepairRemoteOrTwice, LiveVKeyBarricadeUsesRealOutpostGuard, AtomicHeroContactImportRecovery, FreshTechniqueWinsHeldAttack, QueuedTechniqueKeepsAimAndRunsOnce,
            DashAndTechniqueShareAFrame, HudQLateCooldownInputTriggersOneRealSkill, HudTechniqueCanQueue, ReducedMotionHeroInvulnerabilityStaysVisibleWithoutStrobing, CanonicalHeroHitRecoversOnSameWholeBody, CanonicalCombatRecoverySettlesWithoutExtraBody, CombatSettleNeverMasksNewPhysicalAction, InvalidPhysicalContactCannotFakeHeroSettle, QueuedAreaPreviewDrawsRealWorldRadius, QueuedAreaTagsOnlyPhysicalLivingEnemies, RealProfessionQHitsTintOnlyDamageConfirmedContact, RealBasicWeaponHitsRetainFlightElementAfterEquipmentSwap, MeleeSwordContactStopsAtRealSolidCover, ProjectileWallContactKeepsOriginalSourceLight, EnemyArcherRepositionsForTrueArrowRadiusAndCancelsNewCover, ReleasedAreaVfxStopsAtActualWorldCover, ExplosiveWallSplashDrawsOnlyPhysicalNearSide, QueuedRangerVolleyMarksLiveObstructedTarget, RiposteHudFollowsRealParryIntentAndUnreleasedContact, RepairedReliefSignalShowsOnlyGenuineWorldRest, UrgentRoadwardenDeliveryUsesOriginalWorldWaypointsAndLabels, RealSignalSideCampCueRequiresMaterialSafeAndReachableMast, ActualRepairedSignalRoadOnlyShowsAtSafeLinkedWorldMast, ActualBossSupportCallIsVisibleAndRetractsAfterCombatBreak, BossSupportCannotSpawnAcrossAnIsolatedArena, NearWallBossSummonDoesNotBypassPhysicalArenaIsolation, SealedBossReleaseCannotPlantHazardsAcrossNewWalls, LockedFrostBossStrikeRemainsDodgeableAfterPlayerMoves, BossSecondaryStrikesStayOnCasterSideOfLiveCover, CompanionAutoDefenseDistinguishesSealedFromLiveBossAttacks, MalformedBossIntentCancelsAndRecoversRealCombat, RealGroundHazardsRejectCorruptTransientContacts, DefeatedBossRetiresOnlyItsOwnDelayedGroundAttacks, SpawnAndRecoveredAbyssEnemiesDoNotStackOnOneFoot, AbyssSaveRecoveryAvoidsHeroAndCompanionMaterialization, RisingMistportTideSeparatesLiveCombatantsOnDryBanks, FloodRescueCivilianEvacuatesWithoutMaterializingInsideCombatants, LiveEscortResidentOwnsFootAgainstFreshFoesAndBossSupport, CrowdedCombatCorridorFindsDistantConnectedFootWithoutWallTeleport, CivicReserveCombatHudTracksGenuineReceiverObjective, LiveRescueCombatHudKeepsSafetyThreatAndAllyCareVisible, PlantedAllyRescueArrowGuardHasHonestBilingualWorldCue, MenuAndApplicationPauseDiscardIntent,
            ReleasedTechniqueDrawsContactFrame,
            DirectionalLmbAndSkillContactNeverFlashBeforeRelease,
            ActorRegistrationChecks.VerifyUnifiedGaitPixelComposites,
            ActorRegistrationChecks.VerifyBossMovingFlash,
            ActorRegistrationChecks.VerifyBossWholeBodySizeAcrossIdleRun,
            ActorRegistrationChecks.VerifyEnemySingleBodyHandoffs,
            ActorRegistrationChecks.VerifyGpuOnlyMonsterHitAndSelectionFallback,
            ActorRegistrationChecks.VerifyResidentAnimation,
            ActorRegistrationChecks.VerifyNpcActionSignalProduction,
            ActorRegistrationChecks.VerifyHeroStopPlantContinuity,
            ActorRegistrationChecks.VerifyEnemyStopPlantContinuity,
            ActorRegistrationChecks.VerifyCompanionGaitContinuity,
            ActorRegistrationChecks.VerifyOriginalEnemyReleaseDust,
            ActorRegistrationChecks.VerifyAuthoredEnemyHitSparks,
            ActorRegistrationChecks.VerifyMaterialGroundContactFrames,
            ActorRegistrationChecks.VerifyBufferedDodgeWorldCue,
            ActorRegistrationChecks.VerifyDodgeHudEarlyClick,
            ActorRegistrationChecks.VerifyQueuedSkillHudSingleDispatch,
            RenderStateChecks.VerifyLiveVisualLookResource,
            RenderStateChecks.VerifyPaintedShorelineFoamInLiveWorld,
            RenderStateChecks.VerifyOriginalWaterRippleArtAndLiveTide,
            RenderStateChecks.VerifyPaintedCampfireFlamesInProduction,
            RenderStateChecks.VerifyPropVaporsInProduction,
            RenderStateChecks.VerifyLivingGardenInProduction,
            RenderStateChecks.VerifyAuthoredProjectileTrailsAndHotImport,
            RenderStateChecks.VerifyReducedMotionContactEffectsHaveStableSourceScale,
            RenderStateChecks.VerifyOrdinaryMeleeWarningRespectsRealWallCover,
            RenderStateChecks.VerifyEnemyAreaWarningHonorsRealWallCover,
            RenderStateChecks.VerifyCastSigilSourceAndCombatWarning,
            RenderStateChecks.VerifyRelicArtAndWorldIllumination,
            RealMovingSpellCompositesAndBossFrames, HeroActionAccentsFollowRealDirectionalWeaponMount,
            AllEquippedBodiesRemainCanonicalDuringAttackAndSkill,
            CanonicalHeroNeverDependsOnLegacyPlayerImports,
            LiveEquipmentPreviewUsesTheSameCanonicalBody,
            ProduceVerifiedGameplayHeroTrace, GroundContactArtSharesPhysicalFootPivot,
            OriginalAbilityAndStewIconsUsePublishedArt,
            OriginalCombatVfxTransitionsUseActualPaintedFrames,
            RecoveryWalkingRestoresEquippedDrawing,
            StrafeDuringReleasedCastKeepsBodyAndAim,
            RecoveryHitAndDeathRetainVisibleEquipment, CombatOpeningsAndPoiseDrawInActualWorld,
            RenderStateChecks.MapRasterRegression, RejectedRegionLoadReturnsToRealInn, SaveRecoveryUsesNewestVerifiedCopy,
            FrostFieldsAreClearedWhenLoadingAnotherSlot, PortableSaveCommitNeverLosesLastGoodPrimary,
            MaxLevelProgressionShowsFinalState,
            RespecButtonExplainsSkillPointCap,
            TitleSaveManagerNeverOverwritesAnotherJourney,
            FishingSuspendsOnFocusLossAndResumesOnReturn, JournalAvailableQuestFilterHidesLockedAndCompleteButKeepsRealProgress, MapRegionQuestBrowserOpensCorrectJournalEntryWithoutChangingSaves, LiveMapObjectiveClickInspectsQuestWithoutMovingPersonalPin, JournalCanPinRealLocalQuestWithoutRetargetingOrSpoofingMap, QuestWaymarkFollowsRealTrailStagesWithoutChangingManualPins, TrackedQuestAutoFollowSurvivesRealCrossRegionStageChange, QuestStageClosureDoesNotAdvertiseStaleArrivalAndRecovers, QuestApproachAtContactShowsInteractionAndKeepsAutoFollow, QuestApproachPinUsesVerifiedPhysicalRouteAndPreservesTracking, InspectedQuestRouteUsesReachablePhysicalApproachWithoutRetargeting, InspectedQuestCanBeTrackedByExplicitMapActionWithoutErasingPersonalPin, JournalShowDestinationInspectsSelectedQuestWithoutChangingTracking, LiveJourneyJournalExplainsAndTracksRealStory, LiveFishingReelUsesRealBiteAndProductionHud, LiveFishingBiteClockAndHudPreventInfiniteWaiting,
            DeathRecoveryCannotBeInterruptedByMenuOrFocus,
            StalledFramesAdvanceActualWorldTimeWithoutRunawayCatchup,
            ActorRegistrationChecks.VerifyQueuedSkillWorldCueOnlyForActualIntent,
            ActorRegistrationChecks.VerifyQueuedAttackWorldCueFollowsRealGameCore,
            ActorRegistrationChecks.VerifyEnemyFootfallsFollowGenuinePaintedContacts,
            ActorRegistrationChecks.VerifyCompanionFootfallsMatchAuthoredWalkContacts,
            ActorRegistrationChecks.VerifyCompanionFootfallsFollowGaitRestart,
            ActorRegistrationChecks.VerifyCanonicalHeroFootfallsMatchWalkCellContacts,
            RenderStateChecks.VerifyRuinSigilsInProduction,
            RenderStateChecks.VerifyCanopyWhispersInProduction,
            RenderStateChecks.VerifyRoadwardenWardSpriteInProduction,
            RenderStateChecks.VerifyMalformedTransientRecordsAreSkippedByProductionDrawGuards,
            RenderStateChecks.VerifyCanopyOcclusionIncludesEveryLiveActor,
            RenderStateChecks.VerifyWaytrailCueTracksCivicAudit,
            CivicAftermathChronicleReflectsLiveInfrastructure,
            CivicAftercareRoadAuditIsTrackableAndOneTime,
            RuntimeContinuityChecks.NavigationRouteGuidance, RuntimeContinuityChecks.OptedInRemoteRouteRendersWithGeneralGuidanceOff, RuntimeContinuityChecks.RemoteJourneyDoesNotPresentSealedPortalAsOpenRoute, RuntimeContinuityChecks.VerifyCombatMusicReversesOnThreatDeparture };
        int failures = 0;
        foreach (var test in cases)
        {
            try { test(app); }
            catch (Exception error)
            {
                // Reflection-based production render tests may nest multiple
                // TargetInvocationExceptions. Always retain the ORIGINAL
                // failing source/stack rather than one unhelpful wrapper.
                Exception root=error;
                while(root.InnerException!=null)root=root.InnerException;
                failures++;
                Console.WriteLine("FAIL "+test.Method.Name+": "+root.ToString());
            }
        }
        Call(app, "OnDestroy");
        Console.WriteLine("Runtime combat input: " + (cases.Length - failures) + "/" + cases.Length + " groups passed; NATIVE_UNVERIFIED");
        return failures == 0 ? 0 : 1;
    }
}
