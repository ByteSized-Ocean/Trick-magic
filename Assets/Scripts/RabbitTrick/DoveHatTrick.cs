using UnityEngine;

namespace Sleight.RabbitTrick
{
    // Fourth trick: let the dove take the audience's gaze, secretly load flowers, reveal on its return.
    public sealed class DoveHatTrick : StageTrick
    {
        const float HatX = 5.6f, Floor = 1.18f;
        const int HatId = 0, DoveId = 1, FlowersId = 2;
        readonly LooseProp dove = new LooseProp { shape = new Rect(-0.24f, 0, 0.48f, 0.45f) };
        readonly LooseProp flowers = new LooseProp { shape = new Rect(-0.26f, 0, 0.52f, 0.7f) };
        Transform hatT, doveT, flowersT, wingL, wingR;
        float tilt, displayTime;
        bool released;
        readonly float[] brimGrip = { -0.45f, 0.45f };
        public bool Flying { get; private set; }
        public bool DoveGone { get; private set; }
        public LooseProp Dove => dove;
        public LooseProp Flowers => flowers;
        public float HatTilt => tilt;
        public Vector2 Opening => new Vector2(HatX, 2.4f + tilt * 0.35f);
        public Vector2 HatHandle => new Vector2(HatX + 0.45f, 2.38f + tilt * 0.35f);
        public Vector2 BrimPoint(int index) => new Vector2(HatX + brimGrip[index], 2.38f + tilt * 0.35f);
        public Vector2 ClosestBrimPoint(Vector2 point) => new Vector2(Mathf.Clamp(point.x, HatX - 0.6f, HatX + 0.6f), 2.38f + tilt * 0.35f);
        public override string Name => "THE DOVE AND THE FLOWERS";
        public override string SuccessTitle => "FLOWERS FROM AN EMPTY HAT!";
        public override string[] StepNames { get; } = { "01  SHOW THE EMPTY HAT IN LIGHT", "02  RELEASE DOVE / HIDE FLOWERS", "03  REVEAL WHEN THE LIGHT RETURNS" };
        public override float StartLightX => HatX;
        protected override LooseProp[] MovableProps => new[] { dove, flowers };

