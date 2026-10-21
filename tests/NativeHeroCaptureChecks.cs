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

    // Actual Resources and the actual HasCompleteHeroContactArt publication
    // gate: repeated six/seven-cell swaps must not leak runtime Sprite.Create
    // wrappers, yet Unity-imported sub-assets must never be destroyed.
    static Sprite[] MakeImportedCells(Texture2D atlas,int count)
    {
        var cells=new Sprite[count];
        for(int i=0;i<count;i++)
            cells[i]=Sprite.Create(atlas,new Rect(i*256,0,256,256),
                new Vector2(.5f,.0625f),128f,0,SpriteMeshType.FullRect);
        return cells;
    }

    // Actual Resources and the actual HasCompleteHeroContactArt publication
    // gate: dimensions alone must not promote a partial Unity Sprite import.
    // Imported Sprite sub-assets are never owned by the renderer or destroyed.
    static void VerifyAtomicHeroFamilySwap(MethodInfo release,BindingFlags flags)
    {
        var app=new FrontierRuntime();
        var kind=typeof(FrontierRuntime);
        var gate=kind.GetMethod("HasCompleteHeroContactArt",flags);
        var spritesField=kind.GetField("heroPublishedSprites",flags);
        var ownedField=kind.GetField("heroOwnedPublishedSprites",flags);
        var clockField=kind.GetField("clock",flags);
        Require(gate!=null&&spritesField!=null&&ownedField!=null&&clockField!=null,
            "atomic hero contact publish/cache gate missing");
        var published=(Dictionary<string,Sprite[]>)spritesField.GetValue(app);
        var owned=(HashSet<Sprite>)ownedField.GetValue(app);
        var originals=new Dictionary<string,Texture2D>();
        var originalSpriteSets=new Dictionary<string,Sprite[]>();
        var stagedTextures=new Dictionary<string,Texture2D>();
        var incomingImported=new List<Sprite>();
        var temporaries=new List<Sprite>();
        string[] classes={"Vanguard","Ranger","Arcanist"};
        string[] facings={"E","N","S"};
        // First publish the existing six-cell art. This makes the next
        // all-18-file import a genuine in-flight ART SWITCH instead of a
        // first-time initialization.
        Require(!(bool)gate.Invoke(app,null),
            "old production source must begin as 6-cell action family");
        string delayedPath=null;
        try
        {
            foreach(string cls in classes)
            foreach(string action in new[]{"Attack","Cast"})
            foreach(string face in facings)
            {
                string key="Hero_"+cls+"_"+action+"_"+face;
                string path="Frontier/HeroAnimator/Canonical/"+key;
                var old=(Texture2D)Resources.Registered[path];
                originals.Add(path,old);
                originalSpriteSets.Add(path,Resources.RegisteredSprites[path]);
                // Preserve a real imported subasset in the stale cache: it
                // is shared with the production Animator and is NOT ours.
                var asset=originalSpriteSets[path][0];
                var wrapper=Sprite.Create(old,new Rect(256,0,256,256),
                    new Vector2(.5f,.0625f),128f,0,SpriteMeshType.FullRect);
                temporaries.Add(wrapper);
                owned.Add(wrapper);
                published.Add(key,new[]{asset,wrapper});
                var incoming=new Texture2D(7*256,256,
                    TextureFormat.RGBA32,false);
                stagedTextures.Add(path,incoming);
                Resources.Registered[path]=incoming;
                if(delayedPath==null)delayedPath=path;
            }
            clockField.SetValue(app,3f);
            app.Game.playerAction=new ActorAction{kind="attack"};
            Require(!(bool)gate.Invoke(app,null)&&
                published.Count==18&&owned.Count==18,
                "Native mid-attack import must not destroy original one-body published Sprites");

            app.Game.playerAction=null;
            clockField.SetValue(app,6f);
            Require(!(bool)gate.Invoke(app,null)&&
                published.Count==18&&owned.Count==18,
                "Seven-cell texture dimensions alone must not promote the family before Sprite import completes");

            // Import 17 of 18 seven-cell Sprite families, leaving one direction
            // on the six-cell sub-assets. The runtime must continue to use the
            // entire previously accepted family until the last import arrives.
            foreach(var pair in stagedTextures)
            {
                if(pair.Key==delayedPath)continue;
                var cells=MakeImportedCells(pair.Value,7);
                Resources.RegisteredSprites[pair.Key]=cells;
                incomingImported.AddRange(cells);
            }
            clockField.SetValue(app,9f);
            Require(!(bool)gate.Invoke(app,null)&&
                published.Count==18&&owned.Count==18,
                "17/18 imported Sprite families must not expose a mixed class/facing contact release");
            var finalCells=MakeImportedCells(stagedTextures[delayedPath],7);
            Resources.RegisteredSprites[delayedPath]=finalCells;
            incomingImported.AddRange(finalCells);
            clockField.SetValue(app,12f);
            Require((bool)gate.Invoke(app,null),
                "all 18 valid seven-cell textures and Sprite families did not switch after the real source action completed");
            Require(published.Count==0&&owned.Count==0,
                "six-to-seven-frame family swap retained stale cache owners");
            foreach(var item in temporaries)
                Require(item.destroyed,"runtime-owned Sprite leaked after publish");
            foreach(var item in originalSpriteSets.Values.SelectMany(x=>x))
                Require(!item.destroyed,"atomic publish destroyed imported Animator Sprite");
            temporaries.Clear();

            foreach(var pair in originals)
            {
                string key=pair.Key.Substring(
                    "Frontier/HeroAnimator/Canonical/".Length);
                var wrapper=Sprite.Create(stagedTextures[pair.Key],
                    new Rect(0,0,256,256),new Vector2(.5f,.0625f),
                    128f,0,SpriteMeshType.FullRect);
                temporaries.Add(wrapper);
                owned.Add(wrapper);
                published.Add(key,new[]{wrapper});
                Resources.Registered[pair.Key]=pair.Value;
                Resources.RegisteredSprites[pair.Key]=originalSpriteSets[pair.Key];
            }
            clockField.SetValue(app,15f);
            app.Game.playerAction=new ActorAction{kind="skill"};
            Require((bool)gate.Invoke(app,null)&&
                published.Count==18&&owned.Count==18,
                "Mid-cast withdrawal must not destroy the currently authored whole-body recovery");
            app.Game.playerAction=null;
            clockField.SetValue(app,18f);
            Require(!(bool)gate.Invoke(app,null),
                "Withdrawn action atlas did not safely roll back after Cast ended");
            Require(published.Count==0&&owned.Count==0,
                "seven-to-six-frame family rollback retained stale owners");
            foreach(var item in temporaries)
                Require(item.destroyed,
                    "runtime-owned Sprite leaked after all-or-none rollback");
            foreach(var item in originalSpriteSets.Values.SelectMany(x=>x))
                Require(!item.destroyed,
                    "contact rollback destroyed shared imported Hero Sprite");
        }
        finally
        {
            foreach(var pair in originals)
            {
                Resources.Registered[pair.Key]=pair.Value;
                Resources.RegisteredSprites[pair.Key]=originalSpriteSets[pair.Key];
            }
            foreach(var item in incomingImported)
                if(item!=null&&!item.destroyed)UnityEngine.Object.Destroy(item);
            release.Invoke(app,null);
        }
    }

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
            var fallbackGpu=GUI.DrawnTextures[
                GUI.DrawnTextures.Count-1] as RenderTexture;
            int beforeRetest=Camera.RenderedTargets.Count;
            Time.frameCount=3542; // still only 5 imported cells
            Require((bool)draw.Invoke(live,new object[]{
                new Point(100,140),Color.white,false,0,false})&&
                !heroNative.enabled&&
                ReferenceEquals(heroView.sprite,fallback)&&
                !fallback.destroyed&&
                ReferenceEquals(GUI.DrawnTextures[
                    GUI.DrawnTextures.Count-1],fallbackGpu)&&
                Camera.RenderedTargets.Count==beforeRetest,
                "Unchanged partial hero art kept recreating stale frames/GPU buffers");
            Resources.RegisteredSprites[hotPath]=rebaked;
            Time.frameCount=3723;
            Require((bool)draw.Invoke(live,new object[]{
                new Point(100,140),Color.white,false,0,false})&&
                heroNative.enabled&&
                ReferenceEquals(heroView.sprite,rebaked[2])&&
                fallback.destroyed,
                "Corrected hero art did not restore accepted native contact");
            // Editor temporarily removes the ENTIRE atlas asset while
            // reimporting it. The previously accepted entire action is still
            // live; a painted damage contact must NOT flash to Idle.
            var oldTexture=Resources.Registered[hotPath];
            Resources.Registered.Remove(hotPath);
            Time.frameCount=3904;
            Require((bool)draw.Invoke(live,new object[]{
                new Point(100,140),Color.white,false,0,false})&&
                ReferenceEquals(heroView.sprite,rebaked[2])&&
                heroNative.enabled,
                "Missing atlas during Editor import incorrectly flashed an active whole-body attack to Idle");
            Time.frameCount=3905;
            Require((bool)draw.Invoke(live,new object[]{
                new Point(100,140),Color.white,false,0,false})&&
                ReferenceEquals(heroView.sprite,rebaked[2]),
                "Import retry cooldown discarded last accepted attack instead of retaining the whole body");
            // A half-written/dimensionally malformed replacement is likewise
            // never allowed to replace the last verified action silhouette.
            Resources.Registered[hotPath]=new Texture2D(
                5*256,256,TextureFormat.RGBA32,false);
            Time.frameCount=4025;
            Require((bool)draw.Invoke(live,new object[]{
                new Point(100,140),Color.white,false,0,false})&&
                ReferenceEquals(heroView.sprite,rebaked[2]),
                "Malformed replacement atlas discarded the last validated full-body action");
            Resources.Registered[hotPath]=oldTexture;
            Time.frameCount=4145;
            Require((bool)draw.Invoke(live,new object[]{
                new Point(100,140),Color.white,false,0,false})&&
                ReferenceEquals(heroView.sprite,rebaked[2])&&heroNative.enabled,
                "Restored valid atlas failed to republish the real animation clip");
        }
        finally
        {
            Resources.Registered[hotPath]=originalCells[0].texture;
            Resources.RegisteredSprites[hotPath]=originalCells;
            release.Invoke(live,null);
        }
        Require(!originalCells[2].destroyed&&!rebaked[2].destroyed,
            "Canonical hot-refresh destroyed shared imported hero Sprite");

        // Gold-haired protagonist uses its OWN SpriteRenderer capture cache.
        // A Play/replay frame-counter restart can reuse this runtime even
        // though the previous GPU target is a different rendering epoch.
        var heroEpoch=new FrontierRuntime();
        heroEpoch.Game.AppearanceWeapon=0;
        heroEpoch.ProbeAction=2;heroEpoch.ProbeFrames=6;
        heroEpoch.ProbeFrame=2;heroEpoch.ProbePosition=2f;
        try
        {
            Time.frameCount=8500;
            int epochStart=Camera.RenderedTargets.Count;
            Require((bool)draw.Invoke(heroEpoch,new object[]{
                new Point(100,140),Color.white,false,0,false})&&
                Camera.RenderedTargets.Count==epochStart+1,
                "epoch original action contact could not be captured");
            var epochTarget=Camera.RenderedTargets[epochStart];
            Time.frameCount=19;
            Require((bool)draw.Invoke(heroEpoch,new object[]{
                new Point(100,140),Color.white,false,0,false})&&
                Camera.RenderedTargets.Count==epochStart+2&&
                ReferenceEquals(Camera.RenderedTargets[epochStart+1],
                    epochTarget),
                "Play/replay rewind reused old gold-haired hero GPU pixels");
            Time.frameCount=20;
            Require((bool)draw.Invoke(heroEpoch,new object[]{
                new Point(100,140),Color.white,false,0,false})&&
                Camera.RenderedTargets.Count==epochStart+2,
                "Repaired hero GPU pose was repainted on every frame");
            // The Editor may reimport the same Sprite object IN PLACE while
            // its contents change; its InstanceID-based cache key is stable.
            // The bounded published-family review must recapture those
            // exact source pixels once, without a new Sprite allocation.
            Time.frameCount=200;
            Require((bool)draw.Invoke(heroEpoch,new object[]{
                new Point(100,140),Color.white,false,0,false})&&
                Camera.RenderedTargets.Count==epochStart+3&&
                ReferenceEquals(GUI.DrawnTextures[
                    GUI.DrawnTextures.Count-1],epochTarget),
                "In-place canonical attack art remained frozen in GPU cache");
            Time.frameCount=201;
            Require((bool)draw.Invoke(heroEpoch,new object[]{
                new Point(100,140),Color.white,false,0,false})&&
                Camera.RenderedTargets.Count==epochStart+3,
                "In-place imported hero pixel recapture churned every repaint");
            // A republished Animator graph with SAME class/Sprite assets
            // must replace the old Controller while staying on the exact
            // GameCore-controlled contact key. Preserve a briefly missing
            // controller during a partial Editor import.
            const string graphPath=
                "Frontier/HeroAnimator/ProductionVanguard";
            var originalGraph=Resources.Registered[graphPath];
            try
            {
                var republished=new RuntimeAnimatorController{
                    className="Vanguard"};
                Resources.Registered[graphPath]=republished;
                Time.frameCount=380;
                Require((bool)draw.Invoke(heroEpoch,new object[]{
                    new Point(100,140),Color.white,false,0,false})&&
                    ReferenceEquals((animatorField.GetValue(heroEpoch) as
                        Animator).runtimeAnimatorController,republished)&&
                    Camera.RenderedTargets.Count==epochStart+4,
                    "Live hero Controller reimport retained old animation graph");
                Resources.Registered.Remove(graphPath);
                Time.frameCount=560;
                Require((bool)draw.Invoke(heroEpoch,new object[]{
                    new Point(100,140),Color.white,false,0,false})&&
                    ReferenceEquals((animatorField.GetValue(heroEpoch) as
                        Animator).runtimeAnimatorController,republished),
                    "Temporary missing Controller wiped last valid hero art");
            }
            finally {Resources.Registered[graphPath]=originalGraph;}
        }
        finally {release.Invoke(heroEpoch,null);}
        // Real Unity Texture2D.Apply() changes GPU pixels without changing
        // the source Texture, imported Sprite, or its InstanceID. Check
        // revision-triggered recapture for both hero facings, with same-frame
        // GUI submissions immutable until the next safe repaint.
        var heroPixels=new FrontierRuntime();
        heroPixels.Game.AppearanceWeapon=0;
        heroPixels.ProbeAction=2;heroPixels.ProbeFrames=6;
        heroPixels.ProbeFrame=2;heroPixels.ProbePosition=2f;
        var paintedAtlas=(Texture2D)Resources.Registered[
            "Frontier/HeroAnimator/Canonical/Hero_Vanguard_Attack_E"];
        try
        {
            Time.frameCount=9800;
            int pixelStart=Camera.RenderedTargets.Count;
            Require((bool)draw.Invoke(heroPixels,new object[]{
                new Point(100,140),Color.white,false,0,false})&&
                (bool)draw.Invoke(heroPixels,new object[]{
                new Point(100,140),Color.white,true,0,false})&&
                Camera.RenderedTargets.Count==pixelStart+2,
                "hero texture-upload setup failed to capture both facings");
            var right=Camera.RenderedTargets[pixelStart];
            var left=Camera.RenderedTargets[pixelStart+1];
            Time.frameCount=9801;
            paintedAtlas.IncrementUpdateCount();
            Require((bool)draw.Invoke(heroPixels,new object[]{
                new Point(100,140),Color.white,false,0,false})&&
                Camera.RenderedTargets.Count==pixelStart+3&&
                ReferenceEquals(Camera.RenderedTargets[pixelStart+2],right),
                "Same-ID hero texture upload retained stale full-body GPU pixels");
            paintedAtlas.IncrementUpdateCount();
            Require((bool)draw.Invoke(heroPixels,new object[]{
                new Point(100,140),Color.white,false,0,false})&&
                Camera.RenderedTargets.Count==pixelStart+3,
                "Second in-flight source upload overwrote a submitted hero RenderTexture");
            Time.frameCount=9802;
            Require((bool)draw.Invoke(heroPixels,new object[]{
                new Point(100,140),Color.white,false,0,false})&&
                (bool)draw.Invoke(heroPixels,new object[]{
                new Point(100,140),Color.white,true,0,false})&&
                Camera.RenderedTargets.Count==pixelStart+5&&
                ReferenceEquals(Camera.RenderedTargets[pixelStart+3],right)&&
                ReferenceEquals(Camera.RenderedTargets[pixelStart+4],left),
                "Latest in-place hero update failed to refresh both directions");
            Time.frameCount=9803;
            Require((bool)draw.Invoke(heroPixels,new object[]{
                new Point(100,140),Color.white,false,0,false})&&
                Camera.RenderedTargets.Count==pixelStart+5,
                "Unchanged canonical pixels kept recapturing every frame");
        }
        finally {release.Invoke(heroPixels,null);}
        // GPU failure retry must also reset after a large frame-count rewind,
        // rather than pinning the ONE blonde hero to fallback for 10000 frames.
        var heroCooldownEpoch=new FrontierRuntime();
        heroCooldownEpoch.Game.AppearanceWeapon=0;
        heroCooldownEpoch.ProbeAction=2;
        heroCooldownEpoch.ProbeFrames=6;
        heroCooldownEpoch.ProbeFrame=2;
        heroCooldownEpoch.ProbePosition=2f;
        try
        {
            Time.frameCount=11000;
            Require((bool)draw.Invoke(heroCooldownEpoch,new object[]{
                new Point(100,140),Color.white,false,0,false}),
                "epoch retry setup missing canonical Hero");
            var retryField=typeof(FrontierRuntime).GetField(
                "heroCaptureRetryAt",flags);
            brokenField.SetValue(heroCooldownEpoch,true);
            retryField.SetValue(heroCooldownEpoch,11120);
            Time.frameCount=27;
            Require((bool)draw.Invoke(heroCooldownEpoch,new object[]{
                new Point(100,140),Color.white,false,0,false})&&
                !(bool)brokenField.GetValue(heroCooldownEpoch),
                "old GPU failure cooldown survived new Play frame-count epoch");
        }
        finally {release.Invoke(heroCooldownEpoch,null);}
        // 51 distinct actions in one repaint intentionally exceed the 48
        // pose soft limit: no submitted render texture may be destroyed.
        // Next repaint, EVEN if its frameCount has rewound, evict the full
        // backlog and return the cache to <=48 rather than just evicting one.
        var heroCrowd=new FrontierRuntime();
        var cacheField=typeof(FrontierRuntime).GetField(
            "heroCapturedFrames",flags);
        var crowdCache=(System.Collections.IDictionary)
            cacheField.GetValue(heroCrowd);
        var previousTargets=new List<RenderTexture>();
        try
        {
            Time.frameCount=12000;
            for(int k=0;k<51;k++)
            {
                int cls=k/28,cmd=(k%28)/4,dir=k%4;
                heroCrowd.Game.AppearanceWeapon=cls;
                heroCrowd.ProbeAction=cmd;
                heroCrowd.ProbeFrames=lengths[cmd];
                heroCrowd.ProbeFrame=cmd==0?0:
                    cmd==2||cmd==3?2:1;
                heroCrowd.ProbePosition=heroCrowd.ProbeFrame;
                int facing=dir<2?0:dir==2?1:2;
                Require((bool)draw.Invoke(heroCrowd,new object[]{
                    new Point(100,140),Color.white,dir==1,
                    facing,false}),
                    "crowded hero action render unexpectedly fell back");
                previousTargets.Add(GUI.DrawnTextures[
                    GUI.DrawnTextures.Count-1] as RenderTexture);
            }
            Require(crowdCache.Count==51&&
                previousTargets.TrueForAll(x=>x!=null&&x.releaseCount==0),
                "same-frame hero action render prematurely evicted queued images");
            Time.frameCount=31;
            heroCrowd.Game.AppearanceWeapon=1;
            // Action 6 was not in the initial 51 cached poses (0..50).
            // A NEW capture is required to exercise eviction, rather
            // than silently revisiting an already captured frame.
            heroCrowd.ProbeAction=6;heroCrowd.ProbeFrames=4;
            heroCrowd.ProbeFrame=1;heroCrowd.ProbePosition=1f;
            Require((bool)draw.Invoke(heroCrowd,new object[]{
                new Point(100,140),Color.white,false,2,false})&&
                crowdCache.Count<=48&&
                previousTargets.Exists(x=>x.releaseCount>0),
                "old-epoch hero GPU cache failed to reclaim a crowded 51-pose backlog");
        }
        finally {release.Invoke(heroCrowd,null);}
        Console.WriteLine("PASS hero GPU epoch recovery, in-place original art recapture, live Animator graph hot-import, 51-pose eviction safety");

        VerifyAtomicHeroFamilySwap(release,flags);
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
