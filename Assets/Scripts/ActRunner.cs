using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEngine.InputSystem;
#endif

namespace Sleight
{
    public enum ActPhase { Preview, Setup, Curtain, Show, Done }

    // GDD §17 / §0.2：单幕宿主。唯一的 Update 在这里，按 §17.2 的顺序驱动其它组件。
    // 一幕分两段：预览 → 搭台（手，限时）→ 开幕 → 表演（追光）。露馅只重来当前这一段（§0.4）。
    public class ActRunner : MonoBehaviour
    {
        public float presentWindow = 1.5f;
        public float earlyPenaltyPerSec = 25f;
        public float failDuration = 1.0f;
        public float readyDelay = 1.0f;   // 道具全部到位、手里没东西，停这么久自动开幕

        public GameFlow flow;
        public Spotlight spotlight;
        public PlayerController player;
        public HandController hand;
        public Curtain curtain;
        public PreviewCard preview;
        public HudAudio hud;
        public Transform propsRoot;
        public PropInteractable propPrefab;     // 色块占位，按类型的预制体缺图时用它
        public PropInteractable boxPrefab;
        public PropInteractable clothPrefab;
        public PropInteractable mirrorPrefab;
        public GameObject markerPrefab;          // P_Marker_Assist
        public SpriteRenderer eyePrefab;         // P_EyeFlash

        public ActPhase Phase { get; private set; }

        ActData data;
        ActState state;
        readonly List<PropInteractable> props = new List<PropInteractable>();
        readonly List<PropSpec> specs = new List<PropSpec>();          // 与 props 一一对应
        readonly List<GameObject> markers = new List<GameObject>();
        readonly Dictionary<PropInteractable, string> showStartStates = new Dictionary<PropInteractable, string>();
        float setupTime;
        float readySince = -1f;
        bool locked;
        bool timedOut;

        static readonly Color MarkerColor = new Color32(0xD9, 0xCB, 0xB0, 0x59);
        static readonly Color EyeColor = new Color32(0xD9, 0xCB, 0xB0, 0xFF);

        void Awake()
        {
            if (hud.eye == null && eyePrefab != null)
            {
                hud.eye = Instantiate(eyePrefab, transform);
                hud.eye.gameObject.SetActive(false);
            }
            if (hud.eye == null)
            {
                hud.eye = MakeBlock("EyeFlash", transform, "Light", 10, EyeColor);
                hud.eye.transform.localScale = new Vector3(1.0f, 0.625f, 1f);   // §19.3 eye_flash 64×40
                hud.eye.gameObject.SetActive(false);
            }
        }

        void OnEnable()
        {
            spotlight.LightSettled += OnLightSettled;
            player.PerformStarted += OnPerformStarted;
            player.PerformCompleted += OnPerformCompleted;
            player.InteractCompleted += OnInteractCompleted;
            player.Blocked += hud.PlayBlocked;
            player.Blocked += player.FlashBlocked;
            if (hand != null)
            {
                hand.Toggled += OnHandToggled;
                hand.Dropped += OnHandDropped;
            }
        }

        void OnDisable()
        {
            spotlight.LightSettled -= OnLightSettled;
            player.PerformStarted -= OnPerformStarted;
            player.PerformCompleted -= OnPerformCompleted;
            player.InteractCompleted -= OnInteractCompleted;
            player.Blocked -= hud.PlayBlocked;
            player.Blocked -= player.FlashBlocked;
            if (hand != null)
            {
                hand.Toggled -= OnHandToggled;
                hand.Dropped -= OnHandDropped;
            }
        }

        public void Begin(ActData d)
        {
            StopAllCoroutines();
            data = d;
            locked = false;

            foreach (var p in props) Destroy(p.gameObject);
            props.Clear();
            specs.Clear();
            Spawn(d.props, false);
            Spawn(d.decoys, true);

            foreach (var m in markers) Destroy(m);
            markers.Clear();
            if (d.requireAssistant) SpawnMarker(d.TargetZone);

            if (preview != null) preview.Clear();
            bool hasSetup = d.setupTime > 0f && hand != null && curtain != null;
            if (hasSetup)
            {
                StartCoroutine(PreviewThenSetup(true));
            }
            else
            {
                if (curtain != null) curtain.SetUp();
                StartShow();
            }
        }

