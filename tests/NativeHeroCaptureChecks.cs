// Exercises the ACTUAL native-conditional hero SpriteRenderer capture
// across all 84 class/direction/action combinations (including the
// 24 LMB/Q cases), NOT a mocked capture function. Real damage timestamps are
// independently tested by RuntimeCombatInputChecks.cs.
using System;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using Starfall;

public static class NativeHeroCaptureChecks
{
    static void Require(bool ok,string why)
    { if(!ok) throw new Exception("HERO_CAPTURE_REGRESSION: "+why); }

    static void Main()
    {
        var app=new FrontierRuntime();
        var flags=BindingFlags.Instance|BindingFlags.NonPublic;
        var draw=typeof(FrontierRuntime).GetMethod("DrawSpriteRendererHero",flags);
        var release=typeof(FrontierRuntime).GetMethod("ReleaseHeroCapture",flags);
        var renderField=typeof(FrontierRuntime).GetField("heroCaptureRenderer",flags);
        var animatorField=typeof(FrontierRuntime).GetField("heroCaptureAnimator",flags);
        Require(draw!=null&&release!=null&&renderField!=null&&
            animatorField!=null,"missing real capture or Animator");
        string[] classes={"Vanguard","Ranger","Arcanist"};
        string[] directions={"E","W","N","S"};
        string[] actions={"Idle","Run","Attack","Cast","Dodge","Hit","Death"};
        int[] lengths={1,4,6,6,5,5,4};
        RenderTexture attackEast=null;
        var textures=new List<RenderTexture>();
        var unique=new HashSet<RenderTexture>();
        Resources.Registered.Clear();
        Resources.RegisteredSprites.Clear();
        for(int cls=0;cls<classes.Length;cls++)
            Resources.Registered.Add("Frontier/HeroAnimator/Production"+
                classes[cls],new RuntimeAnimatorController { className=classes[cls] });
        Camera.RenderedTargets.Clear();
        GUI.DrawnTextures.Clear();
        Time.frameCount=1000;
        int audited=0;
        for(int cls=0;cls<3;cls++)
        for(int cmd=0;cmd<actions.Length;cmd++)
        for(int dir=0;dir<4;dir++)
        {
            // West mirrors the identical East full-body source across ALL
            // actions (including strafing, hit, dodge, and death).
            int facing=dir<2?0:dir==2?1:2;
            bool flip=dir==1;
            string action=actions[cmd];
            string key="Frontier/HeroAnimator/Canonical/Hero_"+
                classes[cls]+"_"+action+"_"+(facing==0?"E":facing==1?"N":"S");
            if(!Resources.Registered.ContainsKey(key))
                Resources.Registered.Add(key,
                    new Texture2D(lengths[cmd]*256,256,
                        TextureFormat.RGBA32,false));
            var atlas=(Texture2D)Resources.Registered[key];
            if(!Resources.RegisteredSprites.ContainsKey(key))
            {
                var sprites=new Sprite[lengths[cmd]];
                for(int cell=0;cell<sprites.Length;cell++)
                    sprites[cell]=Sprite.Create(atlas,
                        new Rect(cell*256,0,256,256),new Vector2(128,16),
                        128f,0,SpriteMeshType.FullRect);
                Resources.RegisteredSprites.Add(key,sprites);
            }
            app.Game.AppearanceWeapon=cls;
            app.ProbeAction=cmd;
            app.ProbeFrames=lengths[cmd];
            app.ProbeFrame=cmd==2||cmd==3?2:cmd==0?0:1;
            app.ProbePosition=app.ProbeFrame;
            bool drawn=(bool)draw.Invoke(app,new object[]{
                new Point(100,140),new Color(1,1,1,1),flip,facing,false});
            Require(drawn,"fallback "+classes[cls]+"/"+action+"/"+directions[dir]);
            var renderer=renderField.GetValue(app) as SpriteRenderer;
            Require(renderer!=null&&renderer.sprite!=null&&
                ReferenceEquals(renderer.sprite.texture,atlas),
                "class/action/facing atlas leaked between actors");
            Require(renderer.flipX==flip,"West mirror mismatch or N/S flipped");
            // Assert that the PRODUCTION Animator is actually active and
            // selects exactly the source cell chosen by GameCore.
            var nativeAnimator=animatorField.GetValue(app) as Animator;
            Require(nativeAnimator!=null&&nativeAnimator.enabled&&
                nativeAnimator.sampleCount>0,
                "native clip not used; test silently fell back to direct Sprite");
            float expected=(app.ProbeFrame+.5f)/app.ProbeFrames;
            Require(Math.Abs(nativeAnimator.lastNormalizedSample-expected)<.0001f,
                "Animator sampled a fractional boundary instead of the chosen cell centre");
            var chosen=Resources.RegisteredSprites[key][app.ProbeFrame];
            Require(ReferenceEquals(renderer.sprite,chosen),
                "Animator selected a neighbouring, stale, or wrong-class whole-body Sprite");
            var target=GUI.DrawnTextures[GUI.DrawnTextures.Count-1] as RenderTexture;
            Require(target!=null&&target.IsCreated()&&
                target.width==256&&target.height==256,
                "lost source body pixel density");
            Require(unique.Add(target),
                "mutable GPU texture overwritten by another directional action");
            textures.Add(target);
            if(cls==0&&cmd==2&&dir==0) attackEast=target;
            audited++;
        }
        Require(audited==84&&unique.Count==84&&attackEast!=null,
            "missing 3 classes x 4 directions x 7 full-body action cases");
        Require(Camera.RenderedTargets.Count==84,
            "new source poses were not independently captured");
        // Repeat E after W within the SAME frame; it must still reference
        // its original texture, not a changed left-facing image.
        app.Game.AppearanceWeapon=0;
        app.ProbeAction=2;app.ProbeFrames=6;app.ProbeFrame=2;
        bool repeat=(bool)draw.Invoke(app,new object[]{
            new Point(100,140),new Color(1,1,1,1),false,0,false});
        Require(repeat&&ReferenceEquals(
            attackEast,GUI.DrawnTextures[GUI.DrawnTextures.Count-1]),
            "E->W->E changed earlier queued E body");
        // The real contact cell must not mutate the already submitted
        // windup buffer, including on the same frame.
        app.ProbeFrame=3;
        bool contact=(bool)draw.Invoke(app,new object[]{
            new Point(100,140),new Color(1,1,1,1),false,0,false});
        var contactTexture=GUI.DrawnTextures[GUI.DrawnTextures.Count-1]
            as RenderTexture;
        Require(contact&&contactTexture!=null&&
            !ReferenceEquals(contactTexture,attackEast),
            "contact reused pending windup buffer");
        var contactAnimator=animatorField.GetValue(app) as Animator;
        Require(contactAnimator!=null&&contactAnimator.enabled&&
            Math.Abs(contactAnimator.lastNormalizedSample-(3.5f/6f))<.0001f,
            "newly released contact must use the centre of its own exact keyframe");
        attackEast.Release(); // simulate device loss
        app.ProbeFrame=2;
        bool restored=(bool)draw.Invoke(app,new object[]{
            new Point(100,140),new Color(1,1,1,1),false,0,false});
        Require(restored&&attackEast.IsCreated(),
            "lost source-pose GPU buffer was not recreated");
        // Simulate one transient wrong-asset return from a Unity Animator
        // during editor import. The frame visible to the player must ALWAYS
        // be the right published Sprite, and native playback must recover
        // automatically without restarting the scene.
        string actionResource="Frontier/HeroAnimator/Canonical/Hero_Vanguard_Attack_E";
        Animator.InjectWrongSpriteOnce=true;
        app.ProbeFrame=3;
        Time.frameCount=1100;
        bool guarded=(bool)draw.Invoke(app,new object[]{
            new Point(100,140),new Color(1,1,1,1),false,0,false});
        var liveAnimator=animatorField.GetValue(app) as Animator;
        var liveRenderer=renderField.GetValue(app) as SpriteRenderer;
        Require(guarded&&liveAnimator!=null&&!liveAnimator.enabled&&
            ReferenceEquals(liveRenderer.sprite,
                Resources.RegisteredSprites[actionResource][3]),
            "temporary wrong Animator sprite leaked into visible player body");
        int attempts=liveAnimator.sampleCount;
        Time.frameCount=1101;
        bool cooled=(bool)draw.Invoke(app,new object[]{
            new Point(100,140),new Color(1,1,1,1),false,0,false});
        Require(cooled&&!liveAnimator.enabled&&
            liveAnimator.sampleCount==attempts,
            "same bad Animator clip retried every repaint instead of stable fallback");
        Time.frameCount=1221;
        bool recoveredNative=(bool)draw.Invoke(app,new object[]{
            new Point(100,140),new Color(1,1,1,1),false,0,false});
        Require(recoveredNative&&liveAnimator.enabled&&
            liveAnimator.sampleCount>attempts&&
            ReferenceEquals(liveRenderer.sprite,
                Resources.RegisteredSprites[actionResource][3]),
            "native Animator stayed permanently disabled after transient import mismatch");
        release.Invoke(app,null);
        foreach(var target in textures)
            Require(target.releaseCount>0&&!target.IsCreated(),
                "captured action GPU target leaked at shutdown");
        Require(contactTexture.releaseCount>0,
            "contact target leaked at shutdown");
        Console.WriteLine("PASS 84 E/W/N/S three-class all-action native captures (24 LMB/Q), left flip, E-W-E, windup-contact, transient bad-clip recovery, device loss and cleanup");
        Console.WriteLine("NATIVE_UNVERIFIED: Unity GPU/GameView/Animator not executed");
    }
}
