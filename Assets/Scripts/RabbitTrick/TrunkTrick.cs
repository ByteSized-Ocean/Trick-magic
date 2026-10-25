using UnityEngine;

namespace Sleight.RabbitTrick
{
    // 第 2 关：换人柜（THE SUBSTITUTION CABINET）。
    // 1. 当着观众（在灯下）把助手放进柜子，拉上前帘。
    // 2. 趁暗打开柜子侧面的暗门——暗门没人扶就会落下——一手扶门，一手把助手拖出来藏进侧幕，
    //    再把藏在右侧幕后的假人塞进去，关好暗门。
    // 3. 在灯下拉开前帘：助手变成了假人。
    public sealed class TrunkTrick : StageTrick
    {
        public override string Name => "THE SUBSTITUTION CABINET";
        public override string SuccessTitle => "THE ASSISTANT TURNED INTO A DUMMY!";
        public override string[] StepNames { get; } =
            { "01  PUT HER IN, IN THE LIGHT", "02  SWAP HER FOR THE DUMMY", "03  OPEN THE CABINET IN THE LIGHT" };
        public override float StartLightX => CabX;
        protected override LooseProp[] MovableProps => new[] { assistant, dummy };
        public override void DirectAudience(float dt) => game.Audience.Follow(BalletOnStage ? BalletX : CabX, 3f, BalletOnStage ? "BALLERINA" : "CABINET");

        const float CabX = 5.6f, Floor = 1.18f;
        const float CurtainLeft = 4.98f, CurtainClosedEdge = 6.2f, CurtainOpenEdge = 5.12f, CurtainHandleY = 2.35f;
        const float HatchX = 6.27f, HatchHingeY = 3.0f, HatchLength = 1.0f, HatchMaxAngle = 80f;
        const int CurtainId = 0, HatchId = 1, AssistantId = 2, DummyId = 3;
        const float HandleRadius = 0.34f, PropRadius = 0.25f;

        float open;          // 前帘：0 = 拉上，1 = 拉开
        float hatch, hatchVel;   // 暗门：0 = 关，1 = 全开
        float prevOpen;
        readonly LooseProp assistant = new LooseProp { shape = new Rect(-0.25f, 0f, 0.5f, 1.25f), floorY = Floor };
        readonly LooseProp dummy = new LooseProp { shape = new Rect(-0.25f, 0f, 0.5f, 1.15f), floorY = Floor };

        Transform cabinet, curtainPivot, curtainHandle, hatchPivot, assistantT, dummyT;

        // 芭蕾女郎：完成第一步后从舞台左侧慢慢踮脚走上来，追光跟着她跳 20 秒，然后她慢慢退场，灯光再照原样走
        // 跳舞的 20 秒里她带着追光在全台慢慢来回（一个来回 16 秒），玩家要趁她离开柜子的空当动手
        const float BalletEnter = 4f, BalletHold = 20f, BalletExit = 6f;
        const float BalletOffX = -0.8f, BalletStageX = 1.4f, BalletSweepCenter = 5f, BalletSweepHalf = 3.6f, BalletSweepPeriod = 16f;
        float balletTime = -1f;     // -1 = 还没出场
        Transform ballerina, ballerinaSpin;
        public bool BalletOnStage => balletTime >= 0f && balletTime < BalletEnter + BalletHold + BalletExit;

        Vector2 CurtainHandle => new Vector2(Mathf.Lerp(CurtainClosedEdge, CurtainOpenEdge, open), CurtainHandleY);
        Vector2 HatchHandleAt(float amount)
        {
            float a = amount * HatchMaxAngle * Mathf.Deg2Rad;
            return new Vector2(HatchX + Mathf.Sin(a) * HatchLength, HatchHingeY - Mathf.Cos(a) * HatchLength);
        }
        Vector2 HatchHandle => HatchHandleAt(hatch);
        Vector2 CabinetCenter => new Vector2(CabX, 2.3f);
        LooseProp Prop(int id) => id == AssistantId ? assistant : id == DummyId ? dummy : null;

