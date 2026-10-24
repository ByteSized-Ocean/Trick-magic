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
    [InitializeOnLoad]
    public static class RabbitPlaySmoke
    {
        const string EnabledKey = "Sleight.Rabbit.PlaySmoke";
        static int stage, captureFrame;
        static double started, stageAt;
        static Vector2 leftStart, bodyStart, rightStart;
        static Keyboard keyboard;
        static Key[] heldKeys = Array.Empty<Key>();
        static RenderTexture target;
        static RabbitPlaySmoke()
        {
            EditorApplication.update += Poll;
            EditorApplication.playModeStateChanged += OnPlayState;
        }
        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Game.unity");
            SessionState.SetBool(EnabledKey, true);
            SessionState.SetBool(EnabledKey + ".Completed", false);
            EditorApplication.EnterPlaymode();
        }
        static void OnPlayState(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(EnabledKey, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode) BeginInput();
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                SessionState.SetBool(EnabledKey, false);
                EditorApplication.Exit(SessionState.GetBool(EnabledKey + ".Completed", false) ? 0 : 1);
            }
        }
        static void BeginInput()
        {
            stage = 0; started = stageAt = EditorApplication.timeSinceStartup;
            InputSystem.settings = UnityEngine.Object.Instantiate(InputSystem.settings);
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            Application.runInBackground = true;
            keyboard = InputSystem.AddDevice<Keyboard>();
            heldKeys = Array.Empty<Key>();
            InputSystem.onBeforeUpdate -= SupplyKeyboard;
            InputSystem.onBeforeUpdate += SupplyKeyboard;
            InputSystem.onAfterUpdate -= SelectKeyboard;
            InputSystem.onAfterUpdate += SelectKeyboard;
            Application.logMessageReceived -= OnLog;
            Application.logMessageReceived += OnLog;
        }
        static void Poll()
        {
            if (!SessionState.GetBool(EnabledKey, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (keyboard == null)
            {
                BeginInput();
                var restored = UnityEngine.Object.FindAnyObjectByType<RabbitTrickGame>();
                if (restored != null) restored.ResetTrick();
            }
            double now = EditorApplication.timeSinceStartup;
            if (now - started > 45) { Fail("Runtime verification timed out."); return; }
            var game = UnityEngine.Object.FindAnyObjectByType<RabbitTrickGame>();
            if (game == null) { Fail("Game missing after scene reload."); return; }
            try
            {
                switch (stage)
                {
                    case 0:
                        if (now - stageAt < 0.8) return;
                        Expect(game.Hands.Active == 0, "Start selects left hand");
                        bodyStart = game.Hands.BodyPosition; leftStart = game.Hands.Hands[0].position; rightStart = game.Hands.Hands[1].position;
                        BeginCapture(); Press(Key.F); Next(now); break;
                    case 1:
                        if (now - stageAt < 0.4 || Time.frameCount < captureFrame + 3) return;
                        Expect(game.Hands.Active == 1, "F switches to right hand");
                        FinishCapture();
                        Press(Key.RightArrow); Next(now); break;
                    case 2:
                        if (game.Hands.Hands[1].position.x <= rightStart.x + 0.15f && now - stageAt < 4) return;
                        Expect(game.Hands.Hands[1].position.x > rightStart.x + 0.1f, "arrows move selected hand");
                        Expect(game.Hands.BodyPosition == bodyStart && game.Hands.Hands[0].position == leftStart, "arrows do not move body or inactive hand");
                        Press(Key.D); Next(now); break;
                    case 3:
                        if (game.Hands.BodyPosition.x <= bodyStart.x + 0.005f && now - stageAt < 4) return;
                        Expect(game.Hands.BodyPosition.x > bodyStart.x + 0.005f, $"WASD moves magician: before={bodyStart}, after={game.Hands.BodyPosition}, D={Keyboard.current.dKey.isPressed}, dt={Time.deltaTime}");
                        CheckArms(game);
                        Press();
                        game.Hands.MoveBody(game, Vector2.right * (5.15f - game.Hands.BodyPosition.x));
                        game.Hands.Selected.position = game.Hands.ClampHand(1, game.LidHandle);
                        Next(now); break;
                    case 4:
                        if (now - stageAt < 0.1) return;
                        Press(Key.E); Next(now); break;
                    case 5:
                        if (now - stageAt < 0.15) return;
                        Expect(game.Hands.Selected.holding == HandItem.Lid, "E grabs nearby lid");
                        Press(Key.UpArrow); Next(now); break;
                    case 6:
                        if (game.LidAmount < 0.92f && now - stageAt < 4) return;
                        Expect(game.LidAmount >= 0.92f, "hand pulls open lid without stretching");
                        CheckArms(game);
                        Press(); Next(now); break;
                    case 7:
                        if (game.Rules.Step != TrickStep.HideRabbit && now - stageAt < 4) return;
                        Expect(game.Rules.Step == TrickStep.HideRabbit, "live lit-empty display advances");
                        game.Hands.Selected.holding = HandItem.Rabbit;
                        game.Hands.Selected.position = game.BoxOpening + Vector2.up * 0.12f;
                        game.Rules.TouchRabbit(); Next(now); break;
                    case 8:
                        if (now - stageAt < 0.2) return;
                        Expect(game.Rules.Failed, "exposed rabbit fails in actual Update");
                        Press(Key.R); Next(now); break;
                    case 9:
                        if (now - stageAt < 0.2) return;
                        Expect(!game.Rules.Failed && game.Hands.Active == 0 && game.Rules.Step == TrickStep.ShowEmpty, "R resets game");
                        Press(); Next(now); break;
                    case 10:
                        if (now - stageAt < 0.2) return;
                        Directory.CreateDirectory("Logs");
                        File.WriteAllText("Logs/RabbitPlaySmoke.txt", "PASS: actual Play mode; WASD body movement; arrow-only hand control; fixed arm lengths; F/E/R; live presentation/exposure; normal-frame sprite tint and alpha pixel checks.\n");
                        Debug.Log("[RabbitTrick] PLAY MODE INPUT AND NORMAL-FRAME RENDER VERIFIED");
                        SessionState.SetBool(EnabledKey + ".Completed", true);
                        Application.logMessageReceived -= OnLog;
                        EditorApplication.ExitPlaymode(); break;
                }
            }
            catch (Exception exception) { Fail(exception.Message); }
        }
        static void CheckArms(RabbitTrickGame game)
        {
            for (int i = 0; i < 2; i++)
            {
                Expect(Mathf.Abs(Vector2.Distance(game.Hands.Shoulder(i), game.Hands.Elbow(i)) - game.Hands.upperArm) < 0.002f, "upper arm length fixed");
                Expect(Mathf.Abs(Vector2.Distance(game.Hands.Elbow(i), game.Hands.Hands[i].position) - game.Hands.forearm) < 0.002f, "forearm length fixed");
            }
        }
        static void BeginCapture()
        {
            target = new RenderTexture(1600, 900, 24);
            target.Create();
            Camera.main.targetTexture = target;
            var canvas = UnityEngine.Object.FindAnyObjectByType<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = Camera.main;
            canvas.planeDistance = 1;
            Canvas.ForceUpdateCanvases();
            captureFrame = Time.frameCount;
            // Let the normal player loop render several frames. Never call Camera.Render here.
        }
        static void FinishCapture()
        {
            var previous = RenderTexture.active;
            RenderTexture.active = target;
            var image = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); image.Apply();
            Directory.CreateDirectory("Logs");
            File.WriteAllBytes("Logs/RabbitRuntimeVerified.png", image.EncodeToPNG());
            Color curtain = Pixel(image, 0.25f, 3.8f), box = Pixel(image, 5.4f, 1.7f), wall = Pixel(image, 8.9f, 3.7f);
            Debug.Log($"[RabbitTrick] render samples curtain={curtain} box={box} wall={wall}");
            Expect(curtain.r > curtain.g * 1.3f, "curtain keeps red tint during normal rendering");
            Expect(box.b > box.r * 1.08f, "box keeps blue tint rather than turning white");
            Expect(wall.b > wall.r * 1.08f && wall.maxColorComponent < 0.6f, "dark wall is tinted and not a grey-white board");
            RenderTexture.active = previous;
            Camera.main.targetTexture = null;
            UnityEngine.Object.FindAnyObjectByType<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            UnityEngine.Object.DestroyImmediate(image);
            UnityEngine.Object.DestroyImmediate(target);
        }
        static Color Pixel(Texture2D image, float x, float y) => image.GetPixel(Mathf.RoundToInt(x / 10f * (image.width - 1)), Mathf.RoundToInt(y / 5.625f * (image.height - 1)));
        static void Next(double now) { stage++; stageAt = now; }
        static void Press(params Key[] keys) { heldKeys = keys; InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys)); }
        static void SupplyKeyboard()
        {
            if (keyboard != null && keyboard.added) InputSystem.QueueStateEvent(keyboard, new KeyboardState(heldKeys));
        }
        static void SelectKeyboard() { if (keyboard != null && keyboard.added) keyboard.MakeCurrent(); }
        static void Expect(bool value, string reason) { if (!value) throw new InvalidOperationException(reason); }
        static void OnLog(string condition, string stack, LogType type)
        { if ((type == LogType.Exception || type == LogType.Error) && stack.Contains("Sleight.RabbitTrick")) Fail(condition); }
        static void Fail(string reason)
        {
            Application.logMessageReceived -= OnLog; SessionState.SetBool(EnabledKey, false);
            Debug.LogError("[RabbitTrick] PLAY SMOKE FAILED: " + reason); EditorApplication.Exit(1);
        }
    }
}
