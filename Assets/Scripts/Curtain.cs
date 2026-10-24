using System.Collections;
using UnityEngine;

namespace Sleight
{
    // GDD §0.2：大幕。搭台时落下，画成半透明的深红纱，玩家隔着它看得见幕后；
    // 台侧挂着一个沙袋，随搭台时间慢慢往下坠，落地就开幕（代替倒计时数字）。
    public class Curtain : MonoBehaviour
    {
        public Transform cloth;          // 大幕本体
        public Transform sandbag;        // 计时沙袋
        public float downY = 3.1f;
        public float upY = 8.6f;
        public float sandbagTop = 4.8f;
        public float sandbagBottom = 1.15f;
        public float moveTime = 1.0f;

        public bool IsDown { get; private set; }

        public void SetDown()
        {
            StopAllCoroutines();
            SetClothY(downY);
            IsDown = true;
            SetProgress(0f);
            if (sandbag != null) sandbag.gameObject.SetActive(true);
        }

        public void SetUp()
        {
            StopAllCoroutines();
            SetClothY(upY);
            IsDown = false;
            if (sandbag != null) sandbag.gameObject.SetActive(false);
        }

        // 0 = 搭台刚开始，1 = 时间到。
        public void SetProgress(float p)
        {
            if (sandbag == null) return;
            var pos = sandbag.position;
            pos.y = Mathf.Lerp(sandbagTop, sandbagBottom, Mathf.Clamp01(p));
            sandbag.position = pos;
        }

        public IEnumerator Raise()
        {
            IsDown = false;
            yield return Move(downY, upY);
            if (sandbag != null) sandbag.gameObject.SetActive(false);
        }

        public IEnumerator Lower()
        {
            yield return Move(upY, downY);
            IsDown = true;
            if (sandbag != null) sandbag.gameObject.SetActive(true);
        }

        IEnumerator Move(float from, float to)
        {
            for (float t = 0f; t < moveTime; t += Time.deltaTime)
            {
                SetClothY(Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / moveTime)));
                yield return null;
            }
            SetClothY(to);
        }

        void SetClothY(float y)
        {
            if (cloth == null) return;
            var pos = cloth.position;
            pos.y = y;
            cloth.position = pos;
        }
    }
}