        protected override void OnReset()
        {
            open = 1f;            // 开场时前帘是拉开的，让观众看清空柜子
            prevOpen = open;
            hatch = hatchVel = 0f;
            Place(assistant, new Vector2(2.3f, Floor));
            Place(dummy, new Vector2(9.4f, Floor));   // 藏在右侧幕后
            balletTime = -1f;
        }

        static void Place(LooseProp p, Vector2 at)
        {
            p.position = at;
            p.velocity = Vector2.zero;
            p.heldBy = -1;
            p.inside = false;
            p.back = false;
            p.floorY = Floor;
        }

        // ------------------------------------------------------------ 手

        public override bool TryGrab(TrickHand hand, int index)
        {
            // 交接：空手靠近另一只手拿着的道具
            foreach (var p in new[] { assistant, dummy })
                if (p.heldBy == 1 - index && p.DistanceTo(hand.position) <= 0.3f)
                {
                    game.Hands.Hands[1 - index].holding = HandItem.None;
                    p.Grab(index, hand.position);
                    hand.holding = HandItem.Custom;
                    hand.custom = p == assistant ? AssistantId : DummyId;
                    game.Notify("Passed to your " + (index == 0 ? "LEFT" : "RIGHT") + " hand. Keep holding E.");
                    return true;
                }

            int best = -1;
            float bestDist = float.MaxValue;
            if (!InBack)   // 柜子在前侧，站在后侧够不着
            {
                Consider(CurtainId, Vector2.Distance(hand.position, CurtainHandle), HandleRadius, CurtainHandle, index, ref best, ref bestDist);
                Consider(HatchId, Vector2.Distance(hand.position, HatchHandle), HandleRadius, HatchHandle, index, ref best, ref bestDist);
            }
            foreach (var id in new[] { AssistantId, DummyId })
            {
                var p = Prop(id);
                if (p.Held) continue;
                if (p.inside && !(open > 0.5f || hatch > 0.5f)) continue;   // 柜门都关着就拿不到里面的东西
                if (!game.Hands.AtDepth(p.inside ? 0 : p.depth)) continue;
                float d = p.DistanceTo(hand.position);
                if (d <= PropRadius && d < bestDist) { best = id; bestDist = d; }
            }
            if (best < 0) return false;

            hand.holding = HandItem.Custom;
            hand.custom = best;
            if (best == CurtainId) hand.position = CurtainHandle;
            else if (best == HatchId) hand.position = HatchHandle;
            else Prop(best).Grab(index, hand.position);
            return true;
        }

        void Consider(int id, float distance, float radius, Vector2 point, int index, ref int best, ref float bestDist)
        {
            if (distance > radius || distance >= bestDist) return;
            if (Vector2.Distance(game.Hands.Shoulder(index), point) > game.Hands.Reach) return;
            best = id;
            bestDist = distance;
        }

        public override void MoveHeld(TrickHand hand, int index, Vector2 delta)
        {
            if (hand.custom == CurtainId)
            {
                float next = Mathf.Clamp01(open - delta.x / (CurtainClosedEdge - CurtainOpenEdge));
                if (InReach(index, new Vector2(Mathf.Lerp(CurtainClosedEdge, CurtainOpenEdge, next), CurtainHandleY))) open = next;
                hand.position = CurtainHandle;
            }
            else if (hand.custom == HatchId)
            {
                // 沿暗门把手的圆弧方向推拉
                Vector2 tangent = (HatchHandleAt(Mathf.Min(1f, hatch + 0.01f)) - HatchHandleAt(Mathf.Max(0f, hatch - 0.01f))) / 0.02f;
                float next = Mathf.Clamp01(hatch + Vector2.Dot(delta, tangent) / Mathf.Max(0.001f, tangent.sqrMagnitude));
                if (InReach(index, HatchHandleAt(next))) { hatch = next; hatchVel = 0f; }
                hand.position = HatchHandle;
            }
            else
            {
                hand.position = game.Hands.ClampHand(index, hand.position + delta);
                Prop(hand.custom)?.Follow(hand.position);
            }
        }

