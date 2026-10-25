using UnityEngine;

namespace Sleight.RabbitTrick
{
    public sealed class VaultRainTrick : ApparatusTrick
    {
        const int Door = 0, Blower = 1, Wheel = 2, WeightId = 100, GoldId = 101;
        const float VaultX = 5.6f, BlowerX = 8.15f;
        static readonly Vector2 WeightSlot = new Vector2(4.1f, 1.95f), GoldSlot = new Vector2(VaultX, 1.95f);
        readonly LooseProp weight = new LooseProp { shape = new Rect(-0.18f, 0, 0.36f, 0.5f) };
        readonly LooseProp gold = new LooseProp { shape = new Rect(-0.32f, 0, 0.64f, 0.5f) };
        Transform vault, doorT, weightT, goldT, balanceLamp, turnMark;
        Transform[] bills;
        Transform guideArrow; SpriteRenderer guideGlow;
        float open, blower, turn, lastTurn, distraction, proof, reveal, clock;
        public bool Balanced => weight.inside && gold.inside;
        public bool Flipped => turn >= 0.98f;
        public bool ViewClear => game.Hands.Depth > (Flipped ? 0.41f : 0.21f) || Mathf.Abs(game.Hands.BodyPosition.x - VaultX) > 0.7f;
        public float Distraction => distraction;
        public LooseProp Counterweight => weight;
        public LooseProp GoldCase => gold;
        public override string Name => "THE VANISHING VAULT";
        public override string SuccessTitle => "THE VAULT IS EMPTY. THE SKY RAINS MONEY!";
        public override string[] StepNames { get; } = { "01  SHOW THE GOLD IN THE VAULT", "02  BALANCE / DISTRACT / TURN THE VAULT", "03  OPEN THE EMPTY VAULT IN THE LIGHT" };
        public override string Status => "COUNTERWEIGHT: " + (Balanced ? "BALANCED" : "UNBALANCED") + "   |   ROTATION: " + Mathf.RoundToInt(turn * 100) + "%   |   DISTRACTION: " + distraction.ToString("0.0") + "s";
        public override float StartLightX => VaultX;
        protected override void OnReset()
        {
            open = blower = turn = lastTurn = distraction = proof = reveal = clock = 0;
            Place(weight, new Vector2(1.4f, Floor)); Place(gold, GoldSlot - gold.shape.center);
            gold.inside = true;
        }
        public override void DirectAudience(float dt) => game.Audience.Follow(distraction > 0 ? BlowerX : VaultX, 8, distraction > 0 ? "FLYING BANKNOTES" : "BANK VAULT");
        protected override bool Accessible(LooseProp prop) => prop == weight || !prop.inside || open > 0.85f;
        protected override void Taken(LooseProp prop) { }
        protected override void Dropped(LooseProp prop)
        {
            Vector2 slot = prop == weight ? WeightSlot : GoldSlot;
            if (Vector2.Distance(prop.Center, slot) > 0.46f || prop.depth > 0.24f || prop == gold && open < 0.85f) return;
            prop.inside = true; prop.position = slot - prop.shape.center;
        }
        public override void Tick(float dt)
        {
            AdvanceProps(dt); clock += dt;
            if (HeldBy(Blower) < 0) blower = Mathf.MoveTowards(blower, 0, dt * 1.8f);
            distraction = Mathf.Max(0, distraction - dt);
            if (blower > 0.85f) distraction = 7f;
            if (weight.inside) { weight.position = WeightSlot - weight.shape.center; weight.depth = 0; }
            if (gold.inside) { gold.position = GoldSlot - gold.shape.center; gold.depth = Flipped ? 0.85f : 0.2f; }
            bool turning = Mathf.Abs(turn - lastTurn) > 0.0001f;
            lastTurn = turn;
            if (turning && !Balanced) { Expose("The unbalanced vault lurched and exposed its false back. Dock the BRASS counterweight first."); return; }
            if (turning && (open > 0.15f || Lit(GoldSlot, 0.1f)))
            { Expose("They saw the vault revolve. Close its door and draw the light to the banknotes before turning."); return; }
            bool hiddenGold = gold.depth > 0.35f && gold.Center.x > 4.7f && gold.Center.x < 6.55f && gold.Center.y > 1.3f && gold.Center.y < 3.4f;
            if (!gold.inside && !hiddenGold && !InWings(gold.Center.x) && Lit(gold.Center, 0.2f))
            { Expose("The real gold was seen outside its chamber. Keep it behind the vault or in the wings."); return; }
            if (Step == 0)
            {
                // 只要门帘拉开（金匣子在灯下露出来）进度就涨，魔术师站在前面挡着也算
                proof = !Flipped && gold.inside && open > 0.9f && Lit(GoldSlot) ? proof + dt : 0;
                StepProgress = Mathf.Clamp01(proof / 0.8f);
                if (proof >= 0.8f) { Step = 1; StepProgress = 0; }
            }
            else if (Step == 1 && Flipped && Balanced) { Step = 2; StepProgress = 0; }
            else if (Step == 2)
            {
                reveal = Flipped && open > 0.9f && ViewClear && distraction <= 0 && Lit(GoldSlot) ? reveal + dt : 0;
                StepProgress = Mathf.Clamp01(reveal / 0.8f);
                if (reveal >= 0.8f) Step = 3;
            }
        }
        public override string Hint()
        {
            if (Step == 0) return "Stand on the RIGHT side. Pull the vault door UP to show the gold clearly, then close it DOWN.";
            if (Step == 1) return !Balanced ? "Bring the BRASS weight from the LEFT floor to the balance tray beside the vault. Release it on the tray."
                : "Wind the RIGHT banknote blower UP. While the light watches the bills, drag the vault wheel RIGHT to 100%.";
            return "Stand to the RIGHT so your body does not block the reveal. Open UP and wait for the light to show the EMPTY vault.";
        }
        public override void AddDemo(TrickDemo d)
        {
            d.Say("1  The vault starts with real gold. Let everyone see it, then close the door.")
             .Walk(7f).Hand(0).HandTo(() => ControlPoint(Door)).Grab().HandBy(Vector2.up * 0.7f).Until(() => Step >= 1)
             .HandBy(Vector2.down * 0.7f).Release()
             .Say("2  Balance the hidden revolving stage BEFORE using the wheel.")
             .Walk(1.65f).Hand(0).HandTo(() => weight.Center).Grab().HandTo(() => game.Hands.BodyPosition + Vector2.up * 1.2f)
             .Walk(4.05f).Release().Until(() => Balanced)
             .Say("The banknotes pull the spotlight RIGHT for a short window. Turn the balanced vault in the DARK.")
             .Walk(7.7f).Hand(1).HandTo(() => ControlPoint(Blower)).Grab().HandBy(Vector2.up * 0.7f).Release()
             .Walk(6.4f).HandTo(() => ControlPoint(Wheel)).Grab().HandBy(Vector2.right * 1f).Release().Until(() => Step >= 2)
             .Say("3  The other chamber is empty. Reveal it when the light comes back; the finale becomes a rain of banknotes.")
             .Walk(7f).Hand(0).HandTo(() => ControlPoint(Door)).Grab().HandBy(Vector2.up * 0.7f).Until(() => Finished);
        }
        protected override void Build()
        {
            IllusionScenery.Build(view, root, 4);
            vault = Frame("RevolvingVault", new Vector2(VaultX, 2.35f), new Vector2(1.9f, 2.3f), Gold);
            view.Disc("VaultArch", root, new Vector2(VaultX, 3.45f), new Vector2(2.1f, 0.55f), Gold, 18);
            doorT = view.Rect("VaultDoor", root, new Vector2(VaultX, 2.35f), new Vector2(1.82f, 2.22f), new Color32(82, 106, 125, 255), 22).transform;
            turnMark = view.Rect("RotationIndicator", root, new Vector2(VaultX, 3.45f), new Vector2(0.08f, 0.3f), Ink, 24).transform;
            view.Rect("BalanceTray", root, new Vector2(WeightSlot.x, 1.68f), new Vector2(0.85f, 0.1f), Gold, 20);
            view.Rect("BalanceStand", root, new Vector2(WeightSlot.x, 1.4f), new Vector2(0.1f, 0.55f), Gold, 18);
            balanceLamp = view.Disc("BalanceLamp", root, new Vector2(WeightSlot.x, 2.8f), new Vector2(0.17f, 0.17f), Wine, 23).transform;
            weightT = view.Group("BrassCounterweight", root, Vector2.zero);
            view.Rect("Weight", weightT, new Vector2(0, 0.25f), new Vector2(0.36f, 0.5f), Gold, 22);
            view.Rect("WeightBand", weightT, new Vector2(0, 0.26f), new Vector2(0.38f, 0.07f), Ink, 23);
            goldT = view.Group("RealGoldCase", root, Vector2.zero);
            view.Rect("GoldCase", goldT, new Vector2(0, 0.2f), new Vector2(0.64f, 0.4f), new Color32(237, 183, 53, 255), 22);
            for (int i = 0; i < 3; i++) view.Rect("Bullion", goldT, new Vector2((i - 1) * 0.2f, 0.43f), new Vector2(0.17f, 0.13f), Cream, 23);
            view.Rect("MoneyCannon", root, new Vector2(BlowerX, 1.65f), new Vector2(0.65f, 0.8f), Wine, 20);
            view.Disc("MoneyCannonMouth", root, new Vector2(BlowerX, 2.08f), new Vector2(0.65f, 0.2f), Gold, 21);
            bills = new Transform[42];
            for (int i = 0; i < bills.Length; i++) bills[i] = view.Rect("Banknote", root, Vector2.zero, new Vector2(0.19f, 0.09f), i % 2 == 0 ? Cream : Teal, 30).transform;
            Lever(Door, new Vector2(6.2f, 2.25f), Vector2.up * 0.35f, Vector2.up, () => open, v => open = v);
            Lever(Blower, new Vector2(8f, 2f), Vector2.up * 0.35f, Vector2.up, () => blower, v => blower = v);
            Lever(Wheel, new Vector2(6.6f, 2.1f), Vector2.right * 0.3f, Vector2.right, () => turn, v => turn = v, 1f);
            Prop(WeightId, weight); Prop(GoldId, gold);
            BuildGuideArrow();
        }

