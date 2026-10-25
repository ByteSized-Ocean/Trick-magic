using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

namespace Sleight.RabbitTrick.EditorTools
{
    [InitializeOnLoad]
    public static class MenuRuntimeCheck
    {
        const string Flag = "Sleight.MenuRuntimeCheck";
        static Keyboard keyboard;
        static Key[] held = Array.Empty<Key>();
        static RenderTexture target;
        static int phase;
        static double began, phaseAt;
        static float savedX, savedPatience;
        static MenuRuntimeCheck() { EditorApplication.update += Poll; EditorApplication.playModeStateChanged += State; }
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
            began = phaseAt = EditorApplication.timeSinceStartup; phase = 0;
            InputSystem.settings = UnityEngine.Object.Instantiate(InputSystem.settings);
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            foreach (var device in InputSystem.devices) if (device is Keyboard) InputSystem.DisableDevice(device);
            keyboard = InputSystem.AddDevice<Keyboard>(); held = Array.Empty<Key>();
            InputSystem.onBeforeUpdate -= Supply; InputSystem.onBeforeUpdate += Supply;
            InputSystem.onAfterUpdate -= Select; InputSystem.onAfterUpdate += Select;
            Application.runInBackground = true; Application.logMessageReceived -= CheckLog; Application.logMessageReceived += CheckLog;
        }
        static void Supply() { if (keyboard != null && keyboard.added) InputSystem.QueueStateEvent(keyboard, new KeyboardState(held)); }
        static void Select() { if (keyboard != null && keyboard.added) keyboard.MakeCurrent(); }
        static void Next(int value) { phase = value; phaseAt = EditorApplication.timeSinceStartup; }
        static void Poll()
        {
            if (!SessionState.GetBool(Flag, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (keyboard == null) Initialize();
            var game = UnityEngine.Object.FindAnyObjectByType<RabbitTrickGame>();
            if (game == null || game.Menu == null || game.Menu.Canvas == null) return;
            if (target != null) ConfigureCanvas(game.Menu.Canvas);
            double now = EditorApplication.timeSinceStartup;
            if (now - began > 90) { Fail("Menu runtime check timed out, phase " + phase); return; }
            try
            {
                if (phase == 0)
                {
                    if (now - phaseAt < 1) return;
                    Expect(game.Menu.Page == StageMenu.ScreenPage.Home && Time.timeScale == 0, "Game must start on the home menu.");
                    target = new RenderTexture(1600, 900, 24); target.Create(); Camera.main.targetTexture = target;
                    ConfigureCanvas(game.Menu.Canvas);
                    ConfigureCanvas(game.view.HudCanvas.GetComponent<Canvas>());
                    Next(1); return;
                }
                if (now - phaseAt < 0.18) return;
                switch (phase)
                {
                    case 1:
                        Save("MainMenu");
                        foreach (string id in new[] { "StartGame", "ViewLevels", "Settings", "QuitGame" }) Expect(game.Menu.FindButton(id) != null, "Home button missing: " + id);
                        Click(game, "ViewLevels"); Next(2); break;
                    case 2:
                        Expect(game.Menu.Page == StageMenu.ScreenPage.Levels, "Level browser must open."); Save("LevelSelect");
                        Click(game, "Level5"); Expect(game.Level == 4 && game.InDemo && !game.CanSkipDemo && !game.Menu.BlocksGameplay, "Level card must start with its demo, unskippable for the first 3 seconds.");
                        game.EndDemo();
                        Camera.main.targetTexture = target; savedX = game.Hands.BodyPosition.x; held = new[] { Key.D }; Next(3); break;
                    case 3:
                        Expect(game.Hands.BodyPosition.x > savedX + 0.08f, "Player must move after leaving the menu.");
                        savedX = game.Hands.BodyPosition.x; savedPatience = game.Patience; held = new[] { Key.R }; Next(4); break;
                    case 4:
                        Expect(Mathf.Abs(game.Hands.BodyPosition.x - savedX) < 0.02f && game.Patience < savedPatience, "R outside the P menu must not restart the level.");
                        held = new[] { Key.P }; Next(5); break;
                    case 5:
                        Expect(game.Menu.Page == StageMenu.ScreenPage.Pause && Time.timeScale == 0, "P must open a paused menu.");
                        Save("PauseMenu"); savedPatience = game.Patience; held = Array.Empty<Key>(); Next(6); break;
                    case 6:
                        Expect(Mathf.Abs(game.Patience - savedPatience) < 0.001f, "Pause must freeze audience patience.");
                        held = new[] { Key.R }; Next(7); break;
                    case 7:
                        Expect(!game.Menu.BlocksGameplay && game.Patience > 0.98f && Mathf.Abs(game.Hands.BodyPosition.x - 3.85f) < 0.02f, "R in pause menu must restart this level.");
                        held = Array.Empty<Key>(); Next(8); break;
                    case 8: held = new[] { Key.P }; Next(9); break;
                    case 9:
                        held = Array.Empty<Key>(); Click(game, "PlayDemo"); Expect(game.InDemo && !game.Menu.BlocksGameplay, "Demo must start from pause menu."); Next(10); break;
                    case 10:
                        Expect(game.InDemo, "Demonstration should be running."); game.Menu.ShowPause();
                        Next(20); break;
                    case 20:
                        Click(game, "ReturnHome"); Expect(!game.InDemo && game.Menu.Page == StageMenu.ScreenPage.Home, "Return home must stop the demo.");
                        Camera.main.targetTexture = target; Next(11); break;
                    case 11: Click(game, "Settings"); Next(12); break;
                    case 12:
                        Expect(game.Menu.Page == StageMenu.ScreenPage.Settings, "Settings must open."); Save("SettingsMenu");
                        var audio = game.Soundtrack;
                        float volume = audio.MusicVolume;
                        var slider = game.Menu.Canvas.transform.Find("Contents/MusicSlider").GetComponent<Slider>();
                        slider.value = 0.21f;
                        Expect(Mathf.Abs(audio.MusicVolume - 0.21f) < 0.001f && Mathf.Abs(PlayerPrefs.GetFloat("Sleight.Music") - 0.21f) < 0.001f, "Music slider must apply and persist.");
                        slider.value = volume;
                        Expect(audio.Tracks.Length == 5 && audio.Applause != null && audio.Applause.length > 7, "Five classical recordings and real applause must load.");
                        var unique = new HashSet<AudioClip>();
                        foreach (var track in audio.Tracks) { Expect(track != null && track.length > 30, "Missing classical music track."); unique.Add(track); }
                        Expect(unique.Count == 5, "Each level must have a distinct classical recording.");
                        Click(game, "BackSettings"); Next(22); break;
                    case 22:
                        Save("MainMenu");
                        Click(game, "StartGame"); Next(13); break;
                    case 13:
                        Expect(game.Level == 0 && game.InDemo && !game.Menu.BlocksGameplay, "Start Game must begin the first trick with its demo.");
                        game.EndDemo();
                        game.Hands.MoveBody(game, Vector2.right * 0.5f); game.Menu.ShowPause(); Next(21); break;
                    case 21:
                        Click(game, "RestartLevel");
                        Expect(game.Level == 0 && Mathf.Abs(game.Hands.BodyPosition.x - 3.85f) < 0.02f, "Mouse restart must reset current level.");
                        game.BeginLevel(4, false); game.enabled = false; Camera.main.targetTexture = target;
                        SetDepthFrame(game, 5.1f, 0.05f); Next(14); break;
                    case 14:
                        Save("DepthFront"); SetDepthFrame(game, 5.65f, 0.45f); Next(15); break;
                    case 15:
                        Save("DepthMiddle"); SetDepthFrame(game, 5.65f, 0.9f); Next(16); break;
                    case 16:
                        Save("DepthBack");
                        var display = (DepthTransferTrick)game.Trick;
                        SetDepthFrame(game, 7.3f, 0.58f);
                        display.Orb.position = new Vector2(6.8f, 2f); display.Orb.depth = 0.85f;
                        game.Hands.SelectHand(1); game.Hands.Move(game, display.SecondHandle - game.Hands.Selected.position); game.Hands.Grab(game);
                        Expect(game.Hands.Selected.holding == HandItem.Custom && game.Hands.Selected.custom == 1, "Right screen handle must be reachable for the final reveal.");
                        game.Hands.Move(game, Vector2.down * 0.7f); game.Hands.Release(game);
                        display.Orb.inside = true; display.Orb.depth = 0.85f; game.view.Draw(game);
                        Next(17); break;
                    case 17:
                        Save("GoldenReveal"); game.enabled = true;
                        Directory.CreateDirectory("Logs");
                        File.WriteAllText("Logs/MenuRuntimeValidation.txt", "PASS home, level browser, settings; native mouse raycasts and button clicks; P pauses; R only restarts inside pause; mouse restart; demo from pause; return home; five distinct classical recordings and real applause.\n");
                        Debug.Log("[TheatreMenus] HOME, PAUSE, INPUT, AUDIO VERIFIED");
                        held = Array.Empty<Key>(); Application.logMessageReceived -= CheckLog;
                        SessionState.SetBool(Flag + ".Pass", true); Camera.main.targetTexture = null;
                        UnityEngine.Object.DestroyImmediate(target); Time.timeScale = 1; EditorApplication.ExitPlaymode(); break;
                }
            }
            catch (Exception ex) { Fail(ex.ToString()); }
        }
        static void ConfigureCanvas(Canvas canvas)
        { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = Camera.main; canvas.planeDistance = 1; }
        static void SetDepthFrame(RabbitTrickGame game, float x, float depth)
        {
            game.Hands.MoveBody(game, Vector2.right * (x - game.Hands.BodyPosition.x));
            game.Hands.MoveBody(game, Vector2.up * ((DualHandController.FrontY + depth * DualHandController.LaneLift - game.Hands.BodyPosition.y) / 0.55f));
            var orb = ((DepthTransferTrick)game.Trick).Orb; orb.position = new Vector2(x, 2f); orb.depth = depth;
            game.Audience.Follow(x); game.Audience.Tick(2); game.view.Draw(game);
        }
        static void Click(RabbitTrickGame game, string id)
        {
            var button = game.Menu.FindButton(id); Expect(button != null, "Button not found: " + id);
            // Exercise the same overlay raycast used in normal play, including over the demo's separate camera.
            game.Menu.Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            Canvas.ForceUpdateCanvases();
            var rt = button.GetComponent<RectTransform>();
            var point = RectTransformUtility.WorldToScreenPoint(null, rt.TransformPoint(rt.rect.center));
            var pointer = new PointerEventData(EventSystem.current) { position = point, button = PointerEventData.InputButton.Left };
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
            Expect(hits.Count > 0 && hits[0].gameObject == button.gameObject, "Mouse raycast does not reach " + id + ": point=" + point + ", first=" + (hits.Count > 0 ? hits[0].gameObject.name : "none") + ", depth=" + button.targetGraphic.depth);
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
            ConfigureCanvas(game.Menu.Canvas);
            Debug.Log("[TheatreMenus] Click " + id + " -> " + game.Menu.Page);
        }
        static void Save(string name)
        {
            var previous = RenderTexture.active; RenderTexture.active = target;
            var image = new Texture2D(1600, 900, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); image.Apply();
            Directory.CreateDirectory("Logs"); File.WriteAllBytes("Logs/" + name + ".png", image.EncodeToPNG());
            RenderTexture.active = previous; UnityEngine.Object.DestroyImmediate(image);
        }
        static void Expect(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        static void CheckLog(string message, string stack, LogType type)
        { if ((type == LogType.Error || type == LogType.Exception) && stack.Contains("Sleight.RabbitTrick")) Fail(message); }
        static void Fail(string message)
        { Application.logMessageReceived -= CheckLog; SessionState.SetBool(Flag, false); Debug.LogError("[TheatreMenus] FAILED: " + message); Time.timeScale = 1; EditorApplication.Exit(1); }
    }
}