        bool InReach(int index, Vector2 p) => Vector2.Distance(game.Hands.Shoulder(index), p) <= game.Hands.Reach;

        public override void Release(TrickHand hand, int index)
        {
            var p = Prop(hand.custom);
            if (p == null) return;
            p.Drop();
            if (TryPutInside(p)) game.Notify(p == assistant ? "She is inside the cabinet." : "The dummy is inside the cabinet.");
        }

        // 从前帘或拉开的暗门放进柜子
        bool TryPutInside(LooseProp p)
        {
            if (p.back) return false;   // 从后侧够不着柜子
            var c = p.Center;
            bool throughFront = open >= 0.7f && Mathf.Abs(c.x - CabX) <= 0.55f && c.y < 3.4f;
            bool throughHatch = hatch >= 0.6f && c.x > CabX - 0.2f && c.x < HatchX + 0.45f && c.y < 3.4f;
            if (!throughFront && !throughHatch) return false;
            p.inside = true;
            p.velocity = Vector2.zero;
            return true;
        }

        public override bool Anchor(TrickHand hand, out Vector2 position)
        {
            position = hand.custom == CurtainId ? CurtainHandle : HatchHandle;
            return hand.custom == CurtainId || hand.custom == HatchId;
        }

        // ------------------------------------------------------------ 规则

        public override void Tick(float dt)
        {
            // 第一步完成之前，追光一直定在柜子上不动；之后交给芭蕾女郎和原来的走法
            if (Step == 0) game.Audience.Follow(CabX, 3f, "CABINET");
            TickBallet(dt);
            bool hatchHeld = HeldBy(HatchId) >= 0;
            if (!hatchHeld && hatch > 0f)
            {
                // 暗门没人扶：绕铰链落下关上
                hatchVel += 7f * dt;
                hatch = Mathf.Max(0f, hatch - hatchVel * dt);
                if (hatch <= 0f) hatchVel = 0f;
            }
            foreach (var p in new[] { assistant, dummy })
            {
                FollowLane(p, Floor);
                if (p.Held) p.Follow(game.Hands.Hands[p.heldBy].position);
                else p.Tick(dt);
            }
            LayoutInside();

            bool cabLit = Lit(CabinetCenter, 0.35f);
            bool closedNow = open <= 0.05f, justClosed = closedNow && prevOpen > 0.05f;
            prevOpen = open;

            // 什么时候都不能让观众看到的东西
            if (OutInView(dummy)) { Expose("The audience spotted the dummy before the trick."); return; }
            if (hatch > 0.15f && cabLit) { Expose("They saw the secret door in the side of the cabinet."); return; }

            if (Step == 0)
            {
                if (assistant.inside && justClosed)
                {
                    if (cabLit)
                    {
                        Step = 1;
                        balletTime = 0f;   // 芭蕾女郎上场，把观众的目光引到左边
                        game.Notify("She is in! A ballerina takes the spotlight: swap her through the side door while it is dark.");
                    }
                    else game.Notify("Nobody saw her go in. Open the curtain and close it again IN THE LIGHT.");
                }
                return;
            }

            if (OutInView(assistant)) { Expose("They saw your assistant outside the cabinet!"); return; }
            if (Step == 1)
            {
                if (open > 0.3f && cabLit) { Expose("You opened the cabinet before the swap was done."); return; }
                if (dummy.inside && !assistant.inside && !assistant.Held && InWings(assistant.position.x) && hatch <= 0.05f && closedNow)
                {
                    Step = 2;
                    game.Notify("Swap done! Open the curtain while the light is on the cabinet.");
                }
                return;
            }

            if (Step == 2 && open >= 0.9f && cabLit && dummy.inside) Step = 3;
        }

        void TickBallet(float dt)
        {
            if (balletTime < 0f) return;
            float total = BalletEnter + BalletHold + BalletExit;
            if (balletTime >= total) return;
            balletTime += dt;
            if (balletTime >= total) { game.Audience.StopFollowing(); return; }
            game.Audience.Follow(BalletX, 3f, "BALLERINA");
        }

