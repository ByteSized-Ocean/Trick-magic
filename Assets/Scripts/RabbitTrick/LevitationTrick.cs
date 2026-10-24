using UnityEngine;

namespace Sleight.RabbitTrick
{
    // 第 3 关：悬浮术（THE FLOATING LADY）。
    // 桌布底下藏着一根拉杆：一只手伸到桌布下往上推，躺在木板上的助手就浮起来；一松手她就掉下去。
    // 1. 让她浮在灯下（停满 1 秒）。
    // 2. 用另一只手拿起铁环，在灯下从她身体一头套到另一头，证明"没有线"。
    // 3. 慢慢放她回到桌上。
    // 撑着拉杆的手被钉在桌子底下，身体走不远，套环那只手要在够得着的范围里完成整段动作。
    public sealed class LevitationTrick : StageTrick
    {
        public override string Name => "THE FLOATING LADY";
        public override string SuccessTitle => "SHE FLOATED ON THIN AIR!";
        public override string[] StepNames { get; } =
            { "01  RAISE HER INTO THE LIGHT", "02  PASS THE HOOP AROUND HER", "03  LOWER HER BACK DOWN" };
        public override float StartLightX => TableX;

        const float TableX = 5.2f, Floor = 1.18f, ClothTop = 1.95f, TableHalf = 1.0f;
        const float LeverX = 5.52f, LeverLowY = 1.55f, LeverTravel = 0.35f;
        const float BoardRestY = 2.02f, BoardRise = 1.0f, BoardHalf = 0.6f;
        const float HoopRadius = 0.45f, StandX = 1.7f, StandTop = 1.62f;
        const int LeverId = 0, HoopId = 1;
        const float FloatAt = 0.85f;

        float lift, liftVel;           // 0 = 躺在桌上，1 = 最高
        float floatTime;
        int hoopFrom;                  // 套环从哪一侧开始：-1 左，+1 右，0 还没开始
        readonly LooseProp hoop = new LooseProp { shape = new Rect(-HoopRadius, 0f, HoopRadius * 2f, HoopRadius * 2f), floorY = StandTop };

        Transform board, lever, hoopT;
        LineRenderer rod, ring;

        Vector2 LeverHandle => new Vector2(LeverX, LeverLowY + LeverTravel * lift);
        float BoardY => BoardRestY + BoardRise * lift;
        Vector2 BoardCenter => new Vector2(TableX, BoardY + 0.1f);
        Vector2 HoopCenter => hoop.position + new Vector2(0f, HoopRadius);
        bool Floating => lift >= FloatAt;

        protected override void OnReset()
        {
            lift = liftVel = floatTime = 0f;
            hoopFrom = 0;
            hoop.position = new Vector2(StandX, StandTop);
            hoop.floorY = StandTop;     // 挂在架子上
            hoop.velocity = Vector2.zero;
            hoop.heldBy = -1;
            hoop.back = false;
        }

        // ------------------------------------------------------------ 手

        public override bool TryGrab(TrickHand hand, int index)
        {
            if (hoop.heldBy == 1 - index && RingDistance(hand.position) <= 0.2f)   // 交接：空手碰到另一只手拿着的铁环
            {
                game.Hands.Hands[1 - index].holding = HandItem.None;
                hoop.Grab(index, hand.position);
                hand.holding = HandItem.Custom;
                hand.custom = HoopId;
                return true;
            }
            float leverDist = Vector2.Distance(hand.position, LeverHandle);
            bool leverOk = !InBack && leverDist <= 0.34f && Vector2.Distance(game.Hands.Shoulder(index), LeverHandle) <= game.Hands.Reach;
            float hoopDist = hoop.Held || hoop.back != InBack ? float.MaxValue : RingDistance(hand.position);
            bool hoopOk = hoopDist <= 0.2f;
            if (!leverOk && !hoopOk) return false;

            hand.holding = HandItem.Custom;
            if (leverOk && (!hoopOk || leverDist <= hoopDist))
            {
                hand.custom = LeverId;
                hand.position = LeverHandle;
                liftVel = 0f;
            }
            else
            {
                hand.custom = HoopId;
                hoop.Grab(index, hand.position);
                hoop.floorY = Floor;   // 拿起来以后再掉就掉到地上
            }
            return true;
        }

        // 铁环是一圈：手要碰到环本身，而不是环中间的空当
        float RingDistance(Vector2 p) => Mathf.Abs(Vector2.Distance(p, HoopCenter) - HoopRadius);

        public override void MoveHeld(TrickHand hand, int index, Vector2 delta)
        {
            if (hand.custom == LeverId)
            {
                float next = Mathf.Clamp01(lift + delta.y / LeverTravel);
                var handle = new Vector2(LeverX, LeverLowY + LeverTravel * next);
                if (Vector2.Distance(game.Hands.Shoulder(index), handle) <= game.Hands.Reach) lift = next;
                hand.position = LeverHandle;
            }
            else
            {
                hand.position = game.Hands.ClampHand(index, hand.position + delta);
                hoop.Follow(hand.position);
            }
        }

