using UnityEngine;
using UnityEngine.UI;

namespace Sleight.RabbitTrick
{
    public sealed class RabbitStageView : MonoBehaviour
    {
        public Sprite square;
        public Sprite circle;
        public Sprite magicianArt;
        public Sprite[] audienceArt;
        public Sprite[] audienceBodies;   // 观众背影：身体和头分开，头绕脖子转（gen_art.py）
        public Sprite[] audienceHeads;
        public StageCurtains curtains;
        public Sprite glowSprite;    // 柔光（fx_glow），没有就退回硬边圆
        public Sprite flameSprite;   // 火苗（fx_flame）
        public Material stageMaterial;
        public Material magicianMaterial;
        public Material overlayMaterial;
        public Material lineMaterial;
        public Material lineOverlayMaterial;
        public Font interfaceFont;

        [SerializeField] Transform world;
        [SerializeField] Transform box, lid, rabbit, dancer, magician, dancerHandle, dancerBase;
        [SerializeField] Transform[] gloves = new Transform[2];
        [SerializeField] SpriteRenderer[] handRings = new SpriteRenderer[2];
        [SerializeField] LineRenderer[] arms = new LineRenderer[2];
        [SerializeField] LineRenderer[] dancerLimbs = new LineRenderer[4];
        [SerializeField] Transform beam;
        [SerializeField] SpriteRenderer pool;
        [SerializeField] SpriteRenderer emptyWindow;
        [SerializeField] Text[] stepLabels = new Text[3];
        [SerializeField] Image[] stepBars = new Image[3];
        [SerializeField] Text handLabel, hintLabel, gazeLabel, endingLabel;
        [SerializeField] Image showProgress, distractionProgress, flash;
        [SerializeField] Image patienceFill, patienceBack;
        [SerializeField] Text stepsTitle;
        public Transform World => world;
        public float[] GloveAlpha { get; } = { 1f, 1f };   // 关卡可以把藏起来的手画淡（如悬浮术里桌布下的那只手）
        [SerializeField] LineRenderer beamLine;
        static readonly Color BeamColor = new Color(1f, 0.81f, 0.4f, 0.09f);
        static readonly Color PoolColor = new Color(1f, 0.8f, 0.4f, 0.16f);
        [SerializeField] Text patienceLabel;
        readonly System.Collections.Generic.List<Thrown> thrown = new System.Collections.Generic.List<Thrown>();
        float throwTimer;
        System.Random throwRng = new System.Random(3);
        [SerializeField] GameObject resultPanel;
        [SerializeField] Text resultTitle, resultBody;
        [SerializeField] Text controls;
        [SerializeField] LineRenderer[] legs = new LineRenderer[2];
        [SerializeField] Transform[] shoes = new Transform[2];
        [SerializeField] Transform[] audienceHeadPivots = new Transform[0];
        [SerializeField] float[] audienceFollow = new float[0];
        [SerializeField] Transform[] gloveFingers = new Transform[8];   // 每只手 4 根
        [SerializeField] Transform[] gloveThumbs = new Transform[2];
        float[] grip = new float[2];   // 0 = 张开，1 = 握拳
        [SerializeField] Transform[] torchFlames = new Transform[0];
        [SerializeField] Transform[] torchCores = new Transform[0];
        [SerializeField] SpriteRenderer[] torchGlows = new SpriteRenderer[0];
        [SerializeField] SpriteRenderer[] torchWallGlows = new SpriteRenderer[0];
        Font runtimeFont;
        Vector2 lastBody;
        float walkPhase, walkBlend;
        bool facingLeft, hasLastBody;
        Vector3 bodyOffset;   // 走路起伏，身体、肩膀、腿共用
        const float HipHeight = 0.78f, Thigh = 0.41f, Shin = 0.41f, Stride = 0.62f;
        readonly Color cream = new Color32(242, 222, 188, 255);
        readonly Color teal = new Color32(100, 220, 199, 255);
        readonly Color gold = new Color32(210, 156, 79, 255);
        const float BoxBase = 1.18f;