        float BalletX
        {
            get
            {
                if (balletTime < BalletEnter) return Mathf.Lerp(BalletOffX, BalletStageX, Mathf.SmoothStep(0, 1, balletTime / BalletEnter));
                float t = balletTime - BalletEnter;
                if (t < BalletHold) return SweepX(t);
                return Mathf.Lerp(SweepX(BalletHold), BalletOffX, Mathf.SmoothStep(0, 1, (t - BalletHold) / BalletExit));
            }
        }

        // 从左边 1.4 出发，慢慢扫到右边 8.6 再回来；最快约 1.4 单位/秒
        static float SweepX(float t) => BalletSweepCenter - BalletSweepHalf * Mathf.Cos(t / BalletSweepPeriod * Mathf.PI * 2f);

        // 在柜子外、不在侧幕后、又被光照到
        bool OutInView(LooseProp p) => !p.inside && !InWings(p.Center.x) && p.Visible(game.Audience);

        int HeldBy(int id)
        {
            for (int i = 0; i < 2; i++)
                if (game.Hands.Hands[i].holding == HandItem.Custom && game.Hands.Hands[i].custom == id) return i;
            return -1;
        }

        void LayoutInside()
        {
            bool both = assistant.inside && dummy.inside;
            if (assistant.inside) assistant.position = new Vector2(CabX + (both ? -0.22f : 0f), Floor + 0.06f);
            if (dummy.inside) dummy.position = new Vector2(CabX + (both ? 0.22f : 0f), Floor + 0.06f);
        }

        public override void AddDemo(TrickDemo d)
        {
            d.Say("1  Carry your assistant into the open cabinet...")
             .Walk(2.9f).Hand(0).HandTo(() => assistant.position + new Vector2(0.2f, 0.9f)).Grab()
             .Walk(5.3f).HandTo(() => new Vector2(CabX + 0.2f, 2.08f)).Release()
             .Say("...and close the curtain while the LIGHT is on the cabinet.")
             .Hand(1).HandTo(() => CurtainHandle).Grab().HandBy(Vector2.right * 1.15f).Release()
             .Until(() => Step >= 1)
             .Say("2  In the DARK, fetch the dummy from behind the right wings.")
             .Light(2.0f, 1.0f)
             .Walk(9.1f).Hand(1).HandTo(() => dummy.position + new Vector2(0f, 0.75f)).Grab()
             .Walk(7.45f).Release()
             .Say("Hold the self-closing side door open with one hand...")
             .Walk(6.45f).Hand(0).HandTo(() => HatchHandle).Grab().HandTo(() => HatchHandleAt(0.75f))
             .Say("...and swap them with the other hand.")
             .Hand(1).HandTo(() => new Vector2(CabX + 0.35f, 1.95f)).Grab().HandTo(() => new Vector2(7.4f, 1.95f)).Release()
             .HandTo(() => dummy.position + new Vector2(-0.23f, 0.82f)).Grab().HandTo(() => new Vector2(6.07f, 2.0f)).Release()
             .Hand(0).Release()
             .Say("Hide your assistant in the right wings.")
             .Hand(1).HandTo(() => assistant.position + new Vector2(-0.05f, 0.77f)).Grab()
             .Walk(9.15f).HandTo(() => game.Hands.Shoulder(1) + new Vector2(0.5f, -0.55f)).Release()
             .Until(() => Step >= 2)
             .Say("3  When the light returns, open the curtain: she has become a dummy!")
             .Walk(5.6f).Light(CabX, 1.2f)
             .Hand(1).HandTo(() => CurtainHandle).Grab().HandBy(Vector2.left * 1.15f)
             .Wait(0.5f);
        }

