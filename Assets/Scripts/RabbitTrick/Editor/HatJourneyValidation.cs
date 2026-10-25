using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
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
    public static class HatJourneyValidation
    {
        const string Flag = "Sleight.HatJourneyValidation";
        static int phase, species, version, firstCount;
        static double began, phaseAt;
        static StringBuilder report;
        static Keyboard keyboard;
        static RenderTexture target;
        static HatJourneyValidation() { EditorApplication.update += Poll; EditorApplication.playModeStateChanged += State; }

        public static void Prepare()
        {
            MenuHatValidation.Prepare();
            var log = new StringBuilder();
            for (int i = 0; i < 13; i++)
            {
                var animal = (HatSpecies)i; float start = HatAnimalMotion.ExitAt(animal), duration = HatAnimalMotion.ExitDuration(animal);
                bool quick = animal == HatSpecies.Mouse || animal == HatSpecies.Rabbit || animal == HatSpecies.Dove;
                Expect(quick ? Mathf.Approximately(duration, 4.5f) && Mathf.Approximately(start, 2.4f) : start + duration >= 9,
                    "Mouse, rabbit and dove have faster exits; other performances retain their long duration.");
                for (int route = 0; route < 4; route++)
                {
                    var intro = HatAnimalMotion.Sample(animal, 1, route);
                    var exit = HatAnimalMotion.Sample(animal, start + 1.3f, route);
                    Expect(!intro.Departing && intro.Alpha > 0.9f && exit.Departing && exit.Alpha > 0.95f, "Do not fade the animal out during its exit.");
                    Expect(Vector2.Distance(exit.Position, HatAnimalMotion.Hold(animal)) > 20, "Each exit must move away from the hat.");
                    for (int frame = 0; frame <= 160; frame++)
                    {
                        var p = HatAnimalMotion.Sample(animal, frame * (start + duration + 1) / 160, route);
                        Expect(!float.IsNaN(p.Position.x) && !float.IsInfinity(p.Position.y) && !float.IsNaN(p.Rotation), "Motion must remain finite.");
                    }
                    var end = HatAnimalMotion.Sample(animal, start + duration + 0.1f, route);
                    Expect(end.Finished && end.Alpha == 0 && (end.Position.x < -150 || end.Position.x > 1750 || end.Position.y < -150 || end.Position.y > 1050), "Animals must leave the screen before cleanup.");
                }
                log.AppendLine("PASS " + animal + ": " + HatAnimalMotion.Style(animal) + ", " + (start + duration).ToString("0.0") + "s, four finite offscreen routes");
            }
            var penguin = HatAnimalMotion.Sample(HatSpecies.EmperorPenguin, 3.8f, 0);
            Expect(Mathf.Abs(penguin.Rotation) > 85, "Penguin must lie on its belly before sliding away.");
            var jump = HatAnimalMotion.Sample(HatSpecies.Monkey, 2.4f + 7.8f / 8, 0);
            Expect(jump.Position.y < 500, "Monkey must jump rather than simply drift away.");
            File.WriteAllText("Logs/HatJourneyModelValidation.txt", log + "PASS penguin belly pose, monkey jump, unchanged exhaustive probabilities and all imported animal/Foley clips.\n");
            Debug.Log("[HatJourney] PATHS AND AUDIO VERIFIED");
        }
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
            { SessionState.SetBool(Flag, false); InputSystem.onBeforeUpdate -= Input; if (Application.isBatchMode) EditorApplication.Exit(SessionState.GetBool(Flag + ".Pass", false) ? 0 : 1); }
        }
        static void Initialize()
        {
            began = phaseAt = EditorApplication.timeSinceStartup; phase = species = version = 0; report = new StringBuilder();
            InputSystem.settings = UnityEngine.Object.Instantiate(InputSystem.settings);
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            foreach (var device in InputSystem.devices) if (device is Keyboard) InputSystem.DisableDevice(device);
            keyboard = InputSystem.AddDevice<Keyboard>(); InputSystem.onBeforeUpdate -= Input; InputSystem.onBeforeUpdate += Input;
            Application.runInBackground = true; Application.targetFrameRate = 60; QualitySettings.vSyncCount = 0;
        }
        static void Input() { if (keyboard != null && keyboard.added) { InputSystem.QueueStateEvent(keyboard, new KeyboardState()); keyboard.MakeCurrent(); } }
        static void Next(int value) { phase = value; phaseAt = EditorApplication.timeSinceStartup; }
        static string Prefix => "HatJourney-" + (version == 1 ? "Golden-" : "Normal-") + (HatSpecies)species;
        static void Poll()
        {
            if (!SessionState.GetBool(Flag, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (keyboard == null || report == null) Initialize();
            EditorApplication.QueuePlayerLoopUpdate();
            var game = UnityEngine.Object.FindAnyObjectByType<RabbitTrickGame>(); if (game == null || game.Menu == null || game.Menu.HatToy == null) return;
            var toy = game.Menu.HatToy; double now = EditorApplication.timeSinceStartup, wait = now - phaseAt;
            float end = HatAnimalMotion.ExitAt((HatSpecies)species) + HatAnimalMotion.ExitDuration((HatSpecies)species);
            try
            {
                if (now - began > 160) throw new InvalidOperationException("Journey check timed out at phase " + phase);
                switch (phase)
                {
                    case 0:
                        if (wait < 1) return; game.Scores.SaveRecords = false;
                        target = new RenderTexture(1600, 900, 24); target.Create(); Camera.main.targetTexture = target; Configure(game.Menu.Canvas);
                        Expect(Time.timeScale == 0, "Menu animation must use unscaled time."); Next(1); break;
                    case 1:
                        if (toy.CooldownRemaining > 0) return;
                        game.Menu.ShowHome(); Configure(game.Menu.Canvas); Next(2); break;
                    case 2:
                        if (wait < 0.2) return;
                        toy.QueueNextForEditor((HatSpecies)species, version == 1); firstCount = toy.RevealCount; Click(game, "HatButton");
                        Expect(toy.RevealCount == firstCount + 1 && !toy.HasQueuedForEditor && toy.LastDraw.Species == (HatSpecies)species && toy.LastDraw.Golden == (version == 1), "Next-animal override must be used exactly once by a real click.");
                        toy.QueueNextForEditor(HatSpecies.Mouse); Expect(!toy.TryReveal() && toy.HasQueuedForEditor, "Cooldown must not consume a queued animal."); toy.ClearQueuedForEditor(); Next(3); break;
                    case 3:
                        if (toy.VisitorAge < 0.9f) return;
                        toy.RefreshVisuals(); Save(Prefix + "-Entrance");
                        Expect(game.Menu.Canvas.GetComponentsInChildren<HatJourneyGraphic>().Length == 1, "Each visitor must have its own environmental effect.");
                        toy.SeekLatestForEditor(HatAnimalMotion.ExitAt((HatSpecies)species) + 1.3f); Next(4); break;
                    case 4:
                        if (wait < 0.14) return;
                        toy.RefreshVisuals(); Save(Prefix + "-Exit"); Probe(game, "StartGame");
                        Expect(toy.LastDepartureCall != null && toy.LastDepartureCall.name == HatAnimalRoster.Animals[species].Sound, "Departure call must match the animal.");
                        bool visible = false;
                        foreach (var animal in game.Menu.Canvas.GetComponentsInChildren<HatAnimalGraphic>()) visible |= animal.RenderedVertexCount > 0 && animal.color.a > 0.9f;
                        Expect(visible, "Exit animal must remain visible rather than instantly fade.");
                        foreach (var effect in game.Menu.Canvas.GetComponentsInChildren<HatJourneyGraphic>())
                            Expect(effect.RenderedVertexCount > 0, "Departure wind, dust, ice, bubbles, clouds or tracks must render.");
                        if (species == (int)HatSpecies.Monkey) { toy.SeekLatestForEditor(4.75f); Next(5); }
                        else { toy.SeekLatestForEditor(end + 0.3f); Next(6); }
                        break;
                    case 5:
                        if (wait < 0.14) return; Save(Prefix + "-Banana"); toy.SeekLatestForEditor(end + 0.3f); Next(6); break;
                    case 6:
                        if (wait < 0.14) return;
                        Save(Prefix + "-Trail"); Expect(game.Menu.Canvas.GetComponentsInChildren<HatAnimalGraphic>().Length == 0, "Body must leave the screen while the trail finishes.");
                        toy.SeekLatestForEditor(end + 1.8f); Expect(toy.ActiveVisitorCount == 0, "Finished visitor and trail must be reclaimed.");
                        report.AppendLine("PASS " + (version == 1 ? "golden " : "normal ") + (HatSpecies)species + ": native entry, animated exit, departure call, environmental trail, clickable menu and cleanup");
                        if (++species < 13) { Next(1); break; }
                        if (version == 0) { version = 1; species = 0; Next(1); }
                        else { species = 0; Next(7); }
                        break;
                    case 7:
                        if (toy.CooldownRemaining > 0) return;
                        game.Menu.ShowHome(); toy.QueueNextForEditor(HatSpecies.EmperorPenguin, true); Expect(toy.TryReveal(), "First rapid-click visitor must spawn."); Next(8); break;
                    case 8:
                        if (toy.CooldownRemaining > 0) return;
                        toy.QueueNextForEditor(HatSpecies.Eagle); Expect(toy.TryReveal() && toy.ActiveVisitorCount == 2, "A new visitor must not destroy the earlier long exit animation.");
                        game.Menu.ShowLevels(); Expect(toy.ActiveVisitorCount == 0 && !toy.HasDragonEffect && !toy.VoiceSource.isPlaying && !toy.MagicSource.isPlaying, "Leaving home must stop all long sequences and sounds immediately.");
                        game.Menu.ShowHome(); Configure(game.Menu.Canvas); Next(9); break;
                    case 9:
                        if (wait < 0.2) return; Click(game, "StartGame");
                        Expect(game.InDemo && !game.Menu.BlocksGameplay, "Start Game must still work after environmental effects.");
                        File.WriteAllText("Logs/HatJourneyRuntimeValidation.txt", report + "PASS one-shot debug override, cooldown preservation, continuous rapid-click visitors, time-scale-zero animation, environmental effect raycasts, full menu cleanup and Start Game.\n");
                        Debug.Log("[HatJourney] ALL 26 LONG PERFORMANCES VERIFIED"); SessionState.SetBool(Flag + ".Pass", true);
                        Camera.main.targetTexture = null; UnityEngine.Object.DestroyImmediate(target); EditorApplication.ExitPlaymode(); break;
                }
            }
            catch (Exception ex) { Debug.LogError("[HatJourney] FAILED: " + ex); SessionState.SetBool(Flag, false); EditorApplication.Exit(1); }
        }
        static void Configure(Canvas canvas) { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = Camera.main; canvas.planeDistance = 1; }
        static Button Probe(RabbitTrickGame game, string id)
        {
            var button = game.Menu.FindButton(id); Expect(button != null, "Button missing: " + id);
            game.Menu.Canvas.renderMode = RenderMode.ScreenSpaceOverlay; Canvas.ForceUpdateCanvases();
            var rt = button.GetComponent<RectTransform>(); var point = RectTransformUtility.WorldToScreenPoint(null, rt.TransformPoint(rt.rect.center));
            var data = new PointerEventData(EventSystem.current) { position = point, button = PointerEventData.InputButton.Left };
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(data, hits);
            Expect(hits.Count > 0 && hits[0].gameObject == button.gameObject, "Environmental effect blocks " + id); Configure(game.Menu.Canvas); return button;
        }
        static void Click(RabbitTrickGame game, string id)
        { var button = Probe(game, id); ExecuteEvents.Execute(button.gameObject, new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler); }
        static void Save(string name)
        {
            Camera.main.targetTexture = target; Canvas.ForceUpdateCanvases(); Camera.main.Render();
            var previous = RenderTexture.active; RenderTexture.active = target;
            var image = new Texture2D(1600, 900, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); image.Apply();
            File.WriteAllBytes("Logs/" + name + ".png", image.EncodeToPNG()); RenderTexture.active = previous; UnityEngine.Object.DestroyImmediate(image);
        }
        static void Expect(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    }
}