        // 第一步完成后的指引：先在最左边的黄铜配重上方冒白光的箭头上下浮动；
        // 拿起配重后，箭头换到它该放的天平托盘上方。两处不会同时出现，配重放好就消失。
        void BuildGuideArrow()
        {
            guideArrow = view.Group("GuideArrow", root, Vector2.zero);
            var white = new Color(1f, 1f, 1f, 0.95f);
            guideGlow = view.GlowAt("GuideGlow", guideArrow, new Vector2(0, 0.1f), 0.9f, new Color(1f, 1f, 1f, 0.35f), 0);
            view.Rect("GuideShaft", guideArrow, new Vector2(0, 0.22f), new Vector2(0.09f, 0.42f), white, 1);
            var left = view.Rect("GuideHeadL", guideArrow, new Vector2(-0.075f, -0.02f), new Vector2(0.09f, 0.26f), white, 1);
            left.transform.localRotation = Quaternion.Euler(0, 0, 40);
            var right = view.Rect("GuideHeadR", guideArrow, new Vector2(0.075f, -0.02f), new Vector2(0.09f, 0.26f), white, 1);
            right.transform.localRotation = Quaternion.Euler(0, 0, -40);
            foreach (var sr in guideArrow.GetComponentsInChildren<SpriteRenderer>(true)) sr.sharedMaterial = view.overlayMaterial;
            guideArrow.gameObject.SetActive(false);
        }

