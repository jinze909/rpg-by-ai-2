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
        release.Invoke(runtime, null);
        Require(((RenderTexture)first).releaseCount >= 2 &&
            ((RenderTexture)second).releaseCount >= 1 &&
            ((RenderTexture)mirrored).releaseCount >= 1,
            "actor GPU targets leaked on runtime destruction");
        Console.WriteLine("PASS nonhero native capture: unique pose/flip textures, reuse, device loss, cleanup");
        Console.WriteLine("NATIVE_UNVERIFIED: GPU pixels, Unity GameView and Animator");
    }
}
