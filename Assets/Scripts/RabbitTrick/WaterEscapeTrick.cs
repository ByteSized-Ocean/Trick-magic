using UnityEngine;

namespace Sleight.RabbitTrick
{
    public sealed class WaterEscapeTrick : ApparatusTrick
    {
        const int Curtain = 0, Valve = 1, Latch = 2;
        const float TankX = 5.2f, BalconyX = 2.05f, ClockX = 8.15f;
        Transform tank, waterT, veil, assistant, balcony, clockHand, airT;
        float open, valve = 0.5f, latch, water, air, proof, emptySeen, exitSeen, clockAge;
        public bool Escaped { get; private set; }
        public float Water => water;
        public float Air => air;
        public override string Name => "THE WATER VAULT ESCAPE";
        public override string SuccessTitle => "SHE ESCAPED THE SEALED WATER VAULT!";
        public override string[] StepNames { get; } = { "01  FLOOD THE TANK IN THE LIGHT", "02  DRAIN / RELEASE THE SAFETY LATCH", "03  REVEAL THE EMPTY TANK AND BALCONY" };
        public override string Status => "WATER: " + Mathf.RoundToInt(water * 100) + "%   |   AIR: " + Mathf.CeilToInt(air) + "s   |   ESCAPE: " + (Escaped ? "CLEAR" : "LOCKED");
        public override float StartLightX => TankX;
        protected override void OnReset()
        { open = latch = water = proof = emptySeen = exitSeen = clockAge = 0; valve = 0.5f; air = 28; Escaped = false; }
        public override void DirectAudience(float dt)
        {
            float x = Step == 1 ? ClockX : Step == 2 && emptySeen >= 0.8f ? BalconyX : TankX;
            game.Audience.Follow(x, 7f, x == ClockX ? "ESCAPE CLOCK" : x == BalconyX ? "BALCONY ASSISTANT" : "WATER TANK");
        }
        public override void Tick(float dt)
        {
            clockAge += dt;
            if (HeldBy(Valve) < 0) valve = Mathf.MoveTowards(valve, 0.5f, dt * 0.7f);
            if (HeldBy(Latch) < 0) latch = Mathf.MoveTowards(latch, 0, dt * 1.8f);
            water = Mathf.Clamp01(water + (valve - 0.5f) * 2f * dt);
            if (water > 0.55f && !Escaped) air -= dt;
            if (air <= 0) { Expose("The air clock ran out. Drain the tank while the OTHER hand releases the latch."); return; }
            bool hatchSeen = latch > 0.15f && open > 0.2f && Lit(new Vector2(TankX, 2.2f));
            if (hatchSeen) { Expose("They saw the rear escape hatch. Lower the front curtain before opening the latch."); return; }
            if (!Escaped && latch > 0.85f && valve < 0.25f && HeldBy(Valve) >= 0 && water < 0.2f)
            {
                Escaped = true;
                if (Step >= 1) Step = 2;
            }
            if (Step == 0)
            {
                if (Escaped && open < 0.1f && latch < 0.1f) { Escaped = false; air = 28; }
                proof = !Escaped && water > 0.7f && open > 0.85f && Lit(new Vector2(TankX, 2.4f)) ? proof + dt : 0;
                StepProgress = Mathf.Clamp01(proof / 0.8f);
                if (proof >= 0.8f) { Step = 1; StepProgress = 0; }
            }
            else if (Step == 2)
            {
                if (Escaped && water < 0.25f && latch < 0.1f && open > 0.85f && Lit(new Vector2(TankX, 2.4f))) emptySeen += dt;
                if (emptySeen >= 0.8f && Lit(new Vector2(BalconyX, 2.6f))) exitSeen += dt;
                StepProgress = Mathf.Clamp01((Mathf.Min(emptySeen, 0.8f) + exitSeen) / 1.6f);
                if (exitSeen >= 0.8f) Step = 3;
            }
        }
        public override string Hint()
        {
            if (Step == 0) return "Open the front curtain UP. Hold the LEFT valve UP to fill the tank and show the trapped assistant IN THE LIGHT.";
            if (Step == 1) return "Close the curtain DOWN. Keep the LEFT valve DOWN to drain; use the OTHER hand to pull the RIGHT safety latch UP.";
            return "Release the latch and valve. Open the curtain UP: show the empty tank, then let the light find her on the LEFT balcony.";
        }
        public override void AddDemo(TrickDemo d)
        {
            d.Say("1  Open the tank's curtain and fill the sealed tank in front of the audience.")
             .Walk(6.85f).Hand(1).HandTo(() => ControlPoint(Curtain)).Grab().HandBy(Vector2.up * 0.7f).Release()
             .Hand(0).HandTo(() => ControlPoint(Valve)).Grab().HandBy(Vector2.up * 0.25f).Until(() => Step >= 1)
             .Say("2  Hide the hatch. Hold the drain valve DOWN with one hand and the safety latch UP with the other.")
             .Hand(1).HandTo(() => ControlPoint(Curtain)).Grab().HandBy(Vector2.down * 0.7f).Release()
             .Hand(0).HandBy(Vector2.down * 0.55f)
             .Hand(1).HandTo(() => ControlPoint(Latch)).Grab().HandBy(Vector2.up * 0.7f).Until(() => Escaped).Release()
             .Hand(0).Release().Wait(0.6f)
             .Say("3  The empty tank and the balcony are BOTH shown in the spotlight.")
             .Hand(1).HandTo(() => ControlPoint(Curtain)).Grab().HandBy(Vector2.up * 0.7f).Until(() => Finished);
        }
        protected override void Build()
        {
            IllusionScenery.Build(view, root, 1);
            tank = Frame("SealedWaterTank", new Vector2(TankX, 2.35f), new Vector2(1.7f, 2.25f), Gold);
            waterT = view.Rect("TankWater", root, new Vector2(TankX, Floor), new Vector2(1.5f, 2.05f), new Color(0.18f, 0.65f, 0.76f, 0.65f), 23).transform;
            assistant = Person("EscapeArtist", new Color32(181, 80, 115, 255));
            veil = view.Rect("EscapeCurtain", root, new Vector2(TankX, 2.35f), new Vector2(1.65f, 2.2f), Wine, 26).transform;
            balcony = Frame("Balcony", new Vector2(BalconyX, 2.4f), new Vector2(1.25f, 2.25f), Gold);
            view.Rect("BalconyFloor", root, new Vector2(BalconyX, 1.3f), new Vector2(1.5f, 0.12f), Gold, 20);
            view.Disc("EscapeClock", root, new Vector2(ClockX, 3.1f), new Vector2(1.4f, 1.4f), Gold, 18);
            view.Disc("ClockFace", root, new Vector2(ClockX, 3.1f), new Vector2(1.27f, 1.27f), Ink, 19);
            clockHand = view.Rect("ClockHand", root, new Vector2(ClockX, 3.1f), new Vector2(0.04f, 0.5f), Cream, 22).transform;
            airT = view.Rect("AirRemaining", root, new Vector2(5.2f, 3.7f), new Vector2(1.6f, 0.08f), Teal, 24).transform;
            view.Rect("RemoteValveDesk", root, new Vector2(6.85f, 1.62f), new Vector2(1.75f, 0.55f), Ink, 18);
            view.Rect("DeskTrim", root, new Vector2(6.85f, 1.9f), new Vector2(1.85f, 0.05f), Gold, 19);
            Lever(Curtain, new Vector2(7.5f, 2.2f), Vector2.up * 0.35f, Vector2.up, () => open, v => open = v);
            Lever(Valve, new Vector2(6.3f, 2.0f), Vector2.up * 0.35f, Vector2.up, () => valve, v => valve = v);
            Lever(Latch, new Vector2(7.4f, 1.9f), Vector2.up * 0.35f, Vector2.up, () => latch, v => latch = v);
        }
        public override void Draw()
        {
            StageDepth.Set(tank, 0.4f); StageDepth.Set(balcony, 0.4f);
            assistant.position = new Vector2(Escaped ? BalconyX : TankX, Escaped ? 1.35f : 1.3f); StageDepth.Set(assistant, 0.25f);
            Cover(waterT, new Vector2(TankX, 1.3f), new Vector2(1.5f, 2.05f), water); StageDepth.Set(waterT, 0.22f);
            Cover(veil, new Vector2(TankX, 1.25f), new Vector2(1.65f, 2.2f), 1 - open); StageDepth.Set(veil, 0.1f);
            clockHand.localRotation = Quaternion.Euler(0, 0, -clockAge * 60);
            Bar(airT, new Vector2(4.4f, 3.7f), 1.6f, air / 28f);
            DrawControls();
        }
    }
}
