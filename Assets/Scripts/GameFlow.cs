using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Sleight
{
    // GDD §16：全局流程。Boot → 关 1~3 各三幕 → 结局 → 标题。
    public class GameFlow : MonoBehaviour
    {
        public float actGap = 0.6f;
        public float bootDelay = 0.5f;

        public ActRunner runner;
        public HudAudio hud;

        ActData[] acts;
        int current = -1;
        bool advancing;

        IEnumerator Start()
        {
            acts = ActData.LoadAll();
            yield return new WaitForSeconds(bootDelay);
            if (acts.Length > 0) LoadAct(acts[0]);
        }

        public void LoadAct(ActData d)
        {
            current = System.Array.IndexOf(acts, d);
            advancing = false;
            hud.SetActDots(Mathf.Max(0, d.index - 1));
            runner.Begin(d);
            if (d.isEnding) StartCoroutine(PlayEnding());
        }

        public void OnActPassed()
        {
            if (advancing) return;
            advancing = true;
            StartCoroutine(Advance());
        }

        public void RestartAct()
        {
            if (current >= 0) LoadAct(acts[current]);
        }

        IEnumerator Advance()
        {
            var finished = acts[current];
            hud.SetActDots(finished.index);
            yield return new WaitForSeconds(actGap);

            int next = current + 1;
            if (next >= acts.Length) yield break;

            // 关完成：0.3s 淡黑，在全黑那一刻换幕。
            bool newLevel = acts[next].level != finished.level;
            if (newLevel) yield return hud.FadeBlackTo(1f, 0.15f);
            LoadAct(acts[next]);
            if (newLevel && !acts[next].isEnding) yield return hud.FadeBlackTo(0f, 0.15f);
        }

        // §14.4：进场 2.0s（灯逐渐灭）→ 停 3.0s → 眼睛消失 → 淡出 1.0s → 标题。结局不接受输入。
        IEnumerator PlayEnding()
        {
            yield return hud.FadeBlackTo(0f, 0.15f);
            hud.SetEyeVisible(true, new Vector3(5f, 4.6f, 0f));
            yield return hud.FadeBlackTo(0.6f, 2.0f);
            yield return new WaitForSeconds(3.0f);   // TODO: 他在走动的脚步声
            hud.SetEyeVisible(false, Vector3.zero);
            yield return hud.FadeBlackTo(1f, 1.0f);

            // 标题卡（纯图形，无字），之后按空格或 E 从头再来。
            hud.SetTitleVisible(true);
            while (true)
            {
                var kb = Keyboard.current;
                if (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame)) break;
                yield return null;
            }
            hud.SetTitleVisible(false);
            LoadAct(acts[0]);
            yield return hud.FadeBlackTo(0f, 0.25f);
        }
    }
}
