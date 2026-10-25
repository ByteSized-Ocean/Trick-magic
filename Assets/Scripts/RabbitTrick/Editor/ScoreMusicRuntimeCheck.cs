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
    public static class ScoreMusicRuntimeCheck
    {
        const string Flag = "Sleight.ScoreMusicCheck";
        static int phase, lastFrame;
        static double began, phaseAt;
        static float frozenTime;
        static int earned, scoredLevel;
        static TrickDemo routine;
        static RenderTexture target;
        static Keyboard keyboard;
        static StageCurtains curtains;
        static ScoreMusicRuntimeCheck() { EditorApplication.update += Poll; EditorApplication.playModeStateChanged += State; }

        public static void Prepare()
        {
            ValidateModel();
            EditorSceneManager.OpenScene("Assets/Scenes/Game.unity");
            var game = UnityEngine.Object.FindAnyObjectByType<RabbitTrickGame>();
            game.view.EnsureScoreHud(); EditorUtility.SetDirty(game.view);
            EditorSceneManager.SaveScene(game.gameObject.scene);
            foreach (string path in Directory.GetFiles("Assets/Resources/StageAudio", "*.ogg"))
            {
                var importer = AssetImporter.GetAtPath(path.Replace('\\', '/')) as AudioImporter;
                if (importer == null) throw new InvalidOperationException("Missing audio importer: " + path);
                var settings = importer.defaultSampleSettings;
                settings.loadType = path.Contains("TheatreApplause") ? AudioClipLoadType.DecompressOnLoad : AudioClipLoadType.Streaming;
                settings.compressionFormat = AudioCompressionFormat.Vorbis; settings.quality = 0.7f;
                importer.defaultSampleSettings = settings; importer.SaveAndReimport();
            }
            AssetDatabase.SaveAssets(); Debug.Log("[ScoreMusic] SCORING MODEL AND SCENE UPGRADE VERIFIED");
        }

        static void ValidateModel()
        {
            string prefix = "Sleight.ScoreTest." + Guid.NewGuid() + ".";
            try
            {
                var score = new ShowScore(prefix); score.LoadRecords();
                Expect(ShowScore.FromTime(-3) == 0 && ShowScore.FromTime(0) == 0 && ShowScore.FromTime(23.456f) == 2345, "Time bonus must floor fractional seconds and clamp below zero.");
                Expect(ShowScore.FromTime(float.NaN) == 0 && ShowScore.FromTime(float.PositiveInfinity) == 0, "Invalid times must never award points.");
                score.RecordWin(0, 0, 23.456f); score.RecordWin(0, 0, 40);
                Expect(score.Total == 2345 && score.CompletedCount == 1, "One win must award exactly once.");
                var reloaded = new ShowScore(prefix); reloaded.LoadRecords();
                Expect(reloaded.BestForTrick(0) == 2345, "Act records must survive a reload.");
                score.RecordWin(1, 3, 20); score.ClearLevel(1);
                Expect(score.Total == 2345 && score.CompletedCount == 1, "Restart must preserve earlier acts without keeping the restarted award.");
                score.FinishShow(); Expect(score.BestFullShow == 0, "Partial shows must not become full-show records.");
                score.BeginShow();
                for (int i = 0; i < RabbitTrickGame.LevelCount; i++) score.RecordWin(i, RabbitTrickGame.PlayOrder[i], 10 + i);
                score.FinishShow(); Expect(score.Total == 14500 && score.BestFullShow == 14500 && score.CompletedCount == 10, "Full show must sum ten wins.");
                reloaded.LoadRecords(); Expect(reloaded.BestFullShow == 14500, "Full-show record must persist.");
                score.BeginShow(); Expect(score.Total == 0 && score.CompletedCount == 0 && score.BestForTrick(0) == 2345, "A fresh show must reset awards and retain records.");
                Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/ScoreModelValidation.txt", "PASS fractional time conversion, invalid inputs, single award, retries, accumulated score, partial shows, full-show record and persistence.\n");
            }
            finally
            {
                for (int i = 0; i < RabbitTrickGame.LevelCount; i++) PlayerPrefs.DeleteKey(prefix + "Trick." + i);
                PlayerPrefs.DeleteKey(prefix + "FullShow"); PlayerPrefs.Save();
            }
        }

        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Game.unity");
            SessionState.SetBool(Flag, true); SessionState.SetBool(Flag + ".Pass", false); EditorApplication.EnterPlaymode();
        }
        static void State(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Flag, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                phase = 0; lastFrame = -1; began = phaseAt = EditorApplication.timeSinceStartup;
                foreach (var device in InputSystem.devices) if (device is Keyboard) InputSystem.DisableDevice(device);
                keyboard = InputSystem.AddDevice<Keyboard>(); InputSystem.onBeforeUpdate += Input;
                Application.runInBackground = true;
            }
            if (state == PlayModeStateChange.EnteredEditMode)
            { SessionState.SetBool(Flag, false); InputSystem.onBeforeUpdate -= Input; if (Application.isBatchMode) EditorApplication.Exit(SessionState.GetBool(Flag + ".Pass", false) ? 0 : 1); }
        }
        static void Input() { if (keyboard != null && keyboard.added) { InputSystem.QueueStateEvent(keyboard, new KeyboardState()); keyboard.MakeCurrent(); } }
        static void Next(int next) { phase = next; phaseAt = EditorApplication.timeSinceStartup; }
        static void Poll()
        {
            if (!SessionState.GetBool(Flag, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating || lastFrame == Time.frameCount) return;
            lastFrame = Time.frameCount;
            var game = UnityEngine.Object.FindAnyObjectByType<RabbitTrickGame>(); if (game == null || game.Menu == null) return;
            double now = EditorApplication.timeSinceStartup, age = now - phaseAt;
            try
            {
                if (now - began > 100) throw new InvalidOperationException("Score runtime timed out at phase " + phase);
                switch (phase)
                {
                    case 0:
                        if (age < 1) return;
                        game.Scores.SaveRecords = false; game.StartShow();
                        target = new RenderTexture(1600, 900, 24); target.Create(); Camera.main.targetTexture = target;
                        Configure(game.view.HudCanvas.GetComponent<Canvas>()); Configure(game.Menu.Canvas);
                        curtains = game.Curtains; game.Curtains = null; Next(1); break;
                    case 1:
                        if (age < 3.3) return;
                        Expect(game.InDemo && game.Patience == 1 && game.Scores.Total == 0 && !game.ScoreAwarded, "Demo must not consume time or earn points.");
                        game.EndDemo(); Camera.main.targetTexture = target; Next(2); break;
                    case 2:
                        if (age < 1.1) return;
                        Expect(game.RemainingSeconds < 49.8f && game.TimeBonus == ShowScore.FromTime(game.RemainingSeconds), "Live time bonus must track the actual countdown.");
                        game.Menu.ShowPause(); frozenTime = game.RemainingSeconds; Next(3); break;
                    case 3:
                        if (age < 0.5) return;
                        Expect(game.RemainingSeconds == frozenTime && game.Scores.Total == 0, "Pause must freeze time and scoring."); Save("Score-Pause");
                        game.Menu.Resume(); game.Rules.Expose("The audience spotted the secret."); Next(4); break;
                    case 4:
                        if (age < 0.3) return;
                        Expect(game.IsFailed && game.Scores.Total == 0 && !game.ScoreAwarded, "Failure must award zero points.");
                        game.RestartLevel(); Expect(game.Patience == 1, "Retry must restore the act timer.");
                        // Use a hand-only choreography whose light targets come from normal gameplay.
                        // The older rabbit demo directs its own scripted spotlight and belongs in demo mode.
                        game.BeginLevel(RabbitTrickGame.PositionOf(5), false); scoredLevel = game.Level;
                        routine = new TrickDemo(game); game.Trick.AddDemo(routine); Next(5); break;
                    case 5:
                        Expect(!game.IsFailed, "Card performance failed: " + game.FailureReason);
                        if (!game.IsFinished) { routine.Tick(Mathf.Min(Time.deltaTime, 0.05f)); break; }
                        if (!game.ScoreAwarded) return;
                        earned = game.Scores.Total; frozenTime = game.RemainingSeconds;
                        Expect(earned > 0 && earned == game.TimeBonus && game.Scores.CompletedCount == 1, "Normal player loop must award the frozen remaining time on success.");
                        Next(6); break;
                    case 6:
                        if (age < 0.5) return;
                        Expect(game.Scores.Total == earned && game.RemainingSeconds == frozenTime, "Win must freeze time and never award twice."); Save("Score-ActResult");
                        game.BeginLevel(1, false); game.RestartLevel();
                        Expect(game.Scores.Total == earned && game.Scores.Completed(scoredLevel), "Restarting the next act must preserve the previous score.");
                        game.StartShow(9); Expect(game.Scores.Total == 0 && game.Scores.CompletedCount == 0, "Starting a selected act must start a fresh show.");
                        game.EndDemo(); Camera.main.targetTexture = target; game.Menu.ShowPause();
                        for (int i = 0; i < RabbitTrickGame.LevelCount; i++) game.Scores.RecordWin(i, RabbitTrickGame.PlayOrder[i], 21.25f + i);
                        game.Scores.FinishShow(); game.Menu.ShowResults(); Next(7); break;
                    case 7:
                        if (age < 0.3) return;
                        Expect(game.Menu.Page == StageMenu.ScreenPage.Results && Time.timeScale == 0 && game.Scores.CompletedCount == 10, "Show result screen must retain all ten awards.");
                        Expect(game.Menu.FindButton("ReplayShow") != null && game.Menu.FindButton("ResultsLevels") != null && game.Menu.FindButton("ResultsHome") != null, "Result navigation must be available.");
                        Save("Score-ShowResults"); game.Menu.FindButton("ResultsLevels").onClick.Invoke(); Next(8); break;
                    case 8:
                        if (age < 0.3) return;
                        Save("Score-LevelRecords"); game.Menu.ShowResults(); game.Menu.FindButton("ReplayShow").onClick.Invoke();
                        Expect(game.InDemo && game.Level == 0 && game.Scores.Total == 0 && game.Scores.CompletedCount == 0, "Play Again must reset the full show.");
                        game.Curtains = curtains;
                        Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/ScoreRuntimeValidation.txt", "PASS live remaining-time bonus, demo exclusion, pause freeze, zero on failure, normal-loop victory award, frozen one-time score, retry preservation, fresh show, ten-act summary, records and result navigation.\n");
                        Debug.Log("[ScoreMusic] SCORE RUNTIME VERIFIED"); SessionState.SetBool(Flag + ".Pass", true); Camera.main.targetTexture = null;
                        UnityEngine.Object.DestroyImmediate(target); EditorApplication.ExitPlaymode(); break;
                }
            }
            catch (Exception ex) { Debug.LogError("[ScoreMusic] FAILED: " + ex); SessionState.SetBool(Flag, false); EditorApplication.Exit(1); }
        }
        static void Configure(Canvas canvas) { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = Camera.main; canvas.planeDistance = 1; }
        static void Save(string name)
        {
            var previous = RenderTexture.active; RenderTexture.active = target;
            var image = new Texture2D(1600, 900, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); image.Apply();
            float brightest = 0;
            for (int y = 100; y < 820; y += 60) for (int x = 120; x < 1480; x += 60) brightest = Mathf.Max(brightest, image.GetPixel(x, y).grayscale);
            Expect(brightest > 0.1f, "Captured score screen must contain a rendered frame: " + name);
            File.WriteAllBytes("Logs/" + name + ".png", image.EncodeToPNG()); RenderTexture.active = previous; UnityEngine.Object.DestroyImmediate(image);
        }
        static void Expect(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    }
}
