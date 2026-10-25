using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

namespace Sleight.RabbitTrick.EditorTools
{
    // Real player-loop performances. The same hand controller runs against normal gameplay rules.
    [InitializeOnLoad]
    public static class GrandIllusionRuntimeCheck
    {
        const string Flag = "Sleight.GrandIllusionRuntimeCheck";
        static Keyboard keyboard;
        static RabbitTrickGame game;
        static TrickDemo hands;
        static RenderTexture target;
        static HashSet<string> photos;
        static StringBuilder report;
        static int level, lastFrame, photoFrame;
        static string pending;
        static bool started;
        static double began;
        static GrandIllusionRuntimeCheck() { EditorApplication.update += Poll; EditorApplication.playModeStateChanged += State; }
        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Game.unity");
            SessionState.SetBool(Flag, true); SessionState.SetBool(Flag + ".Pass", false); EditorApplication.EnterPlaymode();
        }
        static void State(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Flag, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode) Initialize();
            if (state == PlayModeStateChange.EnteredEditMode)
            { SessionState.SetBool(Flag, false); EditorApplication.Exit(SessionState.GetBool(Flag + ".Pass", false) ? 0 : 1); }
        }
        static void Initialize()
        {
            began = EditorApplication.timeSinceStartup; started = false; level = 5; lastFrame = -1;
            photos = new HashSet<string>(); report = new StringBuilder(); pending = null;
            InputSystem.settings = UnityEngine.Object.Instantiate(InputSystem.settings);
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            foreach (var device in InputSystem.devices) if (device is Keyboard) InputSystem.DisableDevice(device);
            keyboard = InputSystem.AddDevice<Keyboard>();
            InputSystem.onBeforeUpdate -= Supply; InputSystem.onBeforeUpdate += Supply;
            InputSystem.onAfterUpdate -= Select; InputSystem.onAfterUpdate += Select;
            Canvas.willRenderCanvases -= ClearResult; Canvas.willRenderCanvases += ClearResult;
            Application.runInBackground = true;
            Application.logMessageReceived -= CheckLog; Application.logMessageReceived += CheckLog;
        }
        static void Supply() { if (keyboard != null && keyboard.added) InputSystem.QueueStateEvent(keyboard, new KeyboardState()); }
        static void Select() { if (keyboard != null && keyboard.added) keyboard.MakeCurrent(); }
        static void ClearResult()
        {
            if (game == null || !SessionState.GetBool(Flag, false)) return;
            var overlay = game.view.HudCanvas.Find("Result");
            if (overlay != null) overlay.gameObject.SetActive(false);
        }
        static void Load()
        {
            game.BeginLevel(level, false); Camera.main.targetTexture = target;
            var canvas = game.view.HudCanvas.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = Camera.main; canvas.planeDistance = 1;
            hands = new TrickDemo(game); game.Trick.AddDemo(hands);
            Debug.Log("[GrandIllusions] Native performance begins: " + (level + 1));
        }
        static void Poll()
        {
            if (!SessionState.GetBool(Flag, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (keyboard == null) Initialize();
            if (EditorApplication.timeSinceStartup - began > 240) { Fail("Native performances timed out."); return; }
            if (game == null) game = UnityEngine.Object.FindAnyObjectByType<RabbitTrickGame>();
            if (game == null || game.Menu == null || game.Menu.Canvas == null || Time.frameCount == lastFrame) return;
            lastFrame = Time.frameCount;
            try
            {
                if (!started)
                {
                    if (EditorApplication.timeSinceStartup - began < 1) return;
                    target = new RenderTexture(1600, 900, 24); target.Create(); started = true; Load(); return;
                }
                if (game.IsFailed) { Fail("Act " + (level + 1) + ": " + game.FailureReason + " / " + hands.Caption + " / " + game.Trick.Status); return; }
                if (pending != null)
                {
                    if (Time.frameCount <= photoFrame + 1) return;
                    Save(pending); bool complete = pending.EndsWith("Finale"); pending = null;
                    if (complete)
                    {
                        report.AppendLine("PASS native act " + (level + 1) + ": " + game.Trick.Name + " / " + game.Trick.Status);
                        if (++level < RabbitTrickGame.LevelCount) { Load(); return; }
                        Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/GrandIllusionsRuntime.txt", report.ToString());
                        Debug.Log("[GrandIllusions] FIVE NEW ACTS PASSED NORMAL GAMEPLAY\n" + report);
                        Application.logMessageReceived -= CheckLog; Canvas.willRenderCanvases -= ClearResult;
                        SessionState.SetBool(Flag + ".Pass", true); Camera.main.targetTexture = null;
                        UnityEngine.Object.DestroyImmediate(target); EditorApplication.ExitPlaymode();
                    }
                    return;
                }
                string prefix = "Act" + (level + 1).ToString("00") + "-";
                string shot = game.IsFinished ? prefix + "Finale" : game.StepIndex == 2 ? prefix + "Mechanism" : game.StepIndex == 1 ? prefix + "Premise" : null;
                if (game.Trick is PalmCardTrick card && card.Palmed && game.Hands.BodyPosition.x < 8.3f && game.Hands.BodyPosition.x > 7.2f && !photos.Contains(prefix + "Palming")) shot = prefix + "Palming";
                if (shot != null && !photos.Contains(shot))
                { photos.Add(shot); pending = shot; photoFrame = Time.frameCount; return; }
                if (hands.Done && !game.IsFinished) { Fail("Native choreography ended before act " + (level + 1) + " completed. " + game.Trick.Status); return; }
                hands.Tick(Mathf.Min(Time.deltaTime, 0.05f));
            }
            catch (Exception ex) { Fail(ex.ToString()); }
        }
        static void Save(string name)
        {
            var previous = RenderTexture.active; RenderTexture.active = target;
            var image = new Texture2D(1600, 900, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); image.Apply();
            float dark = 1, light = 0;
            for (int y = 150; y < 780; y += 70) for (int x = 120; x < 1480; x += 80)
            { float value = image.GetPixel(x, y).grayscale; dark = Mathf.Min(dark, value); light = Mathf.Max(light, value); }
            if (light < 0.1f || light - dark < 0.06f) throw new InvalidOperationException("Native frame is blank: " + name);
            Directory.CreateDirectory("Logs"); File.WriteAllBytes("Logs/" + name + ".png", image.EncodeToPNG());
            RenderTexture.active = previous; UnityEngine.Object.DestroyImmediate(image);
        }
        static void CheckLog(string message, string stack, LogType type)
        { if ((type == LogType.Error || type == LogType.Exception) && stack.Contains("Sleight.RabbitTrick")) Fail(message); }
        static void Fail(string message)
        {
            Application.logMessageReceived -= CheckLog; Canvas.willRenderCanvases -= ClearResult;
            SessionState.SetBool(Flag, false); Debug.LogError("[GrandIllusions] FAILED: " + message); Time.timeScale = 1; EditorApplication.Exit(1);
        }
    }
}