        public void Build()
        {
            world = Group("Stage", transform, Vector2.zero);
            Rect("Backdrop", world, new Vector2(5, 3.1f), new Vector2(10, 5.0f), Hex(0x453C50), -30);
            for (int i = 0; i < 16; i++)
                Rect("WallPanel", world, new Vector2(i * 0.67f, 3f), new Vector2(0.025f, 4.6f), Hex(0x615061), -29);
            Rect("StageFloor", world, new Vector2(5, 1.28f), new Vector2(10, 1.13f), Hex(0xAD7956), -25);
            Rect("StageLip", world, new Vector2(5, 0.72f), new Vector2(10, 0.12f), gold, -24);
            for (int i = 0; i < 19; i++)
                Rect("FloorBoard", world, new Vector2(i * 0.57f, 1.28f), new Vector2(0.014f, 1.05f), Hex(0x503833), -24);
            for (int side = 0; side < 2; side++)
                for (int fold = 0; fold < 4; fold++)
                    Rect("Velvet", world, new Vector2(side == 0 ? fold * 0.17f : 10 - fold * 0.17f, 3.2f),
                        new Vector2(0.25f, 4.7f), fold % 2 == 0 ? Hex(0x772A43) : Hex(0x562034), 2);
            Rect("Valance", world, new Vector2(5, 5.35f), new Vector2(10, 0.55f), Hex(0x6F2841), 2);
            Rect("ValanceTrim", world, new Vector2(5, 5.07f), new Vector2(10, 0.045f), gold, 3);
            BuildScenery();

            var magicianSprite = Sprite("Magician", world, magicianArt, new Vector2(3.85f, 1.18f), new Vector2(1.4f, 2.3f), Color.white, 10);
            // Original art is bottom-pivoted; place the feet on the boards.
            magicianSprite.transform.position = new Vector3(3.85f, 1.18f, -0.2f);
            magicianSprite.sharedMaterial = magicianMaterial;
            magician = magicianSprite.transform;
            // 腿单独画（材质里 _HideLegs 把剪影原来的腿藏掉），走路时迈步、屈膝、抬脚。
            for (int i = 0; i < 2; i++)
            {
                legs[i] = Line(i == 0 ? "BackLeg" : "FrontLeg", world, 0.12f, i == 0 ? Hex(0x1A2133) : Hex(0x222B42), 9, 3);
                legs[i].numCapVertices = 3;
                legs[i].numCornerVertices = 3;
                shoes[i] = Rect("Shoe", world, Vector2.zero, new Vector2(0.24f, 0.075f), Hex(0x15131C), 10).transform;
            }
            for (int i = 0; i < 2; i++)
            {
                arms[i] = Line("Sleeve", world, 0.11f, Hex(0x925576), 12, 3);
                gloves[i] = Group(i == 0 ? "LeftHand" : "RightHand", world, Vector2.zero);
                handRings[i] = Disc("SelectedHand", gloves[i], Vector2.zero, new Vector2(0.53f, 0.53f), new Color(0.25f, 0.9f, 0.78f, 0.3f), 30, true);
                Disc("Glove", gloves[i], Vector2.zero, new Vector2(0.29f, 0.32f), cream, 32);
                for (int finger = 0; finger < 4; finger++)
                    gloveFingers[i * 4 + finger] = Disc("Finger", gloves[i], new Vector2(-0.105f + finger * 0.069f, 0.135f), new Vector2(0.075f, 0.18f), cream, 32).transform;
                gloveThumbs[i] = Disc("Thumb", gloves[i], new Vector2(i == 0 ? 0.16f : -0.16f, -0.015f), new Vector2(0.14f, 0.09f), cream, 32).transform;
                Rect("Knuckles", gloves[i], new Vector2(0, 0.075f), new Vector2(0.24f, 0.018f), new Color(0.62f, 0.52f, 0.42f, 0f), 35);
                Rect("Cuff", gloves[i], new Vector2(0, -0.17f), new Vector2(0.27f, 0.07f), teal, 33);
            }

            box = Group("RabbitBox", world, new Vector2(5.85f, BoxBase));
            Rect("Interior", box, new Vector2(0, 0.45f), new Vector2(1.58f, 0.9f), Hex(0x101A28), 15);
            Rect("BoxFace", box, new Vector2(0, 0.44f), new Vector2(1.6f, 0.88f), Hex(0x3C6389), 18);
            emptyWindow = Rect("EmptyInteriorWindow", box, new Vector2(0, 0.68f), new Vector2(1.35f, 0.28f), Hex(0x111A29), 20);
            for (int i = -1; i <= 1; i += 2)
                Rect("BrassEdge", box, new Vector2(i * 0.75f, 0.45f), new Vector2(0.05f, 0.9f), gold, 19);
            Rect("BottomTrim", box, new Vector2(0, 0.03f), new Vector2(1.62f, 0.06f), gold, 19);
            var diamond = Rect("Emblem", box, new Vector2(0, 0.39f), new Vector2(0.24f, 0.24f), gold, 19);
            diamond.transform.localRotation = Quaternion.Euler(0, 0, 45);
            Rect("BoxDragHandle", box, new Vector2(0, 0.39f), new Vector2(0.23f, 0.07f), cream, 20);
            lid = Group("LidHinge", box, new Vector2(-0.78f, 0.93f));
            Rect("Lid", lid, new Vector2(0.78f, 0), new Vector2(1.7f, 0.15f), Hex(0x4C769C), 22);
            Rect("LidTrim", lid, new Vector2(0.78f, 0.075f), new Vector2(1.7f, 0.035f), gold, 23);
            Disc("LidHandle", lid, new Vector2(0.78f, 0.06f), new Vector2(0.25f, 0.13f), cream, 24);

            rabbit = Group("Rabbit", world, new Vector2(1.3f, 1.55f));
            Disc("Body", rabbit, new Vector2(0, -0.03f), new Vector2(0.43f, 0.48f), cream, 25);
            Disc("Tail", rabbit, new Vector2(-0.24f, -0.1f), new Vector2(0.18f, 0.18f), Color.white, 26);
            Disc("Head", rabbit, new Vector2(0.05f, 0.18f), new Vector2(0.35f, 0.34f), Color.white, 26);
            for (int i = 0; i < 2; i++)
            {
                float x = i == 0 ? -0.045f : 0.14f;
                Disc("Ear", rabbit, new Vector2(x, 0.45f), new Vector2(0.115f, 0.42f), Color.white, 26);
                Disc("PinkEar", rabbit, new Vector2(x, 0.46f), new Vector2(0.05f, 0.28f), Hex(0xD88D9D), 27);
            }
            Disc("Eye", rabbit, new Vector2(0.13f, 0.22f), new Vector2(0.045f, 0.06f), Hex(0x241E35), 28);
            Disc("Nose", rabbit, new Vector2(0.23f, 0.13f), new Vector2(0.065f, 0.05f), Hex(0xE699AA), 28);
            Rect("Ribbon", rabbit, new Vector2(0.02f, 0.045f), new Vector2(0.31f, 0.055f), Hex(0xBC5A6C), 28);
            Disc("RabbitRest", world, new Vector2(1.3f, 1.17f), new Vector2(0.9f, 0.12f), Hex(0x7F577D), 1);

            dancer = Group("DancingDecoy", world, new Vector2(8.25f, 1.4f));
            dancerBase = Group("DecoyWheels", world, new Vector2(8.25f, 1.3f));
            Rect("MusicBox", dancerBase, Vector2.zero, new Vector2(1.1f, 0.25f), Hex(0x8E684C), 11);
            Rect("MusicBoxTrim", dancerBase, new Vector2(0, 0.14f), new Vector2(1.16f, 0.04f), gold, 12);
            Rect("OpaqueCape", dancer, new Vector2(0, 0.7f), new Vector2(1.2f, 1.5f), Hex(0xB55061), 29);
            Rect("CapeTrim", dancer, new Vector2(0, -0.03f), new Vector2(1.2f, 0.06f), gold, 30);
            Disc("DecoyHead", dancer, new Vector2(0, 1.37f), new Vector2(0.31f, 0.34f), cream, 31);
            Rect("DecoyHat", dancer, new Vector2(0, 1.61f), new Vector2(0.31f, 0.23f), Hex(0x6F345C), 31);
            Rect("DecoyBrim", dancer, new Vector2(0, 1.48f), new Vector2(0.45f, 0.05f), gold, 31);
            for (int i = 0; i < 4; i++) dancerLimbs[i] = Line("DecoyLimb", dancer, 0.065f, cream, 30, 2);
            dancerHandle = Disc("WindUpHandle", world, new Vector2(8.97f, 2.45f), new Vector2(0.19f, 0.19f), gold, 31).transform;

            beam = Group("VisibleSpotlight", world, Vector2.zero);
            beamLine = Line("Cone", beam, 1f, BeamColor, 3, 2);
            beamLine.sharedMaterial = lineOverlayMaterial;
            beamLine.numCapVertices = 0;
            beamLine.startWidth = 0.48f;
            beamLine.endWidth = 2.9f;
            beamLine.SetPosition(0, new Vector3(0, 5.8f, 0));
            beamLine.SetPosition(1, new Vector3(0, 0.85f, 0));
            pool = Disc("LightPool", world, new Vector2(5.85f, 1.05f), new Vector2(2.95f, 0.4f), PoolColor, 2, true);
            BuildAudience();
            BuildCurtains();
            BuildHud();
        }

