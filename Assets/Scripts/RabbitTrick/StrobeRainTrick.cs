using UnityEngine;

namespace Sleight.RabbitTrick
{
    public sealed class StrobeRainTrick : ApparatusTrick
    {
        const int Pump = 0, Frequency = 1;
        const float RainX = 6.7f;
        Transform[] drops;
        Transform flowBar, frequencyBar, heatBar, flywheel;
        float flow, frequency = 0.18f, heat, phase, proof, frozenProof, risingProof;
        public float Flow => flow;
        public bool Frozen => flow > 0.6f && Mathf.Abs(frequency - 0.6f) < 0.045f;
        public bool Rising => flow > 0.6f && Mathf.Abs(frequency - 0.82f) < 0.045f;
        public override string Name => "THE STILL RAIN";
        public override string SuccessTitle => "THE RAIN STOPPED, THEN ROSE INTO THE SKY!";
        public override string[] StepNames { get; } = { "01  MAKE THE RAIN FALL IN THE LIGHT", "02  MATCH THE STROBE / FREEZE THE RAIN", "03  RETUNE / MAKE THE WATER RISE" };
        public override string Status => "PUMP: " + Mathf.RoundToInt(flow * 100) + "%   |   STROBE: " + Mathf.RoundToInt(frequency * 100) + "%   |   WATER: " + (Frozen ? "FROZEN" : Rising ? "RISING" : "FALLING") + "   |   HEAT: " + Mathf.RoundToInt(heat * 100) + "%";
        public override float StartLightX => RainX;
        protected override void OnReset() { flow = heat = phase = proof = frozenProof = risingProof = 0; frequency = 0.18f; }
        public override void DirectAudience(float dt) => game.Audience.Follow(RainX, 8, "WATER COLUMN");
        public override void Tick(float dt)
        {
            if (HeldBy(Pump) < 0) flow = Mathf.MoveTowards(flow, 0, dt * 0.45f);
            heat = Mathf.Clamp01(heat + (flow > 0.95f || frequency > 0.98f ? dt / 2.5f : -dt * 0.3f));
            if (heat >= 1) { Expose("The hidden pump overheated. Keep the pump near 75%, not at full pressure."); return; }
            if (!Frozen) phase += dt * (Rising ? -0.45f : 0.75f) * flow;
            bool watched = Lit(new Vector2(RainX, 2.7f));
            if (Step == 0)
            {
                proof = flow > 0.65f && watched ? proof + dt : 0;
                StepProgress = Mathf.Clamp01(proof / 0.8f);
                if (proof >= 0.8f) { Step = 1; StepProgress = 0; }
            }
            else if (Step == 1)
            {
                frozenProof = Frozen && watched && HeldBy(Pump) >= 0 ? frozenProof + dt : 0;
                StepProgress = Mathf.Clamp01(frozenProof / 1.2f);
                if (frozenProof >= 1.2f) { Step = 2; StepProgress = 0; }
            }
            else if (Step == 2)
            {
                risingProof = Rising && watched && HeldBy(Pump) >= 0 ? risingProof + dt : 0;
                StepProgress = Mathf.Clamp01(risingProof / 1.2f);
                if (risingProof >= 1.2f) Step = 3;
            }
        }
        public override string Hint()
        {
            if (Step == 0) return "Hold the LEFT pump handle around 75%. Keep holding E, then switch hands with F; the pump must stay powered.";
            if (Step == 1) return "With the OTHER hand, tune the RIGHT strobe dial to 60%. Hold both controls steady and watch the drops stop.";
            return "Keep the LEFT pump near 75%. Raise the RIGHT strobe dial to 82%: the rain appears to flow UP, in the spotlight.";
        }
        public override void AddDemo(TrickDemo d)
        {
            d.Say("1  Two hands, two jobs: the LEFT hand keeps the water flowing.")
             .Walk(4.95f).Hand(0).HandTo(() => ControlPoint(Pump)).Grab().HandBy(Vector2.up * 0.525f).Until(() => Step >= 1)
             .Say("2  The OTHER hand matches the strobe to the droplets. At 60%, falling water appears frozen.")
             .Hand(1).HandTo(() => ControlPoint(Frequency)).Grab().HandBy(Vector2.up * 0.294f).Until(() => Step >= 2)
             .Say("3  A small frequency shift reverses the apparent motion. Keep the pump powered.")
             .HandBy(Vector2.up * 0.154f).Until(() => Finished);
        }
        protected override void Build()
        {
            IllusionScenery.Build(view, root, 3);
            view.Rect("RainNozzle", root, new Vector2(RainX, 3.9f), new Vector2(1.15f, 0.13f), Gold, 20);
            view.Rect("CatchBasin", root, new Vector2(RainX, 1.32f), new Vector2(1.5f, 0.25f), new Color32(61, 112, 135, 255), 20);
            view.Rect("ControlConsole", root, new Vector2(4.95f, 1.75f), new Vector2(1.9f, 0.9f), Ink, 18);
            view.Rect("ConsoleTrim", root, new Vector2(4.95f, 2.2f), new Vector2(2f, 0.06f), Gold, 19);
            view.Rect("StrobeProjector", root, new Vector2(2.8f, 2.6f), new Vector2(0.6f, 0.42f), Gold, 20);
            view.Disc("ProjectorLens", root, new Vector2(3.12f, 2.6f), new Vector2(0.15f, 0.25f), Teal, 21);
            var ray = view.Line("StrobeBeam", root, 0.04f, new Color(0.3f, 0.7f, 0.9f, 0.25f), 19, 2);
            ray.SetPosition(0, new Vector2(3.1f, 2.6f)); ray.SetPosition(1, new Vector2(RainX, 2.6f));
            flywheel = view.Disc("StrobeWheel", root, new Vector2(2.8f, 2.6f), new Vector2(0.32f, 0.32f), Ink, 21).transform;
            drops = new Transform[21];
            for (int i = 0; i < drops.Length; i++) drops[i] = view.Disc("WaterDrop", root, Vector2.zero, new Vector2(0.065f, 0.13f), Teal, 24).transform;
            flowBar = view.Rect("Pressure", root, new Vector2(4.5f, 3.1f), new Vector2(0.9f, 0.07f), Teal, 22).transform;
            frequencyBar = view.Rect("Frequency", root, new Vector2(5.5f, 3.1f), new Vector2(0.9f, 0.07f), Gold, 22).transform;
            heatBar = view.Rect("Heat", root, new Vector2(4.95f, 3.4f), new Vector2(1.8f, 0.05f), Wine, 22).transform;
            foreach (float value in new[] { 0.6f, 0.82f }) view.Disc("FrequencyMark", root, new Vector2(5.05f + value * 0.9f, 3.1f), new Vector2(0.08f, 0.14f), Cream, 24);
            Lever(Pump, new Vector2(4.45f, 2f), Vector2.up * 0.35f, Vector2.up, () => flow, v => flow = v);
            Lever(Frequency, new Vector2(5.55f, 2f), Vector2.up * 0.35f, Vector2.up, () => frequency, v => frequency = v);
        }
        public override void Draw()
        {
            for (int i = 0; i < drops.Length; i++)
            {
                drops[i].gameObject.SetActive(flow > 0.04f);
                drops[i].position = new Vector2(RainX + (i % 3 - 1) * 0.3f, 1.55f + Mathf.Repeat(i / 21f - phase, 1) * 2.2f);
                StageDepth.Set(drops[i], 0.3f);
            }
            flywheel.localRotation = Quaternion.Euler(0, 0, -phase * 360 * (1 + frequency));
            Bar(flowBar, new Vector2(4.05f, 3.1f), 0.9f, flow);
            Bar(frequencyBar, new Vector2(5.05f, 3.1f), 0.9f, frequency);
            Bar(heatBar, new Vector2(4.05f, 3.4f), 1.8f, heat);
            DrawControls();
        }
    }
}
