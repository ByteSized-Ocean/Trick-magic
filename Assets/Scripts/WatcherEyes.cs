using UnityEngine;

namespace Sleight
{
    // 前排的"他"（GDD 附录 B）：两只小眼睛反着光，跟着追光往左右看。纯画面，不参与判定。
    public class WatcherEyes : MonoBehaviour
    {
        public Transform spotlight;
        public float maxShift = 0.06f;    // 单位；约 4px
        public float follow = 6f;

        Vector3 rest;

        void Awake()
        {
            rest = transform.localPosition;
        }

        void Update()
        {
            if (spotlight == null) return;
            float origin = transform.parent != null ? transform.parent.position.x : rest.x;
            float target = Mathf.Clamp((spotlight.position.x - origin) * 0.03f, -maxShift, maxShift);
            var p = transform.localPosition;
            p.x = Mathf.Lerp(p.x, rest.x + target, 1f - Mathf.Exp(-follow * Time.deltaTime));
            transform.localPosition = p;
        }
    }
}
