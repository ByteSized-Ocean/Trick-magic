using UnityEngine;
using UnityEngine.UI;

namespace Sleight.RabbitTrick
{
    // Code-native cut-paper animals echo the simple stage props and remain sharp at every resolution.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class HatAnimalGraphic : MaskableGraphic
    {
        public HatSpecies Species;
        public bool Golden;
        float age;
        float departure;
        bool departing;
        public void SetDeparture(float seconds, bool active) { departure = seconds; departing = active; }
        public int RenderedVertexCount { get; private set; }
        VertexHelper mesh;
        static readonly Color Ink = C(0x241E31), Cream = C(0xF5E8CB), Pink = C(0xE39FAF), Brown = C(0x9D644B), Gold = C(0xE5B356);
        public void Animate(float seconds) { age = seconds; SetVerticesDirty(); }
        static Color C(uint c) => new Color32((byte)(c >> 16), (byte)(c >> 8), (byte)c, 255);
        Color Dye(Color c, bool ink)
        {
            if (Golden && !ink) c = Color.Lerp(C(0x825021), C(0xFFF1A3), Mathf.Clamp01(c.grayscale * 0.8f + 0.15f));
            c.a *= color.a; return c;
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            mesh = vh; vh.Clear();
            switch (Species)
            {
                case HatSpecies.Mouse: Mouse(); break;
                case HatSpecies.Rabbit: Rabbit(); break;
                case HatSpecies.Dove: Bird(false); break;
                case HatSpecies.Snake: Snake(); break;
                case HatSpecies.Eagle: Bird(true); break;
                case HatSpecies.Monkey: Monkey(); break;
                case HatSpecies.Bear: Cat(0); break;
                case HatSpecies.Lion: Cat(1); break;
                case HatSpecies.Tiger: Cat(2); break;
                case HatSpecies.Elephant: Elephant(); break;
                case HatSpecies.Orca: Orca(); break;
                case HatSpecies.EmperorPenguin: Penguin(); break;
                case HatSpecies.DivineDragon: Dragon(); break;
            }
            RenderedVertexCount = vh.currentVertCount;
        }
        void Shape(Color c, bool ink, params Vector2[] p)
        {
            int start = mesh.currentVertCount; c = Dye(c, ink);
            Vector2 scale = rectTransform.rect.size / new Vector2(320, 240);
            for (int i = 0; i < p.Length; i++)
            {
                var point = p[i];
                if (departing && Species == HatSpecies.Orca && point.x < -50) point.y += Mathf.Sin(age * 10 + point.x * 0.04f) * 10;
                if (departing && Species == HatSpecies.Rabbit && point.y > 60) point.x += Mathf.Sin(age * 11) * (point.y - 60) * 0.12f;
                mesh.AddVert(Vector2.Scale(point, scale), c, Vector2.zero);
            }
            for (int i = 1; i + 1 < p.Length; i++) mesh.AddTriangle(start, start + i, start + i + 1);
        }
        void E(float x, float y, float w, float h, Color c, bool ink = false)
        {
            const int n = 26; var points = new Vector2[n];
            for (int i = 0; i < n; i++) { float a = i * Mathf.PI * 2 / n; points[i] = new Vector2(x + Mathf.Cos(a) * w * 0.5f, y + Mathf.Sin(a) * h * 0.5f); }
            Shape(c, ink, points);
        }
        void P(Color c, params float[] xy)
        { var p = new Vector2[xy.Length / 2]; for (int i = 0; i < p.Length; i++) p[i] = new Vector2(xy[i * 2], xy[i * 2 + 1]); Shape(c, false, p); }
        void L(Vector2 a, Vector2 b, float width, Color c, bool ink = false)
        { var n = new Vector2(-(b - a).y, (b - a).x).normalized * width * 0.5f; Shape(c, ink, a - n, b - n, b + n, a + n); }
        void Stroke(Color c, float width, params Vector2[] points)
        { for (int i = 1; i < points.Length; i++) L(points[i - 1], points[i], width, c); }
        void Eye(float x, float y, float size = 7)
        { E(x, y, size, size * 1.15f, Ink, true); E(x + size * 0.16f, y + size * 0.18f, size * 0.27f, size * 0.27f, Color.white, true); }
        void Mouse()
        {
            Stroke(Pink, 5, new Vector2(-40, -38), new Vector2(-88, -50), new Vector2(-119, -25), new Vector2(-120, 0), new Vector2(-100, 14));
            E(-12, -12, 117, 77, C(0x9694A2)); E(36, 19, 77, 65, C(0xB3AEBA));
            E(13, 58, 42, 46, C(0xA5A1B1)); E(16, 59, 25, 29, Pink);
            E(54, 48, 35, 41, C(0xACA7B6)); E(55, 49, 21, 25, Pink);
            E(71, 8, 20, 17, Pink); Eye(45, 24); E(-35, -49, 31, 12, Pink); E(17, -49, 32, 12, Pink);
            for (int i = -1; i <= 1; i++) L(new Vector2(65, 8), new Vector2(102, 8 + i * 13), 1.8f, Cream);
            if (departing) for (int i = 0; i < 4; i++) { float x = -40 + i * 19, phase = age * 20 + i * Mathf.PI; L(new Vector2(x, -34), new Vector2(x + Mathf.Sin(phase) * 14, -52 + Mathf.Abs(Mathf.Cos(phase)) * 7), 5, Pink); }
        }
        void Rabbit()
        {
            E(-7, -8, 103, 108, Cream); E(-59, -15, 28, 31, Color.white); E(22, 44, 69, 66, Color.white);
            E(4, 91, 23, 77, Cream); E(39, 94, 24, 86, Cream); E(5, 93, 11, 55, Pink); E(40, 97, 11, 64, Pink);
            E(-29, -57, 47, 21, Cream); E(24, -57, 43, 21, Cream); Eye(31, 52); E(53, 35, 12, 10, Pink);
            P(C(0xB6536F), -28, 18, 0, 6, -25, -4); P(C(0xB6536F), 21, 16, -2, 6, 20, -5);
        }
        void Bird(bool eagle)
        {
            float flap = Mathf.Sin(age * (departing ? eagle ? 14 : 18 : eagle ? 8 : 13)) * (departing ? 28 : 17);
            Color body = eagle ? Brown : Cream, wing = eagle ? C(0x61483E) : C(0xD7E0E7);
            P(wing, -16, 13, -61, 67 + flap, -129, 72 + flap, -102, 34, -123, 43, -92, 9, -105, 14, -61, -15, -6, -13);
            P(wing, 4, 11, 63, 71 - flap, 132, 61 - flap, 105, 28, 124, 34, 84, -4, 105, 3, 51, -24, 4, -16);
            P(body, -17, -17, -44, -65, -10, -52, 12, -64, 19, -24);
            E(0, -1, 62, 84, body); E(12, 42, 48, 43, Cream);
            P(Gold, 27, 45, 54, 32, 32, 21); Eye(23, 47, 6);
            if (eagle) { P(Ink, 13, 53, 28, 50, 29, 55); L(new Vector2(-12, -33), new Vector2(-21, -46), 5, Gold); }
        }
        void Snake()
        {
            Color green = C(0x68AF7E), dark = C(0x387760);
            if (departing)
            {
                var last = new Vector2(-135, 0);
                for (int i = 1; i <= 28; i++) { float x = -135 + i * 8; var next = new Vector2(x, Mathf.Sin(i * 0.4f - age * 9) * 22); L(last, next, 5 + i * 0.7f, green); last = next; }
                E(105, last.y, 52, 35, green); Eye(117, last.y + 7, 6);
                if (Mathf.Sin(age * 14) > 0) { L(new Vector2(129, last.y), new Vector2(150, last.y + 3), 3, Pink); L(new Vector2(143, last.y + 2), new Vector2(150, last.y - 6), 2, Pink); }
                return;
            }
            E(-12, -42, 151, 40, dark); E(1, -31, 118, 43, green); E(8, -34, 75, 16, C(0xD5D59A));
            Stroke(green, 29, new Vector2(-17, -22), new Vector2(18, -3), new Vector2(41, 22), new Vector2(39, 54));
            E(36, 60, 66, 43, green); E(48, 49, 43, 15, C(0xD5D59A)); Eye(49, 68, 6);
            Stroke(C(0xE48693), 3, new Vector2(67, 55), new Vector2(88, 55), new Vector2(97, 62));
            L(new Vector2(88, 55), new Vector2(97, 48), 3, C(0xE48693));
            for (int i = 0; i < 5; i++) E(-48 + i * 25, -30, 7, 7, dark);
        }
        void Monkey()
        {
            Stroke(Brown, 10, new Vector2(-30, -21), new Vector2(-86, -47), new Vector2(-112, -15), new Vector2(-100, 27), new Vector2(-77, 37), new Vector2(-65, 19));
            E(-7, -5, 83, 105, Brown); E(-5, -8, 50, 73, C(0xD0A17C));
            E(-39, 57, 31, 35, Brown); E(37, 57, 31, 35, Brown); E(-1, 53, 79, 73, Brown); E(-1, 48, 58, 53, C(0xD8AE87));
            Eye(-15, 57); Eye(14, 57); E(0, 36, 23, 17, Cream); E(0, 40, 8, 6, Ink, true);
            float wave = departing ? Mathf.Sin(age * 10) * 21 : 0, throwHand = departing && departure < 1.6f ? 45 : wave;
            Stroke(Brown, 15, new Vector2(-32, 9), new Vector2(-61, 3 + wave), new Vector2(-74, 28 + wave));
            Stroke(Brown, 15, new Vector2(30, 9), new Vector2(63, throwHand), new Vector2(73, 29 + throwHand));
            E(-76, 31 + wave, 21, 23, C(0xD8AE87)); E(76, 31 + throwHand, 21, 23, C(0xD8AE87)); E(-27, -60 + wave * 0.4f, 34, 17, Brown); E(24, -60 - wave * 0.4f, 34, 17, Brown);
        }
        void Cat(int kind)
        {
            if (departing) { RunningCat(kind); return; }
            Color fur = kind == 0 ? C(0x886C69) : kind == 1 ? C(0xDAAA58) : C(0xE6A25A);
            E(-4, -18, 114, 108, fur); E(-38, -68, 40, 22, fur); E(34, -68, 40, 22, fur);
            if (kind == 1)
            {
                for (int i = 0; i < 12; i++) { float a = i * Mathf.PI / 6; E(Mathf.Cos(a) * 47, 47 + Mathf.Sin(a) * 46, 40, 44, C(0xA76446)); }
            }
            else { E(-39, 79, 32, 34, fur); E(38, 79, 32, 34, fur); E(-39, 80, 17, 18, Pink); E(38, 80, 17, 18, Pink); }
            E(0, 45, 99, 92, fur); E(0, 24, 56, 36, Cream); Eye(-22, 52, 7); Eye(22, 52, 7); E(0, 32, 22, 15, Ink, true);
            L(new Vector2(0, 25), new Vector2(0, 14), 2, Ink, true);
            if (kind == 2)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    P(Ink, side * 47, 66, side * 24, 64, side * 45, 55);
                    P(Ink, side * 48, 37, side * 29, 37, side * 47, 27);
                    P(Ink, side * 52, -7, side * 28, -17, side * 52, -25);
                }
                P(Ink, -10, 90, 0, 67, 10, 90);
            }
            if (kind > 0) { Stroke(fur, 8, new Vector2(45, -29), new Vector2(81, -20), new Vector2(96, 13)); E(96, 15, 15, 21, kind == 1 ? C(0xA76446) : Ink); }
        }
        void RunningCat(int kind)
        {
            Color fur = kind == 0 ? C(0x886C69) : kind == 1 ? C(0xDAAA58) : C(0xE6A25A);
            for (int i = 0; i < 4; i++)
            {
                float x = -62 + i * 37, phase = age * 15 + i * Mathf.PI * 0.7f;
                var foot = new Vector2(x + Mathf.Sin(phase) * 23, -66 + Mathf.Max(0, Mathf.Cos(phase)) * 21);
                Stroke(fur, 16, new Vector2(x, -6), new Vector2(x + Mathf.Sin(phase) * 11, -38), foot); E(foot.x + 4, foot.y, 25, 12, fur);
            }
            E(-17, 5, 157, 80, fur);
            if (kind == 1) for (int i = 0; i < 10; i++) { float a = i * Mathf.PI / 5; E(69 + Mathf.Cos(a) * 29, 39 + Mathf.Sin(a) * 29, 29, 30, C(0xA76446)); }
            E(72, 39, 62, 61, fur); E(58, 69, 20, 23, fur); E(88, 13, 43, 26, Cream); Eye(86, 44, 6); E(109, 24, 14, 11, Ink, true);
            E(100, 8, 26, 9 + Mathf.Abs(Mathf.Sin(age * 8)) * 9, Ink, true); P(Cream, 94, 15, 97, 6, 100, 15);
            if (kind == 2)
            {
                for (int i = 0; i < 5; i++) P(Ink, -72 + i * 27, 36, -62 + i * 27, 9, -53 + i * 27, 36);
                P(Ink, 61, 65, 74, 45, 78, 67); P(Ink, 78, 55, 92, 49, 89, 61);
            }
            float tail = Mathf.Sin(age * 10) * 15;
            Stroke(fur, kind == 0 ? 5 : 8, new Vector2(-86, 9), new Vector2(-116, 30 + tail), new Vector2(-135, 15 + tail));
            if (kind == 1) E(-135, 15 + tail, 19, 22, C(0xA76446));
        }
        void Elephant()
        {
            Color skin = C(0x9B9EBD), shade = C(0x777B9E);
            if (departing)
                for (int i = 0; i < 4; i++) { float x = -76 + i * 28, swing = Mathf.Sin(age * 8 + i * Mathf.PI * 0.7f) * 13; Stroke(skin, 22, new Vector2(x, -25), new Vector2(x + swing, -61), new Vector2(x + swing * 1.3f, -85)); }
            else { E(-72, -68, 34, 52, skin); E(-3, -68, 34, 52, skin); }
            E(-25, -17, 143, 99, skin);
            E(37, 30, 88, 84, skin); E(2, 30, 55, 72, shade); E(3, 30, 35, 51, C(0xB3A3BB));
            Stroke(skin, 23, new Vector2(67, 24), new Vector2(83, -16), new Vector2(109, -35), new Vector2(129, -13), new Vector2(125, departing ? 38 + Mathf.Sin(age * 7) * 8 : 8));
            P(Cream, 60, 1, 72, -27, 90, -31, 73, -8); Eye(51, 43, 8);
            Stroke(shade, 4, new Vector2(-92, -8), new Vector2(-117, -30), new Vector2(-113, -46));
        }
        void Orca()
        {
            Color body = C(0x333442);
            P(body, -51, -10, -126, 26, -117, -5, -140, -30, -73, -30);
            P(body, -20, 26, -4, 89, 23, 20); E(8, 0, 195, 88, body); E(26, -23, 136, 34, Cream);
            E(59, 18, 38, 22, Cream); P(body, -7, -20, 32, -68, 48, -61, 35, -7); Eye(84, 7, 5);
            Stroke(C(0x7994AC), 4, new Vector2(-18, 75), new Vector2(-24, 91), new Vector2(-16, 105));
            Stroke(C(0x7994AC), 4, new Vector2(-18, 75), new Vector2(-6, 93), new Vector2(9, 98));
        }
        void Penguin()
        {
            Color dark = C(0x303447);
            float kick = departing ? Mathf.Sin(age * 13) * 12 : 0;
            P(dark, -32, 23, -81, -39 + kick, -59, -48 + kick, -24, -19); P(dark, 32, 23, 81, -39 - kick, 59, -48 - kick, 24, -19);
            E(0, -13, 89, 132, dark); E(0, -26, 62, 92, Cream); E(0, 59, 69, 70, dark);
            E(-27, 35, 16, 34, Gold); E(27, 35, 16, 34, Gold); E(0, 7, 41, 28, C(0xEDBC62));
            P(C(0xE2A262), -2, 48, 25, 40, 1, 33); Eye(-13, 64, 5); Eye(15, 64, 5);
            E(-26, -82, 37, 17, C(0xDA9E68)); E(26, -82, 37, 17, C(0xDA9E68));
        }
        void Dragon()
        {
            Color jade = C(0x71C9AE), belly = C(0xEBC875), mane = C(0xDA7D8B);
            Vector2 previous = new Vector2(-137, -31);
            for (int i = 1; i <= 28; i++)
            {
                float x = -137 + i * 7.4f, y = Mathf.Sin(i * 0.27f + age * (departing ? 2.4f : 1.2f)) * (departing ? 39 : 26);
                var next = new Vector2(x, y); L(previous, next, 27 - i * 0.12f, jade); L(previous + Vector2.down * 8, next + Vector2.down * 8, 7, belly);
                if (i % 3 == 0) P(mane, x - 7, y + 12, x, y + 30, x + 7, y + 12);
                previous = next;
            }
            for (int side = -1; side <= 1; side += 2)
            {
                Stroke(jade, 10, new Vector2(side * 52, -3), new Vector2(side * 40, -42), new Vector2(side * 62, -53));
                for (int k = 0; k < 3; k++) L(new Vector2(side * 61, -51), new Vector2(side * (64 + k * 9), -63 + k * 4), 3, Cream);
            }
            E(89, 25, 80, 58, jade); E(119, 10, 55, 28, jade); P(mane, 54, 23, 46, 65, 72, 41, 60, 85, 91, 46);
            Stroke(belly, 7, new Vector2(79, 47), new Vector2(75, 78), new Vector2(89, 96));
            L(new Vector2(76, 74), new Vector2(62, 82), 6, belly);
            Stroke(belly, 7, new Vector2(106, 46), new Vector2(112, 76), new Vector2(128, 88));
            Eye(106, 32, 8); P(Cream, 120, 6, 123, -9, 128, 6);
            Stroke(Cream, 2.5f, new Vector2(128, 13), new Vector2(158, 33), new Vector2(163, 58));
            Stroke(Cream, 2.5f, new Vector2(128, 2), new Vector2(157, -16), new Vector2(166, 4));
        }
    }
}
