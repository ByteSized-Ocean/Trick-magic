using UnityEngine;
using UnityEngine.InputSystem;

namespace Sleight.RabbitTrick
{
    // Debug 菜单：F10 打开 / 关上，点按钮直接跳到某一关，或者把当前关判成功（走一遍落幕）。
    // 只在编辑器和 Development Build 里有，正式包里不会出现。
    public sealed class TrickDebugMenu : MonoBehaviour
    {
        public RabbitTrickGame game;
        bool open;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.f10Key.wasPressedThisFrame) open = !open;
        }

        void OnGUI()
        {
            if (!open || game == null) return;
            float scale = Mathf.Max(1f, Screen.height / 720f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            GUILayout.BeginArea(new Rect(Screen.width / scale - 330f, 260f, 310f, 390f), GUI.skin.box);
            GUILayout.Label("DEBUG  (F10 to close)");
            GUILayout.Label("Now: " + Ordinal(game.Level + 1) + " TRICK  /  step " + (game.StepIndex + 1));
            for (int i = 0; i < RabbitTrickGame.LevelCount; i++)
            {
                if (GUILayout.Button("Jump to " + Ordinal(i + 1) + " trick", GUILayout.Height(28)))
                    game.JumpToLevel(i);
            }
            GUILayout.Space(6);
            if (GUILayout.Button("Win this trick (curtain call)", GUILayout.Height(28))) game.DebugWinLevel();
            if (GUILayout.Button("Restart this trick", GUILayout.Height(28))) game.ResetTrick();
            GUILayout.EndArea();
        }
#endif

        static string Ordinal(int n) => n + (n == 1 ? "st" : n == 2 ? "nd" : n == 3 ? "rd" : "th");
    }
}
