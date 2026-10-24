using UnityEngine;
using UnityEngine.UI;

namespace Sleight.RabbitTrick
{
    // 开场演示放进一台老式电视机里：主摄像机改画到一张 RenderTexture 上，电视屏幕显示这张图，
    // 外面是暗色墙纸、木壳电视、旋钮、天线、扫描线和标题字幕，一眼就看得出"这是在放教学"。
    // 演示结束后摄像机画回屏幕，电视收起。画面全在运行时搭，不需要重建场景。
    public sealed class DemoTelevision : MonoBehaviour
    {
        RectTransform root;
        RawImage screen;
        RawImage scanlines;
        Text title, caption, skip;
        RenderTexture target;
        Camera mainCamera, blankCamera;
        bool showing;

        static readonly Color Wallpaper = new Color32(24, 18, 30, 255);
        static readonly Color Wood = new Color32(110, 70, 44, 255);
        static readonly Color WoodDark = new Color32(78, 48, 30, 255);
        static readonly Color Bezel = new Color32(32, 28, 30, 255);
        static readonly Color Brass = new Color32(210, 156, 79, 255);
        static readonly Color Cream = new Color32(242, 222, 188, 255);

        public void Show(bool on, RabbitStageView view, string titleText, string captionText, string skipText)
        {
            if (on && root == null) Build(view);
            if (on != showing)
            {
                showing = on;
                if (root != null) root.gameObject.SetActive(on);
                RouteCamera(on);
            }
            if (!on) return;

            title.text = titleText;
            caption.text = captionText;
            skip.text = skipText;
            var c = skip.color;
            c.a = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 3.5f);
            skip.color = c;
            // 扫描线缓缓往下滚，屏幕亮度轻微闪烁
            scanlines.uvRect = new Rect(0, -Time.unscaledTime * 0.6f, 1, 90);
            float flicker = 0.93f + 0.04f * Mathf.PerlinNoise(Time.unscaledTime * 8f, 0f);
            screen.color = new Color(flicker, flicker * 0.96f, flicker * 0.88f, 1f);
        }

        void OnDisable()
        {
            if (showing) RouteCamera(false);
            showing = false;
        }

        void OnDestroy()
        {
            if (target != null) { target.Release(); Destroy(target); }
        }

        void RouteCamera(bool toTelevision)
        {
            if (mainCamera == null) mainCamera = Camera.main;
            if (mainCamera == null) return;
            if (toTelevision)
            {
                if (target == null)
                {
                    target = new RenderTexture(1280, 720, 24) { name = "DemoTelevision" };
                    screen.texture = target;
                }
                mainCamera.targetTexture = target;
                if (blankCamera == null)
                {
                    // 主摄像机画到电视里以后，屏幕本身要有人清成纯色，界面才有干净的底
                    var go = new GameObject("DemoBlankCamera");
                    go.transform.SetParent(transform, false);
                    blankCamera = go.AddComponent<Camera>();
                    blankCamera.clearFlags = CameraClearFlags.SolidColor;
                    blankCamera.backgroundColor = Wallpaper;
                    blankCamera.cullingMask = 0;
                    blankCamera.orthographic = true;
                    blankCamera.depth = mainCamera.depth + 1;
                }
                blankCamera.enabled = true;
            }
            else
            {
                mainCamera.targetTexture = null;
                if (blankCamera != null) blankCamera.enabled = false;
            }
        }

