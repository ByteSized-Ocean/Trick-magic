using UnityEngine;

namespace Sleight.RabbitTrick
{
    // 过关时左右两片大幕合拢、停一下再拉开。不做线性平移，而是一套小的弹簧模拟：
    // 拉绳那一端（最靠中间的褶）被弹簧拽着走，其余每一褶再用更软的弹簧追前一褶，
    // 所以幕布会先拉开前缘、后面的褶慢半拍堆上来；两片在中间碰头时略微弹回，
    // 每一褶还会按自己的速度往后甩一点，像下摆在晃。
    public sealed class StageCurtains : MonoBehaviour
    {
        public Transform[] left = new Transform[0];    // 每一褶的挂点（在幕杆上），0 = 最靠中间的那一褶
        public Transform[] right = new Transform[0];
        public float centerX = 5f;
        public float gatheredX = 0.08f;                 // 拉开时褶子堆在离台口边多远
        public float gatheredSpacing = 0.075f;
        public float halfWidth = 4.95f;                 // 合拢时一片幕盖住的宽度
        public float gatheredWidth = 0.22f;             // 拉开时每一褶的宽度
        public float stiffness = 26f, damping = 6.5f;   // 拉绳那一端的弹簧
        public float foldStiffness = 55f, foldDamping = 10f;
        public float swayPerSpeed = 7f, maxSway = 9f;

        float target, edge, edgeVel;
        float[] folds = new float[0], foldVel = new float[0];

        public bool Closing => target > 0.5f;
        public float Closedness => Mathf.Clamp01(edge);   // 0 = 全开，1 = 合拢
        public bool Settled => Mathf.Abs(edge - target) < 0.012f && Mathf.Abs(edgeVel) < 0.05f && FoldsSettled();

        public void Close() => target = 1f;
        public void Open() => target = 0f;

        public void SnapOpen()
        {
            target = edge = edgeVel = 0f;
            EnsureArrays();
            for (int i = 0; i < folds.Length; i++) folds[i] = foldVel[i] = 0f;
            Apply(0f);
        }

        void Awake() => SnapOpen();

        void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.04f);
            EnsureArrays();

            edgeVel += (stiffness * (target - edge) - damping * edgeVel) * dt;
            edge += edgeVel * dt;
            // 两片在中间碰头：不许穿过去，带一点回弹
            if (edge > 1.015f) { edge = 1.015f; if (edgeVel > 0) edgeVel *= -0.3f; }
            if (edge < -0.02f) { edge = -0.02f; if (edgeVel < 0) edgeVel *= -0.3f; }

            for (int i = 0; i < folds.Length; i++)
            {
                // 每一褶追的是"前一褶"：越往外越晚到，拉开时也越晚收拢。
                float lead = i == 0 ? edge : folds[i - 1];
                float goal = Mathf.Lerp(0f, 1f, lead);
                foldVel[i] += (foldStiffness * (goal - folds[i]) - foldDamping * foldVel[i]) * dt;
                folds[i] += foldVel[i] * dt;
            }
            Apply(dt);
        }

        void EnsureArrays()
        {
            int n = Mathf.Max(left.Length, right.Length);
            if (folds.Length == n) return;
            folds = new float[n];
            foldVel = new float[n];
        }

        void Apply(float dt)
        {
            for (int side = 0; side < 2; side++)
            {
                var strips = side == 0 ? left : right;
                float dir = side == 0 ? 1f : -1f;            // 左片往右拉，右片往左拉
                float outer = centerX - dir * (centerX - 0f); // 台口边：左 0，右 10
                int n = strips.Length;
                for (int i = 0; i < n; i++)
                {
                    if (strips[i] == null) continue;
                    float k = Mathf.Clamp(folds.Length > i ? folds[i] : 0f, -0.05f, 1.05f);
                    float closedW = halfWidth / n;
                    float openX = outer + dir * (gatheredX + (n - 1 - i) * gatheredSpacing);
                    float closedX = centerX - dir * (i + 0.5f) * closedW;
                    float x = Mathf.LerpUnclamped(openX, closedX, k);
                    float w = Mathf.Lerp(gatheredWidth, closedW * 1.25f, Mathf.Clamp01(k));
                    strips[i].position = new Vector3(x, strips[i].position.y, strips[i].position.z);
                    var s = strips[i].localScale;
                    strips[i].localScale = new Vector3(w, s.y, s.z);
                    float v = folds.Length > i ? foldVel[i] : 0f;
                    float sway = Mathf.Clamp(-v * dir * swayPerSpeed, -maxSway, maxSway);
                    strips[i].localRotation = Quaternion.Euler(0f, 0f, sway);   // 下摆往运动的反方向甩
                }
            }
        }

        bool FoldsSettled()
        {
            for (int i = 0; i < folds.Length; i++)
                if (Mathf.Abs(folds[i] - target) > 0.02f || Mathf.Abs(foldVel[i]) > 0.08f) return false;
            return true;
        }
    }
}