        public override void Release(TrickHand hand, int index)
        {
            if (hand.custom == HoopId) { hoop.Drop(); return; }
            // 松开拉杆：人还浮着、又在灯下 → 观众看着她掉下来
            if (lift > 0.2f && Lit(BoardCenter, 0.2f)) Expose("You let go of the hidden lever. She dropped in front of everyone!");
        }

        public override bool Anchor(TrickHand hand, out Vector2 position)
        {
            position = LeverHandle;
            return hand.custom == LeverId;
        }

        // ------------------------------------------------------------ 规则

        public override void Tick(float dt)
        {
            int leverHand = HeldBy(LeverId);
            if (leverHand < 0 && lift > 0f)
            {
                liftVel += 6f * dt;
                lift = Mathf.Max(0f, lift - liftVel * dt);
            }
            if (hoop.Held || hoop.back) FollowLane(hoop, Floor);
            if (hoop.Held) hoop.Follow(game.Hands.Hands[hoop.heldBy].position);
            else hoop.Tick(dt);

            // 撑拉杆的手藏在桌布下面，观众看不见；伸出桌布还在灯下就露馅
            for (int i = 0; i < 2; i++)
                view.GloveAlpha[i] = i == leverHand ? 0.35f : 1f;

            bool boardLit = Lit(BoardCenter, 0.2f);
            if (Step == 0)
            {
                floatTime = Floating && boardLit ? floatTime + dt : 0f;
                StepProgress = Mathf.Clamp01(floatTime / 1f);
                if (floatTime >= 1f)
                {
                    Step = 1;
                    StepProgress = 0f;
                    game.Notify("She floats! Now pass the hoop over her from one end to the other, in the light.");
                }
                return;
            }

            if (Step == 1)
            {
                float left = TableX - BoardHalf - 0.05f, right = TableX + BoardHalf + 0.05f;
                var c = HoopCenter;
                bool inBand = hoop.Held && !hoop.back && Mathf.Abs(c.y - BoardCenter.y) <= 0.4f;   // 要在前侧、观众看得见的地方套
                if (!inBand || !Floating || !boardLit) { hoopFrom = 0; StepProgress = 0f; return; }
                if (hoopFrom == 0)
                {
                    if (c.x < left) hoopFrom = -1;
                    else if (c.x > right) hoopFrom = 1;
                    return;
                }
                float t = hoopFrom < 0 ? Mathf.InverseLerp(left, right, c.x) : Mathf.InverseLerp(right, left, c.x);
                StepProgress = Mathf.Max(StepProgress, t);
                if (t >= 1f)
                {
                    Step = 2;
                    StepProgress = 0f;
                    game.Notify("Not a string in sight! Lower her gently back onto the table.");
                }
                return;
            }

            if (Step == 2 && leverHand >= 0 && lift <= 0.02f) Step = 3;
        }

        int HeldBy(int id)
        {
            for (int i = 0; i < 2; i++)
                if (game.Hands.Hands[i].holding == HandItem.Custom && game.Hands.Hands[i].custom == id) return i;
            return -1;
        }

        public override void AddDemo(TrickDemo d)
        {
            d.Say("Take the hoop from its stand first.")
             .Walk(2.1f).Hand(0).HandTo(() => HoopCenter + Vector2.right * HoopRadius).Grab()
             .Say("1  Reach under the tablecloth, hold the hidden lever and push UP.")
             .Walk(5.45f).Hand(1).HandTo(() => LeverHandle).Grab().HandBy(Vector2.up * 0.4f)
             .Until(() => Step >= 1)
             .Say("2  Keep the lever up, switch hands and pass the hoop along her body in the light.")
             .Walk(5.6f).Hand(0).HandTo(() => new Vector2(4.85f, BoardCenter.y))
             .HandTo(() => new Vector2(5.6f, BoardCenter.y)).Walk(6.0f)
             .HandTo(() => new Vector2(6.4f, BoardCenter.y))
             .Until(() => Step >= 2)
             .Say("3  Step back toward the lever and lower her gently onto the table.")
             .Walk(5.45f).Hand(1).HandBy(Vector2.down * 0.4f)
             .Wait(0.5f);
        }

        public override string Hint()
        {
            var sel = game.Hands.Selected;
            if (sel.holding == HandItem.Custom && sel.custom == LeverId)
                return Step == 2 ? "Push DOWN slowly. Do not let go while she is in the air." : "UP raises her. Keep holding E, then press F to use your other hand.";
            if (sel.holding == HandItem.Custom && sel.custom == HoopId)
                return Step == 1 ? "Start beside her head or feet, then sweep the hoop along her body while she floats in the light." : "The hoop proves there are no strings.";
            if (Step == 0) return "The hidden lever is under the RIGHT side of the tablecloth. Hold E on it and push UP.";
            if (Step == 1) return HeldBy(LeverId) >= 0 ? "Switch hands (F) and fetch the hoop from the stand on the left." : "Keep her up with the lever while you use the hoop.";
            return "Lower her with the lever until she rests on the table.";
        }

