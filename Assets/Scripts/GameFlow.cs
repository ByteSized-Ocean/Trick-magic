using System.Collections;
using UnityEngine;

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
            // TODO: 追光闪一下（0.15s 变亮）。
            yield return new WaitForSeconds(actGap);

            int next = current + 1;
            if (next >= acts.Length)
            {
                // TODO: 标题 → 结束（可重开）。
                yield break;
            }
            if (acts[next].level != finished.level)
                yield return hud.FadeBlack(0.3f);
            LoadAct(acts[next]);
        }
    }
}
