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
    private static object CallCore(GameCore game, string method, params object[] args)
    { return typeof(GameCore).GetMethod(method, Private).Invoke(game, args); }
    private static object CoreField(GameCore game, string field)
    { return typeof(GameCore).GetField(field, Private).GetValue(game); }

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
            g.Talk(inn);
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
                    g.save.room=9;g.save.x=inn.x;g.save.y=inn.y;g.Talk(inn);
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
            g.Talk(inn);
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
            attacker.target=start;g.enemies.Add(attacker);
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
            attacker.target=prisoner;g.enemies.Add(attacker);
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
            raider.target=original;
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
        Action<FrontierRuntime>[] cases = { RealWayfarerCoverIsVisibleInWorldAndJournal, NearbyNpcHudMatchesRealKeyboardInteraction, BankedFrostEmberMovesWorldIndicatorToHearth, MineConvoySafeArrivalHoldsUntilPhysicalHandoff, LiveCourierAmbushNeverSpawnsOnCompanion, CourierRaidersRequireReachableLiveSpawn, SealedEscortRouteRejectsFalseNearSideDestination, CivilianEscortNavigatesRealWallDetour, EscortDodgeChoosesFarSideOfLivingAttacker, SealedEscortDodgeCannotSpendEnergyOrTeleport, CivilianEvadeUsesRealContactAndCostsOnlyWhenMoved, LivingEscortStallWarnsOnlyOnRealBlockedMovement, EvacuationFinalHandoffRequiresBothLivingActorsOnOpenSide, NoPhantomRescueMarkersWithoutMission, ActorOriginNavigationPreservesHeroAndCollision, LowSafetyEscortUsesReachableShelterBeforeReturn, TimedRescueCompassOverridesPinnedQuestWithoutMutatingIt, ShelterRestCommandHealsRealEscortOnlyWhenSecure, ActualCivilianClimateSheltersCannotPassThroughWalls, RepairedClimateStationProtectsOnlyItsPhysicalSide, LiveOasisShadeRestIsPhysicalAndSafe, CriticalDesertHeatGuidesActualReachableShade, CriticalAlpineColdGuidesReachablePhysicalBrazier, RealAlpineBrazierRestRequiresSafePhysicalContact, LitAlpineBraziersGivePhysicalSafeRest, LiveAffordableWardUsesActualCaveNavigation, LiveWardRestoresEnergyOnlyFromPhysicalAmbientImpact, LiveTunedAbyssPylonShowsPhysicalShelterAndExit, LiveAbyssRouteMenusBackToGateWithoutBanking, LiveSevenChoiceAbyssGateBanksFromSecondKeyboardPage, LateChronicleChoicePagesRemainPlayable, ThreePageChronicleSupportsPointerHomeEndAndRealBack, ChronicleBackReturnsThroughActualUnlockedChapters, ControllerShouldersPageRealChronicleAndSelectVisibleItem, DialogueChoiceFocusMatchesActualRenderedButton, DialogueReaderCanStepBackwardWithoutTriggeringChoices, LiveYKeyOrdersPhysicalCompanionWatchAndRestoresFollow, LivePKeyHoldsAndReleasesPhysicalCompanion, LiveRBButtonTapAndHoldCompanionOrders, LiveXKeyPatchCannotRepairRemoteOrTwice, LiveVKeyBarricadeUsesRealOutpostGuard, AtomicHeroContactImportRecovery, FreshTechniqueWinsHeldAttack, QueuedTechniqueKeepsAimAndRunsOnce,
            DashAndTechniqueShareAFrame, HudTechniqueCanQueue, ReducedMotionHeroInvulnerabilityStaysVisibleWithoutStrobing, CanonicalHeroHitRecoversOnSameWholeBody, CanonicalCombatRecoverySettlesWithoutExtraBody, CombatSettleNeverMasksNewPhysicalAction, InvalidPhysicalContactCannotFakeHeroSettle, QueuedAreaPreviewDrawsRealWorldRadius, QueuedAreaTagsOnlyPhysicalLivingEnemies, RealProfessionQHitsTintOnlyDamageConfirmedContact, RealBasicWeaponHitsRetainFlightElementAfterEquipmentSwap, MeleeSwordContactStopsAtRealSolidCover, ProjectileWallContactKeepsOriginalSourceLight, EnemyArcherRepositionsForTrueArrowRadiusAndCancelsNewCover, ReleasedAreaVfxStopsAtActualWorldCover, ExplosiveWallSplashDrawsOnlyPhysicalNearSide, QueuedRangerVolleyMarksLiveObstructedTarget, RiposteHudFollowsRealParryIntentAndUnreleasedContact, ActualBossSupportCallIsVisibleAndRetractsAfterCombatBreak, CivicReserveCombatHudTracksGenuineReceiverObjective, MenuAndApplicationPauseDiscardIntent,
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
            FishingSuspendsOnFocusLossAndResumesOnReturn, LiveJourneyJournalExplainsAndTracksRealStory, LiveFishingReelUsesRealBiteAndProductionHud,
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
            RuntimeContinuityChecks.NavigationRouteGuidance };
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
