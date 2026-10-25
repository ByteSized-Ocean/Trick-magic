using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Sleight.RabbitTrick.EditorTools
{
    // 谢幕的实机检查：进 Play 模式跳到第 10 关，直接触发谢幕，确认魔术师走到中央鞠躬、
    // 大幕落下、黑屏后回到主菜单，并把几帧截图存到 Logs/Finale-*.png 方便目视检查。
    // 批处理：Unity -batchmode -projectPath ... -executeMethod Sleight.RabbitTrick.EditorTools.FinaleRuntimeCheck.Run
    [InitializeOnLoad]
    public static class FinaleRuntimeCheck
    {
        const string Flag = "Sleight.FinaleRuntimeCheck";
        static RenderTexture target;
        static int phase;
        static double began, phaseAt;
        static bool sawBow, sawHalf, sawDown;
        static FinaleRuntimeCheck() { EditorApplication.update += Poll; EditorApplication.playModeStateChanged += State; }

        [MenuItem("Sleight/兔子魔术/Debug/实机检查谢幕")]
        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Game.unity");
            SessionState.SetBool(Flag, true); SessionState.SetBool(Flag + ".Pass", false); EditorApplication.EnterPlaymode();
        }

        static void State(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Flag, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode) { began = phaseAt = EditorApplication.timeSinceStartup; phase = 0; sawBow = sawHalf = sawDown = false; Application.runInBackground = true; }
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                SessionState.SetBool(Flag, false);
                if (Application.isBatchMode) EditorApplication.Exit(SessionState.GetBool(Flag + ".Pass", false) ? 0 : 1);
            }
        }

        static void Poll()
        {
            if (!SessionState.GetBool(Flag, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            var game = UnityEngine.Object.FindAnyObjectByType<RabbitTrickGame>();
            if (game == null || game.Menu == null) return;
            double now = EditorApplication.timeSinceStartup;
            try
            {
                if (now - began > 60) throw new InvalidOperationException("Finale check timed out, phase " + phase);
                if (phase == 0)
                {
                    if (now - phaseAt < 1) return;
                    game.BeginLevel(RabbitTrickGame.LevelCount - 1, false);
                    target = new RenderTexture(1600, 900, 24); target.Create();
                    var hud = game.view.HudCanvas.GetComponent<Canvas>();
                    hud.renderMode = RenderMode.ScreenSpaceCamera; hud.worldCamera = Camera.main; hud.planeDistance = 1;
                    Camera.main.targetTexture = target;
                    game.Hands.MoveBody(game, Vector2.right * (8.5f - game.Hands.BodyPosition.x));   // 从舞台右边开始，看得出他走向中央
                    game.DebugWinLevel();
                    phase = 1; phaseAt = now; return;
                }
                if (phase == 1)
                {
                    Expect(game.InFinale || game.Menu.Page == StageMenu.ScreenPage.Home, "Finale must be running.");
                    if (!sawBow && game.view.FinaleBow > 0.95f)
                    {
                        sawBow = true;
                        Expect(Mathf.Abs(game.Hands.BodyPosition.x - 5f) < 0.05f, "Magician must bow at centre stage, x=" + game.Hands.BodyPosition.x);
                        Save("Finale-Bow");
                    }
                    if (!sawHalf && game.view.FinaleCurtain > 0.5f) { sawHalf = true; Save("Finale-CurtainHalf"); }
                    if (!sawDown && game.view.FinaleCurtain >= 1f) { sawDown = true; Save("Finale-CurtainDown"); }
                    if (game.Menu.Page == StageMenu.ScreenPage.Home && !game.InFinale)
                    {
                        Expect(sawBow && sawHalf && sawDown, $"Finale skipped a stage: bow={sawBow} half={sawHalf} down={sawDown}");
                        Expect(game.view.FinaleCurtain == 0f && game.view.FinaleFade == 0f, "Finale visuals must reset at the menu.");
                        Directory.CreateDirectory("Logs");
                        File.WriteAllText("Logs/FinaleRuntimeValidation.txt", "PASS walk to centre, bow, ribbons, main curtain drop, fade, back to the main menu.\n");
                        Debug.Log("[Finale] CURTAIN CALL VERIFIED");
                        Camera.main.targetTexture = null;
                        SessionState.SetBool(Flag + ".Pass", true);
                        EditorApplication.ExitPlaymode();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("[Finale] FAILED: " + ex.Message);
                SessionState.SetBool(Flag, false);
                if (Application.isBatchMode) EditorApplication.Exit(1); else EditorApplication.ExitPlaymode();
            }
        }

        static void Save(string name)
        {
            var previous = RenderTexture.active; RenderTexture.active = target;
            var image = new Texture2D(1600, 900, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); image.Apply();
            Directory.CreateDirectory("Logs"); File.WriteAllBytes("Logs/" + name + ".png", image.EncodeToPNG());
            RenderTexture.active = previous; UnityEngine.Object.DestroyImmediate(image);
        }

        static void Expect(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    }
}
