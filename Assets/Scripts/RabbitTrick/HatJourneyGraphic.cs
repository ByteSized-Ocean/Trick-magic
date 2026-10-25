using UnityEngine;
using UnityEngine.UI;

namespace Sleight.RabbitTrick
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class HatJourneyGraphic : MaskableGraphic
    {
        public HatDraw Draw;
        public int Route;
        public float Age, ExitAge;
        public int RenderedVertexCount { get; private set; }
        public float Duration => ExitAge + HatAnimalMotion.ExitDuration(Draw.Species) + 1.4f;
        VertexHelper mesh;
        float fade;
        HatAnimalPose pose;
        static Color C(uint value, float alpha = 1) => new Color((value >> 16 & 255) / 255f, (value >> 8 & 255) / 255f, (value & 255) / 255f, alpha);
        static float Hash(int x) => Mathf.Repeat(Mathf.Sin(x * 93.17f + 4.71f) * 43758.54f, 1);
        public void Animate(float age, float exitAge) { Age = age; ExitAge = exitAge; SetVerticesDirty(); }
        Color Tint(Color c)
        {
            if (Draw.Golden) { float a = c.a; c = Color.Lerp(C(0xBE7C24), C(0xFFF0A4), Mathf.Clamp01(c.grayscale * 0.9f + 0.15f)); c.a = a; }
            c.a *= fade; return c;
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            mesh = vh; vh.Clear(); pose = HatAnimalMotion.Sample(Draw.Species, Age, Route, ExitAge);
            fade = Mathf.Clamp01(Age / 0.18f) * Mathf.Clamp01((Duration - Age) / 1.2f);
            switch (Draw.Species)
            {
                case HatSpecies.Eagle: Wind(); break;
                case HatSpecies.Dove: Feathers(); break;
                case HatSpecies.Bear: case HatSpecies.Lion: case HatSpecies.Tiger: Dust(false); break;
                case HatSpecies.Elephant: Dust(true); break;
                case HatSpecies.Monkey: Banana(); break;
                case HatSpecies.EmperorPenguin: Glacier(); break;
                case HatSpecies.Orca: Ocean(); break;
                case HatSpecies.DivineDragon: Clouds(); break;
                default: Tracks(); break;
            }
            if (Draw.Golden)
            {
                for (int i = 0; i < 15; i++)
                {
                    float delay = Hash(i) * 1.1f;
                    var p = At(Age - delay).Position + new Vector2((Hash(i * 3) - 0.5f) * 110, (Hash(i * 7) - 0.5f) * 100 - delay * 35);
                    Star(p, 3 + Hash(i) * 4, C(0xFFE693, (1 - delay / 1.2f) * 0.8f));
                }
            }
            RenderedVertexCount = vh.currentVertCount;
        }
        HatAnimalPose At(float time) => HatAnimalMotion.Sample(Draw.Species, Mathf.Max(0, time), Route, ExitAge);
        void Wind()
        {
            if (!pose.Departing) return;
            int side = (Route & 1) == 0 ? -1 : 1;
            for (int k = 0; k < 8; k++)
            {
                Vector2 previous = pose.Position + new Vector2(-side * 20, -35 + k * 12);
                for (int i = 1; i <= 18; i++)
                {
                    float q = i / 18f;
                    var next = pose.Position + new Vector2(-side * (20 + q * 190), -35 + k * 12 + Mathf.Sin(q * Mathf.PI * 3 + Age * 4 + k) * q * 17);
                    Line(previous, next, 2 + k % 2, C(0xBCE8E7, (1 - q) * 0.65f * Mathf.Clamp01(pose.DepartureAge / 0.25f))); previous = next;
                }
            }
            float gust = Mathf.Clamp01(1 - pose.DepartureAge / 3.2f);
            for (int k = 0; k < 3; k++)
            {
                Vector2 previous = new Vector2(645, 700);
                for (int i = 1; i <= 35; i++)
                {
                    float a = i * 0.17f + Age * 4, radius = 10 + i * 2.4f;
                    var next = new Vector2(645 + Mathf.Cos(a) * radius * 1.5f + k * 30, 695 + Mathf.Sin(a) * radius * 0.45f);
                    Line(previous, next, 2, C(0xD5F4E8, gust * 0.3f)); previous = next;
                }
            }
        }
        void Feathers()
        {
            if (!pose.Departing) return;
            for (int i = 0; i < 8; i++)
            {
                float delay = Hash(i + 20) * 1.7f;
                var p = At(Age - delay).Position + new Vector2(Mathf.Sin(Age * 3 + i) * 20, delay * 36);
                Line(p, p + new Vector2(8, -14), 5, C(0xF4EAD1, (1 - delay / 1.9f) * 0.6f));
            }
        }
        void Dust(bool heavy)
        {
            if (!pose.Departing) return;
            int side = (Route & 1) == 0 ? -1 : 1, count = heavy ? 44 : 25;
            for (int i = 0; i < count; i++)
            {
                float r = Hash(i + Route * 17), life = r * 1.7f;
                if (pose.DepartureAge < life) continue;
                var feet = At(Age - life).Position + new Vector2(-side * life * (35 + r * 35), 66 - life * (25 + r * 20));
                Ellipse(feet, new Vector2(14 + life * (heavy ? 40 : 23), 10 + life * 20), C(heavy ? 0xC9AC88u : 0xC3A080u, (1 - life / 1.8f) * (heavy ? 0.35f : 0.25f)));
            }
            for (int i = 0; i < (heavy ? 18 : 6); i++)
            {
                float r = Hash(i * 9 + Route), t = Mathf.Repeat(pose.DepartureAge + r * 1.3f, 1.3f);
                if (pose.DepartureAge < t) continue;
                var p = At(Age - t).Position + new Vector2(-side * t * (35 + r * 90), 69 - (65 + r * 140) * t + 95 * t * t);
                float size = (heavy ? 4 : 2) + r * (heavy ? 8 : 3);
                Polygon(C(0x8E7A70, 1 - t / 1.4f), p + new Vector2(-size, 1), p + new Vector2(-size * 0.4f, -size), p + new Vector2(size, -size * 0.3f), p + new Vector2(size * 0.6f, size));
            }
        }
        void Tracks()
        {
            if (!pose.Departing) return;
            if (Draw.Species == HatSpecies.Snake)
            {
                var previous = pose.Position + new Vector2(0, 25);
                for (int i = 1; i < 20; i++)
                {
                    float delay = i * 0.07f; if (pose.DepartureAge < delay) break;
                    var next = At(Age - delay).Position + new Vector2(0, 25);
                    Line(previous, next, 3, C(0x99CA9D, (1 - i / 20f) * 0.28f)); previous = next;
                }
                return;
            }
            for (int i = 0; i < 9; i++)
            {
                float delay = i * 0.14f; if (pose.DepartureAge < delay) continue;
                var feet = At(Age - delay).Position + new Vector2(i % 2 == 0 ? -10 : 10, 61);
                Ellipse(feet, new Vector2(7, 4), C(0xD9BA92, (1 - i / 10f) * 0.2f));
            }
        }
        void Banana()
        {
            if (!pose.Departing) return;
            float t = pose.DepartureAge - 0.6f;
            if (t < 0 || t > 3) return;
            float p = Mathf.Clamp01(t / 2.4f), alpha = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.84f, 1, p));
            var from = At(ExitAge + 0.6f).Position + new Vector2(25, -75);
            var centre = Vector2.Lerp(from, new Vector2(815, 430), p); centre.y -= Mathf.Sin(p * Mathf.PI) * 125;
            float scale = Mathf.Lerp(0.2f, 4, p * p), rotation = p * 680 * Mathf.Deg2Rad;
            for (int i = 0; i < 24; i++)
            {
                float a = -Mathf.PI * 0.97f + i / 24f * Mathf.PI * 0.88f, b = a + Mathf.PI * 0.88f / 24;
                Vector2 x = new Vector2(Mathf.Cos(a), Mathf.Sin(a)), y = new Vector2(Mathf.Cos(b), Mathf.Sin(b));
                Polygon(C(0xF3CB55, alpha), centre + Rotate(x * 42 * scale, rotation), centre + Rotate(y * 42 * scale, rotation),
                    centre + Rotate(y * 26 * scale + new Vector2(0, -5 * scale), rotation), centre + Rotate(x * 26 * scale + new Vector2(0, -5 * scale), rotation));
                Line(centre + Rotate(x * 35 * scale, rotation), centre + Rotate(y * 35 * scale, rotation), scale, C(0xFFF0AD, alpha));
            }
            if (t > 2.2f)
            {
                float hit = Mathf.Clamp01((t - 2.2f) / 0.8f);
                Ring(new Vector2(815, 430), 85 + hit * 130, 7 * (1 - hit), C(0xE8C260, 1 - hit));
                for (int i = 0; i < 12; i++) { float a = i * Mathf.PI / 6; Star(new Vector2(815, 430) + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (80 + hit * 170), 8, C(0xF3CB55, 1 - hit)); }
            }
        }
        void Glacier()
        {
            float entry = Mathf.Clamp01(Age / 0.6f) * Mathf.Clamp01((4 - Age) / 1.4f);
            for (int i = 0; i < 12; i++)
            {
                float r = Hash(i * 3), x = 515 + i * 23, h = (35 + r * 90) * entry;
                Polygon(C(0x72CFEC, entry * 0.55f), new Vector2(x - 20, 714), new Vector2(x - 7, 714 - h), new Vector2(x + 4, 704 - h * 0.8f), new Vector2(x + 18, 714));
                Line(new Vector2(x - 7, 714 - h), new Vector2(x, 711), 2, C(0xD4F8FF, entry * 0.8f));
            }
            if (!pose.Departing) return;
            int steps = 80;
            var previous = At(ExitAge).Position + new Vector2(0, 23);
            for (int i = 1; i <= steps; i++)
            {
                float time = Mathf.Lerp(ExitAge, Mathf.Min(Age, ExitAge + HatAnimalMotion.ExitDuration(Draw.Species)), i / (float)steps);
                var next = At(time).Position + new Vector2(0, 23);
                Line(previous, next, 42, C(0x64C7EC, 0.16f)); Line(previous, next, 17, C(0xB8F1FF, 0.65f)); Line(previous, next, 3, C(0xFFFFFF, 0.75f));
                if (i % 7 == 0) Polygon(C(0xC8F3FF, 0.7f), next + new Vector2(-9, 6), next + new Vector2(1, -14), next + new Vector2(10, 7));
                previous = next;
            }
            for (int i = 0; i < 16; i++)
            {
                float delay = Hash(i) * 1.3f; if (pose.DepartureAge < delay) continue;
                Star(At(Age - delay).Position + new Vector2((Hash(i * 7) - 0.5f) * 70, 10 - delay * 28), 3 + Hash(i) * 4, C(0xCCF7FF, 0.7f - delay * 0.4f));
            }
        }
        void Ocean()
        {
            float strength = Mathf.Clamp01(Age / 0.7f);
            Vector2 previous = new Vector2(0, 690);
            for (int i = 1; i <= 70; i++)
            {
                float x = i * 1600f / 70, y = 691 + Mathf.Sin(i * 0.42f + Age * 2) * 13;
                var next = new Vector2(x, y);
                Polygon(C(0x357EBB, strength * 0.15f), previous, next, new Vector2(x, 900), new Vector2(previous.x, 900));
                Line(previous, next, 4, C(0x9AE9F2, strength * 0.55f)); previous = next;
            }
            Ring(pose.Position + new Vector2(0, 43), 45 + Mathf.Repeat(Age * 35, 100), 3, C(0x86DCEB, 0.4f));
            for (int i = 0; i < 24; i++)
            {
                float delay = Hash(i + Route * 31) * 2.2f;
                var p = At(Age - delay).Position + new Vector2((Hash(i * 13) - 0.5f) * 90, 30 - delay * (28 + Hash(i) * 35));
                float radius = 3 + Hash(i * 7) * 10;
                Ring(p, radius, 2, C(0xA8F1F2, (1 - delay / 2.4f) * 0.8f)); Ellipse(p + new Vector2(-radius * 0.3f, -radius * 0.3f), Vector2.one * radius * 0.4f, C(0xF8FFF7, 0.55f));
            }
        }
        void Clouds()
        {
            for (int i = 0; i < 7; i++)
            {
                float a = i * Mathf.PI * 2 / 7 + Age * 0.08f;
                var c = Color.HSVToRGB(i / 7f, 0.42f, 1); c.a = 0.64f;
                Cloud(pose.Position + new Vector2(Mathf.Cos(a) * 220, 85 + Mathf.Sin(a) * 50), 30 + Hash(i) * 17, c);
            }
            if (!pose.Departing) return;
            for (int i = 0; i < 28; i++)
            {
                float delay = i * 0.12f; if (pose.DepartureAge < delay) continue;
                var c = Color.HSVToRGB(Mathf.Repeat(i / 7f + Age * 0.05f, 1), 0.48f, 1); c.a = (1 - i / 30f) * 0.45f;
                Cloud(At(Age - delay).Position + new Vector2(Mathf.Sin(i * 3) * 40, 75), 18 + i * 0.9f, c);
            }
        }
        void Cloud(Vector2 p, float radius, Color c)
        { Ellipse(p, new Vector2(radius * 3, radius), c); Ellipse(p + new Vector2(-radius * 0.6f, -radius * 0.35f), Vector2.one * radius * 1.2f, c); Ellipse(p + new Vector2(radius * 0.3f, -radius * 0.5f), Vector2.one * radius * 1.5f, c); }
        static Vector2 Rotate(Vector2 p, float a) => new Vector2(p.x * Mathf.Cos(a) - p.y * Mathf.Sin(a), p.x * Mathf.Sin(a) + p.y * Mathf.Cos(a));
        void Polygon(Color colour, params Vector2[] points)
        {
            int n = mesh.currentVertCount; var c = Tint(colour);
            foreach (var p in points) mesh.AddVert(new Vector3(p.x, -p.y, 0), c, Vector2.zero);
            for (int i = 1; i + 1 < points.Length; i++) mesh.AddTriangle(n, n + i, n + i + 1);
        }
        void Ellipse(Vector2 p, Vector2 size, Color c)
        { var points = new Vector2[18]; for (int i = 0; i < points.Length; i++) { float a = i * Mathf.PI * 2 / points.Length; points[i] = p + Vector2.Scale(new Vector2(Mathf.Cos(a), Mathf.Sin(a)), size) * 0.5f; } Polygon(c, points); }
        void Line(Vector2 a, Vector2 b, float width, Color c)
        { var v = b - a; var n = new Vector2(-v.y, v.x).normalized * width * 0.5f; Polygon(c, a - n, b - n, b + n, a + n); }
        void Star(Vector2 p, float r, Color c) => Polygon(c, p + Vector2.up * r, p + Vector2.right * r * 0.45f, p + Vector2.down * r, p + Vector2.left * r * 0.45f);
        void Ring(Vector2 p, float radius, float width, Color c)
        { for (int i = 0; i < 40; i++) { float a = i * Mathf.PI / 20, b = (i + 1) * Mathf.PI / 20; var x = new Vector2(Mathf.Cos(a), Mathf.Sin(a)); var y = new Vector2(Mathf.Cos(b), Mathf.Sin(b)); Polygon(c, p + x * radius, p + y * radius, p + y * (radius + width), p + x * (radius + width)); } }
    }
}
