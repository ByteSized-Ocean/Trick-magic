using System;
using UnityEngine;

namespace Sleight
{
    // GDD §9：单束光。时间只由这里算一次（§17.2），ActRunner 不许自己算 NextArrival。
    // 循环：落定在 zone → dwell 秒 → transition 秒缓动到另一区 → 落定 → …
    // 画面上可以是一盏真的 Spot Light（3D 舞台），也可以是一张贴图；两者都有时一起调亮度。
    public class Spotlight : MonoBehaviour
    {
        public float width = 4.5f;

        public event Action<Zone> LightSettled;

        public float ActTime { get; private set; }

        LightSpec spec;
        Zone startZone;
        int arrivals;           // 已发出的落定次数
        SpriteRenderer sr;
        Light beam;
        LightCone cone;
        float fullIntensity;
        bool running;

        float Period => spec.dwell + spec.transition;

        void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
            beam = GetComponentInChildren<Light>();
            cone = GetComponentInChildren<LightCone>();
            if (beam != null) fullIntensity = beam.intensity;
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

        // 第 n 次落定后停在哪个区：单数次在另一区，双数次回到起始区。
        Zone ZoneAfter(int n) => n % 2 == 0 ? startZone : Stage.Other(startZone);

        // 下一次落在 zone 的时刻。呈现窗口要对准"光回到目标区"那一次，而不是光离开时的那次落定。
        public float NextArrivalIn(Zone zone)
        {
            int n = arrivals + 1;
            if (ZoneAfter(n) != zone) n++;
            return ArrivalAt(n);
        }

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
            SetBrightness(0.55f + 0.45f * Mathf.Clamp01(suspicion / 100f));
            // TODO: 满怀疑时 2px 抖动
        }

        // 幕完成：追光闪一下（0.15s 变亮，§16）。
        public System.Collections.IEnumerator Pulse(float duration)
        {
            float before = brightness;
            SetBrightness(1.4f);
            yield return new WaitForSeconds(duration);
            SetBrightness(before);
        }

        float brightness = 1f;

        void SetBrightness(float k)
        {
            brightness = k;
            if (sr != null)
            {
                var c = sr.color;
                c.a = Mathf.Clamp01(k);
                sr.color = c;
            }
            if (beam != null) beam.intensity = fullIntensity * k;
            if (cone != null) cone.SetBrightness(k);
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