        public override string Hint()
        {
            var sel = game.Hands.Selected;
            if (sel.holding == HandItem.Custom)
            {
                if (sel.custom == CurtainId) return "HOLDING THE CURTAIN: LEFT opens / RIGHT closes. Let go of E to release.";
                if (sel.custom == HatchId) return "HOLDING THE SIDE DOOR: it drops shut if you let go. Use the OTHER hand (F) for the swap.";
                if (sel.custom == AssistantId) return Step == 0 ? "Put her in through the open curtain." : "Hide her in the WINGS (far left or right), out of the light.";
                return "Push the dummy in through the side door while it is dark.";
            }
            if (Step == 0) return assistant.inside ? "Close the curtain while the cabinet is IN THE LIGHT." : "Carry your assistant to the cabinet and put her in through the open curtain.";
            if (Step == 1)
            {
                if (!dummy.inside && !dummy.Held) return "The dummy waits behind the RIGHT wings. Open the side door in the DARK to swap.";
                return "Assistant out to the wings, dummy in, then close the side door and curtain.";
            }
            return "Wait for the light, then pull the curtain fully open.";
        }

        // ------------------------------------------------------------ 画面

        protected override void Build()
        {
            var wood = Hex(0x5A3A28);
            var gold = Hex(0xC8962E);
            cabinet = view.Group("Cabinet", root, new Vector2(CabX, Floor));
            view.Rect("CabinetInterior", cabinet, new Vector2(0, 1.07f), new Vector2(1.1f, 2.05f), Hex(0x141020), 14);
            view.Rect("CabinetPostL", cabinet, new Vector2(-0.62f, 1.1f), new Vector2(0.1f, 2.2f), wood, 18);
            view.Rect("CabinetPostR", cabinet, new Vector2(0.62f, 1.1f), new Vector2(0.1f, 2.2f), wood, 18);
            view.Rect("CabinetTop", cabinet, new Vector2(0, 2.2f), new Vector2(1.45f, 0.18f), wood, 18);
            view.Rect("CabinetTopTrim", cabinet, new Vector2(0, 2.12f), new Vector2(1.45f, 0.035f), gold, 19);
            view.Rect("CabinetBase", cabinet, new Vector2(0, 0.05f), new Vector2(1.45f, 0.1f), gold, 19);
            var star = view.Rect("CabinetCrest", cabinet, new Vector2(0, 2.42f), new Vector2(0.2f, 0.2f), gold, 19);
            star.transform.localRotation = Quaternion.Euler(0, 0, 45);
            view.Disc("CabinetCrestGem", cabinet, new Vector2(0, 2.42f), new Vector2(0.09f, 0.09f), Hex(0xB55061), 20);

            // 侧面暗门：挂在右侧立柱上，向外向上翻开
            hatchPivot = view.Group("SideDoor", cabinet, new Vector2(HatchX - CabX, HatchHingeY - Floor));
            view.Rect("SideDoorPanel", hatchPivot, new Vector2(0, -HatchLength / 2f), new Vector2(0.09f, HatchLength), Hex(0x4A3020), 19);
            view.Disc("SideDoorHandle", hatchPivot, new Vector2(0, -HatchLength + 0.03f), new Vector2(0.14f, 0.14f), Hex(0xF2DEBC), 20);

            // 前帘：挂点在左边，按拉开程度缩放宽度
            curtainPivot = view.Group("FrontCurtain", root, new Vector2(CurtainLeft, Floor));
            view.Rect("CurtainCloth", curtainPivot, new Vector2(0.5f, 1.07f), new Vector2(1f, 2.0f), Hex(0x8E2C46), 22);
            for (int i = 0; i < 4; i++)
                view.Rect("CurtainFold", curtainPivot, new Vector2(0.15f + i * 0.24f, 1.07f), new Vector2(0.05f, 2.0f), Hex(0x6E2238), 23);
            view.Rect("CurtainFringe", curtainPivot, new Vector2(0.5f, 0.1f), new Vector2(1f, 0.06f), gold, 23);
            curtainHandle = view.Disc("CurtainTassel", root, Vector2.zero, new Vector2(0.15f, 0.2f), gold, 24).transform;

            assistantT = BuildAssistant();
            dummyT = BuildDummy();
            BuildBallerina();
        }

