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
            }
            release.Invoke(character,null);
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
            }
            release.Invoke(actualOriginal,null);
        }
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
        release.Invoke(brokenActor,null);
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