        // 背景细节：彩绘天幕（月亮、远山、星星）、护墙板、两盏壁灯、檐幕的扇形垂边和流苏、
        // 幕布的金色绑带、台口一排脚灯、地板前亮后暗。都只是画面，不参与判定。
        void BuildScenery()
        {
            Rect("Wainscot", world, new Vector2(5, 1.95f), new Vector2(10, 0.55f), Hex(0x3A3045), -28);
            Rect("WainscotTrim", world, new Vector2(5, 2.24f), new Vector2(10, 0.03f), Hex(0x8A6A4A), -27);
            Disc("PaintedMoon", world, new Vector2(7.2f, 4.35f), new Vector2(0.75f, 0.75f), Hex(0x6E6280), -28);
            Disc("MoonShade", world, new Vector2(7.36f, 4.42f), new Vector2(0.62f, 0.62f), Hex(0x453C50), -27);
            float[] hills = { 0.4f, 1.9f, 3.3f, 4.6f, 6.2f, 7.8f, 9.4f };
            for (int i = 0; i < hills.Length; i++)
                Disc("PaintedHill", world, new Vector2(hills[i], 2.3f), new Vector2(2.2f + (i % 3) * 0.5f, 0.9f + (i % 2) * 0.35f), Hex(0x3D3449), -28);
            var stars = new[] { new Vector2(1.4f, 4.5f), new Vector2(2.6f, 4.1f), new Vector2(3.7f, 4.7f), new Vector2(5.1f, 4.4f), new Vector2(8.4f, 4.6f), new Vector2(9.2f, 3.9f), new Vector2(6.1f, 4.8f) };
            foreach (var p in stars) Disc("PaintedStar", world, p, new Vector2(0.05f, 0.05f), Hex(0x9C8FA8), -27);
            // 两支壁挂火把：木柄 + 金色托架 + 会跳的火苗，墙上一圈随火苗忽明忽暗的暖光（Draw 里每帧更新）。
            torchFlames = new Transform[2];
            torchCores = new Transform[2];
            torchGlows = new SpriteRenderer[2];
            torchWallGlows = new SpriteRenderer[2];
            for (int side = 0; side < 2; side++)
            {
                float x = side == 0 ? 1.55f : 8.45f;
                torchWallGlows[side] = Glow("TorchWallGlow", new Vector2(x, 3.7f), 2.4f, new Color(1f, 0.62f, 0.28f, 0.16f), -26);
                Rect("TorchBracket", world, new Vector2(x, 3.18f), new Vector2(0.22f, 0.04f), gold, -25);
                var handle = Rect("TorchHandle", world, new Vector2(x, 3.36f), new Vector2(0.06f, 0.4f), Hex(0x5A3A28), -25);
                handle.transform.localRotation = Quaternion.Euler(0, 0, side == 0 ? -8f : 8f);
                Rect("TorchCup", world, new Vector2(x + (side == 0 ? 0.02f : -0.02f), 3.57f), new Vector2(0.14f, 0.07f), gold, -24);
                torchGlows[side] = Glow("TorchGlow", new Vector2(x, 3.78f), 0.9f, new Color(1f, 0.7f, 0.35f, 0.35f), -23);
                torchFlames[side] = Flame("TorchFlame", new Vector2(x, 3.6f), new Vector2(0.17f, 0.3f), Hex(0xF08A2E), -22);
                torchCores[side] = Flame("TorchFlameCore", new Vector2(x, 3.61f), new Vector2(0.09f, 0.17f), Hex(0xFFE7A6), -21);
            }
            Rect("FloorBackShade", world, new Vector2(5, 1.76f), new Vector2(10, 0.16f), new Color(0, 0, 0, 0.25f), -24);
            for (int i = 0; i < 22; i++)   // 檐幕下沿的扇形垂边
                Disc("ValanceScallop", world, new Vector2(i * 0.48f, 5.07f), new Vector2(0.5f, 0.26f), Hex(0x6F2841), 3);
            for (int i = 0; i < 22; i++)
                Disc("ValanceTassel", world, new Vector2(i * 0.48f + 0.24f, 4.96f), new Vector2(0.05f, 0.1f), gold, 4);
            for (int side = 0; side < 2; side++)
                Disc("CurtainTieback", world, new Vector2(side == 0 ? 0.32f : 9.68f, 2.25f), new Vector2(0.5f, 0.09f), gold, 5);
            for (int i = 0; i < 12; i++)   // 台口脚灯
            {
                var p = new Vector2(0.55f + i * 0.81f, 0.8f);
                var fg = Glow("FootlightGlow", p + Vector2.up * 0.08f, 0.55f, new Color(1f, 0.78f, 0.45f, 0.22f), -22);
                fg.transform.localScale = new Vector3(fg.transform.localScale.x, fg.transform.localScale.y * 0.5f, 1f);
                Disc("Footlight", world, p, new Vector2(0.14f, 0.08f), Hex(0xF3D293), -21, true);
            }
        }

        SpriteRenderer Glow(string name, Vector2 position, float size, Color color, int order)
        {
            var sr = Sprite(name, world, glowSprite != null ? glowSprite : circle, position, new Vector2(size, size), color, order);
            sr.sharedMaterial = overlayMaterial;
            return sr;
        }

        Transform Flame(string name, Vector2 position, Vector2 size, Color color, int order)
        {
            // 火苗的 pivot 在底部，跳动时根部不动、火尖晃
            var pivot = Group(name, world, position);
            var sr = Sprite("Shape", pivot, flameSprite != null ? flameSprite : circle, new Vector2(0, size.y / 2f), size, color, order);
            sr.sharedMaterial = overlayMaterial;
            return pivot;
        }

        // 火把：两层噪声驱动火苗的高矮、宽窄、摆动和光晕亮度，两支火把各用各的噪声，不同步。
        void DrawTorches()
        {
            float t = Time.time;
            for (int i = 0; i < torchFlames.Length; i++)
            {
                if (torchFlames[i] == null) continue;
                float slow = Mathf.PerlinNoise(t * 1.7f, i * 13.1f);
                float fast = Mathf.PerlinNoise(t * 9.3f, i * 7.7f + 3f);
                float flick = 0.6f * slow + 0.4f * fast;
                float sway = (Mathf.PerlinNoise(t * 3.1f, i * 4.2f + 9f) - 0.5f) * 16f;
                torchFlames[i].localScale = new Vector3(0.85f + 0.3f * fast, 0.75f + 0.55f * flick, 1f);
                torchFlames[i].localRotation = Quaternion.Euler(0, 0, sway);
                torchCores[i].localScale = new Vector3(0.8f + 0.25f * slow, 0.7f + 0.5f * fast, 1f);
                torchCores[i].localRotation = Quaternion.Euler(0, 0, sway * 0.6f);
                SetAlpha(torchGlows[i], 0.22f + 0.25f * flick, 0.85f + 0.3f * flick, 0.9f);
                SetAlpha(torchWallGlows[i], 0.08f + 0.12f * slow, 0.9f + 0.2f * slow, 2.4f);
            }
        }

