using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEngine.InputSystem;
#endif

namespace Sleight
{
    // GDD §17：单幕宿主。唯一的 Update 在这里，按 §17.2 的顺序驱动其它组件。
    public class ActRunner : MonoBehaviour
    {
        public float presentWindow = 1.5f;
        public float earlyPenaltyPerSec = 25f;
        public float failDuration = 1.0f;

        public GameFlow flow;
        public Spotlight spotlight;
        public PlayerController player;
        public HudAudio hud;
        public Transform propsRoot;
        public PropInteractable propPrefab;   // TODO: 按类型换成 P_Prop_Box / P_Prop_Cloth / P_Prop_Mirror

        ActData data;
        ActState state;
        readonly List<PropInteractable> props = new List<PropInteractable>();
        bool locked;

        void OnEnable()
        {
            spotlight.LightSettled += OnLightSettled;
            player.PerformStarted += OnPerformStarted;
            player.PerformCompleted += OnPerformCompleted;
            player.InteractCompleted += OnInteractCompleted;
            player.Blocked += hud.PlayBlocked;
        }

        void OnDisable()
        {
            spotlight.LightSettled -= OnLightSettled;
            player.PerformStarted -= OnPerformStarted;
            player.PerformCompleted -= OnPerformCompleted;
            player.InteractCompleted -= OnInteractCompleted;
            player.Blocked -= hud.PlayBlocked;
        }

        public void Begin(ActData d)
        {
            StopAllCoroutines();
            data = d;
            locked = false;
            state = new ActState();

            foreach (var p in props) Destroy(p.gameObject);
            props.Clear();
            foreach (var spec in d.props)
            {
                if (propPrefab == null) break;
                var p = Instantiate(propPrefab, propsRoot);
                p.name = spec.slot;
                p.Init(spec);
                props.Add(p);
                state.propStates[spec.slot] = spec.initial;
            }

            player.ResetTo(d.playerStart, props);

            if (d.lights != null && d.lights.Length > 0) spotlight.ResetTo(d.lights[0]);
            else spotlight.Stop();

            // TODO: 结局幕（§14.4）的演出流程。
        }

        void Update()
        {
            if (data == null || locked || data.isEnding) return;
            float dt = Time.deltaTime;

            player.Tick(dt, spotlight.ActTime);   // 1. 输入 → 移动/动作（2. 道具在 InteractCompleted 里翻）
            spotlight.Tick(dt);                    // 3. 推进时间轴，落定时发 LightSettled

            state.time = spotlight.ActTime;        // 4. 幕计时
            if (state.time >= data.actTimeLimit) Fail(FailReason.Timeout);

            spotlight.SetSuspicion(state.suspicion);   // 5. 反馈
            hud.SetHeartbeat(state.suspicion);

#if UNITY_EDITOR
            DebugHotkeys();
#endif
        }

        // 只在光落到目标区那一刻判定；落到另一区是玩家的暗窗开始。
        void OnLightSettled(Zone zone)
        {
            if (locked || zone != data.TargetZone) return;
            TryJudge();
        }

        public void TryJudge()
        {
            var v = Judgement.Judge(data, state, player.Snapshot(data.TargetZone), state.suspicion);
            if (v.passed) { locked = true; flow.OnActPassed(); }
            else Fail(v.reason);
        }

        void OnPerformStarted()
        {
            state.armed = false;   // §11.2：重按会清空 armed 重算
        }

        // §17.4：唯一一处写 suspicion 的地方。
        void OnPerformCompleted()
        {
            float T = spotlight.NextArrival();
            float c = spotlight.ActTime;
            if (c >= T - presentWindow && c <= T)
            {
                state.armed = true;
                state.lastPresentAt = c;
                hud.PlayReveal();
                hud.ShowEye(data.TargetZone, 0.6f);
            }
            else if (c < T - presentWindow)
            {
                state.suspicion += earlyPenaltyPerSec * (T - presentWindow - c);
            }
        }

        void OnInteractCompleted(PropInteractable p)
        {
            p.Toggle();
            state.propStates[p.slot] = p.State;
        }

        public void Fail(FailReason reason)
        {
            if (locked) return;
            locked = true;
            Debug.Log($"[ActRunner] {data.actId} 露馅：{reason}");
            StartCoroutine(PlayFail());
        }

        // §12.2：铃 → 闪白 1 帧 → 全静音 0.35s → 本幕重置，全程 ≤ 1.0s。
        IEnumerator PlayFail()
        {
            player.SetCaught();
            hud.PlayBell();
            hud.FlashWhite();
            yield return new WaitForSeconds(0.1f);
            hud.MuteFor(0.35f);
            yield return new WaitForSeconds(failDuration - 0.1f);
            flow.RestartAct();
        }

#if UNITY_EDITOR
        // §18.8 调试热键，只在 Editor 生效。
        void DebugHotkeys()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.f1Key.wasPressedThisFrame) flow.OnActPassed();
            if (kb.f2Key.wasPressedThisFrame) Fail(FailReason.PresentMissing);
            if (kb.f3Key.wasPressedThisFrame) flow.RestartAct();
            if (kb.f4Key.wasPressedThisFrame)
            {
                var states = string.Join(", ", state.propStates);
                Debug.Log($"[ActRunner] {data.actId} t={state.time:F2} T={spotlight.NextArrival():F2} armed={state.armed} suspicion={state.suspicion:F1} props=[{states}]");
            }
        }
#endif
    }
}
