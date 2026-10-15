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
        release.Invoke(runtime, null);
        Require(bossCapture.releaseCount >= 1 &&
            wideCapture.releaseCount >= 1,
            "resolution-aware nonhero GPU targets leaked at shutdown");
        Require(((RenderTexture)first).releaseCount >= 2 &&
            ((RenderTexture)second).releaseCount >= 1 &&
            ((RenderTexture)mirrored).releaseCount >= 1,
            "actor GPU targets leaked on runtime destruction");
        Console.WriteLine("PASS nonhero capture: Boss/NPC/monster source pixels, atlas isolation, GPU failure cooldown and automatic recovery, target cleanup");
        Console.WriteLine("NATIVE_UNVERIFIED: GPU pixels, Unity GameView and Animator");
    }
}