        void SetAlpha(SpriteRenderer sr, float alpha, float scale, float baseSize)
        {
            if (sr == null) return;
            var c = sr.color;
            c.a = alpha;
            sr.color = c;
            var art = sr.sprite;
            float k = baseSize * scale / Mathf.Max(0.01f, art.bounds.size.x);
            sr.transform.localScale = new Vector3(k, k, 1f);
        }

        // 台下观众：一排黑色背影，压在画面底边。头绕脖子转，跟着光圈看（Draw 里每帧更新）。
        void BuildAudience()
        {
            bool hasArt = audienceBodies != null && audienceHeads != null && audienceBodies.Length > 0
                && audienceBodies.Length == audienceHeads.Length && audienceBodies[0] != null && audienceHeads[0] != null;
            const int count = 11;
            audienceHeadPivots = new Transform[count];
            audienceFollow = new float[count];
            var rng = new System.Random(7);
            for (int i = 0; i < count; i++)
            {
                float x = 0.4f + i * 0.92f + (float)(rng.NextDouble() - 0.5) * 0.18f;
                float y = 0.05f - (float)rng.NextDouble() * 0.1f;   // 头顶落在台口金边附近，压住舞台下沿
                bool flip = rng.Next(2) == 0;
                audienceFollow[i] = 1.5f + 3f * (float)rng.NextDouble();
                if (hasArt)
                {
                    int kind = rng.Next(audienceBodies.Length);
                    var body = Sprite("AudienceBack", world, audienceBodies[kind], new Vector2(x, y), new Vector2(1.0f, 1.2f), Color.white, 45);
                    body.sharedMaterial = overlayMaterial;
                    body.flipX = flip;
                    // 头图的 pivot 在脖子（自下而上 0.375），所以放在身体底边往上 0.375 × 高度处，转起来绕着脖子。
                    var head = Sprite("AudienceHead", world, audienceHeads[kind], new Vector2(x, y + 1.2f * 0.375f), new Vector2(1.0f, 1.2f), Color.white, 46);
                    head.sharedMaterial = overlayMaterial;
                    head.flipX = flip;
                    audienceHeadPivots[i] = head.transform;
                }
                else
                {
                    var body = Disc("AudienceBack", world, new Vector2(x, 0.3f), new Vector2(0.8f, 0.6f), Hex(0x3A3046), 45, true);
                    var head = Group("AudienceHeadPivot", world, new Vector2(x, 0.55f));
                    Disc("AudienceHead", head, new Vector2(0, 0.2f), new Vector2(0.36f, 0.4f), Hex(0x3A3046), 46, true);
                    audienceHeadPivots[i] = head;
                }
            }
        }

        // 两片大幕：每片若干褶，挂点在幕杆上，褶子从挂点往下垂。由 StageCurtains 模拟拉合。
        void BuildCurtains()
        {
            if (curtains == null) return;
            const int foldsPerSide = 9;
            const float top = 5.02f, length = 4.45f;
            curtains.left = new Transform[foldsPerSide];
            curtains.right = new Transform[foldsPerSide];
            for (int side = 0; side < 2; side++)
            {
                for (int i = 0; i < foldsPerSide; i++)
                {
                    var hang = Group(side == 0 ? "CurtainL" : "CurtainR", world, new Vector2(side == 0 ? 0f : 10f, top));
                    hang.localPosition = new Vector3(hang.localPosition.x, top, -0.5f);
                    Color c = i % 3 == 0 ? Hex(0x8A3350) : i % 3 == 1 ? Hex(0x772A43) : Hex(0x562034);
                    Rect("Fold", hang, new Vector2(0, -length / 2f), new Vector2(1f, length), c, 40);
                    Rect("Hem", hang, new Vector2(0, -length + 0.04f), new Vector2(1f, 0.08f), gold, 41);
                    if (side == 0) curtains.left[i] = hang; else curtains.right[i] = hang;
                }
            }
            curtains.SnapOpen();
        }