        void DrawGuideArrow()
        {
            bool show = Step == 1 && !weight.inside && !Failed;
            guideArrow.gameObject.SetActive(show);
            if (!show) return;
            // 箭头尖端对准：没拿起时是配重顶上，拿起后是托盘上方
            Vector2 tip = weight.Held ? new Vector2(WeightSlot.x, 1.85f) : weight.position + new Vector2(0, weight.shape.yMax + 0.12f);
            float bob = Mathf.Sin((Application.isPlaying ? Time.time : clock) * 4f) * 0.1f;
            guideArrow.position = new Vector3(tip.x, tip.y + 0.18f + bob, -0.3f);
            if (guideGlow != null)
            {
                var c = guideGlow.color;
                c.a = 0.25f + 0.2f * (0.5f + 0.5f * Mathf.Sin((Application.isPlaying ? Time.time : clock) * 6f));
                guideGlow.color = c;
            }
            StageDepth.Foreground(guideArrow, 50);
        }
        public override void Draw()
        {
            float width = Mathf.Max(0.08f, Mathf.Abs(Mathf.Cos(turn * Mathf.PI)));
            vault.localScale = new Vector3(width, 1, 1);
            StageDepth.Set(vault, 0.4f);
            Cover(doorT, new Vector2(VaultX, 1.24f), new Vector2(1.82f, 2.22f), 1 - open); StageDepth.Set(doorT, 0.1f);
            var doorScale = doorT.localScale; doorScale.x = 1.82f * width / view.square.bounds.size.x; doorT.localScale = doorScale;
            turnMark.localRotation = Quaternion.Euler(0, 0, -180 * turn);
            balanceLamp.GetComponent<SpriteRenderer>().color = Balanced ? Teal : Wine;
            weightT.position = weight.position; SetDepth(weightT, weight.depth);
            goldT.position = gold.position; SetDepth(goldT, gold.inside && Flipped ? 0.85f : gold.depth);
            goldT.localScale = new Vector3(gold.inside ? width : 1, 1, 1);
            for (int i = 0; i < bills.Length; i++)
            {
                bool finale = Finished;
                bills[i].gameObject.SetActive(distraction > 0 || finale);
                float t = Mathf.Repeat((Application.isPlaying ? Time.time : clock) * 0.2f + i * 0.071f, 1);
                bills[i].position = new Vector2(finale ? 1.1f + (i % 13) * 0.65f + Mathf.Sin(t * 6 + i) * 0.2f : BlowerX + Mathf.Sin(i * 2.3f + t * 7) * 0.75f,
                    finale ? 5f - t * 4.1f : 2.2f + Mathf.Sin(t * Mathf.PI) * 2.6f);
                bills[i].localRotation = Quaternion.Euler(0, 0, t * 360 + i * 20); StageDepth.Set(bills[i], 0.05f, 10);
            }
            DrawControls();
            DrawGuideArrow();
        }
    }
}
