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
                        new Rect(cell*256,0,256,256),
                        new Vector2(.5f,.0625f),
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
        // Reproduce a stale north-attack Animator Transform scale curve
        // AFTER native Sprite sampling, for every playable profession.
        var rootField=typeof(FrontierRuntime).GetField("heroCaptureRoot",flags);
        var cameraField=typeof(FrontierRuntime).GetField("heroCaptureCamera",flags);
        Require(rootField!=null&&cameraField!=null,
            "missing native root/camera scale contract");
        for(int cls=0;cls<3;cls++)
        for(int action=2;action<=3;action++) // Attack + Q/Cast
        for(int cell=0;cell<6;cell++) // idle, guard, windup, hit, recovery, idle
        {
            app.Game.AppearanceWeapon=cls;
            app.ProbeAction=action;
            app.ProbeFrames=6;
            app.ProbeFrame=cell;
            app.ProbePosition=cell;
            // Poison a reused OFFSCREEN Animator root and framing camera
            // exactly as an old imported m_LocalPosition clip might do.
            // The newly sampled native Sprite must remain rooted at the
            // same original boot regardless of state/direction.
            var staleRoot=rootField.GetValue(app) as GameObject;
            var staleCam=cameraField.GetValue(app) as Camera;
            Require(staleRoot!=null&&staleCam!=null,
                "native hero capture rig disappeared");
            staleRoot.transform.position=new Vector3(9018f,9100f,0f);
            staleRoot.transform.localRotation=Quaternion.Euler(0f,0f,-25f);
            staleCam.transform.localPosition=new Vector3(3f,5f,-8f);
            staleCam.transform.localRotation=Quaternion.Euler(0f,0f,17f);
            staleCam.aspect=1.6f;
            Animator.InjectOverscaledHeroOnce=true;
            // Also poison the sampled native pose itself, not only the
            // state inherited from the previous offscreen capture.
            Animator.InjectRotatedFadedHeroOnce=true;
            bool northDraw=(bool)draw.Invoke(app,new object[]{
                new Point(100,140),new Color(1,1,1,1),false,1,false});
            var root=rootField.GetValue(app) as GameObject;
            var cam=cameraField.GetValue(app) as Camera;
            var current=renderField.GetValue(app) as SpriteRenderer;
            var rect=GUI.DrawnRects[GUI.DrawnRects.Count-1];
            Require(northDraw&&root!=null&&cam!=null&&current!=null&&
                root.transform.localScale.x==1f&&
                root.transform.localScale.y==1f&&
                root.transform.localScale.z==1f&&
                root.transform.localRotation.zAngle==0f&&
                current.transform.localRotation.zAngle==0f&&
                Math.Abs(current.transform.localPosition.x)<.0001f&&
                Math.Abs(current.transform.localPosition.y)<.0001f&&
                Math.Abs(current.transform.localPosition.z)<.0001f&&
                Math.Abs(current.transform.localScale.x-1f)<.0001f&&
                Math.Abs(current.transform.localScale.y-1f)<.0001f&&
                Math.Abs(current.transform.localScale.z-1f)<.0001f&&
                current.color.r==1f&&current.color.g==1f&&
                current.color.b==1f&&current.color.a==1f&&
                cam.transform.localRotation.zAngle==0f&&
                cam.transform.localScale.x==1f&&
                cam.transform.localScale.y==1f&&
                Math.Abs(root.transform.position.x-9000f)<.0001f&&
                Math.Abs(root.transform.position.y-9000f)<.0001f&&
                Math.Abs(cam.transform.localPosition.x)<.0001f&&
                Math.Abs(cam.transform.localPosition.y-.875f)<.0001f&&
                Math.Abs(cam.transform.localPosition.z+10f)<.0001f&&
                Math.Abs(cam.aspect-1f)<.0001f&&
                Math.Abs(cam.orthographicSize-1f)<.0001f,
                "north action Animator displaced/rotated/scaled the child body "+classes[cls]+
                " action "+action+" frame "+cell);
            Require(Math.Abs(rect.width-192f)<.0001f&&
                Math.Abs(rect.height-192f)<.0001f,
                "north action changed "+classes[cls]+" GUI body density");
            Require(ReferenceEquals(current.sprite,
                Resources.RegisteredSprites[
                    "Frontier/HeroAnimator/Canonical/Hero_"+
                    classes[cls]+(action==2?"_Attack_N":"_Cast_N")][cell]),
                "north action lost canonical "+classes[cls]+" source frame");
        }
        Animator.InjectOverscaledHeroOnce=false;
        Animator.InjectRotatedFadedHeroOnce=false;
        // Repeat E after W within the SAME frame; it must still reference
        // its original texture, not a changed left-facing image.
        app.Game.AppearanceWeapon=0;
        app.ProbeAction=2;app.ProbeFrames=6;app.ProbeFrame=2;
        int rendersBeforeReuse=Camera.RenderedTargets.Count;
        bool repeat=(bool)draw.Invoke(app,new object[]{
            new Point(100,140),new Color(1,1,1,1),false,0,false});
        Require(repeat&&ReferenceEquals(
            attackEast,GUI.DrawnTextures[GUI.DrawnTextures.Count-1])&&
            Camera.RenderedTargets.Count==rendersBeforeReuse,
            "E->W->E repainted a submitted full-body GPU target");
        // The identical cached Sprite remains lossless on the following
        // frame, too. Only an actual lost device target needs recapture.
        Time.frameCount=1001;
        repeat=(bool)draw.Invoke(app,new object[]{
            new Point(100,140),new Color(1,1,1,1),false,0,false});
        Require(repeat&&
            Camera.RenderedTargets.Count==rendersBeforeReuse,
            "revisiting a validated hero pose must reuse its GPU pixels");
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
        // Controller hot-import regression: opening Frontier before Unity
        // finishes baking Ranger's Controller used to lock that class to the
        // fallback for this entire session. The exact SAME scene instance
        // must switch from safe full-body Sprite to live Animator once ready.
        const string lateController="Frontier/HeroAnimator/ProductionRanger";
        RuntimeAnimatorController rangerController =
            (RuntimeAnimatorController)Resources.Registered[lateController];
        Resources.Registered.Remove(lateController);
        var late=new FrontierRuntime();
        late.Game.AppearanceWeapon=1; // Ranger outfit: do NOT show Vanguard
        late.ProbeAction=2;
        late.ProbeFrames=6;
        late.ProbeFrame=2;
        late.ProbePosition=2f;
        Time.frameCount=1300;
        bool first=(bool)draw.Invoke(late,new object[]{
            new Point(100,140),new Color(1,1,1,1),false,0,false});
        var delayedAnimator=animatorField.GetValue(late) as Animator;
        var delayedRenderer=renderField.GetValue(late) as SpriteRenderer;
        string rangerKey="Frontier/HeroAnimator/Canonical/Hero_Ranger_Attack_E";
        Require(first&&delayedAnimator!=null&&delayedRenderer!=null&&
            !delayedAnimator.enabled&&
            ReferenceEquals(delayedRenderer.sprite,
                Resources.RegisteredSprites[rangerKey][2]),
            "missing native Controller must keep exact Ranger full-body Sprite");
        int nativeCalls=delayedAnimator.sampleCount;
        Resources.Registered.Add(lateController,rangerController);
        Time.frameCount=1301;
        bool waiting=(bool)draw.Invoke(late,new object[]{
            new Point(100,140),new Color(1,1,1,1),false,0,false});
        Require(waiting&&!delayedAnimator.enabled&&
            delayedAnimator.sampleCount==nativeCalls&&
            ReferenceEquals(delayedRenderer.sprite,
                Resources.RegisteredSprites[rangerKey][2]),
            "missing controller should avoid reimport checks every Repaint");
        Time.frameCount=1420;
        bool nativeRestored=(bool)draw.Invoke(late,new object[]{
            new Point(100,140),new Color(1,1,1,1),false,0,false});
        Require(nativeRestored&&delayedAnimator.enabled&&
            delayedAnimator.sampleCount>nativeCalls&&
            ReferenceEquals(delayedAnimator.runtimeAnimatorController,
                rangerController)&&
            ReferenceEquals(delayedRenderer.sprite,
                Resources.RegisteredSprites[rangerKey][2]),
            "late-imported controller did not recover live native Animator");
        // Even during a previous class's missing-controller cooldown, an
        // actual equipment swap should load the new class immediately.
        late.Game.AppearanceWeapon=0;
        Time.frameCount=1421;
        bool switched=(bool)draw.Invoke(late,new object[]{
            new Point(100,140),new Color(1,1,1,1),false,0,false});
        Require(switched&&delayedAnimator.enabled&&
            ReferenceEquals(delayedAnimator.runtimeAnimatorController,
                Resources.Registered[
                    "Frontier/HeroAnimator/ProductionVanguard"])&&
            ReferenceEquals(delayedRenderer.sprite,
                Resources.RegisteredSprites[actionResource][2]),
            "class swap displayed an old/missing controller's wrong armor");
        release.Invoke(late,null);

        // Native RenderTexture device/context failure can happen during an
        // otherwise valid Animator frame. Keep the full-body fallback alive,
        // then automatically resume the SAME pose/weapon without F8 toggling.
        var device=new FrontierRuntime();
        device.Game.AppearanceWeapon=0;
        device.ProbeAction=2;
        device.ProbeFrames=6;
        device.ProbeFrame=2;
        device.ProbePosition=2f;
        var brokenField=typeof(FrontierRuntime).GetField(
            "heroCaptureBroken",flags);
        Require(brokenField!=null,"native GPU fault state not found");
        Time.frameCount=2000;
        Camera.InjectGpuFailureOnce=true;
        bool failedDraw=(bool)draw.Invoke(device,new object[]{
            new Point(100,140),new Color(1,1,1,1),false,0,false});
        Require(!failedDraw&&(bool)brokenField.GetValue(device),
            "broken GPU capture must request existing whole-body fallback");
        var oldRenderCount=Camera.RenderedTargets.Count;
        Time.frameCount=2001;
        bool waitGpu=(bool)draw.Invoke(device,new object[]{
            new Point(100,140),new Color(1,1,1,1),false,0,false});
        Require(!waitGpu&&Camera.RenderedTargets.Count==oldRenderCount,
            "GPU failures should not retry on every repaint");
        Time.frameCount=2120;
        bool recoveredGpu=(bool)draw.Invoke(device,new object[]{
            new Point(100,140),new Color(1,1,1,1),false,0,false});
        var restoredRenderer=renderField.GetValue(device) as SpriteRenderer;
        var restoredAnimator=animatorField.GetValue(device) as Animator;
        Require(recoveredGpu&&!(bool)brokenField.GetValue(device)&&
            restoredRenderer!=null&&restoredAnimator!=null&&
            restoredAnimator.enabled&&
            ReferenceEquals(restoredRenderer.sprite,
                Resources.RegisteredSprites[actionResource][2])&&
            Camera.RenderedTargets.Count>oldRenderCount,
            "transient GPU loss permanently disabled live hero capture");
        // Unlike an exception from Camera.Render, RenderTexture.Create()
        // may quietly return false. Recreate failures must not submit a
        // transparent player; retain six-cell canonical fallback and retry
        // after the existing 120-frame throttle.
        var lostTarget=GUI.DrawnTextures[GUI.DrawnTextures.Count-1]
            as RenderTexture;
        Require(lostTarget!=null,"missing live hero target for device-loss probe");
        lostTarget.Release();
        int rendersOnRefusal=Camera.RenderedTargets.Count;
        RenderTexture.InjectCreateFailureOnce=true;
        Time.frameCount=2130;
        bool denied=(bool)draw.Invoke(device,new object[]{
            new Point(100,140),new Color(1,1,1,1),false,0,false});
        Require(!denied&&(bool)brokenField.GetValue(device)&&
            Camera.RenderedTargets.Count==rendersOnRefusal &&
            !RenderTexture.InjectCreateFailureOnce,
            "failed GPU recreation submitted a blank hero or ignored Create() result");
        Time.frameCount=2131;
        bool throttled=(bool)draw.Invoke(device,new object[]{
            new Point(100,140),new Color(1,1,1,1),false,0,false});
        Require(!throttled&&Camera.RenderedTargets.Count==rendersOnRefusal,
            "failed hero GPU Create was retried every repaint");
        Time.frameCount=2250;
        bool gpuRestored=(bool)draw.Invoke(device,new object[]{
            new Point(100,140),new Color(1,1,1,1),false,0,false});
        Require(gpuRestored&&Camera.RenderedTargets.Count==rendersOnRefusal+1&&
            ReferenceEquals((renderField.GetValue(device) as SpriteRenderer).sprite,
                Resources.RegisteredSprites[actionResource][2]),
            "recreated hero GPU target did not recover exact class/contact pixels");
        release.Invoke(device,null);

        // Also reject allocation refusal on the very FIRST capture.
        var fresh=new FrontierRuntime();
        fresh.Game.AppearanceWeapon=2;
        fresh.ProbeAction=3;
        fresh.ProbeFrames=6;
        fresh.ProbeFrame=3;
        fresh.ProbePosition=3f;
        int beforeCreate=Camera.RenderedTargets.Count;
        RenderTexture.InjectCreateFailureOnce=true;
        Time.frameCount=2300;
        bool createDenied=(bool)draw.Invoke(fresh,new object[]{
            new Point(100,140),Color.white,false,2,false});
        Require(!createDenied&&Camera.RenderedTargets.Count==beforeCreate,
            "first hero GPU allocation refusal displayed an invalid texture");
        Time.frameCount=2420;
        bool createRecovered=(bool)draw.Invoke(fresh,new object[]{
            new Point(100,140),Color.white,false,2,false});
        var recSprite=renderField.GetValue(fresh) as SpriteRenderer;
        Require(createRecovered&&recSprite!=null&&
            ReferenceEquals(recSprite.sprite,Resources.RegisteredSprites[
                "Frontier/HeroAnimator/Canonical/Hero_Arcanist_Cast_S"][3])&&
            Camera.RenderedTargets.Count==beforeCreate+1,
            "initial GPU creation failure permanently hid Arcanist Cast_S");
        release.Invoke(fresh,null);

        // The artist can rebake ALL 63 canonical hero action/facing
        // families while GameView remains open. An already accepted
        // six-cell Vanguard attack must automatically adopt the new
        // imported Sprite without resetting the character or repainting
        // any earlier queued GPU image, then refuse a PARTIAL reimport.
        const string hotPath=
            "Frontier/HeroAnimator/Canonical/Hero_Vanguard_Attack_E";
        var originalCells=Resources.RegisteredSprites[hotPath];
        var rebaked=new Sprite[originalCells.Length];
        for(int i=0;i<rebaked.Length;i++)
            rebaked[i]=Sprite.Create(originalCells[i].texture,
                originalCells[i].rect,new Vector2(.5f,.0625f),
                128f,0,SpriteMeshType.FullRect);
        var live=new FrontierRuntime();
        live.Game.AppearanceWeapon=0;
        live.ProbeAction=2;live.ProbeFrames=6;
        live.ProbeFrame=2;live.ProbePosition=2f;
        try
        {
            Time.frameCount=3000;
            Require((bool)draw.Invoke(live,new object[]{
                new Point(100,140),Color.white,false,0,false}),
                "Initial golden hero imported body did not render");
            var heroView=renderField.GetValue(live) as SpriteRenderer;
            var heroNative=animatorField.GetValue(live) as Animator;
            RenderTexture originalTarget=GUI.DrawnTextures[
                GUI.DrawnTextures.Count-1] as RenderTexture;
            Require(heroView!=null&&heroNative!=null&&
                ReferenceEquals(heroView.sprite,originalCells[2])&&
                heroNative.enabled&&originalTarget!=null,
                "Missing accepted full-body native source before hot-reimport");
            Resources.RegisteredSprites[hotPath]=rebaked;
            Time.frameCount=3001;
            Require((bool)draw.Invoke(live,new object[]{
                new Point(100,140),Color.white,false,0,false})&&
                ReferenceEquals(heroView.sprite,originalCells[2])&&
                ReferenceEquals(GUI.DrawnTextures[
                    GUI.DrawnTextures.Count-1],originalTarget),
                "Canonical import scanned/mutated an earlier queued GUI pose");
            Time.frameCount=3180; // one bounded 180-frame refresh
            Require((bool)draw.Invoke(live,new object[]{
                new Point(100,140),Color.white,false,0,false})&&
                ReferenceEquals(heroView.sprite,rebaked[2])&&
                heroNative.enabled&&
                !ReferenceEquals(GUI.DrawnTextures[
                    GUI.DrawnTextures.Count-1],originalTarget)&&
                originalTarget.IsCreated(),
                "Rebaked blonde Vanguard frames never reached native Animator");
            var secondTarget=GUI.DrawnTextures[
                GUI.DrawnTextures.Count-1] as RenderTexture;
            // 5 of 6 present: NEVER allow a mixed-maturity attack
            // clip to select valid-looking but partial weapon contact art.
            var partial=new Sprite[rebaked.Length-1];
            Array.Copy(rebaked,partial,partial.Length);
            Resources.RegisteredSprites[hotPath]=partial;
            Time.frameCount=3361;
            Require((bool)draw.Invoke(live,new object[]{
                new Point(100,140),Color.white,false,0,false})&&
                !heroNative.enabled&&
                !ReferenceEquals(heroView.sprite,rebaked[2])&&
                ReferenceEquals(heroView.sprite.texture,
                    originalCells[2].texture)&&
                secondTarget.IsCreated(),
                "Partial imported hero atlas mixed two versions of Attack");
            var fallback=heroView.sprite;
            Resources.RegisteredSprites[hotPath]=rebaked;
            Time.frameCount=3542;
            Require((bool)draw.Invoke(live,new object[]{
                new Point(100,140),Color.white,false,0,false})&&
                heroNative.enabled&&
                ReferenceEquals(heroView.sprite,rebaked[2])&&
                fallback.destroyed,
                "Corrected hero art did not restore accepted native contact");
        }
        finally
        {
            Resources.RegisteredSprites[hotPath]=originalCells;
            release.Invoke(live,null);
        }
        Require(!originalCells[2].destroyed&&!rebaked[2].destroyed,
            "Canonical hot-refresh destroyed shared imported hero Sprite");
        release.Invoke(app,null);
        foreach(var target in textures)
            Require(target.releaseCount>0&&!target.IsCreated(),
                "captured action GPU target leaked at shutdown");
        Require(contactTexture.releaseCount>0,
            "contact target leaked at shutdown");
        Console.WriteLine("PASS 84 E/W/N/S class actions, late Controller import, bounded retry, live GPU fault recovery, full-body fallback, class-swap isolation and cleanup");
        Console.WriteLine("NATIVE_UNVERIFIED: Unity GPU/GameView/Animator not executed");
    }
}