        public void UseRuntimeFont()
        {
            if (Application.platform == RuntimePlatform.WebGLPlayer) return;
            runtimeFont = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "Noto Sans CJK SC", "PingFang SC", "Arial Unicode MS", "Arial" }, 22);
            if (runtimeFont == null) return;
            foreach (var text in GetComponentsInChildren<Text>(true)) text.font = runtimeFont;
        }

        // 团队决定游戏内文字全部用英文（中文文案仍留在代码里，改回只需让这里返回 true）。
        public bool Chinese => false;

        void BuildHud()
        {
            var ui = new GameObject("RabbitHUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            ui.transform.SetParent(transform, false);
            ui.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            ui.GetComponent<Canvas>().sortingOrder = 100;
            ui.GetComponent<Canvas>().overrideSorting = true;
            var scaler = ui.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            scaler.matchWidthOrHeight = 0.5f;
            var parent = ui.transform;
            Label("Title", parent, new Vector2(35, -24), new Vector2(470, 40), "SLEIGHT  /  THE RABBIT", 25, cream);
            handLabel = Label("ActiveHand", parent, new Vector2(35, -64), new Vector2(1000, 34), "LEFT HAND", 21, teal);
            var panel = Panel("Steps", parent, new Vector2(-445, -22), new Vector2(420, 210), new Color(0.07f, 0.055f, 0.11f, 0.93f), true);
            stepsTitle = Label("StepsTitle", panel.transform, new Vector2(20, -13), new Vector2(395, 30), "1st TRICK: THE RABBIT FROM AN EMPTY BOX", 17, gold);
            string[] names = { "01  SHOW THE EMPTY BOX", "02  HIDE THE RABBIT", "03  REVEAL THE RABBIT" };
            for (int i = 0; i < 3; i++)
            {
                stepBars[i] = Panel("StepBar", panel.transform, new Vector2(18, -55 - i * 47), new Vector2(4, 28), teal);
                stepLabels[i] = Label("Step", panel.transform, new Vector2(34, -53 - i * 47), new Vector2(365, 32), names[i], 20, cream);
            }
            showProgress = Panel("ShowProgress", panel.transform, new Vector2(34, -86), new Vector2(340, 3), teal);
            showProgress.type = Image.Type.Filled;
            showProgress.fillMethod = Image.FillMethod.Horizontal;
            gazeLabel = Label("Gaze", parent, new Vector2(-445, -243), new Vector2(420, 32), "WATCH THE LIGHT", 19, gold, true);
            distractionProgress = Panel("DanceTime", parent, new Vector2(-445, -278), new Vector2(420, 4), teal, true);
            distractionProgress.type = Image.Type.Filled;
            distractionProgress.fillMethod = Image.FillMethod.Horizontal;
            hintLabel = Label("Hint", parent, new Vector2(130, -812), new Vector2(1340, 40), "", 23, cream);   // 压在台口下沿以下，不挡舞台
            hintLabel.alignment = TextAnchor.MiddleCenter;
            controls = Label("Controls", parent, new Vector2(100, -857), new Vector2(1400, 36), "ARROWS / WASD  Move hand     F  Switch hand     E  Grab / release / pass     R  Restart", 19, new Color(0.8f, 0.76f, 0.85f));
            controls.alignment = TextAnchor.MiddleCenter;
            resultPanel = Panel("Result", parent, new Vector2(340, -300), new Vector2(920, 250), new Color(0.07f, 0.05f, 0.11f, 0.97f)).gameObject;
            resultTitle = Label("ResultTitle", resultPanel.transform, new Vector2(40, -30), new Vector2(840, 70), "", 38, cream);
            resultTitle.alignment = TextAnchor.MiddleCenter;
            resultBody = Label("ResultBody", resultPanel.transform, new Vector2(40, -118), new Vector2(840, 100), "", 23, cream);
            resultBody.alignment = TextAnchor.MiddleCenter;
            resultPanel.SetActive(false);
            // 观众耐心：画面最左侧一根竖条，从下往上填，随时间往下掉。
            patienceBack = Panel("PatienceBack", parent, new Vector2(10, -190), new Vector2(18, 500), new Color(0.07f, 0.055f, 0.11f, 0.9f));
            patienceFill = Panel("PatienceFill", patienceBack.transform, new Vector2(3, -3), new Vector2(12, 494), teal);
            patienceFill.type = Image.Type.Filled;
            patienceFill.fillMethod = Image.FillMethod.Vertical;
            patienceFill.fillOrigin = (int)Image.OriginVertical.Bottom;
            patienceLabel = Label("PatienceLabel", parent, new Vector2(4, -160), new Vector2(120, 26), "PATIENCE", 15, gold);
            flash = Panel("CaughtFlash", parent, Vector2.zero, new Vector2(1600, 900), new Color(1, 0.7f, 0.6f, 0));
            endingLabel = resultTitle;
        }

        // 第 1 关的道具只在第 1 关出现；第 2、3 关的道具由各自的 StageTrick 管。
        public void SetLevel(int level)
        {
            bool rabbitLevel = level == 0;
            foreach (var t in new[] { box, rabbit, dancer, dancerBase, dancerHandle })
                if (t != null) t.gameObject.SetActive(rabbitLevel);
            var rest = world != null ? world.Find("RabbitRest") : null;
            if (rest != null) rest.gameObject.SetActive(rabbitLevel);
            GloveAlpha[0] = GloveAlpha[1] = 1f;
        }

        public void Draw(RabbitTrickGame game)
        {
            box.position = new Vector3(game.BoxX, BoxBase, 0);
            DrawMagician(game.Hands.BodyPosition);
            lid.localRotation = Quaternion.Euler(0, 0, game.LidAmount * 70f);
            emptyWindow.enabled = game.LidAmount > 0.4f;
            rabbit.position = game.RabbitPosition;
            rabbit.gameObject.SetActive(game.Trick == null && (!game.Rules.RabbitInside || game.LidAmount > 0.75f || game.Rules.Finished));
            if (game.Rules.RabbitInside)
            {
                rabbit.position = new Vector3(game.BoxX, 2.19f, 0);
                rabbit.localScale = Vector3.one * 0.85f;
            }
            else rabbit.localScale = Vector3.one;
            beam.position = new Vector3(game.Audience.X, 0, 0);
            // 大幕合拢时追光被挡住：光柱、地上的光圈和舞台上的打光一起随幕布合拢淡掉
            float hidden = curtains != null ? curtains.Closedness : 0f;
            Shader.SetGlobalFloat("_RabbitSpotHidden", hidden);
            if (beamLine != null)
            {
                var bc = BeamColor; bc.a *= 1f - hidden;
                beamLine.startColor = beamLine.endColor = bc;
            }
            var pc = PoolColor; pc.a *= 1f - hidden;
            pool.color = pc;
            pool.transform.position = new Vector3(game.Audience.X, 1.05f, 0);
            for (int i = 0; i < 2; i++)
            {
                Vector2 hand = game.Hands.Hands[i].position;
                gloves[i].position = hand;
                DrawGrip(i, game.Hands.Hands[i].gripping);
                foreach (var sr in gloves[i].GetComponentsInChildren<SpriteRenderer>())
                {
                    if (sr == handRings[i] || sr.name == "Knuckles") continue;
                    var gc = sr.color; gc.a = GloveAlpha[i]; sr.color = gc;
                }
                handRings[i].enabled = i == game.Hands.Active;
                float pulse = 1f + 0.06f * Mathf.Sin(Time.time * 6);
                handRings[i].transform.localScale = new Vector3(0.53f / circle.bounds.size.x, 0.53f / circle.bounds.size.y, 1) * pulse;
                Vector3 shoulder = game.Hands.Shoulder(i);
                // 袖子从身体真正的肩膀出发：跟着走路起伏一起动，否则胳膊和身体会脱开
                arms[i].SetPosition(0, shoulder + bodyOffset);
                arms[i].SetPosition(1, (Vector3)game.Hands.Elbow(i) + bodyOffset * 0.5f);
                arms[i].SetPosition(2, hand);
                arms[i].sortingOrder = game.Hands.BodyPosition.y > 1.4f ? 12 : 25;
            }
            float dance = game.Audience.Dancing ? Mathf.Sin(Time.time * 10) : 0;
            dancer.localRotation = Quaternion.identity;
            dancer.position = new Vector3(game.Audience.DancerX, 1.4f, -0.35f);
            dancerBase.position = new Vector3(game.Audience.DancerX, 1.3f, -0.35f);
            dancerHandle.position = game.DancerHandle;
            for (int i = 0; i < 4; i++)
            {
                float side = i % 2 == 0 ? -1 : 1;
                bool leg = i >= 2;
                dancerLimbs[i].SetPosition(0, new Vector3(side * 0.19f, leg ? 0.1f : 1.15f, 0));
                dancerLimbs[i].SetPosition(1, new Vector3(side * (leg ? 0.33f : 0.78f), (leg ? -0.14f : 0.83f) + dance * side * 0.15f, 0));
            }
            DrawAudience(game.Audience.X);
            DrawTorches();
            game.Trick?.Draw();
            UpdateHud(game);
        }

        // 程序化走路：按走过的距离推进步伐相位，两腿相差半个周期；脚向前迈、离地抬起，
        // 膝盖用两段固定长度的 IK 往前弯；身体随步伐上下起伏，朝走的方向转身。
        void DrawMagician(Vector2 body)
        {
            float dt = Mathf.Max(Time.deltaTime, 0.0001f);
            if (!hasLastBody) { lastBody = body; hasLastBody = true; }
            Vector2 delta = body - lastBody;
            lastBody = body;
            float speed = delta.magnitude / dt;
            walkBlend = Mathf.MoveTowards(walkBlend, speed > 0.15f ? 1f : 0f, dt * 7f);
            if (Mathf.Abs(delta.x) > 0.0004f) facingLeft = delta.x < 0;
            walkPhase += delta.magnitude / Stride * Mathf.PI * 2f;
            if (walkBlend <= 0f) walkPhase = Mathf.Lerp(walkPhase, Mathf.Round(walkPhase / Mathf.PI) * Mathf.PI, dt * 8f);

            float bob = Mathf.Abs(Mathf.Sin(walkPhase)) * 0.04f * walkBlend;
            float breathe = Mathf.Sin(Time.time * 2.2f) * 0.008f * (1f - walkBlend);
            var sr = magician.GetComponent<SpriteRenderer>();
            sr.flipX = facingLeft;
            bodyOffset = new Vector3(0f, bob + breathe, 0f);
            magician.position = new Vector3(body.x, body.y, -0.2f) + bodyOffset;
            int baseOrder = body.y > 1.4f ? 10 : 24;
            sr.sortingOrder = baseOrder;

            float face = facingLeft ? -1f : 1f;
            for (int i = 0; i < 2; i++)
            {
                float ph = walkPhase + i * Mathf.PI;
                Vector2 hip = new Vector2(body.x + face * (i == 0 ? -0.04f : 0.05f), body.y + HipHeight + bob);
                float swing = Mathf.Sin(ph);
                Vector2 foot = new Vector2(hip.x + face * swing * 0.24f * walkBlend,
                    body.y + 0.05f + Mathf.Max(0f, Mathf.Cos(ph)) * 0.11f * walkBlend);
                Vector2 knee = Knee(hip, foot, face);
                legs[i].SetPosition(0, hip);
                legs[i].SetPosition(1, knee);
                legs[i].SetPosition(2, foot);
                legs[i].sortingOrder = baseOrder - (i == 0 ? 2 : 1);
                shoes[i].position = new Vector3(foot.x + face * 0.07f, foot.y - 0.02f, 0);
                shoes[i].localRotation = Quaternion.Euler(0, 0, face * Mathf.Max(0f, Mathf.Cos(ph)) * 12f * walkBlend);
                shoes[i].GetComponent<SpriteRenderer>().sortingOrder = baseOrder - (i == 0 ? 2 : 1);
            }
        }

        // 抓着东西时握拳：四根手指缩短、往掌心收，大拇指压到指头前面，指节露出一道暗线；
        // 空手时张开、手指略微散开。两种形态之间快速插值，按住 / 松开 E 时看得到手在动。
        void DrawGrip(int i, bool holding)
        {
            if (grip == null || grip.Length != 2) grip = new float[2];
            grip[i] = Mathf.MoveTowards(grip[i], holding ? 1f : 0f, Time.deltaTime * 9f);
            float g = Mathf.SmoothStep(0f, 1f, grip[i]);
            float side = i == 0 ? 1f : -1f;
            for (int f = 0; f < 4; f++)
            {
                var finger = gloveFingers[i * 4 + f];
                if (finger == null) continue;
                float baseX = -0.105f + f * 0.069f;
                float spread = (f - 1.5f) * 0.014f * (1f - g);
                finger.localPosition = new Vector3(Mathf.Lerp(baseX + spread, baseX * 0.85f, g), Mathf.Lerp(0.135f, 0.05f, g), 0);
                SetSize(finger, new Vector2(0.075f, Mathf.Lerp(0.18f, 0.085f, g)));
                finger.localRotation = Quaternion.Euler(0, 0, (f - 1.5f) * Mathf.Lerp(-7f, 3f, g));
            }
            var thumb = gloveThumbs[i];
            if (thumb != null)
            {
                thumb.localPosition = new Vector3(side * Mathf.Lerp(0.16f, 0.05f, g), Mathf.Lerp(-0.015f, 0.025f, g), 0);
                thumb.localRotation = Quaternion.Euler(0, 0, side * Mathf.Lerp(0f, 35f, g));
                thumb.GetComponent<SpriteRenderer>().sortingOrder = g > 0.5f ? 34 : 32;   // 握拳时大拇指压在指头前面
            }
            var knuckles = gloves[i].Find("Knuckles");
            if (knuckles != null)
            {
                var sr = knuckles.GetComponent<SpriteRenderer>();
                var c = sr.color;
                c.a = g * 0.9f;
                sr.color = c;
            }
        }

        void SetSize(Transform t, Vector2 size)
        {
            var art = t.GetComponent<SpriteRenderer>().sprite;
            t.localScale = new Vector3(size.x / Mathf.Max(0.01f, art.bounds.size.x), size.y / Mathf.Max(0.01f, art.bounds.size.y), 1);
        }

        // 耐心条：绿 → 黄 → 红，剩下不到四分之一时闪动、轻微抖。
        void DrawPatience(RabbitTrickGame game)
        {
            if (patienceFill == null) return;
            float p = game.Patience;
            patienceFill.fillAmount = p;
            Color green = teal, yellow = new Color32(232, 190, 80, 255), red = new Color32(214, 76, 70, 255);
            Color col = p > 0.5f ? Color.Lerp(yellow, green, (p - 0.5f) * 2f) : Color.Lerp(red, yellow, p * 2f);
            if (p < 0.25f && !game.IsFailed) col = Color.Lerp(col, Color.white, 0.35f * (0.5f + 0.5f * Mathf.Sin(Time.time * 12f)));
            patienceFill.color = col;
            var rt = patienceBack.rectTransform;
            float shake = p < 0.25f && !game.IsFailed && !game.IsFinished ? Mathf.Sin(Time.time * 40f) * 2f * (1f - p * 4f) : 0f;
            rt.anchoredPosition = new Vector2(10 + shake, -190);
        }

        // 耐心耗尽：台下的观众朝魔术师扔鸡蛋和白菜。东西沿抛物线飞过去，鸡蛋砸中就碎成一摊，
        // 白菜砸中后弹开、掉到地上滚一下。持续几秒，之后结算面板照常弹出。
        sealed class Thrown
        {
            public Transform t; public SpriteRenderer main, detail;
            public Vector2 from, to; public float age, flight, spin; public bool egg, landed;
            public Vector2 vel;
        }

        void DrawThrown(RabbitTrickGame game)
        {
            float dt = Time.deltaTime;
            if (!game.Booed)
            {
                if (thrown.Count > 0) { foreach (var item in thrown) if (item.t != null) Destroy(item.t.gameObject); thrown.Clear(); }
                throwTimer = 0f;
                return;
            }
            if (game.ResultAge < 3.5f)
            {
                throwTimer -= dt;
                if (throwTimer <= 0f)
                {
                    throwTimer = 0.12f + (float)throwRng.NextDouble() * 0.18f;
                    Throw(game);
                }
            }
            foreach (var item in thrown)
            {
                item.age += dt;
                if (!item.landed)
                {
                    float k = Mathf.Clamp01(item.age / item.flight);
                    Vector2 p = Vector2.Lerp(item.from, item.to, k) + Vector2.up * Mathf.Sin(k * Mathf.PI) * 1.6f;
                    item.t.position = new Vector3(p.x, p.y, -0.6f);
                    item.t.localRotation = Quaternion.Euler(0, 0, item.spin * item.age);
                    if (k >= 1f) Land(item);
                }
                else if (!item.egg)
                {
                    // 白菜弹开后受重力落到地板上
                    item.vel += Vector2.down * 9.8f * dt;
                    var p = (Vector2)item.t.position + item.vel * dt;
                    if (p.y < 1.25f) { p.y = 1.25f; item.vel = new Vector2(item.vel.x * 0.6f, Mathf.Abs(item.vel.y) * 0.3f); }
                    item.t.position = new Vector3(p.x, p.y, -0.6f);
                    item.t.localRotation = Quaternion.Euler(0, 0, item.t.localEulerAngles.z - item.vel.x * 120f * dt);
                }
            }
        }

        void Throw(RabbitTrickGame game)
        {
            Vector2 body = game.Hands.BodyPosition;
            float fromX = audienceHeadPivots.Length > 0 && audienceHeadPivots[throwRng.Next(audienceHeadPivots.Length)] != null
                ? audienceHeadPivots[throwRng.Next(audienceHeadPivots.Length)].position.x : (float)throwRng.NextDouble() * 10f;
            var item = new Thrown
            {
                egg = throwRng.Next(3) != 0,
                from = new Vector2(fromX, 0.7f),
                to = body + new Vector2(((float)throwRng.NextDouble() - 0.5f) * 0.7f, 0.6f + (float)throwRng.NextDouble() * 1.4f),
                flight = 0.55f + (float)throwRng.NextDouble() * 0.3f,
                spin = ((float)throwRng.NextDouble() - 0.5f) * 900f,
            };
            item.t = Group(item.egg ? "ThrownEgg" : "ThrownCabbage", world, item.from);
            if (item.egg)
            {
                item.main = Disc("Egg", item.t, Vector2.zero, new Vector2(0.13f, 0.17f), Hex(0xF4EEDC), 60, true);
            }
            else
            {
                item.main = Disc("Cabbage", item.t, Vector2.zero, new Vector2(0.3f, 0.28f), Hex(0x6FA84A), 60, true);
                item.detail = Disc("CabbageLeaf", item.t, new Vector2(0.03f, 0.02f), new Vector2(0.18f, 0.2f), Hex(0x9BCB6A), 61, true);
            }
            thrown.Add(item);
        }

        void Land(Thrown item)
        {
            item.landed = true;
            if (item.egg)
            {
                // 砸碎：变成一摊蛋清加蛋黄，贴在打中的地方
                item.t.localRotation = Quaternion.Euler(0, 0, (float)throwRng.NextDouble() * 360f);
                SetSize(item.main.transform, new Vector2(0.32f, 0.2f));
                item.main.color = new Color(0.97f, 0.95f, 0.88f, 0.92f);
                Disc("Yolk", item.t, new Vector2(0.02f, 0f), new Vector2(0.1f, 0.09f), Hex(0xF2B83A), 61, true);
            }
            else
            {
                item.vel = new Vector2(((float)throwRng.NextDouble() - 0.5f) * 3f, 1.5f + (float)throwRng.NextDouble());
            }
        }

        Text demoBanner;

        // 演示时画面上方正中的大标题："HOW IT'S DONE"，提示这是演示、可以跳过
        void DrawDemoBanner(RabbitTrickGame game)
        {
            if (demoBanner == null)
            {
                var font = hintLabel.font;
                demoBanner = Label("DemoBanner", hintLabel.transform.parent, new Vector2(400, -150), new Vector2(800, 60), "", 34, gold);
                demoBanner.font = font;
                demoBanner.alignment = TextAnchor.MiddleCenter;
            }
            demoBanner.gameObject.SetActive(game.InDemo);
            if (!game.InDemo) return;
            demoBanner.text = "DEMO  ·  " + Ordinal(game.Level + 1).ToUpper() + " TRICK: " + game.TrickName;
            var c = gold;
            c.a = 0.75f + 0.25f * Mathf.Sin(Time.time * 3f);
            demoBanner.color = c;
            demoBanner.resizeTextForBestFit = true;
            demoBanner.resizeTextMinSize = 16;
            demoBanner.resizeTextMaxSize = 34;
        }

        static string Ordinal(int n)
        {
            int mod100 = n % 100;
            string suffix = mod100 >= 11 && mod100 <= 13 ? "th" : (n % 10) switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" };
            return n + suffix;
        }

        static Vector2 Knee(Vector2 hip, Vector2 foot, float face)
        {
            Vector2 d = foot - hip;
            float dist = Mathf.Clamp(d.magnitude, 0.05f, Thigh + Shin - 0.001f);
            Vector2 axis = d.sqrMagnitude > 0.0001f ? d.normalized : Vector2.down;
            float along = (Thigh * Thigh - Shin * Shin + dist * dist) / (2f * dist);
            float h = Mathf.Sqrt(Mathf.Max(0f, Thigh * Thigh - along * along));
            Vector2 perp = new Vector2(-axis.y, axis.x);
            if (Vector2.Dot(perp, new Vector2(face, 0f)) < 0) perp = -perp;   // 膝盖朝前弯
            return hip + axis * along + perp * h;
        }

        // 观众的头跟着光圈转：从背后看，往右看 = 后脑勺朝右歪。每个人反应快慢不一样。
        void DrawAudience(float lightX)
        {
            float dt = Time.deltaTime;
            for (int i = 0; i < audienceHeadPivots.Length; i++)
            {
                var head = audienceHeadPivots[i];
                if (head == null) continue;
                float dx = lightX - head.position.x;
                float target = -Mathf.Clamp(dx * 4.5f, -16f, 16f);
                float k = 1f - Mathf.Exp(-(audienceFollow.Length > i ? audienceFollow[i] : 3f) * dt);
                float z = Mathf.LerpAngle(head.localEulerAngles.z, target, k);
                head.localRotation = Quaternion.Euler(0, 0, z);
            }
        }

        void UpdateHud(RabbitTrickGame game)
        {
            bool zh = Chinese;
            string[] names = zh && game.Trick == null ? new[] { "01  向观众展示空箱", "02  在暗处藏入兔子并关箱", "03  当着观众开箱变兔子" }
                : game.StepNames;
            int step = game.StepIndex;
            for (int i = 0; i < 3; i++)
            {
                stepLabels[i].resizeTextForBestFit = true;
                stepLabels[i].resizeTextMinSize = 12;
                stepLabels[i].resizeTextMaxSize = 20;
                stepLabels[i].text = (i < step ? (zh ? "完成  " : "DONE  ") : "") + (i < names.Length ? names[i] : "");
                stepLabels[i].color = i < step ? teal : i == step ? cream : new Color(0.5f, 0.46f, 0.56f);
                stepBars[i].color = i <= step ? teal : new Color(0.25f, 0.22f, 0.3f);
            }
            showProgress.fillAmount = game.StepProgress;
            // 进度条放在当前这一步下面
            showProgress.rectTransform.anchoredPosition = new Vector2(34, -86 - Mathf.Clamp(step, 0, 2) * 47);
            if (stepsTitle != null) stepsTitle.text = Ordinal(game.Level + 1) + " TRICK: " + game.TrickName;
            handLabel.text = zh ? (game.Hands.Active == 0 ? "正在控制：左手" : "正在控制：右手") + "    F 切换    按住 E 抓取 / 松开 E 放下"
                : (game.Hands.Active == 0 ? "CONTROLLING: LEFT HAND" : "CONTROLLING: RIGHT HAND") + "    F  SWITCH    HOLD E  GRAB / LET GO  DROP";
            gazeLabel.text = game.Audience.Dancing
                ? (zh ? "跳舞小人吸引中  " : "DISTRACTED  ") + Mathf.CeilToInt(game.Audience.DanceRemaining) + "s"
                : (zh ? (game.Audience.Moving ? "观众正在转移目光" : "观众正看着亮处") : (game.Audience.Moving ? "THE AUDIENCE IS LOOKING AWAY" : "THE AUDIENCE IS WATCHING THE LIGHT"));
            distractionProgress.fillAmount = game.Audience.DanceRemaining / 12f;
            if (game.Audience.Dancing && game.Audience.CoversRabbit(game.RabbitPosition) && game.Audience.Illuminates(game.RabbitPosition))
                gazeLabel.text += zh ? "  ·  兔子被遮住了" : "  / RABBIT COVERED";
            hintLabel.text = game.InDemo ? game.DemoCaption : game.Hint(zh);
            DrawDemoBanner(game);
            if (game.InDemo) controls.text = "HOW TO PLAY THIS TRICK  /  PRESS ANY KEY TO SKIP";
            else controls.text = zh ? "WASD  移动人物     方向键  移动手     F  切换手     按住 E  抓取（松开放下）     R  重试"
                : "WASD  Walk     ARROWS  Move hand     F  Switch hand     Hold E  Grab (let go to drop)     R  Retry";
            // 被轰下台时先让东西飞一会儿，再弹结算面板，免得面板挡住魔术师
            resultPanel.SetActive((game.IsFailed || game.IsFinished) && !game.ChangingLevel && !game.InDemo && !(game.Booed && game.ResultAge < 3.5f));
            if (game.IsFailed)
            {
                resultTitle.text = zh ? "露馅了！" : game.Booed ? "BOOED OFF THE STAGE!" : "CAUGHT!";
                resultBody.text = zh ? game.FailureReason + "\n按 R 重试这场魔术" : game.FailureReason + "\nPress R to try again.";
            }
            else if (game.IsFinished)
            {
                resultTitle.text = zh && game.Trick == null ? "空箱变出了兔子！" : game.SuccessTitle;
                bool last = game.Level >= RabbitTrickGame.LevelCount - 1;
                resultBody.text = zh ? "魔术成功" : Ordinal(game.Level + 1).ToUpper() + " TRICK COMPLETE  /  " + (last ? "THE SHOW STARTS AGAIN" : "NEXT TRICK COMING UP");
            }
            var c = flash.color;
            c.a = game.IsFailed && !game.Booed ? Mathf.Max(0, 0.45f - game.ResultAge) : 0;
            flash.color = c;
            DrawPatience(game);
            DrawThrown(game);
        }

        public Transform Group(string name, Transform parent, Vector2 position)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            return go.transform;
        }

        public SpriteRenderer Rect(string name, Transform parent, Vector2 position, Vector2 size, Color color, int order) =>
            Sprite(name, parent, square, position, size, color, order);

        public SpriteRenderer Disc(string name, Transform parent, Vector2 position, Vector2 size, Color color, int order, bool unlit = false)
        {
            var sr = Sprite(name, parent, circle, position, size, color, order);
            if (unlit) sr.sharedMaterial = overlayMaterial;
            return sr;
        }

        SpriteRenderer Sprite(string name, Transform parent, Sprite art, Vector2 position, Vector2 size, Color color, int order)
        {
            Transform child = Group(name, parent, position);
            var sr = child.gameObject.AddComponent<SpriteRenderer>();
            sr.sprite = art;
            sr.sharedMaterial = stageMaterial;
            sr.color = color;
            sr.sortingOrder = order;
            if (order <= -24) child.localPosition = new Vector3(position.x, position.y, 3f);
            if (art != null)
                child.localScale = new Vector3(size.x / Mathf.Max(0.01f, art.bounds.size.x), size.y / Mathf.Max(0.01f, art.bounds.size.y), 1);
            return sr;
        }

        public LineRenderer Line(string name, Transform parent, float width, Color color, int order, int count)
        {
            var go = Group(name, parent, Vector2.zero).gameObject;
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.sharedMaterial = lineMaterial;
            line.positionCount = count;
            line.startWidth = line.endWidth = width;
            line.startColor = line.endColor = color;
            line.sortingOrder = order;
            line.numCapVertices = 5;
            return line;
        }

        Image Panel(string name, Transform parent, Vector2 position, Vector2 size, Color color, bool right = false)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(right ? 1 : 0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            var image = go.GetComponent<Image>();
            image.sprite = square;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        Text Label(string name, Transform parent, Vector2 position, Vector2 size, string value, int fontSize, Color color, bool right = false)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(right ? 1 : 0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            var text = go.GetComponent<Text>();
            text.font = interfaceFont;
            text.fontSize = fontSize;
            text.color = color;
            text.text = value;
            text.raycastTarget = false;
            return text;
        }

        static Color Hex(uint rgb) => new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);
    }
}