        Transform BuildAssistant()
        {
            var t = view.Group("Assistant", root, Vector2.zero);
            var cream = Hex(0xF2DEBC);
            view.Rect("AssistantStand", t, new Vector2(0, 0.03f), new Vector2(0.42f, 0.06f), Hex(0xC8962E), 16);
            view.Rect("AssistantLegL", t, new Vector2(-0.07f, 0.2f), new Vector2(0.05f, 0.32f), cream, 16);
            view.Rect("AssistantLegR", t, new Vector2(0.07f, 0.2f), new Vector2(0.05f, 0.32f), cream, 16);
            view.Rect("AssistantDress", t, new Vector2(0, 0.66f), new Vector2(0.46f, 0.66f), Hex(0xB55061), 17);
            view.Rect("AssistantSash", t, new Vector2(0, 0.86f), new Vector2(0.46f, 0.05f), Hex(0xC8962E), 18);
            var armL = view.Rect("AssistantArmL", t, new Vector2(-0.3f, 0.84f), new Vector2(0.05f, 0.32f), cream, 16);
            armL.transform.localRotation = Quaternion.Euler(0, 0, -25);
            var armR = view.Rect("AssistantArmR", t, new Vector2(0.3f, 0.84f), new Vector2(0.05f, 0.32f), cream, 16);
            armR.transform.localRotation = Quaternion.Euler(0, 0, 25);
            view.Disc("AssistantHead", t, new Vector2(0, 1.1f), new Vector2(0.27f, 0.3f), cream, 18);
            view.Disc("AssistantHair", t, new Vector2(0, 1.2f), new Vector2(0.3f, 0.16f), Hex(0x3A2430), 19);
            view.Rect("AssistantFeather", t, new Vector2(0.1f, 1.33f), new Vector2(0.04f, 0.16f), Hex(0xE8A33C), 19);
            return t;
        }

        Transform BuildDummy()
        {
            var t = view.Group("Dummy", root, Vector2.zero);
            var burlap = Hex(0x9C7A52);
            view.Rect("DummyPost", t, new Vector2(0, 0.12f), new Vector2(0.06f, 0.24f), Hex(0x5A3A28), 16);
            view.Rect("DummyBody", t, new Vector2(0, 0.6f), new Vector2(0.48f, 0.72f), burlap, 17);
            for (int i = 0; i < 3; i++)
                view.Rect("DummyStitch", t, new Vector2(0, 0.38f + i * 0.2f), new Vector2(0.3f, 0.02f), Hex(0x5A4430), 18);
            view.Disc("DummyHead", t, new Vector2(0, 1.04f), new Vector2(0.3f, 0.3f), Hex(0xB08A5E), 18);
            for (int e = -1; e <= 1; e += 2)
                for (int k = -1; k <= 1; k += 2)
                {
                    var x = view.Rect("DummyEyeX", t, new Vector2(e * 0.06f, 1.07f), new Vector2(0.06f, 0.015f), Hex(0x2A2018), 19);
                    x.transform.localRotation = Quaternion.Euler(0, 0, 45 * k);
                }
            view.Rect("DummyMouth", t, new Vector2(0, 0.97f), new Vector2(0.1f, 0.015f), Hex(0x2A2018), 19);
            for (int i = 0; i < 5; i++)
            {
                var straw = view.Rect("DummyStraw", t, new Vector2(-0.1f + i * 0.05f, 1.2f), new Vector2(0.025f, 0.1f), Hex(0xD8B060), 19);
                straw.transform.localRotation = Quaternion.Euler(0, 0, (i - 2) * 14);
            }
            return t;
        }

