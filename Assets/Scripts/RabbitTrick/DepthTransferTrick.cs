using UnityEngine;

namespace Sleight.RabbitTrick
{
    // Fifth trick: the secret is hidden by actual foreground panels, not by a global "back lane" flag.
    public sealed class DepthTransferTrick : StageTrick
    {
        const float Floor = 1.18f, SourceX = 3.15f, DestinationX = 6.8f;
        const float FirstDepth = 0.2f, SecondDepth = 0.58f;
        const int FirstHandleId = 0, SecondHandleId = 1, OrbId = 2;
        readonly LooseProp orb = new LooseProp { shape = new Rect(-0.23f, 0, 0.46f, 0.5f) };
        static readonly Rect FirstCurtainArea = new Rect(2.4f, 1.2f, 1.65f, 2.45f);
        static readonly Rect FirstFixedArea = new Rect(4.05f, 1.2f, 1.3f, 2.45f);
        static readonly Rect SecondCurtainArea = new Rect(4.8f, 1.35f, 2.6f, 2.45f);
        Transform orbT, curtainA, curtainB, handleA, handleB;
        float firstCover, secondCover, showTime, revealTime;
        public LooseProp Orb => orb;
        public bool EmptyPresentationReady => showTime >= 0.8f;
        public override string Name => "THE IMPOSSIBLE GOLDEN TRANSFER";
        public override string SuccessTitle => "THE GOLDEN ORB CROSSED THE STAGE!";
        public override string[] StepNames { get; } = { "01  SHOW THE ORB / CLOSE LEFT SCREEN", "02  CARRY BEHIND BOTH DEPTH SCREENS", "03  REVEAL IN THE RIGHT DISPLAY" };
        public override float StartLightX => SourceX;
        protected override LooseProp[] MovableProps => new[] { orb };
        public Vector2 FirstHandle => new Vector2(3.95f, 2.1f + firstCover * 0.65f);
        public Vector2 SecondHandle => new Vector2(7.28f, 2.1f + secondCover * 0.65f);
        protected override void OnReset()
        {
            firstCover = 0; secondCover = 1; showTime = revealTime = StepProgress = 0;
            orb.depth = 0.45f; orb.frontFloor = Floor; orb.floorY = Floor + orb.depth * DualHandController.LaneLift;
            orb.position = new Vector2(SourceX, orb.floorY); orb.velocity = Vector2.zero; orb.heldBy = -1; orb.inside = false;
        }
        static bool Contains(Rect screen, LooseProp prop) =>
            screen.Contains(prop.position + prop.shape.min) && screen.Contains(prop.position + prop.shape.max);
        public bool IsOccluded(LooseProp prop)
        {
            if (prop.depth > FirstDepth + 0.01f && Contains(FirstFixedArea, prop)) return true;
            if (firstCover > 0.95f && prop.depth > FirstDepth + 0.01f && Contains(new Rect(2.4f, 1.2f, 2.95f, 2.45f), prop)) return true;
            return secondCover > 0.95f && prop.depth > SecondDepth + 0.01f && Contains(SecondCurtainArea, prop);
        }
        public override bool TryGrab(TrickHand hand, int index)
        {
            if (orb.inside) { orb.position = new Vector2(DestinationX, 2f); orb.depth = 0.85f; }
            if ((!orb.inside || secondCover < 0.15f) && game.Hands.AtDepth(orb.depth, 0.22f) && orb.DistanceTo(hand.position) <= 0.25f &&
                (!orb.Held || orb.heldBy == 1 - index))
            {
                if (orb.Held) game.Hands.Hands[orb.heldBy].holding = HandItem.None;
                orb.Grab(index, hand.position); if (Step == 2) Step = 1;
                hand.holding = HandItem.Custom; hand.custom = OrbId; return true;
            }
            int id = -1;
            if (game.Hands.AtDepth(FirstDepth) && Vector2.Distance(hand.position, FirstHandle) <= 0.34f) id = FirstHandleId;
            if (game.Hands.AtDepth(SecondDepth) && Vector2.Distance(hand.position, SecondHandle) <= 0.34f) id = SecondHandleId;
            if (id < 0) return false;
            Vector2 handle = id == FirstHandleId ? FirstHandle : SecondHandle;
            if (Vector2.Distance(game.Hands.Shoulder(index), handle) > game.Hands.Reach) return false;
            hand.holding = HandItem.Custom; hand.custom = id; hand.position = handle; return true;
        }
        public override void MoveHeld(TrickHand hand, int index, Vector2 delta)
        {
            if (hand.custom == OrbId)
            { hand.position = game.Hands.ClampHand(index, hand.position + delta); orb.Follow(hand.position); return; }
            bool first = hand.custom == FirstHandleId;
            float amount = Mathf.Clamp01((first ? firstCover : secondCover) + delta.y / 0.65f);
            Vector2 handle = new Vector2(first ? 3.95f : 7.28f, 2.1f + amount * 0.65f);
            if (Vector2.Distance(game.Hands.Shoulder(index), handle) <= game.Hands.Reach)
            { if (first) firstCover = amount; else secondCover = amount; }
            hand.position = first ? FirstHandle : SecondHandle;
        }
        public override void Release(TrickHand hand, int index)
        {
            if (hand.custom != OrbId) return;
            orb.Drop();
            if (Mathf.Abs(orb.Center.x - DestinationX) < 0.45f && orb.Center.y > 1.5f && orb.Center.y < 2.9f && orb.depth >= 0.75f)
            {
                if (!IsOccluded(orb) && Lit(orb.Center)) { Expose("They saw you loading the right display. Stay BEHIND the second screen."); return; }
                orb.inside = true; if (Step >= 1) Step = 2;
                game.Notify("Transfer hidden! Move FORWARD to the second screen's handle, then pull DOWN in the light.");
            }
        }
        public override bool Anchor(TrickHand hand, out Vector2 position)
        { position = hand.custom == FirstHandleId ? FirstHandle : SecondHandle; return hand.custom != OrbId; }
        public override float AnchorDepth(TrickHand hand) => hand.custom == FirstHandleId ? FirstDepth : SecondDepth;
        public override void Tick(float dt)
        {
            FollowLane(orb, Floor); SyncHeldProps();
            if (!orb.Held && !orb.inside) orb.Tick(dt);
            if (Step == 0)
            {
                if (firstCover < 0.15f && Lit(orb.Center)) showTime = Mathf.Min(0.8f, showTime + dt);
                else if (firstCover < 0.15f) showTime = 0;
                StepProgress = showTime / 0.8f;
                if (firstCover > 0.95f && EmptyPresentationReady)
                { Step = orb.inside ? 2 : 1; StepProgress = 0; game.Notify("The orb vanished. Go BACK behind screen 1, carry it right, then go DEEPER behind screen 2."); }
                return;
            }
            if (Step == 1 && !orb.inside && Lit(orb.Center, 0.2f) && !IsOccluded(orb))
            { Expose("The golden orb left the screens' actual cover. Being at the BACK alone does not hide it!"); return; }
            if (Step == 2)
            {
                bool visible = secondCover < 0.1f && Lit(new Vector2(DestinationX, 2.25f));
                revealTime = visible ? revealTime + dt : 0;
                StepProgress = Mathf.Clamp01(revealTime / 0.8f);
                if (revealTime >= 0.8f) Step = 3;
            }
        }
        public override string Hint()
        {
            if (Step == 0) return EmptyPresentationReady ? "The orb was seen. Grab the LEFT handle and pull UP to cover it."
                : "Show the golden orb IN THE LIGHT before closing its left screen. UP closes / DOWN opens.";
            if (Step == 1) return "Hold W to move smoothly BACK. Stay behind screen 1; move DEEPER in the overlap before crossing behind screen 2. Release the orb in the RIGHT case.";
            return "Use S to approach the second screen's depth. Pull its RIGHT handle DOWN to reveal the orb IN THE LIGHT.";
        }
        public override void AddDemo(TrickDemo d)
        {
            d.Say("1  Show the golden orb. Cover it with the left screen.")
             .Walk(3.8f).Until(() => EmptyPresentationReady).Depth(0.2f).Hand(1).HandTo(() => FirstHandle).Grab().HandBy(Vector2.up * 0.7f).Release()
             .Until(() => Step >= 1)
             .Say("2  Use W/S smoothly. Carry the orb behind the FIRST screen, then go DEEPER behind the SECOND.")
             .Depth(0.45f).Walk(3f).HandTo(() => orb.position + Vector2.up * 0.4f).Grab()
             .Walk(4.75f).Depth(0.9f).Walk(6.65f).Release().Until(() => Step >= 2)
             .Say("3  Step forward to the second screen's depth and reveal the right display IN THE LIGHT.")
             .Walk(6.9f).Depth(0.58f).HandTo(() => SecondHandle).Grab().HandBy(Vector2.down * 0.7f).Release().Until(() => Finished);
        }
        protected override void Build()
        {
            var gold = new Color32(200, 150, 46, 255); var wood = new Color32(90, 58, 40, 255);
            view.Rect("SourcePlinth", root, new Vector2(SourceX, 1.32f), new Vector2(0.9f, 0.18f), gold, 17);
            view.Rect("RightDisplayInterior", root, new Vector2(DestinationX, 2.3f), new Vector2(1.05f, 1.7f), new Color32(20, 16, 32, 255), 14);
            foreach (float x in new[] { DestinationX - 0.6f, DestinationX + 0.6f })
                view.Rect("DisplayPost", root, new Vector2(x, 2.3f), new Vector2(0.08f, 1.9f), gold, 23);
            view.Rect("DisplayTop", root, new Vector2(DestinationX, 3.28f), new Vector2(1.4f, 0.12f), wood, 23);
            view.Rect("DisplayBase", root, new Vector2(DestinationX, 1.5f), new Vector2(1.4f, 0.1f), gold, 23);
            var fixedPanel = view.Rect("FirstScreenFixed", root, FirstFixedArea.center, FirstFixedArea.size, new Color32(89, 54, 103, 255), 30);
            fixedPanel.transform.position = new Vector3(FirstFixedArea.center.x, FirstFixedArea.center.y, FirstDepth * 1.5f);
            curtainA = view.Rect("FirstDepthCurtain", root, FirstCurtainArea.center, FirstCurtainArea.size, new Color32(111, 52, 92, 255), 30).transform;
            curtainB = view.Rect("SecondDepthCurtain", root, SecondCurtainArea.center, SecondCurtainArea.size, new Color32(51, 83, 111, 255), 24).transform;
            for (int i = 0; i < 6; i++)
            {
                view.Rect("FirstScreenTop", root, new Vector2(2.45f + i * 0.55f, 3.68f), new Vector2(0.5f, 0.045f), gold, 31);
                view.Rect("SecondScreenTop", root, new Vector2(4.85f + i * 0.5f, 3.82f), new Vector2(0.46f, 0.045f), gold, 25);
            }
            handleA = view.Disc("LeftScreenHandle", root, FirstHandle, new Vector2(0.15f, 0.15f), gold, 32).transform;
            handleB = view.Disc("RightScreenHandle", root, SecondHandle, new Vector2(0.15f, 0.15f), gold, 26).transform;
            orbT = view.Group("GoldenOrb", root, Vector2.zero);
            view.Disc("Orb", orbT, new Vector2(0, 0.25f), new Vector2(0.46f, 0.5f), gold, 32);
            view.Disc("OrbHighlight", orbT, new Vector2(-0.09f, 0.35f), new Vector2(0.1f, 0.1f), new Color32(250, 230, 177, 255), 33);
        }
        void DrawCover(Transform cover, Rect area, float amount)
        {
            cover.gameObject.SetActive(amount > 0.01f);
            var scale = cover.localScale; scale.y = area.height * amount / view.square.bounds.size.y; cover.localScale = scale;
            cover.position = new Vector3(area.center.x, area.yMin + area.height * amount * 0.5f, (cover == curtainA ? FirstDepth : SecondDepth) * 1.5f);
        }
        public override void Draw()
        {
            DrawCover(curtainA, FirstCurtainArea, firstCover); DrawCover(curtainB, SecondCurtainArea, secondCover);
            handleA.position = new Vector3(FirstHandle.x, FirstHandle.y, FirstDepth * 1.5f);
            handleB.position = new Vector3(SecondHandle.x, SecondHandle.y, SecondDepth * 1.5f);
            orbT.position = orb.inside ? new Vector2(DestinationX, 2.0f) : orb.position;
            SetDepth(orbT, orb.inside ? 0.85f : orb.depth);
            StageDepth.Set(root.Find("FirstScreenFixed"), FirstDepth);
            StageDepth.Set(curtainA, FirstDepth); StageDepth.Set(curtainB, SecondDepth);
            StageDepth.Set(handleA, FirstDepth, 1); StageDepth.Set(handleB, SecondDepth, 1);
            foreach (Transform item in root)
                if (item.name.StartsWith("Display")) StageDepth.Set(item, 0.82f);
                else if (item.name == "RightDisplayInterior") StageDepth.Set(item, 0.96f);
                else if (item.name == "SecondScreenTop") StageDepth.Set(item, SecondDepth, 1);
        }
    }
}