        void Spawn(PropSpec[] list, bool decoy)
        {
            if (list == null) return;
            foreach (var spec in list)
            {
                var prefab = PrefabFor(spec.type);
                if (prefab == null) break;
                var p = Instantiate(prefab, propsRoot);
                p.name = decoy ? "Decoy_" + spec.type : spec.slot;
                p.Init(spec, decoy);
                props.Add(p);
                specs.Add(spec);
            }
        }

        void SpawnMarker(Zone zone)
        {
            // §19.2 prop_marker_assist：平铺在地板上的浅色标记，脚下那一行。
            GameObject marker;
            if (markerPrefab != null)
            {
                marker = Instantiate(markerPrefab, propsRoot);
                marker.transform.position = new Vector3(Stage.AssistX(zone), Stage.Floor + 0.005f, 0f);
            }
            else
            {
                marker = MakeBlock(zone + ".assist", propsRoot, "Stage", 0, MarkerColor).gameObject;
                marker.transform.position = new Vector3(Stage.AssistX(zone), Stage.Floor + 0.005f, -0.4f);
                marker.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                marker.transform.localScale = new Vector3(1.0f, 0.8f, 1f);
            }
            markers.Add(marker);
        }

        // ------------------------------------------------------------ 预览 + 搭台

        IEnumerator PreviewThenSetup(bool withPreview)
        {
            for (int i = 0; i < props.Count; i++) props[i].ResetTo(specs[i]);
            spotlight.Stop();
            curtain.SetDown();
            player.actionsEnabled = false;
            player.ResetTo(data.playerStart, props);
            hand.Hide();

            if (withPreview && preview != null)
            {
                Phase = ActPhase.Preview;
                yield return preview.Play(data, PrefabFor);
            }

            Phase = ActPhase.Setup;
            setupTime = 0f;
            readySince = -1f;
            hand.Show(props, SlotFree);
        }

        bool SlotFree(string slot)
        {
            foreach (var p in props)
                if (p.slot == slot && !p.Held) return false;
            return true;
        }

        void TickSetup(float dt)
        {
            setupTime += dt;
            player.Tick(dt, setupTime);
            hand.Tick(dt, setupTime);
            curtain.SetProgress(setupTime / data.setupTime);

            // 全部到位、手里空着，停 readyDelay 秒就提前开幕；时间到也开幕。
            bool ready = hand.Held == null && SetupComplete();
            if (ready && readySince < 0f) readySince = setupTime;
            if (!ready) readySince = -1f;
            if (setupTime >= data.setupTime || (ready && setupTime - readySince >= readyDelay))
                StartCoroutine(OpenCurtain());
        }

        // §0.7 开幕检查：每个目标道具都有同类型的道具在它的槽位上，干扰道具不在任何槽位上。
        bool SetupComplete()
        {
            foreach (var spec in data.props)
            {
                bool found = false;
                foreach (var p in props)
                    if (!p.isDecoy && p.type == spec.type && p.slot == spec.slot) found = true;
                if (!found) return false;
            }
            foreach (var p in props)
                if (p.isDecoy && !string.IsNullOrEmpty(p.slot)) return false;
            return true;
        }

        IEnumerator OpenCurtain()
        {
            Phase = ActPhase.Curtain;
            hand.Hide();
            player.actionsEnabled = true;
            yield return curtain.Raise();
            if (!SetupComplete())
            {
                yield return PlayFail(FailReason.SetupIncomplete);
                yield return curtain.Lower();
                StartCoroutine(PreviewThenSetup(false));
                yield break;
            }
            StartShow();
        }

        void OnHandToggled(PropInteractable p) => p.Toggle();

        void OnHandDropped(PropInteractable p, bool snapped)
        {
            if (snapped) hud.PlaySnap();
        }

        // ------------------------------------------------------------ 表演

        void StartShow()
        {
            showStartStates.Clear();
            foreach (var p in props) showStartStates[p] = p.State;
            RestartShow();
        }

        // 表演露馅只重来表演：道具位置保留搭台结果，状态回开幕时的样子（§0.4）。
        void RestartShow()
        {
            locked = false;
            timedOut = false;
            state = new ActState();
            foreach (var p in props)
            {
                if (showStartStates.TryGetValue(p, out var s)) p.SetState(s);
            }
            RebuildPropStates();
            player.actionsEnabled = true;
            player.ResetTo(data.playerStart, props);
            if (data.lights != null && data.lights.Length > 0) spotlight.ResetTo(data.lights[0]);
            else spotlight.Stop();
            Phase = data.isEnding ? ActPhase.Done : ActPhase.Show;
        }

