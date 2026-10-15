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
        Require(draw!=null&&release!=null&&renderField!=null,"missing real capture");
        string[] classes={"Vanguard","Ranger","Arcanist"};
        string[] directions={"E","W","N","S"};
        string[] actions={"Idle","Run","Attack","Cast","Dodge","Hit","Death"};
        int[] lengths={1,4,6,6,5,5,4};
        RenderTexture attackEast=null;
        var textures=new List<RenderTexture>();
        var unique=new HashSet<RenderTexture>();
        Resources.Registered.Clear();
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
        attackEast.Release(); // simulate device loss
        app.ProbeFrame=2;
        bool restored=(bool)draw.Invoke(app,new object[]{
            new Point(100,140),new Color(1,1,1,1),false,0,false});
        Require(restored&&attackEast.IsCreated(),
            "lost source-pose GPU buffer was not recreated");
        release.Invoke(app,null);
        foreach(var target in textures)
            Require(target.releaseCount>0&&!target.IsCreated(),
                "captured action GPU target leaked at shutdown");
        Require(contactTexture.releaseCount>0,
            "contact target leaked at shutdown");
        Console.WriteLine("PASS 84 E/W/N/S three-class all-action native captures (24 LMB/Q), left flip, E-W-E, windup-contact, device loss and cleanup");
        Console.WriteLine("NATIVE_UNVERIFIED: Unity GPU/GameView/Animator not executed");
    }
}
