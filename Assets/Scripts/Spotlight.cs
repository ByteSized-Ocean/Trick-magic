using System;
using UnityEngine;

namespace Sleight
{
    // GDD §9：单束光。时间只由这里算一次（§17.2），ActRunner 不许自己算 NextArrival。
    // 循环：落定在 zone → dwell 秒 → transition 秒缓动到另一区 → 落定 → …
    [RequireComponent(typeof(SpriteRenderer))]
    public class Spotlight : MonoBehaviour
    {
        public float width = 4.5f;

        public event Action<Zone> LightSettled;

        public float ActTime { get; private set; }

        LightSpec spec;
        Zone startZone;
        int arrivals;           // 已发出的落定次数
        SpriteRenderer sr;
        bool running;

        float Period => spec.dwell + spec.transition;

        void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
        }

        public void ResetTo(LightSpec s)
        {
            spec = s;
            startZone = s.startZone == "B" ? Zone.B : Zone.A;
            ActTime = 0f;
            arrivals = 0;
            running = true;
            gameObject.SetActive(true);
            ApplyVisual();
        }

        public void Stop()
        {
            running = false;
            gameObject.SetActive(false);
        }

        // 第 n 次落定（n 从 1 起）的时刻。
        float ArrivalAt(int n) => spec.startDelay + n * Period;

        public float NextArrival() => ArrivalAt(arrivals + 1);

        public bool IsSettled()
        {
            float p = Phase();
            return p < spec.dwell;
        }

        public Zone CurrentZone() => arrivals % 2 == 0 ? startZone : Stage.Other(startZone);

        public bool Covers(float x) => IsSettled() && Stage.ZoneOf(x) == CurrentZone();

        // 亮度 = 0.55 + 0.45 * (suspicion/100)（§5.3）
        public void SetSuspicion(float suspicion)
        {
            if (sr == null) return;
            var c = sr.color;
            c.a = 0.55f + 0.45f * Mathf.Clamp01(suspicion / 100f);
            sr.color = c;
            // TODO: 满怀疑时 2px 抖动
        }

        // 由 ActRunner 在每帧第 3 步调用（§17.2）。
        public void Tick(float dt)
        {
            if (!running || spec == null) return;
            ActTime += dt;
            while (ActTime >= NextArrival())
            {
                arrivals++;
                LightSettled?.Invoke(CurrentZone());
            }
            ApplyVisual();
        }

        float Phase()
        {
            float t = ActTime - spec.startDelay;
            if (t < 0f) return 0f;
            return t - (arrivals * Period);
        }

        void ApplyVisual()
        {
            float x;
            float p = Phase();
            Zone from = CurrentZone();
            if (p < spec.dwell)
            {
                x = Stage.ZoneCenter(from);
            }
            else
            {
                float k = Mathf.SmoothStep(0f, 1f, (p - spec.dwell) / spec.transition);
                x = Mathf.Lerp(Stage.ZoneCenter(from), Stage.ZoneCenter(Stage.Other(from)), k);
            }
            var pos = transform.position;
            transform.position = new Vector3(x, pos.y, pos.z);
        }
    }
}