        // Judgement 按槽位查状态：只把在槽位上的非干扰道具记进去。
        void RebuildPropStates()
        {
            state.propStates.Clear();
            foreach (var p in props)
                if (!p.isDecoy && !string.IsNullOrEmpty(p.slot)) state.propStates[p.slot] = p.State;
        }

        void Update()
        {
            if (data == null || locked) return;
            float dt = Time.deltaTime;
#if UNITY_EDITOR
            DebugHotkeys();
#endif

            if (Phase == ActPhase.Setup)
            {
                TickSetup(dt);
                return;
            }
            if (Phase != ActPhase.Show) return;

            player.Tick(dt, spotlight.ActTime);   // 1. 输入 → 移动/动作（2. 道具在 InteractCompleted 里翻）
            spotlight.Tick(dt);                    // 3. 推进时间轴，落定时发 LightSettled

            state.time = spotlight.ActTime;        // 4. 幕计时
            if (!timedOut && state.time >= data.actTimeLimit)
            {
                timedOut = true;
                Fail(FailReason.Timeout);
            }

            spotlight.SetSuspicion(state.suspicion);   // 5. 反馈
            hud.SetHeartbeat(state.suspicion);
        }

        // 只在光落到目标区那一刻判定；落到另一区是玩家的暗窗开始。
        void OnLightSettled(Zone zone)
        {
            if (locked || Phase != ActPhase.Show || zone != data.TargetZone) return;
            TryJudge();
        }

        public void TryJudge()
        {
            var v = Judgement.Judge(data, state, player.Snapshot(data.TargetZone), state.suspicion);
            if (v.passed)
            {
                locked = true;
                Phase = ActPhase.Done;
                StartCoroutine(spotlight.Pulse(0.15f));
                flow.OnActPassed();
            }
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
            RebuildPropStates();
        }

        PropInteractable PrefabFor(string type)
        {
            PropInteractable p = null;
            if (type == "box") p = boxPrefab;
            else if (type == "cloth") p = clothPrefab;
            else if (type == "mirror") p = mirrorPrefab;
            return p != null ? p : propPrefab;
        }

        // 占位色块，复用道具预制体上的白图和材质。
        SpriteRenderer MakeBlock(string name, Transform parent, string layer, int order, Color color)
        {
            var template = propPrefab.GetComponent<SpriteRenderer>();
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = template.sprite;
            sr.sharedMaterial = template.sharedMaterial;
            sr.sortingLayerName = layer;
            sr.sortingOrder = order;
            sr.color = color;
            return sr;
        }

        // 表演段露馅：铃 → 闪白 → 静音 → 只重来表演。
        public void Fail(FailReason reason)
        {
            if (locked || Phase != ActPhase.Show) return;
            locked = true;
            StartCoroutine(FailShow(reason));
        }

        IEnumerator FailShow(FailReason reason)
        {
            yield return PlayFail(reason);
            RestartShow();
        }

        // §12.2：铃 → 闪白 1 帧 → 全静音 0.35s，全程 failDuration。
        IEnumerator PlayFail(FailReason reason)
        {
            Debug.Log($"[ActRunner] {data.actId} 露馅：{reason}");
            player.SetCaught();
            hud.PlayBell();
            hud.FlashWhite();
            yield return new WaitForSeconds(0.1f);
            hud.MuteFor(0.35f);
            yield return new WaitForSeconds(failDuration - 0.1f);
        }

#if UNITY_EDITOR
        // §18.8 调试热键，只在 Editor 生效。
        void DebugHotkeys()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.f1Key.wasPressedThisFrame) { locked = true; Phase = ActPhase.Done; flow.OnActPassed(); }
            if (kb.f2Key.wasPressedThisFrame) Fail(FailReason.PresentMissing);
            if (kb.f3Key.wasPressedThisFrame) flow.RestartAct();
            if (kb.f4Key.wasPressedThisFrame && state != null)
            {
                var states = string.Join(", ", state.propStates);
                Debug.Log($"[ActRunner] {data.actId} t={state.time:F2} T={spotlight.NextArrival():F2} armed={state.armed} suspicion={state.suspicion:F1} props=[{states}]");
            }
        }
#endif
    }
}