        // ------------------------------------------------------------ 画面

        protected override void Build()
        {
            var gold = Hex(0xC8962E);
            var cream = Hex(0xF2DEBC);

            // 撑杆和拉杆画在桌布后面，看不见
            rod = view.Line("HiddenRod", root, 0.05f, Hex(0x241C2C), 19, 2);
            lever = view.Disc("HiddenLever", root, Vector2.zero, new Vector2(0.14f, 0.14f), new Color(0.95f, 0.85f, 0.6f, 0.35f), 21).transform;

            var table = view.Group("FloatTable", root, new Vector2(TableX, Floor));
            view.Rect("TableCloth", table, new Vector2(0, (ClothTop - Floor) / 2f), new Vector2(TableHalf * 2f, ClothTop - Floor), Hex(0x5A3A66), 20);
            for (int i = 0; i < 5; i++)
                view.Rect("ClothFold", table, new Vector2(-0.8f + i * 0.4f, (ClothTop - Floor) / 2f), new Vector2(0.05f, ClothTop - Floor), Hex(0x47304F), 20);
            view.Rect("TableTop", table, new Vector2(0, ClothTop - Floor), new Vector2(TableHalf * 2f + 0.06f, 0.07f), gold, 21);
            for (int i = 0; i < 16; i++)
                view.Rect("ClothFringe", table, new Vector2(-0.94f + i * 0.125f, 0.04f), new Vector2(0.05f, 0.08f), gold, 21);

            board = view.Group("FloatingLady", root, new Vector2(TableX, BoardRestY));
            view.Rect("Board", board, Vector2.zero, new Vector2(BoardHalf * 2f, 0.07f), gold, 22);
            view.Rect("LadyDress", board, new Vector2(0.08f, 0.12f), new Vector2(0.78f, 0.17f), Hex(0xB55061), 23);
            view.Rect("LadySash", board, new Vector2(-0.12f, 0.12f), new Vector2(0.05f, 0.17f), gold, 24);
            view.Disc("LadyHead", board, new Vector2(-0.44f, 0.15f), new Vector2(0.22f, 0.22f), cream, 24);
            view.Disc("LadyHair", board, new Vector2(-0.52f, 0.13f), new Vector2(0.14f, 0.2f), Hex(0x3A2430), 24);
            view.Rect("LadyLegs", board, new Vector2(0.52f, 0.09f), new Vector2(0.2f, 0.06f), cream, 23);
            view.Rect("LadyShoes", board, new Vector2(0.64f, 0.11f), new Vector2(0.06f, 0.08f), Hex(0x6F345C), 24);
            view.Disc("LadyHands", board, new Vector2(-0.12f, 0.24f), new Vector2(0.1f, 0.08f), cream, 25);

            var stand = view.Group("HoopStand", root, new Vector2(StandX, Floor));
            view.Rect("StandPole", stand, new Vector2(0, (StandTop - Floor) / 2f), new Vector2(0.05f, StandTop - Floor), Hex(0x5A3A28), 15);
            view.Rect("StandFoot", stand, new Vector2(0, 0.02f), new Vector2(0.4f, 0.05f), gold, 15);
            view.Rect("StandPeg", stand, new Vector2(0, StandTop - Floor), new Vector2(0.18f, 0.04f), gold, 15);

            hoopT = view.Group("Hoop", root, Vector2.zero);
            ring = view.Line("HoopRing", hoopT, 0.05f, gold, 26, 33);
            for (int i = 0; i <= 32; i++)
            {
                float a = i / 32f * Mathf.PI * 2f;
                ring.SetPosition(i, new Vector3(Mathf.Cos(a) * HoopRadius, HoopRadius + Mathf.Sin(a) * HoopRadius, 0));
            }
        }

        public override void Draw()
        {
            board.position = new Vector3(TableX, BoardY, -0.05f);
            board.localRotation = Quaternion.Euler(0, 0, Floating ? Mathf.Sin(Time.time * 1.6f) * 1.2f : 0f);   // 浮着时轻轻晃
            rod.SetPosition(0, new Vector3(TableX + 0.05f, ClothTop - 0.1f, 0));
            rod.SetPosition(1, new Vector3(TableX + 0.05f, BoardY, 0));
            lever.position = new Vector3(LeverHandle.x, LeverHandle.y, -0.05f);
            hoopT.position = new Vector3(hoop.position.x, hoop.position.y, -0.1f);
            ring.sortingOrder = hoop.back ? 12 : 26;
        }

        static Color Hex(uint rgb) => new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);
    }
}
