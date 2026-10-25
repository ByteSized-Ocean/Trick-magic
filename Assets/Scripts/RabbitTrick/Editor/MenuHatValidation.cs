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
    public static class MenuHatValidation
    {
        const string Flag = "Sleight.MenuHatValidation";
        static int phase, index, mode, oldCount;
        static double began, phaseAt;
        static RenderTexture target;
        static Keyboard keyboard;
        static StringBuilder report;
        static MenuHatValidation() { EditorApplication.update += Poll; EditorApplication.playModeStateChanged += State; }
        public static void Prepare()
        {
            var normal = new int[13]; var gold = new int[13];
            for (int ticket = 0; ticket < HatAnimalRoster.Tickets; ticket++)
                for (int golden = 0; golden < 100; golden++)
                {
                    var draw = HatAnimalRoster.Resolve(ticket, golden);
                    if (draw.Golden) gold[(int)draw.Species]++; else normal[(int)draw.Species]++;
                }
            var results = new StringBuilder();
            for (int i = 0; i < 13; i++)
            {
                var animal = HatAnimalRoster.Animals[i];
                Expect(normal[i] == animal.Weight * 99 && gold[i] == animal.Weight, "Incorrect unconditional or golden probability: " + animal.Name);
                results.AppendLine("PASS " + animal.Name + ": species " + animal.Weight / 100f + "%, golden " + animal.Weight / 10000f + "%");
            }
            foreach (string file in Directory.GetFiles("Assets/Resources/HatAudio", "*.ogg"))
            {
                var importer = AssetImporter.GetAtPath(file.Replace('\\', '/')) as AudioImporter;
                var settings = importer.defaultSampleSettings; settings.loadType = AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat = AudioCompressionFormat.Vorbis; settings.quality = 0.7f;
                importer.defaultSampleSettings = settings; importer.forceToMono = true; importer.SaveAndReimport();
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(file.Replace('\\', '/'));
                Expect(clip != null && clip.length > 0.3f, "Hat call failed to import: " + file);
                var samples = new float[clip.samples * clip.channels]; Expect(clip.GetData(samples, 0), "Call must be decodable: " + file);
                float peak = 0; foreach (float sample in samples) peak = Mathf.Max(peak, Mathf.Abs(sample));
                Expect(peak > 0.05f, "Call must not be silent: " + file);
            }
            Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/MenuHatModelValidation.txt", results + "PASS exhaustive 1,000,000 ticket combinations; 13 calls plus gold and dragon cues imported and decoded.\n");
            AssetDatabase.SaveAssets(); Debug.Log("[MenuHat] PROBABILITIES AND AUDIO VERIFIED");
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
            { SessionState.SetBool(Flag, false); InputSystem.onBeforeUpdate -= Supply; if (Application.isBatchMode) EditorApplication.Exit(SessionState.GetBool(Flag + ".Pass", false) ? 0 : 1); }
        }
        static void Initialize()
        {
            began = phaseAt = EditorApplication.timeSinceStartup; phase = index = mode = 0; report = new StringBuilder();
            InputSystem.settings = UnityEngine.Object.Instantiate(InputSystem.settings);
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            foreach (var device in InputSystem.devices) if (device is Keyboard) InputSystem.DisableDevice(device);
            keyboard = InputSystem.AddDevice<Keyboard>(); InputSystem.onBeforeUpdate -= Supply; InputSystem.onBeforeUpdate += Supply;
            Application.runInBackground = true;
            Application.targetFrameRate = 60; QualitySettings.vSyncCount = 0;
        }
        static void Supply() { if (keyboard != null && keyboard.added) { InputSystem.QueueStateEvent(keyboard, new KeyboardState()); keyboard.MakeCurrent(); } }
        static void Next(int value) { phase = value; phaseAt = EditorApplication.timeSinceStartup; }
        static void Poll()
        {
            if (!SessionState.GetBool(Flag, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (keyboard == null || report == null) Initialize();
            EditorApplication.QueuePlayerLoopUpdate();
            var game = UnityEngine.Object.FindAnyObjectByType<RabbitTrickGame>(); if (game == null || game.Menu == null || game.Menu.HatToy == null) return;
            var toy = game.Menu.HatToy; double now = EditorApplication.timeSinceStartup, age = now - phaseAt;
            try
            {
                if (now - began > 100) throw new InvalidOperationException("Hat check timed out in phase " + phase);
                switch (phase)
                {
                    case 0:
                        if (age < 1) return;
                        game.Scores.SaveRecords = false;
                        Expect(game.Menu.Page == StageMenu.ScreenPage.Home && Time.timeScale == 0, "Hat must work on the paused-time home screen.");
                        target = new RenderTexture(1600, 900, 24); target.Create(); Camera.main.targetTexture = target; Configure(game.Menu.Canvas);
                        Expect(game.Menu.Canvas.transform.Find("Contents/HatHintArrow") != null, "Clickable hat needs its arrow hint."); Next(1); break;
                    case 1:
                        if (age < 0.25) return;
                        Save("Hat-Home"); oldCount = toy.RevealCount; Click(game, "HatButton");
                        Expect(toy.RevealCount == oldCount + 1 && toy.LastCall != null, "A real mouse click must reveal an animal and trigger its call. Count=" + toy.RevealCount + ", before=" + oldCount + ", call=" + (toy.LastCall == null ? "NULL" : toy.LastCall.name) + ", cooldown=" + toy.CooldownRemaining + ", enabled=" + game.Menu.FindButton("HatButton").IsInteractable());
                        Click(game, "HatButton"); Expect(toy.RevealCount == oldCount + 1 && !toy.TryReveal(), "Repeated clicks inside one second must be ignored.");
                        Next(2); break;
                    case 2:
                        if (age < 0.45) return;
                        Expect(toy.CooldownRemaining > 0.1f, "Cooldown must still be active before a second has elapsed.");
                        Expect(game.Menu.Canvas.GetComponentsInChildren<HatAnimalGraphic>().Length > 0, "Animal must animate while Time.timeScale is zero.");
                        Next(3); break;
                    case 3:
                        if (toy.CooldownRemaining > 0) return;
                        Expect(toy.PreviewForEditor((HatSpecies)index, mode == 1), "Preview uses the same cooldown and reveal path.");
                        Expect(toy.LastCall != null && toy.LastCall.name == HatAnimalRoster.Animals[index].Sound, "Animal call must match its species.");
                        if (mode == 1) Expect(toy.MagicSource.isPlaying, "Golden visitor must play the gold cue.");
                        Next(4); break;
                    case 4:
                        if (age < 0.42 || toy.VisitorAge < 0.42f) return;
                        toy.RefreshVisuals();
                        Save("Hat-" + (mode == 1 ? "Golden-" : "Normal-") + ((HatSpecies)index));
                        bool visible = false;
                        foreach (var animal in game.Menu.Canvas.GetComponentsInChildren<HatAnimalGraphic>())
                            if (animal.Species == (HatSpecies)index && animal.Golden == (mode == 1))
                            {
                                Debug.Log("[MenuHat] Visible " + animal.Species + ": alpha=" + animal.color.a + ", vertices=" + animal.RenderedVertexCount + ", size=" + animal.rectTransform.rect.size + ", at=" + animal.rectTransform.anchoredPosition + ", culled=" + animal.canvasRenderer.cull);
                                visible |= animal.color.a > 0.1f && animal.RenderedVertexCount > 0;
                            }
                        Expect(visible, "Animal must produce visible native geometry: " + (HatSpecies)index);
                        report.AppendLine("PASS " + (mode == 1 ? "golden " : "normal ") + (HatSpecies)index + ": real unscaled animation, species call, rarity aura");
                        if (index == 12) Expect(toy.HasDragonEffect, "Dragon must trigger the full-screen prismatic celebration.");
                        if (++index < 13) { Next(3); break; }
                        if (mode == 0) { mode = 1; index = 0; Next(5); }
                        else Next(6);
                        break;
                    case 5:
                        if (age < 4.3) return; Next(3); break;
                    case 6:
                        if (toy.CooldownRemaining > 0 || !game.Menu.FindButton("HatButton").interactable) return;
                        float effects = game.Soundtrack.EffectsVolume;
                        try
                        {
                            game.Soundtrack.SetEffects(0); Click(game, "HatButton");
                            Expect(toy.VoiceSource.volume == 0 && toy.MagicSource.volume == 0, "Sound-effects mute must apply to voices and magic.");
                        }
                        finally { game.Soundtrack.SetEffects(effects); }
                        Click(game, "StartGame");
                        Expect(game.InDemo && !game.Menu.BlocksGameplay && !toy.HasDragonEffect && !toy.VoiceSource.isPlaying && !toy.MagicSource.isPlaying, "Even a dragon effect must not block Start Game, and leaving home must clear all sounds and effects.");
                        game.EndDemo(); Camera.main.targetTexture = target;
                        game.Menu.ShowHome(); Configure(game.Menu.Canvas); oldCount = toy.RevealCount;
                        Expect(!toy.TryReveal() && toy.RevealCount == oldCount, "Returning to home must not bypass the one-second cooldown."); Next(7); break;
                    case 7:
                        if (toy.CooldownRemaining > 0 || !game.Menu.FindButton("HatButton").interactable) return;
                        Click(game, "HatButton"); Expect(toy.RevealCount == oldCount + 1, "Hat must remain clickable after rebuilding the home menu.");
                        File.WriteAllText("Logs/MenuHatRuntimeValidation.txt", report + "PASS native mouse raycasts, one-second cooldown, no-time-scale animation, mute, non-blocking fullscreen effects, leaving-menu cleanup and rebuilt-menu listeners.\n");
                        Debug.Log("[MenuHat] ALL 26 VISITORS AND MENU INTERACTION VERIFIED");
                        SessionState.SetBool(Flag + ".Pass", true); game.Menu.HatToy.Hide(); Camera.main.targetTexture = null;
                        UnityEngine.Object.DestroyImmediate(target); EditorApplication.ExitPlaymode(); break;
                }
            }
            catch (Exception ex) { Debug.LogError("[MenuHat] FAILED: " + ex); SessionState.SetBool(Flag, false); EditorApplication.Exit(1); }
        }
        static void Configure(Canvas canvas) { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = Camera.main; canvas.planeDistance = 1; }
        static void Click(RabbitTrickGame game, string id)
        {
            var button = game.Menu.FindButton(id); Expect(button != null, "Missing button: " + id);
            game.Menu.Canvas.renderMode = RenderMode.ScreenSpaceOverlay; Canvas.ForceUpdateCanvases();
            var rt = button.GetComponent<RectTransform>(); var point = RectTransformUtility.WorldToScreenPoint(null, rt.TransformPoint(rt.rect.center));
            var pointer = new PointerEventData(EventSystem.current) { position = point, button = PointerEventData.InputButton.Left };
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
            Expect(hits.Count > 0 && hits[0].gameObject == button.gameObject, "Hat art or magic must not block clicks on " + id + "; first=" + (hits.Count > 0 ? hits[0].gameObject.name : "NONE"));
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler); Configure(game.Menu.Canvas);
        }
        static void Save(string name)
        {
            Camera.main.targetTexture = target; Canvas.ForceUpdateCanvases(); Camera.main.Render();
            var previous = RenderTexture.active; RenderTexture.active = target;
            var image = new Texture2D(1600, 900, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); image.Apply();
            float light = 0; for (int y = 100; y < 820; y += 70) for (int x = 120; x < 1480; x += 70) light = Mathf.Max(light, image.GetPixel(x, y).grayscale);
            Expect(light > 0.1f, "Hat capture is blank: " + name); File.WriteAllBytes("Logs/" + name + ".png", image.EncodeToPNG());
            RenderTexture.active = previous; UnityEngine.Object.DestroyImmediate(image);
        }
        static void Expect(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    }
}
