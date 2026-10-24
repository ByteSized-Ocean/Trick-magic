using UnityEngine;

namespace Sleight
{
    // GDD §18.7：代码逐帧换图，不建 Animator 状态机。
    [RequireComponent(typeof(SpriteRenderer))]
    public class FrameAnim : MonoBehaviour
    {
        public Sprite[] frames;
        public float fps = 12f;
        public bool loop = true;

        SpriteRenderer sr;
        float t;
        int i;

        public bool Done => !loop && frames != null && i >= frames.Length - 1;

        void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
        }

        public void Play(Sprite[] f, float framesPerSecond, bool lp)
        {
            frames = f;
            fps = framesPerSecond;
            loop = lp;
            t = 0;
            i = 0;
            if (frames != null && frames.Length > 0) sr.sprite = frames[0];
        }

        void Update()
        {
            if (frames == null || frames.Length == 0) return;
            t += Time.deltaTime;
            if (t < 1f / fps) return;
            t = 0;
            if (i < frames.Length - 1) { i++; sr.sprite = frames[i]; }
            else if (loop) { i = 0; sr.sprite = frames[0]; }
        }
    }
}
