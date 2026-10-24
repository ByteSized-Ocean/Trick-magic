using UnityEngine;

namespace Sleight
{
    // 追光看得见的光柱：从画面上方往下打的圆锥（截头），底面落在地面线上。
    // 只是画面，不参与判定（判定仍然按 x 算，§9.2）。物体本身不要旋转，坐标按世界 y 计算。
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class LightCone : MonoBehaviour
    {
        public float topY = 6.2f;
        public float bottomY = 1.0f;
        public float topRadius = 1.2f;
        public float bottomRadius = 2.5f;
        public int segments = 48;
        public Color color = new Color(1f, 0.824f, 0.118f, 0.35f);

        MeshRenderer mr;
        MaterialPropertyBlock block;
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        void OnEnable()
        {
            mr = GetComponent<MeshRenderer>();
            GetComponent<MeshFilter>().sharedMesh = Build();
            SetBrightness(1f);
        }

        public void SetBrightness(float k)
        {
            if (mr == null) return;
            block ??= new MaterialPropertyBlock();
            var c = color;
            c.a = Mathf.Clamp01(color.a * k);
            block.SetColor(BaseColor, c);
            mr.SetPropertyBlock(block);
        }

        Mesh Build()
        {
            float yTop = topY - transform.position.y;
            float yBottom = bottomY - transform.position.y;
            var verts = new Vector3[(segments + 1) * 2];
            var tris = new int[segments * 6];
            for (int i = 0; i <= segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                verts[i * 2] = dir * topRadius + Vector3.up * yTop;
                verts[i * 2 + 1] = dir * bottomRadius + Vector3.up * yBottom;
            }
            for (int i = 0; i < segments; i++)
            {
                int t = i * 6, v = i * 2;
                tris[t] = v; tris[t + 1] = v + 2; tris[t + 2] = v + 1;
                tris[t + 3] = v + 1; tris[t + 4] = v + 2; tris[t + 5] = v + 3;
            }
            var mesh = new Mesh { name = "LightCone", vertices = verts, triangles = tris, hideFlags = HideFlags.DontSave };
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