        void Build(RabbitStageView view)
        {
            var canvas = view.HudCanvas;
            var font = view.HudFont;

            root = Box("DemoTelevision", canvas, Vector2.zero, Vector2.zero, Wallpaper, view.square);
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.sizeDelta = Vector2.zero;

            // 电视：木壳 + 左边屏幕 + 右边旋钮面板，下面两条腿，上面两根天线
            var tv = Box("Set", root, new Vector2(0, -20), new Vector2(980, 610), Wood, view.square);
            Box("SetShade", tv, new Vector2(0, -282), new Vector2(980, 46), WoodDark, view.square);
            for (int i = 0; i < 2; i++)
            {
                var leg = Box("Leg", root, new Vector2(i == 0 ? -400 : 400, -342), new Vector2(26, 44), WoodDark, view.square);
                leg.localRotation = Quaternion.Euler(0, 0, i == 0 ? -12 : 12);
                var antenna = Box("Antenna", root, new Vector2(i == 0 ? -40 : 40, 360), new Vector2(7, 200), new Color32(170, 170, 176, 255), view.square);
                antenna.pivot = new Vector2(0.5f, 0f);
                antenna.anchoredPosition = new Vector2(0, 280);
                antenna.localRotation = Quaternion.Euler(0, 0, i == 0 ? 28 : -28);
                var tip = Box("AntennaTip", antenna, new Vector2(0, 100), new Vector2(18, 18), new Color32(170, 170, 176, 255), view.circle);
                tip.anchoredPosition = new Vector2(0, 100);
            }
            Box("AntennaBase", root, new Vector2(0, 288), new Vector2(110, 26), WoodDark, view.circle);

            var bezel = Box("Bezel", tv, new Vector2(-95, 22), new Vector2(740, 430), Bezel, view.square);
            screen = new GameObject("Screen", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            Place(screen.rectTransform, bezel, Vector2.zero, new Vector2(704, 396));
            screen.raycastTarget = false;

            // 扫描线：一张 1×2 的小贴图平铺很多遍
            var lines = new Texture2D(1, 2, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point };
            lines.SetPixels(new[] { new Color(0, 0, 0, 0.28f), new Color(0, 0, 0, 0f) });
            lines.Apply();
            scanlines = new GameObject("Scanlines", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            Place(scanlines.rectTransform, bezel, Vector2.zero, new Vector2(704, 396));
            scanlines.texture = lines;
            scanlines.raycastTarget = false;
            // 四角的暗角：显像管的圆角感
            for (int cx = -1; cx <= 1; cx += 2)
                for (int cy = -1; cy <= 1; cy += 2)
                    Box("CornerShade", bezel, new Vector2(cx * 352, cy * 198), new Vector2(90, 90), new Color(0, 0, 0, 0.55f), view.circle);

            var panel = Box("ControlPanel", tv, new Vector2(375, 22), new Vector2(170, 430), WoodDark, view.square);
            for (int i = 0; i < 2; i++)
            {
                var knob = Box("Knob", panel, new Vector2(0, 130 - i * 120), new Vector2(84, 84), Brass, view.circle);
                var mark = Box("KnobMark", knob, new Vector2(0, 22), new Vector2(8, 34), WoodDark, view.square);
                mark.localRotation = Quaternion.Euler(0, 0, i == 0 ? -35 : 50);
            }
            for (int i = 0; i < 6; i++)
                Box("Speaker", panel, new Vector2(0, -90 - i * 20), new Vector2(120, 6), Wood, view.square);

            var brand = Label("Brand", tv, new Vector2(-95, -232), new Vector2(500, 36), "SLEIGHT-O-VISION", 24, Brass, font);
            brand.fontStyle = FontStyle.Bold;

            title = Label("Title", root, new Vector2(0, 400), new Vector2(1400, 50), "", 30, Brass, font);
            caption = Label("Caption", root, new Vector2(0, -350), new Vector2(1400, 44), "", 24, Cream, font);
            skip = Label("Skip", root, new Vector2(0, -400), new Vector2(1000, 36), "PRESS ANY KEY TO SKIP THE DEMO", 20, new Color32(100, 220, 199, 255), font);
            root.gameObject.SetActive(false);
        }

        static RectTransform Box(string name, Transform parent, Vector2 position, Vector2 size, Color color, Sprite sprite)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rt = go.GetComponent<RectTransform>();
            Place(rt, parent, position, size);
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            return rt;
        }

        static void Place(RectTransform rt, Transform parent, Vector2 position, Vector2 size)
        {
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
        }

        static Text Label(string name, Transform parent, Vector2 position, Vector2 size, string value, int fontSize, Color color, Font font)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            var rt = go.GetComponent<RectTransform>();
            Place(rt, parent, position, size);
            var t = go.GetComponent<Text>();
            t.font = font;
            t.fontSize = fontSize;
            t.color = color;
            t.text = value;
            t.alignment = TextAnchor.MiddleCenter;
            t.resizeTextForBestFit = true;
            t.resizeTextMinSize = 12;
            t.resizeTextMaxSize = fontSize;
            t.raycastTarget = false;
            return t;
        }
    }
}
