using System;
using System.IO;
using System.Reflection;
using Outlaws.TransformStunt;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Opt-in graphical smoke test; never starts on ordinary editor launch.</summary>
[InitializeOnLoad]
public static class TransformStuntPreview
{
    private const string Key = "Stunt.Preview.Active";
    private static int frames;
    private static int pendingScene = -1;
    static TransformStuntPreview() { EditorApplication.update += Tick; }
    public static void Capture()
    {
        SessionState.SetBool(Key, true);
        SessionState.SetInt("Stunt.Preview.Scene", 0);
        EditorSceneManager.OpenScene("Assets/Scenes/Stunt_Tutorial.unity");
        EditorApplication.EnterPlaymode();
    }
    private static void Tick()
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        if (++frames < 60) return;
        frames = 0;
        try
        {
            if (pendingScene >= 0)
            {
                int next = pendingScene; pendingScene = -1; Time.timeScale = 1;
                if (next < 4) StuntGame.Load(next);
                else { SessionState.SetBool(Key, false); EditorApplication.Exit(0); }
                return;
            }
            var game = UnityEngine.Object.FindFirstObjectByType<StuntGame>();
            if (game == null || game.Run == null) return;
            var run = game.Run; run.Started = true; run.Paused = false;
            float target = game.environment == 0 ? 315 : game.environment == 1 ? 485 : 290;
            for (int step = 0; step < 18000 && run.Position.z < target; step++)
            {
                TransformStuntValidation.Pilot(run, out int size, out float steer);
                if (run.SizeIndex > size) run.Transform(true, false);
                if (run.SizeIndex < size) run.Transform(false, true);
                run.Advance(1f / 120, steer);
                if (run.Crashes > 0) throw new InvalidOperationException("Preview pilot: " + run.Feedback);
            }
            run.Paused = true;
            typeof(StuntGame).GetMethod("UpdateVisuals", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(game, new object[] { true });
            Directory.CreateDirectory("Builds/Previews");
            var rt = new RenderTexture(1280, 720, 24);
            var prior = RenderTexture.active;
            game.view.targetTexture = rt; game.view.Render(); RenderTexture.active = rt;
            var texture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); texture.Apply();
            File.WriteAllBytes("Builds/Previews/" + game.environment + ".png", texture.EncodeToPNG());
            game.view.targetTexture = null; RenderTexture.active = prior;
            UnityEngine.Object.Destroy(texture); rt.Release(); UnityEngine.Object.Destroy(rt);
            Debug.Log("STUNT_PREVIEW_OK " + game.environment);
            run.Paused = false; Time.timeScale = 0;
            ScreenCapture.CaptureScreenshot("Builds/Previews/" + game.environment + "-hud.png");
            pendingScene = game.environment + 1;
        }
        catch (Exception e)
        { Debug.LogException(e); SessionState.SetBool(Key, false); EditorApplication.Exit(1); }
    }
}
