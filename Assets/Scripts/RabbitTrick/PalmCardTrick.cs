using UnityEngine;

namespace Sleight.RabbitTrick
{
    public sealed class PalmCardTrick : ApparatusTrick
    {
        const int Shutter = 0, CardId = 100;
        static readonly Vector2 Slot = new Vector2(6.5f, 2.3f);
        readonly LooseProp card = new LooseProp { shape = new Rect(-0.11f, 0, 0.22f, 0.32f) };
        Transform cardT, cover, frame;
        float open, proof, reveal, exposure;
        public LooseProp Card => card;
        public bool Palmed => card.Held && Vector2.Distance(game.Hands.Hands[1 - card.heldBy].position, card.Center) <= 0.28f;
        public bool FrameVisible => game.Hands.Depth > (card.inside ? 0.22f : 0.31f) ||
            Mathf.Abs(game.Hands.BodyPosition.x - Slot.x) > 0.45f && Vector2.Distance(game.Hands.Hands[0].position, Slot) > 0.3f && Vector2.Distance(game.Hands.Hands[1].position, Slot) > 0.3f;
        public override string Name => "THE INVISIBLE CARD";
        public override string SuccessTitle => "AN IMPOSSIBLE CARD APPEARS!";
        public override string[] StepNames { get; } = { "01  SHOW THE EMPTY FRAME", "02  PALM THE CARD / LOAD THE FRAME", "03  OPEN THE FRAME IN THE LIGHT" };
        public override string Status => "PALM COVER: " + (Palmed ? "SAFE" : "OPEN") + "   |   CARD: " + (card.inside ? "IN FRAME" : card.Held ? "IN HAND" : "ON STAGE") + "   |   FRAME VIEW: " + (FrameVisible ? "CLEAR" : "BLOCKED");
        public override float StartLightX => Slot.x;
        protected override void OnReset()
        {
            open = proof = reveal = exposure = 0;
            Place(card, new Vector2(9.35f, Floor));
        }
        public override void DirectAudience(float dt) => game.Audience.Follow(Step == 1 ? game.Hands.BodyPosition.x : Slot.x, 8f, Step == 1 ? "MAGICIAN'S HANDS" : "DISPLAY FRAME");
        protected override bool Accessible(LooseProp prop) => !prop.inside || open > 0.85f;
        protected override void Taken(LooseProp prop) { if (Step == 2) Step = 1; }
        protected override void Dropped(LooseProp prop)
        {
            if (Vector2.Distance(card.Center, Slot) > 0.45f || card.depth > 0.24f || open < 0.85f) return;
            bool covered = Vector2.Distance(game.Hands.Other.position, card.Center) <= 0.32f;
            if (Lit(Slot, 0.12f) && !covered) { Expose("They saw the card enter the frame. Cover it with your OTHER palm."); return; }
            card.inside = true; card.position = Slot - card.shape.center; card.depth = 0.21f;
            if (Step >= 1) Step = 2;
            open = 0;
        }
        public override void Tick(float dt)
        {
            AdvanceProps(dt);
            if (card.inside) { card.position = Slot - card.shape.center; card.depth = 0.21f; }
            bool exposed = !card.inside && !InWings(card.Center.x) && Lit(card.Center, 0.1f) && !Palmed;
            exposure = exposed ? exposure + dt : 0;
            if (exposure > 0.3f) { Expose("The audience spotted the card. Bring the empty hand over it before crossing the light."); return; }
            if (Step == 0)
            {
                proof = open > 0.9f && !card.inside && FrameVisible && Lit(Slot) ? proof + dt : 0;
                StepProgress = Mathf.Clamp01(proof / 0.8f);
                if (proof >= 0.8f) { Step = 1; StepProgress = 0; }
            }
            else if (Step == 1 && card.inside) Step = 2;
            else if (Step == 2)
            {
                reveal = card.inside && open > 0.9f && FrameVisible && Lit(Slot) ? reveal + dt : 0;
                StepProgress = Mathf.Clamp01(reveal / 0.8f);
                if (reveal >= 0.8f) Step = 3;
            }
        }
        public override string Hint()
        {
            if (Step == 0) return "Stand to one SIDE, pull the frame UP, and show it EMPTY. Leave it OPEN for the secret load.";
            if (Step == 1) return card.Held ? "Keep the OTHER hand over the card. Carry both palms together; open the frame and release the card into its center."
                : "The card waits in the RIGHT wing. Raise it, switch hands with F, and cover it with the empty palm.";
            return "Step to the RIGHT of the frame and move the empty hand away. Pull UP to reveal the card with a CLEAR VIEW.";
        }
        public override void AddDemo(TrickDemo d)
        {
            d.Say("1  Show the empty gallery frame. Every control is available from the start.")
             .Walk(7.5f).Hand(1).HandTo(() => ControlPoint(Shutter)).Grab().HandBy(Vector2.up * 0.7f).Until(() => Step >= 1)
             .Release()
             .Say("2  Take the secret card from the wing. Cover it with your OTHER palm before entering the light.")
             .Walk(9.1f).HandTo(() => card.Center).Grab().HandTo(() => game.Hands.BodyPosition + new Vector2(0.1f, 1.32f))
             .Hand(0).HandTo(() => card.Center).Wait(0.1f).Walk(6.4f).Hand(1).Release().Until(() => Step >= 2)
             .Say("3  The frame closes over the card. Open it once more, in full view.")
             .Walk(7.5f).HandTo(() => ControlPoint(Shutter)).Grab().HandBy(Vector2.up * 0.7f).Until(() => Finished);
        }
        protected override void Build()
        {
            IllusionScenery.Build(view, root, 0);
            frame = Frame("GalleryDisplay", Slot, new Vector2(1.05f, 1.25f), Gold);
            cover = view.Rect("DisplayShutter", root, Slot, new Vector2(0.98f, 1.18f), Wine, 22).transform;
            view.Rect("DisplayStand", root, new Vector2(6.5f, 1.5f), new Vector2(0.12f, 0.75f), Gold, 17);
            Lever(Shutter, new Vector2(7.05f, 2.1f), Vector2.up * 0.35f, Vector2.up, () => open, v => open = v);
            Prop(CardId, card);
            cardT = view.Group("SecretPlayingCard", root, Vector2.zero);
            view.Rect("CardFace", cardT, new Vector2(0, 0.16f), new Vector2(0.22f, 0.32f), Cream, 23);
            var pip = view.Rect("Diamond", cardT, new Vector2(0, 0.16f), new Vector2(0.08f, 0.08f), Wine, 24);
            pip.transform.localRotation = Quaternion.Euler(0, 0, 45);
        }
        public override void Draw()
        {
            cardT.position = card.position; SetDepth(cardT, card.inside ? 0.21f : card.depth);
            StageDepth.Set(frame, 0.3f);
            Cover(cover, new Vector2(Slot.x, Slot.y - 0.59f), new Vector2(0.98f, 1.18f), 1 - open); StageDepth.Set(cover, 0.1f);
            DrawControls();
        }
    }
}
