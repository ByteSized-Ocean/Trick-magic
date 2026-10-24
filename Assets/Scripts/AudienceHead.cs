using UnityEngine;

namespace Sleight
{
    // 台前观众的头绕脖子转，跟着追光看。每个人反应快慢不一样，整排头才像活人。纯画面，不参与判定。
    public class AudienceHead : MonoBehaviour
    {
        public Transform spotlight;
        public float maxTilt = 14f;      // 度
        public float degPerUnit = 4f;    // 光每偏开 1 单位，头歪多少度
        public float maxShift = 0.05f;   // 头顺势往那边挪一点（单位）
        public float follow = 3f;        // 越大跟得越快

        Vector3 rest;

        void Awake()
        {
            rest = transform.localPosition;
        }

        void Update()
        {
            if (spotlight == null || !spotlight.gameObject.activeInHierarchy) return;
            float dx = spotlight.position.x - transform.position.x;
            float k = 1f - Mathf.Exp(-follow * Time.deltaTime);

            // 从背后看：头往右转 = 后脑勺朝右歪（顺时针，z 为负）
            float targetTilt = -Mathf.Clamp(dx * degPerUnit, -maxTilt, maxTilt);
            float tilt = Mathf.LerpAngle(transform.localEulerAngles.z, targetTilt, k);
            transform.localRotation = Quaternion.Euler(0f, 0f, tilt);

            float targetX = rest.x + Mathf.Clamp(dx * 0.02f, -maxShift, maxShift);
            var p = transform.localPosition;
            p.x = Mathf.Lerp(p.x, targetX, k);
            transform.localPosition = p;
        }
    }
}
