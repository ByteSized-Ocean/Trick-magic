using UnityEngine;
using UnityEngine.UI;

namespace Sleight.RabbitTrick
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class HatRevealGraphic : MaskableGraphic
    {
        public HatDraw Draw;
        public float Age;
        public int Seed;
        public float Duration => Draw.Species == HatSpecies.DivineDragon ? 6.8f : Draw.Golden ? 4.2f : 2.5f + Draw.Animal.Tier * 0.12f;
        VertexHelper mesh;
        public void Animate(float age) { Age = age; SetVerticesDirty(); }
        static float Hash(int x) => Mathf.Repeat(Mathf.Sin(x * 93.17f + 16.72f) * 43758.54f, 1);
        Color Rainbow(float phase, float alpha) { var c = Color.HSVToRGB(Mathf.Repeat(phase, 1), 0.64f, 1); c.a = alpha; return c; }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            mesh = vh; vh.Clear();
            float t = Age, life = Mathf.Clamp01(1 - t / Duration), fade = life * Mathf.Clamp01(t / 0.06f);
            bool dragon = Draw.Species == HatSpecies.DivineDragon;
            var origin = dragon ? new Vector2(800, 450) : new Vector2(397, 627);
            Color aura = Draw.Animal.Aura;
            if (dragon)
            {
                // A whole-screen colour wash, prismatic fan, rings and falling stars; controls remain clickable.
                for (int i = 0; i < 16; i++) Quad(i * 100, 0, 101, 900, Rainbow(i / 16f + t * 0.12f, fade * 0.11f));
                for (int i = 0; i < 24; i++)
                {
                    float a = i * Mathf.PI / 12 + t * 0.2f;
                    var p = origin + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 1300;
                    var q = origin + new Vector2(Mathf.Cos(a + 0.075f), Mathf.Sin(a + 0.075f)) * 1300;
                    Triangle(origin, p, q, Rainbow(i / 24f - t * 0.16f, fade * 0.18f));
                }
                for (int i = 0; i < 4; i++) Ring(origin, 100 + Mathf.Repeat(t * 160 + i * 160, 640), 3, Rainbow(i * 0.25f + t * 0.2f, fade * 0.55f));
            }
            else
            {
                aura.a = fade * 0.8f;
                float radius = 15 + Mathf.Sqrt(Mathf.Clamp01(t / 0.75f)) * (65 + Draw.Animal.Tier * 15);
                if (Draw.Animal.Tier == 0) { var rim = new Color(0.72f, 0.65f, 0.78f, fade * 0.45f); Ring(origin, radius + 2, 3, rim); }
                Ring(origin, radius, 4 + Draw.Animal.Tier * 0.4f, aura);
                if (Draw.Animal.Tier >= 3) Ring(origin, radius * 0.7f, 2, aura);
            }
            int count = dragon ? 140 : 9 + Draw.Animal.Tier * 6;
            for (int i = 0; i < count; i++)
            {
                float r = Hash(i + Seed * 53), angle = Hash(i * 7 + Seed) * Mathf.PI * 2;
                var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                Vector2 position = dragon ? new Vector2(Hash(i * 11 + Seed) * 1600, Mathf.Repeat(r * 900 + t * (65 + r * 80), 950) - 25)
                    : origin + dir * (20 + t * (40 + r * (80 + Draw.Animal.Tier * 10))) + Vector2.up * t * t * 20;
                Color c = dragon ? Rainbow(r + t * 0.1f, fade * 0.9f) : new Color(aura.r, aura.g, aura.b, fade * 0.8f);
                float size = (dragon ? 4 : 2) + r * (dragon ? 9 : 5);
                if (Draw.Animal.Tier == 0) Star(position, size + 1.5f, new Color(0.65f, 0.61f, 0.70f, fade * 0.6f));
                Star(position, size, c);
            }
            if (!Draw.Golden) return;
            var goldOrigin = new Vector2(397, 627);
            for (int i = 0; i < 30; i++)
            {
                float a = i * Mathf.PI / 15 + t * 0.3f;
                var direction = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                var start = goldOrigin + direction * (28 + t * 42);
                var end = start + direction * (40 + 65 * life);
                Line(start, end, 3.5f * life, new Color(1, 0.79f, 0.25f, fade * 0.8f));
                Star(end, 4 + Hash(i) * 5, new Color(1, 0.9f, 0.47f, fade));
            }
            Ring(goldOrigin, 20 + t * 130, 6 * life, new Color(1, 0.85f, 0.25f, fade));
        }
        void Triangle(Vector2 a, Vector2 b, Vector2 c, Color col)
        {
            int n = mesh.currentVertCount; mesh.AddVert(new Vector3(a.x, -a.y), col, Vector2.zero); mesh.AddVert(new Vector3(b.x, -b.y), col, Vector2.zero);
            mesh.AddVert(new Vector3(c.x, -c.y), col, Vector2.zero); mesh.AddTriangle(n, n + 1, n + 2);
        }
        void Quad(float x, float y, float w, float h, Color c)
        { var a = new Vector2(x, y); var b = new Vector2(x + w, y); var d = new Vector2(x, y + h); var e = new Vector2(x + w, y + h); Triangle(a, b, e, c); Triangle(a, e, d, c); }
        void Line(Vector2 a, Vector2 b, float width, Color c)
        { Vector2 v = b - a, n = new Vector2(-v.y, v.x).normalized * width * 0.5f; Triangle(a - n, b - n, b + n, c); Triangle(a - n, b + n, a + n, c); }
        void Star(Vector2 p, float r, Color c)
        { Triangle(p + Vector2.up * r, p + Vector2.right * r * 0.45f, p + Vector2.down * r, c); Triangle(p + Vector2.up * r, p + Vector2.down * r, p + Vector2.left * r * 0.45f, c); }
        void Ring(Vector2 p, float radius, float width, Color c)
        {
            const int segments = 70;
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2 / segments, b = (i + 1) * Mathf.PI * 2 / segments;
                var x = new Vector2(Mathf.Cos(a), Mathf.Sin(a)); var y = new Vector2(Mathf.Cos(b), Mathf.Sin(b));
                Triangle(p + x * radius, p + y * radius, p + y * (radius + width), c);
                Triangle(p + x * radius, p + y * (radius + width), p + x * (radius + width), c);
            }
        }
    }
}
