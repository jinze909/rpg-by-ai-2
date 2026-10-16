// Executes the actual nonhero native-conditional capture method against
// instrumented Unity declarations. This tests texture identity and lifetime,
// NOT GPU pixels, GameView, or Animator playback.
using System;
using System.Reflection;
using UnityEngine;
using Starfall;

public static class NativeNonheroCaptureChecks
{
    static void Require(bool ok, string why)
    { if (!ok) throw new Exception("NONHERO_CAPTURE_REGRESSION: " + why); }

    static int MaxExpectedSpriteWrappersForBudget()
    {
        // A 512px ARGB32 + 16-bit depth capture is at least 1.5MiB.
        // Room-spanning evictions may retain fewer, never indefinitely more.
        return 46;
    }
    static void Main()
    {
        var runtime = new FrontierRuntime();
        var method = typeof(FrontierRuntime).GetMethod(
            "DrawSpriteRendererActor", BindingFlags.Instance | BindingFlags.NonPublic);
        var release = typeof(FrontierRuntime).GetMethod(
            "ReleaseActorCapture", BindingFlags.Instance | BindingFlags.NonPublic);
        Require(method != null && release != null, "missing live capture/release");
        var sheet = new Texture2D(512, 512, TextureFormat.RGBA32, false);
        Time.frameCount = 100;
        GUI.DrawnTextures.Clear();
        Camera.RenderedTargets.Clear();
        Action<int,bool> draw = (frame,flip) => {
            bool drawn = (bool)method.Invoke(runtime, new object[] {
                sheet, 4, 4, frame, new Point(200, 220), 96f,
                new Color(1, 1, 1, 1), flip, 0f });
            Require(drawn, "native actor fell back unexpectedly");
        };
        draw(0, false);
        draw(1, false);
        draw(0, false);
        draw(0, true);
        Require(GUI.DrawnTextures.Count == 4, "four actor draw calls required");
        Texture first = GUI.DrawnTextures[0], second = GUI.DrawnTextures[1];
        Texture reused = GUI.DrawnTextures[2], mirrored = GUI.DrawnTextures[3];
        Require(!ReferenceEquals(first, second),
            "different actors share one mutable RenderTexture");
        Require(ReferenceEquals(first, reused),
            "same pose should reuse immutable GPU capture");
        Require(!ReferenceEquals(first, mirrored),
            "flipped pose overwrote unflipped pending draw");
        Require(Camera.RenderedTargets.Count == 3,
            "only three distinct pose/flip captures should render");
        Require(((RenderTexture)first).width == 128 &&
            ((RenderTexture)first).height == 128,
            "128px resident/monster pose must not be resampled into a 256px intermediate");
        Require(ReferenceEquals(Camera.RenderedTargets[0], first) &&
            ReferenceEquals(Camera.RenderedTargets[1], second) &&
            ReferenceEquals(Camera.RenderedTargets[2], mirrored),
            "camera target differs from texture submitted to GUI");
        Time.frameCount = 101;
        draw(0, false);
        Require(ReferenceEquals(GUI.DrawnTextures[4], first) &&
            Camera.RenderedTargets.Count == 3,
            "cached whole-body frame should survive a new frame");
        ((RenderTexture)first).Release(); // simulate GPU device loss
        draw(0, false);
        Require(Camera.RenderedTargets.Count == 4 &&
            ReferenceEquals(Camera.RenderedTargets[3], first),
            "lost render target must be reconstructed before drawing");
        // Source animation clips may carry stale scale/position curves on
        // a reused offscreen rig. Inject this exact failure BEFORE a new
        // captured contact, then inspect the REAL native capture path.
        var rigField=typeof(FrontierRuntime).GetField("actorCaptureRoot",
            BindingFlags.Instance|BindingFlags.NonPublic);
        var cameraField=typeof(FrontierRuntime).GetField("actorCaptureCamera",
            BindingFlags.Instance|BindingFlags.NonPublic);
        var offscreenRendererField=typeof(FrontierRuntime).GetField("actorCaptureRenderer",
            BindingFlags.Instance|BindingFlags.NonPublic);
        var rig=rigField.GetValue(runtime) as GameObject;
        var captureCam=cameraField.GetValue(runtime) as Camera;
        var captureSprite=offscreenRendererField.GetValue(runtime) as SpriteRenderer;
        Require(rig!=null&&captureCam!=null&&captureSprite!=null,
            "native actor capture rig was not constructed");
        rig.transform.localScale=new Vector3(1.75f,1.75f,1f);
        rig.transform.position=new Vector3(9300f,9400f,0f);
        rig.transform.localRotation=Quaternion.Euler(0f,0f,24f);
        captureSprite.transform.localRotation=Quaternion.Euler(0f,0f,-18f);
        captureSprite.color=new Color(.7f,.4f,.2f,.12f);
        captureSprite.transform.localPosition=new Vector3(.24f,.31f,0f);
        captureSprite.transform.localScale=new Vector3(2.2f,2.2f,1f);
        captureCam.transform.localScale=new Vector3(1.4f,1.4f,1f);
        captureCam.transform.localRotation=Quaternion.Euler(0f,0f,11f);
        captureCam.transform.localPosition=new Vector3(1f,1f,-9f);
        captureCam.orthographicSize=.9f;
        captureCam.aspect=1.6f;
        Time.frameCount=102;
        draw(2,false);
        Require(rig.transform.localScale.x==1f&&
            rig.transform.localScale.y==1f&&
            Math.Abs(rig.transform.position.x-9200f)<.0001f&&
            Math.Abs(rig.transform.position.y-9200f)<.0001f&&
            rig.transform.localRotation.zAngle==0f&&
            captureSprite.transform.localRotation.zAngle==0f&&
            captureCam.transform.localRotation.zAngle==0f&&
            captureSprite.color.r==1f&&captureSprite.color.g==1f&&
            captureSprite.color.b==1f&&captureSprite.color.a==1f&&
            captureSprite.transform.localScale.x==1f&&
            captureSprite.transform.localScale.y==1f&&
            captureSprite.transform.localPosition.x==0f&&
            captureSprite.transform.localPosition.y==0f&&
            captureCam.transform.localScale.x==1f&&
            captureCam.transform.localScale.y==1f&&
            Math.Abs(captureCam.transform.localPosition.x)<.0001f&&
            Math.Abs(captureCam.transform.localPosition.y-.4375f)<.0001f&&
            Math.Abs(captureCam.orthographicSize-.5f)<.0001f&&
            Math.Abs(captureCam.aspect-1f)<.0001f,
            "nonhero native Animator inherited stale transform/zoom; source body may enlarge or float");
        // Actual phase-2 Boss cells are 320px; preserve each source pixel
        // in the SpriteRenderer capture rather than shrinking 320 -> 256.
        var bossSheet = new Texture2D(1280, 5760, TextureFormat.RGBA32, false);
        bool bossDrawn = (bool)method.Invoke(runtime, new object[] {
            bossSheet, 4, 18, 17, new Point(200,220), 240f,
            new Color(1,1,1,1), false, 0f });
        Require(bossDrawn, "320px Briar Beast capture unexpectedly fell back");
        var bossCapture = (RenderTexture)GUI.DrawnTextures[
            GUI.DrawnTextures.Count - 1];
        Require(bossCapture.width == 320 && bossCapture.height == 320,
            "Boss keyframes must retain source-pixel resolution");
        // The normalized whole-body bridge also supports rectangular 192x128
        // atlas cells without first forcing them through a 256px framebuffer.
        var wideSheet = new Texture2D(768, 768, TextureFormat.RGBA32, false);
        bool wideDrawn = (bool)method.Invoke(runtime, new object[] {
            wideSheet, 4, 6, 2, new Point(200,220), 96f,
            new Color(1,1,1,1), false, 0f });
        Require(wideDrawn, "192px wide actor capture unexpectedly fell back");
        var wideCapture = (RenderTexture)GUI.DrawnTextures[
            GUI.DrawnTextures.Count - 1];
        Require(wideCapture.width == 192 && wideCapture.height == 192,
            "rectangular art should retain its longest source edge");
        // A new atlas of identical dimensions cannot inherit the prior
        // actor's cached pixels (the offline stub must model Unity IDs).
        var anotherSheet = new Texture2D(512, 512, TextureFormat.RGBA32, false);
        bool anotherDrawn = (bool)method.Invoke(runtime, new object[] {
            anotherSheet, 4, 4, 0, new Point(200,220), 96f,
            new Color(1,1,1,1), false, 0f });
        Require(anotherDrawn && !ReferenceEquals(first,
            GUI.DrawnTextures[GUI.DrawnTextures.Count - 1]),
            "unrelated atlases must not share the same cached capture");
        // Oversized unreviewed cells must fall back, never allocate
        // multi-megapixel GPU targets in a crowded game scene.
        var hugeSheet = new Texture2D(3072, 3072, TextureFormat.RGBA32, false);
        int before = Camera.RenderedTargets.Count;
        bool hugeDrawn = (bool)method.Invoke(runtime, new object[] {
            hugeSheet, 4, 4, 0, new Point(200,220), 96f,
            new Color(1,1,1,1), false, 0f });
        Require(!hugeDrawn && Camera.RenderedTargets.Count == before,
            "oversized actors should use the existing exact-cell fallback");
        // Exercise the ACTUAL runtime GPU-failure handler on a new actor
        // scene. Bosses, enemies and residents all share this drawing path.
        var recovering = new FrontierRuntime();
        Camera.InjectGpuFailureOnce = true;
        Time.frameCount = 1000;
        int previousRenders = Camera.RenderedTargets.Count;
        bool initial = (bool)method.Invoke(recovering, new object[] {
            bossSheet, 4, 18, 17, new Point(200,220), 240f,
            new Color(1,1,1,1), false, 0f });
        Require(!initial && Camera.RenderedTargets.Count == previousRenders,
            "failed native render must fall back to exact Boss source cell");
        Time.frameCount = 1001;
        bool waiting = (bool)method.Invoke(recovering, new object[] {
            bossSheet, 4, 18, 17, new Point(200,220), 240f,
            new Color(1,1,1,1), false, 0f });
        Require(!waiting && Camera.RenderedTargets.Count == previousRenders,
            "native actor capture retried on every repaint during cooldown");
        Time.frameCount = 1120;
        bool healed = (bool)method.Invoke(recovering, new object[] {
            bossSheet, 4, 18, 17, new Point(200,220), 240f,
            new Color(1,1,1,1), false, 0f });
        Require(healed && Camera.RenderedTargets.Count == previousRenders + 1,
            "nonhero GPU capture remained permanently disabled after recovery");
        RenderTexture recoveredTarget = GUI.DrawnTextures[
            GUI.DrawnTextures.Count - 1] as RenderTexture;
        Require(recoveredTarget != null && recoveredTarget.width == 320,
            "recovered Boss must retain native 320px source resolution");
        release.Invoke(recovering, null);
        Require(recoveredTarget.releaseCount >= 1 && !recoveredTarget.IsCreated(),
            "GPU fault recovery leaked restored render target");
        // An actual device can refuse a RenderTexture.Create() call with
        // false instead of an exception. No native Actor draw may claim
        // success while its target was never allocated.
        var noMemory=new FrontierRuntime();
        int beforeAlloc=Camera.RenderedTargets.Count;
        RenderTexture.InjectCreateFailureOnce=true;
        Time.frameCount=1450;
        bool allocDenied=(bool)method.Invoke(noMemory,new object[]{
            bossSheet,4,18,17,new Point(200,220),240f,
            Color.white,false,0f});
        Require(!allocDenied&&Camera.RenderedTargets.Count==beforeAlloc&&
            !RenderTexture.InjectCreateFailureOnce,
            "nonhero ignored false Create() and drew a transparent Boss");
        Time.frameCount=1451;
        bool allocThrottle=(bool)method.Invoke(noMemory,new object[]{
            bossSheet,4,18,17,new Point(200,220),240f,
            Color.white,false,0f});
        Require(!allocThrottle&&Camera.RenderedTargets.Count==beforeAlloc,
            "native Boss retried refused RenderTexture.Create every frame");
        Time.frameCount=1570;
        bool allocRecovered=(bool)method.Invoke(noMemory,new object[]{
            bossSheet,4,18,17,new Point(200,220),240f,
            Color.white,false,0f});
        Require(allocRecovered&&Camera.RenderedTargets.Count==beforeAlloc+1,
            "original 320px Boss Sprite failed to recover native GPU capture");
        var createdTarget=GUI.DrawnTextures[GUI.DrawnTextures.Count-1]
            as RenderTexture;
        Require(createdTarget!=null&&createdTarget.width==320,
            "Boss GPU allocation recovery shrank source pixels");
        createdTarget.Release();
        int beforeLost=Camera.RenderedTargets.Count;
        RenderTexture.InjectCreateFailureOnce=true;
        Time.frameCount=1580;
        bool recreateDenied=(bool)method.Invoke(noMemory,new object[]{
            bossSheet,4,18,17,new Point(200,220),240f,
            Color.white,false,0f});
        Require(!recreateDenied&&Camera.RenderedTargets.Count==beforeLost,
            "device-loss recreation failure submitted uncreated Actor target");
        Time.frameCount=1700;
        bool recreateRecovered=(bool)method.Invoke(noMemory,new object[]{
            bossSheet,4,18,17,new Point(200,220),240f,
            Color.white,false,0f});
        Require(recreateRecovered&&Camera.RenderedTargets.Count==beforeLost+1,
            "nonhero native Sprite failed to recover from denied GPU recreation");
        release.Invoke(noMemory,null);

        // Native animator is no longer a disabled placeholder. Four actual
        // art families: raid monster, guard, 3-phase Boss and talking NPC.
        // Every imported Sprite key points at its original full-body atlas.
        string[] families={"MossboundRaider","CinderstoneGuard",
            "BriarBeast","EmberfallResidents"};
        int[] rowCounts={6,6,18,4};
        for(int kind=0;kind<families.Length;kind++)
        {
            string family=families[kind];
            int height=family=="BriarBeast"?320:128;
            var atlas=new Texture2D(height*4,height*rowCounts[kind],
                TextureFormat.RGBA32,false);
            atlas.name=family;
            Resources.Registered["Frontier/Art/"+family]=atlas;
            Resources.Registered[
                "Frontier/Actors/Native/Actor_"+family]=
                new RuntimeAnimatorController {className="Actor_"+family};
            Sprite[] imported=new Sprite[4*rowCounts[kind]];
            for(int i=0;i<imported.Length;i++)
                imported[i]=Sprite.Create(atlas,
                    new Rect((i%4)*height,
                        atlas.height-(i/4+1)*height,height,height),
                    new Vector2(.5f,.0625f),height,0,SpriteMeshType.FullRect);
            Resources.RegisteredSprites["Frontier/Art/"+family]=imported;
            var character=new FrontierRuntime();
            var spriteField=typeof(FrontierRuntime).GetField(
                "actorCaptureRenderer",BindingFlags.Instance|BindingFlags.NonPublic);
            var animatorField=typeof(FrontierRuntime).GetField(
                "actorCaptureAnimator",BindingFlags.Instance|BindingFlags.NonPublic);
            Require(spriteField!=null&&animatorField!=null,
                "actual nonhero SpriteRenderer/Animator fields absent");
            int[] frames={0,4*(rowCounts[kind]/2)+2,
                4*rowCounts[kind]-1};
            for(int i=0;i<frames.Length;i++)
            {
                Time.frameCount=1200+i;
                int f=frames[i];
                bool success=(bool)method.Invoke(character,new object[]{
                    atlas,4,rowCounts[kind],f,new Point(200,220),96f,
                    new Color(1,1,1,1),i==2,0f});
                var liveRenderer=spriteField.GetValue(character) as SpriteRenderer;
                var liveAnimator=animatorField.GetValue(character) as Animator;
                Require(success&&liveRenderer!=null&&liveAnimator!=null&&
                    liveAnimator.enabled&&
                    ReferenceEquals(liveRenderer.sprite,imported[f])&&
                    liveRenderer.flipX==(i==2),
                    "native row/cell/profession mismatch for "+family+" frame "+f);
                Require(Math.Abs(liveAnimator.lastNormalizedSample-
                    ((f%4+.5f)/4f))<.0001f,
                    "nonhero native frame sampled at wrong action-clock boundary");
                var liveFrames=(System.Collections.IDictionary)
                    typeof(FrontierRuntime).GetField("actorFrameSprites",
                        BindingFlags.Instance|BindingFlags.NonPublic).
                        GetValue(character);
                string approvedKey=atlas.GetInstanceID()+":4:"+
                    rowCounts[kind]+":"+f;
                Require(ReferenceEquals(liveFrames[approvedKey],imported[f]),
                    "Native production remade approved imported art "+family+"/"+f);
            }
            release.Invoke(character,null);
            Require(!imported[0].destroyed&&
                !imported[frames[frames.Length-1]].destroyed,
                "Native cleanup destroyed Unity-owned imported Sprite "+family);
        }
        // Production native Controllers use the already approved 6x3
        // Monsters and 4x4 rich Npcs atlas rather than withdrawn low-detail
        // replacements. Verify GameCore action cells 0..5 in all 3 monster
        // identities and exact NPC individual cells across four rows.
        string[] originalFamilies={"Monsters","WalkMonsters","Npcs",
            "EnemyArcherVertical","Bosses","WalkBossA","WalkBossB",
            "CompanionMira","CompanionIvo","CompanionSen",
            "WalkMira","WalkIvo","WalkSen"};
        int[] origCols={6,4,4,3,4,4,4,3,3,3,4,4,4};
        int[] origRows={3,3,4,2,8,4,4,4,4,4,3,3,3};
        for(int i=0;i<originalFamilies.Length;i++)
        {
            string family=originalFamilies[i];
            int cols=origCols[i],rows=origRows[i];
            int height=128;
            var atlas=new Texture2D(cols*height,rows*height,
                TextureFormat.RGBA32,false);
            atlas.name=family;
            string originalResource="Frontier/Art/"+family;
            Resources.Registered[originalResource]=atlas;
            Sprite[] slices=new Sprite[cols*rows];
            for(int frame=0;frame<slices.Length;frame++)
                slices[frame]=Sprite.Create(atlas,
                    new Rect((frame%cols)*height,
                        atlas.height-(frame/cols+1)*height,height,height),
                    new Vector2(.5f,.0625f),height,0,SpriteMeshType.FullRect);
            Resources.RegisteredSprites[originalResource]=slices;
            Resources.Registered["Frontier/Actors/Native/Actor_"+family]=
                new RuntimeAnimatorController{className="Actor_"+family};
            var actualOriginal=new FrontierRuntime();
            int[] cells={0,cols-1,(rows-1)*cols,rows*cols-1};
            for(int v=0;v<cells.Length;v++)
            {
                Time.frameCount=1550+i*5+v;
                int frame=cells[v];
                bool ok=(bool)method.Invoke(actualOriginal,new object[]{
                    atlas,cols,rows,frame,new Point(200,220),96f,
                    new Color(1,1,1,1),v==3,0f});
                var rendererField=typeof(FrontierRuntime).GetField(
                    "actorCaptureRenderer",BindingFlags.Instance|BindingFlags.NonPublic);
                var animatorField=typeof(FrontierRuntime).GetField(
                    "actorCaptureAnimator",BindingFlags.Instance|BindingFlags.NonPublic);
                var artRenderer=rendererField.GetValue(actualOriginal) as SpriteRenderer;
                var animator=animatorField.GetValue(actualOriginal) as Animator;
                Require(ok&&animator!=null&&animator.enabled&&
                    ReferenceEquals(artRenderer.sprite,slices[frame])&&
                    artRenderer.flipX==(v==3),
                    "accepted original atlas native capture selected wrong family/pose: "+
                    family+"/"+frame);
                Require(Math.Abs(animator.lastNormalizedSample-
                    ((frame%cols+.5f)/cols))<.0001f,
                    "published source native sample did not use exact frame center");
                var originalCache=(System.Collections.IDictionary)
                    typeof(FrontierRuntime).GetField("actorFrameSprites",
                        BindingFlags.Instance|BindingFlags.NonPublic).
                        GetValue(actualOriginal);
                string approvedKey=atlas.GetInstanceID()+":"+
                    cols+":"+rows+":"+frame;
                Require(ReferenceEquals(originalCache[approvedKey],slices[frame]),
                    "Approved source "+family+" unexpectedly regenerated by Sprite.Create");
            }
            release.Invoke(actualOriginal,null);
            Require(!slices[0].destroyed&&
                !slices[cells[cells.Length-1]].destroyed,
                "Accepted original actor Sprite destroyed on cleanup");
        }
        // Hot-reimport of an ALREADY ACCEPTED family: unlike startup
        // recovery, every pose owns cached GPU pixels from the old full-
        // body art. Refresh one complete SpriteImporter family atomically,
        // and invalidate normal/mirrored captures on safe later repaints.
        var upgrade=new FrontierRuntime();
        const string livePath="Frontier/Art/Monsters";
        var liveTexture=(Texture2D)Resources.Registered[livePath];
        var oldSprites=Resources.RegisteredSprites[livePath];
        var newSprites=new Sprite[oldSprites.Length];
        for(int i=0;i<newSprites.Length;i++)
        {
            var old=oldSprites[i];
            newSprites[i]=Sprite.Create(liveTexture,old.rect,
                new Vector2(.5f,.0625f),old.pixelsPerUnit,0,
                SpriteMeshType.FullRect);
        }
        var spriteFieldForRefresh=typeof(FrontierRuntime).GetField(
            "actorFrameSprites",BindingFlags.Instance|
                BindingFlags.NonPublic);
        var framesInUse=(System.Collections.IDictionary)
            spriteFieldForRefresh.GetValue(upgrade);
        string face2=liveTexture.GetInstanceID()+":6:3:2";
        string face3=liveTexture.GetInstanceID()+":6:3:3";
        try
        {
            Time.frameCount=4000;
            Require((bool)method.Invoke(upgrade,new object[]{
                liveTexture,6,3,2,new Point(180,215),96f,
                Color.white,false,0f}),"Warm original whole-body pose failed");
            var originalGpu=GUI.DrawnTextures[
                GUI.DrawnTextures.Count-1] as RenderTexture;
            Require((bool)method.Invoke(upgrade,new object[]{
                liveTexture,6,3,2,new Point(180,215),96f,
                Color.white,true,0f})&&
                (bool)method.Invoke(upgrade,new object[]{
                liveTexture,6,3,3,new Point(180,215),96f,
                Color.white,false,0f}),
                "Initial imported normal and mirror frames missing");
            int oldRenderCount=Camera.RenderedTargets.Count;
            Resources.RegisteredSprites[livePath]=newSprites;
            Time.frameCount=4001;
            Require((bool)method.Invoke(upgrade,new object[]{
                liveTexture,6,3,2,new Point(180,215),96f,
                Color.white,false,0f})&&
                ReferenceEquals(framesInUse[face2],oldSprites[2])&&
                Camera.RenderedTargets.Count==oldRenderCount,
                "Imported Source was rescanned/repainted on every frame");
            // A newly visited monster action DURING the import cooldown
            // must show the last atomically accepted source Sprite rather
            // than a new Animator cell from a partly rebaked family.
            Time.frameCount=4002;
            Require((bool)method.Invoke(upgrade,new object[]{
                liveTexture,6,3,4,new Point(180,215),96f,
                Color.white,false,0f})&&
                ReferenceEquals(framesInUse[
                    liveTexture.GetInstanceID()+":6:3:4"],oldSprites[4])&&
                ReferenceEquals(
                    (typeof(FrontierRuntime).GetField(
                        "actorCaptureRenderer",BindingFlags.Instance|
                        BindingFlags.NonPublic).GetValue(upgrade) as
                        SpriteRenderer).sprite,oldSprites[4])&&
                Camera.RenderedTargets.Count==oldRenderCount+1,
                "Animator leaked newly imported single monster contact cell before whole-family validation");
            int afterBoundedImport=Camera.RenderedTargets.Count;
            Time.frameCount=4181;
            Require((bool)method.Invoke(upgrade,new object[]{
                liveTexture,6,3,2,new Point(180,215),96f,
                Color.white,false,0f})&&
                ReferenceEquals(framesInUse[face2],newSprites[2])&&
                ReferenceEquals(GUI.DrawnTextures[
                    GUI.DrawnTextures.Count-1],originalGpu)&&
                Camera.RenderedTargets.Count==afterBoundedImport+1,
                "Native accepted hot-reimport did not refresh existing GPU pose");
            Time.frameCount=4182;
            Require((bool)method.Invoke(upgrade,new object[]{
                liveTexture,6,3,3,new Point(180,215),96f,
                Color.white,false,0f})&&
                ReferenceEquals(framesInUse[face3],newSprites[3])&&
                Camera.RenderedTargets.Count==afterBoundedImport+2,
                "Full-family artist revision left neighboring action stale");
            Time.frameCount=4183;
            Require((bool)method.Invoke(upgrade,new object[]{
                liveTexture,6,3,2,new Point(180,215),96f,
                Color.white,true,0f})&&
                Camera.RenderedTargets.Count==afterBoundedImport+3,
                "Flipped GPU pose remained a pre-reimport enemy silhouette");
            // Corrupt one frame in the next complete import. Nothing may
            // mix old, new, and corrupted importer Sprites.
            var incomplete=(Sprite[])newSprites.Clone();
            incomplete[4]=Sprite.Create(liveTexture,
                newSprites[4].rect,new Vector2(.5f,.3f),
                newSprites[4].pixelsPerUnit,0,SpriteMeshType.FullRect);
            Resources.RegisteredSprites[livePath]=incomplete;
            Time.frameCount=4362;
            Require((bool)method.Invoke(upgrade,new object[]{
                liveTexture,6,3,2,new Point(180,215),96f,
                Color.white,false,0f}),
                "Missing one imported foot pivot hid the live monster");
            var safeAccepted=framesInUse[face2] as Sprite;
            Require(safeAccepted!=null&&
                ReferenceEquals(safeAccepted,newSprites[2])&&
                !safeAccepted.destroyed&&
                ReferenceEquals(safeAccepted.texture,liveTexture)&&
                ReferenceEquals((typeof(FrontierRuntime).GetField(
                    "actorCaptureRenderer",BindingFlags.Instance|
                    BindingFlags.NonPublic).GetValue(upgrade) as
                    SpriteRenderer).sprite,safeAccepted)&&
                Camera.RenderedTargets.Count==afterBoundedImport+3,
                "Partial reimport must retain the last complete accepted source and GPU pose");
            // A second frame within the retry cooldown must not re-scan
            // the importer or recapture unrelated active animation frames.
            int beforeRetryScan=Resources.LoadAllCounts[livePath];
            Time.frameCount=4363;
            Require((bool)method.Invoke(upgrade,new object[]{
                liveTexture,6,3,3,new Point(180,215),96f,
                Color.white,false,0f})&&
                ReferenceEquals(framesInUse[face3],newSprites[3])&&
                Resources.LoadAllCounts[livePath]==beforeRetryScan&&
                Camera.RenderedTargets.Count==afterBoundedImport+3,
                "Partial actor art retried or invalidated sibling animation cells every repaint");
            Resources.RegisteredSprites[livePath]=newSprites;
            Time.frameCount=4483;
            Require((bool)method.Invoke(upgrade,new object[]{
                liveTexture,6,3,2,new Point(180,215),96f,
                Color.white,false,0f})&&
                ReferenceEquals(framesInUse[face2],newSprites[2])&&
                !safeAccepted.destroyed&&
                Camera.RenderedTargets.Count==afterBoundedImport+3,
                "Corrected complete actor art unnecessarily replaced an already accepted frame");
            release.Invoke(upgrade,null);
            Require(!oldSprites[2].destroyed&&
                !newSprites[2].destroyed&&
                !newSprites[3].destroyed,
                "Source hot-reimport released original Unity-owned art");
        }
        finally
        {
            Resources.RegisteredSprites[livePath]=oldSprites;
            release.Invoke(upgrade,null);
        }
        Console.WriteLine("PASS complete native actor Sprite hot-reimport, mirrored GPU frames, atomic partial-import hold and zero-flash recovery");

        // Editor restart / test replay can rewind the frame counter without
        // replacing the live FrontierRuntime instance. A previous epoch's
        // 180-frame cache deadline must not hide freshly republished complete
        // imported actor poses. Both GPU orientations require a new capture.
        const string rewindPath="Frontier/Art/Monsters";
        var rewindSource=(Texture2D)Resources.Registered[rewindPath];
        var rewindOriginal=Resources.RegisteredSprites[rewindPath];
        var rewindRevision=new Sprite[rewindOriginal.Length];
        for(int i=0;i<rewindRevision.Length;i++)
        {
            var original=rewindOriginal[i];
            rewindRevision[i]=Sprite.Create(rewindSource,original.rect,
                new Vector2(.5f,.0625f),original.pixelsPerUnit,0,
                SpriteMeshType.FullRect);
        }
        var rewindProbe=new FrontierRuntime();
        var rewindCache=(System.Collections.IDictionary)
            typeof(FrontierRuntime).GetField("actorFrameSprites",
                BindingFlags.Instance|BindingFlags.NonPublic).
                GetValue(rewindProbe);
        string rewindKey=rewindSource.GetInstanceID()+":6:3:2";
        try
        {
            Time.frameCount=9600;
            Require((bool)method.Invoke(rewindProbe,new object[]{
                rewindSource,6,3,2,new Point(200,220),96f,
                Color.white,false,0f})&&
                (bool)method.Invoke(rewindProbe,new object[]{
                rewindSource,6,3,2,new Point(200,220),96f,
                Color.white,true,0f})&&
                ReferenceEquals(rewindCache[rewindKey],rewindOriginal[2]),
                "Could not warm both approved original source orientations");
            int beforeRewind=Camera.RenderedTargets.Count;
            Resources.RegisteredSprites[rewindPath]=rewindRevision;
            Time.frameCount=20; // new clock epoch, before old recheckAt=9780
            Require((bool)method.Invoke(rewindProbe,new object[]{
                rewindSource,6,3,2,new Point(200,220),96f,
                Color.white,false,0f})&&
                ReferenceEquals(rewindCache[rewindKey],rewindRevision[2])&&
                Camera.RenderedTargets.Count==beforeRewind+1,
                "A clock rewind preserved stale pre-import monster body pixels");
            Time.frameCount=21;
            Require((bool)method.Invoke(rewindProbe,new object[]{
                rewindSource,6,3,2,new Point(200,220),96f,
                Color.white,true,0f})&&
                Camera.RenderedTargets.Count==beforeRewind+2,
                "A clock rewind left the mirrored monster cache unrefreshed");
            Require(!rewindOriginal[2].destroyed,
                "Re-import recovery destroyed an original Unity-owned Sprite");
        }
        finally
        {
            Resources.RegisteredSprites[rewindPath]=rewindOriginal;
            release.Invoke(rewindProbe,null);
        }
        // A previous epoch can also leave a partial SpriteImporter family
        // in a 120-frame retry hold. Republish the complete family immediately
        // after the rewind, without mixing any of the incomplete subassets.
        var rewindRetry=new FrontierRuntime();
        var retryCache=(System.Collections.IDictionary)
            typeof(FrontierRuntime).GetField("actorFrameSprites",
                BindingFlags.Instance|BindingFlags.NonPublic).
                GetValue(rewindRetry);
        try
        {
            Resources.RegisteredSprites[rewindPath]=
                new Sprite[]{rewindOriginal[0]};
            Time.frameCount=9800;
            Require((bool)method.Invoke(rewindRetry,new object[]{
                rewindSource,6,3,2,new Point(200,220),96f,
                Color.white,false,0f}),
                "Incomplete imported monster atlas lost its full-body fallback");
            var incompleteWrapper=retryCache[rewindKey] as Sprite;
            Require(incompleteWrapper!=null&&
                !ReferenceEquals(incompleteWrapper,rewindOriginal[2]),
                "The partial import did not quarantine its authored fallback");
            Resources.RegisteredSprites[rewindPath]=rewindOriginal;
            Time.frameCount=40;
            Require((bool)method.Invoke(rewindRetry,new object[]{
                rewindSource,6,3,2,new Point(200,220),96f,
                Color.white,false,0f})&&
                ReferenceEquals(retryCache[rewindKey],rewindOriginal[2])&&
                incompleteWrapper.destroyed,
                "Rewound partial-import retry remained stuck on obsolete fallback");
        }
        finally
        {
            Resources.RegisteredSprites[rewindPath]=rewindOriginal;
            release.Invoke(rewindRetry,null);
        }
        Console.WriteLine("PASS nonhero hot-reimport clock rewind: original and mirrored GPU frames, partial-family atomic recovery");
        // Controller-only live art reimport: the 18 published Monsters
        // source cells stay IDENTICAL, but a new Animator Controller must
        // invalidate all previously rendered GPU pose caches (both faces).
        // No actor pose may keep an obsolete controller indefinitely.
        const string graphPath="Frontier/Actors/Native/Actor_Monsters";
        var graphOld=(RuntimeAnimatorController)Resources.Registered[graphPath];
        var controllerProbe=new FrontierRuntime();
        var graphTexture=(Texture2D)Resources.Registered[livePath];
        var controllerField=typeof(FrontierRuntime).GetField(
            "actorCaptureAnimator",BindingFlags.Instance|
            BindingFlags.NonPublic);
        var graphAnimator=(Animator)controllerField.GetValue(controllerProbe);
        try
        {
            Time.frameCount=4700;
            Require((bool)method.Invoke(controllerProbe,new object[]{
                graphTexture,6,3,2,new Point(200,235),96f,
                Color.white,false,0f})&&
                (bool)method.Invoke(controllerProbe,new object[]{
                graphTexture,6,3,2,new Point(200,235),96f,
                Color.white,true,0f}),
                "Native full-body frames failed initial Controller import");
            graphAnimator=(Animator)controllerField.GetValue(controllerProbe);
            Require(graphAnimator!=null&&
                ReferenceEquals(graphAnimator.runtimeAnimatorController,graphOld),
                "First official Animator Controller not sampled");
            var firstNormal=GUI.DrawnTextures[GUI.DrawnTextures.Count-2];
            var firstFlip=GUI.DrawnTextures[GUI.DrawnTextures.Count-1];
            int initialCaptures=Camera.RenderedTargets.Count;
            var nextGraph=new RuntimeAnimatorController{
                className="Actor_Monsters"};
            Resources.Registered[graphPath]=nextGraph;
            Time.frameCount=4879;
            Require((bool)method.Invoke(controllerProbe,new object[]{
                graphTexture,6,3,2,new Point(200,235),96f,
                Color.white,false,0f})&&
                Camera.RenderedTargets.Count==initialCaptures&&
                ReferenceEquals(graphAnimator.runtimeAnimatorController,graphOld),
                "Loading Actor Controller every repaint disturbed stable art");
            // Old Controller had rejected its pose state; the newly
            // published correct Controller must clear THIS family-only
            // quarantine instead of retaining a stale 120-frame lockout.
            var rejectionMap=(System.Collections.IDictionary)
                typeof(FrontierRuntime).GetField(
                    "actorNativeRejectedPoseUntil",BindingFlags.Instance|
                    BindingFlags.NonPublic).GetValue(controllerProbe);
            rejectionMap["Monsters:0"]=5100;
            rejectionMap["WalkBossA:0"]=5100;
            Time.frameCount=4880;
            Require((bool)method.Invoke(controllerProbe,new object[]{
                graphTexture,6,3,2,new Point(200,235),96f,
                Color.white,false,0f})&&
                ReferenceEquals(graphAnimator.runtimeAnimatorController,nextGraph)&&
                ReferenceEquals(GUI.DrawnTextures[
                    GUI.DrawnTextures.Count-1],firstNormal)&&
                Camera.RenderedTargets.Count==initialCaptures+1&&
                !rejectionMap.Contains("Monsters:0")&&
                rejectionMap.Contains("WalkBossA:0"),
                "Corrected native Controller failed to unblock only its own rejected pose");
            // Normal and mirror MUST remain independent references. A
            // simultaneous old pending command cannot be overwritten.
            Require((bool)method.Invoke(controllerProbe,new object[]{
                graphTexture,6,3,2,new Point(200,235),96f,
                Color.white,true,0f})&&
                ReferenceEquals(GUI.DrawnTextures[
                    GUI.DrawnTextures.Count-1],firstFlip)&&
                Camera.RenderedTargets.Count==initialCaptures+2,
                "Imported Controller failed to refresh mirrored actor separately");
            Time.frameCount=4881;
            Require((bool)method.Invoke(controllerProbe,new object[]{
                graphTexture,6,3,2,new Point(200,235),96f,
                Color.white,false,0f})&&
                Camera.RenderedTargets.Count==initialCaptures+2,
                "Controller-only swap caused redundant pose recaptures");
            Resources.Registered.Remove(graphPath);
            Time.frameCount=5061;
            Require((bool)method.Invoke(controllerProbe,new object[]{
                graphTexture,6,3,2,new Point(200,235),96f,
                Color.white,false,0f})&&
                ReferenceEquals(graphAnimator.runtimeAnimatorController,nextGraph)&&
                Camera.RenderedTargets.Count==initialCaptures+2,
                "Temporary missing Controller erased last accepted whole-body pose");
        }
        finally
        {
            Resources.Registered[graphPath]=graphOld;
            release.Invoke(controllerProbe,null);
        }
        Console.WriteLine("PASS native Actor AnimatorController hot-swap: source unchanged, normal/mirror GPU recapture, missing-import hold and bounded refresh");
        // Verify the shared production Controller-swap path for a tall
        // Boss, the original richly illustrated civilian sheet and a
        // companion. This must not be a Monsters-only special case.
        foreach(string family in new[]{"Bosses","Npcs","CompanionMira"})
        {
            int columns=family=="CompanionMira"?3:4;
            int rows=family=="Bosses"?8:4;
            var art=(Texture2D)Resources.Registered[
                "Frontier/Art/"+family];
            string path="Frontier/Actors/Native/Actor_"+family;
            var oldController=(RuntimeAnimatorController)
                Resources.Registered[path];
            var probe=new FrontierRuntime();
            try
            {
                Time.frameCount=8100;
                Require((bool)method.Invoke(probe,new object[]{
                    art,columns,rows,1,new Point(250,240),96f,
                    Color.white,false,0f}),
                    "Native nonhero Controller family could not render "+family);
                var renderer=(Animator)controllerField.GetValue(probe);
                Require(ReferenceEquals(
                    renderer.runtimeAnimatorController,oldController),
                    "Wrong first accepted Animator for "+family);
                int count=Camera.RenderedTargets.Count;
                var replacement=new RuntimeAnimatorController {
                    className="Actor_"+family};
                Resources.Registered[path]=replacement;
                Time.frameCount=8280;
                Require((bool)method.Invoke(probe,new object[]{
                    art,columns,rows,1,new Point(250,240),96f,
                    Color.white,false,0f})&&
                    Camera.RenderedTargets.Count==count+1&&
                    ReferenceEquals(renderer.runtimeAnimatorController,replacement),
                    "Boss/NPC/companion Controller update kept obsolete GPU pose: "+family);
            }
            finally
            {
                Resources.Registered[path]=oldController;
                release.Invoke(probe,null);
            }
        }
        Console.WriteLine("PASS all tall Boss, detailed NPC and companion Animator Controllers hot-reimport with unchanged source paint");
        // DEVICE LOSS AFTER A DIFFERENT ACTOR: one mutable offscreen
        // SpriteRenderer draws many character identities. The old cache
        // recovery reused whichever Sprite was last seen instead of binding
        // the lost target's exact authored frame. Exercise both a real
        // imported Monsters Animator and the source-only fallback.
        var restoration=new FrontierRuntime();
        var nativeAtlas=(Texture2D)Resources.Registered[
            "Frontier/Art/Monsters"];
        var nativeCells=Resources.RegisteredSprites[
            "Frontier/Art/Monsters"];
        var restoredRenderer=typeof(FrontierRuntime).GetField(
            "actorCaptureRenderer",BindingFlags.Instance|
            BindingFlags.NonPublic);
        var restoredAnimator=typeof(FrontierRuntime).GetField(
            "actorCaptureAnimator",BindingFlags.Instance|
            BindingFlags.NonPublic);
        Time.frameCount=1760;
        Require((bool)method.Invoke(restoration,new object[]{
            nativeAtlas,6,3,2,new Point(200,220),96f,
            Color.white,false,0f}),
            "Native original monster capture setup failed");
        var originalTarget=GUI.DrawnTextures[
            GUI.DrawnTextures.Count-1] as RenderTexture;
        Time.frameCount=1761;
        Require((bool)method.Invoke(restoration,new object[]{
            nativeAtlas,6,3,9,new Point(200,220),96f,
            Color.white,false,0f}),
            "Second original monster pose setup failed");
        Require(ReferenceEquals((restoredRenderer.GetValue(restoration) as
            SpriteRenderer).sprite,nativeCells[9]),
            "Offscreen native renderer never switched to newer monster pose");
        originalTarget.Release();
        Time.frameCount=1762;
        int beforeRebuild=Camera.RenderedTargets.Count;
        Require((bool)method.Invoke(restoration,new object[]{
            nativeAtlas,6,3,2,new Point(200,220),96f,
            Color.white,false,0f})&&
            Camera.RenderedTargets.Count==beforeRebuild+1&&
            ReferenceEquals(GUI.DrawnTextures[
                GUI.DrawnTextures.Count-1],originalTarget)&&
            ReferenceEquals((restoredRenderer.GetValue(restoration) as
                SpriteRenderer).sprite,nativeCells[2])&&
            (restoredAnimator.GetValue(restoration) as Animator).enabled,
            "GPU restoration captured another native monster pose");
        release.Invoke(restoration,null);

        var recoveryFallback=new FrontierRuntime();
        var unknown=new Texture2D(512,512,TextureFormat.RGBA32,false);
        Time.frameCount=1770;
        Require((bool)method.Invoke(recoveryFallback,new object[]{
            unknown,4,4,2,new Point(200,220),96f,
            Color.white,false,0f}),
            "Source fallback capture setup failed");
        var fallbackTarget=GUI.DrawnTextures[
            GUI.DrawnTextures.Count-1] as RenderTexture;
        var sourceCache=(System.Collections.IDictionary)typeof(FrontierRuntime)
            .GetField("actorFrameSprites",BindingFlags.Instance|
                BindingFlags.NonPublic).GetValue(recoveryFallback);
        var requiredSprite=sourceCache[unknown.GetInstanceID()+":4:4:2"]
            as Sprite;
        Time.frameCount=1771;
        Require((bool)method.Invoke(recoveryFallback,new object[]{
            unknown,4,4,3,new Point(200,220),96f,
            Color.white,false,0f}),
            "Second source fallback pose setup failed");
        fallbackTarget.Release();
        Time.frameCount=1772;
        Require((bool)method.Invoke(recoveryFallback,new object[]{
            unknown,4,4,2,new Point(200,220),96f,
            Color.white,false,0f})&&
            ReferenceEquals((restoredRenderer.GetValue(recoveryFallback) as
                SpriteRenderer).sprite,requiredSprite)&&
            !(restoredAnimator.GetValue(recoveryFallback) as Animator).enabled,
            "GPU recovery kept stale foreign Sprite instead of exact source cell");
        release.Invoke(recoveryFallback,null);
        Console.WriteLine("PASS native and fallback GPU loss recovery rebinds exact whole-body source pose");

        // A partial SpriteImporter pass must not trigger one expensive
        // LoadAll scan per NPC pose, but once all twelve accepted cells are
        // present the native source can recover without restarting Frontier.
        const string reimportPath="Frontier/Art/WalkMira";
        const string reimportController=
            "Frontier/Actors/Native/Actor_WalkMira";
        var reimportFrames=Resources.RegisteredSprites[reimportPath];
        var reimportGraph=Resources.Registered[reimportController];
        var walkAtlas=(Texture2D)Resources.Registered[reimportPath];
        var loading=new FrontierRuntime();
        var importedCache=(System.Collections.IDictionary)
            typeof(FrontierRuntime).GetField("actorFrameSprites",
                BindingFlags.Instance|BindingFlags.NonPublic).
                GetValue(loading);
        try
        {
            Resources.RegisteredSprites[reimportPath]=
                new Sprite[]{reimportFrames[0]};
            Resources.Registered.Remove(reimportController);
            Resources.LoadAllCounts[reimportPath]=0;
            for(int i=0;i<10;i++)
            {
                Time.frameCount=3500+i;
                Require((bool)method.Invoke(loading,new object[]{
                    walkAtlas,4,3,i,new Point(110,150),96f,
                    Color.white,false,0f}),
                    "Incomplete companion walk import disappeared mid-stride");
                var owned=importedCache[walkAtlas.GetInstanceID()+
                    ":4:3:"+i] as Sprite;
                Require(owned!=null&&owned.texture==walkAtlas&&
                    !ReferenceEquals(owned,reimportFrames[i]),
                    "Partial companion art mixed imported and fallback cells");
            }
            Require(Resources.LoadAllCounts[reimportPath]==1,
                "One partial companion reimport rescanned all Sprites per step");
            Resources.RegisteredSprites[reimportPath]=reimportFrames;
            Resources.Registered[reimportController]=reimportGraph;
            Time.frameCount=3621;
            Require((bool)method.Invoke(loading,new object[]{
                walkAtlas,4,3,11,new Point(110,150),96f,
                Color.white,false,0f}),
                "Completed companion animation did not recover after cooldown");
            Require(ReferenceEquals(importedCache[walkAtlas.GetInstanceID()+
                ":4:3:11"],reimportFrames[11]),
                "Complete imported companion stride still used temporary art");
            Require(Resources.LoadAllCounts[reimportPath]>=2,
                "SpriteImporter retry never retried after valid resource release");
            // A previously visited frame was cached from an incomplete
            // import, unlike the newly visited index 11. On recovery that
            // existing full-body pose should adopt the approved Sprite and
            // resample the imported native Controller automatically.
            string oldKey=walkAtlas.GetInstanceID()+":4:3:2";
            var oldWrapper=importedCache[oldKey] as Sprite;
            Require(oldWrapper!=null&&!oldWrapper.destroyed&&
                !ReferenceEquals(oldWrapper,reimportFrames[2]),
                "Partial import setup did not cache authored fallback stride");
            Time.frameCount=3622;
            var renderer=typeof(FrontierRuntime).GetField(
                "actorCaptureRenderer",BindingFlags.Instance|
                BindingFlags.NonPublic);
            var animator=typeof(FrontierRuntime).GetField(
                "actorCaptureAnimator",BindingFlags.Instance|
                BindingFlags.NonPublic);
            Require((bool)method.Invoke(loading,new object[]{
                walkAtlas,4,3,2,new Point(110,150),96f,
                Color.white,false,0f})&&
                ReferenceEquals(importedCache[oldKey],reimportFrames[2])&&
                oldWrapper.destroyed&&
                ReferenceEquals((renderer.GetValue(loading) as
                    SpriteRenderer).sprite,reimportFrames[2])&&
                (animator.GetValue(loading) as Animator).enabled,
                "Previously visited companion walk frame failed to upgrade to native imported art after recovery");
        }
        finally
        {
            Resources.RegisteredSprites[reimportPath]=reimportFrames;
            Resources.Registered[reimportController]=reimportGraph;
            release.Invoke(loading,null);
        }
        // Adversarial partial Unity SpriteImporter refresh: corrupt ONE
        // cell's pivot and keep the other 17 valid. Reusing imported cells
        // for part of the family would leak a raised-foot enemy silhouette
        // when animation reaches the malformed cell. Fall back atomically
        // to the exact source pixels and NEVER own/destroy the source art.
        string guardedFamily="Frontier/Art/Monsters";
        var acceptedFrames=Resources.RegisteredSprites[guardedFamily];
        var partial=(Sprite[])acceptedFrames.Clone();
        var damaged=acceptedFrames[4];
        partial[4]=Sprite.Create(damaged.texture,damaged.rect,
            new Vector2(.5f,.25f),damaged.pixelsPerUnit,0,
            SpriteMeshType.FullRect);
        Resources.RegisteredSprites[guardedFamily]=partial;
        try
        {
            var corrupt=new FrontierRuntime();
            var originalMonsters=(Texture2D)Resources.Registered[guardedFamily];
            var sourceCells=(System.Collections.IDictionary)
                typeof(FrontierRuntime).GetField("actorFrameSprites",
                    BindingFlags.Instance|BindingFlags.NonPublic).
                    GetValue(corrupt);
            Time.frameCount=1790;
            Require((bool)method.Invoke(corrupt,new object[]{
                originalMonsters,6,3,2,new Point(180,210),96f,
                Color.white,false,0f}),
                "Partial imported atlas made the real original monster invisible");
            string goodKey=originalMonsters.GetInstanceID()+":6:3:2";
            var safeCell=sourceCells[goodKey] as Sprite;
            Require(safeCell!=null&&!ReferenceEquals(safeCell,acceptedFrames[2])&&
                ReferenceEquals(safeCell.texture,originalMonsters),
                "One wrong importer foot pivot was not rejected for WHOLE atlas");
            Time.frameCount=1791;
            Require((bool)method.Invoke(corrupt,new object[]{
                originalMonsters,6,3,4,new Point(180,210),96f,
                Color.white,false,0f}),
                "Actual malformed hit-frame must still show exact source cell");
            var rendered=typeof(FrontierRuntime).GetField(
                "actorCaptureRenderer",BindingFlags.Instance|
                BindingFlags.NonPublic).GetValue(corrupt) as SpriteRenderer;
            var animator=typeof(FrontierRuntime).GetField(
                "actorCaptureAnimator",BindingFlags.Instance|
                BindingFlags.NonPublic).GetValue(corrupt) as Animator;
            var fallbackCell=sourceCells[
                originalMonsters.GetInstanceID()+":6:3:4"] as Sprite;
            Require(fallbackCell!=null&&rendered!=null&&
                ReferenceEquals(rendered.sprite,fallbackCell)&&
                animator!=null&&!animator.enabled,
                "Malformed imported pose bypassed exact source/full-body fallback");
            release.Invoke(corrupt,null);
            Require(safeCell.destroyed&&fallbackCell.destroyed&&
                !acceptedFrames[2].destroyed&&!damaged.destroyed,
                "Owned wrapper cleanup incorrectly destroyed accepted Unity art");
        }
        finally { Resources.RegisteredSprites[guardedFamily]=acceptedFrames; }
        // A first-frame missing Controller must not permanently lock a
        // cached whole-body sprite out of the real Animator after import.
        var late=new FrontierRuntime();
        const string lateFamily="Monsters";
        string latePath="Frontier/Actors/Native/Actor_"+lateFamily;
        var lateAtlas=(Texture2D)Resources.Registered["Frontier/Art/"+lateFamily];
        var importedController=(RuntimeAnimatorController)Resources.Registered[latePath];
        Resources.Registered.Remove(latePath);
        Time.frameCount=1800;
        bool drawnFallback=(bool)method.Invoke(late,new object[]{
            lateAtlas,6,3,2,new Point(100,140),96f,
            new Color(1,1,1,1),false,0f});
        var lateField=typeof(FrontierRuntime).GetField("actorCaptureAnimator",
            BindingFlags.Instance|BindingFlags.NonPublic);
        var lateAnim=lateField.GetValue(late) as Animator;
        Require(drawnFallback&&lateAnim!=null&&!lateAnim.enabled,
            "first frame without controller must still show exact original monster");
        Texture fallbackTexture=GUI.DrawnTextures[GUI.DrawnTextures.Count-1];
        Resources.Registered[latePath]=importedController;
        int beforeRecovery=Camera.RenderedTargets.Count;
        Time.frameCount=1801;
        bool cooled=(bool)method.Invoke(late,new object[]{
            lateAtlas,6,3,2,new Point(100,140),96f,
            new Color(1,1,1,1),false,0f});
        Require(cooled&&!lateAnim.enabled&&
            Camera.RenderedTargets.Count==beforeRecovery,
            "cached fallback must remain stable during controller import cooldown");
        Time.frameCount=1920;
        bool nativeRecovered=(bool)method.Invoke(late,new object[]{
            lateAtlas,6,3,2,new Point(100,140),96f,
            new Color(1,1,1,1),false,0f});
        Require(nativeRecovered&&lateAnim.enabled&&
            Camera.RenderedTargets.Count==beforeRecovery+1&&
            ReferenceEquals(fallbackTexture,
                GUI.DrawnTextures[GUI.DrawnTextures.Count-1]),
            "late imported accepted-art controller never refreshed cached GPU pose");
        release.Invoke(late,null);
        // A missing native Controller at a late game frame must not keep the
        // exact-source fallback pinned after a Play session clock rewind.
        var rewindLate=new FrontierRuntime();
        Resources.Registered.Remove(latePath);
        Time.frameCount=10000;
        Require((bool)method.Invoke(rewindLate,new object[]{
            lateAtlas,6,3,1,new Point(100,140),96f,
            Color.white,false,0f}),"rewind precondition capture failed");
        var rewindLateAnim=lateField.GetValue(rewindLate) as Animator;
        Require(rewindLateAnim!=null&&!rewindLateAnim.enabled,
            "missing-Controller precondition did not use safe source");
        Resources.Registered[latePath]=importedController;
        int beforeRewindController=Camera.RenderedTargets.Count;
        Time.frameCount=40;
        Require((bool)method.Invoke(rewindLate,new object[]{
            lateAtlas,6,3,1,new Point(100,140),96f,
            Color.white,false,0f})&&rewindLateAnim.enabled&&
            Camera.RenderedTargets.Count==beforeRewindController+1,
            "clock rewind kept a recovered nonhero Animator in fallback");
        // Simulate an isolated capture failure's retry state. A fresh epoch
        // must be able to reconstruct real GPU pixels instead of skipping it.
        var captureFailed=typeof(FrontierRuntime).GetField(
            "actorCaptureBroken",BindingFlags.Instance|BindingFlags.NonPublic);
        var captureRetry=typeof(FrontierRuntime).GetField(
            "actorCaptureRetryAt",BindingFlags.Instance|BindingFlags.NonPublic);
        Require(captureFailed!=null&&captureRetry!=null,
            "capture failure cooldown state is missing");
        captureFailed.SetValue(rewindLate,true);
        captureRetry.SetValue(rewindLate,10120);
        int beforeCaptureRewind=Camera.RenderedTargets.Count;
        Time.frameCount=41;
        Require((bool)method.Invoke(rewindLate,new object[]{
            lateAtlas,6,3,2,new Point(100,140),96f,
            Color.white,false,0f})&&
            !(bool)captureFailed.GetValue(rewindLate)&&
            Camera.RenderedTargets.Count==beforeCaptureRewind+1,
            "clock rewind kept healed whole-body GPU capture disabled");
        release.Invoke(rewindLate,null);
        Console.WriteLine("PASS nonhero Controller and GPU-capture retry windows reset on frame-count rewind");
        // A corrupted native clip must NEVER be rendered as a stitched,
        // foreign-actor or wrong-action body: use exact source Sprite instead.
        var brokenActor=new FrontierRuntime();
        var offender=(Texture2D)Resources.Registered[
            "Frontier/Art/MossboundRaider"];
        Animator.InjectWrongSpriteOnce=true;
        Time.frameCount=1400;
        bool fallback=(bool)method.Invoke(brokenActor,new object[]{
            offender,4,6,9,new Point(200,220),96f,
            new Color(1,1,1,1),false,0f});
        var spriteLookup=typeof(FrontierRuntime).GetField(
            "actorCaptureRenderer",BindingFlags.Instance|BindingFlags.NonPublic);
        var animatorLookup=typeof(FrontierRuntime).GetField(
            "actorCaptureAnimator",BindingFlags.Instance|BindingFlags.NonPublic);
        var safe=(SpriteRenderer)spriteLookup.GetValue(brokenActor);
        var stopped=(Animator)animatorLookup.GetValue(brokenActor);
        Require(fallback&&safe.sprite!=null&&safe.sprite.texture==offender&&
            !stopped.enabled,
            "invalid native NPC/monster clip escaped exact full-body fallback");
        var raiderGraphKey="Frontier/Actors/Native/Actor_MossboundRaider";
        var rejectedGraph=(RuntimeAnimatorController)
            Resources.Registered[raiderGraphKey];
        try
        {
            // A DIFFERENT corrected Controller is imported just one frame
            // after a stale/foreign Sprite clip was caught. The canonical
            // 128px whole-body GameCore cell must resume native rendering
            // now, not wait the old 120-frame rejection cooldown.
            var correctedGraph=new RuntimeAnimatorController{
                className="Actor_MossboundRaider"};
            Resources.Registered[raiderGraphKey]=correctedGraph;
            int beforeCorrectedClip=Camera.RenderedTargets.Count;
            Time.frameCount=1401;
            Require((bool)method.Invoke(brokenActor,new object[]{
                offender,4,6,9,new Point(200,220),96f,
                Color.white,false,0f})&&stopped.enabled&&
                ReferenceEquals(stopped.runtimeAnimatorController,
                    correctedGraph)&&
                Camera.RenderedTargets.Count==beforeCorrectedClip+1,
                "Corrected native pose remained quarantined after actual Controller replacement");
        }
        finally {Resources.Registered[raiderGraphKey]=rejectedGraph;}
        release.Invoke(brokenActor,null);
        // A wrong imported pose is quarantined only for 120 frames of the
        // SAME time epoch. The unmodified correct original Controller must
        // recover after a frame-count rewind without a fake asset replacement.
        var rewindRejected=new FrontierRuntime();
        Animator.InjectWrongSpriteOnce=true;
        Time.frameCount=11000;
        Require((bool)method.Invoke(rewindRejected,new object[]{
            offender,4,6,9,new Point(200,220),96f,
            Color.white,false,0f}),"rejected-pose rewind setup failed");
        var rewindRejectedAnim=animatorLookup.GetValue(rewindRejected) as Animator;
        Require(rewindRejectedAnim!=null&&!rewindRejectedAnim.enabled,
            "wrong Clip must first enter exact whole-Sprite fallback");
        int beforePoseRewind=Camera.RenderedTargets.Count;
        Time.frameCount=52;
        Require((bool)method.Invoke(rewindRejected,new object[]{
            offender,4,6,9,new Point(200,220),96f,
            Color.white,false,0f})&&rewindRejectedAnim.enabled&&
            Camera.RenderedTargets.Count==beforePoseRewind+1,
            "stale rejected-pose quarantine survived frame-count rewind");
        release.Invoke(rewindRejected,null);
        Console.WriteLine("PASS rejected nonhero Animator poses recover after a new frame-count epoch");
        // Stress actual production RenderTexture allocation with authored-
        // scale 512px actor frames. The former count-only cap held 192
        // targets (~288 MiB estimated GPU color/depth for 512px sources).
        // Verify both the byte budget and deferred GUI lifetime guarantee.
        var limited=new FrontierRuntime();
        var bytesField=typeof(FrontierRuntime).GetField(
            "actorCachedBytes",BindingFlags.Instance|BindingFlags.NonPublic);
        Require(bytesField!=null,"nonhero GPU byte-budget counter missing");
        long budget=64L*1024L*1024L;
        var spriteMap=(System.Collections.IDictionary)typeof(FrontierRuntime).
            GetField("actorFrameSprites",BindingFlags.Instance|
                BindingFlags.NonPublic).GetValue(limited);
        var renderMap=(System.Collections.IDictionary)typeof(FrontierRuntime).
            GetField("actorCapturedFrames",BindingFlags.Instance|
                BindingFlags.NonPublic).GetValue(limited);
        Sprite earliestSprite=null;
        for(int frame=0;frame<70;frame++)
        {
            Time.frameCount=2400+frame;
            var large=new Texture2D(2048,2048,TextureFormat.RGBA32,false);
            bool ok=(bool)method.Invoke(limited,new object[]{
                large,4,4,0,new Point(200,220),96f,
                new Color(1,1,1,1),false,0f});
            Require(ok,"native large frame dropped instead of LRU eviction");
            Require((long)bytesField.GetValue(limited)<=budget,
                "nonhero GPU cache exceeds 64MiB after an ordinary repaint");
            string currentKey=large.GetInstanceID()+":4:4:0";
            if(frame==0)earliestSprite=spriteMap[currentKey] as Sprite;
            Require(spriteMap.Count<=renderMap.Count&&
                spriteMap.Count<MaxExpectedSpriteWrappersForBudget(),
                "GPU eviction orphaned runtime Sprite.Create wrappers across room loads");
            foreach(System.Collections.DictionaryEntry item in spriteMap)
            {
                string key=(string)item.Key;
                Require(renderMap.Contains(key+":normal")||
                    renderMap.Contains(key+":flipped"),
                    "Owned Sprite survived eviction of both orientation targets");
            }
        }
        Require(earliestSprite!=null&&earliestSprite.destroyed,
            "First room's owned Sprite wrapper survived 70 newer Boss frames");
        var firstBatch=Camera.RenderedTargets[Camera.RenderedTargets.Count-70];
        Require(firstBatch.releaseCount>=1,
            "old large GPU captures were not evicted by byte pressure");
        // The unflipped and flipped versions of ONE painted frame own
        // different GPU targets, but must share a single live Sprite.
        // Reclamation may never destroy that Sprite when only one of the
        // orientation targets has been retired.
        var mirroredOwner=new FrontierRuntime();
        var dual=new Texture2D(2048,2048,TextureFormat.RGBA32,false);
        Time.frameCount=2900;
        Require((bool)method.Invoke(mirroredOwner,new object[]{
            dual,4,4,0,new Point(200,220),96f,Color.white,false,0f})&&
            (bool)method.Invoke(mirroredOwner,new object[]{
            dual,4,4,0,new Point(200,220),96f,Color.white,true,0f}),
            "Both orientations of one native actor must be renderable");
        var dualSprites=(System.Collections.IDictionary)typeof(FrontierRuntime).
            GetField("actorFrameSprites",BindingFlags.Instance|
                BindingFlags.NonPublic).GetValue(mirroredOwner);
        var dualTargets=(System.Collections.IDictionary)typeof(FrontierRuntime).
            GetField("actorCapturedFrames",BindingFlags.Instance|
                BindingFlags.NonPublic).GetValue(mirroredOwner);
        string dualKey=dual.GetInstanceID()+":4:4:0";
        Sprite shared=dualSprites[dualKey] as Sprite;
        Require(shared!=null&&dualTargets.Contains(dualKey+":normal")&&
            dualTargets.Contains(dualKey+":flipped"),
            "Mirrored source art must use one owned full-body Sprite");
        bool sawOneOrientation=false;
        for(int i=0;i<72;i++)
        {
            Time.frameCount=2901+i;
            var newSheet=new Texture2D(2048,2048,
                TextureFormat.RGBA32,false);
            Require((bool)method.Invoke(mirroredOwner,new object[]{
                newSheet,4,4,0,new Point(200,220),96f,
                Color.white,false,0f}),
                "Mirrored artist source eviction stress dropped a real actor");
            int surviving=(dualTargets.Contains(dualKey+":normal")?1:0)+
                (dualTargets.Contains(dualKey+":flipped")?1:0);
            if(surviving==1)
            {
                sawOneOrientation=true;
                Require(dualSprites.Contains(dualKey)&&!shared.destroyed,
                    "Evicted one mirror and prematurely destroyed shared Sprite");
            }
            if(surviving==0)
                Require(!dualSprites.Contains(dualKey)&&shared.destroyed,
                    "Last orientation lost GPU target but leaked its Sprite");
        }
        Require(sawOneOrientation&&shared.destroyed,
            "Never exercised both sides of the last-variant Sprite retirement");
        release.Invoke(mirroredOwner,null);
        var burst=new FrontierRuntime();
        Time.frameCount=3300;
        int burstStart=Camera.RenderedTargets.Count;
        for(int i=0;i<47;i++)
        {
            var large=new Texture2D(2048,2048,TextureFormat.RGBA32,false);
            Require((bool)method.Invoke(burst,new object[]{
                large,4,4,0,new Point(200,220),96f,
                new Color(1,1,1,1),false,0f}),
                "same-frame actor burst must not fall back or clip bodies");
        }
        for(int i=burstStart;i<Camera.RenderedTargets.Count;i++)
            Require(Camera.RenderedTargets[i].releaseCount==0,
                "evicted a target still referenced by this Repaint");
        Time.frameCount=3301;
        var oneMore=new Texture2D(2048,2048,TextureFormat.RGBA32,false);
        Require((bool)method.Invoke(burst,new object[]{
            oneMore,4,4,0,new Point(200,220),96f,
            new Color(1,1,1,1),false,0f}),
            "GPU actor cannot recover from same-frame burst");
        Require((long)bytesField.GetValue(burst)<=budget,
            "GPU byte pressure was never reclaimed on next repaint");
        release.Invoke(burst,null);
        release.Invoke(limited,null);
        Require((long)bytesField.GetValue(limited)==0L &&
                (long)bytesField.GetValue(burst)==0L,
                "actor capture cleanup did not zero GPU cost accounting");
        release.Invoke(runtime, null);
        Require(bossCapture.releaseCount >= 1 &&
            wideCapture.releaseCount >= 1,
            "resolution-aware nonhero GPU targets leaked at shutdown");
        Require(((RenderTexture)first).releaseCount >= 2 &&
            ((RenderTexture)second).releaseCount >= 1 &&
            ((RenderTexture)mirrored).releaseCount >= 1,
            "actor GPU targets leaked on runtime destruction");
        Console.WriteLine("PASS 17 native actor families (13 original accepted, including all companions): 3/4/6-column GameCore samples, late Controller recovery, GPU safety");
        Console.WriteLine("NATIVE_UNVERIFIED: GPU pixels, Unity GameView and Animator");
    }
}
