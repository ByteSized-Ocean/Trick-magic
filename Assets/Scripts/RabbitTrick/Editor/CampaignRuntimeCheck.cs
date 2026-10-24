using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

namespace Sleight.RabbitTrick.EditorTools
{
    // Capture real player-loop frames after loading the persisted scene, and exercise continuous W/S input.
    [InitializeOnLoad]
    public static class CampaignRuntimeCheck
    {
        const string Flag = "Sleight.FiveTricks.RuntimeCheck";
        static Keyboard keyboard;
        static Key[] held = Array.Empty<Key>();
        static RenderTexture target;
        static int phase, level, captureFrame;
        static double begun, phaseAt;
        static float stoppedDepth;
        static CampaignRuntimeCheck()
        {
            EditorApplication.update += Poll;
            EditorApplication.playModeStateChanged += OnState;
        }
        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Game.unity");
            SessionState.SetBool(Flag, true); SessionState.SetBool(Flag + ".Pass", false);
            EditorApplication.EnterPlaymode();
        }
        static void OnState(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Flag, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode) Initialize();
            if (state == PlayModeStateChange.EnteredEditMode)
            { SessionState.SetBool(Flag, false); EditorApplication.Exit(SessionState.GetBool(Flag + ".Pass", false) ? 0 : 1); }
        }
        static void Initialize()
        {
            begun = phaseAt = EditorApplication.timeSinceStartup; phase = level = 0;
            InputSystem.settings = UnityEngine.Object.Instantiate(InputSystem.settings);
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            foreach (var device in InputSystem.devices) if (device is Keyboard) InputSystem.DisableDevice(device);
            keyboard = InputSystem.AddDevice<Keyboard>(); held = Array.Empty<Key>();
            InputSystem.onBeforeUpdate -= Supply; InputSystem.onBeforeUpdate += Supply;
            InputSystem.onAfterUpdate -= Select; InputSystem.onAfterUpdate += Select;
            Application.runInBackground = true;
            Application.logMessageReceived -= CheckLog;
            Application.logMessageReceived += CheckLog;
        }
        static void Supply() { if (keyboard != null && keyboard.added) InputSystem.QueueStateEvent(keyboard, new KeyboardState(held)); }
        static void Select() { if (keyboard != null && keyboard.added) keyboard.MakeCurrent(); }
        static void Poll()
        {
            if (!SessionState.GetBool(Flag, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (keyboard == null) Initialize();
            double now = EditorApplication.timeSinceStartup;
            if (now - begun > 60) { Fail("Runtime check timed out."); return; }
            var game = UnityEngine.Object.FindAnyObjectByType<RabbitTrickGame>();
            if (game == null) return;
            try
            {
                if (phase == 0)
                {
                    if (now - phaseAt < 1) return;
                    target = new RenderTexture(1600, 900, 24); target.Create(); Camera.main.targetTexture = target;
                    var canvas = game.view.HudCanvas.GetComponent<Canvas>();
                    canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = Camera.main; canvas.planeDistance = 1;
                    Load(game, 0); phase = 1; phaseAt = now; captureFrame = Time.frameCount;
                }
                else if (phase == 1)
                {
                    if (now - phaseAt < 0.5 || Time.frameCount < captureFrame + 3) return;
                    SaveFrame("Logs/FiveTricks-Level" + (level + 1) + ".png");
                    Expect(Camera.main.targetTexture == target, "Demo television must release the camera before capture.");
                    Expect(game.Level == level && game.StepNames.Length == 3, "Loaded level / HUD mismatch.");
                    if (++level < RabbitTrickGame.LevelCount)
                    { Load(game, level); phaseAt = now; captureFrame = Time.frameCount; }
                    else { held = new[] { Key.W }; phase = 2; phaseAt = now; }
                }
                else if (phase == 2)
                {
                    if (game.Hands.Depth < 0.3f && now - phaseAt < 5) return;
                    Expect(game.Hands.Depth > 0.1f && game.Hands.Depth < 0.95f, "W must move to intermediate depths, not switch lanes.");
                    held = Array.Empty<Key>(); stoppedDepth = game.Hands.Depth; phase = 3; phaseAt = now;
                }
                else if (phase == 3)
                {
                    if (now - phaseAt < 0.4) return;
                    Expect(Mathf.Abs(game.Hands.Depth - stoppedDepth) < 0.04f, "Releasing W must stop at the chosen depth.");
                    held = new[] { Key.S }; phase = 4; phaseAt = now;
                }
                else
                {
                    if (game.Hands.Depth > 0.05f && now - phaseAt < 5) return;
                    Expect(game.Hands.Depth < stoppedDepth - 0.1f, "S must move continuously toward the front.");
                    held = Array.Empty<Key>();
                    Directory.CreateDirectory("Logs");
                    File.WriteAllText("Logs/FiveTrickRuntime.txt", "PASS persisted five-level scene; five normal-player-loop screenshots; W/S intermediate depth, release-to-stop, return-to-front.\n");
                    Debug.Log("[FiveTricks] NORMAL-FRAME FIVE LEVELS AND SMOOTH W/S VERIFIED");
                    Application.logMessageReceived -= CheckLog;
                    SessionState.SetBool(Flag + ".Pass", true);
                    Camera.main.targetTexture = null;
                    game.view.HudCanvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                    UnityEngine.Object.DestroyImmediate(target);
                    EditorApplication.ExitPlaymode();
                }
            }
            catch (Exception exception) { Fail(exception.Message); }
        }
        static void Load(RabbitTrickGame game, int next)
        {
            game.JumpToLevel(next); if (game.InDemo) game.EndDemo();
            Camera.main.targetTexture = target;
            Canvas.ForceUpdateCanvases();
        }
        static void SaveFrame(string path)
        {
            var previous = RenderTexture.active; RenderTexture.active = target;
            var image = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); image.Apply();
            Directory.CreateDirectory("Logs"); File.WriteAllBytes(path, image.EncodeToPNG());
            float darkest = 1, brightest = 0;
            for (int y = 160; y < 780; y += 60)
                for (int x = 120; x < 1500; x += 80)
                {
                    var c = image.GetPixel(x, y);
                    darkest = Mathf.Min(darkest, c.grayscale); brightest = Mathf.Max(brightest, c.grayscale);
                }
            Expect(brightest > 0.1f && brightest - darkest > 0.06f, "Captured frame is blank or not rendering the stage.");
            RenderTexture.active = previous; UnityEngine.Object.DestroyImmediate(image);
        }
        static void Expect(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        static void CheckLog(string message, string stack, LogType type)
        {
            if ((type == LogType.Exception || type == LogType.Error) && stack.Contains("Sleight.RabbitTrick")) Fail(message);
        }
        static void Fail(string reason)
        { Application.logMessageReceived -= CheckLog; SessionState.SetBool(Flag, false); Debug.LogError("[FiveTricks] RUNTIME CHECK FAILED: " + reason); EditorApplication.Exit(1); }
    }
}