        void BuildBallerina()
        {
            var skin = Hex(0xF2DEBC);
            var pink = Hex(0xF2A7C0);
            var pinkDark = Hex(0xD9809F);
            ballerina = view.Group("Ballerina", root, new Vector2(BalletOffX, Floor));
            ballerinaSpin = view.Group("Spin", ballerina, Vector2.zero);
            var s = ballerinaSpin;
            // 踮着脚尖的腿：一条直立、一条抬起成 passé
            view.Rect("BalletLegStand", s, new Vector2(0.02f, 0.38f), new Vector2(0.06f, 0.72f), skin, 27);
            view.Rect("BalletShoe", s, new Vector2(0.02f, 0.03f), new Vector2(0.07f, 0.08f), pinkDark, 28);
            var thigh = view.Rect("BalletLegLift", s, new Vector2(0.14f, 0.6f), new Vector2(0.06f, 0.32f), skin, 27);
            thigh.transform.localRotation = Quaternion.Euler(0, 0, 70);
            var shin = view.Rect("BalletShin", s, new Vector2(0.18f, 0.5f), new Vector2(0.06f, 0.3f), skin, 27);
            shin.transform.localRotation = Quaternion.Euler(0, 0, -35);
            view.Disc("BalletTutu", s, new Vector2(0, 0.82f), new Vector2(0.86f, 0.2f), pink, 29);
            view.Disc("BalletTutuFrill", s, new Vector2(0, 0.79f), new Vector2(0.7f, 0.12f), pinkDark, 29);
            view.Rect("BalletBodice", s, new Vector2(0, 1.0f), new Vector2(0.2f, 0.32f), pink, 30);
            for (int i = -1; i <= 1; i += 2)
            {
                // 双臂举过头顶成环（fifth position）
                var arm = view.Rect("BalletArm", s, new Vector2(i * 0.15f, 1.32f), new Vector2(0.05f, 0.4f), skin, 30);
                arm.transform.localRotation = Quaternion.Euler(0, 0, -i * 22);
            }
            view.Disc("BalletHead", s, new Vector2(0, 1.3f), new Vector2(0.2f, 0.22f), skin, 31);
            view.Disc("BalletBun", s, new Vector2(0, 1.44f), new Vector2(0.12f, 0.1f), Hex(0x3A2430), 31);
            view.Disc("BalletTiara", s, new Vector2(0, 1.4f), new Vector2(0.14f, 0.04f), Hex(0xC8962E), 32);
            ballerina.gameObject.SetActive(false);
        }

        public override void Draw()
        {
            bool on = BalletOnStage;
            ballerina.gameObject.SetActive(on);
            if (on)
            {
                float t = balletTime;
                bool dancing = t >= BalletEnter && t < BalletEnter + BalletHold;
                // 旋转：横向缩放在正反两面之间翻，像在原地转圈；走场时踮脚小碎步上下颠
                float spin = dancing ? Mathf.Cos(t * 7f) : (t < BalletEnter ? 1f : -1f);
                ballerinaSpin.localScale = new Vector3(Mathf.Sign(spin) * Mathf.Max(0.25f, Mathf.Abs(spin)), 1f, 1f);
                float hop = dancing ? Mathf.Abs(Mathf.Sin(t * 3.5f)) * 0.06f : Mathf.Abs(Mathf.Sin(t * 9f)) * 0.025f;
                ballerina.position = new Vector3(BalletX, Floor + hop, -0.2f);
                StageDepth.Set(ballerina, 0f, 60);   // 她在舞台最前沿跳，不会被柜子和帘子挡住
            }

            hatchPivot.localRotation = Quaternion.Euler(0, 0, hatch * HatchMaxAngle);
            float edge = Mathf.Lerp(CurtainClosedEdge, CurtainOpenEdge, open);
            curtainPivot.localScale = new Vector3(Mathf.Max(0.02f, edge - CurtainLeft), 1f, 1f);
            curtainHandle.position = new Vector3(CurtainHandle.x, CurtainHandle.y, -0.1f);
            assistantT.position = new Vector3(assistant.position.x, assistant.position.y, -0.05f);
            dummyT.position = new Vector3(dummy.position.x, dummy.position.y, -0.05f);
            StageDepth.Set(cabinet, 0.21f);
            StageDepth.Set(curtainPivot, 0.18f);
            StageDepth.Set(curtainHandle, 0.18f, 4);
            SetDepth(assistantT, assistant.inside ? 0.2f : assistant.depth);
            SetDepth(dummyT, dummy.inside ? 0.2f : dummy.depth);
        }

        static Color Hex(uint rgb) => new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);
    }
}