        protected override void OnReset()
        {
            Place(dove, new Vector2(7.35f, Floor));
            Place(flowers, new Vector2(9.4f, Floor));
            tilt = displayTime = 0;
            Flying = DoveGone = released = false;
        }
        static void Place(LooseProp p, Vector2 point)
        {
            p.position = point; p.velocity = Vector2.zero; p.heldBy = -1; p.inside = false;
            p.depth = 0; p.floorY = p.frontFloor = Floor;
        }
        public override void DirectAudience(float dt)
        {
            game.Audience.Follow(Flying ? dove.Center.x : game.Hands.BodyPosition.x,
                DoveGone ? 22f : 8f, Flying ? "FLYING DOVE" : "MAGICIAN");
        }
        public override bool TryGrab(TrickHand hand, int index)
        {
            Vector2 brim = ClosestBrimPoint(hand.position);
            if (game.Hands.AtDepth(0) && Vector2.Distance(hand.position, brim) <= 0.18f &&
                Vector2.Distance(game.Hands.Shoulder(index), brim) <= game.Hands.Reach)
            {
                brimGrip[index] = brim.x - HatX;
                hand.holding = HandItem.Custom; hand.custom = HatId; hand.position = brim;
                return true;
            }
            foreach (var p in MovableProps)
            {
                if (p.inside)
                {
                    if (tilt < 0.55f) continue;
                    p.position = Opening; p.depth = 0;
                }
                if (p == dove && DoveGone || !game.Hands.AtDepth(p.depth)) continue;
                if (p.Held && p.heldBy != 1 - index) continue;
                if (p.DistanceTo(hand.position) > 0.25f) continue;
                if (p.Held) game.Hands.Hands[p.heldBy].holding = HandItem.None;
                p.Grab(index, hand.position);
                if (p == flowers && Step == 2) Step = 1;
                if (p == dove) Flying = false;
                hand.holding = HandItem.Custom; hand.custom = p == dove ? DoveId : FlowersId;
                return true;
            }
            return false;
        }
        public override void MoveHeld(TrickHand hand, int index, Vector2 delta)
        {
            if (hand.custom == HatId)
            {
                float next = Mathf.Clamp01(tilt + delta.y / 0.5f);
                var handle = new Vector2(HatX + brimGrip[index], 2.38f + next * 0.35f);
                if (Vector2.Distance(game.Hands.Shoulder(index), handle) <= game.Hands.Reach) tilt = next;
                hand.position = BrimPoint(index);
                for (int other = 0; other < 2; other++)
                    if (game.Hands.Hands[other].holding == HandItem.Custom && game.Hands.Hands[other].custom == HatId)
                        game.Hands.Hands[other].position = BrimPoint(other);
            }
            else
            {
                hand.position = game.Hands.ClampHand(index, hand.position + delta);
                (hand.custom == DoveId ? dove : flowers).Follow(hand.position);
            }
        }
        public override void Release(TrickHand hand, int index)
        {
            if (hand.custom == DoveId)
            {
                dove.Drop(); Flying = released = true;
                game.Notify("The dove flies LEFT. The spotlight follows it. Hide the flowers before it leaves the stage!");
            }
            else if (hand.custom == FlowersId)
            {
                flowers.Drop();
                if (Mathf.Abs(flowers.Center.x - HatX) < 0.48f &&
                    Mathf.Abs(flowers.Center.y - Opening.y) < 0.55f && flowers.depth < 0.25f)
                {
                    if (Lit(Opening) || Lit(flowers.Center, 0.2f)) { Expose("They saw you loading the flowers into the hat!"); return; }
                    flowers.inside = true;
                    if (Step >= 1 && released) Step = 2;
                    tilt = 0;
                    game.Notify("Flowers hidden. The light snaps back when the dove leaves. Lift the hat IN THE LIGHT.");
                }
            }
        }
        public override bool Anchor(TrickHand hand, out Vector2 position)
        { position = BrimPoint(hand == game.Hands.Hands[0] ? 0 : 1); return hand.custom == HatId; }
        bool HatHeld => game.Hands.Hands[0].holding == HandItem.Custom && game.Hands.Hands[0].custom == HatId ||
            game.Hands.Hands[1].holding == HandItem.Custom && game.Hands.Hands[1].custom == HatId;
        public override void Tick(float dt)
        {
            SyncHeldProps();
            foreach (var p in MovableProps) FollowLane(p, Floor);
            if (Flying)
            {
                dove.position += Vector2.left * 0.52f * dt;
                dove.position = new Vector2(dove.position.x, Mathf.MoveTowards(dove.position.y, 2.85f, dt * 1.3f));
                if (dove.position.x < -0.6f)
                {
                    Flying = false; DoveGone = true;
                    game.Audience.Follow(game.Hands.BodyPosition.x, 22f, "MAGICIAN");
                    game.Notify("The dove is gone! The audience is looking back at YOU.");
                }
            }
            else if (!dove.Held && !DoveGone) dove.Tick(dt);
            if (!flowers.Held && !flowers.inside) flowers.Tick(dt);
            if (!HatHeld) tilt = Mathf.MoveTowards(tilt, 0, dt * 1.4f);
            if (!flowers.inside && !InWings(flowers.Center.x) && Lit(flowers.Center, 0.2f))
            { Expose("The audience spotted the secret bouquet before the reveal."); return; }
            bool watched = Lit(Opening) && !Flying && game.Hands.Depth < 0.25f;
            if (Step == 0)
            {
                displayTime = watched && !flowers.inside && tilt >= 0.85f ? displayTime + dt : 0;
                StepProgress = Mathf.Clamp01(displayTime / 0.8f);
                if (displayTime >= 0.8f) { Step = 1; displayTime = StepProgress = 0; game.Notify("Empty hat shown. Release the dove on the right to draw the spotlight away."); }
            }
            else if (Step == 1 && flowers.inside && released) { Step = 2; displayTime = 0; }
            else if (Step == 2)
            {
                displayTime = watched && DoveGone && tilt >= 0.85f ? displayTime + dt : 0;
                StepProgress = Mathf.Clamp01(displayTime / 0.8f);
                if (displayTime >= 0.8f) Step = 3;
            }
        }
        public override string Hint()
        {
            if (Step == 0) return "Grab ANY part of the hat's brim with E and pull UP. Show the EMPTY hat IN THE LIGHT.";
            if (Step == 1) return !released ? "Pick up the dove on the floor to the RIGHT, then RELEASE E: it takes the spotlight LEFT."
                : (flowers.Held && Flying && dove.position.x > 3.7f ? "Wait in the RIGHT wing until the dove passes LEFT of the hat, then carry the bouquet through the DARK."
                    : "Fetch the bouquet from the far RIGHT wing. Carry it to the hat and RELEASE it into the opening while DARK.");
            return Flying ? "The dove is still being watched. Get ready at the hat: the light will snap back when it flies offscreen."
                : "The spotlight is back on YOU. Pull the hat UP and display the flowers in the LIGHT.";
        }
        public override void AddDemo(TrickDemo d)
        {
            d.Say("1  Lift the empty hat where the audience can see it.")
             .Walk(5.35f).Hand(1).HandTo(() => HatHandle).Grab().HandBy(Vector2.up * 0.55f)
             .Until(() => Step >= 1).HandBy(Vector2.down * 0.55f).Release()
             .Say("2  Pick up the dove, then release it. The spotlight follows its slow flight LEFT.")
             .Walk(7.05f).HandTo(() => dove.position + Vector2.up * 0.4f).Grab().Release()
             .Walk(9.1f).HandTo(() => flowers.position + Vector2.up * 0.4f).Grab()
             .Until(() => dove.position.x < 3.7f)
             .Walk(5.3f).HandTo(() => Opening + (game.Hands.Selected.position - flowers.Center)).Release()
             .Until(() => Step >= 2).HandTo(() => HatHandle).Grab()
             .Say("3  Wait: when the dove leaves the screen, the spotlight quickly returns to the magician.")
             .Until(() => DoveGone).HandBy(Vector2.up * 0.55f).Until(() => Finished);
        }
        protected override void Build()
        {
            var gold = new Color32(200, 150, 46, 255); var cream = new Color32(242, 222, 188, 255);
            view.Rect("HatTable", root, new Vector2(HatX, 1.78f), new Vector2(1.4f, 0.12f), gold, 18);
            view.Rect("HatTableLeg", root, new Vector2(HatX, 1.48f), new Vector2(0.12f, 0.6f), new Color32(90, 58, 40, 255), 17);
            hatT = view.Group("EmptyTopHat", root, Vector2.zero);
            view.Rect("HatCup", hatT, new Vector2(0, 0.2f), new Vector2(0.86f, 0.5f), new Color32(46, 31, 65, 255), 22);
            view.Rect("HatBand", hatT, new Vector2(0, 0.12f), new Vector2(0.87f, 0.1f), new Color32(181, 80, 97, 255), 23);
            view.Rect("HatBrim", hatT, new Vector2(0, 0.48f), new Vector2(1.2f, 0.07f), gold, 24);
            view.Disc("HatOpening", hatT, new Vector2(0, 0.5f), new Vector2(0.78f, 0.12f), new Color32(20, 16, 32, 255), 25);
            doveT = view.Group("Dove", root, Vector2.zero);
            view.Disc("WhiteDoveBody", doveT, new Vector2(0, 0.2f), new Vector2(0.48f, 0.32f), cream, 26);
            view.Disc("DoveHead", doveT, new Vector2(-0.18f, 0.36f), new Vector2(0.2f, 0.2f), Color.white, 27);
            view.Rect("DoveBeak", doveT, new Vector2(-0.3f, 0.35f), new Vector2(0.13f, 0.05f), gold, 28);
            view.Disc("DoveEye", doveT, new Vector2(-0.21f, 0.39f), new Vector2(0.035f, 0.035f), Color.black, 28);
            wingL = view.Disc("WingL", doveT, new Vector2(0.05f, 0.27f), new Vector2(0.45f, 0.15f), Color.white, 27).transform;
            wingR = view.Disc("WingR", doveT, new Vector2(0.1f, 0.3f), new Vector2(0.3f, 0.12f), cream, 27).transform;
            flowersT = view.Group("SecretBouquet", root, Vector2.zero);
            view.Rect("BouquetStems", flowersT, new Vector2(0, 0.23f), new Vector2(0.12f, 0.46f), new Color32(73, 126, 83, 255), 26);
            for (int i = 0; i < 5; i++) view.Disc("Flower", flowersT, new Vector2((i - 2) * 0.1f, 0.47f + Mathf.Sin(i) * 0.08f), new Vector2(0.23f, 0.23f), i % 2 == 0 ? new Color32(220, 111, 134, 255) : gold, 27);
        }
        public override void Draw()
        {
            hatT.position = new Vector3(HatX, 1.9f + tilt * 0.35f, 0);
            doveT.gameObject.SetActive(!DoveGone); doveT.position = dove.position;
            float flap = Flying ? Mathf.Sin(Time.time * 18f) * 60f : 0;
            wingL.localRotation = Quaternion.Euler(0, 0, flap); wingR.localRotation = Quaternion.Euler(0, 0, -flap);
            flowersT.gameObject.SetActive(!flowers.inside || tilt > 0.55f);
            flowersT.position = flowers.inside ? new Vector2(HatX, Opening.y) : flowers.position;
            SetDepth(doveT, dove.depth); SetDepth(flowersT, flowers.inside ? 0 : flowers.depth);
        }
    }
}
